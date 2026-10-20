"""Builds the realistic Commander: SM_Char_Commander, a female naval officer in white service dress (step C10b). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_commander.py

The look is the owner's reference photo of a naval officer: short light-brown hair, a short-sleeved white shirt with an open collar, gold
striped shoulder boards with a star, a name tag on the right chest and a ribbon rack on the left, a white belt with a gold buckle, white
trousers, black shoes, a watch and the white cap tucked under the left arm. The face, the hair and the insignia are generic: nobody's
likeness. It replaces the old blocky Commander of build_humans.py, which now hands "Commander" over to this script.

What is different from the other two humans (they are rigid boxes with flat colours):
  * smooth organic shapes: every part is a sweep, a shell or a thin plate, shaded with custom normals so the cloth and skin are round
    while the boards, buckle and name tag keep crisp edges (smoothed by angle, then cut per joint WITHOUT breaking the shading);
  * a sculpted head (planar-projected UVs, painted eyes, brows and lips), hair as a cap plus locks, ears, hands with fingers;
  * three textured materials (uniform, skin, hair) from make_commander_textures.py, the layout in commander_atlas.py;
  * the arms are modelled bent, the hands clasped in front of the belt like the photo, with every joint still at rotation 0.

Rig: unchanged from the other humans (docs/art-pipeline.md section 18): 18 joint objects under one Empty root rotated +90 degrees about
X, the joint offsets of build_humans.JOINTS, 1.87 m tall, facing +Z, MuzzlePoint under Gun. She aims a pistol with both hands in front of the chest (the Gun socket stays where the Agent Controller expects it, and
MuzzlePoint is the pistol's muzzle, 0.22 m ahead of it); the white cap is on her head.

Numbers below are UNITY metres (x right, y up, z forward) unless a function says Blender; bl() converts. Left = -X.
Budget: 14,000 triangles target (hero character, ModelBudget "SM_Char_Commander"), 3 materials.
"""

import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import commander_atlas as A  # noqa: E402
import build_humans as H  # noqa: E402  (the joint table, the Empty-root trick; importing builds nothing)

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
BLEND = os.path.join(REPO, "Art", "Blender", "SM_Char_Commander.blend")
FBX = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Char_Commander.fbx")

UNIFORM, SKIN, HAIR = 0, 1, 2
MATERIALS = [("M_Char_Commander_Uniform", "uniform", "T_CharCommanderUniform_"),
             ("M_Char_Commander_Skin", "skin", "T_CharCommanderSkin_"),
             ("M_Char_Commander_Hair", "hair", "T_CharCommanderHair_")]
SMOOTH_ANGLE = math.radians(38.0)

# ----------------------------------------------------------------------------- joints
JOINTS = H.JOINTS
WORLD = {}
for _name, _parent, _off in JOINTS:
    WORLD[_name] = Vector(_off) + (WORLD[_parent] if _parent else Vector((0.0, 0.0, 0.0)))


def V(x, y, z):
    return Vector((x, y, z))


def bl(p):
    """Unity metres -> Blender world (the measured mapping Unity = Blender (x, z, y))."""
    return Vector((p[0], p[2], p[1]))


# ----------------------------------------------------------------------------- small maths
def sgnpow(x, e):
    return math.copysign(abs(x) ** e, x)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def gauss(x, c, w):
    return math.exp(-((x - c) / w) ** 2)


def lerp(a, b, t):
    return a + (b - a) * t


def interp(keys, x):
    """Cardinal spline through keys [(x, v0, v1, ...)] with ascending x; clamped at the ends."""
    if x <= keys[0][0]:
        return tuple(keys[0][1:])
    if x >= keys[-1][0]:
        return tuple(keys[-1][1:])
    i = 0
    while keys[i + 1][0] < x:
        i += 1
    x0, x1 = keys[i][0], keys[i + 1][0]
    t = (x - x0) / (x1 - x0)
    out = []
    for c in range(1, len(keys[0])):
        p0, p1 = keys[i][c], keys[i + 1][c]
        pm = keys[i - 1][c] if i > 0 else p0
        pn = keys[i + 2][c] if i + 2 < len(keys) else p1
        xm = keys[i - 1][0] if i > 0 else x0 - (x1 - x0)
        xn = keys[i + 2][0] if i + 2 < len(keys) else x1 + (x1 - x0)
        m0 = 0.5 * ((p1 - p0) / (x1 - x0) + (p0 - pm) / (x0 - xm)) * (x1 - x0)
        m1 = 0.5 * ((pn - p1) / (xn - x1) + (p1 - p0) / (x1 - x0)) * (x1 - x0)
        t2, t3 = t * t, t * t * t
        out.append((2 * t3 - 3 * t2 + 1) * p0 + (t3 - 2 * t2 + t) * m0 + (-2 * t3 + 3 * t2) * p1 + (t3 - t2) * m1)
    return tuple(out)


