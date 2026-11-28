"""Physics standard, build-time half (docs/sigilbound/art/PHYSICS.md): for one shipped HD hero GLB
1. measures the body collider set (Core/Physics/ColliderSet.cs - same shapes, same order) from the hero's own
   bones and mesh girth, and
2. classifies the mesh: dangling parts that should swing (long hair, capes, skirts/robe panels, tails, ears,
   flaps/tassels/pouches, soft wings) get a proposed spring chain; rigid accessories (weapons, shields, head
   gear) are listed as RIGID and are never physics.
3. scores how badly each dangling part fights the body in the battle clips without physics (vertices inside the
   body colliders, summed over a few key frames) so remediation can go worst-first.

    python3 physics_classify.py HERO_GLB [--clips ubc_anims.glb] [--json OUT.json]
    python3 physics_classify.py --all MODELS_DIR OUT.json [--colliders-cs Core/Physics/BodyColliderData.cs]

Pure numpy (glb_io), no Blender. The classifier only proposes; per-hero overrides live in
Tools~/models/physics/overrides.json and the verdict is always a frame sweep (character-td rules)."""
import sys, os, json, math, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio

# --- the body collider set: the library's physics standard (Core/Rig/RigPhysics.BodySet, Biped) - same names, bones,
# order and fallback fractions; this tool measures the radius of each from the hero's own girth -------------------
SHAPES = [  # name, bone, tip (None = sphere on the bone, length to its parent), fraction of that length
    ("head", "Head", None, 1.1), ("neck", "neck_01", "Head", 0.45), ("chest", "spine_03", "neck_01", 0.8),
    ("spine", "spine_01", "spine_03", 0.6), ("hips", "pelvis", "spine_01", 1.3)]
for _s in "lr":
    SHAPES += [("upperarm_" + _s, "upperarm_" + _s, "lowerarm_" + _s, 0.2), ("lowerarm_" + _s, "lowerarm_" + _s, "hand_" + _s, 0.17),
               ("thigh_" + _s, "thigh_" + _s, "calf_" + _s, 0.22), ("calf_" + _s, "calf_" + _s, "foot_" + _s, 0.17)]
NAMES = [x[0] for x in SHAPES]   # BodyColliderData.Names
SPHERE_PARENT = {"Head": "neck_01"}
# sane band per radius as a multiple of the library's fraction x length (the tests use the same band)
BAND = (0.5, 1.8)
GROUPS = {"Head": 1, "Neck": 2, "Torso": 4, "Hips": 8, "Arms": 16, "Forearms": 32, "Thighs": 64, "Calves": 128, "Extra": 256}
HANDISH = ("hand", "index", "middle", "ring", "pinky", "thumb")


def group(name):   # RigPhysics.Group
    if name == "head": return GROUPS["Head"]
    if name == "neck": return GROUPS["Neck"]
    if name in ("chest", "spine"): return GROUPS["Torso"]
    if name == "hips": return GROUPS["Hips"]
    for pre, g in (("upperarm", "Arms"), ("lowerarm", "Forearms"), ("thigh", "Thighs"), ("calf", "Calves")):
        if name.startswith(pre): return GROUPS[g]
    return GROUPS["Extra"]


def ref_len(name, pos):
    _, a, t, frac = next(x for x in SHAPES if x[0] == name)
    return float(np.linalg.norm(pos(a) - pos(t or SPHERE_PARENT[a])))


def fallback(pos):
    return {n: frac * ref_len(n, pos) for n, a, t, frac in SHAPES}


def aliases(R):
    """Convenience sizes the classifier uses (averages of the two sides)."""
    R = dict(R)
    for k in ("upperarm", "lowerarm", "thigh", "calf"): R[k] = (R[k + "_l"] + R[k + "_r"]) / 2
    return R


def starts(arr, prefix):
    pre = prefix if isinstance(prefix, tuple) else (prefix,)
    return np.array([str(x).startswith(pre) for x in arr])


