# Side-by-side sheets: current hero (QA harness shots, battle-shot.cjs) vs the blenderforge prototype.
# python3 compare.py <scratch dir with qa/ and proto/> <out dir>
import sys
from PIL import Image, ImageDraw, ImageFont
S = sys.argv[1]; OUT = sys.argv[2]
def font(n):
    for f in ("/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf", "/usr/share/fonts/truetype/liberation/LiberationSans-Bold.ttf"):
        try: return ImageFont.truetype(f, n)
        except OSError: pass
    return ImageFont.load_default()
F = font(20); f2 = font(15)
def label(im, text, sub=""):
    c = Image.new("RGB", (im.width, im.height + 56), (24, 22, 30)); c.paste(im, (0, 56))
    d = ImageDraw.Draw(c); d.text((10, 6), text, fill=(240, 220, 170), font=F); d.text((10, 32), sub, fill=(200, 200, 210), font=f2); return c
def row(ims, gap=8):
    w = sum(i.width for i in ims) + gap * (len(ims) - 1); h = max(i.height for i in ims)
    c = Image.new("RGB", (w, h), (12, 12, 16)); x = 0
    for i in ims: c.paste(i, (x, 0)); x += i.width + gap
    return c
cur_b = Image.open(f"{S}/qa/current-battle.png").convert("RGB")
for hero, cur_box, proto_box in (("ysolde", (100, 840, 340, 1100), (270, 720, 510, 980)), ("gorrum", (20, 740, 260, 1000), (270, 715, 510, 975))):
    cv = Image.open(f"{S}/qa/shots/{hero}_none_3-4.png").convert("RGB")
    pv = Image.open(f"{S}/proto/{hero}/{hero}_viewer.png").convert("RGB").resize(cv.size)
    cb = cur_b.crop(cur_box).resize((480, 520)); pb = Image.open(f"{S}/proto/{hero}/{hero}_battle.png").convert("RGB").crop(proto_box).resize((480, 520))
    # phone-size read: the hero about 70 px tall
    cp = cur_b.crop(cur_box).resize((96, 104)).resize((240, 260), Image.NEAREST); pp = Image.open(f"{S}/proto/{hero}/{hero}_battle.png").convert("RGB").crop(proto_box).resize((96, 104)).resize((240, 260), Image.NEAREST)
    top = row([label(cv, "Current (code-built on CC0 Quaternius)", "hero viewer, web build"), label(pv, "Free-tools prototype (scripted Blender)", "same framing, game-like shading preview")])
    bot = row([label(cb, "Current, battle crop", "390x844 @2x"), label(pb, "Prototype, battle camera", "34 deg pitch, 62 deg FOV")])
    ph = row([label(cp, "Current, ~70 px", ""), label(pp, "Prototype, ~70 px", "")])
    sheet = Image.new("RGB", (max(top.width, bot.width), top.height + bot.height + ph.height + 16), (12, 12, 16))
    sheet.paste(top, (0, 0)); sheet.paste(bot, (0, top.height + 8)); sheet.paste(ph, (0, top.height + bot.height + 16))
    sheet.save(f"{OUT}/{hero}-compare.png", optimize=True); print(hero, sheet.size)
