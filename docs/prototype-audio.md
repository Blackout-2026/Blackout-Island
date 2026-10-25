# Gameplay audio (AU1 / AU2)

AU2 replaces **30 of the 41 clips** with downloaded recordings and Foley effects.
All retain the existing `Assets/Audio/Prototype` paths, Unity-generated metadata
and GUIDs. Footstep/firing refinement adds **twelve alternate recordings** to the
existing bank: **53 mono PCM16 WAVs**, comprising 42 sourced and 11 synthesized
clips. The twelve firing recordings use **44,100 Hz**; the other 41 WAVs retain
**22,050 Hz**. Main cue indices and player/agent/interaction prefab wiring stay intact.

Sources, authors, licences, exact archive members, processing settings and hashes
are recorded in [the source manifest](../tools/gameplay-audio-sources.json).
[Audio credits](credits/systems-engineer.md#downloaded-gameplay-audio-au2) include
the current sources and retained audition credits. Source archives and the temporary
converter remain outside the repository. Unity generates the twelve alternate clip
metadata files; all 41 existing clip metadata files remain byte-identical.

| Cues replaced | Sound selected |
| --- | --- |
| SMG, rifle, Patrol, Hunter, Guard, Commander and alternate takes | Original close Free Firearm Sound Library recordings: 9 mm SMG, bolt-action rifle, AR-15/AK-47 and 1911; two separate recorded shots per cue with retained decay |
| Knife, switch, jump | Knife swish, metal latch and clothing movement from Kenney RPG Audio |
| Sand and concrete steps | Fantozzi sand/stone left and right footfalls, preserving the full contact and release |
| Dirt, grass, wood and metal steps | Recorded pairs from congusbongus's surface pack, originally recorded by swuing and Eelke; no added metal-impact overlay |
| Landing, robot hurt/death | Soft/heavy body contact and metal impacts from Kenney Impact Sounds |
| Player/human hurt/death | Four isolated utterances from thebardofblasphemy's recorded pain/death vocals |
| Normal/heavy sliding doors | Kenney mechanical door, time-stretched to approximately 0.9 / 2.1 seconds for the existing travel durations |
| Crate | Shortened wooden creak from Kenney RPG Audio |
| Waves, wind, insects | jasinski beach surf, IgnasD wind and syncopika backyard crickets |
| Generator hum/shutdown | YCbCr motor recording, crossfaded for the loop; the same motor falls in pitch and amplitude for shutdown |

The existing short Hit, Kill, Pickup, DoorLocked, HoldTick, Alert and Complete
feedback, Room tone and Alarm remain prototype synthesis. The unused legacy
hinge-open/close clips also remain. Surface selection still uses the existing
zones; the bank retains a grass clip even though the current player route picks
dirt outdoors. No reload, bullet-whiz or other new audio hook is added.

Downloaded samples are trimmed, DC-corrected, faded and given per-cue headroom.
Ambience uses a 150 ms tail/head crossfade rather than silence at the loop seam.
Peak limits range from 0.035 for repeated crickets to 0.83 for the local rifle;
existing source volume and spatial falloff still apply. Trail dirt uses softly
filtered grass/earth recordings. These are intentional Foley adaptations, not
claims that every recording depicts the exact weapon model or surface in-game.

### Footstep refinement

The first AU2 mix was rejected as click-like, repetitive/fast and too loud. The
revised pairs retain approximately 0.24–0.44 seconds of recording, with peak
limits 0.12–0.16 (about 35–50% below the first mix), rather than cutting concrete
and metal to approximately 0.1 seconds. Each surface alternates two real takes;
pitch cycles by only -1.5%, 0%, +1.5%, without consuming Unity's shared random
sequence. `PrototypeAudioBank.GetFootstep` falls back to the main cue when an
alternate is unavailable. Enum values and existing main clip GUIDs are retained.

`GameplayAudio` sounds every other motor pulse. At the existing 4.3/7 m/s speeds
and 0.78/1.05 m stride settings this gives **2.76/3.33 audible steps per second**,
instead of 5.51/6.67. The motor, movement settings, camera bob and **all AI noise
events/radii are unchanged**. This is an intentional audio refinement to the
prototype cadence. Crouch/prone remain quieter. Jump/landing and audio disable,
checkpoint restore and completion reset the audible pulse phase; a landing
contact plays immediately. Existing voice pooling and pause behaviour are reused.

### Firing refinement

Firing uses original **near** recordings identified by the library's recording
sheet, replacing the shorter game edits and the earlier pistol audition. The SMG
uses a Carl Gustav M45 9 mm report, the sniper a Tikka T3 bolt-action report,
Patrol/Guard AR-15 reports, Hunter AK-47 and Commander 1911. These match weapon
classes; they are not recordings of the exact MP-9/R-700 models in the game.
Each cue alternates two separately recorded shots, with a deterministic pitch
cycle of only -0.8%, 0%, +0.8%; it does not consume Unity's shared random sequence.

Shots retain 0.62–1.10 seconds of attack/body/decay at 44.1 kHz. The import uses
one microphone channel from the stereo recordings, a 35 Hz high-pass and light
2:1 compression, with final peaks 0.52–0.83. This preserves body without adding
synthetic bass, and avoids folding differently phased microphones together.
The first 0.2 ms and final 60 ms fades prevent cut boundaries from clicking.
All cuts, filters, sample rates and output hashes are in the manifest.

The same **32** one-shot sources now have separate groups: **8 local firing**,
**4 local feedback**, **20 positional world**. Hit markers, footsteps and damage
cannot steal a player-shot voice. Eight voices cover the SMG's 720 rpm and
0.62-second tails, including its small pitch variation. Gun voices use priority
64; other effects use 128. Saturating a group reuses a source within that group.
Pause, completion and checkpoint recovery retain the existing pooled lifecycle;
recovery resets the take sequence. Weapon timing, damage, recoil, ammo and AI
NoiseEvent publication are unchanged.

## Rebuild

1. Normally, use the committed WAVs; no converter or download is needed by Unity.
   Run `python tools/import-gameplay-audio.py --verify` to check all 42 sourced
   outputs offline, then
   `python tools/generate-prototype-audio.py --verify` to check all 53 WAVs
   (11 synthesized, 42 protected).
2. To rebuild sourced clips, supply FFmpeg **7.1** and a cache outside the repo:
   `python tools/import-gameplay-audio.py --ffmpeg "<ffmpeg.exe>" --cache "<cache>" --download`.
   Omit `--download` when the cache already contains the sources. Downloaded bytes
   must match the pinned SHA-256 values. Windows' bundled `tar` reads the small
   footstep 7z archive. FFmpeg only decodes/resamples and applies the listed
   filters; Python handles trimming, mixing, fades, loop seams and PCM output.
3. `python tools/generate-prototype-audio.py` rebuilds only the 11 remaining
   synthesized cues. It refuses missing/altered sourced WAVs instead of replacing
   them with synthesized placeholders.
4. Run **Blackout > Systems > Refresh Audio Clips** to refresh the existing bank
   and clip importers, including its twelve alternate references. If prefab wiring
   also needs rebuilding, **Blackout > Systems > Build Prototype Audio** configures
   the bank/importers and patches existing prefabs through Editor APIs. It keeps
   GUIDs, prefab variants, manually authored content and other event listeners.

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
one-shot AudioSources** (eight local firing, four local feedback, twenty world sounds)
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

The 11 retained synthesized cues are ports of the prototype recipes, not
sample-identical WebAudio output. Recordings also have finite variation, and
Unity supplies spatial attenuation. The WebAudio compressor
and convolution reverb are not reproduced. Footstep surfaces use the current
zones (dock wood, beach sand, trail dirt, bunker concrete, generator metal),
rather than detailed material sampling. The bank retains an unused recorded
grass pair and synthesized legacy hinge cues; the current four doors use sliding clips.
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

## Original AU1 verification evidence (before the AU2 sample replacement)

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

## Initial AU2 verification evidence (before the footstep refinement)

- Unity 6000.3.25f1 import completed with return code **0** in the Unity log.
  Focused `PrototypeAudioTests` EditMode passed **7/7**, process exit **0**;
  `GameplayAudioPlayTests` PlayMode passed **3/3**, process exit **0**. These are
  selected audio tests, not a whole-project or full-mission pass. PlayMode uses
  Bootstrap with agent decision/movement/shooting disabled for controlled callbacks.
- Conversion and its repeat both exited **0**; the repeat changed **0/30** WAVs.
  Sourced output verification and the combined synthesis/protection check exited
  **0**, covering **30 sourced + 11 synthesized** clips. All 41 WAVs pass independent
  mono/PCM16/22,050 Hz, non-silence, headroom and 0.02–8.01 s duration checks.
  Total WAV bytes: **2,287,418**. The three replaced loop seam jumps are under
  **0.0018** full scale; this is a signal check, not listening approval.
- All **69 other recorded input hashes** remain unchanged, including the 41
  audio metadata files, retained synthesized WAVs, audio bank, Player and generator
  prefabs, scene files and ProjectSettings. Python syntax, local documentation
  links and source whitespace passed. No staging or history changes were made.
- Evidence: system Temp `blackout-AU2/before.json`, `sources.json`, downloaded
  licence pages/archives, `import-retry.log`, `editmode.{log,xml}` and
  `playmode.{log,xml}`. Initial sandbox licensing stalled; an initial user-account
  retry exited 1 while that batch still held the project. Only that verified
  batch was terminated before the successful retry. The import shell's child
  wait outlived Unity; the import return code above is from Unity's shutdown log.
  Existing licence-token/signature and UnityConnect cloud timeout diagnostics
  remain. Final import/tests have no C# compiler errors or warnings.

The five listening checks above still apply. In particular, compare automatic
gun overlap and quieter footsteps against the new recordings; listen to several
surf/wind/motor repetitions, door travel, vocal cuts and the motor shutdown with
the 2.5-second alarm delay. Headless tests do not verify audible quality,
speaker/headphone mix, spatial perception or native gameplay callback ordering.

## Footstep refinement verification (before firing refinement)

Unity import and repeat refresh exited **0**. Focused EditMode tests passed
**14/14** and PlayMode tests **4/4**, both exit **0**, with no C# compiler errors
or warnings. The new controlled motor test feeds real distance samples into
`PlayerMotor.UpdateFootsteps`: six raw steps still emit six unchanged hearing
events, but three quieter alternating recordings reach the actual voice pool;
checkpoint restore starts a fresh audible step. It does not simulate native input.

All **47** WAVs pass mono PCM16/22,050 Hz, duration, non-silence and headroom
checks; all twelve footfalls last 0.24–0.44 s and peak below 0.17. WAV bytes total
**2,385,708**. Repeated conversion changes **0/36** sourced outputs; the bank and
six new Unity-generated metadata files remain byte-identical on refresh. All 41
original clip metadata files, non-footstep audio, player/generator prefabs,
scenes, project settings, movement settings and motor code remain unchanged.

Evidence: system Temp `blackout-AU2-footsteps/{import,repeat}.log`,
`{editmode,playmode}.{log,xml}`, `before.json` and `bank-before-repeat.json`.
Existing licence-token/signature diagnostics remain. These selected tests and
signal checks do not establish audible realism or mix balance. Listen to both
walking and sprinting on dock, beach, trail, bunker and generator-room surfaces;
also check stop/start, crouch/prone, jumping/landing, pause and checkpoint recovery.

## Firing refinement verification

Unity import and repeat bank refresh exited **0**. Focused EditMode passed
**21/21** and PlayMode **5/5**, both exit **0**, with no C# compiler errors or
warnings. The firing regression uses the real acquired SMG and diagnostic Fire
path: two recorded takes remain playing through 48 local feedback requests;
nine shots produce nine AI gunshot events; the gun group remains bounded at
eight sources; recovery resets its sequence. Existing footstep, enemy/pickup,
generator, checkpoint, pause and completion audio tests also pass. Diagnostic
fire bypasses cooldown/input, so this does not verify native trigger timing.

All **53** WAVs pass format, non-silence and duration checks; the sourced clips
retain peak headroom. Twelve
gunshot WAVs are 44,100 Hz and the other 41 remain 22,050 Hz; total WAV bytes
**3,029,524**. A rendered 24-shot SMG burst at 720 rpm, including take/pitch
variation, peaks at **0.823** full scale; the two-rifle-shot preview peaks at
**0.830**. Neither render clips. This checks the isolated recorded mix, not the
entire game's output-device mix. Repeated conversion changes **0/42** outputs;
the bank and twelve alternate metadata files are byte-identical on refresh.
All previous footstep recordings/metadata, non-firing audio, prefabs, scenes,
movement settings, weapon rules and project settings remain unchanged.

Evidence: system Temp `blackout-AU2-guns/{import,repeat}.log`,
`{editmode,playmode}.{log,xml}`, `before.json`, `bank-before-repeat.json`,
`burst-check.json`, `firing-preview.wav`, original recording sheets and source
WAVs. Existing licence-token/signature diagnostics remain. Listen to isolated
SMG taps, sustained bursts, rifle shots and each enemy at different distances;
repeat while moving, taking hits, pausing and recovering. Audible quality and
full-game mix remain manual checks.

## Five-line viva (AU2)

- Does: replaces 30 mismatched synthesized effects with sourced recordings and Foley.
- Design: replace WAV content in place so the existing bank, events and pooled sources stay connected.
- Alternative: a second audio system would duplicate the already-connected playback and lifecycle code.
- Number: 41 main cues plus twelve alternate recordings share the existing 32 pooled voices and four ambience loops.
- Weakness: two takes per footstep/gun cue, zone-based surfaces and short ambience loops still need live listening and mix review.
