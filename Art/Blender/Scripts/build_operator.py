"""Builds the desert Security Patrol operator: SM_Char_Patrol, a hero-quality tactical soldier with a SCAR-style rifle (step PS3). Owner: Core Developer.

    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_operator.py

The look is the owner's reference photo of a desert-tan tactical operator: a beige high-cut helmet with rails and a front mount, a tan balaclava with only the
eyes showing, a high-neck tan jacket with a zip, a plate-carrier vest with webbing rows, magazine pouches, a radio and its cable, a brown belt with a row of
ammo pouches and a holster, cargo trousers with knee pads, thigh straps and drop-leg pouches, and laced tan boots. The face and gear are generic: nobody's
likeness, no insignia. build_humans.py hands "Patrol" over to this script (like the Commander, step C10b), because rigid boxes with flat colours could not
carry this much detail.

It reuses the Commander's engine (build_commander.py): every part is a sweep, a shell or a plate, shaded with custom normals that are kept when the mesh is cut
into joints, so the cloth is round and the buckles stay crisp. What is its own: the geometry, the atlas (patrol_atlas.py, painted by make_patrol_textures.py)
and a box projection for the tan cloth (one tile per metre, make_patrol_cloth.py).

Rig: unchanged from the other humans (docs/art-pipeline.md section 18): 18 joint objects under one Empty root rotated +90 degrees about X, the joint offsets of
build_humans.JOINTS, 1.87 m tall, facing +Z, MuzzlePoint under Gun 0.74 m ahead of the socket. The arms are modelled bent (both hands on the rifle) while every
joint stays where the rig table has it, at rotation 0.

Numbers below are UNITY metres (x right, y up, z forward) unless a function says Blender. Left = -X.
Budget: 12,000 triangles target (hero character, ModelBudget "SM_Char_Patrol"), 2 materials (the third slot is free).
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_commander as C  # noqa: E402  (the engine: sweeps, shells, plates, normals, split, FBX; importing it builds nothing)
import patrol_atlas as A  # noqa: E402

REPO = C.REPO
BLEND = os.path.join(REPO, "Art", "Blender", "SM_Char_Patrol.blend")
FBX = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Char_Patrol.fbx")

CLOTH, KIT = 0, 1
MATERIALS = [("M_Char_Patrol_Cloth", "cloth", "T_CharPatrol_"), ("M_Char_Patrol_Kit", "kit", "T_CharPatrolKit_")]
RIFLE_DX = 0.12                               # the rifle sits this far to the right of the Gun socket (the Hunter's value too)
MUZZLE_AHEAD = 0.74                           # metres from the Gun socket to MuzzlePoint (docs/reference/core-developer.md 4.5)

def V(x, y, z):
    return Vector((x, y, z))


WORLD = C.WORLD
GUN = WORLD["Gun"]
INTERP = C.interp
JOINT_CUT = [("Hips", -9.0, 1.08), ("Spine", 1.08, 1.28), ("Chest", 1.28, 9.0)]


# ----------------------------------------------------------------------------- paints: (material slot, UV maker)
def _swatch(name):
    uv = A.swatch_uv(name)
    return lambda p, n, a, b: uv


def _cloth_uv(p, n, a, b):
    """Box projection at one UV unit per metre: the face normal picks the plane, so the weave keeps its scale on every part."""
    ax, ay, az = abs(n.x), abs(n.y), abs(n.z)
    if ay >= ax and ay >= az:
        return (p.x, p.z)
    if ax >= az:
        return (p.z, p.y)
    return (p.x, p.y)


_cloth_uv.per_face = True
_face_uv = lambda p, n, a, b: A.face_uv(p.x, p.y)  # noqa: E731

PAINT = {name: (KIT, _swatch(name)) for name in A.KIT_SWATCHES}
PAINT["cloth"] = (CLOTH, _cloth_uv)
PAINT["face"] = (KIT, _face_uv)


def sweep(joint, paint, path, radii, **kw):
    return C.sweep(joint, paint[0], paint[1], path, radii, **kw)


def box(joint, paint, centre, size, rot=None, label=""):
    return C.box(joint, paint[0], paint[1], centre, size, rot, label=label)


def plate(joint, paint, centre, axes, size, label=""):
    return C.plate(joint, paint[0], paint[1], centre, axes, size, label=label)


def ellipsoid(joint, paint, centre, radii, u=14, v=8, label="", deform=None):
    return C.ellipsoid(joint, paint[0], paint[1], centre, radii, u=u, v=v, label=label, deform=deform)


def decal(joint, name, centre, u_axis, v_axis, w, h, outward, nx=1, ny=1, snap=None, label=""):
    return C.decal(joint, KIT, A.KIT_DECALS[name], "uniform", centre, u_axis, v_axis, w, h, nx=nx, ny=ny, snap=snap, outward=outward, label=label)


IDENT = (V(1, 0, 0), V(0, 1, 0), V(0, 0, 1))


def rx_(deg):
    return Matrix.Rotation(math.radians(deg), 3, "X")


# ----------------------------------------------------------------------------- the torso and the gear on it
TORSO_P = 2.35
TORSO_KEYS = [  # y, rx, rz: a broad male torso, the jacket's outer surface
    (0.80, 0.100, 0.095), (0.86, 0.146, 0.110), (0.92, 0.170, 0.118), (1.00, 0.176, 0.122), (1.07, 0.170, 0.118), (1.16, 0.170, 0.120),
    (1.24, 0.180, 0.126), (1.32, 0.192, 0.134), (1.40, 0.198, 0.138), (1.46, 0.196, 0.128), (1.50, 0.172, 0.104), (1.52, 0.128, 0.088),
    (1.545, 0.074, 0.068), (1.56, 0.056, 0.056)]
TORSO_ROWS = [1.07, 1.09, 1.12, 1.16, 1.20, 1.24, 1.28, 1.32, 1.36, 1.40, 1.44, 1.47, 1.50, 1.52, 1.545, 1.56]
VEST_T = 0.014                                 # the vest stands this far off the jacket


def trz(y):
    return INTERP(TORSO_KEYS, y)


def tz(y, x, off=0.0):
    """The jacket's front surface at (x, y), the same shape the torso sweep uses."""
    rx, rz = trz(y)
    q = max(0.0, 1.0 - (abs(x) / rx) ** TORSO_P)
    return rz * q ** (1.0 / TORSO_P) + off


