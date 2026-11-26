// The player-facing changelog (Assets/Resources/Changelog.txt): it parses, every entry has a date,
// a title and tagged lines, and "unseen" picks the right entries after an update.
using System;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class ChangelogTests
{
    public static void Run(Action<bool, string> check)
    {
        string dir = AppContext.BaseDirectory, path = null;
        for (int i = 0; i < 8 && dir != null && path == null; i++, dir = Path.GetDirectoryName(dir))
        {
            var p = Path.Combine(dir, "Assets", "Resources", "Changelog.txt");
            if (File.Exists(p)) path = p;
        }
        check(path != null, "changelog: Assets/Resources/Changelog.txt found");
        if (path == null) return;
        var all = Changelog.Parse(File.ReadAllText(path));
        check(all.Count > 0, "changelog: has entries");
        check(all.All(e => e.Date.Length == 10 && e.Title.Length > 0 && e.Items.Count > 0), "changelog: every entry has a date (YYYY-MM-DD), a title and items");
        check(all.Select(e => e.Key).Distinct().Count() == all.Count, "changelog: entry headers are unique");
        check(all.Zip(all.Skip(1), (a, b) => string.CompareOrdinal(a.Date, b.Date) >= 0).All(x => x), "changelog: newest entry first");
        var raw = File.ReadAllLines(path).Select(l => l.Trim()).Where(l => l.Length > 0 && !l.StartsWith("#")).ToList();
        check(raw.All(l => Changelog.Tags.Any(t => l.StartsWith(t + ":"))), "changelog: every item line starts with new:, fix:, change: or balance:");

        var demo = Changelog.Parse("## 2026-01-03 | C\nnew: c\n## 2026-01-02 | B\nfix: b\n## 2026-01-01 | A\nchange: a\n");
        check(Changelog.Unseen(demo, "").Select(e => e.Title).SequenceEqual(new[] { "C" }), "changelog: first launch shows only the newest");
        check(Changelog.Unseen(demo, "2026-01-01 | A | 1").Select(e => e.Title).SequenceEqual(new[] { "C", "B" }), "changelog: after an update, everything since the last seen entry");
        check(Changelog.Unseen(demo, "2026-01-03 | C | 1").Count == 0, "changelog: nothing new, no window");
        check(Changelog.Unseen(demo, "2026-01-03 | C | 0").Select(e => e.Title).SequenceEqual(new[] { "C" }), "changelog: an entry that gained lines shows again");
    }
}

// Every clip a high-detail hero asks for is in the retargeted free UAL set (MECHANICS.md 3.0).
static class HdClipTests
{
    public static void Run(System.Action<bool, string> check)
    {
        var have = new System.Collections.Generic.HashSet<string>(Sigilbound.Core.AnimStyle.HdClips);
        check(have.Count == 48, "hd clips: 45 free UAL clips (no T-pose reference) + 3 hand-made");
        var bad = new System.Collections.Generic.List<string>();
        foreach (var h in Sigilbound.Core.GameData.Heroes)
        {
            var st = Sigilbound.Core.AnimStyle.Hd(h.Id);
            foreach (var n in new[] { st.Idle, st.Cast, st.Support, st.Ult, st.Win, st.Block, st.Defeat, st.Ready }.Concat(st.Strike).Concat(st.Hit).Concat(st.BigHit).Concat(st.Death).Concat(st.Fidget))
                if (n != null && !have.Contains(n)) bad.Add(h.Id + ":" + n);
        }
        foreach (var kv in Sigilbound.Core.AnimStyle.HdAliases) if (!have.Contains(kv.Value)) bad.Add("alias " + kv.Key + ":" + kv.Value);
        { var t = Sigilbound.Core.AnimStyle.TitanHd; foreach (var n in new[] { t.Idle, t.Cast, t.Support, t.Win }.Concat(t.Strike).Concat(t.Hit).Concat(t.BigHit).Concat(t.Death).Concat(t.Fidget)) if (n != null && !have.Contains(n)) bad.Add("titan:" + n); foreach (var kv in Sigilbound.Core.AnimStyle.TitanHdAliases) if (!have.Contains(kv.Value)) bad.Add("titan alias " + kv.Key + ":" + kv.Value); }
        check(bad.Count == 0, "hd clips: every clip a hero plays exists" + (bad.Count > 0 ? " (missing " + string.Join(", ", bad.Distinct().Take(6)) + ")" : ""));
    }
}

