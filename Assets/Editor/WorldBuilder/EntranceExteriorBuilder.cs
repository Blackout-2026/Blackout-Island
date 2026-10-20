using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Builds the missing entrance exterior around W3's existing lobby/ramp.
    /// Prototype dimensions adapted to the frozen 1.4 x 2.3 m doorway. Owner: World Builder.</summary>
    public static class EntranceExteriorBuilder
    {
        private const string Label = "Build Entrance Exterior";
        private const string Container = "EntranceExterior";
        private const float FrontZ = 47.8f;
        private const float RoofY = 14f;

        [MenuItem("Blackout/World/Build Entrance Exterior")]
        public static void Build()
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Single);
            }
            BunkerLayout layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            DoorOpening door = lobby.openings.Find(opening => opening.id == "D-Bunker");
            Transform root = GeneratedObjects.FindOrCreateChild(
                WorldSceneSkeleton.FindRoot(scene, "BunkerExterior").transform, Container, Label);
            if (root.localPosition != Vector3.zero || root.localRotation != Quaternion.identity || root.localScale != Vector3.one)
                throw new InvalidOperationException("EntranceExterior must retain its identity transform.");
            var built = new HashSet<string>();
            Material concrete = LoadMaterial("Concrete");
            Material dark = LoadMaterial("ConcreteStained");
            Material metal = LoadMaterial("MetalPlate");
            Material hazard = LoadMaterial("HazardStripe");
            float halfDoor = door.width * 0.5f;
            float doorTop = lobby.position.y + door.height;

            // Facade is in front of the existing lobby wall, so its visible face cannot z-fight with W3.
            Box(root, built, "FacadeWest", new Vector3(-6.4f, 7f, FrontZ), new Vector3(-halfDoor, RoofY, 48f), concrete);
            Box(root, built, "FacadeEast", new Vector3(halfDoor, 7f, FrontZ), new Vector3(6.4f, RoofY, 48f), concrete);
            Box(root, built, "FacadeLintel", new Vector3(-halfDoor, doorTop, FrontZ), new Vector3(halfDoor, RoofY, 48f), concrete);
            // Outer side walls sit beyond the lobby shell's x +/-6 faces. W3 interior walls remain intact.
            Box(root, built, "SideWest", new Vector3(-6.4f, 7f, FrontZ), new Vector3(-6f, RoofY, 62f), concrete);
            Box(root, built, "SideEast", new Vector3(6f, 7f, FrontZ), new Vector3(6.4f, RoofY, 62f), concrete);
            Box(root, built, "Roof", new Vector3(-6.8f, RoofY, 47.4f), new Vector3(6.8f, 14.5f, 62.5f), dark);
            Box(root, built, "ParapetWest", new Vector3(-6.8f, 14.5f, 47.4f), new Vector3(-6.5f, 15f, 62.5f), dark);
            Box(root, built, "ParapetEast", new Vector3(6.5f, 14.5f, 47.4f), new Vector3(6.8f, 15f, 62.5f), dark);
            Box(root, built, "ParapetFront", new Vector3(-6.5f, 14.5f, 47.4f), new Vector3(6.5f, 15f, 47.7f), dark);
            Box(root, built, "DoorCanopy", new Vector3(-2.6f, doorTop + 0.25f, 46.4f), new Vector3(2.6f, doorTop + 0.45f, FrontZ), dark);
            Box(root, built, "DoorTrimWest", new Vector3(-halfDoor - 0.22f, 10f, 47.65f), new Vector3(-halfDoor, doorTop + 0.22f, FrontZ), hazard);
            Box(root, built, "DoorTrimEast", new Vector3(halfDoor, 10f, 47.65f), new Vector3(halfDoor + 0.22f, doorTop + 0.22f, FrontZ), hazard);
            Box(root, built, "DoorTrimTop", new Vector3(-halfDoor - 0.22f, doorTop, 47.65f), new Vector3(halfDoor + 0.22f, doorTop + 0.22f, FrontZ), hazard);
            // Covers the rounded-out terrain-hole cell at the sill; 4 cm above the plateau is walkable.
            Box(root, built, "Apron", new Vector3(-7f, 9.5f, 40f), new Vector3(7f, 10.04f, FrontZ), dark);

            // Reference retaining wall, split around the ramp. At z62 the ramp roof is below y9.3.
            Box(root, built, "RetainingWest", new Vector3(-24f, 7f, 62f), new Vector3(-2.6f, 15.2f, 63.4f), concrete);
            Box(root, built, "RetainingEast", new Vector3(2.6f, 7f, 62f), new Vector3(24f, 15.2f, 63.4f), concrete);
            Box(root, built, "RetainingBridge", new Vector3(-2.6f, 9.7f, 62f), new Vector3(2.6f, 15.2f, 63.4f), concrete);
            Box(root, built, "RetainingCap", new Vector3(-24.2f, 15.2f, 61.8f), new Vector3(24.2f, 15.45f, 63.6f), dark);
            foreach (float x in new[] { -21f, -15f, -9f, 9f, 15f, 21f })
                Box(root, built, "Pilaster" + x.ToString("+00;-00"), new Vector3(x - 0.45f, 7f, 61.4f), new Vector3(x + 0.45f, 15.5f, 62f), dark);
            foreach (int side in new[] { -1, 1 })
            {
                float wallX = side * 6.4f;
                Box(root, built, "VentBack" + side, new Vector3(wallX - 0.08f, 12f, 54.4f), new Vector3(wallX + 0.08f, 13f, 56f), metal);
                for (int slat = 0; slat < 6; slat++)
                    Box(root, built, "VentSlat" + side + "_" + slat, new Vector3(wallX - 0.12f, 12.1f + slat * 0.15f, 54.5f),
                        new Vector3(wallX + 0.12f, 12.16f + slat * 0.15f, 55.9f), metal);
                float pipeX = side * 12f;
                Box(root, built, "Drain" + side, new Vector3(pipeX - 0.09f, 10f, 61.76f), new Vector3(pipeX + 0.09f, 15f, 61.94f), metal);
            }
            GeneratedObjects.RemoveStale(root, built);
            var terrain = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainBuilder.TerrainDataPath);
            if (terrain == null) throw new InvalidOperationException("Build Terrain before the entrance exterior.");
            int removed = CutLobbyTerrain(terrain, layout);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Entrance exterior built: {built.Count} boxes; door {door.width:0.0} x {door.height:0.0} m; "
                + $"roof y14; retaining bridge underside y9.7; {removed} additional terrain-hole cells.");
        }

        /// <summary>Only cuts cells under the lobby and sill; retains every existing ramp/hand-authored hole.
        /// Build Terrain calls this too, so rebuilding W2 cannot reintroduce the lobby's terrain z-fighting.</summary>
        public static int CutLobbyTerrain(TerrainData terrain, BunkerLayout layout)
        {
            Room lobby = layout.FindRoom("BunkerEntranceLobby");
            float wall = layout.wallThickness;
            Rect footprint = Rect.MinMaxRect(lobby.position.x - lobby.size.x * 0.5f - wall,
                lobby.position.z - lobby.size.z * 0.5f - wall,
                lobby.position.x + lobby.size.x * 0.5f + wall, lobby.position.z + lobby.size.z * 0.5f + wall);
            int resolution = terrain.holesResolution;
            bool[,] holes = terrain.GetHoles(0, 0, resolution, resolution);
            Vector3 origin = IslandLayout.TerrainOrigin;
            float cell = terrain.size.x / resolution;
            int cut = 0;
            for (int z = 0; z < resolution; z++)
                for (int x = 0; x < resolution; x++)
                {
                    var cellRect = new Rect(origin.x + x * cell, origin.z + z * cell, cell, cell);
                    if (holes[z, x] && cellRect.Overlaps(footprint)) { holes[z, x] = false; cut++; }
                }
            if (cut > 0)
            {
                terrain.SetHoles(0, 0, holes);
                EditorUtility.SetDirty(terrain);
            }
            return cut;
        }

        private static Material LoadMaterial(string surface)
        {
            Material result = AssetDatabase.LoadAssetAtPath<Material>(MaterialLibraryBuilder.MaterialPath(surface));
            if (result == null) throw new InvalidOperationException("Build Material Library before the entrance exterior.");
            return result;
        }

        private static void Box(Transform root, HashSet<string> built, string name, Vector3 min, Vector3 max, Material material)
        {
            GameObject go = GeneratedObjects.GetOrCreate(root, name, built, Label, PrimitiveType.Cube);
            go.transform.SetPositionAndRotation((min + max) * 0.5f, Quaternion.identity);
            go.transform.localScale = max - min;
            go.layer = PhysicsLayers.Level;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            var filter = go.GetComponent<MeshFilter>();
            if (filter.sharedMesh.name != go.name + BoxUvMesh.NameSuffix) filter.sharedMesh = new Mesh();
            BoxUvMesh.Build(filter.sharedMesh, go.transform);
            GameObjectUtility.SetStaticEditorFlags(go, StaticFlagRules.Environment);
        }

        public static void BuildAndVerify()
        {
            Build();
            string[] paths = { WorldSceneSkeleton.ScenePath, TerrainBuilder.TerrainDataPath };
            byte[][] before = paths.Select(File.ReadAllBytes).ToArray();
            Build();
            for (int i = 0; i < paths.Length; i++)
                if (!before[i].SequenceEqual(File.ReadAllBytes(paths[i])))
                    throw new InvalidOperationException("Entrance exterior changed on repeat: " + paths[i]);
            Physics.SyncTransforms();
            BunkerLayout layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            Room ramp = layout.FindRoom("BunkerEntranceRamp");
            for (float z = 48f; z <= 69.5f; z += 0.25f)
            {
                float floor = z <= 52.4f ? 10f : BunkerShell.FloorY(ramp, z);
                // A flat-bottomed box intersects an uphill floor. Use the 1.8 m player capsule, with
                // 7 cm above the centre floor sample to clear its contact with the 23.6-degree slope.
                Collider[] hits = Physics.OverlapCapsule(new Vector3(0f, floor + 0.42f, z),
                    new Vector3(0f, floor + 1.52f, z), 0.35f, 1 << PhysicsLayers.Level, QueryTriggerInteraction.Ignore);
                if (hits.Length > 0)
                    throw new InvalidOperationException("Entrance/ramp player headroom obstructed at z=" + z + ": " + hits[0].name);
            }
            Debug.Log("[World] Entrance verification: repeat scene/terrain identical; 1.8 m player clearance through lobby and ramp.");
            NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
        }
    }
}
