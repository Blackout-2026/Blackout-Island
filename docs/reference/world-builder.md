# World Builder reference (extracted from the web prototype)

Source: `prototype/` (read-only). Numbers here are taken from the code, not the README, wherever
the two disagree (see section 8). Files read: `README.md`, `screenshots/`, `src/world/*.js`,
`src/materials/*.js`, `src/models/generator.js`, `src/audio/audio.js` (ambience),
`src/core/engine.js` (post-processing), `src/game/mission.js` and `src/physics/interactables.js`
(trigger radii). Vegetation counts were produced by running the prototype's own seeded placement
code (`Terrain.bake()` + `Forest.place()`) in Node, outside the repo.

## 0. Units and coordinate conversion

| Item | Prototype (three.js) | Unity (this project) |
| --- | --- | --- |
| Unit | 1 unit = 1 metre | 1 unit = 1 metre |
| Up | +Y | +Y |
| North | **−Z** | **+Z** |
| Handedness | right-handed | left-handed |
| Facing at yaw 0 | −Z (north) | +Z (north) |

**Rule used in every table below:** `Unity = (x, y, −z)` and `UnityYaw° = −prototypeYaw°`.

Flipping Z (rather than copying x, y, z verbatim) keeps the level the right way round. A verbatim copy into
Unity's left-handed space would mirror it east–west, which would put the generator room on the player's
right instead of the left. With the flip, +X is east, +Z is north and the player at yaw 0 looks north,
which matches the screenshots. Every table gives both columns; when only one is given, it is the
prototype value.

Water is at y = 0. The compound plateau is at y = 10. The bunker floor is at **y = 2.5** (above sea level,
so the ocean never cuts through the rooms).

## 1. Level table

Floor heights are absolute y. "Size" is the X × Z footprint.

| Zone | Prototype X range | Prototype Z range | Centre (prototype) | Centre (Unity) | Size X × Z (m) | Floor y | Clear height | Screenshot |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Beach (sand, dock, boat) | −55 … 55 | 92 … 128 | (0, 1, 110) | (0, 1, −110) | 110 × 36 | sand 2.0 → 0 at the shoreline, dock deck 1.3 | open | 01 |
| Forest trail | −90 … 90 (vegetation) | −28 … 92 | (0, 6, 32) | (0, 6, −32) | 180 × 120 | trail 2.0 → 10.0 | open | 02 |
| Compound (fenced plateau) | −22 … 22 | −62 … −28 | (0, 10, −45) | (0, 10, 45) | 44 × 34 | 10.0 | open | 03 |
| Entrance building (lobby) | −6 … 6 | −62 … −48 | (0, 10, −55) | (0, 10, 55) | 12 × 14 | 10.0 | lobby 3.4 (ceiling 13.4), roof 14.0 | 03 |
| Ramp tunnel | −2 … 2 | −70 … −50 | (0, 6.25, −60) | (0, 6.25, 60) | 4 × 20 (plan), 21.4 m along the slope | 10.0 → 2.5 (20.6°) | 3.6 above the slope | — |
| Security office | −8 … 8 | −82 … −70 | (0, 2.5, −76) | (0, 2.5, 76) | 16 × 12 | 2.5 | 3.6 | 04 |
| Corridor | −2 … 2 | −123.6 … −82.4 | (0, 2.5, −103) | (0, 2.5, 103) | 4 × 41.2 | 2.5 | 3.4 | 05, 12 |
| Generator room | −20 … −2.4 | −116 … −96 | (−11.2, 2.5, −106) | (−11.2, 2.5, 106) | 17.6 × 20 | 2.5 (catwalk 5.5) | 6.5 | 06, 07, 11 |
| Laboratory | −10 … 10 | −142 … −124 | (0, 2.5, −133) | (0, 2.5, 133) | 20 × 18 | 2.5 | 4.0 | 13 |
| Hill over the bunker | island | z < cliff line (−62 for \|x\| ≤ 24) | — | — | — | terrain 14.6 → 21.5 | — | — |

Room walls are 0.4 m thick and sit outside the room ranges above. The floor slab is 0.4 m and the ceiling slab is 0.4 m.

### Connections

| From → To | Opening | Prototype position | Unity position | Size W × H (m) | Type |
| --- | --- | --- | --- | --- | --- |
| Beach → Forest | trail start, dock root | (1, ~1, 104) → trail | (1, ~1, −104) | trail 3.4 wide | open path |
| Forest → Compound | gate in the south fence | x ±3.2 at z −28 | x ±3.2 at z 28 | 6.4 × open | gate leaves swung open, barrier arm raised |
| Compound → Entrance | **bunker door** | (0, 10, −48) | (0, 10, 48) | 3.0 × 3.4 | slide, unlocked, E to open |
| Entrance lobby → Ramp | lobby back wall gap | x −2 … 2 at z −50 | z 50 | 4.0 × 3.4 | open |
| Ramp → Security | ramp mouth | x −2 … 2 at z −70 | z 70 | 4.0 × 3.6 | open, hazard strip on the floor |
| Security → Corridor | **security door** | (0, 2.5, −82.2) | (0, 2.5, 82.2) | 2.4 × 2.8 | slide, unlocked |
| Corridor → Generator | **generator door** | (−2.2, 2.5, −106) | (−2.2, 2.5, 106) | 2.4 × 2.8 | hinged, unlocked |
| Corridor → Laboratory | **lab door** | (0, 2.5, −123.8) | (0, 2.5, 123.8) | 2.6 × 2.8 | slide, **maglocked until the blackout** |
| Generator room catwalk | stairs, 10 steps × 0.3 m rise, 0.31 m run | x −6.0 … −2.9, z −116 … −114.6 | z 114.6 … 116 | 3.1 run × 1.4 wide | player only, agents cannot climb |

The escape route is the same chain reversed: Lab → Corridor → Security → Ramp → Lobby → Compound gate →
trail → dock → boat zone.

Door opening speeds (fraction per second): bunker 0.45 (≈2.2 s), slide 1.1 (≈0.9 s), hinge 1.6 (≈0.6 s).
Interaction radius for doors is 2.6 m. The hinged door's collider blocks until it is about 35° open.

### Zone and phase triggers (used for fog, audio, culling and checkpoints)

