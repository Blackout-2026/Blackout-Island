using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>
    /// Adds the three weapons to the Player prefab: the WeaponData assets (SMG, rifle, knife), placeholder
    /// viewmodels made of primitives, the WeaponController on the WeaponHolder, and the pooled effects.
    /// Re-runnable: existing assets are left untouched, and a prefab that already has a WeaponController is not
    /// changed, except that a hit mask still set to the Y3 value is moved to PhysicsLayers.PlayerShots (Y7).
    /// To rebuild the weapons, delete the WeaponHolder's children and its WeaponController, then run again.
    /// </summary>
    public static class PlayerWeaponsBuilder
    {
        public const string DataFolder = "Assets/ScriptableObjects/Weapons";
        public const string ArtFolder = PlayerPrefabBuilder.Folder + "/Weapons";
        public const string SmgPath = DataFolder + "/SMG.asset";
        public const string RiflePath = DataFolder + "/Rifle.asset";
        public const string KnifePath = DataFolder + "/Knife.asset";

        private const string HolderPath = "CameraRig/CameraPivot/PlayerCamera/WeaponHolder";
        private const string CameraPath = "CameraRig/CameraPivot/PlayerCamera";
        private const string NoiseChannelPath = EventChannelAssetBuilder.Folder + "/Noise.asset";

        // Prototype: the whole viewmodel holder is scaled 0.85.
        private const float HolderScale = 0.85f;
        // Prototype world flash light: colour 0xffb060, range 14 m.
        private static readonly Color FlashColour = new Color(1f, 0.69f, 0.38f);
        private const float FlashRange = 14f;
        private const float FlashQuadSize = 0.22f;

        private const int UrpAdditiveBlend = 2; // URP's "_Blend" value for Additive

        [MenuItem("Blackout/Systems/Build Player Weapons")]
        public static void Build()
        {
            EnsureFolder(PlayerPrefabBuilder.Folder, "Weapons");
            WeaponData smg = CreateDataIfMissing(SmgPath, SetSmg);
            WeaponData rifle = CreateDataIfMissing(RiflePath, SetRifle);
            WeaponData knife = CreateDataIfMissing(KnifePath, SetKnife);
            var materials = new Materials
            {
                Dark = CreateLitIfMissing("Viewmodel_Dark_Placeholder", new Color(0.12f, 0.12f, 0.13f), 0.45f),
                Tan = CreateLitIfMissing("Viewmodel_Tan_Placeholder", new Color(0.45f, 0.38f, 0.27f), 0.25f),
                Steel = CreateLitIfMissing("Viewmodel_Steel_Placeholder", new Color(0.6f, 0.62f, 0.65f), 0.7f),
                Effects = CreateAdditiveIfMissing("WeaponFX_Additive", Color.white),
                Flash = CreateAdditiveIfMissing("MuzzleFlash_Placeholder", FlashColour),
            };
            AssetDatabase.SaveAssets();

            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>(NoiseChannelPath);
            if (AssetDatabase.LoadMainAssetAtPath(PlayerPrefabBuilder.PrefabPath) == null || noise == null)
            {
                Debug.LogError($"[Blackout] Missing {PlayerPrefabBuilder.PrefabPath} or {NoiseChannelPath}; " +
                               "run Build Player Prefab and Create Event Channels first.");
                return;
            }

            GameObject root = PrefabUtility.LoadPrefabContents(PlayerPrefabBuilder.PrefabPath);
            try
            {
                Transform holder = root.transform.Find(HolderPath);
                if (holder == null)
                {
                    Debug.LogError($"[Blackout] Player prefab has no {HolderPath}; weapons not added.");
                    return;
                }

                var existing = holder.GetComponent<WeaponController>();
                if (existing != null)
                {
                    if (UpgradeHitMask(existing))
                    {
                        PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath, out bool upgraded);
                        Debug.Log(upgraded
                            ? "[Blackout] Player prefab already has a WeaponController; moved its hit mask to " +
                              "PhysicsLayers.PlayerShots."
                            : $"[Blackout] Could not save {PlayerPrefabBuilder.PrefabPath}.");
                    }
                    else
                    {
                        Debug.Log("[Blackout] Player prefab already has a WeaponController; left unchanged.");
                    }

                    return;
                }

                AddWeapons(root, holder, noise, smg, rifle, knife, materials);
                PrefabUtility.SaveAsPrefabAsset(root, PlayerPrefabBuilder.PrefabPath, out bool saved);
                Debug.Log(saved
                    ? $"[Blackout] Added the SMG, rifle and knife to {PlayerPrefabBuilder.PrefabPath}."
                    : $"[Blackout] Could not save {PlayerPrefabBuilder.PrefabPath}.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private struct Materials
        {
            public Material Dark;
            public Material Tan;
            public Material Steel;
            public Material Effects;
            public Material Flash;
        }

        // ---- weapon data (docs/reference/systems-engineer.md section 7) ---------------------------------

        private static void SetSmg(WeaponData d)
        {
            // The class defaults are the SMG's numbers.
        }

        private static void SetRifle(WeaponData d)
        {
            d.displayName = "R-700 Rifle";
            d.slot = 2;
            d.damage = 60f;
            d.range = 320f;
            d.falloffStart = 999f; // no falloff inside the range
            d.falloffEnd = 1000f;
            d.falloffMinMultiplier = 1f;
            d.cooldown = 60f / 52f;
            d.automatic = false;
            d.spread = 0f;
            d.moveSpread = 0f;
            d.bloomPerShot = 0f;
            d.bloomMax = 0f;
            d.recoilPitch = 3.15f;
            d.recoilYaw = 0.57f;
            d.kickBack = 0.11f;
            d.kickRotate = 9.2f;
            d.noiseRadius = 65f;
            d.tracerEvery = 1;
            d.muzzleFlashScale = 1.2f;
        }

        private static void SetKnife(WeaponData d)
        {
            d.displayName = "Combat Knife";
            d.slot = 3;
            d.kind = WeaponKind.Melee;
            d.damage = 40f;
            d.range = 2.3f;
            d.falloffStart = 999f;
            d.falloffEnd = 1000f;
            d.falloffMinMultiplier = 1f;
            d.cooldown = 60f / 125f;
            d.automatic = true;
            d.spread = 0f;
            d.moveSpread = 0f;
            d.bloomPerShot = 0f;
            d.bloomMax = 0f;
            d.recoilPitch = 0f;
            d.recoilYaw = 0f;
            d.kickBack = 0f;
            d.kickRotate = 12f; // [GUESS] stands in for the swing animation, which is not ported
            d.noiseRadius = 3f;
            d.tracerEvery = 0;
            d.muzzleFlashScale = 0f;
        }

        private static WeaponData CreateDataIfMissing(string path, System.Action<WeaponData> setValues)
        {
            var existing = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (existing != null)
            {
                return existing;
            }

            var data = ScriptableObject.CreateInstance<WeaponData>();
            setValues(data);
            AssetDatabase.CreateAsset(data, path);
            Debug.Log($"[Blackout] Created {path}.");
            return data;
        }

        // ---- placeholder materials --------------------------------------------------------------------------

        private static Material CreateLitIfMissing(string name, Color colour, float smoothness)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// URP Particles/Unlit set to additive and double-sided, as its Inspector would set it. Uses vertex colour,
        /// so tracers and sparks can fade through their own colour.
        /// </summary>
        private static Material CreateAdditiveIfMissing(string name, Color colour)
        {
            string path = $"{ArtFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Surface", 1f); // transparent
            material.SetFloat("_Blend", UrpAdditiveBlend);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.One);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_Cull", (float)CullMode.Off);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        // ---- prefab -----------------------------------------------------------------------------------------

        /// <summary>
        /// Player
        ///   ... PlayerCamera
        ///         WeaponHolder (WeaponController)
        ///           SMG (HitscanWeapon) + viewmodel, Muzzle (MuzzleFlash) / FlashQuads
        ///           Rifle (HitscanWeapon) + viewmodel, Muzzle (MuzzleFlash) / FlashQuads
        ///           Knife (MeleeWeapon) + viewmodel
        ///           MuzzleLight (one shared point light)
        ///   WeaponEffects
        ///     Tracers (TracerPool), ImpactSparks (ParticleSystem + ImpactEffects)
        /// </summary>
        private static void AddWeapons(GameObject root, Transform holder, NoiseEventChannel noise, WeaponData smg,
            WeaponData rifle, WeaponData knife, Materials materials)
        {
            holder.localScale = Vector3.one * HolderScale;

            Light flashLight = CreateFlashLight(holder);
            BuildSmg(holder, smg, materials, flashLight);
            BuildRifle(holder, rifle, materials, flashLight);
            BuildKnife(holder, knife, materials);

            Transform effects = PlayerPrefabBuilder.CreateChild("WeaponEffects", root.transform, Vector3.zero);
            Transform tracerObject = PlayerPrefabBuilder.CreateChild("Tracers", effects, Vector3.zero);
            var tracers = tracerObject.gameObject.AddComponent<TracerPool>();
            PlayerPrefabBuilder.Assign(tracers, "material", materials.Effects);
            ImpactEffects impacts = CreateImpactSparks(effects, materials.Effects);

            var controller = holder.gameObject.AddComponent<WeaponController>();
            PlayerPrefabBuilder.Assign(controller, "input", root.GetComponent<PlayerInputReader>());
            PlayerPrefabBuilder.Assign(controller, "motor", root.GetComponent<PlayerMotor>());
            PlayerPrefabBuilder.Assign(controller, "look", root.GetComponent<PlayerLook>());
            PlayerPrefabBuilder.Assign(controller, "aim", root.transform.Find(CameraPath));
            PlayerPrefabBuilder.Assign(controller, "noiseChannel", noise);
            PlayerPrefabBuilder.Assign(controller, "tracers", tracers);
            PlayerPrefabBuilder.Assign(controller, "impacts", impacts);
            SetHitMask(controller);

            PlayerPrefabBuilder.SetLayerRecursively(root.transform, root.layer);
        }

        /// <summary>The world, agents, doors and hitboxes only (docs/reference/systems-engineer.md section 12).</summary>
        private static void SetHitMask(WeaponController controller)
        {
            var serialized = new SerializedObject(controller);
            serialized.FindProperty("hitMask").intValue = PhysicsLayers.PlayerShots;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Moves a hit mask this tool generated before Y7 ("everything except Player, Ignore Raycast and Ragdoll") or
        /// the old class default (everything) to PlayerShots. Any other mask was set by hand and is kept.
        /// </summary>
        private static bool UpgradeHitMask(WeaponController controller)
        {
            const int y3Mask = ~(1 << PhysicsLayers.Player | 1 << PhysicsLayers.IgnoreRaycast | 1 << PhysicsLayers.Ragdoll);
            var serialized = new SerializedObject(controller);
            SerializedProperty mask = serialized.FindProperty("hitMask");
            if (mask.intValue != y3Mask && mask.intValue != ~0)
            {
                return false;
            }

            mask.intValue = PhysicsLayers.PlayerShots;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return true;
        }

        private static Light CreateFlashLight(Transform holder)
        {
            Transform lightObject = PlayerPrefabBuilder.CreateChild("MuzzleLight", holder, new Vector3(0.13f, -0.1f, 0.8f));
            var light = lightObject.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = FlashColour;
            light.range = FlashRange;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        // Positions from the prototype's models/weapons.js, with Z flipped (Unity's camera looks down +Z).

        private static void BuildSmg(Transform holder, WeaponData data, Materials m, Light flashLight)
        {
            Transform weapon = PlayerPrefabBuilder.CreateChild("SMG", holder, new Vector3(0.13f, -0.125f, 0.45f));
            Box(weapon, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.068f, 0.27f), m.Dark);
            Barrel(weapon, "Barrel", new Vector3(0f, 0.004f, 0.19f), 0.02f, 0.13f, m.Dark);
            Box(weapon, "Magazine", new Vector3(0f, -0.1f, 0.03f), new Vector3(0.032f, 0.16f, 0.048f), m.Dark);
            Box(weapon, "Grip", new Vector3(0f, -0.07f, -0.085f), new Vector3(0.034f, 0.095f, 0.044f), m.Dark);
            Box(weapon, "Sight", new Vector3(0f, 0.065f, -0.02f), new Vector3(0.04f, 0.035f, 0.05f), m.Steel);
            AddHitscan(weapon, data, new Vector3(0f, 0.004f, 0.33f), m.Flash, flashLight);
        }

        private static void BuildRifle(Transform holder, WeaponData data, Materials m, Light flashLight)
        {
            Transform weapon = PlayerPrefabBuilder.CreateChild("Rifle", holder, new Vector3(0.12f, -0.12f, 0.46f));
            Box(weapon, "Body", new Vector3(0f, 0f, 0f), new Vector3(0.05f, 0.06f, 0.3f), m.Dark);
            Barrel(weapon, "Barrel", new Vector3(0f, 0.008f, 0.45f), 0.0145f, 0.62f, m.Dark);
            Box(weapon, "Stock", new Vector3(0f, -0.03f, -0.05f), new Vector3(0.055f, 0.075f, 0.48f), m.Tan);
            Box(weapon, "Butt", new Vector3(0f, -0.04f, -0.33f), new Vector3(0.05f, 0.11f, 0.18f), m.Tan);
            Box(weapon, "Magazine", new Vector3(0f, -0.09f, 0.02f), new Vector3(0.03f, 0.08f, 0.06f), m.Dark);
            Barrel(weapon, "Scope", new Vector3(0f, 0.075f, 0.02f), 0.024f, 0.24f, m.Steel);
            AddHitscan(weapon, data, new Vector3(0f, 0.008f, 0.81f), m.Flash, flashLight);
        }

        private static void BuildKnife(Transform holder, WeaponData data, Materials m)
        {
            Transform weapon = PlayerPrefabBuilder.CreateChild("Knife", holder, Vector3.zero);
            Transform hand = PlayerPrefabBuilder.CreateChild("Hand", weapon, new Vector3(0.17f, -0.18f, 0.33f));
            hand.localRotation = Quaternion.Euler(-14f, -14f, -23f); // prototype (0.25, 0.25, -0.4) rad, Z flipped
            Barrel(hand, "Handle", Vector3.zero, 0.017f, 0.12f, m.Dark);
            Box(hand, "Guard", new Vector3(0f, 0f, 0.065f), new Vector3(0.07f, 0.012f, 0.014f), m.Steel);
            Box(hand, "Blade", new Vector3(0f, 0.01f, 0.17f), new Vector3(0.004f, 0.03f, 0.2f), m.Steel);

            var melee = weapon.gameObject.AddComponent<MeleeWeapon>();
            PlayerPrefabBuilder.Assign(melee, "data", data);
        }

        private static void AddHitscan(Transform weapon, WeaponData data, Vector3 muzzlePosition, Material flash,
            Light flashLight)
        {
            Transform muzzle = PlayerPrefabBuilder.CreateChild("Muzzle", weapon, muzzlePosition);
            Transform quads = PlayerPrefabBuilder.CreateChild("FlashQuads", muzzle, Vector3.zero);
            // Three crossed quads, as in the prototype, so the flash reads from any angle.
            Quad(quads, Quaternion.identity, 1f, flash);
            Quad(quads, Quaternion.Euler(0f, 90f, 0f), 1f, flash);
            Quad(quads, Quaternion.Euler(90f, 0f, 0f), 0.7f, flash);
            quads.gameObject.SetActive(false);

            var muzzleFlash = muzzle.gameObject.AddComponent<MuzzleFlash>();
            PlayerPrefabBuilder.Assign(muzzleFlash, "visual", quads);
            PlayerPrefabBuilder.Assign(muzzleFlash, "flashLight", flashLight);

            var hitscan = weapon.gameObject.AddComponent<HitscanWeapon>();
            PlayerPrefabBuilder.Assign(hitscan, "data", data);
            PlayerPrefabBuilder.Assign(hitscan, "muzzle", muzzle);
            PlayerPrefabBuilder.Assign(hitscan, "muzzleFlash", muzzleFlash);
        }

        private static ImpactEffects CreateImpactSparks(Transform parent, Material material)
        {
            Transform sparks = PlayerPrefabBuilder.CreateChild("ImpactSparks", parent, Vector3.zero);
            var particles = sparks.gameObject.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.playOnAwake = false;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2f, 5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f);
            main.startColor = new Color(1f, 0.8f, 0.5f);
            main.gravityModifier = 1f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 200;

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 35f;
            shape.radius = 0.01f;

            var renderer = sparks.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = material;
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 1f;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return sparks.gameObject.AddComponent<ImpactEffects>();
        }

        // ---- primitives (colliders removed: viewmodels must never block rays or the player) ------------------

        private static void Box(Transform parent, string name, Vector3 position, Vector3 size, Material material)
        {
            Transform part = Primitive(PrimitiveType.Cube, parent, name, material);
            part.localPosition = position;
            part.localScale = size;
        }

        /// <summary>A cylinder lying along Z (Unity's cylinder stands along Y, 2 m tall and 1 m wide).</summary>
        private static void Barrel(Transform parent, string name, Vector3 position, float radius, float length,
            Material material)
        {
            Transform part = Primitive(PrimitiveType.Cylinder, parent, name, material);
            part.localPosition = position;
            part.localRotation = Quaternion.Euler(90f, 0f, 0f);
            part.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        }

        private static void Quad(Transform parent, Quaternion rotation, float scale, Material material)
        {
            Transform part = Primitive(PrimitiveType.Quad, parent, "Flash", material);
            part.localRotation = rotation;
            part.localScale = Vector3.one * (FlashQuadSize * scale);
        }

        private static Transform Primitive(PrimitiveType type, Transform parent, string name, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>());
            part.transform.SetParent(parent, false);

            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return part.transform;
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
