using UnityEngine;

namespace Blackout.Takedowns
{
    public enum TakedownStyle { Rear, Frontal }
    [CreateAssetMenu(menuName = "Blackout/Knife Takedown Profile")]
    public sealed class TakedownProfile : ScriptableObject
    {
        public TakedownStyle style;
        [Header("Synchronized full-body Animator clips")]
        public AnimationClip playerAttack;
        [Tooltip("Rear: paired reaction including collapse. Frontal: hit reaction; Victim Death supplies the collapse.")]
        public AnimationClip victimReaction;
        [Header("Frontal counterattack only")]
        public AnimationClip kick;
        public AnimationClip victimIdle;
        public AnimationClip victimDeath;
        [Tooltip("Playback rate of the frontal double-stab clip only. Animation Events follow this speed.")]
        [Range(1f, 4f)] public float stabSpeed = 1f;
        [Header("Staging in metres; default is a rear takedown")]
        [Range(0.5f, 3.5f)] public float range = 1.5f;
        [Tooltip("Authored pair spacing; independent of this profile's activation range.")]
        public Vector3 playerOffset = new Vector3(0f, 0f, -0.85f);
        [Tooltip("Extra level ground needed in front of the victim for baked approach/collapse motion.")]
        [Range(0f, 3f)] public float forwardClearance;
        public float playerYaw;
        [Range(0.1f, 1f)] public float alignmentTime = 0.3f;
        [Range(0.1f, 1f)] public float cameraReturnTime = 0.35f;
        [Header("Third-person camera offset relative to the pair")]
        public Vector3 cameraOffset = new Vector3(1.5f, 1.6f, -1.8f);
        public Vector3 cameraEndOffset = new Vector3(1.2f, 1.5f, -1.5f);
        public float focusHeight = 1.15f;
        [Range(35f, 80f)] public float fieldOfView = 55f;
        [Range(0.2f, 1f)] public float timeScale = 1f;

        public float StabDuration => playerAttack.length / stabSpeed;
        public float Duration => style == TakedownStyle.Frontal ? kick.length + StabDuration + victimDeath.length
            : Mathf.Max(playerAttack.length, victimReaction.length);

        public string MissingRequirement
        {
            get
            {
                if (playerAttack == null || victimReaction == null) return "Import and assign a real paired player attack and victim reaction/collapse clip.";
                if (playerAttack.legacy || victimReaction.legacy) return "Use Animator clips, not Legacy Animation clips.";
                if (playerAttack.isLooping || victimReaction.isLooping) return "Both takedown clips must have Loop Time disabled.";
                if (Mathf.Min(playerAttack.length, victimReaction.length) <= 0f || Mathf.Max(playerAttack.length, victimReaction.length) > 12f)
                    return "Use finite paired clips between 0 and 12 seconds.";
                if (playerOffset.sqrMagnitude < 0.64f || playerOffset.magnitude > 2.5f || Mathf.Abs(playerOffset.y) > 0.01f)
                    return "Authored pair spacing must be 0.8m to 2.5m, on the same floor.";
                if (style == TakedownStyle.Frontal) return FrontalRequirements();
                int impacts = 0, sounds = 0;
                foreach (AnimationEvent marker in playerAttack.events)
                {
                    if (marker.functionName == "TakedownImpact" && marker.time > 0f && marker.time < Mathf.Min(playerAttack.length, victimReaction.length)) impacts++;
                    if (marker.functionName == "TakedownSound" && marker.time >= 0f && marker.time < playerAttack.length) sounds++;
                }
                if (impacts != 1) return "Add exactly one TakedownImpact Animation Event inside both clips' shared time range.";
                return sounds == 1 ? null : "Add exactly one TakedownSound Animation Event to the player clip.";
            }
        }

        private string FrontalRequirements()
        {
            if (!(stabSpeed >= 1f && stabSpeed <= 4f)) return "Set frontal stab speed between 1 and 4.";
            if (kick == null || victimIdle == null || victimDeath == null) return "Assign the frontal kick, victim aim and death clips.";
            if (kick.legacy || victimDeath.legacy || kick.isLooping || victimDeath.isLooping || kick.length <= 0f || victimDeath.length <= 0f || Duration > 12f)
                return "Use finite non-looping kick and death clips; the whole frontal sequence must fit within 12 seconds.";
            int kicks = 0, first = 0, second = 0, sound1 = 0, sound2 = 0;
            float firstTime = 0f, secondTime = 0f;
            foreach (AnimationEvent marker in kick.events)
                if (marker.functionName == "TakedownKick" && marker.time > 0f && marker.time < kick.length) kicks++;
            foreach (AnimationEvent marker in playerAttack.events)
            {
                if (marker.time <= 0f || marker.time >= playerAttack.length) continue;
                if (marker.functionName == "TakedownStab" && marker.intParameter == 1) { first++; firstTime = marker.time; }
                if (marker.functionName == "TakedownStab" && marker.intParameter == 2) { second++; secondTime = marker.time; }
                if (marker.functionName == "TakedownStabSound" && marker.intParameter == 1) sound1++;
                if (marker.functionName == "TakedownStabSound" && marker.intParameter == 2) sound2++;
            }
            return kicks == 1 && first == 1 && second == 1 && sound1 == 1 && sound2 == 1 && secondTime > firstTime ? null
                : "Frontal clips need one TakedownKick and two indexed TakedownStab/TakedownStabSound events (1 then 2).";
        }
    }
}
