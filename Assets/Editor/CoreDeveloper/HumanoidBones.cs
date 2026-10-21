using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>A character's own axes and its origin at the feet (worked out from the skeleton, see <see cref="HumanoidRiflePose.FrameOf"/>).</summary>
    public readonly struct HumanoidFrame
    {
        public readonly Vector3 Origin, Right, Up, Forward;

        public HumanoidFrame(Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
        {
            Origin = origin;
            Right = right;
            Up = up;
            Forward = forward;
        }

        /// <summary>A point given as (right, up, forward) metres from the feet.</summary>
        public Vector3 Point(Vector3 rightUpForward)
        {
            return Origin + Right * rightUpForward.x + Up * rightUpForward.y + Forward * rightUpForward.z;
        }
    }

    /// <summary>
    /// The bone names of one downloaded skeleton, so the same rifle pose (<see cref="HumanoidRiflePose"/>) can pose the Mixamo soldier of the Security Patrol and
    /// the Valve-biped Hunter. A side is "Left" or "Right"; a finger is "Thumb", "Index", "Middle", "Ring" or "Pinky".
    /// </summary>
    public abstract class HumanoidBones
    {
        public abstract string HipsName { get; }
        public abstract string UpperArmName(string side);
        public abstract string ForeArmName(string side);
        public abstract string HandName(string side);
        public abstract string FootName(string side);
        public abstract string ToeName(string side);

        /// <summary>The bones of a finger from its base to its tip, in order.</summary>
        public abstract string[] FingerNames(string side, string finger);

        /// <summary>How many joints of a finger bend: three, but the Mixamo chain has a fourth, end bone after them.</summary>
        public abstract int CurledJoints { get; }

        /// <summary>The way the palm faces in the rest pose, to tell which way round the palm normal's cross product points.</summary>
        public abstract Vector3 RestPalmDirection(HumanoidFrame frame, string side);

        public Transform Find(Transform model, string boneName)
        {
            Transform bone = RobotRig.FindDeep(model, boneName);
            if (bone == null)
            {
                throw new System.InvalidOperationException($"[HumanoidBones] bone {boneName} not found under {model.name}.");
            }

            return bone;
        }

        public Transform Hips(Transform model) => Find(model, HipsName);
        public Transform UpperArm(Transform model, string side) => Find(model, UpperArmName(side));
        public Transform ForeArm(Transform model, string side) => Find(model, ForeArmName(side));
        public Transform Hand(Transform model, string side) => Find(model, HandName(side));
        public Transform Foot(Transform model, string side) => Find(model, FootName(side));
        public Transform Toe(Transform model, string side) => Find(model, ToeName(side));
        public Transform Finger(Transform model, string side, string finger, int index) => Find(model, FingerNames(side, finger)[index]);
    }

    /// <summary>The Mixamo skeleton of the downloaded soldier: "mott_var01:RightHandIndex3", a four-bone finger whose last bone is the tip, palms down in the T-pose.</summary>
    public sealed class MixamoBones : HumanoidBones
    {
        private const string Prefix = "mott_var01:";

        public override string HipsName => Prefix + "Hips";
        public override string UpperArmName(string side) => Prefix + side + "Arm";
        public override string ForeArmName(string side) => Prefix + side + "ForeArm";
        public override string HandName(string side) => Prefix + side + "Hand";
        public override string FootName(string side) => Prefix + side + "Foot";
        public override string ToeName(string side) => Prefix + side + "ToeBase";
        public override int CurledJoints => 3;

        public override string[] FingerNames(string side, string finger)
        {
            return new[] { Prefix + side + "Hand" + finger + "1", Prefix + side + "Hand" + finger + "2", Prefix + side + "Hand" + finger + "3", Prefix + side + "Hand" + finger + "4" };
        }

        public override Vector3 RestPalmDirection(HumanoidFrame frame, string side)
        {
            return -frame.Up; // palms face the floor in a T-pose
        }
    }

    /// <summary>The Valve biped skeleton of the Left 4 Dead Hunter: "ValveBiped.Bip01_R_Finger21", a three-bone finger (Finger0 is the thumb, 1 the index, 4 the little finger).</summary>
    public sealed class ValveBones : HumanoidBones
    {
        private const string Prefix = "ValveBiped.Bip01_";

        private static string Letter(string side) => side == "Left" ? "L" : "R";

        public override string HipsName => Prefix + "Pelvis";
        public override string UpperArmName(string side) => Prefix + Letter(side) + "_UpperArm";
        public override string ForeArmName(string side) => Prefix + Letter(side) + "_Forearm";
        public override string HandName(string side) => Prefix + Letter(side) + "_Hand";
        public override string FootName(string side) => Prefix + Letter(side) + "_Foot";
        public override string ToeName(string side) => Prefix + Letter(side) + "_Toe0";
        public override int CurledJoints => 3;

        public override string[] FingerNames(string side, string finger)
        {
            int number = finger == "Thumb" ? 0 : finger == "Index" ? 1 : finger == "Middle" ? 2 : finger == "Ring" ? 3 : 4;
            string stem = Prefix + Letter(side) + "_Finger" + number;
            return new[] { stem, stem + "1", stem + "2" };
        }

        public override Vector3 RestPalmDirection(HumanoidFrame frame, string side)
        {
            // the arms hang at the sides with the palms turned in, towards the thighs
            return side == "Right" ? -frame.Right : frame.Right;
        }
    }
}
