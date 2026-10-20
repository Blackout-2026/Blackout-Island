"""Builds the realistic pine and palm (step C11): SM_Tree_Pine.fbx and SM_Tree_Palm.fbx, three LODs each. Owner: Core Developer.

    python Art/Blender/Scripts/make_tree_textures.py                                               (once: the textures)
    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_trees.py            (both trees)
    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_trees.py -- Pine    (one)

What replaces the World Builder's placeholders (Pine_Placeholder, Palm_Placeholder: a stack of cones and a cylinder with flat colours):
  Pine   a 10.4 m tree. A trunk with a root flare, a slight sway and a rough, noisy outline, UV-mapped to a tiling bark texture and
         normal map. 13 whorls of drooping branches (3 to 5 per whorl, golden-angle spacing, longest at the bottom, short at the
         top), each a thin tapered tube. On every branch three crossing cards carry the needle-spray texture (alpha cut-out), a leader
         spire at the top and dead branch stubs low on the trunk, as on a real forest pine.
  Palm   a 7 m palm. A curved trunk with scar rings (a bark texture and normal map), a swollen base, a crown knob, 13 drooping fronds,
         each a V-folded pair of strips carrying the frond texture, and a cluster of coconuts.
LODs (the prefab builder switches them at the same screen heights as the placeholders): LOD0 the full tree, LOD1 about a quarter of
its triangles, LOD2 a few crossed cards and a thin trunk. Vertex normals of the foliage are set to point away from the crown's centre
and a little upward ("ball shading"), the usual trick that makes flat alpha cards light like a soft volume instead of like paper.

Frame: the tree stands at the origin (x, z) with its base on y = 0, exported like the generator: Apply Transform ON, rotation 0, one
mesh per LOD, two material slots (bark, foliage). Unity frame = (x, y, z); B() is the axis swap of docs/art-pipeline.md section 4.
"""

import math
import os
import random
import sys

import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
GOLDEN = math.pi * (3.0 - math.sqrt(5.0))


def bl(v):
    """Unity frame -> Blender frame (x, z, y), the measured axis swap."""
    return Vector((v[0], v[2], v[1]))


def unit(v):
    v = Vector(v)
    length = v.length
    return v / length if length > 1e-9 else Vector((0.0, 1.0, 0.0))


class MeshBuilder:
    """Collects vertices (position, normal, uv) and triangles with a material slot, in the Unity frame."""

    def __init__(self):
        self.P, self.N, self.UV, self.F, self.M = [], [], [], [], []

    def vert(self, p, n, uv):
        self.P.append(Vector(p))
        self.N.append(unit(n))
        self.UV.append(uv)
        return len(self.P) - 1

    def tri(self, a, b, c, mat):
        pa, pb, pc = bl(self.P[a]), bl(self.P[b]), bl(self.P[c])
        face = (pb - pa).cross(pc - pa)
        if face.dot(bl(self.N[a] + self.N[b] + self.N[c])) < 0.0:
            b, c = c, b  # wind the face so that it looks the way its vertex normals point (in the Blender frame, as exported)
        self.F.append((a, b, c))
        self.M.append(mat)

    def quad(self, a, b, c, d, mat):
        self.tri(a, b, c, mat)
        self.tri(a, c, d, mat)

    def triangles(self):
        return len(self.F)

    def build(self, name, mats):
        me = bpy.data.meshes.new(name)
        me.from_pydata([tuple(bl(p)) for p in self.P], [], self.F)
        uv = me.uv_layers.new(name="UVMap")
        for poly in me.polygons:
            for li, vi in zip(poly.loop_indices, poly.vertices):
                uv.data[li].uv = self.UV[vi]
        for m in mats:
            me.materials.append(m)
        for poly, mi in zip(me.polygons, self.M):
            poly.material_index = mi
            poly.use_smooth = True
        me.update()
        me.normals_split_custom_set_from_vertices([tuple(bl(n)) for n in self.N])
        return me


# --------------------------------------------------------------------------- shapes

def trunk(mb, mat, height, rows, sides, radius, sway, uv_repeat, metres_per_tile, seed, wobble=0.05):
    """A tapering, swaying trunk: rows x sides quads, normals pointing out and slightly up, a rough outline."""
    rng = random.Random(seed)
    phases = [rng.uniform(0, 2 * math.pi) for _ in range(4)]
    rings = []
    for i in range(rows + 1):
        y = height * i / rows
        cx, cz = sway(y)
        r = radius(y)
        dr = (radius(min(height, y + 0.05)) - radius(max(0.0, y - 0.05))) / 0.1
        ring = []
        for k in range(sides + 1):
            a = 2 * math.pi * k / sides
            rough = 1.0 + wobble * (math.sin(3 * a + phases[0] + y * 1.3) * 0.5 + math.sin(5 * a + phases[1] + y * 2.1) * 0.3
                                    + math.sin(2 * a + phases[2] + y * 0.7) * 0.4)
            rr = r * rough
            p = (cx + rr * math.cos(a), y, cz + rr * math.sin(a))
            ring.append(mb.vert(p, (math.cos(a), -dr * 0.8 + 0.15, math.sin(a)), (k / sides * uv_repeat, y / metres_per_tile)))
        rings.append(ring)
    for i in range(rows):
        for k in range(sides):
            mb.quad(rings[i][k], rings[i][k + 1], rings[i + 1][k + 1], rings[i + 1][k], mat)
    return rings


