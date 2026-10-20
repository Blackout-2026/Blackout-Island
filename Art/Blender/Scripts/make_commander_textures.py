"""Paints the textures of the realistic Commander (step C10b). Owner: Core Developer.

    python Art/Blender/Scripts/make_commander_textures.py            (needs numpy and Pillow, like make_skin_texture.py)

Writes into Assets/Textures/Models:
  T_CharCommanderUniform_BaseColor.png / _MetalSmooth.png   1024  swatches + shoulder board, ribbon rack, name tag, pin, cap visor, device
  T_CharCommanderSkin_BaseColor.png                         1024  the face (eyes, brows, lips, freckles) + freckled skin for arms and hands
  T_CharCommanderHair_BaseColor.png                         512   hair strands, dark roots to sun-lightened tips
The layout (commander_atlas.py) is shared with build_commander.py, and the face is painted with the same planar projection the head uses,
so every feature lands where the sculpted head has it. Fixed random seeds: a re-run gives the same pixels. Everything is our own pattern,
generic insignia, no one's likeness.
"""

import math
import os
import random
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import commander_atlas as A  # noqa: E402

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")
rng = random.Random(5)
nrng = np.random.default_rng(5)
SS = 4  # supersampling of the painted decals


def hex_rgb(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) for i in (0, 2, 4))


def font(size, bold=True):
    for name in ("arialbd.ttf" if bold else "arial.ttf", "arial.ttf"):
        path = os.path.join(os.environ.get("WINDIR", "C:/Windows"), "Fonts", name)
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


class Atlas:
    """A base-colour image plus a metal (R) / smoothness (A) image."""

    def __init__(self, size, with_metal):
        self.size = size
        self.base = Image.new("RGBA", (size, size), (233, 233, 228, 255))
        self.ms = Image.new("RGBA", (size, size), (0, 0, 0, 46)) if with_metal else None

    def paste(self, rect, base_img, ms_img=None):
        x, y, w, h = rect
        self.base.paste(base_img.resize((w, h), Image.LANCZOS), (x, y))
        if self.ms is not None and ms_img is not None:
            self.ms.paste(ms_img.resize((w, h), Image.LANCZOS), (x, y))

    def fill(self, rect, colour, metallic, smooth):
        x, y, w, h = rect
        ImageDraw.Draw(self.base).rectangle([x, y, x + w - 1, y + h - 1], fill=colour + (255,))
        if self.ms is not None:
            ImageDraw.Draw(self.ms).rectangle([x, y, x + w - 1, y + h - 1], fill=(int(metallic * 255), 0, 0, int(smooth * 255)))


class Canvas:
    """A decal drawn at SS x resolution: shapes go onto the colour layer and, with gold(), also onto the metal layer."""

    def __init__(self, w, h, colour, smooth=0.2):
        self.w, self.h = w * SS, h * SS
        self.img = Image.new("RGBA", (self.w, self.h), colour + (255,))
        self.ms = Image.new("RGBA", (self.w, self.h), (0, 0, 0, int(smooth * 255)))
        self.d = ImageDraw.Draw(self.img)
        self.dm = ImageDraw.Draw(self.ms)

    def s(self, v):
        return v * SS

    def poly(self, pts, colour, metallic=0.0, smooth=0.2):
        pts = [(self.s(x), self.s(y)) for x, y in pts]
        self.d.polygon(pts, fill=colour + (255,))
        self.dm.polygon(pts, fill=(int(metallic * 255), 0, 0, int(smooth * 255)))

    def rect(self, x0, y0, x1, y1, colour, metallic=0.0, smooth=0.2):
        self.poly([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], colour, metallic, smooth)

    def ellipse(self, cx, cy, rx, ry, colour, metallic=0.0, smooth=0.2):
        box = [self.s(cx - rx), self.s(cy - ry), self.s(cx + rx), self.s(cy + ry)]
        self.d.ellipse(box, fill=colour + (255,))
        self.dm.ellipse(box, fill=(int(metallic * 255), 0, 0, int(smooth * 255)))

    def line(self, pts, colour, width, metallic=0.0, smooth=0.2):
        pts = [(self.s(x), self.s(y)) for x, y in pts]
        self.d.line(pts, fill=colour + (255,), width=max(1, int(self.s(width))), joint="curve")
        self.dm.line(pts, fill=(int(metallic * 255), 0, 0, int(smooth * 255)), width=max(1, int(self.s(width))), joint="curve")

    def text(self, xy, txt, fnt, colour, metallic=0.0, smooth=0.2):
        self.d.text((self.s(xy[0]), self.s(xy[1])), txt, font=fnt, fill=colour + (255,), anchor="mm")
        self.dm.text((self.s(xy[0]), self.s(xy[1])), txt, font=fnt, fill=(int(metallic * 255), 0, 0, int(smooth * 255)), anchor="mm")

    def result(self, w, h):
        return self.img.resize((w, h), Image.LANCZOS), self.ms.resize((w, h), Image.LANCZOS)


