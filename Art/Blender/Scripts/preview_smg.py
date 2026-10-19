"""Renders review pictures of SM_Weapon_Smg.blend (step C7). Owner: Core Developer.

    "D:\Blender\blender.exe" -b Art/Blender/SM_Weapon_Smg.blend -P Art/Blender/Scripts/preview_smg.py -- <out folder> [samples]

Cameras are Unity-frame metres (x right, y up, +z the muzzle direction), converted with B(). Plain studio light, not the game's.
"""

import math
import os
import sys

import bpy
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath), "preview")
SAMPLES = int(argv[1]) if len(argv) > 1 else 32
os.makedirs(OUT, exist_ok=True)

HELD = tuple(float(v) for v in os.environ.get("HELD", "0.13,-0.125,0.45").split(","))  # the weapon holder position of the Player prefab
VIEWS = {
    "smg_side": ((1.05, 0.02, 0.02), (0.0, -0.04, 0.0), 30),     # right side, like the reference
    "smg_front34": ((0.6, 0.18, 0.75), (0.0, -0.03, 0.02), 38),
    "smg_back34": ((-0.55, 0.2, -0.75), (0.0, -0.04, 0.0), 38),
    "smg_player": ((-0.15, 0.147, -0.53), (-0.15, 0.147, 0.5), 70),   # the held position (0.13,-0.125,0.45) / 0.85, seen from the camera
    "smg_game": (tuple(-c for c in HELD), (-HELD[0], -HELD[1], -HELD[2] + 1.0), 72),   # the game camera: 72 degrees vertical, the weapon at its held position (env HELD="x,y,z" tries another)
}


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1100, 700
scene.view_settings.view_transform = 'AgX'
world = bpy.data.worlds.new("W")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.05, 0.06, 0.08, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0


def light(kind, loc, energy, size=None):
    data = bpy.data.lights.new("L", kind)
    data.energy = energy
    if size is not None and hasattr(data, "size"):
        data.size = size
    ob = bpy.data.objects.new("L", data)
    scene.collection.objects.link(ob)
    ob.location = b(loc)
    look_at(ob, b((0, 0, 0)))


light('SUN', (1.0, 2.0, 0.5), 3.0)
light('AREA', (-1.0, 0.6, 1.0), 40, size=1.0)
light('AREA', (0.8, 0.3, -1.0), 40, size=1.0)

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for name, (loc, target, fov) in VIEWS.items():
    if len(argv) > 2 and name not in argv[2:]:
        continue
    cam_data.sensor_fit = 'VERTICAL' if name == "smg_game" else 'AUTO'
    scene.render.resolution_x, scene.render.resolution_y = (1280, 720) if name == "smg_game" else (1100, 700)
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
