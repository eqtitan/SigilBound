"""Shield on the left forearm of a kit hero: slide it toward the fist by at most MAX m (clip/glbpatch.py rule, capped:
the uncapped slide (33 cm on hask) left a tower shield held by its top edge). In place, nothing else changes.
    python shield_slide.py SRC DST MAX       (hask: 0.20)"""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save, weld, components

def fix(src, dst, mx):
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    G = h.globals_(h.rest); pos = lambda nm: G[h.names.index(nm)][:3, 3]
    V = h.V.copy(); W = h.W; Jt = h.J
    la = h.jnames.index("lowerarm_l"); wl = (W * (Jt == la)).sum(1)
    lh, hl = pos("lowerarm_l"), pos("hand_l"); fl = np.linalg.norm(hl - lh); along = (hl - lh) / fl
    wid = weld(V); comp = components(h.F, wid)
    vcomp = np.zeros(len(V), int)
    for k in range(3): vcomp[h.F[:, k]] = comp
    rel = V - lh; ax = rel @ along; d = np.linalg.norm(rel - np.outer(ax, along), axis=1)
    sel = np.zeros(len(V), bool)
    for c in np.unique(vcomp[wl > 0.999]):
        m = vcomp == c
        if (wl[m] > 0.999).mean() > 0.9 and np.median(d[m]) > 0.05 and np.ptp(ax[m]) > 0.12: sel |= m
    c_old = lh + along * fl * 0.5; ylo = ((V[sel] - c_old) @ along).min()
    shift = min(mx, max(0.0, -ylo - 0.04 - fl * 0.5))
    V[sel] += along * shift
    at = j["meshes"][0]["primitives"][0]["attributes"]
    write_acc(binb, j, at["POSITION"], V); save(dst, j, binb)
    return int(sel.sum()), round(shift * 100, 1)

if __name__ == "__main__": print(fix(sys.argv[1], sys.argv[2], float(sys.argv[3])))
