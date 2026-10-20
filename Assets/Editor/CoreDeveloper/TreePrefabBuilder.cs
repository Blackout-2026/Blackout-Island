using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>One realistic tree: its FBX, its two materials and how its prefab is set up.</summary>
    public sealed class TreeEntry
    {
        public readonly string Name;           // Pine or Palm
        public readonly float[] LodHeights;     // screen-relative heights where each LOD ends (the same as the World Builder's placeholders)
        public readonly Vector3 Capsule;        // radius, height, centre y: the prototype's trunk cylinder, same as the placeholder
        public readonly string BarkMaterial;
        public readonly string FoliageMaterial;
        public readonly string BarkTexture;     // T_<name>_BaseColor, with _Normal next to it
        public readonly string FoliageTexture;
        public readonly string Family;          // Pine, Palm, Almond or Banana: the variants are mixed by TreeDiversifier
        public readonly Color BarkTint;         // multiplies the bark texture (the same numbers as build_trees.py)
        public readonly Color FoliageTint;

        public TreeEntry(string name, float[] lodHeights, Vector3 capsule, string barkMaterial, string foliageMaterial,
            string barkTexture, string foliageTexture, Color? barkTint = null, Color? foliageTint = null)
        {
            Family = name.StartsWith("Pine") ? "Pine" : name.StartsWith("Palm") ? "Palm" : name.StartsWith("Almond") ? "Almond" : "Banana";
            BarkTint = barkTint ?? Color.white;
            FoliageTint = foliageTint ?? Color.white;
            Name = name;
            LodHeights = lodHeights;
            Capsule = capsule;
            BarkMaterial = barkMaterial;
            FoliageMaterial = foliageMaterial;
            BarkTexture = barkTexture;
            FoliageTexture = foliageTexture;
        }

        public string FbxPath => $"Assets/Models/Custom/SM_Tree_{Name}.fbx";
        public string PrefabPath => $"{TreePrefabBuilder.PrefabFolder}/{Name}.prefab";
    }

    /// <summary>
    /// Builds Assets/Environment/Vegetation/Pine.prefab and Palm.prefab (docs/art-pipeline.md section 19): the realistic trees that the World
    /// Builder's <c>VegetationLibrary</c> prefers over its generated placeholders ("your own prefab wins"). Each prefab nests the FBX (three
    /// LOD meshes, bark and foliage slots), gives the two slots URP Lit materials with the bark and the needle or frond textures (the foliage
    /// is alpha-clipped and two-sided, so its shadows are cut out too), sets up a LODGroup at the placeholders' screen heights and the same
    /// CapsuleCollider. Then run Blackout &gt; World &gt; Paint Terrain And Forest to plant them, then Blackout &gt; Art &gt; Diversify Forest to mix
    /// the variants (PineB..D, PalmB..D). Re-runnable.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;" -executeMethod Blackout.Editor.TreePrefabBuilder.BuildAll -logFile -
    /// </summary>
    public static class TreePrefabBuilder
    {
        public const string PrefabFolder = "Assets/Environment/Vegetation";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string ModelName = "Model";

        // The World Builder's own numbers (VegetationPlaceholderBuilder): LOD heights, and the capsule r 0.3 x 10 m (pine) and r 0.25 x 8 m (palm).
        // Four pines, four palms, two sea almonds and two banana plants (a forest is not one tree repeated). "Pine" and "Palm" keep the names the World Builder looks for; the
        // other prefabs (PineB..D, PalmB..D, Almond, AlmondB, Banana, BananaB) are mixed in by TreeDiversifier.
        private static readonly float[] PineLods = { 0.25f, 0.10f, 0.02f };
        private static readonly float[] PalmLods = { 0.25f, 0.15f, 0.02f };
        private static readonly float[] BananaLods = { 0.30f, 0.12f, 0.04f };

        public static readonly TreeEntry[] Trees =
        {
            new TreeEntry("Pine", PineLods, new Vector3(0.3f, 10f, 4f), "M_Tree_PineBark", "M_Tree_PineNeedles", "T_PineBark", "T_PineNeedles"),
            new TreeEntry("PineB", PineLods, new Vector3(0.3f, 12f, 4.8f), "M_Tree_PineBark_B", "M_Tree_PineNeedles_B", "T_PineBark", "T_PineNeedles",
                new Color(0.85f, 0.85f, 0.90f), new Color(0.74f, 0.90f, 0.84f)),
            new TreeEntry("PineC", PineLods, new Vector3(0.34f, 8.5f, 3.4f), "M_Tree_PineBark_C", "M_Tree_PineNeedles_C", "T_PineBark", "T_PineNeedles",
                new Color(1.05f, 0.95f, 0.85f), new Color(0.95f, 1.0f, 0.64f)),
            new TreeEntry("PineD", PineLods, new Vector3(0.2f, 5f, 2f), "M_Tree_PineBark_D", "M_Tree_PineNeedles_D", "T_PineBark", "T_PineNeedles",
                Color.white, new Color(0.86f, 1.0f, 0.74f)),
            new TreeEntry("Palm", PalmLods, new Vector3(0.25f, 8f, 3f), "M_Tree_PalmBark", "M_Tree_PalmFrond", "T_PalmBark", "T_PalmFrond"),
            new TreeEntry("PalmB", PalmLods, new Vector3(0.22f, 9f, 3.6f), "M_Tree_PalmBark_B", "M_Tree_PalmFrond_B", "T_PalmBark", "T_PalmFrond",
                new Color(1.1f, 1.05f, 1.0f), new Color(0.88f, 1.0f, 0.80f)),
            new TreeEntry("PalmC", PalmLods, new Vector3(0.3f, 4.2f, 1.7f), "M_Tree_PalmBark_C", "M_Tree_PalmFrond_C", "T_PalmBark", "T_PalmFrond",
                new Color(1.0f, 0.95f, 0.85f), new Color(0.92f, 1.0f, 0.62f)),
            new TreeEntry("PalmD", PalmLods, new Vector3(0.25f, 8f, 3f), "M_Tree_PalmBark_D", "M_Tree_PalmFrond_D", "T_PalmBark", "T_PalmFrond",
                new Color(0.9f, 0.9f, 0.9f), new Color(0.8f, 0.92f, 0.7f)),
            // the start beach's other trees: a sea almond (tiered, and a rounder one) and a banana plant (full-grown and young)
            new TreeEntry("Almond", PineLods, new Vector3(0.25f, 6f, 3f), "M_Tree_AlmondBark", "M_Tree_AlmondLeaves", "T_AlmondBark", "T_BroadLeaves"),
            new TreeEntry("AlmondB", PineLods, new Vector3(0.3f, 5f, 2.5f), "M_Tree_AlmondBark_B", "M_Tree_AlmondLeaves_B", "T_AlmondBark", "T_BroadLeaves",
                new Color(0.9f, 0.9f, 0.95f), new Color(0.88f, 1.0f, 0.78f)),
            new TreeEntry("Banana", BananaLods, new Vector3(0.2f, 2.6f, 1.3f), "M_Tree_BananaStem", "M_Tree_BananaLeaf", "T_BananaStem", "T_BananaLeaf"),
            new TreeEntry("BananaB", BananaLods, new Vector3(0.15f, 1.7f, 0.85f), "M_Tree_BananaStem_B", "M_Tree_BananaLeaf_B", "T_BananaStem", "T_BananaLeaf",
                new Color(0.95f, 1.0f, 0.9f), new Color(0.92f, 1.0f, 0.78f)),
        };

        [MenuItem("Blackout/Art/Build Realistic Trees")]
        public static void BuildAll()
        {
            ModelMaterials.EnsureFolder(PrefabFolder);
            foreach (TreeEntry entry in Trees)
            {
                Build(entry);
            }

            AssetDatabase.SaveAssets();
            Debug.Log("[Trees] " + Trees.Length + " tree prefabs are in " + PrefabFolder + ". Run Blackout > World > Paint Terrain And Forest, then Blackout > Art > Diversify Forest.");
        }

        public static void Build(TreeEntry entry)
        {
            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(entry.FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[Trees] {entry.FbxPath} not found, so {entry.PrefabPath} was not built.");
                return;
            }

            Dictionary<string, Material> materials = BuildMaterials(entry);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(entry.PrefabPath) : new GameObject(entry.Name);
            try
            {
                Populate(root, fbx, materials, entry);
                PrefabUtility.SaveAsPrefabAsset(root, entry.PrefabPath);
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

            Debug.Log($"[Trees] {(exists ? "Updated" : "Created")} {entry.PrefabPath}");
        }

        /// <summary>The model, its materials, the LOD group and the collider on a root. Safe to call repeatedly.</summary>
        public static void Populate(GameObject root, GameObject fbx, Dictionary<string, Material> materials, TreeEntry entry)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            Transform model = root.transform.Find(ModelName);
            if (model == null || PrefabUtility.GetCorrespondingObjectFromSource(model.gameObject) != fbx)
            {
                if (model != null)
                {
                    Object.DestroyImmediate(model.gameObject);
                }

                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root.transform);
                instance.name = ModelName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                model = instance.transform;
            }

            AssignMaterials(model.gameObject, materials);

            var levels = new LOD[entry.LodHeights.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                Transform lod = RobotRig.FindDeep(model, $"{entry.Name}_LOD{i}");
                Renderer renderer = lod != null ? lod.GetComponent<Renderer>() : null;
                if (renderer == null)
                {
                    Debug.LogWarning($"[Trees] {entry.FbxPath} has no mesh object called {entry.Name}_LOD{i}.");
                    levels[i] = new LOD(entry.LodHeights[i], new Renderer[0]);
                    continue;
                }

                renderer.shadowCastingMode = ShadowCastingMode.On;
                renderer.receiveShadows = true;
                levels[i] = new LOD(entry.LodHeights[i], new[] { renderer });
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
            if (capsule == null)
            {
                capsule = root.AddComponent<CapsuleCollider>();
            }

            capsule.radius = entry.Capsule.x;
            capsule.height = entry.Capsule.y;
            capsule.center = new Vector3(0f, entry.Capsule.z, 0f);
            capsule.direction = 1; // Y
        }

        /// <summary>Replaces each imported slot with the asset of the same name (read from the FBX's own slot).</summary>
        public static void AssignMaterials(GameObject instance, Dictionary<string, Material> materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                Renderer source = PrefabUtility.GetCorrespondingObjectFromSource(renderer);
                Material[] imported = source != null ? source.sharedMaterials : null;
                for (int i = 0; i < slots.Length; i++)
                {
                    Material named = imported != null && i < imported.Length && imported[i] != null ? imported[i] : slots[i];
                    string slotName = named != null ? named.name : string.Empty;
                    if (materials.TryGetValue(slotName, out Material replacement))
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[Trees] {renderer.name} slot {i} uses '{slotName}', which matches no M_Tree_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }

        /// <summary>Creates or updates the bark and the foliage material of a tree. Foliage: alpha clipped, two-sided, instanced.</summary>
        public static Dictionary<string, Material> BuildMaterials(TreeEntry entry)
        {
            ModelMaterials.EnsureFolder(MaterialFolder);
            var set = new Dictionary<string, Material>();
            set[entry.BarkMaterial] = SaveMaterial(entry.BarkMaterial, entry.BarkTexture + "_BaseColor", entry.BarkTexture + "_Normal", false, 0.08f,
                entry.BarkTint);
            set[entry.FoliageMaterial] = SaveMaterial(entry.FoliageMaterial, entry.FoliageTexture + "_BaseColor", null, true, 0.12f, entry.FoliageTint);
            return set;
        }

        private static Material SaveMaterial(string name, string baseColor, string normal, bool foliage, float smoothness, Color tint)
        {
            string path = $"{MaterialFolder}/{name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = name };
            }

            if (foliage)
            {
                PrepareCutoutTexture($"{TextureFolder}/{baseColor}.png");
            }

            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{baseColor}.png"));
            material.SetColor("_BaseColor", tint);
            material.SetTexture("_BumpMap", normal != null ? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureFolder}/{normal}.png") : null);
            material.SetFloat("_BumpScale", 1f);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Surface", 0f); // opaque: the leaf edge is cut out, not blended
            material.SetFloat("_AlphaClip", foliage ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", foliage ? 0f : 2f); // foliage is seen from both sides
            material.enableInstancing = true;
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

        // A cut-out texture keeps its leaf coverage in the smaller mip levels, so a tree does not thin out to nothing at a distance.
        private static void PrepareCutoutTexture(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
            {
                return;
            }

            if (!importer.alphaIsTransparency || !importer.mipMapsPreserveCoverage)
            {
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.5f;
                importer.SaveAndReimport();
            }
        }
    }
}
