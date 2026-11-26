"""Modelled accessories for the high-detail heroes, from the design's outfit.acc / kit strings that the
builder used to drop: belts and sashes, bandoliers and harnesses, scarves, necklaces and prayer beads,
collars, pauldrons, bracers and bangles, gloves and wrist ribbons, earrings, pouches and things hung at
the belt, things carried on the back (kegs, quivers, scroll cases, sacks, packs, baskets, hives, horns,
anvils, shrines, rope coils), aprons and tabards. Capes, cloaks, mantles, shawls, wings and robes are
kit_cloth.py's; held items are kit_props.py's; auras are effects.

    accessories(c, arm, mats, U) -> [objects]     U is the ubc module
"""
import bpy, math, re
from mathutils import Vector, Matrix
import kit_geo as G
from kit_geo import TAU, FRONT
from kit_head import Ctx, feather, lantern, star_outline, circle_outline, REPORT, has

SKIP = ("cape", "cloak", "mantle", "shawl", "wing", "robe", "aura", "breaker", "twin", "wisp", "train", "glider", "coat", "waistcoat",
        "greatcoat", "waders", "capelet", "ears", "tail", "head", "nose", "carapace", "loom", "ring of water", "lily pads", "arc")


# things in kit lists that are weapons, effects, body or hair details, or clothing other tracks own
NOT_ACC = ("club", "mace", "sceptre", "lance", "trident", "saw", "shears", "rake", "pole", "cane", "billhook", "shovel", "cutlass", "rapier",
           "longsword", "greatsword", "sword", "crossbow", "longbow", "bow", "wand", "fan", "cards", "blowpipe", "boomerang", "shuttle", "drumstick",
           "panpipe", "marionette", "orrery", "puppet", "fist", "gauntlet", "tankard", "key", "crank", "standard", "hoop", "kettle", "buckler",
           "anchor", "throwing", "whip", "drum", "barrel", "decal", "particle", "seam", "crack", "trail", "smudge", "splatter", "crust", "drip",
           "feet", "beard", "mohawk", "ponytail", "button", "boot", "skate", "mitten", "jerkin", "armour", "armor", "arm", "lamp", "hem",
           "tricorne", "bicorne", "cowl", "antenna", "antennae", "stamen", "streamer", "falcon", "eclipse", "disc", "scale", "admiral", "glassblower",
           "towel", "hose", "rose blossom", "blossom", "chest", "veins", "furnace")


class Body:
    """Rest-pose body landmarks and torso fitting."""
    def __init__(self, X):
        self.X = X; U = X.U; a = X.arm
        self.pel, _ = U.rest_bone(a, "pelvis")
        self.s1, _ = U.rest_bone(a, "spine_01"); self.s2, _ = U.rest_bone(a, "spine_02"); self.s3, _ = U.rest_bone(a, "spine_03")
        self.neck, self.neck_t = U.rest_bone(a, "neck_01")
        self.fit = X.fit
        self.waist_z = self.pel.z + 0.09

    def ring(self, z, n=32, tilt=0.0):
        return self.fit.ring(self.pel.x, self.s2.y, z, n, fallback=0.15, reach=0.5, zfun=(lambda a: tilt * math.sin(a)) if tilt else None)

    def back_y(self, z):
        hit = self.fit.surface(Vector((self.s3.x, self.s3.y + 0.6, z)), (0, -1, 0))
        return hit.y if hit else self.s3.y + 0.12

    def front_y(self, z):
        hit = self.fit.surface(Vector((self.s3.x, self.s3.y - 0.6, z)), (0, 1, 0))
        return hit.y if hit else self.s3.y - 0.12


def strap(B, mat, side=1, width=0.05, thick=0.006, round_=False, z_hip=None):
    """A diagonal band over one shoulder (side +1 = hero's left) to the opposite hip, lying on the torso."""
    c = Vector((B.pel.x, B.s3.y, (B.s3.z + B.pel.z) / 2 + 0.06))
    th = math.radians(33)
    u = Vector((math.sin(th) * side, 0, math.cos(th))); v = Vector((0, 1, 0)); n = u.cross(v).normalized()
    pts = []
    for i in range(41):
        a = i / 40 * TAU
        d = (u * math.cos(a) + v * math.sin(a)).normalized()
        hit = B.fit.bvh.ray_cast(c + d * 0.7, -d, 0.7)[0]
        pts.append((hit if hit is not None else c + d * 0.15) + d * (thick + 0.004))
    sm = [pts[0]] + [(pts[i - 1] + pts[i] * 2 + pts[i + 1]) / 4 for i in range(1, len(pts) - 1)] + [pts[-1]]
    sm[-1] = sm[0]
    if round_: return G.sweep(sm, width / 2, mat, segs=8, cap=False), sm, n
    return G.sweep(sm, (width / 2, thick / 2), mat, segs=6, side=n, cap=False), sm, n


