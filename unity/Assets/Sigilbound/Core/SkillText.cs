// Human-readable skill and stat descriptions (generated from data, so always accurate).
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public static class SkillText
    {
        public static string Describe(SkillDef sk)
        {
            string tgt = sk.Target switch { Target.Enemy => "one enemy", Target.AllEnemies => "all enemies", Target.Ally => "one ally", Target.AllAllies => "all allies", _ => "self" };
            var parts = new List<string>();
            if (sk.Mult > 0)
            {
                string scale = sk.Scale == Scale.Atk ? "ATK" : sk.Scale == Scale.Def ? "DEF" : "Max HP";
                parts.Add($"Hits {tgt}{(sk.Hits > 1 ? $" {sk.Hits}×" : "")} for {sk.Mult * 100:0}% {scale}");
            }
            else parts.Add($"Targets {tgt}");
            if (sk.Pierce) parts.Add("pierces to the enemy behind (50%)");
            foreach (var f in sk.Fx) parts.Add(Fx(f));
            if (sk.Execute > 0) parts.Add($"executes below {sk.Execute * 100:0}% HP (not bosses)");
            if (sk.Spread) parts.Add("on a kill, Poison and Burn spread to the next enemy");
            if (sk.Rage > 0) parts.Add($"up to +{sk.Rage * 100:0}% damage as the attacker loses HP");
            return string.Join(" · ", parts);
        }

        static string Fx(FxDef f)
        {
            string ch = f.Chance >= 0 ? $"{f.Chance * 100:0}% " : "";
            string who = f.To == FxTo.Self ? " on self" : "";
            return f.Type switch
            {
                FxType.Heal => $"heals {f.Pct * 100:0}% HP{who}",
                FxType.Tm => $"fills Turn Meter by {f.Val * 100:0}%",
                FxType.TmDrain => $"{ch}drains {f.Val * 100:0}% Turn Meter",
                FxType.Pull => $"{ch}takes {f.Val * 100:0}% Turn Meter",
                FxType.Revive => $"{ch}Revive ({f.Turns}t): on falling, back at {f.Pct * 100:0}% HP{who}",
                FxType.Cleanse => "removes debuffs",
                FxType.Strip => "removes buffs",
                FxType.Shield => $"Shield = {f.Pct * 100:0}% of caster HP{who}",
                _ => $"{ch}{EffectName(f.Type)} ({f.Turns}t){who}",
            };
        }

        public static string EffectName(FxType t) => t switch
        {
            FxType.AtkUp => "ATK Up", FxType.DefUp => "DEF Up", FxType.SpdUp => "SPD Up", FxType.AtkDown => "ATK Down",
            FxType.DefDown => "DEF Down", FxType.Slow => "Slow", FxType.Poison => "Poison", FxType.Stun => "Stun",
            FxType.Regen => "Regen", FxType.Shield => "Shield", FxType.Taunt => "Taunt",
            FxType.Burn => "Burn", FxType.Counter => "Counter", FxType.Revive => "Revive", _ => t.ToString(),
        };
        public static string EffectShort(FxType t) => t switch
        {
            FxType.AtkUp => "ATK+", FxType.DefUp => "DEF+", FxType.SpdUp => "SPD+", FxType.AtkDown => "ATK-",
            FxType.DefDown => "DEF-", FxType.Slow => "SLW", FxType.Poison => "PSN", FxType.Stun => "STN",
            FxType.Regen => "RGN", FxType.Shield => "SHD", FxType.Taunt => "TNT",
            FxType.Burn => "BRN", FxType.Counter => "CTR", FxType.Revive => "RVV", _ => "",
        };

        // Short captions for the battle skill buttons (UX review #5: they read "A1 / A2 / A3"). A name that fits
        // stays whole; otherwise the longest run of whole words from the start that fits, else from the end
        // ("Radiant Judgment" -> "Radiant", "Storm of Blades" -> "Blades"), else cut with an ellipsis. Captions
        // stay distinct within one hero's skills.
        public const int ShortMax = 11;
        public static string[] ShortNames(IList<SkillDef> skills, int max = ShortMax)
        {
            var outp = new string[skills.Count];
            for (int i = 0; i < skills.Count; i++)
            {
                var cands = Candidates(skills[i].Name ?? "", max);
                string pick = cands[0];
                foreach (var c in cands)
                {
                    bool clash = false;
                    for (int j = 0; j < i; j++) if (outp[j] == c) clash = true;
                    if (!clash) { pick = c; break; }
                }
                outp[i] = pick;
            }
            return outp;
        }
        public static string Short(string name, int max = ShortMax) => Candidates(name ?? "", max)[0];

        static List<string> Candidates(string name, int max)
        {
            var res = new List<string>();
            name = name.Trim();
            if (name.Length <= max) { res.Add(name); return res; }
            var words = name.Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries);
            bool Minor(string w) => w == "of" || w == "the" || w == "and" || w == "a" || w == "to";
            // Leading words (dropping a dangling "of"/"the"), then trailing words.
            for (int n = words.Length - 1; n >= 1; n--)
            {
                int k = n; while (k > 1 && Minor(words[k - 1].ToLowerInvariant())) k--;
                var head = string.Join(" ", words, 0, k);
                if (head.Length <= max && !Minor(head.ToLowerInvariant()) && !res.Contains(head)) { res.Add(head); break; }
            }
            for (int n = 1; n < words.Length; n++)
            {
                var tail = string.Join(" ", words, n, words.Length - n);
                if (tail.Length <= max && !Minor(words[n].ToLowerInvariant()) && !res.Contains(tail)) { res.Add(tail); break; }
            }
            var cut = name.Substring(0, System.Math.Max(1, max - 1)).TrimEnd() + "\u2026";
            if (!res.Contains(cut)) res.Add(cut);
            return res;
        }

        // A hero's passive, or null (Unity-only roster heroes).
        public static string Passive(HeroDef h) =>
            h != null && h.FallHeal > 0 ? $"Last Light: when an ally falls, heals all allies {h.FallHeal * 100:0}% HP" : null;

        public static string StatName(GameData.Stat s, int v) => s switch
        {
            GameData.Stat.AtkPct => $"ATK +{v}%", GameData.Stat.HpPct => $"HP +{v}%", GameData.Stat.DefPct => $"DEF +{v}%",
            GameData.Stat.SpdPct => $"SPD +{v}%", GameData.Stat.Crit => $"Crit +{v}%", GameData.Stat.Cdmg => $"Crit DMG +{v}%",
            _ => $"{s.ToString().ToUpper()} +{v}",
        };
    }
}
