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

---

# Viva notes: the real robot model (Core Developer, step C8)

## What I built

- `SM_SecurityRobot.fbx` and `Art/Blender/SM_SecurityRobot.blend`: 17 meshes (16 joints and the scanner dot), 4,740 triangles,
  4 materials, 1.068 x 2.370 x 0.635 m, one 2048 texture set. It replaces the primitive placeholder; the joint names,
  offsets and material names did not change, so the Agent Controller edited nothing.
- Five Blender scripts in `Art/Blender/Scripts`: `build_robot.py`, `bake_robot.py`, `export_robot.py`, `preview_robot.py`
  (plus the shared `gen_parts.py` and the `bake_smg.py` helpers). `RobotModelFbxTests` checks the real FBX.

## Why this design

- **I read the animator first.** `ProceduralRobotAnimator` finds 12 joints by exact name with a depth-first search, saves each
  joint's rest rotation, and then sets `rest * AngleAxis(angle, Vector3.right)`. It moves `Pelvis` by its local position. So
  the model needs exact names, unique names, rest rotation 0, local X as the left-right axis, and `Pelvis` directly under the model.
- **The export problem was measured, not assumed.** I built a scratch Unity project with the same import settings and
  exported a 4-level test chain both ways. Apply Transform ON broke the third level (`Shin_L` rotated 270 degrees). OFF left
  a 90 degree rotation on the root. Cancelling that rotation in Blender (an Empty rotated +90 about X, children modelled in
  that frame with z mirrored) gave rotation 0 and the exact `RobotRig` offsets on all four levels.
- **A second probe caught a bug in my first fix.** With `Pelvis` as the only top-level node, Unity merged it into the model
  root, so no object was called `Pelvis` and the robot sat 1.05 m too high. A plain Empty above it fixed that.
- **One atlas, one build for all parts.** The parts are merged for UV unwrapping and baking (so ambient occlusion sees the
  neighbouring parts and every part has the same texel density), then split back into joints. Yellow, steel and hazard stripes
  are painted into the Body atlas through a face-corner colour, which is how the model stays inside 4 material slots.
- **Hidden faces are removed per joint.** A face is only deleted if the same joint's other parts cover it, because parts of
  other joints move away from it.

## One number and why

- **-90 / +90 degrees.** The only rotation in the whole file is +90 about X on the root Empty. It is exactly the opposite of
  what the FBX exporter adds for Z-up to Y-up, so the net is 0 and every joint is clean in Unity.
- **4,740 triangles** against a target of 5,000: the hidden-face pass removed about 500 faces, and I spent the saving on rounder armour (big round pauldrons, a smooth helmet) and trimmed small hidden details (hip balls, chin guard, hoses) to stay under target.
- **White, not grey.** The look was changed to white armour with a black vest on request, after a first grey version; only the build script and the bake colours changed, the joints, size and names did not. Before/after images: `Desktop/Tactical_Guard_Previews/comparison_*.png`.

## Known weaknesses

- Only one level of detail. `RobotLODBuilder` makes LODs for the placeholder only; the FBX needs its own reduced mesh.
- The textures are procedural (edge wear, grime, paint chips), not hand painted. At 487 px/m small details such as the
  vent slats are modelled, not painted.
- The first import must be checked in the Editor: run **Blackout > Art > Build Security Robot Visual Prefab** and
  `RobotModelFbxTests`. I measured the import in a scratch project, not in your open project.
- The scanner dot shares the red eye material (as before), so the amber idle colour is still not shown.
- **The weapon is a white carbine, not the prototype's twin-barrel pod** (my change, from a reference photo). It is built in the hand's own axes so that
  the barrel points down -Y (the way the arm hangs) and the animator's -1.5 rad aim swing turns it forward. Its muzzle brake ends
  at -0.33 m below the wrist, which is where `MuzzlePoint` is created, so the shooter's ray starts at the barrel. The test measures it.
