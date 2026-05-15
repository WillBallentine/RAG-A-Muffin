using RagAMuffin.Auth;
using RagAMuffin.Models;
using RagAMuffin.Services.ExternalApps;
using RagAMuffin.Services.Interfaces;

namespace RagAMuffin.Services.Connectors
{
    public class GmailConnector : IConnector
    {
        private readonly IEmailParser _parser;
        private readonly ILogger<GmailConnector> _logger;
        private readonly string _userId;
        private readonly int _maxPerSync;

        public string SourceType => "gmail";

        public GmailConnector(
            IEmailParser parser,
            ILogger<GmailConnector> logger,
            IConfiguration configuration)
        {
            _parser = parser;
            _logger = logger;
            _userId = configuration["Ingestion:UserId"] ?? string.Empty;
            _maxPerSync = int.TryParse(configuration["Ingestion:MaxEmailsPerSync"], out var max) ? max : 100;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_userId))
            {
                _logger.LogWarning("GmailConnector: Ingestion:UserId not configured — skipping");
                return [];
            }

            if (!await GoogleAuth.HasStoredCredentialAsync(_userId))
            {
                _logger.LogWarning("GmailConnector: no stored credentials for {UserId} — skipping", _userId);
                return [];
            }

            var service = await GoogleAuth.CreateGmailServiceAsync(_userId);

            var inbox = await Gmail.FetchInboxAsync(service, _maxPerSync);
            var sent  = await Gmail.FetchSentAsync(service, _maxPerSync);
            var all   = inbox.Concat(sent).ToList();

            _logger.LogInformation("GmailConnector: fetched {Inbox} inbox + {Sent} sent = {Total} messages",
                inbox.Count, sent.Count, all.Count);

            var documents = new List<SourceDocument>(all.Count);
            foreach (var message in all)
            {
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
            }

            return documents;
        }
    }
}
