namespace RagAMuffin.Services.Extractors
{
    public interface IDocumentExtractor
    {
        bool CanHandle(string extension);
        Task<string> ExtractAsync(Stream stream, CancellationToken ct = default);
    }
}
