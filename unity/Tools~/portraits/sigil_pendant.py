"""Replace the cross pendant on three portraits with a round Sigil medallion.
usage: fix.py SRC_DIR OUT_DIR"""
import sys, math, os
import numpy as np, cv2
from PIL import Image, ImageDraw, ImageFilter
src, out = sys.argv[1], sys.argv[2]; os.makedirs(out, exist_ok=True)
# name: cross bbox (x0,y0,x1,y1) in 384x512 portrait, medallion centre, radius
SPOTS = {"agathe": ((210, 304, 244, 349), (227, 319), 13),
         "isla":   ((159, 244, 194, 296), (177, 264), 14),
         "wren":   ((196, 296, 230, 334), (213, 307), 12)}
def medallion(r, base, S=8):
    R = r * S; W = 2 * R + 4 * S
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0)); d = ImageDraw.Draw(im); c = W / 2
    k = float(np.clip(sum(base) / 640.0, 0.62, 1.0))       # the portrait's local light level
    g = lambda c: tuple(int(v * k) for v in c)
    hi, mid, dk = g((252, 214, 120)), g((206, 152, 58)), g((92, 58, 18))
    d.ellipse([c - R, c - R, c + R, c + R], fill=dk + (255,))                         # rim
    for i in range(24):                                                               # shaded face
        t = i / 23; rr = R * 0.86 * (1 - t * 0.9)
        col = tuple(int(mid[k] * (1 - t) + hi[k] * t) for k in range(3))
        ox, oy = -R * 0.18 * t, -R * 0.22 * t
        d.ellipse([c + ox - rr, c + oy - rr, c + ox + rr, c + oy + rr], fill=col + (255,))
    # engraved sigil: ring + eight-point star + centre dot
    lw = max(1, int(R * 0.09))
    d.ellipse([c - R * 0.62, c - R * 0.62, c + R * 0.62, c + R * 0.62], outline=dk + (255,), width=lw)
    pts = []
    for k in range(16):
        a = -math.pi / 2 + k * math.pi / 8; rr = R * (0.56 if k % 2 == 0 else 0.24)
        pts.append((c + rr * math.cos(a), c + rr * math.sin(a)))
    d.polygon(pts, outline=dk + (255,), width=lw)
    d.ellipse([c - R * 0.12, c - R * 0.12, c + R * 0.12, c + R * 0.12], fill=dk + (255,))
    # bail loop on top
    d.ellipse([c - R * 0.22, c - R - R * 0.38, c + R * 0.22, c - R + R * 0.06], outline=dk + (255,), width=int(lw * 1.6))
    return im.resize((W // S, W // S), Image.LANCZOS)
for name, (bb, (cx, cy), r) in SPOTS.items():
    img = Image.open(os.path.join(src, f"portrait_{name}.jpg")).convert("RGB")
    a = np.array(img)
    region = a[bb[1]:bb[3], bb[0]:bb[2]].reshape(-1, 3).astype(float)
    # the cross is the brightest/most saturated gold in its box: use the top 25% for the medallion base colour
    lum = region.sum(1); base = region[lum >= np.percentile(lum, 75)].mean(0)
    mask = np.zeros(a.shape[:2], np.uint8); cv2.rectangle(mask, bb[:2], (bb[2], bb[3]), 255, -1)
    bgr = cv2.inpaint(cv2.cvtColor(a, cv2.COLOR_RGB2BGR), mask, 6, cv2.INPAINT_TELEA)
    img = Image.fromarray(cv2.cvtColor(bgr, cv2.COLOR_BGR2RGB))
    # re-blend the inpainted box softly so its edge doesn't show
    orig = Image.fromarray(a); soft = Image.fromarray(mask).filter(ImageFilter.GaussianBlur(2))
    img = Image.composite(img, orig, soft)
    m = medallion(r, base)
    sh = Image.new("RGBA", m.size, (0, 0, 0, 0)); sh.putalpha(m.split()[3].point(lambda v: int(v * 0.45)))
    sh = sh.filter(ImageFilter.GaussianBlur(1.5))
    img = img.convert("RGBA")
    img.alpha_composite(sh, (cx - m.width // 2 + 1, cy - m.height // 2 + 2))
    img.alpha_composite(m, (cx - m.width // 2, cy - m.height // 2))
    img.convert("RGB").save(os.path.join(out, f"portrait_{name}.jpg"), quality=90)
    print(name, "base", base.round())
