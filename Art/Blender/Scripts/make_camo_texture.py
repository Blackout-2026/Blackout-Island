"""Makes the digital-camouflage sleeve texture T_WeaponSleeve_BaseColor.png (step C7). Owner: Core Developer.

    python Art/Blender/Scripts/make_camo_texture.py

A 512 x 512 tiling "pixel" camouflage in four muted greens and tans: blotches made from seamless noise at two scales, cut into
square blocks, then a fine cloth weave over the top. Our own pattern from a fixed seed, so a re-run gives the same pixels.
Style reference for the whole sleeve: a military uniform sleeve in a digital camo with a plain cuff (Art/Reference/Hands/).
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models", "T_WeaponSleeve_BaseColor.png")
SIZE = 512
BLOCK = 8  # px per camo square
rng = np.random.default_rng(7)


def periodic_noise(cells):
    """Seamless value noise: random values on a cells x cells grid, bilinearly smoothed and wrapped."""
    grid = rng.random((cells, cells))
    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / (SIZE / cells)
    x0, y0 = xs.astype(int) % cells, ys.astype(int) % cells
    x1, y1 = (x0 + 1) % cells, (y0 + 1) % cells
    fx, fy = xs - np.floor(xs), ys - np.floor(ys)
    fx, fy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    top = grid[y0, x0] * (1 - fx) + grid[y0, x1] * fx
    bottom = grid[y1, x0] * (1 - fx) + grid[y1, x1] * fx
    return top * (1 - fy) + bottom * fy


def main():
    n = 0.62 * periodic_noise(6) + 0.38 * periodic_noise(14)
    small = SIZE // BLOCK
    # one value per block: average the noise inside each square
    blocks = n.reshape(small, BLOCK, small, BLOCK).mean(axis=(1, 3))
    jitter = rng.random((small, small)) * 0.12 - 0.06
    v = np.clip(blocks + jitter, 0, 1)
    colours = np.array([[0x4a, 0x4f, 0x3f], [0x6c, 0x73, 0x5c], [0x8d, 0x91, 0x7c], [0xa8, 0xa0, 0x84]], dtype=float)
    levels = np.digitize(v, np.quantile(v, [0.28, 0.58, 0.86]))
    img = colours[levels]
    img = np.repeat(np.repeat(img, BLOCK, axis=0), BLOCK, axis=1)
    # cloth weave: a fine criss-cross of slightly lighter and darker threads
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]
    weave = 1.0 + 0.05 * (((xs // 2) + (ys // 2)) % 2 - 0.5) + 0.03 * rng.standard_normal((SIZE, SIZE))
    img = np.clip(img * weave[..., None], 0, 255).astype(np.uint8)
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    Image.fromarray(img).save(OUT)
    print(f"[Camo] {OUT}")


if __name__ == "__main__":
    main()
