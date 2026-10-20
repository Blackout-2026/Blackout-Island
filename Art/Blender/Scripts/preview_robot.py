"""Renders review pictures of SM_SecurityRobot.blend (step C8). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_SecurityRobot.blend -P Art/Blender/Scripts/preview_robot.py -- <out folder> [samples] [views]

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
    "front34": ((2.6, 1.9, 3.6), (0.0, 1.2, 0.0), 38),
    "back34": ((-2.6, 1.9, -3.6), (0.0, 1.2, 0.0), 38),
    "right": ((4.5, 1.4, 0.0), (0.0, 1.2, -0.05), 32),
    "left": ((-4.5, 1.4, 0.0), (0.0, 1.2, -0.05), 32),
    "front": ((0.0, 1.3, 4.8), (0.0, 1.2, 0.0), 32),
    "head": ((0.9, 2.15, 1.6), (0.0, 1.95, 0.0), 30),
    "game": ((0.0, 1.65, 7.0), (0.0, 1.1, 0.0), 40),
    "gun": ((1.5, 1.25, 1.3), (0.37, 0.95, 0.0), 28),
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

if os.environ.get("ROBOT_POSE") == "aim":
    # a positive angle about the joint's X axis: the same call as ProceduralRobotAnimator.SetLocalRotation. In the robot's rotated frame the
    # Blender object's local X is also Unity's X, but the z mirror reverses the sense of the angle, so the sign is negated here.
    for name, rad in (("UpperArm_R", -1.5), ("ForeArm_L", -1.1), ("Thigh_L", 0.32), ("Thigh_R", -0.32), ("Shin_L", 0.45)):
        ob = bpy.data.objects[name]
        ob.rotation_euler = (-rad, 0.0, 0.0)

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
tag = "_" + os.environ["ROBOT_POSE"] if os.environ.get("ROBOT_POSE") else ""
for name, (loc, target, fov) in VIEWS.items():
    if ONLY and name not in ONLY:
        continue
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, name + tag + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
