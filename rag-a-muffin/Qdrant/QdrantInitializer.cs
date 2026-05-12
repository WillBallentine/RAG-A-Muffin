using Qdrant.Client;
using Qdrant.Client.Grpc;

namespace RagAMuffin.Qdrant
{
public class QdrantCollectionInitializer
{
    private readonly QdrantClient _client;
    private const string CollectionName = "emails";
    private const ulong VectorSize = 768; // nomic-embed-text output dimensions

    public QdrantCollectionInitializer(QdrantClient client)
    {
        _client = client;
    }

    public async Task InitializeAsync()
    {
        var exists = await _client.CollectionExistsAsync(CollectionName);

        if (!exists)
        {
            await _client.CreateCollectionAsync(CollectionName, new VectorParams
            {
                Size = VectorSize,
                Distance = Distance.Cosine
            });
        }
    }
}
}