# Checkpoint recovery and the cure wave (R1)

The solo design now replaces E1's repeating enemy respawns and Y4's teleport-only
recovery. Dead enemies stay dead while travelling through the island. Taking
the cure brings back fallen enemies **outside the lab once**, at their original
spawn markers with full health. Survivors keep their positions and health.
The six original lab defenders remain excluded: prior deaths count as cleared,
and surviving defenders must be defeated to release the lab exit. Time, repeated
cure notifications and disabling/re-enabling the spawner never grant another wave.

`CheckpointTracker` adds one `CheckpointWorld` component to the live player.
No scene or prefab rebuild is needed. The existing mission phase/escape markers
still choose checkpoint locations. Whenever `SetCheckpoint` runs, it requests
one in-memory world save for that checkpoint. A later checkpoint replaces it.
The initial player start is also saved for a scene run without MissionDirector.

The save occurs in LateUpdate of the checkpoint frame, after synchronous
generator/cure listeners have completed. This matters when cure pickup both
creates the escape wave and activates `Checkpoint_EscapeLab`: its save must
contain the completed wave, the taken cure and the lab combat lock. Player
health is sampled at the checkpoint request so a fatal hit later in that frame
cannot save a dead player.

| System | Saved gameplay state |
| --- | --- |
| Player | Health and invulnerability; weapon ownership, active/pending switch, cooldown, gun bloom and tracer shot counter |
| Mission | Phase, objective, checkpoint ID, generator/cure flags, door progress flag, kills, deaths and elapsed mission time |
| Enemies | Each original slot's alive/dead state, exact health, position and facing; whether the cure wave was used |
| Squad | World flags, alert/buff, last sighting with its age, squad readiness and existing orders |
| Doors | Both leaves' open fraction/target, power/combat locks and remaining unlock delay |
| Generator | Console state, core material/hum state, blackout progress, screens, fixtures, emergency lights and beacons |
| Items | Cure and medkit availability; collected/uncollected weapon drops that existed at the checkpoint |
| Crate | Rigidbody position, rotation, linear/angular velocity and remaining push/scrape timing |
| HUD/effects | Saved alert state; clear failed-attempt toast, tracers, impact particles and viewmodel kick |

After the existing **2.2 s** death delay, the player teleports to the checkpoint.
PlayerRespawned listeners first forget the failed attempt; recovery then restores
the saved state so those callbacks cannot erase it. The player then receives
**full health (100 HP with the current configuration)** and the existing
**1.5 s** safety invulnerability. A checkpoint reached at 75 HP therefore
respawns the player at 100 HP. Enemies still restore their saved health.

Each owner implements the small `Blackout.Shared.ICheckpointState` contract.
CheckpointWorld discovers these components across the loaded additive scenes,
including inactive weapon slots and collected drops. It restores world components
first, then the `ICheckpointRoster` spawner. The roster owns its instances;
individual components under an agent are excluded from the world list.
Existing assembly boundaries remain, with no runtime reference from player
code to the Agents, Environment or Art assemblies.

The spawner unregisters old instances before replacing them. Living saved slots
receive fresh brain/executor instances and saved health/pose; saved dead slots
stay inactive, without death callbacks or another loot drop. Saved orders and
world knowledge are restored after initialization. Enemy AI makes fresh decisions
from that restored situation: private FSM/BT/utility/GOAP execution timers,
animation frames, random sequences and corpse fade frames are not serialized.
The four AI architectures are unchanged.

Weapon drops deactivate when taken so an existing save can restore them.
Drops created after the save are discarded on recovery. A checkpoint snapshot
is not mutated during restoration, so the same save supports repeated deaths.
There is no disk save or persistence between restarts. Restart still reloads
Bootstrap and starts a new mission; it does not restore the previous run.
The active Environment scene remains loaded throughout checkpoint recovery.

Verification uses the existing spawner, mission, health and interaction tests,
plus `CheckpointWorldPlayTests` with the saved Environment/Interactables/Agents
scenes and player prefab. Those tests call the real generator, cure, pickups,
damage callbacks and delayed player respawn, and exercise repeated recovery.
They bypass the unrelated cinematic introduction and freeze enemy movement/fire
to make state comparisons reliable. Separate encounter/smoke tests exercise AI.

Verified on 2026-10-21 with Unity 6000.3.25f1: import/compilation exit **0**;
selected EditMode **495/495**, PlayMode **13/13**, both exit **0**. After adding
the newer outdoor checkpoint case, the final checkpoint-only run passed **2/2**,
exit **0**, covering five real deaths across the two scenarios. The first
expanded assertion checked the door before its saved power delay plus sliding
time had finished; the corrected check waits 4 s (2.6 s delay plus about 0.9 s
opening). No runtime change was needed for that test correction.
The 24-agent smoke recorded zero exceptions, error logs, warnings or stuck
events. Evidence is in system Temp: `blackout-R1-import.log`,
`blackout-R1-editmode.{log,xml}`, `blackout-R1-playmode.{log,xml}`,
`blackout-R1-checkpoint-final.{log,xml}` and
`blackout-agent-stress-20261021-174328.txt`. Logs retain licensing-token
diagnostics and existing fixture warnings; no final test failures or compiler
warnings/errors. These selected headless checks do not prove full gameplay,
visual quality, cinematic integration or an entire-project test pass.

Full-health follow-up: the two checkpoint PlayMode tests passed again (**2/2**,
Unity exit **0**), including repeated recovery from a checkpoint saved at 75 HP
to full player health, with saved enemy health and world state preserved.
Evidence: system Temp `blackout-R1-full-health-playmode.{log,xml}`.

Manual checks in Bootstrap:

1. Kill an enemy before taking the cure. Wait over 20 s; it must stay dead.
2. Reach the generator checkpoint with a wounded player, a missing gun and a
   fallen enemy. Then take a gun/medkit, push the crate, disable the generator
   and die. Check full player health, saved loadout, pickups, crate, doors, powered lighting,
   objective and enemy health/positions return. Repeat without advancing.
3. Take the cure. Fallen enemies outside the lab return once; dead lab defenders
   stay dead. Kill a returned enemy and wait: it must not return again.
4. Kill some lab defenders, die, and verify the cure checkpoint restores its
   surviving defenders and exit lock. Clear the lab and advance to an outdoor
   escape checkpoint; another death must keep the cleared lab cleared.
5. Finish the escape, restart, and check the initial full roster, available cure,
   powered generator, starting loadout progression and Console.

Viva: a checkpoint is a saved gameplay situation shared through a small contract.
Teleport-only recovery was rejected because it allowed failed-attempt changes
to leak into retries. One cure wave refreshes the escape route without endless
respawns. A known limit is fresh AI decisions rather than frame-exact brain replay.
