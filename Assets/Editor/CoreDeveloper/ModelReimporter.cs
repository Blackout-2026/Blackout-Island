using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Re-runs the import rules on every model in Assets/Models/Custom, which also prints each model's
    /// triangle report again. Re-runnable and read-only on everything except the importer settings:
    /// no asset is created, moved or deleted.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.ModelReimporter.ReimportAll -logFile -
    /// </summary>
    public static class ModelReimporter
    {
        public static string[] FindModelPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:Model", new[] { ModelImportRules.Folder.TrimEnd('/') });
            var paths = new string[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                paths[i] = AssetDatabase.GUIDToAssetPath(guids[i]);
            }

            return paths;
        }

        [MenuItem("Blackout/Art/Reimport Custom Models")]
        public static void ReimportAll()
        {
            string[] paths = FindModelPaths();
            foreach (string path in paths)
            {
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            }

            Debug.Log($"[ModelImport] Reimported {paths.Length} model(s) from {ModelImportRules.Folder}");
        }
    }
}
