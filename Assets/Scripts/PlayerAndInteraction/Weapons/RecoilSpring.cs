// Owner: Systems Engineer.
namespace Blackout.Weapons
{
    /// <summary>
    /// A damped spring for the viewmodel kick: each shot adds velocity, the spring pulls it back to rest.
    /// Same integration as the player's LandingSpring (semi-implicit Euler), with the prototype's constants.
    /// </summary>
    public sealed class RecoilSpring
    {
        private readonly float stiffness;
        private readonly float damping;
        private float velocity;

        public RecoilSpring(float stiffness, float damping)
        {
            this.stiffness = stiffness;
            this.damping = damping;
        }

        public float Offset { get; private set; }

        public void Kick(float addedVelocity)
        {
            velocity += addedVelocity;
        }

        public void Update(float dt)
        {
            velocity += (-Offset * stiffness - velocity * damping) * dt;
            Offset += velocity * dt;
        }
    }
}
