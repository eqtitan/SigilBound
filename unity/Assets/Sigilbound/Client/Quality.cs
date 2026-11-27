// Frame-rate cap and graphics quality tiers (Settings: Auto / Ultra / High / Medium / Low, like the
// web game's Graphics quality). Each tier maps to URP settings on the active pipeline asset:
// render scale, MSAA, shadow distance and resolution, plus the sun's shadows. Auto starts from a
// device guess and steps down when the frame rate can't hold the cap (and back up when it's easy),
// up to High; Ultra is manual only, as on the web.
// Frame rate (owner decision 2026-11-27, PMO D2): 60 fps by default; "high" (the screen's 90 or 120 Hz) is a
// Settings choice; auto-replay runs at 30 (ReplayCapped); Low and a warm phone force 30.
// Thermals (Android 10+): PowerManager.getCurrentThermalStatus is polled every 10 s; at MODERATE or
// worse the game drops to 30 fps and one tier with a toast, and restores after two cool minutes.
using UnityEngine;
using UnityEngine.Rendering;
#if SB_URP
using UnityEngine.Rendering.Universal;
#endif

namespace Sigilbound.Client
{
    public sealed class Quality : MonoBehaviour
    {
        public static readonly string[] Tiers = { "Low", "Medium", "High", "Ultra" };
        public static Quality I;
        public string Choice = "auto";    // auto | Ultra | High | Medium | Low (saved in settings.quality)
        public int Tier { get; private set; } = 2;
        public string FpsMode = "60";     // 60 (default) | high (the screen's rate, 90 or 120) | 30 (PlayerPrefs sb.fpsMode)
        public Light Sun;
        // "high" mode's current frame target when it had to come down from the screen rate (0 = the screen rate).
        int _autoHz; float _retryHzAt; float _retryWait = 120f;
        public bool HzLowered => _autoHz > 0;
        // The display's refresh rate (Unity 6), rounded; 60 when unknown.
        public static int DisplayHz { get {
#if UNITY_2022_2_OR_NEWER
            double r = Screen.currentResolution.refreshRateRatio.value;
#else
            double r = Screen.currentResolution.refreshRate;
#endif
            return r > 1 && !double.IsNaN(r) ? Mathf.Clamp((int)System.Math.Round(r), 30, 240) : 60; } }

        // Device-only settings (PlayerPrefs, not the save: the save format stays identical to the web game's).
        // The old "auto" (match the screen, the default until 2026-11-27) reads as 60, the new default.
        public static string SavedFpsMode(int fpsCap) => Mode(PlayerPrefs.GetString("sb.fpsMode", fpsCap == 30 ? "30" : "60"));
        public static void SaveFpsMode(string m) { PlayerPrefs.SetString("sb.fpsMode", m); PlayerPrefs.Save(); }
        public static string SavedShake { get { var s = PlayerPrefs.GetString("sb.shake", "full"); return s == "reduced" || s == "off" ? s : "full"; } set { PlayerPrefs.SetString("sb.shake", value); PlayerPrefs.Save(); } }
        // The "high" choice: the screen's rate up to 120 (90 on a 90 Hz panel); 60 on a 60 Hz screen.
        public static int HighHz => Sigilbound.Core.FrameRate.HighHz(DisplayHz);
        public static bool HighAvailable => HighHz > 60;
        // True while auto-replay loops battles (App): 30 fps, since nobody watches it closely and it can run
        // for an hour (performance review #2).
        public static System.Func<bool> ReplayCapped;
        bool _replay;

        float _acc; int _frames; float _settle; float _noUpUntil;
        public static System.Action<string> Notice; // toasts (App.Toast)
        // True while a battle runs. Auto never changes tier mid-battle: Low turns post-processing off,
        // so a switch flips the whole arena's look (grade, shadows, sharpness) in front of the player.
        // The decision waits in _pending and is applied when the battle ends.
        public static System.Func<bool> Busy;
        int _pending = -1;
        public bool ThermalLimited { get; private set; }
        public int ThermalStatus { get; private set; } = -1; // -1 unknown, 0 none .. 6 shutdown
        float _thermalPoll, _coolSince = -1;
        // Low tier, a warm phone and auto-replay force 30; otherwise the player's choice (60, or the screen's rate).
        public int TargetFps => Sigilbound.Core.FrameRate.Target(FpsMode, DisplayHz, Tier, ThermalLimited, _replay, _autoHz);

