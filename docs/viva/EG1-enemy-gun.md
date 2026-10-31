# EG1: supplied enemy rifle

The Patrol, Hunter and Commander gameplay prefabs carry the textured rifle supplied in `D:\model\gun`.
EG2 restores the Tactical Guard's original integrated gun, muzzle and sidearm animations.
The existing `Agents` scene spawner references these prefab GUIDs, so its spawned enemies inherit the replacement.
Player weapons, weapon pickups, combat tuning, event channels and the four brain architectures are unchanged.

## Source and conversion

- Source: `source/083412fa5dba4c75a3bdc3bc77dd0ed5.zip` (`Gun.obj`, three material groups and normal TGAs),
  with the supplied body/scope/grip colour, metallic, roughness and occlusion PNGs.
- Credit: **Dries Deryckere**, as indicated by the supplied MTL and texture filenames. No licence text or source-page URL
  was included. This records the available attribution without inventing a licence.
- `Art/Blender/EnemyGun.source.json` records source hashes, scale, grip and actual triangle counts.
  The supplied files stay unchanged; the repository contains the converted game assets and conversion script.
- Original length: 87.544769 source units, interpreted as centimetres (0.875448 m).
  Source +Y is up and +Z is the barrel. Unity remains metres, +Y up, +Z forward.
- Grip pivot: source `(1.9, -7, -14)` cm. Muzzle: local `(0.0002, 0.08965, 0.577724)` m, at the barrel tip.
- Source: 21,660 triangles. Derived LOD0/1/2: **9,746 / 3,032 / 866** triangles; one mesh and three material slots per level.
  LOD screen heights: 0.35 / 0.10 / 0.005. Materials use URP/Lit.
- Body/scope maps are 1024 pixels, grip maps 512, normal maps 512. `MetalSmooth` packs metallic in R,
  occlusion in G and `1 - roughness` in alpha. Colour maps are sRGB; packed data is linear.

Reproduce the derivative with Blender 4.2 or newer (verified with installed Blender 5.2):

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' --background --factory-startup `
  --python 'Art/Blender/Scripts/build_enemy_gun.py' -- 'D:\model\gun'
```

Then use **Blackout > Agents > Apply Supplied Enemy Gun**. The scoped builder updates its own visual/materials and
the three humanoid gameplay prefab variants through Unity APIs; Tactical Guard is excluded. Existing model assets and
GUIDs are retained. It samples the
existing rifle aim clip to fit the grip, saves the hand-relative offset, and restores the saved skeleton transforms.
The animation builder reapplies this fit when rebuilding an enemy that already contains the supplied rifle.
Run the scoped gun builder again after rebuilding/replacing character visuals from their art builders.

## Connections and lifecycle

`AgentIntent` still reaches the existing `AgentShooter` and `AnimatorDriver`. Patrol, Hunter and Commander use the existing rifle
aim/shoot clips. Patrol/Hunter retain their hand parenting; `AnimatorDriver.SyncWeaponGrip` follows the animated right
hand on the rigid Commander. The shooter now references the
replacement's `MuzzlePoint`; damage, noise and timing still use the existing weapon configuration.

The original Patrol/Hunter/Commander `Gun` transforms remain in their nested models, with their old renderers disabled.
The Tactical Guard retains its original right-hand mesh (glove and integrated carbine), original hand-mounted muzzle,
sidearm animation controller and non-detachable takedown setup. It has no supplied rifle or additional gun-follow binding.
The replacement builder skips Tactical Guard, including direct calls from another tool. The scoped restore command
removes only the previously managed rifle addition through Unity prefab APIs.

`AgentTakedownTarget.carriedWeapon` contains only the three new LOD renderers. Rear attacks hide them; frontal disarms
detach the existing `Gun` root on the three humanoids; cancellation restores it. The takedown builder preserves these
references on reruns. The new gun stays below `Visual`, so the existing corpse fade includes it. Death pickups continue
to grant the existing SMG/sniper choices; the player does not receive a new weapon type.

The base enemy prefab currently leaves optional tracer wiring unassigned. EG1 does not add a new effect. The focused
shot-origin test injects a temporary tracer template into the existing pool to observe the actual muzzle origin.

## Verification and manual review

Original EG1 evidence is outside the repository in `%TEMP%\blackout-EG1`.
EG2 correction logs/tests are in `%TEMP%\blackout-EG2`; current review images are in `%TEMP%\blackout-enemy-gun\preview`.

EG2 checks: restoration/import exited 0; **27/27 selected EditMode** and **4/4 selected PlayMode** tests passed,
both test runs exiting 0. These cover the three rifle users, the robot exclusion/original muzzle/controller, all-four
animation/firing/death/fresh-spawn callbacks and frontal/rear takedown playback. Reapplying the rifle builder and repeating
the restore command both exited 0 and left every saved enemy prefab byte-identical. The restored Tactical Guard prefab
matches its pre-EG1 structure and references; only six quaternion values differ by at most 0.00000005 from Unity's earlier
serialization. All 1,142 protected art, prefab, scene, animation, configuration and settings files remained byte-identical.

Historical EG1 checks before the EG2 Tactical Guard correction:

- Blender conversion exited 0; both conversions produced the same 9,746 / 3,032 / 866 triangle counts.
  All 13 recorded supplied-input hashes still match.
- Unity import/build and builder rerun exited 0. All 47 task files were byte-identical after the rerun.
- `EditMode-verified.xml`: **47 passed, 0 failed**, exit 0: gun integration, soldier animation, shared shooting,
  weapon drops and takedown configuration (five selected classes; not the whole project suite).
- `PlayMode-final.xml`: **6 passed, 0 failed**, exit 0: all-four animation/firing/death/fresh-spawn callbacks,
  repeated gun hide/disarm/restore across LOD changes, frontal cancellation, the other three frontal rigs,
  all-four rear playback and Bootstrap island-floor integration.
- Graphics-enabled preview exited 0. Whitespace checks passed and Git's index remained untouched.
  Unity repeated its existing licensing-token warning; successful runs also logged a background curl callback abort.

The preview tool renders aim, idle and shooting samples from two views and also captures native skinned aim frames.
These are static Editor samples, not a live gameplay or performance pass.

The existing Commander rigid hand meshes are offset from their Humanoid animation joints; this is visible in the rifle
preview as detached hands. The original body meshes, skeleton and avatars were preserved. Correcting that character's
rig/mesh attachment is separate work. The robot retains its original integrated carbine and original animation pose.

Before committing, inspect the four enemy types in **Bootstrap** gameplay: idle/run/aim/shoot, muzzle position from
both sides, near/far LOD changes, frontal and rear takedowns, cancellation after a disarm, death fade, existing pickups,
checkpoint restore and restart. Check Console for new errors. Pay particular attention to the Commander hand limitation
and the restored Tactical Guard gun/muzzle/sidearm pose. No new input, package, lighting or global project setting is required.

## Five-line viva summary

1. I converted the supplied OBJ and PBR maps into an enemy-only Unity rifle visual, keeping the source attribution.
2. I reduced it to three LODs and reused three URP materials across the three humanoid enemy prefabs.
3. The existing animation driver follows the right hand, while the shared shooter uses the new barrel socket.
4. Takedown renderer/disarm references were updated without changing FSM, BT, utility AI or GOAP decisions.
5. Automated checks cover wiring, shots and lifecycle; live appearance and the existing Commander hand problem need review.
