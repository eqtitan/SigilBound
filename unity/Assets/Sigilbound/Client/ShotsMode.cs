// Screenshot mode, so the game can be checked visually without a phone. Started only by the
// command-line argument -sbShots=<folder> (or "-sbShots <folder>"). It starts a fresh throwaway
// game (nothing is saved, the real save is never read or written), walks the main screens and a
// short auto battle, writes a PNG of each to the folder, prints "SBSHOTS DONE <n>" and quits.
// Add -sbShotsScenes for the campaign scene tour as well (SceneTour: every chapter's stops 1, 3 and 6 in battle
// and its lair reveal, plus two Hard lairs by night). It adds several minutes on software GL, so the per-commit CI
// smoke leaves it off; the workflow's "scenes" input (workflow_dispatch) turns it on.
// Used by tools/unity-shots.sh on the Linux preview build (see docs/sigilbound/PLAN.md, step 3).
using System;
using System.Collections;
using System.Linq;
using System.IO;
using Sigilbound.Core;
using UnityEngine;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        public static bool NoSave;   // set by the screenshot mode: Save() does nothing
        int _shotCount;
        string _shotDir;

        static string ShotsArg()
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].StartsWith("-sbShots=", StringComparison.Ordinal)) return args[i].Substring(9);
                if (args[i] == "-sbShots" && i + 1 < args.Length) return args[i + 1];
            }
            return null;
        }

        // Called at the end of App.Awake (which certainly runs): a separate RuntimeInitializeOnLoad
        // hook never fired in the Linux preview build. Progress also goes to <dir>/shots.log, so the
        // tour can be followed without relying on the player log.
        void StartShotsIfAsked()
        {
            var dir = ShotsArg();
            if (string.IsNullOrEmpty(dir)) return;
            NoSave = true;
            // Under xvfb there is no window manager, so the window never gets focus and the Linux
            // player sits in its paused loop (frames frozen, coroutines stopped). Keep running.
            Application.runInBackground = true;
            try { Directory.CreateDirectory(dir); File.WriteAllText(Path.Combine(dir, "shots.log"), $"started {DateTime.Now:O}\n"); } catch (Exception) { }
            StartCoroutine(RunShots(dir));
        }

        static bool ScenesArg() => Environment.GetCommandLineArgs().Any(a => a == "-sbShotsScenes");

        void ShotLog(string line)
        {
            Debug.Log("SBSHOTS " + line);
            try { File.AppendAllText(Path.Combine(_shotDir, "shots.log"), line + "\n"); } catch (Exception) { }
        }

        const int ClassicChaptersForShots = GameData.ClassicChapters;

        IEnumerator RunShots(string dir)
        {
            _shotDir = dir;
            Directory.CreateDirectory(dir);
            ShotLog($"start {UnityEngine.Screen.width}x{UnityEngine.Screen.height} -> {Path.GetFullPath(dir)}");
            // Fresh game, beginner tips held back so they don't cover every screen (one is shot on purpose).
            G = new Game(new SaveData());
            G.EnsureDaily();
            _tipAt = float.MaxValue;
            CloseModal();
            Go(ScreenId.Intro);
            yield return new WaitForSecondsRealtime(12f); // models (slow on software GL), arena and fonts settle
            yield return Shot("intro");

            G.StartWith(GameData.Starters[0]);
            Go(ScreenId.Home);
            Modal("Welcome!", 230, b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 90), "Screenshot mode: a sample modal with body text and a button.", 15, Palette.Text, FontStyle.Normal, TextAnchor.UpperCenter, true);
                Ui.Button(new Rect(b.x, b.yMax - 48, b.width, 46), "Let's go");
            });
            yield return Shot("modal");
            CloseModal();

            foreach (var s in new[] { ScreenId.Home, ScreenId.Modes, ScreenId.Campaign, ScreenId.Heroes, ScreenId.Summon, ScreenId.Trials, ScreenId.Achievements })
            {
                Go(s);
                yield return Shot(s.ToString().ToLowerInvariant());
            }
            // Sub-screens the UI/UX consistency review could only read from code: hero detail, equip, the Titan,
            // dungeons, the arena and its team setup (the arena opens at stage 12: pretend for these shots).
            if (G.S.heroes.Count > 0)
            {
                _heroUid = G.S.heroes[0].uid; Go(ScreenId.Hero);
                yield return Shot("hero");
                _slot = GameData.Slot.Weapon; Go(ScreenId.Equip);
                yield return Shot("equip");
            }
            int keepTrials = G.S.cleared; G.S.cleared = System.Math.Max(G.S.cleared, Trials.ArenaUnlock);
            foreach (var s in new[] { ScreenId.Titan, ScreenId.Dungeon, ScreenId.ArenaPvp })
            {
                Go(ScreenId.Trials); Go(s);
                yield return Shot(s == ScreenId.ArenaPvp ? "arena" : s.ToString().ToLowerInvariant());
            }
            var shotRivals = G.CurrentRivals();
            if (shotRivals.Count > 0) { OpenArenaPrep(shotRivals[0]); yield return Shot("prep-arena"); }
            G.S.cleared = keepTrials;
            // The campaign map with chapter 1's stage list open (stop names, enemy chips, the gear chance).
            Go(ScreenId.Campaign); _chapterOpen = 0;
            yield return Shot("campaign-stages");
            _chapterOpen = -1;
            // The Act II map (painted storm coast): opens after stage 60, so pretend act 1 is cleared for one
            // shot (nothing is saved in this mode) and skip the one-time Act II intro.
            int keepCleared = G.S.cleared; G.S.cleared = GameData.ClassicStages;
            PlayerPrefs.SetInt("sb.act2Intro", 1); _act = 2;
            yield return Shot("campaign-act2");
            G.S.cleared = keepCleared; _act = 0;
            OpenPrep(0, false, ScreenId.Campaign);
            yield return Shot("prep");
            CloseModal();
            OpenSettings();
            yield return Shot("settings");
            CloseModal();

            // A beginner tip, to check modal text.
            _tipAt = 0; Go(ScreenId.Home);
            yield return new WaitForSecondsRealtime(1.2f);
            yield return Shot("tip");
            CloseModal(); _tipAt = float.MaxValue;

            // Hero gallery (PLAN step 20): every hero, four at a time, menus hidden, after their
            // models have loaded (or 25 s), with a count of real models vs stand-ins in shots.log.
            _shotNoUi = true;
            var ids = GameData.Heroes.Where(h => h.Id != "titan").Select(h => h.Id).ToList();
            for (int b = 0; b < ids.Count; b += 4)
            {
                _shotLineup = ids.Skip(b).Take(4).ToList();
                Go(ScreenId.Home);
                yield return null; yield return null;
                float wait = Time.realtimeSinceStartup + 25f;
                while (Time.realtimeSinceStartup < wait && !_lineup.All(v => v != null && v.Hero != null)) yield return null;
                yield return new WaitForSecondsRealtime(1.5f); // let idle clips settle into a pose
                ShotLog($"gallery {string.Join(",", _shotLineup)}: {_lineup.Count(v => v != null && v.Hero != null)}/{_lineup.Count} real models");
                LogBounds();
                yield return Shot($"gallery-{b / 4 + 1}");
            }
            // Act 2 arenas (chapters 11-15): each theme behind a lineup.
            for (int c = ClassicChaptersForShots; c < GameData.Chapters.Length; c++)
            {
                Arena.Build(c, false);
                yield return new WaitForSecondsRealtime(1.5f);
                ShotLog($"arena {GameData.Chapters[c]}");
                yield return Shot($"arena-{c + 1}");
            }
            Arena.Build(0, false);
            _shotLineup = null; _shotNoUi = false; _lineupKey = "";
            Go(ScreenId.Home);

            // A short auto battle: start, mid-fight, and the result.
            G.S.settings.auto = true; G.S.settings.speed = 1;
            Battle.Begin(0);
            yield return new WaitForSecondsRealtime(2.5f);
            yield return Shot("battle-start");
            yield return new WaitForSecondsRealtime(4f);
            yield return Shot("battle-mid");
            LogBudget("battle-mid");
            // Stage the new combat UI for one shot: a part-filled Break bar on each enemy, a x3 Poison stack
            // (badge) on the first one, a Broken icon on the second and an enraged round counter (round 12,
            // Enrage x3) (nothing is saved in this mode).
            var core = Battle.Engine;
            if (core != null)
            {
                var foes = Sigilbound.Core.Battle.Living(core.Enemies);
                foreach (var f in foes) f.Break = 68;
                if (foes.Count > 0)
                {
                    var p = foes[0].Effects.Find(e => e.Type == FxType.Poison);
                    if (p != null) p.Stacks = 3; else foes[0].Effects.Add(new Effect { Type = FxType.Poison, Turns = 3, Stacks = 3 });
                }
                if (foes.Count > 1 && !foes[1].Has(FxType.Broken)) { foes[1].Break = 0; foes[1].Effects.Add(new Effect { Type = FxType.Broken, Turns = 1 }); }
                core.SkipToRound(Sigilbound.Core.Battle.EnrageFrom + 2);
                yield return Shot("combat-ui");
            }
            G.S.settings.speed = 3; // finish the fight quickly so the results screen is photographed
            float until = Time.realtimeSinceStartup + 150f;
            while (Battle.InProgress && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("battle-result");

            // Story stops (PLAN step 85) as real fights frame them: a throwaway battle on each stage (auto off,
            // ended before anyone wins, so nothing is rewarded or saved), shot from the battle camera.
            foreach (var (c, st, label) in new[] { (1, 0, "story-mill-1"), (1, 5, "story-mill-6"), (14, 5, "story-storm-6"), (5, 5, "story-glass-6"), (6, 5, "story-crown-6"), (4, 5, "story-bell-6"), (10, 5, "story-light-6") })
            {
                CloseModal(); Go(ScreenId.Home); yield return new WaitForSecondsRealtime(0.5f);
                G.S.settings.auto = false;
                Battle.Begin(BattleSpec.ForStage(c * GameData.StagesPerChapter + st));
                yield return new WaitForSecondsRealtime(st == GameData.StagesPerChapter - 1 ? 3.2f : 2.5f); // boss stages: after the lair reveal
                ShotLog($"story {GameData.Chapters[c]} stage {st + 1}: {StoryScenes.Of(c, st)?.Name}");
                yield return Shot(label);
                LogBudget(label);
                Battle.End(ScreenId.Home);
            }
            G.S.settings.auto = true;

            // The lair reveal (boss stages): a mid-reveal frame, then the battle view it settles into.
            // The story shots above already visited these lairs; the reveal plays once per lair per session.
            BattleController.ForgetLairReveals();
            foreach (var (c, label) in new[] { (4, "lair-reveal-1"), (14, "lair-reveal-2") })
            {
                CloseModal(); Go(ScreenId.Home); yield return new WaitForSecondsRealtime(0.5f);
                G.S.settings.auto = false;
                Battle.Begin(BattleSpec.ForStage(c * GameData.StagesPerChapter + GameData.StagesPerChapter - 1));
                yield return new WaitForSecondsRealtime(0.35f);   // Shot waits 0.8 s more: the frame lands ~1.1 s in
                yield return Shot(label);
                yield return new WaitForSecondsRealtime(1.6f);
                yield return Shot(label + "-battle");
                Battle.End(ScreenId.Home);
            }
            G.S.settings.auto = true;

            if (ScenesArg()) yield return SceneTour();

            // Creature heroes (Core/Kit/Creatures.cs): Vessa in the team, on the home lineup and in a battle
            // (wider slot, her own clips, planted feet). A throwaway hero in the throwaway save.
            {
                var keep = G.S.team.ToList();
                var vh = G.AddHero("vessa"); vh.level = 30; vh.stars = 5;
                G.S.team.Clear(); G.S.team.AddRange(new[] { keep[0], vh.uid }.Concat(keep.Skip(1)).Take(4));
                CloseModal(); _lineupKey = ""; Go(ScreenId.Home); yield return new WaitForSecondsRealtime(3f);
                yield return Shot("creature-home");
                G.S.settings.auto = true; G.S.settings.speed = 1;
                Battle.Begin(BattleSpec.ForStage(12 * GameData.StagesPerChapter + 1));
                yield return new WaitForSecondsRealtime(2.5f);
                yield return Shot("creature-lineup");
                LogBudget("creature-lineup");
                yield return new WaitForSecondsRealtime(4f);
                yield return Shot("creature-battle");
                Battle.End(ScreenId.Home);
                G.S.team.Clear(); G.S.team.AddRange(keep); _lineupKey = "";
            }

            // A Titan practice fight (no rewards, nothing saved): the arrival intro, then the worm's-eye view
            // every few seconds, so Titan framing and cinematics can be checked without a phone.
            CloseModal(); Go(ScreenId.Home); yield return new WaitForSecondsRealtime(1f);
            G.S.settings.auto = true; G.S.settings.speed = 1;
            Battle.Begin(BattleSpec.TitanPractice());
            yield return new WaitForSecondsRealtime(1.6f);
            yield return Shot("titan-intro"); LogTitanBounds("titan-intro");
            yield return new WaitForSecondsRealtime(4f);
            for (int i = 1; i <= 6 && Battle.InProgress; i++) { yield return Shot($"titan-{i}"); LogBudget($"titan-{i}"); LogTitanBounds($"titan-{i}"); yield return new WaitForSecondsRealtime(3.2f); }

            ShotLog($"DONE {_shotCount}");
            Application.Quit(0);
        }

        // The campaign scenes (CAMPAIGN_SCENES_PLAN.md, M3), with -sbShotsScenes: for every chapter, stops 1, 3 and 6 as a
        // real fight frames them (throwaway battles, auto off, ended before anyone wins: nothing rewarded or saved), and on
        // stop 6 the lair reveal's middle key first, then the tilted boss view it settles into. Two Hard lairs (night)
        // check the tilt and the lanterns after dark. Each battle frame logs its camera pitch and SBBUDGET line.
        IEnumerator SceneTour()
        {
            ShotLog("scenes on");
            BattleController.ForgetLairReveals();
            G.S.settings.auto = false; G.S.settings.speed = 1;
            // Throwaway save: the team at level 60 so a late chapter's foes don't win before the frame (build 182 shot three
            // defeat screens), and every stage and Hard stage open (Begin refuses a locked Hard stage).
            var keepLv = G.S.team.Select(uid => G.HeroByUid(uid)).Where(h => h != null).Select(h => (h, h.level)).ToList();
            foreach (var (h, _) in keepLv) h.level = 60;
            int keepCleared = G.S.cleared, keepHard = G.S.hardCleared;
            G.S.cleared = GameData.TotalStages; G.S.hardCleared = GameData.TotalStages - 1;
            var stops = new System.Collections.Generic.List<(int c, int st, bool hard)>();
            for (int c = 0; c < GameData.Chapters.Length; c++) foreach (int st in new[] { 0, 2, GameData.StagesPerChapter - 1 }) stops.Add((c, st, false));
            stops.Add((1, GameData.StagesPerChapter - 1, true)); stops.Add((GameData.Chapters.Length - 1, GameData.StagesPerChapter - 1, true));
            foreach (var (c, st, hard) in stops)
            {
                CloseModal(); Go(ScreenId.Home); yield return new WaitForSecondsRealtime(0.4f);
                int idx = c * GameData.StagesPerChapter + st;
                bool boss = st == GameData.StagesPerChapter - 1;
                string tag = $"scene-{c + 1:00}-{st + 1}{(hard ? "H" : "")}";
                Battle.Begin(hard ? BattleSpec.ForHard(idx) : BattleSpec.ForStage(idx));
                if (hard && !G.HardOpen(idx)) ShotLog($"scene {tag}: Hard stage still locked");
                if (boss && !hard)
                {
                    yield return new WaitForSecondsRealtime(0.35f);   // Shot waits 0.8 s more: the reveal's middle key (~1.1 s in)
                    yield return Shot(tag + "-reveal");
                    yield return new WaitForSecondsRealtime(2.2f);    // the reveal ends at 2.5 s and blends into the battle view
                }
                else yield return new WaitForSecondsRealtime(2.5f);
                var fwd = Arena.Cam.transform.forward;
                ShotLog($"scene {GameData.Chapters[c]} stage {st + 1}{(hard ? " hard" : "")}: {StoryScenes.Of(c, st)?.Name} cam pitch {Mathf.Asin(Mathf.Clamp(-fwd.y, -1f, 1f)) * Mathf.Rad2Deg:0.0} tilt {(Arena.BossTilt ? "on" : "off")} landmark {Arena.Layout?.Landmark?.Mesh}");
                yield return Shot(tag);
                LogBudget(tag);
                Battle.End(ScreenId.Home);
            }
            foreach (var (h, lv) in keepLv) h.level = lv;
            G.S.cleared = keepCleared; G.S.hardCleared = keepHard;
            G.S.settings.auto = true;
        }

        // Scene budget probe (PLAN step 81): what the current frame asks of a phone. Logged as
        // "SBBUDGET <shot> tier=.. renderers=.. tris=.. skinned=.. particles=.. systems=.. lights=.. audio=..".
        // The renderer's own triangles. A static-batched renderer's MeshFilter holds the arena's whole combined
        // mesh; only its submesh range (subMeshStartIndex, one per material) is its own. Counting the whole
        // combined mesh for every batched prop inflated the build-138 numbers several times over.
        static long Tris(Renderer r, Mesh m)
        {
            if (m == null) return 0;
            int first = 0, count = m.subMeshCount;
            if (r is MeshRenderer mr && r.isPartOfStaticBatch) { first = mr.subMeshStartIndex; count = Mathf.Min(r.sharedMaterials.Length, m.subMeshCount - first); }
            long t = 0;
            for (int i = first; i < first + count; i++) t += m.GetIndexCount(i) / 3; // works on non-readable meshes too
            return t;
        }

        void LogBudget(string name)
        {
            int renderers = 0, skinned = 0, particles = 0, systems = 0, lights = 0, audio = 0; long tris = 0;
            long heroes = 0, scenery = 0, other = 0;
            var by = new System.Collections.Generic.Dictionary<string, (int n, long t)>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || !r.isVisible) continue;
                renderers++;
                Mesh m = null;
                if (r is SkinnedMeshRenderer smr) { skinned++; m = smr.sharedMesh; }
                else if (r is MeshRenderer) { var f = r.GetComponent<MeshFilter>(); if (f != null) m = f.sharedMesh; }
                long t = Tris(r, m);
                tris += t;
                if (t == 0) continue;
                // Breakdown: heroes (anything under a CharacterView), the arena scenery, everything else;
                // and the heaviest names (the prop's object name for scenery, else the mesh name).
                bool hero = r.GetComponentInParent<CharacterView>() != null, scen = !hero && r.transform.root.name == "Scenery";
                if (hero) heroes += t; else if (scen) scenery += t; else other += t;
                string key = (hero ? "hero:" : scen ? "" : "other:") + (scen || m == null ? r.gameObject.name : m.name);
                by[key] = by.TryGetValue(key, out var e) ? (e.n + 1, e.t + t) : (1, t);
            }
            ShotLog($"SBBUDGET-SPLIT {name} heroes={heroes} scenery={scenery} other={other}");
            ShotLog($"SBBUDGET-TOP {name} " + string.Join(", ", by.OrderByDescending(kv => kv.Value.t).Take(14).Select(kv => $"{kv.Key} x{kv.Value.n}={kv.Value.t}")));
            foreach (var ps in FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)) if (ps.particleCount > 0) { systems++; particles += ps.particleCount; }
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) if (l.enabled && l.gameObject.activeInHierarchy) lights++;
            foreach (var a in FindObjectsByType<AudioSource>(FindObjectsSortMode.None)) if (a.isPlaying) audio++;
            ShotLog($"SBBUDGET {name} tier={QualitySettings.names[QualitySettings.GetQualityLevel()]} renderers={renderers} tris={tris} skinned={skinned} particles={particles} systems={systems} lights={lights} audio={audio}");
        }

        // Pose sanity per gallery hero (in-house report on build 180: Tansy smeared into a sheet with her head on
        // the ground, Corran's body flung into the sky, while "4/4 real models" and skinned>0 still passed).
        // Logged as "SBBOUNDS <id> head=.. top=.. wide=.. ok" or "... BAD <why>"; tools/unity-smoke-check.sh
        // fails on any BAD line. Measured from the figure's feet, in units of its own height marker
        // (HeroModel.Height, about 1.25x the standing height): the Head bone stands between 0.25 and 1.1 of it
        // above the feet and within 0.6 of it sideways, and the skinned meshes' bounds (bone-driven,
        // updateWhenOffscreen) reach no higher than 1.8 of it and stay under 2.8 wide (long polearms and wings
        // included). Calibrated on run 186: every sane hero had head 0.42-0.87, top up to 1.44 (Crusk's horns),
        // wide up to 1.77; the full bounds height is not checked (golems' bounds reach below their feet).
        void LogBounds()
        {
            foreach (var v in _lineup)
            {
                if (v == null || v.Hero == null || v.Hero.Root == null || v.Unit == null) continue;
                var id = v.Unit.Template.Id;
                if (Creatures.Of(id) != null) continue;   // creature heroes have their own body plan
                var root = v.Hero.Root.transform; var feet = root.position;
                float H = Mathf.Max(0.1f, v.Hero.Height * root.lossyScale.y / Mathf.Max(1e-4f, root.localScale.y));
                bool any = false; var bb = new Bounds(feet, Vector3.zero);
                foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    if (!r.enabled) continue;
                    if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
                }
                if (!any) continue;
                var head = BodyShaper.Find(root, "Head");
                float hy = head != null ? (head.position.y - feet.y) / H : 0.6f;
                float hx = head != null ? new Vector2(head.position.x - feet.x, head.position.z - feet.z).magnitude / H : 0f;
                float top = (bb.max.y - feet.y) / H, tall = bb.size.y / H, wide = Mathf.Max(bb.size.x, bb.size.z) / H;
                string why = hy < 0.25f ? "head on the ground" : hy > 1.1f ? "head too high" : hx > 0.6f ? "head off the body"
                           : top > 1.8f ? "stretched up" : wide > 2.8f ? "stretched sideways" : null;
                ShotLog($"SBBOUNDS {id} head={hy:0.00}/{hx:0.00} top={top:0.00} tall={tall:0.00} wide={wide:0.00} " + (why == null ? "ok" : "BAD " + why));
            }
        }

        // The Titan's pose in each Titan shot (in-house report on build 190: its limbs stretched into sheets and it
        // hovered spread-eagled while skinned>0 still passed). "SBBOUNDS titan-<shot> ..." in units of its head height
        // (TitanSpec.Head), measured from the figure's feet (it flies with them in a leap). Its own clips, baked and
        // measured in titan_moves.py, keep the head 0.59-1.08 high and within 0.47 sideways, the mesh top under 1.74
        // and its width under 1.85; the bind (T) pose is 2.65 wide. BAD past head 0.4-1.3 / 0.8, top 2.3 or wide 2.6.
        void LogTitanBounds(string shot)
        {
            var v = Battle != null ? Battle.TitanView : null;
            if (v == null || v.Hero == null || v.Hero.Root == null) { ShotLog($"SBBOUNDS {shot} BAD no Titan figure"); return; }
            var root = v.Hero.Root.transform; var feet = v.transform.position;
            float H = Mathf.Max(0.5f, (float)TitanSpec.Current.Head);
            bool any = false; var bb = new Bounds(feet, Vector3.zero);
            foreach (var r in root.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (!r.enabled) continue;
                if (!any) { bb = r.bounds; any = true; } else bb.Encapsulate(r.bounds);
            }
            if (!any) { ShotLog($"SBBOUNDS {shot} BAD no skinned mesh"); return; }
            var head = BodyShaper.Find(root, TitanSpec.Current.HeadBone);
            float hy = head != null ? (head.position.y - feet.y) / H : 0.8f;
            float hx = head != null ? new Vector2(head.position.x - feet.x, head.position.z - feet.z).magnitude / H : 0f;
            float top = (bb.max.y - feet.y) / H, wide = Mathf.Max(bb.size.x, bb.size.z) / H;
            string why = hy < 0.4f ? "head on the ground" : hy > 1.3f ? "head too high" : hx > 0.8f ? "head off the body"
                       : top > 2.3f ? "stretched up" : wide > 2.6f ? "stretched sideways or bind pose" : null;
            ShotLog($"SBBOUNDS {shot} head={hy:0.00}/{hx:0.00} top={top:0.00} wide={wide:0.00} " + (why == null ? "ok" : "BAD " + why));
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForSecondsRealtime(0.8f);
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                _shotCount++;
                var path = Path.Combine(_shotDir, $"{_shotCount:00}-{name}.png");
                File.WriteAllBytes(path, tex.EncodeToPNG());
                ShotLog("saved " + path);
            }
            finally { Destroy(tex); }
        }
    }
}
