// Owner: Systems Engineer (Hunter AI).
using Blackout.Shared;
using UnityEngine;

namespace Blackout.AI.Hunter
{
    /// <summary>
    /// Every number the Hunter's hearing uses (docs/reference/systems-engineer.md section 14). The values are the
    /// prototype's (agents/perception.js) unless a tooltip says otherwise.
    ///
    /// <para><b>Attenuation formula.</b> For a noise of radius r (metres; <see cref="NoiseEvent.loudness"/>) at distance
    /// d from the Hunter, with <c>walls</c> walls crossed:</para>
    /// <code>perceived = max(0, 1 − d / r) × wallFactor ^ walls × hearing</code>
    /// <para>The noise is heard if <c>perceived &gt; threshold</c>. With the defaults (0.35, 1.7, 0.04) a noise is
    /// heard up to 0.98 r in the open, 0.93 r through one wall, 0.81 r through two and 0.45 r through three or more.</para>
    /// <para><b>Decay.</b> The Hunter remembers the strongest noise; its loudness fades linearly to 0 over
    /// <see cref="memorySeconds"/>: <c>current = perceived × max(0, 1 − age / memorySeconds)</c>.</para>
    /// </summary>
    [CreateAssetMenu(fileName = "HearingConfig", menuName = "Blackout/Hunter/Hearing Config")]
    public sealed class HearingConfig : ScriptableObject
    {
        /// <summary>Level (walls, props) and Interactable (doors): what muffles sound.</summary>
        public const int DefaultOccluders = 1 << PhysicsLayers.Level | 1 << PhysicsLayers.Interactable;

        [Header("Formula: max(0, 1 - d / r) x wallFactor ^ walls x hearing")]
        [Tooltip("The Hunter's hearing multiplier. Its speciality: the prototype gives it 1.7 (Security Patrol 1.0, " +
                 "Tactical Guard and Commander 0.9). A noise right next to the Hunter is perceived at this value.")]
        [Min(0f)] public float hearing = 1.7f;

        [Tooltip("Fraction of the sound that gets through each wall. 0.35 in the prototype: one wall cuts a noise to " +
                 "about a third, two walls to an eighth.")]
        [Range(0f, 1f)] public float wallFactor = 0.35f;

        [Tooltip("Walls counted at most. 3 in the prototype; with 0.35 per wall, a fourth wall would only matter for " +
                 "noises within 0.45 r, and it saves a raycast hit.")]
        [Range(0, 8)] public int maxWalls = 3;

        [Tooltip("A noise is heard only if its perceived loudness is above this. 0.04 in the prototype, so the " +
                 "faintest audible noise is one at 98 % of its radius in the open.")]
        [Min(0f)] public float threshold = 0.04f;

        [Header("Wall raycast (metres above the feet)")]
        [Tooltip("The ray starts this high above the noise's position. Noises are published at the source's feet; " +
                 "1.2 m in the prototype (a gun or a mouth, not the floor).")]
        [Min(0f)] public float sourceHeight = 1.2f;

        [Tooltip("The ray ends this high above the Hunter's feet: its ears. 1.6 m in the prototype.")]
        [Min(0f)] public float listenerHeight = 1.6f;

        [Tooltip("Layers that muffle sound: Level (walls, after Y7) and Interactable (closed doors; an open door slides " +
                 "into the wall). Triggers and the terrain never count.")]
        public LayerMask occluders = DefaultOccluders;

        [Header("Memory")]
        [Tooltip("The remembered noise fades linearly to 0 over this many seconds. 8 s is how long the prototype keeps " +
                 "a heard noise before forgetting it.")]
        [Min(0.1f)] public float memorySeconds = 8f;
    }
}
