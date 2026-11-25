"""Render a front three-quarter Commander review image from SM_Char_Commander.blend."""

import os

import bpy
from mathutils import Vector

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
OUT = os.path.join(ROOT, "Art", "Blender", "Commander_Review.png")

bpy.ops.object.camera_add(location=(2.35, 4.1, 1.35))
camera = bpy.context.object
direction = Vector((0, 0, 0.96)) - camera.location
camera.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()
camera.data.type = 'ORTHO'
camera.data.ortho_scale = 2.35
bpy.context.scene.camera = camera

for location, power, size in [((1.5, 3, 3.5), 350, 3.0), ((-2, 0.5, 2.4), 250, 2.7), ((0, -2, 3), 400, 2.0)]:
    bpy.ops.object.light_add(type='AREA', location=location)
    light = bpy.context.object
    light.data.energy = power
    light.data.shape = 'DISK'
    light.data.size = size
    light.rotation_euler = (Vector((0, 0, 1)) - light.location).to_track_quat('-Z', 'Y').to_euler()

world = bpy.context.scene.world
if world is None:
    world = bpy.data.worlds.new("Commander Review World")
    bpy.context.scene.world = world
world.color = (0.16, 0.16, 0.18)
scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = 32
scene.render.resolution_x = 768
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = 'PNG'
scene.render.film_transparent = False
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[Commander] Review image:", OUT)
