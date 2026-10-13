using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One joint of the security robot: an empty (or mesh) object whose origin is the rotation centre.</summary>
    public readonly struct RobotJoint
    {
        public readonly string Name;
        public readonly string Parent; // null for the root joint, which sits directly under the model
        public readonly Vector3 Offset; // local position in metres, relative to the parent joint (rest pose)

        public RobotJoint(string name, string parent, Vector3 offset)
        {
            Name = name;
            Parent = parent;
            Offset = offset;
        }
    }

    /// <summary>
    /// The joint hierarchy, part names and socket names of the security robot (docs/art-pipeline.md section 11).
    /// One table feeds the placeholder model, the check on the real FBX and the documentation.
    /// Offsets are the prototype's bone offsets (docs/reference/core-developer.md 3.2; prototype/src/models/robot.js)
    /// with X mirrored (Unity = -x, y, z, section 0 of that document), so the robot's right arm, which carries the
    /// weapon pod, is at +X. Every joint rests at rotation (0, 0, 0).
    /// </summary>
    public static class RobotRig
    {
        public const string Pelvis = "Pelvis";
        public const string Spine = "Spine";
        public const string Chest = "Chest";
        public const string Head = "Head";
        public const string ThighL = "Thigh_L";
        public const string ThighR = "Thigh_R";
        public const string ShinL = "Shin_L";
        public const string ShinR = "Shin_R";
        public const string FootL = "Foot_L";
        public const string FootR = "Foot_R";
        public const string UpperArmL = "UpperArm_L";
        public const string UpperArmR = "UpperArm_R";
        public const string ForeArmL = "ForeArm_L";
        public const string ForeArmR = "ForeArm_R";
        public const string HandL = "Hand_L";
        public const string HandR = "Hand_R";

        public const string ScannerDot = "ScannerDot";
        public const string WeaponSocket = "WeaponSocket";
        public const string MuzzlePoint = "MuzzlePoint";

        // The muzzle is 0.33 m below the right wrist, between the two barrels (core-developer.md 3.3).
        public static readonly Vector3 MuzzleOffset = new Vector3(0f, -0.33f, 0f);

        // The barrels hang along the hand's -Y axis. Turning a socket 90 degrees about X makes its +Z (forward) point
        // down the barrels, so Transform.forward of the muzzle is the direction the robot shoots.
        public static readonly Quaternion SocketRotation = Quaternion.Euler(90f, 0f, 0f);

        /// <summary>Parents come before their children, so a hierarchy can be built in one pass.</summary>
        public static readonly RobotJoint[] Joints =
        {
            new RobotJoint(Pelvis, null, new Vector3(0f, 1.05f, 0f)),
            new RobotJoint(Spine, Pelvis, new Vector3(0f, 0.14f, 0f)),
            new RobotJoint(Chest, Spine, new Vector3(0f, 0.30f, 0f)),
            new RobotJoint(Head, Chest, new Vector3(0f, 0.44f, 0.02f)),
            new RobotJoint(UpperArmL, Chest, new Vector3(-0.37f, 0.27f, 0f)),
            new RobotJoint(ForeArmL, UpperArmL, new Vector3(0f, -0.34f, 0f)),
            new RobotJoint(HandL, ForeArmL, new Vector3(0f, -0.32f, 0f)),
            new RobotJoint(UpperArmR, Chest, new Vector3(0.37f, 0.27f, 0f)),
            new RobotJoint(ForeArmR, UpperArmR, new Vector3(0f, -0.34f, 0f)),
            new RobotJoint(HandR, ForeArmR, new Vector3(0f, -0.32f, 0f)),
            new RobotJoint(ThighL, Pelvis, new Vector3(-0.17f, -0.06f, 0f)),
            new RobotJoint(ShinL, ThighL, new Vector3(0f, -0.42f, 0.13f)),
            new RobotJoint(FootL, ShinL, new Vector3(0f, -0.43f, -0.15f)),
            new RobotJoint(ThighR, Pelvis, new Vector3(0.17f, -0.06f, 0f)),
            new RobotJoint(ShinR, ThighR, new Vector3(0f, -0.42f, 0.13f)),
            new RobotJoint(FootR, ShinR, new Vector3(0f, -0.43f, -0.15f)),
        };

        /// <summary>
        /// The joints the Agent Controller's ProceduralRobotAnimator rotates or moves, found by exact name. A test
        /// compares this list with the names serialized on that component, so a rename on either side fails loudly.
        /// </summary>
        public static readonly string[] AnimatedJoints =
        {
            Pelvis, Head, ThighL, ThighR, ShinL, ShinR, FootL, FootR, UpperArmL, UpperArmR, ForeArmL, ForeArmR,
        };

        /// <summary>Every joint name the FBX must contain: the animated joints plus Spine, Chest and both hands.</summary>
        public static string[] RequiredJoints()
        {
            var names = new string[Joints.Length];
            for (int i = 0; i < Joints.Length; i++)
            {
                names[i] = Joints[i].Name;
            }

            return names;
        }

        /// <summary>The first object called <paramref name="objectName"/> under <paramref name="root"/> (depth first), or null.</summary>
        public static Transform FindDeep(Transform root, string objectName)
        {
            Transform direct = root.Find(objectName);
            if (direct != null)
            {
                return direct;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                Transform found = FindDeep(root.GetChild(i), objectName);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }
    }
}
