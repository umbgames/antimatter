using System;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Networking;

namespace Antimatter
{
    
    public static class OpenAITextureGen
    {
        [Serializable] private class ImageGenRequest
        {
            public string model;
            public string prompt;
            public int n = 1;
            public string size;
        }
    
        [Serializable] private class ImageGenResponse
        {
            public DataItem[] data;
            [Serializable] public class DataItem { public string b64_json; }
        }
    
        public static async Task<Texture2D> GenerateTextureAsync(string prompt, string apiKey)
        {
            var req = new ImageGenRequest
            {
                model = AiProviderSettings.ImageModel,
                prompt = prompt,
                size = AiProviderSettings.ImageSize
            };
            string json = JsonUtility.ToJson(req);
    
            using var uwr = new UnityWebRequest("https://api.openai.com/v1/images/generations", "POST");
            byte[] body = Encoding.UTF8.GetBytes(json);
            uwr.uploadHandler = new UploadHandlerRaw(body);
            uwr.downloadHandler = new DownloadHandlerBuffer();
            uwr.SetRequestHeader("Content-Type", "application/json");
            uwr.SetRequestHeader("Authorization", $"Bearer {apiKey}");
    
            var op = uwr.SendWebRequest();
            while (!op.isDone) await Task.Yield();
    
            if (uwr.result != UnityWebRequest.Result.Success)
                throw new Exception($"OpenAI image gen failed: {uwr.error}\n{uwr.downloadHandler.text}");
    
            var resp = JsonUtility.FromJson<ImageGenResponse>(uwr.downloadHandler.text);
            if (resp?.data == null || resp.data.Length == 0 || string.IsNullOrEmpty(resp.data[0].b64_json))
                throw new Exception("OpenAI image gen returned empty data.");
    
            byte[] png = Convert.FromBase64String(resp.data[0].b64_json);
    
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(png);
            tex.Apply();
            return tex;
        }
    
        public static TerrainLayer SaveAsTerrainLayer(Texture2D tex, string assetPathBase)
        {
            if (!AssetDatabase.IsValidFolder("Assets/Generated"))
                AssetDatabase.CreateFolder("Assets", "Generated");
    
            string pngPath = $"Assets/Generated/{assetPathBase}.png";
            string layerPath = $"Assets/Generated/{assetPathBase}.terrainlayer";
    
            System.IO.File.WriteAllBytes(pngPath, tex.EncodeToPNG());
            AssetDatabase.ImportAsset(pngPath, ImportAssetOptions.ForceUpdate);
    
            var imported = AssetDatabase.LoadAssetAtPath<Texture2D>(pngPath);
    
            var layer = new TerrainLayer();
            layer.diffuseTexture = imported;
            layer.tileSize = new Vector2(12, 12);
    
            AssetDatabase.CreateAsset(layer, layerPath);
            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);
        }
    }
}
