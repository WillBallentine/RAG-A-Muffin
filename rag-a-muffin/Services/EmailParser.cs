using RagAMuffin.Services.Interfaces;
using RagAMuffin.Models;
using RagAMuffin.Constants;
using Google.Apis.Gmail.v1.Data;
using System.Globalization;
using System.Text.RegularExpressions;

namespace RagAMuffin.Services
{
    public class EmailParser : IEmailParser
    {
        private readonly ILogger<EmailParser> _logger;

        public EmailParser(ILogger<EmailParser> logger)
        {
            _logger = logger;
        }

        public string GetHeader(Message message, string headerName)
        {
            return message.Payload.Headers
                .FirstOrDefault(h => h.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty;
        }

        public string GetBody(Message message)
        {
            // 1. Prefer text/plain from parts (most reliable, already clean text)
            var plainPart = message.Payload.Parts?.FirstOrDefault(p => p.MimeType == "text/plain");
            if (plainPart?.Body?.Data != null)
                return DecodeBase64(plainPart.Body.Data);

            // 2. Fall back to text/html from parts, stripping markup
            var htmlPart = message.Payload.Parts?.FirstOrDefault(p => p.MimeType == "text/html");
            if (htmlPart?.Body?.Data != null)
                return StripHtml(DecodeBase64(htmlPart.Body.Data));

            // 3. Non-multipart email — payload body is the content
            if (message.Payload?.Body?.Data != null)
            {
                var text = DecodeBase64(message.Payload.Body.Data);
                return text.TrimStart().StartsWith('<') ? StripHtml(text) : text;
            }

            return string.Empty;
        }

        public ParsedEmail ParsedEmail(Message raw)
        {
            var body = GetBody(raw);
            body = StripQuotedReplies(body);
            body = StripSignature(body);
            body = NormalizeWhitespace(body);

            if (body.Length < GeneralConstants.MinBodyLength || IsJunkBody(body))
            {
                _logger.LogInformation("Skipping email '{Subject}' — body is too short or junk content",
                    GetHeader(raw, "Subject"));
                return null;
            }

            var dateHeader = GetHeader(raw, "Date");
            var parsedDate = ParseEmailDate(dateHeader);

            body = body.Length > GeneralConstants.MaxBodyLength
                ? body[..GeneralConstants.MaxBodyLength]
                : body;

            var labelIds = raw.LabelIds ?? [];
            var direction = labelIds.Contains("SENT") ? "sent" : "received";
            var labels = FormatLabels(labelIds);
            var hasAttachments = DetectAttachments(raw);

            _logger.LogInformation(
                "Parsed email '{Subject}' [{Direction}] from '{From}' dated {Date}. " +
                "Body: {Length} chars, Attachments: {HasAttachments}",
                GetHeader(raw, "Subject"), direction, GetHeader(raw, "From"),
                parsedDate, body.Length, hasAttachments);

            return new ParsedEmail
            {
                Id = raw.Id,
                Subject = GetHeader(raw, "Subject"),
                From = GetHeader(raw, "From"),
                To = GetHeader(raw, "To"),
                Cc = GetHeader(raw, "Cc"),
                Date = parsedDate,
                Body = body,
                ThreadId = raw.ThreadId,
                Labels = labels,
                HasAttachments = hasAttachments,
                Direction = direction
            };
        }

        private static string FormatLabels(IList<string> labelIds)
        {
            if (labelIds == null) return string.Empty;
            // Keep only user-meaningful labels — skip system noise like UNREAD, CATEGORY_*
            var meaningful = new HashSet<string> { "STARRED", "IMPORTANT", "SENT", "INBOX" };
            return string.Join(", ", labelIds.Where(l => meaningful.Contains(l)));
        }

        private static bool DetectAttachments(Message message)
        {
            return message.Payload?.Parts?.Any(p =>
                !string.IsNullOrEmpty(p.Filename) &&
                p.MimeType != "text/plain" &&
                p.MimeType != "text/html") == true;
        }

        private static string DecodeBase64(string data)
        {
            var base64 = data.Replace('-', '+').Replace('_', '/');
            return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(base64));
        }

        private static string StripHtml(string html)
        {
            // Drop style/script blocks wholesale — they're pure noise
            html = Regex.Replace(html, @"<style[^>]*>.*?</style>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            html = Regex.Replace(html, @"<script[^>]*>.*?</script>", " ", RegexOptions.Singleline | RegexOptions.IgnoreCase);
            // Block-level elements become line breaks so paragraphs stay readable
            html = Regex.Replace(html, @"<(br|p|div|tr|li|h[1-6]|td|th)[^>]*/?>", "\n", RegexOptions.IgnoreCase);
            // Strip all remaining tags
            html = Regex.Replace(html, @"<[^>]+>", " ");
            return System.Net.WebUtility.HtmlDecode(html);
        }

        private static string StripQuotedReplies(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            foreach (var pattern in RegexPatterns.QuotePatterns)
            {
                var match = pattern.Match(body);
                if (match.Success)
                {
                    body = body[..match.Index].TrimEnd();
                    break;
                }
            }

            body = Regex.Replace(body, @"^>.*$", "", RegexOptions.Multiline);
            return body;
        }

        private static string StripSignature(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            foreach (var delimiter in StringPatterns.SignatureDelimiters)
            {
                var index = body.IndexOf(delimiter, StringComparison.Ordinal);
                if (index > 0)
                    return body[..index].TrimEnd();
            }

            var lines = body.Split('\n');
            var scanFrom = Math.Max(0, lines.Length - 10);

            for (var i = scanFrom; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                foreach (var pattern in RegexPatterns.SignaturePatterns)
                {
                    if (pattern.IsMatch(line))
                        return string.Join('\n', lines[..i]).TrimEnd();
                }
            }

            return body;
        }

        private static DateTime ParseEmailDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.UtcNow;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var parsed))
                return parsed;

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
                    DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var dto))
                return dto.UtcDateTime;

            return DateTime.UtcNow;
        }

        private static string NormalizeWhitespace(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            body = body.Replace("\r\n", "\n").Replace("\r", "\n");
            body = Regex.Replace(body, @"\n{3,}", "\n\n");
            body = Regex.Replace(body, @"[ \t]{2,}", " ");

            var lines = body.Split('\n')
                            .Select(l => l.Trim())
                            .ToArray();

            return string.Join('\n', lines).Trim();
        }

        private static bool IsJunkBody(string body)
        {
            var words = body.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (words.Length == 0) return true;

            var urlCount = words.Count(w =>
                w.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                w.StartsWith("https://", StringComparison.OrdinalIgnoreCase));

            return (double)urlCount / words.Length > 0.15;
        }
    }
}
