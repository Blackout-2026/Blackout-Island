// Owner: Agent Controller. Check the real saved prefabs and native animation evaluation (AN1).
using System.Linq;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Blackout.Agents.Tests
{
    public class SoldierAnimationTests
    {
        [TestCase("SecurityPatrol")]
        [TestCase("Hunter")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void SavedPrefab_HasOneEnabledDriver_AValidAvatar_AndNoRootMotion(string name)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab");
            MonoBehaviour[] drivers = prefab.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(c => c is IAgentAnimator && c.enabled).ToArray();
            Assert.That(drivers.Length, Is.EqualTo(1));
            Assert.That(drivers[0], Is.TypeOf<AnimatorDriver>());
            Animator animator = drivers[0].GetComponent<Animator>();
            Assert.That(animator.avatar.isValid && animator.avatar.isHuman, Is.True);
            Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(animator.cullingMode, Is.EqualTo(AnimatorCullingMode.AlwaysAnimate));
            var driver = new SerializedObject(drivers[0]);
            Assert.That(driver.FindProperty("visualRoot").objectReferenceValue, Is.EqualTo(prefab.transform.Find("Visual")));
        }

        [TestCase("SecurityPatrol")]
        [TestCase("Hunter")]
        [TestCase("TacticalGuard")]
        [TestCase("Commander")]
        public void NativeEvaluation_MovesLegsDuringAim_AndPlaysDeath_WithoutMovingTheAgent(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Agents/{name}.prefab");
            GameObject agent = Object.Instantiate(prefab);
            try
            {
                Animator animator = agent.GetComponentInChildren<AnimatorDriver>().GetComponent<Animator>();
                animator.Rebind();
                animator.SetFloat("Speed", 4f);
                animator.SetBool("Aiming", true);
                animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), 1f);
                animator.Play("Aim", 0, 0f);
                animator.Update(0f);
                Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                Quaternion first = foot.rotation;
                Vector3 origin = agent.transform.position;
                animator.Update(0.17f);
                Assert.That(Quaternion.Angle(first, foot.rotation), Is.GreaterThan(2f), name + " legs stayed frozen while aiming");
                Assert.That(Vector3.Distance(origin, agent.transform.position), Is.LessThan(0.001f));
                animator.SetLayerWeight(animator.GetLayerIndex("WeaponPose"), 0f);
                animator.SetBool("Dead", true);
                animator.Update(0.2f);
                animator.Update(0.8f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True);
                animator.SetTrigger("Hit");
                animator.Update(0.2f);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("Death"), Is.True, "hit must not resurrect a dead agent");
                Assert.That(Vector3.Distance(origin, agent.transform.position), Is.LessThan(0.001f));
                Debug.Log($"[AN1] {name}: native leg motion, aim locomotion, death and fixed gameplay root verified.");
            }
            finally { Object.DestroyImmediate(agent); }
        }

        [Test]
        public void Controller_UsesTheImportedClips_AndKeepsAimLocomotionAndWeaponPoseSeparate()
        {
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Animations/Agent_Humanoid.controller");
            var states = controller.layers[0].stateMachine.states;
            AnimatorState locomotion = states.Single(s => s.state.name == "Locomotion").state;
            AnimatorState aim = states.Single(s => s.state.name == "Aim").state;
            Assert.That(aim.motion, Is.SameAs(locomotion.motion));
            var tree = (BlendTree)locomotion.motion;
            foreach (ChildMotion child in tree.children)
            {
                Assert.That(child.motion, Is.Not.Null);
                Assert.That(AssetDatabase.GetAssetPath(child.motion), Does.StartWith("Assets/Kevin Iglesias/"));
            }
            Assert.That(controller.animationClips.All(c => c != null && c.humanMotion), Is.True);
            Assert.That(controller.animationClips.Select(c => c.name), Does.Contain("HumanM@Death01"));
            AnimatorControllerLayer weapon = controller.layers.Single(l => l.name == "WeaponPose");
            Assert.That(weapon.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftLeg), Is.False);
            Assert.That(weapon.avatarMask.GetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm), Is.True);
        }
    }
}
