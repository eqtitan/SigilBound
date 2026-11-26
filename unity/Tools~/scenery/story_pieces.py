# Story set-pieces for the campaign routes (Core/StoryScenes.cs places them; docs/sigilbound/art/STORY_SCENES.md).
# Built and baked like the prop kit (make_props.py rows, GLOW faces in submesh 1); exported per act by
# make_landmarks.py into Resources/Scenery/story_act1.bytes and story_act2.bytes.
import math, random
import make_props as mp  # imports bpy first (bmesh needs it)
import bmesh
from mathutils import Vector, Matrix
from make_props import (lathe, add_bm, ico, box, two_sided, canopy_clump, skin_tree, chiselled, displace, moss_tops,
                        T, S, Ry, Rx, Rz, BARK, LEAVES, ROCK, MOSS, WOOD, STONE, CLOTH, METAL, STEM, REED, EARTH, TRIM, COAL, GLOW)


def put(dst, src, m=None, row=None):
    add_bm(dst, src, m, row); src.free(); return dst


def roof(w, d, h, row, overhang=0.15):
    """A gabled roof, ridge along X, sitting on y = 0."""
    bm = bmesh.new()
    x, z = w / 2 + overhang, d / 2 + overhang
    a, b, c, e = (bm.verts.new(p) for p in ((-x, 0, -z), (x, 0, -z), (x, 0, z), (-x, 0, z)))
    r0, r1 = bm.verts.new((-x, h, 0)), bm.verts.new((x, h, 0))
    for f in ((a, r0, r1, b), (c, r1, r0, e), (a, e, r0), (b, r1, c)):
        bm.faces.new(f).material_index = row
    return bm


def cone_roof(r, h, sides, row):
    bm = bmesh.new(); lathe(bm, [(r, 0), (0.001, h)], sides, row); return bm


def wheel(r, width, spokes, row, paddles=False):
    """A wheel in the XY plane (axle along Z), centred on the origin."""
    bm = bmesh.new()
    rim = bmesh.new(); lathe(rim, [(r, -width / 2), (r, width / 2)], 14, row); put(bm, rim, Rx(90))
    inner = bmesh.new(); lathe(inner, [(r * 0.88, width / 2), (r * 0.88, -width / 2)], 14, row); put(bm, inner, Rx(90))
    for k in range(spokes):
        a = k / spokes * 360
        put(bm, box(0.06, r * 0.9, 0.06, row), Rz(a))
        if paddles: put(bm, box(0.08, 0.45, width * 1.4, WOOD), Rz(a) @ T(0, r - 0.05, 0))
    hub = bmesh.new(); lathe(hub, [(0.12, -width * 0.7), (0.12, width * 0.7)], 8, METAL, cap_top=True, cap_bottom=True)
    put(bm, hub, Rx(90))
    return bm


def flames(n, r, h, seed):
    rng = random.Random(seed); bm = bmesh.new()
    for k in range(n):
        a = rng.uniform(0, math.tau); d = rng.uniform(0, r)
        f = bmesh.new(); lathe(f, [(0.18, 0), (0.22, h * 0.25), (0.001, h * rng.uniform(0.6, 1.0))], 5, GLOW)
        put(bm, f, T(math.cos(a) * d, 0, math.sin(a) * d) @ Rz(rng.uniform(-12, 12)))
    return bm


def shard(h, row=GLOW):
    bm = bmesh.new()
    rings = lathe(bm, [(0.001, 0), (h * 0.18, h * 0.3), (h * 0.14, h * 0.75)], 6, row)
    tip = bm.verts.new((0, h, 0))
    for i in range(6): bm.faces.new((rings[-1][i], rings[-1][(i + 1) % 6], tip)).material_index = row
    return bm


def segment(a, b, w, row):
    """A square beam from a to b."""
    seg = box(w, 1.0, w, row)
    d = Vector(b) - Vector(a)
    for v in seg.verts: v.co.y *= d.length
    rot = Vector((0, 1, 0)).rotation_difference(d.normalized()).to_matrix().to_4x4()
    m = Matrix.Translation(a) @ rot
    out = bmesh.new(); add_bm(out, seg, m); seg.free(); return out


# ------------------------------------------------------------------ act 1 pieces
def wagon():
    """Modelled pass: a broken farm wagon slewed onto a snapped wheel: plank bed with slatted sides,
    torn canvas over hoops, one wheel off and lying flat, a snapped shaft, spilled barrels and sacks."""
    bm = bmesh.new(); W = bmesh.new()
    put(W, box(2.6, 0.12, 1.3, WOOD))
    for k in range(5): put(W, box(2.6, 0.02, 0.02, BARK), T(0, 0.12, -0.52 + k * 0.26))
    for sz in (-0.62, 0.62):
        for j in range(3): put(W, box(2.6, 0.1, 0.05, WOOD), T(0, 0.2 + j * 0.17, sz))
        for x in (-1.2, -0.4, 0.4, 1.2): put(W, box(0.07, 0.6, 0.07, WOOD), T(x, 0.1, sz))
    put(W, box(0.06, 0.55, 1.3, WOOD), T(1.27, 0.12, 0))
    arcs = []
    for k in range(4):
        x = -1.0 + k * 0.62
        pts = [(x, 0.7 + 0.75 * math.sin(math.pi * j / 6), -0.62 * math.cos(math.pi * j / 6)) for j in range(7)]
        arcs.append(pts)
        for a, b in zip(pts, pts[1:]): put(W, segment(a, b, 0.05, BARK))
    cv = bmesh.new()
    g = [[cv.verts.new((p[0], p[1] - (0.06 if c % 2 else 0), p[2])) for p in arcs[c]] for c in range(3)]
    for c in range(2):
        for j in range(6):
            if c == 1 and j in (4, 5): continue  # the tear
            cv.faces.new((g[c][j], g[c + 1][j], g[c + 1][j + 1], g[c][j + 1])).material_index = CLOTH
    put(W, two_sided(cv))
    put(W, box(0.1, 0.1, 1.5, METAL), T(-0.8, -0.35, 0))
    put(W, wheel(0.52, 0.1, 10, WOOD), T(-0.8, -0.35, -0.78))
    put(W, wheel(0.52, 0.1, 10, WOOD), T(-0.8, -0.35, 0.78))
    put(bm, W, T(0, 0.85, 0) @ Rx(10) @ Rz(-9))
    put(bm, wheel(0.52, 0.1, 10, WOOD), T(1.9, 0.06, 1.2) @ Rx(90) @ Ry(20))
    put(bm, box(0.08, 0.08, 1.6, WOOD), T(-2.1, 0.3, 0.25) @ Ry(80) @ Rx(-12))
    put(bm, box(0.08, 0.08, 0.9, WOOD), T(-2.9, 0.06, 0.9) @ Ry(35))
    for k in range(2):
        br = bmesh.new(); lathe(br, [(0.24, 0), (0.29, 0.3), (0.24, 0.6)], 9, WOOD, cap_top=True, cap_bottom=True)
        for y in (0.08, 0.52):
            bd = bmesh.new(); lathe(bd, [(0.26, y), (0.26, y + 0.04)], 9, METAL); put(br, bd)
        put(bm, br, T(1.2 + k * 0.5, 0.29 if k else 0, -1.1) @ (Rx(90) if k else Matrix.Identity(4)))
    for k in range(3):
        sk = ico(0.27, 1, CLOTH)
        for v in sk.verts: v.co.y *= 0.7
        put(bm, sk, T(0.2 + k * 0.42, 0.14, -1.2 - (k % 2) * 0.25))
    return bm


