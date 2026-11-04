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

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import grip_fit  # GN1: seats the SMG's fingers on the real gun mesh

REPO, SOURCE, EVIDENCE = sys.argv[sys.argv.index('--') + 1:]
SMG_ONLY = os.environ.get('SG_EXPORT_ONLY') == 'Smg'
with open(os.path.join(REPO, 'Art/Blender/SmgGrip.json')) as f:
    smg_grip = json.load(f)
swap = lambda p: Vector((p['x'], p['z'], p['y']))
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
if not SMG_ONLY:
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


def use_ioer_gun():
    """GN1: the reload rig's SMG gun and magazine become the IOER-32G (from SM_IoerSmg.blend, built by build_ioer_smg.py), keeping the
    object names, their armature modifiers and their vertex groups (the gun follows Motion, the magazine follows Magazine)."""
    source = os.path.join(REPO, 'Art/Blender/SM_IoerSmg.blend')
    if not os.path.exists(source):
        return False
    with bpy.data.libraries.load(source) as (src, dst):
        dst.meshes = ['Smg_Gun', 'Smg_Magazine']
    meshes = dict(zip(('Smg_Gun', 'Smg_Magazine'), dst.meshes))
    material = bpy.data.materials['M_Weapon_Smg']
    for name, group in (('Smg_Gun', 'Motion'), ('Smg_Magazine', 'Magazine')):
        obj = bpy.data.objects[name]
        assert max(abs(obj.matrix_world[r][c] - (1.0 if r == c else 0.0)) for r in range(4) for c in range(4)) < 1e-6, name + ' is not in the gun frame'
        mesh = meshes[name]
        mesh.materials.clear()
        mesh.materials.append(material)
        obj.data = mesh
        vgroup = obj.vertex_groups.get(group) or obj.vertex_groups.new(name=group)   # names live on the mesh, so a swapped mesh has none
        vgroup.add(list(range(len(mesh.vertices))), 1.0, 'REPLACE')
    return True


IOER_GUN = use_ioer_gun()
report['ioer_gun'] = IOER_GUN
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
        if name.startswith('WinterSmg'):
            spec = smg_grip['left' if side == 'L' else 'right']
            # Fit the oversized source palm/fingers to the compact grips. Blend back
            # to the original sleeve through its existing weights, preserving the seam.
            forearm_weight = mapping.get('Forearm' + side, 0)
            hand_scale = spec.get('handScale', 1)
            # SM1: the sleeve keeps its full length but is slimmed across its width (x and z of the wrist frame) to
            # forearmScale, so the forearm is in proportion with the smaller hand instead of a tube much thicker than it.
            forearm_scale = spec.get('forearmScale', 1)
            across = hand_scale + (forearm_scale - hand_scale) * forearm_weight
            along = hand_scale + (1 - hand_scale) * forearm_weight
            local = Vector((local.x * across, local.y * along, local.z * across))
            elbow = axes.inverted() @ (joints['Forearm'] - joints['Hand'])
            rotation = (wrist.to_3x3() @ elbow).rotation_difference(swap(spec['elbowDirection']))
            hand_point = wrist @ local
            lower_point = wrist.translation + rotation @ (hand_point - wrist.translation)
            # Carry the cropped sleeve end behind the camera in wide ADS views.
            # Only the elbow end extends; palm, fingers and wrist seam stay fitted.
            direction = swap(spec['elbowDirection']).normalized()
            length = (joints['Forearm'] - joints['Hand']).length * 3
            distance = (lower_point - wrist.translation).dot(direction)
            end_blend = max(0, min(1, (distance / length - .65) / .35))
            end_blend = end_blend * end_blend * (3 - 2 * end_blend)
            lower_point += direction * spec.get('sleeveExtension', 0) * end_blend
            hand_point = hand_point.lerp(lower_point, forearm_weight)
            coords.append(hand_point)
        else:
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


