# Hunter viva (Systems Engineer: behaviour tree + sound propagation)

The Hunter is the forest agent: two of them prowl the island, react to what they hear, and hunt the player. Code in
`Assets/Scripts/AI/Hunter` (assembly `Blackout.AI.Hunter`); the prototype's rules and every [GUESS] are in
`docs/reference/systems-engineer.md` sections 13–16; each step's notes are in `docs/viva/systems-engineer/`
(`-h1-behaviour-tree`, `-h2-sound-propagation`, `-h3-hunter-tree`).

```
Root ?
├─ Attack            [SeesPlayer] [InRange] AimAndFire
├─ Pursue            [SeesPlayer] CloseIn
├─ Investigate       [HeardPreyNoise] MoveToNoise → Ambush
├─ CautiousApproach  [HeardGunfire] ? ( [GunfireClose] TakeCover | AdvanceByCover )
├─ Search            [SawRecently] GoToLastSeen → LookAround
├─ FollowOrders      [HasOrder] MoveToOrder
└─ Roam              NextWaypoint → WalkTo → LookAround
```

`HunterBrain` senses ten times a second (eyes with camouflage, ears with walls), writes the facts into the tree's
blackboard, ticks the tree, and returns an `AgentIntent`. It never moves, animates or shoots: the Agent Controller's
mover, animator and `AgentShooter` do, including the 0.7 s reaction delay and the aim error.

---

## 1. Why a behaviour tree beats a flat FSM for this agent

**The Hunter's behaviour is a priority list, and it changes all the time.** At any moment the right thing to do is
the highest of: shoot, chase, react to a noise, search, obey the Commander, roam. Seeing the player must win over
everything; a gunshot must interrupt a stroll; a stroll must resume when nothing else is going on.

- **In a behaviour tree** that list *is* the root Selector. It is reactive: every tick it starts from the top, so a
  higher branch interrupts a running lower one, and the interrupted branch is reset (`Selector` in H1, as in the
  prototype). Seven branches, seven entries, in the order the brief gives.
- **In a flat FSM** each state needs an exit to every state that can interrupt it. Seven states have up to
  7 × 6 = **42** possible transitions, each with its own condition and clean-up; "a gunshot interrupts Roam, Search,
  FollowOrders and Investigate, but not Attack" has to be written into four states.
- **Adding behaviour is local.** The Commander's orders were one more branch in the list; in an FSM they would be a new
  state plus transitions into and out of every existing one.
- **Branches are made of reusable parts.** `Sequence` gives "check, then act" with guards that are re-checked every
  tick, so an action stops the moment its premise is false (the sneak stops when the player is seen). `Cooldown` and
  `Inverter` are decorators any branch can use.
- **It is testable piece by piece.** The tree reads only facts from its blackboard, so each branch is tested by writing
  the facts by hand (30 tests, no scene), and the H1 core has 75 tests of its own.
- **It is visible.** The debug text shows the running path, for example `Hunter [BT] CautiousApproach > GunfireTactic
  > AdvanceByCover`, read from each node's last status.

What an FSM does better: a fixed cycle of states with exact exit rules, such as my mission's phases
(`MissionStateMachine`, Y6) or a sliding door opening and closing. The Hunter's job is different: choosing among
competing urges every tick.

## 2. Why sound-based sensing rather than a detection radius

**A detection radius would ignore both the player's choices and the level.** With sound:

- **What the player does matters.** A walking step is heard to 4.5 m, a sprinting one to 13 m, a gunshot to 42 m
  (SMG) or 65 m (rifle). The same distance is safe or dangerous depending on how the player moves.
- **Walls matter.** Each wall keeps 35 %: a sprint 6.5 m away is perceived at **0.85** in the open, **0.30** through
  one wall, **0.10** through two, and not at all through three. On the real level, a sprint is heard through the
  closed D-Security door at 10 m (0.137), while an SMG shot from the generator room reaches the lab through two walls
  at 0.139.
- **What was heard decides the reaction.** Footsteps: walk there and wait in ambush (the prey has not noticed).
  Sprinting: run there (the prey is getting away). Gunfire: approach from cover to cover (it can shoot back). A
  radius cannot tell these apart.
- **It fits the Hunter's fiction** (hearing ×1.7, a camouflaged forest predator) and it is fair: the player can learn
  to move quietly.
- **It is cheap and decoupled.** Every system already publishes `NoiseEvent`s to one shared channel (Y1); the Hunter
  only listens. Far noises cost a distance check; the wall raycast runs only when a noise could be heard and could win.
  Hearing a noise allocates nothing.

Sight is still there for the fight itself, with an awareness meter rather than instant detection, and camouflage: a
player standing still fills it half as fast (tick 11 instead of tick 6 at 10 m).

## 3. The attenuation formula, with each constant justified

```
perceived = max(0, 1 − d / r) × wallFactor ^ walls × hearing     heard if perceived > threshold
current   = perceived × max(0, 1 − age / memorySeconds)
```