def tube(mb, mat, points, radii, sides, uv_per_metre=1.5):
    """A thin round tube along a polyline of Unity points with tapering radius (a branch)."""
    rings = []
    length_so_far = 0.0
    prev_frame = None
    for j, p in enumerate(points):
        a = Vector(points[max(j - 1, 0)])
        b = Vector(points[min(j + 1, len(points) - 1)])
        t = unit(b - a)
        ref = Vector((0, 1, 0)) if abs(t.y) < 0.9 else Vector((1, 0, 0))
        n1 = unit(ref - t * ref.dot(t)) if prev_frame is None else unit(prev_frame - t * prev_frame.dot(t))
        prev_frame = n1
        n2 = t.cross(n1)
        if j > 0:
            length_so_far += (Vector(p) - Vector(points[j - 1])).length
        ring = []
        for k in range(sides + 1):
            a_k = 2 * math.pi * k / sides
            d = n1 * math.cos(a_k) + n2 * math.sin(a_k)
            ring.append(mb.vert(Vector(p) + d * radii[j], d, (k / sides, length_so_far * uv_per_metre)))
        rings.append(ring)
    for j in range(len(points) - 1):
        for k in range(sides):
            mb.quad(rings[j][k], rings[j][k + 1], rings[j + 1][k + 1], rings[j + 1][k], mat)


def strip(mb, mat, points, side_vecs, half_widths, normal_fn, fold=0.0, u0=0.0, u1=1.0):
    """A cut-out card along a polyline. u runs across (0 left, 1 right), v along (0 at the first point, 1 at the last).
    fold > 0 bends each half down (a V) by that many metres per metre of half-width, as a palm frond hangs."""
    left, right = [], []
    last = len(points) - 1
    for j, p in enumerate(points):
        p = Vector(p)
        s = Vector(side_vecs[j]) * half_widths[j]
        dip = Vector((0, -fold * half_widths[j], 0))
        v = j / last
        lp, rp = p - s + dip, p + s + dip
        left.append(mb.vert(lp, normal_fn(lp), (u0, v)))
        right.append(mb.vert(rp, normal_fn(rp), (u1, v)))
    for j in range(last):
        mb.quad(left[j], right[j], right[j + 1], left[j + 1], mat)


def fold_strip(mb, mat, points, side_vecs, half_widths, normal_fn, fold, u0=0.0, u1=1.0):
    """A palm frond: two half-strips meeting along the rib (u = 0.5), each dipping by `fold` per metre of its width."""
    last = len(points) - 1
    left, mid, right = [], [], []
    for j, p in enumerate(points):
        p = Vector(p)
        s = Vector(side_vecs[j]) * half_widths[j]
        v = j / last
        lp, rp = p - s + Vector((0, -fold * half_widths[j], 0)), p + s + Vector((0, -fold * half_widths[j], 0))
        left.append(mb.vert(lp, normal_fn(lp), (u0, v)))
        mid.append(mb.vert(p, normal_fn(p), ((u0 + u1) * 0.5, v)))
        right.append(mb.vert(rp, normal_fn(rp), (u1, v)))
    for j in range(last):
        mb.quad(left[j], mid[j], mid[j + 1], left[j + 1], mat)
        mb.quad(mid[j], right[j], right[j + 1], mid[j + 1], mat)


def sphere(mb, mat, centre, radius, u=6, v=4):
    rows = []
    for j in range(v + 1):
        phi = math.pi * j / v
        row = []
        for i in range(u + 1):
            theta = 2 * math.pi * i / u
            n = Vector((math.sin(phi) * math.cos(theta), math.cos(phi), math.sin(phi) * math.sin(theta)))
            row.append(mb.vert(Vector(centre) + n * radius, n, (i / u, j / v)))
        rows.append(row)
    for j in range(v):
        for i in range(u):
            mb.quad(rows[j][i], rows[j][i + 1], rows[j + 1][i + 1], rows[j + 1][i], mat)


# --------------------------------------------------------------------------- the pine

BARK, FOLIAGE = 0, 1

