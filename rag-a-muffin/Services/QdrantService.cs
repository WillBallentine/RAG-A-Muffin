using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using Google.Protobuf.Collections;
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
                Id = Guid.NewGuid(),
                Vectors = chunk.Vector,
                Payload =
                {
                    ["emailId"]        = chunk.EmailId,
                    ["threadId"]       = chunk.ThreadId,
                    ["subject"]        = chunk.Subject,
                    ["from"]           = chunk.From,
                    ["to"]             = chunk.To,
                    ["cc"]             = chunk.Cc,
                    ["date"]           = chunk.Date.ToString("O"),
                    ["labels"]         = chunk.Labels,
                    ["hasAttachments"] = chunk.HasAttachments ? 1 : 0,
                    ["direction"]      = chunk.Direction,
                    ["chunkIndex"]     = chunk.ChunkIndex,
                    ["totalChunks"]    = chunk.TotalChunks,
                    ["text"]           = chunk.Text
                }
            };

            _logger.LogInformation("Upserting chunk for email '{EmailId}' [{Direction}]", chunk.EmailId, chunk.Direction);
            await _client.UpsertAsync(CollectionName, [point], cancellationToken: ct);
        }

        public async Task<List<ScoredChunk>> SearchAsync(float[] queryVector, int topK = 5, CancellationToken ct = default)
        {
            var results = await _client.SearchAsync(
                CollectionName,
                queryVector,
                limit: (ulong)topK,
                cancellationToken: ct
            );

            return results.Select(r => MapPayload(r.Payload, r.Score)).ToList();
        }

        public async Task<List<ScoredChunk>> ScrollBySenderAsync(string field, string name, int limit, CancellationToken ct = default)
        {
            // Uses the full-text payload index created at startup.
            // Match { Text } tokenizes the name and does word-level matching on the field,
            // so "Mike Maseda" finds "Mike Maseda <mike@example.com>" correctly.
            // Dummy zero vector because we're filtering by payload, not by similarity.
            var results = await _client.SearchAsync(
                CollectionName,
                new float[768],
                filter: new Filter
                {
                    Must =
                    {
                        new Condition
                        {
                            Field = new FieldCondition
                            {
                                Key   = field,
                                Match = new Match { Text = name }
                            }
                        }
                    }
                },
                limit: (ulong)limit,
                cancellationToken: ct
            );

            return results.Select(r => MapPayload(r.Payload, 1.0f)).ToList();
        }

        public async Task<bool> EmailExistsAsync(string emailId, CancellationToken ct = default)
        {
            var results = await _client.SearchAsync(
                CollectionName,
                new float[768],
                filter: new Filter
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
                },
                limit: 1,
                cancellationToken: ct
            );

            return results.Any();
        }

        public async Task DeleteByEmailIdAsync(string emailId, CancellationToken ct = default)
        {
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

        private static ScoredChunk MapPayload(MapField<string, Value> payload, float score) =>
            new ScoredChunk
            {
                EmailId        = payload["emailId"].StringValue,
                ThreadId       = payload["threadId"].StringValue,
                Subject        = payload["subject"].StringValue,
                From           = payload["from"].StringValue,
                To             = payload["to"].StringValue,
                Cc             = payload["cc"].StringValue,
                Date           = payload["date"].StringValue,
                Labels         = payload["labels"].StringValue,
                HasAttachments = payload["hasAttachments"].IntegerValue > 0,
                Direction      = payload["direction"].StringValue,
                Text           = payload["text"].StringValue,
                Score          = score
            };
    }
}
