using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IVectorStore
    {
        Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default);
        Task DeleteByDocumentIdAsync(string documentId, CancellationToken ct = default);
        Task<List<ScoredChunk>> SearchAsync(float[] queryVector, int topK = 5, IEnumerable<string>? sourceTypes = null, CancellationToken ct = default);
        Task<List<ScoredChunk>> SearchByFieldAsync(string field, string value, int limit, CancellationToken ct = default);
        Task<bool> DocumentExistsAsync(string documentId, CancellationToken ct = default);
    }
}
