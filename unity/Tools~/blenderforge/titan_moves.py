"""The Titan's own battle clips, sockets and breakable crystal ridges (owner request 2026-11-28: "Since the Titan has
been remodelled, his mechanics need to be updated"). Procedural keyframed FK on its UBC skeleton, baked at 30 fps
and written straight into the rigged GLB, so the rest pose, mesh and skin stay byte-identical to the rigged model.
No Blender dependency (numpy + Pillow), like creature_gait.py; titan_preview.py renders the result in Blender.

  python3 titan_moves.py SRC.glb SRC_glow.png ALBEDO.jpg OUT_DIR
    SRC.glb      the rigged, unanimated Titan (i3d_rig.py -> compress-hd.mjs; run this AFTER compress-hd, whose prune
                 would drop the socket nodes)
    OUT_DIR      gets SB_Hero_titan_HD.glb, SB_Hero_titan_HD_glow.png (alpha = 255 - crystal ridge mask) and
                 titan_clips.json (each clip's length, events and the fist contact windows measured here; the tests
                 check titan.json's event times against it)

Axes are glTF model space: X = the Titan's left, Y up, Z its front; metres of the 1.85 m model (the battle scales it
by titan.json head / Head bone height, about 4.2). Poses: spine, neck, head and pelvis are rotations about these axes
(pitch + = top tips forward, yaw + = turns to its left, roll + = leans to its right); arms and legs are two-bone IK to
wrist and ankle targets. Added nodes: chest_core (the breath's socket in the open chest), fist_l / fist_r (centres of
the rock fists) and ridge_01.. (one per crystal, at its base; ridge stage 1 breaks at 66% health, stage 2 at 33%).
"""
import json, math, os, struct, sys
import numpy as np
from PIL import Image, ImageDraw

FPS = 30
GROUND_TOL = 0.045          # a fist is "on the ground" within this of the floor (model metres, ~19 cm in battle)

# ---------------------------------------------------------------- glb io
CT = {5120: np.int8, 5121: np.uint8, 5122: np.int16, 5123: np.uint16, 5125: np.uint32, 5126: np.float32}
NC = {"SCALAR": 1, "VEC2": 2, "VEC3": 3, "VEC4": 4, "MAT4": 16}

def load(p):
    f = open(p, "rb").read(); l = struct.unpack("<I", f[12:16])[0]
    j = json.loads(f[20:20 + l]); o = 20 + l; bl = struct.unpack("<I", f[o:o + 4])[0]
    return j, bytearray(f[o + 8:o + 8 + bl])

def acc_view(j, i):
    a = j["accessors"][i]; v = j["bufferViews"][a["bufferView"]]
    n = NC[a["type"]]; dt = CT[a["componentType"]]; isz = np.dtype(dt).itemsize
    return a, v, n, dt, isz, v.get("byteOffset", 0) + a.get("byteOffset", 0), v.get("byteStride") or n * isz

def acc(j, b, i):
    a, v, n, dt, isz, off, st = acc_view(j, i)
    raw = np.frombuffer(bytes(b), dtype=np.uint8)
    idx = off + np.arange(a["count"])[:, None] * st + np.arange(n * isz)[None, :]
    out = raw[idx].copy().view(dt).reshape(a["count"], n)
    return out.astype(np.float32) / np.iinfo(dt).max if a.get("normalized") else out

def append(j, b, arr, ctype, typ, minmax=False):
    """Appends a tightly packed accessor (own buffer view) and returns its index."""
    while len(b) % 4: b.append(0)
    data = np.ascontiguousarray(arr, dtype=CT[ctype]).tobytes()
    j["bufferViews"].append({"buffer": 0, "byteOffset": len(b), "byteLength": len(data)}); b.extend(data)
    a = {"bufferView": len(j["bufferViews"]) - 1, "componentType": ctype, "count": int(len(arr)), "type": typ}
    if minmax:
        m = np.asarray(arr, dtype=np.float64).reshape(len(arr), -1)
        a["min"] = [float(x) for x in m.min(0)]; a["max"] = [float(x) for x in m.max(0)]
    j["accessors"].append(a); return len(j["accessors"]) - 1

def save(p, j, b):
    while len(b) % 4: b.append(0)
    j["buffers"] = [{"byteLength": len(b)}]
    js = json.dumps(j, separators=(",", ":")).encode()
    js += b" " * ((4 - len(js) % 4) % 4)
    with open(p, "wb") as f:
        f.write(struct.pack("<III", 0x46546C67, 2, 12 + 8 + len(js) + 8 + len(b)))
        f.write(struct.pack("<II", len(js), 0x4E4F534A)); f.write(js)
        f.write(struct.pack("<II", len(b), 0x004E4942)); f.write(bytes(b))

# ---------------------------------------------------------------- quaternions (w, x, y, z)
def qmul(a, b):
    w1, x1, y1, z1 = a; w2, x2, y2, z2 = b
    return np.array([w1 * w2 - x1 * x2 - y1 * y2 - z1 * z2, w1 * x2 + x1 * w2 + y1 * z2 - z1 * y2,
                     w1 * y2 - x1 * z2 + y1 * w2 + z1 * x2, w1 * z2 + x1 * y2 - y1 * x2 + z1 * w2])
