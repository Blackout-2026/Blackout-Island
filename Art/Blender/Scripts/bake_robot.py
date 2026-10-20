"""Bakes the security robot's PBR textures and exports SM_SecurityRobot.fbx (step C8). Owner: Core Developer.

Run after build_robot.py (which makes the 17 joint meshes, one UV atlas and flat palette materials):
    "D:\\Blender\\blender.exe" -b Art/Blender/SM_SecurityRobot.blend -P Art/Blender/Scripts/bake_robot.py -- [wear samples]

Same method as bake_smg.py and bake_generator.py, for a model of 16 separate parts:
 0. JOIN       The joint meshes are copied, moved to world space and joined into one temporary mesh, so one bake covers the whole
               robot and the ambient occlusion sees the neighbouring parts (an armpit is darker than a shoulder top).
 1. WEAR PASS  Cycles bakes edge wear (R, Bevel node) and cavity dirt (G, Ambient Occlusion node) into one image.
 2. SIGNALS    M_SecurityRobot_Body and _Joint become procedural node trees that output colour, metallic, roughness and height.
               Body reads the face-corner colour "Mask" written by build_robot.py: R = accent yellow, G = bare steel, B = hazard
               stripes. So the yellow, the steel and the grey armour are ONE material and ONE texture set; only the dark joint
               metal is a second material. That is how the model stays at 4 slots (Body, Joint, Eye, Cells).
 3. BAKE       Each signal goes through an Emission shader into the shared 2048 atlas; the normal map comes from the height
               signal through a Bump node.
 4. PACK       numpy builds MetalSmooth (R = metallic, A = smoothness) as URP/Lit expects.
 5. FINAL      Body and Joint are rebuilt to read the baked maps, Eye and Cells stay flat glowing colours (their HDR values are
               set in Unity, a texture cannot exceed 1), the helper data is removed and the FBX is exported.
Output: Assets/Textures/Models/T_SecurityRobot_{BaseColor,Normal,MetalSmooth}.png, Assets/Models/Custom/SM_SecurityRobot.fbx
"""

import os
import sys

import bmesh
import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bake_smg as BK  # noqa: E402  (node helpers and bake plumbing; importing it bakes nothing)
import build_robot as R  # noqa: E402
from export_robot import export_fbx  # noqa: E402

TEX_DIR = BK.TEX_DIR
PREFIX = "T_SecurityRobot_"
Tree, Signals, lin, log = BK.Tree, BK.Signals, BK.lin, BK.log


class RobotTree(Tree):
    def mapping(self, vec, scale):
        n = self.node('ShaderNodeMapping')
        self.nt.links.new(vec, n.inputs['Vector'])
        n.inputs['Scale'].default_value = scale
        return n.outputs['Vector']

    def xyz(self, vec):
        n = self.node('ShaderNodeSeparateXYZ')
        self.nt.links.new(vec, n.inputs['Vector'])
        return n.outputs['X'], n.outputs['Y'], n.outputs['Z']


def wear_masks(t, wear):
    return BK.wear_masks(t, wear)


def seam(t, coord, pitch, width):
    """A thin 0..1 line at every multiple of `pitch` metres (panel seams)."""
    cell = t.math('FRACT', t.add(t.mul(coord, 1.0 / pitch), 0.5))
    dist = t.mul(t.math('ABSOLUTE', t.sub(cell, 0.5)), pitch)
    return t.sub(1.0, t.smooth(dist, width * 0.5, width))