def vest_r(y):
    rx, rz = trz(y)
    return (rx + VEST_T) * (1.0 - 0.16 * C.smoothstep(1.40, 1.47, y)), rz + VEST_T


def vest_z(y, x, off=0.0):
    rx, rz = vest_r(y)
    q = max(0.0, 1.0 - (abs(x) / rx) ** 3.0)
    return rz * q ** (1.0 / 3.0) + off


def build_torso():
    def deform(i, th, p, c, x, z):
        y = c.y
        front = max(0.0, math.cos(th))
        p = p.copy()
        p.z += 0.003 * math.sin(9.0 * y + th * 3.0) * C.gauss(y, 1.10, 0.07)            # the blousing wrinkles over the belt
        p.z += 0.008 * C.gauss(y, 1.075, 0.025) * (0.4 + 0.6 * front)                    # the jacket bulges over the belt
        return p

    sweep("Chest", PAINT["cloth"], [V(0.0, y, 0.0) for y in TORSO_ROWS], [trz(y) for y in TORSO_ROWS], sides=30, power=TORSO_P, deform=deform,
          cut=JOINT_CUT, label="jacket")
    ys = [0.80, 0.84, 0.88, 0.92, 0.96, 1.00, 1.04, 1.075]
    sweep("Hips", PAINT["cloth"], [V(0.0, y, 0.0) for y in ys], [trz(y) for y in ys], sides=30, power=2.2, label="pelvis")
    # the high collar, bunched at the top
    keys = [(1.500, 0.098, 0.088), (1.540, 0.078, 0.074), (1.585, 0.074, 0.072), (1.625, 0.079, 0.077), (1.634, 0.071, 0.069)]
    ys = [1.500, 1.52, 1.54, 1.565, 1.585, 1.605, 1.625, 1.634]
    sweep("Chest", PAINT["cloth"], [V(0.0, y, 0.0) for y in ys], [INTERP(keys, y) for y in ys], sides=24, power=2.1,
          cut=[("Chest", -9.0, 1.55), ("Neck", 1.55, 9.0)], label="collar")
    decal("Chest", "zip", V(0.0, 1.598, 0.0765), V(1, 0, 0), V(0, 1, 0), 0.016, 0.062, lambda p: V(0.0, 0.0, 1.0), label="collar zip")
    # the jacket zip below the vest
    rows = [V(0.0, y, tz(y, 0.0, 0.002)) for y in (1.128, 1.088, 1.048, 1.008)]
    C.grid_surface("Spine", KIT, lambda p, n, a, b: C.A.rect_uv(A.KIT_DECALS["zip"], A.SIZE, 0.1 + 0.8 * b, 0.02 + 0.5 * a), [[V(x, r.y, r.z) for x in (-0.014, 0.014)] for r in rows],
                   lambda p: V(0.0, 0.0, 1.0), smooth=False, cut=[("Hips", -9.0, 1.08), ("Spine", 1.08, 9.0)], label="jacket zip")


LEG_X = 0.100
LEG_KEYS = [(0.17, 0.093, 0.099), (0.22, 0.085, 0.090), (0.30, 0.078, 0.082), (0.40, 0.080, 0.084), (0.50, 0.085, 0.090), (0.62, 0.092, 0.100),
            (0.78, 0.100, 0.110), (0.92, 0.104, 0.116), (1.02, 0.098, 0.112), (1.07, 0.080, 0.100)]
