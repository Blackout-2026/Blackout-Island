"""Converts the downloaded pine forest (D:\\model\\pine-forest-at-sunset, a Sketchfab ponderosa stand as a .glb) into the forest models of the game
(step F1). Owner: World Builder (Core Developer's tree pipeline is reused).

    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_ponderosa_forest.py
    ... -P Art/Blender/Scripts/build_ponderosa_forest.py -- <path to ponderosa_stand_sketchfab.glb> <folder with the glb's textures>

Unity 6 has no .glb importer and the project has no glTFast, so the stand is converted once into the same FBX layout the other trees use:
  SM_Tree_Pine, PineB, PineC, PineD   overwritten in place (same GUID): <Name>_LOD0..2, two slots (bark, needles), the heights that
                                      TreeFbxTests expects. Pine and PineB are the glb's Florasynth trees, PineC and PineD two of its seedlings.
  SM_Forest_Rock, SM_Forest_RockSmall one rock each (Rock_LOD0..2), pivot at the centre of the rock, from the glb's icosphere rocks.
  SM_Forest_Bush                      a young pine's needle sprays, one slot, 1 m tall: the terrain detail "Bush".
  SM_Forest_Grass                     three crossed alpha cards, 1 m x 0.75 m: the terrain detail "Grass".
And the five textures (1024, 512 for the grass) in Assets/Textures/Models.

LODs of the foliage: the glb's needle sprays are loose cards, so a lower LOD keeps a fixed, evenly spread share of the cards and makes each
kept card bigger so the crown keeps its volume (collapsing a card mesh would only wreck the cards). Branches are one connected mesh and use
Blender's Decimate. Everything is deterministic: no random numbers, the cards are chosen by their index.

Frame: the same as build_trees.py (Z up, base on z = 0, Apply Transform on, rotation 0, scale 1), exported with the same FBX settings.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
FBX_DIR = os.path.join(REPO, "Assets", "Models", "Custom")
BLEND_DIR = os.path.join(REPO, "Art", "Blender")

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
GLB = argv[0] if argv else r"D:\model\pine-forest-at-sunset\source\ponderosa_stand_sketchfab.glb"
SRC_TEX = argv[1] if len(argv) > 1 else r"D:\model\pine-forest-at-sunset\textures"

# name -> (height in m, bark mesh of the glb, needle mesh of the glb, width scale). The heights are the ranges of TreeFbxTests.
TREES = {
    "Pine": (10.6, "FlorasynthTree.001_branches_far", "FlorasynthTree.001_foliage_far", 1.0),
    "PineB": (12.8, "FlorasynthTree.002_branches_far", "FlorasynthTree.002_foliage_far", 0.9),
    "PineC": (9.0, "Ponderosa Pine seedling.001_branches", "Ponderosa Pine seedling.001_foliage", 1.15),
    "PineD": (5.3, "Ponderosa Pine seedling.062_branches", "Ponderosa Pine seedling.062_foliage", 1.0),
}
# triangles of (needles, branches) per LOD; the sum stays under TreeFbxTests' 5000 / 300. LOD0 is about 2,700: the average pine of the
# trees it replaces (2,842), so the visible-triangle estimate of docs/level-layout.md does not get worse.
LOD_TRIANGLES = ((1800, 900), (500, 300), (140, 90))
SUFFIX = {"Pine": "", "PineB": "_B", "PineC": "_C", "PineD": "_D"}

# rock: glb mesh, width of the rock in m (the placeholder is 2.3 m), height scale, (LOD0, LOD1, LOD2) triangles
ROCKS = {
    "Rock": ("Icosphere.003", 2.4, 0.75, (900, 320, 90)),
    "RockSmall": ("Icosphere.004", 2.0, 0.7, (700, 260, 80)),
}
BUSH_MESH = "Ponderosa Pine seedling.061_foliage"
BUSH_HEIGHT = 1.0


# --------------------------------------------------------------------------- textures

def prepare_texture(source, target, size, keep_alpha):
    image = bpy.data.images.load(os.path.join(SRC_TEX, source))
    image.alpha_mode = 'CHANNEL_PACKED'  # no premultiplying: the colour under transparent pixels is kept as it is
    image.scale(size, size)
    image.file_format = 'PNG'
    image.filepath_raw = os.path.join(TEX_DIR, target)
    scene = bpy.context.scene
    scene.render.image_settings.file_format = 'PNG'
    scene.render.image_settings.color_mode = 'RGBA' if keep_alpha else 'RGB'
    image.save_render(os.path.join(TEX_DIR, target), scene=scene)
    bpy.data.images.remove(image)
    print(f"[texture] {target}")


def textures():
    prepare_texture("ponderosa-bark-old_1.jpeg", "T_PonderosaBark_BaseColor.png", 1024, False)
    prepare_texture("brown-bark-old-NORMAL_0.jpeg", "T_PonderosaBark_Normal.png", 1024, False)
    prepare_texture("ponderosa-foliage_2.png", "T_PonderosaNeedles_BaseColor.png", 1024, True)
    prepare_texture("grass-lush-min_4.png", "T_ForestGrass_BaseColor.png", 512, True)
    prepare_texture("mossy_rock_diff_1k.jpg", "T_ForestRock_BaseColor.png", 1024, False)


def image_material(name, base, alpha=False, normal=None, rough=0.9):
    """A material that shows the texture files in Blender. Unity builds its own materials and matches them by this name."""
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX_DIR, base))
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
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


# --------------------------------------------------------------------------- the glb

def import_glb():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit_settings = bpy.context.scene.unit_settings
    unit_settings.system, unit_settings.scale_length, unit_settings.length_unit = 'METRIC', 1.0, 'METERS'
    bpy.ops.import_scene.gltf(filepath=GLB)
    return {o.data.name: o for o in sorted(bpy.data.objects, key=lambda o: o.name) if o.type == 'MESH'}


def world_mesh(source, name):
    """A copy of the glb's mesh with its object transform applied: (new mesh, nothing else)."""
    me = source.data.copy()
    me.transform(source.matrix_world)
    me.name = name
    return me


