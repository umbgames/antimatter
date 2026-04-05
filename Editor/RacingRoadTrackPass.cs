using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;


public static class RacingRoadTrackPass
{
    private const string RootName = "AI_Racing";
    private const string TrackTag = "Untagged"; // change if you have a Track tag
    private const float BarrierHeight = 1.2f;
    private const float BarrierThickness = 0.35f;

    public static void Apply(Scene scene, int seed, RoadNetworkPlanner.RoadNetworkResult roads, bool clearPrevious)
    {
        if (!scene.IsValid()) throw new Exception("RacingRoadTrackPass: invalid scene.");

        var root = EnsureRoot(scene, RootName);

        if (clearPrevious)
            ClearChildren(root);

        // 1) Find generated road objects in scene (adjust names to your RoadNetworkPlanner outputs)
        var roadRoot = FindFirst(scene, "AI_RoadNetwork") ?? FindFirst(scene, "AI_RoadNetworkRoot");
        if (roadRoot == null)
        {
            // Fallback: scan for any road-like meshes
            roadRoot = FindAnyRoadLike(scene);
        }

        if (roadRoot == null)
            throw new Exception("Racing LevelGen: no road network found. Ensure worldGen.enabled=true and generateRoadNetwork=true.");

        // 2) Treat road meshes as the track (simple: group reference + add colliders if missing)
        MarkRoadMeshesAsTrack(roadRoot);

        // 3) Compute track bounds from roadRoot meshes
        var bounds = ComputeHierarchyBounds(roadRoot);
        if (bounds.size.sqrMagnitude < 0.01f)
            throw new Exception("Racing LevelGen: road bounds invalid.");

        // 4) Start/Finish placement
        // Prefer roads result if it exposes a "main path"; otherwise pick one edge of bounds
        Vector3 startPos = bounds.center + new Vector3(-bounds.extents.x * 0.6f, 0f, -bounds.extents.z * 0.2f);
        Vector3 fwd = Vector3.forward;

        EnsureStartFinish(root, startPos, fwd);

        // 5) Checkpoints: use roads polylines if available; else distribute around bounds
        if (roads != null)
        {
            // If your RoadNetworkResult exposes polylines/paths, plug them here.
            // Since we don't see its fields, we do a safe fallback.
            EnsureCheckpointRing(root, bounds, 6);
        }
        else
        {
            EnsureCheckpointRing(root, bounds, 6);
        }

        // 6) Barriers around the track area (cheap, deterministic guard-rail)
        EnsureOuterBarrierBox(root, bounds);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    // ---------------- Track capture ----------------

    private static void MarkRoadMeshesAsTrack(GameObject roadRoot)
    {
        foreach (var mf in roadRoot.GetComponentsInChildren<MeshFilter>(true))
        {
            var go = mf.gameObject;
            if (go.GetComponent<MeshRenderer>() == null) continue;

            // Ensure collider
            if (go.GetComponent<MeshCollider>() == null && go.GetComponent<Collider>() == null)
                Undo.AddComponent<MeshCollider>(go);

            go.tag = TrackTag;
        }
    }

    private static Bounds ComputeHierarchyBounds(GameObject root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return new Bounds(root.transform.position, Vector3.zero);

        Bounds b = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            b.Encapsulate(renderers[i].bounds);

        return b;
    }

    // ---------------- Start/Finish ----------------

    private static void EnsureStartFinish(GameObject root, Vector3 pos, Vector3 forward)
    {
        var start = CreateBox(root, "StartFinishLine", pos + new Vector3(0, 0.05f, 0), new Vector3(10f, 0.1f, 0.6f));
        start.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);
        EnsureTriggerGate(root, "StartFinishGate", pos + new Vector3(0, 1.5f, 0), new Vector3(10f, 3f, 0.4f), forward);
    }

    // ---------------- Checkpoints ----------------

