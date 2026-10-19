using System.Collections.Generic;
using System.Diagnostics;
using Blackout.World;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Bake NavMesh: bakes the agents' walkable surface from the colliders in Environment.unity (terrain,
    /// dock, bunker blockout, later the props) into Assets/Environment/NavMesh/EnvironmentNavMesh.asset. The root object
    /// "NavMesh" holds a NavMeshSurface that loads that data whenever the scene is open, in the Editor and in play mode.
    /// Agent size: radius 0.4, height 1.8, step 0.4, slope 40 (docs/level-layout.md and the Agent Controller's
    /// PathProvider). Unity keeps agent sizes per agent type in Project Settings > Navigation (owned by the Systems
    /// Engineer), and the NavMeshSurface's own Bake button reads them from there. So this tool builds the data itself
    /// with these four numbers on agent type 0 (Humanoid), the type that NavMesh queries use by default. Do not press
    /// Bake on the NavMeshSurface: it would rebake with the Project Settings sizes.
    /// Left out of the bake: the invisible island boundary (its tops would become walkable strips), the boat (it bobs)
    /// and trigger colliders (zone volumes are not floors or walls). Added to it: the terrain's trees and boulders as
    /// Not Walkable capsules (W6, <see cref="AddTreeSources"/>), which Unity's collection leaves out.
    /// Re-runnable and deterministic: the same scene gives the same data, written into the same asset.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.NavMeshBaker.BakeNavMesh
    /// </summary>
    public static class NavMeshBaker
    {
        // The agent size agreed with the Agent Controller: the robots' capsule (radius 0.38, height 1.8) fits inside it,
        // and the slope limit is above the 23.6 degree bunker ramp but below the island's cliffs.
        public const float AgentRadius = 0.4f;
        public const float AgentHeight = 1.8f;
        public const float StepHeight = 0.4f;
        public const float MaxSlope = 40f;

        /// <summary>Humanoid, the default agent type: NavMesh queries without an agent type use it.</summary>
        public const int AgentTypeId = 0;

        public const string NavMeshFolder = "Assets/Environment/NavMesh";
        public const string NavMeshDataPath = NavMeshFolder + "/EnvironmentNavMesh.asset";
        public const string RootName = "NavMesh";

        private const string DataName = "EnvironmentNavMesh";
        private const string UndoLabel = "Bake NavMesh";
        private const int AllLayers = ~0;
        private const int WalkableArea = 0;

        // Scene objects left out of the bake: root name, then the path of the child under it.
        private static readonly string[][] Ignored =
        {
            new[] { "Terrain", "IslandBoundary" },
            new[] { "Beach", "Boat" },
        };

        [MenuItem("Blackout/World/Bake NavMesh")]
        public static void BakeNavMesh()
        {
            Stopwatch timer = Stopwatch.StartNew();

            // Opens (or creates) the scene and adds any missing roots. It never removes anything.
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Bake NavMesh cancelled (the Environment scene is not open).");
                return;
            }

            NavMeshBuildSettings settings = CreateBuildSettings();
            Bounds bounds = BakeBounds();
            foreach (string warning in settings.ValidationReport(bounds))
            {
                Debug.LogWarning($"[World] Bake NavMesh: {warning}");
            }

            List<NavMeshBuildSource> sources = CollectSources(scene, out int triggers);
            int trees = AddTreeSources(SceneTerrains(scene), sources);
            NavMeshData built = NavMeshBuilder.BuildNavMeshData(settings, sources, bounds, Vector3.zero, Quaternion.identity);
            if (built == null)
            {
                Debug.LogError("[World] Bake NavMesh failed: Unity returned no NavMesh data.");
                return;
            }

            NavMeshSurface surface = EnsureSurface(scene);
            // CopySerialized replaces native tile data. Unregister the old instance first; removing it
            // after replacement can crash Unity when the new geometry changes the tile layout.
            surface.RemoveData();
            NavMeshData data = SaveData(built);
            surface.navMeshData = data;
            surface.AddData();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
            Debug.Log($"[World] NavMesh baked in {timer.ElapsedMilliseconds} ms: {sources.Count - trees} colliders "
                + $"({triggers} triggers skipped) and {trees} tree and rock capsules; radius {settings.agentRadius}, height {settings.agentHeight}, "
                + $"step {settings.agentClimb}, slope {settings.agentSlope}; {mesh.indices.Length / 3} triangles, "
                + $"{SurfaceArea(mesh):0} m2 walkable; saved to {NavMeshDataPath}.");
        }

        /// <summary>Agent type 0's build settings from Project Settings, with the agreed agent size.</summary>
        public static NavMeshBuildSettings CreateBuildSettings()
        {
            NavMeshBuildSettings settings = NavMesh.GetSettingsByID(AgentTypeId);
            settings.agentRadius = AgentRadius;
            settings.agentHeight = AgentHeight;
            settings.agentClimb = StepHeight;
            settings.agentSlope = MaxSlope;
            return settings;
        }

        /// <summary>The terrain's footprint, from 2 m under its lowest point to 2 m over its highest.</summary>
        public static Bounds BakeBounds()
        {
            var bounds = new Bounds(IslandLayout.TerrainOrigin + IslandLayout.TerrainSize * 0.5f, IslandLayout.TerrainSize);
            bounds.Expand(new Vector3(0f, 4f, 0f));
            return bounds;
        }

        private static List<NavMeshBuildSource> CollectSources(Scene scene, out int triggers)
        {
            var markups = new List<NavMeshBuildMarkup>();
            foreach (string[] path in Ignored)
            {
                GameObject root = WorldSceneSkeleton.FindRoot(scene, path[0]);
                Transform ignored = root != null ? root.transform.Find(path[1]) : null;
                if (ignored != null)
                {
                    markups.Add(new NavMeshBuildMarkup { root = ignored, ignoreFromBuild = true, applyToChildren = true });
                }
            }

            // The reference reserves the catwalk for the player. Keep its physical geometry in the bake
            // as an obstacle, but mark its surfaces Not Walkable even though each riser is below agentClimb.
            GameObject bunker = WorldSceneSkeleton.FindRoot(scene, "Bunker");
            Transform catwalk = bunker != null ? bunker.transform.Find(GeneratorCatwalkBuilder.ObjectPath) : null;
            if (catwalk != null)
                markups.Add(new NavMeshBuildMarkup { root = catwalk, overrideArea = true,
                    area = NavMesh.GetAreaFromName("Not Walkable"), applyToChildren = true });

            // Root by root, in hierarchy order, so the bake only sees this scene and always in the same order.
            var sources = new List<NavMeshBuildSource>();
            var rootSources = new List<NavMeshBuildSource>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                NavMeshBuilder.CollectSources(root.transform, AllLayers, NavMeshCollectGeometry.PhysicsColliders,
                    WalkableArea, false, markups, false, rootSources);
                sources.AddRange(rootSources);
            }

            triggers = sources.RemoveAll(source => source.component is Collider collider && collider.isTrigger);
            return sources;
        }

        /// <summary>
        /// Adds a Not Walkable capsule for every terrain tree whose prefab has a CapsuleCollider (pines, palms, boulders),
        /// so agents walk round them. Unity's own collection takes the terrain's heights but not its trees' colliders
        /// (checked in W6: with the forest painted, the bake was unchanged and every trunk stood on the NavMesh).
        /// The capsule is the one the player bumps into: radius x width scale, height x height scale.
        /// </summary>
        public static int AddTreeSources(IEnumerable<Terrain> terrains, List<NavMeshBuildSource> sources)
        {
            int notWalkable = NavMesh.GetAreaFromName("Not Walkable");
            int added = 0;
            foreach (Terrain terrain in terrains)
            {
                TerrainData data = terrain.terrainData;
                TreePrototype[] prototypes = data.treePrototypes;
                foreach (TreeInstance tree in data.treeInstances)
                {
                    GameObject prefab = prototypes[tree.prototypeIndex].prefab;
                    var capsule = prefab != null ? prefab.GetComponent<CapsuleCollider>() : null;
                    if (capsule == null)
                    {
                        continue;
                    }

                    float width = tree.widthScale;
                    float height = tree.heightScale;
                    Vector3 foot = terrain.GetPosition() + Vector3.Scale(tree.position, data.size);
                    Vector3 centre = foot + Vector3.Scale(capsule.center, new Vector3(width, height, width));
                    sources.Add(new NavMeshBuildSource
                    {
                        shape = NavMeshBuildSourceShape.Capsule,
                        size = new Vector3(capsule.radius * 2f * width, capsule.height * height, capsule.radius * 2f * width),
                        transform = Matrix4x4.Translate(centre),
                        area = notWalkable,
                    });
                    added++;
                }
            }

            return added;
        }

        private static List<Terrain> SceneTerrains(Scene scene)
        {
            var terrains = new List<Terrain>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                terrains.AddRange(root.GetComponentsInChildren<Terrain>(true));
            }

            return terrains;
        }

        private static NavMeshSurface EnsureSurface(Scene scene)
        {
            GameObject root = WorldSceneSkeleton.FindRoot(scene, RootName);
            if (root == null)
            {
                root = new GameObject(RootName);
                SceneManager.MoveGameObjectToScene(root, scene); // in case another scene is active
                Undo.RegisterCreatedObjectUndo(root, UndoLabel);
            }

            // The data is placed relative to this object, so it stays at the origin.
            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;

            // Same agent type, geometry and area as the bake, so the Inspector shows what was used.
            NavMeshSurface surface = GeneratedObjects.GetOrAddComponent<NavMeshSurface>(root);
            surface.agentTypeID = AgentTypeId;
            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = AllLayers;
            surface.defaultArea = WalkableArea;
            return surface;
        }

        private static NavMeshData SaveData(NavMeshData built)
        {
            built.name = DataName;
            var asset = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshDataPath);
            if (asset == null)
            {
                if (!AssetDatabase.IsValidFolder(NavMeshFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Environment", "NavMesh");
                }

                AssetDatabase.CreateAsset(built, NavMeshDataPath);
                return built;
            }

            // Overwrite in place, so the asset keeps its GUID and the scene's reference to it does not change.
            EditorUtility.CopySerialized(built, asset);
            Object.DestroyImmediate(built);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        private static float SurfaceArea(NavMeshTriangulation mesh)
        {
            float area = 0f;
            for (int i = 0; i < mesh.indices.Length; i += 3)
            {
                Vector3 a = mesh.vertices[mesh.indices[i]];
                Vector3 b = mesh.vertices[mesh.indices[i + 1]];
                Vector3 c = mesh.vertices[mesh.indices[i + 2]];
                area += Vector3.Cross(b - a, c - a).magnitude * 0.5f;
            }

            return area;
        }
    }
}