def tri_count(me):
    return sum(len(p.vertices) - 2 for p in me.polygons)


def bounds(me):
    zs = [v.co.z for v in me.vertices]
    return min(zs), max(zs)


def islands(bm):
    """The loose parts of a mesh (one card is one island), as lists of faces, in face order."""
    seen, result = set(), []
    for face in bm.faces:
        if face.index in seen:
            continue
        stack, part = [face], []
        seen.add(face.index)
        while stack:
            f = stack.pop()
            part.append(f)
            for e in f.edges:
                for n in e.link_faces:
                    if n.index not in seen:
                        seen.add(n.index)
                        stack.append(n)
        result.append(part)
    return result


def thin_cards(me, target_tris, max_grow):
    """Keeps an evenly spread share of the cards and scales each kept card about its centre so the crown keeps its volume."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    parts = islands(bm)
    total = sum(len(f.verts) - 2 for part in parts for f in part)
    if total > target_tris:
        keep = max(1, int(len(parts) * target_tris / total))
        step = len(parts) / keep
        kept = {int(i * step) for i in range(keep)}
        grow = min(max_grow, math.sqrt(len(parts) / keep))
        doomed = [f for i, part in enumerate(parts) if i not in kept for f in part]
        for i in sorted(kept):
            verts = {v for f in parts[i] for v in f.verts}
            centre = sum((v.co for v in verts), Vector()) / len(verts)
            for v in verts:
                v.co = centre + (v.co - centre) * grow
        bmesh.ops.delete(bm, geom=doomed, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    return me


def main_limbs(me, target_tris):
    """The branches are about 130 loose tubes, which Decimate cannot reduce below about 800 triangles. A low LOD keeps the tallest tubes
    (the trunk first, then the long limbs) until the budget is reached; Decimate then trims what is left."""
    bm = bmesh.new()
    bm.from_mesh(me)
    bm.faces.ensure_lookup_table()
    parts = islands(bm)

    def height(part):
        zs = [v.co.z for f in part for v in f.verts]
        return max(zs) - min(zs)

    ranked = sorted(parts, key=height, reverse=True)
    kept, count = set(), 0
    for part in ranked:
        if count >= target_tris:
            break
        kept.update(f.index for f in part)
        count += sum(len(f.verts) - 2 for f in part)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.index not in kept], context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    bmesh.ops.triangulate(bm, faces=bm.faces[:])
    bm.to_mesh(me)
    bm.free()
    return me


def decimated(me, target_tris):
    """Blender's collapse decimate, for the connected meshes (branches, rocks)."""
    ob = bpy.data.objects.new("_tmp", me)
    bpy.context.scene.collection.objects.link(ob)
    start = tri_count(me)
    if start > target_tris:
        mod = ob.modifiers.new("Decimate", 'DECIMATE')
        mod.ratio = target_tris / start
        mod.use_collapse_triangulate = True
    result = bpy.data.meshes.new_from_object(ob.evaluated_get(bpy.context.evaluated_depsgraph_get()))
    result.name = me.name
    bpy.data.objects.remove(ob)
    return result