def catmull(points, per_segment=4):
    """Dense Catmull-Rom path through control points."""
    out = []
    n = len(points)
    for i in range(n - 1):
        p0 = points[max(i - 1, 0)]
        p1, p2 = points[i], points[i + 1]
        p3 = points[min(i + 2, n - 1)]
        for s in range(per_segment):
            t = s / per_segment
            t2, t3 = t * t, t * t * t
            out.append(0.5 * ((2 * p1) + (-p0 + p2) * t + (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2 + (-p0 + 3 * p1 - 3 * p2 + p3) * t3))
    out.append(points[-1].copy())
    return out


# ----------------------------------------------------------------------------- UV makers
def swatch(name):
    uv = A.swatch_uv(name)
    return lambda p, n, a, b: uv


def head_planar():
    return lambda p, n, a, b: A.head_uv(p.x, p.y)


def skin_swatch():
    uv = A.rect_uv(A.SKIN_SWATCH, A.SIZE["skin"], 0.5, 0.5)
    return lambda p, n, a, b: uv


def skin_tile(centre, scale=0.22, front=True):
    """Freckled skin by a planar projection around a part's centre (the tile is 352 px for `scale` metres)."""
    def f(p, n, a, b):
        fx = 0.5 + (p.x - centre.x) / scale
        fy = 0.5 - (p.y - centre.y) / scale
        fx = min(max(fx, 0.04), 0.96)
        fy = min(max(fy, 0.04), 0.96)
        return A.rect_uv(A.SKIN_TILE, A.SIZE["skin"], fx, fy)
    return f


def hair_uv(u0=0.0, u1=1.0):
    """u across the lock (b), v from the root (a = 0, v 1) to the tip (a = 1, v 0), inside a margin of the strand texture."""
    def f(p, n, a, b):
        return A.rect_uv(A.HAIR_RECT, A.SIZE["hair"], lerp(0.04, 0.96, lerp(u0, u1, b)), lerp(0.02, 0.98, a))
    return f


def decal_uv(rect, fx, fy, size_key="uniform"):
    return A.rect_uv(rect, A.SIZE[size_key], fx, fy)


# ----------------------------------------------------------------------------- parts
PARTS = []


class Part:
    def __init__(self, joint, slot, verts, faces, uv, params=None, closed=True, smooth=True, cut=None, hint=None, label=""):
        self.joint, self.slot, self.verts, self.faces, self.uv = joint, slot, verts, faces, uv
        self.params = params or [(0.0, 0.0)] * len(verts)
        self.closed, self.smooth, self.cut, self.hint, self.label = closed, smooth, cut, hint, label


def add(part):
    PARTS.append(part)
    return part


def transform_parts(parts, matrix):
    for part in parts:
        part.verts = [matrix @ v for v in part.verts]


def sweep(joint, slot, uv, path, radii, sides=16, power=2.0, ref=(0.0, 0.0, 1.0), deform=None, cap0=True, cap1=True,
          smooth=True, cut=None, label=""):
    """A closed tube along `path` with an elliptical (super-ellipse) section (rx, rz[, ox, oz]) per path point. Ring angle 0 = the ref side."""
    n = len(path)
    verts, params, rings = [], [], []
    prev_z = None
    for i, c in enumerate(path):
        t = (path[min(i + 1, n - 1)] - path[max(i - 1, 0)]).normalized()
        z = Vector(ref) if prev_z is None else prev_z
        z = z - t * z.dot(t)
        if z.length < 1e-5:
            z = Vector((1.0, 0.0, 0.0)) - t * t.x
        z.normalize()
        prev_z = z
        x = t.cross(z)
        r = radii[i]
        rx, rz = r[0], r[1]
        ox = r[2] if len(r) > 2 else 0.0
        oz = r[3] if len(r) > 3 else 0.0
        ring = []
        for k in range(sides):
            th = 2.0 * math.pi * k / sides
            px = rx * sgnpow(math.sin(th), 2.0 / power)
            pz = rz * sgnpow(math.cos(th), 2.0 / power)
            p = c + x * (px + ox) + z * (pz + oz)
            if deform:
                p = deform(i, th, p, c, x, z)
            ring.append(len(verts))
            verts.append(p)
            params.append((i / max(n - 1, 1), k / sides))
        rings.append(ring)
    faces = []
    for a, b in zip(rings, rings[1:]):
        for k in range(sides):
            k2 = (k + 1) % sides
            faces.append((a[k], a[k2], b[k2], b[k]))
    if cap0:
        faces.append(tuple(reversed(rings[0])))
    if cap1:
        faces.append(tuple(rings[-1]))
    return add(Part(joint, slot, verts, faces, uv, params, True, smooth, cut, None, label))


def grid_surface(joint, slot, uv, grid, outward, smooth=True, cut=None, label=""):
    """An open sheet from grid[i][j] points (a single side, normal toward `outward(p)`)."""
    verts, params = [], []
    ni, nj = len(grid), len(grid[0])
    for i in range(ni):
        for j in range(nj):
            verts.append(grid[i][j])
            params.append((i / max(ni - 1, 1), j / max(nj - 1, 1)))
    faces = []
    for i in range(ni - 1):
        for j in range(nj - 1):
            faces.append((i * nj + j, (i + 1) * nj + j, (i + 1) * nj + j + 1, i * nj + j + 1))
    return add(Part(joint, slot, verts, faces, uv, params, False, smooth, cut, outward, label))


def shell(joint, slot, uv, grid, thick, inside, smooth=True, cut=None, label=""):
    """A grid with a thickness: the outer sheet, an inner sheet `thick` below it (away from the point `inside`), and edge walls. Closed."""
    ni, nj = len(grid), len(grid[0])
    outer, inner, params = [], [], []

    def normal(i, j):
        a = grid[min(i + 1, ni - 1)][j] - grid[max(i - 1, 0)][j]
        b = grid[i][min(j + 1, nj - 1)] - grid[i][max(j - 1, 0)]
        n = a.cross(b)
        if n.length < 1e-9:
            return Vector((0.0, 1.0, 0.0))
        n.normalize()
        ref = grid[i][j] - (inside(grid[i][j]) if callable(inside) else inside)
        return n if n.dot(ref) >= 0.0 else -n

    verts = []
    for i in range(ni):
        for j in range(nj):
            verts.append(grid[i][j])
            params.append((i / max(ni - 1, 1), j / max(nj - 1, 1)))
    for i in range(ni):
        for j in range(nj):
            verts.append(grid[i][j] - normal(i, j) * thick)
            params.append((i / max(ni - 1, 1), j / max(nj - 1, 1)))
    off = ni * nj
    faces = []
    for i in range(ni - 1):
        for j in range(nj - 1):
            a, b, c, d = i * nj + j, (i + 1) * nj + j, (i + 1) * nj + j + 1, i * nj + j + 1
            faces.append((a, b, c, d))
            faces.append((a + off, d + off, c + off, b + off))
    for i in range(ni - 1):                                   # the two edges across j
        for j in (0, nj - 1):
            a, b = i * nj + j, (i + 1) * nj + j
            faces.append((a, b, b + off, a + off) if j == 0 else (b, a, a + off, b + off))
    for j in range(nj - 1):                                   # the two edges across i
        for i in (0, ni - 1):
            a, b = i * nj + j, i * nj + j + 1
            faces.append((b, a, a + off, b + off) if i == 0 else (a, b, b + off, a + off))
    return add(Part(joint, slot, verts, faces, uv, params, True, smooth, cut, None, label))


def plate(joint, slot, uv, centre, axes, size, round_=0.0, label="", smooth=False):
    """A flat box with its own axes (x_axis, y_axis, z_axis unit vectors) and size (sx, sy, sz); unshared corners keep it crisp."""
    ax, ay, az = axes
    hx, hy, hz = size[0] / 2.0, size[1] / 2.0, size[2] / 2.0
    corners = {}
    verts, faces, params = [], [], []

    def vtx(sx, sy, sz):
        return centre + ax * (sx * hx) + ay * (sy * hy) + az * (sz * hz)

    quad_defs = [((-1, -1, 1), (1, -1, 1), (1, 1, 1), (-1, 1, 1)), ((-1, 1, -1), (1, 1, -1), (1, -1, -1), (-1, -1, -1)),
                 ((1, -1, -1), (1, 1, -1), (1, 1, 1), (1, -1, 1)), ((-1, -1, 1), (-1, 1, 1), (-1, 1, -1), (-1, -1, -1)),
                 ((-1, 1, 1), (1, 1, 1), (1, 1, -1), (-1, 1, -1)), ((-1, -1, -1), (1, -1, -1), (1, -1, 1), (-1, -1, 1))]
    for qd in quad_defs:
        base = len(verts)
        for c in qd:
            verts.append(vtx(*c))
            params.append((0.0, 0.0))
        faces.append((base, base + 1, base + 2, base + 3))
    return add(Part(joint, slot, verts, faces, uv, params, True, smooth, None, None, label))


def box(joint, slot, uv, centre, size, rot=None, smooth=False, label=""):
    """An axis-aligned box rotated by the 3x3 matrix `rot` about its centre."""
    m = rot if rot is not None else Matrix.Identity(3)
    axes = (m @ Vector((1, 0, 0)), m @ Vector((0, 1, 0)), m @ Vector((0, 0, 1)))
    return plate(joint, slot, uv, centre, axes, size, label=label, smooth=smooth)


def ellipsoid(joint, slot, uv, centre, radii, u=14, v=8, smooth=True, label="", cut=None, deform=None):
    verts, params, faces = [], [], []
    rings = []
    for iv in range(v + 1):
        ph = math.pi * iv / v
        ring = []
        for iu in range(u):
            th = 2.0 * math.pi * iu / u
            p = centre + Vector((radii[0] * math.sin(ph) * math.sin(th), radii[1] * math.cos(ph), radii[2] * math.sin(ph) * math.cos(th)))
            if deform:
                p = deform(p, ph, th)
            ring.append(len(verts))
            verts.append(p)
            params.append((iv / v, iu / u))
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(u):
            k2 = (k + 1) % u
            faces.append((a[k], a[k2], b[k2], b[k]))
    return add(Part(joint, slot, verts, faces, uv, params, True, smooth, cut, None, label))


def decal(joint, slot, rect, size_key, centre, u_axis, v_axis, w, h, nx=1, ny=1, snap=None, outward=None, label="", flip_v=False):
    """A painted quad (or a curved sheet when `snap` moves its points onto a surface); the whole rect maps onto it. Open, one-sided."""
    grid = []
    for i in range(nx + 1):
        row = []
        for j in range(ny + 1):
            p = centre + u_axis * ((i / nx - 0.5) * w) + v_axis * ((0.5 - j / ny) * h)
            row.append(snap(p) if snap else p)
        grid.append(row)
    out = outward if outward is not None else (lambda p, nrm=u_axis.cross(v_axis): nrm)

    def uvf(p, n, a, b):
        fy = b if not flip_v else 1.0 - b
        return A.rect_uv(rect, A.SIZE[size_key], a, fy)

    return grid_surface(joint, slot, uvf, grid, out, smooth=False, label=label)


# ============================================================================= the body
BUST, BUST_Y, BUST_X = 0.026, 1.335, 0.074
TORSO_P = 2.35
TORSO_KEYS = [  # y, rx, rz
    (0.80, 0.060, 0.070), (0.86, 0.110, 0.100), (0.90, 0.160, 0.115), (0.96, 0.176, 0.123), (1.02, 0.166, 0.118), (1.07, 0.151, 0.108), (1.10, 0.146, 0.103),
    (1.16, 0.149, 0.104), (1.22, 0.157, 0.108), (1.28, 0.165, 0.113), (1.34, 0.171, 0.117), (1.40, 0.175, 0.111), (1.45, 0.176, 0.100),
    (1.48, 0.165, 0.088), (1.50, 0.140, 0.078), (1.52, 0.098, 0.068), (1.545, 0.064, 0.057), (1.56, 0.052, 0.052)]
TORSO_ROWS = [1.07, 1.08, 1.10, 1.125, 1.15, 1.18, 1.21, 1.24, 1.265, 1.28, 1.30, 1.32, 1.34, 1.36, 1.38, 1.40, 1.42, 1.44, 1.46, 1.48,
              1.50, 1.52, 1.545, 1.56]


def torso_rxz(y):
    rx, rz = interp(TORSO_KEYS, y)
    return rx, rz


def bust(y, x):
    return BUST * gauss(y, BUST_Y, 0.050) * gauss(abs(x), BUST_X, 0.052)


def torso_z(y, x, off=0.0):
    """The shirt's front surface at (x, y), the same shape the torso sweep uses (so decals lie on it)."""
    rx, rz = torso_rxz(y)
    q = max(0.0, 1.0 - (abs(x) / rx) ** TORSO_P)
    return rz * q ** (1.0 / TORSO_P) + bust(y, x) + off


def snap_torso(off):
    return lambda p: V(p.x, p.y, torso_z(p.y, p.x, off))


def torso_hint(p):
    return V(p.x * 0.4, 0.0, p.z)


def build_torso():
    path = [V(0.0, y, 0.0) for y in TORSO_ROWS]
    radii = [torso_rxz(y) for y in TORSO_ROWS]

    def deform(i, th, p, c, x, z):
        y = c.y
        front = max(0.0, math.cos(th))
        p = p.copy()
        p.z += bust(y, p.x) * front ** 0.8
        p.z -= 0.0035 * gauss(y, 1.25, 0.03) * (0.5 + 0.5 * front)         # the fold under the bust
        p.z += 0.002 * math.sin(7.0 * y + th * 2.0) * gauss(y, 1.09, 0.05)   # the tuck wrinkles above the belt
        return p

    sweep("Chest", UNIFORM, swatch("white"), path, radii, sides=30, power=TORSO_P, deform=deform,
          cut=[("Hips", -9.0, 1.08), ("Spine", 1.08, 1.28), ("Chest", 1.28, 9.0)], label="shirt")


def build_pelvis():
    ys = [0.80, 0.86, 0.90, 0.95, 1.00, 1.04, 1.075]
    path = [V(0.0, y, 0.0) for y in ys]
    radii = [interp(TORSO_KEYS, y) for y in ys]
    sweep("Hips", UNIFORM, swatch("white"), path, radii, sides=30, power=2.2, label="pelvis")
    # the white belt and its gold buckle
    by = [1.052, 1.062, 1.088, 1.098]
    bpath = [V(0.0, y, 0.0) for y in by]
    bradii = [(interp(TORSO_KEYS, y)[0] + 0.004 + (0.002 if 1.06 < y < 1.09 else 0.0), interp(TORSO_KEYS, y)[1] + 0.004 + (0.002 if 1.06 < y < 1.09 else 0.0)) for y in by]
    sweep("Hips", UNIFORM, swatch("shade"), bpath, bradii, sides=30, power=2.2, label="belt")
    zf = interp(TORSO_KEYS, 1.075)[1] + 0.008
    plate("Hips", UNIFORM, swatch("gold"), V(0.0, 1.075, zf + 0.002), (V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)), (0.050, 0.036, 0.008), label="buckle")
    plate("Hips", UNIFORM, swatch("shade"), V(0.0, 1.075, zf + 0.0075), (V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)), (0.036, 0.022, 0.004), label="buckle inset")


