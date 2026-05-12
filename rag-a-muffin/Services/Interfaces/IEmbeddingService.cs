namespace RagAMuffin.Services.Interfaces
{
    public interface IEmbeddingService
    {
        Task<float[]> EmbedAsync(string text, CancellationToken ct = default);
    }
}