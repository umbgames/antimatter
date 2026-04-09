using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public static class FirebaseApiClient
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<(bool success, string errorMsg)> LoginAsync(string email, string password, CancellationToken ct)
        {
            var apiKey = MarketplaceSettings.FirebaseApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return (false, "Firebase API Key is empty.");

            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key={apiKey}";
            var payload = new JObject
            {
                ["email"] = email,
                ["password"] = password,
                ["returnSecureToken"] = true
            };

            return await AuthenticateAsync(url, payload, ct);
        }

        public static async Task<(bool success, string errorMsg)> SignUpAsync(string email, string password, CancellationToken ct)
        {
            var apiKey = MarketplaceSettings.FirebaseApiKey;
            if (string.IsNullOrWhiteSpace(apiKey)) return (false, "Firebase API Key is empty.");

            var url = $"https://identitytoolkit.googleapis.com/v1/accounts:signUp?key={apiKey}";
            var payload = new JObject
            {
                ["email"] = email,
                ["password"] = password,
                ["returnSecureToken"] = true
            };

            return await AuthenticateAsync(url, payload, ct);
        }

        private static async Task<(bool success, string errorMsg)> AuthenticateAsync(string url, JObject payload, CancellationToken ct)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

                using var resp = await Http.SendAsync(req, ct);
                string respText = await resp.Content.ReadAsStringAsync();

                if (!resp.IsSuccessStatusCode)
                {
                    try
                    {
                        var errRoot = JToken.Parse(respText);
                        var msg = errRoot["error"]?["message"]?.Value<string>();
                        return (false, msg ?? $"HTTP {(int)resp.StatusCode}: {respText}");
                    }
                    catch
                    {
                        return (false, $"HTTP {(int)resp.StatusCode}: {respText}");
                    }
                }

                var root = JToken.Parse(respText);
                var token = root["idToken"]?.Value<string>();
                var localId = root["localId"]?.Value<string>();

                if (string.IsNullOrEmpty(token)) return (false, "No token returned.");

                MarketplaceSettings.CurrentToken = token;
                MarketplaceSettings.CurrentUserId = localId;

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static void Logout()
        {
            MarketplaceSettings.CurrentToken = "";
            MarketplaceSettings.CurrentUserId = "";
        }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(MarketplaceSettings.CurrentToken);
    }
}
