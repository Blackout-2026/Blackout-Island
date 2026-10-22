# First-person weapons and hands (steps VM1, VM2)

The SMG is the downloaded **MP9**, the knife the downloaded **M9 bayonet**, and the player's two hands the downloaded **first-person arms**
(fingerless tactical gloves, camouflage sleeves). They went into the three viewmodel files that already existed, `SM_Weapon_Smg.fbx`, `_Knife.fbx` and
`_Sniper.fbx`, retaining the existing weapon prefabs, muzzle contracts and weapon code. VM2 below adjusts the two guns' held poses.

For the rifle's held right-mouse scope, see [Sniper hold-to-scope (SC1)](sniper-scope.md). It changes the camera/HUD and temporarily hides these meshes;
the approved gun and arm transforms stay unchanged.

## What the downloads are

| Download | What it is |
|---|---|
| `mp9-free` | The MP9, 15,001 triangles in 5 loose parts (body, bolt, magazine, two triggers), a long extended stock, 4096 maps (base colour, normal, metalness, roughness, ambient occlusion) |
| `m9-bayonet-knife` | The M9, 1,637 triangles, one mesh, 2048 maps |
| `first-person-arm-hand-fingerless-gloves` | One mesh of two forearms and hands, 16,432 triangles, **no skeleton**, in an open-hand pose; a 4096 base colour and a 4096 normal map (the file called `canvas.png`) |

## What `Art/Blender/Scripts/build_viewmodels.py` does

Run with Blender 5.2 in the background (`blender -b --factory-startup --python build_viewmodels.py -- D:/model <repo>`).

- **The MP9** loses its stock (the player never sees it), is scaled so the 5.0 units from muzzle to the back of the receiver are 0.30 m, turned to face +Y
  (+Z in Unity), thinned to 4,200 triangles, and placed so the muzzle is at 0.33 m, where the old SMG's `MuzzlePoint` and flash already are.
