"""Stereo contact sheet for make_real.py output: per sound, the left (amber) and right (cyan) waveforms,
a -1 dBFS line, and a log-frequency spectrogram (40 Hz - 16 kHz) of the mid signal.
Run: python3 plot_real.py OUT.png [prefix]   (reads the OGGs in Assets/Resources/Audio)"""
import os, sys, glob
import numpy as np
from PIL import Image, ImageDraw
from stereo import load, SR

AUD = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "Assets", "Resources", "Audio")

def logspec(x, w, h, nfft=2048):
    hop = max(32, (len(x) - nfft) // w + 1) if len(x) > nfft else 32
    win = np.hanning(nfft); xx = np.concatenate([x, np.zeros(nfft)])
    F = np.array([np.abs(np.fft.rfft(xx[i:i + nfft] * win)) for i in range(0, max(1, len(x)), hop)][:w]).T
    f = np.fft.rfftfreq(nfft, 1 / SR); edges = np.geomspace(40, 16000, h + 1)
    rows = [F[(f >= edges[k]) & (f < edges[k + 1])].mean(0) if np.any((f >= edges[k]) & (f < edges[k + 1])) else F[np.argmin(np.abs(f - edges[k]))] for k in range(h)]
    S = 20 * np.log10(np.array(rows) + 1e-7); S = np.clip((S - S.max() + 75) / 75, 0, 1)[::-1]
    rgb = np.stack([S ** 0.8 * 255, S ** 1.6 * 200, (1 - S) * S * 4 * 180], -1).astype(np.uint8)
    return Image.fromarray(rgb).resize((w, h))

def sheet(files, path, cols=4, w=320, h=170):
    rows = (len(files) + cols - 1) // cols
    out = Image.new("RGB", (cols * w, rows * h), (12, 10, 16)); d = ImageDraw.Draw(out)
    for i, fn in enumerate(files):
        x = load(fn, 2); ox, oy = (i % cols) * w, (i // cols) * h
        out.paste(logspec(x.mean(1), w, h // 2), (ox, oy + h // 2))
        mid = oy + h // 4; amp = h / 4 - 4
        step = max(1, len(x) // w)
        for k in range(min(w, len(x) // step)):
            seg = x[k * step:(k + 1) * step]
            d.line((ox + k, mid, ox + k, mid - np.abs(seg[:, 0]).max() * amp), fill=(226, 177, 78))
            d.line((ox + k, mid, ox + k, mid + np.abs(seg[:, 1]).max() * amp), fill=(90, 200, 230))
        y1 = int(0.891 * amp); d.line((ox, mid - y1, ox + w, mid - y1), fill=(200, 60, 60)); d.line((ox, mid + y1, ox + w, mid + y1), fill=(200, 60, 60))
        d.text((ox + 4, oy + 2), f"{os.path.basename(fn)[:-4]}  {len(x) / SR:.1f}s", fill=(255, 255, 255))
        d.rectangle((ox, oy, ox + w - 1, oy + h - 1), outline=(40, 36, 50))
    out.save(path)

if __name__ == "__main__":
    pre = sys.argv[2] if len(sys.argv) > 2 else "sfx_"
    files = sorted(f for f in glob.glob(os.path.join(AUD, pre + "*.ogg")) if not f[:-4].split("_")[-1].isdigit())
    sheet(files, sys.argv[1])
    print(len(files), "sounds ->", sys.argv[1])
