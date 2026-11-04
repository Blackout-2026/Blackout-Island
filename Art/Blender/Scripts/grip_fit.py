"""GN1. Seats fingers on the real gun: the grip solver used by build_winter_arms.py for the SMG.

Why: the old fit authored every finger joint by hand for the MP9's vertical foregrip. The IOER-32G has a different shape (a raked pistol grip
and a long flat handguard), so the joints are now computed from the gun's own mesh instead of typed in.

How (all in the Blender gun frame: X right, Y forward, Z up, metres; deterministic, no randomness):
  1. SLICE   Each finger lies in a plane. The mesh is cut with that plane (bmesh bisect) and only the cut points of the part being gripped are kept
             (REGION: a box, so the trigger guard or the receiver is not mistaken for the grip).
  2. OUTLINE The convex hull of those points is the grip's cross-section. Growing it by the finger's radius (round corners) gives the line the
             finger's centre follows, so the skin rests on the surface instead of inside it.
  3. WALK    The finger's three phalanges keep their real lengths (the rig's bone lengths). From the knuckle, joint 2 is the first point further
             along the outline that is exactly one phalanx length away, joint 3 is the next one, and the tip the third. A finger that cannot reach
             the outline raises an error, so a hand placed too far from its grip is caught here and not hidden.
  The thumb walks a straight line along the surface instead of an outline.
"""
import math

import bmesh
from mathutils import Vector


def cut_points(bm, axis, value, keep):
    """Vertices where the plane (coordinate `axis` = value) cuts the mesh, as Vectors, filtered by keep(point)."""
    work = bm.copy()
    normal = [0.0, 0.0, 0.0]
    origin = [0.0, 0.0, 0.0]
    normal[axis] = 1.0
    origin[axis] = value
    result = bmesh.ops.bisect_plane(work, geom=work.verts[:] + work.edges[:] + work.faces[:], plane_co=origin, plane_no=normal)
    points = [v.co.copy() for v in result["geom_cut"] if isinstance(v, bmesh.types.BMVert)]
    work.free()
    return [p for p in points if keep(p)]


def convex_hull(points):
    """Counter-clockwise convex hull of 2D points (monotone chain)."""
    pts = sorted({(round(p[0], 7), round(p[1], 7)) for p in points})
    if len(pts) < 3:
        return pts

    def cross(o, a, b):
        return (a[0] - o[0]) * (b[1] - o[1]) - (a[1] - o[1]) * (b[0] - o[0])

    lower = []
    for p in pts:
        while len(lower) >= 2 and cross(lower[-2], lower[-1], p) <= 0:
            lower.pop()
        lower.append(p)
    upper = []
    for p in reversed(pts):
        while len(upper) >= 2 and cross(upper[-2], upper[-1], p) <= 0:
            upper.pop()
        upper.append(p)
    return lower[:-1] + upper[:-1]


def grow(hull, radius, arc_steps=6):
    """The hull grown outward by `radius` with rounded corners, as a closed counter-clockwise polyline of 2D points."""
    n = len(hull)
    out = []
    for i in range(n):
        prev, here, nxt = hull[i - 1], hull[i], hull[(i + 1) % n]
        a1 = math.atan2(here[1] - prev[1], here[0] - prev[0]) - math.pi / 2   # outward normal of the edge arriving at this corner
        a2 = math.atan2(nxt[1] - here[1], nxt[0] - here[0]) - math.pi / 2     # outward normal of the edge leaving it
        while a2 < a1:
            a2 += 2 * math.pi
        steps = max(1, int(math.ceil((a2 - a1) / (math.pi / 2) * arc_steps)))
        for s in range(steps + 1):
            a = a1 + (a2 - a1) * s / steps
            out.append((here[0] + radius * math.cos(a), here[1] + radius * math.sin(a)))
    return out


def outline(bm, axis, value, keep, radius):
    """The 3D polyline (all points at coordinate `axis` = value) that a finger of this radius follows around the gripped part, and the obstacle."""
    points = cut_points(bm, axis, value, keep)
    if len(points) < 3:
        raise RuntimeError("grip_fit: the plane at axis %d = %.4f cuts nothing inside the grip region" % (axis, value))
    a_axis, b_axis = [i for i in range(3) if i != axis]
    hull = convex_hull([(p[a_axis], p[b_axis]) for p in points])
    ring = grow(hull, radius)
    polyline = []
    for a, b in ring:
        p = [0.0, 0.0, 0.0]
        p[axis], p[a_axis], p[b_axis] = value, a, b
        polyline.append(Vector(p))
    return polyline, Obstacle(hull, (a_axis, b_axis), radius * 0.8)


