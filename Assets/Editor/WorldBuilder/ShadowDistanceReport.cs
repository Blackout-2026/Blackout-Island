using System;
using System.Globalization;
using System.IO;
using Blackout.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using QualityLevel = Blackout.Environment.QualityLevel;

namespace Blackout.Editor
{
    /// <summary>Records W11's compiled shadow policy before/after without editing the Systems Engineer's URP asset.
    /// Runtime application/restoration is verified separately by QualityPresetPlayTests; timings stay manual.</summary>
    public static class ShadowDistanceReport
    {
        [Serializable]
        private sealed class Sample
        {
            public string UnityVersion, Graphics, Quality, Target, PipelinePath;
            public float Low, Medium, High, SavedDistance;
        }

        private static string BeforePath => Path.Combine(Path.GetTempPath(), "blackout-w11-shadow-before.json");

        // Run before changing the runtime policy; the comparison file is intentionally outside the repository.
        public static void CaptureBefore()
        {
            SceneReport.WithEnvironment(scene =>
            {
                Sample sample = Capture();
                File.WriteAllText(BeforePath, JsonUtility.ToJson(sample, true));
                Debug.Log("[World] Shadow policy before: " + sample.Low + " / " + sample.Medium + " / " + sample.High
                    + "; saved URP distance: " + sample.SavedDistance + "; sample: " + BeforePath);
                Debug.Log("[World] Shadow before Scene Report\n" + SceneReport.Format(SceneReport.Capture(scene)));
            });
        }

        public static void AppendComparison()
        {
            if (!File.Exists(BeforePath)) throw new InvalidOperationException("Capture the before policy first: " + BeforePath);
            Sample before = JsonUtility.FromJson<Sample>(File.ReadAllText(BeforePath));
            Sample after = Capture();
            if (before.UnityVersion != after.UnityVersion || before.Graphics != after.Graphics || before.Quality != after.Quality
                || before.Target != after.Target || before.PipelinePath != after.PipelinePath)
                throw new InvalidOperationException("Shadow samples use different contexts; compare the same Editor, graphics, quality, target and pipeline.");
            SceneReport.WithEnvironment(scene =>
            {
                string report = "| Metric | Before | After | What changed and why |\n| --- | ---: | ---: | --- |\n"
                    + "| Runtime Medium shadow distance (m) | " + Number(before.Medium) + " | " + Number(after.Medium)
                    + " | Reduce Medium reach by 10 m; align it with the saved PC distance. Low/High and other preset settings are retained. |\n"
                    + "| Runtime Low / High distance (m) | " + Number(before.Low) + " / " + Number(before.High) + " | "
                    + Number(after.Low) + " / " + Number(after.High) + " | Retained. |\n"
                    + "| Saved URP distance (m), asset read only | " + Number(before.SavedDistance) + " | " + Number(after.SavedDistance)
                    + " | QualityPreset applies in Play mode and restores the original in-memory value when destroyed. |\n"
                    + "| Outdoor fog transmittance at Medium cutoff (calculated %) | " + Number(100f * FogVisibility(before.Medium))
                    + " | " + Number(100f * FogVisibility(after.Medium)) + " | Exponential-squared fog at density 0.0135; visual cutoff still needs review. |\n\n"
                    + "The before/after values are read from the compiled QualityPreset policy in matching contexts. "
                    + "This measures configuration, not rendered shadow triangles, draw calls, CPU/GPU time or visual quality. "
                    + "The saved pipeline asset, cascades, shadow map resolution, scene geometry and gameplay objects are not changed.\n\n"
                    + SceneReport.Format(SceneReport.Capture(scene));
                SceneReport.AppendObservation("runtime Medium shadow distance comparison", report);
                Debug.Log("[World] Runtime Medium shadow distance: " + Number(before.Medium) + " -> " + Number(after.Medium)
                    + "; saved URP distance: " + Number(before.SavedDistance) + " -> " + Number(after.SavedDistance));
            });
        }

        private static Sample Capture()
        {
            var pipeline = GraphicsSettings.currentRenderPipeline as UniversalRenderPipelineAsset;
            if (pipeline == null) throw new InvalidOperationException("The active URP pipeline is required for the comparison.");
            return new Sample
            {
                UnityVersion = Application.unityVersion,
                Graphics = SystemInfo.graphicsDeviceType.ToString(),
                Quality = QualitySettings.names[QualitySettings.GetQualityLevel()],
                Target = EditorUserBuildSettings.activeBuildTarget.ToString(),
                PipelinePath = AssetDatabase.GetAssetPath(pipeline),
                Low = QualityPreset.For(QualityLevel.Low).ShadowDistance,
                Medium = QualityPreset.For(QualityLevel.Medium).ShadowDistance,
                High = QualityPreset.For(QualityLevel.High).ShadowDistance,
                SavedDistance = pipeline.shadowDistance,
            };
        }

        private static float FogVisibility(float distance) => Mathf.Exp(-Mathf.Pow(LightingLayout.FogDensity * distance, 2f));
        private static string Number(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
    }
}