# Four pines so a forest is not one tree repeated. "Pine" is the original (C11); the others change the size, the outline, the droop, the
# spacing of the whorls, the lean and the tint. tint multiplies the needle texture in Unity (and in the previews).
PINE_VARIANTS = {
    # classic cone
    "Pine": dict(seed=100, trunk_seed=7, H=10.4, base_r=0.26, reach=2.7, shape=0.85, first=2.3, step=0.62, jit=0.10, droop=0.50, lift=0.10,
                 count=5.0, count_drop=2.2, card=0.25, sway=0.18, spire=2.1,
                 stubs=((0.9, 0.7), (1.35, 2.9), (1.8, 4.6), (2.15, 1.7)), tint=(1.0, 1.0, 1.0), bark_tint=(1.0, 1.0, 1.0)),
    # tall, narrow, dark: a spruce / cypress column with short, upswept branches
    "PineB": dict(seed=211, trunk_seed=19, H=12.6, base_r=0.30, reach=1.8, shape=0.42, first=2.8, step=0.52, jit=0.08, droop=0.20, lift=0.07,
                  count=5.0, count_drop=1.6, card=0.23, sway=0.10, spire=2.6,
                  stubs=((1.0, 1.2), (1.6, 3.8), (2.2, 5.5)), tint=(0.74, 0.90, 0.84), bark_tint=(0.85, 0.85, 0.9)),
    # broad, old, crooked, light yellow-green: few wide, drooping tiers on a long bare trunk with many dead stubs
    "PineC": dict(seed=337, trunk_seed=3, H=8.8, base_r=0.34, reach=3.3, shape=0.50, first=3.0, step=0.80, jit=0.16, droop=0.38, lift=0.20,
                  count=5.0, count_drop=0.6, card=0.31, sway=0.50, spire=1.7,
                  stubs=((0.8, 0.2), (1.2, 2.0), (1.7, 3.6), (2.1, 5.2), (2.5, 0.9), (2.8, 4.2)), tint=(0.95, 1.0, 0.64),
                  bark_tint=(1.05, 0.95, 0.85)),
    # young: small, bushy to the ground, bright
    "PineD": dict(seed=451, trunk_seed=11, H=5.2, base_r=0.20, reach=1.75, shape=0.85, first=1.4, step=0.50, jit=0.08, droop=0.45, lift=0.12,
                  count=4.0, count_drop=1.4, card=0.26, sway=0.08, spire=1.3, stubs=(), tint=(0.86, 1.0, 0.74), bark_tint=(1.0, 1.0, 1.0)),
}


