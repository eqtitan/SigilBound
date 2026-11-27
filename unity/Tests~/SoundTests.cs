using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Every sound name the client plays is in Sound.Files, and every file Sound.Files, Sound.Tracks and
// Sound.Beds name exists in Resources/Audio (made by Tools~/audio/make_real.py, make_sfx.py and
// make_music.py). Every arena theme has an ambience bed; every hero has an armour class (HeroArmour).
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
            foreach (Match m in Regex.Matches(File.ReadAllText(f), "Sound\\.I\\?\\.Play(?:At)?\\(([^;]*?)\\)"))
                foreach (Match q in Regex.Matches(m.Groups[1].Value, "\"([A-Za-z_]+)\"")) played.Add(q.Groups[1].Value);
        // names built by Swing / Hit / Cast / Status
        foreach (var fam in new[] { "blade", "heavy", "polearm", "daggers", "bow", "shield", "unarmed" }) played.Add("swing_" + fam);
        foreach (var fam in new[] { "blade", "heavy", "polearm", "daggers", "arrow", "shield", "unarmed", "magic" }) played.Add("hit_" + fam);
        foreach (var el in System.Enum.GetNames(typeof(Sigilbound.Core.Element))) played.Add("cast_" + el.ToLowerInvariant());
        foreach (var n in new[] { "stun", "poison", "shield", "buff", "debuff", "crit" }) played.Add(n);
        foreach (var a in System.Enum.GetNames(typeof(Sigilbound.Core.ArmourClass))) played.Add("armour_" + a.ToLowerInvariant());
        // boss-lair set-piece cues the scenery plays through PlayAt (story scenes)
        foreach (var n in new[] { "lairBell", "lairWheel", "lairPyre", "lairBeam", "lairSnuff", "lairThunder", "lairChime", "lairDrip", "lairShard", "lairReveal" }) played.Add(n);
        var unknown = played.Where(p => !keys.Contains(p)).ToList();
        check(unknown.Count == 0, $"sounds: every sound the game plays has an entry ({played.Count} names)" + (unknown.Count > 0 ? " — unknown: " + string.Join(", ", unknown) : ""));
        // ambience: the theme table covers every scenery theme and names only beds that exist
        var themes = Regex.Matches(File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", "Scenery.cs")), "new Theme \\{ Name = \"").Count - 1; // minus the Titan's
        var bedTable = Regex.Match(src, "ThemeBeds =\\s*\\{([^}]*)\\}");
        var beds = Regex.Matches(bedTable.Groups[1].Value, "\"([a-z]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
        check(beds.Count >= themes && beds.All(keys.Contains), $"ambience: each of the {themes} arena themes has a bed that exists ({beds.Distinct().Count()} beds)");
        var heroes = Sigilbound.Core.GameData.Heroes;
        var noArmour = heroes.Where(h => !Sigilbound.Core.HeroArmour.Table.ContainsKey(h.Id)).Select(h => h.Id).ToList();
        var kinds = heroes.Select(h => Sigilbound.Core.HeroArmour.Of(h.Id, h.Role)).Distinct().Count();
        check(noArmour.Count == 0 && kinds == 4, $"armour: all {heroes.Count()} heroes have an armour class from their design, all 4 classes used" + (noArmour.Count > 0 ? " — missing: " + string.Join(", ", noArmour.Take(5)) : ""));
    }
}
