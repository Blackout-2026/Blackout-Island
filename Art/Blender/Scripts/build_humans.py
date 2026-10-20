"""Builds the three humanoid agents: SM_Char_Patrol, SM_Char_Hunter and SM_Char_Commander (step C10). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_humans.py            (all three)
    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_humans.py -- Hunter  (one)

The three agents share ONE body and one skeleton (docs/reference/core-developer.md section 4.1) and differ in clothes, head gear
and weapon:
  Patrol    a police-style tactical operator: navy uniform, black plate-carrier vest with pouches and a radio, helmet with rails,
            night-vision mount, dark visor and ear cups, knee pads, gloves, a holstered pistol and an assault rifle with an optic.
  Hunter    a field soldier in camouflage like the owner's photo: a tan cap with a patch and a brim, sunglasses, a black hood over
            the whole head and face, a dark forest camouflage jacket and trousers (a camouflage texture, T_CharHunter_BaseColor) with leaf and cloth strips hanging from
            the shoulders, arms, waist, thighs, cap and rifle, a dark olive chest rig
            with pouches, black gloves, boots, a side bag, and an AK-style rifle held in both hands (the arms are modelled bent, the right
            hand on the pistol grip and the left under the handguard).
  Commander moved to build_commander.py (step C10b): a realistic female naval officer in white service dress, with a sculpted face,
            hair cards, three textured materials and a 14,000-triangle budget. "-- Commander" (or no argument) runs that script.
The styles come from the owner's reference photos; the shapes are my own and no one's likeness.

Rig: the robot's method (build_robot.py), reused. Rigid parts on pivots, every joint at rotation 0, rotation about the joint's local X,
one Empty root that cancels the FBX exporter's axis rotation (docs/art-pipeline.md section 16.2). Joints (1.87 m tall, feet on y 0,
facing +Z): Hips (root), Spine, Chest, Neck, Head, UpperArm_L/R, ForeArm_L/R, Hand_L/R, Thigh_L/R, Shin_L/R, Foot_L/R and Gun, whose
child MuzzlePoint is an Empty at the muzzle (Patrol and Hunter 0.74 m ahead of the socket, Commander 0.22 m).
Budget (ModelBudget, SM_Char_): 3,000 triangles target, 4,500 maximum, 3 materials. Flat colour materials only: the Unity side
creates the matching M_Char_* assets (CharacterMaterialBuilder), so Blender's colours are for the previews.

Output: Art/Blender/SM_Char_<Name>.blend and Assets/Models/Custom/SM_Char_<Name>.fbx
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_robot as R  # noqa: E402  (the rig kit: parts, merge, hidden-face cull, unwrap; importing it builds nothing)
from build_robot import ellipsoid, loft, rrect  # noqa: E402

REPO = R.REPO
NONE = R.NONE

# name, parent, offset from the parent (Unity axes), from docs/reference/core-developer.md 4.1
JOINTS = [
    ("Hips", None, (0.0, 0.98, 0.0)),
    ("Spine", "Hips", (0.0, 0.10, 0.0)),
    ("Chest", "Spine", (0.0, 0.20, 0.0)),
    ("Neck", "Chest", (0.0, 0.27, 0.0)),
    ("Head", "Neck", (0.0, 0.10, 0.0)),
    ("UpperArm_L", "Chest", (-0.20, 0.20, 0.0)),
    ("ForeArm_L", "UpperArm_L", (0.0, -0.29, 0.0)),
    ("Hand_L", "ForeArm_L", (0.0, -0.26, 0.0)),
    ("UpperArm_R", "Chest", (0.20, 0.20, 0.0)),
    ("ForeArm_R", "UpperArm_R", (0.0, -0.29, 0.0)),
    ("Hand_R", "ForeArm_R", (0.0, -0.26, 0.0)),
    ("Thigh_L", "Hips", (-0.10, -0.06, 0.0)),
    ("Shin_L", "Thigh_L", (0.0, -0.44, 0.0)),
    ("Foot_L", "Shin_L", (0.0, -0.42, 0.0)),
    ("Thigh_R", "Hips", (0.10, -0.06, 0.0)),
    ("Shin_R", "Thigh_R", (0.0, -0.44, 0.0)),
    ("Foot_R", "Shin_R", (0.0, -0.42, 0.0)),
    ("Gun", "Chest", (-0.04, 0.12, 0.20)),   # the weapon socket: world (-0.04, 1.40, 0.20)
]


def configure():
    """Points the robot kit's tables at the human skeleton."""
    R.JOINTS = JOINTS
    R.PARENT = {n: p for n, p, _ in JOINTS}
    R.OFFSET = {n: Vector(o) for n, _, o in JOINTS}
    R.JOINT_ID = {n: i for i, (n, _, _) in enumerate(JOINTS)}
    R.WORLD = {n: R.world(n) for n, _, _ in JOINTS}
    R.PARTS.clear()


# --------------------------------------------------------------------------- small helpers

def box(j, slot, c, s, bevel=0.0, seg=1, tilt=None):
    R.box(j, slot, c, s, bevel=bevel, seg=seg, tilt=tilt)


def cyl(j, slot, c, axis, r, length, segs=10, r2=None):
    R.cyl(j, slot, c, axis, r, length, segs=segs, r2=r2)


