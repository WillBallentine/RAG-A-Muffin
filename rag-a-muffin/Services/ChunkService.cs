using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;

namespace RagAMuffin.Services
{
    public class TextChunker : IChunker
    {
        private readonly int _chunkSize;    // e.g. 200 words for safer embed input length
        private readonly int _overlap;      // e.g. 50 words
        private readonly ILogger<TextChunker> _logger;
        public TextChunker(ILogger<TextChunker> logger, int chunkSize = 200, int overlap = 50)
        {
            _logger = logger;
            _chunkSize = chunkSize;
            _overlap = overlap;
        }

        public List<TextChunk> Chunk(ParsedEmail email)
        {
            var words = email.Body.Split(' ');

            if (words.Length <= _chunkSize)
            {
                return new List<TextChunk>
                {
                    new TextChunk
                    {
                        Text = email.Body,
                        Index = 0,
                        TotalChunks = 1,
                        CharStart = 0,
                        CharEnd = email.Body.Length
                    }
                };
            }

            var totalChunks = Math.Max(1,
                (int)Math.Ceiling((double)(words.Length - _overlap) / (_chunkSize - _overlap)));

            var chunks = new List<TextChunk>(totalChunks); // capacity hint is a nice touch
            var index = 0;
            var position = 0;

            while (position < words.Length)
            {
                var slice = words.Skip(position).Take(_chunkSize).ToArray();
                var text = string.Join(' ', slice);

                chunks.Add(new TextChunk
                {
                    Text = text,
                    Index = index++,
                    TotalChunks = totalChunks,
                    CharStart = position,       // note: this is word position, not char position
                    CharEnd = position + slice.Length
                });

                position += _chunkSize - _overlap;
            }
            _logger.LogInformation("Chunked email '{Subject}' into {ChunkCount} chunks (chunk size: {ChunkSize} words, overlap: {Overlap} words).",
                email.Subject, chunks.Count, _chunkSize, _overlap);

            return chunks;
        }
    }
}