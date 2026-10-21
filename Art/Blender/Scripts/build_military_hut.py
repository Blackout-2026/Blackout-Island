"""Military hut entrance and bunker styling kit (step B1).

Run: blender -b --factory-startup --python build_military_hut.py -- <extracted download folder> <repo root>

The download (Military_Hut.zip, Sketchfab) is a 16 m long arched hut with six animations (a sliding front door, blast shutters, blast doors, two
cameras and a garage door) on one armature. The game keeps its own doors (the cyberpunk door, step D1), so this script keeps only what shows
from the compound yard and cuts the rest, then writes three things:

  SM_Hut_Entrance.fbx   the static front of the hut: arched roof, front wall, the recessed porch, the pad, windows, bollards, front signs and keypad.
                        Scaled by SCALE and moved so the origin is the middle of the door sill, X across, +Y up, the porch towards -Z of the door.
                        The roof is cut off at REAR_CUT, where the compound's retaining wall hides it, and the porch at VESTIBULE_END, where the
                        bunker's lobby wall begins.
  SM_Hut_Props.fbx      wall props for the bunker rooms: fuse box, camera, keypad and two warning signs. Metres, origin at the middle of the face
                        that touches the wall, facing +Y (the game's +Z, so they face the room once placed).
  textures              T_Hut_<Name>_{BaseColor,Normal,MetalSmooth}.png in Assets/Textures/Models (MetalSmooth: R metallic, G occlusion,
                        A smoothness = 1 - roughness), and two tileable sets for the bunker's concrete walls and floors in
                        Assets/Textures/Environment/Concrete and /ConcreteStained.
"""
import bpy
import bmesh
import os
import sys
import mathutils
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
SRC, REPO = args[0], args[1]

SCALE = 1.2                 # door 1.23 x 2.13 -> 1.48 x 2.55; the arch (radius 5.8 -> 7.0) just contains the lobby's 12 x 3.4 m shell
DOOR_Y = -7.33              # the hut door's plane in the download (Blender axes: X across, Y depth, Z up; the front faces -Y)
FLOOR_Z = 0.063             # top of the hut's front concrete pad (the main slab under the hut is 0.12 higher)
VESTIBULE_END = -0.2        # in output metres: the lobby wall's outer face (D-Bunker is at 0)
REAR_CUT = 15.0             # output metres behind the door plane: z 63.2, inside the compound's retaining wall (z 62 ... 63.4)
PAD_BACK = -1.4             # the pad is kept only in front of the front wall

TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
ENV_DIR = os.path.join(REPO, "Assets", "Textures", "Environment")
os.makedirs(TEX_DIR, exist_ok=True)


def to_output(p):
    """Download space -> output space (scaled, origin at the door sill)."""
    return mathutils.Vector((p.x * SCALE, (p.y - DOOR_Y) * SCALE, (p.z - FLOOR_Z) * SCALE))


def parts_of(bm):
    seen = set()
    parts = []
    for v in bm.verts:
        if v.index in seen:
            continue
        stack = [v]
        seen.add(v.index)
        members = []
        while stack:
            a = stack.pop()
            members.append(a)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        parts.append(members)
    return parts


def world_bmesh(ob):
    bm = bmesh.new()
    bm.from_mesh(ob.data)
    bm.transform(ob.matrix_world)
    bm.verts.ensure_lookup_table()
    return bm


def keep_parts(bm, test):
    """Delete every loose part whose vertex list fails test(members)."""
    drop = [v for members in parts_of(bm) if not test(members) for v in members]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')


def clip_y(bm, y, keep_below):
    """Cut the mesh with the plane Y = y and drop one side (output space)."""
    geom = list(bm.verts) + list(bm.edges) + list(bm.faces)
    bmesh.ops.bisect_plane(bm, geom=geom, plane_co=(0.0, y, 0.0), plane_no=(0.0, 1.0, 0.0), dist=1e-5,
                           clear_outer=keep_below, clear_inner=not keep_below)


