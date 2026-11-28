"""Rigid props (accessories review 2026-11-28): weapons on a hand and shields on the left forearm that carry blended
weights (fingers, upper arm, even legs from bone heat on image-to-3D meshes) wobble and stretch in motion. Each
such prop (a connected piece mostly on one hand / the forearm) is bound 100% to that bone, in place.
    python prop_rigid.py SRC.glb DST.glb            prints what it bound"""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save, weld, components

HAND = ("hand_", "index_", "middle_", "ring_", "pinky_", "thumb_")

def props(h, P0, F):
    comp = components(F, weld(P0)); vcomp = np.zeros(len(P0), int)
    for k in range(3): vcomp[F[:, k]] = comp
    def wsum(pref):
        w = np.zeros(len(P0))
        for ji, nm in enumerate(h.jnames):
            if nm.startswith(pref): w += (h.W * (h.J == ji)).sum(1)
        return w
    out = []
    for bone, w, ext in (("hand_r", wsum(tuple(p + "r" if p.endswith("_") else p for p in ("hand_r", "index_", "middle_", "ring_", "pinky_", "thumb_")) ), 0.15),
                         ("hand_l", None, 0.15), ("lowerarm_l", wsum(("lowerarm_l",)), 0.12)):
        if w is None:
            w = np.zeros(len(P0))
            for ji, nm in enumerate(h.jnames):
                if nm == "hand_l" or (nm.startswith(HAND[1:]) and nm.endswith("_l")): w += (h.W * (h.J == ji)).sum(1)
        if bone == "hand_r":
            w = np.zeros(len(P0))
            for ji, nm in enumerate(h.jnames):
                if nm == "hand_r" or (nm.startswith(HAND[1:]) and nm.endswith("_r")): w += (h.W * (h.J == ji)).sum(1)
        for c in np.unique(vcomp[w > 0.9]):
            sel = vcomp == c
            if (w[sel] > 0.5).mean() > 0.8 and np.ptp(P0[sel], 0).max() > ext and sel.sum() < 0.4 * len(P0):
                out.append((bone, sel))
    return out

def fix(src, dst):
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    P0, _ = h.skin(); W = h.W.copy(); Jt = h.J.copy(); log = []
    for bone, sel in props(h, P0, h.F):
        ji = h.jnames.index(bone)
        loose = sel & ~((Jt == ji) & (W > 0.999)).any(1)
        if loose.sum() == 0: continue
        Jt[sel] = [ji, 0, 0, 0]; W[sel] = [1.0, 0, 0, 0]
        log.append(f"{bone}: {int(sel.sum())} verts ({int(loose.sum())} were blended)")
    if log:
        at = j["meshes"][0]["primitives"][0]["attributes"]
        write_acc(binb, j, at["JOINTS_0"], Jt); write_acc(binb, j, at["WEIGHTS_0"], W); save(dst, j, binb)
    return log

if __name__ == "__main__": print(fix(sys.argv[1], sys.argv[2]))