def seg_dist(P, a, b):
    ab = b - a; d = float(ab @ ab)
    t = np.clip(((P - a) @ ab) / d, 0, 1) if d > 1e-12 else np.zeros(len(P))
    return np.linalg.norm(P - (a + t[:, None] * ab), axis=1), t


class Body:
    """Rest pose of one hero: skinned positions, bones, dominant joint per vertex, islands."""
    def __init__(self, path):
        self.path = path
        self.h = h = glbio.Hero(path)
        self.P, self.G = h.skin()
        self.names = h.names
        self.dom = np.array([h.jnames[x] for x in h.J[np.arange(len(self.P)), h.W.argmax(1)]])
        wid = glbio.weld(self.P, 2e-4); self.wid = wid; self.isl_f = glbio.components(h.F, wid)
        self.isl_v = np.full(len(self.P), -1); self.isl_v[h.F.ravel()] = np.repeat(self.isl_f, 3)
        self.handw = np.zeros(len(self.P)); self.headw = np.zeros(len(self.P))
        for ji, nm in enumerate(h.jnames):
            w = (h.W * (h.J == ji)).sum(1)
            if nm.startswith(HANDISH) or nm.startswith("lowerarm"): self.handw += w
            if nm in ("Head", "neck_01"): self.headw += w

    def has(self, n): return n in self.names
    def pos(self, n): return self.G[self.names.index(n)][:3, 3].copy()


def grow(b, mask, seed):
    """The parts of `mask` that are connected (through faces with all corners in the mask, positions welded) to
    any `seed` vertex: a cape is the sheet that hangs from the shoulders, a skirt the cloth that hangs from the waist."""
    wid = b.wid; mw = np.zeros(wid.max() + 1, bool); mw[wid[mask]] = True
    F = wid[b.h.F]; F = F[mw[F].all(1)]
    if len(F) == 0: return np.zeros(len(mask), bool)
    lab = glbio.components(F, wid)  # label per kept face
    sw = np.zeros(wid.max() + 1, bool); sw[wid[seed & mask]] = True
    good = np.unique(lab[sw[F].any(1)])
    keep = np.zeros(wid.max() + 1, bool); keep[F[np.isin(lab, good)].ravel()] = True
    return keep[wid] & mask


def measure(b):
    """Radius of each library body collider from the hero's own girth: per collider, the vertices whose dominant joint is
    that bone, inside the middle of the segment, distance to the bone; the 30th percentile is the body under the clothes
    (a robe or pauldron on the same bone sits further out), clamped to BAND x the library's fraction x the bone length.
    Also 'skull' (the classifier's head-top estimate; not a collider)."""
    pos = b.pos; P = b.P; out = {}; why = {}
    dom_of = {"Head": ["Head"], "neck_01": ["neck_01"], "spine_03": ["spine_02", "spine_03"], "spine_01": ["spine_01", "spine_02"], "pelvis": ["pelvis"]}
    for name, a, t, frac in SHAPES:
        L = ref_len(name, pos); fb = frac * L
        bones = dom_of.get(a, [a]); sel = np.isin(b.dom, bones); val = None
        if name == "head":
            hp = pos("Head"); m = sel & (P[:, 1] > hp[1] - 0.02) & (P[:, 1] < hp[1] + 0.12)
            if m.sum() > 30: val = float(np.percentile(np.linalg.norm(P[m] - hp, axis=1), 30))
        elif sel.sum() >= 30:
            d, tt = seg_dist(P[sel], pos(a), pos(t)); m = (tt > 0.15) & (tt < 0.85) if name != "hips" else np.ones(len(d), bool)
            if m.sum() >= 20: val = float(np.percentile(d[m], 15 if name == "hips" else 30))   # gowns are pelvis-weighted
        if val is None: out[name] = fb; why[name] = "fallback"; continue
        r = min(max(val, BAND[0] * fb), BAND[1] * fb); out[name] = r; why[name] = "girth" if r == val else "clamped"
    hp = pos("Head"); sel = (b.dom == "Head") & (P[:, 1] > hp[1] + 0.02) & (P[:, 1] < hp[1] + 0.16)
    out["skull"] = float(np.clip(np.percentile(np.hypot(P[sel, 0] - hp[0], P[sel, 2] - hp[2]), 30), 0.05, 0.16)) if sel.sum() > 30 else 0.09
    return out, why


