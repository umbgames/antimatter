using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using System;
using System.Collections.Generic;

public static class FPSLayoutPass
{
    public static void Apply(Scene scene, string userPrompt)
    {
        if (!scene.IsValid()) return;

        using (new UndoScope("AI LevelGen (FPS Layout Pass)"))
        {
            // Arena config (small level)
            const float halfSize = 35f;          // XZ bounds
            const float wallHeight = 6f;
            const float wallThickness = 1.2f;

            // 1) Ensure floor
            var floor = Find(scene, "ArenaFloor");
            if (floor == null)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "ArenaFloor";
                Undo.RegisterCreatedObjectUndo(floor, "Create ArenaFloor");
                SceneManager.MoveGameObjectToScene(floor, scene);

                // Unity plane is 10x10 units at scale (1,1,1)
                // Want ~70x70 => scale 7
                floor.transform.position = Vector3.zero;
                floor.transform.rotation = Quaternion.identity;
                floor.transform.localScale = new Vector3(7f, 1f, 7f);
            }

            // 2) Ensure 4 walls
            EnsureWall(scene, "ArenaWall_N", new Vector3(0f, wallHeight * 0.5f,  halfSize), new Vector3(2 * halfSize, wallHeight, wallThickness), Quaternion.identity);
            EnsureWall(scene, "ArenaWall_S", new Vector3(0f, wallHeight * 0.5f, -halfSize), new Vector3(2 * halfSize, wallHeight, wallThickness), Quaternion.identity);
            EnsureWall(scene, "ArenaWall_E", new Vector3( halfSize, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, 2 * halfSize), Quaternion.identity);
            EnsureWall(scene, "ArenaWall_W", new Vector3(-halfSize, wallHeight * 0.5f, 0f), new Vector3(wallThickness, wallHeight, 2 * halfSize), Quaternion.identity);

            // 3) Ensure spawns
            EnsureSpawn(scene, "PlayerSpawn_A", new Vector3(-25f, 1f, 0f));
            EnsureSpawn(scene, "PlayerSpawn_B", new Vector3( 25f, 1f, 0f));

            // 4) Strategic cover placement (symmetric lanes + center)
            int seed = StablePromptSeed(userPrompt);
            var rng = new System.Random(seed);

            // Don’t duplicate if user/plan already made enough cover
            int existingCover = CountByPrefix(scene, "Cover_");
            int targetCover = Mathf.Max(12, existingCover);

            if (existingCover < 10)
            {
                // Core positions (designed lanes)
                var coverPositions = new List<Vector3>
                {
                    // center cluster
                    new Vector3( 0f, 1f,  0f),
                    new Vector3( 0f, 1f,  8f),
                    new Vector3( 0f, 1f, -8f),

                    // left lane cover
                    new Vector3(-12f, 1f,  12f),
                    new Vector3(-12f, 1f,  -12f),
                    new Vector3(-20f, 1f,   0f),

                    // right lane cover
                    new Vector3( 12f, 1f,  12f),
                    new Vector3( 12f, 1f, -12f),
                    new Vector3( 20f, 1f,  0f),

                    // mid flanks
                    new Vector3(-6f, 1f,  20f),
                    new Vector3( 6f, 1f,  20f),
                    new Vector3(-6f, 1f, -20f),
                    new Vector3( 6f, 1f, -20f),
                };

                int coverIndex = 1;
                for (int i = 0; i < coverPositions.Count; i++)
                {
                    string id = $"Cover_{coverIndex:00}";
                    coverIndex++;

                    if (Find(scene, id) != null) continue;

                    var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    go.name = id;
                    Undo.RegisterCreatedObjectUndo(go, "Create Cover");
                    SceneManager.MoveGameObjectToScene(go, scene);

                    // randomize size a bit but keep FPS-readable cover
                    float w = 1.5f + (float)rng.NextDouble() * 1.5f;  // 1.5 - 3.0
                    float h = 1.2f + (float)rng.NextDouble() * 1.6f;  // 1.2 - 2.8
                    float d = 1.0f + (float)rng.NextDouble() * 2.0f;  // 1.0 - 3.0

                    go.transform.position = new Vector3(coverPositions[i].x, h * 0.5f, coverPositions[i].z);
                    go.transform.localScale = new Vector3(w, h, d);

                    // rotate some for variety / angles
                    float yaw = (rng.Next(0, 4) * 45f);
                    go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                }

                // Add a few “thin obstacle walls” to shape lanes (choke points)
                EnsureThinObstacle(scene, "Obstacle_Wall_01", new Vector3(0f, 1.5f,  16f), new Vector3(10f, 3f, 0.8f), Quaternion.Euler(0f, 0f, 0f));
                EnsureThinObstacle(scene, "Obstacle_Wall_02", new Vector3(0f, 1.5f, -16f), new Vector3(10f, 3f, 0.8f), Quaternion.Euler(0f, 0f, 0f));
                EnsureThinObstacle(scene, "Obstacle_Wall_03", new Vector3(-16f, 1.5f, 0f), new Vector3(0.8f, 3f, 10f), Quaternion.Euler(0f, 0f, 0f));
                EnsureThinObstacle(scene, "Obstacle_Wall_04", new Vector3( 16f, 1.5f, 0f), new Vector3(0.8f, 3f, 10f), Quaternion.Euler(0f, 0f, 0f));
            }
        }
    }

    private static void EnsureWall(Scene scene, string id, Vector3 pos, Vector3 size, Quaternion rot)
    {
        var w = Find(scene, id);
        if (w != null) return;

        w = GameObject.CreatePrimitive(PrimitiveType.Cube);
        w.name = id;
        Undo.RegisterCreatedObjectUndo(w, "Create Wall");
        SceneManager.MoveGameObjectToScene(w, scene);

        w.transform.position = pos;
        w.transform.rotation = rot;
        w.transform.localScale = size;
    }

    private static void EnsureThinObstacle(Scene scene, string id, Vector3 pos, Vector3 size, Quaternion rot)
    {
        var o = Find(scene, id);
        if (o != null) return;

        o = GameObject.CreatePrimitive(PrimitiveType.Cube);
        o.name = id;
        Undo.RegisterCreatedObjectUndo(o, "Create Obstacle");
        SceneManager.MoveGameObjectToScene(o, scene);

        o.transform.position = pos;
        o.transform.rotation = rot;
        o.transform.localScale = size;
    }

    private static void EnsureSpawn(Scene scene, string id, Vector3 pos)
    {
        var s = Find(scene, id);
        if (s != null) return;

        s = new GameObject(id);
        Undo.RegisterCreatedObjectUndo(s, "Create Spawn");
        SceneManager.MoveGameObjectToScene(s, scene);

        s.transform.position = pos;
        s.transform.rotation = Quaternion.identity;
        s.transform.localScale = Vector3.one;
    }

    private static GameObject Find(Scene scene, string name)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var t = FindInHierarchy(roots[i].transform, name);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindInHierarchy(Transform root, string name)
    {
        if (root.name == name) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var found = FindInHierarchy(root.GetChild(i), name);
            if (found != null) return found;
        }
        return null;
    }

    private static int CountByPrefix(Scene scene, string prefix)
    {
        int count = 0;
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            foreach (var t in roots[i].GetComponentsInChildren<Transform>(true))
            {
                if (t.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    count++;
            }
        }
        return count;
    }

    private static int StablePromptSeed(string s)
    {
        unchecked
        {
            if (string.IsNullOrEmpty(s)) return 1337;
            int h = 23;
            for (int i = 0; i < s.Length; i++)
                h = h * 31 + s[i];
            return h;
        }
    }

    private sealed class UndoScope : IDisposable
    {
        private readonly int group;
        public UndoScope(string name)
        {
            Undo.IncrementCurrentGroup();
            group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(name);
        }
        public void Dispose()
        {
            Undo.CollapseUndoOperations(group);
        }
    }
}
