"""First-person weapons with the downloaded models (step VM1): the MP9 submachine gun, the M9 bayonet knife and the first-person arms.

Run: blender -b --factory-startup --python build_viewmodels.py -- <folder with the three download folders> <repo root> [preview folder]
  <folder> holds mp9-free, m9-bayonet-knife and first-person-arm-hand-fingerless-gloves, each with source/ and textures/ as downloaded.

The downloads are static meshes: the MP9 (15,001 triangles, with a long stock), the M9 knife, and one mesh with two open forearms and hands in
sleeves (16,432 triangles, no skeleton). This script turns them into the three viewmodels the game already has slots for, in the frames
docs/art-pipeline.md section 14.6 sets (origin near the receiver or the grip, the shot or the blade along +Y here, which is +Z in Unity):

  SM_Weapon_Smg.fbx     Smg (the MP9 without its stock), GloveR, SleeveR, GloveL, SleeveL. The muzzle is at y 0.33 (the old SMG's muzzle point).
  SM_Weapon_Knife.fbx   Knife (the M9), GloveR, SleeveR (round the handle), GloveL, SleeveL (open, below the blade, so the left hand is seen too).
  SM_Weapon_Sniper.fbx  Sniper (the existing rifle, kept), and the same arms on its pistol grip and forend instead of its old gloves.

The arms are bent here: the fingers are curled round a rod of the right size (a rod through the bend), then the arm is scaled, thinned to a
budget, cut in two at the wrist (Glove: the hand and the forearm, Sleeve: the cuff and the sleeve) and moved to the grip. Textures: the
downloaded maps are halved to 2048 and the mask maps repacked (R metallic, G occlusion, A smoothness) into T_WeaponSmg_*, T_WeaponKnife_* and
T_WeaponHands_* in Assets/Textures/Models.
"""
import bpy
import bmesh
import math
import os
import sys
import mathutils
import numpy as np

args = sys.argv[sys.argv.index("--") + 1:]
DOWNLOADS, REPO = args[0], args[1]
PREVIEW = args[2] if len(args) > 2 else None

HANDS_FBX = os.path.join(DOWNLOADS, "first-person-arm-hand-fingerless-gloves", "source", "hand final.fbx")
MP9_FBX = os.path.join(DOWNLOADS, "mp9-free", "source", "MP9.fbx")
M9_FBX = os.path.join(DOWNLOADS, "m9-bayonet-knife", "source", "M9.fbx")
TEX_OUT = os.path.join(REPO, "Assets", "Textures", "Models")
FBX_OUT = os.path.join(REPO, "Assets", "Models", "Custom")

HAND_SCALE = 0.42          # the download's hand is 0.46 long; 0.19 m is a real hand
HAND_TRIS = 2400           # per arm, after thinning (the download has 8,216)
SPLIT_Z = -0.17            # arm-local height where the sleeve cuff starts (the camo cuff is below it)
KNUCKLE_Z = 0.31           # arm-local height of the knuckles: the fingers start here
MP9_SCALE = 0.06           # the MP9's 5.0 units from muzzle to the back of the receiver are 0.30 m
MP9_TRIS = 4200
MUZZLE_Y = 0.33            # the SMG's muzzle point in the gun frame (WeaponDefinitions)
BORE_Z = 0.004
# Where the hands grip, in the gun frame (X right, Y forward, Z up, metres): the point the finger arc is round, the way the palm faces, and the
# way the forearm points to the fingers. rho is the finger arc's radius in arm units (rod radius = rho - 0.023).
GRIPS = {
    "Smg": {
        "R": {"at": (0.0, 0.147, -0.075), "palm": (-1, 0, 0), "fingers": (-0.25, 0.9, 0.35), "rho": 0.075},
        "L": {"at": (0.0, 0.268, -0.075), "palm": (1, 0, 0), "fingers": (0.50, 0.85, 0.30), "rho": 0.075},
    },
    # A reverse grip: the handle lies across the fingers along the knuckle line, the thumb towards the pommel, the forearm coming in from below
    # and the right. The left hand is open and low, seen at the edge of the screen.
    "Knife": {
        "R": {"at": (0.0, -0.0175, 0.0), "palm": (-0.85, 0, -0.5), "fingers": (-0.5, 0, 0.85), "rho": 0.055},
        "L": {"holder": True, "at": (-0.09, -0.12, 0.33), "palm": (0.55, 0.8, 0.1), "fingers": (0.3, 0.25, 0.92), "rho": 0.28},
    },
    "Sniper": {
        "R": {"at": (0.0, -0.10, -0.11), "palm": (-1, 0, 0), "fingers": (-0.25, 0.9, 0.35), "rho": 0.075},
        "L": {"at": (-0.01, 0.28, -0.03), "palm": (1, 0, 0), "fingers": (0.50, 0.85, 0.30), "rho": 0.085},
    },
}

