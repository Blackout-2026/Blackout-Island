"""Builds SM_Weapon_Smg.fbx, the realistic MP-9 viewmodel with hands (step C7). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_smg.py

Style reference: a modern 9 mm carbine with a flat-top rail, KeyMod handguard, curved magazine, angled grip, adjustable stock
and a compact red dot (Art/Reference/Smg/). Our own mesh, not traced.

Frame: every number is a UNITY-frame metre value in the gun frame of docs/art-pipeline.md section 14 (origin at the receiver
centre, x right, y up, +z the way the muzzle points). B() converts it to the Blender point that the FBX exporter
(-Z forward, Y up) maps back to the same Unity point, exactly as gen_parts.py does for the generator.

Output: Art/Blender/SM_Weapon_Smg.blend and Assets/Models/Custom/SM_Weapon_Smg.fbx. Three material slots only
(docs/art-pipeline.md section 6) once bake_smg.py has run: M_Weapon_Smg (the gun, baked textures), M_Weapon_Glove (the gloved hands)
and M_Weapon_Camo (the sleeves, T_WeaponSleeve_BaseColor). Before the bake the gun still has the palette slots M_Weapon_Metal and M_Weapon_Polymer. The muzzle socket and the muzzle flash
are made by WeaponPrefabBuilder, so the FBX holds no empties and a model swap never loses them.
Budget: 4,000 triangles target, 6,000 maximum (a viewmodel fills the screen, so it gets more than a prop).
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
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Weapon_Smg.blend")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Weapon_Smg.fbx")
B = P.B

METAL, POLYMER, SLEEVE, GLOVE, TAN = 0, 1, 2, 3, 4
SLOTS = ["M_Weapon_Metal", "M_Weapon_Polymer", "M_Weapon_Camo", "M_Weapon_Glove", "M_Weapon_Tan"]
COLOURS = {"M_Weapon_Metal": ((0.12, 0.125, 0.14, 1), 0.85, 0.42), "M_Weapon_Polymer": ((0.035, 0.037, 0.04, 1), 0.1, 0.55),
           "M_Weapon_Camo": ((0.15, 0.17, 0.12, 1), 0.0, 0.9), "M_Weapon_Glove": ((0.28, 0.20, 0.10, 1), 0.0, 0.7),
           "M_Weapon_Tan": ((0.30, 0.24, 0.15, 1), 0.0, 0.5)}
BEVEL_ANGLE = math.radians(35.0)
MUZZLE_Z = 0.33  # WeaponDefinitions.Smg socket, and the frontmost point of the model

OBJECTS = {}  # object name -> list of (verts, faces, slot)


# --------------------------------------------------------------------------- tiny kit


def _store(obj, bm, slot, bevel, seg=1):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    if bevel > 0.0:
        bm.edges.ensure_lookup_table()
        edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > BEVEL_ANGLE]
        if edges:
            bmesh.ops.bevel(bm, geom=edges, offset=bevel, offset_type='OFFSET', segments=seg, profile=0.5,
                            affect='EDGES', clamp_overlap=True)
    bm.verts.index_update()
    bm.verts.ensure_lookup_table()
    OBJECTS.setdefault(obj, []).append(([v.co.copy() for v in bm.verts], [[v.index for v in f.verts] for f in bm.faces], slot))
    bm.free()


def _to_unity(v):
    return Vector((v.x, v.z, v.y))


def _xf_last(obj, rot3, pivot_u, move_u=(0, 0, 0)):
    """Rotate the last part of obj by a Unity-frame 3x3 about a Unity point, then move it (Unity frame)."""
    verts, faces, slot = OBJECTS[obj][-1]
    pivot = Vector(pivot_u)
    move = Vector(move_u)
    out = []
    for v in verts:
        u = _to_unity(v) - pivot
        u = rot3 @ u + pivot + move
        out.append(B(u))
    OBJECTS[obj][-1] = (out, faces, slot)


def unity_euler(x_deg, y_deg=0.0, z_deg=0.0):
    """Quaternion.Euler(x, y, z) as a 3x3 in Unity's left-handed frame (z first, then x, then y)."""
    ax, ay, az = (math.radians(a) for a in (x_deg, y_deg, z_deg))
    rx = Matrix(((1, 0, 0), (0, math.cos(ax), -math.sin(ax)), (0, math.sin(ax), math.cos(ax))))
    ry = Matrix(((math.cos(ay), 0, math.sin(ay)), (0, 1, 0), (-math.sin(ay), 0, math.cos(ay))))
    rz = Matrix(((math.cos(az), -math.sin(az), 0), (math.sin(az), math.cos(az), 0), (0, 0, 1)))
    return ry @ rx @ rz


