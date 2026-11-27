"""Modelled head pieces and species features for the high-detail heroes (replaces the cone/torus/box
placeholders): crowns, circlets, halos, horns and antlers, hats, caps, kerchiefs, helms, hoods, veils,
masks, goggles, plumes, flowers; ears, beaks, muzzles, manes, antennae, tails, shells, fins, golem stone
armour and the wraith's tattered lower body. Chosen and tinted from the design text (outfit.head,
kit, species); everything is fitted to the dressed head by ray casting (kit_geo.Fit) and bound rigidly
to its bone with ubc.bind.

    head(c, arm, mats, U)     -> [objects]   U is the ubc module (bind, rest_bone, head_box, body_bvh, has)
    species(c, arm, mats, U)  -> [objects]
"""
import bpy, bmesh, math, re, zlib, random
from mathutils import Vector, Matrix
import kit_geo as G
from kit_geo import TAU, FRONT
import ubc_parts as P

REPORT = []   # (hero, piece) pairs for the mapping report


def has(t, *ks):
    return any(re.search(r"\b" + k + r"(e?s)?\b", t) for k in ks)


class Ctx:
    """Per-hero state: materials, the head box, ray fitting, binding."""
    def __init__(self, c, arm, mats, U):
        self.c, self.arm, self.mats, self.U = c, arm, mats, U
        self.out = []
        self.rng = random.Random(zlib.crc32(c["id"].encode()))
        self.hb = U.head_box(arm)
        self.fit = G.Fit(U.body_bvh())
        self._face = None

        o = c.get("outfit", {}) or {}
        self.head_txt = str(o.get("head", "")).replace("custom:", "").lower()
        self.kit_txt = " | ".join(str(k) for k in c.get("kit", [])).lower()
        self.acc_txt = " | ".join(str(a).replace("custom:", "") for a in (o.get("acc") or [])).lower()
        self.species = str(c.get("species", "human")).lower()
        self.pal = c.get("palette", {})
        self._m = {}
        self.hover = []   # halos and floating crowns: kept clear of hats, hoods and helms (clear_hover)

    @property
    def face(self):
        """Fitting against the bare head only (skin faces above the neck): masks sit on the face, not on
        hair or a hood (owner polish pass: masks bulged where rays hit the hair)."""
        if self._face is None:
            from mathutils.bvhtree import BVHTree
            dg = bpy.context.evaluated_depsgraph_get(); verts, polys = [], []
            nz = self.hb["base"]
            for o in bpy.context.scene.objects:
                if o.type != "MESH" or o.name.startswith(("Kit_", "Icosphere")): continue
                skin = {i for i, m in enumerate(o.data.materials) if m and "Regular" in m.name}
                if not skin: continue
                me = o.evaluated_get(dg).to_mesh(); mw = o.matrix_world; base = len(verts)
                verts += [mw @ v.co for v in me.vertices]
                for p_ in me.polygons:
                    if p_.material_index in skin and (mw @ p_.center).z > nz - 0.02: polys.append([base + i for i in p_.vertices])
                o.evaluated_get(dg).to_mesh_clear()
            self._face = G.Fit(BVHTree.FromPolygons(verts, polys)) if polys else self.fit
        return self._face

    # materials: kind = cloth | metal | glow | leather | stone | horn | skin | fur | glass
    def mat(self, hexcol, kind="cloth"):
        hexcol = hexcol or "#888888"; key = (hexcol, kind)
        if key not in self._m:
            col = P.hexc(hexcol)
            if kind == "metal": m = P.flat("K_HMetal", col, metal=0.85, rough=0.32)
            elif kind == "glow": m = P.flat("K_HGlow", col, emit=4)
            elif kind == "stone": m = P.flat("K_HStone", col, rough=0.95)
            elif kind == "glass": m = P.flat("K_HGlass", col, metal=0.2, rough=0.08)
            elif kind == "horn": m = P.flat("K_HHorn", col, rough=0.45)
            elif kind == "leather": m = P.flat("K_HLeather", col, rough=0.55)
            else: m = P.flat("K_HCloth", col, rough=0.75)
            self._m[key] = m
        return self._m[key]

    @property
    def glow(self): return self.mats["glow"]

    def metal_of(self, text, default=None):
        return self.mat(G.colour_in(text, default or self.pal.get("accent") or "#d8b04a"), "metal")

    def cloth_of(self, text, default=None):
        return self.mat(G.colour_in(text, default or self.pal.get("main") or "#7a6a5a"), "cloth")

    def add(self, obj, bone="Head", tag="Kit_Head", piece=None):
        if obj is None: return None
        obj.name = tag
        if isinstance(bone, dict) or callable(bone): self.U.bind(obj, self.arm, bone)
        else: self.U.bind(obj, self.arm, {bone: 1.0})
        self.out.append(obj)
        if piece: REPORT.append((self.c["id"], piece))
        return obj

    # head landmarks
    @property
    def cx(self): return self.hb["cx"]
    @property
    def cy(self): return self.hb["cy"]
    @property
    def top(self): return self.hb["top"]
    @property
    def base(self): return self.hb["base"]
    @property
    def eye(self): return self.hb["eye"]
    @property
    def front(self): return self.hb["front"]
    @property
    def hw(self): return self.hb["hw"]
    @property
    def brow(self): return self.eye + 0.36 * (self.top - self.eye)
    @property
    def back(self): return self.cy + (self.cy - self.front)

    def head_ring(self, z, n=32, tilt=0.0):
        return self.fit.ring(self.cx, self.cy, z, n, fallback=self.hw, reach=0.45, zfun=(lambda a: tilt * math.sin(a)) if tilt else None)


# ---------- shells fitted to the head (caps, helms, hoods, veils, masks, kerchiefs) ----------

def shell(X, zs, offs, mat, a0=0.0, a1=TAU, segs=32, close_top=True, top_off=0.012, thick=0.006, zfun=None, name="Kit_Head"):
    """A surface hugging the dressed head: rows at heights zs (bottom to top) offset outward by offs
    (one per row); angles a0..a1 (a partial range leaves the face open). zfun(a, row) shifts a row's
    height per angle (a kerchief's front edge rises to the brow). Solidified to `thick`."""
    full = abs((a1 - a0) - TAU) < 1e-6
    n = segs if full else segs + 1
    bm = bmesh.new(); rows = []
    prev = None
    for k, (z, off) in enumerate(zip(zs, offs)):
        row = []
        for i in range(n):
            a = a0 + (a1 - a0) * i / segs
            zz = z + (zfun(a, k) if zfun else 0.0)
            r = X.fit.radius(X.cx, X.cy, zz, a, fallback=(prev[i] if prev else X.hw), reach=0.45)
            if prev and r < prev[i] * 0.35: r = prev[i] * 0.9   # missed the head (above the hair): keep shrinking smoothly
            if zz < X.base: r = min(r, X.hw * 1.02 + (X.base - zz) * 0.45)   # below the jaw: drape, don't wrap the shoulders
            row.append(r)
        prev = row
        rows.append([bm.verts.new((X.cx + math.cos(a0 + (a1 - a0) * i / segs) * (row[i] + off), X.cy + math.sin(a0 + (a1 - a0) * i / segs) * (row[i] + off),
                                   z + (zfun(a0 + (a1 - a0) * i / segs, k) if zfun else 0.0))) for i in range(n)])
    for A, B in zip(rows, rows[1:]):
        for i in range(n - (0 if full else 1)):
            j = (i + 1) % n; bm.faces.new((A[i], A[j], B[j], B[i]))
    if close_top:
        apex = bm.verts.new((X.cx, X.cy, zs[-1] + top_off)); A = rows[-1]
        for i in range(n - (0 if full else 1)): bm.faces.new((A[i], A[(i + 1) % n], apex))
    o = G._obj(name, bm, mat)
    if thick > 0:
        for x in bpy.context.selected_objects: x.select_set(False)
        o.select_set(True); bpy.context.view_layer.objects.active = o
        md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = thick; md.offset = 1.0
        bpy.ops.object.modifier_apply(modifier="S")
    return o


def feather(root, direction, length, width, mat, side=(1, 0, 0), curl=0.25, name="Kit_Head"):
    d = Vector(direction).normalized(); s = Vector(side)
    bend = (d.cross(s)).normalized() if d.cross(s).length > 1e-4 else Vector((0, 0, 1))
    pts = [Vector(root) + d * (length * t) + bend * (curl * length * t * t) for t in [i / 8 for i in range(9)]]
    radii = [(width * w, width * 0.12) for w in (0.25, 0.7, 0.95, 1.0, 0.95, 0.85, 0.65, 0.4, 0.0)]
    return G.sweep(pts, radii, mat, segs=6, side=s, name=name)


def flower(center, normal, r, petal_mat, core_mat, petals=5, name="Kit_Head"):
    n = Vector(normal).normalized(); u = n.orthogonal().normalized(); v = n.cross(u)
    parts = []
    for k in range(petals):
        a = k / petals * TAU; d = u * math.cos(a) + v * math.sin(a)
        M = G.frame_xz(d, n)
        parts.append(G.sphere(Vector(center) + d * r * 0.75 + n * r * 0.1, r * 0.55, petal_mat, segs=8, scale=(1.0, 0.6, 0.22), M=M))
    parts.append(G.sphere(center, r * 0.38, core_mat, segs=8, scale=(1, 1, 0.6), M=G.frame(n)))
    return G.join(parts, name)


def star_outline(R, r, k=5, rot=math.pi / 2):
    return [((R if i % 2 == 0 else r) * math.cos(rot + i * math.pi / k), (R if i % 2 == 0 else r) * math.sin(rot + i * math.pi / k)) for i in range(2 * k)]


def circle_outline(R, n=24):
    return [(R * math.cos(i / n * TAU), R * math.sin(i / n * TAU)) for i in range(n)]


# ---------- crowns ----------

def crown(X, text):
    style = ("thorn" if has(text, "thorn") else "candle" if has(text, "candle") else "shard" if has(text, "basalt", "shard") else
             "rays" if has(text, "ray", "sun") else "ice" if has(text, "ice", "frost") else "coral" if has(text, "coral") else
             "flower" if has(text, "flower", "bloom", "lily", "lilies", "night-lily") else "smoke" if has(text, "smoke") else
             "water" if has(text, "water") else "flame" if has(text, "flame", "ember", "fire", "lava") else
             "star" if has(text, "star") else "fleur")
    # a thorn crown is seated on the hair even when the design says it floats: hovering, it read as a dark
    # disc over the head (owner report 2026-11-27, Morwenna)
    floating = (has(text, "floating") or style in ("smoke", "water")) and style != "thorn"
    gold = X.metal_of(text, "#d8b04a")
    if style in ("thorn",): gold = X.mat(G.colour_in(text, "#241c24"), "horn")
    if style == "shard": gold = X.mat("#2c2a2a", "stone")
    if style == "ice": gold = X.mat("#cfeaff", "glass")
    if style == "coral": gold = X.mat(G.colour_in(text, "#e2725e"), "horn")
    if style == "flower": gold = X.mat("#3f6a2a", "cloth")
    # A floating crown hovers just clear of the head (owner report 2026-11-27: at 3.5 cm, seen from the
    # low battle and portrait cameras, it read as a crown or hair knocked off the head).
    z0 = X.top - 0.04 if not floating else X.top + 0.012
    radii = X.head_ring(z0) if not floating else [X.hw * 0.88] * 32
    if not floating: radii = [r + 0.004 for r in radii]
    parts = []
    bh = 0.03 if style not in ("flower", "smoke", "water", "thorn") else 0.012
    if style == "thorn":
        # two thin twisted briar vines, not a solid band, resting on the hair
        z0 = X.top - 0.03; radii = [r + 0.003 for r in X.head_ring(z0)]
        for ph in (0.0, math.pi):
            vine = []
            for j in range(65):
                a = j / 64 * TAU; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.004)
                vine.append(Vector((p.x, p.y, z0 + 0.006 + 0.005 * math.sin(a * 7 + ph))))
            parts.append(G.sweep(vine, 0.0035, gold, segs=6, cap=False))
    elif style not in ("smoke",):
        parts.append(G.band(radii, gold, X.cx, X.cy, z0, bh, 0.006, flare=0.006))
    n = {"rays": 12, "candle": 3, "flower": 7, "thorn": 9, "shard": 7, "ice": 7, "coral": 6, "water": 1, "smoke": 0}.get(style, 7)
    for k in range(n):
        a = FRONT + k / n * TAU
        p, d = G.ring_point(radii, X.cx, X.cy, a, 0.006)
        base = Vector((p.x, p.y, z0 + bh * 0.8)); t = Vector((-math.sin(a), math.cos(a), 0)); up = (Vector((0, 0, 1)) + d * 0.18).normalized()
        big = 1.25 if k == 0 else 1.0
        if style == "fleur":
            w, h = 0.016 * big, 0.05 * big
            ol = [(-w, 0), (w, 0), (w * 0.5, h * 0.45), (w * 0.95, h * 0.72), (w * 0.35, h * 0.62), (0, h), (-w * 0.35, h * 0.62), (-w * 0.95, h * 0.72), (-w * 0.5, h * 0.45)]
            parts.append(G.extrude(ol, 0.006, gold, base, t, up))
            if k == 0: parts.append(G.sphere(base + up * 0.012 - d * 0.0, 0.009, X.glow, segs=10))
        elif style == "star":
            w, h = 0.012, 0.04 * big
            parts.append(G.extrude([(-w, 0), (w, 0), (0, h)], 0.006, gold, base, t, up))
            parts.append(G.extrude(star_outline(0.014 * big, 0.006 * big), 0.005, X.glow, base + up * h, t, up))
        elif style == "flame":
            w, h = 0.016, 0.065 * big
            ol = [(-w, 0), (w, 0), (w * 0.6, h * 0.4), (w * 0.95, h * 0.66), (w * 0.2, h * 0.58), (w * 0.15, h), (-w * 0.3, h * 0.55), (-w * 0.85, h * 0.72), (-w * 0.55, h * 0.36)]
            parts.append(G.extrude(ol, 0.006, X.glow, base, t, up))
        elif style == "rays":
            w, h = 0.007, 0.075 * (1.3 if k % 2 == 0 else 0.85)
            parts.append(G.extrude([(-w, 0), (w, 0), (0, h)], 0.005, gold, base, t, (Vector((0, 0, 1)) + d * 0.5).normalized()))
        elif style == "thorn":
            side = t * (1 if k % 2 else -1)
            pts = G.bezier(base, base + up * 0.025 + side * 0.01, base + up * 0.05 + d * 0.012 + side * 0.022, n=8)
            parts.append(G.sweep(pts, G.taper(9, 0.004, 0.0004), gold, segs=6))   # thin briar thorns
            if X.glow is not None and "glow" in X.kit_txt + text: parts.append(G.sphere(pts[-1], 0.004, X.glow, segs=6))
            mid = pts[4]; parts.append(G.sweep([mid, mid - side * 0.012 + up * 0.006], [0.0025, 0.0003], gold, segs=5))
        elif style == "candle":
            a2 = FRONT + (k - 1) * 0.55
            p, d = G.ring_point(radii, X.cx, X.cy, a2, 0.004)
            cb = Vector((p.x, p.y, z0 + bh))
            parts.append(G.lathe([(0, 0), (0.011, 0), (0.011, 0.045), (0.0095, 0.05), (0, 0.05)], X.mat("#efe6cf", "cloth"), center=cb, segs=12))
            parts.append(G.lathe([(0, 0.05), (0.006, 0.058), (0.004, 0.07), (0, 0.082)], X.glow, center=cb, segs=10))
        elif style == "shard":
            h = 0.04 + X.rng.random() * 0.04; w = 0.01 + X.rng.random() * 0.008
            tilt = (up + t * (X.rng.random() - 0.5) * 0.5 + d * 0.2).normalized()
            parts.append(G.extrude([(-w, 0), (w * 0.8, 0), (w * 0.3, h * 0.85), (-w * 0.1, h)], 0.012, gold, base, t, tilt))
            parts.append(G.extrude([(-w * 0.3, h * 0.25), (w * 0.3, h * 0.25), (0, h * 0.8)], 0.0125 + 0.002, X.glow, base, t, tilt))
        elif style == "ice":
            h = 0.035 + 0.025 * (1 if k % 2 == 0 else 0.4)
            parts.append(G.lathe([(0, 0), (0.008, 0.002), (0.008, h * 0.7), (0, h)], gold, center=base, segs=6, M=G.frame(up)))
        elif style == "coral":
            pts = G.bezier(base, base + up * 0.03 + t * 0.01, base + up * 0.05 - t * 0.008 + d * 0.01, n=6)
            parts.append(G.sweep(pts, G.taper(7, 0.006, 0.002), gold, segs=6))
            parts.append(G.sweep([pts[3], pts[3] + t * 0.018 + up * 0.012], [0.004, 0.0015], gold, segs=6))
        elif style == "flower":
            lily = has(text, "lily", "lilies", "huge")
            pm = X.mat(G.colour_in(text, "#8a4ad8" if lily else "#f0a8c8"))
            parts.append(flower(base + up * 0.006, (d + Vector((0, 0, 0.8))).normalized(), 0.022 if lily else 0.014, pm, X.mat("#f2d36a")))
            if has(text, "thorn"):
                parts.append(G.sweep([base, base + up * 0.02 + t * 0.012], [0.003, 0.0004], X.mat("#3a2a1a", "horn"), segs=5))
        elif style == "water":
            parts.append(G.extrude(star_outline(0.04, 0.017), 0.01, X.glow, Vector((X.cx, X.cy, z0 + 0.05)), Vector((1, 0, 0)), Vector((0, 0, 1))))
    if style == "smoke":
        # pale violet smoke (a grey ring read as a lock of hair), thinner, with glowing wisps
        sm = X.mat(G.colour_in(text, "#b9aed0"), "cloth")
        for i, (zz, rr) in enumerate(((0.0, 1.0), (0.016, 0.86), (0.032, 0.72))):
            ring = [(X.cx + math.cos(j / 24 * TAU) * X.hw * 0.9 * rr, X.cy + math.sin(j / 24 * TAU) * X.hw * 0.9 * rr, z0 + zz + 0.008 * math.sin(j / 24 * TAU * 3 + i)) for j in range(25)]
            parts.append(G.sweep(ring, 0.006, sm, segs=8, cap=False))
        for k in range(5):
            a = FRONT + k / 5 * TAU
            parts.append(G.sphere(Vector((X.cx + math.cos(a) * X.hw * 0.8, X.cy + math.sin(a) * X.hw * 0.8, z0 + 0.02 + 0.012 * (k % 2))), 0.006, X.glow, segs=8))
    if style == "water" or has(text, "ring of water"):
        ring = [(X.cx + math.cos(j / 32 * TAU) * X.hw * 1.15, X.cy + math.sin(j / 32 * TAU) * X.hw * 1.15, z0 + 0.01) for j in range(33)]
        parts.append(G.sweep(ring, 0.009, X.mat("#5ab8e8", "glass"), segs=8, cap=False))
    if style == "coral" and has(text, "inside"):
        pass
    if has(text, "stone", "gem") and style not in ("fleur",):
        p, d = G.ring_point(radii, X.cx, X.cy, FRONT, 0.01)
        parts.append(G.sphere(Vector((p.x, p.y, z0 + bh * 0.5)), 0.009, X.glow, segs=10))
    if has(text, "broken"):
        pass
    if floating and style != "smoke":
        # motes of light under the ring say "magic", not "misplaced"
        for k in range(4):
            a = FRONT + math.pi / 4 + k / 4 * TAU
            parts.append(G.sphere(Vector((X.cx + math.cos(a) * X.hw * 0.7, X.cy + math.sin(a) * X.hw * 0.7, z0 - 0.004)), 0.005, X.glow, segs=8))
    o = X.add(G.join(parts), "Head", piece="crown:" + style + (" floating" if floating else ""))
    if floating: X.hover.append(o)
    return o