def shapes(b, R, pose_G=None):
    """The body collider set in model space: list of (name, A, B, r, group bit). pose_G: global matrices of a pose."""
    G = pose_G if pose_G is not None else b.G
    pos = lambda n: G[b.names.index(n)][:3, 3]
    out = []
    for name, a, t, frac in SHAPES:
        if not b.has(a) or (t and not b.has(t)): continue
        A = pos(a).copy(); B = pos(t).copy() if t else A
        out.append((name, A, B, R[name], group(name)))
    return out


def outside(P, caps, mask=0xFFFF):
    """Signed distance of every point to the nearest collider surface (negative = inside) and its index."""
    best = np.full(len(P), 9.0); idx = np.full(len(P), -1)
    for k, (_, A, B, r, g) in enumerate(caps):
        if not (g & mask): continue
        d = seg_dist(P, A, B)[0] - r; m = d < best; best[m] = d[m]; idx[m] = k
    return best, idx


# --- classification -----------------------------------------------------------------------------------------------
KIND_CHAIN = {"skirt": (0, 19), "cape": (20, 29), "tail": (30, 34), "wing": (35, 39), "hair": (40, 49), "ear": (50, 54), "flap": (55, 69)}
KIND_MASK = {  # which body colliders a chain of that kind collides with (ChainKinds.Mask in Core/Rig/RigChains.cs)
    "skirt": GROUPS["Hips"] | GROUPS["Thighs"] | GROUPS["Calves"],
    "cape": GROUPS["Torso"] | GROUPS["Hips"] | GROUPS["Thighs"] | GROUPS["Calves"] | GROUPS["Extra"],
    "tail": GROUPS["Hips"] | GROUPS["Thighs"] | GROUPS["Calves"],
    "wing": GROUPS["Torso"] | GROUPS["Arms"] | GROUPS["Head"],
    "hair": GROUPS["Head"] | GROUPS["Neck"] | GROUPS["Torso"] | GROUPS["Arms"],
    "ear": GROUPS["Head"],
    "flap": GROUPS["Hips"] | GROUPS["Thighs"],
}