| Trigger | Prototype rule | Unity equivalent |
| --- | --- | --- |
| zone = bunker | z < −69.5 and y < 9 | z > 69.5 and y < 9 |
| zone = entrance | −6.5 < x < 6.5 and −70.5 < z < −48 | 48 < z < 70.5 |
| interior blend 0 → 1 | smoothstep over z −48.5 → −62 inside the entrance | z 48.5 → 62 |
| phase forest | z < 92 | z > −92 |
| phase entrance | inside the compound rectangle | same rectangle, z 28 … 62 |
| phase generator | inside the generator room and y < 6 | — |
| escape checkpoint 1 | back outside, inside the compound | — |
| escape checkpoint 2 | outside the compound and z > −26 | z < 26 |
| win | within 6 m of (0, 124) with y > 0.5, carrying the cure | (0, −124) |

## 2. Gameplay markers

### Objectives and exit

| Marker | Prototype (x, y, z) | Unity (x, y, z) | Notes |
| --- | --- | --- | --- |
| Generator (hero model) | (−11, 2.8, −106) | (−11, 2.8, 106) | origin on top of a 0.3 m plinth; model ≈ 6.4 (x) × 3.0 (z) × 3.9 m; collider x −14.2 … −7.8, y 2.5 … 6.1, z −107.6 … −104.4 |
| Generator plinth | (−11, 2.5, −106) | (−11, 2.5, 106) | 7.2 × 0.3 × 4.0, grating 10.4 × 7.2 around it |
| Generator console | (−6.9, 2.5, −106) | (−6.9, 2.5, 106) | box x −7.35 … −6.6, z ±0.9, 1.05 high; interact point y 3.65, radius 2.4 m, **hold E 2.0 s**; noise radius 60 m |
| Cure | (0, 3.95, −135) | (0, 3.95, 135) | on a pedestal (top 3.87); taken by walking within 1.05 m; alarm noise radius 18 m |
| Containment chamber | centre (0, 2.5, −135) | (0, 2.5, 135) | glass cylinder r 1.7, 2.9 m tall from y 2.85; opening 1.25 rad (72°) facing the lab door (proto +z, Unity −z); steel base 4.0 × 0.35 × 4.0 |
| Boat | (3.7, 0.15, 122.5), rotY 0.05 rad | (3.7, 0.15, −122.5), rotY −2.9° | hull 2.9 × 8.8 m, bobs ±0.09 m; walkable deck y ≈ 0.78 |
| Boat / exit zone | centre (0, 124), r 6 | (0, −124), r 6 | win condition (y > 0.5, cure carried) |
| Dock | x −1.4 … 1.4, z 97 … 128, deck top y 1.3 | z −128 … −97 | rails 0.8–0.9 m above the deck, gap on +x side for ±4 m around the boat |

### Player start and checkpoints

The prototype teleports to y = 3 (beach), 3.0 (generator), 10.5 (entrance) or 40 (forest), then drops
onto the ground. The table gives the resulting ground height.

| Checkpoint | Prototype (x, z) | Ground y | Proto yaw | Unity (x, y, z) | Unity yaw |
| --- | --- | --- | --- | --- | --- |
| **Player start** = beach | (0, 121) | 1.3 (dock) | 0 | (0, 1.3, −121) | 0° (north) |
| forest | (0, 90) | 1.77 | 0 | (0, 1.77, −90) | 0° |
| entrance | (0, −33) | 10.04 | 0 | (0, 10.04, 33) | 0° |
| generator | (−4.5, −106) | 2.5 | π/2 | (−4.5, 2.5, 106) | 270° (west, toward the generator) |
| escapeEntrance | (0, −40) | 10.0 | π | (0, 10, 40) | 180° (south) |
| escapeForest | (0, −24) | 10.04 | π | (0, 10.04, 24) | 180° |

When the player respawns they get full health and 1.5 s of invulnerability. Escape checkpoints move outward so a death near the
boat does not send the player back underground.

### Agent spawns and routes (all y = ground)

| Agent | Spawn (prototype) | Spawn (Unity) | Route / post (prototype) | Route / post (Unity) |
| --- | --- | --- | --- | --- |
| Hunter-1 (BT) | (−24, 54) y 4.6 | (−24, 4.6, −54) | prowl (−24,54) (−30,30) (−18,12) (−26,−8) (−14,70) (−36,44) | (−24,−54) (−30,−30) (−18,−12) (−26,8) (−14,−70) (−36,−44) |
| Hunter-2 (BT) | (26, 20) y 9.2 | (26, 9.2, −20) | prowl (26,20) (30,44) (20,64) (32,2) (18,−14) (36,30) | (26,−20) (30,−44) (20,−64) (32,−2) (18,14) (36,−30) |
| Patrol-1 (FSM) | (−14, −36) y 10 | (−14, 10, 36) | loop (−14,−36) (14,−36) (16,−46) (10,−55) (−10,−55) (−16,−46) | (−14,36) (14,36) (16,46) (10,55) (−10,55) (−16,46) |
| Patrol-2 (FSM) | (3, −74) y 2.5 | (3, 2.5, 74) | loop (3,−74) (−4,−74) (−4,−79.5) (3,−79.5) | (3,74) (−4,74) (−4,79.5) (3,79.5) |
| Patrol-3 (FSM) | (0, −88) y 2.5 | (0, 2.5, 88) | ping-pong (0,−88) (0,−102) (−0.5,−120) (0,−102) | (0,88) (0,102) (−0.5,120) (0,102) |
| TG-01 (Utility) | (−8, −100) y 2.5 | (−8, 2.5, 100) | post (−8, −100) | (−8, 100) |
| TG-02 (Utility) | (3, −129) y 2.5 | (3, 2.5, 129) | post (2.5, −129) | (2.5, 129) |
| Commander (GOAP) | (6, −138) y 2.5 | (6, 2.5, 138) | post (6, −138) | (6, 138) |

### Cover points (Tactical Guard utility AI, all y = 2.5)

