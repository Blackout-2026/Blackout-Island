using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Window > Blackout > Props Palette: every prop, pickup, weapon and sign the level can use, with its triangle count
    /// measured on the prefab and compared with the budget of docs/art-pipeline.md section 6. Click a row to select the
    /// prefab, double-click to open it, drag it into the Scene or Hierarchy to place one. A missing prefab (the CC0 slots
    /// until a pack is added) shows as a grey row. Read-only: it never changes an asset, except through the Build button.
    /// </summary>
    public class PropPaletteWindow : EditorWindow
    {
        private const float NameWidth = 190f;
        private const float ColumnWidth = 64f;

        private static readonly Color Green = new Color(0.55f, 0.85f, 0.55f);
        private static readonly Color Amber = new Color(0.95f, 0.8f, 0.35f);
        private static readonly Color Red = new Color(0.95f, 0.45f, 0.4f);

        private List<PropPaletteRow> rows = new List<PropPaletteRow>();
        private Vector2 scroll;
        private string search = string.Empty;
        private int sourceFilter; // 0 all, then PropSource + 1
        private bool sortByTriangles;
        private PropPaletteRow dragged;

        [MenuItem("Window/Blackout/Props Palette")]
        public static void Open()
        {
            var window = GetWindow<PropPaletteWindow>("Props Palette");
            window.minSize = new Vector2(760f, 280f);
            window.Refresh();
        }

        private void OnEnable()
        {
            Refresh();
        }

        // Prefabs change when the builders run or an FBX is imported; refreshing when the window gains focus is enough.
        private void OnFocus()
        {
            Refresh();
        }

        private void Refresh()
        {
            rows = PropPaletteModel.BuildRows();
            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();
            DrawHeader();
            DrawRows();
            DrawFooter();
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                sourceFilter = GUILayout.Toolbar(sourceFilter, new[] { "All", "Blender", "CC0 kit", "Generated" }, EditorStyles.toolbarButton, GUILayout.Width(300f));
                GUILayout.Space(8f);
                search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField, GUILayout.Width(180f));
                sortByTriangles = GUILayout.Toggle(sortByTriangles, "Sort by triangles", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Refresh", EditorStyles.toolbarButton))
                {
                    Refresh();
                }

                if (GUILayout.Button("Build all", EditorStyles.toolbarButton))
                {
                    PropKitBuilder.BuildAll();
                    Refresh();
                }
            }
        }

        private static void DrawHeader()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                Cell("Prefab", NameWidth, true);
                Cell("Status", 78f, true);
                Cell("Tris", ColumnWidth, true);
                Cell("Budget", ColumnWidth, true);
                Cell("Mats", 44f, true);
                Cell("Cols", 44f, true);
                Cell("Size (m)", 120f, true);
                Cell("Count", 44f, true);
                Cell("Level tris", 70f, true);
            }
        }

        private void DrawRows()
        {
            IEnumerable<PropPaletteRow> view = Visible();
            using (var scrollView = new EditorGUILayout.ScrollViewScope(scroll))
            {
                scroll = scrollView.scrollPosition;
                foreach (PropPaletteRow row in view)
                {
                    DrawRow(row);
                }
            }
        }

        private List<PropPaletteRow> Visible()
        {
            var list = new List<PropPaletteRow>();
            foreach (PropPaletteRow row in rows)
            {
                bool sourceOk = sourceFilter == 0 || (int)row.Entry.Source == sourceFilter - 1;
                bool searchOk = string.IsNullOrEmpty(search) || row.Entry.Name.IndexOf(search, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (sourceOk && searchOk)
                {
                    list.Add(row);
                }
            }

            if (sortByTriangles)
            {
                list.Sort((a, b) => b.Triangles.CompareTo(a.Triangles));
            }

            return list;
        }

        private void DrawRow(PropPaletteRow row)
        {
            Rect rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(EditorGUIUtility.singleLineHeight + 2f));
            bool exists = row.Exists;
            Color previous = GUI.contentColor;
            GUI.contentColor = exists ? previous : new Color(previous.r, previous.g, previous.b, 0.45f);

            Cell(row.Entry.Name, NameWidth, false, row.Entry.Note);
            Cell(row.Status.ToString(), 78f, false);

            GUI.contentColor = exists ? ColourOf(row.TriangleState) : GUI.contentColor;
            Cell(exists ? row.Triangles.ToString("N0") : "-", ColumnWidth, false, TriangleTip(row));
            GUI.contentColor = exists ? previous : GUI.contentColor;
            Cell(row.Entry.Budget.TargetTriangles.ToString("N0"), ColumnWidth, false, $"Target. Hard maximum {row.Entry.Budget.MaxTriangles:N0}.");

            GUI.contentColor = row.MaterialsOverLimit ? Amber : GUI.contentColor;
            Cell(exists ? row.Materials.ToString() : "-", 44f, false, $"Distinct materials. The budget allows {row.Entry.Budget.MaxMaterials}.");
            GUI.contentColor = exists ? previous : GUI.contentColor;
            Cell(exists ? row.Colliders.ToString() : "-", 44f, false);
            Cell(exists ? $"{row.MeasuredSize.x:0.##} x {row.MeasuredSize.y:0.##} x {row.MeasuredSize.z:0.##}" : "-", 120f, false,
                $"Catalog size {row.Entry.Size.x:0.##} x {row.Entry.Size.y:0.##} x {row.Entry.Size.z:0.##} m");
            Cell(row.Entry.Count.ToString(), 44f, false);
            Cell(exists ? row.LevelTriangles.ToString("N0") : "-", 70f, false, "Triangles of the prefab times the number the level uses");
            GUI.contentColor = previous;
            EditorGUILayout.EndHorizontal();

            HandleMouse(rect, row);
        }

        private void HandleMouse(Rect rect, PropPaletteRow row)
        {
            Event e = Event.current;
            if (!row.Exists || !rect.Contains(e.mousePosition))
            {
                return;
            }

            if (e.type == EventType.MouseDown && e.button == 0)
            {
                Selection.activeObject = row.Prefab;
                EditorGUIUtility.PingObject(row.Prefab);
                dragged = row;
                if (e.clickCount == 2)
                {
                    AssetDatabase.OpenAsset(row.Prefab);
                    dragged = null;
                }

                e.Use();
            }
            else if (e.type == EventType.MouseDrag && dragged == row)
            {
                DragAndDrop.PrepareStartDrag();
                DragAndDrop.objectReferences = new Object[] { row.Prefab };
                DragAndDrop.StartDrag(row.Entry.Name);
                dragged = null;
                e.Use();
            }
            else if (e.type == EventType.MouseUp)
            {
                dragged = null;
            }
        }

        private void DrawFooter()
        {
            int present = 0;
            int total = 0;
            int levelTriangles = 0;
            foreach (PropPaletteRow row in rows)
            {
                total++;
                if (row.Exists)
                {
                    present++;
                    levelTriangles += row.LevelTriangles;
                }
            }

            EditorGUILayout.HelpBox($"{present} of {total} prefabs exist. Placing every prop once per level copy would draw about {levelTriangles:N0} triangles " +
                "(the placeholders are heavier than the Blender models will be).", MessageType.None);
        }

        private static Color ColourOf(BudgetState state)
        {
            switch (state)
            {
                case BudgetState.Ok: return Green;
                case BudgetState.OverTarget: return Amber;
                case BudgetState.OverMax: return Red;
                default: return Color.white;
            }
        }

        private static string TriangleTip(PropPaletteRow row)
        {
            switch (row.TriangleState)
            {
                case BudgetState.Ok: return "At or under the target";
                case BudgetState.OverTarget: return "Over the target, under the hard maximum";
                case BudgetState.OverMax: return "Over the hard maximum: needs a reduction pass";
                default: return "No prefab yet";
            }
        }

        private static void Cell(string text, float width, bool bold, string tooltip = null)
        {
            GUILayout.Label(new GUIContent(text, tooltip), bold ? EditorStyles.boldLabel : EditorStyles.label, GUILayout.Width(width));
        }
    }
}
