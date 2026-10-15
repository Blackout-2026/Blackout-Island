using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds LODs for the primitive robot placeholder. Copies stay below their original part,
    /// so the Agent Controller's joint animation also moves the reduced levels.
    /// </summary>
    public static class RobotLODBuilder
    {
        private const string Lod1Suffix = "__C5LOD1";
        private const string Lod2Suffix = "__C5LOD2";

        private static readonly HashSet<string> SilhouetteParts = new HashSet<string>(StringComparer.Ordinal)
        {
            "PelvisBlock", "ChestPlate", "Helmet", "EyeBar", "ForeArmBlock_L", "PodBlock_R",
            "BarrelLeft_R", "BarrelRight_R", "PowerPack", "Palm_L",
        };

        [MenuItem("Blackout/Art/Build Security Robot LODs")]
        public static void Build()
        {
            GameObject root = PrefabUtility.LoadPrefabContents(RobotPrefabBuilder.PrefabPath);
            try
            {
                Transform model = root.transform.Find(RobotPrefabBuilder.ModelName);
                if (model == null)
                {
                    throw new InvalidOperationException("Security robot visual has no Model child.");
                }

                bool hasFbx = AssetDatabase.LoadAssetAtPath<GameObject>(RobotPrefabBuilder.FbxPath) != null;
                Configure(root, model, hasFbx);
                PrefabUtility.SaveAsPrefabAsset(root, RobotPrefabBuilder.PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>Call after the model is replaced, so the LOD group never retains stale renderer references.</summary>
        public static void Configure(GameObject root, Transform model, bool hasFbx)
        {
            RemoveGenerated(model);
            LODGroup group = root.GetComponent<LODGroup>();
            if (hasFbx)
            {
                // An imported FBX needs exported LOD meshes. Do not pretend a duplicate is a reduction.
                if (group != null)
                {
                    UnityEngine.Object.DestroyImmediate(group);
                }

                Debug.LogWarning("[RobotLOD] Imported FBX has no C5 placeholder LODs; export separate LOD meshes for it.");
                return;
            }

            Renderer[] full = model.GetComponentsInChildren<Renderer>(true);
            var medium = new List<Renderer>();
            var far = new List<Renderer>();
            foreach (Renderer renderer in full)
            {
                if (KeepMedium(renderer.name))
                {
                    medium.Add(CopyPart(renderer, Lod1Suffix));
                }

                if (KeepFar(renderer.name))
                {
                    far.Add(CopyPart(renderer, Lod2Suffix));
                }
            }

            if (group == null)
            {
                group = root.AddComponent<LODGroup>();
            }

            group.fadeMode = LODFadeMode.None;
            group.animateCrossFading = false;
            group.SetLODs(new[]
            {
                new LOD(0.35f, full),
                new LOD(0.10f, medium.ToArray()),
                new LOD(0.01f, far.ToArray()),
            });
            group.RecalculateBounds();

            Debug.Log($"[RobotLOD] LOD0 {Triangles(full)} triangles/{full.Length} renderers, "
                + $"LOD1 {Triangles(medium)} triangles/{medium.Count} renderers, "
                + $"LOD2 {Triangles(far)} triangles/{far.Count} renderers.");
        }

        private static Renderer CopyPart(Renderer source, string suffix)
        {
            GameObject copy = UnityEngine.Object.Instantiate(source.gameObject, source.transform);
            for (int i = copy.transform.childCount - 1; i >= 0; i--)
            {
                Transform child = copy.transform.GetChild(i);
                if (child.name.EndsWith(Lod1Suffix, StringComparison.Ordinal)
                    || child.name.EndsWith(Lod2Suffix, StringComparison.Ordinal))
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }

            copy.name = source.name + suffix;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            return copy.GetComponent<Renderer>();
        }

        private static void RemoveGenerated(Transform model)
        {
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer == null || renderer.name.EndsWith(Lod1Suffix, StringComparison.Ordinal)
                    || renderer.name.EndsWith(Lod2Suffix, StringComparison.Ordinal))
                {
                    continue;
                }

                Transform parent = renderer.transform;
                Transform medium = parent.Find(parent.name + Lod1Suffix);
                Transform far = parent.Find(parent.name + Lod2Suffix);
                if (medium != null)
                {
                    UnityEngine.Object.DestroyImmediate(medium.gameObject);
                }

                if (far != null)
                {
                    UnityEngine.Object.DestroyImmediate(far.gameObject);
                }
            }
        }

        private static bool KeepMedium(string name)
        {
            return !name.StartsWith("VentSlat", StringComparison.Ordinal)
                && !name.StartsWith("PowerCell", StringComparison.Ordinal)
                && !name.StartsWith("ArmPiston", StringComparison.Ordinal)
                && !name.StartsWith("Toe", StringComparison.Ordinal)
                && !name.StartsWith("Claw", StringComparison.Ordinal)
                && !name.StartsWith("KneeRod", StringComparison.Ordinal)
                && !name.StartsWith("ThighStripe", StringComparison.Ordinal)
                && !name.StartsWith("ShoulderStripe", StringComparison.Ordinal)
                && name != "AntennaTip" && name != "ScannerDot" && name != "PodStrip_R";
        }

        private static bool KeepFar(string name)
        {
            return SilhouetteParts.Contains(name)
                || name.StartsWith("ThighArmour", StringComparison.Ordinal)
                || name.StartsWith("ShinCore", StringComparison.Ordinal)
                || name.StartsWith("FootPlate", StringComparison.Ordinal)
                || name.StartsWith("ShoulderPlate", StringComparison.Ordinal)
                || name.StartsWith("ArmLimb", StringComparison.Ordinal);
        }

        private static int Triangles(IEnumerable<Renderer> renderers)
        {
            int triangles = 0;
            foreach (Renderer renderer in renderers)
            {
                MeshFilter filter = renderer.GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    triangles += MeshStats.CountTriangles(filter.sharedMesh);
                }
            }

            return triangles;
        }
    }
}
