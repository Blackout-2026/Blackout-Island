# Viva notes: the three humanoid agents (Core Developer, step C10)

## What I built
- `SM_Char_Patrol`, `SM_Char_Hunter`, `SM_Char_Commander`: one shared body and skeleton, three costumes and weapons, 1.72k to 2.87k
  triangles each (target 3,000), 3 flat materials each, 1.87 m tall, feet on the floor, facing +Z.
- Unity tools: `CharacterMaterialBuilder`, `CharacterPrefabBuilder`, `CharacterVisualSwap` (menu **Blackout > Art > Use Character Models In
  Agents**) and `CharacterFbxTests`.
- Reference photos from the owner drove the look (a tactical operator, a camouflaged field soldier, an officer in white). The shapes are my own.

## Why this design
- **Reuse of the robot's rig method.** It was measured, not guessed: a scratch Unity project showed that a joint hierarchy only arrives at
  rotation 0 with the Empty root trick, so the three humans use the same one.
- **One body, three costumes.** The prototype shares one skeleton between the three humans, so one body builder with different clothing
  parts gives consistent proportions and the same joint names for any future animator.
- **Rigid parts, not a skinned rig.** I can build and test this today. A skinned humanoid would need weights and a matching Avatar and clips,
  which were not available. The cost: no walking animation yet (see below).
- **Flat colours, no textures.** The characters are seen from 10 to 30 m, where silhouette and colour matter more than surface detail
  (docs/reference/core-developer.md 4.6), and it keeps them well under the triangle budget.
- **MuzzlePoint is an Empty in the FBX**, so `CharacterVisualSwap` can point the Agent Controller's shooter at the real muzzle.

## One number and why
- **A camouflage texture on the Hunter's uniform only.** His reference photo is a field soldier in a blotchy camouflage suit; flat colour cannot
  show that, so the uniform and cap use a seamless 512 px dark forest camouflage tile (`T_CharHunter_BaseColor`, `make_hunter_camo.py`, fixed seed, one
  tile per metre via a cube projection). About 70 leaf and cloth strips (6 triangles each, fixed random seed) hang from the shoulders, arms, waist, thighs, cap and rifle, like the scraps on a field soldier's suit. The other materials stay flat. The arms are modelled bent (the joints keep the rig table's places)
  so he holds the rifle in both hands; the rifle sits 0.12 m to the right of the socket, and `MuzzlePoint` moves with it.

## Known weaknesses
- No animation: the models are rigid and have no clips. They stand upright and slide until a procedural humanoid animator exists.
- The Patrol and the Hunter are much less detailed than their photos (flat colours, boxy parts). Only the Commander got the realistic treatment.
- I replaced the capsules inside the Agent Controller's three prefabs through a menu tool. The change is the removed capsule, the nested model
  and one muzzle reference, but they should know.

## The hero Patrol operator (steps PS1 to PS3) - added at the owner's request

**What changed and why.** The Patrol was an old navy operator, and in fact the game still showed the blue capsule: `SecurityPatrol.prefab` had never been swapped. In PS1 the owner asked
for a camouflaged soldier with a tan SCAR-style rifle, in PS2 for a desert-tan tactical operator from a new photo. My rigid-box version looked like building blocks next to that photo,
so in PS3 the Patrol got the Commander's hero treatment: `build_operator.py` builds it, `build_humans.py -- Patrol` hands over to it, and the existing menu
**Blackout > Art > Use Character Models In Agents** nests it in the prefab.

**Design decisions I can defend.**
- **Reuse the Commander's engine, not a copy.** `build_operator.py` imports `build_commander.py` (sweeps, shells, plates, custom normals, per-joint split, FBX export), and I added three
  backward-compatible hooks to it (per-face UVs, a configurable root/materials/muzzle, a neutral material fallback). The Commander's defaults do not change.
- **Same rig, new surface.** Same 18 joints, rotation 0 and MuzzlePoint contract (the flash hider ends at 0.745 m, MuzzlePoint is 0.74 m ahead of the socket), so `CharacterFbxTests`, the swap tool
  and the shooter wiring needed no changes. The photo is an unarmed A-pose, so the rifle and the bent-arm pose are my assumption.
