using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

// Every sound name the client plays is in Sound.Files, and every file Sound.Files, Sound.Tracks and
// Sound.Beds name exists in Resources/Audio (made by Tools~/audio/make_real.py, make_sfx.py and
// make_music.py). Every arena theme has an ambience bed; every hero has an armour class (HeroArmour).
static class SoundTests
{
    // Channel count from an Ogg Vorbis identification header (byte 11 of the vorbis packet).
    static int OggChannels(string path)
    {
        var b = File.ReadAllBytes(path);
        for (int i = 0; i + 12 < b.Length && i < 512; i++)
            if (b[i] == 1 && b[i + 1] == 'v' && b[i + 2] == 'o' && b[i + 3] == 'r' && b[i + 4] == 'b' && b[i + 5] == 'i' && b[i + 6] == 's') return b[i + 11];
        return 0;
    }
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
        // music: every track the battle and menu code asks for is in Sound.Tracks with a stereo file
        var trackTable = Regex.Match(src, "Tracks = new Dictionary<string, string>\\s*\\{([^}]*)\\}").Groups[1].Value;
        var tracks = Regex.Matches(trackTable, "\\[\"([a-z0-9]+)\"\\] = \"([A-Za-z0-9_]+)\"").Cast<Match>().ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
        var wantTracks = new[] { "menu", "battle", "titan", "victory", "boss", "act2", "map" };
        var bc = File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", "BattleController.cs"));
        var asked = Regex.Matches(bc, "Sound\\.I\\?\\.(?:StartMusic|Music)\\(([^;]*?)\\);").Cast<Match>()
            .SelectMany(m => Regex.Matches(m.Groups[1].Value, "\"([a-z0-9]+)\"").Cast<Match>().Select(q => q.Groups[1].Value)).Distinct().ToList();
        var missingTracks = wantTracks.Concat(asked).Where(k => !tracks.ContainsKey(k) || !Directory.GetFiles(audio, tracks[k] + ".*").Any(f => !f.EndsWith(".meta"))).Distinct().ToList();
        check(missingTracks.Count == 0 && asked.Contains("boss") && asked.Contains("act2"),
            $"music: {tracks.Count} tracks, each with its file; battles ask for {string.Join(", ", asked)}" + (missingTracks.Count > 0 ? " — missing: " + string.Join(", ", missingTracks) : ""));
        var newMusic = new[] { "music_boss", "music_act2", "music_map" }.Select(f => Path.Combine(audio, f + ".ogg")).ToList();
        check(newMusic.All(f => File.Exists(f) && OggChannels(f) == 2), "music: the boss, act 2 and map themes are stereo OGG files");
        var heroes = Sigilbound.Core.GameData.Heroes;
        var noArmour = heroes.Where(h => !Sigilbound.Core.HeroArmour.Table.ContainsKey(h.Id)).Select(h => h.Id).ToList();
        var kinds = heroes.Select(h => Sigilbound.Core.HeroArmour.Of(h.Id, h.Role)).Distinct().Count();
        check(noArmour.Count == 0 && kinds == 4, $"armour: all {heroes.Count()} heroes have an armour class from their design, all 4 classes used" + (noArmour.Count > 0 ? " — missing: " + string.Join(", ", noArmour.Take(5)) : ""));
    }
}
