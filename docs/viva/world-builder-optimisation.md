# World Builder optimisation (W11)

Run **Blackout > World > Scene Report** to inspect the loaded Environment scene. Batch entry point: `Blackout.Editor.SceneReport.AppendBaseline`. Reports never save the scene or assets.

## 2026-10-18 02:19:28 — baseline

Unity 6000.3.25f1; target StandaloneWindows64; quality PC; LOD bias 2; graphics Null (`-nographics`)

All objects include inactive children; mesh counts repeat shared meshes per instance. Static means any authored static flag, not a promise that an object cannot move. Terrain vegetation is counted separately from scene Renderer components. Geometry totals are inventory bounds, not triangles visible in one frame.

| Metric | Value |
| --- | ---: |
| GameObjects | 529 |
| Objects with static flags / without flags | 94 / 435 |
| Renderer components / active and enabled | 169 / 168 |
| Renderer: MeshRenderer | 164 |
| Renderer: ParticleSystemRenderer | 5 |
| Renderers with BatchingStatic flag | 93 |
| Scene mesh triangles / vertices (all LOD children) | 7144 / 8066 |
| Terrain full-grid triangles / vertices (upper bound before holes and pixel-error LOD) | 524288 / 263169 |
| Terrain tree instances | 657 |
| Tree LOD0 triangles / vertices (all instances) | 120744 / 207913 |
| Terrain detail instances (authored density) | 6376 |
| Detail mesh triangles / vertices (all instances) | 96160 / 252480 |
| Combined inventory triangles / vertices (full Terrain grid and LOD0 vegetation) | 748336 / 731628 |
| Unique materials (scene, Terrain and all vegetation LODs) | 28 |
| Lights: Mixed / enabled | 37 |
| Lights: Realtime / disabled | 17 |
| Active URP SRP Batcher / shadow distance (m) | True / 50 |
| Unique referenced textures (including embedded scene dependencies) | 4 |
| Texture runtime memory estimate (bytes, Editor process) | 1510972 |

Top 10 referenced textures by Editor runtime memory estimate; this includes object overhead and CPU copies, so it is not compressed build size or GPU-only memory. Imported dimensions reflect the active target.

| Texture / asset | Dimensions | Format | Estimated bytes |
| --- | ---: | --- | ---: |
| Assets/Environment/Terrain/IslandTerrainData.asset / SplatAlpha 0 | 512 × 512 | RGBA32 | 1398996 |
| Assets/Environment/Vegetation/Placeholder/Textures/GrassBlades.asset / GrassBlades | 128 × 128 | RGBA32 | 88276 |
| Assets/Environment/Atmosphere/SoftParticle.png / SoftParticle | 64 × 64 | RGBA32 | 22740 |
| Assets/Environment/Terrain/Layers/TerrainLayerWhite.asset / TerrainLayerWhite | 4 × 4 | RGBA32 | 960 |

Vegetation estimate: fixed 72° FOV, 16:9, eye 1.65 m, active quality LOD bias; whole detail patches, without occlusion or shadow passes. Frame Debugger and Profiler validation remains an Editor task.

```text

  visible vegetation triangles (main camera: FOV 72, 16:9, eye 1.65 m, LOD bias 2 (PC); shadows not counted), budget 250,000:
    Dock (start)         trees LOD0/1/2 25 / 294 / 78 = 36,310 tris; bushes + grass 2185 = 27,278 tris; total 63,588 (25.4 % of budget)
    Forest checkpoint    trees LOD0/1/2 52 / 268 / 67 = 31,057 tris; bushes + grass 3306 = 43,552 tris; total 74,609 (29.8 % of budget)
    Trail middle         trees LOD0/1/2 75 / 160 / 51 = 18,899 tris; bushes + grass 2901 = 41,430 tris; total 60,329 (24.1 % of budget)
    Gate, looking back   trees LOD0/1/2 78 / 182 / 57 = 27,208 tris; bushes + grass 3657 = 53,666 tris; total 80,874 (32.3 % of budget)
    Hunter-1 spawn       trees LOD0/1/2 76 / 171 / 53 = 21,466 tris; bushes + grass 2689 = 37,386 tris; total 58,852 (23.5 % of budget)
    worst: Gate, looking back, 80,874 of 250,000 (within budget)
```


## 2026-10-18 02:44:02 — SRP Batcher compatibility check (settings retained)