def qconj(q): return np.array([q[0], -q[1], -q[2], -q[3]])
def qrot(q, v): return qmul(qmul(q, np.array([0.0, *v])), qconj(q))[1:]
def qaxis(ax, deg):
    ax = np.asarray(ax, float); ax = ax / np.linalg.norm(ax); h = math.radians(deg) / 2
    return np.array([math.cos(h), *(ax * math.sin(h))])
def qeuler(p=0.0, y=0.0, r=0.0):  # roll, then pitch, then yaw (model axes)
    return qmul(qaxis((0, 1, 0), y), qmul(qaxis((1, 0, 0), p), qaxis((0, 0, 1), -r)))
def qarc(a, b):
    a = np.asarray(a, float) / np.linalg.norm(a); b = np.asarray(b, float) / np.linalg.norm(b)
    d = float(np.dot(a, b))
    if d < -0.99999:
        ax = np.cross(a, (1, 0, 0)); ax = ax if np.linalg.norm(ax) > 1e-6 else np.cross(a, (0, 1, 0))
        return qaxis(ax, 180)
    c = np.cross(a, b); q = np.array([1 + d, *c]); return q / np.linalg.norm(q)
def qnorm(q): return q / np.linalg.norm(q)
def gl2q(r): return np.array([r[3], r[0], r[1], r[2]])
def q2gl(q): q = qnorm(q); q = q if q[0] >= 0 else -q; return [q[1], q[2], q[3], q[0]]

# ---------------------------------------------------------------- skeleton
class Skel:
    def __init__(self, j):
        self.j = j; self.nodes = j["nodes"]; self.idx = {n["name"]: i for i, n in enumerate(self.nodes)}
        self.parent = {}
        for i, n in enumerate(self.nodes):
            for c in n.get("children", []): self.parent[c] = i
        self.rt = {i: np.array(n.get("translation", [0, 0, 0]), float) for i, n in enumerate(self.nodes)}
        self.rq = {i: gl2q(n.get("rotation", [0, 0, 0, 1])) for i, n in enumerate(self.nodes)}
        for n in self.nodes: assert np.allclose(n.get("scale", [1, 1, 1]), 1), "unit scale expected"
        self.wt, self.wq = self.world(self.rt, self.rq)

    def world(self, lt, lq):
        wt, wq = {}, {}
        def go(i):
            if i in wt: return
            p = self.parent.get(i)
            if p is None: wt[i], wq[i] = lt[i], lq[i]; return
            go(p); wq[i] = qmul(wq[p], lq[i]); wt[i] = wt[p] + qrot(wq[p], lt[i])
        for i in list(lt): go(i)
        return wt, wq

    def i(self, name): return self.idx[name]
    def rest_dir(self, a, b): return self.wt[self.i(b)] - self.wt[self.i(a)]

# ---------------------------------------------------------------- poses
BASE = {  # the hunched idle: fists hang by the knees, slight crouch
    "hips": (0.0, -0.03, 0.0), "pelvis": (0.0, 0.0, 0.0), "spine": (8.0, 0.0, 0.0), "neck": (0.0, 0.0, 0.0), "head": (-10.0, 0.0, 0.0),
    "clav_l": (0.0, 0.0, 0.0), "clav_r": (0.0, 0.0, 0.0),
    "wrist_l": (0.70, 0.52, 0.06), "wrist_r": (-0.70, 0.52, 0.06), "pole_l": (0.6, 0.0, -1.0), "pole_r": (-0.6, 0.0, -1.0),
    "hand_l": (0.0, 0.0, 0.0), "hand_r": (0.0, 0.0, 0.0),
    "ankle_l": (0.18, 0.055, -0.07), "ankle_r": (-0.18, 0.055, -0.07), "foot_l": (0.0, 0.0, 0.0), "foot_r": (0.0, 0.0, 0.0),
}
SPINE_SPLIT = {"spine_01": 0.3, "spine_02": 0.35, "spine_03": 0.35}

def mirror(v): return (-v[0], v[1], v[2])
def P(**kw):
    """A pose: BASE with overrides; arm=(x,y,z) sets both wrists mirrored, legs likewise."""
    p = dict(BASE)
    if "arms" in kw: w = kw.pop("arms"); p["wrist_l"], p["wrist_r"] = w, mirror(w)
    if "ankles" in kw: a = kw.pop("ankles"); p["ankle_l"], p["ankle_r"] = a, mirror(a)
    if "poles" in kw: a = kw.pop("poles"); p["pole_l"], p["pole_r"] = a, mirror(a)
    if "clavs" in kw: c = kw.pop("clavs"); p["clav_l"], p["clav_r"] = c, (c[0], -c[1], -c[2])
    p.update(kw); return p

def solve2(S, T, L1, L2, pole):
    d = T - S; dist = np.linalg.norm(d); e = d / max(dist, 1e-6)
    dist = min(max(dist, abs(L1 - L2) + 1e-4), L1 + L2 - 1e-4)
    pp = np.asarray(pole, float) - np.dot(pole, e) * e
    pp = pp / max(np.linalg.norm(pp), 1e-6)
    a = (L1 * L1 + dist * dist - L2 * L2) / (2 * dist); h = math.sqrt(max(0.0, L1 * L1 - a * a))
    E = S + e * a + pp * h; return E, S + e * dist

