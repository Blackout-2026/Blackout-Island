using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One generator material: a flat placeholder look that the texture set replaces when it exists.</summary>
    public sealed class GeneratorMaterialSpec
    {
        public readonly string Name;
        public readonly string Hex;
        public readonly float Metallic;
        public readonly float Smoothness;
        public readonly float Alpha;
        public readonly Color Emission;
        public readonly bool Textured;
        public readonly bool EmissionMask;

        public GeneratorMaterialSpec(string name, string hex, float metallic, float smoothness, float alpha = 1f,
            Color emission = default, bool textured = false, bool emissionMask = false)
        {
            Name = name;
            Hex = hex;
            Metallic = metallic;
            Smoothness = smoothness;
            Alpha = alpha;
            Emission = emission;
            Textured = textured;
            EmissionMask = emissionMask;
        }

        public bool HasEmission => Emission.maxColorComponent > 0f;
    }

    /// <summary>The materials the generator prefab needs, found by name.</summary>
    public sealed class GeneratorMaterialSet
    {
        public Material Body;
        public Material Metal;
        public Material Details;
        public Material Glass;
        public Material Core;
        public Material CoreOff;
        public Material Spark;

        /// <summary>The material for a Blender material name (M_Generator_Core), or null for an unknown name.</summary>
        public Material Find(string materialName)
        {
            switch (materialName)
            {
                case GeneratorMaterialBuilder.BodyName: return Body;
                case GeneratorMaterialBuilder.MetalName: return Metal;
                case GeneratorMaterialBuilder.DetailsName: return Details;
                case GeneratorMaterialBuilder.GlassName: return Glass;
                case GeneratorMaterialBuilder.CoreName: return Core;
                case GeneratorMaterialBuilder.CoreOffName: return CoreOff;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Creates the URP Lit materials of the generator in Assets/Materials/Models (docs/art-pipeline.md section 2).
    /// Re-runnable and non-destructive: a missing material is created with flat placeholder colours; an existing one
    /// keeps every hand edit, and only gets a texture where its slot is still empty and the file now exists.
    /// Run it again after adding T_Generator_* to Assets/Textures/Models.
    /// </summary>
    public static class GeneratorMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";

        public const string BodyName = "M_Generator_Body";
        public const string MetalName = "M_Generator_Metal";
        public const string DetailsName = "M_Generator_Details";
        public const string GlassName = "M_Generator_Glass";
        public const string CoreName = "M_Generator_Core";
        public const string CoreOffName = "M_Generator_CoreOff";
        public const string SparkName = "M_Generator_Spark";

        private const string TexturePrefix = "T_Generator_";

        // Colours, metallic and smoothness come from docs/reference/core-developer.md 2.4 (smoothness = 1 - roughness).
        // The core is cyan-white and emissive; its peak is about 2 (section 8.2). CoreOff is what it becomes in the blackout.
        public static readonly GeneratorMaterialSpec[] Specs =
        {
            new GeneratorMaterialSpec(BodyName, "#5f6a4a", 0.4f, 0f, textured: true),
            new GeneratorMaterialSpec(MetalName, "#2b2e31", 0.7f, 0.55f, textured: true),
            new GeneratorMaterialSpec(DetailsName, "#d9d4c3", 0f, 0.4f, textured: true, emissionMask: true),
            new GeneratorMaterialSpec(GlassName, "#9fc9d0", 0.2f, 0.94f, alpha: 0.12f),
            new GeneratorMaterialSpec(CoreName, "#0d598c", 0f, 0.5f, emission: new Color(0.55f, 1.5f, 2f)),
            new GeneratorMaterialSpec(CoreOffName, "#0a1218", 0.2f, 0.3f),
        };

        [MenuItem("Blackout/Art/Build Generator Materials")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Debug.Log($"[GeneratorMaterials] Materials are up to date in {Folder}");
        }

        public static GeneratorMaterialSet BuildAll()
        {
            return BuildAll(Folder, TextureFolder);
        }

        public static GeneratorMaterialSet BuildAll(string folder, string textureFolder)
        {
            var set = new GeneratorMaterialSet
            {
                Spark = GetOrCreateSpark(folder),
            };

            foreach (GeneratorMaterialSpec spec in Specs)
            {
                Material material = GetOrCreate(spec, folder, textureFolder);
                switch (spec.Name)
                {
                    case BodyName: set.Body = material; break;
                    case MetalName: set.Metal = material; break;
                    case DetailsName: set.Details = material; break;
                    case GlassName: set.Glass = material; break;
                    case CoreName: set.Core = material; break;
                    case CoreOffName: set.CoreOff = material; break;
                }
            }

            AssetDatabase.SaveAssets();
            return set;
        }

        public static Material GetOrCreate(GeneratorMaterialSpec spec, string folder, string textureFolder)
        {
            string path = $"{folder}/{spec.Name}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                EnsureFolder(folder);
                material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                ApplyFlatLook(material, spec);
                AssetDatabase.CreateAsset(material, path);
            }

            if (spec.Textured && AssignMissingTextures(material, spec, textureFolder))
            {
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        public static Material GetOrCreateSpark(string folder)
        {
            string path = $"{folder}/{SparkName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            EnsureFolder(folder);
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Universal Render Pipeline/Unlit");
            material = new Material(shader);
            material.SetColor("_BaseColor", new Color(1f, 0.75f, 0.25f, 1f));
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 2f); // Additive: sparks only ever add light
            BaseShaderGUI.SetMaterialKeywords(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>Creates a folder and any missing parents, using AssetDatabase so the .meta files are written.</summary>
        public static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        private static void ApplyFlatLook(Material material, GeneratorMaterialSpec spec)
        {
            ColorUtility.TryParseHtmlString(spec.Hex, out Color colour);
            colour.a = spec.Alpha;
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Metallic", spec.Metallic);
            material.SetFloat("_Smoothness", spec.Smoothness);

            if (spec.Alpha < 1f)
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f); // Alpha
                material.SetFloat("_Cull", 0f); // The glass tube is seen from both sides
            }

            // HDR colour: values above 1 are intentional, they feed the bloom. URP only turns the _EMISSION keyword
            // on when the material's GI flag says it emits (the Inspector's Emission checkbox sets it), so set it here.
            material.SetColor("_EmissionColor", spec.Emission);
            if (spec.HasEmission)
            {
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            }

            // URP's own material setup: keywords, blend state and render queue to match the properties above.
            BaseShaderGUI.SetMaterialKeywords(material);
        }

        private static bool AssignMissingTextures(Material material, GeneratorMaterialSpec spec, string textureFolder)
        {
            bool changed = false;

            Texture2D baseColor = FindTexture(textureFolder, "BaseColor");
            if (baseColor != null && material.GetTexture("_BaseMap") == null)
            {
                material.SetTexture("_BaseMap", baseColor);
                material.SetColor("_BaseColor", Color.white); // The flat placeholder colour would tint the map.
                changed = true;
            }

            Texture2D normal = FindTexture(textureFolder, "Normal");
            if (normal != null && material.GetTexture("_BumpMap") == null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                changed = true;
            }

            Texture2D metalSmooth = FindTexture(textureFolder, "MetalSmooth");
            if (metalSmooth != null && material.GetTexture("_MetallicGlossMap") == null)
            {
                material.SetTexture("_MetallicGlossMap", metalSmooth);
                material.SetFloat("_Smoothness", 1f); // The map's alpha is the smoothness; this scales it by 1.
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                changed = true;
            }

            if (spec.EmissionMask)
            {
                Texture2D emission = FindTexture(textureFolder, "Emission");
                if (emission != null && material.GetTexture("_EmissionMap") == null)
                {
                    material.SetTexture("_EmissionMap", emission);
                    material.SetColor("_EmissionColor", Color.white);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
                    changed = true;
                }
            }

            if (changed)
            {
                BaseShaderGUI.SetMaterialKeywords(material);
            }

            return changed;
        }

        private static Texture2D FindTexture(string textureFolder, string map)
        {
            if (!AssetDatabase.IsValidFolder(textureFolder))
            {
                return null;
            }

            string wanted = TexturePrefix + map;
            foreach (string guid in AssetDatabase.FindAssets($"{wanted} t:Texture2D", new[] { textureFolder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileNameWithoutExtension(path) == wanted)
                {
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                }
            }

            return null;
        }
    }
}