def make_object(name, bm, materials, collection):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for m in materials:
        me.materials.append(m)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    return ob


def tris(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


# ---------------------------------------------------------------- import
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(SRC, "Military_Hut.fbx"))
bpy.context.view_layer.update()
src = {o.name: o for o in bpy.context.scene.objects if o.type == 'MESH'}
mats = {m.name: m for m in bpy.data.materials}


def src_mats(name):
    return [m for m in src[name].data.materials]


entrance = bpy.data.collections.new("Entrance")
props = bpy.data.collections.new("Props")
bpy.context.scene.collection.children.link(entrance)
bpy.context.scene.collection.children.link(props)

# ---------------------------------------------------------------- entrance
out = {}


def build_entrance(name, source, modify=None):
    bm = world_bmesh(src[source])
    for v in bm.verts:
        v.co = to_output(v.co)
    if modify:
        modify(bm)
    bm.normal_update()
    if not bm.faces:
        raise RuntimeError(name + " is empty after cutting")
    out[name] = make_object(name, bm, src_mats(source), entrance)


build_entrance("Hut_Roof", "Roof", lambda bm: clip_y(bm, REAR_CUT, True))
build_entrance("Hut_FrontWall", "Front_Outside_Wall")
build_entrance("Hut_Porch", "Front_Entrace", lambda bm: clip_y(bm, VESTIBULE_END, True))
build_entrance("Hut_Pad", "Foundation", lambda bm: clip_y(bm, PAD_BACK, True))
build_entrance("Hut_WindowFrames", "Window_Frames")
build_entrance("Hut_Windows", "Windows")
build_entrance("Hut_Bollards", "Barricades", lambda bm: keep_parts(bm, lambda m: max(v.co.y for v in m) < -2.0 and abs(sum(v.co.x for v in m) / len(m)) > 1.0))
build_entrance("Hut_Signs", "Signs", lambda bm: keep_parts(bm, lambda m: max(v.co.y for v in m) < -1.0))
build_entrance("Hut_Keypad", "Keypads_SecurityLights", lambda bm: keep_parts(bm, lambda m: max(v.co.y for v in m) < 0.0 and min(v.co.y for v in m) > -1.0))

# ---------------------------------------------------------------- props (metres, no scale; origin at the wall-side face, facing -Y)


def build_prop(name, source, choose, faces_plus_y):
    """A prop from some loose parts of a downloaded object. In the download the prop faces +Y (back at its lowest Y) or -Y (back at its
    highest Y); in the output it always faces +Y, which the game reads as +Z, and its origin is the middle of its back face."""
    bm = world_bmesh(src[source])
    keep_parts(bm, choose)
    xs = [v.co.x for v in bm.verts]
    ys = [v.co.y for v in bm.verts]
    zs = [v.co.z for v in bm.verts]
    ox, oz = (min(xs) + max(xs)) * 0.5, (min(zs) + max(zs)) * 0.5
    oy = min(ys) if faces_plus_y else max(ys)
    turn = 1.0 if faces_plus_y else -1.0           # a camera that faces -Y is turned half a circle about Z
    for v in bm.verts:
        v.co = mathutils.Vector((turn * (v.co.x - ox), turn * (v.co.y - oy), v.co.z - oz))
    bm.normal_update()
    out[name] = make_object(name, bm, src_mats(source), props)


# Which way each one faces was read from where it is mounted in the download: the fuse box, keypad and notice are inside the hut and face the
# room (+Y), the warning sign is on the outside of the back wall (+Y), and the camera hangs on the front wall facing out (-Y).
build_prop("Hut_FuseBox", "Fuse_Box", lambda m: True, True)
build_prop("Hut_Camera", "Camera1", lambda m: True, False)
build_prop("Hut_KeypadProp", "Keypads_SecurityLights", lambda m: min(v.co.y for v in m) > -7.4 and max(v.co.y for v in m) < -7.0, True)
build_prop("Hut_SignNotice", "Signs", lambda m: -7.2 < min(v.co.y for v in m) < -7.0, True)
build_prop("Hut_SignWarning", "Signs", lambda m: min(v.co.y for v in m) > 7.9 and min(v.co.x for v in m) < 0.0, True)

