"""Cyberpunk door for the bunker's four doorways (step D1).

Run: blender -b --factory-startup --python build_cyber_door.py -- <extracted download folder> <repo root>

The download (JesusFernandezGarcia_CoolDoorCyberpunk_JFG_1.zip, Sketchfab) is ONE mesh of 24 loose parts: a double door inside a thick frame.
This script cuts it into three objects so the game can slide the leaves apart:
  DoorFrame   the ring around the opening, the header lights and the two side blocks (static)
  DoorLeaf_L  everything left of the centre seam: the leaf panels, vents, centre strip and half of the lock (slides to -X)
  DoorLeaf_R  the same on the right (slides to +X)
and scales them to the frozen 1.4 x 2.3 m doorway (docs/level-layout.md): origin at the middle of the sill, X across the doorway, Y up,
Z through the wall (centre of the wall). It also repacks the Unreal-style ORM map (R occlusion, G roughness, B metallic) into the layout URP Lit
reads (R metallic, G occlusion, A smoothness).
"""
import bpy
import bmesh
import os
import sys
import shutil
import mathutils

args = sys.argv[sys.argv.index("--") + 1:]
SRC, REPO = args[0], args[1]

# Measured on the downloaded mesh after import (metres): the seam between the leaves, the sill and the wall centre.
SEAM = 0.12
FLOOR = -2.10
CENTRE = -0.10
LEAF_WIDTH = 2.66        # leaf panels, y -1.21 ... 1.45
LEAF_HEIGHT = 4.08       # leaf panels, z -2.08 ... 2.00
GAP_WIDTH = 1.4          # BunkerLayout doorway
GAP_HEIGHT = 2.3
SW = GAP_WIDTH / LEAF_WIDTH
SH = GAP_HEIGHT / LEAF_HEIGHT
FRAME_DEPTH = 0.95       # frame thickness 0.54 m -> 0.51 m: 5 cm proud of the 0.4 m wall on each face
LEAF_DEPTH = 0.75        # leaf thickness 0.47 m -> 0.35 m: slides inside the wall


def parts_of(bm):
    seen = {}
    parts = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen[v.index] = len(parts)
        members = []
        while stack:
            a = stack.pop()
            members.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen[b.index] = len(parts)
                    stack.append(b)
        parts.append(members)
    return parts


def is_frame(members):
    ys = [v.co.y for v in members]
    zs = [v.co.z for v in members]
    size_y = max(ys) - min(ys)
    cy = (max(ys) + min(ys)) * 0.5
    return size_y > 3.0 or abs(cy - SEAM) > 1.3 or min(zs) > 1.9


def build(name, faces_src, bm_src, depth_scale):
    """A new mesh object from the given faces of bm_src, moved and scaled into the doorway's space."""
    bm = bmesh.new()
    uv_src = bm_src.loops.layers.uv.active
    uv = bm.loops.layers.uv.new("UVMap")
    vmap = {}
    for f in faces_src:
        verts = []
        for v in f.verts:
            if v.index not in vmap:
                vmap[v.index] = bm.verts.new(v.co)
            verts.append(vmap[v.index])
        nf = bm.faces.new(verts)
        nf.smooth = f.smooth
        nf.material_index = 0
        for loop_new, loop_old in zip(nf.loops, f.loops):
            loop_new[uv].uv = loop_old[uv_src].uv
    # x (thickness) -> -Y, y (across) -> X, z (height) -> Z: a rotation plus scales, so nothing is mirrored.
    m = mathutils.Matrix((
        (0.0, SW, 0.0, -SW * SEAM),
        (-depth_scale, 0.0, 0.0, depth_scale * CENTRE),
        (0.0, 0.0, SH, -SH * FLOOR),
        (0.0, 0.0, 0.0, 1.0)))
    bm.transform(m)
    bm.normal_update()
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Model", "SM_Door_3_JFG.fbx"))
src = bpy.data.objects["SM_Door_3_JFG"]
bm = bmesh.new()
bm.from_mesh(src.data)
bm.transform(src.matrix_world)
bm.verts.ensure_lookup_table()

frame_verts = set()
leaf_verts = set()
for members in parts_of(bm):
    (frame_verts if is_frame(members) else leaf_verts).update(v.index for v in members)
print("frame verts", len(frame_verts), "leaf verts", len(leaf_verts))

