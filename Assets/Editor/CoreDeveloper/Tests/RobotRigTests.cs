using System.Collections.Generic;
using System.Text.RegularExpressions;
using Blackout.Agents;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    public class RobotRigTests
    {
        // The serialized name fields of ProceduralRobotAnimator (Agent Controller). If that role renames one, or this
        // table changes, the contract between the model and the animation is broken, and this test says so.
        private static readonly string[] AnimatorNameFields =
        {
            "pelvisName", "headName", "thighLName", "thighRName", "shinLName", "shinRName",
            "footLName", "footRName", "upperArmLName", "upperArmRName", "foreArmLName", "foreArmRName",
        };

        [Test]
        public void AnimatedJoints_AreExactlyTheNamesTheProceduralRobotAnimatorLooksFor()
        {
            var go = new GameObject("AnimatorContract");
            var serialized = new SerializedObject(go.AddComponent<ProceduralRobotAnimator>());

            var animatorNames = new List<string>();
            foreach (string field in AnimatorNameFields)
            {
                SerializedProperty property = serialized.FindProperty(field);
                Assert.IsNotNull(property, $"ProceduralRobotAnimator has no field '{field}' any more.");
                animatorNames.Add(property.stringValue);
            }

            CollectionAssert.AreEquivalent(animatorNames, RobotRig.AnimatedJoints);
            Object.DestroyImmediate(go);
        }

        [Test]
        public void Joints_HaveUniqueNamesAndEveryParentComesFirst()
        {
            var seen = new HashSet<string>();
            int roots = 0;
            foreach (RobotJoint joint in RobotRig.Joints)
            {
                if (joint.Parent == null)
                {
                    roots++;
                }
                else
                {
                    Assert.IsTrue(seen.Contains(joint.Parent), $"{joint.Name}: parent {joint.Parent} must be listed before it.");
                }

                Assert.IsTrue(seen.Add(joint.Name), $"Duplicate joint {joint.Name}");
            }

            Assert.AreEqual(1, roots, "One root joint: Pelvis.");
            Assert.AreEqual(16, RobotRig.Joints.Length, "The prototype robot has 16 bones.");
        }

        [Test]
        public void AnimatedJoints_AllExistInTheJointTable()
        {
            var names = new HashSet<string>(RobotRig.RequiredJoints());

            foreach (string animated in RobotRig.AnimatedJoints)
            {
                Assert.IsTrue(names.Contains(animated), animated);
            }
        }

        [Test]
        public void EveryName_FollowsThePascalCaseAndSideSuffixRule()
        {
            // docs/art-pipeline.md section 2: PascalCase, side as an _L or _R suffix, no spaces, no ".001".
            var rule = new Regex("^[A-Z][A-Za-z0-9]*(_[LR])?$");

            foreach (RobotJoint joint in RobotRig.Joints)
            {
                Assert.IsTrue(rule.IsMatch(joint.Name), joint.Name);
            }

            foreach (RobotPart part in RobotPlaceholder.Parts())
            {
                Assert.IsTrue(rule.IsMatch(part.Name), part.Name);
            }
        }

        [Test]
        public void Parts_HaveUniqueNamesThatNeverCollideWithAJoint()
        {
            // The animator finds a joint by name anywhere below it, so a part with a joint's name could be found instead.
            var names = new HashSet<string>(RobotRig.RequiredJoints());
            var jointNames = new HashSet<string>(names);

            foreach (RobotPart part in RobotPlaceholder.Parts())
            {
                Assert.IsTrue(jointNames.Contains(part.Joint), $"{part.Name} hangs off unknown joint {part.Joint}");
                Assert.IsTrue(names.Add(part.Name), $"{part.Name} is not unique or is also a joint name");
            }
        }

        [Test]
        public void MuzzleOffset_IsBelowTheWristAndTheSocketsFaceAlongTheBarrels()
        {
            Assert.AreEqual(new Vector3(0f, -0.33f, 0f), RobotRig.MuzzleOffset);
            Vector3 forward = RobotRig.SocketRotation * Vector3.forward;

            Assert.That(Vector3.Distance(forward, Vector3.down), Is.LessThan(0.0001f));
        }
    }
}
