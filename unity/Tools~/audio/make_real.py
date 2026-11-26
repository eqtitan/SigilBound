"""Battle sound set, pass 2 (owner, 2026-11-26: "armour hits armour", atmospheric, stereo).

Layers CC0 recordings (Kenney Impact Sounds / RPG Audio / Sci-fi, sfx-kit, CC0 field-recording beds;
fetch_sources.sh pins them) with modal metal synthesis, granular rattles and debris, a formant-filtered
growl, fire and bubble synthesis and generated stereo reverbs. Writes true-stereo OGGs to
Assets/Resources/Audio:

  sfx_<name>.ogg, sfx_<name>_2.ogg ...  one-shot variants (Sound.cs loads every _N it finds)
  amb_<bed>.ogg                         seamless ambience loops per arena family

Run:  AUDIO_SRC=~/.cache/sigil-audio-src ./fetch_sources.sh && python3 make_real.py [only-prefix]
"""
import os, sys, json
import numpy as np
from stereo import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")
KI = "kenney-impact/sample/assets/audio/impact/"
KR = "kenney-rpg/assets/kenney_rpgaudio/Audio/"
KS = "kenney-scifi/sounds/"
SK = "sfx-kit/sounds/"
AM = "cc0-ambient/app/src/main/assets/ambient/"
ONLY = sys.argv[1] if len(sys.argv) > 1 else ""
REPORT = {}

def K(name, i): return load(f"{KI}{name}_{i % 5:03d}.ogg")
def R(name): return load(f"{KR}{name}.ogg")
def S(path): return load(f"{SK}{path}.wav")
def KSF(path): return load(f"{KS}{path}.ogg")

MAX_SEC = {"titan_roar": 4.6, "titan_breath": 4.6, "titan_slam": 4.8, "earthsplit": 5.5, "titan_step": 2.2, "titan_swing": 1.8,
           "cast_": 1.6, "heal": 1.8, "buff": 1.4, "debuff": 1.4, "stun": 1.3, "poison": 1.6, "shield": 1.4, "crit": 1.2, "kill": 1.2, "downed": 1.5,
           "armour_plate": 1.2}
def cap(name, x):
    """Long reverb tails are cut to a length that still decays naturally (a 0.4-0.8 s fade at the end)."""
    lim = next((v for k, v in MAX_SEC.items() if name.startswith(k)), None)
    if lim is None or len(x) <= N(lim): return x
    return fade(x[:N(lim)], 0, min(0.8, lim * 0.3))

def save(name, variants, loud, q=2, win=0.2, r=0.04):
    """Level and write a variant set: sfx_<name>.ogg, sfx_<name>_2.ogg, ..."""
    if ONLY and not name.startswith(ONLY): return
    for i, x in enumerate(variants):
        y = cap(name, finish(x, loud, win=win, r=r))
        fn = f"sfx_{name}" + ("" if i == 0 else f"_{i + 1}")
        write_ogg(y, os.path.join(OUT, fn + ".ogg"), q=q)
        REPORT[fn] = stats(y)
        np.save(os.path.join(PREVIEW, fn + ".npy"), y.astype(np.float32)) if PREVIEW else None
    print(f"{name:16s} x{len(variants)}  {REPORT['sfx_' + name]}")

PREVIEW = os.environ.get("AUDIO_PREVIEW")
if PREVIEW: os.makedirs(PREVIEW, exist_ok=True)

def sub_thud(f0=55, sec=0.35, tau=0.09, click=0.3, seed=None):
    """Body of a heavy blow: a pitch-dropping sine plus a soft click."""
    n = N(sec); t = tt(n); f = f0 * (1 + 1.2 * np.exp(-t / 0.02))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / tau)
    x[:N(0.004)] += hp(noise(N(0.004), 0, seed), 1500) * click
    return x

def whoosh(kind, ratio, pan_a=-0.35, pan_b=0.35, gain=1.0, hpf=120):
    w = to_mono(load(f"{SK}whooshes/whoosh-{kind}.wav", 2))
    w = hp(resample(w, ratio), hpf)
    return st(w, pan_sweep(len(w), pan_a, pan_b)) * gain