GOLD = (200, 162, 74)
GOLD_HI = (232, 200, 112)
GOLD_LO = (150, 116, 44)
WHITE = hex_rgb("#e9e9e4")
BLACK = (21, 22, 26)


# ----------------------------------------------------------------------------- the uniform atlas
def star_points(cx, cy, r_out, r_in, rot=-90.0):
    pts = []
    for i in range(10):
        r = r_out if i % 2 == 0 else r_in
        a = math.radians(rot + 36.0 * i)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def paint_board(atlas):
    x, y, w, h = A.UNIFORM_DECALS["board"]
    c = Canvas(w, h, WHITE, 0.2)
    c.rect(0, 0, w, 4, (205, 205, 200))
    c.rect(0, h - 4, w, h, (205, 205, 200))
    c.rect(0, 0, 4, h, (205, 205, 200))
    c.rect(w - 4, 0, w, h, (205, 205, 200))
    # the star toward the neck, three stripes toward the shoulder tip
    c.poly(star_points(56, h / 2, 30, 12), GOLD, 0.85, 0.6)
    c.poly(star_points(56, h / 2, 24, 9.5), GOLD_HI, 0.85, 0.65)
    for k in range(3):
        x0 = 118 + 38 * k
        c.rect(x0, 10, x0 + 26, h - 10, GOLD, 0.85, 0.55)
        for yy in range(12, h - 10, 6):                                     # the woven lace
            c.line([(x0, yy), (x0 + 26, yy + 3)], GOLD_LO, 1.0, 0.8, 0.4)
        c.line([(x0 + 3, 10), (x0 + 3, h - 10)], GOLD_HI, 1.4, 0.9, 0.7)
    base, ms = c.result(w, h)
    atlas.paste((x, y, w, h), base, ms)


RIBBONS = [
    [("#b3202a", 0.50), ("#f0f0f0", 0.05), ("#1d3f8a", 0.45)],
    [("#1d3f8a", 0.18), ("#f0f0f0", 0.06), ("#b3202a", 0.52), ("#f0f0f0", 0.06), ("#1d3f8a", 0.18)],
    [("#2d6a3a", 0.30), ("#f2d34a", 0.06), ("#2d6a3a", 0.28), ("#f2d34a", 0.06), ("#2d6a3a", 0.30)],
    [("#c9a227", 0.28), ("#1d3f8a", 0.44), ("#c9a227", 0.28)],
    [("#b3202a", 0.16), ("#f0f0f0", 0.10), ("#b3202a", 0.16), ("#f0f0f0", 0.10), ("#1d3f8a", 0.20), ("#f0f0f0", 0.10), ("#b3202a", 0.18)],
    [("#1b2d5c", 0.5), ("#c9a227", 0.08), ("#b3202a", 0.42)],
    [("#6b2a73", 0.40), ("#f0f0f0", 0.06), ("#6b2a73", 0.14), ("#f0f0f0", 0.06), ("#6b2a73", 0.34)],
    [("#e8e8e8", 0.14), ("#1d3f8a", 0.36), ("#e8e8e8", 0.14), ("#1d3f8a", 0.36)],
    [("#2b2b2e", 0.20), ("#c9a227", 0.12), ("#b3202a", 0.36), ("#c9a227", 0.12), ("#2b2b2e", 0.20)],
]


