"""Makes the weathered desert cloth texture T_CharPatrol_BaseColor.png (and its normal map) of the Security Patrol's jacket and trousers. Owner: Core Developer.

    python Art/Blender/Scripts/make_patrol_cloth.py

512 x 512, seamless, one tile = one metre of cloth (build_humans.py projects the UVs in world metres). The tan uniform of the owner's
photo: a khaki base with soft dust and dirt stains, a few lighter sun-faded patches and a fine twill weave. It replaces the woodland camouflage
of step PS1 (make_patrol_camo.py, now removed). Our own pattern from a fixed seed, so a re-run gives the same pixels.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 512
rng = np.random.default_rng(83)


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
    big = periodic_noise(1.6, 2, 8)
    mid = periodic_noise(1.3, 5, 20)
    khaki = np.array([0.520, 0.430, 0.310])    # base khaki, sRGB about (133, 110, 79)
    dirt = np.array([0.390, 0.320, 0.230])     # dust and dirt stains
    faded = np.array([0.610, 0.530, 0.400])    # sun-faded patches

    img = np.ones((SIZE, SIZE, 3)) * khaki
    dirt_mask = smoothstep(big, 0.50, 0.78) * 0.65 + smoothstep(mid, 0.62, 0.80) * 0.25
    faded_mask = smoothstep(mid, 0.40, 0.12) * 0.40
    img = img * (1.0 - dirt_mask[..., None]) + dirt * dirt_mask[..., None]
    img = img * (1.0 - faded_mask[..., None]) + faded * faded_mask[..., None]

    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    twill = 0.5 + 0.25 * np.sin(2 * np.pi * 96 * (xs + ys)) + 0.25 * np.sin(2 * np.pi * 96 * (xs - ys) * 0.5)
    fibre = periodic_noise(0.4, 90, 230)
    img *= (0.93 + 0.07 * twill + 0.05 * (fibre - 0.5))[..., None]
    img = np.clip(img, 0.0, 1.0)
    os.makedirs(OUT, exist_ok=True)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrol_BaseColor.png"))

    height = 0.55 * twill + 0.25 * fibre + 0.20 * dirt_mask
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx, ny, nz = -gx * 12.0, -gy * 12.0, np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / norm, ny / norm, nz / norm], axis=-1) * 0.5 + 0.5
    Image.fromarray((normal * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_CharPatrol_Normal.png"))
    print(f"[PatrolCloth] {OUT}")


if __name__ == "__main__":
    main()
