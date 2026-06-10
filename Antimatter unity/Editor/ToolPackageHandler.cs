using System;
using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace Antimatter
{
    public static class ToolPackageHandler
    {
        public static string BasePersistentPath => Path.Combine(Application.persistentDataPath, "AntimatterTools");
        public static string BaseAssetsPath => "Assets/Antimatter.MarketplaceTools";

        public static void InstallToolPackage(string toolId, byte[] zipData)
        {
            try
            {
                // Ensure directories exist
                if (!Directory.Exists(BasePersistentPath))
                    Directory.CreateDirectory(BasePersistentPath);

                string toolPersistentPath = Path.Combine(BasePersistentPath, toolId);

                // Clean existing persistent installation
                if (Directory.Exists(toolPersistentPath))
                    Directory.Delete(toolPersistentPath, true);

                Directory.CreateDirectory(toolPersistentPath);

                // Hash verification could be added here if we had a signed hash from Firebase

                // Write zip temporarily
                string tempZipPath = Path.Combine(BasePersistentPath, $"{toolId}_temp.zip");
                File.WriteAllBytes(tempZipPath, zipData);

                // Extract Zip
                ZipFile.ExtractToDirectory(tempZipPath, toolPersistentPath);

                // Delete temp zip
                if (File.Exists(tempZipPath))
                    File.Delete(tempZipPath);

                // Copy to Assets folder to compile (Option C implementation)
                CopyToAssetsForCompilation(toolId, toolPersistentPath);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Antimatter Marketplace] Failed to install tool {toolId}: {ex.Message}");
            }
        }

        private static void CopyToAssetsForCompilation(string toolId, string sourcePersistentPath)
        {
            // Ensure the root marketplace assets folder exists
            if (!AssetDatabase.IsValidFolder("Assets")) return; // Sanity check
            
            if (!AssetDatabase.IsValidFolder(BaseAssetsPath))
            {
                AssetDatabase.CreateFolder("Assets", "Antimatter.MarketplaceTools");
                // Create minimal asmdef to separate compilation
                string asmdefPath = Path.Combine(BaseAssetsPath, "Antimatter.MarketplaceTools.asmdef");
                string asmdefContent = @"{
    ""name"": ""Antimatter.MarketplaceTools"",
    ""rootNamespace"": """",
    ""references"": [
        ""GUID:2db591038cbe22c4f8260e0a5cc08ff4"",
        ""GUID:0b04c86b51cc1304cadbb8e31a8928db""
    ],
    ""includePlatforms"": [],
    ""excludePlatforms"": [],
    ""allowUnsafeCode"": false,
    ""overrideReferences"": false,
    ""precompiledReferences"": [],
    ""autoReferenced"": true,
    ""defineConstraints"": [],
    ""versionDefines"": [],
    ""noEngineReferences"": false
}";
                // NOTE: We probably need the real GUID of Antimatter.Runtime.asmdef. We will leave it auto-referenced for now.
                File.WriteAllText(asmdefPath, asmdefContent);
            }

            string toolAssetPath = Path.Combine(BaseAssetsPath, toolId);

            // Clean existing
            if (Directory.Exists(toolAssetPath))
            {
                // Delete via AssetDatabase to ensure meta files are cleared
                FileUtil.DeleteFileOrDirectory(toolAssetPath);
            }

            // Copy all files
            FileUtil.CopyFileOrDirectory(sourcePersistentPath, toolAssetPath);

            // Refresh AssetDatabase to trigger Script Compilation
            AssetDatabase.Refresh();

            Debug.Log($"[Antimatter Marketplace] Tool {toolId} imported and scheduled for compilation.");
        }

        public static void UninstallTool(string toolId)
        {
            string toolPersistentPath = Path.Combine(BasePersistentPath, toolId);
            if (Directory.Exists(toolPersistentPath))
            {
                Directory.Delete(toolPersistentPath, true);
            }

            string toolAssetPath = Path.Combine(BaseAssetsPath, toolId);
            if (Directory.Exists(toolAssetPath))
            {
                FileUtil.DeleteFileOrDirectory(toolAssetPath);
                AssetDatabase.Refresh();
            }
        }
    }
}
