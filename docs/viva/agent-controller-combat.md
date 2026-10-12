# Agent steering and combat (A4) — design notes

Owner: Agent Controller. Covers separation steering, stuck detection (both in `PathFollower`), and
`AgentShooter`/`ShotMath`/`AgentWeaponConfig`/`AgentTracerPool`. Numbers cite
`docs/reference/agent-controller.md` sections 3.2 and 5.1 (read from the prototype's source in A0) and
`docs/reference/core-developer.md` where noted, unless marked **[GUESS]** or flagged as a simplification.

## What it does

- **Separation.** `MotionMath.SeparationPush` is added straight into `PathFollower`'s desired velocity
  every tick, from every other living agent (1.3 m, strength 2.4) and the player (1.0 m, strength 3),
  so agents and the player never stack on top of each other — even while standing still.
- **Stuck detection.** Every 1.4 s, if an agent wanting to move faster than 0.4 m/s covered less than
  0.35 m since the last check, it's stuck: the path clears, a replan is forced immediately, and it gets
  a random ±1.25 m/s nudge. Resets the instant it stops trying to move, so a clean stop or arrival is
  never mistaken for being stuck.
- **`AgentShooter`.** The shared executor every agent type fires through (`AgentIntent.fire`): gates a
  shot on reaction delay, facing (`forward · direction ≥ 0.85`) and range, then resolves it with
  `ShotMath`'s aim error and a Rayleigh-style perturbation, a hitscan raycast, damage via `IDamageable`,
  a Gunshot `NoiseEvent`, and an optional pooled tracer.

## Why this design, against the alternatives

- **Separation lives inside `PathFollower`**, not a separate steering component, because the prototype's
  own `agents/movement.js` treats it the same way: one more contribution to the desired velocity before
  acceleration-limiting, not a distinct system layered on top.
- **`ISeparationSource` instead of a direct `AgentRegistry` reference**: keeps `PathFollower` testable
  without a live registry (every existing test still runs with it `null`) and keeps the mover from caring
  *how* nearby agents are found. The real adapter (`AgentRegistrySeparationSource`) walks the registry's
  list by index rather than `foreach`, since boxing an enumerator over an interface-typed list would
  allocate every frame for every agent — the master prompt's hot-path rule.
- **Stuck detection triggers a replan, not a destination change**: "stuck" means the *route* failed, not
  that the *goal* changed. Clearing only the path (not `requestedDestination`) and forcing
  `PathFollower.Plan` to run again on the next opportunity matches the prototype's own "clear the path,
  set repath timer to 0" wording exactly.
- **`AgentShooter` never sees the player directly** — only `AgentIntent.lookTarget` and a `Physics.Raycast`
  along it. This is deliberate: the brain's own perception already decided *whether* to fire (`canSee`
  is the brain's job); the shooter only ever decides *can a shot land right now* from the intent alone.
  The cost of that decoupling is two dropped terms in the aim-error formula (next section).
- **A small custom `AgentTracerPool`** rather than reusing the Systems Engineer's `TracerPool`
  (`Assets/Scripts/PlayerAndInteraction/Weapons/Effects/TracerPool.cs`): reusing it would make
  `Blackout.Agents` depend on `Blackout.PlayerAndInteraction`, backwards from how the two roles' systems
  are meant to relate, and it isn't mine to extend. A few dozen lines of duplication is the cheaper
  trade against that coupling.

## Door awareness: deferred, not guessed at

A4's own brief asks for "door awareness (agents trigger doors on their route via the Door interface
owned by the Systems Engineer)". That interface does not exist in `develop` yet — Y5 (Interactables),
which is where it would be built, has not merged (checked at the start of this session: no `Door`
class/interface anywhere in the tree, only `DoorSocket` markers and `NoiseType.Door` referencing doors
that don't exist as objects yet). Rather than invent the Systems Engineer's contract shape myself, A4
ships separation, stuck handling and the shooter, and door awareness is picked up as a small follow-up
once Y5 lands. The prototype's own rule for it (`agent-controller.md` 3.2): if moving faster than 0.3 m/s
and a closed, unlocked door is within 2.4 m and ahead (dot product > 0), call `door.RequestOpen(agentName)`
— whatever the real method ends up being named.

## Known simplifications (for the viva)

- **`ShotMath.AimError` drops two of the prototype's four multiplicative terms**: the player's own
  horizontal speed and crouch state. The full formula is
  `spread * (1 + dist/28) * (1 + playerHorizSpeed/4.5 * 0.9) * (engaged < 1.2s ? 1.6 : 1) * (crouched ? 1.15 : 1)`;
  this ports everything except the player-speed and crouch factors, because `AgentShooter` has no access
  to the player's movement state without being handed a reference to the player specifically, which the
  intent-only design deliberately avoids. The two kept terms (distance, early-engagement) are the ones
  that most visibly affect how "panicked" an opening shot looks, which is the part that reads in the demo.
- **The `aimBlend ≥ 0.8` gate is dropped.** In the prototype this ties firing to the *animator's* damped
  aim-pose blend, coupling the shooter to animation state the architecture otherwise keeps separate
  (brains → intent → mover / animator / shooter, never animator → shooter). `reactionDelay` alone serves
  the same narrative purpose — a beat before the first shot — without that coupling.
- **The alert-buff hook exists but nothing sets it yet.** `AgentShooter.Heightened` multiplies aim error
  by ×0.9 when true (`agent-controller.md` section 8), mirroring `PathFollower.SpeedMultiplier`'s pattern
  for the ×1.25 speed buff — but no buff system exists yet to set either property outside a unit test.
  Whoever wires the Commander's alert broadcast (G2) should set both.
- **Separation has no vertical gate.** The prototype limits agent-agent separation to pairs within 1.5 m
  vertically as well as 1.3 m horizontally; `PathFollower` is a flat (X/Z) mover with height handled
  separately via NavMesh snapping, so this port only checks the flat distance. In practice agents on
  different floors are usually also far apart horizontally, so this rarely matters, but it is a real gap
  if two floors ever stack directly above each other.

## Numbers and their sources

| Value | Source |
| --- | --- |
| Separation ranges/strengths (1.3/2.4 agent, 1.0/3 player) | `agent-controller.md` 3.2 |
| Stuck check interval 1.4 s, min progress 0.35 m, trying-speed threshold 0.4 m/s, nudge 1.25 m/s | `agent-controller.md` 3.2 |
| `AgentWeaponConfig` defaults (damage 7, burst 3/0.11/0.9, spread 0.035, range 30, reaction 0.55, noise 40) | `agent-controller.md` 5 ("Defaults for any weapon field not given") |
| Facing threshold 0.85, aim-error formula, Rayleigh perturbation `err·√(−2 ln u)·0.7` | `agent-controller.md` 5.1 |
| Muzzle forward offset 0.15 m | `agent-controller.md` 5.1 |
