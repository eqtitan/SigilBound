"""Adds spring-bone chains for skirts, robes and coat tails to a shipped HD hero GLB, in place (no re-encode, so
nothing else about the model changes): a ring of chains hanging from the pelvis (sb_<k>_0..J, the last node is the
tip), and the cloth re-weighted to them. The game simulates them (Client/SpringBones.cs, Core/SpringSolver.cs) with
the thighs and calves as capsule colliders, so the cloth slides off the legs instead of splitting with them.

    bpyenv/bin/python spring_rig.py SRC.glb DST.glb HERO      (needs Blender's mathutils for the layer test)

Pilot result (2026-11-28, scratchpad ctd/): shipped for jasper only. Tried and not shipped: kaelith (kit gown:
the chains opened holes in the idle and let the legs out in Death01; fixed by clip overrides instead), wystan
(robe and sleeves carry hand weights; no frame got clearly better), vorgrim (cape weighted to the right upper arm,
tail to the right thigh; the cape/tail chains below tore from the arm-weighted top).
Roster batch 1: lazlo (coat; idle/jog much better but Spell_Simple_Enter, Hit_Kneel f0-6 and Death01 f28-70 worse,
with or without the leg re-skin), neve (big leg slabs gone, but dark specks of the legs show through the feathered
skirt in the idle; wider colliders tore the skirt) and holt (worse in every clip) - none shipped.

Cloth = vertices below the waist that are further than r_in from every leg bone (full spring weight beyond r_out),
not on the arms/hands/props. Each chain follows the median outline of the cloth in its sector; a vertex takes the
two neighbouring chains (angular blend) and the two nearest bones along them (tent weights), mixed with its old
weights by its cloth factor, top 4 kept."""
import sys, os, json, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save

# per hero: top = waist height above the pelvis joint (m), K chains, J bones per chain, leg distance band (m),
# skip = sector indices without a chain (open coat fronts), hem_min = lowest cloth y kept on the chains
CFG = {
    "wystan":  dict(top=0.02, K=10, J=3, r_in=0.035, r_out=0.07, r_leg=0.07),
    "vorgrim": dict(top=0.02, K=10, J=3, r_in=0.12, r_out=0.17, layers=False, legs=False,
                    cape=dict(src=["upperarm_r", "upperarm_l", "clavicle_r", "clavicle_l", "lowerarm_r"], half=0.4, K=5, J=3),
                    tail=dict(src=["thigh_r", "thigh_l", "calf_r", "calf_l"], J=4)),
    "jasper":  dict(top=0.00, K=10, J=3, r_in=0.08, r_out=0.14),
    "hask": dict(),
    "kaelith": dict(top=0.00, K=12, J=3, r_in=0.07, r_out=0.12, legs=False, layers=False, keep=0.4),
}
ARMISH = ("clavicle", "upperarm", "lowerarm", "hand", "index", "middle", "ring", "pinky", "thumb", "spine_02", "spine_03", "neck", "Head")

def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)

def seg_dist(P, a, b):
    ab = b - a; t = np.clip(((P - a) @ ab) / (ab @ ab), 0, 1); return np.linalg.norm(P - (a + t[:, None] * ab), axis=1)

def innermost(P0, F, pos, names, rmax):
    """Layer test: a vertex is on the leg's own surface if a ray from the nearest point on the nearer leg's bones
    out to it hits nothing before it (trousers, boots, skin). Anything with another surface between it and the
    bone (a robe, coat or skirt over the legs) is an outer layer."""
    import bpy  # noqa: F401  (makes Blender's mathutils importable)
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromPolygons(P0.tolist(), F.tolist(), all_triangles=True)
    best = np.full(len(P0), 9.0); cp = np.zeros_like(P0)
    for s in "lr":
        ch = [pos("thigh_" + s), pos("calf_" + s), pos("foot_" + s)]
        ch.append(pos("ball_" + s) if "ball_" + s in names else ch[-1] + [0, 0, 0.1])
        for a, b in zip(ch[:-1], ch[1:]):
            t = seg_t(P0, a, b); q = a + t[:, None] * (b - a); dd = np.linalg.norm(P0 - q, axis=1)
            m = dd < best; best[m] = dd[m]; cp[m] = q[m]
    inner = np.zeros(len(P0), bool)
    for i in np.nonzero(best < rmax)[0]:
        v = P0[i] - cp[i]; L = np.linalg.norm(v)
        if L < 1e-4: inner[i] = True; continue
        hit = tree.ray_cast(Vector(cp[i]), Vector(v / L), L - 0.004)
        inner[i] = hit[0] is None
    return inner