# How the game holds each weapon (PlayerViewmodelSwap): the weapon's position and Euler angles in the holder, Unity axes. The preview puts the
# camera where the player's camera is, so the picture is the player's view.
FRAMING = {
    "Smg": ((0.12, -0.09, 0.36), (3.0, -10.0, -3.0)),
    "Knife": ((0.17, -0.13, 0.37), (-19.0, -33.0, 12.0)),
    "Sniper": ((0.12, -0.17, 0.62), (4.0, -16.0, -4.0)),
}


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
            members.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in seen:
                    seen.add(b.index)
                    stack.append(b)
        parts.append(members)
    return parts


def import_fbx(path):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.fbx(filepath=path)
    bpy.context.view_layer.update()
    return [o for o in bpy.data.objects if o not in before]


def world_bmesh(objects):
    bm = bmesh.new()
    for o in objects:
        if o.type != 'MESH':
            continue
        part = bmesh.new()
        part.from_mesh(o.data)
        part.transform(o.matrix_world)
        me = bpy.data.meshes.new("tmp")
        part.to_mesh(me)
        part.free()
        bm.from_mesh(me)
        bpy.data.meshes.remove(me)
    bm.verts.ensure_lookup_table()
    return bm


def delete_verts(bm, keep):
    drop = [v for v in bm.verts if not keep(v)]
    bmesh.ops.delete(bm, geom=drop, context='VERTS')


