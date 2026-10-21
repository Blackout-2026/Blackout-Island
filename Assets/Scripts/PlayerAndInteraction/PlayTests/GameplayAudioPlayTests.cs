// Owner: Systems Engineer. Checks real gameplay callbacks and pooled playback requests, not audible quality.
#if UNITY_EDITOR
using System.Collections;
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
