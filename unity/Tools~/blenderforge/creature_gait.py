"""Procedural gait for creature heroes with many legs (spider layout; reusable for other leg layouts).
No Blender dependency: creature.py maps the output onto the rig and bakes it to clips.

Units are the prototype's metres (Z up, the creature faces -Y, left = +X). creature.py scales them to the model.
simulate(clip) -> frames at FPS, each a dict:
  body:  (x, y, z, yaw, pitch, roll, pivot)  delta of the creature body from rest (pitch + = nose up)
  feet:  {leg: (x, y, z)}                      foot tip targets
  abd:   (yaw, pitch, breath)                  abdomen lag, tilt and breathing
  upper: {"spine": (bend, side, twist), "head": (bend, side, twist)}  degrees, body frame, added on top of
         the upper-body source clip (bend + = forward)
The gait: an alternating tetrapod (L1 R2 L3 R4 / R1 L2 R3 L4). Planted feet stay put in the world while the
body glides; a group steps when it drifts from rest, landing half a stride ahead. Each leg has its own small
start delay and step duration (fixed per leg and step parity, so loops stay periodic).
"""
import math, random

FPS = 30
BODY_Z = 0.95
ATTACH, REST, LEGS = {}, {}, []
# Leg layout (owner review 2026-11-27): thin legs whose femurs rise steeply so the knees arch near chest height,
# then drop steeply to the ground; feet REACH from the hips at these angles from forward, front to back.
ANGLES, REACH = (38, 78, 105, 142), 0.82
for side, s in (("L", 1.0), ("R", -1.0)):
    for i, (ay, ang) in enumerate(zip((-0.18, -0.06, 0.06, 0.18), ANGLES), 1):
        n = f"{side}{i}"; LEGS.append(n); a_ = math.radians(ang); ATTACH[n] = (s * 0.16, ay, BODY_Z)
        REST[n] = (s * (0.16 + REACH * math.sin(a_)), ay - REACH * math.cos(a_), 0.0)
GROUP_A = ("L1", "R2", "L3", "R4"); GROUP_B = ("R1", "L2", "R3", "L4")
SEG = (0.10, 0.56, 1.03, 0.62)          # coxa, femur, tibia, tarsus
TARSUS_LEAN = math.radians(9)
_rng = random.Random(7)
JITTER = {(l, p): (_rng.uniform(0.0, 0.04), _rng.uniform(0.88, 1.12)) for l in LEGS for p in (0, 1)}

def smooth(u): u = max(0.0, min(1.0, u)); return u * u * (3 - 2 * u)
def bump(u): return math.sin(math.pi * max(0.0, min(1.0, u)))
def lerp(a, b, u): return tuple(x + (y - x) * u for x, y in zip(a, b))
def add(a, b): return tuple(x + y for x, y in zip(a, b))
def sub(a, b): return tuple(x - y for x, y in zip(a, b))
def mul(a, k): return tuple(x * k for x in a)
def length(a): return math.sqrt(sum(x * x for x in a))
def rotz(p, a):
    c, s = math.cos(a), math.sin(a); return (p[0] * c - p[1] * s, p[0] * s + p[1] * c, p[2])

def body_point(local, body):
    x, y, z, yaw, pitch, roll, pivot = body
    p = sub(local, pivot)
    c, s = math.cos(roll), math.sin(roll); p = (p[0] * c + p[2] * s, p[1], -p[0] * s + p[2] * c)
    c, s = math.cos(-pitch), math.sin(-pitch); p = (p[0], p[1] * c - p[2] * s, p[1] * s + p[2] * c)
    p = rotz(p, yaw)
    return add(add(p, pivot), (x, y, z))

def rest_world(leg, body, spread=1.0):
    x, y, z, yaw, *_ = body
    r = REST[leg]; p = rotz((r[0] * spread, r[1] * spread, 0.0), yaw); return (p[0] + x, p[1] + y, 0.0)