LEG_X = 0.092
LEG_KEYS = [(0.035, 0.082, 0.100), (0.20, 0.078, 0.090), (0.35, 0.075, 0.083), (0.50, 0.076, 0.082), (0.65, 0.081, 0.089),
            (0.80, 0.090, 0.102), (0.92, 0.090, 0.112), (0.97, 0.088, 0.116), (1.02, 0.080, 0.112), (1.07, 0.070, 0.104)]
LEG_ROWS = [0.035, 0.06, 0.10, 0.15, 0.20, 0.27, 0.35, 0.42, 0.48, 0.50, 0.55, 0.62, 0.70, 0.78, 0.86, 0.92, 0.97, 1.02, 1.07]


def build_legs():
    for u, side in ((-1, "L"), (1, "R")):
        path = [V(u * (LEG_X - 0.032 * smoothstep(0.96, 1.07, y)), y, 0.0 + 0.004 * math.sin(y * 5.0)) for y in LEG_ROWS]
        radii = [interp(LEG_KEYS, y) for y in LEG_ROWS]

        def deform(i, th, p, c, x, z):
            y = c.y
            p = p.copy()
            crease = 0.0022 * (gauss(th, 0.0, 0.14) + gauss(th - 2.0 * math.pi, 0.0, 0.14))      # the pressed front crease
            fold = 0.0035 * gauss(y, 0.06, 0.05) + 0.0025 * gauss(y, 0.50, 0.05)                 # a break at the hem and one at the knee
            radial = Vector((p.x - c.x, 0.0, p.z - c.z))
            if radial.length > 1e-9:
                p += radial.normalized() * (crease + fold * (0.4 + 0.6 * max(0.0, math.cos(th))))
            return p

        sweep(f"Thigh_{side}", UNIFORM, swatch("white"), path, radii, sides=20, power=2.1, deform=deform,
              cut=[(f"Shin_{side}", -9.0, 0.48), (f"Thigh_{side}", 0.48, 9.0)], label=f"trouser {side}")
        build_shoe(u, side)


