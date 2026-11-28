# The painted arena textures, in numpy: a mirror of Client/SceneryPaint.cs used by review.py to
# render the review sheets without the game. Keep the two in step (same hash, noise and formulas).
import numpy as np

np.seterr(over="ignore")


def hexc(h):
    h = h.lstrip("#"); return np.array([int(h[i:i + 2], 16) / 255 for i in (0, 2, 4)], dtype=np.float64)


def hash2(ix, iy, seed):
    h = (ix.astype(np.uint32) * np.uint32(374761393) + iy.astype(np.uint32) * np.uint32(668265263) + np.uint32(seed) * np.uint32(1442695041)).astype(np.uint32)
    h = ((h ^ (h >> np.uint32(13))) * np.uint32(1274126177)).astype(np.uint32)
    h = h ^ (h >> np.uint32(16))
    return (h & np.uint32(0xFFFFFF)).astype(np.float64) / 16777216.0


def vnoise(x, y, seed):
    ix, iy = np.floor(x), np.floor(y)
    fx, fy = x - ix, y - iy
    ix = ix.astype(np.int64); iy = iy.astype(np.int64)
    ix0, iy0, ix1, iy1 = ix & 0xFFFF, iy & 0xFFFF, (ix + 1) & 0xFFFF, (iy + 1) & 0xFFFF   # wrap after the +1: no seams at 0
    sx, sy = fx * fx * (3 - 2 * fx), fy * fy * (3 - 2 * fy)
    a, b = hash2(ix0, iy0, seed), hash2(ix1, iy0, seed)
    c, d = hash2(ix0, iy1, seed), hash2(ix1, iy1, seed)
    return (a + (b - a) * sx) + ((c + (d - c) * sx) - (a + (b - a) * sx)) * sy


def fbm(x, y, seed, octaves=5):
    s, amp, norm = 0.0, 0.5, 0.0
    for o in range(octaves):
        s = s + vnoise(x, y, seed + o * 17) * amp; norm += amp
        x, y, amp = (x * 0.8 - y * 0.6) * 2.03, (x * 0.6 + y * 0.8) * 2.03, amp * 0.5   # each octave turned
    return s / norm


def smooth(a, b, x):
    t = np.clip((x - a) / (b - a), 0, 1); return t * t * (3 - 2 * t)


def lerp(a, b, t):
    t = np.asarray(t)[..., None] if np.ndim(t) else t
    return a + (b - a) * t


# ---------------- prop ramp: (shade, row) -> painted colour
RAMP_W, ROWS = 64, 16


def ramp_row(base, shadow_tint, light_tint):
    t = np.arange(RAMP_W) / (RAMP_W - 1)
    t = t + (np.round(t * 4) / 4 - t) * 0.35                     # soft three-band painterly steps
    dark = base * 0.30 * 0.6 + shadow_tint * 0.4 * 0.30 + shadow_tint * 0.12
    lit = np.minimum(base * 1.30 + light_tint * 0.10, 1.0)
    lo = lerp(dark, base, smooth(0, 1, np.clip(t / 0.55, 0, 1)))
    hi = lerp(base, lit, np.clip((t - 0.55) / 0.45, 0, 1))
    return np.where((t < 0.55)[:, None], lo, hi)


def ramp(rows, shadow_tint, light_tint):
    """rows: list of 16 base colours (already albedo-scaled). Returns (64, 64, 3): 4 px per row."""
    img = np.zeros((ROWS * 4, RAMP_W, 3))
    for r, c in enumerate(rows):
        img[r * 4:(r + 1) * 4] = ramp_row(c, shadow_tint, light_tint)[None]
    return img


