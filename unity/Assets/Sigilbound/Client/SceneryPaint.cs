// The painted arena textures, made in code (no image assets): the props' colour ramp, the sky with
// layered clouds, the distant ridge cards and the painted ground. Tools~/scenery/paint.py is the
// numpy mirror used for the review renders; keep the two in step (same hash, noise and formulas).
// See docs/sigilbound/art/SCENERY.md.
using UnityEngine;

namespace Sigilbound.Client
{
    public static class SceneryPaint
    {
        // ---------- noise: an integer hash, value noise with smoothstep, fbm ----------
        static float Hash(int ix, int iy, uint seed)
        {
            unchecked
            {
                uint h = (uint)ix * 374761393u + (uint)iy * 668265263u + seed * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }
        public static float VNoise(float x, float y, uint seed)
        {
            float fx0 = Mathf.Floor(x), fy0 = Mathf.Floor(y);
            float fx = x - fx0, fy = y - fy0;
            int ix = (int)fx0, iy = (int)fy0, ix0 = ix & 0xFFFF, iy0 = iy & 0xFFFF, ix1 = (ix + 1) & 0xFFFF, iy1 = (iy + 1) & 0xFFFF; // wrap after the +1: no seams at 0
            float sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            float a = Hash(ix0, iy0, seed), b = Hash(ix1, iy0, seed), c = Hash(ix0, iy1, seed), d = Hash(ix1, iy1, seed);
            float ab = a + (b - a) * sx, cd = c + (d - c) * sx;
            return ab + (cd - ab) * sy;
        }
        public static float Fbm(float x, float y, uint seed, int octaves = 5)
        {
            float s = 0, amp = 0.5f, norm = 0;
            for (int o = 0; o < octaves; o++)
            {
                s += VNoise(x, y, seed + (uint)(o * 17)) * amp; norm += amp;
                float nx = (x * 0.8f - y * 0.6f) * 2.03f; y = (x * 0.6f + y * 0.8f) * 2.03f; x = nx; amp *= 0.5f; // each octave turned: no grid-aligned blocks
            }
            return s / norm;
        }
        static float Smooth(float a, float b, float x) { float t = Mathf.Clamp01((x - a) / (b - a)); return t * t * (3 - 2 * t); }
        static Color L(Color a, Color b, float t) => new Color(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1);
        static Color32 C32(Color c, float a = 1) => new Color32((byte)(Mathf.Clamp01(c.r) * 255), (byte)(Mathf.Clamp01(c.g) * 255), (byte)(Mathf.Clamp01(c.b) * 255), (byte)(Mathf.Clamp01(a) * 255));

        static Texture2D Tex(int w, int h, string name, bool mips, TextureWrapMode wrap) =>
            new Texture2D(w, h, TextureFormat.RGBA32, mips) { name = name, wrapMode = wrap, filterMode = FilterMode.Bilinear };

        // ---------- prop ramp: u = baked shade, v = material row (4 px per row) ----------
        public const int RampW = 64, Rows = 16;
        public static Texture2D Ramp(Color[] rows, Color shadowTint, Color lightTint)
        {
            var t = Tex(RampW, Rows * 4, "SceneryRamp", false, TextureWrapMode.Clamp);
            var px = new Color32[RampW * Rows * 4];
            for (int r = 0; r < Rows; r++)
            {
                Color c = rows[r];
                Color dark = c * (0.30f * 0.6f) + shadowTint * (0.4f * 0.30f) + shadowTint * 0.12f;
                Color lit = new Color(Mathf.Min(c.r * 1.3f + lightTint.r * 0.1f, 1), Mathf.Min(c.g * 1.3f + lightTint.g * 0.1f, 1), Mathf.Min(c.b * 1.3f + lightTint.b * 0.1f, 1));
                for (int i = 0; i < RampW; i++)
                {
                    float x = i / (RampW - 1f);
                    x += (Mathf.Round(x * 4) / 4 - x) * 0.35f; // soft three-band painterly steps
                    Color o = x < 0.55f ? L(dark, c, Smooth(0, 1, Mathf.Clamp01(x / 0.55f))) : L(c, lit, Mathf.Clamp01((x - 0.55f) / 0.45f));
                    var o32 = C32(o);
                    for (int k = 0; k < 4; k++) px[(r * 4 + k) * RampW + i] = o32;
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // ---------- sky: horizon-to-zenith gradient, a sun glow, layered clouds lit from above ----------
        public static Texture2D Sky(Color top, Color horizon, Color sun, uint seed, float cloudiness, bool storm, int w = 384, int h = 160)
        {
            var t = Tex(w, h, "SkyPaint", false, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            Color cdark = L(horizon, top, 0.55f) * (storm ? 0.55f : 0.8f), clit = L(horizon, sun, 0.6f) * (storm ? 0.75f : 1.05f);
            float th = 0.62f - cloudiness * 0.25f;
            for (int y = 0; y < h; y++)
            {
                float v = (y + 0.5f) / h;
                Color grad = L(horizon, top, Mathf.Pow(Smooth(0, 0.85f, v), 0.8f));
                float band = Smooth(0.05f, 0.35f, v) * (1 - Smooth(0.75f, 1f, v));
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float g = Mathf.Exp(-(Mathf.Pow((u - 0.68f) / 0.22f, 2) + Mathf.Pow((v - 0.12f) / 0.2f, 2)));
                    Color b = grad + (sun * 0.9f - grad) * (g * 0.55f);
                    float wu = u * 7f + 0.7f * Fbm(u * 3f, v * 3f, seed + 50, 3), wv = v * 3.2f + 0.35f * Fbm(u * 3f + 5f, v * 3f, seed + 60, 3); // brush swirl
                    float d = Fbm(wu, wv, seed);
                    float mask = Smooth(th, th + 0.18f, d * (0.6f + 0.6f * band));
                    Color o = b;
                    if (mask > 0.001f)
                    {
                        float up = Fbm(wu, wv + 0.08f, seed) - Fbm(wu, wv - 0.08f, seed);
                        Color cloud = L(cdark, clit, Mathf.Clamp01(0.5f + up * 9f));
                        cloud = cloud + (horizon - cloud) * ((1 - Smooth(0, 0.4f, v)) * 0.6f);
                        o = L(b, cloud, mask * 0.92f);
                    }
                    px[y * w + x] = C32(o);
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // ---------- distant ridges: layer 0 is the farthest; style per theme ----------
        static float RidgeHeight(float u, int layer, string style, uint seed)
        {
            float f = layer == 0 ? 3f : layer == 1 ? 5f : 8f;
            float n = Fbm(u * f, layer * 3.7f, seed + (uint)(layer * 31), 4), hgt;
            switch (style)
            {
                case "peaks": { float r = 1 - Mathf.Abs(n * 2 - 1); hgt = 0.25f + 0.6f * Mathf.Pow(r, 1.6f); break; }
                case "mesas": { float q = Mathf.Floor(n * 4) / 4; hgt = 0.2f + 0.55f * (q + (n - q) * 0.15f); break; }
                case "sea": hgt = 0.08f + 0.25f * n; break;
                case "dunes": hgt = 0.1f + 0.3f * Mathf.Pow(n, 1.3f); break; // long low dune lines (the glass desert)
                case "volcano":
                {
                    float c = Mathf.Exp(-Mathf.Pow((u - 0.5f) / (0.12f + layer * 0.05f), 2));
                    hgt = Mathf.Min(0.2f + 0.25f * n + c * (0.75f - layer * 0.2f), 0.9f - layer * 0.15f); break;
                }
                default: hgt = 0.18f + 0.5f * n; break; // hills, forest, ruins
            }
            if (style == "forest" && layer > 0) // a crown line of trees on the nearer ridges
            {
                float b = Fbm(u * (90 + layer * 50), 2f + layer, seed + 3, 3);
                hgt += 0.08f * Mathf.Sqrt(Mathf.Clamp01(1 - Mathf.Abs(b * 2 - 1) * 1.6f));
            }
            if (style == "ruins" && layer == 1) // a few broken towers
            {
                float k = VNoise(Mathf.Floor(u * 22), 0, seed + 5);
                if (k > 0.86f) hgt += 0.18f + 0.1f * VNoise(Mathf.Floor(u * 22), 1, seed);
            }
            return hgt;
        }

        public static Texture2D Ridge(int layer, string style, Color sil, Color fog, Color sun, uint seed, int w = 512, int h = 96)
        {
            var t = Tex(w, h, "Ridge" + layer, false, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            var hs = new float[w];
            for (int x = 0; x < w; x++) hs[x] = RidgeHeight((x + 0.5f) / w, layer, style, seed);
            float aerial = layer == 0 ? 0.72f : layer == 1 ? 0.45f : 0.2f;
            Color col = L(sil, fog, aerial);
            Color lava = new Color(1f, 0.45f, 0.12f);
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, H = hs[x];
                float slope = (x == 0 ? hs[1] - hs[0] : x == w - 1 ? hs[w - 1] - hs[w - 2] : (hs[x + 1] - hs[x - 1]) * 0.5f) * w;
                for (int y = 0; y < h; y++)
                {
                    float v = (y + 0.5f) / h;
                    float a = Mathf.Clamp01((H - v) * h * 0.9f);
                    if (a <= 0) { px[y * w + x] = new Color32(0, 0, 0, 0); continue; }
                    float rim = Mathf.Clamp(-slope * 0.08f, 0, 0.25f) * Mathf.Clamp01(1 - (H - v) * 6);
                    float tex = Fbm(u * 60, v * 12, seed + 9, 3);
                    Color c = col * (0.92f + 0.16f * tex);
                    c = c + (sun - c) * rim;
                    c = c + (fog - c) * ((1 - Smooth(0, 0.55f, v / Mathf.Max(H, 1e-3f))) * (0.55f - layer * 0.12f)); // haze at the foot
                    if (style == "volcano" && layer == 0)
                        c = c + (lava - c) * (Mathf.Exp(-Mathf.Pow((u - 0.5f) / 0.04f, 2)) * Smooth(0, 0.15f, v - H + 0.15f));
                    px[y * w + x] = C32(c, a);
                }
            }
            t.SetPixels32(px); t.Apply(false, true);
            return t;
        }

        // ---------- painted ground: two grass tones in broad strokes, the worn arena patch with a
        // trodden rim and pebbles, flagstones in the ruins, wet sand toward water ----------
        // horizon: the colour the field fades to where it meets the near ridge card (z 28, x +-30),
        // so the ground runs into the painted hills instead of ending on a hard line.
        public static Texture2D Ground(Color g1, Color g2, Color worn, Color rock, uint seed, bool flag, Color wet, float waterZ, Color horizon, int n = 256, float extent = 45f)
        {
            var t = Tex(n, n, "GroundPaint", true, TextureWrapMode.Clamp);
            var px = new Color32[n * n];
            float sd = seed;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // Unity's Plane maps u to -X and v to -Z; invert so world (wx, wz) lines up.
                    float wx = -(x / (n - 1f) * 2 - 1) * extent, wz = -(y / (n - 1f) * 2 - 1) * extent;
                    float big = Fbm(wx * 0.06f + sd, wz * 0.06f, 11, 4), mid = Fbm(wx * 0.35f, wz * 0.35f + sd, 23, 3);
                    Color c = L(g1, g2, Smooth(0.3f, 0.7f, big * 0.7f + mid * 0.3f));
                    c *= 0.9f + 0.2f * Fbm(wx * 2.2f + wz * 0.6f, wz * 0.5f - wx * 0.2f, 37, 2); // painted strokes
                    float e = Mathf.Sqrt(Mathf.Pow(wx / 5.4f, 2) + Mathf.Pow((wz - 0.6f) / 5.0f, 2));
                    if (e < 2.2f)
                    {
                        float en = Fbm(wx * 0.8f, wz * 0.8f, 41, 3);
                        float wk = Smooth(1.42f, 1.0f, e + (en - 0.5f) * 0.35f);
                        Color soil = L(worn, worn * 0.78f, Fbm(wx * 1.6f, wz * 1.6f, 53, 3));
                        c = L(c, soil, wk * 0.92f);
                        float rim = Mathf.Exp(-Mathf.Pow((e - 1.05f - (en - 0.5f) * 0.3f) / 0.07f, 2)); // the trodden rim
                        c *= 1 - 0.18f * rim;
                        if (Fbm(wx * 4.5f, wz * 4.5f, 61, 2) > 0.7f) c = L(c, rock, wk * 0.5f); // pebbles
                        if (flag && e < 1.25f)
                        {
                            float gx = wx * 0.9f + 0.5f * Mathf.Repeat(Mathf.Floor(wz * 0.9f), 2), gz = wz * 0.9f;
                            float gap = Mathf.Min(Mathf.Abs(gx - Mathf.Round(gx)), Mathf.Abs(gz - Mathf.Round(gz)));
                            float tile = Smooth(0.03f, 0.07f, gap) * (Fbm(wx * 0.5f, wz * 0.5f, 71, 2) > 0.42f ? 1 : 0);
                            float tone = 0.85f + 0.3f * VNoise(Mathf.Floor(gx), Mathf.Floor(gz), 73);
                            c = L(c, L(rock * 0.6f, rock * tone, tile), Smooth(1.25f, 0.6f, e) * 0.85f);
                        }
                    }
                    if (waterZ > 0) c = L(c, wet, Smooth(waterZ - 3f, waterZ - 0.5f, wz));
                    c *= 0.94f + 0.12f * Fbm(wx * 3, wz * 3, 83, 2);
                    float hn = (Fbm(wx * 0.3f, wz * 0.3f, 91, 2) - 0.5f) * 3f;
                    c = L(c, horizon, Mathf.Max(Smooth(21f, 27.5f, wz + hn), Smooth(23f, 29.5f, Mathf.Abs(wx) + hn)) * 0.75f);
                    px[y * n + x] = C32(c);
                }
            t.SetPixels32(px); t.Apply(true, true);
            return t;
        }

        // ---------- water and lava strips (the 90 x 16 m plane beyond the arena; v = 0 at the far edge) ----------
        // Water: shallow near the shore with a foam line, painted wave streaks, deep further out, then haze.
        public static Texture2D Water(Color deep, Color light, Color foam, Color fog, uint seed, int w = 512, int h = 96)
        {
            var t = Tex(w, h, "WaterPaint", true, TextureWrapMode.Clamp);
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float wx = -(u * 2 - 1) * 45f, d = (1 - v) * 16f; // metres across, metres out from the shore
                    Color c = L(light, deep, Smooth(0f, 6f, d));
                    float streak = Fbm(wx * 0.18f, d * 1.7f, seed + 3, 4);
                    c = L(c, light * 1.25f, Smooth(0.58f, 0.74f, streak) * 0.45f);
                    c *= 0.93f + 0.14f * Fbm(wx * 1.1f, d * 3f, seed + 7, 2);
                    float edge = d + (Fbm(wx * 0.6f, 0.5f, seed + 11, 3) - 0.5f) * 0.9f;
                    c = L(c, foam, Smooth(1.1f, 0.25f, edge) * 0.75f);                       // the foam line at the shore
                    c = L(c, fog * 0.85f, Smooth(7f, 16f, d) * 0.55f);                       // out to the haze
                    px[y * w + x] = C32(c);
                }
            t.SetPixels32(px); t.Apply(true, true);
            return t;
        }

        // Lava: dark crusted basalt split by glowing veins; the veins widen and the glow only builds far out.
        // Returns (albedo, emission mask).
        public static (Texture2D albedo, Texture2D glow) Lava(uint seed, int w = 512, int h = 96)
        {
            var a = Tex(w, h, "LavaCrust", true, TextureWrapMode.Clamp);
            var g = Tex(w, h, "LavaGlow", true, TextureWrapMode.Clamp);
            var pa = new Color32[w * h]; var pg = new Color32[w * h];
            Color crust = new Color(0.10f, 0.075f, 0.07f), crust2 = new Color(0.2f, 0.15f, 0.13f), hot = new Color(1f, 0.42f, 0.1f), warm = new Color(0.45f, 0.14f, 0.05f);
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                    float wx = -(u * 2 - 1) * 45f, d = (1 - v) * 16f, far = Smooth(3f, 16f, d);
                    // plates of crust: ridged noise lines are the cracks between them
                    float wu = wx * 0.32f + 0.6f * Fbm(wx * 0.1f, d * 0.2f, seed + 21, 2), wv = d * 0.9f + 0.5f * Fbm(wx * 0.12f + 3, d * 0.2f, seed + 23, 2);
                    float r = 1 - Mathf.Abs(Fbm(wu, wv, seed + 25, 4) * 2 - 1);
                    float vein = Smooth(0.935f - 0.17f * far, 0.99f, r);
                    float tex = Fbm(wx * 1.4f, d * 1.4f, seed + 27, 3);
                    Color c = L(crust, crust2, tex) * (0.85f + 0.3f * Fbm(wx * 0.2f, d * 0.4f, seed + 29, 2));
                    c = L(c, warm, Smooth(0.75f, 0.95f, r) * 0.5f);                          // crust scorched beside the cracks
                    c = L(c, hot, vein);
                    float pool = Smooth(10f, 16f, d) * Smooth(0.45f, 0.75f, Fbm(wx * 0.08f, d * 0.25f, seed + 31, 3)); // open lava far out
                    c = L(c, hot, pool * 0.8f);
                    float glow = Mathf.Clamp01(vein * (0.55f + 0.45f * far) + pool * 0.9f + far * 0.12f);
                    pa[y * w + x] = C32(c);
                    pg[y * w + x] = C32(hot * glow);
                }
            a.SetPixels32(pa); a.Apply(true, true);
            g.SetPixels32(pg); g.Apply(true, true);
            return (a, g);
        }

        // A crack decal for boss slams: jagged dark lines radiating from the centre (alpha only).
        static Texture2D _crack;
        public static Texture2D Crack()
        {
            if (_crack != null) return _crack;
            const int n = 128;
            var t = Tex(n, n, "Crack", false, TextureWrapMode.Clamp);
            var px = new Color32[n * n];
            var rng = new System.Random(5);
            var a = new float[n * n];
            for (int k = 0; k < 9; k++)
            {
                float ang = k / 9f * Mathf.PI * 2 + (float)rng.NextDouble() * 0.5f, x = n / 2f, y = n / 2f, w = 2.6f;
                for (int s = 0; s < 70; s++)
                {
                    ang += ((float)rng.NextDouble() - 0.5f) * 0.6f;
                    x += Mathf.Cos(ang) * 0.85f; y += Mathf.Sin(ang) * 0.85f; w = Mathf.Max(0.6f, w - 0.03f);
                    if (rng.NextDouble() < 0.04) { float b = ang + ((float)rng.NextDouble() > 0.5 ? 0.8f : -0.8f), bx = x, by = y; for (int q = 0; q < 14; q++) { bx += Mathf.Cos(b); by += Mathf.Sin(b); Stamp(a, n, bx, by, 0.8f); } }
                    Stamp(a, n, x, y, w);
                }
            }
            for (int i = 0; i < n * n; i++)
            {
                float dx = (i % n - n / 2f) / (n / 2f), dy = (i / n - n / 2f) / (n / 2f);
                float fade = Mathf.Clamp01(1 - Mathf.Sqrt(dx * dx + dy * dy));
                px[i] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(a[i]) * fade * 255));
            }
            t.SetPixels32(px); t.Apply();
            return _crack = t;
        }
        static void Stamp(float[] a, int n, float x, float y, float r)
        {
            for (int yy = (int)(y - r - 1); yy <= (int)(y + r + 1); yy++)
                for (int xx = (int)(x - r - 1); xx <= (int)(x + r + 1); xx++)
                {
                    if (xx < 0 || yy < 0 || xx >= n || yy >= n) continue;
                    float d = Mathf.Sqrt((xx - x) * (xx - x) + (yy - y) * (yy - y));
                    a[yy * n + xx] = Mathf.Max(a[yy * n + xx], Mathf.Clamp01(r - d + 0.5f));
                }
        }

        // A backdrop strip (sky, ridges): a straight front card at depth z across +-halfW, chamfered
        // corners and side cards running back to backZ, u along the strip, v up. Straight cards project as
        // straight lines from the high battle cameras (a curved arc bowed into an arch).
        // vTop: the texture's v at the top edge; above 1 the clamped top row (the sky's zenith colour, the same
        // as the camera's clear colour) carries on, so low upward shots (the lair reveal) never run out of sky.
        public static Mesh Backdrop(float halfW, float z, float backZ, float y0, float height, float vTop = 1f)
        {
            float c = 8f; // chamfer
            var path = new[] { new Vector2(-halfW, backZ), new Vector2(-halfW, z - c), new Vector2(-halfW + c, z), new Vector2(halfW - c, z), new Vector2(halfW, z - c), new Vector2(halfW, backZ) };
            var len = new float[path.Length];
            for (int i = 1; i < path.Length; i++) len[i] = len[i - 1] + Vector2.Distance(path[i - 1], path[i]);
            var m = new Mesh { name = "Backdrop" };
            var v = new Vector3[path.Length * 2]; var uv = new Vector2[v.Length]; var tri = new int[(path.Length - 1) * 6];
            for (int i = 0; i < path.Length; i++)
            {
                v[i * 2] = new Vector3(path[i].x, y0, path[i].y); v[i * 2 + 1] = new Vector3(path[i].x, y0 + height, path[i].y);
                float u = len[i] / len[path.Length - 1];
                uv[i * 2] = new Vector2(u, 0); uv[i * 2 + 1] = new Vector2(u, vTop);
            }
            for (int i = 0; i < path.Length - 1; i++)
            {
                int b = i * 2; // seen from inside (the materials draw both sides anyway)
                tri[i * 6] = b; tri[i * 6 + 1] = b + 1; tri[i * 6 + 2] = b + 3;
                tri[i * 6 + 3] = b; tri[i * 6 + 4] = b + 3; tri[i * 6 + 5] = b + 2;
            }
            m.vertices = v; m.uv = uv; m.triangles = tri; m.RecalculateBounds(); m.RecalculateNormals();
            return m;
        }
    }
}
