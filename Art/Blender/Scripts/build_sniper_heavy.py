"""Builds SM_Weapon_SniperHeavy.fbx, a heavy anti-materiel sniper rifle (step C9). Owner: Core Developer.

    "D:\\Blender\\blender.exe" -b --factory-startup -P Art/Blender/Scripts/build_sniper_heavy.py

Style reference (the owner's photo): a .50 calibre semi-automatic rifle with a long box receiver full of round lightening
holes, a fluted heavy barrel ending in a big slotted box muzzle brake, a flat-top rail with a large variable scope, a 10-round box
magazine, a black pistol grip, an open skeletal stock with a butt plate, a right-hand charging handle and a deployed bipod. Our
own mesh, not traced, no maker's marks.

Frame: the same gun frame as the other weapons (docs/art-pipeline.md section 14): every number is a UNITY-frame metre value, the
origin is the middle of the receiver, x right, y up, +z the way the muzzle points. The barrel axis is y = 0. The muzzle brake
ends at z = 0.81, the same muzzle distance as the R-700 (WeaponDefinitions.Sniper), so the model could replace it later. The
model is the rifle only (no hands): it is a stand-alone weapon model, not yet wired into the Player prefab.

Output: Art/Blender/SM_Weapon_SniperHeavy.blend and Assets/Models/Custom/SM_Weapon_SniperHeavy.fbx. Before bake_sniper_heavy.py has
run it has the three palette slots M_Weapon_Receiver, M_Weapon_Metal and M_Weapon_Polymer; after it, ONE material
M_Weapon_SniperHeavy with baked textures. Budget (ModelBudget, SM_Weapon_): 6,000 triangles target, 9,000 maximum.
"""

import math
import os
import sys

import bmesh
import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import build_smg as S  # noqa: E402  (the shared weapon kit; importing it builds nothing)
import gen_parts as P  # noqa: E402
from weapon_kit import bevel_bm, boolean_difference  # noqa: E402

B = P.B
REPO = S.REPO
BLEND_PATH = os.path.join(REPO, "Art", "Blender", "SM_Weapon_SniperHeavy.blend")
FBX_PATH = os.path.join(REPO, "Assets", "Models", "Custom", "SM_Weapon_SniperHeavy.fbx")
S.BLEND_PATH = BLEND_PATH  # bake_smg saves and exports to these paths
S.FBX_PATH = FBX_PATH

REC, METAL, POLY = 0, 1, 2  # parkerised grey receiver, blackened steel, black polymer
SLOTS = ["M_Weapon_Receiver", "M_Weapon_Metal", "M_Weapon_Polymer"]
S.COLOURS["M_Weapon_Receiver"] = ((0.05, 0.052, 0.05, 1), 0.7, 0.5)

G = "SniperHeavy"
MUZZLE_Z = 0.81
SCOPE_Y = 0.090  # the scope axis


def cyl_bm(c, axis, r, length, segs=14):
    return S.cyl_bm(c, axis, r, length, segs)


def prism_z(pts, z0, z1):
    """A cross-section of (x, y) points extruded along the barrel from z0 to z1 (the fluted barrel)."""
    bm = bmesh.new()
    a = [bm.verts.new(B((x, y, z0))) for x, y in pts]
    b = [bm.verts.new(B((x, y, z1))) for x, y in pts]
    n = len(pts)
    for i in range(n):
        bm.faces.new((a[i], a[(i + 1) % n], b[(i + 1) % n], b[i]))
    bm.faces.new(a)
    bm.faces.new(list(reversed(b)))
    return bm


def rrect_xy(w, h, r, cs=3):
    """Rounded-rectangle cross-section (x, y) points, centred on the barrel axis: the receiver and its soft corners."""
    pts = []
    for ox, oy, a0 in ((w / 2 - r, h / 2 - r, 0.0), (-(w / 2 - r), h / 2 - r, 90.0), (-(w / 2 - r), -(h / 2 - r), 180.0), (w / 2 - r, -(h / 2 - r), 270.0)):
        for k in range(cs + 1):
            a = math.radians(a0 + 90.0 * k / cs)
            pts.append((ox + r * math.cos(a), oy + r * math.sin(a)))
    return pts


