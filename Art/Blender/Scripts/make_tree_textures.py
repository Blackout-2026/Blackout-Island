"""Draws the tree textures of step C11 with numpy and Pillow (no Blender, no photos). Owner: Core Developer.

    python Art/Blender/Scripts/make_tree_textures.py

Writes into Assets/Textures/Models (the folder ModelTextureRules manages: the suffix _Normal makes a normal map):
    T_PineBark_BaseColor.png / _Normal.png      512 x 512, tileable: vertical plates and dark furrows, like pine bark
    T_PineNeedles_BaseColor.png                 512 x 1024 RGBA: one spray of needle-bearing twigs, the twig at the bottom
    T_PalmBark_BaseColor.png / _Normal.png      512 x 512, tileable: scar rings and fibres, like a palm trunk
    T_PalmFrond_BaseColor.png                   640 x 1024 RGBA, an atlas: u 0..0.8 one whole pinnate frond (the card of LOD1/LOD2), u 0.8..1
                                                one hanging group of four leaflets (the real leaflet geometry of LOD0)
    T_AlmondBark_BaseColor.png / _Normal.png    512 x 512, tileable: smooth grey-brown bark with shallow fissures
    T_BroadLeaves_BaseColor.png                 1024 x 1024 RGBA: rosettes of large veined leaves on twigs (a sea almond branch end)
    T_BananaStem_BaseColor.png / _Normal.png    512 x 512, tileable: green leaf sheaths round a banana pseudo-stem
    T_BananaLeaf_BaseColor.png                  512 x 1024 RGBA: one banana leaf, midrib, parallel veins, torn edges

The bark is a height field made of tileable value noise (a random lattice that wraps round), coloured by height, with the normal
map taken from the height's gradient. The needle spray and the frond are drawn line by line at 4x size and shrunk, so the edges are
smooth; the colour under the transparent pixels is a leaf green, so no dark halo shows at the cut-out edge.
"""

import math
import os
import random

import numpy as np
from PIL import Image, ImageDraw

REPO = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Textures", "Models")


# --------------------------------------------------------------------------- tileable noise and maps

def tnoise(h, w, fy, fx, seed):
    """Value noise on a wrapping lattice of fy x fx cells, smoothly interpolated: it tiles in both directions."""
    rng = np.random.RandomState(seed)
    lattice = rng.rand(fy, fx)
    ys = np.arange(h) * fy / h
    xs = np.arange(w) * fx / w
    y0 = np.floor(ys).astype(int)
    x0 = np.floor(xs).astype(int)
    ty = ys - y0
    tx = xs - x0
    ty = ty * ty * (3 - 2 * ty)
    tx = tx * tx * (3 - 2 * tx)
    y1 = (y0 + 1) % fy
    x1 = (x0 + 1) % fx
    y0 %= fy
    x0 %= fx
    a = lattice[np.ix_(y0, x0)]
    b = lattice[np.ix_(y0, x1)]
    c = lattice[np.ix_(y1, x0)]
    d = lattice[np.ix_(y1, x1)]
    top = a + (b - a) * tx[None, :]
    bottom = c + (d - c) * tx[None, :]
    return top + (bottom - top) * ty[:, None]


def fbm(h, w, fy, fx, octaves, seed, gain=0.5):
    total = np.zeros((h, w))
    amp = 1.0
    norm = 0.0
    for o in range(octaves):
        total += amp * tnoise(h, w, fy * 2 ** o, fx * 2 ** o, seed + o * 17)
        norm += amp
        amp *= gain
    return total / norm


