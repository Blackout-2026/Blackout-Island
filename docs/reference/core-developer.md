# Core Developer reference (extracted from the web prototype)

Source: `prototype/` (read-only). Numbers here come from the code, not the README, wherever the two
disagree (see section 9). Files read: `README.md`, `screenshots/` (06, 07, 08, 09, 13, 04),
`src/models/generator.js`, `robot.js`, `humanoid.js`, `weapons.js`, `src/materials/materials.js` and
`textures.js` (robot paint, camo, gauge), `src/world/bunker_interior.js` (props, cure chamber),
`bunker_exterior.js` and `beach.js` (outdoor props), `src/physics/interactables.js` (cure case,
medkit), `crate.js`, `doors.js` (door visuals), `src/world/level.js` (agent counts).

Dimensions in sections 2 to 6 were **measured** by running the prototype's own model builders in Node
(three.js r128, materials stubbed, script kept outside the repo) and taking bounding boxes and
triangle counts of the resulting geometry. Anything marked **(guess)** is my judgement, not code.

## 0. Units and coordinate conversion

| Item | Prototype (three.js) | Unity (this project) |
| --- | --- | --- |
| Unit | 1 unit = 1 metre | 1 unit = 1 metre |
| Up | +Y | +Y |
| Handedness | right-handed | left-handed |
| Character facing (model local) | +Z | +Z |

The level reference (`docs/reference/world-builder.md`) converts world positions with
`Unity = (x, y, -z)`. For models I use three rules so a model looks the same as in the screenshots
**(chosen, not in the code)**:

| Model | Prototype local frame | Rule for Unity / Blender |
| --- | --- | --- |
| Generator | origin = top of the plinth, length along X, control face at **+X** | `(x, y, -z)`, the same flip as the level, so the room reads the same. Control face stays at +X. Valve wheel, beacon and cables swap sides in Z. |
| Robot, humanoids | origin = feet on the ground, face +Z | `(-x, y, z)`. The character still faces +Z, but its own left/right stay correct (prototype right arm at -X becomes +X). The robot's weapon pod is on its **right** arm. |
| Weapon viewmodels | camera space: +X right, +Y up, **-Z forward** | `(x, y, -z)`: forward becomes +Z, right stays +X. |

Joint and part names below are the prototype's bone names (`thighL`, `shinR`, ...). The Agent Controller
needs these names for procedural animation (step C3 writes the final list into `docs/art-pipeline.md`).

## 1. Model overview

Triangle counts are the prototype's primitive geometry as built (non-indexed, so a face is 2 triangles
for a box). They show the **detail level to match**, not a budget; step C1 proposes the real budgets.

| Model | Count in level | Role in this project | Prototype tris (measured) | Height x width x depth (m) |
| --- | --- | --- | --- | --- |
| Generator "G-1" | 1 | **Hero model 1** | 8,840 (+1,080 floor cables) | 4.10 x 6.96 x 2.90 (see 2.2) |
| Security robot (Tactical Guard) | 2 (TG-01, TG-02) | **Hero model 2** | 3,216 | 2.38 x 1.07 x 0.64 |
| Security Patrol (humanoid) | 3 | character | 2,892 | 1.89 x 0.53 x 1.10 (with rifle) |
| Hunter (humanoid, ghillie) | 2 | character | 3,112 | 1.90 x 0.57 x 1.15 |
| Commander (humanoid, lab coat) | 1 | character | 2,612 | 1.89 x 0.53 x 0.66 |
| SMG viewmodel + hands | 1 | first person | 1,048 | 0.65 x 0.53 x 0.77 |
| Sniper viewmodel + hands | 1 | first person | 1,700 | 0.65 x 0.55 x 1.29 |
| Knife viewmodel + hand | 1 | first person | 532 | 0.32 x 0.58 x 0.66 |
| Cure case | 1 | pickup | about 250 **(guess)** | 0.28 x 0.42 x 0.24 |
| Medkit | 3 | pickup | about 150 **(guess)** | 0.26 x 0.42 x 0.28 |

