# Story landmarks: one signature set-piece per campaign chapter plus its small story props
# (docs/sigilbound/art/STORY_SCENES.md). MOCK-UP STAGE: these build and bake like the prop kit
# (make_props.py: material rows, baked shade, GLOW faces in submesh 1) and are rendered by
# story_review.py for approval; nothing is wired into the game yet.
# Moving parts that the boss stage animates (water wheel, bell, lamp, flames) are separate meshes
# with their pivot at the origin, named "<landmark>_<part>"; LANDMARKS lists where each one sits.
import math, random
import make_props as mp  # imports bpy first (bmesh needs it)
import bmesh
from mathutils import Vector, Matrix, noise
from make_props import (lathe, add_bm, ico, box, two_sided, blade, canopy_clump, skin_tree, chiselled, displace, moss_tops,
                        T, S, Ry, Rx, Rz, BARK, LEAVES, LEAF_A, ROCK, MOSS, WOOD, STONE, CLOTH, METAL, CAP, STEM, REED, EARTH, TRIM, COAL, GLOW)


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
    bm = bmesh.new()
    lathe(bm, [(r, 0), (0.001, h)], sides, row)
    return bm


def wheel(r, width, spokes, row, paddles=False):
    """A wheel in the XY plane (axle along Z), centred on the origin."""
    bm = bmesh.new()
    rim = bmesh.new(); lathe(rim, [(r, -width / 2), (r, width / 2)], 14, row)
    put(bm, rim, Rx(90))
    inner = bmesh.new(); lathe(inner, [(r * 0.88, width / 2), (r * 0.88, -width / 2)], 14, row)
    put(bm, inner, Rx(90))
    for k in range(spokes):
        a = k / spokes * 360
        sp = box(0.06, r * 0.9, 0.06, row); put(bm, sp, Rz(a))
        if paddles:
            pd = box(0.08, 0.45, width * 1.4, WOOD); put(bm, pd, Rz(a) @ T(0, r - 0.05, 0))
    hub = bmesh.new(); lathe(hub, [(0.12, -width * 0.7), (0.12, width * 0.7)], 8, METAL, cap_top=True, cap_bottom=True)
    put(bm, hub, Rx(90))
    return bm


def flames(n, r, h, seed):
    """Glowing flame tongues (GLOW), for pyres and braziers in the mock (the game uses particles too)."""
    rng = random.Random(seed); bm = bmesh.new()
    for k in range(n):
        a = rng.uniform(0, math.tau); d = rng.uniform(0, r)
        f = bmesh.new(); lathe(f, [(0.18, 0), (0.22, h * 0.25), (0.001, h * rng.uniform(0.6, 1.0))], 5, GLOW)
        put(bm, f, T(math.cos(a) * d, 0, math.sin(a) * d) @ Rz(rng.uniform(-12, 12)))
    return bm


def shard(h, row=GLOW):
    """A Sigil shard: a tall double-pointed crystal."""
    bm = bmesh.new()
    rings = lathe(bm, [(0.001, 0), (h * 0.18, h * 0.3), (h * 0.14, h * 0.75)], 6, row)
    tip = bm.verts.new((0, h, 0))
    for i in range(6): bm.faces.new((rings[-1][i], rings[-1][(i + 1) % 6], tip)).material_index = row
    return bm


# ------------------------------------------------------------------ act 1
def thornwick():
    """Thornwick Road: a waystone at a fork, a broken wagon on its side, a thorn hedge, a gallows-sign."""
    rng = random.Random(1); bm = bmesh.new()
    # broken wagon: tipped bed, one wheel on, one lying flat, a snapped shaft
    bed = box(2.4, 0.5, 1.3, WOOD, bevel=0.03); put(bm, bed, T(0, 0.55, 0) @ Rx(-14) @ Rz(6))
    for x in (-1.1, 1.1):
        rail = box(0.08, 0.5, 1.3, WOOD); put(bm, rail, T(x, 1.0, 0.1) @ Rx(-14))
    put(bm, wheel(0.55, 0.12, 8, WOOD), T(-0.8, 0.55, -0.75))
    put(bm, wheel(0.55, 0.12, 8, WOOD), T(1.6, 0.07, 0.9) @ Rx(90))
    put(bm, box(0.09, 0.09, 2.0, WOOD), T(-1.6, 0.25, -0.2) @ Ry(70) @ Rx(-8))
    for k in range(3):  # spilled sacks
        sk = ico(0.28, 1, CLOTH)
        for v in sk.verts: v.co.y *= 0.7
        put(bm, sk, T(0.4 + k * 0.45, 0.15, -1.0 - (k % 2) * 0.3))
    # waystone with a carved arrow-cap and a hanging sign
    ws = chiselled(0.35, 4, planes=8, soften=0.05)
    for v in ws.verts: v.co = Vector((v.co.x * 0.8, (v.co.y + 0.35) * 2.4, v.co.z * 0.6))
    put(bm, ws, T(-3.4, 0, 0.6), row=STONE)
    put(bm, box(0.08, 2.4, 0.08, WOOD), T(-3.0, 0, 1.3))
    put(bm, box(0.9, 0.35, 0.05, WOOD), T(-2.6, 1.9, 1.3) @ Rz(-4))
    # thorn hedge: low tangles of crooked thorny branches with dark leaves
    for k in range(5):
        x = 2.8 + k * 0.9
        tg = skin_tree(([Vector((0, 0, 0)), Vector((0.3, 0.6, 0.1)), Vector((-0.3, 0.9, -0.2)), Vector((0.5, 1.1, 0.3)), Vector((-0.5, 0.5, 0.4))],
                        [(0, 1), (1, 2), (1, 3), (0, 4)]), [0.08, 0.05, 0.02, 0.02, 0.03], subsurf=0)
        put(bm, tg, T(x, 0, 1.2 + rng.uniform(-.3, .3)) @ Ry(rng.uniform(0, 360)))
        c = canopy_clump(0.55, 1, k + 30, LEAVES[k % 3]); put(bm, c, T(x, 0.55, 1.3) @ S(1.2, 0.8, 1.0))
    return bm