def belt(X, B, text):
    leather = X.mat(G.colour_in(text, "#4a3220"), "leather")
    if has(text, "rope", "reed"): leather = X.mat(G.colour_in(text, "#b9a463"), "leather")
    if has(text, "chain", "silver chain"): leather = X.metal_of(text, "#c9ced6")
    z = B.waist_z; radii = B.ring(z + 0.02)
    parts = []
    if has(text, "rope", "reed"):
        ring = []
        for i in range(65):
            a = i / 64 * TAU; p, d = G.ring_point(radii, B.pel.x, B.s2.y, a, 0.012); ring.append(Vector((p.x, p.y, z + 0.02)))
        parts.append(G.sweep(ring, 0.011, leather, segs=8, cap=False, twist=0.0))
        ring2 = [p + Vector((0, 0, 0.012 * math.sin(i * 0.9))) for i, p in enumerate(ring)]
        parts.append(G.sweep(ring2, 0.008, leather, segs=6, cap=False))
        p, d = G.ring_point(radii, B.pel.x, B.s2.y, FRONT + 0.5, 0.02)
        k = Vector((p.x, p.y, z + 0.02)); parts.append(G.sphere(k, 0.02, leather, segs=10))
        for sx in (-0.01, 0.012):
            parts.append(G.sweep([k, k + Vector((sx, -0.01, -0.12))], 0.008, leather, segs=6))
    elif has(text, "chain"):
        for i in range(48):
            a = i / 48 * TAU; p, d = G.ring_point(radii, B.pel.x, B.s2.y, a, 0.01)
            parts.append(G.lathe([(0.006, -0.003), (0.008, 0), (0.006, 0.003), (0.004, 0)], leather, center=Vector((p.x, p.y, z + 0.015)), segs=8, M=G.frame((-math.sin(a), math.cos(a), 0)) if i % 2 else G.frame(d)))
    else:
        h = 0.07 if has(text, "championship") else 0.045
        parts.append(G.band(radii, leather, B.pel.x, B.s2.y, z, h, 0.008, off=0.006))
        p, d = G.ring_point(radii, B.pel.x, B.s2.y, FRONT, 0.016)
        if has(text, "championship"):
            ol = [(0.09 * math.cos(t), 0.06 * math.sin(t)) for t in [i / 20 * TAU for i in range(20)]]
            parts.append(G.extrude(ol, 0.01, X.metal_of("gold"), Vector((p.x, p.y - 0.004, z + h / 2)), Vector((1, 0, 0)), Vector((0, 0, 1)), bevel=0.003))
            parts.append(G.extrude(star_outline(0.03, 0.013), 0.012, X.mat("#b0302a", "metal"), Vector((p.x, p.y - 0.008, z + h / 2)), Vector((1, 0, 0)), Vector((0, 0, 1))))
        else:
            bm = X.metal_of("brass")
            ring = [Vector((p.x - 0.024, p.y - 0.002, z + 0.004)), Vector((p.x + 0.024, p.y - 0.002, z + 0.004)), Vector((p.x + 0.024, p.y - 0.002, z + h - 0.004)), Vector((p.x - 0.024, p.y - 0.002, z + h - 0.004)), Vector((p.x - 0.024, p.y - 0.002, z + 0.004))]
            parts.append(G.sweep(ring, 0.004, bm, segs=6, cap=False))
            parts.append(G.box(Vector((p.x, p.y - 0.004, z + h / 2)), (0.004, 0.004, h * 0.7), bm, bevel=0.001))
    hang = []
    if has(text, "gourd"): hang += [("gourd", FRONT + 1.2), ("gourd", FRONT + 1.6)]
    if has(text, "trap"): hang += [("trap", FRONT + k * 0.55 - 1.1) for k in range(5)]
    if has(text, "bead", "prayer"): hang += [("beads", FRONT + 0.9)]
    for kind, a in hang:
        p, d = G.ring_point(radii, B.pel.x, B.s2.y, a, 0.03); top = Vector((p.x, p.y, z))
        if kind == "gourd":
            parts.append(G.lathe([(0, 0), (0.025, -0.01), (0.03, -0.04), (0.018, -0.06), (0.024, -0.08), (0.018, -0.1), (0, -0.105)], X.mat("#c09a4a", "leather"), center=top, segs=12))
        elif kind == "trap":
            parts.append(G.lathe([(0, 0), (0.022, 0), (0.022, 0.006), (0, 0.006)], X.metal_of("iron"), center=top - Vector((0, 0, 0.03)), segs=10, M=G.frame(d)))
            for s in (-1, 1): parts.append(G.sweep([top - Vector((0.018, 0, 0.03)) + d * 0.004 * s, top - Vector((-0.018, 0, 0.03)) + d * 0.004 * s], 0.0025, X.metal_of("iron"), segs=5))
        else:
            for k in range(8): parts.append(G.sphere(top - Vector((0, 0, 0.012 * k)), 0.006, X.mat("#a8302a", "glass"), segs=6))
    return X.add(G.join(parts), "pelvis", tag="Kit_Acc", piece="belt" + (":rope" if has(text, "rope", "reed") else ":championship" if has(text, "championship") else ":chain" if has(text, "chain") else ""))


def sash(X, B, text, side=1):
    m = X.cloth_of(text, X.pal.get("accent"))
    rope = has(text, "rope", "climbing", "harness")
    if has(text, "rope", "climbing", "harness"): m = X.mat(G.colour_in(text, "#b9a463"), "leather")
    if has(text, "bandolier"): m = X.mat("#4a3220", "leather")
    o, path, n = strap(B, m, side=side, width=0.024 if rope else 0.06, round_=rope)
    parts = [o]
    if has(text, "bandolier", "vial", "pouch", "coal"):
        for k in range(5):
            p = path[3 + k * 3]
            out = (p - Vector((B.pel.x, B.s3.y, p.z))); out.z = 0; out = out.normalized() if out.length > 1e-4 else Vector((0, -1, 0))
            if has(text, "vial", "poison"):
                parts.append(G.lathe([(0, 0), (0.012, 0), (0.012, 0.03), (0.005, 0.038), (0.005, 0.046), (0, 0.046)], X.mat("#6ad86a", "glass"), center=p + out * 0.012 - Vector((0, 0, 0.02)), segs=10))
            else:
                parts.append(G.box(p + out * 0.016, (0.04, 0.026, 0.036), X.mat("#3a2a1a", "leather"), bevel=0.006, M=G.frame_xz(out.cross(Vector((0, 0, 1))), Vector((0, 0, 1)))))
                if has(text, "smoking", "coal"): parts.append(G.sphere(p + out * 0.03 + Vector((0, 0, 0.02)), 0.008, X.glow, segs=6))
    if has(text, "scroll", "oath"):
        for k in range(4):
            p = path[24 + k * 3]
            parts.append(G.lathe([(0, 0), (0.008, 0), (0.008, 0.06), (0, 0.06)], X.mat("#e8dcb8"), center=p + Vector((0, -0.012, -0.06)), segs=8))
            parts.append(G.sphere(p + Vector((0, -0.02, -0.03)), 0.007, X.mat("#a8201a", "glass"), segs=6, scale=(1, 0.4, 1)))
    if has(text, "brass stud", "stud"):
        for k in range(0, 40, 3): parts.append(G.sphere(path[k], 0.004, X.metal_of("brass"), segs=6))
    bones = {"spine_03": 0.6, "spine_02": 0.3, "pelvis": 0.1}
    piece = "bandolier" if has(text, "bandolier") else "harness" if rope else "sash"
    if has(text, "cross", "harness") and not has(text, "cross-chest"):
        o2, _, _ = strap(B, m, side=-side, width=0.024 if rope else 0.06, round_=rope); parts.append(o2)
    return X.add(G.join(parts), bones, tag="Kit_Acc", piece=piece)


def scarf(X, B, text):
    m = X.cloth_of(text, X.pal.get("secondary"))
    if has(text, "frost"): m = X.mat("#cfeaff")
    z = B.neck.z + 0.01
    radii = X.fit.ring(B.neck.x, B.neck.y, z + 0.02, 32, fallback=0.06, reach=0.3)
    parts = [G.band(radii, m, B.neck.x, B.neck.y, z - 0.01, 0.05, 0.016, off=0.004, flare=0.006)]
    fy = B.front_y(z - 0.06)
    if has(text, "kerchief"):
        p = Vector((B.neck.x, fy - 0.01, z - 0.02))
        parts.append(G.extrude([(-0.06, 0), (0.06, 0), (0, -0.08)], 0.004, m, p, Vector((1, 0, 0)), Vector((0, 0.15, 1))))
    else:
        L = 0.4 if has(text, "long") else 0.24
        start = Vector((B.neck.x + 0.035, fy - 0.01, z))
        pts = [start + Vector((0.01 * math.sin(i), -0.01 - 0.015 * (i / 10), -L * i / 10)) for i in range(11)]
        parts.append(G.sweep(pts, [(0.03, 0.005)] * 10 + [(0.032, 0.005)], m, segs=6, side=Vector((1, 0, 0))))
        if has(text, "flowing", "long"):
            by = B.back_y(z - 0.03)
            st2 = Vector((B.neck.x - 0.03, by + 0.01, z))
            pts2 = G.bezier(st2, st2 + Vector((-0.03, 0.08, -0.08)), st2 + Vector((-0.06, 0.2, -0.12)), n=10)
            parts.append(G.sweep(pts2, [(0.03, 0.005)] * 11, m, segs=6, side=Vector((1, 0, 0))))
    return X.add(G.join(parts), {"neck_01": 0.7, "spine_03": 0.3}, tag="Kit_Acc", piece="kerchief" if has(text, "kerchief") else "scarf")


