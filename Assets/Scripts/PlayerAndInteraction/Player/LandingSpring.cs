// Owner: Systems Engineer.
using UnityEngine;

namespace Blackout.Player
{
    /// <summary>
    /// Landing shake as a damped spring: a hard landing kicks it down, then it springs back with one small
    /// rebound (damping ratio 14 / (2√120) ≈ 0.64). The camera applies <see cref="Offset"/> as a dip and a nod.
    /// </summary>
    public sealed class LandingSpring
    {
        private readonly PlayerMovementSettings settings;
        private float velocity;

        public LandingSpring(PlayerMovementSettings settings)
        {
            this.settings = settings;
        }

        /// <summary>Spring displacement, negative while the camera dips.</summary>
        public float Offset { get; private set; }

        /// <summary>Kicks the spring for a landing. Returns false if the landing was too soft to notice.</summary>
        public bool Impact(float landingSpeed)
        {
            if (landingSpeed <= settings.landingMinSpeed)
            {
                return false;
            }

            velocity -= Mathf.Min(settings.landingMaxKick, landingSpeed * settings.landingKickPerSpeed);
            return true;
        }

        public void Update(float dt)
        {
            // Semi-implicit Euler: velocity first, then position. Stable while dt × √stiffness < 2 (dt < 0.18 s).
            velocity += (-Offset * settings.landingStiffness - velocity * settings.landingDamping) * dt;
            Offset += velocity * dt;
        }
    }
}