def anatomical_bind(rig, wrists, smg=False):
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for side in ('L', 'R'):
        _, _, _, _, joints, axes = source_data[side]
        hand_scale = smg_grip['left' if side == 'L' else 'right'].get('handScale', 1) if smg else 1
        point = lambda key: wrists[side] @ (axes.inverted() @ (joints[key] - joints['Hand']) * 3
            * (1 if key == 'Forearm' else hand_scale))
        lower = rig.data.edit_bones['Forearm' + side]
        lower.head, lower.tail = point('Forearm'), point('Hand')
        if smg:
            spec = smg_grip['left' if side == 'L' else 'right']
            lower.head = lower.tail + swap(spec['elbowDirection']).normalized() * (lower.head-lower.tail).length
            rig.data.edit_bones['Hand' + side].matrix = wrists[side] @ Matrix.Rotation(math.pi/2, 4, 'Y')
        for finger in fingers:
            for j in (1, 2, 3):
                key = finger + str(j)
                b = rig.data.edit_bones[key + side]
                b.head = point(key)
                b.tail = point(finger + str(j+1)) if j < 3 else b.head + (b.head - point(finger + '2')) * 0.8
                # Local X runs across the finger: rotation around X now flexes toward the palm.
                b.align_roll(wrists[side].to_3x3() @ Vector((1, 0, 0)))
    bpy.ops.object.mode_set(mode='OBJECT')


def grip(rig, arms, sides, tight=False, smg=False):
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    desired = {}
    for b in rig.data.bones:
        rel = rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name]
        delta = Matrix.Identity(4)
        for side in sides:
            if b.name.endswith(side) and any(b.name.startswith(f) for f in fingers):
                amount = ((55, 75, 55) if tight else (30, 50, 35))[int(b.name[-2])-1]
                if b.name.startswith('Thumb'): amount = ((30, 35, 25) if tight else (12, 20, 12))[int(b.name[-2])-1]
                if smg:
                    spec = smg_grip['left' if side == 'L' else 'right']
                    finger = next(f for f in fingers if b.name.startswith(f))
                    if finger in spec.get('fingerTargets', {}):
                        # Explicit joints are posed once below; do not pre-curl their skin.
                        break
                    curls = spec.get(finger.lower() + 'Curl', spec['curl'])
                    amount = curls[('x', 'y', 'z')[int(b.name[-2])-1]]
                delta = Matrix.Rotation(math.radians(amount), 4, 'X')
                if smg and b.name.startswith('Thumb1'):
                    basis = (desired[b.parent.name] if b.parent else Matrix.Identity(4)) @ rel
                    axis = basis.to_quaternion().inverted() @ swap(spec['back']).normalized()
                    delta = Matrix.Rotation(math.radians(spec['thumbOpposition']),4,axis) @ delta
        desired[b.name] = (desired[b.parent.name] if b.parent else Matrix.Identity(4)) @ rel @ delta
        if smg:
            # Set the local pose directly. Assigning world matrices down a chain before
            # dependency evaluation uses stale parent matrices and stretches the fingers.
            rig.pose.bones[b.name].matrix_basis = delta
        else:
            rig.pose.bones[b.name].matrix = desired[b.name]
    bpy.context.view_layer.update()
    if smg:
        error = max(abs(rig.pose.bones[name].matrix[row][col] - matrix[row][col])
                    for name, matrix in desired.items() for row in range(4) for col in range(4))
        report['smg_grip_matrix_error'] = error
        assert error < 0.0001, 'SMG finger-chain pose differs from requested local rotations: ' + str(error)
    for arm in arms:
        evaluated = arm.evaluated_get(bpy.context.evaluated_depsgraph_get())
        for v, posed in zip(arm.data.vertices, evaluated.data.vertices): v.co = posed.co
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for b in rig.data.edit_bones: b.matrix = desired[b.name]
    bpy.ops.object.mode_set(mode='OBJECT')
    for p in rig.pose.bones: p.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()


