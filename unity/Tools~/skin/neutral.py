"""Moves the painted UI chrome off the Shadow element's violet (art review M7, PLAN P2-14).

The owner's Grok plates for panels, cards, plain buttons, chips and the banner were painted on a violet
ground, so every menu read as "Shadow". This keeps the gold trim and the lightness of every pixel and
turns the violet/magenta hues into the neutral slate of Palette.cs's chrome tokens (Bg/Panel/Panel2:
hue about 215 deg, low saturation). Gold, red and blue plates keep their colour; only their violet gems and inlays turn slate.

    python3 neutral.py [--check]

--check reports the mean hue/saturation of each plate's centre without writing.
"""
import argparse, colorsys, os
from PIL import Image
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SKIN = os.path.join(HERE, "..", "..", "Assets", "Resources", "Skin")
CHROME = ["panel", "card", "card_highlight", "card_small", "banner", "button_secondary", "button_secondary_pressed",
          "button_chip", "button_chipon",
          # the gold, red and blue plates keep their colour; only their violet gems and inlays turn slate
          "button_primary", "button_primary_pressed", "button_primary_orn", "button_primary_pressed_orn",
          "button_secondary_orn", "button_secondary_pressed_orn", "button_red", "button_red_orn", "button_blue", "button_blue_orn"]
SLATE_HUE = 215 / 360.0   # Palette.Panel #181b20
MAX_SAT = 0.22            # in HSV, after the shift

ap = argparse.ArgumentParser(); ap.add_argument("--check", action="store_true"); a = ap.parse_args()


def centre(px):
    h, w = px.shape[:2]; c = px[h // 3:2 * h // 3, w // 3:2 * w // 3, :3].reshape(-1, 3).mean(axis=0) / 255
    hh, ss, vv = colorsys.rgb_to_hsv(*c)
    return f"#{int(c[0]*255):02x}{int(c[1]*255):02x}{int(c[2]*255):02x} hue {hh*360:3.0f} sat {ss:.2f}"


for name in CHROME:
    path = os.path.join(SKIN, name + ".png")
    if not os.path.exists(path): continue
    im = Image.open(path).convert("RGBA"); px = np.asarray(im).astype(float)
    before = centre(px)
    if not a.check:
        rgb = px[..., :3] / 255
        mx, mn = rgb.max(axis=2), rgb.min(axis=2); d = mx - mn
        r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
        hue = np.zeros_like(mx)
        m = d > 1e-6
        rr = m & (mx == r); gg = m & (mx == g) & ~rr; bb = m & ~rr & ~gg
        hue[rr] = ((g - b)[rr] / d[rr]) % 6; hue[gg] = (b - r)[gg] / d[gg] + 2; hue[bb] = (r - g)[bb] / d[bb] + 4
        hue /= 6
        sat = np.where(mx > 0, d / np.maximum(mx, 1e-6), 0)
        # Violet to magenta (250-345 deg), with a soft edge so blends into the gold stay smooth.
        w = np.clip(1 - np.maximum(0, np.maximum(250 / 360 - hue, hue - 345 / 360)) * 360 / 15, 0, 1)
        lum = 0.299 * r + 0.587 * g + 0.114 * b
        ns = np.minimum(sat * 0.35, MAX_SAT)
        # Slate at the same perceived lightness: a grey at lum, tinted a little toward blue.
        tint = np.array(colorsys.hsv_to_rgb(SLATE_HUE, 1, 1))
        slate = lum[..., None] * (1 - ns[..., None]) + lum[..., None] * ns[..., None] * tint / (tint @ [0.299, 0.587, 0.114])
        out = rgb * (1 - w[..., None]) + np.clip(slate, 0, 1) * w[..., None]
        px[..., :3] = out * 255
        Image.fromarray(np.clip(px, 0, 255).astype(np.uint8), "RGBA").save(path, optimize=True)
    print(f"{name:26s} {before}  ->  {centre(px)}")
