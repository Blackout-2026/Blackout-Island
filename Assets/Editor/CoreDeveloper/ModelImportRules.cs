using System;
using UnityEditor;

namespace Blackout.Editor
{
    /// <summary>
    /// The import settings every custom model gets (docs/art-pipeline.md section 9).
    /// Pure rules: the postprocessor decides when to call them.
    /// </summary>
    public static class ModelImportRules
    {
        public const string Folder = "Assets/Models/Custom/";
        private const string ModelsRoot = "Assets/Models/";

        /// <summary>True for any model file inside Assets/Models/Custom.</summary>
        public static bool IsManaged(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                   && assetPath.Replace('\\', '/').StartsWith(Folder, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>A .blend inside Assets would need Blender on every machine; sources live in Art/Blender.</summary>
        public static bool IsBlendFileInModels(string assetPath)
        {
            return !string.IsNullOrEmpty(assetPath)
                   && assetPath.Replace('\\', '/').StartsWith(ModelsRoot, StringComparison.OrdinalIgnoreCase)
                   && assetPath.EndsWith(".blend", StringComparison.OrdinalIgnoreCase);
        }

        public static void Apply(ModelImporter importer)
        {
            // Units and axes: the FBX is exported in metres with -Z forward / Y up (section 4).
            importer.globalScale = 1f;
            importer.useFileScale = true;
            importer.bakeAxisConversion = true;

            // Models never bring scene objects or clips with them; parts are animated by script.
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.importAnimation = false;

            // Shading as modelled in Blender, tangents that match the Blender normal map bake.
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk;

            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.isReadable = false;
            importer.optimizeMeshPolygons = true;
            importer.optimizeMeshVertices = true;
            importer.generateSecondaryUV = false;

            // Materials stay inside the FBX, named after the Blender materials, so no files appear next to it
            // (the External mode writes a Materials folder). The prefab step assigns the real M_ assets by name.
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
        }
    }
}
