using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;

namespace RagAMuffin.Services.Interfaces
{
    public interface IIngestionPipeline
    {
        Task IngestAsync(IEnumerable<Message> messages);
    }
}