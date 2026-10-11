# Level layout (World Builder)

This is the real-scale layout for `Assets/Scenes/Environment.unity`. It is built from
`docs/reference/world-builder.md`. The bunker rooms in the tables below are also stored in
`Assets/ScriptableObjects/Layout/BunkerLayout.asset`, filled by
`Assets/Editor/WorldBuilder/BunkerLayoutAssetBuilder.cs`. The marker positions are also in
`Assets/Editor/WorldBuilder/MarkerLayout.cs`, the agent size in `Assets/Editor/WorldBuilder/NavMeshBaker.cs`, and the
materials in `Assets/Editor/WorldBuilder/SurfaceLibrary.cs` and `MaterialRules.cs`.
If you change a number here, change it there too.

## Conventions

- Unity coordinates: 1 unit = 1 m, +Y up, **+Z north**, +X east. Converted from the prototype as
  `(x, y, −z)` (see the reference doc, section 0).
- **Centre** is the centre of the floor, and **y is the floor height**. **Size** is the clear interior
  (width X × depth Z). Walls, floor and ceiling slabs are **0.4 m** thick and sit outside that box.
- Rooms that touch share one 0.4 m wall. For example, the Security north wall is z 82.0 … 82.4 and the
  Corridor starts at z 82.4.

## Real-scale rules

| Rule | Value | Prototype had | Why |
| --- | --- | --- | --- |
| Player (design target) | 1.8 m tall, eye 1.65 m, radius 0.35 m | 1.75 m, eye 1.63 m | brief |
| Agent (NavMesh) | radius 0.4 m, height 1.8 m, step 0.4 m, max slope 40° | 1 m nav grid | agreed with the Agent Controller's mover; the robots' 0.38 × 1.8 m capsule fits inside it |
| Corridor width | 3.0 m | 4.0 m | brief; two agents (2 × 0.8 m) can still pass |
| Ceiling (clear) | 3.0 m | 3.4–4.0 m | brief; 1.2 m headroom over the player |
| Door opening | 1.4 × 2.3 m (every door) | 2.4–3.0 × 2.8–3.4 m | brief; 0.35 m clearance each side of the player |
| Open passage (no door) | 3.0 × 3.0 m | 4.0 × 3.6 m | same as the corridor section |
| **Exception: Generator room ceiling** | **6.5 m** | 6.5 m | 3.9 m hero generator on a 0.3 m plinth, and a catwalk at +3.0 m with 3.35 m headroom |

## Zone table

| Zone | Centre (x, y, z) | Size X × Z (m) | Floor y | Clear height | Connections |
| --- | --- | --- | --- | --- | --- |
| **Beach** | (0, 1.0, −110) | 110 × 36 (x −55 … 55, z −128 … −92) | sand 0 → 2.0 (shoreline z −105 at x 0); dock deck 1.3 (x ±1.4, z −128 … −97) | open | Forest: trail starts at (1, ~1, −104), 3.4 m wide. Boat / exit zone: (0, −124), radius 6 |
| **Forest** | (0, 6.0, −32) | 180 × 120 (x −90 … 90, z −92 … 28) | trail rises 2.0 → 10.0 | open | Beach (south edge z −92). BunkerEntrance through the compound gate (0, 10, 28), 6.4 m wide, always open |
| **BunkerEntrance** (compound yard) | (0, 10, 45) | 44 × 34 (x −22 … 22, z 28 … 62) | 10.0 | open | Forest (gate). Entrance lobby through door **D-Bunker** |
| BunkerEntrance (lobby) | (0, 10, 50.2) | 11.2 × 3.6 (x −5.6 … 5.6, z 48.4 … 52.0) | 10.0 | 3.0 | Compound through D-Bunker (south wall). Ramp through **O-RampTop** (north wall) |
| BunkerEntrance (ramp) | (0, 6.25, 61.0) | 3.0 × 17.2 (x −1.5 … 1.5, z 52.4 … 69.6) | 10.0 at the south end → 2.5 at the north end (7.5 m drop, 23.6°) | 3.0 above the slope | Lobby (O-RampTop). SecurityArea through **O-RampBottom** |
| **SecurityArea** | (0, 2.5, 76) | 16 × 12 (x −8 … 8, z 70 … 82) | 2.5 | 3.0 | Ramp (O-RampBottom, south wall). Corridor through **D-Security** (north wall) |
| **Corridor** | (0, 2.5, 103) | 3 × 41.2 (x −1.5 … 1.5, z 82.4 … 123.6) | 2.5 | 3.0 | SecurityArea (D-Security, south). GeneratorRoom through **D-Generator** (west wall, z 106). Laboratory through **D-Lab** (north) |
| **GeneratorRoom** | (−10.9, 2.5, 106) | 18 × 20 (x −19.9 … −1.9, z 96 … 116) | 2.5 (catwalk deck 5.5) | **6.5** | Corridor through D-Generator (east wall) |
| **Laboratory** | (0, 2.5, 133) | 20 × 18 (x −10 … 10, z 124 … 142) | 2.5 | 3.0 | Corridor through D-Lab (south wall), locked until the blackout |
| **EscapeRoute** | path, see below | ≈ 280 m | 2.5 → 10.0 → 1.3 | — | Laboratory → … → boat zone |

