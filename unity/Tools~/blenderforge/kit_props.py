"""Modelled hand props for the high-detail heroes (phase E steps 46-50 on the phase F bodies): weapons,
off-hand blades, shields and handheld focus items, built procedurally with bmesh so no asset download is
needed. Replaces the primitive stand-ins ubc.py used (box blades, rod + box hammers, a stick and ball for
anything unrecognised, a plain glowing ball for every focus item).

Frames (as ubc.py expects):
  * weapons / focus items: the grip at the origin, the weapon's "up" along local +Z (from the little
    finger toward the index knuckle), +X toward the palm, blades flat in the YZ plane (edges along Y).
    Things that hang from the fist (lanterns, bells, censers, flails) hang toward -Z.
  * shields: the face in the XY plane, local Y along the forearm, +Z outward (away from the arm).

classify(design) -> (weapon_type, opts) and shield_type(design) map every design to a prop; build_weapon /
build_offhand / build_shield return ONE joined mesh object (materials from the given dict plus a few
shared wood / leather / stone / string ones). Each prop stays roughly 300-2500 triangles.
"""
import bpy, bmesh, math, re
from mathutils import Vector, Matrix

TAU = 2 * math.pi

# ---------------------------------------------------------------- materials

_EXTRA = {}

def mats_for(mats):
    """The ubc.py kit materials plus shared wood/leather/stone/string/dark-iron ones (created once)."""
    import ubc_parts as P
    try:
        for v in _EXTRA.values(): v.name        # materials go away when a caller clears the scene data
    except ReferenceError:
        _EXTRA.clear()
    if not _EXTRA:
        _EXTRA["wood"] = P.flat("K_Wood", P.hexc("#6e4a2c"), rough=0.75)
        _EXTRA["darkwood"] = P.flat("K_DarkWood", P.hexc("#3e2a1c"), rough=0.8)
        _EXTRA["leather"] = P.flat("K_Leather", P.hexc("#3a281d"), rough=0.7)
        _EXTRA["stone"] = P.flat("K_PropStone", P.hexc("#6d6860"), rough=0.95)
        _EXTRA["string"] = P.flat("K_String", P.hexc("#d8ccb0"), rough=0.6)
        _EXTRA["iron"] = P.flat("K_Iron", P.hexc("#4a4c52"), metal=0.85, rough=0.45)
        _EXTRA["gold"] = P.flat("K_Gold", P.hexc("#d4a640"), metal=0.95, rough=0.3)
        _EXTRA["paper"] = P.flat("K_Paper", P.hexc("#e8dcc0"), rough=0.8)
        _EXTRA["marble"] = P.flat("K_Marble", P.hexc("#ece8e0"), rough=0.4)
        _EXTRA["glass_dark"] = P.flat("K_GlassDark", P.hexc("#1a1428"), metal=0.3, rough=0.08)
        _EXTRA["glass_pale"] = P.flat("K_GlassPale", P.hexc("#a8d4e8"), emit=0.35, metal=0.2, rough=0.08)
        # stained-glass panes: coloured and softly lit (a plain glow face rendered as a white slab)
        for k, hx in (("pane_blue", "#2a4ab8"), ("pane_red", "#b8262e"), ("pane_gold", "#e0a830"), ("pane_teal", "#1f9a8a")):
            _EXTRA[k] = P.flat("K_" + k, P.hexc(hx), emit=1.2, metal=0.1, rough=0.15)
    m = dict(_EXTRA); m.update(mats)
    return m

# ---------------------------------------------------------------- mesh helpers

def _obj(bm, mat, name="Prop", smooth=False):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me); bm.free()
    if smooth:
        for p in me.polygons: p.use_smooth = True
    me.uv_layers.new(name="UVMap")
    o = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(o)
    o.data.materials.append(mat)
    return o

def loft(rings, mat, closed_rings=True, cap0=True, cap1=True, smooth=False, closed_path=False):
    """Skin a list of rings (each a list of Vector, same length) into a mesh."""
    bm = bmesh.new()
    vr = [[bm.verts.new(p) for p in r] for r in rings]
    n = len(rings[0])
    seq = list(range(len(vr) - 1)) + ([len(vr) - 1] if closed_path else [])
    for a in seq:
        b = (a + 1) % len(vr)
        for i in range(n if closed_rings else n - 1):
            j = (i + 1) % n
            try: bm.faces.new((vr[a][i], vr[a][j], vr[b][j], vr[b][i]))
            except ValueError: pass
    if not closed_path:
        if cap0 and n > 2:
            try: bm.faces.new(list(reversed(vr[0])))
            except ValueError: pass
        if cap1 and n > 2:
            try: bm.faces.new(vr[-1])
            except ValueError: pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(bm, mat, smooth=smooth)

def lathe(profile, mat, segs=16, smooth=True, z0=0.0, sx=1.0, sy=1.0):
    """Revolve (radius, z) points (bottom to top) about Z. Zero radii become poles."""
    rings = []
    for r, z in profile:
        r = max(r, 1e-5)
        rings.append([Vector((math.cos(a) * r * sx, math.sin(a) * r * sy, z + z0)) for a in (i / segs * TAU for i in range(segs))])
    return loft(rings, mat, smooth=smooth)

def box(size, loc, mat, bevel=0.004, rot=None):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * size[0], v.co.y * size[1], v.co.z * size[2]))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=min(bevel, min(size) * 0.45), segments=1, affect="EDGES", profile=0.5)
    R = (rot or Matrix.Identity(3))
    for v in bm.verts: v.co = R @ v.co + Vector(loc)
    return _obj(bm, mat)

def _frames(pts):
    """Parallel-transport frames along a polyline."""
    T = [(pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized() for i in range(len(pts))]
    up = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0))
    N = [T[0].cross(up).normalized()]
    for i in range(1, len(pts)):
        n = N[-1] - T[i] * N[-1].dot(T[i])
        N.append(n.normalized() if n.length > 1e-6 else N[-1])
    B = [T[i].cross(N[i]).normalized() for i in range(len(pts))]
    return T, N, B

def tube(pts, radius, mat, segs=8, smooth=True, closed=False, sq=1.0):
    """A tube along a polyline; radius is a number or a list (one per point)."""
    pts = [Vector(p) for p in pts]
    rad = radius if isinstance(radius, (list, tuple)) else [radius] * len(pts)
    if closed:
        T = [(pts[(i + 1) % len(pts)] - pts[i - 1]).normalized() for i in range(len(pts))]
        ref = Vector((0, 0, 1)) if abs(T[0].z) < 0.9 else Vector((1, 0, 0))
        N = [(ref - t * ref.dot(t)).normalized() for t in T]; B = [T[i].cross(N[i]) for i in range(len(pts))]
    else:
        T, N, B = _frames(pts)
    rings = [[pts[i] + (N[i] * math.cos(a) + B[i] * math.sin(a) * sq) * rad[i] for a in (k / segs * TAU for k in range(segs))] for i in range(len(pts))]
    return loft(rings, mat, smooth=smooth, closed_path=closed)

def torus(R, r, mat, loc=(0, 0, 0), rot=None, seg=20, mseg=6, sx=1.0, sy=1.0):
    pts = [Vector((math.cos(a) * R * sx, math.sin(a) * R * sy, 0)) for a in (i / seg * TAU for i in range(seg))]
    o = tube(pts, r, mat, segs=mseg, closed=True)
    M = (rot or Matrix.Identity(3)).to_4x4(); M.translation = Vector(loc)
    o.data.transform(M)
    return o

def plate(outline, thick, mat, bulge=0.0, bevel=0.006, R=None):
    """An extruded 2D outline in XY (z from 0 to thick), domed by bulge (centre raised) for shields."""
    bm = bmesh.new()
    vs = [bm.verts.new((x, y, 0)) for x, y in outline]
    f = bm.faces.new(vs)
    ext = bmesh.ops.extrude_face_region(bm, geom=[f])
    top = [g for g in ext["geom"] if isinstance(g, bmesh.types.BMVert)]
    for v in top: v.co.z += thick
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=[e for e in bm.edges if len(e.link_faces) == 2 and abs(e.verts[0].co.z - e.verts[1].co.z) < 1e-6], offset=bevel, segments=1, affect="EDGES")
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    if bulge:
        Rm = R or max(Vector((x, y)).length for x, y in outline)
        for v in bm.verts:
            d = min(1.0, Vector((v.co.x, v.co.y)).length / Rm)
            v.co.z += bulge * (1 - d * d)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    return _obj(bm, mat)

def subdivided_plate(outline, thick, mat, bulge, cuts=4):
    """Like plate() but the face is grid-filled first so the dome curvature reads."""
    o = plate(outline, thick, mat, 0.0, bevel=0.0)
    bm = bmesh.new(); bm.from_mesh(o.data)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges, cuts=cuts, use_grid_fill=True)
    Rm = max(Vector((x, y)).length for x, y in outline)
    for v in bm.verts:
        d = min(1.0, Vector((v.co.x, v.co.y)).length / Rm)
        v.co.z += bulge * (1 - d * d)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(o.data); bm.free()
    return o

def blade(stations, mat, single=False):
    """A blade lofted through stations (z, half_width, thickness, y_offset) along +Z. Cross-section is a
    bevelled hexagon (double-edged) or a wedge with a thick spine (single-edged, edge toward -Y)."""
    rings = []
    for z, hw, t, y in stations:
        hw = max(hw, 0.0015); t = max(t, 0.0008)
        if single:
            sec = [(0, -hw), (t, -hw * 0.3), (t * 0.8, hw * 0.95), (0, hw), (-t * 0.8, hw * 0.95), (-t, -hw * 0.3)]
        else:
            sec = [(0, -hw), (t, -hw * 0.4), (t, hw * 0.4), (0, hw), (-t, hw * 0.4), (-t, -hw * 0.4)]
        rings.append([Vector((x, yy + y, z)) for x, yy in sec])
    return loft(rings, mat)

YZ = Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # plate (x, y, thickness z) -> (y, z, x)

def yz_plate(outline_y_z, thick, mat, bevel=0.002):
    """A flat piece from a (y, z) outline, thickness along X centred on the YZ plane."""
    p = plate(outline_y_z, thick, mat, bevel=bevel)
    p.data.transform(Matrix.Translation((0, 0, 0)) @ YZ @ Matrix.Translation((0, 0, -thick / 2)))
    return p

