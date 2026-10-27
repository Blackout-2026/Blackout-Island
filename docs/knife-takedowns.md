# Cinematic knife takedowns (TD1)

## Connected behaviour

`Player.prefab` and all four enemy prefabs are wired. Normal FPS hands, weapons,
movement and camera continue to use their existing systems. The separate
`TakedownActor.prefab` shows the supplied **Winter Soldier** only during a
cinematic. It does not replace the FPS controller or normal visible hands.
The original Patrol template remains inactive inside that prefab.

Equip a ready knife, stand upright within **1.5 m** and press attack:

- **Behind the enemy:** the existing Mixamo Stealth Assassination pair plays.
  Behind means the enemy's forward direction points away from the player within
  a 60-degree half angle (dot product <= -0.5 against enemy-to-player).
- **Other approaches (front or side):** play Kicking once, then Stabbing once,
  including enemies that are idle, reloading or not firing. The alignment turns
  the pair to face each other. **Stabbing already contains two
  thrusts**, so replaying the whole clip twice would produce four stabs.
- Guns, crouching/jumping, invulnerable/dead targets, cooldowns, blocked
  sight/staging and unsuitable floor also retain ordinary combat behaviour.
  The enemy must be inside the player's forward 120-degree cone.

A ready knife attack checks for a cinematic every time, including when a held
attack becomes ready after cooldown. Exclusive ownership prevents duplicate
sequences. Release attack before another takedown; a release during the cinematic
already counts, so the next click does not need an extra idle frame. One held
press cannot chain multiple cinematics. Nonlethal incoming damage does
not interrupt the frontal kick/double stab, including alignment and camera return.
Health still decreases; player death cancels and releases the cinematic safely.
Rear assassinations still interrupt on damage. Neither style grants invulnerability.

## Imported assets and animation limits

| Project file under `Assets/Animations/Takedowns/` | Original in `D:/model/knife-takedown` | Imported duration / events |
| --- | --- | --- |
| `StealthAssassination_Attacker.fbx` | `Stealth Assassination (1).fbx` | 7.000 s; sound 1.8 s, impact 2.1 s |
| `StealthAssassination_Victim.fbx` | `Stealth Assassination.fbx` | 6.933 s |
| `Takedown_Kick.fbx` | `Kicking.fbx` | 1.600 s; disarm 0.97 s |
| `Takedown_DoubleStab.fbx` | `Stabbing.fbx` | 2.533 s; sounds/hits at 1.0 and 2.4 s |

The double-stab import starts at source frame 6 and ends at 82 (30 FPS), removing
its initial already-extended arm pose. Original FBX bytes are preserved; trimming,
Humanoid mapping, non-looping playback and events live in Unity metadata.
ModelImporter event times are normalized; the builder converts seconds and the
saved-asset tests check the resulting `AnimationClip.events` seconds.

Frontal **Stab Speed = 3.5** plays the double-stab motion continuously in about
0.724 s. Its native sound/hit events occur about 0.286 and 0.686 s after the
stab phase starts: **0.4 s apart**, rather than the original 1.4 s gap. There is
no added wait between thrusts. Kick, victim reactions/death and the rear pair
retain normal playback speed. Recovery waits for the actual native clip clock
to reach its end, so a late frame cannot cut off the finishing event. The overall
sequence duration and camera progress account for the accelerated stab phase.

The rear clip is a **jumping assassination/neck hold**, not a bespoke military
knife capture. The front attack clips are real supplied Mixamo motions, retargeted
onto Winter Soldier. Front enemy reactions reuse the installed Kevin Iglesias
Rifle Aim, Damage01 and Death01 clips. They are **not a matching motion-captured
victim pair**. The graph restarts hit reaction at the kick and first stab, and
starts death at the second stab. Runtime renders demonstrate playback and contact,
but grip, foot-to-gun contact, transitions and differing victim proportions still
need live review. A polished paired frontal reaction would require a compatible
victim animation authored for these attacks; no fabricated clip is presented as
finished choreography.

`SM_Player_WinterSoldier.fbx` copies the supplied `source/00000036.fbx`. Its Biped
bones are explicitly mapped to a valid Humanoid Avatar, including fingers.
The source is about 0.61 m tall; the nested visual uses uniform scale 3 for an
approximately 1.8 m cinematic character. The actor wrapper remains unit scale.
The body texture is assigned to a URP Lit material. The supplied loose gun and
stray mesh submeshes use a transparent material; the existing project knife is
attached to the animated right hand. Separate rear and frontal grip offsets are
fitted from hand/finger bones. The source gun texture is retained with the import.
Credits and source hashes are in [Systems Engineer credits](credits/systems-engineer.md).
The character source folder does not supply an author/licence document.