def limb(j, slot, top_y, length, r_top, r_bottom, segs=10, x=0.0, z=0.0):
    """A tapered limb hanging down from top_y (wider at the top)."""
    R.cyl(j, slot, (x, top_y - length / 2.0, z), 'y', r_bottom, length, segs=segs, r2=r_top)


def leaf(j, slot, x, y, z, length, width, sway=(0.0, 0.0), thick=0.010):
    """A hanging leaf or cloth strip: a thin pyramid whose point (the free end) sways away from the top edge. 6 triangles.
    (x, y, z) is the middle of the top edge in joint-local metres."""
    hw, ht = width / 2.0, thick / 2.0
    sx, sz = sway
    bm = bmesh.new()
    top = [bm.verts.new(R.B(R.W(j, (x + dx, y, z + dz)))) for dx, dz in ((-hw, -ht), (hw, -ht), (hw, ht), (-hw, ht))]
    tip = bm.verts.new(R.B(R.W(j, (x + sx, y - length, z + sz))))
    for i in range(4):
        bm.faces.new((top[i], top[(i + 1) % 4], tip))
    bm.faces.new(list(reversed(top)))
    R.store(j, bm, slot)


def wleaf(j, slot, c, length, width, sway=(0.0, 0.0)):
    """A leaf strip hanging from the WORLD point c."""
    p = Vector(c) - R.WORLD[j]
    leaf(j, slot, p.x, p.y, p.z, length, width, sway)


# Face-corner colour tags for the Hunter's eye area. After the unwrap, every face with one of these tags gets all its UVs moved onto
# one colour swatch of T_CharHunterSkin_BaseColor.png, so the skin, the eye white, the iris and the pupil share ONE material.
SW_SKIN, SW_WHITE, SW_IRIS, SW_BLACK = (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 1, 0), (1, 1, 1, 0)
SWATCH_UV = {SW_SKIN[:3]: (0.25, 0.75), SW_WHITE[:3]: (0.75, 0.75), SW_IRIS[:3]: (0.25, 0.25), SW_BLACK[:3]: (0.75, 0.25)}


def apply_swatches(ob):
    """Moves the UVs of every tagged face onto its colour swatch (see SWATCH_UV)."""
    me = ob.data
    attr = me.color_attributes.get("Mask")
    if attr is None or me.uv_layers.active is None:
        return
    uv = me.uv_layers.active
    for poly in me.polygons:
        for li in poly.loop_indices:
            c = tuple(attr.data[li].color)[:3]
            if sum(c) < 0.5:
                continue
            best = min(SWATCH_UV, key=lambda k: sum((a - b) ** 2 for a, b in zip(k, c)))
            uv.data[li].uv = SWATCH_UV[best]


def wbox(j, slot, c, s, bevel=0.0, seg=1, tilt=None):
    """A box whose centre c is in WORLD metres (Unity axes); the part still belongs to joint j."""
    R.box(j, slot, tuple(Vector(c) - R.WORLD[j]), s, bevel=bevel, seg=seg, tilt=tilt)


def wellipsoid(j, slot, c, r, u=10, v=5):
    R.ellipsoid(j, slot, tuple(Vector(c) - R.WORLD[j]), r, u=u, v=v)


def limb_w(j, slot, a, b, ra, rb, sides=8):
    """A tapered tube between two WORLD points a and b (radius ra at a, rb at b), closed at both ends. Arms are modelled bent, so they
    cannot be the straight hanging limbs of body(); the joints stay where the rig table puts them."""
    a, b = Vector(a), Vector(b)
    axis = (b - a).normalized()
    ref = Vector((0.0, 0.0, 1.0)) if abs(axis.z) < 0.9 else Vector((1.0, 0.0, 0.0))
    n1 = (ref - axis * ref.dot(axis)).normalized()
    n2 = axis.cross(n1)
    bm = bmesh.new()

    def ring(c, r):
        return [bm.verts.new(R.B(c + r * (math.cos(2.0 * math.pi * k / sides) * n1 + math.sin(2.0 * math.pi * k / sides) * n2)))
                for k in range(sides)]

    ra_ring, rb_ring = ring(a, ra), ring(b, rb)
    for k in range(sides):
        k2 = (k + 1) % sides
        bm.faces.new((ra_ring[k], ra_ring[k2], rb_ring[k2], rb_ring[k]))
    bm.faces.new(ra_ring)
    bm.faces.new(list(reversed(rb_ring)))
    R.store(j, bm, slot)


# --------------------------------------------------------------------------- the shared body