        static string Mode(string m) => Sigilbound.Core.FrameRate.Mode(m);

        public static Quality Create(GameObject host, string choice, string fpsMode)
        {
            var q = host.AddComponent<Quality>();
            I = q;
            q.Choice = string.IsNullOrEmpty(choice) ? "auto" : choice;
            q.FpsMode = Mode(fpsMode);
            q.Tier = q.Choice == "auto" ? GuessTier() : System.Array.IndexOf(Tiers, q.Choice) is int i && i >= 0 ? i : 2;
            q.Apply();
            return q;
        }

        public string TierName => Tiers[Tier];

        // Phones with plenty of memory and cores (S26+ class) start on High; the Note20 class on Medium.
        static int GuessTier()
        {
            int mem = SystemInfo.systemMemorySize, cores = SystemInfo.processorCount;
            if (mem >= 7000 && cores >= 8) return 2;
            if (mem >= 3500) return 1;
            return 0;
        }

        public void Set(string choice, string fpsMode)
        {
            Choice = choice; FpsMode = Mode(fpsMode); _autoHz = 0; _retryWait = 120f;
            if (choice != "auto") { int i = System.Array.IndexOf(Tiers, choice); Tier = i >= 0 ? i : 2; if (ThermalLimited && Tier > 0) Tier--; }
            Apply();
        }

        public void Apply()
        {
            // Frame pacing (MECHANICS.md 6.1). Phones: no vsync count, an explicit target (Android picks the
            // display mode that matches it). Desktop: Auto follows the monitor through vsync; a cap uses a
            // vsync divider when it divides the refresh rate evenly, else a frame-rate target.
            int fpsT = TargetFps;
#if UNITY_ANDROID || UNITY_IOS
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = fpsT;
#else
            if (FpsMode == "high" && _autoHz == 0 && Tier > 0 && !ThermalLimited && !_replay) { QualitySettings.vSyncCount = 1; Application.targetFrameRate = -1; }
            else
            {
                int hz = DisplayHz, div = hz % fpsT == 0 ? hz / fpsT : 0;
                QualitySettings.vSyncCount = div >= 1 && div <= 4 ? div : 0;
                Application.targetFrameRate = QualitySettings.vSyncCount > 0 ? -1 : fpsT;
            }
#endif
            if (Sun != null) Sun.shadows = Tier == 0 ? LightShadows.None : Tier >= 2 ? LightShadows.Soft : LightShadows.Hard;
#if SB_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.renderScale = new[] { 0.6f, 0.75f, 0.9f, 1f }[Tier];
                urp.msaaSampleCount = new[] { 1, 2, 2, 4 }[Tier];
                urp.shadowDistance = new[] { 0f, 16f, 22f, 28f }[Tier];
                urp.mainLightShadowmapResolution = new[] { 512, 1024, 2048, 4096 }[Tier];
                urp.shadowCascadeCount = new[] { 1, 1, 2, 4 }[Tier]; // the arena is small: cascades only where they pay off
            }
#else
            QualitySettings.shadowDistance = new[] { 0f, 16f, 22f, 28f }[Tier];
            QualitySettings.antiAliasing = new[] { 0, 2, 2, 4 }[Tier];
#endif
            SkillFx.I?.SetTier(Tier);
            PostFx.SetTier(Tier);
            // Low: up to 10 high-detail heroes on screen, so skinning takes 2 bones per vertex and the hero
            // materials drop their normal maps (PLAN step 58).
#if UNITY_2019_3_OR_NEWER
            QualitySettings.skinWeights = Tier == 0 ? SkinWeights.TwoBones : SkinWeights.FourBones;
#endif
            HeroModels.SetLowDetail(Tier == 0);
            _acc = 0; _frames = 0; _settle = 3f; _pending = -1;
        }

