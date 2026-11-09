# Commander demo for the assignment

## Prepare once

1. Open the project in Unity and wait for imports and script compilation.
2. The supplied Commander model is already installed. If you reimport its FBX or
   change its Blender source, choose **Blackout > Agents > Install Supplied Commander Model**
   to rebuild the visual prefab and animation controller.
3. Open `Assets/Prefabs/Agents/Commander.prefab` to show the new model, pistol,
   `CommanderBrain`, `AgentShooter` and `Health`. The editable source is
   `Art/Blender/Commander_Hero_Source.blend`.

## Play and show behavior

1. Open `Assets/Scenes/Bootstrap.unity` and press Play. Go to the laboratory and
   approach the Commander. Select **Commander** in Hierarchy. In Inspector show
   `Health` (180 maximum) and `AgentShooter` (pistol: 10 damage, 22 m range).
   These values also appear on the live spawned Commander: `AgentSpawner` applies
   `Assets/ScriptableObjects/Agents/DifficultyConfig.asset` at spawn.
2. With Scene view Gizmos on, watch the Commander run to lab **CoverPoint_14**,
   wait behind the analyser, step aside, shoot, and retreat. The tested player
   position `(2, 2.56, 138)` in the lab gives him a clear approach to that cover.
   In Game view the pistol animates and the player loses health when shots hit.
   Where no usable cover exists, he approaches pistol range and shoots. If his
   eyes can see the player but the pistol is blocked by furniture, he shifts
   sideways to find a firing lane before shooting.
3. On the selected `CommanderBrain` component menu choose **Log Commander Plan**.
   Console shows the combat phase, selected cover, current health, GOAP goal,
   active action and fact values. The combat phases are `CombatMovingToCover`,
   `CombatHiding`, `CombatMovingToPeek`, `CombatShooting` and `CombatFlanking`.
4. Trigger the mission's CureTaken event by collecting the cure. The Commander
   broadcasts an alert and plans squad orders while he still fights. Use Scene
   Gizmos to show yellow search areas, magenta regroup targets and cyan converge
   destinations. Log his plan again to explain the new facts and action costs.
5. Damage him below 81 health (45% of 180): he waits behind cover for 2.5 s
   instead of 1.6 s. Kill him and show that no new orders are issued; living
   squad members retain their last orders.

## Explain the architecture

The Commander uses **GOAP** to choose costed squad orders from observed world
facts. Search, regroup and converge actions have preconditions and effects. He
replans when those facts change. His own combat uses a cover selection and
peek/fire/retreat state cycle, with line-of-sight checks and a time-limited player
memory. `AgentRoot` executes the resulting `AgentIntent` through `PathFollower`,
`AnimatorDriver` and `AgentShooter`. This is more than uninformed search: the
agent evaluates goals, costs, current facts and local cover before acting.

## Tests to present

In **Window > General > Test Runner > EditMode**, run
`Blackout.AI.Commander.Tests`. Useful cases are
`VisiblePlayer_UsesPistolIntentWithoutCover`,
`CoverCycle_MovesHidesPeeksAndOnlyThenShoots`,
`EyeCanSeeButPistolIsBlocked_RepositionsInsteadOfFiring`,
`LostPlayer_StopsFireAfterCombatMemory`, and the existing GOAP regroup, stale
sighting and death tests. Also run `CommanderHeroCombatTests` and
`AgentSpawnerTests` in EditMode to show prefab damage and live spawn stats.
The logic tests use an injectable sight query. A live Bootstrap Play run also
confirmed the Commander selected `CoverPoint_14`, hid, peeked, fired, and
returned to cover; a clear-line run landed 11 shots for 110 damage.