def circlet(X, text):
    mt = X.metal_of(text, "#d8b04a")
    if has(text, "ice"): mt = X.mat("#cfeaff", "glass")
    if has(text, "reed"): mt = X.mat("#b9a463", "leather")
    if has(text, "feather"): mt = X.mat(G.colour_in(text, "#8a6a3a"), "leather")
    z = X.brow
    radii = [r + 0.003 for r in X.head_ring(z, tilt=0.018)]
    tiltf = lambda a: 0.018 * math.sin(a)
    parts = [G.band(radii, mt, X.cx, X.cy, z - 0.004, 0.009, 0.004, zfun=tiltf)]
    pf, df = G.ring_point(radii, X.cx, X.cy, FRONT, 0.006); fz = z + tiltf(FRONT)
    t = Vector((1, 0, 0))
    if has(text, "star"):
        for k in range(-3, 4):
            a = FRONT + k * 0.32; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.006)
            s = 0.016 if k == 0 else 0.009
            parts.append(G.extrude(star_outline(s, s * 0.42), 0.004, X.glow if k == 0 else mt, Vector((p.x, p.y, z + tiltf(a) + 0.008)), Vector((-math.sin(a), math.cos(a), 0)), Vector((0, 0, 1))))
    elif has(text, "crescent"):
        ol = [(0.024 * math.cos(u), 0.024 * math.sin(u)) for u in [math.pi * i / 10 for i in range(11)]] + [(0.016 * math.cos(u), 0.012 + 0.016 * math.sin(u) * 0.6) for u in [math.pi * (10 - i) / 10 for i in range(11)]]
        parts.append(G.extrude(ol, 0.004, mt, Vector((pf.x, pf.y, fz + 0.004)), t, Vector((0, 0, 1))))
    elif has(text, "ice"):
        for k in range(-3, 4):
            a = FRONT + k * 0.28; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.006)
            h = 0.03 - abs(k) * 0.006
            parts.append(G.lathe([(0, 0), (0.005, 0.002), (0.005, h * 0.7), (0, h)], mt, center=Vector((p.x, p.y, z + tiltf(a) + 0.004)), segs=6))
    elif has(text, "feather"):
        for k in range(-4, 5):
            a = FRONT + math.pi + k * 0.3; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.006)
            parts.append(feather(Vector((p.x, p.y, z + tiltf(a))), (d * 0.3 + Vector((0, 0.2, 1))).normalized(), 0.06, 0.008, X.mat(G.colour_in(text, "#e8e0d0")), side=Vector((-math.sin(a), math.cos(a), 0))))
    else:
        gem = X.glow if has(text, "ember", "stone", "glow", "gem") or True else mt
        parts.append(G.extrude([(0, -0.008), (0.008, 0), (0, 0.014), (-0.008, 0)], 0.006, mt, Vector((pf.x, pf.y, fz + 0.003)), t, Vector((0, 0, 1))))
        parts.append(G.sphere(Vector((pf.x, pf.y - 0.004, fz + 0.005)), 0.0055, gem, segs=10))
    if has(text, "ribbon", "tail"):
        parts += tails(X, X.cloth_of(text, X.pal.get("secondary")), z, long_=has(text, "long", "very long"))
    return X.add(G.join(parts), "Head", piece="circlet")


def headband(X, text):
    cm = X.cloth_of(text, X.pal.get("secondary"))
    if has(text, "pearl"):
        z = X.brow; radii = X.head_ring(z, tilt=0.02); parts = []
        for k in range(40):
            a = k / 40 * TAU; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.008)
            parts.append(G.sphere(Vector((p.x, p.y, z + 0.02 * math.sin(a))), 0.0055, X.mat("#efe9df", "glass"), segs=8))
        return X.add(G.join(parts), "Head", piece="headband:pearl")
    z = X.brow - 0.008
    radii = [r + 0.002 for r in X.head_ring(z + 0.01, tilt=0.025)]
    parts = [G.band(radii, cm, X.cx, X.cy, z, 0.022, 0.005, zfun=lambda a: 0.025 * math.sin(a))]
    if has(text, "tail", "ribbon"):
        parts += tails(X, cm, z + 0.012, long_=has(text, "long", "very long"))
    if has(text, "pin", "star"):
        p, d = G.ring_point(radii, X.cx, X.cy, FRONT + 0.5, 0.008)
        parts.append(G.extrude(star_outline(0.012, 0.005), 0.004, X.metal_of("silver"), Vector((p.x, p.y, z + 0.012)), Vector((-math.sin(FRONT + 0.5), math.cos(FRONT + 0.5), 0)), Vector((0, 0, 1))))
    return X.add(G.join(parts), "Head", piece="headband")


def tails(X, mat, z, long_=False):
    """Two ribbon tails from a knot at the back of the head."""
    out = []; L = 0.42 if long_ else 0.24
    knot = Vector((X.cx, X.back + 0.004, z + 0.02))
    out.append(G.sphere(knot, 0.016, mat, segs=10, scale=(1.2, 0.8, 0.9)))
    for sx in (-1, 1):
        pts = G.bezier(knot, knot + Vector((sx * 0.03, 0.05, -L * 0.3)), knot + Vector((sx * 0.06, 0.08 + 0.04 * sx, -L)), n=10)
        out.append(G.sweep(pts, [(0.016, 0.002)] * 3 + [(0.018, 0.002)] * 6 + [(0.012, 0.002)] * 2, mat, segs=6, side=Vector((1, 0, 0)), cap=True))
    return out


# ---------- halos ----------

def halo(X, text):
    parts = []; g = X.glow
    if has(text, "sun-disc", "sun disc", "disc"):
        c = Vector((X.cx, X.back + 0.05, X.eye + 0.035)); R = 0.13
        parts.append(G.extrude(circle_outline(R, 32), 0.008, X.metal_of("gold"), c, Vector((1, 0, 0)), Vector((0, 0, 1))))
        parts.append(G.extrude(circle_outline(R * 0.72, 32), 0.012, g, c, Vector((1, 0, 0)), Vector((0, 0, 1))))
        nr = 12 if "12" in text or True else 12
        for k in range(nr):
            a = k / nr * TAU; d = Vector((math.cos(a), 0, math.sin(a)))
            L = 0.07 if k % 2 == 0 else 0.045
            parts.append(G.extrude([(-0.012, 0), (0.012, 0), (0, L)], 0.006, X.metal_of("gold"), c + d * (R - 0.004), Vector((math.sin(a), 0, -math.cos(a))) * -1, d))
        piece = "halo:sun-disc"
    elif has(text, "eclipse"):
        c = Vector((X.cx, X.back + 0.05, X.eye + 0.04))
        parts.append(G.extrude(circle_outline(0.11, 32), 0.01, X.mat("#0c0a10", "cloth"), c, Vector((1, 0, 0)), Vector((0, 0, 1))))
        ring = [c + Vector((math.cos(j / 40 * TAU) * 0.125, 0.004, math.sin(j / 40 * TAU) * 0.125)) for j in range(41)]
        parts.append(G.sweep(ring, 0.01, g, segs=8, cap=False))
        for k in range(16):
            a = k / 16 * TAU; d = Vector((math.cos(a), 0, math.sin(a)))
            parts.append(G.extrude([(-0.008, 0), (0.008, 0), (0, 0.035 + 0.02 * (k % 2))], 0.004, g, c + d * 0.12, Vector((-math.sin(a), 0, math.cos(a))), d))
        piece = "halo:eclipse"
    else:
        flame = has(text, "flame")
        R = X.hw * (1.05 if has(text, "small") else 1.22)
        # 5 cm over the head, the back edge raised 14 degrees: reads as a halo, not a crown off its seat
        c = Vector((X.cx, X.cy + 0.03, X.top + 0.05)); tilt = math.tan(math.radians(14))
        def T(v): return v + Vector((0, 0, (v.y - c.y) * tilt))
        ring = [T(c + Vector((math.cos(j / 48 * TAU) * R, math.sin(j / 48 * TAU) * R * 0.95, 0))) for j in range(49)]
        parts.append(G.sweep(ring, (0.009, 0.006), g, segs=8, side=Vector((0, 0, 1)), cap=False))
        inner = [T(c + Vector((math.cos(j / 48 * TAU) * R * 0.86, math.sin(j / 48 * TAU) * R * 0.82, 0.002))) for j in range(49)]
        parts.append(G.sweep(inner, 0.0035, X.metal_of(text, "#f2d77a"), segs=6, cap=False))
        piece = "halo:ring"
        if flame:
            # small licking flames that lean outward and back (upright gold points read as a crown)
            fl = X.mat(G.colour_in(text, "#ff8a2a"), "glow")
            for k in range(14):
                a = k / 14 * TAU; d = Vector((math.cos(a), math.sin(a) * 0.95, 0)); p = T(c + d * R)
                h = 0.022 if k % 2 else 0.032
                ol = [(-0.008, 0), (0.008, 0), (0.005, h * 0.5), (0.001, h), (-0.004, h * 0.55)]
                up = (Vector((0, 0, 1)) + d * 0.55 + Vector((0, 0.35, 0))).normalized()
                parts.append(G.extrude(ol, 0.003, fl, p, Vector((-math.sin(a), math.cos(a), 0)), up))
            piece = "halo:flame"
        if has(text, "bell"):
            bm_ = X.metal_of("gold")
            for k in range(7):
                a = FRONT + math.pi / 7 + k / 7 * TAU; d = Vector((math.cos(a), math.sin(a) * 0.95, 0)); p = c + d * R
                parts.append(G.sweep([p, p - Vector((0, 0, 0.022))], 0.0016, bm_, segs=5))
                bp = p - Vector((0, 0, 0.046))
                parts.append(G.lathe([(0, 0.024), (0.006, 0.022), (0.009, 0.012), (0.012, 0.002), (0.013, 0.0), (0.011, 0.0), (0.0, 0.004)], bm_, center=bp, segs=12))
                parts.append(G.sphere(bp + Vector((0, 0, 0.001)), 0.0035, bm_, segs=6))
            piece = "halo:bells"
    o = X.add(G.join(parts), "Head", piece=piece)
    if piece.startswith(("halo:ring", "halo:flame", "halo:bells")): X.hover.append(o)
    return o


# ---------- horns and antlers ----------

