using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Primitive stand-ins for the three first-person viewmodels, ported from prototype/src/models/weapons.js with the
    /// camera-space flip (x, y, -z): the prototype looks down -Z, Unity down +Z, so every z and every pitch/yaw angle changes
    /// sign. The origin of each model is the prototype's gun frame (the receiver centre; for the knife the hand), the muzzle
    /// points along +Z, and the gloved hands and sleeves are part of the model (docs/reference/core-developer.md 5).
    /// Replace with free low-poly meshes through <c>SM_Weapon_*.fbx</c>; the pivot and socket names stay.
    /// </summary>
    public static class WeaponDefinitions
    {
        public const string MuzzlePoint = "MuzzlePoint";
        public const string BoltHandle = "BoltHandle";
        public const string Hand = "Hand";

        // Where the weapon holder puts each model relative to the camera (before the 0.85 holder scale), in Unity axes.
        public static readonly Vector3 SmgHeldPosition = new Vector3(0.13f, -0.125f, 0.45f);
        public static readonly Vector3 SniperHeldPosition = new Vector3(0.12f, -0.12f, 0.46f);
        public static readonly Vector3 KnifeHeldPosition = new Vector3(0.17f, -0.18f, 0.33f);

        public static ModelDefinition For(string name)
        {
            switch (name)
            {
                case "Weapon_Smg_Visual": return Smg(name);
                case "Weapon_Sniper_Visual": return Sniper(name);
                case "Weapon_Knife_Visual": return Knife(name);
                default: return null;
            }
        }

        private static ModelDefinition Smg(string name)
        {
            var d = new ModelDefinition(name)
                .Box("Receiver", 0f, 0f, 0f, 0.05f, 0.068f, 0.27f, "Metal")
                .CylinderZ("BarrelShroud", 0f, 0.004f, 0.19f, 0.02f, 0.13f, "Polymer")
                .CylinderZ("Barrel", 0f, 0.004f, 0.28f, 0.012f, 0.06f, "Metal")
                .CylinderZ("MuzzleTip", 0f, 0.004f, 0.31f, 0.016f, 0.03f, "Metal")
                .TiltedBox("Magazine", 0f, -0.1f, 0.03f, 0.032f, 0.16f, 0.048f, Tilt(0.12f, 0f), "Polymer")
                .TiltedBox("PistolGrip", 0f, -0.07f, -0.085f, 0.034f, 0.095f, 0.044f, Tilt(-0.3f, 0f), "Polymer")
                .TiltedBox("Foregrip", 0f, -0.045f, 0.14f, 0.028f, 0.05f, 0.03f, Tilt(0.2f, 0f), "Polymer")
                .Box("TopRail", 0f, 0.04f, 0.01f, 0.03f, 0.012f, 0.22f, "Metal")
                .Box("SightBody", 0f, 0.065f, -0.02f, 0.04f, 0.035f, 0.05f, "Metal")
                .Box("SightLens", 0f, 0.068f, 0.004f, 0.032f, 0.028f, 0.003f, "Lens")
                .Box("RedDot", 0f, 0.068f, 0f, 0.007f, 0.007f, 0.007f, "RedDot")
                .Box("ChargingHandle", 0.03f, 0.02f, -0.06f, 0.012f, 0.012f, 0.03f, "Steel")
                .Box("StockRod_Upper", 0.03f, 0.012f, 0.02f, 0.008f, 0.008f, 0.18f, "Metal")
                .Box("StockRod_Lower", 0.03f, -0.02f, 0.02f, 0.008f, 0.008f, 0.18f, "Metal")
                .Box("StockPlate", 0.03f, -0.004f, 0.11f, 0.006f, 0.04f, 0.01f, "Polymer")
                .Socket(MuzzlePoint, 0f, 0.004f, 0.33f);
            AddArm(d, string.Empty, "R", new Vector3(0f, -0.075f, -0.075f), 0.8f, 0.5f, 0.35f);
            AddArm(d, string.Empty, "L", new Vector3(-0.012f, -0.06f, 0.14f), 0.7f, -0.6f, 0.4f);
            return d;
        }

        private static ModelDefinition Sniper(string name)
        {
            var d = new ModelDefinition(name)
                .Box("Receiver", 0f, 0f, 0f, 0.05f, 0.06f, 0.3f, "Metal")
                .CylinderZ("Barrel", 0f, 0.008f, 0.45f, 0.0145f, 0.62f, "Metal")
                .CylinderZ("MuzzleBrake", 0f, 0.008f, 0.77f, 0.02f, 0.06f, "Metal")
                .Box("StockBody", 0f, -0.03f, -0.05f, 0.055f, 0.075f, 0.48f, "Tan")
                .Box("Butt", 0f, -0.04f, -0.33f, 0.05f, 0.11f, 0.18f, "Tan")
                .Box("CheekRest", 0f, 0.035f, -0.27f, 0.04f, 0.03f, 0.12f, "Polymer")
                .TiltedBox("Magazine", 0f, -0.09f, 0.02f, 0.03f, 0.08f, 0.06f, Tilt(0.1f, 0f), "Polymer")
                .CylinderZ("ScopeTube", 0f, 0.075f, 0.02f, 0.024f, 0.24f, "Metal")
                .CylinderZ("ScopeObjective", 0f, 0.075f, 0.17f, 0.029f, 0.06f, "Metal")
                .CylinderZ("ScopeEyepiece", 0f, 0.075f, -0.12f, 0.027f, 0.05f, "Metal")
                .CylinderZ("ScopeLens", 0f, 0.075f, 0.2f, 0.031f, 0.004f, "Lens")
                .Box("ScopeRing_Front", 0f, 0.045f, 0.08f, 0.03f, 0.035f, 0.02f, "Metal")
                .Box("ScopeRing_Back", 0f, 0.045f, -0.05f, 0.03f, 0.035f, 0.02f, "Metal")
                .CylinderX("ScopeTurret", 0.03f, 0.075f, 0.01f, 0.012f, 0.04f, "Metal")
                .Box("BipodLeg_L", -0.018f, -0.03f, 0.3f, 0.008f, 0.008f, 0.2f, "Metal")
                .Box("BipodLeg_R", 0.018f, -0.03f, 0.3f, 0.008f, 0.008f, 0.2f, "Metal")
                .Socket(MuzzlePoint, 0f, 0.008f, 0.81f);

            // The bolt handle cycles after each shot, so it is its own object with the pivot at its root (5.2).
            d.Pivot(BoltHandle, 0.03f, 0.01f, -0.08f);
            d.CylinderX("BoltShaft", 0.03f, 0f, 0f, 0.006f, 0.06f, "Steel", BoltHandle);
            d.Box("BoltKnob", 0.06f, 0f, 0f, 0.022f, 0.022f, 0.022f, "Steel", BoltHandle);

            // The Blender rifle has no empties, so the bolt pivot is also ensured as a socket (the placeholder keeps its pivot).
            d.Socket(BoltHandle, 0.03f, 0.01f, -0.08f);
            AddArm(d, string.Empty, "R", new Vector3(0f, -0.07f, -0.12f), 0.8f, 0.5f, 0.35f);
            AddArm(d, string.Empty, "L", new Vector3(-0.01f, -0.06f, 0.22f), 0.65f, -0.55f, 0.45f);
            return d;
        }

        // The whole hand swings in a slash, so every part hangs off the "Hand" pivot at the model's origin.
        private static ModelDefinition Knife(string name)
        {
            var d = new ModelDefinition(name).Pivot(Hand, 0f, 0f, 0f);
            d.CylinderZ("Grip", 0f, 0f, 0f, 0.017f, 0.12f, "Polymer", Hand);
            for (int i = 0; i < 4; i++)
            {
                d.CylinderZ($"GripRing{i + 1}", 0f, 0f, 0.04f - i * 0.025f, 0.019f, 0.006f, "Polymer", Hand);
            }

            d.Box("Guard", 0f, 0f, 0.065f, 0.07f, 0.012f, 0.014f, "Metal", Hand);
            d.Box("Blade", 0f, 0f, 0.16f, 0.004f, 0.032f, 0.18f, "Steel", Hand);
            d.Box("BladeTip", 0f, -0.006f, 0.26f, 0.004f, 0.018f, 0.03f, "Steel", Hand);
            d.Box("SpineRidge", 0f, 0.014f, 0.15f, 0.005f, 0.006f, 0.17f, "Metal", Hand);
            AddArm(d, Hand, "R", new Vector3(0f, -0.008f, -0.02f), 0.6f, 0.35f, 0.42f);
            // The Blender knife (SM_Weapon_Knife.fbx) has no empties, so the pivot is also ensured as a socket; the placeholder
            // already has it as a pivot and keeps that one.
            d.Socket(Hand, 0f, 0f, 0f);
            return d;
        }

        // A gloved hand with a sleeve reaching back and down out of view (weapons.js arm()). Position and angles are the
        // prototype's, already flipped by the caller for z; the angles are flipped here.
        private static void AddArm(ModelDefinition d, string parent, string side, Vector3 position, float rx, float ry, float length)
        {
            string pivot = "Arm" + side;
            d.Pivot(pivot, position.x, position.y, position.z, new Vector3(-rx * Mathf.Rad2Deg, -ry * Mathf.Rad2Deg, 0f), parent);
            d.Box("Glove" + side, 0f, 0f, 0f, 0.055f, 0.035f, 0.1f, "Glove", pivot);
            d.TiltedBox("Fingers" + side, 0f, -0.012f, 0.06f, 0.05f, 0.025f, 0.035f, Tilt(0.6f, 0f), "Glove", pivot);
            d.CylinderZ("Wrist" + side, 0f, 0f, -0.1f, 0.034f, 0.12f, "Glove", pivot);
            d.CylinderZ("Sleeve" + side, 0f, 0f, -(0.16f + length / 2f), 0.046f, length, "Sleeve", pivot);
        }

        // The prototype's pitch about X (and yaw about Y) in radians becomes the opposite angle in degrees after the z flip.
        private static Vector3 Tilt(float pitchRadians, float yawRadians)
        {
            return new Vector3(-pitchRadians * Mathf.Rad2Deg, -yawRadians * Mathf.Rad2Deg, 0f);
        }
    }
}
