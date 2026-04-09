using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
using Newtonsoft.Json.Linq;

namespace Antimatter
{
    public static class MarketplaceClient
    {
        private static readonly HttpClient Http = new HttpClient();

        public static async Task<List<ToolManifest>> FetchAvailableToolsAsync(CancellationToken ct)
        {
            var projectId = MarketplaceSettings.FirebaseProjectId;
            if (string.IsNullOrWhiteSpace(projectId))
                throw new Exception("Firebase Project ID is empty.");

            var url = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/tools";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            
            // If we have a token, we could attach it, but the rules are read: if true for tools wrapper.
            // Still, good practice to pass if logged in.
            if (!string.IsNullOrEmpty(MarketplaceSettings.CurrentToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
            }

            using var resp = await Http.SendAsync(req, ct);
            string respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"Failed to fetch tools: HTTP {(int)resp.StatusCode} {respText}");

            var root = JToken.Parse(respText);
            var docs = root["documents"] as JArray;

            var list = new List<ToolManifest>();

            if (docs == null) return list; // No tools

            foreach (var doc in docs)
            {
                list.Add(ParseFirestoreDocument(doc));
            }

            return list;
        }

        private static ToolManifest ParseFirestoreDocument(JToken doc)
        {
            var fields = doc["fields"];
            if (fields == null) return new ToolManifest();

            var manifest = new ToolManifest();
            manifest.id = GetStringField(fields, "id");
            manifest.name = GetStringField(fields, "name");
            manifest.version = GetStringField(fields, "version");
            manifest.author = GetStringField(fields, "author");
            manifest.description = GetStringField(fields, "description");
            manifest.ai_description = GetStringField(fields, "ai_description");
            manifest.category = GetStringField(fields, "category");
            manifest.entry_point = GetStringField(fields, "entry_point");

            var depsToken = fields["dependencies"]?["arrayValue"]?["values"] as JArray;
            if (depsToken != null)
            {
                manifest.dependencies = new List<string>();
                foreach (var d in depsToken)
                {
                    string val = d["stringValue"]?.Value<string>();
                    if (!string.IsNullOrEmpty(val)) manifest.dependencies.Add(val);
                }
            }
            else
            {
                manifest.dependencies = new List<string>();
            }

            return manifest;
        }

        private static string GetStringField(JToken fields, string key)
        {
            return fields[key]?["stringValue"]?.Value<string>() ?? "";
        }
        
        public static async Task<byte[]> DownloadToolPackageAsync(string toolId, string version, CancellationToken ct)
        {
            var bucket = MarketplaceSettings.FirebaseStorageBucket;
            if (string.IsNullOrWhiteSpace(bucket))
                throw new Exception("Firebase Storage Bucket is empty.");

            // Path: /tools/{toolId}/{version}/package.bundle
            string objectPath = $"tools%2F{toolId}%2F{version}%2Fpackage.bundle";
            var url = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{objectPath}?alt=media";

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            
            if (!string.IsNullOrEmpty(MarketplaceSettings.CurrentToken))
            {
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
            }

            using var resp = await Http.SendAsync(req, ct);
            
            if (!resp.IsSuccessStatusCode)
            {
                string respText = await resp.Content.ReadAsStringAsync();
                throw new Exception($"Failed to download tool: HTTP {(int)resp.StatusCode} {respText}");
            }

            return await resp.Content.ReadAsByteArrayAsync();
        }

        public static async Task PublishToolAsync(ToolManifest manifest, CancellationToken ct)
        {
            var bucket = MarketplaceSettings.FirebaseStorageBucket;
            var projectId = MarketplaceSettings.FirebaseProjectId;

            if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(projectId))
                throw new Exception("Firebase config is incomplete.");
                
            if (string.IsNullOrEmpty(MarketplaceSettings.CurrentToken))
                throw new Exception("You must be logged in to publish a tool.");
                
            if (!System.IO.File.Exists(manifest.entry_point))
                throw new Exception($"Entry point script not found: {manifest.entry_point}");

