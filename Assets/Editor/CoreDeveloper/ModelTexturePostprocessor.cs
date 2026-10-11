using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Applies ModelTextureRules to everything in Assets/Textures/Models, so a normal map is always a normal map
    /// and MetalSmooth is always linear (docs/art-pipeline.md section 7). Editor-only.
    /// </summary>
    public class ModelTexturePostprocessor : AssetPostprocessor
    {
        // Raise this when a rule changes so Unity reimports the textures with the new rules.
        public override uint GetVersion()
        {
            return 1;
        }

        private void OnPreprocessTexture()
        {
            if (!ModelTextureRules.IsManaged(assetPath) || assetImporter is not TextureImporter importer)
            {
                return;
            }

            ModelTextureMap map = ModelTextureRules.MapOf(assetPath);
            if (map == ModelTextureMap.Unknown)
            {
                Debug.LogWarning($"[TextureImport] {assetPath}: name must end in _BaseColor, _Normal, _MetalSmooth or _Emission, so no rules were applied.");
                return;
            }

            ModelTextureRules.Apply(importer, map);
        }
    }
}