def waystone():
    bm = bmesh.new()
    ws = chiselled(0.35, 4, planes=8, soften=0.05, subdiv=1)
    for v in ws.verts: v.co = Vector((v.co.x * 0.8, (v.co.y + 0.35) * 2.4, v.co.z * 0.6))
    put(bm, ws, row=STONE)
    put(bm, box(0.08, 2.4, 0.08, WOOD), T(0.45, 0, 0.3))
    put(bm, box(0.95, 0.32, 0.05, WOOD), T(0.85, 1.95, 0.3) @ Rz(-4))
    put(bm, box(0.75, 0.28, 0.05, WOOD), T(0.05, 1.5, 0.3) @ Rz(6))
    return bm


def hedge():
    rng = random.Random(31); bm = bmesh.new()
    for k in range(4):
        x = -1.7 + k * 1.15
        tg = skin_tree(([Vector((0, 0, 0)), Vector((0.3, 0.6, 0.1)), Vector((-0.3, 0.9, -0.2)), Vector((0.5, 1.1, 0.3))], [(0, 1), (1, 2), (1, 3)]),
                       [0.07, 0.05, 0.02, 0.02], subsurf=0)
        put(bm, tg, T(x, 0, rng.uniform(-.2, .2)) @ Ry(rng.uniform(0, 360)))
        put(bm, canopy_clump(0.6, 1, k + 30, LEAVES[k % 3]), T(x + 0.2, 0.6, 0) @ S(1.3, 0.8, 0.9))
    return bm


def tollhut():
    bm = bmesh.new()
    put(bm, box(1.6, 1.8, 1.4, WOOD, bevel=0.02))
    put(bm, box(0.5, 0.5, 0.04, COAL), T(0.3, 0.9, -0.71))
    put(bm, roof(1.6, 1.4, 0.8, EARTH), T(0, 1.8, 0))
    put(bm, box(0.18, 1.1, 0.18, WOOD), T(1.2, 0, -0.3))
    put(bm, box(1.6, 0.08, 0.08, CLOTH), T(2.0, 1.0, -0.3) @ Rz(-28))
    put(bm, box(1.1, 0.08, 0.08, CLOTH), T(3.2, 0.05, -0.1) @ Ry(25))
    return bm


def tent():
    bm = bmesh.new()
    put(bm, roof(1.8, 1.6, 1.3, CLOTH, overhang=0.0), Ry(90))
    put(bm, box(0.06, 1.4, 0.06, WOOD), T(0, 0, -0.95))
    return bm


def camp():
    bm = bmesh.new()
    put(bm, tent(), T(-1.2, 0, 0) @ Ry(15))
    put(bm, tent(), T(1.4, 0, 0.6) @ Ry(-25) @ S(0.85, 0.85, 0.85))
    for k in range(8):
        a = k / 8 * math.tau
        put(bm, chiselled(0.13, 50 + k, planes=6, subdiv=1), T(0.2 + math.cos(a) * 0.5, 0.06, -1.3 + math.sin(a) * 0.5), row=STONE)
    put(bm, ico(0.3, 1, COAL), T(0.2, 0.03, -1.3) @ S(1, 0.25, 1))
    for x in (-0.4, 0.8): put(bm, box(0.05, 0.9, 0.05, WOOD), T(x, 0, -1.3))
    put(bm, box(1.3, 0.05, 0.05, WOOD), T(0.2, 0.88, -1.3))
    return bm


def fence():
    bm = bmesh.new()
    for k in range(7): put(bm, box(0.08, 0.75, 0.06, WOOD), T(-1.5 + k * 0.5, 0, 0) @ Rz((k % 3 - 1) * 3))
    for y in (0.25, 0.55): put(bm, box(3.2, 0.06, 0.04, WOOD), T(0, y, 0.04))
    return bm


def rowboat():
    bm = bmesh.new()
    lathe(bm, [(0.001, -1.4), (0.5, -0.9), (0.62, 0.0), (0.5, 0.9), (0.001, 1.3)], 10, WOOD)
    hull = bmesh.new(); add_bm(hull, bm, Rx(90) @ S(1, 1, 0.5)); bm.free()
    for v in hull.verts:
        if v.co.y > 0: v.co.y = 0.02
    put(hull, box(1.1, 0.05, 0.25, WOOD), T(0, 0.0, 0.2))
    put(hull, box(0.05, 0.05, 1.8, WOOD), T(0.4, 0.05, -0.5) @ Ry(20))
    return hull


