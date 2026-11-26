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

    public sealed class BattleController : MonoBehaviour
    {
        public App App;
        Battle _b; BattleSpec _spec;
        readonly Dictionary<Unit, CharacterView> _views = new Dictionary<Unit, CharacterView>();
        Unit _actor; int _sel; bool _awaiting; string _banner = ""; bool _finished;
        Coroutine _loop;

        Game G => App.G;
        bool Auto => G.S.settings.auto;
        bool IsTitan => _spec.Mode == BattleMode.Titan;
        public BattleMode Mode => _spec?.Mode ?? BattleMode.Stage;
        public bool InProgress => _b != null && !_finished;

        public void Begin(int stage) => Begin(stage < 0 ? BattleSpec.Titan() : BattleSpec.ForStage(stage));

        public void Begin(BattleSpec spec)
        {
            if (G.Team.Count == 0) { App.Toast("Choose at least one hero."); return; }
            if (spec.Mode == BattleMode.Dungeon && !G.CanEnterFloor(spec.Dungeon, spec.Floor)) { App.Toast("No runs left today for this dungeon."); return; }
            if (spec.Mode == BattleMode.Arena && G.S.daily.arenaTries <= 0) { App.Toast("No arena fights left today."); return; }
            if (spec.Mode == BattleMode.Titan && !spec.Practice && G.S.daily.titanTries <= 0) { App.Toast("No Titan tries left today."); return; }
            if (spec.Mode == BattleMode.Hard && !G.HardOpen(spec.Stage)) { App.Toast("Clear the previous Hard stage first."); return; }
            _spec = spec;
            App.ClearLineup();
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
            App.Arena.Build(Mathf.Min(chapter, Chapters.Length - 1), IsTitan);
            App.Arena.FrameBattle(CamKey, IsTitan);
            Clear();
            Spawn(_b.Allies, true); Spawn(_b.Enemies, false);
            _finished = false; _awaiting = false; _banner = ""; _actor = null;
            _t0 = Time.unscaledTime; _paused = false; _taken.Clear(); _healed.Clear();
            App.Go(ScreenId.Battle);
            Sound.I?.StartMusic(IsTitan);
            Time.timeScale = Mathf.Clamp(G.S.settings.speed, 1, 3);
            _loop = StartCoroutine(Loop());
        }

        void Spawn(List<Unit> units, bool ally)
        {
            for (int i = 0; i < units.Count; i++)
            {
                var u = units[i];
                float scale = u.Titan ? 1.3f : u.Boss ? 1.35f : 1f;
                var pos = u.Titan ? Arena.ToUnity(BattleLayout.TitanSlot()) : Arena.Slot(ally, i, units.Count);
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
            Clear();
            App.CloseModal();
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
            if (_finished) { G.S.settings.autoReplay = false; App.Save(); End(BackScreen); }
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
            App.Toast($"Camera: {next.Name} ({(i + 1) % cams.Length + 1}/{cams.Length})");
        }

        // ---------- Titan cinematics (owner picks 2026-11-26; presentation only, results unchanged) ----------
        bool _cinema, _skippable;  // HUD hidden while a cinematic plays; the intro can be skipped by a tap
        static bool IsHdTitan(CharacterView v) => v != null && v.Hero != null && v.Hero.Style == AnimStyle.TitanHd;
        // The HD Titan's real head height (model 4.8 m x the view's 1.3 scale); the HeadTop marker sits at 87% of
        // a nominal height and under-read it. The old stand-in Titan keeps its marker.
        float TitanHeight(CharacterView v) => IsHdTitan(v) ? (float)BattleLayout.TitanHead : v != null && v.HeadTop ? Mathf.Max(2f, v.HeadTop.position.y - v.transform.position.y) : (float)BattleLayout.TitanHead;
        static float Ease(float u) => Mathf.SmoothStep(0, 1, Mathf.Clamp01(u));

        // Arrival: from the Titan's feet the camera tilts up its body to the burning visor, it roars, and
        // the camera pulls back into the battle view. About 3 s; a tap skips it.
        IEnumerator TitanIntro()
        {
            var tu = _views.Keys.FirstOrDefault(k => k.Titan); if (tu == null) yield break;
            var tv = _views[tu];
            for (float w = 0; w < 3f && tv != null && tv.Hero == null; w += Time.unscaledDeltaTime) yield return null; // its model is loading
            if (tv == null) yield break;
            var arena = App.Arena; float keepSpeed = Time.timeScale; Time.timeScale = 1;
            _cinema = _skippable = true;
            var T = tv.transform.position; var toTeam = Arena.ToUnity(BattleLayout.TeamCentre) - T; toTeam.y = 0; toTeam.Normalize();
            var side = Vector3.Cross(Vector3.up, toTeam);
            float H = TitanHeight(tv); bool roared = false;
            for (float t = 0; t < 3.1f; t += Time.unscaledDeltaTime)
            {
                if (t > 0.2f && Input.GetMouseButtonDown(0)) break;
                float u = Ease(t / 1.6f);
                var eye = T + toTeam * Mathf.Lerp(H * 0.62f, H * 0.95f, u) + side * 0.9f + Vector3.up * Mathf.Lerp(0.45f, 1.3f, u);
                var look = T + Vector3.up * Mathf.Lerp(0.3f, H * 0.93f, u);
                arena.Override(eye, look, 52f, t < 2.3f ? 1f : 1f - Ease((t - 2.3f) / 0.8f));
                if (!roared && t >= 1.6f)
                {
                    roared = true;
                    if (tv.Hero != null) foreach (var n in new[] { "Yes", "Wave", "Interact" }) if (tv.Hero.Has(n)) { tv.Hero.Play(n, false, 0.2f, 0.9f); break; }
                    Vfx.I.ShakeLong(0.3f, 0.9f); Sound.I?.Play("titanRoar", 1f, 1, 0);
                    SkillFx.I?.Dust(T, H * 0.7f, tu.El);
                }
                yield return null;
            }
            arena.ClearOverride(); _cinema = _skippable = false; Time.timeScale = _paused ? 0 : keepSpeed;
        }

        // The Titan's fight-ending blow: slow motion, a low camera looking up as it raises its weapon high,
        // then the slam: burning cracks, a shockwave, a flash, the team thrown back.
        IEnumerator Earthsplitter(Unit u, CharacterView v)
        {
            var m = v.Hero; var arena = App.Arena; float speed = Time.timeScale; _cinema = true;
            var T = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre);
            var toTeam = team - T; toTeam.y = 0; float dist = toTeam.magnitude; toTeam /= Mathf.Max(0.01f, dist);
            float H = TitanHeight(v);
            var eye = T + toTeam * (dist * 0.62f) + Vector3.Cross(Vector3.up, toTeam) * 1.8f + Vector3.up * 0.55f;
            Time.timeScale = 0.32f;
            string clip = m.Has("Sword_Attack") ? "Sword_Attack" : "Punch_Cross";
            m.PlayFrom(clip, 0f, 0.7f);
            for (float t = 0; t < 1.0f; t += Time.unscaledDeltaTime)
            {
                arena.Override(eye, T + Vector3.up * H * 0.8f, 44f, Ease(t / 0.25f));
                if (m.CurrentNormalized >= 0.3f) m.SetFrozen(true); // held high at the top of the swing
                yield return null;
            }
            m.SetFrozen(false); Time.timeScale = Mathf.Max(1f, speed);
            m.PlayFrom(clip, 0.32f, 2.2f);
            yield return new WaitForSecondsRealtime(0.1f);
            var hit = T + toTeam * Mathf.Min(dist * 0.62f, 5.5f); hit.y = 0;
            var fx = SkillFx.I;
            if (fx != null) { fx.Cracks(hit, u.El, 5f); fx.TitanSlam(hit, u.El); fx.Shockwave(hit, u.El, 6f); fx.CritFlash(u.El); }
            Vfx.I.ShakeLong(0.55f, 0.8f); Sound.I?.Play("earthsplit", 1f, 1, 0);
            foreach (var kv in _views) if (kv.Key.Ally && kv.Value != null) kv.Value.Shove(hit, 1.1f);
            for (float t = 0; t < 0.7f; t += Time.unscaledDeltaTime) { arena.Override(eye, Vector3.Lerp(T + Vector3.up * H * 0.8f, hit + Vector3.up, Ease(t / 0.2f)), 44f, 1f - Ease((t - 0.3f) / 0.4f)); yield return null; }
            arena.ClearOverride(); _cinema = false; Time.timeScale = _paused ? 0 : speed;
        }

        // The Titan's ultimate (owner direction 2026-11-26). The camera never moves; only the lens and the aim:
        // it crouches; the leap goes into slow motion while the lens zooms out and follows it up to the peak;
        // at the peak the battle's normal speed returns and the lens zooms back in as it falls and slams down
        // in front of the team with its effects.
        IEnumerator MeteorLeap(Unit u, CharacterView v)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true;
            var T0 = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre); team.y = T0.y;
            float H = TitanHeight(v); var eye = arena.PosePosition; float fov = arena.Cam.fieldOfView;
            float wide = Mathf.Min(fov + 34f, 78f), speed = Time.timeScale;
            const float apex = 12f;
            Vector3 Arc(float k) => Vector3.Lerp(T0, team, k) + Vector3.up * (4f * apex * k * (1f - k));
            arena.BreathTarget = 0;
            if (m.Has("Crouch_Idle_Loop")) m.Play("Crouch_Idle_Loop", true, 0.15f, 1f);
            for (float t = 0; t < 0.35f; t += Time.deltaTime) { arena.Override(eye, T0 + Vector3.up * H * 0.55f, fov, Ease(t / 0.2f)); yield return null; }
            // Rise, in slow motion: the lens zooms out and the aim follows the Titan up to the top of its leap.
            if (!_paused) Time.timeScale = speed * 0.4f;
            m.Play("Jump", false, 0.08f, 1.4f, true);
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
            for (float t = 0; t < fall; t += Time.deltaTime)
            {
                float f = t / fall, k = 0.5f + 0.5f * f * f;                  // gravity: faster and faster
                var p = Arc(k); v.transform.position = p;
                if (!landing && f > 0.6f && m.Has("Jump_Land")) { landing = true; m.Play("Jump_Land", false, 0.05f, 1.2f, true); }
                arena.Override(eye, Vector3.Lerp(p + Vector3.up * H * 0.5f, team + Vector3.up * H * 0.45f, Ease(f)), Mathf.Lerp(wide, fov, Ease(f)), 1f);
                yield return null;
            }
            v.transform.position = team;
            var fx = SkillFx.I;
            if (fx != null) { fx.TitanSlam(team, u.El); fx.Cracks(team, u.El, 4.5f); fx.Shockwave(team, u.El, 6f); }
            Vfx.I.ShakeLong(0.6f, 0.8f); Sound.I?.Play("titanSlam", 1f, 1, 0);
            foreach (var kv in _views) if (kv.Key.Ally && kv.Value != null) kv.Value.Shove(team, 1.4f);
            var look = team + Vector3.up * H * 0.45f;
            // Quake: three aftershock rings roll out while the camera rocks on a tilt.
            for (int i = 0; i < 3; i++) { int n = i; StartCoroutine(Later(0.25f * (n + 1), () => { SkillFx.I?.Shockwave(team, u.El, 5f + n * 2f); Vfx.I.Shake(0.18f); })); }
            for (float t = 0; t < 1.0f; t += Time.deltaTime) { arena.Override(eye, look, fov, 1f, 6f * Mathf.Sin(t * 18f) * (1f - t)); yield return null; }
            for (float t = 0; t < 0.5f; t += Time.deltaTime) { arena.Override(eye, look, fov, 1f - Ease(t / 0.5f)); yield return null; }
            arena.ClearOverride(); _cinema = false;
        }
        // Crushing Palm (owner-approved design, 2026-11-26): the Titan strides up to its target, a side-on
        // camera tracking it; then a low camera beside the victim looks up as the palm (its weapon) rises in
        // slow motion, and the slam lands with cracks and a shockwave under the target. It walks home after.
        IEnumerator CrushingPalm(Unit u, CharacterView v, CharacterView target)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true; float speed = Time.timeScale;
            var T0 = v.transform.position; var tp = target != null ? target.transform.position : Arena.ToUnity(BattleLayout.TeamCentre);
            float H = TitanHeight(v); var eye0 = arena.PosePosition; float fov0 = arena.Cam.fieldOfView;
            var to = T0 - tp; to.y = 0; var dir = to.normalized; var dest = tp + dir * 3.6f;
            var side = Vector3.Cross(Vector3.up, dir).normalized;
            arena.BreathTarget = 0;
            // 1. The stride in, side-on.
            if (m.Has("Walk_Loop")) m.Play("Walk_Loop", true, 0.15f, 0.8f);
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                float k = Ease(t / 1.5f); v.transform.position = Vector3.Lerp(T0, dest, k);
                if (Mathf.Repeat(t, 0.45f) < Time.deltaTime) { Vfx.I.Shake(0.12f); SkillFx.I?.Dust(v.transform.position, 1.6f, u.El); }
                var mid = Vector3.Lerp(v.transform.position, tp, 0.5f);
                float e = Ease(t / 0.4f);
                arena.Override(Vector3.Lerp(eye0, mid + side * 12f - dir * 2f + Vector3.up * 3.0f, e), Vector3.Lerp(eye0 + arena.PoseRotation * Vector3.forward * 10f, mid + Vector3.up * 3.0f, e), Mathf.Lerp(fov0, 50f, e), Ease(t / 0.3f));
                yield return null;
            }
            // 2. The palm rises in slow motion; the camera is low beside the victim, looking up.
            // Over the victim's shoulder, straight up the line at the Titan: no other hero can stand in that line
            // (a camera beside the victim had neighbours blocking the Titan).
            var teamC = Arena.ToUnity(BattleLayout.TeamCentre); var outward = Vector3.Dot(side, tp - teamC) >= 0 ? side : -side;
            var eye = tp - dir * 1.4f + outward * 0.7f + Vector3.up * 1.5f;
            var look = v.transform.position + Vector3.up * H * 0.6f;   // the whole Titan in frame
            string clip = m.Has("Ult_Overhead") ? "Ult_Overhead" : m.Has("Sword_Attack") ? "Sword_Attack" : "Punch_Cross";
            m.PlayFrom(clip, 0f, 0.9f);
            for (float t = 0; t < 3.6f && m.CurrentNormalized < 0.55f; t += Time.unscaledDeltaTime)
            {
                if (!_paused) Time.timeScale = m.CurrentNormalized > 0.28f ? speed * 0.3f : speed;   // a long slow-motion hold
                arena.Override(eye, look, 72f, 1f, 5f); yield return null;
            }
            if (!_paused) Time.timeScale = speed;
            // 3. The slam.
            var fx = SkillFx.I;
            if (fx != null) { fx.Cracks(tp, u.El, 2.6f); fx.Shockwave(tp, u.El, 4f); fx.TitanSlam(tp, u.El); }
            Vfx.I.ShakeLong(0.45f, 0.6f); Sound.I?.Play("titanSlam", 0.9f, 1, 0);
            if (target != null) target.Shove(tp + dir, 0.8f);
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
            if (m.Has("Walk_Loop")) m.Play("Walk_Loop", true, 0.15f, 0.9f);
            for (float t = 0; t < 0.9f; t += Time.deltaTime) { v.transform.position = Vector3.Lerp(from, to, Ease(t / 0.9f)); yield return null; }
            v.transform.position = to; m.Idle(0.25f);
        }

        // Cinder Breath (owner-approved design): the Titan rears and gathers fire at its chest, seen over its
        // shoulder looking down at the team; it sweeps a cone of embers across them; then the camera cuts low
        // behind the heroes and pushes in as the fire pours down.
        IEnumerator CinderBreath(Unit u, CharacterView v)
        {
            var m = v.Hero; var arena = App.Arena; _cinema = true;
            var T = v.transform.position; var team = Arena.ToUnity(BattleLayout.TeamCentre); team.y = T.y;
            float H = TitanHeight(v); var fx = SkillFx.I;
            var back = T - team; back.y = 0; back.Normalize(); var right = Vector3.Cross(Vector3.up, -back).normalized;
            // Over the shoulder, far enough back for horns, shoulders and the chest glow: offsets scale with the
            // Titan's real size (at 6.24 m the old fixed offsets put the camera inside its shoulder plate).
            float sz = H / 4.8f;
            var ots = T + right * 3.4f * sz + back * 5.6f * sz + Vector3.up * H * 1.2f;
            arena.BreathTarget = 0;
            string clip = m.Has("Ult_Channel") ? "Ult_Channel" : "Interact";
            m.PlayFrom(clip, 0f, 0.55f);
            Sound.I?.Play("titanRoar", 0.8f, 1, 0);
            // 1. Gather (1.8 s): over the shoulder, a slow push in while fire builds at its chest.
            for (float t = 0; t < 1.8f; t += Time.deltaTime)
            {
                arena.Override(ots - back * 0.6f * Ease(t / 1.8f), team + Vector3.up * 0.8f, 52f, Ease(t / 0.3f));
                if (fx != null && Mathf.Repeat(t, 0.2f) < Time.deltaTime) fx.Charge(v.transform, Element.Flame, 0.2f);
                yield return null;
            }
            var allies = _views.Where(kv => kv.Key.Ally && kv.Key.Alive && kv.Value != null).Select(kv => kv.Value.transform.position).ToList();
            if (allies.Count == 0) allies.Add(team);
            float camSide(Vector3 p) => Vector3.Dot(p - team, right);
            var a = allies.OrderBy(camSide).First() - right * 0.6f; var b = allies.OrderBy(camSide).Last() + right * 0.6f;
            var mouth = T - back * 0.6f + Vector3.up * H * 1.02f; var ots2 = ots - back * 0.6f;
            // 2. The breath sweeps across the team (1.8 s), the Titan still in frame.
            if (fx != null) StartCoroutine(fx.Breath(mouth, a + Vector3.up * 0.6f, b + Vector3.up * 0.6f, u.El, 1.8f));
            Sound.I?.Cast(Element.Flame);
            for (float t = 0; t < 1.8f; t += Time.deltaTime)
            {
                var aim = Vector3.Lerp(a, b, t / 1.8f);
                arena.Override(ots2, aim * 0.5f + team * 0.5f + Vector3.up * 0.8f, 52f, 1f);
                if (fx != null && Mathf.Repeat(t, 0.3f) < Time.deltaTime) foreach (var p in allies) if (Mathf.Abs(camSide(p) - camSide(aim)) < 1.6f) fx.FireFlare(p);
                yield return null;
            }
            // 3. Side-on wide shot (2.4 s): the Titan, the stream and the team in one frame; a second sweep back.
            if (fx != null) StartCoroutine(fx.Breath(mouth, b + Vector3.up * 0.6f, a + Vector3.up * 0.6f, u.El, 1.6f));
            var mid = Vector3.Lerp(T, team, 0.5f);
            for (float t = 0; t < 2.4f; t += Time.deltaTime)
            {
                arena.Override(mid + right * Mathf.Lerp(13f, 11f, Ease(t / 2.4f)) + Vector3.up * 1.6f, mid + Vector3.up * 2.4f, 56f, t < 1.9f ? 1f : 1f - Ease((t - 1.9f) / 0.5f), -4f);
                if (fx != null && Mathf.Repeat(t, 0.35f) < Time.deltaTime) foreach (var p in allies) fx.FireFlare(p);
                yield return null;
            }
            arena.ClearOverride(); _cinema = false;
        }

        // After the Meteor leap: a heavy hop back to its place.
        IEnumerator TitanHopHome(Unit u, CharacterView v)
        {
            var m = v.Hero; var from = v.transform.position; var to = v.Home;
            if (m.Has("Jump")) m.Play("Jump", false, 0.1f, 1.2f, true);
            for (float t = 0; t < 0.6f; t += Time.deltaTime) { float k = Ease(t / 0.6f); v.transform.position = Vector3.Lerp(from, to, k) + Vector3.up * Mathf.Sin(k * Mathf.PI) * 3f; yield return null; }
            v.transform.position = to;
            if (m.Has("Jump_Land")) m.Play("Jump_Land", false, 0.08f, 1.2f);
            Vfx.I.Shake(0.25f); SkillFx.I?.Dust(to, 2.5f, u.El);
            yield return new WaitForSeconds(0.3f);
            m.Idle(0.2f);
        }

        // ---------- turn loop ----------
        IEnumerator Loop()
        {
            if (IsTitan) yield return TitanIntro();
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
            _banner = sk.Name; bool meteor = false, palm = false, breath = false;
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
            if (u.Titan) Sound.I?.Play("swing_heavy", 0.9f, 0.7f);
            else if (melee || Impact.FamilyOf(u.Template.Id) == WeaponFamily.Bow) Sound.I?.Swing(Impact.FamilyOf(u.Template.Id));
            else Sound.I?.Cast(u.El);
            if (view != null)
            {
                var tgtView = hit.Count > 0 && _views.ContainsKey(hit[0]) ? _views[hit[0]] : null;
                Vector3 aim = tgtView != null ? tgtView.transform.position : view.transform.position + view.transform.forward * 2;
                // The colossal Titan: melee heroes stop well short of its legs instead of running into them.
                if (melee && hit.Count > 0 && hit[0].Titan) { var away = view.transform.position - aim; away.y = 0; aim += away.normalized * 1.6f; }
                view.Face(aim); // turn toward the target only while acting (0.17.1)
                if (!melee && !u.Titan && fx != null)
                {
                    // Casters: a spell circle and gathering energy; ultimates on foes also flood the area and shock out.
                    fx.CastCircle(view.transform.position, u.El); fx.Charge(view.transform, u.El, 0.5f);
                    if (ult && tgtView != null && !support && !single)
                    {
                        var c = tgtView.transform.position; c.x *= 0.3f;
                        fx.Aoe(c, 1.9f, u.El); StartCoroutine(Later(0.45f, () => fx.Shockwave(c, u.El, 2.6f)));
                    }
                }
                // Titan turns: the camera breathes down and tilts up at it during the wind-up; the HD Titan's
                // fight-ending blow is the Earthsplitter and its ultimate the Meteor leap.
                bool hdTitan = u.Titan && IsHdTitan(view);
                bool finale = hdTitan && (_b.Allies.All(a => !a.Alive) || _b.BossTurns + 1 >= _b.TurnLimit);
                // The Titan's three moves (owner-approved designs): Crushing Palm (skill 1), Quake = the Meteor
                // leap with aftershocks (skill 2), Cinder Breath (skill 3); any fight-ending blow is the Earthsplitter.
                meteor = hdTitan && !finale && skillIdx == 1;
                palm = hdTitan && !finale && skillIdx == 0;
                breath = hdTitan && !finale && skillIdx == 2;
                if (u.Titan) { App.Arena.BreathFocus = view.HeadTop ? view.HeadTop.position : view.transform.position + Vector3.up * 5f; App.Arena.BreathTarget = 1; }
                if (finale) yield return Earthsplitter(u, view);
                else if (meteor) yield return MeteorLeap(u, view);
                else if (palm) yield return CrushingPalm(u, view, tgtView);
                else if (breath) yield return CinderBreath(u, view);
                else yield return view.Attack(aim, melee, support, ult, () => { Vfx.I.Shake(0.35f); fx?.TitanSlam(view.transform.position, u.El); }, skillIdx);
                if (support && fx == null) Vfx.I.Cast(view.transform.position + Vector3.up * 0.1f, color);
                // Ultimates show the hero's signature big, on the caster and on every target or ally reached.
                if (ult && fx != null && !u.Titan)
                {
                    var motif = HeroSignatures.Of(u.Template.Id, u.El);
                    fx.Signature(view.Center, u.El, motif, 1.4f);
                    foreach (var t in events.Where(e => e.Unit != null && e.Unit != u && (e.Type == EvType.Damage || e.Type == EvType.Heal || e.Type == EvType.Status)).Select(e => e.Unit).Distinct())
                        if (_views.TryGetValue(t, out var tv) && tv != null) fx.Signature(tv.Center, u.El, motif, 1.0f);
                }
                if (!melee && hit.Count > 0 && !finale && !meteor && !palm && !breath)
                {
                    var from = view.Center + view.transform.forward * 0.4f;
                    string prop = u.Titan ? "" : Looks.Of(u.Template.Id).Prop;
                    string kind = prop == "bow" ? "arrow" : prop == "mirror" || prop == "staff" ? "bolt" : prop == "orb" || prop == "candle" ? "orb" : "shard";
                    if (kind == "arrow") Sound.I?.Play("arrow", 0.7f);
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
            yield return ShowEvents(events, color, sk.Hits > 1);
            _ultBy = null;
            if (meteor && view != null) yield return TitanHopHome(u, view);
            if (palm && view != null) yield return TitanWalkHome(u, view);
            if (u.Titan) App.Arena.BreathTarget = 0;
            if (view != null) { yield return view.Recover(); view.FaceHome(); }
            yield return new WaitForSeconds(0.25f);
        }

        IEnumerator ShowEvents(List<BattleEvent> events, Color color, bool multiHit = false)
        {
            // Multi-hit skills: each hit on a target lands on its own beat with a short stop between.
            var hitsLeft = new Dictionary<Unit, int>();
            if (multiHit) foreach (var e in events) if (e.Type == EvType.Damage && !e.Dot && e.Unit != null && e.Amount > 0) hitsLeft[e.Unit] = (hitsLeft.TryGetValue(e.Unit, out var n) ? n : 0) + 1;
            foreach (var e in events)
            {
                if (e.Type == EvType.Damage && e.Unit != null && e.Amount > 0) _taken[e.Unit] = (_taken.TryGetValue(e.Unit, out var tk) ? tk : 0) + e.Amount;
                if (e.Type == EvType.Heal && e.Source != null && e.Amount > 0) _healed[e.Source] = (_healed.TryGetValue(e.Source, out var hl) ? hl : 0) + e.Amount;
                if (e.Unit == null || !_views.TryGetValue(e.Unit, out var v) || v == null) continue;
                var c = v.Center;
                switch (e.Type)
                {
                    case EvType.Damage:
                        if (e.Dot) { SkillFx.I?.DebuffTick(v.transform.position, "poison"); Vfx.I.Number(c, Ui.Fmt(e.Amount), new Color(0.5f, 1f, 0.45f)); break; }
                        if (e.Amount <= 0) break; // fully absorbed by a shield: the "Shield" number says it (no "CRIT 0")
                        if (e.Source != null && !e.Source.Titan) Sound.I?.Hit(Impact.FamilyOf(e.Source.Template.Id), e.Crit);
                        else if (e.Source != null) Sound.I?.Play("hit_heavy", 1f, 0.7f);
                        else Sound.I?.Play(e.Crit ? "crit" : "hit", e.Crit ? 1 : 0.8f);
                        if (SkillFx.I != null) SkillFx.I.Impact(c, e.Source != null ? e.Source.El : _lastEl, e.Crit); else Vfx.I.Burst(c, color, e.Crit);
                        // The attacker's signature motif on each hit (phase E 53-55).
                        if (SkillFx.I != null && e.Source != null && !e.Source.Titan) SkillFx.I.Signature(c, e.Source.El, HeroSignatures.Of(e.Source.Template.Id, e.Source.El), e.Crit ? 0.9f : 0.6f);
                        // Heavier reaction on crits and hits over 14% of max HP; weak glancing blows may be rolled away.
                        v.Hit(e.Crit || e.Amount / (float)Mathf.Max(1, e.Unit.MaxHp) > 0.14f, e.Elem < 1 || e.Amount / (float)Mathf.Max(1, e.Unit.MaxHp) < 0.04f);
                        if (e.Crit) SkillFx.I?.CritFlash(e.Source != null ? e.Source.El : _lastEl);
                        Vfx.I.Number(c + Vector3.up * 0.3f, (e.Crit ? "CRIT " : "") + Ui.Fmt(e.Amount),
                            e.Crit ? new Color(1f, 0.85f, 0.2f) : e.Elem > 1 ? new Color(1f, 0.55f, 0.3f) : e.Elem < 1 ? new Color(0.7f, 0.7f, 0.8f) : Color.white, e.Crit ? 1.3f : 1f);
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
                    case EvType.Heal: Sound.I?.Play("heal", 0.5f); if (SkillFx.I != null) SkillFx.I.Heal(v.transform.position); else Vfx.I.Heal(v.transform.position); Vfx.I.Number(c, "+" + Ui.Fmt(e.Amount), new Color(0.45f, 1f, 0.5f)); break;
                    case EvType.Absorb: Vfx.I.Number(c + Vector3.up * 0.5f, "Shield " + Ui.Fmt(e.Amount), new Color(0.6f, 0.85f, 1f), 0.7f); break;
                    case EvType.Status:
                        Sound.I?.Status(e.Fx);
                        if (SkillFx.I == null) Vfx.I.Buff(v.transform.position, FxDef.IsBuff(e.Fx));
                        else if (e.Fx == FxType.Shield) SkillFx.I.Shield(v.transform.position);
                        else SkillFx.I.Buff(v.transform.position, FxDef.IsBuff(e.Fx));
                        if (e.Fx == FxType.Stun) { if (KnocksDown(e)) v.KnockDown(); else v.Stun(); }
                        else v.StatusPulse(FxDef.IsBuff(e.Fx));
                        Vfx.I.Number(c + Vector3.up * 0.6f, SkillText.EffectName(e.Fx), FxDef.IsBuff(e.Fx) ? new Color(1f, 0.85f, 0.4f) : new Color(0.8f, 0.55f, 1f), 0.7f); break;
                    case EvType.Text: Vfx.I.Number(c + Vector3.up * 0.6f, e.Text, new Color(0.8f, 0.9f, 1f), 0.7f); if (e.Text == "Revived!") v.Revived();
                        if (e.Text == "Counter!")
                        {
                            // The counter's own hit follows: face whoever it lands on.
                            var hitEv = events.Skip(events.IndexOf(e) + 1).FirstOrDefault(x => x.Type == EvType.Damage && x.Source == e.Unit);
                            if (hitEv?.Unit != null && _views.TryGetValue(hitEv.Unit, out var av) && av != null) v.Counter(av.transform.position);
                        }
                        break;
                    case EvType.Death: Sound.I?.Play(e.Unit.Ally ? "downed" : "kill", 0.9f); v.Die(); if (SkillFx.I != null) SkillFx.I.DeathFade(v.transform.position, e.Unit.El); else Vfx.I.Dissolve(v.transform.position + Vector3.up * 0.8f); break;
                }
            }
            yield return new WaitForSeconds(0.2f);
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
            if (_b.Result == BattleResult.Victory) StartCoroutine(Celebrate(true));
            else if (_spec.Mode == BattleMode.Arena) StartCoroutine(Celebrate(true)); // PvP only (owner): survivors kneel and slump forward; fallen heroes keep their death pose
            Sound.I?.StopMusic();
            if (_spec.Mode == BattleMode.Titan) { Sound.I?.Play("bigKill"); Sound.I?.Sting("victory"); StartCoroutine(Later(0.6f, () => Sound.I?.Play("coin"))); }
            else if (_b.Result == BattleResult.Victory) { Sound.I?.Sting("victory"); StartCoroutine(Later(0.45f, () => Sound.I?.Play("coin"))); }
            else Sound.I?.Play("downed", 1, 0.8f, 0);
            switch (_spec.Mode)
            {
                case BattleMode.Titan: FinishTitan(); return;
                case BattleMode.Dungeon: FinishDungeon(); return;
                case BattleMode.Arena: FinishArena(); return;
                case BattleMode.Hard: FinishHard(); return;
            }
            if (_b.Result == BattleResult.Victory)
            {
                int stage = _spec.Stage;
                var res = G.CompleteStage(stage); App.Save();
                App.Online.PushProfile();
                string ups = string.Join("\n", res.LevelUps.Select(l => $"{Hero(l.hero.id).ShortName} → Lv {l.hero.level}"));
                string gear = res.Gear == null ? "" : $"\nFound: {GearRarityNames[res.Gear.rarity]} {res.Gear.Set} {res.Gear.Slot}";
                bool canNext = stage + 1 < TotalStages && G.S.cleared > stage;
                bool replay = G.S.settings.autoReplay;
                // Auto-replay run totals (ui.js autoRecord): a summary when the run ends.
                if (replay || _auto != null)
                {
                    _auto ??= new AutoRun();
                    _auto.Record(true, res.Reward, res.Gear != null, res.LevelUps.Select(l => $"{Hero(l.hero.id).ShortName} Lv {l.hero.level}"));
                    if (!replay) { ShowAutoSummary("auto-replay switched off", stage); return; }
                }
                if (Ui.Landscape && !(replay && _auto != null))
                {
                    App.Modal("VICTORY", ResultsHeight, r => DrawResults(r, true, res.Reward, res.FirstClear,
                        res.LevelUps.Select(l => (l.hero.uid, l.hero.level)).ToList(), res.Gear, stage, canNext));
                    if (G.S.settings.autoReplay) StartCoroutine(AutoReplay());
                    return;
                }
                App.Modal("VICTORY", 330, r =>
                {
                    if (replay && _auto != null)
                    {
                        Ui.Text(new Rect(r.x, r.y + 150, r.width, 40), $"Next battle in a moment…\nThis run: {_auto.Wins} win{(_auto.Wins == 1 ? "" : "s")} · {Ui.Fmt(_auto.Gold)} gold{(_auto.Gear > 0 ? $" · {_auto.Gear} gear" : "")}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                        if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width, 44), "Stop auto-replay", Ui.Kind.Red)) { G.S.settings.autoReplay = false; App.Save(); ShowAutoSummary("stopped", stage); }
                        Ui.Text(new Rect(r.x, r.y, r.width, 60), res.Reward + $"\n+{Ui.Fmt(res.Reward.Xp)} XP each", 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                        Ui.Text(new Rect(r.x, r.y + 64, r.width, 80), ups + gear, 13, Palette.Good, FontStyle.Normal, TextAnchor.UpperCenter, true);
                        return;
                    }
                    Ui.Text(new Rect(r.x, r.y, r.width, 60), res.Reward + $"\n+{Ui.Fmt(res.Reward.Xp)} XP each" + (res.FirstClear ? " · First clear bonus!" : ""), 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    Ui.Text(new Rect(r.x, r.y + 64, r.width, 90), ups + gear, 13, Palette.Good, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    float y = r.yMax - 100;
                    if (canNext && Ui.Button(new Rect(r.x, y, r.width, 44), "Next Stage ▶")) { App.CloseModal(); Begin(Mathf.Min(G.S.cleared, TotalStages - 1)); }
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Replay", Ui.Kind.Secondary)) { App.CloseModal(); Begin(stage); }
                    if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Map", Ui.Kind.Secondary)) End(ScreenId.Campaign);
                });
                if (G.S.settings.autoReplay) StartCoroutine(AutoReplay());
            }
            else
            {
                G.S.settings.autoReplay = false; App.Save();
                if (_auto != null) { _auto.Record(false, null, false, null); ShowAutoSummary("ended by a defeat", _spec.Stage); return; }
                if (Ui.Landscape) { App.Modal("DEFEAT", ResultsHeight, r => DrawResults(r, false, null, false, null, null, _spec.Stage, false)); return; }
                App.Modal("DEFEAT", 250, r =>
                {
                    Ui.Text(new Rect(r.x, r.y, r.width, 80), "Level heroes with training, equip and upgrade gear, bring elements that beat the enemy, and summon new heroes.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Retry", Ui.Kind.Secondary)) { App.CloseModal(); Begin(_spec.Stage); }
                    if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Heroes")) End(ScreenId.Heroes);
                });
            }
        }

        AutoRun _auto;

        // Hard campaign result (MODES.hard.finish in extras.js).
        void FinishHard()
        {
            int idx = _spec.Stage;
            if (_b.Result != BattleResult.Victory)
            {
                App.Modal("DEFEAT", 250, r =>
                {
                    Ui.Text(new Rect(r.x, r.y, r.width, 80), "Hard stages hit much harder. Level up, upgrade gear and try again.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Retry", Ui.Kind.Secondary)) { App.CloseModal(); Begin(BattleSpec.ForHard(idx)); }
                    if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Map")) End(ScreenId.Campaign);
                });
                return;
            }
            var res = G.CompleteHard(idx); App.Save();
            App.Online.PushProfile();
            string ups = string.Join("\n", res.LevelUps.Select(l => $"{Hero(l.hero.id).ShortName} → Lv {l.hero.level}"));
            string gear = $"\nFound: {GearRarityNames[res.Gear.rarity]} {res.Gear.Set} {res.Gear.Slot}";
            int next = idx + 1;
            App.Modal("HARD CLEARED", 340, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 60), res.Reward + $"\n+{Ui.Fmt(res.Reward.Xp)} XP each" + (res.FirstClear ? " · First Hard clear!" : ""), 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                Ui.Text(new Rect(r.x, r.y + 64, r.width, 90), ups + gear, 13, Palette.Good, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (G.HardOpen(next) && Ui.Button(new Rect(r.x, r.yMax - 100, r.width, 44), $"Hard {Game.StageLabel(next)} ▶")) { App.CloseModal(); Begin(BattleSpec.ForHard(next)); }
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Replay", Ui.Kind.Secondary)) { App.CloseModal(); Begin(BattleSpec.ForHard(idx)); }
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Map", Ui.Kind.Secondary)) End(ScreenId.Campaign);
            });
        }

        // Turn-order bar under the header: the next 7 actors (Battle.PredictTurns, extras.js predictTurns).
        List<Unit> _turns = new List<Unit>(); int _turnsFrame = -1;
        void DrawTurnBar(float y)
        {
            if (_b.Over) return;
            if (_turnsFrame != Time.frameCount) { _turnsFrame = Time.frameCount; _turns = _b.PredictTurns(7); } // once per frame, not per GUI event
            var list = _turns;
            GUI.DrawTexture(new Rect(0, y, Ui.W, 30), Ui.Solid(new Color(0.04f, 0.03f, 0.06f, 0.5f)));
            Ui.Text(new Rect(8, y, 40, 30), "Next", 11, Palette.Dim, FontStyle.Bold, TextAnchor.MiddleLeft);
            float w = (Ui.W - 52 - 6 * 4) / 7f;
            for (int i = 0; i < list.Count; i++)
            {
                var u = list[i];
                var r = new Rect(48 + i * (w + 4), y + 3, w, 24);
                bool now = i == 0 && u == _actor;
                Ui.Panel(r, u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), now ? Palette.Gold : u.Ally ? Palette.Good : Palette.Bad, 8);
                string nm = u.Titan ? "Titan" : u.Template.ShortName;
                Ui.Text(r, $"{Ui.ColorTag("●", Palette.Element(u.El))}{(nm.Length > 5 ? nm.Substring(0, 5) : nm)}", 10, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
            }
        }

        // The whole auto-replay run (battles, wins, losses, gold, XP, shards, gear, level ups).
        void ShowAutoSummary(string why, int stage)
        {
            var run = _auto; _auto = null;
            if (run == null) return;
            App.Modal("Auto-replay summary", 330, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 20), $"Run {why}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
                Ui.Text(new Rect(r.x, r.y + 26, r.width, 160), run.Summary(), 13, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Replay", Ui.Kind.Secondary)) { App.CloseModal(); Begin(stage); }
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Map")) End(ScreenId.Campaign);
            });
        }

        void FinishTitan()
        {
            long dmg = _b.TitanDamage;
            if (_spec.Practice)
            {
                // Practice: nothing is granted or saved; the daily tries and best are untouched.
                App.Modal("Titan Practice", 300, r =>
                {
                    Ui.Text(new Rect(r.x, r.y, r.width, 40), $"{Ui.Fmt(dmg)} damage", 26, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                    Ui.Text(new Rect(r.x, r.y + 44, r.width, 40), "Practice: no rewards, no try used", 14, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
                    Ui.Text(new Rect(r.x, r.y + 90, r.width, 70), string.Join("\n", _b.Allies.Select(a => $"{a.Template.ShortName}: {Ui.Fmt(a.DmgDealt)}")), 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Again", Ui.Kind.Secondary)) { App.CloseModal(); Begin(BattleSpec.TitanPractice()); }
                    if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Continue")) End(ScreenId.Titan);
                });
                return;
            }
            var reward = G.CompleteTitan(dmg); App.Save();
            App.Modal("Titan Battle", 300, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 40), $"{Ui.Fmt(dmg)} damage", 26, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                Ui.Text(new Rect(r.x, r.y + 44, r.width, 40), reward.ToString(), 14, Palette.Gold, FontStyle.Normal, TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(r.x, r.y + 90, r.width, 70), string.Join("\n", _b.Allies.Select(a => $"{a.Template.ShortName}: {Ui.Fmt(a.DmgDealt)}")), 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width, 44), "Continue")) End(ScreenId.Titan);
            });
        }

        void FinishDungeon()
        {
            var key = _spec.Dungeon; int floor = _spec.Floor; var D = Trials.Dungeon(key);
            if (_b.Result != BattleResult.Victory)
            {
                App.Modal("DEFEAT", 220, r =>
                {
                    Ui.Text(new Rect(r.x, r.y, r.width, 50), "No run used. Strengthen your team and try again.", 13, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                    if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Retry", Ui.Kind.Secondary)) { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor)); }
                    if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Back")) End(ScreenId.Dungeon);
                });
                return;
            }
            var res = G.CompleteDungeon(key, floor); App.Save();
            string ups = string.Join("\n", res.LevelUps.Select(l => $"{Hero(l.hero.id).ShortName} → Lv {l.hero.level}"));
            string drops = string.Join("\n", res.Drops.Select(g => $"Found: {GearRarityNames[g.rarity]} {g.Set} {g.Slot}"));
            bool next = res.RunsLeft > 0 && floor + 1 < Trials.DungeonFloors;
            App.Modal($"FLOOR {floor + 1} CLEARED", 360, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 60), res.Reward + (res.Reward.Xp > 0 ? $"\n+{Ui.Fmt(res.Reward.Xp)} XP each" : "") + (res.First ? "\nFirst clear bonus!" : ""), 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                Ui.Text(new Rect(r.x, r.y + 64, r.width, 80), (ups + "\n" + drops).Trim(), 13, Palette.Good, FontStyle.Normal, TextAnchor.UpperCenter, true);
                Ui.Text(new Rect(r.x, r.yMax - 128, r.width, 22), $"{D.Name}: {res.RunsLeft} run{(res.RunsLeft == 1 ? "" : "s")} left today", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
                if (next && Ui.Button(new Rect(r.x, r.yMax - 100, r.width, 44), $"Floor {floor + 2} ▶")) { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor + 1)); }
                if (res.RunsLeft > 0 && Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Again", Ui.Kind.Secondary)) { App.CloseModal(); Begin(BattleSpec.ForDungeon(key, floor)); }
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Back", Ui.Kind.Secondary)) End(ScreenId.Dungeon);
            });
        }

        void FinishArena()
        {
            var rv = _spec.Rival;
            var res = G.CompleteArena(rv, _b.Result == BattleResult.Victory); App.Save();
            App.Modal(res.Win ? "VICTORY" : "DEFEAT", 290, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 50), $"vs {rv.Name}\n{Ui.ColorTag((res.Delta > 0 ? "+" : "") + res.Delta, res.Win ? Palette.Good : Palette.Bad)} points → {res.Points}", 15, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                float y = r.y + 56;
                if (res.After.Name != res.Before.Name) { Ui.Text(new Rect(r.x, y, r.width, 26), $"{res.After.Name} rank{(res.Win ? " reached!" : "")}", 17, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter); y += 28; }
                if (res.Win) { Ui.Text(new Rect(r.x, y, r.width, 22), res.Reward.ToString(), 13, Palette.Gold, FontStyle.Normal, TextAnchor.MiddleCenter); y += 24; }
                Ui.Text(new Rect(r.x, y, r.width, 22), $"Fights left today: {G.S.daily.arenaTries}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width, 44), "Continue")) End(ScreenId.ArenaPvp);
            });
        }

        IEnumerator AutoReplay()
        {
            yield return new WaitForSecondsRealtime(3f); // fixed 3-second pause, not sped up by 2x/3x (0.9.0)
            if (G.S.settings.autoReplay && App.Screen == ScreenId.Battle && _finished) { App.CloseModal(); Begin(_spec.Stage); }
        }

        public void AskRetreat()
        {
            if (_finished) return;
            string why = IsTitan ? "Damage so far still counts and uses this try." : _spec.Mode == BattleMode.Arena ? "Retreating counts as a loss." : "You get no rewards for this battle.";
            App.Modal("Retreat?", 200, r =>
            {
                Ui.Text(new Rect(r.x, r.y, r.width, 50), why, 14, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                if (Ui.Button(new Rect(r.x, r.yMax - 46, r.width / 2 - 5, 44), "Stay", Ui.Kind.Secondary)) App.CloseModal();
                if (Ui.Button(new Rect(r.x + r.width / 2 + 5, r.yMax - 46, r.width / 2 - 5, 44), "Retreat", Ui.Kind.Red))
                {
                    App.CloseModal();
                    if (IsTitan || _spec.Mode == BattleMode.Arena) { if (_loop != null) StopCoroutine(_loop); _b.Retreat(); Finish(); }
                    else { G.S.settings.autoReplay = false; if (_auto != null) { if (_loop != null) StopCoroutine(_loop); Time.timeScale = 1; _finished = true; _auto.Record(false, null, false, null); ShowAutoSummary("ended by a retreat", _spec.Stage); } else End(BackScreen); }
                }
            });
        }

        // ---------- HUD ----------
        public void DrawHud()
        {
            if (_b == null) return;
            if (_cinema)
            {
                // Titan cinematics: the HUD steps aside (a skip hint during the arrival).
                if (_skippable) Ui.Text(new Rect(0, Ui.Safe.yMax - 40, Ui.W, 28), "Tap to skip", 13, new Color(1, 1, 1, 0.7f), FontStyle.Normal, TextAnchor.MiddleCenter);
                return;
            }
            if (Ui.Landscape) { DrawHudWide(); return; }
            var safe = Ui.Safe; var cam = App.Arena.Cam;
            float top = safe.y;
            // header
            GUI.DrawTexture(new Rect(0, 0, Ui.W, top + 50), Ui.Solid(new Color(0.06f, 0.05f, 0.08f, 0.55f)));
            if (Ui.Button(new Rect(10, top + 8, 40, 34), "X", Ui.Kind.Secondary, 14)) AskRetreat();
            string info = _spec.Mode switch
            {
                BattleMode.Titan => $"Titan · Turn {Mathf.Min(_b.BossTurns + 1, TitanTurns)}/{TitanTurns} · {Ui.Fmt(_b.TitanDamage)} dmg",
                BattleMode.Dungeon => $"{Trials.Dungeon(_spec.Dungeon).Name} · F{_spec.Floor + 1} · W{_b.Wave + 1}/{_b.Waves.Count}",
                BattleMode.Arena => $"Arena · vs {_spec.Rival.Name}",
                BattleMode.Hard => $"Hard {Game.StageLabel(_spec.Stage)} · Wave {_b.Wave + 1}/{_b.Waves.Count}",
                _ => $"Stage {Game.StageLabel(_spec.Stage)} · Wave {_b.Wave + 1}/{_b.Waves.Count}",
            };
            float bx = Ui.W - 10;
            bx -= 46; if (Ui.Button(new Rect(bx, top + 8, 42, 34), $"{G.S.settings.speed}x", Ui.Kind.Secondary, 13)) { G.S.settings.speed = G.S.settings.speed >= 3 ? 1 : G.S.settings.speed + 1; Time.timeScale = _finished ? 1 : G.S.settings.speed; App.Save(); }
            bx -= 54; if (Ui.Button(new Rect(bx, top + 8, 50, 34), "Auto", Auto ? Ui.Kind.Primary : Ui.Kind.Secondary, 12)) { G.S.settings.auto = !G.S.settings.auto; App.Save(); }
            if (_spec.Mode == BattleMode.Stage) { bx -= 54; if (Ui.Button(new Rect(bx, top + 8, 50, 34), "Loop", G.S.settings.autoReplay ? Ui.Kind.Primary : Ui.Kind.Secondary, 12)) { G.S.settings.autoReplay = !G.S.settings.autoReplay; App.Save(); } }
            bx -= 46; if (Ui.Button(new Rect(bx, top + 8, 42, 34), "Cam", Ui.Kind.Secondary, 11)) CycleCamera();
            Ui.Text(new Rect(56, top + 8, bx - 60, 34), info, 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            DrawTurnBar(top + 50);

            // unit bars + tap targets
            bool manual = _awaiting && _actor != null && _actor.Ally && !Auto;
            var targets = manual ? _b.AffectedBy(_actor, _sel) : new List<Unit>();
            foreach (var kv in _views)
            {
                var u = kv.Key; var v = kv.Value;
                if (v == null) continue;
                v.SetHighlight(manual && targets.Contains(u) ? (u.Ally ? new Color(0.36f, 0.82f, 0.48f, 0.8f) : new Color(1f, 0.35f, 0.35f, 0.85f)) : (u == _actor ? new Color(1f, 0.81f, 0.35f, 0.7f) : (Color?)null));
                if (!u.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.Titan ? v.transform.position + Vector3.up * (float)BattleLayout.WormHornTop // the Titan's tag sits above its horns
                    : v.HeadTop ? v.HeadTop.position + Vector3.up * 0.25f : v.transform.position + Vector3.up * 2.2f);
                if (sp.z < 0) continue;
                float x = Mathf.Clamp(sp.x / Ui.Scale, 46, Ui.W - 46), y = Mathf.Max(safe.y + 114, (Screen.height - sp.y) / Ui.Scale); // tags stay on screen, below the turn bar
                float w = u.Titan ? 140 : 76;
                Ui.Text(new Rect(x - w / 2 - 10, y - 30, w + 20, 16), $"{Ui.ColorTag("●", Palette.Element(u.El))} {(u.Ally ? u.Template.ShortName : u.Name)}", 11, Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                Ui.Bar(new Rect(x - w / 2, y - 13, w, 7), u.Titan ? 1 : u.Hp / (float)u.MaxHp, u.Ally ? Palette.Good : Palette.Bad);
                var sh = u.Shield; if (sh != null) GUI.DrawTexture(new Rect(x - w / 2, y - 13, w * Mathf.Clamp01(sh.Val / (float)u.MaxHp), 3), Ui.Solid(new Color(0.7f, 0.85f, 1f)));
                Ui.Bar(new Rect(x - w / 2, y - 4, w, 3), (float)(u.Tm / Core.Battle.TmFull), Palette.Blue);
                if (u.Effects.Count > 0)
                {
                    var ir = StatusIcons.Row(x, y + 2, u.Effects, 16, true);
                    if (!manual && !App.ModalOpen && Ui.Button(ir, "", Ui.Kind.Ghost)) StatusIcons.ShowLegend(App); // tap the icons: the legend
                }
                if (manual && targets.Contains(u))
                {
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
            }

            // banner + skill bar
            float barTop = safe.yMax - 92;
            string banner = !string.IsNullOrEmpty(_banner) ? _banner : manual ? $"{_actor.Template.ShortName}'s turn" : "";
            Ui.Text(new Rect(0, barTop - 64, Ui.W, 30), banner, 19, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            if (manual) Ui.Text(new Rect(16, barTop - 36, Ui.W - 32, 32), SkillText.Describe(_actor.Template.Skills[_sel]), 11, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
            GUI.DrawTexture(new Rect(0, barTop, Ui.W, Ui.H - barTop), Ui.Solid(new Color(0, 0, 0, 0.6f)));
            Ui.GoldRule(new Rect(0, barTop, Ui.W, 1));
            if (manual)
            {
                var sks = _actor.Template.Skills;
                float w = (Ui.W - 20 - (sks.Length - 1) * 8) / sks.Length;
                for (int i = 0; i < sks.Length; i++)
                {
                    int cd = _actor.Cds[i];
                    string sub = sks[i].Target switch { Target.Enemy => "Single", Target.AllEnemies => "All foes", Target.Ally => "Ally", Target.AllAllies => "All allies", _ => "Self" };
                    string label = $"{sks[i].Name}\n{Ui.ColorTag(cd > 0 ? $"ready in {cd}" : sub, cd > 0 ? Palette.Bad : Palette.Dim)}";
                    if (Ui.Button(new Rect(10 + i * (w + 8), barTop + 10, w, 70), label, _sel == i ? Ui.Kind.ChipOn : Ui.Kind.Secondary, 13, cd == 0)) _sel = i;
                }
            }
            else Ui.Text(new Rect(0, barTop, Ui.W, 90), Auto ? "Auto battle" : _actor != null && !_actor.Ally ? "Enemy turn…" : "", 13, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
        }

        // ---------- landscape battle HUD (docs/sigilbound/PLAN.md step 10) ----------
        float _t0; bool _paused;

        void TogglePause()
        {
            _paused = !_paused;
            Time.timeScale = _paused ? 0 : (_finished ? 1 : Mathf.Clamp(G.S.settings.speed, 1, 3));
        }

        // A round corner button with a drawn icon and a tiny caption.
        bool RoundButton(Rect r, Icon icon, string caption, bool on)
        {
            GUI.DrawTexture(r, Ui.RoundTex(on ? Palette.Hex("#3d3020") : new Color(0.08f, 0.06f, 0.1f, 0.82f), on ? Palette.Gold : Palette.Bronze, Mathf.RoundToInt(r.width / 2)));
            Icons.Draw(new Rect(r.x + r.width * 0.26f, r.y + r.height * 0.18f, r.width * 0.48f, r.height * 0.48f), icon, on ? Palette.Gold : Palette.Text);
            Ui.Text(new Rect(r.x - 6, r.yMax - r.height * 0.36f, r.width + 12, r.height * 0.3f), caption, 9, on ? Palette.Gold : Palette.Dim, FontStyle.Bold, TextAnchor.MiddleCenter);
            return Ui.Button(r, "", Ui.Kind.Ghost);
        }

        void DrawHudWide()
        {
            var safe = Ui.Safe; var cam = App.Arena.Cam;
            float top = safe.y + 6, left = Mathf.Max(8, safe.x + 8), right = Mathf.Min(Ui.W - 8, safe.xMax - 8);

            // ---- top-left: retreat, stage and wave, timer ----
            if (RoundButton(new Rect(left, top, 40, 40), Icon.Close, "Leave", false)) AskRetreat();
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
            var info = new Rect(left + 48, top, 300, 40);
            GUI.DrawTexture(new Rect(info.x - 4, info.y + 2, 230, 36), Ui.RoundTex(new Color(0.06f, 0.05f, 0.08f, 0.6f), null, 10));
            Ui.Text(new Rect(info.x + 6, info.y + 2, 220, 18), where, 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Icons.Draw(new Rect(info.x + 6, info.y + 22, 12, 12), Icon.Clock, Palette.Dim);
            Ui.Text(new Rect(info.x + 22, info.y + 20, 200, 16), $"{secs / 60}:{secs % 60:00}{(round.Length > 0 ? "   " + round : "")}", 11, Palette.Dim);

            // ---- top-right: camera, loop, auto, speed, pause ----
            float bx = right - 40;
            if (RoundButton(new Rect(bx, top, 40, 40), _paused ? Icon.Play : Icon.Pause, _paused ? "Resume" : "Pause", _paused)) TogglePause();
            bx -= 46; if (RoundButton(new Rect(bx, top, 40, 40), Icon.Speed, $"{G.S.settings.speed}x", G.S.settings.speed > 1)) { G.S.settings.speed = G.S.settings.speed >= 3 ? 1 : G.S.settings.speed + 1; if (!_paused) Time.timeScale = _finished ? 1 : G.S.settings.speed; App.Save(); }
            bx -= 46; if (RoundButton(new Rect(bx, top, 40, 40), Icon.Auto, "Auto", Auto)) { G.S.settings.auto = !G.S.settings.auto; App.Save(); }
            if (_spec.Mode == BattleMode.Stage) { bx -= 46; if (RoundButton(new Rect(bx, top, 40, 40), Icon.Loop, "Loop", G.S.settings.autoReplay)) { G.S.settings.autoReplay = !G.S.settings.autoReplay; App.Save(); } }
            bx -= 46; if (RoundButton(new Rect(bx, top, 40, 40), Icon.Camera, "View", false)) CycleCamera();

            // ---- turn order, centred under the top row ----
            if (!_b.Over)
            {
                if (_turnsFrame != Time.frameCount) { _turnsFrame = Time.frameCount; _turns = _b.PredictTurns(7); }
                float cw = 70, cg = 4, tw = _turns.Count * (cw + cg) + 36;
                float tx = Ui.W / 2 - tw / 2, ty = top + 46;
                GUI.DrawTexture(new Rect(tx - 4, ty - 2, tw + 8, 26), Ui.RoundTex(new Color(0.04f, 0.03f, 0.06f, 0.55f), null, 8));
                Ui.Text(new Rect(tx, ty, 34, 22), "Next", 10, Palette.Dim, FontStyle.Bold, TextAnchor.MiddleLeft);
                for (int i = 0; i < _turns.Count; i++)
                {
                    var u = _turns[i]; var r = new Rect(tx + 36 + i * (cw + cg), ty, cw, 22);
                    bool now = i == 0 && u == _actor;
                    Ui.Panel(r, u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), now ? Palette.Gold : u.Ally ? Palette.Good : Palette.Bad, 7);
                    Icons.Draw(new Rect(r.x + 4, r.y + 5, 12, 12), Icons.ForElement(u.El), Palette.Element(u.El));
                    string nm = u.Titan ? "Titan" : u.Template.ShortName;
                    Ui.Text(new Rect(r.x + 17, r.y, r.width - 19, r.height), nm.Length > 7 ? nm.Substring(0, 7) : nm, 10, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                }
            }

            // ---- unit tags: slim bars with a level badge, nudged apart so none overlap ----
            bool manual = _awaiting && _actor != null && _actor.Ally && !Auto && !_paused;
            var targets = manual ? _b.AffectedBy(_actor, _sel) : new List<Unit>();
            var tags = new List<(Unit u, CharacterView v, Rect r)>();
            float minY = top + 136; // name tags stay below the skill banner (top + 76..104), even the Titan's
            foreach (var kv in _views)
            {
                var u = kv.Key; var v = kv.Value;
                if (v == null) continue;
                v.SetHighlight(manual && targets.Contains(u) ? (u.Ally ? new Color(0.36f, 0.82f, 0.48f, 0.8f) : new Color(1f, 0.35f, 0.35f, 0.85f)) : (u == _actor ? new Color(1f, 0.81f, 0.35f, 0.7f) : (Color?)null));
                if (!u.Alive) continue;
                Vector3 sp = cam.WorldToScreenPoint(u.Titan ? v.transform.position + Vector3.up * (float)BattleLayout.WormHornTop // the Titan's tag sits above its horns
                    : v.HeadTop ? v.HeadTop.position + Vector3.up * 0.25f : v.transform.position + Vector3.up * 2.2f);
                if (sp.z < 0) continue;
                float w = u.Titan ? 150 : 86;
                float x = Mathf.Clamp(sp.x / Ui.Scale - Ui.Offset, w / 2 + 4, Ui.W - w / 2 - 4);
                float y = Mathf.Max(minY, (Screen.height - sp.y) / Ui.Scale);
                tags.Add((u, v, new Rect(x - w / 2, y - 26, w, 26)));
            }
            // Lowest on screen first; anything overlapping an earlier tag moves up until it clears.
            tags.Sort((a, b) => b.r.y.CompareTo(a.r.y));
            var placed = new List<Rect>();
            for (int i = 0; i < tags.Count; i++)
            {
                var r = tags[i].r;
                for (int guard = 0; guard < 8 && placed.Any(p => p.Overlaps(r)); guard++) r.y -= 24;
                r.y = Mathf.Max(minY - 26, r.y);
                placed.Add(r); tags[i] = (tags[i].u, tags[i].v, r);
            }
            foreach (var (u, v, r) in tags)
            {
                var badge = new Rect(r.x, r.y + 10, 18, 16);
                GUI.DrawTexture(badge, Ui.RoundTex(u.Ally ? Palette.Hex("#1f3326") : Palette.Hex("#3a1c1c"), Palette.Element(u.El), 4));
                Ui.Text(badge, u.Level.ToString(), 9, Color.white, FontStyle.Bold, TextAnchor.MiddleCenter);
                Ui.Text(new Rect(r.x, r.y - 2, r.width + 30, 13), u.Ally ? u.Template.ShortName : u.Name, 10, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
                var bar = new Rect(r.x + 20, r.y + 12, r.width - 20, 6);
                Ui.Bar(bar, u.Titan ? 1 : u.Hp / (float)u.MaxHp, u.Ally ? Palette.Good : Palette.Bad);
                var sh = u.Shield; if (sh != null) GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(sh.Val / (float)u.MaxHp), 2), Ui.Solid(new Color(0.7f, 0.85f, 1f)));
                Ui.Bar(new Rect(bar.x, bar.yMax + 2, bar.width, 3), (float)(u.Tm / Core.Battle.TmFull), Palette.Blue);
                if (u.Effects.Count > 0)
                {
                    var ir = StatusIcons.Row(r.x, r.yMax + 1, u.Effects, 15, false);
                    if (!manual && !App.ModalOpen && Ui.Button(ir, "", Ui.Kind.Ghost)) StatusIcons.ShowLegend(App); // tap the icons: the legend
                }
                if (manual && targets.Contains(u))
                {
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
            }

            // ---- banner, and the skill buttons bottom-right (only on a manual turn) ----
            string banner = _paused ? "Paused" : !string.IsNullOrEmpty(_banner) ? _banner : manual ? $"{_actor.Template.ShortName}'s turn" : "";
            if (banner.Length > 0) Ui.Text(new Rect(0, top + 76, Ui.W, 28), banner, 18, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter, false, true);
            float bottom = safe.yMax - 8;
            if (manual)
            {
                var sks = _actor.Template.Skills;
                float d = 66, g = 10, x0 = right - sks.Length * (d + g) + g;
                // What the selected skill does, just left of the buttons.
                var desc = new Rect(Mathf.Max(left, x0 - 330), bottom - d, Mathf.Min(320, x0 - left - 10), d);
                GUI.DrawTexture(desc, Ui.RoundTex(new Color(0.06f, 0.05f, 0.08f, 0.72f), Palette.Bronze, 10));
                Ui.Text(new Rect(desc.x + 10, desc.y + 4, desc.width - 20, 18), sks[_sel].Name, 12, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
                Ui.Text(new Rect(desc.x + 10, desc.y + 22, desc.width - 20, desc.height - 24), SkillText.Describe(sks[_sel]), 10, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                for (int i = 0; i < sks.Length; i++)
                {
                    int cd = _actor.Cds[i];
                    var b = new Rect(x0 + i * (d + g), bottom - d, d, d);
                    bool sel = _sel == i;
                    GUI.DrawTexture(b, Ui.RoundTex(sel ? Palette.Hex("#3d3020") : Palette.Hex("#1d1726"), sel ? Palette.Gold : cd > 0 ? Palette.Bronze : Palette.Trim, Mathf.RoundToInt(d / 2)));
                    var ic = sks[i].Target switch { Target.Enemy => Icon.Sword, Target.AllEnemies => Icon.Bolt, Target.Ally => Icon.Heart, Target.AllAllies => Icon.Shield, _ => Icon.Star };
                    Icons.Draw(new Rect(b.x + d * 0.27f, b.y + d * 0.16f, d * 0.46f, d * 0.46f), ic, cd > 0 ? Palette.Dim : sel ? Palette.Gold : Palette.Text);
                    Ui.Text(new Rect(b.x - 8, b.yMax - d * 0.36f, d + 16, d * 0.3f), $"A{i + 1}", 10, sel ? Palette.Gold : Palette.Dim, FontStyle.Bold, TextAnchor.MiddleCenter);
                    if (cd > 0)
                    {
                        GUI.DrawTexture(b, Ui.RoundTex(new Color(0, 0, 0, 0.55f), null, Mathf.RoundToInt(d / 2)));
                        Ui.Text(b, cd.ToString(), 20, Palette.Bad, FontStyle.Bold, TextAnchor.MiddleCenter);
                    }
                    else if (Ui.Button(b, "", Ui.Kind.Ghost)) _sel = i;
                }
            }
            else
            {
                string state = Auto ? "Auto battle" : _actor != null && !_actor.Ally ? "Enemy turn…" : "";
                if (state.Length > 0) Ui.Text(new Rect(right - 200, bottom - 24, 200, 24), state, 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleRight);
            }
        }

        // ---------- results screen (docs/sigilbound/PLAN.md step 11; landscape, campaign stages) ----------
        readonly Dictionary<Unit, long> _taken = new Dictionary<Unit, long>(), _healed = new Dictionary<Unit, long>();
        float ResultsHeight => Mathf.Min(Ui.H - 24, 400);

        void DrawResults(Rect r, bool win, Reward reward, bool first, List<(int uid, int level)> ups, GearSave gear, int stage, bool canNext)
        {
            var allies = _b.Allies;
            int standing = allies.Count(u => u.Alive);
            int secs = Mathf.FloorToInt(Time.unscaledTime - _t0);
            // Banner line: heroes standing and the battle time (the game has no star rating).
            Ui.Text(new Rect(r.x, r.y - 14, r.width, 20), $"Stage {Game.StageLabel(stage)}   ·   {standing}/{allies.Count} heroes standing   ·   {secs / 60}:{secs % 60:00}", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter);
            float y = r.y + 10;
            // Rewards strip.
            var strip = new Rect(r.x, y, r.width, 46);
            GUI.DrawTexture(strip, Ui.RoundTex(new Color(0.12f, 0.2f, 0.3f, 0.55f), Palette.Bronze, 10));
            if (win && reward != null)
            {
                var items = new List<(Icon icon, Color c, string text)> { (Icon.Coin, Palette.Gold, Ui.Fmt(reward.Gold)), (Icon.Star, Palette.Blue, $"{Ui.Fmt(reward.Xp)} XP each") };
                if (reward.Common > 0) items.Add((Icon.Shard, Palette.Hex("#c9a37b"), $"{reward.Common} worn"));
                if (reward.Mystic > 0) items.Add((Icon.Shard, Palette.Blue, $"{reward.Mystic} mystic"));
                if (reward.Radiant > 0) items.Add((Icon.Shard, Palette.Hex("#ffb02e"), $"{reward.Radiant} radiant"));
                if (gear != null) items.Add((Icon.Shield, Palette.GearRarity(gear.rarity), $"{GearRarityNames[gear.rarity]} {gear.Slot}"));
                float iw = Mathf.Min(170, (strip.width - 20) / items.Count), ix = strip.center.x - iw * items.Count / 2;
                foreach (var it in items)
                {
                    Icons.Draw(new Rect(ix + 6, strip.y + 13, 20, 20), it.icon, it.c);
                    Ui.Text(new Rect(ix + 30, strip.y, iw - 32, strip.height), it.text, 12, Palette.Text, FontStyle.Bold);
                    ix += iw;
                }
                if (first) Ui.Text(new Rect(strip.xMax - 140, strip.y - 16, 136, 14), "First clear bonus!", 11, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleRight);
            }
            else Ui.Text(strip, "No rewards this time. Level and gear up your heroes, bring elements that beat these foes, or summon new heroes.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.MiddleCenter, true);
            y = strip.yMax + 10;

            // Per-hero columns: damage dealt, damage taken, healing done.
            float bottomH = 52, colH = r.yMax - bottomH - y;
            long maxDealt = System.Math.Max(1, allies.Max(u => u.DmgDealt));
            long maxTaken = System.Math.Max(1, allies.Max(u => _taken.TryGetValue(u, out var t) ? t : 0));
            long maxHeal = System.Math.Max(1, allies.Max(u => _healed.TryGetValue(u, out var h) ? h : 0));
            float cw = Mathf.Min(190, (r.width - (allies.Count - 1) * 10) / allies.Count), cx = r.center.x - (cw * allies.Count + 10 * (allies.Count - 1)) / 2;
            foreach (var u in allies)
            {
                var col = new Rect(cx, y, cw, colH); cx += cw + 10;
                GUI.DrawTexture(col, Ui.RoundTex(new Color(0.08f, 0.06f, 0.1f, 0.8f), u.Alive ? Palette.Rarity(u.Template.Rarity) : Palette.Bronze, 10));
                Icons.Draw(new Rect(col.x + 8, col.y + 8, 18, 18), Icons.ForElement(u.El), Palette.Element(u.El));
                Ui.Text(new Rect(col.x + 30, col.y + 4, col.width - 36, 22), u.Template.ShortName, 13, u.Alive ? Palette.Text : Palette.Dim, FontStyle.Bold);
                var up = ups?.FirstOrDefault(l => l.uid == u.HeroUid);
                string lv = up.HasValue && up.Value.uid != 0 ? Ui.ColorTag($"Level up! Lv {up.Value.level}", Palette.Good) : $"Lv {u.Level}";
                Ui.Text(new Rect(col.x + 8, col.y + 26, col.width - 16, 16), lv + (u.Alive ? "" : Ui.ColorTag("  · fell", Palette.Bad)), 11, Palette.Dim);
                float by = col.y + 48, bh = Mathf.Max(10, (col.yMax - by - 10) / 3f - 18);
                void Stat(Icon ic, Color c, long v, long max)
                {
                    Icons.Draw(new Rect(col.x + 8, by, 14, 14), ic, c);
                    Ui.Text(new Rect(col.x + 26, by - 2, col.width - 34, 18), Ui.Fmt(v), 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);
                    Ui.Bar(new Rect(col.x + 8, by + 17, col.width - 16, Mathf.Min(6, bh)), v / (float)max, c);
                    by += 34;
                }
                Stat(Icon.Sword, Palette.Bad, u.DmgDealt, maxDealt);
                Stat(Icon.Shield, Palette.Blue, _taken.TryGetValue(u, out var tk) ? tk : 0, maxTaken);
                Stat(Icon.Heart, Palette.Good, _healed.TryGetValue(u, out var hl) ? hl : 0, maxHeal);
            }

            // Buttons: Replay, Edit Team, Next (or Heroes on a defeat).
            float bw = Mathf.Min(200, (r.width - 20) / 3f), bx = r.center.x - (bw * 3 + 20) / 2, btnY = r.yMax - 46;
            if (Ui.Button(new Rect(bx, btnY, bw, 44), win ? "Replay" : "Retry", Ui.Kind.Blue)) { App.CloseModal(); Begin(stage); }
            if (Ui.Button(new Rect(bx + bw + 10, btnY, bw, 44), "Edit Team", Ui.Kind.Blue)) { End(ScreenId.Campaign); App.EditTeam(stage); }
            if (win)
            {
                if (Ui.Button(new Rect(bx + 2 * (bw + 10), btnY, bw, 44), canNext ? "Next" : "Map", Ui.Kind.Primary))
                { if (canNext) { App.CloseModal(); Begin(Mathf.Min(G.S.cleared, TotalStages - 1)); } else End(ScreenId.Campaign); }
            }
            else if (Ui.Button(new Rect(bx + 2 * (bw + 10), btnY, bw, 44), "Heroes", Ui.Kind.Primary)) End(ScreenId.Heroes);
        }
    }
}
