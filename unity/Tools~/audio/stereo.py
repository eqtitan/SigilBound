"""Stereo sound-design toolkit for make_real.py: 44.1 kHz float stereo, CC0 recordings layered with
modal metal synthesis, granular rattles/debris/crackle, formant-filtered growls and generated stereo
reverbs. Everything is numpy + ffmpeg (no paid tools).

Stereo convention: arrays shaped (n, 2). Mono-safety: the dry layers sit near the centre or are panned
with equal-power law; width comes from decorrelated reverb tails and from independent L/R noise or
recording segments, which sum to mono without comb filtering.
"""
import os, subprocess
import numpy as np

SR = 44100
SRC = os.environ.get("AUDIO_SRC", os.path.expanduser("~/.cache/sigil-audio-src"))
rng = np.random.default_rng(1207)

def secs(n): return n / SR
def N(sec): return int(round(sec * SR))
def tt(n): return np.arange(n) / SR

# ---------- I/O ----------
_cache = {}
def load(rel, ch=1):
    """Decode a source file (relative to SRC) to float mono (n,) or stereo (n,2) at SR."""
    key = (rel, ch)
    if key not in _cache:
        path = rel if os.path.isabs(rel) else os.path.join(SRC, rel)
        raw = subprocess.run(["ffmpeg", "-v", "error", "-i", path, "-f", "f32le", "-ac", str(ch), "-ar", str(SR), "-"],
                             check=True, capture_output=True).stdout
        x = np.frombuffer(raw, dtype=np.float32).astype(np.float64)
        x = x.reshape(-1, 2) if ch == 2 else x
        # trim leading silence (sources often start with a few ms of nothing)
        a = np.abs(x if ch == 1 else x.sum(1)); thr = a.max() * 0.02 if a.max() > 0 else 0
        i = int(np.argmax(a > thr)) if thr > 0 else 0
        _cache[key] = x[max(0, i - N(0.002)):]
    return _cache[key].copy()

def write_ogg(x, path, q=2, sr=SR):
    x = np.asarray(x, dtype=np.float32)
    if x.ndim == 1: x = np.stack([x, x], 1)
    subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-",
                    "-ar", str(sr), "-c:a", "libvorbis", "-q:a", str(q), path], input=x.tobytes(), check=True)

# ---------- basic DSP ----------
def fftfilt(x, gain_fn):
    if x.ndim == 2: return np.stack([fftfilt(x[:, c], gain_fn) for c in range(x.shape[1])], 1)
    n = len(x); X = np.fft.rfft(x, n); f = np.fft.rfftfreq(n, 1 / SR)
    return np.fft.irfft(X * gain_fn(np.maximum(f, 1e-3)), n)

def band(x, lo=0.0, hi=None, order=2.0):
    hi = hi or SR / 2
    def g(f):
        r = np.ones_like(f)
        if lo > 0: r *= 1 / np.sqrt(1 + (lo / f) ** (2 * order))
        if hi < SR / 2: r *= 1 / np.sqrt(1 + (f / hi) ** (2 * order))
        return r
    return fftfilt(x, g)
def lp(x, hi, order=2.0): return band(x, 0, hi, order)
def hp(x, lo, order=2.0): return band(x, lo, None, order)

def peak_eq(x, f0, gain_db, q=1.0):
    g = 10 ** (gain_db / 20)
    return fftfilt(x, lambda f: 1 + (g - 1) * np.exp(-0.5 * (np.log2(f / f0) * q * 2.5) ** 2))

def resample(x, ratio):
    """Varispeed: ratio > 1 plays faster/higher (shorter)."""
    if ratio == 1: return x.copy()
    n = len(x); m = max(2, int(n / ratio)); src = np.arange(m) * ratio
    if x.ndim == 2: return np.stack([np.interp(src, np.arange(n), x[:, c]) for c in range(2)], 1)
    return np.interp(src, np.arange(n), x)

def fade(x, a=0.002, r=0.02):
    x = x.copy(); A, R = N(a), N(r)
    if A > 0: x[:A] *= (np.linspace(0, 1, A) if x.ndim == 1 else np.linspace(0, 1, A)[:, None])
    if R > 0 and R < len(x): x[-R:] *= (np.linspace(1, 0, R) ** 2 if x.ndim == 1 else (np.linspace(1, 0, R) ** 2)[:, None])
    return x

def onset(x, pre=0.012):
    """Start a recording just before its loudest moment, so a hit lands on the frame it is played."""
    a = np.abs(x if x.ndim == 1 else x.sum(1)); i = int(np.argmax(a))
    return fade(x[max(0, i - N(pre)):], 0.004, 0.01)