def build_body(mat, wear):
    """Armour: white paint (#dcdfe2 to #eceef0, changed from the prototype's grey-blue at the owner's request) that chips along the edges to dark bare metal, yellow hazard paint, brushed steel."""
    t = RobotTree(mat)
    edge, cavity = wear_masks(t, wear)
    obj = t.object_coords()
    x, y, z = t.xyz(obj)
    m = t.node('ShaderNodeAttribute', attribute_name='Mask', attribute_type='GEOMETRY')
    acc, steel, haz = t.channels(m.outputs['Color'])
    n_mid = t.noise(obj, 9.0, 5)
    n_fine = t.noise(obj, 70.0, 6, 0.6)
    n_dust = t.noise(obj, 22.0, 4, 0.55)
    streak = t.noise(t.mapping(obj, (16.0, 16.0, 1.6)), 1.0, 4)  # stretched along the vertical: drips and rain streaks
    brush = t.noise(t.mapping(obj, (260.0, 3.0, 3.0)), 1.0, 2)     # fine brushed grain on the steel

    groove = t.math('MAXIMUM', seam(t, x, 0.31, 0.005), seam(t, z, 0.27, 0.005))  # panel seams
    chip = t.smooth(t.add(t.add(t.mul(edge, 1.1), t.mul(t.sub(n_fine, 0.5), 0.6)), t.mul(t.sub(n_mid, 0.5), 0.25)), 0.50, 0.66)
    grime = t.add(t.add(t.mul(cavity, 0.34), t.mul(t.smooth(streak, 0.6, 0.82), 0.14)), t.mul(groove, 0.3))
    dirt = t.sub(1.0, t.math('MINIMUM', grime, 0.45))

    grey = t.lerpc(n_mid, t.rgb(lin("#dcdfe2")), t.rgb(lin("#eceef0")))
    grey = t.lerpc(t.smooth(n_dust, 0.62, 0.82), grey, t.rgb(lin("#a7a49c")))               # grime blotches
    stripes = t.smooth(t.math('FRACT', t.mul(t.add(x, z), 1.0 / 0.05)), 0.42, 0.52)       # diagonal hazard stripes, 5 cm pitch
    yellow = t.mulc(t.rgb(lin("#c89a24")), t.add(0.82, t.mul(n_mid, 0.36)))
    yellow = t.lerpc(t.mul(stripes, haz), yellow, t.rgb(lin("#18191b")))
    steel_col = t.mulc(t.rgb(lin("#9aa0a4")), t.add(0.75, t.mul(brush, 0.5)))
    paint = t.lerpc(acc, grey, yellow)
    bare = t.lerpc(t.smooth(n_fine, 0.4, 0.6), t.rgb(lin("#6f7479")), t.rgb(lin("#4d5155")))
    paint_chipped = t.lerpc(t.mul(chip, t.sub(1.0, steel)), paint, bare)
    color = t.lerpc(steel, paint_chipped, steel_col)
    color = t.mulc(color, dirt)

    metal_paint = t.lerp(acc, 0.22, 0.25)
    metallic = t.lerp(t.mul(chip, t.sub(1.0, steel)), metal_paint, 0.9)  # bare metal under chipped paint
    metallic = t.lerp(steel, metallic, 0.92)
    rough = t.add(t.lerp(steel, t.add(0.40, t.mul(t.sub(n_mid, 0.5), 0.25)), 0.34), t.mul(cavity, 0.15))
    rough = t.lerp(t.mul(chip, t.sub(1.0, steel)), rough, 0.42)
    height = t.sub(t.add(t.mul(n_fine, 0.25), t.mul(t.sub(brush, 0.5), t.mul(steel, 0.5))), t.add(t.mul(groove, 0.9), t.mul(chip, 0.5)))
    return t, Signals(color, metallic, rough, height)


def build_joint(mat, wear):
    """Dark metal: near-black steel with scuffed light edges and oil streaks."""
    t = RobotTree(mat)
    edge, cavity = wear_masks(t, wear)
    obj = t.object_coords()
    n_mid = t.noise(obj, 11.0, 5)
    n_fine = t.noise(obj, 80.0, 6, 0.6)
    oil = t.smooth(t.noise(t.mapping(obj, (18.0, 18.0, 1.4)), 1.0, 4), 0.58, 0.78)
    worn = t.smooth(t.add(t.mul(edge, 1.2), t.mul(t.sub(n_fine, 0.5), 0.7)), 0.50, 0.72)
    base = t.lerpc(n_mid, t.rgb(lin("#131416")), t.rgb(lin("#1e2023")))
    color = t.lerpc(worn, base, t.rgb(lin("#5d6165")))
    color = t.mulc(color, t.sub(1.0, t.add(t.mul(cavity, 0.5), t.mul(oil, 0.25))))
    metallic = t.lerp(worn, 0.82, 0.95)
    rough = t.add(t.lerp(worn, 0.40, 0.30), t.add(t.mul(oil, -0.12), t.mul(cavity, 0.2)))
    height = t.add(t.mul(n_fine, 0.25), t.mul(worn, -0.3))
    return t, Signals(color, metallic, rough, height)


def build_flat(rgb, rough=0.3):
    def builder(mat, wear):
        t = RobotTree(mat)
        return t, Signals(t.rgb(lin(rgb)), 0.0, rough, 0.5)
    return builder


