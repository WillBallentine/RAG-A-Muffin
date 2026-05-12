using RagAMuffin.Models;
using RagAMuffin.Constants;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using System.Text.RegularExpressions;

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


    }
}