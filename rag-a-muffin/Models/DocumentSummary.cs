namespace RagAMuffin.Models
{
    public class DocumentSummary
    {
        public required string DocumentId  { get; init; }
        public required string SourceType  { get; init; }
        public required string Title       { get; init; }
        public required string Author      { get; init; }
        public string?         Url         { get; init; }
        public required string PublishedAt { get; init; }
        public required int    ChunkCount  { get; init; }
    }
}
