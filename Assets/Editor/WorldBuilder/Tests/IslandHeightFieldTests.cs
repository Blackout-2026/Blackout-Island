using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class IslandHeightFieldTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        // Sample heights recorded in docs/reference/world-builder.md section 5, converted to Unity (x, -z).
        // The prototype read them from a 2 m grid, so allow 15 cm for interpolation.
        [TestCase(0f, -97f, 0.97f, TestName = "PrototypeHeight_DockRoot_Is0.97")]
        [TestCase(0f, -90f, 1.77f, TestName = "PrototypeHeight_ForestCheckpoint_Is1.77")]
        [TestCase(4f, -44f, 4.70f, TestName = "PrototypeHeight_TrailMidpoint_Is4.70")]
        [TestCase(0f, 28f, 10.08f, TestName = "PrototypeHeight_Gate_Is10.08")]
        [TestCase(11f, 67f, 15.49f, TestName = "PrototypeHeight_MastBase_Is15.49")]
        [TestCase(0f, 90f, 21.08f, TestName = "PrototypeHeight_Hilltop_Is21.08")]
        public void PrototypeHeight_MatchesReferenceSamples(float x, float z, float expected)
        {
            Assert.That(IslandHeightField.PrototypeHeight(x, z), Is.EqualTo(expected).Within(0.15f));
        }

        [Test]
        public void CompoundPad_IsFlatAtPlateauHeight()
        {
            // z >= 40: north of where the trail smoothing ends, so the pad is exactly flat.
            for (float x = IslandLayout.CompoundMinX; x <= IslandLayout.CompoundMaxX; x += 2f)
            {
                for (float z = 40f; z <= IslandLayout.CompoundMaxZ; z += 2f)
                {
                    Assert.That(IslandHeightField.Sample(x, z), Is.EqualTo(IslandLayout.PlateauY).Within(0.001f), $"at ({x}, {z})");
                }
            }
        }

        [TestCase(0f, -105f, TestName = "Shoreline_AtX0_IsZ-105")]
        [TestCase(30f, -101.2f, TestName = "Shoreline_AtX30_IsZ-101.2")]
        public void Shoreline_IsWhereTheReferenceSays(float x, float z)
        {
            Assert.That(IslandHeightField.ShorelineZ(x), Is.EqualTo(z).Within(0.1f));
            Assert.That(IslandHeightField.PrototypeHeight(x, z), Is.EqualTo(IslandLayout.WaterY).Within(0.3f));
        }

        [TestCase(-30f)]
        [TestCase(-8f)]
        [TestCase(8f)]
        [TestCase(30f)]
        public void BeachStrip_IsAGentleSlopeDownToTheWater(float x)
        {
            // From 15 m inland of the shoreline down to the water, in 1 m steps.
            double shore = IslandHeightField.ShorelineZ(x);
            float previous = IslandHeightField.Sample(x, (float)shore + 15f);
            for (float z = (float)shore + 14f; z >= shore; z -= 1f)
            {
                float h = IslandHeightField.Sample(x, z);
                Assert.That(previous - h, Is.InRange(-0.01f, 0.13f), $"slope at ({x}, {z})");
                previous = h;
            }
        }

        [Test]
        public void Berth_IsDeepEnoughForTheBoat()
        {
            Rect berth = IslandLayout.Berth;
            for (float x = berth.xMin; x <= berth.xMax; x += 0.5f)
            {
                for (float z = berth.yMin; z <= berth.yMax; z += 0.5f)
                {
                    Assert.That(IslandHeightField.Sample(x, z), Is.LessThanOrEqualTo(-IslandLayout.BerthDepth + 0.001f), $"at ({x}, {z})");
                }
            }
        }

        [Test]
        public void DockLanding_SandMeetsTheDeck()
        {
            // Just inland of the shore end of the dock the step down from the deck is at most 10 cm.
            float step = IslandLayout.DockDeckY - IslandHeightField.Sample(0f, IslandLayout.DockMaxZ + 0.1f);
            Assert.That(step, Is.InRange(0f, 0.1f));

            // Under the dock the sand never pokes through the deck.
            for (float z = IslandLayout.DockMinZ; z <= IslandLayout.DockMaxZ; z += 0.5f)
            {
                Assert.That(IslandHeightField.Sample(0f, z), Is.LessThan(IslandLayout.DockDeckY), $"at z {z}");
            }
        }

        [Test]
        public void Hill_CoversEveryUndergroundRoom()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);

            const float minCover = 1f;
            int checkedRooms = 0;
            foreach (Room room in layout.rooms)
            {
                // Flat rooms below the plateau are underground; the lobby and the ramp are handled by the terrain hole.
                if (room.floorDropNorth != 0f || room.position.y >= IslandLayout.PlateauY)
                {
                    continue;
                }

                checkedRooms++;
                float wall = layout.wallThickness;
                float roofTop = room.position.y + room.size.y + wall;
                float halfX = room.size.x * 0.5f + wall;
                float halfZ = room.size.z * 0.5f + wall;
                for (float x = room.position.x - halfX; x <= room.position.x + halfX + 0.01f; x += halfX * 0.25f)
                {
                    for (float z = room.position.z - halfZ; z <= room.position.z + halfZ + 0.01f; z += halfZ * 0.25f)
                    {
                        float cover = IslandHeightField.Sample(x, z) - roofTop;
                        Assert.That(cover, Is.GreaterThanOrEqualTo(minCover), $"{room.name} roof at ({x:0.0}, {z:0.0})");
                    }
                }
            }

            Assert.That(checkedRooms, Is.EqualTo(4), "SecurityArea, Corridor, GeneratorRoom, Laboratory");
        }
    }
}
