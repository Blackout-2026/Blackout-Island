using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>FD1: append a fixed forest supplement, preserving terrain painting and every existing tree instance.</summary>
    public static class ForestDensityBuilder
    {
        private const string Prefabs = "Assets/Environment/Vegetation/";
        private const string Models = "Assets/Models/Custom/SM_Forest_";
        private const string Materials = "Assets/Materials/Models/";
        private const string Textures = "Assets/Textures/Models/";
        public static readonly string[] TreeNames = { "Pine", "PineB", "PineC", "PineD", "SmallTreeA", "SmallTreeB", "SmallTreeC" };
        public static readonly string[] RockNames = { "RockWide", "RockRound" };
        public static readonly string[] BushNames = { "BushWide", "BushUpright" };
        private static string Evidence => Path.Combine(Path.GetTempPath(), "blackout-FD1");

        private static List<MarkerSpec> Markers()
        {
            var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            if (layout == null) throw new InvalidOperationException("The existing BunkerLayout is required.");
            return MarkerLayout.Build(layout);
        }

        [MenuItem("Blackout/World/Inspect Forest Density")]
        public static void Inspect()
        {
            SceneReport.WithEnvironment(scene =>
            {
                Terrain terrain = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None)
                    .First(t => t.terrainData == AssetDatabase.LoadAssetAtPath<TerrainData>(ForestModelSwap.TerrainDataPath));
                string report = Inventory(terrain) + VegetationBudget.Report(terrain, Markers());
                Directory.CreateDirectory(Evidence);
                string phase = System.Environment.GetEnvironmentVariable("BLACKOUT_FOREST_PHASE") ?? "inspection";
                File.WriteAllText(Path.Combine(Evidence, phase + "-inventory.txt"), report);
                Debug.Log("[FD1] " + report);
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null) Capture(terrain, phase);
            });
        }

        public static string Inventory(Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            var lines = new List<string>();
            for (int i = 0; i < data.treePrototypes.Length; i++)
                lines.Add($"{data.treePrototypes[i].prefab.name}: {data.treeInstances.Count(t => t.prototypeIndex == i)}");
            for (int i = 0; i < data.detailPrototypes.Length; i++)
            {
                int[,] map = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i);
                long count = 0;
                foreach (int value in map) count += value;
                lines.Add($"detail {data.detailPrototypes[i].prototype.name}: {count}");
            }
            return string.Join("\n", lines) + "\n";
        }

        [MenuItem("Blackout/World/Add Dense Varied Forest")]
        public static void Apply()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Use Edit mode.");
            BuildPrefabs();
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(ForestModelSwap.TerrainDataPath);
            if (data == null) throw new InvalidOperationException("Existing island terrain is required.");
            VegetationSet plan = ForestDensityLayout.Generate(VegetationScatter.HunterClearings(Markers()));
            TreeInstance[] before = data.treeInstances;
            string paintedBefore = PaintedSignature(data);
            var prototypes = data.treePrototypes.ToList();
            int[] trees = TreeNames.Select(n => TreeIndex(prototypes, n)).ToArray();
            int[] rocks = RockNames.Select(n => TreeIndex(prototypes, n)).ToArray();
            if (prototypes.Count != data.treePrototypes.Length) data.treePrototypes = prototypes.ToArray();
            var instances = before.ToList();
            int addedTrees = Append(data, instances, plan.Trees, trees, ForestDensityLayout.TreeSpacing);
            int addedRocks = Append(data, instances, plan.Rocks, rocks, 1.8f);
            if (instances.Count != before.Length) data.SetTreeInstances(instances.ToArray(), false);
            // Append our own detail prototypes, never reset resolution, scatter mode, grass or painted bush layers.
            var details = data.detailPrototypes.ToList();
            foreach (string name in BushNames)
            {
                GameObject prefab = LoadPrefab(name);
                if (details.All(p => p.prototype != prefab)) details.Add(new DetailPrototype
                {
                    prototype = prefab, usePrototypeMesh = true, useInstancing = true,
                    renderMode = DetailRenderMode.VertexLit, minWidth = 0.75f, maxWidth = 1.25f,
                    minHeight = 0.75f, maxHeight = 1.25f, noiseSeed = 50432 + Array.IndexOf(BushNames, name),
                    noiseSpread = 0.1f, useDensityScaling = true, holeEdgePadding = 0f,
                });
            }
            if (details.Count != data.detailPrototypes.Length) data.detailPrototypes = details.ToArray();
            for (int variant = 0; variant < BushNames.Length; variant++)
            {
                int index = details.FindIndex(p => p.prototype == LoadPrefab(BushNames[variant]));
                var map = new int[data.detailHeight, data.detailWidth];
                foreach (VegetationInstance plant in plan.Bushes.Where(p => p.Variant == variant))
                {
                    Vector2Int cell = TerrainPainter.DetailCell(plant.X, plant.Z, data.detailResolution);
                    map[cell.y, cell.x]++;
                }
                data.SetDetailLayer(0, 0, index, map);
            }
            string paintedAfter = PaintedSignature(data);
            if (paintedBefore != paintedAfter || !before.SequenceEqual(data.treeInstances.Take(before.Length)))
                throw new InvalidOperationException("Forest addition changed existing painting or tree instances.");
            Directory.CreateDirectory(Evidence);
            File.WriteAllText(Path.Combine(Evidence, "preservation.txt"),
                $"Existing tree instances preserved: {before.Length}\nHeight/splat/original detail signatures unchanged:\n{paintedAfter}");
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            // The existing baker includes all terrain tree capsule colliders and preserves the saved NavMesh GUID.
            if (addedTrees + addedRocks > 0) NavMeshBaker.BakeNavMesh();
            LayoutValidator.ValidateLayout();
            Debug.Log($"[FD1] Added {addedTrees} trees, {addedRocks} rocks; {plan.Bushes.Count} managed bushes. Existing instances retained.");
            Inspect();
        }

        private static string PaintedSignature(TerrainData data)
        {
            var text = new StringBuilder();
            using (SHA256 sha = SHA256.Create())
            {
                Action<Array> add = values =>
                {
                    var bytes = new byte[Buffer.ByteLength(values)];
                    Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
                    text.AppendLine(BitConverter.ToString(sha.ComputeHash(bytes)));
                };
                add(data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution));
                add(data.GetAlphamaps(0, 0, data.alphamapWidth, data.alphamapHeight));
                for (int i = 0; i < data.detailPrototypes.Length; i++)
                    if (!BushNames.Contains(data.detailPrototypes[i].prototype.name))
                        add(data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, i));
            }
            return text.ToString();
        }

        private static int TreeIndex(List<TreePrototype> prototypes, string name)
        {
            GameObject prefab = LoadPrefab(name);
            int index = prototypes.FindIndex(p => p.prefab == prefab);
            if (index >= 0) return index;
            prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
            return prototypes.Count - 1;
        }

        public static int Append(TerrainData data, List<TreeInstance> instances, List<VegetationInstance> plan,
            int[] prototypes, float spacing)
        {
            int added = 0;
            foreach (VegetationInstance plant in plan)
            {
                bool occupied = instances.Any(t =>
                {
                    Vector3 point = Vector3.Scale(t.position, data.size) + IslandLayout.TerrainOrigin;
                    return (point.x - plant.X) * (point.x - plant.X) + (point.z - plant.Z) * (point.z - plant.Z) < spacing * spacing;
                });
                if (occupied) continue;
                float nx = (plant.X - IslandLayout.TerrainOrigin.x) / data.size.x;
                float nz = (plant.Z - IslandLayout.TerrainOrigin.z) / data.size.z;
                float ground = data.GetInterpolatedHeight(nx, nz) + IslandLayout.TerrainOrigin.y;
                instances.Add(TerrainPainter.MakeTreeInstance(plant, ground, prototypes[plant.Variant]));
                added++;
            }
            return added;
        }

        private static GameObject LoadPrefab(string name)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Prefabs + name + ".prefab");
            if (prefab == null) throw new InvalidOperationException("Missing forest prefab " + name);
            return prefab;
        }

        private static void BuildPrefabs()
        {
            Material bark = Material("M_SmallTree_Bark", "T_SmallTree_Bark_BaseColor", false);
            Material leaf = Material("M_SmallTree_Leaves", "T_SmallTree_Leaves_BaseColor", true);
            var rock = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Forest_Rock.mat");
            var bush = AssetDatabase.LoadAssetAtPath<Material>(Materials + "M_Forest_Bush.mat");
            foreach (string name in TreeNames.Skip(4).Concat(RockNames).Concat(BushNames))
            {
                string path = Prefabs + name + ".prefab";
                // These seven prefabs are wholly managed by this builder; never touch other vegetation prefabs.
                bool exists = File.Exists(path);
                GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(name);
                try
                {
                    var model = AssetDatabase.LoadAssetAtPath<GameObject>(Models + name + ".fbx");
                    if (model == null) throw new InvalidOperationException("Convert source model first: " + name);
                    bool detail = BushNames.Contains(name);
                    if (root.transform.childCount == 0 && root.GetComponent<MeshFilter>() == null)
                    {
                        if (detail)
                        {
                            root.AddComponent<MeshFilter>().sharedMesh = model.GetComponentInChildren<MeshFilter>().sharedMesh;
                            root.AddComponent<MeshRenderer>();
                        }
                        else
                        {
                            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                            instance.transform.SetParent(root.transform, false);
                        }
                    }
                    foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
                    {
                        renderer.sharedMaterials = name.StartsWith("SmallTree", StringComparison.Ordinal)
                            ? new[] { bark, leaf } : new[] { detail ? bush : rock };
                        renderer.shadowCastingMode = detail ? ShadowCastingMode.Off : ShadowCastingMode.On;
                        renderer.receiveShadows = true;
                    }
                    if (!detail)
                    {
                        LODGroup group = root.GetComponent<LODGroup>();
                        if (group == null) group = root.AddComponent<LODGroup>();
                        // Unity automatically imports a second LODGroup from the FBX's _LOD names.
                        // Keep one owner for these renderers; removal is an override on our new prefab only.
                        foreach (LODGroup nested in root.GetComponentsInChildren<LODGroup>(true))
                            if (nested != group) Object.DestroyImmediate(nested);
                        var levels = new LOD[3];
                        float[] heights = name.StartsWith("SmallTree", StringComparison.Ordinal)
                            ? new[] { 0.35f, 0.14f, 0.02f } : new[] { 0.2f, 0.07f, 0.015f };
                        for (int i = 0; i < levels.Length; i++) levels[i] = new LOD(heights[i],
                            root.GetComponentsInChildren<MeshRenderer>(true).Where(r => r.name.EndsWith("_LOD" + i, StringComparison.Ordinal)).Cast<Renderer>().ToArray());
                        group.SetLODs(levels);
                        group.RecalculateBounds();
                        if (name.StartsWith("SmallTree", StringComparison.Ordinal))
                        {
                            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                            if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
                            capsule.radius = 0.2f;
                            capsule.height = name == "SmallTreeB" ? 3f : 5f;
                            capsule.center = Vector3.up * capsule.height * 0.5f;
                        }
                        else
                        {
                            CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
                            if (capsule == null) capsule = root.AddComponent<CapsuleCollider>();
                            capsule.direction = 1;
                            capsule.radius = name == "RockWide" ? 0.65f : 0.45f;
                            capsule.height = capsule.radius * 2;
                            capsule.center = Vector3.zero;
                        }
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    if (exists) PrefabUtility.UnloadPrefabContents(root);
                    else Object.DestroyImmediate(root);
                }
            }
        }

        private static Material Material(string name, string texture, bool alpha)
        {
            string path = Materials + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            var map = (TextureImporter)AssetImporter.GetAtPath(Textures + texture + ".png");
            map.alphaIsTransparency = alpha;
            map.mipMapsPreserveCoverage = alpha;
            map.alphaTestReferenceValue = 0.4f;
            map.SaveAndReimport();
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(Textures + texture + ".png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_Smoothness", 0.12f);
            material.SetFloat("_Cull", alpha ? 0f : 2f);
            material.SetFloat("_AlphaClip", alpha ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.4f);
            if (alpha)
            {
                material.EnableKeyword("_ALPHATEST_ON");
                material.SetOverrideTag("RenderType", "TransparentCutout");
                material.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                string normalPath = Textures + "T_SmallTree_Bark_Normal.png";
                var importer = (TextureImporter)AssetImporter.GetAtPath(normalPath);
                importer.textureType = TextureImporterType.NormalMap;
                importer.SaveAndReimport();
                material.SetTexture("_BumpMap", AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath));
                material.EnableKeyword("_NORMALMAP");
            }
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Capture(Terrain terrain, string phase)
        {
            Scene previousScene = SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(terrain.gameObject.scene);
            var cameraObject = new GameObject("FD1_TemporaryReviewCamera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.enabled = false;
            camera.fieldOfView = 72;
            camera.nearClipPlane = 0.08f;
            camera.farClipPlane = 350;
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var target = new RenderTexture(1280, 720, 24, RenderTextureFormat.ARGB32);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            RenderTexture previous = RenderTexture.active;
            try
            {
                target.Create();
                var views = VegetationBudget.Viewpoints(Markers());
                foreach (Viewpoint view in views)
                {
                    camera.transform.SetPositionAndRotation(view.Eye, Quaternion.Euler(0, view.Yaw, 0));
                    var request = new UniversalRenderPipeline.SingleCameraRequest { destination = target };
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderPipeline.SubmitRenderRequest(camera, request);
                    RenderTexture.active = target;
                    pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                    pixels.Apply();
                    File.WriteAllBytes(Path.Combine(Evidence, phase + "-" + views.IndexOf(view) + ".png"), pixels.EncodeToPNG());
                }
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(pixels);
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                SceneManager.SetActiveScene(previousScene);
            }
        }
    }
}
