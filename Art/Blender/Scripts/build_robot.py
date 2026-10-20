"""Builds SM_SecurityRobot.blend: the Tactical Guard security robot (step C8). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_robot.py

The robot is rigid parts on pivots, not a skinned rig: ProceduralRobotAnimator finds 12 joints by exact name and rotates each
one about its LOCAL X axis, adding to the rest rotation (docs/art-pipeline.md section 11). So the Blender file has to deliver
three things, and this script is built around them:

 1. One Blender object per joint, named exactly like RobotRig.Joints, parented as in the table (Foot_L under Shin_L under
    Thigh_L under Pelvis), each object's origin at the rotation centre. The 16 joint objects carry the meshes, under one
    Empty "SecurityRobot" (Unity makes that Empty the model root, so Pelvis is a direct child of it).
 2. Rest pose = rotation (0, 0, 0) on every joint after import. Measured with a scratch Unity project (not guessed): the FBX
    exporter with Apply Transform OFF puts a 90 degree rotation on the root, and with Apply Transform ON it breaks the third
    level of a hierarchy (Shin_L came out rotated 270 degrees). The fix is to cancel the exporter's own axis conversion: the
    root Empty is rotated +90 degrees about X in Blender, and everything below it is modelled in that rotated, Y-up
    frame. In the root's frame the numbers are Unity's, except z is mirrored (the importer mirrors Z, as the generator proved):
        local = (unity x, unity y, -unity z)         Blender world = (unity x, unity z, unity y)   (same axis swap as the generator)
 3. The size and the pivots of the catalog: 1.07 x 2.38 x 0.64 m, feet on y 0, front +Z, weapon pod on the right arm (+X).

Every number below is a UNITY-frame metre value local to its joint, so it can be compared with RobotPlaceholder.cs, the
prototype (core-developer.md section 3) and RobotRig.cs. B() turns it into the Blender world point.

Pipeline: parts per joint (kit below) -> merged into one mesh with a face attribute "jid" -> faces hidden inside their own joint
removed -> triangulated -> UV unwrapped and packed once (one atlas for all 16 parts) -> split again into one mesh per joint ->
hierarchy -> .blend. bake_robot.py then bakes the textures and writes the FBX.
Budget: 5,000 triangles target (7,500 max), 4 material slots (docs/art-pipeline.md section 6).
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_parts as P  # noqa: E402  (B, axis matrices)

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_SecurityRobot.blend")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_SecurityRobot.fbx")
B = P.B

# material slots, in this order (names equal the Unity assets of RobotMaterialBuilder)
BODY, JOINT, EYE, CELLS = 0, 1, 2, 3
SLOTS = ["M_SecurityRobot_Body", "M_SecurityRobot_Joint", "M_SecurityRobot_Eye", "M_SecurityRobot_Cells"]
# base colour, metallic, roughness, emission (None = no glow): the prototype's values (core-developer.md 3.4, 8.1)
PALETTE = {"M_SecurityRobot_Body": ((0.72, 0.74, 0.77, 1), 0.25, 0.45, None),
           "M_SecurityRobot_Joint": ((0.016, 0.017, 0.019, 1), 0.85, 0.35, None),
           "M_SecurityRobot_Eye": ((0.02, 0.003, 0.002, 1), 0.0, 0.3, (1.0, 0.07, 0.03, 1)),
           "M_SecurityRobot_Cells": ((0.002, 0.02, 0.025, 1), 0.0, 0.3, (0.1, 0.65, 0.75, 1))}

# face-corner colour "Mask" (R accent yellow, G bare steel, B hazard stripes), read by the bake
NONE, ACC, STEEL, HAZ = (0, 0, 0, 0), (1, 0, 0, 0), (0, 1, 0, 0), (1, 0, 1, 0)

# name, parent, offset from the parent in Unity axes (RobotRig.cs, art-pipeline.md 11.1); ScannerDot is the one extra object
JOINTS = [
    ("Pelvis", None, (0.0, 1.05, 0.0)),
    ("Spine", "Pelvis", (0.0, 0.14, 0.0)),
    ("Chest", "Spine", (0.0, 0.30, 0.0)),
    ("Head", "Chest", (0.0, 0.44, 0.02)),
    ("UpperArm_L", "Chest", (-0.37, 0.27, 0.0)),
    ("ForeArm_L", "UpperArm_L", (0.0, -0.34, 0.0)),
    ("Hand_L", "ForeArm_L", (0.0, -0.32, 0.0)),
    ("UpperArm_R", "Chest", (0.37, 0.27, 0.0)),
    ("ForeArm_R", "UpperArm_R", (0.0, -0.34, 0.0)),
    ("Hand_R", "ForeArm_R", (0.0, -0.32, 0.0)),
    ("Thigh_L", "Pelvis", (-0.17, -0.06, 0.0)),
    ("Shin_L", "Thigh_L", (0.0, -0.42, 0.13)),
    ("Foot_L", "Shin_L", (0.0, -0.43, -0.15)),
    ("Thigh_R", "Pelvis", (0.17, -0.06, 0.0)),
    ("Shin_R", "Thigh_R", (0.0, -0.42, 0.13)),
    ("Foot_R", "Shin_R", (0.0, -0.43, -0.15)),
    ("ScannerDot", "Head", (0.0, 0.07, 0.15)),
]
PARENT = {n: p for n, p, _ in JOINTS}
OFFSET = {n: Vector(o) for n, _, o in JOINTS}
JOINT_ID = {n: i for i, (n, _, _) in enumerate(JOINTS)}
TARGET_SIZE = (1.07, 2.38, 0.64)  # catalog: PropCatalog "SecurityRobot_Visual"
BEVEL_ANGLE = math.radians(35.0)


def world(joint):
    """Unity world position of a joint in the rest pose."""
    total = Vector((0.0, 0.0, 0.0))
    while joint is not None:
        total += OFFSET[joint]
        joint = PARENT[joint]
    return total


WORLD = {n: world(n) for n, _, _ in JOINTS}
PARTS = []  # (joint, verts in Blender world, faces, slot, mask)


def W(joint, p):
    return WORLD[joint] + Vector(p)


# --------------------------------------------------------------------------- kit


def unity_rot_x(deg):
    a = math.radians(deg)
    return Matrix(((1, 0, 0), (0, math.cos(a), -math.sin(a)), (0, math.sin(a), math.cos(a))))


def store(joint, bm, slot, bevel=0.0, seg=1, mask=NONE, tilt=None):
    """tilt = (degrees, pivot): rotate the part about the joint's X axis through the pivot (digitigrade legs are modelled sloped)."""
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0.0:
        bm.edges.ensure_lookup_table()
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > BEVEL_ANGLE]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET', segments=seg, profile=0.5, affect='EDGES', clamp_overlap=True)
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    verts = [v.co.copy() for v in bm.verts]
    if tilt is not None:
        deg, pivot = tilt
        piv = W(joint, pivot)
        rot = unity_rot_x(deg)
        out = []
        for v in verts:
            u = Vector((v.x, v.z, v.y)) - piv  # Blender world -> Unity world
            u = rot @ u + piv
            out.append(B(u))
        verts = out
    PARTS.append((joint, verts, [[v.index for v in f.verts] for f in bm.faces], slot, mask))
    bm.free()


