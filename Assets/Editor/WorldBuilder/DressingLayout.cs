using System;
using System.Collections.Generic;
using System.Globalization;
using Blackout.Shared;
using UnityEngine;

namespace Blackout.Editor
{
    public readonly struct DressingSpec
    {
        public readonly string Room, Id, Prefab;
        public readonly Vector3 Position, Scale;
        public readonly float Yaw;

        public DressingSpec(string room, string id, string prefab, Vector3 position, float yaw, Vector3 scale)
        {
            Room = room; Id = id; Prefab = prefab;
            Position = position; Yaw = yaw; Scale = scale;
        }

        public string PrefabPath => "Assets/Prefabs/Props/" + Prefab + ".prefab";
    }

    /// <summary>Editor-only table reader and conservative clearance checks; no dependency on the art assembly.</summary>
    public static class DressingLayout
    {
        public const string DocumentPath = "docs/level-layout.md";
        public const string Begin = "<!-- W10-DRESSING-BEGIN -->";
        public const string End = "<!-- W10-DRESSING-END -->";
        public const float WalkingWidth = 1.2f;
        public const float MarkerRadius = 0.4f;

        // Empty walking volumes, 1.2 m wide and 2 m tall. Generator path stops before the console;
        // the lab path stops south of the cure chamber. Coordinates follow the frozen Unity layout.
        private static readonly Bounds[] WalkingPaths =
        {
            Box(0, 11, 50.2f, WalkingWidth, 2, 3.6f), // lobby
            Box(0, 3.5f, 76.2f, WalkingWidth, 2, 12.4f), // security
            Box(0, 3.5f, 103, WalkingWidth, 2, 41.6f), // corridor
            Box(0, 3.5f, 128.2f, WalkingWidth, 2, 8.8f), // lab approach
            Box(-3.9f, 3.5f, 106, 4.4f, 2, WalkingWidth), // generator approach
        };

        /// <summary>Read only the marked table. Reject malformed data before any scene changes.</summary>
        public static List<DressingSpec> Parse(string document)
        {
            int start = document.IndexOf(Begin, StringComparison.Ordinal);
            int end = document.IndexOf(End, StringComparison.Ordinal);
            if (start < 0 || end <= start) throw new FormatException("Missing W10 dressing table markers.");
            var result = new List<DressingSpec>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (string line in document.Substring(start + Begin.Length, end - start - Begin.Length).Split('\n'))
            {
                string row = line.Trim();
                if (row.Length == 0) continue;
                if (!row.StartsWith("|", StringComparison.Ordinal) || !row.EndsWith("|", StringComparison.Ordinal))
                    throw new FormatException("Expected a W10 table row: " + row);
                string[] cells = row.Trim('|').Split('|');
                for (int i = 0; i < cells.Length; i++) cells[i] = cells[i].Trim();
                if (cells[0] == "Room" || cells[0].StartsWith("---", StringComparison.Ordinal)) continue;
                if (cells.Length != 10) throw new FormatException("W10 rows need ten columns: " + row);
                if (cells[0].Length == 0 || cells[1].Length == 0 || !ids.Add(cells[1]))
                    throw new FormatException("Empty room or duplicate/empty dressing id: " + cells[1]);
                if (!cells[2].StartsWith("Prop_", StringComparison.Ordinal) || cells[2].IndexOfAny(new[] { '/', '\\', '.' }) >= 0)
                    throw new FormatException("Expected a prop basename: " + cells[2]);
                var p = new Vector3(Number(cells[3]), Number(cells[4]), Number(cells[5]));
                var scale = new Vector3(Number(cells[7]), Number(cells[8]), Number(cells[9]));
                if (scale.x <= 0 || scale.y <= 0 || scale.z <= 0) throw new FormatException("Scale must be positive.");
                result.Add(new DressingSpec(cells[0], cells[1], cells[2], p, Number(cells[6]), scale));
            }
            if (result.Count == 0) throw new FormatException("W10 dressing table is empty.");
            return result;
        }

        /// <summary>Null means safe. Check the combined world-space renderer/collider bounds, including rotation.</summary>
        public static string ClearanceIssue(Bounds bounds, IReadOnlyList<MarkerSpec> markers)
        {
            foreach (Bounds path in WalkingPaths)
                if (bounds.Intersects(path)) return "1.2 m walking path";

            // One-metre slices conservatively follow the ramp's slope, keeping 2 m headroom above its floor.
            for (int i = 0; i < 18; i++)
            {
                float z = Mathf.Min(52.4f + i, 69.1f);
                float floor = Mathf.Lerp(10, 2.5f, (z - 52.4f) / 17.2f);
                if (bounds.Intersects(Box(0, floor + 1, z, WalkingWidth, 2.44f, 1)))
                    return "1.2 m ramp walking path";
            }

            Bounds barricade = Box(0.7f, 3.25f, 95, 2.3f, 2.3f, 2.3f);
            if (bounds.Intersects(barricade)) return "dynamic barricade space";
            // Reserve the hero model, console access and chamber for their gameplay owner.
            if (bounds.Intersects(Box(-11, 4.7f, 106, 8.4f, 4.4f, 4.4f))) return "generator space";
            if (bounds.Intersects(Box(0, 4, 135, 4.6f, 3, 4.6f))) return "cure chamber space";

            foreach (MarkerSpec marker in markers)
            {
                float radius = marker.Type == MarkerType.DoorSocket ? 1.3f : MarkerRadius;
                Bounds space = new Bounds(marker.Position + Vector3.up, new Vector3(radius * 2, 2, radius * 2));
                if (bounds.Intersects(space)) return marker.Name;
            }
            return null;
        }

        private static Bounds Box(float x, float y, float z, float sx, float sy, float sz)
            => new Bounds(new Vector3(x, y, z), new Vector3(sx, sy, sz));

        private static float Number(string value)
        {
            float number = float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture);
            if (float.IsNaN(number) || float.IsInfinity(number)) throw new FormatException("Expected a finite number.");
            return number;
        }
    }
}