# ---------------------------------------------------------------- textures
NAMES = {
    # material name in the download -> (file stem in the download, output name, max size)
    "Roof_map": ("Roof", "Roof", 1024),
    "Outside_Walls_map": ("Outside_Walls", "OutsideWalls", 1024),
    "Front_Entrance_map": ("Front_Entrance", "FrontEntrance", 1024),
    "Foundation_map": ("Foundation", "Foundation", 1024),
    "Window_Frames_map": ("Window_Frames", "WindowFrames", 512),
    "Windows_map": ("Windows", "Windows", 512),
    "Window_Glass_map": ("Window_Glass", "WindowGlass", 512),
    "Barricades_map": ("Barricades", "Barricades", 512),
    "Keypads_SecurityLight_Tracks_Brackets_mao": ("Keypads_SecurityLight_Tracks_Brackets_mao", "Keypads", 512),
    "Signs_map": ("Signs", "Signs", 512),
    "Fuse_Box_map": ("Fuse_Box", "FuseBox", 512),
    "Cameras_map": ("Cameras", "Cameras", 512),
}


def path_of(stem, kind):
    for ext in ("jpg", "png", "jpeg"):
        p = os.path.join(SRC, "Military_Hut_%s_map_%s.%s" % (stem, kind, ext))
        if os.path.exists(p):
            return p
        p = os.path.join(SRC, "Military_Hut_%s_%s.%s" % (stem, kind, ext))
        if os.path.exists(p):
            return p
    return None


def load_pixels(path, size=None, data=True):
    """Pixels of an image as a float32 (H, W, 4) array, optionally resized (a copy; the file is untouched)."""
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color" if data else "sRGB"
    if size and (img.size[0] != size or img.size[1] != size):
        img.scale(size, size)
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(h, w, 4)


def write_png(arr, path, name, data=True):
    h, w = arr.shape[0], arr.shape[1]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color" if data else "sRGB"
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def channel(arr, index):
    return arr[:, :, index]


def mean_gray(arr):
    return arr[:, :, :3].mean(axis=2)


def pack_metal_smooth(stem, size):
    """R metallic, G occlusion, A smoothness (1 - roughness): the layout URP Lit and the project's texture rules read."""
    r = path_of(stem, "Roughness")
    m = path_of(stem, "Metallic")
    a = path_of(stem, "AO")
    ref = load_pixels(r or m or a, size)
    h, w = ref.shape[0], ref.shape[1]
    out_arr = np.ones((h, w, 4), dtype=np.float32)
    out_arr[:, :, 0] = mean_gray(load_pixels(m, size)) if m else 0.0
    out_arr[:, :, 1] = mean_gray(load_pixels(a, size)) if a else 1.0
    out_arr[:, :, 2] = 0.0
    out_arr[:, :, 3] = (1.0 - mean_gray(ref)) if r else 0.5
    return out_arr


used = set()
for ob in out.values():
    for m in ob.data.materials:
        used.add(m.name)
written = []
for mat_name in sorted(used):
    stem, name, size = NAMES[mat_name]
    base = path_of(stem, "BaseColor")
    write_png(load_pixels(base, size, data=False), os.path.join(TEX_DIR, "T_Hut_%s_BaseColor.png" % name), "T_Hut_%s_BaseColor" % name, data=False)
    normal = path_of(stem, "Normal")
    if normal:
        write_png(load_pixels(normal, size), os.path.join(TEX_DIR, "T_Hut_%s_Normal.png" % name), "T_Hut_%s_Normal" % name)
    write_png(pack_metal_smooth(stem, size), os.path.join(TEX_DIR, "T_Hut_%s_MetalSmooth.png" % name), "T_Hut_%s_MetalSmooth" % name)
    written.append((mat_name, name, size, bool(normal)))

