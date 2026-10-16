using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using System;

namespace Antimatter
{
    public static class TerrainSnapper
    {
        private static bool IsGeneratedMeshRoadOrBridgeOrTunnel(GameObject go)
        {
            string n = (go.name ?? "").ToLowerInvariant();

            if (n == "ai_roadnetwork" || n == "ai_bridgenetwork" || n == "ai_tunnelnetwork" || n == "ai_roadhubs")
                return true;

            if (n.StartsWith("road_") || n.StartsWith("bridge_") || n.StartsWith("tunnel_") || n.StartsWith("portal_"))
                return true;

            var mf = go.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                string mn = mf.sharedMesh.name.ToLowerInvariant();
                if (mn.Contains("__ai_") && (mn.Contains("road") || mn.Contains("bridge") || mn.Contains("tunnel")))
                    return true;
            }

            return false;
        }

        public static void SnapSceneObjectsToTerrain(Scene scene, Terrain terrain, Func<GameObject, bool> filter = null)
        {
            if (terrain == null) return;

            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                foreach (var tr in roots[i].GetComponentsInChildren<Transform>(true))
                {
                    var go = tr.gameObject;
                    if (filter != null && !filter(go)) continue;

                    if (IsGeneratedMeshRoadOrBridgeOrTunnel(go)) continue;

                    string n = (go.name ?? "").ToLowerInvariant();

                    bool snap =
                        n.Contains("building") || n.Contains("house") || n.Contains("tower") ||
                        n.Contains("block") || n.Contains("settlement") || n.Contains("village") ||
                        n.Contains("town") || n.Contains("landmark") ||
                        n.Contains("med_") || n.Contains("house_") || n.Contains("bld_") || n.Contains("medbuilding_");

                    if (!snap) continue;

                    Vector3 p = go.transform.position;
                    float y = terrain.SampleHeight(p) + terrain.transform.position.y;

                    Undo.RecordObject(go.transform, "Snap To Terrain");

                    if (n.Contains("building") || n.Contains("house") || n.Contains("tower") || n.Contains("block") || n.Contains("landmark"))
                        go.transform.position = new Vector3(p.x, y + (go.transform.localScale.y * 0.5f), p.z);
                    else
                        go.transform.position = new Vector3(p.x, y, p.z);
                }
            }
        }

        public static void SnapBuildingsOnly(Scene scene, Terrain terrain)
        {
            SnapSceneObjectsToTerrain(scene, terrain, go =>
            {
                string n = (go.name ?? "").ToLowerInvariant();
                return n.Contains("building") || n.Contains("house") || n.Contains("tower") || n.Contains("block") || n.Contains("landmark");
            });
        }
    }
}
