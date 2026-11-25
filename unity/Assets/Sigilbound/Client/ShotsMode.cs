// Screenshot mode, so the game can be checked visually without a phone. Started only by the
// command-line argument -sbShots=<folder> (or "-sbShots <folder>"). It starts a fresh throwaway
// game (nothing is saved, the real save is never read or written), walks the main screens and a
// short auto battle, writes a PNG of each to the folder, prints "SBSHOTS DONE <n>" and quits.
// Used by tools/unity-shots.sh on the Linux preview build (see docs/sigilbound/PLAN.md, step 3).
using System;
using System.Collections;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void BootShots()
        {
            var dir = ShotsArg();
            if (string.IsNullOrEmpty(dir)) return;
            NoSave = true;
            var go = new GameObject("Sigilbound Shots");
            DontDestroyOnLoad(go);
            go.AddComponent<ShotsRunner>().Dir = dir;
        }

        sealed class ShotsRunner : MonoBehaviour
        {
            public string Dir;
            IEnumerator Start()
            {
                App app = null;
                for (int i = 0; i < 300 && app == null; i++) { app = FindAnyObjectByType<App>(); yield return null; }
                if (app == null) { Debug.LogError("SBSHOTS FAILED: no App"); Application.Quit(1); yield break; }
                yield return app.RunShots(Dir);
            }
        }

        IEnumerator RunShots(string dir)
        {
            _shotDir = dir;
            Directory.CreateDirectory(dir);
            Debug.Log($"SBSHOTS start: {Screen} {UnityEngine.Screen.width}x{UnityEngine.Screen.height} -> {Path.GetFullPath(dir)}");
            // Fresh game, beginner tips held back so they don't cover every screen (one is shot on purpose).
            G = new Game(new SaveData());
            G.EnsureDaily();
            _tipAt = float.MaxValue;
            CloseModal();
            Go(ScreenId.Intro);
            yield return new WaitForSecondsRealtime(3f); // models, arena and fonts settle
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

            Debug.Log($"SBSHOTS DONE {_shotCount}");
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
                Debug.Log("SBSHOTS saved " + path);
            }
            finally { Destroy(tex); }
        }
    }
}
