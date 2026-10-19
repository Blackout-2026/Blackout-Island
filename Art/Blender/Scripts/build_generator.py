"""Builds SM_Generator.blend: the realistic generator "G-1" (step C6). Owner: Core Developer.

Run (Blender closed or not, it uses its own process):
    "D:\\Blender\\blender.exe" -b -P Art/Blender/Scripts/build_generator.py

What it does, in order:
 1. build_parts():   every part of the machine, in Unity-frame metres (see gen_parts.py), bevelled.
 2. merge():         one mesh "Generator" with 3 material slots (Body, Metal, Details) + two masks + a decal UV.
 3. cleanup:         remove faces nobody can see, triangulate only the n-gons, smooth by angle.
 4. unwrap:          smart UV project, equal texel density, bigger islands for dials and labels, pack into 0..1.
 5. separate meshes: GlassTube (M_Generator_Glass) and EnergyCore (M_Generator_Core).
 6. checks:          bounding box must be 6.96 x 4.10 x 2.90 m (the original brief); prints triangles and texel density.
 7. saves Art/Blender/SM_Generator.blend (the bake step continues from that file).
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import gen_parts as P  # noqa: E402
from gen_parts import (MAT_BODY, MAT_DETAILS, MAT_METAL, annulus, arc_points, box, cylinder, ellipsoid,  # noqa: E402
                       prism, rotate_last, torus, tube)

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Generator.blend")

# The original brief (docs/reference/core-developer.md 2.2). The model must keep these numbers.
TARGET_SIZE = (6.96, 4.10, 2.90)  # Unity x (length), y (height), z (depth)
TARGET_X = (-3.53, 3.43)

PLATE_TOP = 0.30
AXIS_Y = 1.35  # height of the alternator axis
FACE_X = 3.15  # the control face plane
TEXTURE_SIZE = 2048


def ring_pts(radius, a0, a1, steps, cy=AXIS_Y):
    return [(cy + radius * math.sin(math.radians(a0 + (a1 - a0) * i / steps)),
             radius * math.cos(math.radians(a0 + (a1 - a0) * i / steps))) for i in range(steps + 1)]


def build_parts():
    P.reset()

    # ---------------------------------------------------------------- base plate, bolts, feet
    box("Plate", MAT_METAL, (-0.05, 0.15, 0.0), (6.96, 0.30, 2.90), bevel=0.03)
    for x in (-3.30, -1.90, 3.25):
        for z in (-1.30, 1.30):
            cylinder(f"PlateBolt_{x}_{z}", MAT_METAL, (x, PLATE_TOP + 0.03, z), 'y', 0.07, 0.06, segs=6)
    for x in (-2.60, -0.90, 0.80):  # wedge brackets against the housing, back side
        prism(f"FootBack_{x}", MAT_METAL, [(PLATE_TOP, -1.18), (PLATE_TOP, -1.40), (0.52, -1.18)], 'x', x - 0.25, x + 0.25, bevel=0.01, seg=1)
    for x in (0.95, 2.05):  # front side, beside the glass section
        prism(f"FootFront_{x}", MAT_METAL, [(PLATE_TOP, 1.18), (PLATE_TOP, 1.40), (0.52, 1.18)], 'x', x - 0.25, x + 0.25, bevel=0.01, seg=1)

    # ---------------------------------------------------------------- vaulted housing
    profile = [(PLATE_TOP, -1.25), (PLATE_TOP, 1.25)] + ring_pts(1.25, 0, 180, 24, cy=1.40)
    prism("Housing", MAT_BODY, profile, 'x', -3.20, 0.40, bevel=0.03)
    for x0, x1, name in ((-3.215, -3.06, "BandBack"), (0.28, 0.43, "BandFront")):  # not coplanar with the housing caps
        band = [(PLATE_TOP, -1.31), (PLATE_TOP, 1.31)] + ring_pts(1.31, 0, 180, 24, cy=1.40)
        prism(name, MAT_BODY, band, 'x', x0, x1, bevel=0.015, seg=1)

    # top hatch with hinges and handle
    prism("Hatch", MAT_BODY, [(-2.50, 2.48), (-1.10, 2.48), (-1.25, 2.82), (-2.35, 2.82)], 'z', -0.55, 0.55, bevel=0.02)
    for z in (-0.32, 0.32):
        cylinder(f"HatchHinge_{z}", MAT_METAL, (-2.50, 2.50, z), 'z', 0.04, 0.20, segs=10, bevel=0.005, seg=1)
    box("HatchHandle", MAT_METAL, (-1.55, 2.86, 0.0), (0.30, 0.04, 0.05), bevel=0.01, seg=1)
    for x in (-1.75, -1.35):
        box(f"HatchHandlePost_{x}", MAT_METAL, (x, 2.84, 0.0), (0.04, 0.05, 0.05), bevel=0.008, seg=1)

    # awning above the label, label and warning decal plates (tilted to follow the curved wall)
    prism("Awning", MAT_BODY, [(1.97, 1.09), (1.97, 1.38), (2.13, 1.09)], 'x', -2.65, -0.55, bevel=0.012, seg=1)
    wall_tilt = math.degrees(math.asin((1.62 - 1.40) / 1.25))
    wz = math.sqrt(1.25 ** 2 - (1.62 - 1.40) ** 2)
    box("LabelDecal", MAT_DETAILS, (-1.60, 1.62, wz + 0.008), (1.00, 0.125, 0.02), masks1=(0, 1, 0, 0),
        decal=((-1.60, 1.62, wz + 0.02), (0, 0, 1), 1.00, 0.125), uvscale=3.5)
    rotate_last((1, 0, 0), wall_tilt, (-1.60, 1.62, wz))
    box("WarnDecal", MAT_DETAILS, (-1.50, 1.62, -(wz + 0.008)), (0.34, 0.34, 0.02), masks1=(1, 0, 0, 0),
        decal=((-1.50, 1.62, -(wz + 0.02)), (0, 0, -1), 0.34, 0.34), uvscale=3.0)
    rotate_last((1, 0, 0), -wall_tilt, (-1.50, 1.62, -wz))

    # bank of five louvred vent boxes on the front (+z) side
    vent_profile = [(PLATE_TOP, 1.18), (PLATE_TOP, 1.40), (1.10, 1.40), (1.30, 1.18)]
    for i in range(5):
        xc = -2.725 + i * 0.625
        prism(f"Vent_{i}", MAT_BODY, vent_profile, 'x', xc - 0.275, xc + 0.275, bevel=0.015, seg=1)
        for k in range(10):
            yk = 0.44 + k * 0.062
            box(f"Slat_{i}_{k}", MAT_BODY, (xc, yk, 1.395), (0.47, 0.034, 0.07))
            rotate_last((1, 0, 0), 28.0, (xc, yk, 1.395))
        box(f"VentFrame_{i}", MAT_METAL, (xc, 0.34, 1.405), (0.56, 0.05, 0.03), bevel=0.006, seg=1)

    # pipes, brackets and a cable conduit on the back (-z) side
    for y in (1.00, 0.65):
        tube(f"Pipe_{y}", MAT_METAL, [(-3.00, y, -1.36), (0.20, y, -1.36)], 0.06, sides=12)
    for x in (-2.50, -1.20):
        box(f"PipeBracket_{x}", MAT_METAL, (x, 0.825, -1.31), (0.07, 0.50, 0.12), bevel=0.01, seg=1)
    arc = arc_points((1.63, -1.22), 0.12, 90, 180, 5, x=-3.10)
    tube("Conduit", MAT_METAL, [(-3.10, 1.75, -1.15), (-3.10, 1.75, -1.22)] + arc[1:] + [(-3.10, 0.50, -1.34), (-3.10, PLATE_TOP, -1.34)],
         0.08, sides=12)
    cylinder("ConduitFlange", MAT_METAL, (-3.10, 0.34, -1.34), 'y', 0.105, 0.04, segs=12)
    for x in [-3.0 + 0.41 * i for i in range(8)]:  # rivet rows
        for y in (0.42, 1.32):
            cylinder(f"Rivet_{x:.2f}_{y}", MAT_METAL, (x, y, -1.255), 'z', 0.03, 0.03, segs=6)

    # ---------------------------------------------------------------- exhaust stack
    cylinder("StackFlange", MAT_METAL, (-0.10, 2.64, 0.0), 'y', 0.36, 0.08, segs=24, bevel=0.012, seg=1)
    cylinder("StackPipe", MAT_METAL, (-0.10, 3.32, 0.0), 'y', 0.24, 1.30, segs=24)
    for y in (3.05, 3.55):
        cylinder(f"StackCollar_{y}", MAT_METAL, (-0.10, y, 0.0), 'y', 0.30, 0.07, segs=24)
    cylinder("StackCap", MAT_METAL, (-0.10, 4.025, 0.0), 'y', 0.42, 0.15, segs=24, r2=0.24, bevel=0.01, seg=1)
    # NOTE: the cone above spans y 3.95 to 4.10 so the machine is exactly 4.10 m tall.

    # ---------------------------------------------------------------- alternator: rings, coil rods, axle
    for i, x in enumerate((0.45, 0.87, 1.29, 1.71, 2.13, 2.55)):
        end = i in (0, 5)
        annulus(f"Flange_{i}", MAT_BODY if end else MAT_METAL, (x, AXIS_Y, 0.0), 'x', 0.98, 1.20 if end else 1.10,
                0.14 if end else 0.10, segs=32 if end else 24, **({"bevel": 0.012, "seg": 1} if end else {}))
    for k in range(8):
        a = math.radians(45.0 * k)
        cylinder(f"CoilRod_{k}", MAT_DETAILS, (1.50, AXIS_Y + 0.70 * math.sin(a), 0.70 * math.cos(a)), 'x', 0.035, 1.80,
                 segs=8, masks2=(0, 0, 0, 1))
    cylinder("CoreAxle", MAT_METAL, (1.50, AXIS_Y, 0.0), 'x', 0.06, 2.20, segs=12)

    # ---------------------------------------------------------------- control end: nested discs and the face
    for x0, x1, r, name in ((2.60, 2.72, 1.30, "FaceRing_0"), (2.72, 2.84, 1.22, "FaceRing_1"), (2.84, 2.96, 1.14, "FaceRing_2")):
        cylinder(name, MAT_BODY, ((x0 + x1) / 2, AXIS_Y, 0.0), 'x', r, x1 - x0, segs=32, bevel=0.012, seg=1)
    cylinder("FacePlate", MAT_BODY, ((2.96 + FACE_X) / 2, AXIS_Y, 0.0), 'x', 1.05, FACE_X - 2.96, segs=32)
    annulus("FaceRim", MAT_METAL, (FACE_X + 0.02, AXIS_Y, 0.0), 'x', 1.00, 1.08, 0.04, segs=32)
    sector = ring_pts(1.16, 25, 175, 12) + list(reversed(ring_pts(1.10, 25, 175, 12)))
    prism("Hood", MAT_BODY, sector, 'x', FACE_X, 3.40, bevel=0.01, seg=1)
    for k in range(16):  # face bolts, skipping the valve wheel sector
        ang = 22.5 * k
        if 195 <= ang <= 245:
            continue
        a = math.radians(ang)
        cylinder(f"FaceBolt_{k}", MAT_METAL, (FACE_X + 0.01, AXIS_Y + 0.93 * math.sin(a), 0.93 * math.cos(a)), 'x', 0.025, 0.02, segs=6)

    # big gauge (upper right of the face)
    gy, gz = 1.80, 0.20
    annulus("GaugeBigBezel", MAT_METAL, (FACE_X + 0.06, gy, gz), 'x', 0.30, 0.34, 0.12, segs=24)
    cylinder("GaugeBigDial", MAT_DETAILS, (FACE_X + 0.02, gy, gz), 'x', 0.30, 0.04, segs=32, masks1=(0, 0, 1, 0),
             decal=((FACE_X + 0.04, gy, gz), (1, 0, 0), 0.60, 0.60), uvscale=3.0)
    box("GaugeBigNeedle", MAT_METAL, (FACE_X + 0.05, gy + 0.07, gz), (0.006, 0.24, 0.016))
    rotate_last((1, 0, 0), -40.0, (FACE_X + 0.05, gy, gz))
    # small gauge (lower)
    sy, sz = 0.95, 0.25
    annulus("GaugeSmallBezel", MAT_METAL, (FACE_X + 0.05, sy, sz), 'x', 0.17, 0.20, 0.10, segs=20)
    cylinder("GaugeSmallDial", MAT_DETAILS, (FACE_X + 0.02, sy, sz), 'x', 0.17, 0.04, segs=28, masks1=(0, 0, 0, 1),
             decal=((FACE_X + 0.04, sy, sz), (1, 0, 0), 0.34, 0.34), uvscale=3.0)
    box("GaugeSmallNeedle", MAT_METAL, (FACE_X + 0.05, sy + 0.04, sz), (0.006, 0.14, 0.012))
    rotate_last((1, 0, 0), 55.0, (FACE_X + 0.05, sy, sz))

    # button box with 3 red and 3 blue buttons
    bz, by = -0.55, 1.62
    box("ButtonBox", MAT_METAL, (FACE_X + 0.075, by, bz), (0.15, 0.36, 0.40), bevel=0.01, seg=1)
    for row, mask in ((0.08, (1, 0, 0, 0)), (-0.08, (0, 1, 0, 0))):
        for col in (-0.12, 0.0, 0.12):
            cylinder(f"Button_{row}_{col}", MAT_DETAILS, (FACE_X + 0.155, by + row, bz + col), 'x', 0.035, 0.05, segs=12, masks2=mask)

    # copper pipes linking the gauges, and the valve wheel
    tube("CopperPipeA", MAT_DETAILS, [(FACE_X + 0.04, 1.17, 0.25), (FACE_X + 0.04, 1.30, 0.23), (FACE_X + 0.04, 1.47, 0.20)],
         0.016, sides=8, masks2=(0, 0, 0, 1))
    tube("CopperPipeB", MAT_DETAILS, [(FACE_X + 0.04, 1.44, -0.35), (FACE_X + 0.08, 1.44, -0.10), (FACE_X + 0.04, 1.44, 0.04)],
         0.016, sides=8, masks2=(0, 0, 0, 1))
    wy, wz2 = 0.82, -0.62
    torus("ValveWheelRim", MAT_DETAILS, (FACE_X + 0.10, wy, wz2), 'x', 0.15, 0.02, maj_segs=20, min_segs=6, masks2=(0, 0, 1, 0))
    cylinder("ValveWheelHub", MAT_DETAILS, (FACE_X + 0.10, wy, wz2), 'x', 0.04, 0.06, segs=12, masks2=(0, 0, 1, 0))
    for k, ang in enumerate((0.0, 60.0, 120.0)):
        box(f"ValveSpoke_{k}", MAT_DETAILS, (FACE_X + 0.10, wy, wz2), (0.015, 0.02, 0.30), masks2=(0, 0, 1, 0))
        rotate_last((1, 0, 0), ang, (FACE_X + 0.10, wy, wz2))
    cylinder("ValveStem", MAT_METAL, (FACE_X + 0.04, wy, wz2), 'x', 0.02, 0.09, segs=8)

    # two elbow exhaust pipes into the base plate
    for z in (-0.30, 0.30):
        elbow = [(3.10, 0.62, z)] + [(3.17 + 0.16 * math.sin(math.radians(a)), 0.46 + 0.16 * math.cos(math.radians(a)), z)
                                                      for a in range(0, 91, 15)] + [(3.33, 0.30, z)]
        # arc: from heading +x at y=0.62 (a=0) round to heading -y at x=3.33 (a=90)
        tube(f"Elbow_{z}", MAT_METAL, elbow, 0.065, sides=12)
        cylinder(f"ElbowFlange_{z}", MAT_METAL, (3.33, 0.33, z), 'y', 0.095, 0.05, segs=16, bevel=0.008, seg=1)


SKIPPED = []


def merge():
    bm = bmesh.new()
    uv_main = bm.loops.layers.uv.new("UVMap")
    uv_decal = bm.loops.layers.uv.new("DecalUV")
    m1 = bm.loops.layers.float_color.new("Mask1")
    m2 = bm.loops.layers.float_color.new("Mask2")
    pid = bm.faces.layers.int.new("partid")
    z_axis = Vector((0, 0, 1))
    for part in P.PARTS:
        verts = [bm.verts.new(v) for v in part.verts]
        if part.decal is not None:
            origin, normal, su, sv = part.decal
            au = (-normal).cross(z_axis).normalized()
        for idx in part.faces:
            try:
                face = bm.faces.new([verts[i] for i in idx])
            except ValueError:
                SKIPPED.append(part.name)
                continue
            face.material_index = part.mat
            face[pid] = part.pid
            for loop in face.loops:
                loop[m1] = part.masks1
                loop[m2] = part.masks2
                if part.decal is not None:
                    d = loop.vert.co - origin
                    loop[uv_decal].uv = (0.5 + d.dot(au) / su, 0.5 + d.dot(z_axis) / sv)
    return bm


def hidden_faces(bm, samples=18):
    """Faces that no outside viewer can see: every ray over the face's hemisphere hits another face (or the floor).
    The glass tube is not in this test on purpose, so the coil rods and the housing end seen through it stay."""
    from mathutils.bvhtree import BVHTree
    bm.normal_update()
    probe = bm.copy()
    ground = [probe.verts.new((x, y, -0.001)) for x, y in ((-20, -20), (20, -20), (20, 20), (-20, 20))]
    probe.faces.new(ground)
    tree = BVHTree.FromBMesh(probe)
    probe.free()
    golden = math.pi * (3.0 - math.sqrt(5.0))
    local = []
    for i in range(samples):
        elev = math.radians(10.0) + (math.pi / 2 - math.radians(10.0)) * (i + 0.5) / samples
        ang = i * golden
        local.append(Vector((math.cos(elev) * math.cos(ang), math.cos(elev) * math.sin(ang), math.sin(elev))))
    local.reverse()  # most upright rays first: a visible face usually escapes on the first one
    hidden = []
    for f in bm.faces:
        n = f.normal
        if n.length < 0.5:
            continue
        # Sample the centre and the corners (pulled in 20%), so a decal covering only the middle of a long wall does not hide it.
        center = f.calc_center_median()
        step = max(1, len(f.verts) // 8)
        points = [center] + [center + (v.co - center) * 0.8 for v in list(f.verts)[::step]]
        rot = n.to_track_quat('Z', 'Y')
        rays = [rot @ d for d in local]
        covered = True
        for point in points:
            origin = point + n * 0.002
            if any(tree.ray_cast(origin, ray)[0] is None for ray in rays):
                covered = False
                break
        if covered:
            hidden.append(f)
    return hidden


def cleanup(bm):
    removed = hidden_faces(bm)
    bmesh.ops.delete(bm, geom=removed, context='FACES')
    ngons = [f for f in bm.faces if len(f.verts) > 4]
    bmesh.ops.triangulate(bm, faces=ngons, quad_method='BEAUTY', ngon_method='EAR_CLIP')
    bm.edges.ensure_lookup_table()
    for e in bm.edges:
        e.smooth = not (len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(30.0))
    for f in bm.faces:
        f.smooth = True
    return len(removed), len(ngons)


def make_material(name, color, metallic=0.0, rough=0.5, emission=None):
    mat = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    bsdf.inputs["Base Color"].default_value = color
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    if name == "M_Generator_Glass":
        bsdf.inputs["Transmission Weight"].default_value = 1.0
        bsdf.inputs["IOR"].default_value = 1.45
    if emission is not None:
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = 4.0
    return mat


def unwrap_body(obj):
    scene = bpy.context.scene
    scene.tool_settings.use_uv_select_sync = True
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    me = obj.data
    me.uv_layers.active_index = 0
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.0, scale_to_bounds=False)
    bpy.ops.uv.average_islands_scale()
    bm = bmesh.from_edit_mesh(me)
    uv = bm.loops.layers.uv["UVMap"]
    pid = bm.faces.layers.int["partid"]
    scale_for = {p.pid: p.uvscale for p in P.PARTS if abs(p.uvscale - 1.0) > 1e-6}
    groups = {}
    for f in bm.faces:
        if f[pid] in scale_for:
            groups.setdefault(f[pid], []).append(f)
    for part_id, faces in groups.items():
        loops = [l for f in faces for l in f.loops]
        cx = sum(l[uv].uv.x for l in loops) / len(loops)
        cy = sum(l[uv].uv.y for l in loops) / len(loops)
        k = scale_for[part_id]
        for l in loops:
            l[uv].uv = (cx + (l[uv].uv.x - cx) * k, cy + (l[uv].uv.y - cy) * k)
    bmesh.update_edit_mesh(me)
    bpy.ops.uv.pack_islands(rotate=True, margin=0.002)
    bpy.ops.object.mode_set(mode='OBJECT')


def unwrap_simple(obj):
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')


def texel_density(obj):
    me = obj.data
    uv = me.uv_layers["UVMap"].data
    area3d = 0.0
    area_uv = 0.0
    for poly in me.polygons:
        area3d += poly.area
        pts = [uv[i].uv for i in poly.loop_indices]
        a = 0.0
        for i in range(len(pts)):
            j = (i + 1) % len(pts)
            a += pts[i].x * pts[j].y - pts[j].x * pts[i].y
        area_uv += abs(a) / 2.0
    return area3d, area_uv, math.sqrt(area_uv / area3d) * TEXTURE_SIZE


def unity_bounds(objs):
    lo = Vector((1e9, 1e9, 1e9))
    hi = Vector((-1e9, -1e9, -1e9))
    for o in objs:
        for v in o.data.vertices:
            p = o.matrix_world @ v.co
            u = Vector((p.x, p.z, p.y))  # Blender -> Unity (measured, see gen_parts.py)
            for i in range(3):
                lo[i] = min(lo[i], u[i])
                hi[i] = max(hi[i], u[i])
    return lo, hi


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'

    body_mat = make_material("M_Generator_Body", (0.80, 0.22, 0.03, 1), 0.35, 0.45)
    metal_mat = make_material("M_Generator_Metal", (0.05, 0.05, 0.055, 1), 0.85, 0.5)
    detail_mat = make_material("M_Generator_Details", (0.80, 0.78, 0.70, 1), 0.2, 0.5)
    glass_mat = make_material("M_Generator_Glass", (0.6, 0.85, 0.9, 1), 0.0, 0.05)
    core_mat = make_material("M_Generator_Core", (0.05, 0.4, 0.6, 1), 0.0, 0.3, emission=(0.2, 0.8, 1.0, 1))

    build_parts()
    bm = merge()
    removed, ngons = cleanup(bm)
    me = bpy.data.meshes.new("Generator")
    bm.to_mesh(me)
    bm.free()
    for m in (body_mat, metal_mat, detail_mat):
        me.materials.append(m)
    gen = bpy.data.objects.new("Generator", me)
    bpy.context.scene.collection.objects.link(gen)
    unwrap_body(gen)

    # glass tube and energy core: own meshes, own materials (the blackout swaps the core)
    gb = bmesh.new()
    bmesh.ops.create_cone(gb, cap_ends=False, segments=48, radius1=1.0, radius2=1.0, depth=2.20)
    bmesh.ops.transform(gb, matrix=__import__("mathutils").Matrix.Rotation(math.radians(90), 4, 'Y'), verts=gb.verts)
    bmesh.ops.translate(gb, vec=P.B((1.50, AXIS_Y, 0.0)), verts=gb.verts)
    for f in gb.faces:
        f.smooth = True
    gme = bpy.data.meshes.new("GlassTube")
    gb.to_mesh(gme)
    gb.free()
    gme.materials.append(glass_mat)
    glass = bpy.data.objects.new("GlassTube", gme)
    bpy.context.scene.collection.objects.link(glass)
    unwrap_simple(glass)

    cb = bmesh.new()
    bmesh.ops.create_uvsphere(cb, u_segments=24, v_segments=12, radius=1.0)
    bmesh.ops.scale(cb, vec=(0.85, 0.42, 0.42), verts=cb.verts)
    bmesh.ops.translate(cb, vec=P.B((1.50, AXIS_Y, 0.0)), verts=cb.verts)
    for f in cb.faces:
        f.smooth = True
    cme = bpy.data.meshes.new("EnergyCore")
    cb.to_mesh(cme)
    cb.free()
    cme.materials.append(core_mat)
    core = bpy.data.objects.new("EnergyCore", cme)
    bpy.context.scene.collection.objects.link(core)
    unwrap_simple(core)

    lo, hi = unity_bounds((gen, glass, core))
    size = hi - lo
    print("=" * 70)
    print(f"[Generator] size (Unity x,y,z): {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   target {TARGET_SIZE[0]} x {TARGET_SIZE[1]} x {TARGET_SIZE[2]}")
    print(f"[Generator] x range {lo.x:.3f} .. {hi.x:.3f}   target {TARGET_X[0]} .. {TARGET_X[1]}   y min {lo.y:.3f}")
    for i, t in enumerate(TARGET_SIZE):
        if abs(size[i] - t) > 0.02:
            print(f"[Generator] WARNING axis {i} is {size[i]:.3f}, expected {t}")
    tris = 0
    for o in (gen, glass, core):
        o.data.calc_loop_triangles()
        n = len(o.data.loop_triangles)
        tris += n
        print(f"[Generator] {o.name}: {len(o.data.vertices)} verts, {len(o.data.polygons)} faces, {n} triangles, "
              f"materials {[m.name for m in o.data.materials]}")
    print(f"[Generator] faces skipped while merging: {len(SKIPPED)} {sorted(set(SKIPPED))[:8]}")
    print(f"[Generator] total triangles {tris} (target 10,000, max 15,000); parts {len(P.PARTS)}; faces culled {removed}; n-gons triangulated {ngons}")
    a3, auv, px = texel_density(gen)
    print(f"[Generator] body surface {a3:.1f} m2, UV area used {auv:.2f}, average texel density {px:.0f} px/m at {TEXTURE_SIZE}")

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    print(f"[Generator] saved {BLEND_PATH}")


main()