# ---------------------------------------------------------------- swings
def swings():
    out = {}
    v = []
    for i in range(4):
        r = 1.1 + 0.08 * i
        ring = modal([(f, 0.25, 0.05) for f in (4200 * r, 6100 * r, 7900 * r)], 0.25, beat=8, seed=i)
        v.append(mix(0.5, [(whoosh(["fast-medium", "fast-hard"][i % 2], r, -0.5 + 0.1 * i, 0.4), 0, 1.0), (ring, 0.02, 0.18)]))
    out["swing_blade"] = v
    v = []
    for i in range(4):
        w = whoosh(["slow-hard", "medium-hard"][i % 2], 0.72 + 0.05 * i, -0.4, 0.4, hpf=60)
        w = lp(w, 3500)
        rattle = grains([R("handleCoins"), R("metalClick"), K("impactTin_medium", i)], 7, 0.25, 0.03, pitch=(0.7, 0.95), gain=(0.15, 0.4), dur=0.12, seed=10 + i)
        v.append(mix(0.9, [(w, 0, 1.0), (rattle, 0, 0.5)]))
    out["swing_heavy"] = v
    v = []
    for i in range(4):
        w = whoosh("medium-hard", 0.92 + 0.05 * i, -0.45, 0.45)
        whistle = band(noise(N(0.35), 0, 20 + i), 1500 + 200 * i, 2400 + 200 * i, 4) * env_ar(N(0.35), 0.12, 0.06)
        v.append(mix(0.6, [(w, 0, 1.0), (whistle, 0.02, 0.25)]))
    out["swing_polearm"] = v
    v = []
    for i in range(4):
        a = whoosh("fast-soft", 1.45 + 0.05 * i, -0.6, -0.1); b = whoosh("fast-soft", 1.6 + 0.05 * i, 0.6, 0.1)
        scr = hp(resample(trim(R(f"drawKnife{1 + i % 3}"), 0.18), 1.2), 1200)
        v.append(mix(0.5, [(a, 0, 1.0), (b, 0.085, 0.9), (fade(scr), 0.0, 0.25, 0.2)]))
    out["swing_daggers"] = v
    v = []
    for i in range(4):
        n = N(0.5); t = tt(n); f0 = 92 + 9 * i
        tw = sum(np.sin(2 * np.pi * f0 * k * t * (1 + 0.002 * k)) * np.exp(-t / (0.16 / k ** 0.6)) / k ** 0.9 for k in range(1, 14))
        tw = tw * (1 + 0.25 * np.sign(np.sin(2 * np.pi * f0 * t)) * np.exp(-t / 0.03))  # buzz against the bow
        arrow = whoosh("fast-soft", 1.7 + 0.05 * i, 0.0, 0.6)
        v.append(mix(0.6, [(tw, 0, 0.6, -0.15), (arrow, 0.02, 0.8)]))
    out["swing_bow"] = v
    v = []
    for i in range(3):
        w = whoosh("medium-medium", 0.85 + 0.05 * i, -0.35, 0.35)
        lea = R(["handleSmallLeather", "handleSmallLeather2", "clothBelt"][i])
        v.append(mix(0.6, [(w, 0, 1.0), (lea, 0, 0.5), (resample(K("impactMetal_light", i), 0.9), 0.0, 0.08)]))
    out["swing_shield"] = v
    v = []
    for i in range(4):
        w = whoosh("fast-soft", 1.0 + 0.07 * i, -0.4 + 0.2 * (i % 2), 0.3)
        v.append(mix(0.45, [(w, 0, 1.0), (R(f"cloth{1 + i}"), 0, 0.6)]))
    out["swing_unarmed"] = v
    v = []
    for i in range(4):
        w = st(lp(hp(resample(to_mono(load(f"{SK}whooshes/whoosh-fast-soft.wav", 2)), 1.9 + 0.1 * i), 500), 9000), pan_sweep(N(0.3), -0.5, 0.6))
        n = N(0.3); t = tt(n); whistle = np.sin(2 * np.pi * np.cumsum(3200 - 1400 * t / 0.3) / SR) * env_ar(n, 0.05, 0.08) * 0.15
        v.append(mix(0.4, [(w, 0, 1.0), (st(whistle, pan_sweep(n, -0.5, 0.6)), 0, 1.0)]))
    out["arrow"] = v
    # The Titan's swing: a huge slow whoosh with its plates and chains grinding along.
    v = []
    for i in range(3):
        w = lp(whoosh("slow-hard", 0.5 + 0.05 * i, -0.6, 0.6, hpf=35), 2500)
        chain = grains([R("handleCoins"), R("metalPot1"), R("metalLatch"), K("impactPlate_light", i)], 14, 0.6, 0.0, pitch=(0.5, 0.75), gain=(0.2, 0.6), dur=0.25, seed=40 + i, density="even")
        grind = band(resample(R(f"drawKnife{1 + i}"), 0.45), 150, 2500)
        v.append(verb(mix(1.6, [(w, 0, 1.0), (chain, 0.05, 0.55), (grind, 0.1, 0.35, 0.3)]), 1.4, 0.22, 2500, seed=i))
    out["titan_swing"] = v
    return out

