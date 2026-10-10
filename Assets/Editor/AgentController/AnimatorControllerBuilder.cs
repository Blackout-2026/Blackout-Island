// Owner: Agent Controller.
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Blackout.Editor
{
    /// <summary>
    /// Builds Assets/Animations/Agent_Humanoid.controller: the Animator Controller AnimatorDriver (A3 commit 3)
    /// drives for every humanoid agent. One layer: a Locomotion blend tree (Idle/Walk/Run on Speed), plus Aim,
    /// HitReact and Death states. Looks for clips at ClipFolder + "&lt;Name&gt;.fbx" (or ".anim"); a clip that is
    /// not there yet leaves that motion slot empty — the controller still builds and plays, just without a pose
    /// for that part. Re-runnable: never removes a hand-assigned clip or duplicates a state/transition, only
    /// fills a slot that is still empty.
    /// Batch: Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod
    /// Blackout.Editor.AnimatorControllerBuilder.Build -logFile -
    /// </summary>
    public static class AnimatorControllerBuilder
    {
        public const string ControllerPath = "Assets/Animations/Agent_Humanoid.controller";
        public const string ClipFolder = "Assets/Animations/Humanoid/";

        // Exact clip names expected (any free humanoid animation pack, e.g. Mixamo). Checked as "<Name>.fbx" and "<Name>.anim".
        public const string IdleClipName = "Idle";
        public const string WalkClipName = "Walk";
        public const string RunClipName = "Run";
        public const string AimIdleClipName = "Aim_Idle";
        public const string HitReactClipName = "HitReact";
        public const string DeathClipName = "Death";

        public const string SpeedParam = "Speed";
        public const string AimingParam = "Aiming";
        public const string HitParam = "Hit";
        public const string DeadParam = "Dead";

        // docs/reference/agent-controller.md section 3.1 "Controller defaults". Per-type speeds (Hunter
        // 1.6/4.5, Guard 1.4/3.5, ...) differ slightly; one shared controller cannot thread that needle, so
        // this is the one shared approximation (documented in docs/viva/agent-controller-animation.md).
        public const float WalkThreshold = 1.5f;
        public const float RunThreshold = 4.0f;

        [MenuItem("Blackout/Agents/Create Humanoid Animator Controller")]
        public static void Build()
        {
            AnimatorController controller = File.Exists(ControllerPath)
                ? AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath)
                : AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);

            EnsureParameter(controller, SpeedParam, AnimatorControllerParameterType.Float);
            EnsureParameter(controller, AimingParam, AnimatorControllerParameterType.Bool);
            EnsureParameter(controller, HitParam, AnimatorControllerParameterType.Trigger);
            EnsureParameter(controller, DeadParam, AnimatorControllerParameterType.Bool);

            AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;

            AnimatorState locomotion = EnsureState(stateMachine, "Locomotion");
            EnsureLocomotionBlendTree(controller, locomotion);

            AnimatorState aim = EnsureState(stateMachine, "Aim");
            EnsureMotion(aim, AimIdleClipName);

            AnimatorState hitReact = EnsureState(stateMachine, "HitReact");
            EnsureMotion(hitReact, HitReactClipName);

            AnimatorState death = EnsureState(stateMachine, "Death");
            EnsureMotion(death, DeathClipName);

            if (stateMachine.defaultState == null)
            {
                stateMachine.defaultState = locomotion;
            }

            EnsureTransition(locomotion, aim, AimingParam, AnimatorConditionMode.If);
            EnsureTransition(aim, locomotion, AimingParam, AnimatorConditionMode.IfNot);
            EnsureAnyStateTransition(stateMachine, hitReact, HitParam);
            EnsureExitTransition(hitReact, locomotion, 0.9f);
            EnsureAnyStateTransition(stateMachine, death, DeadParam);

            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();

            Debug.Log($"AnimatorControllerBuilder: {ControllerPath} is ready (parameters Speed/Aiming/Hit/Dead; states Locomotion/Aim/HitReact/Death).");
            ReportMissingClips();
        }

        private static void EnsureParameter(AnimatorController controller, string paramName, AnimatorControllerParameterType type)
        {
            foreach (AnimatorControllerParameter p in controller.parameters)
            {
                if (p.name == paramName)
                {
                    return;
                }
            }

            controller.AddParameter(paramName, type);
        }

        private static AnimatorState EnsureState(AnimatorStateMachine stateMachine, string stateName)
        {
            foreach (ChildAnimatorState child in stateMachine.states)
            {
                if (child.state.name == stateName)
                {
                    return child.state;
                }
            }

            return stateMachine.AddState(stateName);
        }

        private static void EnsureLocomotionBlendTree(AnimatorController controller, AnimatorState locomotion)
        {
            BlendTree tree = locomotion.motion as BlendTree;
            if (tree == null)
            {
                tree = new BlendTree
                {
                    name = "Locomotion Blend",
                    blendType = BlendTreeType.Simple1D,
                    blendParameter = SpeedParam,
                    useAutomaticThresholds = false
                };
                AssetDatabase.AddObjectToAsset(tree, controller);
                locomotion.motion = tree;
            }

            EnsureBlendTreeChild(tree, 0f, IdleClipName);
            EnsureBlendTreeChild(tree, WalkThreshold, WalkClipName);
            EnsureBlendTreeChild(tree, RunThreshold, RunClipName);
        }

        private static void EnsureBlendTreeChild(BlendTree tree, float threshold, string clipName)
        {
            ChildMotion[] children = tree.children;
            for (int i = 0; i < children.Length; i++)
            {
                if (Mathf.Approximately(children[i].threshold, threshold))
                {
                    if (children[i].motion == null)
                    {
                        Motion clip = LoadClip(clipName);
                        if (clip != null)
                        {
                            children[i].motion = clip;
                            tree.children = children; // ChildMotion is a struct: write the whole array back
                        }
                    }

                    return;
                }
            }

            // Not present yet: add it, with whatever clip is currently available (possibly none).
            tree.AddChild(LoadClip(clipName), threshold);
        }

        private static void EnsureMotion(AnimatorState state, string clipName)
        {
            if (state.motion != null)
            {
                return; // never overwrite a hand-assigned clip, or one a previous run already found
            }

            Motion clip = LoadClip(clipName);
            if (clip != null)
            {
                state.motion = clip;
            }
        }

        private static Motion LoadClip(string clipName)
        {
            var fromFbx = AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".fbx");
            if (fromFbx != null)
            {
                return fromFbx;
            }

            return AssetDatabase.LoadAssetAtPath<AnimationClip>(ClipFolder + clipName + ".anim");
        }

        private static void EnsureTransition(AnimatorState from, AnimatorState to, string paramName, AnimatorConditionMode mode)
        {
            foreach (AnimatorStateTransition t in from.transitions)
            {
                if (t.destinationState == to)
                {
                    return;
                }
            }

            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.15f;
            transition.AddCondition(mode, 0f, paramName);
        }

        private static void EnsureAnyStateTransition(AnimatorStateMachine stateMachine, AnimatorState to, string paramName)
        {
            foreach (AnimatorStateTransition t in stateMachine.anyStateTransitions)
            {
                if (t.destinationState == to)
                {
                    return;
                }
            }

            AnimatorStateTransition transition = stateMachine.AddAnyStateTransition(to);
            transition.hasExitTime = false;
            transition.duration = 0.1f;
            transition.canTransitionToSelf = false;
            transition.AddCondition(AnimatorConditionMode.If, 0f, paramName);
        }

        private static void EnsureExitTransition(AnimatorState from, AnimatorState to, float exitTime)
        {
            foreach (AnimatorStateTransition t in from.transitions)
            {
                if (t.destinationState == to && t.hasExitTime)
                {
                    return;
                }
            }

            AnimatorStateTransition transition = from.AddTransition(to);
            transition.hasExitTime = true;
            transition.exitTime = exitTime;
            transition.duration = 0.15f;
        }

        private static void ReportMissingClips()
        {
            string[] clipNames = { IdleClipName, WalkClipName, RunClipName, AimIdleClipName, HitReactClipName, DeathClipName };
            foreach (string clipName in clipNames)
            {
                if (LoadClip(clipName) == null)
                {
                    Debug.LogWarning($"AnimatorControllerBuilder: no clip found for '{clipName}' at {ClipFolder}{clipName}.fbx (or .anim). Add it, then run Blackout/Agents/Create Humanoid Animator Controller again to wire it in.");
                }
            }
        }
    }
}
