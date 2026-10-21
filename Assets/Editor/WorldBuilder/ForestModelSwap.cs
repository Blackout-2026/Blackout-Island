using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Apply Forest Models (step F1): the forest's rocks, bushes and grass come from the downloaded ponderosa stand
    /// (docs/credits/world-builder.md), like the pines already do (Core Developer's SM_Tree_Pine*.fbx).
    /// 1. Builds Assets/Environment/Vegetation/Rock, RockSmall, Bush and Grass.prefab, the names that <see cref="VegetationLibrary"/> prefers over
    ///    its generated placeholders, from SM_Forest_*.fbx and their three materials.
    /// 2. Points the terrain's prototypes at them. Paint Terrain And Forest would do the same, but it also repaints the terrain and the trail;
    ///    this only swaps the prefab of each prototype, so every tree, rock, bush and tuft keeps its position, scale and yaw, and the terrain
    ///    layers, the trail, the NavMesh (the capsules are the placeholders' numbers) and the lighting are not touched.
    /// Re-runnable: a second run changes nothing. A later Paint Terrain And Forest uses the new prefabs through the same hook.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.ForestModelSwap.ApplyAll
    /// </summary>
    public static class ForestModelSwap
    {
        public const string ModelFolder = "Assets/Models/Custom";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string TerrainDataPath = "Assets/Environment/Terrain/IslandTerrainData.asset";

        /// <summary>How one prefab is put together. Rocks nest the FBX (three LOD meshes); bushes and grass are one mesh on the root.</summary>
        public sealed class Spec
        {
            public VegetationPrototype Prototype;
            public string Fbx;
            public string Material;
            public string Texture;
            public Color Tint;
            public float Smoothness;
            public bool Foliage; // alpha clipped and two-sided
            public float[] LodHeights; // null: a single mesh
            public Vector3? Capsule; // radius, height, centre y: the placeholders' numbers, so the NavMesh does not change
        }

        // The placeholders' LOD heights and capsules (VegetationPlaceholderBuilder). The tints take the texture to the reference's colours:
        // rock #B8B8B4 over the photo, and the bush and grass a little greener and darker than the sprays so they sit in the dark forest.
        public static readonly Spec[] Specs =
        {
            new Spec { Prototype = VegetationPrototype.Rock, Fbx = "SM_Forest_Rock", Material = "M_Forest_Rock", Texture = "T_ForestRock_BaseColor",
                Tint = new Color(0.72f, 0.72f, 0.71f), Smoothness = 0.2f, LodHeights = new[] { 0.15f, 0.05f, 0.015f },
                Capsule = new Vector3(0.8f, 1.6f, -0.2f) },
            new Spec { Prototype = VegetationPrototype.RockSmall, Fbx = "SM_Forest_RockSmall", Material = "M_Forest_Rock", Texture = "T_ForestRock_BaseColor",
                Tint = new Color(0.72f, 0.72f, 0.71f), Smoothness = 0.2f, LodHeights = new[] { 0.15f, 0.05f, 0.015f } },
            new Spec { Prototype = VegetationPrototype.Bush, Fbx = "SM_Forest_Bush", Material = "M_Forest_Bush", Texture = "T_PonderosaNeedles_BaseColor",
                Tint = new Color(0.8f, 0.9f, 0.7f), Smoothness = 0.12f, Foliage = true },
            new Spec { Prototype = VegetationPrototype.Grass, Fbx = "SM_Forest_Grass", Material = "M_Forest_Grass", Texture = "T_ForestGrass_BaseColor",
                Tint = new Color(0.82f, 0.88f, 0.75f), Smoothness = 0f, Foliage = true },
        };

        public static string FbxPath(Spec spec)
        {
            return $"{ModelFolder}/{spec.Fbx}.fbx";
        }

        [MenuItem("Blackout/World/Apply Forest Models")]
        public static void ApplyAll()
        {
            string prefabs = BuildPrefabs();
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                Debug.LogWarning($"[World] Apply Forest Models: {TerrainDataPath} not found, so only the prefabs were built:{prefabs}");
                return;
            }

            string swap = SwapPrototypes(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                terrain.Flush();
            }

            Debug.Log($"[World] Apply Forest Models:{prefabs}{swap}");
        }

        /// <summary>Builds or updates Rock, RockSmall, Bush and Grass.prefab. Returns a report, one line per prefab.</summary>
        public static string BuildPrefabs()
        {
            var report = new StringBuilder();
            foreach (Spec spec in Specs)
            {
                var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath(spec));
                if (fbx == null)
                {
                    report.Append($"\n  {spec.Prototype}: {FbxPath(spec)} not found, skipped.");
                    continue;
                }

                Material material = SaveMaterial(spec);
                string path = VegetationLibrary.PrefabPath(spec.Prototype);
                bool exists = File.Exists(path);
                GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(spec.Prototype.ToString());
                try
                {
                    if (spec.LodHeights != null)
                    {
                        PopulateLodPrefab(root, fbx, material, spec);
                    }
                    else
                    {
                        PopulateDetailPrefab(root, spec, material);
                    }

                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally
                {
                    if (exists)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                    }
                    else
                    {
                        Object.DestroyImmediate(root);
                    }
                }

                report.Append($"\n  {spec.Prototype}: {path} from {FbxPath(spec)}");
            }

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // A rock: the FBX as a child, a LOD group on its three meshes and the placeholder's capsule (RockSmall has none, as in the prototype).
        private static void PopulateLodPrefab(GameObject root, GameObject fbx, Material material, Spec spec)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform model = root.transform.Find("Model");
            if (model == null || PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) != fbx)
            {
                if (model != null)
                {
                    Object.DestroyImmediate(model.gameObject);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                instance.name = "Model";
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                model = instance.transform;
            }

            var levels = new LOD[spec.LodHeights.Length];
            string meshName = spec.Prototype.ToString();
            for (int i = 0; i < levels.Length; i++)
            {
                Transform lod = model.Find($"{meshName}_LOD{i}");
                var renderer = lod != null ? lod.GetComponent<MeshRenderer>() : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[World] {FbxPath(spec)} has no mesh object called {meshName}_LOD{i}.");
                    levels[i] = new LOD(spec.LodHeights[i], new Renderer[0]);
                    continue;
                }

                var slots = new Material[renderer.sharedMaterials.Length];
                for (int s = 0; s < slots.Length; s++)
                {
                    slots[s] = material;
                }

                renderer.sharedMaterials = slots;
                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                levels[i] = new LOD(spec.LodHeights[i], new Renderer[] { renderer });
            }

            LODGroup group = root.GetComponent<LODGroup>();
            if (group == null)
            {
                group = root.AddComponent<LODGroup>();
            }

            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;
            group.SetLODs(levels);
            group.RecalculateBounds();

            var capsule = root.GetComponent<CapsuleCollider>();
            if (spec.Capsule.HasValue)
            {
                if (capsule == null)
                {
                    capsule = root.AddComponent<CapsuleCollider>();
                }

                capsule.radius = spec.Capsule.Value.x;
                capsule.height = spec.Capsule.Value.y;
                capsule.center = new Vector3(0f, spec.Capsule.Value.z, 0f);
                capsule.direction = 1; // Y
            }
            else if (capsule != null)
            {
                Object.DestroyImmediate(capsule);
            }
        }

        // A terrain detail: one MeshFilter and MeshRenderer on the root, no collider, no shadow (the prototype's bushes and grass are soft).
        private static void PopulateDetailPrefab(GameObject root, Spec spec, Material material)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Mesh mesh = AssetDatabase.LoadAllAssetsAtPath(FbxPath(spec)).OfType<Mesh>().FirstOrDefault();
            var filter = root.GetComponent<MeshFilter>();
            if (filter == null)
            {
                filter = root.AddComponent<MeshFilter>();
            }

            filter.sharedMesh = mesh;
            var renderer = root.GetComponent<MeshRenderer>();
            if (renderer == null)
            {
                renderer = root.AddComponent<MeshRenderer>();
            }

            renderer.sharedMaterials = new[] { material };
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = true;
        }

        private static Material SaveMaterial(Spec spec)
        {
            string path = $"{MaterialFolder}/{spec.Material}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = spec.Material };
            }

            if (spec.Foliage)
            {
                PrepareCutoutTexture($"{TextureFolder}/{spec.Texture}.png");
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{spec.Texture}.png"));
            material.SetColor("_BaseColor", spec.Tint);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", spec.Smoothness);
            material.SetFloat("_Surface", 0f); // opaque: the leaf edge is cut out, not blended
            material.SetFloat("_AlphaClip", spec.Foliage ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", spec.Foliage ? 0f : 2f); // foliage is seen from both sides
            material.enableInstancing = true; // terrain details are drawn instanced
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // A cut-out texture keeps its coverage in the small mip levels, so the needles do not thin out at a distance (as TreePrefabBuilder does).
        private static void PrepareCutoutTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null || (importer.alphaIsTransparency && importer.mipMapsPreserveCoverage))
            {
                return;
            }

            importer.alphaIsTransparency = true;
            importer.mipMapsPreserveCoverage = true;
            importer.alphaTestReferenceValue = 0.5f;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Replaces the placeholder prefab of the Rock, RockSmall, Bush and Grass prototypes by the prefabs of <see cref="BuildPrefabs"/>.
        /// Instances, layers and every other setting are kept. Returns a report with the counts before and after.
        /// </summary>
        public static string SwapPrototypes(TerrainData data)
        {
            int trees = data.treeInstances.Length;
            int bushes = DetailTotal(data, 0);
            int grass = DetailTotal(data, 1);

            TreePrototype[] treePrototypes = data.treePrototypes;
            int swapped = 0;
            foreach (TreePrototype prototype in treePrototypes)
            {
                swapped += Replace(prototype.prefab, VegetationPrototype.Rock, p => prototype.prefab = p);
                swapped += Replace(prototype.prefab, VegetationPrototype.RockSmall, p => prototype.prefab = p);
            }

            data.treePrototypes = treePrototypes;

            DetailPrototype[] detailPrototypes = data.detailPrototypes;
            foreach (DetailPrototype prototype in detailPrototypes)
            {
                swapped += Replace(prototype.prototype, VegetationPrototype.Bush, p => prototype.prototype = p);
                swapped += Replace(prototype.prototype, VegetationPrototype.Grass, p => prototype.prototype = p);
            }

            data.detailPrototypes = detailPrototypes;

            var report = new StringBuilder($"\n  prototypes swapped: {swapped}");
            report.Append($"\n  trees {trees} -> {data.treeInstances.Length}, bushes {bushes} -> {DetailTotal(data, 0)}, grass {grass} -> {DetailTotal(data, 1)}");
            return report.ToString();
        }

        // Swaps the prefab when it is the placeholder (or an older copy) of this kind and the new prefab exists. Returns 1 when it did.
        private static int Replace(GameObject current, VegetationPrototype kind, System.Action<GameObject> assign)
        {
            if (current == null || (current.name != kind + "_Placeholder"))
            {
                return 0;
            }

            var replacement = AssetDatabase.LoadAssetAtPath<GameObject>(VegetationLibrary.PrefabPath(kind));
            if (replacement == null)
            {
                return 0;
            }

            assign(replacement);
            return 1;
        }

        private static int DetailTotal(TerrainData data, int layer)
        {
            if (layer >= data.detailPrototypes.Length)
            {
                return 0;
            }

            int[,] counts = data.GetDetailLayer(0, 0, data.detailWidth, data.detailHeight, layer);
            int total = 0;
            foreach (int count in counts)
            {
                total += count;
            }

            return total;
        }
    }
}