def line_polyline(start, direction, length, step=0.001):
    """A straight polyline for the thumb: `start` along `direction` for `length` metres."""
    d = direction.normalized()
    return [start + d * (step * i) for i in range(int(length / step) + 1)]


def polygon_distance(point, hull):
    """Distance from a 2D point to a convex counter-clockwise polygon: 0 inside, otherwise to its nearest edge."""
    inside = True
    best = float("inf")
    for i in range(len(hull)):
        a, b = hull[i], hull[(i + 1) % len(hull)]
        ex, ey = b[0] - a[0], b[1] - a[1]
        if ex * (point[1] - a[1]) - ey * (point[0] - a[0]) < 0:
            inside = False
        t = max(0.0, min(1.0, ((point[0] - a[0]) * ex + (point[1] - a[1]) * ey) / max(ex * ex + ey * ey, 1e-12)))
        best = min(best, math.hypot(point[0] - (a[0] + t * ex), point[1] - (a[1] + t * ey)))
    return 0.0 if inside else best


class Obstacle:
    """The gripped part's cross-section (a convex hull in two of the three axes), so a bone can be kept from cutting through its corners."""

    def __init__(self, hull, axes, clearance):
        self.hull, self.axes, self.clearance = hull, axes, clearance

    def blocks(self, a, b):
        """True when the straight bone from a to b comes closer to the surface than the clearance (or goes through it)."""
        for k in range(1, 10):
            p = a.lerp(b, k / 10.0)
            if polygon_distance((p[self.axes[0]], p[self.axes[1]]), self.hull) < self.clearance:
                return True
        return False


def walk(root, lengths, polyline, closed=True, obstacle=None, min_ratio=0.7):
    """Joint positions [joint2, joint3, tip] following the polyline from the point nearest the root.

    Every phalanx is its real length, except where the straight bone would cut through the grip (obstacle.blocks): the joint is then placed at the last
    point before that, and the bone is shortened, by at most 1 - min_ratio (the skin is scaled along the bone, as seat_fingers does for any fit).
    """
    n = len(polyline)
    nearest = min(range(n), key=lambda i: (polyline[i] - root).length)
    previous = root
    i = nearest
    steps = 0
    joints = []
    for length in lengths:
        found = None
        last_clear = None
        while steps < 2 * n:
            current = polyline[i % n]
            distance = (current - previous).length
            blocked = obstacle is not None and obstacle.blocks(previous, current)
            if blocked and last_clear is not None and (last_clear - previous).length >= min_ratio * length:
                found = last_clear
                break
            if not blocked:
                last_clear = current
            if distance >= length:
                before = polyline[(i - 1) % n]
                if (before - previous).length >= length:
                    near = polyline[nearest]
                    raise RuntimeError("grip_fit: the knuckle is already further than one phalanx (%.4f m) from the outline: root (x %.4f, forward %.4f, up %.4f), "
                                       "nearest outline point (%.4f, %.4f, %.4f), distance %.4f" % (
                                           length, root.x, root.y, root.z, near.x, near.y, near.z, (near - root).length))
                lo, hi = 0.0, 1.0
                for _ in range(40):
                    mid = (lo + hi) / 2
                    if (before.lerp(current, mid) - previous).length < length:
                        lo = mid
                    else:
                        hi = mid
                found = before.lerp(current, hi)
                break
            i += 1
            steps += 1
            if not closed and i >= n:
                break
        if found is None:
            raise RuntimeError("grip_fit: the outline is too short for the finger (phalanx %.4f m)" % length)
        joints.append(found)
        previous = found
        # the next bone starts from this joint; continue from the polyline index nearest it
        i = min(range(n), key=lambda k: (polyline[k] - found).length) + 1
    return joints


def first_reach(root, polyline):
    """Distance from the root to the nearest point of the polyline (for reporting, and for sanity checks)."""
    return min((p - root).length for p in polyline)
