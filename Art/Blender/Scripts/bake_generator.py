"""Bakes the generator's PBR textures and exports SM_Generator.fbx (step C6). Owner: Core Developer.

Run after build_generator.py:
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Generator.blend -P Art/Blender/Scripts/bake_generator.py

How the textures are made (no Substance, no hand painting):
 1. WEAR PASS   Cycles bakes two geometry masks into one image: R = edge wear (Bevel node: where the normal turns on a
                bevel) and G = cavity dirt (Ambient Occlusion node). This is the only expensive pass.
 2. SIGNALS     Each material (Body, Metal, Details) is a procedural node tree that outputs five signals:
                colour, metallic, roughness, height and emission. Paint chips, rust and grime are noise textures
                thresholded by the wear masks, so paint chips off along the edges first, like real paint.
 3. BAKE        Each signal is routed through an Emission shader and baked (cheap), the normal map is baked from the
                height signal through a Bump node. All three materials write into the SAME 2048 x 2048 atlas.
 4. PACK        numpy builds MetalSmooth (R = metallic, A = smoothness = 1 - roughness) as Unity's URP/Lit expects.
 5. FINAL       The materials are rebuilt to read the four baked maps, so Blender shows what Unity will show.
 6. EXPORT      Decal UVs and masks are removed, then SM_Generator.fbx is exported with the settings of
                docs/art-pipeline.md section 4.
Output: Assets/Textures/Models/T_Generator_{BaseColor,Normal,MetalSmooth,Emission}.png, Assets/Models/Custom/SM_Generator.fbx
"""

import math
import os
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
from export_generator import export_fbx  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Generator.fbx")
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Generator.blend")
GAUGE_PNG = os.path.join(REPO, "Art", "Blender", "Textures", "gauge_dial.png")
SIZE = 2048
BAKE_MARGIN = 4

SIGN_WARN = os.path.join(TEX_DIR, "T_Sign_DangerHighVoltage_BaseColor.png")
SIGN_LABEL = os.path.join(TEX_DIR, "T_Sign_GeneratorG1_BaseColor.png")

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
WEAR_SAMPLES = int(argv[0]) if argv else 48
T0 = time.time()


def log(msg):
    print(f"[Bake {time.time() - T0:6.0f}s] {msg}", flush=True)


def lin(hex_color):
    """sRGB hex -> scene-linear RGBA."""
    h = hex_color.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


# ------------------------------------------------------------------------------------------------ node helpers

