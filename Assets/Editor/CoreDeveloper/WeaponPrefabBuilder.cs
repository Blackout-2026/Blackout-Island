using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds the three viewmodel prefabs Weapon_Smg_Visual, Weapon_Sniper_Visual and Weapon_Knife_Visual in
    /// Assets/Prefabs/Models (docs/art-pipeline.md section 14). Visual only: no collider, no script. The Systems Engineer's
    /// weapon prefabs nest them, move the root to the held position and find the sockets by name: <c>MuzzlePoint</c> (its
    /// +Z is the shooting direction) with a hidden <c>MuzzleFlash</c> child, the sniper's <c>BoltHandle</c> pivot and the
    /// knife's <c>Hand</c> pivot. Uses SM_Weapon_*.fbx when it exists, otherwise a primitive placeholder.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.WeaponPrefabBuilder.BuildAll -logFile -
    /// </summary>
    public static class WeaponPrefabBuilder
    {
        public const string MuzzleFlash = "MuzzleFlash";

        [MenuItem("Blackout/Art/Build Weapon Prefabs")]
        public static void BuildAll()
        {
            Run(false);
        }

        [MenuItem("Blackout/Art/Rebuild Weapon Placeholders")]
        public static void RebuildPlaceholders()
        {
            Run(true);
        }

        private static void Run(bool rebuildPlaceholders)
        {
            PartMaterials materials = WeaponMaterialBuilder.BuildAll();
            int built = 0;
            foreach (PropEntry entry in PropCatalog.All)
            {
                ModelDefinition definition = entry.Group == PropGroup.Weapon ? WeaponDefinitions.For(entry.Name) : null;
                if (definition == null)
                {
                    continue;
                }

                ModelPrefabBuilder.Build(entry, definition, materials, false, root => EnsureMuzzleFlash(root, materials), rebuildPlaceholders);
                built++;

                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath);
                Debug.Log($"[WeaponPrefab] {entry.PrefabPath}: {PropStats.Triangles(prefab)} triangles (budget {entry.Budget.TargetTriangles})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[WeaponPrefab] {built} prefabs are up to date");
        }

        /// <summary>
        /// Adds the hidden flash under the muzzle: three crossed quads of 0.22 m (the third flat and 0.7 times as big),
        /// double-sided, as in the prototype (core-developer.md 5.4). A model without a muzzle (the knife) gets none.
        /// </summary>
        public static void EnsureMuzzleFlash(GameObject root, PartMaterials materials)
        {
            Transform muzzle = RobotRig.FindDeep(root.transform, WeaponDefinitions.MuzzlePoint);
            if (muzzle == null || muzzle.Find(MuzzleFlash) != null)
            {
                return;
            }

            var flash = new GameObject(MuzzleFlash);
            flash.transform.SetParent(muzzle, false);
            AddQuad(flash.transform, "FlashA", Quaternion.identity, 0.22f, materials);
            AddQuad(flash.transform, "FlashB", Quaternion.Euler(0f, 90f, 0f), 0.22f, materials);
            AddQuad(flash.transform, "FlashC", Quaternion.Euler(90f, 0f, 0f), 0.154f, materials);
            flash.SetActive(false); // the weapon system shows it for a few frames after a shot
        }

        private static void AddQuad(Transform parent, string name, Quaternion rotation, float size, PartMaterials materials)
        {
            GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = name;
            quad.transform.SetParent(parent, false);
            quad.transform.localRotation = rotation;
            quad.transform.localScale = new Vector3(size, size, 1f);
            quad.GetComponent<Renderer>().sharedMaterial = materials.Get("Flash");
        }
    }
}
