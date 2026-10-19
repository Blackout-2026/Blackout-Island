// Owner: Systems Engineer.
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;
using Debug = UnityEngine.Debug;
using Random = System.Random;

namespace Blackout.Diagnostics
{
    /// <summary>
    /// The scripted run behind Blackout/Debug/Stress Test (docs/reference/systems-engineer.md section 12). Added in
    /// Play mode once Bootstrap's mission is running, it jumps to each phase start (Beach to Escape), puts a target
    /// dummy in front of the player, and fires the real weapons at it: 30 SMG shots, 5 rifle shots and 5 knife
    /// swings. Every pellet is checked against the dummy's colliders alone, so a pellet that should have hit it but
    /// did not is a missed hit. Errors, exceptions and null references logged during the run are counted. It also
    /// times raycasts with PlayerShots against all layers, measures what the firing calls allocate, and counts the
    /// scene's colliders. It ends with the jump to Complete, then logs a report and writes it to
    /// <see cref="ReportPath"/>. The player is invulnerable for the run, so the agents cannot end it.
    /// </summary>
    public class StressTest : MonoBehaviour
    {
        private const int SmgSlot = 1;
        private const int RifleSlot = 2;
        private const int KnifeSlot = 3;
        private const int SmgShots = 30;
        private const int RifleShots = 5;
        private const int KnifeSwings = 5;
        // The first shots warm up pools and JIT, so their allocations are not counted.
        private const int WarmUpShots = 5;
        private const int BenchmarkRays = 2000;
        private const float BenchmarkRange = 90f; // the SMG's range
        private const float GunDistance = 6f;
        private const float KnifeDistance = 1.2f;
        private const float MinGunDistance = 1.5f;
        private const float WallClearance = 0.8f;
        // A pellet whose weapon hit is this much farther than the dummy went through it.
        private const float HitTolerance = 0.01f;
        private const float SwingWait = 0.25f; // the knife lands 0.11 s after the swing
        private const float StartTimeout = 60f;
        private const float InvulnerableSeconds = 3600f;
        private const int SettleFrames = 3;

        private static readonly MissionPhase[] Phases =
        {
            MissionPhase.Beach, MissionPhase.Forest, MissionPhase.BunkerEntrance, MissionPhase.Corridor,
            MissionPhase.GeneratorRoom, MissionPhase.Lab, MissionPhase.Escape,
        };

        private MissionDirector director;
        private WeaponController weapons;
        private PlayerMotor motor;
        private PlayerLook look;
        private Health playerHealth;
        private Transform aim;
        private GameObject dummy;
        private Transform dummyHead;
        private Health dummyHealth;
        private Collider[] dummyColliders;
        private Vector3[] benchmarkDirections;
        private bool running;
        private int hitsConfirmed;
        private int shotsFired;

        public bool IsDone { get; private set; }
        public StressTestResult Result { get; } = new StressTestResult();
        public string ReportPath { get; private set; }

        private void OnEnable()
        {
            Application.logMessageReceived += OnLog;
        }

        private void OnDisable()
        {
            Application.logMessageReceived -= OnLog;
            if (weapons != null)
            {
                weapons.PelletTraced -= OnPelletTraced;
                weapons.HitConfirmed -= OnHitConfirmed;
            }
        }

        private void OnDestroy()
        {
            if (dummy != null)
            {
                Destroy(dummy);
            }
        }

        private IEnumerator Start()
        {
            float waited = 0f;
            director = FindFirstObjectByType<MissionDirector>();
            while ((director == null || !director.IsRunning) && waited < StartTimeout)
            {
                yield return null;
                waited += Time.unscaledDeltaTime;
                director = director != null ? director : FindFirstObjectByType<MissionDirector>();
            }

            if (!FindPlayer())
            {
                Finish();
                yield break;
            }

            TakeInventory();
            CreateDummy();
            CreateBenchmarkDirections();
            weapons.PelletTraced += OnPelletTraced;
            weapons.HitConfirmed += OnHitConfirmed;
            running = true;

            foreach (MissionPhase phase in Phases)
            {
                yield return RunPhase(phase);
            }

            director.DebugJump(MissionPhase.Complete);
            yield return null;
            Result.ReachedComplete = director.Phase == MissionPhase.Complete;
            Finish();
        }