| Check | Before | After | What changed and why |
| --- | --- | --- | --- |
| SRP shader layout: compatible / incompatible / unknown | 4 / 0 / 9 | 4 / 0 / 9 | Check completed; retained the enabled SRP Batcher. Shader layout alone does not guarantee runtime batching. |

Unity 6000.3.25f1; target StandaloneWindows64; quality PC; LOD bias 2; graphics Direct3D11

All objects include inactive children; mesh counts repeat shared meshes per instance. Static means any authored static flag, not a promise that an object cannot move. Terrain vegetation is counted separately from scene Renderer components. Geometry totals are inventory bounds, not triangles visible in one frame.

| Metric | Value |
| --- | ---: |
| GameObjects | 529 |
| Objects with static flags / without flags | 94 / 435 |
| Renderer components / active and enabled | 169 / 168 |
| Renderer: MeshRenderer | 164 |
| Renderer: ParticleSystemRenderer | 5 |
| Renderers with BatchingStatic flag | 93 |
| Scene mesh triangles / vertices (all LOD children) | 7144 / 8066 |
| Terrain full-grid triangles / vertices (upper bound before holes and pixel-error LOD) | 524288 / 263169 |
| Terrain tree instances | 657 |
| Tree LOD0 triangles / vertices (all instances) | 120744 / 207913 |
| Terrain detail instances (authored density) | 6376 |
| Detail mesh triangles / vertices (all instances) | 96160 / 252480 |
| Combined inventory triangles / vertices (full Terrain grid and LOD0 vegetation) | 748336 / 731628 |
| Unique materials (scene, Terrain and all vegetation LODs) | 28 |
| Lights: Mixed / enabled | 37 |
| Lights: Realtime / disabled | 17 |
| Active URP SRP Batcher / shadow distance (m) | True / 50 |
| Unique referenced textures (including embedded scene dependencies) | 4 |
| Texture runtime memory estimate (bytes, Editor process) | 3018360 |

Top 10 referenced textures by Editor runtime memory estimate; this includes object overhead and CPU copies, so it is not compressed build size or GPU-only memory. Imported dimensions reflect the active target.

| Texture / asset | Dimensions | Format | Estimated bytes |
| --- | ---: | --- | ---: |
| Assets/Environment/Terrain/IslandTerrainData.asset / SplatAlpha 0 | 512 × 512 | RGBA32 | 2797096 |
| Assets/Environment/Vegetation/Placeholder/Textures/GrassBlades.asset / GrassBlades | 128 × 128 | RGBA32 | 175656 |
| Assets/Environment/Atmosphere/SoftParticle.png / SoftParticle | 64 × 64 | RGBA32 | 44584 |
| Assets/Environment/Terrain/Layers/TerrainLayerWhite.asset / TerrainLayerWhite | 4 × 4 | RGBA32 | 1024 |

Batching audit (shader layout and configuration, not measured draw calls):

| Check | Observed |
| --- | --- |
| Player batching, active build target | Static 1, Dynamic 1 (1 enabled, 0 disabled) |
| BatchingStatic Renderer components | 93 |
| Static-rule targets (renderers plus Terrain) / mismatches | 111 / 0 |
| Renderers with a property block now | 5 |
| Configured FlickerLight/PowerStateLighting property-block targets (unique) | 48 |
| Compatible / incompatible / unknown subshaders | 4 / 0 / 9 |

| Referenced material shader | Subshader index | SRP layout check |
| --- | ---: | --- |
| Universal Render Pipeline/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 2 | compatible |
| Universal Render Pipeline/Particles/Simple Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 2 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 3 | compatible |
| Universal Render Pipeline/Particles/Unlit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 2 | compatible |
| Universal Render Pipeline/Terrain/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 2 | compatible |

All subshaders are listed; this does not identify the pass/variant chosen for a frame. Uninitialised shader data is unknown, including in graphics-free batch mode. The shader list covers mesh/Terrain materials; skybox and other special passes still need Frame Debugger inspection. Property blocks exclude their renderer from SRP batching even with a compatible shader. Configured targets may have no block in EditMode; inspect both powered and blackout states in Play mode. Static flags and a Player setting do not prove that Unity combined a renderer into a batch. Repeated Terrain vegetation uses its own rendering path; these Renderer counts exclude Terrain instances.