def box(j, slot, c, s, bevel=0.0, seg=1, mask=NONE, tilt=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(s[0], s[2], s[1]), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(W(j, c)), verts=bm.verts)
    store(j, bm, slot, bevel, seg, mask, tilt)


def cyl(j, slot, c, axis, r, length, segs=10, r2=None, bevel=0.0, mask=NONE, tilt=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r if r2 is None else r2, depth=length)
    bmesh.ops.transform(bm, matrix=P._axis_matrix(axis), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(W(j, c)), verts=bm.verts)
    store(j, bm, slot, bevel, 1, mask, tilt)


def sphere(j, slot, c, r, u=10, v=5, mask=NONE):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=r)
    bmesh.ops.translate(bm, vec=B(W(j, c)), verts=bm.verts)
    store(j, bm, slot, 0.0, 1, mask)


def tube(j, slot, path, r, sides=6, mask=NONE):
    """A round hose swept along joint-local points (the cables from the back pack to the neck)."""
    pts = [B(W(j, p)) for p in path]
    n = len(pts)
    bm = bmesh.new()
    rings, prev = [], None
    for i in range(n):
        t = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
        nrm = (ref - t * ref.dot(t)).normalized() if prev is None else (prev - t * prev.dot(t)).normalized()
        prev = nrm
        bin_ = t.cross(nrm)
        rings.append([bm.verts.new(pts[i] + r * (math.cos(2 * math.pi * k / sides) * nrm + math.sin(2 * math.pi * k / sides) * bin_))
                      for k in range(sides)])
    for i in range(n - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    store(j, bm, slot, 0.0, 1, mask)


def ellipsoid(j, slot, c, r, u=12, v=6, mask=NONE):
    """A stretched sphere with radii r = (rx, ry, rz): the big round shoulder pauldrons."""
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1.0)
    bmesh.ops.scale(bm, vec=(r[0], r[2], r[1]), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(W(j, c)), verts=bm.verts)
    store(j, bm, slot, 0.0, 1, mask)


