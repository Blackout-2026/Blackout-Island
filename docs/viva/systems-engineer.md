# Systems Engineer Viva

Role: Systems Engineer (player, weapons, interactables, mission flow, shared contracts, project settings).
Background for every number here: `docs/reference/systems-engineer.md` (prototype values in sections 1–11, physics in
section 12). All measurements come from `Blackout/Debug/Stress Test` (Y7) unless the row says otherwise.

This file covers the three topics step Y7 asks for. Each earlier step has its own notes in `docs/viva/systems-engineer/`:

| Step | Notes | What it delivered |
| --- | --- | --- |
| P0 | [systems-engineer-p0-setup.md](systems-engineer/systems-engineer-p0-setup.md) | Shared folders, tags, layers, collision matrix, `Blackout.Shared` assembly |
| Y1 | [systems-engineer-y1-shared-contracts.md](systems-engineer/systems-engineer-y1-shared-contracts.md) | Health and damage, event channels and their 7 assets, Marker and agent contracts |
| Y2 | [systems-engineer-y2-player.md](systems-engineer/systems-engineer-y2-player.md) | FPS controller, input, camera feel, footstep noise, Player prefab, checkpoint teleport |
| Y3 | [systems-engineer-y3-weapons.md](systems-engineer/systems-engineer-y3-weapons.md) | SMG, rifle and knife, weapon controller, pooled effects |
| Y4 | [systems-engineer-y4-respawn-hud.md](systems-engineer/systems-engineer-y4-respawn-hud.md) | Death, respawn, checkpoints, fall damage, HUD |
| Y5 | [systems-engineer-y5-interactables.md](systems-engineer/systems-engineer-y5-interactables.md) | Doors, generator, cure, health pickups, crate, Interactables scene |
| Y6 | [systems-engineer-y6-mission-flow.md](systems-engineer/systems-engineer-y6-mission-flow.md) | Mission state machine, director, Bootstrap scene, Mission Complete screen, debug jumps |
| Y7 | this file, section 3 | Layer audit, weapon masks, Stress Test, physics numbers |

---

## 1. Event channel decoupling

### Decision

Systems that belong to different people talk through **ScriptableObject event channels** in
`Assets/ScriptableObjects/Events`:

- `VoidEventChannel`: GeneratorDisabled, CureTaken, PlayerDied, PlayerRespawned and MissionComplete.
- `NoiseEventChannel` (`Noise`): carries `NoiseEvent { position, loudness, type, source }`.
- `StringEventChannel` (`ObjectiveChanged`).

A channel is an asset with a C# `event` and a `Raise()` method (`Blackout.Shared.EventChannel<T>`, 20 lines). The
sender and the listeners reference the asset, never each other.

### Why

- **Additive scenes.** The game runs from `Bootstrap.unity`, which loads Environment (World Builder), Interactables
  (mine) and Agents (Agent Controller) additively. Unity cannot save a reference from an object in one scene to an
  object in another, but every scene and prefab can reference the same asset. A channel is the only Inspector-wired
  link that works across the three scenes.
- **Ownership.** Each role edits only its own scene and scripts (playbook section 2). With channels, the generator
  doesn't know who reacts to it, so adding a listener never means editing someone else's file.
- **Proof that it works.** One `GeneratorDisabled.Raise()` from `GeneratorInteractable` reaches **5 listeners owned by
  3 roles**. None of them references the generator:
  - the lab door's maglock (`SlidingDoor.unlockOn`)
  - `MissionDirector` (moves to the Lab phase)
  - `BlackboardHolder` (Agent Controller)
  - `PowerStateLighting`, the blackout (World Builder)
  - the `DebugEventLogger` listener placed in Y5
- **GUID stability.** The 7 channel assets were created once, in Y1. Everyone pulls the same GUIDs, so no two branches
  ever make two "GeneratorDisabled" assets (playbook risk 1).
- **Testable.** `EventChannelTests` (6) raise and subscribe without a scene.

### Alternatives rejected

| Alternative | Why not |
| --- | --- |
| Direct references (`[SerializeField] GeneratorInteractable`) | Cannot cross scenes; couples every listener to my class |
| Static event bus / singleton `GameEvents` | Hidden dependencies that the Inspector can't show; static state survives between tests and Play sessions unless it is reset by hand |
| `SendMessage` / `BroadcastMessage` | Matched by string and slow; needs a shared GameObject hierarchy |
| `UnityEvent` on the sender | The listener must be dragged onto the sender, so the same cross-scene problem |

### Weaknesses and how they are handled

- **No memory.** A listener that subscribes after `Raise()` misses the event. The MissionDirector therefore keeps the
  generator and cure state itself, and the Stress Test's jumps go through the real objects so every listener hears them.
