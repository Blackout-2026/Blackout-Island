// Owner: Systems Engineer.
using System.Collections.Generic;
using Blackout.Player;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The player's hands and forearms. Every viewmodel carries them as meshes called Glove* and Sleeve* (the downloaded first-person arms: both
    /// hands on the SMG and the rifle, one on the knife and the other beside it), so they are always seen with the weapon, except while the player
    /// sprints: then they are hidden and the weapon is held without them. Only the renderers are switched, so a weapon that is not held
    /// stays hidden as before, and nothing else about the weapons changes.
    /// </summary>
    public class ViewmodelHands : MonoBehaviour
    {
        public const string GlovePrefix = "Glove";
        public const string SleevePrefix = "Sleeve";

        [SerializeField] private PlayerMotor motor;

        private Renderer[] arms;
        private bool shown = true;

        /// <summary>True for the mesh objects of the arms: GloveR, GloveL, SleeveR and SleeveL.</summary>
        public static bool IsArmPart(string objectName)
        {
            return !string.IsNullOrEmpty(objectName)
                   && (objectName.StartsWith(GlovePrefix, System.StringComparison.Ordinal)
                       || objectName.StartsWith(SleevePrefix, System.StringComparison.Ordinal));
        }

        /// <summary>The hands are shown unless the player is sprinting.</summary>
        public static bool ShouldShow(MovementState state)
        {
            return state != MovementState.Sprint;
        }

        /// <summary>The number of arm renderers found (a test and the preview use it).</summary>
        public int ArmCount => arms == null ? 0 : arms.Length;

        public bool Shown => shown;

        /// <summary>Finds the arm renderers under this object, held weapon or not. Called again if a weapon model is swapped at run time.</summary>
        public void Collect()
        {
            var found = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (IsArmPart(renderer.gameObject.name))
                {
                    found.Add(renderer);
                }
            }

            arms = found.ToArray();
            Apply(true);
        }

        /// <summary>Shows or hides every arm renderer now.</summary>
        public void Apply(bool show)
        {
            shown = show;
            if (arms == null)
            {
                return;
            }

            foreach (Renderer renderer in arms)
            {
                if (renderer != null)
                {
                    renderer.enabled = show;
                }
            }
        }

        private void Awake()
        {
            Collect();
        }

        private void LateUpdate()
        {
            if (arms == null || motor == null)
            {
                return;
            }

            bool show = ShouldShow(motor.State);
            if (show != shown)
            {
                Apply(show);
            }
        }
    }
}
