namespace RagAMuffin.Models
{
    public class ChunkCitation
    {
        public required string EmailId { get; init; }
        public required string Subject { get; init; }
        public required string From { get; init; }
        public required string Date { get; init; }
        public required string RelevantText { get; init; }
    }
}