def fluted(r_out, r_in, flutes=6):
    """Six lands and six grooves round the barrel: 4 points per flute (land, land end, groove, groove end)."""
    pts = []
    sector = 2.0 * math.pi / flutes
    for k in range(flutes):
        a0 = k * sector
        for frac, r in ((0.0, r_out), (0.42, r_out), (0.52, r_in), (0.94, r_in)):
            a = a0 + frac * sector
            pts.append((r * math.cos(a), r * math.sin(a)))
    return pts


# --------------------------------------------------------------------------- receiver, rail, barrel, brake

def build_receiver():
    # a long box receiver, z -0.48 to 0.22, with a row of round lightening holes through the front section (two rows, both sides)
    holes = [cyl_bm((0.0, 0.019, 0.026 + 0.0265 * i), 'x', 0.0112, 0.12, 12) for i in range(7)]
    holes += [cyl_bm((0.0, -0.0185, 0.039 + 0.0265 * i), 'x', 0.0080, 0.12, 10) for i in range(6)]
    holes.append(S.box_bm((0.034, 0.012, -0.17), (0.030, 0.030, 0.090)))           # ejection port on the right
    base = prism_z(rrect_xy(0.072, 0.094, 0.017), -0.48, 0.22)   # soft corners, like the photo's rolled receiver
    S._store(G, boolean_difference(base, holes), REC, 0.0)
    S.box(G, METAL, (0.028, 0.012, -0.17), (0.004, 0.020, 0.050))                  # the bolt face inside the port
    # flat-top rail with cross teeth
    S.box(G, REC, (0.0, 0.0495, -0.07), (0.032, 0.007, 0.58), bevel=0.0012)
    for i in range(19):
        S.box(G, REC, (0.0, 0.0555, -0.345 + 0.0295 * i), (0.040, 0.0035, 0.0115))
    # rear flip sight, charging handle (right), receiver screws
    S.box(G, METAL, (0.0, 0.066, 0.125), (0.012, 0.020, 0.012), bevel=0.0008)
    S.box(G, METAL, (0.056, 0.020, -0.215), (0.050, 0.010, 0.020), bevel=0.0012)
    S.cyl(G, POLY, (0.087, 0.020, -0.215), 'x', 0.0115, 0.020, segs=14, bevel=0.0015)
    for z in (-0.40, -0.30, 0.0, 0.215):
        for sx in (-1, 1):
            S.cyl(G, METAL, (sx * 0.0355, -0.030, z), 'x', 0.0045, 0.004, segs=8)


def build_barrel():
    S._store(G, prism_z(fluted(0.0195, 0.0163), 0.215, 0.69), METAL, 0.0)
    S.cyl(G, METAL, (0.0, 0.0, 0.230), 'z', 0.0270, 0.030, segs=16, bevel=0.002)    # barrel collar
    # muzzle brake: a wide box with two side windows, a baffle between them, and a bore
    brake = S.box_bm((0.0, 0.0, 0.745), (0.084, 0.060, 0.130))
    bevel_bm(brake, 0.009, 2)
    cutters = [S.box_bm((sx * 0.037, 0.0, z), (0.030, 0.036, 0.034)) for sx in (-1, 1) for z in (0.716, 0.774)]
    cutters.append(cyl_bm((0.0, 0.0, 0.745), 'z', 0.0098, 0.14, 14))
    S._store(G, boolean_difference(brake, cutters), METAL, 0.0)


# --------------------------------------------------------------------------- scope

