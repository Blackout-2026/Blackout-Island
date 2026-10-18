using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>The prefabs the terrain is painted with: four tree prototypes and two detail prototypes.</summary>
    public enum VegetationPrototype
    {
        Pine,
        Palm,
        Rock, // boulders and rocks larger than VegetationLayout.RockColliderMinScale: with a collider
        RockSmall, // the same rock without a collider, as in the prototype
        Bush, // detail mesh: no collider, no shadow (the prototype's bushes are soft)
        Grass, // detail mesh
    }

    /// <summary>
    /// Where each vegetation prefab lives. Your own prefab wins: put it at Assets/Environment/Vegetation/&lt;Name&gt;.prefab
    /// (for example Pine.prefab, a prefab variant of a free low-poly pine with its LOD group and a CapsuleCollider)
    /// and re-run Paint Terrain And Forest. Until then the generated placeholder
    /// Assets/Environment/Vegetation/Placeholder/&lt;Name&gt;_Placeholder.prefab is used.
    /// </summary>
    public static class VegetationLibrary
    {
        public const string Folder = "Assets/Environment/Vegetation";
        public const string PlaceholderFolder = Folder + "/Placeholder";

        /// <summary>The player camera's vertical field of view (reference section 4), used for LOD distances.</summary>
        public const float CameraFov = 72f;

        // W11: keep nearby palm LOD0, but use its 96-triangle LOD2 sooner than the old 0.10 threshold.
        public const float PalmLod1Height = 0.15f;

        public static string PrefabPath(VegetationPrototype prototype)
        {
            return $"{Folder}/{prototype}.prefab";
        }

        public static string PlaceholderPath(VegetationPrototype prototype)
        {
            return $"{PlaceholderFolder}/{prototype}_Placeholder.prefab";
        }

        /// <summary>Your prefab if it exists, otherwise the placeholder (null if neither exists).</summary>
        public static GameObject Resolve(VegetationPrototype prototype, out bool placeholder)
        {
            var own = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(prototype));
            placeholder = own == null;
            return own != null ? own : AssetDatabase.LoadAssetAtPath<GameObject>(PlaceholderPath(prototype));
        }

        /// <summary>
        /// Distance at which an object of this size fills <paramref name="screenRelativeHeight"/> of the screen's
        /// height: where a LOD group switches LOD. Unity's rule: relative height = size / (2 x distance x tan(fov / 2)),
        /// times the quality setting's LOD bias.
        /// </summary>
        public static float LodDistance(float size, float screenRelativeHeight, float lodBias = 1f)
        {
            return size * lodBias / (2f * screenRelativeHeight * Mathf.Tan(CameraFov * 0.5f * Mathf.Deg2Rad));
        }
    }
}
