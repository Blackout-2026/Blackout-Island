# Blackout Island: The Cure — browser prototype

A playable vertical slice of the game, built in plain HTML + WebGL (three.js r128) so the team
can agree on look, pacing and mechanics before building it in Unity 6.3 LTS. It is also a
reference for SE3032 (Graphics & Visualization) and SE3062 (Intelligent Systems).

![Generator room before the blackout](screenshots/06_generator_before_blackout.png)
![Generator room after the blackout](screenshots/11_generator_after_blackout.png)

---

## 1. How to run

1. Double-click `prototype/index.html`. It runs straight from `file://`: no server, no build
   step, no internet. Everything it needs is in `lib/` and `src/`.
2. Use **Chrome or Edge** (that is what it was tested on). Wait for the loading bar, click
   **CLICK TO START**, then play. Headphones help, because sound is positional.
3. Loading takes **about 14 s on Medium** on an Intel Iris Xe laptop (Low about 9 s, High
   about 32 s). Later launches are a little quicker (about 12 s on Medium), because Chrome
   caches the compiled shaders. The rest of the time goes on generating textures, shaping the
   terrain and a warm-up pass that prevents freezes during play.

URL options (append to the address): `?quality=low|medium|high` forces a quality level, and
`?debug` turns three.js shader error checking back on.

### Controls

| Key | Action | Key | Action |
| --- | --- | --- | --- |
| W A S D | Move | Mouse | Look |
| Shift | Sprint (loud) | C (or Ctrl) | Crouch toggle (quiet, harder to see) |
| Space | Jump | F | Flashlight (casts shadows) |
| E | Interact. Hold E for 2 s on the generator | 1 / 2 / 3, wheel | SMG / sniper / knife |
| Left mouse | Fire / slash | Right mouse | Sniper scope |
| Esc | Pause and settings (sensitivity, quality, volume) | F1 or `` ` `` | AI debug overlay |

### The mission

Beach → forest trail → fenced compound → open the bunker door → ramp down to level B1 →
security office → corridor (there is a pushable crate) → generator room (hold **E** at the
console) → **blackout** → the laboratory maglock releases → walk into the glass chamber to
take the cure → **SECURITY ALERT**, everyone converges → fight back out to the boat →
**Mission Complete** (time, kills, deaths, Restart).

If you die there is no game-over screen. You respawn at the start of the current phase with
full health and 1.5 s of invulnerability, and the generator and cure state are kept.

---

## 2. Architecture

- **No modules, no bundler.** Every file is a classic `<script>` that adds to one global
  namespace, `BI`. `index.html` loads them in dependency order. The libraries are vendored
  in `lib/`: three.js r128 core, plus EffectComposer, RenderPass, ShaderPass,
  UnrealBloomPass, CopyShader, LuminosityHighPassShader and FXAAShader. All were downloaded
  from `cdn.jsdelivr.net/npm/three@0.128.0/` and checked for HTTP 200.
- **Event bus** (`core/eventbus.js`). Systems publish and subscribe to named channels and
  never call each other directly. This is the same pattern as Unity ScriptableObject event
  channels. For example, `generator:disabled` is published once, and lighting, fog,
  materials, audio, doors, AI tuning, mission and HUD each react on their own.
- **AI is split into two layers.** The four decision architectures only write an `intent`
  (`move`, `speed`, `look`, `aim`, `fire`, `crouch`). One shared **AgentController**
  (`agents/movement.js`) turns that intent into A* paths, steering, collision, door
  opening and procedural animation.
- **Data, not code.** Weapons are one table (`BI.WEAPONS`), utility weights are one table
  (`TacticalGuard.WEIGHTS`), GOAP actions are one table (`Commander.ACTIONS`), and the whole
  level layout is in `world/level.js`.

### The event chain

```
hold E 2 s at console ─▶ generator:disabled
   ├─ generator model: core flickers out, flywheel spins down, gauges drop, spark shower + smoke
   ├─ bunker_interior: fluorescent tubes die in a cascade outward from the generator
   │                   (strobe → spark → pop each), then lights:emergency
   │                   ├─ red emergency lamps fade in, two alarm beacons sweep red beams
   │                   └─ materials: interior reflections swap to the red environment
   ├─ lighting:  fog thickens and turns red-black, ambient drops (blackout blend)
   ├─ audio:     generator hum pitches down to silence, alarm siren starts
   ├─ doors:     the lab maglock fails safe and opens (+ HUD toast)
   ├─ agents:    Security Patrols and Tactical Guards become heightened (+25% speed, +40% vision)
   └─ mission/HUD: objective → "Take the cure"

