using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;
using System.Runtime.CompilerServices;
using System.Text.Json;
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

        public async IAsyncEnumerable<string> StreamAsync(string prompt,
    [EnumeratorCancellation] CancellationToken ct = default)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, "api/generate")
            {
                Content = JsonContent.Create(new
                {
                    model = "llama3.2",
                    prompt = prompt,
                    stream = true
                })
            };

            // HttpCompletionOption.ResponseHeadersRead is critical —
            // without it HttpClient buffers the entire response before returning
            using var response = await _http.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, ct);

            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var reader = new StreamReader(stream);

            while (!reader.EndOfStream && !ct.IsCancellationRequested)
            {
                var line = await reader.ReadLineAsync(ct);
                if (string.IsNullOrEmpty(line)) continue;

                OllamaStreamChunk? chunk;
                try
                {
                    chunk = JsonSerializer.Deserialize<OllamaStreamChunk>(line);
                }
                catch (JsonException)
                {
                    continue; // skip malformed lines
                }

                if (chunk is null || chunk.Done) yield break;

                yield return chunk.Response;
            }
        }
    }
}