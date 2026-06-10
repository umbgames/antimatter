using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEditor;

namespace Antimatter
{
    [Serializable]
    public class MarketplaceItem
    {
        public string id;
        public string title;
        public string description;
        public string itemType;
        public string objectKey;
        public string filename;
        public long sizeBytes;
        public string authorName;
        public string createdAt;
    }

    public static class MarketplaceClient
    {
        private static readonly HttpClient _httpClient = new HttpClient();
        // Pointing to the new REST API backend
        private const string API_BASE_URL = "https://7fsbew2573.execute-api.eu-north-1.amazonaws.com/prod/v1/marketplace";

        public static async Task<List<MarketplaceItem>> FetchItemsAsync(string type = "all", CancellationToken ct = default)
        {
            string url = $"{API_BASE_URL}/items";
            if (type != "all") url += $"?type={type}";

            try
            {
                await FirebaseApiClient.EnsureValidTokenAsync(ct);
                string idToken = MarketplaceSettings.CurrentToken;
                
                var request = new HttpRequestMessage(HttpMethod.Get, url);
                if (!string.IsNullOrEmpty(idToken))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);
                }

                var response = await _httpClient.SendAsync(request, ct);
                response.EnsureSuccessStatusCode();
                string json = await response.Content.ReadAsStringAsync();
                
                var data = JObject.Parse(json);
                var itemsArray = data["data"]?["items"] as JArray;
                
                if (itemsArray == null) return new List<MarketplaceItem>();
                
                return itemsArray.ToObject<List<MarketplaceItem>>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Marketplace] Error fetching items: {ex.Message}");
                return new List<MarketplaceItem>();
            }
        }

        public static async Task DownloadAndInstallAsync(MarketplaceItem item, Action<float> onProgress, CancellationToken ct = default)
        {
            try
            {
                onProgress?.Invoke(0.1f);

                await FirebaseApiClient.EnsureValidTokenAsync(ct);
                string idToken = MarketplaceSettings.CurrentToken;
                if (string.IsNullOrEmpty(idToken))
                {
                    Debug.LogError("[Marketplace] Not authenticated. Please login to Antimatter first.");
                    return;
                }

                // 1. Get Download URL
                var requestBody = new StringContent(JsonConvert.SerializeObject(new { objectKey = item.objectKey }), Encoding.UTF8, "application/json");
                var request = new HttpRequestMessage(HttpMethod.Post, $"{API_BASE_URL}/download-url")
                {
                    Content = requestBody
                };
                request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", idToken);

                var urlResponse = await _httpClient.SendAsync(request, ct);
                urlResponse.EnsureSuccessStatusCode();
                
                string urlJson = await urlResponse.Content.ReadAsStringAsync();
                var urlData = JObject.Parse(urlJson);
                string downloadUrl = (string)urlData["data"]?["downloadUrl"];

                if (string.IsNullOrEmpty(downloadUrl))
                {
                    Debug.LogError("[Marketplace] Failed to get valid download URL.");
                    return;
                }

                onProgress?.Invoke(0.4f);

                // 2. Download the actual file from S3
                var fileResponse = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
                fileResponse.EnsureSuccessStatusCode();

                byte[] fileBytes = await fileResponse.Content.ReadAsByteArrayAsync();

                onProgress?.Invoke(0.8f);

                // 3. Save to appropriate folder
                string destFolder = "Assets/Antimatter unity/Editor/Tools/Marketplace";
                if (item.itemType == "model" || item.itemType == "texture")
                {
                    destFolder = "Assets/Antimatter unity/Assets/Marketplace";
                }

                if (!Directory.Exists(destFolder))
                    Directory.CreateDirectory(destFolder);

                string safeFilename = item.filename;
                if (string.IsNullOrEmpty(safeFilename)) safeFilename = item.id + (item.itemType == "tool" ? ".cs" : ".unitypackage");

                string destPath = Path.Combine(destFolder, safeFilename);
                File.WriteAllBytes(destPath, fileBytes);

                Debug.Log($"[Marketplace] Successfully downloaded {item.title} to {destPath}");

                onProgress?.Invoke(1.0f);

                // If it's a unitypackage, import it automatically
                if (destPath.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
                {
                    AssetDatabase.ImportPackage(destPath, interactive: true);
                }

                AssetDatabase.Refresh();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Marketplace] Download failed: {ex.Message}");
            }
        }
    }
}
