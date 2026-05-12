using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using Google.Apis.Gmail.v1.Data;

namespace RagAMuffin.Services.Interfaces
{
    public interface IEmailParser
    {
        string GetHeader(Message message, string headerName);
        string GetBody(Message message);
        ParsedEmail ParsedEmail(Message raw);
    }
}