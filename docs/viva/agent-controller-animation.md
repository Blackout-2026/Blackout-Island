# Agent animation (A3) — design notes

Owner: Agent Controller. Covers `AnimatorDriver`, `ProceduralRobotAnimator`, `AgentVisualAnimatorBase`,
`RobotGaitMath`, `IAgentVelocitySource` and the `AnimatorControllerBuilder` Editor tool. Numbers cite
`docs/reference/agent-controller.md` (sections 9.1–9.3, read from the prototype's source in A0) and
`docs/reference/core-developer.md` (section 3.5) unless marked **[GUESS]**.

## What it does

Two `IAgentAnimator` implementations, both driven only by `AgentRoot` (the mover's velocity and the
brain's intent) — brains never reach either one directly:

- **`AnimatorDriver`** (humanoid): an Animator Controller (`Assets/Animations/Agent_Humanoid.controller`,
  built by `AnimatorControllerBuilder`) with parameters `Speed`/`Aiming`/`Hit`/`Dead`, a `Locomotion` blend
  tree (Idle/Walk/Run) and separate `Aim`, `HitReact`, `Death` states.
- **`ProceduralRobotAnimator`** (security robot): no Animator Controller — it rotates named joint
  transforms directly each frame using `RobotGaitMath`, because the robot is built from separate rigid
  parts with the pivot at each joint, not a skinned/rigged mesh (`core-developer.md` 3.5).

Both share death timing through `AgentVisualAnimatorBase`: whatever "collapse" looks like is the
subclass's own job, but 3.2 s after death both start fading (scale to zero) over 1.6 s, then disable —
the prototype's own numbers (`agent-controller.md` 5.2). Scaling the visual rather than fading a material
keeps this independent of whatever surface type the Core Developer's materials use, which this role
doesn't own.

## Why this design, against the alternatives

- **Procedural for the robot, Animator Controller for the humanoid** rather than one approach for both:
  the robot has no skin weights to animate with clips in the first place (it's rigid parts on pivots), so
  an Animator Controller would need baked clips for a rig that doesn't exist the way a humanoid's does.
  Procedural math is also directly portable from the prototype's own formulas, which are written the same
  way (per-frame trig on joint angles, not keyframes).
- **A shared base class (`AgentVisualAnimatorBase`) for the death fade**, not duplicated in each
  implementation: the fade/disable timing is identical for both agent types and has nothing to do with
  how either one plays its collapse, so duplicating it would be two copies of the same bug waiting to
  diverge.
- **A new `IAgentVelocitySource` interface instead of a concrete `PathFollower` reference**: keeps both
  animators testable against a fake and decoupled from the one mover implementation that happens to exist
  today. Not added to the shared `Blackout.Shared` contracts (section 3 of the playbook) since only this
  role's own two animator classes read it — adding it there would be a contract change nobody else asked
  for.
- **One rotation axis (local X / `Vector3.right`) for every robot joint**: a single, explicit, documented
  convention, chosen because no real rig exists yet to dictate one. If C3's joints end up oriented
  differently, only `ProceduralRobotAnimator.SetLocalRotation`'s axis needs to change — the gait math
  itself (`RobotGaitMath`) is axis-agnostic.

## Exact animation clips I still need to supply

`AnimatorControllerBuilder` looks for these under `Assets/Animations/Humanoid/`, as `<Name>.fbx` or
`<Name>.anim` (either extension works — it checks `.fbx` first):

| Clip name | Used for |
| --- | --- |
| `Idle` | Locomotion blend tree, Speed = 0 |
| `Walk` | Locomotion blend tree, Speed = 1.5 |
| `Run` | Locomotion blend tree, Speed = 4.0 |
| `Aim_Idle` | the `Aim` state |
| `HitReact` | the `HitReact` state |
| `Death` | the `Death` state |

Any free humanoid pack with these poses works (Mixamo is the obvious source — its own clip names don't
need to match, just rename the files on import). Once they're in place, run
**Blackout > Agents > Create Humanoid Animator Controller** again; it fills in whichever slots were empty
and leaves anything already assigned alone.

## Hand-off to the Core Developer (C3 — robot prefab)

`ProceduralRobotAnimator` finds its joints anywhere under its own transform by exact name. For the robot
visual prefab to animate correctly, its part hierarchy needs child objects named:

```
Pelvis, Head, Thigh_L, Thigh_R, Shin_L, Shin_R, Foot_L, Foot_R,
UpperArm_L, UpperArm_R, ForeArm_L, ForeArm_R
```

(PascalCase, `_L`/`_R` suffix — matches `docs/art-pipeline.md` section 2's naming rules and section 11's
working names.) A joint that doesn't exist yet is skipped and logged once as a warning, not an error, so
this component is already safe to add to `AgentBase.prefab`'s `Visual` child before C3 lands.

## Known weaknesses (for the viva)

- **No upper-body avatar mask layer**: the humanoid controller is a single layer, so `Aim` fully replaces
  `Locomotion` rather than blending with it — an agent can't visibly aim *while* walking sideways, even
  though some AI states ask for exactly that (the Tactical Guard's `MoveToCover` fires while running). The
  correct fix is a second layer with an upper-body avatar mask, which needs a real Humanoid-rigged avatar
  to define the mask against — not available until a real model exists. Noted as follow-up work, not
  silently dropped.
- **One shared blend-tree threshold pair (1.5 / 4.0)** for every humanoid agent type, even though each
  type's actual walk/run speeds differ slightly (Hunter 1.6/4.5, Guard — humanoid Security Patrol —
  1.55/4.1, Commander 1.3/3.4; `agent-controller.md` 3.1). A single shared `.controller` asset can't
  thread that needle without per-instance overrides, which Unity's Animator doesn't support for blend
  tree thresholds. Acceptable for now since the mismatch is small relative to the thresholds; worth
  revisiting with Animator Override Controllers per type if it reads badly once real clips are in.
- **Arm walk-swing is not wired** on the robot (`core-developer.md`: "swing ±0.25 rad walking") — only the
  aim pose moves the arms. Scoped out to keep this step to what A3 asked for (walk, run, aim, collapse);
  the formula would be the same shape as the leg swing.
- **Robot thigh/shin death-buckle angles are guessed** (`RobotGaitMath.DeathThighBuckle`/`DeathShinBuckle`,
  -0.9 / 1.4 rad): neither reference doc gives a number, only "thighs/shins buckle" qualitatively. Pelvis
  drop/pitch and head pitch *are* real prototype numbers; these two are not. Flagged `[GUESS]` in the code
  itself as well.
- **No pelvis twist or head look-yaw** on the robot (`core-developer.md` 3.5 lists both: pelvis twists
  ±0.06 rad, head yaws ±1.0 rad for looking) — left out to keep the joint list and the gait math to what's
  testable without a real rig to check against visually.
