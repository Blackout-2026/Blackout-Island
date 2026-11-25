"""RL1: skin copies of the approved arms and fit the imported soldier reloads to them.

blender -b --factory-startup --python build_reload_viewmodels.py -- D:/model <repo>
The original gun FBXs, pack files, materials and textures are read-only inputs.
Outputs: SM_Reload_Smg/Rifle.fbx and Art/Blender/SM_Reload_Viewmodels.blend.
The pack's original bone motion is sampled at 60 Hz, fitted in the gun frame and
baked as a one-second Generic clip. Runtime needs no humanoid proxy or IK solver.
"""
import os
import sys
import math
import json
import bpy
import bmesh
from mathutils import Matrix, Vector, Quaternion

args = sys.argv[sys.argv.index('--') + 1:]
DOWNLOADS, REPO = args[:2]
# Reuse the existing arm source, approved grip transforms and texture/axis conventions.
helpers = {}
script = os.path.join(REPO, 'Art/Blender/Scripts/build_viewmodels.py')
with open(script, encoding='utf-8') as handle:
    exec(compile(handle.read().split('if not os.environ.get("VM_SKIP_TEXTURES"):')[0], script, 'exec'), helpers)

SCALE = helpers['HAND_SCALE']
IDENTITY = Quaternion((1, 0, 0, 0))
FINGERS = ('Index', 'Middle', 'Ring', 'Little', 'Thumb')
SOURCE = {'Index': 'indexFinger', 'Middle': 'middleFinger', 'Ring': 'ringFinger', 'Little': 'pinky', 'Thumb': 'thumb'}


def smooth(a, b, t):
    x = max(0.0, min(1.0, (t - a) / (b - a)))
    return x * x * (3 - 2 * x)


def matrix(position, rotation):
    return Matrix.Translation(position) @ rotation.to_matrix().to_4x4()


def source_pose(rig):
    # This pack's imported FBX bone positions are in centimetres, unlike our metre meshes.
    result = {}
    for side in ('L', 'R'):
        for label, name in [('Forearm', 'B-forearm.' + side), ('Hand', 'B-hand.' + side)] + [
            (finger + str(joint), 'B-' + SOURCE[finger] + '%02d.' % joint + side)
            for finger in FINGERS for joint in (1, 2, 3)]:
            value = rig.matrix_world @ rig.pose.bones[name].matrix
            result[label + side] = (value.translation * 0.01, value.to_quaternion())
    return result


def copy_mesh(name, parts, collection):
    bm = helpers['world_bmesh'](parts)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    mesh.materials.append(parts[0].data.materials[0])
    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)
    return obj


def split_magazine(body, label, collection):
    # Coordinate-connected islands join UV seams for classification without changing the output topology/UVs.
    welded = bmesh.new()
    welded.from_mesh(body.data)
    bmesh.ops.remove_doubles(welded, verts=welded.verts[:], dist=0.00001)
    welded.verts.ensure_lookup_table()
    selected = set()
    key = lambda p: tuple(round(v, 5) for v in p)
    for indexes in helpers['parts_of'](welded):
        points = [welded.verts[i].co for i in indexes]
        lo = Vector(tuple(min(p[i] for p in points) for i in range(3)))
        hi = Vector(tuple(max(p[i] for p in points) for i in range(3)))
        centre = (lo + hi) * 0.5
        is_mag = (label == 'Smg' and lo.z < -0.18 and hi.z < -0.04 and hi.x - lo.x < 0.023 and 0.12 < centre.y < 0.18)
        is_mag |= (label == 'Rifle' and -0.045 < centre.y < 0.09 and lo.z < -0.07 and hi.z < -0.04 and max(abs(lo.x), abs(hi.x)) < 0.033)
        if is_mag:
            selected.update(key(p) for p in points)
    welded.free()
    assert selected, 'No magazine islands found for ' + label
    original = bmesh.new()
    original.from_mesh(body.data)
    pieces = []
    for magazine in (False, True):
        piece = original.copy()
        drop = [face for face in piece.faces if all(key(v.co) in selected for v in face.verts) != magazine]
        bmesh.ops.delete(piece, geom=drop, context='FACES')
        bmesh.ops.delete(piece, geom=[v for v in piece.verts if not v.link_faces], context='VERTS')
        mesh = bpy.data.meshes.new('Magazine' if magazine else 'Gun')
        piece.to_mesh(mesh)
        piece.free()
        mesh.materials.append(body.data.materials[0])
        obj = bpy.data.objects.new(mesh.name, mesh)
        collection.objects.link(obj)
        pieces.append(obj)
    original.free()
    bpy.data.objects.remove(body, do_unlink=True)
    return pieces


