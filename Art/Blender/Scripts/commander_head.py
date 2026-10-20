"""The head of the realistic Commander: neck, a sculpted face, ears and short layered hair (step C10b). Owner: Core Developer.

build(B) is called by build_commander.py with its own module as B (so the parts end up in the same list). Everything is Unity metres, the
face looks along +Z.

The head is built the way a sculptor lays out a portrait, not from a sphere: horizontal super-ellipse rings stacked from the chin to the
crown, each ring's front given by a SIDE PROFILE table (chin, lower lip, mouth, philtrum, forehead...), its half-width by a WIDTH table (jaw,
cheeks, temples) and its back by a skull table. Gaussian features (nose, brows, eye sockets, cheekbones, lips) are added on top, then the
dense grid is thinned with Blender's Decimate (collapse, X symmetry) to a game-sized mesh. Its UVs are a planar front projection, and
make_commander_textures.py paints eyes, brows, lips and skin with the very same projection (commander_atlas.head_px).
Hair is a ridged shell over the skull, cut at a wavy hairline that goes above the ears, plus a few single-sided locks (two-sided material).
All numbers are DESIGN metres; T() scales the head up by commander_atlas.HS about the chin.
"""

import math
import random

import bmesh
import bpy
from mathutils import Vector

import commander_atlas as A

Y_BOTTOM, Y_TOP = 1.6125, 1.846

# side profile of the face plane (the nose is added on top): y, z of the front at the midline
PLANE = [(1.6125, 0.040), (1.620, 0.066), (1.628, 0.0765), (1.636, 0.0735), (1.648, 0.0735), (1.662, 0.0735), (1.690, 0.0755),
         (1.720, 0.0775), (1.745, 0.0805), (1.775, 0.0830), (1.800, 0.0785), (1.820, 0.0650), (1.835, 0.0400), (1.846, 0.0)]
# half width of the head at each height: chin, jaw, cheeks, temples, crown
WIDTH = [(1.6125, 0.022), (1.620, 0.031), (1.630, 0.041), (1.645, 0.052), (1.665, 0.063), (1.690, 0.071), (1.720, 0.0750),
         (1.750, 0.0765), (1.780, 0.0740), (1.810, 0.0620), (1.830, 0.0430), (1.846, 0.0)]
# the back of the skull
BACK = [(1.6125, -0.040), (1.625, -0.062), (1.645, -0.085), (1.680, -0.100), (1.720, -0.108), (1.760, -0.108), (1.800, -0.093),
        (1.820, -0.075), (1.835, -0.045), (1.846, 0.0)]
HAIRLINE = [(0, 1.795), (30, 1.790), (50, 1.768), (70, 1.752), (95, 1.750), (110, 1.738), (130, 1.710), (150, 1.682), (180, 1.668)]
FACE_CENTRE_Z = -0.12
HEAD_MIDDLE = Vector((0.0, 1.72, -0.01))


def gauss(x, c, w):
    return math.exp(-((x - c) / w) ** 2)


def smoothstep(a, b, x):
    t = max(0.0, min(1.0, (x - a) / (b - a)))
    return t * t * (3.0 - 2.0 * t)


def sgnpow(x, e):
    return math.copysign(abs(x) ** e, x)


def T(p):
    """Design coordinates -> the scaled head (see commander_atlas.HS and HEAD_DROP)."""
    c = A.HEAD_PIVOT
    return Vector((c[0] + (p.x - c[0]) * A.HS, c[1] + (p.y - c[1]) * A.HS + A.HEAD_DROP, c[2] + (p.z - c[2]) * A.HS))


def surface(y, theta, B):
    """The bare head at height y and azimuth theta (0 = front, + toward +X), in design coordinates."""
    y = min(max(y, Y_BOTTOM), Y_TOP)
    zf = B.interp(PLANE, y)[0]
    zb = B.interp(BACK, y)[0]
    w = B.interp(WIDTH, y)[0]
    zc, d = 0.5 * (zf + zb), 0.5 * (zf - zb)
    power = 3.0 if math.cos(theta) >= 0.0 else 2.2
    x = w * sgnpow(math.sin(theta), 2.0 / power)
    z = zc + d * sgnpow(math.cos(theta), 2.0 / power)
    return Vector((x, y, z))


