// Owner: Systems Engineer (Hunter AI).
using System.Collections;
using Blackout.Agents;
using Blackout.AI.Hunter;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    /// <summary>
    /// The Hunters in the real game (docs/reference/systems-engineer.md section 15): Bootstrap loads Environment,
    /// Interactables and Agents, the spawner makes both Hunters from Hunter.prefab, and AgentRoot ticks their
    /// HunterBrain at 10 Hz. The test checks they roam their forest routes, then puts the player 5 m in front of one,
    /// standing still, and waits for that Hunter to attack and its AgentShooter to fire. Other systems' log messages
    /// do not fail the test, but any exception does.
    /// </summary>
    public class HunterPlayTests
    {
        private const float LoadTimeout = 120f;
        private const float RoamSeconds = 6f;
        private const float AttackTimeout = 10f;
        private const float ShotTimeout = 5f;
        private const float PlayerDistance = 5f;

        private int exceptions;
        private string firstException;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Application.logMessageReceived -= CountExceptions;
            Time.timeScale = 1f;
            yield return null;
        }

        [UnityTest]
        public IEnumerator SpawnedHunters_RoamTheirRoutes_AndAttackAPlayerInFront()
        {
            LogAssert.ignoreFailingMessages = true;
            Application.logMessageReceived += CountExceptions;
            SceneManager.LoadScene("Bootstrap");

            float waited = 0f;
            MissionDirector director = null;
            while ((director == null || !director.IsRunning) && waited < LoadTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                director = Object.FindFirstObjectByType<MissionDirector>();
            }

            Assert.IsTrue(director != null && director.IsRunning, $"The mission did not start within {LoadTimeout} s");

            // Both Hunters spawned, with their brain running (a brain that has ticked is no longer "Idle").
            HunterBrain[] hunters = Object.FindObjectsByType<HunterBrain>(FindObjectsSortMode.InstanceID);
            waited = 0f;
            while ((hunters.Length < 2 || !AllThinking(hunters)) && waited < 30f)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                hunters = Object.FindObjectsByType<HunterBrain>(FindObjectsSortMode.InstanceID);
            }

            Assert.AreEqual(2, hunters.Length, "Hunter-1 and Hunter-2, each with a HunterBrain");
            var start = new Vector3[hunters.Length];
            for (int i = 0; i < hunters.Length; i++)
            {
                HunterBrain hunter = hunters[i];
                Assert.AreEqual("Roam", hunter.ActiveBranch, $"{hunter.name} starts by roaming its route");
                Assert.AreEqual("HunterConfig", hunter.Config.name, $"{hunter.name} uses HunterConfig.asset");
                Assert.IsNotNull(hunter.Hearing, $"{hunter.name} has its HearingSystem");
                Assert.AreEqual("HearingConfig", hunter.Hearing.Config.name, $"{hunter.name} uses HearingConfig.asset");
                start[i] = hunter.transform.position;
            }

            yield return WaitSeconds(RoamSeconds);
            var registry = Object.FindFirstObjectByType<AgentRegistry>();
            Assert.IsNotNull(registry, "the Agents scene has an AgentRegistry");
            for (int i = 0; i < hunters.Length; i++)
            {
                float moved = CoverMath.FlatDistance(start[i], hunters[i].transform.position);
                Debug.Log($"[Blackout] Hunter play test: {hunters[i].name} walked {moved:0.0} m in {RoamSeconds} s " +
                          $"({hunters[i].ActiveBranch})");
                Assert.Greater(moved, 2f, $"{hunters[i].name} walks its route (1.6 m/s)");

                // H4: AgentRoot publishes the brain's debug text (IDebugInfo) to the squad Blackboard every 0.5 s.
                string state = PublishedState(registry, hunters[i]);
                Debug.Log($"[Blackout] Hunter play test: {hunters[i].name} published \"{FirstLine(state)}\"");
                StringAssert.StartsWith("Hunter [BT] Roam", state, $"{hunters[i].name}'s published state");
            }

            // The player, standing still, 5 m in front of the first Hunter and facing it.
            HunterBrain target = hunters[0];
            Transform body = target.transform;
            Vector3 spot = Ground(body.position + body.forward * PlayerDistance);
            float yaw = Quaternion.LookRotation(body.position - spot).eulerAngles.y;
            var motor = Object.FindFirstObjectByType<PlayerMotor>();
            Assert.IsNotNull(motor, "the player is in Bootstrap");
            motor.Teleport(spot, yaw);

            float detectedAfter = -1f;
            waited = 0f;
            while (waited < AttackTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                if (target.ActiveBranch == "Attack")
                {
                    detectedAfter = waited;
                    break;
                }
            }

            Assert.Greater(detectedAfter, 0f, $"{target.name} attacks within {AttackTimeout} s " +
                                              $"(last branch {target.ActiveBranch}, awareness {target.Vision.Awareness:0.00})");

            var shooter = target.GetComponentInChildren<AgentShooter>();
            Assert.IsNotNull(shooter, $"{target.name} has an AgentShooter");
            waited = 0f;
            while (shooter.ShotsFired == 0 && waited < ShotTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Debug.Log($"[Blackout] Hunter play test: {target.name} attacked {detectedAfter:0.00} s after the player " +
                      $"appeared 5 m in front, standing still; first shot {waited:0.00} s later " +
                      $"({shooter.ShotsFired} shots)");
            Assert.Greater(shooter.ShotsFired, 0, "the shooter fires after its reaction delay");

            // The published state follows within one publish interval (0.5 s).
            string attacking = null;
            waited = 0f;
            while (waited < 1.5f)
            {
                attacking = PublishedState(registry, target);
                if (attacking != null && attacking.StartsWith("Hunter [BT] Attack"))
                {
                    break;
                }

                yield return null;
                waited += Time.unscaledDeltaTime;
            }

            Debug.Log($"[Blackout] Hunter play test: {target.name} published \"{FirstLine(attacking)}\" while attacking");
            StringAssert.StartsWith("Hunter [BT] Attack", attacking);
            Assert.AreEqual(0, exceptions, firstException);
        }

        private static string PublishedState(AgentRegistry registry, HunterBrain hunter)
        {
            string agentName = hunter.GetComponent<AgentRoot>().AgentName;
            return registry.Blackboard.AgentStates.TryGetValue(agentName, out string state) ? state : null;
        }

        private static string FirstLine(string text)
        {
            if (text == null)
            {
                return "(nothing)";
            }

            int end = text.IndexOf('\n');
            return end < 0 ? text : text.Substring(0, end);
        }

        private static bool AllThinking(HunterBrain[] hunters)
        {
            foreach (HunterBrain hunter in hunters)
            {
                if (hunter.ActiveBranch == "Idle")
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The ground under a point: the first solid level collider below it.</summary>
        private static Vector3 Ground(Vector3 point)
        {
            Vector3 from = new Vector3(point.x, point.y + 50f, point.z);
            if (Physics.Raycast(from, Vector3.down, out RaycastHit hit, 100f,
                    1 << PhysicsLayers.Level | 1 << PhysicsLayers.Default, QueryTriggerInteraction.Ignore))
            {
                return hit.point + Vector3.up * 0.05f;
            }

            return point;
        }

        private static IEnumerator WaitSeconds(float seconds)
        {
            float waited = 0f;
            while (waited < seconds)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
            }
        }

        private void CountExceptions(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception)
            {
                return;
            }

            exceptions++;
            firstException ??= $"{condition}\n{stackTrace}";
        }
    }
}
