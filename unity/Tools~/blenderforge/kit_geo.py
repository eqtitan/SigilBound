"""Mesh builders for the modelled kit pieces (kit_head.py, kit_acc.py): surfaces of revolution, swept
tubes and ribbons, extruded outlines, bands fitted to the dressed body by ray casting. Everything is
built directly in world space in the rest pose (+Z up, the hero faces -Y, hero's left is +X), so a
piece needs no transform before it is bound to its bone."""
import bpy, bmesh, math, re
from mathutils import Vector, Matrix

TAU = 2 * math.pi
FRONT = -math.pi / 2          # angle of the face direction (-Y) in the XY plane


def _obj(name, bm, mat):
    me = bpy.data.meshes.new(name); bm.normal_update(); bm.to_mesh(me); bm.free()
    o = bpy.data.objects.new(name, me); bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    for p in o.data.polygons: p.use_smooth = True
    return o


def join(objs, name="Kit_Piece"):
    objs = [o for o in objs if o is not None]
    if not objs: return None
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = name
    return o


def lathe(profile, mat, center=(0, 0, 0), rx=1.0, ry=None, segs=28, a0=0.0, a1=TAU, M=None, zfun=None, rfun=None, close=True, name="Kit_Piece"):
    """Surface of revolution of profile [(r, z), ...] about local Z through center. x = cos(a)*r*rx,
    y = sin(a)*r*ry (an ellipse for heads and waists). M (3x3) orients local axes in the world.
    zfun(a, r, z) -> dz and rfun(a, r, z) -> radius factor deform per vertex (brims, tilted bands).
    A partial range a0..a1 leaves an open seam (hoods, veils)."""
    ry = rx if ry is None else ry
    full = abs((a1 - a0) - TAU) < 1e-6
    n = segs if full else segs + 1
    M = M or Matrix.Identity(3); c = Vector(center)
    bm = bmesh.new(); rings = []
    for r, z in profile:
        if r <= 1e-6:
            rings.append([bm.verts.new(c + M @ Vector((0, 0, z)))]); continue
        ring = []
        for i in range(n):
            a = a0 + (a1 - a0) * i / (segs if full else segs)
            rr = r * (rfun(a, r, z) if rfun else 1.0); zz = z + (zfun(a, r, z) if zfun else 0.0)
            ring.append(bm.verts.new(c + M @ Vector((math.cos(a) * rr * rx, math.sin(a) * rr * ry, zz))))
        rings.append(ring)
    for A, B in zip(rings, rings[1:]):
        if len(A) == 1 and len(B) == 1: continue
        if len(A) == 1:
            for i in range(len(B) - (0 if full else 1)): bm.faces.new((A[0], B[(i + 1) % len(B)], B[i]))
        elif len(B) == 1:
            for i in range(len(A) - (0 if full else 1)): bm.faces.new((A[i], A[(i + 1) % len(A)], B[0]))
        else:
            for i in range(len(A) - (0 if full else 1)):
                j = (i + 1) % len(A); bm.faces.new((A[i], A[j], B[j], B[i]))
    return _obj(name, bm, mat)


def _frames(pts, side=None):
    T = []
    for i in range(len(pts)):
        a = pts[max(0, i - 1)]; b = pts[min(len(pts) - 1, i + 1)]
        t = (b - a); T.append(t.normalized() if t.length > 1e-9 else Vector((0, 0, 1)))
    if side is not None:
        N = []
        for t in T:
            s = Vector(side) - t * Vector(side).dot(t)
            N.append(s.normalized() if s.length > 1e-6 else t.orthogonal().normalized())
        return T, N
    n = T[0].orthogonal().normalized(); N = [n]
    for i in range(1, len(T)):
        q = T[i - 1].rotation_difference(T[i]); n = (q @ n); n = (n - T[i] * n.dot(T[i])).normalized(); N.append(n)
    return T, N


