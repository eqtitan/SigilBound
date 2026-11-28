// Menu screens (immediate-mode GUI on the 390-point logical canvas).
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        const float Pad = 16;
        static Rect Row(ref float y, float h, float gap = 10) { var r = new Rect(Pad, y, Ui.W - Pad * 2, h); y += h + gap; return r; }

        string HeroLine(HeroSave h)
        {
            var t = Hero(h.id);
            return $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)}  {t.Name}  {Ui.ColorTag($"Lv {h.level}", Palette.Dim)}";
        }
        string Tag(HeroDef t) => $"{Ui.ColorTag(t.Rarity.ToString(), Palette.Rarity(t.Rarity))} · {Ui.ColorTag(t.Element.ToString(), Palette.Element(t.Element))} · {RoleName(t.Role)}";

        // ---------- intro ----------
        void DrawIntro(Rect area)
        {
            // Title screen: the owner's citadel painting behind everything (skin "bg_title").
            Ui.Backdrop(new Rect(Ui.Left, 0, Ui.FullW, Ui.H), "bg_title", 0.25f);
            float y = area.y + 20;
            Ui.Heading(new Rect(0, y, Ui.W, 44), "SIGILBOUND", 34, TextAnchor.MiddleCenter); y += 48;
            // A soft dark band so the line reads over the painting's bright sigils.
            float bw = Mathf.Min(Ui.W - 40, 560);
            Ui.Round(new Rect(Ui.W / 2 - bw / 2, y - 2, bw, 44), new Color(0.04f, 0.03f, 0.07f, 0.72f), null, 14);
            Ui.Text(new Rect(30, y, Ui.W - 60, 40), "The Sigil has shattered. Choose the first champion to answer your call.", 14, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            // Starter cards 18 apart (their painted frames ran into each other: UI/UX review #19), each with a
            // Choose button; rarity and element as a letter tab and an icon badge, not two coloured words.
            y = area.yMax - 3 * 100 - 30;
            foreach (var id in Starters)
            {
                var t = Hero(id);
                var r = Row(ref y, 82, 18);
                Ui.Card(r);
                Ui.RarityTab(r, t.Rarity, 16);
                Ui.ElementBadge(new Rect(r.x + 12, r.y + 10, 24, 24), t.Element);
                float cw = Mathf.Max(Ui.Tap * 1.8f, 92);
                Ui.Text(new Rect(r.x + 42, r.y + 8, r.width - cw - 60, 26), t.Name, 18, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 14, r.y + 36, r.width - cw - 30, 20), $"{t.Rarity} · {t.Element} · {RoleName(t.Role)}", 13, Palette.Hex("#d6c7a6"));
                Ui.Text(new Rect(r.x + 14, r.y + 56, r.width - cw - 30, 20), string.Join(" · ", t.Skills.Select(s => s.Name)), 12, Palette.Text);
                bool pick = Ui.Button(new Rect(r.xMax - cw - 12, r.center.y - Ui.Tap / 2, cw, Ui.Tap), "Choose", Ui.Kind.Primary, 14);
                if (pick | Ui.Button(r, "", Ui.Kind.Ghost))
                {
                    G.StartWith(id); Save(); Go(ScreenId.Home); Tutorial.Start();
                    InfoModal("Welcome!", 230, b =>
                    {
                        Ui.Text(new Rect(b.x, b.y, b.width, 90), $"{t.Name} answers your call, joined by Pell and Odo. Summon more heroes with your shards, then fight in the Campaign.", 15, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                        if (ModalFooter(b, "Let's go")) CloseModal();
                    });
                }
            }
        }

        // ---------- home ----------
        // Landscape (UX review #2): the team stands in the middle in 3D, daily login and quests on the left, and
        // "Continue Campaign" pinned on the right, always visible (the welcome tip points at it).
        // Portrait (in-house report S4: the cards covered the team): HomeLayout keeps the team's band of the
        // screen clear. Login, quests and achievements fold into a strip of tabs under the title (each with its
        // ready count); a tab opens its card in a sheet under the team's feet; the button stays pinned above the nav.
        public static Rect HomeCta; // where "Continue Campaign" is (the tutorial points at it)
        Vector2 _homeLeftScroll, _homeSheetScroll;
        float _homeShiftX; // compact landscape Home: the menu camera's sideways shift (Arena.MenuShift)
        int _homeTab = -1; // portrait: the open card (0 login, 1 quests, 2 achievements), -1 none

        static Rect R(URect r) => new Rect((float)r.X, (float)r.Y, (float)r.W, (float)r.H);

        void DrawHome(Rect area)
        {
            if (Ui.Landscape) { DrawHomeWide(area); return; }
            var lay = HomeLayout.Portrait(new URect(area.x, area.y, area.width, area.height), Ui.H, Ui.Tap, Pad);
            var title = R(lay.title); var strip = R(lay.strip); var sheet = R(lay.sheet); var cta = R(lay.cta);
            LoadingPlaceholder.DrawLineup(Arena != null ? Arena.Cam : null, _lineup); // soft cards where heroes are still loading
            Ui.Heading(new Rect(0, title.y, Ui.W, 36), "SIGILBOUND", 28, TextAnchor.MiddleCenter);
            Ui.TextOnArt(new Rect(0, title.y + 36, Ui.W, 20), HomeSubtitle(), 12, Palette.Text);
            HomeTabs(strip);
            if (_homeTab >= 0 && sheet.height >= 60)
            {
                float content = _homeTab == 1 ? QuestsHeight : 64;
                var view = new Rect(sheet.x, sheet.y, sheet.width, Mathf.Min(sheet.height, content + 2));
                _homeSheetScroll = Scroller.Begin(view, _homeSheetScroll, content);
                var r = new Rect(0, 0, view.width, content);
                if (_homeTab == 0) LoginCard(r); else if (_homeTab == 1) QuestsCard(r); else AchievementsCard(r);
                Scroller.End();
                if (content > view.height + 1) ScrollFade(view, _homeSheetScroll.y, content);
            }
            else if (_homeTab >= 0) { if (_homeTab == 2) Go(ScreenId.Achievements); else ShowHomeCard(_homeTab); _homeTab = -1; } // no room under the team: a modal
            float ch = Mathf.Max(44, Ui.Tap);
            if (_homeTab < 0 && sheet.height >= ch) HomeChips(new Rect(sheet.x, sheet.yMax - ch, sheet.width, ch)); // Settings / How to play, low and out of the team's way
            HomeContinue(cta);
        }

        // The tab strip: Login, Quests, Achievements, each with what's waiting; the open one is lit.
        void HomeTabs(Rect r)
        {
            int quests = 0, questsDone = 0;
            foreach (var q in DailyQuests) { if (G.QuestClaimed(q.Id)) questsDone++; else if (G.QuestProgress(q.Id) >= q.Goal) quests++; }
            int ach = G.AchReadyCount;
            var tabs = new (string label, int ready)[]
            {
                (G.LoginAvailable ? "Login · Claim" : "Login · Done", G.LoginAvailable ? 1 : 0),
                ($"Quests {questsDone}/{DailyQuests.Length}", quests),
                ("Achievements", ach),
            };
            float g = 6, w = (r.width - g * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                var b = new Rect(r.x + i * (w + g), r.y, w, r.height);
                if (Ui.Button(b, tabs[i].label, _homeTab == i ? Ui.Kind.ChipOn : Ui.Kind.Chip, 13))
                {
                    _homeTab = _homeTab == i ? -1 : i; _homeSheetScroll = Vector2.zero;
                }
                if (tabs[i].ready > 0)
                {
                    var dot = new Rect(b.xMax - 20, b.y - 6, 22, 18);
                    Ui.Round(dot, Palette.Gold, null, 9);
                    Ui.Text(dot, NumText.Of(tabs[i].ready), 11, Palette.Hex("#1a1208"), FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
        }

        // A card in a modal, for screens too short to open it under the team.
        void ShowHomeCard(int tab) => InfoModal(tab == 0 ? "Daily Login" : "Daily Quests", (tab == 0 ? 64 : QuestsHeight) + 80 + Mathf.Max(44, Ui.Tap), b =>
        {
            var r = new Rect(b.x, b.y, b.width, tab == 0 ? 64 : QuestsHeight);
            if (tab == 0) LoginCard(r); else QuestsCard(r);
            if (ModalFooter(b, "Close")) CloseModal();
        });

        void DrawHomeWide(Rect area)
        {
            LoadingPlaceholder.DrawLineup(Arena != null ? Arena.Cam : null, _lineup); // soft cards where heroes are still loading
            // Core/UiLayout.Home: two columns either side of the team at 16:9 and wider; one column on the right
            // (Continue pinned at its foot) on 4:3 tablets and narrow windows, where the middle was 96 units wide.
            var L = UiLayout.Home(Ui.ToBox(area), Ui.Tap, Ui.FullW);
            var mid = Ui.ToRect(L.Title);
            Ui.Heading(new Rect(mid.x - 40, mid.y, mid.width + 80, 40), "SIGILBOUND", 30, TextAnchor.MiddleCenter);
            Ui.TextOnArt(new Rect(mid.x, mid.yMax, mid.width, 22), HomeSubtitle(), 13, Palette.Text);
            var cta = Ui.ToRect(L.Cta);
            HomeContinue(cta);
            float chipsH = Mathf.Max(44, Ui.Tap);
            // Compact: the menu camera slides so the team stands in the space left of the column (Core/HomeLayout).
            _homeShiftX = L.Compact ? (float)HomeLayout.ShiftFor((L.Team.CenterX - Ui.Left) / Ui.FullW, Ui.FullW / Ui.H) : 0f;
            if (L.Compact)
            {
                // One full-height column on the right edge: login, achievements, quests, the help chips (it scrolls
                // only on screens shorter than a 1024x768 window).
                var col = Ui.ToRect(L.Left);
                float content = UiLayout.HomeColumnH(DailyQuests.Length, chipsH);
                _homeLeftScroll = Scroller.Begin(col, _homeLeftScroll, content);
                float cy = 0, g = UiLayout.HomeGap;
                LoginCard(new Rect(0, cy, col.width, UiLayout.LoginH)); cy += UiLayout.LoginH + g;
                AchievementsCard(new Rect(0, cy, col.width, UiLayout.AchievementsH)); cy += UiLayout.AchievementsH + g;
                QuestsCard(new Rect(0, cy, col.width, QuestsHeight)); cy += QuestsHeight + g;
                HomeChips(new Rect(0, cy, col.width, chipsH));
                Scroller.End();
                if (content > col.height + 1) ScrollFade(col, _homeLeftScroll.y, content);
                return;
            }
            var left = Ui.ToRect(L.Left); var right = Ui.ToRect(L.Right);
            // Right: the main call to action at the bottom (pinned), the login reward and achievements above it.
            float ry = right.y;
            LoginCard(new Rect(right.x, ry, right.width, 64)); ry += 74;
            if (cta.y - ry >= 58 + 10) AchievementsCard(new Rect(right.x, ry, right.width, 58));

            // Left: daily quests and the help chips, scrolling if the screen is short.
            float lcontent = QuestsHeight + 10 + chipsH + 8;
            if (cta.y - ry < 68) lcontent += 68; // achievements move here when the right column is full
            _homeLeftScroll = Scroller.Begin(left, _homeLeftScroll, lcontent);
            float y = 0;
            QuestsCard(new Rect(0, y, left.width, QuestsHeight)); y += QuestsHeight + 10;
            if (cta.y - ry < 68) { AchievementsCard(new Rect(0, y, left.width, 58)); y += 68; }
            HomeChips(new Rect(0, y, left.width, chipsH));
            Scroller.End();
            if (lcontent > left.height + 1) ScrollFade(left, _homeLeftScroll.y, lcontent);
        }

        string HomeSubtitle() => $"Campaign {G.S.cleared}/{TotalStages} · {G.S.heroes.Count} heroes · {G.S.wins} wins";
        float QuestsHeight => UiLayout.QuestsH(DailyQuests.Length);

        void HomeContinue(Rect r)
        {
            HomeCta = r;
            int next = Mathf.Min(G.S.cleared, TotalStages - 1);
            if (Ui.Button(r, G.S.cleared < TotalStages ? $"Continue Campaign · {Game.StageLabel(next)}" : "Campaign complete! Replay stages", Ui.Kind.Primary, 17))
                OpenPrep(next, false, ScreenId.Home);
        }

        void LoginCard(Rect r)
        {
            Ui.Card(r);
            bool login = G.LoginAvailable;
            float bw = 92, bh = Mathf.Min(r.height - 8, Mathf.Max(44, Ui.Tap));
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - bw - 24, 24), $"Daily Login · Day {(G.S.loginDay - (login ? 0 : 1)) % 7 + 1}", 16, Palette.Text, FontStyle.Bold);
            Ui.Text(new Rect(r.x + 14, r.y + 32, r.width - bw - 24, 22), login ? LoginRewards[G.S.loginDay % LoginRewards.Length].ToString() : "Come back tomorrow", 13, Palette.Dim);
            if (Ui.Button(new Rect(r.xMax - bw - 10, r.center.y - bh / 2, bw, bh), login ? "Claim" : "Claimed", Ui.Kind.Primary, 14, login))
            { var rw = G.ClaimLogin(); Save(); Sound.I?.Play("coin"); Toast("Login reward: " + rw); }
        }

        void QuestsCard(Rect r)
        {
            Ui.Card(r);
            Ui.Banner(new Rect(r.center.x - 110, r.y + 6, 220, 26), "Daily Quests", 15);
            float qy = r.y + 40, bw = 78, bh = Mathf.Max(44, Ui.Tap) - 2;
            foreach (var q in DailyQuests)
            {
                int p = Mathf.Min(q.Goal, G.QuestProgress(q.Id)); bool claimed = G.QuestClaimed(q.Id), done = p >= q.Goal;
                float tw = r.width - bw - 34;
                Ui.Text(new Rect(r.x + 14, qy, tw, 18), q.Name, 13, Palette.Text);
                Ui.Bar(new Rect(r.x + 14, qy + 21, Mathf.Min(220, tw), 6), p / (float)q.Goal, Palette.Gold);
                Ui.Text(new Rect(r.x + 14, qy + 28, tw, 18), $"{p}/{q.Goal} · {q.Reward}", 12, Palette.Dim);
                // A disabled button names its state ("2/5", "Done") on a readable grey plate.
                string label = claimed ? "Done" : done ? "Claim" : $"{p}/{q.Goal}";
                if (Ui.Button(new Rect(r.xMax - bw - 12, qy + 24 - bh / 2, bw, bh), label, Ui.Kind.Primary, 13, done && !claimed))
                { var rw = G.ClaimQuest(q.Id); if (rw != null) { Save(); Sound.I?.Play("coin"); Toast("Quest complete: " + rw); } }
                qy += UiLayout.QuestRowH;
            }
        }

        // Reset save moved to Settings > Danger zone (UX review #17: it sat beside How to play).
        void HomeChips(Rect r)
        {
            float half = (r.width - 5) / 2;
            if (Ui.Button(new Rect(r.x, r.y, half, r.height), "Settings", Ui.Kind.Chip, 13)) OpenSettings();
            if (Ui.Button(new Rect(r.x + half + 5, r.y, half, r.height), "How to play", Ui.Kind.Chip, 13)) ShowHelp();
        }

        void ShowHelp() => InfoModal("How to play", 470, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 350),
                    "Battles: units act when their Turn Meter fills, so faster heroes act more often. On your hero's turn, pick a skill, then tap a highlighted target.\n\n" +
                    "Elements: Flame beats Gale, Gale beats Tide, Tide beats Flame. Light and Shadow beat each other.\n\n" +
                    "Grow: win battles for XP or train with gold. At max level, rank up by using heroes with the same star count.\n\n" +
                    "Trials: the Titan (3 tries a day), Dungeons for gold, XP and the gear set you want (3 winning runs each a day), and the Arena (5 fights a day, Bronze to Sigil).\n\n" +
                    "Free: there's no shop. Every shard is earned by playing.", 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (ModalFooter(b)) CloseModal();
            });

        // ---------- campaign ----------
        void DrawCampaign(Rect area)
        {
            var (c0, c1) = ActChapters(false);
            int open = _chapterOpen >= 0 ? _chapterOpen : Mathf.Clamp(G.S.cleared / StagesPerChapter, c0, c1 - 1);
            float content = 108 + (c1 - c0) * 70 + 70;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = CampaignTabs(8);
            Ui.Banner(new Rect(Ui.W / 2 - 130, y + 2, 260, 30), "Campaign", 19); y += 42;
            for (int c = c0; c < c1; c++)
            {
                int first = c * StagesPerChapter;
                bool locked = first > G.S.cleared;
                bool isOpen = open == c && !locked;
                var r = Row(ref y, isOpen ? 76 + Mathf.Max(48, Ui.Tap) : 60);
                var sc = Palette.Scene(c);
                Ui.Panel(r, ChapterCard(Color.Lerp(sc.top, Palette.Panel, 0.6f), locked), Palette.Line);
                int done = Mathf.Clamp(G.S.cleared - first, 0, StagesPerChapter);
                Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 60, 24), $"{c + 1}. {Chapters[c]}", 16, locked ? Palette.Dim : Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 14, r.y + 32, r.width - 60, 20), $"{Game.ChapterElement(c)} · Lv {Game.StageLevel(first)}–{Game.StageLevel(first + 5)} · {done}/{StagesPerChapter}", 12, Palette.Dim);
                Ui.Text(new Rect(r.xMax - 64, r.y + 8, 54, 40), locked ? "Locked" : isOpen ? "–" : "+", 13, Palette.Dim);
                if (!locked && Ui.Button(new Rect(r.x, r.y, r.width, 60), "", Ui.Kind.Ghost)) _chapterOpen = isOpen ? 99 : c;
                if (isOpen)
                {
                    float sw = (r.width - 28 - 5 * 6) / 6f;
                    for (int s = 0; s < StagesPerChapter; s++)
                    {
                        int idx = first + s;
                        var b = new Rect(r.x + 14 + s * (sw + 6), r.y + 64, sw, Mathf.Max(48, Ui.Tap));
                        var kind = idx < G.S.cleared ? Ui.Kind.Secondary : idx == G.S.cleared ? Ui.Kind.Primary : Ui.Kind.Chip;
                        if (Ui.Button(b, Game.IsBossStage(idx) ? "Boss" : Game.StageLabel(idx), kind, 13, idx <= G.S.cleared)) OpenPrep(idx, false, ScreenId.Campaign);
                    }
                }
            }
            Scroller.End();
        }

        // ---------- team prep ----------
        public void EditTeam(int stage, bool hard = false) => OpenPrep(stage, false, ScreenId.Campaign, hard); // from the results screen
        public void EditDungeonTeam(DungeonKey key, int floor) => OpenDungeonPrep(key, floor);
        // While the player picks a team, start loading this fight's enemy models (their Hollow copies, the
        // same for Hard) and the Titan, so they arrive as themselves when the battle starts.
        void PreloadStage(int stage, bool titan)
        {
            try
            {
                var want = new System.Collections.Generic.List<(string, Element, bool, bool)>();
                if (titan) want.Add(("titan", GameData.TitanElement(G.Now()), true, false));
                else if (stage >= 0)
                    foreach (var w in Game.StageWaves(stage)) foreach (var e in w) want.Add((e.Template.Id, e.El, false, true));
                HeroModels.Preload(want);
            }
            catch (System.Exception e) { Debug.LogWarning("Sigilbound: preload failed: " + e.Message); }
        }
        void OpenPrep(int stage, bool titan, ScreenId back, bool hard = false, bool practice = false) { PreloadStage(stage, titan); _prepDungeon = _prepArena = false; _prepStage = stage; _prepTitan = titan; _prepPractice = titan && practice; _prepHard = hard && !titan; _prepBack = back; Go(ScreenId.Prep); }

        // Portrait team setup: the landscape blocks stacked (UI/UX review #23: no waves, matchup or element filter
        // here before), under the shared header; text over the 3D scene sits on a dark pill.
        void DrawPrep(Rect area)
        {
            var waves = PrepWaves();
            var heroes = SortedHeroes().Where(h => _prepFilter == null || Hero(h.id).Element == _prepFilter).ToList();
            float tap = Mathf.Max(44, Ui.Tap);
            float wavesH = waves == null ? 0 : waves.Count * 40 + 4;
            float cardH = UiLayout.CardH(tap);
            float content = 300 + wavesH + tap * 2 + 70 + Mathf.Ceil(heroes.Count / 2f) * (cardH + 8);
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), PrepTitle())) GoBack();
            y += tap + 8;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"{PrepSub(waves)} · Power {Ui.Fmt(G.TeamPower)}", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft); y += 28;
            var team = G.Team;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"Team ({team.Count}/4): " + (team.Count == 0 ? "tap heroes below" : string.Join(", ", team.Select(h => Hero(h.id).ShortName))), 13, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft); y += 30;
            if (Ui.Button(Row(ref y, Mathf.Max(50, tap)), PrepGoLabel(), Ui.Kind.Primary, 18, PrepCanGo())) PrepGo();
            // Matchup and waves, as in landscape.
            var theirs = new System.Collections.Generic.HashSet<Element>(waves == null ? new[] { TitanElement(System.DateTime.Now) } : waves.SelectMany(w => w).Select(e => e.El));
            int good = team.Count(h => theirs.Any(e => ElementMultiplier(Hero(h.id).Element, e) > 1));
            int bad = team.Count(h => theirs.Any(e => ElementMultiplier(e, Hero(h.id).Element) > 1));
            var mr = Row(ref y, 24, 4);
            Ui.Round(mr, new Color(0.04f, 0.03f, 0.06f, 0.72f), null, 10);
            Ui.Text(mr, $"{Ui.ColorTag($"{good} strong", Palette.Good)} · {Ui.ColorTag($"{bad} weak", Palette.Bad)} against these foes", 12, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter);
            if (waves != null)
            {
                var wr = Row(ref y, waves.Count * 40, 8);
                Ui.Round(wr, new Color(0.07f, 0.05f, 0.1f, 0.85f), Palette.Bronze, 10);
                for (int w = 0; w < waves.Count; w++)
                {
                    float wy = wr.y + 2 + w * 40;
                    Ui.Text(new Rect(wr.x + 8, wy, 60, 36), $"Wave {w + 1}", 11, Palette.Hex("#c9bda6"), FontStyle.Bold);
                    float ex = wr.x + 66, ew = (wr.width - 72) / Mathf.Max(1, waves[w].Count);
                    foreach (var e in waves[w]) { EnemyChip(new Rect(ex, wy + 4, ew - 2, 30), e); ex += ew; }
                }
            }
            y = DrawPresets(y);
            // Element filter chips above the picker, as in landscape.
            float fw = (Ui.W - Pad * 2 - 5 * 4) / 6f;
            if (Ui.Button(new Rect(Pad, y, fw, tap), "All", _prepFilter == null ? Ui.Kind.ChipOn : Ui.Kind.Chip, 12)) _prepFilter = null;
            for (int i = 0; i < Elements.Length; i++)
            {
                var el = Elements[i]; var b = new Rect(Pad + (i + 1) * (fw + 4), y, fw, tap);
                if (Ui.Button(b, "", _prepFilter == el ? Ui.Kind.ChipOn : Ui.Kind.Chip)) _prepFilter = _prepFilter == el ? (Element?)null : el;
                Icons.Draw(new Rect(b.center.x - 9, b.center.y - 9, 18, 18), Icons.ForElement(el), Palette.Element(el));
            }
            y += tap + 8;
            float cw = (Ui.W - Pad * 2 - 8) / 2;
            for (int i = 0; i < heroes.Count; i++)
            {
                var h = heroes[i]; var t = Hero(h.id);
                var r = new Rect(Pad + (i % 2) * (cw + 8), y + (i / 2) * (cardH + 8), cw, cardH);
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
        }

        // A hero card in a team picker (landscape and portrait team setup): the small card frame, in the team the one
        // "selected" mark (gold rim and wash, underline, check; design rule 4), and the face, element, name and the
        // stars, level and power line inside the frame's content rect (Core/UiLayout.HeroCard; L028: the chip plate
        // used before drew its crown ornaments over the name and power).
        static void HeroPickCard(Rect r, HeroDef t, bool inTeam, int slot, string line)
        {
            Ui.CardFrame(r, inTeam, Palette.Rarity(t.Rarity));
            Ui.RarityTab(r, t.Rarity, 14); // rarity as a letter too, not only the frame colour (UX review #4)
            var c = Ui.HeroCardRects(r, Portraits.Get(t.Id) != null);
            if (c.Face.W > 0) Portraits.Thumb(Ui.ToRect(c.Face), t.Id, Palette.Rarity(t.Rarity));
            Ui.ElementBadge(Ui.ToRect(c.Badge), t.Element);
            Ui.Text(Ui.ToRect(c.Name), t.ShortName + (slot >= 0 ? $"  #{slot + 1}" : ""), 13, Palette.Text, FontStyle.Bold);
            Ui.Text(Ui.ToRect(c.Line), line, 11, Palette.Hex("#c9bda6"));
        }

        // ---------- heroes ----------
        System.Collections.Generic.List<HeroSave> SortedHeroes()
        {
            var list = G.S.heroes.ToList();
            return _sort switch
            {
                "level" => list.OrderByDescending(h => h.stars).ThenByDescending(h => h.level).ToList(),
                "rarity" => list.OrderByDescending(h => Hero(h.id).Rarity).ThenByDescending(h => h.level).ToList(),
                _ => list.OrderByDescending(G.Power).ToList(),
            };
        }

        void DrawHeroes(Rect area)
        {
            if (DrawRosterPanel(area)) return; // Release Heroes / Rank Up panels (RosterUi.cs)
            var list = SortedHeroes();
            float content = 100 + HeroToolsExtra + list.Count * 62 + 40;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = HeroTabs(8, G.S.heroes.Select(h => h.id).Distinct().Count());
            y = HeroListTools(y); // sort chips (saved), Release…, Rank up all, Undo
            foreach (var h in list)
            {
                var t = Hero(h.id);
                var r = Row(ref y, 54, 8);
                Ui.SmallCard(r, Palette.Panel, Palette.Rarity(t.Rarity), 12);
                Ui.Round(new Rect(r.x + 8, r.y + 10, 6, 34), Palette.Element(t.Element), null, 3);
                float tx = Portraits.Thumb(new Rect(r.x + 20, r.y + 6, 42, 42), t.Id, Palette.Rarity(t.Rarity)) ? 70 : 22; // painted portrait, when there is one
                Ui.RarityTab(new Rect(r.x + 20, r.y + 4, tx > 22 ? 48 : 16, 16), t.Rarity, 15); // rarity as a letter too, not only the frame colour (UX review #4)
                int ti = G.S.team.IndexOf(h.uid);
                // Team tag above the power on the right, so the name keeps room (the portrait took 48 px).
                if (ti >= 0) Ui.Text(new Rect(r.xMax - 84, r.y + 6, 74, 20), $"In team #{ti + 1}", 12, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight); // "Team 1-3" are the presets (UX review #14)
                Ui.Text(new Rect(r.xMax - 84, ti >= 0 ? r.y + 26 : r.y, 74, ti >= 0 ? 22 : 54), Ui.Fmt(G.Power(h)), 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);
                Ui.Text(new Rect(r.x + tx, r.y + 4, r.width - 90 - tx, 24), HeroLine(h), 14, Palette.Text, FontStyle.Bold);
                Ui.Text(new Rect(r.x + tx, r.y + 28, r.width - 90 - tx, 20), Tag(t), 12, Palette.Dim);
                if (Ui.Button(r, "", Ui.Kind.Ghost)) { _heroUid = h.uid; Go(ScreenId.Hero); }
            }
            Scroller.End();
        }

        void DrawHero(Rect area)
        {
            var h = G.HeroByUid(_heroUid);
            if (h == null) { Go(ScreenId.Heroes); return; }
            var t = Hero(h.id); var st = G.HeroStats(h); int cap = MaxLevelFor(h.stars); bool atCap = h.level >= cap;
            float tap = Mathf.Max(44, Ui.Tap);
            float content = 300 + 150 + 120 + 130 + t.Skills.Length * (90 + tap) + 90 + tap * 3;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            // The shared header (Back + title); Lock and Release on the row under it, both 48 dp.
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), t.Name)) GoBack();
            y += tap + 6;
            float hw = (Ui.W - Pad * 2 - 8) / 2;
            if (Ui.Button(new Rect(Pad, y, hw, tap), h.locked ? "Locked" : "Lock", h.locked ? Ui.Kind.ChipOn : Ui.Kind.Chip, 13)) { h.locked = !h.locked; Save(); }
            if (Ui.Button(new Rect(Pad + hw + 8, y, hw, tap), "Release", Ui.Kind.Secondary, 13)) AskRelease(h);
            y += tap + 8;
            Ui.TextOnArt(new Rect(Pad + 8, y, Ui.W - 48, 20), $"{Ui.ColorTag(Ui.Stars(h.stars), Palette.Gold)}   {Tag(t)}   Power {Ui.ColorTag(Ui.Fmt(st.Power), Palette.Gold)}", 13, Palette.Text, FontStyle.Normal, TextAnchor.MiddleLeft);
            y += 230; // the hero stands here in 3D (App.UpdateHeroViewer: drag to spin, tap to attack)
            Ui.Text(new Rect(Pad, y - 20, Ui.W - 32, 16), "Drag to spin · tap to attack", 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);

            var r = Row(ref y, (atCap ? 74 : 60) + tap);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, 200, 22), $"Level {h.level} / {cap}", 15, Palette.Text, FontStyle.Bold);
            Ui.Text(new Rect(r.xMax - 160, r.y + 8, 146, 22), atCap ? "MAX" : $"{h.xp} / {XpToNext(h.level)} XP", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            Ui.Bar(new Rect(r.x + 14, r.y + 34, r.width - 28, 6), atCap ? 1 : h.xp / (float)XpToNext(h.level), Palette.Gold);
            if (atCap)
            {
                Ui.Text(new Rect(r.x + 14, r.y + 44, r.width - 28, 20), h.stars >= MaxStars ? "Fully ascended!" : $"Rank up: {h.stars} other {h.stars}★ heroes ({G.RankUpFodder(h).Count} available) + {Ui.Fmt(RankUpGold(h.stars))} gold", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleLeft, true);
                if (Ui.Button(new Rect(r.x + 14, r.y + 66, r.width - 28, tap), $"Rank Up to {h.stars + 1}\u2605", Ui.Kind.Primary, 14, G.CanRankUp(h))) OpenRankUp(h);
            }
            else
            {
                int lvlCost = Mathf.CeilToInt((XpToNext(h.level) - h.xp) / (float)TrainXpPerGold), maxCost = Mathf.CeilToInt(G.XpToCap(h) / (float)TrainXpPerGold);
                if (Ui.Button(new Rect(r.x + 14, r.y + 50, r.width / 2 - 19, tap), $"Train +1 · {Ui.Fmt(lvlCost)}", Ui.Kind.Secondary, 13, G.S.gold >= lvlCost)) { G.Train(h, false); Save(); }
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.y + 50, r.width / 2 - 19, tap), $"Train to {cap} · {Ui.Fmt(maxCost)}", Ui.Kind.Secondary, 13, G.S.gold >= maxCost)) { G.Train(h, true); Save(); }
            }

            r = Row(ref y, 110);
            Ui.Card(r);
            string[] stats = { $"HP  {Ui.Fmt(st.Hp)}", $"ATK  {st.Atk}", $"DEF  {st.Def}", $"SPD  {st.Spd}", $"Crit  {st.Crit}%", $"Crit DMG  {st.Cdmg}%" };
            for (int i = 0; i < stats.Length; i++) Ui.Text(new Rect(r.x + 14 + (i % 2) * (r.width / 2), r.y + 10 + (i / 2) * 26, r.width / 2 - 20, 24), stats[i], 14, Palette.Text);
            Ui.Text(new Rect(r.x + 14, r.y + 86, r.width - 28, 18), st.Sets.Count > 0 ? "Sets: " + string.Join(", ", st.Sets.Select(s => $"{s} ({SetBonus(s).desc})")) : "Equip 2 pieces of a set for a bonus.", 11, Palette.Dim);

            r = Row(ref y, 86 + tap);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, 100, tap), "Gear", 15, Palette.Text, FontStyle.Bold);
            if (Ui.Button(new Rect(r.xMax - 134, r.y + 6, 120, tap), "Auto-equip", Ui.Kind.Secondary, 12)) { G.AutoEquip(h); Save(); Toast("Best available gear equipped"); }
            float gw = (r.width - 28 - 18) / 4;
            int si = 0;
            foreach (Slot slot in System.Enum.GetValues(typeof(Slot)))
            {
                var g = G.GearByUid(h.GetGear(slot));
                var b = new Rect(r.x + 14 + si * (gw + 6), r.y + 14 + tap, gw, 64);
                string label = g == null ? $"{slot}\n{Ui.ColorTag("empty", Palette.Dim)}" : $"{slot} +{g.level}\n{Ui.ColorTag(g.Set.ToString(), Palette.GearRarity(g.rarity))}";
                if (Ui.Button(b, label, Ui.Kind.Secondary, 12)) { _slot = slot; Go(ScreenId.Equip); }
                si++;
            }

            y = DrawSkills(h, t, y);
            Scroller.End();
        }

        void AskRelease(HeroSave h)
        {
            if (h.locked) { Toast("Unlock this hero first."); return; }
            if (G.S.heroes.Count <= 1) { Toast("You can't release your last hero."); return; }
            int ess = EssenceValue(Hero(h.id).Rarity) * h.stars;
            Modal($"Release {Hero(h.id).ShortName}?", 210, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 60), $"They leave your roster for good. Their gear returns to your bag. You get {ess} essence.", 14, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width / 2 - 5, 44), "Cancel", Ui.Kind.Secondary)) CloseModal();
                if (Ui.Button(new Rect(b.x + b.width / 2 + 5, b.yMax - 46, b.width / 2 - 5, 44), "Release", Ui.Kind.Danger))
                {
                    var u = G.ReleaseMany(G.PlanReleaseOne(h)); // gear back to the bag, out of saved presets
                    if (u != null) SetUndo(u);
                    Save(); CloseModal(); Toast($"+{ess} essence · Undo on the Heroes list for 2 minutes"); Go(ScreenId.Heroes);
                }
            });
        }

        void DrawEquip(Rect area)
        {
            var h = G.HeroByUid(_heroUid);
            if (h == null) { Go(ScreenId.Heroes); return; }
            var list = G.S.gear.Where(g => g.Slot == _slot).OrderByDescending(Game.GearScore).ToList();
            float tap = Mathf.Max(44, Ui.Tap), rowH = UiLayout.GearRowH(tap);
            float content = 80 + tap * 2 + list.Count * (rowH + 8) + 40;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            // The shared header: Back and "<slot> for <hero>".
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), $"{_slot} · {Hero(h.id).ShortName}")) GoBack();
            y += tap + 6;
            float hw = (Ui.W - Pad * 2 - 8) / 2;
            if (h.GetGear(_slot) != 0 && Ui.Button(new Rect(Pad, y, hw, tap), "Unequip", Ui.Kind.Secondary, 13)) { h.SetGear(_slot, 0); Save(); Go(ScreenId.Hero); }
            if (Ui.Button(new Rect(Pad + hw + 8, y, hw, tap), "Sell junk (1-2 stars)", Ui.Kind.Secondary, 12))
            { int total = G.SellJunk(); Save(); Toast(total > 0 ? $"Sold for {Ui.Fmt(total)} gold (locked gear kept)" : "No unequipped, unlocked 1-2 star gear to sell"); }
            y += tap + 10;
            if (list.Count == 0) Ui.Text(Row(ref y, 40), "Nothing for this slot yet. Clear campaign stages to find gear.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleLeft, true);
            foreach (var g in list)
            {
                // Core/UiLayout.GearRow: the name on its own line across the row (at 1024x768 it ran under Lock), then
                // the stats on the left beside two 48 dp button rows: Lock and Upgrade, Sell and Equip (the Primary, last).
                var r = Row(ref y, rowH, 8);
                var owner = G.GearOwner(g);
                Ui.CardFrame(r, false, Palette.GearRarity(g.rarity));
                var L = UiLayout.GearRow(Ui.ToBox(r), Ui.CardBorder, tap);
                Ui.Text(Ui.ToRect(L.Name), $"{Ui.ColorTag(GearRarityNames[g.rarity], Palette.GearRarity(g.rarity))} {g.Set} {g.Slot} +{g.level}", 14, Palette.Text, FontStyle.Bold);
                Ui.Text(Ui.ToRect(L.Main), $"{SlotMainStat(g.Slot).ToString().ToUpper()} +{Game.GearMainValue(g)}", 13, Palette.Text);
                Ui.Text(Ui.ToRect(L.Subs), string.Join(" · ", g.subs.Select(s => SkillText.StatName((Stat)s.stat, s.val))) + (owner != null ? $"\nOn {Hero(owner.id).ShortName}" : ""), 11, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.UpperLeft, true);
                int cost = Game.GearUpgradeCost(g);
                if (g.level < GearMaxLevel && Ui.Button(Ui.ToRect(L.Up), $"Up · {Ui.Fmt(cost)}", Ui.Kind.Secondary, 12, G.S.gold >= cost)) { if (G.UpgradeGear(g)) { Save(); Toast($"Upgraded to +{g.level}"); } }
                if (Ui.Button(Ui.ToRect(L.Lock), g.locked ? "Locked" : "Lock", g.locked ? Ui.Kind.ChipOn : Ui.Kind.Chip, 11)) { g.locked = !g.locked; Save(); }
                if (owner == null && !g.locked && Ui.Button(Ui.ToRect(L.Sell), "Sell", Ui.Kind.Danger, 11)) { int v = Game.GearSellValue(g); G.SellGear(g); Save(); Toast($"Sold for {Ui.Fmt(v)} gold"); }
                if (owner != h && Ui.Button(Ui.ToRect(L.Equip), "Equip", Ui.Kind.Primary, 13)) { G.Equip(h, g); Save(); Go(ScreenId.Hero); }
                else if (owner == h) Ui.Text(Ui.ToRect(L.Equip), "Equipped", 12, Palette.Good, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
            Scroller.End();
        }

        // ---------- summon ----------
        void DrawSummon(Rect area)
        {
            float content = 64 + 3 * 160 + 130;
            Ui.Backdrop(new Rect(Ui.Left, area.y, Ui.FullW, area.height), "bg_summon", 0.32f); // the owner's altar painting behind the shards
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            Ui.Banner(new Rect(Ui.W / 2 - 130, y + 2, 260, 30), "Summon", 19); y += 38;
            { var sr = Row(ref y, 34); Ui.Round(sr, new Color(0.04f, 0.03f, 0.06f, 0.72f), null, 10); // on the altar painting: a dark backing (art review H5)
              Ui.Text(new Rect(sr.x + 8, sr.y, sr.width - 16, sr.height), "Shards drop from the campaign, quests, the Titan and daily logins.", 12, Palette.Hex("#e0d6c4"), FontStyle.Normal, TextAnchor.MiddleCenter, true); }
            foreach (Shard s in System.Enum.GetValues(typeof(Shard)))
            {
                var r = Row(ref y, 148);
                var rates = ShardRates(s);
                Color c = s == Shard.Common ? Palette.Hex("#c9a37b") : s == Shard.Mystic ? Palette.Blue : Palette.Hex("#ffb02e");
                Ui.Panel(r, new Color(0.12f, 0.1f, 0.17f, 0.94f), c);
                string name = ShardName(s);
                Icons.Currency(new Rect(r.x + 14, r.y + 12, 16, 16), Icons.ShardCur(s));
                Ui.Text(new Rect(r.x + 36, r.y + 8, r.width - 50, 24), $"{name}  {Ui.ColorTag($"owned {Ui.Fmt(G.ShardCount(s))}", Palette.Dim)}", 16, c, FontStyle.Bold);
                string rateText = string.Join(" · ", Enumerable.Range(0, 4).Where(i => rates[i] > 0).Select(i => Ui.ColorTag($"{(Rarity)i} {rates[i]}%", Palette.Rarity((Rarity)i))));
                Ui.Text(new Rect(r.x + 14, r.y + 34, r.width - 28, 20), rateText, 12, Palette.Text);
                if (ShardPity(s) > 0) Ui.Text(new Rect(r.x + 14, r.y + 54, r.width - 28, 20), $"Guaranteed Legendary in {G.PityLeft(s)} summons", 12, Palette.Gold);
                if (Ui.Button(new Rect(r.x + 14, r.y + 88, r.width / 2 - 19, 46), "Summon ×1", Ui.Kind.Primary, 15, G.ShardCount(s) >= 1)) DoSummon(s, 1);
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.y + 88, r.width / 2 - 19, 46), G.ShardCount(s) >= 10 ? "Summon ×10" : $"×10 · {G.ShardCount(s)}/10", Ui.Kind.Secondary, 15, G.ShardCount(s) >= 10)) DoSummon(s, 10); // disabled: says how many you have
            }
            var e = Row(ref y, 110);
            Ui.Panel(e, new Color(0.12f, 0.1f, 0.17f, 0.94f));
            Ui.Text(new Rect(e.x + 14, e.y + 8, e.width - 28, 22), $"Essence Exchange  {Ui.ColorTag($"you have {Ui.Fmt(G.S.essence)}", Palette.Dim)}", 15, Palette.Text, FontStyle.Bold);
            int k = 0;
            foreach (Shard s in System.Enum.GetValues(typeof(Shard)))
            {
                int cost = ExchangeCost(s);
                if (Ui.Button(new Rect(e.x + 14 + k * ((e.width - 40) / 3 + 6), e.y + 42, (e.width - 40) / 3, 54), $"{ShardName(s)}\n{cost} essence", Ui.Kind.Secondary, 12, G.S.essence >= cost))
                { if (G.Exchange(s)) { Save(); Toast($"+1 {ShardName(s)}"); } }
                k++;
            }
            Scroller.End();
            if (content > area.height + 1) ScrollFade(area, _scroll.y, content); // the Radiant card and the exchange are below the fold
        }

        void DoSummon(Shard s, int n)
        {
            var res = G.Summon(s, n);
            if (res.Count == 0) { Toast("Not enough shards."); return; }
            Save();
            var best = res.Max(h => Hero(h.id).Rarity);
            // audio.js / ui.js: a low cast, then a fanfare by the best rarity.
            Sound.I?.Play("cast", 0.5f, 0.6f, 0);
            Sound.I?.Play(best >= Rarity.Legendary ? "legendary" : best >= Rarity.Epic ? "epic" : best >= Rarity.Rare ? "rare" : "confirm", 1, 1, 0);
            Vfx.I.Burst(new Vector3(0, 1.2f, 0.6f), Palette.Rarity(best), true);
            if (best >= Rarity.Epic) Vfx.I.Shake(0.15f);
            // A single pull with a painted portrait gets the card reveal above its line.
            bool card = res.Count == 1 && Portraits.Get(res[0].id) != null;
            Modal(best == Rarity.Legendary ? "LEGENDARY!" : "Summoned!", 120 + res.Count * 26 + 60 + (card ? 250 : 0), b =>
            {
                float top = 0;
                if (card) { float cw = 180, ch = 240; Portraits.Card(new Rect(b.center.x - cw / 2, b.y, cw, ch), res[0].id, Palette.Rarity(Hero(res[0].id).Rarity)); top = ch + 10; }
                for (int i = 0; i < res.Count; i++)
                {
                    var t = Hero(res[i].id);
                    Ui.Text(new Rect(b.x, b.y + top + i * 26, b.width, 24), $"{Ui.ColorTag(Ui.Stars(res[i].stars), Palette.Gold)}  {Ui.ColorTag(t.Name, Palette.Rarity(t.Rarity))}  {Ui.ColorTag(t.Element.ToString(), Palette.Element(t.Element))}", 14, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
                bool again = G.ShardCount(s) >= n; float tp = Mathf.Max(44, Ui.Tap);
                var row = again ? new[] { ("OK", UiRole.Secondary, true), ($"Again ×{n}", UiRole.Primary, true) } : new[] { ("OK", UiRole.Primary, true) };
                int hit = Ui.ButtonRow(new Rect(b.x, b.yMax - tp, b.width, tp), row);
                if (hit == 0) CloseModal(); else if (hit == 1) DoSummon(s, n);
            });
        }

        // ---------- titan ----------
        void DrawTitan(Rect area)
        {
            float content = 920;
            _scroll = Scroller.Begin(area, _scroll, content);
            float y = 8;
            var el = TitanElement(System.DateTime.Now);
            float tap = Mathf.Max(44, Ui.Tap);
            if (Ui.ScreenHeader(new Rect(Pad, y, Ui.W - Pad * 2, tap), Titan.Name)) GoBack();
            y += tap + 10;
            var r = Row(ref y, 124 + tap);
            Ui.Card(r);
            var strong = Elements.Where(x => Strong(x) == el).ToList();
            Ui.Text(new Rect(r.x + 14, r.y + 10, r.width - 28, 22), $"Today's element: {Ui.ColorTag(el.ToString(), Palette.Element(el))}", 15, Palette.Text, FontStyle.Bold);
            Ui.Text(new Rect(r.x + 14, r.y + 34, r.width - 28, 60), $"Bring {string.Join(" or ", strong)} heroes. The Titan can't be killed: deal as much damage as you can in {TitanTurns} of its turns. It's immune to stun and turn meter drain.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            Ui.Text(new Rect(r.x + 14, r.y + 94, r.width - 28, 22), $"Tries left today: {Ui.ColorTag($"{G.S.daily.titanTries}/3", Palette.Gold)} · Best today: {Ui.Fmt(G.S.daily.titanBest)}", 13, Palette.Text);
            // "Team ▶" as on stages and floors: it opens team setup (one word for it: UI/UX review #16).
            if (Ui.Button(new Rect(r.x + 14, r.y + 118, r.width - 28, tap), G.S.daily.titanTries > 0 ? "Team \u25B6" : "No tries left today", Ui.Kind.Primary, 15, G.S.daily.titanTries > 0)) OpenPrep(-1, true, ScreenId.Titan);
            // Practice: unlimited Titan fights with no rewards and no try used (try out teams, see the Titan).
            var pr = Row(ref y, tap);
            if (Ui.Button(pr, "Practice (no rewards, no try used)", Ui.Kind.Secondary, 14)) OpenPrep(-1, true, ScreenId.Titan, false, true);
            // Its moves (owner request 2026-11-28): what each one looks like on the remodelled body, and the numbers.
            r = Row(ref y, 40 + Titan.Skills.Length * 78);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 28, 22), "Its moves", 15, Palette.Text, FontStyle.Bold);
            for (int i = 0; i < Titan.Skills.Length; i++)
            {
                var sk = Titan.Skills[i]; float sy = r.y + 34 + i * 78;
                Ui.Text(new Rect(r.x + 14, sy, r.width - 28, 20), sk.Name + (sk.Cooldown > 0 ? $"  ·  cooldown {sk.Cooldown}" : "  ·  basic"), 13, Palette.Gold, FontStyle.Bold);
                Ui.Text(new Rect(r.x + 14, sy + 19, r.width - 28, 36), i < TitanMoveLore.Length ? TitanMoveLore[i] : "", 11, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                Ui.Text(new Rect(r.x + 14, sy + 55, r.width - 28, 18), SkillText.Describe(sk), 11, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            }
            Ui.Text(Row(ref y, 36), "Its crystal ridges crack off as you deal damage: the first at 66% of its health bar, more at 33%.", 11, Palette.Dim, FontStyle.Normal, TextAnchor.UpperLeft, true);
            r = Row(ref y, 40 + TitanTiers.Length * 30);
            Ui.Card(r);
            Ui.Text(new Rect(r.x + 14, r.y + 8, r.width - 28, 22), "Damage rewards (per try)", 15, Palette.Text, FontStyle.Bold);
            for (int i = 0; i < TitanTiers.Length; i++)
            {
                var t = TitanTiers[i];
                bool got = G.S.daily.titanBest > 0 && G.S.daily.titanBest >= t.Dmg;
                if (got) Icons.Draw(new Rect(r.x + 14, r.y + 41 + i * 30, 16, 16), Icon.Check, Palette.Good);
                Ui.Text(new Rect(r.x + 34, r.y + 36 + i * 30, 100, 26), i == 0 ? "Any damage" : Ui.Fmt(t.Dmg) + "+", 13, got ? Palette.Good : Palette.Text);
                Ui.Text(new Rect(r.x + 130, r.y + 36 + i * 30, r.width - 144, 26), t.Reward.ToString(), 11, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            }
            Ui.Text(Row(ref y, 24), $"All-time best: {Ui.Fmt(G.S.titanBestAllTime)}", 12, Palette.Dim);
            Scroller.End();
        }
    }
}
