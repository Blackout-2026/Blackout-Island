using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
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
        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec(BodyName, "#5f6a4a", 0.4f, 0f, textured: true),
            new ModelMaterialSpec(MetalName, "#2b2e31", 0.7f, 0.55f, textured: true),
            new ModelMaterialSpec(DetailsName, "#d9d4c3", 0f, 0.4f, textured: true, emissionMask: true),
            new ModelMaterialSpec(GlassName, "#9fc9d0", 0.2f, 0.94f, alpha: 0.12f),
            new ModelMaterialSpec(CoreName, "#0d598c", 0f, 0.5f, emission: new Color(0.55f, 1.5f, 2f)),
            new ModelMaterialSpec(CoreOffName, "#0a1218", 0.2f, 0.3f),
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

            foreach (ModelMaterialSpec spec in Specs)
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

        public static Material GetOrCreate(ModelMaterialSpec spec, string folder, string textureFolder)
        {
            return ModelMaterials.GetOrCreate(spec, folder, textureFolder, TexturePrefix);
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
            ModelMaterials.EnsureFolder(folder);
        }
    }
}
