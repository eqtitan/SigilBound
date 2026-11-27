"""Music pass 2 (2026-11-27): stereo loops for the story campaign. Writes Assets/Resources/Audio:

- music_boss:  D minor, 100 bpm, 32 bars (76.8 s). War drums (taiko, toms, a gong), spiccato low
               strings, trombone chords, a horn theme, a choir pad; heaviest in the last 16 bars.
- music_act2:  E dorian, 12/8 at 100 dotted beats, 32 bars (76.8 s). Frame drum (bodhran) with
               tipper ghosts and shaker, a fiddle melody with a whistle answer, plucked triplet
               arpeggios, a low string drone, storm timpani swells and wind.
- music_map:   G lydian, 3/4 at 76 bpm, 24 bars (56.8 s). Harp waltz arpeggios, warm strings, a
               flute melody and a soft horn counter-line: the campaign map.

Everything is synthesized here (numpy, encoded with ffmpeg/libvorbis): no samples. Instruments are
placed across the stereo field and share a generated stereo hall. Each loop is an exact number of bars;
note and reverb tails past the end fold back onto the start, so it repeats without a seam.
Run: python3 make_music2.py [boss|act2|map ...]
"""
import os, sys
import numpy as np
from stereo import *

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")
def midi(m): return 440.0 * 2 ** ((m - 69) / 12)

# ---------------------------------------------------------------- instruments (mono)
def osc_saw(f, n, harm_max=14000, phase=0.0):
    """Band-limited saw; f scalar or per-sample array."""
    f = np.broadcast_to(np.asarray(f, dtype=np.float64), (n,)); ph = 2 * np.pi * np.cumsum(f) / SR + phase
    out = np.zeros(n); fmax = f.max()
    for k in range(1, int(harm_max / max(fmax, 20)) + 1):
        out += np.sin(k * ph) / k
    return out * 0.6

def vib(n, f, rate=5.4, depth=0.005, delay=0.25, seed=0):
    t = tt(n); g = np.random.default_rng(seed)
    return f * (1 + depth * np.sin(2 * np.pi * rate * t + g.uniform(0, 6)) * np.clip((t - delay * 0.5) / max(delay, 1e-3), 0, 1))

def adsr(n, a, d, s, r):
    A, D, R = max(1, N(a)), max(1, N(d)), max(1, N(r)); S = max(0, n - A - D - R)
    e = np.concatenate([np.linspace(0, 1, A), np.linspace(1, s, D), np.full(S, s), np.linspace(s, 0, R)])
    return np.pad(e, (0, max(0, n - len(e))))[:n]

def strings(m, sec, bright=2400, seed=0, attack=0.3):
    n = N(sec + 0.4); f = midi(m)
    x = sum(osc_saw(vib(n, f * (1 + d), 5.2 + j * 0.4, 0.004, 0.3, seed + j), n, 9000, seed + j) for j, d in enumerate((-0.005, 0, 0.005)))
    return lp(x / 3, bright) * adsr(n, attack, 0.3, 0.85, 0.4)

def spiccato(m, sec, bright=1700):
    n = N(sec + 0.15); f = midi(m)
    x = osc_saw(f, n, 7000) + 0.5 * osc_saw(f * 1.004, n, 7000)
    return lp(x, bright) * env_ar(n, 0.006, 0.09)

def brass(m, sec, bright=1600, seed=0, vibr=0.0):
    n = N(sec + 0.25); f = midi(m)
    fv = vib(n, f, 5.0, vibr, 0.35, seed) if vibr else f
    x = osc_saw(fv, n, 9000) + 0.5 * osc_saw(fv * 1.003 if np.ndim(fv) else f * 1.003, n, 9000)
    e = adsr(n, 0.07, 0.2, 0.8, 0.22)
    dark, light = lp(x, bright * 0.45), lp(x, bright)
    return (dark * (1 - e ** 2) + light * e ** 2) * e * 0.8      # the bell opens as the note swells

def bass_note(m, sec):
    n = N(sec + 0.1); f = midi(m)
    return (np.sin(2 * np.pi * f * tt(n)) + 0.35 * lp(osc_saw(f, n, 3000), 500)) * adsr(n, 0.02, 0.2, 0.75, 0.1)