| # | Prototype (x, z) | Unity (x, z) | Where |
| --- | --- | --- | --- |
| 1–4 | (−14.8,−101.2) (−7.2,−101.2) (−14.8,−110.8) (−7.2,−110.8) | (−14.8,101.2) (−7.2,101.2) (−14.8,110.8) (−7.2,110.8) | generator corners |
| 5–12 | (−17.5,−99) (−4.5,−113.5) (−18,−110) (−11,−109.4) (−15.5,−108.6) (−16.8,−106) (−9,−97.5) (−13,−97.5) | (−17.5,99) (−4.5,113.5) (−18,110) (−11,109.4) (−15.5,108.6) (−16.8,106) (−9,97.5) (−13,97.5) | generator room: tanks, cabinets, stairs |
| 13–16 | (−6.5,−130.5) (6.5,−130.5) (−6.5,−137) (6.5,−137) | (−6.5,130.5) (6.5,130.5) (−6.5,137) (6.5,137) | lab benches |
| 17–19 | (−1.2,−92.5) (1.2,−110) (−1.2,−117) | (−1.2,92.5) (1.2,110) (−1.2,117) | corridor pillars |

### Pickups and interactables

| Item | Prototype (x, y, z) | Unity (x, y, z) | Notes |
| --- | --- | --- | --- |
| Health kit 1 | (6.5, 2.5, −80.5) | (6.5, 2.5, 80.5) | security office; +35 HP; radius 1.1; hovers +0.35 m; green light 0x60ff90, I 0.5, r 3 |
| Health kit 2 | (1.2, 2.5, −119) | (1.2, 2.5, 119) | corridor near the lab |
| Health kit 3 | (−18.5, 5.5, −114.5) | (−18.5, 5.5, 114.5) | generator catwalk (reward for exploring) |
| Pushable crate | (0.95, 2.5, −95), 1.5 m cube | (0.95, 2.5, 95) | corridor; translation only in the prototype |

### Set dressing positions (for later W-steps)

| Area | Item | Prototype position / extent |
| --- | --- | --- |
| Compound | retaining wall | x −24 … 24, z −63.4 … −62, y 7 … 15.2; pilasters at x ±9, ±15, ±21 |
| Compound | chain-link fence | 3.0 m panels + razor wire, posts every ~3 m; west/east/south runs; north side is the wall |
| Compound | guard booth "CHECKPOINT 1" | centre (8.5, 10, −32.5), 3.2 × 3.2, 2.95 m tall, window on the south face |
| Compound | truck | centre (−12, 10, −40), 2.6 × 6.6 × 2.6, cab toward +z (proto) |
| Compound | sandbag walls | (−6.5, −31.5) 6 bags × 4 rows; (6.8, −44) 5 × 4 rotY 0.3; (−15, −47) 5 × 3 rotY 90° |
| Compound | crates | (16,−38) 1.2, (17.3,−38.2) 1.2, (16.6,−38.1) 1.0 stacked at y 11.2, (−18,−58) 1.3, (−16.6,−59) 1.1, (−9,−45) 1.0 |
| Compound | barrels (Ø0.6 × 0.9) | (18.5,−45) (19.2,−45.6) (18.6,−46.3) olive; (−19,−36) (7.5,−59) rust |
| Compound | steam vents | (−14, −54) and (13.5, −51), Ø1.3 × 1.6 m, steam 7 particles/s |
| Compound | transformer box | x −8.6 … −6.1, z −54 … −52.4, 2.1 m, "DANGER HIGH VOLTAGE" sign |
| Compound | antenna mast | base (11, 15.2, −67), 16 m lattice, blinking red beacon on top |
| Compound | satellite dish | roof of the entrance building, (−3.5, 15.6, −57) |
| Lobby | guard desk, 4 lockers, extinguisher | desk x −4.9 … −3.0, z −51.6 … −50.6; lockers x 3.2 … 5.24, z −51.95; extinguisher (5.1, −49.2) |
| Beach | warning sign "PRIVATE ISLAND / NO LANDING" | (−2.6, ground, 95.6) |
| Beach | crates, fuel barrel, rope coil on the dock | z 100–101 (dock root), coil at z 112 |
| Security | 2 desks with 4 monitors + 1 wall screen | desks centred (6.6, −76.6) and (6.6, −73.2), 3.0 × 1.0 along z |
| Security | glass partition | x 4.35 … 4.5, z −78 … −72, glass from 0.95 to 2.3 m |
| Security | 12 lockers | west wall x −8 … −7.45, z −80.5 … −73.9, 2.0 m tall |
| Security | checkpoint counter + metal detector | counter x −4.2 … −1.8, z −81.6 … −80.9; detector frame x ±1.05 at z −81 … −80.6 |
| Security | table + 2 chairs, weapon rack, facility map | table centre (−3.2, −76.7) 2 × 1; rack on the west wall z −73.2 … −71.2; map on the north wall |
| Corridor | pillars (also cover) | z −88, −94, −100, −112, −118 on both sides (x ±1.62 … ±2), full height |
| Corridor | puddles, leaking pipe | puddles (0.4,−92) (−0.8,−103.5) (0.6,−115); drip at (0.95, 5.55, −115.2) |
| Corridor | ceiling pipes, cable tray, fire hose, fuse boxes, vents | pipes at y 5.55–5.7; fire hose x 1.85 … 2.0, z −91.6 … −90.6 |
| Generator | 2 diesel tanks | Ø2.0 × 4.2 along z at (−18.6, 3.85, −102) and (−18.6, 3.85, −110) |
| Generator | 6 electrical cabinets with LEDs | x −18 … −10.3 against the north wall (z −96.7 … −96), 2.2 m |
| Generator | catwalk + railing | x −19.6 … −6.0, z −116 … −113.6, deck y 5.5 |
| Generator | wall fan Ø2 m | (−19.75, 7.5, −106), spins down at the blackout |
| Lab | 4 benches 3.4 × 0.9 × 0.88 | centres (±6.5, −129.5) and (±6.5, −136.5) |
| Lab | 3 specimen fridges | east wall x 9.2 … 10, z −127.5, −128.9, −130.3 |
| Lab | jar shelves, fume hood | shelves west wall z −133 … −128; hood x −10 … −9, z −140.5 … −137.5 |
| Lab | commander console + outbreak wall map | console x 4.2 … 8.6, z −141.6 … −140.6; map 3.4 × 1.9 at (6.4, 5.1, −141.96) |

## 3. Material and colour palette

