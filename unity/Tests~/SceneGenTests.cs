// The procedural campaign scenes (Core/SceneGen.cs, docs/sigilbound/art/SCENERY.md section 8): for every stage of
// both acts at every quality tier, nothing stands in the hero lane or blocks it from a guarded camera, foreground
// stays at the screen's edges, every piece is seen by some camera, the landmark is framed in the default view, the
// lair reveal sees its landmark, the budget holds and the lane reads against the heroes. `-- scenes OUT.json` dumps
// the layouts for the Blender previews (Tools~/scenery/scene_preview.py).
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Sigilbound.Core;

static class SceneGenTests
{
    static IEnumerable<(int c, int s)> Stages() { for (int c = 0; c < GameData.Chapters.Length; c++) for (int s = 0; s < GameData.StagesPerChapter; s++) yield return (c, s); }

    public static void Run(Action<bool, string> check)
    {
        var t0 = DateTime.Now;
        var lane = new List<string>(); var block = new List<string>(); var fore = new List<string>(); var unseen = new List<string>();
        var frame = new List<string>(); var dropped = new List<string>(); var budget = new List<string>(); var reveal = new List<string>(); var terrain = new List<string>();
        int layouts = 0, items = 0; var tierTris = new int[4];
        foreach (var (c, s) in Stages())
            for (int tier = 0; tier < 4; tier++)
                foreach (bool hard in tier == 2 ? new[] { false, true } : new[] { false })
                {
                    var L = SceneGen.Generate(c, s, hard, tier);
                    layouts++; items += L.Items.Count; tierTris[tier] = Math.Max(tierTris[tier], L.Tris);
                    string id = $"{c + 1}-{s + 1}{(hard ? "H" : "")}/t{tier}";
                    dropped.AddRange(L.Dropped.Select(d => d + "/t" + tier));
                    if (L.Tris > SceneGen.TriBudget[tier]) budget.Add($"{id} {L.Tris}");
                    if (L.Landmark == null || SceneRules.FrameError(L, L.Landmark, L.LandmarkTop) > 1e-9) frame.Add(id + (L.Landmark == null ? " none" : $" {L.Landmark.Mesh} ({L.Landmark.X:0.0},{L.Landmark.Z:0.0}) err {SceneRules.FrameError(L, L.Landmark, L.LandmarkTop):0.00}"));
                    var guarded = L.Cams.Where(k => k.Guard).ToList();
                    foreach (var p in L.Items)
                    {
                        var v = p.Volume();
                        if (SceneCameras.InLane(p.X, p.Z, v.R * 0.85)) lane.Add($"{id} {p.Mesh}");
                        if (!L.Cams.Any(k => SceneCameras.Sees(k, v)) && p.Layer != Layer.LandmarkPart) unseen.Add($"{id} {p.Mesh}");
                        foreach (var k in guarded)
                        {
                            if (SceneCameras.LanePoints().Any(t => SceneCameras.Blocks(k.View.Eye, t, v))) { block.Add($"{id} {p.Mesh} from {k.Name}"); break; }
                            double near = k.LaneNear();
                            if (SceneCameras.Samples(v).Select(q => SceneCameras.Project(k.View, k.Aspect, q)).Any(q => q != null && q.Value.depth < near && Math.Abs(q.Value.x) < SceneCameras.EdgeBand && Math.Abs(q.Value.y) <= 1.05))
                            { fore.Add($"{id} {p.Mesh} in {k.Name}"); break; }
                        }
                        if (L.Cams.Any(k => k.Reveal) && p.Layer != Layer.Landmark && p.Layer != Layer.LandmarkPart && p.Mesh != L.Landmark.Mesh)
                            foreach (var k in L.Cams.Where(k => k.Reveal))
                                if (SceneRules.RevealTargets(L).Any(t => SceneCameras.Blocks(k.View.Eye, t, v))) { reveal.Add($"{id} {p.Mesh}"); break; }
                    }
                    // the ground itself: flat in the lane, and no rise between a guarded camera and the lane
                    if (tier == 2 && !hard)
                    {
                        if (SceneCameras.LanePoints().Any(t => L.Height((float)t.X, (float)t.Z) > 0.001f)) terrain.Add(id + " lane not flat");
                        foreach (var k in guarded)
                            foreach (var t in SceneCameras.LanePoints().Where(t => t.Y < 1))
                            {
                                bool hit = false;
                                for (int i = 1; i < 24 && !hit; i++)
                                {
                                    var q = k.View.Eye + (t - k.View.Eye) * (i / 24.0);
                                    if (q.Y < L.Height((float)q.X, (float)q.Z) + 0.05) hit = true;
                                }
                                if (hit) { terrain.Add($"{id} ground blocks {k.Name}"); goto nextStage; }
                            }
                        nextStage:;
                    }
                }
        string Few(List<string> l) => l.Count == 0 ? "" : $" — {l.Count}: " + string.Join("; ", l.Distinct().Take(5));
        check(lane.Count == 0, $"scenes: nothing stands in the hero lane ({layouts} layouts, {items} placements){Few(lane)}");
        check(block.Count == 0, "scenes: nothing blocks the lane from Classic, Overhead, Cinematic or Three-quarter (portrait, 16:9, 19.5:9)" + Few(block));
        check(fore.Count == 0, "scenes: anything nearer the camera than the lane sits only in the screen's outer bands" + Few(fore));
        check(unseen.Count == 0, "scenes: every placement is inside some camera's view (none wasted off-screen)" + Few(unseen));
        check(frame.Count == 0, "scenes: every stage's landmark (the lair or its copy ahead) is framed in the default camera's background band at every aspect" + Few(frame));
        check(reveal.Count == 0, "scenes: the lair reveal's camera path sees its landmark unblocked" + Few(reveal));
        check(dropped.Count == 0, "scenes: every story set-piece finds a legal spot" + Few(dropped));
        check(budget.Count == 0, $"scenes: scenery triangles within the tier budgets (worst {string.Join("/", tierTris)} of {string.Join("/", SceneGen.TriBudget)})" + Few(budget));
        check(terrain.Count == 0, "scenes: the ground is flat in the lane and never rises between a guarded camera and the lane" + Few(terrain));

        // the same stage gives the same scene; different stops differ
        var a = SceneGen.Generate(1, 2); var b = SceneGen.Generate(1, 2); var d = SceneGen.Generate(1, 3);
        check(a.Items.Count == b.Items.Count && a.Items.Zip(b.Items, (x, y) => x.Mesh == y.Mesh && x.X == y.X && x.Z == y.Z).All(z => z)
              && !(a.Items.Count == d.Items.Count && a.Items.Zip(d.Items, (x, y) => x.Mesh == y.Mesh && x.X == y.X).All(z => z)), "scenes: seeded per stage (same stage, same scene; next stop, a different one)");
        // the route closes in on the lair: the landmark is nearer at the lair than on the first two stops
        var far = new List<string>();
        for (int c = 0; c < GameData.Chapters.Length; c++)
        {
            double D(int s) { var m = SceneGen.Generate(c, s).Landmark; return Math.Sqrt(m.X * m.X + (m.Z - 0.6) * (m.Z - 0.6)); }
            if (!(D(5) < D(0) && D(5) < D(1) && D(4) <= D(0) + 0.01)) far.Add($"{c + 1} ({D(0):0}, {D(1):0}, {D(4):0}, {D(5):0} m)");
        }
        check(far.Count == 0, "scenes: every chapter's landmark draws nearer along the route (stops 1-2 far, 5 close, the lair at its foot)" + Few(far));
        // readability (art review M2): no pitch-black lane, and value contrast against a hero's mid-tone on every stage
        var dark = new List<string>();
        foreach (var (c, s) in Stages())
            foreach (bool hard in new[] { false, true })
            {
                var (g, h) = SceneGen.LaneValues(c, s, hard);
                var li = SceneGen.LightOf(c, s, hard, false);
                if (g < 14 || Math.Abs(h - g) < 12 || li.Sun < 0.85f || li.Ambient < 0.5f) dark.Add($"{c + 1}-{s + 1}{(hard ? "H" : "")} L*{g:0}/{h:0}");
            }
        check(dark.Count == 0, "scenes: the lane reads on every stage, night included (ground L* >= 14, 12+ L* from a hero mid-tone, hero light floor)" + Few(dark));
        // other modes (arena, trials, menus) use the chapter's ground without a stop: same rules, no landmark
        var other = new List<string>();
        for (int c = 0; c < Biomes.Count; c++)
        {
            var L = SceneGen.Generate(c, -1);
            foreach (var p in L.Items) if (SceneCameras.InLane(p.X, p.Z, p.Volume().R * 0.85) || L.Cams.Where(k => k.Guard).Any(k => SceneCameras.LanePoints().Any(t => SceneCameras.Blocks(k.View.Eye, t, p.Volume())))) other.Add($"{c}:{p.Mesh}");
        }
        check(other.Count == 0, "scenes: arenas outside the campaign (no stop) follow the same lane rules" + Few(other));
        check(SceneMeshes.Variants("oak").SequenceEqual(new[] { "oak0", "oak1", "oak2" }) && SceneMeshes.Variants("stone").All(n => n.StartsWith("stone")), "scenes: kit variants by prefix");
        MeshTable(check);
        check(true, $"scenes: generated and checked in {(DateTime.Now - t0).TotalSeconds:0.0} s");
    }

