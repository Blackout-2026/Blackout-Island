// Owner: Agent Controller. Shared contract (docs/UNITY_BUILD_PLAYBOOK.md section 3): extend it, never break it.
// Created by the Systems Engineer in Y1; only the Agent Controller edits it from now on.
namespace Blackout.Shared
{
    /// <summary>
    /// The decision layer of an agent (FSM, behaviour tree, utility or GOAP). It decides and returns an intent;
    /// it never touches the Transform, the NavMesh or the Animator.
    /// </summary>
    public interface IAgentBrain
    {
        void Initialize(IAgentContext context);
        AgentIntent Tick(float deltaTime);
    }
}