Global palette (from `textures.js`): concrete greys `#77766f` / `#5d5c57`, hazard yellow `#d9a521`,
military olive `#4a5136`, rust `#7a4128`, lab white `#dfe5e6`, cyan accent `#47d1d6`, uniform navy
`#1d2633`, emergency red `#ff2a1a`.

Textures are procedural in the prototype. **Albedo** is the dark → light range the texture paints. **Tint** is
the material colour multiplied on top. R = roughness, M = metallic.

### Structure and interior

| Material | Used on | Albedo range | Tint | R / M |
| --- | --- | --- | --- | --- |
| concrete | exterior walls, entrance building | `#5f5e59` → `#86857d`, streaks `#3d3a33`, rust `#6b4a33` | `#ffffff` | 1.0 / 0 |
| concreteDark | pilasters, roof, apron, generator floor, pillars | same | `#8a8a86` | 1.0 / 0 |
| wallPanel | corridor, lobby, ramp walls | `#6c6e6a` → `#8a8c86`, grime `#3b3b36` | `#ffffff` | 1.0 / 0 |
| wallSec | security walls (+ 1 m wainscot band `#1e2833`) | wallPanel | `#aeb9c4` | 1.0 / 0 |
| wallGen | generator room walls | wallPanel | `#9a968a` | 1.0 / 0 |
| wallLab | lab walls | wallPanel | `#e9eeef` | 0.8 / 0 |
| floorConcrete | security, corridor, lobby floors | `#4c4d4a` → `#6d6e69`, oil `#2c2b28` | `#ffffff` | 1.0 / 0, env 0.35 |
| tiles | lab floor | `#b9c0c1` → `#d8dfe0`, grout `#5c605e` | `#d8dcdc` | 1.0 / 0, env 0.3 |
| ceiling | all ceilings | `#55575a` → `#6e7074`, stains `#5b4a35` | `#ffffff` | 0.95 / 0 |
| hazard | door frames, edges, stripes | `#d9a521` / black, worn `#5a5650` | — | 0.75 / 0 |
| lineYellow | corridor floor lines | — | `#b08a22` | 0.6 / 0 |
| diamondPlate | ramp floor, stairs | `#3c3f41` → `#7b7f80` | `#ffffff` | 1.0 / 0.7 |
| grate | generator grating, catwalk | `#5a5d5e` → `#777a7a` | `#ffffff` | 1.0 / 0.6 |
| labWhite | benches, fridges, pedestal | — | `#dfe5e6` | 0.35 / 0.05 |
| locker | lockers, cabinets | metalPlate | `#51606b` | 1.0 / 0.5 |
| deskTop | desks, counters | — | `#3c4146` | 0.6 / 0.1 |

### Metals, plastics, pipes

| Material | Colour | R / M | Notes |
| --- | --- | --- | --- |
| metalPlate texture | `#4a4d4f` → `#6a6e70`, rust `#6e3c22`, scratches `#9da2a4` | — | base of metal, locker, olive paint, boat hull |
| metal / metalOut | tint `#ffffff` / `#cfcfcf` | 1.0 / 0.65, 0.6 | |
| metalDark / metalDarkOut | `#2b2e31` | 0.45 / 0.7, 0.5 / 0.7 | housings, fixtures, frames |
| steel / steelOut | `#9aa0a4` / `#8a9094` | 0.3 / 0.9, 0.35 / 0.9 | rails, posts, legs |
| rust | `#3e2214` → `#8a4a24` | 1.0 / 0.3 | barrels |
| copper | `#b06a3c` | 0.35 / 0.9 | |
| pipe / pipeRed / pipeYellow | `#5d6a62` / `#7a2a20` / `#a27b1d` | 0.5 / 0.6, 0.55 / 0.4, 0.5 / 0.4 | |
| olivePaint / olivePaintOut | `#6a7350` / `#5f6748` | 1.0 / 0.4 | tanks, truck, consoles |
| cable / rubber | `#121212` / `#1a1a1a` | 0.6 / 0.1, 0.9 / 0 | |
| plasticDark / plasticLight | `#23272b` / `#c9ccc8` | 0.55, 0.5 | monitors, chairs, fixture housings |
| signPost | `#45484a` | 0.6 / 0.5 | |
| blackMatte | `#0c0c0d` | 0.9 | |

### Outdoor and natural

| Material | Albedo range / colour | Tint | R / M |
| --- | --- | --- | --- |
| sand (terrain) | `#6f6450` → `#9a8c70`; wet sand darkened 45 %, roughness 0.25 | — | 0.92 |
| grass (terrain) | `#1f2a15` → `#3a4a24` | — | 0.92 |
| forest litter (terrain) | `#241c14` → `#40331f`, moss `#2c3a1a` | — | 0.92 |
| dirt path (terrain) | `#3d2f22` → `#5e4a35`, stones `#7c7466` | — | 0.92 |
| rock (terrain, slopes, boulders) | `#3f3e3b` → `#7a7872`, cracks `#1c1b1a`, lichen `#5d6436` | `#b8b8b4` | 0.8 on terrain, 1.0 on boulders |
| gravel (compound) | concrete texture × (0.75, 0.74, 0.70) | — | 0.92 |
| barkPine | `#1e1611` → `#5b4636`, grey `#4c4842` | `#ffffff` | 1.0 |
| barkPalm | `#2c241b` → `#6e5d47` | `#ffffff` | 1.0 |
| needles | `#1c3320` → `#4a6f40`, tips `#6f9150` | per tree HSL h 0.26–0.32, s 0.3–0.4, l 0.6–0.7 | 1.0, double-sided |
| leaf (palm fronds) | `#2c4520` → `#5f8640`, dry `#6b6235` | HSL h 0.21–0.26, s 0.4, l 0.64 | 0.85, double-sided |
| bushes | leaf texture | `#9fae86` × HSL h 0.24–0.32, s 0.35, l 0.55–0.65 | 0.85 |
| grassBlades | blade gradient | `#d2e0c0`, alpha cutoff 0.5 | 1.0, double-sided |
| wood / woodDark | `#3b2d20` → `#6a5038`, weathered `#5a5850` | `#ffffff` / `#7a6a5a` | 1.0 |
| crate | `#4a4b2c` → `#5d5f39`, wear `#6e5a3e` | `#ffffff` | 1.0 |
| rope / sandbag | fabric texture | `#8a7652` / `#7d7158` | 1.0 |
| boatHull / boatTrim | metalPlate / — | `#3b4a52` / `#8a867c` | 1.0 / 0.35, 0.6 |
| fence | chain-link alpha | `#b0b4b4`, alpha cutoff 0.5 | 0.5 / 0.6 |

