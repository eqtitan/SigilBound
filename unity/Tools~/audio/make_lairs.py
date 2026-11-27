"""Boss-lair set-piece sounds (story scenes, 2026-11-26): the landmark each chapter boss is fought at
reacts to the fight, and the client plays these through Sound.PlayAt(cue, worldPos).

Same CC0 sources as make_real.py (fetch_sources.sh) plus code-made layers: modal bell and glass
synthesis, fire, bubbles, wind, generated stereo halls. Writes sfx_lair_<cue>.ogg (+ _2, _3 variants).

Run:  AUDIO_SRC=~/.cache/sigil-audio-src ./fetch_sources.sh && python3 make_lairs.py
"""
import os
import numpy as np
from stereo import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")
KI = "kenney-impact/sample/assets/audio/impact/"
KR = "kenney-rpg/assets/kenney_rpgaudio/Audio/"
KS = "kenney-scifi/sounds/"
SK = "sfx-kit/sounds/"
def K(name, i): return load(f"{KI}{name}_{i % 5:03d}.ogg")
def R(name): return load(f"{KR}{name}.ogg")
def S(path): return to_mono(load(f"{SK}{path}.wav", 2))

def save(name, variants, loud, max_sec, win=0.3):
    for i, x in enumerate(variants):
        y = finish(x, loud, win=win, r=0.05)
        if len(y) > N(max_sec): y = fade(y[:N(max_sec)], 0, min(0.9, max_sec * 0.3))
        fn = f"sfx_lair_{name}" + ("" if i == 0 else f"_{i + 1}")
        write_ogg(y, os.path.join(OUT, fn + ".ogg"), q=0)
        st_ = stats(y)
    print(f"lair_{name:8s} x{len(variants)}  loud {st_['loud_db']}  corr {st_['corr']}  {st_['sec']}s")

def thud(f0, sec, tau, seed=0):
    n = N(sec); t = tt(n); f = f0 * (1 + 1.0 * np.exp(-t / 0.03))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / tau)

# ---- lairBell: a big cracked bronze bell, one toll, long ring, hall tail
def bell():
    out = []
    for i in range(3):
        f = [98.0, 92.5, 104.0][i]
        # minor-third church bell partials (hum, prime, tierce, quint, nominal, upper) with a crack:
        # pairs split wide so they beat roughly, and a buzzy rattle on the strike
        P = [(0.5, 0.6, 5.0), (1.0, 1.0, 3.6), (1.19, 0.7, 2.6), (1.5, 0.45, 2.0), (2.0, 0.8, 1.8), (2.51, 0.35, 1.2), (2.66, 0.3, 1.0),
             (3.01, 0.3, 0.8), (4.1, 0.2, 0.5), (5.3, 0.12, 0.35), (6.7, 0.08, 0.25)]
        ring = modal([(f * r, a, tau) for r, a, tau in P], 6.0, beat=2.6 + i, seed=1100 + i)
        strike = resample(K("impactBell_heavy", i), 0.42 + 0.03 * i)
        clank = lp(resample(K("impactMetal_heavy", i), 0.55), 3000)
        crack = band(noise(N(0.35), 0, 1110 + i), 600, 3500) * np.exp(-tt(N(0.35)) / 0.05) * (1 + np.sign(np.sin(2 * np.pi * f * 1.19 * tt(N(0.35)))))
        x = mix(6.2, [(ring, 0, 1.0), (strike, 0, 0.6), (clank, 0, 0.35), (crack, 0, 0.15)])
        out.append(verb(x, 4.5, 0.38, 2600, predelay=0.04, seed=1120 + i, lowmult=1.3))
    save("bell", out, -14, 5.5, win=0.5)

# ---- lairWheel: a waterlogged mill wheel lurching faster: creak, paddle slaps, water rush and slosh
def wheel():
    out = []
    for i in range(3):
        L = 3.0; n = N(L)
        rush = np.stack([band(noise(n, 1, 1200 + i * 3 + c), 200, 2500) for c in range(2)], 1)
        rush *= np.interp(np.arange(n), [0, N(0.4), N(2.2), n], [0.2, 0.6, 1.0, 0.0])[:, None]
        layers = [(rush, 0, 0.35)]
        creaks = [R(f"creak{k}") for k in (1, 2, 3)]
        layers.append((lp(resample(creaks[i % 3], 0.62), 2500), 0.0, 0.8, -0.3))
        layers.append((lp(resample(creaks[(i + 1) % 3], 0.7), 2500), 1.1, 0.6, 0.3))
        # paddles slap the water, accelerating as the wheel lurches
        t = 0.15; gap = 0.42; k = 0
        while t < L - 0.3:
            pan = 0.6 * np.sin(k * 1.3)
            slap = lp(resample(K("impactWood_heavy", k + i), 0.6), 1800)
            splash = band(noise(N(0.3), 0, 1230 + 17 * i + k), 300, 3000) * np.exp(-tt(N(0.3)) / 0.06)
            layers += [(slap, t, 0.45, pan), (splash, t + 0.01, 0.4, pan), (bubbles(0.4, 40, (250, 900), seed=1240 + 9 * i + k), t + 0.02, 0.35)]
            t += gap; gap = max(0.17, gap * 0.82); k += 1
        layers.append((thud(55, 0.6, 0.15), 0, 0.5))   # the shaft jolting
        out.append(verb(mix(L + 0.2, layers), 1.3, 0.22, 3500, seed=1250 + i))
    save("wheel", out, -15, 3.4)