def choir(chord, sec, seed=0, vowel=("a", "a")):
    n = N(sec + 0.6); src = np.zeros(n)
    for j, m in enumerate(chord):
        for d in (-0.006, 0.006):
            src += osc_saw(vib(n, midi(m) * (1 + d), 5.0 + 0.3 * j, 0.006, 0.4, seed + j), n, 6000)
    F = {"a": (700, 1150, 2500), "o": (450, 800, 2450)}
    a, b = F[vowel[0]], F[vowel[1]]
    tracks = [lambda u, i=i: a[i] + (b[i] - a[i]) * u for i in range(3)]
    y = formant_filter(src, tracks, bw=(90, 120, 220))
    return y / (np.abs(y).max() + 1e-9) * adsr(n, min(0.8, sec * 0.3), 0.3, 0.9, 0.6)

def pluck(m, sec, bright=0.5, tau=0.9):
    n = N(sec); t = tt(n); f = midi(m); x = np.zeros(n)
    for k in range(1, 16):
        if f * k > 12000: break
        x += np.sin(2 * np.pi * f * k * t * (1 + 0.0004 * k * k)) * (bright ** (k - 1)) * np.exp(-t / (tau / (1 + 0.45 * (k - 1))))
    x[:N(0.003)] += hp(noise(N(0.003), 0, m), 2000) * 0.3
    return x * 0.5

def flute(m, sec, seed=0):
    n = N(sec + 0.15); f = vib(n, midi(m), 5.0, 0.006, 0.3, seed)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) + 0.25 * np.sin(2 * ph) + 0.07 * np.sin(3 * ph)
    x += 0.05 * band(noise(n, 0, seed), midi(m) * 0.9, midi(m) * 4)
    return x * adsr(n, 0.05, 0.1, 0.8, 0.12)

def whistle(m, sec, seed=0):
    n = N(sec + 0.1); f = vib(n, midi(m), 6.0, 0.008, 0.2, seed)
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = np.sin(ph) + 0.12 * np.sin(2 * ph) + 0.08 * band(noise(n, 0, seed + 1), midi(m), midi(m) * 3)
    return x * adsr(n, 0.02, 0.08, 0.8, 0.06)

def fiddle(m, sec, seed=0):
    n = N(sec + 0.12); f = vib(n, midi(m), 5.8, 0.007, 0.18, seed)
    x = osc_saw(f, n, 9000)
    x = peak_eq(peak_eq(peak_eq(x, 290, 6, 1.5), 1000, 4, 1.2), 2800, 5, 1.0)
    x = lp(x, 6000) + 0.03 * band(noise(n, 0, seed), 2000, 7000)
    return x * adsr(n, 0.035, 0.1, 0.85, 0.08)

def taiko(f0=60, sec=1.0, seed=0):
    n = N(sec); t = tt(n); f = f0 * (1 + 0.7 * np.exp(-t / 0.035))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.25) + 0.25 * np.sin(2 * np.pi * np.cumsum(f * 1.52) / SR) * np.exp(-t / 0.1)
    return x + 0.5 * lp(noise(n, 0, seed), 1000) * np.exp(-t / 0.025)

def tom(f0=110, seed=0): return taiko(f0, 0.5, seed) * 0.8
def kick(seed=0):
    n = N(0.5); t = tt(n); f = 48 * (1 + 2.2 * np.exp(-t / 0.025))
    return np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.14) + 0.2 * lp(noise(n, 0, seed), 2000) * np.exp(-t / 0.005)
def snare(seed=0, soft=1.0):
    n = N(0.25); t = tt(n)
    return (band(noise(n, 0, seed), 900, 7000) * np.exp(-t / 0.05) * 0.8 + np.sin(2 * np.pi * 185 * t) * np.exp(-t / 0.035) * 0.4) * soft
def gong(sec=6.0, seed=0, f0=62):
    return modal([(f0 * r, a, tau) for r, a, tau in [(1, 0.5, 3), (1.52, 0.6, 2.5), (2.21, 0.5, 2.2), (2.86, 0.4, 2.0), (3.6, 0.35, 1.6), (4.7, 0.25, 1.2), (6.1, 0.15, 0.9)]], sec, beat=0.8, seed=seed) \
        * np.clip(tt(N(sec)) / 0.05, 0, 1)
