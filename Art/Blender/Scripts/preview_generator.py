"""Renders review pictures of SM_Generator.blend (step C6). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Generator.blend -P Art/Blender/Scripts/preview_generator.py -- <out folder> [samples]

Cameras are given in Unity-frame metres (x along the machine, face at +x, y up) and converted with B(), so the pictures
show what the player will see. Lighting is a plain studio set (sun + sky + two fills), not the game's lighting.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath), "preview")
SAMPLES = int(argv[1]) if len(argv) > 1 else 48
os.makedirs(OUT, exist_ok=True)

VIEWS = {
    "view_front34": ((-7.5, 3.6, 9.0), (0.0, 1.6, 0.0), 45),   # vent side, the reference's main angle
    "view_face": ((8.3, 2.2, 4.4), (2.6, 1.3, 0.0), 45),     # control face, like the 2nd reference
    "view_back34": ((-5.5, 3.6, -9.0), (0.0, 1.6, 0.0), 45),   # pipe side
    "view_glass": ((2.0, 1.7, 6.5), (1.5, 1.35, 0.0), 45),     # the energy core through the glass
}


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    direction = target - obj.location
    obj.rotation_euler = direction.to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'

world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.06, 0.09, 0.14, 1.0)
bg.inputs["Strength"].default_value = 1.0

# floor so the machine sits on something
bpy.ops.mesh.primitive_plane_add(size=40, location=(0, 0, -0.001))
floor = bpy.context.active_object
fm = bpy.data.materials.new("PreviewFloor")
fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.055, 0.06, 1)
fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.8
floor.data.materials.append(fm)


def light(kind, loc, energy, size=None, color=(1, 1, 1)):
    data = bpy.data.lights.new("L", kind)
    data.energy = energy
    data.color = color
    if size is not None and hasattr(data, "size"):
        data.size = size
    ob = bpy.data.objects.new("L", data)
    scene.collection.objects.link(ob)
    ob.location = b(loc)
    look_at(ob, b((0, 1.4, 0)))
    return ob


light('SUN', (-6, 9, 6), 3.0)
light('AREA', (8, 4, 6), 900, size=6, color=(0.8, 0.9, 1.0))
light('AREA', (-8, 3, -6), 500, size=6, color=(1.0, 0.85, 0.7))

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for name, (loc, target, fov) in VIEWS.items():
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
