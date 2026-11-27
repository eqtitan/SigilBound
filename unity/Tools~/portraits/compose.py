"""Composite raw RGBA bust renders onto an element backdrop -> game portraits (3:4 JPEG) and a review sheet.
Usage: python3 compose.py GAMEDATA_CS RAW_DIR OUT_DIR SHEET_JPG"""
import sys, os, re, math
from PIL import Image, ImageDraw, ImageFilter

gd, raw, out, sheet = sys.argv[1:5]
os.makedirs(out, exist_ok=True)
src = open(gd, encoding="utf-8").read()
EL = {m.group(1): m.group(2) for m in re.finditer(r'Id = "([a-z]+)".*?Element = Element\.(\w+)', src)}
EL.update({m.group(1): m.group(2) for m in re.finditer(r'H\("([a-z]+)", "[^"]*", Rarity\.\w+, Element\.(\w+)', src)})
COL = {  # element tints, close to Palette.Element
    "Flame": (232, 96, 60), "Gale": (96, 200, 160), "Tide": (70, 150, 230),
    "Light": (240, 200, 90), "Shadow": (160, 100, 220),
}
W, H = 384, 512

def backdrop(c):
    img = Image.new("RGB", (W, H), (18, 14, 24))
    px = img.load()
    cx, cy = W * 0.5, H * 0.30
    for y in range(H):
        for x in range(W):
            d = math.hypot((x - cx) / W, (y - cy) / H)
            glow = max(0.0, 1.0 - d * 1.9) ** 1.6
            base = 0.20 + 0.25 * (1 - y / H)          # darker toward the bottom
            k = base * 0.55 + glow * 0.75
            px[x, y] = tuple(int(min(255, 16 + c[i] * k * 0.8)) for i in range(3))
    # faint diagonal brush streaks
    d = ImageDraw.Draw(img, "RGBA")
    for i in range(14):
        x0 = (i * 53) % W - 80
        d.line([(x0, H), (x0 + 260, 0)], fill=(255, 255, 255, 7), width=18)
    return img.filter(ImageFilter.GaussianBlur(1.2))

def vignette(img):
    v = Image.new("L", (W, H), 0); d = ImageDraw.Draw(v)
    d.ellipse([-W * 0.25, -H * 0.15, W * 1.25, H * 1.2], fill=255)
    v = v.filter(ImageFilter.GaussianBlur(60))
    dark = Image.new("RGB", (W, H), (8, 6, 12))
    return Image.composite(img, dark, v)

cache = {}
ids = sorted(f[:-4] for f in os.listdir(raw) if f.endswith(".png"))
tiles = []
for hid in ids:
    el = EL.get(hid, "Shadow")
    if el not in cache: cache[el] = backdrop(COL[el])
    bg = cache[el].copy()
    fig = Image.open(os.path.join(raw, hid + ".png")).convert("RGBA")
    # soft dark halo under the figure so it separates from the glow
    a = fig.split()[3].filter(ImageFilter.GaussianBlur(10))
    halo = Image.new("RGBA", (W, H), (0, 0, 0, 0)); halo.putalpha(a.point(lambda v: int(v * 0.45)))
    bg = Image.alpha_composite(bg.convert("RGBA"), halo)
    bg = Image.alpha_composite(bg, fig).convert("RGB")
    bg = vignette(bg)
    bg.save(os.path.join(out, f"portrait_{hid}.jpg"), quality=82, optimize=True)
    tiles.append(bg)

# review sheet: 12 per row, 128x170 thumbs
tw, th, per = 128, 170, 12
rows = math.ceil(len(tiles) / per)
sh = Image.new("RGB", (tw * per, th * rows), (12, 10, 16))
for i, t in enumerate(tiles):
    sh.paste(t.resize((tw, th), Image.LANCZOS), ((i % per) * tw, (i // per) * th))
sh.save(sheet, quality=85)
print("composed", len(tiles))
