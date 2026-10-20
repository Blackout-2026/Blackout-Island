"""The uniform details of the realistic Commander: open collar, V-neck, placket and buttons, shoulder boards, ribbon rack, name tag,
pocket, and the white cap worn on her head, plus the pistol she aims (step C10b). Owner: Core Developer.

build(B) is called by build_commander.py with its own module as B. Unity metres, front +Z, left = -X. Everything that lies on the shirt is
snapped to the torso surface function (B.torso_z), so it follows the bust. The insignia are generic (three gold stripes and a star); the
name tag says COMMANDER, not a name.
"""

import math

from mathutils import Matrix, Vector

import commander_atlas as A


def V(x, y, z):
    return Vector((x, y, z))


def build_collar(B):
    for u in (-1, 1):
        a_pts = [V(u * 0.004, 1.572, -0.060), V(u * 0.040, 1.570, -0.042), V(u * 0.062, 1.562, -0.008), V(u * 0.062, 1.536, 0.040),
                 V(u * 0.034, 1.482, 0.088), V(u * 0.016, 1.436, 0.112)]
        b_pts = [V(u * 0.004, 1.514, -0.088), V(u * 0.060, 1.511, -0.074), V(u * 0.104, 1.505, -0.020), V(u * 0.104, 1.482, 0.040),
                 V(u * 0.082, 1.440, 0.100), V(u * 0.058, 1.402, 0.118)]
        a = B.catmull(a_pts, 3)
        b = B.catmull(b_pts, 3)
        grid = []
        for pa, pb in zip(a, b):
            grid.append([pa, pa.lerp(pb, 0.45) + V(0.0, 0.0045, 0.0), pb])
        B.shell("Chest", B.UNIFORM, B.swatch("white"), grid, 0.0032, lambda p: V(0.0, 1.43, -0.01), label="collar")
    # the skin in the V of the open collar, lying on the shirt and rising to meet the neck
    rows = []
    for k in range(9):
        y = 1.556 - 0.124 * k / 8.0
        w = max(0.003, 0.050 * (y - 1.432) / 0.124)
        row = []
        for x in (-w, -w * 0.5, 0.0, w * 0.5, w):
            zs = B.torso_z(min(y, 1.50), x, 0.0016)
            t = min(max((y - 1.50) / 0.05, 0.0), 1.0)
            zn = -0.016 + 0.054 * math.sqrt(max(0.0, 1.0 - (x / 0.058) ** 2))      # the front of the neck
            row.append(V(x, y, zs + (zn - zs) * t))
        rows.append(row)
    B.grid_surface("Chest", B.SKIN, B.skin_tile(V(0.0, 1.47, 0.0), 0.16), rows, B.torso_hint, label="V neck")


def build_placket(B):
    rows = []
    for k in range(13):
        y = 1.432 - (1.432 - 1.075) * k / 12.0
        rows.append([V(x, y, B.torso_z(y, x, 0.0014)) for x in (-0.011, 0.0, 0.011)])
    B.grid_surface("Chest", B.UNIFORM, B.swatch("shade"), rows, B.torso_hint, cut=[("Hips", -9.0, 1.08), ("Spine", 1.08, 1.28), ("Chest", 1.28, 9.0)],
                   label="placket")
    for y in (1.405, 1.355, 1.305, 1.255, 1.205, 1.155, 1.115):
        z0 = B.torso_z(y, 0.0, 0.0018)
        B.sweep("Chest", B.UNIFORM, B.swatch("shade"), [V(0.0, y, z0), V(0.0, y, z0 + 0.0026)], [(0.0052, 0.0052), (0.0046, 0.0046)], sides=8,
                ref=(0.0, 1.0, 0.0), cut=[("Hips", -9.0, 1.08), ("Spine", 1.08, 1.28), ("Chest", 1.28, 9.0)], label="button")