LEG_ROWS = [0.17, 0.20, 0.24, 0.30, 0.37, 0.44, 0.50, 0.56, 0.64, 0.72, 0.80, 0.88, 0.94, 1.00, 1.04, 1.07]


def build_legs():
    for u, side in ((-1, "L"), (1, "R")):
        thigh, shin = f"Thigh_{side}", f"Shin_{side}"
        path = [V(u * (LEG_X - 0.020 * C.smoothstep(0.96, 1.07, y)), y, 0.004 * math.sin(y * 5.0)) for y in LEG_ROWS]
        radii = [INTERP(LEG_KEYS, y) for y in LEG_ROWS]

        def deform(i, th, p, c, x, z):
            y = c.y
            p = p.copy()
            radial = V(p.x - c.x, 0.0, p.z - c.z)
            if radial.length > 1e-9:
                fold = 0.007 * C.gauss(y, 0.19, 0.030) + 0.004 * C.gauss(y, 0.50, 0.05) + 0.004 * math.sin(13.0 * y + th * 2.0) * C.gauss(y, 0.34, 0.20) + 0.003 * math.sin(9.0 * y + th * 3.0) * C.gauss(y, 0.85, 0.2)
                p += radial.normalized() * fold
            return p

        sweep(thigh, PAINT["cloth"], path, radii, sides=20, power=2.1, deform=deform, cut=[(shin, -9.0, 0.48), (thigh, 0.48, 9.0)],
              label=f"trouser {side}")
        x = u * LEG_X
        rxk, rzk = INTERP(LEG_KEYS, 0.50)
        plate(shin, PAINT["cloth"], V(x, 0.50, rzk + 0.006), IDENT, (0.092, 0.112, 0.020), label=f"knee pad {side}")
        plate(shin, PAINT["dark"], V(x, 0.455, rzk + 0.0125), IDENT, (0.070, 0.014, 0.006), label=f"knee pad edge {side}")
        # cargo pocket with its flap, outside the thigh
        rxp, rzp = INTERP(LEG_KEYS, 0.80)
        px = x + u * (rxp + 0.012)
        plate(thigh, PAINT["cloth"], V(px, 0.735, 0.02), IDENT, (0.030, 0.150, 0.112), label=f"cargo pocket {side}")
        plate(thigh, PAINT["cloth"], V(px + u * 0.004, 0.800, 0.02), IDENT, (0.034, 0.042, 0.116), label=f"cargo flap {side}")
        # two leather thigh straps with a buckle, and a drop-leg pouch
        for y in (0.88, 0.66):
            rxs, rzs = INTERP(LEG_KEYS, y)
            sweep(thigh, PAINT["leather"], [V(x, y - 0.014, 0.0), V(x, y + 0.014, 0.0)], [(rxs + 0.005, rzs + 0.005)] * 2, sides=20, power=2.1,
                  label=f"strap {side}")
            plate(thigh, PAINT["silver"], V(x + u * (rxs + 0.008), y, 0.0), IDENT, (0.008, 0.030, 0.022), label=f"strap buckle {side}")
        rxd, rzd = INTERP(LEG_KEYS, 0.80)
        dx = x + u * (rxd + 0.030)
        plate(thigh, PAINT["olive"], V(dx, 0.80, 0.03), IDENT, (0.046, 0.100, 0.110), label=f"drop pouch {side}")
        decal(thigh, "pouch", V(dx + u * 0.0235, 0.80, 0.03), V(0, 0, -u), V(0, 1, 0), 0.100, 0.096, lambda p, u=u: V(u, 0.0, 0.0), label=f"drop pouch face {side}")
        build_boot(u, side)


def build_boot(u, side):
    j = f"Foot_{side}"
    x = u * LEG_X
    keys = [(-0.075, 0.040, 0.054, 0.0, 0.055), (-0.040, 0.044, 0.062, 0.0, 0.062), (0.0, 0.046, 0.066, 0.0, 0.067), (0.05, 0.047, 0.056, 0.0, 0.064),
            (0.10, 0.047, 0.044, 0.0, 0.054), (0.15, 0.044, 0.034, 0.0, 0.043), (0.19, 0.036, 0.027, 0.0, 0.035), (0.208, 0.022, 0.018, 0.0, 0.030)]
    sweep(j, PAINT["khaki"], [V(x, ky, kz) for kz, _, _, _, ky in keys], [(a, b) for _, a, b, _, _ in keys], sides=14, power=2.2, ref=(0.0, 1.0, 0.0),
          label=f"boot {side}")
    sweep(j, PAINT["khaki"], [V(x, 0.05, -0.006), V(x, 0.14, -0.008), V(x, 0.235, -0.010)], [(0.062, 0.070), (0.060, 0.066), (0.064, 0.068)], sides=16,
          label=f"boot shaft {side}")
    sweep(j, PAINT["dark"], [V(x, 0.236, -0.010), V(x, 0.252, -0.010)], [(0.062, 0.066)] * 2, sides=16, label=f"boot collar {side}")
    plate(j, PAINT["dark"], V(x, 0.007, 0.065), IDENT, (0.092, 0.014, 0.300), label=f"sole {side}")
    plate(j, PAINT["dark"], V(x, 0.022, -0.050), IDENT, (0.074, 0.026, 0.060), label=f"heel {side}")
    decal(j, "boot", V(x, 0.118, 0.0630), V(1, 0, 0), V(0, 1, 0), 0.078, 0.130, lambda p: V(0.0, 0.0, 1.0), label=f"laces {side}")


