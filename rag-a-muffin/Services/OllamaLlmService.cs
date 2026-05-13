using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;
namespace RagAMuffin.Services
{
    public class OllamaLlmService : ILlmService
    {
        private readonly HttpClient _http;
        private readonly ILogger<OllamaLlmService> _logger;

        public OllamaLlmService(HttpClient http, ILogger<OllamaLlmService> logger)
        {
            _http = http;
            _logger = logger;
        }

        public async Task<string> CompleteAsync(string prompt, CancellationToken ct = default)
        {
            var response = await _http.PostAsJsonAsync("api/generate", new
            {
                model = "llama3.2",   // hardcoded for now, could be made dynamic later
                prompt = prompt,
                stream = false      // keep it simple for now
            }, ct);

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<OllamaGenerateResponse>(ct);
            return result!.Response;
        }
    }
}