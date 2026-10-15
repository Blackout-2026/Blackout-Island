using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>The texture maps of docs/art-pipeline.md section 7, named by the suffix of T_&lt;Name&gt;_&lt;Map&gt;.</summary>
    public enum ModelTextureMap
    {
        Unknown,
        BaseColor,
        Normal,
        MetalSmooth,
        Emission,
    }

    /// <summary>
    /// The import settings every texture in Assets/Textures/Models gets (docs/art-pipeline.md section 7).
    /// Pure rules: the postprocessor decides when to call them.
    /// </summary>
    public static class ModelTextureRules
    {
        public const string Folder = "Assets/Textures/Models/";

        // Hero models use 2048 maps (section 7); smaller props simply author smaller files.
        public const int MaxSize = 2048;

        public static bool IsManaged(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                   && assetPath.Replace('\\', '/').StartsWith(Folder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Reads the map from the file name: T_Generator_Normal.png is a Normal map.</summary>
        public static ModelTextureMap MapOf(string assetPath)
        {
            string name = Path.GetFileNameWithoutExtension(assetPath ?? string.Empty);
            int cut = name.LastIndexOf('_');
            if (cut < 0)
            {
                return ModelTextureMap.Unknown;
            }

            string suffix = name.Substring(cut + 1);
            foreach (ModelTextureMap map in Enum.GetValues(typeof(ModelTextureMap)))
            {
                if (map != ModelTextureMap.Unknown && string.Equals(suffix, map.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    return map;
                }
            }

            return ModelTextureMap.Unknown;
        }

        public static void Apply(TextureImporter importer, ModelTextureMap map)
        {
            importer.maxTextureSize = MaxSize;
            importer.mipmapEnabled = true;
            importer.isReadable = false;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.wrapMode = TextureWrapMode.Repeat;
            if (Path.GetFileNameWithoutExtension(importer.assetPath) == "T_Sign_Atlas_BaseColor")
            {
                importer.wrapMode = TextureWrapMode.Clamp;
            }

            switch (map)
            {
                case ModelTextureMap.Normal:
                    // Tangent space, OpenGL (Y+): what Blender bakes and what Unity expects.
                    importer.textureType = TextureImporterType.NormalMap;
                    break;
                case ModelTextureMap.MetalSmooth:
                    // Data, not colour: linear, metallic in R and smoothness in the alpha channel.
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = false;
                    importer.alphaSource = TextureImporterAlphaSource.FromInput;
                    importer.alphaIsTransparency = false;
                    break;
                default:
                    // BaseColor and Emission are colour maps.
                    importer.textureType = TextureImporterType.Default;
                    importer.sRGBTexture = true;
                    break;
            }
        }
    }
}
