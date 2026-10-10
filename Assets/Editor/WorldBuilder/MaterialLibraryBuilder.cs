using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Blackout.Editor
{
    /// <summary>The textures found for one surface; any of them may be null.</summary>
    public struct SurfaceMaps
    {
        public Texture2D BaseColor;
        public Texture2D Normal;
        public Texture2D MetallicSmoothness;
        public Texture2D AO;

        public bool Textured => BaseColor != null;
    }

    /// <summary>
    /// Blackout/World/Build Material Library: makes one URP Lit material per surface in <see cref="SurfaceLibrary"/>,
    /// Assets/Materials/Environment/Env_&lt;Surface&gt;.mat, from the textures in Assets/Textures/Environment/&lt;Surface&gt;/
    /// (named &lt;Surface&gt;_BaseColor, _Normal, _Roughness, _Metallic, _AO, optionally ending _Hero).
    /// First it re-imports any texture whose settings break <see cref="EnvironmentTextureImport"/>'s rules. Roughness
    /// and Metallic (PNG or JPG) are packed into &lt;Surface&gt;_MetallicSmoothness.png, the layout URP Lit reads.
    /// A surface without a BaseColor texture gets the flat palette colour instead. Tiling is the surface's repeats per
    /// metre. The materials are generated: a re-run sets every property again from the table, so edit the table, not
    /// the material. Re-running with the same textures leaves every file unchanged.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.MaterialLibraryBuilder.BuildMaterialLibrary
    /// </summary>
    public static class MaterialLibraryBuilder
    {
        public const string MaterialFolder = "Assets/Materials/Environment";
        public const string MaterialPrefix = "Env_";
        private const string LitShader = "Universal Render Pipeline/Lit";

        public static string MaterialPath(string surface)
        {
            return $"{MaterialFolder}/{MaterialPrefix}{surface}.mat";
        }

        public static string TextureFolder(string surface)
        {
            return $"{EnvironmentTextureImport.Folder}/{surface}";
        }

        [MenuItem("Blackout/World/Build Material Library")]
        public static void BuildMaterialLibrary()
        {
            Stopwatch timer = Stopwatch.StartNew();
            int reimported = EnvironmentTextureImport.ReimportAll();
            var report = new StringBuilder();
            int textured = 0;
            foreach (SurfaceSpec spec in SurfaceLibrary.All)
            {
                SurfaceMaps maps = FindMaps(spec, TextureFolder(spec.Name), report);
                BuildMaterial(spec, maps);
                if (maps.Textured)
                {
                    textured++;
                }

                report.Append($"\n  {MaterialPrefix}{spec.Name}: ").Append(Describe(spec, maps));
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[World] Material library built in {timer.ElapsedMilliseconds} ms: {SurfaceLibrary.All.Count} materials "
                + $"({textured} textured, {SurfaceLibrary.All.Count - textured} flat colour); {reimported} texture(s) re-imported.{report}");
        }

        /// <summary>Sets every property this library manages, so the result depends only on the spec and the maps.</summary>
        public static void Configure(Material material, SurfaceSpec spec, SurfaceMaps maps)
        {
            material.SetColor("_BaseColor", maps.Textured ? spec.TextureTint : spec.FlatColour);
            material.SetTexture("_BaseMap", maps.BaseColor);
            material.SetTexture("_BumpMap", maps.Normal);
            material.SetFloat("_BumpScale", 1f);
            material.SetTexture("_MetallicGlossMap", maps.MetallicSmoothness);
            material.SetTexture("_OcclusionMap", maps.AO);
            material.SetFloat("_OcclusionStrength", 1f);
            material.SetFloat("_Metallic", spec.Metallic);
            // With a packed map the shader multiplies its alpha by _Smoothness, so 1 keeps the map's values.
            material.SetFloat("_Smoothness", maps.MetallicSmoothness != null ? 1f : spec.Smoothness);
            material.SetTextureScale("_BaseMap", Vector2.one * spec.RepeatsPerMetre);
            material.SetTextureOffset("_BaseMap", Vector2.zero);
            material.SetFloat("_Surface", spec.Transparent ? 1f : 0f); // 1 = Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetFloat("_Cull", spec.DoubleSided ? 0f : 2f); // Render Face: 0 = Both, 2 = Front

            // URP's own setup: keywords for the maps above, blend modes, ZWrite and render queue.
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);
        }

        private static void BuildMaterial(SurfaceSpec spec, SurfaceMaps maps)
        {
            string path = MaterialPath(spec.Name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find(LitShader)) { name = MaterialPrefix + spec.Name };
                Configure(material, spec, maps);
                AssetDatabase.CreateAsset(material, path);
                return;
            }

            material.shader = Shader.Find(LitShader);
            Configure(material, spec, maps);
            EditorUtility.SetDirty(material);
        }

        /// <summary>
        /// The surface's textures in <paramref name="folder"/>. Roughness and Metallic are packed first; problems are
        /// appended to <paramref name="report"/> as warnings.
        /// </summary>
        public static SurfaceMaps FindMaps(SurfaceSpec spec, string folder, StringBuilder report)
        {
            Dictionary<TextureMap, string> files = FindTextureFiles(folder, report);
            string packed = null;
            if (files.ContainsKey(TextureMap.Roughness) || files.ContainsKey(TextureMap.Metallic))
            {
                packed = WriteMetallicSmoothness(spec, folder, files, report);
            }
            else if (files.TryGetValue(TextureMap.MetallicSmoothness, out string supplied))
            {
                packed = supplied; // already packed by hand
            }

            return new SurfaceMaps
            {
                BaseColor = Load(files, TextureMap.BaseColor),
                Normal = Load(files, TextureMap.Normal),
                MetallicSmoothness = packed != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(packed) : null,
                AO = Load(files, TextureMap.AO),
            };
        }

        /// <summary>The first file (by path) for each map in the surface's folder; extra or unnamed files are reported.</summary>
        private static Dictionary<TextureMap, string> FindTextureFiles(string folder, StringBuilder report)
        {
            var files = new Dictionary<TextureMap, string>();
            if (!AssetDatabase.IsValidFolder(folder))
            {
                return files;
            }

            IEnumerable<string> paths = AssetDatabase.FindAssets("t:Texture2D", new[] { folder })
                .Select(AssetDatabase.GUIDToAssetPath).Distinct().OrderBy(path => path, StringComparer.Ordinal);
            foreach (string path in paths)
            {
                TextureMap map = EnvironmentTextureImport.MapOf(path);
                if (map == TextureMap.Unknown)
                {
                    report.Append($"\n  WARNING {path}: no map word in the name (BaseColor, Normal, Roughness, Metallic, AO), ignored.");
                }
                else if (files.ContainsKey(map))
                {
                    report.Append($"\n  WARNING {path}: a second {map} map, ignored (using {files[map]}).");
                }
                else
                {
                    files.Add(map, path);
                }
            }

            return files;
        }

        /// <summary>Packs Roughness and Metallic into one PNG next to them and returns its path, or null if unreadable.</summary>
        private static string WriteMetallicSmoothness(SurfaceSpec spec, string folder, Dictionary<TextureMap, string> files,
            StringBuilder report)
        {
            files.TryGetValue(TextureMap.Roughness, out string roughnessPath);
            files.TryGetValue(TextureMap.Metallic, out string metallicPath);
            Texture2D roughness = LoadPixels(roughnessPath);
            Texture2D metallic = LoadPixels(metallicPath);
            try
            {
                if ((roughnessPath != null && roughness == null) || (metallicPath != null && metallic == null))
                {
                    report.Append($"\n  WARNING {spec.Name}: Roughness/Metallic must be PNG or JPG to be packed; using constants.");
                    return null;
                }

                bool hero = EnvironmentTextureImport.IsHero(roughnessPath ?? metallicPath);
                string path = $"{folder}/{spec.Name}_{TextureMap.MetallicSmoothness}"
                    + (hero ? EnvironmentTextureImport.HeroSuffix : string.Empty) + ".png";
                Texture2D packed = MetallicSmoothnessPacker.Pack(roughness, spec.Smoothness, metallic, spec.Metallic,
                    hero ? EnvironmentTextureImport.HeroMaxSize : EnvironmentTextureImport.MaxSize);
                byte[] png = packed.EncodeToPNG();
                UnityEngine.Object.DestroyImmediate(packed);

                // Only write when the pixels changed, so a re-run does not touch the file.
                if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(png))
                {
                    File.WriteAllBytes(path, png);
                    AssetDatabase.ImportAsset(path);
                }

                return path;
            }
            finally
            {
                if (roughness != null)
                {
                    UnityEngine.Object.DestroyImmediate(roughness);
                }

                if (metallic != null)
                {
                    UnityEngine.Object.DestroyImmediate(metallic);
                }
            }
        }

        /// <summary>Reads a PNG or JPG from disk at full size, whatever its import settings. Null if missing or unreadable.</summary>
        private static Texture2D LoadPixels(string assetPath)
        {
            if (assetPath == null)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (texture.LoadImage(File.ReadAllBytes(assetPath)))
            {
                return texture;
            }

            UnityEngine.Object.DestroyImmediate(texture);
            return null;
        }

        private static Texture2D Load(Dictionary<TextureMap, string> files, TextureMap map)
        {
            return files.TryGetValue(map, out string path) ? AssetDatabase.LoadAssetAtPath<Texture2D>(path) : null;
        }

        private static string Describe(SurfaceSpec spec, SurfaceMaps maps)
        {
            if (!maps.Textured && maps.Normal == null && maps.MetallicSmoothness == null && maps.AO == null)
            {
                return $"flat #{ColorUtility.ToHtmlStringRGB(spec.FlatColour)}, smoothness {spec.Smoothness:0.00}, metallic {spec.Metallic:0.00}";
            }

            var names = new List<string>();
            foreach (Texture2D map in new[] { maps.BaseColor, maps.Normal, maps.MetallicSmoothness, maps.AO })
            {
                if (map != null)
                {
                    names.Add(map.name);
                }
            }

            string colour = maps.Textured ? "textured" : $"flat #{ColorUtility.ToHtmlStringRGB(spec.FlatColour)} with maps";
            return $"{colour} ({string.Join(", ", names)}), {spec.RepeatsPerMetre:0.##} repeats per metre";
        }
    }
}