def build_pine(lod, p):
    H = p["H"]

    def radius(y):
        return 0.06 + p["base_r"] * max(0.0, 1.0 - y / H) ** 1.15 + 0.10 * math.exp(-y / 0.45)

    def sway(y):
        f = y / H
        return p["sway"] * math.sin(y * 0.55 + 0.4) * f, p["sway"] * 0.66 * math.sin(y * 0.43 + 2.0) * f

    mb = MeshBuilder()
    rng = random.Random(p["seed"])
    rows, sides = [(24, 14), (11, 8), (4, 5)][lod]
    rows = max(rows, int(rows * H / 10.4)) if lod == 0 else rows
    trunk(mb, BARK, H, rows, sides, radius, sway, 2.0, 1.6, p["trunk_seed"], wobble=0.07 if lod == 0 else 0.03)

    def ball_normal(centre_y):
        return lambda q: Vector((q.x, (q.y - centre_y) * 0.7 + 1.1, q.z))

    first, top = p["first"], H - 0.9
    whorl_ys = []
    y = first
    while y < top:
        whorl_ys.append(y)
        y += p["step"] + p["jit"] * rng.random()
    if lod == 2:
        # a thin trunk and tiers of big radial cards: the silhouette of the tree seen from far away
        fractions = (0.23, 0.356, 0.48, 0.596, 0.712, 0.817, 0.904)
        for tier, f in enumerate(fractions):
            y0 = max(first - 0.6, H * f)
            t = tier / 6.0
            reach = p["reach"] * (1.0 - t) ** p["shape"] + 0.5
            for k in range(7):
                phi = 2 * math.pi * k / 7 + tier * 0.5
                d = Vector((math.cos(phi), 0, math.sin(phi)))
                side = Vector((-math.sin(phi), 0, math.cos(phi)))
                pts = [Vector((0, y0 + 0.2, 0)) + d * 0.1, Vector((0, y0 - 0.9 * (p["droop"] / 0.5) + 0.2 * t, 0)) + d * reach]
                strip(mb, FOLIAGE, pts, [side, side], [0.35 + 0.35 * reach, 1.15 * reach * 0.62], ball_normal(y0 - 1.5))
        return mb
    for wi, y0 in enumerate(whorl_ys):
        t = (y0 - first) / max(0.1, top - first)
        count = max(3, int(round(p["count"] - p["count_drop"] * t)))
        L = p["reach"] * (1.0 - t) ** p["shape"] + 0.55
        cx, cz = sway(y0)
        for b in range(count):
            phi = wi * GOLDEN * 2.2 + b * 2 * math.pi / count + rng.uniform(-0.25, 0.25)
            d = Vector((math.cos(phi), 0, math.sin(phi)))
            side = Vector((-math.sin(phi), 0, math.cos(phi)))
            blen = L * rng.uniform(0.65, 1.15)
            pts = []
            for s in (0.0, 0.34, 0.68, 1.0):
                dy = blen * (p["lift"] * s - p["droop"] * s * s + 0.20 * s ** 3)
                pts.append(Vector((cx, y0, cz)) + d * (radius(y0) * 0.7 + blen * s) + Vector((0, dy, 0)))
            if lod == 0:
                tube(mb, BARK, [pts[0], pts[1], pts[1] + (pts[2] - pts[1]) * 0.5], [0.034 + 0.012 * blen, 0.020, 0.012], 4)
            ext = pts[3] + (pts[3] - pts[2]) * 0.18
            card_pts = [pts[0] + d * 0.15, pts[1], pts[2], ext] if lod == 0 else [pts[0] + d * 0.15, pts[2], ext]
            hw = lambda s_: (p["card"] * blen + 0.30) * (math.sin(math.pi * (0.10 + 0.90 * s_)) ** 0.55)
            n_pts = len(card_pts)
            widths = [hw(j / (n_pts - 1)) * (1.0 if lod == 0 else 1.45) for j in range(n_pts)]
            angles = (0.0, 52.0, -52.0, 85.0) if lod == 0 else (0.0, 62.0, -62.0)
            for ang in angles:
                a = math.radians(ang)
                sv = side * math.cos(a) + Vector((0, 1, 0)) * math.sin(a)
                strip(mb, FOLIAGE, card_pts, [sv] * n_pts, widths, ball_normal(y0 - 1.3))
            if lod == 0:
                for sgn in (-1, 1):
                    sb = pts[1] + (pts[2] - pts[1]) * 0.5
                    sd = unit(d * math.cos(0.9) + side * sgn * math.sin(0.9))
                    slen = blen * 0.40
                    tip = sb + sd * slen + Vector((0, -0.14 * slen, 0))
                    across = Vector((-sd.z, 0.0, sd.x))
                    for ang in (0.0, 70.0):
                        a = math.radians(ang)
                        sv = across * math.cos(a) + Vector((0, 1, 0)) * math.sin(a)
                        strip(mb, FOLIAGE, [sb, tip], [sv, sv], [0.14 * slen + 0.16, 0.08], ball_normal(y0 - 1.3))
    # the leader: a spire of upright cards at the top
    topx, topz = sway(H)
    sp = p["spire"]
    spire_pts = [Vector((topx, H - sp, topz)), Vector((topx, H - sp * 0.48, topz)), Vector((topx, H + 0.15, topz))]
    for k in range(4 if lod == 0 else 3):
        phi = k * math.pi / 2 + 0.4
        sv = Vector((math.cos(phi), 0, math.sin(phi)))
        strip(mb, FOLIAGE, spire_pts, [sv] * 3, [0.55 * sp / 2.1 + 0.1, 0.35, 0.05], ball_normal(H - sp * 1.2))
    if lod == 0:
        # dead branch stubs low on the trunk
        for y0, phi in p["stubs"]:
            d = Vector((math.cos(phi), 0, math.sin(phi)))
            cx, cz = sway(y0)
            base = Vector((cx, y0, cz)) + d * radius(y0) * 0.8
            tube(mb, BARK, [base, base + d * 0.22 + Vector((0, -0.05, 0)), base + d * 0.42 + Vector((0, -0.12, 0))], [0.032, 0.02, 0.012], 4)
    return mb


# --------------------------------------------------------------------------- the palm

# Four coconut palms: "Palm" is the original, B tall and slender, C young and short, D leaning. LOD0 builds every frond from real
# leaflets (groups of four narrow blades, `leaflets` per side along a curved rachis, hanging down in a V and bending at the tip), so the
# crown has the feathery, hanging look of a coconut palm and its shadow breaks up. LOD1 and LOD2 use whole-frond cards.
PALM_VARIANTS = {
    "Palm": dict(seed=9, trunk_seed=21, H=7.4, r=1.0, lean=1.0, twist=0.28, count=17, L=4.0, Lj=0.7, elevs=(70, 50, 32, 16, 2, -14),
                 droop=0.50, fold=0.42, width=0.88, nuts=7, leaflets=12, leaflet=1.10, phi=44.0, hw=0.115,
                 tint=(1.0, 1.0, 1.0), bark_tint=(1.0, 1.0, 1.0)),
    "PalmB": dict(seed=31, trunk_seed=5, H=9.6, r=0.82, lean=1.8, twist=0.38, count=15, L=3.6, Lj=0.6, elevs=(66, 46, 28, 12, -2, -16),
                  droop=0.54, fold=0.36, width=0.74, nuts=4, leaflets=12, leaflet=1.0, phi=48.0, hw=0.105,
                  tint=(0.90, 1.0, 0.82), bark_tint=(1.05, 1.0, 0.95)),
    "PalmC": dict(seed=77, trunk_seed=33, H=4.4, r=1.25, lean=0.5, twist=0.15, count=16, L=2.8, Lj=0.4, elevs=(76, 58, 40, 22, 6, -10),
                  droop=0.42, fold=0.30, width=0.80, nuts=0, leaflets=11, leaflet=0.85, phi=40.0, hw=0.10,
                  tint=(0.92, 1.0, 0.64), bark_tint=(1.0, 0.95, 0.88)),
    "PalmD": dict(seed=58, trunk_seed=14, H=7.8, r=1.0, lean=2.4, twist=0.6, count=15, L=4.2, Lj=0.7, elevs=(68, 48, 30, 14, 0, -16),
                  droop=0.52, fold=0.44, width=0.84, nuts=8, leaflets=12, leaflet=1.10, phi=46.0, hw=0.115,
                  tint=(0.82, 0.94, 0.72), bark_tint=(0.92, 0.92, 0.92)),
}


