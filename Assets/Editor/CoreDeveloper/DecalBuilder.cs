using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Makes every sign and floor marking in <see cref="DecalCatalog"/>: a texture T_Sign_&lt;Name&gt;_BaseColor.png in
    /// Assets/Textures/Models, a material M_Sign_&lt;Name&gt; in Assets/Materials/Models, and a quad prefab Prop_Sign_&lt;Name&gt; in
    /// Assets/Prefabs/Props (docs/art-pipeline.md section 14). The quad's picture faces +Z (a wall sign) or up (a floor
    /// marking), its origin is the middle of the plate, and there is no collider. Re-runnable: an unchanged texture is not
    /// rewritten, a material keeps hand edits, and a prefab only gets what is missing.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.DecalBuilder.BuildAll -logFile -
    /// </summary>
    public static class DecalBuilder
    {
        public const string TextureFolder = "Assets/Textures/Models";
        public const string MaterialFolder = "Assets/Materials/Models";
        public const string QuadName = "Quad";

        // Lifts the plate off the wall or floor so it does not flicker against the surface it is stuck to.
        public const float Lift = 0.002f;

        [MenuItem("Blackout/Art/Build Sign Decals")]
        public static void BuildAll()
        {
            BuildAll(TextureFolder, MaterialFolder, PropCatalog.PropsFolder);
            Debug.Log($"[Decals] {DecalCatalog.All.Count} signs are up to date");
        }

        public static void BuildAll(string textureFolder, string materialFolder, string prefabFolder)
        {
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                WriteTexture(spec, textureFolder);
                Material material = BuildMaterial(spec, materialFolder, textureFolder);
                BuildPrefab(spec, material, prefabFolder);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Renders the decal and writes its PNG, unless the file already holds exactly those pixels. Returns the asset path.</summary>
        public static string WriteTexture(DecalSpec spec, string textureFolder)
        {
            string assetPath = $"{textureFolder}/{spec.TexturePrefix}BaseColor.png";
            Color32[] pixels = DecalRenderer.Render(spec, out int width, out int height);
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels32(pixels);
            byte[] png = texture.EncodeToPNG();
            Object.DestroyImmediate(texture);

            string fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath) || !FileMatches(fullPath, png))
            {
                ModelMaterials.EnsureFolder(textureFolder);
                File.WriteAllBytes(fullPath, png);
                AssetDatabase.ImportAsset(assetPath);
            }

            return assetPath;
        }

        public static Material BuildMaterial(DecalSpec spec, string materialFolder, string textureFolder)
        {
            var modelSpec = new ModelMaterialSpec(spec.MaterialName, spec.Background, 0f, 0.1f, textured: true);
            Material material = ModelMaterials.GetOrCreate(modelSpec, materialFolder, textureFolder, spec.TexturePrefix);

            // The stripe tile repeats every StripeMetres. Only set while the tiling is still the default, so a hand edit stays.
            if (spec.StripeMetres > 0f && material.GetTextureScale("_BaseMap") == Vector2.one)
            {
                material.SetTextureScale("_BaseMap", new Vector2(spec.Width / spec.StripeMetres, spec.Height / spec.StripeMetres));
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        public static string BuildPrefab(DecalSpec spec, Material material, string prefabFolder)
        {
            string path = $"{prefabFolder}/{spec.PrefabName}.prefab";
            ModelMaterials.EnsureFolder(prefabFolder);
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(path) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject(spec.PrefabName);
            try
            {
                Populate(root, spec, material);
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

            return path;
        }

        /// <summary>Adds the quad to a root, or corrects an existing one. A material already on the quad is kept.</summary>
        public static void Populate(GameObject root, DecalSpec spec, Material material)
        {
            Transform quad = root.transform.Find(QuadName);
            if (quad == null)
            {
                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                Object.DestroyImmediate(go.GetComponent<Collider>());
                go.name = QuadName;
                go.transform.SetParent(root.transform, false);
                quad = go.transform;
            }

            // Unity's Quad faces -Z. A wall sign turns 180 degrees about Y to face +Z; a floor marking tips 90 degrees
            // about X to face up, with the picture's top edge toward +Z.
            quad.localRotation = spec.Floor ? Quaternion.Euler(90f, 0f, 0f) : Quaternion.Euler(0f, 180f, 0f);
            quad.localPosition = spec.Floor ? new Vector3(0f, Lift, 0f) : new Vector3(0f, 0f, Lift);
            quad.localScale = new Vector3(spec.Width, spec.Height, 1f);

            var renderer = quad.GetComponent<MeshRenderer>();
            if (renderer.sharedMaterial == null || renderer.sharedMaterial.name.StartsWith("Default-", System.StringComparison.Ordinal))
            {
                renderer.sharedMaterial = material;
            }
        }

        private static bool FileMatches(string fullPath, byte[] bytes)
        {
            byte[] existing = File.ReadAllBytes(fullPath);
            if (existing.Length != bytes.Length)
            {
                return false;
            }

            for (int i = 0; i < bytes.Length; i++)
            {
                if (existing[i] != bytes[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