def drowned_mill():
    """The Drowned Mill: a stone-and-timber mill sunk to its sills and tilted, a millrace, a weir."""
    bm = bmesh.new()
    house = bmesh.new()
    put(house, box(3.2, 2.2, 2.6, STONE, bevel=0.05))                      # stone ground floor
    put(house, box(3.4, 1.6, 2.8, WOOD, bevel=0.03), T(0, 2.2, 0))          # timber upper floor
    for x in (-1.0, 0, 1.0):                                               # timber framing
        put(house, box(0.1, 1.6, 0.06, BARK), T(x, 2.2, -1.43))
    put(house, roof(3.4, 2.8, 1.5, EARTH), T(0, 3.8, 0))                   # thatch-dark roof
    put(house, box(0.5, 1.2, 0.5, STONE), T(1.0, 4.2, 0.6))                 # chimney
    for x in (-0.9, 0.9):                                                  # dark windows
        put(house, box(0.45, 0.55, 0.04, COAL), T(x, 2.6, -1.42))
    put(bm, house, Rz(-5) @ Rx(3) @ T(0, -0.9, 0))                          # sunk and leaning
    put(bm, box(0.3, 0.3, 3.0, WOOD), T(-1.9, 1.4, 0) @ Rx(90) @ T(0, -1.5, 0))  # axle housing out of the wall
    # a broken weir of stakes and planks
    for k in range(7):
        put(bm, box(0.12, 1.0, 0.12, WOOD), T(-6.0 + k * 0.55, -0.4, 1.6 + (k % 2) * 0.1) @ Rz((k - 3) * 3))
    put(bm, box(3.6, 0.12, 0.08, WOOD), T(-4.3, 0.35, 1.65) @ Rz(4))
    # millstones dumped on the bank
    for k, (x, z) in enumerate(((3.2, -1.6), (3.6, -0.6))):
        ms = bmesh.new(); lathe(ms, [(0.06, 0), (0.65, 0), (0.65, 0.22), (0.06, 0.22)], 14, STONE, cap_top=False)
        put(bm, ms, T(x, 0, z) @ Rx(0 if k == 0 else 75))
    return bm


def drowned_mill_wheel():
    """Turns slowly; spins up on crits (boss stage). Axle along Z here; placed turned to face the camera."""
    return wheel(1.7, 0.6, 8, WOOD, paddles=True)


def ashgrave():
    """Ashgrave Pass: a pass gate of two ash-capped cairns, leaning headstones, a mourners' bell-post."""
    rng = random.Random(3); bm = bmesh.new()
    for sx in (-1, 1):  # two tall cairns flanking the old road
        y = 0
        for k in range(6):
            r = 0.9 - k * 0.11
            st = chiselled(r, 10 + k + sx, planes=8, soften=0.15, subdiv=1)
            for v in st.verts: v.co.y *= 0.55
            put(bm, st, T(sx * 3.2 + rng.uniform(-.1, .1), y + r * 0.45, rng.uniform(-.1, .1)), row=ROCK)
            y += r * 0.8
        cap = ico(0.45, 1, STEM)                                           # ash cap (pale)
        for v in cap.verts: v.co.y = max(v.co.y, 0) * 0.5
        put(bm, cap, T(sx * 3.2, y + 0.05, 0))
    for k in range(9):  # graveyard in the pass
        x, z = rng.uniform(-2.4, 2.4), rng.uniform(0.6, 3.2)
        hs = box(0.5, 0.75, 0.12, STONE, bevel=0.04)
        put(bm, hs, T(x, -0.08, z) @ Ry(rng.uniform(-15, 15)) @ Rx(rng.uniform(-14, 14)))
        mound = ico(0.5, 1, STEM)
        for v in mound.verts: v.co = Vector((v.co.x * 0.7, max(v.co.y, -0.05) * 0.3, v.co.z * 1.2))
        put(bm, mound, T(x, 0, z - 0.7))
    # the mourners' post with an iron bell
    put(bm, box(0.14, 2.6, 0.14, WOOD), T(0, 0, 4.0))
    put(bm, box(0.8, 0.1, 0.1, WOOD), T(0.3, 2.5, 4.0))
    bell = bmesh.new(); lathe(bell, [(0.22, 0), (0.18, 0.08), (0.12, 0.3), (0.02, 0.36)], 8, METAL, cap_top=True)
    put(bm, bell, T(0.6, 2.05, 4.0))
    return bm


