using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the nine flat URP Lit materials of the three humanoid agents in Assets/Materials/Models (docs/art-pipeline.md section 18).
    /// The colours are the prototype's (docs/reference/core-developer.md 4.2 to 4.4); there are no textures, so silhouette and colour carry
    /// the look at the 10 to 30 m the characters are seen from (only the Hunter's camouflage uniform has a texture, T_CharHunter_BaseColor). Re-runnable and non-destructive like the robot's: an existing material
    /// keeps every hand edit.
    /// </summary>
    public static class CharacterMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        private const string TexturePrefix = "T_Char_";
        private const string HunterCamoTexturePrefix = "T_CharHunter_";
        private const string HunterSkinTexturePrefix = "T_CharHunterSkin_";
        private const string CommanderUniformPrefix = "T_CharCommanderUniform_";
        private const string CommanderSkinPrefix = "T_CharCommanderSkin_";
        private const string CommanderHairPrefix = "T_CharCommanderHair_";

        // Smoothness = 1 - roughness of the Blender material (build_humans.py).
        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec("M_Char_Patrol_Cloth", "#2b3546", 0f, 0.15f),
            new ModelMaterialSpec("M_Char_Patrol_Gear", "#1a1b1d", 0.1f, 0.2f),
            new ModelMaterialSpec("M_Char_Patrol_Metal", "#161718", 0.6f, 0.6f),
            new ModelMaterialSpec("M_Char_Hunter_Camo", "#27301e", 0f, 0.1f, textured: true), // T_CharHunter_BaseColor (make_hunter_camo.py)
            new ModelMaterialSpec("M_Char_Hunter_Skin", "#b08368", 0f, 0.3f, textured: true), // T_CharHunterSkin: skin, eye white, iris and pupil swatches (make_hunter_skin.py)
            new ModelMaterialSpec("M_Char_Hunter_Gear", "#161718", 0.3f, 0.4f),
            // The Commander is the realistic hero (docs/art-pipeline.md section 18.2): three textured materials from make_commander_textures.py.
            new ModelMaterialSpec("M_Char_Commander_Uniform", "#e9e9e4", 0f, 0.2f, textured: true),
            new ModelMaterialSpec("M_Char_Commander_Skin", "#dfb396", 0f, 0.3f, textured: true),
            new ModelMaterialSpec("M_Char_Commander_Hair", "#8c683b", 0f, 0.25f, textured: true, alphaClip: true, doubleSided: true),
        };

        /// <summary>The texture name up to the map: M_Char_Commander_Skin looks for T_CharCommanderSkin_BaseColor.</summary>
        private static string PrefixOf(string materialName)
        {
            switch (materialName)
            {
                case "M_Char_Hunter_Camo": return HunterCamoTexturePrefix;
                case "M_Char_Hunter_Skin": return HunterSkinTexturePrefix;
                case "M_Char_Commander_Uniform": return CommanderUniformPrefix;
                case "M_Char_Commander_Skin": return CommanderSkinPrefix;
                case "M_Char_Commander_Hair": return CommanderHairPrefix;
                default: return TexturePrefix;
            }
        }

        [MenuItem("Blackout/Art/Build Character Materials")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Debug.Log($"[CharacterMaterials] Materials are up to date in {Folder}");
        }

        /// <summary>Every material by its name (the name of the Blender material it replaces).</summary>
        public static Dictionary<string, Material> BuildAll()
        {
            return BuildAll(Folder, TextureFolder);
        }

        /// <summary>Only the materials of one character ("Patrol", "Hunter" or "Commander"), so one model can be refreshed without touching the others.</summary>
        public static Dictionary<string, Material> BuildFor(string characterName)
        {
            var set = new Dictionary<string, Material>();
            string wanted = "M_Char_" + characterName + "_";
            foreach (ModelMaterialSpec spec in Specs)
            {
                if (spec.Name.StartsWith(wanted, System.StringComparison.Ordinal))
                {
                    set[spec.Name] = ModelMaterials.GetOrCreate(spec, Folder, TextureFolder, PrefixOf(spec.Name));
                }
            }

            AssetDatabase.SaveAssets();
            return set;
        }

        public static Dictionary<string, Material> BuildAll(string folder, string textureFolder)
        {
            var set = new Dictionary<string, Material>();
            foreach (ModelMaterialSpec spec in Specs)
            {
                set[spec.Name] = ModelMaterials.GetOrCreate(spec, folder, textureFolder, PrefixOf(spec.Name));
            }

            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