def decimated(bm, target_tris):
    """The mesh thinned to about target_tris triangles (edge collapse; UVs are carried)."""
    tri = bm.copy()
    bmesh.ops.triangulate(tri, faces=tri.faces[:])
    ratio = min(1.0, target_tris / max(1, len(tri.faces)))
    me = bpy.data.meshes.new("dec_src")
    tri.to_mesh(me)
    tri.free()
    ob = bpy.data.objects.new("dec_src", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("d", 'DECIMATE')
    mod.ratio = ratio
    mod.use_collapse_triangulate = True
    bpy.context.view_layer.update()
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    out_me = bpy.data.meshes.new_from_object(ev)
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    res = bmesh.new()
    res.from_mesh(out_me)
    bpy.data.meshes.remove(out_me)
    return res


# ------------------------------------------------------------------------------------------------------------------ the arms
bpy.ops.wm.read_factory_settings(use_empty=True)
hand_objects = import_fbx(HANDS_FBX)
hands_all = world_bmesh(hand_objects)
for o in hand_objects:
    bpy.data.objects.remove(o)
hand_parts = parts_of(hands_all)
assert len(hand_parts) == 2, "the hands mesh should be two loose arms"
# part 0 has y > 0 and its thumb on the low-y side with the palm towards -x: a RIGHT hand; part 1 is its mirror, a LEFT hand.
ARM_PARTS = {"R": hand_parts[0], "L": hand_parts[1]}


def arm_source(side):
    bm = hands_all.copy()
    bm.verts.ensure_lookup_table()
    keep = set(ARM_PARTS[side])
    delete_verts(bm, lambda v: v.index in keep)
    bm.verts.ensure_lookup_table()
    return bm


def arm_anchor(bm):
    """x of the finger centre line and the middle of the finger spread, at the knuckles."""
    band = [v.co for v in bm.verts if KNUCKLE_Z - 0.02 < v.co.z < KNUCKLE_Z + 0.02]
    top = [v.co for v in bm.verts if v.co.z > KNUCKLE_Z + 0.03]
    return sum(p.x for p in band) / len(band), sum(p.y for p in top) / len(top)


def bend_fingers(bm, x0, rho):
    """Curl everything above the knuckles round a rod through (x0 - rho, *, KNUCKLE_Z) on the palm side (-x for these arms)."""
    for v in bm.verts:
        if v.co.z <= KNUCKLE_Z:
            continue
        u = v.co.z - KNUCKLE_Z
        d = x0 - v.co.x                 # distance from the finger centre line, towards the palm
        r = rho - d
        theta = u / rho
        v.co.z = KNUCKLE_Z + r * math.sin(theta)
        v.co.x = x0 - (rho - r * math.cos(theta))


def basis(palm, fingers):
    """Rotation taking arm axes (x: back of the hand, y: across, z: towards the fingers) to the gun frame: -x -> palm, z -> fingers."""
    p = mathutils.Vector(palm).normalized()
    f = mathutils.Vector(fingers)
    f = (f - f.dot(p) * p).normalized()
    x = -p
    y = f.cross(x)
    return mathutils.Matrix((x, y, f)).transposed()      # columns are the images of the arm's x, y, z


def to_weapon_frame(label, spec):
    """A grip given in the holder's own frame (Unity axes: x right, y up, z forward) as a grip in the weapon's frame (Blender axes)."""
    pos, euler = FRAMING[label]
    inv = unity_rotation(euler).T
    point = lambda v: inv @ (np.array(v) - np.array(pos))
    direction = lambda v: inv @ np.array(v)
    swap = lambda v: (float(v[0]), float(v[2]), float(v[1]))
    out = dict(spec)
    out["at"] = swap(point(spec["at"]))
    out["palm"] = swap(direction(spec["palm"]))
    out["fingers"] = swap(direction(spec["fingers"]))
    out["holder"] = False
    return out


def build_arm(side, spec):
    bm = arm_source(side)
    x0, yc = arm_anchor(bm)
    bend_fingers(bm, x0, spec["rho"])
    # the finger arc's centre is the origin of the arm's own frame
    centre = mathutils.Vector((x0 - spec["rho"], yc, KNUCKLE_Z))
    for v in bm.verts:
        v.co = v.co - centre
    thin = decimated(bm, HAND_TRIS)
    bm.free()
    rot = basis(spec["palm"], spec["fingers"]).to_4x4()
    move = mathutils.Matrix.Translation(spec["at"]) @ rot @ mathutils.Matrix.Scale(HAND_SCALE, 4)
    parts = {}
    for name, below in (("Glove" + side, False), ("Sleeve" + side, True)):
        piece = thin.copy()
        cut = [f for f in piece.faces if ((sum(v.co.z for v in f.verts) / len(f.verts)) + centre.z < SPLIT_Z) != below]
        bmesh.ops.delete(piece, geom=cut, context='FACES')
        loose = [v for v in piece.verts if not v.link_faces]
        bmesh.ops.delete(piece, geom=loose, context='VERTS')
        piece.transform(move)
        piece.normal_update()
        parts[name] = piece
    thin.free()
    return parts


# ------------------------------------------------------------------------------------------------------------------ the guns
def smooth(bm):
    for f in bm.faces:
        f.smooth = True


def build_mp9():
    objs = import_fbx(MP9_FBX)
    bm = world_bmesh(objs)
    for o in objs:
        bpy.data.objects.remove(o)
    ox, muzzle_y, bore_z = 2.049, -4.334, 1.09
    delete_verts(bm, lambda v: v.co.y < 0.9)       # the stock
    thin = decimated(bm, MP9_TRIS)
    bm.free()
    for v in thin.verts:
        v.co = mathutils.Vector((-(v.co.x - ox) * MP9_SCALE, MUZZLE_Y - (v.co.y - muzzle_y) * MP9_SCALE, (v.co.z - bore_z) * MP9_SCALE + BORE_Z))
    # the point order of the faces flips with the half turn only if the transform mirrors; it does not (x and y both change sign)
    thin.normal_update()
    return thin


def build_sniper():
    objs = import_fbx(os.path.join(FBX_OUT, "SM_Weapon_Sniper.fbx"))
    bm = world_bmesh([o for o in objs if o.name == "Sniper"])
    for o in objs:
        bpy.data.objects.remove(o)
    smooth(bm)
    return bm


def build_m9():
    objs = import_fbx(M9_FBX)
    bm = world_bmesh(objs)
    for o in objs:
        bpy.data.objects.remove(o)
    for v in bm.verts:
        v.co = mathutils.Vector((-v.co.x, -v.co.y, v.co.z))     # the tip along +Y, origin 0.04 m in front of the pommel's end of the grip
    bm.normal_update()
    return bm


# ------------------------------------------------------------------------------------------------------------------ materials, textures
def pixels(path, size):
    img = bpy.data.images.load(path)
    img.colorspace_settings.name = "Non-Color"
    if img.size[0] != size:
        img.scale(size, size)
    w, h = img.size
    arr = np.empty(w * h * 4, dtype=np.float32)
    img.pixels.foreach_get(arr)
    bpy.data.images.remove(img)
    return arr.reshape(h, w, 4)


def save_png(arr, path, name, data):
    h, w = arr.shape[0], arr.shape[1]
    img = bpy.data.images.new(name, w, h, alpha=True)
    img.colorspace_settings.name = "Non-Color" if data else "sRGB"
    img.pixels.foreach_set(np.ascontiguousarray(arr, dtype=np.float32).ravel())
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()
    bpy.data.images.remove(img)


def gray(a):
    return a[:, :, :3].mean(axis=2)


def write_textures():
    d = os.path.join(DOWNLOADS, "mp9-free", "textures")
    base = pixels(os.path.join(d, "MP9_diffuse.png"), 2048)
    base[:, :, 3] = 1.0
    save_png(base, os.path.join(TEX_OUT, "T_WeaponSmg_BaseColor.png"), "T_WeaponSmg_BaseColor", False)
    save_png(pixels(os.path.join(d, "MP9_normalmap.png"), 2048), os.path.join(TEX_OUT, "T_WeaponSmg_Normal.png"), "T_WeaponSmg_Normal", True)
    ms = np.ones((2048, 2048, 4), dtype=np.float32)
    ms[:, :, 0] = gray(pixels(os.path.join(d, "MP9_metalness.png"), 2048))
    ms[:, :, 1] = gray(pixels(os.path.join(d, "MP9_AO.png"), 2048))
    ms[:, :, 2] = 0.0
    ms[:, :, 3] = 1.0 - gray(pixels(os.path.join(d, "MP9_roughness.png"), 2048))
    save_png(ms, os.path.join(TEX_OUT, "T_WeaponSmg_MetalSmooth.png"), "T_WeaponSmg_MetalSmooth", True)

    d = os.path.join(DOWNLOADS, "m9-bayonet-knife", "textures")
    base = pixels(os.path.join(d, "M9_BaseColor.png"), 2048)
    base[:, :, 3] = 1.0
    save_png(base, os.path.join(TEX_OUT, "T_WeaponKnife_BaseColor.png"), "T_WeaponKnife_BaseColor", False)
    save_png(pixels(os.path.join(d, "M9_Normal.png"), 2048), os.path.join(TEX_OUT, "T_WeaponKnife_Normal.png"), "T_WeaponKnife_Normal", True)
    ms = np.ones((2048, 2048, 4), dtype=np.float32)
    ms[:, :, 0] = gray(pixels(os.path.join(d, "M9_Metallic.png"), 2048))
    ms[:, :, 1] = gray(pixels(os.path.join(d, "M9_Ambient_Occulsion.png"), 2048))
    ms[:, :, 2] = 0.0
    ms[:, :, 3] = 1.0 - gray(pixels(os.path.join(d, "M9_Roughness.png"), 2048))
    save_png(ms, os.path.join(TEX_OUT, "T_WeaponKnife_MetalSmooth.png"), "T_WeaponKnife_MetalSmooth", True)

    d = os.path.join(DOWNLOADS, "first-person-arm-hand-fingerless-gloves", "textures")
    base = pixels(os.path.join(d, "texture_basecolor.png"), 2048)
    base[:, :, 3] = 1.0
    save_png(base, os.path.join(TEX_OUT, "T_WeaponHands_BaseColor.png"), "T_WeaponHands_BaseColor", False)
    save_png(pixels(os.path.join(d, "canvas.png"), 2048), os.path.join(TEX_OUT, "T_WeaponHands_Normal.png"), "T_WeaponHands_Normal", True)


def material(name, base_png, normal_png):
    m = bpy.data.materials.new(name)
    m.use_nodes = True
    nt = m.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    t = nt.nodes.new("ShaderNodeTexImage")
    t.image = bpy.data.images.load(base_png)
    nt.links.new(t.outputs[0], bsdf.inputs["Base Color"])
    return m


# ------------------------------------------------------------------------------------------------------------------ assemble and export
def make_object(name, bm, mat, collection):
    smooth(bm)
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    me.materials.append(mat)
    ob = bpy.data.objects.new(name, me)
    collection.objects.link(ob)
    return ob


def tri_count(ob):
    return sum(len(p.vertices) - 2 for p in ob.data.polygons)


def build_weapon(label, body_bm, body_mat, hands_mat, grips):
    col = bpy.data.collections.new(label)
    bpy.context.scene.collection.children.link(col)
    objs = {label: make_object(label, body_bm, body_mat, col)}
    for side, spec in grips.items():
        if os.environ.get("VM_ONLY") and os.environ["VM_ONLY"] != side:
            continue
        if spec.get("holder"):
            spec = to_weapon_frame(label, spec)
        for name, piece in build_arm(side, spec).items():
            objs[name] = make_object(name, piece, hands_mat, col)
    return col, objs


def export(fbx_name, objects):
    # Blender object names are unique in the file, so the second weapon's GloveR would be GloveR.001: give every object a throw-away name first,
    # then this weapon's objects their own names, which is what the FBX (and the game, which finds the arms by name) must have.
    for i, o in enumerate(bpy.data.objects):
        o.name = "~tmp%d" % i
    for base, o in objects.items():
        o.name = base
        assert o.name == base, (base, o.name)
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects.values():
        o.select_set(True)
    bpy.ops.export_scene.fbx(
        filepath=os.path.join(FBX_OUT, fbx_name), use_selection=True, object_types={'MESH'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)


CENTRE = {"smg": (0.0, 0.16, -0.05), "knife": (0.0, 0.08, 0.0), "sniper": (0.0, 0.2, 0.0)}


def unity_rotation(euler):
    """3x3 matrix of Unity's Quaternion.Euler(x, y, z) (z first, then x, then y; left-handed) acting on column vectors in Unity axes."""
    x, y, z = (math.radians(a) for a in euler)
    rx = np.array([[1, 0, 0], [0, math.cos(x), -math.sin(x)], [0, math.sin(x), math.cos(x)]])
    ry = np.array([[math.cos(y), 0, math.sin(y)], [0, 1, 0], [-math.sin(y), 0, math.cos(y)]])
    rz = np.array([[math.cos(z), -math.sin(z), 0], [math.sin(z), math.cos(z), 0], [0, 0, 1]])
    return ry @ rx @ rz


def player_eye(label):
    """Where the player's camera is, and where it looks, in the weapon's own frame (Blender axes)."""
    pos, euler = FRAMING[label]
    r = unity_rotation(euler)
    inv = r.T
    eye = inv @ (-np.array(pos))
    fwd = inv @ np.array([0.0, 0.0, 1.0])
    up = inv @ np.array([0.0, 1.0, 0.0])
    to_blender = lambda v: mathutils.Vector((v[0], v[2], v[1]))
    return to_blender(eye), to_blender(fwd), to_blender(up)


CENTRE = {"Smg": (0.0, 0.16, -0.05), "Knife": (0.0, 0.08, 0.0), "Sniper": (0.0, 0.2, 0.0)}


def render_preview(label, folder, size=(900, 540)):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"
    sc.display.shading.color_type = "TEXTURE"
    sc.render.resolution_x, sc.render.resolution_y = size
    sc.world = bpy.data.worlds.new("w")
    sc.world.color = (0.25, 0.28, 0.33)
    cd = bpy.data.cameras.new("c")
    cam = bpy.data.objects.new("c", cd)
    sc.collection.objects.link(cam)
    sc.camera = cam
    cd.sensor_fit = 'VERTICAL'
    cd.clip_start = 0.02
    focus = mathutils.Vector(CENTRE[label])
    eye, fwd, up = player_eye(label)
    views = {"game": (eye, eye + fwd, up, 72), "side": (focus + mathutils.Vector((0.7, 0, 0.05)), focus, None, 30),
             "left": (focus + mathutils.Vector((-0.7, 0, 0.05)), focus, None, 30),
             "angleR": (focus + mathutils.Vector((0.45, -0.45, 0.3)), focus, None, 34), "angleL": (focus + mathutils.Vector((-0.45, -0.45, 0.3)), focus, None, 34),
             "top": (focus + mathutils.Vector((0, 0.05, 0.8)), focus, None, 30), "front": (focus + mathutils.Vector((0.1, 0.8, 0.1)), focus, None, 30)}
    for k, (e, l, upv, fov) in views.items():
        cam.location = mathutils.Vector(e)
        direction = mathutils.Vector(l) - cam.location
        if upv is None:
            cam.rotation_euler = direction.to_track_quat("-Z", "Y").to_euler()
        else:
            # look along direction with the given up vector
            zaxis = -direction.normalized()
            xaxis = upv.cross(zaxis).normalized()
            yaxis = zaxis.cross(xaxis)
            cam.matrix_world = mathutils.Matrix(((xaxis.x, yaxis.x, zaxis.x, e.x), (xaxis.y, yaxis.y, zaxis.y, e.y), (xaxis.z, yaxis.z, zaxis.z, e.z), (0, 0, 0, 1)))
        cd.angle_y = math.radians(fov)
        sc.render.filepath = os.path.join(folder, "%s_%s.png" % (label.lower(), k))
        bpy.ops.render.render(write_still=True)
    bpy.data.objects.remove(cam)


if not os.environ.get("VM_SKIP_TEXTURES"):
    write_textures()
smg_mat = material("M_Weapon_Smg", os.path.join(TEX_OUT, "T_WeaponSmg_BaseColor.png"), None)
knife_mat = material("M_Weapon_Knife", os.path.join(TEX_OUT, "T_WeaponKnife_BaseColor.png"), None)
sniper_mat = material("M_Weapon_Sniper", os.path.join(TEX_OUT, "T_WeaponSniper_BaseColor.png"), None)
hands_mat = material("M_Weapon_Hands", os.path.join(TEX_OUT, "T_WeaponHands_BaseColor.png"), None)

ONLY = os.environ.get("VM_WEAPON")
sniper_body = build_sniper() if (not ONLY or ONLY == "Sniper") else None     # before the sniper file is rewritten below
WEAPONS = [("Smg", build_mp9, smg_mat, "SM_Weapon_Smg.fbx"), ("Knife", build_m9, knife_mat, "SM_Weapon_Knife.fbx"),
           ("Sniper", lambda: sniper_body, sniper_mat, "SM_Weapon_Sniper.fbx")]
built = {}
for label, make_body, body_mat, fbx in WEAPONS:
    if ONLY and ONLY != label:
        continue
    col, objects = build_weapon(label, make_body(), body_mat, hands_mat, GRIPS[label])
    built[fbx] = objects
    if PREVIEW:
        os.makedirs(PREVIEW, exist_ok=True)
        for o in bpy.data.objects:
            o.hide_render = o not in objects.values()
        render_preview(label, PREVIEW)
        for o in bpy.data.objects:
            o.hide_render = False
    for o in objects.values():
        o.hide_render = False
for fbx, objects in built.items():
    export(fbx, objects)
    print("[Viewmodels]", fbx, {n: tri_count(o) for n, o in objects.items()}, "total", sum(tri_count(o) for o in objects.values()))
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, "Art", "Blender", "SM_Viewmodels.blend"))
