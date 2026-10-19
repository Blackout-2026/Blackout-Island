"""Makes the tiling textures of the MetalGrating floor surface (step C7). Owner: Core Developer.

    python Art/Blender/Scripts/make_metal_grating.py

Writes Assets/Textures/Environment/MetalGrating/MetalGrating_{BaseColor,Normal,Roughness,Metallic,AO}.png, the file names
Build Material Library looks for (Assets/Editor/WorldBuilder/MaterialLibraryBuilder.cs). One tile covers 1 m
(MetalGrating repeats 1 per metre in SurfaceLibrary), 2048 x 2048 px, 32 x 32 cells of about 3 cm. The names end in _Hero, so
the importer keeps them at 2048 (EnvironmentTextureImport).

The tile is a welded steel grid: rounded load bars one way, thinner cross bars the other, and dark open holes you look down
into. Everything is generated from a fixed seed with periodic noise, so the tile repeats without seams and a re-run gives
the same pixels. Style reference: a dark industrial grating floor with worn bar tops (Art/Reference/Floor/).
"""

import os

import numpy as np
from PIL import Image

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Environment", "MetalGrating")
SIZE = 2048
CELLS = 32
PITCH = SIZE // CELLS  # 64 px
LOAD_BAR = 15  # px, bars along one direction
CROSS_BAR = 9  # px, the thinner cross bars the other way
rng = np.random.default_rng(1204)


def periodic_noise(scale, octaves=4):
    """Seamless value noise in 0..1: filtered white noise (FFT), so the tile wraps."""
    out = np.zeros((SIZE, SIZE))
    amp = 1.0
    total = 0.0
    for o in range(octaves):
        white = rng.standard_normal((SIZE, SIZE))
        f = np.fft.fft2(white)
        fy = np.fft.fftfreq(SIZE)[:, None]
        fx = np.fft.fftfreq(SIZE)[None, :]
        radius = np.sqrt(fx * fx + fy * fy)
        cutoff = 1.0 / (scale / (2 ** o))
        f *= np.exp(-(radius / cutoff) ** 2 * 40.0) if cutoff > 0 else 0
        layer = np.real(np.fft.ifft2(f))
        layer = (layer - layer.min()) / (layer.max() - layer.min())
        out += layer * amp
        total += amp
        amp *= 0.5
    out /= total
    return (out - out.min()) / (out.max() - out.min())


