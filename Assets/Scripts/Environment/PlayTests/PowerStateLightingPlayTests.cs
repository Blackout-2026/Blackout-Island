using System.Collections;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Fixture = Blackout.Environment.PowerStateLighting.Fixture;
using State = Blackout.Environment.LightingStateProfile.State;

namespace Blackout.Environment.PlayTests
{
    /// <summary>
    /// Plays the real component in a small test scene: three powered fixtures at 5, 25 and 50 m from the cascade origin
    /// (the nearest one a faulty tube), one emergency light, one alarm beacon and a moon. The events are raised on
    /// test-only channel assets, as the generator and the respawn code would raise the real ones.
    /// </summary>
    public class PowerStateLightingPlayTests
    {
        private const float PoweredIntensity = 5f;
        private const float EmergencyIntensity = 7f;
        private const float AlarmIntensity = 200f;
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private readonly List<Object> created = new List<Object>();
        private LightingStateProfile profile;
        private VoidEventChannel generatorDisabled;
        private VoidEventChannel playerRespawned;
        private PowerStateLighting lighting;
        private Light[] powered;
        private Renderer[] lamps;
        private FlickerLight flicker;
        private Light emergency;
        private Light alarm;
        private Quaternion alarmAim;
        private Light moon;

        [SetUp]
        public void SetUp()
        {
            profile = Keep(ScriptableObject.CreateInstance<LightingStateProfile>());
            profile.powered = new State { fogColor = Color.grey, fogDensity = 0.0135f, directionalIntensity = 0.9f, reflectionIntensity = 1f };
            profile.blackout = new State { fogColor = Color.red, fogDensity = 0.05f, directionalIntensity = 0f, reflectionIntensity = 0.2f };
            profile.emergencyLampEmission = Color.red * 3f;
            profile.alarmDomeEmission = Color.red * 2f;
            generatorDisabled = Keep(ScriptableObject.CreateInstance<VoidEventChannel>());
            playerRespawned = Keep(ScriptableObject.CreateInstance<VoidEventChannel>());

            // Built inactive, so the component's Awake runs once everything is wired, as it does in the scene.
            var root = Keep(new GameObject("Lighting"));
            root.SetActive(false);
            var lampMaterial = Keep(new Material(Shader.Find("Universal Render Pipeline/Lit")));
            lampMaterial.SetColor(EmissionColorId, Color.white * 2f);

            powered = new Light[3];
            lamps = new Renderer[3];
            var fixtures = new Fixture[3];
            float[] distances = { 5f, 25f, 50f };
            for (int i = 0; i < 3; i++)
            {
                powered[i] = AddLight(root, $"Powered{i}", LightType.Point, new Vector3(0f, 3f, distances[i]), PoweredIntensity);
                GameObject lamp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                lamp.transform.SetParent(powered[i].transform, false);
                lamps[i] = lamp.GetComponent<Renderer>();
                lamps[i].sharedMaterial = lampMaterial;
                fixtures[i] = new Fixture { light = powered[i], lamp = lamps[i] };
            }

            flicker = powered[0].gameObject.AddComponent<FlickerLight>();
            emergency = AddLight(root, "Emergency", LightType.Point, new Vector3(1f, 3f, 10f), EmergencyIntensity);
            alarm = AddLight(root, "Alarm", LightType.Spot, new Vector3(0f, 3f, 20f), AlarmIntensity);
            alarmAim = Quaternion.Euler(15f, 0f, 0f);
            alarm.transform.rotation = alarmAim;
            moon = AddLight(root, "Moon", LightType.Directional, Vector3.zero, 0.9f);

            lighting = root.AddComponent<PowerStateLighting>();
            lighting.Configure(profile, generatorDisabled, playerRespawned);
            lighting.SetLights(fixtures, new[] { new Fixture { light = emergency } }, new[] { new Fixture { light = alarm } },
                moon, new ReflectionProbe[0]);

            // Everywhere counts as inside the bunker, wherever the test runner's camera is.
            lighting.SetArea(new Vector3(0f, 3f, 0f), new Bounds(Vector3.zero, Vector3.one * 10000f), new Bounds());
            root.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object item in created)
            {
                Object.Destroy(item);
            }

            created.Clear();
        }

