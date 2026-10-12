using System.Collections.Generic;
using Blackout.Environment;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class LightFixtureLayoutTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private static BunkerLayout Layout => AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);

        [Test]
        public void MatchIntensity_GivesThePrototypeBrightnessAtTheMatchedDistance()
        {
            // A security tube: 1.5, range 9, decay 1.6, 3.25 m over the floor in the prototype and 2.65 m in Unity.
            float prototype = 1.5f * Mathf.Pow(1f - 3.25f / 9f, 1.6f);
            float unity = LightingLayout.MatchIntensity(1.5f, 9f, 1.6f, 3.25f, 2.65f) * LightingLayout.UnityFalloff(2.65f, 9f);
            Assert.That(unity, Is.EqualTo(prototype).Within(1e-4f));
        }

        [Test]
        public void MatchIntensity_GeneratorHighBayIs43()
        {
            Assert.That(LightingLayout.MatchIntensity(3f, 14f, 1.6f, 5.55f, 5.55f), Is.EqualTo(43.3f).Within(0.1f));
        }

        [Test]
        public void Powered_HasTheReferenceFixturesPerRoom()
        {
            List<LightSpec> specs = LightFixtureLayout.Powered(Layout);
            Assert.That(specs.Count, Is.EqualTo(31));
            Assert.That(Count(specs, "BunkerEntranceRamp", FixtureKind.Tube), Is.EqualTo(4));
            Assert.That(Count(specs, "SecurityArea", FixtureKind.Tube), Is.EqualTo(6));
            Assert.That(Count(specs, "Corridor", FixtureKind.Tube), Is.EqualTo(10));
            Assert.That(Count(specs, "GeneratorRoom", FixtureKind.Sodium), Is.EqualTo(6));
            Assert.That(Count(specs, "Laboratory", FixtureKind.Panel), Is.EqualTo(5));
        }

        [Test]
        public void Powered_EveryLampIsInsideItsRoomUnderTheCeilingAndAboveHeads()
        {
            BunkerLayout layout = Layout;
            var names = new HashSet<string>();
            foreach (LightSpec spec in LightFixtureLayout.Powered(layout))
            {
                Assert.That(names.Add(spec.Group + "/" + spec.Name), Is.True, $"duplicate {spec.Name}");
                Room room = layout.FindRoom(spec.Group);
                Vector3 half = LightFixtureLayout.LampSize(spec) * 0.5f;
                Assert.That(Mathf.Abs(spec.Position.x - room.position.x) + half.x, Is.LessThan(room.size.x * 0.5f), spec.Name);
                Assert.That(Mathf.Abs(spec.Position.z - room.position.z) + half.z, Is.LessThan(room.size.z * 0.5f), spec.Name);

                float floor = BunkerShell.FloorY(room, spec.Position.z);
                float lampTop = spec.Position.y + LightFixtureLayout.LightBelowLamp + half.y;
                Assert.That(lampTop, Is.LessThanOrEqualTo(floor + room.size.y + 1e-4f), $"{spec.Group} {spec.Name} pokes into the ceiling");
                Assert.That(spec.Position.y - floor, Is.GreaterThan(2.3f), $"{spec.Group} {spec.Name} is too low");
            }
        }

        [Test]
        public void Powered_HasTwoFaultyTubesAtTheReferencePositions()
        {
            List<LightSpec> faulty = LightFixtureLayout.Powered(Layout).FindAll(s => s.Flickers);
            Assert.That(faulty.Count, Is.EqualTo(2));
            Assert.That(faulty.TrueForAll(s => s.Flicker == FlickerLight.Pattern.FaultyTube && s.Kind == FixtureKind.Tube), Is.True);

            LightSpec corridor = faulty.Find(s => s.Group == "Corridor");
            LightSpec security = faulty.Find(s => s.Group == "SecurityArea");
            Assert.That(corridor.Position.z, Is.EqualTo(93f));
            Assert.That(new Vector2(security.Position.x, security.Position.z), Is.EqualTo(new Vector2(4.5f, 79f)));
            Assert.That(corridor.FlickerSeed, Is.Not.EqualTo(security.FlickerSeed));
        }

        [Test]
        public void Powered_ShadowsOnlyOnTheHighBaysAndTheTubeAboveThePlateau()
        {
            List<LightSpec> shadowed = LightFixtureLayout.Powered(Layout).FindAll(s => s.Shadows);
            Assert.That(shadowed.Count, Is.EqualTo(7));
            Assert.That(shadowed.FindAll(s => s.Kind == FixtureKind.Sodium).Count, Is.EqualTo(6));
            LightSpec tube = shadowed.Find(s => s.Kind == FixtureKind.Tube);
            Assert.That(tube.Group + "/" + tube.Name, Is.EqualTo("BunkerEntranceRamp/Tube01"));
            Assert.That(tube.Position.y, Is.GreaterThan(IslandLayout.PlateauY));
        }

        [Test]
        public void Powered_IntensityMatchesTheFloorUnderEachLamp()
        {
            // The prototype lights the floor under its fixtures at 0.53 (ramp tube) to 1.34 (high-bay); Unity's must too.
            BunkerLayout layout = Layout;
            foreach (LightSpec spec in LightFixtureLayout.Powered(layout))
            {
                Room room = layout.FindRoom(spec.Group);
                float height = spec.Position.y - BunkerShell.FloorY(room, spec.Position.z);
                Assert.That(spec.Intensity * LightingLayout.UnityFalloff(height, spec.Range), Is.GreaterThan(0.5f).And.LessThan(1.4f),
                    $"{spec.Group} {spec.Name}");
            }
        }

        [Test]
        public void Outdoor_FloodlightsAreThePrototypeSpots()
        {
            List<LightSpec> floods = LightFixtureLayout.Outdoor(Layout).FindAll(s => s.Type == LightType.Spot);
            Assert.That(floods.Count, Is.EqualTo(3));
            foreach (LightSpec flood in floods)
            {
                Assert.That(flood.SpotAngle, Is.EqualTo(55f).Within(0.1f));
                Assert.That(flood.InnerSpotAngle, Is.EqualTo(24.75f).Within(0.1f));
                Assert.That(flood.Range, Is.EqualTo(42f));
                Assert.That(flood.Shadows, Is.True);
                Assert.That(flood.Target.y, Is.EqualTo(IslandLayout.PlateauY), "aimed at the compound yard");
            }
        }

        [Test]
        public void Outdoor_WallPackIsOverTheBunkerDoor()
        {
            LightSpec wallPack = LightFixtureLayout.Outdoor(Layout).Find(s => s.Name == "WallPack");
            Assert.That(Vector3.Distance(wallPack.Position, new Vector3(0f, 12.8f, 47.2f)), Is.LessThan(1e-3f));
            Assert.That(wallPack.Shadows, Is.True);
        }

        [Test]
        public void Outdoor_LanternLightIsJustOverTheBoatLantern()
        {
            LightSpec lantern = LightFixtureLayout.Outdoor(Layout).Find(s => s.Name == "BoatLantern");

            // The lantern mesh (BeachGreyboxBuilder) is boat-local (-1.2, 2.45, -1.7); the light is 0.15 m above it.
            Vector3 mesh = IslandLayout.BoatPosition + Quaternion.Euler(0f, IslandLayout.BoatYaw, 0f) * new Vector3(-1.2f, 2.45f, -1.7f);
            Assert.That(Vector3.Distance(lantern.Position, mesh + new Vector3(0f, 0.15f, 0f)), Is.LessThan(1e-3f));
            Assert.That(lantern.Flickers && lantern.Flicker == FlickerLight.Pattern.Flame, Is.True);
        }

        private static int Count(List<LightSpec> specs, string room, FixtureKind kind)
        {
            return specs.FindAll(s => s.Group == room && s.Kind == kind).Count;
        }
    }
}
