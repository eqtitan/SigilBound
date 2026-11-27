"""Seamless loop of the CC0 "Battle Theme A" (cynicmusic) for the Unity client: the shipped file faded
out at its end, so every repeat had a gap. Writes Assets/Resources/Audio/music_battle.ogg.

Source: public/game/audio/battle_theme.mp3 (the web build's copy, identical to the one the client used).
Analysis (2026-11-27): steady 73.0 bpm (beat 0.8220 s, beat grid phase 0.518 s, downbeats on the
strongest of the four beat phases). A band-energy self-similarity search over bar-aligned pairs before
the fade found the best long repeat at 24 bars (96 beats): it starts on the downbeat at 3.80 s (just
after the short intro) and ends at 82.71 s (before the fade at ~93 s). The cut is placed 6 ms before the
downbeat attack; what follows the end point is folded onto the loop start with a 120 ms equal-power
crossfade, the same fold the generated loops use. Loudness is matched to the original's body.
Run: python3 loop_battle.py
"""
import os, subprocess
import numpy as np

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "..", "..", "..", "public", "game", "audio", "battle_theme.mp3")
OUT = os.path.join(HERE, "..", "..", "Assets", "Resources", "Audio", "music_battle.ogg")
SR = 44100
BEAT, PHASE, BARS = 0.82200, 0.5179, 24
START = PHASE + 4 * BEAT - 0.006          # the downbeat of bar 2, just ahead of its attack
END = START + BARS * 4 * BEAT
FOLD = 0.12

raw = subprocess.run(["ffmpeg", "-v", "error", "-i", SRC, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"], check=True, capture_output=True).stdout
x = np.frombuffer(raw, np.float32).astype(np.float64).reshape(-1, 2)
iS, iE, xf = int(round(START * SR)), int(round(END * SR)), int(FOLD * SR)
loop = x[iS:iE].copy()
w = np.linspace(0, 1, xf)[:, None]
loop[:xf] = loop[:xf] * np.sin(w * np.pi / 2) + x[iE:iE + xf] * np.cos(w * np.pi / 2)
rms = lambda a: np.sqrt(np.mean(a ** 2))
body = x[int(1 * SR):int(92 * SR)]
loop *= rms(body) / rms(loop)
loop = np.clip(loop, -0.97, 0.97)  # the source peaks at -0.3 dBFS; nothing here should reach it
subprocess.run(["ffmpeg", "-v", "error", "-y", "-f", "f32le", "-ac", "2", "-ar", str(SR), "-i", "-", "-c:a", "libvorbis", "-q:a", "3", OUT],
               input=loop.astype(np.float32).tobytes(), check=True)
print(f"music_battle: {START:.4f}-{END:.4f} s ({END - START:.3f} s, {BARS} bars), rms {20 * np.log10(rms(loop)):.2f} dBFS "
      f"(original {20 * np.log10(rms(body)):.2f}), {os.path.getsize(OUT) / 1024:.0f} KB")
