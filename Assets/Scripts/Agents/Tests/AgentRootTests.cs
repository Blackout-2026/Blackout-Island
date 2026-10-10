using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class AgentRootTests
    {
        private sealed class FakeMover : IAgentMover
        {
            public int setDestinationCalls;
            public int stopCalls;
            public Vector3 lastDestination;
            public MoveSpeed lastSpeed;

            public void SetDestination(Vector3 destination, MoveSpeed speed)
            {
                setDestinationCalls++;
                lastDestination = destination;
                lastSpeed = speed;
            }

            public void Stop() { stopCalls++; }
            public bool HasArrived => false;
            public float RemainingDistance => 0f;
            public Vector3[] CurrentPath => null;
        }

        private sealed class FakeFacingMover : IAgentMover, IAgentFacing
        {
            public int facingCalls;
            public bool hasLook;
            public Vector3 look;
            public bool aiming;

            public void SetDestination(Vector3 destination, MoveSpeed speed) { }
            public void Stop() { }
            public bool HasArrived => false;
            public float RemainingDistance => 0f;
            public Vector3[] CurrentPath => null;

            public void SetFacing(bool hasLookTarget, Vector3 lookTarget, bool isAiming)
            {
                facingCalls++;
                hasLook = hasLookTarget;
                look = lookTarget;
                aiming = isAiming;
            }
        }

        private sealed class FakeAnimator : IAgentAnimator
        {
            public int applyCalls;
            public int hitCalls;
            public int deathCalls;
            public AgentIntent lastIntent;

            public void Apply(in AgentIntent intent) { applyCalls++; lastIntent = intent; }
            public void PlayHit(in DamageInfo info) { hitCalls++; }
            public void PlayDeath() { deathCalls++; }
        }

        private sealed class FakeShooter : IAgentShooter
        {
            public int applyCalls;
            public int ceaseFireCalls;
            public AgentIntent lastIntent;

            public void Apply(in AgentIntent intent) { applyCalls++; lastIntent = intent; }
            public void CeaseFire() { ceaseFireCalls++; }
        }

        private sealed class FakeBrain : IAgentBrain
        {
            public AgentIntent next;
            public float lastDeltaTime;
            public int tickCalls;
            public IAgentContext context;

            public void Initialize(IAgentContext newContext) { context = newContext; }

            public AgentIntent Tick(float deltaTime)
            {
                tickCalls++;
                lastDeltaTime = deltaTime;
                return next;
            }
        }

        private GameObject gameObject;
        private AgentRoot root;
        private Health health;
        private FakeMover mover;
        private FakeAnimator animator;
        private FakeShooter shooter;
        private FakeBrain brain;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Patrol-1");
            root = gameObject.AddComponent<AgentRoot>(); // RequireComponent adds Health
            health = gameObject.GetComponent<Health>();
            health.ResetHealth(0f); // Awake does not run in EditMode
            mover = new FakeMover();
            animator = new FakeAnimator();
            shooter = new FakeShooter();
            brain = new FakeBrain();
            root.Configure(brain, mover, animator, shooter);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
        }

        private static DamageInfo Hit(float amount)
        {
            return new DamageInfo(amount, Vector3.zero, Vector3.forward, null, DamageType.Bullet);
        }

        [Test]
        public void ApplyIntent_Walk_SendsTheDestinationAndSpeedToTheMover()
        {
            var intent = new AgentIntent { moveTarget = new Vector3(3f, 0f, 4f), moveSpeed = MoveSpeed.Run };

            root.ApplyIntent(intent);

            Assert.AreEqual(1, mover.setDestinationCalls);
            Assert.AreEqual(new Vector3(3f, 0f, 4f), mover.lastDestination);
            Assert.AreEqual(MoveSpeed.Run, mover.lastSpeed);
            Assert.AreEqual(0, mover.stopCalls);
        }

        [Test]
        public void ApplyIntent_Stop_StopsTheMoverInsteadOfSettingADestination()
        {
            root.ApplyIntent(AgentIntent.Idle);

            Assert.AreEqual(1, mover.stopCalls);
            Assert.AreEqual(0, mover.setDestinationCalls);
        }

        [Test]
        public void ApplyIntent_PassesTheSameIntentToTheAnimatorAndTheShooter()
        {
            var intent = new AgentIntent { aim = true, fire = true, hasLookTarget = true, lookTarget = Vector3.one };

            root.ApplyIntent(intent);

            Assert.AreEqual(1, animator.applyCalls);
            Assert.AreEqual(1, shooter.applyCalls);
            Assert.IsTrue(animator.lastIntent.aim);
            Assert.IsTrue(shooter.lastIntent.fire);
            Assert.AreEqual(Vector3.one, shooter.lastIntent.lookTarget);
        }

        [Test]
        public void ApplyIntent_RemembersTheLastIntent()
        {
            var intent = new AgentIntent { moveSpeed = MoveSpeed.Walk, moveTarget = Vector3.up };

            root.ApplyIntent(intent);

            Assert.AreEqual(MoveSpeed.Walk, root.LastIntent.moveSpeed);
            Assert.AreEqual(Vector3.up, root.LastIntent.moveTarget);
        }

        [Test]
        public void ApplyIntent_WithoutExecutors_DoesNotThrow()
        {
            root.Configure(null, null, null, null);

            Assert.DoesNotThrow(() => root.ApplyIntent(new AgentIntent { moveSpeed = MoveSpeed.Walk, fire = true }));
        }

        [Test]
        public void Think_AsksTheBrainWithTheDeltaTimeAndAppliesItsIntent()
        {
            brain.next = new AgentIntent { moveSpeed = MoveSpeed.Walk, moveTarget = new Vector3(1f, 0f, 2f) };

            root.Think(0.1f);

            Assert.AreEqual(1, brain.tickCalls);
            Assert.AreEqual(0.1f, brain.lastDeltaTime);
            Assert.AreEqual(new Vector3(1f, 0f, 2f), mover.lastDestination);
        }

        [Test]
        public void Think_WithoutABrain_StandsStill()
        {
            root.Configure(null, mover, animator, shooter);

            root.Think(0.1f);

            Assert.AreEqual(1, mover.stopCalls);
            Assert.AreEqual(0, mover.setDestinationCalls);
        }

        [Test]
        public void TakingDamage_TellsTheAnimatorToReact()
        {
            health.TakeDamage(Hit(10f));

            Assert.AreEqual(1, animator.hitCalls);
            Assert.AreEqual(0, animator.deathCalls);
        }

        [Test]
        public void Dying_StopsMovingAndFiringAndPlaysTheDeathOnce()
        {
            health.TakeDamage(Hit(1000f));

            Assert.IsFalse(root.IsAlive);
            Assert.AreEqual(1, mover.stopCalls);
            Assert.AreEqual(1, shooter.ceaseFireCalls);
            Assert.AreEqual(1, animator.deathCalls);
        }

        [Test]
        public void ApplyIntent_AfterDeath_DoesNothing()
        {
            health.TakeDamage(Hit(1000f));
            int stopsAfterDeath = mover.stopCalls;

            root.ApplyIntent(new AgentIntent { moveSpeed = MoveSpeed.Run, fire = true });

            Assert.AreEqual(0, mover.setDestinationCalls);
            Assert.AreEqual(stopsAfterDeath, mover.stopCalls);
            Assert.AreEqual(0, shooter.applyCalls);
            Assert.AreEqual(0, animator.applyCalls);
        }

        [Test]
        public void Configure_Twice_DoesNotReactToDeathTwice()
        {
            root.Configure(brain, mover, animator, shooter);

            health.TakeDamage(Hit(1000f));

            Assert.AreEqual(1, animator.deathCalls);
        }

        [Test]
        public void AgentName_FallsBackToTheObjectName()
        {
            Assert.AreEqual("Patrol-1", root.AgentName);
        }

        [Test]
        public void Initialize_RegistersWithTheRegistryAndGivesTheBrainItsContext()
        {
            var registryObject = new GameObject("Registry");
            try
            {
                var registry = registryObject.AddComponent<AgentRegistry>();

                root.Initialize(registry);

                Assert.AreEqual(1, registry.Agents.Count);
                Assert.IsNotNull(brain.context);
                Assert.AreSame(gameObject.transform, brain.context.Self);
                Assert.AreSame(registry.Blackboard, brain.context.Blackboard);
            }
            finally
            {
                Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void Dying_RemovesTheAgentFromTheRegistry()
        {
            var registryObject = new GameObject("Registry");
            try
            {
                var registry = registryObject.AddComponent<AgentRegistry>();
                root.Initialize(registry);

                health.TakeDamage(Hit(1000f));

                Assert.AreEqual(0, registry.Agents.Count);
            }
            finally
            {
                Object.DestroyImmediate(registryObject);
            }
        }

        [Test]
        public void ApplyIntent_PassesTheLookTargetAndAimToAMoverThatCanFace()
        {
            var facingMover = new FakeFacingMover();
            root.Configure(brain, facingMover, animator, shooter);
            var intent = new AgentIntent { hasLookTarget = true, lookTarget = new Vector3(5f, 0f, 6f), aim = true };

            root.ApplyIntent(intent);

            Assert.AreEqual(1, facingMover.facingCalls);
            Assert.IsTrue(facingMover.hasLook);
            Assert.AreEqual(new Vector3(5f, 0f, 6f), facingMover.look);
            Assert.IsTrue(facingMover.aiming);
        }

        [Test]
        public void ApplyIntent_WithAMoverThatCannotFace_StillWorks()
        {
            Assert.DoesNotThrow(() => root.ApplyIntent(new AgentIntent { hasLookTarget = true, lookTarget = Vector3.one }));
        }
    }
}
