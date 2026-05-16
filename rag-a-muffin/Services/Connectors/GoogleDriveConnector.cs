using Google.Apis.Drive.v3;
using RagAMuffin.Auth;
using RagAMuffin.Models;
using RagAMuffin.Services.Extractors;
using RagAMuffin.Services.Interfaces;

namespace RagAMuffin.Services.Connectors
{
    public class GoogleDriveConnector : IConnector
    {
        private readonly UserProfileService _profile;
        private readonly IEnumerable<IDocumentExtractor> _extractors;
        private readonly ILogger<GoogleDriveConnector> _logger;
        private readonly IConfiguration _config;

        public string SourceType => "drive";

        public GoogleDriveConnector(
            UserProfileService profile,
            IEnumerable<IDocumentExtractor> extractors,
            ILogger<GoogleDriveConnector> logger,
            IConfiguration config)
        {
            _profile    = profile;
            _extractors = extractors;
            _logger     = logger;
            _config     = config;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_profile.UserId))
            {
                _logger.LogWarning("GoogleDriveConnector: no user configured — complete setup first");
                return [];
            }

            if (!await GoogleAuth.HasStoredCredentialAsync(_profile.UserId))
            {
                _logger.LogWarning("GoogleDriveConnector: no stored credentials for {UserId}", _profile.UserId);
                return [];
            }

            DriveService service;
            try
            {
                service = await GoogleAuth.CreateDriveServiceAsync(_profile.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoogleDriveConnector: failed to create Drive service");
                return [];
            }

            var maxFiles  = _config.GetValue<int>("Connectors:Drive:MaxFiles", 50);
            var folderIds = _config.GetSection("Connectors:Drive:FolderIds").Get<string[]>() ?? [];

            List<Google.Apis.Drive.v3.Data.File> files;
            try
            {
                files = await ListFilesAsync(service, folderIds, maxFiles, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoogleDriveConnector: failed to list Drive files — " +
                    "you may need to re-authorize to grant Drive access");
                return [];
            }

            var documents = new List<SourceDocument>();
            foreach (var file in files)
            {
                try
                {
                    var body = await ExtractFileAsync(service, file, ct);
                    if (string.IsNullOrWhiteSpace(body)) continue;

                    var publishedAt = file.ModifiedTimeDateTimeOffset?.UtcDateTime ?? DateTime.UtcNow;

                    documents.Add(new SourceDocument
                    {
                        Id          = "drive_" + file.Id,
                        SourceType  = SourceType,
                        Title       = file.Name ?? "(untitled)",
                        Author      = _profile.UserId!,
                        Url         = file.WebViewLink,
                        Body        = body,
                        PublishedAt = publishedAt,
                        Metadata    = new Dictionary<string, string>
                        {
                            ["driveFileId"] = file.Id ?? string.Empty,
                            ["mimeType"]    = file.MimeType ?? string.Empty
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "GoogleDriveConnector: failed to process '{Name}'", file.Name);
                }
            }

            _logger.LogInformation("GoogleDriveConnector: processed {Count} file(s)", documents.Count);
            return documents;
        }

        private static async Task<List<Google.Apis.Drive.v3.Data.File>> ListFilesAsync(
            DriveService service, string[] folderIds, int maxFiles, CancellationToken ct)
        {
            var files = new List<Google.Apis.Drive.v3.Data.File>();

            if (folderIds.Length > 0)
            {
                foreach (var folderId in folderIds)
                {
                    var req = service.Files.List();
                    req.Q        = $"'{folderId}' in parents and trashed=false";
                    req.Fields   = "files(id,name,mimeType,modifiedTime,webViewLink)";
                    req.PageSize = Math.Min(maxFiles, 1000);
                    var result = await req.ExecuteAsync(ct);
                    files.AddRange(result.Files ?? []);
                }
            }
            else
            {
                var req = service.Files.List();
                req.Q        = "trashed=false and mimeType != 'application/vnd.google-apps.folder'";
                req.OrderBy  = "modifiedTime desc";
                req.Fields   = "files(id,name,mimeType,modifiedTime,webViewLink)";
                req.PageSize = Math.Min(maxFiles, 1000);
                var result = await req.ExecuteAsync(ct);
                files.AddRange(result.Files ?? []);
            }

            return files;
        }

        private async Task<string?> ExtractFileAsync(
            DriveService service, Google.Apis.Drive.v3.Data.File file, CancellationToken ct)
        {
            switch (file.MimeType)
            {
                case "application/vnd.google-apps.document":
                {
                    using var ms = new MemoryStream();
                    await service.Files.Export(file.Id, "text/plain").DownloadAsync(ms, ct);
                    ms.Seek(0, SeekOrigin.Begin);
                    return await new StreamReader(ms).ReadToEndAsync(ct);
                }
                case "application/vnd.google-apps.spreadsheet":
                {
                    using var ms = new MemoryStream();
                    await service.Files.Export(file.Id, "text/csv").DownloadAsync(ms, ct);
                    ms.Seek(0, SeekOrigin.Begin);
                    return await new StreamReader(ms).ReadToEndAsync(ct);
                }
            }

            var ext = Path.GetExtension(file.Name ?? "").ToLowerInvariant();
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(ext));
            if (extractor is null)
            {
                _logger.LogDebug("GoogleDriveConnector: no extractor for '{Name}' ({MimeType}) — skipping",
                    file.Name, file.MimeType);
                return null;
            }

            using var dlMs = new MemoryStream();
            await service.Files.Get(file.Id).DownloadAsync(dlMs, ct);
            dlMs.Seek(0, SeekOrigin.Begin);
            return await extractor.ExtractAsync(dlMs, ct);
        }
    }
}
