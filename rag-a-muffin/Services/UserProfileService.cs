using System.Text.Json;

namespace RagAMuffin.Services
{
    public class UserProfileService
    {
        private readonly string _profilePath = "/app/data/profile.json";
        private readonly ILogger<UserProfileService> _logger;
        private string? _userId;

        public string? UserId => _userId;
        public bool IsConfigured => !string.IsNullOrWhiteSpace(_userId);

        public UserProfileService(ILogger<UserProfileService> logger, IConfiguration config)
        {
            _logger = logger;
            // Config takes priority; profile.json is the fallback for when config is absent
            _userId = config["Ingestion:UserId"];
            if (string.IsNullOrWhiteSpace(_userId))
                LoadFromFile();
        }

        public async Task SetUserIdAsync(string userId)
        {
            _userId = userId;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_profilePath)!);
                await File.WriteAllTextAsync(_profilePath,
                    JsonSerializer.Serialize(new { userId }));
                _logger.LogInformation("User profile saved: {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to save user profile");
            }
        }

        private void LoadFromFile()
        {
            try
            {
                if (!File.Exists(_profilePath)) return;
                var json = File.ReadAllText(_profilePath);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("userId", out var el))
                    _userId = el.GetString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load user profile");
            }
        }
    }
}