- **Listener order is not defined.** No listener depends on another having run first.
- **Hard to trace.** `GameEventListener` + `DebugEventLogger` print a line when a channel fires.
- **Assets outlive scenes in the Editor.** Every subscriber in my code removes itself in `OnDisable`.
- **Within one prefab** (weapons → HUD, the player's health → HUD), plain C# events are used instead
  (`WeaponController.HitConfirmed`, `WeaponChanged`). Both ends are mine and on the same prefab, so an asset would only
  add a step.

---

## 2. Weapon system design

### Shape

```
WeaponController (on WeaponHolder, under the camera): input, switching, shared cooldown, recoil, holder animation
 ├─ WeaponContext: aim, owner, hit mask, noise channel, effect pools, how the player moves; reports hits
 ├─ IWeapon: HitscanWeapon (SMG, rifle)   ─┐  only "what a shot does"
 ├─ IWeapon: MeleeWeapon (knife)          ─┘
 └─ WeaponData (ScriptableObject): SMG.asset, Rifle.asset, Knife.asset: every number
Pure C# (EditMode-tested): WeaponMath, WeaponSwitcher, WeaponCooldown, RecoilSpring
Pooled effects: TracerPool (24 LineRenderers), ImpactEffects (one ParticleSystem, Emit), MuzzleFlash
```

- **Data, not code, per weapon.** The prototype's numbers live in the three assets:

  | Weapon | Damage | Rate | Range | Other |
  | --- | --- | --- | --- | --- |
  | SMG | 15 | 720 rpm | 90 m | falloff 10–32 m down to ×0.45; spread 1.38° + 1.15° moving + bloom up to 1.26° |
  | Rifle | 60 | 52 rpm | 320 m | no spread, no falloff |
  | Knife | 40 | 125 swings/min (0.48 s) | 2.3 m | 51.7° cone; lands 0.11 s after the swing |

  Tuning a weapon is an asset edit; adding a hitscan weapon is a new asset plus a viewmodel.
- **One controller, small weapons.** Input, the 0.4 s switch, the cooldown and the sprint rule are written once. A
  weapon class only answers `Fire()` and `Tick()`. `IWeapon` is an interface rather than a base class, so the knife
  and the guns share no code they don't need.
- **Hitscan, as in the prototype** (`Combat.hitscan`). A ray costs well under a microsecond (section 3). Rigidbody
  bullets would add simulated bodies, continuous collision detection and hundreds of contacts for no gameplay gain.
- **Testable maths.**
  - 14 `WeaponMathTests`: spread, bloom, falloff, the cone.
  - 7 `WeaponTimingTests`: cooldown, switching.
  - 15 `WeaponMaskTests` (Y7): real raycasts against primitives on each layer.
- **Decoupled outputs.**
  - Gunshot and knife noise go to the `Noise` channel, for the Hunter's hearing (H2).
  - Hits raise `HitConfirmed(killed)`, which drives the HUD's hit marker and the mission's kill count.
  - Damage goes through the shared `IDamageable`, so the weapons never know what an agent is.
- **No per-frame allocation.** Measured by the Stress Test: **0 B allocated per shot over 275 shots**, counting
  everything that runs inside the firing call, including the HUD and mission listeners. The knife uses
  `OverlapSphereNonAlloc` with a 16-collider buffer, and the tracers and sparks are pooled.

### Alternatives rejected

- **One MonoBehaviour per weapon that reads input itself.** That copies the switching, cooldown and sprint rules into
  every weapon, and two weapons can fire on the same click.
- **An inheritance tree (`Weapon` → `Gun` → `SMG`).** The differences between the weapons are numbers, which belong in
  data, not subclasses.
- **Projectile bullets.** Not in the prototype; more physics cost (see above).

### Y7 change: layers

The weapons used to hit "everything except Player, Ignore Raycast and Ragdoll". Now they list what they hit,
`PhysicsLayers.PlayerShots` = Default, Level, Enemy, Interactable and Hit. UI, Water, TransparentFX and the 20 unnamed
layers can't stop a bullet any more, whatever a teammate later puts on them. Every pellet reports its ray and hit
through `PelletTrace` (a struct, only when someone listens), which is how the Stress Test proves there are no missed
hits.

---

## 3. Physics optimisation choices, with numbers

### Measured scene (Bootstrap with Environment, Interactables and Agents loaded)

| Measure | Value |
| --- | --- |
| Colliders | **361**. Before the Y7 layer changes: Default 337, Interactable 15, Enemy 8, Player 1. After: **Level 319**, Ignore Raycast 18 (dock railings), Interactable 15, Enemy 8, Player 1, **Default 0** |
| Triggers | 9 (pickups, cure, doorway, generator console) |
| Mesh colliders | **0**: everything is a box, capsule or the terrain |
| Rigidbodies | **1 dynamic** (the crate) + **4 kinematic** (the doors) |
| Characters | the player is a `CharacterController`, and the agents use NavMesh paths with a capsule. Neither is simulated |
| Matrix | **21 of 66** pairs between the 11 named layers are off (11 from P0, then 10 more when Hit stopped colliding) |
| Fixed timestep | 0.02 s (50 Hz), 6 solver iterations / 1 velocity iteration |

### Choices

1. **Almost nothing is simulated.** With 1 dynamic body, the physics step has almost nothing to solve. Movement uses
   the `CharacterController` and NavMesh, as the prototype did with its own collision code. The 50 Hz step and 6
   solver iterations are kept: no stacked dynamic bodies need more, and fewer iterations would save nothing measurable.
2. **Primitive colliders only.** 0 mesh colliders: box and capsule tests are the cheapest PhysX shapes, and nothing in
   the level needs concave collision.
3. **The collision matrix removes pairs that should never meet.**
   - Enemy × Enemy is off: agents never push each other, and the NavMesh avoidance spaces them instead.
   - Ragdoll only touches Level: a falling body never pushes the player, a door or another ragdoll.
   - Hit touches nothing (Y7): hitboxes are for rays only, so they never push or get pushed.
   That is 21 of 66 pairs the broadphase never reports. The matrix is pinned by 29 EditMode tests
   (`PhysicsLayersTests`).
4. **Queries filter by layer and ignore triggers.** Every weapon query passes `PlayerShots` and
   `QueryTriggerInteraction.Ignore`. **Measured:** 28,000 rays per mask from all 7 phase starts:

   | Run | PlayerShots | All layers | Rays that hit |
   | --- | --- | --- | --- |
   | Batch PlayMode test | 0.679 µs | 0.685 µs | 24,642 of 28,000 (both) |
   | Editor, Play mode | 0.959 µs | 0.937 µs | 24,638 of 28,000 (both) |
   | Batch PlayMode test, repeated | 0.686 µs | 0.691 µs | 24,642 of 28,000 (both) |
   | Batch, after the level moved to Level | 0.686 µs | 0.695 µs | 24,642 of 28,000 (both) |

   **There is no speed difference** (under 3 % in all four runs, and the sign flips between runs). The excluded
   layers hold no colliders today, so PhysX has nothing extra to skip. The mask is a **correctness** choice: anything
   later placed on UI, Water or a new layer can't eat bullets. I am not claiming a speedup.
5. **No allocation in queries or effects.** `OverlapSphereNonAlloc`, pooled tracers, `ParticleSystem.Emit` sparks, a
   struct `PelletTrace`: **0 B per shot** (above). Reuse Collision Callbacks is on, so collision callbacks don't
   allocate either.
6. **Auto Sync Transforms stays off** (the Unity 6 default). Code that moves a collider and queries in the same frame
   calls `Physics.SyncTransforms()` itself, as the Stress Test does after placing its dummy. Otherwise every
   `Transform` change would force a physics sync before each query.

### Correctness results (Stress Test)

| Check | Batch PlayMode test | Editor, Play mode | Batch, after the layer changes |
| --- | --- | --- | --- |
| Phases visited, then Complete | 7, Complete reached | 7, Complete reached | 7, Complete reached |
| Null references / exceptions / errors | 0 / 0 / 0 | 0 / 0 / 0 | 0 / 0 / 0 |
| Pellets: dummy / wide / blocked / own collider / **missed** | 245: 214 / 31 / 0 / 0 / **0** | 245: 201 / 44 / 0 / 0 / **0** | 245: 213 / 32 / 0 / 0 / **0** |
| Pellets stopped per layer | Enemy 209, Default 21, Interactable 9, Hit 5 | Enemy 200, Default 28, Interactable 11, Hit 3 | Enemy 208, Level 11, Interactable 18, Hit 5 |
| Knife swings landed | 35 / 35 | 35 / 35 | 35 / 35 |
| Kills counted by the mission | 14 | 14 | 14 |
| Allocation per shot | 0 B (275 shots) | 0 B (275 shots) | 0 B (275 shots) |

### Findings, reported to the team and then changed with their agreement

The layers and the matrix are shared, so the team heard about these before anything changed (playbook Y7). The
World Builder and the Agent Controller then agreed to the changes in their parts.

1. **Level was empty.** All Environment colliders were on Default.
   - A ragdoll would have fallen through the world, because Ragdoll only collides with Level.
   - The Tactical Guard's sight check counts only Level as blocking, so **guards saw through every wall**.
   - *Now:* 319 colliders are on Level and **0 on Default**. The Stress Test fails if one is left on Default.
2. **Hit collided with everything**, so a head hitbox would have pushed the player and the doors. *Now:* it collides
   with nothing and stays hittable by rays.
3. **`AgentShooter.hitMask` was everything**, so agents could shoot each other, and the prototype has no friendly
   fire. *Now:* `PhysicsLayers.AgentShots`.
4. **The dock railings stopped bullets**, while in the prototype they don't (`bullets: false`). *Now:* the 18 railings
   are on Ignore Raycast. The player still bumps into them, while bullets and guards' eyes pass through.

`Blackout/Systems/Assign Physics Layers` makes the scene and prefab changes. It is re-runnable: the second run
changed nothing, and it only touches objects still on Default. Re-run it after a World Builder tool rebuilds part of
the level.

### Weaknesses

- All timing runs are from one Mac (batch mode and the Editor). They show "no difference", not
  absolute budgets.
- The Stress Test fires from fixed starts at a dummy straight ahead. It doesn't cover moving targets or agents
  shooting back (the player is invulnerable during the run).
- The physics step cost itself wasn't profiled, because there is almost nothing to simulate (1 dynamic body). The
  Profiler's Physics module is the next tool if ragdolls arrive.
