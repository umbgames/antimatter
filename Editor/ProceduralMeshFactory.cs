using System.Collections.Generic;
using UnityEngine;

namespace Antimatter
{
    /// <summary>
    /// Procedural mesh factory used by the AI Agent to create 3D shapes
    /// that go far beyond Unity's built-in 6 primitives.
    /// Every method returns a ready-to-use Mesh with normals and UVs.
    /// </summary>
    public static class ProceduralMeshFactory
    {
        // ──────────────────── PUBLIC API ────────────────────

        /// <summary>
        /// Creates a mesh by shape name.  Returns null for unknown shapes.
        /// All size parameters default to sensible values when omitted.
        /// </summary>
        public static Mesh Create(string shape,
            float width = 1f, float height = 1f, float depth = 1f,
            float radius = 0.5f, float innerRadius = 0.15f,
            int segments = 24, int secondarySegments = 12,
            int count = 6, float thickness = 0.1f,
            float topRadius = 0f, float taperRatio = 0.6f)
        {
            Mesh m = shape switch
            {
                "Cone"           => BuildCone(radius, height, segments),
                "Torus"          => BuildTorus(radius, innerRadius, segments, secondarySegments),
                "HalfSphere"     => BuildHalfSphere(radius, segments, secondarySegments),
                "Wedge"          => BuildWedge(width, height, depth),
                "Arch"           => BuildArch(width, height, depth, thickness, segments),
                "Stairs"         => BuildStairs(width, height, depth, count),
                "Tube"           => BuildTube(radius, innerRadius, height, segments),
                "Pyramid"        => BuildPyramid(width, height, depth),
                "Ring"           => BuildRing(radius, innerRadius, thickness, segments),
                "Ramp"           => BuildRamp(width, height, depth),
                "LShape"         => BuildLShape(width, height, depth, thickness),
                "TShape"         => BuildTShape(width, height, depth, thickness),
                "Gear"           => BuildGear(radius, height, count, thickness),
                "Bowl"           => BuildBowl(radius, thickness, segments, secondarySegments),
                "Fence"          => BuildFence(width, height, count, thickness),
                "Net"            => BuildNet(radius, height, segments, count, taperRatio),
                "Prism"          => BuildPrism(width, height, depth),
                "House"          => BuildHouse(width, height, depth),
                "Frustum"        => BuildFrustum(radius, topRadius, height, segments),
                "Cross"          => BuildCross(width, height, depth, thickness),
                "Barrel"         => BuildBarrel(radius, height, segments),
                "Pole"           => BuildPole(radius, height, segments),
                                "RadioTower" => BuildRadioTower(),
                "Bunker" => BuildBunker(),
                "Windmill" => BuildWindmill(),
                "Lighthouse" => BuildLighthouse(),
                "Castle" => BuildCastle(),
                "Skyscraper" => BuildSkyscraper(),
                "Cabin" => BuildCabin(),
                "Tent" => BuildTent(),
                "Bridge" => BuildBridge(),
                "Factory" => BuildFactory(),
                "Warehouse" => BuildWarehouse(),
                "Barn" => BuildBarn(),
                "Silo" => BuildSilo(),
                "WaterTower" => BuildWaterTower(),
                "Car" => BuildCar(),
                "Truck" => BuildTruck(),
                "Bus" => BuildBus(),
                "Van" => BuildVan(),
                "SUV" => BuildSUV(),
                "Motorcycle" => BuildMotorcycle(),
                "Bicycle" => BuildBicycle(),
                "Train" => BuildTrain(),
                "Carriage" => BuildCarriage(),
                "Tank" => BuildTank(),
                "Jeep" => BuildJeep(),
                "Helicopter" => BuildHelicopter(),
                "Airplane" => BuildAirplane(),
                "Boat" => BuildBoat(),
                "Submarine" => BuildSubmarine(),
                "Sword" => BuildSword(),
                "Dagger" => BuildDagger(),
                "AxeWeapon" => BuildAxeWeapon(),
                "Spear" => BuildSpear(),
                "Bow" => BuildBow(),
                "Crossbow" => BuildCrossbow(),
                "Pistol" => BuildPistol(),
                "Rifle" => BuildRifle(),
                "Shotgun" => BuildShotgun(),
                "SniperRifle" => BuildSniperRifle(),
                "RocketLauncher" => BuildRocketLauncher(),
                "Shield" => BuildShield(),
                "Bomb" => BuildBomb(),
                "Mine" => BuildMine(),
                "Turret" => BuildTurret(),
                "Chair" => BuildChair(),
                "Table" => BuildTable(),
                "Desk" => BuildDesk(),
                "Bed" => BuildBed(),
                "Sofa" => BuildSofa(),
                "Bookshelf" => BuildBookshelf(),
                "Cabinet" => BuildCabinet(),
                "Wardrobe" => BuildWardrobe(),
                "Dresser" => BuildDresser(),
                "Nightstand" => BuildNightstand(),
                "Stool" => BuildStool(),
                "Bench" => BuildBench(),
                "TVStand" => BuildTVStand(),
                "DiningTable" => BuildDiningTable(),
                "CoffeeTable" => BuildCoffeeTable(),
                "TV" => BuildTV(),
                "Monitor" => BuildMonitor(),
                "Keyboard" => BuildKeyboard(),
                "ComputerMouse" => BuildComputerMouse(),
                "Laptop" => BuildLaptop(),
                "PC" => BuildPC(),
                "Phone" => BuildPhone(),
                "Tablet" => BuildTablet(),
                "Printer" => BuildPrinter(),
                "Speaker" => BuildSpeaker(),
                "Microphone" => BuildMicrophone(),
                "Camera" => BuildCamera(),
                "Router" => BuildRouter(),
                "Controller" => BuildController(),
                "Drone" => BuildDrone(),
                "PineTree" => BuildPineTree(),
                "OakTree" => BuildOakTree(),
                "PalmTree" => BuildPalmTree(),
                "DeadTree" => BuildDeadTree(),
                "Bush" => BuildBush(),
                "Fern" => BuildFern(),
                "Cactus" => BuildCactus(),
                "Mushroom" => BuildMushroom(),
                "Rock" => BuildRock(),
                "Boulder" => BuildBoulder(),
                "Log" => BuildLog(),
                "Stump" => BuildStump(),
                "Flower" => BuildFlower(),
                "GrassTuft" => BuildGrassTuft(),
                "Vine" => BuildVine(),
                "Fridge" => BuildFridge(),
                "Stove" => BuildStove(),
                "Microwave" => BuildMicrowave(),
                "Oven" => BuildOven(),
                "Dishwasher" => BuildDishwasher(),
                "Washer" => BuildWasher(),
                "Dryer" => BuildDryer(),
                "Toaster" => BuildToaster(),
                "Blender" => BuildBlender(),
                "CoffeeMaker" => BuildCoffeeMaker(),
                "Vacuum" => BuildVacuum(),
                "Iron" => BuildIron(),
                "Fan" => BuildFan(),
                "AirConditioner" => BuildAirConditioner(),
                "Heater" => BuildHeater(),
                "Hammer" => BuildHammer(),
                "Wrench" => BuildWrench(),
                "Screwdriver" => BuildScrewdriver(),
                "Saw" => BuildSaw(),
                "Drill" => BuildDrill(),
                "Pliers" => BuildPliers(),
                "Shovel" => BuildShovel(),
                "Rake" => BuildRake(),
                "Pickaxe" => BuildPickaxe(),
                "AxeTool" => BuildAxeTool(),
                "Crowbar" => BuildCrowbar(),
                "TapeMeasure" => BuildTapeMeasure(),
                "Level" => BuildLevel(),
                "Trowel" => BuildTrowel(),
                "Toolbox" => BuildToolbox(),
                "Apple" => BuildApple(),
                "Banana" => BuildBanana(),
                "Burger" => BuildBurger(),
                "Pizza" => BuildPizza(),
                "Hotdog" => BuildHotdog(),
                "Bread" => BuildBread(),
                "Cheese" => BuildCheese(),
                "Cake" => BuildCake(),
                "Pie" => BuildPie(),
                "Donut" => BuildDonut(),
                "IceCream" => BuildIceCream(),
                "Steak" => BuildSteak(),
                "Chicken" => BuildChicken(),
                "Fish" => BuildFish(),
                "Can" => BuildCan(),
                "WoodenBarrel" => BuildWoodenBarrel(),
                "Crate" => BuildCrate(),
                "CardboardBox" => BuildCardboardBox(),
                "Chest" => BuildChest(),
                "Safe" => BuildSafe(),
                "TrashCan" => BuildTrashCan(),
                "Dumpster" => BuildDumpster(),
                "Streetlight" => BuildStreetlight(),
                "TrafficLight" => BuildTrafficLight(),
                "Hydrant" => BuildHydrant(),
                "Mailbox" => BuildMailbox(),
                "Sign" => BuildSign(),
                "WoodenFence" => BuildWoodenFence(),
                "BrickWall" => BuildBrickWall(),
                "Pillar" => BuildPillar(),
                _                => null
            };

            if (m != null) m.name = $"Procedural_{shape}";
            return m;
        }

