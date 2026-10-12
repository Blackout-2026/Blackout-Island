using System.Collections.Generic;
using System.Text;
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
    /// - Assets/Environment/Lighting/EnvironmentLighting.lighting: the scene's Lighting Settings asset, with Auto
    ///   Generate off (only the World Builder bakes, from Window > Rendering > Lighting).
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

            LightingSettings settings = SaveLightingSettings();
            Lightmapping.SetLightingSettingsForScene(scene, settings);
            report.Append($"\n  lighting settings: {LightingSettingsPath}, auto generate {(settings.autoGenerate ? "ON" : "off")}, "
                + $"{settings.lightmapper}, Baked Indirect, {settings.lightmapResolution} texels/m");
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

        /// <summary>Every value the Lighting window shows for this scene, for a medium-quality bake.</summary>
        public static void Configure(LightingSettings settings)
        {
            settings.autoGenerate = false;
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
