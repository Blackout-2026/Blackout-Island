"""Bakes the heavy sniper's PBR textures and re-exports SM_Weapon_SniperHeavy.fbx (step C9). Owner: Core Developer.

Run after build_sniper_heavy.py (which makes the geometry, the UVs and a first FBX with flat palette materials):
    "D:\Blender\blender.exe" -b Art/Blender/SM_Weapon_SniperHeavy.blend -P Art/Blender/Scripts/bake_sniper_heavy.py -- [wear samples]

The method is bake_smg.py's (wear pass, procedural signals, bake into one 2048 atlas, pack MetalSmooth, collapse to one material).
The rifle has three surfaces: PARKERISED receiver (a dark grey-green phosphate finish with light steel scuffs on the edges and
fine scratches, as on the photo), blackened steel (the SMG's anodised metal) and black polymer (the SMG's polymer). Output:
Assets/Textures/Models/T_WeaponSniperHeavy_{BaseColor,Normal,MetalSmooth}.png, then SM_Weapon_SniperHeavy.fbx with the one
material M_Weapon_SniperHeavy.
"""

import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_sniper_heavy as R  # noqa: E402  (points build_smg's paths at this rifle; builds nothing)
import bake_smg as BS  # noqa: E402  (the shared bake; importing it bakes nothing)

Tree, Signals, lin = BS.Tree, BS.Signals, BS.lin


def build_receiver_mat(mat, wear_image):
    """Parkerised steel: dark grey-green, edge wear down to light steel, fine scratches, grime in the cavities."""
    t = Tree(mat)
    edge, cavity = BS.wear_masks(t, wear_image)
    obj = t.object_coords()
    blotch = t.noise(obj, 60.0, 5.0, 0.6)
    grime = t.noise(obj, 14.0, 4.0, 0.55)
    scratch = t.smooth(t.noise(obj, 220.0, 1.0, 0.7), 0.70, 0.74)
    worn = t.smooth(t.add(t.mul(edge, 1.4), t.mul(t.sub(blotch, 0.5), 0.9)), 0.64, 0.92)
    base = t.lerpc(t.mul(grime, 0.7), t.rgb(lin("#2b2d2c")), t.rgb(lin("#3c3e3d")))
    steel = t.lerpc(blotch, t.rgb(lin("#4f5356")), t.rgb(lin("#767a7d")))
    colour = t.lerpc(worn, base, steel)
    colour = t.lerpc(t.mul(scratch, 0.5), colour, t.rgb(lin("#85898c")))
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.6)))
    metallic = t.lerp(worn, 0.7, 1.0)
    rough = t.add(t.lerp(worn, 0.50, 0.30), t.mul(t.sub(grime, 0.5), 0.2))
    rough = t.add(rough, t.mul(cavity, 0.25))
    height = t.add(0.5, t.mul(t.sub(t.noise(obj, 300.0, 3.0, 0.6), 0.5), 0.05))
    height = t.sub(height, t.mul(scratch, 0.08))
    return t, Signals(colour, metallic, rough, height)


if __name__ == "__main__":
    BS.main(obj_name="SniperHeavy", prefix="T_WeaponSniperHeavy_", final_name="M_Weapon_SniperHeavy",
            material_names=tuple(R.SLOTS), builders=(build_receiver_mat, BS.build_metal, BS.build_polymer),
            bevel_radius=0.0018, ao_distance=0.03)
