// Core checks: content sanity, progression rules, battles, web parity (ParityTests.cs),
// Trials and promo rules, and a campaign win-rate sweep.
using System;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class Program
{
    static int failures;
    static void Check(bool ok, string what) { Console.WriteLine((ok ? "PASS " : "FAIL ") + what); if (!ok) failures++; }

    static int Main(string[] args)
    {
        // `dotnet run --project Tests~ -- idles`: each hero's in-game idle clip (review renders pose with it).
        if (args.Length > 0 && args[0] == "idles")
        {
            foreach (var hd in Heroes) Console.WriteLine($"{hd.Id} {Sigilbound.Core.AnimStyle.Hd(hd.Id).Idle}");
            Console.WriteLine("titan " + Sigilbound.Core.AnimStyle.TitanHd.Idle);
            return 0;
        }
        // `dotnet run --project Tests~ -- ults`: each hero's family, rarity, role and ultimate (docs/sigilbound/ultfx.py).
        if (args.Length > 0 && args[0] == "ults")
        {
            foreach (var hu in Heroes)
            {
                var sk = hu.Skills.Length > 2 ? hu.Skills[2] : hu.Skills.Last();
                Console.WriteLine($"{hu.Id}\t{Sigilbound.Core.Impact.FamilyOf(hu.Id)}\t{Sigilbound.Core.Looks.Of(hu.Id).Prop}\t{hu.Rarity}\t{hu.Role}\t{sk.Target}\t{sk.Mult}\t{sk.Name}");
            }
            return 0;
        }
        // `dotnet run --project Tests~ -- combatstats`: 2000 random 4v4 battles (all heroes, lv40 4*) -> length,
        // stalemates, AI choices and effect use. Used to judge combat rule changes (PLAN step 97).
        if (args.Length > 1 && args[0] == "stagerate") { CombatProbes.StageRate(int.Parse(args[1]), args.Length > 2 ? int.Parse(args[2]) : 30); return 0; }
        if (args.Length > 0 && args[0] == "campaign") { CombatProbes.Campaign(args.Length > 1 ? int.Parse(args[1]) : 60); return 0; }
        if (args.Length > 0 && args[0] == "roles") { CombatProbes.Roles(args.Length > 1 ? int.Parse(args[1]) : 1500); return 0; }
        if (args.Length > 0 && args[0] == "titandmg") { CombatProbes.TitanDamage(); return 0; }
        if (args.Length > 0 && args[0] == "speed") { SpeedProbe.Run(args.Length > 1 ? int.Parse(args[1]) : 3000); return 0; }
        // `dotnet run --project Tests~ -- stageenemies > fixtures/stage-enemies.json`: the golden enemy ids of every
        // campaign stage (MechanicsTests checks the game against it). Regenerate only for a deliberate pool change.
        if (args.Length > 0 && args[0] == "stageenemies") { Console.WriteLine(MechanicsTests.StageEnemiesJson()); return 0; }
        if (args.Length > 0 && args[0] == "combatstats") { CombatStats.Run(); return 0; }
        // `dotnet run --project Tests~ -- scenes OUT.json [tier]`: every campaign stage's generated layout (Tools~/scenery/scene_preview.py).
        if (args.Length > 1 && args[0] == "scenes") { SceneGenTests.Dump(args[1], args.Length > 2 ? int.Parse(args[2]) : 2); return 0; }
        // `-- scenedebug CHAPTER`: each stop's landmark, its framing in the default view, placements and refusals.
        if (args.Length > 0 && args[0] == "scenedebug")
        {
            int ch = int.Parse(args[1]);
            for (int st = 0; st < 6; st++)
            {
                var L = SceneGen.Generate(ch, st); var m = L.Landmark;
                Console.Write($"{ch + 1}-{st + 1} {m.Mesh} at ({m.X:0.0},{m.Z:0.0}) top {L.LandmarkTop:0.0} err {SceneRules.FrameError(L, m, L.LandmarkTop):0.00} items {L.Items.Count} tris {L.Tris} rejects " + string.Join(",", L.Rejects.Select(kv => kv.Key + "=" + kv.Value)) + " |");
                foreach (var c in L.Cams.Where(k => k.Default)) { var q = SceneCameras.Project(c.View, c.Aspect, new V3(m.X, m.Y + L.LandmarkTop * 0.5, m.Z)); Console.Write($" {c.Aspect:0.00}:({q?.x:0.00},{q?.y:0.00}) eye {c.View.Eye}"); }
                Console.WriteLine();
            }
            return 0;
        }
        // `-- lairtilt`: each lair's landmark (base, body, top) in the default view, level and with the boss tilt.
        if (args.Length > 0 && args[0] == "lairtilt")
        {
            for (int ch = 0; ch < GameData.Chapters.Length; ch++)
            {
                var L = SceneGen.Generate(ch, 5); var m = L.Landmark; if (m == null) continue;
                Console.Write($"{ch + 1}-6 {m.Mesh} ({m.X:0.0},{m.Z:0.0}) top {L.LandmarkTop:0.0}:");
                foreach (var a in SceneCameras.Aspects)
                    foreach (bool boss in new[] { false, true })
                    {
                        var v = BattleLayout.View(BattleLayout.DefaultCamera, a, false, boss);
                        var qb = SceneCameras.Project(v, a, new V3(m.X, 0, m.Z)); var qt = SceneCameras.Project(v, a, new V3(m.X, L.LandmarkTop, m.Z));
                        Console.Write($" {a:0.00}{(boss ? "T" : "")} y {qb?.y:0.00}..{qt?.y:0.00}");
                    }
                Console.WriteLine();
            }
            return 0;
        }
        // `-- scenetests`: only the scene generator checks (Core/SceneGen.cs).
        if (args.Length > 0 && args[0] == "scenetests") { SceneGenTests.Run(Check); Console.WriteLine(failures == 0 ? "All checks passed." : $"{failures} FAILED"); return failures == 0 ? 0 : 1; }
        // `dotnet run --project Tests~ -- balance id...`: just the roster balance sims (docs/sigilbound/newheroes.py tunes with it).
        if (args.Length > 0 && args[0] == "balance")
        {
            foreach (var id in args.Skip(1))
            {
                var nh = Heroes.First(x => x.Id == id);
                Console.WriteLine($"RATE {id} {RosterTests.Peers(nh).Average(pp => RosterTests.WinRate(nh, pp)):0.000}");
            }
            return 0;
        }
        Check(Classic.Length == 24 && Classic.Select(h => h.Id).Distinct().Count() == 24 && Heroes.Select(h => h.Id).Distinct().Count() == Heroes.Length, $"24 classic heroes, {Heroes.Length} in all, unique ids");
        Check(ElementMultiplier(Element.Flame, Element.Gale) == 1.3 && ElementMultiplier(Element.Gale, Element.Flame) == 0.8 && ElementMultiplier(Element.Light, Element.Shadow) == 1.3, "element triangle");
        Check(Game.StageLevel(0) == 2 && Game.StageLevel(59) == 58, $"stage levels match web ({Game.StageLevel(0)}, {Game.StageLevel(59)})");
        // Same deterministic enemy picks as the web version (stage 1: Velka + Kettle in the web build).
        var w0 = Game.StageWaves(0)[0].Select(e => e.Template.Id).ToArray();
        Check(w0.SequenceEqual(new[] { "velka", "kettle" }), $"stage 1-1 enemies match web build ({string.Join(",", w0)})");

        var g = new Game(seed: 42);
        g.StartWith("ysolde");
        Check(g.S.started && g.Team.Count == 3 && g.S.gear.Count == 3, "new game: 3 heroes, 3 weapons");
        var h = g.Team[0];
        int p0 = g.Power(h);
        g.S.gold = 1_000_000;
        Check(g.Train(h, true) && h.level == MaxLevelFor(h.stars), $"train to cap (level {h.level})");
        Check(g.Power(h) > p0, "training raises power");
        while (g.RankUpFodder(h).Count < h.stars) g.AddHero("ysolde").stars = h.stars;
        Check(g.CanRankUp(h) && g.RankUp(h, g.SuggestFodder(h)) && h.stars == 3, "rank up 2★ → 3★");
        var gear = g.MakeGear(4, Slot.Weapon); int before = Game.GearMainValue(gear);
        for (int i = 0; i < 10; i++) g.UpgradeGear(gear);
        Check(gear.level == 10 && Game.GearMainValue(gear) > before && gear.subs.Count == 4, "gear upgrades to +10 and gains a substat");
        g.S.shardMystic = 60; g.S.pityMystic = 0;
        var pulls = g.Summon(Shard.Mystic, 60);
        Check(pulls.Count == 60 && pulls.Any(x => Hero(x.id).Rarity == Rarity.Legendary), "mystic pity guarantees a legendary within 60");
        g.S.daily = new DailySave(); g.EnsureDaily();
        Check(g.ClaimLogin() != null && g.ClaimLogin() == null, "login reward claimable once per day");

        // Save round-trip sanity (field-only model)
        Check(typeof(SaveData).GetProperties().Length == 0, "SaveData has fields only (JsonUtility-safe)");

        // Battles: stage 1 with the starter team should be won; titan runs to its turn limit.
        var g2 = new Game(seed: 7); g2.StartWith("ysolde");
        var b = Battle.ForStage(g2, 0);
        Check(b.RunAuto() == BattleResult.Victory, "starter team wins stage 1-1 on auto");
        var t = Battle.ForTitan(g2);
        var tr = t.RunAuto();
        Check((tr == BattleResult.Timeout || tr == BattleResult.Defeat) && t.TitanDamage > 0 && t.BossTurns <= TitanTurns, $"weak team vs titan ends by wipe or turn limit ({tr}, dmg {t.TitanDamage:N0})");
        var g3 = new Game(seed: 9); g3.S.team = new[] { "seraphine", "ulfgar", "vaelith", "maelis" }.Select(id => { var x = g3.AddHero(id); x.stars = 5; x.level = 50; return x.uid; }).ToList();
        var t3 = Battle.ForTitan(g3); var tr3 = t3.RunAuto();
        Check(tr3 == BattleResult.Timeout && t3.BossTurns == TitanTurns, $"strong team survives all {TitanTurns} titan turns (dmg {t3.TitanDamage:N0})");

        ParityTests.Run(Check);
        RenderTests.Run(Check);
        KitTests.Run(Check);
        ExtrasTests.Run(Check);
        ArtTests.Run(Check);
        RosterTests.Run(Check);
        MechanicsTests.Run(Check);
        ChangelogTests.Run(Check);
        HdClipTests.Run(Check);
        ImpactTests.Run(Check);
        CreatureTests.Run(Check);
        ApkSizeTests.Run(Check);
        FlightTests.Run(Check);
        HdClipFileTests.Run(Check);
        SignatureTests.Run(Check);
        SoundTests.Run(Check);
        TitanCameraTests.Run(Check);
        TitanMechanicsTests.Run(Check);
        StoryScenesTests.Run(Check);
        SpringTests.Run(Check);
        LairRevealTests.Run(Check);
        SceneGenTests.Run(Check);
        SaveSafetyTests.Run(Check);
        RankUpTests.Run(Check);
        ReleaseTests.Run(Check);
        CachePolicyTests.Run(Check);
        ModelPackTests.Run(Check);
        DigitigradeTests.Run(Check);
        UpdaterTests.Run(Check);
        StoreBuildTests.Run(Check);
        FrameRateTests.Run(Check);
        QuitRuleTests.Run(Check);
        UxTests.Run(Check);
        UxRulesTests.Run(Check);
        AccessibilityTests.Run(Check);
        PolishTests.Run(Check);
        HotPathTests.Run(Check);
        AutoClimbTests.Run(Check);
        IntentTests.Run(Check);
        GameplayUiTests.Run(Check);
        PhoneCheckTests.Run(Check);
        BrainTests.Run(Check);
        BattleVisualsTests.Run(Check);

        // Win-rate sweep like the web sim's fixed-team check (geared team at stage level).
        Console.WriteLine("\nStage win rates (fixed teams at stage level, 30 runs):");
        string[][] lineups = { new[] { "ysolde", "corvan", "hask", "kaito" }, new[] { "maelis", "oriel", "aurelia", "vaelith" } };
        for (int idx = 0; idx < TotalStages; idx += idx >= ClassicStages ? 3 : 6)
        {
            int lvl = Game.StageLevel(idx);
            var row = lineups.Select(ids =>
            {
                int wins = 0;
                for (int k = 0; k < 30; k++)
                {
                    var gg = new Game(seed: idx * 100 + k);
                    gg.S.team = ids.Select(id =>
                    {
                        var hh = gg.AddHero(id);
                        hh.stars = idx >= ClassicStages ? MaxStars : Math.Max(hh.stars, Math.Min(5, (int)Math.Ceiling(lvl / 10.0)));
                        hh.level = idx >= ClassicStages ? MaxLevelFor(MaxStars) : Math.Min(MaxLevelFor(hh.stars), lvl); // act 2: endgame teams
                        foreach (Slot s in Enum.GetValues(typeof(Slot))) { var gr = gg.MakeGear(Math.Min(5, 1 + idx / 12), s); gr.level = Math.Min(10, idx / 6 + 2); gg.Equip(hh, gr); }
                        return hh.uid;
                    }).ToList();
                    if (Battle.ForStage(gg, idx).RunAuto() == BattleResult.Victory) wins++;
                }
                return $"{wins * 100 / 30,3}%";
            });
            Console.WriteLine($"  stage {Game.StageLabel(idx),-5} lvl {lvl,2}: {string.Join("  ", row)}");
        }
        Console.WriteLine(failures == 0 ? "\nAll checks passed." : $"\n{failures} check(s) failed.");
        return failures == 0 ? 0 : 1;
    }
}