## Doors and openings

Position is the centre of the opening, in the middle of the wall's thickness, at floor level.

| Id | Between | Wall | Position (x, y, z) | W × H (m) | Kind |
| --- | --- | --- | --- | --- | --- |
| Gate | Forest ↔ compound yard | south fence | (0, 10, 28) | 6.4 × open | open gate, not a door |
| D-Bunker | compound yard ↔ lobby | lobby south (z 48.0 … 48.4) | (0, 10, 48.2) | 1.4 × 2.3 | sliding, unlocked, press E |
| O-RampTop | lobby ↔ ramp | lobby north (z 52.0 … 52.4) | (0, 10, 52.2) | 3.0 × 3.0 | open passage |
| O-RampBottom | ramp ↔ SecurityArea | security south (z 69.6 … 70.0) | (0, 2.5, 69.8) | 3.0 × 3.0 | open passage |
| D-Security | SecurityArea ↔ Corridor | security north (z 82.0 … 82.4) | (0, 2.5, 82.2) | 1.4 × 2.3 | sliding, unlocked |
| D-Generator | Corridor ↔ GeneratorRoom | corridor west (x −1.9 … −1.5) | (−1.7, 2.5, 106) | 1.4 × 2.3 | hinged, unlocked |
| D-Lab | Corridor ↔ Laboratory | lab south (z 123.6 … 124.0) | (0, 2.5, 123.8) | 1.4 × 2.3 | sliding, **maglocked until `generator:disabled`** |

## Escape route

| # | Waypoint (x, y, z) | Leg length |
| --- | --- | --- |
| 1 | cure (0, 2.5, 135) | — |
| 2 | D-Lab (0, 2.5, 123.8) | 11.2 m |
| 3 | D-Security (0, 2.5, 82.2) | 41.6 m |
| 4 | ramp bottom (0, 2.5, 69.6) | 12.6 m |
| 5 | ramp top (0, 10, 52.4) | 18.8 m along the slope |
| 6 | D-Bunker (0, 10, 48.2) | 4.2 m |
| 7 | gate (0, 10, 28) | 20.2 m |
| 8 | trail end on the dock (1, 1.3, −104) | ≈ 157 m (trail spline) |
| 9 | edge of the boat zone (0, 1.3, −118) | 14 m |

Escape checkpoints (from the prototype): escapeEntrance (0, 10, 40) facing 180°, and escapeForest
(0, 10.04, 24) facing 180°.

## Terrain, beach and boundary (W2)

Built by **Blackout/World/Build Terrain** (`Assets/Editor/WorldBuilder/TerrainBuilder.cs`). The outdoor
numbers are in `IslandLayout.cs`. The ground height comes from `IslandHeightField.cs`, a port of the
prototype's height function that is checked by EditMode tests in `Assets/Editor/WorldBuilder/Tests`.
Re-running the tool gives byte-identical output. It overwrites any hand sculpting, and asks first in the Editor.

