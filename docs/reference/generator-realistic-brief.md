# Generator "G-1": realistic model brief (step C6)

Owner: Core Developer. Replaces the primitive placeholder behind `Generator_Visual.prefab` with a real, UV-mapped
Blender model, `SM_Generator.fbx`. Everything in `docs/art-pipeline.md` (units, naming, import rules, section 12
prefab contract) still applies. This file only says what the new model looks like and why.

## 1. Reference images

Saved in `Art/Reference/Generator/` (outside `Assets`, so Unity never imports them):

| File | Shows |
| --- | --- |
| `ref_01_front_three_quarter.png` | Orange barrel-vault housing from the side: top hatch, awning over a logo plate, five louvre boxes, rusted base plate, elbow pipes |
| `ref_02_front_face_gauges.png` | The round control face: nested ring flanges, big gauge, small gauge, button box, copper stubs, elbow pipes, feet |

These are **style reference only**. We model our own mesh, our own decals and our own paint. We do not trace the mesh,
and we do not reuse its logo or sticker artwork. Credit row: `docs/credits/core-developer.md`.

## 2. What we take from the reference, and what stays from the game

| Feature | Source | Notes |
| --- | --- | --- |
| Thick bolted base plate with wedge feet | reference | Replaces the plain skid. Dark steel, heavy rust at the edges |
| Barrel-vault main housing with a raised top hatch | reference | The engine cover. Painted metal, panel lines, hinges, bolts |
| Awning/flap over a label plate on the side | reference | Our own label: "G-1" and a danger-high-voltage decal |
| Bank of 5 louvred vent boxes in a recessed strip | reference | Real slats modelled as geometry, not a texture |
| Stack of ring flanges around the end drum | reference | Used as the metal ribs around the glass tube below |
| Round control face, 2 gauges, button box, copper stubs | reference | At the **+X end**, as the gameplay contract requires |
| Two elbow pipes into the base plate | reference | Steel, with flanges |
| **Glass tube with a glowing cyan energy core** | **game** | Not in the reference. The blackout effect swaps this slot to off, so it must exist |
| **Exhaust stack on top** | **game** | Keeps the 4.10 m total height and the link to the ceiling pipe |
| **Control face at +X, origin on the floor under the base** | **game** | `GeneratorPrefabBuilder` sockets depend on it |

## 3. Layout, left (-X) to right (+X), real metres

| Section | Length | Notes |
| --- | --- | --- |
| Vaulted engine housing with the vent bank | about 4.6 m | Box lower half, half-cylinder roof, hatch on top |
| Alternator drum with the glass tube and energy core | about 1.6 m | Ribbed glass between metal ring flanges |
| Control face | about 0.5 m | Gauges, buttons, copper stubs, curved hood |

Envelope target: **6.96 m long, 2.90 m wide, 4.10 m tall including the stack**, matching the prototype and the prefab
collider (6.4 x 3.6 x 3.2). The sockets keep their values: `InteractPoint` (3.5, 1.3, 0), `SparkVFX` (3.3, 1.9, 0),
`HumAudioSource` (1.6, 1.25, 0).

## 3.1 Contract with the prefab (unchanged)

- File: `Assets/Models/Custom/SM_Generator.fbx`, source `Art/Blender/SM_Generator.blend`.
- Exactly five material slots: `M_Generator_Body`, `M_Generator_Metal`, `M_Generator_Details`, `M_Generator_Glass`,
  `M_Generator_Core`. The core is its own slot. `M_Generator_CoreOff` is made in Unity, never in Blender.
- Triangle budget: **10,000 target, 15,000 maximum**. Round shapes use 24 to 32 sides, bevels use 1 to 2 segments.
- Two UV channels are not needed. UV0 only, one 2048 x 2048 atlas for Body, Metal and Details, with no overlap.
- Textures: `T_Generator_BaseColor`, `_Normal`, `_MetalSmooth`, `_Emission`.

## 4. How it gets built

Blender 5.2 LTS (`D:\Blender\blender.exe`) is run in batch mode on a Python script, so the result is repeatable and
every function can be explained in the viva:

1. Build the geometry from parametric parts, with bevels, then clean up the topology.
2. Unwrap UVs and pack them into one atlas. Assign the five materials.
3. Export FBX, import it into Unity, rebuild the prefab with **Blackout > Art > Build Generator Visual Prefab**.
4. Bake the four texture maps from procedural materials (edge wear, rust, grime), wire them into the Unity materials.
5. Measure with **Blackout > Art > Model Report** and write the viva notes.

## 5. Known limits

- The reference was painted in Substance. We have no Substance, so the wear comes from Blender procedural masks
  (bevel and ambient-occlusion based) and will be cleaner than hand-painted art. The final pass is by hand.
- Lab-grade realism still depends on lighting and post-processing, which belong to the World Builder.
