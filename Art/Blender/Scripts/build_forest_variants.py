"""FD1: additional shapes from the supplied forest GLB and Small Trees FBX.

Run with Blender -b --factory-startup -P Art/Blender/Scripts/build_forest_variants.py.
Source files and the earlier F1 assets are read-only. Reuses the F1 UV-preserving
card thinning, decimation and FBX export; only the seven new models are exported.
"""
import os
import sys
sys.dont_write_bytecode = True

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(__file__))
import build_ponderosa_forest as forest

SMALL = r"D:\model\small-trees"


def texture(source, target, opacity=None, normal=False):
    image = bpy.data.images.load(os.path.join(SMALL, "textures", source))
    if normal:
        image.colorspace_settings.name = 'Non-Color'
    image.alpha_mode = 'CHANNEL_PACKED'
    if opacity:
        mask = bpy.data.images.load(os.path.join(SMALL, "textures", opacity))
        mask.colorspace_settings.name = 'Non-Color'
        # Both source maps are 2048 square. Compose alpha before resizing: Blender's
        # Python pixel wrapper can retain its old length immediately after scale().
        rgba = np.array(image.pixels[:], dtype=np.float32).reshape(-1, 4)
        rgba[:, 3] = np.array(mask.pixels[:], dtype=np.float32).reshape(-1, 4)[:, 0]
        # A JPEG-backed image still saves RGB even after writing pixel alpha. Use a
        # new RGBA image so the leaf cutout survives PNG export and Unity import.
        image = bpy.data.images.new(target, width=image.size[0], height=image.size[1], alpha=True)
        image.alpha_mode = 'CHANNEL_PACKED'
        image.pixels.foreach_set(rgba.ravel())
        image.update()
    image.scale(1024, 1024)
    image.filepath_raw = os.path.join(forest.TEX_DIR, target)
    image.file_format = 'PNG'
    image.save()


def main():
    source = forest.import_glb()
    models = {}
    for name, mesh, width in (
            ("RockWide", "Icosphere.005", 2.1),
            ("RockRound", "Icosphere.001", 1.4)):
        forest.ROCKS[name] = (mesh, width, 0.85, (650, 220, 70))
        models["SM_Forest_" + name] = forest.build_rock(source, name)
    for name, mesh, height in (
            ("BushWide", "Ponderosa Pine seedling.036_foliage", 1.15),
            ("BushUpright", "Ponderosa Pine seedling.047_foliage", 1.45)):
        data = forest.world_mesh(source[mesh], name)
        forest.ground([data], data, height, 1.0)
        material = forest.image_material("M_Forest_Bush", "T_PonderosaNeedles_BaseColor.png", alpha=True)
        models["SM_Forest_" + name] = [forest.make_object(name, [data], [material])]

    bpy.ops.import_scene.fbx(filepath=os.path.join(SMALL, "source", "Small Trees.fbx"))
    # The FBX contains broken author-machine texture paths; resolve the supplied maps explicitly.
    texture("qimhv_2K_Albedo.jpg", "T_SmallTree_Leaves_BaseColor.png", "qimhv_2K_Opacity.jpg")
    texture("vmbibijg_2K_Albedo.jpg", "T_SmallTree_Bark_BaseColor.png")
    texture("vmbibijg_2K_Normal.jpg", "T_SmallTree_Bark_Normal.png", normal=True)
    bark = forest.image_material("M_SmallTree_Bark", "T_SmallTree_Bark_BaseColor.png", normal="T_SmallTree_Bark_Normal.png")
    leaves = forest.image_material("M_SmallTree_Leaves", "T_SmallTree_Leaves_BaseColor.png", alpha=True)
    for name, trunk_name, leaf_name, height in (
            ("SmallTreeA", "tree", "leaves.004", 6.2),
            ("SmallTreeB", "tree.001", "leaves.005", 3.8),
            ("SmallTreeC", "tree.002", "leaves.006", 7.4)):
        trunk = forest.world_mesh(bpy.data.objects[trunk_name], name + "Bark")
        foliage = forest.world_mesh(bpy.data.objects[leaf_name], name + "Leaves")
        forest.ground([trunk, foliage], trunk, height, 1.0)
        objects = []
        for lod, (branch_budget, leaf_budget) in enumerate(((850, 1500), (250, 430), (70, 120))):
            branch = forest.main_limbs(trunk.copy(), branch_budget * 2)
            parts = [forest.decimated(branch, branch_budget),
                     forest.thin_cards(foliage.copy(), leaf_budget, (1.25, 1.8, 2.6)[lod])]
            obj = forest.make_object(f"{name}_LOD{lod}", parts, [bark, leaves])
            low = min(v.co.z for v in obj.data.vertices)
            for vertex in obj.data.vertices:
                vertex.co.z -= low
            objects.append(obj)
            print(f"[FD1] {name} LOD{lod}: {forest.tri_count(obj.data)} triangles")
        models["SM_Forest_" + name] = objects
    for name, objects in models.items():
        forest.report(name, objects[0])
        forest.export(objects, name + ".fbx", "")
    print("[FD1] Exported seven new models; original sources and earlier models retained.")


if __name__ == "__main__":
    main()