def bodhran(low=True, seed=0, amp=1.0):
    n = N(0.35); t = tt(n); f0 = 92 if low else 150
    f = f0 * (1 + 0.4 * np.exp(-t / 0.02))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / (0.12 if low else 0.06))
    x += 0.35 * band(noise(n, 0, seed), 300, 3000) * np.exp(-t / 0.012)
    return x * amp
def shaker(seed=0, amp=1.0):
    n = N(0.09); return hp(noise(n, 0, seed), 5000) * np.sin(np.pi * np.linspace(0, 1, n)) ** 2 * amp * 0.5
def timp_roll(sec, f0=41.2, seed=0):
    n = N(sec); t = tt(n); x = np.zeros(n); g = np.random.default_rng(seed)
    for at in np.arange(0, sec - 0.1, 0.055):
        i = max(0, N(at + g.uniform(-0.006, 0.006))); L = N(0.6); m = min(L, n - i)
        tl = tt(L); h = np.sin(2 * np.pi * f0 * (1 + 0.15 * np.exp(-tl / 0.02)) * tl) * np.exp(-tl / 0.35) + 0.2 * lp(noise(L, 0, i), 600) * np.exp(-tl / 0.02)
        x[i:i + m] += h[:m] * g.uniform(0.6, 1.0)
    return x * np.linspace(0.15, 1, n) ** 1.5

# ---------------------------------------------------------------- mastering
BANDS = [(25, 120), (120, 500), (500, 2000), (2000, 6000), (6000, 16000)]
# Tonal balance of the composed CC0 battle theme (share of energy per band, dB): the new battle tracks
# are matched to it, so they carry on phone speakers the way it does instead of living in the sub-bass.
BATTLE_BALANCE = (-7, -3, -6, -13, -21)
def band_db(y):
    m = y.mean(1); X = np.abs(np.fft.rfft(m)) ** 2; f = np.fft.rfftfreq(len(m), 1 / SR)
    return np.array([10 * np.log10(X[(f >= a) & (f < b)].sum() / X.sum() + 1e-12) for a, b in BANDS])

def match_eq(y, target, max_db=12.0, iters=4):
    """A gentle matching EQ: per-band gains (smooth on a log-frequency axis, capped at +-max_db) that move
    the track's band balance toward `target`; plus a 30 Hz high-pass."""
    total = np.zeros(len(BANDS)); centres = np.log([np.sqrt(a * b) for a, b in BANDS])
    for _ in range(iters):
        cur = band_db(fftfilt(y, lambda f: 10 ** (np.interp(np.log(f), centres, total) / 20)))
        total = np.clip(total + 0.8 * (np.array(target) - cur), -max_db, max_db)
    g = lambda f: 10 ** (np.interp(np.log(f), centres, total) / 20) / np.sqrt(1 + (30 / f) ** 4)
    return fftfilt(y, g), total

# ---------------------------------------------------------------- arrangement helpers
class Song:
    def __init__(self, sec): self.L = sec; self.n = N(sec); self.buf = np.zeros((N(sec + 8), 2))
    def put(self, sig, at, gain=1.0, pan=0.0):
        s = st(sig, pan) if sig.ndim == 1 else sig
        i = N(at); m = min(len(s), len(self.buf) - i); self.buf[i:i + m] += s[:m] * gain
    def render(self, rv_sec=2.4, rv_mix=0.24, damp=3200, loud=-14.5, ceil=-1.0, balance=BATTLE_BALANCE):
        x = verb(self.buf, rv_sec, rv_mix, damp, predelay=0.02, seed=7, lowmult=1.2)
        y = x[:self.n].copy(); k = 1                     # fold everything past the loop end back onto the start
        while k * self.n < len(x):
            seg = x[k * self.n:(k + 1) * self.n]; y[:len(seg)] += seg; k += 1
        y -= y.mean(0)
        y, eq = match_eq(y, balance)
        print("   match EQ (dB per band, sub..air):", " ".join(f"{v:+.1f}" for v in eq), " result:", " ".join(f"{v:.0f}" for v in band_db(y)))
        y *= 10 ** (loud / 20) / (np.sqrt(np.mean(y ** 2)) + 1e-12)
        return limit(y, 10 ** (ceil / 20), 0.5)

