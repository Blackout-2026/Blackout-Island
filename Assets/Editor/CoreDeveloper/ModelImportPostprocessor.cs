using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Applies ModelImportRules to everything in Assets/Models/Custom and prints the triangle count of each
    /// imported mesh (docs/art-pipeline.md section 9). Editor-only; nothing here runs in the game.
    /// </summary>
    public class ModelImportPostprocessor : AssetPostprocessor
    {
        // Raise this when a rule changes so Unity reimports the models with the new rules.
        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessModel()
        {
            if (ModelImportRules.IsBlendFileInModels(assetPath))
            {
                Debug.LogWarning($"[ModelImport] {assetPath}: keep .blend sources in Art/Blender, outside Assets.");
            }

            if (ModelImportRules.IsManaged(assetPath) && assetImporter is ModelImporter importer)
            {
                ModelImportRules.Apply(importer);
            }
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (!ModelImportRules.IsManaged(assetPath))
            {
                return;
            }

            ModelReport report = Measure(root);
            Debug.Log(report.Text);
            foreach (string warning in report.Warnings)
            {
                Debug.LogWarning(warning);
            }
        }

        private ModelReport Measure(GameObject root)
        {
            var meshes = new List<MeshEntry>();
            var materials = new HashSet<Material>();
            var bounds = new Bounds();
            bool hasBounds = false;

            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = MeshOf(renderer);
                if (mesh == null)
                {
                    continue;
                }

                meshes.Add(new MeshEntry(renderer.name, MeshStats.CountTriangles(mesh), MeshStats.HasUv0(mesh)));
                foreach (Material material in renderer.sharedMaterials)
                {
                    materials.Add(material);
                }

                Encapsulate(ref bounds, ref hasBounds, mesh.bounds, renderer.transform.localToWorldMatrix);
            }

            return ModelReport.Build(assetPath, meshes, materials.Count, bounds.size);
        }

        private static Mesh MeshOf(Renderer renderer)
        {
            if (renderer is SkinnedMeshRenderer skinned)
            {
                return skinned.sharedMesh;
            }

            return renderer.TryGetComponent(out MeshFilter filter) ? filter.sharedMesh : null;
        }

        // The model has not been placed in a scene, so transform its mesh bounds by hand instead of using Renderer.bounds.
        private static void Encapsulate(ref Bounds total, ref bool hasBounds, Bounds local, Matrix4x4 toWorld)
        {
            for (int i = 0; i < 8; i++)
            {
                Vector3 corner = local.center + Vector3.Scale(local.extents, new Vector3(
                    (i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                Vector3 point = toWorld.MultiplyPoint3x4(corner);
                if (hasBounds)
                {
                    total.Encapsulate(point);
                }
                else
                {
                    total = new Bounds(point, Vector3.zero);
                    hasBounds = true;
                }
            }
        }
    }
}
