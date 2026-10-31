"""EG1: convert the owner-supplied gun, retaining UVs and the three original material groups.

Run Blender --background --factory-startup --python this_file -- D:\\model\\gun
The supplied directory is read-only. Only this gun's FBX/maps and a provenance manifest are written.
Author credit from the supplied MTL/texture names: Dries Deryckere. No licence text was supplied.
"""
import hashlib
import json
import os
from pathlib import Path
import sys
import tempfile
import zipfile

import bpy
import numpy as np
from mathutils import Matrix

REPO = Path(__file__).resolve().parents[3]
SOURCE = Path(sys.argv[sys.argv.index('--') + 1]) if '--' in sys.argv else Path(r'D:\model\gun')
TEXTURES = REPO / 'Assets/Textures/Models'
MODEL = REPO / 'Assets/Models/Custom/SM_Weapon_EnemyGun.fbx'
# Original OBJ uses centimetres, +Y up, +Z muzzle. Pivot is the pistol grip centre.
GRIP = (1.9, -7.0, -14.0)
PARTS = (
    ('Body', 'Material__183', 'GAP_Examen_Gun_albedo_DriesDeryckere.tga.png',
     'GAP_Examen_Gun_metalness_DriesDeryckere.tg.png', 'GAP_Examen_Gun_roughness_DriesDeryckere.tg.png',
     'GAP_Examen_Gun_occlusion_DriesDeryckere.tg.png', 'Map__9_Normal_Bump.tga', 1024),
    ('Scope', 'Material__180', 'Scope_Tex_A.tga.png', 'Scope_Tex_M.tga.png', 'Scope_Tex_R.tga.png',
     'Scope_Tex_O.tga.png', 'Map__8_Normal_Bump.tga', 1024),
    ('Grip', 'Material__184', 'Grip_Tex_A.tga.png', 'Grip_Tex_M.tga.png', 'Grip_Tex_R.tga.png',
     'Grip_Tex_O.tga.png', 'Map__10_Normal_Bump.tga', 512),
)


def image(path, size):
    img = bpy.data.images.load(str(path), check_existing=False)
    # Work on raw channel values, including the alpha used for smoothness.
    img.colorspace_settings.name = 'Non-Color'
    if tuple(img.size) != (size, size):
        img.scale(size, size)
    values = np.empty(size * size * 4, dtype=np.float32)
    img.pixels.foreach_get(values)
    bpy.data.images.remove(img)
    return values.reshape(size, size, 4)


def write_png(path, values):
    height, width, _ = values.shape
    img = bpy.data.images.new(path.stem, width=width, height=height, alpha=True)
    img.colorspace_settings.name = 'Non-Color'
    img.alpha_mode = 'CHANNEL_PACKED'
    img.pixels.foreach_set(np.ascontiguousarray(values).ravel())
    img.filepath_raw = str(path)
    img.file_format = 'PNG'
    img.save()
    bpy.data.images.remove(img)


def main():
    archives = sorted((SOURCE / 'source').glob('*.zip'))
    if len(archives) != 1:
        raise RuntimeError('Expected exactly one supplied source ZIP.')
    inputs = [archives[0]] + [SOURCE / 'textures' / p for part in PARTS for p in part[2:6]]
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    TEXTURES.mkdir(parents=True, exist_ok=True)
    counts = []
    with tempfile.TemporaryDirectory(prefix='blackout-EG1-') as folder:
        folder = Path(folder)
        with zipfile.ZipFile(archives[0]) as archive:
            # Extract only named files; never trust paths embedded in an archive.
            for name in ['Gun.obj'] + [part[6] for part in PARTS]:
                (folder / name).write_bytes(archive.read(name))
        # The author's MTL contains absolute paths on their PC. A temporary name-only MTL
        # avoids unresolved images; the real supplied maps are converted explicitly below.
        (folder / 'Gun.mtl').write_text('\n'.join('newmtl ' + part[1] + '\nKd 0.6 0.5 0.4' for part in PARTS))
        bpy.ops.wm.obj_import(filepath=str(folder / 'Gun.obj'), forward_axis='Y', up_axis='Z')
        source = next(obj for obj in bpy.context.scene.objects if obj.type == 'MESH')
        # Axis swap matches the established custom-model pipeline. Reflection reverses winding.
        source.data.transform(Matrix(((.01, 0, 0, -.01 * GRIP[0]),
                                      (0, 0, .01, -.01 * GRIP[2]),
                                      (0, .01, 0, -.01 * GRIP[1]), (0, 0, 0, 1))))
        source.data.flip_normals()
        for part, original, albedo, metal, rough, ao, normal, size in PARTS:
            mat = next(m for m in source.data.materials if m.name == original)
            mat.name = 'M_EnemyGun_' + part
            prefix = TEXTURES / ('T_EnemyGun_' + part + '_')
            colour = image(SOURCE / 'textures' / albedo, size)
            colour[:, :, 3] = 1
            write_png(Path(str(prefix) + 'BaseColor.png'), colour)
            write_png(Path(str(prefix) + 'Normal.png'), image(folder / normal, 512))
            packed = np.zeros((size, size, 4), dtype=np.float32)
            packed[:, :, 0] = image(SOURCE / 'textures' / metal, size)[:, :, 0]
            packed[:, :, 1] = image(SOURCE / 'textures' / ao, size)[:, :, 0]
            packed[:, :, 3] = 1 - image(SOURCE / 'textures' / rough, size)[:, :, 0]
            write_png(Path(str(prefix) + 'MetalSmooth.png'), packed)
        # One mesh / three submeshes per level. Collapse is deterministic; all levels retain UVs.
        for level, ratio in enumerate((.45, .14, .04)):
            obj = bpy.data.objects.new('EnemyGun_LOD' + str(level), source.data.copy())
            bpy.context.collection.objects.link(obj)
            bpy.context.view_layer.objects.active = obj
            decimate = obj.modifiers.new('Enemy gun reduction', 'DECIMATE')
            decimate.ratio = ratio
            decimate.use_collapse_triangulate = True
            bpy.ops.object.modifier_apply(modifier=decimate.name)
            triangulate = obj.modifiers.new('Triangles', 'TRIANGULATE')
            bpy.ops.object.modifier_apply(modifier=triangulate.name)
            counts.append(len(obj.data.polygons))
        bpy.data.objects.remove(source, do_unlink=True)
        bpy.ops.object.select_all(action='SELECT')
        bpy.ops.export_scene.fbx(filepath=str(MODEL), use_selection=True, object_types={'MESH'},
            global_scale=1, apply_unit_scale=True, apply_scale_options='FBX_SCALE_UNITS',
            axis_forward='-Z', axis_up='Y', use_space_transform=True, bake_space_transform=True,
            mesh_smooth_type='FACE', use_mesh_modifiers=True, add_leaf_bones=False,
            bake_anim=False, path_mode='STRIP', use_metadata=False)
    manifest = {
        'source': str(SOURCE), 'credit': 'Dries Deryckere (supplied MTL/texture filenames)',
        'licence': 'No licence text included in supplied files.',
        'source_triangles': 21660, 'lod_triangles': counts,
        'source_grip_cm': GRIP, 'unity_muzzle_metres': [0.0002, 0.08965, 0.577724],
        'sha256': {str(p.relative_to(SOURCE)): hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs},
    }
    (REPO / 'Art/Blender/EnemyGun.source.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('[EG1] triangles by LOD:', counts, 'total:', sum(counts), flush=True)


if __name__ == '__main__':
    main()