Vegetation estimate: fixed 72° FOV, 16:9, eye 1.65 m, active quality LOD bias; whole detail patches, without occlusion or shadow passes. Frame Debugger and Profiler validation remains an Editor task.

```text

  visible vegetation triangles (main camera: FOV 72, 16:9, eye 1.65 m, LOD bias 2 (PC); shadows not counted), budget 250,000:
    Dock (start)         trees LOD0/1/2 25 / 294 / 78 = 36,310 tris; bushes + grass 2185 = 27,278 tris; total 63,588 (25.4 % of budget)
    Forest checkpoint    trees LOD0/1/2 52 / 268 / 67 = 31,057 tris; bushes + grass 3306 = 43,552 tris; total 74,609 (29.8 % of budget)
    Trail middle         trees LOD0/1/2 75 / 160 / 51 = 18,899 tris; bushes + grass 2901 = 41,430 tris; total 60,329 (24.1 % of budget)
    Gate, looking back   trees LOD0/1/2 78 / 182 / 57 = 27,208 tris; bushes + grass 3657 = 53,666 tris; total 80,874 (32.3 % of budget)
    Hunter-1 spawn       trees LOD0/1/2 76 / 171 / 53 = 21,466 tris; bushes + grass 2689 = 37,386 tris; total 58,852 (23.5 % of budget)
    worst: Gate, looking back, 80,874 of 250,000 (within budget)
```


## 2026-10-18 02:44:02 — static batching check (existing flags retained)

| Check | Before | After | What changed and why |
| --- | --- | --- | --- |
| BatchingStatic renderers / static-rule mismatches | 93 / 0 | 93 / 0 | Check completed; retained authored flags. Boat, changing lamps and particles keep their existing rendering behaviour. No scene or Player setting was changed; no draw-call or frame-time improvement is claimed. |

Unity 6000.3.25f1; target StandaloneWindows64; quality PC; LOD bias 2; graphics Direct3D11

All objects include inactive children; mesh counts repeat shared meshes per instance. Static means any authored static flag, not a promise that an object cannot move. Terrain vegetation is counted separately from scene Renderer components. Geometry totals are inventory bounds, not triangles visible in one frame.

| Metric | Value |
| --- | ---: |
| GameObjects | 529 |
| Objects with static flags / without flags | 94 / 435 |
| Renderer components / active and enabled | 169 / 168 |
| Renderer: MeshRenderer | 164 |
| Renderer: ParticleSystemRenderer | 5 |
| Renderers with BatchingStatic flag | 93 |
| Scene mesh triangles / vertices (all LOD children) | 7144 / 8066 |
| Terrain full-grid triangles / vertices (upper bound before holes and pixel-error LOD) | 524288 / 263169 |
| Terrain tree instances | 657 |
| Tree LOD0 triangles / vertices (all instances) | 120744 / 207913 |
| Terrain detail instances (authored density) | 6376 |
| Detail mesh triangles / vertices (all instances) | 96160 / 252480 |
| Combined inventory triangles / vertices (full Terrain grid and LOD0 vegetation) | 748336 / 731628 |
| Unique materials (scene, Terrain and all vegetation LODs) | 28 |
| Lights: Mixed / enabled | 37 |
| Lights: Realtime / disabled | 17 |
| Active URP SRP Batcher / shadow distance (m) | True / 50 |
| Unique referenced textures (including embedded scene dependencies) | 4 |
| Texture runtime memory estimate (bytes, Editor process) | 3018360 |

Top 10 referenced textures by Editor runtime memory estimate; this includes object overhead and CPU copies, so it is not compressed build size or GPU-only memory. Imported dimensions reflect the active target.

| Texture / asset | Dimensions | Format | Estimated bytes |
| --- | ---: | --- | ---: |
| Assets/Environment/Terrain/IslandTerrainData.asset / SplatAlpha 0 | 512 × 512 | RGBA32 | 2797096 |
| Assets/Environment/Vegetation/Placeholder/Textures/GrassBlades.asset / GrassBlades | 128 × 128 | RGBA32 | 175656 |
| Assets/Environment/Atmosphere/SoftParticle.png / SoftParticle | 64 × 64 | RGBA32 | 44584 |
| Assets/Environment/Terrain/Layers/TerrainLayerWhite.asset / TerrainLayerWhite | 4 × 4 | RGBA32 | 1024 |

Batching audit (shader layout and configuration, not measured draw calls):

