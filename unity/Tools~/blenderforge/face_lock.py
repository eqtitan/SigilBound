"""Face-only lock for shipped HD heroes (character TD faces pass, 2026-11-28), written in place (only JOINTS_0 and
WEIGHTS_0 change; nothing else in the file moves, so it can be re-run on top of any later re-rig).

Owner report: faces "warped or not bilateral". The earlier head lock (every head vertex above the jaw to Head) was
rejected because manes, hoods and shells then turned rigidly with the head into the shoulders. This one touches only a
tight face mask (eyes, nose, mouth, cheeks, forehead), per hero in face_lock.json:

  mask t (0..1) = soft ellipse in the front view (centre cx / Head_y + cy, radii rx, ry; full inside `core`, smoothstep
  to 0 at the rim; the lower half reaches `chin` x ry for the jaw/neck blend)
  x front layer (within dz of the front-most surface at that x, y: the skin you see, not hair or a hood behind it;
  hands and props in front of the face are ignored for that test)
  x not on hand/finger/prop/spring/tail/wing bones.
  Welded duplicates share one t.

New weights = (1 - t) * old + t * target, top 4, renormalised. target:
  "head": Head 100% (kit heroes: their faces are Head already, only the chin blended into neck_01, 2-7 mm drift);
  "avg":  the mean of the face's own weights over the mask core (image-to-3D heroes: the face then moves as one piece
          with the skull, hood or mane around it instead of warping; locking it to Head made it slide against them,
          e.g. holes opening at Odo's frog hood and Lazlo's shark head).

    python face_lock.py face_lock.json SRC_DIR DST_DIR [id ...]      (default: every id in the table)
"""
import os, sys, json, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io

SKIP_BONES = ("hand", "index", "middle", "ring", "pinky", "thumb", "neutral_bone", "sb_", "tail", "wing", "leg_")
DEF = {"cx": None, "cy": 0.085, "rx": 0.07, "ry": 0.095, "core": 0.6, "chin": 1.25, "dz": 0.02, "auto_cx": True, "mode": "head"}
N = 320

def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)

def front_depth(P, F, box):
    """Front (looking down -Z) z-buffer over box (cx, cy, half): front-most z per pixel, -inf where empty."""
    bx, by, hh = box
    X = (P[:, 0] - (bx - hh)) / (2 * hh) * N; Y = (by + hh - P[:, 1]) / (2 * hh) * N
    Z = np.full((N, N), -np.inf)
    fx, fy = X[F], Y[F]
    keep = (fx.max(1) >= 0) & (fx.min(1) < N) & (fy.max(1) >= 0) & (fy.min(1) < N)
    for f in np.nonzero(keep)[0]:
        x0, x1, x2 = fx[f]; y0, y1, y2 = fy[f]
        area = (x1 - x0) * (y2 - y0) - (y1 - y0) * (x2 - x0)
        if abs(area) < 1e-12: continue
        i0, i1 = max(0, int(np.floor(min(fx[f])))), min(N - 1, int(np.ceil(max(fx[f]))))
        j0, j1 = max(0, int(np.floor(min(fy[f])))), min(N - 1, int(np.ceil(max(fy[f]))))
        if i1 < i0 or j1 < j0: continue
        gx, gy = np.meshgrid(np.arange(i0, i1 + 1) + 0.5, np.arange(j0, j1 + 1) + 0.5)
        w0 = ((x2 - x1) * (gy - y1) - (y2 - y1) * (gx - x1)) / area
        w1 = ((x0 - x2) * (gy - y2) - (y0 - y2) * (gx - x2)) / area
        w2 = 1 - w0 - w1
        ins = (w0 >= 0) & (w1 >= 0) & (w2 >= 0)
        if not ins.any(): continue
        z = w0 * P[F[f, 0], 2] + w1 * P[F[f, 1], 2] + w2 * P[F[f, 2], 2]
        sub = Z[j0:j1 + 1, i0:i1 + 1]
        np.copyto(sub, np.where(ins & (z > sub), z, sub))
    def look(x, y):
        i = np.clip(((x - (bx - hh)) / (2 * hh) * N).astype(int), 0, N - 1)
        j = np.clip(((by + hh - y) / (2 * hh) * N).astype(int), 0, N - 1)
        inb = (y <= by + hh) & (y >= by - hh) & (x >= bx - hh) & (x <= bx + hh)
        return np.where(inb, Z[j, i], -np.inf)
    return look