## Inspector setup and tuning

1. Open `Assets/Prefabs/Player/Player.prefab`. **Takedown Controller** references
   `KnifeTakedown.asset` as **Profile**, `KnifeFrontalTakedown.asset` as **Frontal
   Profile**, and `TakedownActor.prefab` as **Cinematic Actor Prefab**. Input,
   motor, weapons, hands, camera and suspended executors are assigned. Leave the
   FPS hierarchy and normal weapon/Animator setup intact.
2. The rear profile uses player offset `(0,0,-1.85)`, yaw 0 and 2.1 m forward
   clearance. The frontal profile uses `(0,0,1.10)`, yaw 180 and 0.8 m clearance:
   15 cm farther apart than the previous 0.95 m setup. Activation remains 1.5 m.
   Offsets are in the aligned victim's coordinates, with metres, +Y up and +Z
   forward/north. Level placement still follows prototype `(x,y,z)` to Unity
   `(x,y,-z)`. No scenes are regenerated by these builders.
3. Each profile assigns its attack/reaction clips. Front additionally assigns
   **Kick**, **Victim Idle** and **Victim Death**. Set Humanoid, valid Avatar and
   Loop Time off for attacks/reactions/death; the idle may loop. Bake source root
   rotation/translation into the pose and preserve original orientation/position.
   Cinematic root motion is disabled. Match spacing to the animation; horizontal
   separation must be 0.8 to 2.5 m. Inspector warnings validate required events.
4. Rear player events are `TakedownSound` and `TakedownImpact`, one each. Front
   kick needs one `TakedownKick`; double stab needs two `TakedownStabSound` and
   two `TakedownStab` events with integer parameters **1**, then **2**. Events
   belong on the player clips and reach Takedown Actor on its Animator object.
   **Stab Speed** on the frontal profile controls only the double-stab phase
   (1 to 4; installed 3.5). Native Animator events follow that rate automatically;
   source event positions remain at 1.0 and 2.4 s. Review contact after tuning.
5. Open `TakedownActor.prefab` to assign another body. Keep one active Humanoid
   Animator with **Takedown Actor on that same object**, an idle controller and
   a Knife Transform. Use Player layer and visual components only: no Health,
   AI, input, movement, cameras or colliders. Fit feet to root height and local
   +Z facing. Set **Knife Position/Euler** for rear reverse grip and **Frontal
   Knife Position/Euler** for the forward stabbing grip, relative to RightHand.
   Assign the resulting prefab to the player's Cinematic Actor Prefab field.
6. Each enemy's **Agent Takedown Target** references its active Animator,
   AgentRoot, PathFollower, AgentShooter and AnimatorDriver. **Carried Weapon**
   contains gun renderers; **Disarm Weapon** references the detachable Gun root.
   Rear playback hides that gun. Front playback leaves it visible until the kick
   event detaches it with temporary physics. Release restores its original parent,
   transform and layer and removes temporary physics. A living cancellation
   restores visibility; dead victims hide the carried gun while the existing
   Health dropper supplies the normal pickup. Tactical Guard's integrated weapon
   is not detached when there is no separate Gun root; its attack still stops,
   reacts and dies. Do not assign an entire robot arm as Disarm Weapon.
7. Tune camera offsets, end offset, FOV, return time and optional Time Scale on
   each profile. **Cinemachine is not installed**; the native helper smoothly
   borrows the existing camera pose and restores its exact local pose/FOV.
   No package or global input/render setting changes are needed. Timeline is not
   required: one temporary Animator PlayableGraph manages these short sequences.

**Blackout > Systems > Import Downloaded Takedown Animations** copies only missing
source assets from the supplied folders, configures their managed importers,
builds the managed body/materials/profiles, repairs explicit player/enemy wiring
and fits both knife grips. Existing source asset bytes and GUIDs are preserved.
**Build Knife Takedown Support** repairs wiring and the labelled generated actor;
it preserves assigned profile clips. The builders reassert managed visual setup.
For a custom actor use another prefab and reassign it after rebuilding.
Batch-only `TakedownAnimationBuilder.Preview` uses an unsaved review scene and
writes PNGs outside the repo; it refuses interactive execution.
**Apply Quick Frontal Takedown Tuning** explicitly sets the existing frontal
profile to 3.5x stab speed and 1.10 m spacing through Editor APIs. Other profile
settings and clips are preserved. Ordinary builders preserve subsequent tuning.

