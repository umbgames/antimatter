// Assets/Editor/RacingLayoutPass.cs
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor.SceneManagement;

namespace Antimatter
{
    
    
    public static class RacingLayoutPass
    {
        // Root names for grouping generated objects
        private const string RootTrack = "AI_Track";
        private const string RootRace = "AI_Race";
    
        // Deterministic settings (don’t use Random without a stable seed)
        private const float GroundY = 0f;
        private const float WallHeight = 2.0f;
        private const float WallThickness = 0.6f;
        private const float CheckpointWidth = 8.0f;
        private const float CheckpointHeight = 3.0f;
    
        public static void Apply(Scene scene, string userPrompt)
        {
            if (!scene.IsValid()) throw new Exception("RacingLayoutPass: invalid scene.");
    
            // 1) Markers (required)
            var trackCenter = Find(scene, "LM_TrackCenter");
            var trackSize = Find(scene, "LM_TrackSize");
    
            if (trackCenter == null) throw new Exception("Racing LevelGen: missing marker 'LM_TrackCenter'.");
            if (trackSize == null) throw new Exception("Racing LevelGen: missing marker 'LM_TrackSize'.");
    
            Vector3 center = trackCenter.transform.position;
    
            float width = Mathf.Max(20f, trackSize.transform.localScale.x);
            float length = Mathf.Max(40f, trackSize.transform.localScale.z);
    
            // 2) Ensure roots
            var trackRoot = EnsureRoot(scene, RootTrack);
            var raceRoot  = EnsureRoot(scene, RootRace);
    
            // 3) Build the track ground + walls
            EnsureTrackGround(trackRoot, center, width, length);
            EnsureTrackWalls(trackRoot, center, width, length);
    
            // 4) Start grid (marker optional)
            var startGrid = EnsureMarker(scene, "LM_StartGrid", center + new Vector3(-width * 0.35f, 0f, -length * 0.35f));
            startGrid.transform.position = new Vector3(startGrid.transform.position.x, GroundY + 0.1f, startGrid.transform.position.z);
    
            EnsureStartGridVisual(raceRoot, startGrid.transform.position, forward: Vector3.forward);
    
            // 5) Checkpoints (optional markers -> auto-create a loop)
            EnsureCheckpointSet(scene, raceRoot, center, width, length);
    
            // 6) Simple AI racing line (optional helper)
            EnsureRacingLine(raceRoot, center, width, length);
    
            // 7) Cleanup heights
            SnapChildrenToY(trackRoot, GroundY);
            SnapChildrenToY(raceRoot, GroundY);
    
            EditorSceneManager.MarkSceneDirty(scene);
        }
    
        // ---------------- Track ----------------
    
        private static void EnsureTrackGround(GameObject trackRoot, Vector3 center, float width, float length)
        {
            var ground = EnsureChild(trackRoot, "TrackGround", PrimitiveType.Plane);
            Undo.RecordObject(ground.transform, "Racing Track Ground");
            ground.transform.position = new Vector3(center.x, GroundY, center.z);
            // Unity Plane is 10x10 units, scale multiplies that
            ground.transform.localScale = new Vector3(width / 10f, 1f, length / 10f);
    
            EnsureMaterial(ground, "MAT_Track", new Color(0.12f, 0.12f, 0.12f, 1f), metallic: 0.0f, smoothness: 0.35f);
        }
    
        private static void EnsureTrackWalls(GameObject trackRoot, Vector3 center, float width, float length)
        {
            // Rectangular boundary walls (4 sides)
            // Coordinates are aligned to world axes; deterministic.
            float halfW = width * 0.5f;
            float halfL = length * 0.5f;
    
            // Left / Right walls (along Z)
            CreateWall(trackRoot, "Wall_Left",
                pos: center + new Vector3(-halfW, WallHeight * 0.5f, 0f),
                size: new Vector3(WallThickness, WallHeight, length + WallThickness));
    
            CreateWall(trackRoot, "Wall_Right",
                pos: center + new Vector3(halfW, WallHeight * 0.5f, 0f),
                size: new Vector3(WallThickness, WallHeight, length + WallThickness));
    
            // Front / Back walls (along X)
            CreateWall(trackRoot, "Wall_Front",
                pos: center + new Vector3(0f, WallHeight * 0.5f, halfL),
                size: new Vector3(width + WallThickness, WallHeight, WallThickness));
    
            CreateWall(trackRoot, "Wall_Back",
                pos: center + new Vector3(0f, WallHeight * 0.5f, -halfL),
                size: new Vector3(width + WallThickness, WallHeight, WallThickness));
        }
    
