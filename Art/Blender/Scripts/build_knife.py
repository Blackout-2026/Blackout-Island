"""Builds SM_Weapon_Knife.fbx, the realistic combat knife viewmodel with a realistic gripping hand (step C7). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_knife.py

Style reference: a modern tactical fixed blade (Mil-Tec type): blackened drop-point blade that thins towards the tip, a serrated
heel, two fuller grooves with rounded ends, a notched spine, a hooked guard with a finger notch, and a black G10 handle with
finger grooves, three star-drive rivets, a notched thumb rest and a lined lanyard hole. Our own mesh, no maker's logo.

Frame: the same gun-frame idea as build_smg.py. Every number is a UNITY-frame metre value, the origin is the middle of the grip
(the "Hand" pivot of docs/art-pipeline.md section 14.6), +z is the way the blade points, +y is the spine and the edge faces down.
The build kit (B(), prism, box, cyl, export) comes from build_smg.py and the hand from hand_kit.py, so every weapon shares one look.

Output: Art/Blender/SM_Weapon_Knife.blend and Assets/Models/Custom/SM_Weapon_Knife.fbx. Before bake_knife.py has run, the knife
uses the palette slots M_Weapon_Metal and M_Weapon_Polymer; after it, ONE material M_Weapon_Knife (baked textures) plus
M_Weapon_Glove (the gloved hand) and M_Weapon_Camo (the sleeve) = the 3 slots a viewmodel may use.
Budget: 4,000 triangles target, 6,000 maximum.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402  (the shared kit; importing it builds nothing)
import gen_parts as P  # noqa: E402
import hand_kit as H  # noqa: E402
from weapon_kit import bevel_bm, boolean_difference, star_bm  # noqa: E402

B = P.B
METAL, POLYMER, SLEEVE, GLOVE = S.METAL, S.POLYMER, S.SLEEVE, S.GLOVE
REPO = S.REPO
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Weapon_Knife.blend")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Weapon_Knife.fbx")
S.BLEND_PATH = BLEND_PATH  # the shared create/export code writes to these
S.FBX_PATH = FBX_PATH

K = "Knife"
TIP_Z = 0.226            # the blade tip, the frontmost point of the model
SPINE_Y = 0.0175
EDGE_Y = -0.0192         # the edge line of the straight part


# --------------------------------------------------------------------------- blade

def spine_y(z):
    """Straight spine, then a drop point curving down to the tip."""
    if z <= 0.188:
        return SPINE_Y
    t = min((z - 0.188) / (TIP_Z - 0.188), 1.0)
    return SPINE_Y - (SPINE_Y - 0.0115) * t * t * (3.0 - 2.0 * t)


def belly_y(z):
    """The curved cutting edge, a full belly from z 0.1 up to the tip."""
    t = (z - 0.1) / (TIP_Z - 0.1)
    return 0.0115 - (0.0115 - EDGE_Y) * max(0.0, 1.0 - t * t) ** 0.55


def half_thickness(z):
    """Distal taper: 2.5 mm each side at the ricasso, 1.1 mm at the tip."""
    t = (z - 0.074) / (TIP_Z - 0.074)
    return 0.0025 - 0.0014 * min(max(t, 0.0), 1.0)


def edge_stations():
    """(z, edge y) along the blade: eight serration teeth at the heel, then the belly."""
    out = []
    z0, pitch, teeth, depth = 0.0745, 0.00325, 8, 0.0028
    for k in range(teeth):
        out.append((z0 + k * pitch, EDGE_Y))                       # tooth tip
        out.append((z0 + (k + 0.6) * pitch, EDGE_Y + depth))       # valley
    zs = [z0 + teeth * pitch + (TIP_Z - 0.0035 - (z0 + teeth * pitch)) * i / 22.0 for i in range(23)]
    out.extend((z, belly_y(z) if z > 0.1 else EDGE_Y) for z in zs)
    return out


def blade_bm():
    """A flat-ground blade with a chamfered spine and a primary bevel to a centre edge. Built station by station."""
    bm = bmesh.new()
    rings = []
    for z, ye in edge_stations():
        ys = spine_y(z)
        ht = half_thickness(z)
        ysh = ye + min(0.0100, 0.62 * (ys - ye))
        ych = ys - 0.0009                                           # where the spine chamfer meets the flat
        rings.append([
            bm.verts.new(B((-ht * 0.62, ys, z))), bm.verts.new(B((ht * 0.62, ys, z))),       # 0, 1: the flat top of the spine
            bm.verts.new(B((-ht, ych, z))), bm.verts.new(B((ht, ych, z))),                   # 2, 3: chamfer meets the flats
            bm.verts.new(B((-ht, ysh, z))), bm.verts.new(B((ht, ysh, z))),                   # 4, 5: the shoulder of the bevel
            bm.verts.new(B((0.0, ye, z))),                                                   # 6: the edge
        ])
    tip = bm.verts.new(B((0.0, 0.0115, TIP_Z)))
    for a, b in zip(rings[:-1], rings[1:]):
        bm.faces.new((a[0], b[0], b[1], a[1]))                      # spine top
        bm.faces.new((a[0], a[2], b[2], b[0]))                      # left chamfer
        bm.faces.new((a[1], b[1], b[3], a[3]))                      # right chamfer
        bm.faces.new((a[2], a[4], b[4], b[2]))                      # left flat
        bm.faces.new((a[3], b[3], b[5], a[5]))                      # right flat
        bm.faces.new((a[4], a[6], b[6], b[4]))                      # left bevel
        bm.faces.new((a[5], b[5], b[6], a[6]))                      # right bevel
    r = rings[-1]
    bm.faces.new((r[0], tip, r[1]))
    bm.faces.new((r[0], r[2], tip))
    bm.faces.new((r[1], tip, r[3]))
    bm.faces.new((r[2], r[4], tip))
    bm.faces.new((r[3], tip, r[5]))
    bm.faces.new((r[4], r[6], tip))
    bm.faces.new((r[5], tip, r[6]))
    f = rings[0]
    bm.faces.new((f[0], f[1], f[3], f[5], f[6], f[4], f[2]))
    return bm


def slot_cutters(x, y, z0, z1, height, depth):
    """A fuller groove with round ends on the face at x: a box capped by two cylinders, cutting `depth` into the blade."""
    parts = []
    zc, length = (z0 + z1) / 2.0, z1 - z0 - height
    parts.append(S.box_bm((x, y, zc), (depth * 2.0, height, length)))
    for z in (z0 + height / 2.0, z1 - height / 2.0):
        parts.append(S.cyl_bm((x, y, z), 'x', height / 2.0, depth * 2.0, segs=10))
    return parts


def build_blade():
    cutters = []
    # two fuller grooves on both flats (0.6 mm deep), round at both ends
    for sx in (-1, 1):
        ht = half_thickness(0.13)
        cutters += slot_cutters(sx * ht, 0.0118, 0.088, 0.172, 0.0032, 0.0006)
        cutters += slot_cutters(sx * ht, 0.0055, 0.108, 0.150, 0.0032, 0.0006)
    # jimping: small notches across the spine behind the fuller
    for i in range(7):
        cutters.append(S.box_bm((0.0, SPINE_Y, 0.0805 + 0.0042 * i), (0.0100, 0.0017, 0.0018)))
    bm = boolean_difference(blade_bm(), cutters)
    S._store(K, bm, METAL, 0.0)


# --------------------------------------------------------------------------- guard and handle

def build_guard():
    """Hooked guard: a bottle-opener notch on top, a finger notch (a downward hook) underneath."""
    pts = [(0.048, 0.0195), (0.058, 0.0212), (0.0625, 0.0240), (0.0660, 0.0262), (0.0690, 0.0258), (0.0702, 0.0212),
           (0.0745, 0.0192), (0.0770, 0.0150), (0.0770, -0.0175), (0.0745, -0.0218), (0.0700, -0.0262), (0.0640, -0.0286),
           (0.0590, -0.0268), (0.0540, -0.0232), (0.048, -0.0208)]
    S.prism(K, METAL, pts, -0.0075, 0.0075, bevel=0.0015, seg=2)


def handle_outline():
    top = [(0.050, 0.0190), (0.030, 0.0178), (0.005, 0.0170), (-0.020, 0.0168), (-0.045, 0.0170), (-0.062, 0.0164),
           (-0.0690, 0.0142), (-0.0722, 0.0087), (-0.0728, 0.0), (-0.0712, -0.0085), (-0.0675, -0.0150)]
    bottom = []
    z = -0.0675
    while z < 0.0499:
        # four finger grooves: the lower edge swings between 17 and 20 mm below the axis
        bottom.append((z, -(0.0170 + 0.0030 * (0.5 + 0.5 * math.cos(2.0 * math.pi * (z + 0.054) / 0.0265)))))
        z += 0.0033
    bottom.append((0.050, -0.0200))
    return top + bottom


def build_handle():
    bm = S.prism_bm(handle_outline(), -0.0114, 0.0114)
    bevel_bm(bm, 0.0040, 3)
    cutters = [S.cyl_bm((0.0, -0.0035, -0.0618), 'x', 0.0036, 0.05, segs=14)]          # the lanyard hole
    for i in range(6):                                                                  # the thumb rest on top
        cutters.append(S.box_bm((0.0, 0.0172, 0.0150 + 0.0050 * i), (0.0300, 0.0020, 0.0020)))
    bm = boolean_difference(bm, cutters)
    S._store(K, bm, POLYMER, 0.0)
    # a metal liner round the lanyard hole
    ring = boolean_difference(S.cyl_bm((0.0, -0.0035, -0.0618), 'x', 0.0052, 0.0230, segs=14),
                              [S.cyl_bm((0.0, -0.0035, -0.0618), 'x', 0.0036, 0.05, segs=14)])
    S._store(K, ring, METAL, 0.0)
    # three star-drive rivets: a domed disc with a raised star on it, on both sides
    for z, y in ((0.034, 0.0010), (-0.003, 0.0000), (-0.040, -0.0005)):
        for sx in (-1, 1):
            S.cyl(K, METAL, (sx * 0.0118, y, z), 'x', 0.0044, 0.0020, segs=12)
            S._store(K, star_bm((sx * 0.0130, y, z), 0.0030, 0.0016, 0.0005), METAL, 0.0)


# --------------------------------------------------------------------------- the gripping hand

def build_hand():
    """A right hand with the palm against the left face of the handle, the fingers wrapping up over the spine and the thumb
    along the handle towards the guard; the forearm leaves back, down and to the right (hand_kit.grip_frame)."""
    a = math.radians(70.0)
    fwd, up = H.grip_frame((0.0, 0.0, 1.0), (-math.cos(a), math.sin(a), 0.0), 25.0)
    pos = H.palm_position((0.0, 0.0, 0.002), fwd, up)
    fist = ((86, 78, 52), (88, 80, 52), (90, 82, 50), (90, 82, 48))
    # thumb: from under the heel it comes up the far face of the handle (the face away from the palm), lies flat against it and
    # stops short of the guard. Written in the knife's frame (x right, y up, +z towards the blade), then turned into hand space.
    # No knuckle ridge (ridge=None): in this tight fist it showed from the player's camera as an extra finger across the hand.
    inv = S.frame3(fwd, up).inverted()

    def hand_pt(g):
        return Vector(inv @ (Vector(g) - pos) / H.HAND_SCALE)

    thumb_base = [(hand_pt((0.0010, -0.0663, 0.0079)), 0.0190, 0.0150), (hand_pt((0.0110, -0.0470, 0.0090)), 0.0150, 0.0130)]
    thumb = [(hand_pt((0.0190, -0.0340, 0.0060)), 0.0125), (hand_pt((0.0215, -0.0235, 0.0170)), 0.0112),
             (hand_pt((0.0220, -0.0170, 0.0270)), 0.0100), (hand_pt((0.0215, -0.0140, 0.0340)), 0.0090)]
    H.add_hand("R", pos, fwd, up, curls=fist, thumb=thumb, thumb_base=thumb_base, ridge=None,
               arm_dir=-fwd + Vector((0.1, -0.1, -0.15)), visible=0.04)


# --------------------------------------------------------------------------- scene and export

def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_blade()
    build_guard()
    build_handle()
    build_hand()
    mats = [S.make_material(n) for n in S.SLOTS]
    H.glove_material(mats[S.GLOVE])
    names = (K, "GloveR", "SleeveR")
    objs = [S.create_object(n, mats, 85.0 if n.startswith(("Glove", "Sleeve")) else 40.0) for n in names]
    total = 0
    for ob in objs:
        S.unwrap(ob)
        if ob.name.startswith("Glove"):
            H.tile_uvs(ob)
        tris = S.triangles(ob)
        total += tris
        print(f"[KNIFE] {ob.name}: {tris} triangles")
    zs = [S._to_unity(ob.matrix_world @ v.co).z for ob in objs for v in ob.data.vertices]
    print(f"[KNIFE] total {total} triangles (target 4000, max 6000), tip z {max(zs):.3f} (want {TIP_Z})")

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    S.export_fbx(objs[0])
    print(f"[KNIFE] {FBX_PATH}")


if __name__ == "__main__":
    main()
