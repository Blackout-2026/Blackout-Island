using System.Collections.Generic;
using System.Reflection;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentSpawnerTests
    {
        private readonly List<Object> created = new List<Object>();
        private readonly List<Marker> made = new List<Marker>();
        private GameObject registryObject;
        private AgentRegistry registry;
        private DifficultyConfig difficulty;
        private AgentSpawner spawner;
        private GameObject prefab;

        [SetUp]
        public void SetUp()
        {
            registryObject = new GameObject("Registry");
            registry = registryObject.AddComponent<AgentRegistry>();
            difficulty = Keep(ScriptableObject.CreateInstance<DifficultyConfig>());
            spawner = Keep(new GameObject("Spawner")).AddComponent<AgentSpawner>();

            // A scene object stands in for the prefab: the spawner only needs something to Instantiate.
            prefab = Keep(new GameObject("AgentPrefab"));
            prefab.AddComponent<Health>().ResetHealth(0f);
            prefab.AddComponent<AgentShooter>();
            prefab.AddComponent<PathFollower>();
            prefab.AddComponent<AgentRoot>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject agent in new List<GameObject>(spawner.Spawned))
            {
                if (agent != null)
                {
                    Object.DestroyImmediate(agent);
                }
            }
            foreach (Object o in created)
            {
                if (o != null)
                {
                    Object.DestroyImmediate(o);
                }
            }
            Object.DestroyImmediate(registryObject);
        }

        private T Keep<T>(T o) where T : Object
        {
            created.Add(o);
            return o;
        }

        private Marker AddMarker(MarkerType type, string owner, Vector3 position, float yaw = 0f)
        {
            var go = Keep(new GameObject(type + "_" + owner));
            go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            Marker marker = go.AddComponent<Marker>();
            typeof(Marker).GetField("type", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, type);
            typeof(Marker).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(marker, owner);
            made.Add(marker);
            registry.SetMarkers(made); // only this test's markers count, whatever scene is open in the Editor
            return marker;
        }

        private static AgentSpawner.SpawnEntry Entry(AgentKind kind, GameObject prefab, string ownerPrefix)
        {
            return new AgentSpawner.SpawnEntry { kind = kind, prefab = prefab, ownerPrefix = ownerPrefix };
        }

        private List<AgentSpawner.SpawnEntry> AllFourEntries()
        {
            return new List<AgentSpawner.SpawnEntry>
            {
                Entry(AgentKind.Hunter, prefab, "Hunter-"),
                Entry(AgentKind.SecurityPatrol, prefab, "Patrol-"),
                Entry(AgentKind.TacticalGuard, prefab, "TG-"),
                Entry(AgentKind.Commander, prefab, "Commander"),
            };
        }

        private void AddTheEightSpawnMarkers()
        {
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", new Vector3(-24f, 0f, 54f));
            AddMarker(MarkerType.AgentSpawn, "Hunter-2", new Vector3(26f, 0f, 20f));
            AddMarker(MarkerType.AgentSpawn, "Patrol-1", new Vector3(-14f, 0f, -36f));
            AddMarker(MarkerType.AgentSpawn, "Patrol-2", new Vector3(3f, 0f, -74f));
            AddMarker(MarkerType.AgentSpawn, "Patrol-3", new Vector3(0f, 0f, -88f));
            AddMarker(MarkerType.AgentSpawn, "TG-01", new Vector3(-8f, 0f, -100f));
            AddMarker(MarkerType.AgentSpawn, "TG-02", new Vector3(3f, 0f, -129f));
            AddMarker(MarkerType.AgentSpawn, "Commander", new Vector3(6f, 0f, -138f));
        }

        [Test]
        public void SpawnAll_CreatesOneAgentForEachSpawnMarker_TwoThreeTwoOne()
        {
            AddTheEightSpawnMarkers();
            spawner.Configure(registry, difficulty, AllFourEntries());

            int count = spawner.SpawnAll();

            Assert.AreEqual(8, count);
            Assert.AreEqual(8, spawner.Spawned.Count);
        }

        [Test]
        public void SpawnAll_NamesEachAgentAfterItsMarkerOwner_ToMatchItsWaypoints()
        {
            AddTheEightSpawnMarkers();
            spawner.Configure(registry, difficulty, AllFourEntries());

            spawner.SpawnAll();

            var names = new HashSet<string>();
            foreach (GameObject agent in spawner.Spawned)
            {
                names.Add(agent.name);
            }
            CollectionAssert.AreEquivalent(
                new[] { "Hunter-1", "Hunter-2", "Patrol-1", "Patrol-2", "Patrol-3", "TG-01", "TG-02", "Commander" }, names);
        }

        [Test]
        public void SpawnAll_PlacesEachAgentAtItsMarker_FacingAsTheMarkerDoes()
        {
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", new Vector3(-24f, 1f, 54f), yaw: 90f);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") });

            spawner.SpawnAll();

            Transform agent = spawner.Spawned[0].transform;
            Assert.AreEqual(new Vector3(-24f, 1f, 54f), agent.position);
            Assert.AreEqual(90f, agent.eulerAngles.y, 0.01f);
            Assert.AreEqual(0f, agent.eulerAngles.x, 0.01f, "stays upright");
        }

        [Test]
        public void SpawnAll_IgnoresMarkersThatAreNotAgentSpawns()
        {
            AddMarker(MarkerType.Waypoint, "Hunter-1", Vector3.zero);
            AddMarker(MarkerType.Checkpoint, "", Vector3.zero);
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", Vector3.zero);
            spawner.Configure(registry, difficulty, AllFourEntries());

            Assert.AreEqual(1, spawner.SpawnAll());
        }

        [Test]
        public void SpawnAll_IgnoresSpawnMarkersOfNoKnownType()
        {
            AddMarker(MarkerType.AgentSpawn, "Janitor-1", Vector3.zero);
            spawner.Configure(registry, difficulty, AllFourEntries());

            Assert.AreEqual(0, spawner.SpawnAll());
        }

        [Test]
        public void SpawnAll_GivesEachTypeItsOwnNumbers()
        {
            AddTheEightSpawnMarkers();
            spawner.Configure(registry, difficulty, AllFourEntries());

            spawner.SpawnAll();

            Health hunter = Find("Hunter-1").GetComponent<Health>();
            Health commander = Find("Commander").GetComponent<Health>();
            Assert.AreEqual(90f, hunter.Max);
            Assert.AreEqual(70f, commander.Max);
            Assert.AreEqual(12f, Find("Hunter-2").GetComponent<AgentShooter>().Config.damage);
            Assert.AreEqual(5f, Find("Commander").GetComponent<AgentShooter>().Config.damage);
            Assert.AreEqual(4.5f, Find("Hunter-1").GetComponent<PathFollower>().Settings.runSpeed);
        }

        [Test]
        public void SpawnAll_AppliesTheDifficultyMultipliers()
        {
            difficulty.healthMultiplier = 2f;
            difficulty.reactionDelayMultiplier = 0.5f;
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") });

            spawner.SpawnAll();

            GameObject hunter = spawner.Spawned[0];
            Assert.AreEqual(180f, hunter.GetComponent<Health>().Max);
            Assert.AreEqual(0.35f, hunter.GetComponent<AgentShooter>().Config.reactionDelay, 0.0001f);
        }

        [Test]
        public void SpawnAll_SkipsAnEntryWithNoPrefab_AndStillSpawnsTheRest()
        {
            AddTheEightSpawnMarkers();
            var entries = AllFourEntries();
            entries[0].prefab = null; // no Hunter prefab yet
            spawner.Configure(registry, difficulty, entries);

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("Hunter entry has no prefab"));
            int count = spawner.SpawnAll();

            Assert.AreEqual(6, count);
        }

        [Test]
        public void RespawnAll_ReplacesEveryAgentWithAFreshOne()
        {
            AddTheEightSpawnMarkers();
            spawner.Configure(registry, difficulty, AllFourEntries());
            spawner.SpawnAll();
            GameObject oldHunter = Find("Hunter-1");

            spawner.RespawnAll();

            Assert.AreEqual(8, spawner.Spawned.Count);
            Assert.IsTrue(oldHunter == null, "the old Hunter-1 is gone");
            Assert.IsNotNull(Find("Hunter-1"));
        }

        [Test]
        public void RespawnAll_BringsADeadAgentBackAtFullHealth()
        {
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") });
            spawner.SpawnAll();
            spawner.Spawned[0].GetComponent<Health>().TakeDamage(new DamageInfo(1000f, Vector3.zero, Vector3.forward, null, DamageType.Bullet));
            Assert.IsFalse(spawner.Spawned[0].GetComponent<Health>().IsAlive);

            spawner.RespawnAll();

            Health fresh = spawner.Spawned[0].GetComponent<Health>();
            Assert.IsTrue(fresh.IsAlive);
            Assert.AreEqual(fresh.Max, fresh.Current);
        }

        [Test]
        public void RestartChannel_RespawnsEveryAgent()
        {
            var restart = Keep(ScriptableObject.CreateInstance<VoidEventChannel>());
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") }, restart);
            spawner.SpawnAll();
            GameObject before = spawner.Spawned[0];

            restart.Raise();

            Assert.IsTrue(before == null);
            Assert.AreEqual(1, spawner.Spawned.Count);
        }

        [Test]
        public void ReconfiguringTheSpawner_StopsListeningToTheOldRestartChannel()
        {
            var oldChannel = Keep(ScriptableObject.CreateInstance<VoidEventChannel>());
            AddMarker(MarkerType.AgentSpawn, "Hunter-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") }, oldChannel);
            spawner.SpawnAll();
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.Hunter, prefab, "Hunter-") }, null);
            GameObject before = spawner.Spawned[0];

            oldChannel.Raise();

            Assert.IsTrue(before != null, "nothing was respawned");
        }

        [Test]
        public void SpawnAll_WithNoRegistry_SpawnsNothing()
        {
            Object.DestroyImmediate(registryObject);
            registryObject = new GameObject("Empty");
            spawner.Configure(null, difficulty, AllFourEntries());

            UnityEngine.TestTools.LogAssert.Expect(LogType.Warning, new System.Text.RegularExpressions.Regex("no AgentRegistry"));
            Assert.AreEqual(0, spawner.SpawnAll());
        }

        [Test]
        public void PlaceholderBrain_IsAddedToAPrefabWithNoBrain_WhenAskedFor()
        {
            AddMarker(MarkerType.AgentSpawn, "Patrol-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.SecurityPatrol, prefab, "Patrol-") }, null, addPlaceholderBrain: true);

            spawner.SpawnAll();

            Assert.IsNotNull(spawner.Spawned[0].GetComponent<WaypointWalkerBrain>());
        }

        [Test]
        public void PlaceholderBrain_IsNotAddedByDefault()
        {
            AddMarker(MarkerType.AgentSpawn, "Patrol-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.SecurityPatrol, prefab, "Patrol-") });

            spawner.SpawnAll();

            Assert.IsNull(spawner.Spawned[0].GetComponent<WaypointWalkerBrain>());
        }

        private sealed class StubBrain : MonoBehaviour, IAgentBrain
        {
            public void Initialize(IAgentContext context) { }
            public AgentIntent Tick(float deltaTime) { return AgentIntent.Idle; }
        }

        [Test]
        public void PlaceholderBrain_IsNotAddedToAPrefabThatAlreadyHasARealBrain()
        {
            prefab.AddComponent<StubBrain>();
            AddMarker(MarkerType.AgentSpawn, "Patrol-1", Vector3.zero);
            spawner.Configure(registry, difficulty, new[] { Entry(AgentKind.SecurityPatrol, prefab, "Patrol-") }, null, addPlaceholderBrain: true);

            spawner.SpawnAll();

            Assert.IsNull(spawner.Spawned[0].GetComponent<WaypointWalkerBrain>());
        }

        private GameObject Find(string name)
        {
            foreach (GameObject agent in spawner.Spawned)
            {
                if (agent != null && agent.name == name)
                {
                    return agent;
                }
            }
            return null;
        }
    }
}