def solve_leg(leg, hip, foot):
    """Joint points hip, coxa_end, knee, ankle, foot: coxa aims at the foot, tarsus held steep, femur+tibia
    a two-bone solve with the knee up."""
    d = sub(foot, hip); out = (d[0], d[1], 0.0); r = length(out) or 1e-6; o = mul(out, 1 / r)
    coxa_end = add(hip, mul(o, SEG[0]))
    ankle = add(foot, (-o[0] * SEG[3] * math.sin(TARSUS_LEAN), -o[1] * SEG[3] * math.sin(TARSUS_LEAN), SEG[3] * math.cos(TARSUS_LEAN)))
    v = sub(ankle, coxa_end); dist = length(v); a, b = SEG[1], SEG[2]
    dist_c = max(abs(a - b) + 1e-3, min(a + b - 1e-3, dist))
    A = math.acos(max(-1, min(1, (a * a + dist_c * dist_c - b * b) / (2 * a * dist_c))))
    vn = mul(v, 1 / (dist or 1e-6))
    dot = vn[2]; perp = sub((0, 0, 1), mul(vn, dot)); pl = length(perp) or 1e-6; perp = mul(perp, 1 / pl)
    knee = add(coxa_end, add(mul(vn, a * math.cos(A)), mul(perp, a * math.sin(A))))
    return hip, coxa_end, knee, ankle, foot

class Stepper:
    def __init__(self, body0, dur=0.2, lift=0.16, jitter=True):
        self.dur, self.lift, self.jitter = dur, lift, jitter
        self.feet = {l: rest_world(l, body0) for l in LEGS}
        self.step = {}; self.last = None; self.enabled = set(LEGS); self.count = {GROUP_A: 0, GROUP_B: 0}
    def drift(self, group, body):
        ds = [length(sub(self.feet[l], rest_world(l, body))) for l in group if l in self.enabled]
        return max(ds) if ds else 0.0
    def update(self, t, body, body_at, thresh=0.05):
        for l, (p0, p1, t0, d, lift) in list(self.step.items()):
            if t < t0: continue
            u = (t - t0) / d
            if u >= 1: self.feet[l] = p1; del self.step[l]
            else:
                p = lerp(p0, p1, smooth(u)); self.feet[l] = (p[0], p[1], p[2] + lift * math.sin(math.pi * u))
        if self.step: return
        other = lambda g: GROUP_B if g == GROUP_A else GROUP_A
        order = sorted((GROUP_A, GROUP_B), key=lambda g: (g == self.last, -self.drift(g, body)))
        for g in order:
            if self.drift(g, body) > thresh and (g != self.last or self.drift(other(g), body) < thresh * 0.5):
                parity = self.count[g] % 2; self.count[g] += 1
                for l in g:
                    if l not in self.enabled: continue
                    delay, ds = JITTER[(l, parity)] if self.jitter else (0.0, 1.0)
                    fut = body_at(t + delay + 1.5 * self.dur)
                    tgt = rest_world(l, fut); p0 = self.feet[l]
                    if length(sub(tgt, p0)) < 0.01: continue
                    self.step[l] = (p0, tgt, t + delay, self.dur * ds, min(self.lift, 0.05 + 0.4 * length(sub(tgt, p0))))
                self.last = g; return
    def active(self, t): return {l for l, v in self.step.items() if v[2] <= t}

# ---------- clips: lengths (s), impact fraction where battle code waits for contact
CLIP_LEN = {"WalkIn": 2.7, "Idle_Loop": 4.0, "Walk_Loop": 4.0, "Turn": 2.2, "Attack": 1.6, "Attack2": 1.2, "Cast": 1.4,
            "Ult_Pounce": 2.0, "Hit": 0.7, "Death": 1.8, "Win": 2.4, "Stun_Loop": 2.0}
IMPACT = {"Attack": 0.30, "Attack2": 0.30, "Ult_Pounce": 0.62}
LOOPS = {"Idle_Loop", "Walk_Loop", "Win", "Stun_Loop"}
IDLE_EVENTS = [("L2", 0.10, (0.06, -0.05)), ("R3", 0.25, (-0.05, 0.05)), ("L4", 0.50, (0.05, 0.04)), ("R1", 0.65, (-0.04, -0.06))]

