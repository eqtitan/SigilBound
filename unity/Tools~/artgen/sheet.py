"""Side-by-side contact sheet for owner review: python3 sheet.py OUT.jpg LABEL=img.png [LABEL=img2.png ...]"""
import sys
from PIL import Image, ImageDraw
out, items = sys.argv[1], [a.split("=", 1) for a in sys.argv[2:]]
cell = 400; W = Image.new("RGB", (cell * len(items), cell + 24), "white"); d = ImageDraw.Draw(W)
for i, (label, path) in enumerate(items):
    im = Image.open(path).convert("RGB"); im.thumbnail((cell - 8, cell - 8))
    W.paste(im, (i * cell + (cell - im.width) // 2, 24)); d.text((i * cell + 6, 5), label, fill="black")
W.save(out, quality=88); print(out)
