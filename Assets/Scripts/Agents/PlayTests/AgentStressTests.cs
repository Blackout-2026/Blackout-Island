using System.Collections;
using NUnit.Framework;
using UnityEngine.TestTools;

namespace Blackout.Agents.PlayTests
{
    /// <summary>
    /// The A5 scripted run. The short one is an ordinary test; the five-minute one is Explicit, so Run All skips it:
    /// select it in the Test Runner (PlayMode tab) or run it in batch with -testFilter "Stress_FiveMinutes".
    /// </summary>
    public class AgentStressTests
    {
        private const float SmokeSeconds = 15f;
        private const float FullSeconds = 300f;

        [UnityTest, Timeout(120000)]
        public IEnumerator Smoke_AllEightAgentsSpawnAndRunWithoutErrors()
        {
            var run = new AgentStressRun();

            yield return run.Run(SmokeSeconds);

            AssertHealthy(run);
        }

        [UnityTest, Timeout(900000), Explicit("Five real minutes with all eight agents; writes a report to the temp folder.")]
        public IEnumerator Stress_FiveMinutes()
        {
            var run = new AgentStressRun();

            yield return run.Run(FullSeconds);

            AssertHealthy(run);
        }

        private static void AssertHealthy(AgentStressRun run)
        {
            Assert.AreEqual(AgentStressRun.ExpectedAgents, run.agentsSpawned, "Hunter x2, Security Patrol x3, Tactical Guard x2, Commander x1");
            Assert.AreEqual(0, run.nullReferences, "no null references. Report: " + run.reportPath);
            Assert.AreEqual(0, run.exceptions, "no exceptions. Report: " + run.reportPath);
        }
    }
}