def saltmarsh():
    """Saltmarsh Reaches: two stilt huts joined by a plank walk, eel traps and drying nets."""
    bm = bmesh.new()
    for (x, z, s, yaw) in ((-1.6, 0, 1.0, 10), (2.2, 1.2, 0.85, -20)):
        h = bmesh.new()
        for px in (-0.9, 0.9):
            for pz in (-0.8, 0.8):
                put(h, box(0.12, 1.6, 0.12, WOOD), T(px, -0.2, pz) @ Rz(px * 2))
        put(h, box(2.2, 0.1, 2.0, WOOD), T(0, 1.4, 0))                      # platform
        put(h, box(1.8, 1.3, 1.6, WOOD, bevel=0.02), T(0, 1.5, 0))         # walls
        put(h, box(0.4, 0.7, 0.04, COAL), T(0.3, 1.7, -0.82))               # door
        th = cone_roof(1.5, 1.4, 9, REED)                                    # reed thatch
        displace(th, 0.06, 4, 2)
        put(h, th, T(0, 2.75, 0))
        for k in range(5):                                                  # ladder
            put(h, box(0.5, 0.05, 0.05, WOOD), T(-0.2, 0.2 + k * 0.28, -1.1))
        put(h, box(0.05, 1.5, 0.05, WOOD), T(-0.45, 0, -1.1)); put(h, box(0.05, 1.5, 0.05, WOOD), T(0.05, 0, -1.1))
        put(bm, h, T(x, 0, z) @ Ry(yaw) @ S(s, s, s))
    for k in range(6):  # the plank walk
        put(bm, box(0.45, 0.06, 1.0, WOOD), T(-0.2 + k * 0.5, 0.9 + (k % 2) * 0.03, 0.6) @ Ry(8))
        put(bm, box(0.08, 0.95, 0.08, WOOD), T(-0.2 + k * 0.5, 0, 0.15))
    for k in range(2):  # eel traps (wicker cones)
        tr = bmesh.new(); lathe(tr, [(0.3, 0), (0.25, 0.4), (0.06, 0.9)], 8, REED)
        put(bm, tr, T(3.8 + k * 0.6, 0.3, -0.6) @ Rz(80))
    net = bmesh.new()                                                       # a drying net between two poles
    for x in (-4.0, -2.8): put(bm, box(0.07, 1.8, 0.07, WOOD), T(x, 0, -0.5))
    g = [[net.verts.new((-4.0 + c * 0.3, 1.7 - r * 0.3 - 0.08 * math.sin(c / 4 * math.pi), -0.5)) for c in range(5)] for r in range(4)]
    for r in range(3):
        for c in range(4): net.faces.new((g[r][c], g[r][c + 1], g[r + 1][c + 1], g[r + 1][c])).material_index = CLOTH
    put(bm, two_sided(net))
    return bm


def bellspire():
    """Bellspire Ruins: a tall leaning bell tower, belfry open on four sides, a fallen buttress."""
    bm = bmesh.new()
    t = bmesh.new()
    put(t, box(2.4, 0.6, 2.4, STONE, bevel=0.05))                          # plinth
    put(t, box(2.0, 6.0, 2.0, STONE, bevel=0.04), T(0, 0.6, 0))             # shaft
    for y in (2.2, 4.2):                                                   # string courses
        put(t, box(2.15, 0.15, 2.15, STONE), T(0, y, 0))
    put(t, box(0.4, 0.9, 0.04, COAL), T(0, 3.0, -1.02))                     # slit windows
    put(t, box(0.4, 0.9, 0.04, COAL), T(0, 5.0, -1.02))
    for px in (-0.85, 0.85):                                               # belfry piers
        for pz in (-0.85, 0.85):
            put(t, box(0.3, 1.9, 0.3, STONE), T(px, 6.6, pz))
    put(t, box(2.3, 0.3, 2.3, STONE, bevel=0.03), T(0, 8.5, 0))
    sp = bmesh.new(); lathe(sp, [(1.3, 0), (0.001, 2.4)], 4, EARTH)          # slate spire, half its tiles gone
    put(t, sp, T(0, 8.8, 0) @ Ry(45))
    put(t, box(0.1, 0.9, 0.1, METAL), T(0, 11.1, 0))
    moss_tops(t, 5, cut=0.6)
    put(bm, t, Rz(8) @ Rx(-3))                                             # the lean
    fb = box(0.9, 0.6, 2.6, STONE, bevel=0.04); put(bm, fb, T(2.6, 0, 1.0) @ Ry(30) @ Rz(-6))
    for k in range(4):
        put(bm, chiselled(0.35, 40 + k, planes=8, subdiv=1), T(-2.0 + k * 0.6, 0.15, -1.6 - (k % 2) * 0.4), row=STONE)
    return bm


def bellspire_bell():
    """Swings and tolls on kills (boss stage); pivot at the yoke. Placed in the leaning belfry."""
    bm = bmesh.new()
    lathe(bm, [(0.62, -1.15), (0.56, -1.05), (0.4, -0.6), (0.36, -0.2), (0.12, -0.05), (0.001, 0)], 12, TRIM, cap_bottom=False)
    put(bm, box(1.0, 0.14, 0.14, WOOD), T(0, 0, 0))
    return bm


def glass_wastes():
    """The Glass Wastes: a forest of fused glass spires around a fulgurite arch (lightning-made glass)."""
    rng = random.Random(6); bm = bmesh.new()
    for k in range(9):
        a = (k / 9) * math.pi + rng.uniform(-.2, .2)
        x, z = math.cos(a) * rng.uniform(2.0, 4.5), math.sin(a) * rng.uniform(0.3, 2.5)
        h = rng.uniform(2.5, 6.5) * (1.4 if k == 4 else 1)
        sp = shard(h, GLOW)
        put(bm, sp, T(x, -0.3, z) @ Ry(rng.uniform(0, 60)) @ Rz(rng.uniform(-12, 12)) @ S(0.8, 1, 0.8))
    # a fulgurite arch: a crooked glass tube bent over
    pts = [Vector((-1.5, 0, 0)), Vector((-1.4, 1.4, 0.1)), Vector((-0.6, 2.6, 0)), Vector((0.5, 2.7, -0.1)), Vector((1.3, 1.6, 0)), Vector((1.6, 0, 0.1))]
    arch = skin_tree((pts, [(i, i + 1) for i in range(5)]), [0.3, 0.26, 0.22, 0.22, 0.26, 0.3], subsurf=1, decimate=0.6)
    for f in arch.faces: f.material_index = STONE
    put(bm, arch, T(0, 0, 3.0))
    for k in range(5):  # glassy shards strewn about
        put(bm, shard(rng.uniform(0.4, 0.9), GLOW), T(rng.uniform(-3, 3), 0, rng.uniform(-1.5, 1)) @ Rz(rng.uniform(40, 80)) @ Ry(rng.uniform(0, 360)))
    return bm


