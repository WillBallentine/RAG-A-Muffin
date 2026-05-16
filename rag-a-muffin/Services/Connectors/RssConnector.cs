using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using System.Security.Cryptography;
using System.ServiceModel.Syndication;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace RagAMuffin.Services.Connectors
{
    public class RssConnector : IConnector
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ConnectorConfigService _configService;
        private readonly ILogger<RssConnector> _logger;

        public string SourceType => "rss";

        public RssConnector(
            IHttpClientFactory httpFactory,
            ConnectorConfigService configService,
            ILogger<RssConnector> logger)
        {
            _httpFactory   = httpFactory;
            _configService = configService;
            _logger        = logger;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            var feeds = _configService.Current.RssFeeds;
            if (feeds.Count == 0)
            {
                _logger.LogInformation("RssConnector: no feeds configured");
                return [];
            }

            var documents = new List<SourceDocument>();
            var client    = _httpFactory.CreateClient("rss");

            foreach (var feed in feeds)
            {
                if (string.IsNullOrWhiteSpace(feed.Url)) continue;
                try
                {
                    _logger.LogInformation("RssConnector: fetching {Url}", feed.Url);
                    var xml = await client.GetStringAsync(feed.Url, ct);

                    using var reader = XmlReader.Create(
                        new StringReader(xml),
                        new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore });

                    var synFeed = SyndicationFeed.Load(reader);

                    foreach (var item in synFeed.Items)
                    {
                        var rawBody = item.Summary?.Text
                                   ?? (item.Content as TextSyndicationContent)?.Text
                                   ?? string.Empty;

                        var body = StripTags(rawBody);
                        if (string.IsNullOrWhiteSpace(body)) continue;

                        var itemId = item.Id
                                  ?? item.Links.FirstOrDefault()?.Uri?.ToString()
                                  ?? rawBody;
                        var url = item.Links.FirstOrDefault()?.Uri?.ToString();

                        documents.Add(new SourceDocument
                        {
                            Id          = "rss_" + HashShort(itemId),
                            SourceType  = SourceType,
                            Title       = item.Title?.Text ?? "(untitled)",
                            Author      = feed.Label ?? synFeed.Title?.Text ?? feed.Url,
                            Url         = url,
                            Body        = body,
                            PublishedAt = item.PublishDate != DateTimeOffset.MinValue
                                          ? item.PublishDate.UtcDateTime
                                          : DateTime.UtcNow,
                            Metadata    = new Dictionary<string, string>
                            {
                                ["feedLabel"] = feed.Label ?? string.Empty,
                                ["feedUrl"]   = feed.Url
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "RssConnector: failed to fetch {Url}", feed.Url);
                }
            }

            _logger.LogInformation("RssConnector: fetched {Count} item(s) from {FeedCount} feed(s)",
                documents.Count, feeds.Count);
            return documents;
        }

        private static string StripTags(string html) =>
            Regex.Replace(html, "<[^>]+>", " ")
                 .Replace("&amp;",  "&")
                 .Replace("&lt;",   "<")
                 .Replace("&gt;",   ">")
                 .Replace("&quot;", "\"")
                 .Replace("&nbsp;", " ")
                 .Replace("&#39;",  "'")
                 .Trim();

        private static string HashShort(string raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }
    }
}
