// Owner: Systems Engineer (Hunter AI).
using System;
using System.Collections.Generic;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The Hunter's hearing with real colliders (docs/reference/systems-engineer.md section 14). The Hunter stands at
    /// the origin and the noise 6.5 m away along +z; walls are thin boxes across the line between them. Every object
    /// is HideAndDontSave and destroyed after the test, so the open scene is not left with unsaved changes. Auto Sync
    /// Transforms is off, so each new collider is followed by Physics.SyncTransforms.
    /// </summary>
    public class HearingSystemTests
    {
        // A sprint footstep (13 m) 6.5 m away: 0.85 in the open (see HearingMathTests).
        private const float SprintRadius = 13f;
        private const float GeneratorRadius = 60f;
        private static readonly Vector3 NoisePosition = new Vector3(0f, 0f, 6.5f);

        private readonly List<Object> created = new List<Object>();
        private HearingConfig config;
        private GameObject hunter;
        private HearingSystem hearing;
        private float now;
        private bool sceneUnsavedBeforeSetUp;

        /// <summary>Counts calls, so a test can tell whether the raycast was skipped.</summary>
        private sealed class CountingOcclusion : IOcclusionCounter
        {
            public int Calls;
            public int Walls;

            public int CountWalls(Vector3 from, Vector3 to, int maxWalls)
            {
                Calls++;
                return Mathf.Min(Walls, maxWalls);
            }
        }

        [SetUp]
        public void SetUp()
        {
            sceneUnsavedBeforeSetUp = SceneManager.GetActiveScene().isDirty;
            now = 0f;
            config = Track(ScriptableObject.CreateInstance<HearingConfig>());
            hunter = Track(Hidden(new GameObject("Hunter")));
            hearing = hunter.AddComponent<HearingSystem>();
            hearing.SetConfig(config);
            hearing.SetClock(() => now);
        }

        [TearDown]
        public void TearDown()
        {
            hearing.StopListening();
            for (int i = created.Count - 1; i >= 0; i--)
            {
                if (created[i] != null)
                {
                    Object.DestroyImmediate(created[i]);
                }
            }

            created.Clear();
            Physics.SyncTransforms();
        }

        [Test]
        public void OpenSpace_VersusOneWall_VersusTwoWalls()
        {
            float open = HearAndRead();
            Assert.AreEqual(0, hearing.LastWallCount);

            Wall(2f);
            float oneWall = HearAndRead();
            Assert.AreEqual(1, hearing.LastWallCount);

            Wall(4f);
            float twoWalls = HearAndRead();
            Assert.AreEqual(2, hearing.LastWallCount);

            Assert.AreEqual(0.85f, open, 1e-4f);
            Assert.AreEqual(0.2975f, oneWall, 1e-4f);
            Assert.AreEqual(0.104125f, twoWalls, 1e-4f);
        }

        [Test]
        public void ThreeWalls_MakeTheFootstepInaudible()
        {
            Wall(1.5f);
            Wall(3f);
            Wall(4.5f);

            Assert.IsFalse(hearing.Hear(Sprint()));
            Assert.AreEqual(3, hearing.LastWallCount);
            Assert.IsFalse(hearing.HasNoise);
        }

        [Test]
        public void FourWalls_CountAsThree_SoTheGeneratorIsStillHeard()
        {
            Wall(1f);
            Wall(2.5f);
            Wall(4f);
            Wall(5.5f);

            Assert.IsTrue(hearing.Hear(new NoiseEvent(NoisePosition, GeneratorRadius, NoiseType.Generator, null)));
            Assert.AreEqual(3, hearing.LastWallCount);
            float expected = (1f - 6.5f / GeneratorRadius) * 0.042875f * 1.7f;
            Assert.AreEqual(expected, hearing.PerceivedLoudness, 1e-4f);
        }

        [Test]
        public void ClosedDoor_OnInteractable_Muffles()
        {
            Wall(3f, PhysicsLayers.Interactable);

            hearing.Hear(Sprint());
            Assert.AreEqual(1, hearing.LastWallCount);
        }

        [Test]
        public void Triggers_Agents_AndIgnoreRaycast_DoNotMuffle()
        {
            Wall(1.5f).GetComponent<BoxCollider>().isTrigger = true;
            Wall(3f, PhysicsLayers.Enemy);
            Wall(4.5f, PhysicsLayers.IgnoreRaycast);

            hearing.Hear(Sprint());
            Assert.AreEqual(0, hearing.LastWallCount);
            Assert.AreEqual(0.85f, hearing.PerceivedLoudness, 1e-4f);
        }

        [Test]
        public void Terrain_IsNotAWall()
        {
            // A 4 m ridge across the line between the noise and the Hunter, on the Level layer.
            var data = Track(new TerrainData { heightmapResolution = 33 });
            data.size = new Vector3(20f, 4f, 20f);
            var heights = new float[33, 33];
            for (int x = 0; x < 33; x++)
            {
                for (int z = 20; z < 23; z++)
                {
                    heights[z, x] = 1f;
                }
            }

            data.SetHeights(0, 0, heights);
            GameObject terrain = Track(Hidden(Terrain.CreateTerrainGameObject(data)));
            terrain.layer = PhysicsLayers.Level;
            terrain.transform.position = new Vector3(-10f, 0f, -10f);
            Physics.SyncTransforms();

            Vector3 from = NoisePosition + Vector3.up * config.sourceHeight;
            Vector3 to = Vector3.up * config.listenerHeight;
            Assert.IsTrue(Physics.Linecast(from, to, config.occluders, QueryTriggerInteraction.Ignore),
                "the ridge really is between them");

            hearing.Hear(Sprint());
            Assert.AreEqual(0, hearing.LastWallCount);
        }

        [Test]
        public void OwnNoises_AreIgnored()
        {
            var gun = Track(Hidden(new GameObject("Gun")));
            gun.transform.SetParent(hunter.transform);

            Assert.IsFalse(hearing.Hear(new NoiseEvent(NoisePosition, 40f, NoiseType.Gunshot, hunter)));
            Assert.IsFalse(hearing.Hear(new NoiseEvent(NoisePosition, 40f, NoiseType.Gunshot, gun)));
            Assert.IsFalse(hearing.HasNoise);
        }

        [Test]
        public void NoiseFromTheChannel_IsHeard_UntilItStopsListening()
        {
            var channel = Track(ScriptableObject.CreateInstance<NoiseEventChannel>());
            hearing.SetChannel(channel);
            hearing.StartListening();

            channel.Raise(Sprint());
            Assert.IsTrue(hearing.HasNoise);

            hearing.Forget();
            hearing.StopListening();
            channel.Raise(Sprint());
            Assert.IsFalse(hearing.HasNoise);
        }

        [Test]
        public void ExposesLoudnessDirectionAndTimeSince_AndDecays()
        {
            hearing.Hear(new NoiseEvent(new Vector3(3f, 5f, 4f), SprintRadius, NoiseType.Sprint, null));
            float heard = hearing.PerceivedLoudness;

            Assert.AreEqual(0f, Vector3.Distance(new Vector3(0.6f, 0f, 0.8f), hearing.NoiseDirection), 1e-5f,
                "flat, unit length");
            Assert.AreEqual(NoiseType.Sprint, hearing.NoiseType);
            Assert.AreEqual(0f, hearing.TimeSinceNoise);

            now = 4f;
            Assert.AreEqual(heard * 0.5f, hearing.PerceivedLoudness, 1e-5f);
            Assert.AreEqual(4f, hearing.TimeSinceNoise);

            now = 8f;
            Assert.AreEqual(0f, hearing.PerceivedLoudness);
            Assert.IsFalse(hearing.HasNoise);
            Assert.AreEqual(Vector3.zero, hearing.NoiseDirection);
        }

        [Test]
        public void NothingHeard_ExposesSilence()
        {
            Assert.AreEqual(0f, hearing.PerceivedLoudness);
            Assert.AreEqual(Vector3.zero, hearing.NoiseDirection);
            Assert.AreEqual(float.PositiveInfinity, hearing.TimeSinceNoise);
        }

        [Test]
        public void Raycast_IsSkipped_WhenTheNoiseCannotBeHeardOrCannotWin()
        {
            var occlusion = new CountingOcclusion();
            hearing.SetOcclusion(occlusion);

            hearing.Hear(new NoiseEvent(new Vector3(0f, 0f, 20f), SprintRadius, NoiseType.Sprint, null));
            Assert.AreEqual(0, occlusion.Calls, "beyond its radius");

            hearing.Hear(new NoiseEvent(NoisePosition, GeneratorRadius, NoiseType.Generator, null));
            Assert.AreEqual(1, occlusion.Calls);

            hearing.Hear(Sprint());
            Assert.AreEqual(1, occlusion.Calls, "0.85 in the open cannot beat the generator's 1.52");
        }

        [Test]
        public void NoConfig_UsesTheDefaults_AndWarns()
        {
            var bare = Track(Hidden(new GameObject("Bare"))).AddComponent<HearingSystem>();
            bare.SetClock(() => now);
            LogAssert.Expect(LogType.Warning, "[Blackout] HearingSystem has no HearingConfig; using the defaults.");

            bare.Hear(Sprint());
            Assert.AreEqual(1.7f, bare.Config.hearing);
            Assert.AreEqual(0.85f, bare.PerceivedLoudness, 1e-4f);
            Track(bare.Config);
        }

        [Test]
        public void Hearing_AllocatesNothing()
        {
            Wall(2f);
            Wall(4f);
            NoiseEvent noise = Sprint();
            for (int i = 0; i < 10; i++)
            {
                hearing.Hear(noise);
                _ = hearing.PerceivedLoudness + hearing.TimeSinceNoise;
                _ = hearing.NoiseDirection;
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
            {
                now += 0.05f;
                hearing.Hear(noise);
                _ = hearing.PerceivedLoudness + hearing.TimeSinceNoise;
                _ = hearing.NoiseDirection;
            }

            Assert.AreEqual(0, GC.GetAllocatedBytesForCurrentThread() - before);
            Assert.AreEqual(2, hearing.LastWallCount, "the raycast ran every time");
        }

        [Test]
        public void TestObjects_DoNotLeaveTheSceneUnsaved()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (sceneUnsavedBeforeSetUp)
            {
                Assert.Inconclusive("An earlier test left the scene unsaved, so this one cannot tell.");
            }

            Wall(2f);
            hearing.Hear(Sprint());
            TearDown();

            Assert.IsFalse(scene.isDirty);
            SetUp();
        }

        private float HearAndRead()
        {
            hearing.Forget();
            hearing.Hear(Sprint());
            return hearing.PerceivedLoudness;
        }

        private static NoiseEvent Sprint()
        {
            return new NoiseEvent(NoisePosition, SprintRadius, NoiseType.Sprint, null);
        }

        private GameObject Wall(float z, int layer = PhysicsLayers.Level)
        {
            GameObject wall = Track(Hidden(new GameObject("Wall")));
            wall.layer = layer;
            wall.transform.position = new Vector3(0f, 2f, z);
            wall.AddComponent<BoxCollider>().size = new Vector3(10f, 4f, 0.3f);
            Physics.SyncTransforms();
            return wall;
        }

        private static GameObject Hidden(GameObject gameObject)
        {
            gameObject.hideFlags = HideFlags.HideAndDontSave;
            return gameObject;
        }

        private T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