def stormcrown():
    """Stormcrown Heights: a crag whose tips ring like a crown, iron lightning rods at the points."""
    rng = random.Random(7); bm = bmesh.new()
    base = chiselled(3.2, 70, planes=12, soften=0.2)
    for v in base.verts: v.co = Vector((v.co.x * 1.3, max(v.co.y, -0.4) * 0.55 + 0.2, v.co.z * 0.9))
    put(bm, base, row=ROCK)
    for k in range(7):  # the crown points
        a = k / 7 * math.tau
        h = rng.uniform(3.5, 5.0) * (1.25 if k % 2 == 0 else 0.85)
        sp = chiselled(0.7, 80 + k, planes=8, soften=0.05)
        for v in sp.verts:
            t = (v.co.y + 0.7) / 1.4
            v.co = Vector((v.co.x * (1.0 - 0.75 * t), t * h, v.co.z * (1.0 - 0.75 * t)))
        put(bm, sp, T(math.cos(a) * 2.2, 1.2, math.sin(a) * 1.5) @ Rz(-math.cos(a) * 12) @ Rx(math.sin(a) * 12), row=ROCK)
        if k % 2 == 0:
            put(bm, box(0.06, 1.2, 0.06, METAL), T(math.cos(a) * 2.2, 1.2 + h - 0.2, math.sin(a) * 1.5))
    moss_tops(bm, 7, row=STEM, cut=0.75)                                    # snow caps
    return bm


def hollow_deep():
    """Hollow Deep: a cavern mouth of heaped slabs, Hollow light pulsing inside, a miners' winch."""
    rng = random.Random(8); bm = bmesh.new()
    for k in range(9):  # an arch of big slabs
        a = math.pi * k / 8
        sl = chiselled(1.1, 90 + k, planes=9, soften=0.1)
        put(bm, sl, T(-math.cos(a) * 3.0, math.sin(a) * 2.8 + 0.4, rng.uniform(-.3, .3)) @ S(1.1, 1.0, 1.5), row=ROCK)
    for sx in (-1, 1):
        put(bm, chiselled(1.6, 100 + sx, planes=10), T(sx * 4.4, 0.8, 0.6) @ S(1, 1.4, 1.2), row=ROCK)
    # the dark throat and its glow
    th = bmesh.new()
    rings = lathe(th, [(2.4, 0), (2.4, 0.01)], 16, COAL)
    put(bm, th, T(0, 1.2, 0.5) @ Rx(90) @ S(1, 1, 1.15))
    glow = ico(0.9, 1, GLOW)
    for v in glow.verts: v.co.z *= 0.2
    put(bm, glow, T(0, 1.5, 0.9) @ S(1.6, 1.3, 1))
    for k in range(4):  # Hollow crystals at the lip
        put(bm, shard(rng.uniform(0.6, 1.3)), T(rng.uniform(-2.5, 2.5), 0, rng.uniform(-1.6, -0.6)) @ Rz(rng.uniform(-25, 25)))
    # a miners' winch frame and rope
    for x in (3.2, 4.0): put(bm, box(0.12, 2.2, 0.12, WOOD), T(x, 0, -1.6) @ Rz((x - 3.6) * -20))
    put(bm, box(1.2, 0.12, 0.12, WOOD), T(3.6, 2.1, -1.6))
    put(bm, wheel(0.35, 0.1, 6, WOOD), T(3.6, 1.6, -1.6) @ Ry(90))
    return bm


def sunken_cathedral():
    """Sunken Cathedral: a flooded nave of pointed arches and a broken west front with a rose window."""
    bm = bmesh.new()
    for sx in (-1, 1):  # two arcades of three bays wading into the water
        for k in range(3):
            if sx == 1 and k == 2: continue  # a fallen bay
            z = k * 2.4
            put(bm, box(0.55, 4.2, 0.55, STONE, bevel=0.03), T(sx * 2.4, -0.8, z))
            for j in range(6):                                              # pointed arch of voussoirs
                a = math.radians(10 + j * 14)
                put(bm, box(0.45, 0.35, 0.5, STONE, bevel=0.02), T(sx * 2.4, 3.4 + math.sin(a) * 1.1, z + 1.2 - math.cos(a) * 1.2) @ Rx(-math.degrees(a) + 90))
    # the west front: a gable wall with the rose window
    wf = bmesh.new()
    put(wf, box(6.0, 5.5, 0.6, STONE, bevel=0.04), T(0, -0.8, 0))
    put(wf, roof(6.0, 0.6, 2.2, STONE, overhang=0), T(0, 4.7, 0))
    ring = bmesh.new(); lathe(ring, [(1.5, -0.35), (1.5, 0.35), (1.25, 0.35), (1.25, -0.35)], 20, STONE)
    put(wf, ring, T(0, 3.0, -0.4) @ Rx(90))
    glass = bmesh.new(); lathe(glass, [(1.26, 0), (0.001, 0)], 20, GLOW)  # stained glass disc
    put(wf, glass, T(0, 3.0, -0.42) @ Rx(-90))
    for k in range(8):  # tracery spokes
        put(wf, box(0.08, 1.2, 0.08, STONE), T(0, 3.0, -0.5) @ Rz(k * 45))
    put(wf, box(1.4, 2.2, 0.05, COAL), T(0, -0.6, -0.33))                  # drowned doorway
    moss_tops(wf, 9, cut=0.6)
    put(bm, wf, T(0, 0, 7.4) @ Rz(-3))
    return bm


