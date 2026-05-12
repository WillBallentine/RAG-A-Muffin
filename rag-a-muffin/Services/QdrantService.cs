using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagAMuffin.Services
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "emails";
        private readonly ILogger<QdrantVectorStore> _logger;

        public QdrantVectorStore(ILogger<QdrantVectorStore> logger, QdrantClient client)
        {
            _logger = logger;
            _client = client;
        }

        public async Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default)
        {
            var point = new PointStruct
            {
                Id = Guid.NewGuid(),        // PointId has implicit cast from Guid
                Vectors = chunk.Vector,        // implicit cast from float[]
                Payload =
            {
                ["emailId"]     = chunk.EmailId,
                ["threadId"]    = chunk.ThreadId,
                ["subject"]     = chunk.Subject,
                ["from"]        = chunk.From,
                ["date"]        = chunk.Date.ToString("O"),  // ISO 8601
                ["chunkIndex"]  = chunk.ChunkIndex,
                ["totalChunks"] = chunk.TotalChunks,
                ["text"]        = chunk.Text
            }
            };
            _logger.LogInformation("Upserting chunk for email '{EmailId}' with ID: {ChunkId}", chunk.EmailId, point.Id);

            await _client.UpsertAsync(CollectionName, [point], cancellationToken: ct);
        }

        public async Task DeleteByEmailIdAsync(string emailId, CancellationToken ct = default)
        {
            // Delete all chunks belonging to an email using a payload filter
            await _client.DeleteAsync(CollectionName, new Filter
            {
                Must =
            {
                new Condition
                {
                    Field = new FieldCondition
                    {
                        Key   = "emailId",
                        Match = new Match { Text = emailId }
                    }
                }
            }
            }, cancellationToken: ct);
        }
    }
}