using System.Collections.Generic;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// The materials one family of models (props, weapons) is built from, found by the short key a part names:
    /// the key "MetalDark" is the asset "M_Prop_MetalDark". Made by <see cref="PropMaterialBuilder"/> and
    /// <see cref="WeaponMaterialBuilder"/>.
    /// </summary>
    public sealed class PartMaterials
    {
        private readonly string prefix;
        private readonly Dictionary<string, Material> byKey = new Dictionary<string, Material>();

        public PartMaterials(string prefix)
        {
            this.prefix = prefix;
        }

        public string Prefix => prefix;

        public IEnumerable<Material> All => byKey.Values;

        public void Add(Material material)
        {
            byKey[material.name.Substring(prefix.Length)] = material;
        }

        /// <summary>The material for a key such as "Steel", or null when there is none.</summary>
        public Material Get(string key)
        {
            return byKey.TryGetValue(key, out Material material) ? material : null;
        }

        /// <summary>The material for a Blender material name such as "M_Prop_Steel", or null for any other name.</summary>
        public Material Find(string materialName)
        {
            if (string.IsNullOrEmpty(materialName) || !materialName.StartsWith(prefix, System.StringComparison.Ordinal))
            {
                return null;
            }

            return Get(materialName.Substring(prefix.Length));
        }

        /// <summary>Creates every material in the specs (see <see cref="ModelMaterials.GetOrCreate"/>: hand edits are kept).</summary>
        public static PartMaterials Build(string prefix, ModelMaterialSpec[] specs, string folder, string textureFolder)
        {
            var set = new PartMaterials(prefix);
            foreach (ModelMaterialSpec spec in specs)
            {
                set.Add(ModelMaterials.GetOrCreate(spec, folder, textureFolder, prefix));
            }

            UnityEditor.AssetDatabase.SaveAssets();
            return set;
        }
    }
}