# ---------------------------------------------------------------- weapon impacts (attacker side)
def hits():
    out = {}
    out["hit_blade"] = [mix(0.5, [(onset(R(["knifeSlice", "knifeSlice2", "chop", "knifeSlice"][i])), 0, 1.0),
                                  (lp(K("impactPunch_medium", i), 2500), 0.0, 0.5),
                                  (modal([(f, 0.2, 0.06) for f in (3100 + 300 * i, 5200 + 200 * i)], 0.2, beat=6, seed=i), 0.003, 0.12)]) for i in range(4)]
    out["hit_heavy"] = [mix(0.6, [(K("impactMining", i), 0, 0.9), (K("impactPunch_heavy", i), 0, 0.9),
                                  (sub_thud(52 + 4 * i, seed=i), 0, 0.8)]) for i in range(4)]
    out["hit_polearm"] = [mix(0.5, [(onset(R("chop")), 0, 0.8), (K("impactPunch_medium", i + 1), 0.0, 0.8),
                                    (resample(K("impactWood_light", i), 1.1), 0.01, 0.35)]) for i in range(4)]
    out["hit_daggers"] = [mix(0.4, [(onset(R(["knifeSlice", "knifeSlice2"][i % 2])), 0, 1.0),
                                    (fade(hp(trim(R(f"drawKnife{1 + i % 3}"), 0.14), 1500), 0.002, 0.05), 0.04, 0.35, 0.25),
                                    (lp(K("impactPunch_medium", i), 3000), 0, 0.35)]) for i in range(4)]
    out["hit_arrow"] = [mix(0.4, [(resample(K(["impactWood_medium", "impactWood_light"][i % 2], i), 1.25), 0, 1.0),
                                  (trim(onset(R("chop")), 0.12), 0, 0.4), (lp(K("impactPunch_medium", i), 2000), 0, 0.4)]) for i in range(4)]
    out["hit_shield"] = [mix(0.6, [(K("impactPlate_heavy", i), 0, 0.8), (K("impactWood_heavy", i), 0, 0.7),
                                   (K("impactPunch_heavy", i), 0, 0.8), (sub_thud(70, 0.25, seed=i), 0, 0.5)]) for i in range(4)]
    out["hit_unarmed"] = [mix(0.4, [(K("impactPunch_heavy", i), 0, 1.0), (K("impactSoft_heavy", i), 0, 0.6),
                                    (sub_thud(80, 0.2, 0.05, seed=i), 0, 0.4)]) for i in range(4)]
    v = []
    for i in range(4):
        sh = modal([(f * (1 + 0.04 * i), 0.3 / (1 + j * 0.3), 0.18 / (1 + j * 0.2)) for j, f in enumerate([1180, 1770, 2360, 3540, 4720, 5900])], 0.6, beat=3, seed=50 + i)
        air = band(noise(N(0.4), 0, 60 + i), 2000, 9000) * env_ar(N(0.4), 0.005, 0.08)
        v.append(verb(mix(0.6, [(K("impactSoft_medium", i), 0, 0.6), (sh, 0, 0.5, 0.0), (st(air, pan_sweep(N(0.4), -0.5, 0.5)), 0, 0.3)]), 0.9, 0.3, 6000, seed=i))
    out["hit_magic"] = v
    for k in out:
        if k != "hit_magic": out[k] = [verb(x, 0.45, 0.1, 6000, predelay=0.006, seed=sum(map(ord, k)) % 97 + j) for j, x in enumerate(out[k])]
    return out

# ---------------------------------------------------------------- armour response (defender side)
def armour():
    out = {}
    v = []   # plate: clang + plate ring (modal) + rattle tail
    for i in range(5):
        ring = modal(plate_modes(380 + 70 * i, 12, 1.0, 0.45, seed=70 + i, bright=0.93), 1.2, beat=1.2, seed=70 + i)
        rattle = grains([R("metalClick"), R("metalLatch"), K("impactTin_medium", i), K("impactMetal_light", i + 2)], 9, 0.32, 0.04,
                        pitch=(0.95, 1.35), gain=(0.2, 0.55), dur=0.09, seed=80 + i, pan=0.5)
        x = mix(1.3, [(K("impactPlate_heavy", i), 0, 0.9), (K("impactMetal_heavy", i + 1), 0.0, 0.55), (ring, 0.0, 0.35), (rattle, 0, 0.6)])
        v.append(verb(x, 0.9, 0.16, 5000, seed=i))
    out["armour_plate"] = v
    v = []   # mail: dense ring jingle (coins, tin, clicks) + a light metal tick
    for i in range(5):
        coins = fade(trim(resample(R(["handleCoins", "handleCoins2"][i % 2]), 1.15 + 0.06 * i), 0.4), 0.001, 0.12)
        jingle = grains([R("metalClick"), K("impactTin_medium", i), K("impactGlass_light", i), K("impactMetal_light", i)], 22, 0.28, 0.0,
                        pitch=(1.2, 1.9), gain=(0.1, 0.35), dur=0.05, seed=90 + i, pan=0.6)
        x = mix(0.8, [(coins, 0, 0.7, 0.0), (jingle, 0, 0.7), (K("impactMetal_light", i), 0, 0.5), (lp(K("impactPunch_medium", i), 2500), 0, 0.6)])
        v.append(verb(x, 0.6, 0.12, 6000, seed=i))
    out["armour_mail"] = v
    out["armour_leather"] = [mix(0.5, [(R(["dropLeather", "handleSmallLeather", "handleSmallLeather2", "dropLeather"][i]), 0, 0.8),
                                       (K("impactSoft_heavy", i), 0, 0.9), (lp(K("impactPunch_medium", i + 2), 3000), 0, 0.6)]) for i in range(4)]
    out["armour_cloth"] = [mix(0.45, [(K("impactSoft_medium", i), 0, 0.9), (R(f"cloth{1 + i}"), 0, 0.6),
                                      (lp(K("impactPunch_medium", i), 1800), 0, 0.6)]) for i in range(4)]
    for k in ("armour_leather", "armour_cloth"): out[k] = [verb(x, 0.4, 0.1, 5000, predelay=0.006, seed=j) for j, x in enumerate(out[k])]
    # crit accent: a heavy metal crack with a short boom and a bright ring
    v = []
    for i in range(4):
        ring = modal(plate_modes(620 + 90 * i, 8, 1.2, 0.3, seed=110 + i), 0.9, beat=2, seed=110 + i)
        x = mix(1.0, [(resample(K("impactMetal_heavy", i), 0.8), 0, 0.8), (S(["impacts/impact-heavy-hard", "impacts/impact-deep-hard"][i % 2]), 0, 0.7),
                      (ring, 0.0, 0.3), (sub_thud(48, 0.4, 0.12, seed=i), 0, 0.6)])
        v.append(verb(x, 1.1, 0.2, 4500, seed=i))
    out["crit"] = v
    # defeat: the blow, a body fall and the gear clattering down
    def fall(i, dark):
        clatter = grains([R("metalPot1"), R("metalPot2"), R("metalClick"), K("impactMetal_light", i), R("beltHandle1")], 8, 0.4, 0.0,
                         pitch=(0.75, 1.1) if dark else (0.9, 1.25), gain=(0.2, 0.5), dur=0.15, seed=120 + i + (5 if dark else 0))
        x = mix(1.4, [(K("impactPunch_heavy", i), 0, 0.8), (resample(K("impactSoft_heavy", i + 1), 0.85), 0.22, 1.0),
                      (sub_thud(60, 0.3, 0.1, seed=i), 0.22, 0.6), (clatter, 0.28, 0.6)])
        return verb(x, 1.0 if not dark else 1.5, 0.18, 4000, seed=i)
    out["kill"] = [fall(i, False) for i in range(3)]
    v = []
    for i in range(3):
        n = N(1.2); t = tt(n); tone = (np.sin(2 * np.pi * 98 * t) + 0.5 * np.sin(2 * np.pi * 146.8 * t)) * env_ar(n, 0.1, 0.35) * 0.12
        x = fall(i, True); y = np.zeros((max(len(x), n), 2)); y[:len(x)] += x; y[:n] += st(lp(tone, 900)) * 1.0
        v.append(y)
    out["downed"] = v
    return out

