// Checks for the growing roster (docs/sigilbound/ROSTER.md): the original 24 stay the web game's
// heroes (Classic, which also feeds every enemy pool), every hero has a look and a baked free-path
// model, roster heroes appear in ROSTER.md, and each new hero is balanced against the classic hero
// of the same rarity and role (team swap sims).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class RosterTests
{
    static string RepoFile(params string[] rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(new[] { d.FullName }.Concat(rel).ToArray());
            if (File.Exists(p) || Directory.Exists(p)) return p;
        }
        return null;
    }

    static Unit U(HeroDef t, bool ally, Rarity? asRarity = null)
    {
        var r = asRarity ?? t.Rarity;
        var s = Game.BaseStats(t, 30, NaturalStars(r) + 1, Element.Flame, r); // one element for all: no matchup skew
        return Battle.MakeUnit(ally, t, new Stats { El = Element.Flame, Hp = (int)s.Hp, Atk = (int)s.Atk, Def = (int)s.Def, Spd = (int)s.Spd, Crit = (int)s.Crit, Cdmg = (int)s.Cdmg });
    }

    // Win rate of the team holding `hero` against the same team holding `peer` instead, both ways round.
    // A peer of a lower rarity (no classic hero of the same rarity and role) is raised to the hero's rarity.
    public static double WinRate(HeroDef hero, HeroDef peer, int seeds = 150)
    {
        var fill = Classic.Where(x => x.Rarity == hero.Rarity && x.Id != peer.Id).Take(3).ToList();
        int wins = 0, n = 0;
        for (int seed = 1; seed <= seeds; seed++)
            foreach (bool heroAllied in new[] { true, false })
            {
                var a = new List<Unit> { U(heroAllied ? hero : peer, true, hero.Rarity) }; a.AddRange(fill.Select(f => U(f, true)));
                var b = new List<Unit> { U(heroAllied ? peer : hero, false, hero.Rarity) }; b.AddRange(fill.Select(f => U(f, false)));
                var r = new Battle(a, new List<List<Unit>> { b }, new Random(seed * 7919)).RunAuto();
                bool alliesWon = r == BattleResult.Victory;
                if (alliesWon == heroAllied) wins++;
                n++;
            }
        return wins / (double)n;
    }

    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("\nRoster (docs/sigilbound/ROSTER.md):");
        check(Classic.Length == 24 && Classic.SequenceEqual(Heroes.Take(24)) && Heroes.Select(h => h.Id).Distinct().Count() == Heroes.Length,
            $"{Heroes.Length} unique heroes; the first 24 are the web game's (Classic)");
        check(Heroes.All(h => Looks.Table.ContainsKey(h.Id)), "every hero has a model look (Core/Looks.cs), not the Brannoc fallback");

        var bakedDir = RepoFile("unity", "Tools~", "models", "heroes");   // free-path bakes (none left once every hero is high-detail)
        var hdDir = RepoFile("unity", "Tools~", "models", "heroes_hd");
        check(bakedDir != null || hdDir != null, "hero model folders found (models/heroes_hd)");
        if (bakedDir != null || hdDir != null)
        {
            // A high-detail build (phase F, models/heroes_hd) replaces the free-path bake for that hero.
            bool Hd(string id) => hdDir != null && File.Exists(Path.Combine(hdDir, $"SB_Hero_{id}_HD.glb")) && (File.Exists(Path.Combine(hdDir, $"SB_Hero_{id}_HD_albedo.jpg")) || File.Exists(Path.Combine(hdDir, $"SB_Hero_{id}_HD_albedo.png")));
            bool Baked(string id) => bakedDir != null && File.Exists(Path.Combine(bakedDir, $"SB_Hero_{id}_LOD0.glb")) && File.Exists(Path.Combine(bakedDir, $"SB_Hero_{id}_albedo.png"));
            var missing = Heroes.Where(h => !Hd(h.Id) && !Baked(h.Id)).Select(h => h.Id).ToList();
            check(missing.Count == 0, "every hero has a model (high-detail or free-path bake)" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing) : ""));
            var both = Heroes.Where(h => Hd(h.Id) && Baked(h.Id)).Select(h => h.Id).ToList();
            check(both.Count == 0, "no hero ships both models (the old bake is removed when the high-detail one lands)" + (both.Count > 0 ? " — both: " + string.Join(", ", both) : ""));
        }
        var rosterDoc = RepoFile("docs", "sigilbound", "ROSTER.md");
        if (rosterDoc != null)
        {
            var text = File.ReadAllText(rosterDoc);
            var absent = Heroes.Skip(24).Where(h => !text.Contains($"`{h.Id}`")).Select(h => h.Id).ToList();
            check(absent.Count == 0, "every new hero is a ROSTER.md concept" + (absent.Count > 0 ? " — not found: " + string.Join(", ", absent) : ""));
        }

        // Enemy pools stay the original 24, so stages don't change as heroes are added.
        var classicIds = new HashSet<string>(Classic.Select(h => h.Id));
        bool poolsOk = Enumerable.Range(0, ClassicStages).All(i => Game.StageWaves(i).All(w => w.All(e => classicIds.Contains(e.Template.Id))));
        check(poolsOk, "act 1 enemies come only from the original 24 (stages unchanged)");
        int act2Roster = Enumerable.Range(ClassicStages, TotalStages - ClassicStages).Sum(i => Game.StageWaves(i).Sum(w => w.Count(e => !classicIds.Contains(e.Template.Id))));
        check(Chapters.Length == 15 && act2Roster > 0, $"act 2: chapters 11-15, with {act2Roster} roster heroes among its enemies");

        // Balance: each new hero against the classic hero(es) of its rarity and role.
        foreach (var h in Heroes.Skip(24))
        {
            var peers = Classic.Where(x => x.Rarity == h.Rarity && x.Role == h.Role).ToList();
            if (peers.Count == 0) peers = Classic.Where(x => x.Role == h.Role).ToList();
            var rates = peers.Select(p => (p.Id, rate: WinRate(h, p))).ToList();
            double avg = rates.Average(r => r.rate);
            check(avg >= 0.4 && avg <= 0.6, $"balance: {h.Id} wins {avg:P0} against {string.Join(", ", rates.Select(r => $"{r.Id} {r.rate:P0}"))} (aim 40–60%)");
        }
    }
}
