// In-game update check, like the web app's update.js and the Godot demo's Updater.
// On launch, every 5 minutes while the app is in front, and whenever it comes back to the
// foreground, it reads the "unity-latest" GitHub release. When that release holds a newer build
// for THIS platform (Sigilbound-Unity-<ver>-build<N>.apk on Android, Sigilbound-Windows-<ver>-
// build<N>.zip on Windows), a small banner with a Download button appears at the top of the
// screen, without a restart. It starts itself (no scene setup) and draws its own banner, so it
// works on every screen. Disabled in the editor and in local builds without a build number.
using System.Collections;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Sigilbound.Client
{
    public sealed class Updater : MonoBehaviour
    {
        const string ReleaseApi = "https://api.github.com/repos/eqtitan/SigilBound/releases/tags/unity-latest";
        const string Download = "https://github.com/eqtitan/SigilBound/releases/download/unity-latest/";
        const float PollSeconds = 300f, MinGapSeconds = 120f;

        public static int MyBuild { get; private set; }
        public static int LatestBuild { get; private set; }
        public static string LatestVersion { get; private set; } = "";

        float _lastCheck = -999f;
        bool _busy, _dismissed;
        GUIStyle _box, _title, _sub, _btn, _close;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
#if UNITY_EDITOR
            return;
#else
            var info = Resources.Load<TextAsset>("BuildInfo"); // written by BuildScript at build time
            if (info == null || !int.TryParse(info.text.Trim(), out var n) || n <= 0) return;
            if (Application.platform != RuntimePlatform.Android && Application.platform != RuntimePlatform.WindowsPlayer) return;
            MyBuild = n;
            var go = new GameObject("Sigilbound Updater");
            DontDestroyOnLoad(go);
            go.AddComponent<Updater>();
#endif
        }

        static string AssetPattern => Application.platform == RuntimePlatform.Android
            ? @"""name""\s*:\s*""Sigilbound-Unity-([0-9.]+)-build(\d+)\.apk"""
            : @"""name""\s*:\s*""Sigilbound-Windows-([0-9.]+)-build(\d+)\.zip""";

        static string FileUrl => Download + (Application.platform == RuntimePlatform.Android ? "Sigilbound-Unity.apk" : "Sigilbound-Windows.zip");

        void Start()
        {
            StartCoroutine(Loop());
        }

        IEnumerator Loop()
        {
            yield return new WaitForSecondsRealtime(3f);
            while (true)
            {
                Check(true);
                yield return new WaitForSecondsRealtime(PollSeconds);
            }
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) Check(false);
        }

        void Check(bool force)
        {
            if (_busy) return;
            if (!force && Time.realtimeSinceStartup - _lastCheck < MinGapSeconds) return;
            _lastCheck = Time.realtimeSinceStartup;
            StartCoroutine(Fetch());
        }

        IEnumerator Fetch()
        {
            _busy = true;
            using (var req = UnityWebRequest.Get(ReleaseApi))
            {
                req.SetRequestHeader("Accept", "application/vnd.github+json");
                req.SetRequestHeader("User-Agent", "Sigilbound-Unity");
                req.timeout = 15;
                yield return req.SendWebRequest();
                _busy = false;
                if (req.result != UnityWebRequest.Result.Success) yield break; // offline or rate-limited: try later
                int best = 0; string ver = "";
                foreach (Match m in Regex.Matches(req.downloadHandler.text, AssetPattern))
                    if (int.TryParse(m.Groups[2].Value, out var b) && b > best) { best = b; ver = m.Groups[1].Value; }
                if (best > MyBuild)
                {
                    if (best != LatestBuild) _dismissed = false; // a newer build than the one dismissed
                    LatestBuild = best; LatestVersion = ver;
                }
                else LatestBuild = 0;
            }
        }

        void OnGUI()
        {
            if (LatestBuild <= 0 || _dismissed) return;
            GUI.depth = -1000; // above every screen
            EnsureStyles();
            float k = Mathf.Max(1f, Screen.dpi > 0 ? Screen.dpi / 160f : 1f);
            var safe = Screen.safeArea;
            float h = 64 * k, pad = 10 * k;
            float w = Mathf.Min(Screen.width - 2 * pad, 560 * k);
            var r = new Rect((Screen.width - w) / 2f, Screen.height - safe.yMax + pad, w, h);
            GUI.Box(r, GUIContent.none, _box);
            _title.fontSize = Mathf.RoundToInt(17 * k); _sub.fontSize = Mathf.RoundToInt(13 * k);
            _btn.fontSize = Mathf.RoundToInt(15 * k); _close.fontSize = Mathf.RoundToInt(16 * k);
            float closeW = 36 * k, btnW = 120 * k;
            GUI.Label(new Rect(r.x + pad, r.y + 8 * k, w - btnW - closeW - 3 * pad, 24 * k), "Update available", _title);
            GUI.Label(new Rect(r.x + pad, r.y + 34 * k, w - btnW - closeW - 3 * pad, 20 * k),
                _installing != null ? _installing : $"v{LatestVersion} · build {LatestBuild} (you have build {MyBuild})", _sub);
            var btn = new Rect(r.xMax - btnW - closeW - pad, r.y + 12 * k, btnW, h - 24 * k);
            if (_installing != null)
            {
                // Download progress bar in place of the button.
                GUI.Box(btn, GUIContent.none, _box);
                GUI.DrawTexture(new Rect(btn.x, btn.y, btn.width * Mathf.Clamp01(_progress), btn.height), _btn.normal.background);
            }
            else if (GUI.Button(btn, Application.platform == RuntimePlatform.WindowsPlayer ? "Update now" : "Download", _btn))
            {
                if (Application.platform == RuntimePlatform.WindowsPlayer) StartCoroutine(UpdateWindows());
                else Application.OpenURL(FileUrl);
            }
            var close = new Rect(r.xMax - closeW - pad * 0.5f, r.y + 14 * k, closeW, h - 28 * k);
            if (GUI.Button(close, "", _close)) _dismissed = true;
            float ic = Mathf.Min(close.width, close.height) * 0.5f;
            Icons.Draw(new Rect(close.center.x - ic / 2, close.center.y - ic / 2, ic, ic), Icon.Close, Palette.Dim);
        }

        // ---------- Windows: update in place ----------
        // Downloads the new zip, then hands over to a hidden PowerShell helper that waits for the
        // game to close, unpacks the zip over this install folder (no second copy kept), deletes the
        // download and starts the new build. Progress is saved in PlayerPrefs (the Windows
        // registry), not in the game folder, so it carries over. Any failure falls back to the link.
        string _installing; float _progress;

        IEnumerator UpdateWindows()
        {
            _installing = "Downloading the update…"; _progress = 0;
            string work = Path.Combine(Path.GetTempPath(), "sigilbound-update");
            string zip = Path.Combine(work, "Sigilbound-Windows.zip");
            try { if (Directory.Exists(work)) Directory.Delete(work, true); Directory.CreateDirectory(work); }
            catch (System.Exception) { Fallback(); yield break; }
            using (var req = new UnityWebRequest(FileUrl, UnityWebRequest.kHttpVerbGET))
            {
                req.downloadHandler = new DownloadHandlerFile(zip) { removeFileOnAbort = true };
                req.SetRequestHeader("User-Agent", "Sigilbound-Unity");
                var op = req.SendWebRequest();
                while (!op.isDone) { _progress = req.downloadProgress; yield return null; }
                if (req.result != UnityWebRequest.Result.Success) { Fallback(); yield break; }
            }
            _progress = 1; _installing = "Installing… the game will restart.";
            yield return null;
            try
            {
                string dir = Path.GetDirectoryName(Application.dataPath); // the folder holding Sigilbound.exe
                string exe = Path.Combine(dir, "Sigilbound.exe");
                if (!File.Exists(exe)) exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                string Q(string v) => "'" + v.Replace("'", "''") + "'";
                string ps1 = Path.Combine(Path.GetTempPath(), "sigilbound-update.ps1");
                File.WriteAllText(ps1,
                    "$ErrorActionPreference = 'SilentlyContinue'\r\n" +
                    $"Wait-Process -Id {System.Diagnostics.Process.GetCurrentProcess().Id} -Timeout 60\r\n" +
                    $"$x = Join-Path {Q(work)} 'x'\r\n" +
                    $"Expand-Archive -LiteralPath {Q(zip)} -DestinationPath $x -Force\r\n" +
                    "$src = Get-ChildItem -LiteralPath $x -Directory | Select-Object -First 1\r\n" +
                    "if ($src -and (Test-Path (Join-Path $src.FullName 'Sigilbound.exe'))) {\r\n" +
                    $"  robocopy $src.FullName {Q(dir)} /E /R:5 /W:1 /NFL /NDL /NJH /NJS /NP | Out-Null\r\n" +
                    "}\r\n" +
                    $"Remove-Item -LiteralPath {Q(work)} -Recurse -Force\r\n" +
                    $"Start-Process -FilePath {Q(exe)}\r\n");
                var psi = new System.Diagnostics.ProcessStartInfo("powershell.exe", $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1}\"")
                { UseShellExecute = false, CreateNoWindow = true };
                System.Diagnostics.Process.Start(psi);
            }
            catch (System.Exception e) { Debug.LogWarning("Sigilbound: in-place update failed: " + e.Message); Fallback(); yield break; }
            Application.Quit();
        }

        void Fallback()
        {
            _installing = null;
            Application.OpenURL(FileUrl);
        }

        void EnsureStyles()
        {
            if (_box != null) return;
            _box = new GUIStyle { normal = { background = Solid(new Color(0.11f, 0.09f, 0.14f, 0.96f)) }, border = new RectOffset(2, 2, 2, 2) };
            _title = new GUIStyle { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = Palette.Gold } };
            _sub = new GUIStyle { alignment = TextAnchor.MiddleLeft, clipping = TextClipping.Clip, normal = { textColor = Palette.Dim } };
            _btn = new GUIStyle
            {
                fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Clip,
                normal = { background = Solid(Palette.Gold), textColor = new Color(0.16f, 0.1f, 0.02f) },
                active = { background = Solid(Palette.GoldDark), textColor = new Color(0.16f, 0.1f, 0.02f) },
            };
            _close = new GUIStyle { alignment = TextAnchor.MiddleCenter, normal = { textColor = Palette.Dim } };
        }

        static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            t.SetPixel(0, 0, c); t.Apply();
            return t;
        }
    }
}
