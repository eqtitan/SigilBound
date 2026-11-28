// Phone check (Settings > Run phone check): a fixed 60-second scripted sequence that a tester runs on a
// real phone in one tap, and the numbers it reports. This file is the Unity-free part: the script (which
// step plays when), the frame statistics (average fps, 1% low, frame-time p95/p99, spikes over 50 ms) and
// the compact text report. Client/PhoneCheckRun.cs plays the steps and feeds the samples in.
// The same script runs on every phone and every time, so two reports can be compared line by line.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Sigilbound.Core
{
    public static class PhoneCheck
    {
        public const int Version = 1;
        public const float Duration = 60f;
        public const float SpikeMs = 50f;   // a frame longer than this is a visible hitch
        public const int MaxSpikes = 8;     // the report lists the longest few

        // The scripted sequence: Home (menu lineup), the team screen with the stage's enemies preloading, a
        // heavy battle at the current quality, the Titan's arrival, and back to Home. Times in seconds.
        public static readonly (string Name, float Start, float End)[] Steps =
        {
            ("home", 0f, 8f),
            ("team", 8f, 18f),
            ("battle", 18f, 43f),
            ("titan", 43f, 55f),
            ("back", 55f, 60f),
        };

        // The heavy battle is always the same fight, whatever the save: the storm coast's lair (chapter 15,
        // stage 6): weather, the lair landmark and its waves up to the boss.
        public const int HeavyChapter = 14;
        public static int HeavyStage => HeavyChapter * GameData.StagesPerChapter + GameData.StagesPerChapter - 1;

        public static string StepAt(float t)
        {
            if (t < 0) return Steps[0].Name;
            foreach (var s in Steps) if (t < s.End) return s.Name;
            return "done";
        }

        public static int StepIndexAt(float t)
        {
            for (int i = 0; i < Steps.Length; i++) if (t < Steps[i].End) return i;
            return Steps.Length;
        }

        // Nearest-rank percentile of frame times (ms), 0..100.
        public static float Percentile(IReadOnlyList<float> ms, float pct)
        {
            if (ms == null || ms.Count == 0) return 0;
            var sorted = ms.OrderBy(x => x).ToArray();
            int rank = (int)Math.Ceiling(pct / 100.0 * sorted.Length);
            return sorted[Math.Max(0, Math.Min(sorted.Length - 1, rank - 1))];
        }

        // "1% low": the frame rate of the slowest 1% of frames (at least one frame), as 1000 / their mean time.
        public static float OnePercentLowFps(IReadOnlyList<float> ms)
        {
            if (ms == null || ms.Count == 0) return 0;
            int n = Math.Max(1, ms.Count / 100);
            double mean = ms.OrderByDescending(x => x).Take(n).Average();
            return mean <= 0 ? 0 : (float)(1000.0 / mean);
        }

        public static float AvgFps(IReadOnlyList<float> ms)
        {
            if (ms == null || ms.Count == 0) return 0;
            double total = ms.Sum(x => (double)x);
            return total <= 0 ? 0 : (float)(ms.Count * 1000.0 / total);
        }

        internal static string F(double v, int dp = 1) => v.ToString(dp == 0 ? "0" : dp == 1 ? "0.0" : "0.00", CultureInfo.InvariantCulture);
    }

    public sealed class PhoneCheckSpike { public float T; public float Ms; public string Step; public string Perf; }

    // Everything one run records. The client fills it in; Report() turns it into text.
    public sealed class PhoneCheckResult
    {
        // frames, per step (index into PhoneCheck.Steps)
        public readonly List<float>[] StepMs = PhoneCheck.Steps.Select(_ => new List<float>()).ToArray();
        public readonly List<PhoneCheckSpike> Spikes = new List<PhoneCheckSpike>();
        public int SpikeCount;

        // memory in MB (Unity allocated / reserved, the process's resident set; PSS from Android's meminfo)
        public double AllocAvgMb, AllocPeakMb, ReservedPeakMb, RssPeakMb, PssEndMb;
        int _memSamples; double _allocSum;

        // battery 0..100 (-1 unknown), thermal names, quality tier at start and end
        public int BatteryStart = -1, BatteryEnd = -1; public string BatteryState = "";
        public string ThermalStart = "n/a", ThermalEnd = "n/a", TierStart = "", TierEnd = "";
        public bool Completed; public float Seconds; public int Errors;

        // device and build (free text, filled by the client)
        public string Build = "dev", When = "", Device = "", Os = "", Gpu = "", GraphicsApi = "", Screen = "", Quality = "";
        public int RamMb, Cores, TargetFps, DisplayHz;

        public IEnumerable<float> AllMs => StepMs.SelectMany(s => s);

        public void AddFrame(float t, float ms, string perf)
        {
            int i = PhoneCheck.StepIndexAt(t);
            if (i >= StepMs.Length || ms <= 0) return;
            StepMs[i].Add(ms);
            if (ms <= PhoneCheck.SpikeMs) return;
            SpikeCount++;
            Spikes.Add(new PhoneCheckSpike { T = t, Ms = ms, Step = PhoneCheck.Steps[i].Name, Perf = perf });
            if (Spikes.Count > PhoneCheck.MaxSpikes) Spikes.Remove(Spikes.OrderBy(s => s.Ms).First()); // keep the longest
        }

        public void AddMemory(double allocMb, double reservedMb, double rssMb)
        {
            _memSamples++; _allocSum += allocMb;
            AllocAvgMb = _allocSum / _memSamples;
            AllocPeakMb = Math.Max(AllocPeakMb, allocMb);
            ReservedPeakMb = Math.Max(ReservedPeakMb, reservedMb);
            RssPeakMb = Math.Max(RssPeakMb, rssMb);
        }

        // The compact report: plain text, about 15 lines, safe to paste into a chat message.
        // Line labels are fixed ("all:", "steps:", "spikes:", "memory:", "battery:") so reports can be compared.
        public string Report()
        {
            string F(double v, int dp = 1) => PhoneCheck.F(v, dp);
            var all = AllMs.ToList();
            var sb = new StringBuilder();
            sb.Append("Sigilbound phone check v").Append(PhoneCheck.Version).Append(Completed ? "" : " (stopped early)").Append('\n');
            sb.Append("build ").Append(Build).Append(" · ").Append(When).Append('\n');
            sb.Append("device ").Append(Device).Append(" · ").Append(Os).Append('\n');
            sb.Append("gpu ").Append(Gpu).Append(" · ").Append(GraphicsApi).Append(" · RAM ").Append(RamMb).Append(" MB · ").Append(Cores).Append(" cores\n");
            sb.Append("screen ").Append(Screen).Append(" · ").Append(DisplayHz).Append(" Hz · quality ").Append(Quality).Append(" · target ").Append(TargetFps).Append(" fps\n");
            sb.Append("all: ").Append(F(PhoneCheck.AvgFps(all))).Append(" fps avg · 1% low ").Append(F(PhoneCheck.OnePercentLowFps(all)))
              .Append(" · p95 ").Append(F(PhoneCheck.Percentile(all, 95))).Append(" ms · p99 ").Append(F(PhoneCheck.Percentile(all, 99)))
              .Append(" ms · ").Append(all.Count).Append(" frames in ").Append(F(Seconds, 0)).Append(" s\n");
            sb.Append("steps:");
            for (int i = 0; i < StepMs.Length; i++)
            {
                var s = StepMs[i];
                sb.Append(i == 0 ? " " : " · ").Append(PhoneCheck.Steps[i].Name).Append(' ');
                sb.Append(s.Count == 0 ? "-" : F(PhoneCheck.AvgFps(s)) + "/" + F(PhoneCheck.OnePercentLowFps(s), 0) + " p99 " + F(PhoneCheck.Percentile(s, 99), 0));
            }
            sb.Append('\n');
            sb.Append("spikes: ").Append(SpikeCount).Append(" over ").Append(F(PhoneCheck.SpikeMs, 0)).Append(" ms");
            foreach (var sp in Spikes.OrderBy(s => s.T))
            {
                sb.Append("\n  ").Append(sp.Step).Append(" @").Append(F(sp.T)).Append(" s ").Append(F(sp.Ms, 0)).Append(" ms");
                if (!string.IsNullOrEmpty(sp.Perf)) sb.Append(" [").Append(Clip(sp.Perf, 90)).Append(']');
            }
            sb.Append('\n');
            sb.Append("memory: alloc avg ").Append(F(AllocAvgMb, 0)).Append(" / peak ").Append(F(AllocPeakMb, 0)).Append(" MB · reserved peak ").Append(F(ReservedPeakMb, 0))
              .Append(" MB · rss peak ").Append(RssPeakMb > 0 ? F(RssPeakMb, 0) + " MB" : "n/a").Append(" · pss end ").Append(PssEndMb > 0 ? F(PssEndMb, 0) + " MB" : "n/a").Append('\n');
            sb.Append("battery: ").Append(BatteryStart < 0 ? "n/a" : BatteryStart + "%").Append(" -> ").Append(BatteryEnd < 0 ? "n/a" : BatteryEnd + "%");
            if (!string.IsNullOrEmpty(BatteryState)) sb.Append(" (").Append(BatteryState).Append(')');
            sb.Append(" · thermal ").Append(ThermalStart).Append(" -> ").Append(ThermalEnd).Append(" · tier ").Append(TierStart).Append(" -> ").Append(TierEnd)
              .Append(" · errors ").Append(Errors).Append('\n');
            return sb.ToString();
        }

        // One line for the summary screen's headline.
        public string Headline()
        {
            var all = AllMs.ToList();
            return $"{PhoneCheck.F(PhoneCheck.AvgFps(all))} fps average · 1% low {PhoneCheck.F(PhoneCheck.OnePercentLowFps(all))} · {SpikeCount} hitches over {PhoneCheck.F(PhoneCheck.SpikeMs, 0)} ms";
        }

        static string Clip(string s, int n)
        {
            s = s.Replace('\n', ' ').Replace('\r', ' ').Replace('[', '(').Replace(']', ')');
            return s.Length <= n ? s : s.Substring(0, n - 1) + "…";
        }
    }
}
