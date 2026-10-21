# Cyberpunk doors (step D1)

All four bunker doorways (D-Bunker, D-Security, D-Generator, D-Lab) show the downloaded **cyberpunk double door** in place of the grey slab. The map
is unchanged: the doorways are still 1.4 x 2.3 m in a 0.4 m wall, each door is still an instance of `Door.prefab` with the same scene overrides
(label, speed, noise, lock), and `SlidingDoor` still decides when it opens.

## What the download is

`JesusFernandezGarcia_CoolDoorCyberpunk_JFG_1.zip` ("Cyberpunk Sci-Fi Door 3", a Sketchfab free model): one mesh of 24 loose parts, 766 triangles, one
material, four 2048 maps (base colour, normal, emission and an Unreal-style ORM). It is a double door inside a thick frame, 3.3 x 4.4 m, with no
animation, and its pieces are not separate objects.

## What was done to it

`Art/Blender/Scripts/build_cyber_door.py` (run with Blender 5.2 in the background) makes `SM_Door_Cyberpunk.fbx` and `Art/Blender/SM_Door_Cyberpunk.blend`:

| Object | Content | Size (m) |
|---|---|---|
| `DoorFrame` | the ring around the opening, the header lights, the two side blocks | 1.74 wide, 2.5 tall |
| `DoorLeaf_L`, `DoorLeaf_R` | the leaf panels, vents, centre strip and lock; the centre seam is cut so each leaf takes half of the lock | 0.70 x 2.30 each |

- The leaves are scaled to fill the gap exactly: 2 x 0.70 = 1.40 wide, 2.30 tall. The frame is 0.51 m thick, so it stands 5 cm out of each face of the wall;
  the leaves are 0.35 m thick, so they slide inside the wall.
- The origin is the middle of the sill, in the middle of the wall, so the prefab sits on the DoorSocket as the old one did.
- The ORM map (R occlusion, G roughness, B metallic) is repacked into `T_DoorCyber_MetalSmooth.png` (R metallic, G occlusion, A smoothness = 1 - roughness),
  the layout URP Lit and the project's texture rules read. `T_DoorCyber_BaseColor`, `_Normal` and `_Emission` are the unchanged downloads.

## In the game

`Blackout > Systems > Build Cyberpunk Doors` (`DoorVisualBuilder`) creates `M_DoorCyber` (URP Lit, five maps) and rebuilds `Door.prefab`:

- `Model` (the FBX) replaces `Panel`. `DoorLeaf_R` is `SlidingDoor.panel` and `DoorLeaf_L` is the new `mirrorPanel`.
- `SlidingDoor.mirrorPanel` is optional: if set, it slides the same distance the other way, so the leaves part. Left empty, the door works as before.
  `slideDistance` is 0.75 (a leaf plus 5 cm), so the leaves end inside the wall, clear of the gap.
- `Blocker` is a thin kinematic box (1.6 x 2.35 x 0.16, Interactable layer) that stays in the doorway while the door is closed and is switched off at 80 % open,
  as the slab's collider was. The leaves and the frame have no colliders.
- The lock lamp now sits on the wall beside the frame, at eye height: red while locked, green when unlocked.

`DoorVisualBuilder.RenderPreview` (without `-nographics`) writes pictures of the door closed and open, from both sides, in a stand-in wall with the real gap.

## Tests

`CyberDoorTests` (12): the mirror panel slides the other way by the same distance, a combat lock closes both leaves, a door without a mirror panel is unchanged,
the prefab wiring, the leaves fill 1.4 x 2.3, the frame surrounds the gap and stands out of the wall, the open leaves clear the gap, the blocker, no stray
colliders, the material and its five maps, the texture import rules, and that exactly four doorways use the prefab.

## Not checked

Play Mode: the door's look under the bunker lights, and the door noise and speed feel. The normal map is used as downloaded (OpenGL); the preview shows no
inverted bumps, but there was no side-by-side with a flipped map.
