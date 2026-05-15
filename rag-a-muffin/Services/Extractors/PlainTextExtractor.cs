namespace RagAMuffin.Services.Extractors
{
    public class PlainTextExtractor : IDocumentExtractor
    {
        public bool CanHandle(string extension) => extension is ".txt" or ".md";

        public async Task<string> ExtractAsync(Stream stream, CancellationToken ct = default)
        {
            using var reader = new StreamReader(stream, leaveOpen: true);
            return await reader.ReadToEndAsync(ct);
        }
    }
}
