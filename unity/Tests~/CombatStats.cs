// Combat statistics over random battles (see Program.cs "combatstats").
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class CombatStats
{
    public static Unit Mk(HeroDef t, bool ally)
    {
        var st = Game.BaseStats(t, 40, 4, t.Element, t.Rarity);
        return Battle.MakeUnit(ally, t, new Stats { El = st.El, Hp = (int)st.Hp, Atk = (int)st.Atk, Def = (int)st.Def, Spd = (int)st.Spd, Crit = (int)st.Crit, Cdmg = (int)st.Cdmg });
    }

    public static void Run()
    {
        var rng = new Random(7);
        int battles = 0, capped = 0; long turns = 0, acts = 0, stunSkips = 0, wastedCleanse = 0, wastedHeal = 0, wastedShield = 0, statusApplied = 0, statusRefresh = 0, dmgEvents = 0, crits = 0, elemAdv = 0, aiBasic = 0;
        var lens = new List<int>(); int allyWins = 0, fastFirst = 0; long breakSkips = 0, stunSkipOnly = 0;
        var effectCounts = new Dictionary<FxType, int>();
        var roleWins = new Dictionary<Role, int>(); var roleSlots = new Dictionary<Role, int>();
        for (int bi = 0; bi < 2000; bi++)
        {
            var pickA = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList(); var pickB = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList();
            var bt = new Battle(pickA.Select(t => Mk(t, true)).ToList(), new List<List<Unit>> { pickB.Select(t => Mk(t, false)).ToList() }, rng);
            int n = 0;
            var fastest = bt.Units.OrderByDescending(x => bt.Speed(x)).First();
            for (; !bt.Over && n < 3000; n++)
            {
                var u = bt.NextActor();
                if (n == 0 && u == fastest) fastFirst++;
                var (pre, skip) = bt.BeginTurn(u);
                if (skip && u.Alive)
                {
                    stunSkips++;
                    if (pre.Any(e => e.Type == EvType.Text && e.Text == "Broken!")) breakSkips++; else stunSkipOnly++;
                }
                if (!skip)
                {
                    var c = bt.AiChoose(u); var sk = u.Template.Skills[c.skill];
                    var own = Battle.Living(bt.Side(u, true));
                    if (c.skill == 0) aiBasic++;
                    if (sk.Fx.Any(f => f.Type == FxType.Cleanse) && sk.Mult == 0 && !own.Any(a => a.Effects.Any(e => !FxDef.IsBuff(e.Type)))) wastedCleanse++;
                    if (sk.Fx.Any(f => f.Type == FxType.Heal) && sk.Mult == 0 && own.All(a => a.Hp >= a.MaxHp)) wastedHeal++;
                    if (sk.Fx.Any(f => f.Type == FxType.Shield) && sk.Mult == 0 && own.All(a => a.Has(FxType.Shield))) wastedShield++;
                    var had = bt.Units.ToDictionary(x => x, x => x.Effects.Select(e => e.Type).ToHashSet());
                    var ev = bt.Act(u, c.skill, c.target); acts++;
                    foreach (var e in ev)
                    {
                        if (e.Type == EvType.Damage && !e.Dot) { dmgEvents++; if (e.Crit) crits++; if (e.Elem > 1) elemAdv++; }
                        if (e.Type == EvType.Status) { if (had.TryGetValue(e.Unit, out var hs) && hs.Contains(e.Fx)) statusRefresh++; else statusApplied++; effectCounts[e.Fx] = effectCounts.TryGetValue(e.Fx, out var k) ? k + 1 : 1; }
                    }
                }
                bt.EndTurn(u, out _);
            }
            battles++; turns += n; lens.Add(n); if (n >= 3000) capped++;
            if (bt.Result == BattleResult.Victory) allyWins++;
            foreach (var (pick, won) in new[] { (pickA, bt.Result == BattleResult.Victory), (pickB, bt.Result != BattleResult.Victory) })
                foreach (var r in pick.Select(t => t.Role).Distinct())
                {
                    roleSlots[r] = roleSlots.TryGetValue(r, out var rs) ? rs + 1 : 1;
                    if (won) roleWins[r] = roleWins.TryGetValue(r, out var rw) ? rw + 1 : 1;
                }
        }
        lens.Sort();
        double med = lens[lens.Count / 2], p90 = lens[lens.Count * 9 / 10];
        Console.WriteLine($"STAT turns median {med} p90 {p90} avg {turns / (double)battles:0.0} capped {capped}");
        Console.WriteLine($"STAT time at 1.5s/turn: median {med * 1.5 / 60:0.0} min (1x), {med * 1.5 / 180:0.0} min (3x); p90 {p90 * 1.5 / 60:0.0} min (1x)");
        Console.WriteLine($"STAT ally wins {allyWins}/{battles}");
        Console.WriteLine($"STAT wasted: cleanse {wastedCleanse}, heal {wastedHeal}, shield {wastedShield}; re-applied status {100.0 * statusRefresh / Math.Max(1, statusApplied + statusRefresh):0.0}%");
        Console.WriteLine($"STAT turns lost: stun {100.0 * stunSkipOnly / (acts + stunSkips):0.0}%  break {100.0 * breakSkips / (acts + stunSkips):0.0}%   fastest acts first {100.0 * fastFirst / battles:0.0}%");
        Console.WriteLine($"STAT AI basic {100.0 * aiBasic / acts:0.0}%  crit {100.0 * crits / dmgEvents:0.0}%  elem adv {100.0 * elemAdv / dmgEvents:0.0}%  stun skips {100.0 * stunSkips / (acts + stunSkips):0.0}%");
        Console.WriteLine("STAT role slot win %: " + string.Join("  ", Enum.GetValues(typeof(Role)).Cast<Role>().Select(r => $"{r} {100.0 * (roleWins.TryGetValue(r, out var w) ? w : 0) / Math.Max(1, roleSlots.TryGetValue(r, out var n2) ? n2 : 0):0.0}")));
        Console.WriteLine("STAT effects: " + string.Join(", ", effectCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
    }
}

// Quick probes for combat tuning (Program.cs "stagerate idx [runs]", "campaign [runs]", "roles [battles]" and "titandmg").
static class CombatProbes
{
    static readonly string[][] Lineups = { new[] { "ysolde", "corvan", "hask", "kaito" }, new[] { "maelis", "oriel", "aurelia", "vaelith" } };

    static int Wins(string[] ids, int idx, int runs)
    {
        int lvl = Game.StageLevel(idx), wins = 0;
        for (int k = 0; k < runs; k++)
        {
            var gg = new Game(seed: idx * 100 + k);
            gg.S.team = ids.Select(id =>
            {
                var hh = gg.AddHero(id);
                hh.stars = idx >= ClassicStages ? MaxStars : Math.Max(hh.stars, Math.Min(5, (int)Math.Ceiling(lvl / 10.0)));
                hh.level = idx >= ClassicStages ? MaxLevelFor(MaxStars) : Math.Min(MaxLevelFor(hh.stars), lvl);
                foreach (Slot s in Enum.GetValues(typeof(Slot))) { var gr = gg.MakeGear(Math.Min(5, 1 + idx / 12), s); gr.level = Math.Min(10, idx / 6 + 2); gg.Equip(hh, gr); }
                return hh.uid;
            }).ToList();
            if (Battle.ForStage(gg, idx).RunAuto() == BattleResult.Victory) wins++;
        }
        return wins;
    }

    // Every campaign stage for both suite lineups, averaged by act and chapter element (Light/Shadow or triangle).
    public static void Campaign(int runs)
    {
        var groups = new SortedDictionary<string, (double mid, double strong, int n, int under)>();
        for (int idx = 0; idx < TotalStages; idx++)
        {
            double m = Wins(Lineups[0], idx, runs) * 100.0 / runs, st = Wins(Lineups[1], idx, runs) * 100.0 / runs;
            var el = Game.ChapterElement(Game.ChapterOf(idx));
            string key = (idx < ClassicStages ? "Act I " : "Act II ") + (el == Element.Light || el == Element.Shadow ? "Light/Shadow" : "triangle");
            var g = groups.TryGetValue(key, out var v) ? v : (0, 0, 0, 0);
            groups[key] = (g.mid + m, g.strong + st, g.n + 1, g.under + (m < 50 ? 1 : 0));
            Console.WriteLine($"STAGE {Game.StageLabel(idx),-5} {el,-6} mul {Game.Grade(idx).Mul * Game.CampaignEase(idx):0.000}: {m,3:0}%  {st,3:0}%");
        }
        foreach (var kv in groups)
            Console.WriteLine($"GROUP {kv.Key,-20} stages {kv.Value.n,2}: mid {kv.Value.mid / kv.Value.n:0.0}%  strong {kv.Value.strong / kv.Value.n:0.0}%  mid under 50%: {kv.Value.under}");
    }

    // Role against role: three identical fillers each side, the fourth hero's role differs; all Epic, one element.
    public static void Roles(int battles)
    {
        var rng = new Random(23);
        var roles = Enum.GetValues(typeof(Role)).Cast<Role>().ToArray();
        Unit Mk(HeroDef t, bool ally)
        {
            var st = Game.BaseStats(t, 40, 4, Element.Flame, Rarity.Epic);
            return Battle.MakeUnit(ally, t, new Stats { El = Element.Flame, Hp = (int)st.Hp, Atk = (int)st.Atk, Def = (int)st.Def, Spd = (int)st.Spd, Crit = (int)st.Crit, Cdmg = (int)st.Cdmg });
        }
        var vs = new Dictionary<Role, (int w, int n)>();
        foreach (var ra in roles)
            foreach (var rb in roles)
            {
                if (ra == rb) continue;
                var pa = Heroes.Where(h => h.Role == ra).ToList(); var pb = Heroes.Where(h => h.Role == rb).ToList();
                int w = 0;
                for (int i = 0; i < battles; i++)
                {
                    var fill = Heroes.OrderBy(_ => rng.Next()).Take(3).ToList();
                    var ha = pa[rng.Next(pa.Count)]; var hb = pb[rng.Next(pb.Count)];
                    var a = new List<Unit> { Mk(ha, true) }; a.AddRange(fill.Select(f => Mk(f, true)));
                    var b = new List<Unit> { Mk(hb, false) }; b.AddRange(fill.Select(f => Mk(f, false)));
                    if (new Battle(a, new List<List<Unit>> { b }, rng).RunAuto() == BattleResult.Victory) w++;
                }
                var x = vs.TryGetValue(ra, out var v) ? v : (w: 0, n: 0); vs[ra] = (x.w + w, x.n + battles);
                var y = vs.TryGetValue(rb, out var v2) ? v2 : (w: 0, n: 0); vs[rb] = (y.w + battles - w, y.n + battles);
            }
        Console.WriteLine("ROLES vs other roles: " + string.Join("  ", roles.Select(r => $"{r} {100.0 * vs[r].w / vs[r].n:0.0}")) + $"  spread {roles.Max(r => 100.0 * vs[r].w / vs[r].n) - roles.Min(r => 100.0 * vs[r].w / vs[r].n):0.0} pts");
    }

    // Win rate of the suite's two fixed lineups on one stage (same set-up as the full suite's stage table).
    public static void StageRate(int idx, int runs)
    {
        string[][] lineups = { new[] { "ysolde", "corvan", "hask", "kaito" }, new[] { "maelis", "oriel", "aurelia", "vaelith" } };
        int lvl = Game.StageLevel(idx);
        var row = lineups.Select(ids =>
        {
            int wins = 0;
            for (int k = 0; k < runs; k++)
            {
                var gg = new Game(seed: idx * 100 + k);
                gg.S.team = ids.Select(id =>
                {
                    var hh = gg.AddHero(id);
                    hh.stars = idx >= ClassicStages ? MaxStars : Math.Max(hh.stars, Math.Min(5, (int)Math.Ceiling(lvl / 10.0)));
                    hh.level = idx >= ClassicStages ? MaxLevelFor(MaxStars) : Math.Min(MaxLevelFor(hh.stars), lvl);
                    foreach (Slot s in Enum.GetValues(typeof(Slot))) { var gr = gg.MakeGear(Math.Min(5, 1 + idx / 12), s); gr.level = Math.Min(10, idx / 6 + 2); gg.Equip(hh, gr); }
                    return hh.uid;
                }).ToList();
                if (Battle.ForStage(gg, idx).RunAuto() == BattleResult.Victory) wins++;
            }
            return $"{wins * 100.0 / runs:0}%";
        });
        Console.WriteLine($"STAGE {idx / StagesPerChapter + 1}-{idx % StagesPerChapter + 1}: {string.Join("  ", row)}");
    }

    // Average Titan damage over many seeds and teams (Battle.ForTitan, all five Titan elements).
    public static void TitanDamage()
    {
        var rng = new Random(11); long total = 0; int n = 0;
        foreach (var lv in new[] { 20, 40, 60 })
            for (int k = 0; k < 120; k++)
            {
                var g = new Game(seed: 5000 + k + lv * 1000);
                var pick = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList();
                g.S.team = pick.Select(t => { var h = g.AddHero(t.Id); h.stars = Math.Min(MaxStars, Math.Max(h.stars, lv / 10)); h.level = Math.Min(MaxLevelFor(h.stars), lv); return h.uid; }).ToList();
                var b = Battle.ForTitan(g); b.RunAuto();
                total += b.TitanDamage; n++;
            }
        Console.WriteLine($"TITAN avg damage {total / n:N0} over {n} fights");
        // Reward tier reach for a maxed team (6 stars, level 60, Mythic +10 gear): share of fights reaching each tier.
        var dm = new List<long>();
        for (int k = 0; k < 150; k++)
        {
            var g = new Game(seed: 90000 + k);
            var pick = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList();
            g.S.team = pick.Select(t =>
            {
                var h = g.AddHero(t.Id); h.stars = MaxStars; h.level = MaxLevelFor(MaxStars);
                foreach (Slot s in Enum.GetValues(typeof(Slot))) { var gr = g.MakeGear(5, s); gr.level = 10; g.Equip(h, gr); }
                return h.uid;
            }).ToList();
            var b = Battle.ForTitan(g); b.RunAuto(); dm.Add(b.TitanDamage);
        }
        dm.Sort();
        Console.WriteLine($"TITAN maxed team median {dm[dm.Count / 2]:N0} p90 {dm[dm.Count * 9 / 10]:N0}; tier reach: " + string.Join("  ", TitanTiers.Skip(1).Select(t => $">={t.Dmg / 1000}k {100.0 * dm.Count(d => d >= t.Dmg) / dm.Count:0}%")));
    }
}