- **Detail from textures, not triangles.** A 1024 atlas holds swatches and painted decals (vest webbing, pouches, radio, patch, zip, rail teeth, holster, boot laces) and the balaclava face with
  painted eyes, painted by `make_patrol_textures.py` with the same planar projection the head uses. The tan cloth is a 512 tile box-projected at one tile per metre.
- **Two materials.** `M_Char_Patrol_Cloth` and `M_Char_Patrol_Kit` (with a metal map for buckles and the rifle), so the third material slot is free.
- **A hero budget.** 12,000 triangles (18,000 maximum) for this one character (three patrols in the level), like the Commander's 14,000; `ModelBudget` and its test know about it.
- **Retiring materials safely.** Old materials are removed by an obsolete list in `CharacterMaterialBuilder`, which calls `AssetDatabase.DeleteAsset`, so the .meta files and references go with them.

**One number and why.** 11,582 triangles against a 12,000 target: the head, chest and hips take most of them (1,828, 1,700 and 944), because that is where the eye goes at 10 to 30 m.

**Known weaknesses.**
- Rigid parts again, so no walking animation: the operator slides along its path like the other humans.
- It is still a game character, not a scan: the face is painted on a simple head, the hands are blocky, the cloth is smooth tubes with creases and the helmet rails are boxes with painted teeth.
- The decals are generic painted gear, and I have seen the model in Blender renders and Unity tests, not yet in a Play Mode fight.
- The rifle's stock sits inside the vest volume (hidden), because the arms and the Gun socket are fixed by the rig table.

## The realistic Commander (step C10b) - added at the owner's request

**What changed and why.** The owner asked for the Commander to be "really, really realistic" like a photo of a naval officer in white service
dress. The rigid-box method cannot do that, so the Commander has his own pipeline (`build_commander.py` with `commander_head.py`,
`commander_uniform.py`, `commander_atlas.py` and `make_commander_textures.py`) and `build_humans.py` hands him over. Patrol and Hunter did not change.

**Design decisions I can defend.**
- **Same skeleton, new surface.** He keeps the 18 joints, their heights and rotation 0, and the Empty-root trick, so the Agent Controller's tools, the swap
  menu and `CharacterFbxTests` work unchanged. Only the meshes are different (swept tubes and shells with smooth custom normals).
- **Custom normals, cut per joint.** Shading is computed per part and carried through the cut into joints, so there is no lighting seam at
  the waist or the elbow while the shoulder boards and the buckle keep crisp edges.
- **Textures where detail matters.** Three materials: uniform atlas (swatches plus painted ribbons, boards, name tag, cap visor and device, with a metal map so the gold
  is metallic), skin (the painted face and freckled arms) and hair (alpha-clipped cards). The face is painted with the same planar projection the head mesh uses, so eyes and lips land
  where the mesh has them: that shared layout (`commander_atlas.py`) is the one idea to remember.
- **A hero budget.** 14,000 triangles (21,000 max) and 3 materials for this one character instead of the 3,000 for the other two; he is one agent in the
  level, so the cost is small. The test and `ModelBudget` know about it.
- **Pose.** At the owner's second request the cap is on her head and she aims a pistol with both hands in front of the chest (muzzle 0.22 m
  ahead of the Gun socket, so the existing `MuzzlePoint` contract and test hold). The arms are modelled bent while the joints stay where they are.

**Known weaknesses (say these before they ask).**
- It is stylised realism, not a scan: the face is built from landmark tables and Gaussian features and painted, there are no blend shapes, and
  there is no animation (same as the other humans). The cap and the hands are rigid.
- The face and the hair are generic; the insignia (three gold stripes and a star, a ribbon rack) are generic too, and the name tag says COMMANDER.
- Hair uses alpha clipping, so thin strands can shimmer at a distance without MSAA.
- I could not run Unity (the Editor was open), so the Unity changes (`ModelBudget`, the alpha-clip and double-sided options of `ModelMaterialSpec`, the
  new materials, the test changes) are written but not run; the exported FBX was re-imported into Blender and checked, not into Unity.
- I changed one existing test assertion: the muzzle must be 0.35 m (was 0.5 m) in front of the hips, because the Commander's pistol muzzle is 0.42 m from
  the origin; the old limit could only fail for him.

