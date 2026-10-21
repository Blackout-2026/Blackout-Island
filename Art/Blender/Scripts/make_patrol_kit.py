"""Makes the colour swatch texture T_CharPatrolKit_BaseColor.png of the Security Patrol's skin, helmet, vest, belt and boots (step C10, PS2). Owner: Core Developer.

    python Art/Blender/Scripts/make_patrol_kit.py

64 x 32, eight flat colour swatches in two rows of four (u to the right, v UP as in Blender and Unity):
    top row     skin | helmet and rifle tan | vest olive | dark (gloves, straps, radio, eyes)
    bottom row  leather brown | webbing mustard | khaki (boots and face mask) | spare
Cell centres are u = 0.125, 0.375, 0.625, 0.875 and v = 0.75 (top) or 0.25 (bottom). build_humans.py moves the UVs of every face of the
Kit material onto one of these centres (like the Hunter's skin swatches), so the whole kit shares the one material M_Char_Patrol_Kit. Fixed seed.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
W, H = 64, 32
rng = np.random.default_rng(8)

SWATCHES = [   # (row, column): sRGB colour. Row 0 is the TOP of the image, so v 0.75.
    ((0, 0), (0.640, 0.470, 0.370)),   # skin
    ((0, 1), (0.700, 0.630, 0.500)),   # helmet and rifle tan
    ((0, 2), (0.330, 0.290, 0.190)),   # vest olive
    ((0, 3), (0.075, 0.080, 0.065)),   # dark
    ((1, 0), (0.300, 0.200, 0.120)),   # leather brown
    ((1, 1), (0.560, 0.460, 0.200)),   # webbing mustard
    ((1, 2), (0.470, 0.410, 0.300)),   # khaki: boots and face mask
    ((1, 3), (0.200, 0.170, 0.120)),   # spare
]


def main():
    img = np.zeros((H, W, 3))
    cell_h, cell_w = H // 2, W // 4
    for (row, col), colour in SWATCHES:
        block = np.ones((cell_h, cell_w, 3)) * np.array(colour)
        block *= (1.0 + 0.04 * (rng.random((cell_h, cell_w, 1)) - 0.5))
        img[row * cell_h:(row + 1) * cell_h, col * cell_w:(col + 1) * cell_w] = block
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrolKit_BaseColor.png"))
    print(f"[PatrolKit] {OUT}")


if __name__ == "__main__":
    main()
