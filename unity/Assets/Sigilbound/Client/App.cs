// App shell: starts itself when you press Play in any scene, owns the save,
// routes between screens, and shows your heroes standing in the 3D arena behind the menus.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Bootstrap
    {
        // Runs automatically after the first scene loads; no scene setup required.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Start()
        {
            if (UnityEngine.Object.FindAnyObjectByType<App>() != null) return;
            foreach (var cam in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsSortMode.None)) cam.gameObject.SetActive(false);
            var go = new GameObject("Sigilbound");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<App>();
        }
    }

    public enum ScreenId { Intro, Home, Campaign, Prep, Heroes, Hero, Equip, Summon, Titan, Battle, Friends, FriendProfile, Chat, Blocked, Trials, Dungeon, ArenaPvp, Achievements, Modes }

    public sealed partial class App : MonoBehaviour
    {
        public const string SaveKey = SaveStore.MainKey;
        public Game G;
        public Arena Arena;
        public ScreenId Screen = ScreenId.Home;
        public BattleController Battle;

        // screen state
        int _heroUid; GameData.Slot _slot; int _chapterOpen = -1; int _prepStage = -1; bool _prepTitan, _prepPractice; ScreenId _prepBack;
        Vector2 _scroll; // the hero-list sort (_sort) is saved: RosterUi.cs
        readonly List<CharacterView> _lineup = new List<CharacterView>();
        List<string> _shotLineup; bool _shotNoUi; // screenshot mode: a chosen lineup, menus hidden (ShotsMode gallery)
        string _lineupKey = "";

        // modal + toasts
        string _modalTitle; Action<Rect> _modalBody; float _modalHeight = 260, _modalMaxW = ModalW;
        // Modals keep a readable width on wide screens (UX review #10: they stretched to about 1040 units);
        // the results screens get more room for their hero columns.
        public const float ModalW = 560, WideModalW = 900;
        float _modalClosedAt = -10f;
        readonly List<(string text, float until)> _toasts = new List<(string, float)>();

        void Awake()
        {
            // Landscape on phones, either way round (the owner chose landscape for phone and PC).
            UnityEngine.Screen.autorotateToPortrait = false;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
            UnityEngine.Screen.autorotateToLandscapeLeft = true;
            UnityEngine.Screen.autorotateToLandscapeRight = true;
            UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;
            UnityEngine.Screen.sleepTimeout = SleepTimeout.SystemSetting; // kept awake only in battle (UpdatePower)
            G = new Game(Load());
            G.EnsureDaily();
            Arena = Arena.Create();
            // Frame-rate cap (60, or 30 to run cooler) and graphics tier mapped to URP settings.
            Quality.Create(gameObject, G.S.settings.quality, Quality.SavedFpsMode(G.S.settings.fpsCap)).Sun = Arena.Sun;
            Background.Apply(); // desktop: keep running when the window loses focus (Settings)
            Quality.Busy = () => Battle != null && Battle.InProgress; // Auto holds tier changes until the battle ends
            Quality.ReplayCapped = () => Screen == ScreenId.Battle && Battle != null && Battle.Replaying; // auto-replay runs at 30 fps
            Quality.I.Apply();
            Quality.Notice = Toast; // thermal fallback messages
            Vfx.ShakeSetting = () => Quality.SavedShake; // Settings > Screen shake
            PerfOverlay.Sync();     // debug FPS / draw-call overlay, if turned on in Settings
            Vfx.Create(Arena.Cam);
            SkillFx.Create(Arena.Cam).SetTier(Quality.I.Tier); // vfx.mjs-style skill effects
            PostFx.Init(Arena.Cam); PostFx.SetTier(Quality.I.Tier);
            Sound.Create(this);
            Battle = gameObject.AddComponent<BattleController>();
            Battle.App = this;
            Online = gameObject.AddComponent<Online>();
            Online.App = this;
            Screen = G.S.started ? ScreenId.Home : ScreenId.Intro;
            _firstLaunch = !G.S.started; // no save yet: What's New waits for the next update (PopupQueue)
            if (_firstLaunch) MarkChangelogSeen();
            Tips.Seed(G); // existing players skip beginner tips for things they've done
            Arena.Build(Math.Min(GameData.Chapters.Length - 1, G.S.cleared / GameData.StagesPerChapter), false);
            Arena.FrameMenu();
            StartShotsIfAsked(); // -sbShots=DIR: screenshot tour (ShotsMode.cs)
            PreloadModels();
            ShowLoadProblem();
        }

        // Starts loading the high-detail models the player will see first (the menu lineup: the team, or
        // the starters on a new game) and today's Titan, so they appear as themselves instead of the
        // block stand-ins swapping over (owner report 2026-11-26).
        void PreloadModels()
        {
            var want = new List<(string, Element, bool, bool)>();
            foreach (var h in G.Team) { var d = GameData.Hero(h.id); if (d != null) want.Add((d.Id, d.Element, false, false)); }
            if (!G.S.started) foreach (var id in GameData.Starters) { var d = GameData.Hero(id); if (d != null) want.Add((d.Id, d.Element, false, false)); }
            want.Add(("titan", GameData.TitanElement(G.Now()), true, false));
            HeroModels.Preload(want);
        }

        // ---------- save ----------
        // Stored in the web game's JSON layout (SaveCodec), so save codes move between the two.
        // SaveStore (Core) owns the safety rules: an unreadable save is kept as a copy and never
        // overwritten, the previous save is kept as a backup, and nothing is written while a failed
        // load is unresolved (QA review 2026-11-27, P0-1).
        sealed class PrefsStorage : ISaveStorage
        {
            public string Get(string key) => PlayerPrefs.GetString(key, "");
            public void Set(string key, string value) => PlayerPrefs.SetString(key, value);
            public void Flush() => PlayerPrefs.Save();
        }
        public SaveStore Store;
        SaveData Load()
        {
            Store = new SaveStore(new PrefsStorage());
            var s = Store.Load(DateTime.Now);
            if (Store.State == SaveLoadState.FromBackup || Store.State == SaveLoadState.Failed)
                Debug.LogWarning($"Sigilbound: save unreadable ({Store.Error}), kept as {Store.CorruptKey}; state {Store.State}");
            return s;
        }
        public void Save()
        {
            if (NoSave) return; // screenshot mode (ShotsMode.cs) never touches the real save
            if (Store == null || !Store.CanWrite) return; // failed load: never overwrite the real save
            G.S.gameVersion = Application.version;
            Store.Write(G.S);
        }

        // After a failed load: explain, and let the player load a save code or start over on purpose.
        void ShowLoadProblem()
        {
            if (Store == null || NoSave) return;
            if (Store.State == SaveLoadState.FromBackup)
            {
                Modal("Save restored from backup", 230, b =>
                {
                    Ui.Text(new Rect(b.x, b.y, b.width, b.height - 56), "Your save couldn't be read, so the backup from just before your last save was loaded. A copy of the damaged save is kept on this device.", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "OK")) CloseModal();
                });
                return;
            }
            if (Store.State != SaveLoadState.Failed) return;
            Modal("Your save couldn't be read", 330, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 150), "Your progress is kept safe on this device and nothing will be saved over it.\n\nIf you have a save code, load it in Settings. Or copy the damaged save to send with a bug report. Starting a new game keeps the damaged copy too.", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                float bw = (b.width - 10) / 2;
                if (Ui.Button(new Rect(b.x, b.yMax - 96, bw, 44), "Load a save code", Ui.Kind.Secondary)) OpenSettings();
                if (Ui.Button(new Rect(b.x + bw + 10, b.yMax - 96, bw, 44), "Copy damaged save", Ui.Kind.Secondary)) { GUIUtility.systemCopyBuffer = Store.CorruptText; Toast("Copied"); }
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "Start a new game", Ui.Kind.Red)) { Store.AcceptFresh(); CloseModal(); }
            });
        }

        // Replace this device's progress with a save code from the web game (or another device).
        public bool ImportSaveCode(string code)
        {
            var s = SaveCodec.FromCode(code);
            if (s == null) return false;
            Store?.AcceptFresh(); // the player chose this save over the unreadable one
            G = new Game(s);
            G.EnsureDaily();
            _undo = null; _rp = RosterPanel.None; // a pending release undo belongs to the old save
            Save();
            _lineupKey = "";
            Quality.I?.Set(G.S.settings.quality, Quality.SavedFpsMode(G.S.settings.fpsCap));
            Screen = G.S.started ? ScreenId.Home : ScreenId.Intro;
            return true;
        }
        public string ExportSaveCode() { Save(); return SaveCodec.ToCode(G.S); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool focused) => Background.OnFocus(focused);
        void OnApplicationQuit() => Save();

        public void Toast(string text) => _toasts.Add((text, Time.unscaledTime + 2.4f));
        public void Modal(string title, float height, Action<Rect> body, float maxWidth = ModalW) { _modalTitle = title; _modalHeight = height; _modalBody = body; _modalMaxW = maxWidth; }
        public void CloseModal() { if (_modalBody != null) _modalClosedAt = Time.unscaledTime; _modalBody = null; _modalTitle = null; }
        public bool ModalOpen => _modalBody != null;

        public void Go(ScreenId s)
        {
            if (s != Screen) HeroModels.TrimSoon(); // let go of hero models the new screen doesn't show
            Screen = s; _scroll = Vector2.zero; CloseModal();
            _rp = RosterPanel.None; // a roster panel closes when you go anywhere (RosterUi.cs)
        }

        void Update()
        {
            Scroller.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) Back(); // Android back button
            if (Screen != ScreenId.Battle) UpdateLineup();
            if (Screen == ScreenId.Hero && !ModalOpen) UpdateHeroViewer();
            CheckPopups();
            UpdatePower();
        }

        // Battery and heat (owner decision 2026-11-27, performance review #2): the screen may sleep in menus
        // and stays awake only in battle (auto-replay included); the arena isn't drawn behind a menu that
        // covers the whole screen with an opaque painting (the title screen, Summon).
        void UpdatePower()
        {
            var sleep = Screen == ScreenId.Battle ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
            if (UnityEngine.Screen.sleepTimeout != sleep) UnityEngine.Screen.sleepTimeout = sleep;
            bool covered = !_shotNoUi && ((Screen == ScreenId.Intro && Skin.Tex("bg_title") != null) || (Screen == ScreenId.Summon && Skin.Tex("bg_summon") != null));
            Arena?.SetCovered(covered);
        }

        void Back()
        {
            if (Screen == ScreenId.Battle) { Battle.BackPressed(); return; }
            if (ModalOpen) { CloseModal(); return; }
            if (RosterBack()) return; // Release Heroes / Rank Up panels step back inside the Heroes screen
            switch (Screen)
            {
                case ScreenId.Titan: case ScreenId.Dungeon: case ScreenId.ArenaPvp: Go(ScreenId.Trials); break;
                case ScreenId.Hero: Go(ScreenId.Heroes); break;
                case ScreenId.Equip: Go(ScreenId.Hero); break;
                case ScreenId.Prep: Go(_prepBack); break;
                case ScreenId.FriendProfile: case ScreenId.Blocked: Go(ScreenId.Friends); break;
                case ScreenId.Chat: Go(ScreenId.FriendProfile); break;
                case ScreenId.Home: case ScreenId.Intro: Application.Quit(); break;
                default: Go(ScreenId.Home); break;
            }
        }

        // ---------- pop-ups, one at a time (QA review #8: Core/PopupQueue.cs) ----------
        // What's New, the Act II intro, first-time tips (modes.js checkTips) and the update banner all ask the
        // queue, so only one shows, in order, and never over the tutorial's coach panel.
        float _tipAt; bool _firstLaunch, _bannerOk;
        public bool UpdateBannerAllowed => _bannerOk; // Updater.cs draws its banner only when this is true
        void CheckPopups()
        {
            if (Time.unscaledTime < _tipAt) return;
            _tipAt = Time.unscaledTime + 0.35f;
            bool battleScreen = Screen == ScreenId.Battle, inBattle = battleScreen && Battle.InProgress;
            // The tutorial already walks a new player through Home and the battle screen.
            if (Tutorial.Step >= 6) Tips.MarkShown(G, "welcome");
            if (Tutorial.Step >= 8) Tips.MarkShown(G, "battle");
            var ctx = new PopupContext
            {
                ModalOpen = ModalOpen, TutorialBlocking = Tutorial.Blocking, FirstLaunch = _firstLaunch,
                InBattle = battleScreen, OnHome = Screen == ScreenId.Home, OnMap = Screen == ScreenId.Campaign,
                WhatsNewPending = WhatsNewPending(), Act2Pending = Act2IntroPending(),
                TipPending = (!battleScreen || inBattle) && Tips.Peek(G, Screen == ScreenId.Home, inBattle) != null,
                UpdateAvailable = Updater.LatestBuild > 0,
                SinceClose = Time.unscaledTime - _modalClosedAt,
            };
            var next = PopupQueue.Next(ctx);
            _bannerOk = next == Popup.UpdateBanner;
            switch (next)
            {
                case Popup.WhatsNew: ShowWhatsNewOnce(); break;
                case Popup.Act2Intro: ShowAct2Intro(); break;
                case Popup.Tip:
                    var tip = Tips.Next(G, Screen == ScreenId.Home, inBattle);
                    if (tip == null) break;
                    Save();
                    Modal(tip.Title, 300, b =>
                    {
                        Ui.Text(new Rect(b.x, b.y, b.width, b.height - 56), tip.Body, 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                        if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "Got it")) CloseModal();
                    });
                    break;
            }
        }

        // ---------- 3D lineup behind the menus ----------
        // Menu lineup x: 1.4 m per gap for normal heroes, more next to wide ones (Creatures.LineupXs).
        static float LineupX(List<string> ids, int i) => ids.Count <= 1 ? 0 : (float)Creatures.LineupXs(ids)[i];

        void UpdateLineup()
        {
            List<(HeroDef def, Element el)> want;
            List<GearLook> gear = null; // equipped gear on our own heroes (helm, chest, boots, weapon)
            float scale = 1f; bool single = false;
            switch (Screen)
            {
                case ScreenId.Hero: case ScreenId.Equip:
                    var h = G.HeroByUid(_heroUid);
                    want = h == null ? new List<(HeroDef, Element)>() : new List<(HeroDef, Element)> { (GameData.Hero(h.id), GameData.Hero(h.id).Element) };
                    gear = new List<GearLook> { G.GearLookOf(h) };
                    scale = 1.5f; single = true; break;
                case ScreenId.FriendProfile:
                    var fr = Online.Friend(_friendId);
                    want = fr?.profile?.heroes == null ? new List<(HeroDef, Element)>()
                        : fr.profile.heroes.Where(x => GameData.Hero(x.id) != null).Take(4).Select(x => (GameData.Hero(x.id), GameData.Hero(x.id).Element)).ToList();
                    break;
                case ScreenId.Chat: case ScreenId.Blocked:
                    want = new List<(HeroDef, Element)>(); break;
                case ScreenId.Intro:
                    want = GameData.Starters.Select(id => (GameData.Hero(id), GameData.Hero(id).Element)).ToList(); break;
                default:
                    want = G.Team.Select(x => (GameData.Hero(x.id), GameData.Hero(x.id).Element)).ToList();
                    gear = G.Team.Select(x => G.GearLookOf(x)).ToList(); break;
            }
            if (_shotLineup != null)
            {
                want = _shotLineup.Select(id => (GameData.Hero(id), GameData.Hero(id).Element)).ToList();
                gear = null; scale = 1f; single = false;
            }
            GearLook GearAt(int i) => gear != null && i < gear.Count ? gear[i] : null;
            string key = string.Join(",", want.Select((w, i) => w.def.Id + ":" + (GearAt(i)?.Key ?? "-"))) + (single ? "!" : "");
            if (key == _lineupKey) return;
            ClearLineup();       // resets _lineupKey, so set the key after it (the other way round rebuilt
            _lineupKey = key;    // the menu heroes every frame, and their models never finished loading)
            for (int i = 0; i < want.Count; i++)
            {
                var u = new Unit { Template = want[i].def, Name = want[i].def.Name, El = want[i].el, Ally = true, Key = "L" + i };
                var pos = single ? new Vector3(0, 0, 0.4f) : new Vector3(LineupX(want.Select(x => x.def.Id).ToList(), i), 0, 0.6f + (i % 2) * 0.5f);
                if (want.Count == 1 && !single) pos = new Vector3(0, 0, 0.6f);
                var toCam = Arena.Cam.transform.position - pos; toCam.y = 0;
                var v = CharacterView.Create(u, pos, Quaternion.LookRotation(toCam, Vector3.up).eulerAngles.y, scale, GearAt(i));
                _lineup.Add(v);
            }
        }
        // Hero screen viewer: drag sideways on the hero to spin them, tap to see an attack.
        Vector2 _viewDown; bool _viewTouch, _viewSpin; float _viewDownT;
        void UpdateHeroViewer()
        {
            if (_lineup.Count != 1 || !_lineup[0]) return;
            var v = _lineup[0];
            if (Input.GetMouseButtonDown(0))
            {
                // Only touches on the hero (screen box around the model) count.
                var c = Arena.Cam.WorldToScreenPoint(v.Center);
                Vector2 p = Input.mousePosition;
                _viewTouch = c.z > 0 && Mathf.Abs(p.x - c.x) < 110 * Ui.Scale && Mathf.Abs(p.y - c.y) < 150 * Ui.Scale;
                _viewSpin = false; _viewDown = p; _viewDownT = Time.unscaledTime;
            }
            if (!_viewTouch) return;
            if (Input.GetMouseButton(0))
            {
                Vector2 p = Input.mousePosition; float dx = p.x - _viewDown.x;
                if (!_viewSpin && Mathf.Abs(dx) > 12 * Ui.Scale / 2 && Mathf.Abs(dx) > Mathf.Abs(p.y - _viewDown.y)) _viewSpin = true;
                if (_viewSpin) { v.transform.Rotate(0, -(p.x - _viewDown.x) / Ui.Scale * 0.6f, 0, Space.World); _viewDown = p; }
            }
            if (Input.GetMouseButtonUp(0))
            {
                if (!_viewSpin && !Scroller.WasDragging && Time.unscaledTime - _viewDownT < 0.4f) v.Showcase();
                _viewTouch = false;
            }
        }

        public void ClearLineup()
        {
            foreach (var v in _lineup) if (v) Destroy(v.gameObject);
            _lineup.Clear(); _lineupKey = "";
        }

        // ---------- GUI ----------
        public static bool Running { get; private set; }
        void OnEnable() => Running = true;
        void OnDisable() => Running = false;

        // Everything the game draws, then the update banner last so it always sits on top (Updater.Overlay).
        void OnGUI()
        {
            if (_shotNoUi) return;
            try { DrawGui(); }
            finally { GUI.enabled = true; GUI.matrix = Matrix4x4.identity; Updater.Overlay(); }
        }

        void DrawGui()
        {
            // Battle, the modes hub and (in landscape) Home, the map and team setup use the whole width; menus a column.
            bool wide = Screen == ScreenId.Battle || Screen == ScreenId.Modes || ((Screen == ScreenId.Campaign || Screen == ScreenId.Prep || Screen == ScreenId.Home) && Ui.Landscape);
            Ui.Begin(wide);
            GUI.enabled = !ModalOpen; // a modal blocks taps on the screen behind it
            if (Screen == ScreenId.Battle) { Battle.DrawHud(); Tutorial.DrawBattle(this); DrawModalAndToasts(); return; }
            var safe = Ui.Safe;
            float top = safe.y, bottom = safe.yMax;
            if (Screen == ScreenId.Intro) { DrawIntro(new Rect(0, top, Ui.W, bottom - top)); DrawModalAndToasts(); return; }

            // The campaign map's Normal/Hard and Act switches sit in a taller top bar (UX review #7).
            float barH = Screen == ScreenId.Campaign && Ui.Landscape ? Mathf.Max(44f, Ui.Tap + 8) : 44f;
            DrawTopBar(new Rect(0, top, Ui.W, barH));
            // Full-width screens keep their content inside the safe area (camera hole, rounded corners: UX review #6).
            float cx0 = wide ? Mathf.Max(0, safe.x) : 0, cx1 = wide ? Mathf.Min(Ui.W, safe.xMax) : Ui.W;
            var content = new Rect(cx0, top + barH, cx1 - cx0, bottom - top - barH - 64);
            switch (Screen)
            {
                case ScreenId.Home: DrawHome(content); break;
                case ScreenId.Modes: DrawModes(content); break;
                case ScreenId.Campaign: if (Ui.Landscape) DrawWorldMap(content); else if (_hardTab && G.S.cleared > 0) DrawHardCampaign(content); else DrawCampaign(content); break;
                case ScreenId.Prep: if (Ui.Landscape) DrawPrepWide(content); else DrawPrep(content); break;
                case ScreenId.Heroes: if (_codex) DrawCodex(content); else DrawHeroes(content); break;
                case ScreenId.Achievements: DrawAchievements(content); break;
                case ScreenId.Hero: DrawHero(content); break;
                case ScreenId.Equip: DrawEquip(content); break;
                case ScreenId.Summon: DrawSummon(content); break;
                case ScreenId.Titan: DrawTitan(content); break;
                case ScreenId.Trials: DrawTrials(content); break;
                case ScreenId.Dungeon: DrawDungeon(content); break;
                case ScreenId.ArenaPvp: DrawArenaPvp(content); break;
                case ScreenId.Friends: DrawFriends(content); break;
                case ScreenId.FriendProfile: DrawFriendProfile(content); break;
                case ScreenId.Chat: DrawChat(content); break;
                case ScreenId.Blocked: DrawBlocked(content); break;
            }
            DrawNav(new Rect(0, bottom - 64, Ui.W, 64 + (Ui.H - bottom)));
            Tutorial.Draw(this, bottom - 64); // first-time guide (new saves only)
            DrawModalAndToasts();
        }

        // Carved top bar with a gold rule: currencies and the settings button.
        void DrawTopBar(Rect r)
        {
            GUI.DrawTexture(new Rect(Ui.Left, 0, Ui.FullW, r.yMax), Ui.GradientTex(Palette.Hex("#1d1726"), Palette.Hex("#120e19"), null, 2));
            Ui.GoldRule(new Rect(Ui.Left, r.yMax - 1, Ui.FullW, 1));
            // Contents inside the safe area, so the gold and the menu never sit under a camera hole or a rounded corner.
            var sc = Ui.SafeContent(8);
            float x = Ui.Landscape ? sc.x : Mathf.Max(8, sc.x);
            var bar = r;
            r = new Rect(r.x, r.y + (r.height - 44) / 2, r.width, 44); // chips stay centred when the bar is taller (the campaign map)
            // Currency chips: a drawn icon (Icons.cs) plus the amount.
            void Chip(Icon icon, string text, Color c, string painted = null)
            {
                var w = Mathf.Max(48, text.Length * 7.4f + 30);
                Ui.Panel(new Rect(x, r.y + 8, w, 28), Palette.Hex("#0d0a12"), Palette.Bronze, 13);
                // The owner's painted coin / essence / one painted crystal per shard kind (worn, mystic, radiant) when the skin is on.
                if (painted == null || !Icons.Painted(new Rect(x + 3, r.y + 10, 24, 24), painted)) Icons.Draw(new Rect(x + 8, r.y + 15, 14, 14), icon, c);
                Ui.Text(new Rect(x + 24, r.y + 8, w - 30, 28), text, 12, c, FontStyle.Bold, TextAnchor.MiddleCenter);
                x += w + 5;
            }
            Chip(Icon.Coin, Ui.Fmt(G.S.gold), Palette.Gold, "coin");
            Chip(Icon.Shard, G.S.shardCommon.ToString(), Palette.Hex("#c9a37b"), "shard_worn");
            Chip(Icon.Shard, G.S.shardMystic.ToString(), Palette.Blue, "shard_mystic");
            Chip(Icon.Shard, G.S.shardRadiant.ToString(), Palette.Hex("#ffb02e"), "shard_radiant");
            Chip(Icon.Essence, G.S.essence.ToString(), Palette.Hex("#8fd8ff"), "essence");
            // The menu button: 44 wide and the bar's full height to tap, the plate as before.
            float mx1 = Ui.Landscape ? sc.xMax : Mathf.Min(Ui.W - 8, sc.xMax);
            var menu = new Rect(mx1 - 38, r.y + 6, 38, 32);
            float tap = Mathf.Max(44, Ui.Tap);
            var menuHit = new Rect(mx1 - tap, r.y, tap, Mathf.Max(bar.yMax - r.y, tap)); // at least 48 dp square
            if (Ui.Button(menu, "", Ui.Kind.Secondary, 16) | Ui.Button(menuHit, "", Ui.Kind.Ghost)) OpenSettings();
            Icons.Draw(new Rect(menu.x + 10, menu.y + 7, 18, 18), Icon.Menu, Palette.Hex("#f3e3c3"));
            if (Screen == ScreenId.Campaign && Ui.Landscape) DrawCampaignSwitches(new Rect(x + 12, bar.y + 4, menuHit.x - x - 24, bar.height - 8));
        }

        // Cells of the bottom nav: a centred band (at most 560 wide) inside the safe area, the same on every screen.
        public static Rect NavCell(int i, float top)
        {
            var sc = Ui.SafeContent(8);
            float w = Ui.Landscape ? Mathf.Min(sc.width, 560) : Ui.W;
            float x0 = Ui.Landscape ? sc.center.x - w / 2 : 0;
            return new Rect(x0 + i * w / 6f, top, w / 6f, 64);
        }

        void DrawNav(Rect r)
        {
            // Stone bar with a gold edge and a glowing active tab (style.css .nav).
            GUI.DrawTexture(new Rect(Ui.Left, r.y, Ui.FullW, r.height), Ui.GradientTex(Palette.Hex("#1a1522"), Palette.Hex("#0d0a12"), null, 2));
            Ui.GoldRule(new Rect(Ui.Left, r.y, Ui.FullW, 1));
            var items = new (ScreenId id, string label, Icon icon)[] { (ScreenId.Home, "Home", Icon.Home), (ScreenId.Modes, "Battle", Icon.Sword), (ScreenId.Heroes, "Heroes", Icon.Helm), (ScreenId.Summon, "Summon", Icon.Portal), (ScreenId.Trials, "Trials", Icon.Tower), (ScreenId.Friends, "Friends", Icon.Friends) };
            for (int i = 0; i < items.Length; i++)
            {
                var cell = NavCell(i, r.y); float w = cell.width;
                var it = items[i];
                bool on = Screen == it.id || (it.id == ScreenId.Home && Screen == ScreenId.Achievements) || (it.id == ScreenId.Modes && (Screen == ScreenId.Campaign || Screen == ScreenId.Prep)) || (it.id == ScreenId.Heroes && (Screen == ScreenId.Hero || Screen == ScreenId.Equip))
                    || (it.id == ScreenId.Trials && (Screen == ScreenId.Titan || Screen == ScreenId.Dungeon || Screen == ScreenId.ArenaPvp))
                    || (it.id == ScreenId.Friends && (Screen == ScreenId.FriendProfile || Screen == ScreenId.Chat || Screen == ScreenId.Blocked));
                if (Ui.Button(cell, "", Ui.Kind.Ghost)) { if (it.id == ScreenId.Friends) OpenFriends(); else Go(it.id); }
                if (on) { Ui.Round(new Rect(cell.x + 6, r.y + 4, w - 12, 56), new Color(0.95f, 0.77f, 0.36f, 0.12f), null, 12); Ui.GoldRule(new Rect(cell.x + w * 0.22f, r.y + 1, w * 0.56f, 2)); }
                var tint = on ? Palette.Gold : Palette.Hex("#c4b89f"); // inactive labels 9:1 on the bar (art review H5: they were under 3:1 at phone size)
                // Painted nav icons (dimmed when not selected) with the skin on; drawn ones without.
                string pn = it.id == ScreenId.Home ? "home" : it.id == ScreenId.Modes ? "sword" : it.id == ScreenId.Heroes ? "helm" : it.id == ScreenId.Summon ? "portal" : it.id == ScreenId.Trials ? "tower" : "heart";
                if (!Icons.Painted(new Rect(cell.x + w / 2 - 15, r.y + 4, 30, 30), pn, on ? 1f : 0.55f)) Icons.Draw(new Rect(cell.x + w / 2 - 12, r.y + 8, 24, 24), it.icon, tint);
                Ui.Text(new Rect(cell.x, r.y + 34, w, 22), it.label, 12, tint, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
                int badge = it.id == ScreenId.Home ? HomeBadge() : it.id == ScreenId.Trials ? G.TrialsBadge()
                    : it.id == ScreenId.Friends && Online.SignedIn ? Online.Unread.messages + Online.Unread.requests : 0;
                if (badge > 0)
                {
                    var b = new Rect(cell.x + w / 2 + 14, r.y + 8, 18, 18);
                    Ui.Round(b, it.id == ScreenId.Trials ? Palette.Blue : Palette.Bad, null, 9);
                    Ui.Text(b, badge.ToString(), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
        }
        int HomeBadge()
        {
            int n = G.LoginAvailable ? 1 : 0;
            foreach (var q in GameData.DailyQuests) if (!G.QuestClaimed(q.Id) && G.QuestProgress(q.Id) >= q.Goal) n++;
            return n + G.AchReadyCount; // + achievements ready to claim (extras.js homeBadge)
        }

        void DrawModalAndToasts()
        {
            GUI.enabled = true;
            if (ModalOpen)
            {
                GUI.DrawTexture(Ui.Screen01, Ui.Solid(new Color(0, 0, 0, 0.7f)));
                float mx0 = Mathf.Max(16, Ui.Safe.x + 16), mx1 = Mathf.Min(Ui.W - 16, Ui.Safe.xMax - 16); // inside the safe area
                float mw = Mathf.Min(mx1 - mx0, _modalMaxW); // centred, at most ModalW (or WideModalW) wide
                var box = new Rect((mx0 + mx1) / 2 - mw / 2, Mathf.Max(Ui.Safe.y + 20, (Ui.H - _modalHeight) / 2), mw, _modalHeight);
                Ui.Card(box, true); // the web's framed .mbox
                if (!string.IsNullOrEmpty(_modalTitle)) Ui.Heading(new Rect(box.x, box.y + 14, box.width, 34), _modalTitle, 24, TextAnchor.MiddleCenter);
                _modalBody?.Invoke(new Rect(box.x + 16, box.y + 56, box.width - 32, box.height - 72));
            }
            _toasts.RemoveAll(t => t.until < Time.unscaledTime);
            float y = Ui.Safe.yMax - 120;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                float tw = Mathf.Min(Ui.W - 60, 620);
                var r = new Rect(Ui.W / 2 - tw / 2, y, tw, 36);
                Ui.Panel(r, new Color(0, 0, 0, 0.85f), Palette.Line, 10);
                Ui.Text(r, _toasts[i].text, 13, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
                y -= 42;
            }
        }
    }
}
