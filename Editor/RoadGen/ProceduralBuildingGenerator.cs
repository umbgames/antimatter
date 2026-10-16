using UnityEngine;
using System.Collections.Generic;

namespace Antimatter
{
    public enum BuildingType
    {
        House,
        Shop,
        Skyscraper,
        Factory
    }

    public static class ProceduralBuildingGenerator
    {
        public static GameObject GenerateBuilding(
            BuildingType type,
            string name,
            float width,
            float depth,
            float height,
            Material material = null,
            System.Random rng = null)
        {
            if (rng == null) rng = new System.Random();

            GameObject building = new GameObject(name);

            switch (type)
            {
                case BuildingType.House:
                    BuildHouse(building, width, depth, height, material, rng);
                    break;
                case BuildingType.Skyscraper:
                    BuildSkyscraper(building, width, depth, height, material, rng);
                    break;
                case BuildingType.Shop:
                    BuildShop(building, width, depth, height, material, rng);
                    break;
                case BuildingType.Factory:
                    BuildFactory(building, width, depth, height, material, rng);
                    break;
            }

            // Create a compound collider that fits the bounding box roughly
            BoxCollider bc = building.AddComponent<BoxCollider>();
            bc.size = new Vector3(width, height, depth);
            bc.center = new Vector3(0, height / 2f, 0);

            return building;
        }

        private static void BuildHouse(GameObject root, float w, float d, float h, Material mat, System.Random rng)
        {
            // Base structure
            float baseH = h * 0.6f;
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "House_Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0, baseH / 2f, 0);
            baseObj.transform.localScale = new Vector3(w, baseH, d);
            ApplyMaterial(baseObj, mat);

            // Pitched Roof
            float roofH = h * 0.4f;
            GameObject roof = CreatePitchedRoof(w + 0.5f, d + 0.5f, roofH, mat);
            roof.name = "House_Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0, baseH, 0);

            // Chimney
            if (rng.NextDouble() > 0.5)
            {
                GameObject chimney = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chimney.name = "Chimney";
                chimney.transform.SetParent(root.transform, false);
                chimney.transform.localScale = new Vector3(0.6f, roofH * 1.5f, 0.6f);
                chimney.transform.localPosition = new Vector3((w / 2f) - 0.8f, baseH + (roofH / 2f), 0);
                SetColor(chimney, new Color(0.6f, 0.3f, 0.2f)); // Brick red
            }

            // Door
            GameObject door = GameObject.CreatePrimitive(PrimitiveType.Cube);
            door.name = "Door";
            door.transform.SetParent(root.transform, false);
            door.transform.localScale = new Vector3(1.2f, 2.2f, 0.2f);
            door.transform.localPosition = new Vector3(0, 1.1f, (d / 2f));
            SetColor(door, new Color(0.3f, 0.2f, 0.1f));

            // Windows
            for (int i = -1; i <= 1; i += 2)
            {
                GameObject win = GameObject.CreatePrimitive(PrimitiveType.Cube);
                win.name = "Window";
                win.transform.SetParent(root.transform, false);
                win.transform.localScale = new Vector3(w * 0.25f, 1.2f, 0.2f);
                win.transform.localPosition = new Vector3(i * (w * 0.25f), 1.5f, (d / 2f));
                SetColor(win, new Color(0.8f, 0.9f, 1.0f));
            }
        }

        private static void BuildShop(GameObject root, float w, float d, float h, Material mat, System.Random rng)
        {
            // Flat boxy shop, sometimes with a little awning
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Shop_Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0, h / 2f, 0);
            baseObj.transform.localScale = new Vector3(w, h, d);
            ApplyMaterial(baseObj, mat);

            // Large storefront window
            GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "Storefront_Window";
            window.transform.SetParent(root.transform, false);
            window.transform.localScale = new Vector3(w * 0.8f, 3f, 0.2f);
            window.transform.localPosition = new Vector3(0, 2f, (d / 2f));
            SetColor(window, new Color(0.5f, 0.8f, 0.9f, 0.5f)); // Light blueish window