// Impact timing and hit-stop (MECHANICS.md 3.2, 4.1): time-based, so frame rate changes nothing but smoothness.
static class ImpactTests
{
    public static void Run(System.Action<bool, string> check)
    {
        foreach (var len in new[] { 0.9, 1.2, 1.6 })
            foreach (var f in (Sigilbound.Core.WeaponFamily[])System.Enum.GetValues(typeof(Sigilbound.Core.WeaponFamily)))
            {
                double imp = Sigilbound.Core.Impact.ImpactTime(f);
                double t30 = Sigilbound.Core.Impact.CrossingTime(len, imp, 30), t60 = Sigilbound.Core.Impact.CrossingTime(len, imp, 60), t120 = Sigilbound.Core.Impact.CrossingTime(len, imp, 120);
                if (System.Math.Abs(t30 - imp * len) > 1 / 30.0 + 1e-9 || System.Math.Abs(t120 - imp * len) > 1 / 120.0 + 1e-9 || System.Math.Abs(t60 - imp * len) > 1 / 60.0 + 1e-9)
                { check(false, $"impact: {f} at {len}s lands within one frame at 30/60/120 fps"); return; }
            }
        check(true, "impact: every family lands within one frame of its exact time at 30, 60 and 120 fps");
        var B = Sigilbound.Core.WeaponFamily.Blade;
        check(System.Math.Abs(Sigilbound.Core.Impact.HitStop(B, false, false, false, 1) - 0.07) < 1e-9, "hit-stop: blade 0.07 s at 1x");
        check(Sigilbound.Core.Impact.HitStop(Sigilbound.Core.WeaponFamily.Heavy, true, true, false, 1) <= Sigilbound.Core.Impact.MaxHitStop + 1e-9, "hit-stop: capped at 0.25 s");
        double real3 = Sigilbound.Core.Impact.HitStop(B, false, false, false, 3) / 3; // real seconds at 3x
        check(real3 < 0.07 && real3 > 0.07 * 0.5, "hit-stop: shorter in real time at 3x but still visible (speed^-0.55)");
        check(Sigilbound.Core.Impact.FamilyOf("lumen") == B && Sigilbound.Core.Impact.FamilyOf("odo") == Sigilbound.Core.WeaponFamily.Unarmed, "impact: families from the hero's prop");
    }
}

static class FlightTests
{
    public static void Run(System.Action<bool, string> check)
    {
        check(System.Math.Abs(Sigilbound.Core.Impact.FlightTime("arrow", 4.4) - 0.2) < 1e-9, "projectiles: an arrow flies 4.4 m in 0.2 s (22 m/s)");
        check(Sigilbound.Core.Impact.FlightTime("orb", 0.5) == 0.25 && Sigilbound.Core.Impact.FlightTime("orb", 50) == 0.55, "projectiles: flight time clamped (orb 0.25-0.55 s)");
    }
}

// The clip file the high-detail heroes load: every clip a hero plays is in it under its plain name.
static class HdClipFileTests
{
    public static void Run(System.Action<bool, string> check)
    {
        string dir = System.AppContext.BaseDirectory, path = null;
        for (int i = 0; i < 8 && dir != null && path == null; i++, dir = System.IO.Path.GetDirectoryName(dir))
        {
            var p = System.IO.Path.Combine(dir, "Tools~", "models", "ubc_anims.glb");
            if (System.IO.File.Exists(p)) path = p;
        }
        if (path == null) { check(true, "hd clip file: not found here (skipped)"); return; }
        var b = System.IO.File.ReadAllBytes(path);
        int n = System.BitConverter.ToInt32(b, 12);
        var json = System.Text.Encoding.UTF8.GetString(b, 20, n);
        var names = new System.Collections.Generic.HashSet<string>();
        using (var doc = System.Text.Json.JsonDocument.Parse(json))
            foreach (var a in doc.RootElement.GetProperty("animations").EnumerateArray())
                if (a.TryGetProperty("name", out var nm)) names.Add(nm.GetString());
        var missing = Sigilbound.Core.AnimStyle.HdClips.Where(c => !names.Contains(c)).ToList();
        check(names.Count > 0 && missing.Count == 0 && !names.Any(x => x.Contains("TPose")),
            $"hd clip file: {names.Count} clips with plain names, no T-pose clip" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing.Take(5)) : ""));

        // HeroModels.BuildHd puts the Animation on the holder because the clips and every hero file have a
        // single root node named Armature (glTFast then makes no Scene object). Guard that layout.
        string RootOf(string f)
        {
            var bb = System.IO.File.ReadAllBytes(f); int nn = System.BitConverter.ToInt32(bb, 12);
            using var d = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bb, 20, nn));
            var roots = d.RootElement.GetProperty("scenes")[0].GetProperty("nodes").EnumerateArray().Select(x => x.GetInt32()).ToList();
            return roots.Count == 1 ? d.RootElement.GetProperty("nodes")[roots[0]].GetProperty("name").GetString() : $"{roots.Count} roots";
        }
        var hdDir = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(path), "heroes_hd");
        var odd = new[] { path }.Concat(System.IO.Directory.GetFiles(hdDir, "SB_Hero_*_HD*.glb")).Where(f => RootOf(f) != "Armature").Select(System.IO.Path.GetFileName).ToList();
        check(odd.Count == 0, "hd files: one root node, Armature (clip paths resolve from the holder)" + (odd.Count > 0 ? " — not: " + string.Join(", ", odd.Take(5)) : ""));
    }
}
