"""GN1. Replace the player's SMG with the downloaded IOER-32G: convert, trim, thin and re-texture it, in the gun frame the game already uses.

Run (Blender 5.2, background):
  blender -b --factory-startup --python build_ioer_smg.py -- <folder holding ioer-32g> <repo root> <review folder>
  IOER_WRITE=1 additionally writes the game files (textures, the viewmodel FBX, the combined Blender sources). Without it only the review
  folder is written (clay/textured previews, a report), so the geometry can be checked first.

The download is a COLLADA file (model.zip -> model/model.dae): three loose meshes, about 140,000 triangles in all, with 4096 PBR maps for each of
three materials. Blender 5.2 has no COLLADA importer, so this script reads the file itself (xml): every vertex already carries its own normal and
uv, so a polygon is just a list of indices into the shared arrays. The file is Y up, in metres, but the gun is only about 1,600 units long (its
author did not scale it); the scale below makes it a real SMG.

Frame (docs/art-pipeline.md 14.6, the same as build_viewmodels.py): Blender X right, Y forward (the muzzle at 0.33 m, where the old SMG's
MuzzlePoint and flash are), Z up (bore axis at 0.004 m). The FBX exporter's -Z forward / Y up turns that into Unity x right, y up, z forward.

What it does:
  1. Reads the three meshes and turns them into the gun frame. The model's own forward axis is -Z, so (x, y, z) -> (x, -z, y).
  2. Keeps the whole gun: stock, receiver, handguard, pistol grip, magazine, muzzle device, flip sights and red-dot optic. (STOCK_CUT_Y can cut the
     stock and cap it, but a cut stock looked like a box in the game; the hip and aim poses are placed so the whole gun is in view instead.)
  3. Splits the magazine out (so reload can drop it) and thins both parts by dissolving flat faces, then edge collapse, to the triangle targets below.
  4. Packs the three materials' maps (1024 each) into one 2048 atlas, so the gun still uses its one material M_Weapon_Smg:
         T_WeaponSmg_BaseColor.png, _Normal.png, _MetalSmooth.png (R metallic, G occlusion, A smoothness), as the material expects.
  5. Writes the review pictures, and with IOER_WRITE=1 the game files.
"""
import bmesh
import bpy
import math
import mathutils
import numpy as np
import os
import sys
import xml.etree.ElementTree as ET
import zipfile

args = sys.argv[sys.argv.index("--") + 1:]
DOWNLOAD, REPO, REVIEW = args[0], args[1], args[2]
WRITE = os.environ.get("IOER_WRITE") == "1"
os.makedirs(REVIEW, exist_ok=True)

SCALE = 0.00036          # model units -> metres: the whole gun (stock out) is 0.577 m, about the length of the old SMG with its stock
MUZZLE_Y = 0.33          # gun frame: the muzzle (the old MuzzlePoint)
BORE_Z = 0.004           # gun frame: the bore axis height
STOCK_CUT_Y = None       # gun frame: where the stock would be cut. None keeps the whole stock: cut versions (0.12 and 0.09 m) looked like a boxy stump in the game
GUN_TRIS = 3400
OPTIC_TRIS = 900         # the red-dot optic is thinned on its own: as part of the gun its thin mounting riser collapses first and it floats
MAG_TRIS = 450
ATLAS = 2048
TILE = 1024
MAG_Y = (0.085, 0.165)   # gun frame: the magazine's reach along the barrel
MAG_TOP_Z = -0.018       # gun frame: the magazine is whatever of that reach lies below this height
OPTIC_Y = (0.05, 0.145)  # gun frame: the optic's reach along the barrel
OPTIC_BOTTOM_Z = 0.0615  # gun frame: the optic (with its riser) is whatever of that reach lies above this height; the receiver's rail top is at 0.0585-0.061

TEX_OUT = os.path.join(REPO, "Assets", "Textures", "Models")
FBX_OUT = os.path.join(REPO, "Assets", "Models", "Custom")
report = {}


def log(message):
    print("[GN1]", message, flush=True)


