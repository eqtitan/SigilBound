// Screens for the web's extras.js / v0.21 features: Hard campaign map, Achievements, Hero Codex,
// skill upgrades with Skill Tomes, team presets and gear lock.
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        bool _hardTab, _prepHard, _codex;

        // ---------- campaign: Normal / Hard and Act switches (portrait) ----------
        // The same segmented switches as the landscape top bar (DrawCampaignSwitches, PLAN step 106), at least
        // 48 dp tall. Returns the y below them.
        float CampaignTabs(float y)
        {
            float h = Mathf.Max(48, Ui.Tap);
            DrawCampaignSwitches(new Rect(Pad, y, Ui.W - Pad * 2, h));
            return y + h + 10;
        }
        // The chapters of the act the switch shows, and a locked chapter card's colour: dimmed but opaque, so
        // the Home heroes behind the list don't show through its text.
        (int from, int to) ActChapters(bool hard) => MapAct(hard) == 2 ? (ClassicChapters, Chapters.Length) : (0, ClassicChapters);
        static Color ChapterCard(Color c, bool locked) => locked ? Color.Lerp(c, new Color(0.05f, 0.04f, 0.07f), 0.45f) : new Color(c.r, c.g, c.b, 0.95f);

        void DrawHardCampaign(Rect area)
        {
            int hc = G.S.hardCleared;
            var (c0, c1) = ActChapters(true);
            int open = _chapterOpen >= 0 ? _chapterOpen : Mathf.Clamp(hc / StagesPerChapter, c0, c1 - 1);
            float content = 160 + (c1 - c0) * 70 + 70;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = CampaignTabs(8);
            Ui.Banner(new Rect(Ui.W / 2 - 140, y + 2, 280, 30), "Hard Campaign", 19); y += 40;
            Ui.Text(new Rect(Pad, y, Ui.W - Pad * 2, 48), $"Replay stages you've cleared on Normal ({hc}/{G.S.cleared} done). Enemies are about 60% stronger and 10 levels higher, but pay double gold, extra XP and better gear.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 54;
            for (int c = c0; c < c1; c++)
            {
                int first = c * StagesPerChapter;
                bool locked = first >= G.S.cleared || first > hc;
                bool isOpen = open == c && !locked;
                var r = Row(ref y, isOpen ? 76 + Mathf.Max(48, Ui.Tap) : 60);
                Ui.Panel(r, ChapterCard(Color.Lerp(Palette.Hex("#1a0a0a"), Palette.Panel, 0.4f), locked), Palette.Hex("#7a2a2a"));
                int done = Mathf.Clamp(hc - first, 0, StagesPerChapter);
                Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 60, 24), $"Hard {c + 1}. {Chapters[c]}", 16, locked ? Palette.Dim : Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 14, r.y + 32, r.width - 60, 20), $"Lv {Game.StageLevel(first) + Game.HardLvl}–{Game.StageLevel(first + 5) + Game.HardLvl} · {done}/{StagesPerChapter}", 12, Palette.Dim);
                Ui.Text(new Rect(r.xMax - 64, r.y + 8, 54, 40), locked ? "Locked" : isOpen ? "–" : "+", 13, Palette.Dim);
                if (!locked && Ui.Button(new Rect(r.x, r.y, r.width, 60), "", Ui.Kind.Ghost)) _chapterOpen = isOpen ? 99 : c;
                if (!isOpen) continue;
                float sw = (r.width - 28 - 5 * 6) / 6f;
                for (int s = 0; s < StagesPerChapter; s++)
                {
                    int idx = first + s;
                    var b = new Rect(r.x + 14 + s * (sw + 6), r.y + 64, sw, Mathf.Max(48, Ui.Tap));
                    bool can = G.HardOpen(idx);
                    var kind = idx < hc ? Ui.Kind.Secondary : can ? Ui.Kind.Primary : Ui.Kind.Chip; // red is for destructive actions only (rule 3); the banner says Hard
                    if (Ui.Button(b, Game.IsBossStage(idx) ? "Boss" : Game.StageLabel(idx), kind, 13, can)) OpenPrep(idx, false, ScreenId.Campaign, true);
                }
            }
            Scroller.End();
        }

        // ---------- prep: presets, best team, auto-equip team ----------
        float DrawPresets(float y)
        {
            float t = Mathf.Max(44, Ui.Tap);
            var r = Row(ref y, 60 + Game.TeamPresets * (t + 6) + t);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, 160, t), "Team presets", 14, Palette.Text, FontStyle.Bold);
            if (Ui.Button(new Rect(r.xMax - 134, r.y + 6, 120, t), "Best team", Ui.Kind.Secondary, 12)) { G.TeamBest(); Save(); Toast("Strongest 4 heroes selected"); }
            for (int i = 0; i < Game.TeamPresets; i++)
            {
                float ry = r.y + 14 + t + i * (t + 6);
                var heroes = G.PresetHeroes(i);
                Ui.Text(new Rect(r.x + 14, ry, 60, t), $"Team {i + 1}", 12, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 74, ry, r.width - 74 - 168, t), heroes.Count == 0 ? "empty" : string.Join(", ", heroes.Select(h => Hero(h.id).ShortName)), 11, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.MiddleLeft);
                int k = i;
                if (Ui.Button(new Rect(r.xMax - 164, ry, 72, t), "Load", Ui.Kind.Secondary, 12)) { if (G.TeamLoad(k)) { Save(); Toast($"Team {k + 1} loaded"); } else Toast("That preset is empty. Build a team, then tap Save."); }
                if (Ui.Button(new Rect(r.xMax - 86, ry, 72, t), "Save", Ui.Kind.Secondary, 12)) AskSavePreset(k);
            }
            if (Ui.Button(new Rect(r.x + 14, r.yMax - t - 8, r.width - 28, t), "Auto-equip whole team", Ui.Kind.Secondary, 12)) { G.AutoEquipTeam(); Save(); Toast("Best gear equipped on your team"); }
            return y;
        }

        // ---------- hero: skills with Skill Tome upgrades ----------
        float DrawSkills(HeroSave h, HeroDef t, float y)
        {
            var passive = SkillText.Passive(t);
            float extra = passive != null ? 24 : 0;
            float tap = Mathf.Max(44, Ui.Tap), step = 70 + tap;
            var r = Row(ref y, 52 + extra + t.Skills.Length * step);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 6, 100, 24), "Skills", 15, Palette.Text, FontStyle.Bold);
            if (passive != null) Ui.Text(new Rect(r.x + 14, r.y + 30, r.width - 28, 20), Ui.ColorTag("Passive · ", Palette.Gold) + passive, 12, Palette.Text, FontStyle.Normal);
            Ui.Text(new Rect(r.xMax - 174, r.y + 6, 160, 24), $"{G.S.tomes} Skill Tome{(G.S.tomes == 1 ? "" : "s")}", 12, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
            for (int i = 0; i < t.Skills.Length; i++)
            {
                var s = t.Skills[i]; int lv = h.SkillLv(i), cd = Core.Battle.SkillCooldown(s, lv);
                float sy = r.y + 32 + extra + i * step;
                Ui.Text(new Rect(r.x + 14, sy, r.width - 28, 20), $"A{i + 1} · {s.Name}  {Ui.ColorTag($"Lv {lv + 1}/5", Palette.Gold)}   {Ui.ColorTag(s.Cooldown > 0 ? $"cooldown {cd}" : "basic", Palette.Dim)}", 13, Palette.Text, FontStyle.Bold);
                string bonus = lv > 0 ? Ui.ColorTag($"  +{Mathf.Min(lv, 3) * 8}% power{(lv >= 4 && s.Cooldown > 1 ? ", −1 cooldown" : "")}", Palette.Good) : "";
                Ui.Text(new Rect(r.x + 14, sy + 20, r.width - 28, 40), SkillText.Describe(s) + bonus, 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (lv < Game.SkillMaxLv)
                {
                    int cost = Game.SkillUpCost(lv); int k = i;
                    string next = lv + 1 < 4 ? "+8% power" : s.Cooldown > 1 ? "−1 cooldown" : "mastery";
                    if (Ui.Button(new Rect(r.x + 14, sy + 62, r.width - 28, tap), $"Upgrade · {cost} tome{(cost > 1 ? "s" : "")} · {next}", Ui.Kind.Secondary, 12, G.CanSkillUp(h, i)))
                    { if (G.SkillUp(h, k)) { Save(); Sound.I?.Play("buff"); Toast($"{s.Name} → Lv {lv + 2}"); } }
                }
                else Ui.Text(new Rect(r.x + 14, sy + 62, r.width - 28, tap), "Mastered", 12, Palette.Gold, FontStyle.Bold);
            }
            Ui.Text(new Rect(r.x + 14, r.yMax - 22, r.width - 28, 18), "Skill Tomes: Hard bosses, dungeon floors 5 and 10, achievements, weekly Arena.", 11, Palette.Dim);
            return y;
        }

        // ---------- achievements ----------
        void DrawAchievements(Rect area)
        {
            var list = Game.Achievements;
            float tap = Mathf.Max(44, Ui.Tap);
            float content = 60 + tap + list.Length * 64 + 20;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            // The shared sub-screen header; the claimed count on a dark pill under it (it sat on the 3D scene).
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), "Achievements")) GoBack();
            y += tap + 6;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"{list.Count(G.AchClaimed)}/{list.Length} claimed", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            y += 28;
            foreach (var a in list)
            {
                var r = Row(ref y, 58, 6);
                Ui.SmallCard(r, Palette.Panel, G.AchClaimed(a) ? Palette.Gold : Palette.Line, 10);
                long v = G.AchProgress(a); bool claimed = G.AchClaimed(a), ready = G.AchReady(a);
                Ui.Text(new Rect(r.x + 12, r.y + 4, r.width - 110, 18), $"<b>{a.Name}</b> · {Ui.ColorTag(a.Desc, Palette.Dim)}", 12, Palette.Text);
                Ui.Bar(new Rect(r.x + 12, r.y + 25, r.width - 112, 5), v / (float)a.Goal, Palette.Gold);
                string prog = a.Goal >= 10000 ? $"{Ui.Fmt(v)}/{Ui.Fmt(a.Goal)}" : $"{v}/{a.Goal}";
                Ui.Text(new Rect(r.x + 12, r.y + 33, r.width - 110, 18), $"{prog} · {a.Reward}", 11, Palette.Dim);
                // A disabled button names its state (rule 5): the progress, as on the Home quests.
                string shortProg = a.Goal >= 1000 ? $"{Mathf.FloorToInt(100f * Mathf.Min(1f, v / (float)a.Goal))}%" : $"{Mathf.Min(v, a.Goal)}/{a.Goal}";
                if (Ui.Button(new Rect(r.xMax - 90, r.center.y - tap / 2, 80, tap), claimed ? "Done" : ready ? "Claim" : shortProg, Ui.Kind.Primary, 13, ready))
                {
                    var rw = G.ClaimAch(a.Id);
                    if (rw != null) { Save(); Sound.I?.Play("coin"); Toast($"{a.Name}: {rw}"); }
                }
            }
            Scroller.End();
        }

        // Home card (with a ready count), under Daily Quests.
        float AchievementsCard(float y) { AchievementsCard(Row(ref y, 58)); return y; }
        void AchievementsCard(Rect r)
        {
            Ui.Card(r);
            int n = G.AchReadyCount;
            Ui.Text(new Rect(r.x + 14, r.y + 6, 220, 24), "Achievements", 16, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 14, r.y + 30, r.width - 120, 20), $"{Game.Achievements.Count(G.AchClaimed)}/{Game.Achievements.Length} claimed · rewards for milestones", 11, Palette.Dim);
            Icons.Draw(new Rect(r.xMax - 32, r.center.y - 8, 16, 16), Icon.Next, Palette.Dim);
            Ui.Badge(new Vector2(r.xMax - 52, r.center.y), n); // the one red "something to collect" badge
            if (Ui.Button(r, "", Ui.Kind.Ghost)) Go(ScreenId.Achievements);
        }

        // ---------- hero codex ----------
        void DrawCodex(Rect area)
        {
            var owned = new System.Collections.Generic.HashSet<string>(G.S.heroes.Select(h => h.id));
            var groups = System.Enum.GetValues(typeof(Rarity)).Cast<Rarity>().Reverse().Select(r => (r, list: Heroes.Where(t => t.Rarity == r).ToList())).Where(g => g.list.Count > 0).ToList();
            float cell = (Ui.W - Pad * 2 - 3 * 8) / 4;
            float content = 140 + groups.Sum(g => 34 + Mathf.Ceil(g.list.Count / 4f) * (cell + 8));
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            y = HeroTabs(y, owned.Count);
            Ui.Text(new Rect(Pad, y, Ui.W - Pad * 2, 36), "Every hero in Sigilbound. Undiscovered heroes can be summoned with shards.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            y += 40;
            foreach (var (rar, list) in groups)
            {
                Ui.Text(new Rect(Pad, y, 200, 26), rar.ToString(), 15, Palette.Rarity(rar), FontStyle.Bold); y += 30;
                for (int i = 0; i < list.Count; i++)
                {
                    var t = list[i]; bool known = owned.Contains(t.Id);
                    var r = new Rect(Pad + (i % 4) * (cell + 8), y + (i / 4) * (cell + 8), cell, cell);
                    Ui.Panel(r, known ? Palette.Panel2 : Palette.Hex("#0d0b10"), known ? Palette.Rarity(t.Rarity) : Palette.Line, 10);
                    if (known) Ui.Round(new Rect(r.x + 8, r.y + 8, 6, r.height - 34), Palette.Element(t.Element), null, 3);
                    // Undiscovered: a dark silhouette mark instead of the name.
                    Ui.Text(new Rect(r.x, r.y + 4, r.width, r.height - 28), known ? t.ShortName.Substring(0, 1) : "?", known ? 28 : 30, known ? Palette.Text : Palette.Hex("#3a3344"), FontStyle.Bold, TextAnchor.MiddleCenter);
                    Ui.Text(new Rect(r.x + 2, r.yMax - 24, r.width - 4, 20), known ? t.ShortName : "???", 11, known ? Palette.Text : Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
                    if (Ui.Button(r, "", Ui.Kind.Ghost)) CodexPopup(t, known);
                }
                y += Mathf.Ceil(list.Count / 4f) * (cell + 8) + 4;
            }
            Scroller.End();
        }

        // Heroes / Codex tab chips; returns y below.
        float HeroTabs(float y, int discovered)
        {
            // Mutually exclusive tabs: the segmented switch (rule 4), 48 dp tall.
            float h = Mathf.Max(48, Ui.Tap);
            int hit = Ui.Segmented(new Rect(Pad, y, Ui.W - Pad * 2, h), new[] { $"Heroes ({G.S.heroes.Count})", $"Codex ({discovered}/{Heroes.Length})" }, _codex ? 1 : 0, null, 13);
            if (hit >= 0 && (hit == 1) != _codex) { _codex = hit == 1; _scroll = Vector2.zero; }
            return y + h + 10;
        }

        void CodexPopup(HeroDef t, bool known)
        {
            float h = 190 + t.Skills.Length * 64;
            InfoModal(known ? t.Name : "Undiscovered hero", Mathf.Min(Ui.Safe.height - 40, h), b =>
            {
                float y = b.y;
                Ui.Text(new Rect(b.x, y, b.width, 20), Tag(t), 13, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter); y += 28;
                for (int i = 0; i < t.Skills.Length; i++)
                {
                    var s = t.Skills[i];
                    Ui.Text(new Rect(b.x, y, b.width, 20), $"A{i + 1} · {(known ? s.Name : "???")}   {Ui.ColorTag(s.Cooldown > 0 ? $"cooldown {s.Cooldown}" : "basic", Palette.Dim)}", 13, Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(b.x, y + 20, b.width, 40), SkillText.Describe(s), 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
                    y += 64;
                }
                Ui.Text(new Rect(b.x, y, b.width, 36), known ? "In your roster." : $"Found through {(t.Rarity >= Rarity.Epic ? "Mystic or Radiant" : "any")} shard summons.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (ModalFooter(b, "Close")) CloseModal();
            });
        }
    }
}
