using System.Text.Json.Serialization;

namespace RagAMuffin.Models
{
    public class OllamaGenerateResponse
    {
        [JsonPropertyName("response")]
        public string Response { get; init; } = string.Empty;
    }
}