def build_scope():
    y = SCOPE_Y
    S.cyl(G, METAL, (0.0, y, -0.02), 'z', 0.0155, 0.300, segs=16)                    # main tube
    S.cyl(G, METAL, (0.0, y, 0.150), 'z', 0.0250, 0.045, segs=16)                    # the tube swells into the objective bell
    S.cyl(G, METAL, (0.0, y, 0.2025), 'z', 0.0370, 0.060, segs=18, bevel=0.0008)
    shade = boolean_difference(cyl_bm((0.0, y, 0.2575), 'z', 0.0385, 0.050, 18), [cyl_bm((0.0, y, 0.2575), 'z', 0.0360, 0.06, 18)])
    S._store(G, shade, METAL, 0.0)                                                    # sun shade
    S.cyl(G, POLY, (0.0, y, 0.2300), 'z', 0.0350, 0.002, segs=18)                    # objective glass
    S.cyl(G, METAL, (0.0, y, -0.190), 'z', 0.0185, 0.040, segs=16)                   # eyepiece
    S.cyl(G, METAL, (0.0, y, -0.218), 'z', 0.0240, 0.020, segs=16)
    S.cyl(G, POLY, (0.0, y, -0.2285), 'z', 0.0190, 0.002, segs=16)                   # eyepiece glass
    # turrets: elevation on top (with a dial ring), windage on the right, parallax on the left
    S.cyl(G, METAL, (0.0, y + 0.0255, -0.02), 'y', 0.0115, 0.022, segs=16)
    S.cyl(G, METAL, (0.0, y + 0.0375, -0.02), 'y', 0.0095, 0.012, segs=16)
    S.cyl(G, METAL, (0.0255, y, -0.02), 'x', 0.0115, 0.022, segs=16)
    S.cyl(G, METAL, (0.0375, y, -0.02), 'x', 0.0095, 0.012, segs=16)
    S.cyl(G, METAL, (-0.0390, y, 0.130), 'x', 0.0120, 0.022, segs=16)
    S.cyl(G, METAL, (-0.0515, y, 0.130), 'x', 0.0125, 0.004, segs=16)
    # two rings on the rail with clamp screws
    for z in (-0.10, 0.07):
        ring = boolean_difference(cyl_bm((0.0, y, z), 'z', 0.0235, 0.022, 16), [cyl_bm((0.0, y, z), 'z', 0.0153, 0.04, 16)])
        S._store(G, ring, METAL, 0.0)
        S.box(G, METAL, (0.0, 0.0675, z), (0.040, 0.022, 0.026), bevel=0.0015)
        for sy in (-0.012, 0.012):
            S.cyl(G, METAL, (0.0248, y + sy, z), 'x', 0.0040, 0.008, segs=8)


# --------------------------------------------------------------------------- magazine, grip, skeletal stock

def build_lower():
    # 10-round box magazine with side ribs and a floor plate
    S.box(G, POLY, (0.0, -0.133, 0.02), (0.056, 0.176, 0.118), bevel=0.008, seg=2)
    S.box(G, METAL, (0.0, -0.2245, 0.02), (0.060, 0.008, 0.122), bevel=0.003, seg=2)
    for i in range(5):
        for sx in (-1, 1):
            S.box(G, METAL, (sx * 0.0285, -0.075 - 0.027 * i, 0.02), (0.003, 0.008, 0.095))
    # pistol grip, trigger guard and trigger
    S.prism(G, POLY, [(-0.190, -0.040), (-0.135, -0.040), (-0.118, -0.178), (-0.188, -0.178)], -0.019, 0.019, bevel=0.006, seg=2)
    S.tube(G, METAL, [(0.0, -0.046, -0.085), (0.0, -0.078, -0.092), (0.0, -0.088, -0.118), (0.0, -0.082, -0.140), (0.0, -0.046, -0.148)], 0.0035, 6)
    S.prism(G, METAL, [(-0.108, -0.046), (-0.101, -0.046), (-0.099, -0.064), (-0.106, -0.072), (-0.112, -0.068)], -0.003, 0.003, bevel=0.0008)
    # open skeletal stock, like the photo: two sloping side plates (each with a lightening slot) that drop from the receiver to a
    # hooked butt, a bridge on top, a cross bar and a black rubber pad across the back
    plate_pts = [(-0.46, 0.046), (-0.55, 0.040), (-0.605, 0.018), (-0.634, -0.010), (-0.636, -0.150), (-0.606, -0.160),
                 (-0.588, -0.070), (-0.40, -0.076), (-0.21, -0.076), (-0.21, -0.040), (-0.46, -0.040)]
    for sx in (-1, 1):
        plate = S.prism_bm(plate_pts, sx * 0.024 - 0.006, sx * 0.024 + 0.006)
        bevel_bm(plate, 0.004, 2)
        S._store(G, boolean_difference(plate, [S.box_bm((sx * 0.024, -0.012, -0.505), (0.030, 0.034, 0.110)),
                                               S.box_bm((sx * 0.024, -0.010, -0.345), (0.030, 0.030, 0.150))]), REC, 0.0)
    S.box(G, REC, (0.0, 0.043, -0.535), (0.048, 0.010, 0.150), bevel=0.003)
    S.box(G, REC, (0.0, -0.152, -0.619), (0.048, 0.012, 0.016), bevel=0.003)
    S.box(G, POLY, (0.0, -0.075, -0.646), (0.060, 0.150, 0.014), bevel=0.006, seg=2)
    S.box(G, POLY, (0.0, 0.058, -0.440), (0.048, 0.024, 0.140), bevel=0.008, seg=2)             # cheek rest