        private bool FindPlayer()
        {
            if (director == null || !director.IsRunning)
            {
                Debug.LogError("[Blackout] Stress Test: no running mission. Open Assets/Scenes/Bootstrap.unity and " +
                               "press Play first.", this);
                return false;
            }

            motor = FindFirstObjectByType<PlayerMotor>();
            weapons = motor != null ? motor.GetComponentInChildren<WeaponController>() : null;
            if (weapons == null || !weapons.isActiveAndEnabled)
            {
                Debug.LogError("[Blackout] Stress Test: no active player with a WeaponController.", this);
                return false;
            }

            look = motor.GetComponent<PlayerLook>();
            var health = motor.GetComponent<PlayerHealth>();
            playerHealth = health != null ? health.Health : null;
            Camera eye = motor.GetComponentInChildren<Camera>();
            aim = eye != null ? eye.transform : weapons.transform;
            return true;
        }

        private IEnumerator RunPhase(MissionPhase phase)
        {
            if (playerHealth != null)
            {
                playerHealth.ResetHealth(InvulnerableSeconds);
            }

            director.DebugJump(phase);
            for (int i = 0; i < SettleFrames; i++)
            {
                yield return null;
            }

            Result.PhasesVisited++;
            Benchmark();

            if (PlaceDummy(GunDistance, MinGunDistance))
            {
                yield return Fire(SmgSlot, SmgShots);
                yield return Fire(RifleSlot, RifleShots);
            }
            else
            {
                Result.PhasesWithoutRoom++;
            }

            if (PlaceDummy(KnifeDistance, KnifeDistance))
            {
                yield return Swing();
            }
        }

        private IEnumerator Fire(int slot, int shots)
        {
            weapons.DebugEquip(slot);
            dummyHealth.ResetHealth(0f);
            yield return null;
            for (int i = 0; i < shots; i++)
            {
                FireOnce();
                yield return null;
            }
        }

        private IEnumerator Swing()
        {
            weapons.DebugEquip(KnifeSlot);
            yield return null;
            for (int i = 0; i < KnifeSwings; i++)
            {
                dummyHealth.ResetHealth(0f);
                int before = hitsConfirmed;
                FireOnce();
                yield return new WaitForSecondsRealtime(SwingWait);
                Result.KnifeSwings++;
                if (hitsConfirmed > before)
                {
                    Result.KnifeLanded++;
                }
                else
                {
                    Result.AddDetail($"{director.Phase}: a knife swing at the dummy {KnifeDistance} m ahead missed");
                }
            }
        }

        /// <summary>One shot through the real weapon, level aim, with what the call allocates measured.</summary>
        private void FireOnce()
        {
            if (look != null)
            {
                look.ResetPitch();
            }

            long before = GC.GetAllocatedBytesForCurrentThread();
            weapons.DebugFire();
            long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
            if (++shotsFired > WarmUpShots)
            {
                Result.FireAllocatedBytes += allocated;
                Result.MeasuredShots++;
            }
        }

