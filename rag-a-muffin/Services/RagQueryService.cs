using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
            _logger.LogInformation("Embedding query: {Question}", request.Query);
            var queryVector = await _embedder.EmbedAsync(request.Query, ct);

            var chunks = await ResolveChunksAsync(request, queryVector, ct);

            if (chunks.Count == 0)
            {
                return new QueryResponse
                {
                    Answer = "I couldn't find any emails relevant to your question.",
                    Citations = []
                };
            }

            var prompt = BuildPrompt(request.Query, chunks);
            _logger.LogInformation("Sending prompt to LLM ({ChunkCount} chunks)...", chunks.Count);
            var answer = await _llm.CompleteAsync(prompt, ct);

            var citations = chunks.Select(c => new ChunkCitation
            {
                EmailId      = c.EmailId,
                Subject      = c.Subject,
                From         = c.From,
                Date         = c.Date,
                RelevantText = c.Text
            }).ToList();

            return new QueryResponse { Answer = answer, Citations = citations };
        }

        public async IAsyncEnumerable<string> StreamQueryAsync(QueryRequest request,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            _logger.LogInformation("Embedding streaming query: {Question}", request.Query);
            var queryVector = await _embedder.EmbedAsync(request.Query, ct);

            var chunks = await ResolveChunksAsync(request, queryVector, ct);

            foreach (var c in chunks)
                _logger.LogInformation("Using: [{Dir}] Subject='{Subject}' Score={Score:F3}", c.Direction, c.Subject, c.Score);

            if (chunks.Count == 0)
            {
                yield return "I couldn't find any emails relevant to your question.";
                yield break;
            }

            var prompt = BuildPrompt(request.Query, chunks);

            await foreach (var token in _llm.StreamAsync(prompt, ct))
            {
                yield return token;
            }

            var citationsJson = JsonSerializer.Serialize(chunks.Select(c => new
            {
                emailId        = c.EmailId,
                subject        = c.Subject,
                from           = c.From,
                to             = c.To,
                date           = c.Date,
                direction      = c.Direction,
                hasAttachments = c.HasAttachments
            }).ToList());

            yield return $"[CITATIONS]:{citationsJson}";
        }

        // Runs vector search, then merges in any direct sender/recipient payload matches.
        private async Task<List<ScoredChunk>> ResolveChunksAsync(QueryRequest request, float[] queryVector, CancellationToken ct)
        {
            var vectorResults = await _vectorStore.SearchAsync(queryVector, request.TopK, ct);
            _logger.LogInformation("Vector search returned {Count} chunks", vectorResults.Count);

            var (senderName, recipientName) = ExtractPersonFilters(request.Query);

            if (senderName != null)
            {
                _logger.LogInformation("Sender filter detected: '{Name}' — running payload search on 'from'", senderName);
                var senderResults = await _vectorStore.ScrollBySenderAsync("from", senderName, request.TopK, ct);
                _logger.LogInformation("Payload search found {Count} sender matches", senderResults.Count);
                vectorResults = Merge(senderResults, vectorResults, request.TopK);
            }

            if (recipientName != null)
            {
                _logger.LogInformation("Recipient filter detected: '{Name}' — running payload search on 'to'", recipientName);
                var recipientResults = await _vectorStore.ScrollBySenderAsync("to", recipientName, request.TopK, ct);
                _logger.LogInformation("Payload search found {Count} recipient matches", recipientResults.Count);
                vectorResults = Merge(recipientResults, vectorResults, request.TopK);
            }

            return vectorResults;
        }

        // Payload-matched results go first (direct answer), then vector results fill remaining slots.
        private static List<ScoredChunk> Merge(List<ScoredChunk> primary, List<ScoredChunk> secondary, int limit)
        {
            var seenEmailIds = new HashSet<string>(primary.Select(c => c.EmailId));
            return primary
                .Concat(secondary.Where(c => !seenEmailIds.Contains(c.EmailId)))
                .Take(limit)
                .ToList();
        }

        // Detects "from X" and "to X" patterns in the query and returns the extracted name.
        private static (string? senderName, string? recipientName) ExtractPersonFilters(string query)
        {
            // Matches: "from Mike Maseda", "emails from Verizon", "received from Sarah Jones"
            var fromMatch = Regex.Match(query,
                @"\bfrom\s+([A-Za-z][A-Za-z0-9\s\.]{1,40}?)(?:\s*\?|$|\s+(?:about|regarding|on|with|at|that|in))",
                RegexOptions.IgnoreCase);

            // Matches: "to John", "sent to Mike", "emails to Sarah Jones"
            var toMatch = Regex.Match(query,
                @"\bto\s+([A-Za-z][A-Za-z0-9\s\.]{1,40}?)(?:\s*\?|$|\s+(?:about|regarding|on|with|at|that|in))",
                RegexOptions.IgnoreCase);

            return (
                fromMatch.Success ? fromMatch.Groups[1].Value.Trim() : null,
                toMatch.Success   ? toMatch.Groups[1].Value.Trim()   : null
            );
        }

        private static string BuildPrompt(string question, List<ScoredChunk> chunks)
        {
            var today = DateTime.Now.ToString("dddd, MMMM d, yyyy");

            var context = string.Join("\n\n", chunks.Select((c, i) =>
            {
                var text = c.Text.Length > 800 ? c.Text[..800] + "…" : c.Text;
                var direction = c.Direction == "sent" ? "Sent by you" : "Received";
                var sb = new StringBuilder();
                sb.AppendLine($"[Email {i + 1}] {direction}");
                sb.AppendLine($"From: {c.From}");
                if (!string.IsNullOrWhiteSpace(c.To))
                    sb.AppendLine($"To: {c.To}");
                if (!string.IsNullOrWhiteSpace(c.Cc))
                    sb.AppendLine($"Cc: {c.Cc}");
                sb.AppendLine($"Subject: {c.Subject}");
                sb.AppendLine($"Date: {c.Date}");
                if (c.HasAttachments)
                    sb.AppendLine("Has attachments: yes");
                sb.AppendLine($"Content: {text}");
                return sb.ToString().TrimEnd();
            }));

            return $"""
        You are a personal email assistant. Today is {today}.
        Answer the user's question using only the emails provided below.
        Be direct and specific — if the answer is yes/no, lead with that.
        When dates or senders matter, mention them in your answer.
        If the emails don't contain enough information to answer, say so clearly — don't guess.

        EMAILS:
        {context}

        QUESTION: {question}

        ANSWER:
        """;
        }
    }
}
