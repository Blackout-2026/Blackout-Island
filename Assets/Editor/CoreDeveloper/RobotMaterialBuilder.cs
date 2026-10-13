using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>The materials the robot prefab needs, found by name.</summary>
    public sealed class RobotMaterialSet
    {
        public Material Body;
        public Material Joint;
        public Material Accent;
        public Material Eye;
        public Material Cells;

        /// <summary>The material for a Blender material name (M_SecurityRobot_Eye), or null for an unknown name.</summary>
        public Material Find(string materialName)
        {
            switch (materialName)
            {
                case RobotMaterialBuilder.BodyName: return Body;
                case RobotMaterialBuilder.JointName: return Joint;
                case RobotMaterialBuilder.AccentName: return Accent;
                case RobotMaterialBuilder.EyeName: return Eye;
                case RobotMaterialBuilder.CellsName: return Cells;
                default: return null;
            }
        }
    }

    /// <summary>
    /// Creates the URP Lit materials of the security robot in Assets/Materials/Models (docs/art-pipeline.md section 13).
    /// Re-runnable and non-destructive, like the generator's: a missing material gets flat placeholder colours, an
    /// existing one keeps every hand edit and only receives a texture into a slot that is still empty.
    /// Run it again after adding T_SecurityRobot_* to Assets/Textures/Models.
    /// </summary>
    public static class RobotMaterialBuilder
    {
        public const string Folder = "Assets/Materials/Models";
        public const string TextureFolder = "Assets/Textures/Models";

        public const string BodyName = "M_SecurityRobot_Body";
        public const string JointName = "M_SecurityRobot_Joint";
        public const string AccentName = "M_SecurityRobot_Accent";
        public const string EyeName = "M_SecurityRobot_Eye";
        public const string CellsName = "M_SecurityRobot_Cells";

        private const string TexturePrefix = "T_SecurityRobot_";

        // Colours come from docs/reference/core-developer.md 3.4 and 8.1 (smoothness = 1 - roughness). Body is the middle of
        // the robotPaint range #3a3f44 to #4f565c. Body, Joint and Accent will share the one 2048 texture set, so they are
        // marked textured. The two emissive materials stay flat: their glow is an HDR colour above 1 (it feeds the bloom).
        public static readonly ModelMaterialSpec[] Specs =
        {
            new ModelMaterialSpec(BodyName, "#4a5056", 0.55f, 0.4f, textured: true),
            new ModelMaterialSpec(JointName, "#232527", 0.85f, 0.65f, textured: true),
            new ModelMaterialSpec(AccentName, "#c89a24", 0.4f, 0.55f, textured: true),
            new ModelMaterialSpec(EyeName, "#1a0504", 0f, 0.3f, emission: new Color(5f, 0.35f, 0.15f)),
            new ModelMaterialSpec(CellsName, "#05181c", 0f, 0.3f, emission: new Color(0.4f, 2.6f, 3f)),
        };

        [MenuItem("Blackout/Art/Build Robot Materials")]
        public static void BuildFromMenu()
        {
            BuildAll();
            Debug.Log($"[RobotMaterials] Materials are up to date in {Folder}");
        }

        public static RobotMaterialSet BuildAll()
        {
            return BuildAll(Folder, TextureFolder);
        }

        public static RobotMaterialSet BuildAll(string folder, string textureFolder)
        {
            var set = new RobotMaterialSet();
            foreach (ModelMaterialSpec spec in Specs)
            {
                Material material = ModelMaterials.GetOrCreate(spec, folder, textureFolder, TexturePrefix);
                switch (spec.Name)
                {
                    case BodyName: set.Body = material; break;
                    case JointName: set.Joint = material; break;
                    case AccentName: set.Accent = material; break;
                    case EyeName: set.Eye = material; break;
                    case CellsName: set.Cells = material; break;
                }
            }

            AssetDatabase.SaveAssets();
            return set;
        }
    }
}