class Tree:
    """Small builder so the material code below reads like a formula."""

    def __init__(self, mat):
        self.mat = mat
        mat.use_nodes = True
        self.nt = mat.node_tree
        self.nt.nodes.clear()

    def node(self, idname, **props):
        n = self.nt.nodes.new(idname)
        for key, value in props.items():
            setattr(n, key, value)
        return n

    def feed(self, socket, value):
        if isinstance(value, bpy.types.NodeSocket):
            self.nt.links.new(value, socket)
        else:
            socket.default_value = value

    def math(self, op, a, b=None, c=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        if b is not None:
            self.feed(n.inputs[1], b)
        if c is not None:
            self.feed(n.inputs[2], c)
        return n.outputs[0]

    def add(self, a, b):
        return self.math('ADD', a, b)

    def sub(self, a, b):
        return self.math('SUBTRACT', a, b)

    def mul(self, a, b):
        return self.math('MULTIPLY', a, b)

    def smooth(self, x, lo, hi):
        n = self.node('ShaderNodeMapRange', data_type='FLOAT', interpolation_type='SMOOTHSTEP', clamp=True)
        self.feed(n.inputs['Value'], x)
        self.feed(n.inputs['From Min'], lo)
        self.feed(n.inputs['From Max'], hi)
        return n.outputs['Result']

    def lerp(self, t, a, b):
        n = self.node('ShaderNodeMix', data_type='FLOAT')
        self.feed(n.inputs[0], t)
        self.feed(n.inputs[2], a)
        self.feed(n.inputs[3], b)
        return n.outputs[0]

    def lerpc(self, t, a, b):
        n = self.node('ShaderNodeMix', data_type='RGBA')
        self.feed(n.inputs[0], t)
        self.feed(n.inputs[6], a)
        self.feed(n.inputs[7], b)
        return n.outputs[2]

    def mulc(self, color, f):
        n = self.node('ShaderNodeMix', data_type='RGBA', blend_type='MULTIPLY')
        n.inputs[0].default_value = 1.0
        self.feed(n.inputs[6], color)
        self.feed(n.inputs[7], f if isinstance(f, bpy.types.NodeSocket) else (f, f, f, 1.0))
        return n.outputs[2]

    def rgb(self, color):
        n = self.node('ShaderNodeRGB')
        n.outputs[0].default_value = color
        return n.outputs[0]

    def noise(self, vec, scale, detail=5.0, rough=0.5):
        n = self.node('ShaderNodeTexNoise', noise_dimensions='3D')
        self.nt.links.new(vec, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        n.inputs['Detail'].default_value = detail
        n.inputs['Roughness'].default_value = rough
        return n.outputs['Fac']

    def mapping(self, vec, scale):
        n = self.node('ShaderNodeMapping')
        self.nt.links.new(vec, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        return n.outputs['Vector']

    def object_coords(self):
        return self.node('ShaderNodeTexCoord').outputs['Object']

    def image(self, image, vec=None, colorspace=None, interp='Linear'):
        n = self.node('ShaderNodeTexImage', image=image, interpolation=interp, extension='EXTEND')
        if colorspace:
            image.colorspace_settings.name = colorspace
        if vec is not None:
            self.nt.links.new(vec, n.inputs['Vector'])
        return n

    def uv(self, name):
        return self.node('ShaderNodeUVMap', uv_map=name).outputs['UV']

    def channels(self, color_socket):
        n = self.node('ShaderNodeSeparateColor', mode='RGB')
        self.nt.links.new(color_socket, n.inputs['Color'])
        return n.outputs['Red'], n.outputs['Green'], n.outputs['Blue']


class Signals:
    def __init__(self, color, metallic, rough, height, emit):
        self.color, self.metallic, self.rough, self.height, self.emit = color, metallic, rough, height, emit


# ------------------------------------------------------------------------------------------------ procedural materials

def wear_masks(t, wear_image):
    """Edge wear (R) and cavity dirt (G) from the baked wear pass."""
    tex = t.image(wear_image, t.uv("UVMap"), colorspace='Non-Color')
    r, g, _ = t.channels(tex.outputs['Color'])
    return r, g


def build_body(mat, wear_image):
    t = Tree(mat)
    obj = t.object_coords()
    n_big = t.noise(t.mapping(obj, (1, 1, 1)), 0.9, 5)
    n_mid = t.noise(obj, 6.0, 6)
    n_fine = t.noise(obj, 55.0, 8, 0.6)
    n_streak = t.noise(t.mapping(obj, (14.0, 14.0, 1.6)), 1.0, 4)  # stretched along Z: vertical drips
    xyz = t.node('ShaderNodeSeparateXYZ')
    t.nt.links.new(obj, xyz.inputs['Vector'])

    def seam_line(coord, pitch, width):
        # distance (in metres) to the nearest multiple of `pitch`, turned into a thin 0..1 line
        cell = t.math('FRACT', t.add(t.mul(coord, 1.0 / pitch), 0.5))
        dist = t.mul(t.math('ABSOLUTE', t.sub(cell, 0.5)), pitch)
        return t.sub(1.0, t.smooth(dist, width * 0.5, width))

    groove = t.math('MAXIMUM', seam_line(xyz.outputs['X'], 1.15, 0.012), seam_line(xyz.outputs['Z'], 0.95, 0.012))
    edge, cavity = wear_masks(t, wear_image)

    chip_drive = t.add(t.add(t.mul(n_fine, 0.55), t.mul(n_mid, 0.35)), t.add(t.mul(edge, 0.62), t.mul(cavity, 0.08)))
    chip = t.smooth(chip_drive, 0.55, 0.595)
    z = t.node('ShaderNodeSeparateXYZ')
    t.nt.links.new(obj, z.inputs['Vector'])
    low = t.sub(1.0, t.smooth(z.outputs['Z'], 0.0, 2.5))  # more rust near the floor
    rust_drive = t.add(t.add(t.mul(n_big, 0.55), t.mul(n_mid, 0.30)), t.mul(low, 0.22))
    rust_patch = t.smooth(rust_drive, 0.52, 0.60)
    rust_on_bare = t.smooth(n_mid, 0.42, 0.55)

    paint = t.mulc(t.rgb(lin("#e0650f")), t.add(0.80, t.mul(n_big, 0.40)))
    dirt = t.sub(1.0, t.add(t.add(t.mul(cavity, 0.60), t.mul(low, 0.30)), t.add(t.mul(t.smooth(n_streak, 0.52, 0.72), 0.32), t.mul(groove, 0.45))))
    paint = t.mulc(paint, dirt)
    rust_col = t.mulc(t.rgb(lin("#6b2a0c")), t.add(0.7, t.mul(n_fine, 0.6)))
    bare = t.lerpc(rust_on_bare, t.mulc(t.rgb(lin("#8a8a8c")), t.add(0.7, t.mul(n_fine, 0.5))), rust_col)
    color = t.lerpc(t.mul(rust_patch, 0.75), paint, rust_col)
    color = t.lerpc(chip, color, bare)

    metallic = t.mul(chip, t.lerp(rust_on_bare, 0.95, 0.20))
    rough = t.lerp(chip, t.add(0.40, t.mul(n_mid, 0.22)), t.lerp(rust_on_bare, 0.38, 0.82))
    rough = t.add(rough, t.mul(t.add(cavity, groove), 0.12))
    rough = t.math('MINIMUM', t.lerp(t.mul(rust_patch, 0.65), rough, 0.85), 1.0)
    height = t.sub(t.add(t.mul(n_fine, 0.30), t.mul(n_mid, 0.15)), t.add(t.mul(chip, 0.55), t.mul(groove, 0.80)))
    return t, Signals(color, metallic, rough, height, t.rgb((0, 0, 0, 1)))


def build_metal(mat, wear_image):
    t = Tree(mat)
    obj = t.object_coords()
    n_big = t.noise(obj, 1.1, 5)
    n_mid = t.noise(obj, 7.0, 6)
    n_fine = t.noise(obj, 60.0, 8, 0.6)
    n_streak = t.noise(t.mapping(obj, (16.0, 16.0, 1.4)), 1.0, 4)
    edge, cavity = wear_masks(t, wear_image)

    rust = t.smooth(t.add(t.add(t.mul(n_big, 0.50), t.mul(n_mid, 0.30)), t.mul(cavity, 0.35)), 0.46, 0.55)
    steel = t.mulc(t.rgb(lin("#4a4b50")), t.add(0.65, t.mul(n_mid, 0.7)))
    worn = t.lerpc(t.smooth(t.add(edge, t.mul(n_fine, 0.15)), 0.35, 0.8), steel, t.rgb(lin("#9a9ca0")))
    oil = t.smooth(n_streak, 0.60, 0.78)
    worn = t.mulc(worn, t.sub(1.0, t.mul(oil, 0.55)))
    rust_col = t.mulc(t.rgb(lin("#5c2509")), t.add(0.7, t.mul(n_fine, 0.6)))
    color = t.lerpc(rust, worn, rust_col)
    metallic = t.lerp(rust, 0.88, 0.12)
    rough = t.add(t.lerp(rust, t.add(0.34, t.mul(n_mid, 0.30)), 0.84), t.mul(cavity, 0.10))
    rough = t.lerp(t.mul(oil, 0.7), rough, 0.18)
    height = t.add(t.mul(n_fine, 0.35), t.mul(rust, 0.30))
    return t, Signals(color, metallic, rough, height, t.rgb((0, 0, 0, 1)))


def build_details(mat, wear_image, dial_image, warn_image, label_image):
    t = Tree(mat)
    obj = t.object_coords()
    n_mid = t.noise(obj, 8.0, 6)
    n_fine = t.noise(obj, 70.0, 8, 0.6)
    _, cavity = wear_masks(t, wear_image)

    m1 = t.node('ShaderNodeAttribute', attribute_name='Mask1', attribute_type='GEOMETRY')
    m2 = t.node('ShaderNodeAttribute', attribute_name='Mask2', attribute_type='GEOMETRY')
    warn_m, label_m, dial_big = t.channels(m1.outputs['Color'])
    dial_small = m1.outputs['Alpha']
    btn_red, btn_blue, wheel = t.channels(m2.outputs['Color'])
    copper = m2.outputs['Alpha']

    decal_uv = t.uv("DecalUV")
    warn = t.image(warn_image, decal_uv, colorspace='sRGB').outputs['Color']
    label = t.image(label_image, decal_uv, colorspace='sRGB').outputs['Color']
    dial = t.image(dial_image, decal_uv, colorspace='sRGB').outputs['Color']

    base = t.mulc(t.rgb(lin("#8d8b82")), t.add(0.8, t.mul(n_mid, 0.4)))
    color = t.lerpc(warn_m, base, warn)
    color = t.lerpc(label_m, color, label)
    color = t.lerpc(t.math('MAXIMUM', dial_big, dial_small), color, dial)
    color = t.lerpc(btn_red, color, t.rgb(lin("#e01808")))
    color = t.lerpc(btn_blue, color, t.rgb(lin("#1450e0")))
    paint_red = t.mulc(t.rgb(lin("#b01c10")), t.add(0.75, t.mul(n_fine, 0.5)))
    color = t.lerpc(wheel, color, paint_red)
    copper_col = t.mulc(t.rgb(lin("#c46a38")), t.add(0.75, t.mul(n_mid, 0.5)))
    color = t.lerpc(copper, color, copper_col)
    color = t.mulc(color, t.sub(1.0, t.mul(cavity, 0.35)))

    decal_mask = t.math('MAXIMUM', t.math('MAXIMUM', warn_m, label_m), t.math('MAXIMUM', dial_big, dial_small))
    metallic = t.add(t.mul(copper, 0.95), t.mul(wheel, 0.25))
    rough = t.lerp(copper, t.lerp(decal_mask, 0.55, 0.38), 0.32)
    rough = t.lerp(btn_red, rough, 0.25)
    rough = t.lerp(btn_blue, rough, 0.25)
    height = t.mul(n_fine, 0.25)
    # emission colour = red/blue button glow only
    red = t.node('ShaderNodeMix', data_type='RGBA', blend_type='MIX')
    red.inputs[6].default_value = (0, 0, 0, 1)
    red.inputs[7].default_value = (1.0, 0.07, 0.04, 1.0)
    t.nt.links.new(btn_red, red.inputs[0])
    blue = t.node('ShaderNodeMix', data_type='RGBA', blend_type='MIX')
    t.nt.links.new(red.outputs[2], blue.inputs[6])
    blue.inputs[7].default_value = (0.06, 0.30, 1.0, 1.0)
    t.nt.links.new(btn_blue, blue.inputs[0])
    return t, Signals(color, metallic, rough, height, blue.outputs[2])


def attach_output(t, signals, target_image):
    """Principled + Bump for the normal bake, an Emission shader for the signal bakes, and the bake target node."""
    bsdf = t.node('ShaderNodeBsdfPrincipled')
    bump = t.node('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 0.8
    bump.inputs['Distance'].default_value = 0.012
    t.nt.links.new(signals.height, bump.inputs['Height'])
    t.nt.links.new(signals.color, bsdf.inputs['Base Color'])
    t.nt.links.new(signals.metallic, bsdf.inputs['Metallic'])
    t.nt.links.new(signals.rough, bsdf.inputs['Roughness'])
    t.nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    emit_shader = t.node('ShaderNodeEmission')
    out = t.node('ShaderNodeOutputMaterial')
    target = t.node('ShaderNodeTexImage', image=target_image)
    t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return bsdf, emit_shader, out, target


# ------------------------------------------------------------------------------------------------ baking

def new_image(name, colorspace, alpha=False, float_buffer=False):
    img = bpy.data.images.get(name)
    if img:
        bpy.data.images.remove(img)
    img = bpy.data.images.new(name, SIZE, SIZE, alpha=alpha, float_buffer=float_buffer)
    img.colorspace_settings.name = colorspace
    return img


def do_bake(obj, bake_type, samples):
    scene = bpy.context.scene
    scene.cycles.samples = samples
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.bake(type=bake_type, margin=BAKE_MARGIN, margin_type='EXTEND', use_clear=True)


def read(image):
    arr = np.empty(SIZE * SIZE * 4, dtype=np.float32)
    image.pixels.foreach_get(arr)
    return arr.reshape(SIZE, SIZE, 4)


def write_png(array, name, path, colorspace, alpha):
    if array.shape[2] == 3:
        array = np.dstack([array, np.ones(array.shape[:2], dtype=np.float32)])
    img = new_image(name, colorspace, alpha=alpha)
    if alpha:
        img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(np.clip(array, 0.0, 1.0).astype(np.float32).ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def blur(a, passes=2):
    for _ in range(passes):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
    return a


def make_gauge_png(path, size=512):
    """Pressure-gauge dial drawn with numpy: off-white face, 270 degree scale, ticks, red zone and a dark rim."""
    y, x = np.mgrid[0:size, 0:size].astype(np.float32)
    c = (size - 1) / 2.0
    dx, dy = (x - c) / (size / 2.0), (c - y) / (size / 2.0)
    r = np.hypot(dx, dy)
    theta = (225.0 - np.degrees(np.arctan2(dy, dx))) % 360.0  # 0 at the lower left, 270 at the lower right, clockwise
    px = 1.5 / (size / 2.0)

    def ss(v, lo, hi):
        t = np.clip((v - lo) / (hi - lo), 0.0, 1.0)
        return t * t * (3 - 2 * t)

    img = np.zeros((size, size, 3), dtype=np.float32)
    face = np.array([0.93, 0.92, 0.86], dtype=np.float32)
    img[:] = face
    img *= (0.94 + 0.06 * (1.0 - r))[..., None]  # slight vignette
    ink = np.array([0.05, 0.05, 0.05], dtype=np.float32)
    swept = (theta <= 270.0)
    for minor in range(0, 51):
        a = minor * 270.0 / 50.0
        major = (minor % 5 == 0)
        r0, r1 = (0.70, 0.90) if major else (0.80, 0.90)
        width = (0.022 if major else 0.011)
        d = np.abs(theta - a) * np.pi / 180.0 * r
        m = (1.0 - ss(d, width * 0.5, width * 0.5 + px)) * ss(r, r0 - px, r0) * (1.0 - ss(r, r1, r1 + px)) * swept
        img = img * (1 - m[..., None]) + ink * m[..., None]
    red = np.array([0.78, 0.08, 0.05], dtype=np.float32)
    zone = (1.0 - ss(np.abs(r - 0.64), 0.025, 0.025 + px)) * (theta >= 216.0) * (theta <= 270.0)
    img = img * (1 - zone[..., None]) + red * zone[..., None]
    ring = ss(r, 0.92, 0.92 + px) * (1.0 - ss(r, 0.97, 0.97 + px))
    img = img * (1 - ring[..., None]) + ink * ring[..., None]
    img = np.where((r > 0.97)[..., None], ink * 1.4, img)
    hub = 1.0 - ss(r, 0.05, 0.05 + px)
    img = img * (1 - hub[..., None]) + np.array([0.12, 0.12, 0.12], dtype=np.float32) * hub[..., None]
    out = np.concatenate([img, np.ones((size, size, 1), dtype=np.float32)], axis=2)[::-1]  # Blender rows start at the bottom
    os.makedirs(os.path.dirname(path), exist_ok=True)
    im = bpy.data.images.new("gauge_dial", size, size, alpha=True)
    im.pixels.foreach_set(out.ravel())
    im.filepath_raw = path
    im.file_format = 'PNG'
    im.save()
    bpy.data.images.remove(im)


def enable_gpu(scene):
    scene.cycles.device = 'CPU'
    try:
        prefs = bpy.context.preferences.addons['cycles'].preferences
        for kind in ('OPTIX', 'CUDA', 'HIP', 'METAL', 'ONEAPI'):
            try:
                prefs.compute_device_type = kind
                prefs.refresh_devices() if hasattr(prefs, "refresh_devices") else prefs.get_devices()
                devices = [d for d in prefs.devices if d.type == kind]
                if devices:
                    for d in prefs.devices:
                        d.use = (d.type == kind)
                    scene.cycles.device = 'GPU'
                    log(f"Cycles device: {kind} {[d.name for d in devices]}")
                    return
            except Exception:
                continue
    except Exception as exc:
        log(f"GPU probe failed: {exc}")
    log("Cycles device: CPU")


def main():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    enable_gpu(scene)
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.normal_space = 'TANGENT'
    scene.render.bake.normal_r, scene.render.bake.normal_g, scene.render.bake.normal_b = 'POS_X', 'POS_Y', 'POS_Z'

    obj = bpy.data.objects["Generator"]
    obj.data.uv_layers.active_index = 0
    obj.data.uv_layers[0].active_render = True
    mats = {m.name: m for m in obj.data.materials}
    body, metal, details = mats["M_Generator_Body"], mats["M_Generator_Metal"], mats["M_Generator_Details"]

    os.makedirs(TEX_DIR, exist_ok=True)
    make_gauge_png(GAUGE_PNG)
    dial_img = bpy.data.images.load(GAUGE_PNG)
    warn_img = bpy.data.images.load(SIGN_WARN)
    label_img = bpy.data.images.load(SIGN_LABEL)

    # ---- pass 1: wear masks ------------------------------------------------------------------------
    wear = new_image("Wear", 'Non-Color', float_buffer=True)
    for mat in (body, metal, details):
        t = Tree(mat)
        geo = t.node('ShaderNodeNewGeometry')
        bevel = t.node('ShaderNodeBevel', samples=10)
        bevel.inputs['Radius'].default_value = 0.014
        dot = t.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        t.nt.links.new(bevel.outputs['Normal'], dot.inputs[0])
        t.nt.links.new(geo.outputs['Normal'], dot.inputs[1])
        edge = t.smooth(t.sub(1.0, dot.outputs['Value']), 0.0, 0.12)
        ao = t.node('ShaderNodeAmbientOcclusion', samples=12, inside=False, only_local=False)
        ao.inputs['Distance'].default_value = 0.30
        combine = t.node('ShaderNodeCombineColor', mode='RGB')
        t.nt.links.new(edge, combine.inputs['Red'])
        t.nt.links.new(t.sub(1.0, ao.outputs['AO']), combine.inputs['Green'])
        emit = t.node('ShaderNodeEmission')
        t.nt.links.new(combine.outputs['Color'], emit.inputs['Color'])
        out = t.node('ShaderNodeOutputMaterial')
        t.nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
        target = t.node('ShaderNodeTexImage', image=wear)
        t.nt.nodes.active = target
    log(f"baking wear masks at {SIZE}px, {WEAR_SAMPLES} samples")
    do_bake(obj, 'EMIT', WEAR_SAMPLES)
    w = read(wear)
    w[..., 0] = blur(w[..., 0], 2)  # the bake is noisy; the masks only need to be smooth
    w[..., 1] = blur(w[..., 1], 2)
    wear.pixels.foreach_set(w.ravel())
    log("wear masks done")

    # ---- pass 2: signals ---------------------------------------------------------------------------
    targets = {k: new_image("Bake_" + k, cs, float_buffer=True) for k, cs in
               (("color", 'sRGB'), ("normal", 'Non-Color'), ("metal", 'Non-Color'), ("rough", 'Non-Color'), ("emit", 'sRGB'))}
    built = []
    for mat, builder in ((body, lambda m: build_body(m, wear)), (metal, lambda m: build_metal(m, wear)),
                         (details, lambda m: build_details(m, wear, dial_img, warn_img, label_img))):
        t, signals = builder(mat)
        parts = attach_output(t, signals, targets["color"])
        built.append((t, signals) + parts)

    def route(signal_name, image, bake_type, samples):
        for t, signals, bsdf, emit_shader, out, target in built:
            target.image = image
            t.nt.nodes.active = target
            if bake_type == 'EMIT':
                value = getattr(signals, signal_name)
                for link in list(emit_shader.inputs['Color'].links):
                    t.nt.links.remove(link)
                if value.type == 'VALUE':
                    conv = t.node('ShaderNodeCombineColor', mode='RGB')
                    for ch in ('Red', 'Green', 'Blue'):
                        t.nt.links.new(value, conv.inputs[ch])
                    value = conv.outputs['Color']
                t.nt.links.new(value, emit_shader.inputs['Color'])
                t.nt.links.new(emit_shader.outputs['Emission'], out.inputs['Surface'])
            else:
                t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
        do_bake(obj, bake_type, samples)
        log(f"baked {signal_name}")

    route("color", targets["color"], 'EMIT', 4)
    route("metallic", targets["metal"], 'EMIT', 4)
    route("rough", targets["rough"], 'EMIT', 4)
    route("emit", targets["emit"], 'EMIT', 4)
    for t, signals, bsdf, emit_shader, out, target in built:
        target.image = targets["normal"]
        t.nt.nodes.active = target
        t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    do_bake(obj, 'NORMAL', 4)
    log("baked normal")

    # ---- pack and save the four textures -----------------------------------------------------------
    color = read(targets["color"])[..., :3]
    metal_v = read(targets["metal"])[..., 0]
    rough_v = read(targets["rough"])[..., 0]
    normal = read(targets["normal"])[..., :3]
    emit = read(targets["emit"])[..., :3]
    metal_smooth = np.dstack([metal_v, np.zeros_like(metal_v), np.zeros_like(metal_v), 1.0 - rough_v])
    write_png(color, "T_Generator_BaseColor", os.path.join(TEX_DIR, "T_Generator_BaseColor.png"), 'sRGB', False)
    write_png(normal, "T_Generator_Normal", os.path.join(TEX_DIR, "T_Generator_Normal.png"), 'Non-Color', False)
    write_png(metal_smooth, "T_Generator_MetalSmooth", os.path.join(TEX_DIR, "T_Generator_MetalSmooth.png"), 'Non-Color', True)
    write_png(emit, "T_Generator_Emission", os.path.join(TEX_DIR, "T_Generator_Emission.png"), 'sRGB', False)
    log("textures written")

    # ---- final materials read the baked maps -------------------------------------------------------
    finals = {}
    for key, fname, cs in (("base", "T_Generator_BaseColor", 'sRGB'), ("normal", "T_Generator_Normal", 'Non-Color'),
                           ("ms", "T_Generator_MetalSmooth", 'Non-Color'), ("emit", "T_Generator_Emission", 'sRGB')):
        old = bpy.data.images.get(fname + ".png") or bpy.data.images.get(fname)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.load(os.path.join(TEX_DIR, fname + ".png"))
        img.colorspace_settings.name = cs
        if key == "ms":
            img.alpha_mode = 'CHANNEL_PACKED'
        finals[key] = img
    for mat in (body, metal, details):
        t = Tree(mat)
        uv = t.uv("UVMap")
        base = t.image(finals["base"], uv).outputs['Color']
        nrm = t.image(finals["normal"], uv)
        ms = t.image(finals["ms"], uv)
        em = t.image(finals["emit"], uv).outputs['Color']
        nmap = t.node('ShaderNodeNormalMap', space='TANGENT', uv_map="UVMap")
        t.nt.links.new(nrm.outputs['Color'], nmap.inputs['Color'])
        bsdf = t.node('ShaderNodeBsdfPrincipled')
        out = t.node('ShaderNodeOutputMaterial')
        t.nt.links.new(base, bsdf.inputs['Base Color'])
        t.nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
        r, _, _ = t.channels(ms.outputs['Color'])
        t.nt.links.new(r, bsdf.inputs['Metallic'])
        t.nt.links.new(t.sub(1.0, ms.outputs['Alpha']), bsdf.inputs['Roughness'])
        if mat is details:
            t.nt.links.new(em, bsdf.inputs['Emission Color'])
            bsdf.inputs['Emission Strength'].default_value = 1.5
        t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    # ---- strip the helper data and export ----------------------------------------------------------
    me = obj.data
    if "DecalUV" in me.uv_layers:
        me.uv_layers.remove(me.uv_layers["DecalUV"])
    for name in ("Mask1", "Mask2", "partid"):
        if name in me.attributes:
            me.attributes.remove(me.attributes[name])
    export_fbx(FBX_PATH)
    log(f"exported {FBX_PATH}")
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    log(f"saved {BLEND_PATH}")


main()