walk into the chamber ─▶ cure:taken
   └─ Commander (lab sensors) sets cureStolen on the blackboard → GOAP plans
      BroadcastAlert → OrderConverge
         ├─ alert:broadcast → HUD "SECURITY ALERT" banner, radio sting, all agents heightened
         └─ a converge directive on the blackboard for every living agent
            → FSM: Pursue · BT: FollowOrders · Utility: guard point moves to the target
```

### Rendering pipeline (Medium/High)

Scene (half-float HDR, linear) → viewmodel pass (separate scene, depth cleared, so the gun
never clips through walls) → UnrealBloom (reduced resolution) → our composite pass
(exposure, **ACES filmic**, sRGB, vignette, film grain, damage and low-health overlay,
slight chromatic split when hurt) → FXAA.

three.js r128 applies `renderer.toneMapping` inside every material even when rendering to a
render target. So in this pipeline the renderer's tone mapping is off, and ACES is applied
once, after bloom. Low quality skips the composer and uses three's built-in ACES and sRGB.

Lighting uses a **virtual light pool** (`world/lighting.js`). Every lamp in the world is a
virtual light. Each frame, the most important ones near the camera are assigned to a fixed
pool of real PointLights and SpotLights: 3 / 4 / 8 point lights on Low / Medium / High,
with fades so nothing pops. Lamp meshes are emissive, so distant lamps still glow through
bloom. This is how the bunker has about 40 fixtures while the shader evaluates only a handful
of lights. It is the browser version of URP's per-object light limit.

---

## 3. Files and team roles

The roles follow the team table in the root README.

| Team member | Graphics role (SE3032) | Files |
| --- | --- | --- |
| Student 1 | **World Builder** | `world/level.js`, `world/terrain.js`, `world/sky.js`, `world/ocean.js`, `world/forest.js`, `world/beach.js`, `world/bunker_exterior.js`, `world/bunker_interior.js`, `world/lighting.js`, `world/world.js`, `world/effects.js`, `materials/textures.js`, `materials/materials.js`, `models/generator.js` (hero model 1) |
| Student 2 | **Systems Engineer** | `physics/collision.js`, `physics/doors.js`, `physics/crate.js`, `physics/interactables.js`, `audio/audio.js` |
| Student 3 | **Core Developer** | `core/namespace.js`, `core/eventbus.js`, `core/input.js`, `core/engine.js`, `core/geometry.js`, `player/player.js`, `weapons/weapon_system.js`, `models/weapons.js`, `ui/hud.js`, `ui/menus.js`, `ui/style.css`, `game/mission.js`, `main.js` |
| Student 4 | **Agent Controller** | `nav/navgrid.js`, `nav/astar.js`, `agents/movement.js`, `agents/agent_base.js`, `agents/perception.js`, `agents/blackboard.js`, `agents/agent_manager.js`, `models/humanoid.js`, `models/robot.js` (hero model 2), `ui/debug_overlay.js` |

| Team member | IS agent (SE3062) | Architecture | File |
| --- | --- | --- | --- |
| Student 1 | Security Patrol ×3 | Finite State Machine | `agents/security_patrol_fsm.js` |
| Student 2 | Hunter ×2 | Behavior Tree + sound propagation | `agents/hunter_bt.js` (+ `agents/perception.js` noise model) |
| Student 3 | Tactical Guard ×2 (robot) | Utility AI | `agents/tactical_guard_utility.js` |
| Student 4 | Commander ×1 | GOAP + shared blackboard | `agents/commander_goap.js` (+ `agents/blackboard.js`) |

Every file starts with a header comment that explains what it does and which role owns it.
The four AI files also contain a diagram or table of their architecture, for the viva.

### The four agents in one paragraph each

- **Security Patrol (FSM).** States: Patrol, Investigate, Pursue, Attack, Search. Vision is
  a cone plus a range plus a line-of-sight raycast against walls, and it fills an awareness
  meter rather than detecting instantly. Crouching halves visibility; sprinting and the
  flashlight raise it. The patrol searches random walkable points around the last known
  position and gives up when a timer runs out. Commander directives are just more
  transitions.
- **Hunter (Behavior Tree).** A reactive root selector with these branches: Engage, Hunt
  last seen, React to gunfire, React to footsteps, Follow orders, Prowl. Hearing is its
  speciality (×1.7). **Gunfire** makes it run to a flank point 12 m to the side of the shot,
  or dive behind a tree if the shot was close. **Footsteps** make it crouch, sneak silently
  to the sound and wait in ambush. It wears a woodland-camo ghillie suit and prowls the
  forest.
- **Tactical Guard (Utility AI).** Scores Attack, MoveToCover, Reposition and HoldPost every
  tick (10 Hz) from normalised factors: distance, visibility, threat, health, cover quality
  and distance from its post, with an inertia bonus so it doesn't flip-flop. The scores and
  factors appear live in the F1 overlay. Directives move its guarded point. Its body is the
  bipedal security robot.
- **Commander (GOAP).** Builds a world state from the blackboard (`cureStolen`,
  `alertActive`, `playerLocated`, `squadReady`, `playerNeutralized`). It picks the goal
  **PlayerLocated** or **PlayerNeutralized**, and A* searches over its actions
  `BroadcastAlert`, `OrderRegroup`, `OrderSearchZone` and `OrderConverge`, each with
  preconditions, effects and a cost. Example plans seen in testing:
  `BroadcastAlert → OrderConverge`, `BroadcastAlert → OrderRegroup → OrderConverge` (after
  losses), and a re-plan to `OrderSearchZone` when the player escapes. It has a weak sidearm
  and backs off when hurt.

**Sound propagation** (`agents/perception.js`, `nav/astar.js`). Every noise has a radius:
SMG 42 m, sniper 65 m, door 14–22 m, generator 60 m, sprint step 13 m, walk step 4.5 m,
crouched steps make no noise. For each listener, loudness is the larger of two values:

- a Dijkstra flood over the nav grid that goes around walls and through *open* doors only;
- a straight line damped by 0.35 per wall crossed.

So sound bends round corners, is muffled by concrete and is stopped by closed doors. In the
F1 overlay, noises appear as expanding rings.

---

## 4. Testing

Playwright is a dev tool installed inside `prototype/` only (`npm install`, then
`npx playwright install chromium`). The game itself does not need it. Every test opens the
`file://` URL in Chromium on the real GPU (ANGLE/D3D11) and records console errors and
failed requests.