def build_shoe(u, side):
    j = f"Foot_{side}"
    keys = [(-0.070, 0.033, 0.038, 0.0, 0.040), (-0.035, 0.036, 0.046, 0.0, 0.050), (0.0, 0.037, 0.050, 0.0, 0.054),
            (0.05, 0.038, 0.040, 0.0, 0.050), (0.10, 0.039, 0.030, 0.0, 0.040), (0.15, 0.037, 0.025, 0.0, 0.032),
            (0.19, 0.030, 0.020, 0.0, 0.026), (0.205, 0.018, 0.012, 0.0, 0.022)]
    path = [V(u * LEG_X, ky, kz) for kz, _, _, _, ky in keys]
    radii = [(a, b) for _, a, b, _, _ in keys]
    sweep(j, UNIFORM, swatch("black"), path, radii, sides=14, power=2.2, ref=(0.0, 1.0, 0.0), label=f"shoe {side}")
    plate(j, UNIFORM, swatch("sole"), V(u * LEG_X, 0.006, 0.065), (V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)), (0.074, 0.012, 0.295), label=f"sole {side}")
    plate(j, UNIFORM, swatch("sole"), V(u * LEG_X, 0.016, -0.050), (V(1, 0, 0), V(0, 1, 0), V(0, 0, 1)), (0.050, 0.030, 0.050), label=f"heel {side}")