def body(s):
    """The one body of every human. s maps part groups to material slots: torso, hips, arm, hand, leg, boot, head, neck."""
    # hips and lower torso
    loft("Hips", s["hips"], [(-0.10, rrect(0.30, 0.20, 0.06)), (0.0, rrect(0.34, 0.22, 0.07)), (0.10, rrect(0.32, 0.21, 0.06))])
    loft("Spine", s["torso"], [(-0.02, rrect(0.30, 0.20, 0.06)), (0.10, rrect(0.31, 0.21, 0.07)), (0.22, rrect(0.32, 0.22, 0.07))])
    # chest, with the shoulder line
    loft("Chest", s["torso"], [(-0.02, rrect(0.32, 0.22, 0.06)), (0.06, rrect(0.38, 0.24, 0.07)), (0.20, rrect(0.42, 0.25, 0.09)),
                               (0.27, rrect(0.32, 0.21, 0.07))])
    cyl("Neck", s["neck"], (0.0, 0.05, 0.0), 'y', 0.050, 0.12, segs=8, r2=0.055)
    # head: a squashed sphere with jaw, nose and ears
    ellipsoid("Head", s["head"], (0.0, 0.07, 0.0), (0.097, 0.118, 0.105), u=10, v=6)
    box("Head", s["head"], (0.0, 0.01, 0.03), (0.13, 0.07, 0.12), bevel=0.01)
    box("Head", s["head"], (0.0, 0.065, 0.105), (0.03, 0.04, 0.04))
    for u in (-1, 1):
        box("Head", s["head"], (0.098 * u, 0.065, 0.0), (0.02, 0.045, 0.03))
    # arms and hands, legs and boots
    for u, side in ((-1, "L"), (1, "R")):
        up, fore, hand = f"UpperArm_{side}", f"ForeArm_{side}", f"Hand_{side}"
        sphere = R.sphere
        sphere(up, s["arm"], (0.0, 0.0, 0.0), 0.075, u=8, v=4)
        limb(up, s["arm"], 0.0, 0.30, 0.068, 0.056)
        sphere(fore, s["arm"], (0.0, 0.0, 0.0), 0.058, u=8, v=4)
        limb(fore, s["arm"], 0.0, 0.26, 0.056, 0.042)
        box(hand, s["hand"], (0.0, -0.05, 0.0), (0.075, 0.10, 0.04), bevel=0.008)
        box(hand, s["hand"], (0.045 * u, -0.04, 0.012), (0.03, 0.06, 0.03))
        thigh, shin, foot = f"Thigh_{side}", f"Shin_{side}", f"Foot_{side}"
        limb(thigh, s["leg"], 0.0, 0.45, 0.100, 0.074)
        sphere(shin, s["leg"], (0.0, 0.0, 0.0), 0.074, u=8, v=4)
        limb(shin, s["leg"], 0.0, 0.42, 0.072, 0.054)
        box(foot, s["boot"], (0.0, -0.015, 0.06), (0.125, 0.09, 0.28), bevel=0.014)
        cyl(foot, s["boot"], (0.0, 0.08, -0.005), 'y', 0.066, 0.14, segs=10)


def rifle(metal, dark, furniture, scope=False, optic=False, light=False):
    """The assault rifle of the Patrol and the Hunter (docs/reference/core-developer.md 4.5): 1.03 m, muzzle 0.74 m ahead of the socket."""
    j = "Gun"
    box(j, metal, (0.0, 0.0, 0.02), (0.06, 0.09, 0.42), bevel=0.004)                              # receiver
    box(j, metal, (0.0, -0.005, 0.35), (0.052, 0.06, 0.24), bevel=0.004)                          # handguard
    cyl(j, dark, (0.0, 0.01, 0.605), 'z', 0.013, 0.27, segs=8)                                      # barrel
    cyl(j, dark, (0.0, 0.01, 0.72), 'z', 0.020, 0.04, segs=8)                                       # muzzle device, its tip is MuzzlePoint
    box(j, dark, (0.0, -0.115, 0.07), (0.045, 0.16, 0.07), bevel=0.003, tilt=(14.0, (0.0, -0.115, 0.07)))   # magazine
    box(j, furniture, (0.0, -0.085, -0.09), (0.045, 0.10, 0.05), bevel=0.004, tilt=(-17.0, (0.0, -0.085, -0.09)))  # pistol grip
    box(j, furniture, (0.0, -0.01, -0.30), (0.05, 0.08, 0.22), bevel=0.004)                       # stock
    box(j, dark, (0.0, 0.055, 0.43), (0.01, 0.03, 0.012))                                           # front sight
    for z in (-0.04, 0.30):
        box(j, furniture, (0.0, 0.10, z), (0.02, 0.16, 0.016))                                       # sling straps up to the vest
    if optic:
        box(j, dark, (0.0, 0.075, 0.06), (0.05, 0.05, 0.10), bevel=0.006)                         # red-dot optic
    if scope:
        cyl(j, dark, (0.0, 0.075, 0.05), 'z', 0.024, 0.20, segs=10)                                 # scope tube
        for z in (-0.02, 0.10):
            box(j, dark, (0.0, 0.05, z), (0.03, 0.04, 0.02))
    if light:
        cyl(j, dark, (0.0, -0.045, 0.38), 'z', 0.018, 0.08, segs=8)                                 # weapon light


# --------------------------------------------------------------------------- Security Patrol

