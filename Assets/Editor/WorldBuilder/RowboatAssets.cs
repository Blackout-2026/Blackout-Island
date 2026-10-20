using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Builds the approved Core Developer visual assets. Does not change any game scene.
    /// Blender source stays outside Assets; the FBX contains one mesh for each distance level.</summary>
    public static class RowboatAssets
    {
        public const string ModelPath = "Assets/Models/Custom/SM_Rowboat.fbx";
        public const string BasePath = "Assets/Textures/Models/T_Rowboat_BaseColor.png";
        public const string MetalPath = "Assets/Textures/Models/T_Rowboat_MetalSmooth.png";
        public const string MaterialPath = "Assets/Materials/Models/M_Rowboat_Weathered.mat";
        public const string PrefabPath = "Assets/Prefabs/Models/Rowboat_Visual.prefab";
        private const string GeneratedLabel = "BlackoutGeneratedRowboat";

        [MenuItem("Blackout/World/Build Rowboat Visual Assets")]
        public static void Build()
        {
            ConfigureTexture(BasePath, true, 2048);
            ConfigureTexture(MetalPath, false, 1024);
            GameObject model = Required<GameObject>(ModelPath);
            Material material = BuildMaterial();
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            if (exists) RequireGenerated(Required<GameObject>(PrefabPath));
            Scene preview = default;
            GameObject root;
            if (exists) root = PrefabUtility.LoadPrefabContents(PrefabPath);
            else
            {
                preview = EditorSceneManager.NewPreviewScene();
                root = new GameObject("Rowboat_Visual");
                SceneManager.MoveGameObjectToScene(root, preview);
            }
            try
            {
                Transform child = root.transform.Find("Model");
                if (child == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                    child = instance.transform;
                    child.SetParent(root.transform, false);
                    child.name = "Model";
                }
                else if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != model)
                    throw new InvalidOperationException("The rowboat Model has a different source; inspect it before rebuilding.");

                child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.localScale = Vector3.one;
                MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 2) throw new InvalidOperationException("Expected exactly two rowboat LOD meshes.");
                foreach (MeshRenderer renderer in renderers)
                {
                    renderer.sharedMaterials = new[] { material };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                MeshRenderer near = renderers.Single(renderer => renderer.name == "Rowboat_LOD0");
                MeshRenderer far = renderers.Single(renderer => renderer.name == "Rowboat_LOD1");
                LODGroup group = root.GetComponent<LODGroup>();
                if (group == null) group = root.AddComponent<LODGroup>();
                // Relative screen height: retain the net/oars nearby; use the simpler hull farther away.
                group.SetLODs(new[] { new LOD(0.16f, new Renderer[] { near }), new LOD(0.025f, new Renderer[] { far }) });
                group.RecalculateBounds();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SetLabels(saved, new[] { GeneratedLabel });
                int nearTriangles = (int)near.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3;
                int farTriangles = (int)far.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3;
                Debug.Log($"[World] Rowboat visual: LOD0 {nearTriangles} triangles, LOD1 {farTriangles} triangles; "
                    + $"one URP material per LOD; bounds {near.bounds.size:F3} m; no colliders or gameplay components.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[World] Rowboat visual assets ready. Environment scene unchanged.");
        }

        private static Material BuildMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            bool create = material == null;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Rowboat_Weathered" };
            else RequireGenerated(material);
            material.SetTexture("_BaseMap", Required<Texture2D>(BasePath));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_MetallicGlossMap", Required<Texture2D>(MetalPath));
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f); // Smoothness from metallic texture alpha, not albedo.
            BaseShaderGUI.SetMaterialKeywords(material);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (create) AssetDatabase.CreateAsset(material, MaterialPath);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SetLabels(material, new[] { GeneratedLabel });
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void ConfigureTexture(string path, bool colour, int maximumSize)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing baked rowboat texture: " + path);
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.alphaSource = colour ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = maximumSize;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static T Required<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
            ?? throw new InvalidOperationException("Missing rowboat asset: " + path);

        private static void RequireGenerated(UnityEngine.Object asset)
        {
            if (!AssetDatabase.GetLabels(asset).Contains(GeneratedLabel))
                throw new InvalidOperationException("Refusing to overwrite an unlabelled rowboat asset: " + AssetDatabase.GetAssetPath(asset));
        }
    }
}
