"""Waveform + spectrogram contact sheet (PIL only) so levels and character can be checked by eye."""
import numpy as np
from PIL import Image, ImageDraw
from synth import SR

def spectro(x, w, h):
    nfft, hop = 512, max(64, len(x) // w)
    frames = [np.abs(np.fft.rfft(x[i:i + nfft] * np.hanning(nfft))) for i in range(0, max(1, len(x) - nfft), hop)][:w]
    if not frames: frames = [np.zeros(nfft // 2 + 1)]
    S = 20 * np.log10(np.array(frames).T + 1e-6); S = np.clip((S + 60) / 70, 0, 1)
    S = S[::-1]; img = Image.fromarray((S * 255).astype(np.uint8)).resize((w, h))
    return img.convert("RGB")

def sheet(sounds, path, tag, cols=4, w=300, h=150):
    names = list(sounds); rows = (len(names) + cols - 1) // cols
    out = Image.new("RGB", (cols * w, rows * h), (12, 10, 16)); d = ImageDraw.Draw(out)
    for i, n in enumerate(names):
        x = sounds[n]; ox, oy = (i % cols) * w, (i // cols) * h
        sp = spectro(x, w, h // 2); out.paste(sp, (ox, oy + h // 2))
        step = max(1, len(x) // w); mx = [np.max(np.abs(x[j:j + step])) for j in range(0, len(x), step)][:w]
        for k, v in enumerate(mx): d.line((ox + k, oy + h // 4 - v * h / 4, ox + k, oy + h // 4 + v * h / 4), fill=(226, 177, 78))
        d.line((ox, oy + h // 4 - int(0.891 * h / 4), ox + w, oy + h // 4 - int(0.891 * h / 4)), fill=(200, 60, 60))   # -1 dBFS
        d.text((ox + 4, oy + 2), f"{n} ({len(x) / SR:.1f}s)", fill=(255, 255, 255))
    out.save(path)