def face_features(p, B):
    """Outward displacement in metres at the point p of the face: nose, brows, eye sockets, cheeks, lips."""
    x, y = abs(p.x), p.y
    front = smoothstep(0.035, 0.075, p.z)
    if front <= 0.0:
        return 0.0
    nose_a = B.interp([(1.660, 0.0), (1.669, 0.0055), (1.680, 0.0205), (1.690, 0.0240), (1.705, 0.0165), (1.725, 0.0100),
                       (1.745, 0.0045), (1.760, 0.0)], y)[0]
    nose_w = B.interp([(1.660, 0.0125), (1.672, 0.0135), (1.684, 0.0110), (1.705, 0.0088), (1.745, 0.0075)], y)[0]
    d = nose_a * gauss(x, 0.0, nose_w)
    d += 0.0075 * gauss(x, 0.0150, 0.0065) * gauss(y, 1.6725, 0.0065)                        # the nostril wings
    d -= 0.0045 * gauss(y, 1.6615, 0.0050) * gauss(x, 0.0, 0.020)                           # under the nose
    d -= 0.0070 * gauss(x, A.EYE_X, 0.0200) * gauss(y, A.EYE_Y + 0.001, 0.0120)             # eye sockets
    d += 0.0060 * gauss(y, A.BROW_Y + 0.003, 0.0065) * gauss(x, 0.032, 0.030)               # brow ridge
    d += 0.0020 * gauss(y, 1.7395, 0.0040) * gauss(x, 0.032, 0.016)                          # the lid fold
    d += 0.0060 * gauss(x, 0.049, 0.021) * gauss(y, 1.698, 0.018)                            # cheekbones
    d -= 0.0035 * gauss(x, 0.050, 0.020) * gauss(y, 1.662, 0.015)                            # the hollow under them
    d += 0.0058 * gauss(y, 1.6535, 0.0050) * gauss(x, 0.0, 0.0185)                          # upper lip
    d += 0.0068 * gauss(y, 1.6405, 0.0058) * gauss(x, 0.0, 0.0175)                          # lower lip
    d -= 0.0040 * gauss(y, 1.6470, 0.0021) * gauss(x, 0.0, 0.0215)                          # the mouth line
    return d * front


FACE_RATIO = 0.20


def build_face(B):
    ys = [Y_BOTTOM + (Y_TOP - Y_BOTTOM) * (i / 70.0) for i in range(71)]
    nu = 72
    verts, faces, rings = [], [], []
    for y in ys:
        ring = []
        for k in range(nu):
            t = -1.0 + 2.0 * k / nu
            th = math.pi * math.copysign(abs(t) ** 1.35, t)                    # denser at the front and the back
            p = surface(y, th, B)
            disp = face_features(p, B)
            if disp != 0.0:
                n = Vector((p.x, 0.0, p.z - FACE_CENTRE_Z)).normalized()
                p = p + n * disp
            ring.append(len(verts))
            verts.append(p)
        rings.append(ring)
    for a, b in zip(rings, rings[1:]):
        for k in range(nu):
            k2 = (k + 1) % nu
            faces.append((a[k], a[k2], b[k2], b[k]))
    faces.append(tuple(reversed(rings[0])))
    faces.append(tuple(rings[-1]))
    me = bpy.data.meshes.new("face_hi")
    me.from_pydata([tuple(B.bl(T(v))) for v in verts], [], faces)
    me.update()
    ob = bpy.data.objects.new("face_hi", me)
    bpy.context.scene.collection.objects.link(ob)
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    mod = ob.modifiers.new("dec", 'DECIMATE')
    mod.decimate_type = 'COLLAPSE'
    mod.ratio = FACE_RATIO
    mod.use_symmetry = True
    mod.symmetry_axis = 'X'
    deps = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(deps)
    out = ev.to_mesh()
    dverts = [B.bl(v.co) for v in out.vertices]
    dfaces = [tuple(p.vertices) for p in out.polygons]
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    B.add(B.Part("Head", B.SKIN, dverts, dfaces, B.head_planar(), None, True, True, None, None, "head"))
    return len(dfaces)


