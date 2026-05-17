using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;

namespace RagAMuffin.Services.ExternalApps
{
    public class Gmail
    {
        public static Task<List<Message>> FetchInboxAsync(GmailService service, int maxResults = 100)
            => FetchAsync(service, "INBOX", maxResults);

        public static Task<List<Message>> FetchSentAsync(GmailService service, int maxResults = 100)
            => FetchAsync(service, "SENT", maxResults);

        public static async Task<List<Message>> FetchAsync(GmailService service, string label, int maxResults = 100)
        {
            var listRequest = service.Users.Messages.List("me");
            listRequest.LabelIds = label;
            listRequest.MaxResults = maxResults;

            var listResponse = await listRequest.ExecuteAsync();
            var messages = new List<Message>();

            if (listResponse.Messages == null) return messages;

            foreach (var msgRef in listResponse.Messages)
            {
                var fullMessage = await service.Users.Messages
                    .Get("me", msgRef.Id)
                    .ExecuteAsync();

                messages.Add(fullMessage);
            }

            return messages;
        }

        public static async Task<List<Message>> FetchByLabelsAsync(
            GmailService service, IList<string> labels, int maxResults = 100)
        {
            var seen     = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var messages = new List<Message>();

            foreach (var label in labels)
            {
                var batch = await FetchAsync(service, label, maxResults);
                foreach (var msg in batch)
                    if (seen.Add(msg.Id))
                        messages.Add(msg);
            }

            return messages;
        }
    }
}
