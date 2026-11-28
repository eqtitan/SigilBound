// Rank-up fodder rules (research BULK_RELEASE_AUTO_RANKUP 1.3 and 3b; owner decisions 2026-11-28): the cheapest
// fodder is picked first (lowest rarity, then spare copies, then lowest level), so a fresh Epic or Legendary is
// never eaten before a levelled Common; auto-picks use the same protection function as release; the player can
// still pick a protected-by-default hero by hand (never a locked or team hero); "Rank up all" plans and costs.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class RankUpTests
{
    public static void Run(Action<bool, string> check)
    {
        FodderOrder(check);
        Protections(check);
        ManualPicks(check);
        RankAll(check);
    }

    static HeroSave A(Game g, string id, int stars, int level) { var h = g.AddHero(id); h.stars = stars; h.level = level; return h; }

    static void FodderOrder(Action<bool, string> check)
    {
        // A 3★ hero at cap; same-star candidates: a fresh Epic (Lv1, natural 3★) and levelled 3★ Commons.
        var g = new Game(seed: 3);
        var target = A(g, "ysolde", 3, MaxLevelFor(3));
        g.S.team.Add(target.uid);
        g.AddHero("kaito"); var epic = g.AddHero("kaito"); // two copies, so only the rarity rule can save them
        var b1 = A(g, "brannoc", 3, 22); var b2 = A(g, "brannoc", 3, 28); var b3 = A(g, "brannoc", 3, 30);
        var m1 = A(g, "mira", 3, 21); var m2 = A(g, "mira", 3, 24);
        var pick = g.SuggestFodder(target);
        check(pick.Count == 3 && pick.All(f => Hero(f.id).Rarity == Rarity.Common), "rank-up: auto-pick takes 3★ Commons before a fresh Lv1 Epic");
        check(pick.SequenceEqual(new[] { b1, b2, m1 }), $"rank-up: spare copies first (3× Brannoc before 2× Mira), lowest level first, never the last copy ({string.Join(",", pick.Select(f => f.id + f.level))})");

        // At 4★ a fresh Legendary is passed over the same way.
        var g2 = new Game(seed: 4);
        var t2 = A(g2, "kaito", 4, MaxLevelFor(4));
        g2.AddHero("vaelith"); var leg = g2.AddHero("vaelith");
        for (int i = 0; i < 5; i++) A(g2, "hask", 4, 35);
        var p2 = g2.SuggestFodder(t2);
        check(p2.Count == 4 && !p2.Contains(leg) && p2.All(f => f.id == "hask"), "rank-up: auto-pick takes 4★ Rares before a fresh Lv1 Legendary");
    }

    static void Protections(Action<bool, string> check)
    {
        var g = new Game(seed: 5);
        var t = A(g, "hask", 2, 20); g.S.team.Add(t.uid);
        var team2 = A(g, "nettle", 2, 9); g.S.team.Add(team2.uid);
        var locked = A(g, "nettle", 2, 9); locked.locked = true;
        var saved = A(g, "nettle", 2, 9); g.S.teams.Add(new TeamPreset { uids = { saved.uid } });
        var geared = A(g, "nettle", 2, 9); g.Equip(geared, g.MakeGear(1, Slot.Boots));
        var last = A(g, "mira", 2, 9);       // the only Mira
        var rare = g.AddHero("velka"); g.AddHero("velka").stars = 1; // a natural 2★ Rare, not the last copy
        var ok = A(g, "nettle", 2, 15);
        var auto = g.AutoFill(t);
        check(auto.SequenceEqual(new[] { ok, rare }), $"rank-up: auto-fill never picks locked, team, saved-team, geared or last-copy heroes ({string.Join(",", auto.Select(f => f.id))})");
        // The same function decides release: whatever auto-fill would refuse, release (Safe) refuses too.
        var plan = g.PlanRelease(new ReleaseFilter { rarityMask = 0b1111, starMask = 0b111111, keepBest = 0 });
        var refused = g.S.heroes.Where(h => g.IsProtected(h, Protect.Fodder)).ToList();
        check(refused.All(h => !plan.Release.Contains(h)) && refused.All(h => !auto.Contains(h)), "rank-up and release: one IsProtected rule set (anything it protects is in neither)");
        // Epic fodder is never auto-picked.
        var g2 = new Game(seed: 6); var t2 = A(g2, "kaito", 3, 30); g2.AddHero("oriel"); g2.AddHero("oriel"); g2.AddHero("oriel");
        check(g2.AutoFill(t2).Count == 0, "rank-up: auto-fill never picks Epic or Legendary heroes");
    }

    static void ManualPicks(Action<bool, string> check)
    {
        var g = new Game(seed: 7);
        g.S.gold = 100_000;
        var t = A(g, "hask", 2, 20); g.S.team.Add(t.uid);
        var saved = A(g, "nettle", 2, 9); g.S.teams.Add(new TeamPreset { uids = { saved.uid } });
        var plain = A(g, "nettle", 2, 3);
        var locked = A(g, "nettle", 2, 3); locked.locked = true;
        var member = A(g, "nettle", 2, 3); g.S.team.Add(member.uid);
        check(g.ProtectWarning(saved) == "In a saved team" && g.ProtectWarning(plain) == null, "rank-up: a hand-picked saved-team hero gets a warning");
        check(!g.RankUp(t, new List<HeroSave> { plain, locked }) && !g.RankUp(t, new List<HeroSave> { plain, member }) && !g.RankUp(t, new List<HeroSave> { plain, plain }), "rank-up: a locked or team hero (or the same hero twice) is refused");
        check(g.RankUp(t, new List<HeroSave> { plain, saved }) && t.stars == 3 && !g.S.heroes.Contains(saved), "rank-up: a hand-picked saved-team hero is accepted");
        // Hand-picked Epic / last copy: allowed, flagged as dangerous.
        var g2 = new Game(seed: 8); g2.S.gold = 100_000;
        var t2 = A(g2, "kaito", 3, 30); var ep = g2.AddHero("oriel"); var c3 = A(g2, "cole", 3, 25); var c4 = A(g2, "cole", 3, 25);
        check(Game.Dangerous(g2.ProtectWarning(ep)) && g2.ProtectWarning(ep) == "Epic", "rank-up: a hand-picked Epic is flagged as dangerous");
        var filled = g2.AutoFill(t2, new List<HeroSave> { ep });
        check(filled.Count == 2 && filled[0] == ep && filled[1].id == "cole", "auto-fill: keeps the slots you filled by hand and fills the rest (one Cole stays: last copy)");
        check(g2.RankUp(t2, new List<HeroSave> { ep, c3, c4 }) && t2.stars == 4, "rank-up: hand-picked Epic and last copy are accepted");
    }

    static void RankAll(Action<bool, string> check)
    {
        var g = new Game(seed: 9);
        g.S.gold = 30_000;
        var a = A(g, "ysolde", 2, 20); var b = A(g, "hask", 2, 20); var c = A(g, "corvan", 1, 10);
        g.S.team.AddRange(new[] { a.uid, b.uid, c.uid });
        var notCap = A(g, "lumen", 2, 12); g.S.team.Add(notCap.uid);
        for (int i = 0; i < 3; i++) A(g, "brannoc", 2, 15);
        for (int i = 0; i < 3; i++) A(g, "mira", 2, 11);
        for (int i = 0; i < 3; i++) A(g, "pell", 1, 1);
        var plan = g.PlanRankAll(0, true, false);
        var used = plan.Steps.SelectMany(s => s.Fodder).ToList();
        check(plan.Heroes == 3 && plan.Steps.Count == 3 && used.Distinct().Count() == used.Count, $"rank up all: 3 team heroes at cap, no fodder used twice ({plan.Steps.Count} steps, {used.Count} fodder)");
        check(plan.Steps.All(s => !used.Contains(s.Hero)) && plan.Skipped.Any(x => x.h == notCap && x.why.StartsWith("Below max level")), "rank up all: a promoted hero is never fodder; a hero below cap is skipped with a reason");
        check(plan.Gold == plan.Steps.Sum(s => s.Gold + s.TrainGold) && plan.Gold == RankUpGold(2) * 2 + RankUpGold(1) && plan.Gold <= g.S.gold && plan.FodderUsed == 5, $"rank up all: cost preview {plan.Gold} gold, 5 fodder = sum of the steps, within your gold");
        check(plan.Steps.All(s => s.Fodder.All(f => Hero(f.id).Rarity == Rarity.Common)), "rank up all: cheapest fodder (Commons)");
        int gold0 = g.S.gold, n0 = g.S.heroes.Count;
        var expect = plan.Steps.ToDictionary(s => s.Hero.uid, s => s.To);
        int done = g.ApplyRankAll(plan);
        check(done == plan.Steps.Count && expect.All(kv => g.HeroByUid(kv.Key).stars == kv.Value) && gold0 - g.S.gold == plan.Gold && g.S.heroes.Count == n0 - plan.FodderUsed && used.All(f => !g.S.heroes.Contains(f)),
            "rank up all: applying gives exactly the planned stars, gold and fodder");

        // Gold runs out: a partial plan.
        var g2 = new Game(seed: 10); g2.S.gold = 5000;
        var x = A(g2, "ysolde", 2, 20); var y = A(g2, "hask", 2, 20); g2.S.team.AddRange(new[] { x.uid, y.uid });
        for (int i = 0; i < 5; i++) A(g2, "brannoc", 2, 15);
        var p2 = g2.PlanRankAll(0, true, false);
        check(p2.Steps.Count == 1 && p2.GoldShort && p2.Skipped.Any(s => s.why == "Gold runs out") && p2.Gold <= 5000, "rank up all: stops when the gold would run out (1 of 2)");

        // A target above the next star, training to cap included in the preview.
        var g3 = new Game(seed: 11); g3.S.gold = 1_000_000;
        var z = A(g3, "corvan", 2, 20); g3.S.team.Add(z.uid);
        for (int i = 0; i < 3; i++) A(g3, "brannoc", 2, 15);
        for (int i = 0; i < 4; i++) A(g3, "mira", 3, 22);
        var noTrain = g3.PlanRankAll(4, true, false);
        check(noTrain.Steps.Count == 1 && noTrain.Skipped.Any(s => s.why.StartsWith("Needs training")), "rank up all: without training, a hero stops at the next cap");
        var p3 = g3.PlanRankAll(4, true, true);
        int train = p3.Steps.Sum(s => s.TrainGold);
        check(p3.Steps.Count == 2 && p3.Steps[1].TrainGold > 0 && p3.Gold == RankUpGold(2) + RankUpGold(3) + train, $"rank up all: 2★ → 4★ with training costs {p3.Gold} gold (training {train})");
        int g3gold = g3.S.gold;
        check(g3.ApplyRankAll(p3) == 2 && z.stars == 4 && g3gold - g3.S.gold == p3.Gold, "rank up all: applying the trained plan spends exactly the preview");
    }
}