def pose_locals(sk, p):
    """Local (translation, rotation) per node for pose p (unlisted nodes stay at rest)."""
    lt = dict(sk.rt); lq = dict(sk.rq)
    def dloc(name, A):
        i = sk.i(name); wr = sk.wq[i]; lq[i] = qmul(sk.rq[i], qmul(qconj(wr), qmul(A, wr)))
    pi = sk.i("pelvis"); par = sk.parent[pi]
    lt[pi] = sk.rt[pi] + qrot(qconj(sk.wq[par]), np.array(p["hips"], float))
    dloc("pelvis", qeuler(*p["pelvis"]))
    for n, f in SPINE_SPLIT.items(): dloc(n, qeuler(*(f * x for x in p["spine"])))
    dloc("neck_01", qeuler(*p["neck"])); dloc("Head", qeuler(*p["head"]))
    dloc("clavicle_l", qeuler(*p["clav_l"])); dloc("clavicle_r", qeuler(*p["clav_r"]))
    wt, wq = sk.world(lt, lq)

    def chain(a, b, c, target, pole, extra=None):
        ia, ib, ic = sk.i(a), sk.i(b), sk.i(c); pa = sk.parent[ia]
        L1 = np.linalg.norm(sk.rt[ib]); L2 = np.linalg.norm(sk.rt[ic])
        S = wt[ia]; E, T = solve2(S, np.array(target, float), L1, L2, np.array(pole, float))
        dpar = qmul(wq[pa], qconj(sk.wq[pa]))
        cur = qrot(dpar, sk.wt[ib] - sk.wt[ia]); Wa = qmul(qarc(cur, E - S), qmul(dpar, sk.wq[ia]))
        da = qmul(Wa, qconj(sk.wq[ia])); cur = qrot(da, sk.wt[ic] - sk.wt[ib])
        Wb = qmul(qarc(cur, T - E), qmul(da, sk.wq[ib]))
        lq[ia] = qmul(qconj(wq[pa]), Wa); lq[ib] = qmul(qconj(Wa), Wb)
        return Wb
    for s in "lr":
        Wl = chain(f"upperarm_{s}", f"lowerarm_{s}", f"hand_{s}", p[f"wrist_{s}"], p[f"pole_{s}"])
        ih, il = sk.i(f"hand_{s}"), sk.i(f"lowerarm_{s}")
        Wh = qmul(qeuler(*p[f"hand_{s}"]), qmul(Wl, qmul(qconj(sk.wq[il]), sk.wq[ih])))
        lq[ih] = qmul(qconj(Wl), Wh)
        Wc = chain(f"thigh_{s}", f"calf_{s}", f"foot_{s}", p[f"ankle_{s}"], (0, 0, 1))
        jf = sk.i(f"foot_{s}"); lq[jf] = qmul(qconj(Wc), qmul(qeuler(*p[f"foot_{s}"]), sk.wq[jf]))
    return lt, lq

# ---------------------------------------------------------------- clip definitions
def ease(kind, u):
    u = min(1.0, max(0.0, u))
    return {"lin": u, "smooth": u * u * (3 - 2 * u), "in": u * u * u, "out": 1 - (1 - u) ** 3,
            "in2": u * u, "out2": 1 - (1 - u) ** 2}[kind]

def lerp_pose(a, b, u):
    return {k: tuple(x + (y - x) * u for x, y in zip(a[k], b[k])) for k in a}

def keyed(keys, loop=False):
    """keys: [(t, pose, ease_into)]; returns pose(t)."""
    def f(t):
        if t <= keys[0][0]: return keys[0][1]
        for (t0, p0, _), (t1, p1, e) in zip(keys, keys[1:]):
            if t <= t1: return lerp_pose(p0, p1, ease(e, (t - t0) / max(1e-6, t1 - t0)))
        return keys[-1][1]
    return f

def add(p, **kw):
    """Adds offsets to a pose's channels (tremble, breathing)."""
    q = dict(p)
    for k, v in kw.items(): q[k] = tuple(x + y for x, y in zip(q[k], v))
    return q

IDLE = BASE
def idle(t, L=3.0):
    s = math.sin(2 * math.pi * t / L); c = math.cos(2 * math.pi * t / L)
    # inhale lifts and opens the chest (spine back, shoulders up), the fists sway a little behind it
    return add(IDLE, hips=(0, 0.008 * s, 0), spine=(-2.2 * s, 0.6 * c, 0), head=(1.2 * s, 0, 0), clav_l=(0, 0, -2.0 * s),
               clav_r=(0, 0, 2.0 * s), wrist_l=(0.006 * s, 0.012 * s, -0.015 * c), wrist_r=(-0.006 * s, 0.012 * s, -0.015 * c))