def build_patrol():
    CLOTH, GEAR, METAL = 0, 1, 2
    body({"torso": CLOTH, "hips": CLOTH, "arm": CLOTH, "hand": GEAR, "leg": CLOTH, "boot": GEAR, "head": GEAR, "neck": GEAR})
    # plate-carrier vest with a front plate, three pouches, a radio, a back panel and shoulder straps
    loft("Chest", GEAR, [(-0.04, rrect(0.37, 0.27, 0.07)), (0.06, rrect(0.41, 0.30, 0.08)), (0.20, rrect(0.41, 0.29, 0.08)),
                         (0.27, rrect(0.32, 0.24, 0.06))])
    box("Chest", GEAR, (0.0, 0.14, 0.145), (0.30, 0.24, 0.03), bevel=0.006)                        # front plate
    for x in (-0.13, 0.0, 0.13):
        box("Chest", GEAR, (x, 0.02, 0.165), (0.09, 0.11, 0.06), bevel=0.006)                      # three pouches
    box("Chest", GEAR, (0.16, 0.22, 0.10), (0.06, 0.10, 0.04), bevel=0.004)                        # radio on the shoulder side
    cyl("Chest", GEAR, (0.17, 0.30, 0.10), 'y', 0.005, 0.10, segs=5)                               # its antenna
    box("Chest", GEAR, (0.0, 0.12, -0.15), (0.30, 0.24, 0.03), bevel=0.006)                        # back panel
    for u in (-1, 1):
        box("Chest", GEAR, (0.11 * u, 0.28, 0.0), (0.07, 0.03, 0.26), bevel=0.004)                  # shoulder straps
    cyl("Hips", GEAR, (0.0, 0.04, 0.0), 'y', 0.18, 0.06, segs=12)                                   # belt
    for u in (-1, 1):
        box("Hips", GEAR, (0.16 * u, 0.04, 0.05), (0.05, 0.09, 0.08), bevel=0.005)                     # belt pouches
        box(f"Thigh_{'L' if u < 0 else 'R'}", CLOTH, (0.105 * u, -0.22, 0.02), (0.04, 0.13, 0.10), bevel=0.006)  # cargo pockets
        box("Chest", GEAR, (0.215 * u, 0.04, 0.04), (0.03, 0.12, 0.09), bevel=0.005)                   # side plate pouches
    box("Thigh_R", GEAR, (0.085, -0.15, 0.0), (0.05, 0.16, 0.10), bevel=0.006)                     # holster
    box("Thigh_R", METAL, (0.085, -0.04, 0.0), (0.03, 0.05, 0.08))
    for u, side in ((-1, "L"), (1, "R")):
        box(f"Shin_{side}", GEAR, (0.0, 0.0, 0.07), (0.10, 0.10, 0.05), bevel=0.01)                 # knee pads
        box(f"ForeArm_{side}", GEAR, (0.0, 0.0, -0.045), (0.07, 0.07, 0.03), bevel=0.008)           # elbow pads
    # helmet: a shell with a brim, side rails, a night-vision mount, a dark visor and ear cups
    ellipsoid("Head", GEAR, (0.0, 0.10, -0.005), (0.128, 0.115, 0.13), u=12, v=6)
    cyl("Head", GEAR, (0.0, 0.04, 0.0), 'y', 0.135, 0.03, segs=12)
    for u in (-1, 1):
        box("Head", GEAR, (0.13 * u, 0.10, 0.0), (0.015, 0.05, 0.12), bevel=0.003)
        cyl("Head", GEAR, (0.125 * u, 0.06, -0.01), 'x', 0.040, 0.03, segs=8)
    box("Head", METAL, (0.0, 0.17, 0.12), (0.05, 0.04, 0.04), bevel=0.004)
    box("Head", METAL, (0.0, 0.075, 0.105), (0.19, 0.06, 0.06), bevel=0.008)
    rifle(METAL, METAL, GEAR, optic=True, light=True)


# --------------------------------------------------------------------------- Hunter

RIFLE_DX = 0.12   # the Hunter's rifle sits this far to the right of the socket, in front of his right chest, like a rifle at the ready


def ak_rifle(camo, dark, dx=RIFLE_DX):
    """An AK-style rifle at the Gun socket: 1.03 m, the muzzle device ends 0.74 m ahead of the socket (docs/reference/core-developer.md 4.5).
    Camouflage-taped wooden furniture, a black receiver and a curved magazine. dx moves it to the right."""
    j = "Gun"
    box(j, dark, (dx, 0.0, 0.03), (0.050, 0.075, 0.40), bevel=0.004)                                # receiver
    box(j, dark, (dx, 0.045, 0.05), (0.045, 0.02, 0.34), bevel=0.004)                               # dust cover
    box(j, camo, (dx, 0.0, 0.35), (0.052, 0.062, 0.25), bevel=0.006)                                # wooden handguard
    cyl(j, dark, (dx, 0.04, 0.34), 'z', 0.011, 0.34, segs=6)                                        # gas tube
    cyl(j, dark, (dx, 0.0, 0.60), 'z', 0.012, 0.26, segs=6)                                         # barrel
    cyl(j, dark, (dx, 0.0, 0.72), 'z', 0.018, 0.05, segs=7)                                         # muzzle brake, its tip is MuzzlePoint
    box(j, dark, (dx, 0.04, 0.665), (0.012, 0.045, 0.015))                                          # front sight
    box(j, dark, (dx, -0.095, 0.072), (0.040, 0.125, 0.062), bevel=0.004, tilt=(8.0, (dx, -0.095, 0.072)))   # magazine, upper
    box(j, dark, (dx, -0.178, 0.098), (0.040, 0.105, 0.062), bevel=0.004, tilt=(18.0, (dx, -0.178, 0.098)))  # magazine, lower (the curve)
    box(j, camo, (dx, -0.075, -0.09), (0.040, 0.10, 0.045), bevel=0.004, tilt=(-17.0, (dx, -0.075, -0.09)))  # pistol grip
    box(j, camo, (dx, -0.005, -0.225), (0.045, 0.075, 0.075), bevel=0.005)                          # stock, short: the butt is tucked against the chest


