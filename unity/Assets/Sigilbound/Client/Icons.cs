// Code-drawn icons (no emoji, no font glyphs, no image files): each icon is a signed-distance
// shape evaluated once into a small white alpha texture and tinted when drawn. Fonts on Windows
// and Android don't carry the same symbol glyphs (emoji and dingbats went missing on the owner's
// devices), so every icon in the UI comes from here. All shapes are original.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public enum Icon
    {
        Coin, Shard, Essence, Tome, Star, StarEmpty, Menu, Back, Next, Close, Play, Check, Lock,
        Sword, Shield, Clock, Crown, Skull, Home, Map, Helm, Portal, Tower, Friends, Trophy, Heart,
        Bolt, Pause, Speed, Auto, Camera, Loop, Filter, Sort, Flame, Gale, Tide, Light, Shadow, Plus, Minus, Info,
    }

    public static class Icons
    {
        const int N = 64;
        static readonly Dictionary<Icon, Texture2D> _tex = new Dictionary<Icon, Texture2D>();

        public static Texture2D Tex(Icon i)
        {
            if (_tex.TryGetValue(i, out var t) && t != null) return t;
            var f = Shape(i);
            t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, name = "icon_" + i };
            var px = new Color[N * N];
            float aa = 2.2f / N; // ~1 px of antialiasing in shape units (shape space is -1..1)
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2 - 1, v = (y + 0.5f) / N * 2 - 1; // v up
                    float d = f(new Vector2(u, v));
                    px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(0.5f - d / aa));
                }
            t.SetPixels(px); t.Apply(true, true);
            _tex[i] = t;
            return t;
        }

        // Draws an icon tinted with a colour (keeps GUI.color as it was).
        // Painted icon from the owner's art (Resources/Skin/icons/<name>.png, Tools~/skin/sheet2.py), drawn
        // when the skin is on. False when there is none, so callers fall back to the vector icon.
        public static bool Painted(Rect r, string name, float alpha = 1f)
        {
            var t = Skin.Tex("icons/" + name); if (t == null) return false;
            if (Event.current.type != EventType.Repaint) return true;
            var col = GUI.color; GUI.color = new Color(col.r, col.g, col.b, col.a * alpha);
            GUI.DrawTexture(r, t, ScaleMode.ScaleToFit);
            GUI.color = col;
            return true;
        }

        public static void Draw(Rect r, Icon i, Color c)
        {
            if (Event.current.type != EventType.Repaint) return;
            var was = GUI.color; GUI.color = new Color(c.r, c.g, c.b, c.a * was.a);
            GUI.DrawTexture(r, Tex(i), ScaleMode.ScaleToFit, true);
            GUI.color = was;
        }

        public static Icon ForElement(Element e) => e switch
        {
            Element.Flame => Icon.Flame, Element.Gale => Icon.Gale, Element.Tide => Icon.Tide, Element.Light => Icon.Light, _ => Icon.Shadow,
        };

        // ---------- signed-distance helpers (negative inside) ----------
        static float Circle(Vector2 p, Vector2 c, float r) => (p - c).magnitude - r;
        static float Ring(Vector2 p, Vector2 c, float r, float w) => Mathf.Abs((p - c).magnitude - r) - w;
        static float Box(Vector2 p, Vector2 c, Vector2 half, float round = 0)
        {
            var q = new Vector2(Mathf.Abs(p.x - c.x), Mathf.Abs(p.y - c.y)) - half + Vector2.one * round;
            return new Vector2(Mathf.Max(q.x, 0), Mathf.Max(q.y, 0)).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0) - round;
        }
        static float Seg(Vector2 p, Vector2 a, Vector2 b, float w)
        {
            var pa = p - a; var ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - w;
        }
        // Convex/concave polygon via winding (exact distance to edges, sign by crossing count).
        static float Poly(Vector2 p, Vector2[] v)
        {
            float d = Vector2.Dot(p - v[0], p - v[0]); float s = 1;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                var e = v[j] - v[i]; var w = p - v[i];
                var b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }
        static Vector2[] StarPts(int n, float ro, float ri, float rot = 90)
        {
            var pts = new Vector2[n * 2];
            for (int k = 0; k < n * 2; k++)
            {
                float a = (rot + k * 180f / n) * Mathf.Deg2Rad, r = k % 2 == 0 ? ro : ri;
                pts[k] = new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
            }
            return pts;
        }
        static Vector2 V(float x, float y) => new Vector2(x, y);
        static float U(float a, float b) => Mathf.Min(a, b);
        static float Cut(float a, float b) => Mathf.Max(a, -b);
        static float Stroke(float d, float w) => Mathf.Abs(d) - w;

        static System.Func<Vector2, float> Shape(Icon i)
        {
            switch (i)
            {
                case Icon.Coin: return p => U(Ring(p, Vector2.zero, 0.72f, 0.1f), Poly(p, StarPts(4, 0.42f, 0.16f)));
                case Icon.Shard: return p => Poly(p, new[] { V(0, 0.92f), V(0.5f, 0.2f), V(0.28f, -0.88f), V(-0.28f, -0.88f), V(-0.5f, 0.2f) });
                case Icon.Essence: return p => U(Circle(p, V(0, -0.25f), 0.5f), Poly(p, new[] { V(0, 0.92f), V(0.42f, -0.05f), V(-0.42f, -0.05f) }));
                case Icon.Tome: return p => Cut(Box(p, Vector2.zero, V(0.62f, 0.8f), 0.08f), U(Box(p, V(0.12f, -0.62f), V(0.42f, 0.06f)), Box(p, V(-0.4f, 0), V(0.05f, 0.8f))));
                case Icon.Star: return p => Poly(p, StarPts(5, 0.95f, 0.42f));
                case Icon.StarEmpty: return p => Stroke(Poly(p, StarPts(5, 0.86f, 0.38f)), 0.08f);
                case Icon.Menu: return p => U(U(Box(p, V(0, 0.5f), V(0.75f, 0.09f), 0.08f), Box(p, V(0, 0), V(0.75f, 0.09f), 0.08f)), Box(p, V(0, -0.5f), V(0.75f, 0.09f), 0.08f));
                case Icon.Back: return p => U(Seg(p, V(0.3f, 0.7f), V(-0.35f, 0), 0.13f), Seg(p, V(-0.35f, 0), V(0.3f, -0.7f), 0.13f));
                case Icon.Next: return p => U(Seg(p, V(-0.3f, 0.7f), V(0.35f, 0), 0.13f), Seg(p, V(0.35f, 0), V(-0.3f, -0.7f), 0.13f));
                case Icon.Close: return p => U(Seg(p, V(-0.6f, -0.6f), V(0.6f, 0.6f), 0.13f), Seg(p, V(-0.6f, 0.6f), V(0.6f, -0.6f), 0.13f));
                case Icon.Play: return p => Poly(p, new[] { V(-0.5f, 0.8f), V(0.8f, 0), V(-0.5f, -0.8f) });
                case Icon.Check: return p => U(Seg(p, V(-0.7f, 0), V(-0.2f, -0.55f), 0.14f), Seg(p, V(-0.2f, -0.55f), V(0.75f, 0.6f), 0.14f));
                case Icon.Lock: return p => U(Box(p, V(0, -0.3f), V(0.6f, 0.48f), 0.1f), Cut(Ring(p, V(0, 0.25f), 0.38f, 0.1f), Box(p, V(0, -0.2f), V(1, 0.45f))));
                case Icon.Sword: return p => U(U(Seg(p, V(-0.6f, -0.6f), V(0.7f, 0.7f), 0.1f), Seg(p, V(-0.65f, -0.15f), V(-0.15f, -0.65f), 0.09f)), Circle(p, V(-0.78f, -0.78f), 0.12f));
                case Icon.Shield: return p => Poly(p, new[] { V(0, 0.9f), V(0.75f, 0.6f), V(0.65f, -0.1f), V(0, -0.9f), V(-0.65f, -0.1f), V(-0.75f, 0.6f) });
                case Icon.Clock: return p => U(Ring(p, Vector2.zero, 0.75f, 0.1f), U(Seg(p, Vector2.zero, V(0, 0.48f), 0.09f), Seg(p, Vector2.zero, V(0.35f, -0.12f), 0.09f)));
                case Icon.Crown: return p => U(Poly(p, new[] { V(-0.8f, -0.5f), V(-0.85f, 0.55f), V(-0.4f, 0.05f), V(0, 0.7f), V(0.4f, 0.05f), V(0.85f, 0.55f), V(0.8f, -0.5f) }), Box(p, V(0, -0.68f), V(0.8f, 0.1f)));
                case Icon.Skull: return p => Cut(U(Circle(p, V(0, 0.15f), 0.62f), Box(p, V(0, -0.5f), V(0.36f, 0.3f), 0.08f)), U(U(Circle(p, V(-0.25f, 0.1f), 0.16f), Circle(p, V(0.25f, 0.1f), 0.16f)), Box(p, V(0, -0.62f), V(0.04f, 0.2f))));
                case Icon.Home: return p => U(Poly(p, new[] { V(0, 0.85f), V(0.85f, 0.05f), V(-0.85f, 0.05f) }), Cut(Box(p, V(0, -0.4f), V(0.6f, 0.45f)), Box(p, V(0, -0.55f), V(0.18f, 0.32f))));
                case Icon.Map: return p => Cut(Poly(p, new[] { V(-0.85f, 0.65f), V(-0.3f, 0.85f), V(0.3f, 0.65f), V(0.85f, 0.85f), V(0.85f, -0.65f), V(0.3f, -0.85f), V(-0.3f, -0.65f), V(-0.85f, -0.85f) }), U(Seg(p, V(-0.3f, 0.7f), V(-0.3f, -0.6f), 0.05f), Seg(p, V(0.3f, 0.6f), V(0.3f, -0.7f), 0.05f)));
                case Icon.Helm: return p => Cut(U(Circle(p, V(0, 0.1f), 0.72f), Box(p, V(0, -0.4f), V(0.72f, 0.45f))), U(Box(p, V(0, 0.05f), V(0.55f, 0.08f)), Box(p, V(0, -0.35f), V(0.08f, 0.38f))));
                case Icon.Portal: return p => U(Ring(p, Vector2.zero, 0.78f, 0.09f), Poly(p, StarPts(6, 0.55f, 0.22f)));
                case Icon.Tower: return p => U(Box(p, V(0, -0.2f), V(0.42f, 0.7f)), U(U(Box(p, V(-0.36f, 0.62f), V(0.1f, 0.2f)), Box(p, V(0, 0.62f), V(0.1f, 0.2f))), Box(p, V(0.36f, 0.62f), V(0.1f, 0.2f))));
                case Icon.Friends: return p => U(U(Circle(p, V(-0.35f, 0.35f), 0.28f), Circle(p, V(0.38f, 0.42f), 0.24f)), U(Circle(p, V(-0.35f, -0.55f), 0.5f), Circle(p, V(0.4f, -0.45f), 0.4f)));
                case Icon.Trophy: return p => U(U(Cut(Circle(p, V(0, 0.25f), 0.6f), Box(p, V(0, 0.95f), V(1, 0.35f))), Box(p, V(0, -0.45f), V(0.1f, 0.3f))), Box(p, V(0, -0.78f), V(0.45f, 0.1f), 0.04f));
                case Icon.Heart: return p => U(U(Circle(p, V(-0.36f, 0.25f), 0.42f), Circle(p, V(0.36f, 0.25f), 0.42f)), Poly(p, new[] { V(-0.76f, 0.1f), V(0.76f, 0.1f), V(0, -0.85f) }));
                case Icon.Bolt: return p => Poly(p, new[] { V(0.2f, 0.95f), V(-0.55f, -0.1f), V(-0.05f, -0.1f), V(-0.25f, -0.95f), V(0.55f, 0.15f), V(0.05f, 0.15f) });
                case Icon.Pause: return p => U(Box(p, V(-0.3f, 0), V(0.14f, 0.7f), 0.05f), Box(p, V(0.3f, 0), V(0.14f, 0.7f), 0.05f));
                case Icon.Speed: return p => U(Poly(p, new[] { V(-0.85f, 0.65f), V(0.05f, 0), V(-0.85f, -0.65f) }), Poly(p, new[] { V(-0.05f, 0.65f), V(0.85f, 0), V(-0.05f, -0.65f) }));
                case Icon.Auto: return p => U(Cut(Ring(p, Vector2.zero, 0.62f, 0.11f), Box(p, V(0.6f, 0.3f), V(0.25f, 0.25f))), Poly(p, new[] { V(0.35f, 0.75f), V(0.9f, 0.55f), V(0.5f, 0.1f) }));
                case Icon.Camera: return p => Cut(U(Box(p, V(0, -0.1f), V(0.82f, 0.52f), 0.1f), Box(p, V(-0.3f, 0.5f), V(0.25f, 0.15f))), Circle(p, V(0.05f, -0.1f), 0.3f));
                case Icon.Loop: return p => U(Stroke(Box(p, Vector2.zero, V(0.62f, 0.4f), 0.35f), 0.1f), Poly(p, new[] { V(-0.05f, 0.62f), V(0.3f, 0.4f), V(-0.05f, 0.18f) }));
                case Icon.Filter: return p => Poly(p, new[] { V(-0.85f, 0.75f), V(0.85f, 0.75f), V(0.15f, -0.05f), V(0.15f, -0.8f), V(-0.15f, -0.6f), V(-0.15f, -0.05f) });
                case Icon.Sort: return p => U(U(Box(p, V(-0.1f, 0.55f), V(0.75f, 0.09f)), Box(p, V(-0.3f, 0), V(0.55f, 0.09f))), Box(p, V(-0.5f, -0.55f), V(0.35f, 0.09f)));
                case Icon.Flame: return p => U(Circle(p, V(0, -0.3f), 0.5f), Poly(p, new[] { V(0.05f, 0.95f), V(0.48f, -0.2f), V(-0.48f, -0.2f), V(-0.35f, 0.3f), V(-0.12f, 0.15f) }));
                case Icon.Gale: return p => U(U(Cut(Ring(p, V(0.1f, 0.25f), 0.32f, 0.1f), Box(p, V(0.1f, -0.1f), V(0.5f, 0.3f))), Seg(p, V(-0.8f, -0.07f), V(0.1f, -0.07f), 0.1f)), Seg(p, V(-0.6f, -0.45f), V(0.55f, -0.45f), 0.1f));
                case Icon.Tide: return p => U(Cut(Circle(p, V(0, -0.25f), 0.55f), Box(p, V(0, 0.6f), V(1, 0.3f))), Poly(p, new[] { V(0, 0.92f), V(0.5f, -0.1f), V(-0.5f, -0.1f) }));
                case Icon.Light: return p => U(Circle(p, Vector2.zero, 0.36f), Poly(p, StarPts(8, 0.95f, 0.42f, 0)));
                case Icon.Shadow: return p => Cut(Circle(p, Vector2.zero, 0.75f), Circle(p, V(0.35f, 0.25f), 0.6f));
                case Icon.Plus: return p => U(Box(p, Vector2.zero, V(0.7f, 0.12f), 0.05f), Box(p, Vector2.zero, V(0.12f, 0.7f), 0.05f));
                case Icon.Minus: return p => Box(p, Vector2.zero, V(0.7f, 0.12f), 0.05f);
                default: return p => U(Ring(p, Vector2.zero, 0.78f, 0.1f), U(Box(p, V(0, -0.15f), V(0.09f, 0.35f)), Circle(p, V(0, 0.4f), 0.11f))); // Info
            }
        }
    }
}
