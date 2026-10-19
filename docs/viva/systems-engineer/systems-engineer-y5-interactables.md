# Viva notes: interactables (Systems Engineer, step Y5)

PR #25. Commits 0f9e1d1, dcfa758, 2032391, 8b93059, 54b032d, 8fc6c35, 4c7b6f5 and 7b9bd82. Numbers are in
`docs/reference/systems-engineer.md` section 10.

## What I built

- **Shared:** `IDoor` (`IsOpen`, `IsLocked`, `RequestOpen`) for the agents, and `NoiseType` Alarm and Crate.
- **Interaction:**
  - `Interactable` base, and `HoldTimer` (pure, 5 tests).
  - `PlayerInteractor`: a 3 m camera ray; press or hold E; the prompt and hold ring.
- **Objects:**
  - `SlidingDoor`: speed, noise, lock, maglock on `GeneratorDisabled`, and NavMesh carving while locked.
  - `GeneratorInteractable`: hold E for 2.0 s, then raise `GeneratorDisabled` and a 60 m noise.
  - `CurePickup`: walk-in, raises `CureTaken` and an 18 m alarm.
  - `HealthPickup`: +35 HP, only when hurt.
  - `PushableCrate` with `PlayerPusher`.
- **Tools:**
  - `Blackout/Systems/Build Interactable Prefabs`: 5 prefabs, 9 materials, and the player's interactor and pusher.
  - `Blackout/Systems/Place Interactables`: places the prefabs at the World Builder's socket markers, into **my own
    scene**, `Assets/Scenes/Interactables.unity`. Environment is opened only to read the markers and is closed
    without saving.
- **Tests:** 7 `InteractablesTests` and 5 `HoldTimerTests`.

## Why this design

- **My own scene for the interactables.** The World Builder owns Environment.unity. Placing my objects there would
  mean two people editing one scene file, which can't be merged. The markers are the contract between us.
- **Doors talk to agents through `IDoor`.** The Agent Controller's movement calls `RequestOpen` without knowing my
  class.
- **The generator only raises an event.** The lab door, the blackout lighting (World Builder), the agents'
  blackboard and the mission all react without the generator knowing about any of them.
- **A door carves the NavMesh only while locked.** Closed, unlocked doors stay walkable, so agents can reach them and
  open them.
- **A camera ray for picking**, as the playbook asks: walls block interaction, and only the ray decides.
- **Re-runnable tools.** An existing instance is never moved or replaced, so hand adjustments survive, and empty
  event references are filled without overwriting.

## Alternatives rejected

- **The prototype's scoring of every object in a radius** (`facing × 2 − distance × 0.3`). It's harder to aim at a
  specific object, and it ignores walls.
- **A hinged generator room door, as in the prototype.** Every Unity door gap is 1.4 × 2.3 m (level-layout.md), so
  one sliding panel and one script fit all four doors. A second door kind would mean another prefab, another
  collider rule and another test for one door.

## Bugs found and fixed

- **Channels saved as empty.** `OpenScene(Single)` unloads unused assets, so a channel loaded before opening the scene
  became a dead reference. It is now loaded after the scene opens.
- **E couldn't close an open door.** The panel's collider switches off once the door is open (> 0.8), so the ray hit
  nothing. Fixed with a 1.4 × 2.3 × 0.4 m trigger in the gap. Only the interaction ray hits triggers.

## Numbers

- Generator: hold for 2.0 s, 60 m noise. Cure: 1.05 m pickup radius, 18 m alarm.
- Health: +35 HP within 1.1 m. Doors: speed 1.1 (bunker 0.45), noise 14 m (bunker 22 m).
- Crate: pushed at `min(speed, 4.5) × 0.42` (max 1.9 m/s), slows at 5.5 m/s² when not pushed, and makes an 11 m
  scrape every 0.45 s.
- Place Interactables placed 11 objects on its first run and 0 on its second. The fourth run was byte-identical, and
  Environment.unity's hash was unchanged. EditMode 617/617.

## Known weaknesses

- Not ported: the maglock toast, door and pickup sounds (UnityEvent hooks exist for them), and the crate's tilt and
  dust.
- Unity rewrites the interactor's mask when the Editor shows the prefab (`4294967227 → 4019`, the same meaning).
  The fix is an explicit mask, which is still open.
