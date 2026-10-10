using System;
using System.Collections.Generic;
using System.Linq;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds the gameplay markers from <see cref="MarkerLayout"/> under the scene root "Markers", for Build Bunker
    /// Blockout. Each one is an empty GameObject with a Blackout.Shared Marker (type, id = its name, owner) and a
    /// Scene-view label in a colour per type. PlayerStart, GeneratorSocket, CureSocket and BoatExit sit directly under
    /// Markers; the rest go in one group per type (Checkpoint/Checkpoint_Beach), so a re-run reuses them by name and
    /// removes stale ones (<see cref="GeneratedObjects"/>). Anything else under Markers is left alone.
    /// </summary>
    public static class MarkerBuilder
    {
        private const string UndoLabel = "Build Bunker Blockout";

        // Built-in Scene-view label icons: 1 blue, 2 teal, 3 green, 4 yellow, 5 orange, 6 red, 7 purple.
        private static readonly Dictionary<MarkerType, string> LabelIcons = new Dictionary<MarkerType, string>
        {
            { MarkerType.PlayerStart, "sv_label_3" },
            { MarkerType.Checkpoint, "sv_label_3" },
            { MarkerType.AgentSpawn, "sv_label_6" },
            { MarkerType.Waypoint, "sv_label_5" },
            { MarkerType.CoverPoint, "sv_label_1" },
            { MarkerType.DoorSocket, "sv_label_4" },
            { MarkerType.GeneratorSocket, "sv_label_7" },
            { MarkerType.CureSocket, "sv_label_7" },
            { MarkerType.BoatExit, "sv_label_7" },
            { MarkerType.HealthPickup, "sv_label_2" },
        };

        public static string Build(Transform markersRoot, BunkerLayout layout)
        {
            List<MarkerSpec> specs = MarkerLayout.Build(layout);
            var groups = new Dictionary<string, HashSet<string>>();
            foreach (MarkerSpec spec in specs)
            {
                GameObject go = GetOrCreate(markersRoot, spec, groups);
                go.transform.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
                go.transform.localScale = Vector3.one;
                ApplyMarker(go, spec);
                EditorGUIUtility.SetIconForObject(go, EditorGUIUtility.IconContent(LabelIcons[spec.Type]).image as Texture2D);
            }

            int removed = 0;
            foreach (KeyValuePair<string, HashSet<string>> group in groups)
            {
                removed += GeneratedObjects.RemoveStale(markersRoot.Find(group.Key), group.Value);
            }

            string counts = string.Join(", ", specs.GroupBy(s => s.Type).Select(g => $"{g.Key} {g.Count()}"));
            return $"{specs.Count} markers ({counts}), {removed} stale removed";
        }

        /// <summary>Adds (or reuses) the Marker component and sets its type, id and owner.</summary>
        public static Marker ApplyMarker(GameObject go, MarkerSpec spec)
        {
            Marker marker = GeneratedObjects.GetOrAddComponent<Marker>(go);

            // Marker keeps its fields private (the shared contract has no setters), so set them as serialized properties.
            var serialized = new SerializedObject(marker);
            Property(serialized, "type").intValue = (int)spec.Type;
            Property(serialized, "id").stringValue = spec.Name;
            Property(serialized, "owner").stringValue = spec.Owner;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return marker;
        }

        // A marker named after its type is a single marker under the root; any other is "<Type>_<part>" in its group.
        private static GameObject GetOrCreate(Transform markersRoot, MarkerSpec spec, Dictionary<string, HashSet<string>> groups)
        {
            string typeName = spec.Type.ToString();
            if (spec.Name == typeName)
            {
                return GeneratedObjects.FindOrCreateChild(markersRoot, spec.Name, UndoLabel).gameObject;
            }

            Transform group = GeneratedObjects.FindOrCreateChild(markersRoot, typeName, UndoLabel);
            group.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            group.localScale = Vector3.one;
            if (!groups.TryGetValue(typeName, out HashSet<string> built))
            {
                built = new HashSet<string>();
                groups[typeName] = built;
            }

            return GeneratedObjects.GetOrCreate(group, spec.Name.Substring(typeName.Length + 1), built, UndoLabel);
        }

        private static SerializedProperty Property(SerializedObject serialized, string field)
        {
            return serialized.FindProperty(field) ?? throw new MissingFieldException(typeof(Marker).FullName, field);
        }
    }
}