# ------------------------------------------------------------------------------------------------------------ read the COLLADA file
def read_dae():
    work = os.path.join(REVIEW, "dae")
    os.makedirs(work, exist_ok=True)
    with zipfile.ZipFile(os.path.join(DOWNLOAD, "source", "model.zip")) as z:
        z.extract("model/model.dae", work)
    root = ET.parse(os.path.join(work, "model", "model.dae")).getroot()
    q = lambda tag: "{%s}%s" % (root.tag.split("}")[0][1:], tag)
    material_of = {}
    for node in root.iter(q("node")):
        for ig in node.findall(q("instance_geometry")):
            material_of[ig.get("url")[1:]] = next(ig.iter(q("instance_material"))).get("target")[1:]
    parts = {}
    for geometry in root.iter(q("geometry")):
        mesh = geometry.find(q("mesh"))
        sources = {}
        for s in mesh.findall(q("source")):
            array = s.find(q("float_array"))
            if array is None:
                continue
            stride = int(s.find(q("technique_common")).find(q("accessor")).get("stride"))
            values = np.array(array.text.split(), dtype=np.float64)
            sources[s.get("id")] = values.reshape(-1, stride)
        polylist = mesh.find(q("polylist"))
        semantic = {i.get("semantic"): i.get("source")[1:] for i in polylist.findall(q("input"))}
        positions = sources[mesh.find(q("vertices")).find(q("input")).get("source")[1:]]
        uvs = sources[semantic["TEXCOORD"]]
        counts = np.array(polylist.find(q("vcount")).text.split(), dtype=np.int64)
        indices = np.array(polylist.find(q("p")).text.split(), dtype=np.int64)
        parts[material_of[geometry.get("id")]] = (positions, uvs, counts, indices)
    return parts


def to_gun_frame_raw(p):
    """The model's frame (X right, Y up, -Z forward) to a Blender gun frame (X right, Y forward, Z up), still in model units."""
    return mathutils.Vector((p[0], -p[2], p[1]))


def build_part(name, data):
    positions, uvs, counts, indices = data
    bm = bmesh.new()
    verts = [bm.verts.new(to_gun_frame_raw(p)) for p in positions]
    layer = bm.loops.layers.uv.new("UVMap")
    cursor = 0
    skipped = 0
    for count in counts:
        ring = [int(i) for i in indices[cursor:cursor + count]]
        cursor += count
        if len(set(ring)) < 3:
            skipped += 1
            continue
        try:
            face = bm.faces.new([verts[i] for i in ring])
        except ValueError:
            skipped += 1
            continue
        for loop, i in zip(face.loops, ring):
            loop[layer].uv = (uvs[i][0], uvs[i][1])
    report[name + "_skipped_polygons"] = skipped
    bm.verts.ensure_lookup_table()
    return bm


# ------------------------------------------------------------------------------------------------------------ helpers
def tri_count(bm):
    return sum(len(f.verts) - 2 for f in bm.faces)


