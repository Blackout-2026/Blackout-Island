# The IOER-32G submachine gun (step GN1)

The player's SMG is now the downloaded **IOER-32G** (`D:/model/ioer-32g`, folder `source/model.zip` and `textures/`), a compact SMG with a hooked handguard,
a long extended stock and a red-dot optic. It replaces the MP9. Weapon code, ammo, damage, the muzzle point, the recoil holder, the reload clock and
the other weapons are unchanged; this step swaps the model and re-fits the hands to it. The earlier arm re-pose (SM1, [smg-grip.md](smg-grip.md)) is
folded into the same commit: its slimmer forearms and tucked elbows are used here too.

## The download and what was done to it

| | Download | In the game |
| --- | --- | --- |
| Format | COLLADA, 3 meshes, 18 MB; Blender 5.2 has no COLLADA importer, so `build_ioer_smg.py` reads the XML itself | `Assets/Models/Custom/SM_Weapon_Smg.fbx` (the same file and object names as before) |
| Triangles | 92,519 + 47,076 + 1,280 | gun 4,312 (the optic is 912 of them, plus a small mount block) + magazine 450 |
| Size | about 1,600 "units" long (not scaled by its author) | scale 0.00036: 0.577 m with the stock |
| Maps | three materials x five 4096 maps | one 2048 atlas of three 1024 tiles: `T_WeaponSmg_BaseColor.png`, `_Normal.png`, `_MetalSmooth.png` (R metallic, G occlusion, A smoothness), so the gun keeps its one material `M_Weapon_Smg` |

Frame (as before, docs/art-pipeline.md 14.6): origin at the receiver, +Z (Unity) toward the muzzle at **0.33 m**, bore axis at **0.004 m**.

