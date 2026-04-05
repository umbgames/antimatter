// // =========================
// // TerrainHoleCutter.cs
// // =========================
// using System.Collections.Generic;
// using UnityEngine;

// public static class TerrainHoleCutter
// {
//     /// <summary>
//     /// Cuts a single circular hole on the Terrain holes map at the given world center.
//     /// radiusWorld is in meters (world space).
//     /// </summary>
//     public static void CutCircularHole(Terrain terrain, Vector3 worldCenter, float radiusWorld)
//     {
//         if (terrain == null || terrain.terrainData == null) return;

//         var td = terrain.terrainData;

//         int res = td.holesResolution;
//         if (res <= 0)
//         {
//             Debug.LogWarning("Terrain holes resolution is 0/invalid. Ensure Terrain holes are supported/enabled.");
//             return;
//         }

//         bool[,] holes = td.GetHoles(0, 0, res, res);

//         Vector3 tPos = terrain.transform.position;
//         Vector3 tSize = td.size;

//         radiusWorld = Mathf.Max(0.1f, radiusWorld);

//         int cx = WorldToHoleX(worldCenter.x, tPos.x, tSize.x, res);
//         int cz = WorldToHoleZ(worldCenter.z, tPos.z, tSize.z, res);

//         int radX = Mathf.CeilToInt((radiusWorld / Mathf.Max(0.001f, tSize.x)) * (res - 1));
//         int radZ = Mathf.CeilToInt((radiusWorld / Mathf.Max(0.001f, tSize.z)) * (res - 1));
//         radX = Mathf.Max(1, radX);
//         radZ = Mathf.Max(1, radZ);

//         PunchCircle(holes, res, cx, cz, radX, radZ);

//         td.SetHoles(0, 0, holes);
//         terrain.Flush();
//     }

//     /// <summary>
//     /// Utility: cut holes along a path corridor (kept for other uses).
//     /// (Not used for portal-only behavior.)
//     /// </summary>
//     public static void CutHolesAlongPath(
//         Terrain terrain,
//         List<Vector3> worldPath,
//         float radiusWorld,
//         float stepWorld = 2.0f)
//     {
//         if (terrain == null || terrain.terrainData == null) return;
//         if (worldPath == null || worldPath.Count < 2) return;

//         var td = terrain.terrainData;

//         int res = td.holesResolution;
//         if (res <= 0)
//         {
//             Debug.LogWarning("Terrain holes resolution is 0/invalid. Ensure Terrain holes are supported/enabled.");
//             return;
//         }

//         bool[,] holes = td.GetHoles(0, 0, res, res);

//         Vector3 tPos = terrain.transform.position;
//         Vector3 tSize = td.size;

//         radiusWorld = Mathf.Max(0.1f, radiusWorld);
//         stepWorld = Mathf.Max(0.25f, stepWorld);

//         int radX = Mathf.CeilToInt((radiusWorld / Mathf.Max(0.001f, tSize.x)) * (res - 1));
//         int radZ = Mathf.CeilToInt((radiusWorld / Mathf.Max(0.001f, tSize.z)) * (res - 1));
//         radX = Mathf.Max(1, radX);
//         radZ = Mathf.Max(1, radZ);

//         for (int i = 0; i < worldPath.Count - 1; i++)
//         {
//             Vector3 a = worldPath[i];
//             Vector3 b = worldPath[i + 1];

//             float segLen = Vector3.Distance(a, b);
//             int steps = Mathf.Max(1, Mathf.CeilToInt(segLen / stepWorld));

//             for (int s = 0; s <= steps; s++)
//             {
//                 float t = s / (float)steps;
//                 Vector3 p = Vector3.Lerp(a, b, t);

//                 int cx = WorldToHoleX(p.x, tPos.x, tSize.x, res);
//                 int cz = WorldToHoleZ(p.z, tPos.z, tSize.z, res);

//                 PunchCircle(holes, res, cx, cz, radX, radZ);
//             }
//         }

//         td.SetHoles(0, 0, holes);
//         terrain.Flush();
//     }

//     private static int WorldToHoleX(float x, float tx, float sx, int res)
//     {
//         float u = (x - tx) / Mathf.Max(0.001f, sx);
//         return Mathf.RoundToInt(u * (res - 1));
//     }

//     private static int WorldToHoleZ(float z, float tz, float sz, int res)
//     {
//         float v = (z - tz) / Mathf.Max(0.001f, sz);
//         return Mathf.RoundToInt(v * (res - 1));
//     }

//     private static void PunchCircle(bool[,] holes, int res, int cx, int cz, int radX, int radZ)
//     {
//         int x0 = Mathf.Clamp(cx - radX, 0, res - 1);
//         int x1 = Mathf.Clamp(cx + radX, 0, res - 1);
//         int z0 = Mathf.Clamp(cz - radZ, 0, res - 1);
//         int z1 = Mathf.Clamp(cz + radZ, 0, res - 1);

//         float rx2 = radX * radX;
//         float rz2 = radZ * radZ;

//         for (int z = z0; z <= z1; z++)
//         {
//             int dz = z - cz;
//             for (int x = x0; x <= x1; x++)
//             {
//                 int dx = x - cx;

//                 float e = (dx * dx) / Mathf.Max(1f, rx2) + (dz * dz) / Mathf.Max(1f, rz2);
//                 if (e <= 1f)
//                     holes[z, x] = false; // false = hole
//             }
//         }
//     }
// }
