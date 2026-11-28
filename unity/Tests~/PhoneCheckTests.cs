// Phone check (PLAN step 116, Core/PhoneCheck.cs + Client/PhoneCheckRun.cs): the scripted sequence is the
// same every run, the statistics are right, and the report keeps a fixed, culture-proof format.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using Sigilbound.Core;

static class PhoneCheckTests
{
    // A fixed, made-up minute: 60 fps with a slower battle and an SBPERF hitch every 300 frames.
    static PhoneCheckResult Sample()
    {
        var r = new PhoneCheckResult
        {
            Build = "191 (0.21.1)", When = "2026-11-28 10:00", Device = "Test Phone", Os = "Android OS 16 / API-36", Gpu = "Adreno 750",
            GraphicsApi = "Vulkan", RamMb = 11500, Cores = 8, Screen = "2340x1080 450 dpi", DisplayHz = 120, Quality = "High (auto) · fps 60", TargetFps = 60,
            BatteryStart = 81, BatteryEnd = 80, BatteryState = "discharging", ThermalStart = "none", ThermalEnd = "light", TierStart = "High", TierEnd = "High",
            Completed = true, Seconds = 60, PssEndMb = 880,
        };
        float t = 0; int i = 0;
        while (t < PhoneCheck.Duration)
        {
            string step = PhoneCheck.StepAt(t);
            float ms = step == "battle" ? 20f : 16.6f;
            string perf = null;
            if (i % 300 == 150) { ms = 60f + (i / 300); perf = "hd ysolde: main thread albedo 41.0 ms [x]"; }
            t += ms / 1000f;
            r.AddFrame(t, ms, perf);
            if (i % 60 == 0) r.AddMemory(400 + i / 60, 600, 900 + i / 120);
            i++;
        }
        return r;
    }