def sluice_gate():
    bm = bmesh.new()
    for x in (-1.6, 1.6): put(bm, box(1.0, 2.6, 1.4, STONE, bevel=0.04), T(x, -0.6, 0))
    put(bm, box(4.2, 0.4, 1.0, STONE, bevel=0.03), T(0, 2.0, 0))
    put(bm, box(2.2, 1.6, 0.18, WOOD), T(0, 0.3, 0.2))
    for k in range(4): put(bm, box(2.2, 0.06, 0.04, METAL), T(0, 0.45 + k * 0.4, 0.1))
    for x in (-0.5, 0.5): put(bm, box(0.08, 1.4, 0.08, METAL), T(x, 2.4, 0.2))
    put(bm, wheel(0.45, 0.08, 6, METAL), T(0, 3.6, 0.2))
    for x in (-2.1, 2.1): put(bm, box(0.06, 0.9, 0.06, WOOD), T(x, 2.4, -0.45))
    put(bm, box(4.2, 0.06, 0.06, WOOD), T(0, 3.2, -0.45))
    moss_tops(bm, 21, cut=0.6)
    return bm


def cottage():
    bm = bmesh.new()
    put(bm, box(3.0, 1.9, 2.2, STEM, bevel=0.04))
    put(bm, box(3.1, 0.3, 2.3, STONE))
    for x in (-1.0, 0.2, 1.4): put(bm, box(0.1, 1.9, 0.05, BARK), T(x, 0, -1.12))
    put(bm, box(0.55, 1.2, 0.04, WOOD), T(-0.4, 0.25, -1.12))
    put(bm, box(0.5, 0.45, 0.04, COAL), T(0.8, 0.95, -1.12))
    th = roof(3.0, 2.2, 1.5, REED, overhang=0.3); displace(th, 0.04, 4, 3); put(bm, th, T(0, 1.9, 0))
    put(bm, box(0.4, 1.0, 0.4, STONE), T(-1.1, 2.6, 0.4))
    put(bm, roof(1.2, 1.6, 0.5, WOOD, overhang=0.05), T(2.1, 1.1, 0) @ Rz(-20))
    well = bmesh.new(); lathe(well, [(0.6, 0), (0.6, 0.7), (0.45, 0.7), (0.45, 0.1)], 10, STONE)
    put(bm, well, T(3.2, 0, -1.6))
    for x in (2.75, 3.65): put(bm, box(0.08, 1.4, 0.08, WOOD), T(x, 0.7, -1.6))
    put(bm, roof(1.1, 0.9, 0.45, WOOD, overhang=0.05), T(3.2, 2.1, -1.6))
    return bm