def classify(b, R):
    P = b.P; pos = b.pos; n = len(P)
    caps = shapes(b, R); R = aliases(R); R["head"] = R["skull"]
    d_out, near = outside(P, caps)
    pel, sp3, neck, head = pos("pelvis"), pos("spine_03"), pos("neck_01"), pos("Head")
    knee = (pos("calf_l") + pos("calf_r")) / 2
    shoulder_w = abs(pos("upperarm_l")[0] - pos("upperarm_r")[0])
    fingers = starts(b.dom, HANDISH)
    hand = (b.handw > 0.5) | fingers
    dfore = np.minimum.reduce([seg_dist(P, pos("lowerarm_" + s), pos("hand_" + s))[0] - R["lowerarm"] for s in "lr"])
    # rigid accessories: islands on a hand (weapons, staffs), off the forearm (shields), or on the head above the
    # skull (helmets, crowns, hats). Listed, never physics; head gear seating is fix/ctd-headgear's job.
    rigid = np.zeros(n, bool); rig_parts = []
    head_top = head[1] + 2 * R["head"] * 0.95
    for il in np.unique(b.isl_v[b.isl_v >= 0]):
        m = b.isl_v == il; k = int(m.sum())
        if k < 20 or k > 0.3 * n: continue
        if fingers[m].mean() > 0.6 or (b.handw[m].mean() > 0.6 and np.median(dfore[m]) > 0.02):
            rigid |= m; rig_parts.append(dict(kind="prop", verts=k, bone=str(b.dom[m][0])))
        elif b.headw[m].mean() > 0.9 and P[m, 1].max() > head_top - 0.02 and P[m, 1].min() > head[1] - 0.02:
            rigid |= m; rig_parts.append(dict(kind="headgear", verts=k))
    free = (d_out > 0.03) & ~rigid & ~hand
    parts = []
    def part(kind, m, why, anchor, **kw):
        k = int(m.sum())
        if k < 40: return
        ys = P[m, 1]
        parts.append(dict(kind=kind, verts=k, share=round(k / n, 3), why=why, anchor=anchor,
                          top=round(float(ys.max()), 3), bottom=round(float(ys.min()), 3),
                          x=[round(float(P[m, 0].min()), 3), round(float(P[m, 0].max()), 3)],
                          z=[round(float(P[m, 2].min()), 3), round(float(P[m, 2].max()), 3)], mask=m, **kw))
    def used():
        u = np.zeros(n, bool)
        for p in parts: u |= p["mask"]
        return u
    behind_back = P[:, 2] < sp3[2] - R["chest"] * 0.75
    dleg = np.minimum.reduce([seg_dist(P, pos("thigh_" + s), pos("foot_" + s))[0] for s in "lr"])
    # rig-time add-on bones first (tails from i3d_rig "tail", wings from kit_rig): their own vertices
    if b.has("tail_01"): part("tail", starts(b.dom, "tail_"), "rig tail bones", "pelvis", bones=True)
    if b.has("wing_l"): part("wing", starts(b.dom, "wing_") & ~used(), "rig wing bones (RigExtras flutter)", "spine_03", bones=True)
    # long hair: head/neck weighted, below the jaw, outside the neck/chest (hangs down the back or over the shoulders)
    hm = free & ~used() & (b.headw > 0.5) & (P[:, 1] < head[1] - 0.02)
    if hm.sum() > 60 and P[hm, 1].min() < neck[1] - 0.08:
        part("hair", hm, "head-weighted, hangs below the neck", "Head", length=round(float(head[1] - P[hm, 1].min()), 3))
    # ears: head-weighted, sticking out sideways/up beyond the skull (rigid unless long)
    em = (b.headw > 0.8) & ~used() & ~rigid & (P[:, 1] > head[1]) & (np.abs(P[:, 0] - head[0]) > R["head"] * 1.25) & (d_out > 0.03)
    if em.sum() > 60 and float(np.abs(P[em, 0] - head[0]).max()) > R["head"] * 1.8:
        part("ear", em, "head-weighted spikes beyond the skull", "Head")
    # wings without bones: wide sheets behind the shoulders reaching well past the arms' roots
    if not b.has("wing_l"):
        wm = free & ~used() & behind_back & (np.abs(P[:, 0] - pel[0]) > shoulder_w * 0.9) & (P[:, 1] > pel[1] + 0.1)
        if wm.sum() > 150: part("wing", wm, "sheet behind the shoulders past the arms", "spine_03", bones=False)
    # cape: behind the back, a sheet hanging from the shoulders past the pelvis (grown from the upper back, so a gown's
    # back panel below the waist is not a cape)
    legdom = starts(b.dom, ("thigh", "calf", "foot", "ball"))
    armdom = starts(b.dom, ("clavicle", "upperarm", "lowerarm"))
    cm = (d_out > 0.01) & ~rigid & ~hand & ~used() & behind_back & ~legdom & ~armdom & ((P[:, 1] > pel[1]) | (dleg > R["thigh"] + 0.05)) & (P[:, 1] < neck[1] + 0.05) & (np.abs(P[:, 0] - pel[0]) < shoulder_w * 1.0)
    cm = grow(b, cm, cm & (P[:, 1] > pos("spine_02")[1]))
    if cm.sum() > 150 and P[cm, 1].max() > sp3[1] - 0.1 and P[cm, 1].min() < pel[1] - 0.05:
        part("cape", cm, "sheet behind the back from the shoulders past the pelvis", "spine_03")
    # skirt / robe panels / coat tails: cloth hanging from the waist, outside the legs
    sm = free & ~used() & (P[:, 1] < pel[1] + 0.06) & (dleg > R["thigh"] * np.where(starts(b.dom, "thigh"), 1.5, 0.9)) & (P[:, 1] > 0.06) & ~starts(b.dom, ("calf", "foot", "ball"))
    sm = grow(b, sm, sm & (P[:, 1] > pel[1] - 0.12))
    rad = np.hypot(P[:, 0] - pel[0], P[:, 2] - pel[2])
    # a tail without bones: behind the pelvis and reaching beyond the cloth's own radius at that height
    if not b.has("tail_01"):
        ys = np.arange(0.0, pel[1] + 0.1, 0.05); front = sm & (P[:, 2] > pel[2] - 0.05)
        rr = np.array([np.percentile(rad[front & (np.abs(P[:, 1] - y) < 0.05)], 90) if (front & (np.abs(P[:, 1] - y) < 0.05)).sum() > 10 else 0.0 for y in ys])
        cloth_r = np.interp(P[:, 1], ys, rr)
        tm = free & ~used() & (P[:, 2] < pel[2] - 0.14) & (P[:, 1] < pel[1] + 0.05) & (dleg > R["thigh"] + 0.08) & (P[:, 1] > 0.08) & (rad > np.maximum(cloth_r + 0.08, 0.3))
        tm = grow(b, free & ~used() & (P[:, 2] < pel[2] - 0.1) & (P[:, 1] < pel[1] + 0.1), tm)
        if tm.sum() > 60 and (P[tm, 2].min() < pel[2] - 0.3 or P[tm, 1].min() < knee[1]):
            part("tail", tm, "mass behind the pelvis reaching past the cloth", "pelvis", bones=False)
    sm &= ~used()
    if sm.sum() > 300 and P[sm, 1].min() < pel[1] - 0.2:
        part("skirt", sm, "cloth below the waist outside the legs", "pelvis", hem=round(float(np.percentile(P[sm, 1], 3)), 3))
    # flaps, tassels, pouches, sashes, banners: small free islands hanging at the waist or the back
    u = used()
    for il in np.unique(b.isl_v[free & ~u]):
        m = (b.isl_v == il) & free & ~u & ~starts(b.dom, ("calf", "foot", "ball")); k = int(m.sum())
        if 40 <= k <= 0.06 * n and P[m, 1].max() < sp3[1] and (P[m, 1].max() - P[m, 1].min()) > 0.08:
            part("flap", m, "small free island hanging off the body", "pelvis" if P[m, 1].max() < pel[1] + 0.15 else "spine_03")
    return parts, rig_parts, caps


