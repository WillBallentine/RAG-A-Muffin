using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;
namespace RagAMuffin.Services
{
public class IngestionPipeline : IIngestionPipeline
{
    private readonly ILogger<IngestionPipeline> _logger;
    private readonly IEmailParser _parser;
    private readonly IChunker _chunker;
    private readonly IEmbeddingService _embedder;
    private readonly IVectorStore _vectorStore;
    public IngestionPipeline(ILogger<IngestionPipeline> logger, IEmailParser parser, IChunker chunker, IEmbeddingService embedder, IVectorStore vectorStore)
    {
        _logger = logger;
       _parser = parser;
       _chunker = chunker;
        _embedder = embedder;
         _vectorStore = vectorStore; 
    }
    public async Task IngestAsync(IEnumerable<Message> messages)
    {
        foreach (var message in messages)
        {
            _logger.LogInformation("Processing email with ID: {EmailId}", message.Id);
            var parsed = _parser.ParsedEmail(message);
            var chunks = _chunker.Chunk(parsed);

            foreach (var chunk in chunks)
            {
                float[] vector;
                try
                {
                    vector = await _embedder.EmbedAsync(chunk.Text);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to embed chunk {ChunkIndex} for email {EmailId}. Skipping this chunk.", chunk.Index, parsed.Id);
                    continue;
                }

                await _vectorStore.UpsertAsync(new EmbeddedChunk
                {
                    EmailId    = parsed.Id,
                    ThreadId   = parsed.ThreadId,
                    Subject    = parsed.Subject,
                    From       = parsed.From,
                    Date       = parsed.Date,
                    ChunkIndex = chunk.Index,
                    Text       = chunk.Text,
                    TotalChunks= chunk.TotalChunks,
                    Vector     = vector
                });
            }
        }
    }
}
}