def rrect(w, d, r, cs=2, cx=0.0, cz=0.0):
    """A rounded rectangle in the x-z plane (width w, depth d, corner radius r), 4 * (cs + 1) points, centred on (cx, cz)."""
    r = min(r, w / 2.0 - 1e-4, d / 2.0 - 1e-4)
    pts = []
    for ox, oz, a0 in ((w / 2 - r, d / 2 - r, 0.0), (-(w / 2 - r), d / 2 - r, 90.0), (-(w / 2 - r), -(d / 2 - r), 180.0), (w / 2 - r, -(d / 2 - r), 270.0)):
        for k in range(cs + 1):
            a = math.radians(a0 + 90.0 * k / cs)
            pts.append((cx + ox + r * math.cos(a), cz + oz + r * math.sin(a)))
    return pts


def loft(j, slot, secs, mask=NONE, tilt=None):
    """Sections (y, [(x, z), ...]) with the same point count, joined into a closed shell: helmet, torso, limbs."""
    bm = bmesh.new()
    rings = [[bm.verts.new(B(W(j, (x, y, z)))) for x, z in pts] for y, pts in secs]
    n = len(rings[0])
    for a, b in zip(rings, rings[1:]):
        for i in range(n):
            bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(rings[0])
    bm.faces.new(list(reversed(rings[-1])))
    store(j, bm, slot, 0.0, 1, mask, tilt)


def prism_x(j, slot, pts, x0, x1, bevel=0.0, mask=NONE, tilt=None):
    """A side profile of (z, y) points extruded across from x0 to x1: foot plate, toes, elbow guard."""
    bm = bmesh.new()
    a = [bm.verts.new(B(W(j, (x0, p[1], p[0])))) for p in pts]
    b = [bm.verts.new(B(W(j, (x1, p[1], p[0])))) for p in pts]
    n = len(pts)
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    store(j, bm, slot, bevel, 1, mask, tilt)


def prism_z(j, slot, pts, z0, z1, bevel=0.0, mask=NONE, tilt=None):
    """A front profile of (x, y) points extruded from z0 to z1: the chest plate."""
    bm = bmesh.new()
    a = [bm.verts.new(B(W(j, (p[0], p[1], z0)))) for p in pts]
    b = [bm.verts.new(B(W(j, (p[0], p[1], z1)))) for p in pts]
    n = len(pts)
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    store(j, bm, slot, bevel, 1, mask, tilt)


# --------------------------------------------------------------------------- the robot (Unity numbers, local to each joint)

THIGH_TILT = -17.2  # 0.3 rad: the thigh slopes forward as it goes down (hip (0,0) to knee (0,-0.42,+0.13))
SHIN_TILT = 18.9    # 0.33 rad: the shin slopes back (knee to ankle (0,-0.43,-0.15))


def build_pelvis():
    j = "Pelvis"
    loft(j, JOINT, [(-0.10, rrect(0.30, 0.22, 0.05)), (-0.07, rrect(0.42, 0.30, 0.06)), (0.07, rrect(0.46, 0.32, 0.07)), (0.10, rrect(0.42, 0.30, 0.06))])
    loft(j, BODY, [(0.06, rrect(0.40, 0.345, 0.06)), (0.14, rrect(0.36, 0.33, 0.06))])                 # belt
    prism_x(j, BODY, [(0.10, -0.10), (0.17, -0.085), (0.185, -0.02), (0.17, 0.05), (0.10, 0.06)], -0.085, 0.085, bevel=0.008)  # groin guard
    box(j, BODY, (0.0, 0.10, 0.178), (0.10, 0.06, 0.02), bevel=0.004, mask=ACC)                        # belt buckle
    for u in (-1, 1):
        box(j, BODY, (0.115 * u, -0.115, 0.165), (0.15, 0.17, 0.03), bevel=0.006, tilt=(-12.0, (0.115 * u, -0.115, 0.165)))  # hip tasset
        cyl(j, BODY, (0.2 * u, -0.04, 0.0), 'x', 0.085, 0.1, segs=12, mask=STEEL)                       # hip actuator
        cyl(j, JOINT, (0.26 * u, -0.04, 0.0), 'x', 0.06, 0.02, segs=10)


