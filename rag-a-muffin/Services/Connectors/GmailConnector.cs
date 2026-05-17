using RagAMuffin.Auth;
using RagAMuffin.Models;
using RagAMuffin.Services.ExternalApps;
using RagAMuffin.Services.Extractors;
using RagAMuffin.Services.Interfaces;
using System.Security.Cryptography;

namespace RagAMuffin.Services.Connectors
{
    public class GmailConnector : IConnector
    {
        private readonly IEmailParser _parser;
        private readonly ConnectorConfigService _connectorConfig;
        private readonly IEnumerable<IDocumentExtractor> _extractors;
        private readonly ILogger<GmailConnector> _logger;
        private readonly string _userId;
        private readonly int _maxPerSync;

        public string SourceType => "gmail";

        public GmailConnector(
            IEmailParser parser,
            ConnectorConfigService connectorConfig,
            IEnumerable<IDocumentExtractor> extractors,
            ILogger<GmailConnector> logger,
            UserProfileService profile,
            IConfiguration configuration)
        {
            _parser          = parser;
            _connectorConfig = connectorConfig;
            _extractors      = extractors;
            _logger          = logger;
            _userId          = profile.UserId ?? string.Empty;
            _maxPerSync      = int.TryParse(configuration["Ingestion:MaxEmailsPerSync"], out var max) ? max : 100;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_userId))
            {
                _logger.LogWarning("GmailConnector: no user configured — complete setup to enable sync");
                return [];
            }

            if (!await GoogleAuth.HasStoredCredentialAsync(_userId))
            {
                _logger.LogWarning("GmailConnector: no stored credentials for {UserId} — skipping", _userId);
                return [];
            }

            var service = await GoogleAuth.CreateGmailServiceAsync(_userId);

            var labels = _connectorConfig.Current.GmailLabels is { Count: > 0 } l
                ? l
                : ["INBOX", "SENT"];

            var all = await Gmail.FetchByLabelsAsync(service, labels, _maxPerSync);

            _logger.LogInformation("GmailConnector: fetched {Total} messages across label(s): {Labels}",
                all.Count, string.Join(", ", labels));

            var documents = new List<SourceDocument>(all.Count);
            foreach (var message in all)
            {
                if (ct.IsCancellationRequested) break;

                var parsed = _parser.ParsedEmail(message);
                if (parsed is null) continue;

                documents.Add(new SourceDocument
                {
                    Id          = parsed.Id,
                    SourceType  = SourceType,
                    Title       = parsed.Subject,
                    Author      = parsed.From,
                    Recipient   = parsed.To,
                    Cc          = parsed.Cc,
                    Body        = parsed.Body,
                    PublishedAt = parsed.Date,
                    Metadata    = new Dictionary<string, string>
                    {
                        ["threadId"]       = parsed.ThreadId ?? string.Empty,
                        ["labels"]         = parsed.Labels ?? string.Empty,
                        ["hasAttachments"] = parsed.HasAttachments ? "true" : "false",
                        ["direction"]      = parsed.Direction ?? "received"
                    }
                });

                if (parsed.HasAttachments)
                {
                    var attachmentDocs = await ExtractAttachmentsAsync(service, message, parsed.From, parsed.Date, ct);
                    documents.AddRange(attachmentDocs);
                }
            }

            return documents;
        }

        private async Task<IEnumerable<SourceDocument>> ExtractAttachmentsAsync(
            Google.Apis.Gmail.v1.GmailService service,
            Google.Apis.Gmail.v1.Data.Message message,
            string from,
            DateTime date,
            CancellationToken ct)
        {
            var docs = new List<SourceDocument>();
            if (message.Payload?.Parts == null) return docs;

            foreach (var part in message.Payload.Parts)
            {
                if (string.IsNullOrEmpty(part.Filename)) continue;
                if (part.MimeType is "text/plain" or "text/html") continue;

                var ext       = Path.GetExtension(part.Filename).ToLowerInvariant();
                var extractor = _extractors.FirstOrDefault(e => e.CanHandle(ext));
                if (extractor is null) continue;

                byte[] data;
                try
                {
                    if (!string.IsNullOrEmpty(part.Body?.AttachmentId))
                    {
                        var attachment = await service.Users.Messages.Attachments
                            .Get("me", message.Id, part.Body.AttachmentId)
                            .ExecuteAsync(ct);
                        var b64 = attachment.Data.Replace('-', '+').Replace('_', '/');
                        data = Convert.FromBase64String(b64);
                    }
                    else if (!string.IsNullOrEmpty(part.Body?.Data))
                    {
                        var b64 = part.Body.Data.Replace('-', '+').Replace('_', '/');
                        data = Convert.FromBase64String(b64);
                    }
                    else continue;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GmailConnector: failed to download attachment '{File}' from {Id}",
                        part.Filename, message.Id);
                    continue;
                }

                string text;
                try
                {
                    using var ms = new MemoryStream(data);
                    text = await extractor.ExtractAsync(ms, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "GmailConnector: extraction failed for attachment '{File}'", part.Filename);
                    continue;
                }

                if (string.IsNullOrWhiteSpace(text)) continue;

                var stableId = Convert.ToHexString(SHA256.HashData(
                    System.Text.Encoding.UTF8.GetBytes($"gmail-attachment:{message.Id}/{part.Filename}")))
                    .ToLowerInvariant();

                docs.Add(new SourceDocument
                {
                    Id          = stableId,
                    SourceType  = SourceType,
                    Title       = part.Filename,
                    Author      = from,
                    Body        = text,
                    PublishedAt = date,
                    Metadata    = new Dictionary<string, string>
                    {
                        ["messageId"]    = message.Id,
                        ["filename"]     = part.Filename,
                        ["isAttachment"] = "true"
                    }
                });

                _logger.LogInformation("GmailConnector: extracted attachment '{File}' ({KB} KB)",
                    part.Filename, data.Length / 1024);
            }

            return docs;
        }
    }
}