def build_gear():
    # --- the plate-carrier vest: a tube round the chest, with webbing, four magazine pouches, a radio, side buckles and shoulder straps
    ys = [1.13, 1.16, 1.20, 1.26, 1.32, 1.38, 1.43, 1.455, 1.468]
    sweep("Chest", PAINT["olive"], [V(0.0, y, 0.0) for y in ys], [vest_r(y) for y in ys], sides=30, power=3.0, cut=JOINT_CUT, label="vest")
    web_snap = lambda p: V(p.x, p.y, vest_z(p.y, p.x, 0.0015))  # noqa: E731
    decal("Chest", "webbing", V(0.0, 1.355, 0.0), V(1, 0, 0), V(0, 1, 0), 0.310, 0.170, lambda p: V(0.0, 0.0, 1.0), nx=8, ny=4, snap=web_snap, label="webbing front")
    back_snap = lambda p: V(p.x, p.y, -vest_z(p.y, p.x, 0.0015))  # noqa: E731
    decal("Chest", "webbing", V(0.0, 1.30, 0.0), V(-1, 0, 0), V(0, 1, 0), 0.320, 0.250, lambda p: V(0.0, 0.0, -1.0), nx=8, ny=5, snap=back_snap, label="webbing back")
    for x in (-0.125, -0.045, 0.045, 0.125):
        z = vest_z(1.19, x) + 0.0205
        plate("Chest", PAINT["olive"], V(x, 1.19, z), IDENT, (0.064, 0.100, 0.041), label="mag pouch")
        decal("Chest", "pouch", V(x, 1.19, z + 0.0211), V(1, 0, 0), V(0, 1, 0), 0.060, 0.096, lambda p: V(0.0, 0.0, 1.0), label="mag pouch face")
    rz = vest_z(1.385, 0.115) + 0.019
    plate("Chest", PAINT["dark"], V(0.115, 1.385, rz), IDENT, (0.054, 0.086, 0.036), label="radio")
    decal("Chest", "radio", V(0.115, 1.385, rz + 0.0185), V(1, 0, 0), V(0, 1, 0), 0.050, 0.082, lambda p: V(0.0, 0.0, 1.0), label="radio face")
    cable = C.catmull([V(0.108, 1.342, rz + 0.004), V(0.095, 1.30, rz - 0.003), V(0.070, 1.252, rz - 0.004), V(0.050, 1.222, rz - 0.004)], 3)
    sweep("Chest", PAINT["dark"], cable, [(0.0034, 0.0034)] * len(cable), sides=6, label="radio cable")
    for u in (-1, 1):
        for y in (1.215, 1.275):
            rxv, _ = vest_r(y)
            plate("Chest", PAINT["silver"], V(u * (rxv + 0.004), y, 0.02), IDENT, (0.008, 0.030, 0.022), label="side buckle")
        pts = C.catmull([V(u * 0.105, 1.455, vest_z(1.455, u * 0.105) + 0.006), V(u * 0.120, 1.510, 0.040), V(u * 0.122, 1.528, -0.010),
                         V(u * 0.108, 1.470, -vest_z(1.455, u * 0.105) - 0.006)], 3)
        sweep("Chest", PAINT["olive"], pts, [(0.027, 0.0045)] * len(pts), sides=8, ref=(0.0, 1.0, 0.0), label="shoulder strap")
    # --- the brown belt, its buckle, a row of ammo pouches on the left hip and the holster on the right
    by = [1.044, 1.052, 1.078, 1.086]
    sweep("Hips", PAINT["leather"], [V(0.0, y, 0.0) for y in by], [(trz(y)[0] + 0.007, trz(y)[1] + 0.007) for y in by], sides=30, power=2.2, label="belt")
    zf = trz(1.065)[1] + 0.012
    plate("Hips", PAINT["silver"], V(0.0, 1.065, zf), IDENT, (0.048, 0.034, 0.008), label="buckle")
    plate("Hips", PAINT["dark"], V(0.0, 1.065, zf + 0.0045), IDENT, (0.032, 0.020, 0.004), label="buckle inset")
    rxb, rzb = trz(1.065)
    for k, ang in enumerate((22.0, 40.0, 58.0, 76.0, 94.0)):
        a = math.radians(ang)
        pos = V(-(rxb + 0.022) * math.sin(a), 1.052, (rzb + 0.022) * math.cos(a))
        t, n = V(math.cos(a), 0.0, math.sin(a)), V(-math.sin(a), 0.0, math.cos(a))
        plate("Hips", PAINT["leather"], pos, (t, V(0, 1, 0), n), (0.034, 0.060, 0.030), label="ammo pouch")
        plate("Hips", PAINT["dark"], pos + n * 0.0155 + V(0.0, 0.020, 0.0), (t, V(0, 1, 0), n), (0.030, 0.014, 0.004), label="ammo pouch flap")
    hx = trz(0.93)[0] + 0.030
    plate("Hips", PAINT["dark"], V(hx, 0.925, 0.045), IDENT, (0.042, 0.150, 0.076), label="holster")
    decal("Hips", "holster", V(hx + 0.0215, 0.925, 0.045), V(0, 0, -1), V(0, 1, 0), 0.072, 0.146, lambda p: V(1.0, 0.0, 0.0), label="holster face")
    plate("Hips", PAINT["gunmetal"], V(hx, 1.030, 0.045), IDENT, (0.030, 0.050, 0.060), label="pistol grip")
    # --- shoulder velcro patches, lying on the outside of each sleeve
    for u, side in ((-1, "L"), (1, "R")):
        sh, el = ARMS[side]["sh"], ARMS[side]["el"]
        d = (el - sh).normalized()
        n = V(u, 0.15, 0.0)
        n = (n - d * n.dot(d)).normalized()
        across = d.cross(n)
        c = sh + (el - sh) * 0.30 + n * 0.063
        plate(f"UpperArm_{side}", PAINT["dark"], c, (across, d, n), (0.075, 0.075, 0.006), label="patch")
        decal(f"UpperArm_{side}", "patch", c + n * 0.0037, across, d, 0.073, 0.073, lambda p, n=n: n, label="patch face")


