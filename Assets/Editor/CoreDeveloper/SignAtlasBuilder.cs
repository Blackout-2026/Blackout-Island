using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>Packs the generated signs into one texture and one material without changing hand-assigned decals.</summary>
    public static class SignAtlasBuilder
    {
        public const string TexturePath = "Assets/Textures/Models/T_Sign_Atlas_BaseColor.png";
        public const string MaterialPath = "Assets/Materials/Models/M_Sign_Atlas.mat";
        public const string MeshPath = "Assets/Models/Custom/SignAtlasQuads.asset";
        public const int Width = 2048;
        public const int Height = 512;
        public const int Padding = 4;

        private sealed class Tile
        {
            public DecalSpec Spec;
            public Color32[] Pixels;
            public int Width;
            public int Height;
            public int X;
            public int Y;
        }

        [MenuItem("Blackout/Art/Build Sign Atlas")]
        public static void Build()
        {
            List<Tile> tiles = MakeTiles();
            Pack(tiles);
            Texture2D texture = WriteAtlas(tiles);
            Material material = GetMaterial(texture);
            Mesh sourceQuad = MakeSourceQuad();
            try
            {
                foreach (Tile tile in tiles)
                {
                    Mesh mesh = GetMesh(tile, sourceQuad);
                    ApplyToPrefab(tile.Spec, material, mesh);
                }
            }
            finally
            {
                Object.DestroyImmediate(sourceQuad);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[SignAtlas] Packed {tiles.Count} signs into {Width}x{Height}; prefabs share {MaterialPath}.");
        }

        private static List<Tile> MakeTiles()
        {
            var tiles = new List<Tile>();
            foreach (DecalSpec spec in DecalCatalog.All)
            {
                Color32[] pixels = DecalRenderer.Render(spec, out int width, out int height);
                if (spec.StripeMetres > 0f)
                {
                    int repeatX = Mathf.RoundToInt(spec.Width / spec.StripeMetres);
                    int repeatY = Mathf.RoundToInt(spec.Height / spec.StripeMetres);
                    int bakedWidth = width * repeatX;
                    int bakedHeight = height * repeatY;
                    var repeated = new Color32[bakedWidth * bakedHeight];
                    for (int y = 0; y < bakedHeight; y++)
                    {
                        for (int x = 0; x < bakedWidth; x++)
                        {
                            repeated[y * bakedWidth + x] = pixels[(y % height) * width + x % width];
                        }
                    }

                    pixels = repeated;
                    width = bakedWidth;
                    height = bakedHeight;
                }

                tiles.Add(new Tile { Spec = spec, Pixels = pixels, Width = width, Height = height });
            }

            tiles.Sort((a, b) =>
            {
                int height = b.Height.CompareTo(a.Height);
                return height != 0 ? height : string.CompareOrdinal(a.Spec.Name, b.Spec.Name);
            });
            return tiles;
        }

        private static void Pack(List<Tile> tiles)
        {
            int x = Padding;
            int y = Padding;
            int rowHeight = 0;
            foreach (Tile tile in tiles)
            {
                if (x + tile.Width + Padding > Width)
                {
                    x = Padding;
                    y += rowHeight + Padding * 2;
                    rowHeight = 0;
                }

                if (y + tile.Height + Padding > Height)
                {
                    throw new InvalidOperationException($"Sign atlas is too small for {tile.Spec.Name}.");
                }

                tile.X = x;
                tile.Y = y;
                x += tile.Width + Padding * 2;
                rowHeight = Mathf.Max(rowHeight, tile.Height);
            }
        }

        private static Texture2D WriteAtlas(List<Tile> tiles)
        {
            var pixels = new Color32[Width * Height];
            foreach (Tile tile in tiles)
            {
                // Extrude edge colours into the gutter so bilinear filtering does not pull in a neighbouring sign.
                for (int yy = -Padding; yy < tile.Height + Padding; yy++)
                {
                    int sourceY = Mathf.Clamp(yy, 0, tile.Height - 1);
                    for (int xx = -Padding; xx < tile.Width + Padding; xx++)
                    {
                        int sourceX = Mathf.Clamp(xx, 0, tile.Width - 1);
                        pixels[(tile.Y + yy) * Width + tile.X + xx] = tile.Pixels[sourceY * tile.Width + sourceX];
                    }
                }
            }

            var image = new Texture2D(Width, Height, TextureFormat.RGBA32, false);
            image.SetPixels32(pixels);
            image.Apply();
            byte[] png = image.EncodeToPNG();
            Object.DestroyImmediate(image);
            if (!File.Exists(TexturePath) || !SameBytes(File.ReadAllBytes(TexturePath), png))
            {
                File.WriteAllBytes(TexturePath, png);
                AssetDatabase.ImportAsset(TexturePath);
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        }

        private static Material GetMaterial(Texture2D texture)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Sign_Atlas" };
                material.SetFloat("_Metallic", 0f);
                material.SetFloat("_Smoothness", 0.1f);
                material.enableInstancing = true;
                AssetDatabase.CreateAsset(material, MaterialPath);
            }

            if (material.GetTexture("_BaseMap") != texture || material.GetColor("_BaseColor") != Color.white)
            {
                material.SetTexture("_BaseMap", texture);
                material.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        private static Mesh MakeSourceQuad()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Mesh copy = Object.Instantiate(go.GetComponent<MeshFilter>().sharedMesh);
            Object.DestroyImmediate(go);
            return copy;
        }

        private static Mesh GetMesh(Tile tile, Mesh sourceQuad)
        {
            Mesh mesh = null;
            foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(MeshPath))
            {
                if (asset is Mesh candidate && candidate.name == tile.Spec.Name)
                {
                    mesh = candidate;
                    break;
                }
            }

            bool first = !File.Exists(MeshPath);
            if (mesh == null)
            {
                mesh = new Mesh { name = tile.Spec.Name };
                if (first)
                {
                    AssetDatabase.CreateAsset(mesh, MeshPath);
                }
                else
                {
                    AssetDatabase.AddObjectToAsset(mesh, MeshPath);
                }
            }

            Vector2[] uv = AtlasUv(tile, sourceQuad.uv);
            if (SameMesh(mesh, sourceQuad, uv))
            {
                return mesh;
            }

            mesh.Clear();
            mesh.vertices = sourceQuad.vertices;
            mesh.normals = sourceQuad.normals;
            mesh.tangents = sourceQuad.tangents;
            mesh.uv = uv;
            mesh.triangles = sourceQuad.triangles;
            mesh.RecalculateBounds();
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static Vector2[] AtlasUv(Tile tile, Vector2[] original)
        {
            var uv = new Vector2[original.Length];
            for (int i = 0; i < uv.Length; i++)
            {
                uv[i] = new Vector2((tile.X + 0.5f + original[i].x * (tile.Width - 1f)) / Width,
                    (tile.Y + 0.5f + original[i].y * (tile.Height - 1f)) / Height);
            }

            return uv;
        }

        private static bool SameMesh(Mesh mesh, Mesh source, Vector2[] uv)
        {
            if (mesh.vertexCount != source.vertexCount || mesh.uv.Length != uv.Length)
            {
                return false;
            }

            Vector2[] oldUv = mesh.uv;
            Vector3[] oldVertices = mesh.vertices;
            Vector3[] vertices = source.vertices;
            for (int i = 0; i < uv.Length; i++)
            {
                if (oldUv[i] != uv[i] || oldVertices[i] != vertices[i])
                {
                    return false;
                }
            }

            int[] oldTriangles = mesh.triangles;
            int[] triangles = source.triangles;
            if (oldTriangles.Length != triangles.Length)
            {
                return false;
            }

            for (int i = 0; i < triangles.Length; i++)
            {
                if (oldTriangles[i] != triangles[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ApplyToPrefab(DecalSpec spec, Material material, Mesh mesh)
        {
            string path = spec.Entry.PrefabPath;
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform quad = root.transform.Find(DecalBuilder.QuadName);
                MeshRenderer renderer = quad.GetComponent<MeshRenderer>();
                string current = AssetDatabase.GetAssetPath(renderer.sharedMaterial);
                string generated = $"{DecalBuilder.MaterialFolder}/{spec.MaterialName}.mat";
                if (current != generated && current != MaterialPath)
                {
                    Debug.LogWarning($"[SignAtlas] Kept hand-assigned material on {path}.");
                    return;
                }

                MeshFilter filter = quad.GetComponent<MeshFilter>();
                if (renderer.sharedMaterial == material && filter.sharedMesh == mesh)
                {
                    return;
                }

                renderer.sharedMaterial = material;
                filter.sharedMesh = mesh;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static bool SameBytes(byte[] a, byte[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (a[i] != b[i])
                {
                    return false;
                }
            }

            return true;
        }
    }
}
