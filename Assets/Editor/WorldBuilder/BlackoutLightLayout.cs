using System.Collections.Generic;
using Blackout.Environment;
using Blackout.Shared;
using Blackout.World;
using UnityEngine;
using State = Blackout.Environment.LightingStateProfile.State;

namespace Blackout.Editor
{
    /// <summary>An emergency lamp or alarm beacon: a <see cref="LightSpec"/> plus its rotation and where its lamp mesh goes.</summary>
    public class BlackoutLightSpec : LightSpec
    {
        public Quaternion Rotation;
        public Vector3 LampPosition;
    }

    /// <summary>
    /// Numbers for Blackout/World/Setup Blackout Lighting (W8), from docs/reference/world-builder.md section 4 ("Bunker,
    /// blackout" and "Blackout sequence"), placed in the real-scale rooms of BunkerLayout.asset. docs/level-layout.md lists
    /// them too: if you change a number here, change it there.
    /// - The two states of the <see cref="LightingStateProfile"/>: powered is the W7 scene (<see cref="LightingLayout"/>),
    ///   blackout is the reference's bunker blackout, converted from linear hex the same way.
    /// - 14 emergency lamps at the reference's wall positions, on the Unity walls, and 3 alarm beacons under the ceiling.
    ///   Intensities are converted with <see cref="LightingLayout.MatchIntensity"/>, like the W7 fixtures.
    /// - Where the blackout shows (the bunker's volumes) and where the cascade starts.
    /// </summary>
    public static class BlackoutLightLayout
    {
        public const string BlackoutGroup = "BlackoutLights";

        // ---- Blackout state (reference: "Bunker, blackout") ------------------------------------------------------
        public const string FogHex = "#0B0203";
        public const float FogDensity = 0.05f;
        public const string AmbientSkyHex = "#3A0907";
        public const string AmbientGroundHex = "#060202";
        public const float AmbientIntensity = 0.2f;

        /// <summary>The prototype's moon is off inside the bunker.</summary>
        public const float MoonIntensity = 0f;

        /// <summary>The W7 probes as baked, with the lights on.</summary>
        public const float PoweredReflectionIntensity = 1f;

        /// <summary>
        /// The bunker's reflection probes were captured with the lights on, and the prototype swaps to a near-black red
        /// environment instead. At 20 % the old reflections only hint at shapes on metal.
        /// </summary>
        public const float BlackoutReflectionIntensity = 0.2f;

        // ---- Emergency lamps (reference: #FF2614, 2.2, 9 m, decay 1.5) --------------------------------------------
        public const string EmergencyHex = "#FF2614";
        public const float EmergencyPrototypeIntensity = 2.2f;
        public const float EmergencyRange = 9f;
        public const float EmergencyDecay = 1.5f;

        /// <summary>A lamp hangs 0.5 m under the ceiling, but no higher than 4.0 m, the prototype's generator-room lamps.</summary>
        public const float LampBelowCeiling = 0.5f;
        public const float MaxLampHeight = 4f;

        /// <summary>The lamp box: along the wall, tall, deep. Its back touches the wall.</summary>
        public static readonly Vector3 LampSize = new Vector3(0.34f, 0.2f, 0.08f);

        /// <summary>The prototype puts the light 0.4 m out from the lamp and 0.2 m below it.</summary>
        public const float LightOut = 0.4f;
        public const float LightBelow = 0.2f;

        /// <summary>The lamp glows #FF2A1A x 5 at full strength. Unlit it is the prototype's dim red glass (linear 0.12, 0.02, 0.02).</summary>
        public const string LampHex = "#FF2A1A";
        public const float LampEmission = 5f;
        public static readonly Color LampUnlitLinear = new Color(0.12f, 0.02f, 0.02f);
        public const string LampBaseHex = "#330000";

        // ---- Alarm beacons (reference: #FF1808, 9, 22 m, decay 1.3, half angle 0.42 rad, penumbra 0.4) ------------
        public const string AlarmHex = "#FF1808";
        public const float AlarmPrototypeIntensity = 9f;
        public const float AlarmRange = 22f;
        public const float AlarmDecay = 1.3f;
        public const float AlarmHalfAngle = 0.42f;
        public const float AlarmPenumbra = 0.4f;
        public const float AlarmBelowCeiling = 0.2f;

