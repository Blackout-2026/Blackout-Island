using System.IO;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>One model material: a flat placeholder look that the texture set replaces when it exists.</summary>
    public sealed class ModelMaterialSpec
    {
        public readonly string Name;
        public readonly string Hex;
        public readonly float Metallic;
        public readonly float Smoothness;
        public readonly float Alpha;
        public readonly Color Emission;
        public readonly bool Textured;
        public readonly bool EmissionMask;
        public readonly bool AlphaClip;    // cut-out transparency (hair cards): where the base map's alpha is below 0.45 nothing is drawn
        public readonly bool DoubleSided;  // draw both faces (single-sided cards)

        public ModelMaterialSpec(string name, string hex, float metallic, float smoothness, float alpha = 1f,
            Color emission = default, bool textured = false, bool emissionMask = false, bool alphaClip = false, bool doubleSided = false)
        {
            Name = name;
            Hex = hex;
            Metallic = metallic;
            Smoothness = smoothness;
            Alpha = alpha;
            Emission = emission;
            Textured = textured;
            EmissionMask = emissionMask;
            AlphaClip = alphaClip;
            DoubleSided = doubleSided;
        }

        public bool HasEmission => Emission.maxColorComponent > 0f;
    }

    /// <summary>
    /// Creates the URP Lit materials of a custom model in Assets/Materials/Models (docs/art-pipeline.md section 2).
    /// Shared by the generator and the robot. Non-destructive: a missing material is created with flat placeholder
    /// colours; an existing one keeps every hand edit, and only gets a texture where its slot is still empty and
    /// the file now exists (T_&lt;Name&gt;_BaseColor, _Normal, _MetalSmooth, _Emission).
    /// </summary>
    public static class ModelMaterials
    {
        /// <summary>The existing material at folder/spec.Name.mat, or a new one with the flat look of the spec.</summary>
        /// <param name="texturePrefix">The texture name up to the map, for example "T_Generator_".</param>
        public static Material GetOrCreate(ModelMaterialSpec spec, string folder, string textureFolder, string texturePrefix)
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

            if (spec.Textured && AssignMissingTextures(material, spec, textureFolder, texturePrefix))
            {
                EditorUtility.SetDirty(material);
            }

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

        private static void ApplyFlatLook(Material material, ModelMaterialSpec spec)
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

            if (spec.AlphaClip)
            {
                material.SetFloat("_AlphaClip", 1f);
                material.SetFloat("_Cutoff", 0.45f);
            }

            if (spec.DoubleSided)
            {
                material.SetFloat("_Cull", 0f);
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

        private static bool AssignMissingTextures(Material material, ModelMaterialSpec spec, string textureFolder, string texturePrefix)
        {
            bool changed = false;

            Texture2D baseColor = FindTexture(textureFolder, texturePrefix, "BaseColor");
            if (baseColor != null && material.GetTexture("_BaseMap") == null)
            {
                material.SetTexture("_BaseMap", baseColor);
                material.SetColor("_BaseColor", Color.white); // The flat placeholder colour would tint the map.
                changed = true;
            }

            Texture2D normal = FindTexture(textureFolder, texturePrefix, "Normal");
            if (normal != null && material.GetTexture("_BumpMap") == null)
            {
                material.SetTexture("_BumpMap", normal);
                material.EnableKeyword("_NORMALMAP");
                changed = true;
            }

            Texture2D metalSmooth = FindTexture(textureFolder, texturePrefix, "MetalSmooth");
            if (metalSmooth != null && material.GetTexture("_MetallicGlossMap") == null)
            {
                material.SetTexture("_MetallicGlossMap", metalSmooth);
                material.SetFloat("_Smoothness", 1f); // The map's alpha is the smoothness; this scales it by 1.
                material.EnableKeyword("_METALLICSPECGLOSSMAP");
                changed = true;
            }

            if (spec.EmissionMask)
            {
                Texture2D emission = FindTexture(textureFolder, texturePrefix, "Emission");
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

        private static Texture2D FindTexture(string textureFolder, string texturePrefix, string map)
        {
            if (!AssetDatabase.IsValidFolder(textureFolder))
            {
                return null;
            }

            string wanted = texturePrefix + map;
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