| Check | Observed |
| --- | --- |
| Player batching, active build target | Static 1, Dynamic 1 (1 enabled, 0 disabled) |
| BatchingStatic Renderer components | 93 |
| Static-rule targets (renderers plus Terrain) / mismatches | 111 / 0 |
| Renderers with a property block now | 5 |
| Configured FlickerLight/PowerStateLighting property-block targets (unique) | 48 |
| Compatible / incompatible / unknown subshaders | 4 / 0 / 9 |

| Referenced material shader | Subshader index | SRP layout check |
| --- | ---: | --- |
| Universal Render Pipeline/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 2 | compatible |
| Universal Render Pipeline/Particles/Simple Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 2 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 3 | compatible |
| Universal Render Pipeline/Particles/Unlit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 2 | compatible |
| Universal Render Pipeline/Terrain/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 2 | compatible |

All subshaders are listed; this does not identify the pass/variant chosen for a frame. Uninitialised shader data is unknown, including in graphics-free batch mode. The shader list covers mesh/Terrain materials; skybox and other special passes still need Frame Debugger inspection. Property blocks exclude their renderer from SRP batching even with a compatible shader. Configured targets may have no block in EditMode; inspect both powered and blackout states in Play mode. Static flags and a Player setting do not prove that Unity combined a renderer into a batch. Repeated Terrain vegetation uses its own rendering path; these Renderer counts exclude Terrain instances.



Vegetation estimate: fixed 72° FOV, 16:9, eye 1.65 m, active quality LOD bias; whole detail patches, without occlusion or shadow passes. Frame Debugger and Profiler validation remains an Editor task.

```text

  visible vegetation triangles (main camera: FOV 72, 16:9, eye 1.65 m, LOD bias 2 (PC); shadows not counted), budget 250,000:
    Dock (start)         trees LOD0/1/2 25 / 294 / 78 = 36,310 tris; bushes + grass 2185 = 27,278 tris; total 63,588 (25.4 % of budget)
    Forest checkpoint    trees LOD0/1/2 52 / 268 / 67 = 31,057 tris; bushes + grass 3306 = 43,552 tris; total 74,609 (29.8 % of budget)
    Trail middle         trees LOD0/1/2 75 / 160 / 51 = 18,899 tris; bushes + grass 2901 = 41,430 tris; total 60,329 (24.1 % of budget)
    Gate, looking back   trees LOD0/1/2 78 / 182 / 57 = 27,208 tris; bushes + grass 3657 = 53,666 tris; total 80,874 (32.3 % of budget)
    Hunter-1 spawn       trees LOD0/1/2 76 / 171 / 53 = 21,466 tris; bushes + grass 2689 = 37,386 tris; total 58,852 (23.5 % of budget)
    worst: Gate, looking back, 80,874 of 250,000 (within budget)
```


### 2026-10-18 — interpreting the batching checks

The two checks ran sequentially, with a fresh full report after each. SRP Batcher and Windows Player static batching were already enabled. The existing rules matched all 111 inspected renderer/Terrain targets; 93 Renderer components carry BatchingStatic. Both checks retained those settings. Geometry, object counts and authored content stayed unchanged; this is an audit result, not a measured speedup.

The Direct3D 11 run returned four compatible compiled subshaders and nine with uninitialised data, across the four material shaders listed above. The nine unknown entries are not failures or a claim of compatibility. A graphics-free trial returned uninitialised data for all 13; the audit explicitly handles that case. Shader Inspector and Frame Debugger checks below remain necessary for the actual passes and variants used by the game, including the skybox and Terrain vegetation paths.

The report found five renderers with property blocks already present in EditMode and 48 unique lamp/screen targets configured in FlickerLight or PowerStateLighting to receive property blocks. These counts overlap and must not be added. Material property blocks can exclude their renderers from SRP batching; retaining them preserves the existing flicker and blackout emission. Replacing them with per-lamp materials would increase material instances and needs a separate rendered performance comparison before changing this design.

The first baseline ran with graphics Null; the audit used Direct3D11. Texture runtime memory estimates are 1,510,972 and 3,018,360 bytes respectively. That difference reflects the measurement context, including graphics allocations, and is not a content change or an optimisation result. Compare future memory samples only with the same graphics mode, build target, quality level and texture settings.

Editor verification (manual results are still pending):