        [UnityTest]
        public IEnumerator GeneratorDisabled_KillsTheFixturesOutwardThenFadesInTheEmergencyLights()
        {
            Assert.That(emergency.enabled || alarm.enabled, Is.False, "the blackout lights wait for the event");

            float start = Time.time;
            generatorDisabled.Raise();
            Assert.That(lighting.IsBlackout && lighting.GeneratorIsDisabled, Is.True);
            Assert.That(flicker.enabled, Is.False, "the faulty tube's flicker stops first");

            float[] offAt = { -1f, -1f, -1f };
            float end = profile.emergencyDelaySeconds + profile.emergencyFadeSeconds + 0.4f;
            while (Time.time - start < end)
            {
                yield return null;
                float seconds = Time.time - start;
                for (int i = 0; i < 3; i++)
                {
                    if (offAt[i] < 0f && !powered[i].enabled)
                    {
                        offAt[i] = seconds;
                    }
                }

                if (seconds < profile.emergencyDelaySeconds - 0.05f)
                {
                    Assert.That(emergency.enabled, Is.False, $"emergency light on early, at {seconds:0.00} s");
                }
            }

            for (int i = 0; i < 3; i++)
            {
                Assert.That(offAt[i], Is.InRange(profile.strobeSeconds, profile.cascadeSeconds + 0.1f), $"fixture {i} went out at {offAt[i]:0.00} s");
                Assert.That(EmissionOf(lamps[i]).maxColorComponent, Is.EqualTo(0f), $"lamp {i} still glows");
            }

            Assert.That(offAt[0], Is.LessThan(offAt[1]), "the cascade runs outward from the generator");
            Assert.That(offAt[1], Is.LessThan(offAt[2]), "the cascade runs outward from the generator");

            Assert.That(emergency.enabled && alarm.enabled, Is.True);
            Assert.That(emergency.intensity, Is.InRange(EmergencyIntensity * 0.5f - 1e-3f, EmergencyIntensity + 1e-3f), "full strength, pulsing");
            Assert.That(alarm.intensity, Is.EqualTo(AlarmIntensity).Within(1e-3f));
            Assert.That(Quaternion.Angle(alarm.transform.rotation, alarmAim), Is.GreaterThan(1f), "the alarm turns");
            Assert.That(alarm.transform.forward.y, Is.EqualTo((alarmAim * Vector3.forward).y).Within(1e-3f), "and keeps its tilt");

            Assert.That(lighting.Blend, Is.EqualTo(1f));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.05f).Within(1e-5f));
            Assert.That(moon.intensity, Is.EqualTo(0f));
        }

        [UnityTest]
        public IEnumerator PlayerRespawned_KeepsTheRealBlackout()
        {
            generatorDisabled.Raise();
            yield return new WaitForSeconds(profile.cascadeSeconds + 0.2f);

            playerRespawned.Raise();
            Assert.That(lighting.IsBlackout, Is.True);
            foreach (Light light in powered)
            {
                Assert.That(light.enabled, Is.False);
            }
        }

        [UnityTest]
        public IEnumerator PlayerRespawned_UndoesAPreviewFromTheContextMenu()
        {
            lighting.TriggerBlackout();
            Assert.That(lighting.IsBlackout, Is.True);
            Assert.That(lighting.GeneratorIsDisabled, Is.False, "a preview does not pretend the generator is off");
            yield return new WaitForSeconds(profile.emergencyDelaySeconds + 0.5f);
            Assert.That(emergency.enabled, Is.True);

            playerRespawned.Raise();
            Assert.That(lighting.IsBlackout, Is.False);
            for (int i = 0; i < 3; i++)
            {
                Assert.That(powered[i].enabled, Is.True);
                Assert.That(powered[i].intensity, Is.EqualTo(PoweredIntensity));
                Assert.That(lamps[i].HasPropertyBlock(), Is.False, $"lamp {i} is back on its material");
            }

            Assert.That(flicker.enabled, Is.True, "the faulty tube flickers again");
            Assert.That(emergency.enabled || alarm.enabled, Is.False);
            Assert.That(emergency.intensity, Is.EqualTo(EmergencyIntensity));
            Assert.That(Quaternion.Angle(alarm.transform.rotation, alarmAim), Is.LessThan(0.01f));
            Assert.That(lighting.Blend, Is.EqualTo(0f));
            Assert.That(RenderSettings.fogDensity, Is.EqualTo(0.0135f).Within(1e-5f));
            Assert.That(moon.intensity, Is.EqualTo(0.9f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator ResetToPowered_EndsTheRealBlackoutToo()
        {
            generatorDisabled.Raise();
            yield return new WaitForSeconds(0.5f);

            lighting.ResetToPowered();
            Assert.That(lighting.IsBlackout || lighting.GeneratorIsDisabled, Is.False);
            foreach (Light light in powered)
            {
                Assert.That(light.enabled, Is.True);
            }
        }

        private Light AddLight(GameObject root, string name, LightType type, Vector3 position, float intensity)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, false);
            go.transform.position = position;
            var light = go.AddComponent<Light>();
            light.type = type;
            light.intensity = intensity;
            return light;
        }

        private static Color EmissionOf(Renderer renderer)
        {
            var block = new MaterialPropertyBlock();
            renderer.GetPropertyBlock(block);
            return block.GetColor(EmissionColorId);
        }

        private T Keep<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
