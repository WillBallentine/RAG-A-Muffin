using System.Text.Json;

namespace RagAMuffin.Services
{
    public class ConnectorConfigService
    {
        private readonly string _configPath = "/app/data/connectors.json";
        private readonly ILogger<ConnectorConfigService> _logger;
        private readonly SemaphoreSlim _saveLock = new(1, 1);
        private volatile ConnectorConfig _current;

        public ConnectorConfig Current => _current;

        public ConnectorConfigService(ILogger<ConnectorConfigService> logger, IConfiguration config)
        {
            _logger  = logger;
            _current = LoadOrDefault(config);
        }

        public async Task<ConnectorConfig> SaveAsync(ConnectorConfig config)
        {
            await _saveLock.WaitAsync();
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_configPath)!);
                var json = JsonSerializer.Serialize(config,
                    new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_configPath, json);
                _current = config;
                _logger.LogInformation(
                    "Connector config saved: {Feeds} RSS feed(s), {Urls} web URL(s)",
                    config.RssFeeds.Count, config.WebUrls.Count);
                return _current;
            }
            finally
            {
                _saveLock.Release();
            }
        }

        private ConnectorConfig LoadOrDefault(IConfiguration config)
        {
            if (File.Exists(_configPath))
            {
                try
                {
                    var json = File.ReadAllText(_configPath);
                    var loaded = JsonSerializer.Deserialize<ConnectorConfig>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (loaded is not null)
                    {
                        _logger.LogInformation(
                            "Connector config loaded: {Feeds} RSS feed(s), {Urls} web URL(s)",
                            loaded.RssFeeds.Count, loaded.WebUrls.Count);
                        return loaded;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to load connector config — using appsettings defaults");
                }
            }

            // First-run: bootstrap from appsettings.json
            return new ConnectorConfig
            {
                RssFeeds = config.GetSection("Connectors:Rss:Feeds")
                               .Get<List<FeedEntry>>() ?? [],
                WebUrls  = config.GetSection("Connectors:Web:Urls")
                               .Get<List<FeedEntry>>() ?? []
            };
        }
    }

    public class ConnectorConfig
    {
        public List<FeedEntry> RssFeeds { get; set; } = [];
        public List<FeedEntry> WebUrls  { get; set; } = [];
    }

    public class FeedEntry
    {
        public string  Url   { get; set; } = string.Empty;
        public string? Label { get; set; }
    }
}
