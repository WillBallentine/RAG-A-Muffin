using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;

namespace RagAMuffin.Services.Interfaces
{
    public interface IRagQueryService
    {
        Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default);
        IAsyncEnumerable<string> StreamQueryAsync(QueryRequest request, CancellationToken ct = default);
    }
}