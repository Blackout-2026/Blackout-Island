# Weapon loot (steps L1 and L2)

Every fresh gameplay run starts with only the **combat knife**, including Bootstrap without the cinematic and Restart. The guns come back one at a time from dead enemies:

1. The first gun taken from an enemy is the **MP-9 SMG** (slot 1).
2. The second is the **R-700 sniper rifle** (slot 2).
3. After that a pickup has nothing left to give.

Look at the dropped gun within the existing **3 m** interaction reach and press **E**. The acquired gun becomes owned immediately and equips through the existing switch animation. The HUD and weapon keys/wheel only expose owned weapons. Patrols, Hunters and the Commander drop guns; the Tactical Guard's built-in weapon is not collectible. Body ammunition and medkits keep their separate pickup behaviour.

## Why it appeared to be missing

The L1 drop scripts and armed-enemy prefab references were still present. However, `WeaponController` started with every weapon unless `LoseStartingLoadout()` ran. The only production caller found was in the untracked cinematic code; the tracked Bootstrap starts gameplay directly. Starting with both guns meant `NextLootWeapon()` returned null, so drops displayed neither gun nor glow and could not be collected.

L2 initializes the existing `loadoutLost` flag to true. `Awake` therefore creates melee-only ownership and falls back from the configured SMG start index to the knife. The cinematic callback remains safe to call. No scene, prefab, asset rebuild or package change is needed. This deliberately replaces L1's earlier rule that a start without the storm owned every weapon.

## How it works

| Piece | Where | Job |
|---|---|---|
| `WeaponLoadout` | `Scripts/PlayerAndInteraction/Weapons/WeaponLoadout.cs` | Pure C#: which weapons the player owns, the loot order (first missing Hitscan weapon in slot order), and the wheel that skips weapons not owned. |
| `WeaponController` | same folder | Initializes a knife-only `WeaponLoadout`. `LoseStartingLoadout()` also keeps only the knife; `NextLootWeapon()` / `TakeNextLootWeapon()` (the `IWeaponLoot` interface) grant the next gun and switch to it. Raises `LoadoutChanged`. |
| `WeaponSlots` | `Hud/WeaponSlots.cs` | Shows a slot only for a weapon the player owns; refreshes on `LoadoutChanged`. |
| `EnemyWeaponDrop` | `Interaction/EnemyWeaponDrop.cs` | On an armed enemy. When `Health.OnDied` fires it places one `WeaponDrop` beside the body. |
| `WeaponDropPickup` | `Interaction/WeaponDropPickup.cs` | An `Interactable` (press E). Shows the model of the gun it would give now; taking it calls `TakeNextLootWeapon()`, shows "<GUN> ACQUIRED" and removes the pickup. |
| `WeaponDropBuilder` | `Editor/SystemsEngineer/WeaponDropBuilder.cs` | Menu `Blackout > Systems > Build Weapon Drops`: builds `Prefabs/Interactables/WeaponDrop.prefab` and adds `EnemyWeaponDrop` to the Security Patrol, Hunter and Commander. The robot guard carries no gun, so it drops none. |

## Ammunition (step F2)

The SMG has a 45 round magazine and the sniper rifle 10; an emptied gun reloads for 1 s, and every dead body leaves 2 SMG magazines and 1 sniper magazine to take. See [ammunition, stance, doors and walls](ammo-stance-doors.md).

## Design choices (for the viva)

- **The pickup gives "the next missing gun", not a fixed gun.** The order is therefore the same whichever enemy dies first, and a kill can never give the same gun twice.
- **The rules live in a pure class** (`WeaponLoadout`), so they are unit-tested without a scene.
- **No assembly cycle.** `Blackout.Agents` and `Blackout.PlayerAndInteraction` cannot reference each other, so the drop component sits in PlayerAndInteraction and listens to the shared `Health.OnDied`.
- **No per-frame allocation.** The prompt strings are built once; the pickup finds the player's `WeaponController` once.
- **Starting ownership belongs to the weapon controller.** A missing or skipped cinematic cannot grant guns. A separate inventory, duplicate drop system and Bootstrap-only reset were rejected because the existing ownership system already handles the whole flow, including direct prefab starts.
- **Checkpoint recovery restores saved ownership.** A checkpoint saved with the SMG restores the SMG after death; it does not reset every respawn to the knife. Later acquisitions rewind with the saved drops. Disable/re-enable preserves ownership. Restart creates a new controller and returns to knife-only ownership.
- **The stress diagnostic explicitly takes its test guns** through the existing loot API before firing every weapon. Normal gameplay still requires enemy drops.

## Tests

`WeaponLoadoutTests` checks ownership, loot order, wheel behaviour, and the real player prefab's initialization with and without the storm callback. `WeaponDropTests` checks pickup rules, enemy death callbacks and existing prefab wiring.

`CheckpointWorldPlayTests` now starts from the default loadout without forcing storm loss. The added loot scenario uses real enemy death callbacks and dropped prefabs, collects SMG then rifle, checks HUD slots and loaded ammunition, rejects repeated collection, and checks re-enable and delayed checkpoint recovery. Pickup interaction is invoked directly in this test; keyboard E and visual presentation still need the manual checks below.

`StressTestPlayTests` checks actual Bootstrap startup and the existing Restart callback, then exercises the adapted seven-phase weapon diagnostic. Headless checks do not establish gameplay balance or visual quality.

Verified on 2026-10-22 in Unity 6000.3.25f1: compilation/import exit **0**, focused EditMode **69/69**, and PlayMode **6/6**, both test exits **0**. The seven-phase diagnostic recorded zero null references, exceptions, error logs, missed hits or self hits, with **35/35** knife swings landing. Evidence is in system Temp: `blackout-L2-import-elevated.log`, `blackout-L2-editmode.{log,xml}` and `blackout-L2-playmode.{log,xml}`. The first new EditMode fixture used `SendMessage` on an inactive prefab; explicit reflection fixed that test setup. Existing licensing-token diagnostics remain. This is selected coverage, not a whole-project test pass; the unrelated cinematic camera-angle test was not included.

## Manual checks before the PR

1. Open Bootstrap and press Play. Confirm the knife is held, only slot 3 is shown, and keys 1/2 and the wheel cannot select guns.
2. Kill a Hunter or Patrol with the knife. Look at its floating gun within 3 m: the E prompt should name the MP-9. Press E and check the gun equips, slot 1 appears and it fires with ammunition.
3. Kill another armed enemy. Press E on its gun and check the R-700 equips and slot 2 appears. Further gun drops offer nothing; medkits and ammunition still work separately.
4. Reach a checkpoint with the SMG, then take the rifle and die. Confirm full-health recovery restores the saved SMG and the correct drops/enemy roster. Repeat to check for duplicate loot.
5. Finish the mission and use Restart. Confirm knife-only ownership returns, the generator/cure reset and the Console has no new errors.

## Five-line viva

- Does: starts each fresh run with a knife and unlocks the SMG then rifle from fallen armed enemies.
- Design: reuse ownership, shared Health death events, pickup interaction and checkpoint contracts.
- Alternative: rejected making a cinematic callback responsible for the gameplay starting loadout.
- Number: 3 m is the existing interaction reach, reused so gun collection follows the same rule as other E interactions.
- Weakness: any armed enemy's drop grants the next missing gun rather than that enemy's exact carried weapon.
