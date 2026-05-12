using System.Text.RegularExpressions;

namespace RagAMuffin.Constants
{
    public static class RegexPatterns
    {
        public static readonly Regex[] QuotePatterns =
        [
            // "On Mon, Jan 1 2024, John wrote:" (Gmail/Apple Mail)
            new Regex(@"^On\s.+wrote:\s*$", RegexOptions.Multiline | RegexOptions.Compiled),

            // "From: John Smith" block (Outlook)
            new Regex(@"^From:\s.+$", RegexOptions.Multiline | RegexOptions.Compiled),

            // "-----Original Message-----" (Outlook)
            new Regex(@"^-+Original Message-+$", RegexOptions.Multiline | RegexOptions.Compiled),

            // "> quoted text" lines (standard quoting)
            new Regex(@"^>.*$", RegexOptions.Multiline | RegexOptions.Compiled),

            // Sent from my iPhone / Sent from Outlook (mobile footers)
            new Regex(@"^Sent from my .+$", RegexOptions.Multiline | RegexOptions.Compiled),
        ];

        public static readonly Regex[] SignaturePatterns =
        [
            new Regex(@"^(Best|Kind|Warm)\s*[Rr]egards[,.]?", RegexOptions.Multiline | RegexOptions.Compiled),
            new Regex(@"^(Thanks|Thank you|Cheers|Sincerely)[,.]?\s*$", RegexOptions.Multiline | RegexOptions.Compiled),
            new Regex(@"^\+?[\d\s\(\)\-]{7,}$", RegexOptions.Multiline | RegexOptions.Compiled), // phone number line
            new Regex(@"^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z]{2,}$", RegexOptions.Multiline | RegexOptions.Compiled | RegexOptions.IgnoreCase), // standalone email address
            new Regex(@"^https?://[^\s]+$", RegexOptions.Multiline | RegexOptions.Compiled), // standalone URL
        ];



    }

    public static class StringPatterns
    {
        public static readonly string[] SignatureDelimiters =
        [
            "\n-- \n",      // RFC 3676 standard sig delimiter (two dashes + space)
            "\n--\n",       // Common variation without space
            "\n___\n",      // Underscores
            "\n---\n",      // Dashes
        ];

    }

    public static class GeneralConstants
    {
        public const int MinBodyLength = 30;
    }
}