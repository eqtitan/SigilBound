# Story landmarks (the lair set-pieces) and the per-act story packs (docs/sigilbound/art/STORY_SCENES.md).
# Landmarks here plus the route pieces in story_pieces.py build and bake like the prop kit (make_props.py:
# material rows, baked shade, GLOW faces in submesh 1). Run:  python make_landmarks.py
# It reads unity/Assets/Sigilbound/Core/StoryScenes.cs, collects the meshes each act uses that the base
# kit does not have, and writes Resources/Scenery/story_act1.bytes and story_act2.bytes (same format as
# props.bytes), so a chapter only loads its act's pack.
# Moving parts that the boss stage animates (water wheel, bell, lamp, flames) are separate meshes
# with their pivot at the origin, named "<landmark>_<part>"; LANDMARKS lists where each one sits.
import math, random, os, re, struct
import make_props as mp  # imports bpy first (bmesh needs it)
import bmesh
from mathutils import Vector, Matrix, noise
from make_props import (lathe, add_bm, ico, box, two_sided, blade, canopy_clump, skin_tree, chiselled, displace, moss_tops,
                        T, S, Ry, Rx, Rz, BARK, LEAVES, LEAF_A, ROCK, MOSS, WOOD, STONE, CLOTH, METAL, CAP, STEM, REED, EARTH, TRIM, COAL, GLOW)
import story_pieces as SP
from story_pieces import put, roof, cone_roof, wheel, flames, shard


# ------------------------------------------------------------------ act 1
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


def stormcrown():
    """Stormcrown Heights: a crag whose tips ring like a crown, iron lightning rods at the points."""
    rng = random.Random(7); bm = bmesh.new()
    base = chiselled(3.2, 70, planes=12, soften=0.2)
    for v in base.verts: v.co = Vector((v.co.x * 1.3, max(v.co.y, -0.4) * 0.55 + 0.2, v.co.z * 0.9))
    put(bm, base, row=ROCK)
    for k in range(7):  # the crown points
        a = k / 7 * math.tau
        h = rng.uniform(3.5, 5.0) * (1.25 if k % 2 == 0 else 0.85)
        sp = chiselled(0.7, 80 + k, planes=8, soften=0.05, subdiv=1)
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
        sl = chiselled(1.1, 90 + k, planes=9, soften=0.1, subdiv=1)
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
        st = chiselled(0.45, 120 + k, planes=8, soften=0.08, subdiv=1)
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


# ------------------------------------------------------------------ route stops (the two mock strips)
LANDMARKS = {
    "drowned_mill": drowned_mill, "drowned_mill_wheel": drowned_mill_wheel, "bellspire": bellspire, "bellspire_bell": bellspire_bell,
    "stormcrown": stormcrown, "hollow_deep": hollow_deep, "last_pyre": last_pyre, "lighthouse": lighthouse, "lighthouse_lamp": lighthouse_lamp,
    "drowned_shrine": drowned_shrine, "kings_barrow": kings_barrow, "great_tree": great_tree,
}
ALL = dict(LANDMARKS); ALL.update(SP.PIECES)


def bake_one(name):
    bm = ALL[name]()
    verts, subs = mp.bake(bm, sum(map(ord, name)) % 997)
    bm.free()
    subs = [s for s in subs if s] if subs[1] else [subs[0]]
    return verts, subs


def build_all(names=None):
    return {n: bake_one(n) for n in (names or ALL)}


ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..")


def act_meshes():
    """Mesh names each act's stops use (from Core/StoryScenes.cs), minus the base kit's."""
    src = open(os.path.join(ROOT, "Assets", "Sigilbound", "Core", "StoryScenes.cs")).read()
    kit = {n for n, _ in mp.PROPS}
    out = {}
    for act in (1, 2):
        body = src[src.index("// ACT%d-BEGIN" % act):src.index("// ACT%d-END" % act)]
        out[act] = sorted({n for n in re.findall(r'P\("([a-z0-9_]+)"', body) if n not in kit})
    return out


def write_pack(path, meshes):
    data = bytearray(b"SBP1") + struct.pack("<i", len(meshes))
    for name, (verts, subs) in meshes.items():
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
    with open(path, "wb") as f: f.write(data)
    return len(data)


if __name__ == "__main__":
    out_dir = os.path.join(ROOT, "Assets", "Resources", "Scenery")
    total = 0
    for act, names in act_meshes().items():
        missing = [n for n in names if n not in ALL]
        assert not missing, f"act {act}: no builder for {missing}"
        meshes = build_all(names)
        n = write_pack(os.path.join(out_dir, f"story_act{act}.bytes"), meshes)
        total += n
        for name, (v, s) in meshes.items(): print(f"  act{act} {name:20s} tris {sum(map(len, s)) // 3:5d}")
        print(f"story_act{act}.bytes: {len(meshes)} meshes, {sum(sum(map(len, s)) // 3 for v, s in meshes.values())} tris, {n / 1024:.1f} KB")
    print(f"story packs total {total / 1024:.1f} KB")
