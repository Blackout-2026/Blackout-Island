"""Builds SM_Weapon_Sniper.fbx, the realistic R-700 bolt-action rifle viewmodel with two realistic hands (step C7). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_sniper.py

Style reference: a modern bolt-action sniper rifle: round action with a flat-top rail, a heavy barrel with a ported muzzle brake, a
tan polymer stock with a cheek rise, a separate angled pistol grip and a rubber butt pad, a short box magazine, a folded bipod and
a 30 mm scope with turrets, a parallax knob and open flip-up covers. Our own mesh, not traced.

Frame: the gun frame of docs/art-pipeline.md section 14 (WeaponDefinitions.Sniper): every number is a UNITY-frame metre value,
the origin is the middle of the receiver, x right, y up, +z the way the muzzle points. The barrel axis is at y = 0.008 and the
muzzle (WeaponDefinitions.MuzzlePoint, made by WeaponPrefabBuilder) is at z = 0.81, the frontmost point of the model. The bolt
handle is part of the mesh; the BoltHandle pivot (0.03, 0.01, -0.08) is made by WeaponPrefabBuilder as an empty for a future bolt
animation.

Output: Art/Blender/SM_Weapon_Sniper.blend and Assets/Models/Custom/SM_Weapon_Sniper.fbx. Before bake_sniper.py has run the rifle uses
the palette slots M_Weapon_Metal, M_Weapon_Polymer and M_Weapon_Tan; after it, ONE material M_Weapon_Sniper (baked textures) plus
M_Weapon_Glove and M_Weapon_Camo = the 3 slots a viewmodel may use. Budget: 6,000 triangles target, 9,000 maximum.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402  (the shared kit; importing it builds nothing)
import gen_parts as P  # noqa: E402
import hand_kit as H  # noqa: E402
from weapon_kit import bevel_bm, boolean_difference  # noqa: E402

B = P.B
METAL, POLYMER, SLEEVE, GLOVE, TAN = S.METAL, S.POLYMER, S.SLEEVE, S.GLOVE, S.TAN
REPO = S.REPO
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Weapon_Sniper.blend")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Weapon_Sniper.fbx")
S.BLEND_PATH = BLEND_PATH
S.FBX_PATH = FBX_PATH

G = "Sniper"
AXIS_Y = 0.008          # the barrel and receiver axis
SCOPE_Y = 0.074         # the scope axis
MUZZLE_Z = 0.81         # WeaponDefinitions.Sniper socket, and the frontmost point of the model


def bm_cyl(c, axis, r, length, segs=16, r2=None):
    return S.cyl_bm(c, axis, r, length, segs, r2)


def cut_cyl(c, axis, r, length, segs, cutters, slot, bevel=0.0):
    """A cylinder minus cutter meshes, stored as one part."""
    base = bm_cyl(c, axis, r, length, segs)
    if bevel:
        bevel_bm(base, bevel)
    S._store(G, boolean_difference(base, cutters), slot, 0.0)


# --------------------------------------------------------------------------- action, barrel, brake

def build_action():
    # barrel: heavy, stepped, crowned
    S.cyl(G, METAL, (0.0, AXIS_Y, 0.43), 'z', 0.0165, 0.58, segs=16)
    S.cyl(G, METAL, (0.0, AXIS_Y, 0.715), 'z', 0.0135, 0.07, segs=16)
    S.cyl(G, METAL, (0.0, AXIS_Y, 0.145), 'z', 0.0225, 0.032, segs=16)          # barrel nut
    # muzzle brake: a ported cylinder with a bore
    ports = [S.box_bm((0.0, AXIS_Y, 0.752 + 0.019 * i), (0.060, 0.0100, 0.0085)) for i in range(3)]
    ports.append(S.box_bm((0.0, AXIS_Y + 0.0205, 0.771), (0.0100, 0.030, 0.0360)))      # one vent on top
    ports.append(bm_cyl((0.0, AXIS_Y, 0.8125), 'z', 0.0075, 0.04, segs=14))              # the bore
    cut_cyl((0.0, AXIS_Y, 0.775), 'z', 0.0205, 0.075, 16, ports, METAL, bevel=0.0012)

    # receiver: a round action tube with an ejection port on the right, and a flat bedding block under it
    port = [S.box_bm((0.0238, AXIS_Y + 0.004, -0.012), (0.020, 0.018, 0.070))]
    cut_cyl((0.0, AXIS_Y, 0.020), 'z', 0.0238, 0.235, 18, port, METAL, bevel=0.0012)
    S.box(G, METAL, (0.0, -0.011, 0.0), (0.040, 0.030, 0.20), bevel=0.003)
    S.box(G, METAL, (0.0, AXIS_Y, -0.012), (0.004, 0.012, 0.050))                   # the bolt face seen through the port
    # flat-top rail: a base and cross teeth, front to back, left open under the scope rings
    S.box(G, METAL, (0.0, 0.0345, 0.020), (0.028, 0.006, 0.300), bevel=0.0008)
    for i in range(12):
        z = -0.125 + 0.0260 * i
        S.box(G, METAL, (0.0, 0.0390, z), (0.034, 0.0035, 0.0105))
    # rear bolt shroud, end cap, safety lever and the bolt handle on the right (the handle is the knob moving back and forth)
    S.cyl(G, METAL, (0.0, AXIS_Y, -0.128), 'z', 0.0170, 0.072, segs=18)
    S.cyl(G, POLYMER, (0.0, AXIS_Y, -0.168), 'z', 0.0130, 0.016, segs=14)
    S.box(G, METAL, (0.0160, AXIS_Y + 0.011, -0.140), (0.016, 0.005, 0.020), bevel=0.001)
    S.cyl(G, METAL, (0.0290, 0.0100, -0.080), 'x', 0.0050, 0.026, segs=10)         # handle root
    S.cyl(G, METAL, (0.0450, 0.0060, -0.0815), 'x', 0.0045, 0.040, segs=10)        # handle stem, bent down and back
    S.cyl(G, POLYMER, (0.0700, 0.0030, -0.083), 'x', 0.0120, 0.030, segs=16, bevel=0.002)   # the knob
    # receiver screws, the magazine release and the bolt catch
    for z in (-0.060, 0.080):
        for sx in (-1, 1):
            S.cyl(G, METAL, (sx * 0.0200, -0.0060, z), 'x', 0.0038, 0.004, segs=8)
    S.box(G, METAL, (0.0, -0.034, 0.075), (0.012, 0.010, 0.012), bevel=0.001)


def build_trigger():
    S.tube(G, METAL, [(0.0, -0.040, 0.020), (0.0, -0.066, 0.016), (0.0, -0.072, -0.012), (0.0, -0.066, -0.044), (0.0, -0.040, -0.060)],
           0.0035, 6)
    S.prism(G, METAL, [(-0.034, -0.040), (-0.026, -0.040), (-0.024, -0.054), (-0.030, -0.064), (-0.036, -0.062)], -0.0032, 0.0032,
            bevel=0.0008)
    # the magazine: a short box with a metal floor plate, ribs and a release lip
    S.box(G, POLYMER, (0.0, -0.067, 0.036), (0.034, 0.080, 0.066), bevel=0.003)
    S.box(G, METAL, (0.0, -0.1085, 0.036), (0.037, 0.007, 0.069), bevel=0.0012)
    for i in range(4):
        S.box(G, METAL, (0.0171, -0.050 - 0.016 * i, 0.036), (0.0020, 0.0060, 0.050))
        S.box(G, METAL, (-0.0171, -0.050 - 0.016 * i, 0.036), (0.0020, 0.0060, 0.050))


# --------------------------------------------------------------------------- scope

def build_scope():
    y = SCOPE_Y
    S.cyl(G, METAL, (0.0, y, 0.010), 'z', 0.0155, 0.300, segs=16)                    # main tube
    S.cyl(G, METAL, (0.0, y, 0.175), 'z', 0.0215, 0.045, segs=16)                    # the tube swells into the objective bell
    S.cyl(G, METAL, (0.0, y, 0.215), 'z', 0.0295, 0.050, segs=16, bevel=0.0008)
    shade = boolean_difference(bm_cyl((0.0, y, 0.258), 'z', 0.0310, 0.040, segs=16),
                               [bm_cyl((0.0, y, 0.258), 'z', 0.0287, 0.05, segs=16)])
    S._store(G, shade, METAL, 0.0)                                                    # sun shade, a thin tube
    S.cyl(G, POLYMER, (0.0, y, 0.2395), 'z', 0.0278, 0.002, segs=16)                 # objective glass
    S.cyl(G, METAL, (0.0, y, -0.150), 'z', 0.0185, 0.040, segs=16)                   # eyepiece
    S.cyl(G, METAL, (0.0, y, -0.178), 'z', 0.0240, 0.020, segs=16)
    S.cyl(G, POLYMER, (0.0, y, -0.1885), 'z', 0.0190, 0.002, segs=16)                # eyepiece glass
    # turrets: elevation on top, windage on the right, parallax on the left of the bell
    S.cyl(G, METAL, (0.0, y + 0.0255, 0.010), 'y', 0.0110, 0.022, segs=16)
    S.cyl(G, METAL, (0.0, y + 0.0375, 0.010), 'y', 0.0090, 0.012, segs=16)
    S.cyl(G, METAL, (0.0255, y, 0.010), 'x', 0.0110, 0.022, segs=16)
    S.cyl(G, METAL, (0.0375, y, 0.010), 'x', 0.0090, 0.012, segs=16)
    S.cyl(G, METAL, (-0.0300, y, 0.135), 'x', 0.0115, 0.022, segs=16)
    S.cyl(G, METAL, (-0.0425, y, 0.135), 'x', 0.0120, 0.004, segs=16)
    # two rings on the rail with clamp screws
    for z in (-0.060, 0.080):
        ring = boolean_difference(bm_cyl((0.0, y, z), 'z', 0.0225, 0.022, segs=16), [bm_cyl((0.0, y, z), 'z', 0.0153, 0.04, segs=16)])
        S._store(G, ring, METAL, 0.0)
        S.box(G, METAL, (0.0, 0.0555, z), (0.036, 0.026, 0.026), bevel=0.0015)
        for sy in (-0.012, 0.012):
            S.cyl(G, METAL, (0.0233, y + sy, z), 'x', 0.0040, 0.008, segs=8)


# --------------------------------------------------------------------------- stock, grip, butt, bipod

STOCK = [(-0.405, 0.050), (-0.372, 0.064), (-0.310, 0.063), (-0.220, 0.052), (-0.140, 0.030), (-0.095, 0.003),
         (0.140, 0.003), (0.440, 0.002), (0.452, -0.010), (0.445, -0.045), (0.250, -0.050), (0.130, -0.046), (0.070, -0.043),
         (-0.120, -0.045), (-0.150, -0.050), (-0.200, -0.055), (-0.300, -0.074), (-0.405, -0.092)]
GRIP = [(-0.105, -0.036), (-0.125, -0.140), (-0.150, -0.150), (-0.172, -0.145), (-0.150, -0.030), (-0.115, -0.022)]


def build_stock():
    bm = S.prism_bm(STOCK, -0.0260, 0.0260)
    bevel_bm(bm, 0.0075, 2)
    holes = [bm_cyl((0.0, 0.004, -0.330), 'x', 0.0135, 0.08, segs=12), bm_cyl((0.0, 0.026, -0.262), 'x', 0.0095, 0.08, segs=10)]
    S._store(G, boolean_difference(bm, holes), TAN, 0.0)
    S.prism(G, TAN, GRIP, -0.0175, 0.0175, bevel=0.0045, seg=2)                      # the pistol grip, narrower than the stock
    S.box(G, POLYMER, (0.0, -0.021, -0.4155), (0.0535, 0.145, 0.022), bevel=0.004)  # rubber butt pad
    for i in range(3):                                                                # grip panelling on the back strap
        S.box(G, POLYMER, (0.0, -0.060 - 0.020 * i, -0.1405 - 0.0045 * i), (0.040, 0.007, 0.0050))


def build_bipod():
    S.box(G, METAL, (0.0, -0.057, 0.370), (0.046, 0.016, 0.040), bevel=0.002)
    for sx in (-1, 1):
        S.box(G, METAL, (sx * 0.018, -0.062, 0.480), (0.008, 0.008, 0.200), bevel=0.0012)
        S.box(G, POLYMER, (sx * 0.018, -0.0635, 0.586), (0.011, 0.011, 0.018), bevel=0.002)


# --------------------------------------------------------------------------- hands

LEFT_HAND = True  # two hands on the rifle: the right on the pistol grip, the left on the forend (the SMG has one hand only)


def build_hands():
    """The right hand on the pistol grip with the index finger on the trigger; the left hand gripping the forend from its left side, the back of the hand towards the player."""
    grip_c = (0.0, -0.092, -0.141)
    fwd, up = H.grip_frame((0.0, 0.980, 0.196), (0.0, -0.196, 0.980), 20.0, "R")
    pos = H.palm_position(grip_c, fwd, up, 0.0300, -0.020)
    curls = ((20, 32, 22), (92, 84, 50), (94, 86, 52), (96, 88, 50))
    H.add_hand("R", pos, fwd, up, curls=curls, thumb=None, arm_dir=(0.35, -0.95, -0.20), visible=0.04)
    if not LEFT_HAND:
        return
    fend_c = (0.0, -0.022, 0.270)
    fwd, up = H.grip_frame((0.0, 0.0, 1.0), (0.45, -1.0, 0.0), 22.0, "L")
    pos = H.palm_position(fend_c, fwd, up, 0.044, -0.020)
    curls = ((62, 86, 54), (66, 88, 54), (70, 90, 52), (72, 90, 50))
    H.add_hand("L", pos, fwd, up, curls=curls, thumb=None, arm_dir=(-0.35, -0.90, -0.20), visible=0.04)


# --------------------------------------------------------------------------- scene and export

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_action()
    build_trigger()
    build_scope()
    build_stock()
    build_bipod()
    build_hands()
    mats = [S.make_material(n) for n in S.SLOTS]
    H.glove_material(mats[S.GLOVE])
    names = (G, "GloveR", "GloveL", "SleeveR", "SleeveL") if LEFT_HAND else (G, "GloveR", "SleeveR")
    objs = [S.create_object(n, mats, 85.0 if n.startswith(("Glove", "Sleeve")) else 40.0) for n in names]
    total = 0
    for ob in objs:
        S.unwrap(ob)
        if ob.name.startswith("Glove"):
            H.tile_uvs(ob)
        tris = S.triangles(ob)
        total += tris
        print(f"[SNIPER] {ob.name}: {tris} triangles")
    zs = [S._to_unity(ob.matrix_world @ v.co).z for ob in objs for v in ob.data.vertices]
    print(f"[SNIPER] total {total} triangles (target 6000, max 9000), muzzle z {max(zs):.3f} (want {MUZZLE_Z})")

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    S.export_fbx(objs[0])
    print(f"[SNIPER] {FBX_PATH}")


if __name__ == "__main__":
    import build_sniper  # run the importable copy: hand_kit imports build_smg, and all must share one set of parts
    build_sniper.main()
