# Viva notes: shared contracts (Systems Engineer, step Y1)

PR #5. Commits 787cd24, 572f921, 84ee794, 0ba639d and 1e6c0a4, all in `Assets/Scripts/Shared` and
`Assets/ScriptableObjects/Events`.

## What I built

- **Damage:**
  - `DamageInfo`: a struct with amount, point, direction, source and `DamageType` (Generic, Bullet, Melee, Fall).
  - `IDamageable`: `TakeDamage(DamageInfo)` and `IsAlive`.
  - `Health`: a MonoBehaviour ported from the prototype's `player.js`. It has 100 HP, clamps at 0, ignores damage
    while dead or invulnerable, and counts the invulnerability timer down by `deltaTime` (1.5 s after a respawn).
    `OnDamaged` and `OnDied` are C# events, and `OnDied` fires once.
- **Event channels:**
  - `EventChannel<T>` (a ScriptableObject with a C# event and `Raise`).
  - `VoidEventChannel`, `NoiseEventChannel` (carries `NoiseEvent { position, loudness, type, source }`) and
    `StringEventChannel`.
  - `GameEventListener`, which turns a channel into a UnityEvent for Inspector wiring.
- **The 7 channel assets:** GeneratorDisabled, CureTaken, PlayerDied, PlayerRespawned, MissionComplete, Noise and
  ObjectiveChanged. `Blackout/Systems/Create Event Channels` creates them and never overwrites one.
- **Level and agent contracts** (headers name their owners):
  - `Marker` with `MarkerType` (World Builder).
  - `AgentIntent`, `IAgentBrain`, `IAgentContext`, `IAgentMover`, `IDebugInfo`, `Directive` and `Blackboard`
    (Agent Controller).
- **Tests:** 10 `HealthTests` and 6 `EventChannelTests`.

## Why this design

- **Contracts before features.** Every role builds against the same interfaces from week 1, so no one waits for
  another role's implementation, and merges don't break each other.
- **Channels as assets.** The scenes are loaded additively (Environment, Interactables, Agents). Every scene can
  reference an asset, but no scene can reference an object in another scene. The full argument is in
  `systems-engineer.md` section 1.
- **Assets made once, by the owner.** If two branches each made "GeneratorDisabled", there would be two GUIDs. The
  builder refuses to replace an existing asset.
- **`DamageInfo` is a struct.** No allocation per hit. It carries the source, so agents can react to who shot them.
- **Brains return an `AgentIntent` instead of moving the agent.** The FSM, behaviour tree, utility and GOAP brains
  then share one mover, one animator and one shooter.
- **`Health.Tick(dt)` is public.** EditMode tests can drive time without Play mode.

## Alternatives rejected

- **UnityEvents for damage.** They allocate when invoked with arguments, and can only be wired in the Inspector.
- **Each brain drives its own NavMeshAgent.** That means four copies of the movement code, which would drift apart.
- **A static event bus.** It hides who depends on whom, and its state leaks between tests.

## Numbers

- **Health:** 100 HP and 1.5 s invulnerability after a respawn (prototype `player.js`).
- **`NoiseEvent.loudness`** is the hearing radius in metres (the prototype's noise radius), so the Hunter can compare
  distance with loudness directly.
- **Tests:** EditMode went from 32 to 48 (16 new). The builder made 7 assets on its first run and 0 on its second.

## Known weaknesses

- No damage multipliers (headshots, backstabs) in the contract yet.
- A single `AgentIntent` per tick can't express a sequence, so the brain has to hold that state itself.
- A channel has no memory, so a listener that subscribes late misses the event (see `systems-engineer.md`
  section 1).
