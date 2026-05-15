namespace RagAMuffin.Models
{
    public class QueryRequest
    {
        public required string Query { get; init; }
        public int TopK { get; init; } = 8;
        // Optional source type filter. null/empty = all sources.
        // e.g. ["gmail"], ["pdf","docx"], ["pdf","docx","txt","md"]
        public string[]? SourceTypes { get; init; }
    }
}
