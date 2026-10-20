"""Builds the footbridge pieces of the dock (step C12): SM_Dock_Deck, SM_Dock_RailPanel, SM_Dock_RailPost. Owner: Core Developer.

    python Art/Blender/Scripts/make_bridge_textures.py                                               (once: the textures)
    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_bridge.py

The reference is a wooden footbridge: a flat pale-grey deck, white-painted railings made of a wide flat top board over a beam, a lower rail,
white uprights and weathered grey diagonal braces between them, on round wooden piles. The pieces are modular, because the World Builder's dock
is a 31 m deck with rails in 3 m panels (and a gap on the east side where the boat is moored). A Unity tool (DockFootbridgeBuilder) places
them on the dock's own positions and hides the greybox renderers, leaving the colliders.

  SM_Dock_Deck       2.8 m x 31 m deck, top at y 0, centred on x 0 and z 0: a 1 m segment and ten 3 m slabs with 2 cm joints (the dock's
                     posts are 3 m apart), two raised kerbs and two wooden stringers under it.
  SM_Dock_RailPanel  3 m of railing along z, standing on y 0, centred on x 0 and z 0: top board, upper beam, lower rail, two uprights and
                     three bays of diagonal braces.
  SM_Dock_RailPost   one white upright 1.04 m tall with a flat cap, on a round wooden pile that runs 1.4 m down: the pile top is at y 0.

Frame: Unity metres (x right, y up, z along the dock). Static models: Apply Transform ON, rotation 0. Materials (the slot names Unity maps):
M_Dock_Deck (concrete), M_Dock_Paint (white paint), M_Dock_Wood (grey boards and piles).
"""

import math
import os
import random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from build_trees import MeshBuilder, bl  # noqa: E402  (the same mesh collector as the trees)

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")

X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
DECK_HALF_WIDTH = 1.4
DECK_LENGTH = 31.0
RAIL_TOP = 0.92  # the top of the handrail above the deck (the greybox rails are 0.9 m)


def obox(mb, mat, c, axes, size, seed=0, tile=1.0):
    """A box with its centre at c, three orthonormal axes and a size along each. Every face has its own normal and metre-sized UVs, with u
    along the longer side of the face (so the grain of the wood runs along a board)."""
    rnd = random.Random(seed)
    off = (rnd.random(), rnd.random())
    c = Vector(c)
    for i in range(3):
        j, k = [(1, 2), (0, 2), (0, 1)][i]
        ju, jv = (j, k) if size[j] >= size[k] else (k, j)
        for sgn in (-1, 1):
            n = axes[i] * sgn
            face = c + n * (size[i] / 2.0)
            ids = []
            for su, sv in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
                p = face + axes[ju] * (su * size[ju] / 2.0) + axes[jv] * (sv * size[jv] / 2.0)
                uv = ((su * size[ju] / 2.0 + size[ju] / 2.0) / tile + off[0], (sv * size[jv] / 2.0 + size[jv] / 2.0) / tile + off[1])
                ids.append(mb.vert(p, n, uv))
            mb.quad(ids[0], ids[1], ids[2], ids[3], mat)


def box(mb, mat, c, size, seed=0):
    """An axis-aligned box: size = (x, y, z) metres."""
    obox(mb, mat, c, (X, Y, Z), size, seed)


def board(mb, mat, p0, p1, width, thick, seed=0):
    """A board from p0 to p1 in a panel that lies in the y-z plane: `width` in that plane across the board, `thick` along x."""
    p0, p1 = Vector(p0), Vector(p1)
    u = (p1 - p0).normalized()
    w = X.cross(u).normalized()  # in the y-z plane, perpendicular to u
    obox(mb, mat, (p0 + p1) * 0.5, (u, X, w), ((p1 - p0).length, thick, width), seed)


def pile(mb, mat, cx, cz, y0, y1, r, sides=10):
    """A round wooden pile; the grain runs along it (u along the pile, v round it)."""
    r0, r1 = [], []
    circumference = 2.0 * math.pi * r
    for k in range(sides + 1):
        a = 2.0 * math.pi * k / sides
        n = Vector((math.cos(a), 0.0, math.sin(a)))
        r0.append(mb.vert(Vector((cx + r * n.x, y0, cz + r * n.z)), n, (y0, circumference * k / sides)))
        r1.append(mb.vert(Vector((cx + r * n.x, y1, cz + r * n.z)), n, (y1, circumference * k / sides)))
    for k in range(sides):
        mb.quad(r0[k], r0[k + 1], r1[k + 1], r1[k], mat)


# --------------------------------------------------------------------------- the pieces

def build_deck():
    # material slots: 0 concrete, 1 wood
    mb = MeshBuilder()
    half = DECK_LENGTH / 2.0
    thick = 0.20
    edges = [half, half - 1.0]
    z = half - 1.0
    while z > -half + 0.001:
        z -= 3.0
        edges.append(z)
    for i in range(len(edges) - 1):
        z0, z1 = edges[i], edges[i + 1]
        length = z0 - z1 - 0.02
        box(mb, 0, (0.0, -thick / 2.0, (z0 + z1) * 0.5), (DECK_HALF_WIDTH * 2.0, thick, length), seed=i)
    for sx in (-1, 1):
        box(mb, 1, (sx * (DECK_HALF_WIDTH - 0.07), 0.03, 0.0), (0.14, 0.06, DECK_LENGTH), seed=70 + sx)       # the kerb
        box(mb, 1, (sx * 0.95, -thick - 0.11, 0.0), (0.22, 0.22, DECK_LENGTH), seed=80 + sx)                  # the stringer under the deck
    return mb