def paint_ribbons(atlas):
    x, y, w, h = A.UNIFORM_DECALS["ribbons"]
    c = Canvas(w, h, (58, 60, 64), 0.3)
    cw, ch = w / 3.0, h / 3.0
    for r in range(3):
        for k in range(3):
            seq = RIBBONS[(r * 3 + k) % len(RIBBONS)]
            x0, y0 = k * cw + 2.0, r * ch + 2.0
            xx = x0
            for hexc, frac in seq:
                width = (cw - 4.0) * frac
                c.rect(xx, y0, xx + width, y0 + ch - 4.0, hex_rgb(hexc), 0.0, 0.3)
                xx += width
            for i in range(int(cw)):                                         # the rib of the silk
                if i % 3 == 0:
                    c.line([(x0 + i, y0), (x0 + i, y0 + ch - 4.0)], (0, 0, 0), 0.5, 0.0, 0.3)
            c.rect(x0, y0 + ch - 7.0, x0 + cw - 4.0, y0 + ch - 4.0, (0, 0, 0), 0.0, 0.3)   # shaded lower edge
    base, ms = c.result(w, h)
    arr = np.array(base).astype(np.float32)
    shade = np.ones((h, w, 1), np.float32)
    for r in range(3):
        for k in range(3):
            gy0, gy1 = int(r * h / 3), int((r + 1) * h / 3)
            gx0, gx1 = int(k * w / 3), int((k + 1) * w / 3)
            ramp = np.linspace(1.08, 0.86, gy1 - gy0)[:, None, None]
            shade[gy0:gy1, gx0:gx1] = ramp
    arr[..., :3] *= shade
    base = Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8))
    atlas.paste((x, y, w, h), base, ms)


def paint_nametag(atlas):
    x, y, w, h = A.UNIFORM_DECALS["nametag"]
    c = Canvas(w, h, (16, 17, 20), 0.5)
    c.rect(2, 2, w - 2, 5, GOLD, 0.85, 0.6)
    c.rect(2, h - 5, w - 2, h - 2, GOLD, 0.85, 0.6)
    c.rect(2, 2, 5, h - 2, GOLD, 0.85, 0.6)
    c.rect(w - 5, 2, w - 2, h - 2, GOLD, 0.85, 0.6)
    c.text((w / 2, h / 2 + 1), "COMMANDER", font(int(h * 0.50 * SS / SS) * 1), GOLD_HI, 0.85, 0.6)
    base, ms = c.result(w, h)
    atlas.paste((x, y, w, h), base, ms)


def paint_pin(atlas):
    x, y, w, h = A.UNIFORM_DECALS["pin"]
    c = Canvas(w, h, WHITE, 0.2)
    cx, cy = w / 2, h / 2
    for side in (-1, 1):                                                       # the wings: five tapered feathers each
        for k in range(5):
            top = cy - 20 + k * 3.2
            tip = cx + side * (44 - k * 2.0)
            c.poly([(cx + side * 8, cy - 6 + k * 3.0), (tip, top + 2), (tip - side * 3, top + 8), (cx + side * 8, cy + 2 + k * 3.0)], GOLD, 0.85, 0.6)
            c.line([(cx + side * 8, cy - 2 + k * 3.0), (tip - side * 2, top + 5)], GOLD_LO, 1.0, 0.8, 0.4)
    c.ellipse(cx, cy + 2, 12, 17, GOLD_HI, 0.9, 0.7)
    c.ellipse(cx, cy + 2, 9, 14, GOLD, 0.9, 0.6)
    c.line([(cx, cy - 8), (cx, cy + 16)], GOLD_LO, 2.0, 0.8, 0.4)
    c.line([(cx - 6, cy - 1), (cx + 6, cy - 1)], GOLD_LO, 2.0, 0.8, 0.4)
    base, ms = c.result(w, h)
    atlas.paste((x, y, w, h), base, ms)


def paint_visor(atlas):
    x, y, w, h = A.UNIFORM_DECALS["visor"]
    c = Canvas(w, h, (20, 21, 26), 0.62)
    # the outer edge (the bottom of the picture) carries two rows of gold oak leaves along a gold cord
    for row, (base_y, size) in enumerate(((h - 26, 17), (h - 54, 13))):
        n = 15 if row == 0 else 13
        for i in range(n):
            cx = 14 + i * (w - 28) / (n - 1)
            ang = math.radians(-20 + 40 * (i / (n - 1) - 0.5) * 2 * (1 if i % 2 == 0 else -1) * 0.5)
            pts = []
            for t in range(13):
                tt = t / 12.0
                lx = (tt - 0.5) * size * 1.6
                ly = math.sin(tt * math.pi) * size * 0.45
                pts.append((lx, ly))
            top = [(cx + px * math.cos(ang) - py * math.sin(ang), base_y + px * math.sin(ang) + py * math.cos(ang)) for px, py in pts]
            bot = [(cx + px * math.cos(ang) + py * math.sin(ang), base_y + px * math.sin(ang) - py * math.cos(ang)) for px, py in pts]
            c.poly(top + bot[::-1], GOLD if i % 2 == 0 else GOLD_HI, 0.85, 0.6)
            c.line([(cx - size * 0.8 * math.cos(ang), base_y - size * 0.8 * math.sin(ang)), (cx + size * 0.8 * math.cos(ang), base_y + size * 0.8 * math.sin(ang))],
                   GOLD_LO, 1.0, 0.8, 0.4)
    c.line([(6, h - 6), (w - 6, h - 6)], GOLD, 3.0, 0.85, 0.6)
    base, ms = c.result(w, h)
    atlas.paste((x, y, w, h), base, ms)


