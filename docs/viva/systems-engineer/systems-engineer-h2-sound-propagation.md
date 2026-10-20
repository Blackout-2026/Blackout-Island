# Viva notes: sound propagation (Systems Engineer, Hunter step H2)

Branch `feature/ht-h2-sound-propagation`. Commits 502cb84, 12d78fd, 2cf7f87, b9dd0f7 and bdaed22. The prototype's model, the
Unity mapping and the noise radii are in `docs/reference/systems-engineer.md` section 14. Every constant is documented
in `HearingConfig` (class comment and tooltips).

## What I built

All in `Assets/Scripts/AI/Hunter/Hearing`, assembly `Blackout.AI.Hunter`.

- **`HearingConfig`** (ScriptableObject, asset in `Assets/Scripts/AI/Hunter/Config`): the formula and every constant,
  with where each value comes from.
- **`HearingMath`:** the formula as pure functions, plus the decay and the farthest heard distance.
- **`NoiseMemory`:** the strongest recent noise, its type, position and time, fading to 0 over 8 s.
- **`HearingSystem`** (component on the Hunter): listens to the shared `Noise` channel, ignores the Hunter's own
  noises, counts walls, and exposes `PerceivedLoudness`, `NoiseDirection` and `TimeSinceNoise` for the brain (H3).
- **`RaycastOcclusionCounter`:** one non-allocating raycast from 1.2 m above the noise to the Hunter's ears (1.6 m),
  against Level and Interactable; triggers ignored, terrain skipped, at most 3 walls.
- **`Blackout/Systems/Build Hunter Config`:** creates the asset once and never overwrites it.

## The formula

```
perceived = max(0, 1 − d / r) × 0.35 ^ walls × 1.7      heard if perceived > 0.04
current   = perceived × max(0, 1 − age / 8 s)
```

- `r` is the noise's radius (sprint 13 m, SMG 42 m, generator 60 m), `d` the distance between the feet.
- **0.35 per wall:** one wall cuts a noise to about a third, two to an eighth. The prototype's value.
- **× 1.7:** the Hunter's hearing (Security Patrol 1.0, Tactical Guard and Commander 0.9). Its speciality.
- **0.04:** the faintest sound that counts, so a noise is heard up to 98 % of its radius in the open.
- **3 walls max:** a fourth wall would only matter within 0.45 r; it saves raycast hits.
- **8 s:** how long the prototype keeps a noise before forgetting it; here the memory fades to 0 over that time.

## Why this design

- **Sound, not a detection radius.** The Hunter reacts to what the player does: sprinting (13 m) is heard where
  walking (4.5 m) is not, and gunfire carries through the bunker. A fixed radius would ignore both the player's
  choices and the walls.
- **Walls by raycast, as the brief asks.** One ray per noise, only for noises that could matter, against the layers
  that really are walls since Y7 (Level and Interactable).
- **Ignoring the terrain**, as the prototype does: a hill between the Hunter and a gunshot does not muffle it.
- **Pure functions and a plain memory class.** The formula and the decay are tested in EditMode without a scene; the
  component only wires them to the channel and the physics.
- **Events, not references.** The Hunter never references the player, the weapons or the doors; it listens to the
  same `Noise` channel they all publish to (Y1).
- **The raycast is skipped** when a noise could not be heard, or could not beat the memory, even with no walls.

## Alternatives rejected

- **A fixed hearing radius per noise type:** ignores walls and the Hunter's speciality.
- **The prototype's path flood** (Dijkstra over the nav grid so sound bends round corners): the brief asks for the
  raycast model, and it needs a grid over the whole level. It is the known weakness below.
- **`Physics.RaycastAll`:** allocates an array on every noise; `RaycastNonAlloc` reuses one buffer.
- **A list of every noise heard** (the prototype's way): the brief asks for the strongest one with a timestamp, and
  overwriting one memory allocates nothing per noise.
- **Exponential decay:** never reaches 0, so "forgotten after 8 s" would need a second rule. Linear is exact.

## Numbers

- **Open space vs one wall vs two walls** (a sprint footstep 6.5 m away): **0.85**, **0.2975**, **0.104**; not heard
  through three walls (0.036). Tested with the formula and with real colliders.
- **How far the Hunter hears through 0 / 1 / 2 / 3+ walls:** 0.98 r / 0.93 r / 0.81 r / 0.45 r. SMG fire: 41 m in the
  open, 19 m through three walls. The four fractions are checked by a test.
- **Decay:** half after 4 s, gone after 8 s.
- **On the real level** (`HearingLevelTests`: Environment and Interactables opened in the test, positions from
  `docs/level-layout.md`):

  | Case | Distance | Walls | Sprint (13 m) | SMG (42 m) |
  | --- | --- | --- | --- | --- |
  | Dock, open air | 20.0 m | 0 | out of range | 0.890 |
  | Ramp bottom, open passage | 8.2 m | 0 | 0.634 | 1.370 |
  | Corridor → GeneratorRoom, solid wall | 4.5 m | 1 | 0.389 | 0.531 |
  | SecurityArea → Corridor, D-Security closed | 10.0 m | 1 | 0.137 | 0.453 |
  | Same, door collider off | 10.0 m | 0 | — | 1.295 |
  | GeneratorRoom → Laboratory | 14.0 m | 2 | out of range | 0.139 |

  Each real wall counts once (the bunker's 0.4 m wall bands are single boxes), a closed door is one wall and an
  open doorway none. A sprint is heard through the closed door at 10 m, but not from the next room 14 m away.
  (H4: the generator-to-lab line moved to x −2.4, clear of the catwalk stairs the World Builder added in W13;
  across the stairs a solid step counts as a third wall.)
- **Allocation:** 1000 heard noises, each with a real raycast through two walls: **0 bytes**.
- **Tests:** 57 new (HearingMath 22, HearingSystem 14, NoiseMemory 11, real level 6, HearingConfig 4); Hunter
  132/132.
- **Build Hunter Config:** run 2 byte-identical.

## Known weaknesses

- **Sound does not bend round corners.** A footstep in the next room, through an open doorway but not in line, is
  muffled as if it came through the wall. The prototype's path flood would fix it.
- **One remembered noise.** A loud generator can hide a quieter but more interesting footstep until it fades below it.
  H3 reacts to the type of the noise it remembers.
- **A wall is any collider on Level.** A big prop on Level between the two counts as a wall; at 1.2–1.6 m the ray
  passes over desks and tables, but not over lockers.
- **Not on the Hunter prefab yet.** Adding `HearingSystem` and its brain to the Hunter's agent prefab is H3, and the
  Agent Controller owns the agent prefabs.
