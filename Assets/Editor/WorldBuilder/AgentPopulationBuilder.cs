// Owner: World Builder. Solo encounter population step E1.
using System;
using Blackout.Shared;
using Blackout.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Blackout.Editor
{
    /// <summary>Updates only the managed agent markers and adds the lab escape checkpoint.</summary>
    public static class AgentPopulationBuilder
    {
        [MenuItem("Blackout/World/Update Enemy Population")]
        public static void Build()
        {
            const string path = "Assets/Scenes/Environment.unity";
            Scene scene = SceneManager.GetSceneByPath(path);
            if (scene.isLoaded && scene.isDirty)
                throw new InvalidOperationException("Save Environment before updating enemy markers.");
            bool opened = !scene.isLoaded;
            if (opened) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
            try
            {
                Transform root = null;
                foreach (GameObject obj in scene.GetRootGameObjects())
                    if (obj.name == "Markers") root = obj.transform;
                if (root == null) throw new InvalidOperationException("Environment has no Markers root.");
                var layout = AssetDatabase.LoadAssetAtPath<BunkerLayout>("Assets/ScriptableObjects/Layout/BunkerLayout.asset");
                string result = MarkerBuilder.Build(root, layout, agentsOnly: true);
                foreach (MarkerSpec spec in MarkerLayout.Build(layout))
                {
                    if (spec.Name != "Checkpoint_EscapeLab") continue;
                    Transform group = root.Find("Checkpoint");
                    if (group == null) throw new InvalidOperationException("Markers has no Checkpoint group.");
                    Transform point = group.Find("EscapeLab");
                    if (point == null)
                    {
                        var obj = new GameObject("EscapeLab");
                        obj.transform.SetParent(group, false);
                        point = obj.transform;
                    }
                    point.SetPositionAndRotation(spec.Position, Quaternion.Euler(0f, spec.Yaw, 0f));
                    MarkerBuilder.ApplyMarker(point.gameObject, spec);
                }
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[E1] Updated enemy population: {result}; lab escape checkpoint at (0, 2.5, 127).");
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(scene, true);
            }
        }
    }
}