def scratches(count, length, width=1):
    """Thin light scuffs along the bars, wrapped around the tile."""
    img = np.zeros((SIZE, SIZE))
    for _ in range(count):
        x, y = rng.integers(0, SIZE, 2)
        horizontal = rng.random() < 0.5
        n = int(rng.integers(length // 2, length))
        for k in range(n):
            xx = (x + (k if horizontal else int(rng.integers(-1, 2)))) % SIZE
            yy = (y + (int(rng.integers(-1, 2)) if horizontal else k)) % SIZE
            img[yy, xx] = max(img[yy, xx], rng.uniform(0.4, 1.0))
    return img


def blur(a, passes=1):
    for _ in range(passes):
        a = (a + np.roll(a, 1, 0) + np.roll(a, -1, 0) + np.roll(a, 1, 1) + np.roll(a, -1, 1)) / 5.0
    return a


def main():
    ys, xs = np.mgrid[0:SIZE, 0:SIZE]
    # distance to the nearest bar centre, per direction (bars sit on the cell lines, so the tile wraps)
    dx = np.minimum(xs % PITCH, PITCH - xs % PITCH).astype(float)
    dy = np.minimum(ys % PITCH, PITCH - ys % PITCH).astype(float)
    load = dx < LOAD_BAR / 2.0  # vertical bars, wide
    cross = dy < CROSS_BAR / 2.0  # horizontal bars, thin
    bars = load | cross

    # height: rounded bar tops, the load bars a little taller than the cross bars
    h_load = np.where(load, np.sqrt(np.clip(1.0 - (dx / (LOAD_BAR / 2.0)) ** 2, 0, 1)), 0.0)
    h_cross = np.where(cross, 0.8 * np.sqrt(np.clip(1.0 - (dy / (CROSS_BAR / 2.0)) ** 2, 0, 1)), 0.0)
    height = blur(np.maximum(h_load, h_cross), 1)

    # tone: dark steel, cloudy grime, oil patches, a few rust freckles, scuffs and bright worn edges
    grime = periodic_noise(440, 4)
    fine = periodic_noise(48, 2)
    oil = periodic_noise(760, 3)
    edge = np.clip(1.0 - np.abs(np.where(load, dx / (LOAD_BAR / 2.0), 1.0) - 0.9) * 6.0, 0, 1) * load
    edge = np.maximum(edge, np.clip(1.0 - np.abs(np.where(cross, dy / (CROSS_BAR / 2.0), 1.0) - 0.9) * 6.0, 0, 1) * cross)
    scuff = blur(scratches(900, 160), 1)
    rust_mask = np.clip((periodic_noise(200, 3) - 0.88) * 4.0, 0, 1) * 0.6

    steel = np.array([84.0, 88.0, 92.0])
    base = np.zeros((SIZE, SIZE, 3))
    for c in range(3):
        base[..., c] = steel[c] * (0.72 + 0.5 * grime) * (0.92 + 0.16 * fine)
    base *= (1.0 - 0.55 * np.clip((oil - 0.55) * 3.0, 0, 1))[..., None]  # oil: darker
    base += (edge * 26.0)[..., None] + (scuff * 30.0 * bars)[..., None]
    rust = np.array([118.0, 66.0, 38.0])
    base = base * (1.0 - 0.6 * rust_mask[..., None]) + rust * (0.6 * rust_mask[..., None])
    holes = np.array([9.0, 10.0, 11.0])
    # holes go dark; the inside walls of the bars darken toward the hole
    slope = np.clip(height, 0, 1)
    colour = np.where(bars[..., None], base * (0.55 + 0.45 * slope[..., None]), holes)
    # a faint floor glimpse far below, so the holes are not a flat black
    colour = np.where(bars[..., None], colour, holes + (periodic_noise(400, 2) * 8.0)[..., None])
    Image.fromarray(np.clip(colour, 0, 255).astype(np.uint8)).save(os.path.join(OUT, "MetalGrating_BaseColor_Hero.png"))

    # normal (OpenGL, +Y up): the gradient of the height field, image rows run down so +Y is the opposite of the row gradient
    gx = (np.roll(height, -1, 1) - np.roll(height, 1, 1)) * 0.5
    gy = (np.roll(height, -1, 0) - np.roll(height, 1, 0)) * 0.5
    strength = 4.0
    n = np.stack([-gx * strength, gy * strength, np.ones_like(height)], axis=-1)
    n /= np.linalg.norm(n, axis=-1, keepdims=True)
    Image.fromarray(((n * 0.5 + 0.5) * 255).astype(np.uint8)).save(os.path.join(OUT, "MetalGrating_Normal_Hero.png"))

    # roughness: polished bar tops are shinier, grime and rust are rough, the holes are fully rough
    rough = 0.42 + 0.3 * grime + 0.18 * rust_mask + 0.2 * (1.0 - fine) * 0.3 - 0.12 * edge
    rough = np.where(bars, np.clip(rough, 0.2, 0.95), 1.0)
    Image.fromarray((np.clip(rough, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, "MetalGrating_Roughness_Hero.png"))

    # metallic: bare steel, less where the rust sits, none in the holes
    metal = np.where(bars, 0.88 - 0.55 * rust_mask - 0.12 * np.clip((oil - 0.55) * 3.0, 0, 1), 0.0)
    Image.fromarray((np.clip(metal, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, "MetalGrating_Metallic_Hero.png"))

    # ambient occlusion: the holes are deep and dark, and the foot of each bar is shaded
    ao = np.where(bars, 1.0, 0.12)
    ao = np.minimum(blur(ao, 5) * 1.15, 1.0)
    ao = np.where(bars, np.clip(0.7 + 0.3 * ao, 0, 1), np.clip(ao * 0.6, 0.05, 0.4))
    Image.fromarray((np.clip(ao, 0, 1) * 255).astype(np.uint8)).save(os.path.join(OUT, "MetalGrating_AO_Hero.png"))
    print(f"[Grating] wrote 5 maps to {OUT}")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    main()
