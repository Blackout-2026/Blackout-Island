using System;
using System.Collections.Generic;
using System.Diagnostics;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Build Terrain: builds the island in Environment.unity.
    /// - Terrain/IslandTerrain: a Unity Terrain whose heights come from <see cref="IslandHeightField"/>. Its
    ///   TerrainData is saved as Assets/Environment/Terrain/IslandTerrainData.asset.
    /// - A terrain hole over the top of the bunker ramp, where the ramp's roof would stick out of the plateau.
    /// - Terrain/IslandBoundary: invisible walls just offshore, so the player cannot leave the island.
    /// - Beach/Water, Beach/Dock and Beach/Boat: the greybox beach (see <see cref="BeachGreyboxBuilder"/>).
    /// Re-runnable and deterministic: every run writes the same heights, hole and walls, reusing the objects it
    /// generated before, so a re-run leaves the scene file unchanged (see <see cref="GeneratedObjects"/>).
    /// Hand-sculpted heights are overwritten, so in the Editor it asks first.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.TerrainBuilder.BuildTerrain
    /// </summary>
    public static class TerrainBuilder
    {
        public const string TerrainFolder = "Assets/Environment/Terrain";
        public const string TerrainDataPath = TerrainFolder + "/IslandTerrainData.asset";

        /// <summary>One boundary wall every 2 degrees around the island centre.</summary>
        public const int BoundaryRays = 180;

        private const string TerrainRootName = "Terrain";
        private const string BeachRootName = "Beach";
        private const string TerrainObjectName = "IslandTerrain";
        private const string BoundaryName = "IslandBoundary";
        private const string RampRoomName = "BunkerEntranceRamp";
        private const string UndoLabel = "Build Terrain";

        // Walls reach from below the sea bed to far above anything the player can jump from.
        private const float WallBottomY = -8f;
        private const float WallTopY = 30f;
        private const float WallThickness = 1f;

        [MenuItem("Blackout/World/Build Terrain")]
        public static void BuildTerrain()
        {
            if (!Application.isBatchMode
                && AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath) != null
                && !EditorUtility.DisplayDialog(UndoLabel,
                    "Regenerate the island? Hand-sculpted changes to IslandTerrainData will be overwritten.",
                    "Regenerate", "Cancel"))
            {
                return;
            }

            Stopwatch timer = Stopwatch.StartNew();

            // Opens (or creates) the scene and adds any missing roots. It never removes anything.
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Build Terrain cancelled (the Environment scene is not open).");
                return;
            }

            BunkerLayout layout = BunkerLayoutAssetBuilder.LoadOrCreate();
            Rect hole = RampHoleRect(layout.FindRoom(RampRoomName), layout.wallThickness);

            TerrainData data = LoadOrCreateTerrainData();
            data.SetHeights(0, 0, BuildHeights(out float minY, out float maxY));
            data.SetHoles(0, 0, BuildHoles(data.holesResolution, hole, out int holeCells));
            EditorUtility.SetDirty(data);

            Transform root = WorldSceneSkeleton.FindRoot(scene, TerrainRootName).transform;
            Terrain terrain = EnsureTerrain(root, data);
            Vector2[] coast = IslandBoundary.TraceCoast(BoundaryRays);
            float perimeter = BuildBoundary(root, coast);
            string beach = BeachGreyboxBuilder.Build(WorldSceneSkeleton.FindRoot(scene, BeachRootName).transform);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            string material = terrain.materialTemplate != null ? terrain.materialTemplate.name : "none";
            Debug.Log($"[World] Terrain built in {timer.ElapsedMilliseconds} ms: {data.size.x} x {data.size.z} m, "
                + $"heightmap {data.heightmapResolution}, ground y {minY:0.00} .. {maxY:0.00}, material {material}; "
                + $"ramp hole {holeCells} cells over x {hole.xMin:0.0} .. {hole.xMax:0.0}, z {hole.yMin:0.0} .. {hole.yMax:0.0}; "
                + $"boundary {coast.Length} walls, {perimeter:0} m; beach: {beach}.");
        }

        /// <summary>
        /// The part of the ramp (interior x and z) whose roof would stick out of the plateau: from the ramp's top
        /// end to where the top of its roof slab drops below the plateau. Further north the ground covers the ramp.
        /// </summary>
        public static Rect RampHoleRect(Room ramp, float wallThickness)
        {
            if (ramp == null)
            {
                throw new ArgumentNullException(nameof(ramp), $"BunkerLayout has no room named {RampRoomName}.");
            }

            float southZ = ramp.position.z - ramp.size.z * 0.5f;
            float northZ = ramp.position.z + ramp.size.z * 0.5f;
            float endZ = northZ;
            if (ramp.floorDropNorth > 0f)
            {
                float topFloorY = ramp.position.y + ramp.floorDropNorth * 0.5f; // position.y is the floor at the centre
                float roofAbovePlateau = topFloorY + ramp.size.y + wallThickness - IslandLayout.PlateauY;
                float dropPerMetre = ramp.floorDropNorth / ramp.size.z;
                endZ = Mathf.Min(northZ, southZ + roofAbovePlateau / dropPerMetre);
            }

            float halfX = ramp.size.x * 0.5f;
            return Rect.MinMaxRect(ramp.position.x - halfX, southZ, ramp.position.x + halfX, endZ);
        }

        private static TerrainData LoadOrCreateTerrainData()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                if (!AssetDatabase.IsValidFolder(TerrainFolder))
                {
                    AssetDatabase.CreateFolder("Assets/Environment", "Terrain");
                }

                data = new TerrainData();
                AssetDatabase.CreateAsset(data, TerrainDataPath);
            }

            // Resolution first: changing it can rescale the terrain, so the size is set afterwards.
            data.heightmapResolution = IslandLayout.HeightmapResolution;
            data.size = IslandLayout.TerrainSize;
            return data;
        }

        // Heights are stored [z, x] and normalised 0 .. 1 over the terrain's height range (y -8 .. 24).
        private static float[,] BuildHeights(out float minY, out float maxY)
        {
            int res = IslandLayout.HeightmapResolution;
            Vector3 origin = IslandLayout.TerrainOrigin;
            Vector3 size = IslandLayout.TerrainSize;
            var heights = new float[res, res];
            minY = float.MaxValue;
            maxY = float.MinValue;
            for (int iz = 0; iz < res; iz++)
            {
                float z = origin.z + size.z * iz / (res - 1);
                for (int ix = 0; ix < res; ix++)
                {
                    float x = origin.x + size.x * ix / (res - 1);
                    float y = IslandHeightField.Sample(x, z);
                    minY = Mathf.Min(minY, y);
                    maxY = Mathf.Max(maxY, y);
                    heights[iz, ix] = Mathf.Clamp01((y - origin.y) / size.y);
                }
            }

            return heights;
        }

        // true = ground, false = hole. A cell is cut if it overlaps the rectangle at all, so no ground is left
        // inside the ramp; the overrun (under 0.4 m with a 0.78 m cell) is hidden in the ramp and lobby walls.
        private static bool[,] BuildHoles(int res, Rect hole, out int cut)
        {
            Vector3 origin = IslandLayout.TerrainOrigin;
            float cellX = IslandLayout.TerrainSize.x / res;
            float cellZ = IslandLayout.TerrainSize.z / res;
            var solid = new bool[res, res];
            cut = 0;
            for (int iz = 0; iz < res; iz++)
            {
                float zMin = origin.z + iz * cellZ;
                for (int ix = 0; ix < res; ix++)
                {
                    float xMin = origin.x + ix * cellX;
                    bool inHole = xMin + cellX > hole.xMin && xMin < hole.xMax && zMin + cellZ > hole.yMin && zMin < hole.yMax;
                    solid[iz, ix] = !inHole;
                    if (inHole)
                    {
                        cut++;
                    }
                }
            }

            return solid;
        }

        private static Terrain EnsureTerrain(Transform root, TerrainData data)
        {
            Transform existing = root.Find(TerrainObjectName);
            Terrain terrain = existing != null ? existing.GetComponent<Terrain>() : null;
            if (terrain == null)
            {
                GameObject go = Terrain.CreateTerrainGameObject(data); // Terrain + TerrainCollider
                go.name = TerrainObjectName;
                go.transform.SetParent(root, false);
                Undo.RegisterCreatedObjectUndo(go, UndoLabel);
                terrain = go.GetComponent<Terrain>();
            }

            terrain.terrainData = data;
            var terrainCollider = terrain.GetComponent<TerrainCollider>();
            if (terrainCollider == null)
            {
                terrainCollider = terrain.gameObject.AddComponent<TerrainCollider>();
            }

            terrainCollider.terrainData = data;
            terrain.transform.SetPositionAndRotation(IslandLayout.TerrainOrigin, Quaternion.identity);
            terrain.drawInstanced = true; // GPU-instanced terrain patches: fewer draw calls

            // The render pipeline's terrain material (URP Terrain Lit) if none is set. W6 adds the terrain layers.
            if (terrain.materialTemplate == null && GraphicsSettings.currentRenderPipeline != null)
            {
                terrain.materialTemplate = GraphicsSettings.currentRenderPipeline.defaultTerrainMaterial;
            }

            return terrain;
        }

        private static float BuildBoundary(Transform root, Vector2[] coast)
        {
            Transform container = GeneratedObjects.FindOrCreateChild(root, BoundaryName, UndoLabel);
            container.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            container.localScale = Vector3.one;

            var built = new HashSet<string>();
            float height = WallTopY - WallBottomY;
            float perimeter = 0f;
            for (int i = 0; i < coast.Length; i++)
            {
                Vector2 a = coast[i];
                Vector2 b = coast[(i + 1) % coast.Length];
                Vector2 edge = b - a;
                Vector2 middle = (a + b) * 0.5f;
                perimeter += edge.magnitude;

                GameObject wall = GeneratedObjects.GetOrCreate(container, i.ToString("000"), built, UndoLabel);
                wall.transform.SetPositionAndRotation(
                    new Vector3(middle.x, (WallBottomY + WallTopY) * 0.5f, middle.y),
                    Quaternion.LookRotation(new Vector3(edge.x, 0f, edge.y)));
                wall.transform.localScale = Vector3.one;

                // One wall thickness longer than its edge, so neighbouring walls overlap at the corners.
                var box = GeneratedObjects.GetOrAddComponent<BoxCollider>(wall);
                box.center = Vector3.zero;
                box.size = new Vector3(WallThickness, height, edge.magnitude + WallThickness);
            }

            GeneratedObjects.RemoveStale(container, built);
            return perimeter;
        }
    }
}
