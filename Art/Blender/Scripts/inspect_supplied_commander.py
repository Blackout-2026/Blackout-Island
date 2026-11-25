"""Print the structure of an open Blender scene for Commander import review."""

import bpy

print("[Provided Commander] objects", len(bpy.data.objects))
print("[Provided Commander] meshes", len(bpy.data.meshes))
print("[Provided Commander] materials", len(bpy.data.materials))
print("[Provided Commander] images", len(bpy.data.images))
print("[Provided Commander] actions", len(bpy.data.actions))
print("[Provided Commander] camera", bpy.context.scene.camera.name if bpy.context.scene.camera else "-")
print("[Provided Commander] render", bpy.context.scene.render.engine,
      bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y)
for ob in bpy.data.objects:
    if ob.type in {"MESH", "ARMATURE", "EMPTY"}:
        print("[object]", ob.name, ob.type, "parent", ob.parent.name if ob.parent else "-",
              "loc", tuple(round(v, 3) for v in ob.location),
              "dims", tuple(round(v, 3) for v in ob.dimensions),
              "tris", sum(max(0, len(p.vertices) - 2) for p in ob.data.polygons) if ob.type == "MESH" else 0,
              "groups", len(ob.vertex_groups) if ob.type == "MESH" else 0,
              "modifiers", [modifier.type for modifier in ob.modifiers] if ob.type == "MESH" else [])
for image in bpy.data.images:
    print("[image]", image.name, image.filepath, tuple(image.size), "packed", bool(image.packed_file))
for material in bpy.data.materials:
    bsdf = next((node for node in material.node_tree.nodes if node.type == "BSDF_PRINCIPLED"), None) if material.use_nodes else None
    color = tuple(round(v, 3) for v in bsdf.inputs["Base Color"].default_value[:3]) if bsdf else ()
    roughness = round(bsdf.inputs["Roughness"].default_value, 3) if bsdf else 0
    print("[material]", material.name, "color", color, "roughness", roughness)
for armature in bpy.data.armatures:
    print("[armature]", armature.name, "bones", len(armature.bones))
    print("[bones]", ", ".join(bone.name for bone in armature.bones))
for action in bpy.data.actions:
    print("[action]", action.name, "frames", tuple(action.frame_range))
