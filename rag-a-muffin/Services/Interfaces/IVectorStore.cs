using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IVectorStore
    {
        Task UpsertAsync(EmbeddedChunk chunk, CancellationToken ct = default);
        Task DeleteByEmailIdAsync(string emailId, CancellationToken ct = default);
    }
}