Visual style in every screenshot: low-poly, chamfered boxes and cylinders, flat painted colours with a
subtle noise texture, strong emissive accents (cyan, amber, red). It is not photoreal. A Blender model with
clean quads and bevelled edges at roughly the same triangle count will look right.

## 2. Hero model 1: generator "G-1" (3.2 MW diesel generator)

Screenshots: `06_generator_before_blackout`, `07_hero_generator`, `11_generator_after_blackout`.

### 2.1 Silhouette

A long, low locomotive-like machine lying along X. From the left (-X) to the right (+X):

1. A tall, boxy **diesel engine block** (olive green, about 2.7 m long, 1.5 m tall) with a row of 6
   cylinder heads on top and cooling fins along both sides.
2. A **spinning flywheel disc** and a yellow/black **belt guard** at the far -X end.
3. A **round alternator housing** (1.05 m radius, cylinder along X) whose middle section is a
   ribbed glass tube showing a glowing cyan **energy core**. This is the visual centre of the model.
4. A flat **control face** at the +X end with three round gauges, a row of LEDs and a red valve wheel.

On top: a thick rusty **exhaust stack** rising from the middle (it ends at the room's ceiling pipe), a small
amber **beacon** over the engine, an air-filter canister. Along the sides: coolant pipes on the +Z side,
the exhaust runner pipe and copper stubs on the -Z side. Power cables leave the machine on the -Z side and
run across the floor.

### 2.2 Dimensions (front, side, top)

Local origin is the top of the plinth. Machine only, floor cables excluded.

| View | What you see | Extent | Size (m) |
| --- | --- | --- | --- |
| **Front** (looking down -Z at the long side: X by Y) | engine on the left, glass housing right, stack on top | x -3.53 ... +3.43, y 0 ... 4.10 | **6.96 long x 4.10 tall** |
| **Side** (looking down X at the control face: Z by Y) | control panel, 3 gauges, wheel; stack behind it | z -1.45 ... +1.45, y 0 ... 4.10 | **2.90 wide x 4.10 tall** (the round housing is 2.10 wide and 2.10 tall, y 0.20 ... 2.30) |
| **Top** (looking down: X by Z) | skid rectangle with the engine, then the round housing | x -3.53 ... +3.43, z -1.45 ... +1.45 | **6.96 x 2.90** |

Key sub-dimensions:

| Part | Size (m) |
| --- | --- |
| Skid frame | 6.4 x 0.22 x 2.9 (two side rails, deck plate, 5 cross beams) |
| Engine block | 2.7 (x) x 1.5 (y) x 1.9 (z), centred at x -1.7, y 1.0 (spans y 0.25 ... 1.75) |
| Alternator housing | radius 1.05, axis along X at y 1.25, from x 0.2 to 3.0 (2.8 long) |
| Glass section | 1.7 long, radius 1.02 |
| Energy core | radius 0.62, 1.6 long; chrome rod radius 0.16, 1.9 long |
| Exhaust stack | radius 0.30 at the bottom, 0.24 at the top, 1.8 tall, centred x 1.5, spans y 2.3 ... 4.1 |
| Control panel | 0.3 (x) x 1.5 (y) x 1.6 (z), face at x 3.1, centred y 1.35 |
| Flywheel | radius 0.62, 0.14 thick, at x -3.45, y 1.0 |
| Beacon | radius 0.10, 0.18 tall, at (-0.6, 2.55, -0.5) |

The room code puts a box collider of 6.4 x 3.6 x 3.2 m around the generator (x +/-3.2, z +/-1.6). Use the
same as the collider proxy.

### 2.3 Parts list

Positions are prototype local coordinates (`x` along the machine, `y` up, `z` across). Apply the rule
in section 0 when building. "tris" is not given per part; the whole model is 8,840.

| # | Part | Count | Shape and size (m) | Position | Material |
| --- | --- | --- | --- | --- | --- |
| 1 | Side rails | 2 | boxes 6.4 x 0.12 x 0.2 | y 0 ... 0.12, z +/-1.35 | metalDark |
| 2 | Deck plate | 1 | box 6.4 x 0.1 x 2.8 | y 0.12 ... 0.22 | metalDark |
| 3 | Cross beams | 5 | boxes 0.16 x 0.22 x 2.9 | x = -2.8, -1.4, 0, 1.4, 2.8 | metalDark |
| 4 | Engine block | 1 | rounded box 2.7 x 1.5 x 1.9, radius 0.18 | (-1.7, 1.0, 0) | olive paint |
| 5 | Cooling fins | 28 (14 per side) | thin plates 0.04 x 1.1 x 0.16 | x from -3.0 step 0.19, both z faces | metalDark |
| 6 | Cylinder heads | 6 | rounded boxes 0.36 x 0.28 x 0.9 | x -2.75 ... -0.65 step 0.42, y 1.9 | metalDark |
| 7 | Rocker caps | 12 | cylinders r 0.05, h 0.12 | y 2.08, z +/-0.25, above each head | chrome |
| 8 | Intake manifold | 1 | cylinder r 0.11, 2.6 long along X | (-1.7, 1.8, 0.62) | steel |
| 9 | Exhaust runners | 6 | cylinders r 0.06, 0.4 long, tilted 0.9 rad | y 1.75, z -0.65, under each head | copper |
| 10 | Exhaust pipe | 1 | cylinder r 0.14, 2.6 long along X | (-1.7, 1.6, -0.88) | rust |
| 11 | Air filter | 1 | cylinder r 0.32, h 0.6 + lid r 0.34, h 0.06 | (-0.6, 2.05, 0.55) | metalDark, chrome lid |
| 12 | Belt guard | 1 | rounded box 0.3 x 1.4 x 1.2, radius 0.15 | (-3.25, 1.1, 0) | hazard (yellow/black stripes) |
| 13 | Flywheel | 1 | disc r 0.62, 0.14 thick, 6 holes r 0.08 on a 0.38 circle | (-3.45, 1.0, 0) | steel disc, metalDark holes |
| 14 | Coupling | 1 | cylinder r 0.6, 0.6 long along X | (-0.1, 1.15, 0) | metalDark |
| 15 | Housing end sections | 2 | solid cylinders r 1.05, 0.55 long | x 0.2 ... 0.75 and 2.45 ... 3.0, y 1.25 | olive paint |
| 16 | Housing ribs | 6 | tori R 1.05, tube 0.05 | evenly across x 0.75 ... 2.45 | metalDark |
| 17 | Housing bars | 6 | boxes 0.06 x 0.06 x 1.7 around the circumference | x centre 1.6 | metalDark |
| 18 | Support saddles | 2 | boxes 0.3 x 0.38 x 1.8 | x 0.6 and 2.6, y 0.22 ... 0.6 | metalDark |
| 19 | Glass tube | 1 | open cylinder r 1.02, 1.7 long | (1.6, 1.25, 0) | glass (12 % opacity, tint #9fc9d0) |
| 20 | Energy core | 1 | cylinder r 0.62, 1.6 long | (1.6, 1.25, 0) | **emissive core shader** (see 2.5) |
| 21 | Core rod | 1 | cylinder r 0.16, 1.9 long | (1.6, 1.25, 0) | chrome |
| 22 | Exhaust stack | 1 | cylinder r 0.24 / 0.30, 1.8 tall + flange r 0.34, h 0.14 | (1.5, 3.2, 0), flange y 2.35 | rust, metalDark |
| 23 | Control panel | 1 | rounded box 0.3 x 1.5 x 1.6, radius 0.08 | (3.10, 1.35, 0) | metalDark |
| 24 | Gauge faces | 3 | discs r 0.174 with chrome bezel torus (tube 0.025) | x 3.26, y 1.65, z -0.45 / 0 / +0.45 | gauge decal, chrome |
| 25 | Gauge needles | 3 | thin boxes 0.014 x 0.14 x 0.004 on a pivot at the dial centre | same as gauges | red #d02010 |
| 26 | Indicator LEDs | 5 | spheres r 0.03 | x 3.27, y 1.05, z -0.5 ... +0.5 step 0.25 | emissive green (4), amber (1, the 4th) |
| 27 | Valve wheel | 1 | torus R 0.22, tube 0.03 + 3 spokes 0.42 long; stem r 0.05, 0.3 long | (3.40, 0.75, 0.35) | pipeRed, steel stem |
| 28 | Coolant pipes | 2 runs + 2 risers | cylinders r 0.07 and r 0.05 along X, 5.6 long | y 0.5 and 0.75, z 1.15 and 1.2 | pipe (grey-green), pipeYellow |
| 29 | Side valves | 3 | cylinders r 0.1, 0.16 long + red torus handle R 0.12 | x -1.6, 0.9, 2.2; y 0.5, z 1.25 | steel, pipeRed |
| 30 | Power cables | 3 | tubes r 0.05 from the machine to the floor, then 2 to 3 m along it | start (2.6, 0.7, -0.9), (2.4, 0.7, -0.95), (0.8, 0.5, -1.0) | cable (black) |
| 31 | Beacon | 1 | cylinder r 0.10, 0.18 tall + chrome reflector + base r 0.12, 0.06 | (-0.6, 2.55, -0.5) | amber emissive, chrome, metalDark |
| 32 | Label decals | 4 | planes: "DANGER 11 kV" 0.9 x 0.35 on the housing; "G-1 // 3.2 MW" 0.9 x 0.35 on both engine sides; "MAIN CONTROL" 0.7 x 0.18 above the gauges | housing z +1.07; engine z +/-0.97; panel (3.26, 2.0, 0) | decals, see colours below |

### 2.4 Materials and colours

| Material | Colour | Metal / rough (prototype) | Where |
| --- | --- | --- | --- |
| Olive paint | #5f6a4a, metal-plate texture | 0.4 / 1.0 | engine block, housing ends |
| metalDark | #2b2e31 | 0.7 / 0.45 | frame, heads, fins, ribs, saddles, panel |
| Steel | #9aa0a4 | 0.9 / 0.3 | manifold, flywheel, valve stem |
| Chrome | #d9dde0 | 1.0 / 0.12 | rod, bezels, caps |
| Copper | #b06a3c | 0.9 / 0.35 | exhaust runners |
| Rust | rust texture (orange-brown) | 0.3 / 1.0 | exhaust pipe, stack |
| Hazard | yellow/black diagonal stripe texture | 0 / 0.75 | belt guard |
| Pipe / pipeRed / pipeYellow | #5d6a62 / #7a2a20 / #a27b1d | 0.6 / 0.5, 0.4 / 0.55, 0.4 / 0.5 | plumbing, valve wheel |
| Cable | #121212 | 0.1 / 0.6 | cables |
| Glass | #9fc9d0, opacity 0.12, double sided | 0.2 / 0.06 | housing tube |
| Gauge face | cream #d9d4c3 dial, arcs green #2e8b3a, amber #d9a521, red #b3241b | 0 / 0.4 | 3 gauges |
| Label yellow / navy / black | #e8b52a on black text; #1d2633 with #e8e8e0 text; #111 with #d9a521 text | 0 / 0.6 | decals |

Unity URP/Lit: smoothness = 1 - roughness; use the same metalness.

### 2.5 Emissive and animated parts

| Part | Emissive | Running | After `generator:disabled` |
| --- | --- | --- | --- |
| Energy core | cyan-white shader: colour blends (0.05, 0.35, 0.55) to (0.5, 1.4, 1.8), bright rim, vertical bands scrolling; intensity = level x 2.2 | level 0.92 +/- 0.08, a fast flicker | flickers 1.6 s (random 0.15 or 1.1 level, fading) then 0 (dark) |
| Indicator LEDs | green (0.3, 3.0, 0.5) | on | near off (0.02, 0.05, 0.02) |
| Warning LED (4th) | amber (3.0, 1.6, 0.2) | slow blink (4 Hz square) | fast blink (10 Hz), stays on as a fault |
| Beacon | amber #ffa020, intensity 2.5 | rotates 5 rad/s about Y | intensity 0.05, stops |
| Gauges | none | needles hover at 0.35 / 0.47 / 0.59 of the dial with jitter | needles fall to zero (about 2.5 s) |
| Flywheel | none | spins 9 rad/s about X | slows to 0 (about 5 s) |

Other effects on disable: 60 + 50 sparks at the control face and the stack, a smoke plume, then
intermittent spark bursts every 0.4 to 2.0 s at (2.9, 0.6, -1.2) and a steady smoke emitter at
(-1.7, 2.0, 0). The Unity prefab needs empty child transforms for these (step C2 already lists
`InteractPoint`, `SparkVFX`, `HumAudioSource`).

Moving parts (need separate objects with the pivot at the rotation centre): **flywheel** (pivot at its
centre, axis X), **beacon** head (axis Y), **three gauge needles** (pivot at the dial centre, axis along X,
sweep 270 degrees from -135 to +135), **valve wheel** (static in the prototype; spin is optional).

### 2.6 Detail level

High for this game: 8,840 triangles (1,080 more in the floor cables). Everything is chamfered, nothing is a bare box.
The detail that reads in the screenshots at 3 to 8 m: the **fins**, the **6 cylinder heads**, the **ribbed
glass tube with the glowing core**, the **gauges**, the **stack** and the **hazard belt guard**. Items that
could be dropped or baked into the texture if the budget is tight **(guess)**: rocker caps, side valves,
LEDs (can be emissive quads), the cables.

## 3. Hero model 2: security robot (Tactical Guard)

Screenshots: `08_hero_robot`, `09_enemy_fight` (human agents only), `13_lab`.

### 3.1 Silhouette

A **bipedal armoured robot, about 2.1 m to the top of the helmet** (2.38 m with the antenna), broad
shoulders, narrow waist. Key shape points:

- **Digitigrade (reverse-knee) legs**: the thigh slopes forward, the knee bends backward, the shin angles
  down and back, ending in a **clawed three-toe foot** (two toes forward, one heel spur back).
- **Segmented waist**: three stacked rings between pelvis and chest, alternating paint and dark joint.
- **Wide armoured chest** with a raised front plate, yellow hazard bars on the front, a vent grille,
  and a **back power pack** with three glowing cyan cell bars.
- **Head**: a rounded helmet, a dark visor slot with a thin **red emissive eye bar**, a yellow
  roof plate, a whip **antenna** with a red tip, and a small red **scanner dot** that sweeps across the visor.
- **Asymmetric arms**: the left arm ends in a three-finger **claw hand**; the right forearm is replaced
  by a **twin-barrel weapon pod** with an ammo drum on its outer side. Both shoulders have a plate and a
  piston along the upper arm.

### 3.2 Dimensions (front, side, top)

Rest (bind) pose, standing, feet on the floor. Height is to the helmet roof plate (2.13 m) and to the
antenna tip (2.38 m).

| View | Extent | Size (m) |
| --- | --- | --- |
| **Front** (X by Y) | x -0.53 ... +0.53, y 0.01 ... 2.38 | **1.07 wide x 2.38 tall** (2.13 to the helmet) |
| **Side** (Z by Y; +Z is front) | z -0.38 ... +0.26, y 0.01 ... 2.38 | **0.64 deep x 2.38 tall** (back pack at the rear, toes at the front) |
| **Top** (X by Z) | x -0.53 ... +0.53, z -0.38 ... +0.26 | **1.07 x 0.64** |

Joint heights (rest pose, Y above floor) and offsets, for placing pivots:

| Joint | Height | Notes |
| --- | --- | --- |
| Pelvis (root) | 1.05 | centre of the model; lowered 0.04 while walking |
| Spine | 1.19 | 0.14 above the pelvis |
| Chest | 1.49 | 0.30 above the spine |
| Head | 1.93 | 0.44 above the chest, 0.02 forward |
| Shoulder (upperArm L/R) | 1.76 | x = +/-0.37 from the centre line |
| Elbow (foreArm) | 1.42 | 0.34 below the shoulder |
| Wrist (hand) | 1.10 | 0.32 below the elbow |
| Hip (thigh L/R) | 0.99 | x = +/-0.17 |
| Knee (shin) | 0.57 | 0.42 below the hip and **0.13 forward** |
| Ankle (foot) | 0.14 | 0.43 below the knee and **0.15 back** (so the foot sits 0.02 behind the hip) |

Body block sizes (X x Y x Z, metres):

| Part | Size | Centre height |
| --- | --- | --- |
| Pelvis | 0.46 x 0.20 x 0.32, top cap 0.36 x 0.08 x 0.34 | 1.05 |
| Waist rings (3) | 0.34 / 0.32 / 0.30 wide x 0.085 x 0.26 | 1.21, 1.31, 1.41 |
| Chest | 0.62 x 0.44 x 0.40 | 1.63 |
| Chest front plate | 0.50 x 0.22 x 0.06, on the front face | 1.69 |
| Back pack | 0.42 x 0.38 x 0.18, 0.27 behind the chest centre | 1.63 |
| Head (helmet) | 0.27 x 0.22 x 0.27 | 1.99 |
| Visor slot | 0.24 x 0.07 x 0.04, front of the helmet | 2.00 |
| Eye bar | 0.17 x 0.022 x 0.02 | 2.00 |
| Thigh | 0.17 x 0.40 x 0.20, tilted forward 17 degrees | 0.8 |
| Shin | 0.13 x 0.42 x 0.13, tilted 19 degrees | 0.35 |
| Foot | 0.18 x 0.06 x 0.26 plus two 0.05 x 0.04 x 0.12 toes and a 0.05 x 0.04 x 0.10 heel spur | 0.05 |
| Upper arm | tapered limb r 0.065 to 0.055, 0.32 long, shoulder plate 0.22 x 0.14 x 0.26 | 1.76 down to 1.44 |
| Left forearm | 0.13 x 0.30 x 0.13 + claw: palm 0.10 x 0.06 x 0.08, 3 fingers 0.025 x 0.11 x 0.025 | 1.42 down to 1.10 |
| Right forearm (weapon pod) | 0.16 x 0.36 x 0.17 + 2 barrels r 0.022, 0.34 long + drum r 0.075, 0.1 long | 1.42 down to 0.79 |

### 3.3 Parts list by group

Counts are from the code; the whole robot is 3,216 triangles in 2 material slots.

| Group | Parts |
| --- | --- |
| Pelvis | 1 rounded box, 1 cap plate, 2 hip actuator cylinders (r 0.085, 0.1 long, on the sides) |
| Waist | 3 stacked rounded boxes |
| Chest | main plate, front plate, 2 vertical hazard bars (0.06 x 0.32 x 0.03), 1 horizontal hazard bar (0.32 x 0.05 x 0.03), vent grille of 5 slats (0.22 x 0.015 x 0.02), back pack, 3 emissive cell bars (0.06 x 0.24 x 0.02), exhaust stub (r 0.05, 0.18 tall), neck cylinder (r 0.07 / 0.09, 0.12 tall) |
| Head | helmet, visor slot, eye bar (emissive), roof plate (0.20 x 0.035 x 0.24, yellow), antenna (r 0.008, 0.26 tall) with a red tip sphere (r 0.018), scanner dot (r 0.016, separate small mesh) |
| Each arm (x2) | shoulder plate, yellow stripe on it, shoulder ball (r 0.08), upper-arm limb, piston housing (r 0.02, 0.18) and rod (r 0.011, 0.16), elbow cylinder (r 0.07, 0.12 wide) |
| Left forearm and hand | forearm block, palm, 3 claw fingers |
| Right forearm and hand | pod block, yellow strip, ammo drum, 2 barrels, barrel mount block |
| Each leg (x2) | hip ball (r 0.09), thigh armour, yellow front stripe (0.04 x 0.3 x 0.03), knee cylinder (r 0.08, 0.18 wide), dark shin core, front shin plate (0.14 x 0.2 x 0.06), knee piston housing (r 0.028, 0.24) on the thigh and rod (r 0.014, 0.28) on the shin, ankle cylinder (r 0.055, 0.14), foot plate, 2 toes, heel spur |

Sockets the Unity prefab needs: **MuzzlePoint** at the end of the right hand, 0.33 below the wrist pivot
(`handR`, local y -0.33), centred between the two barrels; **WeaponSocket** at the right hand.

### 3.4 Materials and colours

The robot is **one mesh with vertex colours** in two material slots. For Blender, use two or three
materials plus a shared texture instead of vertex colours.

| Slot / colour | Value | Where |
| --- | --- | --- |
| Body paint | white vertex colour x robotPaint texture: grey-blue base #3a3f44 to #4f565c, paint chips #8e9396, grime #23221f; metal 0.55, rough 0.6 | pelvis cap, waist (alternate rings), chest, head shell, shoulders, thighs, forearms, foot plates |
| Accent (hazard yellow-ochre) | #c89a24 | chest bars, head roof plate, shoulder stripes, thigh stripes, forearm strip |
| Joint (dark metal) | #232527 | pelvis, alternate waist rings, power pack, neck, shoulder balls, knee/ankle cylinders, shin core, toes, hand palm |
| Dark | #16181a | vent slats, visor slot, pistons, barrels |
| Steel | #9aa0a4 | hip actuators, upper-arm limbs, piston rods, exhaust stub, antenna |
| Emissive red | (5.0, 0.35, 0.15) | eye bar, antenna tip |
| Emissive cyan | (0.4, 2.6, 3.0) | the 3 power-cell bars on the back |
| Scanner dot | red-orange emissive, changes with state (amber (5, 2.4, 0.2) idle, red (6, 0.4, 0.2) in combat or on alert; the eye bar itself stays red) | small sphere, moves +/-0.07 across the visor |

### 3.5 Emissive and moving parts

Emissive: eye bar, antenna tip, 3 back cells, scanner dot. Everything else is lit normally.

Moving parts (each needs its own mesh with the pivot at the joint, the Agent Controller drives them):

| Part | Pivot | Range seen in the prototype animation |
| --- | --- | --- |
| Pelvis | root, 1.05 | bobs 0.03 to 0.05 while walking, twists +/-0.06 rad; falls 0.8 on death |
| Spine, chest | at their joints | lean forward 0.06 rad running; chest pitch to aim |
| Head | head bone | yaw +/-1.0 rad (look), pitch with aim |
| Upper arms | shoulder | swing +/-0.25 rad walking; **right arm raises to -1.5 rad** to aim the pod |
| Forearms | elbow | left bends to -1.1 rad when aiming; right straightens to -0.08 |
| Thighs | hip | swing +/-0.32 rad walking, 0.54 running |
| Shins | knee | bend up to 0.45 rad |
| Feet | ankle | counter-rotate 0.7 x (thigh + shin) so the sole stays level |
| Pistons | housing on the thigh/upper arm, rod on the shin/forearm | visually telescoping; static in the prototype (the rod simply follows its bone) |
| Scanner dot | head | slides side to side, 2.5 rad/s idle, 9 rad/s aiming |

Timings, gait phase and thresholds belong in `docs/reference/agent-controller.md`.

### 3.6 Detail level

Medium-high: 3,216 triangles, 16 bones, the most complex character in the game. The parts that read in
the screenshot at 5 to 10 m: the **bulky chest and shoulders**, the **reverse-knee legs**, the **glowing
red eye bar**, the **yellow accents** and the **back pack**. The small items (finger claws, antenna,
vent slats, pistons) read only up close and are the first to simplify.
