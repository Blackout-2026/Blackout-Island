using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>
    /// Greybox beach for Build Terrain, under the scene root "Beach":
    /// - Water: a 520 x 520 m transparent plane at y 0 (reference: ocean plane centred on z 20), no collider.
    /// - Dock: deck x -1.4 .. 1.4, z -128 .. -97, top at y 1.3; posts every 3 m down into the sea bed; rails
    ///   0.8 - 0.9 m above the deck, with a gap on the east side where the boat is moored.
    /// - Boat: primitives at the prototype's sizes, at (3.7, 0.15, -122.5) with yaw -2.9 degrees. Bow to the
    ///   south (local -Z), stern toward the shore, fenders on the dock side (local -X). The deck is at y 0.7.
    /// Placeholder materials (URP Lit, palette colours) go in Assets/Materials/Environment/Greybox and are created
    /// only if missing; the W5 material library replaces them. Dock and boat parts are reused by name
    /// (<see cref="GeneratedObjects"/>), so a re-run leaves the scene unchanged.
    /// </summary>
    public static class BeachGreyboxBuilder
    {
        public const string MaterialFolder = "Assets/Materials/Environment/Greybox";
        private const string UndoLabel = "Build Terrain";

        // Ocean plane (Unity's Plane primitive is 10 x 10 m).
        private const float WaterSize = 520f;
        private const float WaterCentreZ = 20f;

        // Dock posts and rails.
        private const float PostSpacing = 3f;
        private const float PostDiameter = 0.3f;
        private const float PostBottomY = -4f;
        private const float RailTopAboveDeck = 0.9f;
        private const float BollardAboveDeck = 0.25f; // short posts beside the boat, where the rail has a gap
        private const float RailSize = 0.1f;
        private const float BoatGapHalfLength = 4.5f; // east rail is left open this far either side of the boat

        public static string Build(Transform beachRoot)
        {
            Material water = GetOrCreateMaterial("Greybox_Water", "#1A2840", 0.95f, alpha: 0.85f);
            Material deck = GetOrCreateMaterial("Greybox_DockWood", "#6A5038", 0.1f);
            Material wood = GetOrCreateMaterial("Greybox_DockWoodDark", "#3B2D20", 0.1f);
            Material hull = GetOrCreateMaterial("Greybox_BoatHull", "#3B4A52", 0.2f, metallic: 0.35f);
            Material trim = GetOrCreateMaterial("Greybox_BoatTrim", "#8A867C", 0.4f);
            Material dark = GetOrCreateMaterial("Greybox_BoatDark", "#2A3A40", 0.8f);

            BuildWater(beachRoot, water);
            int dockParts = BuildDock(beachRoot, deck, wood);
            int boatParts = BuildBoat(beachRoot, hull, trim, dark);
            return $"water {WaterSize} x {WaterSize} m, dock {dockParts} parts, boat {boatParts} parts";
        }

        private static void BuildWater(Transform beachRoot, Material material)
        {
            Transform water = beachRoot.Find("Water");
            if (water == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Plane);
                go.name = "Water";
                go.transform.SetParent(beachRoot, false);
                Object.DestroyImmediate(go.GetComponent<Collider>()); // nobody walks on the water
                Undo.RegisterCreatedObjectUndo(go, UndoLabel);
                water = go.transform;
            }

            water.SetPositionAndRotation(new Vector3(0f, IslandLayout.WaterY, WaterCentreZ), Quaternion.identity);
            water.localScale = new Vector3(WaterSize / 10f, 1f, WaterSize / 10f);
            var meshRenderer = water.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        }

        private static int BuildDock(Transform beachRoot, Material deckMaterial, Material woodMaterial)
        {
            Transform dock = GeneratedObjects.FindOrCreateChild(beachRoot, "Dock", UndoLabel);
            dock.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            dock.localScale = Vector3.one;
            var built = new HashSet<string>();

            float deckY = IslandLayout.DockDeckY;
            float halfWidth = IslandLayout.DockHalfWidth;
            float length = IslandLayout.DockMaxZ - IslandLayout.DockMinZ;
            Box(dock, "Deck", built, new Vector3(0f, deckY - 0.175f, IslandLayout.DockMaxZ - length * 0.5f),
                new Vector3(halfWidth * 2f, 0.35f, length), deckMaterial, true);

            // Posts from 1 m in from the shore end to the sea end, both sides; rails join neighbouring posts.
            // An east post with no east rail on either side is a short mooring bollard.
            float postX = halfWidth + 0.1f;
            float railY = deckY + RailTopAboveDeck - RailSize * 0.5f;
            var railSize = new Vector3(RailSize, RailSize, PostSpacing);
            int index = 0;
            for (float z = IslandLayout.DockMaxZ - 1f; z >= IslandLayout.DockMinZ - 0.01f; z -= PostSpacing, index++)
            {
                bool atSeaEnd = z <= IslandLayout.DockMinZ + 0.01f; // the end rail meets this post
                bool bollard = !atSeaEnd && !HasEastRail(z + PostSpacing * 0.5f) && !HasEastRail(z - PostSpacing * 0.5f);
                Post(dock, $"Post_W{index:00}", built, -postX, z, deckY + RailTopAboveDeck, woodMaterial);
                Post(dock, $"Post_E{index:00}", built, postX, z, deckY + (bollard ? BollardAboveDeck : RailTopAboveDeck), woodMaterial);

                float railZ = z - PostSpacing * 0.5f;
                if (atSeaEnd)
                {
                    continue;
                }

                Box(dock, $"Rail_W{index:00}", built, new Vector3(-postX, railY, railZ), railSize, woodMaterial, true);
                if (HasEastRail(railZ))
                {
                    Box(dock, $"Rail_E{index:00}", built, new Vector3(postX, railY, railZ), railSize, woodMaterial, true);
                }
            }

            Box(dock, "Rail_End", built, new Vector3(0f, deckY + RailTopAboveDeck - RailSize * 0.5f, IslandLayout.DockMinZ),
                new Vector3(postX * 2f, RailSize, RailSize), woodMaterial, true);

            GeneratedObjects.RemoveStale(dock, built);
            return built.Count;
        }

        // Boat parts in boat-local metres: the prototype's numbers with local z negated (bow at -Z).
        private static int BuildBoat(Transform beachRoot, Material hull, Material trim, Material dark)
        {
            Transform boat = GeneratedObjects.FindOrCreateChild(beachRoot, "Boat", UndoLabel);
            boat.SetPositionAndRotation(IslandLayout.BoatPosition, Quaternion.Euler(0f, IslandLayout.BoatYaw, 0f));
            boat.localScale = Vector3.one;
            var built = new HashSet<string>();

            // Hull from the stern (z 4.2) to the bow; the bow is a box turned 45 degrees so it comes to a point.
            Box(boat, "Hull", built, new Vector3(0f, -0.225f, 0.8f), new Vector3(2.9f, 1.55f, 6.8f), hull, true);
            Box(boat, "Bow", built, new Vector3(0f, -0.225f, -2.6f), new Vector3(2.05f, 1.55f, 2.05f), hull, true, 45f);
            Box(boat, "Side_West", built, new Vector3(-1.4f, 0.875f, 0.8f), new Vector3(0.1f, 0.65f, 6.8f), hull, true);
            Box(boat, "Side_East", built, new Vector3(1.4f, 0.875f, 0.8f), new Vector3(0.1f, 0.65f, 6.8f), hull, true);
            Box(boat, "Transom", built, new Vector3(0f, 0.875f, 4.15f), new Vector3(2.9f, 0.65f, 0.1f), hull, true);

            Box(boat, "Cabin", built, new Vector3(0f, 1.4f, 0.2f), new Vector3(2f, 1.7f, 2.4f), trim, true);
            Box(boat, "CabinRoof", built, new Vector3(0f, 2.3f, 0.2f), new Vector3(2.16f, 0.1f, 2.8f), dark, true);
            Box(boat, "Windshield", built, new Vector3(0f, 1.85f, -1.01f), new Vector3(2.02f, 0.6f, 0.04f), dark, false);

            Box(boat, "Motor", built, new Vector3(0f, 1f, 4.55f), new Vector3(0.55f, 0.8f, 0.5f), dark, false);
            Box(boat, "MotorShaft", built, new Vector3(0f, 0.25f, 4.55f), new Vector3(0.14f, 1f, 0.2f), dark, false);
            Cylinder(boat, "Mast", built, new Vector3(0.4f, 3f, 0.6f), 0.1f, 1.4f, trim);
            Box(boat, "Radar", built, new Vector3(0.4f, 3.5f, 0.6f), new Vector3(0.9f, 0.08f, 0.16f), dark, false);

            // Lantern on the dock side; W7 adds its light (reference: (2.5, 2.75, -124.2) in world space).
            Cylinder(boat, "LanternPost", built, new Vector3(-1.2f, 1.75f, -1.7f), 0.06f, 1.1f, trim);
            Cylinder(boat, "Lantern", built, new Vector3(-1.2f, 2.45f, -1.7f), 0.2f, 0.24f, trim);

            float[] fenderZ = { 2.5f, 0.5f, -1.5f };
            for (int i = 0; i < fenderZ.Length; i++)
            {
                Cylinder(boat, $"Fender_{i}", built, new Vector3(-1.48f, 0.75f, fenderZ[i]), 0.26f, 0.55f, trim);
            }

            GeneratedObjects.RemoveStale(boat, built);
            return built.Count;
        }

        // The east rail has a gap where the boat is moored, so the player can step across to it.
        private static bool HasEastRail(float railZ)
        {
            return Mathf.Abs(railZ - IslandLayout.BoatPosition.z) >= BoatGapHalfLength;
        }

        private static void Post(Transform dock, string part, ISet<string> built, float x, float z, float topY, Material material)
        {
            float height = topY - PostBottomY;
            Cylinder(dock, part, built, new Vector3(x, PostBottomY + height * 0.5f, z), PostDiameter, height, material, true);
        }

        private static GameObject Box(Transform container, string part, ISet<string> built, Vector3 localCentre, Vector3 size,
            Material material, bool solid, float yaw = 0f)
        {
            GameObject go = Part(container, part, built, PrimitiveType.Cube, localCentre, size, material, yaw);
            SetCollider<BoxCollider>(go, solid);
            return go;
        }

        // Unity's cylinder is 1 m across and 2 m tall at scale 1.
        private static GameObject Cylinder(Transform container, string part, ISet<string> built, Vector3 localCentre,
            float diameter, float height, Material material, bool solid = false)
        {
            GameObject go = Part(container, part, built, PrimitiveType.Cylinder, localCentre,
                new Vector3(diameter, height * 0.5f, diameter), material, 0f);
            SetCollider<CapsuleCollider>(go, solid);
            return go;
        }

        private static GameObject Part(Transform container, string part, ISet<string> built, PrimitiveType type,
            Vector3 localCentre, Vector3 scale, Material material, float yaw)
        {
            GameObject go = GeneratedObjects.GetOrCreate(container, part, built, UndoLabel, type);
            go.transform.localPosition = localCentre;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            return go;
        }

        private static void SetCollider<T>(GameObject go, bool wanted) where T : Collider
        {
            var existing = go.GetComponent<T>();
            if (wanted && existing == null)
            {
                go.AddComponent<T>();
            }
            else if (!wanted && existing != null)
            {
                Object.DestroyImmediate(existing);
            }
        }

        // Created once with a palette colour; an existing material is left as it is (W5 replaces these).
        private static Material GetOrCreateMaterial(string materialName, string hex, float smoothness, float metallic = 0f, float alpha = 1f)
        {
            string path = $"{MaterialFolder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials/Environment"))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "Environment");
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials/Environment", "Greybox");
            }

            ColorUtility.TryParseHtmlString(hex, out Color colour);
            colour.a = alpha;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (alpha < 1f)
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f); // Alpha
            }

            // URP's own material setup: keywords, blend modes, ZWrite and render queue to match the properties above.
            BaseShaderGUI.SetMaterialKeywords(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