def seat_fingers(rig, arm, side, targets):
    """Author joint positions on the real grip, retaining the source skin/UVs.

    Apply the same weighted rest-to-pose transform as skinning, once offline.
    Scaling only along each phalanx fits its length without inflating its width.
    """
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    changes, poses = {}, {}
    for finger, points in targets.items():
        names = [finger + str(j) + side for j in (1, 2, 3)]
        chain = [rest[names[0]].translation] + [swap(p) for p in points]
        assert len(chain) == 4
        for i, name in enumerate(names):
            bone = rig.data.bones[name]
            direction = chain[i+1] - chain[i]
            assert .01 < direction.length < .08, 'Invalid fitted phalanx length: ' + name
            assert .5 <= direction.length / bone.length <= 1.6, 'Excessive phalanx resizing: ' + name
            turn = (rest[name].to_3x3() @ Vector((0, 1, 0))).rotation_difference(direction)
            pose = (turn.to_matrix() @ rest[name].to_3x3()).to_4x4()
            pose.translation = chain[i]
            scale = Matrix.Diagonal((1, direction.length / bone.length, 1, 1))
            changes[name] = pose @ scale @ rest[name].inverted()
            poses[name] = (pose, direction.length)
    identity = Matrix.Identity(4)
    for vertex in arm.data.vertices:
        original = vertex.co.copy()
        vertex.co = sum(((changes.get(arm.vertex_groups[g.group].name, identity) @ original) * g.weight
                         for g in vertex.groups), Vector())
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    for name, (pose, length) in poses.items():
        rig.data.edit_bones[name].matrix = pose
        rig.data.edit_bones[name].length = length
    bpy.ops.object.mode_set(mode='OBJECT')
    bpy.context.view_layer.update()


def bake_ioer(rig):
    """GN1: a reload made for the IOER-32G (the MP9's baked reload sent the left hand to a magazine that is no longer there).

    One second at 60 Hz, like the original. Everything is in the gun frame; the numbers are under "reload" in SmgGrip.json.
      0.00-0.15  the gun tilts (pitch up, roll right) about the pistol grip so the magazine well opens toward the left hand
      0.00-0.14  the left arm moves from the handguard to the magazine
      0.14-0.34  the magazine slides out and down along its own axis, the hand carrying it
      0.34-0.46  the hand takes it away below the view, drifting left
      0.46-0.62  a magazine comes back up from below, the hand pushing it
      0.62-0.70  it seats in the well
      0.74-0.92  the hand returns to the handguard; the tilt eases out from 0.82
    The arms are moved rigidly (a translation of the whole left arm, the right arm with the gun), so no skin is stretched; the left fingers flex a little round the magazine.
    """
    spec = smg_grip['reload']
    pivot, axis, drift = swap(spec['pivot']), swap(spec['magazineAxisDown']).normalized(), swap(spec['drift'])
    handguard = swap(smg_grip['left']['contact'])
    mag_points = [v.co for v in bpy.data.objects['Smg_Magazine'].data.vertices]
    mag_centre = sum(mag_points, Vector()) / len(mag_points)
    out, away = spec['travelOut'], spec['travelAway']
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    for p in rig.pose.bones: p.rotation_mode = 'QUATERNION'

    def smooth(x):
        x = max(0.0, min(1.0, x))
        return x * x * (3 - 2 * x)

    def ramp(t, a, b):
        return smooth((t - a) / (b - a))

    def gun_matrix(t):
        k = ramp(t, 0, .15) * (1 - ramp(t, .82, 1.0))
        turn = Matrix.Rotation(math.radians(spec['roll']) * k, 4, 'Y') @ Matrix.Rotation(math.radians(spec['pitch']) * k, 4, 'X')
        return Matrix.Translation(pivot) @ turn @ Matrix.Translation(-pivot)

    def magazine_offset(t):
        """How far down its own axis the magazine is (metres, 0 = seated)."""
        if t < .14: return 0.0
        if t < .34: return out * ramp(t, .14, .34)
        if t < .46: return out + (away - out) * ramp(t, .34, .46)
        if t < .62: return away - (away - 0.03) * ramp(t, .46, .62)
        return 0.03 - 0.03 * ramp(t, .62, .70)

    def hand_target(t):
        holding = ramp(t, 0, .14) * (1 - ramp(t, .74, .92))
        point = handguard.lerp(mag_centre + axis * magazine_offset(t), holding)
        return point + drift * (ramp(t, .30, .42) * (1 - ramp(t, .50, .62)))

    for frame in range(1, 62):
        t = (frame - 1) / 60
        gun = gun_matrix(t)
        arm_shift = Matrix.Translation(gun @ hand_target(t) - handguard)
        # The left fingers open a little as the hand reaches the magazine, close round it, and open again on the way back.
        flex = math.radians(12) * ramp(t, .10, .16) * (1 - ramp(t, .70, .76)) - math.radians(8) * ramp(t, 0, .06) * (1 - ramp(t, .08, .12))
        desired = {}
        for b in rig.data.bones:
            name = b.name
            if name == 'Root':
                desired[name] = rest[name]
            elif name == 'Magazine':
                desired[name] = gun @ Matrix.Translation(axis * magazine_offset(t)) @ rest[name]
            elif name.endswith('L') and any(name.startswith(f) for f in fingers):
                rel = rest[b.parent.name].inverted() @ rest[name]
                desired[name] = desired[b.parent.name] @ rel @ Matrix.Rotation(flex, 4, 'X')
            elif name.endswith('L') and name != 'Root':
                desired[name] = arm_shift @ rest[name]
            else:                       # Motion, and the right arm with its fingers: they move with the gun
                desired[name] = gun @ rest[name]
        for b in rig.data.bones:
            parent_pose = desired[b.parent.name] if b.parent else Matrix.Identity(4)
            rel = rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name]
            rig.pose.bones[b.name].matrix_basis = rel.inverted() @ parent_pose.inverted() @ desired[b.name]
            rig.pose.bones[b.name].keyframe_insert('location', frame=frame)
            rig.pose.bones[b.name].keyframe_insert('rotation_quaternion', frame=frame)
    bpy.context.scene.render.fps = 60
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 61


