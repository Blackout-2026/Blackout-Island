using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Reference security/corridor fittings, adapted to the frozen W1 dimensions.
    /// Owns only its named generated children and wet-floor material; no gameplay or prefab edits.</summary>
    public static class SecurityCorridorBuilder
    {
        private const string Label = "Build Security and Corridor Details";
        private const string Container = "SecurityCorridorDetails";
        private const string WetPath = "Assets/Materials/Environment/CorridorPuddle.mat";
        private const string PlayerSettingsPath = "Assets/Prefabs/Player/PlayerMovementSettings.asset";

        [MenuItem("Blackout/World/Build Security and Corridor Details")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Build security/corridor details outside Play mode.");
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Transform root = GeneratedObjects.FindOrCreateChild(
                WorldSceneSkeleton.FindRoot(scene, "Props").transform, Container, Label);
            var security = new Parts(GeneratedObjects.FindOrCreateChild(root, "SecurityDetails", Label));
            var corridor = new Parts(GeneratedObjects.FindOrCreateChild(root, "CorridorDetails", Label));
            BuildSecurity(security);
            BuildCorridor(corridor, WetMaterial());
            security.Finish();
            corridor.Finish();
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Security/corridor details built: {security.Count} security, {corridor.Count} corridor parts; "
                + "10 pillars, 2.4m clear between pillars, 1.86m detector gap; markers/prefabs unchanged.");
        }

        private static void BuildSecurity(Parts p)
        {
            // Thin wainscot skins leave the frozen ramp and door openings clear.
            foreach (float side in new[] { -1f, 1f })
            {
                string id = side < 0 ? "West" : "East";
                p.Box("Band" + id, new Vector3(side * 7.985f, 3f, 76f), new Vector3(0.03f, 1f, 12f), p.Metal, false);
                p.Box("BandNorth" + id, new Vector3(side * 4.35f, 3f, 81.985f), new Vector3(7.3f, 1f, 0.03f), p.Metal, false);
                p.Box("BandSouth" + id, new Vector3(side * 4.75f, 3f, 70.015f), new Vector3(6.5f, 1f, 0.03f), p.Metal, false);
            }
            p.Box("PartitionBase", new Vector3(4.425f, 2.975f, 75f), new Vector3(0.15f, 0.95f, 6f), p.Metal);
            p.Box("PartitionGlass", new Vector3(4.425f, 4.125f, 75f), new Vector3(0.07f, 1.35f, 6f), p.Glass);
            p.Box("PartitionTop", new Vector3(4.425f, 4.83f, 75f), new Vector3(0.19f, 0.06f, 6.04f), p.Metal);
            foreach (int z in new[] { 72, 75, 78 })
                p.Box("PartitionPost" + z, new Vector3(4.425f, 4.125f, z), new Vector3(0.13f, 1.35f, 0.06f), p.Metal);
            foreach (int side in new[] { -1, 1 })
                p.Box("DetectorPost" + side, new Vector3(side * 1.05f, 3.675f, 80.8f), new Vector3(0.24f, 2.35f, 0.4f), p.Metal);
            p.Box("DetectorHeader", new Vector3(0f, 4.95f, 80.8f), new Vector3(2.34f, 0.2f, 0.4f), p.Metal);
            p.Box("CheckpointCounter", new Vector3(-3f, 3.05f, 81.25f), new Vector3(2.4f, 1.1f, 0.7f), p.Metal);
            p.Box("CeilingTray", new Vector3(0f, 5.30f, 76f), new Vector3(14f, 0.05f, 0.3f), p.Metal);
            for (int i = 0; i < 4; i++)
                p.Pipe("CeilingCable" + i, new Vector3(-7f, 5.35f, 75.9f + i * 0.06f),
                    new Vector3(7f, 5.35f, 75.9f + i * 0.06f), 0.025f, p.Rust);
            p.Box("CeilingVent", new Vector3(-5.4f, 5.45f, 70.9f), new Vector3(1.2f, 0.06f, 0.6f), p.Metal);
            for (int i = 0; i < 6; i++)
                p.Box("VentSlat" + i, new Vector3(-5.9f + i * 0.2f, 5.405f, 70.9f), new Vector3(0.05f, 0.03f, 0.55f), p.Concrete, false);
            p.Box("RampStripe", new Vector3(0f, 2.503f, 70.4f), new Vector3(3f, 0.006f, 0.4f), p.Hazard, false);
            p.Box("PH_SecurityCamera", new Vector3(-7.6f, 5.08f, 81.6f), new Vector3(0.3f, 0.18f, 0.4f), p.Metal, false);
        }

        private static void BuildCorridor(Parts p, Material wet)
        {
            foreach (int side in new[] { -1, 1 })
            {
                p.Box("FloorLine" + side, new Vector3(side * 1.1f, 2.503f, 103f), new Vector3(0.1f, 0.006f, 41.2f), p.Hazard, false);
                foreach (int z in new[] { 88, 94, 100, 112, 118 })
                {
                    string id = side + "_" + z;
                    p.Box("Pillar" + id, new Vector3(side * 1.35f, 4.25f, z), new Vector3(0.3f, 2.5f, 0.64f), p.Concrete);
                    p.Box("PillarFoot" + id, new Vector3(side * 1.35f, 2.75f, z), new Vector3(0.3f, 0.5f, 0.68f), p.Hazard);
                }
            }
            p.Pipe("PipeMain", new Vector3(1.1f, 5.12f, 82.4f), new Vector3(1.1f, 5.12f, 123.6f), 0.1f, p.Metal);
            Vector3[] red = { new Vector3(0.78f, 5.28f, 82.4f), new Vector3(0.78f, 5.28f, 103f),
                new Vector3(0.78f, 4.95f, 103.5f), new Vector3(0.78f, 4.95f, 108.5f),
                new Vector3(0.78f, 5.28f, 109f), new Vector3(0.78f, 5.28f, 123.6f) };
            for (int i = 1; i < red.Length; i++) p.Pipe("PipeRust" + i, red[i - 1], red[i], 0.07f, p.Rust);
            p.Pipe("PipeYellow", new Vector3(-1.25f, 5.27f, 82.4f), new Vector3(-1.25f, 5.27f, 104f), 0.06f, p.Hazard);
            p.Pipe("PipeToGenerator", new Vector3(-1.25f, 5.27f, 104f), new Vector3(-1.49f, 5.27f, 104f), 0.06f, p.Hazard);
            for (int i = 0; i < 14; i++)
            {
                float z = 83.9f + i * 3f;
                p.Box("PipeBracket" + i, new Vector3(1.09f, 5.40f, z), new Vector3(0.76f, 0.1f, 0.06f), p.Metal);
                p.Box("PipeSupport" + i, new Vector3(1.45f, 5.25f, z), new Vector3(0.05f, 0.4f, 0.06f), p.Metal);
            }
            p.Box("CableTray", new Vector3(-0.65f, 5.30f, 103f), new Vector3(0.7f, 0.03f, 41.2f), p.Metal);
            for (int i = 0; i < 5; i++)
                p.Pipe("Cable" + i, new Vector3(-0.91f + i * 0.13f, 5.34f, 82.4f),
                    new Vector3(-0.91f + i * 0.13f, 5.34f, 123.6f), 0.022f, p.Rust);
            p.Box("PH_FireHoseCabinet", new Vector3(1.425f, 3.95f, 91.1f), new Vector3(0.15f, 0.9f, 1f), p.Rust);
            foreach (int z in new[] { 97, 115 })
                p.Box("FuseBox" + z, new Vector3(-1.425f, 4.3f, z), new Vector3(0.15f, 0.8f, 0.7f), p.Metal);
            foreach (int z in new[] { 90, 110 })
            {
                p.Box("VentBack" + z, new Vector3(1.485f, 2.95f, z), new Vector3(0.03f, 0.5f, 1f), p.Metal, false);
                for (int i = 0; i < 5; i++)
                    p.Box("Vent" + z + "Slat" + i, new Vector3(1.455f, 2.77f + i * 0.1f, z), new Vector3(0.03f, 0.04f, 0.96f), p.Concrete, false);
            }
            // Thin, non-colliding ellipses: smaller footprints keep the narrowed corridor's walls clear.
            p.Puddle("Puddle01", new Vector3(0.4f, 2.506f, 92f), new Vector3(1.5f, 0.002f, 0.9f), wet);
            p.Puddle("Puddle02", new Vector3(-0.5f, 2.506f, 103.5f), new Vector3(1.2f, 0.002f, 0.8f), wet);
            p.Puddle("Puddle03", new Vector3(0.4f, 2.506f, 115f), new Vector3(1.7f, 0.002f, 1.1f), wet);
        }

        private static Material WetMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(WetPath);
            bool create = material == null;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "CorridorPuddle" };
            material.SetColor("_BaseColor", new Color(0.09f, 0.12f, 0.14f, 1f));
            material.SetFloat("_Metallic", 0.1f);
            material.SetFloat("_Smoothness", 0.92f);
            // Run URP validation before the first asset write, including its legacy _Color synchronisation.
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
            if (create) AssetDatabase.CreateAsset(material, WetPath);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private sealed class Parts
        {
            private readonly Transform root;
            private readonly HashSet<string> built = new HashSet<string>();
            public readonly Material Metal = Load("MetalPlate"), Concrete = Load("ConcreteStained"),
                Hazard = Load("HazardStripe"), Glass = Load("Glass"), Rust = Load("Rust");
            public int Count => built.Count;
            public Parts(Transform root) { this.root = root; }
            private static Material Load(string name) => AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath(name))
                ?? throw new InvalidOperationException("Build Material Library first: missing " + name);

            public void Box(string name, Vector3 center, Vector3 size, Material material, bool solid = true)
            {
                GameObject go = Part(name, PrimitiveType.Cube, center, size);
                MeshFilter filter = go.GetComponent<MeshFilter>();
                if (filter.sharedMesh.name != go.name + BoxUvMesh.NameSuffix) filter.sharedMesh = new Mesh();
                BoxUvMesh.Build(filter.sharedMesh, go.transform);
                SetSurface(go, material, solid);
            }
            public void Pipe(string name, Vector3 from, Vector3 to, float radius, Material material)
            {
                GameObject go = Part(name, PrimitiveType.Cylinder, (from + to) * 0.5f,
                    new Vector3(radius * 2f, Vector3.Distance(from, to) * 0.5f, radius * 2f));
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, to - from);
                SetSurface(go, material, false); // Above head height; no misleading primitive capsule collision.
            }
            public void Puddle(string name, Vector3 center, Vector3 size, Material material)
            {
                GameObject go = Part(name, PrimitiveType.Cylinder, center, size);
                SetSurface(go, material, false);
                GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.SeeThrough);
                go.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }
            private GameObject Part(string name, PrimitiveType primitive, Vector3 center, Vector3 size)
            {
                GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label, primitive);
                Undo.RecordObject(go.transform, Label);
                go.transform.SetPositionAndRotation(center, Quaternion.identity);
                go.transform.localScale = size;
                go.layer = PhysicsLayers.Level;
                return go;
            }
            private void SetSurface(GameObject go, Material material, bool solid)
            {
                Renderer renderer = go.GetComponent<Renderer>();
                renderer.sharedMaterial = material;
                bool glass = material == Glass;
                renderer.shadowCastingMode = glass ? ShadowCastingMode.Off : ShadowCastingMode.On;
                GameObjectUtility.SetStaticEditorFlags(go, glass ? StaticFlagRules.SeeThrough : StaticFlagRules.Environment);
                // Keep generated colliders serialized and disable them; a repeat never adds/removes components.
                go.GetComponent<Collider>().enabled = solid;
            }
            public void Finish() => GeneratedObjects.RemoveStale(root, built);
        }

        public static void BuildAndVerify()
        {
            string[] protectedPaths = { PlayerSettingsPath, "Assets/Scenes/Interactables.unity", "Assets/Scenes/Agents.unity" };
            byte[][] protectedBytes = protectedPaths.Select(File.ReadAllBytes).ToArray();
            Build();
            byte[] scene = File.ReadAllBytes(WorldSceneSkeleton.ScenePath), material = File.ReadAllBytes(WetPath);
            Build();
            if (!scene.SequenceEqual(File.ReadAllBytes(WorldSceneSkeleton.ScenePath)) || !material.SequenceEqual(File.ReadAllBytes(WetPath)))
            {
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "blackout-w13-fix5-first.unity"), scene);
                File.WriteAllBytes(Path.Combine(Path.GetTempPath(), "blackout-w13-fix5-first.mat"), material);
                throw new InvalidOperationException("Security/corridor repeat mismatch: scene="
                    + !scene.SequenceEqual(File.ReadAllBytes(WorldSceneSkeleton.ScenePath)) + ", material="
                    + !material.SequenceEqual(File.ReadAllBytes(WetPath)) + ". First copies are in the temp folder.");
            }
            VerifyPlayerRoute();
            NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
            for (int i = 0; i < protectedPaths.Length; i++)
                if (!protectedBytes[i].SequenceEqual(File.ReadAllBytes(protectedPaths[i])))
                    throw new InvalidOperationException("Protected owner file changed: " + protectedPaths[i]);
            Debug.Log("[World] Security/corridor verification: repeat scene/material identical; player route clear; protected files unchanged.");
        }

        private static void VerifyPlayerRoute()
        {
            using var settings = new SerializedObject(AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PlayerSettingsPath));
            var probe = new GameObject("W13_TemporarySecurityCorridorProbe");
            try
            {
                probe.layer = PhysicsLayers.Player;
                probe.transform.position = new Vector3(0f, 2.55f, 70.5f);
                var controller = probe.AddComponent<CharacterController>();
                controller.height = settings.FindProperty("height").floatValue;
                controller.radius = settings.FindProperty("radius").floatValue;
                controller.center = Vector3.up * controller.height * 0.5f;
                controller.stepOffset = settings.FindProperty("stepOffset").floatValue;
                controller.slopeLimit = settings.FindProperty("slopeLimit").floatValue;
                controller.minMoveDistance = 0f;
                Physics.SyncTransforms();
                // The east side of the partition is reached around its open north end, preserving the pickup.
                foreach (Vector3 target in new[] { new Vector3(0f, 2.5f, 79.5f), new Vector3(6.5f, 2.5f, 80.5f),
                    new Vector3(0f, 2.5f, 79.5f), new Vector3(0f, 2.5f, 123f), new Vector3(0f, 2.5f, 70.5f) })
                {
                    int moves = 0;
                    for (; moves < 2000; moves++)
                    {
                        Vector3 delta = target - probe.transform.position;
                        delta.y = 0f;
                        if (delta.magnitude < 0.04f) break;
                        controller.Move(Vector3.ClampMagnitude(delta, 0.03f) + Vector3.down * 0.05f);
                    }
                    if (moves == 2000 || Mathf.Abs(probe.transform.position.y - target.y) > 0.1f)
                        throw new InvalidOperationException("Security/corridor route blocked at " + probe.transform.position + ", target " + target);
                }
                Debug.Log("[World] Player route passed: ramp mouth -> security health marker -> detector -> lab door -> ramp mouth.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }
    }
}