def necklace(X, B, text):
    m = X.mat("#7aa8a0", "glass") if has(text, "sea-glass") else X.mat("#a8302a", "glass") if has(text, "red") else X.metal_of(text, "#d8b04a") if has(text, "chain", "gold", "silver") else X.mat("#6a4a2e", "horn")
    z = B.neck.z - 0.02
    radii = X.fit.ring(B.neck.x, B.neck.y, z, 32, fallback=0.07, reach=0.35)
    fy = B.front_y(B.neck.z - 0.12)
    parts = []
    n = 36
    for i in range(n):
        a = FRONT + math.pi + i / n * TAU
        p, d = G.ring_point(radii, B.neck.x, B.neck.y, a, 0.012)
        sag = max(0.0, -math.sin(a)) ** 2
        q = Vector((p.x, p.y, z)) + Vector((0, 0, -0.1 * sag))
        if sag > 0.2:
            hit = X.fit.surface(q + Vector((0, -0.4, 0)), (0, 1, 0))
            if hit is not None and hit.y < q.y + 0.02: q.y = hit.y - 0.008
        parts.append(G.sphere(q, 0.0065 if not has(text, "chain") else 0.004, m, segs=6))
    pend = Vector((B.neck.x, fy - 0.014, z - 0.105))
    if has(text, "charm", "bone"): parts.append(G.sphere(pend, 0.012, X.mat("#e0d4b6", "horn"), segs=8, scale=(0.7, 0.5, 1.4)))
    elif has(text, "prayer", "bead"): parts.append(G.extrude([(-0.004, 0.012), (0.004, 0.012), (0.004, 0.004), (0.012, 0.004), (0.012, -0.004), (0.004, -0.004), (0.004, -0.02), (-0.004, -0.02), (-0.004, -0.004), (-0.012, -0.004), (-0.012, 0.004), (-0.004, 0.004)], 0.004, X.metal_of("gold"), pend, Vector((1, 0, 0)), Vector((0, 0, 1))))
    else: parts.append(G.sphere(pend, 0.011, X.glow if has(text, "sea-glass") else m, segs=8))
    return X.add(G.join(parts), {"spine_03": 0.7, "neck_01": 0.3}, tag="Kit_Acc", piece="necklace/beads")


def collar(X, B, text):
    m = X.mat(G.colour_in(text, X.pal.get("main")), "cloth")
    if has(text, "fin"): m = X.mat(G.colour_in(text, "#2a8a86"), "horn")
    big = has(text, "throne", "high")
    z = B.neck.z - 0.04
    radii = X.fit.ring(B.neck.x, B.neck.y, z + 0.02, 32, fallback=0.07, reach=0.35)
    h = 0.15 if big else 0.09
    hf = lambda a: 0.35 + 0.65 * max(0.0, math.sin(a)) ** 0.7     # tall at the back, low at the front
    o = G.band(radii, m, B.neck.x, B.neck.y, z, h, 0.006, off=0.01, flare=0.05 if big else 0.03, hfun=hf)
    parts = [o]
    if has(text, "fin"):
        for k in range(5):
            a = math.pi / 2 + (k - 2) * 0.35; p, d = G.ring_point(radii, B.neck.x, B.neck.y, a, 0.02)
            parts.append(G.sweep([Vector((p.x, p.y, z)), Vector((p.x, p.y, z)) + d * 0.04 + Vector((0, 0, h * 0.95))], 0.004, X.mat("#1a5a5a", "horn"), segs=5))
    return X.add(G.join(parts), {"spine_03": 0.6, "neck_01": 0.4}, tag="Kit_Acc", piece="collar")


def pauldron(X, B, text, sides=(1, -1)):
    mt = X.mat(G.colour_in(text, X.pal.get("metal") or "#9aa1aa"), "metal")
    if has(text, "leather"): mt = X.mat(G.colour_in(text, "#6a4528"), "leather")
    if has(text, "coral"): mt = X.mat("#e2725e", "horn")
    if has(text, "molten"): mt = X.mat("#2a2422", "stone")
    if has(text, "feather"): mt = X.mat(G.colour_in(text, "#6a7a5a"))
    piece = "pauldron"
    for sx in sides:
        side = "l" if sx > 0 else "r"
        ua, ut = X.U.rest_bone(X.arm, "upperarm_" + side)
        top_hit = X.fit.surface(Vector((ua.x + sx * 0.02, ua.y, ua.z + 0.5)), (0, 0, -1))
        tz = top_hit.z if top_hit is not None else ua.z + 0.06
        c = Vector((ua.x + sx * 0.03, ua.y, tz - 0.035))
        axis = Vector((sx * 0.55, 0, 0.85)).normalized()
        M = G.frame_xz(Vector((0, 1, 0)).cross(axis) * sx, axis)
        M = G.frame(axis)
        parts = []
        if has(text, "feather"):
            for k in range(9):
                a = (k - 4) * 0.28
                d = (Vector((sx * 0.9, math.sin(a) * 0.7, -0.15))).normalized()
                parts.append(feather(c + Vector((0, math.sin(a) * 0.05, 0.03)), d, 0.13, 0.03, mt, side=Vector((0, math.cos(a), 0.3)), curl=-0.15))
            piece = "pauldron:feather"
        elif has(text, "bell-tower", "crenellated", "tower"):
            parts.append(G.lathe([(0, 0.06), (0.07, 0.06), (0.075, 0.0), (0.06, -0.02), (0, -0.02)], X.mat("#8a8478", "stone"), center=c, segs=16, M=M))
            for k in range(8):
                a = k / 8 * TAU
                parts.append(G.box(c + M @ Vector((math.cos(a) * 0.066, math.sin(a) * 0.066, 0.074)), (0.022, 0.016, 0.026), X.mat("#8a8478", "stone"), bevel=0.003, M=M))
            piece = "pauldron:tower"
        elif has(text, "bramble"):
            pts = [c + M @ Vector((math.cos(t * 2.2 * TAU) * 0.075, math.sin(t * 2.2 * TAU) * 0.075, 0.03 - t * 0.07)) for t in [i / 40 for i in range(41)]]
            parts.append(G.sweep(pts, 0.009, X.mat("#3a2a1a", "horn"), segs=6))
            for k in range(0, 40, 3):
                d = (pts[k] - c).normalized(); parts.append(G.sweep([pts[k], pts[k] + d * 0.02], [0.004, 0.0003], X.mat("#3a2a1a", "horn"), segs=5))
            piece = "pauldron:bramble"
        else:
            for k, (r, h, dz) in enumerate(((0.1, 0.05, 0.0), (0.095, 0.035, -0.035), (0.088, 0.03, -0.062))):
                prof = [(0, h), (r * 0.6, h * 0.85), (r * 0.92, h * 0.4), (r, 0.0), (r * 1.02, -0.008), (r * 0.94, -0.006)]
                parts.append(G.lathe(prof, mt, center=c + Vector((sx * (0.012 * k), 0, dz)), rx=1.0, ry=1.12, segs=24, M=M))
            if has(text, "spike", "coral"):
                for k in range(5):
                    a = k / 5 * TAU; base = c + M @ Vector((math.cos(a) * 0.05, math.sin(a) * 0.05, 0.035))
                    parts.append(G.lathe([(0, 0), (0.012, 0), (0, 0.05 if has(text, "coral") else 0.04)], mt, center=base, segs=8, M=G.frame((axis + (base - c).normalized() * 0.4).normalized())))
            if has(text, "molten"):
                for k in range(4):
                    a = k / 4 * TAU + 0.4
                    pts = [c + M @ Vector((math.cos(a + 0.2 * t) * 0.03 * (1 + 2 * t), math.sin(a + 0.2 * t) * 0.03 * (1 + 2 * t), 0.052 - 0.045 * t * t)) for t in [i / 6 for i in range(7)]]
                    parts.append(G.sweep(pts, 0.004, X.glow, segs=5))
                piece = "pauldron:molten"
            if has(text, "lion"):
                parts.append(G.sphere(c + M @ Vector((0, 0, 0.055)), 0.03, X.metal_of("gold"), segs=10, scale=(1, 1, 0.6)))
            if has(text, "round", "gold"): piece = "pauldron:round"
        X.add(G.join(parts), {"upperarm_" + side: 0.65, "spine_03": 0.35}, tag="Kit_Acc", piece=piece)


