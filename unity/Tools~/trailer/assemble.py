"""Sigilbound 15-second teaser: assembly (owner-approved storyboard, 2026-11-26).

Takes trailer.py's 312 rendered frames (13 s), adds the fades and the 2 s title card, mixes the game's own Titan
theme with sound effects on the beats, and encodes an H.264 MP4 (1920x1080, 24 fps, yuv420p, AAC).

    python3 assemble.py FRAMES_DIR OUT.mp4 [--repo REPO_ROOT]
"""
import os, sys, subprocess, tempfile, math, shutil
from PIL import Image, ImageDraw, ImageFont, ImageFilter, ImageEnhance

FRAMES, OUT = sys.argv[1], sys.argv[2]
REPO = sys.argv[sys.argv.index("--repo") + 1] if "--repo" in sys.argv else os.path.abspath(os.path.join(os.path.dirname(__file__), "..", "..", ".."))
AUDIO = os.path.join(REPO, "unity", "Assets", "Resources", "Audio")
FONTS = os.path.join(REPO, "unity", "Assets", "Sigilbound", "Resources", "Fonts")
ICON = os.path.join(REPO, "unity", "Assets", "Sigilbound", "Art", "Icon", "icon_foreground.png")
W, H, FPS, RENDERED, TOTAL = 1920, 1080, 24, 312, 360
GOLD_TOP, GOLD_BOT = (253, 227, 160), (201, 139, 52)