        /// <summary>While it turns, the prototype aims the spot 6 m out and 1.6 m down: 14.9 degrees below the horizon.</summary>
        public static readonly Vector2 AlarmAim = new Vector2(6f, 1.6f);

        /// <summary>The dome glows #FF1A0A x 2.5 at full strength and x 0.2 unlit.</summary>
        public const string DomeHex = "#FF1A0A";
        public const float DomeEmission = 2.5f;
        public const float DomeUnlit = 0.2f;
        public const string DomeBaseHex = "#400404";
        public const float DomeDiameter = 0.3f;

        // ---- Volumes and cascade ----------------------------------------------------------------------------------
        public static readonly string[] UndergroundRooms = { "SecurityArea", "Corridor", "GeneratorRoom", "Laboratory" };
        public static readonly string[] EntranceRooms = { "BunkerEntranceLobby", "BunkerEntranceRamp" };

        /// <summary>The cascade starts 2 m above the generator socket, as in the prototype.</summary>
        public const float CascadeOriginHeight = 2f;

        // Reference positions, in Unity coordinates: the lamp's wall and where along it (z on east and west walls, x on
        // north and south walls). The reference's x of 1.9 in the 4 m corridor becomes the 3 m corridor's wall.
        private static readonly (string room, WallSide wall, float along)[] EmergencyWalls =
        {
            ("SecurityArea", WallSide.West, 76f),
            ("SecurityArea", WallSide.East, 79.5f),
            ("Corridor", WallSide.East, 86f),
            ("Corridor", WallSide.West, 95f),
            ("Corridor", WallSide.East, 104f),
            ("Corridor", WallSide.West, 114f),
            ("Corridor", WallSide.East, 121f),
            ("GeneratorRoom", WallSide.West, 99f),
            ("GeneratorRoom", WallSide.West, 112f),
            ("GeneratorRoom", WallSide.South, -9f),
            ("Laboratory", WallSide.West, 130f),
            ("Laboratory", WallSide.East, 137f),
            ("Laboratory", WallSide.North, 0f),
            ("BunkerEntranceRamp", WallSide.East, 60f),
        };

        // Two beacons in the corridor and one over the generator. The prototype starts them at random angles; here they
        // start a third of a turn apart.
        private static readonly (string room, float x, float z, float yaw)[] AlarmPlaces =
        {
            ("Corridor", 0f, 98f, 0f),
            ("Corridor", 0f, 117f, 120f),
            ("GeneratorRoom", -11f, 106f, 240f),
        };

        // ---- Profile values ---------------------------------------------------------------------------------------

        /// <summary>The powered state: the W7 scene's fog, ambient and moon, and the reflection probes as baked.</summary>
        public static State Powered => new State
        {
            fogColor = LightingLayout.FogColour,
            fogDensity = LightingLayout.FogDensity,
            ambientSky = LightingLayout.AmbientSky,
            ambientEquator = LightingLayout.AmbientEquator,
            ambientGround = LightingLayout.AmbientGround,
            directionalIntensity = LightingLayout.MoonIntensity,
            reflectionIntensity = PoweredReflectionIntensity,
        };

        /// <summary>
        /// The blackout state: the reference's bunker blackout fog and hemisphere light, converted like W7's (the hex
        /// values are linear; the equator, a wall's view, is half sky and half ground).
        /// </summary>
        public static State Blackout => new State
        {
            fogColor = LightingLayout.FromLinearHex(FogHex),
            fogDensity = FogDensity,
            ambientSky = LightingLayout.FromLinearHex(AmbientSkyHex, AmbientIntensity),
            ambientEquator = FromLinear((LightingLayout.Linear(AmbientSkyHex) + LightingLayout.Linear(AmbientGroundHex)) * 0.5f * AmbientIntensity),
            ambientGround = LightingLayout.FromLinearHex(AmbientGroundHex, AmbientIntensity),
            directionalIntensity = MoonIntensity,
            reflectionIntensity = BlackoutReflectionIntensity,
        };

