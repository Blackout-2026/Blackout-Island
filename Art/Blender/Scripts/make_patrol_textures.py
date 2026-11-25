"""Paints the kit atlas of the desert Security Patrol operator (step PS3). Owner: Core Developer.

    python Art/Blender/Scripts/make_patrol_textures.py            (needs numpy and Pillow, like make_commander_textures.py)

Writes into Assets/Textures/Models:
  T_CharPatrolKit_BaseColor.png / _MetalSmooth.png   1024  swatches + vest webbing, magazine pouch, radio, velcro patch, zip, rail teeth,
                                                           holster, boot laces and the balaclava face (eyes, brows, mask edge)
The layout (patrol_atlas.py) is shared with build_operator.py, and the face is painted with the same planar projection the head uses, so every
feature lands where the sculpted head has it. The cloth texture is made by make_patrol_cloth.py. Fixed random seeds: a re-run gives the same
pixels. Everything is our own pattern: no insignia, no text, no one's likeness. The Atlas and Canvas helpers are the Commander's.
"""

import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import patrol_atlas as A  # noqa: E402
from make_commander_textures import Atlas, Canvas, hex_rgb  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
rng = random.Random(17)
nrng = np.random.default_rng(17)

OLIVE = hex_rgb("#524a30")
OLIVE_LO = (62, 55, 36)
OLIVE_HI = (104, 94, 62)
MUSTARD = (128, 108, 58)
MUSTARD_HI = (150, 128, 76)
LEATHER = hex_rgb("#4e331d")
DARK = hex_rgb("#1c1d1b")
DARK_HI = (52, 54, 52)
SILVER = (170, 174, 180)
TAN = (178, 160, 128)
KHAKI = (120, 106, 76)


def stitch_rect(c, x0, y0, x1, y1, colour, step=5, dash=3, width=0.8):
    """A dashed stitch line round a rectangle."""
    for x in np.arange(x0, x1, step):
        c.line([(x, y0), (min(x + dash, x1), y0)], colour, width)
        c.line([(x, y1), (min(x + dash, x1), y1)], colour, width)
    for y in np.arange(y0, y1, step):
        c.line([(x0, y), (x0, min(y + dash, y1))], colour, width)
        c.line([(x1, y), (x1, min(y + dash, y1))], colour, width)