| Command | What it checks | Last result |
| --- | --- | --- |
| `npm test` (`tests/smoke.js`) | Teleports through the whole mission with `window.__debug`: generator prompt, a 2 s hold, the event chain (cascade, emergency lights, blackout blend, fog, heightened agents, maglock), cure pickup, Commander alert, HUD banner, GOAP plan, a directive for every agent, every agent in search or pursue, escape phase, boat → Mission Complete screen, 0 console errors, 0 failed requests | **24/24 pass** |
| `npm run ai` (`tests/ai.js`) | Patrol route, detection → attack (player hurt), search after losing the player; Hunter sneaks toward footsteps, flanks distant gunfire, takes cover from close gunfire; closed door blocks sound, open door passes it; Guard Attack → MoveToCover when hurt; crate push moves the crate and updates the A* grid; death → respawn; weapon switch and zoom; knife backstab; Commander plans `OrderRegroup` | **18/18 pass** |
| `npm run soak` | Full mission with live AI and a mortal player, frame times per phase | 58–60 fps, 0 errors |
| `npm run perf` | Average and p95 frame time at 8 fixed views (AI frozen) | see below |
| `npm run shots` | Regenerates `screenshots/` | 13 images |

### Screenshots (`screenshots/`, 1280×720, Medium)

`01_beach`, `02_forest`, `03_bunker_entrance`, `04_security_office`, `05_corridor`,
`06_generator_before_blackout`, `07_hero_generator`, `08_hero_robot`, `09_enemy_fight`,
`10_debug_overlay`, `11_generator_after_blackout`, `12_corridor_after_blackout`, `13_lab`.

