using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds every modelled prop in <see cref="PropCatalog"/> as a prefab: Assets/Prefabs/Props/Prop_&lt;Name&gt;.prefab with
    /// colliders on the root, plus the two visual-only pickups CureCase_Visual and Medkit_Visual in Assets/Prefabs/Models
    /// (docs/art-pipeline.md section 14). Uses SM_Prop_&lt;Name&gt;.fbx when it exists, otherwise a primitive placeholder with
    /// the real size and pivot, so the World Builder can place props before the Blender models are done.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.PropPrefabBuilder.BuildAll -logFile -
    /// </summary>
    public static class PropPrefabBuilder
    {
        [MenuItem("Blackout/Art/Build Prop Prefabs")]
        public static void BuildAll()
        {
            Run(false);
        }

        /// <summary>Use after changing a definition in <see cref="PropDefinitions"/>: replaces the placeholder Model of every prefab.</summary>
        [MenuItem("Blackout/Art/Rebuild Prop Placeholders")]
        public static void RebuildPlaceholders()
        {
            Run(true);
        }

        private static void Run(bool rebuildPlaceholders)
        {
            PartMaterials materials = PropMaterialBuilder.BuildAll();
            int built = 0;
            foreach (PropEntry entry in PropCatalog.All)
            {
                ModelDefinition definition = entry.Source == PropSource.Blender ? PropDefinitions.For(entry.Name) : null;
                if (definition == null)
                {
                    continue; // heroes and weapons have their own builders, kit slots are filled by hand
                }

                bool isProp = entry.Folder == PropCatalog.PropsFolder;
                ModelPrefabBuilder.Build(entry, definition, materials, isProp, null, rebuildPlaceholders);
                built++;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                Debug.Log($"[PropPrefab] {entry.PrefabPath}: {PropStats.Triangles(prefab)} triangles, {PropStats.Colliders(prefab)} colliders");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[PropPrefab] {built} prefabs are up to date");
        }
    }
}
