# Prototype gameplay audio (AU1)

The Unity game uses clips generated from the synthesis recipes in the read-only
`prototype/src/audio/audio.js`. No third-party audio pack is downloaded. The
prototype has no sound files: Python bakes its oscillator frequencies, noise
filters, exponential envelopes and layered effects into **41 mono PCM16 WAVs at
22,050 Hz** in `Assets/Audio/Prototype`.

## Rebuild

1. Run `python tools/generate-prototype-audio.py` from the project root.
2. In Unity, run **Blackout > Systems > Build Prototype Audio**. This configures
   the bank/importers and patches existing prefabs through Editor APIs. It keeps
   GUIDs, prefab variants, manually authored content and other event listeners.
3. Run `python tools/generate-prototype-audio.py --verify` to compare every WAV
   against the deterministic bake without writing. Seeds are fixed per cue.

Unity generates all new metadata. The builder copies the existing blackout
volume bounds from Environment into the player's audio settings, then closes
the scene without saving. There are no persistent cross-scene references or
new assembly dependencies. Rerun it if those layout volumes change.

## Connections

| Sound | Gameplay connection |
| --- | --- |
| Player SMG, rifle, knife | Existing Noise channel; use actual equipped weapon data for the player's gun |
| Patrol, Hunter, robot, Commander shots | Existing AgentShooter Gunshot noise plus each prefab's ActorAudio identity |
| Footsteps, jump, landing | Existing PlayerMotor C# events; crouch/prone quieter, sprint louder |
| Player hit/death and enemy hit/death | Existing shared Health callbacks; fatal damage does not also play a hurt vocal |
| Hit confirmation and kill | Existing WeaponController.HitConfirmed |
| Weapon switch | A small SwitchStarted event when the existing switcher accepts a request; initialization/recovery do not emit it |
| Doors and locked use | Existing SlidingDoor UnityEvent hooks; travel duration selects the heavy or normal slide recipe |
| Generator interaction ticks | Existing onHoldSpark hook on the child console |
| Generator hum | Existing HumAudioSource and GeneratorVisuals power/recovery callbacks |
| Shutdown and emergency alarm | Existing GeneratorDisabled channel; alarm starts after 2.5 seconds |
| Weapon, health, stored medkit, ammunition pickups | Existing pickup hook, or the equivalent added UnityEvent for the two walk-over drops |
| Cure radio alert and completion | Existing CureTaken and MissionComplete channels |
| Crate scrape | Existing Crate NoiseEvent |
| Waves, wind, room tone and insects | Position-based blend using the established Unity coordinates and copied lighting volumes |

The four enemy prefabs now all have a gunfire channel assigned. Existing hearing
listeners can therefore hear the shots that previously had an empty channel;
audio playback itself never publishes another NoiseEvent. AI brains and
AgentIntent execution remain unchanged.

## Playback and lifecycle

`GameplayAudio` lives on the existing player prefab. It owns **32 reusable
one-shot AudioSources** (four reserved for local feedback, 28 for world sounds)
and four ambience loops. Its voice root stays stationary in the same scene,
so a shot's tail stays where it happened when the player moves. A full group
reuses a voice instead of instantiating another effect. Sources have no Doppler
shift and positional sounds use logarithmic attenuation to 120 m. There is
still only the existing camera AudioListener.

Subscriptions follow enable/disable. Disable stops owned voices/loops; destroy
removes the runtime voice root. Pause suspends gameplay voices and ambience;
the completion jingle can still play while the result screen pauses the game.
The generator's existing AudioSource is also paused/resumed with gameplay.

The service implements the existing checkpoint contract: saved blackout/alarm
state returns, failed-attempt effects stop, and restore does not replay cues.
The generator's existing checkpoint implementation restores/stops its own hum.
A scene Restart recreates the service. Repeated generator/cure notifications do
not stack shutdown effects or radio alerts. Collected pickups do not play again
unless checkpoint recovery makes them collectible again.

## Differences and manual checks

These are recognizable ports of the prototype recipes, not sample-identical
WebAudio output. Baked noise/voices have finite variation, wind has a fixed
filter centre, and Unity supplies spatial attenuation. The WebAudio compressor
and convolution reverb are not reproduced. Footstep surfaces use the current
zones (dock wood, beach sand, trail dirt, bunker concrete, generator metal),
rather than detailed material sampling. The library also retains the prototype's
grass/metal and legacy hinge recipes; the current four doors use sliding clips.
The radio cue follows CureTaken, like the existing HUD, rather than the exact
Commander broadcast timing. No flashlight, casing, bolt, bullet-whiz or
surface-impact audio hooks were invented for systems that do not expose them.

Before opening a PR, play Bootstrap with headphones or speakers:

1. Check that dock footsteps, waves and wind are audible; enter the forest and
   hear insects. Compare standing, crouched/prone and sprinting footsteps.
2. Swing the starting knife, take an enemy gun, switch, and fire both guns.
   Check player sounds are immediate and each enemy has the intended shot/voice.
   Move sideways and turn to check positional direction and distance falloff.
3. Open/close the bunker and inner doors, push the crate, collect each pickup
   type, take damage and die. Listen for clipping, repeated cues or missing tails.
4. Enter the bunker: outdoor ambience should fade into room tone; the generator
   should hum. Disable it: the hum stops, shutdown plays once and the alarm
   starts after about 2.5 s. Take the cure and complete the escape.
5. Pause/unpause, die back to powered and blackout checkpoints, then Restart.
   Confirm no stacked loops, repeated shutdowns, stale alarms or extra listeners.
   Inspect Console and balance dialogue-free gameplay levels at the target volume.

Headless tests can check routing, source configuration, state and lifecycle.
They cannot establish audible quality, mix balance or output-device behaviour.

## Verification evidence

Unity 6000.3.25f1 builder/import and repeat both exited **0**. All **13** bank
and prefab outputs were byte-identical on repeat; all four gameplay scene files
stayed identical to startup. The independent Python `--verify` run reported
**41 clips, 0 changed** (2,454,840 WAV bytes in total).

Focused EditMode tests passed **155/155** and PlayMode tests passed **11/11**,
both exit **0**. These include actual Bootstrap/restart, pickup/death callbacks,
single subscriptions after re-enable, stationary world voices, pause/completion,
generator/alarm restoration and existing checkpoint/medkit integration. The
seven-phase diagnostic recorded 35/35 knife hits, zero null references,
exceptions, error logs, missed hits or self hits, and 0 B allocated per measured
firing call after warm-up. This is selected coverage, not a whole-project pass.

Evidence is in system Temp: `blackout-AU1-{build,repeat}.log`,
`blackout-AU1-{editmode,playmode}.{log,xml}`, `blackout-AU1-before-repeat.json`
and `blackout-AU1-scene-hashes-before.json`. The first builder failed on an
incorrect root-console lookup; the corrected lookup uses the existing child.
Earlier PlayMode fixture failures (frame-count loading deadline, cleanup and
counting a legitimate delayed door cue across the restore boundary) are retained
in `blackout-AU1-playmode-{first,second}.{log,xml}`; the final run passes.
Existing licensing-token diagnostics remain. Source whitespace checks pass;
Unity's generated YAML keeps its normal trailing spaces on empty fields.

## Five-line viva

- Does: makes combat, interactions and the island audible using prototype-derived clips.
- Design: offline synthesis, pooled playback and adapters to existing events.
- Alternative: rejected per-shot object creation and live WebAudio emulation in every weapon.
- Number: 32 pooled voices bound runtime objects while allowing overlapping enemy bursts and feedback.
- Weakness: finite baked variation and zone-based surfaces approximate the prototype's live synthesis.
