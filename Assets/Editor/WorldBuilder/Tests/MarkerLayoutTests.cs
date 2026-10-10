using System.Collections.Generic;
using System.Linq;
using Blackout.Shared;
using Blackout.World;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class MarkerLayoutTests
    {
        private const string LayoutPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";
        private const float AgentRadius = 0.4f; // NavMesh agent radius (playbook W4)

        private BunkerLayout layout;
        private List<MarkerSpec> markers;

        [OneTimeSetUp]
        public void BuildMarkers()
        {
            layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(LayoutPath);
            Assert.That(layout, Is.Not.Null, LayoutPath);
            markers = MarkerLayout.Build(layout);
        }

        // The names the W3 brief asks for.
        [TestCase("PlayerStart", MarkerType.PlayerStart)]
        [TestCase("Checkpoint_Beach", MarkerType.Checkpoint)]
        [TestCase("Checkpoint_Forest", MarkerType.Checkpoint)]
        [TestCase("Checkpoint_BunkerEntrance", MarkerType.Checkpoint)]
        [TestCase("Checkpoint_GeneratorRoom", MarkerType.Checkpoint)]
        [TestCase("GeneratorSocket", MarkerType.GeneratorSocket)]
        [TestCase("CureSocket", MarkerType.CureSocket)]
        [TestCase("BoatExit", MarkerType.BoatExit)]
        public void RequiredMarker_Exists(string name, MarkerType type)
        {
            Assert.That(markers.Count(m => m.Name == name && m.Type == type), Is.EqualTo(1));
        }

        [TestCase("Hunter-1", 6)]
        [TestCase("Hunter-2", 6)]
        [TestCase("Patrol-1", 6)]
        [TestCase("Patrol-2", 4)]
        [TestCase("Patrol-3", 4)]
        [TestCase("TG-01", 1)]
        [TestCase("TG-02", 1)]
        [TestCase("Commander", 1)]
        public void Agent_HasOneSpawnAndItsRoute(string agent, int waypoints)
        {
            Assert.That(markers.Count(m => m.Type == MarkerType.AgentSpawn && m.Owner == agent), Is.EqualTo(1), "spawns");
            List<string> route = markers.Where(m => m.Type == MarkerType.Waypoint && m.Owner == agent).Select(m => m.Name).ToList();
            Assert.That(route.Count, Is.EqualTo(waypoints), "waypoints");
            Assert.That(route, Is.Ordered, "waypoints are numbered in route order");
        }

        [Test]
        public void AgentMarkers_BelongToTheEightAgents()
        {
            Assert.That(markers.Count(m => m.Type == MarkerType.AgentSpawn), Is.EqualTo(8));
            var agents = new HashSet<string>(markers.Where(m => m.Type == MarkerType.AgentSpawn).Select(m => m.Owner));
            foreach (MarkerSpec marker in markers)
            {
                bool agentMarker = marker.Type == MarkerType.AgentSpawn || marker.Type == MarkerType.Waypoint;
                Assert.That(agentMarker ? agents.Contains(marker.Owner) : marker.Owner == "", Is.True, $"owner of {marker.Name}");
            }
        }

        [Test]
        public void CoverPoints_AtLeastFourInTheCorridorAndGeneratorRoom()
        {
            Room corridor = layout.FindRoom("Corridor");
            Room generator = layout.FindRoom("GeneratorRoom");
            int count = markers.Count(m => m.Type == MarkerType.CoverPoint
                && (Interior(corridor, m.Position) || Interior(generator, m.Position)));
            Assert.That(count, Is.GreaterThanOrEqualTo(4));
        }

        [Test]
        public void Names_AreUnique_AndGroupedNamesStartWithTheirType()
        {
            Assert.That(markers.Select(m => m.Name), Is.Unique);
            foreach (MarkerSpec marker in markers)
            {
                string type = marker.Type.ToString();
                Assert.That(marker.Name == type || marker.Name.StartsWith(type + "_"), Is.True, marker.Name);
            }
        }

        // Every marker inside the bunker (door sockets sit in their gaps instead) is in a room, on its floor, at
        // least an agent radius from the walls.
        [Test]
        public void BunkerMarkers_AreOnARoomFloorClearOfTheWalls()
        {
            foreach (MarkerSpec marker in markers.Where(m => m.Type != MarkerType.DoorSocket))
            {
                Room room = layout.rooms.FirstOrDefault(r => Footprint(r, marker.Position, layout.wallThickness));
                if (room == null)
                {
                    continue; // outdoors
                }

                Assert.That(Interior(room, marker.Position, AgentRadius), Is.True, $"{marker.Name} is within {AgentRadius} m of a {room.name} wall");
                float floor = BunkerShell.FloorY(room, marker.Position.z);
                if (marker.Name == "HealthPickup_Catwalk")
                {
                    Assert.That(marker.Position.y, Is.EqualTo(5.5f).Within(0.001f), "catwalk deck");
                    continue;
                }

                Assert.That(marker.Position.y, Is.EqualTo(floor).Within(0.001f), $"{marker.Name} is off the {room.name} floor");
            }
        }

        // Ground heights from the reference (section 2), which the prototype read off a 2 m grid: allow 15 cm.
        [TestCase("PlayerStart", 1.3f)]
        [TestCase("BoatExit", 1.3f)]
        [TestCase("Checkpoint_Forest", 1.77f)]
        [TestCase("Checkpoint_BunkerEntrance", 10.04f)]
        [TestCase("Checkpoint_EscapeForest", 10.04f)]
        [TestCase("AgentSpawn_Hunter-1", 4.6f)]
        [TestCase("AgentSpawn_Hunter-2", 9.2f)]
        [TestCase("AgentSpawn_Patrol-1", 10f)]
        public void OutdoorMarker_IsOnTheGround(string name, float y)
        {
            Assert.That(markers.Single(m => m.Name == name).Position.y, Is.EqualTo(y).Within(0.15f));
        }

        [Test]
        public void BarricadeCrateSpace_IsClearOfMarkers()
        {
            float reach = MarkerLayout.BarricadeCrateSize * 0.5f + AgentRadius;
            Vector3 crate = MarkerLayout.BarricadeCrateCentre;
            foreach (MarkerSpec marker in markers)
            {
                bool inside = Mathf.Abs(marker.Position.x - crate.x) < reach && Mathf.Abs(marker.Position.z - crate.z) < reach;
                Assert.That(inside, Is.False, $"{marker.Name} is in the crate's space");
            }
        }

        // Only door gaps get a socket (the two open ramp passages have no door). It faces into the room you enter.
        [TestCase("D-Bunker", 0f)]
        [TestCase("D-Security", 0f)]
        [TestCase("D-Generator", 270f)]
        [TestCase("D-Lab", 0f)]
        public void DoorSocket_IsInItsGap_FacingIn(string id, float yaw)
        {
            Assert.That(markers.Count(m => m.Type == MarkerType.DoorSocket), Is.EqualTo(4));
            MarkerSpec socket = markers.Single(m => m.Name == "DoorSocket_" + id);
            foreach (Room room in layout.rooms)
            {
                foreach (DoorOpening opening in room.openings.Where(o => o.id == id))
                {
                    Assert.That(Vector3.Distance(socket.Position, BunkerShell.OpeningCentre(layout, room, opening)), Is.LessThan(0.001f), room.name);
                }
            }

            Assert.That(socket.Yaw, Is.EqualTo(yaw));
        }

        [Test]
        public void ApplyMarker_SetsTheMarkerTypeIdAndOwner()
        {
            var go = new GameObject("Waypoint_Patrol-1_02");
            try
            {
                var spec = new MarkerSpec(MarkerType.Waypoint, "Waypoint_Patrol-1_02", "Patrol-1", Vector3.zero, 0f);
                Marker marker = MarkerBuilder.ApplyMarker(go, spec);
                Assert.That(marker.Type, Is.EqualTo(MarkerType.Waypoint));
                Assert.That(marker.Id, Is.EqualTo("Waypoint_Patrol-1_02"));
                Assert.That(marker.Owner, Is.EqualTo("Patrol-1"));
                Assert.That(MarkerBuilder.ApplyMarker(go, spec), Is.SameAs(marker), "a re-run reuses the component");
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        // Inside the clear interior of the room, at least `margin` from every wall.
        private static bool Interior(Room room, Vector3 point, float margin = 0f)
        {
            return Mathf.Abs(point.x - room.position.x) <= room.size.x * 0.5f - margin
                && Mathf.Abs(point.z - room.position.z) <= room.size.z * 0.5f - margin;
        }

        // Inside the room's outer footprint, walls included.
        private static bool Footprint(Room room, Vector3 point, float wallThickness)
        {
            return Mathf.Abs(point.x - room.position.x) < room.size.x * 0.5f + wallThickness
                && Mathf.Abs(point.z - room.position.z) < room.size.z * 0.5f + wallThickness;
        }
    }
}