def sweep(pts, radii, mat, segs=10, side=None, flat=1.0, cap=True, twist=0.0, name="Kit_Piece", ridge=0.0, ridges=0):
    """A tube along pts (Vectors). radii: one number or one per point, each a number or (r_side, r_other).
    side: the direction of the cross-section's first axis (a ribbon or ear lies flat across it);
    flat squashes the other axis. ridge/ridges: a ridged surface (horns): radius varies along the length."""
    pts = [Vector(p) for p in pts]
    if not isinstance(radii, (list, tuple)) or (len(radii) == 2 and len(pts) != 2 and not isinstance(radii[0], (list, tuple))):
        radii = [radii] * len(pts)
    T, N = _frames(pts, side)
    bm = bmesh.new(); rings = []
    L = len(pts)
    for i, (p, t, n) in enumerate(zip(pts, T, N)):
        b = t.cross(n).normalized()
        r = radii[i]; ra, rb = (r if isinstance(r, (list, tuple)) else (r, r * flat))
        if ridges: k = 1 + ridge * math.sin(i / max(1, L - 1) * ridges * TAU); ra *= k; rb *= k
        if ra <= 1e-6 and rb <= 1e-6:
            rings.append([bm.verts.new(p)]); continue
        ring = []
        for j in range(segs):
            a = j / segs * TAU + twist * i
            ring.append(bm.verts.new(p + n * math.cos(a) * ra + b * math.sin(a) * rb))
        rings.append(ring)
    for A, B in zip(rings, rings[1:]):
        if len(A) == 1 and len(B) == 1: continue
        if len(A) == 1:
            for j in range(len(B)): bm.faces.new((A[0], B[(j + 1) % len(B)], B[j]))
        elif len(B) == 1:
            for j in range(len(A)): bm.faces.new((A[j], A[(j + 1) % len(A)], B[0]))
        else:
            for j in range(len(A)): bm.faces.new((A[j], A[(j + 1) % len(A)], B[(j + 1) % len(B)], B[j]))
    if cap:
        for R in (rings[0], rings[-1]):
            if len(R) > 2:
                f = bm.faces.new(R if R is rings[-1] else list(reversed(R)))
    o = _obj(name, bm, mat)
    return o


def extrude(outline, depth, mat, origin, U, V, W=None, bevel=0.0, name="Kit_Piece"):
    """A flat shape: outline [(u, v), ...] in the plane through origin spanned by U, V (world vectors),
    given thickness `depth` along W (default U x V), centred on the plane."""
    U = Vector(U).normalized(); V = Vector(V).normalized(); W = Vector(W).normalized() if W is not None else U.cross(V).normalized()
    o0 = Vector(origin); bm = bmesh.new()
    lo = [bm.verts.new(o0 + U * u + V * v - W * depth / 2) for u, v in outline]
    hi = [bm.verts.new(o0 + U * u + V * v + W * depth / 2) for u, v in outline]
    bm.faces.new(list(reversed(lo))); bm.faces.new(hi)
    n = len(outline)
    for i in range(n):
        j = (i + 1) % n; bm.faces.new((lo[i], lo[j], hi[j], hi[i]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect="EDGES")
    o = _obj(name, bm, mat)
    for p in o.data.polygons: p.use_smooth = False
    return o


def box(center, size, mat, bevel=0.006, M=None, name="Kit_Piece"):
    """A bevelled block: size (x, y, z) full extents, oriented by M (3x3)."""
    M = M or Matrix.Identity(3); c = Vector(center); bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = c + M @ Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0: bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=2, affect="EDGES")
    o = _obj(name, bm, mat)
    return o


def sphere(center, r, mat, segs=14, scale=(1, 1, 1), M=None, name="Kit_Piece"):
    prof = [(math.sin(math.pi * i / 8) * 1.0, -math.cos(math.pi * i / 8)) for i in range(9)]
    prof = [(a, b) for a, b in prof]
    M = (M or Matrix.Identity(3)) @ Matrix.Diagonal(Vector(scale) * r)
    return lathe(prof, mat, center=center, segs=segs, M=M, name=name)


def frame(origin_dir):
    """3x3 matrix whose local Z is origin_dir (local X/Y perpendicular)."""
    z = Vector(origin_dir).normalized(); x = z.orthogonal().normalized(); y = z.cross(x)
    return Matrix((x, y, z)).transposed()


def frame_xz(x_dir, z_hint):
    x = Vector(x_dir).normalized(); z = Vector(z_hint); z = (z - x * z.dot(x)).normalized(); y = z.cross(x)
    return Matrix((x, y, z)).transposed()


# ---------- fitting to the dressed body ----------

class Fit:
    """Ray casts against the dressed body (body_bvh) to size pieces: the surface radius around a
    vertical axis at a height, and along any ray."""
    def __init__(self, bvh):
        self.bvh = bvh

    def radius(self, cx, cy, z, a, fallback=0.1, reach=0.6):
        d = Vector((math.cos(a), math.sin(a), 0))
        o = Vector((cx, cy, z)) + d * reach
        hit = self.bvh.ray_cast(o, -d, reach)[0]
        return (Vector((hit.x, hit.y, z)) - Vector((cx, cy, z))).length if hit is not None else fallback

    def ring(self, cx, cy, z, n=32, fallback=0.1, reach=0.6, zfun=None):
        out = []
        for i in range(n):
            a = i / n * TAU
            out.append(self.radius(cx, cy, z + (zfun(a) if zfun else 0), a, fallback, reach))
        # smooth spikes (a ray through a gap between pieces lands deep inside)
        sm = []
        for i in range(n):
            vals = sorted([out[(i - 1) % n], out[i], out[(i + 1) % n]])
            sm.append(vals[1])
        return sm

    def ellipse(self, cx, cy, z, fallback=0.1, reach=0.6):
        r = self.ring(cx, cy, z, 16, fallback, reach)
        rx = (r[0] + r[8]) / 2; ry = (r[4] + r[12]) / 2
        return max(rx, 0.02), max(ry, 0.02)

    def surface(self, origin, direction, reach=0.8):
        d = Vector(direction).normalized(); o = Vector(origin) - d * reach
        hit = self.bvh.ray_cast(o, d, reach * 1.2)[0]
        return hit


