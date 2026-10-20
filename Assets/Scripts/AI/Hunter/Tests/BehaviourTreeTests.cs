// Owner: Systems Engineer (Hunter AI).
using System;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.AI.Hunter.Tests
{
    public class BehaviourTreeTests
    {
        [Test]
        public void Tick_MovesTheClockBeforeTheRootRuns()
        {
            float seen = -1f;
            var tree = new BehaviourTree(new ActionNode("a", c => { seen = c.Time; return NodeStatus.Running; }));

            Assert.AreEqual(NodeStatus.Running, tree.Tick(0.5f));
            Assert.AreEqual(0.5f, seen);
        }

        [Test]
        public void Reset_StartsOverButKeepsTheClockAndTheBlackboard()
        {
            var key = new BlackboardKey<int>("k");
            var a = new ScriptedNode("a", NodeStatus.Success);
            var tree = new BehaviourTree(new Sequence("s", a, new ScriptedNode("b", NodeStatus.Running)));
            tree.Blackboard.Set(key, 7);
            tree.Tick(1f);
            tree.Reset();
            tree.Tick(1f);

            Assert.AreEqual(2, a.Ticks, "the sequence started over");
            Assert.AreEqual(2f, tree.Context.Time);
            Assert.AreEqual(7, tree.Blackboard.Get(key));
        }

        [Test]
        public void NullArguments_Throw()
        {
            Assert.Throws<ArgumentNullException>(() => new BehaviourTree(null));
            Assert.Throws<ArgumentNullException>(() => new BehaviourTree(new ScriptedNode("a", NodeStatus.Success), null));
        }

        /// <summary>
        /// A tree shaped like the prototype's Hunter (engage, react to noise once per cooldown, prowl), with the inputs
        /// flipping so every branch runs, interrupts and resets. After warming up, 1000 ticks allocate nothing.
        /// </summary>
        [Test]
        public void Tick_AllocatesNothing()
        {
            var seesPlayer = new BlackboardKey<bool>("SeesPlayer");
            var noisePosition = new BlackboardKey<Vector3>("NoisePosition");
            var sneakTime = new BlackboardKey<float>("SneakTime");
            var blackboard = new LocalBlackboard();
            int fired = 0, sneaked = 0, prowled = 0;

            var root = new Selector("Root",
                new Sequence("Engage",
                    new Condition("SeesPlayer", c => c.Blackboard.Get(seesPlayer)),
                    new ActionNode("Fire", c => { fired++; return NodeStatus.Running; })),
                new Sequence("ReactToNoise",
                    new Condition("HeardNoise", c => c.Blackboard.Has(noisePosition)),
                    new Cooldown("OncePer2s", 2f,
                        new ActionNode("Sneak", c =>
                        {
                            sneaked++;
                            float t = c.Blackboard.Get(sneakTime) + c.DeltaTime;
                            c.Blackboard.Set(sneakTime, t);
                            return t >= 1f ? NodeStatus.Success : NodeStatus.Running;
                        }, () => blackboard.Remove(sneakTime)))),
                new Sequence("Prowl",
                    new Inverter("NotSeen", new Condition("SeesPlayer", c => c.Blackboard.Get(seesPlayer))),
                    new ActionNode("Walk", c => { prowled++; return NodeStatus.Running; })));
            var tree = new BehaviourTree(root, new BtContext(blackboard));

            void Run(int ticks)
            {
                for (int i = 0; i < ticks; i++)
                {
                    blackboard.Set(seesPlayer, i % 50 >= 40);
                    if (i % 30 == 0)
                    {
                        blackboard.Set(noisePosition, new Vector3(i, 0f, i));
                    }
                    else if (i % 30 == 20)
                    {
                        blackboard.Remove(noisePosition);
                    }

                    tree.Tick(0.1f);
                }
            }

            Run(300);
            long before = GC.GetAllocatedBytesForCurrentThread();
            Run(1000);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Greater(fired, 0);
            Assert.Greater(sneaked, 0);
            Assert.Greater(prowled, 0);
            Assert.AreEqual(0, allocated);
        }
    }
}