def clip_body(name, t, L, base=(0, 0, 0, 0)):
    bx, by, bz, byaw = base; pivot = (0, 0, BODY_Z)
    x = y = z = pitch = roll = yaw = 0.0
    if name == "Walk_Loop": y = -1.5 * t; pitch = math.radians(3)
    elif name == "WalkIn":
        v = 1.5
        if t < 0.4: d = v * t * t / 0.8
        elif t < 1.8: d = v * 0.2 + v * (t - 0.4)
        elif t < 2.3: d = v * 1.6 + v * (t - 1.8) - v * (t - 1.8) ** 2 / 1.0
        else: d = v * 1.85
        y = -d; pitch = math.radians(3) * (1 - smooth((t - 1.8) / 0.5))
    elif name == "Turn": yaw = math.radians(90) * smooth(t / 1.6)
    elif name == "Attack":
        I = IMPACT["Attack"] * L; pivot = (0, 0.18, BODY_Z)
        up = smooth((t - 0.04) / (I - 0.16)); slam = smooth((t - (I - 0.09)) / 0.09); rec = smooth((t - I - 0.08) / 0.75)
        pitch = math.radians(30) * up * (1 - slam) + math.radians(-8) * slam * (1 - rec)
        z = 0.13 * up * (1 - slam) - 0.07 * slam * (1 - rec); y = 0.05 * up * (1 - slam) - 0.12 * slam * (1 - rec)
    elif name == "Attack2":
        I = IMPACT["Attack2"] * L; go = smooth(t / I); rec = smooth((t - I - 0.05) / 0.6)
        y = -0.20 * go * (1 - rec); z = -0.05 * go * (1 - rec); pitch = math.radians(-5) * go * (1 - rec)
    elif name == "Cast":
        w = smooth(t / 0.35) * (1 - smooth((t - 0.9) / 0.45)); pitch = math.radians(7) * w; z = 0.05 * w; y = 0.04 * w
    elif name == "Ult_Pounce":
        I = IMPACT["Ult_Pounce"] * L; crouch = smooth(t / 0.45) * (1 - smooth((t - 0.45) / 0.12))
        u = max(0.0, min(1.0, (t - 0.45) / (I - 0.45)))
        fly = 1.0 if t >= I else u; land = smooth((t - I) / 0.12) * (1 - smooth((t - I - 0.25) / 0.3)); back = smooth((t - I - 0.25) / (L - I - 0.35))
        y = -1.0 * smooth(fly) * (1 - back); z = -0.16 * crouch + (0.6 * math.sin(math.pi * u) if t < I else 0) - 0.08 * land
        pitch = math.radians(-6) * crouch + math.radians(14) * math.sin(math.pi * u) * (1 if t < I else 0) - math.radians(10) * land
    elif name == "Hit":
        w = smooth(t / 0.08) * (1 - smooth((t - 0.12) / 0.5)); pitch = math.radians(13) * w; z = -0.14 * w; y = 0.09 * w; roll = math.radians(5) * w
    elif name == "Death":
        j = bump(t / 0.25) * 0.5; e = smooth((t - 0.2) / 1.0)
        z = 0.05 * j - 0.76 * e; roll = math.radians(20) * e; pitch = math.radians(8) * j - math.radians(4) * e
    elif name == "Idle_Loop": z = 0.012 * math.sin(2 * math.pi * t / (L / 2))
    elif name == "Win":
        w = 0.5 - 0.5 * math.cos(2 * math.pi * t / L); pivot = (0, 0.18, BODY_Z); pitch = math.radians(12) * w; z = 0.05 * w
    elif name == "Stun_Loop":
        z = -0.12 + 0.015 * math.sin(2 * math.pi * t / L); roll = math.radians(5) * math.sin(2 * math.pi * t / L); pitch = math.radians(-4)
    p = rotz((x, y, z), byaw)
    return (bx + p[0], by + p[1], bz + p[2], byaw + yaw, pitch, roll, pivot)

