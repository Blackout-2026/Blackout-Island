using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the shared prop palette in Assets/Materials/Models as M_Prop_&lt;Key&gt; (docs/art-pipeline.md section 14). One
    /// palette for every prop, so the game keeps the prototype's single colour language (core-developer.md 8.1) and the
    /// importer's two-material limit can be met by picking two of these in Blender. Re-runnable and non-destructive: an
    /// existing material keeps every hand edit.
    /// </summary>
    public static class PropMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        public const string Prefix = "M_Prop_";

        // Colours, metalness and smoothness (= 1 - roughness) are the prototype's (core-developer.md 8.1 and 8.2). Glow
        // colours are HDR values above 1 on purpose: they feed the bloom.
        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec(Prefix + "MetalDark", "#2b2e31", 0.7f, 0.55f),
            new ModelMaterialSpec(Prefix + "Steel", "#9aa0a4", 0.9f, 0.7f),
            new ModelMaterialSpec(Prefix + "Chrome", "#d9dde0", 1f, 0.88f),
            new ModelMaterialSpec(Prefix + "Olive", "#6a7350", 0.4f, 0f),
            new ModelMaterialSpec(Prefix + "Locker", "#51606b", 0.5f, 0f),
            new ModelMaterialSpec(Prefix + "LabWhite", "#dfe5e6", 0.05f, 0.65f),
            new ModelMaterialSpec(Prefix + "PlasticDark", "#23272b", 0f, 0.45f),
            new ModelMaterialSpec(Prefix + "DeskTop", "#3c4146", 0.1f, 0.4f),
            new ModelMaterialSpec(Prefix + "Wood", "#7a6a5a", 0f, 0f),
            new ModelMaterialSpec(Prefix + "MedWhite", "#e8e8e2", 0f, 0.5f),
            new ModelMaterialSpec(Prefix + "MedRed", "#c41e1e", 0f, 0.5f),
            new ModelMaterialSpec(Prefix + "Glass", "#9fc9d0", 0.2f, 0.94f, alpha: 0.12f),
            new ModelMaterialSpec(Prefix + "GlowCyan", "#0a2325", 0f, 0.3f, emission: new Color(0.7f, 2.05f, 2.1f)), // cyanGlow #47d1d6 x 2.5
            new ModelMaterialSpec(Prefix + "GlowGreen", "#0c2a12", 0f, 0.3f, emission: new Color(0.5f, 2f, 0.75f)), // greenLed #40ff60 x 2
            new ModelMaterialSpec(Prefix + "GlowScreen", "#06181c", 0f, 0.6f, emission: new Color(0.1f, 0.55f, 0.65f)),
            new ModelMaterialSpec(Prefix + "CureVial", "#05181c", 0f, 0.3f, emission: new Color(0.4f, 2.6f, 2.8f)), // core-developer.md 6.1
        };

        [MenuItem("Blackout/Art/Build Prop Materials")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Debug.Log($"[PropMaterials] {Specs.Length} materials are up to date in {Folder}");
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
