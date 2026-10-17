# World Builder optimisation (W11)

Run **Blackout > World > Scene Report** to inspect the loaded Environment scene. Batch entry point: `Blackout.Editor.SceneReport.AppendBaseline`. Reports never save the scene or assets.

## 2026-10-18 02:19:28 — baseline

Unity 6000.3.25f1; target StandaloneWindows64; quality PC; LOD bias 2

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