# ----------------------------------------------------------------------------- the head: balaclava, helmet
def build_head():
    keys = [(1.600, 0.045, 0.050, 0.0, 0.040), (1.620, 0.062, 0.072, 0.0, 0.035), (1.650, 0.074, 0.088, 0.0, 0.022), (1.685, 0.080, 0.098, 0.0, 0.008),
            (1.720, 0.083, 0.104, 0.0, -0.002), (1.760, 0.082, 0.104, 0.0, -0.006), (1.800, 0.070, 0.092, 0.0, -0.008),
            (1.830, 0.045, 0.062, 0.0, -0.008), (1.845, 0.015, 0.020, 0.0, -0.008)]
    ys = [k[0] for k in keys]
    sweep("Head", PAINT["face"], [V(0.0, y, 0.0) for y in ys], [INTERP(keys, y) for y in ys], sides=26, power=2.0, label="head")
    sweep("Neck", PAINT["khaki"], [V(0.0, 1.52, 0.0), V(0.0, 1.565, 0.0), V(0.0, 1.62, 0.0)], [(0.055, 0.058), (0.050, 0.053), (0.055, 0.060)], sides=16, label="neck")
    ellipsoid("Head", PAINT["face"], V(0.0, 1.700, 0.104), (0.016, 0.024, 0.016), u=10, v=6, label="nose")
    for u in (-1, 1):
        ellipsoid("Head", PAINT["face"], V(u * 0.084, 1.714, -0.006), (0.010, 0.030, 0.020), u=8, v=5, label="ear")
    # --- the helmet: a high-cut shell. Its rim is lifted over the brow so the eyes show, and at the sides so the ears are free of its edge.
    hk = [(1.690, 0.096, 0.116), (1.730, 0.100, 0.121), (1.775, 0.102, 0.122), (1.820, 0.098, 0.116), (1.860, 0.083, 0.099), (1.890, 0.056, 0.068),
          (1.906, 0.020, 0.026)]
    hys = [1.690, 1.710, 1.730, 1.755, 1.775, 1.800, 1.820, 1.840, 1.860, 1.878, 1.890, 1.900, 1.906]

    def lift(i, th, p, c, x, z):
        p = p.copy()
        front = max(0.0, math.cos(th)) ** 1.4
        side = abs(math.sin(th)) ** 2
        p.y = max(p.y, p.y + (1.772 - p.y) * front if p.y < 1.772 else p.y)
        p.y = max(p.y, 1.700 + 0.020 * side)
        return p

    sweep("Head", PAINT["tan"], [V(0.0, y, -0.012) for y in hys], [INTERP(hk, y) for y in hys], sides=26, power=2.3, deform=lift, label="helmet")
    for u in (-1, 1):
        rail = V(u * 0.1075, 1.805, -0.012)
        box("Head", PAINT["khaki"], rail, (0.011, 0.034, 0.130), label="helmet rail")
        decal("Head", "ticks", rail + V(u * 0.0062, 0.0, 0.0), V(0, 0, 1), V(0, 1, 0), 0.126, 0.014, lambda p, u=u: V(u, 0.0, 0.0), label="rail ticks")
        pts = C.catmull([V(u * 0.098, 1.712, 0.000), V(u * 0.094, 1.668, 0.040), V(u * 0.060, 1.616, 0.086), V(u * 0.018, 1.602, 0.098)], 3)
        sweep("Head", PAINT["dark"], pts, [(0.0070, 0.0035)] * len(pts), sides=8, ref=(0.0, 1.0, 0.0), label="chin strap")
    plate("Head", PAINT["dark"], V(0.0, 1.600, 0.092), IDENT, (0.058, 0.012, 0.034), label="chin cup")
    plate("Head", PAINT["gunmetal"], V(0.0, 1.857, 0.110), (V(1, 0, 0), V(0, math.cos(math.radians(25)), -math.sin(math.radians(25))),
                                                           V(0, math.sin(math.radians(25)), math.cos(math.radians(25)))), (0.054, 0.042, 0.020), label="front mount")
    for x in (-0.016, 0.016):
        plate("Head", PAINT["silver"], V(x, 1.862, 0.121), IDENT, (0.006, 0.006, 0.004), label="mount bolt")
    box("Head", PAINT["khaki"], V(0.0, 1.9045, -0.012), (0.016, 0.010, 0.094), label="top rail")
    box("Head", PAINT["dark"], V(0.0, 1.795, -0.128), (0.058, 0.040, 0.022), label="rear pouch")


