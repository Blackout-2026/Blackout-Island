using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentShooterTests
    {
        private sealed class FakeDamageable : MonoBehaviour, IDamageable
        {
            public bool isAlive = true;
            public int takeDamageCalls;
            public DamageInfo lastDamage;
            public bool IsAlive => isAlive;
            public void TakeDamage(DamageInfo info) { takeDamageCalls++; lastDamage = info; }
        }

        private GameObject shooterObject;
        private AgentShooter shooter;
        private GameObject targetObject;
        private FakeDamageable target;
        private NoiseEventChannel noiseChannel;
        private int noiseCalls;
        private NoiseEvent lastNoise;
        private AgentWeaponConfig config;

        [SetUp]
        public void SetUp()
        {
            Random.InitState(1); // deterministic aim perturbation and burst-pause timing

            shooterObject = new GameObject("Shooter");
            shooter = shooterObject.AddComponent<AgentShooter>();

            targetObject = new GameObject("Target");
            targetObject.AddComponent<BoxCollider>().size = Vector3.one * 2f; // generous, so small random perturbation still lands
            target = targetObject.AddComponent<FakeDamageable>();
            targetObject.transform.position = new Vector3(0f, 1.62f, 10f); // matches AgentShooter's default eye height: a clean horizontal shot
            Physics.SyncTransforms(); // EditMode runs no physics step to pick this up automatically before the first raycast

            noiseChannel = ScriptableObject.CreateInstance<NoiseEventChannel>();
            noiseChannel.OnRaised += e => { noiseCalls++; lastNoise = e; };

            // spread 0: ShotMath.PerturbedDirection returns the exact aim direction with zero error, so these
            // tests (timing, gating, the hit/damage/noise pipeline) are not at the mercy of a random miss.
            // The aim-error formula itself is covered separately by ShotMathTests.
            config = new AgentWeaponConfig { reactionDelay = 0.5f, range = 30f, burstCount = 3, burstInterval = 0.1f, burstPause = 0.9f, spread = 0f, facingDotThreshold = 0.85f };
            shooter.Configure(config, noiseChannel);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(shooterObject);
            Object.DestroyImmediate(targetObject);
            Object.DestroyImmediate(noiseChannel);
        }

        private AgentIntent FireIntent()
        {
            return new AgentIntent { fire = true, aim = true, hasLookTarget = true, lookTarget = targetObject.transform.position };
        }

        private void Run(float seconds, float dt = 0.02f)
        {
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                shooter.Tick(dt);
            }
        }

        [Test]
        public void Apply_FireFalse_NeverFires()
        {
            shooter.Apply(new AgentIntent { fire = false });

            Run(5f);

            Assert.AreEqual(0, shooter.ShotsFired);
        }

        [Test]
        public void BeforeTheReactionDelay_DoesNotFire()
        {
            shooter.Apply(FireIntent());

            Run(0.3f); // comfortably under the 0.5 s reaction delay

            Assert.AreEqual(0, shooter.ShotsFired);
        }

        [Test]
        public void AfterTheReactionDelay_FiresAndHitsAndPublishesNoise()
        {
            shooter.Apply(FireIntent());

            // Past the 0.5 s reaction delay with margin, but short of reactionDelay + burstInterval (0.6 s),
            // where a 2nd shot would legitimately also fire.
            Run(0.55f);

            Assert.AreEqual(1, shooter.ShotsFired);
            Assert.AreEqual(1, target.takeDamageCalls);
            Assert.AreEqual(config.damage, target.lastDamage.amount, 0.001f);
            Assert.AreEqual(1, noiseCalls);
            Assert.AreEqual(config.noiseLoudness, lastNoise.loudness);
            Assert.AreEqual(NoiseType.Gunshot, lastNoise.type);
        }

        [Test]
        public void OutOfRange_DoesNotFire()
        {
            targetObject.transform.position = new Vector3(0f, 0f, 1000f); // well beyond range 30
            shooter.Apply(FireIntent());

            Run(1f);

            Assert.AreEqual(0, shooter.ShotsFired);
        }

        [Test]
        public void NotFacingTheTarget_DoesNotFire_UntilItTurns()
        {
            shooterObject.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // facing away from the target at +Z
            shooter.Apply(FireIntent());

            Run(1f);
            Assert.AreEqual(0, shooter.ShotsFired, "still facing away");

            shooterObject.transform.rotation = Quaternion.identity; // now facing the target
            Run(0.02f); // reaction delay and shot timer were already satisfied while facing away; one tick is enough
            Assert.AreEqual(1, shooter.ShotsFired, "fires as soon as it turns to face");
        }

        [Test]
        public void Burst_FiresExactlyBurstCountShots_ThenPauses()
        {
            shooter.Apply(FireIntent());

            Run(0.55f); // past reaction delay (0.5) with margin, short of reaction + interval (0.6): 1st shot
            Assert.AreEqual(1, shooter.ShotsFired);

            Run(0.11f); // past one interval (0.1) with margin, short of two intervals (0.2): 2nd shot
            Assert.AreEqual(2, shooter.ShotsFired);

            Run(0.11f); // 3rd shot: the burst is complete, next delay is the pause, not another interval
            Assert.AreEqual(3, shooter.ShotsFired);

            Run(0.5f); // well under even the minimum possible pause (0.9 * 0.8 = 0.72 s) since the 3rd shot
            Assert.AreEqual(3, shooter.ShotsFired, "still within the burst pause");

            Run(0.7f); // now well past even the maximum possible pause (0.9 * 1.2 = 1.08 s) since the 3rd shot:
            // a 2nd burst's own shots could also have started by now, so check "at least 4", not "exactly 4".
            Assert.GreaterOrEqual(shooter.ShotsFired, 4, "a new burst started after the pause");
        }

        [Test]
        public void CeaseFire_StopsItAndResetsEngagement()
        {
            shooter.Apply(FireIntent());
            Run(0.55f); // past reaction delay (0.5) with margin, short of reaction + interval (0.6)
            Assert.AreEqual(1, shooter.ShotsFired);

            shooter.CeaseFire();
            Run(5f);

            Assert.AreEqual(1, shooter.ShotsFired, "no more shots after CeaseFire");
        }

        [Test]
        public void Apply_FireFalse_AlsoCeasesFire()
        {
            shooter.Apply(FireIntent());
            Run(0.55f); // past reaction delay (0.5) with margin, short of reaction + interval (0.6)
            Assert.AreEqual(1, shooter.ShotsFired);

            shooter.Apply(new AgentIntent { fire = false });
            Run(5f);

            Assert.AreEqual(1, shooter.ShotsFired);
        }

        [Test]
        public void DeadTarget_IsNotDamaged_ButStillCountsAsAShot()
        {
            target.isAlive = false;
            shooter.Apply(FireIntent());

            Run(0.55f); // past reaction delay (0.5) with margin, short of reaction + interval (0.6)

            Assert.AreEqual(1, shooter.ShotsFired);
            Assert.AreEqual(0, target.takeDamageCalls);
        }

        [Test]
        public void ReApplyingFire_WhileAlreadyEngaging_DoesNotResetTheEngagementTimer()
        {
            shooter.Apply(FireIntent());
            Run(0.3f); // under the reaction delay so far

            shooter.Apply(FireIntent()); // the brain sets a fresh intent every think tick; this must not restart reaction
            Run(0.25f); // total 0.55 s: past the 0.5 s reaction delay with margin, short of reaction + interval (0.6)

            Assert.AreEqual(1, shooter.ShotsFired);
        }
    }
}