# ----------------------------------------------------------------------------- arms
SHOULDER_X = 0.20
SLEEVE_KEYS = [(1.33, 0.052, 0.050), (1.37, 0.0535, 0.051), (1.42, 0.0555, 0.053), (1.47, 0.0565, 0.054), (1.495, 0.052, 0.050),
               (1.508, 0.040, 0.040), (1.517, 0.022, 0.022)]
SLEEVE_ROWS = [1.33, 1.35, 1.38, 1.42, 1.46, 1.485, 1.50, 1.51, 1.517]


def arm_x(u, y):
    """The arm leans out a little from the shoulder (x 0.185) to the elbow joint (x 0.20)."""
    return u * (0.185 + 0.015 * min(max((1.50 - y) / 0.31, 0.0), 1.0))
UPPER_KEYS = [(1.185, 0.040, 0.040), (1.24, 0.041, 0.042), (1.30, 0.045, 0.046), (1.36, 0.048, 0.048), (1.42, 0.049, 0.049)]


# the aim pose: both hands on a pistol in front of the chest, elbows tucked. World metres. The pistol itself is built in commander_uniform.py.
PISTOL_X = -0.02                 # the pistol's centre line
BORE_Y = 1.360                   # the height of the barrel; MuzzlePoint is moved here
GRIP_R = V(PISTOL_X, 1.292, 0.236)   # where the right palm meets the grip
GRIP_L = V(PISTOL_X, 1.296, 0.262)   # where the left hand wraps the front of it
ARM = {}                         # side -> (elbow, wrist), for the watch


