# SMG thumb contact and trigger finger (TF1 / TF2)

TF2 revises the SMG support thumb and right index finger. The thumb wraps around
the back of the actual foregrip to its opposite side; the index pad sits on the camera-facing
side of the trigger. Only these fingers change. Wrists, sleeves, support fingers,
weapon/camera framing and the full-body actor remain unchanged.

The rejected straight/raised-finger fit changed phalanx lengths. The current
Editor builder instead rotates the three existing finger joints using a small
CCD solve. Knuckles remain fixed; each bone length is checked before/after.
The index solver limits deviation to 40 degrees at the base and 65 degrees
for its remaining segments. Its surface contact is refined through rotations.
The thumb uses a complete wrap arc: left back corner, across the back face,
then right back corner, preserving every source segment length. A tip-only
contact solver was rejected because it left the middle joint upright. Neither
fit stretches bones or pulls individual vertices.
Source UVs, weights, topology, materials and original FBXs remain available.
The rifle holding pose retains its original geometry.

## Mesh wiring and contact spaces

The source rigs/meshes are read-only Winter Soldier derivatives. Native bind
positions come from inverse bind poses; contact rays use gun-local metres.
The builder explicitly converts between the static holding mesh and gun spaces.
The SMG trigger ray passes inside its guard at y=-0.036. The thumb ray points
forward from z=0.21 at y=-0.055 to measure the foregrip back face (z=0.246733). Measured contacts are saved
as RightIndexContact and LeftThumbContact under the gun.

Existing right-hand static renderers remain disabled for rollback; the separate
TriggerFingerVisual renders the existing trigger blend-shape mesh. The left
GloveL keeps its MeshRenderer/body material but uses the new static derivative
Assets/Models/Custom/SM_SmgThumb.asset. Its original mesh remains in
SM_WinterArms_Smg.fbx. Only Thumb1/2/3 weights affect the left derivative.
No runtime IK, new skeleton, package or new FPS controller is added.

Blackout > Systems > Wrap SMG Support Thumb (BuildThumbOnly) updates only
the left thumb mesh and its Player override. BuildThumbAndPreview also saves
review frames. The right trigger assets/runtime are hash-checked unchanged
by this focused correction.

Blackout > Systems > Add Trigger Finger Motion rebuilds the existing generated
meshes and Player overrides through Unity APIs. It updates native mesh buffers
and reimports before wiring; it preserves existing GUIDs. Run it after older
player/arm builders. BuildAndPreview creates disposable review cameras and
writes hip/side/front rest and pulled images outside the repository.

## Shot and reload behavior

Each accepted firearm shot pulls the finger, preserves the first rendered frame,
then returns to rest even while LMB remains held. A subsequent automatic SMG
shot retriggers the cycle. Release duration is min(140 ms, 85% of cooldown).
The SMG uses a small 11/7-degree distal pull (12/8 until GN1); the rifle retains its small pull
and unchanged holding geometry. Dry fire and knife attacks start no cycle.
WeaponController still owns fire rate, recoil, bullets and ammo. Only the visual
blend weight changes during gameplay; no per-frame mesh baking is used.

Automatic empty-magazine reload and R partial reload remain as committed in TF1.
The original Generic reload clips/rigs remain untouched and own the hands while
reload is active. Switching, hidden/sprint/scope/cinematic hands, pause/focus,
checkpoint and disable reset the finger effect without accumulating listeners.
Check the release/regrip transition into the corrected holding thumb manually.

## Verification

Final evidence is outside the repository at %TEMP%/blackout-TF2-thumb-wrap/.
Compilation/graphics build exited 0. Selected EditMode 75/75 and graphics
PlayMode 43/43 passed, exits 0. The saved thumb distal centre is
(0.015157,-0.059497,0.245281): opposite the original left-side base and across
the measured back face. Its thumb bounds are wider than tall; the regression
checks both this shape and unchanged non-thumb vertices/UVs/triangles.
The builder rejects any change to thumb segment lengths. Side, front and
complete-gun gameplay-camera previews were inspected. Right-hand mesh/runtime
hashes remain unchanged by this correction. Files: build.log, EditMode.log/xml,
PlayMode.log/xml, preview/, thumb-chain.txt, runtime/ and repeat-result.json.
Repeat exited 0; the Player prefab and all three mesh/meta pairs were byte-identical. Earlier trigger-motion evidence
under blackout-TF2-natural remains valid for the preserved right-hand effect.

Inspect in Bootstrap at the actual Game-view resolution: thumb against the
foregrip; index visible on the trigger; single shots and sustained SMG bursts;
ADS; automatic/R reload and regrip; switch/sprint/pause/focus; death/checkpoint/
Restart and Console. Controlled captures and selected tests do not establish
full gameplay feel, native pointer capture or subjective visual acceptance.

Historical TF1 evidence remains under %TEMP%/blackout-TF1-revision/. Earlier
TF2 fitting evidence under blackout-TF2/ includes the rejected stretched pose;
its submillimetre contact result did not establish a natural-looking finger.