def band(radii, mat, cx, cy, z, height, thick, off=0.004, flare=0.0, zfun=None, hfun=None, name="Kit_Piece"):
    """A band hugging a fitted ring of radii (one per angle, Fit.ring): inner face at r+off, outer at
    r+off+thick, from z to z+height (hfun(a) varies the height, zfun(a) tilts it, flare widens the top)."""
    n = len(radii); bm = bmesh.new(); rows = []
    for k, (rr, h_k) in enumerate(((0, 0), (thick, 0), (thick, 1), (0, 1))):
        row = []
        for i in range(n):
            a = i / n * TAU
            hh = height * (hfun(a) if hfun else 1.0)
            r = radii[i] + off + rr + (flare * h_k)
            zz = z + (zfun(a) if zfun else 0) + hh * h_k
            row.append(bm.verts.new((cx + math.cos(a) * r, cy + math.sin(a) * r, zz)))
        rows.append(row)
    for A, B in zip(rows, rows[1:] + rows[:1]):
        for i in range(n):
            j = (i + 1) % n; bm.faces.new((A[i], A[j], B[j], B[i]))
    return _obj(name, bm, mat)


def ring_point(radii, cx, cy, a, extra=0.0):
    n = len(radii); f = (a % TAU) / TAU * n; i = int(f) % n; t = f - int(f)
    r = radii[i] * (1 - t) + radii[(i + 1) % n] * t + extra
    return Vector((cx + math.cos(a) * r, cy + math.sin(a) * r, 0)), Vector((math.cos(a), math.sin(a), 0))


def bezier(p0, p1, p2, p3=None, n=12):
    p0, p1, p2 = Vector(p0), Vector(p1), Vector(p2)
    out = []
    for i in range(n + 1):
        t = i / n
        if p3 is None: out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * p1 + t * t * p2)
        else:
            p3v = Vector(p3); out.append((1 - t) ** 3 * p0 + 3 * (1 - t) ** 2 * t * p1 + 3 * (1 - t) * t * t * p2 + t ** 3 * p3v)
    return out


def taper(n, r0, r1, power=1.0):
    return [r0 + (r1 - r0) * ((i / (n - 1)) ** power) for i in range(n)]


# ---------- colours named in design text ----------

COLOURS = [
    ("gold", "#d8b04a"), ("gilded", "#d8b04a"), ("golden", "#d8b04a"), ("silver", "#c9ced6"), ("iron", "#6d7076"), ("steel", "#9aa1aa"),
    ("bronze", "#a8743a"), ("brass", "#b98a3c"), ("tin", "#a7aaae"), ("copper", "#b4673a"), ("black", "#1e1b22"), ("soot", "#26232a"),
    ("white", "#ece6da"), ("ivory", "#e9dfc6"), ("pearl", "#efe9df"), ("bone", "#e0d4b6"), ("porcelain", "#f2efe8"), ("grey", "#8a8a92"),
    ("ash", "#9a969a"), ("navy", "#22325a"), ("blue", "#3a62b8"), ("teal", "#2a8a86"), ("sea-green", "#3a9a7a"), ("green", "#3f7a3a"),
    ("moss", "#4f7a34"), ("leaf", "#4f8a3a"), ("red", "#b0302a"), ("crimson", "#9a1f2a"), ("scarlet", "#c0302a"), ("rust", "#9a4a26"),
    ("orange", "#d8742a"), ("ember", "#e0662a"), ("violet", "#7a3ab8"), ("purple", "#6a2a9a"), ("pink", "#d88aa8"), ("straw", "#d9bd6e"),
    ("reed", "#b9a463"), ("leather", "#6a4528"), ("brown", "#6a4a2e"), ("velvet", "#5a1f3a"), ("coral", "#e2725e"), ("ice", "#bfe4ff"),
    ("frost", "#cfeaff"), ("crystal", "#b9a0ff"), ("basalt", "#2c2a2a"), ("stone", "#6e6a64"), ("flour", "#efe8d8"), ("sky-blue", "#7ab8e8"),
]


def colour_in(text, default=None):
    t = text.lower(); best = None
    for name, hx in COLOURS:
        m = re.search(r"\b" + re.escape(name) + r"\b", t)
        if m and (best is None or m.start() < best[0]): best = (m.start(), hx)
    return best[1] if best else default
