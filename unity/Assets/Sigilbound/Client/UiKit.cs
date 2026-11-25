// Reusable widgets for the landscape screens, all in the same original visual language: dark
// violet panels, gilded double frames with corner studs, element colours, drawn icons (Icons.cs),
// and code-built hero portraits (no images). Everything draws inside the rect it is given, so a
// label can never land outside its button.
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static partial class Ui
    {
        // ---------- frames ----------
        // A gilded frame: dark gradient panel, gold outer edge, bronze inner edge and corner studs.
        public static void Frame(Rect r, Color? accent = null, bool bright = false)
        {
            var a = accent ?? (bright ? Palette.Gold : Palette.Trim);
            var s = Style($"frame{ColorUtility.ToHtmlStringRGBA(a)}", () => new GUIStyle
            {
                normal = { background = GradientTex(Palette.Hex("#241c31"), Palette.Hex("#130f1b"), a, 10) },
                border = new RectOffset(12, 12, 12, 12),
            });
            GUI.Box(r, GUIContent.none, s);
            if (Event.current.type != EventType.Repaint) return;
            GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.width - 8, 1), Solid(new Color(a.r, a.g, a.b, 0.35f)));
            GUI.DrawTexture(new Rect(r.x + 4, r.yMax - 5, r.width - 8, 1), Solid(new Color(0, 0, 0, 0.4f)));
            foreach (var p in new[] { new Vector2(r.x + 3, r.y + 3), new Vector2(r.xMax - 9, r.y + 3), new Vector2(r.x + 3, r.yMax - 9), new Vector2(r.xMax - 9, r.yMax - 9) })
            {
                GUI.DrawTexture(new Rect(p.x, p.y, 6, 6), RoundTex(Palette.TrimLo, null, 3));
                GUI.DrawTexture(new Rect(p.x + 1, p.y + 1, 4, 4), RoundTex(Palette.TrimHi, null, 2));
            }
        }

        // A title plate: a short gilded bar with the title in display type (card headers).
        public static void TitlePlate(Rect r, string title, int size = 15, Color? tint = null)
        {
            GUI.DrawTexture(r, GradientTex(Palette.Hex("#2c2238"), Palette.Hex("#1a1423"), null, 4));
            GoldRule(new Rect(r.x, r.yMax - 1, r.width, 1));
            Text(r, title, size, tint ?? Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
        }

        // Darkened full-width strip (top bars, HUD bands).
        public static void Band(Rect r, float alpha = 0.7f) => GUI.DrawTexture(r, Solid(new Color(0.05f, 0.04f, 0.07f, alpha)));

        // ---------- buttons ----------
        // Button with a drawn icon and an optional label, both laid out inside the button.
        public static bool IconButton(Rect r, Icon icon, string label = null, Kind k = Kind.Secondary, int size = 13, bool enabled = true, Color? iconColor = null)
        {
            bool hit = Button(r, "", k, size, enabled);
            var col = iconColor ?? (k == Kind.Primary ? Palette.Hex("#2a1a05") : k == Kind.ChipOn ? Palette.Gold : Palette.TrimHi);
            if (!enabled) col.a *= 0.4f;
            float s = Mathf.Min(r.height - 12, 22);
            if (string.IsNullOrEmpty(label)) Icons.Draw(new Rect(r.center.x - s / 2, r.center.y - s / 2, s, s), icon, col);
            else
            {
                var lab = Label(size, col, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                float tw = Mathf.Min(lab.CalcSize(new GUIContent(label)).x, r.width - s - 18);
                float x = r.center.x - (s + 6 + tw) / 2;
                Icons.Draw(new Rect(x, r.center.y - s / 2, s, s), icon, col);
                GUI.Label(new Rect(x + s + 6, r.y, tw + 2, r.height), label, lab);
            }
            return hit;
        }
        public static bool BackButton(Rect r, string label = "Back") => IconButton(r, Icon.Back, label, Kind.Secondary, 13);

        // Round corner button (battle Auto / speed / pause), with an optional caption under the icon.
        public static bool RoundButton(Rect r, Icon icon, bool on, string caption = null)
        {
            var bg = on ? Palette.Hex("#3d3020") : Palette.Hex("#1b1624");
            var st = Style($"round{on}", () => new GUIStyle { normal = { background = RoundTex(bg, on ? Palette.Gold : Palette.Trim, 20) }, active = { background = RoundTex(Color.Lerp(bg, Color.black, 0.3f), Palette.Gold, 20) }, border = new RectOffset(22, 22, 22, 22) });
            bool hit = GUI.Button(r, GUIContent.none, st) && !Scroller.WasDragging;
            if (hit) Sound.I?.Play("tap", 0.45f, 1, 0);
            float s = r.height * (caption == null ? 0.5f : 0.42f);
            Icons.Draw(new Rect(r.center.x - s / 2, r.y + (caption == null ? (r.height - s) / 2 : r.height * 0.14f), s, s), icon, on ? Palette.Gold : Palette.TrimHi);
            if (caption != null) Text(new Rect(r.x, r.yMax - r.height * 0.42f, r.width, r.height * 0.34f), caption, 10, on ? Palette.Gold : Palette.Dim, FontStyle.Bold, TextAnchor.MiddleCenter);
            return hit;
        }

        // Segmented switch (Normal / Hard, filters). Returns the selected index.
        public static int Tabs(Rect r, string[] labels, int sel, bool[] enabled = null, int size = 13)
        {
            float w = r.width / labels.Length;
            for (int i = 0; i < labels.Length; i++)
                if (Button(new Rect(r.x + i * w + (i > 0 ? 2 : 0), r.y, w - 2, r.height), labels[i], i == sel ? Kind.ChipOn : Kind.Chip, size, enabled == null || enabled[i])) sel = i;
            return sel;
        }

        // ---------- small pieces ----------
        public static void Chip(Rect r, Icon icon, string text, Color c)
        {
            Panel(r, Palette.Hex("#0d0a12"), Palette.Bronze, Mathf.Min(13, (int)(r.height / 2)));
            float s = r.height - 10;
            Icons.Draw(new Rect(r.x + 6, r.y + 5, s, s), icon, c);
            Text(new Rect(r.x + s + 8, r.y, r.width - s - 12, r.height), text, 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
        }
        public static float ChipWidth(string text) => Mathf.Max(56, text.Length * 7.2f + 34);

        public static void StarRow(Rect r, int n, int max = 3, Color? on = null, Color? off = null, TextAnchor align = TextAnchor.MiddleCenter)
        {
            float s = Mathf.Min(r.height, r.width / Mathf.Max(1, max)), total = s * max;
            float x = align == TextAnchor.MiddleLeft ? r.x : align == TextAnchor.MiddleRight ? r.xMax - total : r.center.x - total / 2;
            for (int i = 0; i < max; i++)
                Icons.Draw(new Rect(x + i * s, r.center.y - s / 2, s, s), i < n ? Icon.Star : Icon.StarEmpty, i < n ? on ?? Palette.Gold : off ?? new Color(1, 1, 1, 0.3f));
        }

        public static void ElementBadge(Rect r, Element e)
        {
            var c = Palette.Element(e);
            GUI.DrawTexture(r, RoundTex(Color.Lerp(c, Color.black, 0.45f), c, Mathf.Max(2, (int)(r.width / 2))));
            float p = r.width * 0.2f;
            Icons.Draw(new Rect(r.x + p, r.y + p, r.width - 2 * p, r.height - 2 * p), Icons.ForElement(e), Color.Lerp(c, Color.white, 0.55f));
        }

        public static void LevelBadge(Rect r, int level, Color? edge = null)
        {
            GUI.DrawTexture(r, RoundTex(Palette.Hex("#0d0a12"), edge ?? Palette.Trim, Mathf.Max(2, (int)(r.height / 2) - 1)));
            Text(r, level.ToString(), Mathf.Clamp((int)(r.height * 0.62f), 8, 14), Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        // Slim HP bar with an optional shield overlay (battle units).
        public static void HpBar(Rect r, float pct, Color fill, float shieldPct = 0)
        {
            GUI.DrawTexture(new Rect(r.x - 1, r.y - 1, r.width + 2, r.height + 2), Solid(new Color(0, 0, 0, 0.75f)));
            GUI.DrawTexture(r, Solid(new Color(0.18f, 0.06f, 0.06f, 0.9f)));
            if (pct > 0) GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(pct), r.height), Solid(fill));
            if (shieldPct > 0) GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(shieldPct), Mathf.Max(1, r.height * 0.4f)), Solid(new Color(0.75f, 0.9f, 1f)));
        }

        // Ribbon banner across a rect (VICTORY / DEFEAT, card tags).
        public static void Ribbon(Rect r, string text, Color c, int size = 14)
        {
            GUI.DrawTexture(r, GradientTex(Color.Lerp(c, Color.white, 0.25f), Color.Lerp(c, Color.black, 0.35f), Color.Lerp(c, Color.black, 0.6f), 4));
            Text(r, text, size, Color.Lerp(c, Color.black, 0.8f), FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
        }

        // ---------- code-built hero portrait ----------
        // Rarity frame, an element-tinted backdrop with a radiating sigil, the hero's monogram in
        // display type, element badge, level badge and stars. Original art, built from shapes only.
        public static void Portrait(Rect r, HeroDef t, Element el, int level, int stars, bool selected = false, bool dim = false, string tag = null)
        {
            if (t == null) return;
            var ec = Palette.Element(el); var rc = Palette.Rarity(t.Rarity);
            var col = GUI.color; if (dim) GUI.color = new Color(col.r * 0.55f, col.g * 0.55f, col.b * 0.55f, col.a);
            GUI.DrawTexture(r, GradientTex(Color.Lerp(ec, Color.black, 0.35f), Color.Lerp(ec, Palette.Hex("#0d0a12"), 0.82f), selected ? Palette.Gold : rc, 6));
            float s = Mathf.Min(r.width, r.height);
            var sig = new Rect(r.center.x - s * 0.42f, r.y + r.height * 0.12f, s * 0.84f, s * 0.84f);
            Icons.Draw(sig, Icon.Portal, new Color(1, 1, 1, 0.08f));
            Icons.Draw(new Rect(sig.center.x - s * 0.22f, sig.center.y - s * 0.22f, s * 0.44f, s * 0.44f), Icons.ForElement(el), new Color(ec.r, ec.g, ec.b, 0.22f));
            string mono = t.ShortName.Length > 0 ? t.ShortName.Substring(0, 1).ToUpperInvariant() : "?";
            Text(new Rect(r.x, r.y + r.height * 0.08f, r.width, r.height * 0.62f), mono, Mathf.Clamp((int)(s * 0.42f), 10, 40), new Color(1f, 0.95f, 0.85f, 0.92f), FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            Text(new Rect(r.x + 2, r.yMax - r.height * 0.36f, r.width - 4, r.height * 0.18f), t.ShortName, Mathf.Clamp((int)(s * 0.15f), 8, 12), Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (stars > 0) StarRow(new Rect(r.x + 3, r.yMax - r.height * 0.17f, r.width - 6, r.height * 0.13f), stars, Mathf.Max(stars, 1), Palette.Gold);
            float b = Mathf.Clamp(s * 0.26f, 12, 22);
            ElementBadge(new Rect(r.x + 3, r.y + 3, b, b), el);
            if (level > 0) LevelBadge(new Rect(r.xMax - b * 1.35f - 3, r.y + 3, b * 1.35f, b), level, rc);
            if (!string.IsNullOrEmpty(tag)) Ribbon(new Rect(r.x + 4, r.y + r.height * 0.44f, r.width - 8, Mathf.Clamp(s * 0.18f, 12, 18)), tag, Palette.Gold, Mathf.Clamp((int)(s * 0.12f), 8, 11));
            GUI.color = col;
            if (selected) GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), RoundTex(Color.clear, Palette.Gold, 8));
        }
    }
}
