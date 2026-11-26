// Painted UI skin (owner request: Grok Imagine art for the interface). Optional images in
// Resources/Skin/ replace the code-drawn plates when present; anything missing keeps today's look.
// Each image is stretched as a 9-slice: Resources/Skin/skin.txt lists "name left top right bottom"
// (border pixels that never stretch). Names: button_primary, button_primary_pressed (and
// _secondary, _blue, _red, _chip, _chipon), panel, card, card_highlight, banner.
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