# ---- lairPyre: a pyre / torch flaring up: a deep whoosh of fire with crackle
def pyre():
    out = []
    for i in range(3):
        L = 2.2; n = N(L)
        whoomp = lp(noise(N(0.8), 2, 1300 + i), 500) * env_ar(N(0.8), 0.05, 0.22)
        roar = fire_roar(L, seed=1310 + i, body=450, hiss=3200) * np.interp(np.arange(n), [0, N(0.12), N(0.5), n], [0, 1, 0.6, 0])[:, None]
        th = load(f"{KS}SCIFI/SCIShip_ThrusterFire0{1 + i}_Kenney_KenneySciFi.ogg")
        th = lp(resample(to_mono(th) if th.ndim == 2 else th, 0.7), 2200)
        rise = S("reverse-whooshes/reverse-whoosh-short-soft")
        x = mix(L + 0.3, [(st(rise, 0), 0, 0.4), (whoomp, 0.12, 1.0), (thud(48, 0.6, 0.18), 0.12, 0.5), (roar, 0.1, 0.7),
                          (fade(trim(th, 1.2), 0.03, 0.5), 0.12, 0.4), (crackle(L, 30, 4500, seed=1320 + i), 0.15, 0.55)])
        out.append(verb(x, 1.4, 0.2, 4000, seed=1330 + i))
    save("pyre", out, -15, 2.6)

# ---- lairBeam: a lighthouse lamp sweeping past: a low resonant glassy hum that swells
def beam():
    out = []
    for i in range(3):
        L = 3.4; n = N(L); t = tt(n)
        f0 = [110.0, 103.8, 116.5][i]
        sw = np.exp(-((t - L * 0.5) / (L * 0.22)) ** 2)          # the beam passing over
        hum = sum(a * np.sin(2 * np.pi * f0 * r * t + r) for r, a in [(1, 1), (2.0, 0.5), (2.76, 0.35), (4.07, 0.25), (5.4, 0.15), (8.9, 0.08)])
        hum *= (1 + 0.15 * np.sin(2 * np.pi * 0.9 * t)) * (0.25 + 0.75 * sw)
        glass = modal([(f0 * r * 4, a, 1.5) for r, a in [(1, 0.6), (1.5, 0.4), (2.01, 0.3)]], L, beat=0.8, seed=1400 + i) * (0.2 + 0.8 * sw)
        air = band(noise(n, 1, 1410 + i), 400, 3000) * sw * 0.25
        pan = pan_sweep(n, -0.8, 0.8)   # the lamp turns: the swell crosses the stereo field
        x = mix(L, [(st(hum, pan * 0.6), 0, 0.7), (st(glass, pan), 0, 0.5), (st(air, pan), 0, 0.6)])
        out.append(verb(x, 2.5, 0.35, 3500, seed=1420 + i))
    save("beam", out, -17, 4.0, win=0.6)

# ---- lairSnuff: a lantern snuffed out: a soft gust, a hiss, a faint eerie tone
def snuff():
    out = []
    for i in range(3):
        gust = lp(S(["whooshes/whoosh-slow-soft", "whooshes/whoosh-medium-soft", "whooshes/whoosh-slow-soft"][i]), 2500)
        n = N(0.6); hiss = hp(noise(n, 0, 1500 + i), 3500) * env_ar(n, 0.01, 0.12)
        m = N(2.2); t = tt(m); f = [659.3, 622.3, 698.5][i]
        tone = (np.sin(2 * np.pi * f * t) + 0.6 * np.sin(2 * np.pi * f * 1.012 * t) + 0.3 * np.sin(2 * np.pi * f * 1.5 * t)) * env_ar(m, 0.3, 0.6)
        tone *= 1 + 0.3 * np.sin(2 * np.pi * 4.5 * t)
        x = mix(2.6, [(st(gust, pan_sweep(len(gust), -0.4, 0.4)), 0, 0.9), (hiss, 0.12, 0.35), (tone, 0.2, 0.09, 0.25 - 0.25 * i)])
        out.append(verb(x, 2.4, 0.35, 3000, seed=1510 + i))
    save("snuff", out, -17, 2.8)

