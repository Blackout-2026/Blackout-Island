using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Blackout.Interaction.Tests
{
    /// <summary>
    /// The interactables' rules without a scene. Awake does not run in EditMode, so tests only use methods that do
    /// not depend on it, and set private fields through SerializedObject as the prefab builder does.
    /// </summary>
    public class InteractablesTests
    {
        private GameObject gameObject;
        private VoidEventChannel generatorDisabled;
        private NoiseEventChannel noise;

        [SetUp]
        public void SetUp()
        {
            gameObject = new GameObject("Interactable under test");
            generatorDisabled = ScriptableObject.CreateInstance<VoidEventChannel>();
            noise = ScriptableObject.CreateInstance<NoiseEventChannel>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(generatorDisabled);
            Object.DestroyImmediate(noise);
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- generator: the step's "done when" ------------------------------------------------------------

        [Test]
        public void Generator_HoldTwoSeconds_RaisesGeneratorDisabledOnceWithA60mNoise()
        {
            var generator = gameObject.AddComponent<GeneratorInteractable>();
            Set(generator, "generatorDisabled", generatorDisabled);
            Set(generator, "noiseChannel", noise);

            int raised = 0;
            generatorDisabled.OnRaised += () => raised++; // the listener that proves the event fired
            NoiseEvent heard = default;
            noise.OnRaised += e => heard = e;

            // Drive it exactly as the PlayerInteractor does: hold E for 2 s at 60 fps.
            var hold = new HoldTimer();
            bool done = false;
            for (int frame = 0; frame < 200 && !done; frame++)
            {
                done = hold.Update(true, generator.HoldTime, 1f / 60f);
            }

            Assert.IsTrue(done);
            generator.Interact(gameObject);

            Assert.AreEqual(1, raised);
            Assert.AreEqual(NoiseType.Generator, heard.type);
            Assert.AreEqual(60f, heard.loudness);
            Assert.IsFalse(generator.CanInteract, "no prompt once it is off");

            generator.Interact(gameObject);
            Assert.AreEqual(1, raised, "a second use does nothing");
        }

        [Test]
        public void Generator_HoldTimeIsTwoSeconds()
        {
            Assert.AreEqual(2f, gameObject.AddComponent<GeneratorInteractable>().HoldTime);
        }

        // ---- doors -------------------------------------------------------------------------------------

        [Test]
        public void Door_Locked_RefusesAgents_UnlockedOpensOnce()
        {
            var door = gameObject.AddComponent<SlidingDoor>();
            Set(door, "noiseChannel", noise);
            int noises = 0;
            noise.OnRaised += e => noises++;

            door.SetLocked(true);
            Assert.IsFalse(door.RequestOpen(null));
            Assert.AreEqual(0, noises, "a locked door makes no door noise");

            door.SetLocked(false);
            Assert.IsTrue(door.RequestOpen(null));
            Assert.IsFalse(door.RequestOpen(null), "already opening");
            Assert.AreEqual(1, noises);
        }

        // ---- health pickup -------------------------------------------------------------------------------

        [Test]
        public void HealthPickup_AtFullHealth_StaysForLater()
        {
            var health = gameObject.AddComponent<Health>();
            health.ResetHealth(0f);
            var kit = new GameObject("Kit").AddComponent<HealthPickup>();

            Assert.AreEqual(0f, kit.TryHeal(health));
            Assert.IsFalse(kit.IsTaken);
            Object.DestroyImmediate(kit.gameObject);
        }

        [Test]
        public void HealthPickup_WhenHurt_Heals35AndIsUsedUp()
        {
            var health = gameObject.AddComponent<Health>();
            health.ResetHealth(0f);
            health.TakeDamage(new DamageInfo(50f, Vector3.zero, Vector3.zero, null, DamageType.Generic));
            var kit = new GameObject("Kit").AddComponent<HealthPickup>();

            Assert.AreEqual(35f, kit.TryHeal(health));
            Assert.AreEqual(85f, health.Current);
            Assert.IsTrue(kit.IsTaken);
            Assert.AreEqual(0f, kit.TryHeal(health), "only once");
            Object.DestroyImmediate(kit.gameObject);
        }

        // ---- crate ---------------------------------------------------------------------------------------

        [Test]
        public void Crate_PushesAlongTheDominantAxisAt42Percent()
        {
            Vector3 velocity = CrateMath.PushVelocity(new Vector3(0.3f, 0f, 0.9f), 4.3f);
            Assert.AreEqual(0f, velocity.x);
            Assert.AreEqual(4.3f * 0.42f, velocity.z, 0.001f);
        }

        [Test]
        public void Crate_SprintPushIsCappedAt4Point5()
        {
            Assert.AreEqual(4.5f * 0.42f, CrateMath.PushVelocity(Vector3.left, 7f).magnitude, 0.001f);
        }
    }
}
