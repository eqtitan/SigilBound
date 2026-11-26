"""Turns a Grok Imagine UI image on a flat green (#00FF00) background into a game skin piece.

    python3 key.py IN.png NAME [--width 512] [--border PX]

Keys out the green (with a soft edge and green-spill removal), trims to the art, scales to --width,
writes unity/Assets/Resources/Skin/NAME.png and records NAME's 9-slice border in skin.txt
(default: 22% of the shorter side, the part of a frame that must never stretch).
Names the game reads: see Assets/Sigilbound/Client/Skin.cs.
"""
import argparse, os, re
from PIL import Image

ap = argparse.ArgumentParser()
ap.add_argument("src"); ap.add_argument("name"); ap.add_argument("--width", type=int, default=512); ap.add_argument("--border", type=int)
a = ap.parse_args()
im = Image.open(a.src).convert("RGBA")
px = im.load()
for y in range(im.height):
    for x in range(im.width):
        r, g, b, _ = px[x, y]
        green = g - max(r, b)                      # how much greener than it is anything else
        alpha = 255 if green < 40 else 0 if green > 110 else int(255 * (110 - green) / 70)
        if alpha and green > 0: g = max(r, b) + min(green, 12)   # despill the soft edge
        px[x, y] = (r, g, b, alpha)
bbox = im.getchannel("A").getbbox()
if bbox: im = im.crop(bbox)
if im.width > a.width: im = im.resize((a.width, round(im.height * a.width / im.width)), Image.LANCZOS)
here = os.path.dirname(os.path.abspath(__file__))
out = os.path.join(here, "..", "..", "Assets", "Resources", "Skin")
os.makedirs(out, exist_ok=True)
im.save(os.path.join(out, a.name + ".png"))
bd = a.border or round(min(im.size) * 0.22)
spec = os.path.join(out, "skin.txt")
lines = [l for l in (open(spec).read().splitlines() if os.path.exists(spec) else []) if l and not re.match(re.escape(a.name) + r" ", l)]
lines.append(f"{a.name} {bd} {bd} {bd} {bd}")
open(spec, "w").write("\n".join(sorted(lines)) + "\n")
print(f"{a.name}: {im.width}x{im.height}, border {bd}px -> {out}")
