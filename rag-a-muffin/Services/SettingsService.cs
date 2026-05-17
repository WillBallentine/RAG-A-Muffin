using RagAMuffin.Models;
using System.Text.Json;

namespace RagAMuffin.Services
{
    public class SettingsService
    {
        private const string DataPath = "/app/data/settings.json";
        private static readonly JsonSerializerOptions _json = new() { WriteIndented = true };
        private volatile AppSettings _current;

        public AppSettings Current => _current;

        public SettingsService(IConfiguration config)
        {
            _current = LoadOrDefault(config);
        }

        public async Task<AppSettings> SaveAsync(AppSettings settings)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(DataPath)!);
            await File.WriteAllTextAsync(DataPath, JsonSerializer.Serialize(settings, _json));
            _current = settings;
            return _current;
        }

        private static AppSettings LoadOrDefault(IConfiguration config)
        {
            if (File.Exists(DataPath))
            {
                try
                {
                    var loaded = JsonSerializer.Deserialize<AppSettings>(
                        File.ReadAllText(DataPath),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded is not null) return loaded;
                }
                catch { /* corrupt file — fall through */ }
            }

            return new AppSettings
            {
                LlmModel = config.GetValue("Ollama:LlmModel", "llama3.2")!
            };
        }
    }
}
