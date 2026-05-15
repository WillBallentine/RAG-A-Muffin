using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace RagAMuffin.Models
{
    public class ParsedEmail
    {
        public string Id { get; set; }
        public string Subject { get; set; }
        public string From { get; set; }
        public string To { get; set; }
        public string Cc { get; set; }
        public DateTime Date { get; set; }
        public string Body { get; set; }
        public string ThreadId { get; set; }
        public string Labels { get; set; }        // e.g. "STARRED, IMPORTANT"
        public bool HasAttachments { get; set; }
        public string Direction { get; set; }     // "received" or "sent"
    }
}
