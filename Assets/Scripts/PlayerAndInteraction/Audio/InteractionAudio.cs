// Owner: Systems Engineer. Persistent UnityEvent hooks call this adapter; no cross-scene Inspector references.
using UnityEngine;
using Blackout.Interaction;

namespace Blackout.Audio
{
    [DisallowMultipleComponent]
    public sealed class InteractionAudio : MonoBehaviour
    {
        public void DoorMove()
        {
            bool heavy = TryGetComponent(out SlidingDoor door) && door.SlideSeconds > 1.5f;
            GameplayAudio.Current?.Play(heavy ? AudioCue.DoorHeavy : AudioCue.DoorSlide, transform.position, true);
        }
        public void DoorLocked() => GameplayAudio.Current?.Play(AudioCue.DoorLocked);
        public void Pickup() => GameplayAudio.Current?.Play(AudioCue.Pickup);
        public void HoldTick() => GameplayAudio.Current?.Play(AudioCue.HoldTick);
    }
}