They are staged with debug hooks: teleports, frozen AI for the still shots, and in
`09_enemy_fight` the shot's muzzle flash and tracers are held on screen long enough to be
captured. All effects shown are the real in-game ones.

### Performance

Measured on an **Intel Iris Xe laptop GPU**, Chromium (ANGLE/D3D11), 1280×720.

| Quality | Outdoors (beach / forest / compound / entrance) | Bunker (security / corridor / generator / lab) |
| --- | --- | --- |
| Low | 58–60 fps | 60 fps |
| **Medium** | **57–60 fps** (p95 16.8 ms) | **60 fps** (p95 16.8 ms) |
| High | 39–48 fps | 59–60 fps |

The scene draws 75–235 draw calls; triangles are about 450–660k outdoors (including the
shadow pass) and about 20–45k in the bunker.

**Power matters.** The table comes from runs during the session. At the end of the session
the laptop was on **battery** (Windows "Balanced" plan, 64% charge), and the same Medium
test gave **23–38 fps outdoors**, 39–53 fps in the security office, corridor and generator
room, and 60 fps in the lab. The integrated GPU clocks down on battery. Plug in, or use Low,
when demoing on a laptop.

What was optimised, and why:

1. **Shader compile time** on Medium went from 11.7 s to 3.9 s cold (0.7 s once Chrome has
   cached the shaders):
   - Every MeshStandardMaterial gets the same texture slots (1×1 default textures where a
     material has none), so materials collapse onto a few shader programs.
   - Environment maps are small canvas CubeTextures instead of PMREM. Measured: about
     230 ms less compile time per program on ANGLE/D3D11.
   - The wind shader is shared, using uniforms instead of per-material constants.
   - three's synchronous shader-error check is off (turn it back on with `?debug`).
   - Compilation is chunked so the loading bar keeps moving.
2. **Hitches.** A warm-up pass during loading renders the real pipeline from 5 viewpoints
   with frustum culling off, the flashlight on and hidden effects shown. This makes ANGLE
   build every shader variant before you play, instead of stalling the first time something
   appears on screen.
3. **A Chrome D3D11 stall.** Re-showing the instanced light-fixture meshes after they had
   been hidden stalled Chrome's GPU process for 1–3 s every time. A trace showed one 3.5 s
   command-buffer flush. It does not happen with ANGLE's OpenGL or Vulkan backends. Those
   meshes are now never hidden; they cost 5 draw calls. To reproduce:
   `tests/d3d11_reveal_check.js`.
4. **Zone culling.** The island (terrain, ocean, forest, sky) is hidden while you are
   underground, and the bunker rooms are hidden while you are outside.
5. **Static batching.** Walls, props and pipes are merged per material per room. Characters
   are one SkinnedMesh each (one draw call; the robot uses two).
6. **Light pool.** 4 point lights and 2 spot lights on Medium, as described above.
7. **Geometry trims.** Bloom runs at 35% resolution and Medium renders at 0.85 resolution
   scale. Pine foliage was halved, bushes cast no shadows, and the ocean grid was reduced
   to 150². Normal and roughness maps are at half resolution and decals at 256 px. The moon
   shadow map updates every other frame on Medium.

