// Digitigrade legs (owner report 2026-11-28; blenderforge/i3d_legs.py, Client/DigitigradeLegs.cs): the
// animal-folk heroes whose meshes stand on reverse-knee legs carry metatarsal bones and, in their own GLB,
// every shared UBC clip re-solved for those legs (rotation and translation keys on the ten leg bones, same
// names and lengths as models/ubc_anims.glb). The game layers them over the shared clips on the legs only.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

static class DigitigradeTests
{
    // Heroes rigged with "legs": "digitigrade" (i3d_landmarks_portrait.py).
    static readonly string[] Expected = { "tansy", "corran" };
    static readonly string[] LegBones = { "thigh", "calf", "meta", "foot", "ball" };

    static string Find(params string[] rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(new[] { d.FullName }.Concat(rel).ToArray());
            if (File.Exists(p) || Directory.Exists(p)) return p;
        }
        return null;
    }

    static JsonDocument Glb(string f)
    {
        var b = File.ReadAllBytes(f); int n = BitConverter.ToInt32(b, 12);
        return JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(b, 20, n));
    }

    public static void Run(Action<bool, string> check)
    {
        var models = Find("unity", "Tools~", "models");
        var hd = models == null ? null : Path.Combine(models, "heroes_hd");
        var clipsFile = models == null ? null : Path.Combine(models, "ubc_anims.glb");
        if (hd == null || !Directory.Exists(hd) || !File.Exists(clipsFile))
        { bool ci = Environment.GetEnvironmentVariable("CI") == "true"; check(!ci, "digitigrade: models not found here" + (ci ? "" : " (skipped)")); return; }

        // the shared clips and their lengths
        var shared = new Dictionary<string, double>();
        using (var d = Glb(clipsFile))
        {
            var acc = d.RootElement.GetProperty("accessors");
            foreach (var a in d.RootElement.GetProperty("animations").EnumerateArray())
            {
                var name = a.GetProperty("name").GetString();
                if (name.Contains("TPose")) continue;
                double len = 0;
                foreach (var s in a.GetProperty("samplers").EnumerateArray())
                    len = Math.Max(len, acc[s.GetProperty("input").GetInt32()].GetProperty("max")[0].GetDouble());
                shared[name] = len;
            }
        }

        var digi = new List<string>();
        foreach (var f in Directory.GetFiles(hd, "SB_Hero_*_HD.glb").OrderBy(x => x))
        {
            var id = Path.GetFileName(f).Substring(8); id = id.Substring(0, id.Length - 7);
            using var d = Glb(f);
            var nodes = d.RootElement.GetProperty("nodes");
            var names = nodes.EnumerateArray().Select(n => n.TryGetProperty("name", out var nm) ? nm.GetString() : "").ToList();
            bool hasMeta = names.Contains("meta_l") || names.Contains("meta_r");
            bool hasAnims = d.RootElement.TryGetProperty("animations", out var anims) && anims.GetArrayLength() > 0;
            if (!hasMeta)
            {
                if (hasAnims) check(false, $"digitigrade: {id} carries animations but no metatarsal bones");
                continue;
            }
            digi.Add(id);
            // skeleton: both metatarsals, each between its calf and foot
            bool chain = true;
            foreach (var s in new[] { "l", "r" })
            {
                int Idx(string n) => names.IndexOf(n);
                bool Child(string parent, string child) => Idx(parent) >= 0 && Idx(child) >= 0 && nodes[Idx(parent)].TryGetProperty("children", out var ch) && ch.EnumerateArray().Any(x => x.GetInt32() == Idx(child));
                chain &= Child("thigh_" + s, "calf_" + s) && Child("calf_" + s, "meta_" + s) && Child("meta_" + s, "foot_" + s) && Child("foot_" + s, "ball_" + s);
            }
            check(chain, $"digitigrade: {id} legs are thigh > calf > meta > foot > ball on both sides");
            // clips: every shared clip, same length, keys on every leg bone and on nothing else
            var acc = d.RootElement.GetProperty("accessors");
            var legNodes = new HashSet<int>(new[] { "l", "r" }.SelectMany(s => LegBones.Select(b => names.IndexOf(b + "_" + s))));
            var got = new Dictionary<string, (double len, bool legsOnly, int rot, int pos)>();
            if (hasAnims)
                foreach (var a in anims.EnumerateArray())
                {
                    var samplers = a.GetProperty("samplers");
                    double len = 0; bool only = true; var rot = new HashSet<int>(); var pos = new HashSet<int>();
                    foreach (var c in a.GetProperty("channels").EnumerateArray())
                    {
                        int node = c.GetProperty("target").GetProperty("node").GetInt32(); var path = c.GetProperty("target").GetProperty("path").GetString();
                        only &= legNodes.Contains(node);
                        if (path == "rotation") rot.Add(node); else if (path == "translation") pos.Add(node);
                        var s = samplers[c.GetProperty("sampler").GetInt32()];
                        len = Math.Max(len, acc[s.GetProperty("input").GetInt32()].GetProperty("max")[0].GetDouble());
                    }
                    got[a.GetProperty("name").GetString()] = (len, only, rot.Count(legNodes.Contains), pos.Count(legNodes.Contains));
                }
            var missing = shared.Keys.Where(k => !got.ContainsKey(k)).ToList();
            check(missing.Count == 0, $"digitigrade: {id} has a baked leg clip for each of the {shared.Count} shared clips" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing.Take(5)) : ""));
            var badLen = got.Where(g => shared.TryGetValue(g.Key, out var l) && Math.Abs(l - g.Value.len) > 1e-3).Select(g => g.Key).ToList();
            check(badLen.Count == 0, $"digitigrade: {id} leg clips are as long as the shared clips (the game syncs their time)" + (badLen.Count > 0 ? " — not: " + string.Join(", ", badLen.Take(5)) : ""));
            var badKeys = got.Where(g => !g.Value.legsOnly || g.Value.rot != legNodes.Count || g.Value.pos != legNodes.Count).Select(g => g.Key).ToList();
            check(badKeys.Count == 0, $"digitigrade: {id} leg clips key rotation and position of all {legNodes.Count} leg bones and nothing else" + (badKeys.Count > 0 ? " — not: " + string.Join(", ", badKeys.Take(5)) : ""));
        }
        var lost = Expected.Where(e => File.Exists(Path.Combine(hd, $"SB_Hero_{e}_HD.glb")) && !digi.Contains(e)).ToList();
        check(lost.Count == 0, $"digitigrade: {string.Join(", ", Expected)} are rigged with digitigrade legs" + (lost.Count > 0 ? " — not: " + string.Join(", ", lost) : ""));

        // the game side: the layer component is wired, and the rest-pose fix leaves the baked legs alone
        var client = Find("unity", "Assets", "Sigilbound", "Client");
        if (client != null)
        {
            var models_ = File.ReadAllText(Path.Combine(client, "HeroModels.cs"));
            var legs = File.Exists(Path.Combine(client, "DigitigradeLegs.cs")) ? File.ReadAllText(Path.Combine(client, "DigitigradeLegs.cs")) : "";
            var rig = File.ReadAllText(Path.Combine(client, "RigExtras.cs"));
            check(models_.Contains("DigitigradeLegs.Needed(holder)") && legs.Contains("AddMixingTransform") && legs.Contains("tw.time = s.time"),
                "digitigrade: HeroModels layers the hero's leg clips over the shared clips (legs only, time-synced)");
            check(rig.Contains("DigitigradeLegs.LegBone"), "digitigrade: RigExtras doesn't re-express the shared clips on baked digitigrade legs");
        }
    }
}
