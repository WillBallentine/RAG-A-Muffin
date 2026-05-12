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
        var response = await _http.PostAsJsonAsync("/api/embeddings", new
        {
            model = "nomic-embed-text",
            prompt = text
        }, ct);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Ollama embedding failed with status {StatusCode} for text length {TextLength}: {ErrorContent}", response.StatusCode, text.Length, errorContent);
            throw new InvalidOperationException($"Ollama embedding failed with status {(int)response.StatusCode}: {errorContent}");
        }

        var result = await response.Content.ReadFromJsonAsync<OllamaEmbedResponse>(ct);
        _logger.LogInformation("Generated embedding for text of length {TextLength}.", text.Length);
        return result!.Embedding;
    }
}