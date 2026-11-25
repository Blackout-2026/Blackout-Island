# Ammunition, stance, doors and walls (step F2)

One step of play-testing fixes. What each part does, where it lives and how it was checked.

## The introduction animation is gone

`SceneBootstrap` no longer starts `IntroVideoSequence` (the 150 s video, whose paddling shot showed floating hands). The player is placed at `PlayerStart`
and the game starts, as the committed `SceneBootstrap` did before the introduction was hooked in: this file is back to its committed content. The player therefore starts with all three weapons;
the loss of the guns in the introduction's storm (step L1, `LoseStartingLoadout`) only happened in that video, so the loot rules of `docs/weapon-loot.md` have no effect until something calls it again.
The cinematic files themselves (`Cinematic/`, `Art/Cinematic/`, the production scene) are untouched and are not part of this commit.

## Viewmodel

`ViewmodelHands` hid only the glove and sleeve meshes while sprinting, so the gun or knife stayed in the air without hands. It now switches **every mesh
renderer** of the weapon holder (arms and weapon) off while sprinting and back on afterwards. A renderer that its prefab keeps switched off stays off.
A knife attack is still possible while sprinting (as before), so it can swing without being seen.

## Crouch (C) and prone (Z)

| | Standing | Crouching | Prone |
|---|---|---|---|
| Capsule height | 1.75 m | 1.1 m | 0.7 m (twice the 0.35 m radius, the least a capsule can be) |
| Eye height | 1.63 m | 0.98 m | 0.58 m |
| Walk speed | 4.3 m/s | 55 %, 2.4 m/s | 25 %, 1.1 m/s |

- C toggles crouching, Z toggles prone (`PlayerInputReader.CrouchPressed` / `PronePressed`, actions `Crouch` and `Prone` in `PlayerControls.inputactions`).
  C from prone comes up to a crouch. Sprinting forward or jumping stands the player up.
- Getting taller needs room: `PlayerMotor.TrySetStance` checks a capsule of the new height (`Physics.CheckCapsule`), so the player stays crouched under a low
  ceiling or a table and cannot sprint or jump there.
- The camera pivot follows `PlayerMotor.EyeHeight`, which glides between the stances (`stanceEyeDamping`). **SF1:** crouching and prone movement are silent in
  every direction: `PlayerMotor` emits neither footstep audio callbacks nor AI noise events. Silent travel resets the stride remainder, so standing resumes
  with a fresh stride. Standing walk/sprint noise still follows speed; jump and landing impacts retain their existing sounds and hearing events.
- The rules are pure functions in `PlayerMovementLogic` (`NextStance`, `StanceHeight`, `StanceSpeedMultiplier`); the numbers are in `PlayerMovementSettings`.
  `MovementState` is unchanged: stance is a separate value, so sprint and hand execution keep their existing movement states.

## Medkit counter

The panel shows a medkit symbol (a white case with a red cross, two `Image`s, no font needed) and the number. The "MEDKITS" caption and the "Q · USE" hint are gone.

## Ammunition

| | SMG (MP-9) | Sniper (R-700) | Knife |
|---|---|---|---|
| Magazine | **45** | **10** | no ammunition |
| Reload | **1 s**, starts when the magazine is empty | 1 s | |
| Spare magazines at the start | 2 | 1 | |
| Magazines from each dead body | **2** | **1** | |

- `WeaponAmmo` (pure C#) keeps the loaded rounds, the spare magazines and the reload; `WeaponController` owns one per weapon, spends a round on every shot,
  refuses to fire an empty or reloading gun and ticks every reload in the background (switching away does not cancel it). With no spare magazine the gun stays
  empty until a body's magazines are taken. A checkpoint stores the rounds. The numbers are in `WeaponData` (`magazineSize`, `startMagazines`, `reloadTime`,
  `magazinesPerBody`), set in `SMG.asset`, `Rifle.asset` and `Knife.asset`.
- Every enemy body leaves an `AmmoDrop` (`EnemyWeaponDrop.ammoPrefab`, in front of the body, on all four enemy prefabs). Walking over it gives the body's
  magazines for **both** guns, whether or not the player has the gun yet, and shows "AMMUNITION COLLECTED". Each body can be taken once, and it is a checkpoint object
  like the medkit.