| Item | Value |
| --- | --- |
| Terrain | `Terrain/IslandTerrain`: 400 × 400 m (x −200 … 200, z −180 … 220), y −8 … 24, heightmap 513² (0.78 m). TerrainData `Assets/Environment/Terrain/IslandTerrainData.asset` (binary). URP Terrain Lit, no layers yet (W6) |
| Ground range | −5.93 … 21.52 m, the same as the prototype |
| Compound pad | Exactly y 10.0 over x −22 … 22, z 28 … 62, blending out over 16 m. Near the trail end (z 28 … 40) it varies by up to ±0.13 m |
| Forest route | Rises from y 2.0 at z −92 to y 10.0 at the gate (z 30). The trail is smoothed to that profile; the forest either side has rolling relief |
| Hill | North of the cliff line (z 62 for \|x\| ≤ 24): 14.6 m at the cliff top, up to 21.5 m. At least 1 m of ground over every underground room (tested) |
| Beach | Plane sloping 0.115 m per m (6.6°), y 2.0 at 17 m inland of the shoreline. Shoreline z −105 at x 0 and z −101.2 at x 30 |
| Berth (Unity addition) | Sea bed kept at ≤ −1.5 m over x 1.6 … 6.2, z −129 … −116 (3 m blend). The hull bottom is at −0.85, so it clears the bed by at least 0.65 m |
| Dock landing (Unity addition) | Sand raised to y 1.25 under x −2.4 … 2.4, z −101 … −97 (3 m blend). Stepping off the deck is now ≤ 0.1 m, down from 0.33 m |
| Ramp hole | Terrain hole over x −1.5 … 1.5, z 52.4 … 60.2: the part of the ramp whose roof (floor + 3.0 + 0.4) is above the plateau. Computed from `BunkerLayout.asset`. The 0.78 m grid cuts 44 cells (x ±1.56, z 52.03 … 60.63); the overrun is inside the ramp and lobby walls |
| Boundary | `Terrain/IslandBoundary`: 180 invisible BoxColliders, 1 m thick, y −8 … 30, 841 m around. They sit on the 0.6 m water-depth line, traced every 2° from (0, 28). They go round x −10 … 12, z −132 … −92, so the dock, boat and boat zone stay inside |
| Water | `Beach/Water`: 520 × 520 m plane at y 0 centred on (0, 20). Transparent placeholder material, no collider, no shadows |
| Dock | `Beach/Dock`: deck x ±1.4, z −128 … −97, top 1.3, 0.35 thick. Posts Ø 0.3 m every 3 m at x ±1.5 (z −98 … −128), from y −4. Rails at deck + 0.8 … 0.9. The east rail is open from z −119 to −128 beside the boat, with short bollards (deck + 0.25) at z −122 and −125 |
| Boat | `Beach/Boat` at (3.7, 0.15, −122.5), yaw −2.9°, bow to the south, motor toward the shore. Hull 2.9 m wide, deck y 0.7. Fenders on the dock (west) side. Lantern at about (2.6, 2.6, −124.3). Its light (W7) goes just above it; the reference's (2.5, 2.75, −124.2) ignores the boat's yaw. Colliders on the hull, sides, cabin and roof only. Keep it dynamic (it will bob) |

Placeholder materials are in `Assets/Materials/Environment/Greybox`: Water, DockWood, DockWoodDark,
BoatHull, BoatTrim and BoatDark. They stay for now: the W5 library has no wood, water or boat-paint surface (see
"Materials (W5)"). Static flags are set in W7.

## Bunker blockout (W3)

Built by **Blackout/World/Build Bunker Blockout** (`Assets/Editor/WorldBuilder/BunkerBlockoutBuilder.cs`) from
`BunkerLayout.asset`. The geometry rules are in `BunkerShell.cs` and are checked by EditMode tests in
`Tests/BunkerShellTests.cs`. The same menu item also builds the markers (next section). A re-run leaves the scene
byte-identical. It rebuilds only the parts it generated (named `<Room>_...`); anything else under a room is kept.