def last_pyre():
    """The Last Pyre: a stepped stone dais carrying a giant log pyre, a ring of torch posts."""
    rng = random.Random(10); bm = bmesh.new()
    for k in range(3):  # dais steps
        r = 3.6 - k * 0.8
        st = bmesh.new(); lathe(st, [(r, 0), (r, 0.35), (0.001, 0.35)], 10, STONE, cap_bottom=False)
        put(bm, st, T(0, k * 0.35, 0))
    for layer in range(7):  # log crib, crossing each layer
        y = 1.05 + layer * 0.32; w = 2.6 - layer * 0.26
        for j in range(4):
            off = (j - 1.5) * w / 3.4
            lg = bmesh.new(); lathe(lg, [(0.16, -w / 2 - 0.2), (0.16, w / 2 + 0.2)], 6, BARK, cap_top=True, cap_bottom=True)
            put(bm, lg, T(off if layer % 2 else 0, y, 0 if layer % 2 else off) @ (Rx(90) if layer % 2 else Rz(90)))
    put(bm, flames(9, 0.9, 2.4, 3), T(0, 3.2, 0))
    put(bm, ico(0.9, 1, COAL), T(0, 3.1, 0) @ S(1.2, 0.4, 1.2))
    for k in range(6):  # torch posts round the dais
        a = math.pi * (0.1 + 0.8 * k / 5)
        x, z = math.cos(a) * 4.4, -math.sin(a) * 1.0 + 1.2
        put(bm, box(0.12, 1.8, 0.12, WOOD), T(x, 0, z))
        put(bm, flames(2, 0.05, 0.5, k), T(x, 1.8, z))
    return bm


# ------------------------------------------------------------------ act 2
def lighthouse():
    """Saltreach Harbor: the old lighthouse on its rock, a pier with mooring posts, a sunk skiff."""
    bm = bmesh.new()
    rk = chiselled(2.6, 110, planes=11, soften=0.2)
    for v in rk.verts: v.co = Vector((v.co.x * 1.2, max(v.co.y, -0.8) * 0.5 + 0.3, v.co.z))
    put(bm, rk, row=ROCK)
    tw = bmesh.new()
    lathe(tw, [(1.3, 0), (1.1, 3.0), (0.95, 6.5)], 10, STEM)                # whitewashed tower
    for y in (1.6, 4.2):                                                   # red bands
        b = bmesh.new(); lathe(b, [(1.22 - y * 0.05, y), (1.2 - y * 0.05, y + 0.6)], 10, CLOTH); put(tw, b)
    put(tw, bmesh.new() if False else box(1.0, 0.1, 1.0, METAL), T(0, 6.5, 0) @ S(2.4, 1, 2.4))   # gallery deck
    for k in range(10):
        a = k / 10 * math.tau
        put(tw, box(0.04, 0.5, 0.04, METAL), T(math.cos(a) * 1.15, 6.6, math.sin(a) * 1.15))
    put(tw, box(0.04, 6.1, 0.04, COAL), T(0, 1.5, -0.88) @ S(8, 0.12, 1))   # door slot
    for k in range(6):                                                     # lantern room frame
        a = k / 6 * math.tau
        put(tw, box(0.06, 1.1, 0.06, METAL), T(math.cos(a) * 0.6, 6.6, math.sin(a) * 0.6))
    put(tw, cone_roof(0.85, 0.8, 8, METAL), T(0, 7.7, 0))
    put(bm, tw, T(0, 1.2, 0))
    for k in range(6):  # the pier
        put(bm, box(1.4, 0.08, 0.5, WOOD), T(-3.6 + 0.0, 0.45, -1.0 - k * 0.55))
        put(bm, box(0.14, 1.2, 0.14, WOOD), T(-4.2, -0.6, -1.0 - k * 0.55))
        put(bm, box(0.14, 1.2, 0.14, WOOD), T(-3.0, -0.6, -1.0 - k * 0.55))
    sk = bmesh.new(); lathe(sk, [(0.001, -1.0), (0.45, -0.6), (0.5, 0.3), (0.001, 0.9)], 8, WOOD)
    for v in sk.verts: v.co.x *= 0.8; v.co.y = v.co.y
    put(bm, sk, T(2.8, 0.05, -1.6) @ Rx(90) @ Rz(70) @ S(1, 1, 0.45))
    return bm


def lighthouse_lamp():
    """The Flame shard as the lamp: turns its beam, flashes on crits (boss stage)."""
    bm = ico(0.42, 2, GLOW)
    beam = bmesh.new()
    lathe(beam, [(0.3, 0), (1.4, 7.0)], 8, GLOW)
    put(bm, beam, Rz(-90))
    return bm