        private static void CreateWall(GameObject root, string name, Vector3 pos, Vector3 size)
        {
            var wall = EnsureChild(root, name, PrimitiveType.Cube);
            Undo.RecordObject(wall.transform, "Racing Track Wall");
            wall.transform.position = pos;
            wall.transform.localScale = size;
    
            EnsureMaterial(wall, "MAT_Wall", new Color(0.85f, 0.85f, 0.85f, 1f), metallic: 0.0f, smoothness: 0.2f);
    
            // Collider is included automatically for primitive Cube, but keep it explicit if user strips it
            if (wall.GetComponent<BoxCollider>() == null) Undo.AddComponent<BoxCollider>(wall);
        }
    
        // ---------------- Start Grid ----------------
    
        private static void EnsureStartGridVisual(GameObject raceRoot, Vector3 startPos, Vector3 forward)
        {
            // A simple start line strip + 6 grid boxes behind it
            var startLine = EnsureChild(raceRoot, "StartLine", PrimitiveType.Cube);
            Undo.RecordObject(startLine.transform, "Racing StartLine");
            startLine.transform.position = new Vector3(startPos.x, GroundY + 0.05f, startPos.z);
            startLine.transform.localScale = new Vector3(10f, 0.1f, 0.6f);
            startLine.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
            EnsureMaterial(startLine, "MAT_StartLine", new Color(1f, 1f, 1f, 1f), metallic: 0f, smoothness: 0.1f);
    
            // Grid slots (2 columns x 3 rows)
            float colOffset = 2.2f;
            float rowStep = 3.0f;
            int idx = 0;
    
            for (int r = 0; r < 3; r++)
            {
                for (int c = -1; c <= 1; c += 2)
                {
                    idx++;
                    var slot = EnsureChild(raceRoot, $"StartSlot_{idx:00}", PrimitiveType.Cube);
                    Undo.RecordObject(slot.transform, "Racing StartSlot");
                    Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
                    Vector3 back = -forward.normalized;
                    Vector3 p = startPos + right * (c * colOffset) + back * (r * rowStep + 1.2f);
                    slot.transform.position = new Vector3(p.x, GroundY + 0.03f, p.z);
                    slot.transform.localScale = new Vector3(1.2f, 0.06f, 2.2f);
                    slot.transform.rotation = Quaternion.LookRotation(forward, Vector3.up);
    
                    EnsureMaterial(slot, "MAT_GridSlot", new Color(1f, 1f, 0.1f, 1f), metallic: 0f, smoothness: 0.05f);
                }
            }
        }
    
        // ---------------- Checkpoints ----------------
    