| Item | Value |
| --- | --- |
| Parts | 51 cubes under `Bunker/<Room>`: lobby 12, ramp 4, SecurityArea 12, Corridor 5, GeneratorRoom 9, Laboratory 9 |
| Floor and ceiling | 0.4 m slabs over the clear interior only |
| Walls | 0.4 m bands just outside the interior, from the bottom of the floor slab (floor − 0.4) to the top of the ceiling slab (floor + height + 0.4). North and south walls include the corners |
| Openings | A gap from the floor up to the opening height, with a lintel above it and a sill (floor − 0.4 … floor) below it, so the floor runs straight through. No door geometry: doors go at the DoorSocket markers |
| Shared walls | Built once: by the taller wall, else the longer wall, else the room listed first. SecurityArea and Laboratory build the corridor's end walls; the lobby and SecurityArea build the ramp's. The corridor's west wall stops at z 95.6 and 116.4, where the GeneratorRoom's east wall and its corners take over |
| Ramp | Floor and ceiling are cubes tilted 23.56° (18.76 m long). The ceiling is 3.0 m above the floor, measured vertically. The side walls are upright from y 2.1 to 13.4, as in the prototype. The square-cut slab ends tuck up to 0.16 m into the neighbouring sill and lintel, out of sight |
| Colliders and static flags | Every part keeps its BoxCollider. Static flags: Contribute GI, Occluder, Occludee, Batching and Reflection Probe. The NavMesh comes from a NavMeshSurface in W4, so the old Navigation Static flag is not used |
| Materials | One per room, in `Assets/Materials/Environment/Greybox`, using palette colours: `Greybox_Bunker_BunkerEntranceLobby` #86857D, `..._BunkerEntranceRamp` #A27B1D, `..._SecurityArea` #AEB9C4, `..._Corridor` #6C6E6A, `..._GeneratorRoom` #6A7350, `..._Laboratory` #E9EEEF. Build Bunker Blockout still assigns them; Apply Materials (W5) then replaces them |
| Barricade crate space | Corridor, 1.5 m cube at (0.7, 2.5, 95). Nothing is built there, and no marker is within 0.4 m of it |

**Known issues** (fixed in the entrance-building step):
- The lobby floor and the D-Bunker sill are at y 10.0, exactly the plateau's height, so they can flicker against
  the terrain.
- The ramp's side walls and roof stick out of the plateau between z 52.4 and 62.

## Gameplay markers (W3)

Built by the same menu item (`MarkerBuilder.cs`). The table of markers is `MarkerLayout.cs`, checked by
`Tests/MarkerLayoutTests.cs`.
- **Marker component:** each marker is an empty GameObject with a `Blackout.Shared.Marker`.
- **id and owner:** its id is its GameObject name. Its owner is the agent's name on AgentSpawn and Waypoint
  markers, and empty on all others.
- **Hierarchy:** PlayerStart, GeneratorSocket, CureSocket and BoatExit sit directly under `Markers`. The others are
  in one group per type, for example `Markers/Checkpoint/Checkpoint_Beach`.
- **Height:** y is the walking surface: the dock deck (1.3), a room's floor, or the terrain.
- **Yaw:** 0 is north (+Z) and 90 is east. A marker's forward (+Z) is its facing.
- **Labels:** each marker has a Scene-view label, coloured by type.

Code finds markers at runtime with `FindObjectsByType<Marker>`. That order is not route order, so sort an agent's
waypoints by id.

| Marker | Position (x, y, z) | Yaw | Notes |
| --- | --- | --- | --- |
| PlayerStart | (0, 1.3, −121) | 0 | On the dock |
| Checkpoint_Beach | (0, 1.3, −121) | 0 | Same place as the start |
| Checkpoint_Forest | (0, 1.77, −90) | 0 | |
| Checkpoint_BunkerEntrance | (0, 10.04, 33) | 0 | Compound, inside the gate |
| Checkpoint_GeneratorRoom | (−4.5, 2.5, 106) | 270 | Facing the generator |
| Checkpoint_EscapeEntrance | (0, 10, 40) | 180 | Escape: so a death near the boat does not send the player back underground |
| Checkpoint_EscapeForest | (0, 10.04, 24) | 180 | Escape |
| GeneratorSocket | (−11, 2.5, 106) | 0 | Floor level, long side along X. The reference's 0.3 m plinth is not built yet |
| CureSocket | (0, 2.5, 135) | 180 | Floor level, at the centre of the containment chamber; the opening faces D-Lab |
| HealthPickup_Security | (6.5, 2.5, 80.5) | 0 | |
| HealthPickup_Corridor | (0.9, 2.5, 119) | 0 | x 1.2 → 0.9 (corridor pillars) |
| HealthPickup_Catwalk | (−18.5, 5.5, 114.5) | 0 | Catwalk deck height. Out of reach until the catwalk is built (W10) |
| BoatExit | (0, 1.3, −124) | 0 | Centre of the 6 m exit zone |
| DoorSocket_D-Bunker | (0, 10, 48.2) | 0 | Gap 1.4 × 2.3 m. Sliding, unlocked |
| DoorSocket_D-Security | (0, 2.5, 82.2) | 0 | Sliding, unlocked |
| DoorSocket_D-Generator | (−1.7, 2.5, 106) | 270 | Hinged, unlocked |
| DoorSocket_D-Lab | (0, 2.5, 123.8) | 0 | Sliding, maglocked until `GeneratorDisabled` |

