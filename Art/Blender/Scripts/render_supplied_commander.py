"""Render the user's Commander scene through its original portrait camera."""

import os

import bpy

root = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
scene = bpy.context.scene
scene.cycles.samples = 24
scene.render.resolution_percentage = 75
scene.render.filepath = os.path.join(root, "Art", "Blender", "Commander_Supplied_Review.png")
scene.render.image_settings.file_format = "PNG"
bpy.ops.render.render(write_still=True)
print("[Commander] review", scene.render.filepath)
