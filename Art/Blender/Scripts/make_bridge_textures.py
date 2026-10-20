"""Draws the footbridge textures of step C12 with numpy and Pillow (no Blender, no photos). Owner: Core Developer.

    python Art/Blender/Scripts/make_bridge_textures.py

Writes into Assets/Textures/Models (the suffix _Normal makes a normal map). All three tile and are drawn for 1 metre per tile; the grain
of the wood runs along the image's x axis, and build_bridge.py maps each board with its long side along u.
    T_DockWood_BaseColor.png / _Normal.png      512 x 512: weathered grey-brown boards, long grain, dark cracks
    T_DockPaint_BaseColor.png / _Normal.png     512 x 512: old white paint on wood, worn through to grey boards, dirt streaks
    T_BridgeDeck_BaseColor.png / _Normal.png    512 x 512: rough pale-grey concrete, aggregate speckle, hairline cracks, stains
"""

import os

import numpy as np

from make_tree_textures import OUT, fbm, hexrgb, normal_from_height, save_rgb, smoothstep


def dock_wood(size=512):
    h = w = size
    # long grain: few cells along y, many along x would give vertical streaks, so stretch along x instead (many in y, few in x)
    grain = fbm(h, w, 46, 3, 4, 5)
    fine = fbm(h, w, 120, 6, 2, 17)
    cracks = smoothstep(0.58, 0.72, fbm(h, w, 14, 2, 4, 41))
    boards = 0.5 + 0.5 * np.sin(np.arange(h)[:, None] / h * 2 * np.pi * 4 + 0.0)  # four boards per tile
    seam = smoothstep(0.93, 1.0, boards)
    height = 0.45 * grain + 0.25 * fine - 0.35 * cracks - 0.25 * seam
    dark = hexrgb("#4a443a")
    mid = hexrgb("#7a7364")
    light = hexrgb("#a39b8a")
    t = np.clip(0.55 * grain + 0.45 * fine, 0, 1)[..., None]
    colour = dark[None, None, :] + (mid - dark)[None, None, :] * np.clip(t * 1.7, 0, 1) + (light - mid)[None, None, :] * np.clip(t - 0.5, 0, 1) * 1.6
    colour = colour * (1.0 - 0.55 * cracks[..., None]) * (1.0 - 0.45 * seam[..., None])
    tone = fbm(h, w, 3, 3, 3, 71)[..., None]
    colour = colour * (0.85 + 0.3 * tone)
    save_rgb(colour, "T_DockWood_BaseColor.png")
    save_rgb(normal_from_height(height, 4.0), "T_DockWood_Normal.png")


def dock_paint(size=512):
    h = w = size
    grain = fbm(h, w, 46, 3, 4, 9)
    fine = fbm(h, w, 120, 6, 2, 23)
    wear = 0.8 * smoothstep(0.60, 0.72, fbm(h, w, 6, 4, 4, 33))        # where the paint has gone and the grey wood shows
    chips = smoothstep(0.78, 0.86, fbm(h, w, 40, 40, 2, 55))     # small flakes
    gone = np.clip(wear + 0.6 * chips, 0, 1)
    paint = hexrgb("#e9e7df")
    dirt = hexrgb("#b9b5a6")
    wood = hexrgb("#7d7769")
    streak = smoothstep(0.35, 0.8, fbm(h, w, 2, 30, 3, 61))[..., None]   # rain streaks down the board
    painted = paint[None, None, :] * (1 - 0.30 * streak) + dirt[None, None, :] * 0.30 * streak
    painted = painted * (0.94 + 0.06 * grain[..., None])
    bare = wood[None, None, :] * (0.70 + 0.45 * grain[..., None])
    colour = painted * (1 - gone[..., None]) + bare * gone[..., None]
    height = 0.25 * grain + 0.15 * fine - 0.30 * gone
    save_rgb(colour, "T_DockPaint_BaseColor.png")
    save_rgb(normal_from_height(height, 3.0), "T_DockPaint_Normal.png")


def bridge_deck(size=512):
    h = w = size
    # rough concrete: fine aggregate speckle over soft blotches, a few hairline cracks and dark stains
    blotch = fbm(h, w, 5, 5, 4, 3)
    speck = fbm(h, w, 90, 90, 2, 19)
    coarse = smoothstep(0.62, 0.8, fbm(h, w, 36, 36, 2, 29))      # larger stones
    crack_field = fbm(h, w, 7, 7, 4, 47)
    cracks = smoothstep(0.008, 0.0, np.abs(crack_field - 0.5)) * 0.8 * smoothstep(0.55, 0.70, fbm(h, w, 3, 3, 2, 99))
    stain = smoothstep(0.55, 0.75, fbm(h, w, 3, 3, 3, 81))
    light = hexrgb("#a9a7a1")
    mid = hexrgb("#8d8b86")
    dark = hexrgb("#615f5b")
    t = np.clip(0.5 * blotch + 0.5 * speck, 0, 1)[..., None]
    colour = mid[None, None, :] + (light - mid)[None, None, :] * np.clip((t - 0.5) * 3, 0, 1) + (dark - mid)[None, None, :] * np.clip((0.5 - t) * 3, 0, 1)
    colour = colour * (1 - 0.18 * stain[..., None]) * (1 - 0.6 * cracks[..., None])
    colour = colour + 0.04 * coarse[..., None]
    height = 0.35 * speck + 0.25 * coarse + 0.15 * blotch - 0.9 * cracks
    save_rgb(colour, "T_BridgeDeck_BaseColor.png")
    save_rgb(normal_from_height(height, 3.5), "T_BridgeDeck_Normal.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    dock_wood()
    dock_paint()
    bridge_deck()
