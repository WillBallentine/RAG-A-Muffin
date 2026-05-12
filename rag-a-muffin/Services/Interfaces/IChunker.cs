using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IChunker
    {
        List<TextChunk> Chunk(ParsedEmail email);
    }
}