namespace RagAMuffin.Models
{
    public class SourceDocument
    {
        public required string Id { get; init; }
        public required string SourceType { get; init; }
        public required string Title { get; init; }
        public required string Author { get; init; }
        public string? Recipient { get; init; }
        public string? Cc { get; init; }
        public string? Url { get; init; }
        public required string Body { get; init; }
        public required DateTime PublishedAt { get; init; }
        // Source-specific extras (e.g. direction, labels, threadId for Gmail)
        public Dictionary<string, string> Metadata { get; init; } = new();
    }
}
