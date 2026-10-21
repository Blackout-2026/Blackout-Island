# Enemy population and lab clearance (E1)

The scene now has 24 agents. `MarkerLayout` defines their spawns and owned
waypoints; `AgentSpawner` uses the existing four prefabs and difficulty asset.
The FSM, behaviour tree, utility AI and GOAP implementations remain distinct.

| Area | Hunters | Security Patrol | Tactical Guards | Commander |
| --- | --- | --- | --- | --- |
| Forest | 5 | 0 | 0 | 0 |
| Compound outside bunker | 0 | 3 | 0 | 0 |
| Security office / corridor entrance | 0 | 2 | 1 | 0 |
| Corridor | 0 | 2 | 1 | 0 |
| Generator room | 0 | 2 | 2 | 0 |
| Laboratory | 0 | 3 | 2 | 1 |

The first bunker room means the security office. Patrol-1/4/5 are outside,
Patrol-2/6 guard the security office's corridor entrance, Patrol-3/9 patrol the
corridor, Patrol-7/8 the generator room, and Patrol-10/11/12 the lab. TG-01/03
guard the generator, TG-02/04 the lab, TG-05 the corridor and TG-06 the office.
Hunters 3–5 begin at different points on the existing cleared forest routes.
Coordinates use metres, +Y up, +Z north; prototype `(x,y,z)` maps to `(x,y,-z)`.

Dead agents return to their original marker after **20 seconds**, at full health
with fresh brain state and the current world alert. This is a chosen starting
value: long enough to finish nearby combat and collect a weapon, without
allowing the route to stay permanently empty. It still needs gameplay tuning.
The spawner keeps one slot per marker and replaces that slot, so the roster
does not grow on repeated deaths. Dead bodies stop blocking movement and shots.
Enemy weapon drops continue through the existing death component.

Taking the cure stops respawning the six lab defenders, including the Commander.
Defenders already dead at pickup count as cleared, and pending respawns are
cancelled. A living lab defender that chased outside returns to its original
post at pickup so it cannot be trapped beyond the locked exit. Membership is
based on the original spawn, not the current position. Other enemies keep
respawning after pickup.

The existing lab door closes and gains a separate combat lock until every lab
defender is dead. Its prompt says to eliminate the remaining lab defenders.
`IDoorCombatLock` is an optional shared interface; existing `IDoor` consumers
need no changes. Runtime lookup connects the additive scenes without persistent
Inspector references. The generator's delayed power unlock cannot release the
combat lock. Clearance opens the door automatically. Cure pickup also sets
`Checkpoint_EscapeLab` at **(0, 2.5, 127)** so death cannot put the player outside
the locked lab. Later escape checkpoints still advance toward the boat.
Restart reloads the scenes and restores the full roster and mission state.

Run **Blackout > World > Update Enemy Population** to update the managed agent
markers and the new checkpoint. It preserves other markers, room geometry,
lighting, art and manually authored objects outside those managed groups.
The general bunker marker builder uses the same population data.

Automated checks cover the requested distribution, saved marker integration,
NavMesh proximity and solid spawn clearance; timed replacements and post-cure
exceptions; power/combat lock independence; and the lab checkpoint rules.
The PlayMode encounter test loads Environment, Interactables and Agents and
uses the real event channels, death callbacks and replacement registration.

Verified on 2026-10-21 in Unity 6000.3.25f1: import/build and repeat build exited
0; Environment's SHA-256 was identical after both runs. Focused EditMode:
345 passed, 0 failed. PlayMode: 2 passed, 0 failed, including the encounter
lifecycle test and the 15-second smoke test. The smoke run registered all 24
agents and recorded 0 exceptions, error logs, warnings or stuck events. These
are selected tests, not a whole-project pass. Logs and XML are in the system
temporary directory as `blackout-E1-build.log`, `blackout-E1-repeat-build.log`,
`blackout-E1-editmode.{log,xml}` and `blackout-E1-playmode.{log,xml}`; the smoke
report is `blackout-agent-stress-20261021-130723.txt`. Rendering was disabled.

Before committing, play from Bootstrap: count agents by area, fight a Hunter
and wait 20 seconds for its return, disable the generator, take the cure,
confirm the lab door blocks escape until all surviving defenders are dead,
die during the fight and check the lab respawn, then finish the escape and
restart. Headless checks cannot judge crowding, difficulty, animation or visuals.

Viva: one marker slot owns each replacement; respawning belongs in the shared
spawner rather than four brains. Reviving an existing faded corpse was rejected
because animation, sensing, claims and directives would all need separate resets.
Twenty seconds is a tuning value. The main weakness is that more agents and
repeated respawns may make narrow rooms crowded; the full route needs playtesting.
