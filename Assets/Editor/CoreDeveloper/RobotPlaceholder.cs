using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    public enum RobotPaint
    {
        Body,
        Joint,
        Accent,
        Eye,
        Cells,
    }

    public enum RobotShape
    {
        Box,
        CylinderY, // axis along the joint's Y (limbs, pistons, barrels)
        CylinderX, // axis along the joint's X (knee, elbow and ankle pins)
        Sphere,
    }

    /// <summary>One primitive of the placeholder: a shape, its size and where it hangs off its joint.</summary>
    public readonly struct RobotPart
    {
        public readonly string Joint;
        public readonly string Name;
        public readonly RobotShape Shape;
        public readonly Vector3 Position;
        public readonly Vector3 Size; // bounding extents in the joint's own axes, before the tilt
        public readonly float TiltDegrees; // about the joint's X axis; the digitigrade legs are modelled sloped, not rotated
        public readonly RobotPaint Paint;

        public RobotPart(string joint, string name, RobotShape shape, Vector3 position, Vector3 size, float tiltDegrees, RobotPaint paint)
        {
            Joint = joint;
            Name = name;
            Shape = shape;
            Position = position;
            Size = size;
            TiltDegrees = tiltDegrees;
            Paint = paint;
        }
    }

    /// <summary>
    /// A primitive stand-in for SM_SecurityRobot.fbx: the real joint hierarchy (<see cref="RobotRig"/>) with boxes, cylinders
    /// and spheres at the prototype's part positions and sizes (docs/reference/core-developer.md 3.2 and 3.3;
    /// prototype/src/models/robot.js), so the Agent Controller can animate and test against it before the Blender model
    /// exists. Rounded boxes are plain boxes. Every part hangs off a joint object, and no joint carries a mesh.
    /// X is mirrored (Unity = -x, y, z): the left arm is at -X and the weapon pod is on the right arm at +X.
    /// </summary>
    public static class RobotPlaceholder
    {
        private const float Right = 1f;
        private const float Left = -1f;

        /// <summary>Creates every joint under <paramref name="model"/> and hangs all parts off them.</summary>
        public static void Build(Transform model, RobotMaterialSet materials)
        {
            var joints = new Dictionary<string, Transform>();
            foreach (RobotJoint joint in RobotRig.Joints)
            {
                Transform t = new GameObject(joint.Name).transform;
                t.SetParent(joint.Parent == null ? model : joints[joint.Parent], false);
                t.localPosition = joint.Offset;
                joints[joint.Name] = t;
            }

            foreach (RobotPart part in Parts())
            {
                AddPart(joints[part.Joint], part, materials);
            }
        }

        public static List<RobotPart> Parts()
        {
            var parts = new List<RobotPart>(100);

            // Pelvis and hip actuators.
            parts.Add(Box(RobotRig.Pelvis, "PelvisBlock", V(0f, 0f, 0f), V(0.46f, 0.2f, 0.32f), RobotPaint.Joint));
            parts.Add(Box(RobotRig.Pelvis, "PelvisCap", V(0f, 0.1f, 0f), V(0.36f, 0.08f, 0.34f), RobotPaint.Body));
            parts.Add(CylinderX(RobotRig.Pelvis, "HipActuator_L", V(0.2f * Left, -0.04f, 0f), 0.085f, 0.1f, RobotPaint.Body));
            parts.Add(CylinderX(RobotRig.Pelvis, "HipActuator_R", V(0.2f * Right, -0.04f, 0f), 0.085f, 0.1f, RobotPaint.Body));

            // Three stacked waist rings, alternating paint and dark joint.
            for (int i = 0; i < 3; i++)
            {
                parts.Add(Box(RobotRig.Spine, $"Waist{i + 1}", V(0f, 0.02f + i * 0.1f, 0f), V(0.34f - i * 0.02f, 0.085f, 0.26f),
                    i % 2 == 1 ? RobotPaint.Joint : RobotPaint.Body));
            }

            // Chest armour, hazard bars, vent grille, back pack with its three glowing cells, exhaust stub and neck.
            parts.Add(Box(RobotRig.Chest, "ChestPlate", V(0f, 0.14f, 0f), V(0.62f, 0.44f, 0.4f), RobotPaint.Body));
            parts.Add(Box(RobotRig.Chest, "ChestFront", V(0f, 0.2f, 0.2f), V(0.5f, 0.22f, 0.06f), RobotPaint.Body));
            parts.Add(Box(RobotRig.Chest, "HazardBar_L", V(0.24f * Left, 0.14f, 0.205f), V(0.06f, 0.32f, 0.03f), RobotPaint.Accent));
            parts.Add(Box(RobotRig.Chest, "HazardBar_R", V(0.24f * Right, 0.14f, 0.205f), V(0.06f, 0.32f, 0.03f), RobotPaint.Accent));
            parts.Add(Box(RobotRig.Chest, "HazardBarCross", V(0f, -0.04f, 0.205f), V(0.32f, 0.05f, 0.03f), RobotPaint.Accent));
            for (int k = 0; k < 5; k++)
            {
                parts.Add(Box(RobotRig.Chest, $"VentSlat{k + 1}", V(0f, 0.02f + k * 0.035f, 0.215f), V(0.22f, 0.015f, 0.02f), RobotPaint.Joint));
            }

            parts.Add(Box(RobotRig.Chest, "PowerPack", V(0f, 0.14f, -0.27f), V(0.42f, 0.38f, 0.18f), RobotPaint.Joint));
            for (int k = 0; k < 3; k++)
            {
                parts.Add(Box(RobotRig.Chest, $"PowerCell{k + 1}", V(-0.12f + k * 0.12f, 0.14f, -0.365f), V(0.06f, 0.24f, 0.02f), RobotPaint.Cells));
            }

            parts.Add(CylinderY(RobotRig.Chest, "ExhaustStub", V(0f, 0.42f, -0.22f), 0.05f, 0.18f, RobotPaint.Body));
            parts.Add(CylinderY(RobotRig.Chest, "Neck", V(0f, 0.39f, 0f), 0.08f, 0.12f, RobotPaint.Joint));

            // Head: helmet, visor slot, red eye bar, yellow roof plate, antenna with a red tip, scanner dot.
            parts.Add(Box(RobotRig.Head, "Helmet", V(0f, 0.06f, 0f), V(0.27f, 0.22f, 0.27f), RobotPaint.Body));
            parts.Add(Box(RobotRig.Head, "VisorSlot", V(0f, 0.07f, 0.125f), V(0.24f, 0.07f, 0.04f), RobotPaint.Joint));
            parts.Add(Box(RobotRig.Head, "EyeBar", V(0f, 0.07f, 0.142f), V(0.17f, 0.022f, 0.02f), RobotPaint.Eye));
            parts.Add(Box(RobotRig.Head, "RoofPlate", V(0f, 0.185f, 0f), V(0.2f, 0.035f, 0.24f), RobotPaint.Accent));
            parts.Add(CylinderY(RobotRig.Head, "Antenna", V(0.09f, 0.3f, -0.06f), 0.008f, 0.26f, RobotPaint.Body));
            parts.Add(Sphere(RobotRig.Head, "AntennaTip", V(0.09f, 0.43f, -0.06f), 0.018f, RobotPaint.Eye));
            parts.Add(Sphere(RobotRig.Head, RobotRig.ScannerDot, V(0f, 0.07f, 0.15f), 0.016f, RobotPaint.Eye));

            AddArm(parts, "L", Left, RobotRig.UpperArmL, RobotRig.ForeArmL, RobotRig.HandL);
            AddArm(parts, "R", Right, RobotRig.UpperArmR, RobotRig.ForeArmR, RobotRig.HandR);
            AddLeg(parts, "L", Left, RobotRig.ThighL, RobotRig.ShinL, RobotRig.FootL);
            AddLeg(parts, "R", Right, RobotRig.ThighR, RobotRig.ShinR, RobotRig.FootR);
            return parts;
        }

        // Shoulder, upper arm with its piston, elbow pin, then the claw hand (left) or the weapon pod (right).
        private static void AddArm(List<RobotPart> parts, string side, float u, string upper, string fore, string hand)
        {
            parts.Add(Box(upper, "ShoulderPlate_" + side, V(0.03f * u, 0.03f, 0f), V(0.22f, 0.14f, 0.26f), RobotPaint.Body));
            parts.Add(Box(upper, "ShoulderStripe_" + side, V(0.06f * u, 0.1f, 0f), V(0.05f, 0.03f, 0.27f), RobotPaint.Accent));
            parts.Add(Sphere(upper, "ShoulderBall_" + side, V(0f, 0f, 0f), 0.08f, RobotPaint.Joint));
            parts.Add(CylinderY(upper, "ArmLimb_" + side, V(0f, -0.02f, 0f), 0.06f, 0.32f, RobotPaint.Body));
            parts.Add(CylinderY(upper, "ArmPistonHousing_" + side, V(0f, -0.12f, 0.08f), 0.02f, 0.18f, RobotPaint.Joint));
            parts.Add(CylinderY(upper, "ArmPistonRod_" + side, V(0f, -0.26f, 0.08f), 0.011f, 0.16f, RobotPaint.Body));
            parts.Add(CylinderX(fore, "Elbow_" + side, V(0f, 0f, 0f), 0.07f, 0.12f, RobotPaint.Joint));

            if (u == Left)
            {
                parts.Add(Box(fore, "ForeArmBlock_L", V(0f, -0.16f, 0f), V(0.13f, 0.3f, 0.13f), RobotPaint.Body));
                parts.Add(Box(hand, "Palm_L", V(0f, -0.02f, 0f), V(0.1f, 0.06f, 0.08f), RobotPaint.Joint));
                for (int f = -1; f <= 1; f++)
                {
                    parts.Add(new RobotPart(hand, $"Claw{f + 2}_L", RobotShape.Box, V(f * 0.035f, -0.09f, 0.02f), V(0.025f, 0.11f, 0.025f), Deg(0.3f), RobotPaint.Body));
                }
            }
            else
            {
                parts.Add(Box(fore, "PodBlock_R", V(0f, -0.18f, 0f), V(0.16f, 0.36f, 0.17f), RobotPaint.Body));
                parts.Add(Box(fore, "PodStrip_R", V(0f, -0.1f, 0.09f), V(0.17f, 0.05f, 0.03f), RobotPaint.Accent));
                parts.Add(CylinderX(fore, "AmmoDrum_R", V(0.11f, -0.2f, 0f), 0.075f, 0.1f, RobotPaint.Joint)); // outer side
                parts.Add(Box(hand, "BarrelMount_R", V(0f, 0f, 0f), V(0.13f, 0.08f, 0.1f), RobotPaint.Joint));
                parts.Add(CylinderY(hand, "BarrelLeft_R", V(-0.035f, -0.14f, 0f), 0.022f, 0.34f, RobotPaint.Joint));
                parts.Add(CylinderY(hand, "BarrelRight_R", V(0.035f, -0.14f, 0f), 0.022f, 0.34f, RobotPaint.Joint));
            }
        }

        // Reverse-knee leg: hip ball and sloped thigh armour, knee pin and shin, ankle pin, foot plate with two toes and a heel spur.
        private static void AddLeg(List<RobotPart> parts, string side, float u, string thigh, string shin, string foot)
        {
            float thighTilt = Deg(-0.3f);
            float shinTilt = Deg(0.33f);
            parts.Add(Sphere(thigh, "HipBall_" + side, V(0f, 0f, 0f), 0.09f, RobotPaint.Joint));
            parts.Add(new RobotPart(thigh, "ThighArmour_" + side, RobotShape.Box, V(0f, -0.2f, 0.06f), V(0.17f, 0.4f, 0.2f), thighTilt, RobotPaint.Body));
            parts.Add(new RobotPart(thigh, "ThighStripe_" + side, RobotShape.Box, V(0.06f * u, -0.2f, 0.17f), V(0.04f, 0.3f, 0.03f), thighTilt, RobotPaint.Accent));
            parts.Add(new RobotPart(thigh, "KneeHousing_" + side, RobotShape.CylinderY, V(0f, -0.2f, -0.1f), V(0.056f, 0.24f, 0.056f), Deg(0.15f), RobotPaint.Joint));

            parts.Add(CylinderX(shin, "KneePin_" + side, V(0f, 0f, 0f), 0.08f, 0.18f, RobotPaint.Body));
            parts.Add(new RobotPart(shin, "ShinCore_" + side, RobotShape.Box, V(0f, -0.2f, -0.07f), V(0.13f, 0.42f, 0.13f), shinTilt, RobotPaint.Joint));
            parts.Add(new RobotPart(shin, "ShinPlate_" + side, RobotShape.Box, V(0f, -0.14f, 0f), V(0.14f, 0.2f, 0.06f), shinTilt, RobotPaint.Body));
            parts.Add(new RobotPart(shin, "KneeRod_" + side, RobotShape.CylinderY, V(0f, -0.12f, -0.16f), V(0.028f, 0.28f, 0.028f), Deg(0.4f), RobotPaint.Body));

            parts.Add(CylinderX(foot, "AnklePin_" + side, V(0f, 0f, 0f), 0.055f, 0.14f, RobotPaint.Joint));
            parts.Add(Box(foot, "FootPlate_" + side, V(0f, -0.09f, 0.06f), V(0.18f, 0.06f, 0.26f), RobotPaint.Body));
            parts.Add(new RobotPart(foot, "ToeOuter_" + side, RobotShape.Box, V(0.05f * u, -0.1f, 0.22f), V(0.05f, 0.04f, 0.12f), Deg(0.15f), RobotPaint.Joint));
            parts.Add(new RobotPart(foot, "ToeInner_" + side, RobotShape.Box, V(-0.05f * u, -0.1f, 0.22f), V(0.05f, 0.04f, 0.12f), Deg(0.15f), RobotPaint.Joint));
            parts.Add(new RobotPart(foot, "HeelSpur_" + side, RobotShape.Box, V(0f, -0.1f, -0.1f), V(0.05f, 0.04f, 0.1f), Deg(-0.25f), RobotPaint.Joint));
        }

        private static void AddPart(Transform joint, RobotPart part, RobotMaterialSet materials)
        {
            GameObject go = GameObject.CreatePrimitive(part.Shape == RobotShape.Sphere ? PrimitiveType.Sphere
                : part.Shape == RobotShape.Box ? PrimitiveType.Cube : PrimitiveType.Cylinder);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // The visual prefab has no colliders.
            go.name = part.Name;
            go.transform.SetParent(joint, false);
            go.transform.localPosition = part.Position;
            go.transform.localRotation = Quaternion.Euler(part.TiltDegrees, 0f, part.Shape == RobotShape.CylinderX ? 90f : 0f);

            // A Unity cylinder is 2 m tall and 1 m wide along Y, so its scale is (diameter, length / 2, diameter).
            // The X cylinders are turned 90 degrees about Z, so their length runs along the joint's X.
            Vector3 size = part.Size;
            go.transform.localScale = part.Shape == RobotShape.CylinderY ? new Vector3(size.x, size.y / 2f, size.z)
                : part.Shape == RobotShape.CylinderX ? new Vector3(size.y, size.x / 2f, size.z)
                : size;
            go.GetComponent<Renderer>().sharedMaterial = Pick(materials, part.Paint);
        }

        private static Material Pick(RobotMaterialSet materials, RobotPaint paint)
        {
            switch (paint)
            {
                case RobotPaint.Joint: return materials.Joint;
                case RobotPaint.Accent: return materials.Accent;
                case RobotPaint.Eye: return materials.Eye;
                case RobotPaint.Cells: return materials.Cells;
                default: return materials.Body;
            }
        }

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        private static float Deg(float radians)
        {
            return radians * Mathf.Rad2Deg;
        }

        private static RobotPart Box(string joint, string name, Vector3 position, Vector3 size, RobotPaint paint)
        {
            return new RobotPart(joint, name, RobotShape.Box, position, size, 0f, paint);
        }

        private static RobotPart Sphere(string joint, string name, Vector3 position, float radius, RobotPaint paint)
        {
            return new RobotPart(joint, name, RobotShape.Sphere, position, V(radius * 2f, radius * 2f, radius * 2f), 0f, paint);
        }

        private static RobotPart CylinderY(string joint, string name, Vector3 position, float radius, float length, RobotPaint paint)
        {
            return new RobotPart(joint, name, RobotShape.CylinderY, position, V(radius * 2f, length, radius * 2f), 0f, paint);
        }

        private static RobotPart CylinderX(string joint, string name, Vector3 position, float radius, float length, RobotPaint paint)
        {
            return new RobotPart(joint, name, RobotShape.CylinderX, position, V(length, radius * 2f, radius * 2f), 0f, paint);
        }
    }
}