def build_hunter():
    CAMO, SKIN, DARK = 0, 1, 2   # uniform and cap (camouflage texture), the skin round the eyes, black (mask, vest, bag, rifle, gloves, boots)
    LEAF = DARK                  # the vest, pouches, bag and patches are black tactical gear

    # --- trousers, belt and jacket: smooth rounded shells instead of boxes
    loft("Hips", CAMO, [(-0.10, rrect(0.37, 0.275, 0.10)), (0.0, rrect(0.40, 0.285, 0.11)), (0.10, rrect(0.39, 0.28, 0.10))])
    cyl("Hips", DARK, (0.0, 0.035, 0.0), 'y', 0.205, 0.05, segs=12)                                   # belt
    loft("Spine", CAMO, [(-0.04, rrect(0.385, 0.285, 0.10)), (0.10, rrect(0.395, 0.295, 0.105)), (0.22, rrect(0.415, 0.30, 0.105))])
    loft("Chest", CAMO, [(-0.02, rrect(0.42, 0.30, 0.10)), (0.07, rrect(0.47, 0.32, 0.11)), (0.20, rrect(0.50, 0.33, 0.12)),
                         (0.27, rrect(0.36, 0.27, 0.10))])
    cyl("Chest", CAMO, (0.0, 0.285, 0.0), 'y', 0.095, 0.05, segs=10, r2=0.082)                      # jacket collar
    # --- olive chest rig: a shell over the chest, three magazine pouches, a flap pocket, shoulder straps and a back panel
    loft("Chest", LEAF, [(-0.05, rrect(0.45, 0.345, 0.11, cz=0.012)), (0.07, rrect(0.50, 0.365, 0.12, cz=0.014)),
                         (0.19, rrect(0.50, 0.36, 0.12, cz=0.012)), (0.23, rrect(0.43, 0.31, 0.10, cz=0.008))])
    for x in (-0.115, 0.0, 0.115):
        box("Chest", LEAF, (x, 0.00, 0.220), (0.095, 0.110, 0.055), bevel=0.007)                      # magazine pouches
    box("Chest", LEAF, (0.0, 0.14, 0.215), (0.23, 0.09, 0.032), bevel=0.006)                       # upper pocket
    for u in (-1, 1):
        box("Chest", LEAF, (0.115 * u, 0.236, 0.0), (0.085, 0.032, 0.28), bevel=0.005, tilt=(0.0, (0.0, 0.236, 0.0)))   # shoulder straps
    # --- neck, balaclava and head
    cyl("Neck", DARK, (0.0, 0.055, 0.0), 'y', 0.062, 0.14, segs=10, r2=0.078)                       # the gaiter round the neck
    # the head under the hood, from the chin up: a long face with a jaw and cheekbones, a high forehead and a rounded back of the head
    # (sections of width, depth and a forward offset; the cranium reaches back, the face forward)
    loft("Head", DARK, [(-0.050, rrect(0.070, 0.090, 0.030, cz=0.034)), (-0.022, rrect(0.100, 0.125, 0.040, cz=0.030)),
                        (0.020, rrect(0.128, 0.175, 0.050, cz=0.012)), (0.060, rrect(0.146, 0.195, 0.060, cz=0.000)),
                        (0.100, rrect(0.154, 0.208, 0.066, cz=-0.006)), (0.140, rrect(0.152, 0.212, 0.068, cz=-0.014)),
                        (0.175, rrect(0.128, 0.190, 0.060, cz=-0.018)), (0.198, rrect(0.075, 0.125, 0.040, cz=-0.016))])
    for u in (-1, 1):
        ellipsoid("Head", DARK, (0.074 * u, 0.072, -0.012), (0.009, 0.030, 0.020), u=6, v=4)        # ears under the hood
    # the only skin that shows: a band round the eyes (a flat ellipsoid lying on the front of the head), with real eyes in it:
    # the white of the eye, the iris, the pupil, a dark lid line and a brow. Tagged with swatches of the one skin texture.
    ellipsoid("Head", SKIN, (0.0, 0.102, 0.085), (0.052, 0.022, 0.022), u=8, v=4, mask=SW_SKIN)
    for u in (-1, 1):
        x = 0.027 * u
        ellipsoid("Head", SKIN, (x, 0.1025, 0.1025), (0.0105, 0.0060, 0.0045), u=6, v=3, mask=SW_WHITE)             # the eye white
        ellipsoid("Head", SKIN, (x + 0.0006 * u, 0.1025, 0.1052), (0.0050, 0.0050, 0.0022), u=6, v=3, mask=SW_IRIS)   # iris
        ellipsoid("Head", SKIN, (x + 0.0006 * u, 0.1025, 0.1068), (0.0022, 0.0022, 0.0010), u=4, v=3, mask=SW_BLACK)  # pupil
        R.box("Head", SKIN, (x, 0.1100, 0.1030), (0.0240, 0.0022, 0.0045), mask=SW_BLACK)                         # upper lid line
        R.box("Head", SKIN, (x + 0.002 * u, 0.1205, 0.0985), (0.0320, 0.0070, 0.0080), mask=SW_IRIS)              # brow
    # the nose under the mask: a bridge, a rounded tip and a nostril taper, draped in the black fabric (not a block)
    loft("Head", DARK, [(0.097, rrect(0.018, 0.022, 0.007, cs=1, cz=0.090)), (0.074, rrect(0.024, 0.028, 0.009, cs=1, cz=0.096)),
                        (0.054, rrect(0.034, 0.032, 0.012, cs=1, cz=0.098)), (0.040, rrect(0.030, 0.024, 0.011, cs=1, cz=0.096))])
    ellipsoid("Head", CAMO, (0.0, 0.158, -0.016), (0.110, 0.066, 0.122), u=12, v=5)                 # cap crown
    box("Head", CAMO, (0.0, 0.130, 0.118), (0.150, 0.012, 0.078), bevel=0.004)                      # cap brim
    box("Head", LEAF, (0.0, 0.178, 0.102), (0.055, 0.030, 0.010))                                   # velcro patch on the cap

    # --- a rucksack on the back, with a rolled mat on top and side pockets (camouflage cloth, black straps)
    wbox("Chest", CAMO, (0.0, 1.34, -0.245), (0.34, 0.40, 0.20), bevel=0.030)
    wbox("Chest", DARK, (0.0, 1.36, -0.352), (0.22, 0.20, 0.025), bevel=0.006)
    for u in (-1, 1):
        wbox("Chest", CAMO, (0.19 * u, 1.27, -0.235), (0.075, 0.20, 0.13))
    cyl("Chest", DARK, (0.0, 0.285, -0.250), 'x', 0.050, 0.28, segs=6)

    # --- the rifle, held in both hands
    ak_rifle(CAMO, DARK)

    # --- arms: jacket sleeves bent to hold the rifle (world points: shoulder, elbow, wrist), black gloves, a watch on the left wrist
    rdx = RIFLE_DX - 0.04                        # world x of the rifle's centre line
    arms = {
        "R": dict(sh=(0.20, 1.47, 0.0), el=(0.265, 1.165, -0.02), wr=(0.160, 1.290, 0.165), hand=(rdx + 0.065, 1.300, 0.195)),
        "L": dict(sh=(-0.20, 1.47, 0.0), el=(-0.185, 1.295, 0.285), wr=(rdx - 0.01, 1.325, 0.415), hand=(rdx + 0.005, 1.335, 0.470)),
    }
    for side, a in arms.items():
        up, fore, hand = f"UpperArm_{side}", f"ForeArm_{side}", f"Hand_{side}"
        wellipsoid(up, CAMO, a["sh"], (0.092, 0.092, 0.092), u=8, v=4)                               # shoulder cap
        limb_w(up, CAMO, a["sh"], a["el"], 0.082, 0.066, sides=8)                                    # upper arm
        wellipsoid(fore, CAMO, a["el"], (0.068, 0.068, 0.068), u=8, v=4)                             # elbow
        limb_w(fore, CAMO, a["el"], a["wr"], 0.066, 0.053, sides=8)                                  # forearm
        wellipsoid(hand, DARK, a["hand"], (0.056, 0.050, 0.062), u=8, v=5)                           # gloved fist
    # gloved fingers: the right hand wraps the front of the pistol grip, the left curls under and up the side of the handguard
    for k in range(4):
        limb_w("Hand_R", DARK, (rdx + 0.075, 1.335 - 0.022 * k, 0.210), (rdx - 0.040, 1.335 - 0.022 * k, 0.205), 0.0115, 0.0105, sides=5)
        limb_w("Hand_L", DARK, (rdx - 0.040, 1.352, 0.425 + 0.0255 * k), (rdx + 0.060, 1.352, 0.425 + 0.0255 * k), 0.0115, 0.0105, sides=5)
    limb_w("Hand_R", DARK, (rdx + 0.085, 1.335, 0.130), (rdx + 0.060, 1.372, 0.200), 0.0135, 0.0115, sides=5)   # right thumb along the receiver
    wbox("UpperArm_R", LEAF, (0.285, 1.34, 0.0), (0.022, 0.095, 0.08), bevel=0.003)                  # velcro patch on the right sleeve
    wbox("ForeArm_L", DARK, (arms["L"]["wr"][0] - 0.0, arms["L"]["wr"][1] + 0.052, arms["L"]["wr"][2]), (0.05, 0.016, 0.05), bevel=0.004)  # wrist watch

    # --- legs: camouflage trousers, cargo pockets, boots
    for u, side in ((-1, "L"), (1, "R")):
        thigh, shin, foot = f"Thigh_{side}", f"Shin_{side}", f"Foot_{side}"
        x = 0.105 * u
        limb_w(thigh, CAMO, (x, 0.93, 0.0), (x, 0.50, 0.012), 0.118, 0.092, sides=9)
        wellipsoid(shin, CAMO, (x, 0.50, 0.012), (0.092, 0.092, 0.092), u=8, v=4)
        limb_w(shin, CAMO, (x, 0.50, 0.012), (x, 0.13, 0.0), 0.088, 0.078, sides=9)
        wbox(thigh, CAMO, (0.222 * u, 0.74, 0.02), (0.045, 0.15, 0.125), bevel=0.008)                # cargo pocket
        box(foot, DARK, (0.0, -0.012, 0.065), (0.130, 0.095, 0.295), bevel=0.014)                      # boot
        cyl(foot, DARK, (0.0, 0.075, -0.005), 'y', 0.082, 0.15, segs=10)                             # boot shaft

    # --- leaf and cloth strips, like the ghillie scraps on a field soldier's suit: shoulders, arms, waist, thighs, cap, bag and rifle
    rng = random.Random(11)
    n = [0]

    def scrap(j, c, lmin, lmax, wmin=0.055, wmax=0.095, sway=0.06):
        slot = CAMO
        n[0] += 1
        wleaf(j, slot, c, rng.uniform(lmin, lmax), rng.uniform(wmin, wmax), (rng.uniform(-sway, sway), rng.uniform(-sway, sway)))

    for k in range(16):                                                                                 # shoulders and upper back
        ang = rng.uniform(0.0, 2.0 * math.pi)
        scrap("Chest", (0.23 * math.cos(ang), 1.50 + rng.uniform(-0.02, 0.01), 0.16 * math.sin(ang)), 0.16, 0.32)
    for side, u in (("L", -1), ("R", 1)):
        a = arms[side]
        for k in range(8):                                                                              # along each upper arm, outside
            t = rng.uniform(0.05, 0.9)
            pt = (a["sh"][0] + (a["el"][0] - a["sh"][0]) * t + 0.075 * u, a["sh"][1] + (a["el"][1] - a["sh"][1]) * t,
                  a["sh"][2] + (a["el"][2] - a["sh"][2]) * t + rng.uniform(-0.03, 0.03))
            scrap(f"UpperArm_{side}", pt, 0.11, 0.26)
        for k in range(4):                                                                              # forearm
            t = rng.uniform(0.1, 0.8)
            pt = (a["el"][0] + (a["wr"][0] - a["el"][0]) * t, a["el"][1] + (a["wr"][1] - a["el"][1]) * t - 0.04,
                  a["el"][2] + (a["wr"][2] - a["el"][2]) * t + rng.uniform(-0.02, 0.02))
            scrap(f"ForeArm_{side}", pt, 0.08, 0.16, 0.040, 0.065, 0.03)
    for k in range(14):                                                                                 # a skirt of scraps round the waist
        ang = rng.uniform(0.0, 2.0 * math.pi)
        scrap("Hips", (0.215 * math.cos(ang), 1.00, 0.15 * math.sin(ang)), 0.20, 0.36, 0.055, 0.095, 0.07)
    for u, side in ((-1, "L"), (1, "R")):
        for k in range(4):                                                                              # thighs
            scrap(f"Thigh_{side}", (0.105 * u + rng.uniform(-0.08, 0.08), 0.82 + rng.uniform(-0.06, 0.06), rng.uniform(-0.07, 0.07)),
                  0.12, 0.24, 0.050, 0.080, 0.04)
    for k in range(2):                                                                                  # round the cap and its back
        ang = rng.uniform(0.6 * math.pi, 2.4 * math.pi)
        scrap("Head", (0.100 * math.cos(ang), 1.78, 0.110 * math.sin(ang) - 0.015), 0.05, 0.10, 0.030, 0.050, 0.03)
    for k in range(2):
        scrap("Hips", (0.262 + rng.uniform(-0.03, 0.03), 0.98, -0.03 + rng.uniform(-0.08, 0.08)), 0.10, 0.18, 0.040, 0.060, 0.04)   # on the bag
    for k in range(3):
        scrap("Gun", (RIFLE_DX - 0.04 + 0.0 + 0.2 * 0.0, 1.40 - 0.03, 0.30 + 0.07 * k), 0.07, 0.14, 0.030, 0.045, 0.02)      # cloth tied to the handguard

    # --- a side bag on the right hip with a black strap across the chest
    wbox("Hips", LEAF, (0.292, 0.91, -0.03), (0.090, 0.200, 0.225), bevel=0.012, seg=2)
    wbox("Hips", LEAF, (0.292, 1.015, -0.03), (0.095, 0.038, 0.235), bevel=0.009)                    # its flap
    limb_w("Chest", DARK, (-0.165, 1.50, 0.02), (0.262, 1.07, -0.05), 0.016, 0.016, sides=4)         # strap, left shoulder to bag