def bracers(X, B, text, sides=(1, -1)):
    m = X.metal_of(text, "#d8b04a") if has(text, "gold", "bangle") else X.mat(G.colour_in(text, "#5a3a22"), "leather")
    piece = "bangles" if has(text, "bangle") else "arm wraps" if has(text, "wrap") else "bracers"
    for sx in sides:
        side = "l" if sx > 0 else "r"
        la, lt = X.U.rest_bone(X.arm, "lowerarm_" + side); hl, _ = X.U.rest_bone(X.arm, "hand_" + side)
        ax = (hl - la).normalized(); M = G.frame(ax)
        def r_at(p):
            rs = []
            for k in range(8):
                a = k / 8 * TAU; d = M @ Vector((math.cos(a), math.sin(a), 0))
                hit = X.fit.bvh.ray_cast(p + d * 0.2, -d, 0.2)[0]
                if hit is not None: rs.append((hit - p).length)
            return (sum(rs) / len(rs)) if rs else 0.04
        parts = []
        if has(text, "bangle"):
            for k in range(3):
                p = la + (hl - la) * (0.78 + 0.06 * k); r = r_at(p) + 0.006
                ring = [p + M @ Vector((math.cos(j / 20 * TAU) * r, math.sin(j / 20 * TAU) * r, 0)) for j in range(21)]
                parts.append(G.sweep(ring, 0.004, m, segs=6, cap=False))
        else:
            p0 = la + (hl - la) * 0.45; p1 = la + (hl - la) * 0.92
            r0, r1 = r_at(p0) + 0.006, r_at(p1) + 0.006
            L = (p1 - p0).length
            prof = [(r0, 0), (r0 + 0.006, 0.004), (r0 * 0.6 + r1 * 0.4 + 0.006, L * 0.5), (r1 + 0.006, L - 0.004), (r1, L)]
            parts.append(G.lathe(prof, m, center=p0, segs=18, M=M))
            if has(text, "wrap"):
                for k in range(6):
                    t = k / 6; p = p0 + (p1 - p0) * t; r = r0 * (1 - t) + r1 * t + 0.009
                    ring = [p + M @ Vector((math.cos(j / 16 * TAU) * r, math.sin(j / 16 * TAU) * r, 0.01 * j / 16)) for j in range(17)]
                    parts.append(G.sweep(ring, 0.003, m, segs=5, cap=False))
            if has(text, "falconry", "glove"):
                parts.append(G.lathe([(r1 + 0.004, 0), (r1 + 0.012, L * 0.4)], m, center=p1 - ax * L * 0.4, segs=18, M=M))
                parts.append(G.sphere(hl + ax * 0.05, 0.045, m, segs=12, scale=(1.4, 0.9, 0.7), M=G.frame_xz(ax, (0, 0, 1))))
                piece = "falconry glove"
        if has(text, "ribbon"):
            mr = X.cloth_of(text, "#b0302a"); w = la + (hl - la) * 0.9
            pts = G.bezier(w, w + Vector((sx * 0.05, 0.08, -0.05)), w + Vector((sx * 0.1, 0.2, -0.08)), n=8)
            parts.append(G.sweep(pts, [(0.012, 0.002)] * 9, mr, segs=5, side=Vector((0, 0, 1))))
            piece = "wrist ribbons"
        X.add(G.join(parts), "lowerarm_" + side, tag="Kit_Acc", piece=piece)
        if has(text, "left", "falconry"): break


def anklets(X, B, text):
    parts = []
    for side in ("l", "r"):
        ca, ct = X.U.rest_bone(X.arm, "calf_" + side)
        p = ca + (ct - ca) * 0.9; ax = (ct - ca).normalized(); M = G.frame(ax)
        rs = []
        for k in range(8):
            a = k / 8 * TAU; d = M @ Vector((math.cos(a), math.sin(a), 0))
            hit = X.fit.bvh.ray_cast(p + d * 0.2, -d, 0.2)[0]
            if hit is not None: rs.append((hit - p).length)
        r = (sum(rs) / len(rs) if rs else 0.045) + 0.01
        ring = [p + M @ Vector((math.cos(j / 24 * TAU) * r, math.sin(j / 24 * TAU) * r, 0)) for j in range(25)]
        o = [G.sweep(ring, 0.0025, X.mat("#6a4a2e", "leather"), segs=5, cap=False)]
        for k in range(7):
            a = k / 7 * TAU; q = p + M @ Vector((math.cos(a) * (r + 0.004), math.sin(a) * (r + 0.004), -0.01))
            o.append(G.sphere(q, 0.009, X.mat("#efe2cc", "horn"), segs=8, scale=(1, 0.5, 1.2), M=G.frame(M @ Vector((math.cos(a), math.sin(a), 0)))))
        X.add(G.join(o), "calf_" + side, tag="Kit_Acc", piece="shell anklets")


def earring(X, B, text):
    m = X.metal_of(text, "#d8b04a"); parts = []
    for sx in (1,):
        z = X.eye - 0.045; r = X.fit.radius(X.cx, X.cy, z, 0 if sx > 0 else math.pi, X.hw, 0.45)
        p = Vector((X.cx + sx * (r + 0.004), X.cy + 0.008, z - 0.012))
        ring = [p + Vector((0, math.cos(j / 20 * TAU) * 0.014, math.sin(j / 20 * TAU) * 0.014)) for j in range(21)]
        parts.append(G.sweep(ring, 0.0022, m, segs=6, cap=False))
    return X.add(G.join(parts), "Head", tag="Kit_Acc", piece="earring")