def paint_device(atlas):
    x, y, w, h = A.UNIFORM_DECALS["device"]
    c = Canvas(w, h, BLACK, 0.62)
    cx, cy = w / 2, h / 2
    c.ellipse(cx, cy, 56, 56, GOLD_LO, 0.85, 0.5)
    c.ellipse(cx, cy, 52, 52, GOLD, 0.9, 0.65)
    c.ellipse(cx, cy, 40, 40, GOLD_LO, 0.8, 0.45)
    # an anchor between two wings
    c.line([(cx, cy - 30), (cx, cy + 28)], GOLD_HI, 6.0, 0.9, 0.7)
    c.line([(cx - 17, cy - 16), (cx + 17, cy - 16)], GOLD_HI, 5.0, 0.9, 0.7)
    c.ellipse(cx, cy - 32, 6, 6, GOLD_HI, 0.9, 0.7)
    c.line([(cx - 28, cy + 8), (cx - 14, cy + 26), (cx, cy + 30), (cx + 14, cy + 26), (cx + 28, cy + 8)], GOLD_HI, 5.0, 0.9, 0.7)
    c.poly([(cx - 34, cy + 4), (cx - 24, cy + 14), (cx - 22, cy + 6)], GOLD_HI, 0.9, 0.7)
    c.poly([(cx + 34, cy + 4), (cx + 24, cy + 14), (cx + 22, cy + 6)], GOLD_HI, 0.9, 0.7)
    base, ms = c.result(w, h)
    atlas.paste((x, y, w, h), base, ms)


def make_uniform():
    atlas = Atlas(A.SIZE["uniform"], True)
    atlas.base.paste(WHITE + (255,), [0, 0, atlas.size, atlas.size])
    for name, (rect, hexc, metallic, smooth) in A.UNIFORM_SWATCHES.items():
        atlas.fill(rect, hex_rgb(hexc), metallic, smooth)
    # a faint weave on the white and shade swatches
    for name in ("white", "shade"):
        x, y, w, h = A.UNIFORM_SWATCHES[name][0]
        arr = np.array(atlas.base.crop((x, y, x + w, y + h))).astype(np.float32)
        arr[..., :3] *= (1.0 + nrng.normal(0, 0.012, (h, w, 1)))
        atlas.base.paste(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)), (x, y))
    paint_board(atlas)
    paint_ribbons(atlas)
    paint_nametag(atlas)
    paint_pin(atlas)
    paint_visor(atlas)
    paint_device(atlas)
    atlas.base.convert("RGB").save(os.path.join(OUT, "T_CharCommanderUniform_BaseColor.png"))
    atlas.ms.save(os.path.join(OUT, "T_CharCommanderUniform_MetalSmooth.png"))