def chord_tones(name):
    roots = {"C": 48, "C#": 49, "Db": 49, "D": 50, "Eb": 51, "E": 52, "F": 53, "F#": 54, "G": 55, "Ab": 56, "A": 57, "Bb": 58, "B": 59}
    minor = name.endswith("m"); r = roots[name[:-1] if minor else name]
    return [r, r + (3 if minor else 4), r + 7]

# ---------------------------------------------------------------- boss
def boss():
    bpm = 100; beat = 60 / bpm; bar = 4 * beat; bars = 32
    s = Song(bars * bar)
    prog = (["Dm", "Dm", "Bb", "Bb", "Gm", "Gm", "A", "A"] * 2 + ["Dm", "Eb", "Dm", "Eb", "Bb", "Gm", "A", "A"] + ["Dm", "Bb", "Gm", "A", "Dm", "Eb", "A", "A"])
    for b in range(bars):
        t0 = b * bar; ch = chord_tones(prog[b]); sec = b // 8
        r = ch[0] - 12 if ch[0] - 12 >= 43 else ch[0]     # strings' root around D3
        # spiccato low strings: driving 8ths, root/root/fifth/root, octave jumps in the louder sections
        pat = [r, r, r + 7, r, r, r + 12 if sec >= 2 else r, r + 7, r]
        for k, m in enumerate(pat): s.put(spiccato(m - 12, beat / 2, 1400 + 300 * sec), t0 + k * beat / 2, 0.30, -0.35)
        s.put(bass_note(ch[0] - 24, bar * 0.98), t0, 0.3)
        # trombone chords (low brass) in the first and last sections, swelling on each bar
        if sec in (0, 2, 3):
            for j, m in enumerate([ch[0] - 12, ch[2] - 12, ch[1]]): s.put(brass(m, bar * 0.95, 1100 + 200 * sec, seed=b * 3 + j), t0, 0.11, 0.3 + 0.1 * j)
        # low string pad
        s.put(strings(ch[0] - 12, bar, 1500, seed=b) + strings(ch[2] - 12, bar, 1500, seed=b + 50), t0, 0.08, 0.0)
        # choir from bar 17
        if sec >= 2:
            s.put(st(choir([ch[0], ch[1] + 12, ch[2]], bar, seed=b, vowel=("o", "a") if b % 2 == 0 else ("a", "a")), 0) * 1.0, t0, 0.13)
        # war drums
        for p, f0, g in [(0, 55, 1.0), (1.5, 62, 0.6), (2.0, 55, 0.85), (3.0, 70, 0.6)] + ([(2.5, 48, 0.8), (3.5, 82, 0.5)] if sec >= 1 else []):
            s.put(taiko(f0, 1.0, seed=b * 10 + int(p * 2)), t0 + p * beat, 0.42 * g, (-0.4 if f0 > 60 else 0.2))
        s.put(kick(b), t0, 0.22); s.put(kick(b + 99), t0 + 2 * beat, 0.18)
        if sec >= 2:
            for k in range(16):
                if k % 4 == 2 or (k % 2 == 1 and b % 2): s.put(snare(b * 16 + k, 0.5 if k % 4 else 0.8), t0 + k * beat / 4, 0.14, 0.25)
        if b % 8 == 7:   # a fill into the next section (and from bar 32 back into bar 1)
            for k in range(8): s.put(tom(140 - 8 * k, seed=b * 8 + k), t0 + 2 * beat + k * beat / 4, 0.18 + 0.03 * k, -0.5 + 0.13 * k)
        if b in (0, 16, 24): s.put(gong(6, seed=b), t0, 0.22, 0.15)
    # horn theme (bars 9-16), trumpets-and-horns climax (bars 25-32)
    A = [(62, 0, 3), (64, 3, 1), (65, 4, 2), (67, 6, 1), (69, 7, 1), (70, 8, 3), (69, 11, 1), (67, 12, 4), (65, 16, 2), (64, 18, 1), (62, 19, 1),
         (64, 20, 4), (61, 24, 2), (62, 26, 1), (64, 27, 1), (57, 28, 4)]
    B = [(69, 0, 3), (70, 3, 1), (69, 4, 2), (67, 6, 1), (65, 7, 1), (67, 8, 2), (69, 10, 2), (74, 12, 4), (72, 16, 2), (70, 18, 2), (69, 20, 2),
         (67, 22, 2), (69, 24, 6)]
    for (m, p, d) in A:
        s.put(brass(m, d * beat * 0.97, 2000, seed=m + p, vibr=0.004), 8 * bar + p * beat, 0.30, -0.15)
        s.put(brass(m - 12, d * beat * 0.97, 1500, seed=m + p + 1), 8 * bar + p * beat, 0.12, 0.2)
    for (m, p, d) in B:
        s.put(brass(m, d * beat * 0.97, 2600, seed=m + p + 7, vibr=0.005), 24 * bar + p * beat, 0.30, -0.1)
        s.put(brass(m - 12, d * beat * 0.97, 1800, seed=m + p + 8, vibr=0.003), 24 * bar + p * beat, 0.2, 0.15)
        s.put(strings(m + 12, d * beat, 3000, seed=m + p, attack=0.08), 24 * bar + p * beat, 0.06, 0.35)
    # the third section answers the horns with the choir's line an octave down in strings
    for (m, p, d) in A: s.put(strings(m - 12, d * beat, 2200, seed=p, attack=0.1), 16 * bar + p * beat, 0.09, 0.3)
    return s.render(2.6, 0.26, 3000, loud=-14.5)

