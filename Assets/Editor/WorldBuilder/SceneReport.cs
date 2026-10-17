using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Read-only inventory of Environment, including inactive objects and Terrain vegetation.
    /// Counts describe authored data, not GPU submissions, frame time or static batches in a player build.</summary>
    public static class SceneReport
    {
        public const string ReportPath = "docs/viva/world-builder-optimisation.md";

        public sealed class Snapshot
        {
            public int Objects, ObjectsWithStaticFlags, Renderers, EnabledRenderers, BatchFlaggedRenderers;
            public long MeshTriangles, MeshVertices, TreeTriangles, TreeVertices, DetailTriangles, DetailVertices;
            public long TerrainTriangles, TerrainVertices;
            public int Trees, Details;
            public SceneBatchingAudit.Result Batching;
            public readonly HashSet<Material> Materials = new HashSet<Material>();
            public readonly HashSet<Texture> Textures = new HashSet<Texture>();
            public readonly SortedDictionary<string, int> RendererTypes = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly SortedDictionary<string, int> Lights = new SortedDictionary<string, int>(StringComparer.Ordinal);
            public readonly List<Terrain> Terrains = new List<Terrain>();
        }

        [MenuItem("Blackout/World/Scene Report")]
        public static void Print()
        {
            WithEnvironment(scene => Debug.Log("[World] Scene Report\n" + Format(Capture(scene))));
        }

        // Batch entry point. Each explicit invocation appends a dated observation; it never saves scene/assets.
        public static void AppendBaseline()
        {
            WithEnvironment(scene =>
            {
                string report = Format(Capture(scene));
                AppendObservation("baseline", report);
            });
        }

        internal static void AppendObservation(string title, string report)
        {
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", ReportPath));
            string heading = File.Exists(path) ? "" : "# World Builder optimisation (W11)\n\n"
                + "Run **Blackout > World > Scene Report** to inspect the loaded Environment scene. "
                + "Reports never save the scene or assets.\n\n";
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.AppendAllText(path, heading + "## " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                + " — " + title + "\n\n" + report + "\n", new UTF8Encoding(false));
            Debug.Log("[World] Scene Report appended to " + ReportPath + " (" + title + ")\n" + report);
        }

        internal static void WithEnvironment(Action<Scene> action)
        {
            Scene scene = SceneManager.GetSceneByPath(WorldSceneSkeleton.ScenePath);
            bool opened = !scene.IsValid() || !scene.isLoaded;
            if (opened)
                scene = EditorSceneManager.OpenScene(WorldSceneSkeleton.ScenePath, OpenSceneMode.Additive);
            try { action(scene); }
            finally { if (opened) EditorSceneManager.CloseScene(scene, true); }
        }

        public static Snapshot Capture(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                throw new ArgumentException("Scene Report needs a loaded scene.", nameof(scene));
            var result = new Snapshot();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Transform item in root.GetComponentsInChildren<Transform>(true))
                {
                    result.Objects++;
                    if (GameObjectUtility.GetStaticEditorFlags(item.gameObject) != 0) result.ObjectsWithStaticFlags++;
                }
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    result.Renderers++;
                    if (renderer.enabled && renderer.gameObject.activeInHierarchy) result.EnabledRenderers++;
                    if ((GameObjectUtility.GetStaticEditorFlags(renderer.gameObject) & StaticEditorFlags.BatchingStatic) != 0)
                        result.BatchFlaggedRenderers++;
                    Increment(result.RendererTypes, renderer.GetType().Name);
                    AddMaterials(result, renderer);
                    AddMesh(MeshOf(renderer), ref result.MeshTriangles, ref result.MeshVertices);
                }
                foreach (Light light in root.GetComponentsInChildren<Light>(true))
                    Increment(result.Lights, light.lightmapBakeType + " / "
                        + (light.enabled && light.gameObject.activeInHierarchy ? "enabled" : "disabled"));
                foreach (Terrain terrain in root.GetComponentsInChildren<Terrain>(true))
                    AddTerrain(result, terrain);
            }
            // Scene dependencies also include lightmaps, probe cubemaps, Terrain layers and skybox textures.
            // Load all sub-assets so embedded splat/height textures are included and de-duplicated by object identity.
            if (!string.IsNullOrEmpty(scene.path))
                foreach (string path in AssetDatabase.GetDependencies(scene.path, true))
                {
                    // Script dependencies can pull Editor icons into the scene's dependency graph.
                    // These are not rendered by the game and must not inflate its texture inventory.
                    if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0
                        || path.IndexOf("/Gizmos/", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset is Texture texture) result.Textures.Add(texture);
                }
            result.Batching = SceneBatchingAudit.Capture(scene, result.Materials);
            return result;
        }

        public static long TriangleCount(Mesh mesh)
        {
            long count = 0;
            if (mesh != null)
                for (int i = 0; i < mesh.subMeshCount; i++)
                    if (mesh.GetTopology(i) == MeshTopology.Triangles) count += mesh.GetIndexCount(i) / 3;
            return count;
        }

        private static void AddMesh(Mesh mesh, ref long triangles, ref long vertices)
        {
            if (mesh == null) return;
            triangles += TriangleCount(mesh);
            vertices += mesh.vertexCount;
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
            if (!(renderer is MeshRenderer)) return null; // particle mesh is not a particle instance count
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            return filter != null ? filter.sharedMesh : null;
        }

        private static void AddMaterials(Snapshot result, Renderer renderer)
        {
            foreach (Material material in renderer.sharedMaterials)
            {
                if (material == null || !result.Materials.Add(material)) continue;
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null) result.Textures.Add(texture);
                }
            }
        }

        private static void AddTerrain(Snapshot result, Terrain terrain)
        {
            TerrainData data = terrain.terrainData;
            if (data == null) return;
            result.Terrains.Add(terrain);
            if (terrain.materialTemplate != null) result.Materials.Add(terrain.materialTemplate);
            long cells = data.heightmapResolution - 1;
            result.TerrainVertices += (cells + 1) * (cells + 1);
            result.TerrainTriangles += cells * cells * 2; // full grid upper bound, including holes
            TreePrototype[] prototypes = data.treePrototypes;
            foreach (TreeInstance tree in data.treeInstances)
            {
                result.Trees++;
                AddPrefab(result, prototypes[tree.prototypeIndex].prefab, 1, true);
            }
            DetailPrototype[] details = data.detailPrototypes;
            int patches = data.detailPatchCount;
            for (int layer = 0; layer < details.Length; layer++)
            {
                int count = 0;
                for (int z = 0; z < patches; z++)
                    for (int x = 0; x < patches; x++)
                        count += data.ComputeDetailInstanceTransforms(x, z, layer, terrain.detailObjectDensity, out _).Length;
                result.Details += count;
                AddPrefab(result, details[layer].prototype, count, false);
            }
        }

        private static void AddPrefab(Snapshot result, GameObject prefab, int instances, bool tree)
        {
            if (prefab == null) return;
            LODGroup group = prefab.GetComponent<LODGroup>();
            Renderer[] renderers = group != null && group.lodCount > 0
                ? group.GetLODs()[0].renderers : prefab.GetComponentsInChildren<Renderer>(true);
            long triangles = 0, vertices = 0;
            // Materials/textures from every LOD; geometry from LOD0 only, never sum mutually exclusive LODs.
            foreach (Renderer renderer in prefab.GetComponentsInChildren<Renderer>(true)) AddMaterials(result, renderer);
            foreach (Renderer renderer in renderers)
                if (renderer != null) AddMesh(MeshOf(renderer), ref triangles, ref vertices);
            if (tree) { result.TreeTriangles += triangles * instances; result.TreeVertices += vertices * instances; }
            else { result.DetailTriangles += triangles * instances; result.DetailVertices += vertices * instances; }
        }

        private static void Increment(SortedDictionary<string, int> counts, string key)
        {
            counts.TryGetValue(key, out int count);
            counts[key] = count + 1;
        }

        public static string Format(Snapshot data)
        {
            var text = new StringBuilder();
            text.AppendLine("Unity " + Application.unityVersion + "; target " + EditorUserBuildSettings.activeBuildTarget
                + "; quality " + QualitySettings.names[QualitySettings.GetQualityLevel()] + "; LOD bias " + Number(QualitySettings.lodBias)
                + "; graphics " + SystemInfo.graphicsDeviceType);
            text.AppendLine("\nAll objects include inactive children; mesh counts repeat shared meshes per instance. "
                + "Static means any authored static flag, not a promise that an object cannot move. "
                + "Terrain vegetation is counted separately from scene Renderer components. "
                + "Geometry totals are inventory bounds, not triangles visible in one frame.\n");
            text.AppendLine("| Metric | Value |\n| --- | ---: |");
            Row(text, "GameObjects", data.Objects);
            Row(text, "Objects with static flags / without flags", data.ObjectsWithStaticFlags + " / " + (data.Objects - data.ObjectsWithStaticFlags));
            Row(text, "Renderer components / active and enabled", data.Renderers + " / " + data.EnabledRenderers);
            foreach (var pair in data.RendererTypes) Row(text, "Renderer: " + pair.Key, pair.Value);
            Row(text, "Renderers with BatchingStatic flag", data.BatchFlaggedRenderers);
            Row(text, "Scene mesh triangles / vertices (all LOD children)", data.MeshTriangles + " / " + data.MeshVertices);
            Row(text, "Terrain full-grid triangles / vertices (upper bound before holes and pixel-error LOD)", data.TerrainTriangles + " / " + data.TerrainVertices);
            Row(text, "Terrain tree instances", data.Trees);
            Row(text, "Tree LOD0 triangles / vertices (all instances)", data.TreeTriangles + " / " + data.TreeVertices);
            Row(text, "Terrain detail instances (authored density)", data.Details);
            Row(text, "Detail mesh triangles / vertices (all instances)", data.DetailTriangles + " / " + data.DetailVertices);
            Row(text, "Combined inventory triangles / vertices (full Terrain grid and LOD0 vegetation)",
                (data.MeshTriangles + data.TerrainTriangles + data.TreeTriangles + data.DetailTriangles) + " / "
                + (data.MeshVertices + data.TerrainVertices + data.TreeVertices + data.DetailVertices));
            Row(text, "Unique materials (scene, Terrain and all vegetation LODs)", data.Materials.Count);
            foreach (var pair in data.Lights) Row(text, "Lights: " + pair.Key, pair.Value);
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            Row(text, "Active URP SRP Batcher / shadow distance (m)", pipeline != null
                ? pipeline.useSRPBatcher + " / " + Number(pipeline.shadowDistance) : "no active URP asset");
            Row(text, "Unique referenced textures (including embedded scene dependencies)", data.Textures.Count);
            Row(text, "Texture runtime memory estimate (bytes, Editor process)", data.Textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t)));
            text.AppendLine("\nTop 10 referenced textures by Editor runtime memory estimate; this includes object overhead and CPU copies, "
                + "so it is not compressed build size or GPU-only memory. Imported dimensions reflect the active target.\n");
            text.AppendLine("| Texture / asset | Dimensions | Format | Estimated bytes |\n| --- | ---: | --- | ---: |");
            foreach (Texture texture in data.Textures.OrderByDescending(t => Profiler.GetRuntimeMemorySizeLong(t))
                .ThenBy(t => AssetDatabase.GetAssetPath(t) + "/" + t.name, StringComparer.Ordinal).Take(10))
            {
                string format = texture is Texture2D two ? two.format.ToString() : texture.graphicsFormat.ToString();
                text.AppendLine("| " + Cell(AssetDatabase.GetAssetPath(texture) + " / " + texture.name) + " | "
                    + texture.width + " × " + texture.height + " | " + format + " | " + Profiler.GetRuntimeMemorySizeLong(texture) + " |");
            }
            if (data.Batching != null) text.AppendLine(SceneBatchingAudit.Format(data.Batching));
            text.AppendLine("\nVegetation estimate: fixed 72° FOV, 16:9, eye 1.65 m, active quality LOD bias; "
                + "whole detail patches, without occlusion or shadow passes. Frame Debugger and Profiler validation remains an Editor task.");
            var layout = AssetDatabase.LoadAssetAtPath<Blackout.World.BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
            if (layout != null)
                foreach (Terrain terrain in data.Terrains)
                    text.AppendLine("\n```text\n" + VegetationBudget.Report(terrain, MarkerLayout.Build(layout)) + "\n```\n");
            return text.ToString();
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
        private static string Cell(string value) => value.Replace("|", "\\|").Replace("\n", " ").Replace("\r", " ");
        private static void Row(StringBuilder text, string label, object value) => text.AppendLine("| " + label + " | " + value + " |");
    }
}
