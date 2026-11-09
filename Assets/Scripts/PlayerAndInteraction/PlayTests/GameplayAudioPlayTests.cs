// Owner: Systems Engineer. Checks real gameplay callbacks and pooled playback requests, not audible quality.
#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using Blackout.Agents;
using Blackout.Audio;
using Blackout.Interaction;
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Blackout.Diagnostics.PlayTests
{
    public class GameplayAudioPlayTests
    {
        private GameplayAudio audio;
        private GameObject player;
        private WeaponController weapons;
        private static VoidEventChannel Channel(string name) => AssetDatabase.LoadAssetAtPath<VoidEventChannel>("Assets/ScriptableObjects/Events/" + name + ".asset");

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Time.timeScale = 1f;
            SceneManager.LoadScene("Bootstrap");
            float deadline = Time.realtimeSinceStartup + 120f;
            while (Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (GameplayAudio.Current != null && Object.FindFirstObjectByType<SceneBootstrap>().IsLoaded) break;
            }
            audio = GameplayAudio.Current;
            Assert.That(audio, Is.Not.Null);
            player = audio.gameObject;
            weapons = player.GetComponentInChildren<WeaponController>(true);
            player.GetComponent<PlayerInputReader>().enabled = false;
            player.GetComponent<PlayerMotor>().enabled = false;
            player.GetComponent<PlayerLook>().enabled = false;
            foreach (AgentRoot agent in Object.FindObjectsByType<AgentRoot>(FindObjectsSortMode.None))
            {
                agent.enabled = false;
                agent.GetComponent<PathFollower>().enabled = false;
                agent.GetComponent<AgentShooter>().enabled = false;
            }
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            Time.timeScale = 1f;
            // Leave no Bootstrap loader/player for the following fixture, which creates its own player.
            yield return SceneManager.LoadSceneAsync("Environment", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator WeaponHealthAndGeneratorEvents_DoNotDuplicateAfterReenableOrRestore()
        {
            int before = audio.PlayedCount;
            weapons.DebugFire();
            Assert.That(audio.PlayedCount, Is.EqualTo(before + 1));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.Knife));
            audio.enabled = false;
            weapons.DebugFire();
            Assert.That(audio.PlayedCount, Is.EqualTo(before + 1));
            audio.enabled = true;
            weapons.DebugFire();
            Assert.That(audio.PlayedCount, Is.EqualTo(before + 2));

            var health = player.GetComponent<Health>();
            health.TakeDamage(new DamageInfo(10f, player.transform.position, Vector3.forward, null, DamageType.Bullet));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.PlayerHurt));
            object powered = audio.CaptureCheckpoint();
            Channel("GeneratorDisabled").Raise();
            before = audio.PlayedCount;
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.GeneratorStop));
            Channel("GeneratorDisabled").Raise();
            Assert.That(audio.PlayedCount, Is.EqualTo(before));
            yield return new WaitForSeconds(2.6f);
            Assert.That(audio.AlarmVolume, Is.GreaterThan(0f));
            before = audio.PlayedCount; // the blackout can legitimately open/sound the lab door during the wait
            audio.RestoreCheckpoint(powered);
            Assert.That(audio.GeneratorDown, Is.False);
            Assert.That(audio.AlarmVolume, Is.Zero);
            Assert.That(audio.PlayedCount, Is.EqualTo(before), "restoration stops effects without replaying cues");
            Assert.That(GameObject.Find("Gameplay Audio Voices").GetComponentsInChildren<AudioSource>().Length,
                Is.EqualTo(GameplayAudio.VoiceCount + 4));
        }

        [UnityTest]
        public IEnumerator ActualAgentDeathAndGunPickup_UseTheConfiguredAdapters()
        {
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            ActorAudio actor = null;
            foreach (ActorAudio candidate in Object.FindObjectsByType<ActorAudio>(FindObjectsSortMode.None))
                if (candidate.GunCue == AudioCue.HunterGun) { actor = candidate; break; }
            Assert.That(actor, Is.Not.Null);
            Vector3 position = actor.transform.position;
            noise.Raise(new NoiseEvent(position, 40f, NoiseType.Gunshot, actor.gameObject));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.HunterGun));
            var voices = GameObject.Find("Gameplay Audio Voices").GetComponentsInChildren<AudioSource>();
            AudioSource gun = null;
            foreach (AudioSource voice in voices) if (voice.clip != null && voice.clip.name == "HunterGun") gun = voice;
            Assert.That(gun, Is.Not.Null);
            Assert.That(gun.spatialBlend, Is.EqualTo(1f));
            Vector3 gunPosition = gun.transform.position;
            player.GetComponent<PlayerMotor>().Teleport(player.transform.position + Vector3.right, 0f);
            yield return null;
            Assert.That(gun.transform.position, Is.EqualTo(gunPosition), "world voices must not follow the player");
            actor.GetComponent<Health>().TakeDamage(new DamageInfo(10000f, position, Vector3.forward, player, DamageType.Bullet));
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.HumanDeath));
            var drop = actor.GetComponent<EnemyWeaponDrop>().LastDrop.GetComponent<WeaponDropPickup>();
            drop.Interact(player);
            Assert.That(drop.IsTaken, Is.True);
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.Pickup));
            int before = audio.PlayedCount;
            drop.Interact(player);
            Assert.That(audio.PlayedCount, Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator PlayerGunshotsKeepTheirTailsWhenFeedbackVoicesAreFull()
        {
            yield return null;
            Assert.That(weapons.ActiveWeapon.slot, Is.EqualTo(1));
            var bank = AssetDatabase.LoadAssetAtPath<PrototypeAudioBank>("Assets/ScriptableObjects/Audio/PrototypeAudio.asset");
            AudioClip first = bank.GetGunshot(AudioCue.Smg, false), second = bank.GetGunshot(AudioCue.Smg, true);
            var voices = GameObject.Find("Gameplay Audio Voices").GetComponentsInChildren<AudioSource>();
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            int shotNoises = 0;
            void CountShot(NoiseEvent value)
            {
                if (value.source != player || value.type != NoiseType.Gunshot) return;
                shotNoises++;
                Assert.That(value.loudness, Is.EqualTo(weapons.ActiveWeapon.noiseRadius));
            }
            noise.OnRaised += CountShot;
            try
            {
                // Diagnostic fire traverses the real weapon/noise/audio flow without waiting for cooldowns.
                weapons.DebugFire();
                weapons.DebugFire();
                AudioSource firstVoice = null, secondVoice = null;
                foreach (AudioSource voice in voices)
                {
                    if (voice.clip == first) firstVoice = voice;
                    if (voice.clip == second) secondVoice = voice;
                }
                Assert.That(firstVoice, Is.Not.Null);
                Assert.That(secondVoice, Is.Not.Null.And.Not.SameAs(firstVoice));
                for (int i = 0; i < 16; i++)
                {
                    audio.Play(AudioCue.Hit);
                    audio.Play(AudioCue.StepWood);
                    audio.Play(AudioCue.Pickup);
                }
                Assert.That(firstVoice.clip, Is.SameAs(first), "Hit/footstep feedback must not steal player gun voices.");
                Assert.That(secondVoice.clip, Is.SameAs(second));
                Assert.That(firstVoice.isPlaying && secondVoice.isPlaying, Is.True);
                Assert.That(firstVoice.spatialBlend, Is.Zero);
                Assert.That(firstVoice.priority, Is.EqualTo(64));
                Assert.That(firstVoice.pitch, Is.InRange(0.99f, 1.01f));
                for (int i = 0; i < 7; i++) weapons.DebugFire(); // exhaust/reuse the bounded gun group
                int gunSources = 0;
                foreach (AudioSource voice in voices) if (voice.clip == first || voice.clip == second) gunSources++;
                Assert.That(gunSources, Is.EqualTo(8));
                Assert.That(voices.Length, Is.EqualTo(GameplayAudio.VoiceCount + 4));
                Assert.That(shotNoises, Is.EqualTo(9), "Firing audio must not publish duplicate AI noise events.");
                audio.RestoreCheckpoint(audio.CaptureCheckpoint());
                weapons.DebugFire();
                Assert.That(firstVoice.clip, Is.SameAs(first), "Recovery starts from the first recorded take.");
            }
            finally { noise.OnRaised -= CountShot; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MotorFootstepsKeepAINoisesButUseSlowerAlternatingAudio()
        {
            // Controlled distance samples enter the real motor -> event -> audio/noise paths.
            // Native keyboard capture and audible quality remain manual checks.
            var motor = player.GetComponent<PlayerMotor>();
            var motorType = typeof(PlayerMotor);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            ((FootstepTracker)motorType.GetField("footsteps", flags).GetValue(motor)).Reset();
            motorType.GetProperty(nameof(PlayerMotor.Grounded)).SetValue(motor, true);
            motorType.GetProperty(nameof(PlayerMotor.HorizontalSpeed)).SetValue(motor, motor.Settings.walkSpeed);
            motorType.GetProperty(nameof(PlayerMotor.LastMoveDistance)).SetValue(motor, motor.Settings.walkStride);
            motorType.GetProperty(nameof(PlayerMotor.State)).SetValue(motor, MovementState.Walk);
            MethodInfo tick = motorType.GetMethod("UpdateFootsteps", flags);
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            var bank = AssetDatabase.LoadAssetAtPath<PrototypeAudioBank>("Assets/ScriptableObjects/Audio/PrototypeAudio.asset");
            var voices = GameObject.Find("Gameplay Audio Voices").GetComponentsInChildren<AudioSource>();
            int noiseCount = 0;
            void CountNoise(NoiseEvent value)
            {
                if (value.source != player || value.type != NoiseType.Footstep) return;
                noiseCount++;
                Assert.That(value.loudness, Is.EqualTo(motor.Settings.walkNoise).Within(0.001f));
            }
            noise.OnRaised += CountNoise;
            try
            {
                int before = audio.PlayedCount;
                AudioCue surface = default;
                for (int i = 0; i < 6; i++)
                {
                    tick.Invoke(motor, null);
                    Assert.That(audio.PlayedCount - before, Is.EqualTo((i + 2) / 2));
                    if ((i & 1) != 0) continue;
                    if (i == 0) surface = audio.LastCue;
                    Assert.That((int)surface, Is.InRange((int)AudioCue.StepSand, (int)AudioCue.StepConcrete));
                    AudioClip expected = bank.GetFootstep(surface, (i / 2 & 1) != 0);
                    bool found = false;
                    foreach (AudioSource voice in voices)
                        if (voice.clip == expected)
                        {
                            Assert.That(voice.volume, Is.LessThanOrEqualTo(0.7141f));
                            Assert.That(voice.pitch, Is.InRange(0.98f, 1.02f));
                            found = true;
                        }
                    Assert.That(found, Is.True, "The expected recorded footfall must reach the real voice pool.");
                }
                float simulatedSeconds = 6f * motor.Settings.walkStride / motor.Settings.walkSpeed;
                Assert.That((audio.PlayedCount - before) / simulatedSeconds, Is.InRange(2f, 3f));
                Assert.That(noiseCount, Is.EqualTo(6), "All motor hearing events retain their original radii and cadence.");
                object checkpoint = audio.CaptureCheckpoint();
                audio.RestoreCheckpoint(checkpoint);
                before = audio.PlayedCount;
                tick.Invoke(motor, null);
                Assert.That(audio.PlayedCount, Is.EqualTo(before + 1), "Recovery starts a fresh audible step.");
            }
            finally { noise.OnRaised -= CountNoise; }
            yield return null;
        }

        [UnityTest]
        public IEnumerator CrouchAndProneMovementAreSilent_StandingStepsResumeWithoutStoredDistance()
        {
            // Controlled travel exercises the configured motor, audio subscription and shared AI noise channel.
            var motor = player.GetComponent<PlayerMotor>();
            var motorType = typeof(PlayerMotor);
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            MethodInfo tick = motorType.GetMethod("UpdateFootsteps", flags);
            MethodInfo changeStance = motorType.GetMethod("TrySetStance", flags);
            var distance = motorType.GetProperty(nameof(PlayerMotor.LastMoveDistance));
            var speed = motorType.GetProperty(nameof(PlayerMotor.HorizontalSpeed));
            var noise = AssetDatabase.LoadAssetAtPath<NoiseEventChannel>("Assets/ScriptableObjects/Events/Noise.asset");
            motorType.GetProperty(nameof(PlayerMotor.Grounded)).SetValue(motor, true);
            motorType.GetProperty(nameof(PlayerMotor.State)).SetValue(motor, MovementState.Walk);
            int steps = 0, noises = 0;
            NoiseEvent lastNoise = default;
            void CountStep(MovementState _) => steps++;
            void CountNoise(NoiseEvent value)
            {
                if (value.source != player) return;
                noises++;
                lastNoise = value;
            }
            motor.Footstep += CountStep;
            noise.OnRaised += CountNoise;
            try
            {
                foreach (Stance stance in new[] { Stance.Crouching, Stance.Prone })
                {
                    motor.StopMotion();
                    audio.RestoreCheckpoint(audio.CaptureCheckpoint());
                    int stepsBefore = steps, noisesBefore = noises, soundsBefore = audio.PlayedCount;
                    // A partial standing stride must also be discarded when entering a silent stance.
                    speed.SetValue(motor, motor.Settings.walkSpeed);
                    distance.SetValue(motor, motor.Settings.walkStride * 0.75f);
                    tick.Invoke(motor, null);
                    Assert.That((bool)changeStance.Invoke(motor, new object[] { stance }), Is.True);
                    speed.SetValue(motor, PlayerMovementLogic.TargetSpeed(MovementState.Walk, stance, motor.Settings));
                    distance.SetValue(motor, motor.Settings.walkStride);
                    for (int i = 0; i < 8; i++) tick.Invoke(motor, null);
                    Assert.That(steps, Is.EqualTo(stepsBefore), stance.ToString());
                    Assert.That(noises, Is.EqualTo(noisesBefore), "Silent movement must publish no AI stimulus.");
                    Assert.That(audio.PlayedCount, Is.EqualTo(soundsBefore), "Silent movement must request no step audio.");

                    Assert.That((bool)changeStance.Invoke(motor, new object[] { Stance.Standing }), Is.True);
                    speed.SetValue(motor, motor.Settings.walkSpeed);
                    distance.SetValue(motor, motor.Settings.walkStride * 0.5f);
                    tick.Invoke(motor, null);
                    Assert.That(steps, Is.EqualTo(stepsBefore), "Silent travel must not cause an early step after standing.");
                    Assert.That(noises, Is.EqualTo(noisesBefore));
                    Assert.That(audio.PlayedCount, Is.EqualTo(soundsBefore));
                    tick.Invoke(motor, null);
                    Assert.That(steps, Is.EqualTo(stepsBefore + 1));
                    Assert.That(noises, Is.EqualTo(noisesBefore + 1));
                    Assert.That(lastNoise.type, Is.EqualTo(NoiseType.Footstep));
                    Assert.That(lastNoise.loudness, Is.EqualTo(motor.Settings.walkNoise).Within(0.001f));
                    Assert.That(audio.PlayedCount, Is.EqualTo(soundsBefore + 1));
                }
            }
            finally
            {
                motor.Footstep -= CountStep;
                noise.OnRaised -= CountNoise;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator PauseRejectsNewGameplaySounds_ButCompletionCanPlay()
        {
            Time.timeScale = 0f;
            yield return null;
            int before = audio.PlayedCount;
            Assert.That(audio.Play(AudioCue.Smg), Is.False);
            Assert.That(audio.PlayedCount, Is.EqualTo(before));
            Channel("MissionComplete").Raise();
            Assert.That(audio.LastCue, Is.EqualTo(AudioCue.Complete));
            Assert.That(audio.PlayedCount, Is.EqualTo(before + 1));
            yield return null;
        }
    }
}
#endif
