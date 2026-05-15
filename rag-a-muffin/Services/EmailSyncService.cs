using RagAMuffin.Auth;
using RagAMuffin.Services.ExternalApps;
using RagAMuffin.Services.Interfaces;

namespace RagAMuffin.Services
{
    public class EmailSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<EmailSyncService> _logger;
        private readonly string _userId;
        private readonly TimeSpan _interval;
        private readonly int _maxEmailsPerSync;

        public EmailSyncService(
            IServiceScopeFactory scopeFactory,
            ILogger<EmailSyncService> logger,
            IConfiguration configuration)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _userId = configuration["Ingestion:UserId"] ?? string.Empty;
            _interval = TimeSpan.FromMinutes(
                double.TryParse(configuration["Ingestion:IntervalMinutes"], out var m) ? m : 60);
            _maxEmailsPerSync = int.TryParse(configuration["Ingestion:MaxEmailsPerSync"], out var max) ? max : 100;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (string.IsNullOrWhiteSpace(_userId))
            {
                _logger.LogInformation(
                    "Ingestion:UserId not configured — scheduled sync disabled. " +
                    "Set it in appsettings.json to enable automatic email syncing.");
                return;
            }

            _logger.LogInformation(
                "Email sync service started. UserId={UserId}, Interval={Interval}m, MaxEmails={Max}",
                _userId, _interval.TotalMinutes, _maxEmailsPerSync);

            await SyncAsync(stoppingToken);

            using var timer = new PeriodicTimer(_interval);
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                await SyncAsync(stoppingToken);
            }
        }

        private async Task SyncAsync(CancellationToken ct)
        {
            _logger.LogInformation("Scheduled email sync starting for {UserId}", _userId);
            try
            {
                if (!await GoogleAuth.HasStoredCredentialAsync(_userId))
                {
                    _logger.LogWarning(
                        "No stored credentials for {UserId}. " +
                        "Authenticate via the UI first, then sync will begin automatically.", _userId);
                    return;
                }

                var gmailService = await GoogleAuth.CreateGmailServiceAsync(_userId);

                var inbox = await Gmail.FetchInboxAsync(gmailService, _maxEmailsPerSync);
                var sent = await Gmail.FetchSentAsync(gmailService, _maxEmailsPerSync);
                var all = inbox.Concat(sent).ToList();

                _logger.LogInformation("Fetched {Inbox} inbox + {Sent} sent = {Total} messages total",
                    inbox.Count, sent.Count, all.Count);

                using var scope = _scopeFactory.CreateScope();
                var pipeline = scope.ServiceProvider.GetRequiredService<IIngestionPipeline>();
                await pipeline.IngestAsync(all);

                _logger.LogInformation("Scheduled sync complete");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scheduled email sync failed for {UserId}", _userId);
            }
        }
    }
}
