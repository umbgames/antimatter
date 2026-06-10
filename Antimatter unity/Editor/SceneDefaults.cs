using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class SceneDefaults
    {
        // Named constants instead of magic numbers
        private const float DefaultCameraNear = 0.1f;
        private const float DefaultCameraFar = 1000f;
        private const float DefaultLightIntensity = 1f;
    
        public static void EnsureCameraAndLightIfNeeded(Scene scene, bool ensure)
        {
            if (!ensure) return;
    
            bool hasCamera = false;
            bool hasLight = false;
    
            foreach (var root in scene.GetRootGameObjects())
            {
                if (!hasCamera && root.GetComponentInChildren<Camera>(true) != null) hasCamera = true;
                if (!hasLight && root.GetComponentInChildren<Light>(true) != null) hasLight = true;
                if (hasCamera && hasLight) break;
            }
    
            if (!hasCamera)
            {
                var camGo = new GameObject("MainCamera");
                camGo.tag = "MainCamera";
                var cam = camGo.AddComponent<Camera>();
                cam.nearClipPlane = DefaultCameraNear;
                cam.farClipPlane = DefaultCameraFar;
    
                if (UnityEngine.Object.FindObjectOfType<AudioListener>() == null)
                    camGo.AddComponent<AudioListener>();
    
                SceneManager.MoveGameObjectToScene(camGo, scene);
            }
    
            if (!hasLight)
            {
                var lightGo = new GameObject("Directional Light");
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Directional;
                light.intensity = DefaultLightIntensity;
                SceneManager.MoveGameObjectToScene(lightGo, scene);
            }
        }
    }
}
