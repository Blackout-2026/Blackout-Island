"""Exports SM_SecurityRobot.fbx from the open .blend (step C8). Owner: Core Developer.

Used by bake_robot.py, and runnable alone to re-export without re-baking:
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_SecurityRobot.blend -P Art/Blender/Scripts/export_robot.py

Settings follow docs/art-pipeline.md section 4 (-Z forward, Y up, FBX units scale) with Apply Transform OFF, because the robot
has a joint hierarchy. The root Empty is rotated +90 degrees about X in the .blend (see build_robot.py), which cancels the
exporter's own axis rotation, so Unity reads every joint at rotation (0, 0, 0) with the offsets of RobotRig.Joints. This was
measured in a scratch Unity project, including a 4-level chain (Pelvis > Thigh > Shin > Foot).
"""

import os

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_SecurityRobot.fbx")


def export_fbx(path=FBX_PATH):
    for ob in bpy.data.objects:
        ob.select_set(ob.type in ('MESH', 'EMPTY'))
    bpy.context.view_layer.objects.active = bpy.data.objects["Pelvis"]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=False, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)
    print(f"[Export] {path}")


if __name__ == "__main__":
    export_fbx()
