// Owner: Systems Engineer. AU1: offline recipes from prototype/src/audio/audio.js.
using UnityEngine;

namespace Blackout.Audio
{
    public enum AudioCue
    {
        Smg, Sniper, Knife, PatrolGun, HunterGun, GuardGun, CommanderGun,
        StepSand, StepGrass, StepDirt, StepMetal, StepWood, StepConcrete,
        Jump, Land, Hit, Kill, PlayerHurt, PlayerDeath, HumanHurt, HumanDeath, RobotHurt, RobotDeath,
        Pickup, WeaponSwitch, DoorSlide, DoorHeavy, DoorHingeOpen, DoorHingeClose, DoorLocked,
        Crate, GeneratorStop, HoldTick, Alert, Complete, Insect, Waves, Wind, Room, Alarm, GeneratorHum
    }

    [CreateAssetMenu(menuName = "Blackout/Audio/Prototype Bank")]
    public sealed class PrototypeAudioBank : ScriptableObject
    {
        [Tooltip("One baked clip per AudioCue, in enum order. Filled by Build Prototype Audio.")]
        [SerializeField] private AudioClip[] clips;
        [Tooltip("Second recorded footfall for sand, grass, dirt, metal, wood and concrete, in that order.")]
        [SerializeField] private AudioClip[] footstepAlternates;
        [Tooltip("Second recorded gunshot per cue index, through CommanderGun; the Knife slot stays empty.")]
        [SerializeField] private AudioClip[] gunshotAlternates;
        public AudioClip Get(AudioCue cue) => clips != null && (int)cue < clips.Length ? clips[(int)cue] : null;

        public AudioClip GetFootstep(AudioCue cue, bool alternate)
        {
            int index = (int)cue - (int)AudioCue.StepSand;
            return alternate && index >= 0 && footstepAlternates != null && index < footstepAlternates.Length &&
                footstepAlternates[index] != null ? footstepAlternates[index] : Get(cue);
        }

        public static bool IsGunshot(AudioCue cue) => cue == AudioCue.Smg || cue == AudioCue.Sniper ||
            (cue >= AudioCue.PatrolGun && cue <= AudioCue.CommanderGun);

        public AudioClip GetGunshot(AudioCue cue, bool alternate)
        {
            int index = (int)cue;
            return alternate && IsGunshot(cue) && gunshotAlternates != null && index < gunshotAlternates.Length &&
                gunshotAlternates[index] != null ? gunshotAlternates[index] : Get(cue);
        }
    }
}
