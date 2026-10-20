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

S.COLOURS["M_Weapon_Tan"] = ((0.05, 0.052, 0.05, 1), 0.7, 0.5)  # the TAN slot is now the parkerised grey receiver
G = "Sniper"
AXIS_Y = 0.0            # the barrel and receiver axis
SCOPE_Y = 0.086         # the scope axis
MUZZLE_Z = 0.81         # WeaponDefinitions.Sniper socket, and the frontmost point of the model


def bm_cyl(c, axis, r, length, segs=16, r2=None):
    return S.cyl_bm(c, axis, r, length, segs, r2)


def cut_cyl(c, axis, r, length, segs, cutters, slot, bevel=0.0):
    """A cylinder minus cutter meshes, stored as one part."""
    base = bm_cyl(c, axis, r, length, segs)
    if bevel:
        bevel_bm(base, bevel)
    S._store(G, boolean_difference(base, cutters), slot, 0.0)


# --------------------------------------------------------------------------- receiver, barrel, brake (heavy rifle style, step C9b)
# The rifle was restyled on the owner's photo of a .50 calibre anti-materiel rifle: a rounded box receiver with lightening holes, a
# fluted heavy barrel with a big slotted box muzzle brake, a flat-top rail with a large scope, a box magazine, a short skeletal
# stock and a folded bipod. The frame is unchanged (muzzle z 0.81, butt about -0.405, pistol grip and forend where the hands are).

def rrect_xy(w, h, r, cs=3):
    """Rounded-rectangle cross-section (x, y) points: the receiver's soft corners."""
    pts = []
    for ox, oy, a0 in ((w / 2 - r, h / 2 - r, 0.0), (-(w / 2 - r), h / 2 - r, 90.0), (-(w / 2 - r), -(h / 2 - r), 180.0), (w / 2 - r, -(h / 2 - r), 270.0)):
        for k in range(cs + 1):
            a = math.radians(a0 + 90.0 * k / cs)
            pts.append((ox + r * math.cos(a), oy + r * math.sin(a)))
    return pts


def prism_z(pts, z0, z1):
    """A cross-section of (x, y) points extruded along the barrel from z0 to z1."""
    bm = bmesh.new()
    a = [bm.verts.new(B((x, y, z0))) for x, y in pts]
    b = [bm.verts.new(B((x, y, z1))) for x, y in pts]
    n = len(pts)
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    return bm


def fluted(r_out, r_in, flutes=6):
    pts = []
    sector = 2.0 * math.pi / flutes
    for k in range(flutes):
        for frac, r in ((0.0, r_out), (0.42, r_out), (0.52, r_in), (0.94, r_in)):
            a = k * sector + frac * sector
            pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


def build_action():
    # receiver: a rounded tube z -0.30 to 0.22 with two rows of lightening holes in the front section and an ejection port
    holes = [bm_cyl((0.0, 0.019, 0.030 + 0.034 * i), 'x', 0.0108, 0.12, 10) for i in range(5)]
    holes += [bm_cyl((0.0, -0.0185, 0.047 + 0.034 * i), 'x', 0.0078, 0.12, 8) for i in range(4)]
    holes.append(S.box_bm((0.034, 0.012, -0.12), (0.030, 0.030, 0.080)))
    S._store(G, boolean_difference(prism_z(rrect_xy(0.072, 0.094, 0.017, 2), -0.30, 0.22), holes), TAN, 0.0)
    S.box(G, METAL, (0.028, 0.012, -0.12), (0.004, 0.020, 0.045))                    # the bolt face inside the port
    # flat-top rail with cross teeth, rear flip sight, charging handle with its knob, screws
    S.box(G, TAN, (0.0, 0.0495, -0.04), (0.032, 0.007, 0.50), bevel=0.0012)
    for i in range(11):
        S.box(G, TAN, (0.0, 0.0555, -0.27 + 0.0445 * i), (0.040, 0.0035, 0.0115))
    S.box(G, METAL, (0.0, 0.066, 0.125), (0.012, 0.020, 0.012), bevel=0.0008)
    S.box(G, METAL, (0.056, 0.020, -0.16), (0.050, 0.010, 0.020), bevel=0.0012)
    S.cyl(G, POLYMER, (0.087, 0.020, -0.16), 'x', 0.0115, 0.020, segs=12, bevel=0.0015)
    for z in (-0.24, 0.0, 0.215):
        S.cyl(G, METAL, (0.0355, -0.030, z), 'x', 0.0045, 0.004, segs=8)
        S.cyl(G, METAL, (-0.0355, -0.030, z), 'x', 0.0045, 0.004, segs=8)
    # fluted heavy barrel, its collar, a forend sleeve for the left hand, and the big slotted box muzzle brake
    S._store(G, prism_z(fluted(0.0195, 0.0163), 0.215, 0.69), METAL, 0.0)
    S.cyl(G, METAL, (0.0, 0.0, 0.230), 'z', 0.0270, 0.030, segs=14, bevel=0.002)
    S.cyl(G, TAN, (0.0, 0.0, 0.300), 'z', 0.0340, 0.100, segs=14, bevel=0.003)
    brake = S.box_bm((0.0, 0.0, 0.745), (0.084, 0.060, 0.130))
    bevel_bm(brake, 0.009, 2)
    cutters = [S.box_bm((sx * 0.037, 0.0, z), (0.030, 0.036, 0.034)) for sx in (-1, 1) for z in (0.716, 0.774)]
    cutters.append(bm_cyl((0.0, 0.0, 0.745), 'z', 0.0098, 0.14, segs=14))
    S._store(G, boolean_difference(brake, cutters), METAL, 0.0)


