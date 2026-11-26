"""Procedural sound effects for Sigilbound (owner: free tools only). Writes Assets/Resources/Audio/sfx_*.ogg.

    python3 make_sfx.py [--png DIR]   (--png also renders a waveform/spectrogram sheet for checking)

Every effect is synthesized here from noise, oscillators and filters (synth.py): per weapon family
swings and hits, element spells, status sounds, the Titan's roar/slam/crack/leap, UI and summon
stings. Levels: peak -1 dBFS, loudness by category (hits loud, UI soft) so they sit together.
"""
import os, sys
import numpy as np
from synth import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")
S = {}   # name -> (signal, rms_db)

def swoosh(sec, f0, f1, q=1.0):
    """A whoosh: noise band sweeping f0 -> f1 with a swell."""
    n = int(SR * sec); x = noise(sec)
    # cheap sweep: split in 8 slices, each band-passed at its centre, cross-faded
    out = np.zeros(n); sl = 8; w = n // sl
    for s in range(sl):
        fc = f0 * (f1 / f0) ** (s / (sl - 1))
        b = band(x, fc / (1.6 * q), fc * 1.6 * q)
        win = np.zeros(n); a = max(0, s * w - w // 2); z = min(n, (s + 1) * w + w // 2)
        win[a:z] = np.hanning(z - a); out += b * win
    return out * np.sin(np.linspace(0, np.pi, n)) ** 1.5

def thud(f0=70, sec=0.4, click=0.4):
    n = int(SR * sec); f = f0 * (1 + 1.6 * expdecay(n, 0.03))
    body = sine(f, sec) * expdecay(n, sec / 4)
    return body + click * lowpass(noise(sec), 2500) * expdecay(n, 0.012)

def metal(f0, sec, n_partials=6, tau=0.25, inharm=1.47):
    n = int(SR * sec); tt = np.arange(n) / SR; out = np.zeros(n)
    for k in range(n_partials):
        fk = f0 * (1 + k * inharm + 0.13 * k * k)
        if fk > SR / 2.3: break
        out += np.sin(2 * np.pi * fk * tt + k) * np.exp(-tt / (tau / (1 + 0.5 * k))) / (1 + k)
    return out

# ---------- weapon families: swing (attack starts) and hit (contact) ----------
S["sfx_swing_blade"] = (swoosh(0.28, 900, 3200, 0.8), -17)
S["sfx_swing_heavy"] = (swoosh(0.45, 220, 900, 1.2), -16)
S["sfx_swing_polearm"] = (swoosh(0.22, 1400, 2600, 0.7), -18)
S["sfx_swing_daggers"] = (np.concatenate([swoosh(0.12, 2000, 5000, 0.6), swoosh(0.12, 2400, 5600, 0.6)]), -18)
n = int(SR * 0.35); tw = sine(np.full(n, 190.0) * (1 + 0.06 * expdecay(n, 0.02)), 0.35) * expdecay(n, 0.09) + 0.5 * metal(780, 0.35, tau=0.08)
S["sfx_swing_bow"] = (tw + 0.3 * lowpass(noise(0.35), 4000) * expdecay(n, 0.01), -17)            # string twang
S["sfx_arrow"] = (swoosh(0.3, 3000, 1200, 0.5) * 0.9, -20)                                          # arrow in flight
S["sfx_swing_shield"] = (swoosh(0.3, 300, 1100, 1.4), -17)
S["sfx_swing_unarmed"] = (swoosh(0.2, 500, 1800, 1.0), -18)
S["sfx_hit_blade"] = (metal(2100, 0.45, tau=0.18) * 0.6 + band(noise(0.45), 2500, 9000) * expdecay(int(SR * .45), 0.03) + thud(120, 0.45, 0.2) * 0.5, -14)
S["sfx_hit_heavy"] = (thud(55, 0.6, 0.7) * 1.2 + lowpass(noise(0.6), 900) * expdecay(int(SR * .6), 0.08) * 0.6, -13)
S["sfx_hit_polearm"] = (band(noise(0.3), 1200, 5000) * expdecay(int(SR * .3), 0.025) + thud(150, 0.3, 0.1) * 0.7 + metal(1600, 0.3, tau=0.07) * 0.3, -14)
S["sfx_hit_daggers"] = (band(noise(0.18), 3000, 10000) * expdecay(int(SR * .18), 0.012) + metal(3200, 0.18, tau=0.05) * 0.5, -16)
S["sfx_hit_arrow"] = (thud(260, 0.25, 0.5) * 0.8 + band(noise(0.25), 1500, 6000) * expdecay(int(SR * .25), 0.01), -15)
S["sfx_hit_shield"] = (metal(420, 0.6, n_partials=8, tau=0.3) * 0.8 + thud(90, 0.6, 0.6), -13)
S["sfx_hit_unarmed"] = (thud(95, 0.3, 0.9), -14)
n = int(SR * 0.4); zap = saw(1800 * expdecay(n, 0.08) + 300, 0.4) * expdecay(n, 0.1)
S["sfx_hit_magic"] = (band(zap, 200, 7000) + band(noise(0.4), 4000, 12000) * expdecay(n, 0.04) * 0.4, -15)

# ---------- element spells (casts) ----------
n = int(SR * 0.9)
S["sfx_cast_flame"] = (swoosh(0.9, 300, 1600, 1.6) + band(noise(0.9), 1500, 6000) * (rng.random(n) > 0.995) * 6 * env(n, 0.05, 0.7, 0.2, 0.15), -15)  # whoosh + crackle
S["sfx_cast_gale"] = (swoosh(1.0, 400, 2400, 1.0) + swoosh(1.0, 900, 3500, 0.6) * 0.6, -16)
x = lowpass(noise(0.9), 1800) * env(n, 0.01, 0.25, 0.25, 0.4)
bub = np.zeros(n)
for k in range(9): place(bub, sine(np.linspace(500 + 90 * k, 1300 + 120 * k, int(SR * 0.05)), 0.05) * np.hanning(int(SR * .05)), 0.08 + k * 0.07)
S["sfx_cast_tide"] = (x + 0.5 * bub, -15)
ch = sum(pluck(midi(m), 1.2, 0.35, 1.4) for m in (84, 88, 91, 96)) + metal(2600, 1.2, tau=0.9) * 0.2
S["sfx_cast_light"] = (reverb(ch, 1.2, 0.35)[:int(SR * 1.4)], -17)
hum = (saw(55, 1.0) + saw(55.6, 1.0) + 0.5 * sine(110 + 3 * sine(0.7, 1.0), 1.0)) * env(int(SR * 1.0), 0.15, 0.3, 0.6, 0.35)
S["sfx_cast_shadow"] = (lowpass(hum, 700) + 0.3 * swoosh(1.0, 1800, 300, 0.8), -16)

# ---------- status ----------
sh = sum(pluck(midi(m), 0.8, 0.3, 0.7) * 0.5 for m in (79, 83, 86, 91))
S["sfx_buff"] = (reverb(sh, 1.0, 0.3)[:int(SR * 0.9)], -18)
n = int(SR * 0.6); S["sfx_debuff"] = (square(np.linspace(520, 160, n), 0.6) * env(n, 0.01, 0.2, 0.5, 0.25) * 0.5, -19)
tw = np.zeros(int(SR * 0.9))
for k, m in enumerate((96, 100, 93, 98, 95)): place(tw, pluck(midi(m), 0.3, 0.2, 0.25), k * 0.11)
S["sfx_stun"] = (tw, -19)
pb = np.zeros(int(SR * 0.8))
for k in range(7): place(pb, sine(np.linspace(260 + 40 * (k % 3), 700 + 60 * k, int(SR * .07)), 0.07) * np.hanning(int(SR * .07)), 0.04 + k * 0.1 + 0.03 * (k % 2))
S["sfx_poison"] = (lowpass(pb, 2500), -19)
n = int(SR * 0.8); S["sfx_shield"] = (reverb(metal(880, 0.8, tau=0.5) * 0.6 + sine(440, 0.8) * env(n, 0.02, 0.3, 0.3, 0.3) * 0.5, 1.0, 0.3)[:n], -18)
S["sfx_heal"] = (reverb(sum(pluck(midi(m), 1.0, 0.35, 1.0) for m in (72, 76, 79, 84)), 1.2, 0.35)[:int(SR * 1.1)], -18)

# ---------- the Titan ----------
n = int(SR * 2.2); g = np.zeros(n)
for f, w in ((48, 1), (73, 0.7), (97, 0.5)):
    vib = f * (1 + 0.04 * sine(5.5, 2.2) + 0.08 * np.linspace(0, 1, n) * (1 - np.linspace(0, 1, n)) * 4)
    g += saw(vib, 2.2, 30) * w
growl = lowpass(g * (1 + 0.5 * band(noise(2.2), 20, 60)), 1100) + 0.35 * band(noise(2.2), 300, 2500)
S["sfx_titan_roar"] = (reverb(growl * env(n, 0.25, 0.4, 0.8, 0.9), 1.8, 0.3)[:int(SR * 2.6)], -12)
n = int(SR * 1.6); slam = thud(38, 1.6, 1.0) * 1.4 + lowpass(noise(1.6), 600) * expdecay(n, 0.25) * 0.8 + band(noise(1.6), 1500, 6000) * expdecay(n, 0.05) * 0.5
S["sfx_titan_slam"] = (reverb(slam, 2.0, 0.25)[:int(SR * 2.2)], -11)
n = int(SR * 1.8); crack = np.zeros(n)
for k in range(14): place(crack, band(noise(0.06), 800, 6000) * expdecay(int(SR * .06), 0.01) * (1.4 - k * 0.07), 0.02 + k * 0.035 + 0.01 * rng.random())
crack += thud(32, 1.8, 0.8) * 1.3 + lowpass(noise(1.8), 400) * expdecay(n, 0.5) * 0.9
S["sfx_earthsplit"] = (reverb(crack, 2.2, 0.3)[:int(SR * 2.4)], -11)
S["sfx_meteor_whoosh"] = (swoosh(1.3, 120, 1400, 1.8) * np.linspace(0.6, 1.2, int(SR * 1.3)), -14)

# ---------- UI and rewards ----------
S["sfx_tap"] = (pluck(midi(84), 0.09, 0.2, 0.05) + 0.2 * band(noise(0.09), 2000, 8000) * expdecay(int(SR * .09), 0.004), -24)
S["sfx_confirm"] = (np.concatenate([pluck(midi(76), 0.09, 0.35, 0.2), pluck(midi(83), 0.25, 0.35, 0.25)]), -20)
co = np.zeros(int(SR * 0.5)); place(co, metal(1900, 0.4, tau=0.15), 0); place(co, metal(2500, 0.4, tau=0.18), 0.07)
S["sfx_reward"] = (co, -19)
def sting(notes, sec, bright, rv):
    x = np.zeros(int(SR * sec))
    for k, (m, at) in enumerate(notes): place(x, pluck(midi(m), sec - at, bright, 1.2) + 0.5 * pluck(midi(m + 12), sec - at, bright * 0.7, 0.9), at)
    return reverb(x, 1.5, rv)[:int(SR * (sec + 0.6))]
S["sfx_summon_rare"] = (sting([(67, 0), (71, 0.08), (74, 0.16)], 1.0, 0.4, 0.25), -18)
S["sfx_summon_epic"] = (sting([(64, 0), (68, 0.08), (71, 0.16), (76, 0.26)], 1.4, 0.5, 0.3), -17)
leg = sting([(60, 0), (64, 0.1), (67, 0.2), (72, 0.32), (76, 0.44), (79, 0.56), (84, 0.72)], 2.2, 0.55, 0.35)
leg[:len(leg)] += 0.3 * reverb(metal(1046, 2.0, tau=1.2), 1.5, 0.3)[:len(leg)]
S["sfx_summon_legendary"] = (leg, -15)
n = int(SR * 0.7); S["sfx_downed"] = (square(np.linspace(330, 110, n), 0.7) * env(n, 0.01, 0.4, 0.3, 0.25) * 0.4 + thud(70, 0.7, 0.2) * 0.5, -18)

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    total = 0; rows = []
    for name, (x, rms) in S.items():
        y = fade(norm(x, -1.0, rms), 0.002, 0.03)
        total += write_ogg(y, os.path.join(OUT, name + ".ogg"), q=2)
        pk, r = stats(y); rows.append((name, len(y) / SR, pk, r))
    for name, d, pk, r in rows: print(f"{name:24s} {d:5.2f}s  peak {pk:6.1f} dB  rms {r:6.1f} dB")
    print(f"{len(S)} effects, {total / 1024:.0f} KB")
    if "--png" in sys.argv:
        import plot; plot.sheet({k: fade(norm(x, -1.0, r)) for k, (x, r) in S.items()}, sys.argv[sys.argv.index("--png") + 1], "sfx")