# Cut the leaf geometry at the seam so the centre strip and lock halves go with their own leaf.
leaf_vert_list = [v for v in bm.verts if v.index in leaf_verts]
leaf_geom = list(leaf_vert_list)
leaf_geom += list({e for v in leaf_vert_list for e in v.link_edges})
leaf_geom += list({f for v in leaf_vert_list for f in v.link_faces})
original = len(bm.verts)
bmesh.ops.bisect_plane(bm, geom=leaf_geom, plane_co=(0.0, SEAM, 0.0), plane_no=(0.0, 1.0, 0.0), dist=1e-5)
bm.verts.ensure_lookup_table()
bm.faces.ensure_lookup_table()

# A face belongs to the frame only if every vertex was a frame vertex before the cut (the cut added vertices to leaf edges only).
frame_faces, left_faces, right_faces = [], [], []
for f in bm.faces:
    ids = [v.index for v in f.verts]
    if all(i < original and i in frame_verts for i in ids):
        frame_faces.append(f)
    else:
        cy = sum(v.co.y for v in f.verts) / len(f.verts)
        (left_faces if cy < SEAM else right_faces).append(f)
print("faces: frame", len(frame_faces), "left", len(left_faces), "right", len(right_faces))

frame = build("DoorFrame", frame_faces, bm, FRAME_DEPTH)
leaf_l = build("DoorLeaf_L", left_faces, bm, LEAF_DEPTH)
leaf_r = build("DoorLeaf_R", right_faces, bm, LEAF_DEPTH)
bpy.data.objects.remove(src)

mat = bpy.data.materials.new("M_DoorCyber")
for ob in (frame, leaf_l, leaf_r):
    ob.data.materials.append(mat)

# Textures: base colour, normal and emission are copied; the mask is repacked for URP Lit.
tex_dir = os.path.join(REPO, "Assets", "Textures", "Models")
os.makedirs(tex_dir, exist_ok=True)
for suffix, target in (("BC", "T_DoorCyber_BaseColor.png"), ("N", "T_DoorCyber_Normal.png"), ("E", "T_DoorCyber_Emission.png")):
    shutil.copyfile(os.path.join(SRC, "Textures", "M_Door_3_JFG_%s.png" % suffix), os.path.join(tex_dir, target))

import numpy as np
orm = bpy.data.images.load(os.path.join(SRC, "Textures", "M_Door_3_JFG_ORM.png"))
orm.colorspace_settings.name = "Non-Color"
w, h = orm.size
px = np.array(orm.pixels[:], dtype=np.float32).reshape(-1, 4)
mask = np.empty_like(px)
mask[:, 0] = px[:, 2]          # metallic  (ORM blue)
mask[:, 1] = px[:, 0]          # occlusion (ORM red)
mask[:, 2] = 0.0
mask[:, 3] = 1.0 - px[:, 1]    # smoothness = 1 - roughness (ORM green)
out = bpy.data.images.new("T_DoorCyber_MetalSmooth", w, h, alpha=True)
out.colorspace_settings.name = "Non-Color"
out.pixels.foreach_set(mask.ravel())
out.filepath_raw = os.path.join(tex_dir, "T_DoorCyber_MetalSmooth.png")
out.file_format = "PNG"
out.save()
print("mask written", w, h)

# Save the blend and export.
blend_path = os.path.join(REPO, "Art", "Blender", "SM_Door_Cyberpunk.blend")
fbx_path = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Door_Cyberpunk.fbx")
for ob in bpy.context.scene.objects:
    ob.select_set(True)
bpy.ops.wm.save_as_mainfile(filepath=blend_path)
bpy.ops.export_scene.fbx(
    filepath=fbx_path, use_selection=True, object_types={'MESH'}, global_scale=1.0,
    apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
    bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
    use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
    add_leaf_bones=False, use_custom_props=False)
for ob in (frame, leaf_l, leaf_r):
    d = ob.dimensions
    box = [ob.matrix_world @ mathutils.Vector(c) for c in ob.bound_box]
    print("%-11s tris=%4d dims=(%.3f %.3f %.3f) x[%.3f,%.3f] y[%.3f,%.3f] z[%.3f,%.3f]" % (
        ob.name, sum(len(p.vertices) - 2 for p in ob.data.polygons), d.x, d.y, d.z,
        min(b.x for b in box), max(b.x for b in box), min(b.y for b in box), max(b.y for b in box),
        min(b.z for b in box), max(b.z for b in box)))
print("[CyberDoor]", blend_path, fbx_path)
