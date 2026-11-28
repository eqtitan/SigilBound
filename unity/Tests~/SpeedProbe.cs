// Speed probe (Program.cs "speed"): how strong SPD is next to HP and ATK, and how much SPD gear gives
// (mechanics review #6, PMO P2-20). Used to pick Game.GearSpdCap and to judge the Slow/SpdUp size.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class SpeedProbe
{
    // Same random four heroes on both sides (all Epic, one element, level 40, 4 stars); side A gets the
    // gain. Sides swap every other battle so the ally/enemy AI order can't skew it.
    static double Gain(Func<Stats, Stats> boost, int battles, int seed) => GainVs(boost, s => s, battles, seed);
    static double GainVs(Func<Stats, Stats> boost, Func<Stats, Stats> other, int battles, int seed)
    {
        var rng = new Random(seed); int wins = 0;
        Unit Mk(HeroDef t, bool ally, bool boosted)
        {
            var st = Game.BaseStats(t, 40, 4, Element.Flame, Rarity.Epic);
            var s = new Stats { El = Element.Flame, Hp = (int)st.Hp, Atk = (int)st.Atk, Def = (int)st.Def, Spd = (int)st.Spd, Crit = (int)st.Crit, Cdmg = (int)st.Cdmg };
            return Battle.MakeUnit(ally, t, boosted ? boost(s) : other(s));
        }
        for (int i = 0; i < battles; i++)
        {
            var pick = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList();
            bool aBoosted = i % 2 == 0;
            var a = pick.Select(t => Mk(t, true, aBoosted)).ToList();
            var b = pick.Select(t => Mk(t, false, !aBoosted)).ToList();
            bool allyWon = new Battle(a, new List<List<Unit>> { b }, rng).RunAuto() == BattleResult.Victory;
            if (allyWon == aBoosted) wins++;
        }
        return 100.0 * wins / battles;
    }

    // Gear-sourced SPD (final SPD minus the hero's SPD with no gear) for a hero in four Mythic +10
    // pieces, levelled with UpgradeGear so the substats roll as in play.
    static GearSave Piece(Game g, Slot s, int? set = null)
    {
        var gr = g.MakeGear(5, s); if (set.HasValue) gr.set = set.Value;
        g.S.gold = 10_000_000; for (int i = 0; i < GearMaxLevel; i++) g.UpgradeGear(gr);
        return gr;
    }
    static int GearSpd(Game g, HeroSave h)
    {
        foreach (Slot s in Enum.GetValues(typeof(Slot))) h.SetGear(s, 0);
        int bare = g.HeroStats(h).Spd;
        return bare;
    }

    public static void Run(int battles)
    {
        Console.WriteLine($"SPEED Slow/SpdUp size ±{Battle.SpeedFx * 100:0}%, gear SPD cap {(Game.GearSpdCap > 0 ? "+" + Game.GearSpdCap : "none")}");
        var rows = new (string name, Func<Stats, Stats> f)[]
        {
            ("+10 SPD", s => { s.Spd += 10; return s; }),
            ("+10% HP", s => { s.Hp = (int)(s.Hp * 1.1); return s; }),
            ("+10% ATK", s => { s.Atk = (int)(s.Atk * 1.1); return s; }),
            ("+20 SPD", s => { s.Spd += 20; return s; }),
            ("+20% HP", s => { s.Hp = (int)(s.Hp * 1.2); return s; }),
            ("+20% ATK", s => { s.Atk = (int)(s.Atk * 1.2); return s; }),
        };
        // Gear-sized gains, against the side holding the same amount as HP: where SPD stops trading evenly.
        var gearRows = new[] { 20, 40, 60, 80, 110 };
        foreach (var (name, f) in rows) Console.WriteLine($"SPEED gain {name,-9} on every hero: {Gain(f, battles, 31):0.0}%");
        foreach (int x in gearRows)
            Console.WriteLine($"SPEED +{x} SPD against +{x}% HP (same heroes): {GainVs(s => { s.Spd += x; return s; }, s => { s.Hp = (int)(s.Hp * (1 + x / 100.0)); return s; }, battles, 37):0.0}% for the SPD side");

        // A speed-stacked team against the same heroes in ordinary gear (+40 SPD, the median random Mythic set),
        // uncapped (+110) and at candidate caps.
        foreach (int x in new[] { 110, 80, 60, 50 })
            Console.WriteLine($"SPEED stacked +{x} SPD against ordinary +40 SPD: {GainVs(s => { s.Spd += x; return s; }, s => { s.Spd += 40; return s; }, battles, 41):0.0}% for the stacked side");

        // Gear SPD: random full sets (what a player wears) and a speed-stacked set (best SPD piece per slot
        // out of 40 Mythic +10 drops, all Haste).
        int cap = Game.GearSpdCap; Game.GearSpdCap = 0; // measure what the gear would give uncapped
        var g = new Game(seed: 77); var h = g.AddHero("kaito"); h.stars = MaxStars; h.level = MaxLevelFor(MaxStars);
        int bare = GearSpd(g, h);
        var random = new List<int>(); var stacked = new List<int>();
        for (int k = 0; k < 300; k++)
        {
            foreach (Slot s in Enum.GetValues(typeof(Slot))) g.Equip(h, Piece(g, s));
            random.Add(g.HeroStats(h).Spd - bare);
        }
        for (int k = 0; k < 60; k++)
        {
            foreach (Slot s in Enum.GetValues(typeof(Slot)))
            {
                var best = Enumerable.Range(0, 40).Select(_ => Piece(g, s, (int)GearSet.Haste))
                    .OrderByDescending(p => (s == Slot.Boots ? Game.GearMainValue(p) : 0) + p.subs.Where(x => x.stat == (int)Stat.Spd).Sum(x => x.val)).First();
                g.Equip(h, best);
            }
            stacked.Add(g.HeroStats(h).Spd - bare);
            g.S.gear.Clear();
        }
        string Dist(List<int> xs) { xs.Sort(); return $"median +{xs[xs.Count / 2]}  p90 +{xs[xs.Count * 9 / 10]}  max +{xs[xs.Count - 1]}"; }
        Game.GearSpdCap = cap;
        Console.WriteLine($"SPEED gear SPD on a maxed hero (bare {bare}, uncapped): random Mythic +10 sets {Dist(random)}; speed-stacked Haste sets {Dist(stacked)}");
        if (cap > 0) Console.WriteLine($"SPEED cap +{cap}: {100.0 * random.Count(x => x <= cap) / random.Count:0}% of random Mythic +10 sets are under it; a speed-stacked set loses {stacked[stacked.Count / 2] - cap}");
    }
}
