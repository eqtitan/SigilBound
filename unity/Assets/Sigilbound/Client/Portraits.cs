// Painted hero card portraits (owner request: Grok Imagine art). Optional: Resources/Portraits/portrait_<id>
// (made with Tools~/skin/portrait.py). Heroes without one keep today's look everywhere.
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class Portraits
    {
        static readonly Dictionary<string, Texture2D> _tex = new Dictionary<string, Texture2D>();

        public static Texture2D Get(string heroId)
        {
            if (string.IsNullOrEmpty(heroId)) return null;
            if (!_tex.TryGetValue(heroId, out var t)) { t = Resources.Load<Texture2D>("Portraits/portrait_" + heroId); _tex[heroId] = t; }
            return t;
        }

        // The face: a square from the top-middle of the 3:4 portrait, in a rarity-coloured frame.
        public static bool Thumb(Rect r, string heroId, Color frame)
        {
            var t = Get(heroId); if (t == null) return false;
            GUI.DrawTexture(new Rect(r.x - 2, r.y - 2, r.width + 4, r.height + 4), Ui.RoundTex(frame, null, 6));
            GUI.DrawTextureWithTexCoords(r, t, new Rect(0.12f, 0.38f, 0.76f, 0.57f));
            return true;
        }

        // The whole card (3:4), framed.
        public static bool Card(Rect r, string heroId, Color frame)
        {
            var t = Get(heroId); if (t == null) return false;
            GUI.DrawTexture(new Rect(r.x - 3, r.y - 3, r.width + 6, r.height + 6), Ui.RoundTex(frame, null, 10));
            GUI.DrawTexture(r, t, ScaleMode.ScaleAndCrop);
            return true;
        }
    }
}
