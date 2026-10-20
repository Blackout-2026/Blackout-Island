// Owner: World Builder (Security Patrol AI).
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.AI.SecurityPatrol.Tests
{
    public sealed class PhysicsPerceptionRaycastTests
    {
        private static readonly Vector3 Eye = new Vector3(0f, 10000f, 0f);
        private GameObject wall;
        private BoxCollider collider;
        private Perception perception;

        [SetUp]
        public void SetUp()
        {
            int level = LayerMask.NameToLayer("Level");
            Assert.That(level, Is.GreaterThanOrEqualTo(0), "P0 must define the Level layer.");
            wall = new GameObject("Temporary patrol sight wall");
            wall.layer = level;
            wall.transform.position = Eye + Vector3.forward * 5f;
            collider = wall.AddComponent<BoxCollider>();
            collider.size = new Vector3(4f, 4f, 0.4f);
            perception = new Perception(22f, 120f, new PhysicsPerceptionRaycast(1 << level));
            Physics.SyncTransforms();
        }

        [TearDown]
        public void TearDown()
        {
            if (wall != null)
                Object.DestroyImmediate(wall);
        }

        [Test]
        public void LevelWall_BlocksSightUntilMovedOutOfTheRay()
        {
            Vector3 target = Eye + Vector3.forward * 10f;
            perception.UpdateVision(Eye, Vector3.forward, target);
            Assert.That(perception.CanSeePlayer, Is.False);
            Assert.That(perception.HasLastKnownPosition, Is.False);

            wall.transform.position += Vector3.right * 10f;
            Physics.SyncTransforms();
            perception.UpdateVision(Eye, Vector3.forward, target);
            Assert.That(perception.CanSeePlayer, Is.True);
        }

        [Test]
        public void TriggerVolumes_DoNotBlockSight()
        {
            collider.isTrigger = true;
            Physics.SyncTransforms();

            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * 10f);

            Assert.That(perception.CanSeePlayer, Is.True);
        }

        [Test]
        public void ExcludedLayersAndWallsBeyondThePlayer_DoNotBlockSight()
        {
            wall.layer = LayerMask.NameToLayer("Enemy");
            Physics.SyncTransforms();
            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * 10f);
            Assert.That(perception.CanSeePlayer, Is.True);

            wall.layer = LayerMask.NameToLayer("Level");
            wall.transform.position = Eye + Vector3.forward * 15f;
            Physics.SyncTransforms();
            perception.UpdateVision(Eye, Vector3.forward, Eye + Vector3.forward * 10f);
            Assert.That(perception.CanSeePlayer, Is.True);
        }
    }
}