def title_card():
    """Dark ember ground, the sigil icon, SIGILBOUND in gold Cinzel, the tagline and 'Coming soon'."""
    bg = Image.new("RGB", (W, H)); px = bg.load()
    for y in range(H):
        for x in range(0, W, 4):
            d = math.hypot((x - W / 2) / W, (y - H * 0.45) / H)
            c = (int(48 * max(0, 1 - d * 1.6) + 10), int(14 * max(0, 1 - d * 1.6) + 6), int(10 * max(0, 1 - d * 1.6) + 12))
            for k in range(4):
                if x + k < W: px[x + k, y] = c
    card = bg.convert("RGBA")
    if os.path.exists(ICON):
        ic = Image.open(ICON).convert("RGBA"); s = 300; ic = ic.resize((s, s), Image.LANCZOS)
        glow = ic.copy().filter(ImageFilter.GaussianBlur(18)); glow = ImageEnhance.Brightness(glow).enhance(1.4)
        card.alpha_composite(glow, ((W - s) // 2, 150)); card.alpha_composite(ic, ((W - s) // 2, 150))
    big = ImageFont.truetype(os.path.join(FONTS, "Cinzel-Bold.ttf"), 168)
    mid = ImageFont.truetype(os.path.join(FONTS, "Cinzel-Regular.ttf"), 52)
    small = ImageFont.truetype(os.path.join(FONTS, "Cinzel-Bold.ttf"), 38)
    def gold_text(text, font, y, spacing=0):
        m = Image.new("L", (W, H)); d = ImageDraw.Draw(m); tw = d.textlength(text, font=font) + spacing * (len(text) - 1)
        x = (W - tw) / 2
        for ch in text: d.text((x, y), ch, font=font, fill=255); x += d.textlength(ch, font=font) + spacing
        grad = Image.new("RGBA", (W, H)); gd = ImageDraw.Draw(grad)
        for yy in range(H):
            t = max(0, min(1, (yy - y) / max(1, font.size)))
            gd.line([(0, yy), (W, yy)], fill=tuple(int(GOLD_TOP[i] + (GOLD_BOT[i] - GOLD_TOP[i]) * t) for i in range(3)) + (255,))
        shadow = Image.new("RGBA", (W, H), (0, 0, 0, 0)); shadow.putalpha(m.filter(ImageFilter.GaussianBlur(10)).point(lambda v: int(v * 0.7)))
        card.alpha_composite(shadow, (0, 6)); grad.putalpha(m); card.alpha_composite(grad)
    gold_text("SIGILBOUND", big, 480, spacing=14)
    gold_text("Bind the sigils. Break the Titan.", mid, 690)
    gold_text("COMING SOON", small, 830, spacing=10)
    return card.convert("RGB")

def main():
    tmp = tempfile.mkdtemp(prefix="sbteaser_")
    card = title_card(); card.save(os.path.join(os.path.dirname(OUT) or ".", "teaser_title.png"))
    black = Image.new("RGB", (W, H))
    for f in range(TOTAL):
        if f < RENDERED:
            p = os.path.join(FRAMES, f"{f:03d}.png")
            im = Image.open(p).convert("RGB").resize((W, H), Image.LANCZOS)
            if f < 14: im = Image.blend(black, im, f / 14)                       # fade in from black
            if f >= RENDERED - 10: im = Image.blend(im, black, (f - (RENDERED - 10)) / 10)   # dip to black before the title
        else:
            u = f - RENDERED
            im = Image.blend(black, card, min(1.0, u / 12))                      # the title fades in and holds
            if u > 12: im = ImageEnhance.Brightness(card).enhance(1.0 + 0.06 * math.sin((u - 12) * 0.35))
            if f >= TOTAL - 6: im = Image.blend(im, black, (f - (TOTAL - 6)) / 6 * 0.6)
        im.save(os.path.join(tmp, f"{f:03d}.jpg"), quality=95)
    # sound: the Titan theme fades in, builds, and resolves at the title; effects on the beats (seconds)
    sfx = [("sfx_buff", 0.7, 0.35), ("sfx_cast_shadow", 2.0, 0.5), ("sfx_cast_gale", 2.6, 0.5), ("sfx_cast_light", 3.2, 0.5),
           ("sfx_cast_tide", 3.8, 0.5), ("sfx_cast_flame", 4.4, 0.55), ("sfx_titan_roar", 6.6, 1.0), ("sfx_meteor_whoosh", 8.0, 0.9),
           ("sfx_titan_slam", 10.0, 1.0), ("sfx_earthsplit", 10.02, 0.85), ("sfx_cast_flame", 10.45, 0.9), ("sfx_cast_gale", 11.0, 0.8),
           ("sfx_hit_heavy", 11.55, 0.8), ("sfx_cast_shadow", 12.0, 0.8), ("sfx_hit_magic", 12.5, 0.9), ("sfx_summon_legendary", 13.05, 0.9)]
    inputs = ["-i", os.path.join(AUDIO, "music_titan.ogg")]; parts = ["[0:a]atrim=0:15,asetpts=PTS-STARTPTS,volume=0.75,afade=t=in:st=0:d=1.6,afade=t=out:st=13.0:d=2.0[m]"]
    labels = ["[m]"]
    for i, (name, t, vol) in enumerate(sfx, start=1):
        inputs += ["-i", os.path.join(AUDIO, name + ".ogg")]
        ms = int(t * 1000); parts.append(f"[{i}:a]volume={vol},adelay={ms}|{ms}[s{i}]"); labels.append(f"[s{i}]")
    parts.append("".join(labels) + f"amix=inputs={len(labels)}:normalize=0:duration=first,atrim=0:15,alimiter=limit=0.9[a]")
    cmd = ["/usr/bin/ffmpeg", "-y", "-v", "error", "-framerate", str(FPS), "-i", os.path.join(tmp, "%03d.jpg")] + inputs + [
        "-filter_complex", ";".join(parts), "-map", "0:v", "-map", "[a]",
        "-c:v", "libx264", "-preset", "slow", "-crf", "18", "-pix_fmt", "yuv420p", "-r", str(FPS),
        "-c:a", "aac", "-b:a", "192k", "-ar", "48000", "-t", "15", "-movflags", "+faststart", OUT]
    subprocess.run(cmd, check=True)
    shutil.rmtree(tmp)
    print("wrote", OUT)

if __name__ == "__main__":
    main()
