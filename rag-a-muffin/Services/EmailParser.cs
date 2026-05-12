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
            var part = message.Payload.Parts?
                .FirstOrDefault(p => p.MimeType == "text/plain")
                ?? message.Payload;

            if (part?.Body?.Data == null) return string.Empty;

            // Gmail uses URL-safe base64
            var base64 = part.Body.Data.Replace('-', '+').Replace('_', '/');
            var bytes = Convert.FromBase64String(base64);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public ParsedEmail ParsedEmail(Message raw)
        {
            var body = GetBody(raw);         // prefer text/plain, fall back to text/html
            body = StripQuotedReplies(body);
            body = StripSignature(body);
            body = NormalizeWhitespace(body);


            if (body.Length < GeneralConstants.MinBodyLength)
                return null; // or a Result<ParsedEmail> with a skip reason

            var dateHeader = GetHeader(raw, "Date");
            var parsedDate = ParseEmailDate(dateHeader);
            _logger.LogInformation("Parsed email '{Subject}' from '{From}' dated {Date}. Body length after cleaning: {Length} characters.",
                GetHeader(raw, "Subject"), GetHeader(raw, "From"), parsedDate, body.Length);

            return new ParsedEmail
            {
                Id = raw.Id,
                Subject = GetHeader(raw, "Subject"),
                From = GetHeader(raw, "From"),
                Date = parsedDate,
                Body = GetBody(raw),
                ThreadId = raw.ThreadId
            };
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
                    // Truncate everything from the first quote marker onward
                    body = body[..match.Index].TrimEnd();
                    break; // First match wins — don't double-truncate
                }
            }

            // Strip any remaining "> " prefixed lines throughout
            body = Regex.Replace(body, @"^>.*$", "", RegexOptions.Multiline);

            return body;
        }


        private static string StripSignature(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return body;

            // 1. Check explicit delimiters first — most reliable
            foreach (var delimiter in StringPatterns.SignatureDelimiters)
            {
                var index = body.IndexOf(delimiter, StringComparison.Ordinal);
                if (index > 0)
                    return body[..index].TrimEnd();
            }

            // 2. Fall back to heuristics — scan the last 10 lines only
            //    Signatures are always at the bottom; scanning the whole body
            //    risks false positives in the actual email content
            var lines = body.Split('\n');
            var scanFrom = Math.Max(0, lines.Length - 10);

            for (var i = scanFrom; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (string.IsNullOrEmpty(line)) continue;

                foreach (var pattern in RegexPatterns.SignaturePatterns)
                {
                    if (pattern.IsMatch(line))
                    {
                        // Rejoin everything before this line
                        return string.Join('\n', lines[..i]).TrimEnd();
                    }
                }
            }

            return body;
        }

        private static DateTime ParseEmailDate(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTime.UtcNow;

            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var parsed))
            {
                return parsed;
            }

            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces | DateTimeStyles.AdjustToUniversal, out var dto))
            {
                return dto.UtcDateTime;
            }

            return DateTime.UtcNow;
        }

        private static string NormalizeWhitespace(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            // Normalize line endings to \n
            body = body.Replace("\r\n", "\n").Replace("\r", "\n");

            // Collapse 3+ consecutive blank lines down to 2
            // (preserve paragraph breaks, kill excessive whitespace)
            body = Regex.Replace(body, @"\n{3,}", "\n\n");

            // Collapse multiple spaces/tabs on a single line to one space
            body = Regex.Replace(body, @"[ \t]{2,}", " ");

            // Trim leading/trailing whitespace per line
            var lines = body.Split('\n')
                            .Select(l => l.Trim())
                            .ToArray();

            body = string.Join('\n', lines);

            return body.Trim();
        }
    }
}