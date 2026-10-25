// Owner: Systems Engineer. AU1: playback only; never publishes AI noises or changes decisions.
using Blackout.Mission;
using Blackout.Player;
using Blackout.Shared;
using Blackout.Weapons;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Audio
{
    /// <summary>One player-owned sound service, with stationary pooled world voices and blended ambience.</summary>
    [DisallowMultipleComponent]
    public sealed class GameplayAudio : MonoBehaviour, ICheckpointState
    {
        public const int VoiceCount = 32;
        private const int GunVoices = 8, FeedbackVoices = 4;
        [SerializeField] private PrototypeAudioBank bank;
        [SerializeField] private NoiseEventChannel noise;
        [SerializeField] private VoidEventChannel generatorDisabled, cureTaken, missionComplete;
        [SerializeField] private PlayerMotor motor;
        [SerializeField] private WeaponController weapons;
        [SerializeField] private Health health;
        [Tooltip("Copied from Environment's existing blackout volumes by the builder; no cross-scene references.")]
        [SerializeField] private Bounds bunker, entrance;

        private GameObject poolRoot;
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private readonly int[] gunSequences = new int[(int)AudioCue.CommanderGun + 1];
        private AudioSource waves, wind, room, alarm;
        private AudioSource generatorHum;
        private bool paused, completed, alarmActive, alertPlayed;
        private float alarmDelay, insectDelay;
        private int nextVoice;
        private int footstepPulse, footstepSequence;

        public static GameplayAudio Current { get; private set; }
        public int PlayedCount { get; private set; }
        public AudioCue LastCue { get; private set; }
        public bool GeneratorDown { get; private set; }
        public float AlarmVolume => alarm != null ? alarm.volume : 0f;

        private struct Checkpoint
        {
            public bool Down, AlarmActive, AlertPlayed;
            public float AlarmDelay;
        }
        public object CaptureCheckpoint() => new Checkpoint { Down = GeneratorDown, AlarmActive = alarmActive, AlarmDelay = alarmDelay, AlertPlayed = alertPlayed };
        public void RestoreCheckpoint(object state)
        {
            var saved = (Checkpoint)state;
            GeneratorDown = saved.Down;
            alarmActive = saved.AlarmActive;
            alarmDelay = saved.AlarmDelay;
            alertPlayed = saved.AlertPlayed;
            completed = false;
            StopVoices(); // never replay sounds from the failed attempt or checkpoint restoration
            RefreshAmbience(0f);
        }

        private void Awake()
        {
            if (bank == null)
            {
                Debug.LogError("[Blackout] GameplayAudio needs the prototype bank; run Build Prototype Audio.", this);
                enabled = false;
                return;
            }
            poolRoot = new GameObject("Gameplay Audio Voices");
            SceneManager.MoveGameObjectToScene(poolRoot, gameObject.scene);
            for (int i = 0; i < voices.Length; i++) voices[i] = MakeSource("Voice " + i);
            waves = MakeLoop("Waves", AudioCue.Waves);
            wind = MakeLoop("Wind", AudioCue.Wind);
            room = MakeLoop("Room", AudioCue.Room);
            alarm = MakeLoop("Alarm", AudioCue.Alarm);
        }

        private AudioSource MakeSource(string label)
        {
            var child = new GameObject(label);
            child.transform.SetParent(poolRoot.transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 120f;
            source.dopplerLevel = 0f;
            return source;
        }

        private AudioSource MakeLoop(string label, AudioCue cue)
        {
            AudioSource source = MakeSource(label);
            source.clip = bank != null ? bank.Get(cue) : null;
            source.loop = true;
            source.volume = 0f;
            return source;
        }

        private void OnEnable()
        {
            if (Current != null && Current != this)
            {
                Debug.LogError("[Blackout] Only one GameplayAudio may be enabled.", this);
                enabled = false;
                return;
            }
            Current = this;
            if (noise != null) noise.OnRaised += OnNoise;
            if (generatorDisabled != null) generatorDisabled.OnRaised += OnGenerator;
            if (cureTaken != null) cureTaken.OnRaised += OnCure;
            if (missionComplete != null) missionComplete.OnRaised += OnComplete;
            if (motor != null) { motor.Footstep += OnFootstep; motor.Jumped += OnJump; motor.Landed += OnLand; }
            if (weapons != null) { weapons.HitConfirmed += OnHit; weapons.SwitchStarted += OnSwitch; }
            if (health != null) { health.OnDamaged += OnHurt; health.OnDied += OnDeath; }
            StartLoops();
        }

        private void OnDisable()
        {
            if (Current != this) return;
            if (noise != null) noise.OnRaised -= OnNoise;
            if (generatorDisabled != null) generatorDisabled.OnRaised -= OnGenerator;
            if (cureTaken != null) cureTaken.OnRaised -= OnCure;
            if (missionComplete != null) missionComplete.OnRaised -= OnComplete;
            if (motor != null) { motor.Footstep -= OnFootstep; motor.Jumped -= OnJump; motor.Landed -= OnLand; }
            if (weapons != null) { weapons.HitConfirmed -= OnHit; weapons.SwitchStarted -= OnSwitch; }
            if (health != null) { health.OnDamaged -= OnHurt; health.OnDied -= OnDeath; }
            StopVoices();
            if (waves != null) { waves.Stop(); wind.Stop(); room.Stop(); alarm.Stop(); }
            Current = null;
        }

        private void OnDestroy()
        {
            if (poolRoot != null) Destroy(poolRoot);
        }

        private void StartLoops()
        {
            if (waves == null) return;
            if (generatorHum == null)
                foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
                    if (source.name == "HumAudioSource" && source.clip == bank.Get(AudioCue.GeneratorHum)) { generatorHum = source; break; }
            paused = false;
            waves.Play(); wind.Play(); room.Play(); alarm.Play();
            RefreshAmbience(0f);
        }

        /// <summary>Eight local gun voices, four feedback voices, twenty stationary world voices.</summary>
        public bool Play(AudioCue cue, Vector3 position = default, bool spatial = false, float volume = 1f)
        {
            bool gunshot = PrototypeAudioBank.IsGunshot(cue);
            int sequence = gunshot ? gunSequences[(int)cue] : 0;
            AudioClip clip = bank == null ? null : gunshot ? bank.GetGunshot(cue, (sequence & 1) != 0) : bank.Get(cue);
            float pitch = gunshot ? 1f + (sequence % 3 - 1) * 0.008f : 1f;
            bool played = PlayClip(cue, clip, position, spatial, volume, pitch);
            if (played && gunshot) gunSequences[(int)cue]++;
            return played;
        }

        private bool PlayClip(AudioCue cue, AudioClip clip, Vector3 position, bool spatial, float volume, float pitch)
        {
            if (!isActiveAndEnabled || bank == null || poolRoot == null || (Time.timeScale <= 0f && cue != AudioCue.Complete)) return false;
            if (clip == null) return false;
            bool localGun = !spatial && PrototypeAudioBank.IsGunshot(cue);
            int begin = spatial ? GunVoices + FeedbackVoices : localGun ? 0 : GunVoices;
            int end = spatial ? VoiceCount : localGun ? GunVoices : GunVoices + FeedbackVoices;
            int selected = begin + nextVoice++ % (end - begin);
            for (int i = begin; i < end; i++)
                if (!voices[i].isPlaying) { selected = i; break; }
            AudioSource source = voices[selected];
            source.Stop();
            source.clip = clip;
            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.volume = Mathf.Clamp01(volume);
            source.pitch = pitch;
            source.priority = PrototypeAudioBank.IsGunshot(cue) ? 64 : 128;
            source.ignoreListenerPause = cue == AudioCue.Complete;
            source.Play();
            LastCue = cue;
            PlayedCount++;
            return true;
        }

        private void Update()
        {
            bool nowPaused = Time.timeScale <= 0f;
            if (nowPaused != paused)
            {
                for (int i = 0; i < voices.Length; i++)
                    if (!voices[i].ignoreListenerPause) { if (nowPaused) voices[i].Pause(); else voices[i].UnPause(); }
                if (nowPaused) { waves.Pause(); wind.Pause(); room.Pause(); alarm.Pause(); }
                else { waves.UnPause(); wind.UnPause(); room.UnPause(); alarm.UnPause(); }
                if (generatorHum != null) { if (nowPaused) generatorHum.Pause(); else generatorHum.UnPause(); }
                paused = nowPaused;
            }
            if (!nowPaused) RefreshAmbience(Time.deltaTime);
        }

        public static float Interior(Vector3 position, Bounds underground, Bounds ramp)
        {
            if (underground.Contains(position)) return 1f;
            return ramp.Contains(position) ? Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(ramp.min.z, ramp.max.z, position.z)) : 0f;
        }

        private void RefreshAmbience(float dt)
        {
            if (waves == null) return;
            Vector3 p = transform.position;
            float inside = Interior(p, bunker, entrance);
            float shore = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-40f, -100f, p.z));
            float t = Time.time;
            float outside = 1f - inside;
            float swell = 0.6f + 0.4f * Mathf.Sin(t * 0.55f) * Mathf.Sin(t * 0.21f + 1f);
            waves.volume = completed ? 0f : (0.05f + 0.32f * shore) * swell * outside;
            wind.volume = completed ? 0f : 0.045f * outside * (0.7f + 0.3f * Mathf.Sin(t * 0.3f));
            room.volume = completed ? 0f : 0.06f * inside;
            alarmDelay = Mathf.Max(0f, alarmDelay - dt);
            alarm.volume = !completed && alarmActive && alarmDelay <= 0f ? 0.05f + 0.07f * inside : 0f;
            insectDelay -= dt;
            if (!completed && inside < 0.2f && p.z > -85f && p.z < 28f && insectDelay <= 0f && dt > 0f)
            {
                insectDelay = Random.Range(0.15f, 1.05f);
                Play(AudioCue.Insect, p + new Vector3(Random.Range(-15f, 15f), 1f, Random.Range(-15f, 15f)), true);
            }
        }

        private void OnNoise(NoiseEvent value)
        {
            if (value.type == NoiseType.Gunshot)
            {
                if (value.source == gameObject)
                    Play(weapons != null && weapons.ActiveWeapon != null && weapons.ActiveWeapon.slot == 2 ? AudioCue.Sniper : AudioCue.Smg);
                else if (value.source != null && value.source.TryGetComponent(out ActorAudio actor))
                    Play(actor.GunCue, value.position + Vector3.up * 1.4f, true);
            }
            else if (value.type == NoiseType.Melee && value.source == gameObject) Play(AudioCue.Knife);
            else if (value.type == NoiseType.Crate) Play(AudioCue.Crate, value.position, true);
        }

        private void OnGenerator()
        {
            if (GeneratorDown) return;
            GeneratorDown = true;
            alarmActive = true;
            alarmDelay = 2.5f; // prototype cascade end + emergency delay
            Play(AudioCue.GeneratorStop);
        }
        private void OnCure() { if (!alertPlayed) { alertPlayed = true; Play(AudioCue.Alert); } }
        private void OnComplete() { completed = true; StopVoices(); RefreshAmbience(0f); Play(AudioCue.Complete); }
        private void OnSwitch() => Play(AudioCue.WeaponSwitch);
        private void OnJump() { footstepPulse = 0; Play(AudioCue.Jump); }
        private void OnLand(float speed)
        {
            footstepPulse = 0;
            if (speed > 2.5f) { PlayFootstep(MovementState.Walk); Play(AudioCue.Land); }
        }
        private void OnHit(bool killed) { Play(AudioCue.Hit); if (killed) Play(AudioCue.Kill); }
        private void OnHurt(DamageInfo _) { if (health.IsAlive) Play(AudioCue.PlayerHurt); }
        private void OnDeath() => Play(AudioCue.PlayerDeath);

        private void OnFootstep(MovementState state)
        {
            // The prototype motor emits 5.5/6.7 pulses per second at walk/sprint speed.
            // Voice every other pulse; keep the motor's AI noise events and camera bob intact.
            if ((footstepPulse++ & 1) != 0) return;
            PlayFootstep(state);
        }

        private void PlayFootstep(MovementState state)
        {
            Vector3 p = transform.position;
            AudioCue cue = MissionZones.IsInGeneratorRoom(p) ? AudioCue.StepMetal :
                Interior(p, bunker, entrance) > 0.5f ? AudioCue.StepConcrete :
                p.z < -97f && Mathf.Abs(p.x) < 1.6f && p.y > 1f ? AudioCue.StepWood :
                p.z < -92f ? AudioCue.StepSand : AudioCue.StepDirt;
            float volume = motor != null && motor.Stance != Stance.Standing ? 0.32f : state == MovementState.Sprint ? 1f : 0.714f;
            int variation = footstepSequence++;
            AudioClip clip = bank != null ? bank.GetFootstep(cue, (variation & 1) != 0) : null;
            float pitch = 1f + (variation % 3 - 1) * 0.015f;
            PlayClip(cue, clip, default, false, volume, pitch);
        }

        private void StopVoices()
        {
            footstepPulse = footstepSequence = 0;
            for (int i = 0; i < gunSequences.Length; i++) gunSequences[i] = 0;
            for (int i = 0; i < voices.Length; i++) if (voices[i] != null) voices[i].Stop();
        }
    }
}
