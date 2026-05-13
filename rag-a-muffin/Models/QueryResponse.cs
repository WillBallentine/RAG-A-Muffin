namespace RagAMuffin.Models
{
    public class QueryResponse
    {
        public required string Answer { get; init; }
        public required List<ChunkCitation> Citations { get; init; }
    }
}