using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;

namespace RagAMuffin.Services.Interfaces
{
    public interface ILlmService
    {
        Task<string> CompleteAsync(string prompt, CancellationToken ct = default);
    }
}