using System.Text.Json.Serialization;

namespace RagAMuffin.Models
{
    public class OllamaStreamChunk
    {
        [JsonPropertyName("response")]
        public string Response { get; init; } = string.Empty;

        [JsonPropertyName("done")]
        public bool Done { get; init; }
    }
}