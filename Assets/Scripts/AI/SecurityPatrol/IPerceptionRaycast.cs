// Owner: World Builder (Security Patrol AI).
using UnityEngine;

namespace Blackout.AI.SecurityPatrol
{
    /// <summary>A geometry query between two world positions. True means sight is blocked.</summary>
    public interface IPerceptionRaycast
    {
        bool IsBlocked(Vector3 origin, Vector3 target);
    }
}
