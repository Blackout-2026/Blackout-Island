using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Shared;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Restores the prototype's player-only catwalk at the existing health-pickup marker.
    /// Generates owned scene geometry only; does not move the pickup, markers or other owners' prefabs.</summary>
    public static class GeneratorCatwalkBuilder
    {
        public const string ObjectPath = "GeneratorRoom/GeneratorCatwalk";
        public const float DeckY = 5.5f;
        private const string Label = "Build Generator Catwalk";
        private const string PlayerSettingsPath = "Assets/Prefabs/Player/PlayerMovementSettings.asset";
        private const float FloorY = 2.5f;

        [MenuItem("Blackout/World/Build Generator Catwalk")]
        public static void Build()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            Transform room = WorldSceneSkeleton.FindRoot(scene, "Bunker").transform.Find("GeneratorRoom");
            Transform root = GeneratedObjects.FindOrCreateChild(room, "GeneratorCatwalk", Label);
            if (root.localPosition != Vector3.zero || root.localRotation != Quaternion.identity || root.localScale != Vector3.one)
                throw new InvalidOperationException("GeneratorCatwalk must retain its identity transform.");
            // Room's root is at its floor centre. Every coordinate below is world space.
            Material metal = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath("MetalPlate"));
            if (metal == null) throw new InvalidOperationException("Build Material Library before the catwalk.");
            var built = new HashSet<string>();
            Box(root, built, "Deck", new Vector3(-19.6f, 5.35f, 113.6f), new Vector3(-6f, DeckY, 116f), metal);
            for (int step = 0; step < 10; step++)
            {
                float east = -2.9f - 0.31f * step;
                Box(root, built, "Step" + (step + 1).ToString("00"), new Vector3(east - 0.31f, FloorY, 114.6f),
                    new Vector3(east, FloorY + 0.3f * (step + 1), 116f), metal);
            }
            // Skip the prototype's westernmost column: the placed second fuel tank occupies that space.
            foreach (float x in new[] { -16f, -13f, -10f, -7f })
            {
                Box(root, built, "Support" + x.ToString("00"), new Vector3(x - 0.06f, FloorY, 113.68f),
                    new Vector3(x + 0.06f, 5.35f, 113.8f), metal);
                Box(root, built, "Crossbeam" + x.ToString("00"), new Vector3(x - 0.07f, 5.22f, 113.6f),
                    new Vector3(x + 0.07f, 5.35f, 116f), metal);
            }
            Rail(root, built, "Front", new Vector3(-19.6f, DeckY, 113.62f), new Vector3(-6f, DeckY, 113.62f), metal);
            Rail(root, built, "WestEnd", new Vector3(-19.6f, DeckY, 113.62f), new Vector3(-19.6f, DeckY, 115.96f), metal);
            // Leave the eastern end open only where the stairs meet the deck (z114.6...116).
            Rail(root, built, "EastEnd", new Vector3(-6f, DeckY, 113.62f), new Vector3(-6f, DeckY, 114.56f), metal);
            Rail(root, built, "Stair", new Vector3(-2.9f, FloorY, 114.58f), new Vector3(-6f, DeckY, 114.58f), metal);
            GeneratedObjects.RemoveStale(root, built);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[World] Generator catwalk built: {built.Count} parts; deck 13.6 x 2.4 m at y5.5; "
                + "10 steps, rise0.3/run0.31, width1.4; movement-only rail guards; player-only NavMesh policy.");
        }

        private static void Rail(Transform root, HashSet<string> built, string name, Vector3 from, Vector3 to, Material material)
        {
            int segments = Mathf.CeilToInt(Vector3.Distance(from, to) / 1.5f);
            for (int i = 0; i <= segments; i++)
            {
                Vector3 foot = Vector3.Lerp(from, to, (float)i / segments);
                Beam(root, built, name + "Post" + i.ToString("00"), foot, foot + Vector3.up, 0.04f, material);
            }
            Beam(root, built, name + "Top", from + Vector3.up, to + Vector3.up, 0.05f, material);
            Beam(root, built, name + "Mid", from + Vector3.up * 0.5f, to + Vector3.up * 0.5f, 0.035f, material);
            // A broad movement collider closes gaps between the thin visual posts. Weapons ignore this layer.
            Vector3 direction = to - from;
            direction.y = 0f;
            GameObject guard = GeneratedObjects.GetOrCreate(root, name + "Guard", built, Label);
            guard.transform.SetPositionAndRotation((from + to) * 0.5f + Vector3.up * 0.5f, Quaternion.LookRotation(direction));
            guard.transform.localScale = Vector3.one;
            BoxCollider collider = GeneratedObjects.GetOrAddComponent<BoxCollider>(guard);
            collider.size = new Vector3(0.08f, Mathf.Abs(to.y - from.y) + 1f, direction.magnitude);
            guard.layer = PhysicsLayers.IgnoreRaycast;
            GameObjectUtility.SetStaticEditorFlags(guard, 0);
        }

        private static void Beam(Transform root, HashSet<string> built, string name, Vector3 from, Vector3 to,
            float thickness, Material material)
        {
            GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label, PrimitiveType.Cube);
            go.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.FromToRotation(Vector3.up, to - from));
            go.transform.localScale = new Vector3(thickness, Vector3.Distance(from, to), thickness);
            Finish(go, material, PhysicsLayers.IgnoreRaycast);
        }

        private static void Box(Transform root, HashSet<string> built, string name, Vector3 min, Vector3 max, Material material)
        {
            GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label, PrimitiveType.Cube);
            go.transform.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            go.transform.localScale = max - min;
            Finish(go, material, PhysicsLayers.Level);
        }

        private static void Finish(GameObject go, Material material, int layer)
        {
            go.layer = layer;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            MeshFilter filter = go.GetComponent<MeshFilter>();
            if (filter.sharedMesh.name != go.name + BoxUvMesh.NameSuffix) filter.sharedMesh = new Mesh();
            BoxUvMesh.Build(filter.sharedMesh, go.transform);
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.Environment);
        }

        // Real CharacterController movement using the current player settings, without entering Play mode
        // or modifying the player's prefab. The temporary controller is always removed before any scene save.
        private static void VerifyPlayerAccess()
        {
            UnityEngine.Object settingsAsset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(PlayerSettingsPath);
            if (settingsAsset == null) throw new InvalidOperationException("Player movement settings are missing.");
            using var settings = new SerializedObject(settingsAsset);
            var probe = new GameObject("W13_TemporaryCatwalkPlayerProbe");
            try
            {
                probe.layer = PhysicsLayers.Player;
                probe.transform.position = new Vector3(-2.4f, FloorY + 0.05f, 115.3f);
                var controller = probe.AddComponent<CharacterController>();
                controller.height = settings.FindProperty("height").floatValue;
                controller.radius = settings.FindProperty("radius").floatValue;
                controller.center = Vector3.up * controller.height * 0.5f;
                controller.stepOffset = settings.FindProperty("stepOffset").floatValue;
                controller.slopeLimit = settings.FindProperty("slopeLimit").floatValue;
                controller.minMoveDistance = 0f;
                Physics.SyncTransforms();
                Walk(controller, new Vector3(-6.6f, DeckY, 115.3f));
                Walk(controller, new Vector3(-18.5f, DeckY, 114.5f));
                Walk(controller, new Vector3(-6.6f, DeckY, 115.3f));
                Walk(controller, new Vector3(-2.4f, FloorY, 115.3f));
                Debug.Log($"[World] Catwalk player check passed: stairs -> health marker -> stairs -> floor; "
                    + $"height{controller.height}, radius{controller.radius}, step{controller.stepOffset}, slope{controller.slopeLimit}.");
            }
            finally { UnityEngine.Object.DestroyImmediate(probe); }
        }

        private static void Walk(CharacterController controller, Vector3 target)
        {
            for (int frame = 0; frame < 2000; frame++)
            {
                Vector3 delta = target - controller.transform.position;
                delta.y = 0f;
                if (delta.magnitude < 0.04f && Mathf.Abs(controller.transform.position.y - target.y) < 0.15f) return;
                controller.Move(Vector3.ClampMagnitude(delta, 0.03f) + Vector3.down * 0.05f);
            }
            throw new InvalidOperationException("Catwalk player cannot reach " + target + "; stopped at " + controller.transform.position);
        }

        public static void BuildAndVerify()
        {
            Build();
            byte[] before = File.ReadAllBytes(WorldSceneSkeleton.ScenePath);
            Build();
            if (!before.SequenceEqual(File.ReadAllBytes(WorldSceneSkeleton.ScenePath)))
                throw new InvalidOperationException("Catwalk rebuild changed the scene on repeat.");
            VerifyPlayerAccess();
            NavMeshBaker.BakeNavMesh();
            for (float x = -19f; x < -6.1f; x += 0.5f)
                if (NavMesh.SamplePosition(new Vector3(x, DeckY, 114.8f), out _, 0.2f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Agents can use the player-only catwalk at x=" + x);
            for (int step = 0; step < 10; step++)
                if (NavMesh.SamplePosition(new Vector3(-3.055f - 0.31f * step, FloorY + 0.3f * (step + 1), 115.3f),
                    out _, 0.12f, NavMesh.AllAreas))
                    throw new InvalidOperationException("Agents can use catwalk step " + (step + 1));
            Debug.Log("[World] Catwalk verification: repeat scene identical; no agent NavMesh on deck or any stair tread.");
            LayoutValidator.ValidateLayout();
        }
    }
}