### Glass and emissive (emission above 1 drives bloom)

| Material | Base | Emission colour × intensity | Notes |
| --- | --- | --- | --- |
| glass | `#9fc9d0`, opacity 0.12 | — | R 0.06, M 0.2, transparent, double-sided |
| glassOut | `#2a3a40` | — | opaque dark glass (truck, booth, boat) |
| tube (fluorescent) | `#ffffff` | `#e6f0ff` × 3.0 (instanced tubes use 4.2) | |
| sodium bulb | — | `#ffa555` × 6 | generator high-bays |
| lab panel | — | `#e4f6ff` × 3.2 | |
| emergency | `#330000` | `#ff2a1a`, 0 → 5 after the blackout | pulses |
| redLamp | `#400000` | `#ff1a0a` × 3.0 | mast beacon blinks 3.5 / 0.15 |
| cyanGlow | `#0a2a2c` | `#47d1d6` × 2.5 | chamber ring, fridges, jars |
| amberGlow | `#2a1a05` | `#ffa040` × 2.5 | booth lamp, truck headlight, jars |
| greenLed | `#052a05` | `#40ff60` × 2.0 | jars, LEDs |
| lanternGlass | `#ffd9a0`, opacity 0.9 | `#ffb050` × 4.0 (flickers) | boat lantern |
| floodLens | `#ffffff` | `#fff2d8` × 6.0 | floodlights, wall pack |
| alarm dome | `#400404`, opacity 0.85 | `#ff1a0a` × 0.2 → 2.5 ± 0.8 | alarm beacons |
| generator core | shader | cyan `(0.05,0.35,0.55)` → `(0.5,1.4,1.8)` × level × 2.2 | goes to 0 on disable |
| monitor screens | shader | tint (0.55, 1.0, 0.7), live feed (0.7, 1.0, 0.85) | power fades out 0.4 s after the blackout |

### Reflection environments (procedural cubemaps, 64 px)

| Environment | Top | Middle | Bottom | Extra |
| --- | --- | --- | --- | --- |
| night (outdoor metals) | `#0b1428` | `#05080f` | `#010203` | moon glow (200, 215, 255) |
| interior (powered) | `#1b1d20` | `#0e0f11` | `#050506` | 4 white light strips on the ceiling face |
| emergency (blackout) | `#170303` | `#090101` | `#030000` | red glow on two faces |

On `lights:emergency` every interior material swaps from the *interior* to the *emergency* environment.

## 4. Lighting: powered vs blackout

### Global values

| Setting | Outside | Bunker, powered | Bunker, blackout |
| --- | --- | --- | --- |
| Fog (exponential squared) colour | `#0b1220` | `#0b0d11` | `#0b0203` |
| Fog density | 0.0135 | 0.022 | 0.05 |
| Hemisphere sky colour | `#52668f` | `#3c4048` | `#3a0907` |
| Hemisphere ground colour | `#141820` | `#18191c` | `#060202` |
| Hemisphere intensity | 0.78 | 0.42 | 0.20 |
| Moon (directional) | `#a9bde6`, intensity 0.9 | 0 (fades out at interior 0.2 → 0.7) | 0 |
| Camera clear colour | = fog colour | = fog colour | = fog colour |
| Reflection environment | night | interior | emergency |

The three-way blend is: `outside ↔ inside` by the interior factor (ramp position), then
`inside ↔ blackout` by the blackout value. Blackout is damped toward 1 with rate 1.6/s (63 % after
0.6 s, 95 % after 1.9 s). **The outside does not change at the blackout.** The generator only powers
the bunker, so the floodlights, booth lamp and wall pack stay on.

Unity's ExponentialSquared fog uses the same formula as three.js `FogExp2`
(`f = exp(−(density·d)²)`), so these densities can be copied directly.

**Moon direction** (toward the moon) is `(−0.349, 0.419, −0.838)` in the prototype, which is
`(−0.349, 0.419, 0.838)` in Unity: north-west, 24.8° above the horizon. In Unity the Directional Light
rotation is **(24.8°, 157.4°, 0°)**. Shadows use an 80 × 80 m orthographic box that follows the
camera with texel snapping, near 1, far 220, bias −0.0006, normal bias 0.04. The map is 1024 / 2048 / 2048 px
(Low / Medium / High) and updates every 3 / 2 / 1 frames.

### Sky and ocean

| Item | Value |
| --- | --- |
| Sky horizon / zenith | `#0e1626` / `#02040a`, blend `pow(height, 0.55)` |
| Stars | hashed cells, 0.35 % of cells lit, twinkle, colour (0.85, 0.9, 1.0), fade in above the horizon |
| Moon disc | colour (1.0, 0.97, 0.9) × 3.2 (HDR, blooms), angular radius ≈ 1.3–1.7° (stylised, about 6× the real moon); halo (0.45, 0.55, 0.75) |
| Clouds | fbm, cover 0.5, scroll (0.012, 0.004) per second, colour `(0.03,0.045,0.07)` → `(0.16,0.19,0.26)` lit from the moon side |
| Ocean plane | 520 × 520 m, 150² grid, y = 0, centred (0, −20) proto / (0, 20) Unity; far ring r 250 → 1200 at y −0.3, `#02050a` |
| Gerstner waves (dir, steepness, wavelength m) | (1, 0.6) 0.16 24; (−0.4, 1) 0.12 13; (0.3, −1) 0.10 7; (−1, −0.2) 0.08 3.6 (prototype dirs: negate the second component for Unity); amplitude damped to 25 % in water shallower than 5 m |
| Water colours | deep `#02070b`, sky reflection `#1a2840`, moon glint `#d8e2ff` (specular power 380 × 6), lantern reflection `#ffa040` |
| Foam | depth < 0.55 m, colour linear (0.16, 0.19, 0.22), wave bands, noise breakup |
| Transparency | alpha 0.55 at the shore → 0.95 at 2.5 m depth |

### Outdoor lights (unchanged by the blackout)

