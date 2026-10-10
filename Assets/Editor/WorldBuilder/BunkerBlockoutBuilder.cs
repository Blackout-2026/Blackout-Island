using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Build Bunker Blockout: builds every room in BunkerLayout.asset from cubes under
    /// Bunker/&lt;Room&gt; in Environment.unity: floor, ceiling and walls with gaps for the openings (the rules are in
    /// <see cref="BunkerShell"/>). Every part keeps its BoxCollider, is marked static (lightmaps, occlusion, static
    /// batching, reflection probes) and uses one placeholder material per room, so the rooms are easy to tell apart.
    /// There is no door geometry: the Systems Engineer's doors go in the gaps, at the DoorSocket markers.
    /// It then builds every gameplay marker under Markers, door sockets included (see <see cref="MarkerBuilder"/>).
    /// Re-runnable and deterministic: parts named "&lt;Room&gt;_..." are reused by name and stale ones removed; anything
    /// else under a room is left alone (see <see cref="GeneratedObjects"/>).
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.BunkerBlockoutBuilder.BuildBunkerBlockout
    /// </summary>
    public static class BunkerBlockoutBuilder
    {
        private const string BunkerRootName = "Bunker";
        private const string MarkersRootName = "Markers";
        private const string UndoLabel = "Build Bunker Blockout";
        private const float Smoothness = 0.1f; // matt, like the reference's concrete and wall panels (roughness 1.0)

        private const StaticEditorFlags StaticFlags = StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic
            | StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        // One placeholder colour per room, from the reference palette (section 3), chosen so that neighbours differ.
        private static readonly Dictionary<string, string> RoomColours = new Dictionary<string, string>
        {
            { "BunkerEntranceLobby", "#86857D" }, // concrete
            { "BunkerEntranceRamp", "#A27B1D" }, // hazard yellow (pipeYellow)
            { "SecurityArea", "#AEB9C4" }, // wallSec blue-grey
            { "Corridor", "#6C6E6A" }, // wallPanel, dark end
            { "GeneratorRoom", "#6A7350" }, // olive paint
            { "Laboratory", "#E9EEEF" }, // wallLab white
        };

        private const string FallbackColour = "#77766F"; // concrete grey, for a room added to the layout later

        [MenuItem("Blackout/World/Build Bunker Blockout")]
        public static void BuildBunkerBlockout()
        {
            Stopwatch timer = Stopwatch.StartNew();

            // Opens (or creates) the scene and adds any missing roots and room objects. It never removes anything.
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Build Bunker Blockout cancelled (the Environment scene is not open).");
                return;
            }

            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();
            Transform bunker = WorldSceneSkeleton.FindRoot(scene, BunkerRootName).transform;
            var rooms = new StringBuilder();
            int total = 0;
            int removed = 0;
            foreach (Room room in layout.rooms)
            {
                int parts = BuildRoom(bunker, layout, room, out int stale);
                total += parts;
                removed += stale;
                rooms.Append(rooms.Length > 0 ? ", " : string.Empty).Append($"{room.name} {parts}");
            }

            string markers = MarkerBuilder.Build(WorldSceneSkeleton.FindRoot(scene, MarkersRootName).transform, layout);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Bunker blockout built in {timer.ElapsedMilliseconds} ms: {total} parts in "
                + $"{layout.rooms.Count} rooms ({rooms}); {removed} stale part(s) removed; {markers}.");
        }

        private static int BuildRoom(Transform bunker, BunkerLayout layout, Room room, out int removed)
        {
            string colour = RoomColours.TryGetValue(room.name, out string hex) ? hex : FallbackColour;
            Material material = GreyboxMaterials.GetOrCreate($"Greybox_Bunker_{room.name}", colour, Smoothness);

            // The room object sits at the floor centre, where the scene skeleton put it; parts are placed in world space.
            Transform container = GeneratedObjects.FindOrCreateChild(bunker, room.name, UndoLabel);
            container.SetPositionAndRotation(room.position, Quaternion.identity);
            container.localScale = Vector3.one;

            var built = new HashSet<string>();
            foreach (ShellPart part in BunkerShell.BuildRoom(layout, room))
            {
                GameObject go = GeneratedObjects.GetOrCreate(container, part.Name, built, UndoLabel, PrimitiveType.Cube);
                go.transform.SetPositionAndRotation(part.Centre, part.Rotation);
                go.transform.localScale = part.Size;
                go.GetComponent<MeshRenderer>().sharedMaterial = material;
                GeneratedObjects.GetOrAddComponent<BoxCollider>(go);
                GameObjectUtility.SetStaticEditorFlags(go, StaticFlags);
            }

            removed = GeneratedObjects.RemoveStale(container, built);
            return built.Count;
        }
    }
}
