"""A realistic gloved hand and wrist in a camouflage sleeve, shared by every first-person viewmodel (step C7). Owner: Core Developer.

Not run on its own: build_knife.py, build_smg.py and build_sniper.py import it.

How the hand is made. A hand is sculpted from a SKELETON of points with a radius at each one (palm slab, four fingers of three
bones, a thumb, the wrist and the forearm), run through Blender's Skin modifier, subdivided twice and decimated to a triangle
budget. That gives round fingers with knuckles, a flat palm and a tapering forearm instead of boxes and sticks.

Frame. A hand is described in HAND space, an adult male right hand: +y is the back of the hand, +z the way the fingers point, the
thumb is at -x. A left hand is the same shape mirrored in x. add_hand() places it in the weapon's gun frame (Unity metres, as in
build_smg.py) with a position and two directions (fingers, back of hand), curls the fingers by the given angles, bends the thumb,
and runs the forearm out along arm_dir to a rolled camouflage sleeve.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402

B = S.B
GLOVE, SLEEVE = S.GLOVE, S.SLEEVE  # the material slots: M_Weapon_Glove (tan tactical glove) and M_Weapon_Camo (sleeve)

# name, knuckle x, knuckle z, bone lengths (proximal, middle, distal), knuckle radius
FINGERS = (
    ("index", -0.0262, 0.0455, (0.0400, 0.0240, 0.0205), 0.0094),
    ("middle", -0.0088, 0.0480, (0.0440, 0.0275, 0.0220), 0.0097),
    ("ring", 0.0088, 0.0460, (0.0410, 0.0255, 0.0210), 0.0091),
    ("pinky", 0.0255, 0.0405, (0.0330, 0.0190, 0.0180), 0.0080),
)
SPLAY = (-4.0, 0.0, 3.0, 8.0)       # degrees the fingers fan out about the back-of-hand axis when relaxed
TAPER = (1.0, 0.90, 0.80, 0.68)     # radius at the knuckle, middle joint, fingertip joint, tip

ARM_THIN = 0.80                     # the forearm and sleeve are slimmer than the enlarged hand, so a big sleeve does not fill the screen
HAND_SCALE = 1.35                   # first-person models draw the hands larger than life so they read on screen

RELAXED = {"curls": ((12, 14, 10), (14, 16, 10), (16, 18, 12), (20, 22, 12)), "thumb": None}


def _unit(v):
    return Vector(v).normalized()


def finger_points(index, curls, splay_extra=0.0):
    """Joint positions (hand space) and radii of one finger; each curl bends the next bone toward the palm (-y).
    Returns the Skin-modifier points: the joints swell a little, the bone between them has a waist, the tip is rounded."""
    name, x, z, lengths, r = FINGERS[index]
    p = Vector((x, 0.001, z))
    heading = 0.0
    phi = math.radians(SPLAY[index] + splay_extra)
    joints = [(p.copy(), r * TAPER[0])]
    dirs = []
    for k, length in enumerate(lengths):
        heading += curls[k]
        th = math.radians(heading)
        d = Vector((math.sin(phi) * math.cos(th), -math.sin(th), math.cos(phi) * math.cos(th)))
        dirs.append((d, th))
        p = p + d * length
        joints.append((p.copy(), r * TAPER[k + 1]))
    pts = [joints[0]]
    for k in range(3):
        (pa, ra), (pb, rb) = joints[k], joints[k + 1]
        pts.append(((pa + pb) * 0.5, (ra + rb) * 0.5 * 0.93))                    # the waist of the bone
        pts.append((pb, rb * (1.06 if k < 2 else 0.92)))                          # the joint swells
    d_last = dirs[2][0]
    pts.append((joints[3][0] + d_last * 0.0050, joints[3][1] * 0.62))             # the rounded fingertip
    return pts


DEFAULT_THUMB = (  # hand-space points and radii: base on the palm, knuckle, joint, tip
    ((-0.0300, -0.0090, -0.0130), 0.0128),
    ((-0.0490, -0.0125, 0.0070), 0.0118),
    ((-0.0610, -0.0105, 0.0300), 0.0108),
    ((-0.0640, -0.0070, 0.0500), 0.0094),
)


def palm_cage():
    """Hand-space vertices and faces of the palm: a tapered, slightly arched slab that subdivision rounds into a pillow."""
    rings = (  # z, half width, y bottom, y top (the back of the hand is a little arched, the palm a little hollow)
        (-0.0400, 0.0265, -0.0125, 0.0115),
        (-0.0080, 0.0400, -0.0150, 0.0160),
        (0.0300, 0.0475, -0.0135, 0.0150),
        (0.0480, 0.0465, -0.0120, 0.0125),
    )
    verts, faces = [], []
    for z, hw, y0, y1 in rings:
        verts.extend([Vector((-hw, y0, z)), Vector((hw, y0, z)), Vector((hw, y1, z)), Vector((-hw, y1, z))])
    for r in range(len(rings) - 1):
        a, b = r * 4, (r + 1) * 4
        for k in range(4):
            k2 = (k + 1) % 4
            faces.append([a + k, a + k2, b + k2, b + k])
    faces.append([3, 2, 1, 0])
    n = (len(rings) - 1) * 4
    faces.append([n, n + 1, n + 2, n + 3])
    return verts, faces


def _orth(v, d):
    return (v - d * v.dot(d)).normalized()


def _lerp_radius(table, s):
    """Radii (rx, ry) of the arm at distance s along it, linear between the table rows."""
    if s <= table[0][0]:
        return table[0][1]
    for (s0, r0), (s1, r1) in zip(table, table[1:]):
        if s <= s1:
            t = (s - s0) / (s1 - s0)
            return (r0[0] + (r1[0] - r0[0]) * t, r0[1] + (r1[1] - r0[1]) * t)
    return table[-1][1]


def arm_points(w, d, visible):
    """The forearm: it leaves the palm straight back (-z) and bends smoothly to the direction d over the wrist, so the wrist does
    not kink. A quadratic curve from inside the palm through the wrist w to w + d*0.07, then a straight line along d."""
    a = w + Vector((0.0, 0.0, 0.030))
    e = w + d * 0.070
    path = []
    for u in (0.0, 0.25, 0.5, 0.75, 1.0):
        path.append(a * (1 - u) ** 2 + w * (2 * u * (1 - u)) + e * u * u)
    reach = visible + 0.060                                  # the glove ends here; the sleeve starts just after it
    for t in (0.115, 0.17):
        if t < reach - 0.010:
            path.append(w + d * t)
    path.append(w + d * max(reach, 0.085))
    cum = [0.0]
    for k in range(1, len(path)):
        cum.append(cum[-1] + (path[k] - path[k - 1]).length)
    table = ((-0.030, (0.0300, 0.0215)), (0.0, (0.0285, 0.0205)), (0.040, (0.0305, 0.0235)),
             (max(reach, 0.085), (0.0390, 0.0340)))
    out = []
    for k, pt in enumerate(path):
        rx, ry = _lerp_radius(table, cum[k] - cum[2])
        out.append((pt, rx * ARM_THIN, ry * ARM_THIN))
    return out


def chains(curls, thumb, arm_local, visible, splay_extra, thumb_base=None, ridge=(0.0128, 1.0)):
    """The tube parts of the hand in hand space: (list of (point, rx, ry), weight of the triangle budget) and the wrist point."""
    out = []
    for i in range(4):
        pts = finger_points(i, curls[i], splay_extra)
        x, z = FINGERS[i][1], FINGERS[i][2]
        inner = (Vector((x, 0.0, z - 0.020)), pts[0][1] * 1.10)
        out.append(([(inner[0], inner[1], inner[1] * 0.92)] + [(p, r, r * 0.88) for p, r in pts], 1.3))
    tp = thumb or DEFAULT_THUMB
    base = thumb_base or [(Vector((-0.0270, -0.0070, -0.0240)), 0.0190, 0.0150), (Vector((-0.0380, -0.0120, -0.0060)), 0.0150, 0.0130)]
    tpts = [(Vector(p), r) for p, r in tp]
    tmid = [(tpts[0][0], tpts[0][1]), ((tpts[0][0] + tpts[1][0]) * 0.5, tpts[0][1] * 0.94), (tpts[1][0], tpts[1][1] * 1.06),
            ((tpts[1][0] + tpts[2][0]) * 0.5, tpts[1][1] * 0.94), (tpts[2][0], tpts[2][1] * 1.04), (tpts[3][0], tpts[3][1] * 0.92)]
    dt = (tpts[3][0] - tpts[2][0]).normalized()
    tmid.append((tpts[3][0] + dt * 0.0055, tpts[3][1] * 0.64))
    out.append((base + [(p, r, r * 0.92) for p, r in tmid], 1.6))

    d = _unit(arm_local)
    w = Vector((0.0, -0.0040, -0.0400))
    out.append((arm_points(w, d, visible), 1.8))
    # the glove's wrist strap: a slightly fatter band around the wrist where the hook-and-loop closure sits
    out.append(([(w + d * 0.012, 0.0325, 0.0250), (w + d * 0.034, 0.0335, 0.0265)], 0.45))

    # the ball of the thumb: a swelling along the thumb's metacarpal on the palm side
    out.append(([(Vector((-0.0300, -0.0150, -0.0340)), 0.0200, 0.0120), (Vector((-0.0370, -0.0140, -0.0080)), 0.0180, 0.0120),
                 (Vector((-0.0330, -0.0090, 0.0200)), 0.0120, 0.0090)], 0.7))
    # the knuckle row: a ridge across the back of the hand where the finger bones start
    if ridge is not None:
        kn = [(Vector((FINGERS[i][1], ridge[0], FINGERS[i][2] + 0.0030)), FINGERS[i][4] * 1.04 * ridge[1], FINGERS[i][4] * 0.82 * ridge[1])
              for i in range(4)]
        out.append((kn, 1.0))
    return out, w


def evaluate(me, ob_setup):
    ob = bpy.data.objects.new(me.name, me)
    bpy.context.scene.collection.objects.link(ob)
    bpy.context.view_layer.objects.active = ob
    ob_setup(ob)
    dg = bpy.context.evaluated_depsgraph_get()
    out = bpy.data.meshes.new_from_object(ob.evaluated_get(dg))
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    bm = bmesh.new()
    bm.from_mesh(out)
    bpy.data.meshes.remove(out)
    return bm


def decimate(bm, target):
    bmesh.ops.triangulate(bm, faces=bm.faces)
    n = len(bm.faces)
    if n <= target:
        return bm
    m = bpy.data.meshes.new("dense")
    bm.to_mesh(m)
    bm.free()

    def setup(ob):
        dec = ob.modifiers.new("Dec", 'DECIMATE')
        dec.decimate_type = 'COLLAPSE'
        dec.ratio = max(0.04, target / n)

    return evaluate(m, setup)


def sculpt_chain(points_b, target):
    """One tube from the Skin modifier: points_b is [(Blender point, rx, ry)]; subdivided twice, decimated to target triangles."""
    me = bpy.data.meshes.new("chain")
    me.from_pydata([tuple(p[0]) for p in points_b], [(i, i + 1) for i in range(len(points_b) - 1)], [])

    def setup(ob):
        skin = ob.modifiers.new("Skin", 'SKIN')
        skin.use_smooth_shade = True
        for i, (_, rx, ry) in enumerate(points_b):
            me.skin_vertices[0].data[i].radius = (rx, ry)
        me.skin_vertices[0].data[0].use_root = True
        sub = ob.modifiers.new("Sub", 'SUBSURF')
        sub.levels = 2
        sub.render_levels = 2

    return decimate(evaluate(me, setup), target)


def sculpt_palm(cage_b, faces, target):
    me = bpy.data.meshes.new("palm")
    me.from_pydata([tuple(v) for v in cage_b], [], faces)

    def setup(ob):
        sub = ob.modifiers.new("Sub", 'SUBSURF')
        sub.levels = 2
        sub.render_levels = 2

    return decimate(evaluate(me, setup), target)


def add_hand(side, pos, fwd, up, curls=None, thumb=None, arm_dir=(0.0, -0.5, -1.0), visible=0.04, sleeve_len=0.6,
             target_tris=1500, splay_extra=0.0, thumb_base=None, ridge=(0.0128, 1.0)):
    """Adds Glove<side> (the gloved hand and wrist, glove slot) and Sleeve<side> (the camouflage sleeve) to the kit's objects.

    pos: the palm centre in the gun frame. fwd, up: where the fingers point and where the back of the hand faces, in the gun
    frame. curls: four tuples of three bend angles in degrees (index to pinky). thumb: hand-space (point, radius) list, or None.
    thumb_base: hand-space (point, rx, ry) list for the root of the thumb, or None for the default root under the palm.
    ridge: (height above the palm plane, size factor) of the knuckle ridge across the back of the hand, or None for no ridge. In a
    tight fist seen from the back the ridge reads as an extra finger lying across the hand, so the knife leaves it out.
    arm_dir: the direction of the forearm in the gun frame. visible: length of the glove cuff on the forearm before the sleeve starts.
    """
    curls = curls or RELAXED["curls"]
    rot = S.frame3(fwd, up)                       # hand space -> gun frame (columns: right, up, forward)
    mirror = Matrix(((-1, 0, 0), (0, 1, 0), (0, 0, 1))) if side == "L" else Matrix.Identity(3)
    arm_local = (mirror @ (rot.inverted() @ Vector(arm_dir).normalized()))
    pos = Vector(pos)

    def to_gun(v):
        return rot @ (mirror @ (v * HAND_SCALE)) + pos

    cage, cage_faces = palm_cage()
    tube_list, wrist = chains(curls, thumb, arm_local, visible, splay_extra, thumb_base, ridge)
    weights = 2.2 + sum(w for _, w in tube_list)
    unit = target_tris / weights
    verts, faces = [], []
    parts = [sculpt_palm([B(to_gun(v)) for v in cage], cage_faces, unit * 2.2)]
    for pts, w in tube_list:
        parts.append(sculpt_chain([(B(to_gun(p)), rx * HAND_SCALE, ry * HAND_SCALE) for p, rx, ry in pts], unit * w))
    for bm in parts:
        bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
        bm.verts.index_update()
        base = len(verts)
        verts.extend(v.co.copy() for v in bm.verts)
        faces.extend([[base + v.index for v in f.verts] for f in bm.faces])
        bm.free()
    name = "Glove" + side
    S.OBJECTS.setdefault(name, []).append((verts, faces, GLOVE))

    # the sleeve: a rolled cuff, then a loose tapered tube that goes out of view
    d = Vector(arm_dir).normalized()
    start = to_gun(wrist + arm_local * (visible + 0.012))
    sname = "Sleeve" + side
    srot = S.frame3(d, (0.0, 1.0, 0.0) if abs(d.y) < 0.9 else (1.0, 0.0, 0.0))
    first = len(S.OBJECTS.get(sname, []))
    k = HAND_SCALE * 0.85
    S.cyl(sname, SLEEVE, (0.0, 0.0, sleeve_len / 2.0 + 0.012), 'z', 0.0430 * k, sleeve_len, segs=14, r2=0.0520 * k)
    S.cyl(sname, SLEEVE, (0.0, 0.0, 0.010), 'z', 0.0470 * k, 0.026, segs=14)          # the rolled cuff
    S.cyl(sname, SLEEVE, (0.0, 0.0, 0.0), 'z', 0.0490 * k, 0.008, segs=14)            # its folded edge
    for index in range(first, len(S.OBJECTS[sname])):
        sv, sf, slot = S.OBJECTS[sname][index]
        S.OBJECTS[sname][index] = ([B(srot @ S._to_unity(v) + start) for v in sv], sf, slot)


def grip_frame(handle, fingers, tilt_deg=25.0, side="R"):
    """Hand frame (fwd = where the fingers point, up = back of the hand) for a hand that grips a handle.

    handle: the handle's axis pointing to the thumb and index side (the blade, the muzzle, the top of a pistol grip).
    fingers: roughly where the fingers point (it is made perpendicular to the handle); it decides on which side of the handle the
    palm lies. tilt_deg: how far the handle runs diagonally across the palm (about 25 degrees in a real power grip), which tips the
    fingers a little towards the handle direction. side: "R" or "L"; a left hand is the mirror image, so it is computed mirrored.
    The forearm then lies along -fwd, as a real wrist allows. Returns (fwd, up) in the caller's frame."""
    flip = Vector((-1.0, 1.0, 1.0)) if side == "L" else Vector((1.0, 1.0, 1.0))
    h = Vector((handle[0] * flip.x, handle[1], handle[2])).normalized()
    f = Vector((fingers[0] * flip.x, fingers[1], fingers[2]))
    f = (f - h * f.dot(h)).normalized()
    t = math.radians(tilt_deg)
    zl = f * math.cos(t) + h * math.sin(t)
    xl = -(h - zl * math.sin(t)) / math.cos(t)       # the thumb side of the hand
    yl = zl.cross(xl)                                # the back of the hand
    return Vector((zl.x * flip.x, zl.y, zl.z)), Vector((yl.x * flip.x, yl.y, yl.z))


