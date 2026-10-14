using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    public enum PropStatus
    {
        Missing, // no prefab at the catalog path yet
        Placeholder, // a primitive stand-in made by the builder
        Model, // an imported SM_*.fbx sits in the Model child
        Kit, // a prefab added by hand from a CC0 pack
        Generated, // a quad made by the decal tool
    }

    public enum BudgetState
    {
        None, // nothing to compare (no prefab)
        Ok, // at or under the target
        OverTarget, // between the target and the hard maximum
        OverMax, // over the hard maximum: needs a reduction pass
    }

    /// <summary>One line of the Props palette: a catalog entry and what is measured on its prefab.</summary>
    public sealed class PropPaletteRow
    {
        public PropEntry Entry;
        public GameObject Prefab;
        public PropStatus Status;
        public int Triangles;
        public int Materials;
        public int Colliders;
        public Vector3 MeasuredSize;

        public bool Exists => Prefab != null;

        public BudgetState TriangleState
        {
            get
            {
                if (!Exists)
                {
                    return BudgetState.None;
                }

                ModelBudget budget = Entry.Budget;
                return Triangles <= budget.TargetTriangles ? BudgetState.Ok
                    : Triangles <= budget.MaxTriangles ? BudgetState.OverTarget
                    : BudgetState.OverMax;
            }
        }

        public bool MaterialsOverLimit => Exists && Materials > Entry.Budget.MaxMaterials;

        /// <summary>Triangles if every one of the level's copies were drawn at once: what the room actually costs.</summary>
        public int LevelTriangles => Exists ? Triangles * Entry.Count : 0;
    }

    /// <summary>Reads the catalog and the prefabs on disk into palette rows. No window code, so it can be tested.</summary>
    public static class PropPaletteModel
    {
        /// <summary>Every prop in the catalog plus every sign decal.</summary>
        public static List<PropEntry> AllEntries()
        {
            var entries = new List<PropEntry>(PropCatalog.All);
            entries.AddRange(DecalCatalog.Entries);
            return entries;
        }

        public static List<PropPaletteRow> BuildRows()
        {
            return BuildRows(AllEntries());
        }

        public static List<PropPaletteRow> BuildRows(IEnumerable<PropEntry> entries)
        {
            var rows = new List<PropPaletteRow>();
            foreach (PropEntry entry in entries)
            {
                rows.Add(Describe(entry));
            }

            return rows;
        }

        public static PropPaletteRow Describe(PropEntry entry)
        {
            var row = new PropPaletteRow { Entry = entry, Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(entry.PrefabPath) };
            if (row.Prefab == null)
            {
                row.Status = PropStatus.Missing;
                return row;
            }

            row.Triangles = PropStats.Triangles(row.Prefab);
            row.Materials = PropStats.Materials(row.Prefab);
            row.Colliders = PropStats.Colliders(row.Prefab);
            row.MeasuredSize = PropStats.LocalBounds(row.Prefab).size;
            row.Status = StatusOf(entry, row.Prefab);
            return row;
        }

        private static PropStatus StatusOf(PropEntry entry, GameObject prefab)
        {
            switch (entry.Source)
            {
                case PropSource.Generated:
                    return PropStatus.Generated;
                case PropSource.Cc0Kit:
                    return PropStatus.Kit;
                default:
                    Transform model = prefab.transform.Find(ModelPrefabBuilder.ModelName);
                    bool imported = model != null && PrefabUtility.IsPartOfPrefabInstance(model.gameObject);
                    return imported ? PropStatus.Model : PropStatus.Placeholder;
            }
        }
    }
}
