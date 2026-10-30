# Matching Winter Soldier FPS arms (WA1)

SG1 corrects only the SMG grip/wrist frames and adds its own RMB sight pose.
Its gun/magazine reload motion stays intact; right-grip and support-hand motion
now follow the weapon-specific fit. See [SMG grip](smg-grip.md) for current
parameters and the scoped rebuild. WA1 details below remain historical context.

The Player prefab now uses arms from the same supplied Winter Soldier as the
cinematic actor. The left retains its metal sleeve/glove and the right its skin
and dark clothing. They share the existing `M_PlayerWinter_Body.mat` and its
body texture with that actor. No new camera, material or texture is needed.

## Assets

- Read-only source: `D:/model/winter-soldierfreetexturedrigged/source/00000036.fbx`
  and `textures/00000037.png`, already credited under TD1.
- Native extraction: `Assets/Models/Custom/SM_WinterArms_Source.fbx`. This retains
  source UV loops, skin weights and Biped bones with required ancestors. Only arm
  geometry remains, including upper sleeves for future authoring. Native scale
  matches the supplied model; the cinematic visual uses scale 3.
- Fitted derivatives in that same folder: `SM_WinterArms_Smg.fbx`,
  `SM_WinterArms_Rifle.fbx`, `SM_WinterArms_Knife.fbx`. Normal FPS geometry is
  cropped to forearms/hands. Both gun files include the existing gun/magazine
  mesh copies and retargeted reload motion.
- Working file: `Art/Blender/SM_WinterArms.blend`.
- Generator: `Art/Blender/Scripts/build_winter_arms.py`.
- Installer: `Assets/Editor/SystemsEngineer/WinterArmsBuilder.cs`.

Face selection uses source arm deform groups without welding or decimation.
The native extraction contains 1,499 left-arm and 1,232 right-arm triangles.
The fitted FPS pair contains 2,426 triangles, including 6 cm of original sleeve
past the elbow to hide its cut edge during swings. With the original gun geometry,
the SMG reload has 6,626 and the rifle 6,194, below the 10,000 viewmodel budget.
The native rig is preserved separately; fitted copies explicitly merge twist
weights into the forearm and map source finger groups to their corresponding
FPS joints. UV loops and relative weights survive, without automatic weighting.

## Why the animations needed retargeting

The original holding and knife hands were static meshes. Gun reloads used
separate Generic rigs with different pivots and rolls. Neither is directly
compatible with the Winter Soldier Biped. A direct mesh swap distorted fingers
in camera previews and was rejected.

Fitted derivatives use anatomical finger pivots from the source model, uniform
scale 3 and the existing wrist frames. Grip poses are fitted offline. Reload
finger motion follows the previous curves at 35% flexion, capped at 12 degrees
on the right / 25 on the left for the different proportions. This changes the
visual finger range while retaining its timing. Gun/magazine and wrist motion
are baked at 60 Hz with the original one-second duration.

`ViewmodelReload` still samples transform curves using the existing ammo timer;
the imported Animator stays disabled. Original reload clips have no animation
events; no gameplay callback is moved into the new clips. Soldier-pack source
references remain assigned. Normal holding and knife meshes remain rigid
snapshots, moved by the same recoil, switch and elbow-swing code as before.
No new shoulder/elbow/finger motion is claimed for ordinary holding or slashes.

## Integration and preservation

The installer applies visual overrides only to `Assets/Prefabs/Player/Player.prefab`:

- `GloveL/R` meshes now include their own source sleeves; old `SleeveL/R`
  renderers stay present but disabled to prevent overlap.
- Each gun has an inactive `WinterReloadVisual`. Its existing `ViewmodelReload`
  references that rig and retargeted clip; previous `ReloadVisual` instances stay
  inactive for rollback.
- Weapons, muzzle sockets, holder transforms, input, damage, ammo, camera and
  gameplay code are unchanged. Existing weapon prefabs still supply dropped
  guns with their original arm renderers hidden.
- Existing `ViewmodelHands` already collects inactive reload renderers and
  handles sprint/scope/cinematic visibility. The full-body actor, its model,
  controller, cutscene clips and all scenes are unchanged.

Restore the Player prefab from the preceding commit to roll back. All original
hands, gun FBXs, reload clips, textures and materials remain available. If using
the older reload builder, run the Winter installer last to restore current wiring.

## Rebuild

Close Unity first. Run in PowerShell from the repository:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python 'Art/Blender/Scripts/build_winter_arms.py' -- 'C:\GV , IS Assignment\Blackout-Island' 'D:\model\winter-soldierfreetexturedrigged' "$env:TEMP\blackout-WA1"
```

Then run **Blackout > Systems > Use Winter Soldier FPS Arms**. The installer
does not run Blender. It preserves nested instances on subsequent runs.
`Blackout.Editor.WinterArmsBuilder.BuildAndPreview` runs in a separate graphics
batch with `-previewOut` outside the repository. It captures nine normal/knife/
sprint poses and fourteen reload poses using the saved Player camera. Reload
captures use temporary BakeMesh proxies, since Editor rendering does not update
GPU skinning in the same way as PlayMode.

## Verification and manual review

Evidence is under `%TEMP%/blackout-WA1/`. Unity build/render exited 0
(`build-sleeve.log`); selected EditMode passed 33/33 and PlayMode 34/34, both exit
0 (`editmode-final3.log/.xml`, `playmode-final3.log/.xml`). No C# compiler errors
or warnings were found. Existing licence-token and Editor shutdown diagnostics
remain. Earlier experiments and failing tests are retained in the same folder:
the PlayMode helper initially inspected the inactive legacy magazine, and the
new mesh's elbow-rim test caught the need for extra source sleeve geometry.

Repeat extraction reproduces the same geometry, UVs, weights, bind matrices and
61 sampled animation poses for all four FBXs at 1e-6 precision
(`geometry-before.json`, `geometry-after.json`, `audit-*.log`). Repeat Unity
installation exits 0 and leaves the Player prefab and four model metadata files
byte-identical (`before-repeat.json`, `repeat-build.log`). A 1,151-file protected
baseline confirms only the intended Player prefab and three test files changed
among the checked original assets/code; downloaded/full-body sources, actor,
scenes, original weapons, textures and materials remain unchanged.

`WinterArmsTests` checks saved meshes/material, UVs, valid bone
bindings, finite deformation/return to rest and scope/cinematic visibility.
It also compares 61 samples of gun/magazine/wrist motion against original clips.
Existing reload, knife, scope and takedown suites exercise their lifecycle rules.

In Bootstrap: acquire both guns; fire, empty their magazines, then press R; hold/release
RMB during rifle reload; switch mid-reload; walk, sprint, crouch/prone and jump;
slash repeatedly; perform front/rear takedowns; pause, die/respawn, restore a
checkpoint and Restart. Check Console and ensure the FPS hands stay hidden
during cinematics and return afterwards.

Inspect finger/grip contact, cut forearm ends and magazine handling at your usual
Game-view aspect ratio. Selected tests and controlled renders do not certify
native mouse feel, perfect contact, all aspects or a full mission. The supplied
low-poly shape/texture and approximate inherited magazine handling remain visual
limitations. Nothing here is presented as a new paired motion-capture animation.

Five-line viva summary:

- Does: matches FPS arms to the existing cinematic Winter Soldier.
- Design: mesh derivatives and prefab overrides reuse existing execution layers.
- Alternative: a direct Biped-to-Generic swap distorted the fingers in review.
- Number: 60 Hz baking retains one-second reloads and the original ammo timing.
- Weakness: fitted grip/contact and reduced finger flexion need live visual review.
