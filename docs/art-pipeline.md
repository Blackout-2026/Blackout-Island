# Art pipeline: Blender to Unity 6

Owner: Core Developer. Applies to every custom model in the game (generator, security robot, props,
weapon viewmodels). Unity 6000.3.25f1, URP, Blender 4.2 LTS or newer (menu names below are from 4.x).

Reference for sizes and parts: `docs/reference/core-developer.md`. Import rules in this document are
enforced by `Assets/Editor/CoreDeveloper` (section 9), so a model that follows the checklist in section 8
imports with the right settings without clicking anything in the Inspector.

Items marked **(guess)** are my judgement, not measured or taken from the prototype.

## 1. Folders

| What | Where | Rule |
| --- | --- | --- |
| Blender source | `Art/Blender/SM_<Name>.blend` | **Outside `Assets`.** If a `.blend` is inside `Assets`, Unity tries to import it through an installed Blender and every teammate needs Blender. `*.blend1` backups are git-ignored. |
| Exported mesh | `Assets/Models/Custom/SM_<Name>.fbx` | The only place the import rules apply. One FBX per model. |
| Textures | `Assets/Textures/Models/T_<Name>_<Map>.png` | Never inside `Models/Custom`. |
| Materials | `Assets/Materials/Models/M_<Name>_<Part>.mat` | URP/Lit, created in Unity. |
| Visual prefabs | `Assets/Prefabs/Models/<Name>_Visual.prefab` | Visuals, empty sockets and at most one simple collider proxy on the root (no Health, no Rigidbody, no interaction scripts). Other roles nest them. |
| Prop prefabs | `Assets/Prefabs/Props/Prop_<Name>.prefab` | With collider and correct pivot. |

Re-exporting: overwrite the FBX in place so its `.meta` and GUID stay the same and prefabs keep working.
Rename or move assets inside the Unity Project window, never in Explorer, or the `.meta` is orphaned.

## 2. Naming

| Thing | Pattern | Example |
| --- | --- | --- |
| Static mesh (file) | `SM_<Name>` | `SM_Generator.fbx`, `SM_Prop_LabBench.fbx` |
| Category prefixes | `SM_Prop_`, `SM_Weapon_`, `SM_Char_`; the two heroes have no category | `SM_SecurityRobot`, `SM_Weapon_Smg` |
| Material | `M_<Name>_<Part>` | `M_Generator_Body`, `M_Generator_Core` |
| Texture | `T_<Name>_<Map>` | `T_Generator_BaseColor`, `T_Generator_Normal` |
| Texture maps | `BaseColor`, `Normal`, `MetalSmooth`, `Occlusion`, `Emission` | |
| Objects inside the FBX | PascalCase, side as `_L` / `_R` suffix | `Flywheel`, `Thigh_L` |

Rules for names inside Blender:

- The `.blend` has the same base name as the FBX.
- **A Blender material name must equal the Unity material asset name** (`M_Generator_Body`). The importer
  matches slots to materials by name when the prefab is built (section 9), so a typo leaves a slot unassigned.
- No spaces, no Blender defaults (`Cube`, `Cylinder.003`) and no `.001` duplicates in the exported
  hierarchy. The Agent Controller finds moving parts by name; a renamed part breaks the animation.
- Part and joint names that the Agent Controller drives are listed in section 11 (written in step C3).

## 3. Units, scale and transforms

- Blender: Scene Properties > Units > **Metric, Unit Scale 1.0, Length: Meters**. **1 Blender unit = 1 m.**
- Model at real size. Use the dimensions in `docs/reference/core-developer.md` (generator 6.96 x 4.10 x 2.90 m,
  robot 2.38 m tall with antenna). Never fix a size by scaling in Unity.
- **Apply transforms before export:** select everything, `Ctrl+A` > *Rotation & Scale*. Every object must end
  with **rotation 0 and scale 1** (N panel > Item). Do not apply *Location*: a moving part keeps its own origin
  (section 5) and its position inside the model. Set origins first, then apply.
- Apply modifiers that change the shape (Mirror, Bevel, Array) or leave the exporter's *Apply Modifiers* on.
- No negative scale. If a part is mirrored by scale -1, apply scale, then *Mesh > Normals > Recalculate Outside*.

## 4. Axes and FBX export settings

Blender is Z-up, right-handed. Unity is Y-up, left-handed. Do not compensate by rotating the object. Let the
exporter convert and keep these settings fixed.

**File > Export > FBX (.fbx)** (Blender 4.x):