def bake(original, rig, smg=False):
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
                if smg and b.name in ('HandR', 'ForearmR'):
                    desired[b.name] = original.pose.bones['Motion'].matrix @ rest[b.name]
                if smg and b.name == 'HandL':
                    t = (frame - 1) / 60
                    smooth = lambda t: max(0, min(1, t))**2 * (3-2*max(0, min(1,t)))
                    release = smooth(t / .25) * (1-smooth((t-.75)/.25))
                    desired[b.name].translation = desired[b.name].translation.lerp(old.matrix.translation, release)
            parent_pose = desired[b.parent.name] if b.parent else Matrix.Identity(4)
            rel = rest[b.parent.name].inverted() @ rest[b.name] if b.parent else rest[b.name]
            rig.pose.bones[b.name].matrix_basis = rel.inverted() @ parent_pose.inverted() @ desired[b.name]
            rig.pose.bones[b.name].keyframe_insert('location', frame=frame)
            rig.pose.bones[b.name].keyframe_insert('rotation_quaternion', frame=frame)
    bpy.context.scene.render.fps = 60
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 61


def fit_targets(rig, side, spec):
    """GN1: finger joint targets (Unity-frame dicts, the form seat_fingers takes) computed from the gun mesh by grip_fit."""
    fit = spec['fit']
    gun = bmesh.new()
    for name in ('Smg_Gun', 'Smg_Magazine'):
        gun.from_mesh(bpy.data.objects[name].data)
    lo = swap(fit['region']['min'])
    hi = swap(fit['region']['max'])
    keep = lambda p: all(min(lo[i], hi[i]) <= p[i] <= max(lo[i], hi[i]) for i in range(3))
    axis = {'horizontal': 2, 'cross': 1}[fit['slice']]   # the Blender axis each finger plane is perpendicular to (z up, or y forward)
    radius = fit.get('radius', 0.0072)
    targets, notes = {}, {}
    for finger in fingers:
        bones = [rig.data.bones[finger + str(j) + side] for j in (1, 2, 3)]
        root = bones[0].head_local.copy()
        lengths = [b.length for b in bones]
        if finger == 'Thumb':
            thumb = fit['thumb']
            polyline = grip_fit.line_polyline(swap(thumb['start']), swap(thumb['direction']), thumb['length'])
            joints = grip_fit.walk(root, lengths, polyline, closed=False)
        else:
            polyline, obstacle = grip_fit.outline(gun, axis, root[axis], keep, radius)
            joints = grip_fit.walk(root, lengths, polyline, obstacle=obstacle)
        targets[finger] = [{'x': v.x, 'y': v.z, 'z': v.y} for v in joints]
        notes[finger] = {'knuckle_unity': [root.x, root.z, root.y], 'phalanx_lengths': lengths,
                         'reach_to_outline': grip_fit.first_reach(root, polyline)}
    gun.free()
    report.setdefault('smg_fit', {})[side] = {'targets': targets, 'notes': notes}
    return targets


