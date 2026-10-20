using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Environment;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Places existing visual prefabs and generated fittings around the W2 dock/boat.
    /// Original geometry, markers, gameplay scenes and source prefabs are preserved.</summary>
    public static class BeachDressingBuilder
    {
        private const string Label = "Dress Beach and Boat";
        private const string CoilPath = "Assets/Environment/BeachRopeCoil.asset";
        private const string MooringPath = "Assets/Environment/BeachMooringRopes.asset";
        private const string LanternMaterialPath = "Assets/Materials/Environment/BeachLantern.mat";
        private const string PlayerSettingsPath = "Assets/Prefabs/Player/PlayerMovementSettings.asset";
        private const string PrefabFolder = "Assets/Prefabs/Props/";
        public const string LanternPath = "Boat/BoatDressing/BoatDressing_LanternGlass";

        [MenuItem("Blackout/World/Dress Beach and Boat")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Dress beach outside Play mode.");
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Transform beach = WorldSceneSkeleton.FindRoot(scene, "Beach").transform;
            Transform boat = beach.Find("Boat");
            if (boat == null || beach.Find("Dock") == null) throw new InvalidOperationException("Build the W2 beach first.");
            var dock = new Parts(GeneratedObjects.FindOrCreateChild(beach, "BeachDressing", Label), true);
            var fittings = new Parts(GeneratedObjects.FindOrCreateChild(boat, "BoatDressing", Label), false);
            Material metal = LoadMaterial(MaterialLibraryBuilder.MaterialPath("MetalPlate"));
            Material rope = LoadMaterial(MaterialLibraryBuilder.MaterialPath("Fabric"));
            Material wood = LoadMaterial(GreyboxMaterials.Folder + "/Greybox_DockWood.mat");
            Material trim = LoadMaterial(GreyboxMaterials.Folder + "/Greybox_BoatTrim.mat");
            Material dark = LoadMaterial(GreyboxMaterials.Folder + "/Greybox_BoatDark.mat");
            Mesh coil = BeachRopeMesh.Coil(CoilPath);

            // Reserve x +/-0.6 for the mission route: shift the reference cargo slightly toward each edge.
            dock.Prefab("CrateBottom", "Prop_CrateSmall", new Vector3(-0.95f, 1.6f, -100f), 0f, Vector3.one * 0.6f);
            dock.Prefab("CrateTop", "Prop_CrateSmall", new Vector3(-0.95f, 2.125f, -100.1f), -22.92f, Vector3.one * 0.45f);
            dock.Prefab("FuelBarrel", "Prop_Barrel", new Vector3(0.95f, 1.3f, -101f), 0f, Vector3.one);
            UnityEngine.Terrain terrain = WorldSceneSkeleton.FindRoot(scene, "Terrain").transform.Find("IslandTerrain").GetComponent<UnityEngine.Terrain>();
            float ground = terrain.SampleHeight(new Vector3(-2.6f, 0f, -95.6f)) + terrain.transform.position.y;
            dock.Cylinder("SignPole", new Vector3(-2.6f, ground + 1.2f, -95.6f), 0.1f, 2.4f, metal, true);
            dock.Prefab("PrivateIslandSign", "Prop_Sign_PrivateIsland", new Vector3(-2.6f, ground + 2.05f, -95.6f), 165.6f, Vector3.one);
            for (int i = 0; i < 4; i++) dock.Mesh("RopeCoil" + i, coil,
                new Vector3(-0.7f, 1.35f + i * 0.06f, -112f), Vector3.one * (1f - i * 0.12f), rope);
            foreach (float z in new[] { -125f, -120f })
                dock.Box("Cleat" + z, new Vector3(1.15f, 1.35f, z), new Vector3(0.35f, 0.08f, 0.1f), metal);
            var mooring = new BeachRopeMesh();
            foreach (int side in new[] { -1, 1 })
            {
                Vector3 start = new Vector3(1.15f, 1.35f, IslandLayout.BoatPosition.z + side * 2.5f);
                Vector3 end = boat.TransformPoint(new Vector3(-1.35f, 1.05f, side * 2f));
                var points = new Vector3[11];
                for (int i = 0; i < points.Length; i++)
                {
                    float t = i / 10f;
                    points[i] = Vector3.Lerp(start, end, t) - Vector3.up * (Mathf.Sin(t * Mathf.PI) * 0.35f);
                }
                mooring.AddTube(points, 0.025f);
            }
            dock.Mesh("MooringRopes", mooring.Save(MooringPath, "BeachMooringRopes"), Vector3.zero, Vector3.one, rope);

            // Boat-local coordinates negate the reference's local Z. No hull/cabin collider is replaced.
            fittings.Box("Deck", new Vector3(0f, 0.585f, 0.75f), new Vector3(2.6f, 0.07f, 6.7f), wood, true);
            foreach (int side in new[] { -1, 1 })
            {
                fittings.Box("SideWindow" + side, new Vector3(side * 1.011f, 1.825f, 0.225f), new Vector3(0.02f, 0.55f, 2.05f), dark);
                fittings.Box("Gunwale" + side, new Vector3(side * 1.4f, 1.17f, 1.1f), new Vector3(0.12f, 0.1f, 6.2f), trim);
                for (int i = 0; i < 6; i++) fittings.Cylinder("RailPost" + side + "_" + i,
                    new Vector3(side * 1.3f, 1.5f, 3.8f - i * 1.2f), 0.05f, 0.6f, trim);
                GameObject rail = fittings.Cylinder("RailTop" + side, new Vector3(side * 1.3f, 1.8f, 0.8f), 0.05f, 6f, trim);
                rail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            for (int i = 0; i < 2; i++)
            {
                float z = 3.2f - i * 0.25f;
                fittings.Box("PH_JerryCan" + i, new Vector3(0.8f, 0.845f, z), new Vector3(0.35f, 0.45f, 0.18f), rope);
                fittings.Box("JerryHandle" + i, new Vector3(0.8f, 1.1f, z), new Vector3(0.2f, 0.06f, 0.08f), metal);
            }
            for (int i = 0; i < 3; i++) fittings.Mesh("RopeCoil" + i, coil,
                new Vector3(-0.6f, 0.675f + i * 0.06f, 3.2f), Vector3.one * (1.1f - i * 0.15f), rope);
            fittings.Cylinder("LanternGlass", new Vector3(-1.2f, 2.45f, -1.7f), 0.205f, 0.245f, LanternMaterial());
            fittings.Cylinder("LanternCap", new Vector3(-1.2f, 2.63f, -1.7f), 0.28f, 0.08f, dark);
            fittings.Cylinder("LanternBase", new Vector3(-1.2f, 2.31f, -1.7f), 0.24f, 0.05f, dark);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * Mathf.PI * 0.5f;
                fittings.Cylinder("LanternCage" + i, new Vector3(-1.2f + Mathf.Cos(angle) * 0.11f, 2.45f,
                    -1.7f + Mathf.Sin(angle) * 0.11f), 0.016f, 0.27f, dark);
            }
            dock.Finish();
            fittings.Finish();
            Transform light = WorldSceneSkeleton.FindRoot(scene, LightingSetup.LightingRootName).transform.Find("OutdoorLights/OutdoorLights_BoatLantern");
            if (light == null) throw new InvalidOperationException("Run Setup Lighting to create the existing boat lantern light.");
            FlickerLight flicker = light.GetComponent<FlickerLight>();
            Undo.RecordObject(flicker, Label);
            flicker.Configure(flicker.Mode, flicker.Seed, FindLantern(scene));
            EditorUtility.SetDirty(flicker);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Beach dressed: {dock.Count} shore/dock parts and {fittings.Count} boat parts; "
                + "4 linked prefabs, 7 rope coils, 2 mooring lines; central1.2m route reserved; lantern uses existing flicker light.");
        }

        internal static Renderer FindLantern(Scene scene) => WorldSceneSkeleton.FindRoot(scene, "Beach")?.transform
            .Find(LanternPath)?.GetComponent<Renderer>();

        private static Material LoadMaterial(string path) => AssetDatabase.LoadAssetAtPath<Material>(path)
            ?? throw new InvalidOperationException("Missing environment material: " + path);

        private static Material LanternMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(LanternMaterialPath);
            bool create = material == null;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "BeachLantern" };
            ColorUtility.TryParseHtmlString("#FFD9A0", out Color colour);
            material.SetColor("_BaseColor", colour);
            material.SetColor("_EmissionColor", LightingLayout.FromLinearHex("#FFB050", 4f));
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BaseShaderGUI.SetMaterialKeywords(material);
            if (create) AssetDatabase.CreateAsset(material, LanternMaterialPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private sealed class Parts
        {
            private readonly Transform root;
            private readonly bool fixedInWorld;
            private readonly HashSet<string> built = new HashSet<string>();
            public int Count => built.Count;
            public Parts(Transform root, bool fixedInWorld) { this.root = root; this.fixedInWorld = fixedInWorld; }
            public void Prefab(string name, string source, Vector3 position, float yaw, Vector3 scale)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabFolder + source + ".prefab");
                if (prefab == null || BunkerDresser.HasDynamicContent(prefab)) throw new InvalidOperationException("Missing or dynamic visual prefab: " + source);
                string id = GeneratedObjects.Name(root, name);
                built.Add(id);
                Transform existing = root.Find(id);
                GameObject go;
                if (existing == null)
                {
                    go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.gameObject.scene);
                    go.name = id;
                    go.transform.SetParent(root, false);
                    Undo.RegisterCreatedObjectUndo(go, Label);
                }
                else
                {
                    go = existing.gameObject;
                    if (PrefabUtility.GetCorrespondingObjectFromSource(go) != prefab) throw new InvalidOperationException("Different source at " + id);
                }
                Place(go.transform, position, scale);
                go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.transform);
                foreach (Transform part in go.GetComponentsInChildren<Transform>(true))
                {
                    part.gameObject.layer = PhysicsLayers.Level;
                    GameObjectUtility.SetStaticEditorFlags(part.gameObject, StaticFlagRules.Environment);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(part.gameObject);
                }
            }
            public GameObject Box(string name, Vector3 center, Vector3 size, Material material, bool solid = false)
                => Primitive(name, PrimitiveType.Cube, center, size, material, solid);
            public GameObject Cylinder(string name, Vector3 center, float diameter, float height, Material material, bool solid = false)
                => Primitive(name, PrimitiveType.Cylinder, center, new Vector3(diameter, height * 0.5f, diameter), material, solid);
            private GameObject Primitive(string name, PrimitiveType type, Vector3 center, Vector3 size, Material material, bool solid)
            {
                GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label, type);
                Place(go.transform, center, size);
                go.GetComponent<Renderer>().sharedMaterial = material;
                go.GetComponent<Collider>().enabled = solid;
                go.layer = PhysicsLayers.Level;
                GameObjectUtility.SetStaticEditorFlags(go, fixedInWorld ? StaticFlagRules.Environment : 0);
                return go;
            }
            public void Mesh(string name, Mesh mesh, Vector3 center, Vector3 scale, Material material)
            {
                GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label);
                Place(go.transform, center, scale);
                GeneratedObjects.GetOrAddComponent<MeshFilter>(go).sharedMesh = mesh;
                GeneratedObjects.GetOrAddComponent<MeshRenderer>(go).sharedMaterial = material;
                go.layer = PhysicsLayers.Level;
                GameObjectUtility.SetStaticEditorFlags(go, fixedInWorld ? StaticFlagRules.SeeThrough : 0);
            }
            private static void Place(Transform part, Vector3 center, Vector3 scale)
            {
                Undo.RecordObject(part, Label);
                part.localPosition = center;
                part.localRotation = Quaternion.identity;
                part.localScale = scale;
            }
            public void Finish() => GeneratedObjects.RemoveStale(root, built);
        }

        public static void BuildAndVerify()
        {
            string[] sources = { "Prop_CrateSmall", "Prop_Barrel", "Prop_Sign_PrivateIsland" };
            string[] protectedPaths = sources.SelectMany(name => AssetDatabase.GetDependencies(PrefabFolder + name + ".prefab"))
                .Where(path => path.StartsWith("Assets/", StringComparison.Ordinal)).Distinct()
                .SelectMany(path => new[] { path, path + ".meta" }).Where(File.Exists)
                .Concat(new[] { PlayerSettingsPath, "Assets/Scenes/Interactables.unity", "Assets/Scenes/Agents.unity" }).ToArray();
            byte[][] protectedBytes = protectedPaths.Select(File.ReadAllBytes).ToArray();
            Build();
            string[] outputs = { WorldSceneSkeleton.ScenePath, CoilPath, MooringPath, LanternMaterialPath };
            byte[][] first = outputs.Select(File.ReadAllBytes).ToArray();
            Build();
            for (int i = 0; i < outputs.Length; i++)
                if (!first[i].SequenceEqual(File.ReadAllBytes(outputs[i]))) throw new InvalidOperationException("Repeat mismatch: " + outputs[i]);
            VerifyDockRoute();
            NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
            for (int i = 0; i < protectedPaths.Length; i++)
                if (!protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i]))) throw new InvalidOperationException("Protected file changed: " + protectedPaths[i]);
            Debug.Log($"[World] Beach verification passed: scene/2 meshes/material repeat identical; {protectedPaths.Length} protected files unchanged; original route/exit preserved.");
        }

        private static void VerifyDockRoute()
        {
            using var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PlayerSettingsPath));
            var probe = new GameObject("W13_TemporaryDockPlayerProbe");
            try
            {
                probe.layer = PhysicsLayers.Player;
                probe.transform.position = new Vector3(0f, 1.35f, -121f);
                var controller = probe.AddComponent<CharacterController>();
                controller.height = settings.FindProperty("height").floatValue;
                controller.radius = settings.FindProperty("radius").floatValue;
                controller.center = Vector3.up * controller.height * 0.5f;
                controller.stepOffset = settings.FindProperty("stepOffset").floatValue;
                controller.slopeLimit = settings.FindProperty("slopeLimit").floatValue;
                controller.minMoveDistance = 0f;
                Physics.SyncTransforms();
                foreach (float z in new[] { -97.5f, -124f })
                {
                    int moves = 0;
                    for (; moves < 1200 && Mathf.Abs(probe.transform.position.z - z) > 0.04f; moves++)
                        controller.Move(new Vector3(0f, -0.05f, Mathf.Clamp(z - probe.transform.position.z, -0.03f, 0.03f)));
                    if (moves == 1200 || Mathf.Abs(probe.transform.position.y - 1.3f) > 0.1f)
                        throw new InvalidOperationException("Dock route blocked at " + probe.transform.position);
                }
                Debug.Log("[World] Dock player route passed: PlayerStart -> dock landing past cargo -> BoatExit; saved player dimensions, no gameplay events.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }
    }
}
