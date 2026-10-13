using System.Collections.Generic;
using Blackout.Environment;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using State = Blackout.Environment.LightingStateProfile.State;

namespace Blackout.Editor.Tests
{
    public class BlackoutLightLayoutTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private static BunkerLayout Layout => AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);

        // ---- Profile states ---------------------------------------------------------------------------------------

        [Test]
        public void Powered_IsTheW7Scene()
        {
            State powered = BlackoutLightLayout.Powered;
            Assert.That(powered.fogColor, Is.EqualTo(LightingLayout.FogColour));
            Assert.That(powered.fogDensity, Is.EqualTo(0.0135f));
            Assert.That(powered.ambientSky, Is.EqualTo(LightingLayout.AmbientSky));
            Assert.That(powered.ambientEquator, Is.EqualTo(LightingLayout.AmbientEquator));
            Assert.That(powered.ambientGround, Is.EqualTo(LightingLayout.AmbientGround));
            Assert.That(powered.directionalIntensity, Is.EqualTo(0.9f));
            Assert.That(powered.reflectionIntensity, Is.EqualTo(1f));
        }

        [Test]
        public void Blackout_IsTheReferencesBunkerBlackoutInLinearLight()
        {
            State blackout = BlackoutLightLayout.Blackout;
            AssertLinear(blackout.fogColor, 0x0B / 255f, 0x02 / 255f, 0x03 / 255f, "fog");
            Assert.That(blackout.fogDensity, Is.EqualTo(0.05f));
            AssertLinear(blackout.ambientSky, 0.2f * 0x3A / 255f, 0.2f * 0x09 / 255f, 0.2f * 0x07 / 255f, "ambient sky");
            AssertLinear(blackout.ambientGround, 0.2f * 0x06 / 255f, 0.2f * 0x02 / 255f, 0.2f * 0x02 / 255f, "ambient ground");
            AssertLinear(blackout.ambientEquator, 0.1f * (0x3A + 0x06) / 255f, 0.1f * (0x09 + 0x02) / 255f, 0.1f * (0x07 + 0x02) / 255f, "ambient equator");
            Assert.That(blackout.directionalIntensity, Is.EqualTo(0f), "no moon inside the bunker");
            Assert.That(blackout.reflectionIntensity, Is.LessThan(BlackoutLightLayout.Powered.reflectionIntensity));
        }

        [Test]
        public void LampColours_AreTheReferencesEmissionAndNeverBlackWhenUnlit()
        {
            AssertLinear(BlackoutLightLayout.EmergencyLampOn, 5f, 5f * 0x2A / 255f, 5f * 0x1A / 255f, "emergency lamp");
            AssertLinear(BlackoutLightLayout.AlarmDomeOn, 2.5f, 2.5f * 0x1A / 255f, 2.5f * 0x0A / 255f, "alarm dome");

            // URP turns emission off for a black emission colour, and then the lamp could not light up at run time.
            Assert.That(BlackoutLightLayout.EmergencyLampUnlit.maxColorComponent, Is.GreaterThan(1f / 255f));
            Assert.That(BlackoutLightLayout.AlarmDomeUnlit.maxColorComponent, Is.GreaterThan(1f / 255f));
        }

        // ---- Emergency lamps --------------------------------------------------------------------------------------

        [Test]
        public void Emergency_HasTheReferences14LampsPerRoom()
        {
            List<BlackoutLightSpec> specs = BlackoutLightLayout.Emergency(Layout);
            Assert.That(specs.Count, Is.EqualTo(14));
            Assert.That(Count(specs, "SecurityArea"), Is.EqualTo(2));
            Assert.That(Count(specs, "Corridor"), Is.EqualTo(5));
            Assert.That(Count(specs, "GeneratorRoom"), Is.EqualTo(3));
            Assert.That(Count(specs, "Laboratory"), Is.EqualTo(3));
            Assert.That(Count(specs, "BunkerEntranceRamp"), Is.EqualTo(1));

            var names = new HashSet<string>();
            foreach (BlackoutLightSpec spec in specs)
            {
                Assert.That(names.Add(spec.Group + "/" + spec.Name), Is.True, $"duplicate {spec.Name}");
                Assert.That(spec.Type, Is.EqualTo(LightType.Point));
                Assert.That(spec.Shadows, Is.False);
            }
        }

        [Test]
        public void Emergency_LampsSitOnTheirWallAndTheLightIsInsideTheRoom()
        {
            BunkerLayout layout = Layout;
            foreach (BlackoutLightSpec spec in BlackoutLightLayout.Emergency(layout))
            {
                Room room = layout.FindRoom(spec.Group);
                Vector3 inward = spec.Rotation * Vector3.forward;
                Vector3 lampBack = spec.LampPosition - inward * (BlackoutLightLayout.LampSize.z * 0.5f);
                float toWall = Mathf.Abs(inward.x) > 0.5f
                    ? Mathf.Abs(Mathf.Abs(lampBack.x - room.position.x) - room.size.x * 0.5f)
                    : Mathf.Abs(Mathf.Abs(lampBack.z - room.position.z) - room.size.z * 0.5f);
                Assert.That(toWall, Is.LessThan(1e-4f), $"{spec.Group} {spec.Name}: the lamp's back should touch the wall");

                Assert.That(Mathf.Abs(spec.Position.x - room.position.x), Is.LessThan(room.size.x * 0.5f), $"{spec.Group} {spec.Name}");
                Assert.That(Mathf.Abs(spec.Position.z - room.position.z), Is.LessThan(room.size.z * 0.5f), $"{spec.Group} {spec.Name}");
                Assert.That(Vector3.Distance(spec.Position, spec.LampPosition + inward * 0.4f + Vector3.down * 0.2f), Is.LessThan(1e-4f));
            }
        }

        [Test]
        public void Emergency_LampsHangUnderTheCeilingAndAboveTheDoors()
        {
            BunkerLayout layout = Layout;
            foreach (BlackoutLightSpec spec in BlackoutLightLayout.Emergency(layout))
            {
                Room room = layout.FindRoom(spec.Group);
                float floor = BunkerShell.FloorY(room, spec.LampPosition.z);
                float bottom = spec.LampPosition.y - BlackoutLightLayout.LampSize.y * 0.5f - floor;
                float top = spec.LampPosition.y + BlackoutLightLayout.LampSize.y * 0.5f - floor;
                Assert.That(top, Is.LessThanOrEqualTo(room.size.y), $"{spec.Group} {spec.Name} pokes into the ceiling");
                Assert.That(bottom, Is.GreaterThan(2.3f), $"{spec.Group} {spec.Name} is lower than a door's top");
            }
        }

        [Test]
        public void EmergencyIntensity_MatchesThePrototype2Point3mFromTheLight()
        {
            float prototype = 2.2f * Mathf.Pow(1f - 2.3f / 9f, 1.5f);
            float unity = BlackoutLightLayout.EmergencyIntensity * LightingLayout.UnityFalloff(2.3f, 9f);
            Assert.That(unity, Is.EqualTo(prototype).Within(1e-4f));
            Assert.That(BlackoutLightLayout.EmergencyIntensity, Is.EqualTo(7.54f).Within(0.01f));
        }

        // ---- Alarm beacons ----------------------------------------------------------------------------------------

        [Test]
        public void Alarms_AreTwoInTheCorridorAndOneOverTheGenerator()
        {
            BunkerLayout layout = Layout;
            List<BlackoutLightSpec> specs = BlackoutLightLayout.Alarms(layout);
            Assert.That(specs.Count, Is.EqualTo(3));
            Assert.That(Count(specs, "Corridor"), Is.EqualTo(2));
            Assert.That(Count(specs, "GeneratorRoom"), Is.EqualTo(1));
            foreach (BlackoutLightSpec spec in specs)
            {
                Room room = layout.FindRoom(spec.Group);
                Assert.That(spec.Type, Is.EqualTo(LightType.Spot));
                Assert.That(spec.Position.y, Is.EqualTo(BunkerShell.FloorY(room, spec.Position.z) + room.size.y - 0.2f).Within(1e-4f));
                Assert.That(spec.Shadows, Is.True, $"{spec.Name}: the beam must not sweep the next room through the wall");
                float below = -Mathf.Asin((spec.Rotation * Vector3.forward).y) * Mathf.Rad2Deg;
                Assert.That(below, Is.EqualTo(14.93f).Within(0.01f), "aimed 6 m out and 1.6 m down");
            }

            Assert.That(specs[0].SpotAngle, Is.EqualTo(48.13f).Within(0.01f), "0.42 rad half angle");
            Assert.That(specs[0].InnerSpotAngle, Is.EqualTo(28.88f).Within(0.01f), "penumbra 0.4");
            Assert.That(BlackoutLightLayout.AlarmIntensity, Is.EqualTo(228.4f).Within(0.1f));
        }

        // ---- Volumes and cascade ----------------------------------------------------------------------------------

        [Test]
        public void Underground_HoldsEveryUndergroundRoomButNotTheHillAbove()
        {
            BunkerLayout layout = Layout;
            Bounds underground = BlackoutLightLayout.Underground(layout);
            AssertVector(underground.min, new Vector3(-20.3f, 2.1f, 69.6f), "min");
            AssertVector(underground.max, new Vector3(10.4f, 9.4f, 142.4f), "max");

            foreach (string name in BlackoutLightLayout.UndergroundRooms)
            {
                Room room = layout.FindRoom(name);
                Assert.That(underground.Contains(room.position + Vector3.up * 1.65f), Is.True, name);
            }

            Assert.That(underground.Contains(new Vector3(-15f, 7.15f, 115f)), Is.True, "eye height on the catwalk");
            Assert.That(underground.Contains(new Vector3(0f, IslandLayout.PlateauY + 4.6f, 75f)), Is.False, "the hill starts at 14.6 m");
        }

        [Test]
        public void Entrance_RunsFromInsideTheBunkerDoorToTheBottomOfTheRamp()
        {
            BunkerLayout layout = Layout;
            Bounds entrance = BlackoutLightLayout.Entrance(layout);
            AssertVector(entrance.min, new Vector3(-6f, 2.1f, 48.4f), "min");
            AssertVector(entrance.max, new Vector3(6f, 13.4f, 69.6f), "max");
            Assert.That(entrance.max.z, Is.EqualTo(BlackoutLightLayout.Underground(layout).min.z).Within(1e-4f), "the two volumes meet");

            float yard = PowerStateLighting.Interior(new Vector3(0f, 11.65f, 45f), BlackoutLightLayout.Underground(layout), entrance);
            Assert.That(yard, Is.EqualTo(0f), "the compound yard is outside");
        }

        [Test]
        public void CascadeOrigin_Is2mAboveTheGeneratorSocket()
        {
            AssertVector(BlackoutLightLayout.CascadeOrigin(Layout), new Vector3(-11f, 4.5f, 106f), "origin");
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        private static int Count(List<BlackoutLightSpec> specs, string room)
        {
            return specs.FindAll(s => s.Group == room).Count;
        }

        private static void AssertLinear(Color srgb, float r, float g, float b, string what)
        {
            Color linear = srgb.linear;
            Assert.That(linear.r, Is.EqualTo(r).Within(1e-3f), what + " r");
            Assert.That(linear.g, Is.EqualTo(g).Within(1e-3f), what + " g");
            Assert.That(linear.b, Is.EqualTo(b).Within(1e-3f), what + " b");
        }

        private static void AssertVector(Vector3 actual, Vector3 expected, string what)
        {
            Assert.That(Vector3.Distance(actual, expected), Is.LessThan(1e-3f), $"{what}: {actual} != {expected}");
        }
    }
}
