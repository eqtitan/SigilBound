"""Seat floating head gear on the head, in place (character TD headgear pass, 2026-11-28).

Owner: "almost every head accessory is either floating above the head model or floating". Cause: kit_head.py fits
hats, helms, crowns and circlets to the head top / head ring of the build-time head; hair shells were later changed per
hero, so rings now sit wider than the hair and hats a few cm above it.

Per hero (kit and image-to-3D):
1. Head pieces = connected pieces >= 50% on Head/jaw near the head. The head shell is the largest. Pieces that touch
   the shell (within 3 mm: hair at the hairline, brows, ears, masks, forehead circlets) are "seated".
2. The rest form the hero's head-gear set and move as one rigid set (a helm with its plume and horns). It is floating when under 30% of its
   lower rim is within 4 mm of the seated set (rays down and toward the skull centre).
3. If the largest gear piece is a thin ring (all round the head axis, < 5 cm tall: circlet, crown band) the set are shrunk about the head's vertical axis onto the hair (median of
   (R + 2 mm) / r, at least 0.85); then every floating group is lowered by its 15th-percentile vertical gap minus
   2 mm (at most 7 cm). Halos (Looks.Gear "halo", entirely > 20 cm above the Head joint) hover by design: skipped.
   Gear is bound 100% to Head so it can't slide relative to the skull.
    bpyenv/bin/python hg_seat.py SRC.glb DST.glb HERO [--dry]"""
import sys, os, re, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save, weld, components

LOOKS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "../../Assets/Sigilbound/Core/Looks.cs")

def gear_of(hid):
    m = re.search(r'\["%s"\] = new HeroLook \{([^}]*)\}' % hid, open(LOOKS).read())
    g = re.search(r'Gear = "(\w+)"', m.group(1)) if m else None
    return g.group(1) if g else None

def kd_of(P, vs):
    from mathutils.kdtree import KDTree
    kd = KDTree(len(vs))
    for i, v in enumerate(vs): kd.insert(P[v].tolist(), i)
    kd.balance(); return kd

