using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Blackout.Editor
{
    /// <summary>Writes the measured C5 inventory without overwriting hand-written viva notes.</summary>
    public static class C5ModelAudit
    {
        private const string ModelFolder = "Assets/Models/Custom";
        private const string ReportPath = "docs/viva/core-developer.md";
        private const string BeginMarker = "<!-- C5 MODEL INVENTORY BEGIN -->";
        private const string EndMarker = "<!-- C5 MODEL INVENTORY END -->";

        [MenuItem("Blackout/Art/Model Report")]
        public static void ShowReport()
        {
            var report = new StringBuilder();
            report.AppendLine("## Measured model inventory");
            report.AppendLine();
            report.AppendLine("Measured by Blackout > Art > Model Report. FBX rows are imported Blender models; prefab rows without an FBX behind them are still primitive placeholders.");
            report.AppendLine();
            report.AppendLine("| Source | Asset | Triangles | Renderers | Materials | UV channels | Texture sizes |");
            report.AppendLine("| --- | --- | ---: | ---: | ---: | --- | --- |");

            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ModelFolder });
            var paths = new List<string>();
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                {
                    paths.Add(path);
                }
            }

            paths.Sort(StringComparer.Ordinal);
            foreach (string path in paths)
            {
                AddRow(report, "FBX", path, AssetDatabase.LoadAssetAtPath<GameObject>(path));
            }

            if (paths.Count == 0)
            {
                report.AppendLine("| FBX | None imported in `Assets/Models/Custom` | — | — | — | — | — |");
            }

            foreach (PropEntry entry in PropCatalog.All)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                if (prefab != null)
                {
                    LODGroup lodGroup = prefab.GetComponent<LODGroup>();
                    if (lodGroup == null)
                    {
                        AddRow(report, "Prefab", entry.PrefabPath, prefab.GetComponentsInChildren<Renderer>(true));
                    }
                    else
                    {
                        LOD[] lods = lodGroup.GetLODs();
                        for (int i = 0; i < lods.Length; i++)
                        {
                            AddRow(report, "Prefab LOD" + i, entry.PrefabPath, lods[i].renderers);
                        }
                    }
                }
            }

            foreach (DecalSpec spec in DecalCatalog.All)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.Entry.PrefabPath);
                if (prefab != null)
                {
                    AddRow(report, "Sign", spec.Entry.PrefabPath, prefab);
                }
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string fullPath = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            string existing = File.Exists(fullPath) ? File.ReadAllText(fullPath) : "# Core Developer Viva\n";
            string generated = BeginMarker + "\n" + report + EndMarker;
            int begin = existing.IndexOf(BeginMarker, StringComparison.Ordinal);
            int end = existing.IndexOf(EndMarker, StringComparison.Ordinal);
            string updated;
            if (begin >= 0 && end > begin)
            {
                updated = existing.Substring(0, begin) + generated + existing.Substring(end + EndMarker.Length);
            }
            else
            {
                updated = existing.TrimEnd() + "\n\n" + generated + "\n";
            }

            if (!string.Equals(existing, updated, StringComparison.Ordinal))
            {
                File.WriteAllText(fullPath, updated, new UTF8Encoding(false));
            }

            Debug.Log("[C5 Model Report] Wrote " + ReportPath + " (" + paths.Count + " imported FBX models).");
        }

        private static void AddRow(StringBuilder report, string source, string path, GameObject root)
        {
            if (root == null)
            {
                report.Append("| ").Append(source).Append(" | `").Append(path).AppendLine("` | could not load | — | — | — | — |");
                return;
            }

            AddRow(report, source, path, root.GetComponentsInChildren<Renderer>(true));
        }

        private static void AddRow(StringBuilder report, string source, string path, Renderer[] rendererSet)
        {
            int triangles = 0;
            int renderers = 0;
            var materials = new HashSet<Material>();
            var meshes = new HashSet<Mesh>();
            var textures = new HashSet<Texture>();
            foreach (Renderer renderer in rendererSet)
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null)
                {
                    continue;
                }

                renderers++;
                triangles += MeshStats.CountTriangles(mesh);
                meshes.Add(mesh);
                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        materials.Add(material);
                    }
                }
            }

            foreach (Material material in materials)
            {
                foreach (string property in material.GetTexturePropertyNames())
                {
                    Texture texture = material.GetTexture(property);
                    if (texture != null)
                    {
                        textures.Add(texture);
                    }
                }
            }

            var channels = new List<string>();
            for (int i = 0; i < 8; i++)
            {
                foreach (Mesh mesh in meshes)
                {
                    if (mesh.HasVertexAttribute((VertexAttribute)((int)VertexAttribute.TexCoord0 + i)))
                    {
                        channels.Add("UV" + i);
                        break;
                    }
                }
            }

            var textureSizes = new List<string>();
            foreach (Texture texture in textures)
            {
                textureSizes.Add(texture.name + " " + texture.width + "x" + texture.height);
            }

            textureSizes.Sort(StringComparer.Ordinal);
            report.Append("| ").Append(source).Append(" | `").Append(path).Append("` | ").Append(triangles)
                .Append(" | ").Append(renderers).Append(" | ").Append(materials.Count)
                .Append(" | ").Append(channels.Count == 0 ? "none" : string.Join(", ", channels))
                .Append(" | ").Append(textureSizes.Count == 0 ? "none" : string.Join(", ", textureSizes))
                .AppendLine(" |");
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            return renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        }
    }
}