def drowned_shrine():
    """The Black Fens: a small drowned shrine, its roof slid askew, the Shadow shard in a dark niche."""
    rng = random.Random(12); bm = bmesh.new()
    sh = bmesh.new()
    put(sh, box(3.4, 0.4, 3.0, STONE, bevel=0.04), T(0, -0.3, 0))
    for px in (-1.3, 1.3):
        for pz in (-1.1, 1.1):
            if px > 0 and pz > 0: continue  # a fallen column
            col = bmesh.new(); lathe(col, [(0.22, 0), (0.18, 2.6)], 8, STONE); put(sh, col, T(px, 0, pz))
    put(sh, roof(3.6, 3.2, 1.0, EARTH), T(0, 2.6, 0) @ Rz(9) @ T(0.3, 0, 0))
    put(sh, box(0.9, 1.4, 0.6, COAL), T(0, 0.1, 0.6))                      # the niche
    put(sh, shard(1.1), T(0, 0.4, 0.4))                                    # Shadow shard (glow: violet)
    moss_tops(sh, 12, cut=0.4)
    put(bm, sh, Rx(4) @ T(0, -0.5, 0))
    fallen = bmesh.new(); lathe(fallen, [(0.22, 0), (0.18, 2.4)], 8, STONE)
    put(bm, fallen, T(2.2, 0.15, 1.8) @ Rz(84) @ Ry(20))
    for k in range(6):  # spirit lanterns on stakes
        x, z = rng.uniform(-4, 4), rng.uniform(-2.2, -1.2)
        put(bm, box(0.06, 1.3, 0.06, BARK), T(x, 0, z))
        put(bm, ico(0.12, 1, GLOW), T(x, 1.35, z))
    return bm


def kings_barrow():
    """Wraithmoor Barrows: the king's barrow, a lintelled portal, a ring of stones; the Tide shard above."""
    bm = bmesh.new()
    md = ico(1.0, 3, EARTH)
    displace(md, 0.05, 2.5, 13)
    for v in md.verts: v.co = Vector((v.co.x * 4.5, max(v.co.y, -0.1) * 2.2, v.co.z * 3.5))
    md.normal_update()
    for f in md.faces:
        if f.normal.y > 0.35 and noise.noise(f.calc_center_median() * 0.6) > -0.3: f.material_index = MOSS
    put(bm, md)
    for x in (-0.9, 0.9): put(bm, box(0.6, 2.2, 0.7, STONE, bevel=0.04), T(x, 0, -3.3))   # portal
    put(bm, box(2.6, 0.55, 0.9, STONE, bevel=0.04), T(0, 2.2, -3.3))
    put(bm, box(1.2, 2.1, 0.05, COAL), T(0, 0, -3.5))
    for k in range(7):  # ring of standing stones in front
        a = math.pi * (1.1 + 0.8 * k / 6)
        st = chiselled(0.45, 120 + k, planes=8, soften=0.08)
        for v in st.verts: v.co = Vector((v.co.x * 0.7, (v.co.y + 0.45) * 2.2, v.co.z * 0.5))
        put(bm, st, T(math.cos(a) * 5.6, 0, math.sin(a) * 3.0 - 1.0) @ Ry(-math.degrees(a)), row=STONE)
    put(bm, shard(1.6), T(0, 2.6, -0.4))                                    # Tide shard on the crown of the mound
    return bm


def great_tree():
    """The Ember Groves: a vast burning tree split open at the heart, the Light shard inside."""
    rng = random.Random(14)
    pts = [Vector((0, 0, 0)), Vector((0, 2.5, 0)), Vector((0.3, 5.0, 0.1))]
    edges = [(0, 1), (1, 2)]; radii = [1.2, 0.9, 0.6]
    for k in range(5):  # buttress roots
        a = k / 5 * math.tau
        pts.append(Vector((math.cos(a) * 2.2, -0.2, math.sin(a) * 2.2))); edges.append((0, len(pts) - 1)); radii.append(0.35)
    for k in range(5):  # great limbs
        a = k / 5 * math.tau + 0.4
        tip = Vector((math.cos(a) * 3.6, 7.0 + rng.uniform(-.6, .6), math.sin(a) * 3.0))
        pts.append(tip); edges.append((2, len(pts) - 1)); radii.append(0.25)
    bm = skin_tree((pts, edges), radii, decimate=0.6)
    for k in range(9):  # burning canopy masses (leaf rows glow in the ember theme ramp)
        a = rng.uniform(0, math.tau); d = rng.uniform(1.0, 3.4)
        c = canopy_clump(rng.uniform(1.3, 1.8), 1, 140 + k, LEAVES[k % 3])
        put(bm, c, T(math.cos(a) * d, 7.0 + rng.uniform(-0.5, 1.4), math.sin(a) * d * 0.8))
    put(bm, box(0.8, 1.8, 0.4, COAL), T(0, 1.2, -1.0))                     # the split heart
    put(bm, shard(1.3), T(0, 1.4, -1.15))                                  # Light shard
    put(bm, flames(6, 1.6, 1.4, 14), T(0, 0, 0))
    return bm


def sea_throne():
    """The Storm Coast: a sea cliff with the Hollow King's throne carved into its brow, chains to the sea."""
    rng = random.Random(15); bm = bmesh.new()
    cl = chiselled(3.0, 150, planes=12, soften=0.08)
    for v in cl.verts: v.co = Vector((v.co.x * 1.6, (v.co.y + 3.0) * 1.15, v.co.z * 0.9))
    put(bm, cl, row=ROCK)
    for sx in (-1, 1):  # jagged flanking spires
        sp = chiselled(1.0, 160 + sx, planes=8, soften=0.05)
        for v in sp.verts:
            t = (v.co.y + 1) / 2
            v.co = Vector((v.co.x * (1 - 0.7 * t), t * 8.5, v.co.z * (1 - 0.7 * t)))
        put(bm, sp, T(sx * 3.6, 0, 0.4) @ Rz(-sx * 6), row=ROCK)
    th = bmesh.new()                                                       # the throne
    put(th, box(1.8, 0.5, 1.4, STONE, bevel=0.04))
    put(th, box(1.8, 3.2, 0.4, STONE, bevel=0.04), T(0, 0.5, 0.55))
    for sx in (-1, 1):
        put(th, box(0.35, 1.0, 1.3, STONE, bevel=0.03), T(sx * 0.9, 0.5, 0))
        for k in range(3):  # crown spikes on the back
            put(th, shard(0.9 + 0.3 * (k == 1), METAL), T(sx * 0.6 * (k != 1) * (1 if k == 2 else -1) if k != 1 else 0, 3.7, 0.55))
    put(th, shard(1.0), T(0, 1.6, 0.2) @ Rx(-20))                          # Gale shard bound to the seat
    put(bm, th, T(0, 6.9, -0.6))
    for sx in (-1, 1):  # chains down to the sea
        for k in range(8):
            ln = bmesh.new(); lathe(ln, [(0.12, -0.18), (0.12, 0.18)], 6, METAL, cap_top=True, cap_bottom=True)
            put(bm, ln, T(sx * (1.4 + k * 0.35), 6.6 - k * 0.85, -1.4 - k * 0.2) @ Rx(90 * (k % 2)))
    return bm


