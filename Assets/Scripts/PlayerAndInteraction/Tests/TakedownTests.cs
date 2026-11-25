using System.Linq;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Takedowns;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Interaction.Tests
{
    public class TakedownTests
    {
        private const string ProfilePath = "Assets/ScriptableObjects/Weapons/KnifeTakedown.asset";
        private const string ActorPath = "Assets/Prefabs/Player/TakedownActor.prefab";

        [Test]
        public void FrontalProfile_HasOneKickAndTwoOrderedNativeStabs()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>("Assets/ScriptableObjects/Weapons/KnifeFrontalTakedown.asset");
            Assert.That(profile.MissingRequirement, Is.Null);
            Assert.That(profile.style, Is.EqualTo(TakedownStyle.Frontal));
            Assert.That(profile.range, Is.EqualTo(1.5f));
            Assert.That(profile.kick.humanMotion && profile.playerAttack.humanMotion, Is.True);
            Assert.That(profile.kick.length, Is.EqualTo(1.6f).Within(0.04f));
            Assert.That(profile.playerAttack.length, Is.EqualTo(2.533f).Within(0.04f));
            Assert.That(profile.kick.events.Single(e => e.functionName == "TakedownKick").time, Is.EqualTo(0.97f).Within(0.04f));
            AnimationEvent[] hits = profile.playerAttack.events.Where(e => e.functionName == "TakedownStab").ToArray();
            Assert.That(hits.Select(e => e.intParameter), Is.EqualTo(new[] { 1, 2 }));
            Assert.That(hits[0].time, Is.EqualTo(1f).Within(0.04f));
            Assert.That(hits[1].time, Is.EqualTo(2.4f).Within(0.04f));
            Assert.That(profile.stabSpeed, Is.EqualTo(3.5f));
            Assert.That(profile.StabDuration, Is.EqualTo(2.533f / 3.5f).Within(0.02f));
            Assert.That(profile.playerOffset.z, Is.EqualTo(1.1f));
        }

        [Test]
        public void SavedProfile_UsesTheRequestedActivationRange()
        {
            var profile = AssetDatabase.LoadAssetAtPath<TakedownProfile>(ProfilePath);
            Assert.That(profile, Is.Not.Null);
            Assert.That(profile.range, Is.EqualTo(3.5f));
            Assert.That(profile.MissingRequirement, Is.Null);
            Assert.That(profile.playerAttack.humanMotion && profile.victimReaction.humanMotion, Is.True);
            Assert.That(profile.playerAttack.length, Is.EqualTo(7f).Within(0.04f));
            Assert.That(profile.victimReaction.length, Is.EqualTo(6.933f).Within(0.04f));
            Assert.That(profile.playerAttack.events.Single(e => e.functionName == "TakedownSound").time,
                Is.EqualTo(1.8f).Within(0.04f));
            Assert.That(profile.playerAttack.events.Single(e => e.functionName == "TakedownImpact").time,
                Is.EqualTo(2.1f).Within(0.04f));
        }

        [Test]
        public void Profile_RequiresOneNativeImpactMarkerAndOneSoundMarker()
        {
            var profile = ScriptableObject.CreateInstance<TakedownProfile>();
            var clip = new AnimationClip();
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("Bone", typeof(Transform), "m_LocalPosition.x"),
                AnimationCurve.Linear(0f, 0f, 1f, 0f));
            try
            {
                profile.playerAttack = profile.victimReaction = clip;
                Assert.That(profile.MissingRequirement, Does.Contain("TakedownImpact"));
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "TakedownImpact", time = 0.4f } });
                Assert.That(profile.MissingRequirement, Does.Contain("TakedownSound"));
                AnimationUtility.SetAnimationEvents(clip, new[] { new AnimationEvent { functionName = "TakedownImpact", time = 0.4f },
                    new AnimationEvent { functionName = "TakedownSound", time = 0.2f } });
                Assert.That(profile.MissingRequirement, Is.Null);
                AnimationUtility.SetAnimationEvents(clip, clip.events.Concat(new[] { new AnimationEvent { functionName = "TakedownImpact", time = 0.6f } }).ToArray());
                Assert.That(profile.MissingRequirement, Does.Contain("exactly one"));
            }
            finally { Object.DestroyImmediate(profile); Object.DestroyImmediate(clip); }
        }

        [Test]
        public void SavedPlayer_UsesSeparateBodyAndRetainsOriginalFpsRig()
        {
            var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            var controller = player.GetComponent<TakedownController>();
            Assert.That(controller, Is.Not.Null);
            var serialized = new SerializedObject(controller);
            Assert.That(serialized.FindProperty("cinematicActorPrefab").objectReferenceValue,
                Is.EqualTo(AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath)));
            WeaponController weapons = player.GetComponentInChildren<WeaponController>(true);
            Assert.That(new SerializedObject(weapons).FindProperty("takedown").objectReferenceValue, Is.EqualTo(controller));
            foreach (string name in new[] { "SMG", "Rifle", "Knife" }) Assert.That(weapons.transform.Find(name), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerInputReader>(), Is.Not.Null);
            Assert.That(player.GetComponent<PlayerMotor>(), Is.Not.Null);
            Assert.That(player.GetComponentInChildren<Camera>(true).fieldOfView, Is.EqualTo(72f));
            Assert.That(player.GetComponentsInChildren<TakedownActor>(true), Is.Empty, "the actor is an external prefab, instantiated only on demand");
        }

        [Test]
        public void FullBodyTemplate_HasHumanoidAnimatorAndKnife_WithoutAnEnemyBrainOrFpsArms()
        {
            var body = AssetDatabase.LoadAssetAtPath<GameObject>(ActorPath);
            TakedownActor receiver = body.GetComponentInChildren<TakedownActor>(true);
            Animator animator = receiver.GetComponent<Animator>();
            Assert.That(receiver.name, Is.EqualTo("CinematicWinterSoldier"));
            Assert.That(animator.avatar, Is.EqualTo(AssetDatabase.LoadAllAssetsAtPath("Assets/Models/Custom/SM_Player_WinterSoldier.fbx").OfType<Avatar>().Single()));
            Assert.That(animator.avatar.isHuman && animator.avatar.isValid, Is.True);
            Assert.That(animator.applyRootMotion, Is.False);
            Assert.That(body.GetComponentInChildren<SkinnedMeshRenderer>(true), Is.Not.Null);
            Assert.That(body.GetComponentsInChildren<Collider>(true), Is.Empty);
            Assert.That(body.GetComponentsInChildren<Health>(true), Is.Empty);
            Assert.That(body.GetComponentsInChildren<IAgentBrain>(true), Is.Empty);
            foreach (Renderer renderer in body.GetComponentsInChildren<Renderer>(true).Where(r => ViewmodelHands.IsArmPart(r.name)))
                Assert.That(renderer.enabled, Is.False, renderer.name);
            Assert.That(new SerializedObject(receiver).FindProperty("knife").objectReferenceValue, Is.Not.Null);
        }
    }
}