# --------------------------------------------------------------------------- scene, materials, export

CHARACTERS = {
    "Patrol": dict(build=build_patrol, muzzle_z=0.74, muzzle_y=0.01, root="CharPatrol", fbx="SM_Char_Patrol",
                   mats=[("M_Char_Patrol_Cloth", "#2b3546", 0.0, 0.85), ("M_Char_Patrol_Gear", "#1a1b1d", 0.1, 0.8),
                         ("M_Char_Patrol_Metal", "#161718", 0.6, 0.4)]),
    "Hunter": dict(build=build_hunter, muzzle_z=0.74, muzzle_y=0.0, muzzle_x=RIFLE_DX, root="CharHunter", fbx="SM_Char_Hunter",
                   cube_uv=True, camo_texture="T_CharHunter_BaseColor.png", skin_texture="T_CharHunterSkin_BaseColor.png", swatches=True,
                   mats=[("M_Char_Hunter_Camo", "#27301e", 0.0, 0.9), ("M_Char_Hunter_Skin", "#b08368", 0.0, 0.7),
                         ("M_Char_Hunter_Gear", "#161718", 0.3, 0.6)]),
}


def srgb_to_linear(hex_colour):
    h = hex_colour.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def make_material(name, hex_colour, metallic, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = srgb_to_linear(hex_colour)
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    return mat


def cube_unwrap(ob):
    """UVs by a cube projection at one UV unit per metre, so a seamless cloth texture keeps the same scale on every part."""
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=1.0)
    bpy.ops.object.mode_set(mode='OBJECT')