# ------------------------------------------------------------------ route stops (the two mock strips)
def rowboat():
    bm = bmesh.new()
    lathe(bm, [(0.001, -1.4), (0.5, -0.9), (0.62, 0.0), (0.5, 0.9), (0.001, 1.3)], 10, WOOD)
    for v in bm.verts: v.co = Vector((v.co.x, v.co.y, v.co.z))
    hull = bmesh.new(); add_bm(hull, bm, Rx(90) @ S(1, 1, 0.5)); bm.free()
    for v in hull.verts:
        if v.co.y > 0: v.co.y = 0.02          # open top
    put(hull, box(1.1, 0.05, 0.25, WOOD), T(0, 0.0, 0.2))
    put(hull, box(0.05, 0.05, 1.8, WOOD), T(0.4, 0.05, -0.5) @ Ry(20))  # an oar
    return hull


def sluice_gate():
    """A stone sluice with a timber gate half raised and an iron winding wheel."""
    bm = bmesh.new()
    for x in (-1.6, 1.6): put(bm, box(1.0, 2.6, 1.4, STONE, bevel=0.04), T(x, -0.6, 0))
    put(bm, box(4.2, 0.4, 1.0, STONE, bevel=0.03), T(0, 2.0, 0))           # walkway lintel
    put(bm, box(2.2, 1.6, 0.18, WOOD), T(0, 0.3, 0.2))                     # the gate leaf
    for k in range(4): put(bm, box(2.2, 0.06, 0.04, METAL), T(0, 0.45 + k * 0.4, 0.1))
    for x in (-0.5, 0.5): put(bm, box(0.08, 1.4, 0.08, METAL), T(x, 2.4, 0.2))
    put(bm, wheel(0.45, 0.08, 6, METAL), T(0, 3.6, 0.2))
    for x in (-2.1, 2.1): put(bm, box(0.06, 0.9, 0.06, WOOD), T(x, 2.4, -0.45))   # railing
    put(bm, box(4.2, 0.06, 0.06, WOOD), T(0, 3.2, -0.45))
    moss_tops(bm, 21, cut=0.6)
    return bm


def cottage():
    """The miller's cottage: whitewashed walls, thatch, a lean-to, a picket fence and a well."""
    bm = bmesh.new()
    put(bm, box(3.0, 1.9, 2.2, STEM, bevel=0.04))
    put(bm, box(3.1, 0.3, 2.3, STONE), T(0, 0, 0))
    for x in (-1.0, 0.2, 1.4): put(bm, box(0.1, 1.9, 0.05, BARK), T(x, 0, -1.12))
    put(bm, box(0.55, 1.2, 0.04, WOOD), T(-0.4, 0.25, -1.12))
    put(bm, box(0.5, 0.45, 0.04, COAL), T(0.8, 0.95, -1.12))
    th = roof(3.0, 2.2, 1.5, REED, overhang=0.3); displace(th, 0.04, 4, 3); put(bm, th, T(0, 1.9, 0))
    put(bm, box(0.4, 1.0, 0.4, STONE), T(-1.1, 2.6, 0.4))
    put(bm, roof(1.2, 1.6, 0.5, WOOD, overhang=0.05), T(2.1, 1.1, 0) @ Rz(-20))  # lean-to
    for k in range(9):  # picket fence
        put(bm, box(0.08, 0.7, 0.05, WOOD), T(-2.2 + k * 0.5, 0, -2.4))
    put(bm, box(4.2, 0.06, 0.04, WOOD), T(-0.2, 0.5, -2.42))
    well = bmesh.new(); lathe(well, [(0.6, 0), (0.6, 0.7), (0.45, 0.7), (0.45, 0.1)], 10, STONE)
    put(bm, well, T(3.2, 0, -1.6))
    for x in (2.75, 3.65): put(bm, box(0.08, 1.4, 0.08, WOOD), T(x, 0.7, -1.6))
    put(bm, roof(1.1, 0.9, 0.45, WOOD, overhang=0.05), T(3.2, 2.1, -1.6))
    return bm