def build_spine():
    j = "Spine"
    for i in range(3):
        y0, y1, w = 0.02 + 0.1 * i - 0.0425, 0.02 + 0.1 * i + 0.0425, 0.34 - 0.02 * i
        slot = JOINT if i % 2 == 1 else BODY  # three stacked rings, alternating paint and dark joint
        loft(j, slot, [(y0, rrect(w - 0.02, 0.24, 0.04, cs=1)), (y0 + 0.012, rrect(w, 0.26, 0.045, cs=1)),
                       (y1 - 0.012, rrect(w, 0.26, 0.045, cs=1)), (y1, rrect(w - 0.02, 0.24, 0.04, cs=1))])
    cyl(j, JOINT, (0.0, 0.15, 0.0), 'y', 0.085, 0.36, segs=10)
    for u in (-1, 1):
        cyl(j, BODY, (0.12 * u, 0.15, -0.1), 'y', 0.015, 0.30, segs=6, mask=STEEL)


def build_chest():
    j = "Chest"
    # a black tactical vest (dark joint metal) with white clavicle plates, bolts and a white back frame
    loft(j, JOINT, [(-0.08, rrect(0.38, 0.30, 0.06)), (0.0, rrect(0.50, 0.35, 0.07)), (0.14, rrect(0.62, 0.40, 0.08)),
                    (0.30, rrect(0.60, 0.38, 0.08)), (0.36, rrect(0.44, 0.30, 0.07))])
    prism_z(j, JOINT, [(-0.19, 0.12), (0.19, 0.12), (0.25, 0.19), (0.23, 0.31), (-0.23, 0.31), (-0.25, 0.19)], 0.19, 0.232, bevel=0.008)
    for u in (-1, 1):
        box(j, BODY, (0.17 * u, 0.22, 0.24), (0.055, 0.18, 0.016), mask=HAZ)                            # two vertical hazard bars
        box(j, BODY, (0.14 * u, 0.325, 0.07), (0.20, 0.06, 0.20), bevel=0.014, seg=2, tilt=(14.0, (0.14 * u, 0.325, 0.07)))  # white collar plate
        cyl(j, BODY, (0.2 * u, 0.265, 0.238), 'z', 0.018, 0.012, segs=8)                                # plate bolt
    box(j, BODY, (0.0, -0.03, 0.178), (0.34, 0.045, 0.016), mask=HAZ)                                   # the horizontal one
    for k in range(5):                                                                                   # vent grille
        box(j, BODY, (0.0, 0.0 + 0.018 * k + 0.01, 0.205), (0.22, 0.011, 0.02), tilt=(-20.0, (0.0, 0.0 + 0.018 * k + 0.01, 0.205)))
    cyl(j, JOINT, (0.0, 0.345, 0.0), 'y', 0.10, 0.035, segs=12)                                         # collar and neck
    cyl(j, JOINT, (0.0, 0.39, 0.0), 'y', 0.07, 0.13, segs=10)
    box(j, JOINT, (0.0, 0.14, -0.27), (0.42, 0.38, 0.18), bevel=0.012)                                  # back power pack
    box(j, BODY, (0.0, 0.34, -0.27), (0.40, 0.03, 0.17), bevel=0.006)
    for k in range(3):
        box(j, CELLS, (-0.12 + 0.12 * k, 0.14, -0.365), (0.06, 0.24, 0.02))                             # the three glowing cells
    box(j, BODY, (0.0, -0.065, -0.27), (0.44, 0.03, 0.20), bevel=0.006)                                 # pack frame: base and side struts
    for u in (-1, 1):
        box(j, BODY, (0.225 * u, 0.14, -0.27), (0.03, 0.34, 0.20), bevel=0.006)
    cyl(j, BODY, (0.0, 0.42, -0.22), 'y', 0.05, 0.18, segs=8, mask=STEEL)                               # exhaust stub
    cyl(j, JOINT, (0.0, 0.50, -0.22), 'y', 0.062, 0.02, segs=8)


