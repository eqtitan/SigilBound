# Procedural scenery prop kit for the battle arenas (docs/sigilbound/art/SCENERY.md).
# Run with Blender's Python module:  python make_props.py [out.bytes]
# Every prop is built from code (deterministic seeds), then baked: ambient occlusion and a
# top-light term are ray-cast per vertex and stored, with a painterly noise, as a "shade" value.
# Each face carries a material row (bark, leaves, rock, cloth ...). In the game a per-theme ramp
# texture (Scenery.Ramp) maps (shade, row) to a painted colour: cool dark undersides, local colour,
# warm highlights, exactly the hero painting guide (ART_BIBLE 6). One material per theme draws
# every prop, so the whole scatter batches; glowing parts (coals, crystals, lamps, caps) are a
# second submesh drawn with the theme's glow material.
#
# Output format (little endian), read by Client/SceneryKit.cs:
#   "SBP1", int32 meshCount, then per mesh:
#     byte nameLen, name, int32 vertexCount, byte submeshCount, int32 indexCount[submeshCount],
#     float3 boundsMin, float3 boundsSize,
#     vertexCount x (int16 x3 position quantized over the bounds, int8 x3 normal, byte shade, byte row),
#     uint16 indices per submesh.
import bpy, bmesh, math, random, struct, sys, os
from mathutils import Vector, Matrix, noise
from mathutils.bvhtree import BVHTree

BARK, LEAF_A, LEAF_B, LEAF_C, ROCK, MOSS, WOOD, STONE, CLOTH, METAL, CAP, STEM, REED, EARTH, TRIM, COAL = range(16)
GLOW = 16
LEAVES = (LEAF_A, LEAF_B, LEAF_C)
# Rows shaded as one soft volume (leaf canopies) or with smooth vs hard normals.
SMOOTH_ANGLE = {BARK: 75, LEAF_A: 180, LEAF_B: 180, LEAF_C: 180, ROCK: 38, MOSS: 50, WOOD: 30, STONE: 30, CLOTH: 80, METAL: 40,
                CAP: 70, STEM: 70, REED: 70, EARTH: 70, TRIM: 40, COAL: 60, GLOW: 50}


# ---------------------------------------------------------------- bmesh helpers
def lathe(bm, profile, sides, row, y0=0.0, jitter=0.0, rng=None, cap_top=False, cap_bottom=False):
    """profile: list of (radius, y). Returns the rings of verts."""
    rings = []
    for (r, y) in profile:
        ring = []
        for i in range(sides):
            a = i / sides * math.tau
            rr = r * (1 + (rng.uniform(-jitter, jitter) if rng else 0))
            ring.append(bm.verts.new((math.cos(a) * rr, y + y0, math.sin(a) * rr)))
        rings.append(ring)
    for k in range(len(rings) - 1):
        for i in range(sides):
            a, b = rings[k][i], rings[k][(i + 1) % sides]
            c, d = rings[k + 1][(i + 1) % sides], rings[k + 1][i]
            f = bm.faces.new((a, d, c, b)); f.material_index = row
    if cap_top:
        f = bm.faces.new(list(rings[-1])); f.material_index = row
    if cap_bottom:
        f = bm.faces.new(list(reversed(rings[0]))); f.material_index = row
    return rings


def add_bm(dst, src, mat=None, row=None):
    """Copies bmesh src into dst, transformed by mat; row overrides material indices."""
    m = mat or Matrix.Identity(4)
    vmap = {}
    for v in src.verts:
        vmap[v] = dst.verts.new(m @ v.co)
    for f in src.faces:
        nf = dst.faces.new([vmap[v] for v in f.verts])
        nf.material_index = f.material_index if row is None else row
    return dst


def ico(r, subdiv, row):
    bm = bmesh.new()
    bmesh.ops.create_icosphere(bm, subdivisions=subdiv + 1, radius=r)  # Blender 5: 1 = the bare icosahedron
    for f in bm.faces: f.material_index = row
    return bm


def box(sx, sy, sz, row, bevel=0.0):
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    for v in bm.verts: v.co = Vector((v.co.x * sx, v.co.y * sy + sy * 0.5, v.co.z * sz))
    if bevel > 0:
        bmesh.ops.bevel(bm, geom=list(bm.edges), offset=bevel, segments=1, affect='EDGES', profile=0.5)
    for f in bm.faces: f.material_index = row
    return bm


def two_sided(bm):
    """Duplicates every face reversed (cloth, blades)."""
    faces = list(bm.faces)
    vmap = {}
    for v in list(bm.verts): vmap[v] = bm.verts.new(v.co)
    for f in faces:
        nf = bm.faces.new([vmap[v] for v in reversed(f.verts)]); nf.material_index = f.material_index
    return bm