def make_object(name, parts, materials):
    """One object from meshes that each use one material: the slots are in the order of `materials`."""
    bm = bmesh.new()
    for slot, me in enumerate(parts):
        offset = len(bm.faces)
        bm.from_mesh(me)
        bm.faces.ensure_lookup_table()
        for f in bm.faces[offset:]:
            f.material_index = slot
    result = bpy.data.meshes.new(name)
    bm.to_mesh(result)
    bm.free()
    for m in materials:
        result.materials.append(m)
    for p in result.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(name, result)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def ground(meshes, base_mesh, height, width_scale):
    """Puts the tree on z = 0 under its trunk, at `height` m, and `width_scale` times wider. Applies to every mesh in the list."""
    zs = [v.co.z for v in base_mesh.vertices]
    low = min(zs)
    foot = [v.co for v in base_mesh.vertices if v.co.z < low + 0.25]
    cx = sum(c.x for c in foot) / len(foot)
    cy = sum(c.y for c in foot) / len(foot)
    top = max(max(v.co.z for v in me.vertices) for me in meshes)
    s = height / (top - low)
    for me in meshes:
        for v in me.vertices:
            v.co = Vector(((v.co.x - cx) * s * width_scale, (v.co.y - cy) * s * width_scale, (v.co.z - low) * s))
        me.update()


# --------------------------------------------------------------------------- models

def build_tree(source, name):
    height, branch_name, leaf_name, width = TREES[name]
    branches = world_mesh(source[branch_name], "b")
    needles = world_mesh(source[leaf_name], "n")
    ground([branches, needles], branches, height, width)
    suffix = SUFFIX[name]
    bark = image_material("M_Tree_PineBark" + suffix, "T_PonderosaBark_BaseColor.png", normal="T_PonderosaBark_Normal.png", rough=0.95)
    leaves = image_material("M_Tree_PineNeedles" + suffix, "T_PonderosaNeedles_BaseColor.png", alpha=True, rough=0.85)
    objects = []
    for lod, (leaf_tris, branch_tris) in enumerate(LOD_TRIANGLES):
        grow = (1.35, 1.9, 3.0)[lod]
        part_n = thin_cards(needles.copy(), leaf_tris, grow)
        part_b = branches.copy()
        if lod > 0:
            part_b = main_limbs(part_b, branch_tris)
        part_b = decimated(part_b, branch_tris)
        ob = make_object(f"{name}_LOD{lod}", [part_b, part_n], [bark, leaves])
        low = min(v.co.z for v in ob.data.vertices)
        if low < 0.0:  # a grown card can hang below the ground: lift the LOD so the tree still starts at y = 0
            for v in ob.data.vertices:
                v.co.z -= low
        objects.append(ob)
        print(f"[{name}] LOD{lod}: {tri_count(ob.data)} triangles (needles {tri_count(part_n)}, branches {tri_count(part_b)})")
    report(name, objects[0])
    return objects