def palm_position(center, fwd, up, toward_back=0.030, along_fingers=-0.026):
    """Where the palm centre goes so that the handle's centre line passes through the fist: the palm lies `toward_back` from the
    handle on the back-of-hand side and `along_fingers` behind where the fingers start to curl."""
    return Vector(center) + Vector(up) * toward_back + Vector(fwd) * along_fingers


def glove_material(mat):
    """Puts the glove textures on the Blender glove material (only for previews; Unity has its own M_Weapon_Glove)."""
    tex_dir = os.path.join(S.REPO, "Assets", "Textures", "Models")
    base_path = os.path.join(tex_dir, "T_WeaponGlove_BaseColor.png")
    nrm_path = os.path.join(tex_dir, "T_WeaponGlove_Normal.png")
    if not (os.path.exists(base_path) and os.path.exists(nrm_path)):
        return
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    base = nt.nodes.new("ShaderNodeTexImage")
    base.image = bpy.data.images.load(base_path)
    nrm = nt.nodes.new("ShaderNodeTexImage")
    nrm.image = bpy.data.images.load(nrm_path)
    nrm.image.colorspace_settings.name = 'Non-Color'
    nmap = nt.nodes.new("ShaderNodeNormalMap")
    nt.links.new(base.outputs["Color"], bsdf.inputs["Base Color"])
    nt.links.new(nrm.outputs["Color"], nmap.inputs["Color"])
    nt.links.new(nmap.outputs["Normal"], bsdf.inputs["Normal"])
    bsdf.inputs["Roughness"].default_value = 0.5


def tile_uvs(ob, factor=9.0):
    """The glove texture is a seamless tile of about 4 cm of fabric, so repeat it `factor` times over the unwrapped island layout
    (smart_project fits each object into 0..1, which would stretch one tile over a whole hand)."""
    uv = ob.data.uv_layers.active
    for loop in uv.data:
        loop.uv = (loop.uv[0] * factor, loop.uv[1] * factor)