# ---------------- sky
def sky(top, horizon, sun, seed, w=384, h=160, cloudiness=0.5, storm=False):
    u = (np.arange(w) + 0.5) / w; v = (np.arange(h) + 0.5) / h
    U, V = np.meshgrid(u, v)                                      # V = 0 at the bottom row
    base = lerp(horizon, top, smooth(0.0, 0.85, V) ** 0.8)
    glow = np.exp(-(((U - 0.68) / 0.22) ** 2 + ((V - 0.12) / 0.2) ** 2))
    base = base + (sun * 0.9 - base) * (glow * 0.55)[..., None]
    WU = U * 7.0 + 0.7 * fbm(U * 3.0, V * 3.0, seed + 50, 3); WV = V * 3.2 + 0.35 * fbm(U * 3.0 + 5.0, V * 3.0, seed + 60, 3)
    d = fbm(WU, WV, seed)
    band = smooth(0.05, 0.35, V) * (1 - smooth(0.75, 1.0, V))
    th = 0.62 - cloudiness * 0.25
    mask = smooth(th, th + 0.18, d * (0.6 + 0.6 * band))
    up = fbm(WU, WV + 0.08, seed) - fbm(WU, WV - 0.08, seed)
    lit = np.clip(0.5 + up * 9.0, 0, 1)
    cdark = lerp(horizon, top, 0.55) * (0.55 if storm else 0.8)
    clit = lerp(horizon, sun, 0.6) * (0.75 if storm else 1.05)
    cloud = lerp(cdark, clit, lit)
    cloud = cloud + (horizon - cloud) * (1 - smooth(0.0, 0.4, V))[..., None] * 0.6  # haze toward the horizon
    out = lerp(base, cloud, mask * 0.92)
    return np.clip(out, 0, 1)


# ---------------- distant ridges: one strip per layer (0 = far)
def ridge_height(u, layer, style, seed):
    f = (3.0, 5.0, 8.0)[layer]
    n = fbm(u * f, np.full_like(u, layer * 3.7), seed + layer * 31, 4)
    if style == "peaks":
        r = 1 - np.abs(n * 2 - 1); hgt = 0.25 + 0.6 * r ** 1.6
    elif style == "mesas":
        q = np.floor(n * 4) / 4; hgt = 0.2 + 0.55 * (q + (n - q) * 0.15)
    elif style == "sea":
        hgt = 0.08 + 0.25 * n
    elif style == "dunes":
        hgt = 0.1 + 0.3 * n ** 1.3
    elif style == "volcano":
        c = np.exp(-((u - 0.5) / (0.12 + layer * 0.05)) ** 2); hgt = 0.2 + 0.25 * n + c * (0.75 - layer * 0.2)
        hgt = np.minimum(hgt, 0.9 - layer * 0.15)
    else:  # hills, forest, ruins
        hgt = 0.18 + 0.5 * n
    if style == "forest" and layer > 0:     # a crown line of trees on the nearer ridges
        b = fbm(u * (90 + layer * 50), np.full_like(u, 2.0 + layer), seed + 3, 3)
        hgt = hgt + 0.08 * np.sqrt(np.clip(1 - np.abs(b * 2 - 1) * 1.6, 0, 1))
    if style == "ruins" and layer == 1:     # a few broken towers
        k = vnoise(np.floor(u * 22), np.zeros_like(u), seed + 5)
        hgt = np.where(k > 0.86, hgt + 0.18 + 0.1 * vnoise(np.floor(u * 22), np.ones_like(u), seed), hgt)
    return hgt


def ridge(layer, style, sil, fog, sun, seed, w=512, h=96):
    u = (np.arange(w) + 0.5) / w; v = (np.arange(h) + 0.5) / h
    hgt = ridge_height(u, layer, style, seed)
    slope = np.gradient(hgt) * w
    U, V = np.meshgrid(u, v)
    H = hgt[None, :]
    alpha = np.clip((H - V) * h * 0.9, 0, 1)
    aerial = (0.72, 0.45, 0.2)[layer]
    col = lerp(sil, fog, aerial)
    rim = np.clip(-slope[None, :] * 0.08, 0, 0.25) * np.clip(1 - (H - V) * 6, 0, 1)   # sun-side rim on the slopes
    tex = fbm(U * 60, V * 12, seed + 9, 3)
    c = col[None, None, :] * (0.92 + 0.16 * tex)[..., None]
    c = c + (sun - c) * rim[..., None]
    c = c + (fog - c) * (1 - smooth(0.0, 0.55, V / np.maximum(H, 1e-3)))[..., None] * (0.55 - layer * 0.12)  # haze at the foot
    if style == "volcano":
        glow = np.exp(-((U - 0.5) / 0.04) ** 2) * smooth(0.0, 0.15, V - H + 0.15) * (layer == 0)
        c = c + (np.array([1.0, 0.45, 0.12]) - c) * glow[..., None]
    return np.concatenate([np.clip(c, 0, 1), alpha[..., None]], axis=2)


