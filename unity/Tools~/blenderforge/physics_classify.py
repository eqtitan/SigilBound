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

# --- the collider set (keep in step with Core/Physics/ColliderSet.cs: names, bones, groups, data order) ---------
# data per hero: R = [head, headUp, neck, chest, spine, hips, upperarm, lowerarm, thigh, calf]
DATA = ["head", "headUp", "neck", "chest", "spine", "hips", "upperarm", "lowerarm", "thigh", "calf"]
# fallback ratios (radius / reference length) = medians over the 158 heroes (printed by --all); C# uses the same
FALLBACK = {"head": 0.54, "headUp": 0.9, "neck": 0.34, "chest": 0.23, "spine": 0.22, "hips": 0.15,
            "upperarm": 0.21, "lowerarm": 0.17, "thigh": 0.2, "calf": 0.14}
# clamp band per radius, as a fraction of the reference length (keeps a robe or a pauldron from inflating it)
BAND = {"head": (0.35, 0.9), "neck": (0.3, 0.9), "chest": (0.15, 0.4), "spine": (0.12, 0.38), "hips": (0.06, 0.25),
        "upperarm": (0.08, 0.3), "lowerarm": (0.07, 0.26), "thigh": (0.09, 0.3), "calf": (0.07, 0.24)}
SHAPES = [  # name, boneA, boneB (None = sphere on A), data key, group
    ("head", "Head", None, "head", "Head"), ("neck", "neck_01", "Head", "neck", "Neck"),
    ("chest", "spine_02", "neck_01", "chest", "Torso"), ("spine", "pelvis", "spine_02", "spine", "Torso"),
    ("hips", "thigh_l", "thigh_r", "hips", "Hips"),
    ("upperarm_l", "upperarm_l", "lowerarm_l", "upperarm", "Arms"), ("upperarm_r", "upperarm_r", "lowerarm_r", "upperarm", "Arms"),
    ("lowerarm_l", "lowerarm_l", "hand_l", "lowerarm", "Forearms"), ("lowerarm_r", "lowerarm_r", "hand_r", "lowerarm", "Forearms"),
    ("thigh_l", "thigh_l", "calf_l", "thigh", "Thighs"), ("thigh_r", "thigh_r", "calf_r", "thigh", "Thighs"),
    ("calf_l", "calf_l", "foot_l", "calf", "Calves"), ("calf_r", "calf_r", "foot_r", "calf", "Calves"),
]
GROUPS = {"Head": 1, "Neck": 2, "Torso": 4, "Hips": 8, "Arms": 16, "Forearms": 32, "Thighs": 64, "Calves": 128, "Extra": 256}
HANDISH = ("hand", "index", "middle", "ring", "pinky", "thumb")


def ref_len(key, pos):
    """The bone length a radius is measured against (same rule as ColliderSet.RefLength)."""
    L = lambda a, b: float(np.linalg.norm(pos(a) - pos(b)))
    return {"head": L("neck_01", "Head") + 0.08, "headUp": 1.0, "neck": L("neck_01", "Head") + 0.08,
            "chest": L("pelvis", "neck_01"), "spine": L("pelvis", "neck_01"), "hips": L("pelvis", "neck_01"),
            "upperarm": L("upperarm_l", "lowerarm_l"), "lowerarm": L("lowerarm_l", "hand_l"),
            "thigh": L("thigh_l", "calf_l"), "calf": L("calf_l", "foot_l")}[key]


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
    """Collider radii (and the head sphere's lift) from the hero's own girth: per segment, the vertices whose
    dominant joint is that bone, inside the middle of the segment, distance to the bone; the 30th percentile is the
    body under the clothes (a robe or pauldron on the same bone sits further out), clamped to a band of the bone length."""
    pos = b.pos; P = b.P; out = {}; why = {}
    def ring(bones, a, c, key, pct=30):
        sel = np.isin(b.dom, bones)
        if sel.sum() < 30: return None
        d, t = seg_dist(P[sel], pos(a), pos(c)); m = (t > 0.15) & (t < 0.85)
        if m.sum() < 20: return None
        return float(np.percentile(d[m], pct))
    def put(key, val):
        L = ref_len(key, pos); lo, hi = BAND[key]
        if val is None: out[key] = FALLBACK[key] * L; why[key] = "fallback"; return
        r = min(max(val, lo * L), hi * L); out[key] = r; why[key] = "girth" if r == val else "clamped"
    # head: horizontal radius of the Head-weighted band just above the head joint (hair and hats sit above/outside)
    hp = pos("Head"); sel = (b.dom == "Head") & (P[:, 1] > hp[1] + 0.02) & (P[:, 1] < hp[1] + 0.16)
    hr = float(np.percentile(np.hypot(P[sel, 0] - hp[0], P[sel, 2] - hp[2]), 30)) if sel.sum() > 30 else None
    put("head", hr); out["headUp"] = 0.9; why["headUp"] = "rule"
    put("neck", ring(["neck_01"], "neck_01", "Head", "neck"))
    put("chest", ring(["spine_02", "spine_03"], "spine_02", "neck_01", "chest", 25))
    put("spine", ring(["pelvis", "spine_01", "spine_02"], "pelvis", "spine_02", "spine", 25))
    sel = np.isin(b.dom, ["pelvis"]) & (P[:, 1] < pos("pelvis")[1] + 0.05) & (P[:, 1] > pos("pelvis")[1] - 0.15)
    hipv = float(np.percentile(seg_dist(P[sel], pos("thigh_l"), pos("thigh_r"))[0], 30)) if sel.sum() > 30 else None
    put("hips", hipv)
    v = [ring(["upperarm_" + s], "upperarm_" + s, "lowerarm_" + s, "upperarm") for s in "lr"]
    put("upperarm", None if None in v else (v[0] + v[1]) / 2)
    v = [ring(["lowerarm_" + s], "lowerarm_" + s, "hand_" + s, "lowerarm") for s in "lr"]
    put("lowerarm", None if None in v else (v[0] + v[1]) / 2)
    v = [ring(["thigh_" + s], "thigh_" + s, "calf_" + s, "thigh") for s in "lr"]
    put("thigh", None if None in v else (v[0] + v[1]) / 2)
    v = [ring(["calf_" + s], "calf_" + s, "foot_" + s, "calf") for s in "lr"]
    put("calf", None if None in v else (v[0] + v[1]) / 2)
    return out, why