def build_bipod():
    # a clamp round the barrel, a hinge block, and two telescoping legs (thick upper part, thin lower part) with foot pads
    clamp = boolean_difference(cyl_bm((0.0, 0.0, 0.265), 'z', 0.0290, 0.034, 16), [cyl_bm((0.0, 0.0, 0.265), 'z', 0.0195, 0.05, 16)])
    S._store(G, clamp, METAL, 0.0)
    S.box(G, METAL, (0.0, -0.040, 0.265), (0.040, 0.030, 0.040), bevel=0.003)
    S.box(G, METAL, (0.0, -0.062, 0.265), (0.080, 0.016, 0.030), bevel=0.002)
    for sx in (-1, 1):
        p0 = Vector((sx * 0.036, -0.066, 0.265))
        foot = Vector((sx * 0.125, -0.320, 0.345))
        mid = p0 + (foot - p0) * 0.45
        S.tube(G, METAL, [tuple(p0), tuple(mid)], 0.0108, 6)
        S.tube(G, METAL, [tuple(mid - (foot - p0).normalized() * 0.01), tuple(foot)], 0.0078, 6)
        S.box(G, METAL, (foot.x, foot.y, foot.z), (0.034, 0.010, 0.056), bevel=0.002)


def build_all():
    S.OBJECTS.clear()
    build_receiver()
    build_barrel()
    build_scope()
    build_lower()
    build_bipod()


# --------------------------------------------------------------------------- scene, materials, export

def unity_bounds(ob):
    lo, hi = Vector((1e9,) * 3), Vector((-1e9,) * 3)
    for v in ob.data.vertices:
        u = S._to_unity(ob.matrix_world @ v.co)
        for i in range(3):
            lo[i] = min(lo[i], u[i])
            hi[i] = max(hi[i], u[i])
    return lo, hi


def main():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    unit = bpy.context.scene.unit_settings
    unit.system, unit.scale_length, unit.length_unit = 'METRIC', 1.0, 'METERS'
    build_all()
    mats = [S.make_material(n) for n in SLOTS]
    ob = S.create_object(G, mats, 40.0)
    S.unwrap(ob)
    bpy.context.view_layer.update()
    tris = S.triangles(ob)
    lo, hi = unity_bounds(ob)
    size = hi - lo
    print("=" * 70)
    print(f"[SniperHeavy] {tris} triangles (target 6000, max 9000), materials {[m.name for m in ob.data.materials]}")
    print(f"[SniperHeavy] size {size.x:.3f} x {size.y:.3f} x {size.z:.3f} m   x {lo.x:.3f}..{hi.x:.3f}  y {lo.y:.3f}..{hi.y:.3f}  z {lo.z:.3f}..{hi.z:.3f}  (muzzle {MUZZLE_Z})")
    os.makedirs(os.path.dirname(BLEND_PATH), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=BLEND_PATH)
    S.export_fbx(ob)
    print(f"[SniperHeavy] {BLEND_PATH}")


if __name__ == "__main__":
    import build_sniper_heavy  # run the importable copy, as build_smg.py does
    build_sniper_heavy.main()