def blade2d(outline_y_z, thick, mat, edge=0.4):
    """A flat blade from a 2D outline in YZ (axe bits, glaive heads): extruded along X with chamfered
    edges (edge = fraction of thickness kept at the rim)."""
    bm = bmesh.new()
    front = [bm.verts.new((thick * 0.5, y, z)) for y, z in outline_y_z]
    back = [bm.verts.new((-thick * 0.5, y, z)) for y, z in outline_y_z]
    n = len(front)
    bm.faces.new(front); bm.faces.new(list(reversed(back)))
    for i in range(n):
        j = (i + 1) % n
        bm.faces.new((front[i], back[i], back[j], front[j]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    # pull the rim in toward the centre plane (a sharpened edge)
    cy = sum(y for y, _ in outline_y_z) / n; cz = sum(z for _, z in outline_y_z) / n
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    o = _obj(bm, mat)
    return o

def join(objs, name="Prop"):
    objs = [o for o in objs if o is not None]
    if not objs: return None
    for x in bpy.context.selected_objects: x.select_set(False)
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    if len(objs) > 1: bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = name
    return o

def xform(o, loc=(0, 0, 0), rot=None, scale=1.0):
    M = Matrix.Translation(Vector(loc)) @ (rot or Matrix.Identity(3)).to_4x4() @ Matrix.Scale(scale, 4)
    o.data.transform(M)
    return o

def rx(deg): return Matrix.Rotation(math.radians(deg), 3, "X")
def ry(deg): return Matrix.Rotation(math.radians(deg), 3, "Y")
def rz(deg): return Matrix.Rotation(math.radians(deg), 3, "Z")

# ---------------------------------------------------------------- shared parts

def grip(m, z0=-0.11, z1=0.08, r=0.017, mat=None, wrap=True):
    """A leather-wrapped grip: a lathe with ridges every 2.5 cm."""
    prof = []
    n = max(2, int((z1 - z0) / 0.025))
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        prof.append((r * (1.0 if not wrap or i % 2 == 0 else 1.12), z))
    prof = [(0, z0)] + prof + [(0, z1)]
    return lathe(prof, mat or m["leather"], segs=10)

def pommel(m, z, r=0.028, mat=None, kind="disc"):
    mat = mat or m["metal"]
    if kind == "ball":
        return lathe([(0, z - r), (r * 0.7, z - r * 0.7), (r, z), (r * 0.7, z + r * 0.7), (0, z + r)], mat, segs=12)
    return lathe([(0, z - r * 0.5), (r * 0.8, z - r * 0.45), (r, z), (r * 0.8, z + r * 0.45), (0.012, z + r * 0.55)], mat, segs=14)

def crossguard(m, z, half=0.1, mat=None, curl=0.015):
    mat = mat or m["acc"]
    pts = [Vector((0, y, z + curl * (abs(y) / half) ** 2)) for y in [(-1 + 2 * i / 8) * half for i in range(9)]]
    rad = [0.012 + 0.006 * (1 - abs(p.y) / half) for p in pts]
    a = tube(pts, rad, mat, segs=8)
    ends = [lathe([(0, -0.012), (0.014, -0.006), (0.014, 0.006), (0, 0.012)], mat, segs=8) for _ in (0, 1)]
    xform(ends[0], (0, -half, z + curl)); xform(ends[1], (0, half, z + curl))
    return [a] + ends

def shaft(m, z0, z1, r=0.018, mat=None, gnarl=0.0, segs=10, seed=1):
    mat = mat or m["wood"]
    n = max(4, int((z1 - z0) / 0.12))
    pts = []; rad = []
    for i in range(n + 1):
        z = z0 + (z1 - z0) * i / n
        dx = gnarl * math.sin(i * 1.7 + seed) * 0.6; dy = gnarl * math.cos(i * 2.3 + seed) * 0.6
        pts.append((dx, dy, z)); rad.append(r * (1 + gnarl * 3 * math.sin(i * 3.1 + seed) ** 2))
    return tube(pts, rad, mat, segs=segs)

def ferrule(m, z, r=0.02, h=0.04, mat=None):
    return lathe([(0, z - h / 2), (r, z - h / 2), (r * 1.1, z), (r, z + h / 2), (0, z + h / 2)], mat or m["metal"], segs=10, smooth=False)

def bands(m, zs, r=0.02, mat=None):
    return [torus(r, 0.005, mat or m["acc"], loc=(0, 0, z), seg=12, mseg=4) for z in zs]

def chain(m, p0, p1, sag=0.1, links=10, r=0.018, mat=None):
    """Alternating oval links from p0 to p1 sagging along -Z."""
    mat = mat or m["iron"]; out = []
    p0, p1 = Vector(p0), Vector(p1)
    for i in range(links):
        t = (i + 0.5) / links
        p = p0.lerp(p1, t) + Vector((0, 0, -sag * 4 * t * (1 - t)))
        q = p0.lerp(p1, min(1, t + 0.01)) + Vector((0, 0, -sag * 4 * (t + 0.01) * (1 - t - 0.01)))
        d = (q - p).normalized()
        R = d.to_track_quat("Y", "Z").to_matrix()
        if i % 2: R = R @ ry(90)
        out.append(torus(r, r * 0.28, mat, loc=p, rot=R @ rx(90) @ rz(0), seg=8, mseg=4, sx=0.7, sy=1.3))
    return out

def star(m, loc, r=0.06, pts=5, mat=None, thick=0.02, rot=None):
    o = [(math.sin(i / (2 * pts) * TAU) * (r if i % 2 == 0 else r * 0.45), math.cos(i / (2 * pts) * TAU) * (r if i % 2 == 0 else r * 0.45)) for i in range(2 * pts)]
    p = plate(o, thick, mat or m["glow"], bevel=0.002)
    xform(p, (0, 0, -thick / 2)); xform(p, loc, rot or ry(90))
    return p

def crystal(m, loc, h=0.16, r=0.04, mat=None, tilt=(0, 0)):
    mat = mat or m["glow"]
    c = lathe([(0, -h * 0.25), (r, 0), (r * 0.85, h * 0.6), (0, h)], mat, segs=6, smooth=False)
    xform(c, (0, 0, 0), rx(tilt[0]) @ ry(tilt[1])); xform(c, loc)
    return c

def gem(m, loc, r=0.025, mat=None):
    return xform(lathe([(0, -r), (r, 0), (0, r)], mat or m["glow"], segs=8, smooth=False), loc)

def flame(m, loc, h=0.12, r=0.03, mat=None):
    """A teardrop flame (glow)."""
    prof = [(0, 0), (r * 0.7, h * 0.12), (r, h * 0.3), (r * 0.7, h * 0.6), (r * 0.25, h * 0.85), (0, h)]
    return xform(lathe(prof, mat or m["glow"], segs=10), loc)

def leaf_head(m, z, L=0.32, W=0.05, mat=None):
    st = [(z, 0.012, 0.012, 0), (z + L * 0.15, W, 0.008, 0), (z + L * 0.45, W * 0.95, 0.007, 0), (z + L * 0.8, W * 0.45, 0.005, 0), (z + L, 0.002, 0.001, 0)]
    return blade(st, mat or m["metal"])

# ---------------------------------------------------------------- weapons

def w_sword(m, o):
    L = o.get("len", 0.85); W = o.get("width", 0.042); curve = o.get("curve", 0.0)
    single = o.get("single", False); wavy = o.get("wavy", False)
    st = []
    n = 14
    for i in range(n + 1):
        t = i / n; z = 0.1 + L * t
        hw = W * (1 - 0.15 * t) if t < 0.86 else W * (1 - 0.15 * t) * (1 - (t - 0.86) / 0.14) ** 0.8
        if wavy: hw *= 1 + 0.18 * math.sin(t * 9 * math.pi)
        y = curve * L * t * t
        st.append((z, hw, 0.009 * (1 - 0.5 * t), y))
    out = [blade(st, o.get("bmat", m["iron"] if o.get("dark") else m["metal"]), single=single)]
    if o.get("fuller", True) and not single and not wavy:
        out.append(blade([(0.14, 0.006, 0.0095, 0), (0.1 + L * 0.7, 0.005, 0.0095, 0), (0.1 + L * 0.75, 0.001, 0.0095, 0)], m["iron"]))
    hand = o.get("handle", 0.2)
    out.append(grip(m, z0=0.07 - hand, z1=0.08))
    g = o.get("guard", "cross")
    if g == "cross": out += crossguard(m, 0.09, half=o.get("guard_w", 0.1))
    elif g == "tsuba": out.append(lathe([(0, 0.08), (0.045, 0.08), (0.048, 0.09), (0.045, 0.1), (0, 0.1)], m["acc"], segs=16))
    elif g == "cup":
        out.append(lathe([(0.016, 0.06), (0.05, 0.075), (0.055, 0.1), (0.016, 0.1)], m["acc"], segs=16))
        out += crossguard(m, 0.1, half=0.08)
        out.append(tube([(0, -0.08, 0.1), (0.035, -0.07, 0.05), (0.04, -0.03, -0.06), (0, 0, -0.1)], 0.005, m["acc"], segs=6))
    elif g == "knuckle":
        out += crossguard(m, 0.09, half=0.06, curl=0)
        out.append(tube([(0, -0.06, 0.09), (0.02, -0.08, 0.02), (0.02, -0.06, -0.06), (0, -0.02, 0.07 - hand)], 0.007, m["acc"], segs=6))
    out.append(pommel(m, 0.07 - hand - 0.02, kind=o.get("pommel", "disc")))
    if o.get("glow_edge"):
        out.append(blade([(0.12, 0.004, 0.0098, -W * 0.55), (0.1 + L * 0.8, 0.003, 0.0098, -W * 0.45)], m["glow"]))
        out.append(blade([(0.12, 0.004, 0.0098, W * 0.55), (0.1 + L * 0.8, 0.003, 0.0098, W * 0.45)], m["glow"]))
    if o.get("crescent"):
        arc = [Vector((0, math.sin(a) * 0.09 - 0.0, 0.1 + L * 0.9 + math.cos(a) * 0.09)) for a in [math.radians(x) for x in range(-100, 101, 20)]]
        out.append(tube(arc, 0.012, m["metal"], segs=6))
    if o.get("thorns"):
        for k in range(6):
            z = 0.15 + k * L * 0.13; s = 1 if k % 2 else -1
            out.append(xform(lathe([(0.006, 0), (0, 0.03)], m["darkwood"], segs=5, smooth=False), (0, s * W * 0.2, z), rx(-70 * s)))
    if o.get("flames"):   # a burning blade: flame tongues licking up its edge
        for k in range(5):
            t = 0.2 + 0.15 * k; z = 0.1 + L * t
            out.append(flame(m, (0, curve * L * t * t + W * 0.5, z), h=0.07 - 0.008 * k, r=0.018))
    if o.get("cracks"):
        for k in range(4):
            z = 0.2 + k * L * 0.2
            out.append(box((0.02, W * 1.2, 0.006), (0, 0, z), m["glow"], bevel=0, rot=rx(15 * (1 if k % 2 else -1))))
    return out

def w_dagger(m, o):
    L = o.get("len", 0.26); W = o.get("width", 0.022)
    if o.get("crescent") or o.get("hook"):
        st = [(0.06 + L * t, W * (1 - 0.6 * t) * (1 if t < 0.85 else (1 - t) / 0.15), 0.006, (0.07 if o.get("hook") else 0.05) * (L / 0.26) * t * t * (3 if o.get("hook") else 1)) for t in [i / 10 for i in range(11)]]
        out = [blade(st, m["metal"], single=True)]
    else:
        out = [blade([(0.06, W, 0.007, 0), (0.06 + L * 0.6, W * 0.8, 0.006, 0), (0.06 + L, 0.002, 0.001, 0)], m["metal"])]
    out.append(grip(m, z0=-0.075, z1=0.05, r=0.014))
    out += crossguard(m, 0.055, half=0.04, curl=0.006)
    out.append(pommel(m, -0.085, r=0.018, kind="ball"))
    return out

def w_sickle(m, o):
    out = [grip(m, z0=-0.1, z1=0.09, r=0.016, mat=m["wood"]), ferrule(m, 0.1, r=0.017)]
    R = o.get("R", 0.15)
    arc = [(0.0, -R * math.sin(a) + 0.0, 0.11 + R * (1 - math.cos(a)) * 0.0 + R * math.sin(a) * 0.0 + (R * (1 - math.cos(a)))) for a in []]
    st = []
    for i in range(13):
        a = math.radians(i * 15)                          # 0..180 deg hook
        y = -R * (1 - math.cos(a)); z = 0.11 + R * math.sin(a) * 1.1
        st.append((0, y, z))
    pts = [Vector(p) for p in st]
    T, N, B = _frames(pts)
    rings = []
    for i, p in enumerate(pts):
        hw = 0.022 * (1 - i / 13); t = 0.006 * (1 - i / 14)
        out_n = Vector((0, -math.cos(math.radians(i * 15)) * 0 - 1, 0)) if False else None
        # section: edge on the inside of the curve
        inward = (Vector((0, -R, 0.11)) - p); inward.x = 0
        inward = inward.normalized() if inward.length > 1e-6 else Vector((0, -1, 0))
        side = Vector((1, 0, 0))
        rings.append([p + inward * hw, p + side * t, p - inward * hw * 0.6 + side * t * 0.6, p - inward * hw * 0.6 - side * t * 0.6, p - side * t])
    out.append(loft(rings, m["metal"]))
    if o.get("glow"): out.append(gem(m, (0, 0, 0.12), 0.015))
    if o.get("bill"):
        out[0] = shaft(m, -0.5, 0.1, r=0.017)
    return out

def w_axe(m, o):
    hl = o.get("haft", 0.7); big = o.get("big", 1.0)
    out = [shaft(m, -0.2, hl, r=0.019), grip(m, -0.2, 0.05, r=0.02)]
    zc = hl - 0.07 * big
    bit = [(-0.02, zc - 0.05 * big), (-0.1 * big, zc - 0.07 * big), (-0.17 * big, zc - 0.13 * big), (-0.2 * big, zc - 0.05 * big),
           (-0.21 * big, zc + 0.04 * big), (-0.19 * big, zc + 0.1 * big), (-0.1 * big, zc + 0.06 * big), (-0.02, zc + 0.05 * big)]
    if not o.get("bearded"): bit = [(-0.02, zc - 0.045 * big), (-0.14 * big, zc - 0.09 * big), (-0.17 * big, zc - 0.02 * big), (-0.17 * big, zc + 0.05 * big), (-0.14 * big, zc + 0.09 * big), (-0.02, zc + 0.045 * big)]
    out.append(blade2d(bit, 0.014, m["metal"]))
    out.append(blade2d([(-0.17 * big - 0.012 if not o.get("bearded") else -0.2 * big, zc - 0.03 * big), (-0.175 * big - (0.012 if not o.get("bearded") else 0.035 * big), zc), (-0.17 * big - 0.012 if not o.get("bearded") else -0.205 * big, zc + 0.04 * big)], 0.004, m["iron"]))
    out.append(box((0.04, 0.05, 0.1 * big), (0, 0, zc), m["iron"], bevel=0.006))
    if o.get("double"):
        out.append(xform(blade2d(bit, 0.014, m["metal"]), (0, 0, 0), rz(180)))
    else:
        out.append(xform(lathe([(0.018, 0), (0.0, 0.07)], m["iron"], segs=6, smooth=False), (0, 0.02, zc), rx(-90)))
    out += bands(m, [0.06, hl - 0.18 * big], r=0.021)
    out.append(pommel(m, -0.22, r=0.022, mat=m["iron"]))
    if o.get("crystal"):
        out.append(crystal(m, (0, -0.16 * big, zc), h=0.12, r=0.035, tilt=(90, 0)))
    return out

def w_pickaxe(m, o):
    out = [shaft(m, -0.25, 0.65, r=0.019), grip(m, -0.25, 0.0, r=0.02)]
    pts = [Vector((0, y, 0.6 + 0.12 * (1 - (y / 0.28) ** 2))) for y in [-0.28 + 0.56 * i / 10 for i in range(11)]]
    rad = [0.008 + 0.02 * (1 - abs(p.y) / 0.28) for p in pts]
    out.append(tube(pts, rad, m["glow"] if o.get("crystal") else m["metal"], segs=6, smooth=False))
    out.append(box((0.045, 0.06, 0.06), (0, 0, 0.66), m["iron"]))
    return out

def w_hammer(m, o):
    hl = o.get("haft", 0.65); s = o.get("big", 1.0)
    out = [shaft(m, -0.22, hl, r=0.02 * min(1.3, s)), grip(m, -0.22, 0.04, r=0.021 * min(1.3, s))]
    head = o.get("head", "block")
    zc = hl
    if head == "block":
        out.append(box((0.11 * s, 0.24 * s, 0.12 * s), (0, 0, zc), o.get("hmat", m["metal"]), bevel=0.012 * s))
        out.append(box((0.12 * s, 0.03 * s, 0.13 * s), (0, -0.09 * s, zc), m["iron"], bevel=0.006))
        out.append(box((0.12 * s, 0.03 * s, 0.13 * s), (0, 0.09 * s, zc), m["iron"], bevel=0.006))
    elif head == "smith":
        out.append(box((0.07, 0.16, 0.07), (0, -0.01, zc), m["metal"], bevel=0.01))
        out.append(xform(lathe([(0.035, 0), (0.02, 0.06), (0.012, 0.075)], m["metal"], segs=8, smooth=False), (0, 0.07, zc), rx(-90)))
        if o.get("glow"): out.append(box((0.072, 0.03, 0.072), (0, -0.09, zc), m["glow"], bevel=0.006))
    elif head == "marble_sun":
        # a white marble block with gold-capped ends and a glowing sun set into each broad face
        out.append(box((0.15 * s, 0.32 * s, 0.17 * s), (0, 0, zc), m["marble"], bevel=0.014 * s))
        for y in (-0.165, 0.165):
            out.append(box((0.165 * s, 0.025 * s, 0.185 * s), (0, y * s, zc), m["gold"], bevel=0.006))
            out.append(xform(lathe([(0.02 * s, 0), (0, 0.04 * s)], m["gold"], segs=8, smooth=False), (0, y * s * 1.18, zc), rx(90 if y < 0 else -90)))
        for x in (-1, 1):
            out.append(xform(lathe([(0, -0.006), (0.04 * s, -0.004), (0.045 * s, 0), (0.04 * s, 0.004), (0, 0.006)], m["glow"], segs=16), (x * 0.077 * s, 0, zc), ry(90)))
            for k in range(10):
                a = k / 10 * TAU
                out.append(box((0.006, 0.012 * s, 0.03 * s), (x * 0.077 * s, math.cos(a) * 0.058 * s, zc + math.sin(a) * 0.058 * s), m["glow"], bevel=0, rot=rx(math.degrees(-a))))
        out.append(box((0.17 * s, 0.03 * s, 0.03 * s), (0, 0, zc + 0.1 * s), m["gold"], bevel=0.004))
    elif head == "sun":
        out.append(xform(lathe([(0, -0.08), (0.08, -0.07), (0.09, 0), (0.08, 0.07), (0, 0.08)], m["gold"], segs=16), (0, 0, zc), rx(90)))
        for k in range(12):
            a = k / 12 * TAU
            out.append(xform(lathe([(0.018, 0), (0, 0.07)], m["glow"], segs=4, smooth=False), (0, 0, 0), rx(0)))
            out[-1].data.transform((Matrix.Translation((0, math.cos(a) * 0.085, zc + math.sin(a) * 0.085)) @ Matrix.Rotation(-a + math.pi / 2, 4, "X")))
    elif head == "bell":
        out.append(xform(lathe([(0.0, 0.0), (0.05, 0.0), (0.06, 0.03), (0.08, 0.12), (0.11, 0.17), (0.11, 0.19), (0.0, 0.19)], m["gold"], segs=18), (0, -0.09, zc), rx(-90)))
        out.append(box((0.07, 0.06, 0.07), (0, 0.02, zc), m["iron"]))
    elif head == "sack":
        out.append(xform(lathe([(0.0, -0.13), (0.07, -0.12), (0.1, -0.05), (0.1, 0.05), (0.07, 0.12), (0.02, 0.14), (0.0, 0.15)], m["paper"], segs=12), (0, 0, zc), rx(90)))
        out.append(torus(0.03, 0.008, m["string"], loc=(0, 0.12, zc), rot=rx(90), seg=10, mseg=4))
    if o.get("runes"): out.append(box((0.112 * s, 0.1 * s, 0.02), (0, 0, zc), m["glow"], bevel=0))
    if o.get("lava"):
        for k in range(3): out.append(box((0.112 * s, 0.012, 0.13 * s), (0, -0.06 + 0.06 * k, zc), m["glow"], bevel=0, rot=rx(12 * (k - 1))))
    out.append(pommel(m, -0.24, r=0.024, mat=m["iron"]))
    return out

def w_maul(m, o):
    o = dict(o); o.setdefault("big", 1.6); o.setdefault("haft", 0.85)
    return w_hammer(m, o)

def w_club(m, o):
    L = o.get("len", 0.75); k = o.get("kind", "knobbly")
    if k == "pillar":
        out = [lathe([(0.03, -0.15), (0.035, 0.0), (0.09, 0.15), (0.11, 0.25), (0.1, L), (0.12, L + 0.04), (0.0, L + 0.06)], m["stone"], segs=8, smooth=False)]
        out.append(xform(lathe([(0.0, 0.0), (0.12, 0.0), (0.0, 0.04)], m["stone"], segs=8, smooth=False), (0, 0, 0.3)))
        return out
    prof = [(0, -0.16), (0.022, -0.15), (0.022, 0.0)]
    for i in range(8):
        z = L * (i + 1) / 8
        prof.append(((0.025 + 0.055 * (z / L) ** 1.5) * (1.12 if (i % 2 and k == "knobbly") else 1.0), z))
    prof.append((0, L + 0.02))
    out = [lathe(prof, m["darkwood"] if k in ("knobbly", "cudgel") else m["wood"], segs=10)]
    out.append(grip(m, -0.15, 0.04, r=0.024))
    if k == "knobbly":
        for i in range(7):
            a = i * 2.3; z = L * (0.45 + 0.08 * i)
            out.append(xform(lathe([(0.018, 0), (0.0, 0.035)], m["darkwood"], segs=6), (math.cos(a) * 0.06, math.sin(a) * 0.06, z), Matrix.Rotation(math.pi / 2, 3, Vector((-math.sin(a), math.cos(a), 0)))))
    if k == "spiked":
        for i in range(10):
            a = i * 2.4; z = L * (0.55 + 0.045 * i)
            out.append(xform(lathe([(0.01, 0), (0.0, 0.04)], m["iron"], segs=5, smooth=False), (math.cos(a) * 0.065, math.sin(a) * 0.065, z), Matrix.Rotation(math.pi / 2, 3, Vector((-math.sin(a), math.cos(a), 0)))))
    if k == "coral":
        for i in range(5):
            a = i * 1.26
            out.append(tube([(0, 0, L * 0.85), (math.cos(a) * 0.06, math.sin(a) * 0.06, L + 0.05), (math.cos(a) * 0.09, math.sin(a) * 0.09, L + 0.12)], [0.02, 0.014, 0.006], m["acc"], segs=6))
    if k == "crank":
        out.append(tube([(0, 0, L), (0, 0.12, L), (0, 0.12, L + 0.12)], 0.016, m["iron"], segs=8))
        out.append(box((0.04, 0.04, 0.08), (0, 0.12, L + 0.14), m["iron"]))
    if k == "brush":
        out[0] = shaft(m, -0.5, 0.8, r=0.02)
        out.append(lathe([(0.0, 0.78), (0.06, 0.8), (0.07, 0.95), (0.03, 1.0), (0.0, 1.0)], m["paper"], segs=10))
    out += bands(m, [0.05], r=0.025)
    return out

def w_mace(m, o):
    out = [shaft(m, -0.18, 0.5, r=0.017, mat=m["iron"]), grip(m, -0.18, 0.06, r=0.019)]
    k = o.get("kind", "flanged"); zc = 0.55
    if k == "flanged":
        out.append(lathe([(0, zc - 0.07), (0.03, zc - 0.06), (0.04, zc), (0.03, zc + 0.06), (0, zc + 0.08)], m["metal"], segs=12))
        for i in range(6):
            a = i / 6 * TAU
            fl = [(0.0, zc - 0.06), (0.07, zc - 0.03), (0.075, zc + 0.03), (0.0, zc + 0.07)]
            f = blade2d(fl, 0.01, o.get("fmat", m["metal"]))
            f.data.transform(Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(math.pi / 2, 4, "Z"))
            out.append(f)
        if o.get("runes"): out.append(torus(0.045, 0.006, m["glow"], loc=(0, 0, zc), seg=16, mseg=4))
        if o.get("sun"):
            out.append(gem(m, (0, 0, zc + 0.1), 0.03))
    elif k == "ladle":
        out.append(xform(lathe([(0.0, 0.0), (0.06, 0.0), (0.075, 0.03), (0.08, 0.07), (0.075, 0.075), (0.065, 0.035), (0.0, 0.02)], m["iron"], segs=14), (0.0, -0.06, zc), rx(90)))
        out.append(gem(m, (0, -0.06, zc + 0.03), 0.04))
    elif k == "key":
        out.append(torus(0.07, 0.016, m["iron"], loc=(0, 0, zc + 0.08), rot=ry(90), seg=14, mseg=6))
        out.append(box((0.025, 0.1, 0.03), (0, -0.05, zc - 0.07), m["iron"]))
        out.append(box((0.025, 0.04, 0.06), (0, -0.09, zc - 0.05), m["iron"]))
    elif k == "sceptre":
        out.append(lathe([(0, zc - 0.05), (0.04, zc - 0.03), (0.055, zc + 0.03), (0.035, zc + 0.08), (0.02, zc + 0.1), (0, zc + 0.1)], m["gold"], segs=12))
        for i in range(6):
            a = i * 1.1
            out.append(xform(lathe([(0.016, 0), (0.0, 0.025)], m["acc"], segs=6), (math.cos(a) * 0.045, math.sin(a) * 0.045, zc + 0.01 * i)))
        out.append(gem(m, (0, 0, zc + 0.13), 0.03))
    out.append(pommel(m, -0.2, r=0.022, kind="ball"))
    return out

def w_spear(m, o):
    L = o.get("len", 2.0); z0 = -0.6; z1 = z0 + L
    out = [shaft(m, z0, z1, r=o.get("r", 0.017), mat=o.get("smat", m["wood"])), ferrule(m, z0 + 0.02, r=0.018)]
    head = o.get("head", "leaf")
    out.append(lathe([(0.017, z1 - 0.02), (0.022, z1 + 0.06), (0.012, z1 + 0.11)], m["metal"], segs=10))
    if head == "leaf": out.append(leaf_head(m, z1 + 0.09, L=0.3 * o.get("hs", 1.0), W=0.045 * o.get("hs", 1.0)))
    elif head == "lance":
        out[0] = lathe([(0.0, z0), (0.02, z0), (0.02, -0.15), (0.06, -0.05), (0.07, 0.05), (0.055, 0.15), (0.012, z1 + 0.2), (0.0, z1 + 0.25)], m["acc"], segs=14)
        out.append(lathe([(0.0, 0.1), (0.1, 0.12), (0.08, 0.17), (0.0, 0.17)], m["metal"], segs=16))
    elif head == "star": out.append(star(m, (0, 0, z1 + 0.18), r=0.09, thick=0.018, rot=ry(90)))
    elif head == "harpoon":
        out.append(leaf_head(m, z1 + 0.09, L=0.22, W=0.035))
        for s in (-1, 1): out.append(blade2d([(0, z1 + 0.12), (s * 0.07, z1 + 0.06), (s * 0.03, z1 + 0.14)], 0.008, m["metal"]))
    if o.get("lugs"): out += [box((0.016, 0.16, 0.02), (0, 0, z1 + 0.08), m["iron"], bevel=0.004)]
    if o.get("ring"):
        out.append(torus(0.1, 0.012, m["gold"], loc=(0, 0, z1 + 0.22), rot=ry(90), seg=20, mseg=5))
        for k in range(8):
            a = k / 8 * TAU
            p = lathe([(0.012, 0), (0, 0.05)], m["glow"], segs=4, smooth=False)
            p.data.transform(Matrix.Translation((0, math.cos(a) * 0.11, z1 + 0.22 + math.sin(a) * 0.11)) @ Matrix.Rotation(-a + math.pi / 2, 4, "X"))
            out.append(p)
    if o.get("rope"): out.append(tube([(0, 0, z1 - 0.1), (0.05, 0.05, z1 - 0.3), (0.08, 0.02, z1 - 0.6), (0.06, -0.04, -0.2)], 0.007, m["string"], segs=5))
    if o.get("wrap"): out += [tube([(0.0, 0.0, z1 - 0.2), (0.04, 0.06, z1 - 0.5), (0.02, 0.1, z1 - 0.9)], 0.01, m["main"], segs=4, sq=0.3)]
    if not o.get("lance"): out.append(grip(m, -0.12, 0.12, r=0.019))
    return out

def w_trident(m, o):
    L = 1.9; z0 = -0.6; z1 = z0 + L
    out = [shaft(m, z0, z1, r=0.017, mat=o.get("smat", m["wood"])), grip(m, -0.12, 0.12, r=0.019), ferrule(m, z0 + 0.02)]
    out.append(tube([(0, -0.09, z1 + 0.2), (0, -0.08, z1 + 0.05), (0, 0, z1 + 0.0), (0, 0.08, z1 + 0.05), (0, 0.09, z1 + 0.2)], 0.012, o.get("hmat", m["metal"]), segs=6))
    for y in (-0.09, 0.0, 0.09):
        out.append(leaf_head(m, z1 + (0.18 if y else 0.0), L=0.18 if y else 0.36, W=0.022, mat=o.get("hmat", m["metal"])))
        out[-1].data.transform(Matrix.Translation((0, y, 0)))
    if o.get("strings"):
        for y in (-0.06, -0.03, 0.03, 0.06): out.append(tube([(0, y, z1 + 0.06), (0, y, z1 + 0.32)], 0.003, m["glow"], segs=4))
    return out

def w_glaive(m, o):
    L = 1.9; z0 = -0.6; z1 = z0 + L
    out = [shaft(m, z0, z1, r=0.018), grip(m, -0.12, 0.12, r=0.02), ferrule(m, z0 + 0.02), lathe([(0.018, z1 - 0.03), (0.024, z1 + 0.05), (0.016, z1 + 0.08)], m["iron"], segs=10)]
    kind = o.get("kind", "glaive")
    if kind == "glaive":
        curl = o.get("curl", 0.0)
        pts = [(z1 + 0.06 + 0.5 * t, 0.045 * math.sin(math.pi * min(1, t * 1.1)) + 0.012, 0.007, -0.03 * t - curl * t ** 3) for t in [i / 12 for i in range(13)]]
        out.append(blade(pts, m["metal"], single=True))
        if curl: out.append(tube([(0, -curl - 0.03, z1 + 0.56), (0, -curl - 0.1, z1 + 0.5), (0, -curl - 0.08, z1 + 0.42)], [0.012, 0.01, 0.004], m["metal"], segs=6))
    elif kind == "halberd":
        out.append(leaf_head(m, z1 + 0.07, L=0.3, W=0.03))
        bit = [(-0.01, z1 - 0.06), (-0.18, z1 - 0.12), (-0.22, z1 + 0.0), (-0.18, z1 + 0.13), (-0.01, z1 + 0.07)]
        out.append(blade2d(bit, 0.012, o.get("bmat", m["metal"])))
        out.append(blade2d([(0.01, z1 - 0.02), (0.12, z1 - 0.05), (0.01, z1 + 0.04)], 0.012, m["metal"]))
        if o.get("sunrise"):
            for k in range(5):
                a = math.radians(-60 + 30 * k)
                out.append(xform(lathe([(0.008, 0), (0, 0.06)], m["glow"], segs=4, smooth=False), (0, -0.18 + 0.0, z1), rx(90 - math.degrees(a))))
                out[-1].data.transform(Matrix.Translation((0, -math.cos(a) * 0.05, math.sin(a) * 0.05)))
    elif kind == "scythe":
        pts = [Vector((0, -0.62 * t, z1 + 0.02 + 0.12 * math.sin(math.pi * t * 0.8) - 0.05 * t)) for t in [i / 14 for i in range(15)]]
        st = []
        T, N, B = _frames(pts)
        rings = []
        for i, p in enumerate(pts):
            t = i / 14; hw = 0.05 * (1 - t) ** 0.7 + 0.002; th = 0.006 * (1 - t) + 0.001
            dn = Vector((0, 0, -1))
            rings.append([p + dn * hw, p + Vector((th, 0, 0)), p - dn * hw * 0.3 + Vector((th * 0.6, 0, 0)), p - dn * hw * 0.3 - Vector((th * 0.6, 0, 0)), p - Vector((th, 0, 0))])
        out.append(loft(rings, m["metal"]))
        out.append(tube([(0, 0, 0.1), (0.1, 0, 0.12), (0.12, 0, 0.18)], 0.012, m["wood"], segs=6))
    elif kind == "billhook":
        out.append(blade([(z1 + 0.06, 0.03, 0.006, 0), (z1 + 0.25, 0.035, 0.006, -0.01), (z1 + 0.36, 0.03, 0.005, -0.05), (z1 + 0.4, 0.008, 0.003, -0.09)], m["metal"], single=True))
    elif kind == "hook":
        pts = [(0, 0, z1 + 0.02), (0, 0, z1 + 0.2), (0, -0.04, z1 + 0.3), (0, -0.12, z1 + 0.3), (0, -0.15, z1 + 0.22), (0, -0.12, z1 + 0.16)]
        out.append(tube(pts, [0.015, 0.014, 0.013, 0.011, 0.008, 0.003], m["iron"], segs=6))
        if o.get("barbs"):
            for k in range(3): out.append(xform(lathe([(0.008, 0), (0, 0.04)], m["iron"], segs=4, smooth=False), (0, 0, z1 + 0.05 + 0.05 * k), rx(-45)))
    return out

def w_staff(m, o):
    L = o.get("len", 1.85); z0 = -0.65; z1 = z0 + L
    head = o.get("head", "plain")
    gn = o.get("gnarl", 0.0)
    out = [shaft(m, z0, z1, r=o.get("r", 0.018), mat=o.get("smat", m["wood"]), gnarl=gn)]
    if o.get("shod"):
        out += [ferrule(m, z0 + 0.03, r=0.02, h=0.06), ferrule(m, z1 - 0.03, r=0.02, h=0.06)]
    else:
        out.append(ferrule(m, z0 + 0.02, r=0.018))
    out.append(grip(m, -0.1, 0.12, r=0.02, mat=m["leather"]))
    if head == "plain":
        out.append(lathe([(0.0, z1), (0.024, z1 + 0.0), (0.03, z1 + 0.04), (0.02, z1 + 0.08), (0.0, z1 + 0.09)], m["gold" if o.get("gold") else "metal"], segs=10))
    elif head == "orb":
        out += prongs(m, z1, 4, 0.07)
        out.append(lathe([(0, z1 + 0.04), (0.06, z1 + 0.06), (0.075, z1 + 0.12), (0.06, z1 + 0.18), (0, z1 + 0.2)], o.get("omat", m["glow"]), segs=16))
    elif head == "fork":
        for s in (-1, 1): out.append(tube([(0, 0, z1 - 0.02), (0, s * 0.06, z1 + 0.12), (0, s * 0.05, z1 + 0.28)], [0.016, 0.012, 0.006], m["wood"], segs=6))
        out.append(lathe([(0, z1 + 0.1), (0.05, z1 + 0.13), (0.06, z1 + 0.18), (0.05, z1 + 0.23), (0, z1 + 0.26)], m["glow"], segs=14))
        out.append(torus(0.075, 0.004, m["glow"], loc=(0, 0, z1 + 0.18), rot=rx(20), seg=18, mseg=4))
    elif head == "crystal":
        out += prongs(m, z1, 3, 0.05)
        for k, (dx, dy, h, tx, ty) in enumerate(((0, 0, 0.26, 0, 0), (0.03, 0.02, 0.15, 20, -15), (-0.03, 0.025, 0.13, -15, 20), (0.0, -0.035, 0.14, 25, 10))):
            out.append(crystal(m, (dx, dy, z1 + 0.04), h=h, r=0.03 if k == 0 else 0.022, tilt=(tx, ty)))
    elif head == "disc":
        out.append(lathe([(0, z1), (0.03, z1 + 0.02), (0.02, z1 + 0.06), (0, z1 + 0.07)], m["gold"], segs=10))
        out.append(xform(lathe([(0, -0.008), (0.09, -0.006), (0.1, 0), (0.09, 0.006), (0, 0.008)], m["gold"], segs=20), (0, 0, z1 + 0.18), ry(90)))
        out.append(xform(lathe([(0, -0.01), (0.05, 0), (0, 0.01)], m["glow"], segs=16), (0, 0, z1 + 0.18), ry(90)))
        out.append(tube([(0, 0, z1 + 0.06), (0, 0, z1 + 0.08)], 0.01, m["gold"], segs=6))
        out += bands(m, [z1 - 0.1, z1 - 0.3, z1 - 0.5], r=0.02, mat=m["gold"])
    elif head == "crook":
        pts = [(0, 0, z1 - 0.02), (0, 0, z1 + 0.1), (0, -0.04, z1 + 0.22), (0, -0.13, z1 + 0.26), (0, -0.2, z1 + 0.2), (0, -0.2, z1 + 0.1)]
        out.append(tube(pts, 0.017, m["wood"], segs=8))
        if o.get("lantern"): out += lantern(m, (0, -0.2, z1 + 0.06), s=0.75, hang=True)
    elif head == "rocket":
        out.append(lathe([(0, z1), (0.04, z1 + 0.02), (0.045, z1 + 0.2), (0.0, z1 + 0.3)], m["acc"], segs=10))
        for k in range(3):
            f = blade2d([(0.0, z1 + 0.02), (0.07, z1 - 0.02), (0.04, z1 + 0.1)], 0.006, m["metal"]); f.data.transform(Matrix.Rotation(k / 3 * TAU, 4, "Z")); out.append(f)
        out.append(tube([(0, 0, z1 + 0.3), (0.02, 0.02, z1 + 0.36), (0.0, 0.04, z1 + 0.4)], 0.004, m["string"], segs=4))
        out.append(flame(m, (0, 0.04, z1 + 0.4), h=0.05, r=0.015))
    elif head == "bone":
        out[0] = shaft(m, z0, z1, r=0.02, mat=m["paper"], gnarl=0.004)
        out.append(lathe([(0, z1 - 0.02), (0.05, z1 + 0.02), (0.06, z1 + 0.08), (0.04, z1 + 0.14), (0, z1 + 0.15)], m["paper"], segs=10))
        out.append(torus(0.06, 0.006, m["iron"], loc=(0, 0, z1 + 0.02), seg=14, mseg=4))
        for k in range(5):
            a = k / 5 * TAU
            out.append(tube([(math.cos(a) * 0.06, math.sin(a) * 0.06, z1 + 0.02), (math.cos(a) * 0.065, math.sin(a) * 0.065, z1 - 0.1)], 0.003, m["string"], segs=4))
            out.append(xform(lathe([(0, -0.03), (0.012, -0.02), (0.014, 0.02), (0, 0.03)], m["paper"], segs=6), (math.cos(a) * 0.065, math.sin(a) * 0.065, z1 - 0.13)))
    elif head == "candle":
        out[0] = shaft(m, z0, z1, r=0.016, mat=m["iron"])
        out.append(lathe([(0, z1), (0.06, z1 + 0.02), (0.07, z1 + 0.05), (0.03, z1 + 0.05), (0, z1 + 0.05)], m["iron"], segs=14))
        out.append(lathe([(0, z1 + 0.04), (0.025, z1 + 0.04), (0.024, z1 + 0.2), (0, z1 + 0.21)], m["paper"], segs=10))
        out.append(flame(m, (0, 0, z1 + 0.21), h=0.08, r=0.018))
        out.append(tube([(0.03, 0, z1 + 0.15), (0.032, 0.0, z1 + 0.09)], [0.006, 0.002], m["paper"], segs=5))
        for k in range(3): out.append(torus(0.022, 0.006, m["iron"], loc=(0, 0, z1 - 0.15 - 0.25 * k), seg=10, mseg=4))
    elif head == "quill":
        out[0] = shaft(m, z0, z1, r=0.013, mat=m["paper"])
        out.append(blade([(z0 - 0.0, 0.006, 0.006, 0), (z0 + 0.04, 0.012, 0.008, 0), (z0 - 0.14, 0.001, 0.001, 0)][::-1][::-1], m["gold"]))
        st = [(z1 - 0.5 + 0.9 * t, 0.07 * math.sin(math.pi * min(1, t * 1.05)) + 0.004, 0.003, 0.02 * t) for t in [i / 12 for i in range(13)]]
        out.append(blade(st, m["paper"]))
        out.append(lathe([(0, z0 - 0.12), (0.014, z0), (0.013, z0 + 0.03), (0, z0 + 0.03)], m["gold"], segs=8))
    elif head == "lens":
        out[0] = shaft(m, z0, z1, r=0.016, mat=m["gold"])
        out.append(torus(0.1, 0.014, m["gold"], loc=(0, 0, z1 + 0.12), rot=ry(90), seg=20, mseg=6))
        out.append(xform(lathe([(0, -0.02), (0.06, -0.015), (0.1, 0), (0.06, 0.015), (0, 0.02)], m["glow"], segs=10, smooth=False), (0, 0, z1 + 0.12), ry(90)))
        out.append(tube([(0, 0, z1), (0, 0, z1 + 0.025)], 0.02, m["gold"], segs=8))
    elif head == "tome":
        out += prongs(m, z1, 2, 0.06)
        out += book(m, (0, 0, z1 + 0.12), open_=True)
    elif head == "stone":
        out.append(lathe([(0.0, z1 - 0.05), (0.06, z1 - 0.02), (0.075, z1 + 0.12), (0.06, z1 + 0.26), (0.0, z1 + 0.3)], m["stone"], segs=6, smooth=False, sx=1.0, sy=0.6))
        for k in range(3): out.append(box((0.015, 0.08, 0.012), (0.075 * 0 + 0.0, 0, z1 + 0.05 + 0.07 * k), m["glow"], bevel=0, rot=rx(0)))
        for ob in out[-3:]: ob.data.transform(Matrix.Translation((0.042, 0, 0)))
    elif head == "sunflower":
        out[0] = shaft(m, z0, z1, r=0.016, mat=m["main"])
        c = (0, 0, z1 + 0.16)
        out.append(xform(lathe([(0, -0.02), (0.07, -0.01), (0.07, 0.015), (0, 0.025)], m["darkwood"], segs=18), c, ry(-90)))
        for k in range(16):
            a = k / 16 * TAU
            pt = blade2d([(0.0, 0.0), (0.03, 0.04), (0.0, 0.12), (-0.03, 0.04)], 0.004, m["glow"])
            pt.data.transform(Matrix.Translation(c) @ Matrix.Rotation(a, 4, "X") @ Matrix.Translation((0, 0, 0.06)))
            out.append(pt)
        for s in (-1, 1): out.append(blade2d([(0, z1 - 0.3), (s * 0.08, z1 - 0.22), (0, z1 - 0.12)], 0.004, m["acc"]))
    elif head == "owl":
        out.append(tube([(0, -0.08, z1), (0, 0.08, z1)], 0.012, m["wood"], segs=6))
        b = (0, 0, z1 + 0.07)
        out.append(xform(lathe([(0, -0.06), (0.035, -0.05), (0.045, 0.0), (0.04, 0.03), (0.0, 0.05)], m["metal"], segs=12), b))
        out.append(xform(lathe([(0, -0.03), (0.035, -0.02), (0.035, 0.02), (0, 0.035)], m["metal"], segs=12), (0, 0, z1 + 0.14)))
        for s in (-1, 1): out.append(gem(m, (0.032, s * 0.014, z1 + 0.145), 0.008))
        for s in (-1, 1): out.append(xform(lathe([(0.008, 0), (0, 0.025)], m["metal"], segs=4, smooth=False), (0, s * 0.02, z1 + 0.17), rx(-25 * s)))
    elif head == "lily":
        out[0] = shaft(m, z0, z1, r=0.019, mat=m["darkwood"], gnarl=0.01)
        out.append(lathe([(0, z1), (0.03, z1 + 0.03), (0.04, z1 + 0.08), (0.03, z1 + 0.13), (0, z1 + 0.17)], m["glow"], segs=10))
        for k in range(4):
            pt = blade2d([(0.0, 0.0), (0.03, 0.06), (0.0, 0.14), (-0.03, 0.06)], 0.004, m["acc"])
            pt.data.transform(Matrix.Translation((0, 0, z1)) @ Matrix.Rotation(k / 4 * TAU, 4, "Z") @ Matrix.Translation((0.02, 0, 0)) @ Matrix.Rotation(math.radians(20), 4, "Y") @ Matrix.Rotation(math.pi / 2, 4, "Z"))
            out.append(pt)
    elif head == "sceptre":
        out.append(lathe([(0, z1), (0.03, z1 + 0.02), (0.04, z1 + 0.08), (0.02, z1 + 0.12), (0, z1 + 0.13)], m["gold"], segs=12))
        out.append(torus(0.08, 0.01, m["gold"], loc=(0, 0, z1 + 0.2), rot=ry(90), seg=20, mseg=5))
        out.append(xform(lathe([(0, -0.012), (0.065, -0.006), (0.065, 0.006), (0, 0.012)], m["iron"], segs=18), (0, 0, z1 + 0.2), ry(90)))
        out.append(torus(0.068, 0.006, m["glow"], loc=(0, 0, z1 + 0.2), rot=ry(90), seg=20, mseg=4))
    elif head == "standard":
        out[0] = shaft(m, z0, z1, r=0.016, mat=m["metal"])
        out.append(tube([(0, 0, z1), (0, 0, z1 + 0.3)], [0.01, 0.003], m["glow"], segs=5))
        for k in range(3):
            out.append(tube([(0, 0, z1 + 0.05 + 0.08 * k), (0, -0.05, z1 + 0.09 + 0.08 * k), (0, -0.02, z1 + 0.13 + 0.08 * k)], 0.004, m["glow"], segs=4))
        out.append(yz_plate([(0, z1 - 0.02), (-0.32, z1 - 0.04), (-0.36, z1 - 0.16), (-0.3, z1 - 0.26), (0, z1 - 0.28)], 0.004, m["main"], bevel=0))
    elif head == "torch":
        out = [lathe([(0, -0.15), (0.02, -0.15), (0.022, 0.2), (0.04, 0.3), (0.045, 0.38), (0.0, 0.39)], m["wood"], segs=10)]
        out.append(lathe([(0.04, 0.27), (0.05, 0.32), (0.05, 0.38), (0.0, 0.39)], m["leather"], segs=10))
        out.append(flame(m, (0, 0, 0.37), h=0.2, r=0.05))
        out += bands(m, [0.29], r=0.045, mat=m["iron"])
        out.append(grip(m, -0.12, 0.08, r=0.022))
    elif head == "pole_lamp":
        out.append(tube([(0, 0, z1), (0, -0.12, z1 + 0.06), (0, -0.2, z1 + 0.02)], 0.01, m["iron"], segs=6))
        out += lantern(m, (0, -0.2, z1 - 0.02), s=0.6, hang=True)
    elif head == "pole_kettle":
        out.append(tube([(0, 0, z1), (0, -0.14, z1 + 0.04), (0, -0.2, z1 - 0.04)], 0.01, m["iron"], segs=6))
        out.append(tube([(0, -0.2, z1 - 0.04), (0, -0.2, z1 - 0.14)], 0.003, m["iron"], segs=4))
        out.append(lathe([(0, z1 - 0.32), (0.06, z1 - 0.31), (0.085, z1 - 0.25), (0.08, z1 - 0.18), (0.05, z1 - 0.16), (0.0, z1 - 0.15)], m["acc"], segs=14))
        out[-1].data.transform(Matrix.Translation((0, -0.2, 0)))
        out.append(torus(0.06, 0.005, m["acc"], loc=(0, -0.2, z1 - 0.16), rot=rx(90), seg=12, mseg=4))
    elif head == "pole_bell":
        out[0] = shaft(m, z0, z1, r=0.015, mat=m["wood"], gnarl=0.012)
        out.append(tube([(0, 0, z1), (0, -0.12, z1 + 0.08), (0, -0.24, z1 + 0.04)], [0.014, 0.01, 0.007], m["wood"], segs=6))
        out.append(tube([(0, -0.24, z1 + 0.04), (0, -0.24, z1 - 0.06)], 0.003, m["string"], segs=4))
        out += bell(m, (0, -0.24, z1 - 0.06), s=0.6, glow=True)
    elif head == "perch":
        out.append(tube([(0, 0, z1), (0, 0, z1 + 0.1)], 0.015, m["wood"], segs=6))
    if o.get("chimes"):   # wind chimes: a crossbar hung with glass tubes, crystal drops and feathers
        zb = z1 - 0.04
        out.append(tube([(0, -0.12, zb), (0, 0.04, zb)], 0.006, m["wood"], segs=6))
        for q, y in enumerate((-0.11, -0.08, -0.05, -0.02, 0.01, 0.035)):
            L = 0.07 + 0.035 * ((q * 3) % 4)
            out.append(tube([(0, y, zb), (0, y, zb - 0.03)], 0.0012, m["string"], segs=3))
            out.append(tube([(0, y, zb - 0.03), (0, y, zb - 0.03 - L)], 0.006, m["glass_pale"], segs=6))
            if q % 2 == 0: out.append(crystal(m, (0, y, zb - 0.06 - L), h=0.03, r=0.009, mat=m["glass_pale"]))
        out.append(leaf_head(m, zb - 0.02, L=0.1, W=0.02, mat=m["string"]))
    return out

def prongs(m, z, n, R, mat=None):
    out = []
    for k in range(n):
        a = k / n * TAU
        out.append(tube([(0, 0, z - 0.03), (math.cos(a) * R * 0.8, math.sin(a) * R * 0.8, z + 0.05), (math.cos(a) * R, math.sin(a) * R, z + 0.14), (math.cos(a) * R * 0.6, math.sin(a) * R * 0.6, z + 0.2)], [0.012, 0.01, 0.007, 0.003], mat or m["metal"], segs=6))
    return out

def w_bow(m, o):
    """Recurve or long bow along local +Z through the grip; the string toward +X (palm side)."""
    H = o.get("half", 0.62); rec = o.get("recurve", 0.0)
    pts = []; rad = []
    n = 16
    for i in range(n + 1):
        t = i / n * 2 - 1
        z = t * H
        x = 0.11 * t * t - 0.025 * (1 - t * t)
        if rec and abs(t) > 0.75: x -= rec * ((abs(t) - 0.75) / 0.25) ** 2
        pts.append((x, 0.0, z)); rad.append(0.019 * (1 - 0.55 * abs(t)) + 0.004)
    mat = o.get("mat", m["wood"])
    out = [tube(pts, rad, mat, segs=8, sq=0.65)]
    tip = [pts[0], pts[-1]]
    out.append(tube([tip[0], (0.0 + 0.11 * 0.4 if False else tip[0][0], 0, 0), tip[1]] if False else [(tip[0][0] + 0.003, 0, tip[0][2]), (tip[1][0] + 0.003, 0, tip[1][2])], 0.0025, m["string"], segs=4))
    out.append(lathe([(0.0, -0.07), (0.024, -0.065), (0.026, 0.0), (0.024, 0.065), (0.0, 0.07)], m["leather"], segs=10, sx=1.0, sy=0.8))
    out[-1].data.transform(Matrix.Translation((-0.022, 0, 0)))
    for s in (-1, 1): out.append(xform(lathe([(0, -0.012), (0.009, 0), (0, 0.012)], m["acc"], segs=6), (tip[0][0], 0, s * H)))
    if o.get("crystal"):
        for s in (-1, 1): out.append(crystal(m, (pts[0][0] if s < 0 else pts[-1][0], 0, s * (H + 0.02)), h=0.1, r=0.02, tilt=(0 if s > 0 else 180, 0)))
    if o.get("reed"):
        for k in range(10):
            t = -0.9 + 0.2 * k
            out.append(torus(0.014, 0.003, m["string"], loc=(0.11 * t * t - 0.025 * (1 - t * t), 0, t * H), seg=8, mseg=3))
    return out

def w_crossbow(m, o):
    out = []
    stock = [(0.0, -0.3), (0.03, -0.25), (0.03, 0.35), (0.0, 0.4)]
    out.append(box((0.05, 0.06, 0.62), (0.03, 0, 0.12), m["darkwood"] if o.get("thorn") else m["wood"], bevel=0.01))
    out.append(box((0.045, 0.06, 0.14), (0.05, 0, -0.18), m["wood"], bevel=0.012, rot=rx(0)))
    pts = [(0.06 + 0.06 * (1 - (y / 0.32) ** 2) * 0 + 0.04 * (y / 0.32) ** 2, y, 0.38) for y in [-0.32 + 0.64 * i / 12 for i in range(13)]]
    out.append(tube(pts, [0.012 * (1 - 0.5 * abs(p[1]) / 0.32) + 0.004 for p in pts], m["iron"], segs=6))
    out.append(tube([(pts[0][0] + 0.004, pts[0][1], 0.38), (0.035, 0, 0.18), (pts[-1][0] + 0.004, pts[-1][1], 0.38)], 0.0025, m["string"], segs=4))
    out.append(tube([(0.06, 0, 0.2), (0.06, 0, 0.47)], 0.006, m["wood"], segs=5))
    out.append(xform(lathe([(0.01, 0), (0, 0.04)], m["glow"] if o.get("ember") else m["metal"], segs=5, smooth=False), (0.06, 0, 0.47)))
    out.append(tube([(0.0, 0, 0.0), (-0.02, 0, -0.06)], 0.006, m["iron"], segs=5))
    if o.get("thorn"):
        for k in range(8): out.append(xform(lathe([(0.006, 0), (0, 0.025)], m["darkwood"], segs=4, smooth=False), (0.03, 0.032 * (1 if k % 2 else -1), -0.05 + 0.06 * k), rx(-80 * (1 if k % 2 else -1))))
    return out

def w_whip(m, o):
    out = [grip(m, -0.1, 0.06, r=0.018), pommel(m, -0.11, r=0.02, kind="ball")]
    pts = [Vector((0.0, 0.0, -0.11))]
    for i in range(1, 16):
        t = i / 15
        pts.append(Vector((0.12 * math.sin(t * 2.4), -0.25 * t + 0.08 * math.sin(t * 5), -0.11 - 0.95 * t + 0.25 * t * t)))
    if o.get("chain"):
        for i in range(len(pts) - 1):
            p, q = pts[i], pts[i + 1]
            d = (q - p).normalized(); R = d.to_track_quat("Y", "Z").to_matrix()
            if i % 2: R = R @ ry(90)
            out.append(torus(0.022, 0.006, m["iron"], loc=(p + q) / 2, rot=R @ rx(90), seg=8, mseg=4, sx=0.7, sy=1.4))
        if o.get("burn"):
            for i in range(2, len(pts), 3): out.append(flame(m, pts[i], h=0.07, r=0.02))
    else:
        out.append(tube(pts, [0.014 * (1 - 0.8 * i / 15) + 0.002 for i in range(16)], m["darkwood"] if o.get("thorn") else m["leather"], segs=6))
        if o.get("thorn"):
            for i in range(1, 15, 2):
                out.append(xform(lathe([(0.005, 0), (0, 0.022)], m["darkwood"], segs=4, smooth=False), pts[i], rx(90 if i % 4 == 1 else -90)))
                out.append(xform(blade2d([(0, 0), (0.02, 0.02), (0, 0.04), (-0.02, 0.02)], 0.002, m["acc"]), pts[i]))
    return out

def w_flail(m, o):
    """A haft (or a chain from the fist) with a weight hanging on a chain."""
    kind = o.get("kind", "ball")
    out = []
    if o.get("haft", True):
        out += [shaft(m, -0.18, 0.35, r=0.018), grip(m, -0.18, 0.05, r=0.02), torus(0.02, 0.006, m["iron"], loc=(0, 0, 0.38), rot=ry(90), seg=8, mseg=4)]
        top = Vector((0, 0, 0.38)); end = Vector((0.0, -0.16, 0.05))
    else:
        out += [torus(0.03, 0.007, m["iron"], loc=(0, 0, 0.0), rot=ry(90), seg=10, mseg=4)]
        top = Vector((0, 0, -0.03)); end = Vector((0.0, -0.05, -0.55))
    out += chain(m, top, end, sag=0.04, links=8 if o.get("haft", True) else 11, r=0.016)
    if kind == "ball":
        out.append(xform(lathe([(0, -0.07), (0.05, -0.05), (0.07, 0), (0.05, 0.05), (0, 0.07)], m["iron"], segs=12), end + Vector((0, 0, -0.07))))
        if o.get("spikes", True):
            for k in range(10):
                a = k * 2.4; e = math.asin(-1 + 2 * (k + 0.5) / 10)
                d = Vector((math.cos(a) * math.cos(e), math.sin(a) * math.cos(e), math.sin(e)))
                out.append(xform(lathe([(0.012, 0), (0, 0.04)], m["iron"], segs=4, smooth=False), end + Vector((0, 0, -0.07)) + d * 0.06, d.to_track_quat("Z", "Y").to_matrix()))
        if o.get("molten"):
            out.append(xform(lathe([(0, -0.074), (0.052, -0.05), (0.074, 0), (0.052, 0.05), (0, 0.074)], m["glow"], segs=12), end + Vector((0, 0, -0.07))))
            out.pop(-2)
    elif kind == "orb":
        out.append(xform(lathe([(0, -0.06), (0.045, -0.045), (0.06, 0), (0.045, 0.045), (0, 0.06)], m["glow"], segs=14), end + Vector((0, 0, -0.06))))
        out.append(torus(0.062, 0.006, m["metal"], loc=end + Vector((0, 0, -0.06)), rot=rx(90), seg=16, mseg=4))
    elif kind == "censer":
        c = end + Vector((0, 0, -0.09))
        out.append(xform(lathe([(0, -0.06), (0.05, -0.05), (0.065, 0.0), (0.05, 0.04), (0.02, 0.07), (0, 0.075)], m["gold"], segs=14), c))
        for k in range(6): out.append(xform(box((0.006, 0.02, 0.05), (0, 0, 0), m["glow"], bevel=0), c + Vector((math.cos(k) * 0.064, math.sin(k) * 0.064, 0)), rz(math.degrees(k))))
        out.append(xform(lathe([(0, 0), (0.02, 0.02), (0, 0.07)], m["glow"], segs=6), c + Vector((0, 0, 0.07))))
    elif kind == "anchor":
        a = end + Vector((0, 0, -0.3))
        out += anchor(m, a, s=0.6)
    elif kind == "keys":
        for k in range(5):
            ang = -0.6 + 0.3 * k
            kk = box((0.012, 0.025, 0.16), (0, 0, -0.08), m["iron"], bevel=0.003)
            kk.data.transform(Matrix.Translation(end) @ Matrix.Rotation(ang, 4, "X"))
            out.append(kk)
            bow = torus(0.025, 0.006, m["iron"], rot=ry(90), seg=10, mseg=4)
            bow.data.transform(Matrix.Translation(end) @ Matrix.Rotation(ang, 4, "X") @ Matrix.Translation((0, 0, 0.0)))
            out.append(bow)
        out.append(torus(0.06, 0.008, m["iron"], loc=end, rot=ry(90), seg=16, mseg=4))
    elif kind == "shackle":
        out.append(torus(0.04, 0.01, m["iron"], loc=(0, 0, 0.0), rot=rx(90), seg=12, mseg=5))
        out.append(xform(lathe([(0, -0.09), (0.065, -0.065), (0.09, 0), (0.065, 0.065), (0, 0.09)], m["iron"], segs=14), end + Vector((0, 0, -0.09))))
    return out

def anchor(m, c, s=1.0):
    c = Vector(c); out = []
    out.append(tube([c + Vector((0, 0, 0.0)), c + Vector((0, 0, 0.55 * s))], 0.022 * s, m["iron"], segs=8))
    out.append(torus(0.05 * s, 0.012 * s, m["iron"], loc=c + Vector((0, 0, 0.6 * s)), rot=ry(90), seg=12, mseg=5))
    out.append(box((0.03 * s, 0.3 * s, 0.03 * s), c + Vector((0, 0, 0.45 * s)), m["iron"], bevel=0.006))
    arc = [c + Vector((0, math.sin(a) * 0.22 * s, 0.22 * s - math.cos(a) * 0.22 * s)) for a in [math.radians(x) for x in range(-80, 81, 16)]]
    out.append(tube(arc, 0.022 * s, m["iron"], segs=8))
    for sg in (-1, 1):
        out.append(xform(blade2d([(0, 0), (sg * 0.07 * s, 0.03 * s), (0, 0.1 * s)], 0.02 * s, m["iron"]), c + Vector((0, sg * 0.2 * s, 0.17 * s))))
    return out

def w_anchor(m, o):
    out = anchor(m, (0, 0, -0.35), s=1.25)
    out.append(grip(m, -0.05, 0.15, r=0.03))
    if o.get("rope"): out.append(tube([(0, 0, 0.42), (0.06, 0.05, 0.3), (0.05, -0.05, 0.1), (0.07, 0.04, -0.1)], 0.01, m["string"], segs=5))
    return out

def w_spade(m, o):
    out = _spade(m, o)
    for ob in out: ob.data.transform(Matrix.Rotation(math.pi, 4, "Y"))   # blade up, D-handle at the butt
    out.append(grip(m, -0.1, 0.12, r=0.02))
    if o.get("lantern"): out += lantern(m, (0.0, -0.06, 0.45), s=0.6, hang=True)
    return out

def _spade(m, o):
    out = [shaft(m, -0.55, 0.65, r=0.019)]
    out.append(tube([(0, -0.07, 0.75), (0, -0.05, 0.68), (0, 0.05, 0.68), (0, 0.07, 0.75), (0, 0, 0.78)][:4], 0.012, m["wood"], segs=6))
    w = o.get("w", 0.13)
    sh = [(-w, -0.62), (w, -0.62), (w * 1.05, -0.85), (0.0, -0.95 if o.get("pointed", True) else -0.9), (-w * 1.05, -0.85)]
    if o.get("shovel"): sh = [(-w, -0.62), (w, -0.62), (w * 1.1, -0.92), (-w * 1.1, -0.92)]
    out.append(yz_plate(sh, 0.008, m["metal"]))
    out.append(lathe([(0.018, -0.6), (0.026, -0.62), (0.02, -0.55)], m["iron"], segs=8))
    return out

def w_peel(m, o):
    out = [shaft(m, -0.6, 0.9, r=0.02), grip(m, -0.1, 0.12, r=0.021)]
    sh = [(-0.04, 0.88), (0.04, 0.88), (0.15, 1.0), (0.17, 1.25), (0.0, 1.32), (-0.17, 1.25), (-0.15, 1.0)]
    out.append(yz_plate(sh, 0.012, m["wood"]))
    out.append(yz_plate([(-0.17, 1.18), (0.17, 1.18), (0.0, 1.32)], 0.0125, m["darkwood"], bevel=0))
    return out

def w_fork(m, o):
    """Pitchfork, kelp rake, poker, branding iron, trap pole (long-shafted tools)."""
    k = o.get("kind", "pitchfork"); z1 = 1.25
    out = [shaft(m, -0.6, z1, r=0.018, mat=m["iron"] if k in ("poker", "brand") else m["wood"]), grip(m, -0.1, 0.12, r=0.02)]
    if k == "pitchfork":
        out.append(tube([(0, -0.08, z1 + 0.06), (0, -0.06, z1), (0, 0.06, z1), (0, 0.08, z1 + 0.06)], 0.01, m["iron"], segs=6))
        for y in (-0.08, 0.0, 0.08): out.append(tube([(0, y, z1 + (0.0 if y == 0 else 0.06)), (0, y * 1.05, z1 + 0.32)], [0.009, 0.003], m["iron"], segs=6))
    elif k == "rake":
        out.append(box((0.03, 0.34, 0.03), (0, 0, z1 + 0.02), m["wood"], bevel=0.006))
        for i in range(7):
            y = -0.15 + 0.05 * i
            out.append(tube([(0, y, z1 + 0.02), (0.06, y, z1 - 0.06), (0.09, y, z1 - 0.04)], [0.008, 0.006, 0.003], m["acc"], segs=5))
        out.append(tube([(0, -0.1, z1 + 0.03), (0.04, -0.12, z1 - 0.2), (0.0, -0.1, z1 - 0.35)], [0.012, 0.01, 0.004], m["acc"], segs=4, sq=0.3))
    elif k == "poker":
        out.append(tube([(0, 0, z1), (0, 0, z1 + 0.2), (0, -0.06, z1 + 0.28)], [0.012, 0.01, 0.004], m["iron"], segs=6))
        out.append(box((0.02, 0.02, 0.06), (0, 0, z1 + 0.25), m["glow"], bevel=0.004))
        out.append(lathe([(0, -0.62), (0.03, -0.6), (0.035, -0.55), (0, -0.53)], m["acc"], segs=10))
    elif k == "brand":
        out.append(torus(0.06, 0.012, m["glow"], loc=(0, 0, z1 + 0.08), rot=ry(90), seg=14, mseg=5))
        out.append(box((0.016, 0.1, 0.016), (0, 0, z1 + 0.08), m["glow"], bevel=0.003))
        out.append(box((0.016, 0.016, 0.1), (0, 0, z1 + 0.08), m["glow"], bevel=0.003))
    elif k == "trap":
        for s in (-1, 1):
            arc = [(0, s * (0.02 + 0.1 * math.sin(a)), z1 + 0.1 - 0.1 * math.cos(a)) for a in [math.radians(x) for x in range(0, 181, 20)]]
            out.append(tube(arc, 0.008, m["iron"], segs=5))
            for i in range(1, 9, 2): out.append(xform(lathe([(0.006, 0), (0, 0.025)], m["iron"], segs=4, smooth=False), arc[i], rx(90 * -s)))
        out.append(box((0.03, 0.06, 0.03), (0, 0, z1 + 0.0), m["iron"]))
    return out

def w_tankard(m, o):
    out = [lathe([(0, 0.0), (0.12, 0.0), (0.13, 0.02), (0.12, 0.34), (0.125, 0.36), (0.0, 0.36)], m["wood"], segs=18)]
    out[0].data.transform(Matrix.Translation((0.0, 0.17, -0.2)))
    for z in (-0.16, 0.0, 0.12): out.append(torus(0.127, 0.008, m["iron"], loc=(0, 0.17, z), seg=18, mseg=4))
    out.append(tube([(0, 0.05, 0.12), (0, 0.0, 0.08), (0, -0.01, -0.02), (0, 0.0, -0.1), (0, 0.05, -0.14)], 0.018, m["wood"], segs=6))
    out.append(lathe([(0, 0.0), (0.12, 0.0), (0.0, 0.03)], m["paper"], segs=18)); out[-1].data.transform(Matrix.Translation((0, 0.17, 0.16)))
    return out

def w_fist(m, o):
    """Gauntlets (ember fists, stone fist): a cuff and knuckle plates around the hand at the grip origin."""
    big = o.get("big", 1.0); mat = m["stone"] if o.get("stone") else m["iron"]
    out = [xform(lathe([(0.0, -0.05), (0.06 * big, -0.045), (0.07 * big, 0.0), (0.06 * big, 0.06), (0.0, 0.07)], mat, segs=10, smooth=not o.get("stone"), sx=1.0, sy=0.8), (-0.0, 0, 0))]
    out.append(xform(lathe([(0.05 * big, 0), (0.06 * big, 0.1 * big), (0.055 * big, 0.14 * big), (0.0, 0.15 * big)], mat, segs=10, smooth=False), (0, -0.06 * big, 0), rx(90)))
    for k in range(4):
        out.append(box((0.03 * big, 0.03 * big, 0.025 * big), (0.04 * big, 0.04 * big, -0.03 + k * 0.025), m["glow"] if o.get("ember") else mat, bevel=0.005))
    return out

def w_net(m, o):
    out = [grip(m, -0.06, 0.06, r=0.016, mat=m["string"])]
    N = 7
    grid = [[Vector((0.02 + 0.03 * math.sin(i * 1.3 + j), -0.05 * (j - N / 2) * (0.4 + i / N), -0.06 - 0.08 * i)) for j in range(N + 1)] for i in range(N)]
    for i in range(N):
        out.append(tube(grid[i], 0.003, m["string"], segs=3))
    for j in range(N + 1):
        out.append(tube([grid[i][j] for i in range(N)], 0.003, m["string"], segs=3))
    for j in range(0, N + 1, 2): out.append(xform(lathe([(0, -0.015), (0.015, 0), (0, 0.015)], m["iron"], segs=6), grid[-1][j]))
    return out

def w_sling(m, o):
    out = [tube([(0, 0, 0.03), (0.02, -0.02, -0.15), (0.03, -0.06, -0.3)], 0.004, m["string"], segs=4)]
    out.append(xform(lathe([(0, -0.03), (0.035, -0.02), (0.04, 0.0), (0.035, 0.015), (0, 0.02)], m["leather"], segs=10), (0.03, -0.06, -0.32)))
    out.append(xform(lathe([(0, -0.02), (0.02, 0), (0, 0.02)], m["stone"], segs=8), (0.03, -0.06, -0.31)))
    out.append(tube([(0.03, -0.06, -0.3), (0.0, 0.03, -0.15), (-0.01, 0.0, 0.03)], 0.004, m["string"], segs=4))
    out.append(torus(0.015, 0.004, m["string"], loc=(0, 0, 0.03), rot=ry(90), seg=8, mseg=3))
    return out

def w_blowpipe(m, o):
    out = [lathe([(0.012, -0.3), (0.016, -0.3), (0.014, 0.6), (0.018, 0.62), (0.012, 0.62)], m["wood"], segs=10)]
    out += bands(m, [-0.25, 0.1, 0.5], r=0.016)
    out.append(tube([(0, 0, 0.4), (0.04, 0.03, 0.3), (0.03, 0.0, 0.15)], 0.003, m["acc"], segs=4))
    return out

def w_javelin(m, o):
    out = [shaft(m, -0.5, 0.85, r=0.012, mat=m["gold"]), leaf_head(m, 0.85, L=0.16, W=0.025, mat=m["gold"]), grip(m, -0.07, 0.07, r=0.014)]
    return out

def w_cards(m, o):
    out = []
    for k in range(5):
        c = box((0.003, 0.06, 0.09), (0, 0, 0.06), m["acc"] if k % 2 else m["metal"], bevel=0.002)
        c.data.transform(Matrix.Rotation(math.radians(-24 + 12 * k), 4, "X"))
        out.append(c)
        g = box((0.0035, 0.02, 0.02), (0, 0, 0.08), m["glow"], bevel=0); g.data.transform(Matrix.Rotation(math.radians(-24 + 12 * k), 4, "X")); out.append(g)
    return out

def w_boomerang(m, o):
    arc = [(0, -0.22 * math.cos(a), 0.06 + 0.22 * math.sin(a) * 0.6) for a in [math.radians(x) for x in range(10, 171, 16)]]
    return [tube(arc, [0.012, 0.016, 0.02, 0.022, 0.022, 0.022, 0.02, 0.016, 0.012, 0.01, 0.01][:len(arc)], m["wood"], segs=6, sq=0.35),
            tube([arc[2], arc[3]], 0.023, m["acc"], segs=6, sq=0.36)]

def w_chakram(m, o):
    out = [torus(0.22, 0.016, m["glow"] if o.get("ice") else m["metal"], loc=(0, 0.0, 0.2), rot=ry(90), seg=28, mseg=5, sx=1, sy=1)]
    if o.get("film"): out.append(xform(lathe([(0, -0.002), (0.21, -0.001), (0.21, 0.001), (0, 0.002)], m["glow"], segs=24), (0, 0, 0.2), ry(90)))
    out.append(box((0.03, 0.03, 0.1), (0, 0, 0.0), m["leather"], bevel=0.008))
    for k in range(8):
        a = k / 8 * TAU
        s = lathe([(0.01, 0), (0, 0.04)], m["glow"] if o.get("ice") else m["metal"], segs=4, smooth=False)
        s.data.transform(Matrix.Translation((0, math.cos(a) * 0.23, 0.2 + math.sin(a) * 0.23)) @ Matrix.Rotation(-a + math.pi / 2, 4, "X"))
        out.append(s)
    if o.get("moon"): out.append(xform(lathe([(0, -0.045), (0.035, -0.035), (0.045, 0), (0.035, 0.035), (0, 0.045)], m["glow"], segs=12), (0.04, 0.24, 0.42)))
    return out

def w_ribbon(m, o):
    out = [lathe([(0, -0.1), (0.012, -0.1), (0.01, 0.25), (0, 0.27)], m["wood"], segs=8)]
    pts = [Vector((0.04 * math.sin(t * 7), -0.12 * math.sin(t * 3.0), 0.27 + 0.5 * t + 0.1 * math.sin(t * 5))) for t in [i / 20 for i in range(21)]]
    out.append(tube(pts, 0.02, m["glow"], segs=4, sq=0.15))
    return out

def w_umbrella(m, o):
    out = [lathe([(0, -0.1), (0.014, -0.1), (0.012, 0.9), (0, 0.92)], m["iron"], segs=8), grip(m, -0.12, 0.06, r=0.018)]
    out.append(tube([(0, 0, -0.12), (0, -0.04, -0.2), (0, -0.09, -0.18), (0, -0.09, -0.12)], 0.016, m["darkwood"], segs=6))
    out.append(lathe([(0.012, 0.2), (0.05, 0.35), (0.06, 0.6), (0.04, 0.8), (0.012, 0.85)], m["main"], segs=8, smooth=False))
    for k in range(8):
        a = k / 8 * TAU
        out.append(tube([(math.cos(a) * 0.014, math.sin(a) * 0.014, 0.2), (math.cos(a) * 0.06, math.sin(a) * 0.06, 0.55), (math.cos(a) * 0.04, math.sin(a) * 0.04, 0.82)], 0.0035, m["metal"], segs=3))
    out.append(leaf_head(m, 0.9, L=0.12, W=0.012))
    return out

def w_shears(m, o):
    out = []
    for s in (-1, 1):
        out.append(blade([(0.08, 0.025, 0.006, s * 0.012), (0.25, 0.02, 0.005, s * 0.008), (0.34, 0.002, 0.002, 0.0)], m["metal"], single=True))
    out.append(torus(0.045, 0.01, m["iron"], loc=(0, 0, 0.0), rot=ry(90), seg=12, mseg=4, sx=0.6))
    out.append(tube([(0, -0.02, 0.02), (0, -0.025, 0.08)], 0.008, m["iron"], segs=5))
    out.append(tube([(0, 0.02, 0.02), (0, 0.025, 0.08)], 0.008, m["iron"], segs=5))
    return out

def w_smoker(m, o):
    out = [lathe([(0, -0.1), (0.05, -0.1), (0.055, 0.1), (0.035, 0.15), (0.015, 0.2), (0.012, 0.24), (0, 0.24)], m["acc"], segs=14)]
    out[0].data.transform(Matrix.Translation((0, 0.1, 0.0)))
    out.append(box((0.03, 0.08, 0.16), (0, -0.0, -0.02), m["leather"], bevel=0.01))
    out.append(xform(lathe([(0, 0), (0.02, 0.03), (0.0, 0.1)], m["paper"], segs=6), (0, 0.1, 0.25)))
    return out

def w_barrel(m, o):
    out = [lathe([(0, -0.18), (0.12, -0.18), (0.145, 0.0), (0.12, 0.18), (0, 0.18)], m["wood"], segs=16)]
    out[0].data.transform(Matrix.Translation((0.0, 0.18, -0.05)))
    for z in (-0.17, -0.05, 0.07): out.append(torus(0.14 if z == -0.05 else 0.125, 0.008, m["iron"], loc=(0, 0.18, z), seg=16, mseg=4))
    out.append(tube([(0, 0.04, 0.12), (0, -0.02, 0.06), (0, -0.02, -0.12), (0, 0.04, -0.16)], 0.012, m["string"], segs=5))
    return out

def w_drum(m, o):
    out = [lathe([(0.0, -0.06), (0.14, -0.06), (0.15, 0.0), (0.14, 0.06), (0, 0.06)], m["wood"], segs=18)]
    out[0].data.transform(Matrix.Translation((0, 0.22, -0.12)) @ Matrix.Rotation(math.pi / 2, 4, "Y"))
    out.append(xform(lathe([(0, 0.0), (0.141, 0.0), (0.0, 0.002)], m["paper"], segs=18), (0.061, 0.22, -0.12), ry(90)))
    out.append(lathe([(0, -0.08), (0.01, -0.08), (0.012, 0.25), (0.02, 0.27), (0.0, 0.29)], m["wood"], segs=8))
    out.append(tube([(0, 0.0, -0.02), (0.02, 0.1, -0.03), (0.0, 0.22, -0.06)], 0.008, m["leather"], segs=4, sq=0.3))   # the drum's strap to the wrist
    return out

def w_marionette(m, o):
    out = [box((0.02, 0.2, 0.02), (0, 0, 0.06), m["wood"], bevel=0.004), box((0.02, 0.02, 0.16), (0, 0, 0.06), m["wood"], bevel=0.004)]
    body = Vector((0.05, 0.0, -0.45))
    for s in (-1, 0, 1): out.append(tube([(0, s * 0.1, 0.06), body + Vector((0, s * 0.05, 0.14 if s == 0 else 0.06))], 0.0015, m["string"], segs=3))
    out.append(xform(lathe([(0, -0.07), (0.03, -0.06), (0.035, 0.03), (0.02, 0.06), (0, 0.06)], m["acc"], segs=8), body))
    out.append(xform(lathe([(0, -0.025), (0.025, 0), (0, 0.025)], m["paper"], segs=8), body + Vector((0, 0, 0.09))))
    for s in (-1, 1): out.append(tube([body + Vector((0, s * 0.03, 0.04)), body + Vector((0, s * 0.07, -0.04))], 0.008, m["paper"], segs=4))
    return out

def w_orrery(m, o):
    c = Vector((0.06, 0, 0.18))
    out = [xform(lathe([(0, -0.04), (0.03, -0.03), (0.04, 0), (0.03, 0.03), (0, 0.04)], m["glow"], segs=12), c)]
    for k, (R, tilt) in enumerate(((0.08, 15), (0.12, -25), (0.16, 40))):
        out.append(torus(R, 0.004, m["gold"], loc=c, rot=rx(tilt) @ ry(10 * k), seg=24, mseg=4))
        p = (rx(tilt) @ ry(10 * k)) @ Vector((R, 0, 0))
        out.append(xform(lathe([(0, -0.016), (0.014, 0), (0, 0.016)], m["acc"], segs=8), c + p))
    out.append(lathe([(0, -0.08), (0.014, -0.08), (0.012, 0.1), (0, 0.11)], m["gold"], segs=8))
    out.append(tube([(0, 0, 0.1), (0.06, 0, 0.14)], 0.006, m["gold"], segs=5))
    return out

def w_shuttle(m, o):
    sh = lathe([(0, -0.02), (0.015, 0.0), (0.02, 0.1), (0.015, 0.2), (0, 0.24)], m["metal"], segs=10, sx=1.0, sy=0.5)
    return [sh, box((0.005, 0.012, 0.12), (0.012, 0, 0.11), m["glow"], bevel=0), tube([(0.01, 0, 0.11), (0.02, -0.06, 0.3), (0.0, -0.02, 0.5)], 0.002, m["glow"], segs=3)]

def w_kite(m, o):
    out = [lathe([(0, -0.06), (0.025, -0.06), (0.025, 0.06), (0, 0.06)], m["wood"], segs=10)]
    out.append(torus(0.024, 0.01, m["string"], loc=(0, 0, 0), seg=10, mseg=4))
    k = Vector((0.1, -0.35, 0.75))
    out.append(tube([(0, 0, 0.06), (0.05, -0.15, 0.4), k], 0.0015, m["string"], segs=3))
    kp = yz_plate([(0.22, 0), (0.05, 0.13), (-0.25, 0), (0.05, -0.13)], 0.004, m["main"], bevel=0)
    kp.data.transform(Matrix.Translation(k)); out.append(kp)
    out.append(tube([k + Vector((0, 0, -0.25)), k + Vector((0.02, 0.06, -0.45)), k + Vector((0.0, 0.02, -0.65))], 0.006, m["acc"], segs=3, sq=0.2))
    return out

def w_fan(m, o):
    """A folding war fan (petal blades) or a great feather fan, opening upward from the grip."""
    big = o.get("big", False); out = [grip(m, -0.07, 0.06, r=0.014)]
    R = 0.45 if big else 0.3; n = 9 if big else 11
    for k in range(n):
        a = math.radians(-60 + 120 * k / (n - 1))
        if big:
            st = [(0.06 + R * t, 0.06 * math.sin(math.pi * min(1, t * 1.15)) + 0.004, 0.003, 0) for t in [i / 8 for i in range(9)]]
            f = blade(st, m["paper"] if k % 2 else m["acc"])
        else:
            st = [(0.06, 0.008, 0.002, 0), (0.06 + R * 0.7, 0.03, 0.0025, 0), (0.06 + R, 0.002, 0.001, 0)]
            f = blade(st, m["main"] if k % 2 else m["acc"])
        f.data.transform(Matrix.Rotation(a, 4, "X")); out.append(f)
        if not big:
            e = blade([(0.06 + R * 0.75, 0.016, 0.003, 0), (0.06 + R * 1.02, 0.002, 0.001, 0)], m["metal"]); e.data.transform(Matrix.Rotation(a, 4, "X")); out.append(e)
    out.append(torus(0.012, 0.004, m["metal"], loc=(0, 0, 0.06), rot=ry(90), seg=8, mseg=3))
    return out

def w_twig(m, o):
    out = [tube([(0, 0, -0.12), (0.005, 0.0, 0.1), (0.0, 0.01, 0.28)], [0.012, 0.01, 0.006], m["wood"], segs=6)]
    for s in (-1, 1): out.append(tube([(0, 0.0, 0.2), (0, s * 0.06, 0.32)], [0.006, 0.003], m["wood"], segs=5))
    for k in range(6):
        lf = blade2d([(0.0, 0.0), (0.018, 0.02), (0.0, 0.05), (-0.018, 0.02)], 0.003, m["acc"])
        lf.data.transform(Matrix.Translation((0, (-1) ** k * 0.02, 0.12 + 0.03 * k)) @ Matrix.Rotation((-1) ** k * 0.8, 4, "X")); out.append(lf)
    out.append(torus(0.03, 0.012, m["darkwood"], loc=(0, 0, 0.3), seg=10, mseg=5))
    out.append(xform(lathe([(0, -0.012), (0.01, 0), (0, 0.012)], m["paper"], segs=6), (0.0, 0.0, 0.305)))
    return out

def w_quiver_free(m, o): return []

def lantern(m, c, s=1.0, hang=False):
    c = Vector(c); out = []
    top = c + Vector((0, 0, -0.03 * s)) if hang else c
    body = top + Vector((0, 0, -0.1 * s)) if hang else c + Vector((0, 0, 0.1 * s))
    out.append(torus(0.025 * s, 0.004 * s, m["iron"], loc=top + Vector((0, 0, 0.0)), rot=ry(90), seg=10, mseg=4))
    out.append(xform(lathe([(0, 0.0), (0.06, 0.0), (0.03, 0.04), (0.0, 0.05)], m["iron"], segs=8, smooth=False), body + Vector((0, 0, 0.07 * s)), Matrix.Scale(s, 3)))
    out.append(xform(lathe([(0, -0.07), (0.055, -0.07), (0.055, -0.06), (0, -0.06)], m["iron"], segs=8, smooth=False), body, Matrix.Scale(s, 3)))
    for k in range(4):
        a = (k + 0.5) / 4 * TAU
        out.append(tube([body + Vector((math.cos(a) * 0.055 * s, math.sin(a) * 0.055 * s, -0.065 * s)), body + Vector((math.cos(a) * 0.055 * s, math.sin(a) * 0.055 * s, 0.07 * s))], 0.004 * s, m["iron"], segs=4))
    out.append(xform(lathe([(0, -0.05), (0.035, -0.035), (0.04, 0.0), (0.035, 0.035), (0, 0.05)], m["glow"], segs=10), body, Matrix.Scale(s, 3)))
    return out

def bell(m, c, s=1.0, glow=False):
    c = Vector(c)
    out = [xform(lathe([(0.0, 0.0), (0.03, 0.0), (0.04, -0.03), (0.055, -0.1), (0.085, -0.15), (0.088, -0.165), (0.0, -0.165)], m["glow"] if glow else m["gold"], segs=18), c, Matrix.Scale(s, 3))]
    out.append(xform(lathe([(0, -0.02), (0.015, -0.01), (0.0, 0.0)], m["iron"], segs=8), c + Vector((0, 0, -0.16 * s)), Matrix.Scale(s, 3)))
    return out

def book(m, c, open_=False):
    c = Vector(c); out = []
    if open_:
        for s in (-1, 1):
            out.append(box((0.012, 0.11, 0.15), c + Vector((0, s * 0.06, 0)), m["acc"], bevel=0.004, rot=rz(-12 * s)))
            out.append(box((0.03, 0.1, 0.14), c + Vector((0.015, s * 0.055, 0.0)), m["paper"], bevel=0.003, rot=rz(-12 * s)))
            out.append(box((0.002, 0.07, 0.1), c + Vector((0.032, s * 0.055, 0.0)), m["glow"], bevel=0, rot=rz(-12 * s)))
    else:
        out.append(box((0.05, 0.16, 0.22), c, m["acc"], bevel=0.008))
        out.append(box((0.044, 0.165, 0.205), c + Vector((0.004, 0.003, 0)), m["paper"], bevel=0.003))
        out.append(box((0.054, 0.04, 0.05), c + Vector((0, 0.07, 0)), m["metal"], bevel=0.004))
        out.append(gem(m, c + Vector((-0.028, 0, 0)), 0.02))
    return out

def w_focus(m, o):
    """Handheld focus items: lantern, bell, book, mirror, orb, candle, harp, lute, conch, lens, panpipes, wand."""
    k = o.get("kind", "orb"); out = []
    if k == "lantern":
        out += [tube([(0, 0, 0.0), (0, 0, -0.05)], 0.004, m["iron"], segs=4)]
        out += lantern(m, (0, 0, -0.05), s=1.1, hang=True)
        out.append(lathe([(0, -0.02), (0.012, -0.02), (0.012, 0.05), (0, 0.05)], m["wood"], segs=8))
        if o.get("moth"):
            for s in (-1, 1): out.append(xform(blade2d([(0, 0), (s * 0.08, 0.05), (s * 0.06, -0.04)], 0.003, m["acc"]), (0, 0, -0.16)))
    elif k == "bell":
        out.append(lathe([(0, -0.02), (0.016, -0.02), (0.014, 0.1), (0.022, 0.12), (0, 0.13)], m["wood"], segs=10))
        out += bell(m, (0, 0, -0.02), s=1.3)
    elif k == "book":
        out += book(m, (0.04, 0, 0.02))
    elif k == "mirror":
        out.append(lathe([(0, -0.1), (0.014, -0.1), (0.012, 0.06), (0.02, 0.07), (0, 0.075)], m["metal"], segs=10))
        out.append(xform(lathe([(0, -0.012), (0.1, -0.01), (0.11, 0.0), (0.1, 0.01), (0, 0.012)], m["metal"], segs=24, sx=0.72, sy=1.0), (0, 0, 0.18), ry(90)))
        out.append(xform(lathe([(0, 0.011), (0.095, 0.011), (0.0, 0.013)], m["glow"], segs=24, sx=0.72), (0, 0, 0.18), ry(90)))
        out[-1].data.transform(Matrix.Rotation(0, 4, "Z"))
    elif k == "orb":
        out.append(lathe([(0, -0.08), (0.016, -0.08), (0.014, 0.06), (0, 0.07)], m["metal"], segs=8))
        out += prongs(m, 0.06, 4, 0.06)
        out.append(lathe([(0, 0.06), (0.05, 0.075), (0.065, 0.12), (0.05, 0.17), (0, 0.185)], o.get("omat", m["glow"]), segs=16))
        if o.get("orbit"):
            for k2 in range(3): out.append(xform(lathe([(0, -0.025), (0.02, 0), (0, 0.025)], m["glow"], segs=8), (0.05 * math.cos(k2 * 2.1), 0.15 * math.sin(k2 * 2.1), 0.15 + 0.08 * k2)))
    elif k == "candle":
        L = o.get("len", 0.5)
        out.append(lathe([(0, -0.1), (0.016, -0.1), (0.018, 0.0), (0.012, L * 0.9), (0.0, L)], m["paper"], segs=10))
        out.append(lathe([(0, 0.0), (0.04, 0.0), (0.045, 0.012), (0.0, 0.015)], m["gold"], segs=12))
        out.append(flame(m, (0, 0, L - 0.005), h=0.07, r=0.016))
        for z in (0.2, 0.32): out.append(tube([(0.013, 0, z), (0.016, 0, z - 0.05)], [0.005, 0.002], m["paper"], segs=4))
    elif k == "harp":
        fr = [(0, 0, -0.08), (0, 0.02, 0.15), (0, 0.06, 0.3), (0, 0.16, 0.38), (0, 0.26, 0.32), (0, 0.28, 0.2)]
        out.append(tube(fr, 0.013, m["glow"] if o.get("light") else m["gold"], segs=6))
        out.append(tube([(0, 0.28, 0.2), (0, 0.18, 0.02), (0, 0.04, -0.08), (0, 0, -0.08)], 0.016, m["gold"], segs=6))
        for i in range(6):
            y = 0.04 + i * 0.04
            zt = 0.3 + 0.08 * math.sin(math.pi * (y / 0.3)) - 0.04; zb = -0.06 + 0.1 * (y / 0.28) * 0.9
            out.append(tube([(0, y, zb), (0, y, zt)], 0.002, m["glow"], segs=3))
            if o.get("light"): out.append(gem(m, (0, y, (zb + zt) / 2), 0.008))
    elif k == "lute":
        out.append(xform(lathe([(0, -0.02), (0.1, -0.015), (0.14, 0.06), (0.12, 0.16), (0.06, 0.22), (0, 0.24)], m["wood"], segs=16, sx=1.0, sy=0.55), (0.05, 0.0, -0.32), Matrix.Identity(3)))
        out.append(box((0.016, 0.04, 0.4), (0.0, 0, 0.08), m["darkwood"], bevel=0.004))
        out.append(box((0.03, 0.05, 0.08), (-0.01, 0, 0.3), m["darkwood"], bevel=0.006, rot=ry(-20)))
        out.append(xform(lathe([(0, 0), (0.03, 0), (0, 0.002)], m["darkwood"], segs=14), (0.09, 0, -0.16), ry(90)))
        for y in (-0.012, -0.004, 0.004, 0.012): out.append(tube([(0.012, y, -0.25), (0.012, y, 0.28)], 0.0012, m["string"], segs=3))
    elif k == "conch":
        pts = []; rad = []
        for i in range(18):
            t = i / 17; a = t * 2.4 * math.pi
            r = 0.13 * (1 - t) + 0.01
            pts.append((0.06 + math.cos(a) * r * 0.4, math.sin(a) * r * 0.6, 0.02 + t * 0.3)); rad.append(0.075 * (1 - t) ** 1.2 + 0.006)
        out.append(tube(pts, rad, m["paper"], segs=10))
        out.append(xform(lathe([(0.0, 0.0), (0.07, 0.0), (0.08, -0.02), (0.0, -0.01)], m["acc"], segs=12), pts[0]))
        for i in range(3, 15, 3): out.append(xform(lathe([(0.012, 0), (0, 0.03)], m["paper"], segs=5, smooth=False), Vector(pts[i]) + Vector((0, rad[i] * 0.9, 0)), rx(-90)))
        if o.get("great"):
            for ob in out: ob.data.transform(Matrix.Scale(1.4, 4))
    elif k == "lens":
        out.append(torus(0.08, 0.012, m["gold"], loc=(0, 0, 0.16), rot=ry(90), seg=20, mseg=5))
        out.append(xform(lathe([(0, -0.016), (0.08, 0), (0, 0.016)], m["glow"], segs=16), (0, 0, 0.16), ry(90)))
        out.append(lathe([(0, -0.08), (0.014, -0.08), (0.012, 0.08), (0, 0.09)], m["gold"], segs=8))
    elif k == "panpipes":
        for i in range(7):
            h = 0.22 - 0.022 * i
            out.append(xform(lathe([(0.0, 0.0), (0.012, 0.0), (0.012, h), (0.009, h), (0.009, 0.01), (0, 0.01)], m["wood"], segs=8), (0.04, -0.07 + 0.024 * i, 0.15 - h)))
        out.append(box((0.03, 0.18, 0.02), (0.04, 0.003, 0.08), m["string"], bevel=0.004))
    elif k == "wand":
        out.append(lathe([(0, -0.1), (0.014, -0.1), (0.012, 0.25), (0.0, 0.27)], m["wood"], segs=8))
        out.append(gem(m, (0, 0, 0.28), 0.02))
    return out

# ---------------------------------------------------------------- classifier

def _has(t, *ks):
    return any(re.search(r"\b" + k + r"(e?s)?\b", t) for k in ks)

def _main_weapon_text(w):
    """'tower shield and arming sword' -> 'arming sword'; keeps the non-shield part."""
    w = w.lower()
    flat = re.sub(r"\([^)]*\)", "", w)                    # "umbrella (shield and blade)" is one weapon
    parts = re.split(r"\band\b|,", flat)
    if not any(_has(p, "shield", "buckler", "pavise") for p in parts): return w
    keep = [p for p in parts if not _has(p, "shield", "buckler", "pavise")]
    return " ".join(keep).strip()

RULES = [
    # (keywords, type, opts) in priority order; matched against the weapon text (shield part removed)
    (("fists", "bare"), "none", {}),
    (("gauntlet",), "fist", {"ember": True}),
    (("stone fist",), "fist", {"stone": True, "big": 1.8}),
    (("chain-whip", "chain whip"), "whip", {"chain": True, "burn": True}),
    (("thorn-vine whip", "vine whip"), "whip", {"thorn": True}),
    (("whip",), "whip", {}),
    (("meteor hammer",), "flail", {"haft": False, "kind": "ball", "spikes": False, "molten": True}),
    (("ball and chain",), "flail", {"haft": False, "kind": "shackle"}),
    (("chained orb",), "flail", {"haft": False, "kind": "orb"}),
    (("censer",), "flail", {"haft": False, "kind": "censer"}),
    (("key ring",), "flail", {"kind": "keys", "haft": True}),
    (("anchor flail",), "flail", {"kind": "anchor", "haft": False}),
    (("anchor",), "anchor", {"rope": True}),
    (("net",), "net", {}),
    (("crossbow",), "crossbow", {}),
    (("great bow", "longbow", "recurve bow", "bow"), "bow", {}),
    (("sling",), "sling", {}),
    (("blowpipe",), "blowpipe", {}),
    (("javelin",), "javelin", {}),
    (("cards",), "cards", {}),
    (("boomerang",), "boomerang", {}),
    (("chakram",), "chakram", {"ice": True, "film": True}),
    (("hoop",), "chakram", {"moon": True}),
    (("ribbon",), "ribbon", {}),
    (("umbrella",), "umbrella", {}),
    (("shears",), "shears", {}),
    (("smoker",), "smoker", {}),
    (("barrel",), "barrel", {}),
    (("drum",), "drum", {}),
    (("marionette",), "marionette", {}),
    (("orrery",), "orrery", {}),
    (("shuttle",), "shuttle", {}),
    (("kite",), "kite", {}),
    (("tankard",), "tankard", {}),
    (("war-fan", "war fan"), "fan", {}),
    (("feather fan",), "fan", {"big": True}),
    (("twig wand",), "twig", {}),
    (("candle wand", "taper"), "focus", {"kind": "candle", "len": 0.62}),
    (("moth-shaped candle", "moth"), "focus", {"kind": "lantern", "moth": True}),
    (("candlestick staff",), "staff", {"head": "candle"}),
    (("torch",), "staff", {"head": "torch"}),
    (("lantern",), "focus", {"kind": "lantern"}),
    (("hand-bell", "hand bell"), "focus", {"kind": "bell"}),
    (("bell maul",), "maul", {"head": "bell"}),
    (("bell on a reed pole", "reed pole"), "staff", {"head": "pole_bell"}),
    (("mirror",), "focus", {"kind": "mirror"}),
    (("harp-trident",), "trident", {"strings": True}),
    (("harp",), "focus", {"kind": "harp", "light": True}),
    (("lute",), "focus", {"kind": "lute"}),
    (("great conch",), "focus", {"kind": "conch", "great": True}),
    (("conch",), "focus", {"kind": "conch"}),
    (("panpipes",), "focus", {"kind": "panpipes"}),
    (("void orbs",), "focus", {"kind": "orb", "orbit": True}),
    (("orb",), "focus", {"kind": "orb"}),
    (("fire-tome staff", "tome staff"), "staff", {"head": "tome"}),
    (("lens staff", "lighthouse-lens"), "staff", {"head": "lens"}),
    (("quill-pen", "quill"), "staff", {"head": "quill"}),
    (("fuse staff", "rocket"), "staff", {"head": "rocket"}),
    (("wind chime", "wind chimes", "chime staff"), "staff", {"head": "crook", "gnarl": 0.04, "chimes": True}),
    (("bone-chime", "bone staff"), "staff", {"head": "bone"}),
    (("ice-crystal", "crystal staff"), "staff", {"head": "crystal", "smat": "metal"}),
    (("water globe", "driftwood"), "staff", {"head": "fork", "gnarl": 0.012}),
    (("gold-bound",), "staff", {"head": "disc", "smat": "darkwood"}),
    (("standing-stone",), "staff", {"head": "stone"}),
    (("sunflower",), "staff", {"head": "sunflower"}),
    (("perch staff",), "staff", {"head": "owl"}),
    (("bog-oak",), "staff", {"head": "lily"}),
    (("eclipse sceptre",), "staff", {"head": "sceptre", "len": 1.0, "smat": "darkwood"}),
    (("lightning-rod", "standard"), "staff", {"head": "standard"}),
    (("crook",), "staff", {"head": "crook", "lantern": True}),
    (("lamplighter",), "staff", {"head": "pole_lamp", "smat": "darkwood"}),
    (("brew-pole", "kettle"), "staff", {"head": "pole_kettle"}),
    (("quarterstaff",), "staff", {"head": "plain", "shod": True, "len": 2.0}),
    (("walking staff",), "staff", {"head": "plain", "gold": True, "len": 1.4}),
    (("trap-pole",), "fork", {"kind": "trap"}),
    (("pitchfork",), "fork", {"kind": "pitchfork"}),
    (("rake",), "fork", {"kind": "rake"}),
    (("poker",), "fork", {"kind": "poker"}),
    (("branding iron",), "fork", {"kind": "brand"}),
    (("grave-hook",), "glaive", {"kind": "hook", "barbs": True}),
    (("boat-hook",), "glaive", {"kind": "hook"}),
    (("billhook",), "glaive", {"kind": "billhook"}),
    (("scythe",), "glaive", {"kind": "scythe"}),
    (("halberd",), "glaive", {"kind": "halberd"}),
    (("glaive",), "glaive", {"kind": "glaive"}),
    (("trident",), "trident", {}),
    (("harpoon",), "spear", {"head": "harpoon", "rope": True}),
    (("lance",), "spear", {"head": "lance", "lance": True}),
    (("spear",), "spear", {"head": "leaf"}),
    (("peel",), "peel", {}),
    (("spade", "shovel"), "spade", {}),
    (("pickaxe",), "pickaxe", {}),
    (("ice saw",), "sword", {"len": 0.7, "width": 0.05, "single": True, "guard": "none", "handle": 0.18}),
    (("axe", "hatchet"), "axe", {}),
    (("ladle",), "mace", {"kind": "ladle"}),
    (("gate-key", "key"), "mace", {"kind": "key"}),
    (("sceptre",), "mace", {"kind": "sceptre"}),
    (("mace",), "mace", {"kind": "flanged"}),
    (("mallet",), "hammer", {"head": "sack", "haft": 0.55}),
    (("smith",), "hammer", {"head": "smith", "glow": True}),
    (("marble sun hammer", "marble sun-hammer"), "hammer", {"head": "marble_sun", "big": 1.1, "haft": 0.75}),
    (("sun-headed",), "hammer", {"head": "sun"}),
    (("maul",), "maul", {}),
    (("hammer",), "hammer", {}),
    (("pillar",), "club", {"kind": "pillar"}),
    (("coral-tipped",), "club", {"kind": "coral"}),
    (("crank",), "club", {"kind": "crank"}),
    (("brush pole",), "club", {"kind": "brush"}),
    (("knobbly", "club", "cudgel"), "club", {"kind": "knobbly"}),
    (("cane",), "staff", {"head": "plain", "len": 1.0, "gold": True, "smat": "darkwood"}),
    (("crescent dagger", "crescent daggers"), "dagger", {"crescent": True}),
    (("hooked knives", "hooked knife"), "dagger", {"hook": True}),
    (("dagger", "knife", "knives"), "dagger", {}),
    (("sickle",), "sickle", {}),
    (("flamberge",), "sword", {"wavy": True, "len": 1.05, "width": 0.045, "glow_edge": True}),
    (("katana",), "sword", {"curve": 0.05, "single": True, "guard": "tsuba", "len": 0.82, "width": 0.03, "handle": 0.26, "fuller": False}),
    (("rapier",), "sword", {"len": 0.95, "width": 0.014, "guard": "cup", "fuller": False}),
    (("flaming cutlass", "burning cutlass"), "sword", {"curve": 0.08, "single": True, "guard": "knuckle", "len": 0.7, "width": 0.035, "glow_edge": True, "flames": True}),
    (("cutlass", "sabre", "saber"), "sword", {"curve": 0.08, "single": True, "guard": "knuckle", "len": 0.7, "width": 0.035}),
    (("lava-veined greatsword", "molten greatsword"), "sword", {"len": 1.25, "width": 0.075, "handle": 0.3, "guard_w": 0.18, "cracks": True, "glow_edge": True, "fuller": False, "dark": True}),
    (("greatsword",), "sword", {"len": 1.25, "width": 0.07, "handle": 0.3, "guard_w": 0.16}),
    (("skate-blade",), "sword", {"len": 0.9, "width": 0.03, "single": True, "curve": -0.02, "guard": "cross"}),
    (("longsword",), "sword", {"len": 1.0, "width": 0.042, "handle": 0.24}),
    (("short", "broad"), "sword", {"len": 0.62, "width": 0.05}),
    (("sword",), "sword", {}),
    (("wand",), "focus", {"kind": "wand"}),
    (("staff",), "staff", {"head": "plain"}),
]

def classify(c):
    """(type, opts) for the design's main hand prop. Every roster design maps through RULES; a design
    matching nothing is reported (print) and given a plain staff."""
    w = _main_weapon_text(str(c.get("weapon", "")))
    if not w or not any(_has(w, *k) or any(x in w for x in k if " " in x or "-" in x) for k, _, _ in RULES):
        # the weapon line names only a shield (or a horn): the main-hand weapon from the kit list, else bare-handed
        kitw = [str(k).lower().replace("custom:", "") for k in c.get("kit", []) if not _has(str(k).lower(), "shield", "buckler", "pavise")]
        hit = next((k for k in kitw if any(_has(k, *keys) for keys, _, _ in RULES if keys[0] not in ("fists", "net", "kite", "drum"))), None)
        if hit: w = hit
        elif _has(w, "horn"): return "focus", {"kind": "conch"}
        elif not w or _has(str(c.get("weapon", "")).lower(), "shield"): return "none", {}
    for keys, typ, opts in RULES:
        if _has(w, *keys) or any(k in w for k in keys if " " in k or "-" in k):
            o = dict(opts)
            # keyword variants
            if typ == "bow":
                if _has(w, "recurve", "short"): o.update(half=0.45, recurve=0.06)
                if _has(w, "great", "longbow"): o.update(half=0.8)
                if _has(w, "crystal"): o.update(crystal=True, mat="glow")
                if _has(w, "reed"): o.update(reed=True)
            if typ == "crossbow" and _has(w, "firethorn", "thorn"): o.update(thorn=True, ember=True)
            if typ == "axe":
                if _has(w, "war", "bearded"): o.update(bearded=True, haft=0.75, big=1.15)
                if _has(w, "hatchet"): o.update(haft=0.42, big=0.75)
                if _has(w, "boarding"): o.update(haft=0.55, big=0.95)
            if typ == "pickaxe" and _has(w, "crystal"): o.update(crystal=True)
            if typ == "maul":
                if _has(w, "slag", "lava", "volcanic"): o.update(lava=True, hmat="iron")
            if typ == "hammer":
                if _has(w, "war"): o.update(head="block", big=1.15, hmat="stone" if _has(w, "basalt") else "metal")
            if typ == "mace":
                if _has(w, "runed", "sun-forged", "runes"): o.update(runes=True)
                if _has(w, "sun"): o.update(sun=True)
            if typ == "spear":
                if _has(w, "boar", "crossbar"): o.update(lugs=True, len=2.3, hs=1.2)
                if _has(w, "slim", "sky"): o.update(r=0.013, len=2.1, smat="metal")
                if _has(w, "sun-disc", "sun"): o.update(ring=True)
                if _has(w, "star"): o.update(head="star", smat="glow")
                if _has(w, "boarding"): o.update(len=1.5)
                if _has(w, "whaling") or "whaling" in str(c.get("kit", "")): o.update(len=2.2)
            if typ == "glaive":
                if _has(w, "wave", "curl"): o.update(curl=0.06)
                if _has(w, "gale", "leaf"): o.update(curl=0.0)
                if "sunrise" in str(c.get("kit", "")).lower(): o.update(sunrise=True, bmat="gold")
            if typ == "trident" and _has(w, "coral"): o.update(hmat="acc")
            if typ == "sword":
                if _has(w, "crescent"): o.update(crescent=True)
                if _has(w, "thorn", "blackthorn"): o.update(thorns=True)
                if _has(w, "cracked", "slab"): o.update(cracks=True, width=0.09)
                if _has(w, "light", "shining", "sun"): o.update(glow_edge=True)
                if _has(w, "swordfish"): o.update(bmat="paper")
                if _has(w, "gold"): o.update(bmat="gold")
                if _has(w, "storm", "burning"): o.update(glow_edge=True)
            if typ == "club" and _has(w, "spiked"): o.update(kind="spiked")
            if typ == "spade":
                if "lantern" in str(c.get("kit", "")).lower(): o.update(lantern=True)
                if _has(w, "shovel"): o.update(shovel=True)
            if typ == "focus" and o.get("kind") == "orb" and _has(w, "abyss", "black", "void"): o.update(omat="iron")
            return typ, o
    print(f"[kit_props] {c.get('id')}: no rule for weapon {w!r}: plain staff")
    return "staff", {"head": "plain"}

BUILDERS = {
    "sword": w_sword, "dagger": w_dagger, "sickle": w_sickle, "axe": w_axe, "pickaxe": w_pickaxe, "hammer": w_hammer,
    "maul": w_maul, "club": w_club, "mace": w_mace, "spear": w_spear, "trident": w_trident, "glaive": w_glaive,
    "staff": w_staff, "bow": w_bow, "crossbow": w_crossbow, "whip": w_whip, "flail": w_flail, "anchor": w_anchor,
    "spade": w_spade, "peel": w_peel, "fork": w_fork, "tankard": w_tankard, "fist": w_fist, "net": w_net, "sling": w_sling,
    "blowpipe": w_blowpipe, "javelin": w_javelin, "cards": w_cards, "boomerang": w_boomerang, "chakram": w_chakram,
    "ribbon": w_ribbon, "umbrella": w_umbrella, "shears": w_shears, "smoker": w_smoker, "barrel": w_barrel, "drum": w_drum,
    "marionette": w_marionette, "orrery": w_orrery, "shuttle": w_shuttle, "kite": w_kite, "fan": w_fan, "twig": w_twig,
    "focus": w_focus, "none": lambda m, o: [],
}

def _resolve(m, o):
    o = dict(o)
    for k in ("smat", "hmat", "bmat", "omat", "mat", "fmat"):
        if isinstance(o.get(k), str): o[k] = m[o[k]]
    return o

def build_weapon(c, mats, typ=None, opts=None):
    """The design's main-hand prop as one joined object in the grip frame (None for bare hands)."""
    m = mats_for(mats)
    if typ is None: typ, opts = classify(c)
    objs = BUILDERS[typ](m, _resolve(m, opts or {}))
    if not objs: return None
    o = join(objs, "Kit_Weapon")
    # Polish pass: small props read thin at battle size; enlarge them about the grip.
    k = {"cards": 1.7, "shuttle": 1.6, "fist": 1.3}.get(typ, 1.0)
    if typ == "focus" and (opts or {}).get("kind") == "conch": k = 1.5
    if k != 1.0:
        for v in o.data.vertices: v.co *= k
    return o

def offhand(c):
    """(type, opts) for a mirrored off-hand weapon, or None: twin/dual/pair weapons and 'X and Y' pairs."""
    w = str(c.get("weapon", "")).lower()
    if _has(w, "twin", "dual", "pair") or re.search(r"\b(daggers|knives|sabres|hatchets|axes|sickles|blades)\b", w):
        return classify(c)
    return None

def build_offhand(c, mats):
    t = offhand(c)
    if t is None: return None
    return build_weapon(c, mats, *t)

# ---------------------------------------------------------------- shields

def shield_type(c):
    """(shape, opts) for the design's arm shield, or None (no shield, or one carried on the back)."""
    kit = " ".join([str(c.get("weapon", ""))] + [str(k) for k in c.get("kit", [])] + [str(x) for x in ((c.get("outfit") or {}).get("acc") or [])]).lower()
    if not _has(kit, "shield", "buckler", "pavise"): return None
    if _has(str(c.get("weapon", "")).lower(), "umbrella"): return None   # the umbrella opens as its shield
    if re.search(r"(on (his|her|their) back|back shield|carapace)", kit) and not re.search(r"(shield \(left\)|on the left arm|forearm)", kit): return None
    # the clause that describes the shield
    clause = next((p for p in re.split(r"[,;]|\band\b|'", kit) if _has(p, "shield", "buckler", "pavise")), kit)
    o = {}
    shape = "round"
    for keys, s in ((("tower",), "tower"), (("pavise",), "pavise"), (("kite",), "kite"), (("heater",), "heater"), (("buckler",), "buckler"),
                    (("square", "milestone", "oven-door", "door"), "square"), (("hexagonal",), "hex"), (("half-moon", "half-disc", "sundial", "plough", "crescent"), "halfmoon"),
                    (("dome", "cauldron", "bowl"), "dome"), (("oval", "woven", "oar"), "oval"), (("fluke", "twin-lobed"), "fluke"), (("prow", "figurehead"), "kite"),
                    (("sail", "windmill", "lattice"), "tower"), (("scallop",), "scallop")):
        if _has(clause, *keys) or any(k in clause for k in keys if "-" in k):
            shape = s; break
    if _has(clause, "glass", "lens", "stained", "rose-window", "mirror"):
        o["face"] = "glass" if not _has(clause, "mirror") else "mirror"
        o["stained"] = _has(clause, "stained", "rose-window", "rose")
        o["dark_glass"] = _has(clause, "black", "smoked", "obsidian", "dark")
    elif _has(clause, "stone", "waystone", "barrow", "milestone", "basalt", "volcanic"): o["face"] = "stone"
    elif _has(clause, "branch", "woven", "wood", "oak", "plank", "door", "oar", "sail", "ember-wood"): o["face"] = "wood"
    elif _has(clause, "iron", "cast-iron", "steel", "plough", "cauldron"): o["face"] = "iron"
    else: o["face"] = "wood" if shape in ("round", "heater", "kite", "tower", "pavise") else "metal"
    for keys, e in ((("sunburst", "sun", "sunrise", "rose"), "sun"), (("lightning", "bolt"), "bolt"), (("tree", "oak"), "tree"), (("eclipse", "moon"), "moon"),
                    (("star", "constellation", "wind-rose"), "star"), (("spiral",), "spiral"), (("lava", "volcanic", "molten", "coals", "vent", "brazier", "ember"), "lava"),
                    (("wheel", "pinwheel", "vane"), "wheel"), (("chain",), "chains"), (("notch", "tally"), "notches")):
        if _has(clause, *keys) or any(k in clause for k in keys if "-" in k): o["emblem"] = e; break
    if _has(clause, "spike", "spiked"): o["spike"] = True
    return shape, o

def _outline(shape, W, H):
    n = 28
    if shape in ("round", "buckler", "dome", "scallop"):
        return [(math.cos(a) * W, math.sin(a) * H) for a in (i / n * TAU for i in range(n))]
    if shape == "oval":
        return [(math.cos(a) * W * 0.85, math.sin(a) * H * 1.15) for a in (i / n * TAU for i in range(n))]
    if shape == "kite":
        pts = [(math.cos(a) * W, H * 0.25 + math.sin(a) * H * 0.55) for a in [math.radians(x) for x in range(0, 181, 15)]]
        return pts + [(-W * 0.85, H * 0.0), (-W * 0.45, -H * 0.6), (0.0, -H * 1.1), (W * 0.45, -H * 0.6), (W * 0.85, H * 0.0)]
    if shape == "heater":
        top = [(W, H * 0.6), (-W, H * 0.6)]
        bot = [(-W * math.cos(a * math.pi / 2) ** 0.8 * (1 if True else 1), H * 0.6 - H * 1.55 * math.sin(a * math.pi / 2)) for a in [i / 8 for i in range(9)]]
        bot2 = [(-x, y) for x, y in reversed(bot)]
        return top + bot[1:] + bot2[1:-1]
    if shape in ("tower", "pavise", "square"):
        hh = H * (1.6 if shape in ("tower", "pavise") else 1.0); r = W * 0.18
        pts = []
        for cx, cy, a0 in ((W - r, hh - r, 0), (-W + r, hh - r, 90), (-W + r, -hh + r, 180), (W - r, -hh + r, 270)):
            for k in range(4): a = math.radians(a0 + 30 * k); pts.append((cx + math.cos(a) * r, cy + math.sin(a) * r))
        return pts
    if shape == "hex":
        return [(math.cos(a) * W, math.sin(a) * H * 1.2) for a in (i / 6 * TAU + math.pi / 6 for i in range(6))]
    if shape == "halfmoon":
        return [(math.cos(a) * W, math.sin(a) * H * 1.1 - H * 0.3) for a in [math.radians(x) for x in range(0, 181, 12)]] + [(-W * 0.5, -H * 0.3), (0, -H * 0.05), (W * 0.5, -H * 0.3)]
    if shape == "fluke":
        pts = []
        for s in (1, -1):
            for a in [math.radians(x) for x in range(-90, 91, 18)]:
                pts.append((s * (W * 0.5 + math.cos(a) * W * 0.5), math.sin(a) * H * 0.7 * s))
        return pts
    return _outline("round", W, H)

def build_shield(c, mats):
    t = shield_type(c)
    if t is None: return None
    shape, o = t
    m = mats_for(mats)
    W, H = {"buckler": (0.15, 0.15), "round": (0.26, 0.28), "dome": (0.25, 0.25), "scallop": (0.27, 0.27)}.get(shape, (0.25, 0.3))
    # glass: stained glass is a dark iron backing under coloured panes; black glass is smoked glass; other
    # glass (a lens) is pale and softly lit. The plain glow face rendered every one of them as a white slab.
    glass = m["iron"] if o.get("stained") else m["glass_dark"] if o.get("dark_glass") else m["glass_pale"]
    face = {"wood": m["wood"], "stone": m["stone"], "iron": m["iron"], "glass": glass, "mirror": m["metal"], "metal": m["metal"]}[o.get("face", "metal")]
    ol = _outline(shape, W, H)
    out = []
    bulge = 0.09 if shape == "dome" else 0.05 if shape in ("round", "buckler", "scallop", "kite", "heater") else 0.025
    out.append(subdivided_plate(ol, 0.022, face, bulge, cuts=2))
    # rim
    out.append(tube([(x, y, 0.024) for x, y in ol], 0.011, m["acc"] if o.get("face") != "iron" else m["metal"], segs=5, closed=True))
    def span(poly, v, axis):
        """[lo, hi] where the line (x = v if axis == 0, else y = v) crosses the outline."""
        hits = []
        for i in range(len(poly)):
            a, b = poly[i], poly[(i + 1) % len(poly)]
            if (a[axis] - v) * (b[axis] - v) <= 0 and a[axis] != b[axis]:
                t = (v - a[axis]) / (b[axis] - a[axis]); hits.append(a[1 - axis] + (b[1 - axis] - a[1 - axis]) * t)
        return (min(hits), max(hits)) if len(hits) >= 2 else None
    Rm = max(Vector(p).length for p in ol)
    def zt(x, y): return 0.022 + bulge * (1 - min(1, Vector((x, y)).length / Rm) ** 2)
    if o.get("face") == "wood" and shape not in ("buckler", "dome"):
        # plank seams run along the forearm (Y); two iron bands cross them
        xs = [x for x, _ in ol]; xmin, xmax = min(xs), max(xs)
        for k in range(1, 5):
            x = xmin + (xmax - xmin) * k / 5; sp_ = span(ol, x, 0)
            if not sp_: continue
            lo, hi = sp_[0] + 0.012, sp_[1] - 0.012
            pts = [(x, lo + (hi - lo) * i / 6, zt(x, lo + (hi - lo) * i / 6) + 0.0015) for i in range(7)]
            out.append(tube(pts, 0.0035, m["darkwood"], segs=4, sq=0.4))
        ys = [y for _, y in ol]
        for f in (0.3, 0.7):
            y = min(ys) + (max(ys) - min(ys)) * f; sp_ = span(ol, y, 1)
            if not sp_: continue
            lo, hi = sp_[0] + 0.01, sp_[1] - 0.01
            pts = [(lo + (hi - lo) * i / 8, y, zt(lo + (hi - lo) * i / 8, y) + 0.003) for i in range(9)]
            out.append(tube(pts, 0.012, m["iron"], segs=4, sq=0.3))
            for i in (1, 4, 7): out.append(xform(lathe([(0, 0), (0.008, 0.0), (0.0, 0.007)], m["metal"], segs=6), pts[i]))
    if shape in ("round", "buckler", "dome", "scallop", "kite", "heater") and o.get("face") != "glass":
        out.append(xform(lathe([(0, 0), (0.06, 0), (0.06, 0.01), (0.04, 0.035), (0, 0.045)], m["metal"] if o.get("face") != "metal" else m["acc"], segs=16), (0, 0, 0.02 + bulge)))
    if shape == "scallop":
        for k in range(9):
            a = math.radians(-80 + 20 * k)
            out.append(tube([(0, -H * 0.7, 0.03 + bulge * 0.3), (math.sin(a) * W * 0.95, math.cos(a) * H * 0.95 - H * 0.05, 0.03)], 0.012, m["acc"], segs=5))
    if o.get("face") == "glass" and not o.get("stained"):
        # plain glass: a few iron ribs and a ring, as before, over the tinted face
        for k in range(8):
            a = k / 8 * TAU
            out.append(tube([(math.cos(a) * W * r, math.sin(a) * H * r, zt(math.cos(a) * W * r, math.sin(a) * H * r) + 0.005) for r in (0.1, 0.4, 0.7, 0.94)], 0.005, m["iron"], segs=4))
        out.append(tube([(math.cos(j / 32 * TAU) * W * 0.45, math.sin(j / 32 * TAU) * H * 0.45, zt(math.cos(j / 32 * TAU) * W * 0.45, math.sin(j / 32 * TAU) * H * 0.45) + 0.005) for j in range(33)], 0.005, m["iron"], segs=4))
    if o.get("face") == "glass" and o.get("stained"):
        # a rose window: an outer ring of 8 panes and an inner ring of 8 petals in four colours, following the
        # dome, with iron leading that follows it too, and a gold rosette at the heart
        cols = [m["pane_blue"], m["pane_red"], m["pane_gold"], m["pane_teal"]]
        def wedge(r0, r1, a0, a1, n=6):
            return [(math.cos(a0 + (a1 - a0) * i / n) * r1 * W, math.sin(a0 + (a1 - a0) * i / n) * r1 * H) for i in range(n + 1)] + \
                [(math.cos(a1 - (a1 - a0) * i / n) * r0 * W, math.sin(a1 - (a1 - a0) * i / n) * r0 * H) for i in range(n + 1)]
        gap = 0.035
        for k in range(8):
            a0, a1 = k / 8 * TAU + gap, (k + 1) / 8 * TAU - gap
            pane = plate(wedge(0.5, 0.9, a0, a1), 0.004, cols[k % 4], bulge=bulge, bevel=0.0, R=Rm)
            out.append(xform(pane, (0, 0, 0.023)))
            b0, b1 = (k + 0.5) / 8 * TAU + gap * 1.6, (k + 1.5) / 8 * TAU - gap * 1.6
            petal = plate(wedge(0.2, 0.44, b0, b1), 0.004, cols[(k + 2) % 4], bulge=bulge, bevel=0.0, R=Rm)
            out.append(xform(petal, (0, 0, 0.023)))
        def lead(pts2):
            return tube([(x, y, zt(x, y) + 0.006) for x, y in pts2], 0.005, m["iron"], segs=4)
        for k in range(8):
            a = k / 8 * TAU
            out.append(lead([(math.cos(a) * W * r, math.sin(a) * H * r) for r in (0.18, 0.35, 0.5, 0.65, 0.8, 0.94)]))
        for r in (0.47, 0.17):
            out.append(lead([(math.cos(j / 32 * TAU) * W * r, math.sin(j / 32 * TAU) * H * r) for j in range(33)]))
        out.append(xform(lathe([(0, 0), (0.035, 0), (0.03, 0.01), (0.012, 0.02), (0, 0.022)], m["gold"], segs=16), (0, 0, zt(0, 0))))
        out.append(xform(lathe([(0, 0), (0.014, 0), (0, 0.012)], m["glow"], segs=12), (0, 0, zt(0, 0) + 0.018)))
    e = o.get("emblem") if not o.get("stained") else None   # a rose window is its own emblem (its "rose" read as a sun, gold rays on white glass)
    ez = 0.03 + bulge; n_before = len(out)
    if e == "sun":
        out.append(xform(lathe([(0, 0), (0.06, 0), (0.0, 0.012)], m["glow"], segs=16), (0, 0, ez + 0.01)))
        for k in range(12):
            a = k / 12 * TAU
            ray = plate([(0.0, 0.0), (0.025, 0.07), (0.0, 0.16), (-0.025, 0.07)], 0.006, m["gold"], bevel=0.001)
            ray.data.transform(Matrix.Translation((0, 0, ez)) @ Matrix.Rotation(a, 4, "Z") @ Matrix.Translation((0, 0.05, 0)))
            out.append(ray)
    elif e == "bolt":
        p = plate([(0.02, 0.18), (-0.06, 0.0), (0.0, 0.0), (-0.03, -0.18), (0.07, 0.03), (0.01, 0.03), (0.05, 0.18)], 0.01, m["glow"], bevel=0.002)
        out.append(xform(p, (0, 0, ez + 0.02)))
    elif e == "tree":
        out.append(xform(box((0.03, 0.14, 0.008), (0, -0.07, 0), m["paper"], bevel=0), (0, 0, ez + 0.01)))
        out.append(xform(plate([(math.cos(a) * 0.12, 0.06 + math.sin(a) * 0.09) for a in (i / 14 * TAU for i in range(14))], 0.008, m["paper"], bevel=0), (0, 0, ez + 0.005)))
    elif e == "moon":
        out.append(xform(lathe([(0, 0), (0.12, 0), (0.0, 0.01)], m["iron"], segs=24), (0, 0, ez + 0.005)))
        out.append(torus(0.12, 0.01, m["glow"], loc=(0, 0, ez + 0.01), seg=24, mseg=4))
    elif e == "star":
        out.append(star(m, (0, 0, ez + 0.012), r=0.13, pts=8 if "wind-rose" in str(c.get("kit", "")).lower() else 5, mat=m["glow"], thick=0.008, rot=Matrix.Identity(3)))
    elif e == "spiral":
        pts = [(math.cos(t) * 0.012 * t, math.sin(t) * 0.012 * t, ez + 0.006) for t in [i * 0.4 for i in range(26)]]
        out.append(tube(pts, 0.007, m["glow"], segs=4))
    elif e == "lava":
        for k in range(5):
            a = k / 5 * TAU + 0.3
            out.append(tube([(0, 0, ez), (math.cos(a) * W * 0.45, math.sin(a) * H * 0.45, ez - 0.01), (math.cos(a + 0.4) * W * 0.85, math.sin(a + 0.4) * H * 0.85, 0.03)], 0.008, m["glow"], segs=4))
    elif e == "wheel":
        for k in range(8):
            a = k / 8 * TAU
            v = plate([(0, 0), (0.05, 0.02), (0.05, 0.1), (0, 0.08)], 0.008, m["metal"], bevel=0.001)
            v.data.transform(Matrix.Translation((0, 0, ez)) @ Matrix.Rotation(a, 4, "Z") @ Matrix.Translation((0, W * 0.55, 0)))
            out.append(v)
    elif e == "chains":
        out += chain(m, (-W * 0.8, H * 1.1, 0.04), (W * 0.6, -H * 0.3, 0.05), sag=0.05, links=9, r=0.018)
    elif e == "notches":
        for k in range(10): out.append(box((0.006, 0.05, 0.01), (-W * 0.7 + 0.03 * (k % 5) * 3, H * 0.9 - 0.18 * (k // 5), 0.03), m["darkwood"], bevel=0))
    # emblems are built flat at the dome's peak: lay them onto the curved face
    for ob in out[n_before:]:
        if e == "chains": break
        for v in ob.data.vertices:
            v.co.z += zt(v.co.x, v.co.y) - (0.022 + bulge) + 0.004
    if o.get("spike"):
        out.append(xform(lathe([(0.03, 0), (0.0, 0.18)], m["iron"], segs=6, smooth=False), (0, -H * 1.55, 0.02), rx(90)))
    # strap and grip on the inside (toward the forearm)
    out.append(box((0.04, 0.12, 0.02), (0, 0, -0.012), m["leather"], bevel=0.004))
    return join(out, "Kit_Shield")
