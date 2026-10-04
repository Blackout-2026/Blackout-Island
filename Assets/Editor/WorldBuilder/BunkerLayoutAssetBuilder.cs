using System.Collections.Generic;
using Blackout.World;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates Assets/ScriptableObjects/Layout/BunkerLayout.asset from the room table in docs/level-layout.md.
    /// Re-runnable: if the asset already exists it is returned unchanged, so values tuned by hand are never overwritten.
    /// </summary>
    public static class BunkerLayoutAssetBuilder
    {
        public const string AssetPath = "Assets/ScriptableObjects/Layout/BunkerLayout.asset";

        // Real-scale rules (docs/level-layout.md, "Real-scale rules").
        private const float WallThickness = 0.4f;
        private const float Ceiling = 3.0f;
        private const float GeneratorCeiling = 6.5f; // exception: 3.9 m generator + catwalk at +3.0 m
        private const float DoorWidth = 1.4f;
        private const float DoorHeight = 2.3f;
        private const float PassageSize = 3.0f; // open passages match the 3 m corridor

        [MenuItem("Blackout/World/Create Bunker Layout Asset")]
        public static void CreateFromMenu()
        {
            BunkerLayout layout = LoadOrCreate();
            Selection.activeObject = layout;
            EditorGUIUtility.PingObject(layout);
        }

        /// <summary>Loads the layout asset, creating it from the table first if it does not exist.</summary>
        public static BunkerLayout LoadOrCreate()
        {
            BunkerLayout layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(AssetPath);
            if (layout != null)
            {
                return layout;
            }

            layout = ScriptableObject.CreateInstance<BunkerLayout>();
            FillFromTable(layout);
            AssetDatabase.CreateAsset(layout, AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Created {AssetPath} with {layout.rooms.Count} rooms.");
            return layout;
        }

        // Unity coordinates, +Z north. position = floor centre (y = floor height there), size = clear interior.
        private static void FillFromTable(BunkerLayout layout)
        {
            layout.wallThickness = WallThickness;
            layout.rooms = new List<Room>
            {
                MakeRoom("BunkerEntranceLobby", new Vector3(0f, 10f, 50.2f), new Vector3(11.2f, Ceiling, 3.6f), 0f,
                    Door("D-Bunker", WallSide.South, 0f, OpeningKind.SlideDoor, "Compound"),
                    Passage("O-RampTop", WallSide.North, "BunkerEntranceRamp")),

                // Floor runs from y 10.0 (south end) down to y 2.5 (north end): 7.5 m over 17.2 m = 23.6 degrees.
                MakeRoom("BunkerEntranceRamp", new Vector3(0f, 6.25f, 61f), new Vector3(PassageSize, Ceiling, 17.2f), 7.5f,
                    Passage("O-RampTop", WallSide.South, "BunkerEntranceLobby"),
                    Passage("O-RampBottom", WallSide.North, "SecurityArea")),

                MakeRoom("SecurityArea", new Vector3(0f, 2.5f, 76f), new Vector3(16f, Ceiling, 12f), 0f,
                    Passage("O-RampBottom", WallSide.South, "BunkerEntranceRamp"),
                    Door("D-Security", WallSide.North, 0f, OpeningKind.SlideDoor, "Corridor")),

                MakeRoom("Corridor", new Vector3(0f, 2.5f, 103f), new Vector3(3f, Ceiling, 41.2f), 0f,
                    Door("D-Security", WallSide.South, 0f, OpeningKind.SlideDoor, "SecurityArea"),
                    Door("D-Generator", WallSide.West, 3f, OpeningKind.HingeDoor, "GeneratorRoom"), // z 106
                    Door("D-Lab", WallSide.North, 0f, OpeningKind.MaglockDoor, "Laboratory")),

                MakeRoom("GeneratorRoom", new Vector3(-10.9f, 2.5f, 106f), new Vector3(18f, GeneratorCeiling, 20f), 0f,
                    Door("D-Generator", WallSide.East, 0f, OpeningKind.HingeDoor, "Corridor")),

                MakeRoom("Laboratory", new Vector3(0f, 2.5f, 133f), new Vector3(20f, Ceiling, 18f), 0f,
                    Door("D-Lab", WallSide.South, 0f, OpeningKind.MaglockDoor, "Corridor")),
            };
        }

        private static Room MakeRoom(string name, Vector3 position, Vector3 size, float floorDropNorth, params DoorOpening[] openings)
        {
            return new Room
            {
                name = name,
                position = position,
                size = size,
                floorDropNorth = floorDropNorth,
                openings = new List<DoorOpening>(openings),
            };
        }

        private static DoorOpening Door(string id, WallSide wall, float offset, OpeningKind kind, string connectsTo)
        {
            return new DoorOpening { id = id, wall = wall, offset = offset, width = DoorWidth, height = DoorHeight, kind = kind, connectsTo = connectsTo };
        }

        private static DoorOpening Passage(string id, WallSide wall, string connectsTo)
        {
            return new DoorOpening { id = id, wall = wall, offset = 0f, width = PassageSize, height = PassageSize, kind = OpeningKind.Open, connectsTo = connectsTo };
        }
    }
}
