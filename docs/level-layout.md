# Level layout (World Builder)

This is the real-scale layout for `Assets/Scenes/Environment.unity`. It is built from
`docs/reference/world-builder.md`. The bunker rooms in the tables below are also stored in
`Assets/ScriptableObjects/Layout/BunkerLayout.asset`, filled by
`Assets/Editor/WorldBuilder/BunkerLayoutAssetBuilder.cs`. The marker positions are also in
`Assets/Editor/WorldBuilder/MarkerLayout.cs`, the agent size in `Assets/Editor/WorldBuilder/NavMeshBaker.cs`, and the
materials in `Assets/Editor/WorldBuilder/SurfaceLibrary.cs` and `MaterialRules.cs`, the vegetation in
`VegetationLayout.cs`, `TerrainSplat.cs` and `TerrainPainter.cs`, the lighting in `LightingLayout.cs`,
`LightFixtureLayout.cs`, `ProbeLayout.cs` and `StaticFlagRules.cs`, the blackout in `BlackoutLightLayout.cs`,
and the post-processing and particles in `PostProcessLayout.cs` and `AtmosphereLayout.cs`.
Keep those numbers in sync with their code. The exception is the marked W10 dressing table below:
`DressingLayout.cs` reads that table directly; `BunkerDresser.cs` places its props.

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
| Terrain | `Terrain/IslandTerrain`: 400 × 400 m (x −200 … 200, z −180 … 220), y −8 … 24, heightmap 513² (0.78 m). TerrainData `Assets/Environment/Terrain/IslandTerrainData.asset` (binary). URP Terrain Lit. Painted with 4 terrain layers, trees and details in W6 (see "Terrain painting and vegetation") |
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
| Boat | `Beach/Boat` at (3.7, 0.15, −122.5), yaw −2.9°, bow to the south, motor toward the shore. Hull 2.9 m wide, deck y 0.7. Fenders on the dock (west) side. Lantern at about (2.6, 2.6, −124.3). Its light, `Lighting/OutdoorLights/OutdoorLights_BoatLantern` (W7), is 0.15 m above it at (2.6, 2.75, −124.3); the reference's (2.5, 2.75, −124.2) ignores the boat's yaw. Colliders on the hull, sides, cabin and roof only. Keep it dynamic (it will bob) |

Placeholder materials are in `Assets/Materials/Environment/Greybox`: Water, DockWood, DockWoodDark,
BoatHull, BoatTrim and BoatDark. They stay for now: the W5 library has no wood, water or boat-paint surface (see
"Materials (W5)"). Static flags (W7): the dock and the terrain are fully static and lightmapped, the water is only
Occludee and Batching static, and the boat stays dynamic (see "Powered lighting (W7)").

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
| HealthPickup_Catwalk | (−18.5, 5.5, 114.5) | 0 | Catwalk deck height. Still out of reach: W10's dressing does not build a catwalk |
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
| Geometry | The physics colliders under every root of `Environment.unity`: terrain, dock and bunker blockout, 93 colliders. Added (W6): every terrain tree or boulder whose prefab has a CapsuleCollider, as a Not Walkable capsule (604), because Unity's collection ignores terrain tree colliders. Left out: `Terrain/IslandBoundary` (offshore walls), `Beach/Boat` (it bobs) and trigger colliders |
| Bounds | The terrain's footprint: x −200 … 200, z −180 … 220, y −10 … 26 |
| Data | `Assets/Environment/NavMesh/EnvironmentNavMesh.asset`: binary. At the freeze: 416 KB, 2030 triangles, about 155 000 m² walkable. Since W6: 983 KB, 8144 triangles, 154 215 m² walkable. The bake takes about 1.2 to 1.7 s. A re-bake overwrites the asset in place (same GUID) and is byte-identical |
| In the scene | The root `NavMesh` sits at the origin. Its NavMeshSurface loads the data whenever the scene loads, additively too. **Do not press Bake on that component**: it would rebake with the Project Settings agent size and save the data somewhere else. Validate Layout fails if that happens |
| Doors | There are no doors in this scene, so the NavMesh runs through every door gap. Closed and locked doors must block agents at runtime (see the changes table below) |

**Checks.** Validate Layout prints one row per check. It logs an error if any row fails and, in batch mode, exits
with code 1.
- The NavMesh is loaded and comes from Bake NavMesh.
- Every PlayerStart, AgentSpawn, Waypoint, CoverPoint and BoatExit marker is within 1 m of the NavMesh
  (`NavMesh.SamplePosition`).
- Complete paths exist from PlayerStart to CureSocket and from CureSocket to BoatExit (`NavMesh.CalculatePath`).
- Every pine and palm trunk on the terrain is cut out of the NavMesh: no NavMesh within 0.3 m of a trunk's foot (W6, one row).
- Every door gap (DoorSocket) is at least 1.2 m wide: the 0.8 m agent plus 0.2 m each side. The width is the
  narrowest of rays to the jambs at 0.2, 0.9 and 1.7 m above the floor. Only colliders in this scene count.

**Result for this freeze:** PASS, 65 of 65 checks. After W6 (forest painted, NavMesh rebaked): PASS, 66 of 66, with every marker within 0.17 m of the NavMesh and both paths unchanged. After W7 (Setup Lighting, no colliders changed): still PASS, 66 of 66.

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
| Sand | sand (terrain) | #847860 | 0.08 / 0 | 0.22 | terrain layer TL_Sand (W6) |
| Grass | grass (terrain) | #2C3A1D | 0.08 / 0 | 0.30 | terrain layer TL_Grass (W6) |
| ForestFloor | forest litter (terrain) | #32281A | 0.08 / 0 | 0.28 | terrain layer TL_ForestFloor (W6) |
| Rock | rock, tint #B8B8B4 | #43423D | 0.20 / 0 | 0.12 | terrain layer TL_Rock and the rock prefabs (W6) |
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
ForestFloor and Rock through terrain layers (W6, next section). Rust, Glass and Fabric remain available for future
environment props; W10's linked props retain the Core Developer's own materials.

**Known issues**
- A roughness map larger than 1024 (or 2048 for `_Hero`) is shrunk by bilinear sampling, which can alias. Download
  the 1K or 2K version.
- `.gitattributes` sends `*.png` through Git LFS but not `*.jpg`, so JPG textures are stored in plain Git.

## Terrain painting and vegetation (W6)

Two tools, both re-runnable and deterministic (fixed seeds). A re-run with no code change leaves every file
byte-identical. The tests are in `Tests/VegetationScatterTests.cs`, `TerrainSplatTests.cs`, `VegetationMeshesTests.cs`,
`TerrainPainterTests.cs`, `VegetationBudgetTests.cs` and `NavMeshBakerTests.cs`.
- **Blackout/World/Build Vegetation Placeholders** (`VegetationPlaceholderBuilder.cs`, meshes from
  `VegetationMeshes.cs`) writes stand-in prefabs to `Assets/Environment/Vegetation/Placeholder`.
- **Blackout/World/Paint Terrain And Forest** (`TerrainPainter.cs`) paints the terrain layers (rules in
  `TerrainSplat.cs`). It plants trees and details where `VegetationScatter.cs` puts them (numbers in
  `VegetationLayout.cs`), sets the draw distances, and prints the triangle report (`VegetationBudget.cs`).

**Order.** Build Terrain, then Paint Terrain And Forest, then Bake NavMesh, then Validate Layout. Re-run Paint after any
change to the heights, because tree heights are stored when they are planted. Paint overwrites hand painting on the
terrain and asks first in the Editor.

### Terrain layers

URP Terrain Lit draws 4 layers in one pass, so the prototype's 6 ground colours use 4 layers. The trail and the compound
gravel are fixed mixes of them. The rules port the prototype's terrain shader (`terrain.js`). Each step blends toward
its colour, in this order.

| Step | Layer or mix | Where | Tiling (m per repeat) |
| --- | --- | --- | --- |
| 1 | Grass, ForestFloor | Grass everywhere. Forest floor under every tree (3.2 m × scale) and bush (1.6 m × scale), 55 %, fading over 2 m; a slow noise moves the edge | 3.33, 3.57 |
| 2 | Sand | Below 1.9 … 2.8 m on the beach (south of z −60), and below 0.4 … 1.4 m on every shore | 4.55 |
| 3 | Trail: Sand 0.5 + ForestFloor 0.5 | The 3.4 m trail, fading 0.9 … 2.5 m from its centre line; 70 % weaker on sand | — |
| 4 | Gravel: Rock 0.7 + Sand 0.3 | Compound x −21 … 21, z 29 … 62, and a strip through the gate (x −3 … 3, z 24 … 32), 1.5 m blend | — |
| 5 | Rock | Slope (1 − normal y) from 0.30 to 0.48, about 46° to 59°: cliffs and steep banks | 8.33 |

- The prototype's dirt is about 35 % sand. The trail uses 50 % so it is about 50 % brighter than the grass and easy to
  follow at night. The gravel mix matches the prototype's darkened concrete (#56544B).
