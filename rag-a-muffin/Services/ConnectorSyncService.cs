using RagAMuffin.Services.Interfaces;

namespace RagAMuffin.Services
{
    public class ConnectorSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ConnectorSyncService> _logger;
        private readonly TimeSpan _interval;

        public ConnectorSyncService(
            IServiceScopeFactory scopeFactory,
            ILogger<ConnectorSyncService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _interval = TimeSpan.FromMinutes(
                double.TryParse(configuration["Ingestion:IntervalMinutes"], out var m) ? m : 60);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ConnectorSyncService started. Interval={Interval}m", _interval.TotalMinutes);

            await SyncAllAsync(stoppingToken);

            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SyncAllAsync(stoppingToken);
            }
        }

        private async Task SyncAllAsync(CancellationToken ct)
        {
            _logger.LogInformation("Connector sync starting...");
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var connectors = scope.ServiceProvider.GetServices<IConnector>().ToList();
                var pipeline   = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();

                if (connectors.Count == 0)
                {
                    _logger.LogWarning("No connectors registered — nothing to sync");
                    return;
                }

                foreach (var connector in connectors)
                {
                    _logger.LogInformation("Running connector: {SourceType}", connector.SourceType);
                    try
                    {
                        var documents = await connector.FetchAsync(ct);
                        await pipeline.IngestAsync(documents, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Connector '{SourceType}' failed", connector.SourceType);
                    }
                }

                _logger.LogInformation("Connector sync complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ConnectorSyncService encountered an unexpected error");
            }
        }
    }
}
