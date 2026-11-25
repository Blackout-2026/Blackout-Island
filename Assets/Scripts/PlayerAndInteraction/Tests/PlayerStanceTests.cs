using System.Linq;
using Blackout.Player;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Blackout.Interaction.Tests
{
    /// <summary>Step F2: C crouches, Z lies prone, and both slow the player down and lower the eyes.</summary>
    public class PlayerStanceTests
    {
        private PlayerMovementSettings settings;

        [SetUp]
        public void SetUp()
        {
            settings = ScriptableObject.CreateInstance<PlayerMovementSettings>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(settings);
        }

        [Test]
        public void C_TogglesCrouching_AndZ_TogglesProne()
        {
            Assert.That(PlayerMovementLogic.NextStance(Stance.Standing, true, false, false), Is.EqualTo(Stance.Crouching));
            Assert.That(PlayerMovementLogic.NextStance(Stance.Crouching, true, false, false), Is.EqualTo(Stance.Standing));
            Assert.That(PlayerMovementLogic.NextStance(Stance.Standing, false, true, false), Is.EqualTo(Stance.Prone));
            Assert.That(PlayerMovementLogic.NextStance(Stance.Crouching, false, true, false), Is.EqualTo(Stance.Prone));
            Assert.That(PlayerMovementLogic.NextStance(Stance.Prone, false, true, false), Is.EqualTo(Stance.Standing));
            Assert.That(PlayerMovementLogic.NextStance(Stance.Prone, true, false, false), Is.EqualTo(Stance.Crouching), "C from prone comes up to a crouch");
        }

        [Test]
        public void NoKey_KeepsTheStance_AndSprintingOrJumpingStandsUp()
        {
            foreach (Stance stance in new[] { Stance.Standing, Stance.Crouching, Stance.Prone })
            {
                Assert.That(PlayerMovementLogic.NextStance(stance, false, false, false), Is.EqualTo(stance));
                Assert.That(PlayerMovementLogic.NextStance(stance, false, false, true), Is.EqualTo(Stance.Standing));
            }
        }

        [Test]
        public void CrouchingAndProne_AreLowerAndSlower()
        {
            Assert.That(PlayerMovementLogic.StanceHeight(Stance.Standing, settings), Is.EqualTo(1.75f));
            Assert.That(PlayerMovementLogic.StanceHeight(Stance.Crouching, settings), Is.EqualTo(1.1f));
            Assert.That(PlayerMovementLogic.StanceHeight(Stance.Prone, settings), Is.EqualTo(0.7f));
            Assert.That(settings.proneHeight, Is.GreaterThanOrEqualTo(2f * settings.radius), "a capsule cannot be lower than twice its radius");
            Assert.That(settings.EyeHeightFor(Stance.Crouching), Is.LessThan(settings.EyeHeightFor(Stance.Standing)));
            Assert.That(settings.EyeHeightFor(Stance.Prone), Is.LessThan(settings.EyeHeightFor(Stance.Crouching)));

            float walk = PlayerMovementLogic.TargetSpeed(MovementState.Walk, Stance.Standing, settings);
            float crouch = PlayerMovementLogic.TargetSpeed(MovementState.Walk, Stance.Crouching, settings);
            float prone = PlayerMovementLogic.TargetSpeed(MovementState.Walk, Stance.Prone, settings);
            Assert.That(walk, Is.EqualTo(4.3f));
            Assert.That(crouch, Is.EqualTo(4.3f * 0.55f).Within(1e-4f));
            Assert.That(prone, Is.LessThan(crouch));
        }

        [Test]
        public void TheControlsMapCToCrouchAndZToProne()
        {
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/Scripts/PlayerAndInteraction/Input/PlayerControls.inputactions");
            Assert.That(asset, Is.Not.Null);
            InputActionMap map = asset.FindActionMap("Player", true);
            Assert.That(map.FindAction("Crouch", true).bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/c" }));
            Assert.That(map.FindAction("Prone", true).bindings.Select(b => b.path), Is.EquivalentTo(new[] { "<Keyboard>/z" }));
        }

        [Test]
        public void ThePlayerPrefab_HasTheMotorAndTheLookOnOneObject()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
            Assert.That(prefab, Is.Not.Null);
            // PlayerLook finds the motor on its own object to put the camera pivot at the eye height of the stance.
            Assert.That(prefab.GetComponent<PlayerLook>(), Is.Not.Null);
            Assert.That(prefab.GetComponent<PlayerMotor>(), Is.Not.Null);
        }
    }
}
