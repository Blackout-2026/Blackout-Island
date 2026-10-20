// Owner: Systems Engineer (Hunter AI).
using System.Text;
using NUnit.Framework;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>The running path read from the nodes' last status (docs/reference/systems-engineer.md section 16).</summary>
    public class BehaviourTreeDebugTests
    {
        private const NodeStatus Running = NodeStatus.Running;
        private const NodeStatus Success = NodeStatus.Success;
        private const NodeStatus Failure = NodeStatus.Failure;

        [Test]
        public void Path_FollowsTheSelectorsWinner_AndTheSequencesRunningChild()
        {
            var root = new Selector("Root",
                new Sequence("Attack", new Condition("SeesPlayer", _ => false), new ScriptedNode("Fire", Running)),
                new Sequence("Investigate",
                    new Condition("Heard", _ => true),
                    new ScriptedNode("MoveToNoise", Success),
                    new ScriptedNode("Ambush", Running)),
                new ScriptedNode("Roam", Running));
            root.Tick(new BtContext());

            Assert.AreEqual("Investigate > Ambush", Path(root));
        }

        [Test]
        public void Path_GoesThroughNestedSelectorsAndDecorators()
        {
            var root = new Selector("Root",
                new Sequence("CautiousApproach",
                    new Condition("HeardGunfire", _ => true),
                    new Selector("GunfireTactic",
                        new Sequence("CloseFire", new Condition("Close", _ => false), new ScriptedNode("TakeCover", Running)),
                        new Cooldown("NotTooOften", 2f, new ScriptedNode("AdvanceByCover", Running)))));
            root.Tick(new BtContext());

            Assert.AreEqual("CautiousApproach > GunfireTactic > NotTooOften > AdvanceByCover", Path(root));
        }

        [Test]
        public void Path_OfABranchThatJustFinished_IsItsName()
        {
            var root = new Selector("Root",
                new Sequence("Search", new ScriptedNode("GoToLastSeen", Success), new ScriptedNode("LookAround", Success)));
            root.Tick(new BtContext());

            Assert.AreEqual("Search", Path(root), "finishing resets the children");
        }

        [Test]
        public void Path_IsEmpty_WhenNothingRan()
        {
            var root = new Selector("Root", new ScriptedNode("Roam", Failure));
            root.Tick(new BtContext());
            var text = new StringBuilder();

            Assert.IsFalse(BehaviourTreeDebug.AppendRunningPath(root, text));
            Assert.AreEqual(0, text.Length);
        }

        [Test]
        public void Path_IgnoresAnInterruptedBranch()
        {
            var attack = new ScriptedNode("Attack", Failure);
            var roam = new ScriptedNode("Roam", Running);
            var root = new Selector("Root", attack, roam);
            root.Tick(new BtContext());
            Assert.AreEqual("Roam", Path(root));

            attack.Result = Running;
            root.Tick(new BtContext());
            Assert.AreEqual("Attack", Path(root), "Roam was reset when Attack took over");
        }

        private static string Path(Node root)
        {
            var text = new StringBuilder();
            BehaviourTreeDebug.AppendRunningPath(root, text);
            return text.ToString();
        }
    }
}
