# Weapon loot (steps L1 and L2)

Every fresh gameplay run starts with the **MP-9 SMG** (slot 1) and **combat knife** (slot 3), including Bootstrap without the cinematic and Restart. The remaining gun comes from a dead enemy:

1. The first gun taken from an enemy is the **R-700 sniper rifle** (slot 2).
2. After that a pickup has nothing left to give.

Look at the dropped gun within the existing **3 m** interaction reach and press **E**. The acquired gun becomes owned immediately and equips through the existing switch animation. The HUD and weapon keys/wheel only expose owned weapons. Patrols, Hunters and the Commander drop guns; the Tactical Guard's built-in weapon is not collectible. Body ammunition and medkits keep their separate pickup behaviour.

## Why it appeared to be missing

The L1 drop scripts and armed-enemy prefab references were still present. However, `WeaponController` started with every weapon unless `LoseStartingLoadout()` ran. The only production caller found was in the untracked cinematic code; the tracked Bootstrap starts gameplay directly. Starting with both guns meant `NextLootWeapon()` returned null, so drops displayed neither gun nor glow and could not be collected.

The controller now grants the SMG during `Awake` while leaving the rifle unowned. The cinematic callback keeps the same starting pair. The serialized start index already selects the SMG, so no scene or prefab change is needed.

## How it works

| Piece | Where | Job |
|---|---|---|
| `WeaponLoadout` | `Scripts/PlayerAndInteraction/Weapons/WeaponLoadout.cs` | Pure C#: which weapons the player owns, the loot order (first missing Hitscan weapon in slot order), and the wheel that skips weapons not owned. |
| `WeaponController` | same folder | Initializes the SMG and knife. `LoseStartingLoadout()` keeps that pair; `NextLootWeapon()` / `TakeNextLootWeapon()` (the `IWeaponLoot` interface) grant the rifle and switch to it. Raises `LoadoutChanged`. |
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
- **Starting ownership belongs to the weapon controller.** A missing or skipped cinematic still starts with the SMG and knife. The existing ownership system handles direct prefab starts and Bootstrap.
- **Checkpoint recovery restores saved ownership.** A checkpoint saved before collecting the rifle restores the SMG and knife after death. Later acquisitions rewind with the saved drops. Disable/re-enable preserves ownership. Restart creates a new controller with the starting pair.
- **The stress diagnostic explicitly takes its test guns** through the existing loot API before firing every weapon. Normal gameplay still requires enemy drops.

## Tests

`WeaponLoadoutTests` checks ownership, loot order, wheel behaviour, and the real player prefab's initialization with and without the storm callback. `WeaponDropTests` checks pickup rules, enemy death callbacks and existing prefab wiring.

`CheckpointWorldPlayTests` starts from the default loadout. Its loot scenario uses real enemy death callbacks and dropped prefabs, collects the rifle, checks HUD slots and loaded ammunition, rejects repeated collection, and checks re-enable and delayed checkpoint recovery. Pickup interaction is invoked directly in this test; keyboard E and visual presentation still need the manual checks below.

`StressTestPlayTests` checks actual Bootstrap startup and the existing Restart callback, then exercises the adapted seven-phase weapon diagnostic. Headless checks do not establish gameplay balance or visual quality.

The 2026-10-22 Unity 6000.3.25f1 results (focused EditMode **69/69** and PlayMode **6/6**) apply to the earlier knife-only version. The SMG-start change needs a fresh Unity test run.

## Manual checks before the PR

1. Open Bootstrap and press Play. Confirm the SMG is held, slots 1 and 3 are shown, and key 2 cannot select the rifle yet.
2. Kill a Hunter or Patrol. Look at its floating gun within 3 m: the E prompt should name the R-700. Press E and check the rifle equips, slot 2 appears and it fires with ammunition.
3. Check that further gun drops offer nothing; medkits and ammunition still work separately.
4. Reach a checkpoint before taking the rifle, then take it and die. Confirm full-health recovery restores the saved SMG and the correct drops/enemy roster. Repeat to check for duplicate loot.
5. Finish the mission and use Restart. Confirm SMG and knife ownership returns, the generator/cure reset and the Console has no new errors.

## Five-line viva

- Does: starts each fresh run with the SMG and knife, then unlocks the rifle from a fallen armed enemy.
- Design: reuse ownership, shared Health death events, pickup interaction and checkpoint contracts.
- Alternative: rejected making a cinematic callback responsible for the gameplay starting loadout.
- Number: 3 m is the existing interaction reach, reused so gun collection follows the same rule as other E interactions.
- Weakness: any armed enemy's drop grants the next missing gun rather than that enemy's exact carried weapon.