def attach_output(t, signals, target_image):
    bsdf = t.node('ShaderNodeBsdfPrincipled')
    bump = t.node('ShaderNodeBump')
    bump.inputs['Strength'].default_value = 1.0
    bump.inputs['Distance'].default_value = 0.004
    if isinstance(signals.height, bpy.types.NodeSocket):
        t.nt.links.new(signals.height, bump.inputs['Height'])
    else:
        bump.inputs['Height'].default_value = signals.height
    t.nt.links.new(signals.color, bsdf.inputs['Base Color'])
    t.feed(bsdf.inputs['Metallic'], signals.metallic)
    t.feed(bsdf.inputs['Roughness'], signals.rough)
    t.nt.links.new(bump.outputs['Normal'], bsdf.inputs['Normal'])
    emit_shader = t.node('ShaderNodeEmission')
    out = t.node('ShaderNodeOutputMaterial')
    target = t.node('ShaderNodeTexImage', image=target_image)
    t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    return bsdf, emit_shader, out, target


def join_for_bake(objs, mats):
    """All joint meshes, in world space, as one mesh with the four material slots in R.SLOTS order."""
    merged = bmesh.new()
    for ob in objs:
        bm = bmesh.new()
        bm.from_mesh(ob.data)
        names = [m.name for m in ob.data.materials]
        for f in bm.faces:
            f.material_index = R.SLOTS.index(names[f.material_index])
        bmesh.ops.transform(bm, matrix=ob.matrix_world, verts=bm.verts)
        tmp = bpy.data.meshes.new("tmp")
        bm.to_mesh(tmp)
        bm.free()
        merged.from_mesh(tmp)
        bpy.data.meshes.remove(tmp)
    me = bpy.data.meshes.new("BakeMesh")
    merged.to_mesh(me)
    merged.free()
    for m in mats:
        me.materials.append(m)
    ob = bpy.data.objects.new("BakeMesh", me)
    bpy.context.scene.collection.objects.link(ob)
    me.uv_layers.active_index = 0
    me.uv_layers[0].active_render = True
    return ob


