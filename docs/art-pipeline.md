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
| Transform | Apply Transform | **on for static meshes with no hierarchy** (the generator): the Z-up to Y-up rotation is baked into the vertices and Unity gets rotation 0. **Off** for anything with a joint hierarchy (the robot): it is experimental and can rotate children. Measured in step C8: with it on, the third level of a chain (`Shin_L`) came out rotated 270 degrees. With it off the root comes in rotated 90 degrees, which section 16.2 cancels in Blender |
| Geometry | Smoothing | Normals Only |
| Geometry | Apply Modifiers | on |
| Geometry | Tangent Space | off (Unity calculates it) |
| Geometry | Loose Edges | off |
| Geometry | Vertex Colors | only if the model really uses them |
| Armature / Bake Animation | | off. Models are static parts, no clips (animation is procedural) |
| Path Mode | | Auto, **Embed Textures off** |

Why: *Apply Scalings = FBX Units Scale* with *Apply Unit* writes a file that Unity reads as exactly metres, and
*-Z Forward / Y Up* is the pair that gives a clean root transform with Unity's *Bake Axis Conversion*.

### Measured orientation (step C6, replaces the earlier guess)

Measured on the generator with Blender 5.2, these export settings and Unity 6000.3.25f1: the energy core modelled at
x = +1.5 arrived at Unity x = +1.5, and the louvre bank modelled on the +z side arrived on the +z side (1,172 vertices on
+z against 56 on -z in the prefab's world space). The rule is a plain axis swap:

    Unity (x, y, z) = Blender (x, z, y)        so       Blender (x, y, z) = Unity (x, z, y)

| | Blender | Unity |
| --- | --- | --- |
| Up | +Z | +Y |
| Along the machine / to the right | +X | +X |
| Forward, the direction a character faces | **+Y** (the back view, `Ctrl+Numpad 1`) | **+Z** |
| Character's right-hand side | +X | +X |

What this means for the next models: **model the front of the robot facing Blender +Y, not -Y**, and put its weapon pod
(Unity +X, the robot's right arm) on Blender +X. The earlier table in this section said front = Blender -Y and right =
Blender -X. That was derived without running anything and is wrong; the generator (control face at Unity +X) is what
proved it. The swap flips handedness, so models are not mirrored in Unity and text reads the right way round.

`build_generator.py` does this conversion in one function (`B()` in `Art/Blender/Scripts/gen_parts.py`), so a part is
written in the same Unity-frame metres as the prefab sockets.

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
| Weapon viewmodel with hands | `SM_Weapon_*` | 530 to 1,700 | 6,000 | 9,000 | 3 |
| Prop, small (< 0.5 m): mug, flask, medkit, cure case | `SM_Prop_*` | | 1,200 | 1,800 | 4 |
| Prop, medium (0.5 to 1.5 m): chair, monitor, barrel, crate, locker | `SM_Prop_*` | | 2,000 | 3,000 | 4 |
| Prop, large (> 1.5 m): desk, lab bench, fuel tank, chamber | `SM_Prop_*` | | 3,000 | 4,500 | 4 |

The importer cannot know a prop's size class from its name, so it checks every `SM_Prop_*` against the **large**
limit (4,500). Keeping small and medium props to their own limit is on me (checklist item).

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

The security robot is rigid parts on pivots, not a skinned rig. The Agent Controller's `ProceduralRobotAnimator`
finds each joint **by exact name anywhere below the visual** and rotates or moves it every frame. The tree below is
the prototype's 16 bones (`prototype/src/models/robot.js`) with X mirrored (section 0 of `core-developer.md`), so the
robot's right arm, which carries the weapon pod, is at +X. The table is `RobotRig.Joints` in
`Assets/Editor/CoreDeveloper`; the placeholder prefab is built from it, and a test compares the animated names with the
fields of `ProceduralRobotAnimator`.

### 11.1 Joint tree, pivots and rest pose

Offsets are the joint's local position in metres relative to its parent (Unity axes: +X is the robot's right, +Y up,
+Z the front). Height is above the floor, which is where the robot's origin is (between the feet).

| Joint (exact name) | Parent | Offset (x, y, z) | Height | Driven by the animator |
| --- | --- | --- | --- | --- |
| `Pelvis` | the model | (0, 1.05, 0) | 1.05 | yes: moves up and down (walk stomp, death drop), pitches forward on death |
| `Spine` | `Pelvis` | (0, 0.14, 0) | 1.19 | no |
| `Chest` | `Spine` | (0, 0.30, 0) | 1.49 | no |
| `Head` | `Chest` | (0, 0.44, 0.02) | 1.93 | yes: pitches on death |
| `UpperArm_L` / `UpperArm_R` | `Chest` | (-0.37 / +0.37, 0.27, 0) | 1.76 | right arm only: raises -1.5 rad to aim |
| `ForeArm_L` / `ForeArm_R` | `UpperArm_x` | (0, -0.34, 0) | 1.42 | yes: left -1.1 rad, right -0.08 rad when aiming |
| `Hand_L` / `Hand_R` | `ForeArm_x` | (0, -0.32, 0) | 1.10 | no. `Hand_R` carries both sockets |
| `Thigh_L` / `Thigh_R` | `Pelvis` | (-0.17 / +0.17, -0.06, 0) | 0.99 | yes: walk swing, death buckle |
| `Shin_L` / `Shin_R` | `Thigh_x` | (0, -0.42, 0.13) | 0.57 | yes: knee bend |
| `Foot_L` / `Foot_R` | `Shin_x` | (0, -0.43, -0.15) | 0.14 | yes: counter-rotates so the sole stays level |

Rules the Blender model has to follow (they extend sections 3 and 5):

1. **Every joint is an object whose origin is the rotation centre**, at the offset above. It can be a mesh object that
   carries that part's mesh, or an Empty with the meshes as its children. Parent the joints exactly as in the table:
   legs and arms nest (`Foot_L` under `Shin_L` under `Thigh_L` under `Pelvis`), so a thigh swing carries the shin and
   foot with it.
2. **Rest pose = rotation (0, 0, 0) and scale 1 on every joint.** The animator saves the rest rotation and adds to it,
   but the digitigrade look has to be in the mesh: the thigh is modelled sloping forward 17 degrees and the shin
   19 degrees backward inside an un-rotated object (the prototype does the same).
3. **Rotation is about each joint's local X axis**, which must be the robot's left-right axis. A positive angle is a
   nose-down pitch in Unity: the front tips down and the free end of a hanging limb swings back. The aim pose of -1.5 rad
   therefore swings the right arm's lower end forward and up. The prototype's angles carry over unchanged because the
   X mirror does not change a rotation about X.
4. **Names are unique in the whole hierarchy** and a mesh part must never be called like a joint. The animator takes
   the first match it finds. Detail parts use any other PascalCase name (`ThighArmour_L`, `HipActuator_R`).
5. `Pelvis` must be a direct child of the model root, because the animator moves it by its **local position**.
6. Keep the head's `ScannerDot` (a 0.016 m sphere at (0, 0.07, 0.15) in `Head`) as its own object: it is the one part
   that slides side to side across the visor. Optional; the animator does not move it yet.

### 11.2 Sockets

| Name | Parent | Local position | Orientation |
| --- | --- | --- | --- |
| `WeaponSocket` | `Hand_R` | (0, 0, 0) | rotation (90, 0, 0): its +Z (`forward`) runs down the barrels |
| `MuzzlePoint` | `Hand_R` | (0, -0.33, 0): 0.33 m below the wrist, between the two barrels | same: `forward` is the shooting direction |

Both follow the arm, so they are in the right place when the pod is raised. Either can also be exported from Blender
as an Empty with exactly that name; the prefab step keeps an existing one.

### 11.3 Notes for the Agent Controller

- The 12 names the animator already uses are all present: `Pelvis`, `Head`, `Thigh_L/R`, `Shin_L/R`, `Foot_L/R`,
  `UpperArm_L/R`, `ForeArm_L/R`. `Spine`, `Chest`, `Hand_L`, `Hand_R` and `ScannerDot` exist too and are not driven yet.
- The prototype also applies small rest offsets that A3 does not: thigh -0.15 rad, shin +0.12 rad, the arms leaning
  out about 0.18 rad, spine and chest lean, pelvis twist. The model rests with all of them at zero.
- `MuzzlePoint.position` is where a shot starts and `MuzzlePoint.forward` is its direction. `AgentShooter` takes the
  muzzle as a serialized `Transform`; wire it to this object when nesting the prefab.
- The `ScannerDot` shares `M_SecurityRobot_Eye` (red). The prototype turns it amber while patrolling. Changing that
  per robot needs a `MaterialPropertyBlock` on its renderer; do not edit the shared material.

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
| `SM_Generator.fbx` (+ `Art/Blender/SM_Generator.blend`) | `Assets/Models/Custom` | done in step C6, see section 15 |
| `T_Generator_*` textures (4 maps) | `Assets/Textures/Models` | done in step C6, see section 15 |
| Looping generator hum (CC0 or recorded), 5 to 10 s | the `Clip` of `HumAudioSource` | none; credit it in `docs/credits/core-developer.md` |

## 13. Security robot prefab (step C3)

`Assets/Prefabs/Models/SecurityRobot_Visual.prefab` is built by **Blackout > Art > Build Security Robot Visual Prefab**
(`RobotPrefabBuilder`). It is visual only: **no collider, no Rigidbody, no Health, no script**. The Agent Controller nests
it under the `Visual` child of `AgentBase.prefab` and puts `ProceduralRobotAnimator` there. Until `SM_SecurityRobot.fbx`
exists the tool builds a primitive placeholder with the real joint tree and size (2.38 m with the antenna, 1.04 m wide,
0.64 m deep, feet on y 0), so the animation, the shooter and the AI can be tested now. When the FBX is imported, run the
menu command again: only the `Model` child is replaced.

**Re-running is safe.** Materials keep every hand edit and only get a texture into a slot that is still empty. While the
same model stays, a socket moved by hand stays where it is. When the placeholder is replaced by the FBX, the sockets are
created again at their defaults, unless the FBX already contains Empties with those names. After the swap the tool prints
a warning for every name of section 11.1 that the FBX lacks, and for every material that matches none of the five below.

### 13.1 Hierarchy

| Object | Content |
| --- | --- |
| `SecurityRobot_Visual` | empty root at the feet, identity transform |
| `Model` | the FBX instance, or the placeholder |
| `Model/Pelvis/...` | the joint tree of section 11.1, with all parts below it |
| `.../Hand_R/WeaponSocket`, `.../Hand_R/MuzzlePoint` | the sockets of section 11.2 |

The placeholder has 81 primitive parts (101 objects in all). That is why it is not meant to ship: the real model is
about 16 meshes.

### 13.2 Materials

Five URP/Lit assets in `Assets/Materials/Models`, created by **Blackout > Art > Build Robot Materials** (or by the prefab
command). Flat colours and values are from `core-developer.md` 3.4 and 8.1.

| Material | Flat placeholder | Used for | Textured |
| --- | --- | --- | --- |
| `M_SecurityRobot_Body` | #4a5056, metallic 0.55, smoothness 0.4 | painted armour, steel parts | yes |
| `M_SecurityRobot_Joint` | #232527, metallic 0.85, smoothness 0.65 | dark metal: joints, pistons, barrels, visor slot, power pack | yes |
| `M_SecurityRobot_Accent` | #c89a24, metallic 0.4, smoothness 0.55 | hazard yellow: chest bars, roof plate, shoulder and thigh stripes | yes |
| `M_SecurityRobot_Eye` | dark red base, **emission HDR (5, 0.35, 0.15)** | eye bar, antenna tip, scanner dot | no |
| `M_SecurityRobot_Cells` | dark cyan base, **emission HDR (0.4, 2.6, 3.0)** | the three back power cells | no |

The importer allows **4 distinct materials in `SM_SecurityRobot.fbx`** (section 6), so the model uses at most four of the
five names. Recommended: **`M_SecurityRobot_Body`, `M_SecurityRobot_Eye` and `M_SecurityRobot_Cells`**, with the joint
metal and the yellow painted into the one BaseColor atlas (that is what the 2048 texture is for). The placeholder uses all
five only because flat colours cannot come from an atlas. Use `Joint` or `Accent` as a fourth slot if you prefer a separate
material for the dark metal or the yellow. A Blender material name must equal one of these five exactly (section 2). The
glow colours are HDR values above 1 on purpose: they feed the bloom, so do not bake them into a texture.

### 13.3 Textures

One set shared by `Body`, `Joint` and `Accent`, 2048 x 2048, in `Assets/Textures/Models`: `T_SecurityRobot_BaseColor`,
`T_SecurityRobot_Normal` and `T_SecurityRobot_MetalSmooth` (`.png`). Unity fixes the import settings from the name
(`ModelTextureRules`). Then run **Blackout > Art > Build Robot Materials**, which fills the empty slots and sets the flat
tint to white. `Eye` and `Cells` stay flat.

### 13.4 Still to supply

| What | Where | Status |
| --- | --- | --- |
| `SM_SecurityRobot.fbx` (+ `Art/Blender/SM_SecurityRobot.blend`) with the names of section 11 | `Assets/Models/Custom` | done in step C8, see section 16 |
| `T_SecurityRobot_*` textures (3 maps) | `Assets/Textures/Models` | done in step C8, see section 16 |

Budget reminder (section 6): 5,000 triangles target, 7,500 hard maximum, 4 materials. The placeholder is about 7,000
triangles because Unity's spheres and cylinders are heavy; it is not subject to the budget.

## 14. Prop kit, signs and weapon viewmodels (step C4)

Everything here is made by **Blackout > Art > Build All Props, Signs and Weapons** (`PropKitBuilder`), which runs the
prop, sign and weapon builders in order. It is safe to run again: a file that would not change is not rewritten, materials
keep hand edits, and a prefab keeps its colliders, sockets and a `Model` child that is already the FBX. The data lives in
`PropCatalog` (name, size, pivot, how many the level uses) and in the definitions next to it (`PropDefinitions`,
`WeaponDefinitions`, `DecalCatalog`). The Props palette (**Window > Blackout > Props Palette**) lists every prefab with the
triangle count measured on the file.

### 14.1 What I model in Blender and what I take from CC0 packs

| Group | Blender (custom low-poly) | CC0 pack | Why |
| --- | --- | --- | --- |
| The six the brief names | **Cure chamber** (`Prop_ContainmentChamber`), **lab table** (`Prop_LabBench`), **crate / barricade** (`Prop_Crate`, `Prop_CrateSmall`), **monitor** (`Prop_Monitor`, `Prop_WallMonitor`), **desk** (`Prop_Desk`, `Prop_DeskGuard`, `Prop_DeskCommander`), **barrel** (`Prop_Barrel`) | | They are the silhouettes in the screenshots and have exact sizes and a colour language; no pack matches the chamber, the strapped crate or the grey-blue desks. The crate and the chamber also need a specific pivot and collider. |
| Rest of the "model" hints in the reference | Locker, electrical cabinet, control console, fuel tank, specimen fridge, fume hood, analyser, microscope, office chair, meeting table, cure case, medkit | | One-off sizes from the prototype's room code. Do these after the six: the placeholders already have the right size and pivot, so nothing waits on them. |
| Fittings | | **Pipes** (`Prop_PipeStraight`), **cable tray and cables** (`Prop_CableTray`), **vents** (`Prop_Vent`), fire extinguisher, flasks, jars | Generic, repeated, and well covered by free packs. Hand-modelling 8 pipe runs and 27 jars would cost more than it earns. |
| Signs | | | Neither: generated as textured quads (section 14.5). |
| Weapons | | **SMG, rifle, knife** from a free low-poly gun pack (`docs/credits/core-developer.md`) | Guns take the longest to model well; the part to add in Blender is the hands and sleeves. |

Not in this step, by design: sandbags, the military truck, guard booth, floodlights, antenna mast, boat and dock (World
Builder dressing, section 7.6 of the reference), the doors (Systems Engineer), the catwalk, stairs and wall fan, the metal
detector, glass partition, weapon rack, CCTV, checkpoint counter, fire-hose cabinet, fuse boxes, and the two wall maps.
Ask if one of them is needed for the level and I will add it to `PropCatalog` and `PropDefinitions`.

### 14.2 What every prop prefab follows

- `Assets/Prefabs/Props/Prop_<Name>.prefab`, units in metres, **the front of a prop looks along +Z**. The World Builder turns
  it with the marker's yaw.
- The root is the pivot of section 5: `BaseCentre` (footprint centre on the floor, so the prop's `y` equals the floor height),
  `Centre` (the crate), or `WallFace` (the wall monitor: origin on the middle of the face that touches the wall, the screen
  looks along +Z).
- One `Model` child (the FBX or the placeholder), **colliders on the root** (box, or capsule for the barrel and the glass
  panels), sockets next to `Model`. No Rigidbody and no script: the Systems Engineer makes a prefab variant of `Prop_Crate` and
  adds the Rigidbody and the push script.
- Pickups (`CureCase_Visual`, `Medkit_Visual`) and weapons (`Weapon_*_Visual`) go to `Assets/Prefabs/Models` because they are
  visual only (section 1): no collider. The Systems Engineer nests them.
- A Blender export is `Assets/Models/Custom/SM_Prop_<Name>.fbx` (a pickup is `SM_Prop_CureCase.fbx`, a gun
  `SM_Weapon_Smg.fbx`). After the import, run the menu command again: only `Model` is replaced, and the Console prints a
  warning if the size or the pivot of the FBX differs from the table below (`PivotRules`, 0.05 m tolerance).
- The Blender material names are the palette: `M_Prop_Steel`, `M_Prop_LabWhite`, and so on (section 14.4). A prop may use at
  most two, which is the importer's limit; a name that matches none stays as imported and the Console warns.

### 14.3 The prop list (placeholders as built)

Sizes are width x height x depth. Triangles are the **placeholder's**, measured by the palette. A Unity cylinder is 80
triangles, so a few placeholders (the cure case, the barrel, the chamber) are heavier than the Blender model will be, and
they use more materials than the two a real prop may have. The budget for a real prop is the size class of section 6.

| Prefab | Size (m) | Pivot | Level count | Placeholder tris | Colliders | Notes |
| --- | --- | --- | --- | --- | --- | --- |
| Prop_ContainmentChamber | 3.8 x 2.88 x 3.8 | base centre | 1 | 812 | 18 | 16 glass panels, 72 degree opening on +Z, `CureAnchor` socket at (0, 1.22, 0). **2.88 m tall** because the lab ceiling is 3.0 (`level-layout.md`). The `CureSocket` marker's 180 degree yaw turns the opening to the D-Lab door. |
| Prop_LabBench | 3.5 x 0.93 x 1.0 | base centre | 4 | 24 | 1 | White base, dark top |
| Prop_Analyser, Prop_Microscope | 0.4 x 0.42 x 0.4, 0.18 x 0.34 x 0.3 | base centre | 4, 4 | 92, 104 | 1 | Stand on a bench (top at 0.93 m) |
| Prop_SpecimenFridge | 1.3 x 2.1 x 0.8 | base centre | 3 | 24 | 1 | Cyan door panel on +Z |
| Prop_FumeHood | 3.0 x 2.6 x 1.0 | base centre | 1 | 48 | 1 | Glass front on +Z |
| Prop_Desk / DeskGuard / DeskCommander | 3.0 / 1.9 / 4.4 wide, 0.78 / 0.78 / 0.85 high, 1.0 deep | base centre | 2 / 1 / 1 | 184 each | 1 | Drawer unit on the right |
| Prop_OfficeChair | 0.5 x 1.03 x 0.5 | base centre | 4 | 184 | 1 | |
| Prop_MeetingTable | 2.0 x 0.76 x 1.0 | base centre | 1 | 332 | 1 | |
| Prop_Monitor, Prop_WallMonitor | 0.7 x 0.75 x 0.2, 1.16 x 0.68 x 0.08 | base centre, wall face | 7, 1 | 48, 24 | 1 | Screen is an emissive material |
| Prop_Locker | 0.52 x 2.0 x 0.55 | base centre | 16 | 24 | 1 | Model once, instance 16 times |
| Prop_ElectricalCabinet | 1.2 x 2.2 x 0.7 | base centre | 6 | 60 | 1 | Panel and 3 LEDs on +Z |
| Prop_ControlConsole | 1.8 x 1.13 x 0.75 | base centre | 1 | 36 | 1 | Sloped top, low edge toward +Z |
| Prop_FuelTank | 2.0 x 2.35 x 4.2 | base centre | 2 | 104 | 1 | Lies along Z |
| Prop_Crate, Prop_CrateSmall | 1.5 cube, 1.0 cube | **centre** | 1, 8 | 36 each | 1 | A clean cube so the physics box matches the mesh |
| Prop_Barrel | 0.6 x 0.9 x 0.6 | base centre | 6 | 240 | 1 (capsule) | |
| CureCase_Visual | 0.42 x 0.28 x 0.24 | base centre | 1 | 676 | 0 | Pivot under the base plate: spin and bob about the vertical axis. Rests on `CureAnchor`. |
| Medkit_Visual | 0.42 x 0.33 x 0.28 | base centre | 3 | 96 | 0 | Red cross on front and back |

### 14.4 Materials

`M_Prop_<Key>` (16, in `Assets/Materials/Models`) is the single prop palette from `core-developer.md` 8.1 and 8.2:
MetalDark, Steel, Chrome, Olive, Locker, LabWhite, PlasticDark, DeskTop, Wood, MedWhite, MedRed, Glass (12 % opacity),
GlowCyan, GlowGreen, GlowScreen and CureVial. The glow ones are HDR emission colours above 1 (the cyan is `#47d1d6` x 2.5,
the vials (0.4, 2.6, 2.8)), so the bloom picks them up. `M_Weapon_<Key>` (9) are the viewmodels': Metal, Polymer, Tan,
Steel, Glove, Sleeve, Lens, RedDot (HDR (8, 0.4, 0.2)) and Flash. All are flat colours until a texture set exists.

### 14.5 Signs and floor markings

19 prefabs `Prop_Sign_<Name>` in `Assets/Prefabs/Props`, one quad each, with a texture `T_Sign_<Name>_BaseColor` in
`Assets/Textures/Models` (18 files: the two hazard stripes share one) and a material `M_Sign_<Name>`: danger high voltage,
two biohazard signs, fire hose, diesel flammable, authorised engineers, all personnel, security checkpoint, level B1,
generator and laboratory arrows, restricted, med-supply (the crate's stencil), private island, the two large wall stencils
(`GENERATOR G-1`, `LAB-01 // PROJECT LAZARUS`), keep clear, and the two hazard-stripe floor markings (ramp 4.0 x 0.4, lab door
3.2 x 0.8). Sizes and colours are from the reference's sign list (7.8).

- A wall sign **faces +Z**, its origin is the middle of the plate, and it sits 2 mm off the wall plane (z = 0.002) so it does
  not flicker against the wall. Place it with the wall's normal as +Z. A floor marking lies flat, facing up, with the top of
  the picture toward +Z, 2 mm above the floor.
- The pictures are drawn by `DecalRenderer` with a 5 x 7 dot font (`BitmapFont`), about 160 pixels per metre, each side a power
  of two, laid out in metres so the lettering is never stretched. The hazard stripes are one 64 x 64 tile repeated every 0.4 m
  by the material's tiling.
- Left out: the bolt icon and the biohazard trefoil (the sign says BIOHAZARD in text), the facility plan and the outbreak
  screen. The tool rewrites a texture only when its own picture differs from the file, so a hand-drawn
  `T_Sign_<Name>_BaseColor.png` placed over a generated one will be overwritten on the next run: keep it under another name
  and point the material at it.

### 14.6 Weapon viewmodels

`Weapon_Smg_Visual`, `Weapon_Sniper_Visual` and `Weapon_Knife_Visual` are ported from `prototype/src/models/weapons.js` with the
camera-space flip `(x, y, -z)`: the prototype looks down -Z and Unity down +Z, so every z and every pitch and yaw angle
changes sign. They are placeholders of 758, 1,134 and 632 triangles (budget 2,000 each) with the gloved hands and sleeves
included. Use a free low-poly gun in their place (credits in `docs/credits/core-developer.md`).

| | SMG (MP-9) | Sniper (R-700) | Knife |
| --- | --- | --- | --- |
| Origin | the prototype's gun frame (receiver centre) | the same | the hand pivot |
| `MuzzlePoint` (its `+Z` is the shot direction) | (0, 0.004, 0.33) | (0, 0.008, 0.81) | none |
| `MuzzleFlash` (hidden, 3 crossed quads, under the muzzle) | yes | yes | none |
| Moving part | none | `BoltHandle` pivot at (0.03, 0.01, -0.08), the bolt slides along Z | `Hand` pivot at the origin: the whole knife swings |
| Held position, before the holder's 0.85 scale | (0.13, -0.125, 0.45) | (0.12, -0.12, 0.46) | (0.17, -0.18, 0.33), rotated (-14, -14, -23) degrees |

**The Blender viewmodels (step C7).** All three weapons now have real models with two realistic hands (the knife has one), built in
`Art/Blender/Scripts` and imported from `Assets/Models/Custom`:

| | Knife | SMG | Rifle |
| --- | --- | --- | --- |
| File | `SM_Weapon_Knife.fbx` | `SM_Weapon_Smg.fbx` | `SM_Weapon_Sniper.fbx` |
| Scripts | `build_knife.py`, `bake_knife.py`, `preview_knife.py` | `build_smg.py`, `bake_smg.py`, `preview_smg.py` | `build_sniper.py`, `bake_sniper.py`, `preview_sniper.py` |
| Triangles (target 6,000) | 3,732 | 5,208 | 5,944 |
| Body material and maps | `M_Weapon_Knife`, `T_WeaponKnife_*` | `M_Weapon_Smg`, `T_WeaponSmg_*` | `M_Weapon_Sniper`, `T_WeaponSniper_*` |
| Origin | middle of the grip, blade along +Z | receiver centre | receiver centre |

Each has 3 material slots: its body material, `M_Weapon_Glove` (the gloved hands and wrists) and `M_Weapon_Camo` (the sleeves). The
**knife** is a tactical fixed blade in the style of the Mil-Tec combat knife (our own mesh, no maker's logo): blackened drop-point
blade that thins to the tip, serrated heel, two round-ended fuller grooves, notched spine, hooked guard, and a black G10 handle with
finger grooves, a notched thumb rest, three star-drive rivets and a lined lanyard hole. The **rifle** is a bolt-action sniper with a
round action and rail, a heavy barrel with a ported muzzle brake, a 30 mm scope with turrets, a tan stock with a separate pistol
grip, a box magazine and a folded bipod; its `BoltHandle` empty marks the pivot of the (modelled-in) bolt handle.

**The hands** come from one shared kit, `hand_kit.py`: a skeleton of points with radii (palm slab, four fingers of three bones, a
thumb, a forearm) is run through Blender's Skin modifier, subdivided and decimated, so the hand has round fingers, knuckles and a
tapering wrist. `grip_frame()` works out how a right or left hand sits on a handle (index finger and thumb on the blade or muzzle side,
the forearm coming from below and behind), so the fingers wrap the right way round. The hands wear coyote-tan tactical gloves with a wrist strap, as in photos of soldiers
with camouflage sleeves, drawn 18 % larger than life so they read on screen. The glove is `make_glove_texture.py`: a seamless 512 px
tile of woven fabric with wear and dirt, tiled over the hands (`T_WeaponGlove_BaseColor` and `_Normal`). Build Weapon Prefabs reads
each material name from the FBX itself (`ModelPrefabBuilder.AssignMaterials`), so a prefab built from an older model version
is corrected on the next run instead of keeping its old materials.

**Blackout > Art > Use All Weapon Visuals In Player** (`PlayerViewmodelSwap`) nests each model in the Player prefab in place of the
primitive stand-ins: the SMG under `SMG`, the rifle under `Rifle`, the knife under `Knife/Hand`. The knife's Hand is moved to
(0.12, -0.10, 0.30) rotated (-10, -30, -25) degrees, because the real knife is longer than the placeholder blade and the old held
position left it mostly below the screen edge. `preview_*.py` renders what the player sees (the game camera is 72 degrees vertical).

**For the Systems Engineer** (nest, do not edit): put each `Weapon_*_Visual` under the weapon prefab's model slot, set its
local position to the held position above (the constants are in `WeaponDefinitions`), start the shot at `MuzzlePoint`, enable
`MuzzleFlash` for a few frames and add the point light yourself. The flash material is `M_Weapon_Flash`, a stand-in for the
prototype's additive flash texture.

### 14.7 Props palette

**Window > Blackout > Props Palette** lists the catalog (props, pickups, weapons, both hero visuals, the 6 CC0 slots and the
19 signs) with, for each prefab that exists: status (Placeholder, Model, Kit, Generated, or Missing), triangles (green at or
under the target of section 6, amber up to the hard maximum, red above it), the budget, materials (amber above the limit),
colliders, the size measured on the prefab (hover for the catalog size), how many the level uses and the triangles of all of
them. Click a row to select the prefab, double-click to open it, drag it into the Scene. **Build all** runs
`PropKitBuilder`. The window never changes an asset on its own.

### 14.8 Shared sign atlas (C5)

**Blackout > Art > Build Sign Atlas** packs the 19 generated decals into `T_Sign_Atlas_BaseColor.png` (2048 x 512), gives each quad a stable UV region in `SignAtlasQuads.asset`, and assigns one `M_Sign_Atlas` material. It bakes the two repeating hazard stripes into their own regions. Four-pixel edge gutters protect the signs from neighbouring colours when filtered. **Build Sign Decals** runs this step after rebuilding the source signs, so it is safe to repeat. A prefab with a material assigned by hand is left alone.

The original 18 sign PNGs and 19 materials remain as source assets; do not delete them with file commands. The atlas cuts material and bound-texture variety on the placed sign prefabs, while draw calls depend on scene batching. The World Builder owns static flags and scene measurements. Use **Blackout > Art > Model Report** to refresh the measured prefab table in `docs/viva/core-developer.md` after a rebuild.

### 14.9 Still to supply

| What | Where | Status |
| --- | --- | --- |
| Blender models of the six required props, then the rest of the "model" list | `Art/Blender/SM_Prop_<Name>.blend`, `Assets/Models/Custom/SM_Prop_<Name>.fbx` | not made yet, placeholders in use |
| CC0 pipes, cable tray, vent, extinguisher, flask, jar as prefabs | `Assets/Prefabs/Props/Prop_<Name>.prefab` (names in `PropCatalog`) | slots are empty, the palette shows them as Missing |
| Free low-poly SMG, rifle and knife meshes with hands | `Assets/Models/Custom/SM_Weapon_<Name>.fbx` | not downloaded yet, placeholders in use |
| Credits rows for everything third-party | `docs/credits/core-developer.md` | template written |

## 15. Realistic generator (step C6)

`SM_Generator.fbx` replaces the placeholder behind `Generator_Visual.prefab`. Size, origin, sockets, material names and
the +X control face are exactly the ones in section 12; only the look changed. Brief and reference images:
`docs/reference/generator-realistic-brief.md`, `Art/Reference/Generator/`.

### 15.1 How it is built (all scripts in `Art/Blender/Scripts`, run in Blender batch mode)

| Order | Script | Does |
| --- | --- | --- |
| 1 | `build_generator.py` (uses `gen_parts.py`) | Builds 171 bevelled parts in Unity-frame metres, merges them, removes faces nobody can see (ray test), triangulates only the n-gons, unwraps UV0 at equal texel density, writes `Art/Blender/SM_Generator.blend`. Prints a warning if the size drifts from 6.96 x 4.10 x 2.90 m |
| 2 | `bake_generator.py` (uses `export_generator.py`) | Bakes an edge and cavity mask, builds the procedural paint, rust and grime from it, bakes the four 2048 maps, rebuilds the Blender materials to read them, exports the FBX |
| 3 | `preview_generator.py` | Review renders from four cameras (optional) |
| 4 | Unity: **Blackout > Art > Build Generator Visual Prefab** | Replaces only the `Model` child. Materials and textures are wired by the existing builder |

```
"D:\Blender\blender.exe" -b -P Art/Blender/Scripts/build_generator.py
"D:\Blender\blender.exe" -b Art/Blender/SM_Generator.blend -P Art/Blender/Scripts/bake_generator.py -- 48
```

The last number is the sample count of the wear pass (48 takes about 2 minutes on the CPU). Close the Unity Editor before
a batch run that touches `Assets`.

### 15.2 Measured result

| Item | Value |
| --- | --- |
| Size in Unity | 6.96 x 4.10 x 2.90 m, the importer line confirms it |
| Triangles | 7,376 (Generator 6,752, EnergyCore 528, GlassTube 96); target 10,000, maximum 15,000 |
| Materials | 5: `M_Generator_Body`, `_Metal`, `_Details`, `_Glass`, `_Core` |
| UV | UV0 only, 0 to 1, no overlap; average 131 px/m at 2048 (dials and labels 3x to 3.5x denser) |
| Maps | `T_Generator_BaseColor`, `_Normal`, `_MetalSmooth` (R metallic, A smoothness), `_Emission`, 2048 x 2048 |
| Prefab node transforms | rotation 0, scale 1 (Apply Transform on, section 4) |

The textures are procedural, so there is no Substance or hand painting: `docs/viva/core-developer-generator.md` explains
the method and its limits.

## 16. Security robot model (step C8)

`SM_SecurityRobot.fbx` replaces the primitive placeholder behind `SecurityRobot_Visual.prefab`. The joint names, offsets,
rest pose, sockets and material names are exactly those of sections 11 and 13; only the look changed. Style: bulky tactical
armour with big round shoulder pauldrons, heavy greaves and a back pack, kept to the prototype's joint layout and size
(section 3 of `docs/reference/core-developer.md`). The shapes are my own. **Colour change, at the owner's request:** the armour
is now WHITE (paint #dcdfe2 to #eceef0, chipping to dark bare metal) with a black vest, black visor, black joints and hands,
styled on a white tactical humanoid reference photo. The prototype's grey-blue body paint (#3a3f44 to #4f565c) is no longer
used. The yellow stays only on the three chest hazard bars and the belt buckle.

**Weapon change, at the owner's request:** the prototype's twin-barrel weapon pod on the right arm is replaced by a compact
white carbine held in a black glove (styled on the same reference photo: white body, black rail, optic, grip, magazine and
muzzle brake). `Hand_R` carries the gun and the glove, `ForeArm_R` is now an ordinary armoured forearm like the left one.
The gun is modelled so that its barrel runs down the hand's -Y axis and its top is the hand's +Z, which makes it point forward
when the animator raises the arm (-1.5 rad), and its muzzle brake ends exactly 0.33 m below the wrist: that is
`RobotRig.MuzzleOffset`, so `MuzzlePoint` sits on the barrel tip. `RobotModelFbxTests` checks this (barrel tip at wrist y - 0.33).
The primitive placeholder (`RobotPlaceholder`) still has the old pod; it is only a stand-in until the FBX is imported.

### 16.1 How it is built (all scripts in `Art/Blender/Scripts`, run in Blender batch mode)

| Order | Script | Does |
| --- | --- | --- |
| 1 | `build_robot.py` (uses `gen_parts.py` for the axis swap) | Builds 132 parts in Unity-frame metres, local to their joint (lofted rounded shells, bevelled boxes, cylinders, hoses). Merges them, removes faces hidden inside their own joint, triangulates, unwraps and packs ONE atlas, then splits the mesh into 16 joint objects plus `ScannerDot`, parents them as in section 11.1 under one Empty and writes `Art/Blender/SM_SecurityRobot.blend`. Prints the size and the triangles per joint |
| 2 | `bake_robot.py` (uses `bake_smg.py` helpers and `export_robot.py`) | Joins a copy of the parts for the bake, bakes edge wear and cavity dirt, builds the procedural paint, steel, hazard stripes and joint metal, bakes three 2048 maps, rebuilds the Blender materials to read them, exports the FBX |
| 3 | `preview_robot.py` | Review renders from seven cameras. `ROBOT_POSE=aim` rotates the joints like the animator does (optional) |
| 4 | Unity: **Blackout > Art > Build Security Robot Visual Prefab** | Replaces only the `Model` child and creates the sockets under `Hand_R` |

```
"D:\Blender\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_robot.py
"D:\Blender\blender.exe" -b Art/Blender/SM_SecurityRobot.blend -P Art/Blender/Scripts/bake_robot.py -- 24
```

Always run `build_robot.py` before `bake_robot.py`: the bake needs the face-corner colour `Mask` that the build writes (it
marks yellow, steel and hazard stripes) and removes it before the export. Close the Unity Editor before a batch run that touches `Assets`.

### 16.2 Why the Blender file is built this way (measured, not guessed)

The animator needs every joint at rotation (0, 0, 0) with the offsets of `RobotRig.Joints`. A scratch Unity project with the
project's import settings showed that neither FBX export mode gives that for a hierarchy (section 4). The fix:

- The root is an **Empty** `SecurityRobot`, rotated +90 degrees about X. The exporter's own axis conversion turns that into
  rotation 0. Everything below it is modelled in that rotated frame: `local = (unity x, unity y, -unity z)`, and the world
  frame is still the axis swap of section 4. The Empty is needed because Unity merges a lone top-level mesh into the model
  root, which would leave no object called `Pelvis` and lift the whole robot 1.05 m.
- `Pelvis` is therefore a direct child of the model, and every other joint sits under its parent at exactly the table offset.
- The test `RobotModelFbxTests` repeats this check on the real FBX (offsets, rotation 0, X axis, size, budget, aim pose).

### 16.3 Measured result

| Item | Value |
| --- | --- |
| Size in Unity | 1.068 x 2.370 x 0.635 m (x -0.528 to 0.540, y 0.008 to 2.378, z -0.375 to 0.260); catalog 1.07 x 2.38 x 0.64 |
| Triangles | 4,740 in 17 meshes; target 5,000, maximum 7,500 |
| Materials | 4: `M_SecurityRobot_Body` (white armour, yellow bars and bare steel, all from the atlas), `_Joint` (black vest, visor, joints and hands), `_Eye`, `_Cells` |
| UV | UV0 only, 0 to 1, no overlap; 518 px/m at 2048 |
| Maps | `T_SecurityRobot_BaseColor`, `_Normal`, `_MetalSmooth` (R metallic, A smoothness), 2048 x 2048 |
| Rest pose | every joint rotation 0, scale 1; offsets equal `RobotRig.Joints` |
| Sockets | not in the FBX; `RobotPrefabBuilder` creates `WeaponSocket` and `MuzzlePoint` under `Hand_R` |

`M_SecurityRobot_Accent` stays in Unity but is unused by the FBX (the yellow lives in the Body atlas).
`RobotLODBuilder` makes no LODs for an imported FBX (it prints a warning and removes the placeholder's LODGroup), so the
robot has one level at 4,740 triangles. A reduced LOD mesh is still to do.

### 16.4 After swapping the placeholder for the FBX (read before the demo)

`TacticalGuard.prefab` (the Agent Controller's, `Assets/Prefabs/Agents`) points its `AgentShooter.muzzle` at the placeholder's
`MuzzlePoint`. **Blackout > Art > Build Security Robot Visual Prefab** replaces that object, so the link can go empty, and
the shots would then start at eye height instead of the barrels. `AgentSetupBuilder` does not rebuild an existing prefab, so
re-wire it by hand: open `TacticalGuard.prefab`, select the root, drag `Visual/SecurityRobot_Visual/Model/.../Hand_R/MuzzlePoint`
into the `Muzzle` field of `AgentShooter`, save. This file belongs to the Agent Controller: tell them, or ask them to do it.
The animator only raises the right arm and bends the left forearm to aim (no arm swing while walking, `ProceduralRobotAnimator`).
