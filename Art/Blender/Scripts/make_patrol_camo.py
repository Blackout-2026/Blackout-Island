"""Makes the camouflage texture T_CharPatrol_BaseColor.png (and its normal map) of the Security Patrol's uniform and helmet cover. Owner: Core Developer.

    python Art/Blender/Scripts/make_patrol_camo.py

512 x 512, seamless, one tile = one metre of cloth (build_humans.py projects the UVs in world metres). A woodland pattern like the
soldier of the owner's reference photo: a sage green base with blotches of dark green, bark brown and sand. Lighter than the Hunter's
suit (make_hunter_camo.py) so the two read apart at a distance. Our own pattern from a fixed seed, so a re-run gives the same pixels.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 512
rng = np.random.default_rng(57)


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
    big = periodic_noise(1.3, 4, 12)
    mid = periodic_noise(1.2, 7, 24)
    small = periodic_noise(1.0, 14, 55)
    sage = np.array([0.380, 0.450, 0.280])     # base sage green, sRGB about (97, 115, 71)
    dark = np.array([0.190, 0.270, 0.150])     # dark forest green
    brown = np.array([0.380, 0.290, 0.180])    # bark brown
    sand = np.array([0.640, 0.590, 0.420])     # sand / khaki

    img = np.ones((SIZE, SIZE, 3)) * sage
    sand_mask = smoothstep(big, 0.55, 0.60)
    dark_mask = smoothstep(mid, 0.58, 0.63) * (1.0 - sand_mask * 0.5)
    brown_mask = smoothstep(small + 0.30 * big, 0.80, 0.86) * (1.0 - dark_mask * 0.6)
    for mask, colour in ((sand_mask, sand), (dark_mask, dark), (brown_mask, brown)):
        img = img * (1.0 - mask[..., None]) + colour * mask[..., None]

    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    weave = 0.5 + 0.25 * np.sin(2 * np.pi * 96 * xs) + 0.25 * np.sin(2 * np.pi * 96 * ys)
    fibre = periodic_noise(0.4, 90, 230)
    img *= (0.93 + 0.07 * weave + 0.05 * (fibre - 0.5))[..., None]
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrol_BaseColor.png"))

    height = 0.55 * weave + 0.25 * fibre + 0.20 * (sand_mask * 0.5 + dark_mask * 0.7 + brown_mask)
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx, ny, nz = -gx * 12.0, -gy * 12.0, np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / norm, ny / norm, nz / norm], axis=-1) * 0.5 + 0.5
    Image.fromarray((normal * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrol_Normal.png"))
    print(f"[PatrolCamo] {OUT}")


if __name__ == "__main__":
    main()
