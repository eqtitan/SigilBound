// Keeps the project brain (docs/sigilbound/brain) well formed so agents can rely on it: every lesson has the
// required fields, ids are unique and sequential, Related ids exist, every lesson appears in MAP.md, and no
// secrets-looking strings slip in.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class BrainTests
{
    static string Find(string rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(d.FullName, rel);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    public static void Run(Action<bool, string> check)
    {
        var lp = Find("docs/sigilbound/brain/LESSONS.md"); var mp = Find("docs/sigilbound/brain/MAP.md");
        check(lp != null && mp != null, "brain: LESSONS.md and MAP.md exist");
        if (lp == null || mp == null) return;
        var text = File.ReadAllText(lp); var map = File.ReadAllText(mp);
        var heads = Regex.Matches(text, @"^## (L\d{3}) - (.+)$", RegexOptions.Multiline);
        var ids = heads.Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        check(ids.Count > 0, "brain: has lessons");
        check(ids.Distinct().Count() == ids.Count, "brain: lesson ids are unique");
        check(ids.Select((id, i) => int.Parse(id.Substring(1)) == i + 1).All(b => b), "brain: lesson ids are sequential from L001");
        string[] fields = { "Status", "Systems", "Symptom", "Cause", "Fix", "Prevention", "Related", "Evidence" };
        var bodies = Regex.Split(text, @"^## L\d{3} - .+$", RegexOptions.Multiline).Skip(1).ToList();
        var bad = ids.Where((id, i) => fields.Any(f => !Regex.IsMatch(bodies[i], @"^- " + f + @": \S", RegexOptions.Multiline))).ToList();
        check(bad.Count == 0, "brain: every lesson has Status/Systems/Symptom/Cause/Fix/Prevention/Related/Evidence" + (bad.Count > 0 ? " - missing in " + string.Join(", ", bad) : ""));
        var badStatus = ids.Where((id, i) => !Regex.IsMatch(bodies[i], @"^- Status: (solved|open|decision)\s*$", RegexOptions.Multiline)).ToList();
        check(badStatus.Count == 0, "brain: Status is solved, open or decision" + (badStatus.Count > 0 ? " - " + string.Join(", ", badStatus) : ""));
        var related = Regex.Matches(text, @"^- Related: (.*)$", RegexOptions.Multiline).Cast<Match>()
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, @"L\d{3}").Cast<Match>().Select(x => x.Value)).Distinct().ToList();
        var dangling = related.Where(r => !ids.Contains(r)).ToList();
        check(dangling.Count == 0, "brain: Related ids exist" + (dangling.Count > 0 ? " - " + string.Join(", ", dangling) : ""));
        var unmapped = ids.Where(id => !Regex.IsMatch(map, @"\b" + id + @"\b")).ToList();
        check(unmapped.Count == 0, "brain: every lesson is linked from MAP.md" + (unmapped.Count > 0 ? " - " + string.Join(", ", unmapped) : ""));
        bool secretish = Regex.IsMatch(text + map, @"(hf_[A-Za-z0-9]{20,}|AIza[0-9A-Za-z_\-]{30,}|ghp_[A-Za-z0-9]{30,}|sk-[A-Za-z0-9]{20,}|[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[a-z]{2,})");
        check(!secretish, "brain: no keys, tokens or email addresses in the brain");
    }
}
