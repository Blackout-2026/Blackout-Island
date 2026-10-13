using NUnit.Framework;
using UnityEngine;

namespace Blackout.Environment.Tests
{
    public class PowerStateLightingTests
    {
        // About the volumes Setup Blackout Lighting sets (docs/level-layout.md): the underground rooms with their walls,
        // and the lobby plus the ramp, from the inside of the bunker door (z 48.4) to the bottom of the ramp (z 69.6).
        private static readonly Bounds Bunker = MinMax(new Vector3(-20.3f, 2.1f, 69.6f), new Vector3(10.4f, 9.4f, 142.4f));
        private static readonly Bounds Entrance = MinMax(new Vector3(-6f, 2.1f, 48.4f), new Vector3(6f, 13.4f, 69.6f));

        [Test]
        public void Interior_IsOneInTheUndergroundRooms()
        {
            Assert.That(Interior(-11f, 4.15f, 106f), Is.EqualTo(1f), "generator room, eye height");
            Assert.That(Interior(-15f, 7.15f, 115f), Is.EqualTo(1f), "generator catwalk, eye height");
            Assert.That(Interior(0f, 4.15f, 76f), Is.EqualTo(1f), "security area");
            Assert.That(Interior(0f, 4.15f, 103f), Is.EqualTo(1f), "corridor");
            Assert.That(Interior(0f, 4.15f, 135f), Is.EqualTo(1f), "laboratory");
        }

        [Test]
        public void Interior_IsZeroOutside()
        {
            Assert.That(Interior(0f, 2.95f, -121f), Is.EqualTo(0f), "dock");
            Assert.That(Interior(0f, 7f, -40f), Is.EqualTo(0f), "forest trail");
            Assert.That(Interior(0f, 11.65f, 40f), Is.EqualTo(0f), "compound yard");
            Assert.That(Interior(0f, 11.65f, 48f), Is.EqualTo(0f), "in the bunker doorway");
            Assert.That(Interior(0f, 20f, 103f), Is.EqualTo(0f), "on the hill above the corridor");
        }

        [Test]
        public void Interior_RisesAlongTheLobbyAndRampToTheBunker()
        {
            Assert.That(Interior(0f, 11.65f, 48.4f), Is.EqualTo(0f).Within(1e-5f), "inside the bunker door");
            Assert.That(Interior(0f, 7.9f, 59f), Is.EqualTo(0.5f).Within(1e-5f), "middle of the ramp");
            Assert.That(Interior(0f, 4.15f, 69.6f), Is.EqualTo(1f).Within(1e-5f), "bottom of the ramp");

            float previous = 0f;
            for (float z = 49f; z <= 69.6f; z += 0.5f)
            {
                float y = Mathf.Lerp(11.65f, 4.15f, (z - 48.4f) / 21.2f);
                float next = Interior(0f, y, z);
                Assert.That(next, Is.GreaterThan(previous), $"z {z}");
                previous = next;
            }
        }

        private static float Interior(float x, float y, float z)
        {
            return PowerStateLighting.Interior(new Vector3(x, y, z), Bunker, Entrance);
        }

        private static Bounds MinMax(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }
    }
}