# bmesh makers (Blender frame, built from Unity numbers)

def box_bm(c, s):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.scale(bm, vec=(s[0], s[2], s[1]), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(c), verts=bm.verts)
    return bm


def cyl_bm(c, axis, r, length, segs=16, r2=None):
    bm = bmesh.new()
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r,
                          radius2=r if r2 is None else r2, depth=length)
    bmesh.ops.transform(bm, matrix=P._axis_matrix(axis), verts=bm.verts)
    bmesh.ops.translate(bm, vec=B(c), verts=bm.verts)
    return bm


def prism_bm(pts, x0, x1):
    """A side profile of (z, y) pairs extruded across the gun from x0 to x1."""
    bm = bmesh.new()
    left = [bm.verts.new(B((x0, p[1], p[0]))) for p in pts]
    right = [bm.verts.new(B((x1, p[1], p[0]))) for p in pts]
    n = len(pts)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((left[i], left[j], right[j], right[i]))
    bm.faces.new(left)
    bm.faces.new(list(reversed(right)))
    return bm


def box(obj, slot, c, s, bevel=0.0, tilt=None, seg=1):
    _store(obj, box_bm(c, s), slot, bevel, seg)
    if tilt is not None:
        _xf_last(obj, unity_euler(*tilt), c)


def cyl(obj, slot, c, axis, r, length, segs=16, r2=None, bevel=0.0):
    _store(obj, cyl_bm(c, axis, r, length, segs, r2), slot, bevel)


def prism(obj, slot, pts, x0, x1, bevel=0.0, seg=1):
    _store(obj, prism_bm(pts, x0, x1), slot, bevel, seg)


def tube(obj, slot, path, r, sides=6):
    """A round bar swept along Unity points (the trigger guard)."""
    pts = [B(p) for p in path]
    n = len(pts)
    bm = bmesh.new()
    rings = []
    prev = None
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
    _store(obj, bm, slot, 0.0)


