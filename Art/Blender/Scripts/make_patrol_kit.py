"""Makes the colour swatch texture T_CharPatrolKit_BaseColor.png of the Security Patrol's skin, vest, rifle and boots (step C10). Owner: Core Developer.

    python Art/Blender/Scripts/make_patrol_kit.py

64 x 64, four flat colour swatches, one per quadrant (u to the right, v UP as in Blender and Unity):
    top left      skin tone              UV centre (0.25, 0.75)
    top right     rifle tan (FDE)        UV centre (0.75, 0.75)
    bottom left   vest and pouch olive   UV centre (0.25, 0.25)
    bottom right  boots, gloves, straps  UV centre (0.75, 0.25)
build_humans.py moves the UVs of every face of the Kit material onto one of these centres (like the Hunter's skin swatches), so the
face, the vest, the rifle furniture and the boots all use the one material M_Char_Patrol_Kit. Fixed seed.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 64
rng = np.random.default_rng(8)


def main():
    img = np.zeros((SIZE, SIZE, 3))
    half = SIZE // 2
    swatches = {
        (0, 0): (0.640, 0.470, 0.370),   # skin
        (0, 1): (0.600, 0.520, 0.390),   # rifle tan
        (1, 0): (0.300, 0.320, 0.190),   # vest olive
        (1, 1): (0.075, 0.080, 0.065),   # boots, gloves, straps, eyes
    }
    for (row, col), colour in swatches.items():
        block = np.ones((half, half, 3)) * np.array(colour)
        block *= (1.0 + 0.04 * (rng.random((half, half, 1)) - 0.5))
        img[row * half:(row + 1) * half, col * half:(col + 1) * half] = block
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrolKit_BaseColor.png"))
    print(f"[PatrolKit] {OUT}")


if __name__ == "__main__":
    main()