def shapes(b, R, pose_G=None):
    """The collider set in model space: list of (name, A, B, r, group bit). pose_G: global matrices of a pose (else rest)."""
    G = pose_G if pose_G is not None else b.G
    pos = lambda n: G[b.names.index(n)][:3, 3]
    out = []
    for name, a, c, key, grp in SHAPES:
        if not b.has(a) or (c and not b.has(c)): continue
        if c is None:   # head sphere: lifted along the neck->head direction by headUp * r (rest offset carried by the bone)
            up = b.pos("Head") - b.pos("neck_01"); up /= max(np.linalg.norm(up), 1e-9)
            off_rest = up * R["headUp"] * R["head"]
            Ga = G[b.names.index(a)]; Ra = b.G[b.names.index(a)]
            local = np.linalg.inv(Ra[:3, :3]) @ off_rest
            A = Ga[:3, 3] + Ga[:3, :3] @ local
            out.append((name, A, A, R[key], GROUPS[grp]))
        else:
            out.append((name, pos(a).copy(), pos(c).copy(), R[key], GROUPS[grp]))
    if b.has("tail_01") and b.has("tail_02"):
        out.append(("tail", pos("tail_01").copy(), pos("tail_02").copy(), 0.6 * R["calf"], GROUPS["Extra"]))
    for s in "lr":
        if b.has("wing_" + s): out.append(("wing_" + s, pos("wing_" + s).copy(), pos("wing_" + s).copy(), 0.8 * R["upperarm"], GROUPS["Extra"]))
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
KIND_MASK = {  # which body colliders a chain of that kind collides with (ChainKinds in Core/Physics/ColliderSet.cs)
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
    caps = shapes(b, R)
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
        res = {}; ratios = {k: [] for k in DATA}
        for f in sorted(os.listdir(d)):
            if not (f.startswith("SB_Hero_") and f.endswith("_HD.glb")): continue
            hid = f[8:-7]
            b, R, why, parts, rig = run(os.path.join(d, f), clips)
            if R is None: print(hid, "skipped: not a humanoid rig", why); continue
            res[hid] = dict(R={k: round(v, 4) for k, v in R.items()}, how=why, parts=clean(parts), rigid=rig)
            for k in DATA:
                if why.get(k) == "girth": ratios[k].append(R[k] / ref_len(k, b.pos))
            print(hid, " ".join(f"{p['kind']}:{p['verts']}/{p.get('severity', 0)}" for p in parts), flush=True)
        json.dump(res, open(out, "w"), indent=1)
        print("median ratios:", {k: round(float(np.median(v)), 3) for k, v in ratios.items() if v})
        if "--colliders-cs" in a: write_cs(res, a[a.index("--colliders-cs") + 1])
        return
    path = a[0]
    clips = glbio.Clips(a[a.index("--clips") + 1] if "--clips" in a else clips_path)
    b, R, why, parts, rig = run(path, clips)
    rep = dict(R=R, how=why, parts=clean(parts), rigid=rig)
    if "--json" in a: json.dump(rep, open(a[a.index("--json") + 1], "w"), indent=1)
    print(json.dumps(rep, indent=1))


def write_cs(res, path):
    """Core/Physics/BodyColliderData.cs: one line per hero, the radii in DATA order (generated; do not edit)."""
    lines = []
    for hid in sorted(res):
        R = res[hid]["R"]
        lines.append(f'            ["{hid}"] = new[] {{ ' + ", ".join(f"{R[k]:.3f}f" for k in DATA) + " },")
    src = f"""// GENERATED by unity/Tools~/blenderforge/physics_classify.py --all (physics standard, docs/sigilbound/art/PHYSICS.md).
// Per hero: the body collider radii measured from its own mesh girth and bone lengths, in metres (model space), in
// ColliderSet.Data order: {", ".join(DATA)}. Heroes without a line get ColliderSet's fallback ratios of their bone
// lengths. Regenerate after any model change; tests check every shipped hero has a line and sane sizes.
using System.Collections.Generic;

namespace Sigilbound.Core.Physics
{{
    public static class BodyColliderData
    {{
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
