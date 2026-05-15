using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;
using System.Text;

namespace RagAMuffin.Services
{
    public class IngestionPipeline : IIngestionPipeline
    {
        private readonly ILogger<IngestionPipeline> _logger;
        private readonly IEmailParser _parser;
        private readonly IChunker _chunker;
        private readonly IEmbeddingService _embedder;
        private readonly IVectorStore _vectorStore;

        public IngestionPipeline(ILogger<IngestionPipeline> logger, IEmailParser parser, IChunker chunker, IEmbeddingService embedder, IVectorStore vectorStore)
        {
            _logger = logger;
            _parser = parser;
            _chunker = chunker;
            _embedder = embedder;
            _vectorStore = vectorStore;
        }

        public async Task IngestAsync(IEnumerable<Message> messages)
        {
            foreach (var message in messages)
            {
                _logger.LogInformation("Processing email with ID: {EmailId}", message.Id);
                var parsed = _parser.ParsedEmail(message);
                if (parsed is null)
                {
                    _logger.LogInformation("Skipping message {Id} — empty or too short after parsing", message.Id);
                    continue;
                }

                if (await _vectorStore.EmailExistsAsync(parsed.Id))
                {
                    _logger.LogInformation("Email {EmailId} already ingested, skipping", parsed.Id);
                    continue;
                }

                var chunks = _chunker.Chunk(parsed);
                foreach (var chunk in chunks)
                {
                    float[] vector;
                    try
                    {
                        vector = await _embedder.EmbedAsync(BuildEmbedText(parsed, chunk.Text));
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to embed chunk {ChunkIndex} for email {EmailId}. Skipping.", chunk.Index, parsed.Id);
                        continue;
                    }

                    await _vectorStore.UpsertAsync(new EmbeddedChunk
                    {
                        EmailId = parsed.Id,
                        ThreadId = parsed.ThreadId,
                        Subject = parsed.Subject,
                        From = parsed.From,
                        To = parsed.To,
                        Cc = parsed.Cc,
                        Date = parsed.Date,
                        Labels = parsed.Labels,
                        HasAttachments = parsed.HasAttachments,
                        Direction = parsed.Direction,
                        ChunkIndex = chunk.Index,
                        Text = chunk.Text,
                        TotalChunks = chunk.TotalChunks,
                        Vector = vector
                    });
                }
            }
        }

        private static string BuildEmbedText(ParsedEmail email, string chunkText)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"From: {email.From}");
            if (!string.IsNullOrWhiteSpace(email.To))
                sb.AppendLine($"To: {email.To}");
            if (!string.IsNullOrWhiteSpace(email.Cc))
                sb.AppendLine($"Cc: {email.Cc}");
            sb.AppendLine($"Subject: {email.Subject}");
            if (!string.IsNullOrWhiteSpace(email.Labels))
                sb.AppendLine($"Labels: {email.Labels}");
            if (email.HasAttachments)
                sb.AppendLine("Has attachments: yes");
            sb.AppendLine();
            sb.Append(chunkText);
            return sb.ToString();
        }
    }
}
