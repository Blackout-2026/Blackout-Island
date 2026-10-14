using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// The primitive placeholder of every modelled prop and of the two pickups, with the sizes, pivots and colliders of
    /// docs/reference/core-developer.md section 6 and 7 (the prototype's room code). Axes: +Y up, the front of a prop
    /// looks along +Z, and the origin follows the pivot rule of its <see cref="PropEntry"/> (docs/art-pipeline.md section 5).
    /// Rounded boxes are plain boxes; the Blender model replaces all of this, keeping size, pivot, colliders and sockets.
    /// </summary>
    public static class PropDefinitions
    {
        public const string CureAnchor = "CureAnchor";

        /// <summary>The definition for a catalog name such as "Prop_LabBench" or "Medkit_Visual", or null if there is none.</summary>
        public static ModelDefinition For(string name)
        {
            switch (name)
            {
                case "Prop_ContainmentChamber": return Chamber(name);
                case "Prop_LabBench": return LabBench(name);
                case "Prop_Analyser": return Analyser(name);
                case "Prop_Microscope": return Microscope(name);
                case "Prop_SpecimenFridge": return SpecimenFridge(name);
                case "Prop_FumeHood": return FumeHood(name);
                case "Prop_Desk": return Desk(name, 3.0f, 0.78f);
                case "Prop_DeskGuard": return Desk(name, 1.9f, 0.78f);
                case "Prop_DeskCommander": return Desk(name, 4.4f, 0.85f);
                case "Prop_OfficeChair": return OfficeChair(name);
                case "Prop_MeetingTable": return MeetingTable(name);
                case "Prop_Monitor": return Monitor(name);
                case "Prop_WallMonitor": return WallMonitor(name);
                case "Prop_Locker": return Locker(name);
                case "Prop_ElectricalCabinet": return ElectricalCabinet(name);
                case "Prop_ControlConsole": return ControlConsole(name);
                case "Prop_FuelTank": return FuelTank(name);
                case "Prop_Crate": return Crate(name, 1.5f);
                case "Prop_CrateSmall": return Crate(name, 1.0f);
                case "Prop_Barrel": return Barrel(name);
                case "CureCase_Visual": return CureCase(name);
                case "Medkit_Visual": return Medkit(name);
                default: return null;
            }
        }

        // 6.2: floor plate, glass wall of 16 panels (20 slots of 18 degrees, 4 left out for the 72 degree opening, which
        // faces +Z), steel collar and column, glow ring of 24 segments, pedestal. 2.88 m tall: the lab ceiling is 3.0.
        // The marker CureSocket turns it 180 degrees, so the opening faces the D-Lab door.
        private static ModelDefinition Chamber(string name)
        {
            const float radius = 1.7f;
            const float glassBottom = 0.25f;
            const float glassHeight = 2.27f;
            var d = new ModelDefinition(name);
            d.Box("FloorPlate", 0f, 0.125f, 0f, 3.8f, 0.25f, 3.8f, "Steel");
            d.CylinderY("Pedestal", 0f, 0.7f, 0f, 0.34f, 0.9f, "LabWhite");
            d.CylinderY("PedestalPlate", 0f, 1.175f, 0f, 0.34f, 0.05f, "Steel");
            d.CylinderY("Collar", 0f, 2.62f, 0f, 1.85f, 0.2f, "Steel");
            d.CylinderY("Column", 0f, 2.8f, 0f, 0.12f, 0.16f, "Steel");

            for (int k = 0; k < 16; k++)
            {
                float yaw = 45f + k * 18f; // from +Z; the panels at -27, -9, 9 and 27 degrees are the opening
                d.TiltedBox($"GlassPanel{k + 1:00}", RadialX(radius, yaw), glassBottom + glassHeight / 2f, RadialZ(radius, yaw),
                    0.54f, glassHeight, 0.04f, new Vector3(0f, yaw, 0f), "Glass");
                d.CapsuleCollider(RadialX(radius, yaw), glassBottom + glassHeight / 2f, RadialZ(radius, yaw), 0.27f, glassHeight);
            }

            for (int k = 0; k < 24; k++)
            {
                float yaw = k * 15f;
                d.TiltedBox($"GlowRing{k + 1:00}", RadialX(1.65f, yaw), 2.46f, RadialZ(1.65f, yaw), 0.44f, 0.06f, 0.08f, new Vector3(0f, yaw, 0f), "GlowCyan");
            }

            d.BoxCollider(0f, 0.125f, 0f, 3.8f, 0.25f, 3.8f);
            d.CapsuleCollider(0f, 0.725f, 0f, 0.34f, 0.95f);

            // Where the cure case rests: 0.02 m above the pedestal plate (6.1).
            d.Socket(CureAnchor, 0f, 1.22f, 0f);
            return d;
        }

        private static float RadialX(float radius, float yawDegrees)
        {
            return radius * Mathf.Sin(yawDegrees * Mathf.Deg2Rad);
        }

        private static float RadialZ(float radius, float yawDegrees)
        {
            return radius * Mathf.Cos(yawDegrees * Mathf.Deg2Rad);
        }

        // 7.5: white base 3.4 x 0.88 x 0.9 with a dark top 3.5 x 0.05 x 1.0.
        private static ModelDefinition LabBench(string name)
        {
            return new ModelDefinition(name)
                .Box("Base", 0f, 0.44f, 0f, 3.4f, 0.88f, 0.9f, "LabWhite")
                .Box("Top", 0f, 0.905f, 0f, 3.5f, 0.05f, 1.0f, "DeskTop")
                .BoxCollider(0f, 0.465f, 0f, 3.5f, 0.93f, 1.0f);
        }

        private static ModelDefinition Analyser(string name)
        {
            return new ModelDefinition(name)
                .Box("Body", 0f, 0.15f, 0f, 0.4f, 0.3f, 0.4f, "LabWhite")
                .CylinderY("Drum", 0f, 0.36f, 0f, 0.13f, 0.12f, "PlasticDark")
                .BoxCollider(0f, 0.21f, 0f, 0.4f, 0.42f, 0.4f);
        }

        private static ModelDefinition Microscope(string name)
        {
            return new ModelDefinition(name)
                .Box("Base", 0f, 0.02f, -0.03f, 0.18f, 0.04f, 0.24f, "PlasticDark")
                .Box("Arm", 0f, 0.19f, -0.1f, 0.05f, 0.3f, 0.05f, "Steel")
                .Cylinder("Tube", PartShape.CylinderY, 0f, 0.2f, 0.07f, 0.03f, 0.2f, new Vector3(34f, 0f, 0f), "PlasticDark")
                .BoxCollider(0f, 0.17f, 0f, 0.18f, 0.34f, 0.3f);
        }

        // 7.5: 0.8 deep, 1.3 wide, 2.1 tall with a cyan glowing door panel; the door faces +Z.
        private static ModelDefinition SpecimenFridge(string name)
        {
            return new ModelDefinition(name)
                .Box("Body", 0f, 1.05f, -0.01f, 1.3f, 2.1f, 0.78f, "LabWhite")
                .Box("DoorPanel", 0f, 1.05f, 0.39f, 1.1f, 1.8f, 0.02f, "GlowCyan")
                .BoxCollider(0f, 1.05f, 0f, 1.3f, 2.1f, 0.8f);
        }

        private static ModelDefinition FumeHood(string name)
        {
            return new ModelDefinition(name)
                .Box("Base", 0f, 0.45f, 0f, 3.0f, 0.9f, 1.0f, "LabWhite")
                .Box("Top", 0f, 2.3f, 0f, 3.0f, 0.6f, 1.0f, "LabWhite")
                .Box("BackPanel", 0f, 1.45f, -0.45f, 3.0f, 1.1f, 0.1f, "DeskTop")
                .Box("GlassFront", 0f, 1.45f, 0.475f, 3.0f, 1.1f, 0.05f, "Glass")
                .BoxCollider(0f, 1.3f, 0f, 3.0f, 2.6f, 1.0f);
        }

        // 7.1: top 0.04 thick, 4 legs r 0.03, a steel-blue drawer unit on the right.
        private static ModelDefinition Desk(string name, float width, float height)
        {
            float legLength = height - 0.04f;
            float legX = -(width / 2f - 0.08f);
            return new ModelDefinition(name)
                .Box("Top", 0f, height - 0.02f, 0f, width, 0.04f, 1.0f, "DeskTop")
                .Box("DrawerUnit", width / 2f - 0.3f, legLength / 2f, 0f, 0.5f, legLength, 0.9f, "Locker")
                .CylinderY("Leg_FL", legX, legLength / 2f, 0.42f, 0.03f, legLength, "MetalDark")
                .CylinderY("Leg_BL", legX, legLength / 2f, -0.42f, 0.03f, legLength, "MetalDark")
                .BoxCollider(0f, height / 2f, 0f, width, height, 1.0f);
        }

        // 7.1: seat 0.46 x 0.08 x 0.46 at 0.47 high, back 0.44 x 0.5 x 0.06 (top at 1.03), post r 0.03, round base r 0.25.
        private static ModelDefinition OfficeChair(string name)
        {
            return new ModelDefinition(name)
                .CylinderY("Base", 0f, 0.03f, 0f, 0.25f, 0.04f, "MetalDark")
                .CylinderY("Post", 0f, 0.25f, 0f, 0.03f, 0.38f, "Steel")
                .Box("Seat", 0f, 0.47f, 0f, 0.46f, 0.08f, 0.46f, "PlasticDark")
                .Box("Back", 0f, 0.78f, -0.2f, 0.44f, 0.5f, 0.06f, "PlasticDark")
                .BoxCollider(0f, 0.515f, 0f, 0.5f, 1.03f, 0.5f);
        }

        private static ModelDefinition MeetingTable(string name)
        {
            var d = new ModelDefinition(name).Box("Top", 0f, 0.74f, 0f, 2.0f, 0.04f, 1.0f, "DeskTop");
            d.CylinderY("Leg_FL", -0.9f, 0.36f, 0.4f, 0.03f, 0.72f, "Steel");
            d.CylinderY("Leg_FR", 0.9f, 0.36f, 0.4f, 0.03f, 0.72f, "Steel");
            d.CylinderY("Leg_BL", -0.9f, 0.36f, -0.4f, 0.03f, 0.72f, "Steel");
            d.CylinderY("Leg_BR", 0.9f, 0.36f, -0.4f, 0.03f, 0.72f, "Steel");
            return d.BoxCollider(0f, 0.38f, 0f, 2.0f, 0.76f, 1.0f);
        }

        // 7.1: screen 0.62 x 0.42, bezel 0.70 x 0.50 x 0.12, stand 0.06 x 0.25 x 0.06. The foot makes the depth 0.2.
        private static ModelDefinition Monitor(string name)
        {
            return new ModelDefinition(name)
                .Box("Foot", 0f, 0.01f, 0f, 0.3f, 0.02f, 0.2f, "PlasticDark")
                .Box("Stand", 0f, 0.135f, -0.04f, 0.06f, 0.23f, 0.06f, "PlasticDark")
                .Box("Bezel", 0f, 0.5f, -0.04f, 0.7f, 0.5f, 0.12f, "PlasticDark")
                .Box("Screen", 0f, 0.5f, 0.0225f, 0.62f, 0.42f, 0.005f, "GlowScreen")
                .BoxCollider(0f, 0.375f, 0f, 0.7f, 0.75f, 0.2f);
        }

        // Pivot on the back face, centred: the model reaches out along +Z.
        private static ModelDefinition WallMonitor(string name)
        {
            return new ModelDefinition(name)
                .Box("Bezel", 0f, 0f, 0.0375f, 1.16f, 0.68f, 0.075f, "PlasticDark")
                .Box("Screen", 0f, 0f, 0.0775f, 1.10f, 0.62f, 0.005f, "GlowScreen")
                .BoxCollider(0f, 0f, 0.04f, 1.16f, 0.68f, 0.08f);
        }

        // 7.1: 0.52 x 2.0 x 0.55, steel-blue, vent plate 0.3 x 0.15.
        private static ModelDefinition Locker(string name)
        {
            return new ModelDefinition(name)
                .Box("Body", 0f, 1.0f, -0.01f, 0.52f, 2.0f, 0.53f, "Locker")
                .Box("VentPlate", 0f, 1.7f, 0.265f, 0.3f, 0.15f, 0.02f, "MetalDark")
                .BoxCollider(0f, 1.0f, 0f, 0.52f, 2.0f, 0.55f);
        }

        // 7.4: 1.2 x 2.2 x 0.7 with an inset panel 0.9 x 0.6 and 3 LEDs.
        private static ModelDefinition ElectricalCabinet(string name)
        {
            var d = new ModelDefinition(name)
                .Box("Body", 0f, 1.1f, -0.02f, 1.2f, 2.2f, 0.66f, "Olive")
                .Box("Panel", 0f, 1.5f, 0.33f, 0.9f, 0.6f, 0.04f, "MetalDark");
            for (int k = 0; k < 3; k++)
            {
                d.Box($"Led{k + 1}", -0.2f + k * 0.2f, 1.1f, 0.34f, 0.04f, 0.04f, 0.02f, "GlowGreen");
            }

            return d.BoxCollider(0f, 1.1f, 0f, 1.2f, 2.2f, 0.7f);
        }

        // 7.4: olive body with a sloped top panel tilted 0.35 rad; the panel's low edge is the front.
        private static ModelDefinition ControlConsole(string name)
        {
            return new ModelDefinition(name)
                .Box("Body", 0f, 0.525f, 0f, 1.8f, 1.05f, 0.75f, "Olive")
                .TiltedBox("TopPanel", 0f, 1.0f, 0f, 1.8f, 0.06f, 0.6f, new Vector3(20f, 0f, 0f), "DeskTop")
                .TiltedBox("Screen", 0f, 1.0376f, 0.0137f, 1.2f, 0.02f, 0.35f, new Vector3(20f, 0f, 0f), "GlowScreen")
                .BoxCollider(0f, 0.565f, 0f, 1.8f, 1.13f, 0.75f);
        }

        // 7.4: cylinder r 1.0, 4.2 long, lying along Z, centre 1.35 high, on two cradles of 1.8 x 0.6 x 0.24.
        private static ModelDefinition FuelTank(string name)
        {
            return new ModelDefinition(name)
                .CylinderZ("Tank", 0f, 1.35f, 0f, 1.0f, 4.2f, "Olive")
                .Box("Cradle_Front", 0f, 0.3f, 1.2f, 1.8f, 0.6f, 0.24f, "MetalDark")
                .Box("Cradle_Back", 0f, 0.3f, -1.2f, 1.8f, 0.6f, 0.24f, "MetalDark")
                .BoxCollider(0f, 1.175f, 0f, 2.0f, 2.35f, 4.2f);
        }

        // 7.3: a clean cube with a centred pivot (it is pushed), two dark straps. The body is a hair smaller than the straps.
        private static ModelDefinition Crate(string name, float size)
        {
            float strap = size * 0.08f;
            float strapX = size * 0.3f;
            return new ModelDefinition(name)
                .Box("Body", 0f, 0f, 0f, size, size - 0.04f, size - 0.04f, "Wood")
                .Box("Strap_L", -strapX, 0f, 0f, strap, size, size, "MetalDark")
                .Box("Strap_R", strapX, 0f, 0f, strap, size, size, "MetalDark")
                .BoxCollider(0f, 0f, 0f, size, size, size);
        }

        // 7.6: r 0.30, 0.9 tall, 12 sides in the prototype (Unity's cylinder has 20, fine for a placeholder).
        private static ModelDefinition Barrel(string name)
        {
            return new ModelDefinition(name)
                .CylinderY("Body", 0f, 0.45f, 0f, 0.29f, 0.9f, "Olive")
                .CylinderY("Ring_Low", 0f, 0.25f, 0f, 0.3f, 0.04f, "MetalDark")
                .CylinderY("Ring_High", 0f, 0.65f, 0f, 0.3f, 0.04f, "MetalDark")
                .CapsuleCollider(0f, 0.45f, 0f, 0.3f, 0.9f);
        }

        // 6.1: base plate, lid, two chrome posts, three glowing vials with caps, a glass box. Pivot under the base plate.
        private static ModelDefinition CureCase(string name)
        {
            var d = new ModelDefinition(name)
                .Box("BasePlate", 0f, 0.04f, 0f, 0.42f, 0.08f, 0.24f, "Steel")
                .Box("LidPlate", 0f, 0.26f, 0f, 0.42f, 0.04f, 0.24f, "Steel")
                .CylinderY("Post_L", -0.19f, 0.16f, 0f, 0.012f, 0.2f, "Chrome")
                .CylinderY("Post_R", 0.19f, 0.16f, 0f, 0.012f, 0.2f, "Chrome")
                .Box("Glass", 0f, 0.16f, 0f, 0.4f, 0.2f, 0.22f, "Glass");
            for (int k = 0; k < 3; k++)
            {
                float x = -0.1f + k * 0.1f;
                d.CylinderY($"Vial{k + 1}", x, 0.165f, 0f, 0.03f, 0.17f, "CureVial");
                d.CylinderY($"VialCap{k + 1}", x, 0.235f, 0f, 0.034f, 0.03f, "PlasticDark");
            }

            return d;
        }

        // 6.3: off-white case 0.42 x 0.26 x 0.28 with a red cross on the front and back and a handle on top.
        private static ModelDefinition Medkit(string name)
        {
            var d = new ModelDefinition(name).Box("Case", 0f, 0.13f, 0f, 0.42f, 0.26f, 0.26f, "MedWhite");
            foreach (float side in new[] { 1f, -1f })
            {
                string tag = side > 0f ? "Front" : "Back";
                d.Box("CrossUp_" + tag, 0f, 0.13f, 0.135f * side, 0.05f, 0.14f, 0.01f, "MedRed");
                d.Box("CrossAcross_" + tag, 0f, 0.13f, 0.135f * side, 0.14f, 0.05f, 0.01f, "MedRed");
            }

            return d
                .Box("HandleBar", 0f, 0.32f, 0f, 0.14f, 0.02f, 0.03f, "PlasticDark")
                .Box("HandlePost_L", -0.06f, 0.29f, 0f, 0.02f, 0.06f, 0.03f, "PlasticDark")
                .Box("HandlePost_R", 0.06f, 0.29f, 0f, 0.02f, 0.06f, 0.03f, "PlasticDark");
        }
    }
}