- Each weapon slot of the HUD shows the loaded rounds and, after a slash, the rounds in the spare magazines (for example `45 / 90`), `RELOAD` while reloading and
  `NO AMMO` when nothing is left. The strings are cached, so a shot makes no new string.
- **Assumption:** both guns start with a full magazine plus the spare magazines above, and spare magazines have no maximum. Change `startMagazines` to tune it.

## Agents and walls, agents and doors

- **Walls.** The NavMesh had not been baked since before the entrance hut, the retaining wall and the catwalk were added, so the three compound patrols
  (Patrol-1, -4, -5) had a leg from (10, 55) to (-10, 55) that ran straight through the hut. **Bake NavMesh** was run again (336 colliders, 8,553 triangles).
  `LayoutValidator` has a new row, **Routes clear of walls**: it plans every leg of every agent route on the NavMesh and checks a body-sized capsule every
  0.5 m against the scene's colliders. On the old NavMesh it failed exactly Patrol-1 leg 4, Patrol-4 leg 3 and Patrol-5 leg 5; on the new one it passes, 0 of 84
  legs. The mover also no longer snaps across a wall: `IPathProvider.TryStep` (a default method, so existing fakes compile) refuses a step whose snap from off the
  floor would land on the far side of an edge of the floor.
- **Doors.** The NavMesh runs through every door gap, so agents walked through closed doors. Doors now register in the shared `DoorRegistry` (`SlidingDoor` on enable and
  disable). `PathFollower` asks `DoorGate.MustWait` every step: a closed door within 1.9 m, in the direction the agent is moving, is asked to open
  (`IDoor.RequestOpen`, so the door's noise has the agent as its source) and the agent waits until it is open (80 %, under a second). A **locked** door (the lab's
  maglock or combat lock) is not opened: the agent waits, and the mover's routine re-plan finds another way (the locked door carves the NavMesh).

## Textures

The generator room's walls and lintels were a flat `MetalPlate` (it has no texture set), and the same material was on the corridor's west wall beside the generator door
(the generator room builds that stretch). `MaterialRules` no longer has those two rules, so they take the bunker `Concrete` from the hut like every other room;
**Apply Materials** was run again (Concrete 27 → 33 parts; MetalPlate stays on the ramp floor).

## Checks

**SF1:** Unity 6000.3.25f1 compile/import exited 0. Selected movement and stance EditMode tests passed **25/25**;
the existing standing-footstep and new silent-stance PlayMode tests passed **2/2**, all test exits 0.
Controlled travel samples exercise the configured player motor, audio subscription and shared noise channel, including standing-stride resumption.
Evidence: system temporary directory `blackout-SF1` (logs, test XML and protected-file hashes). These checks do not verify native keyboard capture or audible quality.

EditMode, Unity 6000.3.25f1: the focused classes (viewmodel, stance, ammunition, door gate, path follower and provider, material rules, layout, NavMesh, hut, medkit,
loadout and drops) 246 of 247; the Agents, World Editor, Player and Shared test assemblies 889 of 892. The three that fail are not from this step:
`IntroLoadoutTests.ArrivalCameraUsesSharedPoseAndWidescreenFraming` (the cinematic's own camera test, failing before this step) and two `ScreenshotCaptureTests`
that need graphics (`-nographics` was used). **Validate Layout** passes 145 of 145.

## Not checked

For SF1, run Bootstrap: move forward with W after C (crouch) and Z (prone), then try backward/sideways movement. There should be no movement footsteps.
Outside an enemy's line of sight, check that those movements create no new footstep-hearing reaction; prior sightings/noises can still be investigated.
Stand up and confirm walking/sprinting footsteps and enemy hearing resume. Jump and fall impacts retain their existing sounds.

Play Mode by hand: how crouch and prone feel and look (the camera drop, the capsule under real low ceilings and the prone view of the viewmodel), the new HUD in the real
resolution, the medkit symbol, the ammo drops against dead bodies, agents opening real doors and waiting (headless tests use fake doors and a flat floor), the look of the
generator room with the concrete and the lighting, and the patrol's new route around the hut. The viewmodel can still swing a knife unseen while sprinting.