DoorSockets are at floor level in the middle of the wall. They face into the room you enter on the way in. Only
door gaps get one; the open ramp passages (O-RampTop, O-RampBottom) have no door.

**Agents.** Agent names follow the prototype and the Agent Controller's `AgentRoot` (for example `Patrol-1`). An
agent's AgentSpawn is `AgentSpawn_<agent>`. Its waypoints are `Waypoint_<agent>_01`, `_02` …, in route order, and
every route is a loop.
- **Spawn facing:** an agent with a route faces its second waypoint. Guards on a post face their room's door. The
  Commander faces south.
- **Patrol-3:** its route is a ping-pong in the reference, so it is stored as 4 points.
- **Heights:** outdoor heights come from the terrain, for example Hunter-1's spawn is at y 4.59. Patrol-1's
  compound points are at y 10.0.

| Agent | Spawn (x, z), yaw | Waypoints (x, z) in order |
| --- | --- | --- |
| Hunter-1 | (−24, −54), 346 | (−24, −54) (−30, −30) (−18, −12) (−26, 8) (−14, −70) (−36, −44) |
| Hunter-2 | (26, −20), 171 | (26, −20) (30, −44) (20, −64) (32, −2) (18, 14) (36, −30) |
| Patrol-1 | (−14, 36), 90 | (−14, 36) (14, 36) (16, 46) (10, 55) (−10, 55) (−16, 46) |
| Patrol-2 | (3, 74), 270 | (3, 74) (−4, 74) (−4, 79.5) (3, 79.5) |
| Patrol-3 | (0, 88), 0 | (0, 88) (0, 102) (−0.5, 120) (0, 102) |
| TG-01 | (−8, 100), 45 | post (−8, 100) |
| TG-02 | (3, 129), 210 | post (2.5, 129) |
| Commander | (6, 138), 180 | post (6, 138) |

**Cover points** (CoverPoint_01 … 19, y 2.5, from the reference):

| Ids | Where | Points (x, z) |
| --- | --- | --- |
| 01–04 | Generator corners | (−14.8, 101.2) (−7.2, 101.2) (−14.8, 110.8) (−7.2, 110.8) |
| 05–12 | Generator room | (−17.5, 99) (−4.5, 113.5) (−18, 110) (−11, 109.4) (−15.5, 108.6) (−16.8, 106) (−9, 97.5) (−13, 97.5) |
| 13–16 | Lab benches | (−6.5, 130.5) (6.5, 130.5) (−6.5, 137) (6.5, 137) |
| 17–19 | Corridor pillars | (−0.9, 92.5) (0.9, 110) (−0.9, 117) |

Every marker inside the bunker, apart from the door sockets, is at least 0.4 m (the agent radius) from the walls.

## NavMesh and layout validation (W4): Layout freeze v1

**Blackout/World/Bake NavMesh** (`Assets/Editor/WorldBuilder/NavMeshBaker.cs`) bakes the NavMesh.
**Blackout/World/Validate Layout** (`LayoutValidator.cs`, with the table in `LayoutReport.cs`) checks it.
The tests are `Tests/NavMeshBakerTests.cs` and `Tests/LayoutValidatorTests.cs`.

**Layout freeze v1:** from now on, tell the group chat before changing geometry or markers. Then run Bake NavMesh
and Validate Layout, and commit only when the table passes.

| Item | Value |
| --- | --- |
| Agent | Agent type 0 (Humanoid): radius 0.4 m, height 1.8 m, step 0.4 m, max slope 40°. The bake tool sets these in code, because Project Settings > Navigation (Systems Engineer) still has Humanoid at 0.5 / 2 / 0.75 / 45. NavMesh queries that name no agent type use type 0, so they get this mesh |
| Other settings | Unity's defaults: 0.133 m voxels (radius / 3), 256-voxel tiles, regions under 2 m² removed, no height mesh |
| Geometry | The physics colliders under every root of `Environment.unity`: terrain, dock and bunker blockout, 93 colliders. Left out: `Terrain/IslandBoundary` (offshore walls), `Beach/Boat` (it bobs) and trigger colliders |
| Bounds | The terrain's footprint: x −200 … 200, z −180 … 220, y −10 … 26 |
| Data | `Assets/Environment/NavMesh/EnvironmentNavMesh.asset`: binary, 416 KB, 2030 triangles, about 155 000 m² walkable. The bake takes about 1.7 s. A re-bake overwrites the asset in place (same GUID) and is byte-identical |
| In the scene | The root `NavMesh` sits at the origin. Its NavMeshSurface loads the data whenever the scene loads, additively too. **Do not press Bake on that component**: it would rebake with the Project Settings agent size and save the data somewhere else. Validate Layout fails if that happens |
| Doors | There are no doors in this scene, so the NavMesh runs through every door gap. Closed and locked doors must block agents at runtime (see the changes table below) |