    // The generated mesh table (SceneMeshes.Generated.cs) matches the shipped .bytes files.
    static void MeshTable(Action<bool, string> check)
    {
        string dir = AppContext.BaseDirectory, res = null;
        for (int i = 0; i < 8 && dir != null && res == null; i++, dir = Path.GetDirectoryName(dir))
        { var r = Path.Combine(dir, "Assets", "Resources", "Scenery"); if (Directory.Exists(r)) res = r; }
        if (res == null) { check(true, "scenes: Resources/Scenery not found (skipped the mesh table check)"); return; }
        var names = new HashSet<string>();
        foreach (var f in new[] { "props.bytes", "story_act1.bytes", "story_act2.bytes" })
        {
            var p = Path.Combine(res, f); if (!File.Exists(p)) continue;
            using var br = new BinaryReader(File.OpenRead(p));
            br.ReadChars(4); int count = br.ReadInt32();
            for (int m = 0; m < count; m++)
            {
                string name = new string(br.ReadChars(br.ReadByte())); int vc = br.ReadInt32(); int subs = br.ReadByte();
                var ic = new int[subs]; for (int s = 0; s < subs; s++) ic[s] = br.ReadInt32();
                br.ReadBytes(24 + vc * 11); foreach (var n in ic) br.ReadBytes(n * 2);
                names.Add(name);
            }
        }
        var lm = Directory.Exists(Path.Combine(res, "Landmarks")) ? Directory.GetFiles(Path.Combine(res, "Landmarks"), "*.bytes") : new string[0];
        foreach (var f in lm) names.Add(Path.GetFileNameWithoutExtension(f));
        var missing = names.Where(n => !SceneMeshes.Has(n)).Concat(SceneMeshes.All.Keys.Where(n => !names.Contains(n))).ToList();
        check(missing.Count == 0, $"scenes: the mesh footprint table lists exactly the shipped scenery meshes ({names.Count}; run Tools~/scenery/mesh_bounds.py)" + (missing.Count > 0 ? " — " + string.Join(", ", missing.Take(6)) : ""));
        long lmBytes = lm.Sum(f => new FileInfo(f).Length);
        check(lmBytes <= 8L * 1024 * 1024, $"apk size: image-to-3D landmarks {lmBytes / 1024} KB in {lm.Length} files (budget 8 MB)");
        check(SceneMeshes.All.Where(kv => kv.Value.Pack == "i3d").All(kv => kv.Value.Tris <= 4000), "scenes: every image-to-3D landmark is within the 4,000-triangle lair budget");
    }

