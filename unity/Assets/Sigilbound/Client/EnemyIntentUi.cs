// Enemy intent over each foe (UX review #5/#9, PLAN P2-15): a small chip saying what it will do on its next
// turn (attack, AoE, heal, buff, debuff, ultimate ready, or stunned), the target's name when the move hits one
// unit, and a tap (or press and hold) for the skill's name and description. Driven by Core's
// Battle.PreviewAction, which is exactly what the AI will pick if its turn came now; the chips refresh whenever
// the battle changes (after every action), not every frame.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public sealed class EnemyIntentUi
    {
        readonly Dictionary<Unit, Intent> _cache = new Dictionary<Unit, Intent>();
        long _stamp = long.MinValue;

        // A cheap fingerprint of everything a preview reads (HP, effects, cooldowns, who is alive, the wave):
        // the previews are recomputed only when it changes. No allocation.
        static long Stamp(Battle b)
        {
            long h = b.Wave * 7919L + (b.Over ? 1 : 0);
            h = Mix(h, b.Allies); return Mix(h, b.Enemies);
        }
        static long Mix(long h, List<Unit> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                h = h * 31 + u.Hp; h = h * 31 + (u.Alive ? 1 : 0); h = h * 31 + u.Effects.Count;
                for (int k = 0; k < u.Effects.Count; k++) h = h * 17 + (int)u.Effects[k].Type * 5 + u.Effects[k].Turns;
                for (int k = 0; k < u.Cds.Length; k++) h = h * 13 + u.Cds[k];
            }
            return h;
        }

        public Intent Of(Battle b, Unit u)
        {
            long s = Stamp(b);
            if (s != _stamp) { _stamp = s; _cache.Clear(); }
            if (!_cache.TryGetValue(u, out var it)) _cache[u] = it = b.PreviewAction(u);
            return it;
        }
        public void Reset() { _cache.Clear(); _stamp = long.MinValue; }

        public static Icon IconOf(IntentKind k) => k switch
        {
            IntentKind.Attack => Icon.Sword, IntentKind.AoE => Icon.Sword, IntentKind.Heal => Icon.Heart,
            IntentKind.Buff => Icon.Plus, IntentKind.Debuff => Icon.Minus, IntentKind.Ultimate => Icon.Crown,
            IntentKind.Stunned => Icon.Clock, _ => Icon.Info,
        };
        public static Color ColourOf(IntentKind k) => k switch
        {
            IntentKind.Attack => Palette.Hex("#ff7a6a"), IntentKind.AoE => Palette.Hex("#ff9a3a"), IntentKind.Heal => Palette.Good,
            IntentKind.Buff => Palette.Blue, IntentKind.Debuff => Palette.Hex("#d8c84a"), IntentKind.Ultimate => Palette.Gold,
            _ => Palette.Dim,
        };
        public static string WordOf(IntentKind k) => k switch
        {
            IntentKind.Attack => "Attack", IntentKind.AoE => "Attacks all", IntentKind.Heal => "Heal", IntentKind.Buff => "Buff",
            IntentKind.Debuff => "Debuff", IntentKind.Ultimate => "Ultimate ready", IntentKind.Stunned => "Stunned", _ => "",
        };

        // The chip, left-aligned at (x, y), 22 tall. Returns true when tapped (the caller opens the details).
        public static bool Chip(float x, float y, Intent it, bool tappable)
        {
            if (it == null || it.Kind == IntentKind.None) return false;
            var col = ColourOf(it.Kind);
            string hint = it.Kind == IntentKind.Stunned ? "Zz" : it.Kind == IntentKind.AoE || (it.Kind == IntentKind.Ultimate && it.SkillDef?.Target == Target.AllEnemies) ? "ALL"
                : it.Target != null ? "› " + (it.Target.Ally ? it.Target.Template.ShortName : it.Target.Name) : it.Kind == IntentKind.Ultimate ? "ULT" : "";
            if (hint.Length > 12) hint = hint.Substring(0, 12);
            float w = 26 + (hint.Length > 0 ? 6 + hint.Length * 6.2f : 0);
            var r = new Rect(x, y, w, 22);
            bool ult = it.Kind == IntentKind.Ultimate;
            float pulse = ult ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 5f) : 1f;
            Ui.Round(r, new Color(0.05f, 0.06f, 0.07f, 0.86f), col * new Color(1, 1, 1, pulse), 8);
            Icons.Draw(new Rect(r.x + 5, r.y + 3, 16, 16), IconOf(it.Kind), col);
            if (it.Debuffs && it.Kind != IntentKind.Debuff) // the debuff mark: a small amber minus on the icon's corner
            {
                var m = new Rect(r.x + 15, r.y + 11, 11, 11);
                Ui.Round(m, Palette.Hex("#2a2410"), ColourOf(IntentKind.Debuff), 5);
                Icons.Draw(new Rect(m.x + 1.5f, m.y + 1.5f, 8, 8), Icon.Minus, ColourOf(IntentKind.Debuff));
            }
            if (hint.Length > 0) Ui.Text(new Rect(r.x + 26, r.y, w - 28, r.height), hint, 11, it.Target != null && it.Target.Ally ? Palette.Hex("#ffd0c0") : Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (!tappable) return false;
            // A 48 dp invisible hit box around the chip (the chip itself is 22 tall).
            var hit = new Rect(r.x - 4, r.center.y - Ui.Tap / 2, Mathf.Max(r.width + 8, Ui.Tap), Ui.Tap);
            return Ui.Button(hit, "", Ui.Kind.Ghost);
        }

        // The details modal: what it will do, at whom, and the skill's own text.
        public static void ShowDetails(App app, Intent it)
        {
            if (it == null || it.Kind == IntentKind.None) return;
            var sk = it.SkillDef;
            string who = it.Unit.Ally ? it.Unit.Template.ShortName : it.Unit.Name;
            string head = it.Kind == IntentKind.Stunned ? $"{who} loses its next turn." : $"{WordOf(it.Kind)}{(it.Kind == IntentKind.Ultimate ? " · " + WordOf(it.Does) : "")}" + (it.Target != null ? $" → {(it.Target.Ally ? it.Target.Template.ShortName : it.Target.Name)}" : "");
            string body = sk == null ? "" : SkillText.Describe(sk);
            app.InfoModal(sk != null ? $"{who}: {sk.Name}" : who, 254, b =>
            {
                Icons.Draw(new Rect(b.x, b.y + 2, 28, 28), IconOf(it.Kind), ColourOf(it.Kind));
                Ui.Text(new Rect(b.x + 36, b.y, b.width - 36, 32), head, 15, ColourOf(it.Kind), FontStyle.Bold, TextAnchor.MiddleLeft, true);
                Ui.Text(new Rect(b.x, b.y + 40, b.width, b.height - 96), body + "\n\nIts next move if nothing changes first: heroes acting before it can change its mind.", 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (app.ModalFooter(b, "Close")) app.CloseModal(); // the shared info-modal footer, 48 dp
            });
        }
    }
}
