using UnityEditor;

namespace Blackout.Editor
{
    /// <summary>
    /// Runs every step C4 generator in order: prop and weapon materials, prop and pickup prefabs, sign decals, weapon
    /// prefabs. Safe to run again at any time; it only creates what is missing.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.PropKitBuilder.BuildAll -logFile -
    /// </summary>
    public static class PropKitBuilder
    {
        [MenuItem("Blackout/Art/Build All Props, Signs and Weapons")]
        public static void BuildAll()
        {
            PropPrefabBuilder.BuildAll();
            DecalBuilder.BuildAll();
            WeaponPrefabBuilder.BuildAll();
            AssetDatabase.SaveAssets();
        }
    }
}