def horns(X, text, kind=None):
    mt = X.mat(G.colour_in(text, X.pal.get("accent") if not has(text, "ram", "goat") else "#d8c8a8"), "horn")
    if has(text, "flame", "fire"): mt = X.glow
    if "titan" in X.species or has(text, "basalt"): mt = X.mat("#3a3632", "stone")
    kind = kind or ("antler" if has(text, "antler") else "ram" if has(text, "ram", "curl") else "great" if has(text, "great", "colossal", "huge") or "titan" in X.species
                    else "goat" if has(text, "goat") else "swept")
    thorn = has(text, "thorn")
    parts = []
    for sx in (-1, 1):
        if kind == "ram":
            root = Vector((X.cx + sx * X.hw * 0.5, X.cy + 0.0, X.top - 0.03))
            # the curl's axis points out to the side; it starts up and back, sweeps down behind the ear
            # and comes forward under it, growing outward (a helix) and thinning to a point
            R0 = 0.075; C = root + Vector((sx * 0.03, 0.045, -0.055))
            th0 = math.atan2(root.z - C.z, root.y - C.y)
            pts = []
            for i in range(31):
                t = i / 30; th = th0 - t * math.pi * 1.75; R = R0 * (1 - 0.5 * t)
                pts.append(C + Vector((sx * (0.055 * t), math.cos(th) * R, math.sin(th) * R)))
            pts[0] = root
            thick = 0.034 if has(text, "thick") else 0.028
            parts.append(G.sweep(pts, G.taper(31, thick, 0.005, 1.1), mt, segs=14, ridge=0.035, ridges=34))
        elif kind == "great":
            root = Vector((X.cx + sx * X.hw * 0.75, X.cy + 0.01, X.top - 0.05))
            pts = G.bezier(root, root + Vector((sx * 0.13, 0.0, 0.0)), root + Vector((sx * 0.22, -0.02, 0.12)), root + Vector((sx * 0.17, -0.09, 0.24)), n=18)
            parts.append(G.sweep(pts, G.taper(19, 0.05, 0.004, 1.1), mt, segs=14, ridge=0.06, ridges=10))
        elif kind == "goat":
            root = Vector((X.cx + sx * X.hw * 0.4, X.cy - 0.01, X.top - 0.025))
            pts = G.bezier(root, root + Vector((sx * 0.02, 0.05, 0.08)), root + Vector((sx * 0.05, 0.15, 0.06)), root + Vector((sx * 0.07, 0.2, -0.03)), n=14)
            parts.append(G.sweep(pts, G.taper(15, 0.02, 0.003), mt, segs=10, ridge=0.1, ridges=12))
        elif kind == "antler":
            wood = X.mat(G.colour_in(text, "#5a4030"), "horn") if thorn else mt
            root = Vector((X.cx + sx * X.hw * 0.55, X.cy + 0.01, X.top - 0.035))
            beam = G.bezier(root, root + Vector((sx * 0.07, 0.03, 0.1)), root + Vector((sx * 0.14, 0.06, 0.2)), root + Vector((sx * 0.16, 0.02, 0.3)), n=14)
            parts.append(G.sweep(beam, G.taper(15, 0.016, 0.004), wood, segs=8))
            for i, L in ((4, 0.08), (8, 0.09), (11, 0.06)):
                b = beam[i]; tip = b + Vector((sx * 0.02, -0.05, 0.05)).normalized() * L
                parts.append(G.sweep(G.bezier(b, b + (tip - b) * 0.5 + Vector((0, 0, 0.015)), tip, n=6), G.taper(7, 0.009, 0.002), wood, segs=6))
            if thorn:
                for i in range(2, 14, 2):
                    b = beam[i]; d = Vector((X.rng.uniform(-1, 1), X.rng.uniform(-1, 1), 0.4)).normalized()
                    parts.append(G.sweep([b, b + d * 0.02], [0.004, 0.0003], wood, segs=5))
        else:  # swept back
            root = Vector((X.cx + sx * X.hw * 0.6, X.cy - 0.005, X.top - 0.03))
            pts = G.bezier(root, root + Vector((sx * 0.05, 0.02, 0.08)), root + Vector((sx * 0.08, 0.14, 0.1)), root + Vector((sx * 0.09, 0.22, 0.05)), n=14)
            parts.append(G.sweep(pts, G.taper(15, 0.028, 0.003, 1.2), mt, segs=10, ridge=0.07, ridges=10))
    return X.add(G.join(parts), "Head", piece="horns:" + kind)


# ---------- hats, caps, kerchiefs ----------

def _hat_fit(X, z):
    rx, ry = X.fit.ellipse(X.cx, X.cy, z, fallback=X.hw, reach=0.45)
    return rx + 0.008, ry + 0.008


def hat(X, text):
    zb = X.brow + 0.004
    rx, ry = _hat_fit(X, zb)
    own = text.split(" with ")[0]          # "admiral's bicorne with coral": the hat's colour, not the trim's
    if has(text, "straw", "beekeeper"): m = X.mat(G.colour_in(own, "#d9bd6e"), "leather")
    elif has(text, "iron", "kettle"): m = X.mat(G.colour_in(own, "#6d7076"), "metal")
    elif has(text, "sou'wester", "souwester"): m = X.mat(G.colour_in(own, "#d8a82a"), "leather")
    elif has(text, "bicorne", "tricorne"): m = X.mat(G.colour_in(own, "#1e2238"), "cloth")
    else: m = X.cloth_of(own, X.pal.get("secondary") or X.pal.get("main"))
    parts = []
    H = X.top - zb
    if has(text, "witch", "pointed"):
        kind = "witch"; ch = H + 0.24; bw = 0.075
    elif has(text, "stovepipe", "top hat"):
        kind = "stovepipe"; ch = H + (0.08 if has(text, "short") else 0.14); bw = 0.03
    elif has(text, "tricorne"):
        kind = "tricorne"; ch = H + 0.03; bw = 0.06
    elif has(text, "bicorne"):
        kind = "bicorne"; ch = H + 0.03; bw = 0.1
    elif has(text, "sou'wester", "souwester"):
        kind = "souwester"; ch = H + 0.02; bw = 0.05
    else:
        kind = "wide"; ch = H + (0.04 if has(text, "flat-crowned", "flat") else 0.05); bw = 0.1 if has(text, "very wide", "wide-brimmed", "wide brim", "wide-brim", "traveller") else 0.07
    bwn = bw / rx
    if kind in ("stovepipe",):
        crown_prof = [(0, ch), (0.98, ch), (1.0, ch - 0.004), (1.02 if not has(text, "battered") else 1.05, 0.02), (1.0, 0.006)]
    elif kind == "witch":
        crown_prof = [(0, ch), (0.12, ch * 0.8), (0.4, ch * 0.45), (0.85, 0.06), (1.0, 0.012)]
    else:
        crown_prof = [(0, ch), (0.7, ch - 0.004), (0.95, ch * 0.75), (1.0, 0.03), (1.0, 0.006)]
    prof = crown_prof + [(1.02, 0.002), (1 + bwn, -0.006), (1 + bwn, -0.013), (1.0, -0.004)]
    def zf(a, r, z):
        if r <= 1.02: return 0.0
        f = (r - 1.0) / bwn
        if kind == "tricorne": return 0.085 * f ** 0.8 * (0.5 + 0.5 * math.cos(3 * (a - FRONT) + math.pi)) ** 0.7
        if kind == "bicorne": return 0.13 * f ** 0.7 * abs(math.sin(a)) ** 0.8
        if kind == "souwester": return -0.03 * f * max(0, math.sin(a)) + 0.012 * f * max(0, -math.sin(a))
        if has(text, "bent", "slouch"): return -0.025 * f * max(0, math.cos(a - 0.4))
        return -0.008 * f
    def rf(a, r, z):
        if kind == "souwester" and r > 1.02: return 1 + 1.4 * max(0, math.sin(a)) * (r - 1) / r
        if kind == "tricorne" and r > 1.02: return 1 + 0.25 * (0.5 + 0.5 * math.cos(3 * (a - FRONT))) * (r - 1) / r
        return 1.0
    o = G.lathe(prof, m, center=(X.cx, X.cy + 0.004, zb), rx=rx, ry=ry, segs=40, zfun=zf, rfun=rf)
    if kind == "witch":
        for v in o.data.vertices:
            h = v.co.z - (zb + 0.08)
            if h > 0: v.co.y += 1.2 * h * h; v.co.z -= 0.3 * h * h
    parts.append(o)
    # hat band
    bandm = X.mat(G.colour_in(text.split("with")[-1] if "band" in text else "", "#3a2a1a") if not has(text, "brass") else "#b98a3c", "leather" if not has(text, "brass") else "metal")
    if kind not in ("tricorne", "bicorne"):
        parts.append(G.lathe([(1.0, 0.0), (1.012, 0.004), (1.012, 0.022), (1.0, 0.026)], bandm, center=(X.cx, X.cy + 0.004, zb + 0.004), rx=rx, ry=ry, segs=40))
    if has(text, "feather", "plume", "pheasant"):
        fm = X.mat(G.colour_in(text[text.find("feather") - 12:] if "feather" in text else text, "#c8a060" if has(text, "pheasant") else X.pal.get("accent", "#e0d0b0")))
        root = Vector((X.cx + rx * 0.95, X.cy + 0.02, zb + 0.02))
        parts.append(feather(root, (0.25, 0.45, 1.0), 0.22 if has(text, "tall", "pheasant") else 0.14, 0.018, fm, side=(0, 1, -0.4)))
    if has(text, "card"):
        parts.append(G.box(Vector((X.cx + rx * 0.7, X.cy - ry * 0.72, zb + 0.03)), (0.026, 0.002, 0.036), X.mat("#efe6cf"), bevel=0.0, M=Matrix.Rotation(-0.6, 3, "Z")))
    if has(text, "coral"):
        for k in range(4):
            p = Vector((X.cx + (k - 1.5) * 0.03, X.cy, X.top + 0.04))
            parts.append(G.sweep(G.bezier(p, p + Vector((0, 0.01, 0.03)), p + Vector((0.01 * (k - 1.5), 0, 0.05)), n=5), G.taper(6, 0.006, 0.002), X.mat("#e2725e", "horn"), segs=6))
    if has(text, "plume", "teal plume") and kind in ("bicorne", "tricorne"):
        root = Vector((X.cx, X.cy, zb + ch))
        parts.append(feather(root, (0.6, 0.3, 0.8), 0.15, 0.025, X.mat(G.colour_in(text[text.find("plume") - 8:], "#2a8a86")), side=(0, 1, 0)))
    if has(text, "leafy", "leaf"):
        for k in range(5):
            p = Vector((X.cx, X.cy - 0.02 + k * 0.025, X.top + 0.045))
            parts.append(G.extrude([(0, 0), (0.012, 0.02), (0, 0.05), (-0.012, 0.02)], 0.003, X.mat("#4f8a3a"), p, Vector((1, 0, 0)), Vector((0, 0.3, 1))))
    if has(text, "beekeeper", "mesh veil", "veil"):
        vm = X.mat(G.colour_in(text[text.find("veil") - 12:] if "veil" in text else "", "#b8d8a8"))
        parts.append(G.lathe([(1 + bwn * 0.95, -0.01), (1 + bwn * 0.98, -0.13), (1 + bwn * 0.96, -0.2)], vm, center=(X.cx, X.cy + 0.004, zb), rx=rx, ry=ry, segs=32, a0=0.15, a1=math.pi - 0.15))
    if has(text, "gull", "wing"):
        for sx in (-1, 1):
            for k in range(5):
                parts.append(feather(Vector((X.cx + sx * rx * 0.98, X.cy + 0.01, zb + 0.03)), (sx * 0.8, 0.25 + k * 0.1, 0.25 + k * 0.12), 0.09 - k * 0.008, 0.016, X.mat("#ece6da"), side=(0, 0, 1)))
    return X.add(G.join(parts), "Head", piece="hat:" + kind)


def cap(X, text):
    zb = X.brow - (0.005 if not has(text, "knit", "fisherman", "sailor") else 0.012)
    own = text.split(" with ")[0]
    m = X.cloth_of(own, X.pal.get("secondary") or X.pal.get("main"))
    if has(text, "hard", "miner"): m = X.mat(G.colour_in(own, "#8a7a4a"), "metal")
    if has(text, "leather", "flight"): m = X.mat(G.colour_in(own, "#6a4528"), "leather")
    knit = has(text, "knit", "fisherman", "sailor")
    zs = [zb, zb + 0.25 * (X.top - zb), zb + 0.55 * (X.top - zb), zb + 0.82 * (X.top - zb), X.top - 0.004]
    offs = [0.006, 0.007, 0.008, 0.009, 0.01] if not knit else [0.008, 0.01, 0.014, 0.016, 0.016]
    parts = [shell(X, zs, offs, m, top_off=0.014 if not knit else 0.03, thick=0.005)]
    if knit:
        radii = X.head_ring(zb + 0.006)
        parts.append(G.band(radii, m, X.cx, X.cy, zb - 0.002, 0.024, 0.008, off=0.008))
    peaked = has(text, "peaked", "porter", "harbourmaster", "miller", "hard", "falconer", "flight", "cap") and not knit and not has(text, "cat-ear", "velvet")
    if peaked:
        rx, ry = _hat_fit(X, zb + 0.004)
        v = 0.055 / ry
        parts.append(G.lathe([(1.0, 0.004), (1.0 + v, -0.01), (1.0 + v, -0.015), (1.0, -0.002)], X.mat("#1e1b22", "leather") if has(text, "porter", "harbourmaster", "peaked") else m,
                             center=(X.cx, X.cy, zb + 0.004), rx=rx, ry=ry, segs=20, a0=FRONT - 1.05, a1=FRONT + 1.05))
    if has(text, "flight") or has(text, "flaps"):
        for sx in (-1, 1):
            p = Vector((X.cx + sx * (X.hw + 0.008), X.cy + 0.005, zb - 0.02))
            parts.append(G.extrude([(-0.025, 0.02), (0.025, 0.02), (0.022, -0.03), (0, -0.045), (-0.022, -0.03)], 0.006, m, p, Vector((0, 1, 0)), Vector((0, 0, 1))))
    if has(text, "lamp", "crystal"):
        fp = Vector((X.cx, X.front - 0.012, zb + 0.03))
        parts.append(G.lathe([(0, 0), (0.016, 0), (0.016, 0.012), (0, 0.012)], X.metal_of("brass"), center=fp, segs=12, M=G.frame((0, -1, 0))))
        parts.append(G.sphere(fp + Vector((0, -0.016, 0)), 0.011, X.glow, segs=10, scale=(1, 1, 1.4)))
    if has(text, "velvet"):
        parts.append(feather(Vector((X.cx + X.hw * 0.7, X.cy, X.top - 0.01)), (0.5, 0.6, 0.6), 0.1, 0.012, X.mat(X.pal.get("accent", "#d8b04a")), side=(0, 0, 1)))
    if has(text, "brass band", "brass"):
        radii = X.head_ring(zb + 0.01)
        parts.append(G.band(radii, X.metal_of("brass"), X.cx, X.cy, zb + 0.004, 0.014, 0.003, off=0.012))
    return X.add(G.join(parts), "Head", piece="cap" + (":knit" if knit else ":peaked" if peaked else ""))