def build_head():
    j = "Head"
    # a smooth egg-shaped white helmet with one big black visor, the red eye slit inside it, white ear caps and the antenna
    loft(j, BODY, [(-0.05, rrect(0.17, 0.19, 0.05, cs=3)), (-0.02, rrect(0.24, 0.25, 0.08, cs=3)), (0.03, rrect(0.27, 0.275, 0.095, cs=3)),
                   (0.09, rrect(0.275, 0.28, 0.10, cs=3)), (0.14, rrect(0.24, 0.25, 0.09, cs=3)), (0.18, rrect(0.15, 0.17, 0.06, cs=3))])
    box(j, JOINT, (0.0, 0.05, 0.118), (0.235, 0.115, 0.06), bevel=0.022, seg=2)                         # the black visor
    box(j, EYE, (0.0, 0.055, 0.147), (0.15, 0.016, 0.014))                                              # red eye slit
    for u in (-1, 1):
        cyl(j, BODY, (0.147 * u, 0.04, -0.02), 'x', 0.04, 0.03, segs=10)                                # ear caps
    cyl(j, JOINT, (0.09, 0.17, -0.06), 'y', 0.016, 0.03, segs=6)                                        # antenna
    cyl(j, BODY, (0.09, 0.30, -0.06), 'y', 0.008, 0.26, segs=6, mask=STEEL)
    sphere(j, EYE, (0.09, 0.43, -0.06), 0.018, u=8, v=4)


def build_scanner():
    sphere("ScannerDot", EYE, (0.0, 0.0, 0.0), 0.016, u=8, v=4)


GUN_OFFSET = 0.03  # the carbine's grip centre sits this far below the wrist, so the cuff of the forearm does not cover the palm


def build_gun_hand(hand):
    """A compact white carbine in a black glove, like the owner's reference photo. The barrel runs down the hand's -Y axis (the
    direction the arm points when it hangs), so when ProceduralRobotAnimator raises the arm by -1.5 rad the gun points forward.
    The muzzle brake ends exactly 0.33 m below the wrist: that is MuzzlePoint (RobotRig.MuzzleOffset), so shots leave the barrel.
    Gun frame (right, up, forward) -> hand-local: x = right, y = -(forward + GUN_OFFSET), z = up (the top of the gun is the hand's +Z)."""
    def G(gx, gu, gf):
        return (gx, -(gf + GUN_OFFSET), gu)

    def gbox(slot, c, size, bevel=0.0, seg=1, tilt=None, mask=NONE):
        pivot = G(*c)
        box(hand, slot, pivot, (size[0], size[2], size[1]), bevel=bevel, seg=seg, mask=mask, tilt=None if tilt is None else (tilt, pivot))

    def gprism(slot, pts, x0, x1, bevel=0.0):  # a side profile of (forward, up) points
        prism_x(hand, slot, [(u, -(f + GUN_OFFSET)) for f, u in pts], x0, x1, bevel=bevel)

    gbox(BODY, (0.0, 0.065, 0.04), (0.05, 0.06, 0.22), bevel=0.008, seg=2)                   # upper receiver, white
    gbox(BODY, (0.0, 0.02, 0.05), (0.046, 0.04, 0.16), bevel=0.006)                          # lower body
    for sgn in (-1, 1):
        gbox(JOINT, (0.026 * sgn, 0.065, 0.05), (0.004, 0.03, 0.12))                         # black side panels
    gprism(JOINT, [(-0.035, 0.04), (0.02, 0.04), (0.0, -0.11), (-0.05, -0.11)], -0.017, 0.017, bevel=0.004)  # pistol grip
    gbox(JOINT, (0.0, -0.03, 0.085), (0.034, 0.11, 0.04), bevel=0.004, tilt=-6.0)            # magazine
    loft(hand, BODY, [(-(0.13 + GUN_OFFSET), rrect(0.05, 0.06, 0.016, cs=1, cz=0.065)),
                      (-(0.27 + GUN_OFFSET), rrect(0.04, 0.05, 0.014, cs=1, cz=0.065))])    # handguard
    cyl(hand, JOINT, (0.0, -(0.285 + GUN_OFFSET), 0.065), 'y', 0.016, 0.03, segs=8)          # muzzle brake: its tip is MuzzlePoint
    gbox(JOINT, (0.0, 0.1, 0.05), (0.026, 0.012, 0.26))                                      # top rail
    gbox(JOINT, (0.0, 0.122, 0.0), (0.032, 0.034, 0.06), bevel=0.006)                        # optic
    gbox(JOINT, (0.0, 0.112, 0.17), (0.008, 0.02, 0.012))                                    # front sight
    gbox(JOINT, (0.0, 0.065, -0.077), (0.044, 0.065, 0.014))                                 # butt pad
    # the black glove: palm behind the grip, four fingers wrapped round the front of it
    gbox(JOINT, (0.0, -0.045, -0.05), (0.05, 0.07, 0.022), bevel=0.005)
    for gu in (-0.015, -0.04, -0.065, -0.09):
        gbox(JOINT, (0.0, gu, 0.03), (0.05, 0.02, 0.02))


