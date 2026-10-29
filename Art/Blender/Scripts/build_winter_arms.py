"""WA1. Extract the supplied Biped arms; retarget existing FPS reload motion offline.

blender -b --factory-startup --python build_winter_arms.py -- <repo> <source-folder> <evidence-folder>
Inputs, including the existing reload .blend, are read-only. No texture rebake,
decimation or automatic weights. Native extraction retains original groups/UVs.
Fitted copies merge twist/helper weights into the corresponding FPS joint.
"""
import bpy
import bmesh
import sys
import os
import json
import math
from mathutils import Matrix, Vector

REPO, SOURCE, EVIDENCE = sys.argv[sys.argv.index('--') + 1:]
OUT = os.path.join(REPO, 'Assets/Models/Custom')
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=os.path.join(SOURCE, 'source/00000036.fbx'))
native_rig = next(o for o in bpy.context.scene.objects if o.type == 'ARMATURE')
body = next(o for o in bpy.context.scene.objects if o.type == 'MESH')
body.name = 'SourceBody'
material = bpy.data.materials.new('M_PlayerWinter_Body')
fingers = ('Thumb', 'Index', 'Middle', 'Ring', 'Little')
report = {'source': SOURCE, 'scale': 3, 'native': {}, 'fitted': {}}


def side_of(name):
    for side, letter in [('L', 'l'), ('R', 'r')]:
        if name.startswith('bip001 ' + letter + ' ') and any(k in name for k in ('upperarm', 'forearm', 'hand', 'finger')):
            return side
        if name.startswith('bone ' + letter + ' ') and any(k in name for k in ('upperarm', 'forearm')):
            return side
    return None


def dominant(v):
    return body.vertex_groups[max(v.groups, key=lambda g: g.weight).group].name


def extract(side):
    # Face-based deletion retains UV loops and source deform layers without welding seams.
    obj = body.copy()
    obj.data = body.data.copy()
    bpy.context.scene.collection.objects.link(obj)
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bm.verts.ensure_lookup_table()
    keep = {v.index for v in body.data.vertices if side_of(dominant(v)) == side}
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if not all(v.index in keep for v in f.verts)], context='FACES')
    bmesh.ops.delete(bm, geom=[v for v in bm.verts if not v.link_faces], context='VERTS')
    bm.to_mesh(obj.data)
    bm.free()
    obj.name = 'WinterArm' + side
    obj.data.materials.clear()
    obj.data.materials.append(material)
    for p in obj.data.polygons: p.material_index = 0
    assert obj.data.uv_layers and len(obj.data.vertices) > 200
    return obj


def export(path, objects, animated=False):
    bpy.ops.object.select_all(action='DESELECT')
    for o in objects: o.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH', 'ARMATURE'},
        global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
        axis_forward='-Z', axis_up='Y', bake_space_transform=False, mesh_smooth_type='OFF',
        add_leaf_bones=False, use_armature_deform_only=False, bake_anim=animated,
        bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False, bake_anim_simplify_factor=0,
        path_mode='AUTO', embed_textures=False)


native = {s: extract(s) for s in ('L', 'R')}
# Prune only this in-memory derivative's unused bones, preserving every deform group and its ancestors.
used = {o.vertex_groups[g.group].name for o in native.values() for v in o.data.vertices for g in v.groups if g.weight > 0}
for name in list(used):
    bone = native_rig.data.bones.get(name)
    while bone:
        used.add(bone.name)
        bone = bone.parent
bpy.context.view_layer.objects.active = native_rig
bpy.ops.object.mode_set(mode='EDIT')
for b in list(native_rig.data.edit_bones):
    if b.name not in used: native_rig.data.edit_bones.remove(b)
bpy.ops.object.mode_set(mode='OBJECT')
for side, o in native.items():
    report['native'][side] = {'vertices': len(o.data.vertices), 'triangles': sum(len(p.vertices)-2 for p in o.data.polygons)}
export(os.path.join(OUT, 'SM_WinterArms_Source.fbx'), [native_rig, *native.values()])

