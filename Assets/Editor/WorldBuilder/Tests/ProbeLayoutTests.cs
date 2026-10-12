using System.Collections.Generic;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class ProbeLayoutTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";
        private const float Margin = 0.4f; // a probe this close to a wall, floor or ceiling would sample the wall

        private static BunkerLayout Layout => AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);

        [Test]
        public void Steps_SpacesValuesEvenlyAndKeepsBothEnds()
        {
            Assert.That(ProbeLayout.Steps(0f, 10f, 3f), Is.EqualTo(new[] { 0f, 2.5f, 5f, 7.5f, 10f }));
            Assert.That(ProbeLayout.Steps(0f, 6f, 3f), Is.EqualTo(new[] { 0f, 3f, 6f }));
            Assert.That(ProbeLayout.Steps(2f, 2f, 3f), Is.EqualTo(new[] { 2f }));
        }

        [Test]
        public void ReflectionProbes_OnePerRoomWithTheRoomAsItsBox()
        {
            BunkerLayout layout = Layout;
            List<ReflectionProbeSpec> probes = ProbeLayout.ReflectionProbes(layout);
            Assert.That(probes.Count, Is.EqualTo(layout.rooms.Count));
            foreach (ReflectionProbeSpec probe in probes)
            {
                Room room = layout.FindRoom(probe.Room);
                var box = new Bounds(probe.BoxCentre, probe.BoxSize);
                Assert.That(box.Contains(probe.Position), Is.True, probe.Room);
                Assert.That(probe.BoxSize.x, Is.EqualTo(room.size.x));
                Assert.That(probe.BoxSize.z, Is.EqualTo(room.size.z));
                Assert.That(probe.BoxSize.y, Is.EqualTo(room.size.y + Mathf.Abs(room.floorDropNorth)).Within(1e-4f), probe.Room);
                Assert.That(probe.Position.y - BunkerShell.FloorY(room, probe.Position.z), Is.EqualTo(ProbeLayout.EyeHeight).Within(1e-4f));
            }
        }

        [Test]
        public void BunkerProbes_AreInsideARoomOrADoorGap()
        {
            BunkerLayout layout = Layout;
            List<Vector3> openings = OpeningCentres(layout);
            foreach (Vector3 p in ProbeLayout.BunkerProbes(layout))
            {
                bool inRoom = layout.rooms.Exists(room => InsideRoom(room, p));
                bool inGap = openings.Exists(o => Mathf.Abs(o.x - p.x) < 0.01f && Mathf.Abs(o.z - p.z) < 0.01f
                    && p.y - o.y >= Margin && p.y - o.y <= 2.3f - Margin);
                Assert.That(inRoom || inGap, Is.True, $"probe {p} is in a wall, floor or ceiling");
            }
        }

        [Test]
        public void BunkerProbes_HaveLayersUpToTheCeilingAndNoneInTheGenerator()
        {
            BunkerLayout layout = Layout;
            List<Vector3> probes = ProbeLayout.BunkerProbes(layout);
            Assert.That(probes.Exists(p => ProbeLayout.Generator.Contains(p)), Is.False);

            Room generator = layout.FindRoom("GeneratorRoom");
            var heights = new HashSet<float>();
            foreach (Vector3 p in probes)
            {
                if (InsideRoom(generator, p))
                {
                    heights.Add(Mathf.Round((p.y - generator.position.y) * 100f) / 100f);
                }
            }

            Assert.That(heights.Count, Is.EqualTo(4), "6.5 m ceiling: layers at most 2 m apart from 0.5 to 6.0 m");
        }

        [Test]
        public void TrailProbes_FollowTheWholeTrailOverTheGround()
        {
            List<Vector3> probes = ProbeLayout.TrailProbes(IslandHeightField.Sample);
            foreach (Vector2 point in IslandLayout.TrailControlPoints)
            {
                Assert.That(probes.Exists(p => Vector2.Distance(new Vector2(p.x, p.z), point) < 4f), Is.True, $"no probe near {point}");
            }

            foreach (Vector3 p in probes)
            {
                bool onDock = Mathf.Abs(p.x) < 0.01f && p.z < IslandLayout.DockMaxZ;
                float ground = onDock ? IslandLayout.DockDeckY : IslandHeightField.Sample(p.x, p.z);
                Assert.That(p.y - ground, Is.EqualTo(0.5f).Within(1e-3f).Or.EqualTo(2.5f).Within(1e-3f), $"probe {p}");
            }
        }

        [Test]
        public void TrailStations_AreAtMost6mApartFromEndToEnd()
        {
            Vector2[] line = IslandHeightField.TrailPolyline();
            List<Vector2[]> stations = ProbeLayout.TrailStations(line, ProbeLayout.TrailSpacing);
            Assert.That(Vector2.Distance(stations[0][0], line[0]), Is.LessThan(1e-3f));
            Assert.That(Vector2.Distance(stations[stations.Count - 1][0], line[line.Length - 1]), Is.LessThan(1e-3f));
            for (int i = 1; i < stations.Count; i++)
            {
                Assert.That(Vector2.Distance(stations[i - 1][0], stations[i][0]), Is.LessThanOrEqualTo(ProbeLayout.TrailSpacing + 1e-3f));
            }
        }

        [Test]
        public void CompoundProbes_KeepClearOfTheLobbyAndTheRamp()
        {
            BunkerLayout layout = Layout;
            List<Vector3> probes = ProbeLayout.CompoundProbes(layout, IslandHeightField.Sample);
            Assert.That(probes.Count, Is.GreaterThan(50));
            foreach (string name in new[] { "BunkerEntranceLobby", "BunkerEntranceRamp" })
            {
                Room room = layout.FindRoom(name);
                float halfX = room.size.x * 0.5f + layout.wallThickness;
                float halfZ = room.size.z * 0.5f + layout.wallThickness;
                Assert.That(probes.Exists(p => Mathf.Abs(p.x - room.position.x) <= halfX && Mathf.Abs(p.z - room.position.z) <= halfZ),
                    Is.False, $"a compound probe is inside {name}");
            }
        }

        private static bool InsideRoom(Room room, Vector3 p)
        {
            float floor = BunkerShell.FloorY(room, p.z);
            return Mathf.Abs(p.x - room.position.x) <= room.size.x * 0.5f - Margin
                && Mathf.Abs(p.z - room.position.z) <= room.size.z * 0.5f - Margin
                && p.y >= floor + Margin && p.y <= floor + room.size.y - Margin;
        }

        private static List<Vector3> OpeningCentres(BunkerLayout layout)
        {
            var centres = new List<Vector3>();
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings)
                {
                    centres.Add(BunkerShell.OpeningCentre(layout, room, opening));
                }
            }

            return centres;
        }
    }
}