# ---------------------------------------------------------------- the Titan
def growl(sec=2.6, seed=0, f_contour=(46, 60, 52, 40)):
    """A beast roar: a rough glottal source (pitch contour, vibrato, jitter, subharmonic flutter) through a
    moving vocal tract (o -> a -> o), breath noise through the same tract, saturation and chest boom."""
    g = np.random.default_rng(seed); n = N(sec); t = tt(n); u = t / sec
    f0 = np.interp(u, np.linspace(0, 1, len(f_contour)), f_contour) * (1 + 0.03 * np.sin(2 * np.pi * 5.2 * t) + 0.04 * smooth_rand(n, 25, seed))
    ph = 2 * np.pi * np.cumsum(f0) / SR
    src = sum(np.sin(k * ph + g.uniform(0, 6.28)) / k ** 1.05 for k in range(1, 90))
    sub = np.sin(ph / 2) * (0.5 + 0.5 * np.clip(smooth_rand(n, 3, seed + 1), 0, 1))
    flutter = 0.55 + 0.45 * np.abs(lp(noise(n, 0, seed + 2), 35) * 6).clip(0, 1)
    src = (src + sub * 0.7) * flutter
    breath = band(noise(n, 1, seed + 3), 300, 6000) * 0.9
    amp = np.interp(u, [0, 0.08, 0.3, 0.75, 1], [0, 1, 0.95, 0.8, 0])
    F1 = lambda k: np.interp(k, [0, 0.3, 0.7, 1], [380, 720, 640, 420])
    F2 = lambda k: np.interp(k, [0, 0.3, 0.7, 1], [760, 1150, 1080, 820])
    F3 = lambda k: np.interp(k, [0, 0.5, 1], [2300, 2600, 2400])
    F4 = lambda k: 3300.0
    voiced = formant_filter(src * amp, [F1, F2, F3, F4])
    air = formant_filter(breath * np.interp(u, [0, 0.05, 0.5, 1], [0.4, 1, 0.5, 0.7]) * amp, [F1, F2, F3, F4])
    x = voiced / (np.abs(voiced).max() + 1e-9) + air / (np.abs(air).max() + 1e-9) * 0.35
    x = np.tanh(2.2 * x) / np.tanh(2.2)
    chest = lp(x, 160) * 1.6
    return x + chest

