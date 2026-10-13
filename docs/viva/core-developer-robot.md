# Viva notes: security robot prefab (Core Developer, step C3)

## What I built

- `SecurityRobot_Visual.prefab` (`Assets/Prefabs/Models`): the robot's look and two sockets, nothing else. No collider,
  Health or script, so the Agent Controller nests it without editing it.
- `RobotRig`: one table of the 16 joints (name, parent, rest offset), the socket names and the muzzle offset. Joints
  follow the prototype's bone tree, so the numbers (1.05 m pelvis, 1.76 m shoulders, 0.14 m ankle) are measured, not mine.
- `RobotPlaceholder` and `RobotPrefabBuilder`: an Editor tool that builds a primitive stand-in on that tree (81 parts), and
  swaps in `SM_SecurityRobot.fbx` when it exists. `RobotMaterialBuilder` makes the five materials. `ModelMaterials` is the
  code both the generator and the robot use to create them (a refactor, no behaviour change).
- 26 new EditMode tests (572 in the project, all green).
- Joint names, pivots, rest pose and sockets for the Agent Controller: `docs/art-pipeline.md` sections 11 and 13.

## Why this design

- **The joint tree is data, and a test enforces the contract.** The animator finds joints by name, so a wrong or renamed
  joint fails silently (it skips the joint). One test reads the name fields of `ProceduralRobotAnimator` and compares them
  with `RobotRig.AnimatedJoints`, so either side renaming a joint breaks the build, not the demo.
- **Placeholder first, same tree.** The animation, the shooter and the AI can be built and tested before the Blender model
  exists, and the FBX is a drop-in: same names, same pivots, same material names.
- **Nested joints, rest pose at zero.** Legs and arms nest like the prototype's bones, so a thigh swing carries the shin
  and foot. Every joint rests at rotation (0, 0, 0), because the animator adds angles to the rest rotation; the sloped
  thigh and shin are modelled into the mesh.
- **Sockets under the right hand.** A muzzle on the root would stay at the hip when the arm raises. Under `Hand_R` it
  follows the pod, and its `forward` is the barrel direction (rotation 90 degrees about X).
- **Glow as HDR values on flat materials.** The eye (5, 0.35, 0.15) and cells (0.4, 2.6, 3.0) are above 1 on purpose so
  the bloom picks them up, which a baked texture cannot do.

## Alternative I rejected

A skinned, rigged mesh with an Animator Controller. It is what a humanoid would use, but it needs an armature, weights and
baked clips, and the brief (and the prototype) animate the robot by formulas. Rigid parts with pivots at the joints keep the
Blender work to separate meshes, keep the triangle budget (no weighted topology at the joints), and let the Agent Controller
drive the robot from `RobotGaitMath`, which is directly testable.

## One number and why

- **`MuzzlePoint` at (0, -0.33, 0) under `Hand_R`**: 0.33 m below the wrist, between the two barrels. It is the prototype's
  own muzzle offset (`robot.js`), and the barrels run from y -0.31 to +0.03 in the hand, so a shot starts just past the barrel tips.
- Another: **5 material assets, at most 4 in the FBX**. The importer budget is 4 (section 6). Body, Eye and Cells are the
  three that matter; Joint and Accent exist so the placeholder can show dark metal and yellow with flat colours.

## Known weaknesses

- The FBX path of `RobotPrefabBuilder` has not run on a real FBX yet (none exists). Material matching and the missing-joint
  check are tested on in-memory objects; the `InstantiatePrefab` line is not. Re-check on the first import.
- The placeholder is about 7,000 triangles and 81 draw objects per robot, more than the real model will cost. It is only
  for development.
- Rounded boxes are plain boxes, so the silhouette is blockier than the screenshots. Sizes and joints are right, shapes are not.
- `ScannerDot` shares the red eye material, so the prototype's amber idle colour is not shown. Doing it per robot needs a
  `MaterialPropertyBlock`, which is the Agent Controller's call.
- The prototype's small rest offsets (thigh -0.15 rad, shin +0.12 rad, arms leaning out) are not in the model or in A3's
  animator; the robot stands a little straighter than in the screenshots.
- The tests that import real assets write to a `_Test` folder and delete it afterwards, so an interrupted run can leave
  `Assets/Materials/Models/_Test` or `Assets/Textures/Models/_Test` behind. Delete those folders if you see them.
