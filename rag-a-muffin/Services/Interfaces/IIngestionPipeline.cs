using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IIngestionPipeline
    {
        Task IngestAsync(IEnumerable<SourceDocument> documents, CancellationToken ct = default);
    }
}
