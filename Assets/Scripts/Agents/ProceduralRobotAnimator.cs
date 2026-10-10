// Owner: Agent Controller.
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Agents
{
    /// <summary>
    /// Drives the security robot's named joints procedurally instead of an Animator Controller, because the
    /// robot is built from separate rigid parts with the pivot at each joint (docs/reference/core-developer.md
    /// section 3.5), not a skinned rig. Joints are found by name anywhere under this transform
    /// (docs/art-pipeline.md section 11): Pelvis, Head, and the usual Thigh/Shin/Foot/UpperArm/ForeArm pairs
    /// with an _L/_R suffix. A joint that is not on the prefab yet (the robot visual does not exist until C3)
    /// is simply skipped — this component is safe to add before the model lands, and logs once per missing
    /// name so the gap is visible instead of silently wrong.
    /// All rotations are applied about each joint's local X axis (Vector3.right): a single, explicit
    /// convention since no real rig exists yet to dictate one. If C3's joints end up oriented differently,
    /// only this file's SetLocalRotation calls need an axis swap, not the gait math.
    /// Formulas are RobotGaitMath (agent-controller.md 9.1/9.3 and core-developer.md 3.5). Arm walk-swing
    /// (core-developer.md: "swing +/-0.25 rad walking") is not wired — only the aim pose is — to keep this
    /// step's scope to what A3 asks for (walk, run, aim, collapse); noted in docs/viva.
    /// </summary>
    public class ProceduralRobotAnimator : AgentVisualAnimatorBase
    {
        [Header("Joints (found by name anywhere under this transform; docs/art-pipeline.md section 11)")]
        [SerializeField] private string pelvisName = "Pelvis";
        [SerializeField] private string headName = "Head";
        [SerializeField] private string thighLName = "Thigh_L";
        [SerializeField] private string thighRName = "Thigh_R";
        [SerializeField] private string shinLName = "Shin_L";
        [SerializeField] private string shinRName = "Shin_R";
        [SerializeField] private string footLName = "Foot_L";
        [SerializeField] private string footRName = "Foot_R";
        [SerializeField] private string upperArmLName = "UpperArm_L";
        [SerializeField] private string upperArmRName = "UpperArm_R";
        [SerializeField] private string foreArmLName = "ForeArm_L";
        [SerializeField] private string foreArmRName = "ForeArm_R";

        [Header("Aim pose (agent-controller.md 9.3: right arm/forearm raise to aim the weapon pod)")]
        [SerializeField] private float aimUpperArmRRadians = -1.5f;
        [SerializeField] private float aimForeArmLRadians = -1.1f;
        [SerializeField] private float aimForeArmRRadians = -0.08f;

        private Transform pelvis, head, thighL, thighR, shinL, shinR, footL, footR, upperArmL, upperArmR, foreArmL, foreArmR;
        private Quaternion restPelvis, restHead, restThighL, restThighR, restShinL, restShinR, restFootL, restFootR, restUpperArmL, restUpperArmR, restForeArmL, restForeArmR;
        private Vector3 restPelvisLocalPosition;

        private IAgentVelocitySource velocitySource;
        private float gaitPhase;
        private float aimBlend;
        private bool aiming;

        protected override void Awake()
        {
            base.Awake();
            Configure(GetComponentInParent<IAgentVelocitySource>());
        }

        /// <summary>Finds the named joints and caches their rest pose. Awake calls it for the real agent; tests call it directly since Awake does not run in EditMode. Safe to call again if the hierarchy changes (re-binds every joint).</summary>
        public void Configure(IAgentVelocitySource newVelocitySource)
        {
            velocitySource = newVelocitySource;
            BindJoints();
        }

        private void BindJoints()
        {
            pelvis = Bind(pelvisName);
            head = Bind(headName);
            thighL = Bind(thighLName);
            thighR = Bind(thighRName);
            shinL = Bind(shinLName);
            shinR = Bind(shinRName);
            footL = Bind(footLName);
            footR = Bind(footRName);
            upperArmL = Bind(upperArmLName);
            upperArmR = Bind(upperArmRName);
            foreArmL = Bind(foreArmLName);
            foreArmR = Bind(foreArmRName);

            restPelvis = RestRotationOf(pelvis);
            restHead = RestRotationOf(head);
            restThighL = RestRotationOf(thighL);
            restThighR = RestRotationOf(thighR);
            restShinL = RestRotationOf(shinL);
            restShinR = RestRotationOf(shinR);
            restFootL = RestRotationOf(footL);
            restFootR = RestRotationOf(footR);
            restUpperArmL = RestRotationOf(upperArmL);
            restUpperArmR = RestRotationOf(upperArmR);
            restForeArmL = RestRotationOf(foreArmL);
            restForeArmR = RestRotationOf(foreArmR);
            restPelvisLocalPosition = pelvis != null ? pelvis.localPosition : Vector3.zero;
        }

        private Transform Bind(string jointName)
        {
            Transform found = FindDeep(transform, jointName);
            if (found == null)
            {
                Debug.LogWarning($"{name}: ProceduralRobotAnimator found no child named '{jointName}' (docs/art-pipeline.md section 11). Waiting for the robot visual (C3); this joint stays still until it exists.", this);
            }

            return found;
        }

        private static Transform FindDeep(Transform root, string childName)
        {
            Transform direct = root.Find(childName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), childName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private static Quaternion RestRotationOf(Transform t)
        {
            return t != null ? t.localRotation : Quaternion.identity;
        }

        protected override void OnApply(in AgentIntent intent)
        {
            aiming = intent.aim;
        }

        // The prototype gives the robot no hit-react pose of its own (only the generic rig.hit decay in
        // agent-controller.md 9.2, which is not translated to a specific robot pose there); left as a no-op
        // until one is specified.
        protected override void OnHit(in DamageInfo info)
        {
        }

        // The death pose is purely a function of elapsed time, computed every frame in OnDeathTick; there is
        // nothing to kick off at the instant death begins.
        protected override void OnDeathBegin()
        {
        }

        protected override void OnLiveTick(float dt)
        {
            aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, RobotGaitMath.AimDampRate * dt);

            float speed = velocitySource != null ? velocitySource.Speed : 0f;
            float walk = RobotGaitMath.WalkWeight(speed);
            float run = RobotGaitMath.RunWeight(speed);
            gaitPhase = RobotGaitMath.AdvancePhase(gaitPhase, speed * dt, speed);

            // The right leg is simply the left leg's formula half a stride later (phase + PI).
            float thighLAngle = RobotGaitMath.ThighSwing(gaitPhase, walk, run);
            float thighRAngle = RobotGaitMath.ThighSwing(gaitPhase + Mathf.PI, walk, run);
            float shinLAngle = RobotGaitMath.ShinBend(gaitPhase, walk, run);
            float shinRAngle = RobotGaitMath.ShinBend(gaitPhase + Mathf.PI, walk, run);
            float stomp = RobotGaitMath.PelvisStomp(gaitPhase, walk, run);

            SetLocalRotation(thighL, restThighL, thighLAngle);
            SetLocalRotation(thighR, restThighR, thighRAngle);
            SetLocalRotation(shinL, restShinL, shinLAngle);
            SetLocalRotation(shinR, restShinR, shinRAngle);
            SetLocalRotation(footL, restFootL, RobotGaitMath.FootCounter(thighLAngle, shinLAngle));
            SetLocalRotation(footR, restFootR, RobotGaitMath.FootCounter(thighRAngle, shinRAngle));

            if (pelvis != null)
            {
                pelvis.localPosition = restPelvisLocalPosition + new Vector3(0f, stomp, 0f);
            }

            SetLocalRotation(upperArmR, restUpperArmR, Mathf.Lerp(0f, aimUpperArmRRadians, aimBlend));
            SetLocalRotation(foreArmL, restForeArmL, Mathf.Lerp(0f, aimForeArmLRadians, aimBlend));
            SetLocalRotation(foreArmR, restForeArmR, Mathf.Lerp(0f, aimForeArmRRadians, aimBlend));
        }

        protected override void OnDeathTick(float secondsSinceDeath)
        {
            SetLocalRotation(pelvis, restPelvis, RobotGaitMath.DeathPelvisPitch(secondsSinceDeath));
            if (pelvis != null)
            {
                pelvis.localPosition = restPelvisLocalPosition - new Vector3(0f, RobotGaitMath.DeathPelvisDrop(secondsSinceDeath), 0f);
            }

            SetLocalRotation(head, restHead, RobotGaitMath.DeathHeadPitch(secondsSinceDeath));

            float thighBuckle = RobotGaitMath.DeathThighBuckle(secondsSinceDeath);
            float shinBuckle = RobotGaitMath.DeathShinBuckle(secondsSinceDeath);
            SetLocalRotation(thighL, restThighL, thighBuckle);
            SetLocalRotation(thighR, restThighR, thighBuckle);
            SetLocalRotation(shinL, restShinL, shinBuckle);
            SetLocalRotation(shinR, restShinR, shinBuckle);
        }

        private static void SetLocalRotation(Transform t, Quaternion rest, float angleRadians)
        {
            if (t == null)
            {
                return;
            }

            t.localRotation = rest * Quaternion.AngleAxis(angleRadians * Mathf.Rad2Deg, Vector3.right);
        }
    }
}