def titan():
    out = {}
    v = []   # footsteps: a deep stomp, plate grind, chain rattle and a little grit
    for i in range(5):
        stomp = lp(resample(K("impactMining", i), 0.48 + 0.03 * i), 900)
        rumble = lp(S(["booms-and-bass/bass-rumble-short", "booms-and-bass/boom-hit-soft"][i % 2]), 300)
        grind = band(resample(trim(R(f"drawKnife{1 + i % 3}"), 0.5), 0.5 + 0.04 * i), 200, 2600)
        chain = grains([R("handleCoins"), R("handleCoins2"), R("metalPot1"), R("metalClick"), R("metalLatch")], 12, 0.42, 0.04,
                       pitch=(0.55, 0.75), gain=(0.2, 0.55), dur=0.18, seed=200 + i, pan=0.7)
        grit = grains([K("impactGeneric_light", j) for j in range(5)] + [K("impactMining", j) for j in range(5)], 10, 0.5, 0.06,
                      pitch=(0.9, 1.5), gain=(0.05, 0.2), dur=0.06, seed=210 + i, pan=0.9, lowpass=5000)
        x = mix(1.6, [(stomp, 0, 1.0), (rumble, 0, 0.8), (sub_thud(38 + 3 * i, 0.6, 0.18, seed=i), 0, 0.9),
                      (fade(grind, 0.02, 0.15), 0.03, 0.35, 0.25 - 0.1 * i), (chain, 0, 0.6), (grit, 0, 0.6)])
        v.append(verb(x, 1.8, 0.28, 2200, predelay=0.02, seed=i, lowmult=1.5))
    out["titan_step"] = v
    v = []   # roar
    for i, fc in enumerate([(46, 60, 52, 40), (42, 56, 58, 38), (50, 64, 50, 44)]):
        r = growl(2.6, seed=300 + i, f_contour=fc)
        shudder = grains([R("handleCoins"), R("metalPot1"), K("impactPlate_light", i)], 12, 1.4, 0.2, pitch=(0.5, 0.7), gain=(0.08, 0.2), dur=0.2, seed=310 + i, density="even")
        rum = lp(S("booms-and-bass/bass-rumble-long"), 120)
        x = mix(3.2, [(r, 0, 1.0, 0.0), (shudder, 0, 0.4), (rum, 0.05, 0.45)])
        v.append(verb(x, 3.2, 0.32, 2400, predelay=0.03, seed=320 + i, lowmult=1.4))
    out["titan_roar"] = v
    def debris(seed, span=1.6, count=26):
        return grains([K("impactGeneric_light", j) for j in range(5)] + [K("impactWood_light", j) for j in range(5)] + [K("impactMining", j) for j in range(5)],
                      count, span, 0.08, pitch=(0.7, 1.4), gain=(0.08, 0.45), dur=0.12, seed=seed, pan=0.95, lowpass=4500)
    v = []   # slam / landing
    for i in range(4):
        x = mix(4.2, [(lp(resample(K("impactMining", i), 0.42), 1200), 0, 1.0), (resample(K("impactPlate_heavy", i), 0.5), 0, 0.6),
                      (S(["cinematic-hits/cinematic-slam-big", "cinematic-hits/cinematic-slam-massive"][i % 2]), 0, 0.8),
                      (S("booms-and-bass/bass-sub-drop-short"), 0, 0.6), (S("booms-and-bass/boom-hit-strong"), 0, 0.6),
                      (sub_thud(34, 1.2, 0.35, seed=i), 0, 1.0), (debris(400 + i), 0, 0.8),
                      (lp(S("booms-and-bass/bass-rumble-long"), 200), 0.1, 0.55)])
        v.append(verb(x, 3.0, 0.3, 2600, predelay=0.025, seed=410 + i, lowmult=1.5))
    out["titan_slam"] = v
    v = []   # Earthsplitter: the ground cracking open
    for i in range(2):
        cracks = grains([resample(K("impactMining", j), 0.7) for j in range(5)] + [K("impactWood_heavy", j) for j in range(5)], 9, 0.3, 0.0,
                        pitch=(0.6, 0.9), gain=(0.4, 0.9), dur=0.15, seed=500 + i, pan=0.8, density="even")
        x = mix(5.5, [(cracks, 0, 0.8), (resample(KSF(f"EXPLOSIONS/EXPLDsgn_CrunchBlast0{1 + i * 2}_Kenney_KenneySciFi"), 0.6), 0.12, 0.8),
                      (lp(S("cinematic-hits/cinematic-thunder-massive"), 2500), 0.1, 0.7), (S("cinematic-hits/cinematic-slam-massive"), 0.1, 0.8),
                      (sub_thud(30, 2.0, 0.6, seed=i), 0.1, 1.0), (debris(520 + i, 2.6, 40), 0.15, 0.9),
                      (lp(S("booms-and-bass/bass-rumble-long"), 160), 0.2, 0.8)])
        v.append(verb(x, 3.8, 0.32, 2200, predelay=0.03, seed=530 + i, lowmult=1.6))
    out["earthsplit"] = v
    v = []   # Meteor Leap: crouch-launch boom, then a rising roar of air and fire
    for i in range(2):
        rise = lp(resample(to_mono(load(f"{SK}reverse-whooshes/reverse-whoosh-long-strong.wav", 2)), 0.8), 4000)
        air = whoosh("slow-hard", 0.55 + 0.05 * i, -0.5, 0.5, hpf=40)
        fr = fire_roar(1.6, seed=600 + i) * np.interp(np.arange(N(1.6)), [0, N(0.3), N(1.0), N(1.6)], [0.2, 1, 0.8, 0])[:, None]
        launch = mix(1.0, [(lp(resample(K("impactMining", i), 0.45), 800), 0, 1.0), (S("booms-and-bass/boom-drop-medium"), 0, 0.7), (sub_thud(36, 0.8, 0.25, seed=i), 0, 0.8)])
        x = mix(2.4, [(launch, 0, 0.9), (st(rise, -0.2), 0.0, 0.5), (air, 0.1, 0.9), (fr, 0.15, 0.35), (debris(610 + i, 0.6, 10), 0, 0.5)])
        v.append(verb(x, 2.2, 0.25, 3000, seed=620 + i))
    out["meteor"] = v
    v = []   # Cinder Breath: a torrent of fire sweeping left to right across the team
    for i in range(2):
        L = 3.4; n = N(L)
        fr = fire_roar(L, seed=700 + i, body=500, hiss=3000)
        e = np.interp(np.arange(n), [0, N(0.12), N(0.4), N(2.6), N(L)], [0, 1.0, 0.85, 0.9, 0])
        thr = np.zeros((n, 2))
        for j, k in enumerate([1, 3]):
            th = resample(KSF(f"SCIFI/SCIShip_ThrusterFire0{k + i}_Kenney_KenneySciFi"), 0.6)
            th = to_mono(th) if th.ndim == 2 else th
            reps = np.tile(th, int(np.ceil(n / len(th))) + 1)[:n]
            thr[:, j] = lp(reps, 2500)
        torrent = (fr * 0.9 + thr / (np.abs(thr).max() + 1e-9) * 0.5) * e[:, None]
        pan = pan_sweep(n, -0.7, 0.7, 1.0)
        th_ = (pan + 1) * np.pi / 4
        torrent = np.stack([torrent[:, 0] * np.cos(th_) * 1.2 + torrent[:, 1] * 0.25, torrent[:, 1] * np.sin(th_) * 1.2 + torrent[:, 0] * 0.25], 1)
        whoomp = mix(0.8, [(lp(noise(N(0.6), 2, 710 + i), 400) * env_ar(N(0.6), 0.03, 0.15), 0, 1.2), (sub_thud(45, 0.6, 0.15, seed=i), 0, 0.6)])
        x = mix(L + 0.5, [(whoomp, 0, 1.0), (torrent, 0.05, 1.0), (crackle(L, 45, 4500, seed=720 + i) * e[:, None], 0.05, 0.5)])
        v.append(verb(x, 1.8, 0.22, 3500, seed=730 + i))
    out["titan_breath"] = v
    return out

