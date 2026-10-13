using System.IO;
using Blackout.Hud;
using Blackout.Interaction;
using Blackout.Player;
using Blackout.Shared;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.UI;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds the interactable prefabs in Assets/Prefabs/Interactables (Door, Generator, CurePickup, HealthPickup,
    /// Crate) with placeholder looks, and gives the Player prefab its PlayerInteractor, PlayerPusher and the HUD
    /// hold ring. Numbers: docs/reference/systems-engineer.md section 10.
    /// Re-runnable: an existing prefab, material or texture is left untouched (delete it to rebuild), except that
    /// a Door missing its doorway trigger gets one added; a Player that already has a PlayerInteractor is not
    /// changed. The Generator prefab nests the Core Developer's
    /// Generator_Visual and never edits it.
    /// </summary>
    public static class InteractablePrefabBuilder
    {
        public const string Folder = "Assets/Prefabs/Interactables";
        public const string DoorPath = Folder + "/Door.prefab";
        public const string GeneratorPath = Folder + "/Generator.prefab";
        public const string CurePath = Folder + "/CurePickup.prefab";
        public const string HealthPath = Folder + "/HealthPickup.prefab";
        public const string CratePath = Folder + "/Crate.prefab";

        private const string MaterialFolder = Folder + "/Materials";
        private const string GeneratorVisualPath = "Assets/Prefabs/Models/Generator_Visual.prefab";
        private const string HoldRingPath = PlayerHudBuilder.HudFolder + "/HoldRing.png";
        private const string Events = EventChannelAssetBuilder.Folder;
        private const string CameraPath = "CameraRig/CameraPivot/PlayerCamera";

        // Every Unity door gap is 1.4 x 2.3 m (docs/level-layout.md); the panel overlaps the jambs by 0.1 m.
        private const float GapWidth = 1.4f;
        private const float GapHeight = 2.3f;
        private const float WallThickness = 0.4f;
        private const float PanelThickness = 0.16f;
        private const float CrateSize = 1.5f;
        private const float CrateMass = 120f;     // [GUESS] heavy enough that only a push moves it
        private const float CrateDrag = 1f;       // [GUESS] with the default floor friction (0.6 x g ≈ 5.9 m/s²) it
                                                  // stops about as fast as the prototype's 5.5 m/s²

        [MenuItem("Blackout/Systems/Build Interactable Prefabs")]
        public static void Build()
        {
            EnsureFolder("Assets/Prefabs", "Interactables");
            EnsureFolder(Folder, "Materials");

            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>(Events + "/Noise.asset");
            var generatorDisabled = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "/GeneratorDisabled.asset");
            var cureTaken = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(Events + "/CureTaken.asset");
            if (noise == null || generatorDisabled == null || cureTaken == null)
            {
                Debug.LogError("[Blackout] Missing event channels; run Create Event Channels first.");
                return;
            }

            int layer = LayerMask.NameToLayer("Interactable");
            if (layer < 0)
            {
                Debug.LogError("[Blackout] Layer 'Interactable' is not defined (P0); prefabs not built.");
                return;
            }

            int built = 0;
            built += SaveIfMissing(DoorPath, () => BuildDoor(noise), layer);
            built += SaveIfMissing(GeneratorPath, () => BuildGenerator(noise, generatorDisabled), layer);
            built += SaveIfMissing(CurePath, () => BuildCure(noise, cureTaken), layer);
            built += SaveIfMissing(HealthPath, BuildHealth, layer);
            built += SaveIfMissing(CratePath, () => BuildCrate(noise), layer);
            AddDoorwayTriggerToExistingDoor();
            AssetDatabase.SaveAssets();
            Debug.Log($"[Blackout] Interactable prefabs: built {built}, others left unchanged.");

            AddInteractionToPlayer();
        }

        private static int SaveIfMissing(string path, System.Func<GameObject> build, int layer)
        {
            if (AssetDatabase.LoadMainAssetAtPath(path) != null)
            {
                return 0;
            }

            GameObject root = build();
            if (root == null)
            {
                return 0;
            }

            SetLayerSkippingNestedPrefabs(root.transform, layer);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            Debug.Log($"[Blackout] Built {path}.");
            return 1;
        }

        // ---- door -------------------------------------------------------------------------------------------

        /// <summary>
        /// Door (SlidingDoor, NavMeshObstacle carving the gap while locked, trigger box filling the doorway)
        ///   Panel (BoxCollider blocker, kinematic Rigidbody: a moving collider)
        ///   LockLamp (red / green above the gap on the side you enter from)
        /// The root sits at the DoorSocket: floor level, centre of the wall, facing into the room you enter.
        /// </summary>
        private static GameObject BuildDoor(NoiseEventChannel noise)
        {
            var root = new GameObject("Door");
            Material panelMaterial = LitMaterial("Door_Placeholder", new Color(0.36f, 0.39f, 0.37f), 0.35f);
            Material locked = UnlitMaterial("DoorLamp_Locked", new Color(2.5f, 0.15f, 0.1f));
            Material unlocked = UnlitMaterial("DoorLamp_Unlocked", new Color(0.2f, 2.2f, 0.15f));

            Transform panel = Part(PrimitiveType.Cube, root.transform, "Panel", panelMaterial, true,
                new Vector3(0f, (GapHeight + 0.05f) * 0.5f, 0f), new Vector3(GapWidth + 0.2f, GapHeight + 0.05f, PanelThickness));
            var body = panel.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;

            Transform lamp = Part(PrimitiveType.Cube, root.transform, "LockLamp", unlocked, false,
                new Vector3(GapWidth * 0.5f + 0.25f, GapHeight + 0.2f, WallThickness * 0.5f + 0.03f),
                new Vector3(0.16f, 0.08f, 0.05f));

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, GapHeight * 0.5f, 0f);
            obstacle.size = new Vector3(GapWidth, GapHeight, WallThickness);
            obstacle.carving = true;
            obstacle.enabled = false; // the door turns it on while locked

            AddDoorwayTrigger(root);
            var door = root.AddComponent<SlidingDoor>();
            PlayerPrefabBuilder.Assign(door, "panel", panel);
            PlayerPrefabBuilder.Assign(door, "blocker", panel.GetComponent<Collider>());
            PlayerPrefabBuilder.Assign(door, "noiseChannel", noise);
            PlayerPrefabBuilder.Assign(door, "lockObstacle", obstacle);
            PlayerPrefabBuilder.Assign(door, "lockLamp", lamp.GetComponent<Renderer>());
            PlayerPrefabBuilder.Assign(door, "lockedLampMaterial", locked);
            PlayerPrefabBuilder.Assign(door, "unlockedLampMaterial", unlocked);
            SetFloat(door, "slideDistance", GapWidth + 0.05f);
            return root;
        }

        /// <summary>
        /// The interaction ray's target. The panel's collider switches off once the door is open enough to walk
        /// through, so without this an open door could not be aimed at and closed. A trigger blocks nothing: the
        /// player, agents, bullets and the knife all ignore triggers; only PlayerInteractor's ray hits them.
        /// </summary>
        private static void AddDoorwayTrigger(GameObject root)
        {
            var trigger = root.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.center = new Vector3(0f, GapHeight * 0.5f, 0f);
            trigger.size = new Vector3(GapWidth, GapHeight, WallThickness);
        }

        /// <summary>Adds the doorway trigger to a Door prefab built before it existed; nothing else changes.</summary>
        private static void AddDoorwayTriggerToExistingDoor()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(DoorPath);
            try
            {
                foreach (BoxCollider box in root.GetComponents<BoxCollider>())
                {
                    if (box.isTrigger)
                    {
                        return; // already there
                    }
                }

                AddDoorwayTrigger(root);
                PrefabUtility.SaveAsPrefabAsset(root, DoorPath);
                Debug.Log($"[Blackout] Added the doorway trigger to {DoorPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ---- generator --------------------------------------------------------------------------------------

        /// <summary>
        /// Generator
        ///   Generator_Visual (nested prefab instance from the Core Developer, untouched)
        ///   Console (GeneratorInteractable + trigger box over the control panel, at the visual's InteractPoint)
        /// </summary>
        private static GameObject BuildGenerator(NoiseEventChannel noise, VoidEventChannel generatorDisabled)
        {
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GeneratorVisualPath);
            if (visualPrefab == null)
            {
                Debug.LogError($"[Blackout] Missing {GeneratorVisualPath} (Core Developer); Generator not built.");
                return null;
            }

            Transform interactPoint = FindDeep(visualPrefab.transform, "InteractPoint");
            Vector3 consolePosition = interactPoint != null
                ? visualPrefab.transform.InverseTransformPoint(interactPoint.position)
                : new Vector3(3.5f, 1.3f, 0f);

            var root = new GameObject("Generator");
            var visual = (GameObject)PrefabUtility.InstantiatePrefab(visualPrefab);
            visual.transform.SetParent(root.transform, false);

            // The trigger reaches 0.3 m back over the control panel, so the ray finds it before the generator body.
            Transform console = PlayerPrefabBuilder.CreateChild("Console", root.transform, consolePosition);
            var box = console.gameObject.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.center = new Vector3(-0.2f, 0f, 0f);
            box.size = new Vector3(0.6f, 1.6f, 1.8f);

            var generator = console.gameObject.AddComponent<GeneratorInteractable>();
            PlayerPrefabBuilder.Assign(generator, "generatorDisabled", generatorDisabled);
            PlayerPrefabBuilder.Assign(generator, "noiseChannel", noise);
            PlayerPrefabBuilder.Assign(generator, "noiseOrigin", root.transform);
            return root;
        }

        // ---- cure ---------------------------------------------------------------------------------------------

        private static GameObject BuildCure(NoiseEventChannel noise, VoidEventChannel cureTaken)
        {
            var root = new GameObject("CurePickup");
            Material steel = LitMaterial("Cure_Steel_Placeholder", new Color(0.62f, 0.64f, 0.66f), 0.7f);
            Material vial = UnlitMaterial("Cure_Vial_Placeholder", new Color(0.4f, 2.6f, 2.8f));
            Part(PrimitiveType.Cube, root.transform, "Pedestal", steel, true, new Vector3(0f, 0.65f, 0f),
                new Vector3(0.5f, 1.3f, 0.5f));

            // The case sits at 1.45 m, as in the prototype.
            Transform visual = PlayerPrefabBuilder.CreateChild("Case", root.transform, new Vector3(0f, 1.45f, 0f));
            Part(PrimitiveType.Cube, visual, "Base", steel, false, Vector3.zero, new Vector3(0.42f, 0.08f, 0.24f));
            Part(PrimitiveType.Cube, visual, "Lid", steel, false, new Vector3(0f, 0.26f, 0f), new Vector3(0.42f, 0.04f, 0.24f));
            for (int i = 0; i < 3; i++)
            {
                Part(PrimitiveType.Cylinder, visual, "Vial " + i, vial, false, new Vector3(-0.1f + i * 0.1f, 0.13f, 0f),
                    new Vector3(0.06f, 0.085f, 0.06f));
            }

            // Walk-in zone: 1.05 m around the case horizontally, as in the prototype.
            var zone = root.AddComponent<CapsuleCollider>();
            zone.isTrigger = true;
            zone.radius = 1.05f;
            zone.height = 3f;
            zone.center = new Vector3(0f, 1.2f, 0f);

            var cure = root.AddComponent<CurePickup>();
            PlayerPrefabBuilder.Assign(cure, "cureTaken", cureTaken);
            PlayerPrefabBuilder.Assign(cure, "noiseChannel", noise);
            PlayerPrefabBuilder.Assign(cure, "visual", visual);
            return root;
        }

        // ---- health pickup ------------------------------------------------------------------------------------

        private static GameObject BuildHealth()
        {
            var root = new GameObject("HealthPickup");
            Material white = LitMaterial("Medkit_Placeholder", new Color(0.91f, 0.91f, 0.89f), 0.4f);
            Material red = LitMaterial("Medkit_Cross_Placeholder", new Color(0.77f, 0.12f, 0.12f), 0.4f);

            Transform visual = PlayerPrefabBuilder.CreateChild("Kit", root.transform, new Vector3(0f, 0.35f, 0f));
            Part(PrimitiveType.Cube, visual, "Box", white, false, Vector3.zero, new Vector3(0.42f, 0.26f, 0.28f));
            Part(PrimitiveType.Cube, visual, "CrossH", red, false, new Vector3(0f, 0.131f, 0f), new Vector3(0.18f, 0.005f, 0.06f));
            Part(PrimitiveType.Cube, visual, "CrossV", red, false, new Vector3(0f, 0.131f, 0f), new Vector3(0.06f, 0.005f, 0.18f));

            Transform glowObject = PlayerPrefabBuilder.CreateChild("Glow", root.transform, new Vector3(0f, 0.6f, 0f));
            var glow = glowObject.gameObject.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(0.38f, 1f, 0.56f); // 0x60ff90
            glow.intensity = 0.5f;
            glow.range = 3f;
            glow.shadows = LightShadows.None;

            var zone = root.AddComponent<CapsuleCollider>();
            zone.isTrigger = true;
            zone.radius = 1.1f;
            zone.height = 3.2f;
            zone.center = new Vector3(0f, 1f, 0f);

            var pickup = root.AddComponent<HealthPickup>();
            PlayerPrefabBuilder.Assign(pickup, "visual", visual);
            PlayerPrefabBuilder.Assign(pickup, "glow", glow);
            return root;
        }

        // ---- crate -------------------------------------------------------------------------------------------

        private static GameObject BuildCrate(NoiseEventChannel noise)
        {
            var root = new GameObject("Crate");
            Material wood = LitMaterial("Crate_Placeholder", new Color(0.42f, 0.31f, 0.2f), 0.15f);
            Material strap = LitMaterial("Crate_Strap_Placeholder", new Color(0.15f, 0.15f, 0.16f), 0.4f);
            float half = CrateSize * 0.5f;
            Part(PrimitiveType.Cube, root.transform, "Box", wood, false, new Vector3(0f, half, 0f), Vector3.one * CrateSize);
            foreach (float y in new[] { half - 0.45f, half + 0.45f })
            {
                Part(PrimitiveType.Cube, root.transform, "Strap", strap, false, new Vector3(0f, y, 0f),
                    new Vector3(CrateSize + 0.02f, 0.06f, CrateSize + 0.02f));
            }

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, half, 0f);
            box.size = Vector3.one * CrateSize;

            var body = root.AddComponent<Rigidbody>();
            body.mass = CrateMass;
            body.linearDamping = CrateDrag;
            body.constraints = RigidbodyConstraints.FreezeRotation; // stays upright and square, as in the prototype
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Box;
            obstacle.center = new Vector3(0f, half, 0f);
            obstacle.size = Vector3.one * CrateSize;
            obstacle.carving = true;
            obstacle.carveOnlyStationary = true; // re-carve once it stops, not every frame while pushed

            var crate = root.AddComponent<PushableCrate>();
            PlayerPrefabBuilder.Assign(crate, "noiseChannel", noise);
            return root;
        }

        // ---- player -----------------------------------------------------------------------------------------

        /// <summary>Adds the interactor, the crate pusher and the HUD hold ring to the Player prefab once.</summary>
        private static void AddInteractionToPlayer()
        {
            Sprite ring = CreateHoldRingIfMissing();
            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                if (root.GetComponent<PlayerInteractor>() != null)
                {
                    Debug.Log("[Blackout] Player prefab already has a PlayerInteractor; left unchanged.");
                    return;
                }

                var prompt = root.GetComponentInChildren<InteractionPrompt>(true);
                if (prompt == null)
                {
                    Debug.LogError("[Blackout] Player prefab has no HUD; run Build Player HUD first.");
                    return;
                }

                Transform promptRoot = FindDeep(root.transform, "Prompt");
                var ringObject = new GameObject("HoldRing", typeof(RectTransform));
                var ringRect = (RectTransform)ringObject.transform;
                ringRect.SetParent(promptRoot, false);
                ringRect.anchorMin = ringRect.anchorMax = ringRect.pivot = new Vector2(1f, 0.5f);
                ringRect.anchoredPosition = new Vector2(-8f, 0f);
                ringRect.sizeDelta = new Vector2(34f, 34f);
                ringObject.layer = promptRoot.gameObject.layer;
                var image = ringObject.AddComponent<Image>();
                image.sprite = ring;
                image.color = HudColors.Accent;
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Top;
                image.fillClockwise = true;
                image.fillAmount = 0f;
                image.raycastTarget = false;
                image.enabled = false;
                PlayerPrefabBuilder.Assign(prompt, "holdRing", image);

                var interactor = root.AddComponent<PlayerInteractor>();
                PlayerPrefabBuilder.Assign(interactor, "input", root.GetComponent<PlayerInputReader>());
                PlayerPrefabBuilder.Assign(interactor, "playerHealth", root.GetComponent<PlayerHealth>());
                PlayerPrefabBuilder.Assign(interactor, "aim", root.transform.Find(CameraPath));
                PlayerPrefabBuilder.Assign(interactor, "prompt", prompt);
                var serialized = new SerializedObject(interactor);
                serialized.FindProperty("mask").intValue = ~(1 << LayerMask.NameToLayer("Player")
                                                           | 1 << LayerMask.NameToLayer("Ignore Raycast"));
                serialized.ApplyModifiedPropertiesWithoutUndo();

                var pusher = root.AddComponent<PlayerPusher>();
                PlayerPrefabBuilder.Assign(pusher, "motor", root.GetComponent<PlayerMotor>());

                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath, out bool saved);
                Debug.Log(saved
                    ? $"[Blackout] Added PlayerInteractor, PlayerPusher and the hold ring to {PlayerPrefabBuilder.PrefabPath}."
                    : $"[Blackout] Could not save {PlayerPrefabBuilder.PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>A 64 px white ring imported as a Sprite, so a Filled Image can draw it as a progress arc.</summary>
        private static Sprite CreateHoldRingIfMissing()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Sprite>(HoldRingPath);
            if (existing != null)
            {
                return existing;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float r = new Vector2((x + 0.5f) / size - 0.5f, (y + 0.5f) / size - 0.5f).magnitude * 2f;
                    float a = Mathf.Clamp01(1f - Mathf.Abs(r - 0.82f) / 0.1f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(Path.Combine(Directory.GetCurrentDirectory(), HoldRingPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(HoldRingPath);
            var importer = (TextureImporter)AssetImporter.GetAtPath(HoldRingPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
            Debug.Log($"[Blackout] Generated {HoldRingPath}.");
            return AssetDatabase.LoadAssetAtPath<Sprite>(HoldRingPath);
        }

        // ---- helpers -----------------------------------------------------------------------------------------

        /// <summary>A primitive part. Visual-only parts lose their collider; blockers keep it.</summary>
        private static Transform Part(PrimitiveType type, Transform parent, string name, Material material,
            bool keepCollider, Vector3 position, Vector3 scale)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            if (!keepCollider)
            {
                Object.DestroyImmediate(part.GetComponent<Collider>());
            }

            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.GetComponent<MeshRenderer>().sharedMaterial = material;
            return part.transform;
        }

        private static Material LitMaterial(string name, Color colour, float smoothness)
        {
            return MaterialIfMissing(name, "Universal Render Pipeline/Lit", material =>
            {
                material.SetColor("_BaseColor", colour);
                material.SetFloat("_Smoothness", smoothness);
            });
        }

        /// <summary>Unlit, so lamps and vials read as glowing (HDR colours bloom) without lights.</summary>
        private static Material UnlitMaterial(string name, Color colour)
        {
            return MaterialIfMissing(name, "Universal Render Pipeline/Unlit", material =>
            {
                material.SetColor("_BaseColor", colour);
                material.renderQueue = (int)RenderQueue.Geometry;
            });
        }

        private static Material MaterialIfMissing(string name, string shader, System.Action<Material> setUp)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find(shader));
            setUp(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void SetFloat(Object target, string field, float value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).floatValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Puts our objects on the Interactable layer but leaves nested prefabs (Generator_Visual) as they are.</summary>
        private static void SetLayerSkippingNestedPrefabs(Transform target, int layer)
        {
            target.gameObject.layer = layer;
            foreach (Transform child in target)
            {
                if (!PrefabUtility.IsAnyPrefabInstanceRoot(child.gameObject))
                {
                    SetLayerSkippingNestedPrefabs(child, layer);
                }
            }
        }

        private static Transform FindDeep(Transform parent, string name)
        {
            if (parent.name == name)
            {
                return parent;
            }

            foreach (Transform child in parent)
            {
                Transform found = FindDeep(child, name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static void EnsureFolder(string parent, string name)
        {
            if (!AssetDatabase.IsValidFolder($"{parent}/{name}"))
            {
                AssetDatabase.CreateFolder(parent, name);
            }
        }
    }
}
