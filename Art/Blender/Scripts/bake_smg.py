"""Bakes the SMG's PBR textures and re-exports SM_Weapon_Smg.fbx (step C7). Owner: Core Developer.

Run after build_smg.py (which makes the geometry, the UVs and a first FBX with flat palette materials):
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_Weapon_Smg.blend -P Art/Blender/Scripts/bake_smg.py -- [wear samples]

Same method as bake_generator.py, smaller:
 1. WEAR PASS  Cycles bakes edge wear (R, Bevel node: where the normal turns on a bevel) and cavity dirt (G, Ambient
               Occlusion node) into one image.
 2. SIGNALS    The two gun materials (M_Weapon_Metal, M_Weapon_Polymer) become procedural node trees that output colour,
               metallic, roughness and height. Edge wear shows bare steel through the black finish, grime collects in
               the cavities, the polymer has a stippled grip texture.
 3. BAKE       Each signal goes through an Emission shader into the shared 2048 atlas; the normal map is baked from the
               height signal through a Bump node.
 4. PACK       numpy builds MetalSmooth (R = metallic, A = smoothness) as URP/Lit expects.
 5. COLLAPSE   The gun's two palette slots become ONE material, M_Weapon_Smg, that reads the baked maps, so the model has
               three slots in all (M_Weapon_Smg, M_Weapon_Glove, M_Weapon_Sleeve), the viewmodel limit.
Output: Assets/Textures/Models/T_WeaponSmg_{BaseColor,Normal,MetalSmooth}.png, then SM_Weapon_Smg.fbx.
"""

import os
import sys
import time

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402  (export_fbx; importing it builds nothing)

REPO = S.REPO
TEX_DIR = os.path.join(REPO, "Assets", "Textures", "Models")
BLEND_PATH = S.BLEND_PATH
TEX_PREFIX = "T_WeaponSmg_"
SIZE = 2048
BAKE_MARGIN = 6

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
WEAR_SAMPLES = int(argv[0]) if argv else 32
T0 = time.time()


def log(msg):
    print(f"[SmgBake {time.time() - T0:6.0f}s] {msg}", flush=True)


# ------------------------------------------------------------------------------------------------ node helpers

class Tree:
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

    def math(self, op, a, b=None, clamp=False):
        n = self.node('ShaderNodeMath', operation=op, use_clamp=clamp)
        self.feed(n.inputs[0], a)
        if b is not None:
            self.feed(n.inputs[1], b)
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

    def object_coords(self):
        return self.node('ShaderNodeTexCoord').outputs['Object']

    def image(self, image, vec=None, colorspace=None):
        n = self.node('ShaderNodeTexImage', image=image, interpolation='Linear', extension='EXTEND')
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
    def __init__(self, color, metallic, rough, height):
        self.color, self.metallic, self.rough, self.height = color, metallic, rough, height


def lin(hex_color):
    h = hex_color.lstrip("#")
    out = []
    for i in (0, 2, 4):
        c = int(h[i:i + 2], 16) / 255.0
        out.append(c / 12.92 if c <= 0.04045 else ((c + 0.055) / 1.055) ** 2.4)
    return (out[0], out[1], out[2], 1.0)


# ------------------------------------------------------------------------------------------------ procedural materials

def wear_masks(t, wear_image):
    tex = t.image(wear_image, t.uv("UVMap"), colorspace='Non-Color')
    r, g, _ = t.channels(tex.outputs['Color'])
    return r, g


def build_metal(mat, wear_image):
    """Black anodised aluminium: bare steel shows through on the edges, grime in the cavities, fine scratches."""
    t = Tree(mat)
    edge, cavity = wear_masks(t, wear_image)
    obj = t.object_coords()
    blotch = t.noise(obj, 90.0, 5.0, 0.6)  # where the finish has gone
    grime = t.noise(obj, 22.0, 4.0, 0.55)
    scratch = t.smooth(t.noise(obj, 260.0, 1.0, 0.7), 0.72, 0.745)  # a few thin light scuffs, not a speckle
    worn = t.smooth(t.add(t.mul(edge, 1.5), t.mul(t.sub(blotch, 0.5), 0.9)), 0.52, 0.8)
    base = t.lerpc(t.mul(grime, 0.6), t.rgb(lin("#16181b")), t.rgb(lin("#25272b")))
    steel = t.lerpc(blotch, t.rgb(lin("#7c8084")), t.rgb(lin("#a9adb1")))
    colour = t.lerpc(worn, base, steel)
    colour = t.lerpc(t.mul(scratch, 0.35), colour, t.rgb(lin("#6d7175")))
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.65)))  # dirt in the cavities
    metallic = t.lerp(worn, 0.88, 1.0)
    rough = t.add(t.lerp(worn, 0.42, 0.26), t.mul(t.sub(grime, 0.5), 0.2))
    rough = t.add(rough, t.mul(cavity, 0.25))
    height = t.add(0.5, t.mul(t.sub(t.noise(obj, 350.0, 3.0, 0.6), 0.5), 0.05))
    height = t.sub(height, t.mul(scratch, 0.1))
    return t, Signals(colour, metallic, rough, height)


