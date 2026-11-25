"""Retint the existing Commander atlases for the charcoal and burgundy field coat.

Run with Blender's bundled Python through Blender -b -P. This uses Blender's image
IO and NumPy, so the art build does not need a separate Pillow installation.
"""

import os
import sys

import bpy
import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import commander_atlas as A

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
TEXTURES = os.path.join(ROOT, "Assets", "Textures", "Models")


def linear(hex_color):
    """Blender's image pixel buffer is scene-linear, while PNG swatches are sRGB."""
    rgb = np.array([int(hex_color[i:i + 2], 16) / 255.0 for i in (1, 3, 5)], dtype=np.float32)
    return np.where(rgb <= 0.04045, rgb / 12.92, ((rgb + 0.055) / 1.055) ** 2.4)


def load(name):
    image = bpy.data.images.load(os.path.join(TEXTURES, name), check_existing=False)
    width, height = image.size
    pixels = np.empty(width * height * 4, dtype=np.float32)
    image.pixels.foreach_get(pixels)
    return image, pixels.reshape((height, width, 4))


def save(image, pixels):
    image.pixels.foreach_set(pixels.ravel())
    image.file_format = "PNG"
    image.save()
    bpy.data.images.remove(image)


def region(pixels, rect):
    x, y, w, h = rect
    height = pixels.shape[0]
    return pixels[height - y - h:height - y, x:x + w]


def main():
    uniform, pixels = load("T_CharCommanderUniform_BaseColor.png")
    for name, (rect, color, _, _) in A.UNIFORM_SWATCHES.items():
        region(pixels, rect)[..., :3] = linear(color)
    cloth = region(pixels, A.UNIFORM_DECALS["cloth"])
    old_luma = cloth[..., :3].mean(axis=2, keepdims=True)
    cloth[..., :3] = np.clip(linear("#29282b") + (old_luma - old_luma.mean()) * 0.15, 0, 1)
    save(uniform, pixels)

    hair, pixels = load("T_CharCommanderHair_BaseColor.png")
    # Retain the painted strand contrast and alpha while taking the hair to dark brown.
    rgb = pixels[..., :3]
    luma = rgb.mean(axis=2, keepdims=True)
    rgb[:] = np.clip(linear("#262124") + (luma - luma.mean()) * 0.38, 0, 1)
    save(hair, pixels)

    skin, pixels = load("T_CharCommanderSkin_BaseColor.png")
    # Keep the projected eyes, but remove the old pink lip paint and add a little
    # jaw shadow for the older, clean-shaven officer in the supplied reference.
    face = pixels[::-1, :640, :3]
    yy, xx = np.ogrid[:face.shape[0], :face.shape[1]]
    lip = ((xx - 326.0) / 79.0) ** 2 + ((yy - 582.0) / 36.0) ** 2 < 1.0
    face[lip] = face[lip] * 0.02 + linear("#dab099") * 0.98
    jaw = (((xx - 323.0) / 148.0) ** 2 + ((yy - 604.0) / 138.0) ** 2 < 1.0) & (yy > 526)
    grain = np.random.default_rng(47).random(face.shape[:2], dtype=np.float32)
    shadow = jaw * (0.045 + 0.035 * grain)
    face[:] = face * (1.0 - shadow[..., None])
    save(skin, pixels)
    print("[Commander] Retinted uniform, hair and face textures")


if __name__ == "__main__":
    main()
