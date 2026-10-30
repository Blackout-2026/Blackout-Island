# Player reload animations (RL1)

WA1 uses retargeted Winter Soldier arm derivatives in the active Player while
retaining these original RL1 models, clips and source references. See
[matching FPS arms](winter-fps-arms.md) for the current installer and limits.

The active `Assets/Prefabs/Player/Player.prefab` has a fitted reload for the MP9 SMG and R-700 rifle. A gun with spare magazines starts
a reload only after the player presses **R** (MR1). `ViewmodelReload` shows the animated copy during that reload and restores the approved normal holding model afterward.
The knife keeps its existing swing. MR1 adds the manual reload key; no new sounds or animation assets are added.

## Manual reload (MR1)

- Press **R** once to reload the equipped SMG or rifle, whether partially loaded
  or empty. A full magazine, no spare magazine or an existing reload rejects it.
- Emptying a gun and picking up spare magazines both wait for a fresh R press.
  Holding R does not repeatedly reload or queue a request through a blocked state.
- The existing inventory counts whole magazines. On completion one spare replaces
  the current magazine; remaining rounds in a partial magazine are discarded.
  Loaded/reserve counts change on completion, exactly once. Firing is blocked
  during the existing one-second reload.
- Starting a reload requires captured, enabled player controls, normal time flow,
  a ready weapon, and no sprint/takedown. An already requested reload keeps the
  existing background-switch behavior and checkpoint/pause/death handling.
- `PlayerInputReader` reads the existing Player action map's `Reload` Button
  bound to `<Keyboard>/r`. `WeaponController` handles the request before firing;
  `WeaponAmmo.TryStartReload` owns validation/timing. `AmmoChanged` updates the
  HUD at start and completion. ViewmodelReload and both sight systems read the
  same ammo state, so their animations and release/resume behavior are reused.
- **Blackout > Systems > Add Manual Reload Input** (batch method
  `Blackout.Editor.PlayerReloadBuilder.BuildManualReloadInput`) updates only the
  assigned input source through the Input System JSON API, preserving existing
  IDs and the importer GUID. It is safe to rerun; it does not rebuild the Player
  or overwrite the accepted hand poses. The R binding is already installed.

MR1 verification on Unity 6000.3.25f1: compile/input import exited **0**;
selected **EditMode 72/72** and **PlayMode 38/38** passed, both exit **0**.
The selections cover ammo/HUD rules, the saved R binding, actual Input System
keyboard events for both guns, one-press/held-key behavior, control/capture/pause,
sprint/switch gates, pickup, existing reload lifecycle, SMG ADS, sniper scope,
knife and takedown behavior. Existing action IDs, bindings and control schemes
were unchanged; only the Reload action/binding was added. Repeating the input
builder exited **0** and left the input source byte-identical.
Evidence is outside the repository under `%TEMP%/blackout-MR1/`:
`import-input.log`, `input-repeat.log`, `input-repeat-result.txt`,
`EditMode.log/xml` and `PlayMode-final.log/xml`.
These are batch/headless checks; tests supply captured-input state explicitly.
Native window focus/cursor capture and visual gameplay still require the manual
checks below. No arm, weapon, animation, prefab or scene assets changed for MR1.
Historical RL1 verification below describes the original automatic-reload rule.

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

1. Play Bootstrap, recover both guns, empty each magazine and confirm it stays empty. Press R and watch the support hand remove/return the magazine. Ammo should refill once after one second;
   the original grip should return. Check at your usual Game view aspect ratio, while moving and crouching/prone.
2. Press R on the partly loaded rifle while holding RMB. The scope should lower for reload and return if RMB stays held. Release RMB during reload and verify it stays lowered.
3. Switch to the knife during reload, then back before and after completion. Sprint/stop and pause/resume mid-reload; no doubled arms or frozen pose.
4. Test no-spare empty guns and ammunition pickup: pickup should wait for a fresh
   R press. Check partial magazines, a full magazine and the knife; holding R
   must not repeat. Try R during sprinting, switching and pause: it must not
   queue a reload. Test death/respawn, checkpoint restore and Restart. Check
   firing, muzzle effects, ammo HUD, dropped guns and Console.

## Historical RL1 verification and limits

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
