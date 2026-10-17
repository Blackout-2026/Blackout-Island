# Viva notes: Tactical Guard utility AI (Core Developer, steps T1 to T3)

Code: `Assets/Scripts/AI/TacticalGuard`. Numbers: `Config/TacticalGuardConfig.asset` (class `TacticalGuardConfig`).
Prototype source: `docs/reference/agent-controller.md` section 6.3 (`tactical_guard_utility.js`).

## What it does

A guard stands on its post and has four things it can do: **Attack**, **MoveToCover**, **Reposition**, **HoldPost**.
Every think tick (10 Hz, from `AgentRoot`) `TacticalGuardBrain`:

1. **Senses.** Builds a `GuardSituation`: can it see the player (one `Linecast` on the Level layer), distance to the
   player, how fresh its knowledge of the player is, threat, health fraction, distance from its post, and the quality of
   the best free cover point.
2. **Scores.** `GuardActionScorer` gives each action a score from 0 to its weight. The highest wins; the current action
   gets a small bonus so it does not flicker.
3. **Returns an intent.** `GuardIntentBuilder` turns the winner into an `AgentIntent`. The brain never moves, animates or
   shoots; the mover, animator and shooter do that.

Cover is **claimed** (`CoverClaims`) so two guards never run to the same point. A claim is held while the guard is
attacking from the cover or running to it, and released when it goes back to its post, searches, or dies.

## How an action is scored

Each action is a list of considerations. A consideration turns one input (0 to 1) into a utility (0 to 1) through a
response curve, then the action's score is

```
score = weight x (u1 x u2 x ... x un) ^ (1 / n)
```

the product of the utilities with the geometric-mean compensation factor, so an action with three considerations is not
punished for having more of them than an action with two. A utility of 0 on any consideration makes the score 0, which
is how "cannot see the player" switches Attack off. A **floor** on a consideration lifts its lowest utility, for the
places where 0 should weaken an action but not cancel it (HoldPost at its post, a nearly dead guard still able to shoot).

| Action | Considerations (input, curve) | Weight |
| --- | --- | --- |
| Attack | line of sight (1 or 0, linear), proximity (1 - distance / 36, logistic), own health (linear, floor 0.1) | 1.0 |
| MoveToCover | cover availability (best free cover quality, linear), threat (linear) | 1.0 |
| Reposition | knowledge freshness (linear), not yet engaged (visible x attack proximity, **inverse**) | 0.6 |
| HoldPost | pull of the post (distance from post / 12, linear, floor 0.3), calm (knowledge freshness, **inverse**, floor 0.4) | 0.8 |

**Cover quality** (0 to 1) of one cover point is the geometric mean of three factors: near the guard
(1 - d / 14), away from the player (d / 8, capped at 1) and near the post (1 - d / 20). It must also be hidden from the
player's eye (a ray from the player's eye to a crouched head at the cover point is blocked), and not claimed by another
guard. Any factor at 0 rules the point out.

## Every parameter, and why

"Prototype" means the number is taken from `tactical_guard_utility.js`. "Mine" means I chose it, and the last column says how.

### Ranges and distances (metres)

| Parameter | Value | Source | Reasoning |
| --- | --- | --- | --- |
| attackRange | 36 | Prototype (range curve ends at 36) | Proximity input is 1 - distance / 36, so attack reaches 0 at 36 m. |
| sightRange | 36 | Mine | Same as the attack range: there is no point seeing a target it cannot engage. |
| postRadius | 12 | Prototype | Distance from the post that counts as "fully away" (input 1). |
| repositionLimit | 15 | Prototype | A guard searching never strays more than 15 m from its post. |
| repositionLimitAlert | 30 | Prototype | When the Blackboard alert is up the whole squad converges, so the leash doubles. |
| coverSearchRadius | 14 | Prototype | Only cover within 14 m of the guard is considered. |
| coverPostRadius | 20 | Mine | A guard should defend its post, not abandon it for a better wall. 20 m keeps cover inside a room or two. |
| coverStandoff | 8 | Mine | Cover closer than 8 m to the player is penalised in proportion, so a guard does not run towards the player to hide. |
| coverArriveDistance | 1.0 | Prototype | Counts as "at cover" within 1 m. |
| postArriveDistance | 1.2 | Prototype | Counts as "at the post" within 1.2 m. |
| repositionArriveDistance | 1.2 | Prototype | Same arrival distance as the post. |