def kerchief(X, text):
    m = X.cloth_of(text, X.pal.get("secondary") or "#b0302a")
    zl = X.eye - 0.03; zt = X.top - 0.004
    zs = [zl + (zt - zl) * t for t in (0, 0.25, 0.5, 0.75, 0.92)]
    raise_front = lambda a, k: (X.brow - 0.01 - zl) * max(0.0, -math.sin(a)) ** 0.6 * (1 - k / 5) if k < 4 else 0.0
    parts = [shell(X, zs, [0.008, 0.009, 0.01, 0.011, 0.012], m, top_off=0.016, thick=0.005, zfun=raise_front)]
    knot = Vector((X.cx, X.back + 0.012, zl + 0.015))
    parts.append(G.sphere(knot, 0.02, m, segs=10, scale=(1.3, 0.8, 0.9)))
    for sx in (-1, 1):
        pts = G.bezier(knot, knot + Vector((sx * 0.02, 0.03, -0.04)), knot + Vector((sx * 0.04, 0.04, -0.09)), n=6)
        parts.append(G.sweep(pts, [(0.02, 0.003), (0.024, 0.003), (0.022, 0.003), (0.016, 0.003), (0.01, 0.003), (0.0, 0.002), (0, 0.001)], m, segs=6, side=Vector((1, 0, 0))))
    if has(text, "bun"):
        parts.append(G.sphere(Vector((X.cx, X.back + 0.01, X.eye + 0.04)), 0.035, m, segs=12))
    if has(text, "gold"):
        pass
    return X.add(G.join(parts), "Head", piece="kerchief")


def wimple(X, text):
    m = X.cloth_of(text, "#e9dfc6")
    zs = [X.base - 0.07, X.base - 0.02, X.eye - 0.02, X.brow, X.top - 0.02]
    offs = [0.05, 0.03, 0.016, 0.014, 0.014]
    parts = [shell(X, zs, offs, m, a0=FRONT + 0.85, a1=FRONT + TAU - 0.85, top_off=0.02, thick=0.006)]
    radii = X.head_ring(X.brow)
    parts.append(G.band(radii, X.metal_of("gold"), X.cx, X.cy, X.brow - 0.006, 0.012, 0.003, off=0.016))
    return X.add(G.join(parts), "Head", piece="wimple")


# ---------- helms ----------

def helm(X, text):
    mt = X.mat(G.colour_in(text, X.pal.get("metal") or "#9aa1aa"), "metal")
    if has(text, "gilded", "gold"): mt = X.mat("#d8b04a", "metal")
    if has(text, "brass"): mt = X.mat("#b98a3c", "metal")
    if has(text, "moss"): mt = X.mat("#5a6a4a", "metal")
    parts = []
    piece = "helm"
    if has(text, "diving"):
        c = Vector((X.cx, X.cy, (X.top + X.base) / 2 + 0.01)); R = max(X.hw, X.top - c.z) + 0.04
        parts.append(G.sphere(c, R, mt, segs=24))
        wc = Vector((X.cx, c.y - R * 0.92, X.eye))
        parts.append(G.lathe([(0.0, 0.0), (0.06, 0.0), (0.068, 0.012), (0.06, 0.016), (0, 0.016)], X.mat("#1a2a34", "glass"), center=wc, segs=24, M=G.frame((0, -1, 0))))
        ring = [wc + Vector((math.cos(j / 30 * TAU) * 0.064, -0.012, math.sin(j / 30 * TAU) * 0.064)) for j in range(31)]
        parts.append(G.sweep(ring, 0.008, mt, segs=8, cap=False))
        for k in range(8):
            a = k / 8 * TAU; parts.append(G.sphere(wc + Vector((math.cos(a) * 0.075, -0.016, math.sin(a) * 0.075)), 0.006, mt, segs=8))
        collar = [Vector((X.cx + math.cos(j / 32 * TAU) * R * 0.85, X.cy + math.sin(j / 32 * TAU) * R * 0.8, c.z - R * 0.55)) for j in range(33)]
        parts.append(G.sweep(collar, 0.018, mt, segs=10, cap=False))
        if has(text, "coral"):
            for k in range(6):
                p = c + Vector((math.cos(k) * 0.05, math.sin(k) * 0.05, R * 0.95))
                parts.append(G.sweep(G.bezier(p, p + Vector((0, 0, 0.04)), p + Vector((0.02 * math.cos(k), 0.02 * math.sin(k), 0.07)), n=5), G.taper(6, 0.009, 0.003), X.mat("#e2725e", "horn"), segs=6))
        return X.add(G.join(parts), "Head", piece="helm:diving")
    if has(text, "bucket", "great helm"):
        zb = X.base - 0.02; zt = X.top + 0.02
        rx, ry = X.fit.ellipse(X.cx, X.cy, X.eye, fallback=X.hw, reach=0.45)
        prof = [(1.08, 0), (1.1, (zt - zb) * 0.85), (0.85, (zt - zb)), (0, zt - zb + 0.01)]
        parts.append(G.lathe(prof, mt, center=(X.cx, X.cy, zb), rx=rx + 0.02, ry=ry + 0.02, segs=28))
        parts.append(G.box(Vector((X.cx, X.cy - ry * 1.12 - 0.02, X.eye)), (0.12, 0.012, 0.012), X.mat("#0a0a0c", "cloth"), bevel=0.0))
        for k in range(14):
            a = X.rng.uniform(0, TAU); z = zb + X.rng.uniform(0.0, zt - zb)
            p = Vector((X.cx + math.cos(a) * (rx + 0.035), X.cy + math.sin(a) * (ry + 0.035), z))
            parts.append(G.lathe([(0, 0), (0.008, 0), (0.004, 0.008), (0, 0.009)], X.mat("#d8d0bc", "stone"), center=p, segs=6, M=G.frame((math.cos(a), math.sin(a), 0))))
        return X.add(G.join(parts), "Head", piece="helm:great")
    zb = X.brow - 0.018
    pointed = has(text, "bascinet")
    tipped = has(text, "pot", "tipped")
    # a smooth forged dome sized to the head at the brow (a fitted shell follows every lump of hair)
    rx, ry = X.fit.ellipse(X.cx, X.cy, zb + 0.01, fallback=X.hw, reach=0.45)
    rx += 0.016 + (0.012 if tipped else 0); ry += 0.016 + (0.012 if tipped else 0)
    H = X.top - zb + 0.022 + (0.01 if tipped else 0)
    if pointed: prof = [(1.0, 0.0), (1.02, H * 0.35), (0.92, H * 0.7), (0.55, H * 1.02), (0.0, H * 1.25)]
    else: prof = [(1.0, 0.0)] + [(1.02 * math.cos(t * math.pi / 2), H * math.sin(t * math.pi / 2)) for t in [i / 8 for i in range(1, 9)]]
    prof = prof + [(0.0, prof[-1][1] - 0.008)] if False else prof
    o = G.lathe(prof, mt, center=(X.cx, X.cy + 0.004, zb), rx=rx, ry=ry, segs=36)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.008; md.offset = 1.0; bpy.ops.object.modifier_apply(modifier="S")
    if tipped:
        R = Matrix.Rotation(-0.25, 4, "X"); piv = Vector((X.cx, X.back, zb))
        for v in o.data.vertices: v.co = piv + (R @ (v.co - piv))
    parts.append(o)
    radii = X.head_ring(zb + 0.006)
    if has(text, "kettle") or has(text, "wide-brim"):
        rx, ry = _hat_fit(X, zb + 0.004)
        parts.append(G.lathe([(1.03, 0.004), (1 + 0.07 / rx, -0.014), (1 + 0.07 / rx, -0.02), (1.03, -0.004)], mt, center=(X.cx, X.cy, zb + 0.004), rx=rx, ry=ry, segs=36))
        piece = "helm:kettle"
    else:
        parts.append(G.band(radii, mt, X.cx, X.cy, zb - 0.006, 0.016, 0.006, off=0.014))
    if has(text, "rivet", "pot"):
        for k in range(16):
            a = k / 16 * TAU; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.022)
            parts.append(G.sphere(Vector((p.x, p.y, zb + 0.002)), 0.0045, mt, segs=6))
    if has(text, "nasal"):
        p, d = G.ring_point(radii, X.cx, X.cy, FRONT, 0.018)
        parts.append(G.box(Vector((p.x, p.y, zb - 0.03)), (0.012, 0.006, 0.065), mt, bevel=0.002))
    if has(text, "close", "commander", "visor", "jaw"):
        for sx in (-1, 1):
            a = FRONT + sx * 0.95; p, d = G.ring_point(radii, X.cx, X.cy, a, 0.016)
            parts.append(G.extrude([(-0.03, 0.0), (0.03, 0.0), (0.024, -0.07), (-0.01, -0.085), (-0.03, -0.05)], 0.006, mt, Vector((p.x, p.y, zb)), Vector((-math.sin(a), math.cos(a), 0)), Vector((0, 0, 1))))
    if has(text, "visor"):
        parts.append(G.lathe([(1.0, 0.0), (1.04, 0.0), (1.04, 0.022), (1.0, 0.022)], mt, center=(X.cx, X.cy, X.eye - 0.006), rx=X.hw + 0.02, ry=(X.cy - X.front) + 0.02, segs=20, a0=FRONT - 1.1, a1=FRONT + 1.1))
    if has(text, "plume", "horsehair"):
        pm = X.mat(G.colour_in(text[text.find("plume") - 18:] if "plume" in text else text, "#ece6da"))
        root = Vector((X.cx, X.cy + 0.01, X.top + 0.03))
        pts = G.bezier(root, root + Vector((0, 0.02, 0.12 if has(text, "tall") else 0.07)), root + Vector((0, 0.14, 0.1)), root + Vector((0, 0.24, -0.12)), n=14)
        parts.append(G.sweep(pts, [(0.012 + 0.03 * math.sin(math.pi * i / 14), 0.01 + 0.012 * math.sin(math.pi * i / 14)) for i in range(15)], pm, segs=10, side=Vector((1, 0, 0))))
        piece += "+plume"
    if has(text, "crest") and has(text, "fire", "flame"):
        # living fire: a row of flame tongues front to back along the crest, tallest in the middle, leaning back
        for i in range(9):
            t = i / 8; y = X.front + 0.02 + (X.back - X.front + 0.03) * t
            z = X.fit.surface(Vector((X.cx, y, X.top + 0.3)), (0, 0, -1)); z = (z.z if z is not None else X.top) + 0.012
            h = 0.06 + 0.07 * math.sin(math.pi * (0.15 + 0.7 * t)); w = 0.022
            ol = [(-w, 0), (w, 0), (w * 0.7, h * 0.35), (w * 1.0, h * 0.6), (w * 0.25, h * 0.55), (w * 0.35, h), (-w * 0.2, h * 0.62), (-w * 0.75, h * 0.78), (-w * 0.6, h * 0.35)]
            lean = Vector((0, 0.45, 1)).normalized()
            parts.append(G.extrude(ol, 0.01 - 0.004 * abs(t - 0.5), X.glow, Vector((X.cx + 0.004 * (i % 2 - 0.5), y, z)), Vector((0, 1, 0)), lean))
        piece += "+fire crest"
    elif has(text, "sun-crest", "sun crest", "radiating"):
        for k in range(9):
            a = math.pi * (k + 0.5) / 9; d = Vector((0, -math.cos(a), math.sin(a)))
            parts.append(G.extrude([(-0.01, 0), (0.01, 0), (0, 0.07 if k % 2 == 0 else 0.045)], 0.005, X.metal_of("gold"), Vector((X.cx, X.cy, X.top + 0.0)) + d * 0.03, Vector((1, 0, 0)), d))
        piece += "+sun crest"
    elif has(text, "crest") and not has(text, "halo-ring", "halo ring"):
        # a shaped ridge over the dome, front to back, tallest over the crown and tapering at both ends
        # (it was a flat accent-coloured quad that read as a placeholder block)
        dome_top = zb + H
        pts = []
        for i in range(13):
            t = i / 12; ang = math.pi * (0.12 + 0.76 * t)      # along the dome's centre line, brow to nape
            y = X.cy + 0.004 - math.cos(ang) * (ry + 0.004); z = zb + math.sin(ang) * (H + 0.006)
            pts.append(Vector((X.cx, y, z)))
        prof = [(0.005, 0.004 + 0.03 * math.sin(math.pi * i / 12) ** 0.7) for i in range(13)]
        parts.append(G.sweep(pts, prof, X.mat(X.pal.get("accent", "#b0302a"), "metal"), segs=8, side=Vector((1, 0, 0))))
    if has(text, "halo-ring", "halo ring"):
        # a halo above and just behind the helm, tipped back, kept clear of the shell by clear_hover
        dome_top = zb + H + 0.008
        c = Vector((X.cx, X.cy + 0.045, dome_top + 0.04)); Rh = X.hw * 1.12; tilt = math.tan(math.radians(20))
        def T(v): return v + Vector((0, 0, (v.y - c.y) * tilt))
        ring = [T(c + Vector((math.cos(j / 48 * TAU) * Rh, math.sin(j / 48 * TAU) * Rh * 0.95, 0))) for j in range(49)]
        hp = [G.sweep(ring, (0.009, 0.006), X.glow, segs=8, side=Vector((0, 0, 1)), cap=False)]
        inner = [T(c + Vector((math.cos(j / 48 * TAU) * Rh * 0.86, math.sin(j / 48 * TAU) * Rh * 0.82, 0.002))) for j in range(49)]
        hp.append(G.sweep(inner, 0.0035, X.metal_of("gold"), segs=6, cap=False))
        X.hover.append(X.add(G.join(hp), "Head", piece="halo:helm ring"))
    if has(text, "beam", "light"):
        parts.append(G.lathe([(0, 0), (0.02, 0), (0.012, 0.12), (0, 0.13)], X.glow, center=(X.cx, X.cy, X.top + 0.04), segs=10))
    if has(text, "wing"):
        fm = X.mat("#ece6da") if not has(text, "storm") else X.mat("#8aa0b8", "metal")
        for sx in (-1, 1):
            for k in range(5):
                parts.append(feather(Vector((X.cx + sx * (X.hw + 0.012), X.cy + 0.01, X.eye + 0.02)), (sx * 0.5, 0.35 + 0.1 * k, 0.4 + 0.15 * k), 0.1 - 0.008 * k, 0.016, fm, side=(0, 0, 1)))
        piece += "+wings"
    if has(text, "lantern"):
        p = Vector((X.cx + X.hw * 0.9, X.front + 0.02, X.base + 0.02))
        parts.append(lantern(X, p, 0.022))
    if has(text, "goggle", "lens"):
        parts.append(goggles(X, text, on_eyes=True, strap=False))
    if has(text, "leafy", "leaf"):
        for k in range(5):
            p = Vector((X.cx, X.cy - 0.03 + k * 0.025, X.top + 0.02))
            parts.append(G.extrude([(0, 0), (0.012, 0.02), (0, 0.05), (-0.012, 0.02)], 0.003, X.mat("#4f8a3a"), p, Vector((1, 0, 0)), Vector((0, 0.3, 1))))
    return X.add(G.join(parts), "Head", piece=piece)