def build_arms():
    for u, side in ((-1, "L"), (1, "R")):
        up, fore = f"UpperArm_{side}", f"ForeArm_{side}"
        sx = u * SHOULDER_X
        # short sleeve (white, loose) and the bare upper arm inside it
        path = [V(arm_x(u, y), y, 0.0) for y in SLEEVE_ROWS]
        radii = [interp(SLEEVE_KEYS, y) for y in SLEEVE_ROWS]
        sweep(up, UNIFORM, swatch("white"), path, radii, sides=18, label=f"sleeve {side}")
        ys = [1.185, 1.24, 1.30, 1.36, 1.42]
        sweep(up, SKIN, skin_tile(V(sx, 1.3, 0.0)), [V(arm_x(u, y), y, 0.0) for y in ys], [interp(UPPER_KEYS, y) for y in ys], sides=14,
              label=f"upper arm {side}")
        # the forearm swings up, forward and inward from the elbow to the grip (the joint stays where the Agent Controller expects it)
        elbow = WORLD[fore]
        g = GRIP_R if u > 0 else GRIP_L
        n_hint = V(-u, 0.0, 0.0)                                                 # the palm faces the pistol
        w0 = elbow + (g - elbow).normalized() * 0.255
        f0 = (g - w0).normalized()
        f = (f0 - n_hint * f0.dot(n_hint)).normalized()
        n = (n_hint - f * n_hint.dot(f)).normalized()
        wrist = g - f * 0.060 - n * 0.030
        ARM[side] = (elbow, wrist)
        ellipsoid(fore, SKIN, skin_tile(elbow), elbow, (0.0425 * BUILD, 0.0425 * BUILD, 0.0425 * BUILD), u=12, v=7, label=f"elbow {side}")
        d = (wrist - elbow).normalized()
        mid = elbow + (wrist - elbow) * 0.45 + V(0.0, 0.006, 0.0)
        pts = catmull([elbow, mid, wrist], 4)
        keys = [(0.0, 0.040 * BUILD, 0.037 * BUILD), (0.35, 0.043 * BUILD, 0.037 * BUILD), (0.75, 0.033 * BUILD, 0.029 * BUILD), (1.0, 0.028 * BUILD, 0.024 * BUILD)]
        radii = [interp(keys, i / (len(pts) - 1)) for i in range(len(pts))]
        sweep(fore, SKIN, skin_tile(elbow + d * 0.13), pts, radii, sides=14, ref=(0.0, 1.0, 0.0), label=f"forearm {side}")
        build_hand(u, side, wrist, f, n)
    # the watch on the left wrist
    elbow, wrist = ARM["L"]
    d = (wrist - elbow).normalized()
    c = elbow + (wrist - elbow) * 0.86
    pts = [c - d * 0.014, c + d * 0.014]
    sweep("ForeArm_L", UNIFORM, swatch("watch"), pts, [(0.0345, 0.0305), (0.0345, 0.0305)], sides=16, ref=(0.0, 1.0, 0.0), label="watch strap")
    n_out = V(-1.0, 0.4, 0.0)
    n_out = (n_out - d * n_out.dot(d)).normalized()
    plate("ForeArm_L", UNIFORM, swatch("watch"), c + n_out * 0.033, (d.cross(n_out), d, n_out), (0.030, 0.026, 0.009), label="watch case")


def build_hand(u, side, wrist, f, n, curl=1.9):
    """A hand with a palm, a thumb and four fingers curled around a grip, built in its own frame (wrist at the origin, fingers along +Y, palm
    facing -Z) and then moved onto the wrist. f = the direction of the fingers, n = the palm's normal."""
    j = f"Hand_{side}"
    start = len(PARTS)
    sk = skin_swatch()
    palm_keys = [(0.0, 0.028, 0.016), (0.03, 0.033, 0.0165), (0.065, 0.039, 0.0165), (0.095, 0.040, 0.0145)]
    ys = [0.0, 0.025, 0.05, 0.075, 0.097]
    sweep(j, SKIN, sk, [V(0.0, y, 0.0) for y in ys], [interp(palm_keys, y) for y in ys], sides=10, ref=(0.0, 0.0, 1.0), label=f"palm {side}")
    fingers = [(0.030, 0.076, 0.0098), (0.0105, 0.084, 0.0102), (-0.0095, 0.078, 0.0098), (-0.028, 0.062, 0.0085)]
    for fx, length, rad in fingers:
        pts = []
        pos = V(fx, 0.090, 0.0)
        ang = 0.0
        for seg in range(3):
            pts.append(pos.copy())
            seg_len = length * (0.42, 0.30, 0.28)[seg]
            ang += (0.30, 0.55, 0.45)[seg] * curl * 0.62                         # curl toward the palm (-Z)
            pos = pos + V(0.0, math.cos(ang) * seg_len, -math.sin(ang) * seg_len)
        pts.append(pos)
        pts = catmull(pts, 2)
        radii = [(rad * (1.0 - 0.28 * i / (len(pts) - 1)), rad * 0.92 * (1.0 - 0.28 * i / (len(pts) - 1))) for i in range(len(pts))]
        sweep(j, SKIN, sk, pts, radii, sides=7, ref=(0.0, 0.0, 1.0), label=f"finger {side}")
    thumb = catmull([V(0.034, 0.012, -0.004), V(0.050, 0.035, -0.010), V(0.056, 0.060, -0.018), V(0.050, 0.082, -0.026)], 3)
    radii = [(0.0125 - 0.003 * i / (len(thumb) - 1), 0.0115 - 0.003 * i / (len(thumb) - 1)) for i in range(len(thumb))]
    sweep(j, SKIN, sk, thumb, radii, sides=7, ref=(0.0, 0.0, 1.0), label=f"thumb {side}")
    s_axis = f.cross(n)
    transform_parts(PARTS[start:], Matrix.Scale(1.0 + (BUILD - 1.0) * 0.6, 4))
    # canonical axes: +Y -> f, -Z -> n (the palm), +X -> the thumb side (the right hand is mirrored so its thumb comes out on top)
    if u > 0:
        transform_parts(PARTS[start:], Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1))))
    basis = Matrix((s_axis, f, -n)).transposed()                                  # columns: images of x, y, z
    transform_parts(PARTS[start:], Matrix.Translation(wrist) @ basis.to_4x4())


