// Owner: Systems Engineer. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
namespace Blackout.Shared
{
    /// <summary>
    /// Anything weapons can hurt (player, agents, breakable props). Weapons only talk to this interface,
    /// so they never need to know which kind of object they hit.
    /// </summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(DamageInfo info);
    }
}