1. Open Environment and run **Blackout > World > Scene Report**. Confirm the batching audit prints and that the Console has no new errors. Select the referenced shaders and inspect their **SRP Batcher** compatibility for the active subshader; an unknown result needs this check. Read Player > Other Settings > Rendering > Static Batching without changing the Systems Engineer's settings.
2. Once the Systems Engineer's runtime scene loader is available, play through Bootstrap with Environment active. At the dock, gate and inside the bunker, open **Window > Analysis > Frame Debugger**, enable capture, and inspect the batches and reasons for splitting them. Check the chosen shader passes rather than assuming every listed subshader runs. Repeat after **Lighting > PowerStateLighting > Trigger Blackout** and **Reset To Powered** to verify changing lamps still work. This checkout has no runtime additive scene loader; the Environment-only report can be inspected immediately.
3. Inspect a bunker wall or dock renderer in Play mode for its static-batch status, and inspect the boat and changing lamps separately. Terrain trees/details have a separate rendering path; their prefab renderers are not extra scene Renderer components. Do not mark all Environment descendants Static.
4. For performance evidence, keep the same PC quality, resolution, 72 degree FOV, position and facing. Record Profiler CPU/GPU frame time and Frame Debugger batches for powered and blackout states on the demo machine. No draw-call reduction, frame-time improvement or visual approval has been measured in this chunk.

Batch logs: `%TEMP%/blackout-w11-commit2-batching-graphics.log` (Direct3D11); `%TEMP%/blackout-w11-commit2-batching.log` (initial graphics-free trial). The latter predates the corrected uninitialised-result classification; use the dated tables above as the retained measurements.
## 2026-10-18 02:49:46 — occlusion workflow baseline (manual bake pending)

Unity 6000.3.25f1; target StandaloneWindows64; quality PC; LOD bias 2; graphics Direct3D11

All objects include inactive children; mesh counts repeat shared meshes per instance. Static means any authored static flag, not a promise that an object cannot move. Terrain vegetation is counted separately from scene Renderer components. Geometry totals are inventory bounds, not triangles visible in one frame.

| Metric | Value |
| --- | ---: |
| GameObjects | 529 |
| Objects with static flags / without flags | 94 / 435 |
| Renderer components / active and enabled | 169 / 168 |
| Renderer: MeshRenderer | 164 |
| Renderer: ParticleSystemRenderer | 5 |
| Renderers with BatchingStatic flag | 93 |
| Scene mesh triangles / vertices (all LOD children) | 7144 / 8066 |
| Terrain full-grid triangles / vertices (upper bound before holes and pixel-error LOD) | 524288 / 263169 |
| Terrain tree instances | 657 |
| Tree LOD0 triangles / vertices (all instances) | 120744 / 207913 |
| Terrain detail instances (authored density) | 6376 |
| Detail mesh triangles / vertices (all instances) | 96160 / 252480 |
| Combined inventory triangles / vertices (full Terrain grid and LOD0 vegetation) | 748336 / 731628 |
| Unique materials (scene, Terrain and all vegetation LODs) | 28 |
| Lights: Mixed / enabled | 37 |
| Lights: Realtime / disabled | 17 |
| Active URP SRP Batcher / shadow distance (m) | True / 50 |
| Unique referenced textures (including embedded scene dependencies) | 4 |
| Texture runtime memory estimate (bytes, Editor process) | 3018360 |

Top 10 referenced textures by Editor runtime memory estimate; this includes object overhead and CPU copies, so it is not compressed build size or GPU-only memory. Imported dimensions reflect the active target.

| Texture / asset | Dimensions | Format | Estimated bytes |
| --- | ---: | --- | ---: |
| Assets/Environment/Terrain/IslandTerrainData.asset / SplatAlpha 0 | 512 × 512 | RGBA32 | 2797096 |
| Assets/Environment/Vegetation/Placeholder/Textures/GrassBlades.asset / GrassBlades | 128 × 128 | RGBA32 | 175656 |
| Assets/Environment/Atmosphere/SoftParticle.png / SoftParticle | 64 × 64 | RGBA32 | 44584 |
| Assets/Environment/Terrain/Layers/TerrainLayerWhite.asset / TerrainLayerWhite | 4 × 4 | RGBA32 | 1024 |

Batching audit (shader layout and configuration, not measured draw calls):