# ============================================================================= the build (how sturdy she is)
# One knob, BUILD, thickens the body around the same joints: the joint heights, pivots, height and MuzzlePoint do not change, only the meshes.
# 1.0 was the slim first version; the tests limit the width to 0.45..0.85 m.
BUILD = 1.13


def _thick(keys, f):
    return [(k[0],) + tuple(v * f(k[0]) for v in k[1:]) for k in keys]


_torso_f = lambda y: 1.0 + (BUILD - 1.0) * smoothstep(1.52, 1.44, y)
TORSO_KEYS[:] = _thick(TORSO_KEYS, _torso_f)
LEG_KEYS[:] = _thick(LEG_KEYS, lambda y: 1.0 + (BUILD - 1.0) * 0.9 * smoothstep(0.02, 0.2, y))
SLEEVE_KEYS[:] = _thick(SLEEVE_KEYS, lambda y: BUILD)
UPPER_KEYS[:] = _thick(UPPER_KEYS, lambda y: 1.0 + (BUILD - 1.0) * 1.2)
LEG_X = 0.096


def arm_x(u, y):
    """The arm leans in from the shoulder (x 0.215, pushed out by the sturdier torso) to the elbow joint (x 0.20)."""
    return u * (0.215 - 0.015 * min(max((1.50 - y) / 0.31, 0.0), 1.0))


# ============================================================================= assemble, normals, split, export
def inside_point(part):
    return sum(part.verts, Vector((0.0, 0.0, 0.0))) / len(part.verts)


def finalize_part(part):
    """Blender-world mesh of one part, oriented outward, triangulated, with corner normals."""
    bm = bmesh.new()
    vs = [bm.verts.new(bl(v)) for v in part.verts]
    for f in part.faces:
        try:
            bm.faces.new([vs[i] for i in f])
        except ValueError:
            pass
    bm.verts.ensure_lookup_table()
    bm.faces.ensure_lookup_table()
    if part.closed:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    else:
        for f in bm.faces:
            c = f.calc_center_median()
            want = bl(part.hint(Vector((c.x, c.z, c.y))))
            if f.normal.dot(want) < 0.0:
                f.normal_flip()
    index = {v: i for i, v in enumerate(bm.verts)}
    for v in bm.verts:
        v.index = index[v]
    tris = bmesh.ops.triangulate(bm, faces=bm.faces[:])["faces"]
    bm.faces.ensure_lookup_table()
    me = bpy.data.meshes.new("tmp")
    bm.to_mesh(me)
    bm.free()
    for poly in me.polygons:
        poly.use_smooth = part.smooth
    if part.smooth:
        bm2 = bmesh.new()
        bm2.from_mesh(me)
        for e in bm2.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > SMOOTH_ANGLE:
                e.smooth = False
            if len(e.link_faces) != 2:
                e.smooth = True
        bm2.to_mesh(me)
        bm2.free()
    me.update()
    normals = [Vector(n.vector) for n in me.corner_normals]
    verts = [Vector(v.co) for v in me.vertices]
    polys = [tuple(p.vertices) for p in me.polygons]
    loops = [list(p.loop_indices) for p in me.polygons]
    bpy.data.meshes.remove(me)
    return verts, polys, loops, normals


def uv_for(part, verts_bl):
    out = []
    for i, vb in enumerate(verts_bl):
        p = Vector((vb.x, vb.z, vb.y))
        a, b = part.params[i] if i < len(part.params) else (0.0, 0.0)
        out.append(part.uv(p, None, a, b))
    return out


def assemble():
    """Parts -> one dict per joint: verts (Blender world), polygons, uv per corner, normals per corner, material slot per polygon."""
    per_joint = {name: dict(verts=[], polys=[], uvs=[], normals=[], slots=[]) for name, _, _ in JOINTS}
    for part in PARTS:
        verts, polys, loops, normals = finalize_part(part)
        # part.params only exist for the original vertices; triangulation adds none, so indices still match
        uvs = uv_for(part, verts)
        for pi, poly in enumerate(polys):
            c = sum((verts[i] for i in poly), Vector((0.0, 0.0, 0.0))) / len(poly)
            joint = part.joint
            if part.cut:
                y = c.z                                                       # Blender z is Unity y
                for name, y0, y1 in part.cut:
                    if y0 <= y < y1:
                        joint = name
                        break
            tgt = per_joint[joint]
            base = len(tgt["verts"])
            remap = {}
            for vi in poly:
                if vi not in remap:
                    remap[vi] = len(tgt["verts"])
                    tgt["verts"].append(verts[vi])
            tgt["polys"].append(tuple(remap[vi] for vi in poly))
            for corner, vi in enumerate(poly):
                tgt["uvs"].append(uvs[vi])
                tgt["normals"].append(normals[loops[pi][corner]])
            tgt["slots"].append(part.slot)
    return per_joint


def srgb_to_linear(hex_colour):
    h = hex_colour.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