def plan(h, P, hid):
    import bpy  # noqa: F401
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    F = h.F
    hj = [h.jnames.index(n) for n in ("Head", "jaw") if n in h.jnames]
    hw = sum((h.W * (h.J == j)).sum(1) for j in hj)
    G = h.globals_(h.rest); hp = G[h.names.index("Head")][:3, 3]
    comp = components(F, weld(P, 1e-4))
    pcs = []
    for lab in np.unique(comp):
        vs = np.unique(F[comp == lab])
        if len(vs) < 20 or hw[vs].mean() < 0.5 or np.linalg.norm(P[vs].mean(0) - hp - [0, 0.08, 0]) > 0.3: continue
        pcs.append((lab, vs))
    if len(pcs) < 2: return []
    shell_lab, shell = max(pcs, key=lambda x: len(x[1]))
    kds = kd_of(P, shell)
    seated, cand = [shell_lab], []
    for lab, vs in pcs:
        if lab == shell_lab: continue
        dmin = min(kds.find(P[v].tolist())[2] for v in vs[:: max(1, len(vs) // 400)])
        above = P[vs, 1].mean() > hp[1] + 0.10
        if dmin < 0.003 or not above or len(vs) > 0.2 * len(P): seated.append(lab)
        else: cand.append((lab, vs))
    if not cand: return []
    # one head-gear set per hero moves rigidly (a helm with its plume and horns, a hat with its band)
    allc = np.concatenate([vs for _, vs in cand]); main = max(cand, key=lambda x: len(x[1]))[1]
    groups = {0: [allc]}
    tgt = np.isin(comp, seated)
    tree = BVHTree.FromPolygons(P.tolist(), F[tgt].tolist(), all_triangles=True)
    ctr = hp + np.array([0, 0.09, 0.0]); gear = gear_of(hid)
    def vgaps(Q, vs):
        out = []
        for v in vs:
            best = None
            for d in (np.array([0, -1.0, 0]), (ctr - Q[v]) / max(np.linalg.norm(ctr - Q[v]), 1e-6)):
                hit = tree.ray_cast(Vector(Q[v] + d * 1e-4), Vector(d), 0.12)
                if hit[0] is not None:
                    dd = (max(Q[v][1] - hit[0][1], 0.0), hit[3])
                    best = dd if best is None else (min(best[0], dd[0]), min(best[1], dd[1]))
            out.append(best)
        return out
    moves = []
    for vlist in groups.values():
        vs = np.concatenate(vlist)
        low = vs[P[vs, 1] < np.percentile(P[vs, 1], 35)]; low = low[:: max(1, len(low) // 200)]
        g = [x for x in vgaps(P, low) if x is not None]
        if len(g) < 0.25 * max(1, len(low)): continue
        dist = np.array([x[1] for x in g])
        if (dist < 0.004).mean() >= 0.3 or np.median(dist) < 0.009: continue   # resting already (a ring within 9 mm reads as worn)
        Q = P.copy(); s = 1.0
        ang = np.degrees(np.arctan2(P[main, 0] - hp[0], P[main, 2] - hp[2]))
        if len(np.unique((ang // 30).astype(int))) >= 11 and np.ptp(P[main, 1]) < 0.05:   # a thin ring (circlet, crown band): shrink onto the hair first; hats and helms are only lowered
            ratios = []
            for v in low:
                y = P[v, 1]; rv = np.array([P[v, 0] - hp[0], 0, P[v, 2] - hp[2]]); r = np.linalg.norm(rv)
                if r < 1e-4: continue
                d = rv / r; o = np.array([hp[0], y, hp[2]]) + d * 0.3
                hit = tree.ray_cast(Vector(o), Vector(-d), 0.3)
                if hit[0] is not None: ratios.append((0.3 - hit[3] + 0.006) / r)   # 6 mm proud of the hair: tighter rings sank out of sight
            if ratios:
                s = float(np.clip(np.median(ratios), 0.85, 1.0))
                Q[vs, 0] = hp[0] + (P[vs, 0] - hp[0]) * s; Q[vs, 2] = hp[2] + (P[vs, 2] - hp[2]) * s
        g2 = [x for x in vgaps(Q, low) if x is not None]
        dy = float(np.percentile([x[0] for x in g2], 15)) - 0.002 if g2 else 0.0
        dy = min(max(dy, 0.0), 0.07)
        if s > 0.995: continue   # only worn rings are fitted: lowering hats/helms alone was invisible or put hair through them
        if P[vs, 1].min() > hp[1] + 0.2: continue   # a hovering halo
        moves.append((vs, s, dy, float(np.median(dist))))
    return moves, hp

def fix(src, dst, hid, dry=False):
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    if "rest_fit" in h.names: return []   # image-to-3D heads are one fused shell
    P, _ = h.skin(); r = plan(h, P, hid)
    if not r: return []
    mv, hp = r
    if not mv: return []
    V = h.V.copy(); Jt = h.J.copy(); W = h.W.copy(); hj = h.jnames.index("Head"); log = []
    for vs, s, dy, med in mv:
        V[vs, 0] = hp[0] + (V[vs, 0] - hp[0]) * s; V[vs, 2] = hp[2] + (V[vs, 2] - hp[2]) * s; V[vs, 1] -= dy
        Jt[vs] = [hj, 0, 0, 0]; W[vs] = [1.0, 0, 0, 0]
        log.append(f"{len(vs)} verts: scale {s:.2f}, down {dy * 100:.1f} cm (gap was {med * 100:.1f} cm)")
    if not dry:
        at = j["meshes"][0]["primitives"][0]["attributes"]
        write_acc(binb, j, at["POSITION"], V); write_acc(binb, j, at["JOINTS_0"], Jt); write_acc(binb, j, at["WEIGHTS_0"], W)
        save(dst, j, binb)
    return log

if __name__ == "__main__": print(fix(sys.argv[1], sys.argv[2], sys.argv[3], "--dry" in sys.argv))
