using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Prefabs/Models/SecurityRobot_Visual.prefab (docs/art-pipeline.md section 13): the robot's look and
    /// its two sockets only, with no collider, Health or script. The Agent Controller nests it in their agent prefab and
    /// animates the joints by name (section 11). Uses SM_SecurityRobot.fbx when it exists; until then it builds a
    /// primitive placeholder with the same joint tree and size. Re-runnable: only the "Model" child is replaced, and
    /// while the same model stays, sockets moved by hand are kept.
    /// Batch mode: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;"
    /// -executeMethod Blackout.Editor.RobotPrefabBuilder.Build -logFile -
    /// </summary>
    public static class RobotPrefabBuilder
    {
        public const string PrefabPath = "Assets/Prefabs/Models/SecurityRobot_Visual.prefab";
        public const string FbxPath = "Assets/Models/Custom/SM_SecurityRobot.fbx";
        public const string ModelName = "Model";

        [MenuItem("Blackout/Art/Build Security Robot Visual Prefab")]
        public static void Build()
        {
            RobotMaterialSet materials = RobotMaterialBuilder.BuildAll();

            var fbx = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (fbx == null)
            {
                Debug.LogWarning($"[RobotPrefab] {FbxPath} not found: building the primitive placeholder model.");
            }

            ModelMaterials.EnsureFolder("Assets/Prefabs/Models");
            bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) != null;
            GameObject root = exists ? PrefabUtility.LoadPrefabContents(PrefabPath) : new GameObject("SecurityRobot_Visual");
            try
            {
                Populate(root, fbx, materials);
                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                if (exists)
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
                else
                {
                    Object.DestroyImmediate(root);
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[RobotPrefab] {(exists ? "Updated" : "Created")} {PrefabPath} ({(fbx != null ? "from SM_SecurityRobot.fbx" : "placeholder model")})");
        }

        /// <summary>Fills a root object with the model and the two sockets on the right hand. Safe to call repeatedly.</summary>
        public static void Populate(GameObject root, GameObject fbx, RobotMaterialSet materials)
        {
            Transform model = ReplaceModel(root.transform, fbx, materials);
            ReportMissingJoints(model);

            Transform hand = FindSocketParent(model);
            EnsureSocket(model, hand, RobotRig.WeaponSocket, Vector3.zero);
            EnsureSocket(model, hand, RobotRig.MuzzlePoint, RobotRig.MuzzleOffset);
        }

        /// <summary>The names from <see cref="RobotRig.RequiredJoints"/> that the model does not contain.</summary>
        public static List<string> MissingJoints(Transform model)
        {
            var missing = new List<string>();
            foreach (string jointName in RobotRig.RequiredJoints())
            {
                if (RobotRig.FindDeep(model, jointName) == null)
                {
                    missing.Add(jointName);
                }
            }

            return missing;
        }

        /// <summary>
        /// Replaces every imported material slot with the M_SecurityRobot_* asset of the same name. The FBX keeps its
        /// materials inside (docs/art-pipeline.md section 9), so a Blender name that does not match stays as imported.
        /// </summary>
        public static void AssignMaterials(GameObject instance, RobotMaterialSet materials)
        {
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                Material[] slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                {
                    string slotName = slots[i] != null ? slots[i].name : string.Empty;
                    Material replacement = materials.Find(slotName);
                    if (replacement != null)
                    {
                        slots[i] = replacement;
                    }
                    else
                    {
                        Debug.LogWarning($"[RobotPrefab] {renderer.name} slot {i} uses '{slotName}', which matches no M_SecurityRobot_* material.", renderer);
                    }
                }

                renderer.sharedMaterials = slots;
            }
        }

        private static Transform ReplaceModel(Transform root, GameObject fbx, RobotMaterialSet materials)
        {
            Transform old = root.Find(ModelName);
            if (old != null && IsCurrentModel(old.gameObject, fbx))
            {
                // Nothing to replace: keep it, so a re-run leaves the prefab file unchanged.
                if (fbx != null)
                {
                    AssignMaterials(old.gameObject, materials);
                }

                return old;
            }

            if (old != null)
            {
                Object.DestroyImmediate(old.gameObject);
            }

            if (fbx != null)
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(fbx, root);
                instance.name = ModelName;
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.identity;
                AssignMaterials(instance, materials);
                return instance.transform;
            }

            var placeholder = new GameObject(ModelName).transform;
            placeholder.SetParent(root, false);
            RobotPlaceholder.Build(placeholder, materials);
            return placeholder;
        }

        // True when the existing Model child is already what this run would build: the placeholder while there is
        // no FBX, or an instance of this very FBX once there is one (a placeholder is replaced by the FBX).
        private static bool IsCurrentModel(GameObject model, GameObject fbx)
        {
            if (fbx == null)
            {
                return !PrefabUtility.IsPartOfPrefabInstance(model);
            }

            return PrefabUtility.GetCorrespondingObjectFromSource(model) == fbx;
        }

        private static void ReportMissingJoints(Transform model)
        {
            List<string> missing = MissingJoints(model);
            if (missing.Count > 0)
            {
                Debug.LogWarning($"[RobotPrefab] The model has no object named: {string.Join(", ", missing)} (docs/art-pipeline.md section 11). The Agent Controller's animation skips a joint it cannot find.");
            }
        }

        // The sockets belong to the right hand so they follow the raised arm. A model without Hand_R falls back to the
        // forearm, then to the model itself.
        private static Transform FindSocketParent(Transform model)
        {
            return RobotRig.FindDeep(model, RobotRig.HandR) ?? RobotRig.FindDeep(model, RobotRig.ForeArmR) ?? model;
        }

        // Returns the socket if the model already has one (a position tuned by hand, or an Empty exported from Blender)
        // and otherwise creates it under the hand, with its +Z along the barrels.
        private static Transform EnsureSocket(Transform model, Transform hand, string socketName, Vector3 localPosition)
        {
            Transform socket = RobotRig.FindDeep(model, socketName);
            if (socket == null)
            {
                socket = new GameObject(socketName).transform;
                socket.SetParent(hand, false);
                socket.localPosition = localPosition;
                socket.localRotation = RobotRig.SocketRotation;
            }

            return socket;
        }
    }
}
