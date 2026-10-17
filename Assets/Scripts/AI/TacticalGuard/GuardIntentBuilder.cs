// Owner: Core Developer (Tactical Guard AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.TacticalGuard
{
    /// <summary>
    /// Turns the winning action into an AgentIntent (prototype "Action execution"). Pure functions of their
    /// arguments, so each action's behaviour is tested without a scene. The mover, animator and shooter carry
    /// the intent out; nothing here touches a Transform.
    /// </summary>
    public static class GuardIntentBuilder
    {
        /// <summary>Stand, face the player's chest, aim and fire.</summary>
        public static AgentIntent Attack(Vector3 playerChest)
        {
            return new AgentIntent
            {
                moveSpeed = MoveSpeed.Stop,
                lookTarget = playerChest,
                hasLookTarget = true,
                aim = true,
                fire = true
            };
        }

        /// <summary>
        /// Run to the cover point, shooting on the way if the player is visible; once there, stop and keep covering the
        /// player. (AgentIntent has no crouch flag yet, so "crouch at cover" is the Agent Controller's to add.)
        /// </summary>
        public static AgentIntent MoveToCover(Vector3 coverPoint, float flatDistanceToCover, bool canSeePlayer,
            Vector3 lookTarget, bool hasLookTarget, TacticalGuardConfig config)
        {
            bool arrived = flatDistanceToCover <= config.coverArriveDistance;
            return new AgentIntent
            {
                moveTarget = coverPoint,
                moveSpeed = arrived ? MoveSpeed.Stop : MoveSpeed.Run,
                lookTarget = lookTarget,
                hasLookTarget = hasLookTarget,
                aim = canSeePlayer,
                fire = canSeePlayer
            };
        }

        /// <summary>Run to the search point without aiming (face the way it runs); on arrival stop and look at the last known position.</summary>
        public static AgentIntent Reposition(Vector3 target, float flatDistanceToTarget, Vector3 lastKnown,
            bool hasLastKnown, TacticalGuardConfig config)
        {
            if (flatDistanceToTarget > config.repositionArriveDistance)
            {
                return new AgentIntent { moveTarget = target, moveSpeed = MoveSpeed.Run };
            }

            return new AgentIntent { moveSpeed = MoveSpeed.Stop, lookTarget = lastKnown, hasLookTarget = hasLastKnown };
        }

        /// <summary>Walk back to the post; standing on it, stop and sweep the head around it.</summary>
        public static AgentIntent HoldPost(Vector3 post, float flatDistanceToPost, float time, TacticalGuardConfig config)
        {
            if (flatDistanceToPost > config.postArriveDistance)
            {
                return new AgentIntent { moveTarget = post, moveSpeed = MoveSpeed.Walk };
            }

            return new AgentIntent
            {
                moveSpeed = MoveSpeed.Stop,
                lookTarget = GuardMath.ScanPoint(post, time, config),
                hasLookTarget = true
            };
        }
    }
}