# --- severity without physics: how much of each part ends up inside the body during key battle frames -------------
KEY_FRAMES = [("Jog_Fwd_Loop", 0.25), ("Jog_Fwd_Loop", 0.75), ("Hit_Kneel", 0.5), ("Sword_Attack", 0.45),
              ("Death01", 0.6), ("Hit_Chest", 0.4), ("Spell_Simple_Shoot", 0.5)]


def severity(b, R, parts, clips):
    sev = {i: 0.0 for i in range(len(parts))}
    for clip, u in KEY_FRAMES:
        if clip not in clips.clips: continue
        loc = clips.local_pose(b.h, clip, u); Pq, Gq = b.h.skin(loc)
        caps = shapes(b, R, Gq)
        for i, p in enumerate(parts):
            d, _ = outside(Pq[p["mask"]], caps, KIND_MASK[p["kind"]])
            sev[i] += float(np.clip(-d, 0, 0.2).sum())
    for i, p in enumerate(parts): p["severity"] = round(sev[i], 2)


REQUIRED = ("pelvis", "spine_02", "neck_01", "Head", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r",
            "hand_l", "hand_r", "thigh_l", "thigh_r", "calf_l", "calf_r", "foot_l", "foot_r")


def run(path, clips=None):
    b = Body(path)
    miss = [n for n in REQUIRED if not b.has(n)]
    if miss: return b, None, {"missing": miss}, [], []   # creature rigs (CreatureRig): no humanoid body set
    R, why = measure(b)
    parts, rig, caps = classify(b, R)
    if clips is not None: severity(b, R, parts, clips)
    return b, R, why, parts, rig


