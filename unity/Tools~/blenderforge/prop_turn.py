"""Turn a long prop in its hand (in-house report build 190: Obrecht's 1.7 m pick, held at the middle of its haft and
pointing forward, lay on the ground in front of the next hero in the gallery). The prop (prop_rigid's piece on the
bone) is rotated about the hand joint so its haft runs along the forearm toward the elbow (upright when the arm
hangs), and slid so the hand holds it SHIFT of its length from the butt. Positions and normals rewritten in place.
    python prop_turn.py SRC.glb DST.glb BONE [GRIP=0.3]"""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save
from prop_rigid import props

def rot_to(a, b):
    a = a / np.linalg.norm(a); b = b / np.linalg.norm(b); v = np.cross(a, b); c = a @ b
    if np.linalg.norm(v) < 1e-8: return np.eye(3) if c > 0 else -np.eye(3)
    vx = np.array([[0, -v[2], v[1]], [v[2], 0, -v[0]], [-v[1], v[0], 0]])
    return np.eye(3) + vx + vx @ vx * (1 / (1 + c))

def fix(src, dst, bone, grip=0.3):
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    P0, G = h.skin(); pos = lambda n: G[h.names.index(n)][:3, 3]
    sel = max((s for bn, s in props(h, P0, h.F) if bn == bone), key=lambda s: s.sum())
    hand = pos(bone); elbow = pos("lowerarm_" + bone[-1])
    Q = h.V[sel]; c = Q.mean(0); _, _, vt = np.linalg.svd(Q - c); ax = vt[0]
    t = (Q - c) @ ax; head_end = np.sign(np.abs(t.max()) - np.abs(t.min())) or 1.0
    # the heavier (wider) end is the head: compare spread at both ends
    w_hi = np.linalg.norm((Q - c)[t > t.max() * 0.7] - np.outer(t[t > t.max() * 0.7], ax), axis=1).mean()
    w_lo = np.linalg.norm((Q - c)[t < t.min() * 0.7] - np.outer(t[t < t.min() * 0.7], ax), axis=1).mean()
    if w_lo > w_hi: ax = -ax; t = -t
    toward = (elbow - hand) / np.linalg.norm(elbow - hand)
    R = rot_to(ax, toward)
    L = t.max() - t.min(); g = c + ax * (t.min() + grip * L)   # grip point on the haft before turning
    V = h.V.copy(); V[sel] = (V[sel] - g) @ R.T + hand
    N = h.N.copy(); N[sel] = N[sel] @ R.T
    at = j["meshes"][0]["primitives"][0]["attributes"]
    write_acc(binb, j, at["POSITION"], V); write_acc(binb, j, at["NORMAL"], N); save(dst, j, binb)
    return int(sel.sum()), round(float(L), 2)

if __name__ == "__main__":
    a = sys.argv[1:]; print(fix(a[0], a[1], a[2], *(float(x) for x in a[3:4])))
