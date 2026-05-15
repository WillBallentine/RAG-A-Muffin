namespace RagAMuffin.Models
{
    public class QueryRequest
    {
        public required string Query { get; init; }
        public int TopK { get; init; } = 8;
    }
}