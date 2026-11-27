// Quit rule (owner decision 2026-11-27, PMO D3 / QA #5): a Titan try and an arena fight count when the fight
// starts, so leaving or closing the game mid-fight is no free retry. Finishing normally counts once.
using System;
using Sigilbound.Core;

static class QuitRuleTests
{
    // "Closing the app": the save as written right after the start, loaded into a fresh game.
    static Game Reload(Game g, DateTime now) => new Game(SaveCodec.FromJson(SaveCodec.ToJson(g.S)), 1) { Now = () => now };

    public static void Run(Action<bool, string> check)
    {
        var now = new DateTime(2026, 11, 27, 12, 0, 0);
        var g = new Game(seed: 9) { Now = () => now };
        g.StartWith("ysolde"); g.S.cleared = 12; g.EnsureDaily(); g.EnsureModes();

        // Titan
        check(g.S.daily.titanTries == 3 && g.StartTitan() && g.S.daily.titanTries == 2, "quit rule: starting a Titan fight uses the try");
        var killed = Reload(g, now);
        check(killed.S.daily.titanTries == 2, "quit rule: closing the game mid-Titan doesn't give the try back");
        g.CompleteTitan(12_000);
        check(g.S.daily.titanTries == 2 && g.S.daily.titanBest == 12_000, "quit rule: finishing the Titan fight doesn't use a second try");
        g.S.daily.titanTries = 0;
        check(!g.StartTitan() && g.S.daily.titanTries == 0, "quit rule: no Titan fight without a try");
        var legacy = new Game(seed: 9) { Now = () => now }; legacy.EnsureDaily(); legacy.CompleteTitan(1000);
        check(legacy.S.daily.titanTries == 2, "quit rule: a Titan result without a start still uses one try (web order)");

        // Arena: closing the game mid-fight is a loss.
        var A = g.S.modes.arena; int p0 = A.points, l0 = A.losses, w0 = A.wins, t0 = g.S.daily.arenaTries, s0 = A.seed;
        var rv = g.CurrentRivals()[1];
        check(g.StartArena(rv) && g.S.daily.arenaTries == t0 - 1 && A.points == p0 + Trials.ArenaLoss && A.losses == l0 + 1 && A.seed == s0 + 1, "quit rule: starting an arena fight uses the try and books a loss");
        var killedA = Reload(g, now);
        check(killedA.S.daily.arenaTries == t0 - 1 && killedA.S.modes.arena.points == p0 + Trials.ArenaLoss && killedA.S.modes.arena.losses == l0 + 1, "quit rule: closing the game mid-arena counts as a loss");
        var win = g.CompleteArena(rv, true);
        check(win.Win && A.points == p0 + rv.Gain && A.losses == l0 && A.wins == w0 + 1 && g.S.daily.arenaTries == t0 - 1 && A.seed == s0 + 1 && win.Before.Name == Trials.TierFor(p0).Name,
            "quit rule: winning replaces the booked loss (one try, one result)");
        int p1 = A.points, l1 = A.losses;
        var rv2 = g.CurrentRivals()[0];
        g.StartArena(rv2); var loss = g.CompleteArena(rv2, false);
        check(!loss.Win && A.points == p1 + Trials.ArenaLoss && A.losses == l1 + 1 && g.S.daily.arenaTries == t0 - 2, "quit rule: losing (or retreating) counts the loss once");
        g.S.daily.arenaTries = 0;
        check(!g.StartArena(rv2), "quit rule: no arena fight without a try");
    }
}
