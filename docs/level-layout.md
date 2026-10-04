# Level layout (World Builder)

This is the real-scale layout for `Assets/Scenes/Environment.unity`. It is built from
`docs/reference/world-builder.md`. The bunker rooms in the tables below are also stored in
`Assets/ScriptableObjects/Layout/BunkerLayout.asset`, filled by
`Assets/Editor/WorldBuilder/BunkerLayoutAssetBuilder.cs`. If you change a number here, change it there too.

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

Outdoor zones (Beach, Forest, compound yard) keep the prototype's positions unchanged.