def build_arm(u):
    s = "L" if u < 0 else "R"
    up, fore, hand = f"UpperArm_{s}", f"ForeArm_{s}", f"Hand_{s}"
    # shoulder: a big round white pauldron, black ball joint, white sleeve with its piston and an outer armour plate
    ellipsoid(up, BODY, (0.05 * u, 0.02, 0.0), (0.115, 0.085, 0.13), u=16, v=8)
    sphere(up, JOINT, (0.0, 0.0, 0.0), 0.075, u=8, v=4)
    cyl(up, BODY, (0.0, -0.17, 0.0), 'y', 0.062, 0.30, segs=10, r2=0.054)
    box(up, BODY, (0.07 * u, -0.15, 0.0), (0.035, 0.2, 0.1), bevel=0.007)
    cyl(up, JOINT, (0.0, -0.31, 0.0), 'y', 0.066, 0.03, segs=10)
    cyl(up, JOINT, (0.0, -0.12, 0.075), 'y', 0.02, 0.18, segs=8)
    cyl(up, BODY, (0.0, -0.26, 0.075), 'y', 0.011, 0.16, segs=6, mask=STEEL)
    cyl(fore, JOINT, (0.0, 0.0, 0.0), 'x', 0.07, 0.12, segs=12)                                         # black elbow joint
    loft(fore, BODY, [(-0.04, rrect(0.12, 0.12, 0.03, cs=1)), (-0.10, rrect(0.145, 0.15, 0.035, cs=1)),
                      (-0.26, rrect(0.12, 0.12, 0.03, cs=1)), (-0.31, rrect(0.10, 0.10, 0.025, cs=1))])
    prism_x(fore, BODY, [(-0.06, 0.03), (-0.11, -0.02), (-0.11, -0.09), (-0.07, -0.13), (-0.06, -0.05)], -0.045, 0.045, bevel=0.006)
    cyl(fore, JOINT, (0.0, -0.30, 0.0), 'y', 0.062, 0.035, segs=10)
    if u < 0:   # left hand: a black claw
        box(hand, JOINT, (0.0, -0.03, 0.0), (0.10, 0.06, 0.09), bevel=0.008)                             # palm
        for f in (-1, 0, 1):
            box(hand, JOINT, (f * 0.035, -0.08, 0.025), (0.026, 0.06, 0.026), tilt=(17.2, (f * 0.035, -0.08, 0.025)))
            box(hand, JOINT, (f * 0.035, -0.13, 0.045), (0.02, 0.05, 0.02), tilt=(40.0, (f * 0.035, -0.13, 0.045)))
        box(hand, JOINT, (0.06, -0.07, 0.0), (0.03, 0.07, 0.03), tilt=(-10.0, (0.06, -0.07, 0.0)))     # thumb
    else:       # right hand: holds the carbine
        build_gun_hand(hand)