Spot angles: three.js `angle` is a half-angle, while Unity `spotAngle` is the full cone, so Unity spot = 2 × angle.
Inner angle ≈ spot × (1 − penumbra).

| Light | Prototype position | Unity position | Colour | Intensity | Range | Other |
| --- | --- | --- | --- | --- | --- | --- |
| Floodlight W (spot) | (−19.5, 18.2, −30.5) → target (−3, 10, −45) | (−19.5, 18.2, 30.5) → (−3, 10, 45) | `#fff0d8` | 5.0 | 42 | spot 55°, inner 25°, + visible cone (r 5.5, `#fff1d6`, 0.42) |
| Floodlight E (spot) | (19.5, 18.2, −30.5) → (4, 10, −44) | (19.5, 18.2, 30.5) → (4, 10, 44) | `#fff0d8` | 5.0 | 42 | same |
| Floodlight N (spot) | (17.5, 17.2, −58) → (2, 10, −42) | (17.5, 17.2, 58) → (2, 10, 42) | `#fff0d8` | 5.0 | 42 | same |
| Wall pack over the bunker door | (0, 13.9, −47.2) | (0, 13.9, 47.2) | `#d8e4ff` | 2.2 | 12 | |
| Guard booth lamp | (8.5, 12.4, −32.2) | (8.5, 12.4, 32.2) | `#ffb060` | 1.6 | 9 | |
| Boat lantern | (2.5, 2.75, 124.2) | (2.5, 2.75, −124.2) | `#ffa64d` | 1.7 | 15 | flicker 0.85 ± 0.1 |
| Mast beacon (emissive only) | (11, ~31.5, −67) | (11, ~31.5, 67) | `#ff1a0a` | blink | — | on when sin(3t) > 0.6 |

### Bunker fixtures (powered)

There are 31 fixtures. Each one is an emissive mesh, a point light and a faux light shaft.

| Room | Count | Type | Positions (prototype) | Colour | Intensity | Range |
| --- | --- | --- | --- | --- | --- | --- |
| Security | 6 | twin fluorescent tube | x −4.5 / 0 / 4.5 × z −73 / −79, y 6.0 | `#e8f0ff` | 1.5 | 9 |
| Corridor | 10 | twin fluorescent tube | x 0, z −85 to −121 every 4 m, y 5.8 | `#e2ecff` | 1.35 | 8.5 |
| Generator | 6 | sodium high-bay | x −16 / −11 / −6 × z −100 / −112, y 8.3 | `#ffa555` | 3.0 | 14 |
| Lab | 5 | ceiling panel | x −5 / 0 / 5 × z −128 / −138, except (0, −138); y 6.45 | `#e4f6ff` | 1.3 | 9 |
| Ramp | 4 | fluorescent tube | x 0, z −53 / −57.6 / −62.2 / −66.8, y 12.3 / 10.6 / 8.9 / 7.2 | `#e2ecff` | 1.2 | 8 |

Point lights use decay 1.6. Two tubes are faulty and flicker on a 7 s cycle: the corridor tube near z −93 and the
security tube at (4.5, −79). Other bunker lights:

| Light | Prototype position | Colour | Intensity | Range |
| --- | --- | --- | --- | --- |
| Generator core | (−11, 5.1, −104.1) | `#46d6ff` | 1.1 × core level | 8 |
| Chamber spot (down) | (0, 5.8, −135) → floor | `#9ff4ff` | 1.5 | 6, spot 63°, inner 25° |
| Cure glow | (0, 4.2, −135) | `#47d1d6` | 1.0 | 6 |
| Monitor screens (×9: 6 security, 3 lab) | 0.5 m in front of each screen | `#7fd8a0` | 0.35 | 3 |
| Health kits (×3) | 0.6 m above each kit | `#60ff90` | 0.5 | 3 |

### Blackout sequence (on `generator:disabled`)

1. Each fixture dies at `t = 0.35 + 0.028 × d + random(0 … 0.12)` s, where d is its distance from
   (−11, 4.5, −106). It strobes for 0.3 s before it dies, then sparks and pops. The farthest (ramp top) dies at about 2.0 s.
2. Fog, hemisphere and ambient blend to the blackout values (rate 1.6/s), starting immediately.
3. The generator core fades out, the flywheel and wall fan spin down, the hum pitches down and the screens fade over 0.4 s.
4. At cascade end + 0.5 s (≈ 2.5 s), the emergency set fades in over 1.2 s and the interior reflections swap to the emergency environment.

| Emergency light | Count | Positions (prototype) | Colour | Intensity | Range | Animation |
| --- | --- | --- | --- | --- | --- | --- |
| Emergency lamp | 14 | security (−7.85, 5.6, −76), (7.85, 5.6, −79.5); corridor (1.9, 5.5, −86), (−1.9, −95), (1.9, −104), (−1.9, −114), (1.9, −121); generator (−19.85, 6.5, −99), (−19.85, 6.5, −112), (−9, 6.5, −96.05); lab (−9.85, 5.9, −130), (9.85, 5.9, −137), (0, 5.9, −141.85); ramp (1.95, 7.0, −60) | `#ff2614` | 0 → 2.2 | 9 (decay 1.5) | pulse 0.75 + 0.25·sin(3.2t); lamp emission (5, 0.35, 0.2) |
| Alarm beacon (rotating spot) | 3 | corridor (0, 5.82, −98), (0, 5.82, −117); generator (−11, 8.8, −106) | `#ff1808` | 0 → 9 | 22 (decay 1.3) | spot 48°, inner 29°, 3.6 rad/s, tilted 0.25 rad down; beam cone r 2.6 × 9 m, intensity 0.55 |

### Post-processing (same before and after the blackout)

The blackout is done entirely with lights, fog and reflections. Post-processing does not change.