def build_boards(B):
    ang = math.radians(8.0)
    for u in (-1, 1):
        n = V(u * math.sin(ang), math.cos(ang), 0.22).normalized()
        along = V(u * math.cos(ang), -math.sin(ang), 0.0)
        along = (along - n * along.dot(n)).normalized()
        across = n.cross(along)
        across = across if across.z > 0 else -across
        centre = V(u * 0.160, 1.514, 0.004)
        thick = 0.0065
        B.plate("Chest", B.UNIFORM, B.swatch("white"), centre, (along, across, n), (0.106, 0.054, thick), label="board")
        B.decal("Chest", B.UNIFORM, A.UNIFORM_DECALS["board"], "uniform", centre + n * (thick / 2.0 + 0.0007), along, across, 0.106, 0.054,
                outward=lambda p, n=n: n, label="board face")


def build_insignia(B):
    # ribbon rack and warfare pin on the left chest, name tag and pocket on the right chest
    x, y = -0.082, 1.374
    B.decal("Chest", B.UNIFORM, A.UNIFORM_DECALS["ribbons"], "uniform", V(x, y, 0.0), V(1, 0, 0), V(0, 1, 0), 0.108, 0.032, nx=10, ny=2,
            snap=B.snap_torso(0.0045), outward=B.torso_hint, label="ribbons")
    B.decal("Chest", B.UNIFORM, A.UNIFORM_DECALS["pin"], "uniform", V(x, 1.4045, 0.0), V(1, 0, 0), V(0, 1, 0), 0.030, 0.030, nx=4, ny=2,
            snap=B.snap_torso(0.0045), outward=B.torso_hint, label="pin")
    # the pocket: a patch with a flap, on the right chest
    pocket = []
    for j in range(5):
        yy = 1.286 + 0.092 * j / 4.0
        pocket.append([V(xx, yy, B.torso_z(yy, xx, 0.0030)) for xx in (0.032, 0.054, 0.077, 0.100, 0.122)])
    B.shell("Chest", B.UNIFORM, B.swatch("white"), pocket, 0.0024, lambda p: V(0.0, p.y, 0.0), label="pocket")
    flap = []
    for j in range(3):
        yy = 1.346 + 0.034 * j / 2.0
        flap.append([V(xx, yy, B.torso_z(yy, xx, 0.0062)) for xx in (0.030, 0.054, 0.077, 0.100, 0.124)])
    B.shell("Chest", B.UNIFORM, B.swatch("shade"), flap, 0.0022, lambda p: V(0.0, p.y, 0.0), label="pocket flap")
    B.decal("Chest", B.UNIFORM, A.UNIFORM_DECALS["nametag"], "uniform", V(0.077, 1.3565, 0.0), V(1, 0, 0), V(0, 1, 0), 0.064, 0.0213, nx=8, ny=1,
            snap=B.snap_torso(0.0090), outward=B.torso_hint, label="name tag")