def hip_item(X, B, text, slot):
    """Something hung at the belt: pouch, satchel, vial, holster, smoker, padlock, hook, spool, watch chain,
    lantern, rope coil. slot: 0, 1, 2... places them round the hips."""
    z = B.waist_z - 0.02
    radii = B.ring(z + 0.02)
    a = FRONT + (0.9, -0.9, 1.5, -1.5, 2.2)[slot % 5]
    p, d = G.ring_point(radii, B.pel.x, B.s2.y, a, 0.02)
    P0 = Vector((p.x, p.y, z)) + d * 0.015
    t = Vector((-math.sin(a), math.cos(a), 0))
    M = G.frame_xz(t, (0, 0, 1))
    lm = X.mat(G.colour_in(text, "#5a3a22"), "leather"); parts = []; piece = "pouch"
    if has(text, "satchel", "loaf"):
        if has(text, "loaf"): parts.append(G.sphere(P0 + d * 0.04 - Vector((0, 0, 0.06)), 0.07, X.mat("#c08a4a", "leather"), segs=14, scale=(1.2, 0.7, 0.8), M=M)); piece = "loaf satchel"
        else: parts.append(G.box(P0 + d * 0.04 - Vector((0, 0, 0.07)), (0.15, 0.06, 0.11), lm, bevel=0.012, M=M)); piece = "satchel"
        o, _, _ = strap(B, lm, side=1 if a > FRONT else -1, width=0.03); parts.append(o)
    elif has(text, "vial", "dart"):
        parts.append(G.box(P0 + d * 0.02 - Vector((0, 0, 0.04)), (0.07, 0.035, 0.06), lm, bevel=0.008, M=M))
        if has(text, "dart"):
            for k in range(4): parts.append(G.sweep([P0 + t * (k - 1.5) * 0.012 + d * 0.02 - Vector((0, 0, 0.01)), P0 + t * (k - 1.5) * 0.012 + d * 0.02 + Vector((0, 0, 0.04))], 0.002, X.metal_of("iron"), segs=4))
        piece = "dart pouch" if has(text, "dart") else "vial pouch"
    elif has(text, "holster", "shears"):
        parts.append(G.box(P0 + d * 0.02 - Vector((0, 0, 0.07)), (0.05, 0.03, 0.13), lm, bevel=0.008, M=M))
        for s in (-1, 1): parts.append(G.lathe([(0.012, 0), (0.016, 0.004), (0.012, 0.008), (0.008, 0.004)], X.metal_of("iron"), center=P0 + d * 0.02 + t * 0.012 * s + Vector((0, 0, 0.0)), segs=10, M=G.frame(d)))
        piece = "holster"
    elif has(text, "smoker"):
        parts.append(G.lathe([(0, 0), (0.035, 0), (0.035, 0.1), (0.02, 0.13), (0.008, 0.15), (0, 0.15)], X.metal_of("brass"), center=P0 + d * 0.04 - Vector((0, 0, 0.12)), segs=14))
        parts.append(G.box(P0 + d * 0.09 - Vector((0, 0, 0.07)), (0.04, 0.05, 0.08), lm, bevel=0.006, M=M))
        piece = "bee smoker"
    elif has(text, "padlock", "lock"):
        parts.append(G.box(P0 + d * 0.05 - Vector((0, 0, 0.1)), (0.1, 0.04, 0.09), X.metal_of("iron"), bevel=0.01, M=M))
        arc = [P0 + d * 0.05 - Vector((0, 0, 0.055)) + t * math.cos(u) * 0.034 + Vector((0, 0, math.sin(u) * 0.05)) for u in [math.pi * i / 12 for i in range(13)]]
        parts.append(G.sweep(arc, 0.008, X.metal_of("steel"), segs=8))
        piece = "padlock"
    elif has(text, "hook"):
        pts = [P0 + d * 0.02, P0 + d * 0.025 - Vector((0, 0, 0.08))] + [P0 + d * 0.025 - Vector((0, 0, 0.08)) + t * (0.03 - 0.03 * math.cos(u)) - Vector((0, 0, 0.03 * math.sin(u))) for u in [math.pi * i / 8 for i in range(1, 9)]]
        parts.append(G.sweep(pts, G.taper(len(pts), 0.008, 0.003), X.metal_of("iron"), segs=8))
        piece = "cargo hook"
    elif has(text, "spool"):
        parts.append(G.lathe([(0, 0), (0.03, 0), (0.03, 0.006), (0.02, 0.008), (0.02, 0.05), (0.03, 0.052), (0.03, 0.058), (0, 0.058)], X.mat("#8a5a2a", "leather"), center=P0 + d * 0.03 - Vector((0, 0, 0.03)), segs=14, M=G.frame(d)))
        piece = "kite spool"
    elif has(text, "watch"):
        ch = [P0 + Vector((0, -0.008, 0.03)) + t * (0.06 * i / 10) + Vector((0, 0, -0.03 * math.sin(math.pi * i / 10))) for i in range(11)]
        for q in ch: parts.append(G.sphere(q, 0.003, X.metal_of("gold"), segs=5))
        parts.append(G.lathe([(0, 0), (0.016, 0), (0.016, 0.006), (0, 0.006)], X.metal_of("gold"), center=ch[-1] + d * 0.01, segs=14, M=G.frame(d)))
        piece = "watch chain"
    elif has(text, "lantern"):
        parts.append(lantern(X, P0 + d * 0.05 - Vector((0, 0, 0.11)), 0.03)); piece = "belt lantern"
    elif has(text, "rope", "coil"):
        for k in range(4):
            c0 = P0 + d * 0.045 - Vector((0, 0, 0.05 + 0.004 * k))
            ring = [c0 + t * math.cos(j / 20 * TAU) * 0.06 + Vector((0, 0, math.sin(j / 20 * TAU) * 0.06)) + d * 0.006 * k for j in range(21)]
            parts.append(G.sweep(ring, 0.009, X.mat("#b9a463", "leather"), segs=6, cap=False))
        piece = "rope coil (hip)"
    else:
        parts.append(G.box(P0 + d * 0.025 - Vector((0, 0, 0.045)), (0.08, 0.04, 0.07), lm, bevel=0.01, M=M))
        parts.append(G.box(P0 + d * 0.047 - Vector((0, 0, 0.02)), (0.082, 0.006, 0.03), lm, bevel=0.004, M=M))
        if has(text, "herb"): parts.append(G.sweep([P0 + d * 0.03, P0 + d * 0.03 + Vector((0, 0, 0.05))], 0.01, X.mat("#4f8a3a"), segs=6))
        piece = "herb satchel" if has(text, "herb") else "pouch"
    return X.add(G.join(parts), {"pelvis": 0.7, ("thigh_l" if a > FRONT else "thigh_r"): 0.3}, tag="Kit_Acc", piece=piece)