def walk(t, L=1.4):
    """Knuckle-walk in place (the battle slides the Titan): each fist plants ahead and pushes back, the short legs
    shuffle between, the torso rolls onto the planted arm."""
    ph = (t / L) % 1.0; p = P(spine=(22, 0, 0), head=(-24, 0, 0), hips=(0, -0.05, 0.02))
    def arm(phase, s):
        x = 0.62 * s
        if phase < 0.5:  # stance: planted, sliding back under the body
            u = phase / 0.5; return (x, 0.36, 0.52 - 0.5 * u)
        u = (phase - 0.5) / 0.5; return (x, 0.36 + 0.26 * math.sin(math.pi * u), 0.02 + 0.5 * ease("smooth", u))
    def leg(phase, s):
        x = 0.19 * s
        if phase < 0.5: u = phase / 0.5; return (x, 0.055, 0.06 - 0.18 * u)
        u = (phase - 0.5) / 0.5; return (x, 0.055 + 0.07 * math.sin(math.pi * u), -0.12 + 0.18 * ease("smooth", u))
    p["wrist_l"] = arm(ph, 1); p["wrist_r"] = arm((ph + 0.5) % 1, -1)
    p["ankle_l"] = leg((ph + 0.25) % 1, 1); p["ankle_r"] = leg((ph + 0.75) % 1, -1)
    w = math.sin(2 * math.pi * ph)
    p["spine"] = (22 + 2 * math.cos(4 * math.pi * ph), 7 * w, 5 * w)
    p["hips"] = (0.012 * w, -0.05 - 0.02 * abs(math.cos(2 * math.pi * ph)), 0.02)
    return p

ROAR_UP = P(hips=(0, 0.0, -0.03), spine=(-16, 0, 0), head=(-34, 0, 0), arms=(1.00, 1.55, 0.10), poles=(0.2, -1, -0.6),
            clavs=(0, 0, -12), ankles=(0.22, 0.055, -0.07))
ROAR_GATHER = P(hips=(0, -0.08, 0.02), spine=(26, 0, 0), head=(6, 0, 0), arms=(0.48, 0.38, 0.30), clavs=(0, 0, 6))

def roar_f():
    k = keyed([(0.0, IDLE, "lin"), (0.55, ROAR_GATHER, "smooth"), (0.85, ROAR_UP, "out"), (2.0, ROAR_UP, "lin"), (2.6, IDLE, "smooth")])
    def f(t):
        p = k(t)
        if 0.85 <= t <= 2.0:  # it shakes with the roar
            a = math.sin(t * 61.0) * (1 - (t - 0.85) / 1.15)
            p = add(p, spine=(1.2 * a, 0.8 * a, 0), head=(2.0 * a, 1.5 * a, 0))
        return p
    return f

# Bedrock Hammer: both fists raised over the head and brought down together on one hero.
SLAM_PLANT = P(hips=(0, -0.06, 0.0), ankles=(0.24, 0.055, -0.02), spine=(12, 0, 0), arms=(0.64, 0.58, 0.16))
SLAM_TOP = P(hips=(0, 0.03, -0.05), spine=(-16, 0, 0), head=(-26, 0, 0), arms=(0.16, 2.02, -0.10), poles=(1, 0.2, -0.4),
             clavs=(0, 0, -16), ankles=(0.24, 0.055, -0.02))
SLAM_HIT = P(hips=(0, -0.13, 0.10), spine=(48, 0, 0), head=(-30, 0, 0), arms=(0.20, 0.33, 0.80), poles=(1, 0.6, 0),
             clavs=(0, 0, 4), ankles=(0.24, 0.055, -0.02))
SLAM_SQUASH = add(SLAM_HIT, hips=(0, -0.025, 0), spine=(3, 0, 0))
def slam_f():
    k = keyed([(0.0, IDLE, "lin"), (0.25, SLAM_PLANT, "smooth"), (1.05, SLAM_TOP, "out"), (1.35, SLAM_TOP, "lin"),
               (1.5667, SLAM_HIT, "in"), (1.70, SLAM_SQUASH, "out2"), (1.90, SLAM_HIT, "smooth"), (2.80, IDLE, "smooth")])
    def f(t):
        p = k(t)
        if 1.05 <= t <= 1.35: a = math.sin(t * 70.0) * 0.6; p = add(p, spine=(a, 0, 0), wrist_l=(0, 0.004 * a, 0), wrist_r=(0, 0.004 * a, 0))
        return p
    return f

# Earthsplitter (the fight-ending blow): a wide long-arm sweep raking the ground across the team.
SW_COCK = P(hips=(0, -0.05, 0), spine=(14, -38, -6), head=(-18, 24, 0), wrist_r=(-1.05, 1.10, -0.42), pole_r=(-0.2, -1, -1),
            wrist_l=(0.66, 0.62, 0.28), ankles=(0.25, 0.055, -0.04))
SW_MID = P(hips=(0, -0.17, 0.07), spine=(54, 0, 0), head=(-34, 0, 0), wrist_r=(-0.10, 0.12, 0.80), pole_r=(-0.6, 1, 0),
           wrist_l=(0.72, 0.60, 0.05), ankles=(0.25, 0.055, -0.04))
SW_END = P(hips=(0, -0.10, 0.03), spine=(30, 42, 8), head=(-22, -20, 0), wrist_r=(0.72, 0.22, 0.85), pole_r=(0, 1, -1),
           wrist_l=(0.85, 0.75, -0.30), ankles=(0.25, 0.055, -0.04))
SW_START = P(hips=(0, -0.15, 0.05), spine=(46, -30, -4), head=(-30, 14, 0), wrist_r=(-0.98, 0.18, 0.46), pole_r=(-1, 1, 0),
             wrist_l=(0.66, 0.50, 0.30), ankles=(0.25, 0.055, -0.04))
