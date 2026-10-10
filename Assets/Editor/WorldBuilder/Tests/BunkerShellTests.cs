using System.Collections.Generic;
using System.Linq;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class BunkerShellTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";
        private const float Step = 0.25f; // spacing of the sample points in the closure test
        private const float Inset = 0.05f; // keeps sample points off the exact edges of a band

        private BunkerLayout layout;
        private List<(string room, ShellPart part)> allParts;

        [OneTimeSetUp]
        public void LoadLayout()
        {
            layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);
            allParts = layout.rooms
                .SelectMany(room => BunkerShell.BuildRoom(layout, room).Select(part => (room: room.name, part)))
                .ToList();
        }

        // The "Doors and openings" table in docs/level-layout.md.
        [TestCase("D-Bunker", 0f, 10f, 48.2f)]
        [TestCase("O-RampTop", 0f, 10f, 52.2f)]
        [TestCase("O-RampBottom", 0f, 2.5f, 69.8f)]
        [TestCase("D-Security", 0f, 2.5f, 82.2f)]
        [TestCase("D-Generator", -1.7f, 2.5f, 106f)]
        [TestCase("D-Lab", 0f, 2.5f, 123.8f)]
        public void Opening_IsWhereTheLevelLayoutSays(string id, float x, float y, float z)
        {
            int found = 0;
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings.Where(o => o.id == id))
                {
                    Vector3 centre = BunkerShell.OpeningCentre(layout, room, opening);
                    Assert.That(Vector3.Distance(centre, new Vector3(x, y, z)), Is.LessThan(0.01f), $"{id} in {room.name} is at {centre}");
                    found++;
                }
            }

            Assert.That(found, Is.GreaterThan(0), $"{id} is not in the layout");
        }

        [Test]
        public void EveryOpening_IsClearAndHasOneSill()
        {
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings)
                {
                    // Shrunk by 1 cm so parts that only touch the gap (sill, lintel, jambs) do not count.
                    ShellPart gap = GapBox(room, opening, -0.01f);
                    foreach ((string owner, ShellPart part) in allParts)
                    {
                        Assert.That(Overlap(part, gap), Is.False, $"{owner}_{part.Name} blocks {opening.id}");
                    }

                    int sills = allParts.Count(p => p.part.Name.StartsWith("Sill_") && p.part.Name.EndsWith("_" + opening.id));
                    Assert.That(sills, Is.EqualTo(1), $"sills under {opening.id}");
                }
            }
        }

        // The ramp's tilted floor and ceiling are left out: their square-cut ends tuck up to 0.16 m into the sill and
        // lintel of the neighbouring room, out of sight, which is what keeps the walking surface continuous.
        [Test]
        public void NoTwoUprightParts_Overlap()
        {
            var upright = allParts.Where(p => p.part.Pitch == 0f).ToList();
            for (int i = 0; i < upright.Count; i++)
            {
                for (int j = i + 1; j < upright.Count; j++)
                {
                    Assert.That(Overlap(upright[i].part, upright[j].part), Is.False,
                        $"{upright[i].room}_{upright[i].part.Name} overlaps {upright[j].room}_{upright[j].part.Name}");
                }
            }
        }

        [Test]
        public void EveryRoom_IsClosedExceptAtItsOpenings()
        {
            foreach (Room room in layout.rooms)
            {
                foreach (Vector3 point in ShellSamplePoints(room))
                {
                    bool solid = allParts.Any(p => p.part.Contains(point));
                    bool inGap = room.openings.Any(o => GapBox(room, o, 0f).Contains(point));
                    Assert.That(solid || inGap, $"hole in the {room.name} shell at {point}");
                }
            }
        }

        [Test]
        public void SharedWalls_AreBuiltOnce_ByTheTallerOrLongerWall()
        {
            List<ShellPart> corridor = BunkerShell.BuildRoom(layout, layout.FindRoom("Corridor"));
            List<ShellPart> ramp = BunkerShell.BuildRoom(layout, layout.FindRoom("BunkerEntranceRamp"));

            // Longer walls close the ends: SecurityArea and Laboratory for the corridor, the lobby and SecurityArea
            // for the ramp.
            Assert.That(corridor.Any(p => p.Name.Contains("_N_") || p.Name.Contains("_S_")), Is.False, "corridor end walls");
            Assert.That(ramp.Any(p => p.Name.Contains("_N_") || p.Name.Contains("_S_")), Is.False, "ramp end walls");

            // The taller GeneratorRoom builds z 95.6 .. 116.4 of the corridor's west wall, its corners included.
            var west = corridor.Where(p => p.Name.StartsWith("Wall_W_")).ToList();
            Assert.That(west.Count, Is.EqualTo(2));
            Assert.That(west[0].Centre.z - west[0].Size.z * 0.5f, Is.EqualTo(82.4f).Within(0.001f));
            Assert.That(west[0].Centre.z + west[0].Size.z * 0.5f, Is.EqualTo(95.6f).Within(0.001f));
            Assert.That(west[1].Centre.z - west[1].Size.z * 0.5f, Is.EqualTo(116.4f).Within(0.001f));
            Assert.That(west[1].Centre.z + west[1].Size.z * 0.5f, Is.EqualTo(123.6f).Within(0.001f));
        }

        [Test]
        public void PartNames_AreUniqueInEachRoom()
        {
            foreach (Room room in layout.rooms)
            {
                Assert.That(BunkerShell.BuildRoom(layout, room).Select(p => p.Name), Is.Unique, room.name);
            }
        }

        // The clear opening: its width and height above the floor, through the whole wall thickness.
        private ShellPart GapBox(Room room, DoorOpening opening, float grow)
        {
            Vector3 centre = BunkerShell.OpeningCentre(layout, room, opening) + Vector3.up * (opening.height * 0.5f);
            float t = layout.wallThickness;
            bool northSouth = opening.wall == WallSide.North || opening.wall == WallSide.South;
            Vector3 size = northSouth ? new Vector3(opening.width, opening.height, t) : new Vector3(t, opening.height, opening.width);
            return new ShellPart("Gap_" + opening.id, centre, size + Vector3.one * (2f * grow));
        }

        // Points in every wall band (full height, slabs included) and inside both slabs of the room.
        private IEnumerable<Vector3> ShellSamplePoints(Room room)
        {
            float t = layout.wallThickness;
            float minX = room.position.x - room.size.x * 0.5f;
            float maxX = room.position.x + room.size.x * 0.5f;
            float minZ = room.position.z - room.size.z * 0.5f;
            float maxZ = room.position.z + room.size.z * 0.5f;
            float height = room.size.y;

            foreach (float x in Range(minX - t + Inset, maxX + t - Inset))
            {
                foreach (float z in new[] { minZ - t * 0.5f, maxZ + t * 0.5f })
                {
                    float floor = BunkerShell.FloorY(room, z);
                    foreach (float y in Range(floor - t + Inset, floor + height + t - Inset))
                    {
                        yield return new Vector3(x, y, z);
                    }
                }
            }

            foreach (float z in Range(minZ + Inset, maxZ - Inset))
            {
                float floor = BunkerShell.FloorY(room, z);
                foreach (float x in new[] { minX - t * 0.5f, maxX + t * 0.5f })
                {
                    foreach (float y in Range(floor - t + Inset, floor + height + t - Inset))
                    {
                        yield return new Vector3(x, y, z);
                    }
                }
            }

            // Keep 0.25 m from the ends of the ramp, where its tilted slabs end square to the slope.
            foreach (float z in Range(minZ + 0.25f, maxZ - 0.25f))
            {
                float floor = BunkerShell.FloorY(room, z);
                foreach (float x in Range(minX + Inset, maxX - Inset))
                {
                    yield return new Vector3(x, floor - t * 0.5f, z);
                    yield return new Vector3(x, floor + height + t * 0.5f, z);
                }
            }
        }

        private static IEnumerable<float> Range(float from, float to)
        {
            for (float value = from; value <= to + 0.0001f; value += Step)
            {
                yield return value;
            }
        }

        // Parts only ever turn about X, so two of them overlap when their x ranges overlap and their (y, z)
        // rectangles overlap. For the rectangles, the separating axis test needs only their edge directions.
        // Touching, or overlapping by under 1 mm, does not count.
        private static bool Overlap(ShellPart a, ShellPart b)
        {
            const float tolerance = 0.001f;
            if (Mathf.Abs(a.Centre.x - b.Centre.x) >= (a.Size.x + b.Size.x) * 0.5f - tolerance)
            {
                return false;
            }

            foreach (ShellPart box in new[] { a, b })
            {
                foreach (Vector3 axis in new[] { box.Rotation * Vector3.up, box.Rotation * Vector3.forward })
                {
                    float distance = Mathf.Abs(Vector3.Dot(b.Centre - a.Centre, axis));
                    if (distance >= HalfExtent(a, axis) + HalfExtent(b, axis) - tolerance)
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        // Half the box's size measured along an axis in the YZ plane.
        private static float HalfExtent(ShellPart box, Vector3 axis)
        {
            float up = Mathf.Abs(Vector3.Dot(box.Rotation * Vector3.up, axis));
            float forward = Mathf.Abs(Vector3.Dot(box.Rotation * Vector3.forward, axis));
            return (up * box.Size.y + forward * box.Size.z) * 0.5f;
        }
    }
}
