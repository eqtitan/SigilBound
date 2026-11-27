// Screenshot mode, so the game can be checked visually without a phone. Started only by the
// command-line argument -sbShots=<folder> (or "-sbShots <folder>"). It starts a fresh throwaway
// game (nothing is saved, the real save is never read or written), walks the main screens and a
// short auto battle, writes a PNG of each to the folder, prints "SBSHOTS DONE <n>" and quits.
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
            // Stage the new combat UI for one shot: a part-filled Break bar on each enemy and a x3 Poison
            // stack (badge) on the first one (nothing is saved in this mode).
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
            yield return Shot("titan-intro");
            yield return new WaitForSecondsRealtime(4f);
            for (int i = 1; i <= 6 && Battle.InProgress; i++) { yield return Shot($"titan-{i}"); LogBudget($"titan-{i}"); yield return new WaitForSecondsRealtime(3.2f); }

            ShotLog($"DONE {_shotCount}");
            Application.Quit(0);
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
