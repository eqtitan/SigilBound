"""Owner Grok art set 2 (Google AI) -> Resources/Skin. Repeatable when the art is regenerated.

  python3 sheet2.py SHEET.jpg CITADEL.jpg ALTAR.jpg MAP.jpg

SHEET: green-screen sheet with a ribbon banner (top), a bronze 3:4 card frame (left) and a 5x5 icon
grid (right) in ICONS order. Each part is found by its own bounding box inside a search window, so
small layout shifts don't matter. Backgrounds are resized to 2048 px wide JPEGs.
"""
import os, sys
from PIL import Image, ImageFilter

ICONS = ["coin", "shard", "essence", "tome", "star",
         "home", "map", "helm", "portal", "tower",
         "trophy", "heart", "bolt", "sword", "shield",
         "clock", "crown", "skull", "gear", "camera",
         "pause", "speed", "loop", "flame", "tide"]

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Skin")

def key(im):
    """Green key with the same de-spill as plate.py."""
    im = im.convert("RGBA"); px = im.load(); W, H = im.size
    for y in range(H):
        for x in range(W):
            r, g, b, _ = px[x, y]
            green = g - max(r, b)
            alpha = 255 if green < 40 else 0 if green > 110 else int(255 * (110 - green) / 70)
            if alpha and green > 0: g = max(r, b) + min(green, 12)
            px[x, y] = (r, g, b, alpha)
    return im

def part(im, box, pad=2):
    c = im.crop(box); bb = c.split()[3].point(lambda v: 255 if v > 40 else 0).getbbox()
    if not bb: raise SystemExit(f"nothing found in {box}")
    l, t, r, b = bb
    return c.crop((max(0, l - pad), max(0, t - pad), min(c.width, r + pad), min(c.height, b + pad)))

def up(img, w=None, h=None):
    if w: h = round(img.height * w / img.width)
    else: w = round(img.width * h / img.height)
    return img.resize((w, h), Image.LANCZOS).filter(ImageFilter.UnsharpMask(1.2, 60, 2))

def main(sheet, citadel, altar, wmap):
    os.makedirs(os.path.join(OUT, "icons"), exist_ok=True)
    im = key(Image.open(sheet)); W, H = im.size
    sx, sy = W / 1024, H / 572                       # search windows measured on the 1024x572 sheet
    S = lambda *b: tuple(round(v * (sx if i % 2 == 0 else sy)) for i, v in enumerate(b))
    up(part(im, S(140, 0, 900, 118)), w=1200).save(os.path.join(OUT, "banner.png"))
    up(part(im, S(15, 118, 330, 565)), h=640).save(os.path.join(OUT, "card_small.png"))
    xs = [413, 524, 634, 744, 855]; ys = [167, 253, 340, 425, 512]
    for i, name in enumerate(ICONS):
        cx, cy = xs[i % 5], ys[i // 5]
        ic = part(im, S(cx - 52, cy - 43, cx + 52, cy + 43))
        s = max(ic.size); sq = Image.new("RGBA", (s, s), (0, 0, 0, 0)); sq.paste(ic, ((s - ic.width) // 2, (s - ic.height) // 2))
        sq.resize((128, 128), Image.LANCZOS).save(os.path.join(OUT, "icons", name + ".png"))
    for src, name in ((citadel, "bg_title"), (altar, "bg_summon"), (wmap, "bg_campaign")):
        up(Image.open(src).convert("RGB"), w=2048).save(os.path.join(OUT, name + ".jpg"), quality=86, optimize=True)
    print("ok: banner, card_small, 25 icons, 3 backgrounds")

if __name__ == "__main__":
    main(*sys.argv[1:5])
