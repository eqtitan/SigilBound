"""Turns a Grok Imagine card portrait into the game's portrait for one hero.

    python3 portrait.py IN.png HERO_ID

Crops to 3:4 (centred, keeping the top where the face is), scales to 480x640 and writes
unity/Assets/Resources/Portraits/portrait_<id>.jpg (quality 86). The game shows it on hero rows and the
single-summon reveal (Assets/Sigilbound/Client/Portraits.cs).
"""
import os, sys
from PIL import Image
src, hid = sys.argv[1], sys.argv[2]
im = Image.open(src).convert("RGB"); w, h = im.size
if w / h > 0.75: nw = round(h * 0.75); im = im.crop(((w - nw) // 2, 0, (w - nw) // 2 + nw, h))
else: nh = round(w / 0.75); top = round((h - nh) * 0.25); im = im.crop((0, top, w, top + nh))
im = im.resize((480, 640), Image.LANCZOS)
out = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Portraits")
os.makedirs(out, exist_ok=True); p = os.path.join(out, f"portrait_{hid}.jpg"); im.save(p, quality=86, optimize=True)
print("wrote", os.path.relpath(p))