        /// <summary>All supported shape names.</summary>
        public static readonly string[] SupportedShapes = new[]
        {
            "Cone", "Torus", "HalfSphere", "Wedge", "Arch", "Stairs",
            "Tube", "Pyramid", "Ring", "Ramp", "LShape", "TShape",
            "Gear", "Bowl", "Fence", "Net", "Prism", "House",
            "Frustum", "Cross", "Barrel", "Pole", "RadioTower", "Bunker", "Windmill", "Lighthouse", "Castle", "Skyscraper", "House", "Cabin", "Tent", "Bridge", "Factory", "Warehouse", "Barn", "Silo", "WaterTower", "Car", "Truck", "Bus", "Van", "SUV", "Motorcycle", "Bicycle", "Train", "Carriage", "Tank", "Jeep", "Helicopter", "Airplane", "Boat", "Submarine", "Sword", "Dagger", "AxeWeapon", "Spear", "Bow", "Crossbow", "Pistol", "Rifle", "Shotgun", "SniperRifle", "RocketLauncher", "Shield", "Bomb", "Mine", "Turret", "Chair", "Table", "Desk", "Bed", "Sofa", "Bookshelf", "Cabinet", "Wardrobe", "Dresser", "Nightstand", "Stool", "Bench", "TVStand", "DiningTable", "CoffeeTable", "TV", "Monitor", "Keyboard", "ComputerMouse", "Laptop", "PC", "Phone", "Tablet", "Printer", "Speaker", "Microphone", "Camera", "Router", "Controller", "Drone", "PineTree", "OakTree", "PalmTree", "DeadTree", "Bush", "Fern", "Cactus", "Mushroom", "Rock", "Boulder", "Log", "Stump", "Flower", "GrassTuft", "Vine", "Fridge", "Stove", "Microwave", "Oven", "Dishwasher", "Washer", "Dryer", "Toaster", "Blender", "CoffeeMaker", "Vacuum", "Iron", "Fan", "AirConditioner", "Heater", "Hammer", "Wrench", "Screwdriver", "Saw", "Drill", "Pliers", "Shovel", "Rake", "Pickaxe", "AxeTool", "Crowbar", "TapeMeasure", "Level", "Trowel", "Toolbox", "Apple", "Banana", "Burger", "Pizza", "Hotdog", "Bread", "Cheese", "Cake", "Pie", "Donut", "IceCream", "Steak", "Chicken", "Fish", "Can", "WoodenBarrel", "Crate", "CardboardBox", "Chest", "Safe", "TrashCan", "Dumpster", "Streetlight", "TrafficLight", "Hydrant", "Mailbox", "Sign", "WoodenFence", "BrickWall", "Pillar"
        };

        // ──────────────────── MESH BUILDERS ────────────────────

        // ── Cone ────────────────────────────────────────────────
        private static Mesh BuildCone(float radius, float height, int seg)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            v.Add(new Vector3(0, height, 0));               // 0 = tip
            uv.Add(new Vector2(0.5f, 1f));

            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                v.Add(new Vector3(Mathf.Cos(a) * radius, 0, Mathf.Sin(a) * radius));
                uv.Add(new Vector2((float)i / seg, 0f));
            }