# ---------------- painted ground (mirror of the C# GroundTexture)
def ground(g1, g2, worn, rock, n=256, extent=45.0, seed=0, flag=False, wet=None, water_z=None, horizon=None,
           path=None, world=1.0, lane=1.0, lift=1.0, spots=None):
    """Mirror of SceneryPaint.Ground (path, world/lane tint, lift and spots as there)."""
    xs = np.arange(n); X, Y = np.meshgrid(xs, xs)
    wx = -(X / (n - 1) * 2 - 1) * extent; wz = -(Y / (n - 1) * 2 - 1) * extent
    big = fbm(wx * 0.06 + seed, wz * 0.06, 11, 4)
    mid = fbm(wx * 0.35, wz * 0.35 + seed, 23, 3)
    c = lerp(g1, g2, smooth(0.3, 0.7, big * 0.7 + mid * 0.3))
    # painted strokes: elongated noise in two directions
    stroke = fbm(wx * 2.2 + wz * 0.6, wz * 0.5 - wx * 0.2, 37, 2)
    c = c * (0.9 + 0.2 * stroke)[..., None]
    if spots is not None and spots[3] > 0:
        c = lerp(c, np.array(spots[:3]), smooth(0.62, 0.72, fbm(wx * 0.45 + seed * 0.37, wz * 0.45, 97, 3)) * spots[3])
    if path is not None and len(path) >= 2:
        pd = np.full_like(wx, 1e9)
        for (ax, az), (bx, bz) in zip(path[:-1], path[1:]):
            dx, dz = bx - ax, bz - az
            u = np.clip(((wx - ax) * dx + (wz - az) * dz) / max(1e-6, dx * dx + dz * dz), 0, 1)
            pd = np.minimum(pd, np.sqrt((wx - ax - dx * u) ** 2 + (wz - az - dz * u) ** 2))
        pd = pd + (fbm(wx * 0.7, wz * 0.7, 101, 2) - 0.5) * 0.7
        soil = lerp(worn, worn * 0.8, fbm(wx * 1.3, wz * 1.3, 103, 3))
        k = smooth(1.5, 0.9, pd)
        c = lerp(c, soil, k * 0.85)
        rut = np.exp(-((np.abs(pd) - 0.45) / 0.09) ** 2)
        c = c * (1 - 0.12 * rut * k)[..., None]
    e = np.sqrt((wx / 5.4) ** 2 + ((wz - 0.6) / 5.0) ** 2)
    edge_n = fbm(wx * 0.8, wz * 0.8, 41, 3)
    worn_k = smooth(1.42, 1.0, e + (edge_n - 0.5) * 0.35)
    soil = lerp(worn, worn * 0.78, fbm(wx * 1.6, wz * 1.6, 53, 3)) * lift
    c = lerp(c, soil, worn_k * 0.92)
    tint = world + (lane - world) * smooth(1.6, 1.0, e)
    rim = np.exp(-((e - 1.05 - (edge_n - 0.5) * 0.3) / 0.07) ** 2)      # darker trodden rim
    c = c * (1 - 0.18 * rim)[..., None]
    pebbles = (fbm(wx * 4.5, wz * 4.5, 61, 2) > 0.7) * worn_k
    c = lerp(c, rock, pebbles * 0.5)
    if flag:   # flagstones inside the worn patch (ruins)
        gx, gz = wx * 0.9 + 0.5 * (np.floor(wz * 0.9) % 2), wz * 0.9
        gap = np.minimum(np.abs(gx - np.round(gx)), np.abs(gz - np.round(gz)))
        tile = smooth(0.03, 0.07, gap) * (fbm(wx * 0.5, wz * 0.5, 71, 2) > 0.42)
        tone = 0.85 + 0.3 * vnoise(np.floor(gx), np.floor(gz), 73)
        c = lerp(c, lerp(rock * 0.6, rock * tone[..., None], tile), smooth(1.25, 0.6, e) * 0.85)
    if water_z is not None and wet is not None:
        c = lerp(c, wet, smooth(water_z - 3.0, water_z - 0.5, wz))
    c = c * ((0.94 + 0.12 * fbm(wx * 3, wz * 3, 83, 2)) * tint)[..., None]
    if horizon is not None:   # fade into the near ridge card (z 28, x +-30)
        hn = (fbm(wx * 0.3, wz * 0.3, 91, 2) - 0.5) * 3
        c = lerp(c, horizon, np.maximum(smooth(21, 27.5, wz + hn), smooth(23, 29.5, np.abs(wx) + hn)) * 0.75)
    return np.clip(c, 0, 1)