def build_ears(B):
    for u in (-1, 1):
        centre = T(Vector((u * 0.0765, 1.7215, -0.012)))
        ry, rz = 0.0315, 0.0185

        def deform(p, ph, th, u=u, centre=centre):
            q = p - centre
            p = p + Vector((u * 0.004 * (q.y / 0.03), 0.0, -0.004 * (q.y / 0.03)))
            if u * (p.x - centre.x) > 0.0:                                 # the outer face: a dished middle inside a raised rim
                r = math.hypot(q.y / ry, q.z / rz)
                dish = 0.0055 * smoothstep(0.78, 0.25, r)
                rim = 0.0022 * gauss(r, 0.88, 0.13)
                lobe = 0.0015 * gauss(q.y, -0.026, 0.008)
                p.x += u * (rim + lobe - dish)
            return p

        B.ellipsoid("Head", B.SKIN, B.skin_swatch(), centre, (0.0070, ry, rz), u=14, v=9, deform=deform, label="ear")


def build_neck(B):
    ys = [1.470, 1.50, 1.54, 1.58, 1.615, 1.640]
    path = [Vector((0.0, y, -0.004 - 0.012 * (y - 1.47) / 0.17)) for y in ys]
    keys = [(1.47, 0.068, 0.068), (1.52, 0.061, 0.062), (1.57, 0.057, 0.058), (1.615, 0.056, 0.058), (1.64, 0.056, 0.058)]
    B.sweep("Neck", B.SKIN, B.skin_swatch(), path, [B.interp(keys, y) for y in ys], sides=18, power=2.2, label="neck")


# ----------------------------------------------------------------------------- hair
def hair_point(theta, t, B, lift=0.0):
    """A point of the hair cap (world metres) and its outward normal: theta the signed azimuth, t 0 at the crown to 1 at the hairline."""
    deg = abs(math.degrees(theta))
    yb = B.interp(HAIRLINE, deg)[0] + 0.0035 * math.sin(9.0 * theta + 1.0) * smoothstep(5.0, 40.0, deg)
    y = Y_TOP - 0.001 - (Y_TOP - 0.001 - yb) * t
    s = surface(y, theta, B)
    e = 0.004
    dth = surface(y, theta + 0.02, B) - surface(y, theta - 0.02, B)
    dy = surface(y + e, theta, B) - surface(y - e, theta, B)
    n = dth.cross(dy)
    if n.length < 1e-9:
        n = Vector((0.0, 1.0, 0.0))
    n.normalize()
    if n.dot(s - HEAD_MIDDLE) < 0.0:
        n = -n
    dvec = (s - HEAD_MIDDLE).normalized()
    h = B.lerp(0.0200, 0.0050, t ** 1.3) * B.lerp(1.0, 0.62, smoothstep(40.0, 100.0, deg))
    h += 0.020 * smoothstep(0.35, 0.9, dvec.y) * smoothstep(0.0, 0.6, dvec.z) * (1.0 - t * 0.7)        # the volume brushed up over the forehead
    h += 0.008 * smoothstep(0.2, 0.8, dvec.y) * smoothstep(0.0, 0.7, -dvec.z) * (1.0 - t * 0.5)        # and the fullness at the back
    h += 0.0025 * math.sin(5.0 * theta + 6.0 * t) * (1.0 - t)
    h += 0.0040 * (0.35 + 0.65 * t) * (0.6 * math.sin(15.0 * theta + 6.0 * t + 0.5) + 0.4 * math.sin(29.0 * theta - 8.0 * t + 2.0))   # clumps
    h += lift
    return T(s + n * h), n


CAP_Y = 1.775   # design height where the cap's band starts: the hair above it is under the cap, so it is not built


def hair_start(theta, B):
    """t where the hair cap starts for the capped head: the surface height CAP_Y, never nearer than 0.1 to the hairline."""
    deg = abs(math.degrees(theta))
    yb = B.interp(HAIRLINE, deg)[0] + 0.0035 * math.sin(9.0 * theta + 1.0) * smoothstep(5.0, 40.0, deg)
    return min(0.9, max(0.0, (Y_TOP - 0.001 - CAP_Y) / max(Y_TOP - 0.001 - yb, 1e-6)))


