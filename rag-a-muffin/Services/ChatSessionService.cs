using RagAMuffin.Models;
using System.Text.Json;

namespace RagAMuffin.Services
{
    public class ChatSessionService
    {
        private const string DataDir = "/app/data/chats";
        private static readonly JsonSerializerOptions _json =
            new(JsonSerializerDefaults.Web) { WriteIndented = false };

        private readonly ILogger<ChatSessionService> _logger;

        public ChatSessionService(ILogger<ChatSessionService> logger)
        {
            _logger = logger;
            Directory.CreateDirectory(DataDir);
        }

        public async Task<List<ChatSession>> ListAsync()
        {
            var sessions = new List<ChatSession>();
            foreach (var file in Directory.GetFiles(DataDir, "*.json"))
            {
                try
                {
                    var s = JsonSerializer.Deserialize<ChatSession>(await File.ReadAllTextAsync(file), _json);
                    if (s is not null) sessions.Add(s);
                }
                catch (Exception ex) { _logger.LogWarning(ex, "Failed to deserialize session file {File}", file); }
            }
            return [.. sessions.OrderByDescending(s => s.UpdatedAt)];
        }

        public async Task<ChatSession?> GetAsync(string id)
        {
            var path = SafePath(id);
            return path is null || !File.Exists(path)
                ? null
                : JsonSerializer.Deserialize<ChatSession>(await File.ReadAllTextAsync(path), _json);
        }

        public async Task<ChatSession> SaveAsync(ChatSession session)
        {
            var path = SafePath(session.Id)
                ?? throw new ArgumentException("Invalid session id");
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(session, _json));
            return session;
        }

        public Task DeleteAsync(string id)
        {
            var path = SafePath(id);
            if (path is not null && File.Exists(path)) File.Delete(path);
            return Task.CompletedTask;
        }

        // Prevent path traversal: id must be a plain filename-safe string
        private static string? SafePath(string id)
        {
            if (string.IsNullOrWhiteSpace(id) || id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
                return null;
            return Path.Combine(DataDir, $"{id}.json");
        }
    }
}