### Heights (metres above the feet)

| Parameter | Value | Source | Reasoning |
| --- | --- | --- | --- |
| guardEyeHeight | 1.95 | Prototype (guard eye height; the Agent Controller's guard prefab uses it too) | The robot is tall, so its sight ray starts at head height and sees over low props. |
| playerEyeHeight | 1.6 | Mine | The player's camera height; the start of the ray that checks if cover is hidden. |
| coverHeadHeight | 1.0 | Mine | A guard in cover is crouched, so cover only has to hide a head at 1 m, not a standing 2 m robot. |
| playerChestHeight | 1.2 | Mine | Aim point, below the camera, so shots land on the body. |

### Memory (seconds)

| Parameter | Value | Source | Reasoning |
| --- | --- | --- | --- |
| knowledgeWindow | 10 | Prototype | Knowledge of the player fades linearly from 1 to 0 over 10 s, then the guard goes back to its post. |
| damageWindow | 3 | Prototype | "Recent damage" fades over 3 s. The prototype uses a 1-or-0 flag; a fade avoids a threat that snaps off. |

### Threat (sum of the sources, clamped to 0 to 1)

| Parameter | Value | Source | Reasoning |
| --- | --- | --- | --- |
| threatVisible | 0.4 | Prototype | Being seen by the player is a moderate threat on its own. |
| threatRecentDamage | 0.7 | Mine (prototype 0.5) | Scaled by how recent the hit is. Raised from 0.5 because the fade lowers the average. At 0.7 a guard shot from out of sight still prefers cover (0.65) over searching (0.57). |
| threatAimedAt | 0.25 | Prototype | Only counts while visible. |
| aimedDot | 0.97 | Prototype | The player's facing dotted with the direction to the guard; above 0.97 is about 14 degrees. |
| threatAlert | 0.15 | Mine | Brief asks for threat "from the Blackboard"; a raised alert nudges the guard to be careful. Small on purpose, so the Commander's alert never overrules what the guard actually sees. |

### Selection and scanning

| Parameter | Value | Source | Reasoning |
| --- | --- | --- | --- |
| hysteresis | 0.1 | Prototype (inertia) | Bonus added to the current action's score. Tested at the Attack/Reposition boundary: Reposition leads by 0.07 at 32 m and the guard still keeps attacking. |
| scanRadius, scanHeight, scanSpeed | 6, 1.6, 0.5 rad/s | Prototype | Head sweep in a circle around the post while holding it. |

### Action weights (the ceiling of each score)

| Parameter | Value | Reasoning |
| --- | --- | --- |
| attackWeight | 1.0 | Prototype's attack base 0.35 + range 0.65 also peaks at 1.0. |
| moveToCoverWeight | 1.0 | Same ceiling as Attack, so cover only wins by being the better answer (low health under fire), not by a bigger weight. |
| repositionWeight | 0.6 | Chosen so Attack and Reposition cross at about 31 m when the player is visible (prototype "long range" is 30 m): at 32 m Attack scores 0.49, Reposition 0.56. Also keeps it below cover when the guard has been shot. |
| holdPostWeight | 0.8 | Beats a stale Reposition (knowledge 0.1: 0.19 against 0.78) but loses to a fresh one (knowledge 1: 0.60 against 0.51 when 12 m from the post). |

### Response curves

| Consideration | Curve | Reasoning |
| --- | --- | --- |
| attackLineOfSight | Linear | Input is 0 or 1; linear passes it through. |
| attackProximity | Logistic, steepness 12, midpoint 0.25, on closeness (1 - d / 36) | A plateau at close range and a steep drop between about 20 m and 35 m. It is about 0.5 at 27 m, the same as the prototype's 1 - smoothstep(18, 36). |
| attackHealth | Linear, floor 0.1 | A guard at 30% health scores 0.37, enough to make cover win at the same threat; the floor stops a guard at 0 HP forcing the geometric mean to exactly 0. |
| coverAvailability | Linear | Better cover is proportionally more attractive. |
| coverThreat | Linear | Cover is wanted in proportion to the danger. |
| repositionKnowledge | Linear | The fresher the sighting, the more it wants to go and look. |
| repositionDisengaged | Inverse | The input is "how ready to shoot already" (visible x attack proximity); inverted, a guard that can already hit the player has no reason to move. |
| holdPostPull | Linear, floor 0.3 | The post always pulls a little (floor 0.3), and more the further the guard is from it. |
| holdCalm | Inverse, floor 0.4 | The less it knows, the more it stays; the floor stops fresh knowledge from cancelling HoldPost entirely. |

## Worked example: hurt guard, player in view, cover nearby

Situation (this is `WorkedExample_MatchesTheHandCalculationInTheVivaNotes`): the guard can see the player at 15 m, has
30% health, was hit a moment ago, is 4 m from its post, and the best free cover is 4 m away, 12 m from the player and
4 m from the post.

**Inputs**

- Threat = visible 0.4 + fresh damage 1.0 x 0.7 = 1.1, clamped to **1.0**. Knowledge = **1.0** (seen this moment).
- Cover quality: near guard 1 - 4/14 = 0.714; away from player min(1, 12/8) = 1; near post 1 - 4/20 = 0.8.
  Geometric mean = (0.714 x 1 x 0.8)^(1/3) = **0.830**.

**Attack** (weight 1.0)

- Line of sight = 1.
- Closeness = 1 - 15/36 = 0.583. Logistic with steepness 12, midpoint 0.25: sigmoid(12 x 0.333) = sigmoid(4.0) = 0.9820.
  Normalised so the endpoints are exactly 0 and 1: low = sigmoid(-3) = 0.0474, high = sigmoid(9) = 0.99988, so
  (0.9820 - 0.0474) / (0.99988 - 0.0474) = **0.981**.
- Health 0.3 with floor 0.1: 0.1 + 0.9 x 0.3 = **0.37**.
- Score = 1.0 x (1 x 0.981 x 0.37)^(1/3) = (0.363)^(1/3) = **0.713**.

**MoveToCover** (weight 1.0)

- Availability 0.830, threat 1.0. Score = 1.0 x (0.830 x 1.0)^(1/2) = **0.911**.

**Reposition** (weight 0.6)

- Knowledge 1.0. Engaged = visible x proximity = 0.981, inverse = 0.019.
- Score = 0.6 x (1.0 x 0.019)^(1/2) = 0.6 x 0.137 = **0.082**.

**HoldPost** (weight 0.8)

- Post pull: distance 4 / 12 = 0.333, floor 0.3: 0.3 + 0.7 x 0.333 = 0.533. Calm: inverse of knowledge 1.0 = 0, floor 0.4 = 0.4.
- Score = 0.8 x (0.533 x 0.4)^(1/2) = 0.8 x 0.462 = **0.370**.

**Result:** MoveToCover 0.911 > Attack 0.713 > HoldPost 0.370 > Reposition 0.082. The guard runs to cover and shoots
on the way because it can still see the player. At full health the Attack health factor is 1.0 and the same situation
gives Attack about 1.0, so the guard stands and fights: low health is what moves it to cover.

The debug text shows this as `scores: Attack 0.71 | MoveToCover 0.91* | Reposition 0.08 | HoldPost 0.37`.

## Why utility AI fits a guard better than an FSM or a behaviour tree

- **The guard's choice is a trade-off, not a sequence.** "Hurt, low health, still visible, cover nearby, player far away"
  mixes five facts. An FSM needs a transition for every combination, or a priority order that decides in advance which fact
  wins. A behaviour tree needs a fixed order of branches (cover before attack?) and cannot say "attack, unless health is
  low enough that cover is worth more". Utility AI just scores everything and takes the best.
- **Behaviour changes smoothly.** Health, distance and knowledge are continuous, so the guard gets steadily more cautious
  as it is hurt, and steadily more willing to search as its knowledge fades, with no hard thresholds to tune.
- **Tuning is data.** Every weight and curve is in `TacticalGuardConfig`, and the scorer re-reads it every tick, so a
  designer can change the guard's personality live in play mode. In an FSM or tree the equivalent change is a code edit.
- **Adding behaviour is cheap.** A fifth action is a new list of considerations; no existing transition has to change.
- **Stability is built in.** The hysteresis bonus stops flicker at a boundary, where an FSM needs guard conditions or timers.
- **Cost.** The scores are harder to predict than a tree's fixed order, and the numbers need tuning. That is why the
  brain exposes every score and input in `GetDebugText` and draws its cover and post as gizmos.
- **Where the other architectures fit.** A patrol with clear modes suits an FSM; the Hunter's prioritised tactics suit a
  behaviour tree; the Commander's long plans suit GOAP. The guard's job, reacting to a changing mix of pressure and
  opportunity from moment to moment, is what utility AI is for.

## Differences from the prototype

- **Multiplicative scoring.** The prototype adds terms (`base + range * rangeCurve`, and so on). The brief asks for
  considerations combined by multiplication with compensation, so the formulas differ and the weights were re-tuned (above).
- **Cover is claimed.** The prototype lets two guards pick the same point (flagged in the reference notes); here they cannot.
- **Cover must hide a crouched guard, not a standing one.** The prototype checks visibility from the player's eye to the
  point; here the target is 1 m above it.
- **One sight ray, not two.** The prototype casts eye to the player's head and, if that is blocked, eye to the chest.
  The Unity guard casts only to the chest, so it can miss a player whose head shows over a low wall.
- **No crouch at cover.** `AgentIntent` has no crouch flag, so the guard just stops at the cover point. Agent Controller work.
- **No random reposition snap.** The prototype snaps the reposition point to a random walkable point within 3 m. The
  target here is the clamped last known position, and the mover finds the path.

## Tests behind these notes

- `GuardActionScorerTests`: each action wins in its own situation, and the boundary cases (hysteresis, live config).
- `CoverTests`: claims, and cover that is nearest, unclaimed, hidden, near the post and away from the player.
- `GuardIntentBuilderTests` and `TacticalGuardBrainTests`: each action's intent and the whole brain with a fake context.
- `GuardDebugFormatterTests`: the text format, including the worked example above.

## One number and why

`repositionWeight` 0.6. Every other weight is 1.0 or close; this one is the knob that sets where a guard that can see
the player stops shooting and starts closing the distance. At 0.6 it happens at about 31 m, matching the prototype's 30 m
"long range" rule, and Reposition still loses to cover when the guard has been shot.

## Known weaknesses

- The default numbers are tuned against the tests and the hand calculations, not by playing. Expect to adjust them once
  the guard is in the Agents scene.
- Sight is 360 degrees up to 36 m with no reaction delay, so a guard spots the player the instant a ray is clear. The
  prototype has an awareness meter.
- The geometric mean flattens differences: with three considerations a utility of 0.37 only costs 0.71 of the score.
  That is intended, but it makes the weights less direct to reason about than an additive formula.
- Line of sight is blocked only by the Level layer. A wall or door on another layer would not block sight.
- Cover claims live in a shared table that the guards use by default (`CoverClaims.Shared`). If the Agent Controller
  prefers them on the Blackboard, only `CoverClaims` has to move.
