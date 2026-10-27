using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>Only the provided Winter Soldier import and its two managed materials are configured.</summary>
    public static class PlayerCinematicBodyBuilder
    {
        public const string ModelPath = "Assets/Models/Custom/SM_Player_WinterSoldier.fbx";
        public const string BodyMaterialPath = "Assets/Materials/Models/M_PlayerWinter_Body.mat";
        public const string HiddenMaterialPath = "Assets/Materials/Models/M_PlayerWinter_HiddenAttachments.mat";
        private const string TexturePath = "Assets/Textures/Models/T_PlayerWinter_Body.png";

        public static void Build()
        {
            const string source = @"D:\model\winter-soldierfreetexturedrigged";
            CopyNew(Path.Combine(source, "source", "00000036.fbx"), ModelPath);
            CopyNew(Path.Combine(source, "textures", "00000037.png"), TexturePath);
            CopyNew(Path.Combine(source, "textures", "00000039.png"), "Assets/Textures/Models/T_PlayerWinter_Weapon.png");
            var importer = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            importer.globalScale = 1f; // Preserve the source rig's unit conversion; scale the visual prefab uniformly.
            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = false;
            importer.importCameras = importer.importLights = importer.addCollider = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            var rig = new HumanDescription { skeleton = new SkeletonBone[0], upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f, armStretch = 0.05f, legStretch = 0.05f };
            var bones = new System.Collections.Generic.List<HumanBone>();
            Add(bones, "bip001 pelvis", HumanBodyBones.Hips);
            Add(bones, "bip001 spine", HumanBodyBones.Spine);
            Add(bones, "bip001 spine1", HumanBodyBones.Chest);
            Add(bones, "bip001 spine2", HumanBodyBones.UpperChest);
            Add(bones, "bip001 neck", HumanBodyBones.Neck);
            Add(bones, "bip001 head", HumanBodyBones.Head);
            foreach (bool left in new[] { true, false })
            {
                string prefix = "bip001 " + (left ? "l " : "r ");
                string side = left ? "Left" : "Right";
                foreach (var pair in new[] { ("thigh", "UpperLeg"), ("calf", "LowerLeg"), ("foot", "Foot"),
                    ("toe0", "Toes"), ("clavicle", "Shoulder"), ("upperarm", "UpperArm"), ("forearm", "LowerArm"), ("hand", "Hand") })
                    Add(bones, prefix + pair.Item1, (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + pair.Item2));
                string[] fingers = { "Thumb", "Index", "Middle", "Ring", "Little" };
                string[] segments = { "Proximal", "Intermediate", "Distal" };
                for (int finger = 0; finger < 5; finger++)
                    for (int segment = 0; segment < 3; segment++)
                        Add(bones, prefix + "finger" + finger + (segment == 0 ? "" : segment.ToString()),
                            (HumanBodyBones)Enum.Parse(typeof(HumanBodyBones), side + fingers[finger] + segments[segment]));
            }
            rig.human = bones.ToArray();
            importer.humanDescription = rig;
            importer.globalScale = 1f;
            importer.SaveAndReimport();
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(ModelPath).OfType<Avatar>().FirstOrDefault();
            if (avatar == null || !avatar.isHuman || !avatar.isValid) throw new InvalidOperationException("Winter Soldier needs a valid Humanoid avatar.");
            Material body = Material(BodyMaterialPath);
            body.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath));
            body.SetColor("_BaseColor", Color.white);
            body.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(body);
            Material hidden = Material(HiddenMaterialPath);
            hidden.shader = Shader.Find("Universal Render Pipeline/Unlit");
            hidden.SetFloat("_Surface", 1f);
            hidden.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            hidden.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            hidden.SetFloat("_ZWrite", 0f);
            hidden.SetColor("_BaseColor", Color.clear);
            hidden.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            hidden.SetShaderPassEnabled("ShadowCaster", false);
            hidden.renderQueue = 3000;
            EditorUtility.SetDirty(hidden);
            AssetDatabase.SaveAssets();
            Debug.Log("[TD1] Winter Soldier imported with explicit Biped Humanoid mapping and URP materials.");
        }

        private static void Add(System.Collections.Generic.List<HumanBone> bones, string name, HumanBodyBones human) =>
            bones.Add(new HumanBone { boneName = name, humanName = HumanTrait.BoneName[(int)human], limit = new HumanLimit { useDefaultValues = true } });

        private static Material Material(string path)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void CopyNew(string source, string destination)
        {
            if (File.Exists(destination)) return;
            if (!File.Exists(source)) throw new FileNotFoundException("Missing user-supplied character asset.", source);
            File.Copy(source, destination);
            AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceSynchronousImport);
        }
    }
}
