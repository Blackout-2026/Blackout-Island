# Commander (G1-G4)

Owner: Agent Controller. Runtime assembly: `Blackout.AI.Commander`.

## Setup

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
- Regroup: cost 2, midpoint rally, radius 10 m, at least 2 survivors. As in the
  reference, completes when two arrive OR after 12 s with two surviving agents.
- Converge: cost 2, orders every 1 s until the player dies or the sighting expires.

After a failed action, retry is delayed by the configured search interval. Changes
to observed facts replan immediately on the next think tick; CureTaken replans
synchronously. The Commander holds position, looks toward known information and
never aims or fires. Stable ticks reuse fact storage and allocate no managed memory;
planning and debug-text requests may allocate.

## Alert and other owners' integration

AgentRoot applies the default x1.25 movement multiplier to PathFollower and x0.9
aim-error multiplier to AgentShooter. These are absolute assignments, so repeated
alerts do not stack. Newly registered agents inherit the active alert.

**Handoff to the World Builder (SecurityPatrol), Systems Engineer (Hunter), and
Core Developer (TacticalGuard):** implement `Blackout.Shared.IDirectiveReceiver`
on your brain and map Converge, SearchZone and Regroup to your architecture's states.
Also implement `Blackout.Shared.IAlertReceiver.OnAlert(AgentAlert)` and use
`alert.visionMultiplier` (default 1.4) against the unmodified base sight range.
AgentRoot forwards the alert after initialization and forwards a retained directive
when replacing a brain. Blackboard.Alert also exposes the active profile.

At G2 implementation time, none of the other three AI folders implements either
receiver interface. Orders are retained in Blackboard, but those brains still need
their owners' receiver implementations to act on them and extend their vision.
No files in their owned paths were changed. The current humanoid visuals remain
the clearly named placeholders from the existing agent setup.

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
the regroup-timeout weakness, test coverage and the Editor demonstration.
