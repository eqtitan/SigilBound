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
        public const string SaveKey = "sigilbound.save.v1";
        public Game G;
        public Arena Arena;
        public ScreenId Screen = ScreenId.Home;
        public BattleController Battle;

        // screen state
        int _heroUid; GameData.Slot _slot; int _chapterOpen = -1; int _prepStage = -1; bool _prepTitan; ScreenId _prepBack;
        Vector2 _scroll; string _sort = "power";
        readonly List<CharacterView> _lineup = new List<CharacterView>();
        string _lineupKey = "";

        // modal + toasts
        string _modalTitle; Action<Rect> _modalBody; float _modalHeight = 260;
        readonly List<(string text, float until)> _toasts = new List<(string, float)>();

        void Awake()
        {
            // Landscape on phones, either way round (the owner chose landscape for phone and PC).
            UnityEngine.Screen.autorotateToPortrait = false;
            UnityEngine.Screen.autorotateToPortraitUpsideDown = false;
            UnityEngine.Screen.autorotateToLandscapeLeft = true;
            UnityEngine.Screen.autorotateToLandscapeRight = true;
            UnityEngine.Screen.orientation = ScreenOrientation.AutoRotation;
            UnityEngine.Screen.sleepTimeout = SleepTimeout.NeverSleep;
            G = new Game(Load());
            G.EnsureDaily();
            Arena = Arena.Create();
            // Frame-rate cap (60, or 30 to run cooler) and graphics tier mapped to URP settings.
            Quality.Create(gameObject, G.S.settings.quality, G.S.settings.fpsCap).Sun = Arena.Sun;
            Quality.Busy = () => Battle != null && Battle.InProgress; // Auto holds tier changes until the battle ends
            Quality.I.Apply();
            Quality.Notice = Toast; // thermal fallback messages
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
            Tips.Seed(G); // existing players skip beginner tips for things they've done
            Arena.Build(Math.Min(GameData.Chapters.Length - 1, G.S.cleared / GameData.StagesPerChapter), false);
            Arena.FrameMenu();
            StartShotsIfAsked(); // -sbShots=DIR: screenshot tour (ShotsMode.cs)
        }

        // ---------- save ----------
        // Stored in the web game's JSON layout (SaveCodec), so save codes move between the two.
        static SaveData Load()
        {
            try
            {
                var json = PlayerPrefs.GetString(SaveKey, "");
                if (!string.IsNullOrEmpty(json)) return SaveCodec.FromJson(json) ?? new SaveData();
            }
            catch (Exception e) { Debug.LogWarning("Save unreadable, starting fresh: " + e.Message); }
            return new SaveData();
        }
        public void Save()
        {
            if (NoSave) return; // screenshot mode (ShotsMode.cs) never touches the real save
            G.S.gameVersion = Application.version;
            var json = SaveCodec.ToJson(G.S);
            G.S.sourceJson = json;
            PlayerPrefs.SetString(SaveKey, json); PlayerPrefs.Save();
        }

        // Replace this device's progress with a save code from the web game (or another device).
        public bool ImportSaveCode(string code)
        {
            var s = SaveCodec.FromCode(code);
            if (s == null) return false;
            G = new Game(s);
            G.EnsureDaily();
            Save();
            _lineupKey = "";
            Quality.I?.Set(G.S.settings.quality, G.S.settings.fpsCap);
            Screen = G.S.started ? ScreenId.Home : ScreenId.Intro;
            return true;
        }
        public string ExportSaveCode() { Save(); return SaveCodec.ToCode(G.S); }
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationQuit() => Save();

        public void Toast(string text) => _toasts.Add((text, Time.unscaledTime + 2.4f));
        public void Modal(string title, float height, Action<Rect> body) { _modalTitle = title; _modalHeight = height; _modalBody = body; }
        public void CloseModal() { _modalBody = null; _modalTitle = null; }
        public bool ModalOpen => _modalBody != null;

        public void Go(ScreenId s) { Screen = s; _scroll = Vector2.zero; CloseModal(); }

        void Update()
        {
            Scroller.Tick();
            if (Input.GetKeyDown(KeyCode.Escape)) Back(); // Android back button
            if (Screen != ScreenId.Battle) UpdateLineup();
            if (Screen == ScreenId.Hero && !ModalOpen) UpdateHeroViewer();
            CheckTips();
        }

        void Back()
        {
            if (Screen == ScreenId.Battle) { Battle.BackPressed(); return; }
            if (ModalOpen) { CloseModal(); return; }
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

        // ---------- first-time tips (modes.js checkTips) ----------
        float _tipAt;
        void CheckTips()
        {
            if (ModalOpen || Time.unscaledTime < _tipAt) return;
            _tipAt = Time.unscaledTime + 0.35f;
            bool inBattle = Screen == ScreenId.Battle && Battle.InProgress;
            if (Screen == ScreenId.Battle && !inBattle) return;
            var tip = Tips.Next(G, Screen == ScreenId.Home, inBattle);
            if (tip == null) return;
            Save();
            Modal(tip.Title, 300, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, b.height - 56), tip.Body, 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "Got it")) CloseModal();
            });
        }

        // ---------- 3D lineup behind the menus ----------
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
            GearLook GearAt(int i) => gear != null && i < gear.Count ? gear[i] : null;
            string key = string.Join(",", want.Select((w, i) => w.def.Id + ":" + (GearAt(i)?.Key ?? "-"))) + (single ? "!" : "");
            if (key == _lineupKey) return;
            ClearLineup();       // resets _lineupKey, so set the key after it (the other way round rebuilt
            _lineupKey = key;    // the menu heroes every frame, and their models never finished loading)
            for (int i = 0; i < want.Count; i++)
            {
                var u = new Unit { Template = want[i].def, Name = want[i].def.Name, El = want[i].el, Ally = true, Key = "L" + i };
                var pos = single ? new Vector3(0, 0, 0.4f) : new Vector3(-2.1f + i * (4.2f / Math.Max(1, want.Count - 1)) * (want.Count > 1 ? 1 : 0), 0, 0.6f + (i % 2) * 0.5f);
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
        void OnGUI()
        {
            Ui.Begin(Screen == ScreenId.Battle || Screen == ScreenId.Modes || ((Screen == ScreenId.Campaign || Screen == ScreenId.Prep) && Ui.Landscape)); // battle and the modes hub use the whole width; menus a column
            GUI.enabled = !ModalOpen; // a modal blocks taps on the screen behind it
            if (Screen == ScreenId.Battle) { Battle.DrawHud(); DrawModalAndToasts(); return; }
            var safe = Ui.Safe;
            float top = safe.y, bottom = safe.yMax;
            if (Screen == ScreenId.Intro) { DrawIntro(new Rect(0, top, Ui.W, bottom - top)); DrawModalAndToasts(); return; }

            DrawTopBar(new Rect(0, top, Ui.W, 44));
            var content = new Rect(0, top + 44, Ui.W, bottom - top - 44 - 64);
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
            DrawModalAndToasts();
        }

        // Carved top bar with a gold rule: currencies and the settings button.
        void DrawTopBar(Rect r)
        {
            GUI.DrawTexture(new Rect(Ui.Left, 0, Ui.FullW, r.yMax), Ui.GradientTex(Palette.Hex("#1d1726"), Palette.Hex("#120e19"), null, 2));
            Ui.GoldRule(new Rect(Ui.Left, r.yMax - 1, Ui.FullW, 1));
            float x = 8;
            // Currency chips: a drawn icon (Icons.cs) plus the amount.
            void Chip(Icon icon, string text, Color c)
            {
                var w = Mathf.Max(48, text.Length * 7.4f + 30);
                Ui.Panel(new Rect(x, r.y + 8, w, 28), Palette.Hex("#0d0a12"), Palette.Bronze, 13);
                Icons.Draw(new Rect(x + 8, r.y + 15, 14, 14), icon, c);
                Ui.Text(new Rect(x + 24, r.y + 8, w - 30, 28), text, 12, c, FontStyle.Bold, TextAnchor.MiddleCenter);
                x += w + 5;
            }
            Chip(Icon.Coin, Ui.Fmt(G.S.gold), Palette.Gold);
            Chip(Icon.Shard, G.S.shardCommon.ToString(), Palette.Hex("#c9a37b"));
            Chip(Icon.Shard, G.S.shardMystic.ToString(), Palette.Blue);
            Chip(Icon.Shard, G.S.shardRadiant.ToString(), Palette.Hex("#ffb02e"));
            Chip(Icon.Essence, G.S.essence.ToString(), Palette.Hex("#8fd8ff"));
            var menu = new Rect(Ui.W - 46, r.y + 6, 38, 32);
            if (Ui.Button(menu, "", Ui.Kind.Secondary, 16)) OpenSettings();
            Icons.Draw(new Rect(menu.x + 10, menu.y + 7, 18, 18), Icon.Menu, Palette.Hex("#f3e3c3"));
        }

        void DrawNav(Rect r)
        {
            // Stone bar with a gold edge and a glowing active tab (style.css .nav).
            GUI.DrawTexture(new Rect(Ui.Left, r.y, Ui.FullW, r.height), Ui.GradientTex(Palette.Hex("#1a1522"), Palette.Hex("#0d0a12"), null, 2));
            Ui.GoldRule(new Rect(Ui.Left, r.y, Ui.FullW, 1));
            var items = new (ScreenId id, string label, Icon icon)[] { (ScreenId.Home, "Home", Icon.Home), (ScreenId.Modes, "Battle", Icon.Sword), (ScreenId.Heroes, "Heroes", Icon.Helm), (ScreenId.Summon, "Summon", Icon.Portal), (ScreenId.Trials, "Trials", Icon.Tower), (ScreenId.Friends, "Friends", Icon.Friends) };
            float w = Ui.W / items.Length;
            for (int i = 0; i < items.Length; i++)
            {
                var it = items[i];
                bool on = Screen == it.id || (it.id == ScreenId.Home && Screen == ScreenId.Achievements) || (it.id == ScreenId.Modes && (Screen == ScreenId.Campaign || Screen == ScreenId.Prep)) || (it.id == ScreenId.Heroes && (Screen == ScreenId.Hero || Screen == ScreenId.Equip))
                    || (it.id == ScreenId.Trials && (Screen == ScreenId.Titan || Screen == ScreenId.Dungeon || Screen == ScreenId.ArenaPvp))
                    || (it.id == ScreenId.Friends && (Screen == ScreenId.FriendProfile || Screen == ScreenId.Chat || Screen == ScreenId.Blocked));
                var cell = new Rect(i * w, r.y, w, 64);
                if (Ui.Button(cell, "", Ui.Kind.Ghost)) { if (it.id == ScreenId.Friends) OpenFriends(); else Go(it.id); }
                if (on) { GUI.DrawTexture(new Rect(cell.x + 6, r.y + 4, w - 12, 56), Ui.RoundTex(new Color(0.95f, 0.77f, 0.36f, 0.12f), null, 12)); Ui.GoldRule(new Rect(cell.x + w * 0.22f, r.y + 1, w * 0.56f, 2)); }
                var tint = on ? Palette.Gold : Palette.Hex("#9b8f7a");
                Icons.Draw(new Rect(cell.x + w / 2 - 12, r.y + 8, 24, 24), it.icon, tint);
                Ui.Text(new Rect(cell.x, r.y + 34, w, 22), it.label, 11, tint, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
                int badge = it.id == ScreenId.Home ? HomeBadge() : it.id == ScreenId.Trials ? G.TrialsBadge()
                    : it.id == ScreenId.Friends && Online.SignedIn ? Online.Unread.messages + Online.Unread.requests : 0;
                if (badge > 0)
                {
                    var b = new Rect(cell.x + w / 2 + 14, r.y + 8, 18, 18);
                    GUI.DrawTexture(b, Ui.RoundTex(it.id == ScreenId.Trials ? Palette.Blue : Palette.Bad, null, 9));
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
                var box = new Rect(16, Mathf.Max(Ui.Safe.y + 20, (Ui.H - _modalHeight) / 2), Ui.W - 32, _modalHeight);
                Ui.Card(box, true); // the web's framed .mbox
                if (!string.IsNullOrEmpty(_modalTitle)) Ui.Heading(new Rect(box.x, box.y + 14, box.width, 34), _modalTitle, 24, TextAnchor.MiddleCenter);
                _modalBody?.Invoke(new Rect(box.x + 16, box.y + 56, box.width - 32, box.height - 72));
            }
            _toasts.RemoveAll(t => t.until < Time.unscaledTime);
            float y = Ui.Safe.yMax - 120;
            for (int i = _toasts.Count - 1; i >= 0; i--)
            {
                var r = new Rect(30, y, Ui.W - 60, 36);
                Ui.Panel(r, new Color(0, 0, 0, 0.85f), Palette.Line, 10);
                Ui.Text(r, _toasts[i].text, 13, Palette.Text, FontStyle.Normal, TextAnchor.MiddleCenter, true);
                y -= 42;
            }
        }
    }
}
