using RagAMuffin.Models;
using RagAMuffin.Services.Extractors;
using RagAMuffin.Services.Interfaces;
using System.Security.Cryptography;

namespace RagAMuffin.Services
{
    public class FileIngestionService
    {
        private readonly IEnumerable<IDocumentExtractor> _extractors;
        private readonly IIngestionPipeline _pipeline;
        private readonly ILogger<FileIngestionService> _logger;

        private static readonly long MaxBytes = 50 * 1024 * 1024; // 50 MB

        public FileIngestionService(
            IEnumerable<IDocumentExtractor> extractors,
            IIngestionPipeline pipeline,
            ILogger<FileIngestionService> logger)
        {
            _extractors = extractors;
            _pipeline   = pipeline;
            _logger     = logger;
        }

        // Returns the document ID (content hash) on success, null on skip/failure.
        public async Task<string?> IngestAsync(Stream stream, string filename, CancellationToken ct = default)
        {
            var ext = Path.GetExtension(filename).ToLowerInvariant();
            var extractor = _extractors.FirstOrDefault(e => e.CanHandle(ext));
            if (extractor is null)
            {
                _logger.LogWarning("No extractor for '{Ext}' — skipping {Filename}", ext, filename);
                return null;
            }

            // Buffer to memory so we can hash and extract from the same bytes
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms, ct);

            if (ms.Length > MaxBytes)
            {
                _logger.LogWarning("File '{Filename}' is {MB:F1} MB — exceeds 50 MB limit, skipping",
                    filename, ms.Length / 1_048_576.0);
                return null;
            }

            var bytes = ms.ToArray();
            var hash  = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

            using var extractStream = new MemoryStream(bytes);
            string text;
            try
            {
                text = await extractor.ExtractAsync(extractStream, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Extraction failed for '{Filename}'", filename);
                return null;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning("Extracted empty text from '{Filename}' — skipping", filename);
                return null;
            }

            var doc = new SourceDocument
            {
                Id          = hash,
                SourceType  = ext.TrimStart('.'),
                Title       = Path.GetFileNameWithoutExtension(filename),
                Author      = "local",
                Body        = text,
                PublishedAt = DateTime.UtcNow,
                Metadata    = new Dictionary<string, string>
                {
                    ["filename"] = filename,
                    ["fileSize"] = bytes.Length.ToString()
                }
            };

            await _pipeline.IngestAsync([doc], ct);
            _logger.LogInformation("Ingested '{Filename}' ({KB} KB, id={Id})",
                filename, bytes.Length / 1024, hash[..8]);
            return hash;
        }
    }
}
