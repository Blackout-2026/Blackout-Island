"""Renders a viewmodel the way the game camera sees it (step C7). Owner: Core Developer.

    BLEND=Art/Blender/SM_Weapon_Smg.blend HELD=0.13,-0.125,0.45 EULER=0,0,0 OUT=smg_game.png \
      "D:\Blender\blender.exe" -b -P Art/Blender/Scripts/preview_game.py

The camera is at the origin, looks along +Z with a 72 degree vertical field of view (Player.prefab), 16:9. The weapon is
placed like the Player prefab does it: its own position HELD and rotation EULER (Unity degrees, in the WeaponHolder), and the
holder's uniform scale 0.85 on top. Edit PlayerViewmodelSwap.cs to the same numbers to get this picture in the game.
Plain, dim, bluish studio light so the skin and the sleeve are judged at night-game brightness, not in a bright render.
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

BLEND = os.environ["BLEND"]
HELD = Vector(float(v) for v in os.environ.get("HELD", "0.13,-0.125,0.45").split(","))
EULER = tuple(float(v) for v in os.environ.get("EULER", "0,0,0").split(","))
OUT = os.environ["OUT"]
HOLDER_SCALE = 0.85
SAMPLES = int(os.environ.get("SAMPLES", "32"))

bpy.ops.wm.open_mainfile(filepath=BLEND)
P = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))  # Unity <-> Blender frame swap (its own inverse)


def unity_euler(x_deg, y_deg=0.0, z_deg=0.0):
    ax, ay, az = (math.radians(a) for a in (x_deg, y_deg, z_deg))
    rx = Matrix(((1, 0, 0), (0, math.cos(ax), -math.sin(ax)), (0, math.sin(ax), math.cos(ax))))
    ry = Matrix(((math.cos(ay), 0, math.sin(ay)), (0, 1, 0), (-math.sin(ay), 0, math.cos(ay))))
    rz = Matrix(((math.cos(az), -math.sin(az), 0), (math.sin(az), math.cos(az), 0), (0, 0, 1)))
    return ry @ rx @ rz


r3 = unity_euler(*EULER)
m_u = Matrix.Translation(HELD * HOLDER_SCALE) @ (Matrix.Scale(HOLDER_SCALE, 4) @ r3.to_4x4())
m_b = P @ m_u @ P
for ob in bpy.data.objects:
    if ob.type == 'MESH':
        ob.matrix_world = m_b

scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1280, 720
scene.view_settings.view_transform = 'AgX'
world = bpy.data.worlds.new("W")
scene.world = world
world.use_nodes = True
world.node_tree.nodes["Background"].inputs["Color"].default_value = (0.10, 0.13, 0.19, 1)
world.node_tree.nodes["Background"].inputs["Strength"].default_value = 1.0


def light(kind, loc, energy, size=None):
    data = bpy.data.lights.new("L", kind)
    data.energy = energy
    if size is not None and hasattr(data, "size"):
        data.size = size
    ob = bpy.data.objects.new("L", data)
    scene.collection.objects.link(ob)
    ob.location = Vector((loc[0], loc[2], loc[1]))
    ob.rotation_euler = (Vector((0.1, 0.0, 0.5)) - ob.location).to_track_quat('-Z', 'Y').to_euler()


light('SUN', (-1.0, 2.0, -0.5), 1.6)
light('AREA', (0.4, 0.3, -0.2), 12, size=0.6)

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam
cam_data.sensor_fit = 'VERTICAL'
cam_data.angle_y = math.radians(72.0)
cam.location = (0, 0, 0)
cam.rotation_euler = (math.radians(90.0), 0, 0)  # looks along Blender +Y = Unity +Z, up = Blender +Z = Unity +Y
scene.render.filepath = OUT
bpy.ops.render.render(write_still=True)
print("[Game]", OUT)
