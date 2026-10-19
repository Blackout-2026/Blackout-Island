"""Makes the glove textures T_WeaponGlove_BaseColor.png and T_WeaponGlove_Normal.png for the viewmodel hands (step C7). Owner: Core Developer.

    python Art/Blender/Scripts/make_glove_texture.py

512 x 512, seamless (every noise is made in the Fourier domain, so it wraps). The look is a tactical shooting glove in coyote tan, like the
gloves of the reference photos: a woven fabric (fine crossing threads), slow mottling from wear and dust, a few darker specks and
scuffs. The normal map is the weave plus the soft grain, so the light breaks up on the hand instead of sliding over plastic.
Our own pattern from a fixed seed, so a re-run gives the same pixels.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 512
rng = np.random.default_rng(23)


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
    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    threads = 64  # thread pairs across the tile
    warp = 0.5 + 0.5 * np.sin(2 * np.pi * threads * xs)
    weft = 0.5 + 0.5 * np.sin(2 * np.pi * threads * ys)
    checker = np.sin(2 * np.pi * threads * 0.5 * (xs + ys)) * np.sin(2 * np.pi * threads * 0.5 * (xs - ys))
    weave = 0.55 * (warp * 0.5 + weft * 0.5) + 0.45 * (0.5 + 0.5 * checker)   # 0..1, crossing threads over and under
    grain = periodic_noise(0.5, 80, 230)
    mottle = periodic_noise(1.7, 1, 10) - 0.5
    dust = periodic_noise(1.2, 3, 30) - 0.5
    specks = smoothstep(periodic_noise(0.0, 60, 200), 0.86, 0.95)
    scuffs = smoothstep(periodic_noise(1.0, 2, 14), 0.74, 0.90)

    base = np.array([0.560, 0.455, 0.305])             # coyote tan, sRGB about (143, 116, 78)
    img = np.ones((SIZE, SIZE, 3)) * base
    img *= (0.90 + 0.16 * weave)[..., None]            # the threads
    img *= (1.0 + 0.30 * mottle)[..., None]            # slow wear
    img[..., 0] *= 1.0 + 0.10 * dust
    img[..., 2] *= 1.0 - 0.18 * dust                   # dusty areas shift a little towards grey-brown
    img *= (1.0 - 0.30 * specks)[..., None]            # dark specks of dirt
    img *= (1.0 - 0.18 * scuffs)[..., None]            # darker scuffs
    img *= (0.97 + 0.06 * grain)[..., None]
    img = np.clip(img, 0.0, 1.0)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_WeaponGlove_BaseColor.png"))

    height = 0.70 * weave + 0.20 * grain + 0.10 * scuffs
    gx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    gy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    strength = 14.0
    nx, ny, nz = -gx * strength, -gy * strength, np.ones_like(height)
    norm = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.stack([nx / norm, ny / norm, nz / norm], axis=-1) * 0.5 + 0.5
    Image.fromarray((normal * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_WeaponGlove_Normal.png"))
    print(f"[Glove] {OUT}")


if __name__ == "__main__":
    main()