            // 1. Zip the script into memory
            byte[] packageData;
            using (var ms = new System.IO.MemoryStream())
            {
                using (var archive = new ZipArchive(ms, ZipArchiveMode.Create, true))
                {
                    var fileInfo = new System.IO.FileInfo(manifest.entry_point);
                    var entry = archive.CreateEntry(fileInfo.Name);
                    using (var entryStream = entry.Open())
                    using (var fileStream = System.IO.File.OpenRead(manifest.entry_point))
                    {
                        fileStream.CopyTo(entryStream);
                    }
                    manifest.entry_point = fileInfo.Name; // Update entry_point to just the file name inside the zip
                }
                packageData = ms.ToArray();
            }

            // 2. Upload to Storage
            string objectPath = $"tools%2F{manifest.id}%2F{manifest.version}%2Fpackage.bundle";
            string storageUrl = $"https://firebasestorage.googleapis.com/v0/b/{bucket}/o/{objectPath}";

            using var storageReq = new HttpRequestMessage(HttpMethod.Post, storageUrl);
            storageReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
            storageReq.Content = new ByteArrayContent(packageData);
            storageReq.Content.Headers.ContentType = new MediaTypeHeaderValue("application/zip");

            using var storageResp = await Http.SendAsync(storageReq, ct);
            if (!storageResp.IsSuccessStatusCode)
            {
                string err = await storageResp.Content.ReadAsStringAsync();
                throw new Exception($"Failed to upload tool package: HTTP {(int)storageResp.StatusCode} {err}");
            }

            // 3. Create document in Firestore
            var docData = new JObject
            {
                ["fields"] = new JObject
                {
                    ["id"] = new JObject { ["stringValue"] = manifest.id },
                    ["name"] = new JObject { ["stringValue"] = manifest.name },
                    ["version"] = new JObject { ["stringValue"] = manifest.version },
                    ["author"] = new JObject { ["stringValue"] = manifest.author },
                    ["description"] = new JObject { ["stringValue"] = manifest.description },
                    ["ai_description"] = new JObject { ["stringValue"] = manifest.ai_description },
                    ["entry_point"] = new JObject { ["stringValue"] = manifest.entry_point },
                    ["category"] = new JObject { ["stringValue"] = manifest.category },
                    ["dependencies"] = new JObject { ["arrayValue"] = new JObject { ["values"] = new JArray() } }
                }
            };

            string firestoreUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/tools";
            
            // To update or create, we post to the tools collection (creates automatically)
            // But if we want a specific document ID (e.g. manifest.id), we can construct it using documentId.
            firestoreUrl += $"?documentId={manifest.id}";

            using var fireReq = new HttpRequestMessage(HttpMethod.Post, firestoreUrl);
            fireReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
            fireReq.Content = new StringContent(docData.ToString(), System.Text.Encoding.UTF8, "application/json");

            using var fireResp = await Http.SendAsync(fireReq, ct);
            if (!fireResp.IsSuccessStatusCode)
            {
                // If it already exists (409 conflict), try PATCH instead
                if (fireResp.StatusCode == System.Net.HttpStatusCode.Conflict)
                {
                    string patchUrl = $"https://firestore.googleapis.com/v1/projects/{projectId}/databases/(default)/documents/tools/{manifest.id}";
                    using var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl);
                    patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", MarketplaceSettings.CurrentToken);
                    patchReq.Content = new StringContent(docData.ToString(), System.Text.Encoding.UTF8, "application/json");
                    
                    using var patchResp = await Http.SendAsync(patchReq, ct);
                    if (!patchResp.IsSuccessStatusCode)
                    {
                        string err = await patchResp.Content.ReadAsStringAsync();
                        throw new Exception($"Failed to patch document: HTTP {(int)patchResp.StatusCode} {err}");
                    }
                }
                else
                {
                    string err = await fireResp.Content.ReadAsStringAsync();
                    throw new Exception($"Failed to create document: HTTP {(int)fireResp.StatusCode} {err}");
                }
            }
        }
    }
}
