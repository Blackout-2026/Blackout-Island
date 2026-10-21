// Owner: Systems Engineer. Like enemy loot, this adapter uses Shared.Health without an Agents assembly reference.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Audio
{
    [DisallowMultipleComponent, RequireComponent(typeof(Health))]
    public sealed class ActorAudio : MonoBehaviour
    {
        [SerializeField] private AudioCue gunCue = AudioCue.PatrolGun;
        [SerializeField] private bool robot;
        private Health health;
        public AudioCue GunCue => gunCue;

        private void OnEnable()
        {
            health = GetComponent<Health>();
            health.OnDamaged += OnHurt;
            health.OnDied += OnDeath;
        }
        private void OnDisable()
        {
            if (health == null) return;
            health.OnDamaged -= OnHurt;
            health.OnDied -= OnDeath;
        }
        private void OnHurt(DamageInfo _)
        {
            if (health.IsAlive) GameplayAudio.Current?.Play(robot ? AudioCue.RobotHurt : AudioCue.HumanHurt, transform.position + Vector3.up * 1.5f, true);
        }
        private void OnDeath() => GameplayAudio.Current?.Play(robot ? AudioCue.RobotDeath : AudioCue.HumanDeath, transform.position + Vector3.up * 1.2f, true);
    }
}
