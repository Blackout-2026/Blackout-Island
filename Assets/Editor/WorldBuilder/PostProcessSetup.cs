using System.Collections.Generic;
using Blackout.Environment;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Setup Post Processing: the W9 post-processing of Environment.unity, from <see cref="PostProcessLayout"/>.
    /// Run Blackout/World/Setup Blackout Lighting (W8) first.
    /// - Assets/Environment/Volumes/Powered.asset and Blackout.asset: Volume profiles with ACES tonemapping, colour
    ///   adjustments, bloom, vignette and film grain. Their overrides are rewritten on every run; change the numbers in
    ///   PostProcessLayout.cs, not on the assets. Overrides someone adds by hand to other effects are kept.
    /// - Volumes/Volumes_Powered (global, priority 0, weight 1) and Volumes/Volumes_Blackout (global, priority 1, saved at
    ///   weight 0). PowerStateLighting on the Lighting root drives the Blackout weight, so the camera sees
    ///   lerp(Powered, Blackout, blend), inside the bunker only.
    /// - A <see cref="QualityPreset"/> on the Volumes root: it turns post-processing on for the player's camera at run time.
    /// Re-runnable and deterministic: the profiles keep their effects and the volumes are reused by name. It never deletes
    /// anything.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.PostProcessSetup.SetupPostProcessing
    /// </summary>
    public static class PostProcessSetup
    {
        private const string UndoLabel = "Setup Post Processing";

        [MenuItem("Blackout/World/Setup Post Processing")]
        public static void SetupPostProcessing()
        {
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Setup Post Processing cancelled (the Environment scene is not open).");
                return;
            }

            GameObject lighting = WorldSceneSkeleton.FindRoot(scene, LightingSetup.LightingRootName);
            PowerStateLighting power = lighting != null ? lighting.GetComponent<PowerStateLighting>() : null;
            if (power == null)
            {
                Debug.LogError("[World] Setup Post Processing: run Blackout/World/Setup Blackout Lighting first "
                    + "(the Lighting root has no PowerStateLighting).");
                return;
            }

            VolumeProfile poweredProfile = SaveProfile(PostProcessLayout.PoweredProfilePath, PostProcessLayout.Powered);
            VolumeProfile blackoutProfile = SaveProfile(PostProcessLayout.BlackoutProfilePath, PostProcessLayout.Blackout);

            Transform volumes = WorldSceneSkeleton.FindRoot(scene, PostProcessLayout.VolumesRootName).transform;
            Volume powered = BuildVolume(volumes, PostProcessLayout.PoweredPart, poweredProfile, PostProcessLayout.PoweredPriority, 1f);
            Volume blackout = BuildVolume(volumes, PostProcessLayout.BlackoutPart, blackoutProfile, PostProcessLayout.BlackoutPriority, 0f);

            power.SetVolume(blackout);
            EditorUtility.SetDirty(power);
            GeneratedObjects.GetOrAddComponent<QualityPreset>(volumes.gameObject);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            PostLook p = PostProcessLayout.Powered;
            PostLook b = PostProcessLayout.Blackout;
            Debug.Log("[World] Post-processing set up:"
                + $"\n  {powered.name}: {PostProcessLayout.PoweredProfilePath}, priority {powered.priority}, weight {powered.weight}: "
                + $"ACES, post exposure {p.PostExposure:+0.00;-0.00} EV, bloom threshold {p.BloomThreshold:0.000} / intensity "
                + $"{p.BloomIntensity:0.00} / scatter {p.BloomScatter} / {p.BloomMaxIterations} mips, vignette {p.VignetteIntensity} / "
                + $"smoothness {p.VignetteSmoothness} rounded, film grain Thin1 {p.GrainIntensity}"
                + $"\n  {blackout.name}: {PostProcessLayout.BlackoutProfilePath}, priority {blackout.priority}, weight {blackout.weight} "
                + $"(PowerStateLighting drives it): the same, with vignette {b.VignetteIntensity}, contrast {b.Contrast:+0;-0}, "
                + $"film grain {b.GrainIntensity}"
                + $"\n  QualityPreset on {volumes.name}: turns post-processing on for the main camera at run time (Low turns it off)");
        }

        /// <summary>
        /// Writes a look into a profile: one override per value, in a fixed set of effects that is created once and then
        /// reused, so a re-run with the same numbers leaves the asset unchanged.
        /// </summary>
        public static void ApplyLook(VolumeProfile profile, PostLook look)
        {
            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.ACES);

            ColorAdjustments colour = GetOrAdd<ColorAdjustments>(profile);
            colour.postExposure.Override(look.PostExposure);
            colour.contrast.Override(look.Contrast);
            colour.saturation.Override(look.Saturation);
            colour.colorFilter.Override(Color.white);

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(look.BloomThreshold);
            bloom.intensity.Override(look.BloomIntensity);
            bloom.scatter.Override(look.BloomScatter);
            bloom.maxIterations.Override(look.BloomMaxIterations);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.color.Override(Color.black);
            vignette.intensity.Override(look.VignetteIntensity);
            vignette.smoothness.Override(look.VignetteSmoothness);
            vignette.rounded.Override(true);

            FilmGrain grain = GetOrAdd<FilmGrain>(profile);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(look.GrainIntensity);
            grain.response.Override(look.GrainResponse);
        }

        private static VolumeProfile SaveProfile(string path, PostLook look)
        {
            if (!AssetDatabase.IsValidFolder(PostProcessLayout.VolumesFolder))
            {
                AssetDatabase.CreateFolder("Assets/Environment", "Volumes");
            }

            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, path);
            }

            ApplyLook(profile, look);
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // An effect lives in the profile asset as a hidden sub-asset, so a new one is added to the asset file too.
        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>();
            if (AssetDatabase.Contains(profile))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            return component;
        }

        private static Volume BuildVolume(Transform root, string part, VolumeProfile profile, float priority, float weight)
        {
            GameObject go = GeneratedObjects.GetOrCreate(root, part, new HashSet<string>(), UndoLabel);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            Volume volume = GeneratedObjects.GetOrAddComponent<Volume>(go);
            volume.isGlobal = true;
            volume.priority = priority;
            volume.weight = weight;
            volume.sharedProfile = profile;
            EditorUtility.SetDirty(volume);
            return volume;
        }
    }
}
