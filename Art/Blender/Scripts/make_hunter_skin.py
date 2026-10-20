"""Makes the colour swatch texture T_CharHunterSkin_BaseColor.png of the Hunter's eye area (step C10). Owner: Core Developer.

    python Art/Blender/Scripts/make_hunter_skin.py

64 x 64, four flat colour swatches, one per quadrant (u to the right, v UP as in Blender and Unity):
    top left      skin tone       UV centre (0.25, 0.75)
    top right     eye white       UV centre (0.75, 0.75)
    bottom left   iris and brows  UV centre (0.25, 0.25)
    bottom right  pupil and lids  UV centre (0.75, 0.25)
build_humans.py moves the UVs of the tagged faces onto these centres, so the skin patch, the eyeballs, the iris, the pupil, the lid lines
and the brows all use the one material M_Char_Hunter_Skin (a character may use 3 materials). Each swatch has a small soft-edged variation so
the skin is not one flat colour. Fixed seed.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 64
rng = np.random.default_rng(5)


def main():
    img = np.zeros((SIZE, SIZE, 3))
    half = SIZE // 2
    swatches = {
        (0, 0): (0.690, 0.510, 0.405),   # skin, sRGB about (176, 130, 103): top left (image row 0 is the TOP, so v 0.75 is here)
        (0, 1): (0.900, 0.885, 0.850),   # eye white, slightly warm: top right
        (1, 0): (0.215, 0.145, 0.090),   # iris and brows, dark brown: bottom left
        (1, 1): (0.030, 0.028, 0.028),   # pupil and lid lines: bottom right
    }
    for (row, col), colour in swatches.items():
        block = np.ones((half, half, 3)) * np.array(colour)
        block *= (1.0 + 0.04 * (rng.random((half, half, 1)) - 0.5))
        img[row * half:(row + 1) * half, col * half:(col + 1) * half] = block
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharHunterSkin_BaseColor.png"))
    print(f"[Skin] {OUT}")


if __name__ == "__main__":
    main()
