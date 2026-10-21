# First-person weapons and hands (step VM1)

The SMG is the downloaded **MP9**, the knife the downloaded **M9 bayonet**, and the player's two hands the downloaded **first-person arms**
(fingerless tactical gloves, camouflage sleeves). They went into the three viewmodel files that already existed, `SM_Weapon_Smg.fbx`, `_Knife.fbx` and
`_Sniper.fbx`, so the weapon prefabs, the muzzle point, the held positions and the weapon code are the ones the game already used.

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
- **The arms** have no skeleton, so the fingers are bent in the script: everything above the knuckles is curled round a rod of the grip's size
  (the finger arc's radius is a parameter). Each arm is scaled to a real hand (0.19 m), thinned to about 2,400 triangles, cut at the wrist into
  `Glove` (hand and forearm) and `Sleeve` (the camouflage cuff), and moved to the grip: the right hand on the SMG's pistol grip and the left on its foregrip;
  the same on the rifle's pistol grip and forend; on the knife the right hand round the handle (a reverse grip, the thumb towards the pommel) and the left hand
  open at the edge of the picture.
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
- **Framing.** `PlayerViewmodelSwap` holds each weapon in the holder: the SMG at (0.12, -0.09, 0.36), the knife's hand at (0.17, -0.13, 0.37), the rifle as before.
  The Blender script's `FRAMING` table has the same numbers and its previews use the player camera's own field of view, so what it shows is what the player sees.

`Blackout > Systems > Build Viewmodel Hands And Knife Swing` (`ViewmodelBuilder.Build`) adds the two components to the Player prefab and wires them;
`Blackout > Art > Build Weapon Prefabs` and `Use All Weapon Visuals In Player` do the Core Developer's part. All can be run again.
`ViewmodelBuilder.RenderPreview` (without `-nographics`) writes pictures of the Player prefab's camera: each gun, the knife at rest and at four moments of the swing, and
the SMG and knife while sprinting.

## Tests

`ViewmodelTests` (14): the arm part names, hands shown except when sprinting, switching only the arm renderers, the swing starts and ends at rest, it winds up then
slashes left and lands at the hit delay, it is smooth, it turns the right hand and the knife and leaves the left, the Player prefab has both components wired and
every weapon shows both hands, every weapon model has a body and four arm parts and is under budget, the SMG and rifle are held in both hands, the knife in the
right with the left empty, and the materials use the downloaded maps. The existing weapon tests (muzzle, budget, materials) pass with the new models.

## Not checked

- Play Mode: how it feels (framing under the real field of view while walking, jumping and landing, the swing against an enemy, switching weapons).
- The fingers are bent as a block, not each finger separately, and the thumb is not bent; in the pictures the hands read as gripping the guns and the knife
  handle, but a close look at a finger can show it. The arms were not given a skeleton, so there is no idle sway apart from the weapon holder's own.
- The knife swings on every attack, whether or not it hits.
- The downloads have no licence text: the credits rows (`docs/credits/core-developer.md`) have TODO cells for the author, page URL and licence.
