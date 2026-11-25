using System.Collections.Generic;
using UnityEditor;

namespace Blackout.Editor
{
    /// <summary>
    /// The Hunter's visual (step HV1): the downloaded Left 4 Dead Hunter (SM_Char_HunterL4D.fbx, a Valve-biped skeleton, one skinned mesh of 5,046 triangles, one 2048 texture) with
    /// the M_Char_HunterL4D material and a scoped rifle (SM_Char_HunterRifle.fbx) on its right hand, because the Hunter of this game shoots (the AgentShooter needs a muzzle).
    /// The building itself is <see cref="SkinnedHumanoidBuilder"/>; this holds what is particular to him.
    /// </summary>
    public static class HunterModelBuilder
    {
        public const string ModelFbxPath = "Assets/Models/Custom/SM_Char_HunterL4D.fbx";
        public const string RifleFbxPath = "Assets/Models/Custom/SM_Char_HunterRifle.fbx";
        public const string RifleMaterialName = "M_Char_Patrol_Kit"; // the rifle atlas is shared with the Patrol's rifle
        public const string MaterialName = "M_Char_HunterL4D";

        /// <summary>The FBX has one material, "Material #21" (3ds Max's default name).</summary>
        public static readonly KeyValuePair<string, string>[] SlotMaterials =
        {
            new KeyValuePair<string, string>("Material #21", MaterialName),
        };

        public static readonly SkinnedHumanoidSpec Spec = new SkinnedHumanoidSpec("Hunter", ModelFbxPath, RifleFbxPath, RifleMaterialName,
            new HumanoidRiflePose(new ValveBones(), RifleHold.LowReady), ModelImporterAnimationType.Human, 180f, SlotMaterials);
    }
}
