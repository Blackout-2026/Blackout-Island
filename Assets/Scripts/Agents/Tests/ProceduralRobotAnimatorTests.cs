using System.Text.RegularExpressions;
using Blackout.Shared;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blackout.Agents.Tests
{
    public class ProceduralRobotAnimatorTests
    {
        private sealed class FakeVelocitySource : IAgentVelocitySource
        {
            public Vector3 Velocity { get; set; }
            public float Speed { get; set; }
        }

        private GameObject root;
        private ProceduralRobotAnimator animator;
        private FakeVelocitySource velocitySource;

        private Transform pelvis, head, thighL, thighR, shinL, shinR, upperArmR, foreArmL, foreArmR;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("RobotTest");
            animator = root.AddComponent<ProceduralRobotAnimator>();

            pelvis = CreateChild(root.transform, "Pelvis");
            head = CreateChild(pelvis, "Head");
            // The legs sit one level deeper than their direct parent, to exercise the recursive name search.
            Transform hips = CreateChild(pelvis, "Hips");
            thighL = CreateChild(hips, "Thigh_L");
            thighR = CreateChild(hips, "Thigh_R");
            shinL = CreateChild(thighL, "Shin_L");
            shinR = CreateChild(thighR, "Shin_R");
            CreateChild(shinL, "Foot_L");
            CreateChild(shinR, "Foot_R");
            Transform upperArmL = CreateChild(pelvis, "UpperArm_L");
            upperArmR = CreateChild(pelvis, "UpperArm_R");
            foreArmL = CreateChild(upperArmL, "ForeArm_L");
            foreArmR = CreateChild(upperArmR, "ForeArm_R");

            velocitySource = new FakeVelocitySource();
            animator.Configure(velocitySource);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(root);
        }

        private static Transform CreateChild(Transform parent, string childName)
        {
            var go = new GameObject(childName);
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private void Run(float seconds, float dt = 0.02f)
        {
            int steps = Mathf.RoundToInt(seconds / dt);
            for (int i = 0; i < steps; i++)
            {
                animator.Tick(dt);
            }
        }

        [Test]
        public void Configure_FindsAllNamedJoints_EvenNested()
        {
            velocitySource.Speed = 2f;

            Assert.DoesNotThrow(() => Run(0.5f));
            Assert.Greater(Quaternion.Angle(Quaternion.identity, thighL.localRotation), 0f);
            Assert.Greater(Quaternion.Angle(Quaternion.identity, shinL.localRotation), 0f, "the nested Shin_L must still be found");
        }

        [Test]
        public void Configure_WithAMissingJoint_LogsOneWarning_AndStillWorks()
        {
            Object.DestroyImmediate(head.gameObject);

            LogAssert.Expect(LogType.Warning, new Regex("'Head'"));
            animator.Configure(velocitySource);

            Assert.DoesNotThrow(() => animator.Tick(0.1f));
        }

        [Test]
        public void AtStandstill_NothingSwings()
        {
            velocitySource.Speed = 0f;

            Run(1f);

            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, thighL.localRotation), 0.01f);
            Assert.AreEqual(0f, Quaternion.Angle(Quaternion.identity, shinL.localRotation), 0.01f);
        }

        [Test]
        public void Walking_SwingsTheLegsInOppositeDirections()
        {
            velocitySource.Speed = 1.5f; // full walk weight, no run (agent-controller.md 9.1)
            float stride = RobotGaitMath.StrideLength(1.5f);
            float quarterStrideTime = stride / (2f * 1.5f); // phase = PI/2: the left thigh at its swing peak

            Run(quarterStrideTime);

            // Quaternion.Angle only ever returns a magnitude, so compare a rotated axis instead to see direction.
            float leftTilt = (thighL.localRotation * Vector3.forward).y;
            float rightTilt = (thighR.localRotation * Vector3.forward).y;

            Assert.Greater(Mathf.Abs(leftTilt), 0.1f, "the left thigh should be near its swing peak");
            Assert.Less(leftTilt * rightTilt, 0f, "a quarter stride in, the two thighs swing opposite ways");
        }

        [Test]
        public void Apply_Aiming_RaisesTheRightArmTowardThePod()
        {
            animator.Apply(new AgentIntent { aim = true });

            Run(2f); // long enough for the damped aim blend (rate 7/s, agent-controller.md 9.1) to settle

            float angle = Quaternion.Angle(Quaternion.identity, upperArmR.localRotation);
            Assert.Greater(angle, 80f, "close to the 1.5 rad (~86 degree) aim pose");
        }

        [Test]
        public void Apply_NotAiming_AfterAiming_LowersTheArmAgain()
        {
            animator.Apply(new AgentIntent { aim = true });
            Run(2f);

            animator.Apply(new AgentIntent { aim = false });
            Run(2f);

            float angle = Quaternion.Angle(Quaternion.identity, upperArmR.localRotation);
            Assert.Less(angle, 5f);
        }

        [Test]
        public void PlayDeath_CollapsesThePelvisAndLegs()
        {
            animator.PlayDeath();

            animator.Tick(10f); // settle at the final pose (agent-controller.md 9.2: t1 by 0.4s, t2 by 1.1s)

            Assert.Greater(Quaternion.Angle(Quaternion.identity, pelvis.localRotation), 50f, "the pelvis pitches forward");
            Assert.Greater(Quaternion.Angle(Quaternion.identity, thighL.localRotation), 20f, "the thighs buckle");
            Assert.Less(pelvis.localPosition.y, 0f, "the pelvis drops below its rest position (local y 0 in this test rig)");
        }

        [Test]
        public void PlayDeath_EventuallyDisablesTheVisual()
        {
            animator.PlayDeath();

            animator.Tick(AgentVisualAnimatorBase.FadeStartDelay + AgentVisualAnimatorBase.FadeDuration);

            Assert.IsFalse(root.activeSelf);
        }
    }
}
