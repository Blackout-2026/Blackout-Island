# Viva notes: behaviour tree core (Systems Engineer, Hunter step H1)

Branch `feature/ht-h1-behaviour-tree`. Commits fd1e368, 511df31 and d0d21f3. The prototype's rules and the Unity
mapping are in `docs/reference/systems-engineer.md` section 13. The full Hunter viva (`docs/viva/hunter.md`) comes in
H4.

## What I built

All in `Assets/Scripts/AI/Hunter/BehaviourTree`, assembly `Blackout.AI.Hunter` (references `Blackout.Shared` only). No
packages: every node is written by hand.

- **`Node`:** the base class. `Tick(context)` returns `Running`, `Success` or `Failure` and remembers it in
  `LastStatus`; `Reset()` clears the node's progress. `IsGuard` marks side-effect-free checks.
- **`Selector`:** tries its children in priority order and returns the first that does not fail. It is reactive: each
  tick starts again from the first child, so a higher-priority branch interrupts a running lower one, and the
  interrupted branch is reset.
- **`Sequence`:** runs its children in order and keeps its place while one is running. Each tick it re-checks the
  guards before the running child, so an action stops the moment its premise is false.
- **`Condition`** (a yes/no question), **`ActionNode`** (does something, may run for several ticks), **`Inverter`**
  (swaps Success and Failure), **`Cooldown`** (stops its child running again for N seconds after it finishes).
- **`LocalBlackboard`:** one Hunter's private memory, shared by the nodes of its tree. Keys are typed
  (`BlackboardKey<T>`).
- **`BtContext` and `BehaviourTree`:** the context carries the blackboard and the tree's own clock; the tree advances
  the clock and ticks the root.

## Why this design

- **Same rules as the prototype.** The reactive Selector, the Sequence that re-checks its conditions and the reset on
  interrupt are copied from `prototype/src/agents/hunter_bt.js`, so the Unity Hunter will react like the one the team
  agreed on.
- **Reactive, so danger wins.** A Hunter walking to a prowl point must react to a gunshot on the next tick, not after
  it arrives. Starting each tick from the top of the Selector gives that for free.
- **Reset on interrupt.** An interrupted action (a sneak towards an old footstep) must not resume with a stale target
  later, so the Selector and Sequence reset what they abandon.
- **Guards, not all children, are re-checked.** Re-running a finished action would repeat its side effects (pick a
  new prowl point every tick). Only `Condition`s, and `Inverter`s of them, are re-checked.
- **Cooldown survives Reset.** If an interrupt cleared it, the Selector would let the branch fire again straight
  away, which is exactly what a cooldown exists to prevent.
- **Typed blackboard.** A wrong type is a compile error, not a runtime cast, and values are stored per type so floats
  and Vector3s are never boxed.
- **The tree's own clock.** Nodes never read `Time.time`, so tests step time by exact amounts in EditMode and the
  Hunter's timers stop when its tree stops.
- **Decisions only.** Nodes return statuses; in H3 the actions fill an `AgentIntent`, and the Agent Controller's mover
  and animator carry it out (playbook rule: brains never touch the Transform).

## Alternatives rejected

- **A behaviour tree package** (Unity Behavior, Behavior Designer): the playbook forbids AI packages, and I must be
  able to explain every line.
- **A non-reactive Selector** (stays in the running branch until it finishes): the Hunter would finish a prowl walk
  before reacting to being shot at.
- **`Dictionary<string, object>` blackboard:** boxes every float and Vector3 (garbage every tick) and turns a typo in
  a key name into a silent bug.
- **Coroutines for long actions:** hard to interrupt and reset from outside, and each one allocates.
- **Reading `Time.time`:** timing tests would need Play mode and real waiting.

## Numbers

- **Tests:** 75 in `Blackout.AI.Hunter.Tests` (Selector 12, Sequence 11, Cooldown 11, leaves and Inverter 12, local
  blackboard 11, node 8, context 6, tree 4). EditMode 1268/1268.
- **Allocation:** a Hunter-shaped tree (engage, react to noise with a 2 s cooldown, prowl) ticked 1000 times with its
  inputs changing, so every branch runs, interrupts and resets: **0 bytes** allocated after warm-up. Overwriting
  float and Vector3 blackboard values 1000 times: 0 bytes.
- **Tick rate:** the prototype ticks the tree at **10 Hz**; H3's brain will do the same, which also bounds the cost of
  re-checking the higher-priority branches every tick.

## Known weaknesses

- **Re-checking costs.** A reactive Selector evaluates every higher-priority condition each tick, so conditions must
  stay cheap (no raycasts inside them; H2's hearing caches its results).
- **Guards are a promise.** `IsGuard` assumes a `Condition` has no side effects; nothing enforces it.
- **Action memory is manual.** The prototype cleared an action's `mem` object automatically. Here an action that keeps
  progress must clear it in its reset callback, or it resumes stale after an interrupt.
- **No Wait node yet.** Timed actions (the 4 s ambush, the 2–4 s look-around) come with the tree in H3.
- **Built in code.** There is no visual editor; the H4 debug text shows the running branch instead.
