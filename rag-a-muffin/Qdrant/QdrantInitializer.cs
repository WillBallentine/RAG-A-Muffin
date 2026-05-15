using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagAMuffin.Qdrant
{
    public class QdrantCollectionInitializer
    {
        private readonly QdrantClient _client;
        private readonly ILogger<QdrantCollectionInitializer> _logger;
        private const string CollectionName = "documents";
        private const ulong VectorSize = 768; // nomic-embed-text output dimensions

        public QdrantCollectionInitializer(QdrantClient client, ILogger<QdrantCollectionInitializer> logger)
        {
            _client = client;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            var exists = await _client.CollectionExistsAsync(CollectionName);

            if (!exists)
            {
                await _client.CreateCollectionAsync(CollectionName, new VectorParams
                {
                    Size = VectorSize,
                    Distance = Distance.Cosine,
                }, quantizationConfig: new QuantizationConfig
                {
                    Scalar = new ScalarQuantization
                    {
                        Type = QuantizationType.Int8,
                        AlwaysRam = true
                    }
                });

                _logger.LogInformation("Created Qdrant collection '{Collection}'", CollectionName);
            }

            // Full-text indexes enable Match { Text = "name" } word-level search on author/recipient headers.
            // Safe to call on existing collections — idempotent.
            await _client.CreatePayloadIndexAsync(CollectionName, "author",
                PayloadSchemaType.Text, cancellationToken: default);
            await _client.CreatePayloadIndexAsync(CollectionName, "recipient",
                PayloadSchemaType.Text, cancellationToken: default);
            await _client.CreatePayloadIndexAsync(CollectionName, "sourceType",
                PayloadSchemaType.Keyword, cancellationToken: default);

            _logger.LogInformation("Payload indexes ensured on 'author', 'recipient', 'sourceType'");
        }
    }
}
