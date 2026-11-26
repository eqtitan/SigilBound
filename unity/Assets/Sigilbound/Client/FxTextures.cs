// Procedural VFX textures for SkillFx: pure math (no UnityEngine), so the same code can be rendered
// offline to PNG for review. Every generator returns RGBA floats, n*n*4, rows from the bottom (the
// order Texture2D.SetPixels expects). u,v run -1..1 across the texture (or across one flipbook cell).
// Flipbook sheets follow Unity's frame order: frame 0 top-left, then right, then down.
using System;

namespace Sigilbound.Client
{
    public static class FxTextures
    {
        public delegate float Alpha(float u, float v);
        public delegate float Frame(float u, float v, float t);
        public delegate void Rgba(float u, float v, out float r, out float g, out float b, out float a);

        // ---------- noise ----------
        static float C01(float x) => x < 0 ? 0 : x > 1 ? 1 : x;
        static float Sm(float a, float b, float x) { float t = C01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static float Band(float x, float c, float w) => C01(1 - MathF.Abs(x - c) / w);
        static float Len(float u, float v) => MathF.Sqrt(u * u + v * v);
        public static float Hash(int x, int y, int s)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + s * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xffffff) / 16777215f;
            }
        }
        public static float Noise(float x, float y, int s)
        {
            int ix = (int)MathF.Floor(x), iy = (int)MathF.Floor(y);
            float fx = x - ix, fy = y - iy;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix, iy, s), b = Hash(ix + 1, iy, s), c = Hash(ix, iy + 1, s), d = Hash(ix + 1, iy + 1, s);
            return a + (b - a) * fx + (c - a) * fy + (a - b - c + d) * fx * fy;
        }
        public static float Fbm(float x, float y, int s, int oct = 4)
        {
            float sum = 0, amp = 0.5f, norm = 0;
            for (int i = 0; i < oct; i++) { sum += Noise(x, y, s + i * 31) * amp; norm += amp; x = x * 2.03f + 1.7f; y = y * 2.03f - 0.9f; amp *= 0.5f; }
            return sum / norm;
        }
        // Ridged noise: thin bright lines where the noise crosses 0.5 (caustics, lava veins).
        static float Ridge(float x, float y, int s, int oct = 3) => 1 - MathF.Abs(Fbm(x, y, s, oct) * 2 - 1);
        // Noise around a circle (seamless in angle): for ragged ring edges.
        static float Around(float ang, float freq, int s) => Fbm(MathF.Cos(ang) * freq + 5, MathF.Sin(ang) * freq + 5, s, 3);

        // ---------- builders ----------
        public static float[] Mono(int n, Alpha f)
        {
            var px = new float[n * n * 4];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = (y * n + x) * 4;
                px[i] = px[i + 1] = px[i + 2] = 1;
                px[i + 3] = C01(f(x / (n - 1f) * 2 - 1, y / (n - 1f) * 2 - 1));
            }
            return px;
        }
        public static float[] Color(int n, Rgba f)
        {
            var px = new float[n * n * 4];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                int i = (y * n + x) * 4;
                f(x / (n - 1f) * 2 - 1, y / (n - 1f) * 2 - 1, out px[i], out px[i + 1], out px[i + 2], out px[i + 3]);
                for (int k = 0; k < 4; k++) px[i + k] = C01(px[i + k]);
            }
            return px;
        }
        // grid x grid flipbook of `cell`-pixel frames; f gets the frame's normalised time t in 0..1.
        // Frames are faded to zero at their borders so bilinear filtering never bleeds across cells.
        public static float[] Sheet(int grid, int cell, Frame f, bool shade = false)
        {
            int n = grid * cell; var px = new float[n * n * 4];
            for (int k = 0; k < grid * grid; k++)
            {
                int cx = k % grid, cy = grid - 1 - k / grid; float t = k / (grid * grid - 1f);
                for (int y = 0; y < cell; y++) for (int x = 0; x < cell; x++)
                {
                    float u = x / (cell - 1f) * 2 - 1, v = y / (cell - 1f) * 2 - 1;
                    float edge = Sm(1f, 0.88f, MathF.Max(MathF.Abs(u), MathF.Abs(v)));
                    float a = C01(f(u, v, t)) * edge;
                    int i = ((cy * cell + y) * n + cx * cell + x) * 4;
                    // Smoke frames get a little top light so the puffs read as volumes, not flat blobs.
                    float l = shade ? 0.72f + 0.28f * C01(v * 0.5f + 0.5f) : 1;
                    px[i] = px[i + 1] = px[i + 2] = l; px[i + 3] = a;
                }
            }
            return px;
        }

        // ---------- the textures ----------
        public static float[] Dot(int n) => Mono(n, (u, v) => { float d = C01(1 - Len(u, v)); return d * d; });
        // Flash / glow: a hot pin-point core inside a wide soft halo.
        public static float[] Glow(int n) => Mono(n, (u, v) => { float d = Len(u, v); return 0.55f * MathF.Exp(-d * d * 30) + 0.45f * MathF.Pow(C01(1 - d), 2.2f); });
        // Spark: a very bright centre falling off fast; stretched by velocity it becomes a streak.
        public static float[] Spark(int n) => Mono(n, (u, v) => { float d = Len(u, v); return MathF.Exp(-d * d * 14) * 0.85f + MathF.Pow(C01(1 - d), 3) * 0.15f; });
        public static float[] Ring(int n) => Mono(n, (u, v) => C01(1 - MathF.Abs(Len(u, v) - 0.82f) / 0.1f));
        // Shock ring: a crisp leading edge with a soft trailing falloff inside, slightly broken up.
        public static float[] Shock(int n) => Mono(n, (u, v) =>
        {
            float d = Len(u, v), ang = MathF.Atan2(v, u);
            float front = d < 0.86f ? MathF.Pow(Sm(0.35f, 0.86f, d), 2.4f) : C01(1 - (d - 0.86f) / 0.06f);
            return front * (0.7f + 0.3f * Around(ang, 3, 11));
        });
        public static float[] Burst(int n) => Mono(n, (u, v) => { float d = Len(u, v), a = MathF.Atan2(v, u); return C01(1 - d) * (0.4f + 0.6f * MathF.Pow(MathF.Abs(MathF.Cos(a * 6)), 8)); });
        public static float[] Streak(int n) => Mono(n, (u, v) => C01(1 - MathF.Abs(u)) * C01((v + 1) / 2) * (0.6f + 0.4f * MathF.Sin(u * 20)));
        public static float[] Hex(int n) => Mono(n, (u, v) => { float x = (u + 1) * 6, y = (v + 1) * 6; float h = MathF.Abs(MathF.Sin(x * 1.732f) * MathF.Sin(y * 2f + x)); return 0.25f + 0.75f * MathF.Pow(h, 6); });

        // Rune circle: double outer ring with a band of glyphs, a dashed ring, a hexagram inside a
        // circle and a small eight-point star at the heart. Glyphs are hashed strokes in 24 cells.
        public static float[] Rune(int n) => Mono(n, (u, v) =>
        {
            float d = Len(u, v), a = MathF.Atan2(v, u); if (d > 0.97f) return 0;
            float ring = MathF.Max(Band(d, 0.93f, 0.018f), Band(d, 0.72f, 0.014f));
            ring = MathF.Max(ring, Band(d, 0.66f, 0.008f) * (MathF.Sin(a * 36) > 0 ? 1 : 0)); // dashed
            // glyph band between 0.74 and 0.91: 24 cells, each 3 hashed strokes on a 3x3 lattice
            float glyph = 0;
            if (d > 0.745f && d < 0.905f)
            {
                float cellA = (a / (MathF.PI * 2) + 0.5f) * 24; int c = (int)MathF.Floor(cellA);
                float gx = (cellA - c) * 2 - 1, gy = (d - 0.825f) / 0.07f; // local -1..1
                if (MathF.Abs(gx) < 0.75f)
                    for (int s = 0; s < 3; s++)
                    {
                        float h = Hash(c, s, 7); int p0 = (int)(h * 9) % 9, p1 = (int)(Hash(c, s, 9) * 9) % 9; if (p0 == p1) p1 = (p1 + 4) % 9;
                        float x0 = (p0 % 3 - 1) * 0.55f, y0 = (p0 / 3 - 1) * 0.8f, x1 = (p1 % 3 - 1) * 0.55f, y1 = (p1 / 3 - 1) * 0.8f;
                        float dx = x1 - x0, dy = y1 - y0, t = C01(((gx - x0) * dx + (gy - y0) * dy) / (dx * dx + dy * dy + 1e-5f));
                        float dist = Len(gx - (x0 + dx * t), gy - (y0 + dy * t));
                        glyph = MathF.Max(glyph, C01(1 - dist / 0.16f));
                    }
            }
            float hex = 0;
            if (d < 0.66f) for (int i = 0; i < 6; i++) { float th = i * MathF.PI / 3; hex = MathF.Max(hex, Band(u * MathF.Cos(th) + v * MathF.Sin(th), 0.33f, 0.011f)); }
            float inner = Band(d, 0.38f, 0.01f);
            float star = d < 0.3f ? Band(d, 0.3f * (0.35f + 0.65f * MathF.Pow(MathF.Abs(MathF.Cos(a * 4)), 6)), 0.025f) : 0;
            float glowFill = 0.12f * Sm(0.97f, 0.6f, d);
            return MathF.Max(MathF.Max(ring, glyph * 0.95f), MathF.Max(hex * 0.85f, MathF.Max(inner, star))) + glowFill;
        });

        // Ground cracks: nine jagged rays with side branches and fine noise jitter.
        public static float[] Crack(int n) => Mono(n, (u, v) =>
        {
            float r = Len(u, v), a = MathF.Atan2(v, u), step = MathF.PI * 2 / 9, best = 0;
            if (r < 0.04f || r > 1) return 0;
            float jit = (Fbm(u * 9, v * 9, 3, 3) - 0.5f) * 0.12f;
            for (int k = -1; k <= 9; k++)
            {
                float ray = k * step + MathF.Sin(r * 11 + k * 7.3f) * 0.16f + MathF.Sin(r * 31 + k) * 0.05f + jit;
                float dd = MathF.Abs(DeltaAngle(a, ray)) * r;
                float w = 0.024f * (1 - r * 0.7f);
                best = MathF.Max(best, C01(1 - dd / w));
                float ray2 = ray + 0.35f * (k % 2 == 0 ? 1 : -1);
                if (r > 0.42f) { float d2 = MathF.Abs(DeltaAngle(a, ray2)) * r; best = MathF.Max(best, 0.8f * C01(1 - d2 / (w * 0.7f))); }
                float ray3 = ray - 0.22f * (k % 2 == 0 ? 1 : -1);
                if (r > 0.7f) { float d3 = MathF.Abs(DeltaAngle(a, ray3)) * r; best = MathF.Max(best, 0.6f * C01(1 - d3 / (w * 0.5f))); }
            }
            return best * C01((1 - r) * 3);
        });
        static float DeltaAngle(float a, float b) { float d = (a - b) % (MathF.PI * 2); if (d > MathF.PI) d -= MathF.PI * 2; if (d < -MathF.PI) d += MathF.PI * 2; return d; }

        // Light rays: 40 thin god-rays of hashed length around a soft centre.
        public static float[] Rays(int n) => Mono(n, (u, v) =>
        {
            float d = Len(u, v), a = (MathF.Atan2(v, u) / (MathF.PI * 2) + 0.5f) * 40; int k = (int)MathF.Floor(a);
            float f = a - k, len = 0.45f + 0.55f * Hash(k, 0, 21), wid = 0.15f + 0.25f * Hash(k, 1, 21);
            float beam = C01(1 - MathF.Abs(f - 0.5f) / wid) * C01(1 - d / len) * Sm(0, 0.12f, d);
            return MathF.Pow(beam, 1.5f) + 0.5f * MathF.Exp(-d * d * 18);
        });
        // Anamorphic flare: a thin horizontal streak with a small glow (light / crit halo flare).
        public static float[] Flare(int n) => Mono(n, (u, v) => MathF.Exp(-v * v * 260) * MathF.Pow(C01(1 - MathF.Abs(u)), 2) + 0.35f * MathF.Exp(-(u * u + v * v) * 22));
        // Caustics: ridged noise web inside a soft disc (tide ground glow).
        public static float[] Caustic(int n) => Mono(n, (u, v) => MathF.Pow(Ridge(u * 3.2f + 9, v * 3.2f + 9, 41), 7) * Sm(1, 0.55f, Len(u, v)));
        // Foam ring: a ragged frothy band (tide splash).
        public static float[] Foam(int n) => Mono(n, (u, v) =>
        {
            float d = Len(u, v), froth = Sm(0.42f, 0.7f, Fbm(u * 6 + 3, v * 6 + 3, 51));
            return Band(d, 0.74f, 0.2f) * (0.25f + 0.75f * froth) + Band(d, 0.9f, 0.05f) * 0.6f * froth;
        });
        // Lava (additive): bright molten veins over a dimmer melt, with a ragged edge.
        public static float[] Lava(int n) => Mono(n, (u, v) =>
        {
            float d = Len(u, v), edge = 0.72f + 0.2f * Around(MathF.Atan2(v, u), 2.5f, 61);
            float veins = MathF.Pow(Ridge(u * 3 + 2, v * 3 + 2, 63), 4);
            return (0.3f + 0.7f * veins) * Sm(edge, edge - 0.25f, d);
        });
        // Crust (alpha): dark cooling basalt around the pool and floating plates over it.
        public static float[] Crust(int n) => Color(n, (float u, float v, out float r, out float g, out float b, out float a) =>
        {
            float d = Len(u, v), rn = d / (0.8f + 0.2f * Around(MathF.Atan2(v, u), 2.5f, 61));
            float tone = 0.55f + 0.45f * Fbm(u * 8, v * 8, 71);
            r = 0.2f * tone; g = 0.13f * tone; b = 0.1f * tone;
            float rim = Sm(0.62f, 0.86f, rn) * Sm(1.12f, 0.95f, rn);
            float plates = Sm(0.58f, 0.66f, Fbm(u * 3.5f + 4, v * 3.5f + 4, 73)) * Sm(0.85f, 0.6f, rn);
            a = MathF.Max(rim, plates * 0.9f);
        });
        // Rock chunk (alpha): an irregular faceted stone, lit from the top left.
        public static float[] Chunk(int n) => Color(n, (float u, float v, out float r, out float g, out float b, out float a) =>
        {
            float d = Len(u, v), ang = MathF.Atan2(v, u);
            int facets = 7; float fa = (ang / (MathF.PI * 2) + 0.5f) * facets; int k = (int)MathF.Floor(fa);
            float rad = 0.62f + 0.3f * (Hash(k, 0, 81) + (Hash(k + 1, 0, 81) - Hash(k, 0, 81)) * (fa - k));
            if (k == facets - 1) rad = 0.62f + 0.3f * (Hash(k, 0, 81) + (Hash(0, 0, 81) - Hash(k, 0, 81)) * (fa - k));
            a = Sm(rad, rad - 0.08f, d);
            float facetLight = 0.45f + 0.55f * Hash(k, 3, 83), dir = C01(0.5f + 0.5f * (-u + v) * 0.7f);
            float tone = (0.35f + 0.45f * dir) * (0.7f + 0.3f * facetLight) * (0.85f + 0.15f * Fbm(u * 10, v * 10, 85));
            r = tone; g = tone * 0.93f; b = tone * 0.85f;
        });
        // Leaf (alpha): a pointed leaf along u with a darker midrib.
        public static float[] Leaf(int n) => Color(n, (float u, float v, out float r, out float g, out float b, out float a) =>
        {
            float w = 0.42f * MathF.Pow(C01(1 - u * u), 0.8f);
            a = Sm(w, w - 0.06f, MathF.Abs(v + 0.06f * MathF.Sin(u * 3))) * Sm(0.98f, 0.9f, MathF.Abs(u));
            float rib = 1 - 0.35f * C01(1 - MathF.Abs(v) / 0.03f);
            float l = (0.8f + 0.2f * C01(v + 0.5f)) * rib; r = g = b = l;
        });

        // Flame flipbook (4x4): a tongue of fire that licks upward, splits and thins away.
        public static float[] FlameSheet(int cell = 64) => Sheet(4, cell, (u, v, t) =>
        {
            float y = (v + 1) * 0.5f;                                   // 0 bottom .. 1 top
            float n1 = Fbm(u * 1.6f + 3, v * 1.5f - t * 3.5f, 91), n2 = Fbm(u * 3.2f + 7, v * 2.6f - t * 6, 93, 5);
            float sway = (n1 - 0.5f) * 0.9f * y;
            float w = 0.72f * MathF.Pow(C01(1 - y / (0.95f - 0.2f * t)), 0.8f);
            float body = MathF.Pow(C01(1 - MathF.Abs(u + sway) / w), 0.7f);
            float baseFade = Sm(0, 0.22f, y + 0.1f * t);
            float erode = 0.15f + 0.6f * t;                              // burns away over life
            float flame = body * baseFade * (0.4f + 1.2f * n2) - erode * (0.3f + 0.7f * y);
            return Sm(0.0f, 0.75f, flame) * (1 - 0.3f * t);
        });
        // Smoke flipbook (4x4): a billowing puff that swells, curls and thins out.
        public static float[] SmokeSheet(int cell = 64) => Sheet(4, cell, (u, v, t) =>
        {
            float rad = 0.5f + 0.38f * t;
            float wu = u + (Fbm(u * 1.6f + t * 1.2f, v * 1.6f, 101) - 0.5f) * 0.55f, wv = v + (Fbm(u * 1.6f, v * 1.6f - t * 1.2f, 103) - 0.5f) * 0.55f;
            float body = Sm(rad, 0, Len(wu, wv));
            float billow = Fbm(u * 2.6f + t * 1.5f, v * 2.6f - t, 105, 5);
            return C01((body * (0.6f + 0.9f * billow) - 0.05f - t * 0.22f) * 1.8f);
        }, true);
    }
}
