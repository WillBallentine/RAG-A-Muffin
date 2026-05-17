namespace RagAMuffin.Models
{
    public class QueryRequest
    {
        public required string Query { get; init; }
        public int TopK { get; init; } = 8;
        // Optional source type filter. null/empty = all sources.
        // e.g. ["gmail"], ["pdf","docx"], ["pdf","docx","txt","md"]
        public string[]? SourceTypes { get; init; }
        // Prior conversation turns (role + content only; CitationsJson ignored by the LLM).
        // Frontend sends the last N turns so the model can answer follow-up questions.
        public ChatMessage[]?    History  { get; init; }
        public DateTimeOffset?   DateFrom { get; init; }
        public DateTimeOffset?   DateTo   { get; init; }
    }
}
