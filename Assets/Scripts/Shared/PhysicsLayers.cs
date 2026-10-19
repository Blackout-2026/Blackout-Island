// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
using UnityEngine;

namespace Blackout.Shared
{
    /// <summary>
    /// The physics layers defined in P0 (ProjectSettings/TagManager.asset) and the masks physics queries use
    /// (docs/reference/systems-engineer.md section 12). Constants, so no query looks a layer name up at runtime;
    /// EditMode tests fail if the project settings stop matching them.
    /// </summary>
    public static class PhysicsLayers
    {
        public const int Default = 0;
        public const int TransparentFX = 1;
        public const int IgnoreRaycast = 2;
        public const int Water = 4;
        public const int UI = 5;
        public const int Player = 6;
        public const int Enemy = 7;
        public const int Level = 8;
        public const int Interactable = 9;
        public const int Hit = 10;
        public const int Ragdoll = 11;

        /// <summary>
        /// What the player's bullets and knife hit: the world (Default until the level moves to Level), agents, doors
        /// and the crate, and hitboxes. Not the player, ragdolls, UI, Water, TransparentFX or Ignore Raycast.
        /// </summary>
        public const int PlayerShots = 1 << Default | 1 << Level | 1 << Enemy | 1 << Interactable | 1 << Hit;

        /// <summary>
        /// What the agents' bullets should hit: the world, the player, doors and the crate. Not other agents or
        /// their hitboxes, because the prototype has no friendly fire.
        /// </summary>
        public const int AgentShots = 1 << Default | 1 << Level | 1 << Player | 1 << Interactable;

        /// <summary>True if the layer is in the mask.</summary>
        public static bool Contains(int mask, int layer)
        {
            return (mask & (1 << layer)) != 0;
        }

        /// <summary>
        /// The layers a body on this layer collides with, read from the collision matrix. For queries that must agree
        /// with the physics, such as a ground probe: it should not find what the body would pass through. Loops over
        /// the 32 layers, so call it once (in Awake), not per frame.
        /// </summary>
        public static int CollisionMask(int layer)
        {
            int mask = 0;
            for (int other = 0; other < 32; other++)
            {
                if (!Physics.GetIgnoreLayerCollision(layer, other))
                {
                    mask |= 1 << other;
                }
            }

            return mask;
        }
    }
}