def build_leg(u):
    s = "L" if u < 0 else "R"
    thigh, shin, foot = f"Thigh_{s}", f"Shin_{s}", f"Foot_{s}"
    tc = (0.0, -0.2, 0.06)
    sphere(thigh, JOINT, (0.0, 0.0, 0.0), 0.09, u=8, v=4)
    loft(thigh, BODY, [(-0.2 + 0.2, rrect(0.16, 0.18, 0.06, cs=3, cz=0.06)), (-0.2 + 0.12, rrect(0.19, 0.22, 0.07, cs=3, cz=0.06)),
                       (-0.2 - 0.10, rrect(0.18, 0.21, 0.065, cs=3, cz=0.06)), (-0.2 - 0.20, rrect(0.13, 0.16, 0.05, cs=3, cz=0.06))],
         tilt=(THIGH_TILT, tc))
    box(thigh, BODY, (0.095 * u, -0.18, 0.06), (0.03, 0.28, 0.14), bevel=0.008, tilt=(THIGH_TILT, tc))
    cyl(thigh, JOINT, (0.0, -0.2, -0.1), 'y', 0.028, 0.24, segs=8, tilt=(8.6, (0.0, -0.2, -0.1)))      # knee piston housing
    sc = (0.0, -0.2, -0.07)
    cyl(shin, JOINT, (0.0, 0.0, 0.0), 'x', 0.08, 0.18, segs=12)                                         # black knee joint
    box(shin, BODY, (0.0, 0.0, 0.08), (0.13, 0.11, 0.06), bevel=0.016, seg=2)                           # knee cap
    box(shin, BODY, (0.105 * u, 0.0, 0.01), (0.035, 0.12, 0.10), bevel=0.01)                     # outer knee plate
    loft(shin, JOINT, [(-0.2 + 0.21, rrect(0.12, 0.13, 0.03, cs=1, cz=-0.07)), (-0.2, rrect(0.13, 0.14, 0.035, cs=1, cz=-0.07)),
                       (-0.2 - 0.21, rrect(0.085, 0.09, 0.025, cs=1, cz=-0.07))], tilt=(SHIN_TILT, sc))
    box(shin, BODY, (0.0, -0.15, 0.005), (0.15, 0.26, 0.085), bevel=0.014, seg=2, tilt=(SHIN_TILT, (0.0, -0.15, 0.005)))      # greave
    box(shin, BODY, (0.0, -0.14, -0.13), (0.12, 0.20, 0.05), bevel=0.008, tilt=(SHIN_TILT, (0.0, -0.14, -0.13)))     # calf guard
    cyl(shin, BODY, (0.0, -0.12, -0.16), 'y', 0.014, 0.28, segs=6, mask=STEEL, tilt=(23.0, (0.0, -0.12, -0.16)))
    cyl(foot, JOINT, (0.0, 0.0, 0.0), 'x', 0.055, 0.14, segs=10)                                        # ankle pin
    box(foot, BODY, (0.0, -0.035, 0.015), (0.13, 0.09, 0.14), bevel=0.012)                       # ankle guard
    prism_x(foot, BODY, [(-0.065, -0.06), (0.19, -0.07), (0.20, -0.115), (0.15, -0.12), (-0.05, -0.12), (-0.075, -0.10)], -0.09, 0.09, bevel=0.006)
    for t in (-1, 1):
        prism_x(foot, JOINT, [(0.12, -0.085), (0.20, -0.088), (0.28, -0.115), (0.275, -0.12), (0.12, -0.12)], 0.05 * t - 0.025, 0.05 * t + 0.025)  # toes
    box(foot, JOINT, (0.0, -0.1, -0.1), (0.05, 0.04, 0.1), tilt=(-14.0, (0.0, -0.1, -0.1)))             # heel spur


def build_all():
    build_pelvis()
    build_spine()
    build_chest()
    build_head()
    build_scanner()
    for u in (-1, 1):
        build_arm(u)
        build_leg(u)


# --------------------------------------------------------------------------- merge, clean up, unwrap


def merge():
    bm = bmesh.new()
    mask_layer = bm.loops.layers.float_color.new("Mask")
    jid = bm.faces.layers.int.new("jid")
    skipped = 0
    for joint, verts, faces, slot, mask in PARTS:
        vs = [bm.verts.new(v) for v in verts]
        for idx in faces:
            try:
                f = bm.faces.new([vs[i] for i in idx])
            except ValueError:
                skipped += 1
                continue
            f.material_index = slot
            f[jid] = JOINT_ID[joint]
            for loop in f.loops:
                loop[mask_layer] = mask
    return bm, skipped


def hidden_faces(bm, samples=14):
    """Faces nobody can see even when the joint moves: every ray over the face's hemisphere hits another face OF THE SAME JOINT
    (parts of other joints move relative to it, so they never count as cover)."""
    from mathutils.bvhtree import BVHTree
    bm.normal_update()
    bm.verts.index_update()  # the cover test below keys its polygons by vertex index
    jid = bm.faces.layers.int["jid"]
    by_joint = {}
    for f in bm.faces:
        by_joint.setdefault(f[jid], []).append(f)
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local = []
    for i in range(samples):
        elev = math.radians(10.0) + (math.pi / 2 - math.radians(10.0)) * (i + 0.5) / samples
        local.append(Vector((math.cos(elev) * math.cos(i * golden), math.cos(elev) * math.sin(i * golden), math.sin(elev))))
    local.reverse()
    hidden = []
    for faces in by_joint.values():
        coords, polys, remap = [], [], {}
        for f in faces:
            poly = []
            for v in f.verts:
                if v.index not in remap:
                    remap[v.index] = len(coords)
                    coords.append(v.co.copy())
                poly.append(remap[v.index])
            polys.append(poly)
        tree = BVHTree.FromPolygons(coords, polys)
        for f in faces:
            n = f.normal
            if n.length < 0.5:
                continue
            center = f.calc_center_median()
            step = max(1, len(f.verts) // 6)
            points = [center] + [center + (v.co - center) * 0.8 for v in list(f.verts)[::step]]
            rot = n.to_track_quat('Z', 'Y')
            rays = [rot @ d for d in local]
            covered = True
            for point in points:
                origin = point + n * 0.0015
                if any(tree.ray_cast(origin, ray)[0] is None for ray in rays):
                    covered = False
                    break
            if covered:
                hidden.append(f)
    return hidden


def cleanup(bm):
    removed = hidden_faces(bm)
    bmesh.ops.delete(bm, geom=removed, context='FACES')
    # triangles only: Unity then never has to split a twisted quad ("polygon ... self-intersecting and has been discarded")
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    sliver = [f for f in bm.faces if f.calc_area() < 1e-9]
    if sliver:
        bmesh.ops.delete(bm, geom=sliver, context='FACES')
    bm.edges.ensure_lookup_table()
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(40.0))
    for f in bm.faces:
        f.smooth = True
    return len(removed)