    public static void Run(Action<bool, string> check)
    {
        var steps = PhoneCheck.Steps;
        bool contiguous = steps[0].Start == 0 && steps.Last().End == PhoneCheck.Duration && Enumerable.Range(1, steps.Length - 1).All(k => steps[k].Start == steps[k - 1].End && steps[k].End > steps[k].Start);
        check(contiguous && PhoneCheck.Duration == 60f, "phone check: the five steps cover 0-60 s with no gaps");
        check(string.Join(",", steps.Select(s => s.Name)) == "home,team,battle,titan,back", "phone check: Home, team screen, battle, Titan, back, in that order");
        check(PhoneCheck.StepAt(0) == "home" && PhoneCheck.StepAt(7.99f) == "home" && PhoneCheck.StepAt(8f) == "team" && PhoneCheck.StepAt(18f) == "battle"
              && PhoneCheck.StepAt(43f) == "titan" && PhoneCheck.StepAt(55f) == "back" && PhoneCheck.StepAt(60f) == "done", "phone check: StepAt switches exactly at the step boundaries");
        var a = Enumerable.Range(0, 601).Select(k => PhoneCheck.StepAt(k / 10f)).ToArray();
        var b = Enumerable.Range(0, 601).Select(k => PhoneCheck.StepAt(k / 10f)).ToArray();
        check(a.SequenceEqual(b), "phone check: the sequence is deterministic (same step at the same time, every run)");

        int hs = PhoneCheck.HeavyStage;
        check(hs == 14 * GameData.StagesPerChapter + 5 && hs < GameData.TotalStages && hs % GameData.StagesPerChapter == GameData.StagesPerChapter - 1,
              $"phone check: the heavy battle is always the storm coast's lair (stage index {hs}, a boss stage)");
        var w1 = string.Join(",", Game.StageWaves(hs).SelectMany(w => w).Select(e => e.Template.Id));
        var w2 = string.Join(",", Game.StageWaves(hs).SelectMany(w => w).Select(e => e.Template.Id));
        check(w1.Length > 0 && w1 == w2 && Game.StageWaves(hs).Sum(w => w.Count) >= 4, $"phone check: the heavy battle's enemies are the same every run ({w1})");

        // Statistics on known numbers.
        var ms = new float[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };
        check(PhoneCheck.Percentile(ms, 95) == 100 && PhoneCheck.Percentile(ms, 50) == 50 && PhoneCheck.Percentile(ms, 10) == 10, "phone check: nearest-rank p50/p95 of frame times");
        var flat = Enumerable.Repeat(16.6f, 990).Concat(Enumerable.Repeat(100f, 10)).ToArray();
        check(Math.Abs(PhoneCheck.OnePercentLowFps(flat) - 10f) < 0.01f, "phone check: 1% low is the frame rate of the slowest 1% of frames");
        check(Math.Abs(PhoneCheck.AvgFps(Enumerable.Repeat(20f, 50).ToArray()) - 50f) < 0.01f && PhoneCheck.AvgFps(new float[0]) == 0, "phone check: average fps is frames over time");

        var r = Sample();
        // Sample hitches grow 1 ms each time, so the longest eight are the last eight.
        check(r.SpikeCount > PhoneCheck.MaxSpikes && r.Spikes.Count == PhoneCheck.MaxSpikes && r.Spikes.All(s => s.Ms > PhoneCheck.SpikeMs)
              && r.Spikes.Min(s => s.Ms) == 60f + (r.SpikeCount - PhoneCheck.MaxSpikes), $"phone check: hitches over 50 ms counted ({r.SpikeCount}), the {PhoneCheck.MaxSpikes} longest kept");
        check(r.Spikes.All(s => s.Step == PhoneCheck.StepAt(s.T - 0.0001f) || s.Step == PhoneCheck.StepAt(s.T)) && r.Spikes.All(s => s.Perf != null && s.Perf.StartsWith("hd ysolde")),
              "phone check: each hitch keeps its step and the SBPERF line logged with it");

        string rep1 = Sample().Report(), rep2 = Sample().Report();
        check(rep1 == rep2, "phone check: the same samples give the same report");
        var lines = rep1.TrimEnd('\n').Split('\n');
        string[] labels = { "Sigilbound phone check v1", "build ", "device ", "gpu ", "screen ", "all: ", "steps: ", "spikes: ", "memory: ", "battery: " };
        check(labels.All(l => lines.Any(x => x.StartsWith(l, StringComparison.Ordinal))), "phone check: report has the fixed lines (header, build, device, gpu, screen, all, steps, spikes, memory, battery)");
        string all = lines.First(x => x.StartsWith("all: "));
        check(all.Contains("fps avg") && all.Contains("1% low") && all.Contains("p95") && all.Contains("p99") && all.Contains("frames in 60 s"), $"phone check: the all line ({all})");
        string st = lines.First(x => x.StartsWith("steps: "));
        check(steps.All(s => st.Contains(s.Name + " ")) && st.Contains("battle 49."), $"phone check: per-step fps ({st})");
        check(lines.Count(x => x.StartsWith("  ")) == PhoneCheck.MaxSpikes && lines.Where(x => x.StartsWith("  ")).All(x => x.Contains(" ms [hd ysolde") && !x.Contains("[x]")),
              "phone check: the hitch lines list step, time, length and the SBPERF step (brackets inside it escaped)");
        string mem = lines.First(x => x.StartsWith("memory: "));
        check(mem.StartsWith("memory: alloc avg 4") && mem.Contains("reserved peak 600 MB") && mem.Contains("rss peak 9") && mem.Contains("pss end 880 MB"), $"phone check: memory line ({mem})");
        string bat = lines.First(x => x.StartsWith("battery: "));
        check(bat == "battery: 81% -> 80% (discharging) · thermal none -> light · tier High -> High · errors 0", $"phone check: battery/thermal line ({bat})");
        check(rep1.Length < 2000 && lines.Length <= 20, $"phone check: compact enough to paste ({rep1.Length} chars, {lines.Length} lines)");

        // Decimal points stay '.' whatever the phone's language.
        var keep = Thread.CurrentThread.CurrentCulture;
        try { Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE"); check(Sample().Report() == rep1, "phone check: report format is the same in any locale (de-DE)"); }
        finally { Thread.CurrentThread.CurrentCulture = keep; }

        var empty = new PhoneCheckResult().Report();
        check(empty.Contains("(stopped early)") && empty.Contains("battery: n/a -> n/a") && empty.Contains("rss peak n/a"), "phone check: an empty or stopped run still formats");

        // Client wiring (source guards).
        string root = AppContext.BaseDirectory;
        for (int k = 0; k < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); k++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "phone check: client sources not found here (skipped)"); return; }
        string Src(string f) => File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", f));
        string run = Src("PhoneCheckRun.cs"), set = Src("SettingsScreen.cs"), fb = Src("Feedback.cs"), app = Src("App.cs");
        check(run.Contains("NoSave = true") && run.Contains("SaveCodec.FromCode(SaveCodec.ToCode(real.S))") && run.Contains("seed: 20261128") && run.Contains("G = real; NoSave = false"),
              "phone check: runs on a throwaway copy of the save with a fixed seed, saves nothing, restores the real game");
        check(run.Contains("BattleSpec.ForStage(PhoneCheck.HeavyStage)") && run.Contains("BattleSpec.TitanPractice()") && run.Contains("OpenPrep(PhoneCheck.HeavyStage"),
              "phone check: the heavy battle, Titan practice (no try used) and the team screen with enemies preloading");
        check(run.Contains("GetTotalAllocatedMemoryLong") && run.Contains("/proc/self/status") && run.Contains("getPss") && run.Contains("batteryLevel") && run.Contains("SBPERF"),
              "phone check: records Unity memory, resident memory, Android PSS, battery and SBPERF steps");
        check(run.Contains("systemCopyBuffer = report") && run.Contains("LastPhoneCheck = report"), "phone check: the report is copied and kept for Send feedback");
        check(set.Contains("\"Run phone check\"") && set.Contains("StartPhoneCheck()") && set.Contains("Attach the last phone check") && fb.Contains("string phoneCheck = null"),
              "phone check: Settings button, and Send feedback attaches the last report");
        check(app.Contains("PhoneCheckRunning) return;") && app.Contains("DrawPhoneCheckBar()"), "phone check: no pop-ups during the run; a Stop bar is drawn over every screen");
    }
}