# The follow-through ends out in front, not folded across the chest, and the return loops forward and low:
# the per-move check (ShotsMode SBMOVE) measured the right forearm 21 cm into the chest collider at the old end pose
# (wrist_r 0.92, 0.26, 0.42) and on the straight way back to idle; now 6.5 cm (blenderforge offline check, limit 8).
SW_BACK = add(lerp_pose(SW_END, IDLE, 0.5), wrist_r=(0.0, -0.05, 0.45))
def sweep_f():
    return keyed([(0.0, IDLE, "lin"), (0.95, SW_COCK, "smooth"), (1.20, SW_COCK, "lin"), (1.36, SW_START, "in"),
                  (1.4667, SW_MID, "lin"), (1.60, SW_END, "out2"), (1.90, SW_END, "lin"), (2.40, SW_BACK, "smooth"), (3.0, IDLE, "smooth")])

# Faultline Leap: crouch on the knuckles, spring up with the fists overhead (the battle flies it), then land.
LP_CROUCH = P(hips=(0, -0.20, 0.06), spine=(38, 0, 0), head=(-34, 0, 0), arms=(0.46, 0.40, 0.46), poles=(1, 0.3, -0.5))
LP_LAUNCH = P(hips=(0, 0.06, 0.0), spine=(-8, 0, 0), head=(-24, 0, 0), arms=(0.40, 1.98, 0.14), poles=(1, 0, -0.5),
              clavs=(0, 0, -12), foot_l=(-30, 0, 0), foot_r=(-30, 0, 0))
LP_AIR = P(hips=(0, 0.0, 0.0), spine=(-4, 0, 0), head=(-22, 0, 0), arms=(0.30, 2.00, 0.05), poles=(1, 0, -0.5),
           clavs=(0, 0, -14), ankles=(0.20, 0.26, 0.08))
def leap_f():
    return keyed([(0.0, IDLE, "lin"), (0.55, LP_CROUCH, "smooth"), (0.66, add(LP_CROUCH, hips=(0, -0.02, 0)), "lin"),
                  (0.78, LP_LAUNCH, "in2"), (1.10, LP_AIR, "out"), (1.20, LP_AIR, "lin")])
LD_DOWN = P(hips=(0, -0.22, 0.08), spine=(46, 0, 0), head=(-34, 0, 0), arms=(0.36, 0.40, 0.68), poles=(1, 0.5, 0),
            ankles=(0.24, 0.055, -0.03))
def land_f():
    return keyed([(0.0, add(LP_AIR, ankle_l=(0, -0.12, 0), ankle_r=(0, -0.12, 0)), "lin"), (0.1667, LD_DOWN, "in"), (0.30, add(LD_DOWN, hips=(0, -0.025, 0)), "out2"),
                  (0.62, LD_DOWN, "smooth"), (1.60, IDLE, "smooth")])

# Heartfire Torrent: rears and swells the chest core, then thrusts it forward and sweeps the fire across the team.
BR_GATHER = P(hips=(0, -0.02, -0.05), spine=(-14, 0, 0), head=(-12, 0, 0), arms=(0.85, 0.58, -0.32), poles=(1, 0, 0.4),
              clavs=(0, 22, -8))
BR_THRUST = P(hips=(0, -0.06, 0.07), spine=(10, 0, 0), head=(-30, 0, 0), arms=(0.84, 0.70, -0.45), poles=(1, 0, 0.6),
              clavs=(0, 28, -4), ankles=(0.22, 0.055, -0.04))
def breath_f():
    def turn(p, yaw): return add(p, spine=(0, yaw, 0), head=(0, -0.4 * yaw, 0), pelvis=(0, 0.25 * yaw, 0))
    k = keyed([(0.0, IDLE, "lin"), (1.8, BR_GATHER, "smooth"), (2.0, turn(BR_THRUST, 22), "out")])
    def f(t):
        if t <= 2.0:
            p = k(t)
            if t > 0.6: p = add(p, spine=(0.6 * math.sin(t * 40), 0, 0))  # trembling with the heat building
            return p
        if t <= 3.8: return turn(BR_THRUST, 22 - 44 * ease("smooth", (t - 2.0) / 1.8))
        if t <= 5.4: return turn(BR_THRUST, -22 + 44 * ease("smooth", (t - 3.8) / 1.6))
        return lerp_pose(turn(BR_THRUST, 22), IDLE, ease("smooth", (t - 5.4) / 0.6))
    return f

HIT_BACK = P(hips=(0, -0.02, -0.05), spine=(-6, 6, 4), head=(-22, -10, 0), arms=(0.74, 0.58, -0.06))
def hit_f():
    return keyed([(0.0, IDLE, "lin"), (0.12, HIT_BACK, "out"), (0.9, IDLE, "smooth")])
BIG_BACK = P(hips=(0, -0.06, -0.10), spine=(-12, -10, 8), head=(-28, 16, 0), wrist_r=(-0.86, 0.80, -0.30), ankles=(0.20, 0.055, -0.14))
BIG_BRACE = P(hips=(0, -0.10, 0.02), spine=(26, 4, -4), head=(-24, 0, 0), wrist_l=(0.56, 0.40, 0.40), wrist_r=(-0.70, 0.50, 0.05))
def hitbig_f():
    return keyed([(0.0, IDLE, "lin"), (0.14, BIG_BACK, "out"), (0.45, BIG_BRACE, "smooth"), (1.3, IDLE, "smooth")])

