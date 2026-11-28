// Painted UI skin (owner request: Grok Imagine art for the interface). Optional images in
// Resources/Skin/ replace the code-drawn plates when present; anything missing keeps today's look.
// Each image is stretched as a 9-slice: Resources/Skin/skin.txt lists "name left top right bottom"
// (border pixels that never stretch). Names: button_primary, button_primary_pressed (and
// _secondary, _blue, _red, _chip, _chipon), panel, card, card_highlight, banner. A button may also
// have NAME_orn: its centre ornament, drawn unstretched over the middle (Tools~/skin/button.py).
// Buttons scale their borders with the button's height (DrawButton), so hi-res art fits any size.
// The player can turn the skin off in Settings (PlayerPrefs, not the save, like fps and shake).
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Skin
    {
        static Dictionary<string, RectOffset> _borders;
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static bool? _any;

        // Read from PlayerPrefs once, not on every Tex() call (dozens per GUI pass; a native call that can
        // cross JNI on Android; code review M5).
        static bool? _on;
        public static bool On
        {
            get => _on ??= PlayerPrefs.GetInt("sb.skin", 1) == 1;
            set { _on = value; PlayerPrefs.SetInt("sb.skin", value ? 1 : 0); PlayerPrefs.Save(); Ui.ClearStyles(); }
        }

        // True when at least one skin image ships (the Settings row only shows then).
        public static bool Any => _any ??= Resources.LoadAll<Texture2D>("Skin").Length > 0;

        public static Texture2D Tex(string name)
        {
            if (!On) return null;
            if (!_tex.TryGetValue(name, out var t)) { t = Resources.Load<Texture2D>("Skin/" + name); _tex[name] = t; }
            return t;
        }

        // 9-slice scaled so the texture's full height maps to r.height (borders keep their proportions).
        public static void Draw9(Rect r, Texture2D t, RectOffset b)
        {
            float k = r.height / t.height;
            float l = b.left * k, rr = b.right * k, tp = b.top * k, bt = b.bottom * k;
            if (l + rr > r.width) { float s = r.width / (l + rr); l *= s; rr *= s; }
            var n = NineSlice.Of(r.x, r.y, r.width, r.height, l, rr, tp, bt, t.width, t.height, b.left, b.right, b.top, b.bottom); // no per-draw arrays
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(n.X(i), n.Y(j), n.X(i + 1) - n.X(i), n.Y(j + 1) - n.Y(j));
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(dst, t, new Rect(n.U(i), n.V(j + 1), n.U(i + 1) - n.U(i), n.V(j) - n.V(j + 1)));
                }
        }

        // A painted frame (panel, card): borders at texture size, shrunk on small rects so the ornate
        // corners never overlap (a 60 px card would otherwise fold its 76 px corners over each other).
        public static bool DrawFrame(Rect r, string name) => DrawFrame(r, name, 0.5f, true);
        // maxK: largest scale of the painted borders; around: grow the rect so the frame sits around the
        // content (big panels). Small stacked rows pass around = false and a smaller maxK, so their frames
        // stay inside their own rect and never overlap the next row.
        public static bool DrawFrame(Rect r, string name, float maxK, bool around)
        {
            var t = Tex(name); if (t == null) return false;
            if (Event.current.type != EventType.Repaint) return true;
            var b = Border(name, 24);
            // Half the painted size at most (owner check 2026-11-27: at full size the gilded edges covered
            // titles and the first letters of labels), and the frame sits around the content, not on it:
            // the rect grows by most of the band so the inner edge of the gold lines up with the content edge.
            float k = UiLayout.FrameScale(r.width, r.height, Border9(name), maxK); // Core arithmetic, so Tests~ measures the same content rect (L028)
            float pad = around ? Mathf.Min(b.left, b.top) * k * 0.55f : 0f;
            r = new Rect(r.x - pad, r.y - pad, r.width + 2 * pad, r.height + 2 * pad);
            float l = b.left * k, rr = b.right * k, tp = b.top * k, bt = b.bottom * k;
            var n = NineSlice.Of(r.x, r.y, r.width, r.height, l, rr, tp, bt, t.width, t.height, b.left, b.right, b.top, b.bottom);
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(n.X(i), n.Y(j), n.X(i + 1) - n.X(i), n.Y(j + 1) - n.Y(j));
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    var keep = GUI.color;
                    if (i == 1 && j == 1) GUI.color = keep * new Color(0.62f, 0.62f, 0.66f, 1f); // a darker centre so text reads
                    GUI.DrawTextureWithTexCoords(dst, t, new Rect(n.U(i), n.V(j + 1), n.U(i + 1) - n.U(i), n.V(j) - n.V(j + 1)));
                    GUI.color = keep;
                }
            return true;
        }

        // A painted button: plate (pressed variant while held) plus its centre ornament. False if no art.
        // The variant names of a plate ("_pressed", "_pressed_orn", "_orn"), built once per plate, not per draw.
        static readonly Dictionary<string, (string pressed, string pressedOrn, string orn)> _variants = new Dictionary<string, (string, string, string)>();
        static (string pressed, string pressedOrn, string orn) Variants(string name)
        {
            if (!_variants.TryGetValue(name, out var v)) _variants[name] = v = (name + "_pressed", name + "_pressed_orn", name + "_orn");
            return v;
        }
        public static bool DrawButton(Rect r, string name, bool pressed)
        {
            var t = Tex(name); if (t == null) return false;
            var vn = Variants(name);
            var tp = pressed ? Tex(vn.pressed) : null;
            Draw9(r, tp ?? t, Border(tp != null ? vn.pressed : name, 24));
            var orn = (tp != null ? Tex(vn.pressedOrn) : null) ?? Tex(vn.orn);
            if (orn != null) { float w = orn.width * r.height / orn.height; GUI.DrawTexture(new Rect(r.center.x - w / 2, r.y, w, r.height), orn); }
            return true;
        }

        // The frame's borders for Core/UiLayout (FrameScale, FrameInner), cached per name.
        static readonly Dictionary<string, UiLayout.Border9> _b9 = new Dictionary<string, UiLayout.Border9>();
        public static UiLayout.Border9 Border9(string name)
        {
            if (!_b9.TryGetValue(name, out var v)) { var o = Border(name, 24); _b9[name] = v = new UiLayout.Border9(o.left, o.top, o.right, o.bottom); }
            return v;
        }

        public static RectOffset Border(string name, int fallback)
        {
            if (_borders == null)
            {
                _borders = new Dictionary<string, RectOffset>();
                var spec = Resources.Load<TextAsset>("Skin/skin");
                if (spec != null)
                    foreach (var line in spec.text.Split('\n'))
                    {
                        var p = line.Trim().Split(' ');
                        if (p.Length == 5 && int.TryParse(p[1], out var l) && int.TryParse(p[2], out var tp) && int.TryParse(p[3], out var r) && int.TryParse(p[4], out var b))
                            _borders[p[0]] = new RectOffset(l, r, tp, b);
                    }
            }
            if (!_borders.TryGetValue(name, out var o)) _borders[name] = o = new RectOffset(fallback, fallback, fallback, fallback); // once, not per call
            return o;
        }
    }
}