## Runtime and lifecycle

The coordinator pauses motor, look, camera effects, interaction, scope and weapon
simulation, hides FPS renderers and shows the lazily instantiated cinematic body.
Input stays active for pause/release. Alignment uses CharacterController movement,
not a teleport through scenery. Minimum duration is the profile value or distance
at 3 m/s. Ground-following clearance allows at most 0.35 m floor variation, trying
the original angle then +45, -45, +90, -90 degrees across gentle slopes. Alignment
requires horizontal error within 8 cm and allows normal capsule skin-width height.

The target adapter suspends shared executors while preserving Patrol FSM, Hunter
BT, Guard utility AI and Commander GOAP. Animators keep running. The temporary
graph blends front stages over 0.08 s and plays the stab clip only once. Events
queue reactions, keeping graph changes outside Animation Event traversal.

Rear impact is lethal once. Front kick does **no health damage**, stab 1 removes
half the enemy's current health and stab 2 removes the remainder. Ordered counters
reject duplicate, skipped and late callbacks. Damage uses existing WeaponContext,
Health, hit/kill reporting, drops and registry removal. Missing callbacks never
receive a timed fallback kill; a missing second stab leaves a wounded living
enemy. One kick and two stab events use the existing pooled melee sound/noise
path; no separate finished kick recording is supplied.

Completion holds the dead victim's collapse pose while its existing death fade
resumes, hides the cinematic body and restores FPS meshes, camera and prior
component states. Cancellation, player death, rear-assassination damage,
disabled/missing actors, destroyed target,
pause and checkpoint restoration release control and target ownership. Interrupted
already-dead victims use the existing normal death end pose. Slow motion uses
scaled gameplay time, pauses with the game and restores its owned time scale.
Respawn/checkpoint recovery uses fresh enemy prefabs.

## Verification

Latest repeated-kill/input revision: `repeat-kills-regression.log/.xml` passed
**29/29** selected PlayMode tests, exit 0 (22 takedown cases plus seven existing
knife/reload/agent-animation checks). Compilation/import reported no C# errors
or warnings. The earlier focused `repeat-kills-focused.log/.xml` passed 2/2,
exit 0. New coverage uses real Input System left-button events through the normal
WeaponController Tick: four consecutive kills, one reused cinematic body, one
death per victim and no ordinary swings on eligible clicks. Later targets are
passive front/side enemies, followed by a rear victim. Another test presses during
cooldown, keeps holding until ready, verifies cinematic routing and prevents a
second cinematic until release. A release during playback immediately satisfies
that requirement. Batch mode cannot capture the native mouse, so the same Tick
receives an explicit captured-cursor flag; live Bootstrap cursor/input feel still
needs manual confirmation. Input settings are cloned only in memory and restored
after the test. No input assets, animation/art, profiles, prefabs or scenes changed
in this revision; prior pacing/spacing graphics evidence remains applicable.

Latest pacing/spacing revision: `quick-stabs-tune.log` exited 0 and persisted
3.5x speed / 1.10 m spacing using Editor APIs. `quick-stabs-playmode.log/.xml`
passed **20/20** graphics-enabled takedown checks, exit 0, including Bootstrap,
all four rigs, nonlethal/lethal incoming damage and both native hits at 20 FPS.
The measured gameplay hit interval was **0.400 s** and root spacing **1.100 m**.
`quick-stabs-editmode.log/.xml` passed **5/5** selected saved-profile/prefab checks,
exit 0. The updated `diagnostic-preview/FRONTAL-*.png` renders show the wider pair.
These checks include compilation/import; no new C# warnings/errors were reported.
Live pacing/contact remains a manual check, not a visual quality sign-off.

Gunfire-interruption correction: `damage-fixed-final.log/.xml` passed **19/19**
takedown PlayMode checks, exit 0, including compilation/import, Bootstrap and two
new real hitscan-damage tests. Before the fix, `damage-repro-before.log/.xml`
passed 1/2 (exit 2), and the expanded `damage-fix-regression.log/.xml` passed 18/19
(exit 2): the same nonlethal alignment hit cancelled the frontal sequence.
The original frontal tests used an invulnerable player and missed this case.
The new test uses AgentShooter's actual hitscan/Health path with no invulnerability:
four 10-HP hits during alignment, kick, between stabs and camera return leave
60 HP and complete both stabs. A lethal hit after the kick instead releases the
camera, restores the living enemy's gun and prevents late finishing callbacks.
The earlier graphics/asset checks below predate that control-flow correction;
no animations, prefabs or art changed in the correction, and no new graphics
run was needed at that revision. The latest pacing/spacing graphics run above
also covers the gunfire fix. Live incoming-fire gameplay still needs confirmation.

