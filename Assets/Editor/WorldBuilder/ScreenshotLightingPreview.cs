using System;
using Blackout.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Samples a settled blackout on disposable scene instances in Edit mode, without raising gameplay
    /// events. Reads the existing component's wiring and profile; ScreenshotCapture unloads it without saving.</summary>
    internal static class ScreenshotLightingPreview
    {
        private static readonly int EmissionId = Shader.PropertyToID("_EmissionColor");

        internal static string ApplyBlackout(Scene scene)
        {
            GameObject lighting = WorldSceneSkeleton.FindRoot(scene, LightingSetup.LightingRootName);
            PowerStateLighting power = lighting != null ? lighting.GetComponent<PowerStateLighting>() : null;
            if (power == null) throw new InvalidOperationException("Environment needs Setup Blackout Lighting before capture.");
            using var serialized = new SerializedObject(power);
            var profile = Reference<LightingStateProfile>(serialized.FindProperty("profile"));
            if (profile == null) throw new InvalidOperationException("PowerStateLighting has no lighting profile.");

            float seconds = Mathf.Max(6f, Mathf.Max(profile.cascadeSeconds,
                Mathf.Max(profile.transitionSeconds, Mathf.Max(profile.screenFadeSeconds,
                    profile.emergencyDelaySeconds + profile.emergencyFadeSeconds))) + 1f);
            var block = new MaterialPropertyBlock();
            ApplyFixtures(serialized.FindProperty("poweredFixtures"), 0f, Color.black, false, Quaternion.identity, block);
            float pulse = profile.EmergencyPulse(seconds);
            ApplyFixtures(serialized.FindProperty("emergencyLights"), pulse,
                profile.emergencyLampEmission.linear * pulse, true, Quaternion.identity, block, true);
            ApplyFixtures(serialized.FindProperty("alarmBeacons"), 1f,
                profile.alarmDomeEmission.linear * profile.AlarmDomePulse(seconds), true,
                Quaternion.AngleAxis(profile.AlarmAngle(seconds), Vector3.up), block);

            SerializedProperty screens = serialized.FindProperty("screens");
            for (int i = 0; i < screens.arraySize; i++)
                SetEmission(Reference<Renderer>(screens.GetArrayElementAtIndex(i)), Color.black, block);

            LightingStateProfile.State state = profile.Evaluate(1f);
            RenderSettings.fogColor = state.fogColor;
            RenderSettings.fogDensity = state.fogDensity;
            RenderSettings.ambientSkyColor = state.ambientSky;
            RenderSettings.ambientEquatorColor = state.ambientEquator;
            RenderSettings.ambientGroundColor = state.ambientGround;
            Light moon = Reference<Light>(serialized.FindProperty("moon"));
            if (moon != null) moon.intensity = state.directionalIntensity;
            SerializedProperty probes = serialized.FindProperty("bunkerProbes");
            for (int i = 0; i < probes.arraySize; i++)
            {
                ReflectionProbe probe = Reference<ReflectionProbe>(probes.GetArrayElementAtIndex(i));
                if (probe != null) probe.intensity = state.reflectionIntensity;
            }
            Volume volume = Reference<Volume>(serialized.FindProperty("blackoutVolume"));
            if (volume != null) volume.weight = 1f;
            return $"Blackout preview: settled at {seconds:0.###} s; existing profile, fixtures, screens and volume; no gameplay events.";
        }

        private static void ApplyFixtures(SerializedProperty fixtures, float intensity, Color emission,
            bool enabled, Quaternion spin, MaterialPropertyBlock block, bool includeUnlitGlow = false)
        {
            for (int i = 0; i < fixtures.arraySize; i++)
            {
                SerializedProperty fixture = fixtures.GetArrayElementAtIndex(i);
                Light light = Reference<Light>(fixture.FindPropertyRelative("light"));
                if (light != null)
                {
                    light.enabled = enabled;
                    light.intensity *= intensity;
                    light.transform.rotation = spin * light.transform.rotation;
                }
                Renderer lamp = Reference<Renderer>(fixture.FindPropertyRelative("lamp"));
                Color colour = emission;
                // Runtime interpolates the emergency lamp's unlit emission toward its lit colour by the pulse.
                if (includeUnlitGlow && lamp != null && lamp.sharedMaterial != null && lamp.sharedMaterial.HasProperty(EmissionId))
                    colour += lamp.sharedMaterial.GetColor(EmissionId).linear * (1f - intensity);
                SetEmission(lamp, colour, block);
            }
        }

        private static T Reference<T>(SerializedProperty property) where T : UnityEngine.Object
        {
            if (property == null) throw new InvalidOperationException("PowerStateLighting wiring changed; update the screenshot preview field names.");
            return property.objectReferenceValue as T;
        }

        private static void SetEmission(Renderer renderer, Color linear, MaterialPropertyBlock block)
        {
            if (renderer == null) return;
            block.Clear();
            renderer.GetPropertyBlock(block);
            block.SetColor(EmissionId, linear.gamma);
            renderer.SetPropertyBlock(block);
        }
    }
}