outputs = []
for label in ('Smg', 'Rifle'):
    if SMG_ONLY and label != 'Smg': continue
    original = bpy.data.objects[label + '_Rig']
    rig = copy_rig(original, label)
    # Existing align_roll points the bone's Z at the back of the hand, not its X.
    wrists = {s: original.data.bones['Hand' + s].matrix_local @ Matrix.Rotation(-math.pi/2, 4, 'Y') for s in ('L', 'R')}
    if label == 'Smg':
        for side in ('L', 'R'):
            spec = smg_grip['left' if side == 'L' else 'right']
            back, forward = swap(spec['back']).normalized(), swap(spec['fingers']).normalized()
            wrists[side] = Matrix((back, forward, back.cross(forward))).transposed().to_4x4()
            wrists[side].translation = swap(spec['wrist'])
            if 'fit' in spec:
                # GN1: place the hand by where its middle knuckle must be, so the wrist follows the hand's scale and frame.
                _, _, _, _, hand_joints, hand_axes = source_data[side]
                offset = hand_axes.inverted() @ (hand_joints['Middle1'] - hand_joints['Hand']) * 3 * spec.get('handScale', 1)
                wrists[side].translation = swap(spec['fit']['middleKnuckle']) - wrists[side].to_3x3() @ offset
    anatomical_bind(rig, wrists, label == 'Smg')
    arms = [fitted(s, rig, 'Winter' + label + s, wrists[s]) for s in ('L', 'R')]
    grip(rig, arms, ('L', 'R'), smg=label == 'Smg')
    if label == 'Smg':
        for side, arm in zip(('L', 'R'), arms):
            spec = smg_grip['left' if side == 'L' else 'right']
            targets = fit_targets(rig, side, spec) if 'fit' in spec else spec.get('fingerTargets', {})
            if targets: seat_fingers(rig, arm, side, targets)
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
    if label == 'Smg' and IOER_GUN and 'reload' in smg_grip:
        bake_ioer(rig)
    else:
        bake(original, rig, label == 'Smg')
    bpy.context.scene.frame_set(1)
    export(os.path.join(OUT, 'SM_WinterArms_' + label + '.fbx'), [rig, *arms, *parts], True)
    outputs += [rig, *arms, *parts]
    report['fitted'][label] = {s: len(o.data.vertices) for s, o in zip(('L', 'R'), arms)}

if SMG_ONLY:
    bpy.context.preferences.filepaths.save_version = 0
    bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, 'Art/Blender/SM_SmgGrip.blend'))
    os.makedirs(EVIDENCE, exist_ok=True)
    with open(os.path.join(EVIDENCE, 'extraction.json'), 'w') as f: json.dump(report, f, indent=2)
    print('[SG1]', json.dumps(report))
    sys.exit(0)

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