# ----------------------------------------------------------------------------- the rifle (SCAR-style, in the Gun joint)
def RW(lx, ly, lz):
    """A point in the rifle's frame (its centre line at x 0, bore at y 0, the Gun socket at z 0) in world metres."""
    return V(GUN.x + RIFLE_DX + lx, GUN.y + ly, GUN.z + lz)


def build_rifle(scope=False):
    """The rifle's parts in the Gun joint. scope=True adds a rifle scope on two rings over the top rail (the Hunter's rifle, step HV1)."""
    j = "Gun"
    box(j, PAINT["tan"], RW(0, 0, 0.03), (0.052, 0.074, 0.40), label="receiver")
    box(j, PAINT["gunmetal"], RW(0, 0.043, 0.20), (0.024, 0.012, 0.62), label="top rail")
    decal(j, "ticks", RW(0, 0.0494, 0.20), V(0, 0, 1), V(1, 0, 0), 0.62, 0.024, lambda p: V(0.0, 1.0, 0.0), label="top rail ticks")
    box(j, PAINT["tan"], RW(0, -0.003, 0.37), (0.058, 0.066, 0.26), label="handguard")
    for u in (-1, 1):
        box(j, PAINT["gunmetal"], RW(0.034 * u, -0.003, 0.40), (0.010, 0.020, 0.12), label="side rail")
        decal(j, "ticks", RW(0.0396 * u, -0.003, 0.40), V(0, 0, 1), V(0, 1, 0), 0.12, 0.018, lambda p, u=u: V(u, 0.0, 0.0), label="side rail ticks")
    box(j, PAINT["gunmetal"], RW(0, -0.040, 0.40), (0.020, 0.010, 0.16), label="bottom rail")
    sweep(j, PAINT["gunmetal"], [RW(0, 0, 0.50), RW(0, 0, 0.70)], [(0.0125, 0.0125)] * 2, sides=10, ref=(0.0, 1.0, 0.0), label="barrel")
    sweep(j, PAINT["gunmetal"], [RW(0, 0, MUZZLE_AHEAD - 0.050), RW(0, 0, MUZZLE_AHEAD + 0.005)], [(0.0185, 0.0185)] * 2, sides=10, ref=(0.0, 1.0, 0.0),
          label="flash hider")
    for z in (0.708, 0.722):
        sweep(j, PAINT["dark"], [RW(0, 0, z), RW(0, 0, z + 0.004)], [(0.0192, 0.0192)] * 2, sides=10, ref=(0.0, 1.0, 0.0), label="hider groove")
    box(j, PAINT["gunmetal"], RW(0, 0.012, 0.52), (0.024, 0.030, 0.040), label="gas block")
    box(j, PAINT["gunmetal"], RW(0, 0.066, 0.52), (0.012, 0.040, 0.012), label="front sight")
    box(j, PAINT["gunmetal"], RW(0, 0.056, -0.07), (0.040, 0.014, 0.030), label="rear sight base")
    for u in (-1, 1):
        box(j, PAINT["gunmetal"], RW(0.012 * u, 0.080, -0.07), (0.008, 0.036, 0.010), label="rear sight post")
    box(j, PAINT["tan"], RW(0, -0.085, -0.02), (0.040, 0.100, 0.046), rot=rx_(-17.0), label="pistol grip")
    box(j, PAINT["gunmetal"], RW(0, -0.045, 0.035), (0.010, 0.012, 0.060), label="trigger guard")
    box(j, PAINT["tan"], RW(0, -0.055, 0.095), (0.042, 0.040, 0.075), label="magwell")
    mag = C.catmull([RW(0, -0.060, 0.092), RW(0, -0.120, 0.103), RW(0, -0.185, 0.118)], 3)
    sweep(j, PAINT["gunmetal"], mag, [(0.0185, 0.030)] * len(mag), sides=12, power=4.0, ref=(0.0, 0.0, 1.0), label="magazine")
    box(j, PAINT["tan"], RW(0, -0.010, -0.23), (0.050, 0.080, 0.12), label="folding stock")
    box(j, PAINT["tan"], RW(0, 0.040, -0.25), (0.040, 0.030, 0.10), label="cheek riser")
    box(j, PAINT["tan"], RW(0, -0.058, -0.25), (0.036, 0.035, 0.10), label="lower strut")
    box(j, PAINT["gunmetal"], RW(0, -0.005, -0.165), (0.050, 0.070, 0.020), label="stock hinge")
    box(j, PAINT["dark"], RW(0, -0.010, -0.305), (0.050, 0.115, 0.026), label="butt pad")
    if scope:
        for z in (0.02, 0.20):
            box(j, PAINT["gunmetal"], RW(0, 0.063, z), (0.032, 0.028, 0.022), label="scope ring")
        sweep(j, PAINT["gunmetal"], [RW(0, 0.094, -0.025), RW(0, 0.094, 0.285)], [(0.0175, 0.0175)] * 2, sides=14, ref=(0.0, 1.0, 0.0), label="scope tube")
        sweep(j, PAINT["gunmetal"], [RW(0, 0.094, 0.235), RW(0, 0.094, 0.300)], [(0.026, 0.026)] * 2, sides=14, ref=(0.0, 1.0, 0.0), label="scope objective")
        sweep(j, PAINT["gunmetal"], [RW(0, 0.094, -0.055), RW(0, 0.094, -0.020)], [(0.022, 0.022)] * 2, sides=14, ref=(0.0, 1.0, 0.0), label="scope eyepiece")
        box(j, PAINT["dark"], RW(0, 0.118, 0.100), (0.016, 0.014, 0.016), label="scope elevation turret")
        box(j, PAINT["dark"], RW(0.021, 0.094, 0.100), (0.014, 0.016, 0.016), label="scope windage turret")


