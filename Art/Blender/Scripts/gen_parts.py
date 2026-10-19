"""Hard-surface part builders for the generator (step C6).

Owner: Core Developer. Imported by build_generator.py; not run on its own.

Coordinates: every builder takes UNITY-frame numbers (x = along the machine, control face at +X; y = up;
z = across), because the prefab sockets and docs/reference/core-developer.md use that frame. B() turns a Unity
point into the Blender point that the FBX exporter (-Z forward, Y up) maps back to the same Unity point.
Measured in Unity 6000.3 (step C6, not guessed): Unity = (x_blender, z_blender, y_blender), so
    Blender = (x_unity, z_unity, y_unity)
(an axis swap, which also flips handedness, so the model is not mirrored).
Each builder makes a small bmesh, bevels its sharp edges, and registers it as a Part. build_generator.py then
merges all parts into one mesh, so one bevel width per part keeps small parts from collapsing.
"""

import math

import bmesh
from mathutils import Matrix, Vector

MAT_BODY, MAT_METAL, MAT_DETAILS = 0, 1, 2
BEVEL_ANGLE = math.radians(35.0)

PARTS = []
PART_IDS = {}


def B(v):
    """Unity-frame point or direction -> Blender-frame."""
    return Vector((v[0], v[2], v[1]))


class Part:
    def __init__(self, name, pid, mat, verts, faces, masks1, masks2, decal, uvscale):
        self.name = name
        self.pid = pid
        self.mat = mat
        self.verts = verts
        self.faces = faces
        self.masks1 = masks1  # R warning decal, G label decal, B big gauge dial, A small gauge dial
        self.masks2 = masks2  # R red button, G blue button, B red valve wheel, A copper
        self.decal = decal  # (origin_u, normal_u, size_u, size_v) -> DecalUV, or None
        self.uvscale = uvscale  # texel-density multiplier for small, detailed parts


def reset():
    PARTS.clear()
    PART_IDS.clear()


def add_part(bm, name, mat, bevel=0.0, seg=2, masks1=(0, 0, 0, 0), masks2=(0, 0, 0, 0), decal=None, uvscale=1.0, closed=True):
    if closed:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0.0:
        bm.edges.ensure_lookup_table()
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > BEVEL_ANGLE]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET', segments=seg, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
    if decal is not None:
        origin, normal, size_u, size_v = decal
        decal = (B(origin), B(normal).normalized(), size_u, size_v)  # stored in Blender frame, rotated with the part
    bm.verts.index_update()  # bevel adds vertices; their indices are stale until this call
    bm.verts.ensure_lookup_table()
    verts = [v.co.copy() for v in bm.verts]
    faces = [[v.index for v in f.verts] for f in bm.faces]
    pid = len(PARTS)
    PART_IDS[name] = pid
    PARTS.append(Part(name, pid, mat, verts, faces, masks1, masks2, decal, uvscale))
    bm.free()


# --------------------------------------------------------------------------- primitives

def _axis_matrix(axis):
    """Rotation that takes a Blender +Z primitive to the Unity axis 'x', 'y' or 'z'."""
    if axis == 'x':
        return Matrix.Rotation(math.radians(90.0), 4, 'Y')
    if axis == 'z':
        return Matrix.Rotation(math.radians(-90.0), 4, 'X')
    return Matrix.Identity(4)


def _place(bm, axis, c):
    bmesh.ops.transform(bm, matrix=_axis_matrix(axis), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(c), verts=bm.verts)


def box(name, mat, c, s, **kw):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(s[0], s[2], s[1]), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(c), verts=bm.verts)
    add_part(bm, name, mat, **kw)


def cylinder(name, mat, c, axis, r, length, segs=24, r2=None, **kw):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                          radius2=r if r2 is None else r2, depth=length)
    _place(bm, axis, c)
    add_part(bm, name, mat, **kw)


def annulus(name, mat, c, axis, r_in, r_out, length, segs=32, **kw):
    bm = bmesh.new()
    h = length / 2.0
    rings = []
    for z in (-h, h):
        for r in (r_in, r_out):
            rings.append([bm.verts.new((r * math.cos(2 * math.pi * i / segs), r * math.sin(2 * math.pi * i / segs), z))
                          for i in range(segs)])
    bot_in, bot_out, top_in, top_out = rings
    for i in range(segs):
        j = (i + 1) % segs
        bm.faces.new((bot_out[i], bot_out[j], top_out[j], top_out[i]))
        bm.faces.new((bot_in[j], bot_in[i], top_in[i], top_in[j]))
        bm.faces.new((top_in[i], top_in[j], top_out[j], top_out[i]))
        bm.faces.new((bot_in[j], bot_in[i], bot_out[i], bot_out[j]))
    _place(bm, axis, c)
    add_part(bm, name, mat, **kw)


