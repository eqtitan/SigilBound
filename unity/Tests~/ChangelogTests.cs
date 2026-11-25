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
        check(Changelog.Unseen(demo, "2026-01-01 | A").Select(e => e.Title).SequenceEqual(new[] { "C", "B" }), "changelog: after an update, everything since the last seen entry");
        check(Changelog.Unseen(demo, "2026-01-03 | C").Count == 0, "changelog: nothing new, no window");
    }
}

// Every clip a high-detail hero asks for is in the retargeted free UAL set (MECHANICS.md 3.0).
static class HdClipTests
{
    public static void Run(System.Action<bool, string> check)
    {
        var have = new System.Collections.Generic.HashSet<string>(Sigilbound.Core.AnimStyle.HdClips);
        check(have.Count == 46, "hd clips: 46 free UAL clips listed");
        var bad = new System.Collections.Generic.List<string>();
        foreach (var h in Sigilbound.Core.GameData.Heroes)
        {
            var st = Sigilbound.Core.AnimStyle.Hd(h.Id);
            foreach (var n in new[] { st.Idle, st.Cast, st.Support, st.Ult, st.Win }.Concat(st.Strike).Concat(st.Hit).Concat(st.BigHit).Concat(st.Death).Concat(st.Fidget))
                if (n != null && !have.Contains(n)) bad.Add(h.Id + ":" + n);
        }
        foreach (var kv in Sigilbound.Core.AnimStyle.HdAliases) if (!have.Contains(kv.Value)) bad.Add("alias " + kv.Key + ":" + kv.Value);
        check(bad.Count == 0, "hd clips: every clip a hero plays exists" + (bad.Count > 0 ? " (missing " + string.Join(", ", bad.Distinct().Take(6)) + ")" : ""));
    }
}
