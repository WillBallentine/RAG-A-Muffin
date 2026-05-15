using RagAMuffin.Models;

namespace RagAMuffin.Services.Interfaces
{
    public interface IChunker
    {
        List<TextChunk> Chunk(SourceDocument document);
    }
}