    private static void EnsureCheckpointRing(GameObject root, Bounds b, int count)
    {
        count = Mathf.Clamp(count, 3, 20);

        for (int i = 0; i < count; i++)
        {
            float t = (count == 0) ? 0f : (i / (float)count);
            float ang = t * Mathf.PI * 2f;

            Vector3 p = b.center + new Vector3(Mathf.Cos(ang) * b.extents.x * 0.7f, 0f, Mathf.Sin(ang) * b.extents.z * 0.7f);
            Vector3 look = (b.center - p);
            if (look.sqrMagnitude < 0.01f) look = Vector3.forward;

            EnsureTriggerGate(root, $"CheckpointGate_{i + 1:00}", p + new Vector3(0, 1.5f, 0), new Vector3(8f, 3f, 0.35f), look);
        }
    }

    private static void EnsureTriggerGate(GameObject root, string name, Vector3 pos, Vector3 size, Vector3 forward)
    {
        var gate = CreateBox(root, name, pos, size);
        gate.transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

        var bc = gate.GetComponent<BoxCollider>();
        if (bc == null) bc = Undo.AddComponent<BoxCollider>(gate);
        bc.isTrigger = true;
    }

    // ---------------- Barriers ----------------

    private static void EnsureOuterBarrierBox(GameObject root, Bounds b)
    {
        // A simple perimeter barrier around the overall road bounds.
        float y = b.min.y + BarrierHeight * 0.5f;

        Vector3 c = b.center;
        float w = b.size.x + 6f;
        float l = b.size.z + 6f;

        CreateBox(root, "Barrier_Left",  new Vector3(c.x - w * 0.5f, y, c.z), new Vector3(BarrierThickness, BarrierHeight, l));
        CreateBox(root, "Barrier_Right", new Vector3(c.x + w * 0.5f, y, c.z), new Vector3(BarrierThickness, BarrierHeight, l));
        CreateBox(root, "Barrier_Front", new Vector3(c.x, y, c.z + l * 0.5f), new Vector3(w, BarrierHeight, BarrierThickness));
        CreateBox(root, "Barrier_Back",  new Vector3(c.x, y, c.z - l * 0.5f), new Vector3(w, BarrierHeight, BarrierThickness));
    }

    // ---------------- Scene helpers ----------------

    private static GameObject EnsureRoot(Scene scene, string name)
    {
        var existing = FindFirst(scene, name);
        if (existing != null) return existing;

        var go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, "Racing Create Root");
        SceneManager.MoveGameObjectToScene(go, scene);
        return go;
    }

    private static void ClearChildren(GameObject root)
    {
        for (int i = root.transform.childCount - 1; i >= 0; i--)
        {
            var child = root.transform.GetChild(i).gameObject;
            Undo.DestroyObjectImmediate(child);
        }
    }

    private static GameObject CreateBox(GameObject parent, string name, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(go, "Racing Create");
        go.name = name;
        Undo.SetTransformParent(go.transform, parent.transform, "Racing Parent");

        Undo.RecordObject(go.transform, "Racing Transform");
        go.transform.position = pos;
        go.transform.localScale = scale;
        return go;
    }

    private static GameObject FindFirst(Scene scene, string exactName)
    {
        var roots = scene.GetRootGameObjects();
        for (int i = 0; i < roots.Length; i++)
        {
            var t = FindInHierarchy(roots[i].transform, exactName);
            if (t != null) return t.gameObject;
        }
        return null;
    }

    private static Transform FindInHierarchy(Transform root, string exactName)
    {
        if (root.name.Equals(exactName, StringComparison.OrdinalIgnoreCase)) return root;
        for (int i = 0; i < root.childCount; i++)
        {
            var f = FindInHierarchy(root.GetChild(i), exactName);
            if (f != null) return f;
        }
        return null;
    }

    private static GameObject FindAnyRoadLike(Scene scene)
    {
        var roots = scene.GetRootGameObjects();
        foreach (var r in roots)
        {
            foreach (var mf in r.GetComponentsInChildren<MeshFilter>(true))
            {
                string n = (mf.gameObject.name ?? "").ToLowerInvariant();
                if (n.StartsWith("road_") || n.Contains("road"))
                    return mf.gameObject.transform.root.gameObject;
            }
        }
        return null;
    }
}
