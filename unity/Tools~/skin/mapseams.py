"""Removes the vertical fold creases painted into the Act II world map (art review M9: "visible seams on the
Act II map"). The painting came as a folded-parchment map; at the game's size its fold lines read as tiling
seams, two dark 3 px lines at x = 545 and 1024 (of 2048) running the full height inside the frame.

Each crease column is rebuilt row by row: a blend from the clean pixels just left of it to those just
right of it, with the original kept at the frame (the gilded border stays sharp), plus a little of the
parchment grain so it doesn't read as a smear.

    python3 mapseams.py [--src ../../Assets/Resources/Skin/bg_campaign2.jpg] [--check]

--check only reports the crease strength (dark-line contrast against the columns 4 px either side).
"""
import argparse, os
from PIL import Image
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
ap = argparse.ArgumentParser()
ap.add_argument("--src", default=os.path.join(HERE, "..", "..", "Assets", "Resources", "Skin", "bg_campaign2.jpg"))
ap.add_argument("--check", action="store_true")
a = ap.parse_args()

# (first, last) crease columns at 2048 wide, and the frame rows to keep (top, bottom).
CREASES = [(541, 551), (1020, 1029)]
FRAME_Y = (66, 802)


def strength(img):
    g = np.asarray(img.convert("L")).astype(float)[FRAME_Y[0]:FRAME_Y[1]]
    c = g.mean(axis=0); loc = c - (np.roll(c, 4) + np.roll(c, -4)) / 2
    return [round(float(np.abs(loc[x0:x1 + 1]).max()), 1) for x0, x1 in CREASES]


im = Image.open(a.src).convert("RGB")
print("crease contrast before:", strength(im))
if a.check:
    raise SystemExit(0)
px = np.asarray(im).astype(float)
W = px.shape[1]; k = W / 2048.0
rng = np.random.default_rng(9)
for x0, x1 in CREASES:
    x0, x1 = int(x0 * k), int(round(x1 * k))
    left, right = px[:, x0 - 3:x0 - 1].mean(axis=1), px[:, x1 + 2:x1 + 4].mean(axis=1)
    for i, x in enumerate(range(x0, x1 + 1)):
        t = (i + 1) / (x1 - x0 + 2)
        grain = px[:, x - 12] - px[:, x0 - 14:x0 - 10].mean(axis=1)  # texture from a clean column nearby
        px[FRAME_Y[0]:FRAME_Y[1], x] = ((1 - t) * left + t * right + 0.6 * grain)[FRAME_Y[0]:FRAME_Y[1]]
out = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8))
print("crease contrast after:", strength(out))
out.save(a.src, quality=92, subsampling=0)