def decimate(bm, target):
    """Dissolve flat faces first (they cost triangles and show nothing), then collapse edges down to about the target. UVs are carried."""
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(1.5), verts=bm.verts[:], edges=bm.edges[:], delimit={'UV', 'MATERIAL'})
    tri = bm.copy()
    bmesh.ops.triangulate(tri, faces=tri.faces[:])
    after_dissolve = len(tri.faces)
    me = bpy.data.meshes.new("dec_src")
    tri.to_mesh(me)
    tri.free()
    ob = bpy.data.objects.new("dec_src", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("d", 'DECIMATE')
    mod.ratio = min(1.0, target / max(1, after_dissolve))
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.update()
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    out_me = bpy.data.meshes.new_from_object(ev)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    out = bmesh.new()
    out.from_mesh(out_me)
    bpy.data.meshes.remove(out_me)
    return out, after_dissolve


def cut_stock(bm):
    """Bisect at the cut plane, drop what is behind it and cap the hole with one flat face (its uv is the average of the stock beside it)."""
    geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
    result = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0, STOCK_CUT_Y, 0), plane_no=(0, 1, 0), clear_inner=True, clear_outer=False)
    rim = [e for e in result["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
    caps = bmesh.ops.triangle_fill(bm, edges=rim, use_beauty=True)["geom"]
    layer = bm.loops.layers.uv.active
    near = [f for f in bm.faces if f not in caps and all(v.co.y < STOCK_CUT_Y + 0.02 for v in f.verts)]
    uv = mathutils.Vector((0, 0))
    count = 0
    for f in near:
        for loop in f.loops:
            uv += loop[layer].uv
            count += 1
    uv = uv / max(1, count)
    for f in caps:
        if isinstance(f, bmesh.types.BMFace):
            for loop in f.loops:
                loop[layer].uv = uv
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return len([f for f in caps if isinstance(f, bmesh.types.BMFace)])


def split_faces(bm, inside):
    """Moves the faces for which inside(face) is true into a new bmesh (uv carried) and returns it."""
    layer = bm.loops.layers.uv.active
    part = bmesh.new()
    part_layer = part.loops.layers.uv.new("UVMap")
    chosen = [f for f in bm.faces if inside(f)]
    mapping = {}
    for f in chosen:
        new_verts = []
        for v in f.verts:
            if v.index not in mapping:
                mapping[v.index] = part.verts.new(v.co)
            new_verts.append(mapping[v.index])
        try:
            nf = part.faces.new(new_verts)
        except ValueError:
            continue
        for src, dst in zip(f.loops, nf.loops):
            dst[part_layer].uv = src[layer].uv
    bmesh.ops.delete(bm, geom=chosen, context='FACES')
    loose = [v for v in bm.verts if not v.link_faces]
    bmesh.ops.delete(bm, geom=loose, context='VERTS')
    part.verts.ensure_lookup_table()
    return part


def in_magazine(f):
    c = f.calc_center_median()
    return MAG_Y[0] <= c.y <= MAG_Y[1] and c.z < MAG_TOP_Z and abs(c.x) < 0.04


def in_optic(f):
    c = f.calc_center_median()
    return OPTIC_Y[0] <= c.y <= OPTIC_Y[1] and c.z > OPTIC_BOTTOM_Z and abs(c.x) < 0.03


# ------------------------------------------------------------------------------------------------------------ textures
MATERIAL_TILE = {"L_lambert2SG1": (0, 0), "L3_polySurface79SG1": (1, 0), "L_lambert4SG1": (0, 1)}   # tile column, tile row in the atlas
PARTS_ORDER = ["L_lambert2SG1", "L3_polySurface79SG1", "L_lambert4SG1"]


def load_pixels(path, size):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"
    if img.size[0] != size or img.size[1] != size:
        img.scale(size, size)
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(h, w, 4)


def gray(a):
    return a[:, :, :3].mean(axis=2)


def save_png(arr, path, name, data):
    h, w = arr.shape[0], arr.shape[1]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color" if data else "sRGB"
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def build_atlas(out_dir):
    base = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32)
    normal = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32)
    normal[:, :, 0] = 0.5
    normal[:, :, 1] = 0.5
    normal[:, :, 2] = 1.0
    normal[:, :, 3] = 1.0
    ms = np.zeros((ATLAS, ATLAS, 4), dtype=np.float32)
    ms[:, :, 1] = 1.0
    ms[:, :, 3] = 0.5
    base[:, :, 3] = 1.0
    folder = os.path.join(DOWNLOAD, "textures")
    for material in PARTS_ORDER:
        col, row = MATERIAL_TILE[material]
        window = (slice(row * TILE, (row + 1) * TILE), slice(col * TILE, (col + 1) * TILE))
        base[window] = load_pixels(os.path.join(folder, material + "_albedo.jpeg"), TILE)
        base[window][:, :, 3] = 1.0
        normal[window] = load_pixels(os.path.join(folder, material + "_normal.png"), TILE)
        normal[window][:, :, 3] = 1.0
        tile = np.ones((TILE, TILE, 4), dtype=np.float32)
        tile[:, :, 0] = gray(load_pixels(os.path.join(folder, material + "_metallic.jpeg"), TILE))
        ao_path = os.path.join(folder, material + "_AO.jpeg")
        tile[:, :, 1] = gray(load_pixels(ao_path, TILE)) if os.path.exists(ao_path) else 1.0
        tile[:, :, 2] = 0.0
        tile[:, :, 3] = 1.0 - gray(load_pixels(os.path.join(folder, material + "_roughness.jpeg"), TILE))
        ms[window] = tile
    save_png(base, os.path.join(out_dir, "T_WeaponSmg_BaseColor.png"), "T_WeaponSmg_BaseColor", False)
    save_png(normal, os.path.join(out_dir, "T_WeaponSmg_Normal.png"), "T_WeaponSmg_Normal", True)
    save_png(ms, os.path.join(out_dir, "T_WeaponSmg_MetalSmooth.png"), "T_WeaponSmg_MetalSmooth", True)


