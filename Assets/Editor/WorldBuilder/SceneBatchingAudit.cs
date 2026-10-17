using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Blackout.Environment;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Read-only shader layout and authored static batching checks. Never combines meshes or saves assets.</summary>
    public static class SceneBatchingAudit
    {
        public enum CompatibilityStatus { Compatible, Incompatible, Unknown }

        public sealed class Result
        {
            public int Flagged, RuleTargets, PropertyBlocks, ConfiguredPropertyBlockTargets;
            public int CompatibleSubshaders, IncompatibleSubshaders, UnknownSubshaders;
            public string BuildBatching;
            public readonly List<string> RuleMismatches = new List<string>();
            public readonly List<string> ShaderRows = new List<string>();
        }

        // Unity exposes these read-only Inspector queries internally. Match signatures, and report unknown
        // if a future Editor changes them; guessing from a shader's name would hide incompatible custom shaders.
        private const BindingFlags StaticMethods = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private static readonly MethodInfo Compatibility = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityCode",
            StaticMethods, null, new[] { typeof(Shader), typeof(int) }, null);
        private static readonly MethodInfo Reason = typeof(ShaderUtil).GetMethod("GetSRPBatcherCompatibilityIssueReason",
            StaticMethods, null, new[] { typeof(Shader), typeof(int), typeof(int) }, null);

        public static Result Capture(Scene scene, IEnumerable<Material> materials)
        {
            var result = new Result { BuildBatching = BuildBatching() };
            var propertyBlockTargets = new HashSet<Renderer>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (HasBatchFlag(renderer.gameObject)) result.Flagged++;
                    if (renderer.HasPropertyBlock()) result.PropertyBlocks++;
                }
                foreach (Transform child in root.transform)
                {
                    StaticEditorFlags? expected = StaticFlagRules.For(root.name, child.name);
                    if (!expected.HasValue) continue;
                    bool shouldBatch = (expected.Value & StaticEditorFlags.BatchingStatic) != 0;
                    foreach (Renderer renderer in child.GetComponentsInChildren<Renderer>(true))
                        CheckRule(result, renderer.gameObject, shouldBatch);
                    foreach (Terrain terrain in child.GetComponentsInChildren<Terrain>(true))
                        CheckRule(result, terrain.gameObject, shouldBatch);
                }
                foreach (FlickerLight flicker in root.GetComponentsInChildren<FlickerLight>(true))
                    if (flicker.Lamp != null) propertyBlockTargets.Add(flicker.Lamp);
                foreach (PowerStateLighting lighting in root.GetComponentsInChildren<PowerStateLighting>(true))
                {
                    // Read serialized configuration without adding a runtime-only reporting API to the component.
                    using (var serialized = new SerializedObject(lighting))
                    {
                        foreach (string name in new[] { "poweredFixtures", "emergencyLights", "alarmBeacons", "screens" })
                        {
                            SerializedProperty array = serialized.FindProperty(name);
                            if (array == null) continue;
                            for (int i = 0; i < array.arraySize; i++)
                            {
                                SerializedProperty entry = array.GetArrayElementAtIndex(i);
                                SerializedProperty reference = name == "screens" ? entry : entry.FindPropertyRelative("lamp");
                                if (reference?.objectReferenceValue is Renderer renderer) propertyBlockTargets.Add(renderer);
                            }
                        }
                    }
                }
            }
            result.ConfiguredPropertyBlockTargets = propertyBlockTargets.Count;
            foreach (Shader shader in materials.Where(m => m != null && m.shader != null).Select(m => m.shader)
                .Distinct().OrderBy(s => s.name, StringComparer.Ordinal))
                for (int index = 0; index < shader.subshaderCount; index++)
                    CheckShader(result, shader, index);
            result.RuleMismatches.Sort(StringComparer.Ordinal);
            return result;
        }

        private static bool HasBatchFlag(GameObject go) =>
            (GameObjectUtility.GetStaticEditorFlags(go) & StaticEditorFlags.BatchingStatic) != 0;

        private static void CheckRule(Result result, GameObject go, bool expected)
        {
            result.RuleTargets++;
            if (HasBatchFlag(go) != expected)
                result.RuleMismatches.Add(PathOf(go.transform) + ": BatchingStatic should be " + expected);
        }

        private static string PathOf(Transform item) => item.parent == null ? item.name : PathOf(item.parent) + "/" + item.name;

        private static void CheckShader(Result result, Shader shader, int index)
        {
            string status;
            try
            {
                if (Compatibility == null) throw new MissingMethodException("Editor compatibility query unavailable");
                int code = (int)Compatibility.Invoke(null, new object[] { shader, index });
                string explanation = code != 0 && Reason != null
                    ? (string)Reason.Invoke(null, new object[] { shader, index, code }) : "";
                CompatibilityStatus classification = ClassifyCompatibility(code, explanation);
                if (classification == CompatibilityStatus.Compatible) { result.CompatibleSubshaders++; status = "compatible"; }
                else if (classification == CompatibilityStatus.Incompatible)
                {
                    result.IncompatibleSubshaders++;
                    status = "incompatible (code " + code + "): " + explanation;
                }
                else { result.UnknownSubshaders++; status = "unknown (code " + code + "): " + explanation + "; inspect Shader Inspector"; }
            }
            catch (Exception error)
            {
                result.UnknownSubshaders++;
                status = "unknown: " + error.GetBaseException().Message;
            }
            result.ShaderRows.Add("| " + shader.name + " | " + index + " | " + status.Replace("|", "\\|").Replace("\n", " ") + " |");
        }

        public static CompatibilityStatus ClassifyCompatibility(int code, string reason)
        {
            if (code == 0) return CompatibilityStatus.Compatible;
            // With -nographics this Editor returns code 1 and "Not initialized ()" for standard URP shaders.
            // Missing compiled shader data is inconclusive, not evidence of an incompatible layout.
            if (code < 0 || string.IsNullOrWhiteSpace(reason)
                || reason.IndexOf("not initialized", StringComparison.OrdinalIgnoreCase) >= 0)
                return CompatibilityStatus.Unknown;
            return CompatibilityStatus.Incompatible;
        }

        private static string BuildBatching()
        {
            MethodInfo query = typeof(PlayerSettings).GetMethod("GetBatchingForPlatform", StaticMethods, null,
                new[] { typeof(BuildTarget), typeof(int).MakeByRefType(), typeof(int).MakeByRefType() }, null);
            if (query == null) return "unknown; inspect Player > Other Settings > Static Batching";
            try
            {
                object[] arguments = { EditorUserBuildSettings.activeBuildTarget, 0, 0 };
                query.Invoke(null, arguments);
                return "Static " + arguments[1] + ", Dynamic " + arguments[2] + " (1 enabled, 0 disabled)";
            }
            catch (Exception error) { return "unknown: " + error.GetBaseException().Message; }
        }

        public static string Format(Result data)
        {
            var text = new StringBuilder("\nBatching audit (shader layout and configuration, not measured draw calls):\n\n");
            text.AppendLine("| Check | Observed |\n| --- | --- |");
            text.AppendLine("| Player batching, active build target | " + data.BuildBatching + " |");
            text.AppendLine("| BatchingStatic Renderer components | " + data.Flagged + " |");
            text.AppendLine("| Static-rule targets (renderers plus Terrain) / mismatches | " + data.RuleTargets + " / " + data.RuleMismatches.Count + " |");
            text.AppendLine("| Renderers with a property block now | " + data.PropertyBlocks + " |");
            text.AppendLine("| Configured FlickerLight/PowerStateLighting property-block targets (unique) | " + data.ConfiguredPropertyBlockTargets + " |");
            text.AppendLine("| Compatible / incompatible / unknown subshaders | " + data.CompatibleSubshaders + " / " + data.IncompatibleSubshaders + " / " + data.UnknownSubshaders + " |");
            text.AppendLine("\n| Referenced material shader | Subshader index | SRP layout check |\n| --- | ---: | --- |");
            foreach (string row in data.ShaderRows) text.AppendLine(row);
            foreach (string mismatch in data.RuleMismatches) text.AppendLine("\n- " + mismatch);
            text.AppendLine("\nAll subshaders are listed; this does not identify the pass/variant chosen for a frame. "
                + "Uninitialised shader data is unknown, including in graphics-free batch mode. The shader list covers mesh/Terrain materials; "
                + "skybox and other special passes still need Frame Debugger inspection. "
                + "Property blocks exclude their renderer from SRP batching even with a compatible shader. "
                + "Configured targets may have no block in EditMode; inspect both powered and blackout states in Play mode. "
                + "Static flags and a Player setting do not prove that Unity combined a renderer into a batch. "
                + "Repeated Terrain vegetation uses its own rendering path; these Renderer counts exclude Terrain instances.\n");
            return text.ToString();
        }

        // Two sequential checks, with a fresh full Scene Report after each. Existing settings are retained;
        // toggling a correct setting just to manufacture an improvement would not measure an optimisation.
        public static void AppendAudit()
        {
            SceneReport.WithEnvironment(scene =>
            {
                SceneReport.Snapshot before = SceneReport.Capture(scene);
                SceneReport.Snapshot afterSrp = SceneReport.Capture(scene);
                SceneReport.AppendObservation("SRP Batcher compatibility check (settings retained)",
                    Comparison("SRP shader layout: compatible / incompatible / unknown", ShaderCounts(before.Batching), ShaderCounts(afterSrp.Batching),
                        "Check completed; retained the enabled SRP Batcher. Shader layout alone does not guarantee runtime batching.")
                    + SceneReport.Format(afterSrp));
                SceneReport.Snapshot afterStatic = SceneReport.Capture(scene);
                SceneReport.AppendObservation("static batching check (existing flags retained)",
                    Comparison("BatchingStatic renderers / static-rule mismatches", afterSrp.BatchFlaggedRenderers + " / " + afterSrp.Batching.RuleMismatches.Count,
                        afterStatic.BatchFlaggedRenderers + " / " + afterStatic.Batching.RuleMismatches.Count,
                        "Check completed; retained authored flags. Boat, changing lamps and particles keep their existing rendering behaviour. "
                        + "No scene or Player setting was changed; no draw-call or frame-time improvement is claimed.")
                    + SceneReport.Format(afterStatic));
            });
        }

        private static string ShaderCounts(Result data) => data.CompatibleSubshaders + " / " + data.IncompatibleSubshaders + " / " + data.UnknownSubshaders;
        private static string Comparison(string metric, string before, string after, string why) =>
            "| Check | Before | After | What changed and why |\n| --- | --- | --- | --- |\n| "
            + metric + " | " + before + " | " + after + " | " + why + " |\n\n";
    }
}
