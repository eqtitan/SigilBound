// Rank-up fodder rules (research BULK_RELEASE_AUTO_RANKUP 1.3 and 3b): the cheapest fodder is picked first
// (lowest rarity, then spare copies, then lowest level), so a fresh Epic or Legendary is never eaten
// before a levelled Common.
using System;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class RankUpTests
{
    public static void Run(Action<bool, string> check)
    {
        FodderOrder(check);
    }

    static void FodderOrder(Action<bool, string> check)
    {
        // A 3★ hero at cap; same-star candidates: a fresh Epic (Lv1, natural 3★) and levelled 3★ Commons.
        var g = new Game(seed: 3);
        var target = g.AddHero("ysolde"); target.stars = 3; target.level = MaxLevelFor(3);
        g.S.team.Add(target.uid);
        var epic = g.AddHero("kaito"); // natural 3★, level 1
        var c1 = g.AddHero("brannoc"); c1.stars = 3; c1.level = 22;
        var c2 = g.AddHero("mira"); c2.stars = 3; c2.level = 25;
        var c3 = g.AddHero("brannoc"); c3.stars = 3; c3.level = 28; // a second Brannoc: spare copies go first
        var pick = g.SuggestFodder(target);
        check(pick.Count == 3 && !pick.Contains(epic), "rank-up: auto-pick takes 3★ Commons before a fresh Lv1 Epic");
        check(pick[0] == c1 && pick[1] == c3 && pick[2] == c2, "rank-up: spare copies (2× Brannoc) before a single Mira, then lowest level");

        // At 4★ a fresh Legendary is passed over the same way.
        var g2 = new Game(seed: 4);
        var t2 = g2.AddHero("kaito"); t2.stars = 4; t2.level = MaxLevelFor(4);
        var leg = g2.AddHero("vaelith"); // natural 4★, Lv1
        var rares = Enumerable.Range(0, 4).Select(_ => { var x = g2.AddHero("hask"); x.stars = 4; x.level = 35; return x; }).ToList();
        var p2 = g2.SuggestFodder(t2);
        check(p2.Count == 4 && !p2.Contains(leg), "rank-up: auto-pick takes 4★ Rares before a fresh Lv1 Legendary");
    }
}
