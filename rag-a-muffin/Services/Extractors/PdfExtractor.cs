using System.Text;
using UglyToad.PdfPig;

namespace RagAMuffin.Services.Extractors
{
    public class PdfExtractor : IDocumentExtractor
    {
        public bool CanHandle(string extension) => extension == ".pdf";

        public Task<string> ExtractAsync(Stream stream, CancellationToken ct = default)
        {
            using var doc = PdfDocument.Open(stream);
            var sb = new StringBuilder();
            foreach (var page in doc.GetPages())
            {
                sb.AppendLine(page.Text);
            }
            return Task.FromResult(sb.ToString().Trim());
        }
    }
}
