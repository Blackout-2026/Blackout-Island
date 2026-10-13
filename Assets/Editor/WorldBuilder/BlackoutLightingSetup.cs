using System.Collections.Generic;
using System.Text;
using Blackout.Environment;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Fixture = Blackout.Environment.PowerStateLighting.Fixture;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Setup Blackout Lighting: what Environment.unity needs for the W8 blackout, from
    /// <see cref="BlackoutLightLayout"/>. Run Blackout/World/Setup Lighting (W7) first.
    /// - Lighting/BlackoutLights/&lt;Room&gt;: 14 red emergency lights, each with a lamp box on the wall, and 3 alarm beacons
    ///   (spot lights with a dome). They are Realtime, because a Mixed light would bake its red bounce into the powered
    ///   lightmaps, and they are saved switched off: <see cref="PowerStateLighting"/> turns them on at the blackout.
    /// - Assets/Environment/Lighting/LightingStateProfile.asset: the powered and blackout states and the lamp colours are
    ///   rewritten from the reference on every run. Timings tuned on the asset are kept.
    /// - A <see cref="PowerStateLighting"/> on the Lighting root, wired to the GeneratorDisabled and PlayerRespawned
    ///   channels, the profile, the 31 powered fixtures and their lamps, the blackout lights, the moon, the 6 reflection
    ///   probes, the bunker's volumes and the cascade origin. Screens are left as they are (none are built yet).
    /// - Lamp_Emergency and Lamp_Alarm materials in Assets/Materials/Environment/Lighting, showing the unlit glow.
    /// Re-runnable and deterministic: objects are reused by name, and it removes only objects it generated under
    /// Lighting/BlackoutLights. It never bakes.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.BlackoutLightingSetup.SetupBlackoutLighting
    /// </summary>
    public static class BlackoutLightingSetup
    {
        public const string ProfilePath = LightingSetup.LightingFolder + "/LightingStateProfile.asset";
        public const string EventsFolder = "Assets/ScriptableObjects/Events";
        public const string GeneratorDisabledPath = EventsFolder + "/GeneratorDisabled.asset";
        public const string PlayerRespawnedPath = EventsFolder + "/PlayerRespawned.asset";

        private const string UndoLabel = "Setup Blackout Lighting";
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [MenuItem("Blackout/World/Setup Blackout Lighting")]
        public static void SetupBlackoutLighting()
        {
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Setup Blackout Lighting cancelled (the Environment scene is not open).");
                return;
            }

            Transform lighting = WorldSceneSkeleton.FindRoot(scene, LightingSetup.LightingRootName).transform;
            Transform poweredRoot = lighting.Find(LightingSetup.PoweredLightsName);
            Transform moonObject = lighting.Find(LightingSetup.MoonName);
            Transform probesRoot = lighting.Find(LightingSetup.ReflectionProbesName);
            if (poweredRoot == null || moonObject == null || probesRoot == null)
            {
                Debug.LogError("[World] Setup Blackout Lighting: run Blackout/World/Setup Lighting first "
                    + "(Lighting/PoweredLights, Lighting/Moon or Lighting/ReflectionProbes is missing).");
                return;
            }

            var generatorDisabled = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(GeneratorDisabledPath);
            var playerRespawned = AssetDatabase.LoadAssetAtPath<VoidEventChannel>(PlayerRespawnedPath);
            if (generatorDisabled == null || playerRespawned == null)
            {
                Debug.LogError($"[World] Setup Blackout Lighting: the event channels are missing from {EventsFolder}. "
                    + "Pull develop; never re-create them (playbook trap 1).");
                return;
            }

            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();
            List<BlackoutLightSpec> emergencySpecs = BlackoutLightLayout.Emergency(layout);
            List<BlackoutLightSpec> alarmSpecs = BlackoutLightLayout.Alarms(layout);
            Transform blackoutRoot = GeneratedObjects.FindOrCreateChild(lighting, BlackoutLightLayout.BlackoutGroup, UndoLabel);
            ResetTransform(blackoutRoot);

            var builtPerRoom = new Dictionary<Transform, HashSet<string>>();
            Material lampMaterial = SaveMaterial("Lamp_Emergency", BlackoutLightLayout.LampBaseHex, BlackoutLightLayout.EmergencyLampUnlit, 0.5f);
            Material domeMaterial = SaveMaterial("Lamp_Alarm", BlackoutLightLayout.DomeBaseHex, BlackoutLightLayout.AlarmDomeUnlit, 0.8f);
            Fixture[] emergency = BuildLights(blackoutRoot, emergencySpecs, builtPerRoom, lampMaterial, PrimitiveType.Cube,
                "Lamp", BlackoutLightLayout.LampSize);
            Fixture[] alarms = BuildLights(blackoutRoot, alarmSpecs, builtPerRoom, domeMaterial, PrimitiveType.Sphere,
                "Dome", Vector3.one * BlackoutLightLayout.DomeDiameter);
            foreach (KeyValuePair<Transform, HashSet<string>> pair in builtPerRoom)
            {
                GeneratedObjects.RemoveStale(pair.Key, pair.Value);
            }

            LightingStateProfile profile = SaveProfile();
            Fixture[] powered = PoweredFixtures(poweredRoot);
            ReflectionProbe[] probes = probesRoot.GetComponentsInChildren<ReflectionProbe>(true);
            Vector3 origin = BlackoutLightLayout.CascadeOrigin(layout);
            Bounds underground = BlackoutLightLayout.Underground(layout);
            Bounds entrance = BlackoutLightLayout.Entrance(layout);

            PowerStateLighting power = GeneratedObjects.GetOrAddComponent<PowerStateLighting>(lighting.gameObject);
            power.Configure(profile, generatorDisabled, playerRespawned);
            power.SetLights(powered, emergency, alarms, moonObject.GetComponent<Light>(), probes);
            power.SetArea(origin, underground, entrance);
            EditorUtility.SetDirty(power);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            var report = new StringBuilder();
            report.Append($"\n  emergency lights (Lighting/BlackoutLights): {emergency.Length}, Realtime, off, "
                + $"{BlackoutLightLayout.EmergencyHex} {BlackoutLightLayout.EmergencyPrototypeIntensity} -> {BlackoutLightLayout.EmergencyIntensity:0.00}, "
                + $"range {BlackoutLightLayout.EmergencyRange} m{PerRoom(emergencySpecs)}");
            report.Append($"\n  alarm beacons: {alarms.Length}, Realtime spots, off, {BlackoutLightLayout.AlarmHex} "
                + $"{BlackoutLightLayout.AlarmPrototypeIntensity} -> {BlackoutLightLayout.AlarmIntensity:0.0}, range {BlackoutLightLayout.AlarmRange} m, "
                + $"spot {alarmSpecs[0].SpotAngle:0.0} / inner {alarmSpecs[0].InnerSpotAngle:0.0} deg, tilted {BlackoutLightLayout.AlarmTilt:0.0} deg down, "
                + $"Low-tier soft shadows{PerRoom(alarmSpecs)}");
            report.Append($"\n  profile {ProfilePath}: fog density {profile.powered.fogDensity} -> {profile.blackout.fogDensity}, "
                + $"fog #{Hex(profile.powered.fogColor)} -> #{Hex(profile.blackout.fogColor)}, ambient sky #{Hex(profile.powered.ambientSky)} -> "
                + $"#{Hex(profile.blackout.ambientSky)}, moon {profile.powered.directionalIntensity} -> {profile.blackout.directionalIntensity}, "
                + $"reflections {profile.powered.reflectionIntensity} -> {profile.blackout.reflectionIntensity}; cascade {profile.cascadeSeconds} s, "
                + $"fog {profile.transitionSeconds} s, emergency from {profile.emergencyDelaySeconds} s over {profile.emergencyFadeSeconds} s");
            report.Append($"\n  PowerStateLighting on Lighting: {powered.Length} powered fixtures ({CountLamps(powered)} lamps), "
                + $"{probes.Length} reflection probes, cascade origin {Vec(origin)}, underground {Vec(underground.min)} .. {Vec(underground.max)}, "
                + $"entrance {Vec(entrance.min)} .. {Vec(entrance.max)}, channels {generatorDisabled.name} and {playerRespawned.name}");
            Debug.Log($"[World] Blackout lighting set up:{report}\n  Test it in Play mode: right-click PowerStateLighting > Trigger Blackout.");
        }

        // ---- Lights -----------------------------------------------------------------------------------------------

        // Builds each spec as Lighting/BlackoutLights/<Room>/<Room>_<Name>, with its lamp as "<light>_<lampPart>".
        private static Fixture[] BuildLights(Transform root, List<BlackoutLightSpec> specs,
            Dictionary<Transform, HashSet<string>> builtPerRoom, Material material, PrimitiveType shape, string lampPart, Vector3 lampSize)
        {
            var fixtures = new Fixture[specs.Count];
            for (int i = 0; i < specs.Count; i++)
            {
                BlackoutLightSpec spec = specs[i];
                Transform room = GeneratedObjects.FindOrCreateChild(root, spec.Group, UndoLabel);
                ResetTransform(room);
                if (!builtPerRoom.TryGetValue(room, out HashSet<string> built))
                {
                    built = new HashSet<string>();
                    builtPerRoom.Add(room, built);
                }

                GameObject go = GeneratedObjects.GetOrCreate(room, spec.Name, built, UndoLabel);
                go.transform.SetPositionAndRotation(spec.Position, spec.Rotation);
                go.transform.localScale = Vector3.one;
                fixtures[i] = new Fixture { light = ConfigureLight(go, spec), lamp = BuildLamp(go.transform, spec, material, shape, lampPart, lampSize) };
            }

            return fixtures;
        }

        private static Light ConfigureLight(GameObject go, BlackoutLightSpec spec)
        {
            Light light = GeneratedObjects.GetOrAddComponent<Light>(go);
            light.type = spec.Type;
            light.lightmapBakeType = LightmapBakeType.Realtime;
            light.color = LightingLayout.FromLinearHex(spec.Hex);
            light.intensity = spec.Intensity;
            light.range = spec.Range;
            if (spec.Type == LightType.Spot)
            {
                light.spotAngle = spec.SpotAngle;
                light.innerSpotAngle = spec.InnerSpotAngle;
            }

            light.shadows = spec.Shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 1f;
            var urp = new SerializedObject(light.GetUniversalAdditionalLightData());
            urp.FindProperty("m_AdditionalLightsShadowResolutionTier").intValue = spec.ShadowTier;
            urp.ApplyModifiedPropertiesWithoutUndo();

            // Saved off, so the Scene view and any bake show the powered bunker. PowerStateLighting turns it on.
            light.enabled = false;
            return light;
        }

        // The lamp is small and not static: no collider, no shadows, and its emission changes at run time.
        private static Renderer BuildLamp(Transform light, BlackoutLightSpec spec, Material material, PrimitiveType shape,
            string part, Vector3 size)
        {
            var built = new HashSet<string>();
            GameObject go = GeneratedObjects.GetOrCreate(light, part, built, UndoLabel, shape);
            go.transform.SetPositionAndRotation(spec.LampPosition, spec.Rotation);
            go.transform.localScale = size;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.DestroyImmediate(collider);
            }

            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off; // the dome sits round its own spot light
            renderer.receiveShadows = false;
            GameObjectUtility.SetStaticEditorFlags(go, 0);
            GeneratedObjects.RemoveStale(light, built);
            return renderer;
        }

        // Every W7 fixture under Lighting/PoweredLights, in hierarchy order, with its "<fixture>_Lamp" mesh.
        private static Fixture[] PoweredFixtures(Transform poweredRoot)
        {
            Light[] lights = poweredRoot.GetComponentsInChildren<Light>(true);
            var fixtures = new Fixture[lights.Length];
            for (int i = 0; i < lights.Length; i++)
            {
                Transform lamp = lights[i].transform.Find(GeneratedObjects.Name(lights[i].transform, "Lamp"));
                fixtures[i] = new Fixture { light = lights[i], lamp = lamp != null ? lamp.GetComponent<Renderer>() : null };
            }

            return fixtures;
        }

        // ---- Assets -----------------------------------------------------------------------------------------------

        /// <summary>
        /// An emissive URP Lit material showing a lamp's unlit glow, regenerated in full on every run. The emission is
        /// real-time (never baked), and never black, so URP keeps emission on and the lamp can light up at run time.
        /// </summary>
        public static Material SaveMaterial(string name, string baseHex, Color unlitEmission, float smoothness)
        {
            if (!AssetDatabase.IsValidFolder(LightingSetup.LampMaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/Materials/Environment", "Lighting");
            }

            string path = $"{LightingSetup.LampMaterialFolder}/{name}.mat";
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(lit) { name = name };
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = lit;
            ColorUtility.TryParseHtmlString(baseHex, out Color baseColour);
            material.SetColor("_BaseColor", baseColour); // a surface colour (albedo), so the hex is used as sRGB
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", 0f);
            material.SetColor(EmissionColorId, unlitEmission);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            BaseShaderGUI.SetMaterialKeywords(material);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static LightingStateProfile SaveProfile()
        {
            var profile = AssetDatabase.LoadAssetAtPath<LightingStateProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<LightingStateProfile>();
                profile.name = "LightingStateProfile";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            profile.powered = BlackoutLightLayout.Powered;
            profile.blackout = BlackoutLightLayout.Blackout;
            profile.emergencyLampEmission = BlackoutLightLayout.EmergencyLampOn;
            profile.alarmDomeEmission = BlackoutLightLayout.AlarmDomeOn;
            EditorUtility.SetDirty(profile);
            return profile;
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        private static string PerRoom(List<BlackoutLightSpec> specs)
        {
            var counts = new List<string>();
            string last = null;
            foreach (BlackoutLightSpec spec in specs)
            {
                if (spec.Group != last)
                {
                    counts.Add($"{spec.Group} {specs.FindAll(s => s.Group == spec.Group).Count}");
                    last = spec.Group;
                }
            }

            return " (" + string.Join(", ", counts) + ")";
        }

        private static int CountLamps(Fixture[] fixtures)
        {
            int count = 0;
            foreach (Fixture fixture in fixtures)
            {
                count += fixture.lamp != null ? 1 : 0;
            }

            return count;
        }

        private static void ResetTransform(Transform transform)
        {
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
        }

        private static string Hex(Color colour)
        {
            return ColorUtility.ToHtmlStringRGB(colour);
        }

        private static string Vec(Vector3 v)
        {
            return $"({v.x:0.0}, {v.y:0.0}, {v.z:0.0})";
        }
    }
}
