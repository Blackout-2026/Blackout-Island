using System.Collections.Generic;
using Blackout.Environment;
using Blackout.World;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>What a bunker fixture looks like: twin fluorescent tube, ceiling panel or sodium high-bay.</summary>
    public enum FixtureKind
    {
        None, // an outdoor light: no lamp mesh is built yet
        Tube,
        Panel,
        Sodium,
    }

    /// <summary>One light for Setup Lighting to build, in world coordinates.</summary>
    public class LightSpec
    {
        public string Group; // the room (Lighting/PoweredLights/<Group>) or "OutdoorLights"
        public string Name; // part name inside the group, e.g. "Tube01"
        public LightType Type;
        public FixtureKind Kind;
        public Vector3 Position;
        public Vector3 Target; // spot lights point here
        public string Hex; // the reference colour, linear (see LightingLayout)
        public float PrototypeIntensity;
        public float Intensity; // converted with LightingLayout.MatchIntensity
        public float Range;
        public float SpotAngle;
        public float InnerSpotAngle;
        public bool Shadows;
        public int ShadowTier; // URP additional-light shadow resolution tier: 0 Low (256 px), 1 Medium (512)
        public bool Flickers;
        public FlickerLight.Pattern Flicker;
        public int FlickerSeed;
        public bool AlongZ; // a tube's long side runs along Z (else along X)
    }

    /// <summary>
    /// The powered bunker fixtures and the outdoor lights, from the reference's tables (section 4, "Bunker fixtures"
    /// and "Outdoor lights"), placed in the real-scale rooms of BunkerLayout.asset.
    /// - Positions in X and Z are the prototype's (z negated). Heights follow each room's Unity ceiling: the lamp hangs
    ///   as far below it as in the prototype (tube 0.1 m, panel 0.05 m, sodium high-bay 0.7 m), and the light sits
    ///   0.25 m under the lamp. The ramp's 4 tubes are in the middle of each quarter of the ramp.
    /// - Intensities are converted with <see cref="LightingLayout.MatchIntensity"/>, so the floor under each lamp (or a
    ///   floodlight's aim point) is as bright as in the prototype.
    /// - Shadows only where a light would visibly shine through a wall: the generator's high-bays (about 9 times a
    ///   tube's intensity, next to the corridor), the ramp tube that is above the island's surface, the floodlights and
    ///   the wall pack over the bunker door. The other tubes, like every prototype fixture, cast none.
    /// - Two faulty tubes and the boat lantern get a <see cref="FlickerLight"/>.
    /// </summary>
    public static class LightFixtureLayout
    {
        public const string OutdoorGroup = "OutdoorLights";

        // The prototype puts every fixture's light this far below its lamp.
        public const float LightBelowLamp = 0.25f;
        public const float FixtureDecay = 1.6f;

        public const int ShadowTierLow = 0;
        public const int ShadowTierMedium = 1;

        // Faulty tubes (reference: the corridor tube near z 93 and the security tube at (4.5, 79)).
        public const float FaultyCorridorZ = 93f;
        public static readonly Vector2 FaultySecurityTube = new Vector2(4.5f, 79f);

        /// <summary>Boat lantern light, boat-local: 0.15 m over the centre of Beach/Boat/Boat_Lantern (BeachGreyboxBuilder).</summary>
        public static readonly Vector3 BoatLanternLocal = new Vector3(-1.2f, 2.6f, -1.7f);

        private const float BoatDeckY = 0.7f;

        /// <summary>How far below the ceiling the lamp hangs, as in the prototype.</summary>
        public static float LampDrop(FixtureKind kind)
        {
            switch (kind)
            {
                case FixtureKind.Panel: return 0.05f;
                case FixtureKind.Sodium: return 0.7f;
                default: return 0.1f;
            }
        }

        /// <summary>Size of the lamp mesh: a box for tubes and panels, a 0.6 m dome (cylinder) for high-bays.</summary>
        public static Vector3 LampSize(LightSpec spec)
        {
            switch (spec.Kind)
            {
                case FixtureKind.Tube: return spec.AlongZ ? new Vector3(0.32f, 0.12f, 1.45f) : new Vector3(1.45f, 0.12f, 0.32f);
                case FixtureKind.Panel: return new Vector3(1.25f, 0.08f, 0.7f);
                case FixtureKind.Sodium: return new Vector3(0.6f, 0.3f, 0.6f);
                default: return Vector3.zero;
            }
        }

        // ---- Bunker -----------------------------------------------------------------------------------------------

        /// <summary>The 31 powered fixtures, room by room in the layout's order.</summary>
        public static List<LightSpec> Powered(BunkerLayout layout)
        {
            var specs = new List<LightSpec>();

            // Ramp: 4 tubes across the ramp, colour #E2ECFF, 1.2 / 8 m, lamp 3.45 m over the floor in the prototype.
            Room ramp = layout.FindRoom("BunkerEntranceRamp");
            float rampSouth = ramp.position.z - ramp.size.z * 0.5f;
            for (int i = 0; i < 4; i++)
            {
                float z = rampSouth + ramp.size.z * (i + 0.5f) / 4f;
                specs.Add(Fixture(ramp, $"Tube{i + 1:00}", FixtureKind.Tube, 0f, z, "#E2ECFF", 1.2f, 8f, 3.45f, false));
            }

            // SecurityArea: 6 tubes along Z, #E8F0FF, 1.5 / 9 m, lamp 3.5 m over the floor in the prototype.
            Room security = layout.FindRoom("SecurityArea");
            int n = 1;
            foreach (float x in new[] { -4.5f, 0f, 4.5f })
            {
                foreach (float z in new[] { 73f, 79f })
                {
                    specs.Add(Fixture(security, $"Tube{n++:00}", FixtureKind.Tube, x, z, "#E8F0FF", 1.5f, 9f, 3.5f, true));
                }
            }

            // Corridor: 10 tubes along Z every 4 m from z 85 to 121, #E2ECFF, 1.35 / 8.5 m, lamp 3.3 m up.
            Room corridor = layout.FindRoom("Corridor");
            for (int i = 0; i < 10; i++)
            {
                specs.Add(Fixture(corridor, $"Tube{i + 1:00}", FixtureKind.Tube, 0f, 85f + 4f * i, "#E2ECFF", 1.35f, 8.5f, 3.3f, true));
            }

            // GeneratorRoom: 6 sodium high-bays, #FFA555, 3.0 / 14 m, lamp 5.8 m up (the same ceiling as the prototype).
            Room generator = layout.FindRoom("GeneratorRoom");
            n = 1;
            foreach (float x in new[] { -16f, -11f, -6f })
            {
                foreach (float z in new[] { 100f, 112f })
                {
                    specs.Add(Fixture(generator, $"Sodium{n++:00}", FixtureKind.Sodium, x, z, "#FFA555", 3f, 14f, 5.8f, false));
                }
            }

            // Laboratory: 5 ceiling panels, #E4F6FF, 1.3 / 9 m, lamp 3.95 m up; none at (0, 138), over the cure.
            Room lab = layout.FindRoom("Laboratory");
            n = 1;
            foreach (float x in new[] { -5f, 0f, 5f })
            {
                foreach (float z in new[] { 128f, 138f })
                {
                    if (x == 0f && z == 138f)
                    {
                        continue;
                    }

                    specs.Add(Fixture(lab, $"Panel{n++:00}", FixtureKind.Panel, x, z, "#E4F6FF", 1.3f, 9f, 3.95f, false));
                }
            }

            MarkFaulty(specs, corridor.name, new Vector2(0f, FaultyCorridorZ), 1);
            MarkFaulty(specs, security.name, FaultySecurityTube, 2);
            return specs;
        }

        private static LightSpec Fixture(Room room, string name, FixtureKind kind, float x, float z, string hex,
            float prototypeIntensity, float range, float prototypeLampHeight, bool alongZ)
        {
            float floor = BunkerShell.FloorY(room, z);
            float lampHeight = room.size.y - LampDrop(kind); // above the floor
            float lightHeight = lampHeight - LightBelowLamp;
            var position = new Vector3(x, floor + lightHeight, z);

            // Shadows for the high-bays, and for any tube above the plateau, which would light the ground outside.
            bool shadows = kind == FixtureKind.Sodium || position.y > IslandLayout.PlateauY;
            return new LightSpec
            {
                Group = room.name,
                Name = name,
                Type = LightType.Point,
                Kind = kind,
                Position = position,
                Hex = hex,
                PrototypeIntensity = prototypeIntensity,
                Intensity = LightingLayout.MatchIntensity(prototypeIntensity, range, FixtureDecay,
                    prototypeLampHeight - LightBelowLamp, lightHeight),
                Range = range,
                Shadows = shadows,
                ShadowTier = ShadowTierLow,
                AlongZ = alongZ,
            };
        }

        private static void MarkFaulty(List<LightSpec> specs, string room, Vector2 xz, int seed)
        {
            foreach (LightSpec spec in specs)
            {
                if (spec.Group == room && Mathf.Abs(spec.Position.x - xz.x) < 0.01f && Mathf.Abs(spec.Position.z - xz.y) < 0.01f)
                {
                    spec.Flickers = true;
                    spec.Flicker = FlickerLight.Pattern.FaultyTube;
                    spec.FlickerSeed = seed;
                }
            }
        }

        // ---- Outdoors (they stay on through the blackout) ---------------------------------------------------------

        /// <summary>Three compound floodlights, the wall pack over the bunker door and the boat lantern.</summary>
        public static List<LightSpec> Outdoor(BunkerLayout layout)
        {
            var specs = new List<LightSpec>
            {
                Floodlight("FloodlightW", new Vector3(-19.5f, 18.2f, 30.5f), new Vector3(-3f, 10f, 45f)),
                Floodlight("FloodlightE", new Vector3(19.5f, 18.2f, 30.5f), new Vector3(4f, 10f, 44f)),
                Floodlight("FloodlightN", new Vector3(17.5f, 17.2f, 58f), new Vector3(2f, 10f, 42f)),
            };

            // Wall pack: 0.5 m over the bunker door and 0.8 m out from the wall, as in the prototype. The Unity door is
            // 2.3 m tall (the prototype's 3.4 m), so the light is 2.8 m over the yard instead of 3.9 m.
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            DoorOpening door = lobby.openings.Find(o => o.id == "D-Bunker");
            float wallFace = lobby.position.z - lobby.size.z * 0.5f - layout.wallThickness;
            var wallPack = new Vector3(0f, lobby.position.y + door.height + 0.5f, wallFace - 0.8f);
            specs.Add(new LightSpec
            {
                Group = OutdoorGroup,
                Name = "WallPack",
                Type = LightType.Point,
                Position = wallPack,
                Hex = "#D8E4FF",
                PrototypeIntensity = 2.2f,
                Intensity = LightingLayout.MatchIntensity(2.2f, 12f, 2f, 3.4f + 0.5f, wallPack.y - lobby.position.y),
                Range = 12f,
                Shadows = true, // it would light the lobby through its wall
                ShadowTier = ShadowTierLow,
            });

            // Boat lantern: lights the deck 2.05 m below it, as in the prototype; it flickers like a flame.
            Vector3 lantern = IslandLayout.BoatPosition + Quaternion.Euler(0f, IslandLayout.BoatYaw, 0f) * BoatLanternLocal;
            float aboveDeck = lantern.y - BoatDeckY;
            specs.Add(new LightSpec
            {
                Group = OutdoorGroup,
                Name = "BoatLantern",
                Type = LightType.Point,
                Position = lantern,
                Hex = "#FFA64D",
                PrototypeIntensity = 1.7f,
                Intensity = LightingLayout.MatchIntensity(1.7f, 15f, FixtureDecay, aboveDeck, aboveDeck),
                Range = 15f,
                Flickers = true,
                Flicker = FlickerLight.Pattern.Flame,
                FlickerSeed = 3,
            });
            return specs;
        }

        // Floodlight: spot, #FFF0D8, 5.0 / 42 m, decay 1.2, half-angle 0.48 rad with penumbra 0.55. Unity's spot angle is
        // the full cone (55 degrees) and its inner angle is the part without falloff (55 x 0.45 = 24.75 degrees).
        private static LightSpec Floodlight(string name, Vector3 position, Vector3 target)
        {
            float aim = Vector3.Distance(position, target);
            float spotAngle = 2f * 0.48f * Mathf.Rad2Deg;
            return new LightSpec
            {
                Group = OutdoorGroup,
                Name = name,
                Type = LightType.Spot,
                Position = position,
                Target = target,
                Hex = "#FFF0D8",
                PrototypeIntensity = 5f,
                Intensity = LightingLayout.MatchIntensity(5f, 42f, 1.2f, aim, aim),
                Range = 42f,
                SpotAngle = spotAngle,
                InnerSpotAngle = spotAngle * (1f - 0.55f),
                Shadows = true, // cheap (one shadow map each) and it keeps the beam out of the lobby
                ShadowTier = ShadowTierMedium,
            };
        }
    }
}
