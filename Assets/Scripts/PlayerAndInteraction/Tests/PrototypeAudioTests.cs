using System;
using Blackout.Audio;
using Blackout.Interaction;
using Blackout.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;

namespace Blackout.Player.Tests
{
    public class PrototypeAudioTests
    {
        private const string BankPath = "Assets/ScriptableObjects/Audio/PrototypeAudio.asset";
        private static GameObject Prefab(string name) => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/" + name + ".prefab");

        [Test]
        public void EveryRecipeHasAnImportedNonSilentMonoClip()
        {
            var bank = AssetDatabase.LoadAssetAtPath<PrototypeAudioBank>(BankPath);
            Assert.That(bank, Is.Not.Null);
            foreach (AudioCue cue in Enum.GetValues(typeof(AudioCue)))
            {
                AudioClip clip = bank.Get(cue);
                Assert.That(clip, Is.Not.Null, cue.ToString());
                Assert.That(clip.channels, Is.EqualTo(1), cue.ToString());
                Assert.That(clip.frequency, Is.EqualTo(22050), cue.ToString());
                Assert.That(clip.length, Is.InRange(0.02f, 8.01f), cue.ToString());
                clip.LoadAudioData();
                var samples = new float[clip.samples];
                Assert.That(clip.GetData(samples, 0), Is.True, cue.ToString());
                float peak = 0f;
                foreach (float sample in samples) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Assert.That(peak, Is.GreaterThan(0.0001f).And.LessThanOrEqualTo(1f), cue.ToString());
            }
        }

        [Test]
        public void PlayerAndGeneratorAreWiredWithoutAnotherAudioListener()
        {
            GameObject player = Prefab("Player/Player");
            var audio = player.GetComponent<GameplayAudio>();
            Assert.That(audio, Is.Not.Null);
            var fields = new SerializedObject(audio);
            foreach (string field in new[] { "bank", "noise", "generatorDisabled", "cureTaken", "missionComplete", "motor", "weapons", "health" })
                Assert.That(fields.FindProperty(field).objectReferenceValue, Is.Not.Null, field);
            Assert.That(player.GetComponentsInChildren<AudioListener>(true).Length, Is.EqualTo(1));
            Assert.That(fields.FindProperty("bunker").boundsValue.Contains(new Vector3(0f, 3f, 130f)), Is.True);
            Assert.That(fields.FindProperty("entrance").boundsValue.Contains(new Vector3(0f, 7f, 61f)), Is.True);
            AudioSource hum = Prefab("Models/Generator_Visual").transform.Find("HumAudioSource").GetComponent<AudioSource>();
            Assert.That(hum.clip, Is.SameAs(AssetDatabase.LoadAssetAtPath<PrototypeAudioBank>(BankPath).Get(AudioCue.GeneratorHum)));
            Assert.That(hum.loop && hum.playOnAwake, Is.True);
            Assert.That(hum.spatialBlend, Is.EqualTo(1f));
        }

        [TestCase("SecurityPatrol", AudioCue.PatrolGun)]
        [TestCase("Hunter", AudioCue.HunterGun)]
        [TestCase("TacticalGuard", AudioCue.GuardGun)]
        [TestCase("Commander", AudioCue.CommanderGun)]
        public void AgentPrefabHasItsOwnVoiceAndConnectedGunfire(string name, AudioCue cue)
        {
            GameObject agent = Prefab("Agents/" + name);
            Assert.That(agent.GetComponent<ActorAudio>().GunCue, Is.EqualTo(cue));
            foreach (MonoBehaviour component in agent.GetComponents<MonoBehaviour>())
                if (component != null && component.GetType().Name == "AgentShooter")
                {
                    Assert.That(new SerializedObject(component).FindProperty("gunfireChannel").objectReferenceValue,
                        Is.SameAs(AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset")));
                    return;
                }
            Assert.Fail("Missing existing AgentShooter on " + name);
        }

        [Test]
        public void PersistentInteractionHooksArePresentExactlyOnce()
        {
            var door = Prefab("Interactables/Door").GetComponent<SlidingDoor>();
            Hook(door.onOpened, nameof(InteractionAudio.DoorMove));
            Hook(door.onClosed, nameof(InteractionAudio.DoorMove));
            Hook(door.onLockedUsed, nameof(InteractionAudio.DoorLocked));
            Hook(Prefab("Interactables/Generator").GetComponentInChildren<GeneratorInteractable>(true).onHoldSpark, nameof(InteractionAudio.HoldTick));
            Hook(Prefab("Interactables/HealthPickup").GetComponent<HealthPickup>().onPickedUp, nameof(InteractionAudio.Pickup));
            Hook(Prefab("Interactables/MedkitDrop").GetComponent<MedkitPickup>().onPickedUp, nameof(InteractionAudio.Pickup));
            Hook(Prefab("Interactables/AmmoDrop").GetComponent<AmmoPickup>().onPickedUp, nameof(InteractionAudio.Pickup));
            Hook(Prefab("Interactables/WeaponDrop").GetComponent<WeaponDropPickup>().onTaken, nameof(InteractionAudio.Pickup));
        }

        private static void Hook(UnityEvent channel, string method)
        {
            int count = 0;
            for (int i = 0; i < channel.GetPersistentEventCount(); i++)
                if (channel.GetPersistentTarget(i) is InteractionAudio && channel.GetPersistentMethodName(i) == method) count++;
            Assert.That(count, Is.EqualTo(1), method);
        }
    }
}
