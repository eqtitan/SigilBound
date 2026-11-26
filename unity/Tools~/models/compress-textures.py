"""Shrinks the high-detail heroes' textures for phones (run after blenderforge/ubc.py and compress-hd.mjs):

    python3 compress-textures.py heroes_hd

albedo: JPEG quality 82 at its size (1024); normal map: 768 px, JPEG quality 88; glow: stays PNG (tiny).
Reads the builder's PNGs or JPEGs, writes NAME.jpg and removes the PNG. Each JPEG it writes carries
the comment "sb-tex-v1", and files that already carry it are skipped, so re-running never
re-compresses a texture twice (JPEG loss would add up). On a phone the difference from the builder's
q88/q92 1024 px maps is not visible at battle or hero-screen size; it saves about a third of the textures.
Pipeline (PLAN.md step 58): ubc.py -> node compress-hd.mjs DIR -> python3 compress-textures.py DIR.
"""
import os, sys
from PIL import Image

MARK = b"sb-tex-v1"
SPEC = {"albedo": (None, 82), "normal": (768, 88)}

def marked(p):
    try:
        with Image.open(p) as im: return im.format == "JPEG" and im.info.get("comment", b"") == MARK
    except Exception: return False

d = sys.argv[1]; before = after = n = 0
for f in sorted(os.listdir(d)):
    for kind, (size, q) in SPEC.items():
        if not (f.endswith(f"_HD_{kind}.jpg") or f.endswith(f"_HD_{kind}.png")): continue
        src = os.path.join(d, f); dst = src[:-4] + ".jpg"
        if f.endswith(".png") and os.path.exists(dst) and marked(dst): os.remove(src); continue
        if marked(src): continue
        before += os.path.getsize(src)
        im = Image.open(src).convert("RGB")
        if size and im.width > size: im = im.resize((size, round(im.height * size / im.width)), Image.LANCZOS)
        im.save(dst + ".tmp", "JPEG", quality=q, optimize=True, progressive=True, comment=MARK)
        os.replace(dst + ".tmp", dst)
        if src != dst: os.remove(src)
        after += os.path.getsize(dst); n += 1
print(f"textures: {n} re-encoded, {before / 1048576:.1f} MB -> {after / 1048576:.1f} MB")
