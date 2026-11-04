# Ocean and shore (step OC1)

The beach had a flat, dark-blue transparent 520 m plane for a sea and plain sand with no shoreline. **Blackout > World > Build
Ocean and Shore** (`Assets/Editor/WorldBuilder/OceanBuilder.cs`) replaces both with a lit, animated sea and a dressed shoreline.
It can be re-run: it reuses objects by name, never moves the dock, boat, trail or markers, and adds no colliders, so the NavMesh,
the player and the AI are unchanged.

## The sea

| Item | Value |
| --- | --- |
| Object | `Beach/Water`, same place as before: y 0, centred (0, 20), 520 x 520 m, no collider, no shadows |
| Mesh | `Assets/Environment/OceanSurface.asset`: flat 128 x 128 cells (4.06 m), 16,641 vertices, 32,768 triangles, 16-bit indices. Built by `OceanMesh.cs`; it never changes at run time |
| Shader | `Assets/Environment/Shaders/BlackoutOcean.shader` (`Blackout/Ocean`), one transparent URP forward pass, no textures. Material `Assets/Materials/Environment/Ocean.mat` |
| Swells (move the vertices) | three sine waves from different headings (about 50, 100 and 135 degrees apart), wavelength 38 / 26 / 17 m, amplitude 0.06 / 0.05 / 0.03 m (at most 0.14 m, so the shoreline moves under a metre on the 6.6 degree beach). Speed is sqrt(g k), so long waves are faster |
| Ripples (normal only) | eight short waves, 5.4 m down to 0.8 m, whose headings step by the golden angle (137.5 degrees) so none share a direction or spacing: the sum is irregular chop with no stripes. Each fades out at 25 wavelengths from the camera so it never aliases |
| Colour | shallow-to-deep tint from the **vertical** water depth (rebuilt from the depth texture), Fresnel reflection of the scene's own sky ambient colours, a moon glint, and the glow of nearby lights (boat lantern, torch) |
| Shore | soft transparent edge over 0.3 m of depth; a foam front (0.9 m wide, reach varying along the shore like a swash) broken into lace by the chop, a fainter trail behind it, and speckled foam only on the highest crests |

The first version used four same-direction waves and the reflection probe. In the game camera that gave long, even streaks across the
water and a milky sheen (the nearest baked probe is brighter than the night sky), so it was reworked as above.

The shader needs **Depth Texture** on the URP asset. `PC_RPAsset` has it; `Mobile_RPAsset` does not, so a mobile quality level
would lose the depth tint and the foam (the surface still draws). The sea does not receive shadows and the lightmap bake ignores it
(see-through static flags, as before). `BeachGreyboxBuilder` (Build Terrain) now keeps an installed ocean instead of putting the old
plane back.

## The shore

Everything is under `Beach/Shore`, all with see-through static flags (probe lit, no lightmap rebake) and no colliders.

- **Wet sand** (`Shore_WetSand`, `ShoreWetSand.asset`, `Blackout/WetSand`): a strip 3.5 cm above the terrain between heights -0.10 m and
  about +0.55 m (the top edge wobbles by up to 0.28 m). Vertex alpha is the wetness, so the sand darkens and glosses toward the water.
  It is baked from the saved terrain, so it needs no second terrain layer and no repaint.
- **Sand grain**: the Sand terrain layer gets a tileable normal map (`Assets/Textures/Environment/Sand/Sand_Ripple_Normal.png`, 256 px, tile
  4.55 m, normal scale 0.7): five octaves of wrapped noise for soft undulation and grain, plus only a faint, bent, patchy trace of wind
  ripple. The first version was dominated by regular parallel ripples, which looked like corduroy under moonlight. Seeded (9201), so the
  PNG is identical every run; a test requires the slope to be about equal along both axes (no stripes).
- **Rocks** (7) reuse the forest `Rock`, `RockSmall` and `RockRound` LOD0 meshes and material at scale 0.22 - 0.5, partly sunk in the
  sand, mostly at the waterline. **Driftwood** (5) is three generated logs (`ShoreDriftwood_0..2.asset`, 1.8 - 3.2 m) in a weathered
  grey-brown material, lying along the shore and following the slope. Placement seed 9301; the middle of the beach (x -7 .. 9: dock,
  cargo, sign, trail start) stays clear, nothing is within 1.6 m of a tree, and pieces are at least 3.5 m apart.

## Tuning and re-running

Tune the sea in `Ocean.mat` (colours, foam width, ripple strength, glint) or in the wave tables at the top of the shader. The material is
created once and kept on re-runs. **Paint Terrain resets the Sand layer, so run Build Ocean and Shore again after it.**

## Verification (2026-11-04, Unity 6000.3.25f1, Editor open)

- Compile clean; both shaders compile and are supported. `OceanBuilderTests` 10/10 (mesh size and winding, wet strip height/fade/cliff
  skip, driftwood normals and repeatability, tileable and stripe-free sand texture, placement rules, shaders compile, built assets).
  Re-run after the realism rework: 10/10; the broader assembly run below was from the first version.
- `Blackout.World.Editor.Tests` (all 369 EditMode tests): 367 pass; the two `ScreenshotCaptureTests` stop in their SetUp on Editor-state
  checks (a dirty or populated untitled scene in the interactive runner), before any shoreline code runs.
- Repeat run (after the rework): the scene, 5 mesh assets, 3 materials, sand PNG and Sand layer hashes are identical (10 files).
- Captures: Scene View at player eye height on the beach (looking at the sea, and looking at the moon from the dock) and one Game view frame
  of the first version from the dock. After the rework only Scene View was captured (dark water, chop, lacy foam, grainy sand, moon glitter
  path, no stripes). **The reworked look was not re-checked through the game camera.**

## Limits

No boat bobbing or wake, no splashes, no underwater view (the player cannot enter the sea). Rocks and driftwood are visual only: the
player and AI walk through them. The foam is drawn by the water shader, not a particle effect, so there are no splashes. Frame cost of the sea on a target
machine was not measured.

Viva: **does** draws a lit, animated sea with depth tint, shore foam and a dressed beach; **design** GPU-only waves on a small fixed mesh,
depth-texture shoreline and a baked strip for wet sand; **alternative** rejected a re-painted wet-sand terrain layer and CPU wave meshes;
**number** 32,768 sea triangles, 3 swells + 8 ripples, 7 rocks and 5 logs; **weakness** the reworked look was only inspected in Scene View,
not the game camera, and GPU time was not measured.
