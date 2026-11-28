// Landscape stage list and team setup (docs/sigilbound/PLAN.md step 9). Real data only:
// enemies from Game.StageWaves / HardWaves, drops from Game.StageRewards (+ the gear chance in
// Game.RollStageGear), element matchups from GameData.ElementMultiplier. Portrait keeps the old
// screens (Screens.cs DrawPrep, the chapter list).
using System;
using System.Collections.Generic;
using System.Linq;
using Sigilbound.Core;
using UnityEngine;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        Element? _prepFilter;
        Vector2 _pickScroll;

        static List<List<EnemySpec>> WavesFor(int stage, bool hard) => hard ? Game.HardWaves(stage) : Game.StageWaves(stage);

        // ---------- what team setup is for: a campaign stage (normal or Hard), the Titan, or a dungeon floor ----------
        // Dungeon floors go through team setup too (UX review #17: a tap on the floor row started the run).
        bool _prepDungeon; DungeonKey _prepDKey; int _prepFloor;
        void OpenDungeonPrep(DungeonKey key, int floor)
        {
            _prepDungeon = true; _prepDKey = key; _prepFloor = floor;
            _prepStage = -1; _prepTitan = _prepPractice = _prepHard = false; _prepBack = ScreenId.Dungeon;
            Go(ScreenId.Prep);
        }
        List<List<EnemySpec>> PrepWaves() => _prepTitan ? null : _prepDungeon ? Trials.DungeonWaves(_prepDKey, _prepFloor, G.TodayElement) : WavesFor(_prepStage, _prepHard);
        string PrepTitle() => _prepTitan ? "Titan Challenge" : _prepDungeon ? $"{Trials.Dungeon(_prepDKey).Name} · Floor {_prepFloor + 1}"
            : $"{(_prepHard ? "Hard " : "Stage ")}{Game.StageLabel(_prepStage)} · {StoryScenes.StopName(_prepStage) ?? Chapters[Game.ChapterOf(_prepStage)]}";
        string PrepSub(List<List<EnemySpec>> waves)
        {
            if (_prepTitan) return _prepPractice ? $"{TitanElement(DateTime.Now)} Titan · Practice: no rewards, no try used" : $"{TitanElement(DateTime.Now)} Titan today · {G.S.daily.titanTries} tries left";
            if (_prepDungeon)
            {
                int runs = G.DungeonRunsLeft(_prepDKey), lv = waves.Count > 0 ? waves.SelectMany(w => w).Max(e => e.ShownLevel) : 0;
                return $"Enemy Lv {lv} · {waves.Count} waves{(Trials.IsBossFloor(_prepFloor) ? " · Boss" : "")} · {runs} run{(runs == 1 ? "" : "s")} left today";
            }
            return $"Enemy Lv {Game.StageLevel(_prepStage) + (_prepHard ? Game.HardLvl : 0)} · {waves.Count} waves{(Game.IsBossStage(_prepStage) ? " · Boss" : "")}{(_prepHard ? " · Hard" : "")}";
        }
        string PrepGoLabel() => _prepTitan ? (_prepPractice ? "Practice vs the Titan" : "Challenge the Titan") : _prepDungeon ? $"Fight floor {_prepFloor + 1}" : _prepHard ? "Battle (Hard)" : "Battle";
        bool PrepCanGo() => G.Team.Count > 0 && (!_prepTitan || _prepPractice || G.S.daily.titanTries > 0) && (!_prepDungeon || G.CanEnterFloor(_prepDKey, _prepFloor));
        void PrepGo()
        {
            if (_prepTitan && _prepPractice) Battle.Begin(BattleSpec.TitanPractice());
            else if (_prepDungeon) Battle.Begin(BattleSpec.ForDungeon(_prepDKey, _prepFloor));
            else if (_prepHard) Battle.Begin(BattleSpec.ForHard(_prepStage));
            else Battle.Begin(_prepTitan ? -1 : _prepStage);
        }

        // Preset Save asks before replacing a different saved team (UX review #17: it overwrote silently).
        void AskSavePreset(int k)
        {
            var ph = G.PresetHeroes(k);
            if (ph.Count == 0 || ph.Select(h => h.uid).SequenceEqual(G.S.team)) { G.TeamSave(k); Save(); Toast($"Saved as team {k + 1}"); return; }
            string was = string.Join(", ", ph.Select(h => Hero(h.id).ShortName));
            string now = G.Team.Count == 0 ? "an empty team" : string.Join(", ", G.Team.Select(h => Hero(h.id).ShortName));
            Modal($"Replace team {k + 1}?", 230, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, b.height - 56), $"Team {k + 1} is {was}.\nSave {now} over it?", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                float t = Mathf.Max(44, Ui.Tap);
                if (Ui.Button(new Rect(b.x, b.yMax - t, b.width / 2 - 5, t), "Keep it", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - t, b.width / 2 - 5, t), "Replace")) { G.TeamSave(k); Save(); CloseModal(); Toast($"Saved as team {k + 1}"); }
            });
        }

        // Enemy chip: the element's icon on a dark disc (its shape reads without colour: UX review #4), name and
        // level; gold ring for a boss. When "Name LvN" doesn't fit on one line (the in-house report's "Tamsin Lv"),
        // the level goes under the name, and the font steps down to 10 before a long name is cut with an ellipsis.
        static readonly GUIContent _chipText = new GUIContent();
        static float TextW(string t, int size) { _chipText.text = t; return Ui.Label(size).CalcSize(_chipText).x; }
        static void EnemyChip(Rect r, EnemySpec e, int size = 12)
        {
            float d = Mathf.Min(24, r.height);
            var disc = new Rect(r.x, r.y + (r.height - d) / 2, d, d);
            Ui.ElementBadge(disc, e.El, e.Boss ? Palette.Gold : (Color?)null); // the boss is marked by its gold ring (the word "Boss" cut names off)
            var tr = new Rect(disc.xMax + 4, r.y, r.width - d - 4, r.height);
            string name = e.Template.ShortName, lv = "Lv" + e.ShownLevel;
            if (TextW(name + " " + lv, size) <= tr.width) { Ui.Text(tr, $"{name} {Ui.ColorTag(lv, Palette.Dim)}", size, Palette.Text); return; }
            bool stack = r.height >= 28;
            int fs = size; string n = name;
            while (fs > 10 && TextW(stack ? n : n + " " + lv, fs) > tr.width) fs--;
            while (n.Length > 3 && TextW(stack ? n : n + " " + lv, fs) > tr.width) n = n.Substring(0, n.Length - 2) + "…";
            if (!stack) { Ui.Text(tr, $"{n} {Ui.ColorTag(lv, Palette.Dim)}", fs, Palette.Text); return; }
            float hh = Mathf.Min(16, tr.height / 2);
            Ui.Text(new Rect(tr.x, tr.center.y - hh, tr.width, hh), n, fs, Palette.Text, FontStyle.Normal, TextAnchor.LowerLeft);
            Ui.Text(new Rect(tr.x, tr.center.y, tr.width, hh), lv, Mathf.Min(fs, 11), Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
        }

        // ---------- stage list (on the world map, when a chapter is chosen) ----------
        // area: the map plus the reward track under it (the list covers both).
        static Rect StageListRect(Rect area, int c)
        {
            float w = Mathf.Min(area.width * 0.62f, 660), h = area.height - 16;
            var pt = c < ClassicChapters ? Act1Points[c] : Act2Points[c - ClassicChapters]; // act 2 chapters have their own map points
            bool right = pt.x < 0.5f;
            return new Rect(right ? area.xMax - w - 8 : area.x + 8, area.y + 8, w, h);
        }
        Vector2 _stageScroll; int _stageScrollChapter = -1;

        // Rows are at least 48 dp tall and the whole row is the tap target (UX review #1: the Battle buttons were
        // about 19 dp on a phone); the list scrolls when six rows and the story don't fit, with a fade at the edge.
        void DrawStageList(Rect area, int c, bool hard)
        {
            var r = StageListRect(area, c);
            Ui.Card(r, true);
            int first = c * StagesPerChapter;
            int lvl = hard ? Game.HardLvl : 0;
            float tap = Ui.Tap;
            Ui.Text(new Rect(r.x + 14, r.y + 6, r.width - tap - 24, 24), $"{c + 1}. {Chapters[c]}{(hard ? " · Hard" : "")}", 16, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 14, r.y + 29, r.width - tap - 24, 18), $"{Game.ChapterElement(c)} · Lv {Game.StageLevel(first) + lvl}–{Game.StageLevel(first + 5) + lvl}", 12, Palette.Dim);
            var close = new Rect(r.xMax - tap - 4, r.y + 2, tap, tap);
            if (Ui.Button(close, "", Ui.Kind.Ghost)) { _chapterOpen = -1; return; }
            Icons.Draw(new Rect(close.center.x - 9, close.center.y - 9, 18, 18), Icon.Close, Palette.Text);
            if (_stageScrollChapter != c) { _stageScroll = Vector2.zero; _stageScrollChapter = c; }

            var story = StoryOf(c);
            var view = new Rect(r.x + 8, r.y + Mathf.Max(50, tap + 4), r.width - 16, 0);
            view.height = r.yMax - 8 - view.y;
            float storyH = story != null ? 40 : 0;
            float rowH = Mathf.Max(tap + 6, (view.height - storyH) / StagesPerChapter);
            float contentH = storyH + rowH * StagesPerChapter;
            _stageScroll = Scroller.Begin(view, _stageScroll, contentH);
            if (story != null) Ui.Text(new Rect(6, 0, view.width - 12, 38), story, 12, Palette.Text, FontStyle.Italic, TextAnchor.UpperLeft, true);
            for (int s = 0; s < StagesPerChapter; s++)
            {
                int idx = first + s;
                var row = new Rect(2, storyH + s * rowH, view.width - 4, rowH - 4);
                bool open = hard ? G.HardOpen(idx) : idx <= G.S.cleared;
                bool clear = hard ? idx < G.S.hardCleared : idx < G.S.cleared;
                bool next = hard ? idx == G.S.hardCleared : idx == G.S.cleared;
                Ui.Round(row, next ? Palette.Hex("#3a2f22") : Palette.Hex("#1a1522"), next ? Palette.Gold : Palette.Bronze, 8);
                // Label (stage number, Boss tag, the route stop's name) and status.
                float lw = Mathf.Clamp(row.width * 0.25f, 112, 170);
                Ui.Text(new Rect(row.x + 8, row.y + 2, lw - 26, row.height * 0.5f), $"{(Game.IsBossStage(idx) ? Ui.ColorTag("Boss ", Palette.Gold) : "")}{Game.StageLabel(idx)}", 13, open ? Palette.Text : Palette.Hex("#cfc5b2"), FontStyle.Bold, TextAnchor.LowerLeft);
                // The stop's name in plain readable type (the serif face is all small caps, which crushed it).
                Ui.Text(new Rect(row.x + 8, row.y + row.height * 0.5f, lw - 10, row.height * 0.5f - 2), StoryScenes.StopName(idx) ?? "", 12, open ? Palette.Hex("#d6c7a6") : Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
                if (clear) Icons.Draw(new Rect(row.x + lw - 20, row.y + row.height * 0.25f - 8, 16, 16), Icon.Check, Palette.Good);
                else if (!open) Icons.Draw(new Rect(row.x + lw - 20, row.y + row.height * 0.25f - 8, 16, 16), Icon.Lock, Palette.Dim);
                // Final wave's enemies.
                var wave = WavesFor(idx, hard).Last();
                float ex = row.x + lw + 4, ew = Mathf.Min(120, (row.width * 0.42f) / wave.Count);
                foreach (var e in wave) { EnemyChip(new Rect(ex, row.y, ew - 4, row.height), e); ex += ew; }
                // Drops: gold, first-clear shards, gear chance.
                var rw = hard ? Game.HardRewards(idx, !clear) : Game.StageRewards(idx, !clear);
                float dx = row.x + lw + 4 + row.width * 0.42f + 4;
                Icons.Draw(new Rect(dx, row.center.y - 7, 14, 14), Icon.Coin, Palette.Gold);
                Ui.Text(new Rect(dx + 16, row.y, 50, row.height), Ui.Fmt(rw.Gold), 12, Palette.Text); dx += 62;
                if (rw.Common > 0 || rw.Mystic > 0 || rw.Radiant > 0)
                {
                    var sc = rw.Radiant > 0 ? Palette.Hex("#ffb02e") : rw.Mystic > 0 ? Palette.Blue : Palette.Hex("#c9a37b");
                    Icons.Draw(new Rect(dx, row.center.y - 7, 14, 14), Icon.Shard, sc);
                    Ui.Text(new Rect(dx + 16, row.y, 24, row.height), "+" + (rw.Common + rw.Mystic + rw.Radiant), 12, Palette.Text); dx += 38;
                }
                // The button is the row's label for its action; the whole row takes the tap (first drawn wins in
                // IMGUI, so the button, then the row behind it).
                var b = new Rect(row.xMax - 86, row.y + 3, 82, row.height - 6);
                if (b.x - 4 - dx > 30) Ui.Text(new Rect(dx, row.y, b.x - 4 - dx, row.height), $"{(b.x - dx > 72 ? "Gear " : "")}{(Game.IsBossStage(idx) ? 90 : 45)}%", 12, Palette.Dim);
                // It opens team setup, so it says so (UX review #14: it read "Battle"); a cleared stage has its check mark.
                bool tapped = Ui.Button(b, open ? "Team ▶" : "Locked", next ? Ui.Kind.Primary : Ui.Kind.Secondary, 13, open);
                tapped |= Ui.Button(row, "", Ui.Kind.Ghost);
                if (tapped)
                {
                    if (open) OpenPrep(idx, false, ScreenId.Campaign, hard);
                    else Toast(hard && idx >= G.S.cleared ? $"Clear {Game.StageLabel(idx)} on Normal first" : $"Clear {(hard ? "Hard " : "")}{Game.StageLabel(idx - 1)} first");
                }
            }
            Scroller.End();
            if (contentH > view.height + 1) ScrollFade(view, _stageScroll.y, contentH);
        }

        // A soft dark fade at the edge of a scroll view that has more below (and above, once scrolled).
        static void ScrollFade(Rect view, float scrollY, float contentH)
        {
            if (Event.current.type != EventType.Repaint) return;
            for (int i = 0; i < 6; i++)
            {
                float a = 0.11f * (6 - i) / 6f;
                if (scrollY < contentH - view.height - 1) GUI.DrawTexture(new Rect(view.x, view.yMax - (i + 1) * 3, view.width, 3), Ui.Solid(new Color(0.05f, 0.04f, 0.07f, a * 2.4f)));
                if (scrollY > 1) GUI.DrawTexture(new Rect(view.x, view.y + i * 3, view.width, 3), Ui.Solid(new Color(0.05f, 0.04f, 0.07f, a * 2.4f)));
            }
        }

        // ---------- team setup ----------
        void DrawPrepWide(Rect area)
        {
            float pad = 12, gap = 10;
            float lw = area.width * 0.36f, mw = area.width * (area.width < 900 ? 0.245f : 0.27f); // 16:9 phones: the picker keeps 48 dp filter chips
            var L = new Rect(area.x + pad, area.y + 8, lw - pad, area.height - 16);
            var M = new Rect(L.xMax + gap, L.y, mw - gap, L.height);
            var R = new Rect(M.xMax + gap, L.y, area.xMax - pad - (M.xMax + gap), L.height);
            var team = G.Team;
            List<List<EnemySpec>> waves = PrepWaves();

            // ---- left: back and team tools, title, team, battle, presets (every control 48 dp: UX review #1) ----
            float tap = Mathf.Max(44, Ui.Tap);
            if (Ui.Button(new Rect(L.x, L.y, 76, tap), "Back", Ui.Kind.Secondary, 13)) Go(_prepBack);
            if (Ui.Button(new Rect(L.xMax - 214, L.y, 104, tap), "Best team", Ui.Kind.Chip, 13)) { G.TeamBest(); Save(); Toast("Strongest 4 heroes selected"); }
            if (Ui.Button(new Rect(L.xMax - 106, L.y, 106, tap), "Auto-equip", Ui.Kind.Chip, 13)) { G.AutoEquipTeam(); Save(); Toast("Best gear equipped on your team"); }
            float ly = L.y + tap + 4;
            string title = PrepTitle(), sub = PrepSub(waves);
            Ui.Text(new Rect(L.x, ly, L.width, 22), title, 15, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(L.x, ly + 22, L.width, 18), $"{sub} · {Ui.ColorTag($"Power {Ui.Fmt(G.TeamPower)}", Palette.Gold)}", 12, Palette.Hex("#c9bda6"));
            float sw = (L.width - 3 * 6) / 4f, sy = ly + 44;
            for (int i = 0; i < 4; i++)
            {
                var slot = new Rect(L.x + i * (sw + 6), sy, sw, 66);
                if (i < team.Count)
                {
                    var h = team[i]; var t = Hero(h.id);
                    Ui.Round(slot, Palette.Hex("#2a2230"), Palette.Rarity(t.Rarity), 10);
                    Ui.RarityTab(slot, t.Rarity, 15); // rarity as a letter too, not only the frame colour (UX review #4)
                    Icons.Draw(new Rect(slot.x + 6, slot.y + 6, 16, 16), Icons.ForElement(t.Element), Palette.Element(t.Element));
                    Ui.Text(new Rect(slot.x + 24, slot.y + 4, slot.width - 28, 20), t.ShortName, 12, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(slot.x + 6, slot.y + 23, slot.width - 10, 18), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv{h.level}", 11, Palette.Hex("#c9bda6"));
                    Ui.Text(new Rect(slot.x + 6, slot.y + 42, slot.width - 10, 18), Ui.Fmt(G.Power(h)), 12, Palette.Text, FontStyle.Bold);
                    if (slot.width >= 76) Portraits.Thumb(new Rect(slot.xMax - 32, slot.yMax - 32, 26, 26), t.Id, Palette.Rarity(t.Rarity)); // the face, bottom right
                    if (Ui.Button(slot, "", Ui.Kind.Ghost)) { G.S.team.Remove(h.uid); Save(); }
                }
                else
                {
                    Ui.Round(slot, new Color(0, 0, 0, 0.35f), Palette.Bronze, 10);
                    Icons.Draw(new Rect(slot.center.x - 10, slot.center.y - 10, 20, 20), Icon.Plus, Palette.Bronze);
                }
            }
            if (Ui.Button(new Rect(L.x, sy + 74, L.width, Mathf.Max(48, tap)), PrepGoLabel(), Ui.Kind.Primary, 17, PrepCanGo())) PrepGo();
            // Presets side by side: the name and members, then Load and Save at full tap height.
            float py = sy + 74 + Mathf.Max(48, tap) + 8;
            float pw = (L.width - 2 * 6) / Game.TeamPresets, bh = Mathf.Min(tap, Mathf.Max(36, L.yMax - py - 38));
            for (int i = 0; i < Game.TeamPresets; i++)
            {
                var ph = G.PresetHeroes(i); int k = i; float px = L.x + i * (pw + 6);
                Ui.Round(new Rect(px, py, pw, bh + 38), new Color(0.07f, 0.05f, 0.1f, 0.6f), Palette.Bronze, 8);
                Ui.Text(new Rect(px + 6, py + 2, pw - 12, 16), $"Team {i + 1}", 12, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(px + 6, py + 17, pw - 12, 16), ph.Count == 0 ? "empty" : string.Join(", ", ph.Select(h => Hero(h.id).ShortName)), 11, Palette.Hex("#c9bda6"));
                float hw = (pw - 10) / 2;
                if (Ui.Button(new Rect(px + 3, py + 35, hw, bh), "Load", Ui.Kind.Chip, 12)) { if (G.TeamLoad(k)) { Save(); Toast($"Team {k + 1} loaded"); } else Toast("That preset is empty. Build a team, then tap Save."); }
                if (Ui.Button(new Rect(px + 7 + hw, py + 35, hw, bh), "Save", Ui.Kind.Chip, 12)) AskSavePreset(k);
            }

            // ---- middle: element wheel, matchup, enemy waves ----
            Ui.Round(M, new Color(0.07f, 0.05f, 0.1f, 0.75f), Palette.Bronze, 12);
            var mine = new HashSet<Element>(team.Select(h => Hero(h.id).Element));
            var theirs = new HashSet<Element>(waves == null ? new[] { TitanElement(DateTime.Now) } : waves.SelectMany(w => w).Select(e => e.El));
            float wheel = Mathf.Min(M.width - 24, M.height * 0.48f);
            DrawElementWheel(new Rect(M.center.x - wheel / 2, M.y + 8, wheel, wheel), mine, theirs);
            int good = team.Count(h => theirs.Any(e => ElementMultiplier(Hero(h.id).Element, e) > 1));
            int bad = team.Count(h => theirs.Any(e => ElementMultiplier(e, Hero(h.id).Element) > 1));
            float my = M.y + wheel + 12;
            // Arrows as well as green/red, so the matchup reads without colour (UX review #4).
            Ui.Text(new Rect(M.x + 8, my, M.width - 16, 30), $"{Ui.ColorTag($"▲ {good} strong", Palette.Good)} · {Ui.ColorTag($"▼ {bad} weak", Palette.Bad)} against these foes", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            my += 32;
            if (waves != null)
            {
                float wh = Mathf.Min(48, (M.yMax - 6 - my) / waves.Count);
                for (int w = 0; w < waves.Count; w++)
                {
                    Ui.Text(new Rect(M.x + 8, my, 80, 15), $"Wave {w + 1}", 11, Palette.Hex("#c9bda6"), FontStyle.Bold);
                    float ex = M.x + 8, ew = (M.width - 16) / waves[w].Count;
                    foreach (var e in waves[w]) { EnemyChip(new Rect(ex, my + 14, ew - 2, wh - 16), e); ex += ew; }
                    my += wh;
                }
            }

            // ---- right: hero picker ----
            Ui.Round(R, new Color(0.07f, 0.05f, 0.1f, 0.75f), Palette.Bronze, 12);
            float fx = R.x + 8, fy = R.y + 8;
            // Filter and sort chips at tap height; the element chips fit the column's width.
            float fw = Mathf.Min(tap, (R.width - 16 - 48 - 5 * 3) / 5);
            if (Ui.Button(new Rect(fx, fy, 44, tap), "All", _prepFilter == null ? Ui.Kind.ChipOn : Ui.Kind.Chip, 12)) _prepFilter = null;
            fx += 47;
            foreach (var el in Elements)
            {
                var b = new Rect(fx, fy, fw, tap);
                if (Ui.Button(b, "", _prepFilter == el ? Ui.Kind.ChipOn : Ui.Kind.Chip)) _prepFilter = _prepFilter == el ? (Element?)null : el;
                Icons.Draw(new Rect(b.center.x - 9, b.center.y - 9, 18, 18), Icons.ForElement(el), Palette.Element(el));
                fx += fw + 3;
            }
            string[] sorts = { "power", "level", "rarity" };
            float sx2 = R.x + 8, sw2 = Mathf.Min(84, (R.width - 16 - 8) / 3); fy += tap + 4;
            foreach (var so in sorts)
            {
                if (Ui.Button(new Rect(sx2, fy, sw2, tap), char.ToUpper(so[0]) + so.Substring(1), _sort == so ? Ui.Kind.ChipOn : Ui.Kind.Chip, 12)) _sort = so;
                sx2 += sw2 + 4;
            }
            var list = SortedHeroes().Where(h => _prepFilter == null || Hero(h.id).Element == _prepFilter).ToList();
            var view = new Rect(R.x + 6, fy + tap + 6, R.width - 12, R.yMax - fy - tap - 12);
            float cw = (view.width - 6) / 2, ch = Mathf.Max(48, tap);
            _pickScroll = Scroller.Begin(view, _pickScroll, Mathf.Ceil(list.Count / 2f) * (ch + 6));
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i]; var t = Hero(h.id);
                var r = new Rect((i % 2) * (cw + 6), (i / 2) * (ch + 6), cw, ch);
                bool inTeam = G.S.team.Contains(h.uid);
                if (inTeam) Ui.Panel(r, Palette.Hex("#3a2f22"), Palette.Gold, 9); else Ui.SmallCard(r, Palette.Panel2, Palette.Rarity(t.Rarity), 9); // in-team cards keep their gold highlight
                Ui.RarityTab(r, t.Rarity, 14); // rarity as a letter too, not only the frame colour (UX review #4)
                // Portrait face at the left when there is one; the element icon sits beside the name.
                float px = Portraits.Thumb(new Rect(r.x + 6, r.y + 6, ch - 12, ch - 12), t.Id, Palette.Rarity(t.Rarity)) ? ch : 0;
                Icons.Draw(new Rect(r.x + px + 6, r.y + 6, 14, 14), Icons.ForElement(t.Element), Palette.Element(t.Element));
                Ui.Text(new Rect(r.x + px + 22, r.y + 2, r.width - px - 26, 20), t.ShortName + (inTeam ? $"  #{G.S.team.IndexOf(h.uid) + 1}" : ""), 12, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + px + 6, r.y + 24, r.width - px - 10, 20), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv{h.level} · {Ui.Fmt(G.Power(h))}", 11, Palette.Hex("#c9bda6"));
                if (Ui.Button(r, "", Ui.Kind.Ghost))
                {
                    if (inTeam) G.S.team.Remove(h.uid);
                    else if (G.S.team.Count < 4) G.S.team.Add(h.uid);
                    else Toast("Team is full (4). Tap a team member to remove them.");
                    Save();
                }
            }
            Scroller.End();
            float pickH = Mathf.Ceil(list.Count / 2f) * (ch + 6);
            if (pickH > view.height + 1) ScrollFade(view, _pickScroll.y, pickH);
        }

        // Five elements: Flame → Gale → Tide → Flame as a triangle, Light ↔ Shadow below. Rings mark
        // the elements in your team (gold) and among the foes (red); arrows show who beats whom.
        static void DrawElementWheel(Rect r, HashSet<Element> mine, HashSet<Element> theirs)
        {
            var pos = new Dictionary<Element, Vector2>
            {
                [Element.Flame] = new Vector2(0.5f, 0.12f), [Element.Gale] = new Vector2(0.84f, 0.58f), [Element.Tide] = new Vector2(0.16f, 0.58f),
                [Element.Light] = new Vector2(0.3f, 0.9f), [Element.Shadow] = new Vector2(0.7f, 0.9f),
            };
            Vector2 P(Element e) => new Vector2(r.x + pos[e].x * r.width, r.y + pos[e].y * r.height);
            void Arrow(Element a, Element b)
            {
                Vector2 pa = P(a), pb = P(b);
                for (int i = 2; i < 9; i++)
                {
                    var p = Vector2.Lerp(pa, pb, i / 10f);
                    float s = i == 8 ? 6 : 3;
                    Ui.Round(new Rect(p.x - s / 2, p.y - s / 2, s, s), Palette.Element(a), null, 3);
                }
            }
            foreach (var e in Elements) Arrow(e, Strong(e));
            float n = Mathf.Max(24, r.width * 0.2f);
            foreach (var e in Elements)
            {
                var p = P(e); var node = new Rect(p.x - n / 2, p.y - n / 2, n, n);
                Color ring = mine.Contains(e) && theirs.Contains(e) ? Palette.TrimHi : mine.Contains(e) ? Palette.Gold : theirs.Contains(e) ? Palette.Bad : Palette.Bronze;
                Ui.Round(node, Palette.Hex("#1d1726"), ring, Mathf.RoundToInt(n / 2));
                Icons.Draw(new Rect(node.x + n * 0.2f, node.y + n * 0.2f, n * 0.6f, n * 0.6f), Icons.ForElement(e), Palette.Element(e));
            }
        }
    }
}
