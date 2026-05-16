using HtmlAgilityPack;
using RagAMuffin.Models;
using RagAMuffin.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace RagAMuffin.Services.Connectors
{
    public class WebConnector : IConnector
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly ConnectorConfigService _configService;
        private readonly ILogger<WebConnector> _logger;

        public string SourceType => "web";

        public WebConnector(
            IHttpClientFactory httpFactory,
            ConnectorConfigService configService,
            ILogger<WebConnector> logger)
        {
            _httpFactory   = httpFactory;
            _configService = configService;
            _logger        = logger;
        }

        public async Task<IEnumerable<SourceDocument>> FetchAsync(CancellationToken ct = default)
        {
            var urls = _configService.Current.WebUrls;
            if (urls.Count == 0)
            {
                _logger.LogInformation("WebConnector: no URLs configured");
                return [];
            }

            var documents = new List<SourceDocument>();
            var client    = _httpFactory.CreateClient("web");

            foreach (var urlConfig in urls)
            {
                if (string.IsNullOrWhiteSpace(urlConfig.Url)) continue;
                try
                {
                    _logger.LogInformation("WebConnector: scraping {Url}", urlConfig.Url);
                    var html = await client.GetStringAsync(urlConfig.Url, ct);

                    var htmlDoc = new HtmlDocument();
                    htmlDoc.LoadHtml(html);

                    var garbage = htmlDoc.DocumentNode
                        .SelectNodes("//script|//style|//nav|//footer|//header|//aside|//noscript");
                    if (garbage != null)
                        foreach (var node in garbage.ToList())
                            node.Remove();

                    var titleNode = htmlDoc.DocumentNode.SelectSingleNode("//title");
                    var title = urlConfig.Label
                             ?? HtmlEntity.DeEntitize(titleNode?.InnerText?.Trim() ?? "")
                             ?? urlConfig.Url;

                    var rawText = htmlDoc.DocumentNode.InnerText;
                    var text    = Regex.Replace(rawText, @"[ \t]{2,}", " ");
                    text        = Regex.Replace(text, @"\n{3,}", "\n\n").Trim();

                    if (string.IsNullOrWhiteSpace(text)) continue;

                    var host = Uri.TryCreate(urlConfig.Url, UriKind.Absolute, out var uri)
                               ? uri.Host : urlConfig.Url;

                    documents.Add(new SourceDocument
                    {
                        Id          = "web_" + HashShort(urlConfig.Url),
                        SourceType  = SourceType,
                        Title       = string.IsNullOrWhiteSpace(title) ? urlConfig.Url : title,
                        Author      = host,
                        Url         = urlConfig.Url,
                        Body        = text,
                        PublishedAt = DateTime.UtcNow,
                        Metadata    = new Dictionary<string, string>
                        {
                            ["scrapedAt"] = DateTime.UtcNow.ToString("O")
                        }
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "WebConnector: failed to scrape {Url}", urlConfig.Url);
                }
            }

            _logger.LogInformation("WebConnector: scraped {Count} page(s)", documents.Count);
            return documents;
        }

        private static string HashShort(string raw)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
            return Convert.ToHexString(bytes)[..16].ToLowerInvariant();
        }
    }
}
