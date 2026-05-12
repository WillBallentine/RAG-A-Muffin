using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace RagAMuffin.Services.ExternalApps
{
    public class Gmail
    {
        public static async Task<List<Message>> FetchInboxAsync(GmailService service, int maxResults = 20)
        {
            var listRequest = service.Users.Messages.List("me");
            listRequest.LabelIds = "INBOX";
            listRequest.MaxResults = maxResults;

            var listResponse = await listRequest.ExecuteAsync();
            var messages = new List<Message>();

            if (listResponse.Messages == null) return messages;

            foreach (var msgRef in listResponse.Messages)
            {
                // List only returns ID + threadId; fetch full message separately
                var fullMessage = await service.Users.Messages
                    .Get("me", msgRef.Id)
                    .ExecuteAsync();

                messages.Add(fullMessage);
            }

            return messages;
        }

        public static string GetHeader(Message message, string headerName)
        {
            return message.Payload.Headers
                .FirstOrDefault(h => h.Name.Equals(headerName, StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty;
        }

        public static string GetBody(Message message)
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
    }
}