# ---------------------------------------------------------------- act 2
def act2():
    dot = 0.6; bar = 4 * dot; e8 = dot / 3; bars = 32
    s = Song(bars * bar)
    prog = ["Em", "D", "Em", "Em", "G", "Em", "A", "Em"]
    progB = ["C", "D", "Em", "Em", "C", "D", "A", "A"]
    for b in range(bars):
        t0 = b * bar; sec = b // 8; name = (progB if sec == 2 else prog)[b % 8]; ch = chord_tones(name)
        # drone and pad
        s.put(strings(40, bar, 900, seed=b) + strings(47, bar, 900, seed=b + 7), t0, 0.06, 0.0)
        s.put(strings(ch[1], bar, 1800, seed=b + 20) + strings(ch[2], bar, 1800, seed=b + 30), t0, 0.05, 0.2)
        s.put(bass_note(ch[0] - 12, 2 * dot * 0.95), t0, 0.32); s.put(bass_note(ch[0] - 12, 2 * dot * 0.95), t0 + 2 * dot, 0.25)
        # plucked triplet arpeggios (bouzouki-like), right of centre
        arp = [ch[0], ch[1], ch[2], ch[0] + 12, ch[2], ch[1]] * 2
        for k, m in enumerate(arp): s.put(pluck(m, 0.9, 0.55, 0.7), t0 + k * e8, 0.20 * (1.0 if k % 3 == 0 else 0.7), 0.45)
        # frame drum: low strokes on 1 and 4 of each half, tipper ghosts on every eighth, shaker brushes
        for k in range(12):
            low = k in (0, 6) or (sec >= 1 and k == 9)
            acc = 1.0 if k % 3 == 0 else 0.35
            s.put(bodhran(low, seed=b * 12 + k, amp=acc), t0 + k * e8, 0.38, -0.2)
            s.put(shaker(b * 31 + k, 0.6 if k % 3 == 0 else 0.35), t0 + k * e8 + 0.01, 0.12, 0.5)
        if b % 4 == 3:   # a roll at the end of each phrase
            for k in range(6): s.put(bodhran(False, seed=999 + b * 6 + k, amp=0.5 + 0.1 * k), t0 + (3 * dot) + k * e8 / 2, 0.3, -0.3)
        if b % 8 == 6: s.put(timp_roll(2 * bar, seed=b), t0, 0.25, 0.0)            # storm swells into each new section
        if b % 8 == 0 and sec >= 1:
            s.put(kick(b) * 1.0, t0, 0.4)
        if sec == 3:
            for k in (0, 6): s.put(kick(b * 2 + k), t0 + k * e8, 0.25)
    # wind across the whole loop (independent L/R, gusting)
    n = s.n; wind = np.stack([band(noise(n, 1, 77 + c), 250, 1400) * (0.5 + 0.5 * np.tanh(1.5 * smooth_rand(n, 0.12, 78 + c))) for c in range(2)], 1)
    s.put(wind / (np.abs(wind).max() + 1e-9), 0, 0.05)
    M = [(76, 3), (71, 3), (71, 2), (69, 1), (71, 3), (74, 3), (69, 3), (69, 2), (67, 1), (69, 3),
         (76, 3), (71, 2), (73, 1), (74, 3), (71, 3), (69, 2), (67, 1), (66, 3), (64, 6),
         (67, 3), (69, 3), (71, 3), (74, 3), (76, 2), (78, 1), (76, 3), (74, 3), (71, 3),
         (73, 3), (69, 3), (71, 2), (73, 1), (74, 3), (71, 2), (69, 1), (67, 3), (64, 6)]
    def play(voice, start_bar, shift, gain, pan):
        t = start_bar * bar
        for m, d in M:
            s.put(voice(m + shift, d * e8 * 0.95, seed=int(t * 10) % 997), t, gain, pan); t += d * e8
    play(fiddle, 8, 0, 0.16, -0.25)                 # bars 9-16: the fiddle
    play(whistle, 16, 12, 0.07, 0.35)               # bars 17-24: the whistle answers an octave up
    play(fiddle, 16, 0, 0.09, -0.3)
    play(fiddle, 24, 0, 0.16, -0.25); play(fiddle, 24, 12, 0.07, 0.25); play(whistle, 24, 12, 0.05, 0.4)   # bars 25-32: all in
    return s.render(2.2, 0.22, 4000, loud=-14.5)

