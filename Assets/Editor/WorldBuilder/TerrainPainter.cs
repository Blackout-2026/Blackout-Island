using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Paint Terrain And Forest: paints Terrain/IslandTerrain and plants its vegetation.
    /// - Terrain layers Sand, Grass, ForestFloor and Rock (Assets/Environment/Terrain/Layers/TL_&lt;Surface&gt;), from the
    ///   W5 surface table: a surface's BaseColor and Normal textures if it has them, its flat palette colour if not.
    /// - The splat map, from <see cref="TerrainSplat"/>: height, slope, the trail, the compound gravel and forest floor
    ///   under every tree and bush.
    /// - Trees (pines, palms, rocks) as terrain tree instances and bushes and grass as terrain details, placed by
    ///   <see cref="VegetationScatter"/> with clearings around the Hunters' waypoints. Prefabs come from
    ///   <see cref="VegetationLibrary"/>: yours if present, else the placeholders.
    /// - Tree and detail draw distances on the terrain, then a visible-triangle report (<see cref="VegetationBudget"/>).
    /// Run Build Terrain first, and Bake NavMesh and Validate Layout after: trees and rocks with colliders change where
    /// agents can walk. Re-runnable and deterministic (fixed seeds): every run writes the same layers, splat map, trees and
    /// details. Hand painting on the terrain is overwritten, so in the Editor it asks first.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.TerrainPainter.PaintTerrainAndForest
    /// </summary>
    public static class TerrainPainter
    {
        public const string LayerFolder = TerrainBuilder.TerrainFolder + "/Layers";
        public const string WhiteTexturePath = LayerFolder + "/TerrainLayerWhite.asset";

        /// <summary>Splat map texels per side: 0.78 m each over 400 m, like the heightmap, so the 3.4 m trail is 4 texels wide.</summary>
        public const int AlphamapResolution = 512;

        /// <summary>Detail cells per side (0.78 m) and per patch (32 cells = 25 m): Unity culls details patch by patch.</summary>
        public const int DetailResolution = 512;
        public const int DetailResolutionPerPatch = 32;

        /// <summary>
        /// Trees are drawn to 150 m: the outdoor fog (exponential squared, density 0.0135) leaves exp(-(0.0135 x 150)^2)
        /// = 1.7 % of a tree visible there.
        /// </summary>
        public const float TreeDistance = 150f;

        /// <summary>
        /// Bushes and grass are drawn to 60 m: a 0.75 m tuft is under 10 pixels tall there at 1080p and the fog already
        /// hides half of it (exp(-(0.0135 x 60)^2) = 52 %).
        /// </summary>
        public const float DetailDistance = 60f;

        // Forest floor painted under trees and bushes: radius per unit of scale (prototype).
        private const float TreeLitterRadius = 3.2f;
        private const float BushLitterRadius = 1.6f;
        private const float LitterReach = 2f; // the footprint fades out this far past its radius

        private const string TerrainRootName = "Terrain";
        private const string TerrainObjectName = "IslandTerrain";
        private const string UndoLabel = "Paint Terrain And Forest";

        private static readonly VegetationPrototype[] TreePrototypes =
        {
            VegetationPrototype.Pine, VegetationPrototype.Palm, VegetationPrototype.Rock, VegetationPrototype.RockSmall,
        };

        private static readonly VegetationPrototype[] DetailPrototypes = { VegetationPrototype.Bush, VegetationPrototype.Grass };

        [MenuItem("Blackout/World/Paint Terrain And Forest")]
        public static void PaintTerrainAndForest()
        {
            Stopwatch timer = Stopwatch.StartNew();
            WorldSceneSkeleton.CreateSceneSkeleton();
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.Log("[World] Paint Terrain And Forest cancelled (the Environment scene is not open).");
                return;
            }

            Transform terrainObject = WorldSceneSkeleton.FindRoot(scene, TerrainRootName).transform.Find(TerrainObjectName);
            Terrain terrain = terrainObject != null ? terrainObject.GetComponent<Terrain>() : null;
            if (terrain == null || terrain.terrainData == null)
            {
                Debug.LogError("[World] Paint Terrain And Forest: no Terrain/IslandTerrain. Run Blackout/World/Build Terrain first.");
                return;
            }

            TerrainData data = terrain.terrainData;
            if (!Application.isBatchMode && (data.terrainLayers.Length > 0 || data.treeInstanceCount > 0)
                && !EditorUtility.DisplayDialog(UndoLabel,
                    "Repaint the island? Hand-painted layers, trees and details on IslandTerrainData will be overwritten.",
                    "Repaint", "Cancel"))
            {
                return;
            }

            GameObject[] trees = ResolvePrefabs(TreePrototypes, out string treeSources);
            GameObject[] details = ResolvePrefabs(DetailPrototypes, out string detailSources);
            List<MarkerSpec> markers = MarkerLayout.Build(BunkerLayoutAssetBuilder.LoadOrCreate());
            VegetationSet set = VegetationScatter.Scatter(VegetationScatter.HunterClearings(markers));

            var report = new StringBuilder();
            data.terrainLayers = SaveLayers(report);
            data.alphamapResolution = AlphamapResolution;
            data.SetAlphamaps(0, 0, BuildAlphamaps(data, set, report));
            PlantTrees(data, set, trees, report);
            PlantDetails(data, set, details, report);
            ConfigureTerrain(terrain);
            EditorUtility.SetDirty(data);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            report.Append($"\n  prefabs: {treeSources}, {detailSources}");
            report.Append($"\n  terrain: trees drawn to {TreeDistance:0} m, bushes and grass to {DetailDistance:0} m, "
                + $"detail density {terrain.detailObjectDensity:0.##}");
            report.Append(VegetationBudget.Report(terrain, markers));
            Debug.Log($"[World] Terrain and forest painted in {timer.ElapsedMilliseconds} ms:{report}"
                + "\n  Next: Blackout/World/Bake NavMesh, then Blackout/World/Validate Layout.");
        }

        // ---- Layers ---------------------------------------------------------------------------------------------

        private static TerrainLayer[] SaveLayers(StringBuilder report)
        {
            EnsureFolder();
            Texture2D white = SaveWhiteTexture();
            var layers = new TerrainLayer[TerrainSplat.Layers.Length];
            var described = new List<string>();
            for (int i = 0; i < layers.Length; i++)
            {
                SurfaceSpec spec = SurfaceLibrary.Find(TerrainSplat.Layers[i]);
                SurfaceMaps maps = MaterialLibraryBuilder.FindMaps(spec, MaterialLibraryBuilder.TextureFolder(spec.Name), report);
                string path = LayerPath(spec.Name);
                TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(path);
                if (layer == null)
                {
                    layer = new TerrainLayer { name = "TL_" + spec.Name };
                    Configure(layer, spec, maps, white);
                    AssetDatabase.CreateAsset(layer, path);
                }
                else
                {
                    Configure(layer, spec, maps, white);
                    EditorUtility.SetDirty(layer);
                }

                layers[i] = layer;
                described.Add(maps.Textured ? $"{spec.Name} textured" : $"{spec.Name} flat #{ColorUtility.ToHtmlStringRGB(spec.FlatColour)}");
            }

            report.Append("\n  layers: ").Append(string.Join(", ", described));
            return layers;
        }

        public static string LayerPath(string surface)
        {
            return $"{LayerFolder}/TL_{surface}.terrainlayer";
        }

        /// <summary>
        /// Sets every layer property from the surface table, so the result depends only on the table and the textures.
        /// Without a BaseColor texture the layer samples a white texture tinted with the flat colour. URP Terrain Lit
        /// multiplies the texture by (remap max - remap min) without colour conversion, so the tint is given in linear
        /// space, which makes the flat terrain match the Env_ materials.
        /// </summary>
        public static void Configure(TerrainLayer layer, SurfaceSpec spec, SurfaceMaps maps, Texture2D white)
        {
            Color tint = maps.Textured ? spec.TextureTint : spec.FlatColour;
            Color linear = QualitySettings.activeColorSpace == ColorSpace.Linear ? tint.linear : tint;
            layer.diffuseTexture = maps.Textured ? maps.BaseColor : white;
            layer.normalMapTexture = maps.Normal;
            layer.normalScale = 1f;
            layer.maskMapTexture = null; // smoothness and metallic come from the constants below
            layer.tileSize = Vector2.one / spec.RepeatsPerMetre;
            layer.tileOffset = Vector2.zero;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(linear.r, linear.g, linear.b, 1f);
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = Vector4.one;
            layer.specular = Color.black;
            layer.metallic = spec.Metallic;
            layer.smoothness = spec.Smoothness;
        }

        private static Texture2D SaveWhiteTexture()
        {
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(WhiteTexturePath);
            if (existing != null)
            {
                return existing;
            }

            var white = new Texture2D(4, 4, TextureFormat.RGBA32, false, false) { name = "TerrainLayerWhite" };
            var pixels = new Color32[16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 255);
            }

            white.SetPixels32(pixels);
            white.Apply(false);
            AssetDatabase.CreateAsset(white, WhiteTexturePath);
            return white;
        }

        // ---- Splat map ------------------------------------------------------------------------------------------

        // Texel i of the splat map sits at i / (resolution - 1) across the terrain (Terrain Lit samples texel centres
        // at the terrain's edges), so x = origin + i x size / (resolution - 1).
        private static float[,,] BuildAlphamaps(TerrainData data, VegetationSet set, StringBuilder report)
        {
            int res = data.alphamapResolution;
            Vector3 origin = IslandLayout.TerrainOrigin;
            Vector3 size = data.size;
            float step = size.x / (res - 1);
            float[,] litter = BuildLitter(set, res, origin, step);

            var maps = new float[res, res, TerrainSplat.Layers.Length];
            var cover = new double[TerrainSplat.Layers.Length];
            int land = 0;
            for (int iz = 0; iz < res; iz++)
            {
                float nz = (float)iz / (res - 1);
                float z = origin.z + iz * step;
                for (int ix = 0; ix < res; ix++)
                {
                    float nx = (float)ix / (res - 1);
                    float x = origin.x + ix * step;
                    float height = origin.y + data.GetInterpolatedHeight(nx, nz);
                    float normalY = data.GetInterpolatedNormal(nx, nz).y;
                    float trail = TerrainSplat.TrailMask((float)IslandHeightField.TrailDistance(x, z));
                    Vector4 w = TerrainSplat.Weights(height, normalY, z, trail, TerrainSplat.GravelMask(x, z),
                        Mathf.Min(1f, litter[iz, ix]), TerrainSplat.Macro(x, z));
                    for (int layer = 0; layer < TerrainSplat.Layers.Length; layer++)
                    {
                        maps[iz, ix, layer] = w[layer];
                    }

                    if (height > IslandLayout.WaterY)
                    {
                        land++;
                        for (int layer = 0; layer < TerrainSplat.Layers.Length; layer++)
                        {
                            cover[layer] += w[layer];
                        }
                    }
                }
            }

            report.Append($"\n  splat map {res} x {res} ({step:0.00} m), share of the land above water:");
            for (int layer = 0; layer < cover.Length; layer++)
            {
                report.Append($" {TerrainSplat.Layers[layer]} {100.0 * cover[layer] / land:0.0} %");
            }

            return maps;
        }

        // Forest floor under every tree (3.2 m x scale) and bush (1.6 m x scale), added up texel by texel.
        private static float[,] BuildLitter(VegetationSet set, int res, Vector3 origin, float step)
        {
            var litter = new float[res, res];
            foreach (VegetationInstance tree in set.Trees)
            {
                AddFootprint(litter, res, origin, step, tree.X, tree.Z, TreeLitterRadius * tree.Scale);
            }

            foreach (VegetationInstance bush in set.Bushes)
            {
                AddFootprint(litter, res, origin, step, bush.X, bush.Z, BushLitterRadius * bush.Scale);
            }

            return litter;
        }

        private static void AddFootprint(float[,] litter, int res, Vector3 origin, float step, float x, float z, float radius)
        {
            float reach = radius + LitterReach;
            int minX = Mathf.Max(0, Mathf.FloorToInt((x - reach - origin.x) / step));
            int maxX = Mathf.Min(res - 1, Mathf.CeilToInt((x + reach - origin.x) / step));
            int minZ = Mathf.Max(0, Mathf.FloorToInt((z - reach - origin.z) / step));
            int maxZ = Mathf.Min(res - 1, Mathf.CeilToInt((z + reach - origin.z) / step));
            for (int iz = minZ; iz <= maxZ; iz++)
            {
                for (int ix = minX; ix <= maxX; ix++)
                {
                    float dx = origin.x + ix * step - x;
                    float dz = origin.z + iz * step - z;
                    litter[iz, ix] += TerrainSplat.LitterFootprint(Mathf.Sqrt(dx * dx + dz * dz), radius);
                }
            }
        }

        // ---- Trees and details ----------------------------------------------------------------------------------

        private static GameObject[] ResolvePrefabs(VegetationPrototype[] prototypes, out string sources)
        {
            var prefabs = new GameObject[prototypes.Length];
            var names = new List<string>();
            for (int i = 0; i < prototypes.Length; i++)
            {
                prefabs[i] = VegetationLibrary.Resolve(prototypes[i], out bool placeholder);
                if (prefabs[i] == null)
                {
                    VegetationPlaceholderBuilder.Build(); // first run: make the placeholders
                    prefabs[i] = VegetationLibrary.Resolve(prototypes[i], out placeholder);
                }

                names.Add($"{prototypes[i]} = {prefabs[i].name}{(placeholder ? " (placeholder)" : string.Empty)}");
            }

            sources = string.Join(", ", names);
            return prefabs;
        }

        /// <summary>The tree prototype for a scattered instance: rocks over the collider size get the collider.</summary>
        public static VegetationPrototype TreePrototypeFor(VegetationInstance instance)
        {
            switch (instance.Kind)
            {
                case VegetationKind.Palm:
                    return VegetationPrototype.Palm;
                case VegetationKind.Rock:
                    return instance.Scale > VegetationLayout.RockColliderMinScale ? VegetationPrototype.Rock : VegetationPrototype.RockSmall;
                default:
                    return VegetationPrototype.Pine;
            }
        }

        /// <summary>
        /// A terrain tree instance: position normalised over the terrain (x, z in 0 .. 1; y is the height in 0 .. 1 of
        /// the terrain's height range), sunk into the ground as the prototype does.
        /// </summary>
        public static TreeInstance MakeTreeInstance(VegetationInstance instance, float groundY, int prototypeIndex)
        {
            Vector3 origin = IslandLayout.TerrainOrigin;
            Vector3 size = IslandLayout.TerrainSize;
            return new TreeInstance
            {
                position = new Vector3((instance.X - origin.x) / size.x, (groundY - instance.Sink - origin.y) / size.y,
                    (instance.Z - origin.z) / size.z),
                widthScale = instance.Scale,
                heightScale = instance.Scale,
                rotation = instance.Yaw,
                color = Color.white,
                lightmapColor = Color.white,
                prototypeIndex = prototypeIndex,
            };
        }

        private static void PlantTrees(TerrainData data, VegetationSet set, GameObject[] prefabs, StringBuilder report)
        {
            var prototypes = new TreePrototype[prefabs.Length];
            for (int i = 0; i < prefabs.Length; i++)
            {
                prototypes[i] = new TreePrototype { prefab = prefabs[i], bendFactor = 0f };
            }

            data.treePrototypes = prototypes;
            var instances = new List<TreeInstance>();
            var counts = new int[prefabs.Length];
            foreach (List<VegetationInstance> list in new[] { set.Trees, set.Rocks })
            {
                foreach (VegetationInstance instance in list)
                {
                    int index = System.Array.IndexOf(TreePrototypes, TreePrototypeFor(instance));
                    instances.Add(MakeTreeInstance(instance, GroundY(data, instance.X, instance.Z), index));
                    counts[index]++;
                }
            }

            data.SetTreeInstances(instances.ToArray(), false);
            report.Append($"\n  trees: {instances.Count} (");
            for (int i = 0; i < counts.Length; i++)
            {
                report.Append(i > 0 ? ", " : string.Empty).Append($"{TreePrototypes[i]} {counts[i]}");
            }

            report.Append($"); cleared around the Hunters' {VegetationLayout.ClearingRadius:0} m clearings: "
                + $"pine {set.Cleared[(int)VegetationKind.Pine]}, palm {set.Cleared[(int)VegetationKind.Palm]}, "
                + $"rock {set.Cleared[(int)VegetationKind.Rock]}, bush {set.Cleared[(int)VegetationKind.Bush]}");
            report.Append("\n  trees by area (pine / palm): ").Append(TreesByArea(set.Trees));
        }

        /// <summary>The detail cell (x, z) that holds a point, for the detail maps.</summary>
        public static Vector2Int DetailCell(float x, float z, int resolution)
        {
            Vector3 origin = IslandLayout.TerrainOrigin;
            Vector3 size = IslandLayout.TerrainSize;
            int ix = Mathf.Clamp(Mathf.FloorToInt((x - origin.x) / size.x * resolution), 0, resolution - 1);
            int iz = Mathf.Clamp(Mathf.FloorToInt((z - origin.z) / size.z * resolution), 0, resolution - 1);
            return new Vector2Int(ix, iz);
        }

        // Instance-count mode: each cell holds how many bushes or tufts grow in it, and Unity scatters them inside the
        // 0.78 m cell. The scatter's exact point is lost, but nothing moves by more than one cell.
        private static void PlantDetails(TerrainData data, VegetationSet set, GameObject[] prefabs, StringBuilder report)
        {
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.SetDetailResolution(DetailResolution, DetailResolutionPerPatch);
            data.detailPrototypes = new[]
            {
                // Sizes are the prototype's scale ranges (bush 0.6 .. 1.4, grass 0.6 .. 1.25); the noise seeds are the
                // scatter's seeds, so Unity's own per-instance variation is fixed too.
                Detail(prefabs[0], 0.6f, 1.4f, (int)VegetationLayout.BushSeed),
                Detail(prefabs[1], 0.6f, 1.25f, (int)VegetationLayout.GrassSeed),
            };

            foreach (DetailPrototype prototype in data.detailPrototypes)
            {
                if (!prototype.Validate(out string error))
                {
                    Debug.LogError($"[World] Paint Terrain And Forest: detail {prototype.prototype.name}: {error}");
                }
            }

            data.SetDetailLayer(0, 0, 0, CountPerCell(set.Bushes));
            data.SetDetailLayer(0, 0, 1, CountPerCell(set.Grass));
            report.Append($"\n  details: Bush {set.Bushes.Count}, Grass {set.Grass.Count} "
                + $"({DetailResolution} x {DetailResolution} cells, {DetailResolutionPerPatch}-cell patches, instance count mode)");
        }

        private static DetailPrototype Detail(GameObject prefab, float minScale, float maxScale, int seed)
        {
            return new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                useInstancing = true, // draws with the prefab's own URP material
                renderMode = DetailRenderMode.VertexLit,
                minWidth = minScale,
                maxWidth = maxScale,
                minHeight = minScale,
                maxHeight = maxScale,
                noiseSeed = seed,
                noiseSpread = 0.1f,
                useDensityScaling = true, // a quality level's detail density thins them out
                holeEdgePadding = 0f,
            };
        }

        private static int[,] CountPerCell(List<VegetationInstance> instances)
        {
            var counts = new int[DetailResolution, DetailResolution];
            foreach (VegetationInstance instance in instances)
            {
                Vector2Int cell = DetailCell(instance.X, instance.Z, DetailResolution);
                counts[cell.y, cell.x]++;
            }

            return counts;
        }

        private static void ConfigureTerrain(Terrain terrain)
        {
            terrain.drawTreesAndFoliage = true;
            terrain.treeDistance = TreeDistance;
            terrain.detailObjectDistance = DetailDistance;
            terrain.detailObjectDensity = 1f;
            terrain.preserveTreePrototypeLayers = false;
            EditorUtility.SetDirty(terrain);
        }

        // The ground under a point, as the terrain draws it (the heightmap, not the analytic height).
        private static float GroundY(TerrainData data, float x, float z)
        {
            Vector3 origin = IslandLayout.TerrainOrigin;
            return origin.y + data.GetInterpolatedHeight((x - origin.x) / data.size.x, (z - origin.z) / data.size.z);
        }

        // The reference's areas (prototype z ranges, negated): beach z <= -60, forest -60 .. 30, compound band and
        // cliff 30 .. 70, hill over the bunker > 70.
        private static string TreesByArea(List<VegetationInstance> trees)
        {
            string[] names = { "beach", "forest", "compound band", "hill" };
            float[] upper = { -60f, 30f, 70f, float.MaxValue };
            var pines = new int[4];
            var palms = new int[4];
            foreach (VegetationInstance tree in trees)
            {
                int area = 0;
                while (tree.Z > upper[area])
                {
                    area++;
                }

                if (tree.Kind == VegetationKind.Palm)
                {
                    palms[area]++;
                }
                else
                {
                    pines[area]++;
                }
            }

            var parts = new List<string>();
            for (int i = 0; i < names.Length; i++)
            {
                parts.Add($"{names[i]} {pines[i]} / {palms[i]}");
            }

            return string.Join(", ", parts);
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder(LayerFolder))
            {
                AssetDatabase.CreateFolder(TerrainBuilder.TerrainFolder, "Layers");
            }
        }
    }
}