        private static void EnsureCheckpointSet(Scene scene, GameObject raceRoot, Vector3 center, float width, float length)
        {
            // Try to find any existing LM_Checkpoint_ markers
            var markers = scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                .Where(t => t != null && t.name.StartsWith("LM_Checkpoint_", StringComparison.OrdinalIgnoreCase))
                .Select(t => t.gameObject)
                .OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
    
            // If none exist, create a default 6-checkpoint loop
            if (markers.Length == 0)
            {
                Vector3[] pts = DefaultCheckpointLoop(center, width, length);
                for (int i = 0; i < pts.Length; i++)
                    EnsureMarker(scene, $"LM_Checkpoint_{(i + 1):00}", pts[i]);
                markers = scene.GetRootGameObjects()
                    .SelectMany(r => r.GetComponentsInChildren<Transform>(true))
                    .Where(t => t != null && t.name.StartsWith("LM_Checkpoint_", StringComparison.OrdinalIgnoreCase))
                    .Select(t => t.gameObject)
                    .OrderBy(go => go.name, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
    
            // For each marker, create a visible gate
            for (int i = 0; i < markers.Length; i++)
            {
                var m = markers[i];
                Vector3 p = m.transform.position;
                p.y = GroundY + 0.1f;
    
                // Direction: face towards next checkpoint (or towards center if last)
                Vector3 look = (i < markers.Length - 1)
                    ? (markers[i + 1].transform.position - p)
                    : (center - p);
    
                if (look.sqrMagnitude < 0.01f) look = Vector3.forward;
    
                var gate = EnsureChild(raceRoot, $"CheckpointGate_{i + 1:00}", PrimitiveType.Cube);
                Undo.RecordObject(gate.transform, "Racing CheckpointGate");
                gate.transform.position = new Vector3(p.x, GroundY + (CheckpointHeight * 0.5f), p.z);
                gate.transform.localScale = new Vector3(CheckpointWidth, CheckpointHeight, 0.35f);
                gate.transform.rotation = Quaternion.LookRotation(look.normalized, Vector3.up);
    
                EnsureMaterial(gate, "MAT_Checkpoint", new Color(0.2f, 0.7f, 1f, 0.9f), metallic: 0f, smoothness: 0.2f);
    
                // Trigger collider for checkpoint
                var bc = gate.GetComponent<BoxCollider>();
                if (bc == null) bc = Undo.AddComponent<BoxCollider>(gate);
                bc.isTrigger = true;
            }
        }
    
        private static Vector3[] DefaultCheckpointLoop(Vector3 center, float width, float length)
        {
            float halfW = width * 0.5f;
            float halfL = length * 0.5f;
    
            // A “rounded rectangle-ish” loop using 6 points
            return new[]
            {
                center + new Vector3(-halfW * 0.25f, 0f, -halfL * 0.35f),
                center + new Vector3( halfW * 0.30f, 0f, -halfL * 0.15f),
                center + new Vector3( halfW * 0.35f, 0f,  halfL * 0.25f),
                center + new Vector3( halfW * 0.00f, 0f,  halfL * 0.35f),
                center + new Vector3(-halfW * 0.35f, 0f,  halfL * 0.10f),
                center + new Vector3(-halfW * 0.30f, 0f, -halfL * 0.15f),
            };
        }
    
        // ---------------- Racing line helper ----------------
    
        private static void EnsureRacingLine(GameObject raceRoot, Vector3 center, float width, float length)
        {
            // A simple centerline strip (not a spline) so you can visually see direction
            var line = EnsureChild(raceRoot, "RacingLine", PrimitiveType.Cube);
            Undo.RecordObject(line.transform, "Racing RacingLine");
            line.transform.position = new Vector3(center.x, GroundY + 0.02f, center.z);
            line.transform.localScale = new Vector3(width * 0.2f, 0.05f, length * 0.7f);
            EnsureMaterial(line, "MAT_Line", new Color(1f, 0.2f, 0.2f, 1f), metallic: 0f, smoothness: 0.05f);
        }
    
        // ---------------- Utilities ----------------
    
        private static GameObject EnsureRoot(Scene scene, string name)
        {
            var go = Find(scene, name);
            if (go != null) return go;
    
            go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Racing Create Root");
            SceneManager.MoveGameObjectToScene(go, scene);
            return go;
        }
    
        private static GameObject EnsureChild(GameObject parent, string name, PrimitiveType type)
        {
            var tr = parent.transform.Find(name);
            if (tr != null) return tr.gameObject;
    
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Undo.RegisterCreatedObjectUndo(go, "Racing Create Object");
            Undo.SetTransformParent(go.transform, parent.transform, "Racing Parent");
            return go;
        }
    
        private static GameObject Find(Scene scene, string exactName)
        {
            if (!scene.IsValid()) return null;
            var roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                var found = FindInHierarchy(roots[i].transform, exactName);
                if (found != null) return found.gameObject;
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
    
        private static GameObject EnsureMarker(Scene scene, string name, Vector3 pos)
        {
            var m = Find(scene, name);
            if (m != null) return m;
    
            m = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(m, "Racing Create Marker");
            SceneManager.MoveGameObjectToScene(m, scene);
            Undo.RecordObject(m.transform, "Racing Position Marker");
            m.transform.position = new Vector3(pos.x, GroundY, pos.z);
            m.transform.localScale = Vector3.one;
            return m;
        }
    
        private static void SnapChildrenToY(GameObject root, float y)
        {
            if (root == null) return;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t == null) continue;
                // Don’t snap walls/vertical gates incorrectly
                if (t.name.StartsWith("Wall_", StringComparison.OrdinalIgnoreCase)) continue;
                if (t.name.StartsWith("CheckpointGate_", StringComparison.OrdinalIgnoreCase)) continue;
    
                Vector3 p = t.position;
                if (Mathf.Abs(p.y - y) > 0.001f)
                {
                    Undo.RecordObject(t, "Racing Snap Y");
                    t.position = new Vector3(p.x, y, p.z);
                }
            }
        }
    
        private static void EnsureMaterial(GameObject go, string matName, Color c, float metallic, float smoothness)
        {
            if (go == null) return;
            var r = go.GetComponent<Renderer>();
            if (r == null) return;
    
            // Create or reuse editor-time material asset
            var mat = AssetDatabase.LoadAssetAtPath<Material>($"Assets/{matName}.mat");
            if (mat == null)
            {
                mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
                mat.name = matName;
                mat.color = c;
                TrySetFloat(mat, "_Metallic", metallic);
                TrySetFloat(mat, "_Smoothness", smoothness);
    
                AssetDatabase.CreateAsset(mat, $"Assets/{matName}.mat");
                AssetDatabase.SaveAssets();
            }
    
            Undo.RecordObject(r, "Racing Assign Material");
            r.sharedMaterial = mat;
        }
    
        private static void TrySetFloat(Material m, string prop, float v)
        {
            if (m == null) return;
            if (m.HasProperty(prop)) m.SetFloat(prop, v);
        }
    }
}