- **The M9** is turned so the tip faces forward; its origin is the grip, the tip is 0.232 m ahead (the old knife's was 0.226).
- **The arms** have no skeleton. On the guns, VM2 keeps their original proportions and open finger shapes: only rigid placement/rotation,
  the existing uniform real-world scale (0.19 m hand) and reduction to about 2,400 triangles per arm are applied. The right hand sits against the rear grip,
  the left supports the MP9's foregrip or the rifle near its magazine. Each arm is split into `Glove` (hand and forearm) and `Sleeve` (camouflage cuff).
  The knife keeps VM1's finger curl around the handle and its free left hand.
- **Textures.** The downloaded maps are halved to 2048; the mask maps are repacked into `T_Weapon<Name>_MetalSmooth.png` (R metallic, G occlusion, A smoothness).
  The hands use one new material, `M_Weapon_Hands`, for glove, skin and sleeve; each weapon keeps its own body material (2 of the 3 slots).
- **Names.** The files keep the objects the game looks for: the body (`Smg`, `Knife`, `Sniper`) and `GloveR`, `GloveL`, `SleeveR`, `SleeveL`.
  (Blender makes object names unique in a file, so the script gives each weapon's objects their names just before exporting it; without that the second
  weapon's arms were called `GloveR.001`.)

The numbers: SMG 9,000 triangles (gun 4,200 and four arm parts), rifle 8,568, knife 6,436. The viewmodel budget is now **10,000** (hard maximum 15,000),
up from 6,000, because the arms are the one thing always on screen (`ModelBudget`, `PropCatalog`, the budget tests and `docs/art-pipeline.md` agree).

## In the game

- **Both hands are always seen**, with whichever weapon is held, except while the player **sprints**. `ViewmodelHands` (on the `WeaponHolder`) switches the
  renderers of every mesh of the holder, the hands **and the gun or knife** (F2: a weapon hanging in the air without hands looked wrong), off while `PlayerMotor.State`
  is `Sprint` and on again afterwards. A weapon that is not held stays hidden as before, and a renderer that its prefab keeps off stays off.
- **The SMG and the rifle are held in both hands**; the **knife is held in one**.
- **The knife swings.** `KnifeSwing` (on the `Knife`) starts on `MeleeWeapon.Swung`, which `Fire()` raises on every attack. It gathers the knife, the right glove
  and the right sleeve into one `Swing` group at run time and turns the group about the elbow, so the whole forearm swings while the left hand stays: a short
  wind-up up and to the right, a fast slash across the view to the left that lands at the weapon's hit delay (0.11 s, the moment `MeleeWeapon` damages what is in
  front), a follow-through, and the return to rest, 0.36 s in all, inside the knife's 0.48 s cooldown. It only moves meshes: what the knife hits is unchanged.
- **Framing (VM2).** `PlayerViewmodelSwap` holds the SMG at (0.16, -0.10, 0.28), rotated (3, -16, -4) degrees, and the rifle at (0.12, -0.14, 0.58),
  rotated (-5, -20, -4) degrees. The knife's hand stays at (0.17, -0.13, 0.37).
  The Blender script's `FRAMING` table has the same numbers and its previews use the player camera's own field of view, so what it shows is what the player sees.

`Blackout > Systems > Build Viewmodel Hands And Knife Swing` (`ViewmodelBuilder.Build`) adds the two components to the Player prefab and wires them;
`Blackout > Art > Build Weapon Prefabs` and `Use All Weapon Visuals In Player` do the Core Developer's part. All can be run again.
`ViewmodelBuilder.RenderPreview` (without `-nographics`) writes pictures of the Player prefab's camera: each gun, the knife at rest and at four moments of the swing, and
the SMG and knife while sprinting.

## Original model arms with revised placement (VM2)

The reference image shows the support forearm entering from the lower left, the firing hand at the rear grip, and the rest of the arms cropped by the screen.
The MP9 and rifle use the downloaded arms, repositioned and rotated as whole pieces. The MP9 sits closer to the camera; the rifle points diagonally up and left.
Both keep the existing gun bodies, materials, muzzle sockets and nested prefab references. The knife keeps its previous pose and swing.

`build_viewmodels.py` places each gun arm through one rigid transform. Its `GRIPS` table sets the position, palm normal and direction from wrist to fingers;
`curl: False` keeps the source finger shape. The glove and sleeve receive the same transform so the cuff seam stays together. The arms are not stretched,
warped or given a new wrist bend. This is a static mesh pose with no runtime IK or per-frame work. The exports remain 9,000 and 8,568 triangles.

The SMG support hand is rotated inward and moved slightly back so its thumb rests against the receiver above the foregrip rather than sticking out beside it.
The approved rifle pose and both right hands are retained. `VM_EXPORT_ONLY=Smg` limits exports to the SMG while still saving all three weapons in the combined
Blender source; use it with `VM_SKIP_TEXTURES=1` for an SMG-only placement adjustment. No finger bending or arm deformation is added.

To rebuild only the two guns and the combined Blender source, leaving the knife FBX and textures untouched, run from PowerShell:

```powershell
$env:VM_GUNS_ONLY = '1'
$env:VM_SKIP_TEXTURES = '1'
& 'C:\Program Files\Blender Foundation\Blender 5.2\blender.exe' -b --factory-startup --python 'Art/Blender/Scripts/build_viewmodels.py' -- 'D:/model' 'C:\GV , IS Assignment\Blackout-Island'
Remove-Item Env:VM_GUNS_ONLY, Env:VM_SKIP_TEXTURES
```

Then run **Blackout > Art > Use All Weapon Visuals In Player**. It updates the saved Player prefab's two gun transforms while retaining the existing
visuals, components and references. The gun-drop prefab hides the same named arm meshes as before.

Five-line viva summary:

- Does: holds both guns with a wider support arm and partially visible forearms.
- Design: retain the source arm shape and change only rigid placement/rotation through the existing art pipeline.
- Alternative: runtime inverse kinematics would add a rig and execution code to a static visual adjustment.
- Number: review at the existing 72-degree vertical camera FOV and 16:9 aspect; cropping depends on that framing.
- Weakness: the source hands are open; placement hides most fingertips behind the gun, without an animated or fully closed finger grip.

## Tests

`ViewmodelTests` (14): the arm part names, hands shown except when sprinting, switching the arms and weapon together, the swing starts and ends at rest, it winds up then
slashes left and lands at the hit delay, it is smooth, it turns the right hand and the knife and leaves the left, the Player prefab has both components wired and
every weapon shows both hands, every weapon model has a body and four arm parts and is under budget, the SMG and rifle are held in both hands, the knife in the
right with the left empty, and the materials use the downloaded maps. The existing weapon tests (muzzle, budget, materials) pass with the new models.

VM2 verification of this original-shape placement: Unity 6000.3.25f1 import/framing exit 0. The existing Unity camera-preview tool rendered nine
1280 x 720 images, exit 0; both saved gun views were inspected. Evidence: `%TEMP%/blackout-VM2-original-arms-import.log` and
`blackout-VM2-original-arms-unity-preview/`. Focused EditMode tests passed 40/40 (ViewmodelTests, WeaponDropTests, WeaponPrefabTests), exit 0;
evidence: `%TEMP%/blackout-VM2-original-arms-editmode.{log,xml}`. The Player prefab and both model metadata files stayed byte-identical after repeat import.
A repeated Blender build and geometry audit exited 0: all ten meshes retain topology and UVs, arm coordinates
are identical across builds, and rifle-body round-trip drift stays below 0.000002 m relative to the committed mesh. Evidence:
`%TEMP%/blackout-VM2-original-arms-geometry/geometry-audit.json` and `blackout-VM2-original-arms-audit.log`.
Existing licensing-token diagnostics remain; licensed-account batch runs were used. These checks cover selected contracts and static framing, not full gameplay.

SMG thumb follow-up evidence: `%TEMP%/blackout-VM2-smg-thumb/`. `preview.log` records a successful Unity camera render (exit 0); `unity-preview/smg.png`
was inspected. `mesh-audit.json` verifies a rigid change to GloveL/SleeveL only, unchanged topology/UVs, and identical SMG body/right-arm coordinates.
The rifle FBX, knife FBX, Player prefab and existing model metadata match the hashes captured before this adjustment. `contact-check.log` records the sampled
source thumb tip within 0.34 mm of the gun surface; that geometric sample supplements the rendered preview and does not verify an animated grip.
Focused EditMode tests for the final thumb placement passed 40/40, exit 0 (`editmode.log` and `editmode.xml` in the same folder).

Before committing, manually check Bootstrap: acquire both guns, inspect the pose at your usual aspect ratio, fire and switch them, sprint and stop, crouch/prone,
die/respawn and restart. Check the knife swing, dropped guns without arms, the muzzle flash and Console. Other aspect ratios or camera FOVs may crop differently.

## Not checked

RL1 adds separate rigged copies for gun reloads only; see [player-reloads.md](player-reloads.md). The original holding models below remain static.

- Play Mode: how it feels (framing under the real field of view while walking, jumping and landing, the swing against an enemy, switching weapons).
- Gun fingers retain the source open pose; the knife fingers retain VM1's block curl. Neither has animated finger grips. The arms have no skeleton,
  so there is no idle sway apart from the weapon holder's own.
- The knife swings on every attack, whether or not it hits.
- The downloads have no licence text: the credits rows (`docs/credits/core-developer.md`) have TODO cells for the author, page URL and licence.
