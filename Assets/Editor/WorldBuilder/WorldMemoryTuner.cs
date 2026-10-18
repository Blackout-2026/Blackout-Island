using System;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Profiling;

namespace Blackout.Editor
{
    /// <summary>W11's two separate memory-related changes. Saves only Environment or the generated grass texture;
    /// never bakes lighting, changes texture importers globally or saves other owners' assets.</summary>
    public static class WorldMemoryTuner
    {
        [MenuItem("Blackout/World/Apply Dock Post Lightmap Scale And Report")]
        public static void ApplyDockPostLightmapAndReport()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Apply lightmap scales outside Play mode.");
            SceneReport.WithEnvironment(scene =>
            {
                if (scene.isDirty) throw new InvalidOperationException("Save Environment's current edits before running this tool.");
                GameObject beach = WorldSceneSkeleton.FindRoot(scene, "Beach");
                Transform dock = beach != null ? beach.transform.Find("Dock") : null;
                if (dock == null) throw new InvalidOperationException("Beach/Dock was not found.");
                SceneReport.Snapshot before = SceneReport.Capture(scene);
                string scalesBefore = DockScales(dock);
                Debug.Log("[World] Dock lightmap before change\n" + SceneReport.Format(before));
                int changed = ApplyDockPostScales(dock);
                if (changed > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Environment could not be saved.");
                }
                SceneReport.Snapshot after = SceneReport.Capture(scene);
                string report = "| Parameter | Before | After | What changed and why |\n| --- | --- | --- | --- |\n"
                    + "| Dock post Scale In Lightmap : renderer count | " + scalesBefore + " | " + DockScales(dock)
                    + " | Allocate less baked-lightmap detail to narrow support posts; deck, rails and bunker remain authored. |\n\n"
                    + "Only the generated Dock_Post_ renderers' scale changes. GI flags, transforms, geometry, colliders and materials stay authored. "
                    + "No lighting was baked: actual occupied texels, lightmap memory and visual quality remain unmeasured.\n\n"
                    + SceneReport.Format(after);
                if (changed > 0) SceneReport.AppendObservation("dock post lightmap scale comparison", report);
                else Debug.Log("[World] Dock lightmap scales already configured\n" + report);
                Debug.Log("[World] Dock post lightmap scales: " + scalesBefore + " -> " + DockScales(dock) + "; changed renderers: " + changed);
            });
        }

        public static int ApplyDockPostScales(Transform dock)
        {
            int changed = 0;
            foreach (Transform child in dock)
            {
                if (!child.name.StartsWith("Dock_Post_", StringComparison.Ordinal)) continue;
                MeshRenderer renderer = child.GetComponent<MeshRenderer>();
                if (renderer == null) throw new InvalidOperationException("Generated post has no MeshRenderer: " + child.name);
                if (Mathf.Approximately(renderer.scaleInLightmap, BeachGreyboxBuilder.DockPostLightmapScale)) continue;
                renderer.scaleInLightmap = BeachGreyboxBuilder.DockPostLightmapScale;
                changed++;
            }
            return changed;
        }

        private static string DockScales(Transform dock) => string.Join(", ", dock.GetComponentsInChildren<MeshRenderer>(true)
            .Where(r => r.transform.parent == dock && r.name.StartsWith("Dock_Post_", StringComparison.Ordinal))
            .GroupBy(r => r.scaleInLightmap).OrderBy(g => g.Key)
            .Select(g => g.Key.ToString("0.###", CultureInfo.InvariantCulture) + ": " + g.Count()));

        [MenuItem("Blackout/World/Compress Grass Texture And Report")]
        public static void CompressGrassAndReport()
        {
            if (Application.isPlaying) throw new InvalidOperationException("Compress the generated texture outside Play mode.");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64)
                throw new InvalidOperationException("This W11 comparison targets StandaloneWindows64; review compression for another target.");
            SceneReport.WithEnvironment(scene =>
            {
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(VegetationPlaceholderBuilder.GrassTexturePath);
                if (texture == null) throw new InvalidOperationException("The generated GrassBlades texture was not found.");
                SceneReport.Snapshot before = SceneReport.Capture(scene);
                TextureFormat oldFormat = texture.format;
                int oldPayload = texture.GetRawTextureData<byte>().Length;
                long oldMemory = Profiler.GetRuntimeMemorySizeLong(texture);
                long oldTotalMemory = before.Textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
                Debug.Log("[World] Grass texture before change\n" + SceneReport.Format(before));
                bool changed = CompressGrass(texture);
                if (changed)
                {
                    EditorUtility.SetDirty(texture);
                    AssetDatabase.SaveAssetIfDirty(texture);
                }
                SceneReport.Snapshot after = SceneReport.Capture(scene);
                long newMemory = Profiler.GetRuntimeMemorySizeLong(texture);
                long newTotalMemory = after.Textures.Sum(t => Profiler.GetRuntimeMemorySizeLong(t));
                string report = "| Metric | Before | After | What changed and why |\n| --- | ---: | ---: | --- |\n"
                    + "| Generated grass format | " + oldFormat + " | " + texture.format + " | Explicit DXT5/BC3 retains alpha for the Windows target. |\n"
                    + "| Grass raw mip payload (bytes) | " + oldPayload + " | " + texture.GetRawTextureData<byte>().Length + " | Same 128x128 dimensions and mip levels. |\n"
                    + "| Grass Editor runtime memory estimate (bytes) | " + oldMemory + " | " + newMemory + " | Same graphics context, includes CPU copies/overhead. |\n"
                    + "| All reported textures, Editor runtime memory estimate (bytes) | " + oldTotalMemory + " | " + newTotalMemory + " | One texture changed; terrain control map and soft particle gradient are retained. |\n\n"
                    + "The generated texture keeps its GUID, wrap/filter settings, dimensions and mip count. The placeholder builder uses the same compression method. "
                    + "BC3 is lossy: inspect grass edges/colour and distant mip levels before accepting the visual tradeoff. Runtime memory estimates are not build size or GPU frame time.\n\n"
                    + SceneReport.Format(after);
                if (changed) SceneReport.AppendObservation("grass texture compression comparison", report);
                else Debug.Log("[World] Grass compression already configured\n" + report);
                Debug.Log("[World] Grass compression: " + oldFormat + " -> " + texture.format + "; changed: " + changed
                    + "; payload bytes: " + oldPayload + " -> " + texture.GetRawTextureData<byte>().Length
                    + "; texture memory estimate: " + oldMemory + " -> " + newMemory);
            });
        }

        /// <summary>Deterministic Editor compression of the generated readable RGBA grass image, including its mip chain.</summary>
        public static bool CompressGrass(Texture2D texture)
        {
            if (texture.format == TextureFormat.DXT5) return false;
            if (texture.format != TextureFormat.RGBA32 || !texture.isReadable
                || texture.width != VegetationPlaceholderBuilder.GrassTextureSize || texture.height != VegetationPlaceholderBuilder.GrassTextureSize)
                throw new InvalidOperationException("Expected the generated readable 128x128 RGBA32 grass texture.");
            EditorUtility.CompressTexture(texture, TextureFormat.DXT5, TextureCompressionQuality.Best);
            texture.Apply(false, false);
            return true;
        }

        // Batch convenience: one change, fresh report, then the next change and another fresh report.
        public static void ApplyMemoryChanges()
        {
            ApplyDockPostLightmapAndReport();
            CompressGrassAndReport();
        }
    }
}