def back_item(X, B, text):
    zc = B.s3.z - 0.05
    by = B.back_y(zc) + (0.08 if "golem" in X.species else 0.0)   # clear the golem's stone back slab
    C = Vector((B.s3.x, by + 0.01, zc))
    parts = []; piece = "back item"
    def tube(c, axis, r, L, m, caps=None):
        M = G.frame(axis); p0 = c - Vector(axis).normalized() * L / 2
        prof = [(0, 0), (r, 0), (r, L), (0, L)]
        o = [G.lathe(prof, m, center=p0, segs=16, M=M)]
        if caps:
            o.append(G.lathe([(0, -0.004), (r + 0.004, -0.004), (r + 0.004, 0.02), (0, 0.02)], caps, center=p0, segs=16, M=M))
            o.append(G.lathe([(0, L - 0.02), (r + 0.004, L - 0.02), (r + 0.004, L + 0.004), (0, L + 0.004)], caps, center=p0, segs=16, M=M))
        return o
    diag = Vector((0.45, 0, 1)).normalized()
    if has(text, "keg"):
        M = G.frame((1, 0, 0)); wood = X.mat("#8a5a2a", "leather")
        p0 = C + Vector((-0.11, 0.1, 0))
        parts.append(G.lathe([(0, 0), (0.08, 0), (0.095, 0.06), (0.098, 0.11), (0.095, 0.16), (0.08, 0.22), (0, 0.22)], wood, center=p0, segs=20, M=M))
        for t in (0.03, 0.19): parts.append(G.lathe([(0.087, t - 0.008), (0.092 if t < 0.1 else 0.09, t - 0.008), (0.092, t + 0.008), (0.087, t + 0.008)], X.metal_of("iron"), center=p0, segs=20, M=M))
        piece = "keg"
    elif has(text, "quiver", "javelin"):
        lm = X.mat(G.colour_in(text, "#5a3a22"), "leather") if not has(text, "white") else X.mat("#ece6da", "leather")
        qc = C + Vector((0, 0.06, -0.1))
        parts += tube(qc, diag, 0.048, 0.46, lm, caps=X.metal_of("brass"))
        top = qc + diag * 0.23
        n = 5 if has(text, "javelin") else 7
        for k in range(n):
            off = Vector(((k - n / 2) * 0.014, 0.008 * (k % 2), 0))
            if has(text, "javelin"):
                spread = (diag + Vector(((k - 2) * 0.12, 0, 0))).normalized()
                parts.append(G.sweep([top + off - spread * 0.1, top + off + spread * 0.32], 0.006, X.mat("#8a6a3a", "leather"), segs=6))
                parts.append(G.lathe([(0, 0), (0.014, 0.01), (0, 0.06)], X.metal_of("gold"), center=top + off + spread * 0.32, segs=6, M=G.frame(spread)))
            else:
                tip = top + off + diag * 0.08
                parts.append(G.sweep([top + off - diag * 0.05, tip], 0.003, X.mat("#7a5a3a", "leather"), segs=5))
                parts.append(G.extrude([(0, 0), (0.012, 0.02), (0.012, 0.05), (0, 0.04), (-0.012, 0.05), (-0.012, 0.02)], 0.002, X.glow if has(text, "light") else X.mat("#ece6da"), tip - diag * 0.05, Vector((1, 0, 0)), diag))
        piece = "quiver"
    elif has(text, "scroll case", "spyglass", "candle", "horn"):
        if has(text, "spyglass"):
            br = X.metal_of("brass")
            for k, (r, L, o_) in enumerate(((0.03, 0.2, -0.1), (0.025, 0.16, 0.06), (0.02, 0.12, 0.18))):
                parts += tube(C + Vector((0, 0.05, 0)) + diag * o_, diag, r, L, br)
            piece = "spyglass"
        elif has(text, "candle"):
            parts += tube(C + Vector((0, 0.07, 0.15)), (0, 0, 1), 0.04, 0.42, X.mat("#efe6cf"))
            tip = C + Vector((0, 0.07, 0.36))
            parts.append(G.lathe([(0, 0), (0.016, 0.02), (0.01, 0.05), (0, 0.075)], X.glow, center=tip, segs=10))
            for k in range(5):
                a = k / 5 * TAU; parts.append(G.sphere(tip + Vector((math.cos(a) * 0.04, math.sin(a) * 0.04, -0.03 - 0.02 * (k % 2))), 0.012, X.mat("#efe6cf"), segs=6, scale=(1, 1, 2)))
            parts += tube(C + Vector((0, 0.04, -0.02)), (1, 0, 0), 0.008, 0.22, X.mat("#5a3a22", "leather"))
            piece = "candle on back"
        elif has(text, "horn"):
            hm = X.mat("#d8c8a8", "horn")
            pts = G.bezier(C + Vector((-0.18, 0.06, -0.12)), C + Vector((-0.05, 0.12, 0.1)), C + Vector((0.12, 0.1, 0.2)), C + Vector((0.2, 0.06, 0.06)), n=16)
            parts.append(G.sweep(pts, G.taper(17, 0.015, 0.075, 2.0), hm, segs=14))
            parts.append(G.sweep([pts[2], pts[2] + Vector((0, 0, 0.0)), pts[12]], 0.006, X.mat("#5a3a22", "leather"), segs=5))
            piece = "war horn"
        else:
            parts += tube(C + Vector((0, 0.05, 0.05)), diag, 0.04, 0.5, X.mat(G.colour_in(text, "#6a4528"), "leather"), caps=X.metal_of("brass"))
            piece = "scroll case"
    elif has(text, "anvil"):
        im = X.metal_of("iron")
        ol = [(-0.16, 0.0), (-0.1, 0.02), (-0.06, 0.02), (-0.04, -0.06), (-0.07, -0.1), (0.07, -0.1), (0.04, -0.06), (0.06, 0.02), (0.12, 0.02), (0.12, 0.06), (-0.06, 0.06), (-0.12, 0.05)]
        parts.append(G.extrude(ol, 0.09, im, C + Vector((0, 0.07, 0.0)), Vector((1, 0, 0)), Vector((0, 0, 1)), bevel=0.008))
        piece = "anvil"
    elif has(text, "skep", "hive"):
        sm = X.mat("#c9a04a", "leather")
        parts.append(G.lathe([(0, 0.0), (0.13, 0.0)] + [(0.13 * math.cos(t), 0.26 * math.sin(t) + 0.02) for t in [math.pi / 2 * i / 7 for i in range(8)]], sm, center=C + Vector((0, 0.13, -0.1)), segs=24, M=G.frame((0, 0.25, 1))))
        for k in range(6):
            t = math.pi / 2 * k / 6; r = 0.13 * math.cos(t) + 0.004; z = 0.26 * math.sin(t) + 0.02
            ring = [C + Vector((0, 0.13, -0.1)) + Vector((math.cos(j / 24 * TAU) * r, math.sin(j / 24 * TAU) * r * 0.97, z)) for j in range(25)]
            parts.append(G.sweep(ring, 0.008, sm, segs=6, cap=False))
        piece = "skep hive"
    elif has(text, "basket"):
        wm = X.mat("#a8803a", "leather")
        parts.append(G.lathe([(0, 0), (0.13, 0), (0.16, 0.3), (0.155, 0.3), (0.125, 0.006), (0, 0.006)], wm, center=C + Vector((0, 0.14, -0.18)), rx=1.0, ry=0.7, segs=24))
        for k in range(6):
            p = C + Vector(((k - 2.5) * 0.04, 0.14, 0.12))
            parts.append(G.sweep(G.bezier(p, p + Vector((0, 0.02, 0.08)), p + Vector((0.03, 0.06, 0.1)), n=5), G.taper(6, 0.01, 0.004), X.glow, segs=6))
        piece = "basket"
    elif has(text, "shrine", "reliquary"):
        gm = X.metal_of("gold")
        parts.append(G.box(C + Vector((0, 0.11, 0.0)), (0.22, 0.14, 0.3), gm, bevel=0.01))
        parts.append(G.extrude([(-0.13, 0), (0.13, 0), (0, 0.1)], 0.15, gm, C + Vector((0, 0.11, 0.15)), Vector((1, 0, 0)), Vector((0, 0, 1))))
        parts.append(G.box(C + Vector((0, 0.035, 0.0)), (0.12, 0.01, 0.18), X.glow, bevel=0.0))
        piece = "reliquary"
    elif has(text, "chimney"):
        st = X.mat("#5a524a", "stone")
        for sx in (-1, 1):
            for k in range(4):
                parts.append(G.box(C + Vector((sx * 0.1, 0.08, -0.05 + k * 0.09)), (0.1 - 0.008 * k, 0.1, 0.085), st, bevel=0.012, M=Matrix.Rotation(0.1 * k * sx, 3, "Z")))
            top = C + Vector((sx * 0.1, 0.08, 0.33))
            parts.append(G.lathe([(0, 0), (0.035, 0.02), (0.02, 0.08), (0, 0.13)], X.glow, center=top, segs=8))
        piece = "chimney stack"
    elif has(text, "rocket", "firework"):
        parts.append(G.box(C + Vector((0, 0.07, 0)), (0.2, 0.08, 0.26), X.mat("#5a3a22", "leather"), bevel=0.015))
        for k in range(5):
            p = C + Vector(((k - 2) * 0.04, 0.1, 0.2))
            col = ("#c0302a", "#2a62b8", "#d8b04a", "#3f8a3a", "#c0302a")[k]
            parts += tube(p, (0, 0, 1), 0.016, 0.22, X.mat(col))
            parts.append(G.lathe([(0, 0), (0.018, 0), (0, 0.05)], X.mat("#efe6cf"), center=p + Vector((0, 0, 0.11)), segs=8))
        piece = "rocket pack"
    elif has(text, "banner", "pole"):
        parts += tube(C + Vector((0.08, 0.06, 0.35)), (0, 0, 1), 0.012, 1.3, X.mat("#3a2a1a", "leather"))
        fl = []
        top = C + Vector((0.08, 0.07, 0.9))
        for i in range(9):
            t = i / 8
            fl.append(top + Vector((-0.02 - 0.0 * t, 0.01 + 0.03 * math.sin(t * 5), -0.5 * t)))
        parts.append(G.sweep(fl, [(0.13 * (1 - 0.4 * t), 0.004) for t in [i / 8 for i in range(9)]], X.mat(G.colour_in(text, "#3a3448")), segs=6, side=Vector((1, 0, 0))))
        piece = "banner"
    elif has(text, "rope", "net"):
        rm = X.mat("#b9a463", "leather")
        sh, _ = X.U.rest_bone(X.arm, "clavicle_l")
        cc = Vector((sh.x + 0.02, sh.y + 0.02, sh.z - 0.06))
        axis = Vector((0.0, -0.3, 1)).normalized(); M = G.frame(Vector((0.8, 0, 0.6)))
        for k in range(5):
            ring = [cc + M @ Vector((math.cos(j / 24 * TAU) * 0.16, math.sin(j / 24 * TAU) * 0.2, 0.008 * k)) for j in range(25)]
            parts.append(G.sweep(ring, 0.01, rm, segs=6, cap=False))
        if has(text, "net"):
            for k in range(6):
                parts.append(G.sweep([cc + Vector((0.02 * k - 0.05, -0.06, 0.04)), cc + Vector((0.03 * k - 0.07, -0.08, -0.2))], 0.003, rm, segs=4))
        piece = "rope coil (shoulder)" if not has(text, "net") else "net"
    else:   # sacks and packs
        sm = X.mat(G.colour_in(text, "#8a7a5a"))
        if has(text, "charcoal", "soot", "black"): sm = X.mat("#2a2622")
        if has(text, "bedroll"):
            parts.append(G.lathe([(0, 0), (0.06, 0), (0.06, 0.34), (0, 0.34)], X.mat(G.colour_in(text, "#6a5a3a")), center=C + Vector((-0.17, 0.09, 0.12)), segs=14, M=G.frame((1, 0, 0))))
            for t in (0.06, 0.28): parts.append(G.lathe([(0.062, t - 0.008), (0.066, t - 0.008), (0.066, t + 0.008), (0.062, t + 0.008)], X.mat("#3a2a1a", "leather"), center=C + Vector((-0.17, 0.09, 0.12)), segs=14, M=G.frame((1, 0, 0))))
            piece = "bedroll"
        elif has(text, "backpack", "pack"):
            parts.append(G.box(C + Vector((0, 0.08, -0.02)), (0.24, 0.12, 0.3), sm, bevel=0.025))
            parts.append(G.lathe([(0, 0), (0.05, 0), (0.05, 0.26), (0, 0.26)], X.mat("#4a3a2a"), center=C + Vector((-0.13, 0.08, 0.15)), segs=12, M=G.frame((1, 0, 0))))
            piece = "backpack"
        else:
            o = G.sphere(C + Vector((0, 0.11, 0.0)), 0.16, sm, segs=16, scale=(1.0, 0.65, 1.15))
            for v in o.data.vertices: v.co += Vector((0.012 * math.sin(v.co.z * 70), 0.01 * math.sin(v.co.x * 60), 0))
            parts.append(o)
            parts.append(G.lathe([(0.05, 0), (0.03, 0.04), (0.05, 0.07)], sm, center=C + Vector((0, 0.11, 0.17)), segs=10))
            piece = "sack"
        o, _, _ = strap(B, X.mat("#4a3220", "leather"), side=1, width=0.035); parts.append(o)
    return X.add(G.join(parts), {"spine_03": 0.85, "spine_02": 0.15}, tag="Kit_Acc", piece=piece)


