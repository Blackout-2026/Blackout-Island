using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Poses the downloaded Security Patrol soldier (SM_Char_PatrolSoldier.fbx, step PS4) holding the SCAR-style rifle with both hands, in place of the T-pose it
    /// is modelled in. The FBX is not touched: this writes the pose into the bone Transforms of the visual prefab, so the bind pose stays the T-pose and an
    /// Animator with clips can drive the same bones later. Everything is worked out in a character frame taken from the skeleton itself (right, up, forward),
    /// so it does not depend on how the importer turned the axes: the rifle is placed across the chest, each arm is solved with analytic two-bone IK to a wrist
    /// that puts the palm on its grip, the hand is turned to face the grip, and the fingers are curled round it. Editor-only; deterministic (no randomness).
    /// </summary>
    public static class PatrolSoldierPose
    {
        /// <summary>The bones are called "mott_var01:RightArm" and so on in the FBX.</summary>
        public const string BonePrefix = "mott_var01:";

        // Where the rifle is held, in the character frame (right, up, forward; metres from the point between the feet). The butt is tucked at the right of the
        // chest, a hand-span below the shoulder line, and the muzzle points a little to the character's left and down, like a rifle carried at the low ready.
        // These numbers come from a reach search over the soldier's measured arms (upper arm 0.27 m, forearm 0.28 m): both elbows stay bent (about 100 and 145 degrees).
        public static readonly Vector3 ButtCentre = new Vector3(0.08f, 1.34f, 0.08f);
        public const float RifleYawDegrees = 10f;
        public const float RiflePitchDegrees = 6f;   // the muzzle points this far below level

        // The rifle's own frame (SM_Char_PatrolRifle: its centre line is the origin, +Z the muzzle). The butt pad's centre sits 0.305 m behind the origin and 0.01 m
        // below it, so the origin (the Gun object) is that far in front of the butt.
        public static readonly Vector3 ButtToGun = new Vector3(0f, 0.01f, 0.305f);
        public static readonly Vector3 RightGripLocal = new Vector3(0f, -0.085f, -0.02f);   // the pistol grip
        public static readonly Vector3 LeftGripLocal = new Vector3(0f, -0.055f, 0.095f);    // the magazine well
        public const float RightGripHalfWidth = 0.022f;                                    // the palm rests this far from the grip's axis
        public const float LeftGripHalfWidth = 0.026f;
        public const float FingerPitchDegrees = 10f;                                       // the fingers point a little above the barrel

        // Curl of the three joints of a finger and of the thumb, in degrees, wrapping a grip about 4 cm across.
        private static readonly float[] FingerCurl = { 62f, 80f, 52f };
        private static readonly float[] ThumbCurl = { 16f, 22f, 16f };
        private static readonly string[] Fingers = { "Index", "Middle", "Ring", "Pinky" };

        /// <summary>The character's own axes and its origin at the feet.</summary>
        public readonly struct Frame
        {
            public readonly Vector3 Origin, Right, Up, Forward;

            public Frame(Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
            {
                Origin = origin;
                Right = right;
                Up = up;
                Forward = forward;
            }

            public Vector3 Point(Vector3 rightUpForward)
            {
                return Origin + Right * rightUpForward.x + Up * rightUpForward.y + Forward * rightUpForward.z;
            }
        }

        public static Transform Bone(Transform model, string name)
        {
            Transform bone = RobotRig.FindDeep(model, BonePrefix + name);
            if (bone == null)
            {
                throw new System.InvalidOperationException($"[PatrolSoldierPose] bone {BonePrefix}{name} not found under {model.name}.");
            }

            return bone;
        }

        /// <summary>The frame of a model standing upright: forward is the way its toes point, right follows from that and up.</summary>
        public static Frame FrameOf(Transform model)
        {
            Vector3 up = model.up;
            Vector3 toes = Bone(model, "LeftToeBase").position - Bone(model, "LeftFoot").position;
            Vector3 forward = Vector3.ProjectOnPlane(toes, up).normalized;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            return new Frame(model.position, right, up, forward);
        }

        /// <summary>Where the rifle's origin (its Gun object) goes, and how it is turned, for a character frame.</summary>
        public static void RifleWorldPose(Frame frame, out Vector3 gunPosition, out Quaternion gunRotation)
        {
            float yaw = RifleYawDegrees * Mathf.Deg2Rad;
            float pitch = RiflePitchDegrees * Mathf.Deg2Rad;
            Vector3 level = (frame.Forward * Mathf.Cos(yaw) - frame.Right * Mathf.Sin(yaw)).normalized;
            Vector3 direction = (level * Mathf.Cos(pitch) - frame.Up * Mathf.Sin(pitch)).normalized;
            gunRotation = Quaternion.LookRotation(direction, frame.Up);
            gunPosition = frame.Point(ButtCentre) + gunRotation * ButtToGun;
        }

        /// <summary>The middle of the palm: a little more than half way from the wrist to the base of the middle finger.</summary>
        public static Vector3 PalmCentre(Transform model, string side)
        {
            Transform hand = Bone(model, side + "Hand");
            Transform middle = Bone(model, side + "HandMiddle1");
            return Vector3.Lerp(hand.position, middle.position, 0.6f);
        }

        /// <summary>Poses both arms and hands to the rifle that is already placed at gunPosition / gunRotation (world space).</summary>
        public static void Apply(Transform model, Vector3 gunPosition, Quaternion gunRotation)
        {
            Frame frame = FrameOf(model);
            PoseArm(model, frame, gunPosition, gunRotation, "Right");
            PoseArm(model, frame, gunPosition, gunRotation, "Left");
        }

        private static void PoseArm(Transform model, Frame frame, Vector3 gunPosition, Quaternion gunRotation, string side)
        {
            bool right = side == "Right";
            Transform arm = Bone(model, side + "Arm");
            Transform fore = Bone(model, side + "ForeArm");
            Transform hand = Bone(model, side + "Hand");

            Vector3 rifleForward = gunRotation * Vector3.forward;
            Vector3 rifleUp = gunRotation * Vector3.up;
            Vector3 rifleRight = gunRotation * Vector3.right;

            // The hand: fingers along the rifle (a little raised), the palm facing the grip (the right hand grips from the right, the left from the left).
            Vector3 fingerDir = (rifleForward * Mathf.Cos(FingerPitchDegrees * Mathf.Deg2Rad) + rifleUp * Mathf.Sin(FingerPitchDegrees * Mathf.Deg2Rad)).normalized;
            Vector3 palmDir = Vector3.ProjectOnPlane(right ? -rifleRight : rifleRight, fingerDir).normalized;
            Vector3 gripCentre = gunPosition + gunRotation * (right ? RightGripLocal : LeftGripLocal);
            Vector3 palmCentre = gripCentre - palmDir * (right ? RightGripHalfWidth : LeftGripHalfWidth);

            // The palm's normal as the rest pose has it (palms down in a T-pose), to know which way round the cross product below points.
            float palmSign = PalmSign(model, side, frame);
            float wristToPalm = Vector3.Distance(hand.position, PalmCentre(model, side));
            Vector3 wrist = palmCentre - fingerDir * wristToPalm;

            // The elbow hangs below the arm and a little outward.
            Vector3 pole = (-frame.Up + (right ? frame.Right : -frame.Right) * 0.5f).normalized;
            SolveTwoBone(arm, fore, hand, wrist, pole);
            OrientHand(model, side, fingerDir, palmDir, palmSign);
            CurlFingers(model, side, palmDir);
        }

        // The sign that makes Cross(fingerDirection, acrossTheKnuckles) the palm's normal, found at the rest pose where the palm faces down.
        private static float PalmSign(Transform model, string side, Frame frame)
        {
            Vector3 normal = RawPalmNormal(model, side);
            return Vector3.Dot(normal, -frame.Up) >= 0f ? 1f : -1f;
        }

        private static Vector3 RawPalmNormal(Transform model, string side)
        {
            Vector3 along = (Bone(model, side + "HandMiddle1").position - Bone(model, side + "Hand").position).normalized;
            Vector3 across = (Bone(model, side + "HandIndex1").position - Bone(model, side + "HandPinky1").position).normalized;
            return Vector3.Cross(along, across).normalized;
        }

        /// <summary>The palm's normal of a hand in its current pose (pointing out of the palm).</summary>
        public static Vector3 PalmNormal(Transform model, string side, float palmSign)
        {
            return RawPalmNormal(model, side) * palmSign;
        }

        /// <summary>Analytic two-bone IK: puts the hand at target (or as near as the arm reaches) with the elbow on the pole's side.</summary>
        public static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
        {
            Vector3 shoulder = upper.position;
            float upperLength = Vector3.Distance(upper.position, lower.position);
            float lowerLength = Vector3.Distance(lower.position, end.position);
            Vector3 toTarget = target - shoulder;
            float distance = Mathf.Clamp(toTarget.magnitude, Mathf.Abs(upperLength - lowerLength) + 0.005f, upperLength + lowerLength - 0.005f);
            Vector3 direction = toTarget.normalized;

            float along = (upperLength * upperLength - lowerLength * lowerLength + distance * distance) / (2f * distance);
            float height = Mathf.Sqrt(Mathf.Max(0f, upperLength * upperLength - along * along));
            Vector3 sideways = Vector3.ProjectOnPlane(pole, direction);
            sideways = sideways.sqrMagnitude > 1e-8f ? sideways.normalized : Vector3.up;
            Vector3 elbow = shoulder + direction * along + sideways * height;
            Vector3 wrist = shoulder + direction * distance;

            AimBone(upper, lower.position, elbow);
            AimBone(lower, end.position, wrist);
        }

        // Turns a bone about its own position so that the point `from` (a child of it) lands on the line towards `to`.
        private static void AimBone(Transform bone, Vector3 from, Vector3 to)
        {
            bone.rotation = Quaternion.FromToRotation(from - bone.position, to - bone.position) * bone.rotation;
        }

        private static void OrientHand(Transform model, string side, Vector3 fingerDir, Vector3 palmDir, float palmSign)
        {
            Transform hand = Bone(model, side + "Hand");
            Vector3 along = (Bone(model, side + "HandMiddle1").position - hand.position).normalized;
            hand.rotation = Quaternion.FromToRotation(along, fingerDir) * hand.rotation;
            Vector3 normal = PalmNormal(model, side, palmSign);
            float roll = Vector3.SignedAngle(normal, palmDir, fingerDir);
            hand.rotation = Quaternion.AngleAxis(roll, fingerDir) * hand.rotation;
        }

        private static void CurlFingers(Transform model, string side, Vector3 palmDir)
        {
            foreach (string finger in Fingers)
            {
                Curl(model, side + "Hand" + finger, FingerCurl, palmDir);
            }

            Curl(model, side + "HandThumb", ThumbCurl, palmDir);
        }

        // Bends joints 1 to 3 of a finger towards the palm, each by its own angle (a joint carries the ones after it with it).
        private static void Curl(Transform model, string fingerBoneName, float[] angles, Vector3 palmDir)
        {
            for (int k = 1; k <= 3; k++)
            {
                Transform bone = Bone(model, fingerBoneName + k);
                Transform next = Bone(model, fingerBoneName + (k + 1));
                Vector3 segment = (next.position - bone.position).normalized;
                Vector3 axis = Vector3.Cross(segment, palmDir);
                if (axis.sqrMagnitude < 1e-8f)
                {
                    continue;
                }

                bone.rotation = Quaternion.AngleAxis(angles[k - 1], axis.normalized) * bone.rotation;
            }
        }
    }
}