# ----------------------------------------------------------------------------- the arms: bent, both hands on the rifle
ARMS = {"R": dict(sh=V(0.205, 1.46, 0.0), el=V(0.272, 1.165, -0.02), grip=RW(0.005, -0.088, -0.015), n=V(-1.0, 0.0, 0.0)),
        "L": dict(sh=V(-0.205, 1.46, 0.0), el=V(-0.120, 1.255, 0.210), grip=RW(0.0, -0.058, 0.30), n=V(0.0, 1.0, 0.0))}


def sleeve_creases(n):
    """Cloth creases on a sleeve: ripples that grow towards the elbow end, so the arm is not a smooth tube."""
    def deform(i, th, p, c, x, z):
        t = i / max(n - 1, 1)
        radial = V(p.x - c.x, p.y - c.y, p.z - c.z)
        if radial.length > 1e-9:
            p = p + radial.normalized() * (0.0035 * math.sin(17.0 * t + th * 2.0) * (0.4 + t) + 0.003 * C.gauss(t, 0.5, 0.12) * math.cos(3.0 * th))
        return p
    return deform


def build_arms():
    for u, side in ((-1, "L"), (1, "R")):
        a = ARMS[side]
        up, fore, hand = f"UpperArm_{side}", f"ForeArm_{side}", f"Hand_{side}"
        sh, el, g, n_hint = a["sh"], a["el"], a["grip"], a["n"]
        f0 = (g - el).normalized()
        f = (f0 - n_hint * f0.dot(n_hint)).normalized()
        n = (n_hint - f * n_hint.dot(f)).normalized()
        wrist = g - f * 0.060 - n * 0.030
        a["wrist"] = wrist
        ellipsoid(up, PAINT["cloth"], sh, (0.064, 0.064, 0.064), u=12, v=7, label=f"shoulder {side}")
        pts = C.catmull([sh, sh.lerp(el, 0.5) + V(u * 0.006, 0.0, 0.0), el], 4)
        radii = [(0.059 - 0.008 * i / (len(pts) - 1), 0.057 - 0.008 * i / (len(pts) - 1)) for i in range(len(pts))]
        sweep(up, PAINT["cloth"], pts, radii, sides=18, deform=sleeve_creases(len(pts)), label=f"sleeve {side}")
        ellipsoid(fore, PAINT["cloth"], el, (0.052, 0.052, 0.052), u=12, v=7, label=f"elbow {side}")
        pts = C.catmull([el, el.lerp(wrist, 0.5) + V(0.0, 0.006, 0.0), wrist], 4)
        keys = [(0.0, 0.051, 0.049), (0.5, 0.045, 0.043), (0.9, 0.041, 0.039), (1.0, 0.043, 0.041)]
        radii = [INTERP(keys, i / (len(pts) - 1)) for i in range(len(pts))]
        sweep(fore, PAINT["cloth"], pts, radii, sides=16, ref=(0.0, 1.0, 0.0), deform=sleeve_creases(len(pts)), label=f"forearm {side}")
        build_hand(side, hand, wrist, f, n, u)


