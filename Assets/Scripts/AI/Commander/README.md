# Commander (G1-G4)

Owner: Agent Controller. Runtime assembly: `Blackout.AI.Commander`.

## Setup

Run **Blackout > Agents > Install Supplied Commander Model** after the new FBX
imports. This puts the supplied rigged character and its pistol on the Commander
prefab, creates URP materials and an animation controller from the FBX's Idle,
Walk, Run, Aim and Shoot actions, and sets the Commander to 180 health. The
editable Blender source is `Art/Blender/Commander_Hero_Source.blend`.

Run **Blackout > Agents > Set Up Commander (G2)**. The tool creates
`Config/CommanderConfig.asset`, assigns `CommanderBrain` to the Commander prefab's
`AgentRoot`, and connects the existing PlayerDied and PlayerRespawned channels in
`Assets/Scenes/Agents.unity`. Existing config tuning is preserved. Re-running the
tool does not change already configured assets or save other scenes.

G4 also assigns CommanderDirectiveGizmos on the prefab. The additional menu
**Blackout > Agents > Set Up Commander Debug (G4)** runs the same setup safely.

The scene's existing BlackboardHolder listens for CureTaken. It sets the fact and
notifies the living Commander immediately, without waiting for its 10 Hz think
tick. The Commander broadcasts the configured alert and replans. Disabling or
destroying the brain removes its event subscription. Death prevents further
orders and leaves every surviving agent's last directive intact.

## Facts and execution

The six keys are `playerLocated`, `cureTaken`, `generatorDisabled`, `agentsAlive`
(integer, excluding the Commander), `squadAssembled`, and `playerNeutralized`.
Blackboard registration supplies cached transforms, health and receivers. The
registry removes dead agents. Debug state strings are not used to infer health.

The Commander monitors until cureTaken or an existing alert. It selects
PlayerLocated when the sighting is missing or at least 6 s old, otherwise
PlayerNeutralized. The config supplies all action preconditions, predicted
effects and costs. Predicted effects never fabricate sightings or player death.
The player lifecycle channels supply actual neutralization and respawn facts.

- Search: cost 3, ring radius 7 m, search radius 9 m, orders every 8 s, timeout 24 s.
- Regroup: cost 2, midpoint rally, radius 10 m, at least 2 survivors. Completes
  only when two living squad members actually reach the rally. After 12 s without
  those arrivals it fails and retries after the configured interval.
- Converge: cost 2, orders every 1 s until the player dies or the sighting expires.

After a failed action, retry is delayed by the configured search interval. Changes
to observed facts replan immediately on the next think tick; CureTaken replans
synchronously. When he sees the player, the Commander selects nearby hidden
CoverPoint markers, runs to cover, waits, peeks, fires and returns to cover. Below
45% health, he hides longer. Without usable cover he approaches pistol range and
fires directly. The shared AgentShooter applies 10 damage, a 22 m range, spread
and reaction delay. GOAP squad orders continue during his own combat. Stable
ticks reuse fact storage and allocate no managed memory;
planning and debug-text requests may allocate.

## Alert and other owners' integration

AgentRoot applies the default x1.25 movement multiplier to PathFollower and x0.9
aim-error multiplier to AgentShooter. These are absolute assignments, so repeated
alerts do not stack. Newly registered agents inherit the active alert.

SecurityPatrol, Hunter and TacticalGuard implement the shared directive receiver;
SecurityPatrol and Hunter also implement the alert receiver. AgentRoot forwards
directives to their brains, and the scene integration test checks the 23 real
recipients and NavMesh movement. Blackboard retains each accepted order so a
replacement brain can replay it. TacticalGuard handles its shared alert behavior
through AgentRoot's movement and aim modifiers.

## Editor checks

Load the mission with Agents. During Play mode, raise the existing CureTaken
event asset through its context menu. Inspect CommanderBrain through the shared
debug text and confirm that its plan changes immediately. Check living units'
PathFollower.SpeedMultiplier (1.25) and AgentShooter heightened state. Kill the
Commander and confirm no new directives. Raise PlayerDied then PlayerRespawned
to check goal completion and stale-sighting reset. The integration tests exercise
delivery and the 1.4 vision multiplier with receiver doubles; they do not claim
the other owners' actual brains already implement those behaviors.

## G4 diagnostics and viva

IDebugInfo now reports the current goal and state, the stored plan with action
costs and active-step brackets, total/remaining cost, observed world facts and
current Blackboard values. Formatting happens on diagnostic requests. AgentRoot
publishes it on the existing state interval; stable think ticks do not format it.
Published agent summaries use only their first line to avoid nested reports.

In Play mode, enable Scene view Gizmos. CommanderDirectiveGizmos draws one
visualization per living recipient's latest retained directive: cyan Converge,
yellow SearchZone, magenta Regroup. Regroup also has a square center marker.
The renderer reads the live Blackboard without issuing orders or moving agents.
Retained orders remain visible after Commander death while its object exists.

See [Commander coordination and viva notes](../../../../docs/viva/commander.md)
for owner mappings, GOAP/FSM and Blackboard rationale, the worked cost-4 plan,
the regroup-timeout edge case, test coverage and the Editor demonstration.
For a short live presentation sequence, see [Commander demo](../../../../docs/viva/commander-demo.md).
