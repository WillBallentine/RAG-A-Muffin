namespace RagAMuffin.Models
{
    public class IndexStats
    {
        public required long                      TotalVectors   { get; init; }
        public required Dictionary<string, long>  BySourceType   { get; init; }
    }
}
