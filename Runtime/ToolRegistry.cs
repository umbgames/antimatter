using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace Antimatter
{
    public static class ToolRegistry
    {
        private static string RegistryPath => Path.Combine(Application.persistentDataPath, "AntimatterTools", "installed_tools.json");

        public static List<ToolManifest> GetInstalledTools()
        {
            if (!File.Exists(RegistryPath))
                return new List<ToolManifest>();

            try
            {
                string json = File.ReadAllText(RegistryPath);
                return JsonConvert.DeserializeObject<List<ToolManifest>>(json) ?? new List<ToolManifest>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Antimatter Marketplace] Failed to read tool registry: {ex.Message}");
                return new List<ToolManifest>();
            }
        }

        public static void RegisterTool(ToolManifest manifest)
        {
            var tools = GetInstalledTools();
            
            // Remove existing version if present
            tools.RemoveAll(t => t.id == manifest.id);
            tools.Add(manifest);

            SaveRegistry(tools);
        }

        public static void UnregisterTool(string toolId)
        {
            var tools = GetInstalledTools();
            int removed = tools.RemoveAll(t => t.id == toolId);

            if (removed > 0)
            {
                SaveRegistry(tools);
            }
        }

        private static void SaveRegistry(List<ToolManifest> tools)
        {
            try
            {
                string dir = Path.GetDirectoryName(RegistryPath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                string json = JsonConvert.SerializeObject(tools, Formatting.Indented);
                File.WriteAllText(RegistryPath, json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Antimatter Marketplace] Failed to write tool registry: {ex.Message}");
            }
        }

        public static bool IsToolInstalled(string toolId)
        {
            var tools = GetInstalledTools();
            return tools.Exists(t => t.id == toolId);
        }
    }
}