def build_cap(B):
    """The white combination cap: a white crown with a flared top, a black band, a black visor with gold leaves and a gold device. It is
    modelled in its own frame (crown axis +Y, visor toward +Z) and then laid on its side against the left hip, the crown outward."""
    start = len(B.PARTS)
    white, black, gold = B.swatch("white"), B.swatch("black"), B.swatch("gold")
    keys = [(0.000, 0.103, 0.116), (0.040, 0.105, 0.118), (0.056, 0.113, 0.126), (0.070, 0.128, 0.140), (0.080, 0.137, 0.148), (0.087, 0.136, 0.147),
            (0.092, 0.120, 0.130), (0.094, 0.060, 0.065), (0.0945, 0.0, 0.0)]
    ys = [k[0] for k in keys]
    path = [V(0.0, y, 0.0) for y in ys]
    B.sweep("Head", B.UNIFORM, white, path, [(k[1], k[2]) for k in keys], sides=24, power=2.0, cap0=True, cap1=True, label="cap crown")
    B.sweep("Head", B.UNIFORM, black, [V(0.0, 0.0, 0.0), V(0.0, 0.020, 0.0), V(0.0, 0.046, 0.0)],
            [(0.1075, 0.1195), (0.1085, 0.1205), (0.1085, 0.1205)], sides=24, label="cap band")
    # the visor: a curved plate under the front of the band
    grid = []
    for i in range(5):
        r = 0.121 + 0.058 * i / 4.0
        row = []
        for j in range(9):
            th = math.radians(-62.0 + 124.0 * j / 8.0)
            rr = r * (1.0 - 0.10 * abs(th) / 1.1)
            row.append(V(rr * math.sin(th) * 0.95, 0.002 - 0.030 * (i / 4.0) ** 1.4, rr * math.cos(th)))
        grid.append(row)
    B.shell("Head", B.UNIFORM, black, grid, 0.003, lambda p: V(0.0, 0.05, 0.0), label="visor")
    top = []
    for row in grid:
        top.append([p + V(0.0, 0.0009, 0.0) for p in row])

    def vuv(p, n, a, b):
        return A.rect_uv(A.UNIFORM_DECALS["visor"], A.SIZE["uniform"], 0.04 + 0.92 * b, 0.96 - 0.92 * a)

    B.grid_surface("Head", B.UNIFORM, vuv, top, lambda p: V(0.0, 1.0, 0.0), smooth=False, label="visor leaves")
    B.decal("Head", B.UNIFORM, A.UNIFORM_DECALS["device"], "uniform", V(0.0, 0.026, 0.1215), V(1, 0, 0), V(0, 1, 0), 0.040, 0.040,
            outward=lambda p: V(0.0, 0.0, 1.0), label="cap device")
    # on her head: the band just above the brows, the front tipped a little down
    th = math.radians(7.0)
    y_ax = V(0.0, math.cos(th), math.sin(th))
    z_ax = V(0.0, -math.sin(th), math.cos(th))
    x_ax = y_ax.cross(z_ax)
    basis = Matrix((x_ax, y_ax, z_ax)).transposed()
    place = Matrix.Translation(V(0.0, 1.782, -0.012)) @ basis.to_4x4() @ Matrix.Scale(0.95, 4)
    B.transform_parts(B.PARTS[start:], place)


def build_pistol(B):
    """A compact pistol held in both hands in front of the chest: slide, frame, tilted grip, trigger guard, magazine base and sights. Its
    centre line is B.PISTOL_X and its bore B.BORE_Y; the slide's rear is at the Gun socket's z (0.20) and the muzzle 0.22 m ahead of it."""
    x, by = B.PISTOL_X, B.BORE_Y
    black, grey, silver = B.swatch("black"), B.swatch("watch"), B.swatch("silver")
    ident = Matrix.Identity(3)
    lean = Matrix.Rotation(math.radians(14.0), 3, "X")                  # the grip leans back at the bottom
    B.box("Gun", B.UNIFORM, black, V(x, by + 0.014, 0.3075), (0.026, 0.034, 0.215), label="slide")
    B.box("Gun", B.UNIFORM, black, V(x, by - 0.010, 0.290), (0.022, 0.018, 0.180), label="frame")
    B.box("Gun", B.UNIFORM, grey, V(x, by - 0.0735, 0.232), (0.030, 0.100, 0.044), rot=lean, label="grip")
    B.box("Gun", B.UNIFORM, black, V(x, by - 0.1235, 0.2215), (0.033, 0.008, 0.050), rot=lean, label="magazine base")
    B.box("Gun", B.UNIFORM, black, V(x, by - 0.0285, 0.300), (0.014, 0.004, 0.062), label="trigger guard")
    B.box("Gun", B.UNIFORM, black, V(x, by - 0.0215, 0.329), (0.014, 0.020, 0.004), label="trigger guard front")
    B.box("Gun", B.UNIFORM, black, V(x, by - 0.0215, 0.292), (0.006, 0.020, 0.006), label="trigger")
    B.box("Gun", B.UNIFORM, silver, V(x, by + 0.0135, 0.4195), (0.012, 0.012, 0.007), label="barrel tip")
    B.box("Gun", B.UNIFORM, black, V(x, by + 0.0345, 0.404), (0.005, 0.007, 0.007), label="front sight")
    for dx in (-0.007, 0.007):
        B.box("Gun", B.UNIFORM, black, V(x + dx, by + 0.0345, 0.215), (0.005, 0.007, 0.008), label="rear sight")


def build(B):
    build_collar(B)
    build_placket(B)
    build_boards(B)
    build_insignia(B)
    build_cap(B)
    build_pistol(B)