def preview_texture(mat, filename):
    """Shows the camouflage texture on the Blender material, for the previews only (Unity's material has its own)."""
    path = os.path.join(REPO, "Assets", "Textures", "Models", filename)
    if not os.path.exists(path):
        return
    nt = mat.node_tree
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(path)
    nt.links.new(tex.outputs["Color"], nt.nodes["Principled BSDF"].inputs["Base Color"])


def split_into_hierarchy(merged, mats, cfg):
    """One mesh object per joint under one Empty root rotated +90 degrees about X (see build_robot.py), plus the MuzzlePoint Empty."""
    root = bpy.data.objects.new(cfg["root"], None)
    root.empty_display_type = 'PLAIN_AXES'
    root.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(root)
    objs = {}
    for name, parent, off in JOINTS:
        bm = bmesh.new()
        bm.from_mesh(merged.data)
        jid = bm.faces.layers.int["jid"]
        drop = [f for f in bm.faces if f[jid] != R.JOINT_ID[name]]
        bmesh.ops.delete(bm, geom=drop, context='FACES')
        origin = R.WORLD[name]
        for v in bm.verts:
            u = Vector((v.co.x, v.co.z, v.co.y)) - origin
            v.co = Vector((u.x, u.y, -u.z))
        used = sorted({f.material_index for f in bm.faces})
        remap = {old: new for new, old in enumerate(used)}
        for f in bm.faces:
            f.material_index = remap[f.material_index]
        bm.faces.layers.int.remove(jid)
        me = bpy.data.meshes.new(name)
        bm.to_mesh(me)
        bm.free()
        for slot in used:
            me.materials.append(mats[slot])
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = root if parent is None else objs[parent]
        ob.location = (off[0], off[1], -off[2])
        objs[name] = ob
    muzzle = bpy.data.objects.new("MuzzlePoint", None)
    muzzle.empty_display_type = 'ARROWS'
    muzzle.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(muzzle)
    muzzle.parent = objs["Gun"]
    muzzle.location = (cfg.get("muzzle_x", 0.0), cfg["muzzle_y"], -cfg["muzzle_z"])  # the root's rotated frame: z mirrored
    return root, objs