        public static Color EmergencyLampOn => LightingLayout.FromLinearHex(LampHex, LampEmission);
        public static Color EmergencyLampUnlit => FromLinear(LampUnlitLinear);
        public static Color AlarmDomeOn => LightingLayout.FromLinearHex(DomeHex, DomeEmission);
        public static Color AlarmDomeUnlit => LightingLayout.FromLinearHex(DomeHex, DomeUnlit);

        // ---- Intensities ------------------------------------------------------------------------------------------

        /// <summary>
        /// Every emergency light has the prototype's one intensity, matched 2.3 m from the light: its height over the
        /// floor in the 3 m rooms. That gives 7.5.
        /// </summary>
        public static float EmergencyIntensity
        {
            get
            {
                float distance = 3f - LampBelowCeiling - LightBelow;
                return LightingLayout.MatchIntensity(EmergencyPrototypeIntensity, EmergencyRange, EmergencyDecay, distance, distance);
            }
        }

        public static float AlarmAimDistance => AlarmAim.magnitude;

        /// <summary>Degrees below the horizon.</summary>
        public static float AlarmTilt => Mathf.Atan2(AlarmAim.y, AlarmAim.x) * Mathf.Rad2Deg;

        /// <summary>An alarm beacon matches the prototype at its aim point, 6.2 m away, like the W7 floodlights: 228.</summary>
        public static float AlarmIntensity => LightingLayout.MatchIntensity(AlarmPrototypeIntensity, AlarmRange, AlarmDecay,
            AlarmAimDistance, AlarmAimDistance);

        // ---- Lights -----------------------------------------------------------------------------------------------

        /// <summary>The 14 emergency lamps, in the reference's order.</summary>
        public static List<BlackoutLightSpec> Emergency(BunkerLayout layout)
        {
            var specs = new List<BlackoutLightSpec>();
            var perRoom = new Dictionary<string, int>();
            foreach ((string roomName, WallSide wall, float along) in EmergencyWalls)
            {
                Room room = layout.FindRoom(roomName);
                Vector3 inward = Inward(wall);
                Vector3 lamp = WallPoint(room, wall, along) + inward * (LampSize.z * 0.5f);
                lamp.y = BunkerShell.FloorY(room, lamp.z) + Mathf.Min(room.size.y - LampBelowCeiling, MaxLampHeight);
                specs.Add(new BlackoutLightSpec
                {
                    Group = room.name,
                    Name = $"Emergency{Next(perRoom, room.name):00}",
                    Type = LightType.Point,
                    Kind = FixtureKind.None,
                    Position = lamp + inward * LightOut + Vector3.down * LightBelow,
                    Rotation = Quaternion.LookRotation(inward),
                    LampPosition = lamp,
                    Hex = EmergencyHex,
                    PrototypeIntensity = EmergencyPrototypeIntensity,
                    Intensity = EmergencyIntensity,
                    Range = EmergencyRange,
                });
            }

            return specs;
        }

        /// <summary>The 3 alarm beacons: red spots that hang under the ceiling, tilted down, and turn during the blackout.</summary>
        public static List<BlackoutLightSpec> Alarms(BunkerLayout layout)
        {
            var specs = new List<BlackoutLightSpec>();
            var perRoom = new Dictionary<string, int>();
            float spotAngle = 2f * AlarmHalfAngle * Mathf.Rad2Deg;
            foreach ((string roomName, float x, float z, float yaw) in AlarmPlaces)
            {
                Room room = layout.FindRoom(roomName);
                var position = new Vector3(x, BunkerShell.FloorY(room, z) + room.size.y - AlarmBelowCeiling, z);
                Quaternion rotation = Quaternion.Euler(AlarmTilt, yaw, 0f);
                specs.Add(new BlackoutLightSpec
                {
                    Group = room.name,
                    Name = $"Alarm{Next(perRoom, room.name):00}",
                    Type = LightType.Spot,
                    Kind = FixtureKind.None,
                    Position = position,
                    Target = position + rotation * Vector3.forward * AlarmAimDistance,
                    Rotation = rotation,
                    LampPosition = position,
                    Hex = AlarmHex,
                    PrototypeIntensity = AlarmPrototypeIntensity,
                    Intensity = AlarmIntensity,
                    Range = AlarmRange,
                    SpotAngle = spotAngle,
                    InnerSpotAngle = spotAngle * (1f - AlarmPenumbra),
                    Shadows = true, // a 22 m beam would otherwise sweep the next room through the wall
                    ShadowTier = LightFixtureLayout.ShadowTierLow,
                });
            }

            return specs;
        }