def to_atlas_uv(bm, material):
    col, row = MATERIAL_TILE[material]
    layer = bm.loops.layers.uv.active
    for f in bm.faces:
        for loop in f.loops:
            u, v = loop[layer].uv
            loop[layer].uv = ((u % 1.0 if u > 1.0 or u < 0.0 else u) * 0.5 + col * 0.5, (v % 1.0 if v > 1.0 or v < 0.0 else v) * 0.5 + row * 0.5)


# ------------------------------------------------------------------------------------------------------------ build
bpy.ops.wm.read_factory_settings(use_empty=True)
parts = read_dae()
raw = {}
for material in PARTS_ORDER:
    bm = build_part(material, parts[material])
    layer = bm.loops.layers.uv.active
    uvs = np.array([loop[layer].uv for f in bm.faces for loop in f.loops])
    report[material + "_uv_range"] = [float(uvs.min()), float(uvs.max())]
    report[material + "_triangles"] = tri_count(bm)
    raw[material] = bm
log("read: " + ", ".join("%s %d tris" % (k, tri_count(v)) for k, v in raw.items()))

# Reference points, in model units: the muzzle device is coaxial with the bore, and the barrel's front is the forward limit.
every = [v.co for bm in raw.values() for v in bm.verts]
front = max(v.y for v in every)
muzzle_ring = [v for v in every if v.y > front - 40]
bore_z = (max(v.z for v in muzzle_ring) + min(v.z for v in muzzle_ring)) / 2
centre_x = (max(v.x for v in muzzle_ring) + min(v.x for v in muzzle_ring)) / 2
report["model_front_y"] = front
report["model_bore_z"] = bore_z
report["model_centre_x"] = centre_x

def to_gun_frame(co):
    return mathutils.Vector(((co.x - centre_x) * SCALE, MUZZLE_Y + (co.y - front) * SCALE, (co.z - bore_z) * SCALE + BORE_Z))


# The red-dot lens is a disc at the front of the optic; its centre is the sight line, and the optic's rear end is where the eye looks in.
lens_points = [to_gun_frame(v.co) for v in raw["L_lambert4SG1"].verts]
lens_centre = sum(lens_points, mathutils.Vector()) / len(lens_points)
optic_rear = min(to_gun_frame(v.co).y for bm in raw.values() for v in bm.verts
                 if OPTIC_Y[0] - 0.01 <= to_gun_frame(v.co).y <= OPTIC_Y[1] and to_gun_frame(v.co).z > 0.07)
report["lens_centre_gun_frame"] = [lens_centre.x, lens_centre.y, lens_centre.z]
report["optic_rear_y"] = optic_rear
log("red dot: lens centre x %.4f y(forward) %.4f z(up) %.4f, optic rear at y %.4f" % (lens_centre.x, lens_centre.y, lens_centre.z, optic_rear))

merged = bmesh.new()
atlas_layer = merged.loops.layers.uv.new("UVMap")
for material in PARTS_ORDER:
    bm = raw[material]
    to_atlas_uv(bm, material)
    me = bpy.data.meshes.new("part")
    bm.to_mesh(me)
    bm.free()
    merged.from_mesh(me)
    bpy.data.meshes.remove(me)
for v in merged.verts:
    v.co = to_gun_frame(v.co)
merged.verts.ensure_lookup_table()
merged.normal_update()
xs = [v.co.x for v in merged.verts]
ys = [v.co.y for v in merged.verts]
zs = [v.co.z for v in merged.verts]
report["scaled_bounds"] = {"x": [min(xs), max(xs)], "y": [min(ys), max(ys)], "z": [min(zs), max(zs)]}
log("scaled: x %.3f..%.3f  y %.3f..%.3f  z %.3f..%.3f" % (min(xs), max(xs), min(ys), max(ys), min(zs), max(zs)))

