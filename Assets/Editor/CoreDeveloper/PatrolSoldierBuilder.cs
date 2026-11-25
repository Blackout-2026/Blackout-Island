using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// The Security Patrol's visual: the downloaded Mixamo soldier (SM_Char_PatrolSoldier.fbx, step PS4) with the five M_Char_PatrolSoldier_* materials and the SCAR-style rifle
    /// (SM_Char_PatrolRifle.fbx) on its right hand. The building itself is <see cref="SkinnedHumanoidBuilder"/>; this holds what is particular to him.
    /// </summary>
    public static class PatrolSoldierBuilder
    {
        public const string SoldierFbxPath = "Assets/Models/Custom/SM_Char_PatrolSoldier.fbx";
        public const string RifleFbxPath = "Assets/Models/Custom/SM_Char_PatrolRifle.fbx";
        public const string RifleMaterialName = "M_Char_Patrol_Kit";
        public const string ModelName = SkinnedHumanoidBuilder.ModelName;
        public const string GunName = SkinnedHumanoidBuilder.GunName;

        /// <summary>The FBX names its materials lambert1 to lambert7 (Mixamo's export); the five assets share them by what they cover.</summary>
        public static readonly KeyValuePair<string, string>[] SlotMaterials =
        {
            new KeyValuePair<string, string>("lambert1", "M_Char_PatrolSoldier_Body"),    // jacket, vest and pouches
            new KeyValuePair<string, string>("lambert2", "M_Char_PatrolSoldier_Face"),    // the head
            new KeyValuePair<string, string>("lambert3", "M_Char_PatrolSoldier_Face"),    // the eyes are on the same face texture
            new KeyValuePair<string, string>("lambert4", "M_Char_PatrolSoldier_Legs"),    // the cap is on the trousers' atlas
            new KeyValuePair<string, string>("lambert5", "M_Char_PatrolSoldier_Legs"),    // trousers and boots
            new KeyValuePair<string, string>("lambert6", "M_Char_PatrolSoldier_Hands"),
            new KeyValuePair<string, string>("lambert7", "M_Char_PatrolSoldier_Hair"),
        };

        public static readonly SkinnedHumanoidSpec Spec = new SkinnedHumanoidSpec("Patrol", SoldierFbxPath, RifleFbxPath, RifleMaterialName, PatrolSoldierPose.Instance,
            ModelImporterAnimationType.Human, 180f, SlotMaterials);

        public static string MaterialFor(string slotName) => Spec.MaterialFor(slotName);

        public static void EnsureImportSettings() => SkinnedHumanoidBuilder.EnsureImportSettings(Spec);

        public static void Build(CharacterEntry entry, Dictionary<string, Material> materials) => SkinnedHumanoidBuilder.Build(entry, Spec, materials);

        public static Transform Populate(GameObject root, GameObject soldierFbx, GameObject rifleFbx, Dictionary<string, Material> materials) =>
            SkinnedHumanoidBuilder.Populate(root, Spec, soldierFbx, rifleFbx, materials);
    }
}
