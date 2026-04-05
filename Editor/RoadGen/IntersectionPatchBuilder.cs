// =========================
// IntersectionPatchBuilder.cs (ROUNDED + MARKINGS)
// - Builds rounded "plus" junction patch (true rounded outline, ear-clipped triangulation)
// - Adds stop lines + dashed center lines
// =========================
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class IntersectionPatchBuilder
{
    // ------------------------------------------------------------
    // Public API
    // ------------------------------------------------------------
    public static GameObject BuildPatchesFromCenterlines(
        Scene scene,
        Terrain terrain,
        string parentName,
        List<Vector3> intersectionsWorld,
        List<List<Vector3>> surfaceCenterlines,
        float roadHalfWidth,
        float yOffset,
        Material patchMaterial,
        Material markingMaterial,
        bool addCollider = false)
    {
        if (intersectionsWorld == null || intersectionsWorld.Count == 0) return null;

        var parent = GameObject.Find(parentName);
        if (parent == null)
        {
            parent = new GameObject(parentName);
            Undo.RegisterCreatedObjectUndo(parent, "Create Junction Patches Group");
            SceneManager.MoveGameObjectToScene(parent, scene);
        }
        else
        {
            for (int i = parent.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.transform.GetChild(i).gameObject);
        }

        // Visual tuning (good defaults)
        float patchThickness = Mathf.Max(roadHalfWidth * 2.0f, 4f);      // ~ road width
        float armLen = Mathf.Max(roadHalfWidth * 3.6f, 8f);             // junction reach
        float roundR = Mathf.Clamp(patchThickness * 0.35f, 0.6f, 3.0f); // corner radius
        int arcSeg = 8;                                                 // higher = smoother

        // Markings tuning
        float centerLineW = Mathf.Clamp(roadHalfWidth * 0.12f, 0.18f, 0.5f);
        float dashLen = Mathf.Max(roadHalfWidth * 0.9f, 1.4f);
        float dashGap = Mathf.Max(roadHalfWidth * 0.6f, 1.0f);
        float stopLineW = Mathf.Clamp(roadHalfWidth * 0.18f, 0.25f, 0.7f);
        float stopLineLen = patchThickness * 0.95f;
        float stopOffset = Mathf.Max(roadHalfWidth * 1.15f, 2.2f);      // distance from center

        for (int i = 0; i < intersectionsWorld.Count; i++)
        {
            Vector3 ip = intersectionsWorld[i];

            // Terrain snap
            float y = ip.y;
            if (terrain != null && terrain.terrainData != null)
                y = terrain.SampleHeight(ip) + terrain.transform.position.y;

            // Dominant directions around this intersection (XZ)
            GetDominantDirections(ip, surfaceCenterlines, roadHalfWidth, out var dirs);

            // If we don't have at least 2 strong axes, fallback to rounded octagon.
            // For city-like networks you’ll almost always get >=2 here.
            bool canPlus = dirs.Count >= 2;

            // Local basis (x,z) for junction mesh
            Vector3 xAxis = (dirs.Count > 0 ? dirs[0] : Vector3.forward); xAxis.y = 0f;
            if (xAxis.sqrMagnitude < 0.0001f) xAxis = Vector3.forward;
            xAxis.Normalize();

            Vector3 zAxis = Vector3.Cross(Vector3.up, xAxis); // perpendicular
            if (zAxis.sqrMagnitude < 0.0001f) zAxis = Vector3.right;
            zAxis.Normalize();

            // If 2nd direction exists, check if it’s reasonably non-parallel; else we fallback.
            if (canPlus)
            {
                Vector3 d1 = dirs[1];
                d1.y = 0f; if (d1.sqrMagnitude < 0.0001f) d1 = zAxis;
                d1.Normalize();

                float parallel = Mathf.Abs(Vector3.Dot(xAxis, d1));
                if (parallel > 0.90f) canPlus = false;
            }

            // Root
            var go = new GameObject($"Junction_{i:000}");
            Undo.RegisterCreatedObjectUndo(go, "Create Junction");
            go.transform.SetParent(parent.transform, true);
            go.transform.position = new Vector3(ip.x, y + yOffset, ip.z);
            go.transform.rotation = Quaternion.identity;

            // --- Patch Mesh ---
            Mesh patchMesh;
            if (canPlus)
            {
                patchMesh = BuildRoundedPlusMesh(armLen, patchThickness, roundR, arcSeg);
                // plus mesh is axis-aligned in local XZ, so no rotation required
            }
            else
            {
                float radius = Mathf.Max(roadHalfWidth * 1.8f, 5.0f);
                patchMesh = BuildRoundedOctagon(radius, roundR, arcSeg);
            }

            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mf.sharedMesh = TransformMeshXZ(patchMesh, xAxis, zAxis);
            if (patchMaterial != null) mr.sharedMaterial = patchMaterial;

            if (addCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
            }

            // --- Markings Mesh ---
            if (markingMaterial != null)
            {
                var mk = new GameObject($"JunctionMarkings_{i:000}");
                Undo.RegisterCreatedObjectUndo(mk, "Create Junction Markings");
                mk.transform.SetParent(go.transform, false);
                mk.transform.localPosition = Vector3.up * 0.02f;

                var mkMf = mk.AddComponent<MeshFilter>();
                var mkMr = mk.AddComponent<MeshRenderer>();
                mkMr.sharedMaterial = markingMaterial;

                Mesh markings;
                if (canPlus)
                {
                    markings = BuildPlusMarkings(
                        armLen,
                        patchThickness,
                        centerLineW,
                        dashLen,
                        dashGap,
                        stopLineW,
                        stopLineLen,
                        stopOffset
                    );
                }
                else
                {
                    markings = BuildOctagonMarkings(centerLineW, radius: Mathf.Max(roadHalfWidth * 1.8f, 5.0f));
                }

                mkMf.sharedMesh = TransformMeshXZ(markings, xAxis, zAxis);
            }
        }

        return parent;
    }

    // ------------------------------------------------------------
    // Dominant direction estimation (XZ) from centerlines near point
    // ------------------------------------------------------------
    private static void GetDominantDirections(
        Vector3 ip,
        List<List<Vector3>> centerlines,
        float roadHalfWidth,
        out List<Vector3> dirsOut)
    {
        dirsOut = new List<Vector3>();
        if (centerlines == null) return;

        float searchRadius = Mathf.Max(roadHalfWidth * 2.8f, 10f);
        float searchRadiusSqr = searchRadius * searchRadius;

        var raw = new List<Vector3>(32);

        foreach (var line in centerlines)
        {
            if (line == null || line.Count < 2) continue;

            for (int i = 0; i < line.Count - 1; i++)
            {
                Vector3 a = line[i];
                Vector3 b = line[i + 1];

                float d2 = DistPointToSegmentXZ_Sqr(ip, a, b);
                if (d2 > searchRadiusSqr) continue;

                Vector3 dir = b - a;
                dir.y = 0f;
                if (dir.sqrMagnitude < 0.0001f) continue;
                dir.Normalize();

                // unify sign so opposite directions cluster together
                if (dir.x < 0f) dir = -dir;
                raw.Add(dir);
            }
        }

        if (raw.Count == 0) return;

        float cosThresh = Mathf.Cos(18f * Mathf.Deg2Rad); // cluster within ~18°
        var clusters = new List<(Vector3 sum, int count)>();

        for (int i = 0; i < raw.Count; i++)
        {
            Vector3 d = raw[i];
            bool placed = false;

            for (int c = 0; c < clusters.Count; c++)
            {
                Vector3 avg = clusters[c].sum.normalized;
                if (Mathf.Abs(Vector3.Dot(avg, d)) >= cosThresh)
                {
                    clusters[c] = (clusters[c].sum + d, clusters[c].count + 1);
                    placed = true;
                    break;
                }
            }

            if (!placed) clusters.Add((d, 1));
        }

        clusters.Sort((a, b) => b.count.CompareTo(a.count));

        int take = Mathf.Min(4, clusters.Count);
        for (int i = 0; i < take; i++)
        {
            Vector3 d = clusters[i].sum;
            d.y = 0f;
            if (d.sqrMagnitude < 0.0001f) continue;
            dirsOut.Add(d.normalized);
        }
    }

    private static float DistPointToSegmentXZ_Sqr(Vector3 p, Vector3 a, Vector3 b)
    {
        Vector2 P = new Vector2(p.x, p.z);
        Vector2 A = new Vector2(a.x, a.z);
        Vector2 B = new Vector2(b.x, b.z);
        Vector2 AB = B - A;

        float denom = Vector2.Dot(AB, AB);
        if (denom < 1e-6f) return (P - A).sqrMagnitude;

        float t = Mathf.Clamp01(Vector2.Dot(P - A, AB) / denom);
        Vector2 H = A + AB * t;
        return (P - H).sqrMagnitude;
    }

    // ------------------------------------------------------------
    // Rounded geometry: rounded PLUS polygon + triangulation
    // ------------------------------------------------------------
    private static Mesh BuildRoundedPlusMesh(float armLen, float thickness, float radius, int arcSeg)
    {
        // Local axis-aligned plus shape:
        // Rectangle A: [-armLen, armLen] x [-t/2, t/2]
        // Rectangle B: [-t/2, t/2] x [-armLen, armLen]
        // We build the OUTLINE of the union with rounded corners.
        float t = thickness * 0.5f;
        float L = armLen;

        // Corner radius cannot exceed t
        float r = Mathf.Clamp(radius, 0.01f, t - 0.01f);

        // Outline (clockwise) of plus union, with 12 corners (then we round each corner with arc)
        // Points in XZ-plane (x,z):
        // Start at right arm top edge, go clockwise
        var corners = new List<Vector2>
        {
            new Vector2( L,  t),   // 0
            new Vector2( t,  t),   // 1
            new Vector2( t,  L),   // 2
            new Vector2(-t,  L),   // 3
            new Vector2(-t,  t),   // 4
            new Vector2(-L,  t),   // 5
            new Vector2(-L, -t),   // 6
            new Vector2(-t, -t),   // 7
            new Vector2(-t, -L),   // 8
            new Vector2( t, -L),   // 9
            new Vector2( t, -t),   // 10
            new Vector2( L, -t),   // 11
        };

        var poly = BuildRoundedPolygon(corners, r, arcSeg);
        return TriangulatePolygonToMesh(poly, "RoundedPlus");
    }

    private static Mesh BuildRoundedOctagon(float radius, float cornerR, int arcSeg)
    {
        int n = 8;
        var corners = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
        {
            float a = (i / (float)n) * Mathf.PI * 2f;
            corners.Add(new Vector2(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius));
        }

        float r = Mathf.Clamp(cornerR, 0.01f, radius * 0.25f);
        var poly = BuildRoundedPolygon(corners, r, arcSeg);
        return TriangulatePolygonToMesh(poly, "RoundedOctagon");
    }

    /// <summary>
    /// Rounds each vertex of a CW polygon by cutting a small segment off each adjacent edge
    /// and inserting an arc of arcSeg points.
    /// </summary>
    private static List<Vector2> BuildRoundedPolygon(List<Vector2> cwCorners, float r, int arcSeg)
    {
        var outPts = new List<Vector2>(cwCorners.Count * (arcSeg + 2));

        int n = cwCorners.Count;
        for (int i = 0; i < n; i++)
        {
            Vector2 prev = cwCorners[(i - 1 + n) % n];
            Vector2 cur = cwCorners[i];
            Vector2 next = cwCorners[(i + 1) % n];

            Vector2 v0 = (prev - cur).normalized; // incoming (from cur to prev)
            Vector2 v1 = (next - cur).normalized; // outgoing (from cur to next)

            // Points along edges where rounding begins/ends
            Vector2 p0 = cur + v0 * r;
            Vector2 p1 = cur + v1 * r;

            // Compute arc center for a simple circular approximation:
            // Use perpendicular bisector style: center = cur + bisector * (r / sin(theta/2))
            // theta is angle between -v0 and v1 (turn angle)
            Vector2 inDir = (-v0).normalized;
            Vector2 outDir = v1.normalized;

            float dot = Mathf.Clamp(Vector2.Dot(inDir, outDir), -1f, 1f);
            float theta = Mathf.Acos(dot);
            float sinHalf = Mathf.Sin(theta * 0.5f);

            if (sinHalf < 1e-4f)
            {
                // almost straight
                outPts.Add(p0);
                outPts.Add(p1);
                continue;
            }

            Vector2 bis = (inDir + outDir).normalized;
            float dist = r / sinHalf;

            // For CW polygon, the rounded arc center is on the "inside".
            // Determine inside by checking orientation.
            // We want bisector pointing inward; flip if needed.
            // Cross2D(inDir, outDir) < 0 for CW turn.
            float cross = Cross2D(inDir, outDir);
            if (cross > 0f) bis = -bis;

            Vector2 center = cur + bis * dist;

            // Build arc from p0 -> p1 around center
            float a0 = Mathf.Atan2(p0.y - center.y, p0.x - center.x);
            float a1 = Mathf.Atan2(p1.y - center.y, p1.x - center.x);

            // Ensure we move the short way consistent with CW polygon (decreasing angle)
            // normalize angles to [0,2pi)
            a0 = NormalizeAngle(a0);
            a1 = NormalizeAngle(a1);

            // For CW we generally step "down" angles; if not, wrap
            if (a0 < a1) a0 += Mathf.PI * 2f;

            outPts.Add(p0);
            for (int s = 1; s <= arcSeg; s++)
            {
                float t = s / (float)(arcSeg + 1);
                float ang = Mathf.Lerp(a0, a1, t);
                Vector2 pt = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * (Vector2.Distance(center, p0));
                outPts.Add(pt);
            }
            outPts.Add(p1);
        }

        // Dedup consecutive
        for (int i = outPts.Count - 2; i >= 0; i--)
        {
            if ((outPts[i] - outPts[i + 1]).sqrMagnitude < 1e-6f)
                outPts.RemoveAt(i + 1);
        }

        return outPts;
    }

    private static float NormalizeAngle(float a)
    {
        while (a < 0f) a += Mathf.PI * 2f;
        while (a >= Mathf.PI * 2f) a -= Mathf.PI * 2f;
        return a;
    }

    private static float Cross2D(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

    // ------------------------------------------------------------
    // Triangulation: ear clipping for simple polygon
    // ------------------------------------------------------------
    private static Mesh TriangulatePolygonToMesh(List<Vector2> polyCW, string name)
    {
        // Ear clipping expects CCW for "area positive". We’ll convert to CCW.
        var poly = new List<Vector2>(polyCW);
        if (SignedArea(poly) < 0f) poly.Reverse(); // ensure CCW

        var indices = EarClipTriangulate(poly);

        var verts = new Vector3[poly.Count];
        var uvs = new Vector2[poly.Count];

        // Basic planar UV mapping
        Bounds2D b = BoundsOf(poly);
        float invW = 1f / Mathf.Max(1e-3f, b.size.x);
        float invH = 1f / Mathf.Max(1e-3f, b.size.y);

        for (int i = 0; i < poly.Count; i++)
        {
            verts[i] = new Vector3(poly[i].x, 0f, poly[i].y);
            uvs[i] = new Vector2((poly[i].x - b.min.x) * invW, (poly[i].y - b.min.y) * invH);
        }

        var m = new Mesh();
        m.name = name;
        m.vertices = verts;
        m.uv = uvs;
        m.triangles = indices.ToArray();
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private struct Bounds2D
    {
        public Vector2 min;
        public Vector2 max;
        public Vector2 size => max - min;
    }

    private static Bounds2D BoundsOf(List<Vector2> poly)
    {
        Vector2 mn = poly[0], mx = poly[0];
        for (int i = 1; i < poly.Count; i++)
        {
            mn = Vector2.Min(mn, poly[i]);
            mx = Vector2.Max(mx, poly[i]);
        }
        return new Bounds2D { min = mn, max = mx };
    }

    private static float SignedArea(List<Vector2> poly)
    {
        float a = 0f;
        for (int i = 0; i < poly.Count; i++)
        {
            Vector2 p = poly[i];
            Vector2 q = poly[(i + 1) % poly.Count];
            a += (p.x * q.y - q.x * p.y);
        }
        return a * 0.5f;
    }

    private static List<int> EarClipTriangulate(List<Vector2> polyCCW)
    {
        int n = polyCCW.Count;
        var tris = new List<int>((n - 2) * 3);
        if (n < 3) return tris;

        var V = new List<int>(n);
        for (int i = 0; i < n; i++) V.Add(i);

        int guard = 0;
        while (V.Count > 3 && guard++ < 10000)
        {
            bool earFound = false;

            for (int i = 0; i < V.Count; i++)
            {
                int i0 = V[(i - 1 + V.Count) % V.Count];
                int i1 = V[i];
                int i2 = V[(i + 1) % V.Count];

                Vector2 a = polyCCW[i0];
                Vector2 b = polyCCW[i1];
                Vector2 c = polyCCW[i2];

                if (!IsConvex(a, b, c)) continue;

                bool anyInside = false;
                for (int k = 0; k < V.Count; k++)
                {
                    int vk = V[k];
                    if (vk == i0 || vk == i1 || vk == i2) continue;
                    if (PointInTri(polyCCW[vk], a, b, c))
                    {
                        anyInside = true;
                        break;
                    }
                }

                if (anyInside) continue;

                tris.Add(i0);
                tris.Add(i1);
                tris.Add(i2);
                V.RemoveAt(i);
                earFound = true;
                break;
            }

            if (!earFound) break;
        }

        if (V.Count == 3)
        {
            tris.Add(V[0]);
            tris.Add(V[1]);
            tris.Add(V[2]);
        }

        return tris;
    }

    private static bool IsConvex(Vector2 a, Vector2 b, Vector2 c)
    {
        // CCW convex check
        return Cross2D(b - a, c - b) > 0f;
    }

    private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        // Barycentric
        Vector2 v0 = c - a;
        Vector2 v1 = b - a;
        Vector2 v2 = p - a;

        float dot00 = Vector2.Dot(v0, v0);
        float dot01 = Vector2.Dot(v0, v1);
        float dot02 = Vector2.Dot(v0, v2);
        float dot11 = Vector2.Dot(v1, v1);
        float dot12 = Vector2.Dot(v1, v2);

        float denom = dot00 * dot11 - dot01 * dot01;
        if (Mathf.Abs(denom) < 1e-8f) return false;

        float inv = 1f / denom;
        float u = (dot11 * dot02 - dot01 * dot12) * inv;
        float v = (dot00 * dot12 - dot01 * dot02) * inv;

        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }

    // ------------------------------------------------------------
    // Markings for rounded PLUS
    // ------------------------------------------------------------
    private static Mesh BuildPlusMarkings(
        float armLen,
        float thickness,
        float centerLineW,
        float dashLen,
        float dashGap,
        float stopLineW,
        float stopLineLen,
        float stopOffset)
    {
        // Build in local axis (X and Z) then planner transforms to world basis.
        // 1) Dashed center lines on X and Z arms
        // 2) Stop lines across each arm

        var parts = new List<Mesh>();

        // dashed along X (positive and negative)
        parts.Add(BuildDashedStrip(
            axis: Axis.X,
            halfLen: armLen * 0.95f,
            stripW: centerLineW,
            dashLen: dashLen,
            dashGap: dashGap,
            gapAtCenter: thickness * 0.60f)); // no dashes through center

        // dashed along Z
        parts.Add(BuildDashedStrip(
            axis: Axis.Z,
            halfLen: armLen * 0.95f,
            stripW: centerLineW,
            dashLen: dashLen,
            dashGap: dashGap,
            gapAtCenter: thickness * 0.60f));

        // stop lines: across X arm (at +stopOffset and -stopOffset)
        parts.Add(BuildStopLine(Axis.X, +stopOffset, thickness, stopLineW, stopLineLen));
        parts.Add(BuildStopLine(Axis.X, -stopOffset, thickness, stopLineW, stopLineLen));

        // stop lines: across Z arm
        parts.Add(BuildStopLine(Axis.Z, +stopOffset, thickness, stopLineW, stopLineLen));
        parts.Add(BuildStopLine(Axis.Z, -stopOffset, thickness, stopLineW, stopLineLen));

        return CombineMeshes(parts);
    }

    private static Mesh BuildOctagonMarkings(float centerLineW, float radius)
    {
        // simple crosshair marking
        var parts = new List<Mesh>();
        parts.Add(BuildSolidRect(new Vector2(-radius * 0.7f, -centerLineW * 0.5f), new Vector2(radius * 0.7f, centerLineW * 0.5f)));
        parts.Add(BuildSolidRect(new Vector2(-centerLineW * 0.5f, -radius * 0.7f), new Vector2(centerLineW * 0.5f, radius * 0.7f)));
        return CombineMeshes(parts);
    }

    private enum Axis { X, Z }

    private static Mesh BuildDashedStrip(Axis axis, float halfLen, float stripW, float dashLen, float dashGap, float gapAtCenter)
    {
        // Build dashes from gapAtCenter -> halfLen on both sides
        var parts = new List<Mesh>();

        float start = gapAtCenter * 0.5f;
        float end = Mathf.Max(start, halfLen);

        // positive side
        float pos = start;
        while (pos + dashLen <= end)
        {
            parts.Add(BuildDash(axis, pos + dashLen * 0.5f, dashLen, stripW));
            pos += dashLen + dashGap;
        }

        // negative side
        pos = start;
        while (pos + dashLen <= end)
        {
            parts.Add(BuildDash(axis, -(pos + dashLen * 0.5f), dashLen, stripW));
            pos += dashLen + dashGap;
        }

        return CombineMeshes(parts);
    }

    private static Mesh BuildDash(Axis axis, float centerAlongAxis, float len, float w)
    {
        float hl = len * 0.5f;
        float hw = w * 0.5f;

        // Rectangle in XZ:
        // if axis X: x varies, z is width
        // if axis Z: z varies, x is width
        Vector2 min, max;
        if (axis == Axis.X)
        {
            min = new Vector2(centerAlongAxis - hl, -hw);
            max = new Vector2(centerAlongAxis + hl, hw);
        }
        else
        {
            min = new Vector2(-hw, centerAlongAxis - hl);
            max = new Vector2(hw, centerAlongAxis + hl);
        }

        return BuildSolidRect(min, max);
    }

    private static Mesh BuildStopLine(Axis armAxis, float along, float thickness, float lineW, float lineLen)
    {
        // Stop line is perpendicular to the arm axis, centered at 'along'
        float hw = lineW * 0.5f;
        float hl = lineLen * 0.5f;

        Vector2 min, max;

        if (armAxis == Axis.X)
        {
            // arm is X, stop line is oriented along Z (length on Z)
            min = new Vector2(along - hw, -hl);
            max = new Vector2(along + hw, hl);
        }
        else
        {
            // arm is Z, stop line length on X
            min = new Vector2(-hl, along - hw);
            max = new Vector2(hl, along + hw);
        }

        return BuildSolidRect(min, max);
    }

    private static Mesh BuildSolidRect(Vector2 min, Vector2 max)
    {
        var v = new Vector3[4]
        {
            new Vector3(min.x, 0f, min.y),
            new Vector3(max.x, 0f, min.y),
            new Vector3(min.x, 0f, max.y),
            new Vector3(max.x, 0f, max.y),
        };

        var uv = new Vector2[4]
        {
            new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1)
        };

        var t = new int[6] { 0, 2, 1, 1, 2, 3 };

        var m = new Mesh();
        m.name = "Rect";
        m.vertices = v;
        m.uv = uv;
        m.triangles = t;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }

    private static Mesh CombineMeshes(List<Mesh> parts)
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();

        int vOffset = 0;
        for (int m = 0; m < parts.Count; m++)
        {
            if (parts[m] == null) continue;

            var pv = parts[m].vertices;
            var pu = parts[m].uv;
            var pt = parts[m].triangles;

            verts.AddRange(pv);

            if (pu != null && pu.Length == pv.Length) uvs.AddRange(pu);
            else for (int i = 0; i < pv.Length; i++) uvs.Add(Vector2.zero);

            for (int i = 0; i < pt.Length; i++)
                tris.Add(pt[i] + vOffset);

            vOffset += pv.Length;
        }

        var outMesh = new Mesh();
        outMesh.name = "Combined";
        outMesh.SetVertices(verts);
        outMesh.SetUVs(0, uvs);
        outMesh.SetTriangles(tris, 0);
        outMesh.RecalculateNormals();
        outMesh.RecalculateBounds();
        return outMesh;
    }

    // ------------------------------------------------------------
    // Mesh transform: local XZ mesh -> world axes (xAxis,zAxis)
    // ------------------------------------------------------------
    private static Mesh TransformMeshXZ(Mesh src, Vector3 xAxis, Vector3 zAxis)
    {
        if (src == null) return null;

        var v = src.vertices;
        var outV = new Vector3[v.Length];

        for (int i = 0; i < v.Length; i++)
        {
            // src uses (x,0,z) in local plane
            outV[i] = xAxis * v[i].x + zAxis * v[i].z;
        }

        var m = new Mesh();
        m.name = src.name + "_Xformed";
        m.vertices = outV;
        m.uv = src.uv;
        m.triangles = src.triangles;
        m.RecalculateNormals();
        m.RecalculateBounds();
        return m;
    }
}