# Tileable surfaces for Build Material Library. The hut's roof and pad textures have worn edges (the roof's right edge is a dark strip), so a clean
# 512 px patch of each is mirrored in both directions: the copy meets the original edge to edge, so it tiles without a seam. The roof patch becomes
# Concrete (walls and ceilings), the pad patch ConcreteStained (floors).


def mirror_tile(patch, normal=False):
    """2 x 2 mirrored copy of a patch. In a normal map a mirror also flips the mirrored axis of the vector: R across, G down."""
    right = patch[:, ::-1].copy()
    if normal:
        right[:, :, 0] = 1.0 - right[:, :, 0]
    top = np.concatenate([patch, right], axis=1)
    bottom = top[::-1].copy()
    if normal:
        bottom[:, :, 1] = 1.0 - bottom[:, :, 1]
    return np.concatenate([top, bottom], axis=0)


def write_surface(folder, surface, maps):
    d = os.path.join(ENV_DIR, folder)
    os.makedirs(d, exist_ok=True)
    for kind, (arr, data) in maps.items():
        write_png(arr, os.path.join(d, "%s_%s.png" % (surface, kind)), "%s_%s" % (surface, kind), data=data)


RX0, RX1, RY0, RY1 = 256, 768, 256, 768    # the middle of the roof texture, 512 x 512
roof = lambda kind, data=True: mirror_tile(load_pixels(path_of("Roof", kind), 1024, data=data)[RY0:RY1, RX0:RX1], normal=(kind == "Normal"))
write_surface("Concrete", "Concrete", {
    "BaseColor": (roof("BaseColor", False), False),
    "Normal": (roof("Normal"), True),
    "Roughness": (roof("Roughness"), True),
    "AO": (roof("AO"), True),
})

# Pad patch (pixels, bottom row first): the clean concrete in the middle of the pad texture, away from its marking and the dark patch.
PX0, PX1, PY0, PY1 = 60, 572, 300, 812     # 512 x 512
pad = lambda kind, data: mirror_tile(load_pixels(path_of("Foundation", kind), 1024, data=data)[PY0:PY1, PX0:PX1])
write_surface("ConcreteStained", "ConcreteStained", {
    "BaseColor": (pad("BaseColor", False), False),
    "Roughness": (pad("Roughness", True), True),
    "AO": (pad("AO", True), True),
})

# ---------------------------------------------------------------- export
for ob in list(src.values()) + [o for o in bpy.context.scene.objects if o.type == 'ARMATURE']:
    bpy.data.objects.remove(ob)
bpy.context.view_layer.update()
blend_path = os.path.join(REPO, "Art", "Blender", "SM_Military_Hut.blend")
bpy.ops.wm.save_as_mainfile(filepath=blend_path)


def export(fbx_name, names):
    bpy.ops.object.select_all(action='DESELECT')
    for n in names:
        out[n].select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(REPO, "Assets", "Models", "Custom", fbx_name), use_selection=True, object_types={'MESH'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)


entrance_names = [n for n in out if n in {o.name for o in entrance.objects}]
prop_names = [n for n in out if n in {o.name for o in props.objects}]
export("SM_Hut_Entrance.fbx", entrance_names)
export("SM_Hut_Props.fbx", prop_names)

for n, ob in out.items():
    box = [ob.matrix_world @ mathutils.Vector(c) for c in ob.bound_box]
    print("%-18s tris=%5d x[%6.2f,%6.2f] y[%6.2f,%6.2f] z[%6.2f,%6.2f] mats=%s" % (
        n, tris(ob), min(b.x for b in box), max(b.x for b in box), min(b.y for b in box), max(b.y for b in box),
        min(b.z for b in box), max(b.z for b in box), [m.name for m in ob.data.materials]))
print("[Hut] textures", written)
print("[Hut]", blend_path)
