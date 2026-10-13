// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Interaction
{
    /// <summary>
    /// Base class for anything the player uses with E (doors, the generator console). Put it on or above a collider:
    /// the PlayerInteractor raycasts 3 m from the camera and takes the first Interactable it hits.
    /// Subclasses return cached strings, so showing the prompt every frame allocates nothing.
    /// </summary>
    public abstract class Interactable : MonoBehaviour
    {
        /// <summary>Text after the key, for example "Open security door".</summary>
        public abstract string Prompt { get; }

        /// <summary>Key hint, for example "E" or "Hold E". Empty means it cannot be used now (shown, not usable).</summary>
        public virtual string Key => "E";

        /// <summary>Seconds E must be held; 0 means a single press.</summary>
        public virtual float HoldTime => 0f;

        /// <summary>False hides the prompt completely (for example, a generator that is already off).</summary>
        public virtual bool CanInteract => true;

        /// <summary>Called once the press or hold is complete. <paramref name="by"/> is the player.</summary>
        public abstract void Interact(GameObject by);

        /// <summary>Called every frame while a hold is in progress (feedback hooks such as sparks).</summary>
        public virtual void WhileHeld(float progress, float dt)
        {
        }
    }
}
