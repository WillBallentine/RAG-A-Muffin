namespace RagAMuffin.Models
{
    public class ChatSession
    {
        public required string            Id        { get; init; }
        public required string            Title     { get; set;  }
        public required DateTime          CreatedAt { get; init; }
        public          DateTime          UpdatedAt { get; set;  }
        public          List<ChatMessage> Messages  { get; init; } = [];
    }
}
