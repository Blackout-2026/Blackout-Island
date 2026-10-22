# Player reload animations (RL1)

The active `Assets/Prefabs/Player/Player.prefab` has a fitted reload for the MP9 SMG and R-700 rifle. Emptying a magazine with spare magazines available starts
the existing automatic reload. `ViewmodelReload` shows the animated copy during that reload and restores the approved normal holding model afterward.
The knife keeps its existing swing. This step adds no manual reload key or new reload sounds.

## Source and fitting

The motion comes from Kevin Iglesias' installed Human Soldier Animations pack:

- SMG: `Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/AssaultRifle/HumanM@AssaultRifle_Reload01.fbx`.
- Rifle: `Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat/Rifle/HumanM@Rifle_Reload01.fbx`.

Those are full-body Humanoid clips, about 1.7 seconds long. They cannot directly deform the original static viewmodel meshes. The Blender source script copies
the current gun and arm geometry, adds forearm, wrist and finger bones, separates the magazine, fits the source hand motion to each gun and bakes transform
curves at 60 Hz. The result is a one-second Generic animation in each `SM_Reload_*.fbx`, matching both guns' existing `WeaponData.reloadTime = 1`.
The original pack, holding models, materials, UVs and credits are retained. Reload copies keep the original totals: SMG 9,000 triangles, rifle 8,568.
These are fitted derivatives of the pack clips; they are not ready-made first-person clips supplied by the pack.
The fitted grip turns the support palm toward the magazine and lowers the gun during reload to keep the source forearms' open sleeve ends out of view.

## Runtime and integration

- Each gun owns one `ViewmodelReload`, connected to the same `WeaponController` and `WeaponData` as its hitscan weapon. Only its `Visual` and `ReloadVisual`
  children swap. Muzzle effects and the gun's firing components stay in place.
- `LateUpdate` samples the fitted clip at `1 - ReloadRemaining / reloadTime`. The imported Animator is disabled: explicit transform-curve sampling follows
  the ammo clock without a separate controller, animation events, root motion or runtime IK. Ammo remains authoritative for firing and refill.
- Pausing freezes the ammo timer and pose. Switching away hides the reload copy; background ammo reloads continue under the existing rule. Switching back
  samples the current phase. Checkpoint restores use the restored ammo state; disabling the gun restores its normal visual.
- The existing `ViewmodelHands` collects inactive reload renderers too, so sprint and scope still control visibility. A rifle reload lowers the scope;
  continuing to hold RMB aims again after the magazine is ready. The current holding pose returns after reload.
- The new importer applies only to `Assets/Models/Custom/SM_Reload_*.fbx`. It overrides the common static-model animation restriction for these two rigs.
  No scene, assembly, package, input binding or project setting change is needed.

## Rebuild

The existing downloaded arm source must be available under `D:/model`, as for `build_viewmodels.py`. With the Editor closed:

```powershell
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python 'Art/Blender/Scripts/build_reload_viewmodels.py' -- 'D:/model' 'C:\GV , IS Assignment\Blackout-Island'
```

Then run Unity with `-batchmode -nographics -quit -projectPath "C:\GV , IS Assignment\Blackout-Island" -executeMethod Blackout.Editor.PlayerReloadBuilder.Build`.
The Editor menu is **Blackout > Systems > Build Player Reload Animations**. It updates only the managed reload children, component references and renderer
settings in the existing Player prefab. Re-running preserves their references and the other player content. `.blend` source stays under `Art/Blender`.

`Blackout.Editor.PlayerReloadBuilder.RenderPreview` runs in a graphics-enabled batch. It samples seven phases per gun through the saved player camera and
writes PNGs to `%TEMP%/blackout-RL1-preview/`. Because edit-mode Camera.Render does not refresh GPU skinning, this tool draws temporary BakeMesh copies of
the sampled rig. It saves no scene or prefab. These images inspect static pose samples, not gameplay or callback ordering.

## Manual checks before PR

1. Play Bootstrap, recover both guns, empty each magazine and watch the support hand remove/return the magazine. Ammo should refill once after one second;
   the original grip should return. Check at your usual Game view aspect ratio, while moving and crouching/prone.
2. Empty the rifle while holding RMB. The scope should lower for reload and return if RMB stays held. Release RMB during reload and verify it stays lowered.
3. Switch to the knife during reload, then back before and after completion. Sprint/stop and pause/resume mid-reload; no doubled arms or frozen pose.
4. Test no-spare empty guns, ammunition pickup, death/respawn, checkpoint restore and Restart. Check firing, muzzle effects, ammo HUD, dropped guns and Console.

## Verification and limits

Unity 6000.3.25f1 import/wiring, graphics preview and repeat build exited 0. Final selected tests passed **EditMode 92/92** and **PlayMode 12/12**, both exit 0.
The EditMode selection covers reload wiring/deformation, ammo, scope, viewmodels, loadout, drops, weapon prefabs and medkit/compass contracts. The PlayMode
selection covers both guns' real ammo timers, repeated reloads, switching away/back, pause, loaded/mid-reload checkpoints, empty/no-spare pickup recovery,
death/respawn, reload/scope recovery, existing scope lifecycle cases and medkit input. The pause test measures after LateUpdate has settled.

Fourteen static pose frames were captured. Eight camera frames from the actual runtime SkinnedMeshRenderers were captured and inspected, covering normal,
reload and returned views of both guns. Camera capture can stall a batch frame, so its filenames do not imply exact animation timestamps. This checks
rendering in a controlled test scene, not complete gameplay or subjective smoothness. Batch tests spend rounds through the controller-owned `WeaponAmmo`;
they do not simulate native captured LMB firing. Scope cases supply the capture flag to the same Tick method used by Update. Native firing/input, audio,
live feel, arbitrary aspect ratios and the Bootstrap checks above remain manual.

A repeated Blender export had identical signatures for geometry/weights rounded to 1e-6 and sampled bone matrices rounded to 1e-5. The Player prefab and
both new model metadata files were byte-identical after repeat Unity wiring. All three original weapon FBXs and all four gameplay scenes match their
pre-RL1 hashes. No final C# compiler warning/error or test exception was reported. Existing licensing-token and future-timestamp diagnostics remain.
Source/document whitespace checks passed; strict Git diff-check flags ten Unity-native empty YAML fields in the prefab. With only that end-of-line check
excluded for serialized YAML, diff-check exits 0; the serializer output is retained.

Evidence is outside the repository: `%TEMP%/blackout-RL1-{build,preview,repeat-build,editmode,playmode}.log`, test XML with matching names,
`blackout-RL1-preview/`, `blackout-RL1-runtime-preview/`, `blackout-RL1-geometry-{before,after}.json`, `blackout-RL1-before-repeat.json`,
`blackout-RL1-protected.json` and `blackout-RL1-blender{,-repeat}.log`.

## Viva summary

- Does: plays fitted pack reloads on the active player's two guns.
- Design: baked rigged visual copies follow the existing ammo timer; gameplay remains in the weapon system.
- Alternative: a runtime humanoid proxy and IK would add dependencies and another pose-solving layer.
- Number: source motion is retimed from about 1.7 seconds to the existing one-second reload.
- Weakness: fitted full-body motion and approximate skin weights need manual gameplay review at the player's aspect ratio.
