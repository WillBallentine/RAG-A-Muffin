using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using Google.Protobuf.Collections;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using System.Text.Json;

namespace RagAMuffin.Services
{
    public class QdrantVectorStore : IVectorStore
    {
        private readonly QdrantClient _client;
        private const string CollectionName = "documents";
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
                    ["documentId"]  = chunk.DocumentId,
                    ["sourceType"]  = chunk.SourceType,
                    ["title"]       = chunk.Title,
                    ["author"]      = chunk.Author,
                    ["recipient"]   = chunk.Recipient ?? string.Empty,
                    ["cc"]          = chunk.Cc ?? string.Empty,
                    ["url"]         = chunk.Url ?? string.Empty,
                    ["publishedAt"] = chunk.PublishedAt.ToString("O"),
                    ["chunkIndex"]  = chunk.ChunkIndex,
                    ["totalChunks"] = chunk.TotalChunks,
                    ["text"]        = chunk.Text,
                    ["metadata"]    = JsonSerializer.Serialize(chunk.Metadata)
                }
            };

            _logger.LogInformation("Upserting [{SourceType}] '{Title}' chunk {ChunkIndex}",
                chunk.SourceType, chunk.Title, chunk.ChunkIndex);
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

        public async Task<List<ScoredChunk>> SearchByFieldAsync(string field, string value, int limit, CancellationToken ct = default)
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
                                Key   = field,
                                Match = new Match { Text = value }
                            }
                        }
                    }
                },
                limit: (ulong)limit,
                cancellationToken: ct
            );

            return results.Select(r => MapPayload(r.Payload, 1.0f)).ToList();
        }

        public async Task<bool> DocumentExistsAsync(string documentId, CancellationToken ct = default)
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
                                Key   = "documentId",
                                Match = new Match { Keyword = documentId }
                            }
                        }
                    }
                },
                limit: 1,
                cancellationToken: ct
            );

            return results.Any();
        }

        public async Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default)
        {
            await _client.DeleteAsync(CollectionName, new Filter
            {
                Must =
                {
                    new Condition
                    {
                        Field = new FieldCondition
                        {
                            Key   = "documentId",
                            Match = new Match { Keyword = documentId }
                        }
                    }
                }
            }, cancellationToken: ct);
        }

        private static ScoredChunk MapPayload(MapField<string, Value> payload, float score)
        {
            var metadataRaw = payload.TryGetValue("metadata", out var m) ? m.StringValue : "{}";
            var metadata = JsonSerializer.Deserialize<Dictionary<string, string>>(metadataRaw) ?? new();

            return new ScoredChunk
            {
                DocumentId  = payload["documentId"].StringValue,
                SourceType  = payload["sourceType"].StringValue,
                Title       = payload["title"].StringValue,
                Author      = payload["author"].StringValue,
                Recipient   = payload.TryGetValue("recipient", out var r) ? r.StringValue : null,
                Url         = payload.TryGetValue("url", out var u) && !string.IsNullOrEmpty(u.StringValue) ? u.StringValue : null,
                PublishedAt = payload["publishedAt"].StringValue,
                Text        = payload["text"].StringValue,
                Metadata    = metadata,
                Score       = score
            };
        }
    }
}