def build_trigger():
    S.tube(G, METAL, [(0.0, -0.046, -0.045), (0.0, -0.070, -0.050), (0.0, -0.078, -0.075), (0.0, -0.072, -0.098), (0.0, -0.046, -0.103)], 0.0035, 6)
    S.prism(G, METAL, [(-0.065, -0.046), (-0.058, -0.046), (-0.056, -0.064), (-0.063, -0.072), (-0.069, -0.068)], -0.0032, 0.0032, bevel=0.0008)
    # 10-round box magazine with side ribs and a floor plate
    S.box(G, POLYMER, (0.0, -0.133, 0.02), (0.056, 0.176, 0.118), bevel=0.007)
    S.box(G, METAL, (0.0, -0.2245, 0.02), (0.060, 0.008, 0.122), bevel=0.003)
    for i in range(4):
        for sx in (-1, 1):
            S.box(G, METAL, (sx * 0.0285, -0.080 - 0.032 * i, 0.02), (0.003, 0.008, 0.095))


# --------------------------------------------------------------------------- scope

def build_scope():
    y = SCOPE_Y
    S.cyl(G, METAL, (0.0, y, -0.02), 'z', 0.0155, 0.300, segs=12)                    # main tube
    S.cyl(G, METAL, (0.0, y, 0.150), 'z', 0.0250, 0.045, segs=12)                    # the tube swells into the objective bell
    S.cyl(G, METAL, (0.0, y, 0.2025), 'z', 0.0370, 0.060, segs=14, bevel=0.0008)
    shade = boolean_difference(bm_cyl((0.0, y, 0.2575), 'z', 0.0385, 0.050, segs=14), [bm_cyl((0.0, y, 0.2575), 'z', 0.0360, 0.06, segs=14)])
    S._store(G, shade, METAL, 0.0)                                                    # sun shade
    S.cyl(G, POLYMER, (0.0, y, 0.2300), 'z', 0.0350, 0.002, segs=14)                 # objective glass
    S.cyl(G, METAL, (0.0, y, -0.190), 'z', 0.0185, 0.040, segs=14)                   # eyepiece
    S.cyl(G, METAL, (0.0, y, -0.218), 'z', 0.0240, 0.020, segs=14)
    S.cyl(G, POLYMER, (0.0, y, -0.2285), 'z', 0.0190, 0.002, segs=14)                # eyepiece glass
    S.cyl(G, METAL, (0.0, y + 0.0255, -0.02), 'y', 0.0115, 0.022, segs=14)           # elevation turret and its dial ring
    S.cyl(G, METAL, (0.0, y + 0.0375, -0.02), 'y', 0.0095, 0.012, segs=14)
    S.cyl(G, METAL, (0.0255, y, -0.02), 'x', 0.0115, 0.022, segs=14)                 # windage turret
    S.cyl(G, METAL, (-0.0390, y, 0.130), 'x', 0.0120, 0.022, segs=14)                # parallax knob
    for z in (-0.10, 0.07):
        ring = boolean_difference(bm_cyl((0.0, y, z), 'z', 0.0235, 0.022, segs=12), [bm_cyl((0.0, y, z), 'z', 0.0153, 0.04, segs=12)])
        S._store(G, ring, METAL, 0.0)
        S.box(G, METAL, (0.0, 0.0645, z), (0.040, 0.026, 0.026), bevel=0.0015)


