using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class BridgeBuilder
{
    public static GameObject BuildBridge(
        Scene scene,
        Terrain terrain,
        string name,
        List<Vector3> deckPath,          // centerline, Y is the deck height
        float halfWidth,
        float deckThickness,
        float pillarSpacing,
        float pillarWidth,
        Material deckMaterial = null,
        Material pillarMaterial = null,
        Material markingMaterial = null,
        // Upgrades:
        float railHeight = 1.2f,
        float railThickness = 0.18f,
        float markingWidth = 0.15f,
        float markingYOffset = 0.02f,
        float dashLength = 3.0f,
        float dashGap = 2.0f,
        bool enableTruss = true,
        float trussHeight = 4.5f,
        float trussThickness = 0.25f,
        float trussPostSpacing = 6f)
    {
        if (!scene.IsValid()) return null;
        if (terrain == null || terrain.terrainData == null) return null;
        if (deckPath == null || deckPath.Count < 2) return null;

        halfWidth = Mathf.Max(0.5f, halfWidth);
        deckThickness = Mathf.Max(0.05f, deckThickness);
        pillarSpacing = Mathf.Max(2f, pillarSpacing);
        pillarWidth = Mathf.Max(0.5f, pillarWidth);

        railHeight = Mathf.Max(0.2f, railHeight);
        railThickness = Mathf.Max(0.02f, railThickness);
        markingWidth = Mathf.Max(0.02f, markingWidth);
        markingYOffset = Mathf.Max(0.0f, markingYOffset);
        dashLength = Mathf.Max(0.2f, dashLength);
        dashGap = Mathf.Max(0.1f, dashGap);

        if (markingMaterial == null) markingMaterial = deckMaterial;

        // Root
        GameObject root = GameObject.Find(name);
        if (root == null)
        {
            root = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(root, "Create Bridge");
            SceneManager.MoveGameObjectToScene(root, scene);
        }

        // Clear old
        for (int i = root.transform.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.transform.GetChild(i).gameObject);

        var deckRoot = new GameObject("Deck");
        var railsRoot = new GameObject("Rails");
        var marksRoot = new GameObject("Markings");
        var piersRoot = new GameObject("Piers");
        var abutRoot = new GameObject("Abutments");
        var trussRoot = new GameObject("Truss");

        deckRoot.transform.SetParent(root.transform, false);
        railsRoot.transform.SetParent(root.transform, false);
        marksRoot.transform.SetParent(root.transform, false);
        piersRoot.transform.SetParent(root.transform, false);
        abutRoot.transform.SetParent(root.transform, false);
        trussRoot.transform.SetParent(root.transform, false);

        // --- 1) DECK (solid ribbon) ---
        var deckGO = new GameObject("DeckMesh");
        deckGO.transform.SetParent(deckRoot.transform, false);
        AddMeshObject(deckGO, BuildSolidRibbon(deckPath, halfWidth, deckThickness, yUp: Vector3.up), deckMaterial);

        // --- 2) RAILS (two extruded strips) ---
        // Left rail centerline = deckPath offset left by (halfWidth - railThickness*0.5)
        var leftRailPath  = OffsetPath(deckPath, -(halfWidth - railThickness * 0.5f));
        var rightRailPath = OffsetPath(deckPath, +(halfWidth - railThickness * 0.5f));

        var leftRail = new GameObject("LeftRail");
        leftRail.transform.SetParent(railsRoot.transform, false);
        AddMeshObject(leftRail, BuildRaisedStrip(leftRailPath, railThickness * 0.5f, railHeight, deckThickness, yUp: Vector3.up), deckMaterial);

        var rightRail = new GameObject("RightRail");
        rightRail.transform.SetParent(railsRoot.transform, false);
        AddMeshObject(rightRail, BuildRaisedStrip(rightRailPath, railThickness * 0.5f, railHeight, deckThickness, yUp: Vector3.up), deckMaterial);

        // --- 3) LANE MARKINGS (center dashed strip) ---
        // Dashes: thin quads repeated along path length
        var marksGO = new GameObject("CenterDashes");
        marksGO.transform.SetParent(marksRoot.transform, false);
        var dashMesh = BuildDashedCenterLine(deckPath, markingWidth, deckThickness + markingYOffset, dashLength, dashGap);
        AddMeshObject(marksGO, dashMesh, markingMaterial, addCollider: false);

        // --- 4) ABUTMENTS (end caps + small end blocks) ---
        BuildAbutment(abutRoot.transform, terrain, deckPath, atStart: true, halfWidth: halfWidth, deckThickness: deckThickness, deckMaterial);
        BuildAbutment(abutRoot.transform, terrain, deckPath, atStart: false, halfWidth: halfWidth, deckThickness: deckThickness, deckMaterial);

        // --- 5) PIERS (pillars + caps) ---
        BuildPiers(piersRoot.transform, terrain, deckPath, pillarSpacing, pillarWidth, halfWidth, deckThickness, pillarMaterial);

        // --- 6) SIMPLE SIDE TRUSS (optional) ---
        if (enableTruss)
            BuildSideTruss(trussRoot.transform, deckPath, halfWidth, deckThickness, trussHeight, trussThickness, trussPostSpacing, deckMaterial);

        return root;
    }

    // ============================================================
    // Mesh helpers
    // ============================================================

    private static void AddMeshObject(GameObject go, Mesh m, Material mat, bool addCollider = true)
    {
        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        mf.sharedMesh = m;
        if (mat != null) mr.sharedMaterial = mat;

        if (addCollider)
        {
            var mc = go.AddComponent<MeshCollider>();
            mc.sharedMesh = m;
        }
    }

    // Solid ribbon: top + bottom + sides
    private static Mesh BuildSolidRibbon(List<Vector3> path, float halfWidth, float thickness, Vector3 yUp)
    {
        int n = path.Count;
        var verts = new Vector3[n * 4]; // top L/R + bottom L/R
        var uvs = new Vector2[n * 4];
        var tris = new List<int>((n - 1) * 24);

        float total = 0f;
        var lens = new float[n];
        for (int i = 1; i < n; i++)
        {
            total += Vector3.Distance(path[i - 1], path[i]);
            lens[i] = total;
        }
        if (total < 0.001f) total = 1f;

        for (int i = 0; i < n; i++)
        {
            Vector3 p = path[i];
            Vector3 fwd = GetFwd(path, i);
            Vector3 right = Vector3.Cross(yUp, fwd).normalized;

            Vector3 topL = p - right * halfWidth;
            Vector3 topR = p + right * halfWidth;
            Vector3 botL = topL - yUp * thickness;
            Vector3 botR = topR - yUp * thickness;

            int b = i * 4;
            verts[b + 0] = topL;
            verts[b + 1] = topR;
            verts[b + 2] = botL;
            verts[b + 3] = botR;

            float v = lens[i] / total;
            uvs[b + 0] = new Vector2(0f, v);
            uvs[b + 1] = new Vector2(1f, v);
            uvs[b + 2] = new Vector2(0f, v);
            uvs[b + 3] = new Vector2(1f, v);
        }

        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;

            // TOP
            AddQuad(tris, a + 0, b + 0, a + 1, b + 1);
            // BOTTOM (reverse)
            AddQuad(tris, a + 3, b + 3, a + 2, b + 2);
            // LEFT
            AddQuad(tris, a + 2, b + 2, a + 0, b + 0);
            // RIGHT
            AddQuad(tris, a + 1, b + 1, a + 3, b + 3);
        }

        var m = new Mesh();
        m.SetVertices(verts);
        m.SetUVs(0, uvs);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Raised strip (rails): a narrow strip extruded upward from deck top
    private static Mesh BuildRaisedStrip(List<Vector3> path, float halfStripWidth, float height, float deckThickness, Vector3 yUp)
    {
        int n = path.Count;
        // top L/R, bottom L/R (bottom sits on deck top)
        var verts = new Vector3[n * 4];
        var tris = new List<int>((n - 1) * 24);

        for (int i = 0; i < n; i++)
        {
            Vector3 p = path[i];
            Vector3 fwd = GetFwd(path, i);
            Vector3 right = Vector3.Cross(yUp, fwd).normalized;

            // base sits on deck top (not underside)
            Vector3 baseL = p - right * halfStripWidth;
            Vector3 baseR = p + right * halfStripWidth;

            Vector3 topL = baseL + yUp * height;
            Vector3 topR = baseR + yUp * height;

            int b = i * 4;
            verts[b + 0] = topL;
            verts[b + 1] = topR;
            verts[b + 2] = baseL;
            verts[b + 3] = baseR;
        }

        for (int i = 0; i < n - 1; i++)
        {
            int a = i * 4;
            int b = (i + 1) * 4;

            // TOP
            AddQuad(tris, a + 0, b + 0, a + 1, b + 1);
            // BOTTOM
            AddQuad(tris, a + 3, b + 3, a + 2, b + 2);
            // LEFT
            AddQuad(tris, a + 2, b + 2, a + 0, b + 0);
            // RIGHT
            AddQuad(tris, a + 1, b + 1, a + 3, b + 3);
        }

        var m = new Mesh();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    // Dashed centerline as thin quads placed along the path
    private static Mesh BuildDashedCenterLine(List<Vector3> path, float halfWidth, float yLiftFromDeckTop, float dashLen, float dashGap)
    {
        // Build dashes in XZ projection along accumulated length.
        // Each dash is a small quad strip between two points, centered on path.
        var verts = new List<Vector3>();
        var tris = new List<int>();

        float total = 0f;
        var segLens = new float[path.Count];
        for (int i = 1; i < path.Count; i++)
        {
            total += Vector3.Distance(path[i - 1], path[i]);
            segLens[i] = total;
        }
        if (total < 0.01f) total = 1f;

        float cursor = 0f;
        while (cursor < total)
        {
            float a = cursor;
            float b = Mathf.Min(total, cursor + dashLen);
            if (b > a + 0.05f)
                AddDash(path, segLens, a, b, halfWidth, yLiftFromDeckTop, verts, tris);
            cursor += dashLen + dashGap;
        }

        var m = new Mesh();
        m.SetVertices(verts);
        m.SetTriangles(tris, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static void AddDash(
        List<Vector3> path,
        float[] lens,
        float aLen,
        float bLen,
        float halfWidth,
        float yLiftFromDeckTop,
        List<Vector3> verts,
        List<int> tris)
    {
        Vector3 p0 = SampleAlong(path, lens, aLen);
        Vector3 p1 = SampleAlong(path, lens, bLen);

        // Keep level in world Y based on sampled Y, lift slightly above deck
        p0.y += yLiftFromDeckTop;
        p1.y += yLiftFromDeckTop;

        Vector3 fwd = (p1 - p0);
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;

        Vector3 aL = p0 - right * halfWidth;
        Vector3 aR = p0 + right * halfWidth;
        Vector3 bL = p1 - right * halfWidth;
        Vector3 bR = p1 + right * halfWidth;

        int baseIndex = verts.Count;
        verts.Add(aL); verts.Add(aR); verts.Add(bL); verts.Add(bR);

        // quad
        tris.Add(baseIndex + 0); tris.Add(baseIndex + 2); tris.Add(baseIndex + 1);
        tris.Add(baseIndex + 1); tris.Add(baseIndex + 2); tris.Add(baseIndex + 3);
    }

    private static Vector3 SampleAlong(List<Vector3> path, float[] lens, float targetLen)
    {
        if (targetLen <= 0f) return path[0];
        float total = lens[lens.Length - 1];
        if (targetLen >= total) return path[path.Count - 1];

        // find segment
        int i = 1;
        while (i < lens.Length && lens[i] < targetLen) i++;

        float prev = lens[i - 1];
        float cur = lens[i];
        float t = Mathf.InverseLerp(prev, cur, targetLen);
        return Vector3.Lerp(path[i - 1], path[i], t);
    }

    private static void BuildAbutment(Transform parent, Terrain terrain, List<Vector3> deckPath, bool atStart, float halfWidth, float deckThickness, Material mat)
    {
        int i0 = atStart ? 0 : deckPath.Count - 1;
        int i1 = atStart ? 1 : deckPath.Count - 2;

        Vector3 p0 = deckPath[i0];
        Vector3 p1 = deckPath[i1];

        Vector3 fwd = (p1 - p0);
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        float groundY = terrain.SampleHeight(p0) + terrain.transform.position.y;

        // End cap block
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = atStart ? "Abutment_A" : "Abutment_B";
        cap.transform.SetParent(parent, false);
        cap.transform.position = new Vector3(p0.x, (p0.y + groundY) * 0.5f, p0.z);
        cap.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);

        float h = Mathf.Max(2f, p0.y - groundY);
        cap.transform.localScale = new Vector3(halfWidth * 2.4f, h, Mathf.Max(2f, halfWidth * 0.8f));

        var r = cap.GetComponent<Renderer>();
        if (r != null && mat != null) r.sharedMaterial = mat;
    }

    private static void BuildPiers(
        Transform parent,
        Terrain terrain,
        List<Vector3> deckPath,
        float spacing,
        float pierWidth,
        float halfWidth,
        float deckThickness,
        Material mat)
    {
        float accum = 0f;
        Vector3 prev = deckPath[0];

        for (int i = 1; i < deckPath.Count; i++)
        {
            Vector3 cur = deckPath[i];
            float segLen = Vector3.Distance(prev, cur);
            if (segLen < 0.001f) { prev = cur; continue; }

            Vector3 dir = (cur - prev) / segLen;

            float t = 0f;
            while (t <= segLen)
            {
                if (accum <= 0f)
                {
                    Vector3 p = prev + dir * t;
                    CreatePier(parent, terrain, p, pierWidth, halfWidth, deckThickness, mat);
                    accum = spacing;
                }

                float step = Mathf.Min(accum, segLen - t);
                t += step;
                accum -= step;
            }

            prev = cur;
        }
    }

    private static void CreatePier(Transform parent, Terrain terrain, Vector3 deckPoint, float pierWidth, float halfWidth, float deckThickness, Material mat)
    {
        float groundY = terrain.SampleHeight(deckPoint) + terrain.transform.position.y;
        float topY = deckPoint.y;

        float h = topY - groundY;
        if (h < 3.0f) return;

        // Main pier
        var pier = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pier.name = "Pier";
        pier.transform.SetParent(parent, false);
        pier.transform.position = new Vector3(deckPoint.x, (topY + groundY) * 0.5f, deckPoint.z);
        pier.transform.localScale = new Vector3(pierWidth, h, pierWidth);

        // Pier cap (wider) just under deck
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cap.name = "PierCap";
        cap.transform.SetParent(parent, false);
        cap.transform.position = new Vector3(deckPoint.x, topY - deckThickness * 0.8f, deckPoint.z);
        cap.transform.localScale = new Vector3(halfWidth * 2.2f, Mathf.Max(0.4f, deckThickness * 0.6f), pierWidth * 1.8f);

        if (mat != null)
        {
            var r1 = pier.GetComponent<Renderer>(); if (r1 != null) r1.sharedMaterial = mat;
            var r2 = cap.GetComponent<Renderer>();  if (r2 != null) r2.sharedMaterial = mat;
        }
    }

    private static void BuildSideTruss(
        Transform parent,
        List<Vector3> deckPath,
        float halfWidth,
        float deckThickness,
        float trussHeight,
        float trussThickness,
        float postSpacing,
        Material mat)
    {
        // Very lightweight “engineering look”:
        // - vertical posts at intervals
        // - top chord beam along both sides
        // - optional diagonal braces (cheap)

        // Side paths at edge
        var leftEdge  = OffsetPath(deckPath, -halfWidth);
        var rightEdge = OffsetPath(deckPath, +halfWidth);

        // Top chord paths elevated
        var leftTop  = LiftPath(leftEdge,  trussHeight);
        var rightTop = LiftPath(rightEdge, trussHeight);

        // Top chords (solid strips)
        var leftChord = new GameObject("LeftTopChord");
        leftChord.transform.SetParent(parent, false);
        AddMeshObject(leftChord, BuildRaisedStrip(leftTop, trussThickness * 0.5f, trussThickness, 0f, Vector3.up), mat);

        var rightChord = new GameObject("RightTopChord");
        rightChord.transform.SetParent(parent, false);
        AddMeshObject(rightChord, BuildRaisedStrip(rightTop, trussThickness * 0.5f, trussThickness, 0f, Vector3.up), mat);

        // Posts
        BuildPosts(parent, leftEdge, leftTop, postSpacing, trussThickness, mat, "LeftPost");
        BuildPosts(parent, rightEdge, rightTop, postSpacing, trussThickness, mat, "RightPost");
    }

    private static void BuildPosts(Transform parent, List<Vector3> basePath, List<Vector3> topPath, float spacing, float thickness, Material mat, string name)
    {
        float accum = 0f;
        Vector3 prev = basePath[0];

        for (int i = 1; i < basePath.Count; i++)
        {
            Vector3 cur = basePath[i];
            float segLen = Vector3.Distance(prev, cur);
            if (segLen < 0.001f) { prev = cur; continue; }

            Vector3 dir = (cur - prev) / segLen;

            float t = 0f;
            while (t <= segLen)
            {
                if (accum <= 0f)
                {
                    Vector3 p = prev + dir * t;
                    Vector3 top = new Vector3(p.x, topPath[i].y, p.z);

                    var post = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    post.name = name;
                    post.transform.SetParent(parent, false);
                    post.transform.position = new Vector3(p.x, (p.y + top.y) * 0.5f, p.z);
                    post.transform.localScale = new Vector3(thickness, Mathf.Max(0.5f, top.y - p.y), thickness);

                    if (mat != null)
                    {
                        var r = post.GetComponent<Renderer>();
                        if (r != null) r.sharedMaterial = mat;
                    }

                    accum = spacing;
                }

                float step = Mathf.Min(accum, segLen - t);
                t += step;
                accum -= step;
            }

            prev = cur;
        }
    }

    private static List<Vector3> OffsetPath(List<Vector3> path, float offset)
    {
        var outp = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i];
            Vector3 fwd = GetFwd(path, i);
            Vector3 right = Vector3.Cross(Vector3.up, fwd).normalized;
            outp.Add(p + right * offset);
        }
        return outp;
    }

    private static List<Vector3> LiftPath(List<Vector3> path, float yLift)
    {
        var outp = new List<Vector3>(path.Count);
        for (int i = 0; i < path.Count; i++)
        {
            Vector3 p = path[i];
            outp.Add(new Vector3(p.x, p.y + yLift, p.z));
        }
        return outp;
    }

    private static Vector3 GetFwd(List<Vector3> path, int i)
    {
        Vector3 fwd;
        if (i == 0) fwd = (path[i + 1] - path[i]);
        else if (i == path.Count - 1) fwd = (path[i] - path[i - 1]);
        else fwd = (path[i + 1] - path[i - 1]);

        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        return fwd.normalized;
    }

    private static void AddQuad(List<int> tris, int i0, int i1, int i2, int i3)
    {
        tris.Add(i0); tris.Add(i1); tris.Add(i2);
        tris.Add(i2); tris.Add(i1); tris.Add(i3);
    }
}