        private void OnPelletTraced(PelletTrace trace)
        {
            Result.Pellets++;
            Collider hit = trace.Hit;
            if (hit != null)
            {
                Result.PelletsStoppedPerLayer[hit.gameObject.layer]++;
            }

            float dummyDistance = float.MaxValue;
            foreach (Collider part in dummyColliders)
            {
                if (part.Raycast(trace.Ray, out RaycastHit partHit, trace.Range) && partHit.distance < dummyDistance)
                {
                    dummyDistance = partHit.distance;
                }
            }

            if (hit != null && hit.transform.IsChildOf(motor.transform))
            {
                Result.SelfBlocked++;
            }
            else if (dummyDistance == float.MaxValue)
            {
                Result.PelletsWide++;
            }
            else if (hit != null && hit.transform.IsChildOf(dummy.transform))
            {
                Result.DummyHits++;
            }
            else if (hit != null && trace.Distance < dummyDistance - HitTolerance)
            {
                Result.BlockedBeforeDummy++; // a wall or an agent in front of the dummy
            }
            else
            {
                Result.MissedHits++;
                Result.AddDetail($"{director.Phase}: missed hit, the pellet went through the dummy at " +
                                 $"{dummyDistance:0.00} m but hit {(hit != null ? hit.name : "nothing")}");
            }
        }

        private void OnHitConfirmed(bool killed)
        {
            hitsConfirmed++;
        }

        private void OnLog(string message, string stackTrace, LogType type)
        {
            if (!running || type == LogType.Log || type == LogType.Warning)
            {
                return;
            }

            if (type == LogType.Exception)
            {
                Result.Exceptions++;
                if (message.Contains("NullReferenceException") || message.Contains("MissingReferenceException")
                    || message.Contains("UnassignedReferenceException"))
                {
                    Result.NullReferences++;
                }
            }
            else
            {
                Result.Errors++;
            }

            Result.AddDetail($"{type}: {message}");
        }

        /// <summary>Counts the loaded scenes' colliders and bodies, and the matrix pairs that are off.</summary>
        private void TakeInventory()
        {
            foreach (Collider collider in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                Result.Colliders++;
                Result.CollidersPerLayer[collider.gameObject.layer]++;
                Result.Triggers += collider.isTrigger ? 1 : 0;
                Result.MeshColliders += collider is MeshCollider ? 1 : 0;
                if (collider.gameObject.layer == PhysicsLayers.Default)
                {
                    // Level geometry belongs on Level (Blackout/Systems/Assign Physics Layers); name what is left.
                    Result.AddDetail($"Collider on Default: {collider.name} in {collider.gameObject.scene.name}");
                }
            }

            foreach (Rigidbody body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (body.isKinematic)
                {
                    Result.KinematicBodies++;
                }
                else
                {
                    Result.DynamicBodies++;
                }
            }

            for (int a = 0; a < 32; a++)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(a)))
                {
                    continue;
                }

