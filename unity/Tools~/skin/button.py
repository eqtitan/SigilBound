"""Turns a Grok Imagine button plate (wide, rounded ends, ornaments centred top and bottom, flat green
background) into the two textures the game draws (Skin.cs):
  NAME.png      the plate with its centre ornaments replaced by plain band, so it can stretch to any width
  NAME_orn.png  (with --orn) the centre ornaments alone, drawn unstretched at the middle of the button

    python3 button.py IN.png NAME [--orn] [--height 200]

Fractions below were measured on the 2172x724 Grok plates (2026-11-26); they hold for any size of the
same layout. The 9-slice border goes into skin.txt.
"""
import argparse, os, re
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("name"); ap.add_argument("--orn", action="store_true"); ap.add_argument("--height", type=int, default=200)
a = ap.parse_args()
im = Image.open(a.src).convert("RGBA"); W, H = im.size
px = im.load()
for y in range(H):
    for x in range(W):
        r, g, b, _ = px[x, y]
        green = g - max(r, b)
        alpha = 255 if green < 40 else 0 if green > 110 else int(255 * (110 - green) / 70)
        if alpha and green > 0: g = max(r, b) + min(green, 12)
        px[x, y] = (r, g, b, alpha)
o0, o1 = int(W * 0.380), int(W * 0.620)          # the centre ornament columns
t0, t1 = int(W * 0.626), int(W * 0.746)          # plain band + plate to tile over them
plate_top, plate_bot = int(H * 0.326), int(H * 0.671)
if a.orn:
    orn = im.crop((o0, 0, o1, H)); op = orn.load()
    for y in range(plate_top, plate_bot):
        for x in range(orn.width): op[x, y] = (0, 0, 0, 0)
frame = im.copy()
# Patch only the band rows above and below the plate: the plate keeps its own lighting (no seam).
for y0, y1 in ((0, plate_top), (plate_bot, H)):
    tile = im.crop((t0, y0, t1, y1)); x = o0
    while x < o1:
        w = min(tile.width, o1 - x); frame.paste(tile.crop((0, 0, w, y1 - y0)), (x, y0)); x += w
k = a.height / H
size = lambda img: img.resize((max(1, round(img.width * k)), a.height), Image.LANCZOS)
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Skin")
os.makedirs(out, exist_ok=True)
fr = size(frame); fr.save(os.path.join(out, a.name + ".png"))
if a.orn: size(orn).save(os.path.join(out, a.name + "_orn.png"))
side = round(W * 0.111 * k); top = round(plate_top * k); bot = round((H - plate_bot) * k)
spec = os.path.join(out, "skin.txt")
lines = [l for l in (open(spec).read().splitlines() if os.path.exists(spec) else []) if l and not re.match(re.escape(a.name) + " ", l)]
lines.append(f"{a.name} {side} {top} {side} {bot}")
open(spec, "w").write("\n".join(sorted(lines)) + "\n")
print(f"{a.name}: {fr.size}, border L/R {side} T {top} B {bot}" + (" + ornament" if a.orn else ""))
