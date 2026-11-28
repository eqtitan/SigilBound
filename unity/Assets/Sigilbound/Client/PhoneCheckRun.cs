// Settings > Run phone check: plays the fixed 60-second sequence in Core/PhoneCheck.cs (Home, the team screen
// with the storm-coast lair's enemies preloading, that battle at the current quality, the Titan's arrival,
// back to Home) and measures every frame. Then it shows a summary, copies a compact report, keeps it on this
// device and adds it to Send feedback.
// The run plays on a throwaway copy of the save with a fixed seed and nothing is saved while it runs, so it
// never grants rewards, uses a Titan try or touches progress; the real game comes back when it ends.
using System;
using System.Collections;
using System.IO;
using Sigilbound.Core;
using UnityEngine;
using UnityEngine.Profiling;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        const string PhoneCheckKey = "sb.phoneCheck";
        public static string LastPhoneCheck
        {
            get { try { return PlayerPrefs.GetString(PhoneCheckKey, ""); } catch (Exception) { return ""; } }
            private set { PlayerPrefs.SetString(PhoneCheckKey, value ?? ""); PlayerPrefs.Save(); }
        }

        public bool PhoneCheckRunning => _check != null;
        PhoneCheckResult _check; float _checkT; bool _checkStop; string _checkPerf = ""; int _checkPerfFrame = -10; int _checkErrors;
        static readonly string[] ThermalNames = { "none", "light", "moderate", "severe", "critical", "emergency", "shutdown" };

        void StartPhoneCheck()
        {
            if (_check != null || NoSave) return;
            CloseModal();
            StartCoroutine(RunPhoneCheck());
        }

        void OnCheckLog(string msg, string stack, LogType type)
        {
            if (msg != null && msg.StartsWith("SBPERF", StringComparison.Ordinal)) { _checkPerf = msg.Substring(7); _checkPerfFrame = Time.frameCount; }
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) _checkErrors++;
        }

        IEnumerator RunPhoneCheck()
        {
            var real = G; float realTip = _tipAt;
            var r = new PhoneCheckResult();
            _check = r; _checkT = 0; _checkStop = false; _checkErrors = 0; _checkPerf = ""; _checkPerfFrame = -10;
            FillDevice(r);
            r.BatteryStart = Battery(); r.ThermalStart = Thermal(); r.TierStart = Quality.I != null ? Quality.I.TierName : "";
            Application.logMessageReceived += OnCheckLog;
            NoSave = true; _tipAt = float.MaxValue;
            Debug.Log("SBCHECK start");
            try
            {
                // The throwaway copy: same heroes, team and settings, a fixed seed, auto battle at 1x, no replay or climb.
                var copy = SaveCodec.FromCode(SaveCodec.ToCode(real.S)) ?? new SaveData();
                G = new Game(copy, seed: 20261128);
                G.EnsureDaily();
                if (!G.S.started || G.Team.Count == 0) G.StartWith(GameData.Starters[0]);
                G.S.settings.auto = true; G.S.settings.speed = 1; G.S.settings.autoReplay = false; AutoClimb.SetClimb(G.S.settings, false);
                _lineupKey = "";

                int step = -1; float memNext = 0;
                while (_checkT < PhoneCheck.Duration && !_checkStop)
                {
                    int s = PhoneCheck.StepIndexAt(_checkT);
                    if (s != step) { step = s; EnterCheckStep(s); }
                    yield return null;
                    if (_checkStop) break;
                    float dt = Time.unscaledDeltaTime;
                    _checkT += dt;
                    // An SBPERF line logged in this frame or the one before is the likely cause of a long frame.
                    r.AddFrame(_checkT, dt * 1000f, Time.frameCount - _checkPerfFrame <= 1 ? _checkPerf : null);
                    if (_checkT >= memNext) { memNext = _checkT + 1f; SampleMemory(r); }
                }
                r.Completed = !_checkStop; r.Seconds = Mathf.Min(_checkT, PhoneCheck.Duration);
            }
            finally
            {
                Application.logMessageReceived -= OnCheckLog;
                if (Battle.InProgress || Screen == ScreenId.Battle) Battle.End(ScreenId.Home);
                G = real; NoSave = false; _tipAt = realTip; _lineupKey = "";
                CloseModal();
                Go(ScreenId.Home);
                Arena.Build(Mathf.Min(GameData.Chapters.Length - 1, G.S.cleared / GameData.StagesPerChapter), false);
                Arena.FrameMenu();
                _check = null;
            }
            // After the measured minute (these calls can take a few ms): Android's PSS, the battery and heat.
            r.PssEndMb = AndroidPssMb();
            r.BatteryEnd = Battery(); r.BatteryState = BatteryState(); r.ThermalEnd = Thermal(); r.TierEnd = Quality.I != null ? Quality.I.TierName : "";
            r.Errors = _checkErrors;
            var report = r.Report();
            LastPhoneCheck = report;
            GUIUtility.systemCopyBuffer = report;
            try { File.WriteAllText(Path.Combine(Application.persistentDataPath, "phone-check.txt"), report); } catch (Exception) { }
            Debug.Log("SBCHECK done\n" + report);
            ShowPhoneCheckSummary(r, report);
        }

        // Each step starts the same way every run (PhoneCheck.Steps).
        void EnterCheckStep(int s)
        {
            switch (s < PhoneCheck.Steps.Length ? PhoneCheck.Steps[s].Name : "done")
            {
                case "home": CloseModal(); Go(ScreenId.Home); break;
                case "team": OpenPrep(PhoneCheck.HeavyStage, false, ScreenId.Home); break; // preloads the stage's enemies
                case "battle": Battle.Begin(BattleSpec.ForStage(PhoneCheck.HeavyStage)); break;
                case "titan": if (Battle.InProgress || Screen == ScreenId.Battle) Battle.End(ScreenId.Home); Battle.Begin(BattleSpec.TitanPractice()); break;
                case "back": if (Battle.InProgress || Screen == ScreenId.Battle) Battle.End(ScreenId.Home); else Go(ScreenId.Home); break;
            }
        }

        void SampleMemory(PhoneCheckResult r)
        {
            const double Mb = 1024.0 * 1024.0;
            r.AddMemory(Profiler.GetTotalAllocatedMemoryLong() / Mb, Profiler.GetTotalReservedMemoryLong() / Mb, RssMb());
        }

        // The process's resident memory from /proc (Android and Linux; cheap, no JNI). 0 where there is none.
        static double RssMb()
        {
            try
            {
                if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.LinuxPlayer) return 0;
                foreach (var line in File.ReadAllLines("/proc/self/status"))
                    if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
                    {
                        var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length >= 2 && long.TryParse(parts[1], out var kb)) return kb / 1024.0;
                    }
            }
            catch (Exception) { }
            return 0;
        }

        // Android's own figure for the app's memory (what the system judges it by): android.os.Debug.getPss(), in KB.
        static double AndroidPssMb()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var dbg = new AndroidJavaClass("android.os.Debug")) return dbg.CallStatic<long>("getPss") / 1024.0; }
            catch (Exception) { return 0; }
