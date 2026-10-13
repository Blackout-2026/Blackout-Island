using System;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Environment
{
    /// <summary>
    /// Plays the bunker blackout (W8) when the GeneratorDisabled channel is raised, from a <see cref="LightingStateProfile"/>:
    /// 1. the powered fixtures strobe and die one by one, outward from the generator, within about 1.5 s, and their
    ///    lamps go dark with them; the screens fade out over 0.4 s;
    /// 2. fog, ambient, the moon and the bunker's reflection probes move to the blackout state over about 2 s;
    /// 3. after a moment of darkness the red emergency lights fade in and pulse, and the alarm beacons start turning.
    /// - Event-driven: it listens to two VoidEventChannel assets and never references the generator. The cascade
    ///   starts from a fixed point above the generator socket.
    /// - The generator powers only the bunker, as in the prototype, so the outdoor lights stay on and the blackout fog
    ///   and ambient are weighted by how far inside the bunker the main camera is: 1 in the underground rooms, rising
    ///   along the lobby and ramp, 0 outside. With no main camera (Environment played on its own) it counts as inside.
    /// - It writes RenderSettings only during a blackout and when it resets, so the scene's own (W7) values stay in
    ///   charge until the event. Environment must be the active scene (playbook trap 7).
    /// - PlayerRespawned puts the power back only while the generator is still on: a respawn undoes a preview from
    ///   the context menu, never the real blackout.
    /// Blackout/World/Setup Blackout Lighting adds it to the Lighting root and fills every field. Nothing is allocated
    /// per frame. W9 can read <see cref="Blend"/> to blend the post-processing volumes.
    /// Owner: World Builder.
    /// </summary>
    [DisallowMultipleComponent]
    public class PowerStateLighting : MonoBehaviour
    {
        /// <summary>A light and, optionally, the lamp mesh that glows with it.</summary>
        [Serializable]
        public struct Fixture
        {
            public Light light;
            public Renderer lamp;
        }

        // What a fixture looked like before the blackout, and how far it has got.
        private struct FixtureState
        {
            public float intensity;
            public Quaternion rotation;
            public Color lampLinear; // the lamp material's emission, in linear space
            public FlickerLight flicker;
            public float offTime;
            public bool off;
        }

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Header("Events and profile")]
        [SerializeField] private VoidEventChannel generatorDisabled;
        [SerializeField] private VoidEventChannel playerRespawned;
        [SerializeField] private LightingStateProfile profile;

        [Header("Powered by the generator")]
        [Tooltip("The bunker fixtures (Lighting/PoweredLights). They die one by one, nearest the cascade origin first.")]
        [SerializeField] private Fixture[] poweredFixtures = new Fixture[0];

        [Tooltip("Where the cascade starts: 2 m above the generator socket.")]
        [SerializeField] private Vector3 cascadeOrigin;

        [Tooltip("Monitor screens and other renderers whose emission (_EmissionColor) fades out at the event.")]
        [SerializeField] private Renderer[] screens = new Renderer[0];

        [Header("Blackout lights (Lighting/BlackoutLights)")]
        [Tooltip("Red point lights with their lamps. Off until the blackout.")]
        [SerializeField] private Fixture[] emergencyLights = new Fixture[0];

        [Tooltip("Red spot lights that turn about the world's up axis, with their domes as lamps. Off until the blackout.")]
        [SerializeField] private Fixture[] alarmBeacons = new Fixture[0];

        [Header("Global lighting")]
        [SerializeField] private Light moon;
        [SerializeField] private ReflectionProbe[] bunkerProbes = new ReflectionProbe[0];

        [Tooltip("The underground rooms: the full blackout look.")]
        [SerializeField] private Bounds bunker;

        [Tooltip("The lobby and ramp: the look blends in from the south end (min z) to the north end (max z).")]
        [SerializeField] private Bounds entrance;

        private FixtureState[] powered;
        private FixtureState[] emergency;
        private FixtureState[] alarms;
        private Color[] screenLinear;
        private Color emergencyOnLinear;
        private Color domeOnLinear;
        private MaterialPropertyBlock block;

        private bool generatorIsDisabled;
        private bool blackoutPlaying;
        private bool blackoutLightsOn;
        private bool screensOff;
        private float blackoutStart;
        private int fixturesLeft;
        private float appliedBlend;
        private Transform viewer;

        /// <summary>True once the GeneratorDisabled event has arrived (a preview from the context menu does not count).</summary>
        public bool GeneratorIsDisabled => generatorIsDisabled;

        /// <summary>True from the start of a blackout (real or preview) until a reset.</summary>
        public bool IsBlackout => blackoutPlaying;

        /// <summary>The look applied to fog, ambient, moon and reflections: 0 powered, 1 full blackout.</summary>
        public float Blend => appliedBlend;

        /// <summary>Sets the event channels and the profile. Used by the Editor tool that builds the lighting.</summary>
        public void Configure(LightingStateProfile newProfile, VoidEventChannel generatorDisabledChannel,
            VoidEventChannel playerRespawnedChannel)
        {
            profile = newProfile;
            generatorDisabled = generatorDisabledChannel;
            playerRespawned = playerRespawnedChannel;
        }

        /// <summary>Sets the lights it controls. Used by the Editor tool that builds the lighting.</summary>
        public void SetLights(Fixture[] newPowered, Fixture[] newEmergency, Fixture[] newAlarms, Light newMoon,
            ReflectionProbe[] newProbes)
        {
            poweredFixtures = newPowered;
            emergencyLights = newEmergency;
            alarmBeacons = newAlarms;
            moon = newMoon;
            bunkerProbes = newProbes;
        }

        /// <summary>Sets the cascade origin and the bunker's volumes. Used by the Editor tool that builds the lighting.</summary>
        public void SetArea(Vector3 newCascadeOrigin, Bounds newBunker, Bounds newEntrance)
        {
            cascadeOrigin = newCascadeOrigin;
            bunker = newBunker;
            entrance = newEntrance;
        }

        /// <summary>
        /// How far inside the bunker a point is, for the blackout look: 1 in <paramref name="bunkerBounds"/>, an S-curve
        /// from 0 at the south end of <paramref name="entranceBounds"/> to 1 at its north end, and 0 everywhere else.
        /// </summary>
        public static float Interior(Vector3 position, Bounds bunkerBounds, Bounds entranceBounds)
        {
            if (bunkerBounds.Contains(position))
            {
                return 1f;
            }

            if (!entranceBounds.Contains(position))
            {
                return 0f;
            }

            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(entranceBounds.min.z, entranceBounds.max.z, position.z));
        }

        /// <summary>Plays the blackout as if the generator had gone off, without raising the event for anyone else.</summary>
        [ContextMenu("Trigger Blackout")]
        public void TriggerBlackout()
        {
            if (RefuseOutsidePlayMode())
            {
                return;
            }

            StartBlackout();
        }

        /// <summary>
        /// Puts the power back and forgets the event, as at the start of the mission. Not called "Reset": Unity's own
        /// Reset item on the component menu would clear every field the tool filled in.
        /// </summary>
        [ContextMenu("Reset To Powered")]
        public void ResetToPowered()
        {
            if (RefuseOutsidePlayMode())
            {
                return;
            }

            generatorIsDisabled = false;
            RestorePower();
        }

        private void Awake()
        {
            if (profile == null)
            {
                Debug.LogError("[World] PowerStateLighting has no LightingStateProfile: run Blackout/World/Setup Blackout Lighting.", this);
                enabled = false;
                return;
            }

            block = new MaterialPropertyBlock();
            powered = Capture(poweredFixtures);
            emergency = Capture(emergencyLights);
            alarms = Capture(alarmBeacons);
            screenLinear = new Color[screens.Length];
            for (int i = 0; i < screens.Length; i++)
            {
                screenLinear[i] = EmissionOf(screens[i]);
            }

            emergencyOnLinear = profile.emergencyLampEmission.linear;
            domeOnLinear = profile.alarmDomeEmission.linear;
            SwitchOffBlackoutLights();
        }

        private void OnEnable()
        {
            if (generatorDisabled != null)
            {
                generatorDisabled.OnRaised += OnGeneratorDisabled;
            }

            if (playerRespawned != null)
            {
                playerRespawned.OnRaised += OnPlayerRespawned;
            }
        }

        private void OnDisable()
        {
            if (generatorDisabled != null)
            {
                generatorDisabled.OnRaised -= OnGeneratorDisabled;
            }

            if (playerRespawned != null)
            {
                playerRespawned.OnRaised -= OnPlayerRespawned;
            }
        }

        private void OnGeneratorDisabled()
        {
            generatorIsDisabled = true;
            StartBlackout();
        }

        private void OnPlayerRespawned()
        {
            if (!generatorIsDisabled)
            {
                RestorePower();
            }
        }

        private void Update()
        {
            if (!blackoutPlaying)
            {
                return;
            }

            float seconds = Time.time - blackoutStart;
            if (fixturesLeft > 0)
            {
                UpdateCascade(seconds);
            }

            if (!screensOff)
            {
                UpdateScreens(seconds);
            }

            UpdateBlackoutLights(seconds);
            ApplyBlend(profile.TransitionAt(seconds) * ViewerInterior());
        }

        // ---- Blackout -----------------------------------------------------------------------------------------------

        private void StartBlackout()
        {
            if (blackoutPlaying)
            {
                return;
            }

            blackoutPlaying = true;
            blackoutStart = Time.time;
            screensOff = false;

            float farthest = 0f;
            foreach (Fixture fixture in poweredFixtures)
            {
                if (fixture.light != null)
                {
                    farthest = Mathf.Max(farthest, Vector3.Distance(cascadeOrigin, fixture.light.transform.position));
                }
            }

            fixturesLeft = 0;
            for (int i = 0; i < powered.Length; i++)
            {
                Light light = poweredFixtures[i].light;
                powered[i].off = light == null;
                if (light == null)
                {
                    continue;
                }

                // A faulty tube's flicker would fight the cascade. Disabling it puts its light and lamp back to full first.
                if (powered[i].flicker != null)
                {
                    powered[i].flicker.enabled = false;
                }

                float distance = Vector3.Distance(cascadeOrigin, light.transform.position);
                powered[i].offTime = profile.FixtureOffTime(distance, farthest, i);
                fixturesLeft++;
            }
        }

        private void UpdateCascade(float seconds)
        {
            for (int i = 0; i < powered.Length; i++)
            {
                if (powered[i].off)
                {
                    continue;
                }

                float level = profile.FixtureLevel(seconds, powered[i].offTime, i);
                if (level == 1f)
                {
                    continue; // nothing changes until its strobe starts
                }

                Light light = poweredFixtures[i].light;
                if (level <= 0f)
                {
                    // Out for good. A disabled light costs nothing to draw and gives its shadow map back.
                    light.enabled = false;
                    powered[i].off = true;
                    fixturesLeft--;
                }
                else
                {
                    light.intensity = powered[i].intensity * level;
                }

                SetEmission(poweredFixtures[i].lamp, powered[i].lampLinear * level);
            }
        }

        private void UpdateScreens(float seconds)
        {
            float level = profile.ScreenLevel(seconds);
            for (int i = 0; i < screens.Length; i++)
            {
                SetEmission(screens[i], screenLinear[i] * level);
            }

            screensOff = level <= 0f;
        }

        private void UpdateBlackoutLights(float seconds)
        {
            float level = profile.EmergencyLevel(seconds);
            if (level <= 0f)
            {
                return;
            }

            if (!blackoutLightsOn)
            {
                SetEnabled(emergencyLights, true);
                SetEnabled(alarmBeacons, true);
                blackoutLightsOn = true;
            }

            float pulse = profile.EmergencyPulse(seconds);
            for (int i = 0; i < emergency.Length; i++)
            {
                Light light = emergencyLights[i].light;
                if (light != null)
                {
                    light.intensity = emergency[i].intensity * level * pulse;
                }

                SetEmission(emergencyLights[i].lamp, Color.Lerp(emergency[i].lampLinear, emergencyOnLinear, level * pulse));
            }

            Quaternion spin = Quaternion.AngleAxis(profile.AlarmAngle(seconds), Vector3.up);
            Color dome = domeOnLinear * profile.AlarmDomePulse(seconds);
            for (int i = 0; i < alarms.Length; i++)
            {
                Light light = alarmBeacons[i].light;
                if (light != null)
                {
                    light.intensity = alarms[i].intensity * level;
                    light.transform.rotation = spin * alarms[i].rotation;
                }

                SetEmission(alarmBeacons[i].lamp, Color.Lerp(alarms[i].lampLinear, dome, level));
            }
        }

        // ---- Reset --------------------------------------------------------------------------------------------------

        private void RestorePower()
        {
            if (!blackoutPlaying)
            {
                return; // already powered: nothing to undo, and RenderSettings stay the scene's own
            }

            blackoutPlaying = false;
            for (int i = 0; i < powered.Length; i++)
            {
                Light light = poweredFixtures[i].light;
                if (light != null)
                {
                    light.intensity = powered[i].intensity;
                    light.enabled = true;
                }

                ClearEmission(poweredFixtures[i].lamp);
                if (powered[i].flicker != null)
                {
                    powered[i].flicker.enabled = true;
                }
            }

            foreach (Renderer screen in screens)
            {
                ClearEmission(screen);
            }

            SwitchOffBlackoutLights();
            appliedBlend = -1f; // force the write below
            ApplyBlend(0f);
        }

        // Off, at their authored intensity and aim, with their lamps showing the material's unlit glow.
        private void SwitchOffBlackoutLights()
        {
            for (int i = 0; i < emergency.Length; i++)
            {
                Light light = emergencyLights[i].light;
                if (light != null)
                {
                    light.enabled = false;
                    light.intensity = emergency[i].intensity;
                }

                ClearEmission(emergencyLights[i].lamp);
            }

            for (int i = 0; i < alarms.Length; i++)
            {
                Light light = alarmBeacons[i].light;
                if (light != null)
                {
                    light.enabled = false;
                    light.intensity = alarms[i].intensity;
                    light.transform.rotation = alarms[i].rotation;
                }

                ClearEmission(alarmBeacons[i].lamp);
            }

            blackoutLightsOn = false;
        }

        // ---- Global lighting ----------------------------------------------------------------------------------------

        private void ApplyBlend(float blend)
        {
            if (blend == appliedBlend)
            {
                return; // unchanged since the last frame: nothing to write
            }

            appliedBlend = blend;
            LightingStateProfile.State state = profile.Evaluate(blend);
            RenderSettings.fogColor = state.fogColor;
            RenderSettings.fogDensity = state.fogDensity;
            RenderSettings.ambientSkyColor = state.ambientSky;
            RenderSettings.ambientEquatorColor = state.ambientEquator;
            RenderSettings.ambientGroundColor = state.ambientGround;
            if (moon != null)
            {
                moon.intensity = state.directionalIntensity;
            }

            foreach (ReflectionProbe probe in bunkerProbes)
            {
                if (probe != null)
                {
                    probe.intensity = state.reflectionIntensity;
                }
            }
        }

        // How far inside the bunker the player's view is. The camera can live in another scene, so it is looked up by
        // its MainCamera tag (Camera.main) and cached, not referenced.
        private float ViewerInterior()
        {
            if (viewer == null)
            {
                Camera main = Camera.main;
                viewer = main != null ? main.transform : null;
            }

            return viewer != null ? Interior(viewer.position, bunker, entrance) : 1f;
        }

        // ---- Helpers ------------------------------------------------------------------------------------------------

        private static FixtureState[] Capture(Fixture[] fixtures)
        {
            var states = new FixtureState[fixtures.Length];
            for (int i = 0; i < fixtures.Length; i++)
            {
                Light light = fixtures[i].light;
                if (light != null)
                {
                    states[i].intensity = light.intensity;
                    states[i].rotation = light.transform.rotation;
                    states[i].flicker = light.GetComponent<FlickerLight>();
                }

                states[i].lampLinear = EmissionOf(fixtures[i].lamp);
            }

            return states;
        }

        // Colour properties are sRGB. Keep the linear value, so scaling it scales the light the lamp gives off.
        private static Color EmissionOf(Renderer renderer)
        {
            Material material = renderer != null ? renderer.sharedMaterial : null;
            return material != null && material.HasProperty(EmissionColorId) ? material.GetColor(EmissionColorId).linear : Color.black;
        }

        private void SetEmission(Renderer renderer, Color linear)
        {
            if (renderer == null)
            {
                return;
            }

            block.SetColor(EmissionColorId, linear.gamma);
            renderer.SetPropertyBlock(block);
        }

        private static void ClearEmission(Renderer renderer)
        {
            if (renderer != null)
            {
                renderer.SetPropertyBlock(null);
            }
        }

        private static void SetEnabled(Fixture[] fixtures, bool on)
        {
            foreach (Fixture fixture in fixtures)
            {
                if (fixture.light != null)
                {
                    fixture.light.enabled = on;
                }
            }
        }

        // The context menu also shows outside Play mode, where nothing would animate and the scene would be left changed.
        private bool RefuseOutsidePlayMode()
        {
            if (Application.isPlaying && powered != null)
            {
                return false;
            }

            Debug.LogWarning("[World] PowerStateLighting: Trigger Blackout and Reset To Powered work only in Play mode, "
                + "on a component that has a LightingStateProfile.", this);
            return true;
        }
    }
}
