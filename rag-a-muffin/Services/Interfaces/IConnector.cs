using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IConnector
    {
        string SourceType { get; }
        Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default);
    }
}
