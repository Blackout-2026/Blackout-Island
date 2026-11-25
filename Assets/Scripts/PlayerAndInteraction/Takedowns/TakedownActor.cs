using UnityEngine;

namespace Blackout.Takedowns
{
    /// <summary>On the cinematic body's Animator object, so Unity Animation Events reach this receiver.</summary>
    [RequireComponent(typeof(Animator))]
    public sealed class TakedownActor : MonoBehaviour
    {
        [SerializeField] private Transform knife;
        [SerializeField] private Vector3 knifePosition;
        [SerializeField] private Vector3 knifeEuler = new Vector3(0f, 90f, 0f);
        [SerializeField] private Vector3 frontalKnifePosition;
        [SerializeField] private Vector3 frontalKnifeEuler;
        private Transform hand;
        public Animator Animator { get; private set; }
        internal TakedownController Owner { get; set; }

        private void Awake()
        {
            Animator = GetComponent<Animator>();
            if (Animator.isHuman) hand = Animator.GetBoneTransform(HumanBodyBones.RightHand);
        }

        private void LateUpdate()
        {
            if (hand != null && knife != null)
            {
                bool frontal = Owner != null && Owner.Style == TakedownStyle.Frontal;
                knife.SetPositionAndRotation(hand.TransformPoint(frontal ? frontalKnifePosition : knifePosition),
                    hand.rotation * Quaternion.Euler(frontal ? frontalKnifeEuler : knifeEuler));
            }
        }

        // Add these events to the PLAYER clip. Duplicate/late impact callbacks are rejected by the owner.
        public void TakedownImpact() { if (Owner != null) Owner.ApplyImpact(); }
        public void TakedownSound() { if (Owner != null) Owner.PlayAttackSound(); }
        public void TakedownKick() { if (Owner != null) Owner.KickWeapon(); }
        public void TakedownStab(int index) { if (Owner != null) Owner.ApplyStab(index); }
        public void TakedownStabSound(int index) { if (Owner != null) Owner.PlayStabSound(index); }
    }
}
