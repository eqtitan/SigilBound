// Trials tab (modes.js): the Titan, daily Dungeons and the Arena.
using System;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        DungeonKey _dungeonKey = DungeonKey.Vault;

        static string ElName(Element e) => e.ToString();

        // A Trials card: open, the painted card with the tries left as plain text in it (counts of tries are text, not
        // badges); locked, the shared Ui.LockedCard (opaque and readable, not faded to 50%), which says why on a tap.
        void TrialCard(ref float y, string title, string sub, bool open, string lockText, string left, Action go, float progress = -1)
        {
            var r = Row(ref y, 78, 14);
            if (!open) { if (Ui.LockedCard(r, title, lockText, progress)) Toast(lockText); return; }
            Ui.Card(r, !string.IsNullOrEmpty(left));
            Ui.Text(new Rect(r.x + 16, r.y + 10, r.width - 130, 26), title, 17, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 16, r.y + 36, r.width - 40, 36), sub, 12, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.UpperLeft, true);
            if (!string.IsNullOrEmpty(left)) Ui.Text(new Rect(r.xMax - 120, r.y + 10, 90, 26), left, 13, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
            Icons.Draw(new Rect(r.xMax - 26, r.y + 14, 16, 16), Icon.Next, Palette.Dim);
            if (Ui.Button(r, "", Ui.Kind.Ghost)) go();
        }

        void DrawTrials(Rect area)
        {
            var M = G.EnsureModes(); var A = M.arena; var tier = Trials.TierFor(A.points);
            _scroll = Scroller.Begin(area, _scroll, 460);
            float y = 8;
            Ui.Banner(new Rect(Ui.W / 2 - 130, y + 2, 260, 30), "Trials", 19); y += 44;
            var tel = TitanElement(DateTime.Now); var today = G.TodayElement;
            TrialCard(ref y, Titan.Name, $"Daily boss · {ElName(tel)} today · {G.S.daily.titanTries}/3 tries", true, "", G.S.daily.titanTries > 0 ? $"{G.S.daily.titanTries} left" : "", () => Go(ScreenId.Titan));
            int runs = Enum.GetValues(typeof(DungeonKey)).Cast<DungeonKey>().Sum(k => G.DungeonRunsLeft(k));
            TrialCard(ref y, "Dungeons", $"Gold, hero XP and the gear set you want · today's foes: {ElName(today)}", G.DungeonsOpen, Words.Unlock("1-6"), $"{runs} runs left", () => Go(ScreenId.Dungeon), Mathf.Clamp01(G.S.cleared / 6f));
            TrialCard(ref y, "Arena", $"{tier.Name} · {A.points} points · {G.S.daily.arenaTries}/{Trials.ArenaTries} fights today", G.ArenaOpen, Words.Unlock("2-6"), G.S.daily.arenaTries > 0 ? $"{G.S.daily.arenaTries} left" : "", () => Go(ScreenId.ArenaPvp), Mathf.Clamp01(G.S.cleared / 12f));
            // Below the last card's painted frame band (it grows about 21 units around the card; the line showed through).
            y += 12;
            Ui.TextOnArt(Row(ref y, 36), "Everything resets daily. Every reward is earned by playing: there is nothing to buy.", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            Scroller.End();
        }

        void DrawDungeon(Rect area)
        {
            var key = _dungeonKey; var D = Trials.Dungeon(key);
            int best = G.DungeonFloorsCleared(key), runs = G.DungeonRunsLeft(key);
            float tap = Mathf.Max(44, Ui.Tap);
            float content = 330 + tap * 2 + (key == DungeonKey.Forge ? 60 + 2 * (tap + 6) : 0) + Trials.DungeonFloors * 62;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), "Dungeons")) GoBack();
            y += tap + 6;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"Power {Ui.Fmt(G.TeamPower)}", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            y += 26;
            // The three dungeons: one segmented switch (one "selected" style, 48 dp).
            var names = Trials.Dungeons.Select(d => d.Name).ToArray();
            int sel = Array.FindIndex(Trials.Dungeons, d => d.Key == key);
            int hit = Ui.Segmented(new Rect(Pad, y, Ui.W - Pad * 2, Mathf.Max(48, tap)), names, sel, null, 12);
            if (hit >= 0 && hit != sel) { _dungeonKey = Trials.Dungeons[hit].Key; _scroll = Vector2.zero; }
            y += Mathf.Max(48, tap) + 10;
            var el = G.TodayElement;
            var counters = Elements.Where(e => Strong(e) == el).Select(ElName);
            var r = Row(ref y, 150);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 16, r.y + 10, r.width - 32, 26), D.Name, 18, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 16, r.y + 36, r.width - 32, 40), D.Blurb, 12, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Ui.Text(new Rect(r.x + 16, r.y + 80, r.width - 32, 20), $"Reward: {D.Reward} · Runs today: {Ui.ColorTag($"{runs}/{Trials.DungeonRuns}", Palette.Gold)} (a loss doesn't use a run)", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Ui.Text(new Rect(r.x + 16, r.y + 112, r.width - 32, 34), $"Today's foes lean {ElName(el)} — bring {string.Join(", ", counters)}.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            if (key == DungeonKey.Forge)
            {
                var sets = (GearSet[])Enum.GetValues(typeof(GearSet));
                int rows = (sets.Length + 2) / 3;
                r = Row(ref y, 40 + rows * (tap + 6) + 4);
                Ui.Card(r);
                Ui.Text(new Rect(r.x + 16, r.y + 8, r.width - 32, 20), $"Gear set to forge · {G.ForgeSet}: {SetBonus(G.ForgeSet).desc}", 12, Palette.Hex("#c9bda6"));
                float sw = (r.width - 32 - 8 * 2) / 3;
                for (int i = 0; i < sets.Length; i++)
                {
                    var b = new Rect(r.x + 16 + (i % 3) * (sw + 8), r.y + 34 + (i / 3) * (tap + 6), sw, tap);
                    if (Ui.Button(b, sets[i].ToString(), G.ForgeSet == sets[i] ? Ui.Kind.ChipOn : Ui.Kind.Chip, 12)) { G.ForgeSet = sets[i]; Save(); }
                }
            }
            for (int f = 0; f < Trials.DungeonFloors; f++)
            {
                bool locked = f > best, done = f < best;
                var rw = Trials.DungeonRewards(key, f, !done);
                var fr = Row(ref y, 54, 8);
                if (locked)
                {
                    // The shared locked look (opaque, readable, says why) instead of the whole row at 45%.
                    if (Ui.LockedCard(fr, $"Floor {f + 1}  ·  Lv {Game.StageLevel(Trials.DungeonIdx(f))}", $"Clear floor {f} to unlock")) Toast($"Clear floor {f} to unlock");
                    continue;
                }
                Ui.Card(fr, !done && runs > 0);
                int rar = Trials.ForgeRarity(f);
                string what = (key == DungeonKey.Forge ? $"{Ui.ColorTag(GearRarityNames[rar], Palette.GearRarity(rar))} {G.ForgeSet} gear · " : "") + rw + (rw.Xp > 0 ? $" · {Ui.Fmt(rw.Xp)} XP" : "");
                Ui.Text(new Rect(fr.x + 14, fr.y + 6, fr.width - 70, 22), $"{(Trials.IsBossFloor(f) ? "Boss · " : "")}Floor {f + 1}  {Ui.ColorTag($"Lv {Game.StageLevel(Trials.DungeonIdx(f))}", Palette.Dim)}", 14, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                Ui.Text(new Rect(fr.x + 14, fr.y + 28, fr.width - 70, 20), what, 11, Palette.Dim);
                Ui.Text(new Rect(fr.xMax - 90, fr.y, 60, fr.height), runs <= 0 ? "Tomorrow" : done ? "Done" : "Team", 12, done ? Palette.Good : Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
                Icons.Draw(new Rect(fr.xMax - 28, fr.center.y - 8, 16, 16), done ? Icon.Check : Icon.Next, done ? Palette.Good : Palette.Gold);
                // Team setup first, as for the campaign and the Titan (UX review #17: a tap started the run).
                if (Ui.Button(fr, "", Ui.Kind.Ghost))
                {
                    if (runs <= 0) Toast("No runs left today for this dungeon.");
                    else OpenDungeonPrep(key, f);
                }
            }
            Scroller.End();
        }

        void DrawArenaPvp(Rect area)
        {
            var M = G.EnsureModes(); var A = M.arena; var tier = Trials.TierFor(A.points);
            int ti = Array.IndexOf(Trials.Tiers, tier);
            var next = ti + 1 < Trials.Tiers.Length ? Trials.Tiers[ti + 1] : null;
            var rivals = G.CurrentRivals();
            bool pending = !string.IsNullOrEmpty(A.pendingTier);
            float tap = Mathf.Max(44, Ui.Tap);
            _scroll = Scroller.Begin(area, _scroll, 380 + (pending ? 80 : 0) + rivals.Count * (110 + tap));
            float y = 8;
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), "Arena")) GoBack();
            y += tap + 6;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"Power {Ui.Fmt(G.TeamPower)}", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            y += 26;
            if (pending)
            {
                var pr = Row(ref y, 70);
                Ui.Card(pr, true);
                Ui.Text(new Rect(pr.x + 16, pr.y + 8, pr.width - 130, 22), "Weekly reward", 15, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                Ui.Text(new Rect(pr.x + 16, pr.y + 30, pr.width - 130, 36), $"You finished last week in {A.pendingTier}: {Trials.TierNamed(A.pendingTier).Week}", 11, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (Ui.Button(new Rect(pr.xMax - 100, pr.center.y - tap / 2, 86, tap), "Claim", Ui.Kind.Primary, 14)) { var rw = G.ClaimArenaWeekly(); Save(); if (rw != null) Toast("Weekly reward: " + rw); }
            }
            var r = Row(ref y, 140);
            Ui.Card(r);
            Ui.Heading(new Rect(r.x, r.y + 10, r.width, 34), tier.Name, 26, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(r.x, r.y + 46, r.width, 22), $"{Ui.ColorTag(A.points.ToString(), Palette.Gold)} points" + (next != null ? $" · {next.Min - A.points} to {next.Name}" : ""), 14, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(r.x, r.y + 70, r.width, 20), $"This week {A.wins}W–{A.losses}L · Best {A.best} · Fights today {G.S.daily.arenaTries}/{Trials.ArenaTries}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(r.x + 10, r.y + 94, r.width - 20, 40), $"Weekly reward at your rank: {tier.Week}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
            var hr = Row(ref y, tap);
            Ui.TextOnArt(new Rect(hr.x + 8, hr.y, 200, tap), "Choose a rival", 17, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (Ui.Button(new Rect(hr.xMax - 130, hr.y, 130, tap), "New rivals", Ui.Kind.Secondary, 13)) { G.RefreshRivals(); Save(); }
            foreach (var rv in rivals)
            {
                var cr = Row(ref y, 96 + tap);
                Ui.Card(cr);
                Ui.Text(new Rect(cr.x + 16, cr.y + 8, cr.width - 130, 22), rv.Name, 16, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                Ui.Text(new Rect(cr.x + 16, cr.y + 30, cr.width - 130, 20), $"{rv.Label} · Lv {rv.Level} · Power ~{Ui.Fmt(rv.Power)}", 12, Palette.Dim);
                Ui.Text(new Rect(cr.xMax - 110, cr.y + 8, 96, 40), $"Win {Ui.ColorTag("+" + rv.Gain, Palette.Good)}\nLoss {Ui.ColorTag(Trials.ArenaLoss.ToString(), Palette.Bad)}", 12, Palette.Text, FontStyle.Normal, TextAnchor.UpperRight);
                Ui.Text(new Rect(cr.x + 16, cr.y + 54, cr.width - 32, 26), string.Join("  ·  ", rv.Ids.Select(id => $"{Ui.ColorTag(Hero(id).ShortName, Palette.Rarity(Hero(id).Rarity))}")), 13, Palette.Text);
                // Team setup first, like every other fight (UI/UX review #10: Fight started at once).
                if (Ui.Button(new Rect(cr.x + 16, cr.y + 86, cr.width - 32, tap), G.S.daily.arenaTries > 0 ? "Team \u25B6" : "No fights left today", Ui.Kind.Primary, 15, G.S.daily.arenaTries > 0)) OpenArenaPrep(rv);
            }
            Ui.Text(Row(ref y, 36), "Rivals are matched to your current team. Points soften toward 1000 each Monday.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            Scroller.End();
        }
    }
}