def apron(X, B, text, tabard=False):
    m = X.cloth_of(text, "#efe8d8" if has(text, "white", "flour") else X.pal.get("secondary"))
    if has(text, "leather", "smith"): m = X.mat(G.colour_in(text, "#5a3a22"), "leather")
    th_l, tt_l = X.U.rest_bone(X.arm, "thigh_l"); th_r, _ = X.U.rest_bone(X.arm, "thigh_r"); kn, _ = X.U.rest_bone(X.arm, "calf_l")
    z_top = (B.s3.z + 0.02) if tabard else B.waist_z
    z_bot = (kn.z + 0.08) if not has(text, "skirt", "long") else (kn.z - 0.12)
    if tabard: z_bot = (th_l.z + kn.z) / 2
    rows, cols = 12, 9
    out = []
    for back_side in ((False, True) if tabard else (False,)):
        sgn = 1 if back_side else -1
        grid = []
        for i in range(rows + 1):
            z = z_top + (z_bot - z_top) * i / rows
            half = (0.14 if tabard else 0.13) + 0.04 * i / rows
            row = []
            for j in range(cols + 1):
                x = B.pel.x - half + 2 * half * j / cols
                hit = X.fit.surface(Vector((x, B.s2.y + sgn * 0.6, z)), (0, -sgn, 0))
                y = (hit.y if hit is not None else B.s2.y + sgn * 0.12) + sgn * 0.012
                if i > 0 and grid: y = min(y, grid[-1][j].y) if not back_side else max(y, grid[-1][j].y)
                row.append(Vector((x, y, z)))
            grid.append(row)
        import bmesh
        bm = bmesh.new(); V = [[bm.verts.new(p) for p in r] for r in grid]
        for i in range(rows):
            for j in range(cols):
                f = (V[i][j], V[i][j + 1], V[i + 1][j + 1], V[i + 1][j]) if back_side else (V[i][j], V[i + 1][j], V[i + 1][j + 1], V[i][j + 1])
                bm.faces.new(f)
        o = G._obj("Kit_Acc", bm, m)
        for x in bpy.context.selected_objects: x.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.006; bpy.ops.object.modifier_apply(modifier="S")
        out.append(o)
    if has(text, "hem") and not tabard:
        ring = [Vector((B.pel.x - 0.17 + 0.34 * j / 12, 0, z_bot)) for j in range(13)]
        for p in ring:
            hit = X.fit.surface(Vector((p.x, B.s2.y - 0.6, z_bot)), (0, 1, 0)); p.y = (hit.y if hit else B.s2.y - 0.12) + -0.016
        out.append(G.sweep(ring, (0.006, 0.012), X.mat(G.colour_in(text[text.find("hem") - 15:], "#e0662a")), segs=6, side=Vector((0, 0, 1))))
    if tabard and has(text, "star"):
        sc = Vector((B.pel.x, B.front_y(B.s2.z) - 0.02, B.s2.z))
        out.append(G.extrude(star_outline(0.06, 0.025, 8 if has(text, "eight") else 5), 0.004, X.metal_of(text.split("with")[-1], "#d8b04a"), sc, Vector((1, 0, 0)), Vector((0, 0, 1))))
    o = G.join(out)
    pel_z, kz = B.pel.z, kn.z
    def w(p):
        t = max(0.0, min(1.0, (pel_z - p.z) / max(0.05, pel_z - kz)))
        if tabard and p.z > pel_z: return {"spine_03": 0.6, "spine_02": 0.4}
        sl = max(0.0, min(1.0, (p.x - th_r.x) / max(1e-6, th_l.x - th_r.x)))
        return {"pelvis": 1 - t * 0.85, "thigh_l": t * 0.85 * sl, "thigh_r": t * 0.85 * (1 - sl)}
    return X.add(o, w, tag="Kit_Acc", piece="tabard" if tabard else "apron")