def mesh_smooth(a, F, it):
    for _ in range(it):
        acc = np.zeros_like(a); cnt = np.zeros_like(a)
        fa = a[F].mean(1)
        for k in range(3): np.add.at(acc, F[:, k], fa); np.add.at(cnt, F[:, k], 1)
        a = np.where(cnt > 0, acc / np.maximum(cnt, 1), a)
    return a

def leg_gap(P0, d, pos, c):
    """Per height band, the leg's own radius: the first empty 1 cm shell around the leg bones between 5 and 25 cm
    (trousers/boots inside, robe/coat outside). Bands without a gap use r_in."""
    ys = np.arange(P0[:, 1].min(), pos("pelvis")[1] + 0.1, 0.03)
    gaps = []
    for y in ys:
        sel = np.abs(P0[:, 1] - y) < 0.02
        g = c["r_in"]
        if sel.sum() > 10:
            hist, e = np.histogram(d[sel], bins=np.arange(0.0, 0.30, 0.01))
            for k in range(5, 25):
                if hist[k] == 0 and hist[:k].sum() > 0:
                    g = max(c["r_in"] * 0.6, e[k] - 0.005); break
        gaps.append(min(g, c.get("r_max", 0.2)))
    gaps = np.array(gaps)
    gs = np.array([np.median(gaps[max(0, i - 1):i + 2]) for i in range(len(gaps))])
    return np.interp(P0[:, 1], ys, gs)

def seg_t(P, a, b):
    ab = b - a; return np.clip(((P - a) @ ab) / (ab @ ab), 0, 1)

def leg_weights(P0, pos, jn, hip_blend=0.35, knee=0.05, ankle=0.04):
    """Clean leg skinning by projection: side = nearer leg chain; thigh/calf/foot by the nearest segment with
    smooth blends over +-knee / +-ankle metres around the joints and pelvis -> thigh over the top hip_blend of
    the thigh. Returns a list of {joint: w} per vertex (only meaningful near the legs)."""
    out = []
    side = {}
    for s in "lr":
        th, ca, fo = pos("thigh_" + s), pos("calf_" + s), pos("foot_" + s)
        ba = pos("ball_" + s) if "ball_" + s in jn else fo + [0, 0, 0.1]
        dd = np.minimum.reduce([seg_dist(P0, th, ca), seg_dist(P0, ca, fo), seg_dist(P0, fo, ba)])
        side[s] = (dd, th, ca, fo, ba)
    useL = side["l"][0] <= side["r"][0]
    W = np.zeros((len(P0), 5)); names = [None] * len(P0)
    res = {}
    for s in "lr":
        dd, th, ca, fo, ba = side[s]
        m = useL if s == "l" else ~useL
        P = P0[m]
        Lt, Lc = np.linalg.norm(ca - th), np.linalg.norm(fo - ca)
        # arc length along the chain of the nearest point
        dt_, dc_, df_ = seg_dist(P, th, ca), seg_dist(P, ca, fo), seg_dist(P, fo, ba)
        tt, tc, tf = seg_t(P, th, ca), seg_t(P, ca, fo), seg_t(P, fo, ba)
        arc = np.where((dt_ <= dc_) & (dt_ <= df_), tt * Lt, np.where(dc_ <= df_, Lt + tc * Lc, Lt + Lc + tf * np.linalg.norm(ba - fo)))
        wk = smooth(Lt - knee, Lt + knee, arc)           # thigh -> calf
        wa = smooth(Lt + Lc - ankle, Lt + Lc + ankle, arc)  # calf -> foot
        wp = 1 - smooth(0, hip_blend * Lt, arc)            # pelvis near the hip
        w = np.c_[wp, (1 - wp) * (1 - wk), (1 - wp) * wk * (1 - wa), (1 - wp) * wk * wa]
        res[s] = (np.nonzero(m)[0], w)
    return res

