# Trigger finger motion (TF1)

Both equipped guns flex the right index finger on an accepted shot. The first
rendered firing frame always retains the pull, even if its frame time exceeds
the release duration. After firing, holding LMB keeps the trigger pressed;
releasing LMB returns the finger over 0.14 seconds. A held automatic trigger
therefore does not vibrate between individual bullets. Dry fire, reload
requests, pickups and knife swings cannot start a fresh pull.

The initial 85 ms pulse could be consumed by LateUpdate in the same slow frame
as firing, so a changed property/CPU bake did not guarantee visible motion.
This revision guards the firing frame and checks live rendered pixels as well.

`WeaponController.ShotFired` reports the firearm after its existing `Fire()`.
The separate `TriggerFinger` presentation component changes only the baked
`TriggerPull` blend-shape weight. It resets on switch, checkpoint restoration,
reload, pause, lost focus, hidden hands and disable; subscriptions are removed
on disable. The existing reload visual disables the normal holding Visual,
so its sampled Generic clip retains exclusive control of reload fingers.
No firing/recoil/damage/camera/input settings change.

## Mesh and prefab integration

The accepted static right arm MeshFilters remain intact. Their MeshRenderers
are disabled, and a child `GloveR/TriggerFingerVisual` renders the same vertices,
UVs, triangles and body material with one additional blend shape. One rigid
identity bone carries the holding mesh; no live finger skeleton or runtime mesh
allocation is needed. The left arm, wrists, palm, sleeves and other fingers do
not change. ViewmodelHands collects the new renderer through its existing
sprint/scope/cinematic visibility gates. Knife and full-body actors are unchanged.

`Blackout > Systems > Add Trigger Finger Motion` rebuilds only two mesh assets
and their Player overrides via Unity APIs:

- `Assets/Models/Custom/SM_TriggerFinger_Smg.asset`
- `Assets/Models/Custom/SM_TriggerFinger_Rifle.asset`

The builder reads the accepted `SM_WinterArms_Smg/Rifle.fbx` meshes and their
existing Index2R/Index3R pivots/weights. It bakes joint flexion toward the rear
of the gun (-Z), then stores the before/after vertex and normal deltas. The
SMG uses 8/5 degree joint rotations (maximum travel 5.172 mm); the longer fitted
rifle finger uses 2/1.5 degrees (5.886 mm). The base meshes, reload rigs/clips,
FBXs, Blender sources, grip targets and original body remain untouched.
Rebuilding older arm/player tools may restore the static renderer; rerun this
scoped installer last. No Blender extraction or new package is required.

## Verification

Unity 6000.3.25f1 revision build exited **0**. Selected EditMode **74/74**
and graphics-enabled PlayMode **43/43** passed, both exit **0**. The live GPU
hand-renderer check measured **818 changed pixels** initially (**809** on the
final focused rerun) between rest and held
trigger, excluding camera movement and muzzle lighting. Its rest/held/released
silhouettes were inspected. Tests also verify held/released input, a 250 ms
firing-frame step without held input, both guns auto-reloading from a last
actual shot without R, and the existing reload/visibility/checkpoint consumers.
Evidence: `%TEMP%/blackout-TF1-revision/` contains `build.log`,
`EditMode.log/xml`, `PlayMode.log/xml`, `live-finger/`, and runtime captures.
Final focused graphics rerun: **5/5** passed, exit **0**, in
`TriggerFinger-final.log/xml`. Builder repeat exited **0**; the Player prefab
and both generated mesh/meta pairs were byte-identical (`repeat-result.json`).
Selected first-person held/rest captures under `runtime/` were inspected.
The production finger is small and partly occluded by the weapon, so perceived
visibility/trigger contact at the actual Game-view resolution remains manual.

The previous selected 74 EditMode / 41 PlayMode checks and baked previews remain
under `%TEMP%/blackout-TF1/`, but the user reported no visible firing motion.
Those earlier checks did not establish a visible live-rendered finger change.
The new graphics test compares the actual hand renderer before a shot, while
LMB is held after the shot, and after release. It temporarily uses a plain
unlit silhouette on a disposable Player instance; production material/assets
are unchanged. Original geometry/UVs/weights are still checked in EditMode.
These are controlled captures/selected tests, not complete gameplay acceptance;
input tests supply captured-cursor state explicitly.

Manual Bootstrap checks: acquire both guns, fire single shots and sustained
SMG bursts, and inspect finger movement at the trigger and return without
wrist motion or duplicate arms. Check ADS, dry fire, automatic empty reload,
R partial reload, switching,
sprint, pause/focus loss, death/respawn, checkpoint restore and Restart. During
reload inspect the existing hand motion and clean return to the holding pose.
The trigger mesh itself remains static; this change animates the index finger.