def bool_cut(obj, slot, base_bm, cutters, bevel=0.0):
    """base minus a set of Unity-frame boxes (centre, size): slots, pockets, lightening holes."""
    base = bpy.data.meshes.new("base")
    base_bm.to_mesh(base)
    base_bm.free()
    base_ob = bpy.data.objects.new("base", base)
    bpy.context.scene.collection.objects.link(base_ob)

    cutter_bm = bmesh.new()
    for c, s in cutters:
        sub = box_bm(c, s)
        tmp = bpy.data.meshes.new("t")
        sub.to_mesh(tmp)
        sub.free()
        cutter_bm.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    cm = bpy.data.meshes.new("cut")
    cutter_bm.to_mesh(cm)
    cutter_bm.free()
    cut_ob = bpy.data.objects.new("cut", cm)
    bpy.context.scene.collection.objects.link(cut_ob)

    mod = base_ob.modifiers.new("cuts", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'EXACT'
    mod.object = cut_ob
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(base_ob.evaluated_get(dg))
    bm2 = bmesh.new()
    bm2.from_mesh(mesh)
    bmesh.ops.dissolve_limit(bm2, angle_limit=math.radians(1.0), verts=bm2.verts, edges=bm2.edges)
    bpy.data.objects.remove(base_ob)
    bpy.data.objects.remove(cut_ob)
    bpy.data.meshes.remove(mesh)
    _store(obj, bm2, slot, bevel)


# --------------------------------------------------------------------------- the gun


def build_gun():
    g = "Smg"

    # ---- receiver: upper with a chamfered rear top, lower with a flared magwell
    prism(g, METAL, [(-0.125, -0.028), (0.068, -0.028), (0.068, 0.034), (-0.116, 0.034), (-0.125, 0.025)], -0.024, 0.024,
          bevel=0.0035, seg=2)
    prism(g, METAL, [(-0.118, -0.028), (0.05, -0.028), (0.064, -0.052), (0.06, -0.075), (-0.1, -0.075), (-0.118, -0.058)],
          -0.0225, 0.0225, bevel=0.003)
    # ejection port, bolt face inside it, a side bump, and the left charging handle
    box(g, POLYMER, (0.0243, 0.012, -0.012), (0.002, 0.024, 0.056))
    box(g, METAL, (0.0225, 0.012, -0.03), (0.003, 0.014, 0.02))
    box(g, METAL, (0.0268, -0.005, -0.04), (0.006, 0.01, 0.02), bevel=0.001)
    box(g, METAL, (-0.034, 0.026, -0.085), (0.018, 0.013, 0.034), bevel=0.0015)
    box(g, METAL, (-0.0425, 0.026, -0.098), (0.008, 0.011, 0.01), bevel=0.001)
    # takedown and pivot pins
    for z in (-0.108, 0.052):
        cyl(g, METAL, (0.0, -0.02, z), 'x', 0.0042, 0.054, segs=8)
        cyl(g, METAL, (0.0268, -0.02, z), 'x', 0.0072, 0.005, segs=8)
    # safety selector (both sides), mag release and bolt catch
    cyl(g, METAL, (0.0, -0.03, -0.088), 'x', 0.0055, 0.052, segs=8)
    box(g, METAL, (0.0258, -0.0245, -0.082), (0.004, 0.012, 0.022), bevel=0.0008)
    box(g, METAL, (-0.0258, -0.0245, -0.082), (0.004, 0.012, 0.022), bevel=0.0008)
    cyl(g, METAL, (0.0258, -0.062, 0.028), 'x', 0.0052, 0.012, segs=8)
    box(g, METAL, (-0.0235, -0.045, 0.01), (0.004, 0.01, 0.026), bevel=0.001)

    # ---- handguard: KeyMod slots clean through the sides, pockets underneath, the barrel visible inside
    side_slots = [((0.0, 0.002, 0.105 + 0.03 * i), (0.06, 0.013, 0.022)) for i in range(6)]
    belly_pockets = [((0.0, -0.026, 0.105 + 0.03 * i), (0.016, 0.014, 0.014)) for i in range(6)]
    bool_cut(g, METAL, box_bm((0.0, 0.002, 0.168), (0.046, 0.060, 0.215)), side_slots + belly_pockets, bevel=0.0028)
    cyl(g, METAL, (0.0, 0.002, 0.16), 'z', 0.0072, 0.22, segs=8)
    box(g, METAL, (0.0, 0.0365, 0.075), (0.024, 0.004, 0.40))  # rail base strip
    cyl(g, POLYMER, (0.0, 0.002, 0.2815), 'z', 0.0225, 0.004, segs=12)  # handguard end cap ring
    # flat-top rail: cross teeth, front to back, left open where the sight sits
    for i in range(21):
        z = -0.117 + 0.0195 * i
        if -0.062 < z < 0.002:
            continue
        box(g, METAL, (0.0, 0.0405, z), (0.022, 0.006, 0.0105))

    # ---- barrel and a slotted flash hider
    cyl(g, METAL, (0.0, 0.002, 0.292), 'z', 0.0075, 0.04, segs=10)
    hider = cyl_bm((0.0, 0.002, 0.3085), 'z', 0.0112, 0.043, segs=12)
    bool_cut(g, METAL, hider, [((0.0, 0.002, 0.318), (0.04, 0.0034, 0.016)), ((0.0, 0.002, 0.318), (0.0034, 0.04, 0.016))])
    cyl(g, POLYMER, (0.0, 0.002, 0.3), 'z', 0.0128, 0.006, segs=12)

    # ---- trigger group
    tube(g, METAL, [(0.0, -0.076, 0.012), (0.0, -0.088, 0.026), (0.0, -0.108, 0.027), (0.0, -0.116, 0.01), (0.0, -0.116, -0.012),
                    (0.0, -0.104, -0.026)], 0.0034, 6)
    prism(g, METAL, [(-0.004, -0.076), (0.003, -0.076), (0.005, -0.09), (0.001, -0.102), (-0.004, -0.1)], -0.0032, 0.0032,
          bevel=0.0008)

    # ---- grip: back strap and scalloped finger grooves on the front
    prism(g, POLYMER, [(-0.052, -0.075), (-0.062, -0.1), (-0.075, -0.14), (-0.088, -0.18), (-0.046, -0.18), (-0.036, -0.168),
                       (-0.037, -0.154), (-0.026, -0.143), (-0.028, -0.129), (-0.017, -0.118), (-0.013, -0.075)],
          -0.0165, 0.0165, bevel=0.002)
    box(g, METAL, (0.0, -0.1815, -0.067), (0.034, 0.005, 0.044), bevel=0.0012)  # base of the grip

    # ---- magazine: curved, ribbed, with witness windows and a lipped base plate
    zf = [0.040, 0.043, 0.049, 0.058, 0.07]
    ys = [-0.075, -0.108, -0.142, -0.176, -0.212]

    def front_z(y):
        for i in range(len(ys) - 1):
            if ys[i + 1] <= y <= ys[i]:
                t = (y - ys[i]) / (ys[i + 1] - ys[i])
                return zf[i] + t * (zf[i + 1] - zf[i])
        return zf[-1]

    pts = [(z, y) for z, y in zip(zf, ys)] + [(z - 0.048, y) for z, y in reversed(list(zip(zf, ys)))]
    prism(g, POLYMER, pts, -0.0155, 0.0155, bevel=0.0012)
    for i in range(5):
        y = -0.092 - 0.027 * i
        box(g, METAL, (0.0, y, front_z(y) + 0.0013), (0.03, 0.0055, 0.0032))
    for i in range(3):
        y = -0.1 - 0.026 * i
        box(g, METAL, (0.0158, y, front_z(y) - 0.026), (0.002, 0.008, 0.012))
    box(g, METAL, (0.0, -0.2155, 0.0455), (0.036, 0.007, 0.044), bevel=0.0012)

    # ---- stock: collapsed, because the player's camera is right behind it (a full-size butt fills a third of the screen).
    # Ribbed buffer tube, hinge block, a short slim butt with one lightening slot and a rubber pad.
    cyl(g, METAL, (0.0, 0.006, -0.165), 'z', 0.0135, 0.082, segs=10)
    for z in (-0.145, -0.165, -0.185):
        cyl(g, METAL, (0.0, 0.006, z), 'z', 0.0148, 0.004, segs=10)
    box(g, METAL, (0.0, 0.006, -0.125), (0.044, 0.052, 0.03), bevel=0.003)
    stock = prism_bm([(-0.195, 0.026), (-0.226, 0.026), (-0.238, 0.014), (-0.24, -0.034), (-0.226, -0.04), (-0.2, -0.022)],
                     -0.0135, 0.0135)
    bool_cut(g, POLYMER, stock, [((0.0, -0.004, -0.218), (0.04, 0.02, 0.012))], bevel=0.002)
    box(g, METAL, (0.0, -0.007, -0.2425), (0.03, 0.058, 0.005), bevel=0.0012)
    box(g, METAL, (0.0, -0.026, -0.197), (0.01, 0.012, 0.016), bevel=0.001)  # stock release

    # ---- red-dot sight: low mount with clamp screws, tube, hood, lens, rear ring, turrets and an opened front cap
    box(g, METAL, (0.0, 0.0455, -0.03), (0.026, 0.01, 0.05), bevel=0.0015)
    for z in (-0.045, -0.015):
        cyl(g, METAL, (0.0148, 0.0455, z), 'x', 0.004, 0.006, segs=8)
    cyl(g, METAL, (0.0, 0.0705, -0.03), 'z', 0.0165, 0.044, segs=16, bevel=0.0012)
    cyl(g, METAL, (0.0, 0.0705, -0.0075), 'z', 0.0188, 0.012, segs=16)
    cyl(g, POLYMER, (0.0, 0.0705, -0.0012), 'z', 0.0148, 0.003, segs=16)
    cyl(g, METAL, (0.0, 0.0705, -0.0545), 'z', 0.0135, 0.008, segs=16)
    cyl(g, METAL, (0.0, 0.0905, -0.03), 'y', 0.0072, 0.012, segs=8)
    cyl(g, METAL, (0.0195, 0.0705, -0.03), 'x', 0.0066, 0.01, segs=8)
    cyl(g, POLYMER, (0.0, 0.0705, 0.0), 'z', 0.0185, 0.003, segs=16)
    _xf_last(g, unity_euler(-75.0), (0.0, 0.0705 + 0.0185, 0.0), (0.0, 0.0, 0.004))


def frame3(fwd, up):
    """Unity-frame rotation whose columns are (right, up, forward): hand-local +z -> fwd, +y -> up."""
    f = Vector(fwd).normalized()
    u = Vector(up)
    u = (u - f * u.dot(f)).normalized()
    r = u.cross(f)
    return Matrix(((r.x, u.x, f.x), (r.y, u.y, f.y), (r.z, u.z, f.z)))


def build_hands():
    """Two realistic hands (hand_kit.py): the right one grips the pistol grip with its index finger on the trigger, the left one
    holds the handguard from its left side with the back of the hand towards the player."""
    import hand_kit as H  # here, not at the top: hand_kit imports this module
    # pistol grip: its axis runs up and forward (the grip leans back as it goes down); the palm lies against the back strap
    grip_c = (0.0, -0.122, -0.047)
    fwd, up = H.grip_frame((0.0, 0.946, 0.324), (0.0, -0.324, 0.946), 20.0, "R")
    pos = H.palm_position(grip_c, fwd, up, 0.031, -0.020)
    curls = ((26, 40, 24), (92, 84, 50), (94, 86, 52), (96, 88, 50))     # the index lies along the trigger, the rest wrap the grip
    H.add_hand("R", pos, fwd, up, curls=curls, thumb=SMG_THUMB_R, arm_dir=(0.30, -0.30, -0.90), visible=0.04)
    # handguard: a C-grip from below. The palm lies under it facing up, the fingers cross to the right and curl up its right side,
    # and the forearm comes in from the lower left.
    hg_c = (0.0, 0.002, 0.175)
    fwd, up = H.grip_frame((0.0, 0.0, 1.0), (0.45, -1.0, 0.0), 22.0, "L")
    pos = H.palm_position(hg_c, fwd, up, 0.043, -0.020)
    curls = ((62, 86, 54), (66, 88, 54), (70, 90, 52), (72, 90, 50))
    H.add_hand("L", pos, fwd, up, curls=curls, thumb=None, arm_dir=(-0.50, -0.80, -0.30), visible=0.07)


SMG_THUMB_R = None  # the right thumb: the relaxed default until it is placed against the receiver


# --------------------------------------------------------------------------- scene, materials, export


def make_material(name):
    colour, metallic, rough = COLOURS[name]
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes["Principled BSDF"]
    bsdf.inputs["Base Color"].default_value = colour
    bsdf.inputs["Metallic"].default_value = metallic
    bsdf.inputs["Roughness"].default_value = rough
    camo = os.path.join(REPO, "Assets", "Textures", "Models", "T_WeaponSleeve_BaseColor.png")
    if name == "M_Weapon_Camo" and os.path.exists(camo):
        tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
        tex.image = bpy.data.images.load(camo)
        mat.node_tree.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    return mat


def create_object(name, mats, smooth_angle=40.0):
    verts, faces, slots = [], [], []
    for pv, pf, slot in OBJECTS[name]:
        base = len(verts)
        verts.extend(pv)
        for f in pf:
            faces.append([base + i for i in f])
            slots.append(slot)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([tuple(v) for v in verts], [], faces)
    mesh.update()
    used = sorted(set(slots))
    remap = {old: new for new, old in enumerate(used)}
    for m in used:
        mesh.materials.append(mats[m])
    for i, poly in enumerate(mesh.polygons):
        poly.material_index = remap[slots[i]]
        poly.use_smooth = True
    bm = bmesh.new()
    bm.from_mesh(mesh)
    # triangles only: a twisted quad or a concave cap is then split here, where Blender can see it, and Unity never has to
    # guess ("A polygon of Mesh ... is self-intersecting and has been discarded")
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)  # the bevel of the cut stock leaves duplicate points
    bmesh.ops.triangulate(bm, faces=bm.faces, quad_method='BEAUTY', ngon_method='BEAUTY')
    sliver = [f for f in bm.faces if f.calc_area() < 1e-9]
    if sliver:
        bmesh.ops.delete(bm, geom=sliver, context='FACES')
    bmesh.ops.dissolve_degenerate(bm, dist=1e-7, edges=bm.edges)
    for e in bm.edges:
        if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(smooth_angle):
            e.smooth = False
    bm.to_mesh(mesh)
    bm.free()
    ob = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def unwrap(ob):
    bpy.ops.object.select_all(action='DESELECT')
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=math.radians(66.0), island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')


