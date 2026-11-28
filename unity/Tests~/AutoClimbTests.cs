// Auto Climb (owner request 2026-11-28; Core/AutoClimb.cs): what comes next in each area (chapter and act
// boundaries, locked stages, runs and tries running out), stopping on the first loss, the summary's totals,
// the saved switch and Replay/Climb exclusivity.
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class AutoClimbTests
{
    public static void Run(Action<bool, string> check)
    {
        var now = new DateTime(2026, 11, 28, 12, 0, 0);
        Game New(int cleared, int hard = 0)
        {
            var g = new Game(seed: 5) { Now = () => now };
            g.StartWith("ysolde"); g.S.cleared = cleared; g.S.hardCleared = hard; g.EnsureDaily(); g.EnsureModes();
            return g;
        }

        // ---- Normal ----
        var n = New(20);
        var t = AutoClimb.NextTarget(n, ClimbArea.Normal, 3);
        check(t.Ok && t.Index == 4, "climb: Normal goes to the next stage (1-4 -> 1-5)");
        t = AutoClimb.NextTarget(n, ClimbArea.Normal, StagesPerChapter - 1);
        check(t.Ok && t.Index == StagesPerChapter && Game.ChapterOf(t.Index) == 1, "climb: Normal crosses into the next chapter after a boss stage");
        t = AutoClimb.NextTarget(n, ClimbArea.Normal, 19);
        check(t.Ok && t.Index == 20, "climb: Normal goes on to the first uncleared stage");
        t = AutoClimb.NextTarget(n, ClimbArea.Normal, 20);
        check(!t.Ok && t.Why == ClimbStop.Locked, "climb: Normal stops when the next stage is locked");
        var act = New(ClassicStages);
        t = AutoClimb.NextTarget(act, ClimbArea.Normal, ClassicStages - 1);
        check(t.Ok && t.Index == ClassicStages && ActOf(Game.ChapterOf(t.Index)) == 2, "climb: Normal crosses from Act I into Act II once unlocked");
        check(AutoClimb.PlaysActIntro(ClimbArea.Normal, ClassicStages - 1, ClassicStages, false) && !AutoClimb.PlaysActIntro(ClimbArea.Normal, ClassicStages - 1, ClassicStages, true)
              && !AutoClimb.PlaysActIntro(ClimbArea.Normal, 10, 11, false) && !AutoClimb.PlaysActIntro(ClimbArea.Hard, ClassicStages - 1, ClassicStages, false),
              "climb: the Act II intro plays once, only when a Normal climb crosses into Act II");
        var end = New(TotalStages);
        t = AutoClimb.NextTarget(end, ClimbArea.Normal, TotalStages - 1);
        check(!t.Ok && t.Why == ClimbStop.ReachedEnd, "climb: Normal stops at the last stage (reached the end)");

        // ---- Hard ----
        var h = New(10, 4);
        t = AutoClimb.NextTarget(h, ClimbArea.Hard, 3);
        check(t.Ok && t.Index == 4, "climb: Hard goes to the next unlocked Hard stage");
        t = AutoClimb.NextTarget(h, ClimbArea.Hard, 4);
        check(!t.Ok && t.Why == ClimbStop.Locked, "climb: Hard stops at a Hard stage not yet unlocked");
        var h2 = New(6, 5);
        h2.CompleteHard(5); // clears Hard 1-6; Normal 2-1 isn't cleared, so Hard 2-1 is locked
        t = AutoClimb.NextTarget(h2, ClimbArea.Hard, 5);
        check(!t.Ok && t.Why == ClimbStop.Locked, "climb: Hard stops when the Normal stage behind the next Hard stage isn't cleared");
        var h3 = New(12, 5); h3.CompleteHard(5);
        t = AutoClimb.NextTarget(h3, ClimbArea.Hard, 5);
        check(t.Ok && t.Index == 6, "climb: Hard crosses the chapter boundary when unlocked");
        t = AutoClimb.NextTarget(New(TotalStages, TotalStages), ClimbArea.Hard, TotalStages - 1);
        check(!t.Ok && t.Why == ClimbStop.ReachedEnd, "climb: Hard stops at the last stage");

        // ---- Dungeons ----
        var d = New(30);
        d.CompleteDungeon(DungeonKey.Vault, 0);
        t = AutoClimb.NextTarget(d, ClimbArea.Dungeon, 0, DungeonKey.Vault);
        check(t.Ok && t.Index == 1 && d.DungeonRunsLeft(DungeonKey.Vault) == Trials.DungeonRuns - 1, "climb: a dungeon goes to the next floor while runs remain");
        d.CompleteDungeon(DungeonKey.Vault, 1); d.CompleteDungeon(DungeonKey.Vault, 2);
        t = AutoClimb.NextTarget(d, ClimbArea.Dungeon, 2, DungeonKey.Vault);
        check(!t.Ok && t.Why == ClimbStop.OutOfRuns && d.DungeonRunsLeft(DungeonKey.Vault) == 0, "climb: a dungeon stops when today's runs are used up");
        t = AutoClimb.NextTarget(d, ClimbArea.Dungeon, 0, DungeonKey.Trial);
        check(!t.Ok && t.Why == ClimbStop.Locked, "climb: another dungeon's floor 2 is locked until floor 1 is cleared");
        var top = New(30); top.S.modes.floorsForge = Trials.DungeonFloors;
        t = AutoClimb.NextTarget(top, ClimbArea.Dungeon, Trials.DungeonFloors - 1, DungeonKey.Forge);
        check(!t.Ok && t.Why == ClimbStop.ReachedEnd, "climb: a dungeon stops after its top floor");

        // ---- Arena ----
        var a = New(20);
        int tries = a.S.daily.arenaTries, fights = 0;
        var rv = a.CurrentRivals()[1];
        while (true)
        {
            check(a.StartArena(rv), $"climb: arena fight {fights + 1} starts (a try is used at the start)");
            a.CompleteArena(rv, true); fights++;
            t = AutoClimb.NextTarget(a, ClimbArea.Arena, 1);
            if (!t.Ok) break;
            check(t.Index == 1, "climb: the arena keeps the same slot (Even)");
            rv = a.CurrentRivals()[t.Index];
            if (fights > 10) break;
        }
        check(fights == tries && t.Why == ClimbStop.OutOfTries && a.S.daily.arenaTries == 0, $"climb: the arena fights back to back until the tries run out ({fights} of {tries})");
        check(AutoClimb.NextTarget(New(6), ClimbArea.Arena, 0).Why == ClimbStop.Locked, "climb: no arena climb before the arena opens");

        // ---- stop on the first loss ----
        check(AutoClimb.AfterFight(n, ClimbArea.Normal, 3, false).Why == ClimbStop.Lost && AutoClimb.AfterFight(d, ClimbArea.Dungeon, 0, false, DungeonKey.Trial).Why == ClimbStop.Lost
              && AutoClimb.AfterFight(n, ClimbArea.Normal, 3, true).Ok, "climb: a loss stops the climb, a win goes on");

        // A real climb from 1-1: fights stage after stage with the engine until the first loss (or 12 stages), with a levelled starter team.
        var c = New(0); c.S.settings.auto = false;
        foreach (var th in c.Team) th.level = 20; // strong enough for most of chapter 1, so the climb runs a while
        AutoClimb.SetClimb(c.S.settings, true);
        var log = new ClimbLog(ClimbArea.Normal);
        var sum = new Reward(); int gearSum = 0, stage = 0, gold0 = c.S.gold, guard = 0;
        while (!log.Stopped && guard++ < 12)
        {
            var b = Battle.ForStage(c, stage);
            bool won = b.RunAuto() == BattleResult.Victory;
            string where = AutoClimb.Where(ClimbArea.Normal, stage);
            if (won)
            {
                var res = c.CompleteStage(stage);
                sum.Gold += res.Reward.Gold; sum.Xp += res.Reward.Xp; sum.Common += res.Reward.Common; sum.Mystic += res.Reward.Mystic; sum.Radiant += res.Reward.Radiant;
                gearSum += res.Gear != null ? 1 : 0;
                log.Win(res.Reward, res.Gear != null ? 1 : 0, res.LevelUps.Select(l => l.hero.id), where);
            }
            else log.Loss(where);
            var next = AutoClimb.AfterFight(c, ClimbArea.Normal, stage, won);
            if (!next.Ok) { log.Finish(next.Why, where); break; }
            stage = next.Index;
        }
        if (!log.Stopped) log.Finish(ClimbStop.ByPlayer, log.Last);
        check(log.Cleared >= 4 && log.Cleared == c.S.cleared, $"climb: a levelled starter team climbs from 1-1 ({log.Cleared} stages, cleared = {c.S.cleared})");
        check(log.Gold == sum.Gold && log.Xp == sum.Xp && log.Common == sum.Common && log.Mystic == sum.Mystic && log.Radiant == sum.Radiant && log.Gear == gearSum
              && c.S.gold - gold0 == sum.Gold, $"climb: summary totals equal the sum of each battle's rewards ({log.Gold} gold, {log.Xp} XP, {log.Common} worn)");
        check(log.Fights == log.Cleared + log.Lost && (log.Lost == 0 || log.Stop == ClimbStop.Lost), "climb: fights = clears + losses, and a loss is the stop");

        // ClimbLog: the first stop wins; rewards of every kind add up; wording.
        var L = new ClimbLog(ClimbArea.Dungeon, DungeonKey.Trial);
        L.Win(new Reward { Gold = 100, Xp = 50, Essence = 8, Common = 1, Mystic = 1, Tome = 1 }, 0, new[] { "Pell Lv 5" }, "Hall of Echoes floor 1");
        L.Win(new Reward { Gold = 200, Xp = 70, Essence = 11, Radiant = 1 }, 2, new[] { "Pell Lv 5", "Odo Lv 6" }, "Hall of Echoes floor 2");
        L.Loss("Hall of Echoes floor 3");
        L.Finish(ClimbStop.Lost, "Hall of Echoes floor 3"); L.Finish(ClimbStop.ByPlayer, "x");
        check(L.Gold == 300 && L.Xp == 120 && L.Essence == 19 && L.Common == 1 && L.Mystic == 1 && L.Radiant == 1 && L.Tome == 1 && L.Gear == 2 && L.LevelUps.Count == 2,
              "climb log: gold, XP, shards, essence, tomes and gear add up; level ups listed once");
        check(L.Stop == ClimbStop.Lost && L.Reason == "Lost at Hall of Echoes floor 3" && L.ClearedNoun == "floors cleared" && L.First == "Hall of Echoes floor 1",
              "climb log: the first stop is kept (lost at X), floors for a dungeon");
        check(AutoClimb.StopText(ClimbStop.OutOfRuns, "") == "Out of dungeon runs for today" && AutoClimb.StopText(ClimbStop.ReachedEnd, "") == "Reached the end"
              && AutoClimb.StopText(ClimbStop.ByPlayer, "") == "Stopped by you" && new ClimbLog(ClimbArea.Arena) { Cleared = 1 }.ClearedNoun == "fight won",
              "climb: stop reasons read as the owner asked");
        check(L.RewardLines().Contains("1 Worn Shard") && L.RewardLines().Contains("1 Radiant Shard") && L.RewardLines().Contains("2 gear pieces"), "climb log: summary lines name the shard kinds");

        // ---- lair reveals ----
        check(AutoClimb.SkipLairReveal(false, true, false) && AutoClimb.SkipLairReveal(true, false, true) && !AutoClimb.SkipLairReveal(true, false, false) && !AutoClimb.SkipLairReveal(false, false, true),
              "climb: a lair reveal already watched is skipped while climbing; a new one plays once");

        // ---- switches: exclusivity, forced Auto ----
        var st = new SettingsSave();
        AutoClimb.SetReplay(st, true); AutoClimb.SetClimb(st, true);
        check(st.autoClimb && !st.autoReplay && st.auto, "climb: turning Climb on turns Replay off and Auto battle on");
        AutoClimb.SetReplay(st, true);
        check(st.autoReplay && !st.autoClimb, "climb: turning Replay on turns Climb off");
        AutoClimb.SetReplay(st, false);
        check(!st.autoReplay && !st.autoClimb, "climb: turning Replay off leaves Climb off");
        var st2 = new SettingsSave(); AutoClimb.SetClimb(st2, true); st2.auto = false;
        check(AutoClimb.AutoBattle(st2, true) && !AutoClimb.AutoBattle(st2, false), "climb: Auto battle is forced on while climbing");
        AutoClimb.SetClimb(st2, false);
        check(!st2.autoClimb && !st2.autoReplay, "climb: turning Climb off leaves Replay off");

        // ---- save: written only when on, round-trips, old saves unchanged ----
        var sg = New(5);
        var plain = SaveCodec.ToJson(sg.S);
        check(!plain.Contains("autoClimb"), "save: Auto Climb off writes no \"autoClimb\" key (old layout unchanged)");
        check(!SaveCodec.FromJson(plain).settings.autoClimb, "save: a save without the key loads with Auto Climb off");
        AutoClimb.SetClimb(sg.S.settings, true);
        var on = SaveCodec.ToJson(sg.S);
        var back = SaveCodec.FromJson(on);
        check(on.Contains("\"autoClimb\"") && back.settings.autoClimb && !back.settings.autoReplay && back.settings.auto, "save: Auto Climb on round-trips (Replay off, Auto on)");
        back.sourceJson = on; AutoClimb.SetClimb(back.settings, false);
        var off = SaveCodec.ToJson(back);
        check(!SaveCodec.FromJson(off).settings.autoClimb, "save: turning it off again is remembered");
        var both = on.Replace("\"autoReplay\":false", "\"autoReplay\":true");
        check(both != on && !SaveCodec.FromJson(both).settings.autoClimb && SaveCodec.FromJson(both).settings.autoReplay, "save: a save with both on loads with Replay only (never both)");
        var mem = new MemStore(); var store = new SaveStore(mem); store.Load(now);
        check(store.Write(sg.S) && SaveStore.TryParse(mem.Get(SaveStore.MainKey), out var parsed, out _) && parsed.settings.autoClimb, "save store: a save with Auto Climb on writes and reads back");
    }

    sealed class MemStore : ISaveStorage
    {
        readonly Dictionary<string, string> _d = new Dictionary<string, string>();
        public string Get(string k) => _d.TryGetValue(k, out var v) ? v : "";
        public void Set(string k, string v) => _d[k] = v;
        public void Flush() { }
    }
}
