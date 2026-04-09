// =========================
// RoadCenterlineConnector.cs
// =========================
using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    
    public static class RoadCenterlineConnector
    {
        public struct Settings
        {
            public float snapRadius;        // endpoint-to-endpoint snap
            public float mergeEps;          // point merge epsilon (dedupe)
            public float intersectionEps;   // skip near existing vertices
            public bool flattenY;           // do logic in XZ
        }
    
        /// <summary>
        /// Connects all centerlines in-place and outputs intersection points (world).
        /// </summary>
        public static void ConnectAll(List<List<Vector3>> centerlines, Settings s, out List<Vector3> intersections)
        {
            intersections = new List<Vector3>();
            if (centerlines == null || centerlines.Count < 2) return;
    
            // 1) snap endpoints
            for (int i = 0; i < centerlines.Count; i++)
            {
                var a = centerlines[i];
                if (!Valid(a)) continue;
    
                SnapEndpoint(a, 0, centerlines, i, s);
                SnapEndpoint(a, a.Count - 1, centerlines, i, s);
                DedupConsecutive(a, s.mergeEps);
            }
    
            // 2) insert intersections pairwise
            for (int i = 0; i < centerlines.Count; i++)
            {
                var A = centerlines[i];
                if (!Valid(A)) continue;
    
                for (int j = i + 1; j < centerlines.Count; j++)
                {
                    var B = centerlines[j];
                    if (!Valid(B)) continue;
    
                    InsertIntersections(A, B, s, intersections);
                    DedupConsecutive(A, s.mergeEps);
                    DedupConsecutive(B, s.mergeEps);
                }
            }
    
            // 3) de-dupe returned intersection points
            intersections = DedupPointsXZ(intersections, Mathf.Max(s.mergeEps, s.intersectionEps));
        }
    
        private static bool Valid(List<Vector3> p) => p != null && p.Count >= 2;
        private static Vector2 XZ(Vector3 v) => new Vector2(v.x, v.z);
    
        private static void SnapEndpoint(List<Vector3> a, int idx, List<List<Vector3>> all, int selfIndex, Settings s)
        {
            Vector3 p = a[idx];
            Vector2 pxz = XZ(p);
    
            float best = s.snapRadius;
            Vector3 bestP = p;
            bool found = false;
    
            for (int k = 0; k < all.Count; k++)
            {
                if (k == selfIndex) continue;
                var b = all[k];
                if (!Valid(b)) continue;
    
                Vector3 b0 = b[0];
                Vector3 b1 = b[b.Count - 1];
    
                float d0 = Vector2.Distance(pxz, XZ(b0));
                if (d0 < best) { best = d0; bestP = b0; found = true; }
    
                float d1 = Vector2.Distance(pxz, XZ(b1));
                if (d1 < best) { best = d1; bestP = b1; found = true; }
            }
    
            if (!found) return;
    
            // Snap XZ; keep Y so we don't mess grade lines
            a[idx] = new Vector3(bestP.x, a[idx].y, bestP.z);
        }
    
        private static void InsertIntersections(List<Vector3> A, List<Vector3> B, Settings s, List<Vector3> outHits)
        {
            var aHits = new List<(int seg, float t, Vector3 p)>();
            var bHits = new List<(int seg, float t, Vector3 p)>();
    
            for (int i = 0; i < A.Count - 1; i++)
            {
                Vector3 a0 = A[i];
                Vector3 a1 = A[i + 1];
    
                for (int j = 0; j < B.Count - 1; j++)
                {
                    Vector3 b0 = B[j];
                    Vector3 b1 = B[j + 1];
    
                    if (TryIntersectXZ(a0, a1, b0, b1, out float ta, out float tb, out Vector3 ip))
                    {
                        // skip if too close to existing vertices
                        if (IsNearAny(ip, A, s.intersectionEps) || IsNearAny(ip, B, s.intersectionEps))
                            continue;
    
                        // choose Y from A segment (surface roads later project anyway)
                        ip.y = Mathf.Lerp(a0.y, a1.y, ta);
    
                        aHits.Add((i, ta, ip));
                        bHits.Add((j, tb, ip));
                        outHits.Add(ip);
                    }
                }
            }
    
            // insert from back to front so indices remain valid
            aHits.Sort((x, y) => x.seg != y.seg ? y.seg.CompareTo(x.seg) : y.t.CompareTo(x.t));
            bHits.Sort((x, y) => x.seg != y.seg ? y.seg.CompareTo(x.seg) : y.t.CompareTo(x.t));
    
            foreach (var h in aHits) InsertOnSeg(A, h.seg, h.p, s.mergeEps);
            foreach (var h in bHits) InsertOnSeg(B, h.seg, h.p, s.mergeEps);
        }
    
        private static void InsertOnSeg(List<Vector3> pts, int segIndex, Vector3 p, float mergeEps)
        {
            if (IsNearAny(p, pts, mergeEps)) return;
            int insertAt = Mathf.Clamp(segIndex + 1, 1, pts.Count - 1);
            pts.Insert(insertAt, p);
        }
    
        private static bool IsNearAny(Vector3 p, List<Vector3> pts, float eps)
        {
            Vector2 pxz = XZ(p);
            for (int i = 0; i < pts.Count; i++)
            {
                if (Vector2.Distance(pxz, XZ(pts[i])) < eps) return true;
            }
            return false;
        }
    
        private static void DedupConsecutive(List<Vector3> pts, float eps)
        {
            if (pts == null || pts.Count < 2) return;
            for (int i = pts.Count - 2; i >= 0; i--)
            {
                if (Vector3.Distance(pts[i], pts[i + 1]) < eps)
                    pts.RemoveAt(i + 1);
            }
        }
    
        private static List<Vector3> DedupPointsXZ(List<Vector3> pts, float eps)
        {
            var result = new List<Vector3>(pts.Count);
            for (int i = 0; i < pts.Count; i++)
            {
                Vector2 a = XZ(pts[i]);
                bool exists = false;
                for (int k = 0; k < result.Count; k++)
                {
                    if (Vector2.Distance(a, XZ(result[k])) < eps) { exists = true; break; }
                }
                if (!exists) result.Add(pts[i]);
            }
            return result;
        }
    
        // 2D (XZ) segment intersection
        private static bool TryIntersectXZ(
            Vector3 a0, Vector3 a1,
            Vector3 b0, Vector3 b1,
            out float ta, out float tb, out Vector3 ip)
        {
            Vector2 p = XZ(a0);
            Vector2 r = XZ(a1) - XZ(a0);
            Vector2 q = XZ(b0);
            Vector2 s = XZ(b1) - XZ(b0);
    
            float rxs = Cross(r, s);
            ta = tb = 0f;
            ip = Vector3.zero;
    
            if (Mathf.Abs(rxs) < 1e-6f) return false; // parallel
    
            Vector2 qmp = q - p;
            ta = Cross(qmp, s) / rxs;
            tb = Cross(qmp, r) / rxs;
    
            if (ta < 0f || ta > 1f || tb < 0f || tb > 1f) return false;
    
            Vector2 hit = p + ta * r;
            ip = new Vector3(hit.x, a0.y, hit.y);
            return true;
        }
    
        private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
    }
}