| Panel | Setting | Value |
| --- | --- | --- |
| Include | Limit to | Selected Objects (or Visible, when only the model is visible) |
| Include | Object Types | **Mesh** and **Empty**. Camera, Light, Armature and Other off |
| Transform | Scale | 1.00 |
| Transform | Apply Scalings | **FBX Units Scale** |
| Transform | Forward | **-Z Forward** |
| Transform | Up | **Y Up** |
| Transform | Apply Unit | on |
| Transform | Apply Transform | **off** (experimental, can rotate children) |
| Geometry | Smoothing | Normals Only |
| Geometry | Apply Modifiers | on |
| Geometry | Tangent Space | off (Unity calculates it) |
| Geometry | Loose Edges | off |
| Geometry | Vertex Colors | only if the model really uses them |
| Armature / Bake Animation | | off. Models are static parts, no clips (animation is procedural) |
| Path Mode | | Auto, **Embed Textures off** |

Why: *Apply Scalings = FBX Units Scale* with *Apply Unit* writes a file that Unity reads as exactly metres, and
*-Z Forward / Y Up* is the pair that gives a clean root transform with Unity's *Bake Axis Conversion*.

### Expected orientation (confirm once, step 0 of the checklist)

| | Blender | Unity (expected) |
| --- | --- | --- |
| Up | +Z | +Y |
| Front of the model (what the player or camera sees first) | **-Y** (the face you see in front view, numpad 1) | **+Z** |
| Character's right-hand side | -X | +X |
| Character's left-hand side | +X | -X |

The prototype rules in `core-developer.md` section 0 describe the **Unity** result (robot faces +Z, its weapon
pod on its right arm, generator control face at Unity +X, which is Blender -X). I derived the Blender column
from the exporter's axis conversion but could not run Blender, so it is **unconfirmed**:

1. In Blender make `SM_AxisTest`: a 1 m cube with a 0.5 m cone on its -Y face (front) and a small cube on
   its -X face, plus an Empty named `Marker`. Export with the settings above into `Assets/Models/Custom`.
2. In Unity, drag it into a scene. The cone must point along the blue (Z) axis, the small cube sits on +X,
   the root has rotation (0, 0, 0) and scale (1, 1, 1), and the Console line shows 1.00 x 1.00 x 1.50 m or similar.
3. If the cone points to -Z, change **Forward** in the exporter (not the model, not Unity), repeat, and correct
   the table above. Delete `SM_AxisTest.fbx` and its `.meta` from Unity afterwards. Do not commit it.

## 5. Pivots and origins

The origin of each object is where Unity rotates and places it. Set it in Blender (*Object > Set Origin*)
before applying transforms.

| Kind | Origin | Why |
| --- | --- | --- |
| Floor props and furniture (desk, bench, locker, barrel, tank) | Centre of the footprint, **at floor level** (bottom face, y = 0) | Marker placement and snapping put it straight on the floor. |
| Generator | Centre of the footprint at the **floor** under the skid | The `GeneratorSocket` marker sits on the floor. The prototype origin was the plinth top, see `core-developer.md` 2.2, so subtract that offset when copying heights. |
| Pushable crate, barricade | **Geometric centre** | It is pushed and rotated by physics; a centre pivot keeps the rigidbody centre of mass right. |
| Wall-mounted items (wall monitor, fuse box, locker rack) | Centre of the face that touches the wall | Snaps flush to the wall with no offset. |
| Floating pickups (cure case, medkit) | Centre of the model in X and Z, **base plate underside** in Y | They bob and spin around the vertical axis through the pivot. |
| Characters, robot | Between the feet at floor level, facing the front direction of section 4 | The NavMesh agent's position is its feet. |
| Moving parts (flywheel, beacon, gauge needles, valve wheel, robot joints, bolt handle) | **At the rotation centre**, local axes aligned so the rest pose has rotation (0, 0, 0) | Scripts only set the rotation of the part; no offset maths. |
| Weapon viewmodels | At the grip, muzzle along +Z | The weapon system positions the grip relative to the camera. |

Sockets (`InteractPoint`, `SparkVFX`, `MuzzlePoint`, `WeaponSocket`, ...) are empty child objects. Create them
in the Unity prefab at the positions given in `core-developer.md`. If a position is easier to place in Blender,
export an Empty with exactly the socket's name and the prefab keeps it.

## 6. Triangle budgets

Counts are triangles after Unity's triangulation (quads count as 2). **Target** is what I model for. **Hard max**
is 1.5 x target and the importer prints a warning above it. Targets start from the prototype counts measured in
`core-developer.md` section 1.

