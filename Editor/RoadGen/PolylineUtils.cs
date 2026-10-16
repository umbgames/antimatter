using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    
    public static class PolylineUtils
    {
        // Ramer–Douglas–Peucker in XZ plane
        public static List<Vector3> SimplifyXZ(List<Vector3> pts, float tolerance)
        {
            if (pts == null || pts.Count < 3) return pts;
    
            bool[] keep = new bool[pts.Count];
            keep[0] = keep[pts.Count - 1] = true;
    
            SimplifySection(pts, 0, pts.Count - 1, tolerance * tolerance, keep);
    
            var outPts = new List<Vector3>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
                if (keep[i]) outPts.Add(pts[i]);
    
            return outPts;
        }
    
        private static void SimplifySection(List<Vector3> pts, int a, int b, float tol2, bool[] keep)
        {
            if (b <= a + 1) return;
    
            Vector2 A = new Vector2(pts[a].x, pts[a].z);
            Vector2 B = new Vector2(pts[b].x, pts[b].z);
    
            float maxD2 = 0f;
            int idx = -1;
    
            for (int i = a + 1; i < b; i++)
            {
                Vector2 P = new Vector2(pts[i].x, pts[i].z);
                float d2 = DistPointSegment2(P, A, B);
                if (d2 > maxD2)
                {
                    maxD2 = d2;
                    idx = i;
                }
            }
    
            if (idx >= 0 && maxD2 > tol2)
            {
                keep[idx] = true;
                SimplifySection(pts, a, idx, tol2, keep);
                SimplifySection(pts, idx, b, tol2, keep);
            }
        }
    
        private static float DistPointSegment2(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, Vector2.Dot(ab, ab));
            t = Mathf.Clamp01(t);
            Vector2 proj = a + t * ab;
            return (p - proj).sqrMagnitude;
        }
    
        // Chaikin smoothing (keeps endpoints)
        public static List<Vector3> Chaikin(List<Vector3> pts, int iterations)
        {
            if (pts == null || pts.Count < 3 || iterations <= 0) return pts;
    
            var cur = new List<Vector3>(pts);
            for (int it = 0; it < iterations; it++)
            {
                var next = new List<Vector3>(cur.Count * 2);
                next.Add(cur[0]);
    
                for (int i = 0; i < cur.Count - 1; i++)
                {
                    Vector3 p0 = cur[i];
                    Vector3 p1 = cur[i + 1];
    
                    Vector3 q = Vector3.Lerp(p0, p1, 0.25f);
                    Vector3 r = Vector3.Lerp(p0, p1, 0.75f);
    
                    next.Add(q);
                    next.Add(r);
                }
    
                next.Add(cur[cur.Count - 1]);
                cur = next;
            }
    
            return cur;
        }

        // Catmull-Rom Spline Smoothing
        public static List<Vector3> CatmullRom(List<Vector3> points, int resolution)
        {
            if (points == null || points.Count < 3 || resolution <= 1) return points;

            List<Vector3> smoothPath = new List<Vector3>();
            
            // Loop over segments
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 p0 = i == 0 ? points[0] : points[i - 1];
                Vector3 p1 = points[i];
                Vector3 p2 = points[i + 1];
                Vector3 p3 = i == points.Count - 2 ? points[points.Count - 1] : points[i + 2];

                for (int j = 0; j < resolution; j++)
                {
                    float t = j / (float)resolution;
                    smoothPath.Add(GetCatmullRomPosition(t, p0, p1, p2, p3));
                }
            }
            // Add the final point
            smoothPath.Add(points[points.Count - 1]);
            
            return smoothPath;
        }

        private static Vector3 GetCatmullRomPosition(float t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            float t2 = t * t;
            float t3 = t2 * t;

            return 0.5f * (
                (2.0f * p1) +
                (-p0 + p2) * t +
                (2.0f * p0 - 5.0f * p1 + 4.0f * p2 - p3) * t2 +
                (-p0 + 3.0f * p1 - 3.0f * p2 + p3) * t3
            );
        }
    
        // Enforce max grade by clamping road profile toward terrain (and marking tunnel if needed externally).
        public static void ApplyGradeProfileToTerrain(
            TerrainCostGrid grid,
            List<Vector3> path,
            float yOffset,
            float maxGrade)
        {
            if (path == null || path.Count < 2) return;
    
            // Build target heights by "following terrain" but limited by grade
            float[] target = new float[path.Count];
    
            // initial
            target[0] = grid.HeightAtWorld(path[0]) + yOffset;
    
            // forward pass
            for (int i = 1; i < path.Count; i++)
            {
                float terrainY = grid.HeightAtWorld(path[i]) + yOffset;
                float dxz = Vector2.Distance(new Vector2(path[i - 1].x, path[i - 1].z), new Vector2(path[i].x, path[i].z));
                dxz = Mathf.Max(0.01f, dxz);
    
                float maxDy = maxGrade * dxz;
                float prev = target[i - 1];
    
                float dy = Mathf.Clamp(terrainY - prev, -maxDy, maxDy);
                target[i] = prev + dy;
            }
    
            // backward pass (reduces bias)
            for (int i = path.Count - 2; i >= 0; i--)
            {
                float dxz = Vector2.Distance(new Vector2(path[i + 1].x, path[i + 1].z), new Vector2(path[i].x, path[i].z));
                dxz = Mathf.Max(0.01f, dxz);
    
                float maxDy = maxGrade * dxz;
                float next = target[i + 1];
    
                float dy = Mathf.Clamp(target[i] - next, -maxDy, maxDy);
                target[i] = next + dy;
            }
    
            // write back Y
            for (int i = 0; i < path.Count; i++)
            {
                Vector3 p = path[i];
                p.y = target[i];
                path[i] = p;
            }
        }
    }
}
