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
        public AudioClip Get(AudioCue cue) => clips != null && (int)cue < clips.Length ? clips[(int)cue] : null;
    }
}
