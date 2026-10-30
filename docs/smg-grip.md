# SMG grip and sight alignment (SG1)

The Player keeps its existing MP9, Winter Soldier arm meshes/material, camera,
weapon scripts, ammo clock and visibility gates. This step refits only the SMG
arm derivative and its visual pose. Rifle, knife and the full cinematic actor
retain their setup. Original meshes and the inactive legacy reload remain.

## Pose and mesh fit

The support hand now wraps the front face of the actual vertical foregrip,
returning its fingertips toward the rear where the first-person camera can see
them. This replaces the earlier side grip whose glove disappeared against the
receiver. Source wrist-to-middle-knuckle length is about 114 mm; the right hand
uses 80% (about 92 mm), and the bulkier left glove uses 75% (about 86 mm).
The wrist transition blends back to the original forearm with its existing
weights. UVs, topology, materials and weight mapping remain unchanged.
Source/full-body assets remain read-only. This is a SMG-specific fit.

`Art/Blender/SmgGrip.json` uses Unity metres, +Y up and +Z along the gun:

| Reference | Gun-local position |
| --- | --- |
| Rear pistol-grip contact | (0.022,-0.075,0.15) |
| Front foregrip contact | (0,-0.075,0.2842) |
| Right wrist | (0.027,-0.075,0.069) |
| Left wrist | (-0.05515,-0.11859,0.24061) |
| Sight reference | (0,0.05,0.065) |

Contact positions were checked against the actual mesh. The MP9 spans z 0.0347
through 0.33 m; its front vertical grip's front face at y=-0.075, x=0 is z=0.2842.
The left hand frame now follows the forearm direction exactly, rather than
turning sideways at the wrist. The palm's dorsal axis is projected perpendicular
to that direction. Its wrist is repositioned to keep the middle knuckle fixed at
the existing grip, and the authored finger targets remain on the foregrip.
Forearm direction is unchanged; the hand rotates into a neutral wrist position.
The saved-pose regression requires less than 5 degrees of left wrist bend,
measured elbow-to-wrist against wrist-to-middle-knuckle (about 50 degrees before
this correction). This uses anatomical points rather than bone-axis assumptions.
The right index remains near the trigger. The left sleeve's cropped elbow end
extends 0.18 m along its pole, blended only beyond 65% of the forearm length.
This carries the open cut behind/below the camera in wide ADS; wrist, palm and
finger anchors are unaffected. It retains the existing faces, UVs and weights.
`handScale` fits the palm/fingers;
`middleCurl`, `ringCurl` and `littleCurl` override the shared `curl` fallback.

`fingerTargets` now authors the finger fit directly on both actual grips. Each
finger stores three gun-local points: middle joint, distal joint and tip. The
source knuckle position stays fixed. The generator maps each phalanx from its
source rest transform to that target, retaining its cross-section while fitting
its length (bounded to 0.5-1.6 times the source phalanx), and applies those transforms through the existing vertex weights.
This is one offline skinning pass; targeted fingers skip the older pre-curl to
avoid deforming their skin twice. The fitted joints become the new rest pose
before the existing reload curves are retargeted. No bones are added or renamed.
The right thumb/index now curl at the rear grip/trigger instead of projecting
forward into the support fingers; the left pads sit around the foregrip faces.
Curl fields remain as the fallback for fingers without explicit target points.

The existing Blender script fits native joints/skin weights offline. Normal
holding uses static MeshFilters; there is no runtime IK or holding Animator.
`RightHandGrip`/`LeftHandGrip` are authoring references. Moving a marker alone
will not deform a static holding mesh. Finger posing assigns local rotations
and asserts the evaluated chain, avoiding stale parent matrices during baking.
No animation package or runtime mesh work is introduced.

The Generic reload retains the original Motion/Magazine curves and one-second
ammo clock, baked at 60 Hz. The right wrist follows Motion; the left blends from
its new support pose into the existing magazine motion over the first quarter
and returns during the last quarter. Existing finger-flex timing is adapted to
the fitted joints. `ViewmodelReload`, its callback behavior and explicit clip
sampling remain unchanged. The disabled Animator does not compete with IK.

## Framing and aiming

SMG hip position is (0.13,-0.05,0.14), Euler (-6,-10,0), under the existing
WeaponHolder. This brings the compact gun closer with a small inward/upward
hip cant, matching the supplied pose reference more closely. Camera transforms,
FOV, sensitivity, layers/culling and all other weapons are unchanged.

`SmgAimPose` aligns the sights with the camera axis at full RMB aim, with eye
reference distance 0.32 m and blend rate 14/s. WeaponController still owns parent
recoil, switching and shots. Hitscan direction, spread, damage and muzzle socket
remain unchanged. Both hands move with the SMG child pose. Reload, sprint,
pause, focus/cursor loss, death or disabled/cinematic-hidden controls cancel
visual aim; held RMB can resume when ready. No subscriptions are added.

