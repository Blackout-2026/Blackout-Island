// Owner: Systems Engineer.
using System.Collections.Generic;
using Blackout.Player;
using UnityEngine;

namespace Blackout.Weapons
{
    /// <summary>
    /// The player's hands and the weapon in them. Every viewmodel carries the arms as meshes called Glove* and Sleeve* (the downloaded first-person
    /// arms: both hands on the SMG and the rifle, one on the knife and the other beside it), so they are always seen with the weapon, except while
    /// the player sprints: then the whole viewmodel, the hands and the gun or the knife with them, is hidden (step F2: a gun hanging in the air
    /// without hands looked wrong). Only the mesh renderers are switched, so a weapon that is not held stays hidden as before, and nothing else
    /// about the weapons changes.
    /// </summary>
    public class ViewmodelHands : MonoBehaviour
    {
        public const string GlovePrefix = "Glove";
        public const string SleevePrefix = "Sleeve";

        [SerializeField] private PlayerMotor motor;

        private Renderer[] parts;
        private bool[] enabledByDesign; // a renderer that the prefab keeps switched off stays off when the viewmodel comes back
        private bool shown = true;
        private bool scopeHidden;

        /// <summary>True for the mesh objects of the arms: GloveR, GloveL, SleeveR and SleeveL.</summary>
        public static bool IsArmPart(string objectName)
        {
            return !string.IsNullOrEmpty(objectName)
                   && (objectName.StartsWith(GlovePrefix, System.StringComparison.Ordinal)
                       || objectName.StartsWith(SleevePrefix, System.StringComparison.Ordinal));
        }

        /// <summary>The hands and the weapon are shown unless the player is sprinting.</summary>
        public static bool ShouldShow(MovementState state)
        {
            return state != MovementState.Sprint;
        }

        /// <summary>The number of viewmodel renderers found, the arms and every weapon model (a test and the preview use it).</summary>
        public int ArmCount => parts == null ? 0 : parts.Length;

        public bool Shown => shown;

        /// <summary>
        /// Finds the mesh renderers under this object, held weapon or not (the arms and the guns and the knife; the muzzle flash switches itself).
        /// Called again if a weapon model is swapped at run time.
        /// </summary>
        public void Collect()
        {
            var found = new List<Renderer>();
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                {
                    found.Add(renderer);
                }
            }

            parts = found.ToArray();
            enabledByDesign = new bool[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                enabledByDesign[i] = !shown || parts[i].enabled; // while hidden every renderer reads as off
            }

            Apply(!scopeHidden && (motor == null || ShouldShow(motor.State)));
        }

        /// <summary>Hide renderers through the scope while leaving weapon simulation and firing active.</summary>
        public void SetScopeHidden(bool hidden)
        {
            if (scopeHidden == hidden) return;
            scopeHidden = hidden;
            Apply(!hidden && (motor == null || ShouldShow(motor.State)));
        }

        /// <summary>Shows or hides every viewmodel renderer now.</summary>
        public void Apply(bool show)
        {
            shown = show;
            if (parts == null)
            {
                return;
            }

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] != null)
                {
                    parts[i].enabled = show && enabledByDesign[i];
                }
            }
        }

        private void Awake()
        {
            Collect();
        }

        private void LateUpdate()
        {
            if (parts == null || motor == null)
            {
                return;
            }

            bool show = !scopeHidden && ShouldShow(motor.State);
            if (show != shown)
            {
                Apply(show);
            }
        }
    }
}