# Collapse: when your team outlasts it, it staggers, drops to its knees on its fists and sags, glow dying.
CL_STAGGER = P(hips=(0, -0.04, -0.08), spine=(-10, 8, 6), head=(-20, 0, 0), arms=(0.80, 0.62, -0.10))
CL_KNEEL = P(hips=(0, -0.30, 0.04), spine=(40, 0, 0), head=(-14, 0, 0), arms=(0.44, 0.40, 0.50), poles=(1, 0.4, -0.2),
             ankles=(0.20, 0.07, -0.34), foot_l=(60, 0, 0), foot_r=(60, 0, 0))
CL_SAG = P(hips=(0, -0.33, 0.06), spine=(54, 0, 4), head=(18, 0, 0), arms=(0.40, 0.40, 0.42), poles=(1, 0.4, -0.2),
           ankles=(0.20, 0.07, -0.34), foot_l=(60, 0, 0), foot_r=(60, 0, 0))
def collapse_f():
    return keyed([(0.0, IDLE, "lin"), (0.4, CL_STAGGER, "out"), (1.2, CL_KNEEL, "in2"), (1.35, add(CL_KNEEL, hips=(0, -0.02, 0)), "out2"),
                  (2.6, CL_SAG, "smooth")])

# name: (length s, pose(t), loop, events as seconds; titan.json holds the normalized times the game uses)
CLIPS = {
    "Titan_Idle": (3.0, idle, True, {"inhale": 0.75}),
    "Titan_Walk": (1.4, walk, True, {"plant_l": 0.0, "plant_r": 0.7}),
    "Titan_Roar": (2.6, roar_f(), False, {"roar": 0.85}),
    "Titan_Slam": (2.8, slam_f(), False, {"windup": 0.25, "top": 1.05, "impact": 1.5667}),
    "Titan_Sweep": (3.0, sweep_f(), False, {"windup": 0.0, "top": 1.1, "impact": 1.4333}),
    "Titan_Leap": (1.2, leap_f(), False, {"crouch": 0.55, "takeoff": 0.72}),
    "Titan_Land": (1.6, land_f(), False, {"impact": 0.1667}),
    "Titan_Breath": (6.0, breath_f(), False, {"release": 2.0, "sweep2": 3.8, "end": 5.4}),
    "Titan_Hit": (0.9, hit_f(), False, {}),
    "Titan_HitBig": (1.3, hitbig_f(), False, {}),
    "Titan_Collapse": (2.6, collapse_f(), False, {"ground": 1.2}),
}
ANIMATED = ["pelvis", "spine_01", "spine_02", "spine_03", "neck_01", "Head", "clavicle_l", "clavicle_r", "upperarm_l", "lowerarm_l",
            "hand_l", "upperarm_r", "lowerarm_r", "hand_r", "thigh_l", "calf_l", "foot_l", "thigh_r", "calf_r", "foot_r"]

# ---------------------------------------------------------------- crystals and sockets
def find_crystals(P_, I, dom_spine3, lum, sat):
    """Light, unsaturated spikes riding spine_03 (the back and shoulder ridges), welded by position and split into
    connected crystals; each grows into the neighbouring spike vertices."""
    key = np.round(P_ / 2e-3).astype(np.int64); _, weld = np.unique(key, axis=0, return_inverse=True); weld = weld.ravel()
    seed = dom_spine3 & (lum > 105) & (sat < 30) & (P_[:, 2] < 0.08)
    par = np.arange(weld.max() + 1)
    def f(x):
        while par[x] != x: par[x] = par[par[x]]; x = par[x]
        return x
    for t in I:
        for a, c in ((t[0], t[1]), (t[1], t[2]), (t[0], t[2])):
            if seed[a] and seed[c]:
                ra, rc = f(weld[a]), f(weld[c])
                if ra != rc: par[ra] = rc
    roots = np.array([f(w) for w in weld])
    ids, cnt = np.unique(roots[seed], return_counts=True)
    groups = [np.where(seed & (roots == r))[0] for r, c in zip(ids, cnt) if c >= 20]
    cents = np.array([P_[g].mean(0) for g in groups])
    # grow: spine_03 vertices that are fairly light, within reach of a crystal's seed, join the nearest crystal
    cand = np.where(dom_spine3 & (lum > 78) & (P_[:, 2] < 0.1))[0]
    member = -np.ones(len(P_), int)
    for gi, g in enumerate(groups): member[g] = gi
    for v in cand:
        if member[v] >= 0: continue
        d = np.linalg.norm(cents - P_[v], axis=1); gi = int(np.argmin(d))
        ext = np.linalg.norm(P_[groups[gi]] - cents[gi], axis=1).max()
        if d[gi] < ext * 1.15 + 0.012: member[v] = gi
    # triangles wholly inside one crystal define it (the hinge rows stay on spine_03 so the skin stays closed)
    return member, len(groups)

