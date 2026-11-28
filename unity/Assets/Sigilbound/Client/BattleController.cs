// Runs a battle (campaign stage, Titan, dungeon floor or arena rival): spawns 3D units on the
// rune circle's rim, drives the turn loop, animates events, takes player input (skill + target),
// and shows results.
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public enum BattleMode { Stage, Titan, Dungeon, Arena, Hard }

    public sealed class BattleSpec
    {
        public BattleMode Mode; public int Stage; public DungeonKey Dungeon; public int Floor; public Rival Rival; public bool Practice;
        public static BattleSpec ForStage(int idx) => new BattleSpec { Mode = BattleMode.Stage, Stage = idx };
        public static BattleSpec ForHard(int idx) => new BattleSpec { Mode = BattleMode.Hard, Stage = idx };
        public static BattleSpec Titan() => new BattleSpec { Mode = BattleMode.Titan };
        public static BattleSpec TitanPractice() => new BattleSpec { Mode = BattleMode.Titan, Practice = true }; // no rewards, no try used
        public static BattleSpec ForDungeon(DungeonKey k, int floor) => new BattleSpec { Mode = BattleMode.Dungeon, Dungeon = k, Floor = floor };
        public static BattleSpec ForArena(Rival rv) => new BattleSpec { Mode = BattleMode.Arena, Rival = rv };
    }

    public sealed partial class BattleController : MonoBehaviour
    {
        public App App;
        Battle _b; BattleSpec _spec;
        public Battle Engine => _b; // the screenshot tour stages UI states with it
        readonly Dictionary<Unit, CharacterView> _views = new Dictionary<Unit, CharacterView>();
        Unit _actor; int _sel; bool _awaiting; string _banner = ""; bool _finished;
        Coroutine _loop;

        Game G => App.G;
        bool Auto => AutoClimb.AutoBattle(G.S.settings, Climbing); // Auto Climb keeps Auto battle on (AutoClimbUi.cs)
        bool IsTitan => _spec.Mode == BattleMode.Titan;
        public BattleMode Mode => _spec?.Mode ?? BattleMode.Stage;
        public bool InProgress => _b != null && !_finished;
        // The Titan's figure in this battle, if any (the screenshot tour's pose check).
        public CharacterView TitanView => _views.FirstOrDefault(kv => kv.Key.Titan).Value;
        // Auto-replay is looping campaign battles, or Auto Climb is running (Quality caps both at 30 fps).
        public bool Replaying => _b != null && _spec != null && ((_spec.Mode == BattleMode.Stage && G.S.settings.autoReplay) || Climbing || _climbPending);

        public void Begin(int stage) => Begin(stage < 0 ? BattleSpec.Titan() : BattleSpec.ForStage(stage));

        public void Begin(BattleSpec spec)
        {
            if (G.Team.Count == 0) { App.Toast("Choose at least one hero."); return; }
            if (spec.Mode == BattleMode.Dungeon && !G.CanEnterFloor(spec.Dungeon, spec.Floor)) { App.Toast("No runs left today for this dungeon."); return; }
            if (spec.Mode == BattleMode.Arena && G.S.daily.arenaTries <= 0) { App.Toast("No arena fights left today."); return; }
            if (spec.Mode == BattleMode.Titan && !spec.Practice && G.S.daily.titanTries <= 0) { App.Toast("No Titan tries left today."); return; }
            if (spec.Mode == BattleMode.Hard && !G.HardOpen(spec.Stage)) { App.Toast("Clear the previous Hard stage first."); return; }
            // Quit rule (owner decision 2026-11-27): a Titan try and an arena fight count from the start, saved
            // at once, so leaving or closing the game mid-fight is no free retry. Practice and the campaign are free.
            if (spec.Mode == BattleMode.Titan && !spec.Practice) { G.StartTitan(); App.Save(); }
            if (spec.Mode == BattleMode.Arena) { G.StartArena(spec.Rival); App.Save(); }
            _spec = spec;
            if (spec.Mode == BattleMode.Arena) _arenaSlot = SlotOf(spec.Rival);
            _climbPending = false; _climbRetreat = false;
            _titanShown = 0; // one controller lasts the session: a real Titan fight after practice must start full (in-house report, build 190)
            App.ClearLineup();
            HeroModels.TrimSoon(); // auto-replay starts battles back to back: free the last one's leftovers
            _b = spec.Mode switch
            {
                BattleMode.Titan => Core.Battle.ForTitan(G),
                BattleMode.Dungeon => Core.Battle.ForDungeon(G, spec.Dungeon, spec.Floor),
                BattleMode.Arena => Core.Battle.ForArena(G, spec.Rival),
                BattleMode.Hard => Core.Battle.ForHard(G, spec.Stage),
                _ => Core.Battle.ForStage(G, spec.Stage),
            };
            // Scenery: as battleScene() in modes.js.
            int chapter = spec.Mode switch
            {
                BattleMode.Titan => 9,
                BattleMode.Dungeon => Trials.Dungeon(spec.Dungeon).Chapter,
                BattleMode.Arena => 4 + spec.Rival.Ids.Count % 5,
                _ => Game.ChapterOf(spec.Stage),
            };
            // Boss stages of the campaign (normal and Hard) and the Titan get the interactive backdrop.
            bool bossArena = IsTitan || ((spec.Mode == BattleMode.Stage || spec.Mode == BattleMode.Hard) && Game.IsBossStage(spec.Stage));
            // Campaign battles stand at their route stop (Core.StoryScenes); Hard mode is the same stop by night.
            bool campaign = spec.Mode == BattleMode.Stage || spec.Mode == BattleMode.Hard;
            App.Arena.Build(Mathf.Min(chapter, Chapters.Length - 1), IsTitan, bossArena, campaign ? spec.Stage % StagesPerChapter : -1, spec.Mode == BattleMode.Hard);
            SetRing(); // before framing: the camera fits the ring at its line-up's size
            App.Arena.FrameBattle(CamKey, IsTitan);
            Clear();
            Spawn(_b.Allies, true); Spawn(_b.Enemies, false);
            _finished = false; _awaiting = false; _banner = ""; _actor = null;
            ResetCinema(); // a cinematic cut short by leaving the last battle
            _t0 = Time.unscaledTime; _paused = false; _taken.Clear(); _healed.Clear();
            App.Go(ScreenId.Battle);
            // Music: the Titan its own; a campaign boss stage waits in silence for the lair reveal's swell and
            // the boss theme starts as the fight does (Loop); act 2 stages have their own battle theme.
            _bossMusic = campaign && Game.IsBossStage(spec.Stage);
            Sound.I?.StartMusic(IsTitan ? "titan" : _bossMusic ? null : campaign && chapter >= ClassicChapters ? "act2" : "battle");
            Sound.I?.Ambience(GameData.ThemeOf(Mathf.Min(chapter, Chapters.Length - 1)), IsTitan);   // a quiet bed of the arena's weather
            Time.timeScale = Mathf.Clamp(G.S.settings.speed, 1, 3);
            _loop = StartCoroutine(Loop());
        }

        // Room each unit takes in the line-up: creatures and winged / big-shield heroes more (Creatures.LineupWidth),
        // a boss its 1.35x scale. The ring is sized once per battle for the allies and the longest wave (art review M1),
        // so the team never moves when a new wave arrives, and both rows share one stand ellipse (_maxWidth).
        static double UnitWidth(Unit u) => Creatures.LineupWidth(u.Template.Id) * (u.Boss ? BattleLayout.BossWidth : 1);
        double _maxWidth = 1;
        void SetRing()
        {
            var allies = _b.Allies.Select(UnitWidth).ToList();
            double ring = 1, widest = allies.Count > 0 ? allies.Max() : 1;
            foreach (var wave in _b.Waves)
            {
                var foes = wave.Where(x => !x.Titan).Select(UnitWidth).ToList();
                ring = System.Math.Max(ring, BattleLayout.RingScale(allies, foes));
                if (foes.Count > 0) widest = System.Math.Max(widest, foes.Max());
            }
            BattleLayout.Ring = ring; _maxWidth = widest;
            App.Arena.SetRing((float)ring);
        }

        void Spawn(List<Unit> units, bool ally)
        {
            var widths = units.Select(UnitWidth).ToList(); // creatures, wide heroes and bosses take more room
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                float scale = u.Titan ? (float)TitanSpec.Current.ViewScale : u.Boss ? (float)BattleLayout.BossWidth : 1f;
                var pos = u.Titan ? Arena.ToUnity(BattleLayout.TitanSlot()) : Arena.Slot(ally, i, widths, _maxWidth);
                _views[u] = CharacterView.Create(u, pos, Arena.FacingYaw(pos), scale, G.GearLookOf(u));
            }
        }
        void Clear()
        {
            foreach (var v in _views.Values) if (v) Destroy(v.gameObject);
            _views.Clear();
        }

        public void End(ScreenId to)
        {
            if (_loop != null) StopCoroutine(_loop);
            Time.timeScale = 1; _paused = false;
            _auto = null;
            _climb = null; _climbPending = false; // leaving the battle ends any climb (its summary has shown)
            Clear();
            App.CloseModal();
            BattleLayout.Ring = 1; _maxWidth = 1; // the menus' arena: the classic ring
            App.Arena.Build(Mathf.Min(Chapters.Length - 1, G.S.cleared / StagesPerChapter), false);
            App.Arena.FrameMenu();
            App.Go(to);
        }

        // Where "Back"/"Continue" leaves a battle for (resultBack in the web game).
        ScreenId BackScreen => _spec.Mode switch
        {
            BattleMode.Titan => ScreenId.Titan, BattleMode.Dungeon => ScreenId.Dungeon, BattleMode.Arena => ScreenId.ArenaPvp, _ => ScreenId.Campaign,
        };

        // Android Back: asks to retreat mid-battle; on a result popup it leaves the battle (0.19.2).
        public void BackPressed()
        {
            if (_finished && _climb != null && App.ModalOpen) { AutoClimb.SetClimb(G.S.settings, false); App.Save(); StopClimb(ClimbStop.ByPlayer); }
            else if (_finished) { G.S.settings.autoReplay = false; App.Save(); End(BackScreen); }
            else if (App.ModalOpen) App.CloseModal();
            else AskRetreat();
        }

        // Titan fights keep their own view (PlayerPrefs, not the save): Worm's-eye by default.
        static string TitanCam { get => PlayerPrefs.GetString("sb.titanCam", BattleLayout.DefaultTitanCamera); set { PlayerPrefs.SetString("sb.titanCam", value); PlayerPrefs.Save(); } }
        string CamKey => IsTitan ? TitanCam : G.S.settings.cam;

        public void CycleCamera()
        {
            var cams = IsTitan ? BattleLayout.TitanCycle : BattleLayout.Cameras;
            int i = System.Array.FindIndex(cams, c => c.Key == BattleLayout.AnyCamera(CamKey, IsTitan).Key);
            var next = cams[(i + 1) % cams.Length];
            if (IsTitan) TitanCam = next.Key; else { G.S.settings.cam = next.Key; App.Save(); }
            App.Arena.FrameBattle(next.Key, IsTitan);
            App.Toast($"View: {next.Name} ({(i + 1) % cams.Length + 1}/{cams.Length})");
        }

        // ---------- Titan cinematics (owner picks 2026-11-26; presentation only, results unchanged) ----------
        bool _cinema, _skippable;  // HUD hidden while a cinematic plays; the intro can be skipped by a tap
        // Above the horn tips: from its head bone (follows it as it bows, leaps or strides), the horns'
        // height over the head (TitanSpec.HornTop) and room for the tag; else a fixed height over its feet.
        // Where a unit's floating numbers start (Vfx.Number): its head-tag point, the same as the HUD tag's, so
        // numbers float above the name and bars instead of on the body (art review M3). The Titan's is above its horns.
        Vector3 NumberAnchor(Unit u, CharacterView v) => u != null && u.Titan ? TitanTagPoint(v)
            : v.HeadTop ? v.HeadTop.position + Vector3.up * 0.25f : v.transform.position + Vector3.up * 2.2f;
        Transform _titanHead; CharacterView _titanHeadOf;
        Vector3 TitanTagPoint(CharacterView v)
        {
            var spec = TitanSpec.Current;
            if (_titanHeadOf != v) { _titanHeadOf = v; _titanHead = null; }
            if (_titanHead == null && v.Hero != null && v.Hero.Root != null) _titanHead = BodyShaper.Find(v.Hero.Root.transform, spec.HeadBone);
            float above = (float)((spec.HornTop - 1) * spec.Head) + 0.6f;
            return _titanHead != null ? new Vector3(v.transform.position.x, Mathf.Max(_titanHead.position.y + above, v.transform.position.y + 2f), v.transform.position.z)
                                      : v.transform.position + Vector3.up * (float)BattleLayout.WormHornTop;
        }
        static bool IsHdTitan(CharacterView v) => v != null && v.Hero != null && v.Hero.Style == AnimStyle.TitanHd;
        // The HD Titan's real head height (model 4.8 m x the view's 1.3 scale); the HeadTop marker sits at 87% of
        // a nominal height and under-read it. The old stand-in Titan keeps its marker.
        const float TitanTagGap = 18;
        float TitanHeight(CharacterView v) => IsHdTitan(v) ? (float)BattleLayout.TitanHead : v != null && v.HeadTop ? Mathf.Max(2f, v.HeadTop.position.y - v.transform.position.y) : (float)BattleLayout.TitanHead;
        static float Ease(float u) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(u));

        // Arrival (art review H3): shot from the heroes' side, their backs in the foreground and the Titan facing
        // the camera. It opens low behind the team on its legs, cranes up its front to its face as it roars
        // (BattleLayout.TitanIntroKeys), then blends into the battle view. About 3 s; a tap skips it.
        IEnumerator TitanIntro()
        {
            var tu = _views.Keys.FirstOrDefault(k => k.Titan); if (tu == null) yield break;
            var tv = _views[tu];
            for (float w = 0; w < 8f && tv != null && tv.Hero == null; w += Time.unscaledDeltaTime) yield return null; // its model is loading (preloaded at start, so normally instant)
            if (tv == null) yield break;
            var arena = App.Arena; float keepSpeed = Time.timeScale; Time.timeScale = 1;
            _cinema = _skippable = true;
            var T = tv.transform.position;
            var heroXs = _views.Where(kv => kv.Key.Ally && kv.Value != null).Select(kv => (double)kv.Value.transform.position.x).ToList();
            var (ka, kb) = BattleLayout.TitanIntroKeys(Mathf.Max(0.2f, arena.Cam.aspect), heroXs);
            var eyeA = Arena.ToUnity(ka.Eye); var eyeB = Arena.ToUnity(kb.Eye);
            var lookA = Arena.ToUnity(ka.Target); var lookB = Arena.ToUnity(kb.Target);
            float H = TitanHeight(tv); bool roared = false, roaring = false; int steps = 0;
            // Its own clips (2026-11-28): it knuckle-walks the last few metres into its place, then rears up and roars,
            // seams flaring. The shared clips: it stands and shifts its weight, then roars (as before).
            var m = tv.Hero; var body = TitanBody.Of(tv);
            string walk = TClip(m, "walk", "Walk_Loop"), roar = TClip(m, "roar", "Yes", "Wave", "Interact");
            bool own = walk == TS.ClipOf("walk", null) && roar == TS.ClipOf("roar", null);
            var start = T - tv.transform.forward * 3f; float roarEv = own ? (float)TS.Ev("roar", "roar", 0.33) : 0;
            if (own) { tv.transform.position = start; m.Play(walk, true, 0.1f, 0.9f); _plantPrev = -1; }
            for (float t = 0; t < 3.1f; t += Time.unscaledDeltaTime)
            {
                if (t > 0.2f && Input.GetMouseButtonDown(0)) break;
                float u = Ease(t / 1.6f);
                arena.Override(Vector3.Lerp(eyeA, eyeB, u), Vector3.Lerp(lookA, lookB, u), Mathf.Lerp((float)ka.Fov, (float)kb.Fov, u), t < 2.3f ? 1f : 1f - Ease((t - 2.3f) / 0.8f));
                if (own)
                {
                    if (t < 1.0f) { tv.transform.position = Vector3.Lerp(start, T, Ease(t / 1.0f)); WalkPlants(tv, m, walk, tu, 0.1f, 0.8f); }
                    else if (!roaring) { roaring = true; tv.transform.position = T; m.PlayFrom(roar, 0f, 1f); body?.Charge(0.85f, 0.35f); }
                    if (roaring && !roared && m.CurrentNormalized >= roarEv)
                    {
                        roared = true; Vfx.I.ShakeLong(0.3f, 0.9f); Sound.I?.Play("titanRoar", 1f, 1, 0); SkillFx.I?.Dust(T, H * 0.7f, tu.El); body?.Pulse();
                        StartCoroutine(Later(0.9f, () => body?.Charge(0, 0.6f)));
                    }
                    yield return null; continue;
                }
                // it shifts its weight as the camera climbs: plates grinding, chains rattling, the ground thudding
                if (steps < 2 && t >= 0.35f + 0.6f * steps) { steps++; Sound.I?.PlayAt("titanStep", T, 0.8f, 1, 0.04f); }
                if (!roared && t >= 1.6f)
                {
                    roared = true;
                    if (tv.Hero != null) foreach (var n in new[] { "Yes", "Wave", "Interact" }) if (tv.Hero.Has(n)) { tv.Hero.Play(n, false, 0.2f, 0.9f); break; }
                    Vfx.I.ShakeLong(0.3f, 0.9f); Sound.I?.Play("titanRoar", 1f, 1, 0);
                    SkillFx.I?.Dust(T, H * 0.7f, tu.El);
                }
                yield return null;
            }
            if (own && tv != null) { tv.transform.position = T; if (!roaring) m.Idle(0.2f); body?.Charge(0, 0.5f); } // a tap skipped it
            arena.ClearOverride(); _cinema = _skippable = false; Time.timeScale = _paused ? 0 : keepSpeed;
        }

        // ---------- lair reveal (owner pick C, 2026-11-27; presentation only) ----------
        // On a campaign boss stage (stage 6, Normal and Hard) the camera starts low behind the team looking up at
        // the lair's landmark whole against the sky, rises, then settles into the battle view, while the lair's
        // name fades in and out. About 2.5 s at normal speed (timeScale 1, whatever the battle speed); a tap
        // skips it; the fight starts after it. Landmark and height: Core.StoryScenes.RevealOf.
        string _revealTitle, _revealSub; float _revealT;
        const float RevealLen = 2.5f;
        static readonly HashSet<int> _lairsRevealed = new HashSet<int>();
        bool _bossMusic;   // this battle plays the boss theme once its lair reveal ends
        public static void ForgetLairReveals() => _lairsRevealed.Clear(); // the screenshot tour
        IEnumerator LairReveal()
        {
            if ((_spec.Mode != BattleMode.Stage && _spec.Mode != BattleMode.Hard) || !Game.IsBossStage(_spec.Stage)) yield break;
            int chapter = Game.ChapterOf(_spec.Stage);
            var rv = StoryScenes.RevealOf(chapter);
            if (rv == null) yield break;
            // Once per lair per session: the arrival is the point; replays of the same boss skip it.
            // While climbing, a lair already seen (this session, or cleared before this fight) is skipped too.
            bool clearedBefore = _spec.Mode == BattleMode.Hard ? _spec.Stage < G.S.hardCleared : _spec.Stage < G.S.cleared;
            bool seen = !_lairsRevealed.Add(chapter);
            if (AutoClimb.SkipLairReveal(Climbing, seen, clearedBefore)) yield break;
            var (pc, top) = rv.Value;
            // the generator may have moved the landmark to frame it for the default view (Core/SceneGen)
            var lmk = App.Arena.Layout?.Landmark;
            if (lmk != null) { pc = new Piece { Mesh = lmk.Mesh, X = lmk.X, Z = lmk.Z, Scale = lmk.Scale }; top = App.Arena.Layout.LandmarkTop; }
            var arena = App.Arena; float keepSpeed = Time.timeScale; Time.timeScale = 1;
            _cinema = _skippable = true;
            _revealTitle = StoryScenes.LairTitle(chapter); _revealSub = Chapters[chapter];
            var L = new Vector3(pc.X, 0, pc.Z);
            Sound.I?.PlayAt("lairReveal", L, 0.9f);
            // Three keys: low behind the team looking up at the whole landmark; risen and pulled back; then the
            // override fades out into the battle view (the framed pose).
            // The low eye looks through a gap in the team: the sight line to the landmark crosses the allies' row
            // (z ~ -3) between two heroes, or just beside the row, whichever is nearest the straight line.
            float k = 2.6f / Mathf.Max(4f, pc.Z + 5.6f), straight = pc.X * k;
            var xs = _views.Where(kv => kv.Key.Ally && kv.Value != null).Select(kv => kv.Value.transform.position.x).OrderBy(x => x).ToList();
            var gaps = new List<float>();
            if (xs.Count > 0) { gaps.Add(xs[0] - 1.2f); gaps.Add(xs[xs.Count - 1] + 1.2f); for (int i = 1; i < xs.Count; i++) gaps.Add((xs[i - 1] + xs[i]) * 0.5f); }
            float cross = gaps.Count > 0 ? gaps.OrderBy(g => Mathf.Abs(g - straight)).First() : straight;
            float ex = (cross - pc.X * k) / (1 - k);
            var eye0 = new Vector3(ex, 1.05f, -5.6f); var look0 = new Vector3(pc.X, top * 0.72f, pc.Z);
            var eye1 = new Vector3(3.2f, 2.7f, -8f); var look1 = new Vector3(pc.X * 0.6f, top * 0.45f, pc.Z * 0.7f);
            arena.ClearSightForReveal(new[] { eye0, Vector3.Lerp(eye0, eye1, 0.5f), eye1 }, L, top);
            for (_revealT = 0; _revealT < RevealLen; _revealT += Time.unscaledDeltaTime)
            {
                if (_revealT > 0.2f && Input.GetMouseButtonDown(0)) break;
                float u = Ease(_revealT / 1.8f);
                arena.Override(Vector3.Lerp(eye0, eye1, u), Vector3.Lerp(look0, look1, u), Mathf.Lerp(54f, 52f, u),
                               _revealT < 1.8f ? 1f : 1f - Ease((_revealT - 1.8f) / (RevealLen - 1.8f)));
                yield return null;
            }
            arena.ClearOverride(); _cinema = _skippable = false; _revealTitle = null; Time.timeScale = _paused ? 0 : keepSpeed;
            arena.FrameBattle(CamKey, false); // restores what the reveal hid and hides the battle view's own occluders
        }

        // The lair's name in the gold serif heading style, the chapter under it, fading in and out.
        void DrawRevealTitle()
        {
            float t = _revealT, a = Mathf.Clamp01((t - 0.25f) / 0.45f) * Mathf.Clamp01((RevealLen - 0.15f - t) / 0.5f);
            if (a <= 0) return;
            float y = Ui.Safe.y + Ui.Safe.height * 0.18f, w = Ui.W;
            var gold = Palette.Gold; gold.a = a; var shade = new Color(0, 0, 0, 0.6f * a); var sub = new Color(0.92f, 0.88f, 0.8f, a);
            Ui.Text(new Rect(2, y + 2, w, 44), _revealTitle, 30, shade, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            Ui.Text(new Rect(0, y, w, 44), _revealTitle, 30, gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            Ui.Text(new Rect(1, y + 45, w, 24), _revealSub, 15, shade, FontStyle.Normal, TextAnchor.MiddleCenter, false, true);
            Ui.Text(new Rect(0, y + 44, w, 24), _revealSub, 15, sub, FontStyle.Normal, TextAnchor.MiddleCenter, false, true);
        }

        // ---------- the Titan's own moves (owner request 2026-11-28: the remodelled body) ----------
        // Clips and event times come from titan.json "moves" (TitanSpec; made by blenderforge/titan_moves.py), so the
        // impacts land when the fists really meet the ground; without them the shared clips and the old timings play.
        // Damage numbers, hit reactions and the screen shake fire at the impact (impact()), not after the cinematic.
        static TitanSpec TS => TitanSpec.Current;
        static string TClip(HeroModel m, string move, params string[] fallbacks)
        {
            var own = TS.ClipOf(move, null);
            if (own != null && m.Has(own)) return own;
            foreach (var f in fallbacks) if (m.Has(f)) return f;
            return fallbacks.Length > 0 ? fallbacks[fallbacks.Length - 1] : own;
        }
        // Shake, dust and a thud when a walking fist plants (the walk clip's plant events), every crossing caught.
        float _plantPrev = -1;
        void WalkPlants(CharacterView v, HeroModel m, string clip, Unit u, float shake, float vol)
        {
            if (!m.Has(clip)) return;
            float L = m.Length(clip), tt = m.StateTime(clip); if (tt < 0 || L <= 0) return;
            float now = tt / L, prev = _plantPrev; _plantPrev = now;
            if (prev < 0 || now < prev - 1.5f) return;
            foreach (var e in new[] { "plant_l", "plant_r" })
            {
                float p = (float)TS.Ev("walk", e, e == "plant_l" ? 0 : 0.5);
                // crossed p (or p + k) between prev and now, wrapping loops
                if (Mathf.Floor(now - p) > Mathf.Floor(prev - p))
                {
                    var at = v.transform.position + v.transform.forward * 1.4f + v.transform.right * (e == "plant_l" ? -1.6f : 1.6f);
                    Vfx.I.Shake(shake); SkillFx.I?.Dust(at, 1.6f, u.El); Sound.I?.PlayAt("titanStep", at, vol, 1, 0.04f);
                }
            }
        }

        // The Titan's fight-ending blow (the Earthsplitter): slow motion, a low camera looking up as it cocks one long
        // arm far back, seams and ridges flaring; then the sweep rakes the ground in front of the team and splits it.
        IEnumerator Earthsplitter(Unit u, CharacterView v, System.Action impact)
        {
            var m = v.Hero; var arena = App.Arena; float speed = Time.timeScale; _cinema = true; var body = TitanBody.Of(v);
            var T = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre);
            var toTeam = team - T; toTeam.y = 0; float dist = toTeam.magnitude; toTeam /= Mathf.Max(0.01f, dist);
            float H = TitanHeight(v);
            var eye = T + toTeam * (dist * 0.62f) + Vector3.Cross(Vector3.up, toTeam) * 1.8f + Vector3.up * 0.55f;
            string clip = TClip(m, "sweep", "Sword_Attack", "Punch_Cross");
            bool own = clip == TS.ClipOf("sweep", null);
            float top = own ? (float)TS.Ev("sweep", "top", 0.36) : 0.3f, contact = own ? (float)TS.Ev("sweep", "impact", 0.48) : 0.42f;
            Time.timeScale = 0.32f; body?.Charge(1, 0.45f);
            m.PlayFrom(clip, 0f, own ? 1f : 0.7f);
            for (float t = 0; t < 1.2f; t += Time.unscaledDeltaTime)
            {
                arena.Override(eye, T + Vector3.up * H * 0.8f, 44f, Ease(t / 0.25f));
                if (m.CurrentNormalized >= top) m.SetFrozen(true); // held, arm cocked back, at the top of the wind-up
                yield return null;
            }
            m.SetFrozen(false); Time.timeScale = Mathf.Max(1f, speed);
            m.PlayFrom(clip, top, own ? 1.6f : 2.2f);
            for (float w = 0; w < 1.5f && m.CurrentNormalized < contact; w += Time.unscaledDeltaTime) { arena.Override(eye, T + Vector3.up * H * 0.8f, 44f, 1f); yield return null; }
            var hit = body != null && body.LowFist is Vector3 fp ? new Vector3(fp.x, 0, fp.z) : T + toTeam * Mathf.Min(dist * 0.62f, 5.5f); hit.y = 0;
            var fx = SkillFx.I;
            if (fx != null) { fx.Cracks(hit, u.El, 5f); fx.TitanSlam(hit, u.El); fx.Shockwave(hit, u.El, 6f); fx.CritFlash(u.El); }
            ArenaEvents.RaiseShock(hit, 1.6f, true); body?.Pulse(); body?.Charge(0, 0.6f);
            Vfx.I.ShakeLong(0.55f, 0.8f); Sound.I?.PlayAt("earthsplit", hit, 1f, 1, 0);
            foreach (var kv in _views) if (kv.Key.Ally && kv.Value != null) kv.Value.Shove(hit, 1.1f);
            impact?.Invoke();
            for (float t = 0; t < 0.9f; t += Time.unscaledDeltaTime) { arena.Override(eye, Vector3.Lerp(T + Vector3.up * H * 0.8f, hit + Vector3.up, Ease(t / 0.2f)), 44f, 1f - Ease((t - 0.4f) / 0.5f)); yield return null; }
            arena.ClearOverride(); _cinema = false; Time.timeScale = _paused ? 0 : speed;
        }

        // Faultline Leap (owner direction 2026-11-26, retimed to its own clips 2026-11-28). The camera never moves; only
        // the lens and the aim: it crouches on its knuckles while its seams flare; it springs off when the clip's takeoff
        // comes (slow motion, the lens zooming out to follow it up); at the peak normal speed returns and it falls, the
        // landing clip started so its impact frame is the touchdown, when the quake and the damage land on the team.
        IEnumerator FaultlineLeap(Unit u, CharacterView v, System.Action impact)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true; var body = TitanBody.Of(v);
            var T0 = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre); team.y = T0.y;
            float H = TitanHeight(v); var eye = arena.PosePosition; float fov = arena.Cam.fieldOfView;
            float wide = Mathf.Min(fov + 34f, 78f), speed = Time.timeScale;
            const float apex = 12f;
            Vector3 Arc(float k) => Vector3.Lerp(T0, team, k) + Vector3.up * (4f * apex * k * (1f - k));
            arena.BreathTarget = 0;
            string leap = TClip(m, "leap", "Jump"), land = TClip(m, "land", "Jump_Land");
            bool own = leap == TS.ClipOf("leap", null);
            float takeoff = own ? (float)TS.Ev("leap", "takeoff", 0.6) : 0f;
            body?.Charge(1, 0.3f);
            if (own) m.PlayFrom(leap, 0f, 1f); else if (m.Has("Crouch_Idle_Loop")) m.Play("Crouch_Idle_Loop", true, 0.15f, 1f);
            // the crouch: until the clip's takeoff (own clip) or 0.35 s (the shared crouch)
            for (float t = 0; t < 1.6f && (own ? m.CurrentNormalized < takeoff : t < 0.35f); t += Time.deltaTime) { arena.Override(eye, T0 + Vector3.up * H * 0.55f, fov, Ease(t / 0.2f)); yield return null; }
            // Rise, in slow motion: the lens zooms out and the aim follows the Titan up to the top of its leap.
            if (!_paused) Time.timeScale = speed * 0.4f;
            if (!own) m.Play("Jump", false, 0.08f, 1.4f, true);
            SkillFx.I?.Dust(T0, H * 0.5f, u.El); Vfx.I.Shake(0.2f); Sound.I?.Play("meteor", 1f, 1, 0);
            const float rise = 0.6f;
            for (float t = 0; t < rise; t += Time.deltaTime)
            {
                float k = 0.5f * (1f - (1f - t / rise) * (1f - t / rise));   // eases into the peak
                var p = Arc(k); v.transform.position = p;
                arena.Override(eye, p + Vector3.up * H * 0.5f, Mathf.Lerp(fov, wide, Ease(t / rise)), 1f);
                yield return null;
            }
            // Peak: normal speed again; the lens zooms back in as it falls onto the team.
            if (!_paused) Time.timeScale = speed;
            const float fall = 0.5f; bool landing = false;
            // the landing clip starts this long before touchdown, so its impact frame is the touchdown
            float lead = m.Has(land) ? (own ? (float)TS.Ev("land", "impact", 0.1) * m.Length(land) / 1.0f : 0.2f) : 0f;
            for (float t = 0; t < fall; t += Time.deltaTime)
            {
                float f = t / fall, k = 0.5f + 0.5f * f * f;                  // gravity: faster and faster
                var p = Arc(k); v.transform.position = p;
                if (!landing && m.Has(land) && t >= fall - lead) { landing = true; if (own) m.PlayFrom(land, 0f, 1f); else m.Play(land, false, 0.05f, 1.2f, true); }
                arena.Override(eye, Vector3.Lerp(p + Vector3.up * H * 0.5f, team + Vector3.up * H * 0.45f, Ease(f)), Mathf.Lerp(wide, fov, Ease(f)), 1f);
                yield return null;
            }
            v.transform.position = team;
            var fx = SkillFx.I;
            if (fx != null) { fx.TitanSlam(team, u.El); fx.Cracks(team, u.El, 4.5f); fx.Shockwave(team, u.El, 6f); }
            ArenaEvents.RaiseShock(team, 1.5f, true); body?.Pulse(); body?.Charge(0, 0.7f);
            Vfx.I.ShakeLong(0.6f, 0.8f); Sound.I?.PlayAt("titanSlam", team, 1f, 1, 0);
            foreach (var kv in _views) if (kv.Key.Ally && kv.Value != null) kv.Value.Shove(team, 1.4f);
            impact?.Invoke();
            var look = team + Vector3.up * H * 0.45f;
            // the quake: three aftershock rings roll out while the camera rocks on a tilt
            for (int i = 0; i < 3; i++) { int n = i; StartCoroutine(Later(0.25f * (n + 1), () => { SkillFx.I?.Shockwave(team, u.El, 5f + n * 2f); Vfx.I.Shake(0.18f); })); }
            for (float t = 0; t < 1.0f; t += Time.deltaTime) { arena.Override(eye, look, fov, 1f, 6f * Mathf.Sin(t * 18f) * (1f - t)); yield return null; }
            for (float t = 0; t < 0.5f; t += Time.deltaTime) { arena.Override(eye, look, fov, 1f - Ease(t / 0.5f)); yield return null; }
            arena.ClearOverride(); _cinema = false;
        }

        // Bedrock Hammer (owner-approved staging 2026-11-26, its own clips 2026-11-28): the Titan knuckle-walks up to its
        // target, a side-on camera tracking it, each fist plant shaking the ground; then a low camera over the victim's
        // shoulder looks up as both fists rise over its horns (seams, core and ridges flaring; slow motion from the top of
        // the wind-up) and the slam lands when the fists meet the ground, cracks and the damage under the target.
        IEnumerator BedrockHammer(Unit u, CharacterView v, CharacterView target, System.Action impact)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true; float speed = Time.timeScale; var body = TitanBody.Of(v);
            var T0 = v.transform.position; var tp = target != null ? target.transform.position : Arena.ToUnity(BattleLayout.TeamCentre);
            float H = TitanHeight(v); var eye0 = arena.PosePosition; float fov0 = arena.Cam.fieldOfView;
            var to = T0 - tp; to.y = 0; var dir = to.normalized; var dest = tp + dir * (float)TS.Reach; // its fists land on the target
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            arena.BreathTarget = 0;
            // 1. The knuckle-walk in, side-on.
            string walk = TClip(m, "walk", "Walk_Loop"); bool ownWalk = walk == TS.ClipOf("walk", null);
            if (m.Has(walk)) m.Play(walk, true, 0.15f, 0.8f);
            _plantPrev = -1;
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                float k = Ease(t / 1.5f); v.transform.position = Vector3.Lerp(T0, dest, k);
                if (ownWalk) WalkPlants(v, m, walk, u, 0.12f, 0.9f);
                else if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) { Vfx.I.Shake(0.12f); SkillFx.I?.Dust(v.transform.position, 1.6f, u.El); Sound.I?.PlayAt("titanStep", v.transform.position, 0.9f, 1, 0.04f); }
                var mid = Vector3.Lerp(v.transform.position, tp, 0.5f);
                float e = Ease(t / 0.4f);
                arena.Override(Vector3.Lerp(eye0, mid + side * 12f - dir * 2f + Vector3.up * 3.0f, e), Vector3.Lerp(eye0 + arena.PoseRotation * Vector3.forward * 10f, mid + Vector3.up * 3.0f, e), Mathf.Lerp(fov0, 50f, e), Ease(t / 0.3f));
                yield return null;
            }
            // 2. The fists rise in slow motion; over the victim's shoulder, straight up the line at the Titan (no other
            //    hero can stand in that line).
            var teamC = Arena.ToUnity(BattleLayout.TeamCentre); var outward = Vector3.Dot(side, tp - teamC) >= 0 ? side : -side;
            var eye = tp - dir * 1.4f + outward * 0.7f + Vector3.up * 1.5f;
            var look = v.transform.position + Vector3.up * H * 0.6f;   // the whole Titan in frame
            string clip = TClip(m, "fists", "Ult_Overhead", "Sword_Attack", "Punch_Cross"); bool own = clip == TS.ClipOf("fists", null);
            float top = own ? (float)TS.Ev("fists", "top", 0.375) : 0.28f, contact = own ? (float)TS.Ev("fists", "impact", 0.56) : 0.55f;
            m.PlayFrom(clip, 0f, 0.9f); body?.Charge(1, 0.5f);
            for (float t = 0; t < 4.2f && m.CurrentNormalized < contact; t += Time.unscaledDeltaTime)
            {
                if (!_paused) Time.timeScale = m.CurrentNormalized > top && m.CurrentNormalized < contact - 0.03f ? speed * 0.3f : speed;   // a long slow-motion hold at the top
                arena.Override(eye, look, 72f, 1f, 5f); yield return null;
            }
            if (!_paused) Time.timeScale = speed;
            // 3. The slam, where the fists are.
            var at = body != null && body.Fists is Vector3 fp ? new Vector3(fp.x, tp.y, fp.z) : tp;
            var fx = SkillFx.I;
            if (fx != null) { fx.Cracks(at, u.El, 2.6f); fx.Shockwave(at, u.El, 4f); fx.TitanSlam(at, u.El); }
            ArenaEvents.RaiseShock(at, 1.3f, true); body?.Pulse(); body?.Charge(0, 0.6f);
            Vfx.I.ShakeLong(0.45f, 0.6f); Sound.I?.PlayAt("titanSlam", at, 0.9f, 1, 0);
            if (target != null) target.Shove(tp + dir, 0.8f);
            impact?.Invoke();
            // aftermath: cracks spread and dust rolls while the camera eases back and levels out
            for (float t = 0; t < 1.7f; t += Time.deltaTime)
            {
                float e = Ease(t / 1.7f);
                arena.Override(eye - dir * 1.5f * e + Vector3.up * 0.4f * e, look, 72f, t < 1.2f ? 1f : 1f - Ease((t - 1.2f) / 0.5f), 5f * (1f - e)); yield return null;
            }
            arena.ClearOverride(); _cinema = false;
        }
        IEnumerator TitanWalkHome(Unit u, CharacterView v)
        {
            var m = v.Hero; var from = v.transform.position; var to = v.Home;
            string walk = TClip(m, "walk", "Walk_Loop"); bool own = walk == TS.ClipOf("walk", null);
            if (m.Has(walk)) m.Play(walk, true, 0.15f, 0.9f);
            _plantPrev = -1;
            for (float t = 0; t < 0.9f; t += Time.deltaTime)
            {
                v.transform.position = Vector3.Lerp(from, to, Ease(t / 0.9f));
                if (own) WalkPlants(v, m, walk, u, 0.06f, 0.7f);
                else if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) Sound.I?.PlayAt("titanStep", v.transform.position, 0.7f, 1, 0.04f);
                yield return null;
            }
            v.transform.position = to; m.Idle(0.25f);
        }

        // Heartfire Torrent (owner-approved staging, its own clip 2026-11-28): the Titan rears and its open chest core
        // swells and flares, seen from behind the heroes; at the clip's release it thrusts its chest forward and the
        // torrent pours FROM THE CHEST CORE (the socket follows the body as it turns), sweeping across the team over its
        // shoulder; then a side-on wide shot as it sweeps back. Each hero's damage shows as the fire reaches them.
        IEnumerator HeartfireTorrent(Unit u, CharacterView v, System.Action<Unit> reached)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true; var body = TitanBody.Of(v);
            var T = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre); team.y = T.y;
            float H = TitanHeight(v); var fx = SkillFx.I;
            var back = T - team; back.y = 0; back.Normalize(); var right = Vector3.Cross(Vector3.up, -back).normalized;
            // Over the shoulder, far enough back for horns, shoulders and the chest glow (offsets scale with its size).
            float sz = H / 4.8f;
            var ots = T + right * 3.4f * sz + back * 5.6f * sz + Vector3.up * H * 1.2f;
            arena.BreathTarget = 0;
            string clip = TClip(m, "breath", "Ult_Channel", "Interact"); bool own = clip == TS.ClipOf("breath", null);
            float rel = own ? (float)TS.Ev("breath", "release", 0.33) : 0.4f, sw2 = own ? (float)TS.Ev("breath", "sweep2", 0.63) : 0.7f, end = own ? (float)TS.Ev("breath", "end", 0.9) : 1f;
            float L = Mathf.Max(0.1f, m.Length(clip)), spd = own ? 1f : 0.55f;
            m.PlayFrom(clip, 0f, spd); body?.Charge(1, 0.9f);
            Sound.I?.Play("titanRoar", 0.8f, 1, 0);
            Vector3 Chest() => body != null ? body.ChestPoint(v) : v.transform.position + Vector3.up * (float)(TS.Chest * H);
            // 1. Gather: from behind the heroes, low, looking up at the core swelling in its chest (its front faces the camera).
            var front = team - back * 3.2f + Vector3.up * 1.2f;
            float gather = own ? rel * L / spd : 1.8f;
            for (float t = 0; t < gather; t += Time.deltaTime)
            {
                arena.Override(front + back * 1.4f * Ease(t / gather), Vector3.Lerp(Chest(), T + Vector3.up * (float)BattleLayout.TitanFace, 0.35f), 50f, Ease(t / 0.3f));
                if (fx != null && Mathf.Repeat(t, 0.2f) < Time.deltaTime) fx.ChargeAt(Chest(), u.El, 0.2f, 2.2f);
                yield return null;
            }
            var allies = _views.Where(kv => kv.Key.Ally && kv.Key.Alive && kv.Value != null).ToList();
            var pts = allies.Select(kv => kv.Value.transform.position).ToList();
            if (pts.Count == 0) pts.Add(team);
            float camSide(Vector3 p) => Vector3.Dot(p - team, right);
            var a = pts.OrderBy(camSide).First() - right * 0.6f; var b = pts.OrderBy(camSide).Last() + right * 0.6f;
            var ots2 = ots - back * 0.6f;
            float d1 = own ? (sw2 - rel) * L / spd : 1.8f, d2 = own ? (end - sw2) * L / spd : 1.6f;
            // 2. The torrent sweeps across the team, the Titan still in frame; each hero is reached as the aim passes it.
            if (fx != null) StartCoroutine(fx.Breath(Chest, a + Vector3.up * 0.6f, b + Vector3.up * 0.6f, u.El, d1));
            ArenaEvents.RaiseShock(team, 0.9f, false); body?.Pulse(0.8f);
            Sound.I?.Play("titanBreath", 1f, 1, 0);
            var hitOnce = new HashSet<Unit>();
            for (float t = 0; t < d1; t += Time.deltaTime)
            {
                var aim = Vector3.Lerp(a, b, t / d1);
                arena.Override(ots2, aim * 0.5f + team * 0.5f + Vector3.up * 0.8f, 52f, 1f);
                foreach (var kv in allies)
                    if (Mathf.Abs(camSide(kv.Value.transform.position) - camSide(aim)) < 0.8f && hitOnce.Add(kv.Key)) { reached?.Invoke(kv.Key); fx?.FireFlare(kv.Value.transform.position, u.El); }
                if (fx != null && Mathf.Repeat(t, 0.3f) < Time.deltaTime) foreach (var p in pts) if (Mathf.Abs(camSide(p) - camSide(aim)) < 1.6f) fx.FireFlare(p, u.El);
                yield return null;
            }
            foreach (var kv in allies) if (hitOnce.Add(kv.Key)) reached?.Invoke(kv.Key);
            // 3. Side-on wide shot: the Titan, the stream and the team in one frame; the second sweep back.
            if (fx != null) StartCoroutine(fx.Breath(Chest, b + Vector3.up * 0.6f, a + Vector3.up * 0.6f, u.El, d2));
            var mid = Vector3.Lerp(T, team, 0.5f); float wideLen = d2 + 0.8f;
            for (float t = 0; t < wideLen; t += Time.deltaTime)
            {
                arena.Override(mid + right * Mathf.Lerp(13f, 11f, Ease(t / wideLen)) + Vector3.up * 1.6f, mid + Vector3.up * 2.4f, 56f, t < wideLen - 0.5f ? 1f : 1f - Ease((t - (wideLen - 0.5f)) / 0.5f), -4f);
                if (t > d2 * 0.85f) body?.Charge(0, 0.6f);
                if (fx != null && t < d2 && Mathf.Repeat(t, 0.35f) < Time.deltaTime) foreach (var p in pts) fx.FireFlare(p, u.El);
                yield return null;
            }
            body?.Charge(0, 0.5f);
            arena.ClearOverride(); _cinema = false;
        }

        // After the leap: a heavy hop back to its place (the leap clip from its takeoff, then the landing clip).
        IEnumerator TitanHopHome(Unit u, CharacterView v)
        {
            var m = v.Hero; var from = v.transform.position; var to = v.Home;
            string leap = TClip(m, "leap", "Jump"), land = TClip(m, "land", "Jump_Land"); bool own = leap == TS.ClipOf("leap", null);
            if (own) m.PlayFrom(leap, (float)TS.Ev("leap", "takeoff", 0.6), 1.2f); else if (m.Has("Jump")) m.Play("Jump", false, 0.1f, 1.2f, true);
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { float k = Ease(t / 0.6f); v.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f; yield return null; }
            v.transform.position = to;
            if (m.Has(land)) { if (own) m.PlayFrom(land, (float)TS.Ev("land", "impact", 0.1), 1.3f); else m.Play(land, false, 0.08f, 1.2f); }
            Vfx.I.Shake(0.25f); SkillFx.I?.Dust(to, 2.5f, u.El); Sound.I?.PlayAt("titanStep", to, 1f, 0.9f, 0.03f);
            yield return new WaitForSeconds(own ? 0.9f : 0.3f);
            m.Idle(0.25f);
        }

        // The fight is over and the team still stands: the Titan staggers, drops to its knees on its fists and sags,
        // its glow dying (the final blow, from the team's side). If it wiped the team, it roars instead.
        IEnumerator TitanFinale(bool teamStands)
        {
            var tv = _views.FirstOrDefault(kv => kv.Key.Titan).Value; if (tv == null || tv.Hero == null) yield break;
            var m = tv.Hero; var body = TitanBody.Of(tv); var u = _views.First(kv => kv.Key.Titan).Key;
            if (!teamStands) { body?.Charge(0.8f, 0.3f); m.Play(TClip(m, "roar", "Yes", "Interact"), false, 0.2f); Sound.I?.Play("titanRoar", 0.8f, 1, 0); yield return new WaitForSeconds(1.2f); body?.Charge(0, 0.6f); yield break; }
            string clip = TClip(m, "collapse", "Death01"); bool own = clip == TS.ClipOf("collapse", null);
            if (!m.Has(clip)) yield break;
            m.PlayFrom(clip, 0f, 1f);
            float ground = own ? (float)TS.Ev("collapse", "ground", 0.46) : 0.5f;
            for (float w = 0; w < 3f && m.CurrentNormalized < ground; w += Time.unscaledDeltaTime) yield return null;
            var at = tv.transform.position + tv.transform.forward * 2f;
            Vfx.I.Shake(0.3f); SkillFx.I?.Dust(at, 3f, u.El); Sound.I?.PlayAt("titanSlam", at, 0.8f, 0.8f, 0);
            body?.Dim(0.15f, 1.2f);
        }

        // ---------- turn loop ----------
        IEnumerator Loop()
        {
            if (IsTitan) yield return TitanIntro();
            else yield return LairReveal();
            if (_bossMusic) Sound.I?.Music("boss");   // MUSIC: the boss theme takes over from the lair reveal (or at once if it was skipped)
            yield return new WaitForSeconds(0.5f);
            while (!_b.Over)
            {
                var u = _b.NextActor();
                if (u == null) break;
                _actor = u; _banner = "";
                var (events, skip) = _b.BeginTurn(u);
                if (events.Count > 0) { yield return ShowEvents(events, Palette.Hex("#7dff7a")); yield return new WaitForSeconds(0.25f); }
                if (!skip)
                {
                    if (_views.TryGetValue(u, out var rv) && rv != null) rv.Ready();
                    int skill; Unit target;
                    if (u.Ally && !Auto)
                    {
                        _sel = 0; _awaiting = true; _chosen = null;
                        while (_awaiting && !Auto) yield return null;
                        if (_awaiting) { _awaiting = false; (skill, target) = _b.AiChoose(u); } // switched to auto mid-turn
                        else { skill = _sel; target = _chosen; }
                    }
                    else (skill, target) = _b.AiChoose(u);
                    yield return DoAct(u, skill, target);
                }
                else yield return new WaitForSeconds(0.35f);
                _b.EndTurn(u, out bool newWave);
                if (newWave)
                {
                    foreach (var dead in _views.Where(kv => !kv.Key.Ally).Select(kv => kv.Value).ToList()) if (dead) Destroy(dead.gameObject, 1.5f);
                    foreach (var k in _views.Keys.Where(k => !k.Ally).ToList()) _views.Remove(k);
                    _banner = $"Wave {_b.Wave + 1}!";
                    yield return new WaitForSeconds(0.6f);
                    Spawn(_b.Enemies, false);
                    yield return new WaitForSeconds(0.5f);
                }
            }
            _actor = null;
            Finish();
        }

        Unit _chosen;

        IEnumerator DoAct(Unit u, int skillIdx, Unit target)
        {
            var sk = u.Template.Skills[skillIdx];
            _banner = sk.Name; bool leap = false, fists = false, torrent = false;
            // The Titan's moves show their numbers at the impact (impact / reached), not after the cinematic.
            Coroutine shownCo = null; bool shown = false; var early = new List<Coroutine>(); var reachedUnits = new HashSet<Unit>();
            yield return new WaitForSeconds(0.2f);
            var events = _b.Act(u, skillIdx, target);
            var color = Palette.Element(u.El);
            var view = _views.TryGetValue(u, out var v) ? v : null;
            var hit = events.Where(e => e.Type == EvType.Damage && e.Unit != u).Select(e => e.Unit).Distinct().ToList();
            bool melee = sk.Mult > 0 && !u.Titan && !Looks.IsCaster(u.Template.Id) && sk.Target == Target.Enemy;
            // As ui.js: ult = the third skill or an all-enemies skill; support = no damage; single = one target.
            bool ult = skillIdx == 2 || sk.Target == Target.AllEnemies, support = sk.Mult == 0, single = sk.Target == Target.Enemy;
            _lastEl = u.El;
            var fx = SkillFx.I;
            // Each hero sounds like its weapon (melee and bows) or its element (casters); the Titan swings heavy.
            float upan = view != null ? Sound.PanOf(view.Center) : 0;   // heard from where the attacker stands
            if (u.Titan) Sound.I?.Play("titanSwing", 0.9f, 1, 0.04f, upan);
            else if (melee || Impact.FamilyOf(u.Template.Id) == WeaponFamily.Bow) Sound.I?.Swing(Impact.FamilyOf(u.Template.Id), 0.6f, upan);
            else Sound.I?.Cast(u.El, 0.6f, upan);
            if (view != null)
            {
                var tgtView = hit.Count > 0 && _views.ContainsKey(hit[0]) ? _views[hit[0]] : null;
                Vector3 aim = tgtView != null ? tgtView.transform.position : view.transform.position + view.transform.forward * 2;
                // The colossal Titan: melee heroes stop well short of its legs instead of running into them.
                if (melee && hit.Count > 0 && hit[0].Titan) { var away = view.transform.position - aim; away.y = 0; aim += away.normalized * 1.6f; }
                else if (melee && hit.Count > 0 && Creatures.Is(hit[0].Template.Id)) { var away = view.transform.position - aim; away.y = 0; aim += away.normalized * (float)Creatures.MeleeReach(hit[0].Template.Id); }
                view.Face(aim); // turn toward the target only while acting (0.17.1)
                if (!melee && !u.Titan && fx != null)
                {
                    // Casters: a spell circle and gathering energy; ultimates on foes also flood the area and shock out.
                    // The circle matches the caster's floor disc, which hides while it shows (art review M3).
                    float foot = (float)BattleFx.Footprint(view.Model ? view.Model.localScale.x : 1f, Creatures.LineupWidth(u.Template.Id));
                    fx.CastCircle(view.transform.position, u.El, foot); view.HideAura((float)BattleFx.CircleTime); fx.Charge(view.transform, u.El, 0.5f);
                    if (ult && tgtView != null && !support && !single)
                    {
                        var c = tgtView.transform.position; c.x *= 0.3f;
                        fx.Aoe(c, 1.9f, u.El); StartCoroutine(Later(0.45f, () => fx.Shockwave(c, u.El, 2.6f)));
                    }
                }
                // Titan turns: the camera breathes down and tilts up at it during the wind-up; the HD Titan's
                // fight-ending blow is the Earthsplitter and its ultimate the Faultline Leap.
                bool hdTitan = u.Titan && IsHdTitan(view);
                bool finale = hdTitan && (_b.Allies.All(a => !a.Alive) || _b.BossTurns + 1 >= _b.TurnLimit);
                // The Titan's three moves (owner request 2026-11-28, for the remodelled body): Bedrock Hammer (skill 1, the
                // two-fist slam), Faultline Leap (skill 2, the leap and quake), Heartfire Torrent (skill 3, the chest-core
                // breath); any fight-ending blow is the Earthsplitter (the long-arm sweep).
                leap = hdTitan && !finale && skillIdx == 1;
                fists = hdTitan && !finale && skillIdx == 0;
                torrent = hdTitan && !finale && skillIdx == 2;
                var showColor = color; bool multi = sk.Hits > 1;
                System.Action impact = () => { if (!shown) { shown = true; shownCo = StartCoroutine(ShowEvents(events, showColor, multi)); } };
                System.Action<Unit> reached = t => { if (reachedUnits.Add(t)) early.Add(StartCoroutine(ShowEvents(events.Where(e => e.Unit == t).ToList(), showColor, multi))); };
                if (u.Titan) { App.Arena.BreathFocus = view.HeadTop ? view.HeadTop.position : view.transform.position + Vector3.up * 5f; App.Arena.BreathTarget = 1; }
                if (finale) yield return Earthsplitter(u, view, impact);
                else if (leap) yield return FaultlineLeap(u, view, impact);
                else if (fists) yield return BedrockHammer(u, view, tgtView, impact);
                else if (torrent) yield return HeartfireTorrent(u, view, reached);
                else yield return view.Attack(aim, melee, support, ult, () => { Vfx.I.Shake(0.35f); fx?.TitanSlam(view.transform.position, u.El); ArenaEvents.RaiseShock(view.transform.position, 1.2f, true); }, skillIdx);
                if (support && fx == null) Vfx.I.Cast(view.transform.position + Vector3.up * 0.1f, color);
                // Ultimates show the hero's signature big, on the caster and on every target or ally reached.
                if (ult && fx != null && !u.Titan)
                {
                    var motif = HeroSignatures.Of(u.Template.Id, u.El);
                    fx.Signature(view.Center, u.El, motif, 1.4f);
                    foreach (var t in events.Where(e => e.Unit != null && e.Unit != u && (e.Type == EvType.Damage || e.Type == EvType.Heal || e.Type == EvType.Status)).Select(e => e.Unit).Distinct())
                        if (_views.TryGetValue(t, out var tv) && tv != null) fx.Signature(tv.Center, u.El, motif, 1.0f);
                }
                if (!melee && hit.Count > 0 && !finale && !leap && !fists && !torrent)
                {
                    var from = view.Center + view.transform.forward * 0.4f;
                    string prop = u.Titan ? "" : Looks.Of(u.Template.Id).Prop;
                    string kind = prop == "bow" ? "arrow" : prop == "mirror" || prop == "staff" ? "bolt" : prop == "orb" || prop == "candle" ? "orb" : "shard";
                    if (kind == "arrow") Sound.I?.PlayAt("arrow", from, 0.7f);
                    var flights = hit.Where(_views.ContainsKey).Select(h => StartCoroutine(
                        fx == null ? Vfx.I.Projectile(from, _views[h].Center, color)
                        : ult && single && !support ? fx.Beam(from, _views[h].Center, u.El) : fx.Projectile(from, _views[h].Center, u.El, kind))).ToList();
                    foreach (var f in flights) yield return f;
                }
                if (melee) foreach (var h in hit) if (_views.ContainsKey(h)) { if (fx != null) fx.Slash(_views[h].Center, u.El, _views[h].transform.position - view.transform.position); else Vfx.I.Slash(_views[h].Center, color); }
                // Hit-stop at the impact: attacker and targets (melee) or targets only (ranged) hold their
                // poses briefly, so the hit has weight; time-based, the same at 30/60/120 fps.
                if (!support && hit.Count > 0 && !u.Titan)
                {
                    bool crit = events.Any(e => e.Type == EvType.Damage && e.Crit), kill = events.Any(e => e.Type == EvType.Death);
                    float stop = (float)Impact.HitStop(Impact.FamilyOf(u.Template.Id), crit, kill, ult, G.S.settings.speed);
                    if (melee) StartCoroutine(view.FreezeFor(stop));
                    foreach (var h in hit) if (_views.TryGetValue(h, out var hv) && hv != null) StartCoroutine(hv.FreezeFor(stop));
                    yield return new WaitForSeconds(stop);
                }
            }
            _ultBy = ult && !support ? u : null;
            // The scenery answers ultimates and boss blows (ArenaReactor on boss stages; presentation only).
            if (!u.Titan && !support && hit.Count > 0 && (ult || u.Boss))
            {
                var at = _views.TryGetValue(hit[0], out var hv0) && hv0 != null ? hv0.transform.position : view != null ? view.transform.position : Vector3.zero;
                ArenaEvents.RaiseShock(at, ult ? 1f : 0.6f, u.Boss && ult);
            }
            if (shown) yield return shownCo;
            else if (reachedUnits.Count > 0) { foreach (var c in early) yield return c; yield return ShowEvents(events.Where(e => e.Unit == null || !reachedUnits.Contains(e.Unit)).ToList(), color, sk.Hits > 1); }
            else yield return ShowEvents(events, color, sk.Hits > 1);
            _ultBy = null;
            if (leap && view != null) yield return TitanHopHome(u, view);
            if (fists && view != null) yield return TitanWalkHome(u, view);
            if (u.Titan) App.Arena.BreathTarget = 0;
            if (view != null) { yield return view.Recover(); if (view != null) view.FaceHome(); } // the view can be destroyed during Recover (scene change)
            yield return new WaitForSeconds(0.25f);
        }

        long _titanShown;   // damage on the Titan shown so far this fight (its health bar and crystal ridges follow it)
        IEnumerator ShowEvents(List<BattleEvent> events, Color color, bool multiHit = false)
        {
            // Multi-hit skills: each hit on a target lands on its own beat with a short stop between.
            var hitsLeft = new Dictionary<Unit, int>();
            if (multiHit) foreach (var e in events) if (e.Type == EvType.Damage && !e.Dot && e.Unit != null && e.Amount > 0) hitsLeft[e.Unit] = (hitsLeft.TryGetValue(e.Unit, out var n) ? n : 0) + 1;
            foreach (var e in events)
            {
                if (e.Type == EvType.Damage && e.Unit != null && e.Amount > 0) _taken[e.Unit] = (_taken.TryGetValue(e.Unit, out var tk) ? tk : 0) + e.Amount;
                // The Titan's health (the top damage tier still to deal) falls as each number shows: its crystal ridges
                // break off at the stages (TitanSpec.RidgeStages), and the bar over it follows.
                if (e.Type == EvType.Damage && e.Unit != null && e.Unit.Titan && e.Amount > 0)
                {
                    _titanShown += e.Amount;
                    if (_views.TryGetValue(e.Unit, out var tvw)) TitanBody.Of(tvw)?.SetHealth(TitanSpec.Health(_titanShown));
                }
                if (e.Type == EvType.Heal && e.Source != null && e.Amount > 0) _healed[e.Source] = (_healed.TryGetValue(e.Source, out var hl) ? hl : 0) + e.Amount;
                if (e.Unit == null || !_views.TryGetValue(e.Unit, out var v) || v == null) continue;
                var c = v.Center;
                switch (e.Type)
                {
                    case EvType.Damage:
                        if (e.Dot) { SkillFx.I?.DebuffTick(v.transform.position, "poison"); Vfx.I.Number(NumberAnchor(e.Unit, v), Ui.Fmt(e.Amount), new Color(0.5f, 1f, 0.45f)); break; }
                        if (e.Amount <= 0) { v.Hit(false, true); break; } // fully absorbed by a shield: the "Shield" number says it (no "CRIT 0"); only now may it dodge
                        // weapon x armour: the attacker's weapon landing and the defender's armour answering (plate clangs,
                        // mail jingles, leather slaps, cloth thuds), heard from where the blow lands; the Titan is plate.
                        float hpan = Sound.PanOf(c);
                        if (e.Source != null && !e.Source.Titan) Sound.I?.Hit(Impact.FamilyOf(e.Source.Template.Id), e.Crit, Sound.ArmourOf(e.Unit), hpan, e.Unit.Titan ? 0.8f : 1f);
                        else if (e.Source != null) Sound.I?.Hit(WeaponFamily.Heavy, e.Crit, Sound.ArmourOf(e.Unit), hpan, 0.75f);
                        else Sound.I?.Play(e.Crit ? "crit" : "hit", e.Crit ? 1 : 0.8f, 1, 0.06f, hpan);
                        if (SkillFx.I != null) SkillFx.I.Impact(c, e.Source != null ? e.Source.El : _lastEl, e.Crit); else Vfx.I.Burst(c, color, e.Crit);
                        // The attacker's signature motif on each hit (phase E 53-55).
                        if (SkillFx.I != null && e.Source != null && !e.Source.Titan) SkillFx.I.Signature(c, e.Source.El, HeroSignatures.Of(e.Source.Template.Id, e.Source.El), e.Crit ? 0.9f : 0.6f);
                        // Heavier reaction on crits and hits over 14% of max HP. HP was lost, so no dodge (PLAN step 99).
                        v.Hit(e.Crit || e.Amount / (float)Mathf.Max(1, e.Unit.MaxHp) > 0.14f);
                        if (e.Crit) SkillFx.I?.CritFlash(e.Source != null ? e.Source.El : _lastEl);
                        ArenaEvents.RaiseHit(v.transform.position, e.Amount / (float)Mathf.Max(1, e.Unit.MaxHp), e.Crit, _ultBy != null || (e.Source != null && e.Source.Boss));
                        // Above the head tag, stacked when several land at once; WEAK!/RESIST on its own line in its own
                        // colour (words as well as colour, UX review #4; art review M3).
                        Vfx.I.Number(NumberAnchor(e.Unit, v), (e.Crit ? "CRIT " : "") + Ui.Fmt(e.Amount),
                            e.Crit ? new Color(1f, 0.85f, 0.2f) : e.Elem > 1 ? new Color(1f, 0.55f, 0.3f) : e.Elem < 1 ? new Color(0.7f, 0.7f, 0.8f) : Color.white, e.Crit ? 1.3f : 1f,
                            e.Elem > 1 ? "WEAK!" : e.Elem < 1 ? "RESIST" : null, e.Elem > 1 ? new Color(1f, 0.62f, 0.25f) : new Color(0.78f, 0.82f, 0.95f));
                        if (e.Crit) Vfx.I.Shake();
                        if (multiHit && hitsLeft.TryGetValue(e.Unit, out var left) && left > 1)
                        {
                            hitsLeft[e.Unit] = left - 1;
                            StartCoroutine(v.FreezeFor((float)Impact.MultiHitStop));
                            if (e.Source != null && _views.TryGetValue(e.Source, out var sv) && sv != null) StartCoroutine(sv.FreezeFor((float)Impact.MultiHitStop));
                            yield return new WaitForSeconds((float)Impact.MultiHitGap);
                        }
                        else yield return new WaitForSeconds(0.12f);
                        break;
                    case EvType.Heal: Sound.I?.PlayAt("heal", c, 0.5f); if (SkillFx.I != null) SkillFx.I.Heal(v.transform.position); else Vfx.I.Heal(v.transform.position); Vfx.I.Number(NumberAnchor(e.Unit, v), "+" + Ui.Fmt(e.Amount), new Color(0.45f, 1f, 0.5f)); break;
                    case EvType.Absorb: Vfx.I.Number(NumberAnchor(e.Unit, v), "Shield " + Ui.Fmt(e.Amount), new Color(0.6f, 0.85f, 1f), 0.7f); break;
                    case EvType.Status:
                        Sound.I?.Status(e.Fx, Sound.PanOf(c));
                        if (SkillFx.I == null) Vfx.I.Buff(v.transform.position, FxDef.IsBuff(e.Fx));
                        else if (e.Fx == FxType.Shield) SkillFx.I.Shield(v.transform.position);
                        else SkillFx.I.Buff(v.transform.position, FxDef.IsBuff(e.Fx));
                        if (e.Fx == FxType.Stun) { if (KnocksDown(e)) v.KnockDown(); else v.Stun(); }
                        else if (e.Fx == FxType.Broken)
                        {
                            // Its own look (not Stun's): recoil and hold, cracked ground in the element that broke it.
                            v.Broken(); Tips.SawBreak = true; // the one-time Weakness Break tip
                            SkillFx.I?.Cracks(v.transform.position, BreakerOf(e.Unit.El), 1.4f);
                            Vfx.I.Shake(0.08f);
                        }
                        else v.StatusPulse(FxDef.IsBuff(e.Fx));
                        Vfx.I.Number(NumberAnchor(e.Unit, v), SkillText.EffectName(e.Fx), FxDef.IsBuff(e.Fx) ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.55f, 1f), 0.7f); break;
                    case EvType.Text:
                        if (e.Text == "Break!" || e.Text == "Broken!") Vfx.I.Number(NumberAnchor(e.Unit, v), e.Text == "Break!" ? "BREAK!" : "Broken!", BreakColor, e.Text == "Break!" ? 1.35f : 0.9f); // the break banner, in the break bar's orange
                        else Vfx.I.Number(NumberAnchor(e.Unit, v), e.Text, new Color(0.8f, 0.9f, 1f), 0.7f);
                        if (e.Text.StartsWith("Enrage")) Tips.SawEnrage = true; // the one-time Enrage tip
                        if (e.Text == "Revived!") v.Revived();
                        if (e.Text == "Counter!")
                        {
                            // The counter's own hit follows: face whoever it lands on.
                            var hitEv = events.Skip(events.IndexOf(e) + 1).FirstOrDefault(x => x.Type == EvType.Damage && x.Source == e.Unit);
                            if (hitEv?.Unit != null && _views.TryGetValue(hitEv.Unit, out var av) && av != null) v.Counter(av.transform.position);
                        }
                        break;
                    case EvType.Death: Sound.I?.PlayAt(e.Unit.Ally ? "downed" : "kill", c, 0.9f); ArenaEvents.RaiseKill(v.transform.position); v.Die(); if (SkillFx.I != null) SkillFx.I.DeathFade(v.transform.position, e.Unit.El); else Vfx.I.Dissolve(v.transform.position + Vector3.up * 0.8f); break;
                }
            }
            yield return new WaitForSeconds(0.2f);
        }

        static readonly Color BreakColor = new Color(1f, 0.6f, 0.22f); // the weakness break meter (PLAN step 98)
        // The element that beats `el` (the one whose hits break it).
        static Element BreakerOf(Element el) { foreach (var x in Elements) if (ElementMultiplier(x, el) > 1) return x; return el; }

        // Round counter (PLAN step 99): the wave's round, red with "Enrage xN" from round EnrageFrom, when every hit
        // deals +10% per stack and healing and shields lose as much. Not in the Titan fight (no enrage; it has turns).
        static readonly Color EnrageRed = new Color(0.86f, 0.2f, 0.16f);
        void RoundPill(float x, float y, bool detail, bool centre = false)
        {
            if (_b == null || _b.IsTitan || _b.Over) return;
            int st = _b.EnrageStacks;
            string txt = st > 0 ? (detail ? $"Round {_b.Round} · Enrage x{st}  +{st * 10}% dmg  -{System.Math.Min(100, st * 10)}% heal" : $"Round {_b.Round} · Enrage x{st}") : $"Round {_b.Round}";
            float w = st > 0 ? (detail ? 262 : 138) : 70;
            var r = new Rect(centre ? x - w / 2 : x, y, w, 20);
            Ui.Round(r, st > 0 ? new Color(EnrageRed.r, EnrageRed.g, EnrageRed.b, 0.88f) : new Color(0.06f, 0.05f, 0.08f, 0.6f), st > 0 ? (Color?)new Color(1f, 0.55f, 0.45f) : null, 8);
            Ui.Text(r, txt, 11, st > 0 ? Color.white : Palette.Dim, FontStyle.Bold, TextAnchor.MiddleCenter);
        }
        // The break meter: a dark track with an orange fill and a bright leading edge, so a part-filled meter
        // reads at battle size (the first 2 px line was invisible in the in-game check).
        static void BreakBar(Rect r, double brk)
        {
            if (brk <= 0) return;
            GUI.DrawTexture(r, Ui.Solid(new Color(0.08f, 0.05f, 0.03f, 0.85f)));
            float f = Mathf.Clamp01((float)(brk / 100));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * f, r.height), Ui.Solid(f >= 0.66f ? new Color(1f, 0.78f, 0.3f) : BreakColor));
            GUI.DrawTexture(new Rect(r.x + r.width * f - 1, r.y - 1, 2, r.height + 2), Ui.Solid(new Color(1f, 0.95f, 0.75f)));
        }
        Element _lastEl;
        // A stun from a heavy-family weapon, the Titan or an ultimate knocks the target down (MECHANICS.md 3.1).
        Unit _ultBy; // the unit whose ultimate's events are being shown
        bool KnocksDown(BattleEvent e)
        {
            if (e.Source == null || e.Source.Ally == e.Unit.Ally) return false;
            return e.Source.Titan || e.Source == _ultBy || Impact.FamilyOf(e.Source.Template.Id) == WeaponFamily.Heavy;
        }
        IEnumerator Later(float s, System.Action a) { yield return new WaitForSeconds(s); a(); }

        // Winners celebrate (three3d.mjs victory()), staggered so they don't move in lockstep.
        IEnumerator Celebrate(bool allies)
        {
            int i = 0;
            foreach (var kv in _views.ToList())
            {
                if (kv.Key.Ally != allies || !kv.Key.Alive || kv.Value == null) continue;
                yield return new WaitForSeconds(i++ == 0 ? 0.1f : 0.12f);
                if (kv.Value != null) { if (allies && _b.Result != BattleResult.Victory) kv.Value.Lose(); else kv.Value.Win(); }
            }
        }

        // ---------- results ----------
        void Finish()
        {
            if (_finished) return;
            _finished = true;
            Time.timeScale = 1; _paused = false;
            if (_b.Result == BattleResult.None)
            {
                // Nobody left to fight or to act (an empty side from bad data: code review L1). Not a defeat:
                // a neutral note, nothing granted.
                Sound.I?.StopMusic();
                G.S.settings.autoReplay = false; App.Save(); _auto = null;
                App.Modal("Battle over", 210, r =>
                {
                    Ui.Text(new Rect(r.x, r.y, r.width, r.height - 56), "This battle had nobody left to fight, so it ended without a result. Nothing was won or lost.", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width, 44), "Back")) End(BackScreen);
                });
                return;
            }
            if (_b.Result == BattleResult.Victory) StartCoroutine(Celebrate(true));
            else if (_spec.Mode == BattleMode.Arena) StartCoroutine(Celebrate(true)); // PvP only (owner): survivors kneel and slump forward; fallen heroes keep their death pose
            Sound.I?.StopMusic();
            if (_spec.Mode == BattleMode.Titan) { Sound.I?.Play("bigKill"); Sound.I?.Sting("victory"); StartCoroutine(Later(0.6f, () => Sound.I?.Play("coin"))); }
            else if (_b.Result == BattleResult.Victory) { Sound.I?.Sting("victory"); StartCoroutine(Later(0.45f, () => Sound.I?.Play("coin"))); }
            else Sound.I?.Play("downed", 1, 0.8f, 0);
            switch (_spec.Mode)
            {
                case BattleMode.Titan: StartCoroutine(TitanFinale(_b.Allies.Any(a => a.Alive))); FinishTitan(); return;
                case BattleMode.Dungeon: FinishDungeon(); return;
                case BattleMode.Arena: FinishArena(); return;
                case BattleMode.Hard: FinishHard(); return;
            }
            if (_b.Result == BattleResult.Victory)
            {
                int stage = _spec.Stage;
                var res = G.CompleteStage(stage); App.Save();
                App.Online.PushProfile();
                if (ClimbResult(true, res.Reward, res.Gear != null ? 1 : 0, res.LevelUps)) return; // Auto Climb: the summary, or a countdown on the results
                bool canNext = stage + 1 < TotalStages && G.S.cleared > stage;
                bool replay = G.S.settings.autoReplay;
                // Replay run totals (ui.js autoRecord): a summary when the run ends.
                if (replay || _auto != null)
                {
                    _auto ??= new AutoRun();
                    _auto.Record(true, res.Reward, res.Gear != null, res.LevelUps.Select(l => $"{Hero(l.hero.id).ShortName} Lv {l.hero.level}"));
                    if (!replay) { ShowAutoSummary("switched off", stage); return; }
                }
                // One results screen for every mode and both orientations (UI/UX review #6).
                var spec = new ResultsSpec { Win = true, Mvp = true, Where = $"Stage {Game.StageLabel(stage)}", Reward = res.Reward, First = res.FirstClear, Ups = UpsOf(res.LevelUps) };
                if (res.Gear != null) spec.Gear.Add(res.Gear);
                if (replay && _auto != null)
                {
                    // Replay running: the run so far and one way out; the stage starts again after the pause.
                    spec.Note = $"Next battle in a moment · this run: {_auto.Wins} win{(_auto.Wins == 1 ? "" : "s")}, {Ui.Fmt(_auto.Gold)} gold{(_auto.Gear > 0 ? $", {_auto.Gear} gear" : "")}";
                    spec.Add("Stop Replay", UiRole.Danger, () => { G.S.settings.autoReplay = false; App.Save(); ShowAutoSummary("stopped", stage); });
                }
                else
                {
                    if (canNext) spec.Add(Words.BackTo("Map"), UiRole.Secondary, () => End(ScreenId.Campaign));
                    spec.Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Campaign); App.EditTeam(stage); });
                    spec.Add(Words.Again, UiRole.Secondary, () => { App.CloseModal(); Begin(stage); });
                    if (canNext) spec.Add(Words.Next + " \u25B6", UiRole.Primary, () => { App.CloseModal(); Begin(Mathf.Min(G.S.cleared, TotalStages - 1)); });
                    else spec.Add(Words.BackTo("Map"), UiRole.Primary, () => End(ScreenId.Campaign));
                }
                ShowResults(Words.Victory, spec);
                if (G.S.settings.autoReplay) StartCoroutine(AutoReplay());
            }
            else
            {
                G.S.settings.autoReplay = false; App.Save();
                if (ClimbResult(false, null, 0, null)) return; // a loss ends the climb: its summary
                if (_auto != null) { _auto.Record(false, null, false, null); ShowAutoSummary("ended by a defeat", _spec.Stage); return; }
                int stage = _spec.Stage;
                ShowResults(Words.Defeat, new ResultsSpec { Where = $"Stage {Game.StageLabel(stage)}" }
                    .Add(Words.BackTo("Map"), UiRole.Secondary, () => End(ScreenId.Campaign))
                    .Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Campaign); App.EditTeam(stage); })
                    .Add(Words.Retry, UiRole.Primary, () => { App.CloseModal(); Begin(stage); }));
            }
        }

        AutoRun _auto;

        // Hard campaign result (MODES.hard.finish in extras.js): the campaign's results, Hard's own actions.
        void FinishHard()
        {
            int idx = _spec.Stage;
            string where = $"Hard {Game.StageLabel(idx)}";
            if (_b.Result != BattleResult.Victory)
            {
                if (ClimbResult(false, null, 0, null)) return;
                ShowResults(Words.Defeat, new ResultsSpec { Where = where, LoseText = "Hard stages hit much harder. Level up, upgrade gear and try again." }
                    .Add(Words.BackTo("Map"), UiRole.Secondary, () => End(ScreenId.Campaign))
                    .Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Campaign); App.EditTeam(idx, true); })
                    .Add(Words.Retry, UiRole.Primary, () => { App.CloseModal(); Begin(BattleSpec.ForHard(idx)); }));
                return;
            }
            var res = G.CompleteHard(idx); App.Save();
            App.Online.PushProfile();
            if (ClimbResult(true, res.Reward, res.Gear != null ? 1 : 0, res.LevelUps)) return;
            int next = idx + 1; bool canNext = G.HardOpen(next);
            var spec = new ResultsSpec { Win = true, Mvp = true, Where = where, Reward = res.Reward, First = res.FirstClear, FirstText = "First Hard clear!", Ups = UpsOf(res.LevelUps) };
            if (res.Gear != null) spec.Gear.Add(res.Gear);
            if (canNext) spec.Add(Words.BackTo("Map"), UiRole.Secondary, () => End(ScreenId.Campaign));
            spec.Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Campaign); App.EditTeam(idx, true); })
                .Add(Words.Again, UiRole.Secondary, () => { App.CloseModal(); Begin(BattleSpec.ForHard(idx)); });
            if (canNext) spec.Add(Words.Next + " \u25B6", UiRole.Primary, () => { App.CloseModal(); Begin(BattleSpec.ForHard(next)); });
            else spec.Add(Words.BackTo("Map"), UiRole.Primary, () => End(ScreenId.Campaign));
            ShowResults(Words.Victory, spec);
        }

        static readonly List<Unit> NoUnits = new List<Unit>();
        readonly List<(Unit u, CharacterView v, Rect r)> _tags = new List<(Unit u, CharacterView v, Rect r)>();
        readonly List<Rect> _placed = new List<Rect>();
        static bool OverlapsAny(List<Rect> rs, Rect r) { for (int i = 0; i < rs.Count; i++) if (rs[i].Overlaps(r)) return true; return false; }

        // Turn-order bar under the header: the next 7 actors (Battle.PredictTurns, extras.js predictTurns).
        List<Unit> _turns = new List<Unit>(); int _turnsFrame = -1;
        // Portrait turn order: six chips 36 tall, each with its element icon and an ally shield or enemy skull
        // glyph (not only green against red, UX review #4), and the name below at up to 7 letters.
        void DrawTurnBar(float y)
        {
            if (_b.Over) return;
            if (_turnsFrame != Time.frameCount) { _turnsFrame = Time.frameCount; _turns = _b.PredictTurns(7); } // once per frame, not per GUI event
            var list = _turns;
            GUI.DrawTexture(new Rect(0, y, Ui.W, 42), Ui.Solid(new Color(0.04f, 0.03f, 0.06f, 0.5f)));
            Ui.Text(new Rect(8, y, 40, 42), "Next", 11, Palette.Dim, FontStyle.Bold, TextAnchor.MiddleLeft);
            HudTurns = new Rect(0, y, Ui.W, 42);
            int n = Mathf.Min(6, list.Count);
            float w = (Ui.W - 52 - (n - 1) * 4) / 6f;
            for (int i = 0; i < n; i++)
            {
                var u = list[i];
                var r = new Rect(48 + i * (w + 4), y + 3, w, 36);
                bool now = i == 0 && u == _actor;
                Ui.Panel(r, u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), now ? Palette.Gold : u.Ally ? Palette.Good : Palette.Bad, 8);
                Icons.Draw(new Rect(r.x + 4, r.y + 4, 13, 13), Icons.ForElement(u.El), Palette.Element(u.El)); // the element's shape, not a coloured dot
                Icons.Draw(new Rect(r.xMax - 15, r.y + 4, 11, 11), u.Ally ? Icon.Shield : Icon.Skull, u.Ally ? Palette.Hex("#bfe8c8") : Palette.Hex("#ffc0b0"));
                string nm = u.Titan ? "Titan" : u.Template.ShortName;
                Ui.Text(new Rect(r.x + 1, r.y + 17, r.width - 2, 18), nm.Length > 7 ? nm.Substring(0, 7) : nm, 11, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        // The whole Replay run (battles, wins, losses, gold, XP, shards, gear, level ups), in the Climb Summary's
        // layout (UI/UX review #6: "Auto-replay summary" and "Climb Summary" were two different screens).
        void ShowAutoSummary(string why, int stage)
        {
            var run = _auto; _auto = null;
            if (run == null) return;
            var log = new ClimbLog(ClimbArea.Normal);
            log.Fights = run.Battles; log.Cleared = run.Wins; log.Lost = run.Losses;
            log.Gold = run.Gold; log.Xp = run.Xp; log.Common = run.Common; log.Mystic = run.Mystic; log.Radiant = run.Radiant; log.Gear = run.Gear;
            foreach (var l in run.LevelUps.Distinct()) log.LevelUps.Add(l);
            log.First = log.Last = $"Stage {Game.StageLabel(stage)}";
            log.Finish(why.Contains("defeat") ? ClimbStop.Lost : ClimbStop.ByPlayer, log.Last);
            var spec = BattleSpec.ForStage(stage);
            float h = Ui.Landscape ? Mathf.Min(Ui.H - 24, 390) : 430;
            App.Modal("Replay Summary", h, r => DrawClimbSummary(r, log, null, spec, _arenaSlot, true), App.ModalW);
        }

        void FinishTitan()
        {
            long dmg = _b.TitanDamage;
            // The campaign's results layout (UX review #10): damage on the strip, each hero's share below.
            Reward got = _spec.Practice ? null : G.CompleteTitan(dmg);
            if (!_spec.Practice) App.Save();
            var spec = new ResultsSpec { Win = true, Mvp = true, Where = _spec.Practice ? "Titan practice" : "Titan", Headline = $"{Ui.Fmt(dmg)} damage", Reward = got,
                LoseText = _spec.Practice ? "Practice: no rewards, no try used" : null, Note = _spec.Practice ? null : $"Titan tries left today: {G.S.daily.titanTries}" };
            if (_spec.Practice) spec.Add(Words.Again, UiRole.Secondary, () => { App.CloseModal(); Begin(BattleSpec.TitanPractice()); });
            spec.Add(Words.BackTo("Titan"), UiRole.Primary, () => End(ScreenId.Titan));
            ShowResults(Words.Cleared, spec);
        }

        void FinishDungeon()
        {
            var key = _spec.Dungeon; int floor = _spec.Floor; var D = Trials.Dungeon(key);
            string where = $"{D.Name} · Floor {floor + 1}";
            if (_b.Result != BattleResult.Victory)
            {
                if (ClimbResult(false, null, 0, null)) return;
                ShowResults(Words.Defeat, new ResultsSpec { Where = where, LoseText = "No run used. Strengthen your team and try again." }
                    .Add(Words.BackTo("Dungeons"), UiRole.Secondary, () => End(ScreenId.Dungeon))
                    .Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Dungeon); App.EditDungeonTeam(key, floor); })
                    .Add(Words.Retry, UiRole.Primary, () => { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor)); }));
                return;
            }
            var res = G.CompleteDungeon(key, floor); App.Save();
            if (ClimbResult(true, res.Reward, res.Drops.Count, res.LevelUps)) return;
            bool next = res.RunsLeft > 0 && floor + 1 < Trials.DungeonFloors;
            var spec = new ResultsSpec { Win = true, Mvp = true, Where = where, Reward = res.Reward, First = res.First, Ups = UpsOf(res.LevelUps),
                Note = $"{D.Name}: {res.RunsLeft} run{(res.RunsLeft == 1 ? "" : "s")} left today" };
            spec.Gear.AddRange(res.Drops);
            if (next) spec.Add(Words.BackTo("Dungeons"), UiRole.Secondary, () => End(ScreenId.Dungeon));
            spec.Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.Dungeon); App.EditDungeonTeam(key, floor); });
            if (res.RunsLeft > 0) spec.Add(Words.Again, UiRole.Secondary, () => { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor)); });
            if (next) spec.Add(Words.Next + " \u25B6", UiRole.Primary, () => { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor + 1)); });
            else spec.Add(Words.BackTo("Dungeons"), UiRole.Primary, () => End(ScreenId.Dungeon));
            ShowResults(Words.Cleared, spec);
        }

        // The arena uses the same results screen as every other fight (it had the old narrow pop-up in both
        // orientations): the points on the strip, the rank change and fights left as the note.
        void FinishArena()
        {
            var rv = _spec.Rival;
            var res = G.CompleteArena(rv, _b.Result == BattleResult.Victory); App.Save();
            if (ClimbResult(res.Win, res.Reward, 0, null)) return;
            string rank = res.After.Name != res.Before.Name ? $"{res.After.Name} rank{(res.Win ? " reached!" : "")} · " : "";
            var spec = new ResultsSpec { Win = res.Win, Mvp = res.Win, Where = $"Arena · vs {rv.Name}", Reward = res.Win ? res.Reward : null,
                Headline = $"{(res.Delta > 0 ? "+" : "")}{res.Delta} points ({res.Points})", LoseText = res.Win ? null : "A loss costs points. Strengthen your team or pick an easier rival.",
                Note = $"{rank}Fights left today: {G.S.daily.arenaTries}" };
            spec.Add(Words.EditTeam, UiRole.Secondary, () => { End(ScreenId.ArenaPvp); var again = G.CurrentRivals().FirstOrDefault(x => x.Name == rv.Name); if (again != null && G.S.daily.arenaTries > 0) App.OpenArenaPrep(again); });
            spec.Add(Words.BackTo("Arena"), UiRole.Primary, () => End(ScreenId.ArenaPvp));
            ShowResults(res.Win ? Words.Victory : Words.Defeat, spec);
        }

        IEnumerator AutoReplay()
        {
            yield return new WaitForSecondsRealtime(3f); // fixed 3-second pause, not sped up by 2x/3x (0.9.0)
            if (G.S.settings.autoReplay && App.Screen == ScreenId.Battle && _finished) { App.CloseModal(); Begin(_spec.Stage); }
        }

        public void AskRetreat()
        {
            if (_finished) return;
            string why = IsTitan ? (_spec.Practice ? "Practice: nothing is used." : "Damage so far still counts. The try was used when the fight started.") : _spec.Mode == BattleMode.Arena ? "Retreating counts as a loss." : "You get no rewards for this battle.";
            App.Modal("Retreat?", 200, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 50), why, 14, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Stay", Ui.Kind.Secondary)) App.CloseModal();
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Retreat", Ui.Kind.Danger))
                {
                    App.CloseModal();
                    // Leaving a climb after its first fight stops it (stopped by you) and shows its summary; an arena
                    // retreat still counts as a loss.
                    if (_spec.Mode == BattleMode.Arena && _climb != null) _climbRetreat = true;
                    else if (_climb != null) { G.S.settings.autoReplay = false; StopClimb(ClimbStop.ByPlayer, WhereOf(_spec)); return; }
                    if (IsTitan || _spec.Mode == BattleMode.Arena) { if (_loop != null) StopCoroutine(_loop); ResetCinema(); _b.Retreat(); Finish(); }
                    else { G.S.settings.autoReplay = false; if (_auto != null) { if (_loop != null) StopCoroutine(_loop); Time.timeScale = 1; _finished = true; _auto.Record(false, null, false, null); ShowAutoSummary("ended by a retreat", _spec.Stage); } else End(BackScreen); }
                }
            });
        }

        // Leaving mid-cinematic (Titan moves, intro, lair reveal) stops the coroutine before its own clean-up
        // runs (code review M4): restore the camera, the HUD, the Titan's place and pose, and drop "Tap to skip".
        void ResetCinema()
        {
            _cinema = _skippable = false; _revealTitle = null;
            if (App?.Arena != null) { App.Arena.ClearOverride(); App.Arena.BreathTarget = 0; }
            foreach (var kv in _views) if (kv.Key.Titan && kv.Value != null) kv.Value.ResetToHome();
        }

        // ---------- HUD ----------
        public void DrawHud()
        {
            if (_b == null) return;
            if (_cinema)
            {
                // Titan cinematics and the lair reveal: the HUD steps aside (a skip hint during the arrivals).
                if (_revealTitle != null) DrawRevealTitle();
                if (_skippable) Ui.Text(new Rect(0, Ui.Safe.yMax - 40, Ui.W, 28), "Tap to skip", 13, new Color(1, 1, 1, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter);
                return;
            }
            if (Ui.Landscape) { DrawHudWide(); return; }
            HudSkills = Rect.zero;
            var safe = Ui.Safe; var cam = App.Arena.Cam;
            // Portrait gets the landscape rules too (PLAN step 111, in-house report S3): 48 dp round buttons with
            // 6-unit gaps, a Pause button, and turn chips marked by a shield or skull glyph.
            float bs = Mathf.Max(48, Ui.Tap), bg = 6, top = safe.y + 6;
            float left = Mathf.Max(6, safe.x + 6), right = Mathf.Min(Ui.W - 6, safe.xMax - 6);
            HudTurns = HudSkills = Rect.zero;
            GUI.DrawTexture(new Rect(0, 0, Ui.W, top + bs + 24), Ui.Solid(new Color(0.06f, 0.05f, 0.08f, 0.55f)));
            if (RoundButton(new Rect(left, top, bs, bs), Icon.Close, "Leave", false)) AskRetreat();
            string info = _spec.Mode switch
            {
                BattleMode.Titan => $"Titan · Turn {Mathf.Min(_b.BossTurns + 1, TitanTurns)}/{TitanTurns} · {Ui.Fmt(_b.TitanDamage)} dmg",
                BattleMode.Dungeon => $"{Trials.Dungeon(_spec.Dungeon).Name} · F{_spec.Floor + 1} · W{_b.Wave + 1}/{_b.Waves.Count}",
                BattleMode.Arena => $"Arena · vs {_spec.Rival.Name}",
                BattleMode.Hard => $"Hard {Game.StageLabel(_spec.Stage)} · Wave {_b.Wave + 1}/{_b.Waves.Count}",
                _ => $"Stage {Game.StageLabel(_spec.Stage)} · Wave {_b.Wave + 1}/{_b.Waves.Count}",
            };
            // The same buttons in the same order as landscape (HudOrder); the ones that don't fit beside Leave
            // wrap to a second row under them, right-aligned (Status moved to the bottom before: UI/UX review #9).
            var (rows, lastRowX) = DrawHudButtons(right, top, left + bs + bg, bs, bg);
            float infoY = top + bs + 2;
            if (rows > 1) infoY = top + rows * (bs + bg) - bg + 2;
            Ui.Text(new Rect(left + 4, infoY, right - left - 8, 20), info, 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            float barY = infoY + 22;
            if (rows > 1) GUI.DrawTexture(new Rect(0, top + bs + 24, Ui.W, barY - top - bs - 24), Ui.Solid(new Color(0.06f, 0.05f, 0.08f, 0.55f)));
            DrawTurnBar(barY);
            RoundPill(8, barY + 48, false);

            // unit bars + tap targets
            float barTop = safe.yMax - 92;
            bool manual = _awaiting && _actor != null && _actor.Ally && !Auto && !_paused;
            var targets = manual ? _b.AffectedBy(_actor, _sel) : NoUnits;
            foreach (var kv in _views)
            {
                var u = kv.Key; var v = kv.Value;
                if (v == null) continue;
                v.SetHighlight(manual && targets.Contains(u) ? (u.Ally ? new Color(0.36f, 0.82f, 0.48f, 0.8f) : new Color(1f, 0.35f, 0.35f, 0.85f)) : (u == _actor ? new Color(1f, 0.81f, 0.35f, 0.7f) : (Color?)null));
                if (!u.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.Titan ? TitanTagPoint(v) // the Titan's tag sits above its horn tips
                    : v.HeadTop ? v.HeadTop.position + Vector3.up * 0.25f : v.transform.position + Vector3.up * 2.2f);
                if (sp.z < 0) continue;
                // Tags stay on screen, below the turn bar and round pill, and above the turn banner (its WEAK/RESIST
                // badge and status icons ran into "Ysolde's turn": UI/UX review #9).
                float x = Mathf.Clamp(sp.x / Ui.Scale, 46, Ui.W - 46), y = Mathf.Min(Mathf.Max(barY + 104, (Screen.height - sp.y) / Ui.Scale), barTop - 96);
                float w = u.Titan ? 140 : 76;
                Icons.Draw(new Rect(x - w / 2 - 10, y - 29, 13, 13), Icons.ForElement(u.El), Palette.Element(u.El));
                Ui.Text(new Rect(x - w / 2 + 4, y - 30, w + 6, 16), u.Ally ? u.Template.ShortName : u.Name, 11, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                Ui.Bar(new Rect(x - w / 2, y - 15, w, 7), u.Titan ? (float)TitanSpec.Health(_titanShown) : u.Hp / (float)u.MaxHp, u.Ally ? Palette.Good : Palette.Bad);
                var sh = u.Shield; if (sh != null) GUI.DrawTexture(new Rect(x - w / 2, y - 15, w * Mathf.Clamp01(sh.Val / (float)u.MaxHp), 3), Ui.Solid(new Color(0.7f, 0.85f, 1f)));
                BreakBar(new Rect(x - w / 2, y - 7, w, 4), u.Break); // weakness break meter
                Ui.Bar(new Rect(x - w / 2, y - 2, w, 3), (float)(u.Tm / Core.Battle.TmFull), Palette.Blue);
                if (u.Effects.Count > 0)
                {
                    var ir = StatusIcons.Row(x, y + 4, u.Effects, 16, true);
                    var hitIr = new Rect(ir.x - 4, ir.center.y - Ui.Tap / 2, Mathf.Max(ir.width + 8, Ui.Tap), Ui.Tap); // 48 dp, as in landscape
                    if (!manual && !App.ModalOpen && Ui.Button(hitIr, "", Ui.Kind.Ghost)) StatusIcons.ShowLegend(App); // tap the icons: the legend
                }
                if (manual && targets.Contains(u))
                {
                    MatchupBadge(new Rect(x - 34, y - 50, 68, 18), Core.Battle.Matchup(_actor, _actor.Template.Skills[_sel], u));
                    Vector3 feet = cam.WorldToScreenPoint(v.transform.position);
                    float fy = (Screen.height - feet.y) / Ui.Scale;
                    var tap = new Rect(x - 45, y - 30, 90, Mathf.Max(60, fy - y + 40));
                    if (!App.ModalOpen && Ui.Button(tap, "", Ui.Kind.Ghost))
                    {
                        var sk = _actor.Template.Skills[_sel];
                        _chosen = sk.Target == Target.Enemy || sk.Target == Target.Ally ? u : null;
                        _awaiting = false;
                    }
                }
                IntentChip(u, x - w / 2, y - (manual && targets.Contains(u) ? 74 : 54)); // enemy intent, above the tag (and the matchup badge)
            }

            // banner + skill bar
            string banner = _paused ? "Paused" : !string.IsNullOrEmpty(_banner) ? _banner : manual ? $"{_actor.Template.ShortName}'s turn" : "";
            Ui.Text(new Rect(0, barTop - 64, Ui.W, 30), banner, 19, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            if (manual) Ui.Text(new Rect(16, barTop - 36, Ui.W - bs - 30, 32), SkillText.Describe(_actor.Template.Skills[_sel]), 11, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
            GUI.DrawTexture(new Rect(0, barTop, Ui.W, Ui.H - barTop), Ui.Solid(new Color(0, 0, 0, 0.6f)));
            Ui.GoldRule(new Rect(0, barTop, Ui.W, 1));
            if (manual)
            {
                // The landscape disc's content on a plate (UI/UX review #9: the portrait buttons were text plates with
                // an unreadable target line): the target icon, the short name, and the target or "N turns" in a
                // readable colour.
                var sks = _actor.Template.Skills; var names = ShortNames(_actor.Template);
                float w = (Ui.W - 20 - (sks.Length - 1) * 8) / sks.Length;
                for (int i = 0; i < sks.Length; i++)
                {
                    int cd = _actor.Cds[i]; bool sel = _sel == i;
                    var b = new Rect(10 + i * (w + 8), barTop + 10, w, 70);
                    if (i == 0) HudSkills = new Rect(10, barTop + 10, Ui.W - 20, 70);
                    if (Ui.Button(b, "", sel ? Ui.Kind.ChipOn : Ui.Kind.Secondary, 13, cd == 0)) _sel = i;
                    Icons.Draw(new Rect(b.x + 10, b.y + 10, 22, 22), SkillIcon(sks[i].Target), cd > 0 ? Ui.DisabledText : sel ? Palette.Gold : Palette.Text);
                    Ui.Text(new Rect(b.x + 36, b.y + 6, b.width - 44, 30), names[i], 13, cd > 0 ? Ui.DisabledText : sel ? Palette.Gold : Palette.Text, FontStyle.Bold);
                    Ui.Text(new Rect(b.x + 6, b.y + 38, b.width - 12, 24), cd > 0 ? CooldownText(cd) : TargetWord(sks[i].Target), 12, cd > 0 ? Palette.Hex("#ffb0a4") : Palette.Hex("#e0d6c4"), FontStyle.Bold, TextAnchor.MiddleCenter);
                }
            }
            else Ui.Text(new Rect(0, barTop, Ui.W, 90), Auto ? "Auto battle" : _actor != null && !_actor.Ally ? "Enemy turn…" : "", 13, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        // ---------- the corner buttons, one list for both orientations (design rule 8) ----------
        enum HudBtn { Pause, Speed, Auto, Climb, Replay, View, Status }
        // Right to left. Climb shows where Auto Climb can run, Replay on campaign stages.
        static readonly HudBtn[] HudOrder = { HudBtn.Pause, HudBtn.Speed, HudBtn.Auto, HudBtn.Climb, HudBtn.Replay, HudBtn.View, HudBtn.Status };
        // Draws them from `right` leftward; a button that would cross minX starts a new row under the first.
        // Returns the rows used and the x where the last row ended.
        (int rows, float lastX) DrawHudButtons(float right, float top, float minX, float bs, float bg)
        {
            var shown = _hudShown; shown.Clear();
            foreach (var hb in HudOrder)
            {
                if (hb == HudBtn.Climb && !CanClimb) continue;
                if (hb == HudBtn.Replay && _spec.Mode != BattleMode.Stage) continue;
                shown.Add(hb);
            }
            var boxes = UiLayout.HudButtons(right, top, minX, bs, bg, shown.Count); // Core: checked at 390 units with 56 dp taps
            int rows = 1; float bx = right;
            for (int i = 0; i < shown.Count; i++)
            {
                var hb = shown[i]; var r = Ui.ToRect(boxes[i]); bx = r.x; float y = r.y;
                if (r.y > top + 0.5f) rows = Mathf.Max(rows, 1 + Mathf.RoundToInt((r.y - top) / (bs + bg)));
                switch (hb)
                {
                    case HudBtn.Pause: if (RoundButton(r, _paused ? Icon.Play : Icon.Pause, _paused ? "Resume" : "Pause", _paused)) TogglePause(); break;
                    case HudBtn.Speed: if (RoundButton(r, Icon.Speed, $"{G.S.settings.speed}x", G.S.settings.speed > 1)) { G.S.settings.speed = G.S.settings.speed >= 3 ? 1 : G.S.settings.speed + 1; if (!_paused) Time.timeScale = _finished ? 1 : G.S.settings.speed; App.Save(); } break;
                    case HudBtn.Auto: if (RoundButton(r, Icon.Auto, "Auto", Auto)) ToggleAuto(); HudAutoSpeed = new Rect(bx, y, bs * 2 + bg, bs); break;
                    case HudBtn.Climb: if (RoundButton(r, Icon.Climb, Words.Climb, Climbing)) ToggleClimb(); break; // Auto Climb (PLAN step 112)
                    case HudBtn.Replay: if (RoundButton(r, Icon.Loop, Words.Replay, G.S.settings.autoReplay)) ToggleAutoReplay(); break;
                    case HudBtn.View: if (RoundButton(r, Icon.Camera, Words.View, false)) CycleCamera(); break;
                    // The status legend on any turn, your own included (UX review #5/#9).
                    case HudBtn.Status: if (RoundButton(r, Icon.Info, "Status", false) && !App.ModalOpen) StatusIcons.ShowLegend(App); break;
                }
            }
            return (rows, bx);
        }
        readonly List<HudBtn> _hudShown = new List<HudBtn>(); // reused: the HUD runs every GUI event
        // Skill buttons read the same in both orientations: the target's icon and word, and "N turns" while cooling down.
        static Icon SkillIcon(Target t) => t switch { Target.Enemy => Icon.Sword, Target.AllEnemies => Icon.Bolt, Target.Ally => Icon.Heart, Target.AllAllies => Icon.Shield, _ => Icon.Star };
        static string TargetWord(Target t) => t switch { Target.Enemy => "Single", Target.AllEnemies => "All foes", Target.Ally => "Ally", Target.AllAllies => "All allies", _ => "Self" };
        static string CooldownText(int cd) => cd == 1 ? "1 turn" : $"{cd} turns";

        // ---------- landscape battle HUD (docs/sigilbound/PLAN.md step 10) ----------
        float _t0; bool _paused;

        // One name for it everywhere (UX review #14: the button said Loop, the results said auto-replay).
        void ToggleAutoReplay()
        {
            bool climbing = _climbPending;
            AutoClimb.SetReplay(G.S.settings, !G.S.settings.autoReplay); App.Save(); // Replay on turns Auto Climb off
            App.Toast(G.S.settings.autoReplay ? "Replay on: the stage starts again after each win" : "Replay off");
            if (climbing && G.S.settings.autoReplay) { StopClimb(ClimbStop.ByPlayer); return; }
            // On at a campaign victory screen: the stage starts again after the usual pause.
            if (G.S.settings.autoReplay && _finished && _b != null && _b.Result == BattleResult.Victory && _spec.Mode == BattleMode.Stage) StartCoroutine(AutoReplay());
        }

        // Auto battle; while Auto Climb runs it stays on (the climb needs it).
        void ToggleAuto()
        {
            if (Climbing) { App.Toast("Auto stays on while Auto Climb is on"); return; }
            G.S.settings.auto = !G.S.settings.auto; App.Save();
        }

        void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0 : (_finished ? 1 : Mathf.Clamp(G.S.settings.speed, 1, 3));
        }

        // A round corner button with a drawn icon and a caption; at least 48 dp (UX review #3: they were 37 dp, 6 apart).
        // "On" also shows as a lit ring and a gold dot, not only a colour change.
        bool RoundButton(Rect r, Icon icon, string caption, bool on)
        {
            Ui.Round(r, on ? Palette.Hex("#3d3020") : new Color(0.08f, 0.06f, 0.1f, 0.86f), on ? Palette.Gold : Palette.Trim, Mathf.RoundToInt(r.width / 2));
            if (on) Ui.Round(new Rect(r.xMax - 12, r.y + 1, 10, 10), Palette.Gold, Palette.Hex("#3d3020"), 5);
            Icons.Draw(new Rect(r.x + r.width * 0.29f, r.y + r.height * 0.14f, r.width * 0.42f, r.height * 0.42f), icon, on ? Palette.Gold : Palette.Text);
            Ui.Text(new Rect(r.x - 8, r.yMax - r.height * 0.42f, r.width + 16, r.height * 0.34f), caption, 11, on ? Palette.Gold : Palette.Hex("#d8ccb6"), FontStyle.Bold, TextAnchor.MiddleCenter);
            return Ui.Button(r, "", Ui.Kind.Ghost);
        }
        // Short skill captions per hero, worked out once (SkillText.ShortNames).
        static readonly Dictionary<HeroDef, string[]> _shortNames = new Dictionary<HeroDef, string[]>();
        static string[] ShortNames(HeroDef t)
        {
            if (!_shortNames.TryGetValue(t, out var n)) _shortNames[t] = n = SkillText.ShortNames(t.Skills);
            return n;
        }

        // "▲ WEAK" / "▼ RESIST" over a highlighted target (Battle.Matchup); nothing for an even matchup.
        // Enemy intent (PLAN P2-15): what each foe will do next (Battle.PreviewAction), tap for the skill's details.
        // Not on the foe whose turn it is (it is doing it), nor in cinematics or once the fight is over.
        readonly EnemyIntentUi _intents = new EnemyIntentUi();
        void IntentChip(Unit u, float x, float y)
        {
            if (u.Ally || u == _actor || _b.Over || _finished) return;
            var it = _intents.Of(_b, u);
            if (EnemyIntentUi.Chip(x, y, it, !App.ModalOpen)) EnemyIntentUi.ShowDetails(App, it);
        }

        static void MatchupBadge(Rect r, int m)
        {
            if (m == 0) return;
            Ui.Round(r, m > 0 ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), m > 0 ? Palette.Good : Palette.Bad, 8);
            Ui.Text(r, m > 0 ? "▲ WEAK" : "▼ RESIST", 11, m > 0 ? Palette.Hex("#bfe8c8") : Palette.Hex("#ffc0b0"), FontStyle.Bold, TextAnchor.MiddleCenter);
        }

        // Where the HUD put its turn bar, skill buttons and Auto/speed this frame (the tutorial's rings use them).
        public static Rect HudTurns, HudSkills, HudAutoSpeed;

        void DrawHudWide()
        {
            var safe = Ui.Safe; var cam = App.Arena.Cam;
            float top = safe.y + 6, left = Mathf.Max(8, safe.x + 8), right = Mathf.Min(Ui.W - 8, safe.xMax - 8);
            // Corner buttons at least 48 dp with 8-unit gaps; everything below the top row moves down by dy.
            float bs = Mathf.Max(48, Ui.Tap), bg = 8, dy = bs - 40;
            HudTurns = HudSkills = Rect.zero;

            // ---- top-left: retreat, stage and wave, timer ----
            if (RoundButton(new Rect(left, top, bs, bs), Icon.Close, "Leave", false)) AskRetreat();
            string where = _spec.Mode switch
            {
                BattleMode.Titan => $"Titan · Turn {Mathf.Min(_b.BossTurns + 1, TitanTurns)}/{TitanTurns}",
                BattleMode.Dungeon => $"{Trials.Dungeon(_spec.Dungeon).Name} · Floor {_spec.Floor + 1}",
                BattleMode.Arena => $"Arena · vs {_spec.Rival.Name}",
                BattleMode.Hard => $"Hard {Game.StageLabel(_spec.Stage)}",
                _ => $"Stage {Game.StageLabel(_spec.Stage)}",
            };
            string round = _spec.Mode == BattleMode.Titan ? $"{Ui.Fmt(_b.TitanDamage)} damage" : _spec.Mode == BattleMode.Arena ? "" : $"Wave {_b.Wave + 1} of {_b.Waves.Count}";
            int secs = Mathf.FloorToInt(Time.unscaledTime - _t0);
            float infoW = Ui.Compact ? 170 : 246; // narrower on 4:3 tablets, so the corner buttons keep one row
            var info = new Rect(left + bs + bg, top + (bs - 40) / 2, infoW + 54, 40);
            Ui.Round(new Rect(info.x - 4, info.y, infoW, 40), new Color(0.06f, 0.05f, 0.08f, 0.72f), null, 10);
            Ui.Text(new Rect(info.x + 6, info.y + 1, infoW - 10, 20), where, 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Icons.Draw(new Rect(info.x + 6, info.y + 23, 12, 12), Icon.Clock, Palette.Dim);
            Ui.Text(new Rect(info.x + 22, info.y + 20, infoW - 30, 18), $"{secs / 60}:{secs % 60:00}{(round.Length > 0 ? "   " + round : "")}", 12, Palette.Hex("#d0c4ae"));
            // Centred under the turn-order bar (beside the info box it ran under the turn chips).
            RoundPill(Ui.W / 2, top + 82 + dy, true, true);

            // ---- top-right: the shared corner buttons (HudOrder), the same order as portrait ----
            DrawHudButtons(right, top, info.x + infoW + 4, bs, bg);

            // ---- turn order, centred under the top row ----
            if (!_b.Over)
            {
                if (_turnsFrame != Time.frameCount) { _turnsFrame = Time.frameCount; _turns = _b.PredictTurns(7); }
                // Chips 28 tall with 11-point names; allies and enemies differ by a shield or skull glyph and by
                // the frame, not only green against red (UX review #4), and each face carries its element icon.
                float cg = 4, ch = 28, cw = Mathf.Min(88, (Ui.W - 40 - 56) / Mathf.Max(1, _turns.Count) - cg), tw = _turns.Count * (cw + cg) + 40; // fits a 640-unit tablet
                float tx = Ui.W / 2 - tw / 2, ty = top + bs + 6;
                Ui.Round(new Rect(tx - 4, ty - 2, tw + 8, ch + 4), new Color(0.04f, 0.03f, 0.06f, 0.66f), null, 8);
                Ui.Text(new Rect(tx, ty, 38, ch), "Next", 11, Palette.Hex("#d0c4ae"), FontStyle.Bold, TextAnchor.MiddleLeft);
                HudTurns = new Rect(tx - 4, ty - 2, tw + 8, ch + 4);
                for (int i = 0; i < _turns.Count; i++)
                {
                    var u = _turns[i]; var r = new Rect(tx + 40 + i * (cw + cg), ty, cw, ch);
                    bool now = i == 0 && u == _actor;
                    Ui.Panel(r, u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), now ? Palette.Gold : u.Ally ? Palette.Good : Palette.Bad, 7);
                    // A small face in an element-coloured frame, with the element's icon on its corner.
                    bool face = !u.Titan && Portraits.Thumb(new Rect(r.x + 3, r.y + 3, 22, 22), u.Template.Id, Palette.Element(u.El));
                    if (face) Ui.ElementBadge(new Rect(r.x + 15, r.y + 14, 13, 13), u.El);
                    else Ui.ElementBadge(new Rect(r.x + 4, r.y + 4, 20, 20), u.El);
                    Icons.Draw(new Rect(r.xMax - 14, r.y + 8, 11, 11), u.Ally ? Icon.Shield : Icon.Skull, u.Ally ? Palette.Hex("#bfe8c8") : Palette.Hex("#ffc0b0"));
                    string nm = u.Titan ? "Titan" : u.Template.ShortName;
                    Ui.Text(new Rect(r.x + 29, r.y, r.width - 44, r.height), nm.Length > 6 ? nm.Substring(0, 6) : nm, 11, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                }
            }

            // ---- unit tags: slim bars with a level badge, nudged apart so none overlap ----
            bool manual = _awaiting && _actor != null && _actor.Ally && !Auto && !_paused;
            var targets = manual ? _b.AffectedBy(_actor, _sel) : NoUnits;
            var tags = _tags; tags.Clear(); // reused lists, no LINQ: this runs every GUI event (performance review #5)
            // Name tags (30 tall, above minY) and their WEAK/RESIST badge (22 above the tag) stay below the round pill and
            // the turn banner (top + 104..132 + dy): the badges crossed "Ysolde's turn" (UI/UX review #9).
            float minY = top + 192 + dy;
            foreach (var kv in _views)
            {
                var u = kv.Key; var v = kv.Value;
                if (v == null) continue;
                v.SetHighlight(manual && targets.Contains(u) ? (u.Ally ? new Color(0.36f, 0.82f, 0.48f, 0.8f) : new Color(1f, 0.35f, 0.35f, 0.85f)) : (u == _actor ? new Color(1f, 0.81f, 0.35f, 0.7f) : (Color?)null));
                if (!u.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.Titan ? TitanTagPoint(v) // the Titan's tag sits above its horn tips
                    : v.HeadTop ? v.HeadTop.position + Vector3.up * 0.25f : v.transform.position + Vector3.up * 2.2f);
                if (sp.z < 0) continue;
                float w = (u.Titan ? 156 : 96) * Ui.TextGrow; // wider with bigger text, so names never run into the next tag
                float x = Mathf.Clamp(sp.x / Ui.Scale - Ui.Offset, Mathf.Max(0, safe.x) + w / 2 + 4, Mathf.Min(Ui.W, safe.xMax) - w / 2 - 4); // inside the safe area
                // The Titan's tag keeps an extra 18-unit gap under the skill banner: in close shots its head is off the
                // top and the tag is pinned here, where it read as part of the "QUAKE" banner (in-house report).
                float y = Mathf.Max(u.Titan ? minY + TitanTagGap : minY, (Screen.height - sp.y) / Ui.Scale);
                tags.Add((u, v, new Rect(x - w / 2, y - 30, w, 30)));
            }
            // Lowest on screen first; anything overlapping an earlier tag moves up until it clears.
            tags.Sort((a, b) => b.r.y.CompareTo(a.r.y));
            var placed = _placed; placed.Clear();
            for (int i = 0; i < tags.Count; i++)
            {
                var r = tags[i].r;
                for (int guard = 0; guard < 8 && OverlapsAny(placed, r); guard++) r.y -= 28;
                r.y = Mathf.Max(minY - 30, r.y);
                placed.Add(r); tags[i] = (tags[i].u, tags[i].v, r);
            }
            foreach (var (u, v, r) in tags)
            {
                // Name (11) with the element icon, the level badge (10), HP 8 units, break and turn meter 4 each.
                Icons.Draw(new Rect(r.x, r.y - 1, 13, 13), Icons.ForElement(u.El), Palette.Element(u.El));
                Ui.Text(new Rect(r.x + 15, r.y - 3, r.width + 30, 16), u.Ally ? u.Template.ShortName : u.Name, 11, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                var badge = new Rect(r.x, r.y + 13, 22, 16);
                Ui.Round(badge, u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), Palette.Element(u.El), 4);
                Ui.Text(badge, NumText.Of(u.Level), 11, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                var bar = new Rect(r.x + 24, r.y + 14, r.width - 24, 8);
                Ui.Bar(bar, u.Titan ? (float)TitanSpec.Health(_titanShown) : u.Hp / (float)u.MaxHp, u.Ally ? Palette.Good : Palette.Bad);
                var sh = u.Shield; if (sh != null) GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(sh.Val / (float)u.MaxHp), 3), Ui.Solid(new Color(0.7f, 0.85f, 1f)));
                BreakBar(new Rect(bar.x, bar.yMax + 1, bar.width, 4), u.Break);
                Ui.Bar(new Rect(bar.x, bar.yMax + (u.Break > 0 ? 6 : 2), bar.width, 4), (float)(u.Tm / Core.Battle.TmFull), Palette.Blue);
                if (u.Effects.Count > 0)
                {
                    var ir = StatusIcons.Row(r.x, r.yMax + 5, u.Effects, 18, false);
                    // A 48 dp invisible hit box around the icons (UX review #9: the row was 14 dp tall).
                    var hitIr = new Rect(ir.x - 4, ir.center.y - Ui.Tap / 2, Mathf.Max(ir.width + 8, Ui.Tap), Ui.Tap);
                    if (!manual && !App.ModalOpen && Ui.Button(hitIr, "", Ui.Kind.Ghost)) StatusIcons.ShowLegend(App); // tap the icons: the legend
                }
                if (manual && targets.Contains(u))
                {
                    // Weak / Resist for the chosen skill on this target: an arrow and a word, not only a colour.
                    MatchupBadge(new Rect(r.center.x - 36, r.y - 22, 72, 18), Core.Battle.Matchup(_actor, _actor.Template.Skills[_sel], u));
                    Vector3 feet = cam.WorldToScreenPoint(v.transform.position);
                    float fy = (Screen.height - feet.y) / Ui.Scale;
                    var tap = new Rect(r.center.x - 48, r.y, 96, Mathf.Max(70, fy - r.y + 30));
                    if (!App.ModalOpen && Ui.Button(tap, "", Ui.Kind.Ghost))
                    {
                        var sk = _actor.Template.Skills[_sel];
                        _chosen = sk.Target == Target.Enemy || sk.Target == Target.Ally ? u : null;
                        _awaiting = false;
                    }
                }
                IntentChip(u, r.x, r.y - (manual && targets.Contains(u) ? 46 : 26));
            }

            // ---- banner, and the skill buttons bottom-right (only on a manual turn) ----
            string banner = _paused ? "Paused" : !string.IsNullOrEmpty(_banner) ? _banner : manual ? $"{_actor.Template.ShortName}'s turn" : "";
            if (banner.Length > 0) Ui.Text(new Rect(0, top + 104 + dy, Ui.W, 28), banner, 18, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true); // under the round pill, not on it
            float bottom = safe.yMax - 8;
            if (manual)
            {
                var sks = _actor.Template.Skills;
                var names = ShortNames(_actor.Template);
                float d = 66, g = 10, x0 = right - sks.Length * (d + g) + g;
                // What the selected skill does, just left of the buttons.
                HudSkills = new Rect(x0, bottom - d, right - x0, d);
                // Description at 12 (was 10) in a box that grows upward to fit it.
                float dw = Mathf.Min(360, x0 - left - 10), dh = Mathf.Max(d, Ui.Label(12, null, FontStyle.Normal, TextAnchor.UpperLeft, true).CalcHeight(new GUIContent(SkillText.Describe(sks[_sel])), dw - 20) + 30);
                var desc = new Rect(Mathf.Max(left, x0 - dw - 10), bottom - dh, dw, dh);
                Ui.Round(desc, new Color(0.06f, 0.05f, 0.08f, 0.8f), Palette.Bronze, 10);
                Ui.Text(new Rect(desc.x + 10, desc.y + 4, desc.width - 20, 20), sks[_sel].Name, 13, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                Ui.Text(new Rect(desc.x + 10, desc.y + 25, desc.width - 20, desc.height - 27), SkillText.Describe(sks[_sel]), 12, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                for (int i = 0; i < sks.Length; i++)
                {
                    int cd = _actor.Cds[i];
                    var b = new Rect(x0 + i * (d + g), bottom - d, d, d);
                    bool sel = _sel == i;
                    Ui.Round(b, sel ? Palette.Hex("#3d3020") : Palette.Card, sel ? Palette.Gold : cd > 0 ? Palette.Bronze : Palette.Trim, Mathf.RoundToInt(d / 2));
                    var ic = SkillIcon(sks[i].Target);
                    Icons.Draw(new Rect(b.x + d * 0.27f, b.y + d * 0.16f, d * 0.46f, d * 0.46f), ic, cd > 0 ? Palette.Dim : sel ? Palette.Gold : Palette.Text);
                    // The skill's (short) name, not "A1" (UX review #5); the full name and text are in the box beside.
                    Ui.Text(new Rect(b.x - 10, b.yMax - d * 0.38f, d + 20, d * 0.32f), names[i], 11, sel ? Palette.Gold : Palette.Hex("#d0c4ae"), FontStyle.Bold, TextAnchor.MiddleCenter);
                    if (cd > 0)
                    {
                        Ui.Round(b, new Color(0, 0, 0, 0.55f), null, Mathf.RoundToInt(d / 2));
                        Ui.Text(new Rect(b.x, b.y + d * 0.1f, d, d * 0.36f), cd.ToString(), 20, Palette.Hex("#ff8a7a"), FontStyle.Bold, TextAnchor.MiddleCenter);
                        Ui.Text(new Rect(b.x - 4, b.y + d * 0.42f, d + 8, d * 0.22f), cd == 1 ? "turn" : "turns", 11, Palette.Hex("#ffb0a4"), FontStyle.Bold, TextAnchor.MiddleCenter); // "N turns", as in portrait
                    }
                    else if (Ui.Button(b, "", Ui.Kind.Ghost)) _sel = i;
                }
            }
            else
            {
                string state = Auto ? "Auto battle" : _actor != null && !_actor.Ally ? "Enemy turn…" : "";
                if (state.Length > 0) Ui.TextOnArt(new Rect(right - 200, bottom - 26, 196, 26), state, 13, Palette.Hex("#e0d6c4"), FontStyle.Normal, TextAnchor.MiddleRight);
            }
        }

        // ---------- results screen (docs/sigilbound/PLAN.md step 11; landscape, campaign stages) ----------
        readonly Dictionary<Unit, long> _taken = new Dictionary<Unit, long>(), _healed = new Dictionary<Unit, long>();
        float ResultsHeight => Mathf.Min(Ui.H - 24, 400);

        // What a results screen shows; every mode fills one (UX review #10: only campaign stages had this layout).
        sealed class ResultsSpec
        {
            public bool Win, Mvp;                 // Mvp: crown the top damage dealer (wins, and the Titan)
            public string Where = "";             // "Stage 1-3", "Hard 2-4", "Vault · Floor 3", "Titan"
            public Reward Reward; public bool First; public string FirstText = "First clear bonus!";
            public List<(int uid, int level)> Ups; public List<GearSave> Gear = new List<GearSave>();
            public string Headline;               // big text on the rewards strip (the Titan's damage)
            public string Note;                   // a line above the buttons ("2 runs left today")
            public string LoseText = "No rewards this time. Level and gear up your heroes, bring elements that beat these foes, or summon new heroes.";
            // The buttons by role (design rule 3); drawn through Ui.ButtonRow, so the Primary is always last (right).
            public readonly List<(string label, UiRole role, System.Action go)> Buttons = new List<(string, UiRole, System.Action)>();
            public ResultsSpec Add(string label, UiRole role, System.Action go) { Buttons.Add((label, role, go)); return this; }
        }
        static List<(int uid, int level)> UpsOf(List<(HeroSave hero, int levels)> l) => l?.Select(x => (x.hero.uid, x.hero.level)).ToList();
        // Every battle finish, every mode, both orientations (design rule 7): one ResultsSpec, one renderer. The
        // height grows when the footer stacks (the switches over the buttons on narrow screens).
        void ShowResults(string title, ResultsSpec spec)
        {
            float w = UiLayout.ModalWidth(Ui.SafeContent().width, App.WideModalW) - 32, tap = Mathf.Max(44, Ui.Tap);
            var foot = UiLayout.ResultsFooter(new Box(0, 0, w, 400), ToggleCount, spec.Buttons.Count, tap);
            float extra = foot.Height - tap + (spec.Note != null ? 18 : 0);
            float h = Ui.Landscape ? Mathf.Min(Ui.H - 24, ResultsHeight + extra) : Mathf.Min(Ui.H - 40, 420 + extra + allyCount * 54);
            App.Modal(title, h, r => DrawResults(r, spec), App.WideModalW);
        }
        int allyCount => _b?.Allies.Count ?? 4;

        void DrawResults(Rect r, ResultsSpec spec)
        {
            bool win = spec.Win; var reward = spec.Reward; var ups = spec.Ups;
            var allies = _b.Allies;
            int standing = allies.Count(u => u.Alive);
            int secs = Mathf.FloorToInt(Time.unscaledTime - _t0);
            // Banner line: heroes standing and the battle time (the game has no star rating).
            Ui.Text(new Rect(r.x, r.y - 14, r.width, 20), $"{spec.Where}   ·   {standing}/{allies.Count} heroes standing   ·   {secs / 60}:{secs % 60:00}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            float y = r.y + 10;
            bool narrow = r.width < 600; // portrait and narrow windows: hero rows instead of columns
            // Rewards strip.
            var strip = new Rect(r.x, y, r.width, narrow ? 48 : 46);
            Ui.Round(strip, new Color(0.1f, 0.08f, 0.14f, 0.9f), Palette.Trim, 10);
            var itemsArea = strip;
            if (spec.Headline != null)
            {
                // The Titan's damage, large, at the left of the strip; any rewards share the rest.
                bool alone = reward == null && spec.LoseText == null;
                float hw = alone ? strip.width - 24 : Mathf.Min(260, strip.width * (narrow ? 0.42f : 0.36f));
                Ui.Text(new Rect(strip.x + 14, strip.y, hw, strip.height), spec.Headline, 20, Palette.Text, FontStyle.Bold, alone ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft);
                itemsArea = new Rect(strip.x + hw + 14, strip.y, strip.width - hw - 14, strip.height);
            }
            if (reward != null && (win || spec.Headline != null))
            {
                // Items: the same pictures as the top bar (Icons.Currency), shard names through ShardName.
                var items = new List<(Icon icon, int cur, Color c, string text)>();
                if (reward.Gold > 0) items.Add((Icon.Coin, (int)Icons.Cur.Gold, Palette.Gold, Ui.Fmt(reward.Gold)));
                if (reward.Xp > 0) items.Add((Icon.Star, -1, Palette.Blue, $"{Ui.Fmt(reward.Xp)} XP each"));
                if (reward.Common > 0) items.Add((Icon.Shard, (int)Icons.Cur.WornShard, Palette.Hex("#c9a37b"), $"{reward.Common} {ShardName(Shard.Common, reward.Common)}"));
                if (reward.Mystic > 0) items.Add((Icon.Shard, (int)Icons.Cur.MysticShard, Palette.Blue, $"{reward.Mystic} {ShardName(Shard.Mystic, reward.Mystic)}"));
                if (reward.Radiant > 0) items.Add((Icon.Shard, (int)Icons.Cur.RadiantShard, Palette.Hex("#ffb02e"), $"{reward.Radiant} {ShardName(Shard.Radiant, reward.Radiant)}"));
                if (reward.Essence > 0) items.Add((Icon.Essence, (int)Icons.Cur.Essence, Palette.Hex("#8fd8ff"), $"{reward.Essence} essence"));
                if (reward.Tome > 0) items.Add((Icon.Tome, (int)Icons.Cur.Tome, Palette.Gold, $"{reward.Tome} {TomeName(reward.Tome)}"));
                foreach (var gear in spec.Gear) items.Add((Icon.Shield, -1, Palette.GearRarity(gear.rarity), $"{GearRarityNames[gear.rarity]} {gear.Slot}"));
                if (items.Count == 0) items.Add((Icon.Check, -1, Palette.Dim, "No rewards"));
                // Narrow (portrait): two rows of items in a taller strip.
                int perRow = narrow ? Mathf.Max(1, Mathf.CeilToInt(items.Count / 2f)) : items.Count;
                float iw = Mathf.Min(170, (itemsArea.width - 20) / perRow);
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i]; int row = i / perRow, inRow = Mathf.Min(perRow, items.Count - row * perRow);
                    float ix = itemsArea.center.x - iw * inRow / 2 + (i % perRow) * iw, iy = strip.y + (narrow ? 2 + row * 22 : 0), ih = narrow ? 22 : strip.height;
                    var ir = new Rect(ix + 6, iy + ih / 2 - 10, 20, 20);
                    if (it.cur >= 0) Icons.Currency(ir, (Icons.Cur)it.cur); else Icons.Draw(ir, it.icon, it.c);
                    Ui.Text(new Rect(ix + 30, iy, iw - 32, ih), it.text, 12, Palette.Text, FontStyle.Bold);
                }
                if (spec.First) Ui.Text(new Rect(strip.xMax - 180, strip.y - 16, 176, 14), spec.FirstText, 11, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
            }
            else if (spec.LoseText != null && spec.Headline == null) Ui.Text(strip, spec.LoseText, 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            else if (spec.LoseText != null) Ui.Text(itemsArea, spec.LoseText, 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            y = strip.yMax + 10;

            // The footer: the Replay / Climb switches and the buttons, side by side or stacked (Core/UiLayout).
            float tap = Mathf.Max(44, Ui.Tap);
            var foot = UiLayout.ResultsFooter(Ui.ToBox(r), ToggleCount, spec.Buttons.Count, tap);
            // Per-hero columns: damage dealt, damage taken, healing done.
            float bottomH = foot.Height + 8 + (spec.Note != null ? 22 : 0), colH = r.yMax - bottomH - y;
            long maxDealt = System.Math.Max(1, allies.Max(u => u.DmgDealt));
            long maxTaken = System.Math.Max(1, allies.Max(u => _taken.TryGetValue(u, out var t) ? t : 0));
            long maxHeal = System.Math.Max(1, allies.Max(u => _healed.TryGetValue(u, out var h) ? h : 0));
            float cw = Mathf.Min(190, (r.width - (allies.Count - 1) * 10) / allies.Count), cx = r.center.x - (cw * allies.Count + 10 * (allies.Count - 1)) / 2;
            // MVP: the hero who dealt the most damage (a win only).
            var mvp = spec.Mvp ? allies.OrderByDescending(u => u.DmgDealt).FirstOrDefault() : null;
            if (narrow)
            {
                // Portrait: one compact row per hero (face, name, level or level up, the MVP crown, damage bar), so
                // portrait has the MVP and stats too (UI/UX review #6).
                float rh = Mathf.Clamp((colH - 6 * (allies.Count - 1)) / Mathf.Max(1, allies.Count), 40, 56), ry = y;
                foreach (var u in allies)
                {
                    var row = new Rect(r.x, ry, r.width, rh); ry += rh + 6;
                    var rar = Palette.Rarity(u.Template.Rarity);
                    var up = ups?.FirstOrDefault(l => l.uid == u.HeroUid);
                    bool levelled = up.HasValue && up.Value.uid != 0;
                    Ui.Round(row, new Color(0.09f, 0.07f, 0.12f, 0.92f), u == mvp ? Palette.Gold : u.Alive ? rar : Palette.Bronze, 10);
                    var crest = new Rect(row.x + 6, row.y + 4, rh - 8, rh - 8);
                    if (!Portraits.Thumb(crest, u.Template.Id, rar)) Ui.ElementBadge(crest, u.El, rar);
                    if (!u.Alive) { Ui.Round(crest, new Color(0, 0, 0, 0.55f), null, Mathf.RoundToInt(crest.width / 2)); Icons.Draw(new Rect(crest.center.x - 9, crest.center.y - 9, 18, 18), Icon.Skull, Palette.Bad); }
                    float tx = crest.xMax + 8, tw = row.width * 0.42f;
                    Ui.Text(new Rect(tx, row.y + 2, tw, rh * 0.5f), u.Template.ShortName + (u == mvp ? "  " + Ui.ColorTag("MVP", Palette.Gold) : ""), 13, u.Alive ? Palette.Text : Palette.Dim, FontStyle.Bold, TextAnchor.LowerLeft);
                    Ui.Text(new Rect(tx, row.y + rh * 0.5f, tw, rh * 0.5f - 2), levelled ? Ui.ColorTag($"Level up! Lv {up.Value.level}", Palette.Good) : $"Lv {u.Level}", 11, Palette.Hex("#c9b896"), FontStyle.Normal, TextAnchor.UpperLeft);
                    if (u == mvp) Icons.Draw(new Rect(crest.xMax - 12, crest.y - 4, 14, 14), Icon.Crown, Palette.Gold);
                    float sx = tx + tw + 6, sw = row.xMax - 10 - sx;
                    Icons.Draw(new Rect(sx, row.y + 6, 13, 13), Icon.Sword, Palette.Bad);
                    Ui.Text(new Rect(sx + 16, row.y + 2, sw - 16, 18), Ui.Fmt(u.DmgDealt), 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);
                    Ui.Bar(new Rect(sx, row.y + 22, sw, 5), u.DmgDealt / (float)maxDealt, Palette.Bad);
                    long hl0 = _healed.TryGetValue(u, out var hv) ? hv : 0;
                    if (hl0 > 0 && rh >= 46) Ui.Text(new Rect(sx, row.y + 28, sw, 16), $"Healing {Ui.Fmt(hl0)}", 11, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.MiddleRight);
                }
            }
            else foreach (var u in allies)
            {
                var col = new Rect(cx, y, cw, colH); cx += cw + 10;
                var rar = Palette.Rarity(u.Template.Rarity);
                var up = ups?.FirstOrDefault(l => l.uid == u.HeroUid);
                bool levelled = up.HasValue && up.Value.uid != 0;
                // Card: dark plate with a crisp rarity rim (gold for the MVP), dimmed if the hero fell.
                Ui.Round(col, new Color(0.09f, 0.07f, 0.12f, 0.92f), u == mvp ? Palette.Gold : u.Alive ? rar : Palette.Bronze, 12);
                var head = new Rect(col.x + 6, col.y + 6, col.width - 12, 64);
                Ui.Round(head, Color.Lerp(Palette.Element(u.El), Color.black, 0.72f), null, 9);
                // Crest: the painted portrait if there is one, else the element sigil in a rarity ring.
                var crest = new Rect(head.x + 8, head.y + 8, 48, 48);
                if (!Portraits.Thumb(crest, u.Template.Id, rar))
                {
                    Ui.Round(crest, Color.Lerp(Palette.Element(u.El), Color.black, 0.45f), rar, 24);
                    Icons.Draw(new Rect(crest.x + 10, crest.y + 10, 28, 28), Icons.ForElement(u.El), Color.white);
                }
                if (!u.Alive) { Ui.Round(crest, new Color(0, 0, 0, 0.55f), null, 24); Icons.Draw(new Rect(crest.x + 14, crest.y + 14, 20, 20), Icon.Skull, Palette.Bad); }
                float tx = crest.xMax + 8, tw = head.xMax - tx - 4;
                Ui.Text(new Rect(tx, head.y + 6, tw, 22), u.Template.ShortName, 15, u.Alive ? Palette.Text : Palette.Dim, FontStyle.Bold);
                Ui.Text(new Rect(tx, head.y + 27, tw, 16), levelled ? Ui.ColorTag($"Level up!  Lv {up.Value.level}", Palette.Good) : $"Lv {u.Level}", 12, Palette.Hex("#c9b896"), levelled ? FontStyle.Bold : FontStyle.Normal);
                // XP toward the next level (our saved heroes only).
                var hs = u.Ally ? G.HeroByUid(u.HeroUid) : null;
                if (hs != null) Ui.Bar(new Rect(tx, head.y + 47, tw - 2, 5), hs.xp / (float)Mathf.Max(1, XpToNext(hs.level)), levelled ? Palette.Good : Palette.Blue);
                if (u == mvp)
                {
                    var tag = new Rect(col.xMax - 52, col.y - 9, 46, 18);
                    Ui.Round(tag, Palette.Gold, null, 9);
                    Icons.Draw(new Rect(tag.x + 4, tag.y + 3, 12, 12), Icon.Crown, Palette.Hex("#1a1208"));
                    Ui.Text(new Rect(tag.x + 15, tag.y, 31, 18), "MVP", 11, Palette.Hex("#1a1208"), FontStyle.Bold, TextAnchor.MiddleCenter);
                }
                // Three labelled stats with bars scaled to the team's best.
                float by = head.yMax + 10, step = Mathf.Clamp((col.yMax - by - 6) / 3f, 30, 44);
                void Stat(Icon ic, Color c, string label, long v, long max)
                {
                    Icons.Draw(new Rect(col.x + 10, by + 1, 14, 14), ic, c);
                    Ui.Text(new Rect(col.x + 28, by - 2, col.width - 36, 18), label, 11, Palette.Dim);
                    Ui.Text(new Rect(col.x + 28, by - 2, col.width - 38, 18), Ui.Fmt(v), 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);
                    Ui.Bar(new Rect(col.x + 10, by + 18, col.width - 20, 6), v / (float)max, c);
                    by += step;
                }
                Stat(Icon.Sword, Palette.Bad, "Damage", u.DmgDealt, maxDealt);
                Stat(Icon.Shield, Palette.Blue, "Taken", _taken.TryGetValue(u, out var tk) ? tk : 0, maxTaken);
                Stat(Icon.Heart, Palette.Good, "Healing", _healed.TryGetValue(u, out var hl) ? hl : 0, maxHeal);
            }

            // While Auto Climb counts down to the next fight: its line and a Stop button instead of the mode's buttons.
            if (ClimbBar(r)) return;
            // A note (runs left, practice) and the mode's buttons, centred; the Replay / Climb switches at the left.
            if (spec.Note != null) Ui.Text(new Rect(r.x, r.yMax - foot.Height - 24, r.width, 20), spec.Note, 12, Palette.Hex("#c9bda6"), FontStyle.Normal, TextAnchor.MiddleCenter, true);
            ResultToggles(Ui.ToRect(foot.Toggles));
            var row2 = new List<(string, UiRole, bool)>(spec.Buttons.Count);
            foreach (var b in spec.Buttons) row2.Add((b.label, b.role, true));
            int hit = Ui.ButtonRow(Ui.ToRect(foot.Buttons), row2);
            if (hit >= 0) spec.Buttons[hit].go();
        }
    }
}
