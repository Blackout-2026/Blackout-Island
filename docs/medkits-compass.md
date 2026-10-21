# Stored medkits and compass (MC1)

Every defeated enemy now leaves one stored medkit beside its body, including
the Tactical Guard. Walk over the kit to collect it, even at full health.
The player starts with zero stored kits. The panel above the health bar shows
the count and **Q · USE +25 HP**. A fresh Q press consumes one kit and heals
up to **25 HP**, capped at 100 with the current player configuration. Holding Q
does not repeat. Empty inventory, full health and death never consume a kit.
Disabled player input and a paused game block Q usage.

This is the requested solo addition to the prototype. The three placed bunker
`HealthPickup` objects retain their original immediate +35 HP behaviour; they
do not supply stored inventory. Existing weapon loot progression is unchanged.

`MedkitInventory` owns the count, calls the existing `Health.Heal`, and exposes
a C# count-change event. `MedkitCounter` only reads that event and refreshes on
re-enable. The existing `EnemyWeaponDrop` gains an optional `medkitPrefab` field
and reuses its ground-placement code. Guns drop 0.6 m to the right and medkits
0.6 m to the left. The robot has the medkit reference only. Its historical
component name is retained to preserve serialized data. No AI brain changes.

The inventory implements `ICheckpointState`; dropped kits implement
`ICheckpointSpawnedObject`. Collection deactivates a kit rather than destroying
it. Checkpoint recovery restores the saved count and saved kit availability,
and discards kits created during the failed attempt. Restoring a dead enemy
does not raise another death event. Full-health player recovery and the one
outside-lab cure wave retain the existing R1 rules. Restart creates a new,
empty inventory with the new player.

The top compass follows `prototype/src/ui/hud.js`, `ui/style.css` and
`prototype/screenshots/02_forest.png`: a 460 px scrolling strip, 15-degree
spacing, cardinal labels and a gold destination pointer. MC1 additionally
labels intermediate degrees and displays the current numeric heading.
There are 2 reference pixels per degree; an off-strip destination clamps at
112 degrees to either side. Coordinates remain metres, +Y up, +Z north and
+X east: N=0, E=90, S=180, W=270. Prototype `(x,y,z)` maps to Unity `(x,y,-z)`.

`MissionDirector.TryGetNavigationTarget` uses its existing runtime marker
lookup and the actual generator console. Guidance starts at D-Bunker, then
points down the ramp, toward the generator, toward the cure, and back through
security/the entrance to the boat during escape. Completion and player death
hide the compass. Checkpoint restoration changes the target through the
restored mission state. The HUD holds no persistent cross-scene references.
Distance is horizontal straight-line distance, not route length; this is
guidance, not a pathfinding system. A destination behind the player appears
at the strip edge until the player turns.

Run **Blackout > Systems > Add Medkits And Compass** to update the managed
content. `MedkitCompassBuilder` adds the inventory, two HUD widgets and the
four enemy references through prefab APIs. It nests the existing
`Medkit_Visual` in `MedkitDrop.prefab`, retains weapon/model references and
prefab variants, and moves the original compact alert below the compass.
It does not rebuild Bootstrap, the existing HUD or the cinematic introduction.
The configured `PlayerControls.inputactions` binds `UseMedkit` to keyboard Q.
The new runtime code stays in `Blackout.PlayerAndInteraction`; only the
PlayMode test assembly adds Input System/uGUI references for input/HUD tests.

Verification covers inventory limits, healing, death, duplicate death
subscriptions, bearings/north wrap, mission targets, prefab references, real
Q input/holding/disabled input, trigger collection, HUD re-enable and repeated
delayed checkpoint recovery. The headless keyboard tests temporarily use an
in-memory settings copy because batch mode has no focused Game view; teardown
restores the original settings. No package or project-setting change is needed.
These checks do not establish visual quality, cinematic integration or a full
manual mission playthrough.

Verified on 2026-10-21 with Unity 6000.3.25f1: builder/import and repeat builder
both exited 0; all six managed prefabs were byte-identical on the second run.
Final focused EditMode **90/90** and PlayMode **5/5** passed, both exit 0.
The first EditMode attempt used SendMessage for a test callback, which Unity
rejects outside PlayMode; the test now invokes it through reflection like the
existing drop tests. Q test setup/cleanup was corrected for headless focus and
Input System's destruction of replaced temporary settings. No runtime change
was needed for those test-harness failures. No C# compiler errors or warnings
were found in the final logs. Existing licensing-token diagnostics remain.
Evidence is in system Temp: `blackout-MC1-build.log`, `blackout-MC1-repeat.log`,
`blackout-MC1-before-repeat.json`, `blackout-MC1-editmode.{log,xml}` and
`blackout-MC1-playmode.{log,xml}`. These are selected tests, not a whole-project pass.

Manual checks in Bootstrap:

1. After the intro, confirm the side count starts at 0. Kill each enemy type,
   including a robot; find a medkit beside the body and walk over it. The count
   increases once and collection does not heal immediately.
2. While wounded, press Q: health rises by 25 (or to 100) and the count drops by
   one. Hold Q, try it with no kits, and try at full health: no extra kit is used.
3. Face north/east/south/west and check 000/090/180/270 degrees. Turn through
   north and locate the gold BUNKER marker; its distance should fall as you
   approach. Check readable labels at 1280x720 and 1920x1080, plus your target
   aspect ratio, with no overlap with the objective, checkpoint or alert.
4. Continue through the generator and cure, then escape. Check CURE/EXIT/BOAT
   guidance, including after death. Save a checkpoint with a stored kit and an
   uncollected drop, use/collect them, kill another enemy and die twice: the
   saved count/drop should return and later drops should disappear.
5. Finish and restart. Check an empty inventory, current compass target, normal
   weapon pickups, the original +35 HP bunker pickups and no new Console errors.

Viva:

- Does: stores enemy medkits for Q healing and shows the objective bearing.
- Design: separate inventory/HUD, reuse death drops, health and checkpoint contracts.
- Alternative rejected: immediate healing from enemy drops removes the choice of when to heal.
- Parameter: 25 HP per kit is the requested quarter of the player's 100 HP maximum.
- Weakness: the compass points at route anchors and does not plan a path around obstacles.
