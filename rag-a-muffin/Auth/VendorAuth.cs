using System.IO;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Gmail.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace RagAMuffin.Auth
{
    public static class GoogleAuth
    {
        private const string TokenStoreFolder = "GmailApp.Tokens";

        private static async Task<GoogleAuthorizationCodeFlow> CreateFlowAsync()
        {
            await using var stream = new FileStream("credentials.json", FileMode.Open, FileAccess.Read);
            var secrets = GoogleClientSecrets.FromStream(stream).Secrets;

            return new GoogleAuthorizationCodeFlow(new GoogleAuthorizationCodeFlow.Initializer
            {
                ClientSecrets = secrets,
                Scopes = new[] { GmailService.Scope.GmailReadonly },
                DataStore = new FileDataStore(TokenStoreFolder, fullPath: true)
            });
        }

        public static async Task<bool> HasStoredCredentialAsync(string userId)
        {
            var flow = await CreateFlowAsync();
            return await flow.LoadTokenAsync(userId, CancellationToken.None) != null;
        }

        public static async Task<string> GetAuthorizationUrlAsync(string userId, string redirectUri)
        {
            var flow = await CreateFlowAsync();
            var request = (global::Google.Apis.Auth.OAuth2.Requests.GoogleAuthorizationCodeRequestUrl)flow.CreateAuthorizationCodeRequest(redirectUri);
            request.AccessType = "offline";
            request.Prompt = "consent";
            request.IncludeGrantedScopes = "true";
            request.State = Uri.EscapeDataString(userId);
            return request.Build().AbsoluteUri;
        }

        public static async Task<TokenResponse> ExchangeCodeForTokenAsync(string userId, string code, string redirectUri)
        {
            var flow = await CreateFlowAsync();
            return await flow.ExchangeCodeForTokenAsync(userId, code, redirectUri, CancellationToken.None);
        }

        public static async Task<GmailService> CreateGmailServiceAsync(string userId)
        {
            var flow = await CreateFlowAsync();
            var token = await flow.LoadTokenAsync(userId, CancellationToken.None);
            if (token is null)
            {
                throw new InvalidOperationException($"No stored token found for user '{userId}'.");
            }

            var credential = new UserCredential(flow, userId, token);
            return new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "RAG-A-Muffin"
            });
        }
    }
}
