// Story scenes (PLAN step 85, Core/StoryScenes.cs): every chapter has six stops ending in its lair, every
// piece's mesh exists in the base kit or that act's story pack, and no piece intrudes on the battle ring,
// the camera side or the near ridge. Also the act 1 story lines and the two new chapter themes.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class StoryScenesTests
{
    public static void Run(System.Action<bool, string> check)
    {
        var sets = StoryScenes.Sets;
        check(sets.Length == GameData.Chapters.Length && sets.All(c => c.Length == GameData.StagesPerChapter && c.All(s => s != null && !string.IsNullOrEmpty(s.Name) && s.Pieces.Length > 0)),
            $"story: {sets.Length} chapters x {GameData.StagesPerChapter} named stops, each with set-pieces");
        check(Enumerable.Range(0, GameData.TotalStages).All(i => StoryScenes.StopName(i) != null), "story: every campaign stage has a stop name");
        check(sets.All(c => c[GameData.StagesPerChapter - 1].Pieces.Any(p => p.Fx != LairFx.None && p.Fx != LairFx.Vent)), "story: every lair (stage 6) has a set-piece that joins the boss interactions");
        check(sets.All(c => c[GameData.StagesPerChapter - 1].Pieces.All(p => !p.Far)), "story: lairs are fought at the landmark (no distant copy)");
        check(sets.SelectMany(c => c).SelectMany(s => s.Pieces).Where(p => p.Glow != null).All(p => StoryScenes.GlowHex(p.Glow) != null), "story: every glow key has a colour");
        check(sets.SelectMany(c => c).SelectMany(s => s.Pieces).Where(p => p.Fx != LairFx.None && p.Fx != LairFx.Sway && p.Fx != LairFx.Vent).All(p => StoryScenes.CueOf(p.Fx) != null),
            "story: every lair behaviour raises a sound cue");

        // Clear of the ring (the scatter's ellipse, grown by the piece's footprint), the camera side and the near ridge (z 28).
        var bad = new List<string>();
        for (int c = 0; c < sets.Length; c++)
            for (int s = 0; s < sets[c].Length; s++)
                foreach (var p in sets[c][s].Pieces)
                {
                    double ex = p.X / (6.2 + p.R), ez = (p.Z - 0.6) / (5.6 + p.R);
                    bool ring = ex * ex + ez * ez < 1, camera = p.Z - p.R < -1 && System.Math.Abs(p.X) < 9, ridge = p.Z + (p.Far ? 0 : p.R) > 27 || System.Math.Abs(p.X) > 26;
                    if (ring || camera || ridge) bad.Add($"{c + 1}-{s + 1} {p.Mesh}{(ring ? " ring" : "")}{(camera ? " camera" : "")}{(ridge ? " ridge" : "")}");
                }
        check(bad.Count == 0, "story: every piece stays clear of the ring, the camera side and the near ridge" + (bad.Count > 0 ? " — " + string.Join(", ", bad.Take(6)) : ""));

        // Every mesh exists: the base kit (props.bytes) or the chapter's act pack (story_actN.bytes).
        string dir = System.AppContext.BaseDirectory, res = null;
        for (int i = 0; i < 8 && dir != null && res == null; i++, dir = Path.GetDirectoryName(dir))
        { var r = Path.Combine(dir, "Assets", "Resources", "Scenery"); if (Directory.Exists(r)) res = r; }
        if (res == null) { check(true, "story: Resources/Scenery not found here (skipped mesh checks)"); }
        else
        {
            var kit = Names(Path.Combine(res, "props.bytes"));
            var packs = new[] { Names(Path.Combine(res, "story_act1.bytes")), Names(Path.Combine(res, "story_act2.bytes")) };
            var missing = new List<string>();
            for (int c = 0; c < sets.Length; c++)
                foreach (var p in sets[c].SelectMany(s => s.Pieces))
                    if (!kit.Contains(p.Mesh) && !packs[GameData.ActOf(c) - 1].Contains(p.Mesh)) missing.Add($"{c + 1}:{p.Mesh}");
            check(missing.Count == 0, $"story: every piece's mesh is in the kit or its act's pack ({kit.Count} kit, {packs[0].Count} + {packs[1].Count} story meshes)" + (missing.Count > 0 ? " — missing " + string.Join(", ", missing.Distinct().Take(6)) : ""));
            long bytes = new[] { "story_act1.bytes", "story_act2.bytes" }.Sum(f => File.Exists(Path.Combine(res, f)) ? new FileInfo(Path.Combine(res, f)).Length : 0);
            check(bytes > 0 && bytes <= 900 * 1024, $"apk size: story packs {bytes / 1024} KB (budget 900 KB)");
        }

        check(Enumerable.Range(0, GameData.Chapters.Length).All(c => !string.IsNullOrEmpty(GameData.StoryOf(c))), "story: every chapter (acts 1 and 2) has its story line");
        check(GameData.ThemeOf(5) == GameData.GlassTheme && GameData.ThemeOf(6) == GameData.CragTheme
              && Enumerable.Range(0, GameData.Chapters.Length).Where(c => c != 5 && c != 6).All(c => GameData.ThemeOf(c) == (c < 10 ? c : 10 + (c - 10) % 5)),
            "story: The Glass Wastes and Stormcrown Heights have their own themes; every other chapter keeps its theme");
    }

    // Mesh names in a kit file (Client/SceneryKit.Parse format).
    static HashSet<string> Names(string path)
    {
        var set = new HashSet<string>();
        if (!File.Exists(path)) return set;
        using var br = new BinaryReader(File.OpenRead(path));
        if (new string(br.ReadChars(4)) != "SBP1") return set;
        int count = br.ReadInt32();
        for (int m = 0; m < count; m++)
        {
            string name = new string(br.ReadChars(br.ReadByte()));
            int vc = br.ReadInt32(); int subs = br.ReadByte();
            var ic = new int[subs]; for (int s = 0; s < subs; s++) ic[s] = br.ReadInt32();
            br.ReadBytes(24 + vc * 11);
            foreach (var n in ic) br.ReadBytes(n * 2);
            set.Add(name);
        }
        return set;
    }
}