def main(src, glow_src, albedo, out_dir):
    j, b = load(src)
    assert not j.get("animations") and "chest_core" not in [n["name"] for n in j["nodes"]], "pass the rigged, unanimated model"
    sk = Skel(j)
    prim = j["meshes"][0]["primitives"][0]; A = prim["attributes"]
    Pv = acc(j, b, A["POSITION"]); J = acc(j, b, A["JOINTS_0"]).astype(int); W = acc(j, b, A["WEIGHTS_0"]); UV = acc(j, b, A["TEXCOORD_0"])
    I = acc(j, b, prim["indices"]).reshape(-1, 3)
    skin = j["skins"][0]; joints = skin["joints"]; jname = [j["nodes"][k]["name"] for k in joints]
    dom = np.array([jname[J[i, np.argmax(W[i])]] for i in range(len(Pv))])
    alb = np.asarray(Image.open(albedo).convert("RGB")).astype(float)
    H, Wd = alb.shape[:2]
    px = alb[np.clip((UV[:, 1] * H).astype(int), 0, H - 1), np.clip((UV[:, 0] * Wd).astype(int), 0, Wd - 1)]
    lum = px.mean(1); sat = px.max(1) - px.min(1)

    # --- crystals: one ridge bone each, at its base, child of spine_03
    member, ng = find_crystals(Pv, I, dom == "spine_03", lum, sat)
    s3 = sk.i("spine_03"); body_c = np.array([0.0, 1.36, -0.10])
    info = []
    for gi in range(ng):
        vs = np.where(member == gi)[0]; c = Pv[vs].mean(0)
        u = c - body_c; u = u / np.linalg.norm(u); s = (Pv[vs] - c) @ u
        base = Pv[vs[s <= np.percentile(s, 20)]].mean(0)
        info.append({"verts": vs, "centre": c, "base": base, "size": float(np.linalg.norm(Pv[vs] - c, axis=1).max())})
    # stage 1 (66%): the smaller crystals; stage 2 (33%): the next; the three biggest stay (a broken titan, not a bald one)
    order = sorted(range(ng), key=lambda g: info[g]["size"])
    n1 = round(ng * 0.4); n2 = round(ng * 0.35)
    stage = {g: (1 if r < n1 else 2 if r < n1 + n2 else 0) for r, g in enumerate(order)}

    new_nodes = []
    def add_node(name, parent, world_pos):
        pi = sk.i(parent) if isinstance(parent, str) else parent
        local = qrot(qconj(sk.wq[pi]), np.asarray(world_pos, float) - sk.wt[pi])
        j["nodes"].append({"name": name, "translation": [float(x) for x in local]})
        ni = len(j["nodes"]) - 1; j["nodes"][pi].setdefault("children", []).append(ni); new_nodes.append(ni); return ni
    core = (Pv[:, 1] > 0.85) & (Pv[:, 1] < 1.2) & (abs(Pv[:, 0]) < 0.12) & (Pv[:, 2] > 0.05)
    core_dom = max(("spine_02", "spine_03"), key=lambda n: int((dom[core] == n).sum()))
    chest = np.array([0.0, 1.0, float(np.percentile(Pv[core][:, 2], 60)) - 0.02])
    add_node("chest_core", core_dom, chest)
    fists = {}
    for s in "lr":
        fv = Pv[dom == f"hand_{s}"]; fists[s] = fv
        add_node(f"fist_{s}", f"hand_{s}", fv.mean(0))
    ridge_nodes = []
    for k, g in enumerate(sorted(range(ng), key=lambda g: (-info[g]["base"][1], info[g]["base"][0]))):
        name = f"ridge_{k + 1:02d}_s{stage[g]}"; ni = add_node(name, "spine_03", info[g]["base"]); ridge_nodes.append((ni, g))
    # skin: sockets and ridges join the joints (prune-safe, and the ridges carry their crystals' weights)
    ibm = acc(j, b, skin["inverseBindMatrices"]).reshape(-1, 16).tolist()
    jidx = {}
    for ni in new_nodes:
        joints.append(ni); jidx[ni] = len(joints) - 1
        n = j["nodes"][ni]
        # world of a new node = parent's rest world + rotated local translation (no rotation of its own)
        par = next(i for i, nn in enumerate(j["nodes"]) if ni in nn.get("children", []))
        wt = sk.wt[par] + qrot(sk.wq[par], np.array(n["translation"])); wq = sk.wq[par]
        R = np.eye(4); R[:3, :3] = quat_mat(wq); R[:3, 3] = wt
        ibm.append(np.linalg.inv(R).T.reshape(-1).tolist())   # column-major
    skin["inverseBindMatrices"] = append(j, b, np.array(ibm, np.float32), 5126, "MAT4")
    # crystal vertices: their spine_03 influence moves to their ridge joint (same rest motion, now breakable)
    a, v, n_, dt, isz, off, st = acc_view(j, A["JOINTS_0"]); assert dt == np.uint8
    s3j = jname.index("spine_03"); moved = 0
    for ni, g in ridge_nodes:
        for vi in info[g]["verts"]:
            row = J[vi].copy()
            if s3j in row:
                row[row == s3j] = jidx[ni]; b[off + vi * st: off + vi * st + 4] = bytes(row.astype(np.uint8)); moved += 1

    # --- clips
    names = {i: n["name"] for i, n in enumerate(j["nodes"])}
    ground = float(Pv[:, 1].min())     # AutoGround puts the bind pose's lowest point on the floor
    fsub = {s: fists[s][:: max(1, len(fists[s]) // 600)] for s in "lr"}
    report = {"_doc": "Written by blenderforge/titan_moves.py: each clip's length, its authored events (normalized) and the "
                      "fist contact windows measured from the baked fists (fist bottom within %.3f m of the floor, model metres)." % GROUND_TOL,
              "ground": ground, "headBoneY": float(sk.wt[sk.i("Head")][1]), "chestCore": [float(x) for x in chest], "chestParent": core_dom,
              "ridges": [{"node": names[ni], "stage": stage[g], "base": [round(float(x), 4) for x in info[g]["base"]], "verts": int(len(info[g]["verts"]))} for ni, g in ridge_nodes],
              "clips": {}}
    anims = []
    for cname, (L, fn, loop, events) in CLIPS.items():
        nf = int(round(L * FPS)) + 1; times = np.arange(nf) / FPS
        rot = {n: [] for n in ANIMATED}; pel = []
        bottoms = {"l": [], "r": []}; fz = {"l": [], "r": []}
        for t in times:
            lt, lq = pose_locals(sk, fn(t % L) if loop else fn(t))
            for n in ANIMATED: rot[n].append(q2gl(lq[sk.i(n)]))
            pel.append(lt[sk.i("pelvis")])
            wt, wq = sk.world(lt, lq)
            for s in "lr":
                h = sk.i(f"hand_{s}"); dq = qmul(wq[h], qconj(sk.wq[h]))
                pts = np.array([wt[h] + qrot(dq, p - sk.wt[h]) for p in fsub[s]])
                bottoms[s].append(float(pts[:, 1].min())); fz[s].append(float(pts[:, 2].mean()))
        if loop:  # the last frame repeats the first exactly
            for n in ANIMATED: rot[n][-1] = rot[n][0]
            pel[-1] = pel[0]
        on = [(min(bottoms["l"][k], bottoms["r"][k]) <= ground + GROUND_TOL) for k in range(nf)]
        wins = []; k = 0
        while k < nf:
            if on[k]:
                e = k
                while e + 1 < nf and on[e + 1]: e += 1
                wins.append([round(times[k] / L, 4), round(times[e] / L, 4)]); k = e + 1
            else: k += 1
        zc = [max(fz["l"][k], fz["r"][k]) for k in range(nf) if on[k]]
        report["clips"][cname] = {"len": L, "loop": loop, "events": {e: round(s / L, 4) for e, s in events.items()}, "contact": wins,
                                  "contactZ": round(float(np.median(zc)), 4) if zc else None,
                                  "minFistBottom": round(min(min(bottoms["l"]), min(bottoms["r"])) - ground, 4)}
        inp = append(j, b, times.astype(np.float32), 5126, "SCALAR", minmax=True)
        chans, samps = [], []
        for n in ANIMATED:
            samps.append({"input": inp, "output": append(j, b, np.array(rot[n], np.float32), 5126, "VEC4"), "interpolation": "LINEAR"})
            chans.append({"sampler": len(samps) - 1, "target": {"node": sk.i(n), "path": "rotation"}})
        samps.append({"input": inp, "output": append(j, b, np.array(pel, np.float32), 5126, "VEC3"), "interpolation": "LINEAR"})
        chans.append({"sampler": len(samps) - 1, "target": {"node": sk.i("pelvis"), "path": "translation"}})
        anims.append({"name": cname, "channels": chans, "samplers": samps})
    j["animations"] = anims
    os.makedirs(out_dir, exist_ok=True)
    save(os.path.join(out_dir, "SB_Hero_titan_HD.glb"), j, b)

    # --- glow mask: alpha = 255 - crystal ridge mask (the game's MaskFromGlow puts the ridge mask in the red channel)
    g = Image.open(glow_src).convert("RGB"); gw, gh = g.size
    rid = Image.new("L", (gw, gh), 0); d = ImageDraw.Draw(rid)
    inr = member >= 0
    for t in I:
        if inr[t[0]] and inr[t[1]] and inr[t[2]]:
            d.polygon([(float(UV[v, 0] * gw), float(UV[v, 1] * gh)) for v in t], fill=255)
    from PIL import ImageFilter
    rid = rid.filter(ImageFilter.MaxFilter(3))
    out = g.copy(); out.putalpha(Image.eval(rid, lambda x: 255 - x))
    out.save(os.path.join(out_dir, "SB_Hero_titan_HD_glow.png"), optimize=True)
    report["ridgeMaskCoverage"] = round(float((np.asarray(rid) > 0).mean()), 4)
    report["crystalVertsMoved"] = moved
    with open(os.path.join(out_dir, "titan_clips.json"), "w") as f: json.dump(report, f, indent=1)
    print(f"{ng} crystals (stage1 {sum(1 for s in stage.values() if s == 1)}, stage2 {sum(1 for s in stage.values() if s == 2)}), {moved} verts moved")
    for c, r in report["clips"].items(): print(f"{c:15s} {r['len']:.2f}s contact {r['contact']} z {r['contactZ']} events {r['events']}")
    print("glb", os.path.getsize(os.path.join(out_dir, "SB_Hero_titan_HD.glb")) // 1024, "KB")

def quat_mat(q):
    w, x, y, z = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])

if __name__ == "__main__":
    main(*sys.argv[1:5])
