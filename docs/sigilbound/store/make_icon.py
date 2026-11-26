"""Sigilbound app icon and store feature graphic, drawn from the game's palette (no outside art).
python3 docs/sigilbound/store/make_icon.py -> unity/Assets/Sigilbound/Art/Icon/*.png + docs/sigilbound/store/*.png
The sigil: a gold rune ring, a five-pointed star whose points carry the five element gems
(Flame, Gale, Tide, Light, Shadow), and an upright sword through the centre."""
import math, os
from PIL import Image, ImageDraw, ImageFilter, ImageFont
HERE = os.path.dirname(os.path.abspath(__file__)); ROOT = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
ICON = os.path.join(ROOT, "unity", "Assets", "Sigilbound", "Art", "Icon"); os.makedirs(ICON, exist_ok=True)
SS = 4; N = 1024 * SS
GOLD, GOLD_HI, GOLD_LO, INK = (226, 177, 78), (253, 227, 160), (138, 96, 30), (18, 12, 22)
ELEMENTS = [(255, 122, 58), (63, 181, 122), (58, 142, 232), (255, 226, 122), (160, 80, 255)]  # Flame Gale Tide Light Shadow

def background(n):
    im = Image.new("RGB", (n, n)); px = im.load(); c = n / 2
    for y in range(0, n, 4):
        for x in range(0, n, 4):
            d = min(1, math.hypot(x - c, y - c) / (n * 0.72))
            col = tuple(int(a + (b - a) * d) for a, b in zip((62, 30, 84), (13, 9, 20)))
            for dy in range(4):
                for dx in range(4):
                    if x + dx < n and y + dy < n: px[x + dx, y + dy] = col
    d = ImageDraw.Draw(im)   # faint outer rune ring on the background layer
    r = n * 0.47
    for k in range(48):
        a = k / 48 * math.tau; x0, y0 = c + math.cos(a) * r, c + math.sin(a) * r
        d.line((x0, y0, c + math.cos(a) * (r - n * 0.02), c + math.sin(a) * (r - n * 0.02)), fill=(90, 60, 110), width=max(1, n // 300))
    return im

def sigil(n, scale=0.66):
    """Foreground: everything inside the adaptive-icon safe zone (the central 66%)."""
    im = Image.new("RGBA", (n, n), (0, 0, 0, 0)); d = ImageDraw.Draw(im); c = n / 2; R = n * scale / 2
    def ring(r, w, col): d.ellipse((c - r, c - r, c + r, c + r), outline=col, width=int(w))
    ring(R * 0.98, n * 0.012, INK); ring(R * 0.95, n * 0.022, GOLD); ring(R * 0.95, n * 0.006, GOLD_HI)
    ring(R * 0.80, n * 0.010, GOLD)
    for k in range(24):   # rune ticks between the rings
        a = k / 24 * math.tau; r0, r1 = R * 0.82, R * (0.92 if k % 3 == 0 else 0.88)
        d.line((c + math.cos(a) * r0, c + math.sin(a) * r0, c + math.cos(a) * r1, c + math.sin(a) * r1), fill=GOLD_HI if k % 3 == 0 else GOLD, width=int(n * 0.008))
    pts = [(c + math.cos(-math.pi / 2 + k * math.tau / 5) * R * 0.78, c + math.sin(-math.pi / 2 + k * math.tau / 5) * R * 0.78) for k in range(5)]
    star = [pts[(k * 2) % 5] for k in range(5)] + [pts[0]]
    d.line(star, fill=INK, width=int(n * 0.03), joint="curve"); d.line(star, fill=GOLD, width=int(n * 0.016), joint="curve")
    # the sword: blade, fuller, guard, grip, pommel
    bw, top, guard = n * 0.035, c - R * 0.86, c + R * 0.36
    blade = [(c, top), (c + bw, top + bw * 2.2), (c + bw, guard), (c - bw, guard), (c - bw, top + bw * 2.2)]
    d.polygon([(x + n * 0.006, y) for x, y in blade], fill=INK); d.polygon(blade, fill=(236, 230, 214))
    d.line((c, top + bw * 2.5, c, guard - n * 0.01), fill=(170, 170, 185), width=int(n * 0.008))
    d.rounded_rectangle((c - R * 0.36, guard, c + R * 0.36, guard + n * 0.03), radius=n * 0.012, fill=GOLD, outline=INK, width=int(n * 0.005))
    d.rectangle((c - n * 0.016, guard + n * 0.03, c + n * 0.016, guard + n * 0.11), fill=(110, 60, 30), outline=INK, width=int(n * 0.004))
    d.ellipse((c - n * 0.03, guard + n * 0.105, c + n * 0.03, guard + n * 0.165), fill=GOLD, outline=INK, width=int(n * 0.005))
    for (x, y), col in zip(pts, ELEMENTS):   # element gems on the star's points
        r = n * 0.05
        d.ellipse((x - r * 1.18, y - r * 1.18, x + r * 1.18, y + r * 1.18), fill=INK)
        d.ellipse((x - r, y - r, x + r, y + r), fill=GOLD_LO)
        d.ellipse((x - r * 0.78, y - r * 0.78, x + r * 0.78, y + r * 0.78), fill=col)
        d.ellipse((x - r * 0.45, y - r * 0.6, x - r * 0.05, y - r * 0.2), fill=(255, 255, 255, 170))
    glow = im.filter(ImageFilter.GaussianBlur(n * 0.012)); g = Image.new("RGBA", im.size, (0, 0, 0, 0))
    g.paste((255, 200, 110, 0), (0, 0, n, n)); g.putalpha(glow.getchannel("A").point(lambda a: int(a * 0.45)))
    return Image.alpha_composite(g, im)

def save(im, path, size): im.resize((size, size), Image.LANCZOS).save(path)
bg = background(N); fg = sigil(N)
save(bg, os.path.join(ICON, "icon_background.png"), 1024)
save(fg, os.path.join(ICON, "icon_foreground.png"), 1024)
full = bg.convert("RGBA"); full.alpha_composite(sigil(N, 0.84)); save(full, os.path.join(ICON, "icon_full.png"), 1024)
save(full, os.path.join(HERE, "icon_512.png"), 512); save(full, os.path.join(HERE, "icon_1024.png"), 1024)
# Feature graphic 1024x500: the sigil left, the name in gold on violet.
W, H = 1024 * 2, 500 * 2
fe = Image.new("RGBA", (W, H)); bgw = background(W).resize((W, W)).crop((0, (W - H) // 2, W, (W + H) // 2)); fe.paste(bgw, (0, 0))
s = sigil(H * 2, 0.9).resize((H, H), Image.LANCZOS); fe.alpha_composite(s, (int(W * 0.06), 0))
d = ImageDraw.Draw(fe)
try:
    f1 = ImageFont.truetype(os.path.join(ROOT, "unity", "Assets", "Sigilbound", "Resources", "Fonts", "Cinzel-Bold.ttf"), 150)
    f2 = ImageFont.truetype(os.path.join(ROOT, "unity", "Assets", "Sigilbound", "Resources", "Fonts", "Cinzel-Regular.ttf"), 54)
except Exception:
    f1 = f2 = ImageFont.load_default()
x0 = int(W * 0.06) + H + 40
room = W - x0 - 70
def fit(path, size, text):   # largest size up to `size` whose text fits the room right of the sigil
    while size > 20:
        try: f = ImageFont.truetype(path, size)
        except Exception: return ImageFont.load_default()
        if d.textlength(text, font=f) <= room: return f
        size -= 4
    return ImageFont.load_default()
FONTS = os.path.join(ROOT, "unity", "Assets", "Sigilbound", "Resources", "Fonts")
f1 = fit(os.path.join(FONTS, "Cinzel-Bold.ttf"), 150, "SIGILBOUND")
f2 = fit(os.path.join(FONTS, "Cinzel-Regular.ttf"), 54, "Summon 150 heroes. Master five elements.")
d.text((x0 + 6, 300 + 6), "SIGILBOUND", font=f1, fill=INK); d.text((x0, 300), "SIGILBOUND", font=f1, fill=GOLD_HI)
d.text((x0 + 4, 520), "Summon 150 heroes. Master five elements.", font=f2, fill=(234, 223, 202))
d.text((x0 + 4, 600), "Topple the Titan.", font=f2, fill=GOLD)
fe.convert("RGB").resize((1024, 500), Image.LANCZOS).save(os.path.join(HERE, "feature_1024x500.png"))
print("icons and feature graphic written")