All values are in `HearingConfig` (asset in `Assets/Scripts/AI/Hunter/Config`), each with its reason in a tooltip.

| Constant | Value | Why this value |
| --- | --- | --- |
| `r` (noise radius) | per source: walk 4.5, sprint 13, jump 7, landing 9, door 14, crate 11, alarm 18, SMG 42, generator 60, rifle 65 m | the prototype's radius for each noise; set by the system that makes the noise, so the Hunter needs no table of its own |
| `1 − d / r` | linear falloff | 1 at the source, 0 at the radius: the radius means exactly "heard up to here in open space", which makes each source's number easy to tune |
| `hearing` | **1.7** | the Hunter's speciality in the prototype (Security Patrol 1.0, Tactical Guard and Commander 0.9). A noise beside it counts 1.7, so it hears further and through more walls than any other agent |
| `wallFactor` | **0.35** per wall | the prototype's value: one wall cuts a noise to about a third, two to an eighth; concrete muffles strongly but a loud noise still gets through |
| `walls` | counted by one raycast, at most **3** | from 1.2 m above the noise (a gun or a mouth, not the floor) to 1.6 m above the Hunter's feet (its ears). Level and Interactable colliders count (walls, closed doors); triggers and the terrain do not, so a hill is not a wall. A fourth wall would only matter within 0.45 r, so the cap saves raycast hits for nothing audible |
| `threshold` | **0.04** | the prototype's: the faintest audible noise is one at 98 % of its radius in the open. Through 1 / 2 / 3 walls a noise is heard to 93 % / 81 % / 45 % of its radius |
| `memorySeconds` | **8 s**, linear | how long the prototype keeps a heard noise. Linear reaches exactly 0 at 8 s, so "forgotten" needs no second rule; a new noise replaces the remembered one if it is at least as loud as what is left of it |
| tree thresholds | gunfire > **0.06** within 6 s; other noises > **0.04** within 5 s | the prototype's reaction rules: gunfire must be a little louder to pull the Hunter off its route |

The Hunter's constants and the formula are checked by tests (`HearingMathTests`, `HearingConfigTests`,
`HunterConfigTests`, `HearingLevelTests` on the real level). The radii belong to the systems that make the noises.

## 4. One known weakness: sound does not bend round corners

The wall count is a straight line. A footstep in the next room, heard through an open doorway that is not in line
with the Hunter, is muffled as if it came through the wall: one wall costs 65 %, so the Hunter may miss a sprint it
should have heard. The prototype also measured the path around walls (a flood over its nav grid) and kept the louder
of the two; the brief asks for the raycast model, and the flood would need a grid over the whole level.

**How it could be fixed:** for noises that fail the straight line, ask the NavMesh for the walking path from the noise
to the Hunter (`NavMesh.CalculatePath`) and use `1 − pathLength / r` when it is louder. That costs a path query per
muffled noise, so it would run only for noises within their radius.

Other limits are listed in the step notes: no crouch or sneak speed in `AgentIntent`, agents' own gunfire is silent
until `AgentBase`'s shooter has a noise channel, and cover is not checked for line of sight.

## Debugging it (H4)

- **Debug text** (`IDebugInfo`, published to the squad Blackboard every 0.5 s):

  ```
  Hunter [BT] Investigate > MoveToNoise
  eyes: sees no, detected no, awareness 0.00, never seen
  ears: Sprint 0.76 heard 0.10 s ago through 0 walls
  order: Converge (20.00, 30.00)
  ```

- **Gizmos** (select a Hunter in Play mode): the vision cone coloured calm / suspicious / combat, a line to the move
  target and a red one to the aim target, a yellow box at the last sighting, the cyan route, and a ring for each noise
  of the last 8 s at its radius, a dimmer ring at how far it could be heard through the walls counted, and a line to
  the Hunter, green if it was remembered. Colours are the prototype overlay's. Drawing goes through a small painter
  interface (Unity's `Gizmos` in the Editor), so tests record and check every line it would draw.

## Evidence

- **Tests:** 250 EditMode tests in `Blackout.AI.Hunter.Tests` (tree core, hearing, vision, the tree with a fake
  blackboard, the brain, cover on the real island, the prefab, debug text, and the gizmos recorded line by line), all
  passing; the full EditMode suite passes except two World Builder screenshot tests that fail on develop too.
- **In the real game** (`HunterPlayTests`, from Bootstrap): both Hunters walk about 9 m of their routes in 6 s and
  publish `Hunter [BT] Roam > WalkTo`; with the player standing still 5 m in front, a Hunter attacks after about
  0.7–0.8 s, publishes `Hunter [BT] Attack > AimAndFire`, and fires 0.70 s later, its reaction delay. The full
  PlayMode suite: 14 passed, 1 skipped by design.
- **Allocation:** 0 bytes per tree tick, per brain tick and per heard noise.
