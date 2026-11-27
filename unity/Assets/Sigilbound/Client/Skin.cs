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

namespace Sigilbound.Client
{
    public static class Skin
    {
        static Dictionary<string, RectOffset> _borders;
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();
        static bool? _any;

        public static bool On
        {
            get => PlayerPrefs.GetInt("sb.skin", 1) == 1;
            set { PlayerPrefs.SetInt("sb.skin", value ? 1 : 0); PlayerPrefs.Save(); Ui.ClearStyles(); }
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
            float k = r.height / t.height, W = t.width, H = t.height;
            float l = b.left * k, rr = b.right * k, tp = b.top * k, bt = b.bottom * k;
            if (l + rr > r.width) { float s = r.width / (l + rr); l *= s; rr *= s; }
            float[] dx = { r.x, r.x + l, r.xMax - rr, r.xMax }, dy = { r.y, r.y + tp, r.yMax - bt, r.yMax };
            float[] u = { 0, b.left / W, 1 - b.right / W, 1 }, v = { 1, 1 - b.top / H, b.bottom / H, 0 }; // GUI y down, UV y up
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    GUI.DrawTextureWithTexCoords(dst, t, new Rect(u[i], v[j + 1], u[i + 1] - u[i], v[j] - v[j + 1]));
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
            float k = Mathf.Min(maxK, Mathf.Min(r.width / (b.left + b.right), r.height / (b.top + b.bottom)) * 0.85f);
            float pad = around ? Mathf.Min(b.left, b.top) * k * 0.55f : 0f;
            r = new Rect(r.x - pad, r.y - pad, r.width + 2 * pad, r.height + 2 * pad);
            float W = t.width, H = t.height;
            float l = b.left * k, rr = b.right * k, tp = b.top * k, bt = b.bottom * k;
            float[] dx = { r.x, r.x + l, r.xMax - rr, r.xMax }, dy = { r.y, r.y + tp, r.yMax - bt, r.yMax };
            float[] u = { 0, b.left / W, 1 - b.right / W, 1 }, v = { 1, 1 - b.top / H, b.bottom / H, 0 };
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 3; j++)
                {
                    var dst = new Rect(dx[i], dy[j], dx[i + 1] - dx[i], dy[j + 1] - dy[j]);
                    if (dst.width <= 0 || dst.height <= 0) continue;
                    var keep = GUI.color;
                    if (i == 1 && j == 1) GUI.color = keep * new Color(0.62f, 0.62f, 0.66f, 1f); // a darker centre so text reads
                    GUI.DrawTextureWithTexCoords(dst, t, new Rect(u[i], v[j + 1], u[i + 1] - u[i], v[j] - v[j + 1]));
                    GUI.color = keep;
                }
            return true;
        }

        // A painted button: plate (pressed variant while held) plus its centre ornament. False if no art.
        public static bool DrawButton(Rect r, string name, bool pressed)
        {
            var t = Tex(name); if (t == null) return false;
            var tp = pressed ? Tex(name + "_pressed") : null;
            Draw9(r, tp ?? t, Border(tp != null ? name + "_pressed" : name, 24));
            var orn = (tp != null ? Tex(name + "_pressed_orn") : null) ?? Tex(name + "_orn");
            if (orn != null) { float w = orn.width * r.height / orn.height; GUI.DrawTexture(new Rect(r.center.x - w / 2, r.y, w, r.height), orn); }
            return true;
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
            return _borders.TryGetValue(name, out var o) ? o : new RectOffset(fallback, fallback, fallback, fallback);
        }
    }
}
