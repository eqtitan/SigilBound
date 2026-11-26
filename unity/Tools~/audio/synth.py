"""Tiny numpy synth used by make_sfx.py and make_music.py (no downloads, no paid tools).

Mono float32 at SR. Oscillators, envelopes, FFT-domain filters, an FFT convolution reverb,
loudness normalisation and OGG Vorbis export through ffmpeg.
"""
import os, subprocess, tempfile, wave
import numpy as np

SR = 32000
rng = np.random.default_rng(7)

def t(sec): return np.arange(int(SR * sec)) / SR

def env(n, a=0.005, d=0.1, s=0.0, r=0.05, sus=None):
    """ADSR over n samples: attack a, decay d to level s, hold, release r (seconds)."""
    A, D, R = int(a * SR), int(d * SR), int(r * SR)
    S = max(0, n - A - D - R)
    e = np.concatenate([np.linspace(0, 1, max(A, 1)), np.linspace(1, s, max(D, 1)), np.full(S, s), np.linspace(s, 0, max(R, 1))])
    return np.pad(e, (0, max(0, n - len(e))))[:n]

def expdecay(n, tau): return np.exp(-np.arange(n) / (SR * tau))

def sine(f, sec, phase=0.0):
    f = np.broadcast_to(np.asarray(f, dtype=np.float64), (int(SR * sec),))
    return np.sin(2 * np.pi * np.cumsum(f) / SR + phase)

def saw(f, sec, harm=24):
    """Band-limited-ish saw by additive harmonics (f may be a sweep array)."""
    n = int(SR * sec); f = np.broadcast_to(np.asarray(f, dtype=np.float64), (n,)); ph = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros(n)
    for k in range(1, harm + 1):
        out += np.where(f * k < SR / 2.2, np.sin(k * ph) / k, 0)
    return out * 0.6

def square(f, sec, harm=15):
    n = int(SR * sec); f = np.broadcast_to(np.asarray(f, dtype=np.float64), (n,)); ph = 2 * np.pi * np.cumsum(f) / SR
    out = np.zeros(n)
    for k in range(1, harm + 1, 2):
        out += np.where(f * k < SR / 2.2, np.sin(k * ph) / k, 0)
    return out * 0.8

def noise(sec): return rng.standard_normal(int(SR * sec))

def band(x, lo=0.0, hi=None, soft=1.15):
    """FFT band-pass with soft edges (lo/hi in Hz)."""
    n = len(x); X = np.fft.rfft(x); f = np.fft.rfftfreq(n, 1 / SR)
    hi = hi or SR / 2
    g = np.ones_like(f)
    if lo > 0: g *= 1 / (1 + (lo / np.maximum(f, 1)) ** (4 * soft))
    if hi < SR / 2: g *= 1 / (1 + (f / hi) ** (4 * soft))
    return np.fft.irfft(X * g, n)

def lowpass(x, hi): return band(x, 0, hi)
def highpass(x, lo): return band(x, lo, None)

def pluck(f, sec, bright=0.6, tau=0.6):
    """Harp/lute-like tone: harmonics with faster decay for higher partials."""
    n = int(SR * sec); tt = np.arange(n) / SR; out = np.zeros(n)
    for k in range(1, 12):
        if f * k > SR / 2.2: break
        out += (bright ** (k - 1)) * np.sin(2 * np.pi * f * k * tt) * np.exp(-tt * k / tau)
    return out * env(n, 0.002, 0.0, 1, 0.03)

def reverb(x, sec=1.6, mix=0.25, damp=3500, wrap=False):
    """Convolution with a decaying noise impulse (FFT). wrap=True folds the tail back to the start (seamless loops)."""
    if not wrap: x = fade(x, 0.0005, 0.06)   # no step where the dry sound ends (it would click through the tail)
    ir = lowpass(noise(sec), damp) * expdecay(int(SR * sec), sec / 5)
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-9
    n = len(x) + len(ir)
    wet = np.fft.irfft(np.fft.rfft(x, n) * np.fft.rfft(ir, n), n)
    if wrap:
        out = wet[:len(x)].copy(); tail = wet[len(x):]; out[:len(tail)] += tail[:len(x)]
        wet = out
    else:
        x = np.pad(x, (0, len(ir)))
    return (1 - mix) * x + mix * wet

def place(buf, sig, at):
    i = int(at * SR)
    if i >= len(buf): return
    j = min(len(buf), i + len(sig)); buf[i:j] += sig[:j - i]

def norm(x, peak_db=-1.0, rms_db=None):
    """Scale to an RMS target (if given) without letting the peak exceed peak_db; soft-clips the rest."""
    x = x - np.mean(x)
    if rms_db is not None:
        r = np.sqrt(np.mean(x ** 2)) + 1e-9; x = x * (10 ** (rms_db / 20) / r)
    pk = np.max(np.abs(x)) + 1e-9; lim = 10 ** (peak_db / 20)
    if pk > lim: x = lim * np.tanh(x / lim) if rms_db is not None else x * (lim / pk)   # soft-saturate, never past lim
    return x

def fade(x, a=0.003, r=0.02):
    n = len(x); A, R = min(n, int(a * SR)), min(n, int(r * SR))
    x = x.copy(); x[:A] *= np.linspace(0, 1, A); x[n - R:] *= np.linspace(1, 0, R); return x

def stats(x):
    return 20 * np.log10(np.max(np.abs(x)) + 1e-9), 20 * np.log10(np.sqrt(np.mean(x ** 2)) + 1e-9)

def write_ogg(x, path, q=3):
    """16-bit WAV in memory -> ffmpeg libvorbis (quality q)."""
    pcm = (np.clip(x, -1, 1) * 32767).astype(np.int16)
    with tempfile.NamedTemporaryFile(suffix=".wav", delete=False) as f: tmp = f.name
    with wave.open(tmp, "wb") as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(pcm.tobytes())
    subprocess.run(["ffmpeg", "-y", "-loglevel", "error", "-i", tmp, "-c:a", "libvorbis", "-q:a", str(q), path], check=True)
    os.remove(tmp)
    return os.path.getsize(path)

def midi(m): return 440.0 * 2 ** ((m - 69) / 12)