def clean(parts): return [{k: v for k, v in p.items() if k != "mask"} for p in parts]


def main():
    a = sys.argv[1:]
    here = os.path.dirname(os.path.abspath(__file__))
    clips_path = os.path.join(here, "..", "models", "ubc_anims.glb")
    if a and a[0] == "--all":
        d, out = a[1], a[2]
        clips = glbio.Clips(clips_path)
        res = {}; ratios = {k: [] for k in NAMES}
        for f in sorted(os.listdir(d)):
            if not (f.startswith("SB_Hero_") and f.endswith("_HD.glb")): continue
            hid = f[8:-7]
            b, R, why, parts, rig = run(os.path.join(d, f), clips)
            if R is None: print(hid, "skipped: not a humanoid rig", why); continue
            res[hid] = dict(R={k: round(v, 4) for k, v in R.items()}, how=why, parts=clean(parts), rigid=rig)
            for k in NAMES:
                if why.get(k) == "girth": ratios[k].append(R[k] / ref_len(k, b.pos))
            print(hid, " ".join(f"{p['kind']}:{p['verts']}/{p.get('severity', 0)}" for p in parts), flush=True)
        json.dump(res, open(out, "w"), indent=1)
        print("median radius / bone length (library fractions: head 1.1, neck .45, chest .8, spine .6, hips 1.3, upperarm .2, lowerarm .17, thigh .22, calf .17):", {k: round(float(np.median(v)), 3) for k, v in ratios.items() if v})
        if "--colliders-cs" in a: write_cs(res, a[a.index("--colliders-cs") + 1])
        return
    path = a[0]
    clips = glbio.Clips(a[a.index("--clips") + 1] if "--clips" in a else clips_path)
    b, R, why, parts, rig = run(path, clips)
    rep = dict(R=R, how=why, parts=clean(parts), rigid=rig)
    if "--json" in a: json.dump(rep, open(a[a.index("--json") + 1], "w"), indent=1)
    print(json.dumps(rep, indent=1))


def write_cs(res, path):
    """Core/Rig/BodyColliderData.cs: one line per hero, the measured radii in RigPhysics.BodySet order (generated)."""
    lines = []
    for hid in sorted(res):
        R = res[hid]["R"]
        lines.append(f'            ["{hid}"] = new[] {{ ' + ", ".join(f"{R[k]:.3f}f" for k in NAMES) + " },")
    names = ", ".join(f'"{n}"' for n in NAMES)
    src = f"""// GENERATED by unity/Tools~/blenderforge/physics_classify.py --all (physics standard, docs/sigilbound/art/PHYSICS.md).
// Per hero: the radius of each body collider of the library standard (Core/Rig/RigPhysics.BodySet) measured from the
// hero's own mesh girth, in metres (model space), in Names order. RigPhysics.Radius uses it instead of fraction x bone
// length x girth. Regenerate after any model change; tests check every shipped hero has a line and sane sizes.
using System.Collections.Generic;

namespace Sigilbound.Core
{{
    public static class BodyColliderData
    {{
        public static readonly List<string> Names = new List<string> {{ {names} }};

        public static readonly Dictionary<string, float[]> ById = new Dictionary<string, float[]>
        {{
{chr(10).join(lines)}
        }};
    }}
}}
"""
    open(path, "w").write(src)


if __name__ == "__main__":
    main()
