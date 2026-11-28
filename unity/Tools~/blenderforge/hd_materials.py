"""Accessory materials for the high-detail heroes (accessories review 2026-11-28).

The game builds each hero's mask (R metal, G AO, B glow, A smoothness) from the _glow PNG only, so every hero was
fully matte: steel blades, shield rims, helmets and gold crowns read like cloth. This writes, per hero,
SB_Hero_<id>_HD_mat.png (R metal, G smoothness) covering only the props - weapons on a hand and shields on the
forearm (head gear was tried: kit hair above the skull read as gold, so it is left out) - and inside them only the texels whose albedo is steel-grey or gold;
cloth, leather and wood on the same props stay matte. index_mat.txt lists the heroes with a map (the game only asks
for those).
    python hd_materials.py HEROES_HD_DIR [id ...]      (no ids: every hero; needs numpy + PIL)"""
import sys, os, ctypes, numpy as np
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import glb_io as glbio
from glb_io import weld, components
from prop_rigid import props
from PIL import Image

N = 512
HOLES_MIN = 0.02     # silhouette share that disappears with back-face culling (back/side views)

def tri_fill(uv, F, sel_faces, n=N):
    """Texels covered by the UV triangles of the given faces (numpy scan per triangle bbox)."""
    out = np.zeros((n, n), bool)
    for f in np.nonzero(sel_faces)[0]:
        p = uv[F[f]] * [n, n]
        x0, y0 = np.floor(p.min(0)).astype(int); x1, y1 = np.ceil(p.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, n - 1), min(y1, n - 1)
        if x1 < x0 or y1 < y0: continue
        xs, ys = np.meshgrid(np.arange(x0, x1 + 1) + 0.5, np.arange(y0, y1 + 1) + 0.5)
        a, b, c = p
        d = (b[0] - a[0]) * (c[1] - a[1]) - (b[1] - a[1]) * (c[0] - a[0])
        if abs(d) < 1e-9: out[int((a[1] + b[1] + c[1]) / 3) % n, int((a[0] + b[0] + c[0]) / 3) % n] = True; continue
        w1 = ((xs - a[0]) * (c[1] - a[1]) - (ys - a[1]) * (c[0] - a[0])) / d
        w2 = ((b[0] - a[0]) * (ys - a[1]) - (b[1] - a[1]) * (xs - a[0])) / d
        inside = (w1 >= -0.02) & (w2 >= -0.02) & (w1 + w2 <= 1.04)
        out[y0:y1 + 1, x0:x1 + 1] |= inside
    return out

def accessories(h, P0):
    F = h.F; sel = np.zeros(len(P0), bool)
    for _, s in props(h, P0, F): sel |= s
    return sel

def mat_map(h, tex):
    P0, _ = h.skin()
    acc = accessories(h, P0)
    if acc.sum() == 0: return None, 0
    fsel = np.all(acc[h.F], 1)
    cover = tri_fill(h.UV % 1.0, h.F, fsel)
    t = np.asarray(Image.fromarray(tex).resize((N, N), Image.BILINEAR).convert("HSV")).astype(float) / 255
    hh, ss, vv = t[..., 0], t[..., 1], t[..., 2]
    steel = (ss < 0.18) & (vv > 0.35) & (vv < 0.82)   # grey steel; white paint and bone stay matte
    gold = (hh > 0.08) & (hh < 0.17) & (ss > 0.35) & (vv > 0.45)
    metal = cover & (steel | gold)
    if metal.mean() < 0.0005: return None, 0
    img = np.zeros((N, N, 3), np.uint8)
    # Half metal, very smooth: the painted shader has no reflection probe, so full metal goes dark; this keeps the
    # painted colour and adds a sharp highlight. Other parts of the prop (wood, leather, paint) stay as they were.
    img[..., 0] = np.where(metal, 115, 0)
    img[..., 1] = np.where(metal, 225, 90)
    return Image.fromarray(img), float(metal.mean())

if __name__ == "__main__":
    d = sys.argv[1]; ids = sys.argv[2:] or sorted(f[8:-7] for f in os.listdir(d) if f.startswith("SB_Hero_") and f.endswith("_HD.glb"))
    have = []
    for hid in ids:
        if hid == "titan": continue
        h = glbio.Hero(os.path.join(d, f"SB_Hero_{hid}_HD.glb"))
        ap = os.path.join(d, f"SB_Hero_{hid}_HD_albedo.jpg")
        if not os.path.exists(ap): continue
        img, share = mat_map(h, np.asarray(Image.open(ap).convert("RGB")))
        out = os.path.join(d, f"SB_Hero_{hid}_HD_mat.png")
        if img is None:
            if os.path.exists(out): os.remove(out)
            print(hid, "no metal accessories"); continue
        img.save(out, optimize=True); have.append(hid)
        print(hid, f"metal {share * 100:.1f}% of the map")
    if len(sys.argv) == 2:
        open(os.path.join(d, "index_mat.txt"), "w").write("\n".join(have) + ("\n" if have else ""))
