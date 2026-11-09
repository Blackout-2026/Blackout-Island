"""Export the user's rigged Commander, pistol and five actions for Unity.

Open Art/Blender/Commander_Hero_Source.blend in background mode, then run this
script. The studio floor, camera and lights are deliberately left out of the FBX.
The source blend is kept intact; modest decimation is applied only in memory.
"""

import os

import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
FBX = os.path.join(ROOT, "Assets", "Models", "Custom", "SM_Char_CommanderHero.fbx")
TEXTURES = os.path.join(ROOT, "Assets", "Textures", "Models")
RATIOS = {
    "Boots": 0.55,
    "Coat & mantle": 0.65,
    "Cuirass & belt": 0.60,
    "Field trousers": 0.60,
    "Head, eyes & hair": 0.80,
    "Pistol": 0.60,
    "Sleeves & gloves": 0.60,
}


def export_image(image_name, filename):
    image = bpy.data.images[image_name]
    target = os.path.join(TEXTURES, filename)
    image.filepath_raw = target
    image.file_format = "PNG"
    image.save()
    image.filepath = target
    print("[Commander Hero] texture", target)


def main():
    rig = bpy.data.objects.get("Commander Rig")
    if rig is None or rig.type != "ARMATURE":
        raise RuntimeError("Source blend is missing Commander Rig")
    if len(bpy.data.actions) < 5:
        raise RuntimeError("Expected Idle, Walk, Run, Aim and Shoot actions")

    export_image("Commander_Face.png", "T_CommanderHero_Face.png")
    export_image("Commander_Hair.png", "T_CommanderHero_Hair.png")

    count = 0
    for ob in bpy.data.objects:
        ob.select_set(ob == rig or (ob.type == "MESH" and ob.parent == rig))
        if not ob.select_get() or ob.type != "MESH":
            continue
        bpy.context.view_layer.objects.active = ob
        decimate = ob.modifiers.new("Unity game mesh reduction", "DECIMATE")
        decimate.ratio = RATIOS[ob.name]
        bpy.ops.object.modifier_move_up(modifier=decimate.name)
        bpy.ops.object.modifier_apply(modifier=decimate.name)
        count += sum(len(face.vertices) - 2 for face in ob.data.polygons)

    for mat in bpy.data.materials:
        if mat.node_tree is None:
            continue
        bsdf = next((n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED"), None)
        if bsdf is not None:
            mat.diffuse_color = bsdf.inputs["Base Color"].default_value

    bpy.context.view_layer.objects.active = rig
    os.makedirs(os.path.dirname(FBX), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=FBX, use_selection=True, object_types={"ARMATURE", "MESH"},
        axis_forward="-Z", axis_up="Y", global_scale=1.0,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_UNITS",
        add_leaf_bones=False, use_armature_deform_only=False,
        use_mesh_modifiers=True, mesh_smooth_type="FACE", use_tspace=False,
        path_mode="RELATIVE", embed_textures=False,
        bake_anim=True, bake_anim_use_all_bones=True,
        bake_anim_use_nla_strips=False, bake_anim_use_all_actions=True,
        bake_anim_step=1.0, bake_anim_simplify_factor=0.5,
    )
    print("[Commander Hero] triangles", count, "actions", [a.name for a in bpy.data.actions])
    print("[Commander Hero] FBX", FBX)


if __name__ == "__main__":
    main()
