namespace RagAMuffin.Models
{
    public class ChatMessage
    {
        public required string Role         { get; init; } // "user" or "assistant"
        public required string Content      { get; init; }
        public string?         CitationsJson { get; init; } // stored on assistant turns for session restore
    }
}