def torus(name, mat, c, axis, major, minor, maj_segs=24, min_segs=8, **kw):
    bm = bmesh.new()
    grid = []
    for i in range(maj_segs):
        a = 2 * math.pi * i / maj_segs
        row = []
        for j in range(min_segs):
            b = 2 * math.pi * j / min_segs
            rr = major + minor * math.cos(b)
            row.append(bm.verts.new((rr * math.cos(a), rr * math.sin(a), minor * math.sin(b))))
        grid.append(row)
    for i in range(maj_segs):
        for j in range(min_segs):
            i2, j2 = (i + 1) % maj_segs, (j + 1) % min_segs
            bm.faces.new((grid[i][j], grid[i2][j], grid[i2][j2], grid[i][j2]))
    _place(bm, axis, c)
    add_part(bm, name, mat, **kw)


def ellipsoid(name, mat, c, s, u=24, v=12, **kw):
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=u, v_segments=v, radius=1.0)
    bmesh.ops.scale(bm, vec=(s[0], s[2], s[1]), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(c), verts=bm.verts)
    add_part(bm, name, mat, **kw)


def prism(name, mat, pts, along, a0, a1, **kw):
    """A 2D polygon extruded between a0 and a1 along the Unity axis 'x', 'y' or 'z'.
    pts are (y,z) for 'x', (x,z) for 'y', (x,y) for 'z'."""
    def to_u(p, a):
        if along == 'x':
            return (a, p[0], p[1])
        if along == 'y':
            return (p[0], a, p[1])
        return (p[0], p[1], a)

    bm = bmesh.new()
    v0 = [bm.verts.new(B(to_u(p, a0))) for p in pts]
    v1 = [bm.verts.new(B(to_u(p, a1))) for p in pts]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((v0[i], v0[j], v1[j], v1[i]))
    bm.faces.new(v0)
    bm.faces.new(list(reversed(v1)))
    add_part(bm, name, mat, **kw)


def tube(name, mat, path, r, sides=12, **kw):
    """Round pipe swept along a polyline of Unity points (bends are approximated by the caller's points)."""
    pts = [B(p) for p in path]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    prev_n = None
    for i in range(n):
        a = pts[max(i - 1, 0)]
        b = pts[min(i + 1, n - 1)]
        t = (b - a).normalized()
        if prev_n is None:
            ref = Vector((0, 0, 1)) if abs(t.z) < 0.9 else Vector((1, 0, 0))
            nrm = (ref - t * ref.dot(t)).normalized()
        else:
            nrm = (prev_n - t * prev_n.dot(t)).normalized()
        prev_n = nrm
        bin_ = t.cross(nrm)
        ring = []
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            ring.append(bm.verts.new(pts[i] + r * (math.cos(ang) * nrm + math.sin(ang) * bin_)))
        rings.append(ring)
    for i in range(n - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            bm.faces.new((rings[i][k], rings[i][k2], rings[i + 1][k2], rings[i + 1][k]))
    bm.faces.new(list(reversed(rings[0])))
    bm.faces.new(rings[-1])
    add_part(bm, name, mat, **kw)


def rotate_last(axis_u, deg, pivot_u):
    """Rotate the part that was just added about a Unity axis through a Unity point."""
    part = PARTS[-1]
    pivot = B(pivot_u)
    m = Matrix.Translation(pivot) @ Matrix.Rotation(math.radians(deg), 4, B(axis_u).normalized()) @ Matrix.Translation(-pivot)
    part.verts = [m @ v for v in part.verts]
    if part.decal is not None:
        origin, normal, size_u, size_v = part.decal
        part.decal = (m @ origin, (m.to_3x3() @ normal).normalized(), size_u, size_v)


def arc_points(center_yz, radius, a0, a1, steps, x):
    """Points (x, y, z) on an arc in the plane of constant x. Angle is measured from +z toward +y, in degrees."""
    out = []
    for i in range(steps + 1):
        a = math.radians(a0 + (a1 - a0) * i / steps)
        out.append((x, center_yz[0] + radius * math.sin(a), center_yz[1] + radius * math.cos(a)))
    return out