Unity 6000.3.25f1 import/build and repeat both exited 0. Evidence under
`%TEMP%/blackout-TD1/`:

- `front-build-final.log`, `front-repeat-final.log`: managed import/build;
  all 65 proposed commit files had identical SHA-256 values across the repeat
  (`front-repeat-before.json`). Subsequent changes only document these results.
- `front-editmode-final.log/.xml`: **68/68** selected EditMode checks, exit 0.
- `front-regression-verified.log/.xml`: **24/24** selected headless PlayMode
  checks, exit 0 (17 takedown cases and seven existing combat/animation checks).
- `front-play-final.log/.xml`: **17/17** graphics takedown cases, exit 0;
  `front-visual-final.log/.xml`: **1/1** final graphics front sequence after the
  dead-gun visibility fix, exit 0. `diagnostic-preview/FRONTAL-*.png` contains
  real runtime captures; `REAL-*.png` and `BOOTSTRAP-*.png` show rear playback.

Original downloaded bytes match the project FBXs/textures. Pre-step hashes of
existing art, animation, scenes, packages and settings differ only for the
intentionally wired Player and four enemy prefabs. The pre-existing settings
edit and cinematic-intro work remain outside TD1. All required metadata exists.
Source/document whitespace checks passed; Git's default check reports Unity's
serialized empty `m_Name: ` fields as trailing spaces. No C# compiler errors or
warnings occurred in the final runs. Existing licensing/access-token messages
remain in batch logs; they did not prevent compilation or tests.

The source/asset checks validate profiles, imported clip
seconds/events, prefab references and the Humanoid body. PlayMode tests exercise
rear playback on all four rigs; rear/front/side selection, disarm, two live
native hits, duplicate/out-of-order protection, cancellation and missing finishing
callback; all four front collapses; camera/control restoration; pause, damage,
checkpoint cleanup and the normal death fade.

The saved Bootstrap integration test loads its additive scenes, verifies the
inherited player and 24 adapters, probes real island floor and completes rear
playback on Hunter-1 and Hunter-2. It freezes AI and grants the test player
invulnerability for repeatability. This does not prove live incoming-fire gameplay.
Rendered runtime captures show the actual supplied body and clips, rather than
the old TEST ONLY diagnostic death clips used in isolated lifecycle tests.
These are selected tests and controlled captures, not a whole-project or visual
quality sign-off. An earlier graphics AN1 shooting test missed its short Shoot
state window; its headless regression passed without changing normal shooting.

Before calling the animation finished, check in Bootstrap:

1. Equip knife and approach behind: rear animation. From the side or front,
   whether the enemy fires or not: kick, exactly two stabs, death after stab 2,
   one kill/drop, then FPS return. Repeat on several enemies without restarting.
   A click during cooldown followed by holding must still choose the cinematic
   when ready. Holding through a completed sequence must require release before
   another cinematic; releasing during the sequence must allow the next click.
2. Check each enemy type's grip, feet, knife contact, gun motion, hit reactions,
   collapse, held corpse pose and fade. Guard's integrated weapon stays attached.
3. Review camera motion/lighting on beach, forest, slopes and near walls; ensure
   FPS meshes never overlap the cinematic body and no body/gun remains orphaned.
4. Test range limits, cooldown, crouch/jump, guns, dead/invulnerable targets,
   repeated presses/held attack, blocked staging and an obstacle during alignment.
5. Test pause/resume, optional slow motion, taking damage, death/respawn,
   checkpoint restart and scene changes during alignment, kick and stabbing.
   With enough health, nonlethal bullets must leave the frontal sequence running
   while HP decreases; lethal damage must cancel safely. Rear damage still cancels.
   Confirm camera, controls, gun visibility, time scale and audio recover.
6. Verify normal movement, weapon switching, reload, scope, shooting and restart.

## Five-line viva summary

- A transient coordinator borrows control without replacing the FPS controller.
- The supplied Winter Soldier is a separate cinematic Humanoid visual.
- Approach direction selects rear assassination or kick/double stab on every eligible attack.
- Animator events synchronize disarm, sound, nonlethal hit and finishing damage.
- Shared executor suspension and cleanup preserve the four AI architectures.