            // Awning
            GameObject awning = GameObject.CreatePrimitive(PrimitiveType.Cube);
            awning.name = "Awning";
            awning.transform.SetParent(root.transform, false);
            awning.transform.localScale = new Vector3(w + 0.4f, 0.2f, 2f);
            awning.transform.localPosition = new Vector3(0, 3.8f, (d / 2f) + 1f);
            awning.transform.localRotation = Quaternion.Euler(15, 0, 0);
            SetColor(awning, new Color(0.8f, 0.2f, 0.2f)); // Red awning
        }

        private static void BuildSkyscraper(GameObject root, float w, float d, float h, Material mat, System.Random rng)
        {
            float floorH = 3f;
            int numFloors = Mathf.FloorToInt(h / floorH);
            if (numFloors < 2) numFloors = 2;

            float curY = 0;
            float curW = w;
            float curD = d;

            for (int i = 0; i < numFloors; i++)
            {
                // Taper the skyscraper at the top
                if (i > numFloors * 0.7f && rng.NextDouble() > 0.5f)
                {
                    curW *= 0.8f;
                    curD *= 0.8f;
                }

                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = $"Floor_{i}";
                floor.transform.SetParent(root.transform, false);
                floor.transform.localPosition = new Vector3(0, curY + (floorH / 2f), 0);
                floor.transform.localScale = new Vector3(curW, floorH * 0.9f, curD); // Leave gap for ledge/trim
                ApplyMaterial(floor, mat);

                // Add glass window bands
                GameObject window = GameObject.CreatePrimitive(PrimitiveType.Cube);
                window.name = $"Window_{i}";
                window.transform.SetParent(floor.transform, false);
                window.transform.localPosition = Vector3.zero;
                window.transform.localScale = new Vector3(1.02f, 0.6f, 1.02f); // Slightly larger than floor
                SetColor(window, new Color(0.1f, 0.2f, 0.3f, 0.8f)); // Dark glass

                curY += floorH;
            }

            // Roof block
            GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0, curY + 0.5f, 0);
            roof.transform.localScale = new Vector3(curW * 0.9f, 1f, curD * 0.9f);
            ApplyMaterial(roof, mat);

