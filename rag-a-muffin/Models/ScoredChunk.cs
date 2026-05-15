namespace RagAMuffin.Models
{
    public class ScoredChunk
    {
        public required string EmailId { get; init; }
        public required string ThreadId { get; init; }
        public required string Subject { get; init; }
        public required string From { get; init; }
        public required string To { get; init; }
        public required string Cc { get; init; }
        public required string Date { get; init; }
        public required string Text { get; init; }
        public required string Labels { get; init; }
        public required bool HasAttachments { get; init; }
        public required string Direction { get; init; }
        public required float Score { get; init; }
    }
}
