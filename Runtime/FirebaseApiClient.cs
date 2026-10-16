using System;
using System.Net;
using System.Net.Sockets;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
#if UNITY_EDITOR
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

        private static int GetRandomUnusedPort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        public static async Task<(bool success, string errorMsg)> SignInWithGoogleBrowserAsync()
        {
            int port = GetRandomUnusedPort();
            string redirectUri = $"http://localhost:{port}/";
            
            var listener = new HttpListener();
            
            try
            {
                listener.Prefixes.Add(redirectUri);
                listener.Start();
                
                string authUrl = $"https://antimatter.ng/editor-auth?port={port}";
                Application.OpenURL(authUrl);
                Debug.Log($"[Antimatter Auth] Opened browser for Google Sign-In. Waiting for redirect on {redirectUri}...");

                var contextTask = listener.GetContextAsync();
                
                // Wait up to 2 minutes
                if (await Task.WhenAny(contextTask, Task.Delay(120000)) != contextTask)
                {
                    listener.Stop();
                    return (false, "Sign-in timed out. Please try again.");
                }

                var context = await contextTask;
                var request = context.Request;
                var response = context.Response;

                string token = request.QueryString["token"];
                string refresh = request.QueryString["refresh"];

                string responseString = "<html><head><style>body{font-family:sans-serif;display:flex;justify-content:center;align-items:center;height:100vh;background:#0f172a;color:#fff;}</style></head><body><h2>Authentication Successful! You can close this tab and return to Unity.</h2></body></html>";
                byte[] buffer = Encoding.UTF8.GetBytes(responseString);
                response.ContentLength64 = buffer.Length;
                response.OutputStream.Write(buffer, 0, buffer.Length);
                response.OutputStream.Close();

                if (string.IsNullOrEmpty(token))
                {
                    return (false, "Browser returned without a valid token.");
                }

                MarketplaceSettings.CurrentToken = token;
                MarketplaceSettings.CurrentRefreshToken = refresh ?? "";
                MarketplaceSettings.TokenExpiryTime = DateTime.UtcNow.AddSeconds(GetJwtExpiresInSeconds(token) - 300).ToString("o");
                
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
            finally
            {
                if (listener.IsListening)
                    listener.Stop();
                
                try { ((IDisposable)listener).Dispose(); } catch { } // Swallow any Mono HttpListener Dispose bugs
            }
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
                var refreshToken = root["refreshToken"]?.Value<string>();
                var expiresIn = root["expiresIn"]?.Value<int>() ?? 3600;
                var localId = root["localId"]?.Value<string>();

                if (string.IsNullOrEmpty(token)) return (false, "No token returned.");

                MarketplaceSettings.CurrentToken = token;
                MarketplaceSettings.CurrentRefreshToken = refreshToken;
                MarketplaceSettings.TokenExpiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 300).ToString("o"); // 5 min buffer
                MarketplaceSettings.CurrentUserId = localId;

                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        public static async Task EnsureValidTokenAsync(CancellationToken ct)
        {
            if (!IsLoggedIn) throw new Exception("User is not logged in.");

            if (DateTime.TryParse(MarketplaceSettings.TokenExpiryTime, null, System.Globalization.DateTimeStyles.RoundtripKind, out var expiryTime))
            {
                if (DateTime.UtcNow < expiryTime) return; // Token is still valid
            }

            var refreshToken = MarketplaceSettings.CurrentRefreshToken;
            if (string.IsNullOrEmpty(refreshToken))
                throw new Exception("No refresh token available. Please log in again.");

            var apiKey = MarketplaceSettings.FirebaseApiKey;
            var url = $"https://securetoken.googleapis.com/v1/token?key={apiKey}";

            var payload = new JObject
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Content = new StringContent(payload.ToString(), Encoding.UTF8, "application/json");

            using var resp = await Http.SendAsync(req, ct);
            string respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                Logout();
                throw new Exception("Session expired. Please log in again.");
            }

            var root = JToken.Parse(respText);
            MarketplaceSettings.CurrentToken = root["id_token"]?.Value<string>();
            MarketplaceSettings.CurrentRefreshToken = root["refresh_token"]?.Value<string>();
            var expiresIn = root["expires_in"]?.Value<int>() ?? 3600;
            MarketplaceSettings.TokenExpiryTime = DateTime.UtcNow.AddSeconds(expiresIn - 300).ToString("o");
        }

        private static int GetJwtExpiresInSeconds(string token)
        {
            try
            {
                var parts = token.Split('.');
                if (parts.Length < 2) return 3600;
                var payload = parts[1];
                payload = payload.Replace('-', '+').Replace('_', '/');
                switch (payload.Length % 4)
                {
                    case 2: payload += "=="; break;
                    case 3: payload += "="; break;
                }
                var jsonBytes = Convert.FromBase64String(payload);
                var jsonStr = Encoding.UTF8.GetString(jsonBytes);
                var root = JToken.Parse(jsonStr);
                if (root["exp"] != null)
                {
                    long exp = root["exp"].Value<long>();
                    long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    int expiresIn = (int)(exp - now);
                    return expiresIn > 0 ? expiresIn : 0;
                }
            }
            catch { }
            return 3600;
        }

        public static void Logout()
        {
            MarketplaceSettings.CurrentToken = "";
            MarketplaceSettings.CurrentRefreshToken = "";
            MarketplaceSettings.TokenExpiryTime = "";
            MarketplaceSettings.CurrentUserId = "";
        }

        public static bool IsLoggedIn => !string.IsNullOrEmpty(MarketplaceSettings.CurrentToken);
    }
#endif
}