def build_palm(lod, p):
    H = p["H"]

    def radius(y):
        return p["r"] * (0.17 + 0.09 * max(0.0, 1.0 - y / H) ** 1.4 + 0.17 * math.exp(-y / 0.45))

    def sway(y):
        f = y / H
        return p["lean"] * f * f, p["twist"] * math.sin(y * 0.5) * f

    mb = MeshBuilder()
    rng = random.Random(p["seed"])
    rows, sides = [(22, 12), (8, 6), (3, 4)][lod]
    trunk(mb, BARK, H, max(rows, int(rows * H / 7.4)) if lod == 0 else rows, sides, radius, sway, 1.0, 1.6, p["trunk_seed"],
          wobble=0.03 if lod == 0 else 0.0)
    cx, cz = sway(H)
    top = Vector((cx, H, cz))
    down = Vector((0.0, -1.0, 0.0))

    def frond_normal(q):
        return Vector((q.x - cx, 0.0, q.z - cz)) * 0.35 + Vector((0, 1.0, 0))

    if lod == 0:
        sphere(mb, BARK, top + Vector((0, 0.05, 0)), 0.21 * p["r"] ** 0.5, 8, 5)  # the crown knob where the fronds start
        for k in range(p["nuts"]):  # a cluster of coconuts under the crown
            a = k * 2 * math.pi / max(1, p["nuts"]) + 0.3
            sphere(mb, BARK, top + Vector((math.cos(a) * 0.23, -0.20 - 0.05 * (k % 2), math.sin(a) * 0.23)), 0.12, 6, 4)
    count = (p["count"], max(6, round(p["count"] * 0.85)), max(5, round(p["count"] * 0.5)))[lod]
    elevs = p["elevs"]
    for i in range(count):
        phi = 2 * math.pi * i / count + rng.uniform(-0.12, 0.12)
        elev = math.radians(elevs[i % len(elevs)] + rng.uniform(-6, 6))
        L = p["L"] + rng.random() * p["Lj"]
        d = Vector((math.cos(phi), 0, math.sin(phi)))
        side = Vector((-math.sin(phi), 0, math.cos(phi)))

        def arc(s):
            return top + d * (L * s * math.cos(elev)) + Vector((0, L * (math.sin(elev) * s - p["droop"] * s * s), 0))

        def tangent(s):
            return unit(d * (L * math.cos(elev)) + Vector((0, L * (math.sin(elev) - 2.0 * p["droop"] * s), 0)))

        if lod == 0:
            seg = 7
            pts = [arc(j / seg) for j in range(seg + 1)]
            tube(mb, BARK, pts, [0.045 * (1.0 - 0.8 * j / seg) + 0.008 for j in range(seg + 1)], 3, uv_per_metre=0.04)
            n_leaf = p["leaflets"]
            for m in range(n_leaf):
                s = 0.14 + 0.84 * (m + 0.5) / n_leaf
                b = arc(s)
                T = tangent(s)
                prof = math.sin(math.pi * (0.10 + 0.88 * (m + 0.5) / n_leaf)) ** 0.6
                ell = p["leaflet"] * (0.30 + 0.70 * prof)
                for sg in (-1, 1):
                    ph = math.radians(p["phi"] + rng.uniform(-7, 7))
                    dirv = unit(side * sg * math.cos(ph) + down * math.sin(ph) + T * (0.30 + 0.55 * s))
                    p1 = b + dirv * (ell * 0.5) + down * (ell * 0.04)
                    p2 = b + dirv * ell + down * (ell * 0.32)
                    wv = unit(T - dirv * T.dot(dirv))
                    strip(mb, FOLIAGE, [b, p1, p2], [wv] * 3, [p["hw"]] * 3, frond_normal, u0=0.8, u1=1.0)
        else:
            seg = (6, 4, 2)[lod]
            pts, sides_v, widths = [], [], []
            for j in range(seg + 1):
                s = j / seg
                pts.append(arc(s))
                sides_v.append(side)
                widths.append(p["width"] * (math.sin(math.pi * (0.05 + 0.95 * s)) ** 0.55) + 0.03)
            if lod == 2:
                strip(mb, FOLIAGE, pts, sides_v, widths, frond_normal, u0=0.0, u1=0.8)
            else:
                fold_strip(mb, FOLIAGE, pts, sides_v, widths, frond_normal, p["fold"], u0=0.0, u1=0.8)
    return mb


