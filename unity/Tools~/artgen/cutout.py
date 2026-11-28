"""Remove a flat plain background (light grey/white) from a generated image -> RGBA PNG for image-to-3D.

    python3 cutout.py IN.png OUT.png [threshold=28]

TRELLIS sometimes keeps a flat 'backdrop card' from the background; sending a cut-out avoids it (Poppy needed this).
Flood-fills from the corners and edge midpoints, so the subject must not touch the image border.
"""
import sys
import numpy as np
from PIL import Image, ImageDraw, ImageFilter

src, out = sys.argv[1], sys.argv[2]
th = int(sys.argv[3]) if len(sys.argv) > 3 else 28
im = Image.open(src).convert("RGB"); W, H = im.size
m = im.copy()
for c in [(0, 0), (W - 1, 0), (0, H - 1), (W - 1, H - 1), (W // 2, 0), (W // 2, H - 1), (0, H // 2), (W - 1, H // 2)]:
    ImageDraw.floodfill(m, c, (255, 0, 255), thresh=th)
a = np.asarray(m); bg = (a[..., 0] == 255) & (a[..., 1] == 0) & (a[..., 2] == 255)
alpha = Image.fromarray(((~bg) * 255).astype(np.uint8)).filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1))
res = im.convert("RGBA"); res.putalpha(alpha); res.save(out)
print(f"{out}: background {bg.mean():.0%} of the image")