| Model | File | Prototype | Target | Hard max | Material slots (max) |
| --- | --- | --- | --- | --- | --- |
| Generator G-1 | `SM_Generator` | 8,840 | **10,000** | 15,000 | 6 |
| Security robot | `SM_SecurityRobot` | 3,216 | **5,000** | 7,500 | 4 |
| Humanoid (if modelled) | `SM_Char_*` | 2,600 to 3,100 | 3,000 | 4,500 | 3 |
| Weapon viewmodel with hands | `SM_Weapon_*` | 530 to 1,700 | 2,000 | 3,000 | 3 |
| Prop, small (< 0.5 m): mug, flask, medkit, cure case | `SM_Prop_*` | | 300 | 450 | 2 |
| Prop, medium (0.5 to 1.5 m): chair, monitor, barrel, crate, locker | `SM_Prop_*` | | 800 | 1,200 | 2 |
| Prop, large (> 1.5 m): desk, lab bench, fuel tank, chamber | `SM_Prop_*` | | 1,500 | 2,250 | 2 |

The importer cannot know a prop's size class from its name, so it checks every `SM_Prop_*` against the **large**
limit (2,250). Keeping small and medium props to their own limit is on me (checklist item).

Justification:

- **Generator 10,000.** The prototype's 8,840 are bare chamfered primitives. In Blender, round shapes (the 2.1 m
  alternator housing, the stack, 6 cylinder heads, 28 fins) need more segments to look smooth up close, and the
  player stands 3 to 8 m from it for the whole mission. There is one generator, so it can afford the most.
- **Robot 5,000, not 3,216.** There are two robots. The robot is built from about 16 separate parts so each
  can be rotated at its joint. Every part is a closed mesh with its own end caps, so the same silhouette costs
  more triangles than one welded mesh. About 1.5 x the prototype pays for that.
- **Frame cost is not the limit.** The worst view is about 8 agents (8 x 7,500 = 60,000 at the hard max) plus
  a room of props (lab: chamber, 4 benches, 3 fridges, a few thousand each), well under 200,000 triangles for
  models. Directional light shadow cascades draw geometry 2 to 4 times, which is why I stay at 1.5 x the
  prototype and not 5 x. The real limits are modelling time, clean topology I can explain in the viva, and
  draw calls (hence the material slot limits above).
- **Hard max = 1.5 x target** is one rule to remember. Going over the target needs a reason, going over
  the hard max needs a reduction pass (LODs, step C5).

Topology rules: quads where the surface bends, triangles only where unavoidable, no n-gons in the final mesh,
no interior faces (a part the player can never see), no hidden duplicate vertices. Bevel edges that catch light
(reads well in the screenshots); do not model what a normal map or a texture can show (screws, panel seams,
gauge dials, labels).

## 7. UVs and textures

- **UV0 required** on every mesh, no overlaps, islands inside 0 to 1, margin 4 px at the final size. The importer
  warns about a mesh without UV0. Mirrored halves may overlap only if a texture is truly symmetric (not the
  robot: its weapon pod makes it asymmetric).
