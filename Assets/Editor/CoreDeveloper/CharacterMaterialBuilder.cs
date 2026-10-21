using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates the nine flat URP Lit materials of the three humanoid agents in Assets/Materials/Models (docs/art-pipeline.md section 18).
    /// The colours are the prototype's (docs/reference/core-developer.md 4.2 to 4.4); there are no textures, so silhouette and colour carry
    /// the look at the 10 to 30 m the characters are seen from (only the camouflage uniforms of the Hunter and the Patrol and the Patrol's swatch atlas have textures). Re-runnable and non-destructive like the robot's: an existing material
    /// keeps every hand edit.
    /// </summary>
    public static class CharacterMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";
        private const string TexturePrefix = "T_Char_";
        private const string PatrolClothTexturePrefix = "T_CharPatrol_";
        private const string PatrolKitTexturePrefix = "T_CharPatrolKit_";
        private const string SoldierTexturePrefix = "T_CharPatrolSoldier"; // + Body_, Face_, Legs_, Hands_ or Hair_
        private const string HunterCamoTexturePrefix = "T_CharHunter_";
        private const string HunterSkinTexturePrefix = "T_CharHunterSkin_";
        private const string CommanderUniformPrefix = "T_CharCommanderUniform_";
        private const string CommanderSkinPrefix = "T_CharCommanderSkin_";
        private const string CommanderHairPrefix = "T_CharCommanderHair_";

        // Smoothness = 1 - roughness of the Blender material (build_humans.py).
        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec("M_Char_Patrol_Cloth", "#84694f", 0f, 0.1f, textured: true), // T_CharPatrol_BaseColor and _Normal (make_patrol_cloth.py)
            new ModelMaterialSpec("M_Char_Patrol_Kit", "#4d4a30", 0f, 0.2f, textured: true),   // T_CharPatrolKit_BaseColor and _MetalSmooth: swatches, decals and the face (make_patrol_textures.py)
            // The Patrol is the downloaded skinned soldier (docs/art-pipeline.md section 18.4): photographic textures, one material per atlas.
            new ModelMaterialSpec("M_Char_PatrolSoldier_Body", "#5d5a42", 0f, 0.12f, textured: true),   // jacket, vest and pouches: BaseColor and Normal
            new ModelMaterialSpec("M_Char_PatrolSoldier_Face", "#a77d62", 0f, 0.30f, textured: true),   // the head and the eyes
            new ModelMaterialSpec("M_Char_PatrolSoldier_Legs", "#58563c", 0f, 0.12f, textured: true),   // trousers, boots and the cap
            new ModelMaterialSpec("M_Char_PatrolSoldier_Hands", "#a77d62", 0f, 0.30f, textured: true),
            new ModelMaterialSpec("M_Char_PatrolSoldier_Hair", "#3a2f26", 0f, 0.20f, textured: true),
            new ModelMaterialSpec("M_Char_Hunter_Camo", "#27301e", 0f, 0.1f, textured: true), // T_CharHunter_BaseColor (make_hunter_camo.py)
            new ModelMaterialSpec("M_Char_Hunter_Skin", "#b08368", 0f, 0.3f, textured: true), // T_CharHunterSkin: skin, eye white, iris and pupil swatches (make_hunter_skin.py)
            new ModelMaterialSpec("M_Char_Hunter_Gear", "#161718", 0.3f, 0.4f),
            // The Commander is the realistic hero (docs/art-pipeline.md section 18.2): three textured materials from make_commander_textures.py.
            new ModelMaterialSpec("M_Char_Commander_Uniform", "#e9e9e4", 0f, 0.2f, textured: true),
            new ModelMaterialSpec("M_Char_Commander_Skin", "#dfb396", 0f, 0.3f, textured: true),
            new ModelMaterialSpec("M_Char_Commander_Hair", "#8c683b", 0f, 0.25f, textured: true, alphaClip: true, doubleSided: true),
        };

        // Materials of an earlier look that no model uses any more. BuildAll deletes them through the AssetDatabase, so their GUIDs
        // and .meta files go with them and nothing is removed by hand.
        private static readonly string[] ObsoleteMaterials = { "M_Char_Patrol_Camo", "M_Char_Patrol_Metal" };

        /// <summary>The texture name up to the map: M_Char_Commander_Skin looks for T_CharCommanderSkin_BaseColor.</summary>
        private static string PrefixOf(string materialName)
        {
            switch (materialName)
            {
                case "M_Char_PatrolSoldier_Body": return SoldierTexturePrefix + "Body_";
                case "M_Char_PatrolSoldier_Face": return SoldierTexturePrefix + "Face_";
                case "M_Char_PatrolSoldier_Legs": return SoldierTexturePrefix + "Legs_";
                case "M_Char_PatrolSoldier_Hands": return SoldierTexturePrefix + "Hands_";
                case "M_Char_PatrolSoldier_Hair": return SoldierTexturePrefix + "Hair_";
                case "M_Char_Patrol_Cloth": return PatrolClothTexturePrefix;
                case "M_Char_Patrol_Kit": return PatrolKitTexturePrefix;
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
            foreach (string obsolete in ObsoleteMaterials)
            {
                string path = $"{folder}/{obsolete}.mat";
                if (AssetDatabase.LoadAssetAtPath<Material>(path) != null)
                {
                    AssetDatabase.DeleteAsset(path);
                }
            }

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
