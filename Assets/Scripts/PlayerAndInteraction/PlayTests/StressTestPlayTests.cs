using System.Collections;
using Blackout.Mission;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    /// <summary>
    /// Runs the Stress Test through the whole mission from Bootstrap (docs/reference/systems-engineer.md section 12).
    /// Errors logged by any system are counted by the Stress Test itself, so the log check is turned off and the
    /// test fails on its counts instead: any null reference, exception or missed hit, or a knife swing that missed.
    /// </summary>
    public class StressTestPlayTests
    {
        private const float LoadTimeout = 120f;
        private const float RunTimeout = 180f;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f; // the run ends on the Mission Complete screen, which pauses the game
            yield return null;
        }

        [UnityTest]
        public IEnumerator FullRun_HasNoNullReferencesOrMissedHits()
        {
            LogAssert.ignoreFailingMessages = true;
            SceneManager.LoadScene("Bootstrap");

            float waited = 0f;
            MissionDirector director = null;
            while ((director == null || !director.IsRunning) && waited < LoadTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                director = Object.FindFirstObjectByType<MissionDirector>();
            }

            Assert.IsNotNull(director, "Bootstrap has no MissionDirector");
            Assert.IsTrue(director.IsRunning, $"The mission did not start within {LoadTimeout} s");

            var stress = new GameObject("StressTest").AddComponent<StressTest>();
            waited = 0f;
            while (!stress.IsDone && waited < RunTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Assert.IsTrue(stress.IsDone, $"The Stress Test did not finish within {RunTimeout} s");
            StressTestResult result = stress.Result;
            string report = result.ToReport();
            Debug.Log(report);
            Assert.AreEqual(7, result.PhasesVisited, report);
            Assert.IsTrue(result.ReachedComplete, report);
            Assert.Greater(result.DummyHits, 0, report);
            Assert.AreEqual(0, result.NullReferences, report);
            Assert.AreEqual(0, result.Exceptions, report);
            Assert.AreEqual(0, result.MissedHits, report);
            Assert.AreEqual(0, result.SelfBlocked, report);
            Assert.AreEqual(result.KnifeSwings, result.KnifeLanded, report);
            // Y7: the level's solid colliders are on Level (Blackout/Systems/Assign Physics Layers), none left on Default.
            Assert.Greater(result.CollidersPerLayer[PhysicsLayers.Level], 0, report);
            Assert.AreEqual(0, result.CollidersPerLayer[PhysicsLayers.Default], report);
        }
    }
}