**Checks.** Validate Layout prints one row per check. It logs an error if any row fails and, in batch mode, exits
with code 1.
- The NavMesh is loaded and comes from Bake NavMesh.
- Every PlayerStart, AgentSpawn, Waypoint, CoverPoint and BoatExit marker is within 1 m of the NavMesh
  (`NavMesh.SamplePosition`).
- Complete paths exist from PlayerStart to CureSocket and from CureSocket to BoatExit (`NavMesh.CalculatePath`).
- Every door gap (DoorSocket) is at least 1.2 m wide: the 0.8 m agent plus 0.2 m each side. The width is the
  narrowest of rays to the jambs at 0.2, 0.9 and 1.7 m above the floor. Only colliders in this scene count.

**Result for this freeze:** PASS, 65 of 65 checks.

| Check | Rows | Measured |
| --- | --- | --- |
| NavMesh from Bake NavMesh | 1 | `EnvironmentNavMesh.asset`, 2030 triangles |
| Marker within 1 m of the NavMesh | 58 (PlayerStart 1, AgentSpawn 8, Waypoint 29, CoverPoint 19, BoatExit 1) | 0.02 … 0.16 m (the largest is Waypoint_Hunter-2_03) |
| Path PlayerStart → CureSocket | 1 | complete, 256.0 m, 2 corners |
| Path CureSocket → BoatExit | 1 | complete, 259.0 m, 2 corners |
| Door gap at least 1.2 m | 4 | 1.40 m at D-Bunker, D-Security, D-Generator and D-Lab |

Each path has only 2 corners because every door on the route is at x = 0, so seen from above it is one straight
line. Unity's path corners ignore height, so these lengths are straight lines from above. The real walk is longer:
it climbs to the plateau at y 10 and goes down the ramp.

## Materials (W5)

Three tools, all re-runnable, with tests in `Tests/EnvironmentTextureImportTests.cs`, `Tests/MaterialLibraryTests.cs`
and `Tests/MaterialApplierTests.cs`:
- **Texture import rules** (`EnvironmentTextureImport.cs`) run automatically on every texture under
  `Assets/Textures/Environment`.
- **Blackout/World/Build Material Library** (`MaterialLibraryBuilder.cs`) makes `Env_<Surface>.mat` in
  `Assets/Materials/Environment` from the table in `SurfaceLibrary.cs`.
- **Blackout/World/Apply Materials** (`MaterialApplier.cs`) gives every bunker blockout part its material, using the
  rules in `MaterialRules.cs` and a box mesh from `BoxUvMesh.cs`.

**Order.** Run Build Material Library, then Apply Materials. Build Bunker Blockout puts the greybox materials back,
so run Apply Materials again after it. After adding or changing textures, only Build Material Library is needed
(the scene keeps pointing at the same material files). The materials are regenerated in full on every run: change
`SurfaceLibrary.cs`, not the `.mat` files.

### Surfaces

