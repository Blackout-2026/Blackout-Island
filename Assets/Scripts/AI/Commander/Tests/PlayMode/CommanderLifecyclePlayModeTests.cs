// Owner: Agent Controller (Commander AI).
using System.Collections;
using System.Collections.Generic;
using Blackout.Agents;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.AI.Commander.Tests
{
    public sealed class CommanderLifecyclePlayModeTests
    {
        private sealed class Context : IAgentContext
        {
            public Transform Self { get; set; }
            public Health Health { get; set; }
            public Blackboard Blackboard { get; set; }
            public Transform Player => null;
            public IReadOnlyList<Marker> GetMarkers(MarkerType type, string owner = null) => System.Array.Empty<Marker>();
        }

        private sealed class Receiver : IDirectiveReceiver
        {
            public int Orders;
            public void OnDirective(Directive directive) => Orders++;
        }

        private readonly List<Object> created = new List<Object>();

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            foreach (Object obj in created)
                if (obj != null) Object.Destroy(obj);
            created.Clear();
            yield return null;
        }

        [UnityTest]
        public IEnumerator EnableAfterMissedCure_UnityCallbacksTriggerImmediateReplan()
        {
            var board = new Blackboard();
            CommanderBrain brain = MakeBrain(board, out Receiver receiver);
            brain.enabled = false;

            board.ReportCureTaken();
            Assert.That(receiver.Orders, Is.Zero);
            brain.enabled = true; // actual Unity callback, no SendMessage

            Assert.That(receiver.Orders, Is.EqualTo(1));
            Assert.That(board.HasAlertBuff, Is.True);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DestroyedBrain_UnityCallbacksRemoveCureSubscription()
        {
            var board = new Blackboard();
            CommanderBrain brain = MakeBrain(board, out Receiver receiver);

            Object.Destroy(brain);
            yield return null;
            Assert.DoesNotThrow(() => board.ReportCureTaken());

            Assert.That(receiver.Orders, Is.Zero);
            Assert.That(board.HasAlertBuff, Is.False);
        }

        [UnityTest]
        public IEnumerator DestroyedHolder_UnityCallbacksRemovePlayerChannelSubscription()
        {
            var obj = Track(new GameObject("G2-PlayMode-Holder"));
            var holder = obj.AddComponent<BlackboardHolder>();
            var died = Track(ScriptableObject.CreateInstance<VoidEventChannel>());
            holder.BindPlayerLifecycle(died, null);
            Blackboard board = holder.Instance;

            Object.Destroy(obj);
            yield return null;
            died.Raise();

            Assert.That(board.playerNeutralized, Is.False);
        }

        private CommanderBrain MakeBrain(Blackboard board, out Receiver receiver)
        {
            var obj = Track(new GameObject("G2-PlayMode-Commander"));
            var health = obj.AddComponent<Health>();
            var brain = obj.AddComponent<CommanderBrain>();
            var config = Track(ScriptableObject.CreateInstance<CommanderConfig>());
            brain.SetConfig(config);
            var unit = Track(new GameObject("G2-PlayMode-Unit"));
            receiver = new Receiver();
            board.RegisterAgent(unit.name, unit.transform, unit.AddComponent<Health>(), receiver);
            brain.Initialize(new Context { Self = obj.transform, Health = health, Blackboard = board });
            return brain;
        }

        private T Track<T>(T obj) where T : Object
        {
            created.Add(obj);
            return obj;
        }
    }
}