# Cache immutable source vertices, UV loops and exact input weights before loading the reference .blend.
source_data = {}
for side, obj in native.items():
    letter = side.lower()
    prefix = 'bip001 ' + letter + ' '
    point = lambda name: native_rig.matrix_world @ native_rig.data.bones[prefix + name].head_local
    wrist = point('hand')
    y = (point('finger2') - wrist).normalized()
    across = (point('finger4') - point('finger1')).normalized()
    x = (y.cross(across) if side == 'L' else across.cross(y)).normalized()
    z = x.cross(y).normalized()
    x = y.cross(z).normalized()
    axes = Matrix((x, y, z)).transposed()
    joints = {'Forearm': point('forearm'), 'Hand': wrist}
    for fi, finger in enumerate(fingers):
        for j in range(3): joints[finger + str(j+1)] = point('finger' + str(fi) + (str(j) if j else ''))
    def target_name(name):
        if 'finger' in name:
            suffix = name.split('finger')[1]
            return fingers[int(suffix[0])] + str(int(suffix[1:]) + 1 if suffix[1:] else 1) + side
        return ('Hand' if 'hand' in name else 'Forearm') + side
    vertices, weights = [], []
    for v in obj.data.vertices:
        vertices.append(tuple(obj.matrix_world @ v.co))
        mapped = {}
        for g in v.groups:
            name = obj.vertex_groups[g.group].name
            if g.weight > 0 and side_of(name) == side:
                key = target_name(name)
                mapped[key] = mapped.get(key, 0) + g.weight
        total = sum(mapped.values())
        assert total > 0
        weights.append({n: w/total for n, w in mapped.items()})
    # FPS view keeps elbow-to-fingertip geometry plus a small original upper-sleeve margin.
    toward_elbow = (joints['Forearm'] - wrist).normalized()
    length = (joints['Forearm'] - wrist).length
    # Retain a little original upper sleeve past the elbow so the cropped edge stays below the camera.
    eligible = {v.index for v in obj.data.vertices
                if (obj.matrix_world @ v.co - wrist).dot(toward_elbow) <= length + 0.02}
    faces = [p for p in obj.data.polygons if all(i in eligible for i in p.vertices)]
    indexes = sorted({i for p in faces for i in p.vertices})
    remap = {old: new for new, old in enumerate(indexes)}
    source_data[side] = ([vertices[i] for i in indexes], [tuple(remap[i] for i in p.vertices) for p in faces],
        [tuple(obj.data.uv_layers.active.data[i].uv) for p in faces for i in p.loop_indices],
        [weights[i] for i in indexes], joints, axes)

bpy.ops.wm.open_mainfile(filepath=os.path.join(REPO, 'Art/Blender/SM_Reload_Viewmodels.blend'))
for o in bpy.data.objects: o.hide_render = True
material = bpy.data.materials.get('M_PlayerWinter_Body') or bpy.data.materials.new('M_PlayerWinter_Body')
image = bpy.data.images.load(os.path.join(SOURCE, 'textures/00000037.png'), check_existing=True)
material.use_nodes = True
tex = material.node_tree.nodes.new('ShaderNodeTexImage')
tex.image = image
material.node_tree.links.new(tex.outputs['Color'], material.node_tree.nodes.get('Principled BSDF').inputs['Base Color'])


def fitted(side, rig, name, wrist):
    vertices, faces, uv, weights, joints, axes = source_data[side]
    coords = []
    for v, mapping in zip(vertices, weights):
        p = Vector(v)
        # One anatomical wrist fit preserves proportions and continuous surfaces.
        # Finger pivots are fitted separately; source weights drive those joints during reload.
        local = axes.inverted() @ (p - joints['Hand']) * 3
        coords.append(wrist @ local)
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(coords, [], faces)
    mesh.uv_layers.new(name='UVMap')
    for a, b in zip(mesh.uv_layers.active.data, uv): a.uv = b
    mesh.materials.append(material)
    for p in mesh.polygons: p.use_smooth = True
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    for bone in rig.data.bones:
        group = obj.vertex_groups.new(name=bone.name)
        for i, mapping in enumerate(weights):
            if mapping.get(bone.name, 0) > 0: group.add([i], mapping[bone.name], 'REPLACE')
    modifier = obj.modifiers.new('Existing FPS skin', 'ARMATURE')
    modifier.object = rig
    obj.parent = rig
    return obj