def build_rail_panel():
    # slots: 0 paint, 1 wood
    mb = MeshBuilder()
    length = 3.0
    h = RAIL_TOP
    box(mb, 0, (0.0, h - 0.025, 0.0), (0.17, 0.05, length), seed=1)                        # the wide flat top board
    box(mb, 0, (0.0, h - 0.10, 0.0), (0.07, 0.10, length), seed=2)                         # the beam under it
    box(mb, 0, (0.0, 0.15, 0.0), (0.06, 0.08, length), seed=3)                             # the lower rail
    low, high = 0.19, h - 0.15
    for i, zc in enumerate((-0.5, 0.5)):
        box(mb, 0, (0.0, (low + high) * 0.5, zc), (0.06, high - low, 0.06), seed=10 + i)    # uprights between the bays
    bay = length / 3.0
    for b in range(3):
        z0 = -length / 2.0 + b * bay
        z1 = z0 + bay
        a, c = ((z0, z1) if b % 2 == 0 else (z1, z0))
        board(mb, 1, Vector((0.0, low, a)), Vector((0.0, high, c)), 0.075, 0.035, seed=20 + b)  # a grey diagonal brace, alternating
    return mb


def build_rail_post():
    # slots: 0 paint, 1 wood
    mb = MeshBuilder()
    pile(mb, 1, 0.0, 0.0, -1.4, 0.0, 0.15)
    box(mb, 1, (0.0, -0.02, 0.0), (0.34, 0.04, 0.34), seed=5)                              # the pile's cap where the post stands
    box(mb, 0, (0.0, 0.5, 0.0), (0.14, 1.0, 0.14), seed=6)                                 # the white upright
    box(mb, 0, (0.0, 1.02, 0.0), (0.19, 0.04, 0.19), seed=7)                               # its flat cap
    return mb


# --------------------------------------------------------------------------- materials, scene, export

def image_material(name, base, normal, rough):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    bsdf.inputs["Roughness"].default_value = rough
    tex = nt.nodes.new("ShaderNodeTexImage")
    tex.image = bpy.data.images.load(os.path.join(TEX_DIR, base))
    tex.image.colorspace_settings.name = 'sRGB'
    nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
    ntex = nt.nodes.new("ShaderNodeTexImage")
    ntex.image = bpy.data.images.load(os.path.join(TEX_DIR, normal))
    ntex.image.colorspace_settings.name = 'Non-Color'
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(ntex.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    return mat


PIECES = {
    "Deck": (build_deck, ("M_Dock_Deck", "T_BridgeDeck"), ("M_Dock_Wood", "T_DockWood")),
    "RailPanel": (build_rail_panel, ("M_Dock_Paint", "T_DockPaint"), ("M_Dock_Wood", "T_DockWood")),
    "RailPost": (build_rail_post, ("M_Dock_Paint", "T_DockPaint"), ("M_Dock_Wood", "T_DockWood")),
}


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit_settings = bpy.context.scene.unit_settings
    unit_settings.system, unit_settings.scale_length, unit_settings.length_unit = 'METRIC', 1.0, 'METERS'
    cache = {}

    def material(name, tex):
        if name not in cache:
            cache[name] = image_material(name, tex + "_BaseColor.png", tex + "_Normal.png", 0.9)
        return cache[name]

    objs = {}
    for name, (builder, m0, m1) in PIECES.items():
        mb = builder()
        mats = [material(*m0), material(*m1)]
        me = mb.build(name, mats)
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        objs[name] = ob
        lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
        for v in me.vertices:
            u = Vector((v.co.x, v.co.z, v.co.y))
            for i in range(3):
                lo[i], hi[i] = min(lo[i], u[i]), max(hi[i], u[i])
        print(f"[Bridge] {name}: {mb.triangles()} triangles, size {hi.x - lo.x:.2f} x {hi.y - lo.y:.2f} x {hi.z - lo.z:.2f} m, "
              f"x {lo.x:.2f}..{hi.x:.2f}, y {lo.y:.2f}..{hi.y:.2f}, z {lo.z:.2f}..{hi.z:.2f}")
    blend = os.path.join(REPO, "Art", "Blender", "SM_Dock_Footbridge.blend")
    bpy.ops.wm.save_as_mainfile(filepath=blend)
    out = os.path.join(REPO, "Assets", "Models", "Custom")
    os.makedirs(out, exist_ok=True)
    for name, ob in objs.items():
        for other in bpy.data.objects:
            other.select_set(other is ob)
        bpy.context.view_layer.objects.active = ob
        fbx = os.path.join(out, f"SM_Dock_{name}.fbx")
        bpy.ops.export_scene.fbx(
            filepath=fbx, use_selection=True, object_types={'MESH'}, global_scale=1.0, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_UNITS', axis_forward='-Z', axis_up='Y', bake_space_transform=True,
            mesh_smooth_type='OFF', use_mesh_modifiers=True, use_tspace=False, use_mesh_edges=False, colors_type='NONE',
            path_mode='AUTO', embed_textures=False, bake_anim=False, add_leaf_bones=False, use_custom_props=False)
        print(f"[Bridge] {fbx}")
    print(f"[Bridge] {blend}")


if __name__ == "__main__":
    main()