| Check | Observed |
| --- | --- |
| Player batching, active build target | Static 1, Dynamic 1 (1 enabled, 0 disabled) |
| BatchingStatic Renderer components | 93 |
| Static-rule targets (renderers plus Terrain) / mismatches | 111 / 0 |
| Renderers with a property block now | 5 |
| Configured FlickerLight/PowerStateLighting property-block targets (unique) | 48 |
| Compatible / incompatible / unknown subshaders | 4 / 0 / 9 |

| Referenced material shader | Subshader index | SRP layout check |
| --- | ---: | --- |
| Universal Render Pipeline/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Lit | 2 | compatible |
| Universal Render Pipeline/Particles/Simple Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 2 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Simple Lit | 3 | compatible |
| Universal Render Pipeline/Particles/Unlit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Particles/Unlit | 2 | compatible |
| Universal Render Pipeline/Terrain/Lit | 0 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 1 | unknown (code 1): Not initialized (); inspect Shader Inspector |
| Universal Render Pipeline/Terrain/Lit | 2 | compatible |

All subshaders are listed; this does not identify the pass/variant chosen for a frame. Uninitialised shader data is unknown, including in graphics-free batch mode. The shader list covers mesh/Terrain materials; skybox and other special passes still need Frame Debugger inspection. Property blocks exclude their renderer from SRP batching even with a compatible shader. Configured targets may have no block in EditMode; inspect both powered and blackout states in Play mode. Static flags and a Player setting do not prove that Unity combined a renderer into a batch. Repeated Terrain vegetation uses its own rendering path; these Renderer counts exclude Terrain instances.



Vegetation estimate: fixed 72° FOV, 16:9, eye 1.65 m, active quality LOD bias; whole detail patches, without occlusion or shadow passes. Frame Debugger and Profiler validation remains an Editor task.

```text

  visible vegetation triangles (main camera: FOV 72, 16:9, eye 1.65 m, LOD bias 2 (PC); shadows not counted), budget 250,000:
    Dock (start)         trees LOD0/1/2 25 / 294 / 78 = 36,310 tris; bushes + grass 2185 = 27,278 tris; total 63,588 (25.4 % of budget)
    Forest checkpoint    trees LOD0/1/2 52 / 268 / 67 = 31,057 tris; bushes + grass 3306 = 43,552 tris; total 74,609 (29.8 % of budget)
    Trail middle         trees LOD0/1/2 75 / 160 / 51 = 18,899 tris; bushes + grass 2901 = 41,430 tris; total 60,329 (24.1 % of budget)
    Gate, looking back   trees LOD0/1/2 78 / 182 / 57 = 27,208 tris; bushes + grass 3657 = 53,666 tris; total 80,874 (32.3 % of budget)
    Hunter-1 spawn       trees LOD0/1/2 76 / 171 / 53 = 21,466 tris; bushes + grass 2689 = 37,386 tris; total 58,852 (23.5 % of budget)
    worst: Gate, looking back, 80,874 of 250,000 (within budget)
```


### 2026-10-18 — occlusion bake workflow and measured current state

W11 permits bake instructions instead of generated data. This chunk documents the workflow and reruns Scene Report; no bake, scene save, gameplay edit or culling speedup is claimed. Reading the saved Environment scene confirms that its occlusion-data reference is unassigned. The static-flag counts below come from the saved GameObjects, not visible-object counts. The Player prefab already has Camera Occlusion Culling enabled and FOV 72; it was only read.

| Check | Before | After this chunk | What changed and why |
| --- | --- | --- | --- |
| Linked Environment occlusion data | Unassigned | Unassigned; manual bake pending | Documented isolated validation and the coordinated additive-scene bake. |
| GameObjects with Occluder Static / Occludee Static flags | 93 / 94 | 93 / 94 | Retained existing opaque structure flags; water is only an occludee and the boat stays dynamic. |
| Saved Smallest Occluder / Smallest Hole / Backface Threshold | 5 m / 0.25 m / 100 | 5 m / 0.25 m / 100 | Retained parameters as the first bake baseline. |
| Full Scene Report inventory triangles / vertices | 748,336 / 731,628 | 748,336 / 731,628 | Same Direct3D11 context as commit 2; occlusion does not remove stored geometry. |
| Runtime culled objects, batches and CPU/GPU time | Not measured | Not measured | Requires baked data and a rendered camera view. |

First validate Environment on its own:

1. Open only `Assets/Scenes/Environment.unity`. Run **Blackout > World > Scene Report** and retain its Console output. Inspect the **Occluder Static** and **Occludee Static** flags on the bunker, dock and Terrain. Keep transparent water out of the occluder set; leave the boat, changing lamps, particles and all gameplay actors as authored. Opaque fixed walls and Terrain are suitable occluders; the bake uses rendered geometry, not colliders. [Unity scene setup](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-getting-started.html).
2. Open **Window > Rendering > Occlusion Culling > Bake**. For the first bake keep **Smallest Occluder 5**, **Smallest Hole 0.25**, **Backface Threshold 100**, then click **Bake**. Here 0.25 m is below the layout's 1.4 m door gaps; 100 avoids removing potential camera regions based on backfaces. These are starting settings, not validated optimal values. Smaller occluder/hole settings increase precision and data cost. [Unity bake settings](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-window.html).
3. Wait for completion and inspect the generated asset path in Project. Unity's multi-scene manual documents `Assets/[Scene name]/OcclusionCullingData.asset`; for Environment that is inside the owned `Assets/Environment` root. Confirm the actual location before staging. Save **Environment only**. Include its scene change and the generated asset and `.meta`, plus any new owned folder `.meta`; do not move or delete assets with shell commands. This future bake is a separate change from this documentation commit. [Unity data persistence](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-scene-loading.html).
4. For this isolated scene, create a temporary Camera named `WorldOcclusionPreviewCamera`, with FOV 72 and Occlusion Culling enabled. Select it in **Visualization** and move it through the dock, forest, lobby, ramp, each door gap and bunker rooms at eye height about 1.65 m over the floor. Check the camera is within baked camera volumes and visible geometry never disappears. Remove this temporary object using the Hierarchy before saving; it is only a preview, with no gameplay scripts. [Unity visualization](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-getting-started.html).
5. Run Scene Report again and append a dated row for the linked data asset, bake settings, data bytes and results. Inventory triangles should remain unchanged. Compare the same rendered view with Camera Occlusion Culling off/on temporarily; record batches and CPU/GPU time, then restore the camera setting. If culling is too conservative, test **Smallest Occluder 5 → 2 m** as a separate bake, report and comparison. Keep 0.25 and 100 fixed for that test; the 2 m candidate is below the 3 m room height and remains unmeasured.

For the integrated game, coordinate with the scene owners before a shared bake. Unity loads one occlusion-data asset at a time; scenes used additively must share the bake and its references. Open Bootstrap, Environment, Interactables and Agents together. The team performs the shared bake and reviews the generated output location; each owner saves and commits only their own scene's reference to the same data. World Builder owns Environment and data under `Assets/Environment`; Systems Engineer owns Bootstrap/Interactables; Agent Controller owns Agents. Do not stage or save their scenes as part of a World Builder chunk. The Environment-only bake is an isolated validation, not proof of integrated runtime culling. [Unity additive-scene requirements](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-scene-loading.html).

Doors and agents must remain movable. They can be occluded by fixed structure but cannot become baked static occluders; do not close door gaps with proxy walls. Verify open/closed doors, the lab unlock and the return route after the shared bake. Any dynamic-renderer setting belongs to its prefab owner. [Unity dynamic objects](https://docs.unity3d.com/6000.3/Documentation/Manual/occlusion-culling-dynamic-gameobjects.html).

Handoff to Systems Engineer (`Assets/Scenes/Bootstrap.unity`, `Assets/Scenes/Interactables.unity` and the runtime loader under `Assets/Scripts/PlayerAndInteraction`): coordinate a shared occlusion bake, save those scenes' references to the shared data yourself, and verify data loading with Environment active after the additive loads; no runtime additive loader exists in this checkout yet.

Handoff to Agent Controller (`Assets/Scenes/Agents.unity`): join the coordinated occlusion bake, save/commit Agents' reference to the same data yourself, and verify agents remain visible through door openings and never become static occluders.

Verification: Unity 6000.3.25f1, Direct3D11, batch exit 0; Environment SHA256 unchanged; no C# compiler errors/warnings. No tests rerun for this documentation-only chunk. Log: `%TEMP%/blackout-w11-commit3-occlusion.log`. Unity Pipeline logged an ObjectDisposedException serving `/api/commands` during shutdown; the report completed and Unity exited 0. Bake/visual/performance checks above remain manual.
