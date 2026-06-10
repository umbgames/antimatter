using System;
using UnityEngine;

namespace Antimatter
{
    
    public sealed class TerrainCostGrid
    {
        public readonly int N;
        public readonly float[,] cost;      // combined cost
        public readonly float[,] height01;  // normalized height [0..1]
        public readonly float[,] slopeDeg;  // slope degrees
        public readonly bool[,] blockedSurface;
    
        private readonly Terrain terrain;
        private readonly TerrainData data;
        private readonly Vector3 tPos;
        private readonly Vector3 tSize;
    
        public Terrain Terrain => terrain;
    
        public TerrainCostGrid(Terrain t, int resolution)
        {
            terrain = t;
            data = t.terrainData;
            tPos = t.GetPosition();
            tSize = data.size;
    
            N = Mathf.Max(16, resolution);
            cost = new float[N, N];
            height01 = new float[N, N];
            slopeDeg = new float[N, N];
            blockedSurface = new bool[N, N];
        }
    
        public void Build(RoadGenSettings s)
        {
            // Pre-sample height + slope
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    Vector3 w = GridToWorld(x, y, 0f);
    
                    float h = terrain.SampleHeight(w) + tPos.y;
                    float hNorm = Mathf.InverseLerp(tPos.y, tPos.y + tSize.y, h);
    
                    // Approx slope using TerrainData.GetInterpolatedNormal
                    float u = (w.x - tPos.x) / tSize.x;
                    float v = (w.z - tPos.z) / tSize.z;
                    Vector3 n = data.GetInterpolatedNormal(u, v);
                    float slope = Vector3.Angle(n, Vector3.up);
    
                    height01[x, y] = hNorm;
                    slopeDeg[x, y] = slope;
                }
            }
    
            // Compute valley bonus: compare with neighbor average height
            for (int y = 0; y < N; y++)
            {
                for (int x = 0; x < N; x++)
                {
                    float h = height01[x, y];
                    float nbAvg = NeighborAvgHeight01(x, y);
    
                    float valley = Mathf.Clamp01((nbAvg - h) * 4f); // scale factor
    
                    // Slope penalties
                    float slope = slopeDeg[x, y];
                    bool hardBlock = slope >= s.hardBlockSlopeDeg;
    
                    blockedSurface[x, y] = hardBlock;
    
                    float slopeSoft = Mathf.Max(0f, slope - s.maxSurfaceSlopeDeg);
                    float slopePenalty = Mathf.Pow(slopeSoft / Mathf.Max(0.01f, (s.hardBlockSlopeDeg - s.maxSurfaceSlopeDeg)), 3f) * s.slopePenalty;
    
                    // Height penalty (prefer low)
                    float heightPenalty = h * s.heightPenalty;
    
                    // Combined: base 1 + penalties - bonus
                    float c = 1f + slopePenalty + heightPenalty - (valley * s.valleyBonus);
    
                    // Clamp
                    cost[x, y] = Mathf.Max(0.01f, c);
                }
            }
        }
    
        private float NeighborAvgHeight01(int x, int y)
        {
            int count = 0;
            float sum = 0f;
    
            for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                if (ox == 0 && oy == 0) continue;
                int nx = x + ox;
                int ny = y + oy;
                if (nx < 0 || ny < 0 || nx >= N || ny >= N) continue;
                sum += height01[nx, ny];
                count++;
            }
    
            return count > 0 ? (sum / count) : height01[x, y];
        }
    
        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < N && y < N;
    
        public Vector3 GridToWorld(int gx, int gy, float yOffset)
        {
            // map grid to terrain XZ
            float fx = (gx + 0.5f) / N;
            float fz = (gy + 0.5f) / N;
            float wx = tPos.x + fx * tSize.x;
            float wz = tPos.z + fz * tSize.z;
            float wy = terrain.SampleHeight(new Vector3(wx, 0f, wz)) + tPos.y + yOffset;
            return new Vector3(wx, wy, wz);
        }
    
        public Vector2Int WorldToGrid(Vector3 world)
        {
            float u = Mathf.InverseLerp(tPos.x, tPos.x + tSize.x, world.x);
            float v = Mathf.InverseLerp(tPos.z, tPos.z + tSize.z, world.z);
            int gx = Mathf.Clamp(Mathf.FloorToInt(u * N), 0, N - 1);
            int gy = Mathf.Clamp(Mathf.FloorToInt(v * N), 0, N - 1);
            return new Vector2Int(gx, gy);
        }
    
        public float HeightAtWorld(Vector3 world)
        {
            return terrain.SampleHeight(world) + tPos.y;
        }
    
        public float SlopeAtWorld(Vector3 world)
        {
            float u = (world.x - tPos.x) / tSize.x;
            float v = (world.z - tPos.z) / tSize.z;
            u = Mathf.Clamp01(u);
            v = Mathf.Clamp01(v);
            Vector3 n = data.GetInterpolatedNormal(u, v);
            return Vector3.Angle(n, Vector3.up);
        }
    
        public bool IsInsideTerrainXZ(Vector3 w)
        {
            return w.x >= tPos.x && w.x <= tPos.x + tSize.x &&
                   w.z >= tPos.z && w.z <= tPos.z + tSize.z;
        }
    }
}