def trim(x, sec): return x[:N(sec)]
def expdec(n, tau): return np.exp(-np.arange(n) / (SR * tau))
def env_ar(n, a, r_tau):
    A = max(1, N(a)); e = np.ones(n); e[:A] = np.linspace(0, 1, A); e[A:] = np.exp(-np.arange(n - A) / (SR * r_tau)); return e

def noise(n, color=0.0, seed=None):
    """color 0 white, 1 pink, 2 brown (FFT shaped)."""
    g = np.random.default_rng(seed) if seed is not None else rng
    w = g.standard_normal(n)
    if color == 0: return w
    y = fftfilt(w, lambda f: (np.maximum(f, 20) / 1000.0) ** (-color / 2))
    return y / (np.std(y) + 1e-12)

def smooth_rand(n, rate_hz, seed=None):
    """Slow random control signal (0-mean, ~unit), bandlimited to rate_hz."""
    g = np.random.default_rng(seed) if seed is not None else rng
    k = max(4, int(n / SR * rate_hz) + 4)
    pts = g.standard_normal(k)
    return np.interp(np.linspace(0, k - 3, n), np.arange(k), pts)

# ---------- stereo ----------
def st(x, pan=0.0):
    """Mono -> stereo, equal-power pan (-1 left .. 1 right); pan may be an array (moving source)."""
    if x.ndim == 2: return x.copy()
    p = np.clip(np.asarray(pan, dtype=np.float64), -1, 1)
    if p.ndim == 1 and len(p) != len(x): p = np.interp(np.linspace(0, 1, len(x)), np.linspace(0, 1, len(p)), p)
    th = (p + 1) * np.pi / 4
    return np.stack([x * np.cos(th), x * np.sin(th)], 1) * np.sqrt(2)

def pan_sweep(n, a, b, curve=1.0): return a + (b - a) * np.linspace(0, 1, n) ** curve

def width(x, w):
    """Mid/side width (w=0 mono, 1 unchanged, >1 wider). Side gain only: mono sum unaffected."""
    m = (x[:, 0] + x[:, 1]) / 2; s = (x[:, 0] - x[:, 1]) / 2 * w
    return np.stack([m + s, m - s], 1)

def mix(length_sec, layers):
    """layers: list of (signal, start_sec, gain[, pan]); mono signals are panned, stereo kept."""
    out = np.zeros((N(length_sec), 2))
    for L in layers:
        sig, at, g = L[0], L[1], L[2]; pan = L[3] if len(L) > 3 else 0.0
        s = st(sig, pan) if sig.ndim == 1 else sig
        i = N(at)
        if i >= len(out): continue
        m = min(len(s), len(out) - i)
        out[i:i + m] += s[:m] * g
    return out

def to_mono(x): return x.mean(1) if x.ndim == 2 else x

# ---------- reverb ----------
def make_ir(sec=1.6, damp=4000, predelay=0.012, er=8, size=1.0, seed=0, lowmult=1.25):
    """Stereo IR: decorrelated L/R exponential tails, frequency-dependent decay (lows ring longer than
    highs), plus sparse early reflections."""
    g = np.random.default_rng(seed)
    n = N(sec * 1.15 + predelay + 0.05); t = tt(n)
    out = np.zeros((n, 2))
    tau = sec / 6.9
    for c in range(2):
        w = g.standard_normal(n)
        lo = lp(w, 500) * np.exp(-t / (tau * lowmult))
        mid = band(w, 500, damp) * np.exp(-t / tau)
        hi = hp(w, damp) * np.exp(-t / (tau * 0.35))
        tail = lo + mid + hi
        tail *= np.clip(t / 0.02, 0, 1)   # tail swells in after the early reflections
        out[:, c] = tail
    out /= np.sqrt((out ** 2).sum(0).mean())
    # early reflections (sparse, alternating sides)
    ner = np.zeros((n, 2))
    for k in range(er):
        d = predelay + g.uniform(0.004, 0.045) * size; i = N(d)
        if i < n:
            amp = 0.5 * (1 - k / er) * g.uniform(0.5, 1.0); side = k % 2
            ner[i, side] += amp; ner[min(n - 1, i + N(g.uniform(0.0003, 0.0012))), 1 - side] += amp * 0.6
    pd = N(predelay)
    out = np.concatenate([np.zeros((pd, 2)), out[:n - pd]], 0)
    return out * 0.6 + ner

def conv(x, h):
    n = len(x) + len(h) - 1; nf = 1 << (n - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(x, nf) * np.fft.rfft(h, nf), nf)[:n]

