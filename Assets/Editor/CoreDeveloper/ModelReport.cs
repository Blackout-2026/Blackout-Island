using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>What the importer found for one mesh in the FBX.</summary>
    public readonly struct MeshEntry
    {
        public readonly string Name;
        public readonly int Triangles;
        public readonly bool HasUv0;

        public MeshEntry(string name, int triangles, bool hasUv0)
        {
            Name = name;
            Triangles = triangles;
            HasUv0 = hasUv0;
        }
    }

    /// <summary>
    /// Turns the numbers measured on an imported model into the Console text and the warnings
    /// (docs/art-pipeline.md section 9). No Unity asset access, so it can be tested directly.
    /// </summary>
    public sealed class ModelReport
    {
        public const float MaxPlausibleSize = 50f;
        public const float MinPlausibleSize = 0.05f;
        private const string Prefix = "[ModelImport] ";

        public string Text { get; private set; }
        public List<string> Warnings { get; } = new List<string>();
        public int TotalTriangles { get; private set; }

        public static ModelReport Build(string assetPath, IReadOnlyList<MeshEntry> meshes, int materialCount, Vector3 size)
        {
            var report = new ModelReport();
            string fileName = Path.GetFileName(assetPath);
            string assetName = Path.GetFileNameWithoutExtension(assetPath);

            for (int i = 0; i < meshes.Count; i++)
            {
                report.TotalTriangles += meshes[i].Triangles;
            }

            bool hasBudget = ModelBudget.TryGet(assetName, out ModelBudget budget);
            var text = new StringBuilder();
            text.Append(Prefix).Append(fileName).Append(": ")
                .Append(meshes.Count).Append(meshes.Count == 1 ? " mesh, " : " meshes, ")
                .Append(Format(report.TotalTriangles)).Append(" triangles");
            if (hasBudget)
            {
                text.Append(" (target ").Append(Format(budget.TargetTriangles))
                    .Append(", max ").Append(Format(budget.MaxTriangles)).Append(')');
            }

            text.Append(", ").Append(materialCount).Append(materialCount == 1 ? " material" : " materials")
                .Append(", size ").Append(size.x.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" x ").Append(size.y.ToString("F2", CultureInfo.InvariantCulture))
                .Append(" x ").Append(size.z.ToString("F2", CultureInfo.InvariantCulture)).Append(" m");

            for (int i = 0; i < meshes.Count; i++)
            {
                text.Append("\n  ").Append(meshes[i].Name).Append(": ")
                    .Append(Format(meshes[i].Triangles)).Append(" triangles");
            }

            report.Text = text.ToString();
            report.AddWarnings(fileName, assetName, meshes, materialCount, size, hasBudget, budget);
            return report;
        }

        private void AddWarnings(string fileName, string assetName, IReadOnlyList<MeshEntry> meshes, int materialCount,
            Vector3 size, bool hasBudget, ModelBudget budget)
        {
            if (!assetName.StartsWith("SM_"))
            {
                Warn(fileName, "file name should start with SM_ (docs/art-pipeline.md section 2)");
            }

            if (hasBudget)
            {
                if (TotalTriangles > budget.MaxTriangles)
                {
                    Warn(fileName, $"{Format(TotalTriangles)} triangles is over the hard max of {Format(budget.MaxTriangles)}");
                }

                if (materialCount > budget.MaxMaterials)
                {
                    Warn(fileName, $"{materialCount} materials is over the limit of {budget.MaxMaterials}");
                }
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                if (!meshes[i].HasUv0)
                {
                    Warn(fileName, $"mesh '{meshes[i].Name}' has no UV0");
                }
            }

            float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            if (largest > MaxPlausibleSize || largest < MinPlausibleSize)
            {
                Warn(fileName, $"largest side is {largest.ToString("F2", CultureInfo.InvariantCulture)} m; "
                               + "check the export scale (Apply Scalings: FBX Units Scale, Scale 1.0)");
            }
        }

        private void Warn(string fileName, string message)
        {
            Warnings.Add(Prefix + fileName + ": " + message);
        }

        private static string Format(int value)
        {
            return value.ToString("N0", CultureInfo.InvariantCulture);
        }
    }
}