def triangles(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def export_fbx(active):
    for ob in bpy.data.objects:
        ob.select_set(True)
    bpy.context.view_layer.objects.active = active
    bpy.ops.export_scene.fbx(
        filepath=FBX_PATH, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='EDGE', use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE',
        path_mode='AUTO', embed_textures=False, bake_anim=False, add_leaf_bones=False, use_custom_props=False)


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    build_gun()
    build_hands()
    mats = [make_material(n) for n in SLOTS]
    import hand_kit as H
    H.glove_material(mats[GLOVE])
    objs = [create_object(n, mats, 85.0 if n.startswith(("Glove", "Sleeve")) else 40.0) for n in ("Smg", "GloveR", "GloveL", "SleeveR", "SleeveL")]
    total = 0
    for ob in objs:
        unwrap(ob)
        if ob.name.startswith("Glove"):
            H.tile_uvs(ob)
        tris = triangles(ob)
        total += tris
        print(f"[SMG] {ob.name}: {tris} triangles")
    zs = [(_to_unity(ob.matrix_world @ v.co)).z for ob in objs for v in ob.data.vertices]
    print(f"[SMG] total {total} triangles (target 4000, max 6000), muzzle z {max(zs):.3f} (want {MUZZLE_Z})")

    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    export_fbx(objs[0])
    print(f"[SMG] {FBX_PATH}")


if __name__ == "__main__":
    import build_smg  # run the importable copy: hand_kit imports build_smg, and both must share one set of parts
    build_smg.main()
