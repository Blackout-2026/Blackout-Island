"""Builds the realistic prop models SM_Prop_<Name>.fbx (step C7). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_props.py -- [Name ...] [--preview <folder>]

With no names it builds every prop below. Each prop is modelled the way the real object is made (a locker has a door with
louvre slots, hinges and a recessed handle; a barrel has rolling hoops and chimes), in the exact size, pivot and facing the
catalog asks for (Assets/Editor/CoreDeveloper/PropCatalog.cs, docs/art-pipeline.md section 5):
    x = width, y = up, z = depth, the FRONT faces +Z, and the pivot is the base centre, the centre, or the wall face.
The script checks each model's bounds against the catalog size (tolerance 0.05 m, PivotRules) and prints a warning.

Materials are the palette M_Prop_* names (Assets/Editor/CoreDeveloper/PropMaterialBuilder.cs), at most four per prop. UVs are a
cube projection at one UV unit per metre, so a tiling detail texture has the same scale on every prop.
Frame and helpers are the ones of build_smg.py (Unity-frame numbers, B() converts to Blender).
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402  (the kit: box, cyl, prism, tube, bool_cut, create_object)

REPO = S.REPO
OUT_DIR = os.path.join(REPO, "Assets", "Models", "Custom")
B = S.B
OBJ = "M"

# slot order = index used by the kit; (hex, metallic, roughness) for the Blender preview, copied from PropMaterialBuilder
PALETTE = [
    ("MetalDark", "#2b2e31", 0.7, 0.45), ("Steel", "#9aa0a4", 0.9, 0.3), ("Chrome", "#d9dde0", 1.0, 0.12),
    ("Olive", "#6a7350", 0.4, 0.7), ("Locker", "#51606b", 0.5, 0.6), ("LabWhite", "#dfe5e6", 0.05, 0.35),
    ("PlasticDark", "#23272b", 0.0, 0.55), ("DeskTop", "#3c4146", 0.1, 0.6), ("Wood", "#7a6a5a", 0.0, 0.85),
    ("MedWhite", "#e8e8e2", 0.0, 0.5), ("MedRed", "#c41e1e", 0.0, 0.5), ("Glass", "#9fc9d0", 0.2, 0.06),
    ("GlowCyan", "#47d1d6", 0.0, 0.3), ("GlowGreen", "#40ff60", 0.0, 0.3), ("GlowScreen", "#1a8fa8", 0.0, 0.6),
    ("CureVial", "#47e0e6", 0.0, 0.3),
]
MAT = {name: i for i, (name, *_rest) in enumerate(PALETTE)}


def lin(hex_color):
    h = hex_color.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


# --------------------------------------------------------------------------- short helpers (Unity frame)

def bx(mat, c, s, bevel=0.0, seg=1, tilt=None):
    S.box(OBJ, MAT[mat], c, s, bevel=bevel, seg=seg, tilt=tilt)


def cyl_y(mat, c, r, h, segs=16, r2=None, bevel=0.0):
    S.cyl(OBJ, MAT[mat], c, 'y', r, h, segs=segs, r2=r2, bevel=bevel)


def cyl_z(mat, c, r, h, segs=16, r2=None, bevel=0.0):
    S.cyl(OBJ, MAT[mat], c, 'z', r, h, segs=segs, r2=r2, bevel=bevel)


def cyl_x(mat, c, r, h, segs=16, r2=None, bevel=0.0):
    S.cyl(OBJ, MAT[mat], c, 'x', r, h, segs=segs, r2=r2, bevel=bevel)


def yaw_last(deg, centre):
    S._xf_last(OBJ, S.unity_euler(0.0, deg, 0.0), centre)


def roll_last(deg, centre):
    S._xf_last(OBJ, S.unity_euler(0.0, 0.0, deg), centre)


def pitch_last(deg, centre):
    S._xf_last(OBJ, S.unity_euler(deg, 0.0, 0.0), centre)


def prism_side(mat, pts_zy, half_x, bevel=0.0, seg=1, cx=0.0):
    """A side profile (z, y) extruded across x."""
    S.prism(OBJ, MAT[mat], pts_zy, cx - half_x, cx + half_x, bevel=bevel, seg=seg)


def prism_front_mat(mat, pts_xy, z0, z1, bevel=0.0):
    bm = bmesh.new()
    a = [bm.verts.new(B((p[0], p[1], z0))) for p in pts_xy]
    b = [bm.verts.new(B((p[0], p[1], z1))) for p in pts_xy]
    n = len(pts_xy)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((a[i], a[j], b[j], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    S._store(OBJ, bm, MAT[mat], bevel)


def cut(mat, base_bm, cutters, bevel=0.0):
    S.bool_cut(OBJ, MAT[mat], base_bm, cutters, bevel=bevel)


# --------------------------------------------------------------------------- the props

def medkit():
    """A moulded first-aid case: base shell and lid with a parting line, two latches on each long side, a folding handle,
    rubber feet and a red cross on the front and back."""
    w, d = 0.42, 0.27
    bx("MedWhite", (0, 0.095, 0), (w, 0.17, d), bevel=0.014, seg=2)  # base shell
    bx("PlasticDark", (0, 0.181, 0), (w - 0.006, 0.006, d - 0.006))  # the parting line
    bx("MedWhite", (0, 0.2265, 0), (w - 0.006, 0.085, d - 0.006), bevel=0.014, seg=2)  # lid
    bx("MedWhite", (0, 0.271, 0), (w - 0.1, 0.006, d - 0.08), bevel=0.003)  # raised panel on the lid
    for sx in (-1, 1):
        for sz in (-1, 1):
            bx("PlasticDark", (sx * 0.17, 0.005, sz * 0.095), (0.05, 0.01, 0.05), bevel=0.003)  # rubber feet
    for sz in (1, -1):
        for sx in (-1, 1):
            bx("Steel", (sx * 0.12, 0.181, sz * 0.1385), (0.045, 0.06, 0.008), bevel=0.0015)  # latch plate
            bx("PlasticDark", (sx * 0.12, 0.181, sz * 0.1445), (0.03, 0.026, 0.006), bevel=0.001)  # latch hook
        bx("MedRed", (0, 0.105, sz * 0.1375), (0.04, 0.12, 0.0036))  # red cross (the bars differ in depth, so they do not z-fight)
        bx("MedRed", (0, 0.105, sz * 0.1375), (0.12, 0.04, 0.0042))
    for sx in (-1, 1):
        bx("PlasticDark", (sx * 0.075, 0.292, 0), (0.026, 0.05, 0.04), bevel=0.004)  # handle brackets
    bx("PlasticDark", (0, 0.322, 0), (0.18, 0.02, 0.034), bevel=0.006, seg=2)  # handle bar


def cure_case():
    """An aluminium transport case with a clear cover on four posts, two clasps, and three capped vials in a foam tray."""
    bx("Steel", (0, 0.035, 0), (0.42, 0.07, 0.24), bevel=0.008, seg=2)  # base
    bx("PlasticDark", (0, 0.0775, 0), (0.386, 0.015, 0.206), bevel=0.003)  # foam tray
    for k in range(3):
        x = -0.1 + 0.1 * k
        cyl_y("PlasticDark", (x, 0.095, 0), 0.026, 0.02, 12)  # collar the vial sits in
        cyl_y("Glass", (x, 0.172, 0), 0.022, 0.135, 12)  # glass tube
        cyl_y("CureVial", (x, 0.162, 0), 0.0175, 0.115, 12)  # the cure inside
        cyl_y("PlasticDark", (x, 0.2495, 0), 0.0245, 0.016, 12)  # cap
    bx("Glass", (0, 0.165, 0), (0.4, 0.19, 0.22))  # cover
    for sx in (-1, 1):
        for sz in (-1, 1):
            bx("Steel", (sx * 0.2, 0.165, sz * 0.11), (0.014, 0.19, 0.014))  # corner posts
    bx("Steel", (0, 0.27, 0), (0.42, 0.02, 0.24), bevel=0.006, seg=2)  # lid frame
    for sx in (-1, 1):
        bx("Steel", (sx * 0.14, 0.07, 0.124), (0.034, 0.046, 0.012), bevel=0.002)  # clasps
        cyl_x("Steel", (sx * 0.14, 0.07, -0.122), 0.008, 0.05, 8)  # hinge pins


def locker():
    """A steel locker: shell on four feet, a door with louvre slots at the top and bottom, a recessed handle with a lock,
    a number plate, three hinges and a capped top."""
    for sx in (-1, 1):
        for sz in (-1, 1):
            bx("MetalDark", (sx * 0.22, 0.015, sz * 0.22 - 0.015), (0.045, 0.03, 0.045))  # feet
    bx("Locker", (0, 1.005, -0.025), (0.52, 1.95, 0.5), bevel=0.004)  # shell
    bx("Locker", (0, 1.99, -0.015), (0.52, 0.02, 0.52), bevel=0.004)  # top cap
    slots = [((0.0, 1.78 + 0.027 * i, 0.2375), (0.3, 0.014, 0.05)) for i in range(5)]
    slots += [((0.0, 0.14 + 0.027 * i, 0.2375), (0.3, 0.014, 0.05)) for i in range(3)]
    cut("Locker", S.box_bm((0, 1.0, 0.2375), (0.5, 1.9, 0.025)), slots, bevel=0.003)  # the door
    bx("PlasticDark", (0, 1.84, 0.2275), (0.32, 0.15, 0.004))  # dark behind the top louvres
    bx("PlasticDark", (0, 0.18, 0.2275), (0.32, 0.08, 0.004))  # ... and the bottom ones
    bx("Steel", (0.17, 1.0, 0.2605), (0.04, 0.13, 0.012), bevel=0.003)  # recessed pull
    cyl_z("Steel", (0.17, 1.14, 0.2525), 0.012, 0.01, 10)  # lock barrel
    bx("Steel", (0.0, 1.7, 0.2515), (0.07, 0.035, 0.003))  # number plate
    for y in (0.4, 1.0, 1.6):
        cyl_y("Steel", (-0.248, y, 0.2475), 0.007, 0.07, 8)  # hinges
    bx("MetalDark", (0, 0.05, 0.2525), (0.5, 0.01, 0.01))  # door bottom lip


def desk(width, height, both_pedestals=False):
    """A steel office desk with a laminate top: a side panel on one end, one or two drawer pedestals, a modesty panel at the
    back, three drawers per pedestal with handles, and a cable grommet."""
    top_y = height - 0.02
    bx("DeskTop", (0, top_y, 0), (width, 0.04, 1.0), bevel=0.008, seg=2)
    bx("MetalDark", (0, height - 0.065, -0.4), (width - 0.1, 0.05, 0.05), bevel=0.003)  # frame rail under the top
    ped_h = height - 0.04 - 0.03

    def pedestal(sign):
        px = sign * (width / 2.0 - 0.3)
        bx("Locker", (px, 0.03 + ped_h / 2.0, 0), (0.5, ped_h, 0.9), bevel=0.004)
        dh = (ped_h - 0.04) / 3.0
        for i in range(3):
            y = 0.05 + dh * (i + 0.5)
            bx("Locker", (px, y, 0.4575), (0.46, dh - 0.012, 0.015), bevel=0.003)  # drawer front
            bx("Steel", (px, y + dh * 0.22, 0.481), (0.2, 0.014, 0.024), bevel=0.003)  # handle
            if i == 2:
                cyl_z("Steel", (px - 0.19, y + dh * 0.18, 0.468), 0.009, 0.008, 8)  # key lock
        for sx in (-1, 1):
            bx("MetalDark", (px + sx * 0.2, 0.015, 0.38), (0.05, 0.03, 0.05))  # feet
            bx("MetalDark", (px + sx * 0.2, 0.015, -0.38), (0.05, 0.03, 0.05))

    pedestal(1)
    if both_pedestals:
        pedestal(-1)
        left_edge = -(width / 2.0 - 0.55)
    else:
        left_edge = -(width / 2.0 - 0.06) + 0.015
        bx("Locker", (-(width / 2.0 - 0.06), (height - 0.045) / 2.0 + 0.0, 0), (0.03, height - 0.045, 0.85), bevel=0.003)
        bx("MetalDark", (-(width / 2.0 - 0.06), 0.012, 0.37), (0.05, 0.024, 0.1))  # side-panel feet
        bx("MetalDark", (-(width / 2.0 - 0.06), 0.012, -0.37), (0.05, 0.024, 0.1))
    right_edge = width / 2.0 - 0.55
    bx("Locker", ((left_edge + right_edge) / 2.0, height - 0.28, -0.42), (right_edge - left_edge, 0.4, 0.02), bevel=0.003)
    cyl_y("MetalDark", (-width / 2.0 + 0.45, height + 0.002, -0.3), 0.03, 0.004, 14)  # cable grommet


def meeting_table():
    """A laminate top on two steel trestles, each with a top rail, two round legs and a foot bar, tied by a stretcher."""
    bx("DeskTop", (0, 0.74, 0), (2.0, 0.04, 1.0), bevel=0.008, seg=2)
    for sx in (-1, 1):
        x = sx * 0.85
        bx("MetalDark", (x, 0.695, 0), (0.05, 0.05, 0.86), bevel=0.004)  # top rail
        for sz in (-1, 1):
            cyl_y("Steel", (x, 0.36, sz * 0.4), 0.03, 0.66, 14)  # legs
        bx("MetalDark", (x, 0.02, 0), (0.06, 0.04, 0.92), bevel=0.006, seg=2)  # foot bar
        for sz in (-1, 1):
            bx("PlasticDark", (x, 0.005, sz * 0.44), (0.07, 0.01, 0.05), bevel=0.002)  # glides
    bx("Steel", (0, 0.25, 0), (1.7, 0.03, 0.04), bevel=0.004)  # stretcher
    bx("MetalDark", (0, 0.66, -0.2), (1.4, 0.04, 0.22), bevel=0.003)  # cable tray under the top


def monitor():
    """A flat-panel monitor: an oval foot, a hinged neck, a thin panel with a glowing screen, a back housing and a power LED."""
    bx("PlasticDark", (0, 0.01, 0), (0.3, 0.02, 0.2), bevel=0.008, seg=2)  # foot
    prism_side("MetalDark", [(-0.065, 0.02), (-0.02, 0.02), (-0.03, 0.3), (-0.075, 0.3)], 0.02, bevel=0.003)  # neck
    cyl_x("MetalDark", (0, 0.3, -0.058), 0.022, 0.08, 12)  # hinge
    bx("PlasticDark", (0, 0.52, -0.03), (0.7, 0.44, 0.025), bevel=0.008, seg=2)  # panel
    bx("PlasticDark", (0, 0.5, -0.075), (0.4, 0.3, 0.06), bevel=0.012, seg=2)  # back housing
    bx("GlowScreen", (0, 0.525, -0.0165), (0.66, 0.39, 0.003))  # screen
    bx("GlowGreen", (0.3, 0.31, -0.0175), (0.008, 0.008, 0.002))  # power LED


def wall_monitor():
    """A wall-mounted flat screen: a bracket on the wall, a thin panel with a glowing screen, a rear housing, a power LED."""
    bx("MetalDark", (0, 0, 0.015), (0.4, 0.25, 0.03), bevel=0.004)  # wall bracket
    bx("PlasticDark", (0, 0, 0.05), (1.16, 0.68, 0.04), bevel=0.01, seg=2)  # panel
    bx("PlasticDark", (0, 0, 0.033), (0.7, 0.4, 0.012), bevel=0.003)  # rear housing step
    bx("GlowScreen", (0, 0.01, 0.0705), (1.10, 0.6, 0.003))  # screen
    bx("GlowGreen", (0.5, -0.315, 0.0705), (0.01, 0.01, 0.002))  # power LED


def office_chair():
    """A swivel chair: a five-star base on casters, a gas lift with a bellows, a seat mechanism, a padded seat, a tilted
    backrest on a post, and two armrests."""
    for i in range(5):
        a = math.radians(72.0 * i)
        dx, dz = math.sin(a), math.cos(a)
        bx("MetalDark", (dx * 0.13, 0.078, dz * 0.13), (0.045, 0.03, 0.27), bevel=0.005, tilt=(0.0, math.degrees(a), 0.0))
        cyl_y("MetalDark", (dx * 0.235, 0.062, dz * 0.235), 0.012, 0.04, 8)  # caster fork
        cyl_x("PlasticDark", (dx * 0.235, 0.03, dz * 0.235), 0.03, 0.022, 10)
        yaw_last(math.degrees(a), (dx * 0.235, 0.03, dz * 0.235))
    cyl_y("MetalDark", (0, 0.1, 0), 0.055, 0.05, 12)  # hub
    cyl_y("Steel", (0, 0.24, 0), 0.025, 0.25, 12)  # gas lift
    cyl_y("PlasticDark", (0, 0.32, 0), 0.036, 0.13, 12, r2=0.03)  # bellows
    bx("MetalDark", (0, 0.425, 0), (0.22, 0.05, 0.22), bevel=0.01, seg=2)  # seat mechanism
    bx("PlasticDark", (0, 0.47, 0.0), (0.46, 0.08, 0.46), bevel=0.02, seg=2)  # seat
    bx("MetalDark", (0, 0.58, -0.185), (0.04, 0.26, 0.03), bevel=0.004)  # back post
    bx("PlasticDark", (0, 0.78, -0.2), (0.44, 0.46, 0.06), bevel=0.02, seg=2, tilt=(-8.0, 0.0, 0.0))  # backrest
    for sx in (-1, 1):
        bx("MetalDark", (sx * 0.215, 0.56, 0.0), (0.025, 0.22, 0.025), bevel=0.003)  # arm posts
        bx("PlasticDark", (sx * 0.215, 0.68, 0.02), (0.055, 0.03, 0.26), bevel=0.012, seg=2)  # arm pads


def crate(size):
    """A boarded wooden crate: corner posts, horizontal boards on all four sides with small gaps and a slightly different
    thickness each, top and bottom slabs, two steel straps with rivets, and a stencil plate. Pivot at the centre."""
    rnd = random.Random(1117)
    h = size / 2.0
    for sx in (-1, 1):
        for sz in (-1, 1):
            bx("Wood", (sx * (h - 0.04), 0, sz * (h - 0.04)), (0.08, size - 0.01, 0.08), bevel=0.004)  # corner posts
    n = max(5, int(round(size / 0.21)))
    bh = (size - 0.02) / n
    for i in range(n):
        y = -h + 0.01 + bh * (i + 0.5)
        for sz in (-1, 1):
            t = 0.022 + rnd.uniform(-0.003, 0.003)
            bx("Wood", (0, y, sz * (h - 0.011 + 0.002)), (size - 0.16, bh - 0.008, t), bevel=0.002)
        for sx in (-1, 1):
            t = 0.022 + rnd.uniform(-0.003, 0.003)
            bx("Wood", (sx * (h - 0.011 + 0.002), y, 0), (t, bh - 0.008, size - 0.16), bevel=0.002)
    bx("Wood", (0, h - 0.015, 0), (size - 0.02, 0.03, size - 0.02), bevel=0.004)  # lid
    bx("Wood", (0, -h + 0.015, 0), (size - 0.02, 0.03, size - 0.02), bevel=0.004)  # base
    sw = size * 0.07
    for sx in (-1, 1):
        x = sx * size * 0.3
        bx("MetalDark", (x, 0, h + 0.002), (sw, size + 0.004, 0.008))  # strap, front
        bx("MetalDark", (x, 0, -h - 0.002), (sw, size + 0.004, 0.008))  # back
        bx("MetalDark", (x, h + 0.002, 0), (sw, 0.008, size + 0.004))  # over the top
        bx("MetalDark", (x, -h - 0.002, 0), (sw, 0.008, size + 0.004))  # under the base
        for yy in (-0.38, 0.38):
            for sz in (-1, 1):
                cyl_z("Steel", (x, yy * size, sz * (h + 0.007)), 0.011, 0.008, 6)  # rivets
    bx("LabWhite", (-size * 0.05, -size * 0.28, h + 0.003), (size * 0.34, size * 0.14, 0.004))  # stencil plate


def barrel():
    """A 200-litre steel drum: a tight body, two rolling hoops, top and bottom chimes, a recessed lid with two bungs and a rim."""
    cyl_y("Olive", (0, 0.45, 0), 0.285, 0.9, 24)
    for y in (0.3, 0.6):
        cyl_y("Olive", (0, y, 0), 0.295, 0.035, 24, bevel=0.003)  # rolling hoops
    cyl_y("Olive", (0, 0.885, 0), 0.3, 0.03, 24)  # top chime
    cyl_y("Olive", (0, 0.015, 0), 0.3, 0.03, 24)  # bottom chime
    cyl_y("MetalDark", (0, 0.887, 0), 0.272, 0.014, 24)  # recessed lid
    cyl_y("Steel", (0.12, 0.897, 0.08), 0.026, 0.012, 10)  # bungs
    cyl_y("Steel", (-0.1, 0.897, -0.12), 0.018, 0.01, 10)
    cyl_y("Steel", (0.12, 0.903, 0.08), 0.016, 0.004, 8)
    bx("MetalDark", (0, 0.45, 0.292), (0.14, 0.08, 0.006))  # label plate


def fuel_tank():
    """A horizontal diesel tank: a welded body with dished heads and seam rings, two steel straps over two saddle cradles, a
    top manway with bolts, a filler cap, a vent, a level gauge, a drain valve and a hazard diamond on the side."""
    cyl_z("Olive", (0, 1.35, 0), 1.0, 3.6, 32)  # shell
    cyl_z("Olive", (0, 1.35, 1.95), 1.0, 0.3, 32, r2=0.35)  # dished heads (cone frustums)
    cyl_z("Olive", (0, 1.35, -1.95), 0.35, 0.3, 32, r2=1.0)
    for z in (-1.8, -0.6, 0.6, 1.8):
        cyl_z("Olive", (0, 1.35, z), 1.012, 0.03, 32)  # weld seams
    for z in (-1.2, 1.2):
        cyl_z("MetalDark", (0, 1.35, z), 1.02, 0.09, 32)  # straps
        prism_front_mat("MetalDark", [(-0.9, 0.0), (0.9, 0.0), (0.9, 0.3), (0.62, 0.58), (-0.62, 0.58), (-0.9, 0.3)], z - 0.13, z + 0.13)
        bx("MetalDark", (0, 0.015, z), (1.9, 0.03, 0.34), bevel=0.004)  # cradle base plate
    cyl_y("MetalDark", (0, 2.364, 0.0), 0.32, 0.03, 24)  # manway flange
    for k in range(12):
        a = math.radians(30.0 * k)
        cyl_y("Steel", (math.sin(a) * 0.27, 2.385, math.cos(a) * 0.27), 0.012, 0.012, 6)  # bolts
    cyl_y("Steel", (0, 2.385, 0), 0.19, 0.01, 18)  # manway cover
    def shell_y(x):
        return 1.35 + math.sqrt(max(0.0, 1.0 - x * x))  # the top of the tank at this x

    cyl_y("MetalDark", (0.55, shell_y(0.55) + 0.05, 1.3), 0.075, 0.15, 14)  # filler neck, sunk into the shell
    cyl_y("Steel", (0.55, shell_y(0.55) + 0.13, 1.3), 0.05, 0.014, 12)  # filler cap
    cyl_y("MetalDark", (-0.5, shell_y(0.5) + 0.06, -1.2), 0.03, 0.15, 10)  # vent pipe
    cyl_z("Steel", (0.12, 1.5, 2.108), 0.09, 0.03, 16)  # level gauge on the flat of the head
    cyl_z("MetalDark", (0.12, 1.5, 2.124), 0.075, 0.004, 16)  # gauge face
    cyl_y("MetalDark", (0.0, 0.28, 1.55), 0.035, 0.14, 10)  # drain pipe under the shell
    cyl_y("Steel", (0.0, 0.2, 1.55), 0.05, 0.012, 12)  # valve handwheel
    bx("MedRed", (1.0035, 1.5, 0.3), (0.004, 0.32, 0.32), tilt=(45.0, 0.0, 0.0))  # hazard diamond


BUILDERS = {
    "Medkit_Visual": ("SM_Prop_Medkit", medkit, "BaseCentre", (0.42, 0.33, 0.28)),
    "CureCase_Visual": ("SM_Prop_CureCase", cure_case, "BaseCentre", (0.42, 0.28, 0.24)),
    "Prop_Locker": ("SM_Prop_Locker", locker, "BaseCentre", (0.52, 2.0, 0.55)),
    "Prop_Desk": ("SM_Prop_Desk", lambda: desk(3.0, 0.78), "BaseCentre", (3.0, 0.78, 1.0)),
    "Prop_DeskGuard": ("SM_Prop_DeskGuard", lambda: desk(1.9, 0.78), "BaseCentre", (1.9, 0.78, 1.0)),
    "Prop_DeskCommander": ("SM_Prop_DeskCommander", lambda: desk(4.4, 0.85, True), "BaseCentre", (4.4, 0.85, 1.0)),
    "Prop_MeetingTable": ("SM_Prop_MeetingTable", meeting_table, "BaseCentre", (2.0, 0.76, 1.0)),
    "Prop_Monitor": ("SM_Prop_Monitor", monitor, "BaseCentre", (0.7, 0.75, 0.2)),
    "Prop_WallMonitor": ("SM_Prop_WallMonitor", wall_monitor, "WallFace", (1.16, 0.68, 0.08)),
    "Prop_OfficeChair": ("SM_Prop_OfficeChair", office_chair, "BaseCentre", (0.5, 1.03, 0.5)),
    "Prop_Crate": ("SM_Prop_Crate", lambda: crate(1.5), "Centre", (1.5, 1.5, 1.5)),
    "Prop_CrateSmall": ("SM_Prop_CrateSmall", lambda: crate(1.0), "Centre", (1.0, 1.0, 1.0)),
    "Prop_Barrel": ("SM_Prop_Barrel", barrel, "BaseCentre", (0.6, 0.9, 0.6)),
    "Prop_FuelTank": ("SM_Prop_FuelTank", fuel_tank, "BaseCentre", (2.0, 2.35, 4.2)),
}


# --------------------------------------------------------------------------- scene, checks, export

def make_materials():
    mats = []
    for name, hexcol, metallic, rough in PALETTE:
        mat = bpy.data.materials.new("M_Prop_" + name)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        bsdf.inputs["Base Color"].default_value = lin(hexcol)
        bsdf.inputs["Metallic"].default_value = metallic
        bsdf.inputs["Roughness"].default_value = rough
        mats.append(mat)
    return mats


def unity_bounds(ob):
    pts = [S._to_unity(v.co) for v in ob.data.vertices]
    lo = Vector((min(p.x for p in pts), min(p.y for p in pts), min(p.z for p in pts)))
    hi = Vector((max(p.x for p in pts), max(p.y for p in pts), max(p.z for p in pts)))
    return lo, hi


def check(name, ob, pivot, size):
    lo, hi = unity_bounds(ob)
    sz = hi - lo
    c = (lo + hi) / 2.0
    problems = []
    if any(abs(sz[i] - size[i]) > 0.05 for i in range(3)):
        problems.append(f"size {sz.x:.3f} x {sz.y:.3f} x {sz.z:.3f}, catalog {size[0]} x {size[1]} x {size[2]}")
    if pivot == "BaseCentre" and (abs(lo.y) > 0.05 or abs(c.x) > 0.05 or abs(c.z) > 0.05):
        problems.append(f"pivot: bottom y {lo.y:.3f}, centre ({c.x:.3f}, {c.z:.3f})")
    if pivot == "Centre" and (abs(c.x) > 0.05 or abs(c.y) > 0.05 or abs(c.z) > 0.05):
        problems.append(f"pivot: centre ({c.x:.3f}, {c.y:.3f}, {c.z:.3f})")
    if pivot == "WallFace" and (abs(lo.z) > 0.05 or abs(c.x) > 0.05 or abs(c.y) > 0.05):
        problems.append(f"pivot: back z {lo.z:.3f}, centre ({c.x:.3f}, {c.y:.3f})")
    print(f"[Props] {name}: {sum(len(p.vertices) - 2 for p in ob.data.polygons)} triangles, "
          f"{len(ob.data.materials)} materials, bounds {sz.x:.2f} x {sz.y:.2f} x {sz.z:.2f}"
          + ("  WARNING: " + "; ".join(problems) if problems else "  ok"))


def export(ob, path):
    for o in bpy.data.objects:
        o.select_set(o == ob)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='EDGE', use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE',
        path_mode='AUTO', embed_textures=False, bake_anim=False, add_leaf_bones=False, use_custom_props=False)


def world_uv(ob):
    """Cube projection at one UV unit per metre, so a tiling detail texture has the same scale on every prop."""
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.cube_project(cube_size=1.0)
    bpy.ops.object.mode_set(mode='OBJECT')


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    preview = None
    if "--preview" in argv:
        i = argv.index("--preview")
        preview = argv[i + 1]
        argv = argv[:i] + argv[i + 2:]
    wanted = [a for a in argv if a in BUILDERS] or list(BUILDERS)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    mats = make_materials()
    built = []
    for key in wanted:
        stem, builder, pivot, size = BUILDERS[key]
        S.OBJECTS.clear()
        builder()
        ob = S.create_object(OBJ, mats, 40.0)
        ob.name = stem
        ob.data.name = stem
        world_uv(ob)
        check(key, ob, pivot, size)
        export(ob, os.path.join(OUT_DIR, stem + ".fbx"))
        built.append((key, ob, size))
    if preview:
        render_sheet(built, preview)


def render_sheet(built, folder):
    """One review picture per prop, from the front left and a little above, each framed on its own bounds."""
    os.makedirs(folder, exist_ok=True)
    sc = bpy.context.scene
    sc.render.engine = 'CYCLES'
    sc.cycles.samples = 24
    sc.cycles.use_denoising = True
    sc.render.resolution_x, sc.render.resolution_y = 640, 480
    sc.view_settings.view_transform = 'AgX'
    world = bpy.data.worlds.new("W")
    sc.world = world
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.5, 0.52, 0.56, 1)
    world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0
    bpy.ops.mesh.primitive_plane_add(size=80, location=(0, 0, -0.0))
    floor = bpy.context.active_object
    fm = bpy.data.materials.new("Floor")
    fm.use_nodes = True
    fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.25, 0.25, 0.26, 1)
    floor.data.materials.append(fm)
    sun = bpy.data.lights.new("Sun", 'SUN')
    sun.energy = 3.0
    so = bpy.data.objects.new("Sun", sun)
    sc.collection.objects.link(so)
    so.rotation_euler = (math.radians(55), 0, math.radians(-40))
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam"))
    sc.collection.objects.link(cam)
    sc.camera = cam
    cam.data.lens = 35
    for key, ob, size in built:
        for other in built:
            other[1].hide_render = other[1] != ob
        lo, hi = unity_bounds(ob)
        centre = S.B((lo + hi) / 2.0)
        # a pivot-centred prop would sit half below the floor; lift the floor to the bottom of the model
        floor.location.z = lo.y - 0.0 if lo.y > -10 else 0.0
        dist = max(size) * 2.1 + 0.3
        az, el = math.radians(-35.0), math.radians(22.0)
        # camera on the FRONT side: Unity +z is Blender +y
        cam.location = centre + Vector((math.sin(az) * dist * math.cos(el), math.cos(az) * dist * math.cos(el), math.sin(el) * dist))
        cam.rotation_euler = (centre - cam.location).to_track_quat('-Z', 'Y').to_euler()
        sc.render.filepath = os.path.join(folder, key + ".png")
        bpy.ops.render.render(write_still=True)
        print(f"[Props] {sc.render.filepath}")


main()
