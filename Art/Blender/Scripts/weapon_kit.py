"""Shared Blender helpers for the weapon build scripts (step C7). Owner: Core Developer. Not run on its own.

boolean_difference: a mesh minus cutter meshes with Blender's exact solver (slots, holes, pockets).
bevel_bm: round the sharp edges of a bmesh. star_bm: a raised star (a Torx rivet drive). Everything is in the Blender frame; callers
build their shapes from Unity-frame numbers with B()/the build_smg kit.
"""

import math
import os
import sys

import bmesh
import bpy

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402
import gen_parts as P  # noqa: E402

B = P.B


def boolean_difference(base_bm, cutter_bms):
    """base minus the cutters (each already a Blender-frame bmesh); the result is a new bmesh."""
    # the exact solver decides inside from the normals, so make them consistent first (hand-built faces have random winding)
    bmesh.ops.recalc_face_normals(base_bm, faces=base_bm.faces)
    for sub in cutter_bms:
        bmesh.ops.recalc_face_normals(sub, faces=sub.faces)
    bad = sum(1 for e in base_bm.edges if len(e.link_faces) != 2)
    if bad:
        print(f"[KNIFE] WARNING: the mesh to cut has {bad} open edges")
    base = bpy.data.meshes.new("base")
    base_bm.to_mesh(base)
    base_bm.free()
    base_ob = bpy.data.objects.new("base", base)
    bpy.context.scene.collection.objects.link(base_ob)
    merged = bmesh.new()
    for sub in cutter_bms:
        tmp = bpy.data.meshes.new("t")
        sub.to_mesh(tmp)
        sub.free()
        merged.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    cm = bpy.data.meshes.new("cut")
    merged.to_mesh(cm)
    merged.free()
    cut_ob = bpy.data.objects.new("cut", cm)
    bpy.context.scene.collection.objects.link(cut_ob)
    mod = base_ob.modifiers.new("cuts", 'BOOLEAN')
    mod.operation = 'DIFFERENCE'
    mod.solver = 'EXACT'
    mod.use_self = True            # the cutters overlap (a slot is a box plus two round ends)
    mod.object = cut_ob
    dg = bpy.context.evaluated_depsgraph_get()
    mesh = bpy.data.meshes.new_from_object(base_ob.evaluated_get(dg))
    out = bmesh.new()
    out.from_mesh(mesh)
    bmesh.ops.dissolve_limit(out, angle_limit=math.radians(1.0), verts=out.verts, edges=out.edges)
    bpy.data.objects.remove(base_ob)
    bpy.data.objects.remove(cut_ob)
    bpy.data.meshes.remove(mesh)
    return out


def bevel_bm(bm, offset, seg=1):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.edges.ensure_lookup_table()
    edges = [e for e in bm.edges if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > S.BEVEL_ANGLE]
    if edges:
        bmesh.ops.bevel(bm, geom=edges, offset=offset, offset_type='OFFSET', segments=seg, profile=0.5,
                        affect='EDGES', clamp_overlap=True)



def star_bm(c, r_out, r_in, depth, lobes=6):
    """A raised star (the drive of a Torx rivet) on the x face at c, as a bmesh in the Blender frame."""
    bm = bmesh.new()
    ring0, ring1 = [], []
    for k in range(lobes * 2):
        r = r_out if k % 2 == 0 else r_in
        a = math.pi * k / lobes
        y, z = c[1] + r * math.cos(a), c[2] + r * math.sin(a)
        ring0.append(bm.verts.new(B((c[0] - depth / 2.0, y, z))))
        ring1.append(bm.verts.new(B((c[0] + depth / 2.0, y, z))))
    n = len(ring0)
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((ring0[i], ring0[j], ring1[j], ring1[i]))
    bm.faces.new(ring0)
    bm.faces.new(list(reversed(ring1)))
    return bm
