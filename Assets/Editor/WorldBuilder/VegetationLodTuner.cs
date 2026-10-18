using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>W11's one-parameter palm LOD change. Only updates the generated placeholder's middle threshold;
    /// meshes, renderers, bounds, colliders, terrain instances and the Environment scene are left as authored.</summary>
    public static class VegetationLodTuner
    {
        [MenuItem("Blackout/World/Apply Palm LOD And Report")]
        public static void ApplyPalmLodAndReport()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Apply the palm LOD outside Play mode.");
            SceneReport.WithEnvironment(scene =>
            {
                SceneReport.Snapshot before = SceneReport.Capture(scene);
                var layout = AssetDatabase.LoadAssetAtPath<Blackout.World.BunkerLayout>(BunkerLayoutAssetBuilder.AssetPath);
                if (layout == null) throw new InvalidOperationException("The saved BunkerLayout is required for the five viewpoints.");
                List<Viewpoint> views = VegetationBudget.Viewpoints(MarkerLayout.Build(layout));
                Terrain terrain = before.Terrains.Find(t => t.name == "IslandTerrain");
                if (terrain == null) throw new InvalidOperationException("IslandTerrain was not found.");
                string path = VegetationLibrary.PlaceholderPath(VegetationPrototype.Palm);
                bool inUse = Array.Exists(terrain.terrainData.treePrototypes,
                    prototype => AssetDatabase.GetAssetPath(prototype.prefab) == path);
                if (!inUse) throw new InvalidOperationException("IslandTerrain is not using the generated palm placeholder; inspect its authored prefab first.");

                var previous = new ViewCount[views.Count];
                for (int i = 0; i < views.Count; i++) previous[i] = VegetationBudget.Measure(terrain, views[i]);
                // Format now: vegetation measurement reads live prefab data, so formatting later would show new LODs.
                Debug.Log("[World] Palm LOD before change\n" + SceneReport.Format(before));
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                float oldHeight, groupSize;
                bool changed;
                try
                {
                    LODGroup group = contents.GetComponent<LODGroup>();
                    if (group == null) throw new InvalidOperationException("Generated palm has no LODGroup.");
                    LOD[] levels = group.GetLODs();
                    if (levels.Length != 3) throw new InvalidOperationException("Expected three generated palm LODs.");
                    oldHeight = levels[1].screenRelativeTransitionHeight;
                    groupSize = group.size;
                    changed = ApplyThreshold(group);
                    if (changed) PrefabUtility.SaveAsPrefabAsset(contents, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }

                SceneReport.Snapshot after = SceneReport.Capture(scene);
                var report = new StringBuilder();
                report.AppendLine("Changed only the generated palm's LOD1 transition height; "
                    + "712-triangle LOD0 stays at 0.25, 230-triangle LOD1 switches to 96-triangle LOD2 sooner, "
                    + "and the final cull threshold stays at 0.02. The builder uses the same shared constant.\n");
                report.AppendLine("| Parameter | Before | After | Why |\n| --- | ---: | ---: | --- |");
                report.AppendLine("| Palm LOD1 screen height | " + Number(oldHeight) + " | " + Number(VegetationLibrary.PalmLod1Height)
                    + " | Reduce distant palm geometry while retaining nearby LOD0. |");
                report.AppendLine("| Unit-scale LOD1-to-LOD2 distance at current LOD bias (m) | "
                    + Number(VegetationLibrary.LodDistance(groupSize, oldHeight, QualitySettings.lodBias)) + " | "
                    + Number(VegetationLibrary.LodDistance(groupSize, VegetationLibrary.PalmLod1Height, QualitySettings.lodBias))
                    + " | Instance scale changes actual distances; camera FOV stays 72 degrees. |\n");
                report.AppendLine("| Viewpoint | Tree triangles before | Tree triangles after | Total before | Total after | Triangles saved |\n"
                    + "| --- | ---: | ---: | ---: | ---: | ---: |");
                int worstBefore = 0, worstAfter = 0;
                for (int i = 0; i < views.Count; i++)
                {
                    ViewCount current = VegetationBudget.Measure(terrain, views[i]);
                    if (current.Details != previous[i].Details || current.DetailTriangles != previous[i].DetailTriangles
                        || current.Total > previous[i].Total)
                        throw new InvalidOperationException("Unexpected vegetation measurement change at " + views[i].Name);
                    worstBefore = Mathf.Max(worstBefore, previous[i].Total);
                    worstAfter = Mathf.Max(worstAfter, current.Total);
                    report.AppendLine("| " + views[i].Name + " | " + previous[i].TreeTriangles + " | " + current.TreeTriangles
                        + " | " + previous[i].Total + " | " + current.Total + " | " + (previous[i].Total - current.Total) + " |");
                }
                report.AppendLine("\nHighest five-view estimate: " + worstBefore + " -> " + worstAfter
                    + " triangles. These are main-view estimates, without shadow passes or occlusion, not GPU timings. "
                    + "Check distant palm silhouettes and the LOD switch in the Editor before accepting the visual tradeoff.\n");
                string afterReport = SceneReport.Format(after);
                if (changed) SceneReport.AppendObservation("palm LOD1 threshold comparison", report + afterReport);
                else Debug.Log("[World] Palm LOD already configured; report\n" + afterReport);
                Debug.Log("[World] Palm LOD threshold: " + Number(oldHeight) + " -> " + Number(VegetationLibrary.PalmLod1Height)
                    + "; prefab changed: " + changed + "; highest vegetation estimate: " + worstBefore + " -> " + worstAfter);
            });
        }

        /// <summary>Preserves all LOD renderer references and other transition settings. Refuses unexpected authored layouts.</summary>
        public static bool ApplyThreshold(LODGroup group)
        {
            LOD[] levels = group.GetLODs();
            if (levels.Length != 3 || !Mathf.Approximately(levels[0].screenRelativeTransitionHeight, 0.25f)
                || !Mathf.Approximately(levels[2].screenRelativeTransitionHeight, 0.02f))
                throw new InvalidOperationException("Expected the generated palm's three LODs at 0.25 / middle / 0.02.");
            if (Mathf.Approximately(levels[1].screenRelativeTransitionHeight, VegetationLibrary.PalmLod1Height)) return false;
            if (!Mathf.Approximately(levels[1].screenRelativeTransitionHeight, 0.10f))
                throw new InvalidOperationException("Palm LOD1 has an authored threshold; no change made.");
            levels[1].screenRelativeTransitionHeight = VegetationLibrary.PalmLod1Height;
            // SetLODs can recalculate these values; retain the authored bounds used by LOD selection.
            float size = group.size;
            Vector3 referencePoint = group.localReferencePoint;
            group.SetLODs(levels);
            group.size = size;
            group.localReferencePoint = referencePoint;
            return true;
        }

        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