# ----------------------------------------------------------------------------- skin and face
def skin_noise(w, h, amount=0.025):
    n = nrng.normal(0, 1, (h, w))
    big = np.array(Image.fromarray(((n - n.min()) / (n.max() - n.min()) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32) / 255.0
    low = np.array(Image.fromarray((nrng.random((max(h // 24, 2), max(w // 24, 2))) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)).astype(np.float32) / 255.0
    return ((low - 0.5) * 2.0 * amount)[..., None] + ((big - 0.5) * 0.02)[..., None]


def make_skin_tile(w, h, freckles=True):
    base = np.zeros((h, w, 3), np.float32) + np.array(hex_rgb(A.SKIN_HEX), np.float32)
    base *= 1.0 + skin_noise(w, h, 0.012)
    img = Image.fromarray(np.clip(base, 0, 255).astype(np.uint8)).convert("RGBA")
    layer = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    for _ in range(int(w * h / 330) if freckles else 0):                    # freckles
        x, y = rng.uniform(0, w), rng.uniform(0, h)
        r = rng.uniform(1.0, 2.8)
        a = rng.randint(30, 75)
        d.ellipse([x - r, y - r, x + r, y + r], fill=(176, 116, 88, a))
    layer = layer.filter(ImageFilter.GaussianBlur(0.9))
    return Image.alpha_composite(img, layer)


def blob(canvas, cx, cy, rx, ry, colour, alpha, blur, S):
    """A soft elliptical spot of colour at head-projection pixel (cx, cy) (already scaled by S)."""
    layer = Image.new("RGBA", canvas.size, (0, 0, 0, 0))
    ImageDraw.Draw(layer).ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=colour + (int(alpha * 255),))
    layer = layer.filter(ImageFilter.GaussianBlur(blur * S))
    return Image.alpha_composite(canvas, layer)


def paint_head(skin_atlas):
    S = 3
    rx, ry, rw, rh = A.HEAD_RECT
    sx, sy = A.head_scale()
    sx, sy = sx * A.HS, sy * A.HS      # design metres -> pixels of the scaled head
    W, H = rw * S, rh * S

    def P(x, y):
        px, py = A.head_px(A.hx(x), A.hy(y))
        return ((px - rx) * S, (py - ry) * S)

    def R(dx, dy):
        return dx * sx * S, dy * sy * S

    img = make_skin_tile(rw, rh, False).resize((W, H), Image.BICUBIC)
    skin = hex_rgb(A.SKIN_HEX)

    # broad shading: lighter forehead and nose bridge, warmer cheeks and nose tip, shadow below the lower lip and at the sides
    def spot(x, y, wx, wy, colour, alpha, blur=0.004):
        nonlocal img
        cx, cy = P(x, y)
        ax, ay = R(wx, wy)
        img = blob(img, cx, cy, ax, ay, colour, alpha, blur * sx, S)

    for u in (-1, 1):
        spot(u * 0.047, 1.690, 0.024, 0.018, (226, 140, 128), 0.34, 0.010)      # blush
        spot(u * 0.0335, 1.7285, 0.032, 0.014, (176, 120, 100), 0.16, 0.006)     # the eye sockets
        spot(u * 0.0335, 1.7195, 0.024, 0.006, (190, 130, 112), 0.20, 0.004)     # under the eyes
        spot(u * 0.068, 1.70, 0.012, 0.060, (190, 138, 112), 0.35, 0.010)        # the sides of the face
        spot(u * 0.0125, 1.671, 0.0075, 0.0058, (60, 30, 28), 0.0, 0.002)
    spot(0.0, 1.760, 0.022, 0.020, (240, 208, 184), 0.22, 0.008)               # forehead
    spot(0.0, 1.700, 0.006, 0.030, (240, 208, 184), 0.18, 0.004)               # nose bridge
    spot(0.0, 1.683, 0.012, 0.009, (226, 150, 138), 0.14, 0.004)               # nose tip
    spot(0.0, 1.620, 0.030, 0.011, (170, 118, 98), 0.22, 0.007)               # under the chin
    spot(0.0, 1.6575, 0.020, 0.0035, (170, 112, 100), 0.15, 0.002)              # under the nose
    d = ImageDraw.Draw(img)

    # freckles over the nose and cheeks
    fr = Image.new("RGBA", img.size, (0, 0, 0, 0))
    fd = ImageDraw.Draw(fr)
    for _ in range(95):
        x = rng.gauss(0.0, 0.032)
        y = rng.gauss(1.693, 0.012)
        if abs(x) > 0.062 or abs(x) < 0.013:
            continue
        cx, cy = P(x, y)
        r = rng.uniform(0.0006, 0.0013) * sx * S
        fd.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(165, 105, 80, rng.randint(28, 70)))
    img = Image.alpha_composite(img, fr.filter(ImageFilter.GaussianBlur(0.8 * S)))
    d = ImageDraw.Draw(img)

    # ---- the eyes
    ex_in, ex_out = A.EYE_X - A.EYE_W, A.EYE_X + A.EYE_W
    for u in (-1, 1):
        def curve(kind):
            pts = []
            for i in range(25):
                t = i / 24.0
                x = ex_in + t * (ex_out - ex_in)
                yc = A.EYE_Y + 0.0013 * t - 0.0005 * (1 - t)
                if kind == "up":
                    yy = yc + 0.0078 * math.sin(math.pi * t ** 0.85) ** 0.9 * (1.0 - 0.12 * t)
                else:
                    yy = yc - 0.0044 * math.sin(math.pi * t) ** 1.1
                pts.append(P(u * x, yy))
            return pts

        up, low = curve("up"), curve("low")
        mask = Image.new("L", img.size, 0)
        ImageDraw.Draw(mask).polygon(up + low[::-1], fill=255)
        eye = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ed = ImageDraw.Draw(eye)
        ed.polygon(up + low[::-1], fill=(238, 232, 228, 255))
        ix, iy = P(u * (A.EYE_X + 0.0008), A.EYE_Y + 0.0004)
        ir = 0.0072 * sx * S
        for k in range(14, 0, -1):                                             # the iris: dark limbus, green-brown ring, lighter centre
            f = k / 14.0
            col = (int(52 + 76 * (1 - f)), int(70 + 84 * (1 - f)), int(50 + 52 * (1 - f)))
            ed.ellipse([ix - ir * f, iy - ir * f, ix + ir * f, iy + ir * f], fill=col + (255,))
        pr = 0.0027 * sx * S
        ed.ellipse([ix - pr, iy - pr, ix + pr, iy + pr], fill=(10, 10, 12, 255))
        ed.ellipse([ix - ir, iy - ir, ix + ir, iy + ir], outline=(34, 40, 34, 255), width=max(1, int(S * 0.9)))
        hr = 0.0016 * sx * S
        ed.ellipse([ix - ir * 0.45 - hr, iy - ir * 0.45 - hr, ix - ir * 0.45 + hr, iy - ir * 0.45 + hr], fill=(255, 255, 255, 235))
        shadow = Image.new("RGBA", img.size, (0, 0, 0, 0))
        sd = ImageDraw.Draw(shadow)
        sd.polygon(up + [(x, y + 0.0030 * sy * S) for x, y in up[::-1]], fill=(40, 24, 20, 120))
        eye = Image.alpha_composite(eye, shadow.filter(ImageFilter.GaussianBlur(1.6 * S)))
        eye.putalpha(Image.composite(eye.getchannel("A"), Image.new("L", img.size, 0), mask))
        img = Image.alpha_composite(img, eye)
        d = ImageDraw.Draw(img)
        lw = max(2, int(0.00135 * sy * S))
        d.line(up, fill=(38, 26, 22, 255), width=lw, joint="curve")                                # the upper lid line
        d.line(low, fill=(150, 98, 86, 190), width=max(1, int(lw * 0.55)), joint="curve")          # the lower lid line
        wing = [up[-1], P(u * (ex_out + 0.0025), A.EYE_Y + 0.0035)]
        d.line(wing, fill=(38, 26, 22, 235), width=max(1, int(lw * 0.8)))
        crease = [P(u * (ex_in + (ex_out - ex_in) * (i / 16.0)), A.EYE_Y + 0.0090 + 0.0010 * math.sin(math.pi * i / 16.0)) for i in range(17)]
        d.line(crease, fill=(170, 112, 94, 150), width=max(1, int(lw * 0.5)), joint="curve")        # the lid crease
        for i in range(2, 24):                                                                       # lashes
            t = i / 24.0
            bx, by = up[i]
            ln = (0.0028 + 0.0018 * t) * sy * S
            d.line([(bx, by), (bx + (0.0006 + 0.0020 * t) * sx * S * u, by - ln)], fill=(46, 32, 26, 120), width=max(1, int(lw * 0.30)))

    # ---- the brows (many short strokes along the flow, dark blond)
    brow = Image.new("RGBA", img.size, (0, 0, 0, 0))
    bd = ImageDraw.Draw(brow)
    for u in (-1, 1):
        for _ in range(170):
            t = rng.random()
            x = 0.0150 + t * 0.0445
            arch = 0.0090 * math.sin(math.pi * min(1.0, t * 1.12)) ** 1.2 - 0.0040 * t
            half = (0.0050 * (1.0 - t) ** 0.6 + 0.0018) * (1.0 if t > 0.1 else 1.4)
            yy = A.BROW_Y + arch + rng.uniform(-half, half) * 0.9
            x0, y0 = P(u * x, yy)
            dxs = 0.0034 * sx * S * u
            dys = (0.0016 * math.cos(math.pi * t) - 0.0004) * sy * S * -1.0
            col = (int(96 + rng.uniform(-14, 10)), int(70 + rng.uniform(-10, 8)), int(48 + rng.uniform(-8, 6)))
            bd.line([(x0, y0), (x0 + dxs, y0 + dys)], fill=col + (rng.randint(120, 200),), width=max(1, int(0.00055 * sx * S)))
    img = Image.alpha_composite(img, brow.filter(ImageFilter.GaussianBlur(0.35 * S)))
    d = ImageDraw.Draw(img)

    # ---- the nose: nostrils and the soft wings
    for u in (-1, 1):
        nx, ny = P(u * 0.0105, 1.6672)
        a, b = 0.0026 * sx * S, 0.0011 * sy * S
        nl = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(nl).ellipse([nx - a, ny - b, nx + a, ny + b], fill=(86, 46, 42, 150))
        img = Image.alpha_composite(img, nl.filter(ImageFilter.GaussianBlur(0.9 * S)))
        wx, wy = P(u * 0.0185, 1.6700)
        wl = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(wl).arc([wx - 0.004 * sx * S, wy - 0.006 * sy * S, wx + 0.004 * sx * S, wy + 0.006 * sy * S],
                               start=(250 if u > 0 else 290), end=(110 if u > 0 else 70), fill=(160, 100, 90, 130), width=max(1, int(S * 1.2)))
        img = Image.alpha_composite(img, wl.filter(ImageFilter.GaussianBlur(0.9 * S)))
    d = ImageDraw.Draw(img)

    # ---- the lips: a Cupid's bow on top, a fuller lower lip, a darker mouth line
    def lip_curve(y_edge, height, bow, n=33):
        up_pts, lo_pts = [], []
        for i in range(n):
            t = i / (n - 1)
            x = (t - 0.5) * 2.0 * 0.0225
            env = max(0.0, 1.0 - abs((t - 0.5) * 2.0) ** 2.2)
            lo_pts.append(P(x, y_edge))
            cupid = bow * (1.0 - min(1.0, abs(x) / 0.0050)) if abs(x) < 0.0050 else 0.0
            up_pts.append(P(x, y_edge + height * env - 0.0016 * (1 - env) - cupid * 0.0 + (-0.0011 if abs(x) < 0.0014 else 0.0)))
        return up_pts, lo_pts

    mouth_y = A.MOUTH_Y
    up_top, up_bot = lip_curve(mouth_y, 0.0102, 0.0010)
    lo_bot, lo_top = lip_curve(mouth_y, -0.0125, 0.0)
    lips = Image.new("RGBA", img.size, (0, 0, 0, 0))
    ld = ImageDraw.Draw(lips)
    ld.polygon(up_top + up_bot[::-1], fill=(184, 98, 104, 255))
    ld.polygon(lo_bot + lo_top[::-1], fill=(202, 116, 120, 255))
    hx, hy = P(0.0, mouth_y - 0.0058)
    ld.ellipse([hx - 0.0085 * sx * S, hy - 0.0016 * sy * S, hx + 0.0085 * sx * S, hy + 0.0016 * sy * S], fill=(230, 160, 158, 255))
    lips = lips.filter(ImageFilter.GaussianBlur(0.45 * S))
    img = Image.alpha_composite(img, lips)
    d = ImageDraw.Draw(img)
    mouth = [P((i / 24.0 - 0.5) * 2.0 * 0.0235, mouth_y - 0.0003 * math.sin(math.pi * i / 24.0) + (0.0007 if abs(i / 24.0 - 0.5) > 0.38 else 0.0)) for i in range(25)]
    d.line(mouth, fill=(116, 52, 58, 255), width=max(2, int(0.0012 * sy * S)), joint="curve")
    for u in (-1, 1):                                                           # the corners
        cx, cy = P(u * 0.0242, mouth_y + 0.0004)
        spot_l = Image.new("RGBA", img.size, (0, 0, 0, 0))
        ImageDraw.Draw(spot_l).ellipse([cx - 0.0016 * sx * S, cy - 0.0016 * sy * S, cx + 0.0016 * sx * S, cy + 0.0016 * sy * S], fill=(120, 60, 62, 150))
        img = Image.alpha_composite(img, spot_l.filter(ImageFilter.GaussianBlur(0.7 * S)))
    d = ImageDraw.Draw(img)

    out = img.resize((rw, rh), Image.LANCZOS)
    skin_atlas.paste(out, (rx, ry))


def make_skin():
    atlas = Image.new("RGBA", (A.SIZE["skin"], A.SIZE["skin"]), hex_rgb(A.SKIN_HEX) + (255,))
    # the freckled tile (arms, hands, the chest in the V of the collar)
    x, y, w, h = A.SKIN_TILE
    atlas.paste(make_skin_tile(w, h), (x, y))
    sx, sy, sw, sh = A.SKIN_SWATCH
    ImageDraw.Draw(atlas).rectangle([sx, sy, sx + sw - 1, sy + sh - 1], fill=hex_rgb(A.SKIN_HEX) + (255,))
    # the head: paint_head pastes into the atlas through a tiny adapter
    class Adapter:
        def paste(self, img, pos):
            atlas.paste(img, pos)
    paint_head(Adapter())
    atlas.convert("RGB").save(os.path.join(OUT, "T_CharCommanderSkin_BaseColor.png"))


# ----------------------------------------------------------------------------- hair
def hair_tone(t):
    root_c = np.array(hex_rgb("#5a4126"), np.float32)
    mid_c = np.array(hex_rgb("#8c683b"), np.float32)
    hi_c = np.array(hex_rgb("#c2a068"), np.float32)
    return mid_c * (1 - t) + hi_c * t, root_c


def make_hair():
    size = A.SIZE["hair"]
    out = Image.new("RGBA", (size, size), (0, 0, 0, 255))
    # the cap: opaque strands, dark roots at the top (the crown) to lighter tips
    cx, cy, cw, ch = A.HAIR_CAP_RECT
    arr = np.zeros((ch, cw, 3), np.float32)
    tone = np.zeros(cw, np.float32)
    k = 0
    while k < cw:
        wdt = rng.randint(2, 9)
        tone[k:k + wdt] = rng.random()
        k += wdt
    fine = nrng.normal(0, 1, cw).astype(np.float32)
    v = np.linspace(0.0, 1.0, ch)[:, None]
    for i in range(cw):
        mid, root = hair_tone(tone[i])
        arr[:, i, :] = (root * (1 - v) + mid * v) * (1.0 + 0.05 * fine[i])
    out.paste(Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8)).convert("RGBA"), (cx, cy))
    # the lock card: a leaf-shaped clump made of ~30 overlapping strands that are longest in the middle and stop short toward the edges, so the
    # silhouette is a tapered, slightly ragged lock instead of separate thin straws
    x0, y0, w, h = A.HAIR_CARD_RECT
    S = 3
    card = Image.new("RGBA", (w * S, h * S), (140, 104, 59, 0))        # transparent, but the colour behind is hair-coloured so mip levels do not darken
    d = ImageDraw.Draw(card)
    n = 30
    for i in range(n):
        fx = (i + 0.5) / n
        centre = 1.0 - abs(fx - 0.5) * 2.0                                # 1 in the middle, 0 at the edges
        length = h * (0.30 + 0.70 * centre ** 0.8) * rng.uniform(0.86, 1.0)
        sw = w / n * 1.7
        xs = fx * w
        taper = (0.5 - fx) * 0.35 * w                                      # the strands lean toward the middle at the tip
        mid, root = hair_tone(rng.random())
        pts = []
        for j in range(41):
            t = j / 40.0
            pts.append((xs + taper * t ** 1.6 + math.sin(t * 5.0 + i) * 1.2, t * length))
        for j in range(40):
            t = j / 40.0
            wd = sw * (1.0 - t ** 2.2) + 1.2
            col = (root * (1 - min(1.0, t * 2.0)) + mid * min(1.0, t * 2.0)) * (0.92 + 0.14 * rng.random())
            d.line([(pts[j][0] * S, pts[j][1] * S), (pts[j + 1][0] * S, pts[j + 1][1] * S)], fill=tuple(int(c) for c in col) + (255,), width=max(1, int(wd * S)))
    card = card.resize((w, h), Image.LANCZOS)
    out.paste(card, (x0, y0))
    out.save(os.path.join(OUT, "T_CharCommanderHair_BaseColor.png"))


def main():
    os.makedirs(OUT, exist_ok=True)
    make_uniform()
    make_skin()
    make_hair()
    print("[CommanderTextures] wrote 4 textures to", OUT)


if __name__ == "__main__":
    main()
