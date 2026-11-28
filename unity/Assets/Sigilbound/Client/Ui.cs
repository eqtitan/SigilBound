// Code-only UI toolkit on Unity's immediate-mode GUI: a phone-sized logical canvas
// (390 points wide, scaled to the screen), rounded panels/buttons from generated
// textures, and touch drag-to-scroll. No editor setup or UI package needed.
// Styled like the web game's gilded dark-fantasy skin (style.css): framed panels with a bronze
// edge, gilded gradient buttons, gold rules, and serif display type for headings and buttons
// (Cinzel, OFL, bundled in Resources/Fonts; see OFL.txt there).
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Ui
    {
        // Layout canvas. Portrait: 390 logical units wide, scaled to the screen width (as before).
        // Landscape: scaled to the screen HEIGHT (RefH logical units tall), so text stays readable
        // from 16:9 to 21:9; menu screens keep their 390-wide layout as a centred column (W = 390,
        // offset by Left), while full-width screens (the battle) get W = the whole width.
        public const float ColumnW = 390f;
        public static bool Landscape => Screen.width > Screen.height;
        static float RefH => Application.isMobilePlatform ? 480f : 600f;
        public static float Scale => Landscape ? Screen.height / RefH : Screen.width / ColumnW;
        public static float FullW => Screen.width / Scale;        // the whole screen, logical units
        public static float W { get; private set; } = ColumnW;    // the width screens lay out in
        public static float Offset { get; private set; }          // logical x of the layout's left edge
        public static float Left => -Offset;                       // the screen's left edge, in layout x
        public static float H => Screen.height / Scale;
        public static Rect Safe                       // safe area in layout units
        {
            get
            {
                var s = Screen.safeArea; float k = Scale;
                return new Rect(s.x / k - Offset, (Screen.height - s.yMax) / k, s.width / k, s.height / k);
            }
        }
        // Full-screen rect in layout coordinates (backdrops, bars that span the whole width).
        public static Rect Screen01 => new Rect(Left, 0, FullW, H);

        // Physical sizes (UX review #1, #3): logical units for a length in dp (160 per inch). Phones put about
        // 2.25 px in a unit, so a fixed size in units comes out smaller in dp than it looks on a PC screen.
        public static float Dp(float dp)
        {
            float dpi = Screen.dpi;
            if (dpi <= 0) dpi = Application.isMobilePlatform ? 400f : 96f;
            return dp * Mathf.Max(1f, dpi / 160f) / Scale;
        }
        // The smallest side of anything tappable: 48 dp on a phone (never under 44 units on a PC), so every
        // target clears the 44 dp minimum.
        public static float Tap => Mathf.Max(44f, Dp(48f));
        // The safe area (camera hole, rounded corners) inset by a gutter on the left and right, in layout units.
        // Screens laid out across the whole width use it for their content; bars use it for their contents.
        public static Rect SafeContent(float gutter = 0)
        {
            var s = Safe;
            float x0 = Mathf.Max(Left, s.x) + gutter, x1 = Mathf.Min(Left + FullW, s.xMax) - gutter;
            return new Rect(x0, s.y, Mathf.Max(0, x1 - x0), s.height);
        }

        // Readable text (UX review #11, art review H5; WCAG contrast 4.5:1 for text): disabled controls keep a
        // grey plate with light grey text instead of fading everything to 40%.
        public static readonly Color DisabledText = Palette.Hex("#d2ccc0");
        public static readonly Color DisabledPlate = Palette.Hex("#34313a");
        // WCAG relative luminance contrast of two opaque colours (1 to 21).
        public static float Contrast(Color a, Color b)
        {
            static float Ch(float c) => c <= 0.03928f ? c / 12.92f : Mathf.Pow((c + 0.055f) / 1.055f, 2.4f);
            static float L(Color c) => 0.2126f * Ch(c.r) + 0.7152f * Ch(c.g) + 0.0722f * Ch(c.b);
            float la = L(a), lb = L(b);
            return (Mathf.Max(la, lb) + 0.05f) / (Mathf.Min(la, lb) + 0.05f);
        }

        // The largest font size from max down to min at which one line of text fits the width.
        public static int FitSize(string text, float width, int max, int min, bool serif = false)
        {
            for (int sz = max; sz > min; sz--)
                if (Label(sz, null, serif ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleCenter, false, serif && SerifCanDraw(text)).CalcSize(new GUIContent(text)).x <= width) return sz;
            return min;
        }

        // A short dark pill behind a line of text that sits on art or the 3D scene (art review H5).
        public static void TextOnArt(Rect r, string text, int size, Color color, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleCenter)
        {
            if (Event.current.type == EventType.Repaint)
            {
                float w = Mathf.Min(r.width, Label(size, color, fs, align).CalcSize(new GUIContent(text)).x + 24);
                float x = align == TextAnchor.MiddleLeft || align == TextAnchor.UpperLeft || align == TextAnchor.LowerLeft ? r.x - 8
                    : align == TextAnchor.MiddleRight || align == TextAnchor.UpperRight || align == TextAnchor.LowerRight ? r.xMax - w + 8 : r.center.x - w / 2;
                Round(new Rect(x, r.y + 1, w, r.height - 2), new Color(0.04f, 0.03f, 0.06f, 0.72f), null, Mathf.Clamp(Mathf.RoundToInt(r.height / 2 - 1), 4, 14));
            }
            Text(r, text, size, color, fs, align);
        }

        // Colour-blind safety (UX review #4): rarity also reads as a letter, never by frame colour alone.
        public static string RarityLetter(Sigilbound.Core.Rarity r) => r switch
        {
            Sigilbound.Core.Rarity.Common => "C", Sigilbound.Core.Rarity.Rare => "R", Sigilbound.Core.Rarity.Epic => "E", _ => "L",
        };
        // A small rarity tab (letter on the rarity colour) in the top-right corner of a rarity-coloured frame.
        public static void RarityTab(Rect frame, Sigilbound.Core.Rarity r, float size = 15)
        {
            var t = new Rect(frame.xMax - size - 2, frame.y + 2, size, size);
            Round(t, Color.Lerp(Palette.Rarity(r), Color.black, 0.55f), Palette.Rarity(r), 4);
            Text(t, RarityLetter(r), Mathf.Max(10, Mathf.RoundToInt(size * 0.72f)), Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
        }
        // An element badge: the element's icon (its shape tells it apart without the colour) on a dark disc.
        public static void ElementBadge(Rect r, Sigilbound.Core.Element e, Color? ring = null)
        {
            int rad = Mathf.RoundToInt(Mathf.Min(r.width, r.height) / 2);
            Round(r, Color.Lerp(Palette.Element(e), Color.black, 0.6f), ring ?? Palette.Element(e), rad);
            if (Icons.PaintedElement(new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4), e)) return; // element ring stays visible
            float k = Mathf.Min(r.width, r.height) * 0.64f;
            Icons.Draw(new Rect(r.center.x - k / 2, r.center.y - k / 2, k, k), Icons.ForElement(e), Color.Lerp(Palette.Element(e), Color.white, 0.35f));
        }

        // A segmented switch (UX review #7): the options side by side on a solid dark plate, the chosen one
        // gilded. Locked options stay readable with a lock icon. Returns the option tapped (locked ones too, so
        // the caller can say why), or -1.
        public static int Segmented(Rect r, string[] labels, int selected, bool[] open, int size = 14)
        {
            Round(r, Palette.Hex("#0d0a12"), Palette.Trim, 12);
            int n = labels.Length, hit = -1;
            float w = (r.width - 6) / n;
            for (int i = 0; i < n; i++)
            {
                var seg = new Rect(r.x + 3 + i * w, r.y + 3, w, r.height - 6);
                bool on = i == selected, ok = open == null || open[i];
                if (on) { Round(seg, Palette.Hex("#3d3020"), Palette.Gold, 10); GoldRule(new Rect(seg.x + seg.width * 0.2f, seg.yMax - 3, seg.width * 0.6f, 2)); }
                else if (i > 0 && i - 1 != selected) GUI.DrawTexture(new Rect(seg.x, seg.y + 8, 1, seg.height - 16), Solid(Palette.Bronze));
                var c = on ? Palette.Gold : ok ? Palette.Hex("#e6dccb") : Palette.Dim;
                float lw = ok ? 0 : 16;
                if (!ok) Icons.Draw(new Rect(seg.center.x - Label(size, c, FontStyle.Bold).CalcSize(new GUIContent(labels[i])).x / 2 - lw + 2, seg.center.y - 7, 14, 14), Icon.Lock, c);
                Text(new Rect(seg.x + lw / 2, seg.y, seg.width, seg.height), labels[i], size, c, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (Button(seg, "", Kind.Ghost)) hit = i;
            }
            return hit;
        }

        // Press feedback for drawn controls (UX review #8): a soft light overlay while held, and on hover with a mouse.
        public static bool Held(Rect r) => GUI.enabled && Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition) && !Scroller.WasDragging && !(Cover.HasValue && !OnTop && Cover.Value.Contains(Event.current.mousePosition));
        static void PressOverlay(Rect r, bool held)
        {
            bool hover = !held && GUI.enabled && !Application.isMobilePlatform && !Input.GetMouseButton(0) && r.Contains(Event.current.mousePosition);
            if (!held && !hover) return;
            int rad = Mathf.Abs(r.width - r.height) < 1 ? Mathf.RoundToInt(r.width / 2) : Mathf.RoundToInt(Mathf.Min(12, Mathf.Min(r.width, r.height) / 2));
            Round(r, held ? new Color(1f, 0.93f, 0.75f, 0.16f) : new Color(1f, 0.93f, 0.75f, 0.07f), held ? new Color(1f, 0.85f, 0.5f, 0.45f) : null, rad);
        }
        // A pressed painted plate shrinks a little (3%), so the tap reads even on art without a pressed variant.
        static Rect Pressed(Rect r) { float dx = r.width * 0.03f, dy = r.height * 0.05f; return new Rect(r.x + dx, r.y + dy, r.width - 2 * dx, r.height - 2 * dy); }

        // Generated textures and styles, keyed by a UiKey struct (Core/UiCache.cs): no string building, boxing or
        // closures per call (performance review #5). Kinds:
        const int KRound = 1, KGrad = 2, KSolid = 3, KGoldRule = 4, KLabel = 5, KRoundStyle = 6, KGradStyle = 7, KBox = 8, KCard = 9, KButton = 10, KField = 11;
        static readonly Dictionary<UiKey, Texture2D> _tex = new Dictionary<UiKey, Texture2D>();
        static readonly Dictionary<UiKey, GUIStyle> _kstyles = new Dictionary<UiKey, GUIStyle>();
        static readonly Dictionary<string, GUIStyle> _styles = new Dictionary<string, GUIStyle>(); // Style(string, make) for other screens
        static uint Rgba(Color c) => UiKey.Rgba(c.r, c.g, c.b, c.a);
        static uint Rgba(Color? c) => c.HasValue ? Rgba(c.Value) : 0u;
        static int Has(Color? c, int shift) => UiKey.Bits(c.HasValue, shift);
        static Font _font, _serif, _serifBold;
        public static Font Serif => _serif;

        // fullWidth: lay out across the whole screen (battle HUD). Otherwise a ColumnW-wide column,
        // centred in landscape. Call once per OnGUI before drawing.
        public static void Begin(bool fullWidth = false)
        {
            W = fullWidth || !Landscape ? FullW : Mathf.Min(ColumnW, FullW);
            Offset = (FullW - W) / 2f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(Offset * Scale, 0, 0), Quaternion.identity, new Vector3(Scale, Scale, 1));
            if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (_serif == null)
            {
                // The web's display face, bundled. Never Font.CreateDynamicFontFromOSFont: with Unity 6's
                // TextCore-based IMGUI, text in an OS font is not generated, and those controls redraw the
                // previous built-in-font string instead (labels and buttons showing other controls' text).
                _serif = Resources.Load<Font>("Fonts/Cinzel-Regular");
                _serifBold = Resources.Load<Font>("Fonts/Cinzel-Bold");
                if (_serif == null) _serif = _font;
                if (_serifBold == null) _serifBold = _serif;
            }
            GUI.skin.font = _font;
        }

        // Rounded-rect texture, used 9-sliced so corners stay crisp at any size.
        public static Texture2D RoundTex(Color fill, Color? border = null, int radius = 12)
        {
            var key = new UiKey(KRound, Rgba(fill), Rgba(border), x: radius, y: Has(border, 0));
            if (_tex.TryGetValue(key, out var t) && t != null) return t;
            int size = radius * 2 + 4;
            t = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, size - radius), cy = Mathf.Clamp(y + 0.5f, radius, size - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    Color c = fill;
                    if (border.HasValue && d > radius - 1.6f) c = border.Value;
                    c.a *= a;
                    px[y * size + x] = c;
                }
            t.SetPixels(px); t.Apply();
            _tex[key] = t;
            return t;
        }
        // Vertical gradient with rounded corners and an optional border (gilded plates, panels).
        public static Texture2D GradientTex(Color top, Color bottom, Color? border, int radius, Color? midA = null, float midAt = 0.5f, Color? midB = null)
        {
            var key = new UiKey(KGrad, Rgba(top), Rgba(bottom), Rgba(border), Rgba(midA), Rgba(midB), radius,
                Has(border, 0) | Has(midA, 1) | Has(midB, 2) | Mathf.RoundToInt(midAt * 1000) << 3);
            if (_tex.TryGetValue(key, out var t) && t != null) return t;
            int w = radius * 2 + 4, h = Mathf.Max(48, radius * 2 + 4);
            t = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = 1f - y / (h - 1f); // 0 at the top
                // Four stops (top 0, midA 38%, midB 62%, bottom 100%) like the CSS button gradients.
                Color fill = midA.HasValue && midB.HasValue
                    ? (v < 0.38f ? Color.Lerp(top, midA.Value, v / 0.38f) : v < 0.62f ? Color.Lerp(midA.Value, midB.Value, (v - 0.38f) / 0.24f) : Color.Lerp(midB.Value, bottom, (v - 0.62f) / 0.38f))
                    : Color.Lerp(top, bottom, v);
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, radius, w - radius), cy = Mathf.Clamp(y + 0.5f, radius, h - radius);
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(cx, cy));
                    float a = Mathf.Clamp01(radius - d + 0.5f);
                    Color c = fill;
                    if (border.HasValue && (d > radius - 1.6f || x < 1 || x > w - 2 || y < 1 || y > h - 2)) c = border.Value;
                    c.a *= a;
                    px[y * w + x] = c;
                }
            }
            t.SetPixels(px); t.Apply();
            _tex[key] = t;
            return t;
        }

        // Thin gold rule that fades out at both ends (under the top bar, above the nav).
        public static void GoldRule(Rect r)
        {
            var key = new UiKey(KGoldRule);
            if (!_tex.TryGetValue(key, out var t) || t == null)
            {
                t = new Texture2D(64, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
                for (int x = 0; x < 64; x++)
                {
                    float k = Mathf.Abs(x / 63f - 0.5f) * 2; // 0 centre, 1 ends
                    var c = Color.Lerp(Palette.TrimHi, Palette.Trim, Mathf.Clamp01(k * 1.6f));
                    c.a = 1 - Mathf.Clamp01((k - 0.6f) / 0.4f);
                    t.SetPixel(x, 0, c);
                }
                t.Apply(); _tex[key] = t;
            }
            GUI.DrawTexture(r, t);
        }

        public static Texture2D Solid(Color c)
        {
            var key = new UiKey(KSolid, Rgba(c));
            if (_tex.TryGetValue(key, out var t) && t != null) return t;
            t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply();
            _tex[key] = t;
            return t;
        }

        public static void ClearStyles() { _styles.Clear(); _kstyles.Clear(); } // after the skin is switched on or off
        public static GUIStyle Style(string key, System.Func<GUIStyle> make)
        {
            if (!_styles.TryGetValue(key, out var s) || s == null) { s = make(); _styles[key] = s; }
            return s;
        }
        // A rounded rectangle at any size. RoundTex is a small (2r+4 px) texture; stretched over a card it
        // went soft and blurry, with a thick smeared rim (owner report 2026-11-26: results cards, map frame).
        // Larger rects are drawn 9-sliced so the corners and rim stay crisp; small ones and circles as before.
        public static void Round(Rect r, Color fill, Color? border = null, int radius = 12)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tex = RoundTex(fill, border, radius);
            if (Mathf.Min(r.width, r.height) <= radius * 2 + 6) { GUI.DrawTexture(r, tex); return; }
            SliceStyle(KRoundStyle, tex, radius).Draw(r, false, false, false, false);
        }
        // A texture drawn 9-sliced with radius+2 borders (Round, GradientBox).
        static GUIStyle SliceStyle(int kind, Texture2D tex, int radius)
        {
            var key = new UiKey(kind, x: tex.GetInstanceID(), y: radius);
            if (!_kstyles.TryGetValue(key, out var s) || s == null)
                _kstyles[key] = s = new GUIStyle { normal = { background = tex }, border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2) };
            return s;
        }

        static GUIStyle BoxStyle(Color fill, Color? border, int radius) => new GUIStyle
        {
            normal = { background = RoundTex(fill, border, radius) }, border = new RectOffset(radius + 2, radius + 2, radius + 2, radius + 2),
        };

        public static GUIStyle Label(int size = 15, Color? color = null, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool serif = false)
        {
            var c = color ?? Palette.Text;
            var key = new UiKey(KLabel, Rgba(c), x: size, y: (int)fs | (int)align << 4 | UiKey.Bits(wrap, 12) | UiKey.Bits(serif, 13));
            if (!_kstyles.TryGetValue(key, out var s) || s == null)
                _kstyles[key] = s = new GUIStyle
                {
                    // Bold serif uses Cinzel's real bold face rather than a synthesized one.
                    fontSize = size, fontStyle = serif && fs == FontStyle.Bold ? FontStyle.Normal : fs, alignment = align, wordWrap = wrap, richText = true, clipping = TextClipping.Clip,
                    normal = { textColor = c }, font = serif ? (fs == FontStyle.Bold ? _serifBold : _serif) : _font,
                };
            return s;
        }

        // Cinzel only covers Latin text; strings with symbols it lacks (★ ≡ ✕ ◆ …) use the built-in
        // font so nothing renders blank. Cached per string.
        static readonly Dictionary<string, bool> _serifOk = new Dictionary<string, bool>();
        public static bool SerifCanDraw(string s)
        {
            if (string.IsNullOrEmpty(s) || _serif == null || _serif == _font) return true;
            if (_serifOk.TryGetValue(s, out var ok)) return ok;
            ok = true;
            bool tag = false;
            foreach (var ch in s)
            {
                if (ch == '<') tag = true;
                if (!tag && ch > 127 && !_serif.HasCharacter(ch)) { ok = false; break; }
                if (ch == '>') tag = false;
            }
            if (_serifOk.Count > 4000) _serifOk.Clear();
            _serifOk[s] = ok;
            return ok;
        }

        public static void Panel(Rect r, Color? fill = null, Color? border = null, int radius = 14)
        {
            // Painted skin (Skin.cs) for the plain panel; tinted or recoloured panels keep the drawn look.
            if (fill == null && border == null && Skin.DrawFrame(r, "panel")) return;
            var key = new UiKey(KBox, Rgba(fill), Rgba(border), x: radius, y: Has(fill, 0) | Has(border, 1));
            if (!_kstyles.TryGetValue(key, out var st) || st == null) _kstyles[key] = st = BoxStyle(fill ?? Palette.Panel, border ?? Palette.Line, radius);
            GUI.Box(r, GUIContent.none, st);
        }

        // The web's framed ".card": dark violet gradient, bronze edge, gold corner studs.
        public static void Card(Rect r, bool highlight = false)
        {
            if (Skin.DrawFrame(r, highlight ? "card_highlight" : "card")) return;
            var key = new UiKey(KCard, x: highlight ? 1 : 0);
            if (!_kstyles.TryGetValue(key, out var s) || s == null)
                _kstyles[key] = s = new GUIStyle
                {
                    normal = { background = GradientTex(Palette.Hex("#221b2e"), Palette.Hex("#1a1523"), highlight ? Palette.Gold : Palette.Hex("#5c4a30"), 12) },
                    border = new RectOffset(14, 14, 14, 14),
                };
            GUI.Box(r, GUIContent.none, s);
            var stud = RoundTex(Palette.TrimHi, null, 2);
            GUI.DrawTexture(new Rect(r.x + 5, r.y + 5, 4, 4), stud); GUI.DrawTexture(new Rect(r.xMax - 9, r.y + 5, 4, 4), stud);
            GUI.DrawTexture(new Rect(r.x + 5, r.yMax - 9, 4, 4), stud); GUI.DrawTexture(new Rect(r.xMax - 9, r.yMax - 9, 4, 4), stud);
        }

        // A screen title on the owner's painted ribbon (skin "banner"); today's gold heading without it.
        public static void Banner(Rect r, string text, int size = 20)
        {
            var t = Skin.Tex("banner");
            if (t == null) { Heading(r, text, size, TextAnchor.MiddleCenter); return; }
            float h = r.height * 1.3f, w = Mathf.Max(r.width, h * 3.2f);
            var br = new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h);
            if (Event.current.type == EventType.Repaint) Skin.Draw9(br, t, Skin.Border("banner", 40));
            // The ribbon's face sits a little above the middle of the art (the tails hang below).
            var face = new Rect(br.x, br.y + h * 0.06f, br.width, h * 0.62f);
            Text(new Rect(face.x + 1, face.y + 1, face.width, face.height), text, size, new Color(0, 0, 0, 0.6f), FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            Text(face, text, size, Palette.Hex("#f3dfa6"), FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
        }

        // A small card or list row: the owner's bronze frame (skin "card_small") over a dark fill, or the
        // given plain panel without it.
        public static void SmallCard(Rect r, Color fill, Color border, int radius = 10)
        {
            if (Skin.Tex("card_small") != null) { Round(r, new Color(fill.r, fill.g, fill.b, Mathf.Max(fill.a, 0.85f)), null, radius); Skin.DrawFrame(r, "card_small", 0.3f, false); }
            else Panel(r, fill, border, radius);
        }

        // A painted backdrop (skin "bg_*") filling a rect, cropped to fit and darkened for readable text.
        public static bool Backdrop(Rect r, string name, float darken = 0.35f)
        {
            var t = Skin.Tex(name); if (t == null) return false;
            if (Event.current.type != EventType.Repaint) return true;
            GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop);
            if (darken > 0) GUI.DrawTexture(r, Solid(new Color(0.04f, 0.03f, 0.06f, darken)));
            return true;
        }

        // A gradient plate at any size (9-sliced so its corners and rim stay crisp on tall cards).
        public static void GradientBox(Rect r, Color top, Color bottom, Color? border, int radius)
        {
            if (Event.current.type != EventType.Repaint) return;
            var tex = GradientTex(top, bottom, border, radius);
            SliceStyle(KGradStyle, tex, radius).Draw(r, false, false, false, false);
        }

        // Section heading in serif gold (h2 in the web skin).
        public static void Heading(Rect r, string text, int size = 22, TextAnchor align = TextAnchor.MiddleLeft) =>
            Text(r, text, size, Palette.Gold, FontStyle.Bold, align, false, true);

        public static void Bar(Rect r, float pct, Color fill, Color? back = null)
        {
            Round(r, back ?? new Color(0, 0, 0, 0.55f), null, 3);
            if (pct > 0) Round(new Rect(r.x, r.y, r.width * Mathf.Clamp01(pct), r.height), fill, null, 3);
        }

        public enum Kind { Primary, Secondary, Blue, Red, Chip, ChipOn, Ghost }
        static GUIStyle ButtonStyle(Kind k, int size, bool serif = true, bool off = false)
        {
            var key = new UiKey(KButton, x: size, y: (int)k | UiKey.Bits(serif, 4) | UiKey.Bits(off, 5));
            if (!_kstyles.TryGetValue(key, out var st) || st == null) _kstyles[key] = st = MakeButtonStyle(k, size, serif, off);
            return st;
        }
        static GUIStyle MakeButtonStyle(Kind k, int size, bool serif, bool off)
        {
            {
                // Gilded plates as in style.css: gradient stops top / upper-mid / lower-mid / bottom.
                (Color top, Color midA, Color midB, Color bottom, Color text, Color? border) = k switch
                {
                    Kind.Primary => (Palette.Hex("#fde3a0"), Palette.Hex("#e2b14e"), Palette.Hex("#b98330"), Palette.Hex("#d9a64a"), Palette.Hex("#2a1a05"), (Color?)Palette.Hex("#4a3110")),
                    Kind.Blue => (Palette.Hex("#a6d6ff"), Palette.Hex("#4aa3ff"), Palette.Hex("#2563b5"), Palette.Hex("#3f8ae0"), Color.white, Palette.Hex("#0f2c52")),
                    Kind.Red => (Palette.Hex("#ffb0a0"), Palette.Hex("#ff6a5a"), Palette.Hex("#b52a2a"), Palette.Hex("#e0503e"), Color.white, Palette.Hex("#4a0f0f")),
                    Kind.Chip => (Palette.Hex("#2a2236"), Palette.Hex("#241d30"), Palette.Hex("#1f1829"), Palette.Hex("#1b1624"), Palette.Hex("#d8cbb2"), Palette.Hex("#5c4a30")),
                    Kind.ChipOn => (Palette.Hex("#3d3020"), Palette.Hex("#33281b"), Palette.Hex("#2a2117"), Palette.Hex("#241c14"), Palette.Gold, Palette.Gold),
                    Kind.Ghost => (Color.clear, Color.clear, Color.clear, Color.clear, Palette.Text, (Color?)null),
                    _ => (Palette.Hex("#3a2f45"), Palette.Hex("#2f2639"), Palette.Hex("#261e30"), Palette.Hex("#1c1624"), Palette.Hex("#f3e3c3"), Palette.Hex("#6b5432")),
                };
                int rad = k == Kind.Chip || k == Kind.ChipOn ? 14 : 10;
                var s = new GUIStyle
                {
                    normal = { background = k == Kind.Ghost ? null : GradientTex(top, bottom, border, rad, midA, 0.45f, midB) },
                    border = new RectOffset(rad + 2, rad + 2, rad + 2, rad + 2),
                };
                if (k != Kind.Ghost) s.active.background = GradientTex(Color.Lerp(top, Color.black, 0.2f), Color.Lerp(bottom, Color.black, 0.2f), border, rad, Color.Lerp(midA, Color.black, 0.2f), 0.45f, Color.Lerp(midB, Color.black, 0.2f));
                // Painted skin (Skin.cs): Button() draws the plate itself, so the style only carries the text.
                bool painted = k != Kind.Ghost && Skin.Tex(SkinName(k)) != null;
                if (painted || off) { s.normal.background = s.active.background = s.hover.background = null; }
                bool display = serif && k != Kind.Ghost;
                s.fontSize = size; s.fontStyle = display ? FontStyle.Normal : FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter; s.wordWrap = true; s.richText = true;
                if (off) text = k == Kind.Ghost ? Palette.Dim : DisabledText;
                // Pressed: the label darkens a touch on light plates and brightens on dark ones.
                s.normal.textColor = s.hover.textColor = text;
                s.active.textColor = k == Kind.Primary ? Color.Lerp(text, Color.black, 0.25f) : Color.Lerp(text, Color.white, 0.25f);
                if (!painted && !off) s.hover.background = s.normal.background;
                // One padding (UX review #8: the painted plates' 18-unit side padding was set and then always
                // overwritten; it would wrap labels on narrow buttons, so the plates keep 6 at the sides).
                s.padding = new RectOffset(6, 6, 4, 4); s.font = display ? _serifBold : _font;
                return s;
            }
        }

        // Buttons ignore the release that ends a drag-scroll.
        // A panel drawn on top of other buttons (IMGUI gives a tap to whichever button is drawn FIRST, so a
        // button underneath an open panel stole its taps; owner report 2026-11-26, the campaign stage list).
        // While Cover is set, buttons under it ignore input unless they are drawn with OnTop.
        public static Rect? Cover;
        public static bool OnTop;

        public static bool Button(Rect r, string text, Kind k = Kind.Primary, int size = 15, bool enabled = true)
        {
            bool covered = Cover.HasValue && !OnTop && Cover.Value.Contains(Event.current.mousePosition);
            bool serif = SerifCanDraw(text);
            if (!enabled)
            {
                // Disabled: a grey plate with light grey text (4.5:1 or better), clearly off but readable.
                if (Event.current.type == EventType.Repaint)
                {
                    if (k != Kind.Ghost)
                    {
                        var keep = GUI.color;
                        GUI.color = keep * new Color(0.34f, 0.34f, 0.37f, 1f); // the painted plate, dark and greyed: the label stays 4.5:1 even on the brightest gold band
                        if (!Skin.DrawButton(r, SkinName(k), false)) { GUI.color = keep; GradientBox(r, Palette.Hex("#45414c"), DisabledPlate, Palette.Hex("#5a5560"), 10); }
                        GUI.color = keep;
                    }
                    ButtonStyle(k, size, serif, true).Draw(r, new GUIContent(text), false, false, false, false);
                }
                return false;
            }
            var was = GUI.enabled; GUI.enabled = was && !covered;
            bool held = Held(r);
            if (Event.current.type == EventType.Repaint)
            {
                if (k != Kind.Ghost) Skin.DrawButton(held ? Pressed(r) : r, SkinName(k), held);
                else PressOverlay(r, held);
            }
            bool hit = GUI.Button(r, text, ButtonStyle(k, size, serif));
            if (k != Kind.Ghost && !held && Event.current.type == EventType.Repaint) PressOverlay(r, false); // hover only on plates (the press shows on the plate itself)
            GUI.enabled = was;
            hit = hit && !Scroller.WasDragging;
            if (hit) Sound.I?.Play("tap", k == Kind.Ghost ? 0.3f : 0.45f, 1, 0); // every button taps, as the web (drawn ones too)
            return hit;
        }

        // The painted plate per Kind ("button_primary" ...), built once instead of three strings per button per pass.
        static readonly string[] _skinNames = System.Array.ConvertAll((Kind[])System.Enum.GetValues(typeof(Kind)), k => "button_" + k.ToString().ToLowerInvariant());
        static string SkinName(Kind k) => _skinNames[(int)k];

        public static void Text(Rect r, string text, int size = 15, Color? color = null, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool serif = false) =>
            GUI.Label(r, text, Label(size, color, fs, align, wrap, serif && SerifCanDraw(text)));

        // Multi-line text box (save codes, promo codes). Tapping it opens the phone keyboard.
        public static string Field(Rect r, string value, int maxLength, bool multiline)
        {
            var key = new UiKey(KField, x: multiline ? 1 : 0);
            if (!_kstyles.TryGetValue(key, out var st) || st == null)
                _kstyles[key] = st = new GUIStyle(multiline ? GUI.skin.textArea : GUI.skin.textField)
                {
                    fontSize = 13, wordWrap = multiline, padding = new RectOffset(8, 8, 6, 6), font = _font,
                    normal = { background = RoundTex(Palette.Hex("#0d0a12"), Palette.Hex("#5c4a30"), 8), textColor = Palette.Text },
                    focused = { background = RoundTex(Palette.Hex("#120e19"), Palette.Gold, 8), textColor = Palette.Text },
                    border = new RectOffset(10, 10, 10, 10),
                };
            return multiline ? GUI.TextArea(r, value ?? "", maxLength, st) : GUI.TextField(r, value ?? "", maxLength, st);
        }

        public static string Stars(int n) => new string('★', Mathf.Clamp(n, 0, 6));
        public static string Fmt(long n) => n >= 1_000_000 ? (n / 1e6).ToString("0.00") + "M" : n >= 10_000 ? (n / 1e3).ToString("0.0") + "K" : n.ToString("N0");
        public static string ColorTag(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(c)}>{s}</color>";
    }

    // Drag-to-scroll for touch screens (IMGUI scroll views only scroll by wheel/scrollbar).
    public static class Scroller
    {
        public static bool WasDragging;
        static bool _down, _dragging; static Vector2 _start, _last;
        public static float Delta;   // logical units moved this frame (positive = content moves up)

        public static void Tick()
        {
            Delta = 0;
            if (Input.GetMouseButtonDown(0)) { _down = true; _dragging = false; WasDragging = false; _start = _last = Input.mousePosition; }
            if (_down && Input.GetMouseButton(0))
            {
                Vector2 p = Input.mousePosition;
                if (!_dragging && Mathf.Abs(p.y - _start.y) > 10 * Ui.Scale / 2) _dragging = true;
                if (_dragging) { Delta = (p.y - _last.y) / Ui.Scale; WasDragging = true; }
                _last = p;
            }
            if (Input.GetMouseButtonUp(0)) { _down = false; _dragging = false; }
        }

        public static Vector2 Begin(Rect view, Vector2 scroll, float contentHeight)
        {
            if (view.Contains(Event.current.mousePosition))
                scroll.y = Mathf.Clamp(scroll.y + (Event.current.type == EventType.Repaint ? Delta : 0), 0, Mathf.Max(0, contentHeight - view.height));
            return GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 1, Mathf.Max(contentHeight, view.height)), GUIStyle.none, GUIStyle.none);
        }
        public static void End() => GUI.EndScrollView();
    }
}