def verb(x, sec=1.4, mix_=0.25, damp=4000, predelay=0.012, seed=0, tail=True, lowmult=1.25):
    """Reverb: the mono sum feeds a decorrelated stereo IR (mono-safe), mixed under the dry signal.
    Output is longer than input by the tail when tail=True."""
    xs = st(x) if x.ndim == 1 else x
    h = make_ir(sec, damp, predelay, seed=seed, lowmult=lowmult)
    m = to_mono(xs)
    wet = np.stack([conv(m, h[:, 0]), conv(m, h[:, 1])], 1)
    wet *= np.sqrt(np.mean(m ** 2) + 1e-12) / (np.sqrt(np.mean(wet ** 2)) + 1e-12) * 1.0
    out = np.zeros((len(wet) if tail else len(xs), 2))
    out[:len(xs)] += xs * (1 - mix_ * 0.5)
    out += wet[:len(out)] * mix_
    return out

# ---------- synthesis ----------
def modal(partials, sec, beat=0.6, seed=None, strike=0.0008):
    """Struck-metal modes: [(freq, amp, tau)], each split into two slightly detuned components (the
    beating shimmer of real plate), with a short noisy strike transient."""
    g = np.random.default_rng(seed) if seed is not None else rng
    n = N(sec); t = tt(n); out = np.zeros(n)
    for f, a, tau in partials:
        if f >= SR / 2.1: continue
        d = beat * g.uniform(0.4, 1.4); ph = g.uniform(0, 2 * np.pi, 2)
        out += a * np.exp(-t / tau) * (np.sin(2 * np.pi * (f - d / 2) * t + ph[0]) + 0.8 * np.sin(2 * np.pi * (f + d / 2) * t + ph[1])) / 1.8
    k = N(strike) + 1
    out[:k * 8] += hp(g.standard_normal(k * 8), 2000) * np.exp(-np.arange(k * 8) / k) * 0.6
    return out

def plate_modes(f0, n=12, spread=1.0, tau=0.35, seed=None, bright=1.0):
    """Inharmonic mode set of a thin armour plate (ratios ~ 1, 1.6, 2.3, 2.9, 3.9... jittered)."""
    g = np.random.default_rng(seed) if seed is not None else rng
    base = [1.0, 1.59, 2.14, 2.30, 2.65, 2.92, 3.50, 3.91, 4.65, 5.40, 6.20, 7.10, 8.30, 9.6]
    out = []
    for i, r in enumerate(base[:n]):
        f = f0 * (1 + (r - 1) * spread) * g.uniform(0.97, 1.03)
        out.append((f, g.uniform(0.4, 1.0) * (1 / (1 + i * 0.35)) * (bright ** i), tau * g.uniform(0.6, 1.2) / (1 + i * 0.12)))
    return out

def grains(sources, count, span, t0=0.0, pitch=(0.9, 1.1), gain=(0.4, 1.0), pan=0.6, dur=None, density="decay", seed=None, lowpass=None):
    """Scatter short copies of recordings over `span` seconds (rattles, debris, jingles, crackle)."""
    g = np.random.default_rng(seed) if seed is not None else rng
    total = t0 + span + 1.0
    out = np.zeros((N(total), 2))
    for i in range(count):
        u = g.random()
        at = t0 + span * (u ** 2 if density == "decay" else u)
        s = sources[g.integers(len(sources))]
        s = resample(s, g.uniform(*pitch))
        if dur: s = fade(trim(s, dur * g.uniform(0.6, 1.2)), 0.0005, 0.01)
        if lowpass: s = lp(s, lowpass * g.uniform(0.6, 1.3))
        k = N(at); m = min(len(s), len(out) - k)
        a = g.uniform(*gain) * (1 - 0.6 * (at - t0) / max(span, 1e-3) if density == "decay" else 1)
        out[k:k + m] += st(s[:m], g.uniform(-pan, pan)) * a
    return out

def crackle(sec, rate=18, bright=4000, seed=None, pan=0.8, size=(0.0008, 0.004)):
    """Ember pops: Poisson-timed tiny filtered bursts with random pans."""
    g = np.random.default_rng(seed) if seed is not None else rng
    n = N(sec); out = np.zeros((n, 2))
    k = g.poisson(rate * sec)
    for _ in range(k):
        i = g.integers(0, n); L = N(g.uniform(*size)) + 4
        b = g.standard_normal(L * 6) * np.exp(-np.arange(L * 6) / L)
        b = band(b, bright * 0.25, bright * g.uniform(0.7, 1.6))
        m = min(len(b), n - i)
        out[i:i + m] += st(b[:m], g.uniform(-pan, pan)) * g.uniform(0.2, 1.0) ** 2
    return out

