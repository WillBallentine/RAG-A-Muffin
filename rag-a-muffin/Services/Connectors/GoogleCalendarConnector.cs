using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using RagAMuffin.Auth;
using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using System.Text;

namespace RagAMuffin.Services.Connectors
{
    public class GoogleCalendarConnector : IConnector
    {
        private readonly UserProfileService _profile;
        private readonly ILogger<GoogleCalendarConnector> _logger;
        private readonly IConfiguration _config;

        public string SourceType => "calendar";

        public GoogleCalendarConnector(
            UserProfileService profile,
            ILogger<GoogleCalendarConnector> logger,
            IConfiguration config)
        {
            _profile = profile;
            _logger  = logger;
            _config  = config;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(_profile.UserId))
            {
                _logger.LogWarning("GoogleCalendarConnector: no user configured — complete setup first");
                return [];
            }

            if (!await GoogleAuth.HasStoredCredentialAsync(_profile.UserId))
            {
                _logger.LogWarning("GoogleCalendarConnector: no stored credentials for {UserId}", _profile.UserId);
                return [];
            }

            CalendarService service;
            try
            {
                service = await GoogleAuth.CreateCalendarServiceAsync(_profile.UserId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoogleCalendarConnector: failed to create Calendar service");
                return [];
            }

            var daysBack  = _config.GetValue<int>("Connectors:Calendar:DaysBack", 30);
            var daysAhead = _config.GetValue<int>("Connectors:Calendar:DaysAhead", 7);

            IList<Event> events;
            try
            {
                var req = service.Events.List("primary");
                req.TimeMinDateTimeOffset = DateTimeOffset.UtcNow.AddDays(-daysBack);
                req.TimeMaxDateTimeOffset = DateTimeOffset.UtcNow.AddDays(daysAhead);
                req.SingleEvents          = true;
                req.OrderBy               = EventsResource.ListRequest.OrderByEnum.StartTime;
                req.MaxResults            = 500;
                var response = await req.ExecuteAsync(ct);
                events = response.Items ?? [];
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GoogleCalendarConnector: failed to list events — " +
                    "you may need to re-authorize to grant Calendar access");
                return [];
            }

            var documents = new List<SourceDocument>();
            foreach (var evt in events)
            {
                var start = evt.Start?.DateTimeDateTimeOffset?.UtcDateTime
                         ?? (DateTime.TryParse(evt.Start?.Date, out var d) ? d.ToUniversalTime() : DateTime.UtcNow);

                var body = BuildEventBody(evt);
                if (string.IsNullOrWhiteSpace(body)) continue;

                documents.Add(new SourceDocument
                {
                    Id          = "cal_" + evt.Id,
                    SourceType  = SourceType,
                    Title       = evt.Summary ?? "(no title)",
                    Author      = _profile.UserId!,
                    Body        = body,
                    PublishedAt = start,
                    Metadata    = new Dictionary<string, string>
                    {
                        ["calendarEventId"] = evt.Id ?? string.Empty,
                        ["location"]        = evt.Location ?? string.Empty,
                        ["status"]          = evt.Status ?? string.Empty
                    }
                });
            }

            _logger.LogInformation("GoogleCalendarConnector: fetched {Count} event(s) " +
                "({DaysBack}d back, {DaysAhead}d ahead)", documents.Count, daysBack, daysAhead);
            return documents;
        }

        private static string BuildEventBody(Event evt)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"Event: {evt.Summary ?? "(no title)"}");

            var start = evt.Start?.DateTimeDateTimeOffset?.ToString("f")
                     ?? evt.Start?.Date ?? "(unknown)";
            var end   = evt.End?.DateTimeDateTimeOffset?.ToString("f")
                     ?? evt.End?.Date ?? "(unknown)";
            sb.AppendLine($"When: {start} – {end}");

            if (!string.IsNullOrWhiteSpace(evt.Location))
                sb.AppendLine($"Where: {evt.Location}");

            if (evt.Attendees?.Count > 0)
            {
                var attendees = string.Join(", ", evt.Attendees
                    .Select(a => a.DisplayName is not null ? $"{a.DisplayName} ({a.Email})" : a.Email));
                sb.AppendLine($"Attendees: {attendees}");
            }

            if (!string.IsNullOrWhiteSpace(evt.Description))
            {
                sb.AppendLine();
                sb.AppendLine(evt.Description);
            }

            return sb.ToString();
        }
    }
}