    // `dotnet run -- scenes OUT.json [tier]`: every stage's layout for the previews.
    public static void Dump(string path, int tier)
    {
        var ci = CultureInfo.InvariantCulture; string F(double v) => v.ToString("0.###", ci);
        var sb = new StringBuilder("[\n");
        bool first = true;
        foreach (var (c, s) in Stages())
            foreach (bool hard in new[] { false })
            {
                var L = SceneGen.Generate(c, s, hard, tier); var b = L.Biome; var li = L.Light;
                if (!first) sb.Append(",\n"); first = false;
                sb.Append($"{{\"chapter\":{c},\"stage\":{s},\"hard\":{(hard ? "true" : "false")},\"name\":\"{StoryScenes.Of(c, s)?.Name}\",\"chapterName\":\"{GameData.Chapters[c]}\",\"biome\":\"{b.Name}\",\"tris\":{L.Tris},");
                sb.Append($"\"light\":{{\"mood\":\"{li.Mood}\",\"sun\":{F(li.Sun)},\"pitch\":{F(li.SunPitch)},\"yaw\":{F(li.SunYaw)},\"world\":{F(li.WorldTint)},\"lane\":{F(li.LaneTint)},\"dusk\":{F(li.Dusk)},\"fogNear\":{F(b.FogNear * li.FogK)},\"fogFar\":{F(b.FogFar * li.FogK)}}},");
                sb.Append($"\"water\":{F(L.WaterZ)},\"relief\":{F(L.Relief)},");
                sb.Append("\"path\":[" + string.Join(",", L.Path.Select(p => $"[{F(p.x)},{F(p.z)}]")) + "],");
                if (L.Landmark != null) sb.Append($"\"landmark\":{{\"mesh\":\"{L.Landmark.Mesh}\",\"x\":{F(L.Landmark.X)},\"z\":{F(L.Landmark.Z)},\"top\":{F(L.LandmarkTop)}}},");
                sb.Append("\"cams\":[" + string.Join(",", L.Cams.Where(k => k.Default || k.Reveal || k.Name.StartsWith("classic@1.78") || k.Name.StartsWith("front@1.78")).Select(k =>
                    $"{{\"name\":\"{k.Name}\",\"aspect\":{F(k.Aspect)},\"eye\":[{F(k.View.Eye.X)},{F(k.View.Eye.Y)},{F(k.View.Eye.Z)}],\"target\":[{F(k.View.Target.X)},{F(k.View.Target.Y)},{F(k.View.Target.Z)}],\"fov\":{F(k.View.Fov)},\"foot\":[" +
                    string.Join(",", SceneCameras.Footprint(k.View, k.Aspect, 45).Select(q => $"[{F(q.X)},{F(q.Z)}]")) + "]}")) + "],");
                sb.Append("\"items\":[" + string.Join(",", L.Items.Select(p =>
                    $"{{\"mesh\":\"{p.Mesh}\",\"x\":{F(p.X)},\"y\":{F(p.Y)},\"z\":{F(p.Z)},\"s\":{F(p.Scale)},\"yaw\":{F(p.Yaw)},\"layer\":\"{p.Layer}\",\"far\":{(p.Far ? "true" : "false")},\"glow\":\"{p.Glow}\",\"moved\":{(p.Moved ? "true" : "false")},\"r\":{F(p.R)}}}")) + "],");
                // terrain heights on a 46 x 46 grid over the 90 m field (row = z, column = x)
                var hs = new List<string>();
                for (int iz = 0; iz < 46; iz++) for (int ix = 0; ix < 46; ix++) hs.Add(F(L.Height(-45 + ix * 2f, -45 + iz * 2f)));
                sb.Append("\"heights\":[" + string.Join(",", hs) + "]}");
            }
        sb.Append("\n]\n");
        File.WriteAllText(path, sb.ToString());
        Console.WriteLine("wrote " + path);
    }
}