def mill_yard():
    bm = bmesh.new()
    put(bm, box(2.0, 0.4, 1.1, WOOD, bevel=0.03), T(0, 0.5, 0))
    for x in (-0.6, 0.6): put(bm, wheel(0.5, 0.1, 8, WOOD), T(x, 0.5, -0.62))
    put(bm, box(0.08, 0.08, 1.6, WOOD), T(-1.6, 0.55, 0) @ Ry(90))
    for k in range(5):
        sk = ico(0.32, 1, CLOTH)
        for v in sk.verts: v.co.y *= 0.75
        put(bm, sk, T(-0.6 + (k % 3) * 0.55, 1.15 + (k // 3) * 0.35, 0.1))
    for k, (x, z) in enumerate(((-2.4, 0.6), (-2.9, 1.3))):
        ms = bmesh.new(); lathe(ms, [(0.06, 0), (0.7, 0), (0.7, 0.24), (0.06, 0.24)], 14, STONE); put(bm, ms, T(x, 0, z) @ Rx(80 if k else 0))
    for x in (1.6, 3.0): put(bm, box(0.14, 3.0, 0.14, WOOD), T(x, 0, -1.4))
    put(bm, box(1.8, 0.14, 0.14, WOOD), T(2.3, 2.9, -1.4))
    put(bm, box(0.04, 1.4, 0.04, CLOTH), T(2.3, 1.5, -1.4))
    return bm


def cairn():
    rng = random.Random(41); bm = bmesh.new(); y = 0
    for k in range(6):
        r = 0.9 - k * 0.11
        st = chiselled(r, 10 + k, planes=8, soften=0.15, subdiv=1)
        for v in st.verts: v.co.y *= 0.55
        put(bm, st, T(rng.uniform(-.1, .1), y + r * 0.45, rng.uniform(-.1, .1)), row=ROCK)
        y += r * 0.8
    cap = ico(0.45, 1, STEM)
    for v in cap.verts: v.co.y = max(v.co.y, 0) * 0.5
    put(bm, cap, T(0, y + 0.05, 0))
    return bm


def graves():
    rng = random.Random(42); bm = bmesh.new()
    for k in range(6):
        x, z = rng.uniform(-2.0, 2.0), rng.uniform(-1.0, 1.0)
        put(bm, box(0.5, 0.75, 0.12, STONE, bevel=0.04), T(x, -0.08, z) @ Ry(rng.uniform(-15, 15)) @ Rx(rng.uniform(-14, 14)))
        md = ico(0.5, 1, STEM)
        for v in md.verts: v.co = Vector((v.co.x * 0.7, max(v.co.y, -0.05) * 0.3, v.co.z * 1.2))
        put(bm, md, T(x, 0, z - 0.7))
    return bm


def bellpost():
    bm = bmesh.new()
    put(bm, box(0.14, 2.6, 0.14, WOOD))
    put(bm, box(0.8, 0.1, 0.1, WOOD), T(0.3, 2.5, 0))
    bl = bmesh.new(); lathe(bl, [(0.22, 0), (0.18, 0.08), (0.12, 0.3), (0.02, 0.36)], 8, METAL, cap_top=True)
    put(bm, bl, T(0.6, 2.05, 0))
    return bm


def stilt_hut():
    bm = bmesh.new()
    for px in (-0.9, 0.9):
        for pz in (-0.8, 0.8): put(bm, box(0.12, 1.6, 0.12, WOOD), T(px, -0.2, pz) @ Rz(px * 2))
    put(bm, box(2.2, 0.1, 2.0, WOOD), T(0, 1.4, 0))
    put(bm, box(1.8, 1.3, 1.6, WOOD, bevel=0.02), T(0, 1.5, 0))
    put(bm, box(0.4, 0.7, 0.04, COAL), T(0.3, 1.7, -0.82))
    th = cone_roof(1.5, 1.4, 9, REED); displace(th, 0.06, 4, 2); put(bm, th, T(0, 2.75, 0))
    for x in (-0.45, 0.05): put(bm, box(0.05, 1.5, 0.05, WOOD), T(x, 1.5, -0.95) @ Rx(-80))   # ladder drawn up
    return bm


def plankwalk():
    bm = bmesh.new()
    for k in range(7):
        put(bm, box(0.45, 0.06, 1.0, WOOD), T(-1.5 + k * 0.5, 0.5 + (k % 2) * 0.03, 0) @ Ry((k % 3 - 1) * 4))
        if k % 2 == 0:
            for sz in (-0.45, 0.45): put(bm, box(0.08, 0.6, 0.08, WOOD), T(-1.5 + k * 0.5, 0, sz))
    return bm


def fishing_gear():
    bm = bmesh.new()
    for k in range(2):
        tr = bmesh.new(); lathe(tr, [(0.3, 0), (0.25, 0.4), (0.06, 0.9)], 8, REED); put(bm, tr, T(1.2 + k * 0.6, 0.3, 0.4) @ Rz(80))
    for x in (-1.2, 0.0): put(bm, box(0.07, 1.8, 0.07, WOOD), T(x, 0, 0))
    net = bmesh.new()
    g = [[net.verts.new((-1.2 + c * 0.3, 1.7 - r * 0.3 - 0.08 * math.sin(c / 4 * math.pi), 0)) for c in range(5)] for r in range(4)]
    for r in range(3):
        for c in range(4): net.faces.new((g[r][c], g[r][c + 1], g[r + 1][c + 1], g[r + 1][c])).material_index = CLOTH
    put(bm, two_sided(net))
    return bm


def salt_pans():
    bm = bmesh.new()
    for k in range(3):
        put(bm, box(1.6, 0.12, 1.1, STONE), T(-1.8 + k * 1.8, 0, 0))
        put(bm, box(1.4, 0.02, 0.9, STEM), T(-1.8 + k * 1.8, 0.1, 0))
    for k in range(3):
        hp = bmesh.new(); lathe(hp, [(0.45, 0), (0.001, 0.55)], 8, STEM); put(bm, hp, T(-1.5 + k * 1.3, 0, 1.2))
    put(bm, box(0.06, 0.06, 1.4, WOOD), T(2.2, 0.05, 1.0) @ Ry(40))
    return bm


def arcade(bays=3, ruined=False, seed=3):
    """Modelled pass: pointed (equilateral) arches swept along a true curve on clustered piers."""
    rng = random.Random(seed); bm = bmesh.new()
    span, pierH, d = 2.4, 3.0, 0.55
    for k in range(bays + 1):
        x = (k - bays / 2) * span
        pr = bmesh.new()
        lathe(pr, [(0.36, 0), (0.3, 0.3), (0.26, 0.4), (0.26, pierH), (0.34, pierH + 0.2)], 8, STONE)
        for j in range(4):
            a = j / 4 * math.tau + math.pi / 4
            sh = bmesh.new(); lathe(sh, [(0.08, 0.35), (0.08, pierH)], 5, STONE); put(pr, sh, T(math.cos(a) * 0.27, 0, math.sin(a) * 0.27))
        put(bm, pr, T(x, 0, 0))
    for k in range(bays):
        if ruined and k == bays - 1: continue
        x0 = (k - bays / 2) * span; r = span; n = 7; y0 = pierH + 0.2
        left = [(x0 + span - r * math.cos(math.radians(60) * j / n), y0 + r * math.sin(math.radians(60) * j / n)) for j in range(n + 1)]
        right = [(x0 + r * math.cos(math.radians(60) * (n - j) / n), y0 + r * math.sin(math.radians(60) * (n - j) / n)) for j in range(n + 1)]
        curve = left + right[1:]
        ring = [[bm.verts.new((cx, cy + dy, dz)) for dy, dz in ((0, -d / 2), (0.34, -d / 2), (0.34, d / 2), (0, d / 2))] for cx, cy in curve]
        for a, b in zip(ring, ring[1:]):
            for i in range(4): bm.faces.new((a[i], b[i], b[(i + 1) % 4], a[(i + 1) % 4])).material_index = STONE
    if ruined:
        for k in range(3): put(bm, chiselled(0.35, seed + k, planes=8, subdiv=1), T(bays / 2 * span - 0.5 + k * 0.4, 0.15, rng.uniform(-0.8, 0.8)), row=STONE)
    moss_tops(bm, seed, cut=0.65)
    return bm


def west_front():
    wf = bmesh.new()
    put(wf, box(6.0, 5.5, 0.6, STONE, bevel=0.04), T(0, -0.8, 0))
    put(wf, roof(6.0, 0.6, 2.2, STONE, overhang=0), T(0, 4.7, 0))
    for sx in (-1, 1): put(wf, box(0.7, 6.4, 0.9, STONE, bevel=0.03), T(sx * 3.2, -0.8, -0.1))
    ring = bmesh.new(); lathe(ring, [(1.5, -0.35), (1.5, 0.35), (1.25, 0.35), (1.25, -0.35)], 20, STONE); put(wf, ring, T(0, 3.0, -0.4) @ Rx(90))
    glass = bmesh.new(); lathe(glass, [(1.26, 0), (0.001, 0)], 20, GLOW); put(wf, glass, T(0, 3.0, -0.42) @ Rx(-90))
    for k in range(8): put(wf, box(0.08, 1.2, 0.08, STONE), T(0, 3.0, -0.5) @ Rz(k * 45))
    put(wf, box(1.4, 2.2, 0.05, COAL), T(0, -0.6, -0.33))
    moss_tops(wf, 9, cut=0.6)
    return wf


def market_cross():
    bm = bmesh.new()
    for k in range(3):
        st = bmesh.new(); r = 1.2 - k * 0.3; lathe(st, [(r, k * 0.25), (r, k * 0.25 + 0.25), (0.001, k * 0.25 + 0.25)], 8, STONE); put(bm, st)
    sh = bmesh.new(); lathe(sh, [(0.16, 0), (0.12, 2.2)], 8, STONE); put(bm, sh, T(0.4, 0.75, 0.6) @ Rz(80))
    put(bm, box(0.7, 0.5, 0.5, STONE, bevel=0.03), T(2.4, 0.0, 0.5) @ Rz(30))
    for k in range(2):
        put(bm, box(1.4, 0.08, 0.8, WOOD), T(-2.2, 0.9, -0.6 + k * 1.4) @ Rz(8 * (1 - 2 * k)))
        for x in (-2.8, -1.6): put(bm, box(0.07, 0.9, 0.07, WOOD), T(x, 0, -0.6 + k * 1.4))
        put(bm, roof(1.4, 0.8, 0.3, CLOTH, overhang=0.1), T(-2.2, 1.6, -0.6 + k * 1.4))
    return bm


def buttress():
    bm = bmesh.new()
    put(bm, box(0.9, 0.6, 2.6, STONE, bevel=0.04), Rz(-6))
    for k in range(4): put(bm, chiselled(0.35, 40 + k, planes=8, subdiv=1), T(-1.0 + k * 0.6, 0.15, -1.6 - (k % 2) * 0.4), row=STONE)
    return bm


def glass_spire(seed=1):
    rng = random.Random(seed); bm = bmesh.new()
    for k in range(4):
        h = rng.uniform(1.6, 3.6) * (1.4 if k == 0 else 1)
        put(bm, shard(h, GLOW), T(rng.uniform(-.8, .8) if k else 0, -0.2, rng.uniform(-.6, .6) if k else 0) @ Ry(rng.uniform(0, 60)) @ Rz(rng.uniform(-15, 15)) @ S(0.8, 1, 0.8))
    return bm


def fulgurite():
    pts = [Vector((-1.5, 0, 0)), Vector((-1.4, 1.4, 0.1)), Vector((-0.6, 2.6, 0)), Vector((0.5, 2.7, -0.1)), Vector((1.3, 1.6, 0)), Vector((1.6, 0, 0.1))]
    a = skin_tree((pts, [(i, i + 1) for i in range(5)]), [0.3, 0.26, 0.22, 0.22, 0.26, 0.3], subsurf=1, decimate=0.5)
    for f in a.faces: f.material_index = STONE
    return a


def glass_wastes():
    bm = bmesh.new()
    for k in range(6):
        a = (k / 5) * math.pi
        put(bm, glass_spire(10 + k), T(math.cos(a) * 3.6, 0, math.sin(a) * 1.8) @ S(1.2, 1.3 + 0.4 * (k in (2, 3)), 1.2))
    put(bm, fulgurite(), T(0, 0, 1.0) @ S(1.4, 1.4, 1.4))
    return bm


def rope_bridge():
    bm = bmesh.new()
    for sx in (-1, 1):
        for sz in (-0.5, 0.5): put(bm, box(0.14, 1.4, 0.14, WOOD), T(sx * 3.0, 0, sz))
    for k in range(12):
        x = -2.75 + k * 0.5; sag = 0.5 * math.sin(math.pi * (k + 0.5) / 12)
        put(bm, box(0.4, 0.05, 0.9, WOOD), T(x, 0.9 - sag, 0) @ Rz((k % 3 - 1) * 4))
    for sz in (-0.5, 0.5):
        for k in range(12):
            x = -2.75 + k * 0.5; sag = 0.4 * math.sin(math.pi * (k + 0.5) / 12)
            put(bm, box(0.5, 0.03, 0.03, CLOTH), T(x, 1.6 - sag, sz))
    return bm


def bothy():
    bm = bmesh.new()
    put(bm, box(2.6, 1.5, 2.0, STONE, bevel=0.05))
    put(bm, roof(2.6, 2.0, 1.1, EARTH), T(0, 1.5, 0))
    put(bm, box(0.5, 1.0, 0.04, COAL), T(-0.5, 0, -1.03))
    put(bm, box(0.4, 0.9, 0.4, STONE), T(1.0, 2.0, 0.3))
    return bm


def scree():
    rng = random.Random(61); bm = bmesh.new()
    for k in range(9):
        r = rng.uniform(0.2, 0.55)
        put(bm, chiselled(r, 60 + k, planes=7, subdiv=1), T(rng.uniform(-2, 2), r * 0.4, rng.uniform(-1.2, 1.2)), row=ROCK)
    return bm


def storm_altar():
    bm = bmesh.new()
    put(bm, box(1.4, 0.8, 0.9, STONE, bevel=0.05))
    put(bm, box(1.2, 0.05, 0.7, COAL), T(0, 0.8, 0))
    for k in range(4):
        a = k / 4 * math.tau + 0.4
        put(bm, box(0.06, 2.6, 0.06, METAL), T(math.cos(a) * 1.4, 0, math.sin(a) * 1.0))
        put(bm, chiselled(0.3, 70 + k, planes=7, subdiv=1), T(math.cos(a) * 1.4, 0.1, math.sin(a) * 1.0), row=COAL)
    return bm


def mine_cart():
    bm = bmesh.new()
    for sz in (-0.4, 0.4): put(bm, box(4.0, 0.06, 0.06, METAL), T(0, 0.06, sz))
    for k in range(8): put(bm, box(0.15, 0.06, 1.1, WOOD), T(-1.8 + k * 0.52, 0, 0))
    cart = bmesh.new()
    put(cart, box(1.1, 0.6, 0.8, METAL, bevel=0.03), T(0, 0.25, 0))
    for x in (-0.35, 0.35):
        for sz in (-0.42, 0.42): put(cart, wheel(0.17, 0.06, 4, METAL), T(x, 0.17, sz))
    put(bm, cart, T(1.6, 0.4, 0.9) @ Rx(-80))
    for k in range(4): put(bm, chiselled(0.18, 80 + k, planes=6, subdiv=1), T(1.0 + k * 0.3, 0.08, 1.6), row=ROCK)
    return bm


def winch():
    bm = bmesh.new()
    for x in (-0.4, 0.4): put(bm, box(0.12, 2.2, 0.12, WOOD), T(x, 0, 0) @ Rz(-x * 20))
    put(bm, box(1.2, 0.12, 0.12, WOOD), T(0, 2.1, 0))
    put(bm, wheel(0.35, 0.1, 6, WOOD), T(0, 1.6, 0) @ Ry(90))
    put(bm, box(0.03, 1.5, 0.03, CLOTH), T(0, 0.1, 0))
    return bm


def sinkhole():
    rng = random.Random(81); bm = bmesh.new()
    gl = ico(1.0, 1, GLOW)
    for v in gl.verts: v.co = Vector((v.co.x * 1.6, min(v.co.y, 0) * 0.3 + 0.02, v.co.z * 1.1))
    put(bm, gl)
    for k in range(10):
        a = k / 10 * math.tau
        put(bm, chiselled(rng.uniform(0.35, 0.6), 85 + k, planes=7, subdiv=1), T(math.cos(a) * 1.8, 0.1, math.sin(a) * 1.3), row=ROCK)
    return bm


def shrine_post():
    bm = bmesh.new()
    put(bm, box(0.18, 1.4, 0.18, STONE))
    put(bm, box(0.5, 0.5, 0.4, STONE, bevel=0.03), T(0, 1.4, 0))
    put(bm, roof(0.6, 0.5, 0.3, STONE, overhang=0.06), T(0, 1.9, 0))
    put(bm, box(0.28, 0.28, 0.05, COAL), T(0, 1.5, -0.2))
    put(bm, ico(0.06, 1, GLOW), T(0, 1.52, -0.18))
    return bm


def fallen_bell():
    bm = bmesh.new()
    bl = bmesh.new(); lathe(bl, [(0.9, 0), (0.8, 0.12), (0.55, 0.85), (0.5, 1.4), (0.18, 1.6), (0.001, 1.65)], 12, TRIM)
    put(bm, bl, T(0, 0.6, 0) @ Rz(70))
    put(bm, box(1.8, 0.2, 0.2, WOOD), T(0.8, 0.1, 0.9) @ Ry(30))
    return bm


def chapel():
    bm = bmesh.new()
    put(bm, box(3.6, 2.6, 0.4, STONE, bevel=0.03), T(0, 0, 1.6))
    for sx in (-1, 1): put(bm, box(0.4, 2.2 - (0.8 if sx > 0 else 0), 3.2, STONE, bevel=0.03), T(sx * 1.8, 0, 0))
    put(bm, box(0.5, 1.2, 0.06, COAL), T(0, 1.0, 1.38))
    for k in range(4): put(bm, chiselled(0.3, 90 + k, planes=7, subdiv=1), T(-0.8 + k * 0.5, 0.12, -0.6 + (k % 2) * 0.4), row=STONE)
    moss_tops(bm, 91, cut=0.6)
    return bm


def nave_steps():
    bm = bmesh.new()
    for k in range(5): put(bm, box(4.0, 0.22, 0.6, STONE, bevel=0.02), T(0, 0.22 * (4 - k) - 0.6, k * 0.6))
    for sx in (-1, 1):
        pr = bmesh.new(); lathe(pr, [(0.3, 0), (0.26, 2.2)], 8, STONE); put(bm, pr, T(sx * 2.3, -0.4, 1.0))
    return bm


def torch_post():
    bm = bmesh.new()
    put(bm, box(0.12, 1.8, 0.12, WOOD))
    bs = bmesh.new(); lathe(bs, [(0.05, 1.7), (0.18, 1.85), (0.2, 1.95)], 6, METAL); put(bm, bs)
    put(bm, flames(2, 0.05, 0.5, 2), T(0, 1.9, 0))
    return bm


def gatehouse():
    bm = bmesh.new()
    for sx in (-1, 1):
        tw = bmesh.new(); rings = lathe(tw, [(1.1, 0), (1.0, 3.5)], 10, STONE)
        rng = random.Random(100 + sx)
        for v in rings[-1]: v.co.y -= rng.uniform(0, 1.0)
        put(bm, tw, T(sx * 2.4, 0, 0))
    put(bm, box(3.0, 0.8, 1.0, STONE, bevel=0.03), T(0, 2.6, 0))
    put(bm, box(1.6, 2.4, 0.05, COAL), T(0, 0, -0.45))
    for k in range(5): put(bm, chiselled(0.4, 110 + k, planes=7, subdiv=1), T(-1 + k * 0.5, 0.15, -1.2), row=STONE)
    return bm


# ------------------------------------------------------------------ act 2 pieces
def crane():
    bm = bmesh.new()
    for x in (-0.6, 0.6):
        for z in (-0.6, 0.6): put(bm, box(0.16, 3.6, 0.16, WOOD), T(x, 0, z) @ Rz(-x * 6) @ Rx(z * 6))
    put(bm, box(4.0, 0.2, 0.2, WOOD), T(1.2, 3.6, 0) @ Rz(12))
    put(bm, box(0.03, 2.0, 0.03, CLOTH), T(3.0, 2.0, 0))
    put(bm, box(0.6, 0.5, 0.6, WOOD, bevel=0.02), T(3.0, 1.6, 0))
    return bm


def warehouse():
    bm = bmesh.new()
    put(bm, box(4.0, 2.4, 2.6, WOOD, bevel=0.02))
    for x in (-1.5, -0.5, 0.5, 1.5): put(bm, box(0.12, 2.4, 0.06, BARK), T(x, 0, -1.32))
    put(bm, box(1.4, 1.8, 0.05, COAL), T(0, 0, -1.33))
    put(bm, roof(4.0, 2.6, 1.1, EARTH), T(0, 2.4, 0))
    return bm


def market_stall():
    bm = bmesh.new()
    put(bm, box(1.8, 0.1, 0.9, WOOD), T(0, 0.85, 0))
    for x in (-0.85, 0.85):
        for z in (-0.4, 0.4): put(bm, box(0.07, 1.8 if z < 0 else 1.5, 0.07, WOOD), T(x, 0, z))
    put(bm, roof(1.9, 1.0, 0.3, CLOTH, overhang=0.1), T(0, 1.6, 0) @ Rx(-8))
    for k in range(3): put(bm, box(0.5, 0.18, 0.35, WOOD), T(-0.55 + k * 0.55, 0.95, 0))
    return bm


def hanging_tree():
    bm = mp.dead_tree(12)
    for (x, y, z) in ((0.6, 1.8, 0.2), (-0.5, 1.6, -0.3)): put(bm, box(0.02, 0.6, 0.02, CLOTH), T(x, y - 0.6, z))
    return bm


def spirit_lantern():
    """One spirit lantern on its stake; the fen lair snuffs them one by one."""
    bm = bmesh.new()
    put(bm, box(0.06, 1.3, 0.06, BARK))
    cg = bmesh.new(); lathe(cg, [(0.02, 1.3), (0.12, 1.35), (0.1, 1.6), (0.02, 1.7)], 5, METAL); put(bm, cg)
    put(bm, ico(0.1, 1, GLOW), T(0, 1.48, 0))
    return bm


def opened_grave():
    bm = mp.mound(5)
    for k in range(3):
        sp = ico(0.6, 1, EARTH)
        for v in sp.verts: v.co.y = max(v.co.y, 0) * 0.5
        put(bm, sp, T(-2.8 + k * 0.6, 0, -1.8 - k * 0.3))
    put(bm, box(1.2, 1.4, 0.05, COAL), T(0, 0, -1.55))
    put(bm, box(0.05, 1.2, 0.05, WOOD), T(-2.2, 0, -2.4) @ Rz(25))
    put(bm, box(0.25, 0.3, 0.03, METAL), T(-2.45, 0.05, -2.4) @ Rz(25))
    return bm


def kiln():
    bm = bmesh.new()
    lathe(bm, [(1.1, 0), (1.0, 0.8), (0.5, 1.6), (0.25, 1.8)], 10, EARTH, cap_top=True)
    put(bm, box(0.5, 0.5, 0.05, GLOW), T(0, 0.1, -1.05))
    for k in range(6):
        lg = bmesh.new(); lathe(lg, [(0.1, -0.7), (0.1, 0.7)], 6, BARK, cap_top=True, cap_bottom=True)
        put(bm, lg, T(1.6, 0.1 + (k // 3) * 0.2, -0.3 + (k % 3) * 0.22) @ Rz(90))
    return bm


def burning_arch():
    pts = [Vector((-2.0, 0, 0)), Vector((-1.8, 2.0, 0)), Vector((-0.8, 3.6, 0)), Vector((0.8, 3.6, 0)), Vector((1.8, 2.0, 0)), Vector((2.0, 0, 0))]
    bm = skin_tree((pts, [(i, i + 1) for i in range(5)]), [0.45, 0.35, 0.28, 0.28, 0.35, 0.45], subsurf=1, decimate=0.5)
    for k in range(4): put(bm, canopy_clump(0.9, 1, 200 + k, LEAVES[k % 3]), T(-1.5 + k, 3.9 + (k % 2) * 0.3, 0))
    put(bm, flames(4, 1.6, 1.0, 9))
    return bm


def cinder_stream():
    rng = random.Random(91); bm = bmesh.new()
    pts = [(-4 + k * 0.8, 0.02, math.sin(k * 0.7) * 0.6) for k in range(11)]
    for (x0, y0, z0), (x1, y1, z1) in zip(pts, pts[1:]):
        w = 0.35
        a, b, c, d = bm.verts.new((x0, y0, z0 - w)), bm.verts.new((x1, y1, z1 - w)), bm.verts.new((x1, y1, z1 + w)), bm.verts.new((x0, y0, z0 + w))
        bm.faces.new((a, d, c, b)).material_index = GLOW
    for k in range(8):
        x, y, z = pts[k + 1]
        for sz in (-1, 1): put(bm, chiselled(rng.uniform(0.2, 0.35), 95 + k + sz, planes=7, subdiv=1), T(x, 0.08, z + sz * 0.6), row=COAL)
    return bm


def roots():
    pts = [Vector((0, 0.3, 0))]; edges = []; radii = [0.6]
    for k in range(5):
        a = k / 5 * math.tau
        pts.append(Vector((math.cos(a) * 2.8, -0.2, math.sin(a) * 2.2))); edges.append((0, len(pts) - 1)); radii.append(0.18)
    return skin_tree((pts, edges), radii, subsurf=1, decimate=0.5)


def shipwreck():
    bm = bmesh.new()
    put(bm, box(0.3, 0.3, 7.0, WOOD))
    for k in range(9):
        z = -3.0 + k * 0.75; w = 1.6 * math.sin(math.pi * (k + 0.5) / 9) + 0.3
        for sx in (-1, 1):
            put(bm, segment((0, 0, z), (sx * w, 0.8, z), 0.12, WOOD))
            put(bm, segment((sx * w, 0.8, z), (sx * w * 1.05, 2.0 - (k % 3) * 0.3, z), 0.1, WOOD))
    for k in range(4): put(bm, box(1.4, 0.06, 0.3, WOOD), T(-0.9, 0.9 + k * 0.25, -2.5 + k * 0.4) @ Rz(30))
    put(bm, box(0.22, 4.0, 0.22, WOOD), T(0.4, 0, 0.8) @ Rz(-35))
    sail = bmesh.new()
    g = [[sail.verts.new((0.5 + c * 0.45 + r * 0.5, 2.6 - r * 0.6 - 0.15 * math.sin(c), 0.9 + 0.2 * math.sin(r + c))) for c in range(4)] for r in range(3)]
    for r in range(2):
        for c in range(3): sail.faces.new((g[r][c], g[r][c + 1], g[r + 1][c + 1], g[r + 1][c])).material_index = CLOTH
    put(bm, two_sided(sail))
    return bm


def beacon():
    bm = bmesh.new()
    rings = lathe(bm, [(1.7, 0), (1.5, 2.5), (1.4, 5.0)], 12, STONE)
    rng = random.Random(23)
    for v in rings[-1]: v.co.y -= rng.uniform(0, 1.3)
    put(bm, box(0.5, 1.0, 0.05, COAL), T(0, 0.3, -1.68))
    put(bm, box(0.35, 0.6, 0.05, COAL), T(0.6, 3.0, -1.5))
    bs = bmesh.new(); lathe(bs, [(0.1, 0), (0.6, 0.4), (0.7, 0.75)], 8, METAL); put(bm, bs, T(-0.4, 4.0, 0))
    for k in range(5): put(bm, chiselled(0.35, 30 + k, planes=8, subdiv=1), T(2.0 + k * 0.3, 0.15, -1.0 + (k % 2)), row=STONE)
    moss_tops(bm, 23, cut=0.5)
    return bm


def sea_stacks():
    rng = random.Random(24); bm = bmesh.new()
    for k in range(3):
        h = rng.uniform(4.0, 7.0)
        sp = chiselled(1.0, 170 + k, planes=9, soften=0.06, subdiv=1)
        for v in sp.verts:
            t = (v.co.y + 1) / 2
            v.co = Vector((v.co.x * (1.0 - 0.35 * t), t * h, v.co.z * (1.0 - 0.35 * t)))
        put(bm, sp, T(k * 2.6 - 2.6, -0.3, rng.uniform(-1, 1)) @ S(1.2, 1, 1.0), row=ROCK)
    put(bm, box(0.14, 2.6, 0.14, WOOD), T(4.0, 0, -1.0))
    put(bm, box(1.2, 1.0, 1.2, WOOD, bevel=0.03), T(4.0, 2.6, -1.0))
    return bm


def sea_throne():
    """Modelled pass: a stepped headland of cliff strata, carved steps up its face, the Hollow King's
    throne on the top ledge between two jagged spires, chains running down to the sea."""
    rng = random.Random(15); bm = bmesh.new()
    y = 0.0
    for i, (w, h) in enumerate(((5.4, 2.4), (4.4, 2.2), (3.4, 2.1))):
        blk = chiselled(1.0, 150 + i, planes=10, soften=0.03, subdiv=2, cliff=True)
        for v in blk.verts:
            yy = max(min(v.co.y * 1.6, 0.5), -0.5)
            v.co = Vector((v.co.x * w / 2, (yy + 0.5) * h, v.co.z * w / 3))
        put(bm, blk, T(rng.uniform(-.2, .2), y, 0.25 * i), row=ROCK)
        y += h * 0.95
    for k in range(8):  # steps cut up the face
        put(bm, box(1.1, 0.25, 0.55, STONE), T(1.6 - k * 0.1, 0.55 * k, -1.85 + k * 0.12))
    for sx in (-1, 1):
        sp = chiselled(1.0, 160 + sx, planes=8, soften=0.05, subdiv=1)
        for v in sp.verts:
            t = (v.co.y + 1) / 2
            v.co = Vector((v.co.x * (1 - 0.75 * t) * 0.9, t * (y + 3.5), v.co.z * (1 - 0.75 * t) * 0.9))
        put(bm, sp, T(sx * 2.6, 0, 0.8) @ Rz(-sx * 5), row=ROCK)
    th = bmesh.new()
    put(th, box(1.8, 0.5, 1.4, STONE, bevel=0.04))
    put(th, box(1.8, 3.0, 0.4, STONE, bevel=0.04), T(0, 0.5, 0.55))
    for sx in (-1, 1): put(th, box(0.35, 1.0, 1.3, STONE, bevel=0.03), T(sx * 0.9, 0.5, 0))
    for k, x in enumerate((-0.65, 0, 0.65)): put(th, shard(0.9 + 0.4 * (k == 1), METAL), T(x, 3.5, 0.55))
    put(th, shard(1.0), T(0, 1.6, 0.2) @ Rx(-20))
    put(bm, th, T(0, y, 0.6))
    for sx in (-1, 1):
        for k in range(7):
            ln = bmesh.new(); lathe(ln, [(0.12, -0.18), (0.12, 0.18)], 6, METAL, cap_top=True, cap_bottom=True)
            put(bm, ln, T(sx * (1.3 + k * 0.4), y - 0.3 - k * 0.8, -1.4 - k * 0.25) @ Rx(90 * (k % 2)))
    return bm


def sunken_cathedral():
    """Flooded nave (two arcades) and the west front with its rose window."""
    bm = bmesh.new()
    for sx in (-1, 1): put(bm, arcade(2, ruined=sx > 0, seed=5 + sx), T(sx * 2.6, -0.6, 2.4) @ Ry(90))
    put(bm, west_front(), T(0, 0, 6.6))
    return bm


PIECES = {
    "wagon": wagon, "waystone": waystone, "hedge": hedge, "tollhut": tollhut, "camp": camp, "fence": fence,
    "rowboat": rowboat, "sluice_gate": sluice_gate, "cottage": cottage, "mill_yard": mill_yard,
    "cairn": cairn, "graves": graves, "bellpost": bellpost, "stilt_hut": stilt_hut, "plankwalk": plankwalk,
    "fishing_gear": fishing_gear, "salt_pans": salt_pans, "arcade": arcade, "market_cross": market_cross, "buttress": buttress,
    "glass_spire": glass_spire, "fulgurite": fulgurite, "glass_wastes": glass_wastes,
    "rope_bridge": rope_bridge, "bothy": bothy, "scree": scree, "storm_altar": storm_altar,
    "mine_cart": mine_cart, "winch": winch, "sinkhole": sinkhole,
    "shrine_post": shrine_post, "fallen_bell": fallen_bell, "chapel": chapel, "nave_steps": nave_steps, "sunken_cathedral": sunken_cathedral,
    "torch_post": torch_post, "gatehouse": gatehouse,
    "crane": crane, "warehouse": warehouse, "market_stall": market_stall, "hanging_tree": hanging_tree, "spirit_lantern": spirit_lantern,
    "opened_grave": opened_grave, "kiln": kiln, "burning_arch": burning_arch, "cinder_stream": cinder_stream, "roots": roots,
    "shipwreck": shipwreck, "beacon": beacon, "sea_stacks": sea_stacks, "sea_throne": sea_throne,
}