# ---------------------------------------------------------------- casts and statuses
def bell(f, sec, seed, bright=1.0, tau=0.7):
    return modal([(f * r, a, tau * d) for r, a, d in [(1, 1, 1), (2.0, 0.5 * bright, 0.6), (2.76, 0.4 * bright, 0.45), (5.4, 0.25 * bright, 0.3), (8.9, 0.12 * bright, 0.2)]], sec, beat=1.5, seed=seed)

def casts():
    out = {}
    v = []
    for i in range(4):
        n = N(0.9); t = tt(n)
        burst = noise(n, 1, 800 + i)
        cutoff = 300 + 4000 * np.clip(t / 0.15, 0, 1)
        whoomp = np.zeros(n)
        for k in range(0, n, 512):  # moving low-pass in blocks
            seg = burst[k:k + 1024]; whoomp[k:k + len(seg)] += lp(seg, cutoff[k]) * np.hanning(len(seg)) if len(seg) > 8 else 0
        whoomp *= env_ar(n, 0.01, 0.25)
        x = mix(1.1, [(st(whoomp, 0), 0, 1.0), (fire_roar(0.9, seed=810 + i) * env_ar(N(0.9), 0.05, 0.3)[:, None], 0, 0.5), (crackle(0.9, 40, 5000, seed=820 + i), 0.05, 0.5)])
        v.append(verb(x, 1.0, 0.18, 5000, seed=i))
    out["cast_flame"] = v
    v = []
    for i in range(4):
        n = N(0.9); t = tt(n)
        w = to_mono(load(f"{SK}whooshes/whoosh-medium-medium.wav", 2)); w = hp(resample(w, 0.9 + 0.08 * i), 200)
        fc = 900 + 1400 * np.sin(np.pi * t / 0.9)
        whistle = np.zeros(n); src = noise(n, 0, 830 + i)
        for k in range(0, n, 512):
            seg = src[k:k + 1024]; whistle[k:k + len(seg)] += band(seg, fc[k] * 0.9, fc[k] * 1.1, 4) * np.hanning(len(seg)) if len(seg) > 8 else 0
        swirl = np.sin(2 * np.pi * 2.5 * t + i) * 0.7
        x = mix(1.0, [(st(w, pan_sweep(len(w), -0.5, 0.5)), 0, 1.0), (st(whistle * env_ar(n, 0.2, 0.25), swirl), 0, 0.5)])
        v.append(verb(x, 1.2, 0.2, 7000, seed=i))
    out["cast_gale"] = v
    v = []
    for i in range(4):
        splash = band(noise(N(0.5), 0, 840 + i), 400, 4000) * env_ar(N(0.5), 0.004, 0.09)
        x = mix(1.0, [(st(splash), 0, 0.8), (bubbles(0.8, 45, (350, 1600), seed=850 + i), 0.02, 0.6),
                      (sub_thud(90, 0.25, 0.06, 0.1, seed=i), 0, 0.5), (lp(K("impactSoft_medium", i), 1200), 0, 0.5)])
        v.append(verb(x, 1.1, 0.22, 5000, seed=i))
    out["cast_tide"] = v
    v = []
    for i in range(4):
        root = [880, 988, 1047, 1175][i]
        sp = np.zeros((N(1.4), 2))
        for j in range(5):
            d = N(0.04 * j); s = st(bell(root * [1, 1.25, 1.5, 2, 2.5][j], 1.0, 870 + j + i, 0.7, 0.4), (j - 2) * 0.35); sp[d:d + len(s)] += s * 0.4
        air = st(band(noise(N(1.0), 0, 880 + i), 3000, 12000) * env_ar(N(1.0), 0.25, 0.25), 0)
        bl = resample(K("impactBell_heavy", i), 2.0)
        x = mix(1.6, [(sp, 0, 1.0), (air, 0, 0.25), (bl, 0, 0.25)])
        v.append(verb(x, 1.6, 0.3, 8000, seed=i))
    out["cast_light"] = v
    v = []
    for i in range(4):
        n = N(1.1); t = tt(n)
        rw = to_mono(load(f"{SK}reverse-whooshes/reverse-whoosh-short-strong.wav", 2)); rw = lp(resample(rw, 0.8), 3000)
        drone = sum(np.sin(2 * np.pi * f * t + k) for k, f in enumerate([55, 55.7, 82.4, 110.6])) * env_ar(n, 0.15, 0.35)
        whisper = band(noise(n, 0, 890 + i), 2000, 6000) * (0.5 + 0.5 * np.sin(2 * np.pi * 7 * t)) * env_ar(n, 0.2, 0.2)
        x = mix(1.3, [(st(rw, -0.3), 0, 0.8), (st(lp(drone, 500)), 0.1, 0.35), (st(whisper, np.sin(2 * np.pi * 1.5 * t) * 0.6), 0.1, 0.18)])
        v.append(verb(x, 1.6, 0.3, 3000, seed=i))
    out["cast_shadow"] = v
    # statuses
    pent = [523.3, 587.3, 659.3, 784.0, 880.0, 1046.5]
    out["heal"] = [verb(mix(1.6, [(bell(pent[(j + i) % 6], 1.2, 900 + i * 9 + j, 0.5, 0.5), 0.09 * j, 0.5, -0.5 + 0.33 * j) for j in range(4)]
                             + [(st(band(noise(N(1.0), 1, 905 + i), 600, 3000) * env_ar(N(1.0), 0.3, 0.2)), 0, 0.12)]), 1.5, 0.3, 7000, seed=i) for i in range(3)]
    out["buff"] = [verb(mix(1.2, [(bell(pent[j + i], 0.9, 910 + i * 9 + j, 0.8, 0.35), 0.06 * j, 0.45, -0.3 + 0.3 * j) for j in range(3)]
                             + [(st(band(noise(N(0.5), 0, 915 + i), 1500, 9000) * np.linspace(0, 1, N(0.5)) ** 2, pan_sweep(N(0.5), -0.4, 0.4)), 0, 0.2)]), 1.2, 0.25, 7000, seed=i) for i in range(3)]
    def debuff_one(i):
        n = N(0.9); t = tt(n); f = 330 * (1 - 0.35 * t / 0.9)
        tone = (np.sin(2 * np.pi * np.cumsum(f) / SR) + np.sin(2 * np.pi * np.cumsum(f * 1.06) / SR) * 0.7) * env_ar(n, 0.02, 0.3)
        return verb(mix(1.1, [(st(lp(tone, 1500)), 0, 0.5), (st(lp(to_mono(load(f"{SK}whooshes/whoosh-slow-soft.wav", 2)), 1500)), 0, 0.5)]), 1.2, 0.25, 3000, seed=i)
    out["debuff"] = [debuff_one(i) for i in range(3)]
    def stun_one(i):
        ding = resample(K("impactBell_heavy", i), 1.8 + 0.1 * i)
        n = N(1.0); t = tt(n); circ = np.zeros((n, 2))
        for j in range(5):
            at = N(0.12 + 0.15 * j); L = N(0.07); tc = tt(L)
            ch = np.sin(2 * np.pi * np.cumsum(2600 + 1800 * np.sin(np.pi * tc / 0.07)) / SR) * np.sin(np.pi * tc / 0.07)
            p = np.sin(2 * np.pi * (j / 5 + i * 0.1)) * 0.7
            circ[at:at + L] += st(ch, p) * 0.12
        return verb(mix(1.3, [(ding, 0, 0.6), (circ, 0, 1.0)]), 1.0, 0.25, 7000, seed=i)
    out["stun"] = [stun_one(i) for i in range(3)]
    out["poison"] = [verb(mix(1.0, [(bubbles(0.8, 35, (160, 520), seed=930 + i, tau=(0.02, 0.06)), 0, 1.0),
                                    (st(band(noise(N(0.7), 0, 935 + i), 2500, 7000) * env_ar(N(0.7), 0.1, 0.2)), 0, 0.12)]), 0.9, 0.2, 4000, seed=i) for i in range(3)]
    def shield_one(i):
        n = N(1.0); t = tt(n)
        hum = sum(np.sin(2 * np.pi * f * t) for f in (220 * (1 + 0.01 * i), 277.2, 329.6, 440.5)) * env_ar(n, 0.15, 0.3)
        return verb(mix(1.2, [(K("impactPlate_light", i), 0, 0.7), (st(lp(hum, 2000)), 0, 0.25), (bell(1320, 0.8, 940 + i, 0.6, 0.3), 0.02, 0.2, 0.3)]), 1.1, 0.25, 6000, seed=i)
    out["shield"] = [shield_one(i) for i in range(3)]
    return out