def fire_roar(sec, seed=None, body=600, hiss=3500):
    """Turbulent fire: brown-ish rumble + mid roar + hiss, each with its own slow turbulence; stereo from
    independent L/R noise."""
    n = N(sec); out = np.zeros((n, 2))
    for c in range(2):
        s = None if seed is None else seed * 7 + c
        a = lp(noise(n, 2, s), body) * (0.7 + 0.5 * smooth_rand(n, 6, s))
        b = band(noise(n, 1, None if s is None else s + 1), 250, 1800) * (0.6 + 0.6 * np.abs(smooth_rand(n, 11, None if s is None else s + 2)))
        h = band(noise(n, 0, None if s is None else s + 3), hiss * 0.6, hiss * 2.2) * (0.3 + 0.4 * np.abs(smooth_rand(n, 15, None if s is None else s + 4)))
        out[:, c] = a * 1.0 + b * 0.55 + h * 0.18
    return out / (np.abs(out).max() + 1e-9)

def bubbles(sec, rate=30, f=(300, 1400), seed=None, pan=0.7, tau=(0.012, 0.04)):
    """Water/lava bubbles: rising-pitch damped sines (the resonance of a forming bubble)."""
    g = np.random.default_rng(seed) if seed is not None else rng
    n = N(sec); out = np.zeros((n, 2))
    for _ in range(g.poisson(rate * sec)):
        i = g.integers(0, n); ta = g.uniform(*tau); L = N(ta * 5)
        f0 = g.uniform(*f); t = tt(L); fr = f0 * (1 + 2.5 * t / (ta * 5))
        b = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t / ta)
        m = min(L, n - i)
        out[i:i + m] += st(b[:m], g.uniform(-pan, pan)) * g.uniform(0.2, 1.0)
    return out

def formant_filter(x, tracks, bw=(90, 110, 160, 240), frame=2048, hop=512):
    """Time-varying vocal-tract filter: tracks = list of formant-frequency arrays (one per formant,
    each sampled per output frame or a callable of time fraction)."""
    n = len(x); win = np.hanning(frame); out = np.zeros(n + frame); norm = np.zeros(n + frame)
    xx = np.concatenate([x, np.zeros(frame)])
    freqs = np.fft.rfftfreq(frame, 1 / SR)
    nfr = (n // hop) + 1
    for k in range(nfr):
        i = k * hop; seg = xx[i:i + frame] * win
        u = k / max(1, nfr - 1)
        G = np.full_like(freqs, 0.02)
        for j, tr in enumerate(tracks):
            fc = tr(u) if callable(tr) else tr
            amp = 1.0 / (1 + j * 0.6)
            G += amp / (1 + ((freqs - fc) / (bw[min(j, len(bw) - 1)] * (1 + fc / 3000))) ** 2)
        y = np.fft.irfft(np.fft.rfft(seg) * G, frame) * win
        out[i:i + frame] += y; norm[i:i + frame] += win ** 2
    return out[:n] / np.maximum(norm[:n], 1e-3)

# ---------- loudness ----------
def short_rms(x, win=0.2):
    m = to_mono(x) if x.ndim == 2 else x
    w = max(1, N(win)); c = np.cumsum(np.concatenate([[0], m ** 2]))
    if len(m) <= w: return np.sqrt(np.mean(m ** 2))
    return np.sqrt(((c[w:] - c[:-w]) / w).max())

def limit(x, ceil=0.89, knee=0.6):
    """Soft-knee peak limiter (no hard clipping): above `knee` the curve bends smoothly toward `ceil`."""
    a = np.abs(x); s = np.sign(x); r = ceil - knee
    return np.where(a <= knee, x, s * (knee + r * np.tanh((a - knee) / r)))

def finish(x, loud_db=-14.0, win=0.2, ceil_db=-1.0, a=0.001, r=0.03):
    """Level a sound: its loudest `win` window to loud_db RMS, then the soft limiter to ceil_db peak."""
    x = st(x) if x.ndim == 1 else x
    x = x - x.mean(0)   # no DC
    g = 10 ** (loud_db / 20) / (short_rms(x, win) + 1e-12)
    y = limit(x * g, 10 ** (ceil_db / 20))
    # trim trailing near-silence
    e = np.abs(y).max(1); thr = 10 ** (-54 / 20); idx = np.nonzero(e > thr)[0]
    if len(idx): y = y[:idx[-1] + N(0.02)]
    return fade(y, a, r)

def stats(x):
    m = to_mono(x); s = x
    return dict(peak_db=float(round(20 * np.log10(np.abs(s).max() + 1e-12), 1)), loud_db=float(round(20 * np.log10(short_rms(x) + 1e-12), 1)),
                corr=round(float(np.corrcoef(x[:, 0], x[:, 1])[0, 1]) if x.ndim == 2 and np.std(x[:, 0]) > 0 and np.std(x[:, 1]) > 0 else 1.0, 2),
                mono_loss_db=float(round(20 * np.log10((np.sqrt(np.mean(m ** 2)) + 1e-12) / (np.sqrt(np.mean(x ** 2)) + 1e-12)), 1)),
                sec=round(len(x) / SR, 2))