## Rebuild and review

With Unity closed, from the repository in PowerShell:

```powershell
$env:SG_EXPORT_ONLY = 'Smg'
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python-exit-code 1 --python 'Art/Blender/Scripts/build_winter_arms.py' -- 'C:\GV , IS Assignment\Blackout-Island' 'D:\model\winter-soldierfreetexturedrigged' "$env:TEMP\blackout-SG1-finger-fit"
Remove-Item Env:SG_EXPORT_ONLY
```

This exports only `Assets/Models/Custom/SM_WinterArms_Smg.fbx` and the working
`Art/Blender/SM_SmgGrip.blend`. Run **Blackout > Systems > Apply SMG Grip And
Sights** after older player builders to reinstall this SMG-only configuration.

`BuildSmgAndPreview` renders hip/transition/ADS at 4:3, 16:9 and 8:3, plus reload
samples and control weapons. `RenderGripViews` renders front/left/right/top and
weapon-only comparisons under `%TEMP%/blackout-SG1-review/`. It records actual
mesh surface hits and static-versus-skinned bind comparisons. BakeMesh uses
scale compensation before applying the existing holder transform, preventing
double scaling in temporary review proxies (see [Unity BakeMesh](https://docs.unity.com/en-us/engine/6000.3/script-reference/unityengine/skinnedmeshrenderer/bakemesh)). These tools never save scenes.

`SmgGripTests` checks contacts against the actual weapon collider, distal finger
proximity, separated elbows, wrist/forearm alignment, static/reload-start mesh
agreement, right-grip attachment through reload, framing and ADS restoration.
It also checks that the distal sleeve end stays outside hip/ADS views at all
three aspect ratios. A separate guard keeps the trigger thumb/index at least
80 mm behind the support contact.
`SmgAimPlayTests` uses real Input System RMB with explicit batch cursor capture;
it covers recoil inheritance, reload/resume, switching, pause/focus and death.
With `-smgPreviewOut`, it saves live hip/ADS/reload captures at all three aspect
ratios. Selected reload/knife/scope/takedown regressions check affected consumers.

Latest neutral-wrist evidence is under `%TEMP%/blackout-SG1-straight-wrist/`.
`joints-before/after.json` records the anatomical points for the bend measurement.
Measured rest/holding bend is 50.16312 degrees before and about 0.00002 degrees
after. Final import/studio/side/forest renders exit 0 (`preview-final`,
`inspect-final`, `forest-final` logs). Selected EditMode 37/37 and PlayMode 35/35
pass, both process exit 0 (`EditMode-final`, `PlayMode-final` XML/logs). Selected
side, forest, live hip/ADS/reload captures were inspected. The original curves,
visibility gates and all other weapon poses remain covered by selected tests.
Repeated geometry/UV/weight/bind/61-pose signatures match at 1e-6 precision;
repeat install exits 0 with byte-identical prefab/importer metadata. Existing
SMG topology, UVs and weights match the committed version. The protected asset/
code baseline retains 3,113 of 3,119 hashes; only six intended existing files
change. Source/docs whitespace passes; native Unity prefab empty-name spacing
still flags git diff --check (exit 2). No C# compiler warnings/errors or runtime
reference errors found; existing licensing/graphics/shutdown diagnostics remain.
These are selected regressions and captures, not a whole-project/gameplay pass. The existing forest
capture tool still writes to `blackout-SG1-finger-fit/forest/`; it uses the saved
Environment lighting without saving the scene or running gameplay/input.
Live isolated PlayMode captures are written into the latest evidence folder.
The generated reference uses different glove/grip geometry; original cybernetic
materials remain. Final visual acceptance is separate from automated tests.

In Bootstrap, acquire the SMG and inspect standing, walking, crouching/prone,
jumping and sprinting at your actual Game-view aspect. Fire hip/ADS, empty a
magazine with RMB held, switch away/back during reload, pause, alt-tab,
die/respawn, restore a checkpoint, perform takedowns and Restart. Inspect finger
pads, wrists, sleeve ends and support-hand release/return in motion. Retained
low-poly geometry/materials limit how closely this can resemble the supplied
render; tests and selected captures do not certify perfect skin contact or a
complete mission.

Viva: a weapon-specific offline fit authors finger joints around the real grips;
the runtime visual component only aligns the sight under the recoil holder.
Separate grip contacts are about 13.4 cm apart along the MP9. Existing skin weights
blend the smaller palms into their sleeves. Reload uses the same ammo clock.
Visual contact during motion remains the main manual limitation.