# --------------------------------------------------------------------------- the sea almond (a broadleaf shade tree)

# A beach and lowland broadleaf for the start of the island, with its large leaves in rosettes at the ends of the branches. "Almond"
# stands in tiers of near-horizontal branches (the umbrella shape of Terminalia catappa); "AlmondB" is a rounder tree whose branches
# rise and arch. A tier is (height as a fraction of H, branches, reach in metres).
ALMOND_VARIANTS = {
    "Almond": dict(seed=5, trunk_seed=2, H=7.4, base_r=0.26, sway=0.16, elev=7.0, droop=0.30, card=1.45,
                   tiers=((0.36, 6, 2.5), (0.50, 7, 3.2), (0.64, 7, 3.3), (0.78, 6, 2.7), (0.92, 4, 1.6)),
                   tint=(1.0, 1.0, 1.0), bark_tint=(1.0, 1.0, 1.0)),
    "AlmondB": dict(seed=17, trunk_seed=9, H=6.0, base_r=0.30, sway=0.30, elev=38.0, droop=0.55, card=1.35,
                    tiers=((0.28, 5, 2.4), (0.42, 6, 3.0), (0.58, 6, 3.0), (0.76, 5, 2.3), (0.93, 4, 1.2)),
                    tint=(0.88, 1.0, 0.78), bark_tint=(0.9, 0.9, 0.95)),
}


def build_almond(lod, p):
    H = p["H"]

    def radius(y):
        return 0.07 + p["base_r"] * max(0.0, 1.0 - y / H) ** 1.2 + 0.09 * math.exp(-y / 0.5)

    def sway(y):
        f = y / H
        return p["sway"] * math.sin(y * 0.7 + 0.5) * f, p["sway"] * 0.6 * math.sin(y * 0.5 + 2.0) * f

    mb = MeshBuilder()
    rng = random.Random(p["seed"])
    rows, sides = [(18, 12), (8, 7), (3, 4)][lod]
    trunk(mb, BARK, H * 0.98, rows, sides, radius, sway, 1.5, 1.4, p["trunk_seed"], wobble=0.06 if lod == 0 else 0.02)
    centre_y = H * 0.62

    def ball(q):
        return Vector((q.x * 0.6, (q.y - centre_y) * 0.45 + 0.9, q.z * 0.6))

    elev = math.radians(p["elev"])
    if lod == 2:
        for tier, (yf, count, reach) in enumerate(p["tiers"][:4]):
            y0 = H * yf
            for k in range(6):
                phi = 2 * math.pi * k / 6 + tier * 0.55
                d = Vector((math.cos(phi), 0, math.sin(phi)))
                side = Vector((-math.sin(phi), 0, math.cos(phi)))
                pts = [Vector((0, y0, 0)) + d * 0.3, Vector((0, y0 + reach * math.sin(elev) * 0.8, 0)) + d * reach * 1.05]
                strip(mb, FOLIAGE, pts, [side, side], [0.8 * p["card"], 1.1 * p["card"]], ball)
        return mb
    for tier, (yf, count, reach) in enumerate(p["tiers"]):
        y0 = H * yf
        cx0, cz0 = sway(y0)
        for b in range(count):
            phi = tier * GOLDEN * 2.6 + b * 2 * math.pi / count + rng.uniform(-0.3, 0.3)
            d = Vector((math.cos(phi), 0, math.sin(phi)))
            side = Vector((-math.sin(phi), 0, math.cos(phi)))
            blen = reach * rng.uniform(0.78, 1.12)
            pts = []
            for s in (0.0, 0.34, 0.68, 1.0):
                pts.append(Vector((cx0, y0, cz0)) + d * (radius(y0) * 0.8 + blen * s) + Vector((0, blen * (math.sin(elev) * s - p["droop"] * s * s * 0.5), 0)))
            if lod == 0:
                tube(mb, BARK, [pts[0], pts[1], pts[2], pts[2] + (pts[3] - pts[2]) * 0.10], [0.075 * (0.6 + 0.5 * blen / 2.6), 0.050, 0.030, 0.018], 5)
            ext = pts[3] + (pts[3] - pts[2]) * 0.30
            card_pts = [pts[0] + d * 0.5, pts[1], pts[2], ext] if lod == 0 else [pts[1], ext]
            n_pts = len(card_pts)
            widths = [(0.45 + 0.55 * math.sin(math.pi * (0.20 + 0.80 * j / (n_pts - 1)))) * p["card"] * (0.6 + 0.4 * blen / 3.0) * (1.0 if lod == 0 else 1.25)
                      for j in range(n_pts)]
            angles = (0.0, 42.0, -42.0, 80.0) if lod == 0 else (0.0, 60.0)
            for ang in angles:
                a = math.radians(ang)
                sv = side * math.cos(a) + Vector((0, 1, 0)) * math.sin(a)
                strip(mb, FOLIAGE, card_pts, [sv] * n_pts, widths, ball)
    return mb


