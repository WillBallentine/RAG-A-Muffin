using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;
using System.Runtime.CompilerServices;
namespace RagAMuffin.Services
{
    public class RagQueryService : IRagQueryService
    {
        private readonly IEmbeddingService _embedder;
        private readonly IVectorStore _vectorStore;
        private readonly ILlmService _llm;
        private readonly ILogger<RagQueryService> _logger;

        public RagQueryService(
            IEmbeddingService embedder,
            IVectorStore vectorStore,
            ILlmService llm,
            ILogger<RagQueryService> logger)
        {
            _embedder = embedder;
            _vectorStore = vectorStore;
            _llm = llm;
            _logger = logger;
        }

        public async Task<QueryResponse> QueryAsync(QueryRequest request, CancellationToken ct = default)
        {
            // 1. Embed the question
            _logger.LogInformation("Embedding query: {Question}", request.Query);
            var queryVector = await _embedder.EmbedAsync(request.Query, ct);

            // 2. Search for relevant chunks
            var chunks = await _vectorStore.SearchAsync(queryVector, request.TopK, ct);
            _logger.LogInformation("Retrieved {Count} chunks from vector store", chunks.Count);

            if (chunks.Count == 0)
            {
                return new QueryResponse
                {
                    Answer = "I couldn't find any relevant emails for your question.",
                    Citations = []
                };
            }

            // 3. Build the prompt
            var prompt = BuildPrompt(request.Query, chunks);

            // 4. Call the LLM
            _logger.LogInformation("Sending prompt to LLM...");
            var answer = await _llm.CompleteAsync(prompt, ct);

            // 5. Build citations from retrieved chunks
            var citations = chunks.Select(c => new ChunkCitation
            {
                EmailId = c.EmailId,
                Subject = c.Subject,
                From = c.From,
                Date = c.Date,
                RelevantText = c.Text
            }).ToList();

            return new QueryResponse { Answer = answer, Citations = citations };
        }

        public async IAsyncEnumerable<string> StreamQueryAsync(QueryRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            // Steps 1-3 are identical to QueryAsync — embed, search, build prompt
            _logger.LogInformation("Embedding query for streaming: {Question}", request.Query);
            var queryVector = await _embedder.EmbedAsync(request.Query, ct);

            var chunks = await _vectorStore.SearchAsync(queryVector, request.TopK, ct);
            _logger.LogInformation("Retrieved {Count} chunks for streaming query", chunks.Count);

            if (chunks.Count == 0)
            {
                yield return "I couldn't find any relevant emails for your question.";
                yield break;
            }

            var prompt = BuildPrompt(request.Query, chunks);

           // Stream tokens directly from the LLM
            await foreach (var token in _llm.StreamAsync(prompt, ct))
            {
                yield return token;
            }

            // Citations sent as a final SSE event — see endpoint below
        }

        private static string BuildPrompt(string question, List<ScoredChunk> chunks)
        {
            var context = string.Join("\n\n", chunks.Select((c, i) =>
                $"[Email {i + 1}]\n" +
                $"From: {c.From}\n" +
                $"Subject: {c.Subject}\n" +
                $"Date: {c.Date}\n" +
                $"Content: {c.Text}"));

            return $"""
            You are a helpful assistant with access to a user's emails.
            Answer the question using only the email context provided below.
            If the answer isn't in the emails, say so — do not make anything up.
            Be concise and cite which email(s) your answer comes from.

            EMAIL CONTEXT:
            {context}

            QUESTION:
            {question}

            ANSWER:
            """;
        }
    }
}