def lantern(X, p, s, name="Kit_Head"):
    mt = X.metal_of("iron")
    parts = [G.lathe([(0, 0), (s, 0), (s, 0.004), (0.8 * s, 0.006), (0.8 * s, 1.6 * s), (s * 1.05, 1.65 * s), (0.4 * s, 2.2 * s), (0, 2.3 * s)], mt, center=p, segs=8)]
    parts.append(G.lathe([(0, 0.2 * s), (0.6 * s, 0.4 * s), (0.6 * s, 1.4 * s), (0, 1.6 * s)], X.glow, center=p, segs=8))
    return G.join(parts, name)


# ---------- hoods, veils, masks, goggles ----------

def hood(X, text):
    m = X.cloth_of(text, X.pal.get("main"))
    if has(text, "frog"): m = X.mat("#6a9a3a")
    if has(text, "fox"): m = X.mat(G.colour_in(text, "#c8662a"))
    if has(text, "leaf"): m = X.mat("#4f7a34")
    if has(text, "frost", "ice"): m = X.mat("#cfe0f0")
    if has(text, "wave", "water"): m = X.mat("#2a7ab8", "glass")
    if has(text, "straw"): m = X.mat("#c9ad5a", "leather")
    zs = [X.base - 0.1, X.base - 0.065, X.base - 0.03, X.base, X.eye - 0.03, X.eye, X.eye + 0.025, X.brow + 0.01, X.brow + 0.5 * (X.top - X.brow), X.top - 0.015]
    offs = [0.075, 0.058, 0.042, 0.03, 0.023, 0.021, 0.02, 0.02, 0.021, 0.022]
    gap = 0.95 if not has(text, "deep") else 0.75
    parts = [shell(X, zs, offs, m, a0=FRONT + gap, a1=FRONT + TAU - gap, top_off=0.035 if not has(text, "pointed", "deep") else 0.07, thick=0.008)]
    piece = "hood"
    if has(text, "pointed", "deep"):
        o = parts[0]
        for v in o.data.vertices:
            h = v.co.z - X.top
            if h > 0: v.co.y += h * 1.2
    if has(text, "frog"):
        for sx in (-1, 1):
            parts.append(G.sphere(Vector((X.cx + sx * 0.035, X.cy - 0.02, X.top + 0.02)), 0.022, m, segs=12))
            parts.append(G.sphere(Vector((X.cx + sx * 0.035, X.cy - 0.038, X.top + 0.026)), 0.01, X.mat("#1a1a10", "glass"), segs=8))
        piece = "hood:frog"
    if has(text, "fox"):
        parts.append(animal_ears(X, "fox", m, X.mat("#f0e0c8"), z_off=0.03))
        piece = "hood:fox"
    if has(text, "frost", "bead"):
        for k in range(11):
            a = FRONT + gap + 0.02 + k * (TAU - 2 * gap - 0.04) / 10 if False else FRONT - gap + k * (2 * gap) / 10
            r = X.fit.radius(X.cx, X.cy, X.brow, a, X.hw, 0.45) + 0.03
            p = Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, X.brow + 0.005))
        for k in range(9):
            a = FRONT + gap + k * 0.12
            for sgn in (1, -1):
                aa = FRONT + sgn * (gap + 0.02); zz = X.top - 0.02 - k * 0.022
                r = X.fit.radius(X.cx, X.cy, zz, aa, X.hw, 0.45) + 0.024
                p = Vector((X.cx + math.cos(aa) * r, X.cy + math.sin(aa) * r, zz))
                parts.append(G.lathe([(0, 0), (0.004, -0.004), (0.004, -0.01), (0, -0.016)], X.mat("#dff2ff", "glass"), center=p, segs=6))
        piece = "hood:frost"
    if has(text, "leaf"):
        for k in range(12):
            a = FRONT + gap + k * (TAU - 2 * gap) / 11; zz = X.base - 0.07
            r = X.fit.radius(X.cx, X.cy, X.base - 0.03, a, X.hw, 0.45) + 0.07
            p = Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, zz))
            parts.append(G.extrude([(0, 0), (0.016, -0.02), (0, -0.05), (-0.016, -0.02)], 0.003, X.mat("#3f6a2a"), p, Vector((-math.sin(a), math.cos(a), 0)), Vector((0, 0, 1))))
        piece = "hood:leaf"
    if has(text, "beak"):
        parts.append(beak(X, "plague", X.mat("#1e1b22", "leather")))
        piece += "+beak mask"
    return X.add(G.join(parts), "Head", piece=piece)


def veil(X, text, back=True, hood=False):
    """A veil: a lower-face half-veil (fitted), or a real drape: a thin cloth sheet pinned over the crown of
    the head that falls behind and beside the face onto the hair, neck and shoulders, shaped by Blender's
    cloth simulation against the dressed body (like kit_cloth's gowns), then skinned head -> neck -> spine."""
    base_c = G.colour_in(text, None)
    if base_c is None:
        mc = P.hexc(X.pal.get("main", "#c8c0d0")); base_c = "#%02x%02x%02x" % tuple(int(255 * min(1, (c ** (1 / 2.2)) * 0.35 + 0.62)) for c in mc[:3])
    if has(text, "black", "eclipse"): base_c = "#1e1b22"
    if has(text, "frost", "ice"): base_c = "#d8ecfa"
    vm = X.mat(base_c)
    if not back:
        zs = [X.base + 0.005, X.base + 0.035, X.eye - 0.045, X.eye - 0.028]
        o = shell(X, zs, [0.03, 0.022, 0.014, 0.012], vm, a0=FRONT - 1.25, a1=FRONT + 1.25, close_top=False, thick=0.003)
        return X.add(o, "Head", piece="veil:lower face")
    import kit_cloth as KC
    gap = 0.95 if not hood else 0.82                      # half-angle of the face opening
    L = (0.42 if has(text, "long", "train") else 0.3)
    cols, rows = 36, 16
    angs = [FRONT + gap + (TAU - 2 * gap) * i / cols for i in range(cols + 1)]
    pin_z = [X.top - 0.006, X.top - 0.022, X.top - 0.042, X.brow + 0.35 * (X.top - X.brow)]
    rings = []
    prev = None
    for z in pin_z:                                        # the pinned cap, fitted to the crown
        row = []
        for k, a in enumerate(angs):
            r = X.fit.radius(X.cx, X.cy, z, a, (prev[k] if prev else X.hw * 0.5), 0.45)
            if prev and r < prev[k] * 0.5: r = prev[k]
            row.append(r)
        prev = row
        rings.append([Vector((X.cx + math.cos(a) * (r + 0.012), X.cy + math.sin(a) * (r + 0.012), z)) for a, r in zip(angs, row)])
    z0 = pin_z[-1]; r0 = prev
    for k in range(1, rows + 1):                           # the free drape, started flared out so it falls onto the body
        t = k / rows; z = z0 - L * t
        row = []
        for i, a in enumerate(angs):
            behind = max(0.0, math.sin(a))                 # +Y is behind the head
            ex = 0.03 + (0.05 + 0.13 * behind) * t
            row.append(Vector((X.cx + math.cos(a) * (r0[i] + 0.012 + ex), X.cy + math.sin(a) * (r0[i] + 0.012 + ex * (1.0 + 0.4 * behind)), z)))
        rings.append(row)
    verts = [p for r in rings for p in r]; n = cols + 1; faces = []
    for r in range(len(rings) - 1):
        for i in range(cols):
            a_, b_ = r * n + i, r * n + i + 1
            faces.append((a_, b_, b_ + n, a_ + n))
    apex = len(verts); verts.append(Vector((X.cx, X.cy, X.top + 0.006)))
    for i in range(cols): faces.append((i + 1, i, apex))
    me = bpy.data.meshes.new("Kit_Head"); me.from_pydata(verts, [], faces); me.update()
    o = bpy.data.objects.new("Kit_Head", me); bpy.context.scene.collection.objects.link(o); o.data.materials.append(vm)
    pin = o.vertex_groups.new(name="pin")
    pin.add([v.index for v in o.data.vertices if v.co.z > z0 - 0.002], 1.0, "REPLACE")
    colliders = KC._body_meshes()
    for b in colliders:
        if not any(m.type == "COLLISION" for m in b.modifiers): b.modifiers.new("SBCollisionV", "COLLISION")
        b.collision.thickness_outer = 0.006; b.collision.cloth_friction = 8
    cl = o.modifiers.new("Cloth", "CLOTH"); st = cl.settings
    st.quality = 6; st.mass = 0.12; st.air_damping = 2.5
    st.tension_stiffness = st.compression_stiffness = 12; st.shear_stiffness = 5; st.bending_stiffness = 0.15
    st.vertex_group_mass = "pin"; st.pin_stiffness = 1.0
    cl.collision_settings.distance_min = 0.005; cl.collision_settings.collision_quality = 3
    sc = bpy.context.scene; frames = 40
    cl.point_cache.frame_start = 1; cl.point_cache.frame_end = frames
    for f in range(1, frames + 1): sc.frame_set(f)
    KC._select(o); bpy.ops.object.modifier_apply(modifier="Cloth"); sc.frame_set(1)
    for b in colliders:
        for m in [m for m in b.modifiers if m.name == "SBCollisionV"]: b.modifiers.remove(m)
    o.vertex_groups.remove(o.vertex_groups["pin"])
    # a soft hem: the lowest rows in a lighter tint of the veil (it reads thinner toward its edge)
    hem_m = X.mat("#%02x%02x%02x" % tuple(min(255, int(int(base_c[k:k + 2], 16) * 0.55 + 255 * 0.45)) for k in (1, 3, 5)))
    o.data.materials.append(hem_m); zmin = min(v.co.z for v in o.data.vertices)
    for p_ in o.data.polygons:
        if p_.center.z < zmin + 0.05: p_.material_index = 1
    KC._select(o); md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.0015; md.offset = 1.0; bpy.ops.object.modifier_apply(modifier="S")
    for p_ in o.data.polygons: p_.use_smooth = True
    parts = [o]
    if has(text, "star", "fleck"):
        for k in range(16):
            v = o.data.vertices[X.rng.randrange(len(o.data.vertices))]
            if v.co.z < X.eye: parts.append(G.sphere(v.co.copy() + (v.normal * 0.004), 0.0045, X.glow, segs=6))
    if has(text, "bead", "ice"):
        for k in range(12):                                # ice beads hanging along the front edges
            for sgn in (1, -1):
                pass
        edge = [v.co.copy() for v in o.data.vertices if abs(((math.atan2(v.co.y - X.cy, v.co.x - X.cx) - FRONT + math.pi) % TAU - math.pi)) < gap + 0.12 and v.co.z < X.brow]
        edge.sort(key=lambda p: -p.z)
        for p in edge[::max(1, len(edge) // 10)][:10]:
            parts.append(G.lathe([(0, 0), (0.004, -0.005), (0.004, -0.011), (0, -0.018)], X.mat("#e6f6ff", "glass"), center=p, segs=6))
    neck_z = X.base - 0.02
    def w(p):
        if p.z > X.base: return {"Head": 1.0}
        t = min(1.0, (X.base - p.z) / 0.14)
        return {"Head": 1 - t, "neck_01": t * 0.5, "spine_03": t * 0.5}
    return X.add(G.join(parts), w, piece="veil (cloth-draped)" + (" hood" if hood else ""))


def mask(X, text):
    m = X.mat(G.colour_in(text, "#f2efe8"))
    if has(text, "grass", "straw"): m = X.mat("#b9a463", "leather")
    if has(text, "bone", "cloth"): m = X.mat("#2a2630")
    half = has(text, "half")
    zs = ([X.eye - 0.05, X.eye - 0.025, X.eye + 0.005, X.brow - 0.006] if half else [X.base + 0.02, X.eye - 0.05, X.eye - 0.01, X.eye + 0.02, X.brow + 0.012])
    a0, a1 = FRONT - 1.15, FRONT + 1.15
    if has(text, "cracked"): a1 = FRONT + 0.35
    # a face-shaped oval: each row spans the oval's width at its height (narrow chin, wide cheekbones)
    z_lo, z_hi = zs[0], zs[-1]; zc = (z_lo + z_hi) / 2; zh = (z_hi - z_lo) / 2
    rows_, cols_ = 14, 18; wa = 1.0 if half else 0.95
    bm = bmesh.new(); grid = []
    for i in range(rows_ + 1):
        v = -1 + 2 * i / rows_; z = zc + v * zh
        w_ = math.sqrt(max(0.0, 1 - v * v)) ** 0.75
        if v < 0 and not half: w_ *= 0.62 + 0.38 * (1 + v)
        w_ = max(w_, 0.06)
        row = []
        for j in range(cols_ + 1):
            u = -1 + 2 * j / cols_
            if has(text, "cracked"): u = -1 + (2 * j / cols_) * 0.62
            a = FRONT + u * wa * w_
            r = X.face.radius(X.cx, X.cy, z, a, X.hw, 0.45) + 0.007
            row.append(bm.verts.new((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, z)))
        grid.append(row)
    for i in range(rows_):
        for j in range(cols_): bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
    o = G._obj("Kit_Head", bm, m)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.005; md.offset = 1.0; bpy.ops.object.modifier_apply(modifier="S")
    parts = [o]
    dark = X.mat("#0c0a10", "cloth")
    for sx in (-1, 1):
        a = FRONT + sx * 0.38
        if a > a1: continue
        r = X.face.radius(X.cx, X.cy, X.eye, a, X.hw, 0.45) + 0.016
        p = Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, X.eye))
        parts.append(G.sphere(p, 0.012, dark, segs=10, scale=(1.3, 0.35, 0.7), M=G.frame_xz((-math.sin(a), math.cos(a), 0), (0, 0, 1))))
    if has(text, "bone"):
        for k in range(7):
            a = FRONT - 0.6 + k * 0.2; z = X.eye - 0.04 - 0.01 * (k % 2)
            r = X.face.radius(X.cx, X.cy, z, a, X.hw, 0.45) + 0.02
            parts.append(G.sphere(Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, z)), 0.007, X.mat("#e0d4b6", "horn"), segs=8, scale=(1, 1, 1.6)))
    if has(text, "crack"):
        a = FRONT + 0.1; r = X.face.radius(X.cx, X.cy, X.eye, a, X.hw, 0.45) + 0.019
        p = Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, X.eye + 0.01))
        parts.append(G.extrude([(0, 0.03), (0.004, 0.012), (-0.003, 0.0), (0.004, -0.018), (0.001, -0.02), (-0.007, 0.0), (0.0, 0.012), (-0.003, 0.03)], 0.002, dark, p, Vector((1, 0, 0)), Vector((0, 0, 1))))
    return X.add(G.join(parts), "Head", piece="mask" + (":half" if half else ""))