def make_material(name, key, prefix):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    base = os.path.join(TEX_DIR, prefix + "BaseColor.png")
    if os.path.exists(base):
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(base)
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        if key == "hair":
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])                      # the lock cards are alpha-clipped in Unity
        ms = os.path.join(TEX_DIR, prefix + "MetalSmooth.png")
        if os.path.exists(ms):
            mtex = nt.nodes.new("ShaderNodeTexImage")
            mtex.image = bpy.data.images.load(ms)
            mtex.image.colorspace_settings.name = 'Non-Color'
            sep = nt.nodes.new("ShaderNodeSeparateColor")
            invert = nt.nodes.new("ShaderNodeMath")
            invert.operation = 'SUBTRACT'
            invert.inputs[0].default_value = 1.0
            nt.links.new(mtex.outputs["Color"], sep.inputs["Color"])
            nt.links.new(sep.outputs["Red"], bsdf.inputs["Metallic"])
            nt.links.new(mtex.outputs["Alpha"], invert.inputs[1])
            nt.links.new(invert.outputs["Value"], bsdf.inputs["Roughness"])
    else:
        bsdf.inputs["Base Color"].default_value = srgb_to_linear({"uniform": "#e9e9e4", "skin": A.SKIN_HEX, "hair": "#8a6a42"}[key])
    if key == "skin":
        bsdf.inputs["Roughness"].default_value = 0.65
    return mat


def build_scene(per_joint):
    root = bpy.data.objects.new("CharCommander", None)
    root.empty_display_type = 'PLAIN_AXES'
    root.rotation_euler = (math.radians(90.0), 0.0, 0.0)
    bpy.context.scene.collection.objects.link(root)
    mats = [make_material(*m) for m in MATERIALS]
    objs = {}
    total = 0
    rot = Matrix.Rotation(math.radians(-90.0), 3, 'X')
    for name, parent, off in JOINTS:
        data = per_joint[name]
        origin = bl(WORLD[name])
        me = bpy.data.meshes.new(name)
        if data["polys"]:
            verts = [rot @ (v - origin) for v in data["verts"]]
            me.from_pydata([tuple(v) for v in verts], [], data["polys"])
            me.update()
            uv = me.uv_layers.new(name="UVMap")
            li = 0
            for poly in me.polygons:
                for _ in poly.loop_indices:
                    uv.data[li].uv = data["uvs"][li]
                    li += 1
            used = sorted(set(data["slots"]))
            remap = {old: new for new, old in enumerate(used)}
            for pi, poly in enumerate(me.polygons):
                poly.material_index = remap[data["slots"][pi]]
                poly.use_smooth = True
            for slot in used:
                me.materials.append(mats[slot])
            me.normals_split_custom_set([tuple(rot @ n) for n in data["normals"]])
            total += len(me.polygons)
        if data["polys"]:
            ob = bpy.data.objects.new(name, me)
        else:                                                                 # a joint without geometry (the Gun socket) is a plain Empty
            bpy.data.meshes.remove(me)
            ob = bpy.data.objects.new(name, None)
            ob.empty_display_type = 'PLAIN_AXES'
            ob.empty_display_size = 0.04
        bpy.context.scene.collection.objects.link(ob)
        ob.parent = root if parent is None else objs[parent]
        ob.location = (off[0], off[1], -off[2])
        objs[name] = ob
    muzzle = bpy.data.objects.new("MuzzlePoint", None)
    muzzle.empty_display_type = 'ARROWS'
    muzzle.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(muzzle)
    muzzle.parent = objs["Gun"]
    muzzle.location = (PISTOL_X - WORLD["Gun"].x, BORE_Y - WORLD["Gun"].y, -0.22)     # the root's rotated frame: z mirrored
    return root, objs, total


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


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    build_torso()
    build_pelvis()
    build_legs()
    build_arms()
    import commander_head
    import commander_uniform
    me = sys.modules[__name__]
    commander_head.build(me)
    commander_uniform.build(me)
    per_joint = assemble()
    root, objs, total = build_scene(per_joint)
    bpy.context.view_layer.update()
    lo = Vector((9e9, 9e9, 9e9))
    hi = Vector((-9e9, -9e9, -9e9))
    for name, ob in objs.items():
        if ob.type != 'MESH':
            continue
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            u = Vector((w.x, w.z, w.y))
            for k in range(3):
                lo[k] = min(lo[k], u[k])
                hi[k] = max(hi[k], u[k])
    tris = sum(len(p.vertices) - 2 for ob in objs.values() if ob.type == 'MESH' for p in ob.data.polygons)
    size = hi - lo
    print("=" * 70)
    print(f"[Commander] {tris} triangles (target 14000), {len(MATERIALS)} materials, {len(PARTS)} parts")
    print(f"[Commander] size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   y {lo.y:.3f}..{hi.y:.3f}   z {lo.z:.3f}..{hi.z:.3f}   (height 1.87)")
    heavy = sorted(((sum(len(p.vertices) - 2 for p in ob.data.polygons), n) for n, ob in objs.items() if ob.type == 'MESH'), reverse=True)[:5]
    print(f"[Commander] heaviest joints {heavy}")
    draft = os.environ.get("COMMANDER_DRAFT")                                  # a folder: write there instead of into the project
    blend_path, fbx_path = (os.path.join(draft, "SM_Char_Commander.blend"), os.path.join(draft, "SM_Char_Commander.fbx")) if draft else (BLEND, FBX)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    export_fbx(fbx_path)
    print(f"[Commander] {blend_path}")
    print(f"[Commander] {fbx_path}")


if __name__ == "__main__":
    main()