        // Auto: average the frame rate over 4-second windows (after a settle period) and step.
        void Update()
        {
            PollThermals();
            bool replay = ReplayCapped != null && ReplayCapped();
            if (replay != _replay) { _replay = replay; Apply(); return; }
            bool busy = Busy != null && Busy();
            // Auto frame rate at a high-refresh screen: lowered to 60 under load, it tries the screen rate
            // again between battles after a wait that doubles on each failed try (up to 8 minutes).
            if (FpsMode == "high" && _autoHz > 0 && !busy && Time.unscaledTime > _retryHzAt) { _autoHz = 0; _retryWait = Mathf.Min(480f, _retryWait * 2f); Apply(); return; }
            if (Choice != "auto" && FpsMode != "high") { _pending = -1; return; }
            if (_pending >= 0 && !busy) { Tier = _pending; _pending = -1; Apply(); return; }
            if (_settle > 0) { _settle -= Time.unscaledDeltaTime; return; }
            _acc += Time.unscaledDeltaTime; _frames++;
            if (_acc < 4f) return;
            float fps = _frames / _acc;
            int target = TargetFps;
            // At a high refresh rate, lower the frame target before the graphics (not gated by Busy:
            // the look doesn't change, only smoothness).
            if (FpsMode == "high" && target > 60 && fps < target * 0.85f)
            {
                _autoHz = 60; _retryHzAt = Time.unscaledTime + _retryWait; Apply();
                return;
            }
            if (Choice != "auto") { _acc = 0; _frames = 0; return; }
            int from = _pending >= 0 ? _pending : Tier;
            // Low runs capped at 30 fps, so holding 30 there says nothing about holding the player's
            // target on Medium: from Low, only step up when the player chose 30 anyway (else it would bounce).
            int upTarget = from == 0 ? (FpsMode == "30" ? 30 : 60) : target;
            int next = from;
            if (fps < target * 0.85f && from > 0) { next = from - 1; _noUpUntil = Time.unscaledTime + 60f; }
            else if (fps > upTarget * 0.97f && from < 2 && !ThermalLimited && Time.unscaledTime > _noUpUntil) next = from + 1;
            _acc = 0; _frames = 0;
            if (next == from) return;
            if (busy) { _pending = next; return; }
            Tier = next; Apply();
        }

        // ---------- thermals ----------
        const int ThermalModerate = 2; // PowerManager.THERMAL_STATUS_MODERATE
        void PollThermals()
        {
            if ((_thermalPoll -= Time.unscaledDeltaTime) > 0) return;
            _thermalPoll = 10f;
            ThermalStatus = ReadThermalStatus();
            if (ThermalStatus < 0) return;
            if (ThermalStatus >= ThermalModerate)
            {
                _coolSince = -1;
                if (ThermalLimited) return;
                ThermalLimited = true;
                if (Tier > 0) Tier--;
                _noUpUntil = Time.unscaledTime + 120f;
                Apply();
                Notice?.Invoke("Phone is warm: 30 fps and lighter graphics for now");
            }
            else if (ThermalLimited)
            {
                if (_coolSince < 0) { _coolSince = Time.unscaledTime; return; }
                if (Time.unscaledTime - _coolSince < 120f) return;
                ThermalLimited = false; _coolSince = -1; _autoHz = 0;
                if (Choice != "auto") { int i = System.Array.IndexOf(Tiers, Choice); Tier = i >= 0 ? i : 2; }
                Apply();
                Notice?.Invoke("Phone has cooled down: graphics restored");
            }
        }

#if UNITY_ANDROID && !UNITY_EDITOR
        AndroidJavaObject _power; bool _noThermalApi;
        int ReadThermalStatus()
        {
            if (_noThermalApi) return -1;
            try
            {
                if (_power == null)
                {
                    using (var version = new AndroidJavaClass("android.os.Build$VERSION"))
                        if (version.GetStatic<int>("SDK_INT") < 29) { _noThermalApi = true; return -1; }
                    using (var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                    using (var activity = player.GetStatic<AndroidJavaObject>("currentActivity"))
                        _power = activity.Call<AndroidJavaObject>("getSystemService", "power");
                    if (_power == null) { _noThermalApi = true; return -1; }
                }
                return _power.Call<int>("getCurrentThermalStatus");
            }
            catch (System.Exception e) { _noThermalApi = true; Debug.LogWarning("Sigilbound: thermal status unavailable: " + e.Message); return -1; }
        }
        void OnDestroy() { _power?.Dispose(); }
#else
        int ReadThermalStatus() => -1;
#endif
    }
}