#else
            return 0;
#endif
        }

        static int Battery() { float b = SystemInfo.batteryLevel; return b < 0 ? -1 : Mathf.RoundToInt(b * 100); }
        static string BatteryState() => SystemInfo.batteryStatus switch
        {
            UnityEngine.BatteryStatus.Charging => "charging", UnityEngine.BatteryStatus.Discharging => "discharging",
            UnityEngine.BatteryStatus.Full => "full", UnityEngine.BatteryStatus.NotCharging => "not charging", _ => "",
        };
        static string Thermal() { var q = Quality.I; return q == null || q.ThermalStatus < 0 ? "n/a" : ThermalNames[Mathf.Clamp(q.ThermalStatus, 0, 6)]; }

        static void FillDevice(PhoneCheckResult r)
        {
            var q = Quality.I;
            r.Build = (Updater.MyBuild > 0 ? Updater.MyBuild.ToString() : "dev") + $" ({Application.version})";
            r.When = DateTime.Now.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture);
            r.Device = SystemInfo.deviceModel; r.Os = SystemInfo.operatingSystem;
            r.Gpu = SystemInfo.graphicsDeviceName; r.GraphicsApi = SystemInfo.graphicsDeviceType.ToString();
            r.RamMb = SystemInfo.systemMemorySize; r.Cores = SystemInfo.processorCount;
            r.Screen = $"{UnityEngine.Screen.width}x{UnityEngine.Screen.height} {Mathf.RoundToInt(UnityEngine.Screen.dpi)} dpi";
            r.DisplayHz = Quality.DisplayHz;
            r.Quality = q == null ? "?" : q.TierName + (q.Choice == "auto" ? " (auto)" : "") + " · fps " + q.FpsMode;
            r.TargetFps = q != null ? q.TargetFps : Application.targetFrameRate;
        }

        void ShowPhoneCheckSummary(PhoneCheckResult r, string report)
        {
            Vector2 scroll = Vector2.zero;
            Modal(r.Completed ? "Phone check done" : "Phone check stopped", Mathf.Min(Ui.Safe.height - 40, 520), b =>
            {
                Ui.Text(new Rect(b.x, b.y, b.width, 24), r.Headline(), 14, Palette.TrimHi, FontStyle.Bold, TextAnchor.MiddleCenter, true);
                Ui.Text(new Rect(b.x, b.y + 26, b.width, 34), "The report is copied. Paste it in a message to the developer; it's also added to Send feedback.", 12, Palette.Dim, FontStyle.Normal, TextAnchor.UpperCenter, true);
                var view = new Rect(b.x, b.y + 64, b.width, b.height - 64 - 54);
                GUI.Box(view, GUIContent.none);
                scroll = GUI.BeginScrollView(view, scroll, new Rect(0, 0, view.width - 18, Mathf.Max(view.height, 16 * (report.Split('\n').Length + 1))));
                Ui.Text(new Rect(6, 4, view.width - 26, 16 * (report.Split('\n').Length + 1)), report, 11, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                GUI.EndScrollView();
                float bw = (b.width - 10) / 2;
                if (Ui.Button(new Rect(b.x, b.yMax - 46, bw, 44), "Copy again", Ui.Kind.Secondary)) { GUIUtility.systemCopyBuffer = report; Toast("Report copied"); }
                if (Ui.Button(new Rect(b.x + bw + 10, b.yMax - 46, bw, 44), "Done")) CloseModal();
            });
        }

        // A bar across the top while the check runs: the step, the time left, and Stop.
        void DrawPhoneCheckBar()
        {
            if (_check == null) return;
            GUI.enabled = true;
            var safe = Ui.Safe;
            var bar = new Rect(safe.x + 8, safe.y + 4, Mathf.Min(safe.width - 16, 520), 40);
            bar.x = safe.center.x - bar.width / 2;
            Ui.Panel(bar, new Color(0, 0, 0, 0.8f), Palette.Gold, 10);
            int left = Mathf.Max(0, Mathf.CeilToInt(PhoneCheck.Duration - _checkT));
            Ui.Text(new Rect(bar.x + 12, bar.y, bar.width - 110, bar.height), $"Phone check · {PhoneCheck.StepAt(_checkT)} · {left} s left · don't touch", 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (Ui.Button(new Rect(bar.xMax - 92, bar.y + 4, 84, bar.height - 8), "Stop", Ui.Kind.Danger, 12)) _checkStop = true;
        }
    }
}
