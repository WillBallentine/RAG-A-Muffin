using RagAMuffin.Models;
using RagAMuffin.Services.Extractors;
using RagAMuffin.Services.Interfaces;
using System.Security.Cryptography;

namespace RagAMuffin.Services.Connectors
{
    public class LocalDirectoryConnector : IConnector
    {
        private readonly ConnectorConfigService _connectorConfig;
        private readonly IEnumerable<IDocumentExtractor> _extractors;
        private readonly ILogger<LocalDirectoryConnector> _logger;
        private static readonly long MaxBytes = 50 * 1024 * 1024;

        public string SourceType => "local";

        public LocalDirectoryConnector(
            ConnectorConfigService connectorConfig,
            IEnumerable<IDocumentExtractor> extractors,
            ILogger<LocalDirectoryConnector> logger)
        {
            _connectorConfig = connectorConfig;
            _extractors      = extractors;
            _logger          = logger;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            var dirs = _connectorConfig.Current.LocalDirectories;
            if (dirs.Count == 0) return [];

            var documents = new List<SourceDocument>();

            foreach (var dir in dirs)
            {
                if (!Directory.Exists(dir))
                {
                    _logger.LogWarning("LocalDirectoryConnector: '{Dir}' does not exist — skipping", dir);
                    continue;
                }

                IEnumerable<string> files;
                try
                {
                    files = Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "LocalDirectoryConnector: failed to enumerate '{Dir}'", dir);
                    continue;
                }

                foreach (var filePath in files)
                {
                    if (ct.IsCancellationRequested) break;

                    var ext       = Path.GetExtension(filePath).ToLowerInvariant();
                    var extractor = _extractors.FirstOrDefault(e => e.CanHandle(ext));
                    if (extractor is null) continue;

                    var docId = Convert.ToHexString(SHA256.HashData(
                        System.Text.Encoding.UTF8.GetBytes($"local:{Path.GetFullPath(filePath)}")))
                        .ToLowerInvariant();

                    byte[] bytes;
                    try
                    {
                        var fi = new FileInfo(filePath);
                        if (fi.Length > MaxBytes)
                        {
                            _logger.LogWarning(
                                "LocalDirectoryConnector: '{File}' is {MB:F1} MB — exceeds 50 MB limit, skipping",
                                filePath, fi.Length / 1_048_576.0);
                            continue;
                        }
                        bytes = await File.ReadAllBytesAsync(filePath, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "LocalDirectoryConnector: failed to read '{File}'", filePath);
                        continue;
                    }

                    string text;
                    try
                    {
                        using var ms = new MemoryStream(bytes);
                        text = await extractor.ExtractAsync(ms, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "LocalDirectoryConnector: extraction failed for '{File}'", filePath);
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    documents.Add(new SourceDocument
                    {
                        Id          = docId,
                        SourceType  = SourceType,
                        Title       = Path.GetFileNameWithoutExtension(filePath),
                        Author      = "local",
                        Body        = text,
                        PublishedAt = File.GetLastWriteTimeUtc(filePath),
                        Metadata    = new Dictionary<string, string>
                        {
                            ["filePath"]  = filePath,
                            ["filename"]  = Path.GetFileName(filePath),
                            ["directory"] = dir
                        }
                    });

                    _logger.LogInformation("LocalDirectoryConnector: indexed '{File}' ({KB} KB)",
                        Path.GetFileName(filePath), bytes.Length / 1024);
                }
            }

            _logger.LogInformation(
                "LocalDirectoryConnector: indexed {Count} document(s) from {Dirs} director(ies)",
                documents.Count, dirs.Count);

            return documents;
        }
    }
}
