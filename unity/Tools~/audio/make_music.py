"""Procedural music loops for Sigilbound (owner: free tools only). Writes Assets/Resources/Audio/music_*.ogg.

    python3 make_music.py [--png DIR]

- music_menu:    D dorian, 84 bpm, harp arpeggios, string pad, a flute-like melody (~69 s loop)
- music_titan:   C phrygian, 124 bpm, taiko and low drums, brass ostinato, choir pad (~70 s loop)
- music_victory: a short brass-and-harp fanfare (sting, not a loop)
Loops are rendered an exact number of bars with the reverb tail folded back to the start, so they
repeat without a seam. Loudness: RMS -18 dBFS, peaks under -1 dBFS. (Normal battles keep the
composed CC0 battle_theme; see CREDITS.md. The stereo boss, act 2 and map themes: make_music2.py.)
"""
import os, sys
import numpy as np
from synth import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")
r = np.random.default_rng(11)

def pad(chord, sec, bright=1400, detune=0.004):
    n = int(SR * sec); x = np.zeros(n)
    for m in chord:
        f = midi(m)
        for d in (-detune, 0, detune): x += saw(f * (1 + d), sec, 12)
    return lowpass(x / (3 * len(chord)), bright) * env(n, min(0.6, sec * 0.3), 0.2, 0.85, min(0.8, sec * 0.3))

def choir(chord, sec):
    n = int(SR * sec); x = np.zeros(n); tt = np.arange(n) / SR
    for m in chord:
        f = midi(m) * (1 + 0.004 * np.sin(2 * np.pi * 5.2 * tt + m))
        for k, a in ((1, 1), (2, 0.5), (3, 0.35), (4, 0.2), (5, 0.12)): x += a * sine(f * k, sec)
    x = band(x, 200, 2600) / (2.2 * len(chord))
    return x * env(n, min(0.7, sec * 0.35), 0.2, 0.9, min(0.9, sec * 0.35))

def flute(m, sec):
    n = int(SR * sec); tt = np.arange(n) / SR
    f = midi(m) * (1 + 0.006 * np.sin(2 * np.pi * 5.0 * tt) * np.clip(tt / 0.3, 0, 1))
    x = sine(f, sec) + 0.28 * sine(f * 2, sec) + 0.08 * sine(f * 3, sec)
    x += 0.04 * band(noise(sec), midi(m) * 0.8, midi(m) * 3)
    return x * env(n, 0.06, 0.12, 0.75, min(0.25, sec * 0.4))

def brass(m, sec, bright=1800):
    n = int(SR * sec); f = midi(m)
    x = saw(f, sec, 18) + 0.6 * saw(f * 1.003, sec, 18)
    swell = env(n, 0.05, 0.15, 0.7, min(0.2, sec * 0.3))
    return lowpass(x, bright) * swell * 0.7

def bass(m, sec):
    n = int(SR * sec); f = midi(m)
    return (sine(f, sec) + 0.4 * lowpass(saw(f, sec, 8), 600)) * env(n, 0.01, 0.2, 0.7, 0.08)

def kick(sec=0.5):
    n = int(SR * sec); f = 50 * (1 + 2.5 * expdecay(n, 0.025))
    return sine(f, sec) * expdecay(n, 0.13) + 0.2 * lowpass(noise(sec), 1800) * expdecay(n, 0.006)

def taiko(sec=0.9, f0=62):
    n = int(SR * sec); f = f0 * (1 + 0.6 * expdecay(n, 0.04))
    return sine(f, sec) * expdecay(n, 0.22) + 0.5 * lowpass(noise(sec), 900) * expdecay(n, 0.03) + 0.3 * sine(f * 1.5, sec) * expdecay(n, 0.1)

def snare(sec=0.3):
    n = int(SR * sec)
    return band(noise(sec), 900, 7000) * expdecay(n, 0.06) * 0.8 + sine(190, sec) * expdecay(n, 0.04) * 0.5

def hat(sec=0.08, open_=False):
    n = int(SR * sec); return highpass(noise(sec), 7000) * expdecay(n, 0.05 if open_ else 0.012) * 0.5

def scale_notes(root, mode):
    steps = {"dorian": [0, 2, 3, 5, 7, 9, 10], "phrygian": [0, 1, 3, 5, 7, 8, 10], "ionian": [0, 2, 4, 5, 7, 9, 11]}[mode]
    return [root + 12 * o + s for o in range(-2, 4) for s in steps]

def melody(buf, start, bars, beat, chords, scale, lo, hi, voice, density=0.55, rest=0.25):
    """A stepwise melody over the chords: phrases of 2 bars, landing on chord tones."""
    cur = (lo + hi) // 2
    for b in range(bars):
        chord = chords[b % len(chords)]; tones = [c % 12 for c in chord]
        t0 = start + b * 4 * beat; pos = 0.0
        while pos < 4 - 1e-6:
            dur = r.choice([0.5, 1.0, 1.0, 1.5, 2.0]) if r.random() < density else 1.0
            dur = min(dur, 4 - pos)
            if r.random() < rest and pos > 0: pos += dur; continue
            cands = [n for n in scale if lo <= n <= hi and abs(n - cur) <= 4]
            strong = pos in (0.0, 2.0)
            pool = [n for n in cands if n % 12 in tones] if strong else cands
            if not pool: pool = cands or [cur]
            cur = int(r.choice(pool))
            place(buf, voice(cur, dur * beat * 0.98), t0 + pos * beat)
            pos += dur