- Same texel density across the parts of one model (use Blender's UV checker grid). Guide: about 300 px per
  metre on the generator, about 600 px per metre on the robot **(guess)**.
- Unity does not generate a second UV set for these models (lightmaps are the World Builder's job).

| Model | Texture size | Maps |
| --- | --- | --- |
| Hero models (generator, robot) | **2048 x 2048** | BaseColor, Normal, MetalSmooth, Emission mask |
| Humanoids, weapon viewmodels | 1024 x 1024 | BaseColor, Normal, MetalSmooth |
| Large and medium props | 1024 x 1024, or 512 | BaseColor (+ Normal if it helps) |
| Small props | 512 x 512 | BaseColor |
| Emission masks, decals | 256 to 512 | |

This matches the project rule "1024, 2048 for hero items" (playbook section 4). Power-of-two sizes only.
One texture set per model, shared by all of its materials, so a hero model costs about 3 maps x 5.6 MB with
compression and mipmaps **(guess, BC7 plus mip chain)**.

Map conventions for URP/Lit:

- **BaseColor**: sRGB, no baked lighting.
- **Normal**: tangent space, OpenGL (Y+) as Blender bakes it, which is Unity's convention. Mark it as *Normal map*.
- **MetalSmooth**: metallic in the **R** channel, **smoothness in alpha**. Smoothness = 1 - roughness. Linear
  (sRGB off). The prototype values in `core-developer.md` section 8.1 convert the same way.
- **Emission**: colours above 1 (the prototype uses values up to 8 for the SMG red dot) are set with an HDR
  colour on the Unity material, not baked in the texture.

## 8. Checklist before export

Tick everything. Step 0 is done once for the whole project.

0. [ ] Orientation calibration with `SM_AxisTest` done and the table in section 4 confirmed.
1. [ ] Scene units: Metric, scale 1.0, Meters. The model's dimensions match the reference (N panel > Item > Dimensions).
2. [ ] Triangle count (Statistics overlay, *Triangles*) is at or under the target in section 6. Small and medium props are under their own limit.
3. [ ] All transforms applied (rotation 0, scale 1); no negative scale; normals face outward.
4. [ ] Origins follow section 5; moving parts have the origin at the rotation centre.
5. [ ] No interior faces, loose vertices, n-gons, or duplicate overlapping meshes (*Select > Select All by Trait* helps).
6. [ ] UV0 on every mesh, within 0 to 1, no overlap, same texel density. UV layout checked with a grid texture.
7. [ ] Object names follow section 2 (PascalCase, `_L`/`_R`, nothing called `Cube` or `.001`).
8. [ ] Material names equal the Unity material assets (`M_<Name>_<Part>`), and the slot count is within the limit.
9. [ ] Hidden helper objects deleted. Only Mesh and Empty objects are selected for export.
10. [ ] Export settings as in section 4 to `Assets/Models/Custom/SM_<Name>.fbx` (overwrite, do not rename).
11. [ ] `.blend` saved as `Art/Blender/SM_<Name>.blend`; no `.blend` inside `Assets`.
12. [ ] After Unity imports: the Console line `[ModelImport]` shows the expected size in metres and the triangle count, and no warnings.
13. [ ] Commit the `.blend`, the `.fbx` and the FBX's `.meta` together.

## 9. What Unity does on import

`Assets/Editor/CoreDeveloper/ModelImportPostprocessor.cs` runs for every model whose path starts with
`Assets/Models/Custom/`. It forces these settings on every import, so they cannot drift between team members:

| Importer setting | Value | Why |
| --- | --- | --- |
| Scale Factor / Convert Units | 1, Convert Units on | With the export in section 4 the file is already in metres. |
| Bake Axis Conversion | on | Root transform stays (0, 0, 0) rotation, scale 1. |
| Import Cameras / Lights | off | Models never bring a camera or light into a scene. |
| Import Blend Shapes, Visibility, Animation | off | Not used. Parts are rotated by script, there are no clips. |
| Normals | Import (Blender's normals) | The shading I modelled. |
| Tangents | Calculate Mikktspace | Matches the Blender normal map bake. |
| Mesh Compression | Off | Small meshes, no visible artefacts to risk. |
| Read/Write | off | Saves memory; nothing reads the mesh at run time. |
| Optimise Mesh | on | Better vertex cache use. |
| Generate Lightmap UVs | off | Not needed (section 7). |
| Materials | Kept inside the FBX (In Prefab), named after the **Blender material** | No files appear next to the FBX. The prefab step (C2, C3) assigns the real `Assets/Materials/Models/M_*` assets by matching these names. |

After the import it prints one Console entry with the total and one line per mesh, for example:

```
[ModelImport] SM_Generator.fbx: 3 meshes, 9,874 triangles (target 10,000, max 15,000), 5 materials, size 6.96 x 4.10 x 2.90 m
  Engine: 4,210 triangles
  Housing: 3,664 triangles
  Flywheel: 2,000 triangles
```

Warnings (yellow in the Console) appear for: triangles over the hard max, more materials than allowed,
a mesh without UV0, a file name without `SM_`, a size that looks like a unit mistake (largest side over 50 m
or under 5 cm), and a `.blend` file placed in `Assets/Models`.

To change a rule, edit the code, not the Inspector: a manual change is reset on the next import. After changing
a rule, run **Blackout > Art > Reimport Custom Models** to apply it to every model and print the reports again.
In batch mode: `Unity.exe -batchmode -nographics -quit -projectPath "<repo>" -executeMethod
Blackout.Editor.ModelReimporter.ReimportAll -logFile -`.

## 10. Hand-off to other roles

- Systems Engineer and Agent Controller nest `Assets/Prefabs/Models/*_Visual.prefab`. They do not edit them.
  If a visual needs changing, ask the Core Developer.
- The World Builder places props from `Assets/Prefabs/Props`. A prop's pivot is on the floor (section 5), so
  its `y` equals the floor height.
- Credits for any third-party mesh or texture go into `docs/credits/core-developer.md` with name, URL and licence.

## 11. Parts and joint names for the Agent Controller

Written in step C3, once the robot's part hierarchy exists. Until then the working names are the prototype's
(`pelvis`, `chest`, `head`, `upperArm`, `foreArm`, `hand`, `thigh`, `shin`, `foot`; see `core-developer.md`
section 3.5), written in PascalCase with the side suffix: `Thigh_L`, `Shin_R`.

## 12. Generator prefab (step C2)

`Assets/Prefabs/Models/Generator_Visual.prefab` is built by **Blackout > Art > Build Generator Visual Prefab**
(`GeneratorPrefabBuilder`). Until `SM_Generator.fbx` exists the tool builds a primitive placeholder with the real
envelope (4.10 m tall, 2.90 m deep, control face at +X), so the Systems Engineer can nest the prefab already.
When the FBX is imported, run the menu command again: only the `Model` child is replaced.

**Re-running is safe.** Sockets, the collider and references that already exist are kept, so a socket you moved by
hand stays where it is. Materials keep every hand edit and only get a texture into a slot that is still empty.
To force a socket back to its default position, delete it in the prefab and run the command again.

### 12.1 Hierarchy

| Child of `Generator_Visual` | Content | Local position (m) |
| --- | --- | --- |
| `Model` | the FBX (or the placeholder), materials replaced by name | origin |
| `InteractPoint` | empty; where the Systems Engineer measures the hold-E distance from | (3.5, 1.3, 0): in front of the gauges and valve wheel |
| `SparkVFX` | a `ParticleSystem`, off until the event, bursts of 40 orange streaks every 0.9 s | (3.3, 1.9, 0): top of the control panel, pointing out and up |
| `HumAudioSource` | looping 3D `AudioSource`, 3 to 30 m, **no clip yet** | (1.6, 1.25, 0): the energy core |

The root has a `BoxCollider` of 6.4 x 3.6 x 3.2 m centred at y 1.8 (the prototype's room collider) and the
`GeneratorVisuals` script, already wired to the `GeneratorDisabled` channel. When the event fires it switches the
core to `M_Generator_CoreOff`, stops the hum and plays the sparks. To test without the event, use the component's
context menu: **Test: Power Down** and **Test: Restore Power**. In Prefab Mode these are real edits of the prefab and
the Scene view's **Auto Save** keeps them, so untick Auto Save first (or run Restore Power) and check
`git status` before committing. Running **Build Generator Visual Prefab** again also puts the core back.

### 12.2 What the Blender model must contain

- **Material names, exactly:** `M_Generator_Body` (olive painted metal), `M_Generator_Metal` (frame, steel, chrome,
  pipes), `M_Generator_Details` (gauges, LEDs, labels, valve wheel), `M_Generator_Glass` (the ribbed tube) and
  `M_Generator_Core` (the energy core). That is 5 slots, one under the limit of 6. `M_Generator_CoreOff` exists
  only in Unity and is swapped in at run time; do not make it in Blender.
- **The energy core is its own material slot** (`M_Generator_Core`), on any object. The prefab finds the object and the
  slot by material, so the object name does not matter. A name that matches none of the five stays as imported and
  the Console prints a warning.
- Origin on the floor under the skid, so the skid's underside is at y 0 (section 5).

### 12.3 Textures

One set shared by `Body`, `Metal` and `Details`, 2048 x 2048, in `Assets/Textures/Models`: `T_Generator_BaseColor`,
`T_Generator_Normal`, `T_Generator_MetalSmooth` and `T_Generator_Emission` (`.png`). Unity fixes the import
settings from the name (`ModelTextureRules`): Normal becomes a normal map, MetalSmooth is linear with smoothness in
alpha. Then run **Blackout > Art > Build Generator Materials** (or the prefab command, which includes it) to fill the
empty slots. `Glass`, `Core` and `CoreOff` stay flat on purpose: they are a tint and an HDR emission colour.

Until the textures exist the materials use the prototype's flat colours (`core-developer.md` 2.4). The core's cyan
emission peaks at about 2 and is my reading of section 8.2, so tune it by eye.

### 12.4 Still to supply

| What | Where | Status |
| --- | --- | --- |
| `SM_Generator.fbx` (+ `Art/Blender/SM_Generator.blend`) | `Assets/Models/Custom` | not made yet, placeholder in use |
| `T_Generator_*` textures (4 maps) | `Assets/Textures/Models` | not made yet, flat colours in use |
| Looping generator hum (CC0 or recorded), 5 to 10 s | the `Clip` of `HumAudioSource` | none; credit it in `docs/credits/core-developer.md` |
