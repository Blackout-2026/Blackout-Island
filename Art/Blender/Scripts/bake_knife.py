"""Bakes the knife's PBR textures and re-exports SM_Weapon_Knife.fbx (step C7). Owner: Core Developer.

Run after build_knife.py (which makes the geometry, the UVs and a first FBX with flat palette materials):
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Weapon_Knife.blend -P Art/Blender/Scripts/bake_knife.py -- [wear samples]

The method is bake_smg.py's (wear pass, procedural signals, bake into one 2048 atlas, pack MetalSmooth, collapse to one material);
only the two surface looks differ:
  * the blade, guard and rivets are BLACKENED STEEL: a satin dark-grey coating that has worn through to bright steel along the
    edge, the guard hook and the serrations, with fine brushed scratches and a little grime in the grooves;
  * the handle is BLACK G10: matt, with a fine stipple and the wavy diagonal cut lines of the reference knife.
Output: Assets/Textures/Models/T_WeaponKnife_{BaseColor,Normal,MetalSmooth}.png, then SM_Weapon_Knife.fbx with the material
M_Weapon_Knife. The wear radii are small (2.5 mm bevel, 1 cm occlusion) because the blade is only 4.4 mm thick.
"""

import os
import sys

import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_knife as K  # noqa: E402,F401  (points build_smg's paths at the knife; builds nothing)
import bake_smg as BS  # noqa: E402  (the shared bake; importing it bakes nothing)

Tree, Signals, lin = BS.Tree, BS.Signals, BS.lin


def build_blade_metal(mat, wear_image):
    """Blackened steel: satin dark grey, brushed along the blade, lighter worn patches, bright steel on the edges."""
    t = Tree(mat)
    edge, cavity = BS.wear_masks(t, wear_image)
    obj = t.object_coords()
    stretch = t.node('ShaderNodeMapping')  # the blade runs along Blender's y: stretch the noise that way for brushed streaks
    t.nt.links.new(obj, stretch.inputs['Vector'])
    stretch.inputs['Scale'].default_value = (1.0, 0.04, 1.0)
    brush = t.noise(stretch.outputs['Vector'], 750.0, 2.0, 0.6)
    blotch = t.noise(obj, 80.0, 5.0, 0.6)  # where the coating has gone
    grime = t.noise(obj, 24.0, 4.0, 0.55)
    patch = t.smooth(t.noise(obj, 9.0, 3.0, 0.6), 0.52, 0.85)  # big soft areas where the finish is lighter
    scratch = t.smooth(t.noise(stretch.outputs['Vector'], 1500.0, 1.0, 0.7), 0.72, 0.76)
    worn = t.smooth(t.add(t.mul(edge, 1.7), t.mul(t.sub(blotch, 0.5), 0.35)), 0.62, 0.88)  # chips only near the edges
    base = t.lerpc(t.mul(grime, 0.7), t.rgb(lin("#1d1f22")), t.rgb(lin("#2c2f33")))
    base = t.lerpc(t.mul(patch, 0.55), base, t.rgb(lin("#4a4e53")))
    base = t.mulc(base, t.add(0.9, t.mul(brush, 0.2)))
    steel = t.lerpc(blotch, t.rgb(lin("#8a8e92")), t.rgb(lin("#b4b8bb")))
    colour = t.lerpc(worn, base, steel)
    colour = t.lerpc(t.mul(scratch, 0.45), colour, t.rgb(lin("#8a8e92")))
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.7)))
    metallic = t.lerp(worn, 0.85, 1.0)
    rough = t.add(t.lerp(worn, 0.34, 0.2), t.mul(t.sub(brush, 0.5), 0.16))
    rough = t.add(rough, t.mul(t.sub(grime, 0.5), 0.12))
    rough = t.add(rough, t.mul(cavity, 0.25))
    height = t.add(0.5, t.mul(t.sub(brush, 0.5), 0.05))
    height = t.sub(height, t.mul(scratch, 0.15))
    return t, Signals(colour, metallic, rough, height)


def build_g10(mat, wear_image):
    """Black G10: matt, very slightly greyer on the edges, a fine woven grain and a few wavy diagonal cut lines."""
    t = Tree(mat)
    edge, cavity = BS.wear_masks(t, wear_image)
    obj = t.object_coords()
    blotch = t.noise(obj, 70.0, 5.0, 0.6)
    grime = t.noise(obj, 30.0, 4.0, 0.55)
    worn = t.smooth(t.add(t.mul(edge, 1.3), t.mul(t.sub(blotch, 0.5), 0.6)), 0.5, 0.85)
    base = t.lerpc(t.mul(grime, 0.7), t.rgb(lin("#08090a")), t.rgb(lin("#121314")))
    colour = t.lerpc(worn, base, t.rgb(lin("#2e3033")))
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.5)))
    rough = t.add(t.lerp(worn, 0.78, 0.55), t.mul(t.sub(grime, 0.5), 0.12))
    grain = t.node('ShaderNodeTexChecker')
    t.nt.links.new(obj, grain.inputs['Vector'])
    grain.inputs['Scale'].default_value = 700.0
    fine = t.noise(obj, 2400.0, 1.0, 0.5)
    wave = t.node('ShaderNodeTexWave', wave_type='BANDS', bands_direction='DIAGONAL', wave_profile='SAW')
    t.nt.links.new(obj, wave.inputs['Vector'])
    wave.inputs['Scale'].default_value = 62.0
    wave.inputs['Distortion'].default_value = 2.5
    wave.inputs['Detail'].default_value = 0.0
    lines = t.smooth(wave.outputs['Fac'], 0.84, 0.97)
    grain_v = t.smooth(grain.outputs['Fac'], 0.4, 0.6)
    height = t.add(0.5, t.mul(t.sub(grain_v, 0.5), 0.10))
    height = t.add(height, t.mul(t.sub(fine, 0.5), 0.12))
    height = t.sub(height, t.mul(lines, 0.28))
    return t, Signals(colour, 0.0, rough, height)


if __name__ == "__main__":
    BS.main(obj_name="Knife", prefix="T_WeaponKnife_", final_name="M_Weapon_Knife", builders=(build_blade_metal, build_g10),
            bevel_radius=0.0025, ao_distance=0.01)
