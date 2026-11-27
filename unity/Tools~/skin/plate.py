"""Grok Imagine UI art on a flat green background -> the textures Skin.cs draws. Unlike button.py
(fixed fractions for the first plates), this finds the plate and its ornaments itself.

  python3 plate.py IN.png NAME button [--height 200]   pill button: NAME.png (stretchable plate with the
                                                        centre ornaments patched out) + NAME_orn.png
  python3 plate.py IN.png NAME frame  [--width 400]    panel/card frame: corners kept, the mid-edge
                                                        ornaments patched out so edges can stretch
Writes into Assets/Resources/Skin and updates skin.txt (9-slice borders, texture pixels).
"""
import argparse, os, re
from PIL import Image, ImageEnhance

ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("name"); ap.add_argument("kind", choices=["button", "frame"])
ap.add_argument("--height", type=int, default=200); ap.add_argument("--width", type=int, default=400)
ap.add_argument("--out", default=os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Skin"))
ap.add_argument("--brighten", type=float, default=1.0)
a = ap.parse_args()

im = Image.open(a.src).convert("RGBA"); W, H = im.size; px = im.load()
for y in range(H):                      # key out the green, de-spill the edges
    for x in range(W):
        r, g, b, _ = px[x, y]
        green = g - max(r, b)
        alpha = 255 if green < 40 else 0 if green > 110 else int(255 * (110 - green) / 70)
        if alpha and green > 0: g = max(r, b) + min(green, 12)
        px[x, y] = (r, g, b, alpha)
bbox = im.getbbox(); im = im.crop(bbox); W, H = im.size; px = im.load()
if a.brighten != 1.0:
    rgb, al = im.convert("RGB"), im.split()[3]
    im = ImageEnhance.Brightness(rgb).enhance(a.brighten).convert("RGBA"); im.putalpha(al); px = im.load()

def solid(x, y): return px[x, y][3] > 128
def row_cover(y): return sum(1 for x in range(0, W, 2) if solid(x, y)) / (W / 2)
def col_cover(x, y0, y1): return sum(1 for y in range(y0, y1) if solid(x, y))

def patch(img, box, src_box):
    """Fill box with copies of src_box tiled horizontally (or vertically if box is tall)."""
    x0, y0, x1, y1 = box; tile = img.crop(src_box)
    if (x1 - x0) >= (y1 - y0):
        x = x0
        while x < x1:
            w = min(tile.width, x1 - x); img.paste(tile.crop((0, 0, w, tile.height)), (x, y0)); x += w
    else:
        y = y0
        while y < y1:
            h = min(tile.height, y1 - y); img.paste(tile.crop((0, 0, tile.width, h)), (x0, y)); y += h

out = os.path.abspath(a.out); os.makedirs(out, exist_ok=True)
spec = os.path.join(out, "skin.txt")

if a.kind == "button":
    # The plate: the rows where the art spans most of the width.
    rows = [y for y in range(H) if row_cover(y) > 0.80]
    pt, pb = rows[0], rows[-1] + 1
    # Centre ornaments: columns around the middle with art above the plate.
    mid = W // 2; o0 = o1 = mid
    while o0 > W * 0.2 and col_cover(o0, 0, pt) > 2: o0 -= 1
    while o1 < W * 0.8 and col_cover(o1, 0, pt) > 2: o1 += 1
    o0 -= 4; o1 += 4
    orn = im.crop((o0, 0, o1, H)); op = orn.load()
    for y in range(pt, pb):
        for x in range(orn.width): op[x, y] = (0, 0, 0, 0)
    frame = im.copy()
    tw = max(8, int(W * 0.06))
    for y0, y1 in ((0, pt), (pb, H)):            # band rows above/below the plate only: no lighting seam
        patch(frame, (o0, y0, o1, y1), (o1 + 2, y0, o1 + 2 + tw, y1))
    # Side border: the rounded end cap (and its gem) never stretch.
    cap = 0
    for x in range(W // 3):
        if col_cover(x, pt, pb) > (pb - pt) * 0.9: cap = x; break
    side = int(cap + (pb - pt) * 0.55)
    k = a.height / H
    size = lambda img: img.resize((max(1, round(img.width * k)), a.height), Image.LANCZOS)
    size(frame).save(os.path.join(out, a.name + ".png")); size(orn).save(os.path.join(out, a.name + "_orn.png"))
    border = (round(side * k), round(pt * k), round(side * k), round((H - pb) * k))
else:
    # Frame: corners hold the big ornaments; the mid-edge ornaments are patched with plain edge.
    cx, cy = W // 2, H // 2
    ow, oh = int(W * 0.18), int(H * 0.22)       # half-width of the top/bottom centre ornament, side ornament half-height
    edge_t, edge_b = int(H * 0.21), int(H * 0.79)
    edge_l, edge_r = int(W * 0.085), int(W * 0.915)
    frame = im.copy()
    # Fill from a narrow plain strip of the same edge, so the edge reads as one uniform band and
    # compresses cleanly when a short card squeezes the stretch zone.
    sx = cx + ow + 30; sy = cy + oh + 40
    patch(frame, (cx - ow, 0, cx + ow, edge_t), (sx, 0, sx + 4, edge_t))
    patch(frame, (cx - ow, edge_b, cx + ow, H), (sx, edge_b, sx + 4, H))
    patch(frame, (0, cy - oh, edge_l, cy + oh), (0, sy, edge_l, sy + 4))
    patch(frame, (edge_r, cy - oh, W, cy + oh), (edge_r, sy, W, sy + 4))
    # The whole stretch zone of each edge gets the same strip (the corners keep their ornaments).
    c0, c1 = int(W * 0.20), int(W * 0.80); r0, r1 = int(H * 0.27), int(H * 0.73)
    patch(frame, (c0, 0, c1, edge_t), (sx, 0, sx + 4, edge_t)); patch(frame, (c0, edge_b, c1, H), (sx, edge_b, sx + 4, H))
    patch(frame, (0, r0, edge_l, r1), (0, sy, edge_l, sy + 4)); patch(frame, (edge_r, r0, W, r1), (edge_r, sy, W, sy + 4))
    k = a.width / W
    fr = frame.resize((a.width, round(H * k)), Image.LANCZOS); fr.save(os.path.join(out, a.name + ".png"))
    c = round(W * 0.19 * k)
    border = (c, c, c, c)

lines = [l for l in (open(spec).read().splitlines() if os.path.exists(spec) else []) if l and not re.match(re.escape(a.name) + " ", l)]
lines.append(f"{a.name} {border[0]} {border[1]} {border[2]} {border[3]}")
open(spec, "w").write("\n".join(sorted(lines)) + "\n")
print(a.name, "border", border)
