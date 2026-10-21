# Sniper hold-to-scope (SC1)

Hold the right mouse button with the R-700 equipped to look through its scope. Release it to zoom out. The MP9 and knife keep their normal view.
Left mouse still fires through the player camera while scoped. Ammunition, recoil, damage, noise, ownership and gun poses use the existing systems.

The prototype supplies the defaults: normal 72-degree vertical FOV, scoped 22 degrees, look sensitivity multiplied by 0.35, damping rate 14 per second,
and the circular mask appearing above a 0.85 zoom blend. The aperture radius is 34% of the viewport height, clamped to fit narrow views.
`SniperScope` stores its rifle asset reference and zoom/sensitivity settings on the Player prefab. It captures the normal camera FOV rather than overwriting
the player's movement settings. The ring is a simple mesh edge rather than the prototype's blurred CSS shadow; walking speed is unchanged.

## Connected flow

- `PlayerControls.inputactions` supplies a Button action named `Aim`, bound to `<Mouse>/rightButton`; `PlayerInputReader.AimHeld` reads its held state.
- `SniperScope` runs after the input reader and before `PlayerLook`. It applies the FOV and a separate transient look-sensitivity multiplier.
  Its per-frame `Tick(dt, mouseCaptured)` accepts the capture state; normal Update always supplies the real `Cursor.lockState`.
- Once zoomed in, `ScopeOverlay` draws a black surround, clear circular aperture, fine reticle and red centre marker on the existing HUD canvas. A CanvasGroup
  hides the normal crosshair without interfering with the crosshair's death/respawn activation rules.
- `ViewmodelHands` combines the scope-hidden flag with its existing sprint rule. It hides renderers, leaving weapon scripts and hit rays active. This also
  hides the muzzle-flash quads during full zoom; the shared short-lived muzzle light still works.
- Switching starts, weapon changes/checkpoint restoration, death and disabling the scope reset its transient state. Input/cursor/focus loss, pause and sprint
  also reset the FOV, sensitivity, mask and scope visibility request. The LateUpdate guard handles state changes later in the frame before rendering.

No new event channels, cross-scene references, packages, global settings or AI changes are needed. The scope is not checkpoint inventory: a restore clears it,
and a held right button may aim again when the rifle is ready. Reloading retains the held scope; the existing ammo system controls whether the gun can fire.

## Rebuild and review

With the Editor closed, run Unity in batch mode with `-executeMethod Blackout.Editor.SniperScopeBuilder.Build`, or use **Blackout > Systems > Build Sniper Scope**.
The builder preserves the existing Player hierarchy, weapon visuals, manually authored content, prefab references and input action/binding IDs. It manages only
the scope component, `HUD/SniperScope`, the crosshair visibility group and the Aim action. Existing scope tuning is retained on re-run.
The installed Input System's JSON serializer adds its normal version/default-priority fields when saving the action asset.

`Blackout.Editor.SniperScopeBuilder.RenderPreview` runs only in a graphics-enabled batch and writes normal/scoped static views of the saved Player camera and HUD
to `%TEMP%/blackout-SC1-preview/`. It uses temporary targets and never saves a scene. This checks static framing, not live gameplay or real mouse feel.

Before opening the PR, use Bootstrap to acquire both guns and test:

1. Hold/release RMB on the rifle several times; zoom should ease in/out and the mask, reticle, crosshair and gun should swap cleanly.
2. Fire through the scope, including an empty magazine's automatic reload. Check recoil, hit alignment, ammo, audio and the return to the approved gun pose.
3. Hold RMB while switching to/from the MP9 and knife. Only the ready rifle should zoom. Test keyboard slots and the wheel.
4. Sprint/stop, crouch/prone, pause/resume, alt-tab, die/respawn and Restart; no stuck FOV, sensitivity, scope mask or hidden gun should remain.
5. Inspect the circular aperture and HUD at 16:9 and your usual Free Aspect size; check the Console.

## Verification and limits

Unity 6000.3.25f1 builder/import and repeat builds exited 0; the saved Player prefab and input asset were byte-identical on repeat. All four gameplay scenes
and the three weapon FBXs match their pre-SC1 hashes. Existing input actions/bindings retain their IDs and values; only Aim and native JSON defaults are added.
The graphics-enabled static preview exited 0 and both images were inspected, including the rendered black mask, clear aperture, reticle and zoom.
Final selected checks: EditMode **58/58**, PlayMode **7/7**, both exit 0; no final C# compiler errors/warnings or test exceptions were reported.

`SniperScopeTests` checks saved wiring, the held RMB binding and the actual Graphic rebuild geometry at four aspect ratios; existing viewmodel, gun-drop,
loadout and medkit/compass tests provide affected-contract coverage. The five scope PlayMode cases exercise real mouse action events, release, weapon/sprint
guards, disabled input/focus/pause, repeated re-enabling, checkpoint changes, real death/respawn, and the scoped rifle's diagnostic shot ray. Two existing medkit
input tests check coexistence with the added action. Batch Unity cannot maintain native cursor capture, even with graphics enabled: these PlayMode cases pass
captured input explicitly to the same `Tick` method that gameplay Update calls, and dispatch switch/diagnostic-shot requests directly. Native mouse capture,
look feel, ordinary LMB firing/ammo accounting and live frame ordering therefore remain manual checks; no complete-gameplay pass is claimed.

Evidence is outside the repository: `%TEMP%/blackout-SC1-{build,editmode,playmode,preview}.log`, corresponding test XML, `blackout-SC1-preview/`,
`blackout-SC1-before-repeat.json` and `blackout-SC1-protected-before.json`. Earlier fixture overload/cursor probes and the first unrendered-mask preview are
retained with `-first`, `-second` or `-probe` names. The visual check caught and fixed Graphic's legacy mesh-generation default; the aperture test now uses
the actual rebuild path. Existing licensing and future-timestamp diagnostics remain; the first preview also logged Pipeline-service shutdown errors after its
capture. Unity's native empty YAML fields retain their serializer spacing.

## Viva summary

- Does: aims the sniper through a zoomed circular scope for as long as RMB is held.
- Design: separate camera/HUD effect; reuse the input reader, weapon state and viewmodel visibility layer.
- Alternative: moving the gun model into the camera or disabling its holder would disturb firing and the approved pose.
- Number: 22-degree FOV and 0.35 sensitivity come from the read-only prototype weapon definition.
- Weakness: a static overlay with open-hand meshes, not a render-texture lens or animated aiming rig; live feel and arbitrary aspect ratios need manual review.