def mill_yard():
    """Sacks, a laden cart, millstones and a hoist frame in the yard."""
    bm = bmesh.new()
    put(bm, box(2.0, 0.4, 1.1, WOOD, bevel=0.03), T(0, 0.5, 0))
    for x in (-0.6, 0.6): put(bm, wheel(0.5, 0.1, 8, WOOD), T(x, 0.5, -0.62))
    put(bm, box(0.08, 0.08, 1.6, WOOD), T(-1.6, 0.55, 0) @ Ry(90))
    for k in range(5):
        sk = ico(0.32, 1, CLOTH)
        for v in sk.verts: v.co.y *= 0.75
        put(bm, sk, T(-0.6 + (k % 3) * 0.55, 1.15 + (k // 3) * 0.35, 0.1))
    for k in range(4):
        sk = ico(0.32, 1, CLOTH)
        for v in sk.verts: v.co.y *= 0.75
        put(bm, sk, T(2.0 + (k % 2) * 0.6, 0.22 + (k // 2) * 0.4, 0.8))
    for k, (x, z) in enumerate(((-2.4, 0.6), (-2.9, 1.3))):
        ms = bmesh.new(); lathe(ms, [(0.06, 0), (0.7, 0), (0.7, 0.24), (0.06, 0.24)], 14, STONE); put(bm, ms, T(x, 0, z) @ Rx(80 if k else 0))
    for x in (1.6, 3.0): put(bm, box(0.14, 3.0, 0.14, WOOD), T(x, 0, -1.4))   # hoist
    put(bm, box(1.8, 0.14, 0.14, WOOD), T(2.3, 2.9, -1.4))
    put(bm, box(0.04, 1.4, 0.04, CLOTH), T(2.3, 1.5, -1.4))
    return bm


def shipwreck():
    """A wreck on the shingle: keel and bare ribs, a snapped mast, a torn sail."""
    bm = bmesh.new()
    put(bm, box(0.3, 0.3, 7.0, WOOD), T(0, 0, 0))
    for k in range(9):
        z = -3.0 + k * 0.75; w = 1.6 * math.sin(math.pi * (k + 0.5) / 9) + 0.3
        for sx in (-1, 1):
            rib = skin_tree(([Vector((0, 0, 0)), Vector((sx * w, 0.8, 0)), Vector((sx * w * 1.05, 2.0 - (k % 3) * 0.3, 0))], [(0, 1), (1, 2)]), [0.1, 0.08, 0.06], subsurf=0)
            for f in rib.faces: f.material_index = WOOD
            put(bm, rib, T(0, 0, z))
    for k in range(4): put(bm, box(1.4, 0.06, 0.3, WOOD), T(-0.9, 0.9 + k * 0.25, -2.5 + k * 0.4) @ Rz(30))
    put(bm, box(0.22, 4.0, 0.22, WOOD), T(0.4, 0, 0.8) @ Rz(-35))
    sail = bmesh.new()
    g = [[sail.verts.new((0.5 + c * 0.45 + r * 0.5, 2.6 - r * 0.6 - 0.15 * math.sin(c), 0.9 + 0.2 * math.sin(r + c))) for c in range(4)] for r in range(3)]
    for r in range(2):
        for c in range(3): sail.faces.new((g[r][c], g[r][c + 1], g[r + 1][c + 1], g[r + 1][c])).material_index = CLOTH
    put(bm, two_sided(sail))
    return bm


def beacon():
    """A dead coastal beacon: a stout round tower, broken crown, a cold iron fire-basket."""
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
        sp = chiselled(1.0, 170 + k, planes=9, soften=0.06)
        for v in sp.verts:
            t = (v.co.y + 1) / 2
            v.co = Vector((v.co.x * (1.0 - 0.35 * t), t * h, v.co.z * (1.0 - 0.35 * t)))
        put(bm, sp, T(k * 2.6 - 2.6, -0.3, rng.uniform(-1, 1)) @ S(1.2, 1, 1.0), row=ROCK)
    put(bm, box(0.14, 2.6, 0.14, WOOD), T(4.0, 0, -1.0))                   # Hollow watch post
    put(bm, box(1.2, 1.0, 1.2, WOOD, bevel=0.03), T(4.0, 2.6, -1.0))
    return bm


def bolt():
    """A frozen lightning bolt for the mock (in game: a LineRenderer, ArenaReactor.Lightning)."""
    rng = random.Random(25); bm = bmesh.new()
    p = Vector((0, 22, 0))
    for k in range(10):
        q = p + Vector((rng.uniform(-1.4, 1.4), -2.2, rng.uniform(-0.4, 0.4)))
        seg = bmesh.new(); lathe(seg, [(0.12, 0), (0.08, (q - p).length)], 4, GLOW)
        d = (q - p).normalized(); rot = Vector((0, 1, 0)).rotation_difference(d).to_matrix().to_4x4()
        put(bm, seg, Matrix.Translation(p) @ rot)
        p = q
    return bm


# name: builder; moving parts are separate entries ("<landmark>_<part>")
LANDMARKS = {
    "thornwick": thornwick, "drowned_mill": drowned_mill, "drowned_mill_wheel": drowned_mill_wheel,
    "ashgrave": ashgrave, "saltmarsh": saltmarsh, "bellspire": bellspire, "bellspire_bell": bellspire_bell,
    "glass_wastes": glass_wastes, "stormcrown": stormcrown, "hollow_deep": hollow_deep,
    "sunken_cathedral": sunken_cathedral, "last_pyre": last_pyre,
    "lighthouse": lighthouse, "lighthouse_lamp": lighthouse_lamp, "drowned_shrine": drowned_shrine,
    "kings_barrow": kings_barrow, "great_tree": great_tree, "sea_throne": sea_throne,
    # route stops for the mock strips (Drowned Mill, Storm Coast)
    "rowboat": rowboat, "sluice_gate": sluice_gate, "cottage": cottage, "mill_yard": mill_yard,
    "shipwreck": shipwreck, "beacon": beacon, "sea_stacks": sea_stacks, "bolt": bolt,
}


def build_all():
    out = {}
    for name, make in LANDMARKS.items():
        bm = make()
        verts, subs = mp.bake(bm, sum(map(ord, name)) % 997)
        bm.free()
        subs = [s for s in subs if s] if subs[1] else [subs[0]]
        out[name] = (verts, subs)
    return out
