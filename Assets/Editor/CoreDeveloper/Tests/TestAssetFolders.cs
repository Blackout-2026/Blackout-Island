using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor.Tests
{
    /// <summary>
    /// Scratch folders for the tests that must go through the real importer. Both are created and deleted by the
    /// tests themselves, and nothing else is ever written there.
    /// </summary>
    public static class TestAssetFolders
    {
        public const string Materials = "Assets/Materials/Models/_Test";
        public const string Textures = "Assets/Textures/Models/_Test";
        public const string Prefabs = "Assets/Prefabs/Props/_Test";

        public static void Create()
        {
            Delete();
            GeneratorMaterialBuilder.EnsureFolder(Textures);
        }

        public static void Delete()
        {
            foreach (string folder in new[] { Materials, Textures, Prefabs })
            {
                if (AssetDatabase.IsValidFolder(folder))
                {
                    AssetDatabase.DeleteAsset(folder);
                }
            }
        }

        /// <summary>Writes a tiny PNG at an asset path and imports it.</summary>
        public static string WriteTexture(string fileName)
        {
            string assetPath = $"{Textures}/{fileName}";
            var texture = new Texture2D(4, 4);
            File.WriteAllBytes(Path.GetFullPath(assetPath), texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(assetPath);
            return assetPath;
        }
    }
}