def main():
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    BK.enable_gpu(scene)
    scene.render.bake.use_pass_direct = False
    scene.render.bake.use_pass_indirect = False
    scene.render.bake.normal_space = 'TANGENT'
    scene.render.bake.normal_r, scene.render.bake.normal_g, scene.render.bake.normal_b = 'POS_X', 'POS_Y', 'POS_Z'

    mats = [bpy.data.materials[n] for n in R.SLOTS]
    joints = [bpy.data.objects[n] for n, _, _ in R.JOINTS]
    bpy.context.view_layer.update()
    obj = join_for_bake(joints, mats)
    builders = (build_body, build_joint, build_flat("#3a0a08"), build_flat("#06303a"))
    os.makedirs(TEX_DIR, exist_ok=True)

    # ---- pass 1: wear masks --------------------------------------------------------------------------
    wear = BK.new_image("Wear", 'Non-Color', float_buffer=True)
    for mat in mats:
        t = Tree(mat)
        geo = t.node('ShaderNodeNewGeometry')
        bevel = t.node('ShaderNodeBevel', samples=10)
        bevel.inputs['Radius'].default_value = 0.012
        dot = t.node('ShaderNodeVectorMath', operation='DOT_PRODUCT')
        t.nt.links.new(bevel.outputs['Normal'], dot.inputs[0])
        t.nt.links.new(geo.outputs['Normal'], dot.inputs[1])  # the SMOOTH normal: curved armour is not an edge, a hard chamfer is
        edge = t.smooth(t.sub(1.0, dot.outputs['Value']), 0.01, 0.12)
        ao = t.node('ShaderNodeAmbientOcclusion', samples=12, inside=False, only_local=False)
        ao.inputs['Distance'].default_value = 0.035
        combine = t.node('ShaderNodeCombineColor', mode='RGB')
        t.nt.links.new(edge, combine.inputs['Red'])
        t.nt.links.new(t.sub(1.0, ao.outputs['AO']), combine.inputs['Green'])
        emit = t.node('ShaderNodeEmission')
        t.nt.links.new(combine.outputs['Color'], emit.inputs['Color'])
        out = t.node('ShaderNodeOutputMaterial')
        t.nt.links.new(emit.outputs['Emission'], out.inputs['Surface'])
        target = t.node('ShaderNodeTexImage', image=wear)
        t.nt.nodes.active = target
    log(f"baking wear masks at {BK.SIZE}px, {BK.WEAR_SAMPLES} samples")
    BK.do_bake(obj, 'EMIT', BK.WEAR_SAMPLES)
    w = BK.read(wear)
    log(f"wear raw: edge mean {w[..., 0].mean():.4f} max {w[..., 0].max():.3f}, cavity mean {w[..., 1].mean():.4f} max {w[..., 1].max():.3f}")
    w[..., 0] = BK.blur(w[..., 0], 2)
    w[..., 1] = BK.blur(w[..., 1], 2)
    wear.pixels.foreach_set(w.ravel())
    wear.update()
    wear.pack()
    if os.environ.get("ROBOT_DEBUG_DIR"):
        BK.write_png(w[..., :3], "WearDebug", os.path.join(os.environ["ROBOT_DEBUG_DIR"], "wear.png"), 'sRGB', False)

    # ---- pass 2: signals -----------------------------------------------------------------------------
    targets = {k: BK.new_image("Bake_" + k, cs, float_buffer=True) for k, cs in
               (("color", 'sRGB'), ("normal", 'Non-Color'), ("metal", 'Non-Color'), ("rough", 'Non-Color'))}
    built = []
    for mat, builder in zip(mats, builders):
        t, signals = builder(mat, wear)
        built.append((t, signals) + attach_output(t, signals, targets["color"]))

    def route(signal_name, image):
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
            if isinstance(value, bpy.types.NodeSocket):
                t.nt.links.new(value, emit_shader.inputs['Color'])
            else:
                emit_shader.inputs['Color'].default_value = (value, value, value, 1.0)
            t.nt.links.new(emit_shader.outputs['Emission'], out.inputs['Surface'])
        BK.do_bake(obj, 'EMIT', 4)
        log(f"baked {signal_name}")

    route("color", targets["color"])
    route("metallic", targets["metal"])
    route("rough", targets["rough"])
    for t, signals, bsdf, emit_shader, out, target in built:
        target.image = targets["normal"]
        t.nt.nodes.active = target
        t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])
    BK.do_bake(obj, 'NORMAL', 4)
    log("baked normal")

    # ---- pack and save -------------------------------------------------------------------------------
    color = BK.read(targets["color"])[..., :3]
    metal_v = BK.read(targets["metal"])[..., 0]
    rough_v = BK.read(targets["rough"])[..., 0]
    normal = BK.read(targets["normal"])[..., :3]
    metal_smooth = np.dstack([metal_v, np.zeros_like(metal_v), np.zeros_like(metal_v), 1.0 - rough_v])
    BK.write_png(color, PREFIX + "BaseColor", os.path.join(TEX_DIR, PREFIX + "BaseColor.png"), 'sRGB', False)
    BK.write_png(normal, PREFIX + "Normal", os.path.join(TEX_DIR, PREFIX + "Normal.png"), 'Non-Color', False)
    BK.write_png(metal_smooth, PREFIX + "MetalSmooth", os.path.join(TEX_DIR, PREFIX + "MetalSmooth.png"), 'Non-Color', True)
    log("textures written")

    # ---- final materials -----------------------------------------------------------------------------
    imgs = {}
    for key, suffix, cs in (("base", "BaseColor", 'sRGB'), ("normal", "Normal", 'Non-Color'), ("ms", "MetalSmooth", 'Non-Color')):
        name = PREFIX + suffix
        old = bpy.data.images.get(name + ".png") or bpy.data.images.get(name)
        if old:
            bpy.data.images.remove(old)
        img = bpy.data.images.load(os.path.join(TEX_DIR, name + ".png"))
        img.colorspace_settings.name = cs
        if key == "ms":
            img.alpha_mode = 'CHANNEL_PACKED'
        imgs[key] = img
    for mat in mats[:2]:
        t = RobotTree(mat)
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
    for mat in mats[2:]:
        colour, metallic, rough, emission = R.PALETTE[mat.name]
        t = RobotTree(mat)
        bsdf = t.node('ShaderNodeBsdfPrincipled')
        out = t.node('ShaderNodeOutputMaterial')
        bsdf.inputs["Base Color"].default_value = colour
        bsdf.inputs["Roughness"].default_value = rough
        bsdf.inputs["Emission Color"].default_value = emission
        bsdf.inputs["Emission Strength"].default_value = 4.0
        t.nt.links.new(bsdf.outputs['BSDF'], out.inputs['Surface'])

    # ---- strip the helper data and export -------------------------------------------------------------
    me = obj.data
    bpy.data.objects.remove(obj)
    bpy.data.meshes.remove(me)
    for ob in joints:
        if "Mask" in ob.data.attributes:
            ob.data.attributes.remove(ob.data.attributes["Mask"])
    export_fbx()
    log(f"exported {R.FBX_PATH}")
    bpy.ops.wm.save_as_mainfile(filepath=R.BLEND_PATH)
    log(f"saved {R.BLEND_PATH}")


if __name__ == "__main__":
    main()