def accessories(c, arm, mats, U):
    X = Ctx(c, arm, mats, U)
    B = Body(X)
    o = c.get("outfit", {}) or {}
    # the design's custom accessories (pack accessories are already loaded by ubc_parts) and kit details
    items = [str(a).replace("custom:", "").lower() for a in (o.get("acc") or []) if str(a).startswith("custom")] + \
            [str(k).lower() for k in c.get("kit", []) if not str(k).startswith("custom:")]
    weapon = str(c.get("weapon", "")).lower()
    done = set(); hip = 0; unmapped = []
    for it in items:
        if not it or it in done: continue
        done.add(it)
        if has(it, "tail", "ears", "flower", "bloom") and not has(it, "tailed"): continue
        if has(it, "goggle", "lens"):
            if not has(X.head_txt, "goggle", "lens", "spectacle") and "goggles" not in done:
                done.add("goggles"); import kit_head as KH_; X.add(KH_.goggles(X, it), "Head", piece="goggles")
            continue
        if any(has(it, s) for s in SKIP) and not has(it, "bandolier", "belt", "sash", "quiver", "pauldron", "collar", "scarf"):
            continue
        if any(has(it, s) for s in NOT_ACC) and not has(it, "apron", "tabard", "pauldron", "anklet", "wrap", "bracer", "bangle", "lantern hung", "belt", "sash", "back", "quiver"):
            continue
        if has(it, "crown", "circlet", "halo", "horn", "antler", "veil", "mask", "hood", "hat", "helm", "cap", "headband", "orb", "staff", "spear", "sword",
               "blade", "dagger", "bow", "shield", "hammer", "axe", "lantern in", "bell", "book", "tome", "mirror", "glow", "flame", "fire", "spark", "light", "gem") \
                and not has(it, "war horn", "belt", "sash", "quiver", "pauldron", "satchel", "pouch", "bandolier", "collar", "lantern hung", "back"):
            continue
        if weapon and it in weapon: continue
        try:
            if has(it, "pauldron"):
                pauldron(X, B, it, sides=(1,) if has(it, "left", "single", "one shoulder", "asymmetric") else (1, -1)); continue
            if has(it, "bandolier", "sash", "harness") or has(it, "climbing rope"): sash(X, B, it); continue
            if has(it, "belt") and not has(it, "on his belt", "on her belt", "at his belt", "at her belt", "at the back of the belt"): belt(X, B, it); continue
            if has(it, "scarf", "kerchief"): scarf(X, B, it); continue
            if has(it, "necklace", "prayer", "bead", "charm string", "chain") and not has(it, "watch"): necklace(X, B, it); continue
            if has(it, "collar") and not has(it, "clasp"): collar(X, B, it); continue
            if has(it, "bangle", "bracer", "arm wrap", "wrap", "wrapped", "glove", "ribbon") and not has(it, "bramble"): bracers(X, B, it); continue
            if has(it, "anklet"): anklets(X, B, it); continue
            if has(it, "whistle"): necklace(X, B, it); continue
            if has(it, "earring"): earring(X, B, it); continue
            if has(it, "apron"): apron(X, B, it); continue
            if has(it, "tabard"): apron(X, B, it, tabard=True); continue
            if has(it, "bramble"): pauldron(X, B, "bramble", sides=(1,)); continue
            if has(it, "keg", "quiver", "javelin", "scroll case", "spyglass", "war horn", "anvil", "skep", "hive", "basket", "reliquary", "shrine", "chimney",
                   "rocket", "banner", "sack", "backpack", "strapped", "bedroll") or (has(it, "rope", "net") and has(it, "shoulder", "over")) or has(it, "on the back", "on his back", "on her back"):
                back_item(X, B, it); continue
            if has(it, "pouch", "satchel", "vial", "holster", "smoker", "padlock", "hook", "spool", "watch", "lantern", "rope", "coil"):
                hip_item(X, B, it, hip); hip += 1; continue
            unmapped.append(it)
        except Exception as e:
            print(f"[kit_acc] {c['id']}: {it!r} failed: {e}")
            unmapped.append(it + " (error)")
    for u in unmapped: REPORT.append((c["id"], "UNMAPPED: " + u))
    return X.out