def copy_rig(original, label):
    rig = original.copy()
    rig.data = original.data.copy()
    rig.animation_data_clear()
    rig.name = 'Winter' + label + 'Rig'
    bpy.context.scene.collection.objects.link(rig)
    for p in rig.pose.bones: p.matrix_basis = Matrix.Identity(4)
    return rig


def anatomical_bind(rig, wrists):
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for side in ('L', 'R'):
        _, _, _, _, joints, axes = source_data[side]
        point = lambda key: wrists[side] @ (axes.inverted() @ (joints[key] - joints['Hand']) * 3)
        lower = rig.data.edit_bones['Forearm' + side]
        lower.head, lower.tail = point('Forearm'), point('Hand')
        for finger in fingers:
            for j in (1, 2, 3):
                key = finger + str(j)
                b = rig.data.edit_bones[key + side]
                b.head = point(key)
                b.tail = point(finger + str(j+1)) if j < 3 else b.head + (b.head - point(finger + '2')) * 0.8
                # Local X runs across the finger: rotation around X now flexes toward the palm.
                b.align_roll(wrists[side].to_3x3() @ Vector((1, 0, 0)))
    bpy.ops.object.mode_set(mode='OBJECT')


def grip(rig, arms, sides, tight=False):
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    desired = {}
    for b in rig.data.bones:
        rel = rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name]
        delta = Matrix.Identity(4)
        for side in sides:
            if b.name.endswith(side) and any(b.name.startswith(f) for f in fingers):
                amount = ((55, 75, 55) if tight else (30, 50, 35))[int(b.name[-2])-1]
                if b.name.startswith('Thumb'): amount = ((30, 35, 25) if tight else (12, 20, 12))[int(b.name[-2])-1]
                delta = Matrix.Rotation(math.radians(amount), 4, 'X')
        desired[b.name] = (desired[b.parent.name] if b.parent else Matrix.Identity(4)) @ rel @ delta
        rig.pose.bones[b.name].matrix = desired[b.name]
    bpy.context.view_layer.update()
    for arm in arms:
        evaluated = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for v, posed in zip(arm.data.vertices, evaluated.data.vertices): v.co = posed.co
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for b in rig.data.edit_bones: b.matrix = desired[b.name]
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in rig.pose.bones: p.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def bake(original, rig):
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    old_rest = {b.name: b.matrix_local.copy() for b in original.data.bones}
    for p in rig.pose.bones: p.rotation_mode = 'QUATERNION'
    for frame in range(1, 62):
        bpy.context.scene.frame_set(frame)
        bpy.context.view_layer.update()
        desired = {}
        for b in rig.data.bones:
            old = original.pose.bones[b.name]
            if any(b.name.startswith(f) for f in fingers):
                rel = rest[b.parent.name].inverted() @ rest[b.name]
                old_rel = old_rest[b.parent.name].inverted() @ old_rest[b.name]
                delta = old_rel.inverted() @ original.pose.bones[b.parent.name].matrix.inverted() @ old.matrix
                bend = delta.to_quaternion().angle
                # Preserve the source curve's timing while limiting flexion for the different hand proportions.
                bend = min(bend * 0.35, math.radians(12 if b.name.endswith('R') else 25))
                desired[b.name] = desired[b.parent.name] @ rel @ Matrix.Rotation(bend, 4, 'X')
            else:
                desired[b.name] = old.matrix @ old_rest[b.name].inverted() @ rest[b.name]
            parent_pose = desired[b.parent.name] if b.parent else Matrix.Identity(4)
            rel = rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name]
            rig.pose.bones[b.name].matrix_basis = rel.inverted() @ parent_pose.inverted() @ desired[b.name]
            rig.pose.bones[b.name].keyframe_insert('location', frame=frame)
            rig.pose.bones[b.name].keyframe_insert('rotation_quaternion', frame=frame)
    bpy.context.scene.render.fps = 60
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 61