def make_rig(label, collection, arms):
    data = bpy.data.armatures.new('ReloadSkeleton_' + label)
    rig = bpy.data.objects.new('ReloadRig_' + label, data)
    collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig
    rig.select_set(True)
    bpy.ops.object.mode_set(mode='EDIT')
    root = data.edit_bones.new('Root')
    root.head, root.tail = (0, 0, 0), (0, 0.01, 0)
    motion = data.edit_bones.new('Motion')
    motion.head, motion.tail, motion.parent = (0, 0, 0), (0, 0.01, 0), root
    transforms = {}
    for side in ('L', 'R'):
        raw = helpers['arm_source'](side)
        x, y = helpers['arm_anchor'](raw)
        low = min(v.co.z for v in raw.verts)
        raw.free()
        spec = helpers['GRIPS']['Smg' if label == 'Smg' else 'Sniper'][side]
        centre = Vector((x - spec['rho'], y, helpers['KNUCKLE_Z']))
        move = Matrix.Translation(spec['at']) @ helpers['basis'](spec['palm'], spec['fingers']).to_4x4() @ Matrix.Scale(SCALE, 4)
        transforms[side] = (move, centre, x, y)
        point = lambda p: move @ (Vector(p) - centre)
        def bone(name, head, tail, parent):
            made = data.edit_bones.new(name + side)
            made.head, made.tail = point(head), point(tail)
            made.parent = data.edit_bones[parent]
            made.align_roll((move.to_3x3() @ Vector((1, 0, 0))).normalized())
            return made
        bone('Forearm', (x, y, low + 0.02), (x, y, 0.08), 'Motion')
        bone('Hand', (x, y, 0.08), (x, y, 0.30), 'Forearm' + side)
        sign = 1 if side == 'L' else -1
        for finger, offset, length in [('Index', 0.088, 0.15), ('Middle', 0.031, 0.18), ('Ring', -0.031, 0.16), ('Little', -0.088, 0.12)]:
            for joint in (1, 2, 3):
                z = 0.30 + (joint - 1) * length / 3
                bone(finger + str(joint), (x, y + sign * offset, z), (x, y + sign * offset, z + length / 3),
                     'Hand' + side if joint == 1 else finger + str(joint - 1) + side)
        thumb = [(x - 0.015, y + sign * 0.07, 0.12), (x - 0.065, y + sign * 0.135, 0.185),
                 (x - 0.11, y + sign * 0.185, 0.24), (x - 0.15, y + sign * 0.22, 0.285)]
        for joint in (1, 2, 3):
            bone('Thumb' + str(joint), thumb[joint - 1], thumb[joint], 'Hand' + side if joint == 1 else 'Thumb' + str(joint - 1) + side)
    bpy.ops.object.mode_set(mode='OBJECT')
    return rig, transforms


def skin_arm(obj, side, rig, transform):
    move, centre, x, y = transform
    inverse = move.inverted()
    groups = {bone.name: obj.vertex_groups.new(name=bone.name) for bone in rig.data.bones if bone.name.endswith(side)}
    sign = 1 if side == 'L' else -1
    for vertex in obj.data.vertices:
        p = inverse @ vertex.co + centre
        if p.z < 0.14:
            blend = smooth(0.02, 0.14, p.z)
            weights = [('Forearm' + side, 1 - blend), ('Hand' + side, blend)]
        elif p.x < x - 0.045 and (p.y - y) * sign > 0.09:
            t = max(0, min(2.999, (p.z - 0.12) / 0.165 * 3))
            joint = min(3, int(t) + 1)
            weights = [('Thumb' + str(joint) + side, 1)]
        elif p.z > 0.27:
            finger, offset, length = min([('Index', 0.088, 0.15), ('Middle', 0.031, 0.18), ('Ring', -0.031, 0.16), ('Little', -0.088, 0.12)],
                                         key=lambda item: abs((p.y - y) * sign - item[1]))
            t = max(0, min(2.999, (p.z - 0.30) / length * 3))
            joint = min(3, int(t) + 1)
            blend = smooth(0.27, 0.31, p.z)
            weights = [('Hand' + side, 1 - blend), (finger + str(joint) + side, blend)]
        else:
            weights = [('Hand' + side, 1)]
        for name, weight in weights:
            if weight > 0: groups[name].add([vertex.index], weight, 'REPLACE')
    modifier = obj.modifiers.new('Reload skin', 'ARMATURE')
    modifier.object = rig
    obj.parent = rig


