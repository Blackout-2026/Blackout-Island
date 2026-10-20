"""Renders review pictures of SM_Weapon_Knife.blend (step C7). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Weapon_Knife.blend -P Art/Blender/Scripts/preview_knife.py -- <out folder> [samples] [view names]

Cameras are Unity-frame metres (x right, y up, +z the blade direction), converted with B(). Plain studio light, not the game's.
"knife_player" is what the player sees: the camera at the origin, the knife at the held position of the Player prefab
(Knife/Hand at (0.17, -0.18, 0.33) with the holder's 0.85 scale, rotated (-14, -14, -23)), computed back into the knife's frame.
"""

import math
import os
import sys

import bpy
from mathutils import Matrix, Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
OUT = argv[0] if argv else os.path.join(os.path.dirname(bpy.data.filepath), "preview")
SAMPLES = int(argv[1]) if len(argv) > 1 else 32
ONLY = argv[2:]  # optional view names; all views when empty
os.makedirs(OUT, exist_ok=True)

HELD = Vector((0.17, -0.18, 0.33))
HELD_EULER = (-14.0, -14.0, -23.0)
if os.environ.get("KNIFE_HELD"):  # try another held pose: KNIFE_HELD="x,y,z" KNIFE_EULER="rx,ry,rz"
    HELD = Vector(float(v) for v in os.environ["KNIFE_HELD"].split(","))
if os.environ.get("KNIFE_EULER"):
    HELD_EULER = tuple(float(v) for v in os.environ["KNIFE_EULER"].split(","))


def unity_euler(x_deg, y_deg=0.0, z_deg=0.0):
    ax, ay, az = (math.radians(a) for a in (x_deg, y_deg, z_deg))
    rx = Matrix(((1, 0, 0), (0, math.cos(ax), -math.sin(ax)), (0, math.sin(ax), math.cos(ax))))
    ry = Matrix(((math.cos(ay), 0, math.sin(ay)), (0, 1, 0), (-math.sin(ay), 0, math.cos(ay))))
    rz = Matrix(((math.cos(az), -math.sin(az), 0), (math.sin(az), math.cos(az), 0), (0, 0, 1)))
    return ry @ rx @ rz


def b(v):
    return Vector((v[0], v[2], v[1]))


def aim(obj, pos_u, forward_u, up_u):
    """Camera at pos_u (Unity frame) looking along forward_u with up_u; the Blender camera looks down -Z."""
    f = b(forward_u).normalized()
    u = b(up_u).normalized()
    r = u.cross(-f).normalized()
    u = (-f).cross(r).normalized()
    obj.matrix_world = Matrix(((r.x, u.x, -f.x, b(pos_u).x), (r.y, u.y, -f.y, b(pos_u).y),
                               (r.z, u.z, -f.z, b(pos_u).z), (0, 0, 0, 1)))


def look(obj, pos_u, target_u):
    aim(obj, pos_u, Vector(target_u) - Vector(pos_u), (0, 1, 0))


rot = unity_euler(*HELD_EULER)
inv = rot.inverted()
VIEWS = {
    # name: (position, forward, up, horizontal fov, size)
    "knife_left": ((-0.55, 0.0, 0.07), (1.0, 0.0, 0.0), (0, 1, 0), 28, (1100, 600)),   # tip to the left, like the reference
    "knife_right": ((0.55, 0.0, 0.07), (-1.0, 0.0, 0.0), (0, 1, 0), 28, (1100, 600)),
    "knife_front34": ((0.28, 0.2, 0.42), (-0.28, -0.2, -0.3), (0, 1, 0), 34, (1000, 700)),
    "knife_back34": ((-0.25, 0.22, -0.35), (0.25, -0.2, 0.42), (0, 1, 0), 40, (1000, 700)),
    # the game camera's field of view is 72 degrees vertical (Player.prefab)
    "knife_player": (tuple(inv @ -HELD), tuple(inv @ Vector((0, 0, 1))), tuple(inv @ Vector((0, 1, 0))), 72, (1280, 720)),
}

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
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
    look(ob, loc, (0, 0, 0.05))


light('SUN', (1.0, 2.0, 0.5), 3.0)
light('AREA', (-1.0, 0.6, 1.0), 40, size=1.0)
light('AREA', (0.8, 0.3, -1.0), 40, size=1.0)

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
for name, (pos, fwd, up, fov, size) in VIEWS.items():
    if ONLY and name not in ONLY:
        continue
    scene.render.resolution_x, scene.render.resolution_y = size
    cam_data.sensor_fit = 'VERTICAL' if name == "knife_player" else 'AUTO'
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    aim(cam, pos, Vector(fwd), up)
    scene.render.filepath = os.path.join(OUT, name + ".png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")