def export_fbx(path):
    for ob in bpy.data.objects:
        ob.select_set(ob.type in ('MESH', 'EMPTY'))
    bpy.context.view_layer.objects.active = bpy.data.objects["Hips"]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)


def build(kind):
    cfg = CHARACTERS[kind]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    configure()
    mats = [make_material(*m) for m in cfg["mats"]]
    if cfg.get("camo_texture"):
        preview_texture(mats[0], cfg["camo_texture"])
    if cfg.get("skin_texture"):
        preview_texture(mats[1], cfg["skin_texture"])
    cfg["build"]()
    bm, skipped = R.merge()
    removed = R.cleanup(bm)
    me = bpy.data.meshes.new("CharAll")
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    merged = bpy.data.objects.new("CharAll", me)
    bpy.context.scene.collection.objects.link(merged)
    if cfg.get("cube_uv"):
        cube_unwrap(merged)
        if cfg.get("swatches"):
            apply_swatches(merged)
    else:
        R.unwrap(merged)
    root, objs = split_into_hierarchy(merged, mats, cfg)
    bpy.data.objects.remove(merged)
    bpy.data.meshes.remove(me)
    bpy.context.view_layer.update()

    total = 0
    for name, _, _ in JOINTS:
        tris = sum(len(p.vertices) - 2 for p in objs[name].data.polygons)
        total += tris
    lo, hi = R.unity_bounds(objs)
    size = hi - lo
    print("=" * 70)
    print(f"[{kind}] {total} triangles (target 3000, max 4500), {len(mats)} materials, parts {len(R.PARTS)}, hidden faces removed {removed}, skipped {skipped}")
    print(f"[{kind}] size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   y {lo.y:.3f}..{hi.y:.3f}   z {lo.z:.3f}..{hi.z:.3f}   (height 1.87)")
    biggest = sorted(((sum(len(p.vertices) - 2 for p in o.data.polygons), n) for n, o in objs.items()), reverse=True)[:4]
    print(f"[{kind}] heaviest joints {biggest}")

    blend = os.path.join(REPO, "Art", "Blender", cfg["fbx"] + ".blend")
    fbx = os.path.join(REPO, "Assets", "Models", "Custom", cfg["fbx"] + ".fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    export_fbx(fbx)
    print(f"[{kind}] {blend}")
    print(f"[{kind}] {fbx}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for kind in (argv or list(CHARACTERS) + ["Commander"]):
        if kind == "Commander":
            import build_commander  # the realistic Commander has its own script (it resets the Blender scene itself)
            build_commander.main()
        else:
            build(kind)


if __name__ == "__main__":
    main()
