using Blackout.Takedowns;
using UnityEditor;

namespace Blackout.Editor
{
    [CustomEditor(typeof(TakedownProfile))]
    public sealed class TakedownProfileInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            string missing = ((TakedownProfile)target).MissingRequirement;
            EditorGUILayout.HelpBox(missing ?? "Clip/event configuration is ready. Paired motion and contact still require visual review.",
                missing == null ? MessageType.Info : MessageType.Warning);
        }
    }

    [CustomEditor(typeof(TakedownController))]
    public sealed class TakedownControllerInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            var controller = (TakedownController)target;
            EditorGUILayout.HelpBox(controller.SetupStatus, controller.SetupStatus == "Ready" ? MessageType.Info : MessageType.Warning);
            string front = controller.FrontalProfile != null ? controller.FrontalProfile.MissingRequirement : "Assign the frontal profile.";
            EditorGUILayout.HelpBox("Frontal counter: " + (front ?? "Ready"), front == null ? MessageType.Info : MessageType.Warning);
            if (UnityEngine.Application.isPlaying)
            {
                EditorGUILayout.LabelField("Phase", controller.State.ToString());
                EditorGUILayout.LabelField("Style / kicks / stabs", $"{controller.Style} / {controller.KickCount} / {controller.ImpactCount}");
                EditorGUILayout.LabelField("Last attempt");
                EditorGUILayout.HelpBox(controller.LastResult ?? "No attack attempt yet.", MessageType.None);
            }
        }
    }
}
