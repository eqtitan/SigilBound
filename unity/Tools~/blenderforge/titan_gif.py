"""Labels titan_preview.py's frames and builds one animated GIF per move plus a contact sheet (owner review).
  python3 titan_gif.py OUT_DIR      (reads OUT_DIR/<mode>/f####.png and OUT_DIR/<mode>.json)
Writes OUT_DIR/titan_<mode>.gif and OUT_DIR/titan_contact_sheet.png. Pillow only."""
import glob, json, os, sys
from PIL import Image, ImageDraw, ImageFont

OUT = sys.argv[1]
TITLES = {"fists": "Bedrock Hammer", "leap": "Faultline Leap", "breath": "Heartfire Torrent", "sweep": "Earthsplitter (final blow)",
          "collapse": "Collapse (team outlasts it)", "ridges": "Crystal ridges at 66% / 33% health"}
def font(n):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/dejavu/DejaVuSans-Bold.ttf"):
        if os.path.exists(f): return ImageFont.truetype(f, n)
    return ImageFont.load_default()

def label(im, title, phase, highlight):
    im = im.convert("RGB"); d = ImageDraw.Draw(im); w, h = im.size; s = max(11, w // 30)
    d.rectangle([0, 0, w, s * 2 + 10], fill=(0, 0, 0))
    d.text((8, 4), "PREVIEW (Blender) · " + title, font=font(s), fill=(255, 214, 140))
    d.text((8, s + 8), phase, font=font(s), fill=(255, 90, 60) if highlight else (230, 230, 230))
    return im

sheet_rows = []
for mode, title in TITLES.items():
    files = sorted(glob.glob(os.path.join(OUT, mode, "f*.png")))
    if not files: continue
    log = json.load(open(os.path.join(OUT, f"{mode}.json")))
    phases = sorted(log["phases"])
    def phase_at(f):
        cur = phases[0][1]
        for pf, lab in phases:
            if f >= pf: cur = lab
        return cur
    frames, keys = [], []
    for fp in files:
        f = int(os.path.basename(fp)[1:5]); ph = phase_at(f)
        hi = "IMPACT" in ph or "RELEASE" in ph
        frames.append(label(Image.open(fp), title, ph, hi))
    step = (int(os.path.basename(files[1])[1:5]) - int(os.path.basename(files[0])[1:5])) if len(files) > 1 else 1
    dur = int(1000 * step / log["fps"])
    pal = [fr.quantize(colors=128, method=Image.Quantize.MEDIANCUT) for fr in frames]
    pal[0].save(os.path.join(OUT, f"titan_{mode}.gif"), save_all=True, append_images=pal[1:], duration=dur, loop=0, optimize=True)
    # contact sheet: the first frame of each phase, plus the frame after each impact
    pick = []
    for pf, lab in phases:
        k = min(range(len(files)), key=lambda i: abs(int(os.path.basename(files[i])[1:5]) - pf))
        pick.append(k)
    pick = sorted(set(pick + [len(files) - 1]))[:6]
    sheet_rows.append((title, [frames[k] for k in pick]))
    print(mode, len(frames), "frames", dur, "ms")

if sheet_rows:
    H = 300
    rows = []
    for title, ims in sheet_rows:
        ims = [i.resize((int(i.width * H / i.height), H)) for i in ims]
        r = Image.new("RGB", (sum(i.width for i in ims) + 4 * len(ims), H), (15, 15, 15)); x = 0
        for i in ims: r.paste(i, (x, 0)); x += i.width + 4
        rows.append(r)
    W = max(r.width for r in rows); sheet = Image.new("RGB", (W, (H + 6) * len(rows) + 40), (15, 15, 15))
    d = ImageDraw.Draw(sheet); d.text((10, 8), "Titan moves for the remodelled body (Blender preview of the shipped GLB + clips): wind-up, impact, recovery", font=font(20), fill=(255, 214, 140))
    for k, r in enumerate(rows): sheet.paste(r, (0, 40 + k * (H + 6)))
    sheet.save(os.path.join(OUT, "titan_contact_sheet.png"))