def mask(h, p):
    P0, G0 = h.skin(); hp = G0[h.names.index("Head")][:3, 3]
    cx = hp[0] if p["cx"] is None else p["cx"]; cy = hp[1] + p["cy"]; rx, ry = p["rx"], p["ry"]
    dom = h.J[np.arange(len(P0)), h.W.argmax(1)]
    skip = np.array([n.startswith(SKIP_BONES) for n in h.jnames])[dom]
    if p["auto_cx"]:   # midline through the most forward point (nose / muzzle / beak) near the guess
        q = (np.abs(P0[:, 0] - cx) < rx * 0.7) & (np.abs(P0[:, 1] - cy) < ry * 0.9) & ~skip
        if q.sum() > 20:
            zq = P0[q, 2]; cx = float(np.median(P0[q][zq >= np.percentile(zq, 97), 0]))
    dx = (P0[:, 0] - cx) / rx; dy = (P0[:, 1] - cy) / ry; dy = np.where(dy < 0, dy / p["chin"], dy)
    t = 1 - smooth(p["core"], 1.0, np.sqrt(dx * dx + dy * dy))
    Pr = P0.copy(); Pr[np.unique(h.F[skip[h.F].any(1)])] += np.array([0, 0, -50.0])   # hands/props don't hide the face
    look = front_depth(Pr, h.F, (cx, cy, max(rx, ry) * 1.6))
    cand = t > 0; zf = np.full(len(P0), -np.inf); zf[cand] = look(P0[cand, 0], P0[cand, 1])
    t *= 1 - smooth(p["dz"], 2 * p["dz"], zf - P0[:, 2])
    t[skip] = 0
    wid = glb_io.weld(P0); tw = np.zeros(wid.max() + 1); np.maximum.at(tw, wid, t)
    return tw[wid]

def lock(h, t, mode):
    hj = h.jnames.index("Head"); W = h.W.copy(); J = h.J.copy()
    if mode == "head": tg = {hj: 1.0}
    else:
        acc = {}
        for i in np.nonzero(t >= 0.9)[0]:
            for k in range(4):
                if W[i, k] > 0: acc[int(J[i, k])] = acc.get(int(J[i, k]), 0) + W[i, k]
        top = sorted(acc.items(), key=lambda kv: -kv[1])[:4]; s = sum(w for _, w in top); tg = {k: w / s for k, w in top}
    idx = np.nonzero(t > 1e-3)[0]
    for i in idx:
        ws = {}
        for k in range(4):
            if W[i, k] > 0: ws[int(J[i, k])] = ws.get(int(J[i, k]), 0) + W[i, k] * (1 - t[i])
        for b, w in tg.items(): ws[b] = ws.get(b, 0) + w * t[i]
        top = sorted(ws.items(), key=lambda kv: -kv[1])[:4]; s = sum(w for _, w in top)
        J[i] = [k for k, _ in top] + [0] * (4 - len(top)); W[i] = [w / s for _, w in top] + [0.0] * (4 - len(top))
    return W, J, len(idx)

def patch(src, dst, p):
    h = glb_io.Hero(src); t = mask(h, p); W, J, n = lock(h, t, p["mode"])
    j, b = glb_io.load(src); binb = bytearray(b)
    mi = next(i for i, nd in enumerate(j["nodes"]) if "mesh" in nd and "skin" in nd)
    at = j["meshes"][j["nodes"][mi]["mesh"]]["primitives"][0]["attributes"]
    glb_io.write_acc(binb, j, at["JOINTS_0"], J); glb_io.write_acc(binb, j, at["WEIGHTS_0"], W)
    glb_io.save(dst, j, binb)
    return n

if __name__ == "__main__":
    tab = json.load(open(sys.argv[1])); src, dst = sys.argv[2:4]; os.makedirs(dst, exist_ok=True)
    ids = sys.argv[4:] or [k for k in tab if not k.startswith("_")]
    for hid in ids:
        p = dict(DEF); p.update(tab.get("_default", {})); p.update(tab.get(hid, {}))
        f = f"SB_Hero_{hid}_HD.glb"
        print(hid, p["mode"], patch(os.path.join(src, f), os.path.join(dst, f), p), "verts", flush=True)