def water(deep, light, foam, fog, seed, w=512, h=96):
    u = (np.arange(w) + 0.5) / w; v = (np.arange(h) + 0.5) / h
    U, V = np.meshgrid(u, v)
    wx = -(U * 2 - 1) * 45; d = (1 - V) * 16
    c = lerp(light, deep, smooth(0, 6, d))
    c = lerp(c, light * 1.25, smooth(0.58, 0.74, fbm(wx * 0.18, d * 1.7, seed + 3, 4)) * 0.45)
    c = c * (0.93 + 0.14 * fbm(wx * 1.1, d * 3, seed + 7, 2))[..., None]
    edge = d + (fbm(wx * 0.6, np.full_like(wx, 0.5), seed + 11, 3) - 0.5) * 0.9
    c = lerp(c, foam, smooth(1.1, 0.25, edge) * 0.75)
    c = lerp(c, fog * 0.85, smooth(7, 16, d) * 0.55)
    return np.clip(c, 0, 1)   # row 0 = v 0 = the far edge


def lava(seed, w=512, h=96):
    u = (np.arange(w) + 0.5) / w; v = (np.arange(h) + 0.5) / h
    U, V = np.meshgrid(u, v)
    wx = -(U * 2 - 1) * 45; d = (1 - V) * 16; far = smooth(3, 16, d)
    crust, crust2, hot, warm = np.array([0.10, 0.075, 0.07]), np.array([0.2, 0.15, 0.13]), np.array([1, 0.42, 0.1]), np.array([0.45, 0.14, 0.05])
    wu = wx * 0.32 + 0.6 * fbm(wx * 0.1, d * 0.2, seed + 21, 2); wv = d * 0.9 + 0.5 * fbm(wx * 0.12 + 3, d * 0.2, seed + 23, 2)
    r = 1 - np.abs(fbm(wu, wv, seed + 25, 4) * 2 - 1)
    vein = smooth(0.935 - 0.17 * far, 0.99, r)
    c = lerp(crust, crust2, fbm(wx * 1.4, d * 1.4, seed + 27, 3)) * (0.85 + 0.3 * fbm(wx * 0.2, d * 0.4, seed + 29, 2))[..., None]
    c = lerp(c, warm, smooth(0.75, 0.95, r) * 0.5)
    c = lerp(c, hot, vein)
    pool = smooth(10, 16, d) * smooth(0.45, 0.75, fbm(wx * 0.08, d * 0.25, seed + 31, 3))
    c = lerp(c, hot, pool * 0.8)
    glow = np.clip(vein * (0.55 + 0.45 * far) + pool * 0.9 + far * 0.12, 0, 1)
    return np.clip(c, 0, 1), hot * glow[..., None]
