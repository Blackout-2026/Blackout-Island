# Viva notes: weapons (Systems Engineer, step Y3)

PR #19. Commits 7988390, 8f7cdb5, 9a138d5, c826baa and fa8cdae. Numbers are in `docs/reference/systems-engineer.md`
section 7. The design argument is in `docs/viva/systems-engineer.md` section 2, and this file records the step.

## What I built

- **Data:** `WeaponData` (a ScriptableObject) and three assets: SMG, Rifle and Knife in
  `Assets/ScriptableObjects/Weapons`.
- **Pure logic with 21 tests:**
  - `WeaponMath`: spread, spread direction, bloom, falloff and the melee cone.
  - `WeaponCooldown` and `WeaponSwitcher` (lower 0.18 s, raise 0.22 s).
  - `RecoilSpring`.
- **Runtime:**
  - `WeaponController` handles input, switching (1/2/3 and the wheel), the shared cooldown, the sprint rule, view and
    viewmodel recoil, and the holder animation.
  - `IWeapon` has two implementations: `HitscanWeapon` (SMG, rifle) and `MeleeWeapon` (knife).
  - `WeaponContext` holds what the weapons share.
- **Effects:** `TracerPool` (24), `ImpactEffects` (one ParticleSystem, `Emit`), `MuzzleFlash` (one shared light) and
  `HitFlash` (Shared, a MaterialPropertyBlock).
- **`Blackout/Systems/Build Player Weapons`.** It adds the weapons, placeholder viewmodels at the prototype's
  positions, effects and materials to the Player prefab.
- **Events for the HUD (Y4):** `HitConfirmed(killed)` and `WeaponChanged`.

## Why this design

- **Behaviour differs only by data.** The prototype has one table for all three weapons, and `WeaponData` is that
  table.
- **One controller.** Switching, cooldown and the sprint rule are written once. A weapon class answers only "what does
  a shot do".
- **Hitscan from the camera.** That's what the prototype does, and it's the cheapest physics query: no simulated
  bullets.
- **Damage through `IDamageable`.** The weapons don't know what an agent is.
- **Pools, created in `Awake`.** No `Instantiate` or garbage while firing. Y7 measured 0 B per shot.
- **The view recoil is an offset.** The aim settles back, as in the prototype, instead of permanently changing the
  view.

## Alternatives rejected

- **A MonoBehaviour per weapon that reads input itself.** The rules get copied, and two weapons can fire on one click.
- **An inheritance tree (`Weapon` → `Gun` → `SMG`).** The differences are numbers.
- **Rigidbody projectiles.** Not in the prototype; they'd mean simulated bodies and continuous collision.
- **One LineRenderer per tracer instantiated per shot.** That allocates while firing.

## Numbers

- SMG: 15 damage, 720 rpm, 90 m, falloff from 10 to 32 m down to ×0.45, spread 1.38°.
- Rifle: 60 damage, 52 rpm, 320 m.
- Knife: 40 damage, 2.3 m (+0.4 tolerance), 51.7° cone, the hit lands at 0.11 s.
- Falloff = `lerp(1, min, smoothstep(10, 32, d))`. The switch takes 0.4 s, and the lowered pose is 0.3 m down and
  40° pitched.
- Viewmodel springs: 180/18 (ζ ≈ 0.67) and 160/16 (ζ ≈ 0.63).
- Hit mask in Y3 was "everything except Player, Ignore Raycast and Ragdoll". Y7 narrowed it to `PlayerShots`.
- Tests: EditMode 509/509 (21 new). The builder built everything on its first run, left it unchanged on its second,
  and the prefab's 17 original objects kept their IDs.

## Known weaknesses

- No headshot or backstab multipliers (there's no head hitbox contract), no sniper zoom, and no surface-specific
  impacts, decals, sounds or viewmodel camera.
- The cooldown is set rather than accumulated, so the fire rate rounds to whole frames. At low frame rates the SMG
  fires slightly slower than 720 rpm.
- The knife's `kickRotate` of 12° is my own [GUESS], so a swing is visible without the swing animation.
- The viewmodels are placeholder primitives until the Core Developer's models are swapped in.
