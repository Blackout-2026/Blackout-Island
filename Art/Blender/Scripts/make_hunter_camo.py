"""Makes the camouflage texture T_CharHunter_BaseColor.png of the Hunter's uniform and cap (step C10). Owner: Core Developer.

    python Art/Blender/Scripts/make_hunter_camo.py

512 x 512, seamless, one tile = one metre of cloth (build_humans.py projects the UVs in world metres). A very dark forest-green base with irregular blotches
of moss green, bark brown and near-black green, as on the field uniform of the reference photo: two noise scales are thresholded into
soft-edged blotches, and a fine weave and fibre noise keeps the flat cloth from looking like plastic. Our own pattern from a fixed seed,
so a re-run gives the same pixels.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 512
rng = np.random.default_rng(31)


def periodic_noise(beta, lo=1.0, hi=None):
    """Seamless noise with a 1/f^beta spectrum, band-limited to [lo, hi] cycles per tile, normalised to 0..1."""
    f = np.fft.fftfreq(SIZE) * SIZE
    fx, fy = np.meshgrid(f, f)
    r = np.sqrt(fx * fx + fy * fy)
    amp = np.where(r >= lo, 1.0 / np.maximum(r, 1.0) ** beta, 0.0)
    if hi is not None:
        amp = np.where(r <= hi, amp, 0.0)
    spec = np.fft.fft2(rng.standard_normal((SIZE, SIZE))) * amp
    n = np.real(np.fft.ifft2(spec))
    n -= n.min()
    return n / n.max()


def smoothstep(x, a, b):
    t = np.clip((x - a) / (b - a), 0, 1)
    return t * t * (3 - 2 * t)


def main():
    big = periodic_noise(1.3, 4, 14)       # large blotches, about 8 cm across at one tile per metre
    mid = periodic_noise(1.2, 8, 28)       # medium blotches
    small = periodic_noise(1.0, 14, 60)    # small flecks
    tan = np.array([0.150, 0.185, 0.110])      # the base is a very dark forest green, sRGB about (38, 47, 28)
    olive = np.array([0.245, 0.290, 0.155])    # a darker moss green, (62, 74, 40)
    brown = np.array([0.165, 0.130, 0.085])    # dark bark brown, (42, 33, 22)
    dark = np.array([0.045, 0.058, 0.040])     # near black green, (11, 15, 10)

    img = np.ones((SIZE, SIZE, 3)) * tan
    olive_mask = smoothstep(big, 0.56, 0.61)
    brown_mask = smoothstep(mid, 0.60, 0.65) * (1.0 - olive_mask * 0.4)
    dark_mask = smoothstep(small + 0.25 * mid, 0.80, 0.85)
    for mask, colour in ((olive_mask, olive), (brown_mask, brown), (dark_mask, dark)):
        img = img * (1.0 - mask[..., None]) + colour * mask[..., None]

    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    weave = 0.5 + 0.25 * np.sin(2 * np.pi * 96 * xs) + 0.25 * np.sin(2 * np.pi * 96 * ys)
    fibre = periodic_noise(0.4, 90, 230)
    img *= (0.93 + 0.07 * weave + 0.05 * (fibre - 0.5))[..., None]
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharHunter_BaseColor.png"))

    # the matching normal map: the weave, the fibres and a slight puckering at the blotch edges, so the cloth catches the light
    height = 0.55 * weave + 0.25 * fibre + 0.20 * (olive_mask * 0.5 + brown_mask * 0.7 + dark_mask)
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx, ny, nz = -gx * 12.0, -gy * 12.0, np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / norm, ny / norm, nz / norm], axis=-1) * 0.5 + 0.5
    Image.fromarray((normal * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharHunter_Normal.png"))
    print(f"[Camo] {OUT}")


if __name__ == "__main__":
    main()