| Setting | Low | Medium | High | Suggested URP start value |
| --- | --- | --- | --- | --- |
| HDR | no | half-float | half-float | HDR on |
| Render scale (pixel-ratio cap) | 0.8 | 0.85 | 1.25 | 0.85 / 1.0 |
| Bloom (strength, radius, threshold) | off | 0.75, 0.5, 0.85 at 35 % res | same at 50 % res | Intensity 0.75, Scatter 0.5, Threshold 0.85 |
| Tone mapping | ACES | ACES, exposure 1.0 | ACES | ACES, Post Exposure 0 |
| Vignette | — | 0.55 (smoothstep 1.05 → 0.25 radius) | 0.55 | Intensity ≈ 0.4, Smoothness ≈ 0.5 |
| Film grain | — | 0.035 | 0.035 | Thin1, Intensity ≈ 0.15 |
| Anti-aliasing | none | FXAA | FXAA | FXAA on the camera |
| Camera FOV (vertical) | 72° | 72° | 72° | 72°, near 0.08, far 700 |
| Point / spot light budget | 3 / 1 | 4 / 2 | 8 / 3 | Forward+ (no per-object cap) |
| Grass density | 35 % | 70 % | 100 % | detail density per quality level |

Gameplay overlays (Core Developer, not World Builder): damage vignette colour (0.55, 0.02, 0.02),
low-health desaturation 60 %, chromatic split damage × 0.004, invulnerability tint (0.25, 0.35, 0.45).

## 5. Vegetation

Placement is deterministic (seeded RNG). The counts below are the actual output of the prototype code.

| Type | Count | Seed | Area (prototype) | Rules | Size | Collider |
| --- | --- | --- | --- | --- | --- | --- |
| Pine | **465** | 1234 | jittered 4.6 m grid, x −118 … 118, z −175 … 100 | skip: ground < 1 m, < 3.4 m from the path, compound ± 5 m, cliff face, dock approach, slope steeper than ~37°; density 0.2 → 1.0 nearer the path, × 0.5 on the hill, × 0.3 in noise clearings | scale 0.78–1.28; trunk 9.6 m, Ø0.68 base; 6 cone tiers up to ~10.6 m | cylinder r 0.3 × scale |
| Palm | **65** | (same pass) | low ground near the beach (h < 3.2, z > 60, 45 % kept) or 18 % of trees with z > 35 and h < 5.5 | — | 7.5 m curved trunk, 9 fronds 3.4–4.4 m, 4 coconuts | cylinder r 0.25 × scale |
| Bush | **420** (cap) | 4321 | x ±90, z −60 … 95 | 3.8–45 m from the path, ground ≥ 1.6 | scale 0.6–1.4, ~1 m tall | soft (does not block sight or bullets), no shadows |
| Grass tuft | **6000** (cap) | 999 | x ±80, z −62 … 98 | 1.4–38 m from the path, ground ≥ 1.3, in noise clumps or within 4 m of the path | 3 crossed quads 1.0 × 0.75 m, scale 0.6–1.25 | none, no shadows |
| Rock | **170** (116 with colliders) | 777 | x ±110, z −120 … 104 | near the cliff (± 6 m): scale 1.6–4.2; elsewhere 0.3–1.5 with 45 % kept; not on the path or in the compound | 3 variants | cylinder if scale > 0.7 |
| Fallen log | **12** | 55 | x ±50, z −20 … 85 | 4–25 m from the path | length 3–6 m, Ø ≈ 0.6 | 3 cylinders each |

Distribution by area:

| Area (prototype z) | Pines | Palms |
| --- | --- | --- |
| Beach (z ≥ 60) | 39 | 53 |
| Forest (−30 … 60) | 297 | 12 |
| Compound band and cliff (−70 … −30) | 77 | 0 |
| Hill over the bunker (z < −70) | 52 | 0 |

The trail is a Catmull-Rom spline through 12 control points. It is **159.4 m** long and 3.4 m wide (dirt),
with ruts at ± 1.6 m. Control points (prototype x, z): (1,104) (0,96) (−6,85) (4,72) (12,58) (4,44)
(−9,32) (−10,16) (2,3) (9,−10) (3,−22) (0,−30). For Unity, negate z.

Wind sway grows with (height / reference)², and each instance has its own phase. Amplitude / reference height: pine 0.32 / 9.5 m,
palm 0.45 / 8 m, bush 0.08 / 1.4 m, grass 0.14 / 0.75 m.

**Terrain** covers 400 × 400 m on a 2 m grid: prototype x −200 … 200, z −220 … 180 (Unity z −180 … 220).
Heights range from −5.93 to 21.52 m, and the prototype packs them as −8 … 24. Sample heights: dock root 0.97, forest
checkpoint 1.77, trail midpoint (4, 44) 4.70, gate 10.08, mast base 15.49, hilltop (0, −90) 21.08.
The shoreline is at z 105.0 (x 0), 101.2 (x 30) and 92.7 (x 55). The beach slopes 0.115 per metre (2.0 m at z ≈ 88 down to 0 at the shoreline).
The terrain layers blend by height (sand below 2.8 m south of z 60), slope (rock from 0.30 to 0.48 slope), a path mask and a compound gravel
mask. Tiling per metre: sand 0.22, grass 0.30, litter 0.28, dirt 0.30, rock 0.12, gravel 0.2.

## 6. What the prototype fakes that Unity needs a real solution for

