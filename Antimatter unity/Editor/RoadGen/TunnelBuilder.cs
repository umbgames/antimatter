using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Antimatter
{
    
    public static class TunnelBuilder
    {
        public static GameObject BuildTunnelWithRoad(
            Scene scene,
            string name,
            List<Vector3> centerline,
            float innerRadius,
            float wallThickness,
            float portalLength,
            float roadHalfWidth,
            Material roadMaterial = null,
            bool addRoadCollider = true,
            Terrain terrainForClamping = null,
    
            // quality knobs
            Material tunnelMaterial = null,
            int radialSegments = 24,
            bool buildOuterShell = true,
            bool addTunnelCollider = true,
            bool capPortals = true,
    
            // URP helpers
            bool forceTwoSidedURP = true,
            float uvVScale = 0.15f,
    
            // entrance structure knobs
            bool buildPortalCollars = true,
            float portalCollarThicknessMultiplier = 1.6f,  // thicker than wallThickness
            float portalCollarLengthMultiplier = 0.35f     // collar length relative to portalLength
        )
        {
            if (centerline == null || centerline.Count < 2) return null;
            radialSegments = Mathf.Max(6, radialSegments);
    
            GameObject root = GameObject.Find(name);
            if (root == null) root = new GameObject(name);
            SceneManager.MoveGameObjectToScene(root, scene);
    
            // Clear previous children (Editor-time)
            for (int i = root.transform.childCount - 1; i >= 0; i--)
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
    
            var shellRoot = new GameObject("Shell");
            var roadRoot  = new GameObject("Road");
            shellRoot.transform.SetParent(root.transform, false);
            roadRoot.transform.SetParent(root.transform, false);
    
            float outerRadius = innerRadius + wallThickness;
    
            // ✅ Use portalLength to extend the tunnel so entrances exist
            List<Vector3> tunnelPath = ExtendPathForPortals(centerline, portalLength);
    
            // ✅ HOLLOW tunnel tube mesh
            var tunnelGO = BuildTubeMeshURP(
                name: "TunnelShell",
                parent: shellRoot.transform,
                path: tunnelPath,
                innerR: innerRadius,
                outerR: buildOuterShell ? outerRadius : 0f,
                radialSegments: radialSegments,
                capEnds: capPortals,
                mat: tunnelMaterial,
                addCollider: addTunnelCollider,
                forceTwoSidedURP: forceTwoSidedURP,
                uvVScale: uvVScale
            );
    
            // ✅ Portal "entrance structure" collars (thicker ring segments near ends)
            if (buildPortalCollars && buildOuterShell && portalLength > 0.01f)
            {
                float collarOuter = innerRadius + wallThickness * portalCollarThicknessMultiplier;
                float collarLen   = Mathf.Max(0.2f, portalLength * portalCollarLengthMultiplier);
    
                BuildPortalCollar(
                    parent: shellRoot.transform,
                    name: "PortalCollar_Start",
                    centerline: centerline,
                    atStart: true,
                    collarLength: collarLen,
                    innerR: innerRadius,
                    outerR: collarOuter,
                    radialSegments: radialSegments,
                    mat: tunnelMaterial,
                    forceTwoSidedURP: forceTwoSidedURP,
                    uvVScale: uvVScale
                );
    
                BuildPortalCollar(
                    parent: shellRoot.transform,
                    name: "PortalCollar_End",
                    centerline: centerline,
                    atStart: false,
                    collarLength: collarLen,
                    innerR: innerRadius,
                    outerR: collarOuter,
                    radialSegments: radialSegments,
                    mat: tunnelMaterial,
                    forceTwoSidedURP: forceTwoSidedURP,
                    uvVScale: uvVScale
                );
            }
    
            // ✅ Roadbed inside tunnel (your existing logic)
            float roadDrop = Mathf.Max(0.5f, innerRadius * 0.65f);
    
            var roadPath = new List<Vector3>(centerline.Count);
            for (int i = 0; i < centerline.Count; i++)
            {
                Vector3 p = centerline[i];
                float y = p.y - roadDrop;
    
                if (terrainForClamping != null && terrainForClamping.terrainData != null)
                {
                    float terrainY = terrainForClamping.SampleHeight(p) + terrainForClamping.transform.position.y;
                    float minBelow = 0.5f;
                    y = Mathf.Min(y, terrainY - minBelow);
                }
    
                roadPath.Add(new Vector3(p.x, y, p.z));
            }
    
            var roadGO = RoadMeshBuilder.BuildOrUpdateRoad(
                scene,
                terrain: null,
                name: name + "_Roadbed",
                path: roadPath,
                halfWidth: roadHalfWidth,
                yOffset: 0f,
                mat: roadMaterial,
                mode: RoadMeshBuilder.RoadConformMode.TunnelNoProject,
                addCollider: addRoadCollider);
    
            if (roadGO != null)
                roadGO.transform.SetParent(roadRoot.transform, true);
    
            return root;
        }
    
        // ----------------------------
        // Portal path extension
        // ----------------------------
        private static List<Vector3> ExtendPathForPortals(List<Vector3> centerline, float portalLength)
        {
            if (portalLength <= 0.001f) return new List<Vector3>(centerline);
    
            Vector3 p0 = centerline[0];
            Vector3 p1 = centerline[1];
            Vector3 pn = centerline[centerline.Count - 1];
            Vector3 pn1 = centerline[centerline.Count - 2];
    
            Vector3 fStart = (p1 - p0).normalized;
            Vector3 fEnd   = (pn - pn1).normalized;
    
            // Extend outward
            Vector3 startExt = p0 - fStart * portalLength;
            Vector3 endExt   = pn + fEnd   * portalLength;
    
            var extended = new List<Vector3>(centerline.Count + 2);
            extended.Add(startExt);
            extended.AddRange(centerline);
            extended.Add(endExt);
            return extended;
        }
    
        // ----------------------------
        // Portal collar builder
        // ----------------------------
        private static void BuildPortalCollar(
            Transform parent,
            string name,
            List<Vector3> centerline,
            bool atStart,
            float collarLength,
            float innerR,
            float outerR,
            int radialSegments,
            Material mat,
            bool forceTwoSidedURP,
            float uvVScale
        )
        {
            if (centerline == null || centerline.Count < 2) return;
    
            Vector3 a, b, fwd;
            if (atStart)
            {
                a = centerline[0];
                b = centerline[1];
                fwd = (b - a).normalized;
                // collar sits just inside/outside the entrance region
                var p0 = a - fwd * (collarLength * 0.5f);
                var p1 = a + fwd * (collarLength * 0.5f);
                BuildTubeMeshURP(
                    name: name,
                    parent: parent,
                    path: new List<Vector3> { p0, p1 },
                    innerR: innerR,
                    outerR: outerR,
                    radialSegments: radialSegments,
                    capEnds: true,     // collar is a "solid ring segment"
                    mat: mat,
                    addCollider: false,
                    forceTwoSidedURP: forceTwoSidedURP,
                    uvVScale: uvVScale
                );
            }
            else
            {
                a = centerline[centerline.Count - 2];
                b = centerline[centerline.Count - 1];
                fwd = (b - a).normalized;
    
                var p0 = b - fwd * (collarLength * 0.5f);
                var p1 = b + fwd * (collarLength * 0.5f);
                BuildTubeMeshURP(
                    name: name,
                    parent: parent,
                    path: new List<Vector3> { p0, p1 },
                    innerR: innerR,
                    outerR: outerR,
                    radialSegments: radialSegments,
                    capEnds: true,
                    mat: mat,
                    addCollider: false,
                    forceTwoSidedURP: forceTwoSidedURP,
                    uvVScale: uvVScale
                );
            }
        }
    
        // ----------------------------
        // Tube mesh builder (URP-friendly, hollow)
        // ----------------------------
        private static GameObject BuildTubeMeshURP(
            string name,
            Transform parent,
            List<Vector3> path,
            float innerR,
            float outerR,
            int radialSegments,
            bool capEnds,
            Material mat,
            bool addCollider,
            bool forceTwoSidedURP,
            float uvVScale
        )
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
    
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
    
            if (mat != null)
            {
                mr.sharedMaterial = mat;
    
                // URP: force double-sided if shader supports _Cull
                // _Cull: 0=Off, 1=Front, 2=Back
                if (forceTwoSidedURP && mr.sharedMaterial.HasProperty("_Cull"))
                    mr.sharedMaterial.SetFloat("_Cull", 0f);
            }
    
            Mesh mesh = new Mesh { name = name + "_Mesh" };
    
            var frames = BuildFrames(path);
    
            int n = path.Count;
            int ring = Mathf.Max(6, radialSegments);
    
            int innerBase = 0;
            int outerBase = n * ring; // only valid if outerR > 0
    
            int vertCount = n * ring * (outerR > 0f ? 2 : 1);
    
            var verts   = new Vector3[vertCount];
            var normals = new Vector3[vertCount];
            var uvs     = new Vector2[vertCount];
    
            float[] dist = new float[n];
            dist[0] = 0f;
            for (int i = 1; i < n; i++)
                dist[i] = dist[i - 1] + Vector3.Distance(path[i - 1], path[i]);
    
            for (int i = 0; i < n; i++)
            {
                Vector3 center = path[i];
                Vector3 right  = frames[i].right;
                Vector3 up     = frames[i].up;
    
                for (int s = 0; s < ring; s++)
                {
                    float t = (float)s / ring;
                    float ang = t * Mathf.PI * 2f;
    
                    Vector3 radial = Mathf.Cos(ang) * right + Mathf.Sin(ang) * up;
    
                    // Inner surface (visible from inside)
                    int vi = innerBase + i * ring + s;
                    verts[vi]   = center + radial * innerR;
                    normals[vi] = -radial;
                    uvs[vi]     = new Vector2(t, dist[i] * uvVScale);
    
                    if (outerR > 0f)
                    {
                        int vo = outerBase + i * ring + s;
                        verts[vo]   = center + radial * outerR;
                        normals[vo] = radial;
                        uvs[vo]     = new Vector2(t, dist[i] * uvVScale);
                    }
                }
            }
    
            var tris = new List<int>((n - 1) * ring * 6 * (outerR > 0f ? 2 : 1) + (capEnds ? ring * 6 * 2 : 0));
    
            // INNER surface
            for (int i = 0; i < n - 1; i++)
            {
                int a0 = innerBase + i * ring;
                int b0 = innerBase + (i + 1) * ring;
    
                for (int s = 0; s < ring; s++)
                {
                    int s1 = (s + 1) % ring;
    
                    int a  = a0 + s;
                    int aN = a0 + s1;
                    int b  = b0 + s;
                    int bN = b0 + s1;
    
                    tris.Add(a);
                    tris.Add(bN);
                    tris.Add(b);
    
                    tris.Add(a);
                    tris.Add(aN);
                    tris.Add(bN);
                }
            }
    
            // OUTER surface
            if (outerR > 0f)
            {
                for (int i = 0; i < n - 1; i++)
                {
                    int a0 = outerBase + i * ring;
                    int b0 = outerBase + (i + 1) * ring;
    
                    for (int s = 0; s < ring; s++)
                    {
                        int s1 = (s + 1) % ring;
    
                        int a  = a0 + s;
                        int aN = a0 + s1;
                        int b  = b0 + s;
                        int bN = b0 + s1;
    
                        tris.Add(a);
                        tris.Add(b);
                        tris.Add(bN);
    
                        tris.Add(a);
                        tris.Add(bN);
                        tris.Add(aN);
                    }
                }
            }
    
            // Caps (only meaningful when there is thickness)
            if (capEnds && outerR > 0f)
            {
                AddRingCap(tris, innerBase, outerBase, ring, ringIndex: 0,     flip: false);
                AddRingCap(tris, innerBase, outerBase, ring, ringIndex: n - 1, flip: true);
            }
    
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
    
            mf.sharedMesh = mesh;
    
            if (addCollider)
            {
                var mc = go.AddComponent<MeshCollider>();
                mc.sharedMesh = mesh;
                mc.convex = false;
            }
    
            return go;
        }
    
        private static void AddRingCap(List<int> tris, int innerBase, int outerBase, int ring, int ringIndex, bool flip)
        {
            int inner0 = innerBase + ringIndex * ring;
            int outer0 = outerBase + ringIndex * ring;
    
            for (int s = 0; s < ring; s++)
            {
                int s1 = (s + 1) % ring;
    
                int i0 = inner0 + s;
                int i1 = inner0 + s1;
                int o0 = outer0 + s;
                int o1 = outer0 + s1;
    
                if (!flip)
                {
                    tris.Add(i0); tris.Add(o1); tris.Add(o0);
                    tris.Add(i0); tris.Add(i1); tris.Add(o1);
                }
                else
                {
                    tris.Add(i0); tris.Add(o0); tris.Add(o1);
                    tris.Add(i0); tris.Add(o1); tris.Add(i1);
                }
            }
        }
    
        private static (Vector3 forward, Vector3 right, Vector3 up)[] BuildFrames(List<Vector3> path)
        {
            int n = path.Count;
            var frames = new (Vector3 forward, Vector3 right, Vector3 up)[n];
    
            Vector3 upRef = Vector3.up;
    
            for (int i = 0; i < n; i++)
            {
                Vector3 fwd = (i == n - 1)
                    ? (path[i] - path[i - 1]).normalized
                    : (path[i + 1] - path[i]).normalized;
    
                if (Mathf.Abs(Vector3.Dot(fwd, upRef)) > 0.95f)
                    upRef = Vector3.right;
    
                Vector3 right = Vector3.Cross(upRef, fwd).normalized;
                Vector3 up    = Vector3.Cross(fwd, right).normalized;
    
                upRef = Vector3.Slerp(upRef, up, 0.25f).normalized;
                frames[i] = (fwd, right, up);
            }
    
            return frames;
        }
    }
}