# ---------------------------------------------------------------- campaign map
def mapt():
    bpm = 76; beat = 60 / bpm; bar = 3 * beat; bars = 24
    s = Song(bars * bar)
    prog = ["G", "A", "G", "A", "Em", "C", "D", "D"]
    for b in range(bars):
        t0 = b * bar; ch = chord_tones(prog[b % 8]); sec = b // 8
        s.put(bass_note(ch[0] - 12, bar * 0.95), t0, 0.28)
        for j, m in enumerate(ch): s.put(strings(m, bar, 1500, seed=b * 3 + j, attack=0.5), t0, 0.06, -0.3 + 0.3 * j)
        arp = [ch[0], ch[1] + 12, ch[2] + 12, ch[0] + 24, ch[2] + 12, ch[1] + 12]
        for k, m in enumerate(arp): s.put(pluck(m, 1.8, 0.4, 1.2), t0 + k * beat / 2, 0.16, 0.35 if k % 2 else 0.15)
    mel = [(74, 0, 2), (76, 2, 1), (78, 3, 3), (76, 6, 1.5), (74, 7.5, 1.5), (71, 9, 3), (73, 12, 2), (74, 14, 1), (76, 15, 3),
           (71, 18, 3), (72, 21, 2), (71, 23, 1), (69, 24, 6)]
    for start in (8 * bar, 16 * bar):
        for m, p, d in mel:
            s.put(flute(m, d * beat * 0.95, seed=int(m + p)), start + p * beat, 0.16, -0.2)
    for m, p, d in mel:   # the horn counter-line in the last section, a sixth below
        s.put(brass(m - 9, d * beat * 0.95, 1200, seed=int(p), vibr=0.003), 16 * bar + p * beat, 0.07, 0.3)
    return s.render(2.8, 0.3, 3500, loud=-20.5, balance=(-7, -3, -5, -16, -27))   # softer top than the battles

if __name__ == "__main__":
    want = sys.argv[1:] or ["boss", "act2", "map"]
    fns = {"boss": boss, "act2": act2, "map": mapt}
    for k in want:
        y = fns[k]()
        path = os.path.join(OUT, f"music_{k}.ogg")
        write_ogg(y, path, q=1 if k != "map" else 0)
        sz = os.path.getsize(path)
        print(f"music_{k:5s} {len(y) / SR:5.1f}s  rms {20 * np.log10(np.sqrt(np.mean(y ** 2))):5.1f} dB  peak {20 * np.log10(np.abs(y).max()):5.1f} dB"
              f"  corr {np.corrcoef(y[:, 0], y[:, 1])[0, 1]:.2f}  {sz / 1024:.0f} KB")
