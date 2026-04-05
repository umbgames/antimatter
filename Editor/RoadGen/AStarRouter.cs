using System;
using System.Collections.Generic;
using UnityEngine;

public static class AStarRouter
{
    private struct Node
    {
        public float g;
        public float f;
        public int cameFrom;   // packed index
        public byte cameDir;   // direction index 0..7, 255 = none
        public bool closed;
        public bool opened;
    }

    private static readonly Vector2Int[] Neigh8 =
    {
        new Vector2Int( 1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int( 0, 1),
        new Vector2Int( 0,-1),
        new Vector2Int( 1, 1),
        new Vector2Int( 1,-1),
        new Vector2Int(-1, 1),
        new Vector2Int(-1,-1),
    };

    private static float Heuristic(Vector2Int a, Vector2Int b)
    {
        // Octile distance
        int dx = Mathf.Abs(a.x - b.x);
        int dy = Mathf.Abs(a.y - b.y);
        int min = Mathf.Min(dx, dy);
        int max = Mathf.Max(dx, dy);
        return (min * 1.41421356f) + (max - min);
    }

    private static int Pack(int x, int y, int N) => y * N + x;
    private static Vector2Int Unpack(int idx, int N) => new Vector2Int(idx % N, idx / N);

    /// <summary>
    /// Finds a path on the cost grid. If allowTunnel==false, cells with blockedSurface are treated as blocked.
    /// If allowTunnel==true, blockedSurface is allowed but with extra penalty.
    /// </summary>
    public static bool FindPath(
        TerrainCostGrid grid,
        RoadGenSettings s,
        Vector2Int start,
        Vector2Int goal,
        bool allowTunnel,
        out List<Vector2Int> path)
    {
        path = null;

        int N = grid.N;
        if (!grid.InBounds(start.x, start.y) || !grid.InBounds(goal.x, goal.y))
            return false;

        int total = N * N;
        Node[] nodes = new Node[total];

        int sIdx = Pack(start.x, start.y, N);
        int gIdx = Pack(goal.x, goal.y, N);

        // simple binary heap priority queue
        var open = new MinHeap(total);

        nodes[sIdx].g = 0f;
        nodes[sIdx].f = Heuristic(start, goal);
        nodes[sIdx].cameFrom = -1;
        nodes[sIdx].cameDir = 255;
        nodes[sIdx].opened = true;
        open.Push(sIdx, nodes[sIdx].f);

        while (open.Count > 0)
        {
            int curIdx = open.PopMin();
            ref Node cur = ref nodes[curIdx];
            if (cur.closed) continue;
            cur.closed = true;

            if (curIdx == gIdx)
            {
                path = Reconstruct(nodes, gIdx, N);
                return path != null && path.Count >= 2;
            }

            Vector2Int c = Unpack(curIdx, N);

            for (byte dir = 0; dir < 8; dir++)
            {
                Vector2Int d = Neigh8[dir];
                Vector2Int npos = new Vector2Int(c.x + d.x, c.y + d.y);
                if (!grid.InBounds(npos.x, npos.y)) continue;

                int nIdx = Pack(npos.x, npos.y, N);
                ref Node nn = ref nodes[nIdx];
                if (nn.closed) continue;

                bool blocked = grid.blockedSurface[npos.x, npos.y];
                if (!allowTunnel && blocked) continue;

                float stepDist = (d.x == 0 || d.y == 0) ? 1f : 1.41421356f;

                // Base cell cost
                float cellCost = grid.cost[npos.x, npos.y];

                // If tunneling, add extra cost for "digging"
                if (allowTunnel && blocked)
                    cellCost *= 2.8f;

                // Turn penalty
                float turnCost = 0f;
                if (cur.cameDir != 255 && cur.cameDir != dir)
                    turnCost = s.turnPenalty;

                float ng = cur.g + (cellCost * stepDist) + turnCost;

                if (!nn.opened || ng < nn.g)
                {
                    nn.g = ng;
                    nn.f = ng + Heuristic(npos, goal);
                    nn.cameFrom = curIdx;
                    nn.cameDir = dir;
                    nn.opened = true;
                    open.Push(nIdx, nn.f);
                }
            }
        }

        return false;
    }

    private static List<Vector2Int> Reconstruct(Node[] nodes, int goalIdx, int N)
    {
        var rev = new List<Vector2Int>(256);
        int cur = goalIdx;

        int guard = 0;
        while (cur >= 0 && guard++ < N * N)
        {
            rev.Add(Unpack(cur, N));
            cur = nodes[cur].cameFrom;
        }

        if (rev.Count < 2) return null;
        rev.Reverse();
        return rev;
    }

    // Minimal binary heap (index, key)
    private sealed class MinHeap
    {
        private int[] items;
        private float[] keys;
        public int Count { get; private set; }

        public MinHeap(int capacity)
        {
            items = new int[Mathf.Max(16, capacity)];
            keys = new float[items.Length];
            Count = 0;
        }

        public void Push(int item, float key)
        {
            int i = Count++;
            if (i >= items.Length)
            {
                Array.Resize(ref items, items.Length * 2);
                Array.Resize(ref keys, keys.Length * 2);
            }

            items[i] = item;
            keys[i] = key;
            SiftUp(i);
        }

        public int PopMin()
        {
            int min = items[0];
            Count--;
            items[0] = items[Count];
            keys[0] = keys[Count];
            SiftDown(0);
            return min;
        }

        private void SiftUp(int i)
        {
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (keys[p] <= keys[i]) break;
                Swap(i, p);
                i = p;
            }
        }

        private void SiftDown(int i)
        {
            while (true)
            {
                int l = (i << 1) + 1;
                int r = l + 1;
                if (l >= Count) break;

                int m = (r < Count && keys[r] < keys[l]) ? r : l;
                if (keys[i] <= keys[m]) break;

                Swap(i, m);
                i = m;
            }
        }

        private void Swap(int a, int b)
        {
            (items[a], items[b]) = (items[b], items[a]);
            (keys[a], keys[b]) = (keys[b], keys[a]);
        }
    }
}
