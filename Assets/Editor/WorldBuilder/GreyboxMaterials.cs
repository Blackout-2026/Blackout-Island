using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Flat-colour URP Lit placeholder materials for the World Builder's greybox tools, kept in
    /// Assets/Materials/Environment/Greybox. A material is created once with a palette colour; an existing one is
    /// returned as it is, so hand edits survive re-runs. The W5 material library replaces them.
    /// </summary>
    public static class GreyboxMaterials
    {
        public const string Folder = "Assets/Materials/Environment/Greybox";

        public static Material GetOrCreate(string materialName, string hex, float smoothness, float metallic = 0f, float alpha = 1f)
        {
            string path = $"{Folder}/{materialName}.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder("Assets/Materials/Environment"))
            {
                AssetDatabase.CreateFolder("Assets/Materials", "Environment");
            }

            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Materials/Environment", "Greybox");
            }

            ColorUtility.TryParseHtmlString(hex, out Color colour);
            colour.a = alpha;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", smoothness);
            material.SetFloat("_Metallic", metallic);
            if (alpha < 1f)
            {
                material.SetFloat("_Surface", 1f); // Transparent
                material.SetFloat("_Blend", 0f); // Alpha
            }

            // URP's own material setup: keywords, blend modes, ZWrite and render queue to match the properties above.
            BaseShaderGUI.SetMaterialKeywords(material);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
