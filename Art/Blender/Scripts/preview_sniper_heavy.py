"""Renders review pictures of SM_Weapon_SniperHeavy.blend (step C9). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Weapon_SniperHeavy.blend -P Art/Blender/Scripts/preview_sniper_heavy.py -- <out folder> [samples] [views]

Cameras are Unity-frame metres (x right, y up, +z the muzzle direction), converted with B(). A light studio, a white backdrop
like the reference photo, and a floor under the bipod feet. Not the game's lighting.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath), "preview")
SAMPLES = int(argv[1]) if len(argv) > 1 else 32
ONLY = argv[2].split(",") if len(argv) > 2 else None
os.makedirs(OUT, exist_ok=True)

VIEWS = {
    "photo_angle": ((1.25, 0.55, 1.75), (0.0, -0.08, 0.10), 34),    # front three-quarter, like the reference photo
    "right_side": ((2.3, 0.0, 0.08), (0.0, -0.08, 0.08), 27),
    "left_side": ((-2.3, 0.0, 0.08), (0.0, -0.08, 0.08), 27),
    "back34": ((-1.1, 0.55, -1.5), (0.0, -0.05, 0.0), 38),
    "top": ((0.0, 2.3, 0.08), (0.0, 0.0, 0.08), 26),
    "scope_close": ((0.55, 0.28, -0.55), (0.0, 0.07, -0.03), 30),
    "muzzle_close": ((0.55, 0.18, 1.25), (0.0, 0.0, 0.70), 28),
    "bipod_close": ((0.7, -0.15, 0.85), (0.0, -0.17, 0.30), 32),
}


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1280, 800
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'

world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
bg = world.node_tree.nodes["Background"]
bg.inputs["Color"].default_value = (0.92, 0.93, 0.95, 1.0)
bg.inputs["Strength"].default_value = 1.0

FLOOR_Y = min((o.matrix_world @ v.co).z for o in bpy.data.objects if o.type == 'MESH' for v in o.data.vertices) - 0.0005  # under the lowest bipod foot
bpy.ops.mesh.primitive_plane_add(size=30, location=(0, 0, FLOOR_Y))
floor = bpy.context.active_object
fm = bpy.data.materials.new("PreviewFloor")
fm.use_nodes = True
fm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.85, 0.86, 0.88, 1)
fm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.9
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
    look_at(ob, b((0, -0.05, 0.1)))


light('SUN', (-2, 4, 3), 2.5)
light('AREA', (2.5, 1.5, 2.5), 160, size=2.5, color=(0.9, 0.95, 1.0))
light('AREA', (-2.5, 1.2, -2.0), 90, size=2.5, color=(1.0, 0.92, 0.85))

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for name, (loc, target, fov) in VIEWS.items():
    if ONLY and name not in ONLY:
        continue
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