# ---- lairThunder: a close lightning strike: a sharp crack into a rolling stereo tail
def thunder():
    out = []
    for i in range(3):
        n = N(0.25); t = tt(n)
        crack = np.stack([hp(noise(n, 0, 1600 + i * 2 + c), 1500) * np.exp(-t / 0.035) for c in range(2)], 1)
        sizzle = np.stack([band(noise(N(0.5), 0, 1610 + i * 2 + c), 2500, 9000) * np.exp(-tt(N(0.5)) / 0.08) * (np.random.default_rng(1620 + i + c).random(N(0.5)) > 0.97) * 4 for c in range(2)], 1)
        boom = lp(S(["cinematic-hits/cinematic-thunder-massive", "cinematic-hits/cinematic-thunder-big", "cinematic-hits/cinematic-thunder-massive"][i]), 3000)
        roll = np.stack([lp(noise(N(4.5), 2, 1630 + i * 2 + c), 220) * (0.5 + 0.5 * np.abs(smooth_rand(N(4.5), 3, 1640 + i * 2 + c))) for c in range(2)], 1)
        roll *= np.interp(np.arange(N(4.5)), [0, N(0.15), N(1.0), N(4.5)], [0, 1, 0.7, 0])[:, None]
        x = mix(5.0, [(crack, 0, 1.0), (sizzle, 0, 0.5), (resample(boom, 0.9 + 0.05 * i), 0.02, 0.9), (roll / (np.abs(roll).max() + 1e-9), 0.1, 0.5),
                      (thud(40, 1.0, 0.3), 0.02, 0.6)])
        out.append(verb(x, 3.0, 0.3, 3000, predelay=0.02, seed=1650 + i))
    save("thunder", out, -13, 5.0, win=0.3)

# ---- lairChime: glass spires singing in the wind: an eerie wind-glass chord
def chime():
    out = []
    chords = [[659.3, 784.0, 987.8, 1244.5], [622.3, 830.6, 932.3, 1174.7], [698.5, 880.0, 1046.5, 1318.5]]
    for i in range(3):
        L = 3.6; n = N(L); layers = []
        g = np.random.default_rng(1700 + i)
        for j, f in enumerate(chords[i]):
            at = 0.05 + j * 0.18 + g.uniform(0, 0.08)
            note = modal([(f, 1, 2.2), (f * 2.32, 0.35, 1.0), (f * 4.25, 0.15, 0.5)], L - at, beat=1.2, seed=1710 + i * 5 + j)
            note *= np.clip(tt(len(note)) / 0.25, 0, 1)   # bowed by wind: swells in instead of striking
            layers.append((note, at, 0.35, -0.6 + 0.4 * j))
        layers.append((resample(K("impactGlass_light", i), 1.4), 0.02, 0.15, 0.4))
        wind = np.stack([band(noise(n, 1, 1720 + 2 * i + c), 500, 2500) for c in range(2)], 1) * np.interp(np.arange(n), [0, N(1.2), n], [0, 1, 0])[:, None]
        layers.append((wind, 0, 0.12))
        out.append(verb(mix(L, layers), 2.8, 0.38, 7000, seed=1730 + i))
    save("chime", out, -16, 4.2, win=0.6)

# ---- lairDrip: a cavern drip swelling into a flooded nave's long reverb
def drip():
    out = []
    for i in range(3):
        layers = []
        g = np.random.default_rng(1800 + i)
        for k, at in enumerate([0.0, 0.55 + g.uniform(0, 0.2)][: 1 + (i != 1)]):
            L = N(0.06); t = tt(L); f0 = g.uniform(1100, 1800)
            plip = np.sin(2 * np.pi * np.cumsum(f0 * (1 + 3 * t / 0.06)) / SR) * np.exp(-t / 0.012)
            layers += [(plip, at, 0.8 / (1 + k), g.uniform(-0.4, 0.4)), (bubbles(0.15, 25, (500, 1300), seed=1810 + 3 * i + k, tau=(0.008, 0.02)), at + 0.01, 0.25 / (1 + k))]
        n = N(3.0)
        swell = np.stack([band(noise(n, 1, 1820 + 2 * i + c), 150, 900) for c in range(2)], 1) * np.interp(np.arange(n), [0, N(0.4), N(1.2), n], [0, 0.6, 1, 0])[:, None]
        layers.append((swell, 0, 0.06))
        out.append(verb(mix(3.0, layers), 4.0, 0.55, 3000, predelay=0.05, seed=1830 + i, lowmult=1.2))
    save("drip", out, -17, 4.0, win=0.4)

