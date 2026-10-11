using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Rendering.Universal.ShaderGUI;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Blackout/World/Build Vegetation Placeholders: generates stand-in vegetation prefabs in
    /// Assets/Environment/Vegetation/Placeholder until real models are added (see <see cref="VegetationLibrary"/>):
    /// - Pine, Palm and Rock with three LODs in a LOD group and a CapsuleCollider (the prototype's trunk and boulder
    ///   cylinders; terrain trees only use capsule colliders), RockSmall without a collider,
    /// - Bush and Grass as single meshes for terrain details (no collider, no shadow).
    /// Meshes come from <see cref="VegetationMeshes"/>, colours from the reference's palette (section 3: the middle of
    /// the albedo range, times the tint and the prototype's average per-instance HSL tint). Rocks use Env_Rock from
    /// the material library. Everything it writes is its own: the Placeholder folder only. Re-runnable and
    /// deterministic: assets are updated in place (same GUIDs), so a re-run with no code change changes no file.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.VegetationPlaceholderBuilder.BuildPlaceholders
    /// </summary>
    public static class VegetationPlaceholderBuilder
    {
        public const string MeshFolder = VegetationLibrary.PlaceholderFolder + "/Meshes";
        public const string MaterialFolder = VegetationLibrary.PlaceholderFolder + "/Materials";
        public const string TextureFolder = VegetationLibrary.PlaceholderFolder + "/Textures";
        public const string GrassTexturePath = TextureFolder + "/GrassBlades.asset";
        public const int GrassTextureSize = 128; // the prototype's canvas size

        private const string LitShader = "Universal Render Pipeline/Lit";
        private const string MaterialPrefix = "Veg_";

        /// <summary>A placeholder material: reference albedo range, tint and average per-instance HSL colour.</summary>
        public sealed class FoliageMaterial
        {
            public readonly string Name;
            public readonly string Reference;
            public readonly Color Colour;
            public readonly float Roughness;
            public readonly bool DoubleSided;
            public readonly bool GrassTexture;

            public FoliageMaterial(string name, string reference, string dark, string light, string tint, Vector3? hsl,
                float roughness, bool doubleSided, bool grassTexture = false)
            {
                Name = name;
                Reference = reference;
                Color colour = Color.Lerp(Hex(dark), Hex(light), 0.5f) * Hex(tint);
                if (hsl.HasValue)
                {
                    colour *= HslToRgb(hsl.Value.x, hsl.Value.y, hsl.Value.z);
                }

                colour.a = 1f;
                Colour = colour;
                Roughness = roughness;
                DoubleSided = doubleSided;
                GrassTexture = grassTexture;
            }
        }

        // Reference section 3, "Outdoor and natural". HSL tints are the middle of the prototype's per-instance ranges
        // (forest.js build()), because terrain trees and details share one material per prototype.
        public static readonly IReadOnlyList<FoliageMaterial> Materials = new[]
        {
            new FoliageMaterial("PineBark", "barkPine", "#1E1611", "#5B4636", "#FFFFFF", null, 1f, false),
            new FoliageMaterial("PineNeedles", "needles, HSL 0.26-0.32 / 0.3-0.4 / 0.6-0.7", "#1C3320", "#4A6F40", "#FFFFFF",
                new Vector3(0.29f, 0.35f, 0.65f), 1f, true),
            new FoliageMaterial("PalmBark", "barkPalm", "#2C241B", "#6E5D47", "#FFFFFF", null, 1f, false),
            new FoliageMaterial("PalmLeaf", "leaf (palm fronds), HSL 0.21-0.26 / 0.4 / 0.64", "#2C4520", "#5F8640", "#FFFFFF",
                new Vector3(0.235f, 0.4f, 0.64f), 0.85f, true),
            new FoliageMaterial("Bush", "leaf x #9FAE86, HSL 0.24-0.32 / 0.35 / 0.55-0.65", "#2C4520", "#5F8640", "#9FAE86",
                new Vector3(0.28f, 0.35f, 0.6f), 0.85f, false),
            new FoliageMaterial("Grass", "grassBlades #D2E0C0 (alpha cutoff 0.5), HSL 0.22-0.27 / 0.35 / 0.5-0.65", "#FFFFFF", "#FFFFFF",
                "#D2E0C0", new Vector3(0.245f, 0.35f, 0.575f), 1f, true, grassTexture: true),
        };

        /// <summary>How one placeholder prefab is put together.</summary>
        private sealed class PrefabSpec
        {
            public VegetationPrototype Prototype;
            public string MeshName; // Pine -> Pine_LOD0 .. Pine_LOD2, or a single mesh
            public Func<int, Mesh> Mesh;
            public float[] LodHeights; // screen-relative height where each LOD ends; null = no LOD group
            public string[] MaterialPaths;
            public Vector3? Capsule; // radius, height, centre y; null = no collider
            public bool CastShadows;
        }

        // LOD heights: trees switch at 0.25 / 0.10 of the screen height (about 29 m and 73 m for the 10.6 m pine at
        // the 72 degree FOV) and the last LOD stays until the terrain's tree distance culls it; rocks are smaller, so
        // their thresholds are lower. Capsules: the prototype's tree cylinders (pine r 0.3, 1 m below to 9 m above the
        // ground; palm r 0.25, to 7 m) and rock cylinders (r 0.8 x scale, up to 0.6 x scale above the rock's centre).
        private static PrefabSpec[] Specs()
        {
            string bark = MaterialPath("PineBark");
            string rock = MaterialLibraryBuilder.MaterialPath("Rock");
            return new[]
            {
                new PrefabSpec { Prototype = VegetationPrototype.Pine, MeshName = "Pine", Mesh = VegetationMeshes.Pine,
                    LodHeights = new[] { 0.25f, 0.10f, 0.02f }, MaterialPaths = new[] { bark, MaterialPath("PineNeedles") },
                    Capsule = new Vector3(0.3f, 10f, 4f), CastShadows = true },
                new PrefabSpec { Prototype = VegetationPrototype.Palm, MeshName = "Palm", Mesh = VegetationMeshes.Palm,
                    LodHeights = new[] { 0.25f, 0.10f, 0.02f }, MaterialPaths = new[] { MaterialPath("PalmBark"), MaterialPath("PalmLeaf") },
                    Capsule = new Vector3(0.25f, 8f, 3f), CastShadows = true },
                new PrefabSpec { Prototype = VegetationPrototype.Rock, MeshName = "Rock", Mesh = VegetationMeshes.Rock,
                    LodHeights = new[] { 0.15f, 0.05f, 0.015f }, MaterialPaths = new[] { rock },
                    Capsule = new Vector3(0.8f, 1.6f, -0.2f), CastShadows = true },
                new PrefabSpec { Prototype = VegetationPrototype.RockSmall, MeshName = "Rock", Mesh = VegetationMeshes.Rock,
                    LodHeights = new[] { 0.15f, 0.05f, 0.015f }, MaterialPaths = new[] { rock }, CastShadows = true },
                new PrefabSpec { Prototype = VegetationPrototype.Bush, MeshName = "Bush", Mesh = _ => VegetationMeshes.Bush(),
                    MaterialPaths = new[] { MaterialPath("Bush") } },
                new PrefabSpec { Prototype = VegetationPrototype.Grass, MeshName = "GrassTuft", Mesh = _ => VegetationMeshes.GrassTuft(),
                    MaterialPaths = new[] { MaterialPath("Grass") } },
            };
        }

        public static string MaterialPath(string name)
        {
            return $"{MaterialFolder}/{MaterialPrefix}{name}.mat";
        }

        [MenuItem("Blackout/World/Build Vegetation Placeholders")]
        public static void BuildPlaceholders()
        {
            Stopwatch timer = Stopwatch.StartNew();
            string report = Build();
            Debug.Log($"[World] Vegetation placeholders built in {timer.ElapsedMilliseconds} ms:{report}");
        }

        /// <summary>Builds or updates every placeholder asset and returns a report (one line per prefab).</summary>
        public static string Build()
        {
            EnsureFolder(VegetationLibrary.Folder);
            EnsureFolder(VegetationLibrary.PlaceholderFolder);
            EnsureFolder(MeshFolder);
            EnsureFolder(MaterialFolder);
            EnsureFolder(TextureFolder);

            Texture2D grass = SaveGrassTexture();
            foreach (FoliageMaterial spec in Materials)
            {
                SaveMaterial(spec, grass);
            }

            var report = new StringBuilder();
            var meshes = new Dictionary<string, Mesh[]>();
            foreach (PrefabSpec spec in Specs())
            {
                if (!meshes.TryGetValue(spec.MeshName, out Mesh[] lods))
                {
                    lods = SaveMeshes(spec);
                    meshes.Add(spec.MeshName, lods);
                }

                SavePrefab(spec, lods);
                report.Append($"\n  {spec.Prototype}: ").Append(Describe(spec, lods));
            }

            AssetDatabase.SaveAssets();
            return report.ToString();
        }

        // ---- Assets ---------------------------------------------------------------------------------------------

        private static Mesh[] SaveMeshes(PrefabSpec spec)
        {
            int count = spec.LodHeights != null ? spec.LodHeights.Length : 1;
            var lods = new Mesh[count];
            for (int i = 0; i < count; i++)
            {
                Mesh mesh = spec.Mesh(i);
                string name = spec.LodHeights != null ? $"{spec.MeshName}_LOD{i}" : spec.MeshName;
                mesh.name = name;
                lods[i] = SaveInPlace(mesh, $"{MeshFolder}/{name}.asset");
            }

            return lods;
        }

        // Writes the object as a new asset, or copies it over the existing one so the GUID stays the same.
        private static T SaveInPlace<T>(T generated, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }

            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static Texture2D SaveGrassTexture()
        {
            var texture = new Texture2D(GrassTextureSize, GrassTextureSize, TextureFormat.RGBA32, true, false)
            {
                name = "GrassBlades",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            texture.SetPixels32(VegetationMeshes.GrassBlades(GrassTextureSize));
            texture.Apply(true);
            return SaveInPlace(texture, GrassTexturePath);
        }

        private static void SaveMaterial(FoliageMaterial spec, Texture2D grass)
        {
            string path = MaterialPath(spec.Name);
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = material == null;
            if (created)
            {
                material = new Material(Shader.Find(LitShader)) { name = MaterialPrefix + spec.Name };
            }

            material.shader = Shader.Find(LitShader);
            material.SetColor("_BaseColor", spec.Colour);
            material.SetTexture("_BaseMap", spec.GrassTexture ? grass : null);
            material.SetTextureScale("_BaseMap", Vector2.one);
            material.SetTexture("_BumpMap", null);
            material.SetTexture("_MetallicGlossMap", null);
            material.SetTexture("_OcclusionMap", null);
            material.SetFloat("_Metallic", 0f);
            material.SetFloat("_Smoothness", 1f - spec.Roughness);
            material.SetFloat("_Surface", 0f); // opaque
            material.SetFloat("_AlphaClip", spec.GrassTexture ? 1f : 0f);
            material.SetFloat("_Cutoff", 0.5f);
            material.SetFloat("_Cull", spec.DoubleSided ? 0f : 2f); // Render Face: 0 = Both, 2 = Front
            material.enableInstancing = true; // terrain details are drawn instanced
            BaseShaderGUI.SetMaterialKeywords(material, LitGUI.SetMaterialKeywords);

            if (created)
            {
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
        }

        private static void SavePrefab(PrefabSpec spec, Mesh[] lods)
        {
            string path = VegetationLibrary.PlaceholderPath(spec.Prototype);
            bool exists = File.Exists(path);
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(path) : new GameObject();
            try
            {
                root.name = Path.GetFileNameWithoutExtension(path);
                Configure(root, spec, lods, LoadMaterials(spec.MaterialPaths));
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
        }

        // Sets up the prefab's contents, reusing existing children and components so their IDs stay the same.
        private static void Configure(GameObject root, PrefabSpec spec, Mesh[] lods, Material[] materials)
        {
            root.transform.localPosition = Vector3.zero;
            root.transform.localRotation = Quaternion.identity;
            root.transform.localScale = Vector3.one;

            if (spec.LodHeights == null)
            {
                SetRenderer(root, lods[0], materials, spec.CastShadows);
            }
            else
            {
                var levels = new LOD[lods.Length];
                for (int i = 0; i < lods.Length; i++)
                {
                    Transform child = root.transform.Find($"LOD{i}");
                    if (child == null)
                    {
                        child = new GameObject($"LOD{i}").transform;
                        child.SetParent(root.transform, false);
                    }

                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.identity;
                    child.localScale = Vector3.one;
                    levels[i] = new LOD(spec.LodHeights[i], new Renderer[] { SetRenderer(child.gameObject, lods[i], materials, spec.CastShadows) });
                }

                LODGroup group = GeneratedObjects.GetOrAddComponent<LODGroup>(root);
                group.fadeMode = LODFadeMode.None;
                group.animateCrossFading = false;
                group.SetLODs(levels);
                group.RecalculateBounds();
            }

            var capsule = root.GetComponent<CapsuleCollider>();
            if (spec.Capsule.HasValue)
            {
                capsule = capsule != null ? capsule : root.AddComponent<CapsuleCollider>();
                capsule.radius = spec.Capsule.Value.x;
                capsule.height = spec.Capsule.Value.y;
                capsule.center = new Vector3(0f, spec.Capsule.Value.z, 0f);
                capsule.direction = 1; // Y
            }
            else if (capsule != null)
            {
                Object.DestroyImmediate(capsule);
            }
        }

        private static MeshRenderer SetRenderer(GameObject go, Mesh mesh, Material[] materials, bool castShadows)
        {
            GeneratedObjects.GetOrAddComponent<MeshFilter>(go).sharedMesh = mesh;
            MeshRenderer renderer = GeneratedObjects.GetOrAddComponent<MeshRenderer>(go);
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = true;
            return renderer;
        }

        private static Material[] LoadMaterials(string[] paths)
        {
            var materials = new Material[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                materials[i] = AssetDatabase.LoadAssetAtPath<Material>(paths[i]);
                if (materials[i] == null)
                {
                    Debug.LogError($"[World] Build Vegetation Placeholders: missing material {paths[i]} "
                        + "(run Blackout/World/Build Material Library first for Env_Rock).");
                }
            }

            return materials;
        }

        private static string Describe(PrefabSpec spec, Mesh[] lods)
        {
            var triangles = new List<string>();
            foreach (Mesh mesh in lods)
            {
                triangles.Add(TriangleCount(mesh).ToString());
            }

            string text = $"{string.Join(" / ", triangles)} triangles";
            if (spec.LodHeights != null)
            {
                Bounds bounds = lods[0].bounds;
                float largest = Mathf.Max(bounds.size.x, Mathf.Max(bounds.size.y, bounds.size.z));
                float reach = Mathf.Max(Mathf.Max(-bounds.min.x, bounds.max.x), Mathf.Max(-bounds.min.z, bounds.max.z));
                text += $", size {largest:0.0} m, reach {reach:0.0} m from the trunk, "
                    + $"LOD1 from {VegetationLibrary.LodDistance(largest, spec.LodHeights[0]):0} m, "
                    + $"LOD2 from {VegetationLibrary.LodDistance(largest, spec.LodHeights[1]):0} m at scale 1";
            }

            return text + (spec.Capsule.HasValue ? $", capsule r {spec.Capsule.Value.x} x {spec.Capsule.Value.y} m" : ", no collider");
        }

        public static int TriangleCount(Mesh mesh)
        {
            int count = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                count += (int)mesh.GetIndexCount(i) / 3;
            }

            return count;
        }

        private static void EnsureFolder(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\', '/'), Path.GetFileName(path));
            }
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color colour);
            return colour;
        }

        /// <summary>HSL (each 0 .. 1) to RGB, as three.js Color.setHSL.</summary>
        public static Color HslToRgb(float h, float s, float l)
        {
            float q = l <= 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;
            return new Color(Hue(p, q, h + 1f / 3f), Hue(p, q, h), Hue(p, q, h - 1f / 3f));
        }

        private static float Hue(float p, float q, float t)
        {
            if (t < 0f)
            {
                t += 1f;
            }

            if (t > 1f)
            {
                t -= 1f;
            }

            if (t < 1f / 6f)
            {
                return p + (q - p) * 6f * t;
            }

            if (t < 0.5f)
            {
                return q;
            }

            return t < 2f / 3f ? p + (q - p) * 6f * (2f / 3f - t) : p;
        }
    }
}
