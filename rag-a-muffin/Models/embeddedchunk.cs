namespace RagAMuffin.Models
{
    public class EmbeddedChunk
    {
        // From TextChunk
        public required string Text { get; init; }
        public required int ChunkIndex { get; init; }
        public required int TotalChunks { get; init; }
        public int CharStart { get; init; }
        public int CharEnd { get; init; }

        // From ParsedEmail
        public required string EmailId { get; init; }
        public required string ThreadId { get; init; }
        public required string Subject { get; init; }
        public required string From { get; init; }
        public required string To { get; init; }
        public required string Cc { get; init; }
        public required DateTimeOffset Date { get; init; }
        public required string Labels { get; init; }
        public required bool HasAttachments { get; init; }
        public required string Direction { get; init; }

        // From the embedder
        public required float[] Vector { get; init; }
    }
}
