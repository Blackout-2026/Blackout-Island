"""Bakes the rifle's PBR textures and re-exports SM_Weapon_Sniper.fbx (step C7). Owner: Core Developer.

Run after build_sniper.py (which makes the geometry, the UVs and a first FBX with flat palette materials):
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Weapon_Sniper.blend -P Art/Blender/Scripts/bake_sniper.py -- [wear samples]

The method is bake_smg.py's (wear pass, procedural signals, bake into one 2048 atlas, pack MetalSmooth, collapse to one material);
the rifle has three surfaces: blackened steel (the SMG's anodised metal), black polymer (the SMG's polymer: butt pad, magazine,
scope glass) and TAN polymer (the stock and grip: matt, fine stipple, darker where it is worn through at the edges, dust in the
hollows). Output: Assets/Textures/Models/T_WeaponSniper_{BaseColor,Normal,MetalSmooth}.png, then SM_Weapon_Sniper.fbx with the
material M_Weapon_Sniper.
"""

import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_sniper as R  # noqa: E402,F401  (points build_smg's paths at the rifle; builds nothing)
import bake_smg as BS  # noqa: E402  (the shared bake; importing it bakes nothing)

Tree, Signals, lin = BS.Tree, BS.Signals, BS.lin


def build_tan(mat, wear_image):
    """Tan polymer stock: matt khaki, fine stipple, the dark base showing through on worn edges, dust in the hollows."""
    t = Tree(mat)
    edge, cavity = BS.wear_masks(t, wear_image)
    obj = t.object_coords()
    blotch = t.noise(obj, 40.0, 5.0, 0.6)
    grime = t.noise(obj, 18.0, 4.0, 0.55)
    worn = t.smooth(t.add(t.mul(edge, 1.4), t.mul(t.sub(blotch, 0.5), 0.5)), 0.55, 0.85)
    base = t.lerpc(t.mul(grime, 0.8), t.rgb(lin("#7c6b4d")), t.rgb(lin("#9a8863")))
    colour = t.lerpc(worn, base, t.rgb(lin("#3a3226")))              # the black-brown moulding colour under the tan
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.45)))
    rough = t.add(t.lerp(worn, 0.62, 0.45), t.mul(t.sub(grime, 0.5), 0.15))
    stipple = t.noise(obj, 1400.0, 1.0, 0.5)
    height = t.add(0.5, t.mul(t.sub(stipple, 0.5), 0.55))
    return t, Signals(colour, 0.0, rough, height)


if __name__ == "__main__":
    BS.main(obj_name="Sniper", prefix="T_WeaponSniper_", final_name="M_Weapon_Sniper",
            material_names=("M_Weapon_Metal", "M_Weapon_Polymer", "M_Weapon_Tan"),
            builders=(BS.build_metal, BS.build_polymer, build_tan))
