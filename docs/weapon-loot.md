# Weapon loot (step L1)

After the storm in the introduction the player has only the **combat knife**. The guns come back one at a time from dead enemies:

1. The first gun taken from an enemy is the **MP-9 SMG** (slot 1).
2. The second is the **R-700 sniper rifle** (slot 2).
3. After that a pickup has nothing left to give.

## How it works

| Piece | Where | Job |
|---|---|---|
| `WeaponLoadout` | `Scripts/PlayerAndInteraction/Weapons/WeaponLoadout.cs` | Pure C#: which weapons the player owns, the loot order (first missing Hitscan weapon in slot order), and the wheel that skips weapons not owned. |
| `WeaponController` | same folder | Owns a `WeaponLoadout`. `LoseStartingLoadout()` keeps only the knife; `NextLootWeapon()` / `TakeNextLootWeapon()` (the `IWeaponLoot` interface) grant the next gun and switch to it. Raises `LoadoutChanged`. |
| `WeaponSlots` | `Hud/WeaponSlots.cs` | Shows a slot only for a weapon the player owns; refreshes on `LoadoutChanged`. |
| `EnemyWeaponDrop` | `Interaction/EnemyWeaponDrop.cs` | On an armed enemy. When `Health.OnDied` fires it places one `WeaponDrop` beside the body. |
| `WeaponDropPickup` | `Interaction/WeaponDropPickup.cs` | An `Interactable` (press E). Shows the model of the gun it would give now; taking it calls `TakeNextLootWeapon()`, shows "<GUN> ACQUIRED" and removes the pickup. |
| `WeaponDropBuilder` | `Editor/SystemsEngineer/WeaponDropBuilder.cs` | Menu `Blackout > Systems > Build Weapon Drops`: builds `Prefabs/Interactables/WeaponDrop.prefab` and adds `EnemyWeaponDrop` to the Security Patrol, Hunter and Commander. The robot guard carries no gun, so it drops none. |

## Design choices (for the viva)

- **The pickup gives "the next missing gun", not a fixed gun.** The order is therefore the same whichever enemy dies first, and a kill can never give the same gun twice.
- **The rules live in a pure class** (`WeaponLoadout`), so they are unit-tested without a scene.
- **No assembly cycle.** `Blackout.Agents` and `Blackout.PlayerAndInteraction` cannot reference each other, so the drop component sits in PlayerAndInteraction and listens to the shared `Health.OnDied`.
- **No per-frame allocation.** The prompt strings are built once; the pickup finds the player's `WeaponController` once.
- A normal start (no storm) still owns every weapon, so the game outside the introduction is unchanged.

## Tests

`WeaponLoadoutTests` (ownership, loot order, wheel) and `WeaponDropTests` (pickup, enemy drop, built prefabs) in `Scripts/PlayerAndInteraction/Tests`.