bmesh.ops.recalc_face_normals(merged, faces=merged.faces)   # one consistent outward winding: Unity's mesh colliders and culling are one-sided
caps = cut_stock(merged) if STOCK_CUT_Y is not None else 0
report["stock_cap_faces"] = caps
magazine = split_faces(merged, in_magazine)
optic = split_faces(merged, in_optic)
log("magazine %d tris and optic %d tris after the split; gun %d tris" % (tri_count(magazine), tri_count(optic), tri_count(merged)))
gun, gun_dissolved = decimate(merged, GUN_TRIS)
optic_thin, optic_dissolved = decimate(optic, OPTIC_TRIS)
mag, mag_dissolved = decimate(magazine, MAG_TRIS)
# The optic goes back into the gun's mesh (it is one rigid part of it).
RAIL_TOP_Z = 0.0585      # gun frame: the top of the receiver's rail; the source model hovers the optic's riser (from 0.068) about 1 cm above it
optic_layer = optic_thin.loops.layers.uv.active
gun_layer = gun.loops.layers.uv.active
copied = {}
for f in optic_thin.faces:
    ring = []
    for v in f.verts:
        if v.index not in copied:
            copied[v.index] = gun.verts.new(v.co)
        ring.append(copied[v.index])
    nf = gun.faces.new(ring)
    for src, dst in zip(f.loops, nf.loops):
        dst[gun_layer].uv = src[optic_layer].uv
# Bridge the gap with a small mounting block (it uses the uv of the receiver's top, so it takes the receiver's own texels).
bottom = min([v.co.z for v in optic_thin.verts if v.co.z > 0.068] or [RAIL_TOP_Z])
if bottom - RAIL_TOP_Z > 0.001:
    low = [v.co for v in optic_thin.verts if bottom <= v.co.z < bottom + 0.003]
    y0, y1 = min(v.y for v in low), max(v.y for v in low)
    margin = (y1 - y0) * 0.2
    y0, y1 = y0 + margin, y1 - margin
    top_uv = mathutils.Vector((0, 0))
    top_faces = [f for f in gun.faces if abs(f.calc_center_median().z - RAIL_TOP_Z) < 0.003 and abs(f.calc_center_median().x) < 0.01]
    for f in top_faces:
        top_uv += f.loops[0][gun_layer].uv
    top_uv = top_uv / max(1, len(top_faces))
    box = bmesh.ops.create_cube(gun, size=1.0)
    verts = box["verts"]
    for v in verts:
        v.co = mathutils.Vector((0.006 if v.co.x > 0 else -0.006, y1 if v.co.y > 0 else y0, bottom + 0.001 if v.co.z > 0 else RAIL_TOP_Z - 0.001))
    for f in {f for v in verts for f in v.link_faces}:
        for loop in f.loops:
            loop[gun_layer].uv = top_uv
    report["optic_mount"] = {"rail_top_z": RAIL_TOP_Z, "optic_bottom_z": bottom, "y": [y0, y1]}
for bm_free in (merged, magazine, optic, optic_thin):
    bm_free.free()
report["gun_triangles"] = tri_count(gun)
report["magazine_triangles"] = tri_count(mag)
log("thinned: gun with optic %d tris (%d after dissolving; optic %d), magazine %d tris" % (tri_count(gun), gun_dissolved, tri_count(gun) - GUN_TRIS, tri_count(mag)))


def make_object(name, bm, material):
    for f in bm.faces:
        f.smooth = True
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    me.materials.append(material)
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


atlas_dir = TEX_OUT if WRITE else os.path.join(REVIEW, "textures")
os.makedirs(atlas_dir, exist_ok=True)
build_atlas(atlas_dir)
mat = bpy.data.materials.new("M_Weapon_Smg")
mat.use_nodes = True
tex = mat.node_tree.nodes.new("ShaderNodeTexImage")
tex.image = bpy.data.images.load(os.path.join(atlas_dir, "T_WeaponSmg_BaseColor.png"))
mat.node_tree.links.new(tex.outputs["Color"], next(n for n in mat.node_tree.nodes if n.type == 'BSDF_PRINCIPLED').inputs["Base Color"])
gun_object = make_object("Smg_Gun", gun, mat)
mag_object = make_object("Smg_Magazine", mag, mat)

