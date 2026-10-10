using System;
using UnityEditor;

namespace Blackout.Editor
{
    /// <summary>Which map a texture file holds, read from its name (see <see cref="EnvironmentTextureImport"/>).</summary>
    public enum TextureMap
    {
        Unknown,
        BaseColor,
        Normal,
        Roughness,
        Metallic,
        AO,
        MetallicSmoothness, // made by Build Material Library from Metallic and Roughness
    }

    /// <summary>The import settings one environment texture should have.</summary>
    public readonly struct TextureImportRule
    {
        public readonly TextureMap Map;
        public readonly int MaxSize;
        public readonly bool Srgb;
        public readonly bool NormalMap;

        public TextureImportRule(TextureMap map, int maxSize, bool srgb, bool normalMap)
        {
            Map = map;
            MaxSize = maxSize;
            Srgb = srgb;
            NormalMap = normalMap;
        }
    }

    /// <summary>
    /// Import rules for every texture under Assets/Textures/Environment, applied by
    /// <see cref="EnvironmentTexturePostprocessor"/> each time one is imported. Files are named
    /// "&lt;Surface&gt;_&lt;Map&gt;[_Hero]", for example Concrete_BaseColor.jpg or Concrete_Normal_Hero.png. Rules: max size
    /// 1024 (2048 when the name ends in _Hero), compressed, mipmaps on, Normal files imported as normal maps, and sRGB
    /// only for colour (BaseColor or unknown); Roughness, Metallic, AO and MetallicSmoothness hold data, so they are linear.
    /// </summary>
    public static class EnvironmentTextureImport
    {
        public const string Folder = "Assets/Textures/Environment";
        public const int MaxSize = 1024;
        public const int HeroMaxSize = 2048;
        public const string HeroSuffix = "_Hero";

        public static bool Applies(string assetPath)
        {
            return assetPath.StartsWith(Folder + "/", StringComparison.Ordinal);
        }

        /// <summary>The map named by one of the underscore-separated words of the file name, else Unknown.</summary>
        public static TextureMap MapOf(string fileName)
        {
            // Compared by name, because Enum.TryParse would also accept a number such as the "2" in "Rock_2_AO".
            foreach (string word in System.IO.Path.GetFileNameWithoutExtension(fileName).Split('_'))
            {
                foreach (TextureMap map in Enum.GetValues(typeof(TextureMap)))
                {
                    if (map != TextureMap.Unknown && string.Equals(word, map.ToString(), StringComparison.OrdinalIgnoreCase))
                    {
                        return map;
                    }
                }
            }

            return TextureMap.Unknown;
        }

        public static TextureImportRule RuleFor(string assetPath)
        {
            TextureMap map = MapOf(assetPath);
            bool hero = System.IO.Path.GetFileNameWithoutExtension(assetPath)
                .EndsWith(HeroSuffix, StringComparison.OrdinalIgnoreCase);
            bool colour = map == TextureMap.BaseColor || map == TextureMap.Unknown;
            return new TextureImportRule(map, hero ? HeroMaxSize : MaxSize, colour, map == TextureMap.Normal);
        }

        public static void Apply(TextureImporter importer, TextureImportRule rule)
        {
            importer.textureType = rule.NormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;
            importer.sRGBTexture = rule.Srgb;
            importer.mipmapEnabled = true;
            importer.maxTextureSize = rule.MaxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.crunchedCompression = false;
        }

        public static bool Matches(TextureImporter importer, TextureImportRule rule)
        {
            return importer.textureType == (rule.NormalMap ? TextureImporterType.NormalMap : TextureImporterType.Default)
                && importer.sRGBTexture == rule.Srgb
                && importer.mipmapEnabled
                && importer.maxTextureSize == rule.MaxSize
                && importer.textureCompression == TextureImporterCompression.Compressed
                && !importer.crunchedCompression;
        }

        /// <summary>Re-imports every environment texture whose settings break the rules, and returns how many.</summary>
        public static int ReimportAll()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                return 0;
            }

            int reimported = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is TextureImporter importer && !Matches(importer, RuleFor(path)))
                {
                    Apply(importer, RuleFor(path));
                    importer.SaveAndReimport();
                    reimported++;
                }
            }

            return reimported;
        }
    }

    /// <summary>Applies <see cref="EnvironmentTextureImport"/>'s rules whenever an environment texture is imported.</summary>
    public class EnvironmentTexturePostprocessor : AssetPostprocessor
    {
        // Bump this when the rules change, so Unity re-imports every texture with the new settings.
        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessTexture()
        {
            if (EnvironmentTextureImport.Applies(assetPath))
            {
                EnvironmentTextureImport.Apply((TextureImporter)assetImporter, EnvironmentTextureImport.RuleFor(assetPath));
            }
        }
    }
}
