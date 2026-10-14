# Viva notes: prop kit, signs and weapon viewmodels (Core Developer, step C4)

## What I built

- **22 prop and pickup prefabs** (20 in `Assets/Prefabs/Props`, `CureCase_Visual` and `Medkit_Visual` in `Assets/Prefabs/Models`) with
  colliders, the pivot rule of `docs/art-pipeline.md` section 5 and the real size from the prototype. The cure chamber has a
  72 degree opening and a `CureAnchor` socket for the cure case.
- **19 sign and floor-marking quads** with their materials and textures, drawn by code from the prototype's sign list.
- **3 weapon viewmodels** (`Weapon_Smg/Sniper/Knife_Visual`) ported from `weapons.js`, with `MuzzlePoint`, a hidden `MuzzleFlash`,
  the sniper's `BoltHandle` and the knife's `Hand`.
- **A Props palette window** that lists every prefab with the triangle count measured on the file, its budget, materials,
  colliders and size, and shows the six CC0 slots as missing.
- `PropCatalog` is the one table the builders, the palette and the tests all read. 103 new EditMode tests across the step (734 in
  the project, all green).
- The list of what is modelled in Blender and what comes from CC0 packs, and the hand-off notes: `docs/art-pipeline.md`
  section 14. Credits template: `docs/credits/core-developer.md`.

## Why this design

- **Props are data, the builder is generic.** A prop is a `ModelDefinition` (parts, colliders, sockets) and one builder turns it
  into a prefab. The same builder makes the pickups and the weapons. Adding a prop is one definition and one catalog line, and
  a test then checks its size and pivot automatically.
- **The catalog is the contract.** The size and pivot of every prop live in `PropCatalog`, and a test builds each placeholder
  and compares its measured bounds with the catalog. When the Blender model arrives, the builder runs the same check on the FBX
  and prints a warning, so a wrong pivot is caught at import and not when a prop floats in the level.
- **Placeholder first, FBX later, same prefab.** Everything works now (the World Builder can place props, the Systems Engineer
  can nest the pickups and guns) and only the `Model` child changes when Blender models exist.
- **Colliders on the root, not on parts.** The collider is the gameplay shape and the model is the look, so replacing the model
  never moves the collision. The crate is a clean 1.5 m cube with a centred pivot so the physics box matches the mesh.
- **Signs are generated.** The prototype bakes text into textures; doing the same here (a 5 x 7 dot font, laid out in metres)
  keeps the textures deterministic and avoids a font licence. A rerun writes nothing when the picture is unchanged.

## Alternative I rejected

Waiting for the Blender models and placing nothing until then, or hand-assembling each prefab from primitives in the Scene view.
The first blocks the World Builder, the Systems Engineer and the Agent Controller for days; the second works once but cannot be
re-run, tested or checked against the sizes. Generating from data costs more code now and removes both problems.

## One number and why

- **2.88 m for the cure chamber.** The prototype's chamber is 3.25 m tall, but the lab ceiling is 3.0 m in `level-layout.md`
  and the chamber must stay under 2.9 m including its base. I kept the glass 2.27 m tall, the collar 0.2 m and the column
  0.16 m, and a test fails if the catalog height reaches 2.9 m.
- Another: **0.05 m pivot and size tolerance** (`PivotRules`). A real Blender model will not hit a size to the millimetre, but 5 cm
  is the smallest gap a player or an agent can see between a prop and the floor or wall.
- And: **160 pixels per metre** for the signs, rounded to a power of two per side (64 to 1,024), so a 4.6 m sign is 512 pixels wide
  and a 0.6 m one 128.

## Known weaknesses

- **Every model is a placeholder.** No Blender model, no CC0 pack and no free gun mesh has been added, because I could not
  download or model them in this step. The palette shows this honestly: the cure case (676 triangles) and the barrel (240) are
  over what a real small or medium prop may cost because Unity's cylinders are 80 triangles each.
- **The FBX path of `ModelPrefabBuilder` has never run on a real FBX** (none exists). Material matching and the size and pivot
  check are tested on in-memory objects; the `InstantiatePrefab` line is not. Re-check on the first import.
- **Rounded boxes are plain boxes**, so the silhouettes are blockier than the screenshots. Sizes, pivots and colliders are right.
- **The chamber's glass is 16 flat panels and its collider 16 capsules**, so the wall is a polygon of 20-degree facets, not a
  smooth cylinder, and a Blender cylinder will replace the look but the collider stays faceted.
- **Weapon sleeves are approximate.** The prototype's arm rotation order (XYZ) differs from Unity's (ZXY), so a two-axis angle
  is close but not identical.
- **Hand edits to a sign texture are overwritten** by the next run, and a stripe material keeps its tiling only while it still has
  the default; I found the stripe materials changed on a second run (Unity copies the legacy `_MainTex` scale), and fixed it by
  writing both scales.
- **A bug I only found by looking in the Editor:** the first generated sign prefabs used Unity's default "Lit" material, not
  their own, so they would have been blank grey panels. My check recognised the name `Default-…` and this project's default is
  called `Lit`. The rule is now "anything that is not a material in `Assets` is replaced", and three tests cover it (a new sign,
  a sign repaired after a reset, and an audit of the real prefabs in the project). The lesson: a test that checks size and
  direction does not check what the object looks like.
- **Some of the sign pictures are simpler than the prototype's**: no bolt or trefoil icon, no drawn map.
- **The palette has not been looked at in the Editor by me**; I could only run its data code in batch mode. Check it by eye.