def goggles(X, text, on_eyes=None, strap=True):
    up = not on_eyes and has(text, "pushed", "flight", "cap", "goggles") and not has(text, "helm")
    if on_eyes is None: on_eyes = not up
    br = X.metal_of("brass")
    z = X.eye + 0.004 if on_eyes else X.brow + 0.022
    parts = []
    spect = has(text, "spectacle") and not has(text, "goggle")
    for sx in (-1, 1):
        a = FRONT + sx * 0.36
        r = X.fit.radius(X.cx, X.cy, z, a, X.hw, 0.45)
        p = Vector((X.cx + math.cos(a) * (r + 0.012), X.cy + math.sin(a) * r - 0.008, z))
        if spect:
            ring = [p + Vector((math.cos(j / 20 * TAU) * 0.016, 0, math.sin(j / 20 * TAU) * 0.016)) for j in range(21)]
            parts.append(G.sweep(ring, 0.0018, br, segs=6, cap=False))
            parts.append(G.extrude(circle_outline(0.015, 16), 0.001, X.mat("#cfe6ff", "glass"), p, Vector((1, 0, 0)), Vector((0, 0, 1))))
            tip = Vector((X.cx + sx * (X.hw + 0.004), X.cy + 0.03, z + 0.004))
            parts.append(G.sweep([p + Vector((sx * 0.016, 0, 0)), tip], 0.0015, br, segs=5))
            if has(text, "flip", "extra"):
                parts.append(G.extrude(circle_outline(0.01, 12), 0.002, X.mat("#7ab8e8", "glass"), p + Vector((sx * 0.012, -0.006, 0.022)), Vector((1, 0, 0)), Vector((0, -0.4, 1))))
        else:
            parts.append(G.lathe([(0.0, 0.0), (0.019, 0.0), (0.021, 0.004), (0.021, 0.014), (0.017, 0.016), (0.0, 0.016)], br, center=p + Vector((0, 0.004, 0)), segs=16, M=G.frame((0, -1, 0))))
            parts.append(G.extrude(circle_outline(0.015, 16), 0.003, X.mat("#3a5a6a", "glass"), p + Vector((0, -0.013, 0)), Vector((1, 0, 0)), Vector((0, 0, 1))))
    if spect:
        parts.append(G.sweep(G.bezier(Vector((X.cx - 0.016, X.front - 0.006, z)), Vector((X.cx, X.front - 0.01, z + 0.006)), Vector((X.cx + 0.016, X.front - 0.006, z)), n=4), 0.0016, br, segs=5))
    elif strap:
        radii = X.head_ring(z)
        parts.append(G.band(radii, X.mat("#3a2a1a", "leather"), X.cx, X.cy, z - 0.008, 0.014, 0.003, off=0.003))
    return G.join(parts, "Kit_Head")


def flowers_in_hair(X, text):
    pm = X.mat(G.colour_in(text, "#8a4ad8" if has(text, "night") else "#f0a8c8")); core = X.mat("#f2d36a")
    parts = []
    for k, a in enumerate((0.25, 0.75, 1.3, 2.4)):
        z = X.brow + 0.02 + 0.015 * (k % 2)
        r = X.fit.radius(X.cx, X.cy, z, a, X.hw, 0.45) + 0.004
        d = Vector((math.cos(a), math.sin(a), 0.4)).normalized()
        parts.append(flower(Vector((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, z)), d, 0.018, pm, core))
    return X.add(G.join(parts), "Head", piece="flowers in hair")


def plume_on_top(X, text):
    root = Vector((X.cx, X.cy + 0.01, X.top + 0.005))
    return X.add(feather(root, (0, 0.35, 1), 0.16, 0.022, X.mat(G.colour_in(text, X.pal.get("accent", "#d8b04a"))), side=(1, 0, 0)), "Head", piece="plume")


# ---------- species features ----------

def leaf_ear(X, kind, mat):
    parts = []
    for sx in (-1, 1):
        z = X.eye - 0.012
        r = X.fit.radius(X.cx, X.cy + 0.01, z, 0 if sx > 0 else math.pi, X.hw, 0.45)
        root = Vector((X.cx + sx * (r - 0.012), X.cy + 0.008, z))
        if kind == "gnome": d, L, W = Vector((sx, 0.25, 0.12)), 0.12, 0.024
        elif kind == "fae": d, L, W = Vector((sx * 0.55, 0.55, 0.62)), 0.08, 0.018
        else: d, L, W = Vector((sx * 0.72, 0.38, 0.55)), 0.09, 0.022
        d.normalize(); n = 9
        pts = [root + d * (L * i / (n - 1)) + Vector((0, 0.012 * math.sin(math.pi * i / (n - 1)), 0)) for i in range(n)]
        prof = [W * w for w in (0.9, 1.0, 1.0, 0.92, 0.8, 0.62, 0.45, 0.25, 0.0)]
        parts.append(G.sweep(pts, [(p, max(0.003, p * 0.22)) for p in prof], mat, segs=10, side=Vector((0, 0.3, 1))))
    return parts


def animal_ears(X, kind, mat, inner_mat, z_off=0.0):
    parts = []
    for sx in (-1, 1):
        if kind in ("bear", "lion"):
            p = Vector((X.cx + sx * X.hw * 0.72, X.cy + 0.01, X.top - 0.025 + z_off))
            parts.append(G.sphere(p, 0.03, mat, segs=12, scale=(1.0, 0.45, 0.9)))
            parts.append(G.sphere(p + Vector((0, -0.01, -0.002)), 0.019, inner_mat, segs=10, scale=(1.0, 0.3, 0.85)))
            continue
        hare = kind in ("hare", "rabbit")
        L = 0.2 if hare else 0.09 if kind == "fox" else 0.08 if kind == "wolf" else 0.07
        root = Vector((X.cx + sx * X.hw * (0.45 if hare else 0.58), X.cy + 0.01, X.top - 0.03 + z_off))
        d = Vector((sx * (0.15 if hare else 0.35), 0.06, 1)).normalized()
        n = 9
        pts = [root + d * (L * i / (n - 1)) for i in range(n)]
        if hare and sx > 0:   # one ear bent forward
            for i in range(5, n): pts[i] = pts[4] + (Vector((sx * 0.3, -0.6, 0.6)).normalized()) * (L * (i - 4) / (n - 1))
        W = 0.035 if hare else 0.04
        prof = [W * w for w in ((0.8, 1.0, 1.0, 0.95, 0.85, 0.7, 0.5, 0.3, 0.0) if hare else (1.0, 0.95, 0.85, 0.72, 0.58, 0.42, 0.28, 0.14, 0.0))]
        side = Vector((1, 0, 0))
        parts.append(G.sweep(pts, [(p, max(0.004, p * 0.3)) for p in prof], mat, segs=10, side=side))
        ipts = [p + Vector((0, -0.007, 0.004)) for p in pts[:-1]]
        parts.append(G.sweep(ipts, [(p * 0.62, max(0.002, p * 0.12)) for p in prof[:-1]], inner_mat, segs=8, side=side))
    return G.join(parts, "Kit_Species")


def face_plate(X, mat, z_lo, z_hi, half_w=1.05, out=0.006):
    """A feathered or furred cover over the middle of the face (cheeks to brow), fitted to the bare head,
    so a beak or muzzle grows from it instead of sitting on a human nose."""
    rows_, cols_ = 10, 16; bm = bmesh.new(); grid = []
    for i in range(rows_ + 1):
        v = i / rows_; z = z_lo + (z_hi - z_lo) * v
        w_ = half_w * (0.75 + 0.25 * math.sin(math.pi * v))
        row = []
        for j in range(cols_ + 1):
            a = FRONT + (-1 + 2 * j / cols_) * w_
            r = X.face.radius(X.cx, X.cy, z, a, X.hw, 0.45) + out
            row.append(bm.verts.new((X.cx + math.cos(a) * r, X.cy + math.sin(a) * r, z)))
        grid.append(row)
    for i in range(rows_):
        for j in range(cols_): bm.faces.new((grid[i][j], grid[i][j + 1], grid[i + 1][j + 1], grid[i + 1][j]))
    o = G._obj("Kit_Species", bm, mat)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.004; md.offset = 1.0; bpy.ops.object.modifier_apply(modifier="S")
    return o


def beak2(X, kind, mat, plate_mat):
    """Polish pass: a beak with a broad base that grows out of a feathered face plate (raven: long, hooked;
    owl: short and hooked down), so it reads as a bird's face, not a nose."""
    root = Vector((X.cx, X.face.radius(X.cx, X.cy, X.eye - 0.02, FRONT, X.hw, 0.45) * -1 + X.cy + 0.004, X.eye - 0.02))
    plate = face_plate(X, plate_mat, X.eye - 0.07, X.brow + 0.01)
    if kind == "owl":
        pts = G.bezier(root + Vector((0, 0.012, 0.004)), root + Vector((0, -0.022, -0.004)), root + Vector((0, -0.03, -0.04)), n=9)
        b = G.sweep(pts, [(r * 1.25, r * 0.9) for r in G.taper(10, 0.026, 0.002)], mat, segs=12, side=Vector((0, 0, 1)))
    else:
        pts = G.bezier(root + Vector((0, 0.014, 0.006)), root + Vector((0, -0.05, -0.002)), root + Vector((0, -0.085, -0.035)), n=11)
        b = G.sweep(pts, [(r * 1.3, r * 0.85) for r in G.taper(12, 0.03, 0.0015)], mat, segs=12, side=Vector((0, 0, 1)))
    return G.join([plate, b], "Kit_Species")


def muzzle2(X, mat, nose_mat, plate_mat):
    """Polish pass: a bear or lion muzzle: a furred face plate over cheeks and mouth with a broad snout
    pad sunk into it and a nose at the tip, so it blends into the face."""
    zc = X.eye - 0.05
    plate = face_plate(X, plate_mat, X.base + 0.01, X.eye - 0.005, half_w=1.2, out=0.005)
    fr = X.face.radius(X.cx, X.cy, zc, FRONT, X.hw, 0.45)
    c = Vector((X.cx, X.cy - fr - 0.004, zc))
    snout = G.sphere(c, 0.04, mat, segs=16, scale=(1.25, 0.95, 0.72))
    nose = G.sphere(c + Vector((0, -0.036, 0.012)), 0.014, nose_mat, segs=12, scale=(1.35, 0.85, 0.8))
    return G.join([plate, snout, nose], "Kit_Species")


def beak(X, kind, mat):
    root = Vector((X.cx, X.front + 0.012, X.eye - 0.032))
    if kind == "plague":
        pts = G.bezier(root + Vector((0, 0.01, 0)), root + Vector((0, -0.08, -0.01)), root + Vector((0, -0.17, -0.06)), n=10)
        return G.sweep(pts, G.taper(11, 0.03, 0.002), mat, segs=12)
    if kind == "owl":
        pts = G.bezier(root, root + Vector((0, -0.03, -0.004)), root + Vector((0, -0.035, -0.035)), n=8)
        return G.sweep(pts, [(r * 1.0, r * 0.8) for r in G.taper(9, 0.016, 0.002)], mat, segs=10, side=Vector((0, 0, 1)))
    if kind == "turtle":
        pts = G.bezier(root + Vector((0, 0.01, -0.01)), root + Vector((0, -0.03, -0.012)), root + Vector((0, -0.045, -0.03)), n=8)
        return G.sweep(pts, [(r * 1.4, r * 0.9) for r in G.taper(9, 0.022, 0.004)], mat, segs=10, side=Vector((1, 0, 0)))
    pts = G.bezier(root, root + Vector((0, -0.06, 0.0)), root + Vector((0, -0.1, -0.03)), n=10)
    return G.sweep(pts, [(r * 1.2, r * 0.8) for r in G.taper(11, 0.022, 0.0015)], mat, segs=10, side=Vector((0, 0, 1)))


def muzzle(X, mat, nose_mat):
    c = Vector((X.cx, X.front + 0.008, X.eye - 0.045))
    return G.join([G.sphere(c, 0.042, mat, segs=14, scale=(1.05, 0.85, 0.75)), G.sphere(c + Vector((0, -0.034, 0.012)), 0.013, nose_mat, segs=10, scale=(1.3, 0.9, 0.8))], "Kit_Species")


def mane(X, mat):
    """A lion's mane: layered locks framing the face, swept back and down from the hairline and jaw."""
    parts = []
    for ring, (zf, L, W) in enumerate(((-0.1, 0.16, 0.05), (0.2, 0.17, 0.052), (0.5, 0.15, 0.05), (0.78, 0.12, 0.045), (0.95, 0.09, 0.04))):
        z = X.base - 0.02 + (X.top - X.base) * zf
        n = 11
        for k in range(n):
            a = FRONT + 1.2 + (TAU - 2.4) * (k + 0.5 * (ring % 2)) / (n - 1)
            r = X.fit.radius(X.cx, X.cy, max(z, X.base), a, X.hw, 0.45)
            d = Vector((math.cos(a), math.sin(a), 0))
            root = Vector((X.cx, X.cy, z)) + d * (r - 0.015)
            dirv = (d * 0.45 + Vector((0, 0.55, -0.45 - 0.35 * (1 - max(0, zf))))).normalized()
            pts = G.bezier(root, root + dirv * L * 0.45 + d * 0.025, root + dirv * L, n=7)
            parts.append(G.sweep(pts, [(W * w, W * 0.4 * w) for w in (0.9, 1.0, 1.0, 0.9, 0.7, 0.45, 0.2, 0.0)], mat, segs=8, side=Vector((-d.y, d.x, 0))))
    return G.join(parts, "Kit_Species")


def antennae(X, text, mat):
    parts = []
    feathery = has(text, "feather", "moth")
    for sx in (-1, 1):
        root = Vector((X.cx + sx * 0.022, X.front + 0.03, X.brow + 0.012))
        pts = G.bezier(root, root + Vector((sx * 0.02, -0.02, 0.07)), root + Vector((sx * 0.08, 0.02, 0.12)), root + Vector((sx * 0.11, 0.07, 0.1)), n=14)
        if feathery:
            parts.append(G.sweep(pts, [(0.012 * math.sin(math.pi * (0.15 + 0.85 * i / 14)), 0.0015) for i in range(15)], mat, segs=6, side=Vector((sx * 0.3, 1, 0))))
        else:
            parts.append(G.sweep(pts, 0.0025, mat, segs=6))
            parts.append(G.sphere(pts[-1], 0.007, mat, segs=8))
    return G.join(parts, "Kit_Head")


