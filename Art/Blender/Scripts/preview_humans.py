"""Renders review pictures of one of the three human characters (SM_Char_Patrol, SM_Char_Hunter, SM_Char_Commander) (step C10). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Char_<Name>.blend -P Art/Blender/Scripts/preview_humans.py -- <out folder> [samples] [views]

Cameras are in Unity-frame metres (x right, y up, the robot faces +z) and converted with B(), so the pictures show what the
game camera sees. Lighting is a plain studio set, not the game's lighting. Env ROBOT_POSE=aim rotates the joints the way
ProceduralRobotAnimator does (right arm -1.5 rad, left forearm -1.1 rad about the joint's X axis) to check the pivots.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath), "preview")
SAMPLES = int(argv[1]) if len(argv) > 1 else 24
ONLY = argv[2].split(",") if len(argv) > 2 else None
os.makedirs(OUT, exist_ok=True)

VIEWS = {
    "front34": ((1.7, 1.45, 2.7), (0.0, 0.95, 0.15), 36),
    "back34": ((-1.7, 1.45, -2.7), (0.0, 0.95, 0.0), 36),
    "right": ((3.6, 1.1, 0.1), (0.0, 0.95, 0.15), 30),
    "front": ((0.0, 1.2, 3.8), (0.0, 0.95, 0.15), 30),
    "head": ((0.8, 1.85, 1.2), (0.0, 1.62, 0.0), 28),
    "face": ((0.0, 1.73, 1.6), (0.0, 1.725, 0.0), 14),
    "profile": ((1.6, 1.73, 0.0), (0.0, 1.725, 0.0), 14),
    "face34": ((0.9, 1.78, 1.3), (0.0, 1.72, 0.0), 14),
    "upper": ((0.0, 1.45, 2.4), (0.0, 1.40, 0.0), 22),
    "aim": ((1.3, 1.5, 1.7), (-0.02, 1.38, 0.15), 24),
    "aimside": ((1.9, 1.45, 0.3), (0.0, 1.38, 0.15), 24),
}


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 960, 960
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'

world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.07, 0.09, 0.12, 1.0)
bg.inputs["Strength"].default_value = 1.0

bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, -0.001))
floor = bpy.context.active_object
fm = bpy.data.materials.new("PreviewFloor")
fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.06, 0.065, 0.07, 1)
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
    look_at(ob, b((0, 1.2, 0)))


light('SUN', (-3, 6, 4), 3.0)
light('AREA', (4, 2.5, 4), 220, size=3, color=(0.8, 0.9, 1.0))
light('AREA', (-4, 2, -3), 150, size=3, color=(1.0, 0.85, 0.7))

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
tag = ""
for name, (loc, target, fov) in VIEWS.items():
    if ONLY and name not in ONLY:
        continue
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, name + tag + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