def skin_tree(edges_pts, radii, subsurf=1, decimate=1.0):
    """A branching trunk from a vertex skeleton through Blender's Skin modifier."""
    me = bpy.data.meshes.new("skel")
    pts = [p for p in edges_pts[0]]
    me.from_pydata(pts, edges_pts[1], [])
    ob = bpy.data.objects.new("skel", me); bpy.context.collection.objects.link(ob)
    mod = ob.modifiers.new("skin", 'SKIN'); mod.use_smooth_shade = True
    sk = me.skin_vertices[0].data
    for i, r in enumerate(radii): sk[i].radius = (r, r)
    sk[0].use_root = True
    if subsurf:
        s = ob.modifiers.new("sub", 'SUBSURF'); s.levels = subsurf
    if decimate < 1:
        d = ob.modifiers.new("dec", 'DECIMATE'); d.ratio = decimate
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    bm = bmesh.new(); bm.from_mesh(ev.to_mesh())
    ev.to_mesh_clear(); bpy.data.objects.remove(ob); bpy.data.meshes.remove(me)
    for f in bm.faces: f.material_index = BARK
    return bm


def displace(bm, amp, freq, seed, flatten_below=None):
    off = Vector((seed * 3.17, seed * 1.31, seed * 2.03))
    for v in bm.verts:
        n = v.co.normalized() if v.co.length > 1e-5 else Vector((0, 1, 0))
        v.co += n * amp * noise.noise((v.co * freq) + off)
    if flatten_below is not None:
        for v in bm.verts:
            if v.co.y < flatten_below: v.co.y = flatten_below + (v.co.y - flatten_below) * 0.25


def T(x, y, z): return Matrix.Translation((x, y, z))
def S(x, y, z): return Matrix.Diagonal((x, y, z, 1))
def Ry(d): return Matrix.Rotation(math.radians(d), 4, 'Y')
def Rx(d): return Matrix.Rotation(math.radians(d), 4, 'X')
def Rz(d): return Matrix.Rotation(math.radians(d), 4, 'Z')


# ---------------------------------------------------------------- props
def canopy_clump(r, subdiv, seed, row):
    c = ico(r, subdiv, row)
    displace(c, r * 0.22, 1.6 / r, seed)
    for v in c.verts:  # painted canopies: soft rounded tops, flatter undersides
        if v.co.y < -0.25 * r: v.co.y = -0.25 * r + (v.co.y + 0.25 * r) * 0.35
    return c


def oak(seed):
    rng = random.Random(seed)
    h = rng.uniform(1.4, 1.8)
    pts = [Vector((0, 0, 0)), Vector((rng.uniform(-.08, .08), h * 0.55, rng.uniform(-.08, .08))), Vector((rng.uniform(-.1, .1), h, rng.uniform(-.1, .1)))]
    edges = [(0, 1), (1, 2)]; radii = [0.24, 0.17, 0.13]
    for k in range(3):  # root flare
        a = k / 3 * math.tau + rng.uniform(-.4, .4)
        pts.append(Vector((math.cos(a) * 0.38, -0.06, math.sin(a) * 0.38))); edges.append((0, len(pts) - 1)); radii.append(0.07)
    tips = []
    for k in range(rng.choice((3, 4))):  # branches up into the canopy
        a = k / 4 * math.tau + rng.uniform(-.5, .5)
        tip = Vector((math.cos(a) * rng.uniform(.55, .8), h + rng.uniform(.5, .8), math.sin(a) * rng.uniform(.55, .8)))
        pts.append(tip); edges.append((2, len(pts) - 1)); radii.append(0.05); tips.append(tip)
    bm = skin_tree((pts, edges), radii, decimate=0.25)  # budget pass (build 138): ~700 tris an oak
    # Layered canopy: a broad lower tier, a middle tier over the branch tips, a small crown.
    top = h + 0.7
    layers = [(top - 0.2, 0.95, 1, 3), (top + 0.4, 0.85, 1, 2), (top + 0.95, 0.62, 1, 1)]
    for (y, r, sub, count) in layers:
        for k in range(count):
            a = k / max(count, 1) * math.tau + rng.uniform(0, math.tau)
            d = 0.0 if count == 1 else r * (0.8 if count > 2 else 0.5)
            row = LEAVES[rng.randrange(3)]
            c = canopy_clump(r * rng.uniform(0.85, 1.1), sub, seed * 10 + k + int(y * 7), row)
            add_bm(bm, c, T(math.cos(a) * d, y, math.sin(a) * d))
            c.free()
    return bm