def tail(X, kind, mat, tip_mat=None):
    pz, _ = X.U.rest_bone(X.arm, "pelvis")
    hit = X.fit.surface(Vector((pz.x, pz.y + 0.5, pz.z - 0.04)), (0, -1, 0))
    root = Vector((pz.x, (hit.y if hit else pz.y + 0.12) - 0.01, pz.z - 0.04))
    if kind in ("stub", "cotton"):
        return G.sphere(root + Vector((0, 0.02, 0)), 0.04 if kind == "cotton" else 0.035, mat, segs=12, scale=(1, 1, 0.9) if kind == "cotton" else (0.8, 1.4, 0.8))
    if kind == "fox":
        pts = G.bezier(root, root + Vector((0, 0.14, -0.14)), root + Vector((0.04, 0.32, -0.26)), root + Vector((0.07, 0.44, -0.14)), n=16)
        rad = [0.03, 0.045, 0.06, 0.072, 0.08, 0.085, 0.088, 0.088, 0.085, 0.08, 0.072, 0.064, 0.055, 0.045, 0.032, 0.018, 0.0]
        a = G.sweep(pts[:12], rad[:12], mat, segs=12, cap=False)
        b = G.sweep(pts[11:], rad[11:], tip_mat or mat, segs=12)
        return G.join([a, b], "Kit_Species")
    if kind == "wolf":
        pts = G.bezier(root, root + Vector((0, 0.12, -0.12)), root + Vector((0.02, 0.22, -0.3)), n=12)
        return G.sweep(pts, [0.03, 0.045, 0.058, 0.064, 0.066, 0.064, 0.058, 0.05, 0.04, 0.03, 0.018, 0.008, 0.0], mat, segs=12)
    if kind == "lion":
        pts = G.bezier(root, root + Vector((0, 0.18, -0.3)), root + Vector((0.06, 0.36, -0.28)), root + Vector((0.09, 0.42, -0.08)), n=16)
        return G.join([G.sweep(pts, G.taper(17, 0.022, 0.012), mat, segs=10), G.sphere(pts[-1], 0.03, tip_mat or mat, segs=10, scale=(1, 1, 1.3))], "Kit_Species")
    if kind == "lizard":
        pts = G.bezier(root + Vector((0, 0, 0.02)), root + Vector((0, 0.15, -0.3)), root + Vector((0.02, 0.35, -0.6)), root + Vector((0.06, 0.6, -0.68)), n=16)
        return G.sweep(pts, [(r, r * 0.85) for r in G.taper(17, 0.075, 0.006, 0.9)], mat, segs=12, side=Vector((1, 0, 0)), ridge=0.05, ridges=9)
    if kind == "shark":
        pts = G.bezier(root, root + Vector((0, 0.14, -0.2)), root + Vector((0, 0.3, -0.42)), n=12)
        o = G.sweep(pts, G.taper(13, 0.06, 0.012), mat, segs=12)
        f = G.extrude([(0, 0), (0.1, 0.12), (0.07, 0.0), (0.12, -0.1)], 0.01, mat, pts[-1], Vector((0, 0.6, -0.8)), Vector((0, 0.8, 0.6)))
        return G.join([o, f], "Kit_Species")
    # cat: long, slim, S-curve
    pts = G.bezier(root, root + Vector((0, 0.15, -0.28)), root + Vector((0.05, 0.36, -0.22)), root + Vector((0.08, 0.42, 0.06)), n=18)
    return G.sweep(pts, G.taper(19, 0.022, 0.01), mat, segs=10)


def carapace(X, mat, plate_mat, big=False):
    s3, _ = X.U.rest_bone(X.arm, "spine_03"); s1, _ = X.U.rest_bone(X.arm, "spine_01")
    zc = (s3.z + s1.z) / 2
    hit = X.fit.surface(Vector((s3.x, s3.y + 0.6, zc)), (0, -1, 0))
    yb = hit.y if hit else s3.y + 0.12
    c = Vector((s3.x, yb - 0.02, zc))
    rx, rz, h = (0.3, 0.36, 0.14) if big else (0.24, 0.28, 0.1)
    M = Matrix(((1, 0, 0), (0, 0, 1), (0, 1, 0))).transposed() @ Matrix.Diagonal((1, 1, 1))
    M = Matrix((Vector((1, 0, 0)), Vector((0, 0, -1)), Vector((0, 1, 0)))).transposed()
    prof = [(1.06, -0.01), (1.0, 0.0)] + [(math.cos(t * math.pi / 2) * 0.98, h * math.sin(t * math.pi / 2)) for t in [i / 8 for i in range(1, 9)]]
    dome = G.lathe(prof, mat, center=c, rx=rx, ry=rz, segs=32, M=M)
    parts = [dome]
    def on_dome(u, v):     # u, v in -1..1 across the dome
        rr = min(0.95, math.hypot(u, v)); z = h * math.sqrt(max(0.0, 1 - rr * rr))
        p = c + Vector((u * rx, z, v * rz))
        nrm = Vector((u / rx * 0.6, 1.0, v / rz * 0.6)).normalized()
        return p, nrm
    for (u, v, s) in [(0, 0, 0.09)] + [(0.55 * math.cos(k / 6 * TAU), 0.55 * math.sin(k / 6 * TAU), 0.075) for k in range(6)]:
        p, nrm = on_dome(u, v)
        hexo = [(s * math.cos(i / 6 * TAU + math.pi / 6), s * math.sin(i / 6 * TAU + math.pi / 6)) for i in range(6)]
        U_ = Vector((1, 0, 0)); U_ = (U_ - nrm * U_.dot(nrm)).normalized(); V_ = nrm.cross(U_)
        parts.append(G.extrude(hexo, 0.014, plate_mat, p + nrm * 0.003, U_, V_, bevel=0.003))
    return G.join(parts, "Kit_Species")


def fin(X, where, mat):
    if where == "head":
        p = Vector((X.cx, X.cy + 0.01, X.top - 0.01))
        return G.extrude([(-0.04, 0), (0.07, 0), (0.05, 0.03), (0.0, 0.1), (-0.02, 0.05)], 0.008, mat, p, Vector((0, 1, 0)), Vector((0, 0, 1)), bevel=0.002)
    s3, _ = X.U.rest_bone(X.arm, "spine_03")
    hit = X.fit.surface(Vector((s3.x, s3.y + 0.6, s3.z)), (0, -1, 0))
    p = Vector((s3.x, (hit.y if hit else s3.y + 0.12) - 0.01, s3.z - 0.02))
    return G.extrude([(-0.08, 0), (0.12, -0.04), (0.06, 0.06), (-0.04, 0.22), (-0.06, 0.08)], 0.016, mat, p, Vector((0, 1, 0)), Vector((0, 0, 1)), bevel=0.003)


def fin_ears(X, mat):
    parts = []
    for sx in (-1, 1):
        z = X.eye - 0.01; r = X.fit.radius(X.cx, X.cy, z, 0 if sx > 0 else math.pi, X.hw, 0.45)
        p = Vector((X.cx + sx * (r - 0.006), X.cy + 0.01, z))
        ol = [(0, -0.015), (0.03, -0.02), (0.07, 0.02), (0.05, 0.03), (0.06, 0.06), (0.02, 0.03), (0, 0.02)]
        parts.append(G.extrude(ol, 0.004, mat, p, Vector((sx * 0.4, 1, 0)).normalized() * 1, Vector((0, 0.2, 1)).normalized()))
    return G.join(parts, "Kit_Head")


def ear_tufts(X, mat):
    parts = []
    for sx in (-1, 1):
        for k in range(3):
            root = Vector((X.cx + sx * X.hw * 0.6, X.cy + 0.01 * k, X.top - 0.02))
            parts.append(feather(root, (sx * (0.35 + 0.1 * k), 0.1, 1), 0.07 - 0.012 * k, 0.012, mat, side=(0, 1, 0), curl=0.15))
    return G.join(parts, "Kit_Species")


def crest_feathers(X, mat):
    parts = []
    for k in range(5):
        root = Vector((X.cx, X.cy + 0.01 + 0.015 * k, X.top - 0.008))
        parts.append(feather(root, (0, 0.6 + 0.15 * k, 0.8 - 0.1 * k), 0.08 - 0.006 * k, 0.012, mat, side=(1, 0, 0)))
    return G.join(parts, "Kit_Species")


def tail_fan(X, mat):
    pz, _ = X.U.rest_bone(X.arm, "pelvis")
    hit = X.fit.surface(Vector((pz.x, pz.y + 0.5, pz.z - 0.02)), (0, -1, 0))
    root = Vector((pz.x, (hit.y if hit else pz.y + 0.12) - 0.01, pz.z - 0.02))
    parts = []
    for k in range(7):
        a = (k - 3) * 0.22
        parts.append(feather(root, (math.sin(a) * 0.7, 0.5, -0.8), 0.2, 0.025, mat, side=(math.cos(a), 0, math.sin(a)), curl=0.1))
    return G.join(parts, "Kit_Species")


def golem_armour(X, mats):
    """Stone armour for golems: carved pauldrons, a breastplate and back slab, forearm and thigh
    plates (each a bevelled block with a raised carved panel). The Titan keeps its glowing chest
    runes and visor slit (the game tints the glow with its current element)."""
    sp = X.species; arm = X.arm; U = X.U
    titan = "titan" in sp
    # the design's own stone (a single grey-beige for every golem read as unpainted blocks)
    o_ = X.c.get("outfit", {}) or {}
    st = (" ".join(str(o_.get(k, "")) for k in ("arms", "body", "legs")) + " " + X.acc_txt).lower()
    base_hex = ("#4e4a44" if titan else "#b8956a" if "sandstone" in st else "#6c6862" if "fieldstone" in st else "#3e3a38" if has(st, "basalt", "obsidian") else
                "#8a5a44" if has(st, "hearthstone", "brick", "clay") else "#7a746a")
    stone = X.mat(base_hex, "stone")
    dk = [max(0, int(base_hex[i:i + 2], 16) - 34) for i in (1, 3, 5)]
    dark = X.mat("#%02x%02x%02x" % tuple(dk), "stone")
    band = X.metal_of(st, "#a0703a") if has(st, "band", "bronze", "iron") else None
    def boulders(bone, center, size, M):
        """A pauldron of three faceted boulders over a darker mortar stone (a flat box read as a placeholder)."""
        M = M or Matrix.Identity(3); c = Vector(center); parts = []
        parts.append(G.sphere(c - M @ Vector((0, 0, size[2] * 0.25)), max(size[0], size[1]) * 0.42, dark, segs=7, scale=(1.0, 0.95, 0.55)))
        for k, (dx, dy, r) in enumerate(((-0.28, -0.18, 0.36), (0.22, -0.1, 0.33), (0.0, 0.26, 0.34))):
            j = 1.0 + (X.rng.random() - 0.5) * 0.18
            p = c + M @ Vector((dx * size[0], dy * size[1], size[2] * 0.15))
            parts.append(G.sphere(p, max(size[0], size[1]) * r * j, stone, segs=6 + k % 2, scale=(1.0, 0.9, 0.62)))
        X.add(G.join(parts), bone, tag="Kit_Species")
    def slab(bone, center, size, M=None, carve=True):
        o = G.box(center, size, stone, bevel=0.012, M=M)
        X.add(o, bone, tag="Kit_Species")
        if carve:
            M_ = M or Matrix.Identity(3)
            nrm = M_ @ Vector((0, -1, 0)) if size[1] < size[0] and size[1] < size[2] else M_ @ Vector((0, 0, 1))
            inner = G.box(Vector(center) + nrm * (min(size) / 2 + 0.004), (size[0] * 0.7 if size[1] < size[2] else size[0] * 0.7, max(0.008, min(size) * 0.3), size[2] * 0.6 if size[1] < size[2] else size[2]), dark, bevel=0.005, M=M)
            X.add(inner, bone, tag="Kit_Species")
    for sx, side in ((1, "l"), (-1, "r")):
        ua, ut = U.rest_bone(arm, "upperarm_" + side)
        Mz = Matrix.Rotation(math.radians(-16 * sx), 3, "Y")
        if titan:
            slab("upperarm_" + side, ua + Vector((0.05 * sx, 0, 0.11)), (0.22, 0.22, 0.07), Mz, carve=False)
            slab("upperarm_" + side, ua + Vector((0.09 * sx, 0, 0.065)), (0.16, 0.2, 0.05), Mz, carve=False)
        else:
            boulders("upperarm_" + side, ua + Vector((0.06 * sx, 0, 0.09)), (0.22, 0.22, 0.09), Mz)
        la, lt = U.rest_bone(arm, "lowerarm_" + side)
        mid = (la + lt) / 2
        L = abs(lt.x - la.x) * 0.7
        slab("lowerarm_" + side, mid + Vector((0, 0, 0.055)), (L, 0.11, 0.04), carve=False)
        if band is not None and not titan:
            for f in (-0.3, 0.3):   # bronze / iron bands across the forearm plate
                X.add(G.box(mid + Vector((f * L, 0, 0.055)), (0.018, 0.122, 0.052), band, bevel=0.003), "lowerarm_" + side, tag="Kit_Species")
        th, tt = U.rest_bone(arm, "thigh_" + side)
        tm = th + (tt - th) * 0.4
        hit = X.fit.surface(Vector((tm.x, tm.y - 0.5, tm.z)), (0, 1, 0))
        fy = (hit.y if hit else tm.y - 0.08) - 0.02
        slab("thigh_" + side, Vector((tm.x, fy, tm.z)), (0.13, 0.04, 0.2))
    s2, _ = U.rest_bone(arm, "spine_03"); nk, _ = U.rest_bone(arm, "neck_01"); s2 = Vector((s2.x, s2.y, nk.z - 0.2))
    bvh = X.fit.bvh; cz = s2.z
    fr = bvh.ray_cast(Vector((s2.x, s2.y - 1.0, cz)), Vector((0, 1, 0)), 2.0)[0]
    bk = bvh.ray_cast(Vector((s2.x, s2.y + 1.0, cz)), Vector((0, -1, 0)), 2.0)[0]
    fy = (fr.y if fr is not None else s2.y - 0.12) - 0.03; by = (bk.y if bk is not None else s2.y + 0.12) + 0.035
    if not titan: slab("spine_03", Vector((s2.x, fy, cz)), (0.3, 0.05, 0.26), Matrix.Rotation(math.radians(-6), 3, "X"))
    else: fy = (fr.y if fr is not None else s2.y - 0.12) + 0.022
    slab("spine_03", Vector((s2.x, by, cz + 0.02)), (0.32, 0.06, 0.3), Matrix.Rotation(math.radians(8), 3, "X"))
    REPORT.append((X.c["id"], "golem armour" + (" (titan)" if titan else "")))
    if titan:
        glow = mats["glow"]
        for k, (dx, dz, w_, h_) in enumerate(((0, 0.07, 0.16, 0.018), (0, -0.04, 0.12, 0.018), (-0.05, 0.015, 0.018, 0.1), (0.05, 0.015, 0.018, 0.1))):
            X.add(G.box(Vector((s2.x + dx, fy - 0.027, cz + dz)), (w_, 0.01, h_), glow, bevel=0.0), "spine_03", tag="Kit_Species")
        hb = X.hb
        eye = hb["base"] + (hb["top"] - hb["base"]) * 0.47
        vis = bvh.ray_cast(Vector((hb["cx"], hb["front"] - 0.5, eye)), Vector((0, 1, 0)), 1.0)[0]
        vy = (vis.y if vis is not None else hb["front"]) - 0.004
        X.add(G.box(Vector((hb["cx"], vy, eye)), (hb["hw"] * 1.1, 0.012, 0.016), glow, bevel=0.0), "Head", tag="Kit_Species")


