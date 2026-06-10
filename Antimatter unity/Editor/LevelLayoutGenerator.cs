using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class LevelLayoutGenerator
    {
        private const string RootName = "AI_LevelLayout";
    
        // Marker ids
        private const string ArenaCenter = "LM_ArenaCenter";
        private const string ArenaSize   = "LM_ArenaSize";   // use scale: x=width, y=wallHeight, z=length
        private const string PlayerSpawn = "LM_PlayerSpawn";
        private const string MidHotspot  = "LM_MidHotspot";
    
        public static void BuildFromMarkers(Scene scene, int seed, bool clearPreviousLayout = true)
        {
            var markers = CollectMarkers(scene);
    
            Require(markers, ArenaCenter);
            Require(markers, ArenaSize);
            Require(markers, PlayerSpawn);
            Require(markers, MidHotspot);
            Require(markers, "LM_LaneA_Entry");
            Require(markers, "LM_LaneA_Exit");
            Require(markers, "LM_LaneB_Entry");
            Require(markers, "LM_LaneB_Exit");
    
            var enemySpawns = markers
                .Where(kv => kv.Key.StartsWith("LM_EnemySpawn_", StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Value.position)
                .ToList();
    
            if (enemySpawns.Count < 3)
                throw new Exception("Need at least 3 enemy spawns: LM_EnemySpawn_01..03");
    
            Vector3 center = markers[ArenaCenter].position;
    
            // ArenaSize marker uses scale as dimensions
            Vector3 arenaScale = markers[ArenaSize].localScale;
            float width  = Mathf.Clamp(arenaScale.x, 18f, 120f);
            float length = Mathf.Clamp(arenaScale.z, 18f, 160f);
            float wallH  = Mathf.Clamp(arenaScale.y <= 0.01f ? 6f : arenaScale.y, 3f, 18f);
    
            var coverZones = markers
                .Where(kv => kv.Key.StartsWith("LM_CoverZone_", StringComparison.OrdinalIgnoreCase))
                .Select(kv => kv.Value.position)
                .ToList();
    
            if (clearPreviousLayout)
                ClearExistingLayout(scene);
    
            var root = GetOrCreateRoot(scene);
            var rng = new System.Random(seed);
    
            CreateArena(root.transform, center, width, length, wallH);
    
            CreateLaneObstacles(root.transform, rng, center,
                markers["LM_LaneA_Entry"].position, markers["LM_LaneA_Exit"].position, width, length);
    
            CreateLaneObstacles(root.transform, rng, center,
                markers["LM_LaneB_Entry"].position, markers["LM_LaneB_Exit"].position, width, length);
    
            CreateMidFightCover(root.transform, rng, markers[MidHotspot].position);
    
            // If AI didn’t provide cover zones, derive a few automatically around mid + lanes
            if (coverZones.Count == 0)
            {
                coverZones = DeriveDefaultCoverZones(center,
                    markers["LM_LaneA_Entry"].position, markers["LM_LaneA_Exit"].position,
                    markers["LM_LaneB_Entry"].position, markers["LM_LaneB_Exit"].position,
                    markers[MidHotspot].position);
            }
    
            CreateCoverClusters(root.transform, rng, coverZones, markers[PlayerSpawn].position, enemySpawns);
    
            CreateSpawnPad(root.transform, markers[PlayerSpawn].position, "PlayerSpawnPad");
            for (int i = 0; i < enemySpawns.Count; i++)
                CreateSpawnPad(root.transform, enemySpawns[i], $"EnemySpawnPad_{i + 1:00}");
        }
    
        // ---------------- markers ----------------
    
        private static Dictionary<string, Transform> CollectMarkers(Scene scene)
        {
            var dict = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name.StartsWith("LM_", StringComparison.OrdinalIgnoreCase))
                        dict[t.name] = t;
                }
            }
            return dict;
        }
    
        private static Transform Require(Dictionary<string, Transform> markers, string id)
        {
            if (!markers.TryGetValue(id, out var t) || t == null)
                throw new Exception($"LevelGen marker missing: {id}");
            return t;
        }
    
        private static List<Vector3> DeriveDefaultCoverZones(Vector3 center, Vector3 a0, Vector3 a1, Vector3 b0, Vector3 b1, Vector3 mid)
        {
            // deterministic “good enough” fallback zones
            Vector3 laneADir = (a1 - a0); laneADir.y = 0; laneADir = laneADir.sqrMagnitude < 0.001f ? Vector3.forward : laneADir.normalized;
            Vector3 laneBDir = (b1 - b0); laneBDir.y = 0; laneBDir = laneBDir.sqrMagnitude < 0.001f ? Vector3.right : laneBDir.normalized;
    
            Vector3 rightA = new Vector3(-laneADir.z, 0, laneADir.x);
            Vector3 rightB = new Vector3(-laneBDir.z, 0, laneBDir.x);
    
            return new List<Vector3>
            {
                mid + rightA * 6f,
                mid - rightA * 6f,
                mid + rightB * 6f,
                mid - rightB * 6f,
                center + rightA * 10f + laneADir * 8f,
                center - rightA * 10f - laneADir * 8f
            };
        }
    
        // ---------------- layout objects ----------------
    
        private static void ClearExistingLayout(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (string.Equals(go.name, RootName, StringComparison.OrdinalIgnoreCase))
                {
                    Undo.DestroyObjectImmediate(go);
                    return;
                }
            }
        }
    
        private static GameObject GetOrCreateRoot(Scene scene)
        {
            foreach (var go in scene.GetRootGameObjects())
                if (string.Equals(go.name, RootName, StringComparison.OrdinalIgnoreCase))
                    return go;
    
            var created = new GameObject(RootName);
            SceneManager.MoveGameObjectToScene(created, scene);
            Undo.RegisterCreatedObjectUndo(created, "Create Level Layout Root");
            return created;
        }
    
        private static void CreateArena(Transform parent, Vector3 center, float w, float l, float wallH)
        {
            CreateBox(parent, "ArenaFloor", center + new Vector3(0, -0.5f, 0), new Vector3(w, 1f, l), isStatic: true);
    
            float t = 1f;
            CreateBox(parent, "Wall_N", center + new Vector3(0, wallH * 0.5f, +l * 0.5f), new Vector3(w, wallH, t), true);
            CreateBox(parent, "Wall_S", center + new Vector3(0, wallH * 0.5f, -l * 0.5f), new Vector3(w, wallH, t), true);
            CreateBox(parent, "Wall_E", center + new Vector3(+w * 0.5f, wallH * 0.5f, 0), new Vector3(t, wallH, l), true);
            CreateBox(parent, "Wall_W", center + new Vector3(-w * 0.5f, wallH * 0.5f, 0), new Vector3(t, wallH, l), true);
        }
    
        private static void CreateLaneObstacles(Transform parent, System.Random rng, Vector3 center, Vector3 entry, Vector3 exit, float w, float l)
        {
            Vector3 dir = (exit - entry);
            dir.y = 0;
            if (dir.sqrMagnitude < 0.001f) return;
            dir.Normalize();
    
            Vector3 start = entry;
            Vector3 end = exit;
    
            int segments = 6;
            for (int i = 1; i <= segments; i++)
            {
                float u = i / (float)(segments + 1);
                Vector3 p = Vector3.Lerp(start, end, u);
                p.y = 0;
    
                float side = (rng.NextDouble() > 0.5) ? 1f : -1f;
                Vector3 right = new Vector3(-dir.z, 0, dir.x);
                p += right * side * (2.2f + (float)rng.NextDouble() * 1.4f);
    
                p.x = Mathf.Clamp(p.x, center.x - w * 0.45f, center.x + w * 0.45f);
                p.z = Mathf.Clamp(p.z, center.z - l * 0.45f, center.z + l * 0.45f);
    
                float len = 3.5f + (float)rng.NextDouble() * 2.0f;
                float h = 1.2f + (float)rng.NextDouble() * 0.8f;
    
                var go = CreateBox(parent, $"LaneCover_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                    p + new Vector3(0, h * 0.5f, 0),
                    new Vector3(len, h, 1.0f),
                    isStatic: true);
    
                go.transform.rotation = Quaternion.LookRotation(dir);
            }
        }
    
        private static void CreateMidFightCover(Transform parent, System.Random rng, Vector3 mid)
        {
            float r = 3.8f;
            for (int i = 0; i < 4; i++)
            {
                float a = i * Mathf.PI * 0.5f;
                Vector3 p = mid + new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r);
                CreateBox(parent, $"MidPillar_{i + 1}", p + new Vector3(0, 1.5f, 0), new Vector3(1.3f, 3.0f, 1.3f), true);
            }
    
            CreateBox(parent, "MidCrate", mid + new Vector3(0, 0.6f, 0), new Vector3(2.0f, 1.2f, 2.0f), true);
        }
    
        private static void CreateCoverClusters(Transform parent, System.Random rng, List<Vector3> zones, Vector3 playerSpawn, List<Vector3> enemySpawns)
        {
            if (zones == null || zones.Count == 0) return;
    
            int perZone = 4;
            foreach (var z in zones)
            {
                for (int i = 0; i < perZone; i++)
                {
                    Vector3 p = z + new Vector3(
                        (float)(rng.NextDouble() * 6 - 3),
                        0,
                        (float)(rng.NextDouble() * 6 - 3)
                    );
    
                    if ((p - playerSpawn).sqrMagnitude < 10f * 10f) continue;
                    if (enemySpawns.Any(es => (p - es).sqrMagnitude < 8f * 8f)) continue;
    
                    float h = 1.0f + (float)rng.NextDouble() * 1.2f;
                    float sx = 1.4f + (float)rng.NextDouble() * 2.0f;
                    float sz = 1.4f + (float)rng.NextDouble() * 2.0f;
    
                    CreateBox(parent, $"Cover_{Guid.NewGuid().ToString("N").Substring(0, 6)}",
                        p + new Vector3(0, h * 0.5f, 0),
                        new Vector3(sx, h, sz),
                        true);
                }
            }
        }
    
        private static void CreateSpawnPad(Transform parent, Vector3 pos, string name)
        {
            CreateBox(parent, name, pos + new Vector3(0, 0.05f, 0), new Vector3(2.0f, 0.1f, 2.0f), true);
        }
    
        private static GameObject CreateBox(Transform parent, string name, Vector3 pos, Vector3 size, bool isStatic)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(go, "Create Layout Object");
            go.name = name;
            go.transform.position = pos;
            go.transform.localScale = size;
            go.isStatic = isStatic;
            go.transform.SetParent(parent, true);
            return go;
        }
    }
}