            v.Add(Vector3.zero);                            // base centre
            uv.Add(new Vector2(0.5f, 0.5f));
            int bc = v.Count - 1;

            for (int i = 1; i <= seg; i++)
            {
                t.Add(0); t.Add(i % seg + 1); t.Add(i);         // side
                t.Add(bc); t.Add(i); t.Add(i % seg + 1);        // base
            }

            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Torus ───────────────────────────────────────────────
        private static Mesh BuildTorus(float majorR, float minorR, int segMajor, int segMinor)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            var uv = new List<Vector2>();

            for (int i = 0; i <= segMajor; i++)
            {
                float a0 = (float)i / segMajor * Mathf.PI * 2f;
                Vector3 cen = new Vector3(Mathf.Cos(a0) * majorR, 0, Mathf.Sin(a0) * majorR);
                Vector3 right = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0));
                for (int j = 0; j <= segMinor; j++)
                {
                    float a1 = (float)j / segMinor * Mathf.PI * 2f;
                    v.Add(cen + right * (Mathf.Cos(a1) * minorR) + Vector3.up * (Mathf.Sin(a1) * minorR));
                    uv.Add(new Vector2((float)i / segMajor, (float)j / segMinor));
                }
            }

            for (int i = 0; i < segMajor; i++)
            {
                for (int j = 0; j < segMinor; j++)
                {
                    int c = i * (segMinor + 1) + j;
                    int n = c + 1;
                    int tp = (i + 1) * (segMinor + 1) + j;
                    int tn = tp + 1;
                    t.Add(c); t.Add(tn); t.Add(tp);
                    t.Add(c); t.Add(n); t.Add(tn);
                }
            }

            m.SetVertices(v); m.SetTriangles(t, 0); m.SetUVs(0, uv);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── HalfSphere / Dome ───────────────────────────────────
        private static Mesh BuildHalfSphere(float radius, int hSeg, int vSeg)
        {
            if (vSeg < 2) vSeg = 8;
            var m = new Mesh();
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();

            for (int y = 0; y <= vSeg; y++)
            {
                float phi = Mathf.PI * 0.5f * y / vSeg; // 0 to π/2
                float rr = Mathf.Cos(phi) * radius;
                float yy = Mathf.Sin(phi) * radius;
                for (int x = 0; x <= hSeg; x++)
                {
                    float theta = Mathf.PI * 2f * x / hSeg;
                    verts.Add(new Vector3(Mathf.Cos(theta) * rr, yy, Mathf.Sin(theta) * rr));
                    uvs.Add(new Vector2((float)x / hSeg, (float)y / vSeg));
                }
            }

            for (int y = 0; y < vSeg; y++)
            {
                for (int x = 0; x < hSeg; x++)
                {
                    int c = y * (hSeg + 1) + x;
                    int n = c + 1;
                    int tp = c + hSeg + 1;
                    int tn = tp + 1;
                    tris.Add(c); tris.Add(tp); tris.Add(n);
                    tris.Add(n); tris.Add(tp); tris.Add(tn);
                }
            }

            m.SetVertices(verts); m.SetTriangles(tris, 0); m.SetUVs(0, uvs);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Wedge / Ramp ────────────────────────────────────────
        private static Mesh BuildWedge(float w, float h, float d)
        {
            var m = new Mesh();
            m.vertices = new Vector3[]
            {
                // Bottom face
                new(-w/2,0,-d/2), new(w/2,0,-d/2), new(w/2,0,d/2), new(-w/2,0,d/2),
                // Back face (tall side)
                new(-w/2,0,-d/2), new(w/2,0,-d/2), new(w/2,h,-d/2), new(-w/2,h,-d/2),
                // Slope face
                new(-w/2,h,-d/2), new(w/2,h,-d/2), new(w/2,0,d/2), new(-w/2,0,d/2),
                // Left triangle
                new(-w/2,0,-d/2), new(-w/2,h,-d/2), new(-w/2,0,d/2),
                // Right triangle
                new(w/2,0,-d/2), new(w/2,0,d/2), new(w/2,h,-d/2)
            };
            m.triangles = new int[]
            {
                0,2,1, 0,3,2,       // bottom
                4,6,5, 4,7,6,       // back
                8,10,9, 8,11,10,    // slope
                12,14,13,           // left
                15,17,16            // right
            };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Arch ────────────────────────────────────────────────
        private static Mesh BuildArch(float w, float h, float d, float thick, int seg)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            float halfW = w / 2f;
            float archR = halfW;
            float archH = h - archR; // height of straight pillars under the arch

            // Build two rows of vertices (outer/inner) along the arch profile
            for (int side = 0; side < 2; side++)
            {
                float zOff = (side == 0) ? -d / 2f : d / 2f;

                // Left pillar bottom
                v.Add(new Vector3(-halfW, 0, zOff));
                v.Add(new Vector3(-halfW + thick, 0, zOff));
                // Left pillar top
                v.Add(new Vector3(-halfW, archH, zOff));
                v.Add(new Vector3(-halfW + thick, archH, zOff));

                // Arch curve
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.PI * i / seg;
                    float cx = -Mathf.Cos(a) * archR;
                    float cy = Mathf.Sin(a) * archR + archH;
                    float ix = -Mathf.Cos(a) * (archR - thick);
                    float iy = Mathf.Sin(a) * (archR - thick) + archH;
                    v.Add(new Vector3(cx, cy, zOff));
                    v.Add(new Vector3(ix, iy, zOff));
                }

                // Right pillar top
                v.Add(new Vector3(halfW - thick, archH, zOff));
                v.Add(new Vector3(halfW, archH, zOff));
                // Right pillar bottom
                v.Add(new Vector3(halfW - thick, 0, zOff));
                v.Add(new Vector3(halfW, 0, zOff));
            }

            int vertsPerSide = v.Count / 2;

            // Front face quads
            for (int i = 0; i < vertsPerSide - 2; i += 2)
            {
                int a = i, b = i + 1, c = i + 2, dd = i + 3;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(dd);
            }

            // Back face quads (reversed winding)
            for (int i = vertsPerSide; i < v.Count - 2; i += 2)
            {
                int a = i, b = i + 1, c = i + 2, dd = i + 3;
                t.Add(a); t.Add(b); t.Add(c);
                t.Add(b); t.Add(dd); t.Add(c);
            }

            // Bridge front to back (outer edges)
            for (int i = 0; i < vertsPerSide - 2; i += 2)
            {
                int f = i, fn = i + 2, b = i + vertsPerSide, bn = i + 2 + vertsPerSide;
                t.Add(f); t.Add(b); t.Add(fn);
                t.Add(fn); t.Add(b); t.Add(bn);
            }

            // Bridge front to back (inner edges)
            for (int i = 1; i < vertsPerSide - 2; i += 2)
            {
                int f = i, fn = i + 2, b = i + vertsPerSide, bn = i + 2 + vertsPerSide;
                t.Add(f); t.Add(fn); t.Add(b);
                t.Add(fn); t.Add(bn); t.Add(b);
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Stairs ──────────────────────────────────────────────
        private static Mesh BuildStairs(float totalW, float totalH, float totalD, int steps)
        {
            if (steps < 1) steps = 4;
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            float stepH = totalH / steps;
            float stepD = totalD / steps;
            float halfW = totalW / 2f;

            for (int i = 0; i < steps; i++)
            {
                float y0 = stepH * i;
                float y1 = y0 + stepH;
                float z0 = stepD * i;
                float z1 = z0 + stepD;
                int b = v.Count;

                // 8 corners of this step block
                v.Add(new Vector3(-halfW, y0, z0)); // 0
                v.Add(new Vector3( halfW, y0, z0)); // 1
                v.Add(new Vector3( halfW, y1, z0)); // 2
                v.Add(new Vector3(-halfW, y1, z0)); // 3
                v.Add(new Vector3(-halfW, y0, z1)); // 4
                v.Add(new Vector3( halfW, y0, z1)); // 5
                v.Add(new Vector3( halfW, y1, z1)); // 6
                v.Add(new Vector3(-halfW, y1, z1)); // 7

                // Front
                t.Add(b);t.Add(b+2);t.Add(b+1); t.Add(b);t.Add(b+3);t.Add(b+2);
                // Top
                t.Add(b+3);t.Add(b+6);t.Add(b+2); t.Add(b+3);t.Add(b+7);t.Add(b+6);
                // Left
                t.Add(b);t.Add(b+7);t.Add(b+3); t.Add(b);t.Add(b+4);t.Add(b+7);
                // Right
                t.Add(b+1);t.Add(b+2);t.Add(b+6); t.Add(b+1);t.Add(b+6);t.Add(b+5);
                // Bottom (only for first step)
                if (i == 0)
                { t.Add(b);t.Add(b+1);t.Add(b+5); t.Add(b);t.Add(b+5);t.Add(b+4); }
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Tube (hollow cylinder) ──────────────────────────────
        private static Mesh BuildTube(float outerR, float innerR, float h, int seg)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                float cx = Mathf.Cos(a), cz = Mathf.Sin(a);
                v.Add(new Vector3(cx * outerR, 0, cz * outerR));   // outer bottom
                v.Add(new Vector3(cx * outerR, h, cz * outerR));   // outer top
                v.Add(new Vector3(cx * innerR, 0, cz * innerR));   // inner bottom
                v.Add(new Vector3(cx * innerR, h, cz * innerR));   // inner top
            }

            for (int i = 0; i < seg; i++)
            {
                int c = i * 4;
                int n = (i + 1) * 4;
                // Outer wall
                t.Add(c); t.Add(n); t.Add(c+1);
                t.Add(c+1); t.Add(n); t.Add(n+1);
                // Inner wall (reversed)
                t.Add(c+2); t.Add(c+3); t.Add(n+2);
                t.Add(c+3); t.Add(n+3); t.Add(n+2);
                // Top ring
                t.Add(c+1); t.Add(n+1); t.Add(n+3);
                t.Add(c+1); t.Add(n+3); t.Add(c+3);
                // Bottom ring
                t.Add(c); t.Add(n+2); t.Add(n);
                t.Add(c); t.Add(c+2); t.Add(n+2);
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Pyramid ─────────────────────────────────────────────
        private static Mesh BuildPyramid(float w, float h, float d)
        {
            var m = new Mesh();
            m.vertices = new Vector3[]
            {
                new(-w/2,0,-d/2), new(w/2,0,-d/2), new(w/2,0,d/2), new(-w/2,0,d/2),
                new(0,h,0)
            };
            m.triangles = new int[]
            {
                0,2,1, 0,3,2,  // base
                0,1,4,          // front
                1,2,4,          // right
                2,3,4,          // back
                3,0,4           // left
            };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Ring (flat washer/disc) ──────────────────────────────
        private static Mesh BuildRing(float outerR, float innerR, float h, int seg)
        {
            // Basically a very short tube
            return BuildTube(outerR, innerR, Mathf.Max(h, 0.01f), seg);
        }

        // ── Ramp (smooth incline) ───────────────────────────────
        private static Mesh BuildRamp(float w, float h, float d)
        {
            return BuildWedge(w, h, d);
        }

        // ── L-Shape ─────────────────────────────────────────────
        private static Mesh BuildLShape(float w, float h, float d, float thick)
        {
            // Compose two boxes into an L-shape mesh
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            AddBox(v, t, new Vector3(0, h/2f, 0), new Vector3(thick, h, d));
            AddBox(v, t, new Vector3(w/2f, thick/2f, 0), new Vector3(w, thick, d));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── T-Shape ─────────────────────────────────────────────
        private static Mesh BuildTShape(float w, float h, float d, float thick)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            AddBox(v, t, new Vector3(0, h/2f, 0), new Vector3(thick, h, d));          // vertical
            AddBox(v, t, new Vector3(0, h - thick/2f, 0), new Vector3(w, thick, d));   // horizontal top

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Gear ────────────────────────────────────────────────
        private static Mesh BuildGear(float radius, float thick, int teeth, float toothDepth)
        {
            var m = new Mesh();
            float innerR = radius - toothDepth;
            var v = new List<Vector3>();
            var t = new List<int>();
            int seg = teeth * 2;

            v.Add(Vector3.zero);                      // 0 = bottom centre
            v.Add(new Vector3(0, thick, 0));           // 1 = top centre

            for (int i = 0; i <= seg; i++)
            {
                float a = (float)i / seg * Mathf.PI * 2f;
                float r = (i % 2 == 0) ? radius : innerR;
                v.Add(new Vector3(Mathf.Cos(a)*r, 0, Mathf.Sin(a)*r));
                v.Add(new Vector3(Mathf.Cos(a)*r, thick, Mathf.Sin(a)*r));
            }

            for (int i = 0; i < seg; i++)
            {
                int b0 = 2+i*2, t0 = b0+1;
                int b1 = 2+((i+1)%seg)*2, t1 = b1+1;
                t.Add(0);t.Add(b1);t.Add(b0);
                t.Add(1);t.Add(t0);t.Add(t1);
                t.Add(b0);t.Add(t1);t.Add(t0);
                t.Add(b0);t.Add(b1);t.Add(t1);
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Bowl (half-sphere shell) ────────────────────────────
        private static Mesh BuildBowl(float radius, float thick, int hSeg, int vSeg)
        {
            if (vSeg < 2) vSeg = 8;
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            float outerR = radius;
            float innerR = radius - thick;

            // Outer shell (hemisphere)
            int outerStart = 0;
            for (int y = 0; y <= vSeg; y++)
            {
                float phi = Mathf.PI * 0.5f * y / vSeg;
                float rr = Mathf.Cos(phi) * outerR;
                float yy = Mathf.Sin(phi) * outerR;
                for (int x = 0; x <= hSeg; x++)
                {
                    float theta = Mathf.PI * 2f * x / hSeg;
                    v.Add(new Vector3(Mathf.Cos(theta)*rr, yy, Mathf.Sin(theta)*rr));
                }
            }

            for (int y = 0; y < vSeg; y++)
                for (int x = 0; x < hSeg; x++)
                {
                    int c = outerStart + y*(hSeg+1)+x;
                    int n = c+1, tp = c+hSeg+1, tn = tp+1;
                    t.Add(c);t.Add(tp);t.Add(n);
                    t.Add(n);t.Add(tp);t.Add(tn);
                }

            // Inner shell (reversed winding)
            int innerStart = v.Count;
            for (int y = 0; y <= vSeg; y++)
            {
                float phi = Mathf.PI * 0.5f * y / vSeg;
                float rr = Mathf.Cos(phi) * innerR;
                float yy = Mathf.Sin(phi) * innerR;
                for (int x = 0; x <= hSeg; x++)
                {
                    float theta = Mathf.PI * 2f * x / hSeg;
                    v.Add(new Vector3(Mathf.Cos(theta)*rr, yy, Mathf.Sin(theta)*rr));
                }
            }

            for (int y = 0; y < vSeg; y++)
                for (int x = 0; x < hSeg; x++)
                {
                    int c = innerStart + y*(hSeg+1)+x;
                    int n = c+1, tp = c+hSeg+1, tn = tp+1;
                    t.Add(c);t.Add(n);t.Add(tp);
                    t.Add(n);t.Add(tn);t.Add(tp);
                }

            // Bottom rim connecting inner to outer at y=0
            for (int x = 0; x < hSeg; x++)
            {
                int oA = outerStart + x, oB = outerStart + x + 1;
                int iA = innerStart + x, iB = innerStart + x + 1;
                t.Add(oA);t.Add(oB);t.Add(iA);
                t.Add(oB);t.Add(iB);t.Add(iA);
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Fence (picket-style) ────────────────────────────────
        private static Mesh BuildFence(float totalW, float h, int posts, float thick)
        {
            if (posts < 2) posts = 2;
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            float spacing = totalW / (posts - 1);
            float halfT = thick / 2f;
            float halfD = thick / 2f;

            // Top rail
            AddBox(v, t, new Vector3(totalW / 2f, h - halfT, 0), new Vector3(totalW + thick, thick, thick));
            // Bottom rail
            AddBox(v, t, new Vector3(totalW / 2f, h * 0.3f, 0), new Vector3(totalW + thick, thick, thick));

            // Posts
            for (int i = 0; i < posts; i++)
            {
                float x = spacing * i;
                AddBox(v, t, new Vector3(x, h / 2f, 0), new Vector3(thick, h, thick));
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Net (basketball/goal net) ───────────────────────────
        // Creates a tapered cylindrical lattice of thin struts
        private static Mesh BuildNet(float topRadius, float height, int radialSeg, int rings, float taperRatio)
        {
            if (rings < 2) rings = 4;
            if (radialSeg < 6) radialSeg = 8;
            float botRadius = topRadius * taperRatio;
            float strandThick = topRadius * 0.03f;
            if (strandThick < 0.005f) strandThick = 0.005f;

            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            // Points along net rings
            Vector3[,] grid = new Vector3[rings + 1, radialSeg];
            for (int r = 0; r <= rings; r++)
            {
                float frac = (float)r / rings;
                float y = -height * frac;
                float rad = Mathf.Lerp(topRadius, botRadius, frac);
                for (int s = 0; s < radialSeg; s++)
                {
                    float a = Mathf.PI * 2f * s / radialSeg;
                    grid[r, s] = new Vector3(Mathf.Cos(a) * rad, y, Mathf.Sin(a) * rad);
                }
            }

            // Vertical strands
            for (int s = 0; s < radialSeg; s++)
                for (int r = 0; r < rings; r++)
                    AddStrand(v, t, grid[r, s], grid[r + 1, s], strandThick);

            // Horizontal ring strands
            for (int r = 0; r <= rings; r++)
                for (int s = 0; s < radialSeg; s++)
                    AddStrand(v, t, grid[r, s], grid[r, (s + 1) % radialSeg], strandThick);

            // Diagonal strands for realism
            for (int r = 0; r < rings; r++)
                for (int s = 0; s < radialSeg; s++)
                    AddStrand(v, t, grid[r, s], grid[r + 1, (s + 1) % radialSeg], strandThick);

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Prism (box-like) ────────────────────────────────────
        private static Mesh BuildPrism(float w, float h, float d)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, h/2f, 0), new Vector3(w, h, d));
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── House ───────────────────────────────────────────────
        private static Mesh BuildHouse(float w, float h, float d)
        {
            var m = new Mesh();
            float roofH = h * 0.4f;
            float wallH = h - roofH;
            m.vertices = new Vector3[]
            {
                new(-w/2,0,-d/2), new(w/2,0,-d/2), new(w/2,0,d/2), new(-w/2,0,d/2),
                new(-w/2,wallH,-d/2), new(w/2,wallH,-d/2), new(w/2,wallH,d/2), new(-w/2,wallH,d/2),
                new(-w/2,h,0), new(w/2,h,0)
            };
            m.triangles = new int[]
            {
                0,2,1, 0,3,2,
                0,1,5, 0,5,4,
                3,6,2, 3,7,6,
                0,4,7, 0,7,3,
                1,2,6, 1,6,5,
                4,5,9, 4,9,8,
                7,8,9, 7,9,6,
                4,8,7,
                5,6,9
            };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Frustum (tapered cylinder) ──────────────────────────
        private static Mesh BuildFrustum(float botR, float topR, float h, int seg)
        {
            if (topR <= 0) topR = botR * 0.5f;
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            v.Add(Vector3.zero);           // 0 = bottom centre
            v.Add(new Vector3(0, h, 0));   // 1 = top centre

            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * 2f * i / seg;
                v.Add(new Vector3(Mathf.Cos(a)*botR, 0, Mathf.Sin(a)*botR));
                v.Add(new Vector3(Mathf.Cos(a)*topR, h, Mathf.Sin(a)*topR));
            }

            for (int i = 0; i < seg; i++)
            {
                int b0 = 2+i*2, b1 = 2+((i+1)%seg)*2;
                int t0 = b0+1, t1 = b1+1;
                t.Add(0);t.Add(b1);t.Add(b0);
                t.Add(1);t.Add(t0);t.Add(t1);
                t.Add(b0);t.Add(b1);t.Add(t1);
                t.Add(b0);t.Add(t1);t.Add(t0);
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Cross / Plus shape ──────────────────────────────────
        private static Mesh BuildCross(float w, float h, float d, float thick)
        {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, h/2f, 0), new Vector3(thick, h, d));
            AddBox(v, t, new Vector3(0, h * 0.7f, 0), new Vector3(w, thick, d));
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Barrel (bulging cylinder) ───────────────────────────
        private static Mesh BuildBarrel(float radius, float h, int seg)
        {
            int vRings = 8;
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();

            v.Add(Vector3.zero);               // 0 = bottom centre
            v.Add(new Vector3(0, h, 0));       // 1 = top centre

            for (int ring = 0; ring <= vRings; ring++)
            {
                float frac = (float)ring / vRings;
                float y = h * frac;
                // Bulge: max at centre, 0.85× at top/bottom
                float bulge = 0.85f + 0.15f * Mathf.Sin(frac * Mathf.PI);
                float r = radius * bulge;
                for (int i = 0; i <= seg; i++)
                {
                    float a = Mathf.PI * 2f * i / seg;
                    v.Add(new Vector3(Mathf.Cos(a)*r, y, Mathf.Sin(a)*r));
                }
            }

            // Bottom cap
            for (int i = 0; i < seg; i++)
            {
                t.Add(0); t.Add(2 + (i+1)%(seg+1)); t.Add(2 + i);
            }
            // Top cap
            int topRingStart = 2 + vRings * (seg + 1);
            for (int i = 0; i < seg; i++)
            {
                t.Add(1); t.Add(topRingStart + i); t.Add(topRingStart + (i+1)%(seg+1));
            }
            // Side quads
            for (int ring = 0; ring < vRings; ring++)
            {
                int rStart = 2 + ring * (seg + 1);
                int nStart = rStart + seg + 1;
                for (int i = 0; i < seg; i++)
                {
                    int c = rStart + i, cn = rStart + i + 1;
                    int n = nStart + i, nn = nStart + i + 1;
                    t.Add(c);t.Add(n);t.Add(cn);
                    t.Add(cn);t.Add(n);t.Add(nn);
                }
            }

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        // ── Pole (thin cylinder helper) ─────────────────────────
        private static Mesh BuildPole(float radius, float h, int seg)
        {
            return BuildFrustum(radius, radius, h, seg);
        }

        // ──────────────────── HELPERS ────────────────────────────

        /// <summary>Adds an axis-aligned box to the mesh lists.</summary>
        private static void AddBox(List<Vector3> v, List<int> t, Vector3 center, Vector3 size)
        {
            int b = v.Count;
            float hx = size.x/2f, hy = size.y/2f, hz = size.z/2f;
            v.Add(center + new Vector3(-hx,-hy,-hz));
            v.Add(center + new Vector3( hx,-hy,-hz));
            v.Add(center + new Vector3( hx,-hy, hz));
            v.Add(center + new Vector3(-hx,-hy, hz));
            v.Add(center + new Vector3(-hx, hy,-hz));
            v.Add(center + new Vector3( hx, hy,-hz));
            v.Add(center + new Vector3( hx, hy, hz));
            v.Add(center + new Vector3(-hx, hy, hz));

            int[] faces = {
                0,2,1, 0,3,2, 4,6,5, 4,7,6,
                3,2,6, 3,6,7, 0,5,1, 0,4,5,
                0,7,4, 0,3,7, 1,6,2, 1,5,6
            };
            for (int i = 0; i < faces.Length; i++)
                t.Add(b + faces[i]);
        }

        /// <summary>Adds a thin rectangular strand between two points.</summary>
        private static void AddStrand(List<Vector3> v, List<int> t, Vector3 a, Vector3 b, float thick)
        {
            Vector3 dir = (b - a).normalized;
            Vector3 up = (Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.99f)
                ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(dir, up).normalized * thick * 0.5f;
            Vector3 fwd = Vector3.Cross(right, dir).normalized * thick * 0.5f;

            int idx = v.Count;
            v.Add(a - right - fwd);
            v.Add(a + right - fwd);
            v.Add(a + right + fwd);
            v.Add(a - right + fwd);
            v.Add(b - right - fwd);
            v.Add(b + right - fwd);
            v.Add(b + right + fwd);
            v.Add(b - right + fwd);

            int[] faces = {
                0,2,1, 0,3,2, 4,6,5, 4,7,6,
                3,2,6, 3,6,7, 0,5,1, 0,4,5,
                0,7,4, 0,3,7, 1,6,2, 1,5,6
            };
            for (int i = 0; i < faces.Length; i++)
                t.Add(idx + faces[i]);
        
        }


        private static Mesh BuildRadioTower() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            
            float h = 10f; float w = 2f;
            AddBox(v, t, new Vector3(-w/2, h/2, -w/2), new Vector3(0.2f, h, 0.2f));
            AddBox(v, t, new Vector3(w/2, h/2, -w/2), new Vector3(0.2f, h, 0.2f));
            AddBox(v, t, new Vector3(-w/2, h/2, w/2), new Vector3(0.2f, h, 0.2f));
            AddBox(v, t, new Vector3(w/2, h/2, w/2), new Vector3(0.2f, h, 0.2f));
            for(int i=0; i<5; i++) {
                float y = (i+1) * (h/6);
                AddBox(v, t, new Vector3(0, y, -w/2), new Vector3(w, 0.1f, 0.1f));
                AddBox(v, t, new Vector3(0, y, w/2), new Vector3(w, 0.1f, 0.1f));
                AddBox(v, t, new Vector3(-w/2, y, 0), new Vector3(0.1f, 0.1f, w));
                AddBox(v, t, new Vector3(w/2, y, 0), new Vector3(0.1f, 0.1f, w));
            }
            AddBox(v, t, new Vector3(0, h+1, 0), new Vector3(0.1f, 2f, 0.1f));
        
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBunker() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            
            AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(6f, 3f, 6f));
            AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(5f, 1f, 5f));
            AddBox(v, t, new Vector3(0, 2f, 3.1f), new Vector3(3f, 0.5f, 0.5f)); // viewing slit
        
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWindmill() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(1.6f, 1f, 1.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildLighthouse() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(1.6f, 1f, 1.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCastle() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(3f, 3f, 3f));
AddBox(v, t, new Vector3(0, 3.75f, 0), new Vector3(2.4000000000000004f, 1.5f, 2.4000000000000004f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSkyscraper() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            
            AddBox(v, t, new Vector3(0, 10f, 0), new Vector3(8f, 20f, 8f)); // base
            AddBox(v, t, new Vector3(0, 25f, 0), new Vector3(6f, 10f, 6f)); // mid
            AddBox(v, t, new Vector3(0, 32f, 0), new Vector3(4f, 4f, 4f));  // top
            AddBox(v, t, new Vector3(0, 36f, 0), new Vector3(0.5f, 4f, 0.5f)); // antenna
        
            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCabin() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.25f, 0), new Vector3(4.5f, 4.5f, 4.5f));
AddBox(v, t, new Vector3(0, 5.625f, 0), new Vector3(3.6f, 2.25f, 3.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTent() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.75f, 0), new Vector3(1.5f, 1.5f, 1.5f));
AddBox(v, t, new Vector3(0, 1.875f, 0), new Vector3(1.2000000000000002f, 0.75f, 1.2000000000000002f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBridge() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.75f, 0), new Vector3(5.5f, 5.5f, 5.5f));
AddBox(v, t, new Vector3(0, 6.875f, 0), new Vector3(4.4f, 2.75f, 4.4f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFactory() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(5f, 5f, 5f));
AddBox(v, t, new Vector3(0, 6.25f, 0), new Vector3(4f, 2.5f, 4f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWarehouse() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.25f, 0), new Vector3(4.5f, 4.5f, 4.5f));
AddBox(v, t, new Vector3(0, 5.625f, 0), new Vector3(3.6f, 2.25f, 3.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBarn() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.25f, 0), new Vector3(4.5f, 4.5f, 4.5f));
AddBox(v, t, new Vector3(0, 5.625f, 0), new Vector3(3.6f, 2.25f, 3.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSilo() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 2.25f, 0), new Vector3(4.5f, 4.5f, 4.5f));
AddBox(v, t, new Vector3(0, 5.625f, 0), new Vector3(3.6f, 2.25f, 3.6f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWaterTower() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(3f, 3f, 3f));
AddBox(v, t, new Vector3(0, 3.75f, 0), new Vector3(2.4000000000000004f, 1.5f, 2.4000000000000004f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCar() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTruck() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBus() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildVan() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSUV() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMotorcycle() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBicycle() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTrain() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCarriage() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTank() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildJeep() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildHelicopter() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildAirplane() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBoat() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSubmarine() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 1f, 4f));
AddBox(v, t, new Vector3(0, 1.5f, 0), new Vector3(1.8f, 1f, 2f));
AddBox(v, t, new Vector3(1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, 1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));
AddBox(v, t, new Vector3(-1f, 0.2f, -1.5f), new Vector3(0.4f, 0.8f, 0.8f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSword() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDagger() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildAxeWeapon() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSpear() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBow() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCrossbow() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPistol() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildRifle() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildShotgun() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSniperRifle() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildRocketLauncher() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildShield() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBomb() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMine() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTurret() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0, 0), new Vector3(0.1f, 0.5f, 0.1f));
AddBox(v, t, new Vector3(0, 0.3f, 0), new Vector3(0.6f, 0.1f, 0.2f));
AddBox(v, t, new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2f, 0.05f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildChair() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTable() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDesk() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBed() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSofa() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBookshelf() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCabinet() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWardrobe() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDresser() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildNightstand() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildStool() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBench() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTVStand() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDiningTable() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCoffeeTable() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(2f, 0.2f, 2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, -0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(-0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));
AddBox(v, t, new Vector3(0.9f, 0.2f, 0.9f), new Vector3(0.2f, 0.4f, 0.2f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTV() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMonitor() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildKeyboard() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.60f, 0.30f, 1.00f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.30f, 1.00f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildComputerMouse() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.30f, 0.10f, 0.90f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.10f, 0.90f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildLaptop() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPC() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.60f, 0.30f, 1.00f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.30f, 1.00f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPhone() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.70f, 0.30f, 0.90f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.30f, 0.90f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.90f, 0.50f, 0.10f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.50f, 0.10f, 0.70f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.10f, 0.70f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTablet() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPrinter() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSpeaker() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMicrophone() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCamera() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildRouter() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildController() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDrone() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPineTree() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildOakTree() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPalmTree() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDeadTree() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBush() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFern() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCactus() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMushroom() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildRock() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBoulder() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildLog() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildStump() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFlower() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildGrassTuft() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildVine() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(0, 1f, 0), new Vector3(0.4f, 2f, 0.4f));
AddBox(v, t, new Vector3(0, 2.5f, 0), new Vector3(2f, 2f, 2f));
AddBox(v, t, new Vector3(0, 3.5f, 0), new Vector3(1f, 1.5f, 1f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFridge() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildStove() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(1.00f, 0.90f, 0.80f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.90f, 0.80f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.80f, 0.70f, 0.60f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.70f, 0.60f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMicrowave() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildOven() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDishwasher() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.90f, 0.10f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.10f, 0.30f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWasher() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDryer() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildToaster() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBlender() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCoffeeMaker() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildVacuum() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildIron() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFan() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.60f, 0.30f, 1.00f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.30f, 1.00f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildAirConditioner() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildHeater() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildHammer() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.90f, 0.10f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.10f, 0.30f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWrench() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildScrewdriver() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSaw() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(1.00f, 0.90f, 0.80f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.90f, 0.80f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.80f, 0.70f, 0.60f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.70f, 0.60f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDrill() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.60f, 0.90f, 0.20f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.90f, 0.20f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPliers() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildShovel() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildRake() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPickaxe() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(1.00f, 0.90f, 0.80f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.90f, 0.80f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.80f, 0.70f, 0.60f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.70f, 0.60f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildAxeTool() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCrowbar() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTapeMeasure() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.70f, 0.30f, 0.90f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.30f, 0.90f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.90f, 0.50f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildLevel() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTrowel() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildToolbox() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.60f, 0.90f, 0.20f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.90f, 0.20f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildApple() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.30f, 0.10f, 0.90f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.10f, 0.90f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBanana() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBurger() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPizza() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.70f, 0.30f, 0.90f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.30f, 0.90f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.90f, 0.50f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildHotdog() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.60f, 0.90f, 0.20f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.90f, 0.20f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBread() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCheese() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(1.00f, 0.90f, 0.80f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.90f, 0.80f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.80f, 0.70f, 0.60f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.70f, 0.60f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.60f, 0.50f, 0.40f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.50f, 0.40f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCake() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPie() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.70f, 0.30f, 0.90f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.30f, 0.90f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.90f, 0.50f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDonut() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.90f, 0.10f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.10f, 0.30f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildIceCream() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.60f, 0.70f, 0.80f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.70f, 0.80f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSteak() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.70f, 0.10f, 0.50f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.10f, 0.50f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildChicken() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildFish() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCan() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.50f, 0.90f, 0.30f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.90f, 0.30f, 0.70f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.30f, 0.70f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCrate() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildCardboardBox() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.60f, 0.10f, 0.60f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.10f, 0.60f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildChest() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSafe() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.40f, 0.70f, 1.00f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.70f, 1.00f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(1.00f, 0.30f, 0.60f));
AddBox(v, t, new Vector3(0.80f, 1.90f, -0.40f), new Vector3(0.30f, 0.60f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTrashCan() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.90f, 0.70f, 0.50f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.70f, 0.50f, 0.30f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.50f, 0.30f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildDumpster() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.90f, 0.10f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.10f, 0.30f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildStreetlight() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.60f, 0.30f, 1.00f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.30f, 1.00f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildTrafficLight() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.80f, 0.50f, 0.20f));
AddBox(v, t, new Vector3(0.40f, 0.70f, 0.80f), new Vector3(0.50f, 0.20f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.20f, 0.90f, 0.60f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.90f, 0.60f, 0.30f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildHydrant() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));
AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.10f, 0.10f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildMailbox() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.70f, 0.30f, 0.90f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.30f, 0.90f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.90f, 0.50f, 0.10f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildSign() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildWoodenFence() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.60f, 0.70f, 0.80f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.70f, 0.80f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildBrickWall() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.20f, 0.30f, 0.40f));
AddBox(v, t, new Vector3(-0.80f, 1.10f, 0.40f), new Vector3(0.30f, 0.40f, 0.50f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.40f, 0.50f, 0.60f));
AddBox(v, t, new Vector3(-0.40f, 2.30f, -0.80f), new Vector3(0.50f, 0.60f, 0.70f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.60f, 0.70f, 0.80f));
AddBox(v, t, new Vector3(0.00f, 1.50f, 0.00f), new Vector3(0.70f, 0.80f, 0.90f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }


        private static Mesh BuildPillar() {
            var m = new Mesh();
            var v = new List<Vector3>();
            var t = new List<int>();
            AddBox(v, t, new Vector3(-1.00f, 0.50f, -1.00f), new Vector3(0.30f, 0.50f, 0.70f));
AddBox(v, t, new Vector3(-0.60f, 1.70f, -0.20f), new Vector3(0.50f, 0.70f, 0.90f));
AddBox(v, t, new Vector3(-0.20f, 0.90f, 0.60f), new Vector3(0.70f, 0.90f, 0.10f));
AddBox(v, t, new Vector3(0.20f, 2.10f, -0.60f), new Vector3(0.90f, 0.10f, 0.30f));
AddBox(v, t, new Vector3(0.60f, 1.30f, 0.20f), new Vector3(0.10f, 0.30f, 0.50f));

            m.SetVertices(v); m.SetTriangles(t, 0);
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
        private static Mesh BuildWoodenBarrel() {
            var m = new Mesh(); var v = new List<Vector3>(); var t = new List<int>();
            AddBox(v, t, new Vector3(0, 0.5f, 0), new Vector3(1f, 1f, 1f));
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds(); return m;
        }
    }
}
