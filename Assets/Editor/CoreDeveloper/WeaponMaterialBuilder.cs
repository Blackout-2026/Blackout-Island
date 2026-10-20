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

        /// <summary>
        /// The SMG's body: one material that reads the baked maps T_WeaponSmg_BaseColor, _Normal and _MetalSmooth in the
        /// texture folder (Art/Blender/Scripts/bake_smg.py makes them). Flat dark metal until those files exist, then
        /// Build Weapon Prefabs fills the empty slots. Kept out of <see cref="Specs"/> because its textures have their own prefix.
        /// </summary>
        public static readonly ModelMaterialSpec SmgBody = new ModelMaterialSpec(Prefix + "Smg", "#2e3135", 0.8f, 0.5f, textured: true);

        public const string SmgTexturePrefix = "T_WeaponSmg_";

        /// <summary>
        /// The SMG's camouflage sleeves: flat olive until T_WeaponSleeve_BaseColor exists (Art/Blender/Scripts/make_camo_texture.py).
        /// </summary>
        public static readonly ModelMaterialSpec SleeveCamo = new ModelMaterialSpec(Prefix + "Camo", "#6c735c", 0f, 0.08f, textured: true);

        public const string CamoTexturePrefix = "T_WeaponSleeve_";

        /// <summary>
        /// The knife's body (blackened blade, guard, rivets and G10 handle): one material that reads T_WeaponKnife_BaseColor, _Normal
        /// and _MetalSmooth (Art/Blender/Scripts/bake_knife.py makes them). Flat dark steel until those files exist.
        /// </summary>
        public static readonly ModelMaterialSpec KnifeBody = new ModelMaterialSpec(Prefix + "Knife", "#2a2d30", 0.8f, 0.45f, textured: true);

        public const string KnifeTexturePrefix = "T_WeaponKnife_";

        /// <summary>
        /// The rifle's body (blackened steel, black polymer and the tan stock in one material) from T_WeaponSniper_BaseColor, _Normal
        /// and _MetalSmooth (Art/Blender/Scripts/bake_sniper.py makes them). Flat dark metal until those files exist.
        /// </summary>
        public static readonly ModelMaterialSpec SniperBody = new ModelMaterialSpec(Prefix + "Sniper", "#6b604a", 0.3f, 0.45f, textured: true);

        public const string SniperTexturePrefix = "T_WeaponSniper_";

        /// <summary>
        /// The tactical gloves of every viewmodel (the hand and the wrist under the sleeve): coyote-tan woven fabric from
        /// T_WeaponGlove_BaseColor and _Normal (Art/Blender/Scripts/make_glove_texture.py), tiled over the hand. Flat tan until
        /// those files exist. Replaces the old plain black Glove and the bare-skin material, so a viewmodel keeps to 3 slots.
        /// </summary>
        public static readonly ModelMaterialSpec GloveHands = new ModelMaterialSpec(Prefix + "Glove", "#8f744e", 0f, 0.25f, textured: true);

        public const string GloveTexturePrefix = "T_WeaponGlove_";

        public static PartMaterials BuildAll(string folder, string textureFolder)
        {
            PartMaterials set = PartMaterials.Build(Prefix, Specs, folder, textureFolder);
            set.Add(ModelMaterials.GetOrCreate(SmgBody, folder, textureFolder, SmgTexturePrefix));
            set.Add(ModelMaterials.GetOrCreate(SleeveCamo, folder, textureFolder, CamoTexturePrefix));
            set.Add(ModelMaterials.GetOrCreate(KnifeBody, folder, textureFolder, KnifeTexturePrefix));
            set.Add(ModelMaterials.GetOrCreate(GloveHands, folder, textureFolder, GloveTexturePrefix));
            set.Add(ModelMaterials.GetOrCreate(SniperBody, folder, textureFolder, SniperTexturePrefix));
            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
