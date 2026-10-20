"""Renders review pictures of SM_Tree_Pine.blend or SM_Tree_Palm.blend (step C11). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Tree_Pine.blend -P Art/Blender/Scripts/preview_trees.py -- <out folder> [samples]

Cameras are in Unity-frame metres (x right, y up) and converted with the axis swap. Views: the whole tree (LOD0), a close-up of the trunk
and the lower branches, a close-up of the crown, and the three LODs side by side. A sun, a blue sky and a grass-coloured ground, so
the shadows, the bark and the needles can be judged. Not the game's lighting.
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

kind = next(ob.name[:-5] for ob in bpy.data.objects if ob.name.endswith("_LOD0"))  # Pine, PineB, Palm, PalmC ...
HEIGHT = max(v.co.y for v in bpy.data.objects[f"{kind}_LOD0"].data.vertices)  # Blender y is Unity z; recomputed below
HEIGHT = max(v.co.z for v in bpy.data.objects[f"{kind}_LOD0"].data.vertices)


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.cycles.transparent_max_bounces = 16
scene.render.resolution_x, scene.render.resolution_y = 1000, 1000
scene.render.image_settings.file_format = 'PNG'
scene.view_settings.view_transform = 'AgX'
scene.view_settings.exposure = -1.6

world = bpy.data.worlds.new("PreviewWorld")
scene.world = world
world.use_nodes = True
nodes = world.node_tree.nodes
sky = nodes.new("ShaderNodeTexSky")
sky.sun_elevation = math.radians(42.0)
sky.sun_rotation = math.radians(210.0)
world.node_tree.links.new(sky.outputs["Color"], nodes["Background"].inputs["Color"])
nodes["Background"].inputs["Strength"].default_value = 0.7

sun = bpy.data.lights.new("Sun", 'SUN')
sun.energy = 1.2
sun.angle = math.radians(1.2)
sun_ob = bpy.data.objects.new("Sun", sun)
scene.collection.objects.link(sun_ob)
sun_ob.rotation_euler = (math.radians(48), 0.0, math.radians(30))

bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -0.002))
ground = bpy.context.active_object
gm = bpy.data.materials.new("Ground")
gm.use_nodes = True
gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.09, 0.12, 0.05, 1)
gm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 1.0
ground.data.materials.append(gm)

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam

lods = [bpy.data.objects[f"{kind}_LOD{i}"] for i in range(3)]


def show_only(*indices):
    for i, ob in enumerate(lods):
        ob.hide_render = i not in indices
        ob.hide_viewport = i not in indices


def shoot(name, loc, target, fov):
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, f"{kind}_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")


show_only(0)
verts = [Vector((v.co.x, v.co.z, v.co.y)) for v in bpy.data.objects[f"{kind}_LOD0"].data.vertices]  # Unity frame
upper = [v for v in verts if v.y > HEIGHT * 0.7]
cc = sum(upper, Vector()) / max(1, len(upper))  # the centre of the crown
k = max(HEIGHT / 10.4, 0.45)
shoot("whole", (12.0 * k + cc.x, HEIGHT * 0.5, 17.0 * k), (cc.x * 0.5, HEIGHT * 0.5, 0.0), 40)
shoot("game_view", (6.0, 1.7, 14.0), (0.0, HEIGHT * 0.45, 0.0), 72)  # about what the player sees from 14 m
shoot("trunk", (3.2 * k + 0.3, min(2.0, HEIGHT * 0.3), 4.2 * k + 0.3), (0.0, min(2.2, HEIGHT * 0.32), 0.0), 50)
shoot("crown", (cc.x + 5.0 * k, HEIGHT * 0.9, cc.z + 7.0 * k), (cc.x, HEIGHT * 0.78, cc.z), 42)

# the three LODs side by side: move LOD1 and LOD2 sideways
lods[1].location = b((6.5, 0.0, 0.0))
lods[2].location = b((13.0, 0.0, 0.0))
show_only(0, 1, 2)
lods[0].location = b((0.0, 0.0, 0.0))
shoot("lods", (6.5, HEIGHT * 0.5, 30.0), (6.5, HEIGHT * 0.5, 0.0), 42)
