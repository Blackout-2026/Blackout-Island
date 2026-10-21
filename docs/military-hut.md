# Military hut entrance and bunker style (step B1)

The bunker entrance is now the downloaded **military hut**, and the hut's textures and props style the rooms inside. The map is unchanged: the hut sits
on D-Bunker's socket (0, 10, 48.2), no room, opening, marker or collider moved, and the old facade boxes keep every collider.

## What the download is

`Military_Hut.zip` (`Military_Hut.fbx` and 20 materials of 1024 maps): an arched (Quonset) hut, 11.3 m wide, 16.4 m long and 5.9 m high, with the door in
a recessed porch, two windows, bollards and a concrete pad. It has 16,796 triangles and one armature with six animations (front door, blast shutters, blast
doors, two cameras, garage door). The game keeps its own doors (the cyberpunk door, `docs/cyberpunk-doors.md`), so none of the animations are used.

## What was done to it

`Art/Blender/Scripts/build_military_hut.py` (Blender 5.2 in the background) writes two models and the textures:

| Output | Content |
|---|---|
| `SM_Hut_Entrance.fbx` | `Hut_Roof` (cut at the compound's retaining wall), `Hut_FrontWall`, `Hut_Porch` (cut at the lobby wall), `Hut_Pad` (the part in front of the wall), `Hut_WindowFrames`, `Hut_Windows`, `Hut_Bollards` (the four beside the path; the one in the middle of the path is dropped), `Hut_Signs`, `Hut_Keypad`. 1,900 triangles. |
| `SM_Hut_Props.fbx` | `Hut_FuseBox`, `Hut_Camera`, `Hut_KeypadProp`, `Hut_SignNotice`, `Hut_SignWarning`: 1 : 1 scale, origin in the middle of the face that touches the wall, facing +Z |
| `T_Hut_<name>_BaseColor/_Normal/_MetalSmooth.png` | 12 materials in `Assets/Textures/Models` (the big ones 1024 px, the small ones halved to 512); `_MetalSmooth` is repacked from the download (R metallic, G occlusion, A smoothness) |
| `Concrete_*.png`, `ConcreteStained_*.png` | tileable sets for the bunker's surfaces (see below) |

Everything that is not seen from the compound is cut: the armature and its doors, shutters and cameras, the inside walls, ceiling, metal ribs, lights and the
rear 3 m of the roof.

- **Scale 1.2**, baked into the mesh: the arch (radius 7.0 m) just contains the lobby's 12 x 3.4 m shell, and the hut's 1.23 x 2.13 m door becomes
  1.48 x 2.55 m, so the porch frames the 1.4 x 2.3 m doorway. The porch is 1.58 m deep and ends at the lobby wall's outer face (z 48.0), where the cyberpunk door
  frame sits; the hut's own door and frame are not used.
- The roof runs from z 46.5 to z 63.2, so it covers the lobby and the head of the ramp and ends inside the retaining wall (z 62 ... 63.4).
- The pad's top is at y 10.06, 2 cm above the entrance apron (y 10.04), so the two do not flicker.

## In the game

`Blackout > World > Build Hut Entrance` (`HutEntranceBuilder.BuildAll`):

- creates `M_Hut_*` (URP Lit, from the three maps) in `Assets/Materials/Models`;
- builds `Assets/Prefabs/Environment/Hut_Entrance.prefab` (one child per mesh; mesh colliders on the roof, front wall, porch and bollards, on the Level layer;
  static; no scripts) and the five `Prop_Hut*` prefabs in `Assets/Prefabs/Props`;
- puts `Hut_Entrance` in Environment under `BunkerExterior/EntranceHut`, and turns off the **renderers** of the old `EntranceExterior` boxes that the hut
  replaces (facade, side walls, roof, parapets, canopy, vents: 24 boxes). Their colliders stay on, so the walkable and blocked space is the same as before.
  The apron, the retaining wall, its pilasters and drains, and the yellow door trim stay drawn.

`Blackout > World > Style Bunker With Hut` (`HutEntranceBuilder.StyleBunker`) runs **Build Material Library** and **Dress Bunker**:

- `Assets/Textures/Environment/Concrete` and `.../ConcreteStained` now hold the hut's roof and pad maps, so every wall and ceiling that was `Concrete` (lobby, ramp,
  security area, corridor) and every floor that was `ConcreteStained` (lobby, security area, corridor) is textured; the lab (tile), the generator room (plate and
  grating) and the ramp floor (plate) keep their own surfaces. The roof and pad maps have worn edges, so a clean 512 px patch of each is mirrored in both
  directions (a mirrored normal map also flips its red and green), which makes the tile seamless.
- Ten rows in the dressing table (`docs/level-layout.md`) place the props: a fuse box in the lobby, security area, corridor and generator room, a camera in the
  lobby, security area and corridor, the keypad and the "NOTICE: keep this door closed" sign by D-Bunker, and a warning sign in the corridor. Dress Bunker
  checks every placement against the walking paths, markers and door sockets, and all ten were accepted.

Both tools can be run again: materials, prefabs and the scene instance are updated in place.
Batch: `Unity.exe -batchmode -nographics -quit -projectPath "..." -executeMethod Blackout.Editor.HutEntranceBuilder.BuildAll` (then `.StyleBunker`).
`HutEntranceBuilder.RenderPreview` (without `-nographics`) writes review pictures of the saved scene (compound approach, porch, corner, side, lobby, ramp,
security area, corridor) to `-previewOut`.

## Tests

`HutEntranceTests` (10): the models have every part and no animation, the 12 materials, the prefab (layers, colliders only on the solid parts, no scripts), the
porch ends at the lobby wall, the arch contains the lobby shell, the layout numbers are unchanged, the hut is placed and every old facade box keeps its collider,
the concrete surfaces are textured and tile without a seam, the props face +Z from their pivot and are in the dressing table, and every prop is in Environment.

## Not checked

- Play Mode: the look under the real (blackout) lighting, and walking round the hut (the hut and its bollards have colliders; the old facade colliders inside it are unchanged).
- The NavMesh was not baked again, so agents still path as before; the next **Bake NavMesh** will treat the hut's walls and bollards as obstacles.
- The windows show the lobby's outer wall behind their (opaque, dark) glass; the hut's own interior is not modelled.
- The light fixtures were not replaced by the hut's fluorescent lamps: the existing lamps carry the blackout's power logic, and a different mesh would need that rewired.
- The normal maps are used as downloaded (OpenGL); the preview shows no inverted bumps, but there was no side-by-side with a flipped map.
- The download has no licence text; the credits rows (`docs/credits/world-builder.md`) have TODO cells for the page URL and licence.