                Result.NamedLayers++;
                for (int b = a; b < 32; b++)
                {
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(b)))
                    {
                        continue;
                    }

                    Result.NamedPairs++;
                    Result.NamedPairsOff += Physics.GetIgnoreLayerCollision(a, b) ? 1 : 0;
                }
            }

            Result.FixedDeltaTime = Time.fixedDeltaTime;
            Result.SolverIterations = Physics.defaultSolverIterations;
        }

        /// <summary>A 1.8 m body capsule on Enemy (the prototype's 0.3 m radius) and a head box on Hit at eye height.</summary>
        private void CreateDummy()
        {
            dummy = new GameObject("StressTest_Dummy");
            dummyHealth = dummy.AddComponent<Health>();

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.layer = PhysicsLayers.Enemy;
            body.transform.SetParent(dummy.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.9f, 0f);
            body.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);

            GameObject head = GameObject.CreatePrimitive(PrimitiveType.Cube);
            head.name = "Head";
            head.layer = PhysicsLayers.Hit;
            head.transform.SetParent(dummy.transform, false);
            head.transform.localScale = Vector3.one * 0.3f;
            dummyHead = head.transform;

            dummyColliders = dummy.GetComponentsInChildren<Collider>();
            dummy.SetActive(false);
        }

        /// <summary>
        /// Stands the dummy straight ahead of the player, short of any wall. Physics.SyncTransforms makes the move
        /// visible to queries at once, because Auto Sync Transforms is off.
        /// </summary>
        private bool PlaceDummy(float wanted, float minimum)
        {
            dummy.SetActive(false);
            Vector3 feet = motor.transform.position;
            Vector3 eye = aim.position;
            Vector3 forward = Vector3.ProjectOnPlane(motor.transform.forward, Vector3.up).normalized;
            float distance = wanted;
            if (Physics.Raycast(eye, forward, out RaycastHit wall, wanted + WallClearance, PhysicsLayers.PlayerShots,
                    QueryTriggerInteraction.Ignore))
            {
                distance = Mathf.Min(wanted, wall.distance - WallClearance);
            }

            if (distance < minimum)
            {
                Result.AddDetail($"{director.Phase}: no room for the dummy {wanted} m ahead (wall at " +
                                 $"{distance + WallClearance:0.0} m)");
                return false;
            }

            dummy.transform.SetPositionAndRotation(feet + forward * distance, Quaternion.LookRotation(-forward));
            dummyHead.localPosition = new Vector3(0f, eye.y - feet.y, 0f);
            dummy.SetActive(true);
            Physics.SyncTransforms();
            return true;
        }

        /// <summary>Fixed, evenly spread directions, so every run times the same rays.</summary>
        private void CreateBenchmarkDirections()
        {
            var random = new Random(7);
            benchmarkDirections = new Vector3[BenchmarkRays];
            for (int i = 0; i < BenchmarkRays; i++)
            {
                float y = (float)(random.NextDouble() * 2.0 - 1.0);
                float angle = (float)(random.NextDouble() * 2.0 * Math.PI);
                float ring = Mathf.Sqrt(1f - y * y);
                benchmarkDirections[i] = new Vector3(ring * Mathf.Cos(angle), y, ring * Mathf.Sin(angle));
            }
        }

        /// <summary>Times the same rays from the eye with PlayerShots and with every layer, in both orders.</summary>
        private void Benchmark()
        {
            dummy.SetActive(false);
            Physics.SyncTransforms();
            Vector3 eye = aim.position;
            Result.MaskedTicks += TimeRays(eye, PhysicsLayers.PlayerShots, ref Result.MaskedRayHits);
            Result.AllLayersTicks += TimeRays(eye, ~0, ref Result.AllLayersRayHits);
            Result.AllLayersTicks += TimeRays(eye, ~0, ref Result.AllLayersRayHits);
            Result.MaskedTicks += TimeRays(eye, PhysicsLayers.PlayerShots, ref Result.MaskedRayHits);
            Result.BenchmarkRaysPerMask += 2 * BenchmarkRays;
        }

        private long TimeRays(Vector3 eye, int mask, ref int hits)
        {
            long start = Stopwatch.GetTimestamp();
            for (int i = 0; i < benchmarkDirections.Length; i++)
            {
                if (Physics.Raycast(eye, benchmarkDirections[i], out _, BenchmarkRange, mask,
                        QueryTriggerInteraction.Ignore))
                {
                    hits++;
                }
            }

            return Stopwatch.GetTimestamp() - start;
        }

        private void Finish()
        {
            running = false;
            if (weapons != null)
            {
                weapons.PelletTraced -= OnPelletTraced;
                weapons.HitConfirmed -= OnHitConfirmed;
            }

            if (playerHealth != null && playerHealth.IsAlive)
            {
                playerHealth.ResetHealth(0f);
            }

            if (director != null)
            {
                Result.Kills = director.Kills;
            }

            if (dummy != null)
            {
                Destroy(dummy);
            }

            string report = Result.ToReport();
            ReportPath = Path.Combine(Application.temporaryCachePath, "StressTest.txt");
            try
            {
                File.WriteAllText(ReportPath, report);
            }
            catch (IOException exception)
            {
                Debug.LogWarning($"[Blackout] Stress Test: could not write {ReportPath}: {exception.Message}", this);
                ReportPath = null;
            }

            Debug.Log(report + (ReportPath != null ? $"Report written to {ReportPath}" : string.Empty), this);
            IsDone = true;
        }
    }
}
