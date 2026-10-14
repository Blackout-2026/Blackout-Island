using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the weapon viewmodel materials M_Weapon_&lt;Key&gt; in Assets/Materials/Models (docs/art-pipeline.md section 14).
    /// Colours are the prototype's (weapons.js, core-developer.md 5.1 and 8.1). Re-runnable; hand edits are kept.
    /// </summary>
    public static class WeaponMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string Prefix = "M_Weapon_";

        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec(Prefix + "Metal", "#2e3135", 0.85f, 0.62f),
            new ModelMaterialSpec(Prefix + "Polymer", "#1b1d1f", 0.1f, 0.4f),
            new ModelMaterialSpec(Prefix + "Tan", "#6b604a", 0f, 0.3f),
            new ModelMaterialSpec(Prefix + "Steel", "#b8bec2", 1f, 0.82f),
            new ModelMaterialSpec(Prefix + "Glove", "#2b2c2a", 0f, 0.1f),
            new ModelMaterialSpec(Prefix + "Sleeve", "#3a4030", 0f, 0f),
            new ModelMaterialSpec(Prefix + "Lens", "#0d1f29", 0f, 0.9f),
            new ModelMaterialSpec(Prefix + "RedDot", "#1a0504", 0f, 0.3f, emission: new Color(8f, 0.4f, 0.2f)), // core-developer.md 5.1
            new ModelMaterialSpec(Prefix + "Flash", "#ffeccc", 0f, 0f, alpha: 0.6f, emission: new Color(3.5f, 2.6f, 1.6f)), // 5.4, a stand-in for the additive flash texture
        };

        [MenuItem("Blackout/Art/Build Weapon Materials")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Debug.Log($"[WeaponMaterials] {Specs.Length} materials are up to date in {Folder}");
        }

        public static PartMaterials BuildAll()
        {
            return BuildAll(Folder, TextureFolder);
        }

        public static PartMaterials BuildAll(string folder, string textureFolder)
        {
            return PartMaterials.Build(Prefix, Specs, folder, textureFolder);
        }
    }
}
