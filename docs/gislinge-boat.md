# Gislinge boat replacement (B1)

The supplied `D:/model/gislinge-viking-boat/source/Gislinge_Boat.zip` replaces the
weathered fishing rowboat's appearance. This intentionally departs from the
prototype's cabin motorboat. The original boat FBX and textures remain in the
project, and existing credits are retained.

The imported `Assets/Models/Custom/SM_GislingeBoat.fbx` is a byte-for-byte copy of
`Gislinge_Boat.fbx` in that ZIP. Unity reports 29,035 triangles, 21 meshes and
two material slots. Its bounds, including the sail and rigging, are approximately
4.16 x 4.73 x 7.71 m. The planking alone is about 1.73 m wide and 7.23 m long.
There is one supplied detail level, culled below screen height 0.025; no new LOD
geometry or performance improvement is claimed.

The existing `Rowboat_Visual.prefab`, its GUID, root, `Model` child convention and
the scene's `Beach/Boat/RowboatVisual` name are retained. This also preserves the
lookup used by the pending cinematic tools. The generated old Model child is
replaced through Unity APIs after checking its source and added content. Other
prefab children are preserved. The replacement is a visual only: no runtime
scripts, colliders, Rigidbody or animation controller are added to it.

`M_Gislinge_Hull` and `M_Gislinge_Sail` use URP Lit. Each has a 2048px colour,
normal and packed MetalSmooth map. The packed map stores metallic in R,
occlusion in G and inverted roughness (smoothness) in A; it supplies both URP
texture slots. Only colour uses sRGB. Sail rendering is double-sided, opaque;
the optional transmission map is omitted. The ZIP's `basecolor_04` hull colour
is used with its matching maps, rather than mixing the separate `_02` variant.

The original boat root stays at (3.7, 0.15, -122.5) with its existing yaw and unit
scale. The visual/collision local offset is (-0.65, -0.2, -0.269) m: the narrower
hull sits closer to the dock, its longitudinal bounds are centred, and its keel
is around world Y -0.05 m. Unity remains metres, +Y up, +Z north; prototype world
positions map as (x,y,-z). Source geometry is neither stretched nor decimated.

The collision root reuses nine narrow floor strips and twelve tapered side
segments, with one new mast box: 22 enabled BoxColliders on Level. Old benches
and the transom remain disabled in the hierarchy because this model has neither.
The floor proxy top is world Y 0.18 m. Sail, ropes and rudder details have no
collision. These are simple fitted proxies, not exact mesh collision.

The existing mooring mesh is refitted to the narrower gunwales. The existing
torch, particle seeds, flicker/emission link, dock geometry and walking route
are retained. The torch mount moves to boat-local (-1.15,0.1,2.5), at the stern
gunwale, to clear the sail; its existing light follows through the builder.
No boarding bridge is added. The boat remains excluded from the
NavMesh bake. No package, assembly, input or global setting change is needed.

`BoatExit` stays at (0,1.3,-124). MissionDirector still resolves it at runtime
after additive loading. The existing mission rule requires the cure, horizontal
distance below 6 m and player Y above 0.5; completion from the dock qualifies.
The low boat floor remains below that height rule, as it was before B1.

Rebuild with Unity closed:

1. Run `python tools/import-gislinge-boat.py <path-to-Gislinge_Boat.zip>` with
   Pillow installed. It writes the original FBX and six converted PNGs in their
   existing destination paths; it never edits the download.
2. Run `Blackout.Editor.RowboatAssets.Build` with Unity batch mode, or use
   **Blackout > World > Build Rowboat Visual Assets** in the Editor.
3. Run `Blackout.Editor.RowboatPlacement.Build`, or **Blackout > World > Replace
   Boat With Rowboat**. This saves Environment and moorings through Editor APIs.
4. **Dress Beach and Boat** and **Build Terrain** retain the installed visual and
   reapply its placement. A complete terrain rebuild is outside this step.

Batch compile/build uses `-batchmode -nographics -quit`. EditMode verification
uses `-batchmode -nographics -runTests -testPlatform EditMode` without `-quit`.
Logs and XML belong under `%TEMP%/blackout-B1/`. Optional **Capture Rowboat
Preview** uses graphics, saves two PNGs under `%TEMP%/BlackoutIsland-Rowboat/`,
and restores the previous scene setup. No render camera is saved in the scene.

Verified in Unity 6000.3.25f1 on 2026-10-26:

- Final import/build, placement/render and both repeated builders exited 0.
  `repeat-final-before.json` and `repeat-result.json` show all 22 checked model,
  texture, material, prefab/metadata, scene and mooring outputs byte-identical.
- `editmode-final.xml`: 48/48 selected tests passed, exit 0 (SavedBoatTests,
  SavedDressingTests, IslandBoundaryTests and MissionStateMachineTests).
- Two final 1920x1080 previews were inspected under
  `%TEMP%/BlackoutIsland-Rowboat/20261026-015654-23d1a3b6/`. The first preview
  exposed the torch/sail overlap; the final stern mount clears it.
- Final logs contain no C# compiler warnings/errors or exceptions. Existing
  licensing signature/token diagnostics remain. The first sandbox attempt
  stalled at licensing; a retry exited 1 while it still held the project lock.
  After stopping that verified batch, licensed runs succeeded. The initial
  tests were 22/23: a nested-prefab source assertion was corrected before the
  final 48-test run. No PlayMode test or complete gameplay run is claimed.
- Other gameplay scenes, player/agent prefabs, terrain, NavMesh, packages and
  project settings retain their pre-step bytes. Source/docs whitespace passes;
  native Unity YAML spacing remains. Several unrelated serializer line-ending
  rewrites have no Git content diff and are excluded from the proposed commit.

Before the demo, open Bootstrap, inspect sail and hull materials from both sides,
walk the dock, check the jump/gap and collision inside the hull, and check torch
flicker/mooring endpoints. Carry the cure back to the dock and verify completion
and Restart. Inspect the pending cinematic arrival framing separately: a taller
sail changes its composition even though the lookup name and prefab GUID remain.

Viva: this step imports a credited third-party model, not a student-modelled hero.
It reuses a stable visual prefab and separates appearance from simple collision
and marker-driven mission logic. Direct mesh collision was avoided for the
rigging. The supplied boat has 29,035 triangles with no custom lower LOD; visual
fit and runtime draw cost still require manual review.