def build_hand(side, joint, wrist, f, n, u):
    """A bare hand with a palm, a thumb and four fingers curled round the rifle, built in its own frame (wrist at the origin, fingers along +Y, palm facing -Z)
    and then moved onto the wrist (the Commander's hand, with this character's skin swatch). f = the direction of the fingers, n = the palm's normal."""
    start = len(C.PARTS)
    sk = PAINT["skin"]
    palm_keys = [(0.0, 0.030, 0.017), (0.03, 0.036, 0.0175), (0.065, 0.042, 0.0175), (0.095, 0.043, 0.0155)]
    ys = [0.0, 0.025, 0.05, 0.075, 0.097]
    sweep(joint, sk, [V(0.0, y, 0.0) for y in ys], [INTERP(palm_keys, y) for y in ys], sides=10, ref=(0.0, 0.0, 1.0), label=f"palm {side}")
    for fx, length, rad in [(0.032, 0.080, 0.0105), (0.0112, 0.088, 0.0110), (-0.0102, 0.082, 0.0105), (-0.030, 0.066, 0.0092)]:
        pts = []
        pos = V(fx, 0.090, 0.0)
        ang = 0.0
        for seg in range(3):
            pts.append(pos.copy())
            seg_len = length * (0.42, 0.30, 0.28)[seg]
            ang += (0.30, 0.55, 0.45)[seg] * 1.9 * 0.62
            pos = pos + V(0.0, math.cos(ang) * seg_len, -math.sin(ang) * seg_len)
        pts.append(pos)
        pts = C.catmull(pts, 2)
        radii = [(rad * (1.0 - 0.28 * i / (len(pts) - 1)), rad * 0.92 * (1.0 - 0.28 * i / (len(pts) - 1))) for i in range(len(pts))]
        sweep(joint, sk, pts, radii, sides=7, ref=(0.0, 0.0, 1.0), label=f"finger {side}")
    thumb = C.catmull([V(0.036, 0.012, -0.004), V(0.053, 0.035, -0.010), V(0.059, 0.060, -0.018), V(0.053, 0.082, -0.026)], 3)
    sweep(joint, sk, thumb, [(0.0135 - 0.003 * i / (len(thumb) - 1), 0.0125 - 0.003 * i / (len(thumb) - 1)) for i in range(len(thumb))], sides=7,
          ref=(0.0, 0.0, 1.0), label=f"thumb {side}")
    s_axis = f.cross(n)
    C.transform_parts(C.PARTS[start:], Matrix.Scale(1.08, 4))
    if u > 0:
        C.transform_parts(C.PARTS[start:], Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1))))
    basis = Matrix((s_axis, f, -n)).transposed()
    C.transform_parts(C.PARTS[start:], Matrix.Translation(wrist) @ basis.to_4x4())


# ----------------------------------------------------------------------------- build, report, export
def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    build_torso()
    build_legs()
    build_arms()
    build_gear()
    build_head()
    build_rifle()
    per_joint = C.assemble()
    root, objs, total = C.build_scene(per_joint, "CharPatrol", MATERIALS, (RIFLE_DX, 0.0, MUZZLE_AHEAD))
    bpy.context.view_layer.update()
    lo = V(9e9, 9e9, 9e9)
    hi = V(-9e9, -9e9, -9e9)
    for ob in objs.values():
        if ob.type != 'MESH':
            continue
        for v in ob.data.vertices:
            w = ob.matrix_world @ v.co
            p = V(w.x, w.z, w.y)
            for k in range(3):
                lo[k] = min(lo[k], p[k])
                hi[k] = max(hi[k], p[k])
    tris = sum(len(p.vertices) - 2 for ob in objs.values() if ob.type == 'MESH' for p in ob.data.polygons)
    size = hi - lo
    print("=" * 70)
    print(f"[Patrol] {tris} triangles (target 12000), {len(MATERIALS)} materials, {len(C.PARTS)} parts")
    print(f"[Patrol] size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   y {lo.y:.3f}..{hi.y:.3f}   z {lo.z:.3f}..{hi.z:.3f}   (height 1.87)")
    heavy = sorted(((sum(len(p.vertices) - 2 for p in ob.data.polygons), n) for n, ob in objs.items() if ob.type == 'MESH'), reverse=True)[:5]
    print(f"[Patrol] heaviest joints {heavy}")
    draft = os.environ.get("OPERATOR_DRAFT")                                      # a folder: write there instead of into the project
    blend_path, fbx_path = (os.path.join(draft, "SM_Char_Patrol.blend"), os.path.join(draft, "SM_Char_Patrol.fbx")) if draft else (BLEND, FBX)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    C.export_fbx(fbx_path)
    print(f"[Patrol] {blend_path}")
    print(f"[Patrol] {fbx_path}")


if __name__ == "__main__":
    main()
