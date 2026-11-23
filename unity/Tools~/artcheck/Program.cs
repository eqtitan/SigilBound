// Offline hero art check: what the Unity import tool would report, measured from the files.
// .glb files are read fully (tris, bones, materials, embedded images and clips, humanoid mapping);
// .fbx files can only be listed here, Unity measures them on import.
// Exit code: 0 all heroes pass, 1 a hero fails, 2 usage error.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length < 1 || !Directory.Exists(args[0])) { Console.Error.WriteLine("usage: artcheck <folder> [report.md]"); return 2; }
        var files = Directory.GetFiles(args[0], "*", SearchOption.AllDirectories);
        var parsed = files.Select(f => (Path: f, P: ArtSpec.Parse(Path.GetFileName(f)))).ToList();
        foreach (var x in parsed.Where(x => x.P == null && !x.Path.EndsWith(".json") && !x.Path.EndsWith(".md")))
            Console.WriteLine($"ignored (not named per the convention): {Path.GetFileName(x.Path)}");
        var stats = new List<HeroAssetStats>();
        foreach (var id in parsed.Where(x => x.P?.Kind == "hero").Select(x => x.P.HeroId).Distinct().OrderBy(x => x))
        {
            var mine = parsed.Where(x => x.P != null && (x.P.HeroId == id || (x.P.Kind == "anim" && (x.P.Owner == null || x.P.Owner == id || x.P.Owner == ArtSpec.ArchetypeOf(id).ToString())))).ToList();
            var lod0 = mine.FirstOrDefault(x => x.P.Kind == "hero" && x.P.Lod == 0);
            HeroAssetStats s;
            if (lod0.Path != null && lod0.P.Ext == "glb")
            {
                var info = GlbInfo.Read(File.ReadAllBytes(lod0.Path));
                s = info.ToStats(id, Path.GetFileName(lod0.Path));
                // Sockets are created from the mapped bones on import.
                var map = HumanoidMap.Map(info.JointHierarchy());
                if (map.ContainsKey("RightHand")) s.Sockets.Add("Socket_HandR");
                if (map.ContainsKey("LeftHand")) s.Sockets.Add("Socket_HandL");
                if (map.ContainsKey("Head")) s.Sockets.Add("Socket_Head");
                if (map.ContainsKey("Spine")) s.Sockets.Add("Socket_Back");
                // Humanoid rigs use the shared library in Unity: count those clips too.
                if (s.MissingHumanBones.Count == 0 && s.HumanoidProblems.Count == 0)
                {
                    var lib = mine.Where(x => x.P.Kind == "anim").Select(x => x.P.Clip).ToList();
                    if (HeroClips.All.Where(c => c.Required).All(c => HeroClips.Effective(c.Name, lib) != null))
                    { s.Clips.Clear(); foreach (var c in lib) s.Clips.Add(c); s.AnimBackend = "humanoid-library"; }
                }
                // Per-hero clip files (SB_Anim_<id>_<state>.glb) count as the file's own clips.
                foreach (var a in mine.Where(x => x.P.Kind == "anim" && x.P.Owner == id)) s.Clips.Add(a.P.Clip);
            }
            else
            {
                s = new HeroAssetStats { HeroId = id, Source = lod0.Path != null ? Path.GetFileName(lod0.Path) : "-" };
                if (lod0.Path != null) { s.LodTris[0] = 0; s.Bones = 1; s.Notes.Add("FBX: measured by the Unity import tool, not here"); foreach (var c in HeroClips.All) s.Clips.Add(c.Name); foreach (var k in ArtSpec.RequiredSockets) s.Sockets.Add(k); }
            }
            foreach (var l in mine.Where(x => x.P.Kind == "hero" && x.P.Lod > 0 && x.P.Ext == "glb"))
                s.LodTris[l.P.Lod] = GlbInfo.Read(File.ReadAllBytes(l.Path)).Triangles;
            foreach (var t in mine.Where(x => x.P.Kind == "texture"))
            {
                var b = File.ReadAllBytes(t.Path);
                var (w, h) = GlbInfo.ImageSize(b, 0, b.Length);
                s.Textures.Add((Path.GetFileNameWithoutExtension(t.Path), w, h));
            }
            if (s.Textures.Count > 0 && mine.Any(x => x.P.Kind == "texture" && x.P.Map == "Albedo"))
                s.Textures.RemoveAll(x => !x.Name.StartsWith("T_Hero_")); // the hero's own maps replace embedded ones
            foreach (var g in mine.Where(x => x.P.Kind == "gear")) s.Notes.Add($"gear: {Path.GetFileName(g.Path)}");
            stats.Add(s);
        }
        var md = ArtValidation.Markdown(stats, $"Offline hero art check: {Path.GetFileName(Path.GetFullPath(args[0]).TrimEnd('/'))}");
        if (args.Length > 1) File.WriteAllText(args[1], md);
        Console.WriteLine(md);
        int fails = stats.Count(s => !ArtValidation.Passes(ArtValidation.Validate(s)));
        Console.WriteLine($"{stats.Count} hero(es), {fails} failing.");
        return stats.Count == 0 ? 2 : fails > 0 ? 1 : 0;
    }
}
