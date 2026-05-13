using System.Text.Json.Serialization;
namespace RagAMuffin.Models
{
    public class OllamaEmbedResponse
    {
        [JsonPropertyName("embedding")]
        public float[] Embedding { get; init; } = [];
    }
}