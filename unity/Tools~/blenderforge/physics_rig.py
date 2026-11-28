"""Physics standard, chain builder (docs/sigilbound/art/PHYSICS.md): adds spring chains for the dangling parts
physics_classify.py found on a shipped HD hero GLB, in place (no re-encode, nothing else about the model changes),
for the kinds the hero's overrides enable. Chains are nodes sb_<k>_<i> (last node = tip); k gives the kind
(Core/Physics/ColliderSet.cs ChainKinds: 20-29 cape, 30-34 tail, 40-49 hair, 55-69 flap; skirts 0-19 come from the
pilot's spring_rig.py ring). Runtime: Client/SpringBones.cs with the hero's body collider set.

    python3 physics_rig.py SRC.glb DST.glb HERO [--overrides Tools~/models/physics/overrides.json]

Rules every chain obeys (tests check them on the shipped files): >= 2 bones + a tip, <= MaxJointsPerHero joints per
hero, <= 255 skin joints, every chain joint at rest outside the colliders its kind collides with (by the kind's joint
radius; joints are pushed out at build time - moving a pivot a few cm doesn't change the skin at rest).
Run it LAST (after compress-hd.mjs, which prunes empty leaf nodes such as chain tips).
Weights: a part's vertices take the two neighbouring chains (blend across) and the two nearest bones along them
(tent weights down), blended with their old weights near the anchor so the top stays seated (after spring_rig.py)."""
import sys, os, json, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save
import physics_classify as pc

MAX_JOINTS = 48  # ChainKinds.MaxJointsPerHero
INFLATE = {"cape": 0.045}  # ChainKinds.Defaults Inflate
JOINT_R = {"cape": 0.025, "tail": 0.04, "hair": 0.02, "flap": 0.02, "skirt": 0.03, "ear": 0.015, "wing": 0.03}  # ChainKinds.Defaults


def smooth(e0, e1, x):
    t = np.clip((x - e0) / (e1 - e0), 0, 1); return t * t * (3 - 2 * t)


def mat_rot(R):
    M = np.eye(4); M[:3, :3] = R; return glbio.mat_rot(M)


