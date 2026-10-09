// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
namespace Blackout.Shared
{
    /// <summary>Implemented by every brain so the shared F1 debug overlay can show its state.</summary>
    public interface IDebugInfo
    {
        string GetDebugText();
    }
}
