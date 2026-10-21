"""Builds the SCAR-style rifles of the two downloaded characters, each on its own with its MuzzlePoint: SM_Char_PatrolRifle (the Security Patrol's soldier, step PS4) and
SM_Char_HunterRifle (the Hunter, step HV1: the same rifle with a scope). Owner: Core Developer.

    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_patrol_rifle.py            (both)
    "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_patrol_rifle.py -- Hunter     (one)

The downloaded models (docs/art-pipeline.md sections 18.4 and 18.5) carry no weapon, so the rifle of build_operator.py is exported by itself and nested under the character's
right hand in the visual prefab. It is the same geometry and the same atlas (patrol_atlas.py, M_Char_Patrol_Kit) as the operator's; only its place changed: the rifle's centre
line is the origin (not 0.12 m right of a Gun socket) and the object is called "Gun", with the Empty "MuzzlePoint" 0.74 m ahead of it (the flash hider's tip is 0.745 m ahead),
exactly like the Gun of the rigid humans, so the Agent Controller's shooter wiring finds it by name.

Output: Art/Blender/SM_Char_<Name>Rifle.blend and Assets/Models/Custom/SM_Char_<Name>Rifle.fbx (env RIFLE_DRAFT = a folder writes there instead).
"""

import os
import sys

import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_commander as C  # noqa: E402
import build_operator as O  # noqa: E402

VARIANTS = {"Patrol": False, "Hunter": True}   # name -> has a scope


def build(name):
    scope = VARIANTS[name]
    blend_file, fbx_file = f"SM_Char_{name}Rifle.blend", f"SM_Char_{name}Rifle.fbx"
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    O.RIFLE_DX = 0.0                                   # the rifle's centre line is the origin
    C.PARTS.clear()
    O.build_rifle(scope=scope)
    per_joint = C.assemble()
    root, objs, _ = C.build_scene(per_joint, f"{name}Rifle", O.MATERIALS, (0.0, 0.0, O.MUZZLE_AHEAD))
    gun = objs["Gun"]
    muzzle = bpy.data.objects["MuzzlePoint"]
    for ob in list(objs.values()):                     # keep only the root, the Gun and its MuzzlePoint
        if ob is not gun:
            bpy.data.objects.remove(ob)
    gun.parent = root
    gun.matrix_parent_inverse = Matrix.Identity(4)
    gun.location = (0.0, 0.0, 0.0)
    muzzle.parent = gun
    bpy.context.view_layer.update()
    tris = sum(len(p.vertices) - 2 for p in gun.data.polygons)
    lo = Vector((9e9, 9e9, 9e9))
    hi = Vector((-9e9, -9e9, -9e9))
    for v in gun.data.vertices:
        w = gun.matrix_world @ v.co
        p = Vector((w.x, w.z, w.y))
        for k in range(3):
            lo[k] = min(lo[k], p[k])
            hi[k] = max(hi[k], p[k])
    mw = muzzle.matrix_world.translation
    print("=" * 70)
    print(f"[{name}Rifle] {tris} triangles, {len(gun.data.materials)} materials")
    print(f"[{name}Rifle] z {lo.z:.3f}..{hi.z:.3f} (length {hi.z - lo.z:.3f} m)  y {lo.y:.3f}..{hi.y:.3f}  x {lo.x:.3f}..{hi.x:.3f}")
    print(f"[{name}Rifle] MuzzlePoint at unity ({mw.x:.3f}, {mw.z:.3f}, {mw.y:.3f})")
    draft = os.environ.get("RIFLE_DRAFT")
    blend_path = os.path.join(draft or os.path.join(O.REPO, "Art", "Blender"), blend_file)
    fbx_path = os.path.join(draft or os.path.join(O.REPO, "Assets", "Models", "Custom"), fbx_file)
    os.makedirs(os.path.dirname(blend_path), exist_ok=True)
    os.makedirs(os.path.dirname(fbx_path), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=blend_path)
    for ob in bpy.data.objects:
        ob.select_set(ob.type in ('MESH', 'EMPTY'))
    bpy.context.view_layer.objects.active = gun
    bpy.ops.export_scene.fbx(
        filepath=fbx_path, use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)
    print(f"[{name}Rifle] {blend_path}")
    print(f"[{name}Rifle] {fbx_path}")


def main():
    argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
    for name in (argv or list(VARIANTS)):
        build(name)


if __name__ == "__main__":
    main()