def rig(src, dst, hid, cfg=None):
    c = dict(CFG[hid]); c.update(cfg or {})
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    if any(n.startswith("sb_") for n in h.names): raise SystemExit("already rigged")
    P0, G0 = h.skin()
    pos = lambda nm: G0[h.names.index(nm)][:3, 3]
    pel = pos("pelvis"); ytop = pel[1] + c["top"]
    d = np.full(len(P0), 9.0)
    for s in "lr":
        d = np.minimum(d, seg_dist(P0, pos("thigh_" + s), pos("calf_" + s)))
        d = np.minimum(d, seg_dist(P0, pos("calf_" + s), pos("foot_" + s)))
    dfoot = np.full(len(P0), 9.0)
    for s in "lr":
        tip = pos("ball_" + s) if "ball_" + s in h.names else pos("foot_" + s) + [0, 0, 0.1]
        dfoot = np.minimum(dfoot, seg_dist(P0, pos("foot_" + s), tip))
    W = h.W.copy(); Jt = h.J.copy()
    armw = np.zeros(len(P0)); footw = np.zeros(len(P0))
    for ji, nm in enumerate(h.jnames):
        if nm.startswith(ARMISH): armw += (W * (Jt == ji)).sum(1)
        if nm.startswith(("foot_", "ball_")): footw += (W * (Jt == ji)).sum(1)
    # arms and props: near an arm bone in the rest pose, or rigidly on a hand (weapons). Bone-heat weights from the
    # mesh's own arms-down pose put hand/forearm weights on robes, so weights alone can't tell.
    darm = np.full(len(P0), 9.0); handw = np.zeros(len(P0))
    for s in "lr":
        ch = [pos(n) for n in ("upperarm_" + s, "lowerarm_" + s, "hand_" + s) if n in h.names]
        for a_, b_ in zip(ch[:-1], ch[1:]): darm = np.minimum(darm, seg_dist(P0, a_, b_))
    for ji, nm in enumerate(h.jnames):
        if nm.startswith(("hand", "index", "middle", "ring", "pinky", "thumb")): handw += (W * (Jt == ji)).sum(1)
    armlike = (darm < c.get("r_arm", 0.12)) | (handw > 0.9) if c.get("geo_arms", True) else (armw >= 0.05)
    if c.get("torso"):
        # robe/torso cloth carrying arm or hand weights (bone heat from an arms-down mesh pose): spine weights by height
        sp = [n for n in ("pelvis", "spine_01", "spine_02", "spine_03", "neck_01") if n in h.names]
        hy = np.array([pos(n)[1] for n in sp]); jx = [h.jnames.index(n) for n in sp]
        sel = (~armlike) & (armw > 0.05) & (P0[:, 1] > ytop - 0.05) & (P0[:, 1] < hy[-1])
        for i in np.nonzero(sel)[0]:
            y = P0[i, 1]; k = int(np.clip(np.searchsorted(hy, y) - 1, 0, len(hy) - 2)); t = float(np.clip((y - hy[k]) / (hy[k + 1] - hy[k]), 0, 1))
            Jt[i] = [jx[k], jx[k + 1], 0, 0]; W[i] = [1 - t, t, 0, 0]
        armw[sel] = 0
    if c.get("layers", True):
        inner = innermost(P0, h.F, pos, h.names, c.get("r_max", 0.22))
        a = np.where(inner & (d < c.get("r_leg", 0.18)), 0.0, 1.0)
        a = np.where(d < c["r_in"] * 0.6, 0.0, a)          # always leg right at the bone
        a = mesh_smooth(a, h.F, 2)
        rin = np.where(inner, d + 0.02, np.minimum(d, c["r_in"]))
    else:
        rin = np.full(len(P0), c["r_in"])
        a = smooth(rin, rin + (c["r_out"] - c["r_in"]), d)
    a = a * smooth(ytop + 0.02, ytop - 0.10, P0[:, 1]) * (~armlike)
    a *= (P0[:, 1] > c.get("hem_min", -1)) * (footw < 0.2) * smooth(c.get("foot_r", 0.08), c.get("foot_r", 0.08) + 0.04, dfoot)
    cloth = a > 0.5
    a = a * (1 - c.get("keep", 0.0))   # keep part of the old (leg-following) weights on the cloth
    legs_rule = c.get("legs", True)
    if legs_rule:
        # vertices near the legs below the waist, not arms/props: clean projection weights instead of the old ones
        lw = leg_weights(P0, pos, h.names)
        jix = {nm: h.jnames.index(nm) for nm in h.jnames}
        lb = smooth(ytop + 0.02, ytop - 0.12, P0[:, 1]) * (~armlike) * (1 - a) * ((d < c.get("r_max", 0.22)) | (footw > 0.2))
        for s_, (ids, w) in lw.items():
            names_ = ["pelvis", "thigh_" + s_, "calf_" + s_, "foot_" + s_]
            for ii, i in enumerate(ids):
                if lb[i] <= 0: continue
                ws = {}
                for q in range(4):
                    if W[i, q] > 0: ws[int(Jt[i, q])] = ws.get(int(Jt[i, q]), 0) + W[i, q] * (1 - lb[i])
                for nm, wv in zip(names_, w[ii]):
                    if wv > 0: ws[jix[nm]] = ws.get(jix[nm], 0) + wv * lb[i]
                top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s2 = sum(x for _, x in top)
                Jt[i] = [k for k, _ in top] + [0] * (4 - len(top)); W[i] = [x / s2 for _, x in top] + [0.0] * (4 - len(top))
    K, J = c["K"], c["J"]
    rel = P0 - pel; phi = np.arctan2(rel[:, 0], rel[:, 2]); rad = np.hypot(rel[:, 0], rel[:, 2])
    th = 2 * np.pi * np.arange(K) / K
    chains = {}
    for k in range(K):
        if k in c.get("skip", ()): continue
        dphi = np.angle(np.exp(1j * (phi - th[k])))
        sec = cloth & (np.abs(dphi) < np.pi / K * 1.2)
        if sec.sum() < 20: continue
        yh = np.percentile(P0[sec, 1], 2)
        ys = np.linspace(ytop, yh, J + 1)
        pts = []
        for y in ys:
            band = sec & (np.abs(P0[:, 1] - y) < max(0.04, (ytop - yh) / J / 2))
            r = np.median(rad[band]) if band.sum() > 3 else (np.median(rad[sec]))
            pts.append(pel * [1, 0, 1] + [0, y, 0] + r * np.array([math.sin(th[k]), 0, math.cos(th[k])]))
        chains[k] = (np.array(pts), yh)
    ks = sorted(chains)
    if len(ks) < 3: raise SystemExit("too few chains")
    # nodes
    nodes = j["nodes"]; pi = h.names.index("pelvis"); Gp = G0[pi]; Rp = Gp[:3, :3]; ip = np.linalg.inv(Gp)
    qp_inv = glbio.qinv(mat_rot(Rp))
    sk = j["skins"][0]; ibm_new = []; jidx = {}
    for k in ks:
        pts, _ = chains[k]; prev = None
        for s in range(J + 1):
            nm = f"sb_{k}_{s}"
            if s == 0: n = {"name": nm, "translation": list(map(float, (ip @ np.r_[pts[0], 1])[:3])), "rotation": list(map(float, qp_inv))}
            else: n = {"name": nm, "translation": list(map(float, pts[s] - pts[s - 1]))}
            nodes.append(n); ni = len(nodes) - 1
            if s == 0: nodes[pi].setdefault("children", []).append(ni)
            else: nodes[prev].setdefault("children", []).append(ni)
            prev = ni
            if s < J:
                sk["joints"].append(ni); jidx[(k, s)] = len(sk["joints"]) - 1
                M = np.eye(4); M[:3, 3] = pts[s]; ibm_new.append(np.linalg.inv(M))
    if len(sk["joints"]) > 255: raise SystemExit("too many joints for uint8")
    # weights
    nk = len(ks); thk = th[ks]
    idx = np.nonzero(a > 1e-3)[0]
    for i in idx:
        dp = np.angle(np.exp(1j * (phi[i] - thk)))
        # neighbours: nearest chain on each side
        pos_ = np.where(dp >= 0, dp, 9); neg = np.where(dp < 0, -dp, 9)
        k0 = int(np.argmin(pos_)); k1 = int(np.argmin(neg))
        g0, g1 = pos_[k0], neg[k1]
        if g0 + g1 > np.pi: # wide gap (skipped sectors): nearest only
            k0 = k1 = int(np.argmin(np.abs(dp))); t = 0.0
        else: t = g0 / max(g0 + g1, 1e-9)  # weight of k1
        ws = {}
        for kk, wk in ((ks[k0], 1 - t), (ks[k1], t)):
            pts, yh = chains[kk]
            sp = np.clip((ytop - P0[i, 1]) / max(ytop - yh, 1e-3), 0, 1.15) * J
            for bb in range(J):
                tent = 1.0 if (bb == 0 and sp <= 0.5) or (bb == J - 1 and sp >= J - 0.5) else max(0.0, 1 - abs(sp - (bb + 0.5)))
                if tent > 0: ws[jidx[(kk, bb)]] = ws.get(jidx[(kk, bb)], 0) + wk * tent
        tot = sum(ws.values()); ws = {kk: v / tot * a[i] for kk, v in ws.items()}
        for q in range(4):
            if W[i, q] > 0: ws[int(Jt[i, q])] = ws.get(int(Jt[i, q]), 0) + W[i, q] * (1 - a[i])
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s_ = sum(w for _, w in top)
        Jt[i] = [kk for kk, _ in top] + [0] * (4 - len(top)); W[i] = [w / s_ for _, w in top] + [0.0] * (4 - len(top))
    # extra chains: a cape hanging from the upper spine and a tail from the pelvis (per-hero masks)
    def add_chain(k, anchor, pts):
        ai = h.names.index(anchor); Ga = G0[ai]; ia = np.linalg.inv(Ga); qa = glbio.qinv(mat_rot(Ga[:3, :3])); prev = None; out = []
        for s_, p_ in enumerate(pts):
            n_ = {"name": f"sb_{k}_{s_}", "translation": list(map(float, (ia @ np.r_[p_, 1])[:3] if s_ == 0 else p_ - pts[s_ - 1]))}
            if s_ == 0: n_["rotation"] = list(map(float, qa))
            nodes.append(n_); ni = len(nodes) - 1
            (nodes[ai] if s_ == 0 else nodes[prev]).setdefault("children", []).append(ni); prev = ni
            if s_ < len(pts) - 1:
                sk["joints"].append(ni); out.append(len(sk["joints"]) - 1)
                M = np.eye(4); M[:3, 3] = p_; ibm_new.append(np.linalg.inv(M))
        return out
    def setw(i, ws, mix):
        ws = {kk: v * mix for kk, v in ws.items()}
        for q in range(4):
            if W[i, q] > 0: ws[int(Jt[i, q])] = ws.get(int(Jt[i, q]), 0) + W[i, q] * (1 - mix)
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s_ = sum(w for _, w in top)
        Jt[i] = [kk for kk, _ in top] + [0] * (4 - len(top)); W[i] = [w / s_ for _, w in top] + [0.0] * (4 - len(top))
    dom0 = np.array([h.jnames[x] for x in h.J[np.arange(len(P0)), h.W.argmax(1)]])
    cp = c.get("cape")
    if cp:
        sp3 = pos(cp.get("anchor", "spine_03"))
        m = np.isin(dom0, cp["src"]) & (P0[:, 2] < sp3[2] - cp.get("behind", 0.08)) & (P0[:, 1] < sp3[1] + 0.12)
        xs = np.linspace(-cp["half"], cp["half"], cp["K"]); Jc = cp.get("J", 3); ytop_c = sp3[1]
        cks = []; prof = []
        for kk, x0 in enumerate(xs):
            band = m & (np.abs(P0[:, 0] - x0) < cp["half"] / (cp["K"] - 1) * 1.2)
            if band.sum() < 15: continue
            yb = np.percentile(P0[band, 1], 3); ys = np.linspace(ytop_c, yb, Jc + 1); pts = []
            for y in ys:
                bb = band & (np.abs(P0[:, 1] - y) < max(0.05, (ytop_c - yb) / Jc / 2))
                z = np.median(P0[bb, 2]) if bb.sum() > 3 else np.median(P0[band, 2])
                pts.append(np.array([x0, y, z]))
            cks.append((x0, yb, add_chain(20 + kk, cp.get("anchor", "spine_03"), pts)))
        ancj = h.jnames.index(cp.get("anchor", "spine_03"))
        cx = np.array([q[0] for q in cks])
        for i in np.nonzero(m)[0]:
            x = np.clip(P0[i, 0], cx[0], cx[-1]); k1 = int(np.clip(np.searchsorted(cx, x), 1, len(cx) - 1)); k0 = k1 - 1
            t = (x - cx[k0]) / max(cx[k1] - cx[k0], 1e-6); ws = {}
            for (x0, yb, jl), wk in ((cks[k0], 1 - t), (cks[k1], t)):
                spv = np.clip((ytop_c - P0[i, 1]) / max(ytop_c - yb, 1e-3), 0, 1.15) * Jc
                for bb in range(Jc):
                    tent = 1.0 if (bb == 0 and spv <= 0.5) or (bb == Jc - 1 and spv >= Jc - 0.5) else max(0.0, 1 - abs(spv - (bb + 0.5)))
                    if tent > 0: ws[jl[bb]] = ws.get(jl[bb], 0) + wk * tent
            f = smooth(ytop_c + 0.05, ytop_c - 0.15, P0[i, 1])   # top of the cape: on the spine, chains below
            ws = {kk: v * f for kk, v in ws.items()}; ws[ancj] = ws.get(ancj, 0) + (1 - f)
            setw(i, ws, 1.0)
    tl = c.get("tail")
    if tl:
        m = np.isin(dom0, tl["src"]) & (P0[:, 2] < pel[2] - tl.get("behind", 0.12)) & (d > tl.get("dleg", 0.12)) & (P0[:, 1] < pel[1])
        base = pel + np.array([0, -0.05, -0.1]); r = np.linalg.norm(P0 - base, axis=1)
        qs = np.percentile(r[m], np.linspace(0, 100, tl["J"] + 1))
        pts = [base] + [P0[m & (r >= qs[q]) & (r <= qs[q + 1])].mean(0) for q in range(tl["J"])]
        jl = add_chain(30, "pelvis", pts)
        P_ = np.array(pts)
        for i in np.nonzero(m)[0]:
            dd = [seg_dist(P0[i:i + 1], P_[q], P_[q + 1])[0] for q in range(len(jl))]
            q = int(np.argmin(dd)); tt = seg_t(P0[i:i + 1], P_[q], P_[q + 1])[0]; ws = {jl[q]: 1.0}
            if tt < 0.3 and q > 0: ws = {jl[q]: 0.5 + tt / 0.6, jl[q - 1]: 0.5 - tt / 0.6}
            setw(i, ws, 1.0)
    at = j["meshes"][0]["primitives"][0]["attributes"]
    write_acc(binb, j, at["JOINTS_0"], Jt); write_acc(binb, j, at["WEIGHTS_0"], W)
    # inverse bind matrices: append the new ones after the IBM view (the last view in these files)
    ai = sk["inverseBindMatrices"]; acc = j["accessors"][ai]; bv = j["bufferViews"][acc["bufferView"]]
    end = bv.get("byteOffset", 0) + bv["byteLength"]
    assert end == len(b) or end == j["buffers"][0]["byteLength"], "IBM view is not last"
    extra = b"".join(np.asarray(M.T, np.float32).tobytes() for M in ibm_new)
    binb = binb[:end] + bytearray(extra)
    bv["byteLength"] += len(extra); acc["count"] += len(ibm_new); j["buffers"][0]["byteLength"] = len(binb)
    save(dst, j, binb)
    return dict(chains=len(ks), joints=len(ibm_new), cloth=int(cloth.sum()), touched=int(len(idx)), verts=len(P0))

def mat_rot(R):
    M = np.eye(4); M[:3, :3] = R; return glbio.mat_rot(M)

if __name__ == "__main__":
    print(rig(*sys.argv[1:4]))
