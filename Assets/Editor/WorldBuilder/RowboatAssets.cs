using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Installs the supplied Gislinge model in the existing visual prefab, keeping its GUID.
    /// Only the labelled generated Model child is replaced; scenes and gameplay stay separate.</summary>
    public static class RowboatAssets
    {
        public const string ModelPath = "Assets/Models/Custom/SM_GislingeBoat.fbx";
        private const string PreviousModelPath = "Assets/Models/Custom/SM_Rowboat.fbx";
        public const string PrefabPath = "Assets/Prefabs/Models/Rowboat_Visual.prefab";
        private const string GeneratedLabel = "BlackoutGeneratedRowboat";

        [MenuItem("Blackout/World/Build Rowboat Visual Assets")]
        public static void Build()
        {
            GameObject model = Required<GameObject>(ModelPath);
            Material hull = BuildMaterial("Hull");
            Material sail = BuildMaterial("Sail");
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
                if (child != null && PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != model)
                {
                    if (PrefabUtility.GetCorrespondingObjectFromSource(child.gameObject) != Required<GameObject>(PreviousModelPath)
                        || PrefabUtility.GetAddedGameObjects(child.gameObject).Count != 0
                        || PrefabUtility.GetAddedComponents(child.gameObject).Count != 0)
                        throw new InvalidOperationException("Unexpected Model source or custom additions; inspect the boat prefab before rebuilding.");
                    // Unity APIs preserve the outer prefab's GUID, root and scene/cinematic references.
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                    child = null;
                }
                if (child == null)
                {
                    GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(model, root.scene);
                    child = instance.transform;
                    child.SetParent(root.transform, false);
                    child.name = "Model";
                }
                child.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
                child.localScale = Vector3.one;
                MeshRenderer[] renderers = child.GetComponentsInChildren<MeshRenderer>(true);
                if (renderers.Length != 21) throw new InvalidOperationException("Expected the supplied boat's 21 meshes.");
                foreach (MeshRenderer renderer in renderers)
                {
                    renderer.sharedMaterials = new[] { renderer.name == "Sail Full" ? sail : hull };
                    PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                }
                LODGroup group = root.GetComponent<LODGroup>();
                if (group == null) group = root.AddComponent<LODGroup>();
                // The download has one detail level. Keep the existing final cull threshold.
                group.SetLODs(new[] { new LOD(0.025f, renderers) });
                group.RecalculateBounds();
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
                AssetDatabase.SetLabels(saved, new[] { GeneratedLabel });
                int triangles = renderers.Sum(r => (int)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(0) / 3);
                foreach (string part in new[] { "Planking", "Mast", "Rudder", "Sail Full" })
                {
                    MeshRenderer renderer = renderers.Single(r => r.name == part);
                    Debug.Log($"[World] Gislinge {part}: centre {renderer.bounds.center:F3}, size {renderer.bounds.size:F3}");
                }
                Debug.Log($"[World] Gislinge boat: {triangles} triangles, {renderers.Length} meshes, two URP materials; "
                    + "original geometry/UVs, no colliders or gameplay components in the visual prefab.");
            }
            finally
            {
                if (exists) PrefabUtility.UnloadPrefabContents(root);
                else EditorSceneManager.ClosePreviewScene(preview);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[World] Rowboat visual assets ready. Environment scene unchanged.");
        }

        private static Material BuildMaterial(string part)
        {
            string texture = "Assets/Textures/Models/T_Gislinge_" + part;
            string path = "Assets/Materials/Models/M_Gislinge_" + part + ".mat";
            ConfigureTexture(texture + "_BaseColor.png", true);
            ConfigureTexture(texture + "_Normal.png", false, true);
            ConfigureTexture(texture + "_MetalSmooth.png", false);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool create = material == null;
            if (create) material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Gislinge_" + part };
            else RequireGenerated(material);
            material.SetTexture("_BaseMap", Required<Texture2D>(texture + "_BaseColor.png"));
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BumpMap", Required<Texture2D>(texture + "_Normal.png"));
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", Required<Texture2D>(texture + "_MetalSmooth.png"));
            material.SetTexture("_OcclusionMap", Required<Texture2D>(texture + "_MetalSmooth.png"));
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetFloat("_Metallic", 1f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_SmoothnessTextureChannel", 0f); // Smoothness from metallic texture alpha, not albedo.
            material.SetFloat("_Cull", part == "Sail" ? 0f : 2f); // The source sail is a single surface.
            BaseShaderGUI.SetMaterialKeywords(material);
            material.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (create) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            AssetDatabase.SetLabels(material, new[] { GeneratedLabel });
            AssetDatabase.SaveAssetIfDirty(material);
            return material;
        }

        private static void ConfigureTexture(string path, bool colour, bool normal = false)
        {
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) throw new InvalidOperationException("Missing baked rowboat texture: " + path);
            importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = colour;
            importer.alphaSource = colour ? TextureImporterAlphaSource.None : TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = false;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Repeat;
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
