using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagAMuffin.Qdrant
{
    public class QdrantCollectionInitializer
    {
        private readonly QdrantClient _client;
        private readonly ILogger<QdrantCollectionInitializer> _logger;
        private const string CollectionName = "emails";
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

            // Full-text indexes let Match { Text = "Mike Maseda" } tokenize and search
            // within the from/to headers. Safe to call on existing collections — idempotent.
            await _client.CreatePayloadIndexAsync(CollectionName, "from",
                PayloadSchemaType.Text, cancellationToken: default);
            await _client.CreatePayloadIndexAsync(CollectionName, "to",
                PayloadSchemaType.Text, cancellationToken: default);

            _logger.LogInformation("Payload indexes ensured on 'from' and 'to' fields");
        }
    }
}