- Layers are `Assets/Environment/Terrain/Layers/TL_<Surface>.terrainlayer`. A layer uses its surface's W5 BaseColor and
  Normal textures when they exist. Until then it is a white texture times the flat colour (#847860, #2C3A1D, #32281A,
  #43423D), stored in linear space because URP Terrain Lit multiplies by it as it is. Smoothness and metallic are the
  surface's constants; no mask maps.
- The splat map is 512² (0.78 m per texel, like the heightmap). Share of the land above water: Sand 6.3 %, Grass 61.1 %,
  ForestFloor 23.1 %, Rock 9.4 %.

### Vegetation

The scatter ports the prototype's `Forest.place()` step by step, with the same seeds and random generator (mulberry32).
The prototype measures ground height and trail distance on a 2 m grid, while Unity uses the exact height field. That
moves 4 pines and 4 palms: on exact values the prototype's own code also gives 461 and 69.

| Kind | Drawn as | Prototype → before clearings → planted | Rules (Unity coordinates) | Scale | Collider |
| --- | --- | --- | --- | --- | --- |
| Pine | terrain tree | 465 → 461 → **431** | jittered 4.6 m grid, x −118 … 118, z −100 … 175; ground ≥ 1 m, ≥ 3.4 m from the trail, slope ≤ 37°; denser near the trail, half on the hill, 30 % in noise clearings | 0.78 … 1.28 | capsule r 0.3 × 10 m |
| Palm | terrain tree | 65 → 69 → **63** | low ground by the beach (below 3.2 m, z < −60, 45 % kept), or 18 % of trees below 5.5 m with z < −35 | 0.78 … 1.28 | capsule r 0.25 × 8 m |
| Rock | terrain tree (Rock or RockSmall) | 170 → 170 → **163** (110 Rock, 53 RockSmall) | boulders 1.6 … 4.2 within 6 m of the cliff line, small rocks 0.3 … 1.5 elsewhere (45 % kept); ≥ 3 m from the trail, not in the compound | 0.3 … 4.2 | capsule r 0.8 × 1.6 m if scale > 0.7 |
| Bush | terrain detail | 420 → 420 → **376** | 3.8 … 45 m from the trail, ground ≥ 1.6 m | 0.6 … 1.4 | none (soft, as in the prototype) |
| Grass tuft | terrain detail | 6000 → 6000 → **6000** | 1.4 … 38 m from the trail, ground ≥ 1.3 m, in noise clumps or within 4 m of the trail | 0.6 … 1.25 | none |

- **Hunter clearings (Unity addition):** no tree, rock or bush within 6 m of the 12 Hunter route points (each Hunter's
  spawn is its first waypoint). This removed 30 pines, 6 palms, 7 rocks and 44 bushes; grass stays. A pine reaches
  2.7 m from its trunk (3.5 m at scale 1.28), so at least 2.5 m of open ground is left round each point.
- **Readable route:** nothing grows on the trail (trees 3.4 m, bushes 3.8 m, rocks 3 m from its centre line). It is
  painted as the bright dirt mix and continues as gravel through the gate. The compound plus 5 m, the cliff face behind
  it and the dock approach (|x| < 4, z < −88) stay clear.
- **Trees by area** (pines / palms; the reference's numbers in brackets): beach 34 / 54 (39 / 53), forest 267 / 9
  (297 / 12; most of the clearings are here), compound band and cliff 79 / 0 (77 / 0), hill 51 / 0 (52 / 0).
- **Details** are stored as a count per 0.78 m cell (instance-count mode, 512² cells, 32-cell patches of 25 m), and
  Unity scatters them inside their cell. They are drawn with GPU instancing and the prefab's own material.
- **Terrain settings:** trees are drawn to 150 m and details to 60 m. The outdoor fog leaves 1.7 % of a tree visible at
  150 m and 52 % of a grass tuft at 60 m, where a 0.75 m tuft is under 10 pixels tall at 1080p. Detail density 1, scaled
  by the quality level.
- **Not placed:** the prototype's 12 fallen logs; W10 covers bunker rooms only.

**Prefabs.** `VegetationLibrary` uses `Assets/Environment/Vegetation/<Name>.prefab` (Pine, Palm, Rock, RockSmall,
Bush, Grass) if it exists, else the generated `Placeholder/<Name>_Placeholder.prefab`. The placeholders port the
prototype's own models and add 2 lower LODs:

| Placeholder | Triangles LOD0 / LOD1 / LOD2 | LOD heights (screen) | Size | Material |
| --- | --- | --- | --- | --- |
| Pine | 108 / 46 / 23 | 0.25 / 0.10 / 0.02 | 9.8 m tall, 2.7 m reach | Veg_PineBark, Veg_PineNeedles (double-sided) |
| Palm | 712 / 230 / 96 | 0.25 / 0.15 / 0.02 (W11) | 7.9 m, 5.2 m reach | Veg_PalmBark, Veg_PalmLeaf (double-sided) |
| Rock, RockSmall | 180 / 80 / 20 | 0.15 / 0.05 / 0.015 | 2.3 m | Env_Rock |
| Bush | 160 | — | about 1 m tall | Veg_Bush |
| Grass | 6 (3 crossed cards) | — | 1.0 × 0.75 m | Veg_Grass (alpha cut-out, 128 px BC3 blade texture, W11) |

The colours are the middle of the reference's albedo range, times its tint and the prototype's average per-tree HSL
tint. The PC quality level has LOD bias 2, so every LOD switch happens twice as far away as these screen heights give
at the 72° FOV (the pine's LOD1 at about 59 m instead of 29 m). To use real models, see `docs/credits/world-builder.md`.

W11 changes only the generated palm's middle screen-height threshold from 0.10 to **0.15**
(`VegetationLibrary.PalmLod1Height`). **Blackout/World/Apply Palm LOD And Report** updates that one field in
`Placeholder/Palm_Placeholder.prefab`; Build Vegetation Placeholders uses the same constant. Nearby LOD0,
the final 0.02 cull threshold, meshes, material references, colliders and scatter are retained. The LOD1-to-LOD2
switch distance is two thirds of its former distance at the same instance scale, FOV and LOD bias.
The dated comparison is in `docs/viva/world-builder-optimisation.md`; distant silhouettes and popping need an Editor check.
Measured at PC LOD bias 2, the unit-scale palm's LOD1-to-LOD2 distance changes from **109.883 m to 73.255 m**.
The five-view vegetation estimates are now Dock **61,846**, Forest checkpoint **74,475**, Trail middle **60,329**,
Gate looking back **76,318** and Hunter-1 spawn **58,852** triangles. The highest falls from **80,874 to 76,318**
(about **5.6%**); detail counts and the full LOD0 geometry inventory stay unchanged. These are estimates, not GPU timings.

### Visible-triangle budget

**Budget: 250,000 vegetation triangles in the main camera's view**, from any player viewpoint, shadow passes not
counted. That leaves room in a frame of about 1.5 million triangles for the shadow passes (the moon shadow draws the
nearby trees again), the terrain, the compound, the agents and the weapons.

Paint Terrain And Forest measures it with Unity's own rules on the painted data. A tree counts if its bounds are in the
view and it is within 150 m, at the LOD that its LOD group size × scale, the 72° FOV and the quality level's LOD bias
select. Details count by whole 25 m patches in view and within 60 m, from Unity's own instance positions. The camera
is 16:9 with the eye 1.65 m above the ground. **W6 baseline before W11**, at PC LOD bias 2:

| Viewpoint | Trees LOD0 / LOD1 / LOD2 | Tree triangles | Bushes + grass | Detail triangles | Total | Share of budget |
| --- | --- | --- | --- | --- | --- | --- |
| Dock (start), north | 25 / 294 / 78 | 36,310 | 2,185 | 27,278 | 63,588 | 25.4 % |
| Forest checkpoint, north | 52 / 268 / 67 | 31,057 | 3,306 | 43,552 | 74,609 | 29.8 % |
| Trail middle (4, −44), along the trail | 75 / 160 / 51 | 18,899 | 2,901 | 41,430 | 60,329 | 24.1 % |
| Gate (0, 24), looking back south | 78 / 182 / 57 | 27,208 | 3,657 | 53,666 | **80,874** | **32.3 %** (worst) |
| Hunter-1 spawn | 76 / 171 / 53 | 21,466 | 2,689 | 37,386 | 58,852 | 23.5 % |

**For real models:** keep a tree's LOD0 at most 1,200 triangles, LOD1 at most 400 and LOD2 at most 120. At the worst
viewpoint that gives 78 × 1,200 + 182 × 400 + 57 × 120 = 173,240 tree triangles, plus 53,666 for details: about
227,000, within the budget. Re-run Paint Terrain And Forest after adding models to get the new numbers.

### NavMesh after W6

Bake NavMesh now adds the 604 tree and boulder capsules (431 pines, 63 palms, 110 rocks) as Not Walkable volumes.
Unity's own collection ignores terrain tree colliders: with the forest painted and the old baker, the bake did not
change and all 494 trunks stood on the NavMesh. The new bake has 8144 triangles and 154,215 m² walkable (923 m² less),
in 983 KB. Validate Layout passes 66 of 66, with a new row "Trunks cut out: 0 of 494 on the NavMesh".

### Known issues

- The look was not checked in batch mode: open the scene and check the colours, trees and details once.
- No wind yet. The prototype sways foliage with a vertex shader, which needs a foliage Shader Graph (later).
- The prototype's wet sand at the waterline is not a separate layer.
- Detail positions are random inside their 0.78 m cell. `useLegacyDetailDistribution` is on in the quality settings
  (Systems Engineer), so the prototypes' position-jitter setting has no effect.
- `IslandTerrainData.asset` (2.3 MB) and `EnvironmentNavMesh.asset` (983 KB) are binary and cannot be merged: only
  the World Builder regenerates them.
- The budget is computed, not measured on the GPU, and leaves out shadow passes.

## Powered lighting (W7)

**Blackout/World/Setup Lighting** (`LightingSetup.cs`) builds the pre-blackout lighting of `Environment.unity`. Its
tables are in four files: `LightingLayout.cs` (sky, fog, ambient, moon, shadows), `LightFixtureLayout.cs`
(bunker fixtures and outdoor lights), `ProbeLayout.cs` (probes) and `StaticFlagRules.cs`. The flicker is
`Assets/Scripts/Environment/FlickerLight.cs` (assembly `Blackout.Environment`). The tests are in
`Tests/LightingLayoutTests.cs`, `LightingSetupTests.cs`, `LightFixtureLayoutTests.cs`, `ProbeLayoutTests.cs`,
`StaticFlagRulesTests.cs` and `Assets/Scripts/Environment/Tests/FlickerLightTests.cs`.

**Order.** Build Terrain, Paint Terrain And Forest, Build Bunker Blockout, Apply Materials and Bake NavMesh come first.
Then run Setup Lighting and Validate Layout, and bake by hand (see "Bake settings"). Re-run Setup Lighting after
changing the bunker layout or the terrain, because the probes follow them, then bake again.
- **Re-runs:** a re-run with no code change leaves every file byte-identical. It reuses objects by name under
  `Lighting` and removes only objects it generated there.
- **What it never does:** it never bakes and never edits the URP asset.

### Sky, fog, ambient and moon

The prototype runs three.js r128 without colour management, so the reference's hex values for lights, fog, ambient and
sky are linear numbers. Unity's colour fields are sRGB, so the tool converts each one (`LightingLayout.FromLinearHex`).
The Inspector therefore shows a different hex value from the reference, while the light that reaches a surface matches
the prototype. Surface colours (W5) are albedo and are not converted.

| Item | Reference (outside column) | Unity |
| --- | --- | --- |
| Fog | exponential squared, #0B1220, density 0.0135 | Exponential Squared, #3B4B63, 0.0135. It is the same formula as `FogExp2`, so the density copies across |
| Ambient | hemisphere light, sky #52668F, ground #141820, × 0.78 | Gradient (Trilight): sky #8998B1, equator #6E798D (half sky, half ground, as a hemisphere light gives a wall), ground #464D58 |
| Moon | directional #A9BDE6 × 0.9, toward (−0.349, 0.419, 0.838) | `Lighting/Moon`: Directional, Mixed, #D5DFF4 × 0.9, rotation (24.8°, 157.4°, 0°), soft shadows. It is the scene's Sun Source |
| Sky | dome: horizon #0E1626, zenith #02040A, stars, moon disc, clouds (cover 0.5) | Skybox `Assets/Environment/Lighting/NightSky.mat`, shader `Blackout/NightSky`: a URP port of the dome |
| Reflections | night cubemap outdoors | Environment Reflections from the skybox, 128 px. Each bunker room has its own probe (below) |

The reference's "Bunker, powered" fog and ambient (density 0.022, ambient × 0.42) are not used. There is one value
for the whole scene, and indoors the bake replaces the ambient with the light that actually reaches each room.

### Intensities

The prototype fades a light as (1 − d / range)^decay, so the light stays bright almost to its range. URP uses the
inverse square, 1 / d², times (1 − (d / range)⁴)², which reaches 0 at the range. Copying the reference's intensities
as they are would make the bunker 3 to 14 times too dark on the floor, and the floodlights 250 times too dark.

`LightingLayout.MatchIntensity` picks the Unity intensity that matches the prototype's brightness at one distance:
- the floor under each lamp, at the Unity height;
- a floodlight's aim point;
- the boat deck under the lantern.

Nearer the lamp Unity is brighter than the prototype, and farther away it is darker. For example, a generator high-bay
in the prototype gives 3.0 × (1 − 5.55 / 14)^1.6 = 1.34 on the floor 5.55 m below it. Unity needs
1.34 × 5.55² / (1 − (5.55 / 14)⁴)² = **43.3** for the same.

### Powered fixtures

Each fixture is `Lighting/PoweredLights/<Room>/<Room>_<Kind>NN`: a Mixed point light plus a `_Lamp` mesh. The
prototype's decay of 1.6 is used only in the intensity conversion, because URP has no decay setting.
- **Lamp mesh:** it has no collider, casts no shadow and is not static. It uses an emissive material from
  `Assets/Materials/Environment/Lighting` at the reference's emission: `Lamp_Tube` #E6F0FF × 4.2, `Lamp_Panel` #E4F6FF
  × 3.2 and `Lamp_Sodium` #FFA555 × 6. The emission is marked real-time, so it is never baked: the Light components do
  the lighting, and the lamps only glow.
- **Rod:** each high-bay also hangs on a `_Rod`.
- **Positions:** X and Z are the reference's. Each lamp hangs below the Unity ceiling by the same drop as in the
  prototype (tube 0.1 m, panel 0.05 m, high-bay 0.7 m), and its light is 0.25 m under the lamp.

| Room | Fixtures (x, z) | Reference colour, intensity, range | Unity intensity | Light over the floor | Shadows |
| --- | --- | --- | --- | --- | --- |
| BunkerEntranceRamp | 4 tubes across the ramp, at the middle of each quarter: z 54.55, 58.85, 63.15, 67.45 | #E2ECFF, 1.2, 8 m | 3.8 | 2.65 m | Tube01 only (y 11.7, above the plateau) |
| SecurityArea | 6 tubes along Z: x −4.5 / 0 / 4.5 × z 73 / 79 | #E8F0FF, 1.5, 9 m | 5.2 | 2.65 m | none |
| Corridor | 10 tubes along Z: x 0, z 85 … 121 every 4 m | #E2ECFF, 1.35, 8.5 m | 4.7 | 2.65 m | none |
| GeneratorRoom | 6 sodium high-bays: x −16 / −11 / −6 × z 100 / 112 | #FFA555, 3.0, 14 m | 43.3 | 5.55 m | all 6 |
| Laboratory | 5 ceiling panels: x −5 / 0 / 5 × z 128 / 138, none at (0, 138) over the cure | #E4F6FF, 1.3, 9 m | 4.1 | 2.70 m | none |

**Faulty tubes.** `Corridor_Tube03` (z 93, seed 1) and `SecurityArea_Tube06` (4.5, 79; seed 2) have a `FlickerLight` in
FaultyTube mode. Each 7 s cycle has a 0.6 s strobe, 0.15 s out at 5 %, then a 97 % ± 2 % hum, and the two seeds keep
the tubes 2.3 s apart.
- **Deterministic:** the random part is a hash of the time step and the seed, so the flicker is the same on every run
  and at any frame rate. Nothing is allocated per frame.
- **Lamp:** its emission follows the light through a MaterialPropertyBlock.
- **Disabling:** disabling the component puts the light and lamp back to full strength.

### Outdoor lights

The generator only powers the bunker, so these stay on through the blackout. They are under
`Lighting/OutdoorLights/OutdoorLights_<Name>`.

| Light | Position (x, y, z) | Reference colour, intensity, range | Unity intensity | Other |
| --- | --- | --- | --- | --- |
| FloodlightW | (−19.5, 18.2, 30.5) → aim (−3, 10, 45) | #FFF0D8, 5.0, 42 m, decay 1.2 | 1265.0 | Spot 55°, inner 24.75°; shadows, Medium tier (512 px) |
| FloodlightE | (19.5, 18.2, 30.5) → (4, 10, 44) | same | 1171.0 | same |
| FloodlightN | (17.5, 17.2, 58) → (2, 10, 42) | same | 1262.4 | same |
| WallPack | (0, 12.8, 47.2) | #D8E4FF, 2.2, 12 m, decay 2 | 7.9 | Shadows, Low tier |
| BoatLantern | (2.6, 2.75, −124.3) | #FFA64D, 1.7, 15 m | 5.7 | `FlickerLight` Flame (85 % ± 10 % from two sine waves, plus ± 4 % noise), seed 3 |

- **Spot angles:** three.js gives a half angle (0.48 rad), so the full cone is 55°. The inner angle is 55° × (1 − the
  0.55 penumbra).
- **Wall pack:** it is 0.5 m over the D-Bunker door and 0.8 m out from the wall, as in the prototype. That puts it
  2.8 m over the yard instead of 3.9 m, because the Unity door is 2.3 m tall, not 3.4 m.
- **Not placed yet:** the guard booth lamp (there is no booth yet) and the mast beacon (emissive only).

### Shadows

Only the lights that would visibly shine through a wall or the ground cast shadows. Every other tube and panel casts
none, as in the prototype.

| Lights | Why | Resolution |
| --- | --- | --- |
| Moon | the island, the forest, the compound | main light: 2048 px (URP asset) |
| 6 sodium high-bays | about 9 times a tube's intensity, next to the corridor | Low tier: 256 px per cube face |
| `BunkerEntranceRamp_Tube01` | at y 11.7, above the plateau (y 10), so without shadows it would light the yard through the ground | Low |
| 3 floodlights | keep their beams out of the lobby and the ramp; one map each | Medium tier: 512 px |
| Wall pack | would light the lobby through its wall | Low |

All of them use soft shadows. In the worst case all 11 local lights with shadows are in view at once. They need
8 × 6 × 256² + 3 × 512² = 3.93 M texels, which fits the PC asset's 2048² additional-light atlas (4.19 M).

**URP asset (Systems Engineer).** Setup Lighting reads `Assets/Settings/PC_RPAsset.asset` and prints how it differs
from a medium preset. It does not edit the asset.

| Setting | Now | Medium preset |
| --- | --- | --- |
| Max Distance | 50 m | **50 m** (W11 runtime policy) |
| Cascade Count | 4 | **2**, split at 25 % |
| Main light resolution | 2048 px | 2048 px |
| Soft shadows | on | on |

- **Prototype:** its medium preset is one 2048 px moon map over an 80 × 80 m box.
- **Cascades:** the two-cascade recommendation is retained but has not been applied or profiled; the PC asset still uses four.
- **Distance:** W11 reduces runtime Medium from 60 to 50 m, matching the saved PC distance. The outdoor fog leaves
  about 63% transmittance at 50 m versus 52% at 60 m, so the nearer shadow fade needs visual review.

W11's QualityPreset changes the distance to **40 / 50 / 80 m** for Low / Medium / High at runtime.
`QualityPreset.MediumShadowDistance` also drives the Editor recommendation so they stay consistent. The saved URP
asset, cascade count and shadow-map resolutions stay as authored; shadow timing and fade quality remain manual checks.

### Probes

**Reflection probes** (`Lighting/ReflectionProbes/<Room>`): one per room, captured in the middle of the room at eye
height (1.65 m over the floor).
- **Settings:** baked, 128 px, HDR, blend distance 0.5 m, far clip 60 m (longer than the corridor).
- **Box projection:** each probe's box is the room's clear interior, so reflections line up with the walls.
- **Ramp:** its box runs from the bottom floor to the top ceiling.

| Room | Captured at | Box (X × Y × Z) |
| --- | --- | --- |
| BunkerEntranceLobby | (0, 11.65, 50.2) | 11.2 × 3.0 × 3.6 |
| BunkerEntranceRamp | (0, 7.9, 61.0) | 3.0 × 10.5 × 17.2 (y 2.5 … 13.0) |
| SecurityArea | (0, 4.15, 76) | 16 × 3.0 × 12 |
| Corridor | (0, 4.15, 103) | 3.0 × 3.0 × 41.2 |
| GeneratorRoom | (−10.9, 4.15, 106) | 18 × 6.5 × 20 |
| Laboratory | (0, 4.15, 133) | 20 × 3.0 × 18 |

**Light probes** (`Lighting/LightProbes`, 806 probes in three Light Probe Groups at the origin, with deringing on) give
moving objects the baked bounce light: the player, the agents, the doors and the boat.
- **Not Adaptive Probe Volumes:** they need Light Probe System = APV in the URP asset, which the World Builder does
  not own.

| Group | Probes | Placement |
| --- | --- | --- |
| Bunker | 504 | In every room, a grid at most 3 m apart, 0.5 m in from the walls, in layers at most 2 m apart from 0.5 m over the floor to 0.5 m under the ceiling. Two in every door gap or passage, at 0.5 m and 1.8 m, so light does not blend through a wall. None inside the generator's volume (x −14.2 … −7.8, y 2.5 … 6.1, z 104.4 … 107.6) |
| Compound | 116 | Every 5 m over the yard (x −20 … 20, z 30 … 60), except over the lobby and ramp (+ 0.9 m); 0.5 m and 2.5 m over the ground |
| Trail | 186 | The dock deck every 4 m. Then the trail every 6 m or less, on its centre line and 3 m to each side, 0.5 m and 2.5 m over the terrain |

Hunters off the trail are outside the probes and take the light of the nearest ones at the edge. The forest is lit
evenly by the moon and the sky, so that is close enough.

### Static flags

Setup Lighting sets the flags on every renderer under these objects, and on the terrain. In the first run it changed
42 of 59 beach renderers and the terrain. The bunker parts already had these flags (W3).

| Objects | Flags | Why |
| --- | --- | --- |
| `Bunker/*` (51 parts), `Beach/Dock`, `Terrain/IslandTerrain` | Contribute GI, Occluder, Occludee, Batching and Reflection Probe static | They are lightmapped, they hide what is behind them in an occlusion bake, they batch, and they show in the reflection probes |
| `Beach/Water` | Occludee and Batching static only | A 520 m see-through plane. As an occluder it would hide the sea bed and the hull under it, and in the lightmap it would take a large area for nothing |
| `Beach/Boat` | none | It bobs, so it is dynamic and takes its light from the dock's light probes |
| Lamp meshes | none | W8 changes their emission at run time |
| Fixed opaque parts under `Props/BunkerDressing` | Same as `Bunker/*` | Dress Bunker sets these flags on scene instances, preserving the source prefabs |
| Transparent dressing parts | Occludee and Batching only | They neither occlude rooms nor consume lightmap space |
| Emissive dressing parts, including monitor screens and LEDs | none | They remain outside static batching and lightmaps; monitor emission changes at the blackout |

W11 sets **Scale In Lightmap 0.5** on the **22** generated `Beach/Dock/Dock_Post_*` renderers, using
`BeachGreyboxBuilder.DockPostLightmapScale`. The deck, rails and bunker keep scale 1; the scene's GI MeshRenderer
distribution is now 22 at 0.5 and 70 at 1. The scene's lightmap resolution stays **10 texels/unit**. The generated
posts keep their GI flags, transforms, meshes, colliders and materials. **Blackout/World/Apply Dock Post Lightmap
Scale And Report** updates only their scale fields and saves Environment; no lighting bake runs. Actual occupied
texels, lightmap memory and dock lighting quality still need a before/after bake.

W11 also stores the generated `GrassBlades.asset` as **DXT5/BC3** for the Windows target, still **128 × 128**, with
**8 mip levels**, the same GUID, Clamp and Bilinear. The mip payload falls from **87,380 to 21,872 bytes**. In the
same Direct3D11 Editor context, its runtime memory estimate falls from **175,656 to 44,640 bytes**; this includes
CPU copies and overhead. **Blackout/World/Compress Grass Texture And Report** updates only that texture and the
placeholder builder uses the same compression method. Terrain control maps and the soft particle gradient stay
as authored. Check grass edges, colour and distant mips visually; recheck format support when changing target.

Everything else (markers, the NavMesh, the boundary and the `Lighting` objects) is left as it is. Occlusion culling is
not baked yet, but the flags are ready for it. The terrain's Scale In Lightmap is 0.064, so it gets 0.64 texels per
metre (1.6 m per texel). That is enough for the sky's occlusion and the moon's bounce.

### Bake settings (Window > Rendering > Lighting)

Bake only in the Editor, never in batch mode, and only the World Builder bakes (playbook trap 4). Unity 6.3 has no
Auto Generate any more, so lighting is baked only when someone clicks **Generate Lighting**.
1. Open `Environment.unity` on its own (not additively with other scenes).
2. Run Blackout/World/Setup Lighting. It writes every value below into
   `Assets/Environment/Lighting/EnvironmentLighting.lighting`, the scene's Lighting Settings asset.
3. Check the values in the **Scene** tab.

| Section | Setting | Value |
| --- | --- | --- |
| Lighting Settings | Lighting Settings Asset | `EnvironmentLighting` (do not click New) |
| Realtime Lighting | Realtime Global Illumination | off |
| Mixed Lighting | Baked Global Illumination | on |
| | Lighting Mode | **Baked Indirect** |
| Lightmapping Settings | Lightmapper | **Progressive GPU** (Unity falls back to Progressive CPU if the GPU cannot bake) |
| | Direct Samples | 32 |
| | Indirect Samples | **256** |
| | Environment Samples | 256 |
| | Light Probe Sample Multiplier | 4 |
| | Min Bounces / Max Bounces | 2 / 2 |
| | Filtering | Advanced |
| | Direct Denoiser, Filter | **OpenImageDenoise**, Gaussian, radius 1 |
| | Indirect Denoiser, Filter | **OpenImageDenoise**, Gaussian, radius 5 |
| | Ambient Occlusion Denoiser, Filter | **OpenImageDenoise**, Gaussian, radius 2 |
| | Lightmap Resolution | **10** texels per unit |
| | Lightmap Padding | 2 texels |
| | Max Lightmap Size | 1024 |
| | Lightmap Compression | Normal Quality |
| | Ambient Occlusion | on: Max Distance 1, Indirect Contribution 1, Direct Contribution 0 |
| | Directional Mode | Non-Directional |
| | Albedo Boost / Indirect Intensity | 1 / 1 |
| | Lightmap Parameters | the default (unchanged) |

The **Environment** tab is also set by the tool. Check it only:
- **Skybox Material:** `NightSky`. **Sun Source:** `Moon`.
- **Environment Lighting:** Source Gradient (sky #8998B1, equator #6E798D, ground #464D58).
- **Environment Reflections:** Source Skybox, Resolution 128, Intensity Multiplier 1, Bounces 1.
- **Other Settings:** Fog on, #3B4B63, Exponential Squared, density 0.0135.

**Why these values**
- **Baked Indirect:** direct light and shadows stay real-time, so the W8 blackout can switch every fixture off. Only
  the soft bounce is baked, so 10 texels per metre is enough (a 0.4 m wall gets 4 texels), and 1024 maps keep the
  medium preset light.
- **Shadowmask rejected:** it allows at most 4 overlapping shadowmask lights, and more than 4 corridor tubes overlap.
  The extra ones would be demoted to fully baked, which W8 cannot switch off.
- **Samples:** 256 indirect samples are enough for soft bounce light once OpenImageDenoise has filtered it. The direct
  samples only matter for Baked lights and there are none, so 32 is enough. The light probes get 4 times the samples, because probes are not
  denoised.
- **Bounces:** concrete's linear albedo is about 0.17, so a third bounce would add less than 1 %.
- **Ambient occlusion:** 1 m gives contact darkening in room corners and under the dock. It does not touch the
  real-time direct light (Direct Contribution 0).
- **Denoiser:** OpenImageDenoise does not need a particular GPU make, so any team PC can bake.

**After Generate Lighting**
- **Output:** Unity writes `Assets/Scenes/Environment/`: `LightingData.asset`, the `Lightmap-N_comp_light.exr` maps
  and the `ReflectionProbe-N.exr` cubemaps. `Environment.unity` then points at that data.
- **Before committing it:** that folder is not in the World Builder's paths yet (playbook section 2), and `.exr` is
  not in Git LFS. Ask first (see the PR handoffs), then commit the folder with its `.meta` files and the scene.

**What to check**
- **Console:** no lightmapper errors. If the GPU cannot bake, Unity switches to Progressive CPU, which is only slower.
- **Scene view, Baked Lightmap draw mode:** no black or blotchy bunker walls, which would mean overlapping UV2.
- **The look:** compare each room with the prototype's pre-blackout screenshots. The ramp, security and corridor tubes
  are cool white, the generator room has orange pools under its high-bays, and the lab panels are bright. Outside,
  the floodlights light the yard and the moon casts tree shadows.
- **Play mode:** `Corridor_Tube03` and `SecurityArea_Tube06` strobe every 7 s, and the boat lantern flickers.
- **Validate Layout:** it still passes 66 of 66, because the bake changes no collider.

### Known issues

- **The bounce stays after the blackout.** The baked bounce of the powered fixtures stays in the lightmaps and light
  probes after the blackout, and the reflection probes still show the lit rooms. The ambient is baked too, so changing
  `RenderSettings` ambient at run time hardly reaches lightmapped or probe-lit objects. W8 options:
  - bake a second "blackout" set with the fixtures off, and swap `LightmapSettings.lightmaps` and `lightProbes` on the
    event;
  - or accept a faint glow under the red emergency lights.

  Either way, lower the reflection probes' intensity at the blackout. W8 accepts the glow for now and dims the
  reflection probes to 20 % (see "Blackout lighting (W8)").
- **Indoor fog:** the bunker has the outside fog (0.0135, not the reference's 0.022) while it is powered. W8 blends
  it to the blackout fog (0.05) only after the event.
- **Light through walls:** tubes without shadows (range 8.5 to 9 m) light a few metres of the next room through its
  0.4 m wall. Examples are the corridor tubes at z 101 … 109 next to the generator room, and the end tubes next to the
  security area. If it shows after the bake, turn on Low-tier shadows for those tubes.
- **Not baked yet:** the look has only been checked in batch mode, not by eye.
- **Size:** W7 grew `Environment.unity` from 1,047 KB to 1,317 KB (37 lights, 806 probes, the lamp meshes).
- **FlickerLight and W8:** it sets the light's intensity every frame, so PowerStateLighting (W8) disables it before
  the cascade and enables it again at a reset.

## Blackout lighting (W8)

When the generator is disabled, the bunker's powered lights die one by one and red emergency lighting takes over.
- **`PowerStateLighting`** (`Assets/Scripts/Environment/PowerStateLighting.cs`, assembly `Blackout.Environment`) is a
  component on the `Lighting` root. It plays the blackout.
- **`LightingStateProfile`** (`LightingStateProfile.cs`) holds the two moods and the timings. The asset is
  `Assets/Environment/Lighting/LightingStateProfile.asset`.
- **Blackout/World/Setup Blackout Lighting** (`Assets/Editor/WorldBuilder/BlackoutLightingSetup.cs`, numbers in
  `BlackoutLightLayout.cs`) builds the emergency lights and alarm beacons, writes the profile's states and wires the
  component.

The tests are `Assets/Scripts/Environment/Tests/LightingStateProfileTests.cs` (the interpolation and every timing),
`PowerStateLightingTests.cs` (the bunker volumes), `Assets/Scripts/Environment/PlayTests/PowerStateLightingPlayTests.cs`
(PlayMode: it raises the channels and checks the real component over time) and
`Assets/Editor/WorldBuilder/Tests/BlackoutLightLayoutTests.cs`.

**Order.** Run Setup Lighting (W7) first, then Setup Blackout Lighting. The component lists the powered fixtures, so
run Setup Blackout Lighting again whenever Setup Lighting adds or removes one.
- **Re-runs:** a re-run with no code change leaves every file byte-identical. It reuses objects by name and removes
  only objects it generated under `Lighting/BlackoutLights`.
- **Profile:** every run rewrites the two states and the two lamp colours from the reference. It keeps the timings, so
  tune those on the asset.
- **Bake:** it never bakes. The new lights are Realtime and saved switched off, so they do not change a bake.

### How it works

- **Event-driven.** It listens to the `GeneratorDisabled` and `PlayerRespawned` channels
  (`Assets/ScriptableObjects/Events`). It has no reference to the generator: the cascade starts from a fixed point
  2 m above `GeneratorSocket`, at (−11, 4.5, 106).
- **Respawn.** `PlayerRespawned` puts the power back only while the generator is still on. After the real event, a
  respawn keeps the blackout.
- **Testing.** The component's context menu has two items, which work in Play mode only:
  - **Trigger Blackout** plays the blackout without raising the event for anyone else. It is a preview, so a respawn
    undoes it.
  - **Reset To Powered** puts everything back and forgets the event. It is not called "Reset" because Unity's own
    Reset item on the same menu would clear every field the tool filled in.
  - To test the whole chain, right-click the `GeneratorDisabled` asset and choose **Raise**.

| Seconds after the event | What happens | Profile setting |
| --- | --- | --- |
| 0 … 1.5 | The 31 powered fixtures die outward from the generator. Each strobes for 0.3 s (every 1/30 s step at 1.4 × or 0.05 ×, half and half), then its light is disabled and its lamp stops glowing. A fixture d metres from the origin goes out at 0.3 + 1.08 × d / d<sub>max</sub> s, plus up to 0.12 s of jitter, so the farthest one (the top ramp tube, 53 m away) is out by 1.5 s | cascadeSeconds 1.5, strobeSeconds 0.3, jitterSeconds 0.12, strobeHigh 1.4, strobeLow 0.05 |
| 0 … 0.4 | Seven W10 monitor screens fade out through the component's `screens` list | screenFadeSeconds 0.4 |
| 0 … 2 | Fog, ambient, the moon and the bunker's reflection probes move to the blackout state on an S-curve | transitionSeconds 2 |
| 1.5 … 2 | Darkness. Only the outdoor lights are on | — |
| 2 … 3.2 | The emergency lights and alarm beacons fade in. Then the emergency lights pulse at 0.75 + 0.25 sin(3.2 t), the beacons turn at 206.3° per second (3.6 rad/s), and their domes pulse at 2.5 ± 0.8 | emergencyDelaySeconds 2, emergencyFadeSeconds 1.2, emergencyPulseDepth 0.25, emergencyPulseSpeed 3.2, alarmDegreesPerSecond 206.3, alarmDomePulseDepth 0.32, alarmDomePulseSpeed 7.2 |

- **Faster than the prototype:** its cascade takes about 2.0 s (0.35 + 0.028 × d + up to 0.12 s), with the emergency
  lights 0.5 s after it. The brief asks for about 1.5 s, so the wave is faster, and the 0.5 s of darkness is kept.
- **Faulty tubes:** their `FlickerLight` is disabled first, which puts the light and lamp back to full before the
  strobe.
- **Repeatable:** the jitter and the strobe use FlickerLight's hash, so the cascade is the same on every run.

### Powered and blackout states

The blackout values are the reference's linear hex values, converted as in W7 (`LightingLayout.FromLinearHex`). Colours
are mixed in linear space.

| Setting | Powered (the W7 scene) | Blackout: reference | Blackout: Unity |
| --- | --- | --- | --- |
| Fog colour | #3B4B63 (reference #0B1220) | #0B0203 | #3B161C |
| Fog density | 0.0135 | 0.05 | 0.05 |
| Ambient sky / equator / ground | #8998B1 / #6E798D / #464D58 | hemisphere #3A0907 / #060202 × 0.20 | #3C1411 / #2C0E0C / #0F0505 |
| Moon intensity | 0.9 | 0 (the prototype's moon is off inside the bunker) | 0 |
| Intensity of the 6 reflection probes | 1 | none: the prototype swaps to a near-black red cubemap | 0.2 (chosen: the old reflections only hint at shapes on metal) |

**Where the blackout shows.** The generator powers only the bunker, so the outdoor lights stay on. The blackout look
is weighted by how far inside the bunker the main camera is (`PowerStateLighting.Interior`): the applied blend is the
transition × the interior factor.

| Volume | Box | Interior factor |
| --- | --- | --- |
| Underground | x −20.3 … 10.4, y 2.1 … 9.4, z 69.6 … 142.4: SecurityArea, Corridor, GeneratorRoom and Laboratory with their walls | 1 |
| Entrance | x −6 … 6, y 2.1 … 13.4, z 48.4 … 69.6: the lobby and ramp with their side walls | S-curve from 0 just inside the bunker door to 1 at the bottom of the ramp |
| Everywhere else | — | 0 |

- **Camera:** it is found by its MainCamera tag (`Camera.main`), because it lives in another scene. With no main
  camera (Environment played on its own) the whole scene counts as inside.
- **RenderSettings:** they are written only during a blackout or a reset. Before the event, the scene's own values
  are in charge. Environment must be the active scene (playbook trap 7), or the writes go to another scene's settings.

### Emergency lights and alarm beacons

Each emergency light is `Lighting/BlackoutLights/<Room>/<Room>_EmergencyNN`: a point light with a `_Lamp` box. Each
beacon is `<Room>_AlarmNN`: a spot light with a `_Dome` sphere. All 17 are **Realtime** and saved **switched off**.
- **Not Mixed:** in Baked Indirect mode, a Mixed light would bake its red bounce into the powered lightmaps. A Baked
  light could not change at run time.
- **On and off:** the component turns them on at 2 s, and off again at a reset.

| Lights | Reference | Unity intensity | Other |
| --- | --- | --- | --- |
| 14 emergency lamps (positions below) | #FF2614, 2.2, 9 m, decay 1.5 | 7.54 | No shadows |
| 3 alarm beacons: Corridor (0, 5.3, 98) and (0, 5.3, 117), GeneratorRoom (−11, 8.8, 106) | #FF1808, 9, 22 m, decay 1.3, half angle 0.42 rad, penumbra 0.4 | 228.4 | Spot 48.1°, inner 28.9°, tilted 14.9° down, turning about the world's up axis from 0°, 120° and 240°. Soft shadows, Low tier |

| Room | Wall | Light positions (x, y, z) |
| --- | --- | --- |
| SecurityArea | west, east | (−7.56, 4.8, 76), (7.56, 4.8, 79.5) |
| Corridor | east, west, east, west, east | x ±1.06, y 4.8, z 86, 95, 104, 114, 121 (east is +x) |
| GeneratorRoom | west, west, south | (−19.46, 6.3, 99), (−19.46, 6.3, 112), (−9, 6.3, 96.44) |
| Laboratory | west, east, north | (−9.56, 4.8, 130), (9.56, 4.8, 137), (0, 4.8, 141.56) |
| BunkerEntranceRamp | east | (1.06, 8.99, 60) |

- **Placement:** these are the reference's positions, moved onto the Unity walls (the corridor's x ±1.9 becomes its
  ±1.5 wall). The lamp (0.34 × 0.2 × 0.08 m) touches its wall 0.5 m under the ceiling, but no higher than 4.0 m, the
  prototype's generator-room height. The light is 0.4 m out from the lamp and 0.2 m lower, as in the prototype. In the
  3 m rooms the lamp is 2.5 m over the floor, above the 2.3 m doors.
- **Intensities:** matched with `LightingLayout.MatchIntensity`, as in W7.
  - Emergency lights: the prototype gives them all one intensity, so Unity does too. 7.54 matches the prototype 2.3 m
    from the light, which is the floor in the 3 m rooms.
  - Alarm beacons: matched at the prototype's aim point, 6 m out and 1.6 m down (6.2 m away), like the floodlights:
    228.4. Closer than that, Unity is brighter than the prototype.
- **Shadows:** the alarm beams reach 22 m and would sweep the next room through the walls, so they cast shadows. The
  three spot maps take 0.2 M texels of the shadow atlas. By then the 6 high-bays and the top ramp tube are off, which
  frees the 2.75 M they used. The emergency lights cast none, as in the prototype.
- **Lamps:** `Lamp_Emergency` (base #330000, unlit emission linear (0.12, 0.02, 0.02)) and `Lamp_Alarm` (base #400404,
  emission #FF1A0A × 0.2), in `Assets/Materials/Environment/Lighting`.
  - The profile holds the lit colours: #FF2A1A × 5 and #FF1A0A × 2.5.
  - The unlit emission is never black, because URP turns emission off for a black colour, and then the lamp could not
    light up.
  - The lamps have no collider and cast no shadow, and they are not static. Their emission changes through a
    MaterialPropertyBlock, as in FlickerLight.

### Performance

- **No allocations per frame:** one MaterialPropertyBlock is reused, colours are cached in Awake, and the camera is
  cached.
- **Dead fixtures are disabled,** not set to 0, so Forward+ skips them and they give their shadow maps back.
- **Work per frame:** before the event, Update returns at once. After the cascade it sets 17 light intensities, 3
  rotations and 17 lamp colours. RenderSettings are written only when the blend changes: during the 2 s transition and
  while the camera is on the ramp.

### What to check

1. Enter Play mode with `Environment.unity` on its own. With no player camera the whole scene counts as inside.
2. Fly the Scene view into the generator room, select `Lighting` and right-click PowerStateLighting > Trigger
   Blackout. You should see the lamps strobe and die outward to the ramp by 1.5 s and the fog thicken to dark red by
   2 s. From 2 s the red lamps fade in and the three alarm beams start sweeping.
3. Choose Reset To Powered: everything comes back, and the two faulty tubes flicker again.
4. Right-click the `GeneratorDisabled` asset and choose Raise for the real event. Raising `PlayerRespawned` afterwards
   must not bring the power back.
5. **Validate Layout:** it still passes 66 of 66, because no collider changed.

### Known issues

- **The baked bounce stays.** Once the scene is baked, the powered fixtures' bounce is in the lightmaps and light
  probes (W7, Baked Indirect). The walls and the robots will keep a faint warm glow after the blackout. W8 dims only the
  reflection probes, and the ambient change barely reaches lightmapped surfaces. If the glow shows after the bake,
  either:
  - set the bunker fixtures' Indirect Multiplier to 0 in Setup Lighting (no bounce from them at all), or
  - add a second "blackout" bake and swap the lightmaps and light probes at the event (future work; W10 does not implement this).
- **No visible beams or sparks.** The prototype's alarm beam cones, light shafts and sparks remain deferred after W9;
  W9 adds steam, dust, beach fog and optional rain. The generator's own sparks, hum and core glow belong to its prefab (Systems
  Engineer and Core Developer), which listens to the same channel.
- **The emergency lights leak a little.** They cast no shadows, so within their 9 m range they tint the next room red
  through the wall.
- **Screens (W10).** Dress Bunker assigns seven monitor screen renderers to `screens`, retaining manual assignments.
  Their materials use `_EmissionColor`; their static flags are clear. The other emissive dressing parts are not
  added to this list, so specimen fridge glow and cabinet LEDs keep their authored emission.
- **The camera needs its tag.** The player's camera must be tagged MainCamera, or the blackout shows outdoors too.
- **The tests use the default timings.** Timings tuned on the asset are not tested.
- **Powered fog is W7's.** The powered bunker keeps W7's one fog value (0.0135), not the reference's 0.022.
- **Not checked by eye:** the look was checked only in batch mode.

## Post-processing and atmosphere (W9)

The **Volumes** root holds `Volumes_Powered`, `Volumes_Blackout`, `QualityPreset` and an `Atmosphere` child.
Runtime scripts are in `Assets/Scripts/Environment` (`Blackout.Environment`, referencing `Blackout.Shared` and URP).
Editor tools and their layout numbers are in `Assets/Editor/WorldBuilder` (`Blackout.Editor`).

### Build order and generated assets

Run these menus outside Play mode with Environment open:

1. **Blackout/World/Setup Blackout Lighting** if the W8 lighting has not been built, or its fixtures changed.
2. **Blackout/World/Setup Post Processing** (`PostProcessSetup.SetupPostProcessing`).
3. **Blackout/World/Build Atmosphere Particles** (`AtmosphereParticlesBuilder.BuildAtmosphereParticles`).

Setup Post Processing writes `Assets/Environment/Volumes/Powered.asset` and `Blackout.asset`, reuses the two global
volumes and connects the Blackout volume to PowerStateLighting. Existing overrides for other effects are kept.
Build Atmosphere Particles reuses its named systems, removes only stale generated children under `Volumes/Atmosphere`,
and gives QualityPreset all five systems, including the inactive rain.

The particle tool also writes the 64 × 64 soft disc `Assets/Environment/Atmosphere/SoftParticle.png` and four
materials in `Assets/Materials/Environment/Atmosphere`: `Atmosphere_Steam`, `Atmosphere_Dust`, `Atmosphere_Fog` and
`Atmosphere_Rain`. Dust uses URP Particles/Simple Lit; the other three use Particles/Unlit. Steam and fog use soft
particles with depth fade distances 1 / 2 m; fog also fades near the camera over 1–4 m. The PC URP asset supplies
the depth texture. No external image or audio is required for these effects. Outdoor vent housings remain deferred;
the W10 corridor grille slot is separate and its prefab is currently missing.

Change numbers in **PostProcessLayout.cs** and **AtmosphereLayout.cs**, then rerun their tools. The tools overwrite
their authored settings and particle materials. A particle rebuild saves rain inactive again. Keep generated `.meta`
files with the assets so their GUIDs survive teammates' imports.

### Powered and Blackout profiles

The prototype has the same post-processing before and after the blackout. The two-profile brief adds a modest
Blackout variation: more contrast, grain and corner darkening. The emergency lights provide the red colour.

| Override | Powered | Blackout | Reason |
| --- | --- | --- | --- |
| Tonemapping | ACES | ACES | Prototype's filmic curve |
| Color Adjustments: Post Exposure | +0.737 EV | +0.737 EV | `log2(1 / 0.6)`, from the prototype's ACES input scale |
| Contrast / Saturation / Color Filter | 0 / 0 / white | +10 / 0 / white | Blackout contrast is a Unity addition |
| Bloom: Threshold / Intensity | 0.931 / 2.25 | 0.931 / 2.25 | Linear threshold 0.85 converted to gamma; strength 0.75 × five mip weights of 0.6 |
| Bloom: Scatter / Max Iterations | 0.7 / 5 | 0.7 / 5 | URP spread and five-level blur |
| Vignette: Intensity / Smoothness / Rounded | 0.25 / 0.4 / on | 0.27 / 0.4 / on | Fitted round falloff, darker blackout corners |
| Film Grain: Type / Intensity / Response | Thin1 / 0.12 / 0.8 | Thin1 / 0.2 / 0.8 | Approximation of prototype grain 0.035; stronger blackout grain |

These are conversions and curve fits, with a visual comparison still outstanding. The Powered vignette matches
the prototype's top edge and corners within 0.01 in the tested linear-light calculation; its middle falloff can be
up to 0.12 lighter. Grain and bloom use different implementations in URP, so the formulas give starting values.

`Volumes_Powered` is global, priority **0**, weight **1**. `Volumes_Blackout` is global, priority **1**, saved at
weight **0**. PowerStateLighting now writes its existing `Blend` into the Blackout volume's weight as well as the
fog and ambient: the W8 **2 s** transition multiplied by the camera's interior factor. The Powered weight stays 1;
the higher-priority volume blends toward Blackout. Outdoors its weight returns to 0, and Reset To Powered restores
0. The W8 event, cascade and respawn behaviour stay as documented above. With no MainCamera, the interior factor is 1.

### Particle placement and motion

All positions below are Unity metres. All systems simulate in world space, reuse native Particle System storage,
and have fixed seeds **9001–9005** in table order. Steam, dust and beach fog prewarm; rain starts empty.
Rates and caps are the authored **Medium** values.

| System under Volumes/Atmosphere | Position / emission box | Rate / cap | Lifetime / size | Motion |
| --- | --- | --- | --- | --- |
| Atmosphere_SteamVentWest | (−14, 11.75, 54) | 7/s / 32 | 2.6–3.6 s / 0.35 → 2.4 m | Cone leans east, speed 1.4–2.2 m/s, drag 0.5 |
| Atmosphere_SteamVentEast | (13.5, 11.75, 51) | 7/s / 32 | Same as west | Same as west |
| Atmosphere_CorridorDust | (0, 4, 103), box 2.6 × 2.6 × 40.8 m | 25/s / 300 | 8–12 s / 0.015–0.035 m | Noise strength 0.05, frequency 0.4 |
| Atmosphere_BeachFog | (0, 1.5, −112), box 110 × 1 × 32 m | 3.2/s / 120 | 20–30 s / 6–10 m | Eastward drift 0.3–0.5 m/s, spin up to ±3°/s |
| Atmosphere_Rain (inactive) | Initially (0, 13.3, −121), horizontal box 30 × 30 m | 500/s / 1200 | 2 s / 0.012 m | Downward 6–8 m/s, stretched with velocity scale 0.04 |

Steam follows the prototype's two vents and fade: opacity 0.22 × `min(1, t / 0.2) × (1 − t)^1.3`, with normalized
lifetime `t`. Its narrow cone approximates the prototype's mean velocity (0.25, 1.8, 0) m/s and sideways spread.
Dust, beach fog and rain are Unity additions requested by the brief. Lit dust responds to the red emergency lights;
unlit beach fog uses the outdoor ambient-sky tint at opacity 0.1. The fog's emission box bounds its starting positions;
its large puffs and drift extend beyond it.

To preview rain, enable **Atmosphere_Rain in Play mode**. RainEmitter follows the MainCamera 12 m above its eye.
One upward raycast checks 30 m for a roof, using Physics.DefaultRaycastLayers and ignoring triggers. Cover disables
emission; existing drops continue for up to 2 s. The single ray covers the camera's location, so roof edges can still
show drops from the wider emitter. Rain has no per-drop collision. It remains optional and off in the saved scene.

### Quality presets

QualityPreset starts at **Medium**. In Play mode use its context menu **Apply Low**, **Apply Medium** or **Apply High**;
settings code can call `Apply(Blackout.Environment.QualityLevel.Low)` (or Medium / High).

| Setting | Low | Medium | High |
| --- | --- | --- | --- |
| Active URP asset shadow distance | 40 m | 50 m (W11; previously 60 m) | 80 m |
| MainCamera post-processing / FXAA | off / off | on / on | on / on |
| Atmosphere emission rate and particle cap | ×0.5 | ×1 | ×1.5 |
| Steam emission per vent | 3.5/s | 7/s | 10.5/s |

The component caches authored rates and caps, so switching repeatedly always scales from Medium. Caps round to the
nearest integer with a minimum of one. Inactive rain stays inactive at every quality level. The shadow distance is
changed in memory during Play mode and restored when the component is destroyed; the Systems Engineer's URP asset
is kept on disk. The camera is configured when found or replaced. These controls leave the existing terrain density,
render scale and shadow-map resolution under their current project settings.

**Handoff to Systems Engineer:** in `Assets/Prefabs/Player/Player.prefab`, add Universal Additional Camera Data to
PlayerCamera, enable Post Processing and set Anti-aliasing to FXAA. It is already tagged MainCamera. QualityPreset
provides this configuration at runtime, with Low switching it off. Keep Environment active after additive loading
for the W8 RenderSettings changes.

### Verification and Editor checks

Batch verification on **2026-10-17**, Unity **6000.3.25f1**:

- Both W9 generators completed and were rerun: all **19** checked files (including metadata) remained byte-identical.
- Focused EditMode tests: **29/29** passed (PostProcessLayout, AtmosphereLayout, QualityPreset and PowerStateLighting).
- Environment PlayMode tests: **8/8** passed, including the volume blend, quality switches and rain roof behaviour.
- Validate Layout: **PASS, 66 of 66**. Generated files remained unchanged after the tests; no C# compiler errors or warnings.
- Logs and XML results are outside the repo: `%TEMP%/blackout-w9-*.log` and `%TEMP%/blackout-w9-*-tests.xml`.
  The layout run logged a Unity Pipeline `HandleStatusRequest` disposed-object error during shutdown and exited 0.

Before the PR, check the appearance in the Editor:

1. Open Environment and inspect Volumes: Powered weight 1, Blackout weight 0, QualityPreset Medium and rain inactive.
2. Play through Bootstrap with the tagged player camera. Look at the vent steam, corridor dust and beach fog; compare
   with `prototype/screenshots/`. Verify readable silhouettes, glow and corners in the Game view. Scene-view previews
   require the Scene view's post-processing/effects controls enabled.
3. On Lighting, use **PowerStateLighting > Trigger Blackout**: Blackout weight should approach 1 inside over 2 s,
   fall to 0 outside, and return to 0 with **Reset To Powered**. Then test the real GeneratorDisabled event and respawn.
4. Apply Low / High / Medium on QualityPreset. Verify post-processing, shadow reach and particle emission rates;
   repeatedly switching back to Medium should restore steam to 7/s. Check for new Console errors.
5. Enable rain in Play mode, move under a roof and back out, and allow 2 s for old drops to finish. Stop Play mode
   and confirm rain is still inactive in the saved scene. Check performance on the intended demo machine.

The visual comparison, performance measurement and lighting bake remain manual work. Large fog particles can add
transparent overdraw. W9 does not add visible alarm beam cones, fixture sparks or wind-sway shaders.

## Bunker dressing table (W10)

`DressingLayout.cs` reads the marked table below; it is the placement source for **Blackout/World/Dress Bunker**.
Positions are absolute Unity metres, yaw is degrees, and SX/SY/SZ multiply the prefab's authored scale.
Prefab names resolve only within `Assets/Prefabs/Props`. Missing prefabs are skipped and reported; the optional
CC0 slots are `Prop_PipeStraight`, `Prop_CableTray`, `Prop_Vent`, `Prop_FireExtinguisher` and `Prop_Flask`.
The Core Developer supplies those prefabs using their catalog's sizes and pivots. No kit assets are fabricated.

The tool tests actual combined renderer/collider bounds before accepting a placement. It reserves a **1.2 m**
wide, **2 m** high central walking path through the lobby, ramp, security and corridor, then to the generator console
and the lab chamber approach. Every marker keeps a **0.4 m** radius standing space with 2 m headroom; door sockets
keep 1.3 m on each horizontal axis for the opening and approach. The barricade keeps its 1.5 m footprint plus
0.4 m clearance per side. The generator and cure chamber spaces remain reserved for gameplay prefabs.
These conservative boxes can reject a detailed prop whose mesh would fit; rejection is preferable to blocking play.

The lobby furniture moves south into the W1 lobby. Lab benches move to z 128.8 / 135.3 so the cover markers at
130.5 / 137 remain free. The second diesel tank moves north to z 112.6, clear of CoverPoint_07 at (-18, 110).
Crates here are fixed dressing; the Systems Engineer's pushable corridor crate is separate and stays dynamic.
The 4.6 m security sign is centred at x -3.8, one metre west of the initial draft, to clear the central route.
Floor props use base-centre pivots except crates (centre); signs and vents use wall-face pivots facing local +Z.
Monitors and lab equipment sit on their listed desk/bench tops. Pipes and trays stay above the standing volumes.
Only bunker rooms are covered here; outdoor dressing, the catwalk and its stairs need separate placement work.

<!-- W10-DRESSING-BEGIN -->
| Room | Id | Prefab | X | Y | Z | Yaw | SX | SY | SZ |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BunkerEntranceLobby | LobbyDesk | Prop_DeskGuard | -3.8 | 10 | 49.5 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyMonitor | Prop_Monitor | -3.8 | 10.78 | 49.5 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyLocker01 | Prop_Locker | 3.2 | 10 | 49.1 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyLocker02 | Prop_Locker | 3.8 | 10 | 49.1 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyLocker03 | Prop_Locker | 4.4 | 10 | 49.1 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyLocker04 | Prop_Locker | 5 | 10 | 49.1 | 0 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbyExtinguisher | Prop_FireExtinguisher | 5.1 | 10 | 51.5 | 270 | 1 | 1 | 1 |
| BunkerEntranceLobby | LobbySign | Prop_Sign_AllPersonnel | -2.7 | 12 | 48.43 | 0 | 1 | 1 | 1 |
| BunkerEntranceRamp | RampPipe01 | Prop_PipeStraight | 1.1 | 11.8 | 53.5 | 0 | 1 | 1 | 1 |
| SecurityArea | SecurityDesk01 | Prop_Desk | 6.6 | 2.5 | 76.6 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityDesk02 | Prop_Desk | 6.6 | 2.5 | 73.2 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityMonitor01 | Prop_Monitor | 6.6 | 3.28 | 76 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityMonitor02 | Prop_Monitor | 6.6 | 3.28 | 77.2 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityMonitor03 | Prop_Monitor | 6.6 | 3.28 | 72.6 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityMonitor04 | Prop_Monitor | 6.6 | 3.28 | 73.8 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityWallScreen | Prop_WallMonitor | 7.97 | 4.1 | 79.5 | 270 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker01 | Prop_Locker | -7.7 | 2.5 | 74 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker02 | Prop_Locker | -7.7 | 2.5 | 74.6 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker03 | Prop_Locker | -7.7 | 2.5 | 75.2 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker04 | Prop_Locker | -7.7 | 2.5 | 75.8 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker05 | Prop_Locker | -7.7 | 2.5 | 76.4 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker06 | Prop_Locker | -7.7 | 2.5 | 77 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker07 | Prop_Locker | -7.7 | 2.5 | 77.6 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker08 | Prop_Locker | -7.7 | 2.5 | 78.2 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker09 | Prop_Locker | -7.7 | 2.5 | 78.8 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker10 | Prop_Locker | -7.7 | 2.5 | 79.4 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker11 | Prop_Locker | -7.7 | 2.5 | 80 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityLocker12 | Prop_Locker | -7.7 | 2.5 | 80.6 | 90 | 1 | 1 | 1 |
| SecurityArea | SecurityTable | Prop_MeetingTable | -3.2 | 2.5 | 76.7 | 0 | 1 | 1 | 1 |
| SecurityArea | SecurityChair01 | Prop_OfficeChair | -3.2 | 2.5 | 75.6 | 0 | 1 | 1 | 1 |
| SecurityArea | SecurityChair02 | Prop_OfficeChair | -3.2 | 2.5 | 77.8 | 180 | 1 | 1 | 1 |
| SecurityArea | SecurityCrate | Prop_CrateSmall | -6 | 3 | 71.5 | 0 | 1 | 1 | 1 |
| SecurityArea | SecuritySign | Prop_Sign_SecurityCheckpoint | -3.8 | 4.5 | 81.97 | 180 | 1 | 1 | 1 |
| Corridor | CorridorPipe01 | Prop_PipeStraight | 1.1 | 5.1 | 85 | 0 | 1 | 1 | 1 |
| Corridor | CorridorPipe02 | Prop_PipeStraight | 1.1 | 5.1 | 87 | 0 | 1 | 1 | 1 |
| Corridor | CorridorPipe03 | Prop_PipeStraight | 1.1 | 5.1 | 89 | 0 | 1 | 1 | 1 |
| Corridor | CorridorCable01 | Prop_CableTray | -0.6 | 5.2 | 101 | 0 | 1 | 1 | 1 |
| Corridor | CorridorCable02 | Prop_CableTray | -0.6 | 5.2 | 103 | 0 | 1 | 1 | 1 |
| Corridor | CorridorVent | Prop_Vent | -1.47 | 4.9 | 108 | 90 | 1 | 1 | 1 |
| Corridor | CorridorSign | Prop_Sign_SignLaboratory | 1.47 | 4.3 | 121 | 270 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorTank01 | Prop_FuelTank | -18.6 | 2.5 | 102 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorTank02 | Prop_FuelTank | -18.6 | 2.5 | 112.6 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet01 | Prop_ElectricalCabinet | -18 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet02 | Prop_ElectricalCabinet | -16.5 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet03 | Prop_ElectricalCabinet | -15 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet04 | Prop_ElectricalCabinet | -13.5 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet05 | Prop_ElectricalCabinet | -12 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCabinet06 | Prop_ElectricalCabinet | -10.5 | 2.5 | 96.5 | 0 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorSign | Prop_Sign_DangerHighVoltage | -19.87 | 5.2 | 106 | 90 | 1 | 1 | 1 |
| GeneratorRoom | GeneratorCrate | Prop_CrateSmall | -3.3 | 3 | 98 | 0 | 1 | 1 | 1 |
| Laboratory | LabBench01 | Prop_LabBench | -6.5 | 2.5 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabBench02 | Prop_LabBench | 6.5 | 2.5 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabBench03 | Prop_LabBench | -6.5 | 2.5 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabBench04 | Prop_LabBench | 6.5 | 2.5 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabAnalyser01 | Prop_Analyser | -6.9 | 3.43 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabAnalyser02 | Prop_Analyser | 6.1 | 3.43 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabAnalyser03 | Prop_Analyser | -6.9 | 3.43 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabAnalyser04 | Prop_Analyser | 6.1 | 3.43 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabMicroscope01 | Prop_Microscope | -5.9 | 3.43 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabMicroscope02 | Prop_Microscope | 7.1 | 3.43 | 128.8 | 0 | 1 | 1 | 1 |
| Laboratory | LabMicroscope03 | Prop_Microscope | -5.9 | 3.43 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabMicroscope04 | Prop_Microscope | 7.1 | 3.43 | 135.3 | 0 | 1 | 1 | 1 |
| Laboratory | LabFridge01 | Prop_SpecimenFridge | 9.5 | 2.5 | 127.5 | 270 | 1 | 1 | 1 |
| Laboratory | LabFridge02 | Prop_SpecimenFridge | 9.5 | 2.5 | 128.9 | 270 | 1 | 1 | 1 |
| Laboratory | LabFridge03 | Prop_SpecimenFridge | 9.5 | 2.5 | 130.3 | 270 | 1 | 1 | 1 |
| Laboratory | LabHood | Prop_FumeHood | -9.4 | 2.5 | 139 | 90 | 1 | 1 | 1 |
| Laboratory | LabCommanderDesk | Prop_DeskCommander | 6.4 | 2.5 | 141.1 | 180 | 1 | 1 | 1 |
| Laboratory | LabCommanderMonitor | Prop_Monitor | 6.4 | 3.35 | 141.1 | 180 | 1 | 1 | 1 |
| Laboratory | LabSign | Prop_Sign_BiohazardLevel4 | -2.8 | 4.5 | 124.03 | 0 | 1 | 1 | 1 |
| Laboratory | LabFlask01 | Prop_Flask | -7.5 | 3.43 | 128.8 | 0 | 1 | 1 | 1 |
<!-- W10-DRESSING-END -->

### Rebuilding and preserving hand edits

Open **Environment** outside Play mode and run these menus in order:

1. **Blackout/World/Dress Bunker** (`Blackout.Editor.BunkerDresser.DressBunker`).
2. **Blackout/World/Bake NavMesh** (`Blackout.Editor.NavMeshBaker.BakeNavMesh`).
3. **Blackout/World/Validate Layout** (`Blackout.Editor.LayoutValidator.ValidateLayout`).

Dress Bunker saves only `Assets/Scenes/Environment.unity`. It creates linked prefab instances under
`Props/BunkerDressing/<Room>/<Room>_<Id>` and checks each candidate in a temporary preview scene first.
Positions and bounds use world metres; the organisational containers need unit world scale and no rotation.
There is no random placement or seed: table order, transforms and existing prefab links determine the result.
The layout asset is loaded after opening Environment, because opening a scene can unload unreferenced assets.

Rerunning reuses matching instances and overwrites their table position, yaw, scale and authored static flags.
Hand-added children survive and are included in the next bounds check. An object with a different prefab source
or an unsafe envelope is left unchanged with a warning. Removed table rows also remain with a warning; remove
unwanted scene instances through the Editor's Hierarchy after reviewing their hand additions. If a row changes
prefab type, replace its old instance through the Editor before rerunning. No existing scene object or asset is
automatically deleted. The tool does not change source prefabs, materials, shared contracts or another owner's scene.

The bounds check also rejects props outside their room footprint or intersecting its floor/ceiling (2 cm tolerance).
Prefabs containing a Rigidbody, Animator, Animation, script, Particle System, Light or skinned mesh are rejected
as fixed dressing. Unsupported collider shapes require manual review. These checks reserve authored routes;
they do not guarantee a 1.2 m aisle between every pair of furniture pieces or account for another scene's closed doors.

### Saved output and missing kit pieces

The current scene contains **61 linked instances** from 70 rows. Existing art and its materials are reused;
no new art placeholders were needed. Nine rows are skipped because these exact prefab paths are absent:

| Missing prefab under `Assets/Prefabs/Props/` | Placements | Purpose |
| --- | --- | --- |
| `Prop_FireExtinguisher.prefab` | 1 | Lobby extinguisher |
| `Prop_PipeStraight.prefab` | 4 | One ramp and three corridor pipe sections |
| `Prop_CableTray.prefab` | 2 | Corridor cable runs |
| `Prop_Vent.prefab` | 1 | Corridor grille |
| `Prop_Flask.prefab` | 1 | Lab bench glassware |

**Handoff to Core Developer:** please supply those five prefabs using `PropCatalog` sizes, pivots and local +Z
fronts; then World Builder can rerun the three menus. Do not replace or edit the current prop assets for this step.

| Room | Placed | Missing |
| --- | --- | --- |
| Lobby | 7 | 1 |
| Ramp | 0 | 1 |
| Security | 24 | 0 |
| Corridor | 1 | 6 |
| Generator | 10 | 0 |
| Laboratory | 19 | 1 |

### Objects that must stay dynamic

Keep **all static flags clear** on moving or collectible objects and their affected children:

| Objects | Reason / owner |
| --- | --- |
| Door leaves and moving children | Doors open and close; Systems Engineer owns their prefabs and Interactables scene. Fixed bunker frames can remain static |
| Pushable barricade crate and its visual children | Physics moves it; Systems Engineer. This is separate from W10's two fixed dressing crates |
| Cure and health pickup roots and collectible visuals | They are collected, hidden or respawned; Systems Engineer. Keep their marker spaces free |
| Boat; player, agents and animated model parts | Boat bobs; player and agents move. Their owners configure those prefabs |
| Monitor screens, emissive dressing surfaces and lamp meshes | Emission can change at runtime. W10 keeps emissive dressing dynamic; seven monitor renderers join PowerStateLighting |
| Particle effects and other moving effects | Native particle systems animate them; W10 rejects such prefabs as fixed dressing |

Only Environment's fixed dressing flags are authored here. Door, barricade, pickup, player and agent assets were
not edited or validated by the saved-scene tests. Their owners must retain dynamic flags and closed-door navigation
blocking. The catwalk, its stairs, outdoor dressing and the generator plinth remain deferred; the catwalk health
marker is still unreachable. A future catwalk build must keep agents off its stairs as specified in the reference.

### Verification and final lighting bake

Verified with Unity **6000.3.25f1** during this W10 session:

- Dress Bunker ran twice: **61/70** accepted each time, nine missing-prefab warnings, both exits **0**.
  The scene was **byte-identical** on repeat. Its text contains 61 generated instance names and seven screen references.
- Bake NavMesh exited **0**: **149 colliders**, **604** tree/rock capsules, **8,297 triangles**, **154,106 m²** walkable.
  `Assets/Environment/NavMesh/EnvironmentNavMesh.asset` is 993,768 bytes; its GUID and the scene's GUID are unchanged.
- Validate Layout exited **0**, **PASS 66/66**: complete mission/escape paths, four 1.40 m door gaps, all tested
  spawns/waypoints/cover points within 1 m of the NavMesh, and none of 494 pine/palm trunks on it.
- Focused EditMode tests: **43/43 passed**, exit **0** (DressingLayoutTests, BunkerDresserTests, SavedDressingTests
  and StaticFlagRulesTests). Saved-scene checks use a preview scene; scene and NavMesh hashes remained unchanged.
  No C# compiler errors or warnings were reported in the successful runs.
- Logs and XML are outside the repo: `%TEMP%/blackout-w10-commit3-{dress,dress-repeat,navmesh,layout}.log`
  and `%TEMP%/blackout-w10-commit4-tests.log` / `-tests.xml`. Early sandbox runs lacked a license; approved runs
  used the existing activation. The earlier commit-1 test run logged a Unity Pipeline shutdown error despite passing.

**NavMesh baking is complete. Lightmaps, reflection-probe baking and occlusion culling remain unbaked.**
Saved lighting settings were checked: Baked Indirect, baked GI on, realtime GI off, 10 texels/m, 1024 maps.
This verifies configuration, not a completed bake. No visual comparison or performance measurement was performed.
Use the W7 bake settings above when completing the manual Editor bake; baked powered bounce will remain after blackout.

**Handoff to the team:** please add `Assets/Scenes/Environment/` to World Builder ownership in the playbook before
the default-location lighting bake is saved or committed. It is Unity's documented output folder for this scene,
but it is outside the currently listed owned paths. This step did not generate files there or change the playbook.

Before the PR, open Environment and inspect furniture and signs against `prototype/screenshots/`. Then play through
Bootstrap, walk both the inward and escape routes, check door approaches and cover positions, and trigger the blackout
from PowerStateLighting's context menu. Seven monitor emissions should fade over 0.4 s and return on Reset To Powered.
Inspect static flags on fixed furniture and clear flags on screens. Check the Console for new errors and run
Validate Layout again after any manual collider or prop changes. Complete the lighting bake and its W7 visual checks
only after the ownership handoff is resolved; save and review every generated asset together with its metadata.

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
| Trees and boulders cut out of the NavMesh (W6) | 494 pines and palms and 110 boulders are Not Walkable capsules, so agents path round them. Hunters' routes keep 6 m clearings at every waypoint | Agent Controller, for information |
| Tree trunks and boulders are solid (W6) | Their capsule colliders (through the TerrainCollider) stop the player and block raycasts: bullets and the Hunter's line of sight. Bushes and grass have no collider, as in the prototype | Systems Engineer (player, weapons, Hunter sight) |
| Trail and gravel are mixes of 4 layers (W6) | The prototype's dirt and gravel colours are blends of Sand, ForestFloor and Rock, which keeps URP Terrain Lit at one pass | — |
| 461 pines and 69 palms before clearings, not 465 and 65 (W6) | Exact heights instead of the prototype's 2 m grid; the same seeded rules | — |
| No fallen logs, wet sand or wind sway yet (W6) | Outdoor logs remain deferred after W10. Wet sand would be a fifth layer. Wind needs a foliage shader | World Builder (later steps) |
| Reference light colours used as linear values (W7) | The Inspector shows other hex values than the reference (fog #0B1220 shows as #3B4B63), but surfaces receive the prototype's light. Convert a reference light colour the same way (`LightingLayout.FromLinearHex`) | Systems Engineer and Core Developer, for information (lights on their prefabs, for example the generator core or the health kits) |
| Light intensities converted (W7) | Unity's inverse-square falloff needs 3 to 14 times the reference's intensity in the bunker (corridor tube 1.35 → 4.7, high-bay 3.0 → 43.3) and about 250 times for the floodlights | Systems Engineer and Core Developer, for information: convert lights copied from the reference with the same rule (`LightingLayout.MatchIntensity`) |
| Fixtures under the 3.0 m Unity ceilings (W7) | Lights are 2.65 m over the floor (lab 2.70 m, generator 5.55 m). The ramp's 4 tubes sit in the middle of each quarter of the 17.2 m ramp, and the wall pack is 2.8 m over the yard, not 3.9 m | — |
| Shadows instead of light layers (W7) | The reference suggests light layers to keep the outdoor lights out of the bunker. Instead the moon, floodlights, wall pack, high-bays and top ramp tube cast shadows, so every renderer stays on the Default rendering layer and teammates' lights (for example the muzzle flash) still reach everything | Systems Engineer and Core Developer, for information |
| Mixed lights in Baked Indirect mode (W7) | Direct light and shadows stay real-time, so the blackout can switch the fixtures off, but their baked bounce stays after the blackout | World Builder (W8) |
| One fog and ambient for the whole scene (W7) | The bunker has the outside fog (0.0135, not 0.022). The ambient indoors comes from the bake | World Builder (W8) |
| Booth lamp and mast beacon not placed (W7) | There is no guard booth or mast yet | World Builder (later steps) |
| Camera background and active scene (W7) | The night sky is a skybox material, so cameras should keep Background Type Skybox. The prototype clears to the fog colour behind its sky dome. Fog, skybox and ambient come from `Environment.unity` only when it is the active scene. After the additive loads, call `LightProbes.TetrahedralizeAsync()` so moving objects use Environment's light probes | Systems Engineer (player camera; Y6 Bootstrap makes Environment active), for information |
| Blackout cascade over 1.5 s, not about 2.0 s (W8) | The fixtures die faster, as the brief asks. The emergency lights still start 0.5 s after the last one, at 2 s | — |
| PowerStateLighting listens to GeneratorDisabled and PlayerRespawned (W8) | Lighting has no reference to the generator. Raise GeneratorDisabled once, when the generator is disabled, and PlayerRespawned after every respawn. A respawn after the event keeps the blackout | Systems Engineer (generator and respawn) |
| Blackout look only inside the bunker (W8) | Fog, ambient and the moon follow the main camera's depth in the bunker, and the outdoor lights stay on, as in the reference. The player camera must be tagged MainCamera, and Environment must be the active scene | Systems Engineer (player camera, Bootstrap) |
| Emergency lamps on the Unity walls (W8) | In the smaller rooms the reference's lamps are 3.0 to 3.4 m up; Unity's hang 0.5 m under the 3 m ceilings (2.5 m) and at 4.0 m in the generator room. The corridor's lamps move from x ±1.9 to its ±1.5 walls | — |
| No alarm beam cones, light shafts or sparks yet (W8) | The beacons light the walls but show no visible beam; W9's particles cover steam, dust, beach fog and optional rain | World Builder (later work) |
| Separate Blackout post-processing profile (W9) | The prototype keeps one look; Unity adds contrast +10, vignette 0.27 and grain 0.2 inside the bunker | — |
| URP post-processing enabled at runtime (W9) | QualityPreset configures MainCamera, including a camera loaded later; Low disables post-processing and FXAA | Systems Engineer (Player.prefab): add Universal Additional Camera Data, Post Processing on, FXAA |
| Corridor dust, beach fog and optional rain (W9) | New native particle systems with fixed seeds; vent steam follows the prototype, rain remains inactive. Outdoor vent housings remain deferred | World Builder (later work) |
| Shifted benches, tank and security sign (W10) | Benches at z 128.8/135.3, second tank at z 112.6 and security sign x -3.8 leave standing markers and routes free | Agent Controller, for information; markers unchanged |
| Nine optional dressing placements missing (W10) | Pipes, cable trays, grille, extinguisher and flask skip until their five prefabs are supplied | Core Developer (Assets/Prefabs/Props) |

Outdoor zones (Beach, Forest, compound yard) keep the prototype's positions. The terrain only adds the
berth, the dock landing and the ramp hole (see "Terrain, beach and boundary"), and the forest only adds the Hunter
clearings (see "Terrain painting and vegetation").
