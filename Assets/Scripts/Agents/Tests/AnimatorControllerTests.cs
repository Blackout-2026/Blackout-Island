using System;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine; // AnimatorControllerParameterType is the runtime enum, not UnityEditor.Animations

namespace Blackout.Agents.Tests
{
    public class AnimatorControllerTests
    {
        private const string ControllerPath = "Assets/Animations/Agent_Humanoid.controller";

        private AnimatorController controller;

        [SetUp]
        public void SetUp()
        {
            controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            Assert.IsNotNull(controller, $"{ControllerPath} is missing. Run Blackout > Agents > Create Humanoid Animator Controller.");
        }

        private static void AssertHasParameter(AnimatorController c, string paramName, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in c.parameters)
            {
                if (p.name == paramName)
                {
                    Assert.AreEqual(type, p.type, $"{paramName} has the wrong type");
                    return;
                }
            }

            Assert.Fail($"No parameter named {paramName}");
        }

        [Test]
        public void HasTheFourParametersAnimatorDriverDrives()
        {
            AssertHasParameter(controller, "Speed", AnimatorControllerParameterType.Float);
            AssertHasParameter(controller, "Aiming", AnimatorControllerParameterType.Bool);
            AssertHasParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
            AssertHasParameter(controller, "Dead", AnimatorControllerParameterType.Bool);
        }

        [Test]
        public void HasTheFourExpectedStates()
        {
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            string[] expected = { "Locomotion", "Aim", "HitReact", "Death" };

            foreach (string stateName in expected)
            {
                bool found = Array.Exists(states, child => child.state.name == stateName);
                Assert.IsTrue(found, $"missing state '{stateName}'");
            }
        }

        [Test]
        public void LocomotionIsTheDefaultState_AndHoldsABlendTreeOnSpeed()
        {
            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            Assert.IsNotNull(stateMachine.defaultState);
            Assert.AreEqual("Locomotion", stateMachine.defaultState.name);

            var tree = stateMachine.defaultState.motion as BlendTree;
            Assert.IsNotNull(tree, "Locomotion's motion should be a BlendTree");
            Assert.AreEqual("Speed", tree.blendParameter);
            Assert.AreEqual(3, tree.children.Length, "Idle, Walk, Run");
        }

        [Test]
        public void TheBlendTreeThresholdsMatchTheControllerDefaults()
        {
            var tree = controller.layers[0].stateMachine.defaultState.motion as BlendTree;
            float[] thresholds = Array.ConvertAll(tree.children, c => c.threshold);
            Array.Sort(thresholds);

            Assert.AreEqual(0f, thresholds[0], 0.001f);
            Assert.AreEqual(1.5f, thresholds[1], 0.001f, "docs/reference/agent-controller.md 3.1 walk speed default");
            Assert.AreEqual(4.0f, thresholds[2], 0.001f, "docs/reference/agent-controller.md 3.1 run speed default");
        }

        [Test]
        public void AnyStateTransitionsIntoHitReactAndDeath()
        {
            AnimatorStateTransition[] anyState = controller.layers[0].stateMachine.anyStateTransitions;

            Assert.IsTrue(Array.Exists(anyState, t => t.destinationState != null && t.destinationState.name == "HitReact"), "no Any State -> HitReact transition");
            Assert.IsTrue(Array.Exists(anyState, t => t.destinationState != null && t.destinationState.name == "Death"), "no Any State -> Death transition");
        }

        [Test]
        public void AimingTogglesBetweenLocomotionAndAim()
        {
            ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
            AnimatorState locomotion = Array.Find(states, s => s.state.name == "Locomotion").state;
            AnimatorState aim = Array.Find(states, s => s.state.name == "Aim").state;

            Assert.IsTrue(Array.Exists(locomotion.transitions, t => t.destinationState == aim), "Locomotion should transition into Aim");
            Assert.IsTrue(Array.Exists(aim.transitions, t => t.destinationState == locomotion), "Aim should transition back to Locomotion");
        }
    }
}
