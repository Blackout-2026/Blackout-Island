// Owner: Agent Controller. Real additive scenes, saved prefabs and squad receivers (SO1).
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blackout.Agents;
using Blackout.AI.Hunter;
using Blackout.AI.SecurityPatrol;
using Blackout.AI.TacticalGuard;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Blackout.AI.Commander.Tests
{
    public sealed class SquadOrderIntegrationPlayTests
    {
        private readonly List<string> loaded = new List<string>();
        private readonly List<Object> created = new List<Object>();

        private sealed class GuardContext : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; } = new Blackboard();
            public Transform Player { get; set; }
            public Marker[] Covers;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) =>
                type == MarkerType.CoverPoint ? Covers : System.Array.Empty<Marker>();
        }

        private sealed class GuardSight : IGuardSensor
        {
            public bool HasLineOfSight(Vector3 from, Vector3 to) => Mathf.Abs(to.y - 1.2f) < .001f;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = loaded.Count - 1; i >= 0; i--)
                if (SceneManager.GetSceneByName(loaded[i]).isLoaded)
                    yield return SceneManager.UnloadSceneAsync(loaded[i]);
            loaded.Clear();
            foreach (Object obj in created) if (obj != null) Object.Destroy(obj);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator GuardDisableAndReenable_ReleaseAndReacquireCover_ThroughRealCallbacks()
        {
            var self = new GameObject("SO1 guard"); created.Add(self);
            var player = new GameObject("SO1 target"); created.Add(player);
            player.transform.position = Vector3.forward * 12f;
            var cover = new GameObject("SO1 cover").AddComponent<Marker>(); created.Add(cover.gameObject);
            cover.transform.position = Vector3.right * 3f;
            var config = ScriptableObject.CreateInstance<TacticalGuardConfig>(); created.Add(config);
            var claims = new CoverClaims();
            var health = self.AddComponent<Health>();
            var brain = self.AddComponent<TacticalGuardBrain>();
            brain.SetConfig(config); brain.SetSensor(new GuardSight()); brain.SetClaims(claims);
            brain.Initialize(new GuardContext { Self = self.transform, Health = health, Player = player.transform, Covers = new[] { cover } });
            health.TakeDamage(new DamageInfo(70f, Vector3.zero, Vector3.forward, null, DamageType.Bullet));
            brain.Tick(.1f);
            Assert.That(brain.HeldCover, Is.SameAs(cover));
            brain.enabled = false;
            Assert.That(claims.Count, Is.Zero);
            yield return null;
            brain.enabled = true;
            brain.Tick(.1f);
            Assert.That(brain.HeldCover, Is.SameAs(cover));
            Assert.That(claims.Count, Is.EqualTo(1));
            brain.OnDirective(new Directive(DirectiveType.Regroup, Vector3.forward * 25f, 10f));
            Assert.That(claims.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator SavedSquad_ReceivesAllGoapOrders_AndRestoresAcrossDeathCheckpointAndRestart()
        {
            foreach (string name in new[] { "Environment", "Agents" })
            {
                Assert.That(SceneManager.GetSceneByName(name).isLoaded, Is.False, "test needs an isolated world");
                loaded.Add(name);
                yield return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
            }
            yield return null; yield return null;
            AgentRegistry registry = AgentRegistry.Current;
            Assert.That(registry.Player, Is.Null, "isolate order handling from direct player perception");
            Blackboard board = registry.Blackboard;
            var commander = Object.FindFirstObjectByType<CommanderBrain>();
            var spawner = Object.FindFirstObjectByType<AgentSpawner>();
            BlackboardAgent[] squad = Squad(board);
            Assert.That(squad.Length, Is.EqualTo(23));
            Assert.That(squad.Count(a => a.Receiver is SecurityPatrolBrain), Is.EqualTo(12));
            Assert.That(squad.Count(a => a.Receiver is TacticalGuardBrain), Is.EqualTo(6));
            Assert.That(squad.Count(a => a.Receiver is HunterBrain), Is.EqualTo(5));
            FreezeExecutors(registry);

            float now = Time.time;
            commander.SetClock(() => now);
            board.lastKnownPlayerPos = new Vector3(0f, 2.5f, 100f);
            board.lastKnownPlayerTime = now;
            board.squadAssembled = false;
            board.ReportCureTaken(); // Real notification -> GOAP -> BlackboardAgent -> the three real brains.
            Assert.That(commander.State, Is.EqualTo(CommanderConfig.OrderRegroup)); // Nearby units may complete it immediately.
            AssertOrders(squad, DirectiveType.Regroup);

            // Exercise the new receivers through the real shared mover on the saved NavMesh.
            var walkers = new[] { squad.Single(a => a.Name == "Patrol-3"), squad.Single(a => a.Name == "TG-05") };
            Vector3[] starts = walkers.Select(a => a.Transform.position).ToArray();
            float sightingTime = board.lastKnownPlayerTime;
            board.lastKnownPlayerTime = float.NegativeInfinity; // Let HoldPost win over a fresh-knowledge Reposition.
            foreach (BlackboardAgent walker in walkers)
            {
                Assert.That(NavMesh.SamplePosition(walker.Transform.position + Vector3.forward * 3f,
                    out NavMeshHit hit, 1f, NavMesh.AllAreas), Is.True, walker.Name);
                walker.Issue(new Directive(DirectiveType.Regroup, hit.position, 10f));
                walker.Transform.GetComponent<PathFollower>().enabled = true;
            }
            Vector3[] targets = walkers.Select(a => a.LastDirective.position).ToArray();
            for (int frame = 0; frame < 20; frame++)
            {
                foreach (BlackboardAgent walker in walkers) walker.Transform.GetComponent<AgentRoot>().Think(.1f);
                yield return new WaitForSeconds(.1f);
            }
            for (int i = 0; i < walkers.Length; i++)
                Assert.That(Vector3.Distance(walkers[i].Transform.position, targets[i]),
                    Is.LessThan(Vector3.Distance(starts[i], targets[i]) - .2f), walkers[i].Name);
            FreezeExecutors(registry);
            board.lastKnownPlayerTime = sightingTime;

            board.squadAssembled = true;
            now += .1f; commander.Tick(.1f);
            Assert.That(commander.CurrentAction, Is.EqualTo(CommanderConfig.OrderConverge));
            AssertOrders(squad, DirectiveType.Converge);

            board.lastKnownPlayerTime = float.NegativeInfinity;
            now += .1f; commander.Tick(.1f);
            Assert.That(commander.CurrentAction, Is.EqualTo(CommanderConfig.OrderSearchZone));
            AssertOrders(squad, DirectiveType.SearchZone);
            object checkpoint = spawner.CaptureCheckpoint();

            commander.GetComponent<Health>().TakeDamage(new DamageInfo(1000f, commander.transform.position,
                Vector3.forward, null, DamageType.Bullet));
            commander.Tick(.1f);
            Assert.That(commander.State, Is.EqualTo("Dead"));
            AssertOrders(squad, DirectiveType.SearchZone); // Existing orders survive the issuer's death.

            spawner.RestoreCheckpoint(checkpoint);
            yield return null;
            FreezeExecutors(registry);
            AssertOrders(Squad(board), DirectiveType.SearchZone);
            Assert.That(Object.FindObjectsByType<CommanderBrain>(FindObjectsSortMode.None)
                .Any(c => c.GetComponent<Health>().IsAlive), Is.True);

            // A fresh Agents scene is the roster/Blackboard part of the game's scene-reload restart.
            yield return SceneManager.UnloadSceneAsync("Agents");
            yield return SceneManager.LoadSceneAsync("Agents", LoadSceneMode.Additive);
            yield return null; yield return null;
            Blackboard fresh = AgentRegistry.Current.Blackboard;
            Assert.That(Squad(fresh).Length, Is.EqualTo(23));
            Assert.That(Squad(fresh).All(a => a.Receiver != null && !a.HasDirective), Is.True);
            Assert.That(fresh.cureTaken, Is.False);
        }

        private static BlackboardAgent[] Squad(Blackboard board) => board.Agents
            .Where(a => a.IsAlive && a.Transform.GetComponent<CommanderBrain>() == null).ToArray();

        private static void FreezeExecutors(AgentRegistry registry)
        {
            foreach (IAgentInfo info in registry.Agents)
            {
                info.Transform.GetComponent<AgentRoot>().enabled = false;
                info.Transform.GetComponent<PathFollower>().enabled = false;
            }
        }

        private static void AssertOrders(BlackboardAgent[] squad, DirectiveType type)
        {
            foreach (BlackboardAgent member in squad)
            {
                Assert.That(member.Receiver, Is.Not.Null, member.Name);
                Assert.That(member.HasDirective && member.LastDirective.type == type, Is.True, member.Name);
                Directive order = member.LastDirective;
                if (member.Receiver is TacticalGuardBrain guard)
                    Assert.That(guard.Post, Is.EqualTo(order.position), member.Name);
                if (member.Receiver is SecurityPatrolBrain patrol)
                    Assert.That(patrol.HasDirective, Is.True, member.Name);
                var brain = (IAgentBrain)member.Receiver;
                AgentIntent intent = brain.Tick(.1f);
                Assert.That(intent.fire, Is.False, member.Name + " received an order without seeing a player");
                if (intent.moveSpeed != MoveSpeed.Stop && type != DirectiveType.SearchZone &&
                    !(member.Receiver is TacticalGuardBrain)) // Guard utility may choose Reposition or cover instead.
                    Assert.That(intent.moveTarget, Is.EqualTo(order.position), member.Name);
            }
        }
    }
}
