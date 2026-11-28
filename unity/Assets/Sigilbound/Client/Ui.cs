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
        // Settings > Accessibility > Larger tap targets makes it 56 dp.
        public static float Tap => Mathf.Max(44f, Dp(Accessibility.TapDp(Access.LargeTaps)));
        // The safe area (camera hole, rounded corners) inset by a gutter on the left and right, in layout units.
        // Screens laid out across the whole width use it for their content; bars use it for their contents.
        public static Rect SafeContent(float gutter = 0)
        {
            var s = Safe;
            float x0 = Mathf.Max(Left, s.x) + gutter, x1 = Mathf.Min(Left + FullW, s.xMax) - gutter;
            return new Rect(x0, s.y, Mathf.Max(0, x1 - x0), s.height);
        }

        // ---------- shared screen pieces (UI/UX consistency review: fix once, every screen follows) ----------
        // Narrow landscape (4:3 tablets at 640 units, a 1024x768 window at 800): the wide screens stack or drop
        // columns (Core/UiLayout.cs holds the arithmetic, checked at 640, 1067 and 1422 units in Tests~).
        public static bool Compact => Landscape && UiLayout.Compact(FullW);
        public static Box ToBox(Rect r) => new Box(r.x, r.y, r.width, r.height);
        public static Rect ToRect(Box b) => new Rect(b.X, b.Y, b.W, b.H);

        // The one Back button (design rule 2): Tap tall, a drawn arrow and the word Back (no "‹" glyph), the
        // Secondary plate. It returns to where the player came from (App.GoBack keeps the trail).
        public static float BackW => Mathf.Max(UiLayout.BackW, Tap);
        public static bool BackButton(Rect r)
        {
            bool hit = Button(r, "", Kind.Secondary);
            float s = Mathf.Min(18, r.height - 16), tw = Label(14, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, true).CalcSize(new GUIContent("Back")).x;
            float x = r.center.x - (s + 6 + tw) / 2;
            Icons.Draw(new Rect(x, r.center.y - s / 2, s, s), Icon.Back, Palette.Hex("#f3e3c3"));
            Text(new Rect(x + s + 6, r.y, r.xMax - x - s - 8, r.height), "Back", 14, Palette.Hex("#f3e3c3"), FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            return hit;
        }
        // A sub-screen's header (rule 2): the Back button and the screen's title on one Tap-tall row, the title in
        // gold serif on a dark pill (it sits over the 3D scene). Tab roots draw a ribbon Banner instead. Returns true
        // when Back was tapped. `right` reserves room at the row's end for the screen's own tools.
        public static bool ScreenHeader(Rect row, string title, float right = 0)
        {
            float t = Mathf.Max(row.height, Tap);
            var back = new Rect(row.x, row.y, BackW, t);
            bool hit = BackButton(back);
            if (!string.IsNullOrEmpty(title))
            {
                var tr = new Rect(back.xMax + 14, row.y, Mathf.Max(0, row.xMax - right - back.xMax - 22), t);
                int sz = FitSize(title, tr.width - 20, 18, 13, true);
                TextOnArt(tr, title, sz, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            }
            return hit;
        }

        // A row of buttons by role (rule 3): Secondary and Danger in the order given, the Primary last (right),
        // sized together and wrapping upward when the row is too narrow (UiLayout.ButtonRow). Returns the index of
        // the button tapped, or -1. ButtonRowHeight says how tall the row will be.
        public static float ButtonRowHeight(float width, int n) => UiLayout.RowHeight(width, n, 10, 120, Mathf.Max(44, Tap));
        public static int ButtonRow(Rect r, IList<(string label, UiRole role, bool enabled)> buttons, int size = 15)
        {
            var roles = new UiRole[buttons.Count];
            for (int i = 0; i < roles.Length; i++) roles[i] = buttons[i].role;
            var boxes = UiLayout.ButtonRow(ToBox(r), roles, Mathf.Max(44, Tap));
            int hit = -1;
            for (int i = 0; i < boxes.Length; i++)
                if (Button(ToRect(boxes[i]), buttons[i].label, Of(buttons[i].role), size, buttons[i].enabled) && hit < 0) hit = i;
            return hit;
        }

        // A locked card or row (rule 5): opaque, the readable disabled text colours, a lock icon, the reason in
        // words ("Clear stage 1-6 to unlock") and, when known, the progress toward it. Never faded with GUI.color.
        // Returns true when tapped (the caller toasts the reason).
        public static bool LockedCard(Rect r, string title, string reason, float progress = -1)
        {
            Round(r, Palette.Panel, Palette.Hex("#5a5560"), 12);
            float s = Mathf.Min(22, r.height - 20);
            Icons.Draw(new Rect(r.x + 14, r.y + 12, s, s), Icon.Lock, DisabledText);
            Text(new Rect(r.x + 20 + s, r.y + 8, r.width - 34 - s, 24), title, 15, DisabledText, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Text(new Rect(r.x + 20 + s, r.y + 32, r.width - 34 - s, Mathf.Max(18, r.height - 40 - (progress >= 0 ? 10 : 0))), reason, 12, DisabledText, FontStyle.Normal, TextAnchor.UpperLeft, true);
            if (progress >= 0) Bar(new Rect(r.x + 20 + s, r.yMax - 12, r.width - 34 - s, 5), progress, Palette.Dim);
            return Button(r, "", Kind.Ghost);
        }

        // The one count badge (rule: one red "something to collect" badge): a red disc with the number, anchored
        // by its centre. Counts of tries or runs left are plain text in their card instead.
        public static void Badge(Vector2 centre, int n)
        {
            if (n <= 0) return;
            string t = n > 99 ? "99+" : n.ToString();
            float w = Mathf.Max(20, 10 + t.Length * 8);
            var b = new Rect(centre.x - w / 2, centre.y - 10, w, 20);
            Round(b, Palette.Bad, Palette.Hex("#2a0d0d"), 10);
            Text(b, t, 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
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
                int px = TextPx(r, text, size, fs, align, false, false);
                _gc.text = text; float w = Mathf.Min(r.width, RawLabel(px, color, fs, align).CalcSize(_gc).x + 24);
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
            Round(r, Palette.Well, Palette.Trim, 12);
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
        const int KRound = 1, KGrad = 2, KSolid = 3, KGoldRule = 4, KLabel = 5, KRoundStyle = 6, KGradStyle = 7, KBox = 8, KCard = 9, KButton = 10, KField = 11, KPlain = 12;
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

        public static void ClearStyles() { _styles.Clear(); _kstyles.Clear(); _fit.Clear(); } // after the skin is switched on or off
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

        // ---------- text size (Settings > Accessibility; Core/Accessibility.cs) ----------
        // Every size written in the draw code is the Default-setting size. Sized() is what it is drawn at for
        // the player's setting (with the minimum readable size), and every text helper here goes through it:
        // Label (styles other screens measure with), Text, TextOnArt, Heading, Banner, Button, Field, FitSize.
        // Nothing is drawn under 11 points (design rule 11; the 9- and 10-point labels were about 9 sp on a phone).
        public const int MinText = 11;
        public static int Sized(int size) { size = Mathf.Max(MinText, size); return Accessibility.TextSize(size, Access.TextLevel); }
        // How much larger than Default the player's text is (1 at Default and Small): layouts that can grow
        // (scrolling lists such as Settings) multiply their row heights by it.
        public static float TextGrow => Mathf.Max(1f, Access.TextFactor);
        // The size text written at `size` is drawn at inside rect r: Sized(size), or, when that doesn't fit the
        // rect (one line: its width and line height; wrapped: its height), the largest size that does, never
        // below the Default size. Layouts that can't grow therefore shrink to fit rather than overflow.
        // Measured on Repaint only and cached per string and rect.
        public static int TextPx(Rect r, string text, int size, FontStyle fs, TextAnchor align, bool wrap, bool serif)
        {
            size = Mathf.Max(MinText, size);
            int level = Access.TextLevel, top = Accessibility.TextSize(size, level), floor = Accessibility.FitFloor(size, level);
            if (top <= floor || string.IsNullOrEmpty(text) || Event.current == null || Event.current.type != EventType.Repaint) return top;
            var key = new FitKey(text, r.width, r.height, size, (int)fs | (int)align << 4 | (wrap ? 1 << 12 : 0) | (serif ? 1 << 13 : 0) | level << 14);
            if (_fit.TryGetValue(key, out int px)) return px;
            _gc.text = text;
            for (px = top; px > floor; px--)
            {
                var st = RawLabel(px, null, fs, align, wrap, serif);
                if (wrap ? st.CalcHeight(_gc, r.width) <= r.height + 2 : st.CalcSize(_gc).x <= r.width + 1 && st.lineHeight <= r.height + 3) break;
            }
            if (_fit.Count > 3000) _fit.Clear();
            return _fit[key] = px;
        }
        // How far a button's label keeps from its sides when fitting: the style's 6-unit padding, plus the painted
        // plates' ornamented ends (about 0.3 of the height, at most 18) on every kind but Ghost.
        public static float ButtonInset(float height, Kind k = Kind.Primary) => k == Kind.Ghost ? 6 : 6 + Mathf.Min(18, height * 0.3f);
        // The same for a button's label (side inset as above, 4-unit top/bottom padding; labels wrap at spaces).
        static int ButtonPx(Rect r, string text, int size, bool serif, Kind k)
        {
            size = Mathf.Max(MinText, size);
            int level = Access.TextLevel, top = Accessibility.TextSize(size, level), floor = Accessibility.FitFloor(size, level);
            if (top <= floor || string.IsNullOrEmpty(text) || Event.current == null || Event.current.type != EventType.Repaint) return top;
            var key = new FitKey(text, r.width, r.height, size, 1 << 20 | (serif ? 1 << 13 : 0) | level << 14 | (k == Kind.Ghost ? 1 << 21 : 0));
            if (_fit.TryGetValue(key, out int px)) return px;
            _gc.text = text; float w = r.width - 2 * ButtonInset(r.height, k), h = r.height - 8; bool words = text.IndexOf(' ') >= 0 || text.IndexOf('\n') >= 0;
            for (px = top; px > floor; px--)
            {
                var st = RawLabel(px, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, serif);
                var one = st.CalcSize(_gc);
                if (text.IndexOf('\n') < 0 && one.x <= w + 1 && st.lineHeight <= h + 4) break;
                if (words && RawLabel(px, null, FontStyle.Bold, TextAnchor.MiddleCenter, true, serif).CalcHeight(_gc, w) <= h + 4) break;
            }
            if (_fit.Count > 3000) _fit.Clear();
            return _fit[key] = px;
        }
        readonly struct FitKey : System.IEquatable<FitKey>
        {
            readonly string _t; readonly int _w, _h, _s, _f;
            public FitKey(string t, float w, float h, int s, int f) { _t = t; _w = Mathf.RoundToInt(w * 4); _h = Mathf.RoundToInt(h * 4); _s = s; _f = f; }
            public bool Equals(FitKey o) => _w == o._w && _h == o._h && _s == o._s && _f == o._f && string.Equals(_t, o._t);
            public override bool Equals(object o) => o is FitKey k && Equals(k);
            public override int GetHashCode() => ((_t.GetHashCode() * 31 + _w) * 31 + _h) * 31 + (_s | _f << 8);
        }
        static readonly Dictionary<FitKey, int> _fit = new Dictionary<FitKey, int>();
        static readonly GUIContent _gc = new GUIContent();

        // A label style at the player's text size (other screens measure text with it).
        public static GUIStyle Label(int size = 15, Color? color = null, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool serif = false) =>
            RawLabel(Sized(size), color, fs, align, wrap, serif);
        // A label style at exactly px (already scaled).
        static GUIStyle RawLabel(int px, Color? color = null, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool serif = false)
        {
            var c = color ?? Palette.Text;
            var key = new UiKey(KLabel, Rgba(c), x: px, y: (int)fs | (int)align << 4 | UiKey.Bits(wrap, 12) | UiKey.Bits(serif, 13));
            if (!_kstyles.TryGetValue(key, out var s) || s == null)
                _kstyles[key] = s = new GUIStyle
                {
                    // Bold serif uses Cinzel's real bold face rather than a synthesized one.
                    fontSize = px, fontStyle = serif && fs == FontStyle.Bold ? FontStyle.Normal : fs, alignment = align, wordWrap = wrap, richText = true, clipping = TextClipping.Clip,
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
                    normal = { background = GradientTex(Palette.PanelHi, Palette.PanelLo, highlight ? Palette.Gold : Palette.Hex("#5c4a30"), 12) },
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

        // Three button roles (UI/UX consistency review, design rule 3): Primary (one per screen or modal, last in a
        // row), Secondary, Danger (destructive or stop actions only). Chip / ChipOn are toggles and filters (ChipOn is
        // the one "selected" style, rule 4); Ghost is an invisible tap area over drawn content.
        public enum Kind { Primary, Secondary, Danger, Chip, ChipOn, Ghost }
        public static Kind Of(UiRole r) => r == UiRole.Primary ? Kind.Primary : r == UiRole.Danger ? Kind.Danger : Kind.Secondary;
        static GUIStyle ButtonStyle(Kind k, int size, bool serif = true, bool off = false, bool textOnly = false)
        {
            var key = new UiKey(KButton, x: size, y: (int)k | UiKey.Bits(serif, 4) | UiKey.Bits(off, 5) | UiKey.Bits(textOnly, 6));
            if (!_kstyles.TryGetValue(key, out var st) || st == null) _kstyles[key] = st = MakeButtonStyle(k, size, serif, off || textOnly, off);
            return st;
        }
        // noPlate: the style carries only the label (the plate is drawn separately); off: the disabled label colours.
        static GUIStyle MakeButtonStyle(Kind k, int size, bool serif, bool noPlate, bool off)
        {
            {
                // Gilded plates as in style.css: gradient stops top / upper-mid / lower-mid / bottom.
                (Color top, Color midA, Color midB, Color bottom, Color text, Color? border) = k switch
                {
                    Kind.Primary => (Palette.Hex("#fde3a0"), Palette.Hex("#e2b14e"), Palette.Hex("#b98330"), Palette.Hex("#d9a64a"), Palette.Hex("#2a1a05"), (Color?)Palette.Hex("#4a3110")),
                    Kind.Danger => (Palette.Hex("#ffb0a0"), Palette.Hex("#ff6a5a"), Palette.Hex("#b52a2a"), Palette.Hex("#e0503e"), Color.white, Palette.Hex("#4a0f0f")),
                    Kind.Chip => (Palette.ChipHi, Palette.ChipMid, Palette.ChipLo, Palette.Panel, Palette.Hex("#d8cbb2"), Palette.Hex("#5c4a30")),
                    Kind.ChipOn => (Palette.Hex("#3d3020"), Palette.Hex("#33281b"), Palette.Hex("#2a2117"), Palette.Hex("#241c14"), Palette.Gold, Palette.Gold),
                    Kind.Ghost => (Color.clear, Color.clear, Color.clear, Color.clear, Palette.Text, (Color?)null),
                    _ => (Palette.ButtonHi, Palette.ButtonMid, Palette.ButtonLo, Palette.ButtonEdge, Palette.Hex("#f3e3c3"), Palette.Hex("#6b5432")),
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
                if (painted || noPlate) { s.normal.background = s.active.background = s.hover.background = null; }
                bool display = serif && k != Kind.Ghost;
                s.fontSize = size; s.fontStyle = display ? FontStyle.Normal : FontStyle.Bold; s.alignment = TextAnchor.MiddleCenter; s.wordWrap = true; s.richText = true;
                if (off) text = k == Kind.Ghost ? Palette.Dim : DisabledText;
                // Pressed: the label darkens a touch on light plates and brightens on dark ones.
                s.normal.textColor = s.hover.textColor = text;
                s.active.textColor = k == Kind.Primary ? Color.Lerp(text, Color.black, 0.25f) : Color.Lerp(text, Color.white, 0.25f);
                if (!painted && !noPlate) s.hover.background = s.normal.background;
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

        // Every button taps over at least Ui.Tap on each side (design rule 1): a smaller plate keeps its drawn size
        // and gets a larger, centred hit rect, so the ~45 hand-sized buttons on older screens all reach 48 dp.
        public static Rect HitRect(Rect r)
        {
            float t = Tap, dw = Mathf.Max(0, t - r.width), dh = Mathf.Max(0, t - r.height);
            return dw <= 0 && dh <= 0 ? r : new Rect(r.x - dw / 2, r.y - dh / 2, r.width + dw, r.height + dh);
        }

        // Labels never draw symbol glyphs (design rule 10: the fonts on the owner's devices lacked them, and a
        // glyph Cinzel lacks switched the whole label to the sans face). A trailing " ▶" or "★" in a label is drawn
        // as Icon.Next / Icon.Star beside the words instead.
        static bool TailIcon(ref string text, out Icon icon)
        {
            icon = Icon.Next;
            if (string.IsNullOrEmpty(text)) return false;
            if (text.EndsWith(" \u25B6", System.StringComparison.Ordinal)) { text = text.Substring(0, text.Length - 2); return true; }
            if (text[text.Length - 1] == '\u2605') { text = text.Substring(0, text.Length - 1); icon = Icon.Star; return true; }
            return false;
        }

        public static bool Button(Rect r, string text, Kind k = Kind.Primary, int size = 15, bool enabled = true)
        {
            var hitR = HitRect(r);
            bool covered = Cover.HasValue && !OnTop && Cover.Value.Contains(Event.current.mousePosition);
            bool tail = TailIcon(ref text, out var tailIcon);
            float iconS = tail ? Mathf.Min(16, r.height * 0.36f) : 0;
            var lr = tail ? new Rect(r.x, r.y, r.width - iconS - 4, r.height) : r; // the label's room, beside the icon
            bool serif = SerifCanDraw(text);
            int px = ButtonPx(lr, text, size, serif && k != Kind.Ghost, k); // the player's text size, shrunk to fit the button (Ghost labels use the sans face)
            if (tail && Event.current.type == EventType.Repaint)
            {
                var st = ButtonStyle(k, px, serif, false, true);
                float tw = Mathf.Min(lr.width - 2 * ButtonInset(r.height, k), st.CalcSize(new GUIContent(text)).x - 12);
                float x0 = r.center.x - (tw + 4 + iconS) / 2;
                lr = new Rect(x0 - 6, r.y, tw + 12, r.height);
                Color ic = !enabled ? DisabledText : k == Kind.Primary ? Palette.Hex("#2a1a05") : k == Kind.ChipOn ? Palette.Gold : Palette.Hex("#f3e3c3");
                var icr = new Rect(x0 + tw + 4, r.center.y - iconS / 2, iconS, iconS);
                // Drawn after the plate below; remember it for then.
                _tailRect = icr; _tailIcon = tailIcon; _tailColor = ic;
            }
            else _tailRect = Rect.zero;
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
                    ButtonStyle(k, px, serif, true).Draw(lr, new GUIContent(text), false, false, false, false);
                    DrawTail();
                }
                return false;
            }
            var was = GUI.enabled; GUI.enabled = was && !covered;
            bool held = Held(hitR);
            if (Event.current.type == EventType.Repaint)
            {
                if (k != Kind.Ghost)
                {
                    var plate = held ? Pressed(r) : r;
                    bool painted = Skin.DrawButton(plate, SkinName(k), held);
                    if (!painted) ButtonStyle(k, px, serif).Draw(plate, GUIContent.none, false, held, false, false);
                    if (k == Kind.ChipOn) SelectedMark(plate, painted);
                }
                else PressOverlay(r, held);
                if (!string.IsNullOrEmpty(text)) ButtonStyle(k, px, serif, false, true).Draw(lr, new GUIContent(text), false, held, false, false);
                DrawTail();
            }
            bool hit = GUI.Button(hitR, GUIContent.none, GUIStyle.none);
            if (k != Kind.Ghost && !held && Event.current.type == EventType.Repaint) PressOverlay(r, false); // hover only on plates (the press shows on the plate itself)
            GUI.enabled = was;
            hit = hit && !Scroller.WasDragging;
            if (hit) Sound.I?.Play("tap", k == Kind.Ghost ? 0.3f : 0.45f, 1, 0); // every button taps, as the web (drawn ones too)
            return hit;
        }

        // A button's plate with no input (cards that take the "selected" look under their own tap area).
        public static void Plate(Rect r, Kind k)
        {
            if (Event.current.type != EventType.Repaint || k == Kind.Ghost) return;
            bool painted = Skin.DrawButton(r, SkinName(k), false);
            if (!painted) ButtonStyle(k, 15).Draw(r, GUIContent.none, false, false, false, false);
            if (k == Kind.ChipOn) SelectedMark(r, painted);
        }

        static Rect _tailRect; static Icon _tailIcon; static Color _tailColor;
        static void DrawTail() { if (_tailRect.width > 0) Icons.Draw(_tailRect, _tailIcon, _tailColor); _tailRect = Rect.zero; }

        // The one "selected" look (design rule 4): a gold rim and a light gold wash over the plate (the painted
        // chip_on art alone differed from the chip only by its label colour), a gold underline, and a check badge on
        // the corner, so "on" never reads by colour alone.
        static void SelectedMark(Rect r, bool painted)
        {
            int rad = Mathf.RoundToInt(Mathf.Min(14, Mathf.Min(r.width, r.height) / 2));
            Round(r, new Color(1f, 0.86f, 0.45f, painted ? 0.16f : 0.08f), Palette.Gold, rad);
            if (r.width >= 30) Round(new Rect(r.x + r.width * 0.22f, r.yMax - 6, r.width * 0.56f, 3), Palette.Gold, null, 1);
            if (r.width >= 40 && r.height >= 24)
            {
                var c = new Rect(r.xMax - 13, r.y - 4, 15, 15);
                Round(c, Palette.Gold, Palette.Hex("#3d3020"), 7);
                Icons.Draw(new Rect(c.x + 3, c.y + 3, 9, 9), Icon.Check, Palette.Hex("#1a1208"));
            }
        }

        // The painted plate per Kind, in Kind order (Danger keeps the red plate's art name), built once.
        static readonly string[] _skinNames = { "button_primary", "button_secondary", "button_red", "button_chip", "button_chipon", "button_ghost" };
        static string SkinName(Kind k) => _skinNames[(int)k];

        // All screen text goes through here: the player's text size, shrunk to fit r if it has to (TextPx).
        public static void Text(Rect r, string text, int size = 15, Color? color = null, FontStyle fs = FontStyle.Normal, TextAnchor align = TextAnchor.MiddleLeft, bool wrap = false, bool serif = false)
        {
            bool sf = serif && SerifCanDraw(text);
            GUI.Label(r, text, RawLabel(TextPx(r, text, size, fs, align, wrap, sf), color.HasValue ? Readable(color.Value) : (Color?)null, fs, align, wrap, sf));
        }

        // A text colour lifted to 4.5:1 on the dark panel when it falls short (Core TextColour: the colour-blind
        // palettes had element and rarity colours at 3.2:1 as text). Dark label colours for gold plates stay as they
        // are; translucent colours (shadows) too. Cached per colour.
        static readonly Dictionary<uint, Color> _readable = new Dictionary<uint, Color>();
        public static Color Readable(Color c)
        {
            if (c.a < 0.99f) return c;
            uint key = Rgba(c);
            if (_readable.TryGetValue(key, out var o)) return o;
            var bg = Palette.Panel;
            var (r, g, b) = TextColour.Readable(c.r, c.g, c.b, bg.r, bg.g, bg.b);
            o = new Color(r, g, b, c.a);
            if (_readable.Count > 512) _readable.Clear();
            return _readable[key] = o;
        }

        // A one-line label that must fit its cell (the bottom nav: at Extra large, "HEROESSUMMON" ran together): the
        // player's text size, shrunk as far as the 11-point floor (not the Default floor of Text), then the narrower
        // sans face if the serif still doesn't fit.
        public static void FitText(Rect r, string text, int size, Color c, bool serif = true)
        {
            bool sf = serif && SerifCanDraw(text);
            _gc.text = text;
            int top = Sized(size), px = top;
            for (; px > MinText; px--) if (RawLabel(px, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, sf).CalcSize(_gc).x <= r.width - 4) break;
            if (sf && RawLabel(px, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, true).CalcSize(_gc).x > r.width - 4)
            {
                sf = false;
                for (px = top; px > MinText; px--) if (RawLabel(px, null, FontStyle.Bold, TextAnchor.MiddleCenter, false, false).CalcSize(_gc).x <= r.width - 4) break;
            }
            GUI.Label(r, text, RawLabel(px, Readable(c), FontStyle.Bold, TextAnchor.MiddleCenter, false, sf));
        }

        // Text the player typed (friend names, chat): no rich-text tags, otherwise as Text (scaled, fitted).
        public static void PlainText(Rect r, string text, int size, Color c, FontStyle fs = FontStyle.Normal, TextAnchor a = TextAnchor.MiddleLeft, bool wrap = false)
        {
            c = Readable(c);
            int px = TextPx(r, text, size, fs, a, wrap, false);
            var key = new UiKey(KPlain, Rgba(c), x: px, y: (int)fs | (int)a << 4 | UiKey.Bits(wrap, 12));
            if (!_kstyles.TryGetValue(key, out var st) || st == null) _kstyles[key] = st = new GUIStyle(RawLabel(px, c, fs, a, wrap)) { richText = false };
            GUI.Label(r, text, st);
        }

        // Multi-line text box (save codes, promo codes). Tapping it opens the phone keyboard.
        public static string Field(Rect r, string value, int maxLength, bool multiline)
        {
            var key = new UiKey(KField, x: multiline ? 1 : 0, y: Sized(13));
            if (!_kstyles.TryGetValue(key, out var st) || st == null)
                _kstyles[key] = st = new GUIStyle(multiline ? GUI.skin.textArea : GUI.skin.textField)
                {
                    fontSize = Sized(13), wordWrap = multiline, padding = new RectOffset(8, 8, 6, 6), font = _font,
                    normal = { background = RoundTex(Palette.Well, Palette.Hex("#5c4a30"), 8), textColor = Palette.Text },
                    focused = { background = RoundTex(Palette.WellFocus, Palette.Gold, 8), textColor = Palette.Text },
                    border = new RectOffset(10, 10, 10, 10),
                };
            return multiline ? GUI.TextArea(r, value ?? "", maxLength, st) : GUI.TextField(r, value ?? "", maxLength, st);
        }

        public static string Stars(int n) => new string('★', Mathf.Clamp(n, 0, 6));
        // Counts read "12,345" up to 99,999 (the K step came at 10,000: UI/UX review #24), then "123.4K", "1.23M".
        public static string Fmt(long n) => n >= 1_000_000 ? (n / 1e6).ToString("0.00") + "M" : n >= 100_000 ? (n / 1e3).ToString("0.0") + "K" : n.ToString("N0");
        public static string ColorTag(string s, Color c) => $"<color=#{ColorUtility.ToHtmlStringRGB(Readable(c))}>{s}</color>";
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
