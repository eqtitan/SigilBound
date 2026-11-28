// Polish pass (PLAN step 108, PMO P2-15/16/18/19/23/24): the pop-up queue order, the empty-side guards,
// the turn-order bar agreeing with the real next actor, skill button captions, wording helpers, and
// source checks for the client changes (the client can't run here).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class PolishTests
{
    sealed class Fixed : Random { protected override double Sample() => 0.5; public override double NextDouble() => 0.5; }
    static HeroDef Def(string id, params SkillDef[] sk) => new HeroDef { Id = id, Name = id, Rarity = Rarity.Rare, Element = Element.Flame, Role = Role.Attack, Skills = sk };
    static Unit U(bool ally, HeroDef d, int spd = 100, Element el = Element.Flame) =>
        Battle.MakeUnit(ally, d, new Stats { El = el, Hp = 10000, Atk = 1000, Def = 100, Spd = spd, Crit = 15, Cdmg = 50 });

    public static void Run(Action<bool, string> check)
    {
        Console.WriteLine("\nPolish (P2 items):");
        Queue(check);
        EmptySides(check);
        TurnOrder(check);
        Wording(check);
        Sources(check);
    }

    // ---------- P2-24: one pop-up at a time ----------
    static void Queue(Action<bool, string> check)
    {
        PopupContext All() => new PopupContext
        {
            OnHome = true, WhatsNewPending = true, Act2Pending = true, TipPending = true, UpdateAvailable = true, SinceClose = 10,
        };
        var c = All();
        check(PopupQueue.Next(c) == Popup.WhatsNew, "popups: with everything pending on Home, What's New comes first");
        c.FirstLaunch = true;
        check(PopupQueue.Next(c) == Popup.Tip, "popups: never What's New on the very first launch (the tip goes instead)");
        check(!PopupQueue.WhatsNewOwed(true, 5) && PopupQueue.WhatsNewOwed(false, 5) && !PopupQueue.WhatsNewOwed(false, 0), "popups: What's New is owed only after an update with unseen notes");
        c = All(); c.OnHome = false; c.OnMap = true;
        check(PopupQueue.Next(c) == Popup.Act2Intro, "popups: What's New waits for Home; on the map the Act II intro goes before tips");
        c = All(); c.ModalOpen = true;
        check(PopupQueue.Next(c) == Popup.None, "popups: nothing opens over an open modal");
        c = All(); c.TutorialBlocking = true;
        check(PopupQueue.Next(c) == Popup.None, "popups: nothing opens while the tutorial's coach panel is up (the update banner waits too)");
        c = All(); c.SinceClose = 0.3;
        check(PopupQueue.Next(c) == Popup.None, "popups: a short gap after a modal closes before the next one");
        c = All(); c.LoadProblem = true; c.TutorialBlocking = true;
        check(PopupQueue.Next(c) == Popup.LoadProblem, "popups: a save problem notice comes before everything");
        c = new PopupContext { UpdateAvailable = true, SinceClose = 10 };
        check(PopupQueue.Next(c) == Popup.UpdateBanner, "popups: the update banner shows when nothing else is pending");
        c.InBattle = true;
        check(PopupQueue.Next(c) == Popup.None, "popups: no update banner in battle");
        c = new PopupContext { InBattle = true, TipPending = true, WhatsNewPending = true, OnHome = false, SinceClose = 10 };
        check(PopupQueue.Next(c) == Popup.Tip, "popups: battle tips still show in battle; What's New doesn't");
        // Walk a first launch: tutorial up -> nothing; tutorial done -> tips one by one -> banner last.
        var seq = new List<Popup>();
        c = new PopupContext { FirstLaunch = true, OnHome = true, WhatsNewPending = true, TipPending = true, UpdateAvailable = true, TutorialBlocking = true, SinceClose = 10 };
        seq.Add(PopupQueue.Next(c)); c.TutorialBlocking = false;
        seq.Add(PopupQueue.Next(c)); c.TipPending = false;
        seq.Add(PopupQueue.Next(c));
        check(seq.SequenceEqual(new[] { Popup.None, Popup.Tip, Popup.UpdateBanner }), $"popups: first launch order is tutorial, then tips, then the banner ({string.Join(",", seq)})");

        // Tips.Peek doesn't mark; Next does; MarkShown skips a tip the tutorial covered.
        var g = new Game(seed: 3); g.StartWith("ysolde");
        var peek = Tips.Peek(g, true, false);
        check(peek != null && peek.Id == "welcome" && Tips.Peek(g, true, false) == peek, "popups: Tips.Peek shows the next tip without using it up");
        Tips.MarkShown(g, "welcome");
        check(Tips.Peek(g, true, false)?.Id != "welcome", "popups: a tip the tutorial covered is skipped");
    }

    // ---------- P2-23 / code review L1: empty sides ----------
    static void EmptySides(Action<bool, string> check)
    {
        var d = Def("d", new SkillDef { Name = "Hit", Target = Target.Enemy, Mult = 1 });
        bool threw = false; Battle b = null;
        try { b = new Battle(new List<Unit> { U(true, d) }, new List<List<Unit>>(), new Fixed()); } catch (Exception) { threw = true; }
        check(!threw && b.Over && b.Result == BattleResult.None && b.Enemies.Count == 0, "empty side: no waves at all ends at once with no result (no throw, nothing to reward)");
        threw = false; BattleResult r = BattleResult.Victory;
        try { r = new Battle(new List<Unit>(), new List<List<Unit>> { new List<Unit>() }, new Fixed()).RunAuto(); } catch (Exception) { threw = true; }
        check(!threw && r == BattleResult.Defeat, "empty side: no heroes and no foes is a defeat, and RunAuto doesn't crash");
        threw = false;
        try { b = new Battle(new List<Unit>(), new List<List<Unit>> { new List<Unit> { U(false, d) } }, new Fixed()); r = b.RunAuto(); } catch (Exception) { threw = true; }
        check(!threw && b.Over && r == BattleResult.Defeat, "empty side: no heroes is an immediate defeat");
        var foe = U(false, d);
        b = new Battle(new List<Unit> { U(true, d) }, new List<List<Unit>> { new List<Unit>(), new List<Unit> { foe }, new List<Unit>() }, new Fixed());
        check(!b.Over && b.Waves.Count == 1 && b.Enemies.Contains(foe), "empty side: empty waves are dropped, the real one stays");
        threw = false;
        try { b = new Battle(new List<Unit> { U(true, d, 0) }, new List<List<Unit>> { new List<Unit> { U(false, d, 0) } }, new Fixed()); r = b.RunAuto(5); } catch (Exception) { threw = true; }
        check(!threw && r == BattleResult.None, "empty side: when nobody can ever act (no speed), RunAuto stops instead of crashing");
    }

    // ---------- P2-23 / code review L8: the turn-order bar agrees with NextActor ----------
    static void TurnOrder(Action<bool, string> check)
    {
        // A case where the old 0.7 step and NextActor's 0.07 * 10 disagree (found by search).
        var d = Def("d", new SkillDef { Name = "Hit", Target = Target.Enemy, Mult = 1 });
        var a = U(true, d, 98); var e = U(false, d, 128);
        var b = new Battle(new List<Unit> { a }, new List<List<Unit>> { new List<Unit> { e } }, new Fixed());
        a.Tm = 314; e.Tm = 104;
        var pred = b.PredictTurns(1);
        check(pred.Count == 1 && pred[0] == b.NextActor(), "turn order: the bar's first unit is the real next actor on a near tie");

        // Random 4v4 fights: at every turn the bar's first unit is the one that acts.
        var rng = new Random(11); int turns = 0, wrong = 0;
        for (int bi = 0; bi < 60; bi++)
        {
            var pa = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList(); var pb = Heroes.OrderBy(_ => rng.Next()).Take(4).ToList();
            var bt = new Battle(pa.Select(t => CombatStats.Mk(t, true)).ToList(), new List<List<Unit>> { pb.Select(t => CombatStats.Mk(t, false)).ToList() }, rng);
            for (int n = 0; !bt.Over && n < 400; n++)
            {
                var p = bt.PredictTurns(1);
                var u = bt.NextActor();
                turns++; if (p.Count == 0 || p[0] != u) wrong++;
                var (_, skip) = bt.BeginTurn(u);
                if (!skip) { var c = bt.AiChoose(u); bt.Act(u, c.skill, c.target); }
                bt.EndTurn(u, out _);
            }
        }
        check(wrong == 0 && turns > 1000, $"turn order: the bar's first unit acted next on all {turns} turns of 60 random fights ({wrong} wrong)");
    }

    // ---------- P2-15 / P2-18: captions and names ----------
    static void Wording(Action<bool, string> check)
    {
        int over = 0, clash = 0, cut = 0;
        foreach (var h in Heroes)
        {
            var n = SkillText.ShortNames(h.Skills);
            over += n.Count(x => x.Length > SkillText.ShortMax);
            if (n.Distinct().Count() != n.Length) clash++;
            cut += n.Count(x => x.EndsWith("…"));
        }
        check(over == 0 && clash == 0, $"skill buttons: every hero's short skill captions fit ({SkillText.ShortMax} letters) and differ ({over} too long, {clash} heroes with a clash, {cut} cut with …)");
        check(SkillText.Short("Judgment") == "Judgment" && SkillText.Short("Radiant Judgment") == "Radiant" && SkillText.Short("Storm of Blades") == "Storm", "skill buttons: short names keep whole words");
        var twin = SkillText.ShortNames(new[] { new SkillDef { Name = "Thunder Strike" }, new SkillDef { Name = "Thunder Crashdown" } });
        check(twin[0] != twin[1], $"skill buttons: two skills starting with the same word get different captions ({twin[0]} / {twin[1]})");
        check(RoleName(Role.Hp) == "HP" && RoleName(Role.Attack) == "Attack", "wording: the HP role reads \"HP\", not \"Hp\"");
        check(ShardName(Shard.Common) == "Worn Shard" && ShardName(Shard.Mystic, 3) == "Mystic Shards" && ShardName(Shard.Radiant) == "Radiant Shard", "wording: one name per shard");

        // Weak / Resist hints for the chosen skill.
        var hit = new SkillDef { Name = "Hit", Target = Target.Enemy, Mult = 1 };
        var heal = new SkillDef { Name = "Mend", Target = Target.Ally, Mult = 0 };
        var d = Def("d", hit);
        var flame = U(true, d, 100, Element.Flame);
        check(Battle.Matchup(flame, hit, U(false, d, 100, Element.Gale)) == 1 && Battle.Matchup(flame, hit, U(false, d, 100, Element.Tide)) == -1
              && Battle.Matchup(flame, hit, U(false, d, 100, Element.Flame)) == 0 && Battle.Matchup(flame, heal, U(true, d, 100, Element.Gale)) == 0,
              "targets: Weak/Resist follows the element triangle and only for damaging skills on foes");
    }

    // ---------- client source checks ----------
    static void Sources(Action<bool, string> check)
    {
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "polish: client sources not found here (skipped)"); return; }
        string Src(string f) => File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", f));
        string bc = Src("BattleController.cs"), app = Src("App.cs"), home = Src("Screens.cs"), set = Src("SettingsScreen.cs"), prep = Src("PrepScreens.cs"),
            trials = Src("TrialsScreens.cs"), map = Src("WorldMap.cs"), upd = Src("Updater.cs"), wn = Src("WhatsNew.cs");

        check(!bc.Contains("$\"A{i + 1}\"") && bc.Contains("ShortNames(_actor.Template)"), "skill buttons: named with the skill, not A1/A2/A3");
        check(bc.Contains("MatchupBadge(") && bc.Split("MatchupBadge(new Rect").Length - 1 >= 2, "targets: Weak/Resist badges in both battle layouts");
        check(System.Text.RegularExpressions.Regex.Matches(bc, "RoundButton\\([^;]*Icon\\.Info, \"Status\"").Count >= 2, "status legend: a Status button opens it on any turn (both layouts; portrait uses the 48 dp round button too)");
        check(!bc.Contains("\"Loop\"") && !bc.Contains("\"Cam\"") && !set.Contains("with Cam"), "wording: \"Replay\" (auto-replay) and \"View\" everywhere, no Loop or Cam");
        check(!prep.Contains("next ? \"Battle\" : clear ? \"Replay\"") && prep.Contains("\"Team ▶\""), "wording: the stage row's button says it opens team setup");
        check(!home.Contains("\"Reset save\"") && set.Contains("\"Reset save…\"") && set.Contains("void AskResetSave()") && set.Contains("Store?.AcceptFresh()"), "reset save: moved from Home to Settings > Danger zone, behind a confirm");
        check(prep.Contains("void AskSavePreset(int k)") && !prep.Contains("\"Save\", Ui.Kind.Chip, 12)) { G.TeamSave(k)") && Src("ExtrasScreens.cs").Contains("AskSavePreset(k)"), "presets: Save asks before replacing a different saved team");
        check(!trials.Contains("Battle.Begin(BattleSpec.ForDungeon") && trials.Contains("OpenDungeonPrep(key, f)") && prep.Contains("BattleSpec.ForDungeon(_prepDKey, _prepFloor)"), "dungeon: a floor opens team setup first");
        check(app.Contains("public const float ModalW = 560, WideModalW = 900") && app.Contains("Mathf.Min(mx1 - mx0, _modalMaxW)"), "modals: at most 560 wide (results 900), centred");
        check(bc.Contains("ShowResults(\"HARD CLEARED\"") && bc.Contains("ShowResults(_spec.Practice ? \"Titan Practice\" : \"Titan Battle\"") && bc.Contains("ShowResults($\"FLOOR {floor + 1} CLEARED\""), "results: Hard, Titan and Dungeon use the campaign results layout in landscape");
        check(bc.Contains("_b.Result == BattleResult.None") && bc.Contains("\"Battle over\""), "empty side: the client shows a neutral result, not a defeat");
        int mapTry = map.IndexOf("try { DrawWorldMapInner(area); }", StringComparison.Ordinal);
        check(mapTry > 0 && map.IndexOf("finally { Ui.Cover = null; Ui.OnTop = false; }", mapTry, StringComparison.Ordinal) > mapTry, "world map: Ui.Cover is reset in a finally (code review L2)");
        check(app.Contains("PopupQueue.Next(ctx)") && app.Contains("ShowAct2Intro()") && app.Contains("ShowWhatsNewOnce()") && !app.Contains("void CheckTips()")
              && !home.Contains("MaybeShowWhatsNew") && map.IndexOf("Modal(Act2Title", StringComparison.Ordinal) > map.IndexOf("void ShowAct2Intro()", StringComparison.Ordinal)
              && map.IndexOf("Modal(Act2Title", StringComparison.Ordinal) < map.IndexOf("void DrawWorldMap(", StringComparison.Ordinal),
              "popups: tips, What's New and the Act II intro go through the queue");
        check(upd.Contains("!_app.UpdateBannerAllowed") && wn.Contains("PopupQueue.WhatsNewOwed(_firstLaunch"), "popups: the update banner and What's New ask the queue");
    }
}