- **The whole gun is kept; the stock is not cut.** An earlier version cut the stock (0.12 m, then 0.09 m behind the receiver) and capped it; in the game
  that looked like a boxy stump, so the cut is off (`STOCK_CUT_Y = None`; the option remains in the converter). The hip pose then sat 0.45 m ahead of the
  camera to show the whole gun, but that was too far; it was then tried at 0.32 m and 0.24 m. It is now 0.27 m ahead: the gun is large and close, and the rear
  half of the stock runs off the right edge of the view, which is accepted. (The camera's near clip is 0.08 m and the holder is scaled 0.85, so below about 0.34 m
  the stock end is clipped; at 0.24 m the clip showed the stock's hollow inside as a dark panel, so 0.27 m is the closest that looked acceptable.) The aim pose uses
  `eyeDistance` 0.31 m (the MP9's was 0.32).
- **The red dot is kept.** Its lens centre is 0.087 m above the origin and its rear window at z 0.054 m; `AimReference` is at (0, 0.087, 0.064), so
  right-mouse aiming centres the dot on the screen.
- The thin riser under the optic collapsed when thinned with the gun, and the source hovers the optic about 1 cm above the rail, so the optic is thinned
  on its own and a small block now bridges it to the rail.
- The **magazine** is split out (`Smg_Magazine`) so the reload can drop it. The static viewmodel mesh `Smg` is the gun and magazine joined, as before.
- Hip pose: `SmgGrip.json` `hipPosition` (0.105, -0.075, 0.27), rotation (-5, -8, 0). The gun is longer than the MP9 and its pistol grip is further back, so
  it sits 0.13 m further forward than the MP9 did.

## The hands

The MP9 had a vertical foregrip 13 cm in front of its pistol grip. This gun's support hand cups a flat handguard 21 cm in front of it, so the old
hand-typed finger joints no longer apply. `Art/Blender/Scripts/grip_fit.py` computes them from the gun's mesh:

1. each finger lies in a plane through its knuckle; the mesh is cut with that plane and the cut points of the gripped part are kept;
2. their convex hull, grown by the finger radius (7.2 mm) with rounded corners, is the line the finger's centre follows;
3. from the knuckle the three phalanges keep their real lengths: joint 2, joint 3 and the tip are the next points on that line at those distances. Where a
   straight bone would cut through a corner of the grip, its joint stops short and the bone is shortened by at most 30%;
4. the thumb follows a straight line along the surface.

A knuckle too far from its outline stops the build with a message, so a badly placed hand is caught. `SmgGrip.json` now holds, per hand, the frame
(`back`, `fingers`), `elbowDirection`, the marker `contact`, `forearmScale` and a `fit` block: where the middle knuckle goes (the wrist is derived from it),
the slice type and region, the finger radius, and the thumb line. The old `fingerTargets` lists are gone.

| | Right hand | Left (support) hand |
| --- | --- | --- |
| Grips | pistol grip: horizontal slices, fingers wrap from the right-back corner round the front; the index goes to the trigger | handguard: slices across the barrel, palm up under it, fingers wrap under and up the right side, thumb along the left face |
| Middle knuckle (gun frame, x / up / forward, m) | 0.028, -0.048, 0.000 | 0.010, -0.026, 0.225 |
| Elbow direction | (0.5, -0.7, -2.0) | (-0.4, -0.55, -0.73): the forearm rises from below and behind, so it is short in view |
| Forearm slimming (`forearmScale`) | 0.82 | 0.70 |

The first left-hand version (fingers pointing sideways, forearm coming in from the left) showed as a huge forearm crossing the screen with no visible hand,
and was changed to the above: the hand frame is tilted up 16 degrees so the wrist stays within 18 degrees, and the forearm comes up from below.

`TriggerFingerBuilder` found the trigger blade at z 0.041 m (ray from inside the guard, as the MP9's was) and the thumb contact on the handguard's left face
(x -0.022 m); its thumb guides now run forward along that face.

## Rebuild

```powershell
$env:IOER_WRITE = '1'
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python 'Art/Blender/Scripts/build_ioer_smg.py' -- 'D:\model\ioer-32g' 'C:\GV , IS Assignment\Blackout-Island' "$env:TEMP\blackout-GN1\ioer-review"
Remove-Item Env:IOER_WRITE
$env:SG_EXPORT_ONLY = 'Smg'
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python 'Art/Blender/Scripts/build_winter_arms.py' -- 'C:\GV , IS Assignment\Blackout-Island' 'D:\model\winter-soldierfreetexturedrigged' "$env:TEMP\blackout-GN1"
Remove-Item Env:SG_EXPORT_ONLY
```

Without `IOER_WRITE` the first command writes only review pictures and a report to its last argument, so the shapes can be checked first. Then in
Unity run **Blackout > Systems > Apply SMG Grip And Sights** and **Add Trigger Finger Motion** (it also rebuilds the support thumb). The converter writes
`Art/Blender/SM_IoerSmg.blend`, the shared source of the gun and magazine that the static viewmodel export and the reload rig both read, so the three
places that show the SMG always have the same geometry. The legacy `build_viewmodels.py` still builds the MP9; run the converter after it.

## Reload (made for this gun)

The MP9's baked reload sent the left hand to a magazine that this gun does not have, and the magazine never visibly left it. `build_winter_arms.py`
now bakes a reload for the IOER-32G (`bake_ioer`; its numbers are under `reload` in `SmgGrip.json`). It runs one second at 60 Hz, like the original,
so the ammo clock and `ViewmodelReload` are unchanged:

| Time | What happens |
| --- | --- |
| 0.00 - 0.15 | the gun tilts (8 degrees up, 14 degrees right) about the pistol grip so the magazine well opens toward the left hand |
| 0.00 - 0.14 | the left arm moves from the handguard to the magazine (the fingers open slightly, then close round it) |
| 0.14 - 0.34 | the magazine slides out and down along its own axis (0.18 m), the hand carrying it |
| 0.34 - 0.46 | the hand takes it away below the view (0.42 m), drifting left |
| 0.46 - 0.70 | a magazine comes back up from below, the hand pushing it, and seats (a 3 cm final push) |
| 0.74 - 0.92 | the hand returns to the handguard; the tilt eases out from 0.82 |

The left arm moves as one rigid piece and the right arm follows the gun, so no skin is stretched. Frame 0 and the last frame equal the approved hip pose.

## Right-click red-dot view

Right mouse looks through the red dot. While the SMG's aim pose is fully raised, `RedDotScope` narrows the camera from 72 to 58 degrees, takes the normal
crosshair out, hides the gun and arms (you look through the optic, not at it) and `RedDotOverlay` draws a black surround with a round clear window
(radius 36% of the viewport height, soft edge, a faint red tint on the glass and a thin red ring) and the red dot with its glow at the centre. Because
it reads `SmgAimPose.Blend`, everything that already cancels the aim (reload, sprint, pause, focus or cursor loss, death, switching, cinematics) also ends
the view. `ViewmodelHands` has a new `SetSightHidden` flag, kept apart from the sniper's scope flag, so hiding the gun does not cancel the aim that caused
it (`SmgAimPose` now reads `ShownIgnoringSight`). While the aim is down `RedDotScope` touches nothing, so the rifle's scope keeps its own FOV and HUD.
Run **Blackout > Systems > Build Red Dot Scope** to (re)wire it on the Player prefab.

## Tests changed

- `SmgGripTests`: the support-hand marker is checked to lie inside the handguard (rays in from both sides hit within 3 cm) instead of on a foregrip's front
  face; wrist bend under 25 degrees and elbows 0.07 m off the gun axis (from SM1).
- `WinterArmsTests`: the support thumb must lie along the handguard's left face, pointing forward (was: reach the far side of a foregrip); the SMG's reload is
  compared with the original clip only at its first and last frame (the rifle's whole motion is still compared).
- `RedDotScopeTests` (new): saved wiring, the overlay's real mesh at three aspect ratios (clear window, opaque surround, red dot at the centre) and the
  sight-hidden flag.
- The SMG trigger pull is 11/7 degrees (was 12/8): the longer index finger moved a vertex 12.2 mm against the 12 mm limit.

## Verification (2026-11-04, Unity 6000.3.25f1, Editor open)

- Blender conversion and arm build exit 0 (system Temp `blackout-GN1`: logs, review pictures, `extraction.json` with each finger's knuckle, reach and joints).
  The converter's review mode, then its write mode, were run before the arm build.
- Isolated renders from the saved Player prefab were inspected: hip and aiming at 16:9 and 8:3, orthographic left, right, top and front views of the gun with
  both hands, reload frames from 0.10 to 0.85 (frame 0 matches the hip pose), and the red-dot view with and without the overlay. At the hip the gun fills the lower
  centre and the rear of the stock leaves the view at the right edge; the red dot is centred when aiming.
- EditMode: Player assembly 259/260 (the failure is the unrelated untracked cinematic `IntroLoadoutTests`; the five new `RedDotScopeTests` pass); Art Editor
  assembly 311/311, which includes the model budget (the SMG viewmodel file is under the 10,000-triangle target) and the weapon prefab checks.
- PlayMode: `RedDotScopePlayTests` 1/1, `SmgAimPlayTests` 1/1, `PlayerReloadPlayTests` 7/7, `SniperScopePlayTests` 6/6 (the rifle's scope is unaffected by the hands
  change), `TriggerFingerPlayTests` 4/5. The failing one, `RealFireInput_PullsOnlyEquippedGun_AndDryFireDoesNotPull` ("each shot returns even while LMB
  remains held"), is a real-time (0.2 s) frame-timing check that failed the same way before this gun existed, in an Editor that was not the focused window.
- Not run: the full PlayMode and other EditMode assemblies, and anything in a focused interactive Game view.

## Limits

The hands are placed by a solver and checked in static renders; live motion (walk, sprint, crouch, fire, reload, support-hand release) has not been
inspected. The old `ReloadVisual` rollback rig and `SM_Reload_Smg.fbx` still hold the MP9 and its reload. The download's author and licence are unknown
(`docs/credits/core-developer.md`). Dark in-game lighting was not judged.

Viva: **does** swaps the SMG to a downloaded model and re-fits the hands; **design** one shared gun source for every place it appears, and a solver that
walks each finger round a slice of the real mesh at its true bone lengths; **alternative** rejected hand-typed joint lists and an IK rig; **number** 140,000
-> 4,762 triangles, 0.577 m, 1024 tiles in one 2048 atlas; **weakness** only static renders were inspected.