def skin_rigid(obj, rig, bone):
    group = obj.vertex_groups.new(name=bone)
    group.add([v.index for v in obj.data.vertices], 1, 'REPLACE')
    modifier = obj.modifiers.new('Reload rigid skin', 'ARMATURE')
    modifier.object = rig
    obj.parent = rig


def bake(label, rig, source, magazine):
    action = source.animation_data.action
    first, last = action.frame_range
    def sample(t):
        f = first + (last - first) * t
        bpy.context.scene.frame_set(int(f), subframe=f % 1)
        bpy.context.view_layer.update()
        return source_pose(source)
    reference = sample(0)
    rest = {b.name: b.matrix_local.copy() for b in rig.data.bones}
    bind = {name: (m.translation.copy(), m.to_quaternion()) for name, m in rest.items()}
    source_tpose = {}
    for side in ('L', 'R'):
        for finger in FINGERS:
            for joint in (1, 2, 3):
                b = source.data.bones['B-' + SOURCE[finger] + '%02d.' % joint + side]
                source_tpose[finger + str(joint) + side] = b.parent.matrix_local.to_quaternion().inverted() @ b.matrix_local.to_quaternion() @ Vector((0, 1, 0))
    right0 = reference['HandR']
    correction = bind['HandR'][1] @ right0[1].inverted()
    mag_points = [v.co for v in magazine.data.vertices]
    mag_centre = sum(mag_points, Vector()) / len(mag_points)
    # Fit the support palm to the left side of the magazine; fingers wrap toward its front.
    grip_basis = helpers['basis']((1, 0, 0), (0, 1, 0)).to_quaternion()
    original = helpers['GRIPS']['Smg' if label == 'Smg' else 'Sniper']['L']
    original_basis = helpers['basis'](original['palm'], original['fingers']).to_quaternion()
    grip_rotation = grip_basis @ original_basis.inverted() @ bind['HandL'][1]
    touch = mag_centre - Vector((0.035, 0.065, 0))
    def raw_left(pose):
        delta = pose['HandR'][1] @ right0[1].inverted()
        relative = delta.inverted() @ (pose['HandL'][0] - pose['HandR'][0])
        return correction @ (relative - (reference['HandL'][0] - right0[0]))
    reach = raw_left(sample(0.28))
    fitted = touch - bind['HandL'][0]
    fit_rotation = reach.rotation_difference(fitted) if reach.length > 0.001 else IDENTITY
    fit_scale = min(1.5, fitted.length / max(0.001, reach.length))

    def hand_pose(t, pose):
        delta_r = pose['HandR'][1] @ right0[1].inverted()
        rotations, locations = {}, {}
        for side in ('L', 'R'):
            for name in ('Forearm', 'Hand'):
                key = name + side
                d = delta_r.inverted() @ pose[key][1] @ reference[key][1].inverted()
                delta = correction @ d @ correction.inverted()
                if side == 'L':
                    delta = fit_rotation @ delta @ fit_rotation.inverted()
                    angle = min(delta.angle, 2 * math.pi - delta.angle)
                    if angle > math.radians(20): delta = IDENTITY.slerp(delta, math.radians(20) / angle)
                rotations[key] = delta @ bind[key][1]
                if side == 'L' and name == 'Hand':
                    holding = smooth(0.08, 0.25, t) * (1 - smooth(0.73, 0.92, t))
                    rotations[key] = rotations[key].slerp(grip_rotation, holding)
        locations['HandR'] = bind['HandR'][0].copy()
        travel = fit_rotation @ raw_left(pose) * fit_scale
        if travel.length > 0.18: travel *= 0.18 / travel.length
        locations['HandL'] = bind['HandL'][0] + travel
        return rotations, locations

    attach_pose = sample(0.25)
    ar, al = hand_pose(0.25, attach_pose)
    attachment = matrix(al['HandL'], ar['HandL']).inverted() @ Matrix.Translation(mag_centre)
    bpy.context.view_layer.objects.active = rig
    for p in rig.pose.bones: p.rotation_mode = 'QUATERNION'
    for frame in range(61):
        t = frame / 60
        pose = sample(t)
        envelope = smooth(0, 0.10, t) * (1 - smooth(0.85, 1, t))
        rotations, locations = hand_pose(t, pose)
        delta = correction @ (pose['HandR'][1] @ right0[1].inverted()) @ correction.inverted()
        if delta.angle > math.radians(20): delta = IDENTITY.slerp(delta, math.radians(20) / delta.angle)
        delta = IDENTITY.slerp(delta, envelope)
        shift = correction @ (pose['HandR'][0] - right0[0]) * 0.35 * envelope
        # These are cropped source forearms: lower the reload view so their open sleeve ends stay below the frame.
        shift.z -= (0.10 if label == 'Smg' else 0.14) * envelope
        motion = matrix(bind['HandR'][0] + shift - delta @ bind['HandR'][0], delta)
        desired = {'Root': rest['Root'], 'Motion': motion}
        for side in ('L', 'R'):
            hand = 'Hand' + side
            lower = 'Forearm' + side
            locations[hand] = bind[hand][0].lerp(locations[hand], envelope)
            rotations[hand] = bind[hand][1].slerp(rotations[hand], envelope)
            # Keep the cropped forearm pointing from the sleeve toward the fitted wrist, without stretching it.
            original_direction = bind[hand][0] - bind[lower][0]
            current_direction = locations[hand] - bind[lower][0]
            hand_direction = rotations[hand] @ Vector((0, 1, 0))
            lower_direction = current_direction.normalized()
            rotations[lower] = hand_direction.rotation_difference(lower_direction) @ rotations[hand]
            offset = bind[lower][1].inverted() @ (bind[hand][0] - bind[lower][0])
            lower_pos = locations[hand] - rotations[lower] @ offset
            desired[lower] = motion @ matrix(lower_pos, rotations[lower])
            desired[hand] = motion @ matrix(locations[hand], rotations[hand])
            for finger in FINGERS:
                for joint in (1, 2, 3):
                    name = finger + str(joint) + side
                    parent = rig.data.bones[name].parent.name
                    direction = pose[parent][1].inverted() @ pose[name][1] @ Vector((0, 1, 0))
                    bend = source_tpose[name].angle(direction)
                    limit = 35 if finger == 'Thumb' else (55, 70, 45)[joint - 1]
                    bend = min(bend, math.radians(limit)) * envelope
                    # Transfer the pack's joint flexion to this mesh's palm axis, rather than its different bone rolls.
                    relative = rest[parent].inverted() @ rest[name]
                    desired[name] = desired[parent] @ relative @ Matrix.Rotation(-bend, 4, 'X')
        mag_held = matrix(locations['HandL'], rotations['HandL']) @ attachment
        weight = smooth(0.20, 0.28, t) * (1 - smooth(0.68, 0.80, t))
        desired['Magazine'] = motion @ matrix(mag_centre.lerp(mag_held.translation, weight), IDENTITY.slerp(mag_held.to_quaternion(), weight))
        for p in rig.pose.bones:
            parent = p.bone.parent
            relative = rest[parent.name].inverted() @ rest[p.name] if parent else rest[p.name]
            parent_pose = desired[parent.name] if parent else Matrix.Identity(4)
            p.matrix_basis = relative.inverted() @ parent_pose.inverted() @ desired[p.name]
            p.keyframe_insert('location', frame=frame + 1, group=p.name)
            p.keyframe_insert('rotation_quaternion', frame=frame + 1, group=p.name)
    rig.animation_data.action.name = 'Reload_' + label + '_From_SoldierPack'
    bpy.context.scene.render.fps = 60
    bpy.context.scene.frame_start, bpy.context.scene.frame_end = 1, 61
    return {'label': label, 'source': action.name, 'source_seconds': (last - first) / 25, 'seconds': 1,
            'arm_reach_fit': fit_scale, 'magazine_triangles': sum(len(p.vertices) - 2 for p in magazine.data.polygons)}


