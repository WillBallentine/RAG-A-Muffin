using System.Threading.Channels;

namespace RagAMuffin.Services
{
    public class FileWatcherService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<FileWatcherService> _logger;
        private readonly string _watchDir;

        // Decouples the FileSystemWatcher callback (sync) from async ingestion processing
        private readonly Channel<string> _queue = Channel.CreateUnbounded<string>(
            new UnboundedChannelOptions { SingleReader = true });

        public FileWatcherService(IServiceScopeFactory scopeFactory, ILogger<FileWatcherService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
            _watchDir     = "/app/data/watch";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            Directory.CreateDirectory(_watchDir);

            using var watcher = new FileSystemWatcher(_watchDir)
            {
                IncludeSubdirectories = false,
                EnableRaisingEvents   = true,
                NotifyFilter          = NotifyFilters.FileName | NotifyFilters.Size
            };

            watcher.Created += (_, e) =>
            {
                _logger.LogInformation("FileWatcher: detected '{File}'", e.Name);
                _queue.Writer.TryWrite(e.FullPath);
            };

            _logger.LogInformation("FileWatcherService watching '{Dir}'", _watchDir);

            await foreach (var path in _queue.Reader.ReadAllAsync(stoppingToken))
            {
                // Brief delay to let the OS finish writing the file before we open it
                await Task.Delay(500, stoppingToken);
                await IngestFileAsync(path, stoppingToken);
            }
        }

        private async Task IngestFileAsync(string path, CancellationToken ct)
        {
            if (!File.Exists(path))
            {
                _logger.LogWarning("FileWatcher: '{Path}' no longer exists — skipping", path);
                return;
            }

            try
            {
                using var scope   = _scopeFactory.CreateScope();
                var ingestor      = scope.ServiceProvider.GetRequiredService<FileIngestionService>();
                await using var stream = File.OpenRead(path);
                var id = await ingestor.IngestAsync(stream, Path.GetFileName(path), ct);

                if (id is not null)
                    _logger.LogInformation("FileWatcher: ingested '{File}' successfully", Path.GetFileName(path));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "FileWatcher: failed to ingest '{Path}'", path);
            }
        }
    }
}
