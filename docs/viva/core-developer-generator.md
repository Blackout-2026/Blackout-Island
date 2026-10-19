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

---

# Viva notes: realistic generator model (Core Developer, step C6)

## What I built

- `SM_Generator.fbx` (`Assets/Models/Custom`) and its source `Art/Blender/SM_Generator.blend`: the C2 placeholder replaced by
  a bevelled, UV-unwrapped, PBR-textured model. 7,376 triangles, 3 meshes (`Generator`, `GlassTube`, `EnergyCore`), 5 materials.
- Four 2048 x 2048 maps in `Assets/Textures/Models`: `T_Generator_BaseColor`, `_Normal`, `_MetalSmooth` (R metallic, A
  smoothness), `_Emission`. The existing `GeneratorMaterialBuilder` wired them with no code change.
- Three Blender Python scripts in `Art/Blender/Scripts` (run in batch mode, so the model is rebuilt from code):
  `build_generator.py` (geometry, UVs), `bake_generator.py` (procedural wear, bake, FBX export), `preview_generator.py`
  (review renders). `gen_parts.py` holds the part builders (box, cylinder, annulus, torus, prism, tube).
- The size is the original brief, unchanged: **6.96 x 4.10 x 2.90 m**, origin on the floor under the base plate, control
  face at +X. The script prints a warning if any axis drifts more than 2 cm, and Unity's own import line confirms it.
- **Axis calibration, done for real:** the playbook's table of how Blender axes arrive in Unity was a guess. Unity put my
  energy core at -X, so I measured the mapping (`Unity = Blender (x, z, y)`, checked on X, Y and Z) and corrected
  `docs/art-pipeline.md` section 4. The robot must be modelled facing Blender +Y, not -Y.
- Style reference: `Art/Reference/Generator/` (two images, credited in `docs/credits/core-developer.md`). Shapes, logo
  and decals are my own: the vaulted housing, 5 louvred vents, ring flanges, gauges, button box, elbow pipes.

## Why this design

- **One shared texture atlas for Body, Metal and Details.** Three materials, one texture set: Unity binds the same four
  textures, so the three slots cost almost nothing extra. Glass and Core stay flat colours on purpose, the blackout swaps
  the core's material and the glass needs no texture.
- **Wear comes from the geometry, not from random noise alone.** Cycles bakes an edge mask (Bevel node) and a cavity mask
  (Ambient Occlusion node) first. Paint chips and rust are thresholded noise driven by those masks, so paint comes off
  along the edges and dirt gathers in the crevices, which is why it reads as real wear.
- **Everything the player cannot see is deleted.** A ray test (18 rays over each face's hemisphere, from the centre and
  the corners) removes 1,208 faces that are fully enclosed. Together with trimming the segment counts, the body surface
  that needs texture went from 213 m2 to 133 m2 and the texel density from 86 to 131 px/m.
- **Decals are baked into the atlas.** The danger sign, the "GENERATOR G-1" label and the gauge dials are projected from a
  second UV set while baking. That set is deleted before export, so Unity sees UV0 only.
- **Everything is built in Unity-frame numbers.** One function (`B()` in `gen_parts.py`) converts to Blender, so the
  prefab sockets (3.5, 1.3, 0) and the 6.4 x 3.6 x 3.2 collider stay valid with no edits.

## Alternative I rejected

Modelling a high-poly version and baking its normal map onto this low-poly mesh (the usual game-art route). It gives
finer bolts and seams, but it needs a second, much denser model and a cage, and for a machine seen from 3 to 8 m the
bevelled low-poly mesh plus a procedural bump map looks the same. I also rejected tileable CC0 textures per material:
they cannot place chips along my edges or put decals at exact spots.

## One number and why

- **Bevel width 0.03 m, 2 segments, on the big housing and plate; 0.8 to 1.5 cm, 1 segment, on small parts.** Three
  centimetres is the smallest edge that still catches a highlight at 3 to 8 m. The bevel node's wear mask also needs a
  real bevel to find, so it doubles as the source of the chipped-paint effect.
- Another: **average texel density 131 px/m** (133 m2 of body surface in a 2048 x 2048 atlas, 55% of the UV space
  used). Dials and labels are scaled 3x and 3.5x, so they get about 390 to 460 px/m, enough to read the ticks and letters.

## Measured numbers

| Item | Value |
| --- | --- |
| Triangles | 7,376 total: Generator 6,752, EnergyCore 528, GlassTube 96 (target 10,000, maximum 15,000) |
| Parts merged | 171, then 1,208 hidden faces removed, 61 n-gons triangulated (end caps only, the rest are quads) |
| Materials | 5 (limit 6) |
| UV channels | UV0 only, all inside 0 to 1, no overlap |
| Textures | 4 maps, 2048 x 2048 |
| Bake time (CPU, no GPU found on this PC) | about 2.5 minutes in total, about 2 of them for the 48-sample wear pass |

## Known weaknesses

- Wear is procedural, so it is more even than hand-painted wear. The hand pass in Blender (adding scratches, a stain, a
  repaired patch) is still to do.
- 131 px/m is below the 300 px/m I guessed in `art-pipeline.md` section 7. The generator has about 130 m2 of surface, so
  a 2048 map cannot reach that. A second 2048 atlas would double it at the cost of one more texture set.
- The nested ring flanges around the control face are thin and read as sheets from some angles.
- The danger sign is a 128 x 128 image I generated for the sign set (C4), so it is soft up close.
- I checked the result with Blender renders and Unity's import report (size, triangles, material and texture wiring), not
  with a screenshot from inside Unity. Look at it in the Scene view with the game's lighting before the demo.
- The hum clip is still missing (`HumAudioSource` has no clip), and the glow strength of the core in the blackout is
  still my estimate.
