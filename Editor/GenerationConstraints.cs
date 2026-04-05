using System;
using UnityEngine;

[Serializable]
public class GenerationConstraints
{
    public int maxObjectsPerScene = 200;
    public int maxScenes = 3;

    public bool forbidScripts = false;
    public bool forbidTerrain = false;
    public bool forbidMaterials = false;

    // If true, executor will apply deterministic arrangement overrides when it detects “city”
    public bool enableAutoArrange = true;

    // If true, force camera/light default behavior regardless of model forgetfulness
    public bool forceEnsureCameraLight = true;

    public string ToPromptClause()
    {
        return
            "CONSTRAINTS:\n" +
            $"- Max scenes: {maxScenes}\n" +
            $"- Max objects per scene: {maxObjectsPerScene}\n" +
            $"- Scripts allowed: {!forbidScripts}\n" +
            $"- Terrain allowed: {!forbidTerrain}\n" +
            $"- Materials allowed: {!forbidMaterials}\n" +
            $"- Auto-arrange enabled: {enableAutoArrange}\n" +
            $"- Force camera/light defaults: {forceEnsureCameraLight}\n" +
            "- Do not exceed limits. If necessary, simplify.\n";
    }

}