def build_hair(B):
    rng = random.Random(21)
    nth, nrings = 60, 6
    verts, params, faces = [], [], []
    ring_idx = []
    for r in range(0, nrings + 2):
        ring = []
        for k in range(nth):
            th = ((math.pi * 2.0 * k / nth) % (2.0 * math.pi)) - math.pi
            t0 = hair_start(th, B)
            t = t0 + (1.0 - t0) * min(r / nrings, 1.0)
            if r <= nrings:
                p, _ = hair_point(th, t, B)
            else:                                                       # the last ring tucks into the skin under the hairline
                p, _ = hair_point(th, 1.0, B, lift=-0.0085)
            ring.append(len(verts))
            verts.append(p)
            params.append((t, abs(th) / math.pi))
        ring_idx.append(ring)
    for a, b in zip(ring_idx, ring_idx[1:]):
        for k in range(nth):
            k2 = (k + 1) % nth
            faces.append((a[k], a[k2], b[k2], b[k]))

    def uv(p, n, a, b):
        return A.rect_uv(A.HAIR_CAP_RECT, A.SIZE["hair"], 0.04 + 0.92 * b, 0.02 + 0.96 * a)

    centre = T(HEAD_MIDDLE)
    B.add(B.Part("Head", B.HAIR, verts, faces, uv, params, False, True, None, lambda p: p - centre, "hair cap"))
    build_locks(B, rng)


def build_locks(B, rng):
    """Single-sided hair locks laid over the cap: swept up and back over the front, tousled on the crown, short round the sides and nape."""
    specs = []
    for _ in range(12):                              # the sides over the ears
        specs.append((rng.choice((-1, 1)) * rng.uniform(70.0, 115.0), rng.uniform(0.6, 0.97), rng.uniform(0.026, 0.040), "down"))
    for _ in range(14):                              # the nape
        specs.append((rng.choice((-1, 1)) * rng.uniform(125.0, 180.0), rng.uniform(0.6, 0.97), rng.uniform(0.026, 0.040), "down"))
    centre = T(HEAD_MIDDLE)
    for deg, t, length, mode in specs:
        theta = math.radians(deg)
        root, n = hair_point(theta, t, B, lift=-0.0035)
        pa, _ = hair_point(theta, max(t - 0.05, 0.0), B)
        pb, _ = hair_point(theta, min(t + 0.05, 1.0), B)
        down = (pb - pa).normalized()                  # toward the hairline
        side = Vector((math.cos(theta), 0.0, -math.sin(theta)))
        if mode == "up":
            flow = (-down * 0.85 + side * 0.45 * (1.0 if rng.random() < 0.72 else -1.0)).normalized()
            lift = rng.uniform(0.003, 0.007)
        elif mode == "fringe":
            flow = (down * 0.6 + side * 0.7 * (1.0 if rng.random() < 0.75 else -1.0)).normalized()
            lift = rng.uniform(0.0005, 0.002)
        elif mode == "crown":
            flow = (down * rng.uniform(-0.6, 0.6) + side * rng.uniform(-0.8, 0.8)).normalized()
            lift = rng.uniform(0.003, 0.007)
        else:
            flow = (down * 0.80 + Vector((0.0, 0.0, -0.25)) * (1.0 if abs(deg) < 100.0 else 0.0) + side * rng.uniform(-0.2, 0.2)).normalized()
            lift = rng.uniform(0.001, 0.004)
        flow = (flow - n * flow.dot(n)).normalized()
        across = flow.cross(n).normalized()
        width = rng.uniform(0.030, 0.044) * A.HS
        rows = []
        for i in range(5):
            s_ = i / 4.0
            c = root + flow * (length * A.HS * s_) + n * ((lift * s_ ** 1.5 + 0.0015) * A.HS)
            w = width * (1.0 - 0.15 * s_)
            ridge = 0.0030 * (1.0 - s_)
            rows.append([c - across * (w / 2.0), c + n * ridge, c + across * (w / 2.0)])
        flip = rng.random() < 0.5

        def card_uv(p, n, a, b, flip=flip):
            return A.rect_uv(A.HAIR_CARD_RECT, A.SIZE["hair"], (1.0 - b) if flip else b, a * 0.97)

        B.grid_surface("Head", B.HAIR, card_uv, rows, (lambda p, c=centre: p - c), label="lock")


def build(B):
    n = build_face(B)
    print(f"[Commander] face {n} triangles after the decimation")
    build_ears(B)
    build_neck(B)
    build_hair(B)
