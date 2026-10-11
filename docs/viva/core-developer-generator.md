# Viva notes: generator prefab (Core Developer, step C2)

## What I built

- `GeneratorVisuals` (`Assets/Prefabs/Models/Scripts`): listens to the `GeneratorDisabled` channel and, on the blackout,
  swaps the core's material to `M_Generator_CoreOff`, stops the hum and plays the sparks. It does nothing else.
- `GeneratorMaterialBuilder`, `GeneratorPrefabBuilder` and `ModelTextureRules` (`Assets/Editor/CoreDeveloper`): Editor
  tools that create the six materials, the spark material and `Generator_Visual.prefab`, and fix texture import settings.
- 42 new EditMode tests: 9 in `Blackout.Art.Tests` and 33 in `Blackout.Art.Editor.Tests` (which now holds 76 with the C1 ones).

## Why this design

- **Visuals only.** The Systems Engineer owns interaction (hold E, the event). My prefab has no interaction script, so
  it can be nested without edits and the two roles never touch the same file.
- **Event, not reference.** `GeneratorVisuals` knows only the channel asset. It does not know who raises the event, so
  the lighting, the agents and the visuals all react to one event with no direct links.
- **Swap the shared material, do not change colours at run time.** `renderer.sharedMaterials` makes no per-object
  material copy, and it needs no per-frame code at all: the blackout happens once, so nothing runs in `Update`.
- **Tools are re-runnable and never overwrite hand work.** Existing materials, sockets and references are kept. Only
  an empty texture slot or the `Model` child is filled or replaced.
- **Placeholder first.** The prefab and the whole event chain can be tested before the Blender model exists. The FBX is
  a drop-in: same name, same materials, same sockets.

## Alternative I rejected

Animating the core with a material property (a script lerping `_EmissionColor` through a `MaterialPropertyBlock`).
It would give the prototype's 1.6 s flicker, but it needs an `Update`, a coroutine or a tween and a second code path to
test. The brief asks for "switches the core material to off", and a swap is one line and testable. The flicker is a
possible extension once everything else works.

## One number and why

- **Collider 6.4 x 3.6 x 3.2 m** at centre y 1.8: the prototype's room collider (`core-developer.md` 2.2). It is a
  single box on the root (no per-part colliders), because the player only needs to be stopped and shot at the
  silhouette, and one box is cheaper than a collider per part.
- Another: the **spark burst** is 40 particles every 0.9 s, max 200 alive. The prototype uses 60 + 50 sparks at the
  event, then bursts every 0.4 to 2 s; one repeating burst is the simple version of that.

## Known weaknesses

- The FBX path of `GeneratorPrefabBuilder` has not run on a real FBX yet (none exists). Its material matching and
  core search are unit-tested on in-memory renderers; the `InstantiatePrefab` line is not. Re-check on the first import.
- Only the core goes dark. The prototype also dims the LEDs, stops the flywheel and beacon and drops the gauge needles.
- The hum has no clip yet, and the core emission strength is my estimate.
- The tests that import real assets write to a `_Test` folder and delete it afterwards, so an interrupted test run can
  leave `Assets/Materials/Models/_Test` or `Assets/Textures/Models/_Test` behind. Delete those folders if you see them.