| # | Prototype trick | Unity solution |
| --- | --- | --- |
| 1 | Analytic heightmap baked to a 2 m grid | Unity **Terrain** generated by an Editor script with a fixed seed: terrain at y −8, height 32 m, heightmap 513² (0.78 m) over 400 m |
| 2 | Custom splat shader (height, slope and painted mask, wet sand) | **Terrain Layers** (sand, grass, litter, dirt, rock, gravel) with an alphamap generated by the same rules; wet sand as a darker/smoother sand layer |
| 3 | Shader `discard` hole for the entrance | `TerrainData.SetHoles` over the entrance building footprint |
| 4 | Virtual light pool (3–8 real lights shared by ~60 lamps) | URP **Forward+** rendering path with real lights and ranges; baked or mixed for static fixtures where possible; light layers to keep outdoor lights out of the bunker |
| 5 | Fog colour/density blended by zone and blackout | Script lerping `RenderSettings.fogColor` / `fogDensity` (URP Volumes have no fog), driven by trigger volumes and the blackout event |
| 6 | HemisphereLight blended by zone | `RenderSettings.ambientMode = Trilight` (sky / equator / ground) lerped by the same script; light probes so dynamic objects pick up local light |
| 7 | Procedural env cubemaps swapped at the blackout | **Reflection probes** per room (baked), with a second "emergency" set or probe intensity animated at the blackout |
| 8 | Additive cone meshes for light shafts and alarm beams | Same idea as a URP Shader Graph (additive, fresnel edge, fog fade); URP has no built-in volumetric lighting |
| 9 | Sky dome shader (gradient, stars, moon, clouds) | Skybox material from Shader Graph (or a cubemap); the moon is the Directional Light plus a disc in the sky shader |
| 10 | Ocean shader with a baked terrain-height texture for foam | Shader Graph water: Gerstner vertex waves; foam and shallow damping from **Scene Depth** (enable the Depth Texture on the URP asset) |
| 11 | Zone culling (island hidden underground, bunker hidden outside) | **Occlusion culling** bake with the hill and walls as occluders, plus trigger volumes that toggle the Terrain/Forest roots if needed |
| 12 | Hand-merged static batches | Mark geometry **Static** (static batching, SRP Batcher); GPU instancing for repeated props |
| 13 | Instanced emissive tubes with per-instance colour for the cascade | Emissive materials and a script that drives emission per fixture (MaterialPropertyBlock or per-fixture material instance) in step with its Light |
| 14 | Analytic box/cylinder/ramp collision | Real colliders: TerrainCollider, BoxColliders for walls, tree colliders on the Terrain prototypes, a sloped BoxCollider for the ramp |
| 15 | 1 m nav grid with walls and open doors | **NavMesh** (AI Navigation package) baked from the level; NavMeshObstacle (carve) on doors and the crate; Agent Controller owns agents, World Builder provides walkable surfaces |
| 16 | Every texture painted on canvas at load | Real texture assets (placeholders first) imported as albedo/normal/roughness; see the list below |
| 17 | Signs and decals drawn with canvas text | Quads with sign textures, or URP Decal Projectors (enable the Decal renderer feature) |
| 18 | Viewmodel light from `sampleAt` | Unity Light Probe Group in the bunker and compound |
| 19 | CPU particle pools (steam, sparks, drips) | Particle System prefabs (pooled) or VFX Graph |
| 20 | Wind vertex shader patch | Shader Graph wind (vertex offset ∝ height²) on foliage materials, or WindZone with compatible tree shaders |
| 21 | Live camera feed into a 256 × 144 target | Camera rendering to a RenderTexture at about 5 Hz, High quality only |
| 22 | Moon shadow camera that follows the player with texel snapping | URP cascaded shadows: shadow distance ≈ 60–80 m, 2–4 cascades |
| 23 | Quality presets switching light counts, grass and shadows | URP assets per Quality level (`PC_RPAsset`, `Mobile_RPAsset` already exist) and terrain detail density per level |
| 24 | No LODs; distance handled by fog | LODGroup on trees, rocks and props; fog still hides the far edge |
| 25 | Monitor static / data screens shader | Small Unlit Shader Graph with a `_Power` property faded at the blackout |
| 26 | Boat bob and lantern flicker with `Math.random` | Small script with seeded noise (deterministic) |

**Placeholder assets** the team will need to supply later: albedo/normal/roughness for sand, grass,
forest litter, dirt, rock, gravel, concrete, wall panel, floor concrete, lab tiles, ceiling, hazard
stripes, diamond plate, grating, metal plate, rust, bark (pine, palm), pine needles (alpha), palm
leaf (alpha), grass blades (alpha), wood, crate, fabric, chain-link fence (alpha), puddle; sign/decal
images (warning, restricted, biohazard, high voltage, diesel, "B-1", "GENERATOR G-1", facility map,
outbreak map, med cross); tree, rock, log, boat and generator models; ambient audio loops (section 7).

## 7. Ambient audio (reference; check the playbook for which role owns it)

| Sound | Where | Prototype recipe | Level |
| --- | --- | --- | --- |
| Waves | outside, louder toward the shore | brown noise, low-pass 520 Hz, slow swell 0.6 + 0.4·sin(0.55t)·sin(0.21t + 1) | (0.05 + 0.32 × shore) × (1 − interior), shore = smoothstep over z 40 → 100 (proto) |
| Wind | outside | white noise, band-pass 500 ± 300 Hz drifting | 0.045 × (1 − interior) |
| Insects | forest (proto z < 85), outside | 3 short chirps, 3.8–5.4 kHz, every 0.15–1.05 s, random spot within 15 m | 0.025 each |
| Bunker room tone | inside | brown noise, low-pass 180 Hz; reverb send 0.4 × interior | 0.06 × interior |
| Generator hum | positional at (−11, 4.0, −106) | 50 Hz sine 0.5, 100 Hz saw 0.12, 150 Hz sine 0.15, 300 Hz triangle 0.03, low-pass 420 Hz, 0.4 Hz wobble; ref distance 4 m, rolloff 1.3 | 0.55; pitches down to silence at the blackout |
| Alarm siren | bunker, after the blackout | square wave alternating 620 / 880 Hz every 0.625 s, band-pass 900 Hz Q 2 | 0.05 + 0.07 × interior |

## 8. Prototype discrepancies and scale notes

- The README says the ramp is 22°; the code (`level.js`) gives 7.5 m drop over 20 m = **20.6°**.
- The README says "two alarm beacons"; the code builds **three** (two in the corridor, one in the generator room).
- The README says the trail is "about 140 m"; the spline is **159.4 m**.
- A comment in `bunker_exterior.js` says the lobby back wall is at z −52; the value used is `ramp.zTop` = **−50**.
- The beach checkpoint (0, 121) is on the dock, not the sand; the player lands on the 1.3 m deck.
- **Prototype scale vs the W1 real-scale targets:**

| Item | Prototype | W1 target |
| --- | --- | --- |
| Player height / eye | 1.75 / 1.63 (crouch 1.15), radius 0.35 | 1.8 |
| Corridor width | 4.0 (x −2 … 2) | 3.0 |
| Room ceiling | 3.4 (corridor), 3.6 (security), 4.0 (lab), 6.5 (generator) | 3.0 (except tall rooms) |
| Doors | 2.4–2.6 × 2.8 inside, bunker 3.0 × 3.4 | 1.4 × 2.3 |

  The bunker interior will shrink in W1. Coordinates that depend on the corridor width (for example the
  generator door at x −2.2) will move. Outdoor coordinates are unaffected.
