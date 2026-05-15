using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace RagAMuffin.Services.Extractors
{
    public class DocxExtractor : IDocumentExtractor
    {
        public bool CanHandle(string extension) => extension is ".docx" or ".doc";

        public Task<string> ExtractAsync(Stream stream, CancellationToken ct = default)
        {
            using var doc = WordprocessingDocument.Open(stream, isEditable: false);
            var body = doc.MainDocumentPart?.Document?.Body;
            if (body is null) return Task.FromResult(string.Empty);

            var sb = new StringBuilder();
            foreach (var para in body.Elements<Paragraph>())
            {
                var line = para.InnerText;
                if (!string.IsNullOrWhiteSpace(line))
                    sb.AppendLine(line);
            }
            return Task.FromResult(sb.ToString().Trim());
        }
    }
}