def build_polymer(mat, wear_image):
    """Matt black polymer: slightly greyer on the edges and with a stippled grip texture."""
    t = Tree(mat)
    edge, cavity = wear_masks(t, wear_image)
    obj = t.object_coords()
    blotch = t.noise(obj, 70.0, 5.0, 0.6)
    grime = t.noise(obj, 30.0, 4.0, 0.55)
    worn = t.smooth(t.add(t.mul(edge, 1.3), t.mul(t.sub(blotch, 0.5), 0.8)), 0.45, 0.75)
    base = t.lerpc(t.mul(grime, 0.7), t.rgb(lin("#0f1011")), t.rgb(lin("#1c1d1f")))
    colour = t.lerpc(worn, base, t.rgb(lin("#4a4c4f")))
    colour = t.mulc(colour, t.sub(1.0, t.mul(cavity, 0.5)))
    metallic = 0.0
    rough = t.add(t.lerp(worn, 0.62, 0.42), t.mul(t.sub(grime, 0.5), 0.2))
    stipple = t.noise(obj, 1100.0, 1.0, 0.5)  # knurled, grippy surface
    height = t.add(0.5, t.mul(t.sub(stipple, 0.5), 0.9))
    return t, Signals(colour, metallic, rough, height)


def attach_output(t, signals, target_image):
    bsdf = t.node('ShaderNodeBsdfPrincipled')
    bump = t.node('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 1.0
    bump.inputs['Distance'].default_value = 0.0012
    t.nt.links.new(signals.height, bump.inputs['Height'])
    t.nt.links.new(signals.color, bsdf.inputs['Base Color'])
    t.feed(bsdf.inputs['Metallic'], signals.metallic)
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


def main(obj_name="Smg", prefix=TEX_PREFIX, final_name="M_Weapon_Smg", builders=None, bevel_radius=0.007, ao_distance=0.02,
         material_names=("M_Weapon_Metal", "M_Weapon_Polymer")):
    """The SMG bake by default; bake_knife.py and bake_sniper.py call it with their own object, texture prefix, material names
    (one procedural builder each, in the same order) and wear radii."""
    builders = builders or (build_metal, build_polymer)
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    enable_gpu(scene)
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.normal_space = 'TANGENT'
    scene.render.bake.normal_r, scene.render.bake.normal_g, scene.render.bake.normal_b = 'POS_X', 'POS_Y', 'POS_Z'

    obj = bpy.data.objects[obj_name]
    obj.data.uv_layers.active_index = 0
    obj.data.uv_layers[0].active_render = True
    mats = {m.name: m for m in obj.data.materials}
    body = [mats[n] for n in material_names]
    os.makedirs(TEX_DIR, exist_ok=True)

    # ---- pass 1: wear masks ------------------------------------------------------------------------
    wear = new_image("Wear", 'Non-Color', float_buffer=True)
    for mat in body:
        t = Tree(mat)
        geo = t.node('ShaderNodeNewGeometry')
        bevel = t.node('ShaderNodeBevel', samples=10)
        bevel.inputs['Radius'].default_value = bevel_radius  # 0.007 for the gun; wider than the mesh's own chamfers, so they register as edges
        dot = t.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        t.nt.links.new(bevel.outputs['Normal'], dot.inputs[0])
        t.nt.links.new(geo.outputs['True Normal'], dot.inputs[1])  # the face normal; the smooth one hides the edges
        edge = t.smooth(t.sub(1.0, dot.outputs['Value']), 0.0, 0.05)
        ao = t.node('ShaderNodeAmbientOcclusion', samples=12, inside=False, only_local=False)
        ao.inputs['Distance'].default_value = ao_distance
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
    log(f"wear raw: edge mean {w[..., 0].mean():.4f} max {w[..., 0].max():.3f}, cavity mean {w[..., 1].mean():.4f} max {w[..., 1].max():.3f}")
    w[..., 0] = blur(w[..., 0], 2)
    w[..., 1] = blur(w[..., 1], 2)
    wear.pixels.foreach_set(w.ravel())
    wear.update()  # push the blurred pixels to the image the next bake samples
    wear.pack()
    if os.environ.get("SMG_DEBUG_DIR"):  # look at the masks: R edge wear, G cavity dirt
        write_png(w[..., :3], "WearDebug", os.path.join(os.environ["SMG_DEBUG_DIR"], "wear.png"), 'sRGB', False)
    log("wear masks done")

    # ---- pass 2: signals ---------------------------------------------------------------------------
    targets = {k: new_image("Bake_" + k, cs, float_buffer=True) for k, cs in
               (("color", 'sRGB'), ("normal", 'Non-Color'), ("metal", 'Non-Color'), ("rough", 'Non-Color'))}
    built = []
    for mat, builder in zip(body, builders):
        t, signals = builder(mat, wear)
        parts = attach_output(t, signals, targets["color"])
        built.append((t, signals) + parts)

    def route(signal_name, image, samples):
        for t, signals, bsdf, emit_shader, out, target in built:
            target.image = image
            t.nt.nodes.active = target
            value = getattr(signals, signal_name)
            for link in list(emit_shader.inputs['Color'].links):
                t.nt.links.remove(link)
            if isinstance(value, bpy.types.NodeSocket) and value.type == 'VALUE':
                conv = t.node('ShaderNodeCombineColor', mode='RGB')
                for ch in ('Red', 'Green', 'Blue'):
                    t.nt.links.new(value, conv.inputs[ch])
                value = conv.outputs['Color']
            elif not isinstance(value, bpy.types.NodeSocket):
                value = (value, value, value, 1.0)
            if isinstance(value, bpy.types.NodeSocket):
                t.nt.links.new(value, emit_shader.inputs['Color'])
            else:
                emit_shader.inputs['Color'].default_value = value
            t.nt.links.new(emit_shader.outputs['Emission'], out.inputs['Surface'])
        do_bake(obj, 'EMIT', samples)
        log(f"baked {signal_name}")

    route("color", targets["color"], 4)
    route("metallic", targets["metal"], 4)
    route("rough", targets["rough"], 4)
    for t, signals, bsdf, emit_shader, out, target in built:
        target.image = targets["normal"]
        t.nt.nodes.active = target
        t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    do_bake(obj, 'NORMAL', 4)
    log("baked normal")

    # ---- pack and save -----------------------------------------------------------------------------
    color = read(targets["color"])[..., :3]
    metal_v = read(targets["metal"])[..., 0]
    rough_v = read(targets["rough"])[..., 0]
    normal = read(targets["normal"])[..., :3]
    metal_smooth = np.dstack([metal_v, np.zeros_like(metal_v), np.zeros_like(metal_v), 1.0 - rough_v])
    write_png(color, prefix + "BaseColor", os.path.join(TEX_DIR, prefix + "BaseColor.png"), 'sRGB', False)
    write_png(normal, prefix + "Normal", os.path.join(TEX_DIR, prefix + "Normal.png"), 'Non-Color', False)
    write_png(metal_smooth, prefix + "MetalSmooth", os.path.join(TEX_DIR, prefix + "MetalSmooth.png"), 'Non-Color', True)
    log("textures written")

    # ---- collapse the gun to one material that reads the baked maps --------------------------------
    final = bpy.data.materials.new(final_name)
    t = Tree(final)
    imgs = {}
    for key, suffix, cs in (("base", "BaseColor", 'sRGB'), ("normal", "Normal", 'Non-Color'), ("ms", "MetalSmooth", 'Non-Color')):
        name = prefix + suffix
        old = bpy.data.images.get(name + ".png") or bpy.data.images.get(name)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.load(os.path.join(TEX_DIR, name + ".png"))
        img.colorspace_settings.name = cs
        if key == "ms":
            img.alpha_mode = 'CHANNEL_PACKED'
        imgs[key] = img
    uv = t.uv("UVMap")
    base = t.image(imgs["base"], uv).outputs['Color']
    nrm = t.image(imgs["normal"], uv)
    ms = t.image(imgs["ms"], uv)
    nmap = t.node('ShaderNodeNormalMap', space='TANGENT', uv_map="UVMap")
    t.nt.links.new(nrm.outputs['Color'], nmap.inputs['Color'])
    bsdf = t.node('ShaderNodeBsdfPrincipled')
    out = t.node('ShaderNodeOutputMaterial')
    t.nt.links.new(base, bsdf.inputs['Base Color'])
    t.nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    r, _, _ = t.channels(ms.outputs['Color'])
    t.nt.links.new(r, bsdf.inputs['Metallic'])
    t.nt.links.new(t.sub(1.0, ms.outputs['Alpha']), bsdf.inputs['Roughness'])
    t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    me = obj.data
    for poly in me.polygons:
        poly.material_index = 0
    me.materials.clear()
    me.materials.append(final)
    for m in body:
        bpy.data.materials.remove(m)

    S.export_fbx(obj)
    log(f"exported {S.FBX_PATH}")
    bpy.ops.wm.save_as_mainfile(filepath=S.BLEND_PATH)
    log(f"saved {S.BLEND_PATH}")


if __name__ == "__main__":
    main()
