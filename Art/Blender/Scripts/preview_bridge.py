"""Renders review pictures of SM_Dock_Footbridge.blend (step C12). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Dock_Footbridge.blend -P Art/Blender/Scripts/preview_bridge.py -- <out folder> [samples]

Assembles the dock as the Unity tool does (deck, a rail panel every 3 m on both sides, a post at each panel end, the east rail missing where
the boat is moored) and shoots it: along the deck like the reference photo, from the side and close up on a railing. A sun and a sky, not the
game's lighting.
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


def b(v):
    return Vector((v[0], v[2], v[1]))


def look_at(obj, target):
    obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()


scene = bpy.context.scene
scene.render.engine = 'CYCLES'
scene.cycles.samples = SAMPLES
scene.cycles.use_denoising = True
scene.render.resolution_x, scene.render.resolution_y = 1000, 1000
scene.view_settings.view_transform = 'AgX'
scene.view_settings.exposure = -1.2

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
sun.energy = 1.4
sun.angle = math.radians(1.2)
sun_ob = bpy.data.objects.new("Sun", sun)
scene.collection.objects.link(sun_ob)
sun_ob.rotation_euler = (math.radians(48), 0.0, math.radians(30))

# water, 1.3 m below the deck top
bpy.ops.mesh.primitive_plane_add(size=300, location=(0, 0, -1.3))
water = bpy.context.active_object
wm = bpy.data.materials.new("Water")
wm.use_nodes = True
wm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.05, 0.12, 0.2, 1)
wm.node_tree.nodes["Principled BSDF"].inputs["Roughness"].default_value = 0.2
water.data.materials.append(wm)

deck, panel, post = bpy.data.objects["Deck"], bpy.data.objects["RailPanel"], bpy.data.objects["RailPost"]
deck.location = b((0, 0, 0))
panel.hide_render = post.hide_render = True
panel.hide_viewport = post.hide_viewport = True


def clone(src, name, loc, yaw=0.0):
    ob = bpy.data.objects.new(name, src.data)
    scene.collection.objects.link(ob)
    ob.location = b(loc)
    ob.rotation_euler = (0.0, 0.0, yaw)
    return ob


for i in range(10):
    zc = 13.0 - 3.0 * i
    for side, x in (("W", -1.5), ("E", 1.5)):
        if side == "E" and i >= 7:
            continue  # the gap where the boat is moored
        clone(panel, f"Panel_{side}{i}", (x, 0.0, zc))
for i in range(11):
    z = 14.5 - 3.0 * i
    clone(post, f"PostW{i}", (-1.5, 0.0, z))
    if i < 8 or i == 10:
        clone(post, f"PostE{i}", (1.5, 0.0, z))
clone(panel, "EndRail", (0.0, 0.0, -15.5), math.radians(90))

cam_data = bpy.data.cameras.new("Cam")
cam = bpy.data.objects.new("Cam", cam_data)
scene.collection.objects.link(cam)
scene.camera = cam


def shoot(name, loc, target, fov):
    cam.location = b(loc)
    cam_data.lens = 18.0 / math.tan(math.radians(fov) / 2.0)
    look_at(cam, b(target))
    scene.render.filepath = os.path.join(OUT, f"Bridge_{name}.png")
    bpy.ops.render.render(write_still=True)
    print(f"[Preview] {scene.render.filepath}")


shoot("along", (0.0, 1.75, 15.5), (0.0, 1.0, 0.0), 62)             # like the reference photo: standing on the deck, looking along it
shoot("side", (7.5, 2.6, 8.5), (0.0, 0.5, 2.0), 52)
shoot("rail", (-0.2, 1.1, 5.0), (-1.5, 0.55, 1.5), 40)
shoot("water", (-6.0, -0.2, 6.0), (0.0, 0.4, 0.0), 60)             # the piles from the water
