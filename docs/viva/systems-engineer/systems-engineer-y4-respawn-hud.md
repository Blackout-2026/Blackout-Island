# Viva notes: death, respawn and HUD (Systems Engineer, step Y4)

PR #22. Commits 87c8c8b, ad168ec, df9980f, 74d0b30 and f0d9514. Numbers are in `docs/reference/systems-engineer.md`
sections 8–9.

## What I built

- **Rules, as pure code with 16 tests:**
  - `DeathSequence`: the 0.8 s fall-over camera and the 2.2 s respawn delay.
  - `PlayerHealthRules`: fall damage and kill height.
  - `HudMath`: the ghost bar, the damage flash and the low-health pulse.
  - `CheckpointName`.
- **`PlayerHealth`:**
  - It wires the shared `Health` to the player.
  - On death it raises `PlayerDied`, disables the input reader (which stops movement, look and weapons), hides the
    weapons and plays the death camera.
  - After 2.2 s it respawns at the checkpoint with full health and 1.5 s of invulnerability, then raises
    `PlayerRespawned`.
- **`CheckpointTracker`:** the current `Checkpoint` marker. Until Y6 it used a 6 m reach rule, with escape checkpoints
  only after `CureTaken`. Since Y6 the MissionDirector sets it.
- **Fall damage** of `(speed − 13) × 8` above 13 m/s, a **kill height** at y < −20, and the **damage shake**.
- **HUD** (uGUI):
  - health bar with a ghost bar
  - weapon slots
  - a crosshair for each weapon
  - hit marker
  - objective panel
  - checkpoint label and toasts
  - interaction prompt
  - alert banner
  - damage direction arcs
  - damage flash
- **`Blackout/Systems/Build Player HUD`.** It adds Health, CheckpointTracker, PlayerHealth and the HUD canvas to the
  Player prefab, with four generated textures stored in Git LFS.

## Why this design

- **Respawn instead of game over**, as in the prototype. Death costs time, and the mission state (generator, cure)
  is kept.
- **Disabling the input reader** is one switch that stops everything the player controls. No system needs its own
  "is dead" check.
- **The HUD only listens.** It subscribes to `Health.OnDamaged`, `WeaponController.HitConfirmed`, `WeaponChanged`
  and the `ObjectiveChanged` channel, and it never calls game code. Removing the canvas breaks nothing.
- **The maths is tested apart from the UI.** The 2.5-rate ghost bar and the flash formula are checked in EditMode,
  with no screenshots needed.
- **Legacy uGUI `Text`.** TextMeshPro would need its Essential Resources imported into a folder nobody owns.

## Alternatives rejected

- **A separate HUD scene.** The HUD belongs to the player, so it moves and respawns with the prefab.
- **Polling the player's health every frame.** Events fire only when something changes.

## Numbers

- **Death and respawn:** the fall-over takes 0.8 s, with the eye dropping to 0.25 m, 63° of roll and 11.5° of
  pitch. Respawn is after 2.2 s, followed by 1.5 s of invulnerability.
- **Hit marker:** shows for 0.16 s, or 0.35 s in red on a kill.
- **Health bar:** turns red below 35. The ghost bar is damped at rate 2.5.
- **Damage flash:** `+amount / 25` per hit, fading at 1.4/s.
- **Tests:** EditMode 605/605. The builder left the prefab unchanged on its second run, and the 132 existing prefab
  objects kept their IDs.

## Known weaknesses

- Not ported: the compass and objective distance, the scope overlay and the objective slide-in.
- The crosshair gap is fixed per weapon; it doesn't open with live spread.
- The alert banner listens to `CureTaken`, because the Commander's broadcast has no channel.
- The 4 HUD textures first went in as plain Git blobs before LFS was installed (fixed in Y5, 8fc6c35).
