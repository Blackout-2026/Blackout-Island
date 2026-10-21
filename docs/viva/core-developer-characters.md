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

## The camo Patrol soldier (step PS1) - added at the owner's request

**What changed and why.** The owner supplied a photo of a camouflaged soldier and one of a tan SCAR-style rifle and asked for that soldier,
holding that rifle, as the Security Patrol's model. The Patrol was an old navy operator, and in fact the game still showed the blue capsule:
`SecurityPatrol.prefab` had never been swapped. `build_humans.py` now builds the soldier, and the existing menu **Blackout > Art > Use Character
Models In Agents** nests it in the prefab.

**Design decisions I can defend.**
- **Same rig, new costume.** Same 18 joints, rotation 0 and MuzzlePoint contract (0.74 m ahead of the socket; the flash hider ends at 0.745 m), so
  `CharacterFbxTests`, the swap tool and the shooter wiring did not need changes.
- **Same bent-arm pose as the Hunter.** Both hands hold the rifle, so the arms are modelled bent while the joints stay put.
- **Two textures, three materials.** A seamless woodland camo (`make_patrol_camo.py`) for the uniform and helmet cover, and a 64 px four-swatch atlas
  (`make_patrol_kit.py`: skin, rifle tan, vest olive, dark) so face, vest, rifle furniture and boots share one `M_Char_Patrol_Kit`; the third material
  is the flat black metal. That keeps the 3-material budget with a bare face and a two-tone rifle.
- **Own shapes.** The rifle is my own SCAR-style box model (rails, flip-up sights, folding stock, straight magazine), with no markings.

**One number and why.** 2,792 triangles against a 3,000 target (4,500 maximum): enough for a helmet with skirts, a vest with pouches and a
railed rifle, and still cheap for three agents on screen.

**Known weaknesses.**
- Rigid parts again, so no walking animation: the soldier slides along its path like the other humans.
- The helmet and rifle are boxy at close range; the face is a few blocks with dark eyes, not a painted face.
- The camo is generated noise, not a real camouflage pattern, and the model has been checked in Blender renders and Unity tests, not yet in a Play Mode fight.
- The rifle's stock sits inside the vest volume (hidden), like the Hunter's, because the arms and the Gun socket are fixed by the rig table.

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