all_outputs = []
reports = []
for label, original, body, folder, clip in [
    ('Smg', 'Smg', 'Smg', 'AssaultRifle', 'AssaultRifle_Reload01'),
    ('Rifle', 'Sniper', 'Sniper', 'Rifle', 'Rifle_Reload01')]:
    imported = helpers['import_fbx'](os.path.join(REPO, 'Assets/Models/Custom/SM_Weapon_' + original + '.fbx'))
    by_name = {o.name.split('.')[0]: o for o in imported if o.type == 'MESH'}
    collection = bpy.data.collections.new('Reload_' + label)
    bpy.context.scene.collection.children.link(collection)
    arms = {s: copy_mesh('ReloadArm' + s, [by_name['Glove' + s], by_name['Sleeve' + s]], collection) for s in ('L', 'R')}
    gun = copy_mesh('ReloadGun', [by_name[body]], collection)
    for ob in imported: bpy.data.objects.remove(ob, do_unlink=True)
    gun, magazine = split_magazine(gun, label, collection)
    rig, transforms = make_rig(label, collection, arms)
    bpy.context.view_layer.objects.active = rig
    bpy.ops.object.mode_set(mode='EDIT')
    centre = sum((v.co for v in magazine.data.vertices), Vector()) / len(magazine.data.vertices)
    mag = rig.data.edit_bones.new('Magazine')
    mag.head, mag.tail, mag.parent = centre, centre + Vector((0, 0.01, 0)), rig.data.edit_bones['Motion']
    bpy.ops.object.mode_set(mode='OBJECT')
    for side, arm in arms.items(): skin_arm(arm, side, rig, transforms[side])
    skin_rigid(gun, rig, 'Motion')
    skin_rigid(magazine, rig, 'Magazine')
    imported = helpers['import_fbx'](os.path.join(REPO, 'Assets/Kevin Iglesias/Human Animations/Animations/Male/Combat', folder, 'HumanM@' + clip + '.fbx'))
    source = next(o for o in imported if o.type == 'ARMATURE')
    reports.append(bake(label, rig, source, magazine))
    for ob in imported: bpy.data.objects.remove(ob, do_unlink=True)
    objects = [rig, gun, magazine, *arms.values()]
    all_outputs.extend(objects)
    # Stable export names independent of which variant was built first.
    for ob in bpy.data.objects: ob.name = 'tmp_' + str(ob.as_pointer())
    for ob, name in zip(objects, ['ReloadRig', 'ReloadGun', 'ReloadMagazine', 'ReloadArmL', 'ReloadArmR']): ob.name = name
    bpy.ops.object.select_all(action='DESELECT')
    for ob in objects: ob.select_set(True)
    bpy.context.view_layer.objects.active = rig
    bpy.context.scene.frame_set(1)
    bpy.ops.export_scene.fbx(filepath=os.path.join(REPO, 'Assets/Models/Custom/SM_Reload_' + label + '.fbx'),
        use_selection=True, object_types={'MESH', 'ARMATURE'}, global_scale=1, apply_unit_scale=True,
        apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=False,
        mesh_smooth_type='OFF', add_leaf_bones=False, use_armature_deform_only=False,
        bake_anim=True, bake_anim_use_all_actions=False, bake_anim_use_nla_strips=False,
        bake_anim_force_startend_keying=True, bake_anim_step=1, bake_anim_simplify_factor=0,
        path_mode='AUTO', embed_textures=False)
for ob in bpy.data.objects:
    if ob not in all_outputs: ob.hide_render = True
for i in range(0, len(all_outputs), 5):
    label = 'Smg' if i == 0 else 'Rifle'
    for ob, name in zip(all_outputs[i:i + 5], ('Rig', 'Gun', 'Magazine', 'ArmL', 'ArmR')):
        ob.name = label + '_' + name
bpy.context.scene.frame_set(1)
bpy.context.preferences.filepaths.save_version = 0
bpy.ops.wm.save_as_mainfile(filepath=os.path.join(REPO, 'Art/Blender/SM_Reload_Viewmodels.blend'))
print('[RL1]', json.dumps(reports))
