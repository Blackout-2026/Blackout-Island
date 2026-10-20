using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Mixes the tree shapes over the island (docs/art-pipeline.md section 19.2). The World Builder's Paint Terrain And Forest plants one
    /// prototype per kind (Pine, Palm), so every pine is the same tree, and the start beach, which is all palms, is a row of the same palm.
    /// This tool runs after it: it adds the other prefabs built by <see cref="TreePrefabBuilder"/> as extra tree prototypes on
    /// IslandTerrainData and reassigns the planted trees:
    /// - every pine becomes one of Pine, PineB, PineC, PineD;
    /// - every palm becomes one of Palm, PalmB, PalmC, PalmD, and about <see cref="BeachShare"/> of them become another species
    ///   (a sea almond or a banana plant) so the start beach is a mixed grove and not a palm plantation.
    /// The choice is a fixed hash of the tree's position, so it is the same every run and the same on every machine, and the tool can be
    /// run again without changing anything. Positions, scales and yaw are not touched. No World Builder code is changed.
    /// A repaint resets the island to the single Pine and Palm, so run this again after every Paint Terrain And Forest.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "&lt;repo&gt;" -executeMethod Blackout.Editor.TreeDiversifier.Diversify -logFile -
    /// </summary>
    public static class TreeDiversifier
    {
        /// <summary>The variants of a group and how often each is chosen. The first one is the original ("Pine", "Palm").</summary>
        public sealed class Mix
        {
            public readonly string[] Names;
            public readonly float[] Weights;
            public readonly int Salt; // a fixed number per group (not string.GetHashCode, which is not stable)

            public Mix(string[] names, float[] weights, int salt)
            {
                Names = names;
                Weights = weights;
                Salt = salt;
            }
        }

        public static readonly Mix PineMix = new Mix(new[] { "Pine", "PineB", "PineC", "PineD" }, new[] { 0.34f, 0.26f, 0.24f, 0.16f }, 1);
        public static readonly Mix PalmMix = new Mix(new[] { "Palm", "PalmB", "PalmC", "PalmD" }, new[] { 0.34f, 0.26f, 0.18f, 0.22f }, 2);
        public static readonly Mix BeachMix = new Mix(new[] { "Almond", "AlmondB", "Banana", "BananaB" }, new[] { 0.34f, 0.26f, 0.22f, 0.18f }, 3);

        /// <summary>The share of the planted palms that become another species (sea almond, banana) instead.</summary>
        public const float BeachShare = 0.38f;

        private const int ShareSalt = 11;

        // The World Builder's terrain data (TerrainBuilder.TerrainDataPath). Written out here because this assembly must not reference theirs.
        public const string TerrainDataPath = "Assets/Environment/Terrain/IslandTerrainData.asset";

        [MenuItem("Blackout/Art/Diversify Forest")]
        public static void Diversify()
        {
            var data = AssetDatabase.LoadAssetAtPath<TerrainData>(TerrainDataPath);
            if (data == null)
            {
                Debug.LogWarning($"[Trees] {TerrainDataPath} not found: run Blackout > World > Paint Terrain And Forest first.");
                return;
            }

            string report = Apply(data);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            foreach (Terrain terrain in Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None))
            {
                terrain.Flush();
            }

            Debug.Log(report);
        }

        /// <summary>Adds the variant prototypes to the terrain data and reassigns the pines and palms. Returns a report.</summary>
        public static string Apply(TerrainData data)
        {
            var prototypes = new List<TreePrototype>(data.treePrototypes);
            TreeInstance[] instances = data.treeInstances;
            var report = new StringBuilder("[Trees] Diversify Forest:");

            // which prototypes the World Builder (or an earlier run) already planted, before any are added
            HashSet<int> pineFamily = Family(prototypes, PineMix);
            HashSet<int> palmFamily = Family(prototypes, PalmMix); // the palm zone: the beach and the inland palm patches
            palmFamily.UnionWith(Family(prototypes, BeachMix));

            Group pines = Resolve(prototypes, PineMix, report);
            Group palms = Resolve(prototypes, PalmMix, report);
            Group beach = Resolve(prototypes, BeachMix, report);

            if (pineFamily.Count == 0)
            {
                report.Append("\n  pines: none planted from Pine.prefab (Build Realistic Trees, then Paint Terrain And Forest, then again).");
            }

            if (palmFamily.Count == 0)
            {
                report.Append("\n  palms: none planted from Palm.prefab (Build Realistic Trees, then Paint Terrain And Forest, then again).");
            }

            for (int i = 0; i < instances.Length; i++)
            {
                int current = instances[i].prototypeIndex;
                Vector3 position = instances[i].position;
                if (pines.Indices.Count > 0 && pineFamily.Contains(current))
                {
                    int pick = ChooseVariant(position, pines.Weights, PineMix.Salt);
                    instances[i].prototypeIndex = pines.Indices[pick];
                    pines.Counts[pick]++;
                }
                else if (palmFamily.Contains(current))
                {
                    if (beach.Indices.Count > 0 && Chance(position, ShareSalt) < BeachShare)
                    {
                        int pick = ChooseVariant(position, beach.Weights, BeachMix.Salt);
                        instances[i].prototypeIndex = beach.Indices[pick];
                        beach.Counts[pick]++;
                    }
                    else if (palms.Indices.Count > 0)
                    {
                        int pick = ChooseVariant(position, palms.Weights, PalmMix.Salt);
                        instances[i].prototypeIndex = palms.Indices[pick];
                        palms.Counts[pick]++;
                    }
                }
            }

            report.Append(pines.Report("pines", prototypes)).Append(palms.Report("palms", prototypes)).Append(beach.Report("beach species", prototypes));
            data.treePrototypes = prototypes.ToArray();
            data.SetTreeInstances(instances, false);
            return report.ToString();
        }

        private sealed class Group
        {
            public readonly List<int> Indices = new List<int>();
            public readonly List<float> WeightList = new List<float>();
            public int[] Counts = new int[0];

            public float[] Weights => WeightList.ToArray();

            public string Report(string label, List<TreePrototype> prototypes)
            {
                var text = new StringBuilder($"\n  {label}:");
                if (Indices.Count == 0)
                {
                    return text.Append(" none").ToString();
                }

                for (int v = 0; v < Indices.Count; v++)
                {
                    text.Append($" {prototypes[Indices[v]].prefab.name} {Counts[v]}");
                }

                return text.ToString();
            }
        }

        /// <summary>The prototype indices that already are a member of a mix (planted by the World Builder or by an earlier run).</summary>
        private static HashSet<int> Family(List<TreePrototype> prototypes, Mix mix)
        {
            var family = new HashSet<int>();
            for (int i = 0; i < prototypes.Count; i++)
            {
                if (prototypes[i].prefab != null && System.Array.IndexOf(mix.Names, prototypes[i].prefab.name) >= 0)
                {
                    family.Add(i);
                }
            }

            return family;
        }

        /// <summary>Finds each variant's prefab, appends it to the prototypes if it is not there, and returns the indices and weights.</summary>
        private static Group Resolve(List<TreePrototype> prototypes, Mix mix, StringBuilder report)
        {
            var group = new Group();
            for (int v = 0; v < mix.Names.Length; v++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{TreePrefabBuilder.PrefabFolder}/{mix.Names[v]}.prefab");
                if (prefab == null)
                {
                    report.Append($"\n  {mix.Names[v]}.prefab does not exist: skipped (run Blackout > Art > Build Realistic Trees).");
                    continue;
                }

                int index = prototypes.FindIndex(p => p.prefab == prefab);
                if (index < 0)
                {
                    prototypes.Add(new TreePrototype { prefab = prefab, bendFactor = 0f });
                    index = prototypes.Count - 1;
                }

                group.Indices.Add(index);
                group.WeightList.Add(mix.Weights[v]);
            }

            group.Counts = new int[group.Indices.Count];
            return group;
        }

        private static uint Hash(Vector3 position, int salt)
        {
            unchecked
            {
                uint h = (uint)Mathf.RoundToInt(position.x * 100000f) * 73856093u ^ (uint)Mathf.RoundToInt(position.z * 100000f) * 19349663u
                    ^ (uint)salt * 83492791u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }

        /// <summary>A fixed number in 0 .. 1 for a position and a salt: the same place always gives the same number.</summary>
        public static float Chance(Vector3 position, int salt)
        {
            return (Hash(position, salt) & 0xFFFFFF) / 16777216f;
        }

        /// <summary>
        /// The variant (an index into <paramref name="weights"/>) of the tree at a normalised terrain position: a fixed hash, so the same
        /// place always gets the same tree, and the mix follows the weights.
        /// </summary>
        public static int ChooseVariant(Vector3 position, float[] weights, int salt = 0)
        {
            float total = 0f;
            foreach (float weight in weights)
            {
                total += weight;
            }

            float u = Chance(position, salt) * total;
            for (int i = 0; i < weights.Length; i++)
            {
                u -= weights[i];
                if (u < 0f)
                {
                    return i;
                }
            }

            return weights.Length - 1;
        }
    }
}