---

## 5. Decisions made where the spec was silent

- **Layout and scale.** North is −Z. The beach is a curved bay in the south. The forest
  trail is about 140 m long. The compound sits on a plateau (y = 10) against a cliff. The
  bunker floor is at **y = 2.5, above sea level**, so the animated ocean plane can never
  cut through underground rooms.
- **Getting underground.** A 22° ramp, not stairs, leads down; it is easier to make solid
  for player and AI. Real **stairs** (ten 0.3 m steps) lead up to a catwalk in the generator
  room, where one health pickup rewards exploring.
- **Doors.** The bunker blast door and the security door open with E. The generator-room
  door is hinged. The **lab door is maglocked until the blackout**; the lock fails safe when
  power dies, so the generator must come first. Agents may open unlocked doors (they have
  clearance).
- **Escape checkpoints** move outward (generator room → bunker entrance → forest edge), so
  dying near the boat doesn't send you back underground.
- **Detection** uses an awareness meter, not instant spotting, so stealth (crouch,
  flashlight off) is meaningful. Being shot always reveals your position.
- **Combat extras.** Headshots (SMG ×1.6, sniper ×2.5, so a sniper headshot kills anything).
  Knife **backstab** ×2.5 on an unaware enemy (a silent takedown). Crouch is a toggle.
- **Commander.** Has a fourth action, `BroadcastAlert`, which turns "cure stolen" into
  "alert active". If the Commander is already dead, the lab's automated system raises the
  alert instead, so the event chain always fires.
- **Minimap.** Replaced by a **compass** with an objective marker and distance (the spec
  allowed either).
- **Rain.** Not added (it was optional).
- **Camera feeds.** Two security monitors show a **live camera feed** of the corridor on
  High only. On Low and Medium they show animated static and data screens.
- **Restart** reloads the page. That is the simplest way to reset every system.
- **Settings** (sensitivity, quality, volume) persist in `localStorage`. This fails safely
  if `file://` storage is blocked.
- **F1** also works as `` ` `` because some browsers reserve F1.

---

## 6. Approximations, known issues, not finished

Being honest about what is simplified or untested:

- **Crate.** Translation only. It pushes along the dominant axis, slides along walls and has
  friction, but does not rotate (a small visual tilt fakes the weight).
- **Hinged door.** Its collider is all-or-nothing: it blocks until about 35° open.
- **Agents** cannot use the generator-room stairs: the nav grid's step limit is 0.6 m per
  1 m cell. They don't need to.
- **Paths** are on a 1 m grid. Paths are string-pulled and separation steering prevents
  stacking, but agents can jostle briefly in doorways.
- **Hunter camouflage** is visual only; it does not change how the player is detected.
- **Shadows.** The moon casts shadows outdoors and the flashlight everywhere. Bunker lamps
  do not cast shadows; that would be too expensive.
- **High quality** does not hold 60 fps outdoors on an Iris Xe.
- **Not verified automatically:**
  - **Pointer lock and real mouse look.** Tests run with `allowUnlocked`, so the Esc → pause
    → resume flow is coded but only exercised in headless mode.
  - **Audio.** The WebAudio graph runs without errors in the tests, but nobody has listened
    to it in this pass.
  - **Difficulty.** It was tuned with numbers (reaction delays, spread, damage per burst)
    and the automated encounters, not with human play-testing. Expect to tweak
    `weapon: {...}` in the four agent constructors.
  - **Other browsers.** Only Chromium-based browsers were tested; Firefox and Safari were
    not.
- **Intermittent headless issue.** Early in development, two headless test runs lost the
  page context ("execution context destroyed") when teleporting between surface and
  bunker. No crash, navigation or WebGL context-loss event was reported, and it has not
  recurred since the warm-up and fixture changes. The cause is unknown.
- **Mid-game hitch.** If you change quality in the pause menu, shaders recompile, so expect
  one long frame.