        // ---- Volumes and cascade ----------------------------------------------------------------------------------

        /// <summary>The underground rooms and their walls, floors and ceilings: inside it the blackout shows in full.</summary>
        public static Bounds Underground(BunkerLayout layout)
        {
            Bounds rooms = Union(layout, UndergroundRooms);
            float wall = layout.wallThickness;
            return MinMax(rooms.min - Vector3.one * wall, rooms.max + Vector3.one * wall);
        }

        /// <summary>
        /// The lobby and the ramp, with their walls but not their end walls: from just inside the bunker door (z 48.4) to
        /// the bottom of the ramp (z 69.6), where the underground volume starts.
        /// </summary>
        public static Bounds Entrance(BunkerLayout layout)
        {
            Bounds rooms = Union(layout, EntranceRooms);
            float wall = layout.wallThickness;
            var grow = new Vector3(wall, wall, 0f);
            return MinMax(rooms.min - grow, rooms.max + grow);
        }

        /// <summary>Where the cascade starts: above the GeneratorSocket marker.</summary>
        public static Vector3 CascadeOrigin(BunkerLayout layout)
        {
            foreach (MarkerSpec marker in MarkerLayout.Build(layout))
            {
                if (marker.Type == MarkerType.GeneratorSocket)
                {
                    return marker.Position + Vector3.up * CascadeOriginHeight;
                }
            }

            throw new System.InvalidOperationException("MarkerLayout has no GeneratorSocket.");
        }

        // ---- Helpers ----------------------------------------------------------------------------------------------

        // A room's clear interior as a box. A sloping room (the ramp) spans its lowest floor to its highest ceiling.
        private static Bounds RoomBox(Room room)
        {
            float drop = room.floorDropNorth * 0.5f;
            var min = new Vector3(room.position.x - room.size.x * 0.5f, room.position.y - drop, room.position.z - room.size.z * 0.5f);
            var max = new Vector3(room.position.x + room.size.x * 0.5f, room.position.y + drop + room.size.y, room.position.z + room.size.z * 0.5f);
            return MinMax(min, max);
        }

        private static Bounds Union(BunkerLayout layout, string[] roomNames)
        {
            Bounds bounds = RoomBox(layout.FindRoom(roomNames[0]));
            for (int i = 1; i < roomNames.Length; i++)
            {
                bounds.Encapsulate(RoomBox(layout.FindRoom(roomNames[i])));
            }

            return bounds;
        }

        private static Bounds MinMax(Vector3 min, Vector3 max)
        {
            var bounds = new Bounds();
            bounds.SetMinMax(min, max);
            return bounds;
        }

        // The point on a wall's inner face at floor height of the room's centre.
        private static Vector3 WallPoint(Room room, WallSide wall, float along)
        {
            Vector3 c = room.position;
            Vector3 half = room.size * 0.5f;
            switch (wall)
            {
                case WallSide.West: return new Vector3(c.x - half.x, c.y, along);
                case WallSide.East: return new Vector3(c.x + half.x, c.y, along);
                case WallSide.South: return new Vector3(along, c.y, c.z - half.z);
                default: return new Vector3(along, c.y, c.z + half.z);
            }
        }

        /// <summary>The direction from a wall into its room.</summary>
        public static Vector3 Inward(WallSide wall)
        {
            switch (wall)
            {
                case WallSide.West: return Vector3.right;
                case WallSide.East: return Vector3.left;
                case WallSide.South: return Vector3.forward;
                default: return Vector3.back;
            }
        }

        private static int Next(Dictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int n);
            counts[key] = n + 1;
            return n + 1;
        }

        private static Color FromLinear(Color linear)
        {
            Color srgb = linear.gamma;
            srgb.a = 1f;
            return srgb;
        }
    }
}
