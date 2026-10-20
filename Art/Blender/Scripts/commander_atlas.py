"""The texture-atlas layout of the realistic Commander, shared by build_commander.py (Blender) and make_commander_textures.py (Python + PIL).

Pure data and tiny helpers, no bpy and no PIL, so both sides can import it. Rects are (x, y, w, h) in pixels from the TOP-LEFT of the PNG.
UV space is bottom-left based, so v = 1 - y / size.

Three textures, three materials (docs/art-pipeline.md section 18):
  uniform  M_Char_Commander_Uniform  white cloth, gold, black leather, ribbons, shoulder boards, name tag, cap
  skin     M_Char_Commander_Skin     the face (planar front projection), freckled arms and hands, neck
  hair     M_Char_Commander_Hair     strand texture on the hair cap and the hair locks
"""

SIZE = {"uniform": 1024, "skin": 1024, "hair": 512}

# ----- uniform atlas: flat swatches (base colour, metallic, smoothness) and painted decals
SWATCH_SIZE = 96
UNIFORM_SWATCHES = {
    # name: (rect, hex, metallic, smoothness)
    "white": ((0, 0, SWATCH_SIZE, SWATCH_SIZE), "#e9e9e4", 0.0, 0.18),
    "gold": ((SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#c8a24a", 0.85, 0.62),
    "black": ((2 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#15161a", 0.0, 0.62),
    "watch": ((3 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#2a2c30", 0.35, 0.55),
    "silver": ((4 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#b9bdc4", 0.85, 0.6),
    "shade": ((5 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#cfd0cb", 0.0, 0.18),   # the cloth in a fold or under a collar
    "navy": ((6 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#1b2540", 0.0, 0.3),
    "sole": ((7 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#0d0d0f", 0.0, 0.25),
}
UNIFORM_DECALS = {
    "board": (0, 112, 256, 128),      # shoulder board: white backing, gold stripes and a star
    "ribbons": (272, 112, 324, 96),   # 3 x 3 ribbon rack (the aspect of the 0.108 x 0.032 m rack)
    "nametag": (608, 112, 192, 64),   # black plate, gold edge, text
    "pin": (608, 192, 96, 96),        # gold warfare pin above the ribbons
    "visor": (0, 256, 384, 192),      # black cap visor with the gold oak leaves of a commander
    "device": (400, 320, 128, 128),   # the cap's gold device
    "cloth": (0, 464, 512, 512),      # white cloth with a faint weave, for the big garments
}

# ----- skin atlas
HEAD_RECT = (0, 0, 640, 738)
HEAD_X0, HEAD_W = -0.13, 0.26         # metres: the planar front projection of the head covers x -0.13..0.13
HEAD_Y1, HEAD_H = 1.87, 0.30          # and y 1.57..1.87
SKIN_TILE = (660, 0, 352, 352)        # freckled skin for arms, hands, chest patch
SKIN_SWATCH = (660, 380, 64, 64)      # plain skin for the neck and the inside of the ears
SKIN_HEX = "#dfb396"

# ----- hair atlas (RGBA, alpha-clipped): the left half is an opaque strand texture for the cap (u across the head, mirrored; v from the crown (top)
# to the hairline), the right half is one lock card (strands that taper to a point, transparent between them; u across, v from the root (top))
HAIR_CAP_RECT = (0, 0, 256, 512)
HAIR_CARD_RECT = (256, 0, 256, 512)
HAIR_RECT = HAIR_CAP_RECT

# ----- the face, in world metres (the Commander faces +Z, left = -X). The Blender head and the painter use the same numbers.
SKULL_C = (0.0, 1.735, -0.008)
SKULL_R = (0.076, 0.104, 0.098)
EYE_Y = 1.7285
EYE_X = 0.0335
EYE_W = 0.0180                         # half-width of the visible eye (outer to inner corner is 2 x this)
BROW_Y = 1.7525
NOSE_TIP_Y = 1.684
MOUTH_Y = 1.6475
CHIN_Y = 1.6125

# The head is designed at the numbers above and then scaled up by HS about its chin so it is in proportion with the 1.87 m body
# (a head of 0.23 m on 1.87 m is too small). Geometry and painter both apply it: world = T(design).
HS = 1.06
HEAD_PIVOT = (0.0, 1.6125, -0.008)
HEAD_DROP = -0.017                    # the whole head sits this much lower on the neck




def hx(x):
    return x * HS


def hy(y):
    return HEAD_PIVOT[1] + (y - HEAD_PIVOT[1]) * HS + HEAD_DROP


def rect_uv(rect, size, fx, fy):
    """UV of a point at fraction (fx, fy) across the rect (fx from the left, fy from the TOP of the rect)."""
    x, y, w, h = rect
    return ((x + fx * w) / size, 1.0 - (y + fy * h) / size)


def swatch_uv(name):
    rect = UNIFORM_SWATCHES[name][0]
    return rect_uv(rect, SIZE["uniform"], 0.5, 0.5)


def head_uv(x, y):
    """UV of a head vertex at world (x, y): a planar projection from the front."""
    fx = (x - HEAD_X0) / HEAD_W
    fy = (HEAD_Y1 - y) / HEAD_H
    return rect_uv(HEAD_RECT, SIZE["skin"], fx, fy)


def head_px(x, y):
    """The pixel of the skin texture where the head projection puts world (x, y); the painter draws the face with it."""
    fx = (x - HEAD_X0) / HEAD_W
    fy = (HEAD_Y1 - y) / HEAD_H
    return HEAD_RECT[0] + fx * HEAD_RECT[2], HEAD_RECT[1] + fy * HEAD_RECT[3]


def head_scale():
    """Pixels per metre of the head projection (x and y)."""
    return HEAD_RECT[2] / HEAD_W, HEAD_RECT[3] / HEAD_H