outputs = []
for label in ('Smg', 'Rifle'):
    original = bpy.data.objects[label + '_Rig']
    rig = copy_rig(original, label)
    # Existing align_roll points the bone's Z at the back of the hand, not its X.
    wrists = {s: original.data.bones['Hand' + s].matrix_local @ Matrix.Rotation(-math.pi/2, 4, 'Y') for s in ('L', 'R')}
    anatomical_bind(rig, wrists)
    arms = [fitted(s, rig, 'Winter' + label + s, wrists[s]) for s in ('L', 'R')]
    grip(rig, arms, ('L', 'R'))
    parts = []
    for name in ('Gun', 'Magazine'):
        part = bpy.data.objects[label + '_' + name].copy()
        part.data = part.data.copy()
        part.parent = rig
        part.name = 'Winter' + label + name
        for mod in part.modifiers:
            if mod.type == 'ARMATURE': mod.object = rig
        bpy.context.scene.collection.objects.link(part)
        parts.append(part)
    bake(original, rig)
    bpy.context.scene.frame_set(1)
    export(os.path.join(OUT, 'SM_WinterArms_' + label + '.fbx'), [rig, *arms, *parts], True)
    outputs += [rig, *arms, *parts]
    report['fitted'][label] = {s: len(o.data.vertices) for s, o in zip(('L', 'R'), arms)}

# Knife retains its original grip frame and rigid elbow swing. Rotate both bind bones and mesh.
original = bpy.data.objects['Smg_Rig']
rig = copy_rig(original, 'Knife')
wrists = {s: original.data.bones['Hand' + s].matrix_local @ Matrix.Rotation(-math.pi/2, 4, 'Y') for s in ('L', 'R')}
anatomical_bind(rig, wrists)
arms = []
for side in ('L', 'R'):
    arm = fitted(side, rig, 'WinterKnife' + side, wrists[side])
    palm, fingers_dir = ((-0.85, 0, -0.5), (-0.5, 0, 0.85)) if side == 'R' else ((0.55, 0.8, 0.1), (0.3, 0.25, 0.92))
    at = Vector((0, -0.0175, 0) if side == 'R' else (-0.09, -0.12, 0.33))
    palm, forward = Vector(palm), Vector(fingers_dir)
    if side == 'L':
        # Original free-hand placement is specified in Unity holder space, not knife space.
        x, y, z = (math.radians(a) for a in (-19, -33, 12))
        r = Matrix.Rotation(y, 3, 'Y') @ Matrix.Rotation(x, 3, 'X') @ Matrix.Rotation(z, 3, 'Z')
        swap = lambda v: Vector((v.x, v.z, v.y))
        at = swap(r.transposed() @ (at - Vector((0.17, -0.13, 0.37))))
        palm, forward = swap(r.transposed() @ palm), swap(r.transposed() @ forward)
    back = -palm.normalized()
    forward = (forward - forward.dot(back) * back).normalized()
    basis = Matrix((back, forward, back.cross(forward))).transposed().to_4x4()
    basis.translation = at + back * ((0.055 if side == 'R' else 0.28) * 0.42) - forward * (0.23 * 0.42)
    change = basis @ wrists[side].inverted()
    arm.data.transform(change)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for bone in rig.data.edit_bones:
        if bone.name.endswith(side): bone.transform(change)
    bpy.ops.object.mode_set(mode='OBJECT')
    arms.append(arm)
grip(rig, arms, ('R',), tight=True)
export(os.path.join(OUT, 'SM_WinterArms_Knife.fbx'), [rig, *arms])
outputs += [rig, *arms]
for o in outputs: o.hide_render = False
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, 'Art/Blender/SM_WinterArms.blend'))
os.makedirs(EVIDENCE, exist_ok=True)
with open(os.path.join(EVIDENCE, 'extraction.json'), 'w') as f: json.dump(report, f, indent=2)
print('[WA1]', json.dumps(report))
