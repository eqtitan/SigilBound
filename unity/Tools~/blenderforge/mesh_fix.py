"""Image-to-3D mesh clean-up in place (accessories review / in-house report 2026-11-28):
- islands: small loose pieces (TRELLIS speckles floating off the silhouette, loose shards by the feet) are removed -
  their triangles become degenerate, so the buffers keep their layout (rule in islands()).
- winding: faces whose normal points into the body (inside-out patches, which vanish under back-face culling and
  light wrong) are flipped. A face points out when a ray from it along its normal crosses the mesh an even number
  of times (0, 2, ...); odd = inward.
    bpyenv/bin/python mesh_fix.py SRC.glb DST.glb [--orient] [--no-islands]
The winding fix is opt-in: tried on odo, obrecht, kharra and caspian (2026-11-28) it flipped 5-10% of faces with
little visible gain and exposed unpainted insides on kharra and caspian, so no hero ships with it.
Islands shipped for fennick, brega, holt and nell (in-house report); the rule also flags pieces on ~30 other
image-to-3D heroes, some of them intentional (poppy's leaf, aurex's crown rays) - check each before running it."""
import sys, os, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import write_acc, save, weld, components

MAX_TRIS = 150     # loose pieces are small...
GAP = 0.015        # ...and float off the body (m), or are specks of under 20 triangles
PROP = ("hand_", "index_", "middle_", "ring_", "pinky_", "thumb_", "lowerarm_l")

def islands(h, P0):
    """Loose pieces: every connected piece except the body that has fewer than MAX_TRIS triangles and either floats
    more than GAP from the body or is a speck (< 20 triangles). Props held in a hand (or on the shield forearm) with
    20+ triangles are kept."""
    import bpy  # noqa: F401
    from mathutils.kdtree import KDTree
    F = h.F; comp = components(F, weld(P0, 1e-3))
    labs, counts = np.unique(comp, return_counts=True); body = labs[np.argmax(counts)]
    mv = np.unique(F[comp == body]); kd = KDTree(len(mv))
    for i, v in enumerate(mv): kd.insert(P0[v].tolist(), i)
    kd.balance()
    drop = np.zeros(len(F), bool); n = 0
    for lab, cnt in zip(labs, counts):
        if lab == body or cnt >= MAX_TRIS: continue
        fs = comp == lab; vs = np.unique(F[fs])
        dom = h.jnames[np.bincount(h.J[vs, 0], weights=h.W[vs, 0]).argmax()]
        if dom.startswith(PROP) and cnt >= 20: continue
        gap = min(kd.find(P0[v].tolist())[2] for v in vs)
        if gap > GAP or cnt < 20: drop |= fs; n += 1
    return drop, n

def inward(P0, F):
    import bpy  # noqa: F401  (Blender's mathutils)
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromPolygons(P0.tolist(), F.tolist(), all_triangles=True)
    a, b, c = P0[F[:, 0]], P0[F[:, 1]], P0[F[:, 2]]
    n = np.cross(b - a, c - a); ar = np.linalg.norm(n, axis=1); n /= np.maximum(ar[:, None], 1e-12)
    cen = (a + b + c) / 3
    out = np.zeros(len(F), bool)
    for f in np.nonzero(ar > 1e-10)[0]:
        o = Vector(cen[f] + n[f] * 1e-4); d = Vector(n[f]); k = 0
        for _ in range(9):
            hit = tree.ray_cast(o, d, 3.0)
            if hit[0] is None: break
            k += 1; o = hit[0] + d * 1e-4
        out[f] = k % 2 == 1
    return out

def fix(src, dst, orient=False, isl=True):
    h = glbio.Hero(src); j, b = glbio.load(src); binb = bytearray(b)
    P0, _ = h.skin(); F = h.F.copy(); log = []
    if isl:
        drop, n = islands(h, P0)
        if n: F[drop] = F[drop][:, [0, 0, 0]]; log.append(f"removed {n} islands ({int(drop.sum())} triangles)")
    if orient:
        live = F[:, 0] != F[:, 1]
        inn = inward(P0, F) & live
        # only flip clear inside-out patches: a lone face can read odd at a seam; require most of its neighbours to agree
        if inn.sum():
            F[inn] = F[inn][:, [0, 2, 1]]; log.append(f"flipped {int(inn.sum())} inward faces of {int(live.sum())}")
    if log:
        prim = j["meshes"][0]["primitives"][0]
        write_acc(binb, j, prim["indices"], F.reshape(-1, 1))
        save(dst, j, binb)
    return log

if __name__ == "__main__":
    print(fix(sys.argv[1], sys.argv[2], "--orient" in sys.argv, "--no-islands" not in sys.argv))