def make_material(name):
    colour, metallic, rough, emission = PALETTE[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = 3.0
    return mat


def unwrap(ob):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.0, scale_to_bounds=False)
    bpy.ops.uv.average_islands_scale()
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
    bpy.ops.object.mode_set(mode='OBJECT')


def split_into_hierarchy(merged, mats):
    """One mesh object per joint, origin at the joint, in the rotated root frame described at the top of this file."""
    # The FBX needs ONE top-level Empty: Unity merges a lone top-level mesh node into the model root, which would leave no object
    # called Pelvis (and put the whole robot 1.05 m up). The Empty becomes the FBX root itself and Pelvis its first child.
    root = bpy.data.objects.new("SecurityRobot", None)
    root.empty_display_type = 'PLAIN_AXES'
    root.rotation_euler = (math.radians(90.0), 0.0, 0.0)  # cancels the FBX exporter's own Z-up to Y-up rotation
    bpy.context.scene.collection.objects.link(root)
    objs = {}
    for name, parent, off in JOINTS:
        bm = bmesh.new()
        bm.from_mesh(merged.data)
        jid = bm.faces.layers.int["jid"]
        drop = [f for f in bm.faces if f[jid] != JOINT_ID[name]]
        bmesh.ops.delete(bm, geom=drop, context='FACES')
        origin = WORLD[name]
        for v in bm.verts:
            u = Vector((v.co.x, v.co.z, v.co.y)) - origin      # Blender world -> Unity, relative to the joint
            v.co = Vector((u.x, u.y, -u.z))                    # the root's rotated frame: z mirrored
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
    return objs


def unity_bounds(objs):
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for ob in objs.values():
        for v in ob.data.vertices:
            p = ob.matrix_world @ v.co
            u = Vector((p.x, p.z, p.y))
            for i in range(3):
                lo[i] = min(lo[i], u[i])
                hi[i] = max(hi[i], u[i])
    return lo, hi


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    mats = [make_material(n) for n in SLOTS]

    build_all()
    bm, skipped = merge()
    removed = cleanup(bm)
    me = bpy.data.meshes.new("RobotAll")
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    merged = bpy.data.objects.new("RobotAll", me)
    bpy.context.scene.collection.objects.link(merged)
    unwrap(merged)

    objs = split_into_hierarchy(merged, mats)
    bpy.data.objects.remove(merged)
    bpy.data.meshes.remove(me)

    total = 0
    for name, _, _ in JOINTS:
        o = objs[name]
        tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
        total += tris
        print(f"[Robot] {name:11s} {tris:5d} triangles  materials {[m.name[len('M_SecurityRobot_'):] for m in o.data.materials]}")
    bpy.context.view_layer.update()
    lo, hi = unity_bounds(objs)
    size = hi - lo
    print("=" * 70)
    print(f"[Robot] total {total} triangles (target 5000, max 7500); parts {len(PARTS)}; hidden faces removed {removed}; skipped faces {skipped}")
    print(f"[Robot] size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   target {TARGET_SIZE[0]} x {TARGET_SIZE[1]} x {TARGET_SIZE[2]}")
    print(f"[Robot] x {lo.x:.3f} .. {hi.x:.3f}   y {lo.y:.3f} .. {hi.y:.3f}   z {lo.z:.3f} .. {hi.z:.3f}   (spec z -0.38 .. 0.26)")

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print(f"[Robot] saved {BLEND_PATH}")


if __name__ == "__main__":
    main()
