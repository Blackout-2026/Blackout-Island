"""The texture-atlas layout of the desert Security Patrol operator, shared by build_operator.py (Blender) and make_patrol_textures.py
(Python + PIL) (step PS3). Owner: Core Developer.

Pure data and tiny helpers, no bpy and no PIL, so both sides can import it. Rects are (x, y, w, h) in pixels from the TOP-LEFT of the PNG.
UV space is bottom-left based, so v = 1 - y / size. Two materials (docs/art-pipeline.md section 18):
  M_Char_Patrol_Cloth  the weathered khaki tile T_CharPatrol_BaseColor (make_patrol_cloth.py), box-projected at one tile per metre
  M_Char_Patrol_Kit    this 1024 atlas, T_CharPatrolKit_BaseColor and _MetalSmooth: flat swatches plus painted decals and the face
"""

SIZE = 1024

SWATCH_SIZE = 96
KIT_SWATCHES = {
    # name: (rect, hex, metallic, smoothness)
    "skin": ((0, 0, SWATCH_SIZE, SWATCH_SIZE), "#a9785d", 0.0, 0.30),
    "tan": ((SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#b2a07f", 0.0, 0.22),         # helmet shell and rifle furniture
    "olive": ((2 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#524a30", 0.0, 0.15),    # the vest
    "dark": ((3 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#1c1d1b", 0.0, 0.28),     # straps, gear, rubber
    "leather": ((4 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#4e331d", 0.0, 0.30),  # belt and thigh straps
    "khaki": ((5 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#786a4c", 0.0, 0.18),    # boots and rails
    "gunmetal": ((6 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#2b2d30", 0.85, 0.55),
    "silver": ((7 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#a9adb2", 0.9, 0.6),     # buckles and snaps
    "mustard": ((8 * SWATCH_SIZE, 0, SWATCH_SIZE, SWATCH_SIZE), "#8c752f", 0.0, 0.15),
}

# painted decals (the aspect of each is the aspect of the part it lies on)
KIT_DECALS = {
    "webbing": (0, 112, 384, 256),      # the vest's PALS webbing panel, front and back
    "pouch": (400, 112, 160, 192),      # a magazine pouch: flap, buckle strap and stitching
    "radio": (576, 112, 160, 192),      # the chest radio: speaker, display and knobs
    "patch": (752, 112, 128, 128),      # a velcro loop patch, no insignia
    "zip": (896, 112, 64, 256),         # a zip: tape, teeth and a pull tab
    "ticks": (0, 384, 512, 48),         # the teeth of a picatinny rail
    "holster": (528, 320, 160, 192),    # the holster: nylon, stitching and a strap with a snap
    "boot": (0, 448, 192, 320),         # a boot's front: tongue, eyelets and criss-cross laces
}

# ----- the face: a planar front projection of the head, in world metres (the operator faces +Z, left = -X)
FACE_RECT = (672, 544, 320, 480)
FACE_X0, FACE_W = -0.10, 0.20          # x -0.10..0.10
FACE_Y1, FACE_H = 1.88, 0.30           # y 1.58..1.88
EYE_Y = 1.737
EYE_X = 0.0335
EYE_W = 0.0150                         # half-width of a visible eye
BROW_Y = 1.762
SLIT_X, SLIT_Y0, SLIT_Y1 = 0.062, 1.719, 1.755   # the opening of the balaclava: x +-0.062, y 1.719..1.755


def rect_uv(rect, size, fx, fy):
    """UV of a point at fraction (fx, fy) across the rect (fx from the left, fy from the TOP of the rect)."""
    x, y, w, h = rect
    return ((x + fx * w) / size, 1.0 - (y + fy * h) / size)


def swatch_uv(name):
    return rect_uv(KIT_SWATCHES[name][0], SIZE, 0.5, 0.5)


def face_uv(x, y):
    """UV of a head vertex at world (x, y): a planar projection from the front."""
    fx = min(max((x - FACE_X0) / FACE_W, 0.0), 1.0)
    fy = min(max((FACE_Y1 - y) / FACE_H, 0.0), 1.0)
    return rect_uv(FACE_RECT, SIZE, fx, fy)


def face_px(x, y):
    """The pixel of the atlas where the head projection puts world (x, y); the painter draws the face with it."""
    fx = (x - FACE_X0) / FACE_W
    fy = (FACE_Y1 - y) / FACE_H
    return FACE_RECT[0] + fx * FACE_RECT[2], FACE_RECT[1] + fy * FACE_RECT[3]


def face_scale():
    """Pixels per metre of the face projection (x and y)."""
    return FACE_RECT[2] / FACE_W, FACE_RECT[3] / FACE_H