# --------------------------------------------------------------------------- stock, grip, butt, bipod

GRIP = [(-0.105, -0.036), (-0.125, -0.140), (-0.150, -0.150), (-0.172, -0.145), (-0.150, -0.030), (-0.115, -0.022)]
STOCK_PLATE = [(-0.26, 0.046), (-0.34, 0.040), (-0.385, 0.018), (-0.408, -0.010), (-0.410, -0.135), (-0.383, -0.144),
               (-0.366, -0.072), (-0.26, -0.076), (-0.20, -0.076), (-0.20, -0.040), (-0.26, -0.040)]


def build_stock():
    # a short skeletal stock: two sloping side plates with a slot, a top bridge, a cross bar, and a rubber pad
    for sx in (-1, 1):
        plate = S.prism_bm(STOCK_PLATE, sx * 0.024 - 0.006, sx * 0.024 + 0.006)
        bevel_bm(plate, 0.004, 1)
        S._store(G, boolean_difference(plate, [S.box_bm((sx * 0.024, -0.012, -0.315), (0.030, 0.030, 0.070))]), TAN, 0.0)
    S.box(G, TAN, (0.0, 0.043, -0.335), (0.048, 0.010, 0.140), bevel=0.003)
    S.box(G, TAN, (0.0, -0.140, -0.396), (0.048, 0.012, 0.016), bevel=0.003)
    S.box(G, POLYMER, (0.0, -0.0645, -0.4155), (0.0535, 0.145, 0.022), bevel=0.006)            # rubber butt pad
    S.prism(G, POLYMER, GRIP, -0.0175, 0.0175, bevel=0.0045, seg=2)                                     # the pistol grip
    for i in range(3):                                                                                   # grip panelling on the back strap
        S.box(G, METAL, (0.0, -0.060 - 0.020 * i, -0.1405 - 0.0045 * i), (0.040, 0.007, 0.0050))


def build_bipod():
    # folded under the barrel: a clamp round it, a hinge block and two legs lying forward with foot pads
    clamp = boolean_difference(bm_cyl((0.0, 0.0, 0.365), 'z', 0.0290, 0.034, segs=14), [bm_cyl((0.0, 0.0, 0.365), 'z', 0.0195, 0.05, segs=14)])
    S._store(G, clamp, METAL, 0.0)
    S.box(G, METAL, (0.0, -0.040, 0.365), (0.046, 0.026, 0.040), bevel=0.002)
    for sx in (-1, 1):
        S.box(G, METAL, (sx * 0.020, -0.056, 0.490), (0.009, 0.009, 0.210), bevel=0.0012)
        S.box(G, POLYMER, (sx * 0.020, -0.056, 0.598), (0.013, 0.013, 0.022), bevel=0.002)


# --------------------------------------------------------------------------- hands

LEFT_HAND = True  # two hands on the rifle: the right on the pistol grip, the left on the forend (the SMG has one hand only)


def build_hands():
    """The right hand on the pistol grip with the index finger on the trigger; the left hand gripping the forend from its left side, the back of the hand towards the player."""
    grip_c = (0.0, -0.092, -0.141)
    fwd, up = H.grip_frame((0.0, 0.980, 0.196), (0.0, -0.196, 0.980), 20.0, "R")
    pos = H.palm_position(grip_c, fwd, up, 0.0300, -0.020)
    curls = ((20, 32, 22), (92, 84, 50), (94, 86, 52), (96, 88, 50))
    H.add_hand("R", pos, fwd, up, curls=curls, thumb=None, arm_dir=(0.35, -0.95, -0.20), visible=0.04, target_tris=850)
    if not LEFT_HAND:
        return
    fend_c = (0.0, -0.022, 0.270)
    fwd, up = H.grip_frame((0.0, 0.0, 1.0), (0.45, -1.0, 0.0), 22.0, "L")
    pos = H.palm_position(fend_c, fwd, up, 0.044, -0.020)
    curls = ((62, 86, 54), (66, 88, 54), (70, 90, 52), (72, 90, 50))
    H.add_hand("L", pos, fwd, up, curls=curls, thumb=None, arm_dir=(-0.20, -0.97, -0.15), visible=0.04, target_tris=850)


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