# --------------------------------------------------------------------------- the banana plant

# A banana plant: a fat green pseudo-stem and a crown of big arching leaves torn into ribbons by the wind, and (on the big one) a hanging
# bunch of bananas. "Banana" is a full-grown plant, "BananaB" a young one.
BANANA_VARIANTS = {
    "Banana": dict(seed=3, trunk_seed=8, H=2.2, r=0.17, count=11, L=3.0, Lj=0.4, elevs=(80, 62, 46, 34, 24, 14, 6), droop=0.72, hw=0.44, fold=0.20,
                   bunch=True, tint=(1.0, 1.0, 1.0), bark_tint=(1.0, 1.0, 1.0)),
    "BananaB": dict(seed=12, trunk_seed=4, H=1.5, r=0.11, count=8, L=2.1, Lj=0.3, elevs=(80, 62, 46, 34, 22, 10), droop=0.66, hw=0.34, fold=0.18,
                    bunch=False, tint=(0.92, 1.0, 0.78), bark_tint=(0.95, 1.0, 0.9)),
}


def build_banana(lod, p):
    H = p["H"]

    def radius(y):
        return p["r"] * (1.0 - 0.42 * y / H) + 0.035 * math.exp(-y / 0.3)

    def sway(y):
        return 0.04 * math.sin(y * 1.3) * y / H, 0.03 * math.sin(y * 0.9 + 1.0) * y / H

    mb = MeshBuilder()
    rng = random.Random(p["seed"])
    rows, sides = [(10, 12), (5, 7), (2, 4)][lod]
    trunk(mb, BARK, H, rows, sides, radius, sway, 2.0, 1.1, p["trunk_seed"], wobble=0.04 if lod == 0 else 0.0)
    cx, cz = sway(H)
    top = Vector((cx, H, cz))
    count = (p["count"], max(5, p["count"] - 2), 5)[lod]
    if lod < 2:
        sphere(mb, BARK, top + Vector((0, 0.02, 0)), p["r"] * 0.62, 8, 4)  # the top of the pseudo-stem closes the crown
        tube(mb, BARK, [top, top + Vector((0.02, 0.35, 0.0)), top + Vector((0.04, 0.70, 0.02))], [0.045, 0.034, 0.012], 4)  # the rolled new leaf

    def leaf_normal(q):
        return Vector((q.x - cx, 0.0, q.z - cz)) * 0.25 + Vector((0, 1.0, 0))

    for i in range(count):
        phi = i * GOLDEN + rng.uniform(-0.15, 0.15)
        elev = math.radians(p["elevs"][i % len(p["elevs"])] + rng.uniform(-5, 5))
        L = p["L"] + rng.random() * p["Lj"]
        d = Vector((math.cos(phi), 0, math.sin(phi)))
        side = Vector((-math.sin(phi), 0, math.cos(phi)))
        seg = (7, 4, 2)[lod]
        pts = [top + d * (L * (j / seg) * math.cos(elev)) + Vector((0, L * (math.sin(elev) * (j / seg) - p["droop"] * (j / seg) ** 2), 0))
               for j in range(seg + 1)]
        widths = [p["hw"]] * (seg + 1)
        if lod == 2:
            strip(mb, FOLIAGE, pts, [side] * (seg + 1), widths, leaf_normal)
        else:
            fold_strip(mb, FOLIAGE, pts, [side] * (seg + 1), widths, leaf_normal, p["fold"])
    if lod == 0 and p["bunch"]:
        # the stalk hangs from the crown on one side, with four hands of bananas on its lower half
        phi = 2.1
        d = Vector((math.cos(phi), 0, math.sin(phi)))
        stalk = [top + d * 0.05 + Vector((0, -0.05, 0)), top + d * 0.22 + Vector((0, -0.28, 0)), top + d * 0.30 + Vector((0, -0.55, 0)),
                 top + d * 0.32 + Vector((0, -0.85, 0))]
        tube(mb, BARK, stalk, [0.025, 0.022, 0.020, 0.018], 4)
        for hand in range(4):
            base = stalk[1] + (stalk[3] - stalk[1]) * (hand / 3.0)
            for k in range(6):
                a = k * 2 * math.pi / 6 + hand * 0.6
                out = Vector((math.cos(a), 0, math.sin(a)))
                q0 = base + out * 0.03
                q1 = q0 + out * 0.07 + Vector((0, -0.12, 0))
                q2 = q1 + out * 0.04 + Vector((0, -0.09, 0))
                tube(mb, BARK, [q0, q1, q2], [0.028, 0.032, 0.016], 4)
    return mb