            // Antenna
            if (rng.NextDouble() > 0.5)
            {
                GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                antenna.name = "Antenna";
                antenna.transform.SetParent(root.transform, false);
                antenna.transform.localPosition = new Vector3(0, curY + 3.5f, 0);
                antenna.transform.localScale = new Vector3(0.5f, 2.5f, 0.5f);
                SetColor(antenna, Color.gray);
            }
        }

        private static void BuildFactory(GameObject root, float w, float d, float h, Material mat, System.Random rng)
        {
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.name = "Factory_Base";
            baseObj.transform.SetParent(root.transform, false);
            baseObj.transform.localPosition = new Vector3(0, h / 2f, 0);
            baseObj.transform.localScale = new Vector3(w, h, d);
            ApplyMaterial(baseObj, mat);

            // Add some smokestacks
            int numStacks = rng.Next(1, 4);
            for (int i = 0; i < numStacks; i++)
            {
                float sx = (float)(rng.NextDouble() - 0.5) * (w * 0.6f);
                float sz = (float)(rng.NextDouble() - 0.5) * (d * 0.6f);
                float sh = h * 1.5f;

                GameObject stack = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                stack.name = $"Smokestack_{i}";
                stack.transform.SetParent(root.transform, false);
                stack.transform.localPosition = new Vector3(sx, h + (sh / 2f), sz);
                stack.transform.localScale = new Vector3(1.5f, sh / 2f, 1.5f);
                SetColor(stack, new Color(0.4f, 0.4f, 0.4f));
            }
        }

        public static GameObject GeneratePOI(string description, string name, float size, Material mat, System.Random rng)
        {
            if (rng == null) rng = new System.Random();
            GameObject poi = new GameObject(name);
            
            description = description.ToLowerInvariant();
            if (description.Contains("castle") || description.Contains("ruin"))
            {
                // Build a ruined castle: Central keep, 4 corner towers (some broken)
                float h = size * 1.5f;
                GameObject keep = GameObject.CreatePrimitive(PrimitiveType.Cube);
                keep.name = "Keep";
                keep.transform.SetParent(poi.transform, false);
                keep.transform.localPosition = new Vector3(0, h/2, 0);
                keep.transform.localScale = new Vector3(size, h, size);
                ApplyMaterial(keep, mat);
                SetColor(keep, new Color(0.4f, 0.4f, 0.4f));

                for (int i=0; i<4; i++) {
                    float towerH = (rng.NextDouble() > 0.5f) ? h * 1.2f : h * 0.6f; // some broken
                    float tx = (i%2 == 0) ? size/2 : -size/2;
                    float tz = (i/2 == 0) ? size/2 : -size/2;
                    GameObject t = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    t.name = $"Tower_{i}";
                    t.transform.SetParent(poi.transform, false);
                    t.transform.localPosition = new Vector3(tx, towerH/2, tz);
                    t.transform.localScale = new Vector3(size*0.4f, towerH/2f, size*0.4f);
                    ApplyMaterial(t, mat);
                    SetColor(t, new Color(0.35f, 0.35f, 0.35f));
                }
            }
            else if (description.Contains("crater") || description.Contains("glowing") || description.Contains("magic") || description.Contains("crystal"))
            {
                // Magical crystal formation
                for (int i=0; i<8; i++) {
                    GameObject crystal = GameObject.CreatePrimitive(PrimitiveType.Cylinder); // fake crystal
                    crystal.name = $"Crystal_{i}";
                    crystal.transform.SetParent(poi.transform, false);
                    float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                    float rad = size * 0.4f;
                    crystal.transform.localPosition = new Vector3(Mathf.Cos(angle)*rad, size*0.8f, Mathf.Sin(angle)*rad);
                    crystal.transform.localRotation = Quaternion.Euler((float)rng.NextDouble()*45f, (float)rng.NextDouble()*360f, (float)rng.NextDouble()*45f);
                    crystal.transform.localScale = new Vector3(size*0.2f, size*1.5f, size*0.2f);
                    SetColor(crystal, new Color(0.1f, 0.8f, 1f)); // cyan glow
                }
            }
            else
            {
                // Generic obelisk
                GameObject ob = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ob.name = "Obelisk";
                ob.transform.SetParent(poi.transform, false);
                ob.transform.localPosition = new Vector3(0, size*2f, 0);
                ob.transform.localScale = new Vector3(size*0.5f, size*4f, size*0.5f);
                SetColor(ob, new Color(0.2f, 0.2f, 0.2f));
            }
            return poi;
        }

        private static GameObject CreatePitchedRoof(float w, float d, float h, Material mat)
        {
            GameObject roofGo = new GameObject("PitchedRoof");
            MeshFilter mf = roofGo.AddComponent<MeshFilter>();
            MeshRenderer mr = roofGo.AddComponent<MeshRenderer>();
            
            if (mat != null) mr.sharedMaterial = mat;

            Mesh mesh = new Mesh();
            
            float hw = w / 2f;
            float hd = d / 2f;

            Vector3[] vertices = new Vector3[]
            {
                new Vector3(-hw, 0, -hd), // 0: bottom front left
                new Vector3(hw, 0, -hd),  // 1: bottom front right
                new Vector3(hw, 0, hd),   // 2: bottom back right
                new Vector3(-hw, 0, hd),  // 3: bottom back left
                new Vector3(-hw, h, 0),   // 4: top ridge left
                new Vector3(hw, h, 0)     // 5: top ridge right
            };

            int[] triangles = new int[]
            {
                // Front triangle
                0, 4, 1,
                1, 4, 5, // Wait, front face is 0,1, ridge is 4,5
                // Actually front slope
                0, 4, 5,
                0, 5, 1,
                // Back slope
                3, 5, 4,
                3, 2, 5,
                // Left triangle
                0, 3, 4,
                // Right triangle
                1, 5, 2,
                // Bottom
                0, 1, 2,
                0, 2, 3
            };

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            return roofGo;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            if (mat != null)
            {
                Renderer r = go.GetComponent<Renderer>();
                if (r != null) r.sharedMaterial = mat;
            }
        }

        private static void SetColor(GameObject go, Color color)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                // Create a property block to set color without instantiating materials
                MaterialPropertyBlock props = new MaterialPropertyBlock();
                r.GetPropertyBlock(props);
                props.SetColor("_Color", color);
                r.SetPropertyBlock(props);
            }
        }
    }
}
