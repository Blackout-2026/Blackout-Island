using System.Collections.Generic;
using System.Text;
using Blackout.Environment;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Setup Lighting: the powered (pre-blackout) lighting of Environment.unity, from
    /// <see cref="LightingLayout"/> (reference section 4).
    /// - Sky, fog and ambient: the NightSky skybox (Assets/Environment/Lighting/NightSky.shader), exponential squared fog
    ///   and three-colour ambient, all at the prototype's outdoor values.
    /// - Lighting/Moon: one directional light, Mixed, with soft shadows. It is also the scene's sun.
    /// - Lighting/PoweredLights/&lt;Room&gt;: the 31 bunker fixtures from <see cref="LightFixtureLayout"/>, Mixed point lights,
    ///   each with an emissive lamp mesh (Assets/Materials/Environment/Lighting). The W8 blackout switches these off.
    /// - Lighting/OutdoorLights: the compound floodlights, the wall pack over the bunker door and the boat lantern. The
    ///   generator only powers the bunker, so these stay on.
    /// - Lighting/ReflectionProbes (one baked probe per room) and Lighting/LightProbes (the bunker, the compound yard,
    ///   the dock and the trail), placed by <see cref="ProbeLayout"/>.
    /// - Static flags on the environment's renderers, from <see cref="StaticFlagRules"/>.
    /// - Assets/Environment/Lighting/EnvironmentLighting.lighting: the scene's Lighting Settings asset. Unity 6.3 has no
    ///   Auto Generate any more, so lighting is only baked when the World Builder clicks Generate Lighting in
    ///   Window > Rendering > Lighting.
    /// It never bakes. It does not edit the URP asset (Assets/Settings): it reports its shadow settings against the
    /// medium-preset values in <see cref="LightingLayout"/>.
    /// Re-runnable and deterministic: objects are reused by name and every value is set from the table.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.LightingSetup.SetupLighting
    /// </summary>
    public static class LightingSetup
    {
        public const string LightingFolder = "Assets/Environment/Lighting";
        public const string SkyShaderPath = LightingFolder + "/NightSky.shader";
        public const string SkyMaterialPath = LightingFolder + "/NightSky.mat";
        public const string LightingSettingsPath = LightingFolder + "/EnvironmentLighting.lighting";
        public const string LightingRootName = "Lighting";
        public const string MoonName = "Moon";
        public const string PoweredLightsName = "PoweredLights";
        public const string ReflectionProbesName = "ReflectionProbes";
        public const string LightProbesName = "LightProbes";
        public const string LampMaterialFolder = "Assets/Materials/Environment/Lighting";

        // Reflection probes: 128 px cubemaps are enough for rough concrete and metal at a medium preset.
        public const int ReflectionProbeResolution = 128;
        public const float ReflectionBlendDistance = 0.5f;

        // ---- Lightmapping (see docs/level-layout.md, "Bake settings") ----------------------------------------------
        // Mixed lights in Baked Indirect mode: direct light and shadows stay real-time (so the blackout can switch the
        // lights off), and only the bounced light is baked. Bounce is soft, so 10 texels per metre is enough: a 0.4 m
        // wall gets 4 texels.
        public const float LightmapResolution = 10f;
        public const int LightmapPadding = 2;
        public const int LightmapMaxSize = 1024;
        public const int DirectSamples = 32;
        public const int IndirectSamples = 256;
        public const int EnvironmentSamples = 256;
        public const int LightProbeSampleMultiplier = 4;
        public const int Bounces = 2;
        public const float AoMaxDistance = 1f;

        private const string UndoLabel = "Setup Lighting";

        [MenuItem("Blackout/World/Setup Lighting")]
        public static void SetupLighting()
        {
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Setup Lighting cancelled (the Environment scene is not open).");
                return;
            }

            // Fog, ambient and the skybox belong to the active scene.
            SceneManager.SetActiveScene(scene);
            EnsureFolder();
            Transform lighting = WorldSceneSkeleton.FindRoot(scene, LightingRootName).transform;

            var report = new StringBuilder();
            Light moon = BuildMoon(lighting);
            Material sky = SaveSkyMaterial();
            ApplyRenderSettings(sky, moon);
            report.Append($"\n  sky, fog and ambient: fog #{Hex(LightingLayout.FogColour)} density {LightingLayout.FogDensity}, "
                + $"ambient sky #{Hex(LightingLayout.AmbientSky)} / equator #{Hex(LightingLayout.AmbientEquator)} / "
                + $"ground #{Hex(LightingLayout.AmbientGround)}, skybox {SkyMaterialPath}");
            report.Append($"\n  moon: Mixed, soft shadows, #{Hex(moon.color)} x {moon.intensity}, rotation {Euler(moon.transform.rotation)}");

            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();
            List<LightSpec> powered = LightFixtureLayout.Powered(layout);
            List<LightSpec> outdoor = LightFixtureLayout.Outdoor(layout);
            BuildLights(GeneratedObjects.FindOrCreateChild(lighting, PoweredLightsName, UndoLabel), powered, true);
            BuildLights(GeneratedObjects.FindOrCreateChild(lighting, LightFixtureLayout.OutdoorGroup, UndoLabel), outdoor, false);
            report.Append(DescribeLights("powered fixtures (Lighting/PoweredLights)", powered));
            report.Append(DescribeLights("outdoor lights (Lighting/OutdoorLights)", outdoor));

            report.Append(BuildReflectionProbes(lighting, ProbeLayout.ReflectionProbes(layout)));
            report.Append(BuildLightProbes(lighting, layout, GroundHeight(scene)));
            report.Append(ApplyStaticFlags(scene));

            LightingSettings settings = SaveLightingSettings();
            Lightmapping.SetLightingSettingsForScene(scene, settings);
            report.Append($"\n  lighting settings: {LightingSettingsPath}, {settings.lightmapper}, Baked Indirect, "
                + $"{settings.lightmapResolution} texels/m; bakes only from Generate Lighting (Unity 6.3 has no Auto Generate)");
            report.Append(UrpShadowReport());

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Lighting set up:{report}\n  Nothing was baked: generate lighting from Window > Rendering > Lighting.");
        }

        // ---- Moon -------------------------------------------------------------------------------------------------

        private static Light BuildMoon(Transform lighting)
        {
            Transform moon = GeneratedObjects.FindOrCreateChild(lighting, MoonName, UndoLabel);
            moon.SetPositionAndRotation(Vector3.zero, LightingLayout.MoonRotation);
            moon.localScale = Vector3.one;

            Light light = GeneratedObjects.GetOrAddComponent<Light>(moon.gameObject);
            light.type = LightType.Directional;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.color = LightingLayout.MoonColour;
            light.intensity = LightingLayout.MoonIntensity;
            light.bounceIntensity = 1f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 1f;

            // URP's per-light data. Its defaults are kept: shadow bias and resolution come from the URP asset.
            light.GetUniversalAdditionalLightData();
            return light;
        }

        // ---- Sky, fog, ambient ------------------------------------------------------------------------------------

        private static Material SaveSkyMaterial()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(SkyShaderPath);
            var material = AssetDatabase.LoadAssetAtPath<Material>(SkyMaterialPath);
            if (material == null)
            {
                material = new Material(shader) { name = "NightSky" };
                AssetDatabase.CreateAsset(material, SkyMaterialPath);
            }

            material.shader = shader;
            material.SetColor("_HorizonColor", LightingLayout.SkyHorizon);
            material.SetColor("_ZenithColor", LightingLayout.SkyZenith);
            material.SetVector("_MoonDirection", LightingLayout.TowardMoon);
            material.SetFloat("_CloudCover", LightingLayout.CloudCover);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ApplyRenderSettings(Material sky, Light moon)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = LightingLayout.FogColour;
            RenderSettings.fogDensity = LightingLayout.FogDensity;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = LightingLayout.AmbientSky;
            RenderSettings.ambientEquatorColor = LightingLayout.AmbientEquator;
            RenderSettings.ambientGroundColor = LightingLayout.AmbientGround;

            RenderSettings.skybox = sky;
            RenderSettings.sun = moon;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.defaultReflectionResolution = LightingLayout.SkyReflectionResolution;
            RenderSettings.reflectionIntensity = 1f;
            RenderSettings.reflectionBounces = 1;
        }

        // ---- Powered fixtures and outdoor lights ------------------------------------------------------------------

        // Builds every spec as "<group>_<name>". Bunker fixtures go one level down, in a container per room.
        private static void BuildLights(Transform root, List<LightSpec> specs, bool perRoom)
        {
            ResetTransform(root);
            var builtPerContainer = new Dictionary<Transform, HashSet<string>>();
            foreach (LightSpec spec in specs)
            {
                Transform container = perRoom ? GeneratedObjects.FindOrCreateChild(root, spec.Group, UndoLabel) : root;
                ResetTransform(container);
                if (!builtPerContainer.TryGetValue(container, out HashSet<string> built))
                {
                    built = new HashSet<string>();
                    builtPerContainer.Add(container, built);
                }

                BuildLight(container, spec, built);
            }

            foreach (KeyValuePair<Transform, HashSet<string>> pair in builtPerContainer)
            {
                GeneratedObjects.RemoveStale(pair.Key, pair.Value);
            }
        }

        private static void BuildLight(Transform container, LightSpec spec, ISet<string> built)
        {
            GameObject go = GeneratedObjects.GetOrCreate(container, spec.Name, built, UndoLabel);
            Quaternion rotation = spec.Type == LightType.Spot ? Quaternion.LookRotation(spec.Target - spec.Position) : Quaternion.identity;
            go.transform.SetPositionAndRotation(spec.Position, rotation);
            go.transform.localScale = Vector3.one;

            Light light = GeneratedObjects.GetOrAddComponent<Light>(go);
            light.type = spec.Type;
            light.lightmapBakeType = LightmapBakeType.Mixed;
            light.color = LightingLayout.FromLinearHex(spec.Hex);
            light.intensity = spec.Intensity;
            light.range = spec.Range;
            if (spec.Type == LightType.Spot)
            {
                light.spotAngle = spec.SpotAngle;
                light.innerSpotAngle = spec.InnerSpotAngle;
            }

            light.bounceIntensity = 1f;
            light.shadows = spec.Shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 1f;

            // URP keeps the shadow resolution tier on its own component: Low (256 px per cube face) for point lights.
            var urp = new SerializedObject(light.GetUniversalAdditionalLightData());
            urp.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = spec.ShadowTier;
            urp.ApplyModifiedPropertiesWithoutUndo();

            Renderer lamp = BuildLamp(go.transform, spec);
            var flicker = go.GetComponent<FlickerLight>();
            if (spec.Flickers)
            {
                if (flicker == null)
                {
                    flicker = go.AddComponent<FlickerLight>();
                }

                flicker.Configure(spec.Flicker, spec.FlickerSeed, lamp);
                EditorUtility.SetDirty(flicker);
            }
            else if (flicker != null)
            {
                Object.DestroyImmediate(flicker);
            }
        }

        // The lamp mesh hangs 0.25 m over its light; a high-bay also gets a rod up to the ceiling.
        private static Renderer BuildLamp(Transform fixture, LightSpec spec)
        {
            var built = new HashSet<string>();
            Renderer lamp = null;
            if (spec.Kind != FixtureKind.None)
            {
                Vector3 size = LightFixtureLayout.LampSize(spec);
                bool dome = spec.Kind == FixtureKind.Sodium;
                GameObject go = GeneratedObjects.GetOrCreate(fixture, "Lamp", built, UndoLabel,
                    dome ? PrimitiveType.Cylinder : PrimitiveType.Cube);
                go.transform.localPosition = new Vector3(0f, LightFixtureLayout.LightBelowLamp, 0f);
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = dome ? new Vector3(size.x, size.y * 0.5f, size.z) : size; // a cylinder is 2 m tall
                lamp = ConfigureLampPart(go, LampMaterial(spec.Kind));

                if (dome)
                {
                    float top = LightFixtureLayout.LightBelowLamp + size.y * 0.5f;
                    float rod = LightFixtureLayout.LampDrop(spec.Kind) - size.y * 0.5f;
                    GameObject rodGo = GeneratedObjects.GetOrCreate(fixture, "Rod", built, UndoLabel, PrimitiveType.Cylinder);
                    rodGo.transform.localPosition = new Vector3(0f, top + rod * 0.5f, 0f);
                    rodGo.transform.localRotation = Quaternion.identity;
                    rodGo.transform.localScale = new Vector3(0.05f, rod * 0.5f, 0.05f);
                    ConfigureLampPart(rodGo, AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath("MetalPlate")));
                }
            }

            GeneratedObjects.RemoveStale(fixture, built);
            return lamp;
        }

        // Lamps are small and not static: they take light from the light probes, cast no shadow, have no collider,
        // and W8 changes their emission at run time.
        private static Renderer ConfigureLampPart(GameObject go, Material material)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            return renderer;
        }

        /// <summary>
        /// Emissive URP Lit material for a lamp, at the reference's emission (section 3: tube #E6F0FF x 4.2, lab panel
        /// #E4F6FF x 3.2, sodium #FFA555 x 6), regenerated in full on every run. The emission is marked real-time, so it
        /// is never baked (real-time GI is off): the Light components do the lighting and the lamp only glows.
        /// </summary>
        public static Material LampMaterial(FixtureKind kind)
        {
            string name = kind == FixtureKind.Panel ? "Lamp_Panel" : kind == FixtureKind.Sodium ? "Lamp_Sodium" : "Lamp_Tube";
            Color emission = kind == FixtureKind.Panel ? LightingLayout.FromLinearHex("#E4F6FF", 3.2f)
                : kind == FixtureKind.Sodium ? LightingLayout.FromLinearHex("#FFA555", 6f)
                : LightingLayout.FromLinearHex("#E6F0FF", 4.2f);

            if (!AssetDatabase.IsValidFolder(LampMaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials/Environment", "Lighting");
            }

            string path = $"{LampMaterialFolder}/{name}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = lit;
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.5f);
            material.SetFloat("_Metallic", 0f);
            material.SetColor("_EmissionColor", emission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BaseShaderGUI.SetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        // One line per room (its fixtures are alike), or per light outdoors: reference intensity -> Unity intensity.
        private static string DescribeLights(string title, List<LightSpec> specs)
        {
            var lines = new StringBuilder();
            int shadows = 0;
            int flickers = 0;
            string lastGroup = null;
            foreach (LightSpec spec in specs)
            {
                shadows += spec.Shadows ? 1 : 0;
                flickers += spec.Flickers ? 1 : 0;
                if (spec.Group == LightFixtureLayout.OutdoorGroup)
                {
                    lines.Append($"\n    {spec.Name}: {spec.Type} at ({spec.Position.x:0.0}, {spec.Position.y:0.0}, {spec.Position.z:0.0}), "
                        + $"{spec.Hex} {spec.PrototypeIntensity} -> {spec.Intensity:0.0}, range {spec.Range} m");
                }
                else if (spec.Group != lastGroup)
                {
                    int count = specs.FindAll(s => s.Group == spec.Group).Count;
                    lines.Append($"\n    {spec.Group}: {count} x {spec.Kind}, {spec.Hex} {spec.PrototypeIntensity} -> "
                        + $"{spec.Intensity:0.0}, range {spec.Range} m, light {spec.Position.y - BunkerFloor(spec):0.00} m over the floor");
                }

                lastGroup = spec.Group;
            }

            return $"\n  {title}: {specs.Count} lights, {shadows} with shadows, {flickers} flickering{lines}";
        }

        private static float BunkerFloor(LightSpec spec)
        {
            Room room = BunkerLayoutAssetBuilder.LoadOrCreate().FindRoom(spec.Group);
            return BunkerShell.FloorY(room, spec.Position.z);
        }

        // ---- Probes -----------------------------------------------------------------------------------------------

        private static string BuildReflectionProbes(Transform lighting, List<ReflectionProbeSpec> specs)
        {
            Transform container = GeneratedObjects.FindOrCreateChild(lighting, ReflectionProbesName, UndoLabel);
            ResetTransform(container);
            var built = new HashSet<string>();
            foreach (ReflectionProbeSpec spec in specs)
            {
                GameObject go = GeneratedObjects.GetOrCreate(container, spec.Room, built, UndoLabel);
                go.transform.SetPositionAndRotation(spec.Position, Quaternion.identity);
                go.transform.localScale = Vector3.one;

                ReflectionProbe probe = GeneratedObjects.GetOrAddComponent<ReflectionProbe>(go);
                probe.mode = ReflectionProbeMode.Baked;
                probe.resolution = ReflectionProbeResolution;
                probe.hdr = true;
                probe.boxProjection = true;
                probe.center = spec.BoxCentre - spec.Position;
                probe.size = spec.BoxSize;
                probe.blendDistance = ReflectionBlendDistance;
                probe.importance = 1;
                probe.intensity = 1f;
                probe.clearFlags = ReflectionProbeClearFlags.Skybox;
                probe.nearClipPlane = 0.1f;
                probe.farClipPlane = 60f; // longer than the 41 m corridor
                probe.cullingMask = ~0;
            }

            GeneratedObjects.RemoveStale(container, built);
            return $"\n  reflection probes (Lighting/ReflectionProbes): {specs.Count}, one per room, baked, "
                + $"{ReflectionProbeResolution} px, box projection, blend {ReflectionBlendDistance} m";
        }

        private static string BuildLightProbes(Transform lighting, BunkerLayout layout, System.Func<float, float, float> ground)
        {
            Transform container = GeneratedObjects.FindOrCreateChild(lighting, LightProbesName, UndoLabel);
            ResetTransform(container);
            var built = new HashSet<string>();
            int bunker = SetProbeGroup(container, "Bunker", ProbeLayout.BunkerProbes(layout), built);
            int compound = SetProbeGroup(container, "Compound", ProbeLayout.CompoundProbes(layout, ground), built);
            int trail = SetProbeGroup(container, "Trail", ProbeLayout.TrailProbes(ground), built);
            GeneratedObjects.RemoveStale(container, built);
            return $"\n  light probes (Lighting/LightProbes): {bunker + compound + trail} (bunker {bunker}, compound {compound}, "
                + $"dock and trail {trail})";
        }

        // A Light Probe Group at the origin, so its local probe positions are world positions.
        private static int SetProbeGroup(Transform container, string part, List<Vector3> positions, ISet<string> built)
        {
            GameObject go = GeneratedObjects.GetOrCreate(container, part, built, UndoLabel);
            ResetTransform(go.transform);
            LightProbeGroup group = GeneratedObjects.GetOrAddComponent<LightProbeGroup>(go);
            group.probePositions = positions.ToArray();
            group.dering = true; // no dark rings next to the bright high-bays
            return positions.Count;
        }

        // Height of the painted terrain (what the player walks on), or the height field if there is no terrain.
        private static System.Func<float, float, float> GroundHeight(Scene scene)
        {
            GameObject terrainRoot = WorldSceneSkeleton.FindRoot(scene, "Terrain");
            Transform terrainObject = terrainRoot != null ? terrainRoot.transform.Find("IslandTerrain") : null;
            Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
            if (terrain == null)
            {
                return IslandHeightField.Sample;
            }

            return (x, z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;
        }

        // ---- Static flags -----------------------------------------------------------------------------------------

        private static string ApplyStaticFlags(Scene scene)
        {
            var counts = new List<string>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                int changed = 0;
                int total = 0;
                foreach (Transform child in root.transform)
                {
                    StaticEditorFlags? flags = StaticFlagRules.For(root.name, child.name);
                    if (!flags.HasValue)
                    {
                        continue;
                    }

                    foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                    {
                        changed += SetFlags(renderer.gameObject, flags.Value);
                        total++;
                    }

                    foreach (Terrain terrain in child.GetComponentsInChildren<Terrain>(true))
                    {
                        changed += SetFlags(terrain.gameObject, flags.Value);
                        total++;
                    }
                }

                if (total > 0)
                {
                    counts.Add($"{root.name} {total} ({changed} changed)");
                }
            }

            return $"\n  static flags: {string.Join(", ", counts)}; the water is see-through static, the boat dynamic";
        }

        private static int SetFlags(GameObject go, StaticEditorFlags flags)
        {
            if (GameObjectUtility.GetStaticEditorFlags(go) == flags)
            {
                return 0;
            }

            GameObjectUtility.SetStaticEditorFlags(go, flags);
            return 1;
        }

        private static void ResetTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        // ---- Lighting Settings asset ------------------------------------------------------------------------------

        private static LightingSettings SaveLightingSettings()
        {
            var settings = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingSettingsPath);
            if (settings == null)
            {
                settings = new LightingSettings { name = "EnvironmentLighting" };
                Configure(settings);
                AssetDatabase.CreateAsset(settings, LightingSettingsPath);
                return settings;
            }

            Configure(settings);
            EditorUtility.SetDirty(settings);
            return settings;
        }

        /// <summary>
        /// Every value the Lighting window shows for this scene, for a medium-quality bake. There is no Auto Generate to
        /// turn off: Unity 6.3 removed it (LightingSettings.autoGenerate is obsolete and ignored).
        /// </summary>
        public static void Configure(LightingSettings settings)
        {
            settings.bakedGI = true;
            settings.realtimeGI = false;
            settings.mixedBakeMode = MixedLightingMode.IndirectOnly;
            settings.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU; // falls back to CPU if the GPU cannot bake
            settings.prioritizeView = false;

            settings.lightmapResolution = LightmapResolution;
            settings.lightmapPadding = LightmapPadding;
            settings.lightmapMaxSize = LightmapMaxSize;
            settings.lightmapCompression = LightmapCompression.NormalQuality;
            settings.directionalityMode = LightmapsMode.NonDirectional;
            settings.albedoBoost = 1f;
            settings.indirectScale = 1f;

            settings.directSampleCount = DirectSamples;
            settings.indirectSampleCount = IndirectSamples;
            settings.environmentSampleCount = EnvironmentSamples;
            settings.lightProbeSampleCountMultiplier = LightProbeSampleMultiplier;
            settings.maxBounces = Bounces;
            settings.minBounces = Bounces;

            settings.ao = true;
            settings.aoMaxDistance = AoMaxDistance;
            settings.aoExponentIndirect = 1f;
            settings.aoExponentDirect = 0f;

            settings.filteringMode = LightingSettings.FilterMode.Advanced;
            settings.denoiserTypeDirect = LightingSettings.DenoiserType.OpenImage;
            settings.denoiserTypeIndirect = LightingSettings.DenoiserType.OpenImage;
            settings.denoiserTypeAO = LightingSettings.DenoiserType.OpenImage;
            settings.filterTypeDirect = LightingSettings.FilterType.Gaussian;
            settings.filterTypeIndirect = LightingSettings.FilterType.Gaussian;
            settings.filterTypeAO = LightingSettings.FilterType.Gaussian;
            settings.filteringGaussianRadiusDirect = 1;
            settings.filteringGaussianRadiusIndirect = 5;
            settings.filteringGaussianRadiusAO = 2;
        }

        // ---- URP shadows (report only) ----------------------------------------------------------------------------

        private static string UrpShadowReport()
        {
            var asset = QualitySettings.renderPipeline as UniversalRenderPipelineAsset
                ?? GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
            if (asset == null)
            {
                return "\n  URP shadows: no URP asset is active.";
            }

            List<string> advice = ShadowAdvice(asset.shadowDistance, asset.shadowCascadeCount, asset.cascade2Split,
                asset.mainLightShadowmapResolution, asset.supportsSoftShadows);
            string current = $"{asset.name}: distance {asset.shadowDistance:0} m, {asset.shadowCascadeCount} cascades, "
                + $"main light {asset.mainLightShadowmapResolution} px, soft shadows {(asset.supportsSoftShadows ? "on" : "off")}";
            return advice.Count == 0
                ? $"\n  URP shadows ({current}): match the medium preset."
                : $"\n  URP shadows ({current}), not changed by this tool. For a medium preset ask its owner to set: {string.Join(", ", advice)}.";
        }

        /// <summary>The URP shadow settings that differ from the medium preset in <see cref="LightingLayout"/>.</summary>
        public static List<string> ShadowAdvice(float distance, int cascades, float split, int resolution, bool soft)
        {
            var advice = new List<string>();
            if (!Mathf.Approximately(distance, LightingLayout.ShadowDistance))
            {
                advice.Add($"Max Distance {LightingLayout.ShadowDistance:0}");
            }

            if (cascades != LightingLayout.ShadowCascades || !Mathf.Approximately(split, LightingLayout.ShadowCascadeSplit))
            {
                advice.Add($"Cascade Count {LightingLayout.ShadowCascades} (split {LightingLayout.ShadowCascadeSplit * 100f:0} %)");
            }

            if (resolution != LightingLayout.MainShadowResolution)
            {
                advice.Add($"Main Light Shadow Resolution {LightingLayout.MainShadowResolution}");
            }

            if (!soft)
            {
                advice.Add("Soft Shadows on");
            }

            return advice;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LightingFolder))
            {
                AssetDatabase.CreateFolder("Assets/Environment", "Lighting");
            }
        }

        private static string Hex(Color colour)
        {
            return ColorUtility.ToHtmlStringRGB(colour);
        }

        private static string Euler(Quaternion rotation)
        {
            Vector3 e = rotation.eulerAngles;
            return $"({e.x:0.0}, {e.y:0.0}, {e.z:0.0})";
        }
    }
}
