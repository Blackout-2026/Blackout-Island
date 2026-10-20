# Viva notes: the Hunter's tree (Systems Engineer, Hunter step H3)

Branch `feature/ht-h3-the-hunter-tree`. Commits 0122a8c, 8e26c4d, 9a6aeb6, 57ffb84, d6f99c9, 4f8ed59 and 97f7898. The prototype's
leaves and numbers, the Unity mapping and every [GUESS] are in `docs/reference/systems-engineer.md` section 15. The
tree core is H1, the hearing H2.

## What I built

All in `Assets/Scripts/AI/Hunter/Brain`, assembly `Blackout.AI.Hunter`.

- **`HunterBrain`** (`IAgentBrain`, on the Hunter prefab): every think tick (10 Hz, from `AgentRoot`) it senses, writes
  what it sensed into the tree's blackboard, ticks the tree and returns the `AgentIntent`. It also takes the
  Commander's orders (`IDirectiveReceiver`) and alert (`IAlertReceiver`), and reports sightings to the squad Blackboard.
- **`HunterTree`**, built from the H1 nodes:

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

- **`HunterVision`:** the prototype's awareness meter: 115° cone, 25 m, rays to the head then the chest, fills while
  seen, detected at 1, lost 6 s after the last sighting.
- **`HunterWorld`:** its own forest waypoints in route order, cover from cover markers, terrain trees and rocks, and
  bushes, and randomness seeded from its name. Bushes are terrain *details*, not trees, so they are read from the
  detail layer once and shared by both Hunters (a first version missed them; found by checking the brief again).
- **`HunterConfig`** (asset in `Assets/Scripts/AI/Hunter/Config`): every number, with where it comes from.
- **Wiring:** `Blackout/Systems/Build Hunter Config` puts `HunterBrain` and `HearingSystem` on `Hunter.prefab`, with
  the Agent Controller's permission.

## Different reactions (the brief's "gunfire vs footsteps vs sprinting")

| Heard | Branch | What the Hunter does | Why |
| --- | --- | --- | --- |
| Footstep, door, crate, melee | Investigate | walks to it, then ambushes 4 s aiming at the spot | the prey has not noticed it; quiet approach, then wait for it |
| Sprint | Investigate | runs to it, then ambushes 2 s | the prey is moving fast; closing the distance matters more than stealth |
| Gunfire, generator, alarm | CautiousApproach | beyond 16 m walks from cover to cover, pausing 1.5 s at each; within 16 m runs to cover and holds 2.5 s | the noise can shoot back |

**Camouflage:** while the player stands still, the Hunter's awareness fills half as fast (0.5, a config value). A
walking player 10 m away is detected and fired on at tick 6 (0.6 s), a still one at tick 11, a sprinting one at
tick 5. (Through the brain the still player is fired on at tick 10: on its first tick the brain has no earlier position
to measure speed from, so it counts the player as walking.)

## Why this design

- **A behaviour tree for this agent:** its reactions are a priority list (shoot, chase, react to noise, search, obey,
  roam), and a reactive Selector re-checks that list every tick, so a gunshot interrupts a stroll without a
  transition written for every pair of states. Adding the Commander's orders was one more branch.
- **Senses → facts → tree → intent.** The tree reads only its blackboard, so each branch is tested by writing facts
  by hand (the brief's "fake blackboard"): no scene, no physics, no player.
- **The tree never changes the senses.** It records when it finished with a noise, a sighting or an order, and
  ignores anything older. Senses stay honest; the tree's memory is in one place.
- **Reaction delay and aim error stay in `AgentShooter`** (the Agent Controller's A4 executor, 0.7 s and 0.024 for
  the Hunter). The brain only says aim, fire and where to look, like every brain, so they are not done twice.
- **Speed from movement.** The brain measures how far the player moved between ticks; it needs nothing from the
  player's code (no reference to `PlayerMotor`), so camouflage works through the shared contracts only.

## Alternatives rejected

- **A flat FSM:** six reactions plus orders would need transitions between most pairs, and interruption rules on each.
- **The prototype's flank point** for gunfire: the brief asks to "advance using cover/bushes, slower"; cover-to-cover
  walking says exactly that.
- **Clearing the hearing when a noise is handled:** the senses would then depend on the tree; handled times keep them
  separate.
- **A camouflage multiplier on the Hunter's own visibility:** the player is not an AI, so nothing would read it. It is
  a multiplier on the Hunter's detection of a still player instead (my reading of the brief, marked [GUESS]).

## Numbers

- **Tests:** 84 new EditMode (tree 30, vision 22, brain 13, world 6, cover on the real island 5, config 4, prefab 3)
  and 1 PlayMode; Hunter EditMode 215/215.
- **Detection** at 10 m: walking tick 6, still tick 11 (camouflage), sprinting tick 5.
- **Allocation:** 0 bytes over 1200 tree ticks and over 1000 brain ticks.
- **Cover on the island:** 657 trees and rocks and 371 bush cells (376 bushes); no bush inside the 6 m clearings the
  World Builder keeps around the Hunters' route points, which checks the position maths.
- **In the real game** (`HunterPlayTests`, from Bootstrap): both Hunters spawn with the brain and their config
  assets, and walk about 9 m of their routes in 6 s (1.6 m/s). With the player standing still 5 m in front, the
  Hunter attacks after **0.70 s** and fires **0.70 s** later: exactly the 0.7 s reaction delay of its `AgentShooter`.
- **PlayMode suite:** 14 passed, 1 skipped by design; the Agent Controller's 8-agent smoke test and my Stress Test
  pass with the Hunters on the new brain.
- **Build Hunter Config:** run 2 byte-identical.

## Known weaknesses

- **No crouch or sneak.** `AgentIntent` has neither (flagged by the Agent Controller in A0), so the Hunter walks where
  the prototype sneaks and crouches.
- **Visibility from speed only.** The player has no crouch or flashlight yet, so only still and sprinting change it.
- **Agents' gunfire is silent.** `AgentShooter` on `AgentBase` has no gunfire channel, so the Hunter reacts to the
  player's shots only, until the Agent Controller assigns `Noise.asset` there.
- **Cover is not checked for line of sight.** A tree between the Hunter and the noise counts as cover whatever its
  size; the prototype does the same.
- **A move that cannot be reached** gives up after 25 s, as in the prototype; the brain cannot see the mover's path
  failures.
