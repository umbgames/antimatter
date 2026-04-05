using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class CityAutoArranger
{
    // If your project uses different naming, tweak these tokens.
    private static readonly string[] RoadTokens = { "road", "street", "avenue", "lane", "highway", "path" };
    private static readonly string[] BuildingTokens = { "building", "house", "home", "tower", "shop", "store", "mall", "office", "block" };
    private static readonly string[] SidewalkTokens = { "sidewalk", "pavement", "walkway", "footpath" };

    // Main entry
    public static void Arrange(List<ObjectDefinition> objects)
    {
        if (objects == null || objects.Count == 0) return;

        // ---- classify ----
        var roads = new List<ObjectDefinition>();
        var buildings = new List<ObjectDefinition>();
        var sidewalks = new List<ObjectDefinition>();
        var props = new List<ObjectDefinition>();

        foreach (var o in objects)
        {
            string id = (o?.id ?? "").ToLowerInvariant();

            if (ContainsAny(id, SidewalkTokens))
                sidewalks.Add(o);
            else if (ContainsAny(id, RoadTokens))
                roads.Add(o);
            else if (ContainsAny(id, BuildingTokens))
                buildings.Add(o);
            else
                props.Add(o);
        }

        // Fallback: if nothing labeled, treat "bigger footprint" as building.
        if (roads.Count == 0 && buildings.Count == 0)
        {
            foreach (var o in props.ToList())
            {
                var sc = SafeScale(o);
                if (!IsZeroOrInvalid(sc) && sc.x >= 1.5f && sc.z >= 1.5f)
                {
                    buildings.Add(o);
                    props.Remove(o);
                }
            }
        }

        // If still nothing usable, bail.
        if (roads.Count == 0 && buildings.Count == 0 && props.Count == 0) return;

        // ---- scale parameters (rough real-world ratios, Unity units ~ meters) ----
        // 2 lanes x 3.5m ~ 7m road. Sidewalks often 1.2m min, 2.5m is comfortable.
        float roadWidth = 7.0f;
        float sidewalkWidth = 2.5f;
        float sidewalkThickness = 0.12f;
        float roadThickness = 0.18f;

        // Distance between road centerlines (defines block size).
        // Keep it "city-block-ish" but not massive for Unity scenes.
        float blockSize = 60.0f; // tweak if you want bigger cities

        // Lot safety margins
        float lotMargin = 2.0f; // inside the cell, away from sidewalks
        float minGap = 1.5f;    // between building footprints

        // Decide grid from building count
        int buildingCount = buildings.Count;
        int cellsPerSide = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(Mathf.Max(buildingCount, 4))), 2, 10);
        int roadLinesPerSide = cellsPerSide + 1;

        float span = cellsPerSide * blockSize;
        float halfSpan = span * 0.5f;

        // Precompute road line coordinates
        var xLines = new float[roadLinesPerSide];
        var zLines = new float[roadLinesPerSide];
        for (int i = 0; i < roadLinesPerSide; i++)
        {
            float t = (roadLinesPerSide == 1) ? 0f : i / (float)(roadLinesPerSide - 1);
            xLines[i] = Mathf.Lerp(-halfSpan, halfSpan, t);
            zLines[i] = Mathf.Lerp(-halfSpan, halfSpan, t);
        }

        // ---- 1) place roads (use what exists) ----
        // Need vertical + horizontal lines:
        int neededRoadObjects = roadLinesPerSide * 2;
        int usableRoadObjects = Mathf.Min(roads.Count, neededRoadObjects);

        for (int i = 0; i < usableRoadObjects; i++)
        {
            bool vertical = i < roadLinesPerSide;
            int lineIndex = vertical ? i : (i - roadLinesPerSide);

            var r = roads[i];
            EnsurePrimitiveCube(r);

            float coord = vertical ? xLines[lineIndex] : zLines[lineIndex];

            r.position = vertical ? new Vector3(coord, 0f, 0f) : new Vector3(0f, 0f, coord);
            r.scale = vertical
                ? new Vector3(roadWidth, roadThickness, span)
                : new Vector3(span, roadThickness, roadWidth);

            // Keep flat yaw
            SetRotationYIfPossible(r, 0f);
        }

        // ---- 2) ensure sidewalks exist (append if missing/insufficient) ----
        // We create 2 sidewalks per road line (left/right of each road strip).
        // Total needed = 2*(vertical lines + horizontal lines) = 4*roadLinesPerSide
        int neededSidewalks = 4 * roadLinesPerSide;

        // If the model already made some sidewalks, we’ll reuse them, then create the rest.
        if (sidewalks.Count < neededSidewalks)
        {
            int toCreate = neededSidewalks - sidewalks.Count;
            for (int i = 0; i < toCreate; i++)
            {
                var sw = new ObjectDefinition();
                sw.id = $"sidewalk_auto_{i}";
                sw.kind = "Primitive";
                sw.primitiveType = "Cube";
                sw.position = Vector3.zero;
                sw.scale = Vector3.one;
                sidewalks.Add(sw);
                objects.Add(sw);
            }
        }

        // Place sidewalks deterministically around each road line
        int swIndex = 0;

        // Vertical roads: sidewalks offset in X
        for (int i = 0; i < roadLinesPerSide; i++)
        {
            float x = xLines[i];
            float offset = (roadWidth * 0.5f) + (sidewalkWidth * 0.5f);

            // Left sidewalk
            {
                var sw = sidewalks[swIndex++];
                EnsurePrimitiveCube(sw);
                sw.position = new Vector3(x - offset, 0f, 0f);
                sw.scale = new Vector3(sidewalkWidth, sidewalkThickness, span);
                SetRotationYIfPossible(sw, 0f);
            }

            // Right sidewalk
            {
                var sw = sidewalks[swIndex++];
                EnsurePrimitiveCube(sw);
                sw.position = new Vector3(x + offset, 0f, 0f);
                sw.scale = new Vector3(sidewalkWidth, sidewalkThickness, span);
                SetRotationYIfPossible(sw, 0f);
            }
        }

        // Horizontal roads: sidewalks offset in Z
        for (int i = 0; i < roadLinesPerSide; i++)
        {
            float z = zLines[i];
            float offset = (roadWidth * 0.5f) + (sidewalkWidth * 0.5f);

            // Bottom sidewalk
            {
                var sw = sidewalks[swIndex++];
                EnsurePrimitiveCube(sw);
                sw.position = new Vector3(0f, 0f, z - offset);
                sw.scale = new Vector3(span, sidewalkThickness, sidewalkWidth);
                SetRotationYIfPossible(sw, 0f);
            }

            // Top sidewalk
            {
                var sw = sidewalks[swIndex++];
                EnsurePrimitiveCube(sw);
                sw.position = new Vector3(0f, 0f, z + offset);
                sw.scale = new Vector3(span, sidewalkThickness, sidewalkWidth);
                SetRotationYIfPossible(sw, 0f);
            }
        }

        // ---- 3) place buildings into lots (cells between roads, away from sidewalks) ----
        var occupied = new List<Rect>();

        // Reserve road + sidewalk strips as "blocked" areas (so buildings never sit on them)
        ReserveRoadAndSidewalkAreas(occupied, xLines, zLines, span, roadWidth, sidewalkWidth);

        int b = 0;
        for (int cx = 0; cx < cellsPerSide && b < buildings.Count; cx++)
        {
            for (int cz = 0; cz < cellsPerSide && b < buildings.Count; cz++)
            {
                // Cell bounded by consecutive road centerlines
                float x0 = xLines[cx];
                float x1 = xLines[cx + 1];
                float z0 = zLines[cz];
                float z1 = zLines[cz + 1];

                // Lot shrunk away from sidewalks + margins
                float shrink = (roadWidth * 0.5f) + sidewalkWidth + lotMargin;

                Rect lot = Rect.MinMaxRect(
                    Mathf.Min(x0, x1) + shrink,
                    Mathf.Min(z0, z1) + shrink,
                    Mathf.Max(x0, x1) - shrink,
                    Mathf.Max(z0, z1) - shrink
                );

                if (lot.width < 6f || lot.height < 6f) continue;

                var o = buildings[b];
                EnsurePrimitiveCube(o);

                // --- scale buildings to lot (so they don't look tiny) ---
                // Fill 40%–65% of lot footprint (deterministic per id)
                float fill = Mathf.Lerp(0.40f, 0.65f, Hash01(o.id, 777));
                float targetX = lot.width * fill;
                float targetZ = lot.height * fill;

                // Deterministic aspect variety (wide vs deep)
                float aspect = Mathf.Lerp(0.75f, 1.35f, Hash01(o.id, 779));
                targetX *= aspect;
                targetZ /= aspect;

                // Clamp inside lot
                targetX = Mathf.Clamp(targetX, 6f, lot.width - minGap);
                targetZ = Mathf.Clamp(targetZ, 6f, lot.height - minGap);

                // Height: depends on footprint + deterministic height band
                // Towers can be taller (if id suggests tower/office)
                bool isTower = ContainsAny((o.id ?? "").ToLowerInvariant(), "tower", "office", "landmark");
                float baseH = isTower ? Mathf.Lerp(22f, 40f, Hash01(o.id, 901)) : Mathf.Lerp(10f, 22f, Hash01(o.id, 903));
                float height = baseH + (targetX + targetZ) * (isTower ? 0.25f : 0.15f);

                // Apply scale (override tiny or missing)
                Vector3 sc = SafeScale(o);
                if (IsZeroOrInvalid(sc) || sc.x < targetX * 0.7f || sc.z < targetZ * 0.7f)
                {
                    o.scale = new Vector3(targetX, height, targetZ);
                    sc = o.scale;
                }

                // Place inside lot with deterministic non-overlap search
                Vector2 placed = FindNonOverlappingPointInLot(lot, sc.x, sc.z, occupied, o.id);

                o.position = new Vector3(placed.x, sc.y * 0.5f, placed.y);

                // Occupancy
                occupied.Add(Rect.MinMaxRect(
                    placed.x - sc.x * 0.5f - (minGap * 0.5f),
                    placed.y - sc.z * 0.5f - (minGap * 0.5f),
                    placed.x + sc.x * 0.5f + (minGap * 0.5f),
                    placed.y + sc.z * 0.5f + (minGap * 0.5f)
                ));

                // Face nearest road
                float yRot = ComputeFacingToNearestRoad(placed.x, placed.y, xLines, zLines);
                SetRotationYIfPossible(o, yRot);

                b++;
            }
        }

        // ---- 4) place props (small stuff) in lots, away from roads/sidewalks/buildings ----
        foreach (var p in props)
        {
            EnsurePrimitiveCube(p);

            Vector3 sc = SafeScale(p);
            if (IsZeroOrInvalid(sc))
            {
                float s = 0.6f + 1.2f * Hash01(p.id, 101);
                float h = 1.2f + 3.5f * Hash01(p.id, 131);
                p.scale = new Vector3(s, h, s);
                sc = p.scale;
            }

            // Deterministic cell choice
            int ix = Mathf.Clamp((int)(Hash01(p.id, 7) * cellsPerSide), 0, cellsPerSide - 1);
            int iz = Mathf.Clamp((int)(Hash01(p.id, 11) * cellsPerSide), 0, cellsPerSide - 1);

            float x0 = xLines[ix];
            float x1 = xLines[ix + 1];
            float z0 = zLines[iz];
            float z1 = zLines[iz + 1];

            float shrink = (roadWidth * 0.5f) + sidewalkWidth + lotMargin;

            Rect lot = Rect.MinMaxRect(
                Mathf.Min(x0, x1) + shrink,
                Mathf.Min(z0, z1) + shrink,
                Mathf.Max(x0, x1) - shrink,
                Mathf.Max(z0, z1) - shrink
            );
            if (lot.width < 4f || lot.height < 4f) continue;

            float fx = Mathf.Clamp(sc.x, 0.5f, lot.width - 0.5f);
            float fz = Mathf.Clamp(sc.z, 0.5f, lot.height - 0.5f);

            Vector2 placed = FindNonOverlappingPointInLot(lot, fx, fz, occupied, p.id);

            p.position = new Vector3(placed.x, sc.y * 0.5f, placed.y);
            occupied.Add(Rect.MinMaxRect(
                placed.x - fx * 0.5f - 0.25f, placed.y - fz * 0.5f - 0.25f,
                placed.x + fx * 0.5f + 0.25f, placed.y + fz * 0.5f + 0.25f
            ));

            SetRotationYIfPossible(p, Hash01(p.id, 19) * 360f);
        }
    }

    // ---------------- layout helpers ----------------

    private static void ReserveRoadAndSidewalkAreas(List<Rect> occupied, float[] xLines, float[] zLines, float span, float roadW, float sidewalkW)
    {
        float halfSpan = span * 0.5f;

        float roadHalf = roadW * 0.5f;
        float sidewalkHalf = sidewalkW * 0.5f;

        // Road strips
        for (int i = 0; i < xLines.Length; i++)
        {
            float x = xLines[i];
            occupied.Add(Rect.MinMaxRect(
                x - roadHalf, -halfSpan, x + roadHalf, halfSpan
            ));

            // Sidewalk strips on both sides (vertical)
            float off = roadHalf + sidewalkHalf;
            occupied.Add(Rect.MinMaxRect(x - off - sidewalkHalf, -halfSpan, x - off + sidewalkHalf, halfSpan));
            occupied.Add(Rect.MinMaxRect(x + off - sidewalkHalf, -halfSpan, x + off + sidewalkHalf, halfSpan));
        }

        for (int i = 0; i < zLines.Length; i++)
        {
            float z = zLines[i];
            occupied.Add(Rect.MinMaxRect(
                -halfSpan, z - roadHalf, halfSpan, z + roadHalf
            ));

            // Sidewalk strips on both sides (horizontal)
            float off = roadHalf + sidewalkHalf;
            occupied.Add(Rect.MinMaxRect(-halfSpan, z - off - sidewalkHalf, halfSpan, z - off + sidewalkHalf));
            occupied.Add(Rect.MinMaxRect(-halfSpan, z + off - sidewalkHalf, halfSpan, z + off + sidewalkHalf));
        }
    }

    // Deterministic hash [0..1)
    private static float Hash01(string s, int salt)
    {
        unchecked
        {
            // FNV-ish offset basis, forced into int (this avoids your uint->int compiler error)
            int h = unchecked((int)2166136261);
            h = (h ^ salt) * 16777619;

            if (!string.IsNullOrEmpty(s))
            {
                for (int i = 0; i < s.Length; i++)
                    h = (h ^ s[i]) * 16777619;
            }

            int v = h & 0x00FFFFFF; // 24 bits
            return v / 16777216f;   // 2^24
        }
    }

    private static bool ContainsAny(string s, params string[] tokens)
    {
        if (string.IsNullOrEmpty(s)) return false;
        for (int i = 0; i < tokens.Length; i++)
            if (s.Contains(tokens[i])) return true;
        return false;
    }

    private static Vector3 SafeScale(ObjectDefinition o) => o == null ? Vector3.zero : o.scale;

    private static bool IsZeroOrInvalid(Vector3 v)
    {
        if (float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)) return true;
        if (float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z)) return true;
        return v == Vector3.zero || v.x <= 0f || v.y <= 0f || v.z <= 0f;
    }

    private static void EnsurePrimitiveCube(ObjectDefinition o)
    {
        if (o == null) return;
        if (string.IsNullOrWhiteSpace(o.kind)) o.kind = "Primitive";
        if (o.kind == "Primitive" && string.IsNullOrWhiteSpace(o.primitiveType))
            o.primitiveType = "Cube";
    }

    // Finds a point inside lot avoiding occupied rects, deterministic spiral search.
    private static Vector2 FindNonOverlappingPointInLot(Rect lot, float footprintX, float footprintZ, List<Rect> occupied, string id)
    {
        float px = Mathf.Lerp(lot.xMin, lot.xMax, Hash01(id, 501));
        float pz = Mathf.Lerp(lot.yMin, lot.yMax, Hash01(id, 503));

        px = Mathf.Clamp(px, lot.xMin + footprintX * 0.5f, lot.xMax - footprintX * 0.5f);
        pz = Mathf.Clamp(pz, lot.yMin + footprintZ * 0.5f, lot.yMax - footprintZ * 0.5f);

        if (!IntersectsAny(px, pz, footprintX, footprintZ, occupied))
            return new Vector2(px, pz);

        float step = Mathf.Max(1.0f, Mathf.Min(lot.width, lot.height) * 0.06f);
        int rings = 22;

        for (int r = 1; r <= rings; r++)
        {
            float radius = r * step;
            int points = 16 + r * 5;

            for (int i = 0; i < points; i++)
            {
                float ang = (i / (float)points) * Mathf.PI * 2f;
                float tx = px + Mathf.Cos(ang) * radius;
                float tz = pz + Mathf.Sin(ang) * radius;

                tx = Mathf.Clamp(tx, lot.xMin + footprintX * 0.5f, lot.xMax - footprintX * 0.5f);
                tz = Mathf.Clamp(tz, lot.yMin + footprintZ * 0.5f, lot.yMax - footprintZ * 0.5f);

                if (!IntersectsAny(tx, tz, footprintX, footprintZ, occupied))
                    return new Vector2(tx, tz);
            }
        }

        float cx = Mathf.Clamp(lot.center.x, lot.xMin + footprintX * 0.5f, lot.xMax - footprintX * 0.5f);
        float cz = Mathf.Clamp(lot.center.y, lot.yMin + footprintZ * 0.5f, lot.yMax - footprintZ * 0.5f);
        return new Vector2(cx, cz);
    }

    private static bool IntersectsAny(float x, float z, float fx, float fz, List<Rect> occupied)
    {
        Rect r = Rect.MinMaxRect(x - fx * 0.5f, z - fz * 0.5f, x + fx * 0.5f, z + fz * 0.5f);
        for (int i = 0; i < occupied.Count; i++)
            if (occupied[i].Overlaps(r)) return true;
        return false;
    }

    private static float ComputeFacingToNearestRoad(float x, float z, float[] xLines, float[] zLines)
    {
        float bestDx = float.MaxValue;
        for (int i = 0; i < xLines.Length; i++)
            bestDx = Mathf.Min(bestDx, Mathf.Abs(x - xLines[i]));

        float bestDz = float.MaxValue;
        for (int i = 0; i < zLines.Length; i++)
            bestDz = Mathf.Min(bestDz, Mathf.Abs(z - zLines[i]));

        if (bestDx < bestDz)
        {
            float nearest = Nearest(x, xLines);
            return (x < nearest) ? 90f : 270f;
        }
        else
        {
            float nearest = Nearest(z, zLines);
            return (z < nearest) ? 0f : 180f;
        }
    }

    private static float Nearest(float v, float[] lines)
    {
        float best = lines[0];
        float bestD = Mathf.Abs(v - best);
        for (int i = 1; i < lines.Length; i++)
        {
            float d = Mathf.Abs(v - lines[i]);
            if (d < bestD)
            {
                bestD = d;
                best = lines[i];
            }
        }
        return best;
    }

    // Reflection-safe rotation setter (won't crash if ObjectDefinition doesn't have rotation).
    private static void SetRotationYIfPossible(ObjectDefinition obj, float yawDegrees)
    {
        if (obj == null) return;

        TrySetVector3(obj, "rotation", new Vector3(0f, yawDegrees, 0f));
        TrySetVector3(obj, "eulerRotation", new Vector3(0f, yawDegrees, 0f));
        TrySetVector3(obj, "rotationEuler", new Vector3(0f, yawDegrees, 0f));
        TrySetVector3(obj, "eulerAngles", new Vector3(0f, yawDegrees, 0f));

        TrySetQuaternion(obj, "rotation", Quaternion.Euler(0f, yawDegrees, 0f));
        TrySetQuaternion(obj, "rot", Quaternion.Euler(0f, yawDegrees, 0f));
    }

    private static void TrySetVector3(object target, string member, Vector3 value)
    {
        var t = target.GetType();
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(Vector3))
        {
            f.SetValue(target, value);
            return;
        }

        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType == typeof(Vector3))
            p.SetValue(target, value, null);
    }

    private static void TrySetQuaternion(object target, string member, Quaternion value)
    {
        var t = target.GetType();
        var f = t.GetField(member, BindingFlags.Public | BindingFlags.Instance);
        if (f != null && f.FieldType == typeof(Quaternion))
        {
            f.SetValue(target, value);
            return;
        }

        var p = t.GetProperty(member, BindingFlags.Public | BindingFlags.Instance);
        if (p != null && p.CanWrite && p.PropertyType == typeof(Quaternion))
            p.SetValue(target, value, null);
    }
}