def pine(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    H = rng.uniform(3.2, 4.0)
    lathe(bm, [(0.2, -0.05), (0.15, 0.6), (0.05, H)], 6, BARK)
    tiers = 5
    for t in range(tiers):
        k = 1 - t / tiers
        y0 = 0.7 + t * (H - 0.9) / tiers
        r = 0.4 + 1.0 * k
        sides = 11
        top = bm.verts.new((0, y0 + 1.05 * (0.6 + 0.4 * k), 0))
        outer, mid, inner = [], [], []
        for i in range(sides):
            a = i / sides * math.tau + t * 0.37
            rr = r * (1.0 if i % 2 == 0 else 0.78) * rng.uniform(0.92, 1.08)
            droop = rng.uniform(-0.08, 0.02)
            outer.append(bm.verts.new((math.cos(a) * rr, y0 + droop, math.sin(a) * rr)))
            mid.append(bm.verts.new((math.cos(a) * rr * 0.55, y0 + 0.45 * (0.6 + 0.4 * k), math.sin(a) * rr * 0.55)))
            inner.append(bm.verts.new((math.cos(a) * 0.12, y0 + 0.12, math.sin(a) * 0.12)))
        row = LEAVES[rng.randrange(2)]
        for i in range(sides):
            j = (i + 1) % sides
            for f in (bm.faces.new((top, mid[j], mid[i])), bm.faces.new((mid[i], mid[j], outer[j], outer[i])),
                      bm.faces.new((outer[i], outer[j], inner[j], inner[i]))):
                f.material_index = row
    return bm


def dead_tree(seed):
    rng = random.Random(seed)
    h = rng.uniform(2.2, 3.0)
    lean = Vector((rng.uniform(-.3, .3), 0, rng.uniform(-.3, .3)))
    pts = [Vector((0, 0, 0)), Vector((0, h * .4, 0)) + lean * .3, Vector((0, h * .75, 0)) + lean * .7, Vector((0, h, 0)) + lean]
    edges = [(0, 1), (1, 2), (2, 3)]; radii = [0.17, 0.12, 0.08, 0.04]
    for k in range(3):
        a = k / 3 * math.tau + rng.uniform(-.4, .4)
        pts.append(Vector((math.cos(a) * .35, -0.05, math.sin(a) * .35))); edges.append((0, len(pts) - 1)); radii.append(0.05)
    for k in range(4):
        base = rng.choice((1, 2, 2, 3))
        a = rng.uniform(0, math.tau); L = rng.uniform(0.5, 0.9)
        p1 = pts[base] + Vector((math.cos(a) * L * .6, L * .5, math.sin(a) * L * .6))
        pts.append(p1); edges.append((base, len(pts) - 1)); radii.append(0.035)
        p2 = p1 + Vector((math.cos(a + .6) * L * .4, L * .45, math.sin(a + .6) * L * .4))
        pts.append(p2); edges.append((len(pts) - 2, len(pts) - 1)); radii.append(0.015)
    return skin_tree((pts, edges), radii, decimate=0.2)


def chiselled(r, seed, planes=11, soften=0.18, subdiv=2, cliff=False):
    """A rock: the intersection of random planes (big painted facets) blended with a sphere.
    cliff: near-vertical planes (mesa walls)."""
    rng = random.Random(seed)
    ns = []
    for _ in range(planes):
        n = Vector((rng.gauss(0, 1), rng.gauss(0, 1) * (0.12 if cliff else 0.8), rng.gauss(0, 1))).normalized()
        ns.append((n, rng.uniform(0.72, 0.95)))
    bm = ico(1.0, subdiv, ROCK)
    for v in bm.verts:
        d = v.co.normalized()
        rr = 1.0
        for (n, dist) in ns:
            c = d.dot(n)
            if c > 1e-3: rr = min(rr, dist / c)
        rr = rr * (1 - soften) + soften
        v.co = d * rr * r
    displace(bm, r * 0.05, 3.0 / r, seed)
    return bm


def rock(seed, sx=1.0, sy=0.7, sz=0.9):
    bm = chiselled(0.6, seed)
    for v in bm.verts:
        v.co = Vector((v.co.x * sx, v.co.y * sy, v.co.z * sz))
        if v.co.y < -0.15: v.co.y = -0.15 + (v.co.y + 0.15) * 0.2  # half sunk into the ground
        v.co.y += 0.15
    moss_tops(bm, seed)
    return bm


def moss_tops(bm, seed, row=MOSS, cut=0.55):
    bm.normal_update()
    for f in bm.faces:
        c = f.calc_center_median()
        if f.normal.y > cut and noise.noise(c * 2.2 + Vector((seed, 0, 0))) > -0.1:
            f.material_index = row


def standing_stone(seed):
    bm = chiselled(0.5, seed, planes=9, soften=0.1)
    for v in bm.verts:
        v.co = Vector((v.co.x * 0.75, (v.co.y + 0.5) * 2.0, v.co.z * 0.5))
        if v.co.y < 0.05: v.co.y = 0.05 + (v.co.y - 0.05) * 0.2
    moss_tops(bm, seed, cut=0.4)
    return bm


def mound(seed):
    bm = ico(1.0, 2, EARTH)
    displace(bm, 0.08, 2.0, seed)
    for v in bm.verts:
        v.co = Vector((v.co.x * 2.2, max(v.co.y, -0.1) * 0.9, v.co.z * 1.7))
    bm.normal_update()
    for f in bm.faces:
        if f.normal.y > 0.3 and noise.noise(f.calc_center_median() * 0.9) > -0.25: f.material_index = MOSS
    # a ring of kerb stones at the mouth
    for k in range(5):
        st = chiselled(0.22, seed + k, planes=8, subdiv=1)
        a = math.radians(-30 + k * 15)
        add_bm(bm, st, T(math.sin(a) * 2.1, 0.12, -math.cos(a) * 1.55), row=STONE)
        st.free()
    return bm


def column(seed, broken):
    rng = random.Random(seed)
    bm = box(0.9, 0.3, 0.9, STONE, bevel=0.04)
    H = rng.uniform(1.4, 2.0) if broken else 2.8
    prof = [(0.34, 0.3), (0.3, 0.45)] + [(0.3 - 0.03 * k / 4, 0.45 + (H - 0.45) * k / 4) for k in range(1, 5)]
    rings = lathe(bm, prof, 12, STONE)
    for k, ring in enumerate(rings[1:], 1):
        for i, v in enumerate(ring):
            if i % 2: v.co.x *= 0.9; v.co.z *= 0.9
    if broken:  # a jagged broken top
        for v in rings[-1]: v.co.y -= rng.uniform(0, 0.35)
        f = bm.faces.new(list(rings[-1])); f.material_index = STONE
        chunk = chiselled(0.28, seed + 5, planes=8, subdiv=1)
        add_bm(bm, chunk, T(0.7, 0.18, 0.35) @ S(1.2, 0.8, 1.0), row=STONE); chunk.free()
    else:
        f = bm.faces.new(list(rings[-1])); f.material_index = STONE
        cap = box(0.85, 0.25, 0.85, STONE, bevel=0.04)
        add_bm(bm, cap, T(0, H, 0)); cap.free()
    moss_tops(bm, seed, cut=0.6)
    return bm


def ruin_wall(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    W, rows = 5, 4
    for r in range(rows):
        n_here = W - (0 if r < 2 else rng.randint(1, 3))
        start = 0 if r < 2 else rng.randint(0, W - n_here)
        for i in range(start, start + n_here):
            if r == rows - 1 and rng.random() < 0.4: continue
            w = rng.uniform(0.62, 0.72)
            b = box(w, 0.42, 0.5, STONE)  # plain blocks: the baked AO draws the joints
            x = (i - W / 2 + 0.5) * 0.72 + (0.36 if r % 2 else 0)
            add_bm(bm, b, T(x, r * 0.44, rng.uniform(-.03, .03)) @ Ry(rng.uniform(-4, 4))); b.free()
    for k in range(2):  # fallen blocks
        b = box(0.6, 0.4, 0.45, STONE)
        add_bm(bm, b, T(rng.uniform(-1.5, 1.5), 0, rng.uniform(-0.9, -0.6)) @ Ry(rng.uniform(0, 90)) @ Rz(rng.uniform(-10, 10))); b.free()
    moss_tops(bm, seed, cut=0.7)
    return bm


def arch(seed):
    bm = bmesh.new()
    for x in (-1.0, 1.0):  # two pillars and a lintel arc of voussoirs
        p = box(0.5, 2.0, 0.5, STONE, bevel=0.035); add_bm(bm, p, T(x, 0, 0)); p.free()
    n = 7
    for k in range(n):
        a = math.pi * (k + 0.5) / n
        if k == 5: continue  # one stone fallen out
        b = box(0.42, 0.34, 0.5, STONE, bevel=0.03)
        add_bm(bm, b, T(-math.cos(a) * 1.0, 2.0 + math.sin(a) * 0.75 - 0.17, 0) @ Rz(90 - math.degrees(a))); b.free()
    moss_tops(bm, seed, cut=0.6)
    return bm


def banner_pole():
    bm = bmesh.new()
    lathe(bm, [(0.07, -0.05), (0.06, 3.0)], 6, WOOD, cap_top=True)
    bar = box(1.0, 0.07, 0.07, WOOD); add_bm(bm, bar, T(0, 2.82, 0)); bar.free()
    fin = ico(0.1, 1, TRIM); add_bm(bm, fin, T(0, 3.08, 0)); fin.free()
    for x in (-0.5, 0.5):
        k = ico(0.05, 1, TRIM); add_bm(bm, k, T(x, 2.855, 0)); k.free()
    return bm


def banner_cloth():
    """Origin at the top edge (the crossbar), hanging down -Y, swallow-tailed; sways about X."""
    bm = bmesh.new()
    cols, rows, W, H = 4, 7, 0.82, 1.7
    grid = []
    for r in range(rows + 1):
        row = []
        for c in range(cols + 1):
            u = c / cols; v = r / rows
            y = -v * H
            if r == rows: y += (0.35 if c == cols // 2 else 0.0)  # the swallowtail notch
            z = 0.05 * math.sin(u * math.pi * 1.5 + v * 2.0) * v
            row.append(bm.verts.new(((u - 0.5) * W, y, z)))
        grid.append(row)
    for r in range(rows):
        for c in range(cols):
            f = bm.faces.new((grid[r][c], grid[r][c + 1], grid[r + 1][c + 1], grid[r + 1][c]))
            f.material_index = TRIM if r == 0 or r == rows - 1 else CLOTH
    return two_sided(bm)


def brazier():
    """A squat iron fire bowl on a pedestal (art review M3: the old flared cone under a glowing dome of coals
    read as a lamp shade). The coals are a dark heap lit like the rest; only a few small embers glow (submesh 1,
    the arena's dim ember material), and ArenaReactor adds a small flame above them."""
    bm = bmesh.new()
    # foot: a broad stepped base, a short column and a collar
    lathe(bm, [(0.30, 0.0), (0.30, 0.06), (0.20, 0.11), (0.08, 0.18), (0.08, 0.52), (0.13, 0.57), (0.09, 0.64)], 10, METAL)
    # bowl: rounded and deep, its lip rolled out (a cauldron, not a cone)
    lathe(bm, [(0.06, 0.62), (0.27, 0.67), (0.37, 0.78), (0.38, 0.89), (0.41, 0.95), (0.33, 0.96), (0.30, 0.88)], 10, METAL)
    for k in range(3):  # three struts from the column to the bowl
        leg = box(0.04, 0.30, 0.04, METAL)
        add_bm(bm, leg, Ry(k * 120 + 60) @ T(0.06, 0.44, 0) @ Rz(-38)); leg.free()
    coals = ico(0.3, 1, COAL)
    for v in coals.verts: v.co = Vector((v.co.x, max(v.co.y, 0) * 0.32, v.co.z))
    displace(coals, 0.035, 6, 3)
    add_bm(bm, coals, T(0, 0.86, 0)); coals.free()
    rng = random.Random(11)
    for k in range(5):  # a few embers glowing in the heap
        a = k / 5 * math.tau + rng.uniform(-0.3, 0.3); r = rng.uniform(0.04, 0.18)
        e = ico(rng.uniform(0.035, 0.05), 0, GLOW)
        add_bm(bm, e, T(math.cos(a) * r, 0.865 + 0.096 * (1 - (r / 0.3) ** 2), math.sin(a) * r)); e.free()  # on the heap's surface
    return bm


def crystal(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    for k in range(rng.randint(5, 7)):
        h = rng.uniform(0.5, 1.4) * (1.3 if k == 0 else 1.0); r = rng.uniform(0.09, 0.17) * (1.4 if k == 0 else 1)
        c = bmesh.new()
        rings = lathe(c, [(r, -0.1), (r, h)], 6, GLOW)
        tip = c.verts.new((0, h + r * 2.2, 0))
        for i in range(6): c.faces.new((rings[-1][i], rings[-1][(i + 1) % 6], tip)).material_index = GLOW
        tilt = 0 if k == 0 else rng.uniform(15, 40)
        add_bm(bm, c, T(rng.uniform(-.25, .25) if k else 0, 0, rng.uniform(-.25, .25) if k else 0) @ Ry(rng.uniform(0, 360)) @ Rz(tilt)); c.free()
    base = chiselled(0.3, seed, planes=8, subdiv=1)
    for v in base.verts: v.co.y = v.co.y * 0.4
    add_bm(bm, base, row=ROCK); base.free()
    return bm


def mushrooms(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    for k in range(3):
        s = (1.0, 0.65, 0.45)[k]
        m = bmesh.new()
        lathe(m, [(0.09 * s, 0), (0.07 * s, 0.35 * s), (0.06 * s, 0.5 * s)], 7, STEM)
        cap = [(0.08 * s, 0.42 * s), (0.34 * s, 0.46 * s), (0.36 * s, 0.52 * s), (0.28 * s, 0.66 * s), (0.12 * s, 0.74 * s), (0.001, 0.76 * s)]
        lathe(m, cap[:2], 9, STEM)          # gills underneath
        lathe(m, cap[1:], 9, GLOW)          # the cap (glows in glow marsh / fens)
        add_bm(bm, m, T(*(((0, 0, 0), (0.32, 0, 0.1), (-0.2, 0, 0.26))[k])) @ Rz(rng.uniform(-10, 10))); m.free()
    return bm


def blade(bm, base, h, w, bend, yaw, row, segs=3):
    prev = None
    for s in range(segs + 1):
        t = s / segs
        ww = w * (1 - t) + 0.004
        off = Vector((bend * t * t, h * t, 0))
        m = Ry(yaw)
        l = bm.verts.new(base + (m @ (off + Vector((0, 0, -ww)))))
        r = bm.verts.new(base + (m @ (off + Vector((0, 0, ww)))))
        if prev: bm.faces.new((prev[0], prev[1], r, l)).material_index = row
        prev = (l, r)


def reeds(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    for k in range(14):
        b = Vector((rng.uniform(-.35, .35), 0, rng.uniform(-.35, .35)))
        blade(bm, b, rng.uniform(0.8, 1.5), 0.035, rng.uniform(0.1, 0.4), rng.uniform(0, 360), REED)
    two_sided(bm)
    for k in range(4):  # cattail heads
        b = Vector((rng.uniform(-.25, .25), 0, rng.uniform(-.25, .25)))
        h = rng.uniform(1.1, 1.6)
        st = bmesh.new(); lathe(st, [(0.012, 0), (0.012, h)], 4, REED); add_bm(bm, st, T(b.x, 0, b.z)); st.free()
        hd = bmesh.new(); lathe(hd, [(0.03, 0), (0.045, 0.06), (0.045, 0.22), (0.02, 0.26)], 6, BARK, cap_top=True); add_bm(bm, hd, T(b.x, h - 0.3, b.z)); hd.free()
    return bm


def tuft(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    for k in range(9):
        b = Vector((rng.uniform(-.12, .12), 0, rng.uniform(-.12, .12)))
        blade(bm, b, rng.uniform(0.3, 0.6), 0.045, rng.uniform(0.1, 0.3), rng.uniform(0, 360), MOSS, segs=2)
    return two_sided(bm)


def bush(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    for k in range(4):
        r = rng.uniform(0.35, 0.55)
        c = canopy_clump(r, 1, seed + k, LEAVES[rng.randrange(3)])
        add_bm(bm, c, T(rng.uniform(-.4, .4), r * 0.6, rng.uniform(-.4, .4))); c.free()
    return bm


def lantern_post():
    bm = bmesh.new()
    lathe(bm, [(0.09, -0.05), (0.07, 2.3)], 6, WOOD, cap_top=True)
    arm = box(0.55, 0.06, 0.06, WOOD); add_bm(bm, arm, T(0.25, 2.12, 0)); arm.free()
    cage = bmesh.new(); lathe(cage, [(0.02, 0.0), (0.13, 0.04), (0.13, 0.06), (0.1, 0.34), (0.16, 0.38), (0.02, 0.5)], 6, METAL, cap_bottom=True)
    add_bm(bm, cage, T(0.48, 1.62, 0)); cage.free()
    core = ico(0.09, 1, GLOW); add_bm(bm, core, T(0.48, 1.84, 0) @ S(1, 1.4, 1)); core.free()
    return bm


def post(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    rings = lathe(bm, [(0.13, -0.05), (0.12, 0.8), (0.11, 1.4)], 5, WOOD)
    for v in rings[-1]: v.co.y -= rng.uniform(0, 0.15)
    bm.faces.new(list(rings[-1])).material_index = WOOD
    rope = bmesh.new(); lathe(rope, [(0.135, 0.9), (0.14, 0.96), (0.135, 1.02)], 8, CLOTH); add_bm(bm, rope); rope.free()
    return bm


def crate():
    bm = box(0.62, 0.62, 0.62, WOOD, bevel=0.03)
    for (x, z, r) in ((0, 0.315, 0), (0, -0.315, 0), (0.315, 0, 90), (-0.315, 0, 90)):
        slat = box(0.08, 0.66, 0.03, METAL); add_bm(bm, slat, T(x, -0.02, z) @ Ry(r)); slat.free()
    return bm


def mesa(seed):
    """Badland strata: stacked slabs, each one a chiselled disc in its own row."""
    rng = random.Random(seed)
    bm = bmesh.new(); y = 0.0
    rows = (ROCK, EARTH, STONE, ROCK)
    for i in range(4):
        h = rng.uniform(0.55, 0.85); w = 1.55 - i * 0.2
        s = chiselled(1.0, seed + i, planes=9, soften=0.04, subdiv=1, cliff=True)
        for v in s.verts:  # a slab with cliff walls: squash the sphere's caps flat
            yy = max(min(v.co.y * 1.8, 0.5), -0.5)
            v.co = Vector((v.co.x * w, (yy + 0.5) * h, v.co.z * w * 0.8))
        add_bm(bm, s, T(rng.uniform(-.1, .1), y, rng.uniform(-.1, .1)), row=rows[i]); s.free()
        y += h * 0.92
    return bm


def debris(seed):
    return chiselled(0.18, seed, planes=8, subdiv=1)


PROPS = [
    ("oak0", lambda: oak(11)), ("oak1", lambda: oak(23)), ("oak2", lambda: oak(37)),
    ("pine0", lambda: pine(5)), ("pine1", lambda: pine(9)),
    ("dead0", lambda: dead_tree(3)), ("dead1", lambda: dead_tree(8)),
    ("rock0", lambda: rock(1)), ("rock1", lambda: rock(2, 1.2, 0.6, 0.8)), ("rock2", lambda: rock(4, 0.9, 1.0, 0.9)), ("rock3", lambda: rock(6, 1.4, 0.5, 1.0)),
    ("stone0", lambda: standing_stone(3)), ("stone1", lambda: standing_stone(7)), ("mound", lambda: mound(2)),
    ("column0", lambda: column(1, False)), ("column1", lambda: column(2, True)), ("column2", lambda: column(5, True)),
    ("wall0", lambda: ruin_wall(1)), ("wall1", lambda: ruin_wall(4)), ("arch", lambda: arch(2)),
    ("banner_pole", banner_pole), ("banner_cloth", banner_cloth), ("brazier", brazier),
    ("crystal0", lambda: crystal(1)), ("crystal1", lambda: crystal(4)),
    ("mushrooms0", lambda: mushrooms(1)), ("mushrooms1", lambda: mushrooms(5)),
    ("reeds0", lambda: reeds(1)), ("reeds1", lambda: reeds(2)),
    ("tuft0", lambda: tuft(1)), ("tuft1", lambda: tuft(2)), ("tuft2", lambda: tuft(3)),
    ("bush0", lambda: bush(1)), ("bush1", lambda: bush(4)),
    ("lantern", lantern_post), ("post0", lambda: post(1)), ("post1", lambda: post(2)), ("crate", crate),
    ("mesa0", lambda: mesa(1)), ("mesa1", lambda: mesa(3)),
    ("debris0", lambda: debris(1)), ("debris1", lambda: debris(2)),
]


# ---------------------------------------------------------------- bake + export
def bake(bm, seed, grassy=False):
    """Triangulates, then returns verts [(pos, normal, shade, row, glow)] and tris per submesh."""
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.normal_update(); bm.verts.ensure_lookup_table(); bm.faces.ensure_lookup_table()
    tree = BVHTree.FromBMesh(bm)
    size = max((max(v.co[i] for v in bm.verts) - min(v.co[i] for v in bm.verts)) for i in range(3))
    reach = min(1.2, 0.35 + size * 0.25)
    leaf_pts = [v.co for f in bm.faces if f.material_index in LEAVES for v in f.verts]
    centre = sum(leaf_pts, Vector()) / len(leaf_pts) if leaf_pts else None
    rng = random.Random(seed)
    dirs = []
    for i in range(28):  # cosine-weighted hemisphere (local, around +Z)
        u, v = (i + 0.5) / 28, rng.random()
        r = math.sqrt(u); a = v * math.tau
        dirs.append(Vector((r * math.cos(a), r * math.sin(a), math.sqrt(1 - u))))
    vnorm = {}
    for v in bm.verts:
        n = Vector()
        for f in v.link_faces: n += f.normal * f.calc_area()
        vnorm[v.index] = n.normalized() if n.length > 1e-8 else Vector((0, 1, 0))
    ao = {}
    for v in bm.verts:
        n = vnorm[v.index]
        if centre is not None and any(f.material_index in LEAVES for f in v.link_faces):
            n = (n * 0.35 + (v.co - centre).normalized() * 0.65).normalized()
        q = n.to_track_quat('Z', 'Y')
        occ = 0
        o = v.co + n * 0.01
        for d in dirs:
            w = q @ d
            if w.y < 0 and o.y > -0.01 and (o.y / -w.y) < reach * 0.7: occ += 1; continue  # the ground
            hit = tree.ray_cast(o, w, reach)
            if hit[0] is not None: occ += 1
        ao[v.index] = 1 - occ / len(dirs)
    out_v, index_of, subs = [], {}, ([], [])
    for f in bm.faces:
        row = f.material_index; glow = row >= GLOW
        ang = math.cos(math.radians(SMOOTH_ANGLE.get(row, 40)))
        for v in f.verts:
            if row in LEAVES and centre is not None:
                n = (vnorm[v.index] * 0.35 + (v.co - centre).normalized() * 0.65).normalized()
            else:
                n = Vector()
                for g in v.link_faces:
                    if g.normal.dot(f.normal) >= ang and (g.material_index >= GLOW) == glow: n += g.normal * g.calc_area()
                n = n.normalized() if n.length > 1e-8 else f.normal.copy()
            if grassy:  # blades light like the ground they grow from
                n = (n * 0.4 + Vector((0, 0.6, 0))).normalized()
            a = ao[v.index]
            if grassy: a = 0.45 + 0.55 * a
            up = n.y * 0.5 + 0.5
            nz = noise.noise(v.co * 2.7 + Vector((seed, 0, seed))) * 0.5 + noise.noise(v.co * 7.1) * 0.25
            if row in LEAVES:  # dappled foliage: light and dark leaf masses inside each clump
                nz = nz * 0.6 + noise.noise(v.co * 5.3 + Vector((0, seed, 0))) * 0.9
            shade = (0.22 + 0.78 * a ** 1.15) * (0.6 + 0.4 * up) * (0.92 + 0.22 * nz)
            shade = max(0.0, min(1.0, shade))
            key = (v.index, round(n.x, 2), round(n.y, 2), round(n.z, 2), row)
            if key not in index_of:
                index_of[key] = len(out_v)
                out_v.append((v.co.copy(), n, shade, min(row, 15)))
            subs[1 if glow else 0].append(index_of[key])
    return out_v, subs


def export(path):
    data = bytearray(b"SBP1") + struct.pack("<i", len(PROPS))
    stats = []
    meshes = {}
    for name, make in PROPS:
        bm = make()
        verts, subs = bake(bm, sum(map(ord, name)) % 997, grassy=name.startswith(("tuft", "reeds")))
        bm.free()
        assert len(verts) < 65535, name
        subs = [s for s in subs if s] if subs[1] else [subs[0]]
        lo = Vector((min(p[0][i] for p in verts) for i in range(3)))
        hi = Vector((max(p[0][i] for p in verts) for i in range(3)))
        sz = Vector((max(hi[i] - lo[i], 1e-4) for i in range(3)))
        nb = name.encode()
        data += struct.pack("<B", len(nb)) + nb + struct.pack("<iB", len(verts), len(subs))
        for s in subs: data += struct.pack("<i", len(s))
        data += struct.pack("<6f", lo.x, lo.y, lo.z, sz.x, sz.y, sz.z)
        for (p, n, sh, row) in verts:
            q = [int(round((p[i] - lo[i]) / sz[i] * 65535)) - 32768 for i in range(3)]
            data += struct.pack("<3h3bBB", *q, *[max(-127, min(127, int(round(n[i] * 127)))) for i in range(3)], int(round(sh * 255)), row)
        for s in subs: data += struct.pack(f"<{len(s)}H", *s)
        tris = sum(len(s) for s in subs) // 3
        stats.append((name, len(verts), tris))
        meshes[name] = (verts, subs)
    with open(path, "wb") as f: f.write(data)
    return stats, meshes, len(data)


if __name__ == "__main__":
    out = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "Assets", "Resources", "Scenery", "props.bytes")
    os.makedirs(os.path.dirname(os.path.abspath(out)), exist_ok=True)
    stats, _, n = export(out)
    for s in stats: print(f"{s[0]:14s} verts {s[1]:5d} tris {s[2]:5d}")
    print(f"wrote {out}: {n / 1024:.1f} KB, {sum(s[2] for s in stats)} tris total")