def build_rock(source, name):
    mesh_name, width, flatten, tris = ROCKS[name]
    me = world_mesh(source[mesh_name], name)
    xs, ys, zs = [v.co.x for v in me.vertices], [v.co.y for v in me.vertices], [v.co.z for v in me.vertices]
    centre = Vector(((min(xs) + max(xs)) / 2, (min(ys) + max(ys)) / 2, (min(zs) + max(zs)) / 2))
    s = width / max(max(xs) - min(xs), max(ys) - min(ys))
    for v in me.vertices:
        v.co = Vector(((v.co.x - centre.x) * s, (v.co.y - centre.y) * s, (v.co.z - centre.z) * s * flatten))
    material = image_material("M_Forest_Rock", "T_ForestRock_BaseColor.png", rough=0.9)
    objects = []
    for lod, count in enumerate(tris):
        ob = make_object(f"{name}_LOD{lod}", [decimated(me.copy(), count)], [material])
        objects.append(ob)
        print(f"[{name}] LOD{lod}: {tri_count(ob.data)} triangles")
    report(name, objects[0], centred=True)
    return objects


def build_bush(source):
    me = world_mesh(source[BUSH_MESH], "Bush")
    ground([me], me, BUSH_HEIGHT, 1.0)
    material = image_material("M_Forest_Bush", "T_PonderosaNeedles_BaseColor.png", alpha=True, rough=0.85)
    ob = make_object("Bush", [me], [material])
    print(f"[Bush] {tri_count(ob.data)} triangles")
    report("Bush", ob)
    return [ob]


def build_grass():
    """Three cards crossing at 60 degrees. The picture hangs from its top edge, so the top of the picture is the root: v = 1 at the ground."""
    width, height = 1.0, 0.75
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    for k in range(3):
        a = math.radians(60 * k)
        dx, dy = math.cos(a) * width / 2, math.sin(a) * width / 2
        verts = [bm.verts.new((-dx, -dy, 0)), bm.verts.new((dx, dy, 0)), bm.verts.new((dx, dy, height)), bm.verts.new((-dx, -dy, height))]
        face = bm.faces.new(verts)
        for loop, (u, v) in zip(face.loops, ((0, 1), (1, 1), (1, 0), (0, 0))):
            loop[uv].uv = (u, v)
    me = bpy.data.meshes.new("GrassTuft")
    bm.to_mesh(me)
    bm.free()
    material = image_material("M_Forest_Grass", "T_ForestGrass_BaseColor.png", alpha=True, rough=1.0)
    me.materials.append(material)
    ob = bpy.data.objects.new("GrassTuft", me)
    bpy.context.scene.collection.objects.link(ob)
    return [ob]


def report(name, ob, centred=False):
    xs, ys, zs = ([v.co[i] for v in ob.data.vertices] for i in range(3))
    print(f"[{name}] LOD0 size {max(xs) - min(xs):.2f} x {max(zs) - min(zs):.2f} (up) x {max(ys) - min(ys):.2f} m, "
          f"up {min(zs):.2f}..{max(zs):.2f}{' (centred)' if centred else ''}")


# --------------------------------------------------------------------------- export

def export(objects, fbx_name, blend_name):
    for ob in bpy.data.objects:
        ob.select_set(ob in objects)
    bpy.context.view_layer.objects.active = objects[0]
    fbx = os.path.join(FBX_DIR, fbx_name)
    os.makedirs(os.path.dirname(fbx), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=fbx, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
        mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE',
        path_mode='AUTO', embed_textures=False, bake_anim=False, add_leaf_bones=False, use_custom_props=False)
    print(f"[export] {fbx}")


def save_blend(objects, blend_name):
    """Each model gets its own small .blend: a fresh file with only that model, so the .blend is not the 37 MB stand."""
    keep = set(objects)
    for ob in list(bpy.data.objects):
        if ob not in keep:
            bpy.data.objects.remove(ob)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(BLEND_DIR, blend_name))


def main():
    textures()
    source = import_glb()
    # build everything first, then export one file at a time (the export selects only that file's objects)
    models = {}
    for name in TREES:
        models[f"SM_Tree_{name}"] = build_tree(source, name)
    for name in ROCKS:
        models[f"SM_Forest_{name}"] = build_rock(source, name)
    models["SM_Forest_Bush"] = build_bush(source)
    models["SM_Forest_Grass"] = build_grass()
    for key, objects in models.items():
        export(objects, key + ".fbx", key + ".blend")
    # one .blend with all the new models (the stand itself stays in D:\model, it is too big for the repository)
    all_objects = [ob for objects in models.values() for ob in objects]
    save_blend(all_objects, "SM_Forest_Ponderosa.blend")


if __name__ == "__main__":
    main()