# --------------------------------------------------------------------------- materials, scene, export

def image_material(name, base, alpha=False, normal=None, rough=0.9, tint=(1.0, 1.0, 1.0)):
    """A Cycles material that shows the texture files, for the previews. Unity builds its own M_Tree_* materials."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX_DIR, base))
    tex.image.colorspace_settings.name = 'sRGB'
    if tuple(tint) == (1.0, 1.0, 1.0):
        nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    else:  # the same multiply that Unity's _BaseColor does
        mix = nt.nodes.new("ShaderNodeMix")
        mix.data_type, mix.blend_type = 'RGBA', 'MULTIPLY'
        mix.inputs["Factor"].default_value = 1.0
        mix.inputs["B"].default_value = (tint[0], tint[1], tint[2], 1.0)
        nt.links.new(tex.outputs["Color"], mix.inputs["A"])
        nt.links.new(mix.outputs["Result"], bsdf.inputs["Base Color"])
    if alpha:
        nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
    if normal:
        ntex = nt.nodes.new("ShaderNodeTexImage")
        ntex.image = bpy.data.images.load(os.path.join(TEX_DIR, normal))
        ntex.image.colorspace_settings.name = 'Non-Color'
        nmap = nt.nodes.new("ShaderNodeNormalMap")
        nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
        nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


def _tree_table():
    families = (
        ("Pine", PINE_VARIANTS, build_pine, "PineBark", "T_PineBark", "PineNeedles", "T_PineNeedles"),
        ("Palm", PALM_VARIANTS, build_palm, "PalmBark", "T_PalmBark", "PalmFrond", "T_PalmFrond"),
        ("Almond", ALMOND_VARIANTS, build_almond, "AlmondBark", "T_AlmondBark", "AlmondLeaves", "T_BroadLeaves"),
        ("Banana", BANANA_VARIANTS, build_banana, "BananaStem", "T_BananaStem", "BananaLeaf", "T_BananaLeaf"),
    )
    table = {}
    for family, variants, builder, bark_name, bark_tex, leaf_name, leaf_tex in families:
        for key, p in variants.items():
            suffix = "_" + key[len(family):] if len(key) > len(family) else ""
            table[key] = dict(build=builder, p=p, bark=("M_Tree_" + bark_name + suffix, bark_tex + "_BaseColor.png", bark_tex + "_Normal.png"),
                              foliage=("M_Tree_" + leaf_name + suffix, leaf_tex + "_BaseColor.png"))
    return table


TREES = _tree_table()


def build(kind):
    cfg = TREES[kind]
    p = cfg["p"]
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit_settings = bpy.context.scene.unit_settings
    unit_settings.system, unit_settings.scale_length, unit_settings.length_unit = 'METRIC', 1.0, 'METERS'
    mats = [image_material(cfg["bark"][0], cfg["bark"][1], normal=cfg["bark"][2], rough=0.95, tint=p["bark_tint"]),
            image_material(cfg["foliage"][0], cfg["foliage"][1], alpha=True, rough=0.85, tint=p["tint"])]
    objs = []
    for lod in range(3):
        mb = cfg["build"](lod, p)
        me = mb.build(f"{kind}_LOD{lod}", mats)
        ob = bpy.data.objects.new(f"{kind}_LOD{lod}", me)
        bpy.context.scene.collection.objects.link(ob)
        objs.append(ob)
        print(f"[{kind}] LOD{lod}: {mb.triangles()} triangles, {len(mb.P)} vertices")
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for v in objs[0].data.vertices:
        u = Vector((v.co.x, v.co.z, v.co.y))
        for i in range(3):
            lo[i], hi[i] = min(lo[i], u[i]), max(hi[i], u[i])
    print(f"[{kind}] LOD0 size {hi.x - lo.x:.2f} x {hi.y - lo.y:.2f} x {hi.z - lo.z:.2f} m, x {lo.x:.2f}..{hi.x:.2f}, y {lo.y:.2f}..{hi.y:.2f}, z {lo.z:.2f}..{hi.z:.2f}")
    blend = os.path.join(REPO, "Art", "Blender", f"SM_Tree_{kind}.blend")
    fbx = os.path.join(REPO, "Assets", "Models", "Custom", f"SM_Tree_{kind}.fbx")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    for ob in bpy.data.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    os.makedirs(os.path.dirname(fbx), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE',
        path_mode='AUTO', embed_textures=False, bake_anim=False, add_leaf_bones=False, use_custom_props=False)
    print(f"[{kind}] {blend}")
    print(f"[{kind}] {fbx}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for kind in (argv or list(TREES)):
        build(kind)


if __name__ == "__main__":
    main()
