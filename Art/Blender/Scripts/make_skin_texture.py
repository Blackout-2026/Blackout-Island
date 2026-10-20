"""Makes the skin textures T_WeaponSkin_BaseColor.png and T_WeaponSkin_Normal.png for the viewmodel hands (step C7). Owner: Core Developer.

    python Art/Blender/Scripts/make_skin_texture.py

512 x 512, seamless (every noise is made in the Fourier domain, so it wraps). BaseColor: a medium skin tone with slow reddish and
yellowish mottling, pores, and faint blue-green veins. Normal: the fine criss-cross relief of real skin plus the pores, so the
light breaks up on it instead of sliding over a plastic surface. Our own pattern from a fixed seed, so a re-run gives the same pixels.
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
SIZE = 512
rng = np.random.default_rng(11)


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
    base = np.array([0.768, 0.560, 0.440])  # sRGB 196,143,112
    blotch = periodic_noise(1.6, 1, 12) - 0.5
    warm = periodic_noise(1.8, 1, 8) - 0.5
    pores_raw = periodic_noise(0.0, 90, 200)
    pores = smoothstep(pores_raw, 0.80, 0.93)  # sparse small dark dots
    fine = periodic_noise(0.6, 60, 220)
    # criss-cross skin relief: two crossed diagonal wave patterns bent by noise
    ys, xs = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    bend = (periodic_noise(1.5, 2, 10) - 0.5) * 0.9
    k = 38.0
    line_a = np.abs(np.sin(np.pi * (k * (xs + ys) + 6 * bend)))
    line_b = np.abs(np.sin(np.pi * (k * (xs - ys) + 6 * bend)))
    creases = 1.0 - np.minimum(line_a, line_b)
    creases = smoothstep(creases, 0.78, 0.97)
    veins_raw = periodic_noise(2.2, 2, 7)
    veins = 1.0 - smoothstep(np.abs(veins_raw - 0.5), 0.0, 0.03)  # thin ridges where the noise crosses 0.5

    img = np.ones((SIZE, SIZE, 3)) * base
    img[..., 0] *= 1.0 + 0.06 * warm * 2
    img[..., 1] *= 1.0 + 0.07 * blotch * 2
    img[..., 2] *= 1.0 + 0.08 * blotch * 2 - 0.05 * warm * 2
    img *= (1.0 - 0.11 * pores)[..., None]
    img *= (1.0 - 0.035 * creases)[..., None]
    img *= (1.0 + 0.05 * (fine - 0.5))[..., None]
    vein_tint = np.array([0.80, 0.90, 1.02])  # slightly cooler and darker
    img = img * (1.0 - 0.07 * veins[..., None]) + img * vein_tint * 0.07 * veins[..., None]
    img = np.clip(img, 0, 1)
    Image.fromarray((img * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_WeaponSkin_BaseColor.png"))

    height = 0.55 * creases + 0.35 * pores + 0.25 * (fine - 0.5) + 0.10 * (periodic_noise(1.0, 4, 30) - 0.5)
    gy, gx = np.gradient(height)
    strength = 14.0
    nx, ny = -gx * strength, -gy * strength
    nz = np.ones_like(nx)
    ln = np.sqrt(nx * nx + ny * ny + nz * nz)
    normal = np.dstack([nx / ln, ny / ln, nz / ln]) * 0.5 + 0.5
    Image.fromarray((normal * 255 + 0.5).astype(np.uint8)).save(os.path.join(OUT, "T_WeaponSkin_Normal.png"))
    print(f"[Skin] {OUT}")


if __name__ == "__main__":
    main()
