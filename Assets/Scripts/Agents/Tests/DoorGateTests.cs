using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    /// <summary>Step F2: agents ask a closed door to open and wait for it; they never walk through a closed or locked one.</summary>
    public class DoorGateTests
    {
        private sealed class FakeDoor : IDoor
        {
            public Vector3 Position { get; set; }
            public bool IsOpen { get; set; }
            public bool IsLocked { get; set; }
            public int Requests;

            public bool RequestOpen(GameObject by)
            {
                if (IsLocked || IsOpen)
                {
                    return false;
                }

                Requests++;
                return true;
            }
        }

        private static readonly Vector2 East = Vector2.right;

        private static List<IDoor> Doors(params FakeDoor[] doors) => new List<IDoor>(doors);

        [Test]
        public void ClosedDoorAhead_IsAskedToOpenAndTheAgentWaits()
        {
            var door = new FakeDoor { Position = new Vector3(1.5f, 0f, 0f) };

            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(door)), Is.True);
            Assert.That(door.Requests, Is.EqualTo(1));
        }

        [Test]
        public void OpenDoor_DoesNotStopTheAgent()
        {
            var door = new FakeDoor { Position = new Vector3(1.5f, 0f, 0f), IsOpen = true };

            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(door)), Is.False);
        }

        [Test]
        public void LockedDoor_StopsTheAgentButIsNotOpened()
        {
            var door = new FakeDoor { Position = new Vector3(1.5f, 0f, 0f), IsLocked = true };

            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(door)), Is.True);
            Assert.That(door.Requests, Is.Zero);
        }

        [Test]
        public void DoorBehind_FarAway_OrOnAnotherFloor_IsIgnored()
        {
            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(new FakeDoor { Position = new Vector3(-1.5f, 0f, 0f) })), Is.False, "behind");
            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(new FakeDoor { Position = new Vector3(DoorGate.LookAhead + 0.5f, 0f, 0f) })), Is.False, "far");
            Assert.That(DoorGate.MustWait(Vector3.zero, East, null, Doors(new FakeDoor { Position = new Vector3(1.5f, 7.5f, 0f) })), Is.False, "upstairs");
        }

        [Test]
        public void StandingAgent_AsksForNothing()
        {
            var door = new FakeDoor { Position = new Vector3(1f, 0f, 0f) };

            Assert.That(DoorGate.MustWait(Vector3.zero, Vector2.zero, null, Doors(door)), Is.False);
            Assert.That(door.Requests, Is.Zero);
        }

        [Test]
        public void TheMover_StopsAtAClosedDoorAndWalksThroughOnceItIsOpen()
        {
            var door = new FakeDoor { Position = new Vector3(5f, 0f, 0f) };
            DoorRegistry.Register(door);
            var go = new GameObject("DoorWalker");
            try
            {
                var follower = go.AddComponent<PathFollower>();
                follower.Configure(new FlatFloor());
                follower.Settings.repathJitter = 0f;
                follower.SetDestination(new Vector3(12f, 0f, 0f), MoveSpeed.Walk);

                for (int i = 0; i < 600; i++)
                {
                    follower.Tick(0.02f);
                }

                Assert.That(go.transform.position.x, Is.LessThan(5f), "it did not pass the closed door");
                Assert.That(go.transform.position.x, Is.GreaterThan(5f - DoorGate.LookAhead - 0.5f), "it walked up to it");
                Assert.That(door.Requests, Is.GreaterThan(0), "and asked it to open");

                door.IsOpen = true;
                for (int i = 0; i < 1000 && !follower.HasArrived; i++)
                {
                    follower.Tick(0.02f);
                }

                Assert.That(follower.HasArrived, Is.True, "through the open door to the destination");
            }
            finally
            {
                DoorRegistry.Unregister(door);
                Object.DestroyImmediate(go);
            }
        }

        // An endless flat floor with the straight line as the path.
        private sealed class FlatFloor : IPathProvider
        {
            public bool TryCalculatePath(Vector3 from, Vector3 to, out Vector3[] corners)
            {
                corners = new[] { from, to };
                return true;
            }

            public bool TrySnap(Vector3 position, float maxDistance, out Vector3 snapped)
            {
                snapped = new Vector3(position.x, 0f, position.z);
                return true;
            }

            public bool IsBlocked(Vector3 from, Vector3 to) => false;
        }
    }
}
