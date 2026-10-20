// Owner: Systems Engineer (Hunter AI).
using System.Collections.Generic;
using System.Text;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.AI.Hunter.Tests
{
    /// <summary>
    /// The Hunter's hearing against the real level: Environment.unity (bunker walls, terrain, dock) and
    /// Interactables.unity (the doors, closed), opened additively and closed again without saving. Positions come from
    /// the zone and door tables in docs/level-layout.md and are feet positions, as noises are published. Each case
    /// checks the wall count; a wrong count lists every collider the ray crossed. The measured loudness of a sprint
    /// step and an SMG shot is logged for docs/viva/systems-engineer/systems-engineer-h2-sound-propagation.md.
    /// </summary>
    public class HearingLevelTests
    {
        private const string EnvironmentPath = "Assets/Scenes/Environment.unity";
        private const string InteractablesPath = "Assets/Scenes/Interactables.unity";
        private const string ConfigPath = "Assets/Scripts/AI/Hunter/Config/HearingConfig.asset";
        private const float SprintRadius = 13f;
        private const float SmgRadius = 42f;

        private readonly List<Scene> opened = new List<Scene>();
        private HearingConfig config;
        private GameObject hunter;
        private HearingSystem hearing;

        [OneTimeSetUp]
        public void OpenLevel()
        {
            opened.Add(EditorSceneManager.OpenScene(EnvironmentPath, OpenSceneMode.Additive));
            opened.Add(EditorSceneManager.OpenScene(InteractablesPath, OpenSceneMode.Additive));
            Physics.SyncTransforms();

            config = AssetDatabase.LoadAssetAtPath<HearingConfig>(ConfigPath);
            Assert.IsNotNull(config, ConfigPath);
            hunter = new GameObject("Hunter (hearing test)") { hideFlags = HideFlags.HideAndDontSave };
            hearing = hunter.AddComponent<HearingSystem>();
            hearing.SetConfig(config);
            hearing.SetClock(() => 0f);
        }

        [OneTimeTearDown]
        public void CloseLevel()
        {
            Object.DestroyImmediate(hunter);
            for (int i = opened.Count - 1; i >= 0; i--)
            {
                if (opened[i].IsValid() && opened[i].isLoaded)
                {
                    EditorSceneManager.CloseScene(opened[i], true);
                }
            }

            opened.Clear();
            Physics.SyncTransforms();
        }

        [TestCase("Dock, open air", 0f, 1.3f, -100f, 0f, 1.3f, -120f, 0)]
        [TestCase("O-RampBottom, open passage", 0f, 2.5f, 74f, 0f, 4.07f, 66f, 0)]
        [TestCase("Corridor to GeneratorRoom, solid wall", 0.5f, 2.5f, 101f, -4f, 2.5f, 101f, 1)]
        [TestCase("SecurityArea to Corridor, D-Security closed", 0f, 2.5f, 78f, 0f, 2.5f, 88f, 1)]
        [TestCase("GeneratorRoom to Laboratory", -5f, 2.5f, 112f, -5f, 2.5f, 128f, 2)]
        public void RealLevel_CountsTheWallsAPlayerWouldSee(string name, float nx, float ny, float nz,
            float hx, float hy, float hz, int expectedWalls)
        {
            var noise = new Vector3(nx, ny, nz);
            hunter.transform.position = new Vector3(hx, hy, hz);
            Physics.SyncTransforms();

            // The SMG first: it is in range everywhere here, so its raycast always runs. A sprint step beyond 13 m
            // is skipped before the raycast, which would leave the wall count of the previous noise.
            float smg = Measure(noise, SmgRadius);
            int walls = hearing.LastWallCount;
            float sprint = Measure(noise, SprintRadius);

            float distance = Vector3.Distance(noise, hunter.transform.position);
            Debug.Log($"[Blackout] Hearing on the real level: {name}: {distance:0.0} m, {walls} walls, " +
                      $"sprint {sprint:0.000}, SMG {smg:0.000}");
            Assert.AreEqual(expectedWalls, walls, $"{name}: the ray crossed {DescribeHits(noise)}");
        }

        /// <summary>The 1 counted through D-Security is the door: with its colliders off, the doorway is open.</summary>
        [Test]
        public void DSecurity_ClosedMuffles_OpenDoesNot()
        {
            var noise = new Vector3(0f, 2.5f, 78f);
            hunter.transform.position = new Vector3(0f, 2.5f, 88f);
            Collider[] door = Physics.OverlapBox(new Vector3(0f, 3.65f, 82.2f), new Vector3(0.8f, 1.1f, 0.3f),
                Quaternion.identity, 1 << PhysicsLayers.Interactable, QueryTriggerInteraction.Ignore);
            Assert.IsNotEmpty(door, "D-Security's door is in Interactables.unity");

            Physics.SyncTransforms();
            float closed = Measure(noise, SmgRadius);
            int closedWalls = hearing.LastWallCount;
            SetEnabled(door, false);
            try
            {
                float open = Measure(noise, SmgRadius);
                Debug.Log($"[Blackout] Hearing on the real level: D-Security closed {closed:0.000} " +
                          $"({closedWalls} walls), open {open:0.000} ({hearing.LastWallCount} walls)");
                Assert.AreEqual(1, closedWalls);
                Assert.AreEqual(0, hearing.LastWallCount);
                Assert.AreEqual(closed / 0.35f, open, 1e-3f, "the closed door keeps 35 %");
            }
            finally
            {
                SetEnabled(door, true);
            }
        }

        private static void SetEnabled(Collider[] colliders, bool enabled)
        {
            foreach (Collider collider in colliders)
            {
                collider.enabled = enabled;
            }

            Physics.SyncTransforms();
        }

        private float Measure(Vector3 position, float radius)
        {
            hearing.Forget();
            hearing.Hear(new NoiseEvent(position, radius, NoiseType.Gunshot, null));
            return hearing.PerceivedLoudness;
        }

        /// <summary>Every collider on the ray, with its layer, so a wrong count shows what was crossed.</summary>
        private string DescribeHits(Vector3 noise)
        {
            Vector3 from = noise + Vector3.up * config.sourceHeight;
            Vector3 to = hunter.transform.position + Vector3.up * config.listenerHeight;
            Vector3 delta = to - from;
            RaycastHit[] hits = Physics.RaycastAll(from, delta.normalized, delta.magnitude, ~0,
                QueryTriggerInteraction.Ignore);
            if (hits.Length == 0)
            {
                return "nothing";
            }

            var text = new StringBuilder();
            foreach (RaycastHit hit in hits)
            {
                text.Append($"\n  {hit.collider.name} ({LayerMask.LayerToName(hit.collider.gameObject.layer)}, " +
                            $"{hit.collider.GetType().Name}) at {hit.distance:0.00} m");
            }

            return text.ToString();
        }
    }
}