# ---------------------------------------------------------------- ambience beds
def bed_from(name, sec, gain=1.0, start=0.0):
    """A CC0 field-recording bed made genuinely stereo: left and right are different stretches of the
    recording (uncorrelated, so the mono fold-down is clean), upsampled from its 16 kHz master."""
    x = load(f"{AM}{name}.ogg")
    n = N(sec); L = len(x)
    a = int(start * SR) % L; b = (a + L // 2) % L
    def seg(o): return np.tile(x, int(np.ceil((o + n) / L)) + 1)[o:o + n]
    return np.stack([seg(a), seg(b)], 1) * gain

def air_band(ref, lo=7000, hi=14000, amt=0.25, seed=0):
    """Restore the top octave the 16 kHz beds lack: noise in that band following the bed's own envelope."""
    n = len(ref); out = np.zeros((n, 2))
    for c in range(2):
        e = lp(np.abs(hp(ref[:, c], 3000)), 30)
        out[:, c] = band(noise(n, 0, seed + c), lo, hi) * e
    return out * amt / (np.sqrt(np.mean(out ** 2)) + 1e-12) * np.sqrt(np.mean(ref ** 2))

def wind_synth(sec, seed=0, lo=150, hi=1400, gust=0.12, whistle=True):
    n = N(sec); out = np.zeros((n, 2))
    for c in range(2):
        s = seed * 10 + c
        g = 0.55 + 0.45 * np.tanh(1.5 * smooth_rand(n, gust, s))
        body = band(noise(n, 1, s + 1), lo, hi) * g
        if whistle:
            fc = 700 + 350 * smooth_rand(n, 0.2, s + 2)
            src = noise(n, 0, s + 3); wh = np.zeros(n); blk = 2048
            for k in range(0, n, blk):
                seg = src[k:k + blk * 2]
                if len(seg) > 16: wh[k:k + len(seg)] += band(seg, fc[k] * 0.93, fc[k] * 1.07, 6) * np.hanning(len(seg))
            body += wh * g ** 2 * 0.6
        out[:, c] = body
    return out / (np.abs(out).max() + 1e-9)

def loopify(x, xf=2.0):
    """Seamless loop: the last xf seconds crossfade (equal power) into the start."""
    k = N(xf); a = x[:-k].copy(); tail = x[-k:]
    w = np.linspace(0, 1, k)[:, None]
    a[:k] = a[:k] * np.sin(w * np.pi / 2) + tail * np.cos(w * np.pi / 2)
    return a

def thunder_roll(sec, seed):
    t = lp(S("cinematic-hits/cinematic-thunder-big"), 1800)
    n = N(sec); out = np.zeros((n, 2))
    g = np.random.default_rng(seed)
    for at in (g.uniform(3, 8), g.uniform(15, 20)):
        r = resample(t, g.uniform(0.6, 0.8)); r = verb(r, 3.5, 0.6, 1500, seed=seed)
        i = N(at); m = min(len(r), n - i); out[i:i + m] += r[:m] * 0.6
    return out

def beds():
    D = 26.0   # 24 s loop after the 2 s crossfade
    b = {}
    forest = bed_from("scene_snow", D) * 0.8 + bed_from("scene_sakura", D, start=20) * 0.45
    b["forest"] = forest + air_band(forest, amt=0.12, seed=1)
    meadow = bed_from("scene_sakura", D, start=5) + wind_synth(D, 2, 200, 1200, whistle=False) * 0.05
    b["meadow"] = meadow + air_band(meadow, amt=0.1, seed=2)
    shore = bed_from("scene_ocean", D, start=10)
    b["shore"] = shore + air_band(shore, 6000, 14000, 0.35, seed=3)
    wind = wind_synth(D, 4, 120, 1600) * 0.6 + bed_from("scene_snow", D, start=30) * 0.25
    b["wind"] = wind
    marsh = bed_from("scene_firefly", D) + bed_from("scene_night", D, start=12) * 0.25 + st(lp(bubbles(D, 2.5, (90, 260), seed=5, tau=(0.03, 0.08)).mean(1), 900)) * 0.15
    b["marsh"] = marsh + air_band(marsh, amt=0.08, seed=5)
    fire = bed_from("scene_campfire", D) + crackle(D, 6, 4500, seed=6) * 0.25 + wind_synth(D, 6, 150, 900, whistle=False) * 0.12
    b["fire"] = fire + air_band(fire, amt=0.15, seed=6)
    storm = bed_from("scene_rain", D, start=3) + bed_from("scene_ocean", D, start=40) * 0.35 + thunder_roll(D, 7) * 0.5
    b["storm"] = storm + air_band(storm, 6000, 15000, 0.4, seed=7)
    # Titan caldera: sub-bass rumble, lava gloops, ember crackle, a deep wind
    n = N(D); t = tt(n)
    sub = np.stack([lp(noise(n, 2, 80 + c), 90) for c in range(2)], 1)
    sub += st((np.sin(2 * np.pi * 31 * t) + 0.6 * np.sin(2 * np.pi * 43.5 * t + 1)) * (0.6 + 0.4 * smooth_rand(n, 0.15, 81)) * 0.25)
    lava = bubbles(D, 3, (60, 180), seed=82, tau=(0.05, 0.15)) * 0.6
    cal = sub / (np.abs(sub).max() + 1e-9) * 0.8 + lp(lava, 700) + crackle(D, 9, 4000, seed=83) * 0.3 + wind_synth(D, 8, 80, 600, whistle=False) * 0.35
    b["caldera"] = cal
    for name, x in b.items():
        if ONLY and not ("amb_" + name).startswith(ONLY): continue
        y = loopify(x - x.mean(0), 2.0)
        y = y * (10 ** (-24 / 20) / (np.sqrt(np.mean(y ** 2)) + 1e-12))   # -24 dBFS RMS: a quiet bed under the music
        y = limit(y, 10 ** (-3 / 20), 0.4)
        write_ogg(y, os.path.join(OUT, f"amb_{name}.ogg"), q=1, sr=32000)
        REPORT["amb_" + name] = stats(y)
        if PREVIEW: np.save(os.path.join(PREVIEW, f"amb_{name}.npy"), y[:N(8)].astype(np.float32))
        print(f"amb_{name:12s} {REPORT['amb_' + name]}")

if __name__ == "__main__":
    for group, loud in ((swings, -16), (hits, -13), (armour, -14), (titan, -10), (casts, -15)):
        for name, vs in group().items():
            l = loud
            if name in ("crit",): l = -13
            if name in ("kill", "downed"): l = -14
            if name in ("heal", "buff", "debuff", "stun", "poison", "shield"): l = -17
            if name == "titan_swing": l = -13
            if name == "titan_step": l = -12
            if name in ("titan_roar", "titan_breath"): l = -11
            save(name, vs, l, win=0.4 if name.startswith(("titan", "earth", "meteor")) else 0.2,
                 r=0.3 if name.startswith(("titan", "earth", "meteor")) else 0.04)
    beds()
    if not ONLY:
        json.dump(REPORT, open(os.path.join(os.path.dirname(os.path.abspath(__file__)), "real_report.json"), "w"), indent=1)