# ---- lairReveal: the 2.5 s camera reveal of a lair: sub-bass rising, a distant choir/wind pad, a soft
# low boom at ~2.2 s as the camera settles, then a wide tail
def reveal():
    out = []
    chords = [[73.4, 110.0, 146.8, 174.6], [69.3, 103.8, 138.6, 164.8], [77.8, 116.5, 155.6, 185.0]]   # low minor triads (D, C#, D#)
    for i in range(3):
        L = 3.4; n = N(L); t = tt(n); hit = 2.2
        rise = np.clip(t / hit, 0, 1) ** 1.6 * np.interp(t, [0, hit, hit + 0.15, L], [1, 1, 0.55, 0])
        f = 28 + 16 * np.clip(t / hit, 0, 1)
        sub = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.5 * lp(noise(n, 2, 1900 + i), 70)) * rise
        # choir-like pad: detuned saw voices through "oo"->"ah" formants, each side detuned differently (wide, not phasey)
        pad = np.zeros((n, 2))
        for c in range(2):
            src = np.zeros(n)
            for j, fr in enumerate(chords[i]):
                det = 1 + (0.004 if c else -0.004) * (j + 1)
                ph = 2 * np.pi * np.cumsum(fr * det * (1 + 0.003 * np.sin(2 * np.pi * (4.7 + j) * t + c))) / SR
                src += sum(np.sin(k * ph) / k for k in range(1, 30) if fr * k < 8000)
            F1 = lambda u: np.interp(u, [0, 1], [320, 650]); F2 = lambda u: np.interp(u, [0, 1], [800, 1080])
            pad[:, c] = formant_filter(src, [F1, F2, lambda u: 2500.0], bw=(80, 100, 200))
        pad = pad / (np.abs(pad).max() + 1e-9) * np.interp(t, [0, 1.2, hit, L], [0, 0.6, 1.0, 0.0])[:, None]
        wind = np.stack([band(noise(n, 1, 1910 + 2 * i + c), 250, 1800) * (0.6 + 0.4 * smooth_rand(n, 0.8, 1915 + 2 * i + c)) for c in range(2)], 1)
        wind *= np.interp(t, [0, 1.5, hit, L], [0, 0.8, 1.0, 0.0])[:, None]
        boom = mix(1.4, [(lp(S(["booms-and-bass/boom-hit-soft", "booms-and-bass/boom-drop-soft", "booms-and-bass/boom-hit-medium"][i]), 400), 0, 0.8),
                         (thud(38 + 3 * i, 1.2, 0.35), 0, 1.0)])
        x = mix(L + 0.2, [(st(sub), 0, 0.6), (pad, 0, 0.22), (wind, 0, 0.12), (boom, hit, 1.5)])
        out.append(verb(x, 3.2, 0.3, 2500, predelay=0.03, seed=1920 + i, lowmult=1.4))
    save("reveal", out, -15, 3.4, win=0.5)

# ---- lairShard: a Tide/Shadow shard pulsing: a deep crystalline pulse, glassy, with a sub thump
def shard():
    out = []
    for i in range(3):
        f0 = [220.0, 207.7, 233.1][i]
        glass = modal([(f0 * r, a, tau) for r, a, tau in [(1, 0.7, 0.9), (2.76, 0.6, 0.7), (5.40, 0.45, 0.5), (8.93, 0.3, 0.35), (13.3, 0.2, 0.25)]], 1.6, beat=1.8, seed=2000 + i)
        n = N(0.12); pre = np.stack([band(noise(n, 0, 2010 + 2 * i + c), 2500, 9000) * np.linspace(0, 1, n) ** 3 for c in range(2)], 1)   # an inhaled shimmer
        tick = resample(K("impactGlass_light", i), 0.6)
        x = mix(1.9, [(pre, 0, 0.25), (st(glass, -0.25), 0.12, 0.6), (st(resample(glass, 1.003), 0.25), 0.12, 0.6), (tick, 0.12, 0.25),
                      (thud(46 + 3 * i, 0.5, 0.12), 0.12, 0.9)])
        out.append(verb(x, 1.8, 0.33, 6000, seed=2020 + i))
    save("shard", out, -16, 2.0, win=0.3)

if __name__ == "__main__":
    import sys
    allf = dict(bell=bell, wheel=wheel, pyre=pyre, beam=beam, snuff=snuff, thunder=thunder, chime=chime, drip=drip, reveal=reveal, shard=shard)
    for k in (sys.argv[1:] or list(allf)): allf[k]()