# ------------------------------------------------------------------------------------------------------------ review pictures
scene = bpy.context.scene
scene.render.engine = 'BLENDER_WORKBENCH'
scene.display.shading.light = 'STUDIO'
scene.display.shading.color_type = 'TEXTURE'
scene.render.resolution_x, scene.render.resolution_y = 1400, 700
camera = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scene.collection.objects.link(camera)
scene.camera = camera
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 0.78
centre = mathutils.Vector((0, 0.1, -0.03))
for name, direction in {"side": (1, 0, 0), "top": (0, 0, 1), "front": (0, 1, 0), "back": (0, -1, 0)}.items():
    d = mathutils.Vector(direction)
    camera.location = centre + d * 3
    camera.rotation_euler = (-d).to_track_quat('-Z', 'Y').to_euler()
    scene.render.filepath = os.path.join(REVIEW, "gun_" + name + ".png")
    bpy.ops.render.render(write_still=True)

# ------------------------------------------------------------------------------------------------------------ the shared source
# The two thinned parts, in the gun frame, are kept in a small .blend that build_winter_arms.py (the reload rig's gun and magazine) and the static
# viewmodel export below both read, so the three places that show the SMG always get the same geometry.
bpy.data.objects.remove(camera)
bpy.context.preferences.filepaths.save_version = 0
source_path = os.path.join(REPO, "Art", "Blender", "SM_IoerSmg.blend") if WRITE else os.path.join(REVIEW, "SM_IoerSmg.blend")
bpy.ops.wm.save_as_mainfile(filepath=source_path)
log("source " + source_path)


# ------------------------------------------------------------------------------------------------------------ the static viewmodel
def export_static_viewmodel():
    """SM_Weapon_Smg.fbx: the existing file's objects (Smg, GloveR, SleeveR, GloveL, SleeveL) with the Smg mesh replaced by the new gun + magazine."""
    bpy.ops.wm.open_mainfile(filepath=os.path.join(REPO, "Art", "Blender", "SM_Viewmodels.blend"))
    collection = bpy.data.collections["Smg"]
    objects = {o.name.split(".")[0]: o for o in collection.objects}
    assert set(objects) >= {"Smg", "GloveR", "SleeveR", "GloveL", "SleeveL"}, sorted(objects)
    with bpy.data.libraries.load(source_path) as (src, dst):
        dst.objects = ["Smg_Gun", "Smg_Magazine"]
    gun_ob, mag_ob = (bpy.data.objects[n] for n in ("Smg_Gun", "Smg_Magazine"))
    target = objects["Smg"]
    material = target.data.materials[0]
    joined = bmesh.new()
    for o in (gun_ob, mag_ob):
        joined.from_mesh(o.data)
    new_mesh = bpy.data.meshes.new("Smg")
    joined.to_mesh(new_mesh)
    joined.free()
    new_mesh.materials.append(material)
    for polygon in new_mesh.polygons:
        polygon.use_smooth = True
    target.data = new_mesh
    for o in (gun_ob, mag_ob):
        bpy.data.objects.remove(o)
    for i, o in enumerate(bpy.data.objects):
        o.name = "~tmp%d" % i
    for base, o in objects.items():
        o.name = base
        assert o.name == base
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects.values():
        o.select_set(True)
    out = os.path.join(FBX_OUT, "SM_Weapon_Smg.fbx")
    bpy.ops.export_scene.fbx(
        filepath=out, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=False, mesh_smooth_type='OFF',
        use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False,
        bake_anim=False, add_leaf_bones=False, use_custom_props=False)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Art", "Blender", "SM_Viewmodels.blend"))
    log("exported " + out)


if WRITE:
    export_static_viewmodel()

import json
with open(os.path.join(REVIEW, "report.json"), "w") as f:
    json.dump(report, f, indent=2)
log(json.dumps(report))
