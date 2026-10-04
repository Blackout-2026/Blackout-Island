using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class IslandBoundaryTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        private static readonly Vector2[] Coast = IslandBoundary.TraceCoast(TerrainBuilder.BoundaryRays);

        [Test]
        public void EveryWallPoint_IsOutAtSea()
        {
            foreach (Vector2 p in Coast)
            {
                Assert.That(IslandHeightField.Sample(p.x, p.y), Is.LessThan(IslandBoundary.ShoreHeight), $"at {p}");
            }
        }

        [Test]
        public void EveryWall_StaysOffTheLand()
        {
            // The middle of each wall is under water, so no wall cuts across the beach or the coast.
            for (int i = 0; i < Coast.Length; i++)
            {
                Vector2 middle = (Coast[i] + Coast[(i + 1) % Coast.Length]) * 0.5f;
                Assert.That(IslandHeightField.Sample(middle.x, middle.y), Is.LessThan(IslandLayout.WaterY), $"wall {i} at {middle}");
            }
        }

        // Gameplay positions from docs/level-layout.md and the reference (Unity x, z).
        [TestCase(0f, -121f, TestName = "Boundary_Contains_PlayerStartOnTheDock")]
        [TestCase(-1.4f, -128f, TestName = "Boundary_Contains_DockEndWest")]
        [TestCase(1.4f, -128f, TestName = "Boundary_Contains_DockEndEast")]
        [TestCase(3.7f, -122.5f, TestName = "Boundary_Contains_Boat")]
        [TestCase(3.9f, -127.1f, TestName = "Boundary_Contains_BoatBow")]
        [TestCase(0f, 45f, TestName = "Boundary_Contains_Compound")]
        [TestCase(11f, 67f, TestName = "Boundary_Contains_MastBase")]
        [TestCase(0f, 133f, TestName = "Boundary_Contains_Laboratory")]
        [TestCase(-24f, -54f, TestName = "Boundary_Contains_Hunter1Spawn")]
        [TestCase(26f, -20f, TestName = "Boundary_Contains_Hunter2Spawn")]
        public void Boundary_ContainsGameplayPositions(float x, float z)
        {
            Assert.That(Contains(Coast, new Vector2(x, z)), Is.True);
        }

        [Test]
        public void Boundary_ContainsTheWholeBoatZone()
        {
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8f;
                Vector2 p = IslandLayout.BoatZoneCentre + IslandLayout.BoatZoneRadius * new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
                Assert.That(Contains(Coast, p), Is.True, $"at {p}");
            }
        }

        [Test]
        public void RampHole_CoversTheRampUntilItsRoofIsUnderground()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Rect hole = TerrainBuilder.RampHoleRect(layout.FindRoom("BunkerEntranceRamp"), layout.wallThickness);

            // Ramp interior x -1.5 .. 1.5 from its top end (z 52.4); its roof top (floor + 3.0 + 0.4) drops below
            // the plateau (y 10) 3.4 / (7.5 / 17.2) = 7.8 m further north, at z 60.2.
            Assert.That(hole.xMin, Is.EqualTo(-1.5f).Within(0.01f));
            Assert.That(hole.xMax, Is.EqualTo(1.5f).Within(0.01f));
            Assert.That(hole.yMin, Is.EqualTo(52.4f).Within(0.01f));
            Assert.That(hole.yMax, Is.EqualTo(60.2f).Within(0.05f));
        }

        // Even-odd ray casting test for a point in a polygon.
        private static bool Contains(Vector2[] polygon, Vector2 p)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
            {
                Vector2 a = polygon[i];
                Vector2 b = polygon[j];
                if ((a.y > p.y) != (b.y > p.y) && p.x < (b.x - a.x) * (p.y - a.y) / (b.y - a.y) + a.x)
                {
                    inside = !inside;
                }
            }

            return inside;
        }
    }
}
