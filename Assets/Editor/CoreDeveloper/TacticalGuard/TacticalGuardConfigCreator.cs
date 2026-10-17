using Blackout.AI.TacticalGuard;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Creates Assets/Scripts/AI/TacticalGuard/Config/TacticalGuardConfig.asset with the default weights and curves.
    /// Re-runnable: if the asset already exists it is left alone, so numbers tuned in the Inspector are never overwritten.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.TacticalGuardConfigCreator.CreateDefault -logFile -
    /// </summary>
    public static class TacticalGuardConfigCreator
    {
        public const string FolderPath = "Assets/Scripts/AI/TacticalGuard/Config";
        public const string AssetPath = FolderPath + "/TacticalGuardConfig.asset";

        [MenuItem("Blackout/AI/Create Tactical Guard Config")]
        public static void CreateDefault()
        {
            if (AssetDatabase.LoadAssetAtPath<TacticalGuardConfig>(AssetPath) != null)
            {
                Debug.Log($"[TacticalGuard] {AssetPath} already exists, left unchanged.");
                return;
            }

            if (!AssetDatabase.IsValidFolder(FolderPath))
            {
                AssetDatabase.CreateFolder("Assets/Scripts/AI/TacticalGuard", "Config");
            }

            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<TacticalGuardConfig>(), AssetPath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TacticalGuard] Created {AssetPath} with the default weights and curves.");
        }
    }
}