Every value comes from the reference's palette (section 3). **Flat colour** is used while a surface has no
BaseColor texture: the middle of the reference's dark → light albedo range, times its tint. Smoothness is
1 − the reference's roughness. **Repeats per metre** is the material's tiling, taken from the prototype's world-UV
scale (`worldUV` in `geometry.js`, and the terrain shader for the outdoor four). With a BaseColor texture the
base colour becomes the tint, which is white except for Rock (#B8B8B4) and Tile (#D8DCDC).

| Surface | From the reference | Flat colour | Smoothness / metallic | Repeats per metre | Applied to |
| --- | --- | --- | --- | --- | --- |
| Sand | sand (terrain) | #847860 | 0.08 / 0 | 0.22 | — (terrain, W6) |
| Grass | grass (terrain) | #2C3A1D | 0.08 / 0 | 0.30 | — (terrain, W6) |
| ForestFloor | forest litter (terrain) | #32281A | 0.08 / 0 | 0.28 | — (terrain, W6) |
| Rock | rock, tint #B8B8B4 | #43423D | 0.20 / 0 | 0.12 | — (terrain and boulders, W6) |
| Concrete | concrete, wallPanel | #72726B | 0 / 0 | 0.3 | ceilings, walls and lintels (27 parts) |
| ConcreteStained | floorConcrete (oil stains) | #5C5E5A | 0 / 0 | 0.25 | floors of the lobby, SecurityArea, Corridor and GeneratorRoom (4) |
| MetalPlate | metalPlate, metal, diamond plate | #5A5E60 | 0 / 0.65 | 0.6 | ramp floor, GeneratorRoom walls and lintel (7) |
| Rust | rust | #64361C | 0 / 0.3 | 0.5 (chosen: only on cylinders in the prototype) | — (props, W10) |
| Tile | tiles, tint #D8DCDC | #AAB3B4 | 0 / 0 | 0.25 | Laboratory floor, walls and lintel (7) |
| HazardStripe | hazard | #D9A521 | 0.25 / 0 | 1.5 | the 6 sills: every door and ramp passage threshold |
| Glass | glass: transparent, alpha 0.12, double-sided | #9FC9D0 | 0.94 / 0.2 | 1.0 (chosen) | — (W10) |
| Fabric | sandbag / rope | #7D7158 | 0 / 0 | 1.0 (chosen) | — (W10) |

If a texture covers a different real size than the prototype's, change its repeats per metre in `SurfaceLibrary.cs`.

### Textures

Put each set in `Assets/Textures/Environment/<Surface>/`, named `<Surface>_<Map>`, for example
`Concrete_BaseColor.jpg`. The maps are `BaseColor`, `Normal` (OpenGL style), `Roughness`, `Metallic` and `AO`;
any can be left out. Record every file in `docs/credits/world-builder.md`, which also lists how to rename
ambientCG and Poly Haven files.

| Import rule | Value |
| --- | --- |
| Max size | 1024; 2048 only when the name ends in `_Hero` |
| Compression | on (Normal Quality), no crunch |
| Mipmaps | on |
| Normal maps | files named `_Normal` are imported as normal maps |
| sRGB | on for BaseColor only; Roughness, Metallic, AO and MetallicSmoothness are data, so linear |

URP Lit has no roughness slot: it reads metallic from red and smoothness from alpha of one texture. Build Material
Library packs Roughness and Metallic (PNG or JPG) into a generated `<Surface>_MetallicSmoothness.png`. A missing map
is filled with the surface's constant. The file is rewritten only when its pixels change, and it should not be edited
by hand.

### Apply Materials

| Rule (first match wins) | Surface |
| --- | --- |
| Any room, `Sill_*` | HazardStripe |
| Laboratory `Floor`, `Wall_*`, `Lintel_*` | Tile |
| BunkerEntranceRamp `Floor` | MetalPlate |
| GeneratorRoom `Wall_*`, `Lintel_*` | MetalPlate |
| Any room, `Floor` | ConcreteStained |
| Any room, `Ceiling`, `Wall_*`, `Lintel_*` | Concrete |

**World-metre UVs.** The cube primitive has 0–1 UVs on every face, so one texture would stretch over the 41 m
corridor wall and squash onto a 0.4 m sill. Apply Materials gives each part its own unit box mesh
(`<part>_BoxUV`, stored in the scene) whose UVs are world positions in metres. Each face is projected along its
main world axis, as the prototype's `applyWorldUV` does: top and bottom use (x, z), faces along X use (z, y) and faces
along Z use (x, y). Tiling is then repeats per metre on every part, and textures line up across neighbouring parts.
UV2 (for the W7 lightmaps) is unwrapped at each part's real size. The part's scale, rotation, BoxCollider and static
flags are unchanged, so the NavMesh does not need a rebake.

**Result:** 51 parts (Concrete 27, Tile 7, MetalPlate 7, HazardStripe 6, ConcreteStained 4) and 51 meshes, which
add about 290 KB to `Environment.unity`. A re-run leaves the scene byte-identical, and Validate Layout still passes
65 of 65.

**Not covered yet:** the dock, boat and water keep their greybox materials. The terrain uses Sand, Grass,
ForestFloor and Rock through terrain layers in W6, and Rust, Glass and Fabric are for props in W10.

**Known issues**
- A roughness map larger than 1024 (or 2048 for `_Hero`) is shrunk by bilinear sampling, which can alias. Download
  the 1K or 2K version.
- `.gitattributes` sends `*.png` through Git LFS but not `*.jpg`, so JPG textures are stored in plain Git.

## Changes from the prototype, and who they affect

| Change | Effect | Owner to tell |
| --- | --- | --- |
| Corridor 4 m → 3 m (x ±1.5) | D-Generator moves from x −2.2 to −1.7. The GeneratorRoom east edge moves from −2.4 to −1.9 (room 17.6 → 18 m wide) | World Builder (this layout) |
| Corridor pillars become 0.3 m deep (x ±1.2 … ±1.5) | Corridor cover points at x ±1.2 fall inside pillars; use x ±0.9 | Agent Controller (cover points) |
| Corridor narrower | Pushable crate (1.5 m) at x 0.95 would hit the wall; use x ≈ 0.7 | Systems Engineer (crate) |
| All doors 1.4 × 2.3 | Door prefabs and door colliders get smaller | Systems Engineer (doors) |
| Lab ceiling 4.0 → 3.0 | The containment chamber must be under 2.9 m including its base (it was 3.25 m) | World Builder (later step) |
| Lobby redesigned as 11.2 × 3.6 m | The prototype's lobby desk and lockers were hidden behind the back wall; they move into the lobby | World Builder (later step) |
| Ramp 20 m at 20.6° → 17.2 m at 23.6° | Still under the default 45° CharacterController and NavMesh slope limits | — |
| Sand raised under the dock root (W2) | Stepping between the dock and the beach is ≤ 0.1 m, so the default 0.3 m CharacterController step offset works | Systems Engineer (player controller), for information |
| Berth dredged to −1.5 m (W2) | The boat hull clears the sea bed by at least 0.65 m | — |
| Terrain hole over the ramp top (W2) | The ramp is open from above at z 52.4 … 60.2 until the entrance building (later W step) covers it | World Builder (later step) |
| Invisible boundary walls (W2) | On the Default layer, so they also stop raycasts, but they are all offshore | Systems Engineer (layers), only if a separate layer is wanted |
| Corridor points at x ±1.2 move to ±0.9 (W3) | Applied to CoverPoint_17 … 19 and HealthPickup_Corridor, clear of the pillars | Agent Controller and Systems Engineer, for information |
| Patrol-3's ping-pong route stored as a 4-point loop (W3) | Every route is a loop: walk the waypoints in id order and wrap | Agent Controller |
| Generator plinth not built yet (W3) | GeneratorSocket is at floor level (y 2.5); the reference's generator origin sits on a 0.3 m plinth at y 2.8 | Systems Engineer and Core Developer (generator prefab) |
| Agent size set by the bake tool, not Project Settings (W4) | The baked data uses 0.4 / 1.8 / 0.4 / 40 on agent type 0, while Humanoid in Project Settings > Navigation still shows 0.5 / 2 / 0.75 / 45 | Systems Engineer (optional: set Humanoid to match, so the NavMeshSurface Inspector shows the same size); Agent Controller, for information |
| NavMesh runs through the door gaps (W4) | Doors are in another scene, so agents would path through a closed or locked door, for example D-Lab before the blackout | Systems Engineer (doors): block agents while a door is shut, for example with a carving NavMeshObstacle |
| Step height 0.4 m (W4) | The catwalk stairs' 0.3 m risers would be climbable by agents, but the reference says agents cannot climb them | World Builder (W10: mark the stairs not walkable when the catwalk is built) |
| One material per surface, not a tint per room (W5) | The reference tints the same wall panel per room (security #AEB9C4, generator #9A968A). The library has one material per surface, so rooms differ by surface instead: Concrete walls in most rooms, MetalPlate in the GeneratorRoom, Tile in the Laboratory | — |
| Door sills are HazardStripe (W5) | A yellow 0.4 m strip runs across the floor under every door and ramp passage, where the door leaf will stand | Systems Engineer (doors), for information |

Outdoor zones (Beach, Forest, compound yard) keep the prototype's positions. The terrain only adds the
berth, the dock landing and the ramp hole (see "Terrain, beach and boundary").