def wraith_body(X, mats):
    pz, _ = X.U.rest_bone(X.arm, "pelvis")
    m = X.mat(X.pal.get("main", "#3a3448")); g = mats["glow"]
    seed = X.rng.random() * 10
    prof = [(0.14, 0.06), (0.15, 0.0), (0.18, -0.2), (0.23, -0.45), (0.28, -0.7), (0.3, -0.88)]
    def zf(a, r, z):
        if z < -0.85: return -0.16 * abs(math.sin(a * 7 + seed)) ** 2.5 - 0.05 * abs(math.sin(a * 13 + seed * 2))
        return 0.0
    o = G.lathe(prof, m, center=(pz.x, pz.y, pz.z), rx=1.0, ry=0.9, segs=40, zfun=zf)
    for x in bpy.context.selected_objects: x.select_set(False)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    md = o.modifiers.new("S", "SOLIDIFY"); md.thickness = 0.008; bpy.ops.object.modifier_apply(modifier="S")
    parts = [o]
    for k in range(5):
        a = k / 5 * TAU + seed; d = Vector((math.cos(a), math.sin(a), 0))
        p = Vector((pz.x, pz.y, pz.z - 0.7)) + d * 0.26
        pts = G.bezier(p, p + d * 0.06 + Vector((0, 0.05, -0.15)), p + d * 0.1 + Vector((0, 0.12, -0.22)), n=8)
        parts.append(G.sweep(pts, [(0.02 * (1 - i / 8), 0.002) for i in range(9)], g, segs=5, side=Vector((-d.y, d.x, 0))))
    REPORT.append((X.c["id"], "wraith lower body"))
    return X.add(G.join(parts), {"pelvis": 0.6, "thigh_l": 0.2, "thigh_r": 0.2}, tag="Kit_Species")


# ---------- entry points ----------

PACK_HEAD = ("Knight_Head_Armet", "Knight_Head_Horns", "Ranger_Head_Hood", "Noble_Head_Crown")


def head(c, arm, mats, U):
    """Every modelled head piece the design asks for (crowns, circlets, halos, horns, headwear)."""
    X = Ctx(c, arm, mats, U)
    h = X.head_txt; kitt = X.kit_txt
    o = c.get("outfit", {}) or {}
    pack = any(p in str(o.get("head", "")) for p in PACK_HEAD) and not has(h, "reshaped")
    allt = h + " | " + kitt
    done = set()
    def once(k):
        if k in done: return False
        done.add(k); return True
    # headwear (the design's head slot)
    animal_head = has(h, "turtle head", "shark head", "lion head", "raven head", "owl head", "bear head")
    if not pack and h and not h.startswith("none") and not animal_head:
        if has(h, "diving", "helm", "helmet", "bascinet", "great helm") and once("helm"): helm(X, h)
        elif has(h, "veil") and has(h, "hood") and once("veil"): veil(X, h, hood=True)
        elif has(h, "hood", "cowl") and once("hood"): hood(X, h)
        elif has(h, "wimple") and once("wimple"): wimple(X, h)
        elif has(h, "hat", "tricorne", "bicorne", "sou'wester") and not has(h, "hard cap") and once("hat"): hat(X, h)
        elif has(h, "cap") and once("cap"): cap(X, h)
        elif has(h, "kerchief", "headscarf", "scarf") and once("kerchief"): kerchief(X, h)
        elif has(h, "bandana", "headband") and once("headband"): headband(X, h)
    elif pack:
        if has(h, "wing", "plume", "lantern", "crest") and once("helm-extra"):
            # extras on the pack's own helm
            parts = []
            if has(h, "wing"):
                fm = X.mat("#8aa0b8", "metal") if has(h, "storm") else X.mat("#ece6da")
                for sx in (-1, 1):
                    for k in range(5):
                        parts.append(feather(Vector((X.cx + sx * (X.hw + 0.004), X.cy + 0.01, X.eye + 0.03)), (sx * 0.5, 0.35 + 0.1 * k, 0.4 + 0.15 * k), 0.11 - 0.008 * k, 0.017, fm, side=(0, 0, 1)))
            X.add(G.join(parts), "Head", piece="helm wings")
        if has(h, "flame") and has(h, "horn") and once("horns"):
            horns(X, h, "swept")
        if has(h, "thorn antler") and once("horns"):
            horns(X, "thorn antler", "antler")
        if has(h, "porcelain", "mask") and once("mask"):
            mask(X, h)
    # crowns, circlets, halos (head slot or kit)
    if has(allt, "crown") and not str(o.get("head", "")).startswith("Noble_Head_Crown") and not has(allt, "coral crown welded") and once("crown"):
        txt = h if has(h, "crown") else next((k for k in c.get("kit", []) if "crown" in str(k).lower()), allt)
        crown(X, str(txt).lower())
    if has(allt, "circlet", "diadem") and once("circlet"):
        txt = h if has(h, "circlet", "diadem") else next((str(k).lower() for k in c.get("kit", []) if "circlet" in str(k).lower()), allt)
        circlet(X, txt)
    if has(h, "headband") and once("headband"): headband(X, h)
    if (has(allt, "halo", "sun-disc", "eclipse") or (c.get("rarity") == "Legendary" and not has(allt, "crown", "helm", "hood"))) and not (has(h, "halo-ring", "halo ring") and has(h, "helm", "helmet")) and once("halo"):  # a helm's halo crest is the halo
        txt = h if has(h, "halo", "sun-disc", "eclipse") else next((str(k).lower() for k in c.get("kit", []) if any(w in str(k).lower() for w in ("halo", "sun-disc", "eclipse"))), allt)
        halo(X, txt)
    if has(allt, "horn", "antler") and not has(allt, "war horn", "horn slung", "hornbill") and once("horns"):
        txt = h if has(h, "horn", "antler") else next((str(k).lower() for k in c.get("kit", []) if "horn" in str(k).lower() or "antler" in str(k).lower()), allt)
        if not (pack and "Knight_Head_Horns" in str(o.get("head", "")) and not has(txt, "flame", "curl")):
            horns(X, txt)
    # extras anywhere in the head text
    if has(h, "veil") and once("veil"):
        if has(h, "half-veil", "lower face"): veil(X, h, back=False)
        else: veil(X, h)
    if has(h, "mask") and once("mask") and not has(h, "beak mask"): mask(X, h)
    if has(h, "goggle", "spectacle", "lens") and once("goggles"):
        X.add(goggles(X, h, on_eyes=has(h, "spectacle", "helm", "round-lens")), "Head", piece="goggles" if "goggle" in h else "spectacles")
    if has(h, "flower", "bloom") and not has(h, "crown") and once("flowers"): flowers_in_hair(X, h)
    if has(h, "antenna", "antennae") and once("antennae"): X.add(antennae(X, h, X.mat("#3a3040", "leather")), "Head", piece="antennae")
    if has(X.acc_txt, "antennae", "stamen") and once("antennae"): X.add(antennae(X, X.acc_txt, X.mat("#e8d870", "leather")), "Head", piece="stamen curls")
    if has(h, "cat ear", "cat-ear") and once("ears"):
        X.add(animal_ears(X, "cat", X.cloth_of(h, X.pal.get("main")), X.mat("#e8b8b8")), "Head", piece="cat ears")
    if has(h, "hare ear") and once("ears"):
        X.add(animal_ears(X, "hare", X.mat((c.get("hair") or {}).get("colour", "#d8c8b0")), X.mat("#e8c0c0")), "Head", piece="hare ears")
    if has(h, "fin-ear", "fin ear") and once("finears"): X.add(fin_ears(X, X.mat("#5ac8c0", "glass")), "Head", piece="fin ears")
    if has(h, "gnomon"): X.add(fin(X, "head", X.metal_of("bronze")), "Head", piece="gnomon fin")
    clear_hover(X)
    return X.out


def clear_hover(X, gap=0.015):
    """Lift each halo / floating crown so it sits `gap` above every other head piece under it (a hat,
    hood or helm built on the same hero); the head box only knows the bare head and hair."""
    if not X.hover: return
    R = X.hw * 1.5
    def under(o):
        mw = o.matrix_world
        return [mw @ v.co for v in o.data.vertices if math.hypot((mw @ v.co).x - X.cx, (mw @ v.co).y - X.cy) < R]
    others = [o for o in X.out if o not in X.hover and o.type == "MESH"]
    top = max((p.z for o in others for p in under(o)), default=None)
    if top is None: return
    for o in X.hover:
        pts = [o.matrix_world @ v.co for v in o.data.vertices]
        if not pts: continue
        low = min(p.z for p in pts)
        if low < top + gap:
            d = o.matrix_world.inverted().to_3x3() @ Vector((0, 0, top + gap - low))
            for v in o.data.vertices: v.co += d
            REPORT.append((X.c["id"], f"lifted {o.name} {top + gap - low:.3f} m clear of the headwear"))


def species(c, arm, mats, U):
    """Non-human features: ears, beaks, muzzles, manes, crests, tails, shells, fins, golem stone and the
    wraith's lower body, modelled and fitted to the head and body."""
    sp = str(c.get("species", "human")).lower()
    X = Ctx(c, arm, mats, U)
    accs = X.acc_txt; h = X.head_txt
    if sp == "human" and not has(accs, "elf ears", "gnome ears", "fae ears", "goat ears", "fin ears", "bear head", "tail", "carapace"): return []
    pal = X.pal
    skin = X.mat(c.get("skin") if str(c.get("skin", "")).startswith("#") else P.SKIN.get(c.get("skin"), "#c8915f"), "cloth")
    fur = X.mat((c.get("hair") or {}).get("colour", pal.get("secondary", "#8a6a4a")), "cloth")
    furtip = X.mat("#f2ece0")
    inner = X.mat("#e8b8b0")
    out_before = len(X.out)
    def say(p): REPORT.append((c["id"], p))
    # ears
    if any(k in sp for k in ("fae", "sylph", "sprite")) or has(accs, "fae ears"):
        X.add(G.join(leaf_ear(X, "fae", skin)), "Head", tag="Kit_Species", piece="fae ears")
    elif "gnome" in sp or has(accs, "gnome ears"):
        X.add(G.join(leaf_ear(X, "gnome", skin)), "Head", tag="Kit_Species", piece="gnome ears")
    elif any(k in sp for k in ("elf", "dryad")) or has(accs, "elf ears"):
        X.add(G.join(leaf_ear(X, "elf", skin)), "Head", tag="Kit_Species", piece="elf ears")
    helmed = has(h, "helm", "helmet", "hat", "cap")
    for k in ("fox", "cat", "wolf", "hare", "rabbit", "bear", "lion"):
        if k in sp and not has(h, "hood", "cat ear", "cat-ear", "hare ear"):
            X.add(animal_ears(X, k, fur, inner, z_off=0.03 if helmed else 0.0), "Head", tag="Kit_Species", piece=k + " ears"); break
    if "goat" in sp or has(accs, "goat ears"):
        parts = []
        for sx in (-1, 1):
            z = X.eye; r = X.fit.radius(X.cx, X.cy, z, 0 if sx > 0 else math.pi, X.hw, 0.45)
            root = Vector((X.cx + sx * (r - 0.01), X.cy + 0.01, z))
            pts = [root + Vector((sx, 0.15, -0.3)).normalized() * (0.08 * i / 7) for i in range(8)]
            parts.append(G.sweep(pts, [(0.022 * w, 0.005) for w in (0.7, 1.0, 1.0, 0.9, 0.7, 0.5, 0.3, 0.0)], fur, segs=8, side=Vector((0, 1, 0))))
        X.add(G.join(parts), "Head", tag="Kit_Species", piece="goat ears")
    if ("tidekin" in sp or "merfolk" in sp or has(accs, "fin ears")) and not has(h, "fin-ear"):
        if True: X.add(fin_ears(X, X.mat("#5ac8c0", "glass")), "Head", tag="Kit_Species", piece="fin ears")
    # faces
    if "raven" in sp: X.add(beak2(X, "raven", X.mat("#1e1b22", "horn"), X.mat("#2a2632")), "Head", tag="Kit_Species", piece="raven beak + face"); X.add(crest_feathers(X, X.mat("#2a2632")), "Head", tag="Kit_Species", piece="raven crest")
    if "owl" in sp: X.add(beak2(X, "owl", X.mat("#d8b04a", "horn"), fur), "Head", tag="Kit_Species", piece="owl beak + face"); X.add(ear_tufts(X, fur), "Head", tag="Kit_Species", piece="owl ear tufts")
    # Polish pass: bear and lion muzzles grow from a furred face plate (alone they read as a stuck-on nose)
    for k in ("bear", "lion"):
        if k in sp and not has(h, "mask", "helm"):
            X.add(muzzle2(X, fur, X.mat("#1e1712", "horn"), fur), "Head", tag="Kit_Species", piece=k + " muzzle + face"); break
    if "lion" in sp: X.add(mane(X, X.mat((c.get("hair") or {}).get("colour", "#9a6a2a"))), {"Head": 0.8, "neck_01": 0.2}, tag="Kit_Species", piece="lion mane")
    if has(accs, "bulbous", "nose"): X.add(G.sphere(Vector((X.cx, X.front - 0.004, X.eye - 0.028)), 0.018, skin, segs=12, scale=(1, 0.9, 0.85)), "Head", tag="Kit_Species", piece="bulbous nose")
    # horns from species (if the design text did not already give them)
    if any(k in sp for k in ("goat", "stag", "tree")) and not has(X.head_txt + " " + X.kit_txt, "horn", "antler"):
        twig = "stag" in sp or "tree" in sp
        horns(X, ("thorn antler" if "tree" in sp else "antler") if twig else "goat", "antler" if twig else "goat")
        for o in X.out[-1:]: o.name = "Kit_Species"
    # tails
    tk = None
    if "fox" in sp or has(accs, "bushy"): tk = "fox"
    elif "wolf" in sp: tk = "wolf"
    elif "lion" in sp or has(accs, "tufted"): tk = "lion"
    elif "lizard" in sp or "salamander" in sp or "drake" in sp: tk = "lizard"
    elif "shark" in sp: tk = "shark"
    elif "cat" in sp or has(accs, "cat tail"): tk = "cat"
    elif has(accs, "cotton tail") or "hare" in sp or "rabbit" in sp: tk = "cotton"
    elif has(accs, "stubby"): tk = "stub"
    if tk: X.add(tail(X, tk, fur if tk not in ("lizard", "shark") else skin, furtip if tk == "fox" else X.mat("#3a2a1a") if tk == "lion" else None), "pelvis", tag="Kit_Species", piece=tk + " tail")
    if has(accs, "tail-feather", "tail feather"): X.add(tail_fan(X, fur), "pelvis", tag="Kit_Species", piece="tail-feather fan")
    if has(accs, "tail fin"): X.add(fin(X, "back", X.mat("#2a8a86")), "spine_03", tag="Kit_Species", piece="tail fin")
    # backs
    if "turtle" in sp or has(accs, "carapace"):
        X.add(carapace(X, X.mat(pal.get("secondary", "#5a6a3a"), "horn"), X.mat(pal.get("main", "#7a8a4a"), "horn"), big=has(accs, "huge", "carapace")), "spine_03", tag="Kit_Species", piece="turtle shell")
    if "shark" in sp:
        X.add(fin(X, "back", skin), "spine_03", tag="Kit_Species", piece="shark dorsal fin")
        if has(h, "shark", "fin crest"): X.add(fin(X, "head", skin), "Head", tag="Kit_Species", piece="shark head fin")
    if "golem" in sp: golem_armour(X, mats)
    if "wraith" in sp or "spirit" in sp: wraith_body(X, mats)
    return X.out


def report():
    from collections import Counter
    cnt = Counter(p.split("+")[0] for _, p in REPORT)
    return cnt