def wear(img, amount=0.07, seed=0):
    """Multiplies an RGBA image by soft noise, so painted cloth is not perfectly flat."""
    arr = np.asarray(img).astype(np.float32)
    g = np.random.default_rng(seed)
    noise = g.random(arr.shape[:2])
    noise = np.asarray(Image.fromarray((noise * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(1.2))).astype(np.float32) / 255.0
    arr[..., :3] *= (1.0 - amount + 2.0 * amount * noise)[..., None]
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def paint_webbing():
    w, h = A.KIT_DECALS["webbing"][2:]
    c = Canvas(w, h, OLIVE, 0.12)
    rows = 8
    pitch = h / rows
    for r in range(rows):
        y = r * pitch
        c.rect(0, y + pitch * 0.18, w, y + pitch * 0.78, MUSTARD, 0.0, 0.12)                # a webbing strap
        c.rect(0, y + pitch * 0.18, w, y + pitch * 0.26, MUSTARD_HI, 0.0, 0.12)             # its lit edge
        c.rect(0, y + pitch * 0.78, w, y + pitch * 0.94, OLIVE_LO, 0.0, 0.1)                # the dark gap under it
        for x in np.arange(6, w, 16):                                                       # bar-tack stitches that hold the strap
            c.rect(x, y + pitch * 0.22, x + 7, y + pitch * 0.30, OLIVE_LO, 0.0, 0.1)
            c.rect(x, y + pitch * 0.70, x + 7, y + pitch * 0.76, OLIVE_LO, 0.0, 0.1)
    c.rect(0, 0, 6, h, OLIVE_LO, 0.0, 0.1)
    c.rect(w - 6, 0, w, h, OLIVE_LO, 0.0, 0.1)
    img, ms = c.result(w, h)
    return wear(img, 0.08, 1), ms


def paint_pouch():
    w, h = A.KIT_DECALS["pouch"][2:]
    c = Canvas(w, h, (86, 62, 36), 0.25)
    c.rect(0, 0, w, h * 0.42, (70, 50, 30), 0.0, 0.25)                                     # the flap
    c.rect(0, h * 0.42 - 3, w, h * 0.42 + 3, (40, 28, 16), 0.0, 0.2)                       # its hem
    c.rect(w * 0.42, 0, w * 0.58, h * 0.56, (44, 36, 24), 0.0, 0.3)                        # the buckle strap
    c.rect(w * 0.38, h * 0.50, w * 0.62, h * 0.62, SILVER, 0.9, 0.6)                       # its buckle
    c.rect(w * 0.44, h * 0.53, w * 0.56, h * 0.59, (30, 30, 32), 0.0, 0.3)
    stitch_rect(c, 8, 8, w - 8, h - 8, (150, 124, 78))
    for x in (w * 0.18, w * 0.82):                                                          # the two mags show their baseplates' ribbing
        c.line([(x, h * 0.70), (x, h * 0.94)], (60, 44, 26), 3)
    img, ms = c.result(w, h)
    return wear(img, 0.07, 2), ms


def paint_radio():
    w, h = A.KIT_DECALS["radio"][2:]
    c = Canvas(w, h, (28, 29, 31), 0.3)
    c.rect(10, 10, w - 10, h * 0.40, (40, 42, 44), 0.0, 0.3)                               # the face plate
    for k in range(5):                                                                      # speaker grille
        for j in range(6):
            c.ellipse(22 + j * 22, 22 + k * 11, 3.2, 3.2, (12, 12, 13))
    c.rect(w * 0.18, h * 0.46, w * 0.82, h * 0.62, (70, 96, 70), 0.0, 0.6)                 # the display
    c.rect(w * 0.22, h * 0.49, w * 0.58, h * 0.53, (150, 190, 140), 0.0, 0.6)
    c.ellipse(w * 0.30, h * 0.78, 13, 13, (14, 14, 15), 0.2, 0.5)                          # two knobs
    c.ellipse(w * 0.70, h * 0.78, 13, 13, (14, 14, 15), 0.2, 0.5)
    c.line([(w * 0.30, h * 0.70), (w * 0.30, h * 0.78)], (190, 190, 190), 2.0, 0.6, 0.5)
    c.line([(w * 0.70, h * 0.70), (w * 0.74, h * 0.77)], (190, 190, 190), 2.0, 0.6, 0.5)
    stitch_rect(c, 5, 5, w - 5, h - 5, (60, 62, 64))
    img, ms = c.result(w, h)
    return img, ms


def paint_patch():
    w, h = A.KIT_DECALS["patch"][2:]
    c = Canvas(w, h, (44, 46, 44), 0.1)
    for y in range(0, h, 4):                                                                # the hook-and-loop texture
        c.line([(0, y), (w, y)], (52, 54, 52), 1.2)
    for x in range(0, w, 4):
        c.line([(x, 0), (x, h)], (38, 40, 38), 1.0)
    stitch_rect(c, 6, 6, w - 6, h - 6, (92, 90, 80), step=7, dash=4)
    for k in range(3):                                                                      # three plain bars, no insignia
        c.rect(26, 34 + k * 20, w - 26, 44 + k * 20, (66, 68, 64), 0.0, 0.1)
    img, ms = c.result(w, h)
    return wear(img, 0.1, 3), ms


def paint_zip():
    w, h = A.KIT_DECALS["zip"][2:]
    c = Canvas(w, h, (26, 27, 26), 0.2)
    c.rect(0, 0, 6, h, (36, 37, 36), 0.0, 0.2)
    c.rect(w - 6, 0, w, h, (36, 37, 36), 0.0, 0.2)
    for y in range(10, h - 40, 8):                                                          # the teeth
        c.rect(w * 0.30, y, w * 0.50, y + 4, SILVER, 0.9, 0.55)
        c.rect(w * 0.50, y + 4, w * 0.70, y + 8, SILVER, 0.9, 0.55)
    c.rect(w * 0.28, 4, w * 0.72, 30, (150, 154, 160), 0.9, 0.6)                           # the slider
    c.rect(w * 0.40, 26, w * 0.60, 54, (120, 124, 130), 0.9, 0.6)                          # its pull tab
    img, ms = c.result(w, h)
    return img, ms


def paint_ticks():
    w, h = A.KIT_DECALS["ticks"][2:]
    c = Canvas(w, h, (36, 38, 40), 0.5)
    c.rect(0, 0, w, h, (46, 48, 50), 0.85, 0.5)
    for x in np.arange(4, w, 12):                                                           # the cross slots of a picatinny rail
        c.rect(x, 4, x + 6, h - 4, (14, 15, 16), 0.5, 0.3)
    c.rect(0, 0, w, 4, (62, 64, 66), 0.9, 0.55)
    img, ms = c.result(w, h)
    return img, ms


def paint_holster():
    w, h = A.KIT_DECALS["holster"][2:]
    c = Canvas(w, h, (30, 31, 29), 0.2)
    c.rect(6, 6, w - 6, h * 0.30, (40, 41, 38), 0.0, 0.2)                                  # the retention flap
    c.rect(w * 0.40, h * 0.18, w * 0.60, h * 0.42, (22, 22, 21), 0.0, 0.2)                # its strap
    c.ellipse(w * 0.50, h * 0.42, 9, 9, SILVER, 0.9, 0.6)                                  # the snap
    stitch_rect(c, 9, 9, w - 9, h - 9, (84, 86, 80))
    c.line([(w * 0.5, h * 0.55), (w * 0.5, h * 0.92)], (24, 24, 23), 2)
    img, ms = c.result(w, h)
    return wear(img, 0.07, 4), ms


def paint_boot():
    w, h = A.KIT_DECALS["boot"][2:]
    c = Canvas(w, h, (122, 106, 76), 0.18)
    c.rect(w * 0.24, 0, w * 0.76, h, (138, 120, 88), 0.0, 0.18)                            # the tongue
    c.rect(0, 0, w, 34, (96, 82, 58), 0.0, 0.2)                                             # the padded collar
    for k in range(9):
        y = 54 + k * 28
        for x in (w * 0.18, w * 0.82):
            c.ellipse(x, y, 5.5, 5.5, (150, 154, 158), 0.9, 0.55)                          # eyelets
            c.ellipse(x, y, 2.4, 2.4, (24, 22, 18), 0.0, 0.3)
        if k < 8:
            c.line([(w * 0.18, y), (w * 0.82, y + 28)], (34, 32, 24), 4.2)                # criss-cross laces
            c.line([(w * 0.82, y), (w * 0.18, y + 28)], (46, 42, 30), 4.2)
        c.line([(w * 0.18, y), (w * 0.82, y)], (40, 38, 28), 3.6)
    stitch_rect(c, 6, 40, w - 6, h - 6, (82, 70, 48), step=7, dash=4)
    img, ms = c.result(w, h)
    return wear(img, 0.1, 5), ms


# ----- the balaclava face
def paint_face(atlas):
    rx, ry, rw, rh = A.FACE_RECT
    px, py = A.face_scale()
    cv = Canvas(rw, rh, hex_rgb("#6e6348"), 0.15)

    def P(x, y):
        a, b = A.face_px(x, y)
        return a - rx, b - ry

    # knit weave on the mask
    for y in range(0, rh, 3):
        cv.line([(0, y), (rw, y)], (96, 86, 62), 0.7)
    for x in range(0, rw, 3):
        cv.line([(x, 0), (x, rh)], (116, 106, 78), 0.5)
    # the opening: a skin band across the eyes with a darker hem
    cx0, cy0 = P(-A.SLIT_X, A.SLIT_Y1)
    cx1, cy1 = P(A.SLIT_X, A.SLIT_Y0)
    cv.ellipse((cx0 + cx1) / 2, (cy0 + cy1) / 2, (cx1 - cx0) / 2 + 3.0, (cy1 - cy0) / 2 + 3.0, (62, 56, 40), 0.0, 0.2)
    cv.ellipse((cx0 + cx1) / 2, (cy0 + cy1) / 2, (cx1 - cx0) / 2, (cy1 - cy0) / 2, (172, 124, 98), 0.0, 0.3)
    # eyes
    for u in (-1, 1):
        ex, ey = P(u * A.EYE_X, A.EYE_Y)
        ew = A.EYE_W * px
        eh = ew * 0.46
        cv.ellipse(ex, ey, ew * 1.5, eh * 1.9, (150, 104, 80), 0.0, 0.3)                    # the lid shadow
        cv.ellipse(ex, ey, ew, eh, (232, 226, 214), 0.0, 0.5)                               # the white
        cv.ellipse(ex, ey, ew * 0.46, ew * 0.46, (96, 122, 150), 0.0, 0.6)                  # the iris
        cv.ellipse(ex, ey, ew * 0.20, ew * 0.20, (14, 14, 16), 0.0, 0.7)                    # the pupil
        cv.ellipse(ex - ew * 0.13, ey - ew * 0.14, ew * 0.08, ew * 0.08, (250, 250, 250), 0.0, 0.9)   # a glint
        cv.line([(ex - ew * 1.05, ey - eh * 0.2), (ex, ey - eh * 1.05), (ex + ew * 1.05, ey - eh * 0.1)], (48, 30, 22), 1.6)   # the upper lid
        bx, by = P(u * (A.EYE_X + 0.002), A.BROW_Y)
        cv.line([(bx - ew * 1.2, by + 2.0), (bx, by - 1.5), (bx + ew * 1.2, by + 1.0)], (58, 40, 28), 3.2)    # the brow
    # nose bridge and the mask's folds over the mouth
    nx, ny = P(0.0, 1.700)
    cv.line([(nx - 9, ny - 24), (nx - 7, ny + 6), (nx + 7, ny + 6), (nx + 9, ny - 24)], (92, 82, 58), 1.6)
    mx, my = P(0.0, 1.648)
    cv.line([(mx - 22, my), (mx, my + 3), (mx + 22, my)], (84, 74, 52), 1.6)
    # the neck gaiter below the jaw is a little darker
    gx, gy = P(0.0, 1.602)
    cv.rect(0, gy, rw, rh, (88, 80, 58), 0.0, 0.12)
    img, ms = cv.result(rw, rh)
    return wear(img, 0.05, 6), ms


def main():
    atlas = Atlas(A.SIZE, True)
    atlas.base = Image.new("RGBA", (A.SIZE, A.SIZE), OLIVE + (255,))
    for name, (rect, hexv, metallic, smooth) in A.KIT_SWATCHES.items():
        atlas.fill(rect, hex_rgb(hexv), metallic, smooth)
    for name, painter in (("webbing", paint_webbing), ("pouch", paint_pouch), ("radio", paint_radio), ("patch", paint_patch), ("zip", paint_zip),
                          ("ticks", paint_ticks), ("holster", paint_holster), ("boot", paint_boot)):
        img, ms = painter()
        atlas.paste(A.KIT_DECALS[name], img, ms)
    img, ms = paint_face(atlas)
    atlas.paste(A.FACE_RECT, img, ms)
    os.makedirs(OUT, exist_ok=True)
    atlas.base.convert("RGBA").save(os.path.join(OUT, "T_CharPatrolKit_BaseColor.png"))
    atlas.ms.save(os.path.join(OUT, "T_CharPatrolKit_MetalSmooth.png"))
    print(f"[PatrolKit] {OUT}")


if __name__ == "__main__":
    main()
