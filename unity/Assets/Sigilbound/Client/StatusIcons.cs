// Status icons (owner-approved legend, 2026-11-26) in place of the text tags (DEF+, PSN...) under the
// health bars. Gold rims are buffs, violet rims debuffs; the small number is the turns left. Tapping a
// unit's icons opens the legend. Art: Resources/StatusIcons/<FxType>.png, rendered from the approved page.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class StatusIcons
    {
        static readonly Dictionary<FxType, Texture2D> _tex = new Dictionary<FxType, Texture2D>();
        public static Texture2D Get(FxType t)
        {
            if (!_tex.TryGetValue(t, out var x)) { x = Resources.Load<Texture2D>("StatusIcons/" + t); _tex[t] = x; }
            return x;
        }

        public static readonly (FxType t, string name, string what)[] Legend =
        {
            (FxType.AtkUp, "Attack up", "Deals more damage."), (FxType.DefUp, "Defense up", "Takes less damage."),
            (FxType.SpdUp, "Speed up", "Acts sooner."), (FxType.Regen, "Regeneration", "Heals each turn."),
            (FxType.Shield, "Shield", "Absorbs damage."), (FxType.Taunt, "Taunt", "Enemies must attack it."),
            (FxType.Counter, "Counter", "Strikes back when hit."), (FxType.Revive, "Revive", "Saved once from falling."),
            (FxType.AtkDown, "Attack down", "Deals less damage."), (FxType.DefDown, "Defense down", "Takes more damage."),
            (FxType.Slow, "Slow", "Acts later."), (FxType.Poison, "Poison", "Loses health each turn (stacks up to 3)."),
            (FxType.Burn, "Burn", "Takes fire damage each turn (stacks up to 3)."), (FxType.Stun, "Stun", "Skips its next turn."),
            (FxType.Broken, "Broken", "Weakness break: hit 3 times by the element that beats it, it skips a turn."),
        };

        // Draws the icons left to right from (x, y) (or centred on x when centre), each `size` square with its
        // turns in the corner. Effects without art fall back to their text tag. Returns the area covered.
        public static Rect Row(float x, float y, IList<Effect> effects, float size, bool centre)
        {
            // A stacked effect (Poison/Burn x2/x3) gets extra room on its right for a readable "x3" (the
            // corner badge was too small to read at battle size).
            float gap = 2, sw = size * 1.3f;
            float w = -gap; foreach (var e in effects) w += size + gap + (e.Stacks > 1 ? sw : 0);
            float x0 = centre ? x - w / 2 : x, cx = x0;
            int fs = Mathf.Max(10, Mathf.RoundToInt(size * 0.72f));
            for (int i = 0; i < effects.Count; i++)
            {
                var e = effects[i]; var r = new Rect(cx, y, size, size);
                cx += size + gap + (e.Stacks > 1 ? sw : 0);
                var t = Get(e.Type);
                if (t != null) GUI.DrawTexture(r, t);
                else Ui.Text(r, SkillText.EffectShort(e.Type), 7, FxDef.IsBuff(e.Type) ? Palette.Gold : Palette.Hex("#c07aff"), FontStyle.Bold, TextAnchor.MiddleCenter);
                if (e.Stacks > 1)
                {
                    var sr = new Rect(r.xMax + 1, r.y, sw, size);
                    Ui.Text(new Rect(sr.x + 1, sr.y + 1, sr.width, sr.height), "\u00d7" + e.Stacks, fs, new Color(0, 0, 0, 0.85f), FontStyle.Bold, TextAnchor.MiddleLeft);
                    Ui.Text(sr, "\u00d7" + e.Stacks, fs, new Color(1f, 0.85f, 0.45f), FontStyle.Bold, TextAnchor.MiddleLeft);
                }
                if (e.Turns > 0 && e.Type != FxType.Shield)
                {
                    var n = new Rect(r.xMax - size * 0.42f, r.yMax - size * 0.42f, size * 0.46f, size * 0.46f);
                    Ui.Round(n, new Color(0.07f, 0.05f, 0.09f, 0.95f), FxDef.IsBuff(e.Type) ? Palette.Gold : Palette.Hex("#a05ae0"), 6);
                    Ui.Text(n, e.Turns.ToString(), Mathf.Max(7, Mathf.RoundToInt(size * 0.38f)), Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
            return new Rect(x0, y, Mathf.Max(0, w), size);
        }

        // The legend modal: every icon, its name and what it does, in two columns.
        public static void ShowLegend(App app)
        {
            int rows = (Legend.Length + 1) / 2;
            app.Modal("Status icons", 70 + rows * 40, b =>
            {
                float cw = b.width / 2;
                for (int i = 0; i < Legend.Length; i++)
                {
                    var (t, name, what) = Legend[i];
                    float x = b.x + (i % 2) * cw, y = b.y + (i / 2) * 40;
                    var tex = Get(t); if (tex != null) GUI.DrawTexture(new Rect(x, y, 34, 34), tex);
                    Ui.Text(new Rect(x + 40, y, cw - 44, 18), name, 13, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(x + 40, y + 17, cw - 44, 18), what, 11, Palette.Dim);
                }
                if (Ui.Button(new Rect(b.x, b.yMax - 44, b.width, 42), "Close", Ui.Kind.Secondary)) app.CloseModal();
            });
        }
    }
}