def smoothstep(lo, hi, x):
    t = np.clip((x - lo) / (hi - lo), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def normal_from_height(height, strength):
    """Tangent-space normal map (OpenGL, Y up) from a height field, wrapping round the edges so it tiles."""
    dx = (np.roll(height, -1, axis=1) - np.roll(height, 1, axis=1)) * 0.5
    dy = (np.roll(height, -1, axis=0) - np.roll(height, 1, axis=0)) * 0.5
    nx = -dx * strength
    ny = dy * strength  # image rows run top to bottom, texture V runs bottom to top
    nz = np.ones_like(nx)
    length = np.sqrt(nx * nx + ny * ny + nz * nz)
    return np.dstack([(nx / length * 0.5 + 0.5), (ny / length * 0.5 + 0.5), (nz / length * 0.5 + 0.5)])


def hexrgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def save_rgb(array, name):
    Image.fromarray((np.clip(array, 0, 1) * 255).astype(np.uint8), "RGB").save(os.path.join(OUT, name))
    print("wrote", name)


# --------------------------------------------------------------------------- bark

def pine_bark(size=512):
    h = w = size
    # long vertical plates: the noise is stretched along the trunk (few cells in y, many in x), broken by a second, finer layer
    plates = fbm(h, w, 3, 14, 4, 11)
    cracks = fbm(h, w, 9, 36, 3, 41)
    height = 0.6 * plates + 0.4 * cracks
    furrow = smoothstep(0.30, 0.52, height)  # 0 in the furrows, 1 on the plate tops
    ridge = furrow * (0.75 + 0.25 * fbm(h, w, 24, 24, 2, 71))
    dark = hexrgb("#241a13")
    mid = hexrgb("#5a4535")
    light = hexrgb("#8a6f58")
    tone = fbm(h, w, 6, 6, 3, 5)
    plate_colour = mid[None, None, :] + (light - mid)[None, None, :] * (tone[..., None] * 0.9)
    colour = dark[None, None, :] + (plate_colour - dark[None, None, :]) * ridge[..., None]
    # a little grey-green lichen on the plates, and warm reddish patches near the top of the plates
    lichen = smoothstep(0.62, 0.75, fbm(h, w, 5, 5, 3, 23)) * furrow
    colour = colour * (1 - lichen[..., None] * 0.35) + hexrgb("#6e7355")[None, None, :] * lichen[..., None] * 0.35
    save_rgb(colour, "T_PineBark_BaseColor.png")
    save_rgb(normal_from_height(ridge * 1.0 + 0.25 * cracks, 6.0), "T_PineBark_Normal.png")


def palm_bark(size=512):
    h = w = size
    # a coconut trunk: pale grey-tan, a ring scar every 1/16 of the tile (where a frond fell), vertical fibres, darker scar shadow
    y = np.arange(h)[:, None] / h
    wobble = 0.9 * fbm(h, w, 4, 12, 2, 7)
    ring = 0.5 + 0.5 * np.cos(2 * math.pi * y * 16 + wobble)
    scar = smoothstep(0.78, 0.98, ring)                  # the thin raised lip of the scar
    groove = smoothstep(0.30, 0.0, ring)                 # the shadow between two scars
    fibres = fbm(h, w, 2, 36, 3, 33)
    grain = fbm(h, w, 14, 44, 3, 61)
    height = 0.55 * scar - 0.25 * groove + 0.08 * fibres + 0.06 * grain
    light = hexrgb("#b4a68a")
    mid = hexrgb("#8d7f66")
    dark = hexrgb("#4f4331")
    t = np.clip(0.5 + 0.30 * (fibres - 0.5) + 0.30 * (grain - 0.5), 0, 1)[..., None]
    up = np.clip((t - 0.5) * 2.0, 0, 1)
    down = np.clip((0.5 - t) * 2.0, 0, 1)
    colour = mid[None, None, :] + (light - mid)[None, None, :] * up + (dark - mid)[None, None, :] * down
    colour = colour * (1.0 - 0.28 * groove[..., None]) + (light[None, None, :] * 0.15) * scar[..., None]
    # greenish algae streaks
    algae = smoothstep(0.66, 0.8, fbm(h, w, 3, 14, 3, 91))
    colour = colour * (1 - algae[..., None] * 0.25) + hexrgb("#6d7556")[None, None, :] * algae[..., None] * 0.25
    save_rgb(colour, "T_PalmBark_BaseColor.png")
    save_rgb(normal_from_height(height, 4.5), "T_PalmBark_Normal.png")


def almond_bark(size=512):
    h = w = size
    # smooth grey-brown bark broken by shallow, wandering fissures
    fissure = fbm(h, w, 3, 24, 4, 17)
    cell = fbm(h, w, 10, 10, 3, 5)
    ridge = smoothstep(0.38, 0.56, fissure)
    height = 0.65 * ridge + 0.35 * cell
    dark = hexrgb("#2f271f")
    mid = hexrgb("#6b5d4d")
    light = hexrgb("#9a8d7b")
    tone = fbm(h, w, 7, 7, 3, 29)[..., None]
    plate = mid[None, None, :] + (light - mid)[None, None, :] * tone
    colour = dark[None, None, :] + (plate - dark[None, None, :]) * (0.25 + 0.75 * ridge[..., None])
    lichen = smoothstep(0.64, 0.78, fbm(h, w, 6, 6, 3, 57))
    colour = colour * (1 - lichen[..., None] * 0.3) + hexrgb("#8a9a82")[None, None, :] * lichen[..., None] * 0.3
    save_rgb(colour, "T_AlmondBark_BaseColor.png")
    save_rgb(normal_from_height(height, 5.0), "T_AlmondBark_Normal.png")


def banana_stem(size=512):
    h = w = size
    # overlapping leaf sheaths: pale green with darker horizontal sheath edges, vertical fibres and a few brown dead patches
    y = np.arange(h)[:, None] / h
    sheath = 0.5 + 0.5 * np.cos(2 * math.pi * y * 6 + 1.2 * fbm(h, w, 3, 8, 2, 13))
    edge = smoothstep(0.82, 1.0, sheath)
    fibres = fbm(h, w, 2, 70, 3, 21)
    height = 0.4 * sheath + 0.3 * fibres + 0.4 * edge
    light = hexrgb("#a9b46c")
    mid = hexrgb("#6e8a3c")
    dark = hexrgb("#3a4a22")
    t = (0.55 * fibres + 0.45 * sheath)[..., None]
    colour = dark[None, None, :] + (mid - dark)[None, None, :] * np.clip(t * 1.6, 0, 1) + (light - mid)[None, None, :] * np.clip(t - 0.55, 0, 1) * 2
    colour = colour * (1 - 0.35 * edge[..., None])
    dead = smoothstep(0.62, 0.78, fbm(h, w, 3, 5, 3, 83))
    colour = colour * (1 - dead[..., None] * 0.7) + hexrgb("#6b5436")[None, None, :] * dead[..., None] * 0.7
    save_rgb(colour, "T_BananaStem_BaseColor.png")
    save_rgb(normal_from_height(height, 4.0), "T_BananaStem_Normal.png")


# --------------------------------------------------------------------------- needle spray and palm frond

SCALE = 4


def lerp_col(a, b, t):
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def finish_leaf(colour_img, alpha_img, name, fill, fade=False):
    """Shrinks the 4x drawing, bleeds the leaf colour under the transparent pixels and writes an RGBA png."""
    w, h = colour_img.size[0] // SCALE, colour_img.size[1] // SCALE
    colour = np.asarray(colour_img.resize((w, h), Image.LANCZOS).convert("RGB")).astype(np.float32) / 255.0
    alpha = np.asarray(alpha_img.resize((w, h), Image.LANCZOS)).astype(np.float32) / 255.0
    # premultiplied shrink then divide, so the edge pixels keep the needle colour instead of the background
    solid = np.asarray(colour_img.resize((w, h), Image.BOX).convert("RGB")).astype(np.float32) / 255.0
    a = np.clip(alpha, 1e-3, 1.0)[..., None]
    colour = np.where(alpha[..., None] > 0.02, np.clip(colour / np.maximum(a, 0.3), 0, 1), np.asarray(fill)[None, None, :])
    colour = np.clip(colour, 0, 1)
    if fade:
        # dissolve the alpha toward the left, right and top edges, so a card fades out instead of ending in a hard polygon edge
        ys = np.linspace(1.0, 0.0, h)[:, None]  # 1 at the top row, 0 at the bottom row
        xs = np.linspace(0.0, 1.0, w)[None, :]
        side = np.clip(np.minimum(xs, 1.0 - xs) / 0.17, 0.0, 1.0)
        top = np.clip((ys - 0.90) / 0.08, 0.0, 1.0)
        env = side * (1.0 - top)
        alpha = alpha * (env * env * (3 - 2 * env))
    rgba = np.dstack([colour, alpha])
    Image.fromarray((rgba * 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, name))
    print("wrote", name)


def pine_needles(w=512, h=1024):
    W, H = w * SCALE, h * SCALE
    rng = random.Random(3)
    colour = Image.new("RGB", (W, H), (34, 62, 34))
    alpha = Image.new("L", (W, H), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)
    twig_dark, twig_light = (62, 46, 34), (96, 72, 54)
    needle_dark, needle_light = (22, 48, 24), (64, 104, 52)

    def needles(px, py, heading, length, count, tip=0.0):
        """A fascicle fanned round a point: count needles, spread about the heading (angles in radians, 0 = up the image)."""
        for _ in range(count):
            spread = rng.uniform(-1.15, 1.15)
            a = heading + spread
            ln = length * rng.uniform(0.65, 1.0) * (1.0 - 0.25 * abs(spread))
            x2, y2 = px + math.sin(a) * ln, py - math.cos(a) * ln
            col = lerp_col(needle_dark, needle_light, min(1.0, rng.random() * (1.0 - tip) + tip * 0.95))
            width = int(SCALE * rng.uniform(2.0, 3.0))
            dc.line([(px, py), (x2, y2)], fill=col, width=width)
            da.line([(px, py), (x2, y2)], fill=255, width=width)

    def shoot(x0, y0, heading, length, needle_len, depth):
        """A twig with a fascicle every few pixels, tapering needles toward the tip, and side shoots."""
        steps = int(length / (11 * SCALE))
        px, py = x0, y0
        for i in range(steps):
            t = i / max(1, steps - 1)
            heading += rng.uniform(-0.03, 0.03)
            nx, ny = px + math.sin(heading) * 11 * SCALE, py - math.cos(heading) * 11 * SCALE
            width = max(2, int(SCALE * (3.0 - 2.0 * t)))
            col = lerp_col(twig_dark, twig_light, rng.random() * 0.5)
            dc.line([(px, py), (nx, ny)], fill=col, width=width)
            da.line([(px, py), (nx, ny)], fill=255, width=width)
            needles(nx, ny, heading, needle_len * (1.0 - 0.40 * t), rng.randint(8, 12), tip=0.55 * t * t)
            if depth > 0 and i > 1 and i % 4 == 0 and t < 0.85:
                side = 1 if (i // 4) % 2 == 0 else -1
                shoot(nx, ny, heading + side * rng.uniform(0.6, 0.9), length * (0.42 - 0.25 * t), needle_len * 0.9, depth - 1)
            px, py = nx, ny
        needles(px, py, heading, needle_len * 0.7, 12, tip=0.9)  # the tuft of new growth at the tip

    shoot(W * 0.5, H * 0.99, 0.0, H * 0.94, 100.0, 2)
    finish_leaf(colour, alpha, "T_PineNeedles_BaseColor.png", (0.17, 0.29, 0.16), fade=True)


def palm_frond(w=640, h=1024):
    W, H = w * SCALE, h * SCALE
    FW = 512 * SCALE                       # the frond card occupies the left 512 columns, the leaflet group the right 128
    rng = random.Random(8)
    colour = Image.new("RGB", (W, H), (70, 112, 48))
    alpha = Image.new("L", (W, H), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)
    rib_dark, rib_light = (112, 118, 62), (150, 150, 86)
    leaf_dark, leaf_light = (28, 66, 24), (96, 142, 52)
    cx = FW * 0.5
    top, bottom = H * 0.03, H * 0.99
    n = 120
    for i in range(n):
        t = i / (n - 1)                    # 0 at the base, 1 at the tip
        y = bottom - t * (bottom - top)
        # leaflet length: short at the base, longest at 35 %, tapering to the tip
        length = (FW * 0.49) * math.sin(math.pi * min(1.0, 0.16 + t * 0.86)) ** 0.7 * (1.0 - 0.08 * t)
        for side in (-1, 1):
            # a leaflet leans toward the tip and droops a little: two segments
            ang = math.radians(48 - 20 * t) + rng.uniform(-0.05, 0.05)
            x1 = cx + side * math.sin(ang) * length * 0.55
            y1 = y - math.cos(ang) * length * 0.55
            x2 = cx + side * math.sin(ang * 0.8) * length
            y2 = y1 - math.cos(ang * 0.55) * length * 0.55 + length * 0.1
            col = lerp_col(leaf_dark, leaf_light, 0.25 + 0.6 * rng.random())
            width = int(SCALE * (8.5 - 4.5 * t))
            dc.line([(cx, y), (x1, y1), (x2, y2)], fill=col, width=width, joint="curve")
            da.line([(cx, y), (x1, y1), (x2, y2)], fill=255, width=width, joint="curve")
    # the rib on top, thick at the base
    for i in range(0, 200):
        t0, t1 = i / 200, (i + 1) / 200
        y0, y1 = bottom - t0 * (bottom - top), bottom - t1 * (bottom - top)
        width = int(SCALE * (11 - 8 * t0))
        col = lerp_col(rib_dark, rib_light, t0)
        dc.line([(cx, y0), (cx, y1)], fill=col, width=width)
        da.line([(cx, y0), (cx, y1)], fill=255, width=width)

    # the leaflet group: four long narrow blades hanging from the base (bottom) to the tip (top), each pointed, with a pale midrib
    gx0, gw = FW, 128 * SCALE
    for k in range(4):
        xc = gx0 + gw * (0.15 + 0.23 * k) + rng.uniform(-0.02, 0.02) * gw
        tilt = rng.uniform(-0.05, 0.05) * gw + (k - 1.5) * 0.035 * gw      # the blades fan out a little toward the tip
        length = H * rng.uniform(0.90, 0.985)
        wmax = gw * rng.uniform(0.085, 0.105)
        steps = 40
        pts_l, pts_r, mids = [], [], []
        for i in range(steps + 1):
            t = i / steps
            y = H * 0.995 - t * length
            x = xc + tilt * t + math.sin(t * 3.0 + k) * gw * 0.012
            hw = wmax * (math.sin(math.pi * min(1.0, 0.04 + 0.96 * t ** 0.85)) ** 0.75) * (1.0 - 0.15 * t) + SCALE * 0.5
            pts_l.append((x - hw, y))
            pts_r.append((x + hw, y))
            mids.append((x, y))
        for i in range(steps):
            t = i / steps
            col = lerp_col((34, 74, 28), (92, 140, 54), 0.35 + 0.55 * math.sin(math.pi * t) * rng.uniform(0.7, 1.0))
            poly = [pts_l[i], pts_r[i], pts_r[i + 1], pts_l[i + 1]]
            dc.polygon(poly, fill=col)
            da.polygon(poly, fill=255)
        dc.line(mids, fill=(140, 160, 84), width=SCALE)
    finish_leaf(colour, alpha, "T_PalmFrond_BaseColor.png", (0.2, 0.34, 0.15))


def broad_leaves(w=1024, h=1024):
    """Rosettes of big obovate leaves on twigs: the end of a sea-almond branch."""
    W, H = w * SCALE, h * SCALE
    rng = random.Random(14)
    colour = Image.new("RGB", (W, H), (60, 104, 42))
    alpha = Image.new("L", (W, H), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)

    def outline(x, y, sx, sy, length, width, shrink=1.0):
        px, py = -sy, sx
        steps = 28
        left, right, spine = [], [], []
        for i in range(steps + 1):
            t = i / steps
            hw = (width * 0.5 * (t ** 0.55) * ((1.0 - t) ** 0.48) * 2.15) * shrink + SCALE * 0.4
            cx_, cy_ = x + sx * length * t, y + sy * length * t
            left.append((cx_ - px * hw, cy_ - py * hw))
            right.append((cx_ + px * hw, cy_ + py * hw))
            spine.append((cx_, cy_))
        return left + right[::-1]

    def leaf(x, y, heading, length, width):
        """An obovate leaf (widest past the middle, pointed tip) from (x, y) outward along heading (0 = up the image)."""
        base = rng.choice([(46, 94, 36), (62, 112, 40), (82, 128, 44), (98, 140, 50), (118, 146, 52)])
        edge = (max(0, base[0] - 18), max(0, base[1] - 22), max(0, base[2] - 10))
        sx, sy = math.sin(heading), -math.cos(heading)
        px, py = -sy, sx
        dc.polygon(outline(x, y, sx, sy, length, width), fill=edge)
        da.polygon(outline(x, y, sx, sy, length, width), fill=255)
        dc.polygon(outline(x, y, sx, sy, length, width, 0.8), fill=base)
        rib = (min(255, base[0] + 40), min(255, base[1] + 38), min(255, base[2] + 24))
        dc.line([(x, y), (x + sx * length * 0.97, y + sy * length * 0.97)], fill=rib, width=int(SCALE * 2.2))
        for i in range(2, 13):  # side veins, swept toward the tip
            t = i / 14.0
            bx, by = x + sx * length * t, y + sy * length * t
            hw = width * 0.5 * (t ** 0.55) * ((1.0 - t) ** 0.48) * 2.15 * 0.85
            for side in (-1, 1):
                ex = bx + sx * hw * 0.8 + side * px * hw
                ey = by + sy * hw * 0.8 + side * py * hw
                dc.line([(bx, by), (ex, ey)], fill=rib, width=SCALE)

    def rosette(x, y, heading, size):
        count = rng.randint(6, 8)
        for k in range(count):
            a = heading + (k - (count - 1) / 2) * rng.uniform(0.34, 0.46) + rng.uniform(-0.1, 0.1)
            L = size * rng.uniform(0.75, 1.0) * (1.0 - 0.18 * abs(k - (count - 1) / 2) / count)
            leaf(x, y, a, L, L * rng.uniform(0.40, 0.48))

    def twig(x0, y0, heading, length):
        x, y = x0, y0
        steps = 6
        for i in range(steps):
            heading += rng.uniform(-0.12, 0.12)
            nx, ny = x + math.sin(heading) * length / steps, y - math.cos(heading) * length / steps
            wd = int(SCALE * 4.0 * (1.0 - 0.6 * i / steps))
            dc.line([(x, y), (nx, ny)], fill=(78, 64, 46), width=wd)
            da.line([(x, y), (nx, ny)], fill=255, width=wd)
            x, y = nx, ny
        return x, y

    def branchlet(x, y, heading, length, depth):
        ex, ey = twig(x, y, heading, length)
        rosette(ex, ey, heading, H * 0.145)
        if depth > 0:
            bx, by = x + (ex - x) * 0.6, y + (ey - y) * 0.6
            for sgn in (-1, 1):
                branchlet(bx, by, heading + sgn * 0.8, length * 0.66, depth - 1)
            branchlet(ex, ey, heading + rng.uniform(-0.2, 0.2), length * 0.6, depth - 1)

    branchlet(W * 0.5, H * 0.995, 0.0, H * 0.27, 3)
    finish_leaf(colour, alpha, "T_BroadLeaves_BaseColor.png", (0.22, 0.38, 0.16), fade=True)


def banana_leaf(w=512, h=1024):
    W, H = w * SCALE, h * SCALE
    rng = random.Random(22)
    colour = Image.new("RGB", (W, H), (84, 140, 48))
    alpha = Image.new("L", (W, H), 0)
    dc, da = ImageDraw.Draw(colour), ImageDraw.Draw(alpha)
    cx = W * 0.5
    top, bottom = H * 0.02, H * 0.995
    steps = 80

    def half_width(t):
        return W * 0.47 * (math.sin(math.pi * min(1.0, 0.05 + 0.95 * t ** 0.9)) ** 0.42) * (1.0 - 0.10 * t)

    left, right = [], []
    for i in range(steps + 1):
        t = i / steps
        y = bottom - t * (bottom - top)
        hw = half_width(t) + SCALE
        left.append((cx - hw, y))
        right.append((cx + hw, y))
    for i in range(steps):
        t = i / steps
        shade = 0.5 + 0.5 * math.sin(t * 5.0)
        col = lerp_col((52, 106, 34), (104, 156, 58), 0.35 + 0.35 * shade)
        poly = [left[i], right[i], right[i + 1], left[i + 1]]
        dc.polygon(poly, fill=col)
        da.polygon(poly, fill=255)
    # parallel veins leaving the midrib at a steep angle, and the midrib itself
    for i in range(6, steps * 2):
        t = i / (steps * 2.0)
        y = bottom - t * (bottom - top)
        hw = half_width(t)
        for side in (-1, 1):
            dc.line([(cx, y), (cx + side * hw, y - hw * 0.28)], fill=(64, 120, 40), width=max(1, SCALE // 2))
    for i in range(steps):
        dc.line([(cx, left[i][1]), (cx, left[i + 1][1])], fill=(150, 176, 86), width=int(SCALE * (7 - 5 * i / steps)))
    # the leaf is torn between veins into ribbons: thin cuts from the edge toward the rib
    for _ in range(24):
        t = rng.uniform(0.18, 0.92)
        y = bottom - t * (bottom - top)
        hw = half_width(t)
        side = rng.choice((-1, 1))
        depth = hw * rng.uniform(0.45, 0.90)
        x_edge = cx + side * hw * 1.05
        x_in = cx + side * (hw - depth)
        y_in = y - depth * 0.28
        width = int(SCALE * rng.uniform(2.5, 6.0))
        da.line([(x_edge, y + hw * 0.05), (x_in, y_in)], fill=0, width=width)
        dc.line([(x_in, y_in), (x_in + side * SCALE * 2, y_in)], fill=(130, 120, 60), width=SCALE)
    finish_leaf(colour, alpha, "T_BananaLeaf_BaseColor.png", (0.28, 0.46, 0.18))


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    pine_bark()
    palm_bark()
    pine_needles()
    palm_frond()
    almond_bark()
    banana_stem()
    broad_leaves()
    banana_leaf()