class Builder:
    def __init__(self, src, hid):
        self.hid = hid
        self.body = b = pc.Body(src)
        if any(n.startswith("sb_") for n in b.names): raise SystemExit("already has spring chains")
        self.R, _ = pc.measure(b)
        self.parts, self.rigid, self.caps = pc.classify(b, self.R)
        self.j, binb = glbio.load(src); self.bin = bytearray(binb)
        self.h = b.h; self.W = b.h.W.copy(); self.J = b.h.J.copy(); self.P = b.P
        self.sk = self.j["skins"][0]; self.ibm_new = []; self.nodes = self.j["nodes"]; self.report = []

    def pos(self, n): return self.body.pos(n)

    def push_out(self, pts, kind):
        """Chain joints at rest must sit outside the colliders their kind collides with, by the kind's joint radius."""
        mask = pc.KIND_MASK[kind]; jr = JOINT_R[kind] + INFLATE.get(kind, 0.0) + 0.005; out = []
        for p in pts:
            p = np.array(p, float)
            for _ in range(3):
                for (_, A, B, r, g) in self.caps:
                    if not (g & mask): continue
                    ab = B - A; t = float(np.clip(((p - A) @ ab) / max(ab @ ab, 1e-12), 0, 1)); c = A + ab * t
                    d = p - c; dl = float(np.linalg.norm(d))
                    if dl < r + jr: p = c + (d / max(dl, 1e-9) if dl > 1e-6 else np.array([0, 0, -1.0])) * (r + jr)
            out.append(p)
        return out

    def add_chain(self, k, anchor, pts):
        nodes = self.nodes; sk = self.sk; names = self.body.names
        ai = names.index(anchor); Ga = self.body.G[ai]; ia = np.linalg.inv(Ga); qa = glbio.qinv(mat_rot(Ga[:3, :3])); prev = None; out = []
        for s_, p_ in enumerate(pts):
            n_ = {"name": f"sb_{k}_{s_}", "translation": list(map(float, (ia @ np.r_[p_, 1])[:3] if s_ == 0 else p_ - pts[s_ - 1]))}
            if s_ == 0: n_["rotation"] = list(map(float, qa))
            nodes.append(n_); ni = len(nodes) - 1
            (nodes[ai] if s_ == 0 else nodes[prev]).setdefault("children", []).append(ni); prev = ni
            if s_ < len(pts) - 1:
                sk["joints"].append(ni); out.append(len(sk["joints"]) - 1)
                M = np.eye(4); M[:3, 3] = p_; self.ibm_new.append(np.linalg.inv(M))
        return out

    def setw(self, i, ws, mix):
        W, Jt = self.W, self.J
        ws = {kk: v * mix for kk, v in ws.items()}
        for q in range(4):
            if W[i, q] > 0: ws[int(Jt[i, q])] = ws.get(int(Jt[i, q]), 0) + W[i, q] * (1 - mix)
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s_ = sum(w for _, w in top)
        Jt[i] = [kk for kk, _ in top] + [0] * (4 - len(top)); W[i] = [w / s_ for _, w in top] + [0.0] * (4 - len(top))

    def sheet(self, kind, m, anchor, k0, K, J, ytop, across="x", top_band=(0.05, 0.15), min_band=15):
        """Chains hanging down a sheet (cape, hair curtain): K chains spread across `across`, J bones each from ytop
        to the sheet's lowest point; vertices blend between the two neighbouring chains and the nearest bones."""
        P = self.P; ax = 0 if across == "x" else 2
        sel = m & (P[:, 1] < ytop + 0.02)
        if sel.sum() < 40: return 0
        lo, hi = np.percentile(P[sel, ax], 4), np.percentile(P[sel, ax], 96)
        xs = np.linspace(lo, hi, K) if K > 1 else np.array([(lo + hi) / 2])
        dx = (hi - lo) / max(K - 1, 1)
        chains = []
        for kk, x0 in enumerate(xs):
            band = sel & (np.abs(P[:, ax] - x0) < max(dx * 0.6, 0.03))
            if band.sum() < min_band: continue
            yb = np.percentile(P[band, 1], 3); ys = np.linspace(ytop, yb, J + 1); pts = []
            for y in ys:
                bb = band & (np.abs(P[:, 1] - y) < max(0.04, (ytop - yb) / J / 2))
                q = P[bb] if bb.sum() > 3 else P[band]
                p = np.median(q, 0); p[ax] = x0; p[1] = y; pts.append(p)
            pts = self.push_out(pts, kind)
            chains.append((x0, yb, self.add_chain(k0 + len(chains), anchor, pts)))
        if not chains: return 0
        cx = np.array([c[0] for c in chains])
        for i in np.nonzero(m)[0]:
            x = np.clip(P[i, ax], cx[0], cx[-1])
            if len(cx) == 1: pairs = [(chains[0], 1.0)]
            else:
                k1 = int(np.clip(np.searchsorted(cx, x), 1, len(cx) - 1)); k0_ = k1 - 1
                t = (x - cx[k0_]) / max(cx[k1] - cx[k0_], 1e-6); pairs = [(chains[k0_], 1 - t), (chains[k1], t)]
            ws = {}
            for (x0, yb, jl), wk in pairs:
                spv = np.clip((ytop - P[i, 1]) / max(ytop - yb, 1e-3), 0, 1.15) * J
                for bb in range(J):
                    tent = 1.0 if (bb == 0 and spv <= 0.5) or (bb == J - 1 and spv >= J - 0.5) else max(0.0, 1 - abs(spv - (bb + 0.5)))
                    if tent > 0: ws[jl[bb]] = ws.get(jl[bb], 0) + wk * tent
            if not ws: continue
            f = float(smooth(ytop + top_band[0], ytop - top_band[1], P[i, 1]))   # above the chain roots: old weights
            if f > 0: self.setw(i, ws, f)
        self.report.append(dict(kind=kind, chains=len(chains), bones=J, joints=len(chains) * J, verts=int(m.sum())))
        return len(chains) * J

    def whole_islands(self, m):
        """Kit parts are their own islands (a cape sheet with its thickness): take every island the part mostly covers,
        drop the few part vertices on islands it barely touches (the body). A fused image-to-3D mesh (one island) keeps
        the classifier's mask."""
        isl = self.body.isl_v; n = len(isl); strong = []
        for il, c in zip(*np.unique(isl[m], return_counts=True)):
            tot = int((isl == il).sum())
            if c / tot >= 0.4 and tot < 0.3 * n: strong.append(il)
        return np.isin(isl, strong) if strong else m

    def cape(self, part, o):
        m = self.whole_islands(part["mask"]); anchor = o.get("anchor", "spine_03")
        ytop = o.get("top", float(self.pos(anchor)[1]))
        width = float(np.percentile(self.P[m, 0], 96) - np.percentile(self.P[m, 0], 4))
        K = o.get("K", int(np.clip(round(width / 0.07), 5, 7))); J = o.get("J", 3)
        return self.sheet("cape", m, anchor, 20, K, J, ytop)

    def hair(self, part, o):
        m = self.whole_islands(part["mask"]); head = self.pos("Head")
        ytop = o.get("top", float(head[1] - 0.02))
        width = part["x"][1] - part["x"][0]
        K = o.get("K", int(np.clip(round(width / 0.1), 2, 5))); J = o.get("J", 2 if part.get("length", 0.3) < 0.3 else 3)
        return self.sheet("hair", m, "Head", 40, K, J, ytop, top_band=(0.03, 0.08), min_band=10)

    def flap(self, part, o, k):
        m = part["mask"]; P = self.P
        anchor = o.get("anchor", part["anchor"]); ytop = float(P[m, 1].max()) - 0.01
        return self.sheet("flap", m, anchor, k, 1, o.get("J", 2), ytop, top_band=(0.02, 0.05), min_band=8)

    def tail_bones(self):
        """The rig's own tail_01.. chain (i3d_rig "tail") becomes spring chain 30 (+ a tip node); RigExtras then no
        longer sways it (it looks for tail_01)."""
        names = self.body.names; nodes = self.nodes
        tb = sorted(n for n in names if n.startswith("tail_"))
        for k_, n_ in enumerate(tb): nodes[names.index(n_)]["name"] = f"sb_30_{k_}"
        last = nodes[names.index(tb[-1])]; prevt = nodes[names.index(tb[-2])].get("translation", [0, -0.1, 0]) if len(tb) > 1 else [0, -0.1, 0]
        nodes.append({"name": f"sb_30_{len(tb)}", "translation": list(map(float, prevt))}); last.setdefault("children", []).append(len(nodes) - 1)
        self.report.append(dict(kind="tail", chains=1, bones=len(tb), joints=len(tb), verts=0, from_rig=True))
        return len(tb)

    def build(self, ov):
        kinds = ov.get("chains", [])
        total = 0; flap_k = 55
        for p in self.parts:
            o = ov.get(p["kind"], {})
            if p["kind"] not in kinds: continue
            if p["kind"] == "cape": total += self.cape(p, o)
            elif p["kind"] == "hair": total += self.hair(p, o)
            elif p["kind"] == "flap" and flap_k < 70:
                n = self.flap(p, o, flap_k); flap_k += 1 if n else 0; total += n
            elif p["kind"] == "tail" and p.get("bones"): total += self.tail_bones()
        if total == 0: raise SystemExit("no chains built")
        if total > MAX_JOINTS: raise SystemExit(f"{total} spring joints > budget {MAX_JOINTS}")
        if len(self.sk["joints"]) > 255: raise SystemExit("too many joints for uint8")
        return total

    def save(self, dst):
        j, b = self.j, self.bin
        at = j["meshes"][0]["primitives"][0]["attributes"]
        write_acc(b, j, at["JOINTS_0"], self.J); write_acc(b, j, at["WEIGHTS_0"], self.W)
        if self.ibm_new:
            ai = self.sk["inverseBindMatrices"]; acc = j["accessors"][ai]; bv = j["bufferViews"][acc["bufferView"]]
            end = bv.get("byteOffset", 0) + bv["byteLength"]
            assert end == len(b) or end == j["buffers"][0]["byteLength"], "IBM view is not last"
            extra = b"".join(np.asarray(M.T, np.float32).tobytes() for M in self.ibm_new)
            b = b[:end] + bytearray(extra)
            bv["byteLength"] += len(extra); acc["count"] += len(self.ibm_new); j["buffers"][0]["byteLength"] = len(b)
        save(dst, j, b)


def load_overrides(path):
    return json.load(open(path)) if path and os.path.exists(path) else {}


def main():
    a = sys.argv[1:]
    src, dst, hid = a[:3]
    here = os.path.dirname(os.path.abspath(__file__))
    ovp = a[a.index("--overrides") + 1] if "--overrides" in a else os.path.join(here, "..", "models", "physics", "overrides.json")
    ov = load_overrides(ovp).get(hid)
    if not ov: raise SystemExit(f"{hid}: no physics overrides entry (classify first, then enable kinds)")
    bd = Builder(src, hid); n = bd.build(ov); bd.save(dst)
    print(json.dumps(dict(hero=hid, joints=n, chains=bd.report)))


if __name__ == "__main__":
    main()