def menu():
    bpm = 84; beat = 60 / bpm; bars = 24; L = bars * 4 * beat
    prog = [[50, 57, 62, 65], [48, 55, 60, 64], [46, 53, 58, 62], [48, 55, 60, 64],   # Dm  C  Bb  C
            [50, 57, 62, 65], [45, 52, 57, 60], [46, 53, 58, 62], [45, 52, 57, 61]]  # Dm  Am  Bb  A
    mix = {k: np.zeros(int(SR * L)) for k in ("pad", "harp", "bass", "mel")}
    for b in range(bars):
        ch = prog[b % len(prog)]; t0 = b * 4 * beat
        place(mix["pad"], pad([n + 12 for n in ch[1:]], 4 * beat, 1300), t0)
        place(mix["bass"], bass(ch[0] - 12, 4 * beat * 0.95), t0)
        arp = [ch[0] + 12, ch[1] + 12, ch[2] + 12, ch[3] + 12, ch[2] + 24, ch[3] + 12, ch[2] + 12, ch[1] + 12]
        for i, m in enumerate(arp): place(mix["harp"], pluck(midi(m), 1.6, 0.45, 0.9), t0 + i * beat / 2)
    sc = scale_notes(62, "dorian")
    melody(mix["mel"], 8 * 4 * beat, 16, beat, prog, sc, 69, 86, flute, density=0.6, rest=0.3)   # melody enters after 8 bars
    y = 0.55 * mix["pad"] + 0.5 * mix["harp"] + 0.45 * mix["bass"] + 0.42 * mix["mel"]
    return reverb(y, 2.4, 0.32, 3200, wrap=True)

def titan():
    bpm = 124; beat = 60 / bpm; bars = 36; L = bars * 4 * beat
    prog = [[48, 55, 60], [49, 56, 61], [48, 55, 60], [46, 53, 58]]   # Cm  Db  Cm  Bb (phrygian colour)
    mix = {k: np.zeros(int(SR * L)) for k in ("drum", "brass", "choir", "bass", "hat")}
    for b in range(bars):
        ch = prog[b % len(prog)]; t0 = b * 4 * beat; section = b // 8
        # taiko pattern: 1 . . 1 | . . 1 . with a roll every 4th bar
        for p in (0, 1.5, 3.0): place(mix["drum"], taiko(0.9, 58), t0 + p * beat)
        if b % 4 == 3:
            for k in range(4): place(mix["drum"], taiko(0.4, 82) * (0.5 + 0.15 * k), t0 + (3 + k * 0.25) * beat)
        place(mix["drum"], kick(), t0); place(mix["drum"], kick(), t0 + 2 * beat)
        if section >= 1:
            for p in (1, 3): place(mix["drum"], snare(), t0 + p * beat)
            for k in range(8): place(mix["hat"], hat(), t0 + k * beat / 2)
        # brass ostinato on the root (8ths), lifting a step on beat 4
        for k in range(8):
            m = ch[0] + (1 if (k == 6 and b % 2) else 0)
            place(mix["brass"], brass(m, beat / 2 * 0.9, 1400 + 400 * min(section, 2)), t0 + k * beat / 2)
        place(mix["bass"], bass(ch[0] - 12, 4 * beat * 0.95), t0)
        if section >= 2: place(mix["choir"], choir([n + 12 for n in ch], 4 * beat), t0)
    sc = scale_notes(60, "phrygian")
    big = np.zeros(int(SR * L)); melody(big, 16 * 4 * beat, 20, beat, prog, sc, 60, 75, lambda m, s: brass(m, s, 2400), density=0.75, rest=0.2)
    y = 0.6 * mix["drum"] + 0.32 * mix["brass"] + 0.4 * mix["choir"] + 0.5 * mix["bass"] + 0.25 * mix["hat"] + 0.38 * big
    return reverb(y, 2.0, 0.22, 3000, wrap=True)

def victory():
    beat = 60 / 110; L = 5.5; y = np.zeros(int(SR * L))
    seq = [([60, 64, 67], 0, 0.5), ([60, 64, 67], 0.5, 0.5), ([65, 69, 72], 1, 1), ([67, 71, 74], 2, 1), ([72, 76, 79], 3, 4)]
    for ch, at, d in seq:
        for m in ch: place(y, brass(m, d * beat * 0.95, 2600) * 0.6, at * beat)
        place(y, bass(ch[0] - 12, d * beat), at * beat)
    for i, m in enumerate([72, 76, 79, 84, 88, 91, 96]): place(y, pluck(midi(m), 2.5, 0.5, 1.5) * 0.5, 3 * beat + i * 0.06)
    place(y, taiko(1.0, 55), 3 * beat)
    return reverb(y, 2.0, 0.3)[:int(SR * 6.5)]

if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    tracks = {"music_menu": menu(), "music_titan": titan(), "music_victory": victory()}
    total = 0; out = {}
    for name, x in tracks.items():
        y = norm(x, -1.0, -18.0)
        if name == "music_victory": y = fade(y, 0.002, 0.8)
        out[name] = y; size = write_ogg(y, os.path.join(OUT, name + ".ogg"), q=2); total += size
        pk, rms = stats(y); print(f"{name:14s} {len(y) / SR:5.1f}s  peak {pk:5.1f} dB  rms {rms:5.1f} dB  {size / 1024:.0f} KB")
    print(f"music total {total / 1024:.0f} KB")
    if "--png" in sys.argv:
        import plot; plot.sheet(out, sys.argv[sys.argv.index("--png") + 1], "music", cols=1, w=1200, h=200)
