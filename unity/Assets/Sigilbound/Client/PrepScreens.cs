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

        // Enemy chip: element-coloured disc with the initial, name and level; gold ring for a boss.
        static void EnemyChip(Rect r, EnemySpec e)
        {
            var disc = new Rect(r.x, r.y + (r.height - 22) / 2, 22, 22);
            Ui.Round(disc, Color.Lerp(Palette.Element(e.El), Color.black, 0.35f), e.Boss ? Palette.Gold : Palette.Element(e.El), 11);
            Ui.Text(disc, e.Template.ShortName.Substring(0, 1), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
            Ui.Text(new Rect(disc.xMax + 4, r.y, r.width - 26, r.height), $"{e.Template.ShortName} {Ui.ColorTag("Lv" + e.ShownLevel, Palette.Dim)}", 11, Palette.Text); // the boss is marked by its gold ring (the word "Boss" cut names off)
        }

        // ---------- stage list (on the world map, when a chapter is chosen) ----------
        static Rect StageListRect(Rect map, int c)
        {
            float w = Mathf.Min(map.width * 0.62f, 640), h = map.height - 16;
            var pt = c < ClassicChapters ? MapPoints[c] : MapPoints2[c - ClassicChapters]; // act 2 chapters have their own map points
            bool right = pt.x < 0.5f;
            return new Rect(right ? map.xMax - w - 8 : map.x + 8, map.y + 8, w, h);
        }

        void DrawStageList(Rect map, int c, bool hard)
        {
            var r = StageListRect(map, c);
            Ui.Card(r, true);
            int first = c * StagesPerChapter;
            int lvl = hard ? Game.HardLvl : 0;
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 60, 22), $"{c + 1}. {Chapters[c]}{(hard ? " · Hard" : "")}", 15, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 14, r.y + 28, r.width - 60, 16), $"{Game.ChapterElement(c)} · Lv {Game.StageLevel(first) + lvl}–{Game.StageLevel(first + 5) + lvl}", 11, Palette.Dim);
            var close = new Rect(r.xMax - 36, r.y + 8, 28, 28);
            if (Ui.Button(close, "", Ui.Kind.Ghost)) { _chapterOpen = -1; return; }
            Icons.Draw(new Rect(close.x + 7, close.y + 7, 14, 14), Icon.Close, Palette.Dim);
            float top = r.y + 48;
            var story = StoryOf(c);
            if (story != null)
            {
                Ui.Text(new Rect(r.x + 14, top - 2, r.width - 28, 36), story, 11, Palette.Text, FontStyle.Italic, TextAnchor.UpperLeft, true);
                top += 38;
            }
            float rowH = (r.yMax - 8 - top) / StagesPerChapter;
            for (int s = 0; s < StagesPerChapter; s++)
            {
                int idx = first + s;
                var row = new Rect(r.x + 10, top + s * rowH, r.width - 20, rowH - 4);
                bool open = hard ? G.HardOpen(idx) : idx <= G.S.cleared;
                bool clear = hard ? idx < G.S.hardCleared : idx < G.S.cleared;
                bool next = hard ? idx == G.S.hardCleared : idx == G.S.cleared;
                Ui.Round(row, next ? Palette.Hex("#3a2f22") : Palette.Hex("#1a1522"), next ? Palette.Gold : Palette.Bronze, 8);
                // Label (stage number, Boss tag, the route stop's name) and status.
                float lw = Mathf.Clamp(row.width * 0.25f, 104, 160);
                Ui.Text(new Rect(row.x + 8, row.y + 2, lw - 26, row.height * 0.5f), $"{(Game.IsBossStage(idx) ? Ui.ColorTag("Boss ", Palette.Gold) : "")}{Game.StageLabel(idx)}", 12, open ? Palette.Text : Palette.Dim, FontStyle.Bold, TextAnchor.LowerLeft);
                // The stop's name in plain readable type (the serif face is all small caps, which crushed it).
                Ui.Text(new Rect(row.x + 8, row.y + row.height * 0.5f, lw - 10, row.height * 0.5f - 2), StoryScenes.StopName(idx) ?? "", 11, open ? Palette.Hex("#c9b896") : Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft);
                if (clear) Icons.Draw(new Rect(row.x + lw - 18, row.y + row.height * 0.25f - 7, 14, 14), Icon.Check, Palette.Good);
                else if (!open) Icons.Draw(new Rect(row.x + lw - 18, row.y + row.height * 0.25f - 7, 14, 14), Icon.Lock, Palette.Dim);
                // Final wave's enemies.
                var wave = WavesFor(idx, hard).Last();
                float ex = row.x + lw + 4, ew = Mathf.Min(116, (row.width * 0.42f) / wave.Count);
                foreach (var e in wave) { EnemyChip(new Rect(ex, row.y, ew - 4, row.height), e); ex += ew; }
                // Drops: gold, first-clear shards, gear chance.
                var rw = hard ? Game.HardRewards(idx, !clear) : Game.StageRewards(idx, !clear);
                float dx = row.x + lw + 4 + row.width * 0.42f + 4;
                Icons.Draw(new Rect(dx, row.center.y - 7, 14, 14), Icon.Coin, Palette.Gold);
                Ui.Text(new Rect(dx + 16, row.y, 50, row.height), Ui.Fmt(rw.Gold), 11, Palette.Text); dx += 62;
                if (rw.Common > 0 || rw.Mystic > 0 || rw.Radiant > 0)
                {
                    var sc = rw.Radiant > 0 ? Palette.Hex("#ffb02e") : rw.Mystic > 0 ? Palette.Blue : Palette.Hex("#c9a37b");
                    Icons.Draw(new Rect(dx, row.center.y - 7, 14, 14), Icon.Shard, sc);
                    Ui.Text(new Rect(dx + 16, row.y, 24, row.height), "+" + (rw.Common + rw.Mystic + rw.Radiant), 11, Palette.Text); dx += 38;
                }
                // Battle button first, so the gear chance never runs under it.
                var b = new Rect(row.xMax - 78, row.y + 3, 72, row.height - 6);
                if (b.x - 4 - dx > 30) Ui.Text(new Rect(dx, row.y, b.x - 4 - dx, row.height), $"{(b.x - dx > 72 ? "Gear " : "")}{(Game.IsBossStage(idx) ? 90 : 45)}%", 11, Palette.Dim);
                if (Ui.Button(b, next ? "Battle" : clear ? "Replay" : "Locked", next ? Ui.Kind.Primary : Ui.Kind.Secondary, 12, open)) OpenPrep(idx, false, ScreenId.Campaign, hard);
            }
        }

        // ---------- team setup ----------
        void DrawPrepWide(Rect area)
        {
            float pad = 12, gap = 10;
            float lw = area.width * 0.36f, mw = area.width * 0.27f;
            var L = new Rect(area.x + pad, area.y + 8, lw - pad, area.height - 16);
            var M = new Rect(L.xMax + gap, L.y, mw - gap, L.height);
            var R = new Rect(M.xMax + gap, L.y, area.xMax - pad - (M.xMax + gap), L.height);
            var team = G.Team;
            List<List<EnemySpec>> waves = _prepTitan ? null : WavesFor(_prepStage, _prepHard);

            // ---- left: title, team, battle, presets ----
            if (Ui.Button(new Rect(L.x, L.y, 74, 30), "Back", Ui.Kind.Secondary, 12)) Go(_prepBack);
            Ui.Text(new Rect(L.x + 80, L.y, L.width - 80, 30), $"Power {Ui.Fmt(G.TeamPower)}", 13, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
            string title = _prepTitan ? "Titan Challenge" : $"{(_prepHard ? "Hard " : "Stage ")}{Game.StageLabel(_prepStage)} · {StoryScenes.StopName(_prepStage) ?? Chapters[Game.ChapterOf(_prepStage)]}";
            string sub = _prepTitan ? (_prepPractice ? $"{TitanElement(DateTime.Now)} Titan · Practice: no rewards, no try used" : $"{TitanElement(DateTime.Now)} Titan today · {G.S.daily.titanTries} tries left")
                : $"Enemy Lv {Game.StageLevel(_prepStage) + (_prepHard ? Game.HardLvl : 0)} · {waves.Count} waves{(Game.IsBossStage(_prepStage) ? " · Boss" : "")}{(_prepHard ? " · Hard" : "")}";
            Ui.Text(new Rect(L.x, L.y + 34, L.width, 22), title, 15, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(L.x, L.y + 56, L.width, 16), sub, 11, Palette.Dim);
            float sw = (L.width - 3 * 6) / 4f, sy = L.y + 78;
            for (int i = 0; i < 4; i++)
            {
                var slot = new Rect(L.x + i * (sw + 6), sy, sw, 70);
                if (i < team.Count)
                {
                    var h = team[i]; var t = Hero(h.id);
                    Ui.Round(slot, Palette.Hex("#2a2230"), Palette.Rarity(t.Rarity), 10);
                    Icons.Draw(new Rect(slot.x + 6, slot.y + 6, 16, 16), Icons.ForElement(t.Element), Palette.Element(t.Element));
                    Ui.Text(new Rect(slot.x + 24, slot.y + 4, slot.width - 28, 20), t.ShortName, 12, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(slot.x + 6, slot.y + 24, slot.width - 10, 16), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv{h.level}", 10, Palette.Dim);
                    Ui.Text(new Rect(slot.x + 6, slot.y + 44, slot.width - 10, 18), Ui.Fmt(G.Power(h)), 11, Palette.Text, FontStyle.Bold);
                    if (Ui.Button(slot, "", Ui.Kind.Ghost)) { G.S.team.Remove(h.uid); Save(); }
                }
                else
                {
                    Ui.Round(slot, new Color(0, 0, 0, 0.35f), Palette.Bronze, 10);
                    Icons.Draw(new Rect(slot.center.x - 10, slot.center.y - 10, 20, 20), Icon.Plus, Palette.Bronze);
                }
            }
            bool can = team.Count > 0 && (!_prepTitan || _prepPractice || G.S.daily.titanTries > 0);
            if (Ui.Button(new Rect(L.x, sy + 78, L.width, 44), _prepTitan ? (_prepPractice ? "Practice vs the Titan" : "Challenge the Titan") : _prepHard ? "Battle (Hard)" : "Battle", Ui.Kind.Primary, 17, can))
            {
                if (_prepTitan && _prepPractice) Battle.Begin(BattleSpec.TitanPractice());
                else if (_prepHard) Battle.Begin(BattleSpec.ForHard(_prepStage));
                else Battle.Begin(_prepTitan ? -1 : _prepStage);
            }
            float py = sy + 130;
            Ui.Text(new Rect(L.x, py, 120, 22), "Presets", 12, Palette.Text, FontStyle.Bold);
            if (Ui.Button(new Rect(L.xMax - 190, py, 90, 24), "Best team", Ui.Kind.Chip, 11)) { G.TeamBest(); Save(); Toast("Strongest 4 heroes selected"); }
            if (Ui.Button(new Rect(L.xMax - 96, py, 96, 24), "Auto-equip", Ui.Kind.Chip, 11)) { G.AutoEquipTeam(); Save(); Toast("Best gear equipped on your team"); }
            py += 28;
            float rowH = Mathf.Min(28, (L.yMax - py) / Game.TeamPresets - 2);
            for (int i = 0; i < Game.TeamPresets; i++)
            {
                var ph = G.PresetHeroes(i); int k = i; float ry = py + i * (rowH + 2);
                Ui.Text(new Rect(L.x, ry, L.width - 120, rowH), $"{Ui.ColorTag($"Team {i + 1}", Palette.Text)}  {(ph.Count == 0 ? "empty" : string.Join(", ", ph.Select(h => Hero(h.id).ShortName)))}", 11, Palette.Dim);
                if (Ui.Button(new Rect(L.xMax - 112, ry, 54, rowH - 2), "Load", Ui.Kind.Chip, 11)) { if (G.TeamLoad(k)) { Save(); Toast($"Team {k + 1} loaded"); } else Toast("That preset is empty. Build a team, then tap Save."); }
                if (Ui.Button(new Rect(L.xMax - 54, ry, 54, rowH - 2), "Save", Ui.Kind.Chip, 11)) { G.TeamSave(k); Save(); Toast($"Saved as team {k + 1}"); }
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
            Ui.Text(new Rect(M.x + 8, my, M.width - 16, 30), $"{Ui.ColorTag($"{good} strong", Palette.Good)} · {Ui.ColorTag($"{bad} weak", Palette.Bad)} against these foes", 11, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            my += 32;
            if (waves != null)
            {
                float wh = Mathf.Min(48, (M.yMax - 6 - my) / waves.Count);
                for (int w = 0; w < waves.Count; w++)
                {
                    Ui.Text(new Rect(M.x + 8, my, 60, 14), $"Wave {w + 1}", 10, Palette.Dim, FontStyle.Bold);
                    float ex = M.x + 8, ew = (M.width - 16) / waves[w].Count;
                    foreach (var e in waves[w]) { EnemyChip(new Rect(ex, my + 14, ew - 2, wh - 16), e); ex += ew; }
                    my += wh;
                }
            }

            // ---- right: hero picker ----
            Ui.Round(R, new Color(0.07f, 0.05f, 0.1f, 0.75f), Palette.Bronze, 12);
            float fx = R.x + 8, fy = R.y + 8;
            if (Ui.Button(new Rect(fx, fy, 40, 26), "All", _prepFilter == null ? Ui.Kind.ChipOn : Ui.Kind.Chip, 11)) _prepFilter = null;
            fx += 44;
            foreach (var el in Elements)
            {
                var b = new Rect(fx, fy, 30, 26);
                if (Ui.Button(b, "", _prepFilter == el ? Ui.Kind.ChipOn : Ui.Kind.Chip)) _prepFilter = _prepFilter == el ? (Element?)null : el;
                Icons.Draw(new Rect(b.x + 7, b.y + 5, 16, 16), Icons.ForElement(el), Palette.Element(el));
                fx += 34;
            }
            string[] sorts = { "power", "level", "rarity" };
            float sx2 = R.x + 8; fy += 32;
            foreach (var so in sorts)
            {
                if (Ui.Button(new Rect(sx2, fy, 70, 24), char.ToUpper(so[0]) + so.Substring(1), _sort == so ? Ui.Kind.ChipOn : Ui.Kind.Chip, 11)) _sort = so;
                sx2 += 74;
            }
            var list = SortedHeroes().Where(h => _prepFilter == null || Hero(h.id).Element == _prepFilter).ToList();
            var view = new Rect(R.x + 6, fy + 30, R.width - 12, R.yMax - fy - 36);
            float cw = (view.width - 6) / 2, ch = 48;
            _pickScroll = Scroller.Begin(view, _pickScroll, Mathf.Ceil(list.Count / 2f) * (ch + 6));
            for (int i = 0; i < list.Count; i++)
            {
                var h = list[i]; var t = Hero(h.id);
                var r = new Rect((i % 2) * (cw + 6), (i / 2) * (ch + 6), cw, ch);
                bool inTeam = G.S.team.Contains(h.uid);
                Ui.Panel(r, inTeam ? Palette.Hex("#3a2f22") : Palette.Panel2, inTeam ? Palette.Gold : Palette.Rarity(t.Rarity), 9);
                Icons.Draw(new Rect(r.x + 6, r.y + 6, 14, 14), Icons.ForElement(t.Element), Palette.Element(t.Element));
                Ui.Text(new Rect(r.x + 22, r.y + 2, r.width - 26, 20), t.ShortName + (inTeam ? $"  #{G.S.team.IndexOf(h.uid) + 1}" : ""), 12, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 6, r.y + 24, r.width - 10, 20), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)} Lv{h.level} · {Ui.Fmt(G.Power(h))}", 10, Palette.Dim);
                if (Ui.Button(r, "", Ui.Kind.Ghost))
                {
                    if (inTeam) G.S.team.Remove(h.uid);
                    else if (G.S.team.Count < 4) G.S.team.Add(h.uid);
                    else Toast("Team is full (4). Tap a team member to remove them.");
                    Save();
                }
            }
            Scroller.End();
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
