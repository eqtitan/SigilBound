using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Sigilbound.Core;

// Creature heroes (Core/Kit/Creatures.cs): each is a hero, ships a high-detail model WITH its own clips, has
// the bones its spec names (body, every leg chain and tip), and every clip its style or aliases ask for.
static class CreatureTests
{
    public static void Run(System.Action<bool, string> check)
    {
        string dir = System.AppContext.BaseDirectory, hd = null;
        for (int i = 0; i < 8 && dir != null && hd == null; i++, dir = Path.GetDirectoryName(dir))
        { var p0 = Path.Combine(dir, "Tools~", "models", "heroes_hd"); if (Directory.Exists(p0)) hd = p0; }
        // Menu and gallery lineups give wide heroes room (in-house report: the new heroes overlapped on gallery page 39).
        {
            var plain = Creatures.LineupXs(new[] { "pell", "odo", "wren", "mira" });
            var wide = Creatures.LineupXs(new[] { "wystan", "crusk", "hartwin", "nyssa" });
            var pair = Creatures.LineupXs(new[] { "vorgrim", "talvo" });
            bool plainSame = System.Math.Abs(plain[0] + 2.1) < 1e-9 && System.Math.Abs(plain[3] - 2.1) < 1e-9 && System.Math.Abs(plain[1] + 0.7) < 1e-9;
            bool widerGaps = Enumerable.Range(1, 3).All(k => wide[k] - wide[k - 1] > 1.6) && wide[3] - wide[0] <= Creatures.MaxLineupSpan + 1e-9 && System.Math.Abs(wide[0] + wide[3]) < 1e-9;
            bool everyNew = new[] { "vorgrim", "talvo", "wystan", "crusk", "hartwin", "nyssa", "vessa" }.All(id => Creatures.LineupWidth(id) > 1.1) && Creatures.LineupWidth("pell") == 1 && Creatures.WideHeroes.Keys.All(id => GameData.Hero(id) != null);
            check(plainSame && widerGaps && pair[1] - pair[0] > 1.7 && everyNew,
                $"lineup: normal heroes keep 1.4 m gaps (4.2 m for four); wide new heroes get {wide[1] - wide[0]:0.00}-{wide[3] - wide[2]:0.00} m, Vorgrim-Talvo {pair[1] - pair[0]:0.00} m, within {Creatures.MaxLineupSpan} m");
        }
        foreach (var spec in Creatures.Table.Values)
        {
            check(GameData.Heroes.Any(h => h.Id == spec.Id), $"creature {spec.Id}: is a hero");
            check(spec.Style != null && spec.Scale > 0 && spec.SlotWidth >= 1 && spec.MeleeReach >= 0, $"creature {spec.Id}: style, scale, slot width and reach set");
            if (hd == null) continue;
            var glb = Path.Combine(hd, $"SB_Hero_{spec.Id}_HD.glb");
            check(File.Exists(glb), $"creature {spec.Id}: high-detail model present");
            if (!File.Exists(glb)) continue;
            var b = File.ReadAllBytes(glb); int n = System.BitConverter.ToInt32(b, 12);
            using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(b, 20, n));
            var nodes = new HashSet<string>(doc.RootElement.GetProperty("nodes").EnumerateArray().Select(x => x.TryGetProperty("name", out var v) ? v.GetString() : ""));
            var clips = doc.RootElement.TryGetProperty("animations", out var an) ? new HashSet<string>(an.EnumerateArray().Select(x => x.GetProperty("name").GetString())) : new HashSet<string>();
            var bones = new[] { spec.BodyBone }.Concat(spec.Legs.SelectMany(l => l)).ToList();
            var missingBones = bones.Where(x => !nodes.Contains(x)).ToList();
            check(missingBones.Count == 0, $"creature {spec.Id}: {bones.Count} rig bones present (body, {spec.Legs.Length} legs with tips)" + (missingBones.Count > 0 ? " — missing: " + string.Join(", ", missingBones.Take(5)) : ""));
            var s = spec.Style;
            var want = new[] { s.Idle, s.Cast, s.Support, s.Ult, s.Win, s.Defeat }.Concat(s.Strike).Concat(s.Hit).Concat(s.BigHit).Concat(s.Death).Concat(spec.Aliases.Values)
                .Where(x => !string.IsNullOrEmpty(x)).Distinct().ToList();
            var missingClips = want.Where(x => !clips.Contains(x)).ToList();
            check(missingClips.Count == 0, $"creature {spec.Id}: its own {clips.Count} clips cover style and aliases" + (missingClips.Count > 0 ? " — missing: " + string.Join(", ", missingClips) : ""));
            var ult = HeroUlts.ClipFor(spec.Id);
            check(ult == null || spec.Aliases.ContainsKey(ult) || clips.Contains(ult), $"creature {spec.Id}: its ultimate ({ult}) plays one of its own clips");
        }
    }
}
