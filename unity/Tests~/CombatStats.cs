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
        }
        lens.Sort();
        double med = lens[lens.Count / 2], p90 = lens[lens.Count * 9 / 10];
        Console.WriteLine($"STAT turns median {med} p90 {p90} avg {turns / (double)battles:0.0} capped {capped}");
        Console.WriteLine($"STAT time at 1.5s/turn: median {med * 1.5 / 60:0.0} min (1x), {med * 1.5 / 180:0.0} min (3x); p90 {p90 * 1.5 / 60:0.0} min (1x)");
        Console.WriteLine($"STAT ally wins {allyWins}/{battles}");
        Console.WriteLine($"STAT wasted: cleanse {wastedCleanse}, heal {wastedHeal}, shield {wastedShield}; re-applied status {100.0 * statusRefresh / Math.Max(1, statusApplied + statusRefresh):0.0}%");
        Console.WriteLine($"STAT turns lost: stun {100.0 * stunSkipOnly / (acts + stunSkips):0.0}%  break {100.0 * breakSkips / (acts + stunSkips):0.0}%   fastest acts first {100.0 * fastFirst / battles:0.0}%");
        Console.WriteLine($"STAT AI basic {100.0 * aiBasic / acts:0.0}%  crit {100.0 * crits / dmgEvents:0.0}%  elem adv {100.0 * elemAdv / dmgEvents:0.0}%  stun skips {100.0 * stunSkips / (acts + stunSkips):0.0}%");
        Console.WriteLine("STAT effects: " + string.Join(", ", effectCounts.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}")));
    }
}

// Quick probes for combat tuning (Program.cs "stagerate idx [runs]" and "titandmg").
static class CombatProbes
{
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
    }
}
