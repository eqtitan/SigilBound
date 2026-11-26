using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Every sound name the client plays is in Sound.Files, and every file Sound.Files and Sound.Tracks name
// exists in Resources/Audio (the procedural ones come from Tools~/audio/make_sfx.py and make_music.py).
static class SoundTests
{
    public static void Run(System.Action<bool, string> check)
    {
        string dir = System.AppContext.BaseDirectory, root = null;
        for (int i = 0; i < 8 && dir != null && root == null; i++, dir = Path.GetDirectoryName(dir))
            if (File.Exists(Path.Combine(dir, "Assets", "Sigilbound", "Client", "Sound.cs"))) root = dir;
        if (root == null) { check(true, "sounds: client sources not found here (skipped)"); return; }
        var src = File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", "Sound.cs"));
        var map = Regex.Matches(src, "\\[\"([A-Za-z_]+)\"\\] = \"([A-Za-z_]+)\"").Cast<Match>().ToDictionary(m => m.Groups[1].Value + "|" + m.Index, m => (key: m.Groups[1].Value, file: m.Groups[2].Value));
        var keys = map.Values.Select(v => v.key).ToHashSet();
        var audio = Path.Combine(root, "Assets", "Resources", "Audio");
        var missingFiles = map.Values.Where(v => !Directory.GetFiles(audio, v.file + ".*").Any(f => !f.EndsWith(".meta"))).Select(v => v.file).Distinct().ToList();
        check(missingFiles.Count == 0, $"sounds: every file the sound table names exists ({map.Count} entries)" + (missingFiles.Count > 0 ? " — missing: " + string.Join(", ", missingFiles) : ""));
        // names played directly: Play("x" ...), including both arms of a ternary inside the call
        var played = new System.Collections.Generic.HashSet<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Assets", "Sigilbound", "Client"), "*.cs"))
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "Sound\\.I\\?\\.Play\\(([^;]*?)\\)"))
                foreach (Match q in Regex.Matches(m.Groups[1].Value, "\"([A-Za-z_]+)\"")) played.Add(q.Groups[1].Value);
        // names built by Swing / Hit / Cast / Status
        foreach (var fam in new[] { "blade", "heavy", "polearm", "daggers", "bow", "shield", "unarmed" }) played.Add("swing_" + fam);
        foreach (var fam in new[] { "blade", "heavy", "polearm", "daggers", "arrow", "shield", "unarmed", "magic" }) played.Add("hit_" + fam);
        foreach (var el in System.Enum.GetNames(typeof(Sigilbound.Core.Element))) played.Add("cast_" + el.ToLowerInvariant());
        foreach (var n in new[] { "stun", "poison", "shield", "buff", "debuff", "crit" }) played.Add(n);
        var unknown = played.Where(p => !keys.Contains(p)).ToList();
        check(unknown.Count == 0, $"sounds: every sound the game plays has an entry ({played.Count} names)" + (unknown.Count > 0 ? " — unknown: " + string.Join(", ", unknown) : ""));
    }
}
