"""Exports SM_Generator.fbx from the open .blend (step C6). Owner: Core Developer.

Used by bake_generator.py, and runnable alone to re-export without re-baking:
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Generator.blend -P Art/Blender/Scripts/export_generator.py

Settings follow docs/art-pipeline.md section 4 (-Z forward, Y up, FBX units scale). Apply Transform is ON here: the
model is three static meshes with no hierarchy, so baking the Z-up to Y-up rotation into the vertices is safe and gives
Unity clean node transforms (rotation 0, scale 1) instead of a 90 degree rotation on every mesh.
"""

import os

import bpy

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Generator.fbx")
OBJECTS = ("Generator", "GlassTube", "EnergyCore")


def export_fbx(path=FBX_PATH):
    for ob in bpy.data.objects:
        ob.select_set(ob.name in OBJECTS)
    bpy.context.view_layer.objects.active = bpy.data.objects[OBJECTS[0]]
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=path, use_selection=True, object_types={'MESH', 'EMPTY'}, global_scale=1.0,
        apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y',
        bake_space_transform=True, mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False,
        use_mesh_edges=False, colors_type='NONE', path_mode='AUTO', embed_textures=False, bake_anim=False,
        add_leaf_bones=False, use_custom_props=False)
    print(f"[Export] {path}")


if __name__ == "__main__":
    export_fbx()
