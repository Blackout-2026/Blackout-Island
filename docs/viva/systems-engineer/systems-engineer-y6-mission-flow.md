# Viva notes: mission flow (Systems Engineer, step Y6)

PR #37. Commits aaf47c1, 81b9446, a079a99, e3c4f76 and 4ffdf47. Numbers are in `docs/reference/systems-engineer.md`
section 11.

## What I built

- **`MissionStateMachine`** (plain C#, 24 tests):
  - The phases: Beach → Forest → BunkerEntrance → Corridor → GeneratorRoom → Lab → Escape → Complete.
  - `Tick(position)` reads zone boxes from `docs/level-layout.md` (`MissionZones`). `GeneratorDisabled` moves the
    mission to Lab, `CureTaken` moves it to Escape, and the boat zone completes it.
  - Each call returns `MissionChange` flags (Phase, Objective, Checkpoint).
- **`MissionDirector`:**
  - It feeds the state machine and raises `ObjectiveChanged` with the prototype's texts.
  - It sets the checkpoint for each phase; during the escape the checkpoints follow the route back out.
  - It counts kills (`HitConfirmed`) and deaths (`PlayerDied`).
  - On the win it raises `MissionComplete`, stops input, frees the cursor and sets `timeScale` to 0.
- **`MissionCompleteScreen`:** TIME, KILLS, DEATHS and RESTART (reloads Bootstrap).
- **`SceneBootstrap` + `Bootstrap.unity`** (built by `Blackout/Systems/Build Bootstrap Scene`):
  - It loads Environment, Interactables and Agents additively and skips a scene that isn't in Build Settings.
  - It makes Environment the active scene, then places the inactive player at `PlayerStart` and starts the mission.
- **`Blackout/Debug/Mission/Jump to …`:** eight Play-mode items. Jumping to Lab or later turns the generator off
  through the real object, and Escape or later takes the cure, so every listener reacts as in play.

## Why this design

- **Rules without Unity objects.** The whole mission is tested in EditMode, including a full run that checks all
  eight phases come in order. No scene is needed.
- **Change flags.** The director raises events only when something changed, so there's nothing per frame and no
  allocation.
- **Phases only move forward.** A player walking back to the beach doesn't undo progress, as in the prototype. Debug
  jumps are the only way back, and they keep the generator and cure state.
- **Events, not references.** The director never references the generator or the cure; it listens to the same
  channels as everyone else.
- **One Bootstrap scene.** Each role keeps its own scene. Environment must be the active scene because Unity takes fog,
  skybox and ambient light from the active scene.
- **Markers found at runtime** by id, because the markers live in another scene.

## Alternatives rejected

- **Trigger colliders per zone.** They'd have to go in the World Builder's scene, and they fire on entering, not on
  being inside. Boxes in code are tested, and they're copied from the agreed layout table.
- **One scene containing everything.** All four roles would edit one file.
- **A coroutine script for the whole mission.** It can't be tested or jumped through.

## Numbers

- **Win:** horizontal distance from `BoatExit` < 6 m and y > 0.5, with the cure.
- **Escape checkpoints:** EscapeEntrance once back in the compound, then EscapeForest once outside it with z < 26.
- **Tests:** MissionStateMachineTests 24/24 and EditMode 696/696.
- **Build Bootstrap Scene:** run 3 was byte-identical to run 2. All 14 references were wired.

## Known weaknesses

- KILLS shows a count only, not "kills / total": there's no "agent killed" channel or agent count in Shared.
- The zones are boxes copied from the layout. If a room moves, `MissionZones` has to change as well.
- Not ported: the start screen, the pause menu, phase-change toasts and the compass.
- Agents.unity didn't exist yet. Y7 added it to Build Settings.