def upper_offsets(name, t, L, yawrate=0.0):
    """Extra upper-body bend/side/twist (degrees) on top of the source clip."""
    sp = [0.0, 0.0, 0.0]; hd = [0.0, 0.0, 0.0]
    if name in ("Walk_Loop", "WalkIn"): sp[2] = 4 * math.sin(2 * math.pi * t / 0.8); sp[0] = 3
    elif name == "Turn": tw = 25 * bump(t / 1.8); sp[2] = tw * 0.5; hd[2] = tw * 0.6
    elif name == "Death": e = smooth((t - 0.3) / 1.1); sp[0] = 40 * e; sp[1] = 12 * e; hd[0] = 30 * e; hd[1] = 15 * e
    elif name == "Stun_Loop": sp[0] = 14; hd[0] = 22; hd[1] = 8 * math.sin(2 * math.pi * t / L)
    elif name == "Hit": w = smooth(t / 0.08) * (1 - smooth((t - 0.12) / 0.5)); sp[0] = -12 * w; hd[0] = -14 * w; hd[2] = 10 * w
    return {"spine": tuple(sp), "head": tuple(hd)}

def simulate(name, base=(0, 0, 0, 0), t0=0.0, length_s=None, sub=8, every=None):
    L = length_s or CLIP_LEN[name]; dt = 1.0 / (FPS * sub)
    body_at = lambda tt: clip_body(name, max(0.0, min(L, tt)), L, base)
    st = Stepper(body_at(0)); spring = [0.0, 0.0]; frames = []; prev_yaw = body_at(0)[3]
    I = IMPACT.get(name, 0) * L
    for k in range(int(round(L * FPS * sub)) + 1):
        t = k * dt; body = body_at(t)
        yawrate = (body[3] - prev_yaw) / dt; prev_yaw = body[3]
        if name == "Turn": body = body[:5] + (-0.12 * yawrate,) + body[6:]
        ov = {}
        def raised(leg, fwd, up, outw=1.0):
            s = 1 if leg[0] == "L" else -1; r = REST[leg]
            return body_point((r[0] * 0.6 * outw, r[1] - fwd, BODY_Z + up), body)
        if name == "Idle_Loop":
            st.enabled = set()
            for leg, f, off in IDLE_EVENTS:
                te = f * L; rw = rest_world(leg, body); out = (rw[0] + off[0], rw[1] + off[1], 0)
                for (ta, tb, p0, p1) in ((te, te + 0.3, rw, out), (te + 0.9, te + 1.2, out, rw)):
                    if ta <= t <= tb:
                        u = (t - ta) / 0.3; p = lerp(p0, p1, smooth(u)); ov[leg] = (p[0], p[1], 0.07 * math.sin(math.pi * u)); break
                    if te + 0.3 < t < te + 0.9: ov[leg] = out
        elif name == "Attack":
            st.enabled = set(LEGS) if t > I + 0.15 else set()
            lift = smooth((t - 0.05) / (I - 0.15)); slam = smooth((t - (I - 0.09)) / 0.09)
            for leg in ("L1", "R1", "L2", "R2"):
                hi = leg[1] == "1"; ground = add(rest_world(leg, body), (0, -0.16, 0))
                if t <= I + 0.15:
                    up = raised(leg, 0.05 if hi else -0.2, 0.48 if hi else 0.15)
                    ov[leg] = lerp(lerp(st.feet[leg], up, lift), ground, slam)
                    if t > I: st.feet[leg] = ground
        elif name == "Attack2":
            st.enabled = set(LEGS) if t > I + 0.1 else set(("L2", "R2", "L3", "R3", "L4", "R4"))
            for leg in ("L1", "R1"):
                stab = add(rest_world(leg, body), (0, -0.22, 0)); w = smooth(t / (I * 0.6)); hit = smooth((t - I * 0.6) / (I * 0.4))
                if t <= I + 0.1:
                    ov[leg] = lerp(lerp(st.feet[leg], raised(leg, 0.25, 0.45), w), stab, hit)
                    if t > I: st.feet[leg] = stab
        elif name == "Cast":
            st.enabled = set(); w = smooth(t / 0.35) * (1 - smooth((t - 0.9) / 0.45))
            for leg in ("L1", "R1"): ov[leg] = lerp(st.feet[leg], raised(leg, 0.1, 0.35), w)
        elif name == "Ult_Pounce":
            air = smooth((t - 0.45) / 0.15) * (1 - smooth((t - (I - 0.1)) / 0.1))
            st.enabled = set(LEGS) if t > I + 0.3 else set()
            for leg in LEGS:
                tuck = body_point((REST[leg][0] * 0.55, REST[leg][1] * 0.7, BODY_Z - 0.5), body)
                landing = add(rest_world(leg, clip_body(name, I, L, base)), (0, -0.05 if leg[1] in "12" else 0.0, 0))
                if t < I:
                    ov[leg] = lerp(st.feet[leg], tuck, air)
                    if t > I - 0.1: ov[leg] = lerp(tuck, landing, smooth((t - (I - 0.1)) / 0.1))
                elif t <= I + 0.3:
                    st.feet[leg] = landing; ov[leg] = landing
        elif name == "Hit":
            st.enabled = set(); w = smooth(t / 0.08) * (1 - smooth((t - 0.12) / 0.5))
            for leg in LEGS:
                hip = body_point(ATTACH[leg], body); f = st.feet[leg]
                ov[leg] = add(lerp(f, (hip[0], hip[1], 0), 0.5 * w), (0, 0, 0.12 * w))
        elif name == "Death":
            st.enabled = set(); e = smooth((t - 0.15) / 0.9)
            for leg in LEGS:
                r = REST[leg]; curl = body_point((r[0] * 0.32, r[1] * 0.55, BODY_Z + 0.32), body)
                ov[leg] = lerp(st.feet[leg], curl, e)
        elif name == "Win":
            st.enabled = set(); w = 0.5 - 0.5 * math.cos(2 * math.pi * t / L)
            for i, leg in enumerate(("L1", "R1")):
                wave = 0.08 * math.sin(2 * math.pi * (2 * t / L) + i * math.pi)
                ov[leg] = lerp(st.feet[leg], raised(leg, 0.05, 0.32 + wave), w)
        elif name == "Stun_Loop":
            st.enabled = set()
            for leg in LEGS: ov[leg] = rest_world(leg, body, 1.12)
        else:
            st.enabled = set(LEGS)
        st.update(t, body, body_at, thresh=0.05)
        feet = dict(st.feet); feet.update(ov)
        act = st.active(t)
        bob = sum(math.sin(math.pi * min(1, (t - st.step[l][2]) / st.step[l][3])) for l in act) / 4.0
        body = (body[0], body[1], body[2] - 0.025 * bob) + body[3:]
        target = -0.25 * yawrate + (0.06 * math.sin(2 * math.pi * t / 0.8) if name in ("Walk_Loop", "WalkIn") else 0.0)
        if name in ("Hit", "Attack") : target += -0.15 * bump(t / 0.3)
        spring[1] += (40 * (target - spring[0]) - 9 * spring[1]) * dt; spring[0] += spring[1] * dt
        abd_pitch = 0.03 * math.sin(2 * math.pi * t / 2.0) + (math.radians(18) * smooth((t - 0.2) / 1.0) if name == "Death" else 0)
        if k % (every or sub) == 0:
            frames.append({"t": t0 + t, "body": body, "feet": feet, "abd": (spring[0], abd_pitch, 1.0 + 0.025 * math.sin(2 * math.pi * t / 2.0)),
                           "upper": upper_offsets(name, t, L, yawrate), "stepping": dict(st.step)})
    return frames

def walk_loop():
    """Two seamless walk cycles in place, cut between group-A step starts on a fine clock, resampled to frames."""
    sub = 8
    fr = simulate("Walk_Loop", length_s=4.0, sub=sub, every=1)
    starts = [i for i in range(1, len(fr)) if any(l in fr[i]["stepping"] for l in GROUP_A) and not any(l in fr[i - 1]["stepping"] for l in GROUP_A)]
    a, b = starts[-3], starts[-1]
    period = (b - a) / (FPS * sub); n = max(2, round(period * FPS)); out = []
    for k in range(n):
        f = fr[a + round(k * (b - a) / n)]; bx, by = f["body"][0], f["body"][1]
        g = dict(f); g["body"] = (0.0, 0.0) + f["body"][2:]
        g["feet"] = {l: (p[0] - bx, p[1] - by, p[2]) for l, p in f["feet"].items()}
        g["t"] = k / FPS
        out.append(g)
    return out, period

def clip(name):
    if name == "Walk_Loop": return walk_loop()[0]
    return simulate(name)

if __name__ == "__main__":
    for n in CLIP_LEN:
        f = clip(n); print(n, len(f))
