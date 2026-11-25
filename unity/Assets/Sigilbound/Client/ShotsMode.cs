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
            float until = Time.realtimeSinceStartup + 90f;
            while (Battle.InProgress && Time.realtimeSinceStartup < until) yield return null;
            yield return new WaitForSecondsRealtime(2f);
            yield return Shot("battle-result");

            ShotLog($"DONE {_shotCount}");
            Application.Quit(0);
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
