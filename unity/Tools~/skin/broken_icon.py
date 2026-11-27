# Usage: python3 unity/Tools~/skin/broken_icon.py unity/Assets/Resources/StatusIcons/Broken.png
# Broken status icon (PLAN step 99): the same orange rim and fill as the old icon, with a cracked
# diamond sigil in place of Stun's orbiting stars, so Broken no longer reads as Stun.
import sys
from PIL import Image, ImageDraw
S = 512; k = S / 128
img = Image.new("RGBA", (S, S), (40, 10, 0, 0)); d = ImageDraw.Draw(img)
d.ellipse([3*k, 3*k, 125*k, 125*k], fill=(218, 89, 19, 255))
d.ellipse([11*k, 11*k, 117*k, 117*k], fill=(85, 30, 5, 255))
def P(pts): return [(x*k, y*k) for x, y in pts]
cream = (255, 214, 150, 255); glow = (255, 160, 60, 255)
# crack line through the diamond: zigzag from top-right to bottom-left
crack = [(78, 26), (66, 50), (74, 58), (56, 76), (62, 84), (50, 102)]
left = [(64, 24), (78, 26), (66, 50), (74, 58), (56, 76), (62, 84), (50, 102), (64, 104), (24, 64)]
right = [(64, 24), (104, 64), (64, 104), (50, 102), (62, 84), (56, 76), (74, 58), (66, 50), (78, 26)]
# the two halves pushed apart (left up-left, right down-right)
dx = 4
L = [(x - dx, y - dx * 0.6) for x, y in left]; R = [(x + dx, y + dx * 0.6) for x, y in right]
for poly in (L, R):
    d.polygon(P(poly), fill=cream, outline=glow)
    d.line(P(poly + [poly[0]]), fill=glow, width=int(3*k))
# inner sigil cut-out (a small diamond), split too
inner = [(64, 48), (80, 64), (64, 80), (48, 64)]
d.polygon(P([(x - dx * 0.5, y) for x, y in inner]), fill=(85, 30, 5, 255))
# the crack glows in the gap
d.line(P(crack), fill=(255, 245, 220, 255), width=int(2.4*k), joint="curve")
# flying shards
for tri in ([(98, 30), (106, 26), (102, 36)], [(26, 96), (34, 100), (24, 104)], [(92, 98), (100, 96), (96, 106)]):
    d.polygon(P(tri), fill=cream)
img = img.resize((128, 128), Image.LANCZOS)
img.save(sys.argv[1])
