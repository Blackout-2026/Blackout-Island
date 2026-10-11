using System;
using System.Collections.Generic;
using System.IO;
using Blackout.Shared;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Validate Layout: checks Environment.unity against what the agents and the mission need, and prints
    /// a pass/fail table to the Console (as an error if any check fails):
    /// - the NavMesh is loaded and is the one from Blackout/World/Bake NavMesh;
    /// - every PlayerStart, AgentSpawn, Waypoint, CoverPoint and BoatExit marker is within 1 m of the NavMesh;
    /// - complete NavMesh paths exist PlayerStart -> CureSocket -> BoatExit (the mission, there and back);
    /// - every door gap (DoorSocket) is at least 1.2 m wide, measured with rays from the socket to the jambs;
    /// - every pine and palm trunk on the terrain is cut out of the NavMesh, so agents walk round the trees (W6).
    /// Read-only: it never changes the scene. Doors live in another scene, so paths go through the open gaps.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.LayoutValidator.ValidateLayout
    /// (exit code 1 when a check fails).
    /// </summary>
    public static class LayoutValidator
    {
        public const float MaxMarkerDistance = 1f;
        public const float MinDoorWidth = 1.2f;

        /// <summary>
        /// How far from a trunk's foot to look for the NavMesh. Uncut, the NavMesh runs under the trunk (within about
        /// 0.2 m of the ground); cut out, it starts at the trunk's radius (0.23 m or more) plus the agent radius (0.4 m).
        /// </summary>
        public const float TrunkSampleRadius = 0.3f;

        /// <summary>How far to look for a jamb on each side of a door socket.</summary>
        public const float JambReach = 3f;

        // A door gap is measured at three heights over the agent's body: ankles, middle and head.
        private static readonly float[] GapHeights = { 0.2f, NavMeshBaker.AgentHeight * 0.5f, NavMeshBaker.AgentHeight - 0.1f };

        private static readonly MarkerType[] OnNavMeshTypes =
        {
            MarkerType.PlayerStart, MarkerType.AgentSpawn, MarkerType.Waypoint, MarkerType.CoverPoint, MarkerType.BoatExit,
        };

        private static readonly RaycastHit[] Hits = new RaycastHit[16];

        [MenuItem("Blackout/World/Validate Layout")]
        public static void ValidateLayout()
        {
            Scene scene = OpenEnvironmentScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Validate Layout cancelled (the Environment scene is not open).");
                return;
            }

            List<LayoutCheck> checks = Run(scene);
            string table = LayoutReport.Format(checks);
            bool passed = LayoutReport.AllPassed(checks);
            if (passed)
            {
                Debug.Log(table);
            }
            else
            {
                Debug.LogError(table);
            }

            if (Application.isBatchMode && !passed)
            {
                EditorApplication.Exit(1);
            }
        }

        /// <summary>Runs every check on the scene, in table order: NavMesh, markers, paths, door gaps.</summary>
        public static List<LayoutCheck> Run(Scene scene)
        {
            var markers = new List<Marker>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                markers.AddRange(root.GetComponentsInChildren<Marker>(true));
            }

            var filter = new NavMeshQueryFilter { agentTypeID = NavMeshBaker.AgentTypeId, areaMask = NavMesh.AllAreas };
            var checks = new List<LayoutCheck> { CheckNavMeshData(scene) };
            foreach (Marker marker in markers)
            {
                if (Array.IndexOf(OnNavMeshTypes, marker.Type) >= 0)
                {
                    checks.Add(CheckOnNavMesh(marker, filter));
                }
            }

            var path = new NavMeshPath();
            checks.Add(CheckPath(markers, MarkerType.PlayerStart, MarkerType.CureSocket, filter, path));
            checks.Add(CheckPath(markers, MarkerType.CureSocket, MarkerType.BoatExit, filter, path));

            foreach (Marker marker in markers)
            {
                if (marker.Type == MarkerType.DoorSocket)
                {
                    checks.Add(CheckDoorGap(marker, scene));
                }
            }

            checks.Add(CheckTrunksCutOut(scene, filter));
            return checks;
        }

        // One row for all the terrain's pines and palms: how many trunks still have NavMesh at their foot.
        private static LayoutCheck CheckTrunksCutOut(Scene scene, NavMeshQueryFilter filter)
        {
            const string check = "Trunks cut out";
            const string limit = "0 on the NavMesh";
            var trunks = new HashSet<GameObject>
            {
                VegetationLibrary.Resolve(VegetationPrototype.Pine, out _),
                VegetationLibrary.Resolve(VegetationPrototype.Palm, out _),
            };

            int trees = 0;
            int onNavMesh = 0;
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                {
                    TerrainData data = terrain.terrainData;
                    TreePrototype[] prototypes = data.treePrototypes;
                    foreach (TreeInstance tree in data.treeInstances)
                    {
                        GameObject prefab = prototypes[tree.prototypeIndex].prefab;
                        if (!trunks.Contains(prefab) || prefab.GetComponent<CapsuleCollider>() == null)
                        {
                            continue;
                        }

                        trees++;
                        Vector3 foot = terrain.GetPosition() + Vector3.Scale(tree.position, data.size);
                        foot.y = terrain.SampleHeight(foot) + terrain.GetPosition().y;
                        if (NavMesh.SamplePosition(foot, out NavMeshHit _, TrunkSampleRadius, filter))
                        {
                            onNavMesh++;
                        }
                    }
                }
            }

            return new LayoutCheck(check, $"{trees} pines and palms", $"{onNavMesh} on the NavMesh", limit, onNavMesh == 0);
        }

        /// <summary>
        /// Width of the opening around a floor point, along <paramref name="along"/> (the wall's direction): the narrowest
        /// of three rays to the jambs on both sides, at ankle, middle and head height. Only colliders in
        /// <paramref name="scene"/> count, so a closed door loaded from another scene does not hide the gap.
        /// Infinity if a jamb is missing within <see cref="JambReach"/>.
        /// </summary>
        public static float MeasureGap(Vector3 floorPoint, Vector3 along, Scene scene)
        {
            Physics.SyncTransforms(); // colliders moved or created in the Editor are not in the physics scene until synced
            float narrowest = float.PositiveInfinity;
            foreach (float height in GapHeights)
            {
                Vector3 origin = floorPoint + Vector3.up * height;
                float width = DistanceToJamb(origin, along, scene) + DistanceToJamb(origin, -along, scene);
                if (float.IsPositiveInfinity(width))
                {
                    return width;
                }

                narrowest = Mathf.Min(narrowest, width);
            }

            return narrowest;
        }

        private static LayoutCheck CheckNavMeshData(Scene scene)
        {
            const string check = "NavMesh";
            const string limit = "baked by Bake NavMesh";
            GameObject root = WorldSceneSkeleton.FindRoot(scene, NavMeshBaker.RootName);
            NavMeshSurface surface = root != null ? root.GetComponent<NavMeshSurface>() : null;
            if (surface == null || surface.navMeshData == null)
            {
                return new LayoutCheck(check, NavMeshBaker.RootName, "no NavMesh data", limit, false);
            }

            // Pressing Bake on the NavMeshSurface would save other data, baked with the Project Settings agent size.
            string dataPath = AssetDatabase.GetAssetPath(surface.navMeshData);
            int triangles = NavMesh.CalculateTriangulation().indices.Length / 3;
            bool passed = dataPath == NavMeshBaker.NavMeshDataPath && triangles > 0;
            return new LayoutCheck(check, NavMeshBaker.RootName, $"{Path.GetFileName(dataPath)}, {triangles} triangles",
                limit, passed);
        }

        private static LayoutCheck CheckOnNavMesh(Marker marker, NavMeshQueryFilter filter)
        {
            Vector3 position = marker.transform.position;
            bool found = NavMesh.SamplePosition(position, out NavMeshHit hit, MaxMarkerDistance, filter);
            string measured = found ? $"{Vector3.Distance(position, hit.position):0.00} m" : "not found";
            return new LayoutCheck("On NavMesh", Label(marker), measured, $"<= {MaxMarkerDistance:0.0} m", found);
        }

        private static LayoutCheck CheckPath(List<Marker> markers, MarkerType fromType, MarkerType toType,
            NavMeshQueryFilter filter, NavMeshPath path)
        {
            const string check = "Path";
            const string limit = "complete";
            Marker from = markers.Find(marker => marker.Type == fromType);
            Marker to = markers.Find(marker => marker.Type == toType);
            string subject = $"{(from != null ? Label(from) : fromType.ToString())} -> {(to != null ? Label(to) : toType.ToString())}";
            if (from == null || to == null)
            {
                return new LayoutCheck(check, subject, "marker missing", limit, false);
            }

            if (!NavMesh.SamplePosition(from.transform.position, out NavMeshHit start, MaxMarkerDistance, filter)
                || !NavMesh.SamplePosition(to.transform.position, out NavMeshHit end, MaxMarkerDistance, filter))
            {
                return new LayoutCheck(check, subject, "end off the NavMesh", limit, false);
            }

            NavMesh.CalculatePath(start.position, end.position, filter, path);
            bool complete = path.status == NavMeshPathStatus.PathComplete;
            string measured = complete
                ? $"{LayoutReport.PathLength(path.corners):0.0} m, {path.corners.Length} corners"
                : path.status.ToString();
            return new LayoutCheck(check, subject, measured, limit, complete);
        }

        private static LayoutCheck CheckDoorGap(Marker socket, Scene scene)
        {
            // A socket sits mid-wall, facing through the door, so the wall runs along its right axis.
            float width = MeasureGap(socket.transform.position, socket.transform.right, scene);
            bool found = !float.IsPositiveInfinity(width);
            string measured = found ? $"{width:0.00} m" : $"no jamb within {JambReach:0} m";
            return new LayoutCheck("Door gap", Label(socket), measured, $">= {MinDoorWidth:0.0} m",
                found && width >= MinDoorWidth);
        }

        private static float DistanceToJamb(Vector3 origin, Vector3 direction, Scene scene)
        {
            int count = scene.GetPhysicsScene().Raycast(origin, direction, Hits, JambReach,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (Hits[i].collider.gameObject.scene == scene)
                {
                    nearest = Mathf.Min(nearest, Hits[i].distance);
                }
            }

            return nearest;
        }

        private static string Label(Marker marker)
        {
            return string.IsNullOrEmpty(marker.Id) ? marker.name : marker.Id;
        }

        private static Scene OpenEnvironmentScene()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (scene.IsValid() && scene.isLoaded)
            {
                return scene;
            }

            // Opening a scene replaces the open ones, so give the user the chance to save first.
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return default;
            }

            return EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
        }
    }
}
