using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Bakes small test layouts with the tool's build settings, far below the island, and checks that the agreed agent
    /// size behaves as intended: door gaps narrower than the agent are closed, steps up to the step height are climbable.
    /// </summary>
    public class NavMeshBakerTests
    {
        private static readonly Vector3 Origin = new Vector3(1000f, -500f, 1000f); // far from the island's NavMesh
        private const float FloorSize = 12f;
        private const float WallHeight = 3f;
        private const float WallThickness = 0.4f;

        private NavMeshData data;
        private NavMeshDataInstance instance;

        [TearDown]
        public void TearDown()
        {
            instance.Remove();
            instance = default;
            if (data != null)
            {
                Object.DestroyImmediate(data);
                data = null;
            }
        }

        [Test]
        public void BuildSettings_UseTheAgreedAgentSize()
        {
            NavMeshBuildSettings settings = NavMeshBaker.CreateBuildSettings();

            Assert.That(settings.agentTypeID, Is.EqualTo(0), "Humanoid, the type default NavMesh queries use");
            Assert.That(settings.agentRadius, Is.EqualTo(0.4f));
            Assert.That(settings.agentHeight, Is.EqualTo(1.8f));
            Assert.That(settings.agentClimb, Is.EqualTo(0.4f));
            Assert.That(settings.agentSlope, Is.EqualTo(40f));
        }

        [Test]
        public void BakeBounds_CoverTheTerrainFromSeaBedToHilltop()
        {
            Bounds bounds = NavMeshBaker.BakeBounds();

            Assert.That(bounds.min.x, Is.EqualTo(-200f).Within(0.001f));
            Assert.That(bounds.max.x, Is.EqualTo(200f).Within(0.001f));
            Assert.That(bounds.min.z, Is.EqualTo(-180f).Within(0.001f));
            Assert.That(bounds.max.z, Is.EqualTo(220f).Within(0.001f));
            Assert.That(bounds.min.y, Is.LessThan(-5.93f), "lowest sea bed");
            Assert.That(bounds.max.y, Is.GreaterThan(21.52f + NavMeshBaker.AgentHeight), "an agent standing on the hilltop");
        }

        [TestCase(1.4f, true)]
        [TestCase(0.6f, false)]
        public void DoorGap_IsPassableOnlyIfWiderThanTheAgent(float gap, bool passable)
        {
            // A wall across the middle of the floor, with a gap of the given width in its centre.
            float side = (FloorSize - gap) * 0.5f;
            float sideCentre = (gap + side) * 0.5f;
            var sources = new List<NavMeshBuildSource>
            {
                Floor(),
                Box(new Vector3(-sideCentre, WallHeight * 0.5f, 0f), new Vector3(side, WallHeight, WallThickness)),
                Box(new Vector3(sideCentre, WallHeight * 0.5f, 0f), new Vector3(side, WallHeight, WallThickness)),
            };

            Bake(sources);

            Assert.That(PathIsComplete(new Vector3(0f, 0f, -4f), new Vector3(0f, 0f, 4f)), Is.EqualTo(passable));
        }

        [TestCase(0.3f, true)]
        [TestCase(0.6f, false)]
        public void Step_IsClimbableUpToTheStepHeight(float step, bool climbable)
        {
            // A raised platform over the north half of the floor.
            var sources = new List<NavMeshBuildSource>
            {
                Floor(),
                Box(new Vector3(0f, step * 0.5f, FloorSize * 0.25f), new Vector3(FloorSize, step, FloorSize * 0.5f)),
            };

            Bake(sources);

            Assert.That(PathIsComplete(new Vector3(0f, 0f, -4f), new Vector3(0f, step, 4f)), Is.EqualTo(climbable));
        }

        private void Bake(List<NavMeshBuildSource> sources)
        {
            var bounds = new Bounds(Origin, new Vector3(FloorSize + 2f, 10f, FloorSize + 2f));
            data = NavMeshBuilder.BuildNavMeshData(NavMeshBaker.CreateBuildSettings(), sources, bounds,
                Vector3.zero, Quaternion.identity);
            instance = NavMesh.AddNavMeshData(data);
        }

        private static bool PathIsComplete(Vector3 from, Vector3 to)
        {
            var filter = new NavMeshQueryFilter { agentTypeID = NavMeshBaker.AgentTypeId, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(Origin + from, out NavMeshHit start, 0.5f, filter)
                || !NavMesh.SamplePosition(Origin + to, out NavMeshHit end, 0.5f, filter))
            {
                return false;
            }

            var path = new NavMeshPath();
            NavMesh.CalculatePath(start.position, end.position, filter, path);
            return path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>A 0.4 m slab whose top is at the origin's height.</summary>
        private static NavMeshBuildSource Floor()
        {
            return Box(new Vector3(0f, -0.2f, 0f), new Vector3(FloorSize, 0.4f, FloorSize));
        }

        private static NavMeshBuildSource Box(Vector3 centre, Vector3 size)
        {
            return new NavMeshBuildSource
            {
                shape = NavMeshBuildSourceShape.Box,
                transform = Matrix4x4.Translate(Origin + centre),
                size = size,
                area = 0,
            };
        }
    }
}
