using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IVectorStore
    {
        Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default);
        Task DeleteByEmailIdAsync(string emailId, CancellationToken ct = default);
        Task<List<ScoredChunk>> SearchAsync(float[] queryVector, int topK = 5, CancellationToken ct = default);
        Task<bool> EmailExistsAsync(string emailId, CancellationToken ct = default);
    }
}