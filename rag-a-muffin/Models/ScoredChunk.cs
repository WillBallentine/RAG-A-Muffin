namespace RagAMuffin.Models
{
    public class ScoredChunk
    {
        public required string EmailId { get; init; }
        public required string ThreadId { get; init; }
        public required string Subject { get; init; }
        public required string From { get; init; }
        public required string Date { get; init; }
        public required string Text { get; init; }
        public required float Score { get; init; }
    }
}