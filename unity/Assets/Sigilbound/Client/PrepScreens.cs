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
        // The arena goes through team setup too, like every other fight (UI/UX review #10: Fight started at once,
        // and the Climb Summary's "Edit Team" opened the Heroes list).
        bool _prepArena; Rival _prepRival;
        public void OpenArenaPrep(Rival rv)
        {
            if (rv == null) { Go(ScreenId.ArenaPvp); return; }
            _prepArena = true; _prepRival = rv; _prepDungeon = false;
            _prepStage = -1; _prepTitan = _prepPractice = _prepHard = false; _prepBack = ScreenId.ArenaPvp;
            try { HeroModels.Preload(rv.Ids.Where(id => Hero(id) != null).Select(id => (id, Hero(id).Element, false, false)).ToList()); } catch (Exception e) { Debug.LogWarning("Sigilbound: preload failed: " + e.Message); }
            Go(ScreenId.Prep);
        }
        void OpenDungeonPrep(DungeonKey key, int floor)
        {
            _prepArena = false;
            _prepDungeon = true; _prepDKey = key; _prepFloor = floor;
            _prepStage = -1; _prepTitan = _prepPractice = _prepHard = false; _prepBack = ScreenId.Dungeon;
            Go(ScreenId.Prep);
        }
        static List<List<EnemySpec>> RivalWaves(Rival rv) => new List<List<EnemySpec>>
        {
            rv.Ids.Where(id => Hero(id) != null).Select(id => new EnemySpec { Template = Hero(id), Level = rv.Level, ShownLevel = rv.Level, Stars = rv.Stars, Mul = 1, El = Hero(id).Element }).ToList(),
        };
        List<List<EnemySpec>> PrepWaves() => _prepTitan ? null : _prepArena ? RivalWaves(_prepRival) : _prepDungeon ? Trials.DungeonWaves(_prepDKey, _prepFloor, G.TodayElement) : WavesFor(_prepStage, _prepHard);
        string PrepTitle() => _prepTitan ? "Titan Challenge" : _prepArena ? $"Arena · vs {_prepRival.Name}" : _prepDungeon ? $"{Trials.Dungeon(_prepDKey).Name} · Floor {_prepFloor + 1}"
            : $"{(_prepHard ? "Hard " : "Stage ")}{Game.StageLabel(_prepStage)} · {StoryScenes.StopName(_prepStage) ?? Chapters[Game.ChapterOf(_prepStage)]}";
        string PrepSub(List<List<EnemySpec>> waves)
        {
            if (_prepTitan) return _prepPractice ? $"{TitanElement(DateTime.Now)} Titan · Practice: no rewards, no try used" : $"{TitanElement(DateTime.Now)} Titan today · {G.S.daily.titanTries} tries left";
            if (_prepArena) return $"{_prepRival.Label} · Lv {_prepRival.Level} · Power ~{Ui.Fmt(_prepRival.Power)} · {G.S.daily.arenaTries} fights left";
            if (_prepDungeon)
            {
                int runs = G.DungeonRunsLeft(_prepDKey), lv = waves.Count > 0 ? waves.SelectMany(w => w).Max(e => e.ShownLevel) : 0;
                return $"Enemy Lv {lv} · {waves.Count} waves{(Trials.IsBossFloor(_prepFloor) ? " · Boss" : "")} · {runs} run{(runs == 1 ? "" : "s")} left today";
            }
            return $"Enemy Lv {Game.StageLevel(_prepStage) + (_prepHard ? Game.HardLvl : 0)} · {waves.Count} waves{(Game.IsBossStage(_prepStage) ? " · Boss" : "")}{(_prepHard ? " · Hard" : "")}";
        }
        // One word for the fight button everywhere: Battle (UI/UX review #16: "Fight floor N", "Challenge the Titan").
        string PrepGoLabel() => _prepTitan && _prepPractice ? "Battle (practice)" : _prepHard ? "Battle (Hard)" : "Battle";
        bool PrepCanGo() => G.Team.Count > 0 && (!_prepTitan || _prepPractice || G.S.daily.titanTries > 0) && (!_prepDungeon || G.CanEnterFloor(_prepDKey, _prepFloor)) && (!_prepArena || G.S.daily.arenaTries > 0);
        void PrepGo()
        {
            if (_prepArena) Battle.Begin(BattleSpec.ForArena(_prepRival));
            else if (_prepTitan && _prepPractice) Battle.Begin(BattleSpec.TitanPractice());
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
                int hit = Ui.ButtonRow(new Rect(b.x, b.yMax - t, b.width, t), new[] { ("Keep it", UiRole.Secondary, true), ("Replace", UiRole.Primary, true) });
                if (hit == 0) CloseModal(); else if (hit == 1) { G.TeamSave(k); Save(); CloseModal(); Toast($"Saved as team {k + 1}"); }
            });
        }

        // Enemy chip: the element's icon on a dark disc (its shape reads without colour: UX review #4), name and
        // level; gold ring for a boss. When "Name LvN" doesn't fit on one line (the in-house report's "Tamsin Lv"),
        // the level goes under the name, and the font steps down to 11 (the text floor) before a long name is cut.
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
            while (fs > Ui.MinText && TextW(stack ? n : n + " " + lv, fs) > tr.width) fs--;
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
            var pt = c < ClassicChapters ? Act1Points[c] : Act2Points[c - ClassicChapters]; // act 2 chapters have their own map points
            return Ui.ToRect(UiLayout.StageList(Ui.ToBox(area), pt.x < 0.5f, Ui.FullW)); // nearly the whole map when compact
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
                Ui.Round(row, next ? Palette.Hex("#3a2f22") : Palette.CardAlt, next ? Palette.Gold : Palette.Bronze, 8);
                // Label (stage number, Boss tag, the route stop's name) and status.
                float lw = Mathf.Clamp(row.width * 0.25f, 112, 170);
                Ui.Text(new Rect(row.x + 8, row.y + 2, lw - 26, row.height * 0.5f), $"{(Game.IsBossStage(idx) ? Ui.ColorTag("Boss ", Palette.Gold) : "")}{Game.StageLabel(idx)}", 13, open ? Palette.Text : Palette.Hex("#cfc5b2"), FontStyle.Bold, TextAnchor.LowerLeft);
                // The stop's name in plain readable type (the serif face is all small caps, which crushed it).
                Ui.Text(new Rect(row.x + 8, row.y + row.height * 0.5f, lw - 10, row.height * 0.5f - 2), StoryScenes.StopName(idx) ?? "", 12, open ? Palette.Hex("#d6c7a6") : Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
                if (clear) Icons.Draw(new Rect(row.x + lw - 20, row.y + row.height * 0.25f - 8, 16, 16), Icon.Check, Palette.Good);
                else if (!open) Icons.Draw(new Rect(row.x + lw - 20, row.y + row.height * 0.25f - 8, 16, 16), Icon.Lock, Palette.Dim);
                // Right to left (Core/UiLayout.StageRow): the Team button, the drops, then the enemies in what is left,
                // so the shard drop no longer runs under the button on narrow screens.
                var wave = WavesFor(idx, hard).Last();
                var rw = hard ? Game.HardRewards(idx, !clear) : Game.StageRewards(idx, !clear);
                bool shards = rw.Common > 0 || rw.Mystic > 0 || rw.Radiant > 0;
                var L = UiLayout.StageRow(Ui.ToBox(row), wave.Count, shards);
                float ex = L.Enemies.X;
                foreach (var e in wave) { EnemyChip(new Rect(ex, row.y, L.EnemyW - 4, row.height), e); ex += L.EnemyW; }
                // Drops: gold, first-clear shards (the same pictures as the top bar), gear chance when there's room.
                var gr = Ui.ToRect(L.Gold);
                Icons.Currency(new Rect(gr.x, row.center.y - 7, 14, 14), Icons.Cur.Gold);
                Ui.Text(new Rect(gr.x + 16, row.y, gr.width - 16, row.height), Ui.Fmt(rw.Gold), 12, Palette.Text);
                if (shards)
                {
                    var sr = Ui.ToRect(L.Shard);
                    Icons.Currency(new Rect(sr.x, row.center.y - 7, 14, 14), rw.Radiant > 0 ? Icons.Cur.RadiantShard : rw.Mystic > 0 ? Icons.Cur.MysticShard : Icons.Cur.WornShard);
                    Ui.Text(new Rect(sr.x + 16, row.y, sr.width - 16, row.height), "+" + (rw.Common + rw.Mystic + rw.Radiant), 12, Palette.Text);
                }
                if (L.Gear.W > 0) Ui.Text(Ui.ToRect(L.Gear), $"Gear {(Game.IsBossStage(idx) ? 90 : 45)}%", 12, Palette.Hex("#c9bda6"));
                var b = Ui.ToRect(L.Button);
                // It opens team setup, so it says so (UX review #14: it read "Battle"); a cleared stage has its check mark.
                bool tapped = Ui.Button(b, open ? "Team \u25B6" : "Locked", next ? Ui.Kind.Primary : Ui.Kind.Secondary, 13, open);
                tapped |= Ui.Button(row, "", Ui.Kind.Ghost);
                if (tapped)
                {
                    if (open) OpenPrep(idx, false, ScreenId.Campaign, hard);
                    else Toast(hard && idx >= G.S.cleared ? $"Clear stage {Game.StageLabel(idx)} on Normal to unlock" : Words.Unlock((hard ? "Hard " : "") + Game.StageLabel(idx - 1)));
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
        // Core/UiLayout.Prep: one header row (Back, title, Best team, Auto-equip), then team | waves | picker at 16:9
        // and wider, or team (waves under it) | picker below 900 units, where Back and Best team overlapped and the
        // slot names were cut (UI/UX review #2).
        void DrawPrepWide(Rect area)
        {
            float tap = Mathf.Max(44, Ui.Tap);
            var P = UiLayout.Prep(Ui.ToBox(area), tap, Ui.FullW);
            var team = G.Team;
            List<List<EnemySpec>> waves = PrepWaves();

            // ---- header ----
            if (Ui.BackButton(Ui.ToRect(P.Back))) GoBack();
            var titleR = Ui.ToRect(P.Title);
            Ui.TextOnArt(titleR, PrepTitle(), Ui.FitSize(PrepTitle(), titleR.width - 20, 17, 12, true), Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (Ui.Button(Ui.ToRect(P.Best), "Best team", Ui.Kind.Chip, 13)) { G.TeamBest(); Save(); Toast("Strongest 4 heroes selected"); }
            if (Ui.Button(Ui.ToRect(P.AutoEquip), "Auto-equip", Ui.Kind.Chip, 13)) { G.AutoEquipTeam(); Save(); Toast("Best gear equipped on your team"); }

            // ---- team: the sub line, four slots, Battle, presets ----
            var T = UiLayout.Team(P.Team, tap);
            var Lr = Ui.ToRect(P.Team);
            Ui.Text(Ui.ToRect(T.Sub), $"{PrepSub(waves)} · {Ui.ColorTag($"Power {Ui.Fmt(G.TeamPower)}", Palette.Gold)}", 12, Palette.Hex("#c9bda6"));
            for (int i = 0; i < 4; i++)
            {
                var slot = Ui.ToRect(T.Slots[i]);
                if (i < team.Count)
                {
                    var h = team[i]; var t = Hero(h.id);
                    Ui.Round(slot, Palette.Panel2, Palette.Rarity(t.Rarity), 10);
                    Ui.RarityTab(slot, t.Rarity, 15); // rarity as a letter too, not only the frame colour (UX review #4)
                    Ui.ElementBadge(new Rect(slot.x + 4, slot.y + 3, 17, 17), t.Element); // elements are always the badge (UI/UX review #12)
                    // The name on its own line, the slot's full width (it was cut to "Ysold" beside the icon).
                    Ui.Text(new Rect(slot.x + 5, slot.y + 20, slot.width - 8, 16), t.ShortName, 12, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(slot.x + 5, slot.y + 35, slot.width - 8, 15), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv {h.level}", 11, Palette.Hex("#c9bda6"));
                    Ui.Text(new Rect(slot.x + 5, slot.y + 50, slot.width - 8, 15), Ui.Fmt(G.Power(h)), 11, Palette.Text, FontStyle.Bold);
                    if (slot.width >= 84) Portraits.Thumb(new Rect(slot.xMax - 30, slot.yMax - 30, 24, 24), t.Id, Palette.Rarity(t.Rarity)); // the face, bottom right
                    if (Ui.Button(slot, "", Ui.Kind.Ghost)) { G.S.team.Remove(h.uid); Save(); }
                }
                else
                {
                    Ui.Round(slot, new Color(0, 0, 0, 0.35f), Palette.Bronze, 10);
                    Icons.Draw(new Rect(slot.center.x - 10, slot.center.y - 10, 20, 20), Icon.Plus, Palette.Bronze);
                }
            }
            if (Ui.Button(Ui.ToRect(T.Go), PrepGoLabel(), Ui.Kind.Primary, 17, PrepCanGo())) PrepGo();
            // Presets: one 48 dp chip each ("Team 2 · Mira, Odo"); Load and Save in a small dialog, so neither
            // shrinks to 30 units on a tablet.
            for (int i = 0; i < Game.TeamPresets; i++)
            {
                var ph = G.PresetHeroes(i); int k = i; var pr = Ui.ToRect(T.Presets[i]);
                // A card, not a chip plate: two lines of text sat on the chip art's crown ornaments (L028). One line
                // inside the frame's content rect: "Team 2 · Mira, Odo".
                var pin = Ui.CardFrame(pr, false, Palette.Bronze);
                string who = ph.Count == 0 ? "empty" : string.Join(", ", ph.Select(h => Hero(h.id).ShortName));
                Ui.Text(new Rect(pin.x + 3, pin.y, pin.width - 6, pin.height), $"Team {i + 1} {Ui.ColorTag("· " + who, Palette.Hex("#c9bda6"))}", 12, Palette.Text, FontStyle.Bold);
                if (Ui.Button(pr, "", Ui.Kind.Ghost)) PresetModal(k);
            }

            // ---- waves: the wheel and matchup (wide), or the matchup and waves under the team (compact) ----
            var theirs = new HashSet<Element>(waves == null ? new[] { TitanElement(DateTime.Now) } : waves.SelectMany(w => w).Select(e => e.El));
            int good = team.Count(h => theirs.Any(e => ElementMultiplier(Hero(h.id).Element, e) > 1));
            int bad = team.Count(h => theirs.Any(e => ElementMultiplier(e, Hero(h.id).Element) > 1));
            // Words as well as green and red, so the matchup reads without colour (UX review #4).
            string matchup = $"{Ui.ColorTag($"{good} strong", Palette.Good)} · {Ui.ColorTag($"{bad} weak", Palette.Bad)} against these foes";
            Rect M; float my;
            if (P.Wheel)
            {
                M = Ui.ToRect(P.Waves);
                Ui.Round(M, new Color(0.07f, 0.05f, 0.1f, 0.75f), Palette.Bronze, 12);
                var mine = new HashSet<Element>(team.Select(h => Hero(h.id).Element));
                float wheel = Mathf.Min(M.width - 24, M.height * 0.44f);
                DrawElementWheel(new Rect(M.center.x - wheel / 2, M.y + 8, wheel, wheel), mine, theirs);
                my = M.y + wheel + 10;
                // The wheel's legend: what the ring colours mean (UI/UX review #11).
                Ui.Text(new Rect(M.x + 8, my, M.width - 16, 16), $"{Ui.ColorTag("gold", Palette.Gold)} ring = your team · {Ui.ColorTag("red", Palette.Bad)} ring = foes", 11, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.MiddleCenter);
                my += 18;
            }
            else
            {
                M = Ui.ToRect(T.Rest);
                if (M.height >= 30) Ui.Round(M, new Color(0.07f, 0.05f, 0.1f, 0.75f), Palette.Bronze, 12);
                my = M.y + 2;
            }
            if (M.height >= 30)
            {
                Ui.Text(new Rect(M.x + 8, my, M.width - 16, 26), matchup, 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
                my += 28;
                if (waves != null && M.yMax - 6 - my >= 24)
                {
                    float wh = Mathf.Min(48, (M.yMax - 6 - my) / waves.Count);
                    bool oneLine = wh < 38; // compact: "Wave 1" beside its chips
                    for (int w = 0; w < waves.Count; w++)
                    {
                        float lx = oneLine ? 54 : 0;
                        Ui.Text(new Rect(M.x + 8, my, oneLine ? 50 : 80, oneLine ? wh : 15), $"Wave {w + 1}", 11, Palette.Hex("#c9bda6"), FontStyle.Bold);
                        float ex = M.x + 8 + lx, ew = (M.width - 16 - lx) / waves[w].Count;
                        foreach (var e in waves[w]) { EnemyChip(oneLine ? new Rect(ex, my + 1, ew - 2, wh - 2) : new Rect(ex, my + 14, ew - 2, wh - 16), e); ex += ew; }
                        my += wh;
                    }
                }
            }

            // ---- right: hero picker ----
            var R = Ui.ToRect(P.Picker);
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
            // Core/UiLayout.Picker: the grid under the chips (one column when two would be under 160 units each).
            var grid = UiLayout.Picker(Ui.ToBox(R), tap);
            var view = Ui.ToRect(grid.View);
            float pickH = Mathf.Ceil(list.Count / (float)grid.Cols) * (grid.CardH + grid.Gap);
            _pickScroll = Scroller.Begin(view, _pickScroll, pickH);
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i]; var t = Hero(h.id);
                var r = Ui.ToRect(grid.Cell(i));
                bool inTeam = G.S.team.Contains(h.uid);
                HeroPickCard(r, t, inTeam, inTeam ? G.S.team.IndexOf(h.uid) : -1, $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv {h.level} · {Ui.Fmt(G.Power(h))}");
                if (Ui.Button(r, "", Ui.Kind.Ghost))
                {
                    if (inTeam) G.S.team.Remove(h.uid);
                    else if (G.S.team.Count < 4) G.S.team.Add(h.uid);
                    else Toast("Team is full (4). Tap a team member to remove them.");
                    Save();
                }
            }
            Scroller.End();
            if (pickH > view.height + 1) ScrollFade(view, _pickScroll.y, pickH);
        }

        // A preset's Load and Save at full size.
        void PresetModal(int k)
        {
            var ph = G.PresetHeroes(k);
            Modal($"Team {k + 1}", 200, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, b.height - 60), ph.Count == 0 ? "This preset is empty. Build a team, then tap Save." : string.Join(", ", ph.Select(h => Hero(h.id).ShortName)), 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                float t = Mathf.Max(44, Ui.Tap);
                int hit = Ui.ButtonRow(new Rect(b.x, b.yMax - t, b.width, t), new[] { ("Cancel", UiRole.Secondary, true), ("Save here", UiRole.Secondary, true), ("Load", UiRole.Primary, ph.Count > 0) });
                if (hit == 0) CloseModal();
                else if (hit == 1) { CloseModal(); AskSavePreset(k); }
                else if (hit == 2) { CloseModal(); if (G.TeamLoad(k)) { Save(); Toast($"Team {k + 1} loaded"); } }
            });
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
                Ui.Round(node, Palette.Card, ring, Mathf.RoundToInt(n / 2));
                Icons.Draw(new Rect(node.x + n * 0.2f, node.y + n * 0.2f, n * 0.6f, n * 0.6f), Icons.ForElement(e), Palette.Element(e));
            }
        }
    }
}
