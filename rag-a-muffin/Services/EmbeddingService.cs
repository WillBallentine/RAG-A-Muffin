using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
public class OllamaEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly ILogger<OllamaEmbeddingService> _logger;

    public OllamaEmbeddingService(ILogger<OllamaEmbeddingService> logger, HttpClient http)
    {
        _logger = logger;
        _http = http;
    }
    

    public async Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/embeddings", new
        {
            model = "nomic-embed-text",   // hardcoded for now, could be made dynamic later
            prompt = text
        }, ct);

        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        return result!.Embedding;
    }
}