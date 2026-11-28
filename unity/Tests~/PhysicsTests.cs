// Physics standard (docs/sigilbound/art/PHYSICS.md; Core/Physics/ColliderSet.cs, Client/BodyColliders.cs,
// Client/SpringBones.cs): every shipped hero gets a complete body collider set of sane size, every spring chain is
// well formed, within the per-hero budget and outside its kind's colliders at rest, the multi-kind runtime is
// deterministic and cheap enough for a full battle, Low quality (and over-budget heroes) run kinematic, and the
// ground keeps chains above the floor.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilbound.Core;
using Sigilbound.Core.Physics;

static class PhysicsTests
{
    // Rest pose of a shipped GLB: node name -> model-space position and rotation (all node scales are 1).
    public sealed class Rest
    {
        public readonly Dictionary<string, SVec> Pos = new Dictionary<string, SVec>();
        public readonly Dictionary<string, SQuat> Rot = new Dictionary<string, SQuat>();
        public readonly Dictionary<string, string> Parent = new Dictionary<string, string>();
        public readonly List<string> Names = new List<string>();
        public SVec? P(string n) => n != null && Pos.TryGetValue(n, out var p) ? p : (SVec?)null;
    }

    public static Rest LoadRest(string file)
    {
        var bytes = File.ReadAllBytes(file); int n = BitConverter.ToInt32(bytes, 12);
        using var d = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, n));
        var nodes = d.RootElement.GetProperty("nodes").EnumerateArray().ToList();
        var parent = new int[nodes.Count]; for (int i = 0; i < parent.Length; i++) parent[i] = -1;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i].TryGetProperty("children", out var ch)) foreach (var c in ch.EnumerateArray()) parent[c.GetInt32()] = i;
        var r = new Rest(); var gp = new SVec[nodes.Count]; var gr = new SQuat[nodes.Count]; var done = new bool[nodes.Count];
        string Name(int i) => nodes[i].TryGetProperty("name", out var nm) ? nm.GetString() : "n" + i;
        void Visit(int i)
        {
            if (done[i]) return;
            if (parent[i] >= 0) Visit(parent[i]);
            var t = nodes[i].TryGetProperty("translation", out var tt) ? new SVec(tt[0].GetSingle(), tt[1].GetSingle(), tt[2].GetSingle()) : new SVec(0, 0, 0);
            var q = nodes[i].TryGetProperty("rotation", out var rr) ? new SQuat(rr[0].GetSingle(), rr[1].GetSingle(), rr[2].GetSingle(), rr[3].GetSingle()) : SQuat.Identity;
            if (parent[i] >= 0) { gp[i] = gp[parent[i]] + gr[parent[i]].Rotate(t); gr[i] = (gr[parent[i]] * q).Normalized; }
            else { gp[i] = t; gr[i] = q.Normalized; }
            done[i] = true;
        }
        for (int i = 0; i < nodes.Count; i++)
        {
            Visit(i); var nm = Name(i); r.Names.Add(nm);
            if (!r.Pos.ContainsKey(nm)) { r.Pos[nm] = gp[i]; r.Rot[nm] = gr[i]; r.Parent[nm] = parent[i] >= 0 ? Name(parent[i]) : null; }
        }
        return r;
    }

    static Dictionary<string, JsonElement> _ov;
    public static Dictionary<string, JsonElement> Overrides(string models)
    {
        if (_ov != null) return _ov;
        _ov = new Dictionary<string, JsonElement>();
        var f = Path.Combine(models, "..", "physics", "overrides.json");
        if (!File.Exists(f)) return _ov;
        using var d = JsonDocument.Parse(File.ReadAllText(f));
        foreach (var p in d.RootElement.EnumerateObject()) if (!p.Name.StartsWith("_")) _ov[p.Name] = p.Value.Clone();
        return _ov;
    }

    static float SegDist(SVec p, SVec a, SVec b)
    {
        var ab = b - a; float l2 = SVec.Dot(ab, ab);
        float t = l2 > 1e-12f ? Math.Max(0f, Math.Min(1f, SVec.Dot(p - a, ab) / l2)) : 0f;
        return (p - (a + ab * t)).Length;
    }

    // A test rig: chains of 3 joints hanging from one anchor, spread across x (a cape behind a torso).
    static SpringSolver Sheet(int chains, SpringRig rig, float z = -0.18f)
    {
        var par = new List<int>(); var anc = new List<int>(); var lp = new List<SVec>(); var lr = new List<SQuat>(); var ax = new List<SVec>();
        for (int c = 0; c < chains; c++)
        {
            float x = -0.15f + 0.3f * c / Math.Max(1, chains - 1);
            for (int s = 0; s < 3; s++)
            {
                par.Add(s == 0 ? -1 : par.Count - 1); anc.Add(s == 0 ? 0 : -1);
                lp.Add(s == 0 ? new SVec(x, 0f, z) : new SVec(0, -0.3f, -0.01f)); lr.Add(SQuat.Identity); ax.Add(new SVec(0, -0.3f, -0.01f));
            }
        }
        return new SpringSolver(par.ToArray(), anc.ToArray(), lp.ToArray(), lr.ToArray(), ax.ToArray(), rig);
    }

    public static void Run(Action<bool, string> check)
    {
        // 1. kinds and budgets are consistent
        check(ChainKinds.Of(0) == ChainKind.Skirt && ChainKinds.Of(20) == ChainKind.Cape && ChainKinds.Of(30) == ChainKind.Tail && ChainKinds.Of(35) == ChainKind.Wing
              && ChainKinds.Of(40) == ChainKind.Hair && ChainKinds.Of(50) == ChainKind.Ear && ChainKinds.Of(55) == ChainKind.Flap
              && Enum.GetValues(typeof(ChainKind)).Cast<ChainKind>().All(k => ChainKinds.Of(ChainKinds.First(k)) == k),
              "physics: chain numbers map to kinds (0 skirt, 20 cape, 30 tail, 35 wing, 40 hair, 50 ear, 55 flap)");
        check(Enum.GetValues(typeof(ChainKind)).Cast<ChainKind>().All(k => (ChainKinds.Mask(k) & (BodyGroup.Arms | BodyGroup.Forearms)) == 0 || k == ChainKind.Hair || k == ChainKind.Wing),
              "physics: only hair and wings collide with the arms (cloth never catches on a swinging arm)");
        check(ChainKinds.BattleBudget >= 10 * ChainKinds.MaxJointsPerHero / 2 && ChainKinds.MaxJointsPerHero <= 64, $"physics: budgets {ChainKinds.MaxJointsPerHero} joints per hero, {ChainKinds.BattleBudget} per battle");

        // 2. the fallback set (a hero without a data line) is complete and inside the sane bands
        {
            var fake = new Dictionary<string, SVec>
            {
                ["pelvis"] = new SVec(0, 0.95f, 0), ["spine_02"] = new SVec(0, 1.2f, 0), ["neck_01"] = new SVec(0, 1.52f, 0), ["Head"] = new SVec(0, 1.6f, 0.02f),
                ["upperarm_l"] = new SVec(0.19f, 1.46f, 0), ["lowerarm_l"] = new SVec(0.46f, 1.46f, 0), ["hand_l"] = new SVec(0.72f, 1.46f, 0),
                ["upperarm_r"] = new SVec(-0.19f, 1.46f, 0), ["lowerarm_r"] = new SVec(-0.46f, 1.46f, 0), ["hand_r"] = new SVec(-0.72f, 1.46f, 0),
                ["thigh_l"] = new SVec(0.09f, 0.97f, 0), ["calf_l"] = new SVec(0.09f, 0.54f, 0), ["foot_l"] = new SVec(0.09f, 0.09f, 0),
                ["thigh_r"] = new SVec(-0.09f, 0.97f, 0), ["calf_r"] = new SVec(-0.09f, 0.54f, 0), ["foot_r"] = new SVec(-0.09f, 0.09f, 0),
            };
            SVec? P(string n) => n != null && fake.TryGetValue(n, out var p) ? p : (SVec?)null;
            var miss = new List<string>(); var set = ColliderSet.Build("no-such-hero", P, miss);
            var a = ColliderSet.Build("no-such-hero", P);
            bool same = set.Length == a.Length && set.Zip(a, (x, y) => x.Name == y.Name && x.Radius == y.Radius && x.Offset.X == y.Offset.X && x.Offset.Y == y.Offset.Y).All(z => z);
            check(set.Length == 13 && miss.Count == 0 && same, $"physics: fallback body set has all 13 shapes, deterministic ({string.Join(",", set.Select(s => s.Name))})");
            var R = ColliderSet.Radii("no-such-hero", P);
            bool inBand = true;
            for (int i = 0; i < R.Length; i++) { float L = ColliderSet.RefLength(i, P); if (i != 1 && (R[i] < ColliderSet.BandLo[i] * L - 1e-4f || R[i] > ColliderSet.BandHi[i] * L + 1e-4f)) inBand = false; }
            check(inBand, "physics: fallback radii inside the sane bands");
            fake.Remove("calf_l"); miss.Clear(); var part = ColliderSet.Build("no-such-hero", P, miss);
            check(miss.SequenceEqual(new[] { "calf_l" }) && part.All(s => s.BoneA != "calf_l" && s.BoneB != "calf_l") && part.Length == 11, "physics: a missing bone drops only its own shapes and is reported");
        }

        // 3. the ground sphere keeps a falling sheet above the floor (dynamic and kinematic)
        foreach (bool kin in new[] { false, true })
        {
            var s = Sheet(3, ChainKinds.Defaults(ChainKind.Cape)); var ar = new SQuat[1]; var ap = new SVec[1];
            var caps = new[] { ChainKinds.Ground() };
            float low = 9;
            for (int f = 0; f < 240; f++)
            {   // the anchor (a lying hero's upper back) sinks from 1.4 m to 0.12 m and tips over onto its back
                float t = Math.Min(1f, f / 120f); float ang = -1.5f * t;
                ar[0] = new SQuat((float)Math.Sin(ang / 2), 0, 0, (float)Math.Cos(ang / 2)); ap[0] = new SVec(0, 1.4f - 1.28f * t, 0);
                s.Update(1 / 60f, ar, ap, caps, 1, kin);
                if (f > 150) for (int i = 0; i < s.Joints; i++) low = Math.Min(low, s.Tail(i).Y);
            }
            check(low > -0.005f, $"physics: ground keeps chains above the floor ({(kin ? "kinematic" : "dynamic")}, lowest tail {low * 100:0.0} cm)");
        }

        // 4. cost: a full-budget hero (48 joints) against a full body set (16 shapes + ground), and a whole battle
        {
            var rig = ChainKinds.Defaults(ChainKind.Cape); var s = Sheet(16, rig);
            var caps = new SCapsule[17]; var ar = new[] { SQuat.Identity }; var ap = new[] { new SVec(0, 1.3f, 0) };
            for (int i = 0; i < 16; i++) caps[i] = new SCapsule { A = new SVec(-0.2f + 0.025f * i, 0.2f + 0.08f * i, 0), B = new SVec(-0.2f + 0.025f * i, 0.3f + 0.08f * i, 0.05f), R = 0.08f };
            caps[16] = ChainKinds.Ground();
            check(s.Joints == ChainKinds.MaxJointsPerHero, $"physics: cost rig has {s.Joints} joints");
            for (int f = 0; f < 200; f++) { ap[0] = new SVec(0.1f * (float)Math.Sin(f * 0.1), 1.3f, 0); s.Update(1 / 60f, ar, ap, caps, 17, false); }
            double us = double.MaxValue;
            for (int rep = 0; rep < 3; rep++)
            {
                var sw = Stopwatch.StartNew();
                for (int f = 0; f < 5000; f++) { ap[0] = new SVec(0.1f * (float)Math.Sin(f * 0.1), 1.3f, 0); s.Update(1 / 60f, ar, ap, caps, 17, false); }
                sw.Stop(); us = Math.Min(us, sw.Elapsed.TotalMilliseconds * 1000 / 5000);
            }
            double battle = us * ChainKinds.BattleBudget / ChainKinds.MaxJointsPerHero;
            check(us < 150, $"physics: one full-budget hero (48 joints x 17 colliders) costs {us:0.0} us per frame here (budget 150 us)");
            check(battle < 1500, $"physics: a full battle at the budget ({ChainKinds.BattleBudget} joints) costs {battle / 1000:0.00} ms per frame here (budget 1.5 ms; a phone core is ~4x slower, Low quality runs kinematic)");
            var sk = Sheet(16, rig); double ukin = double.MaxValue;
            for (int rep = 0; rep < 3; rep++) { var sw = Stopwatch.StartNew(); for (int f = 0; f < 5000; f++) sk.Update(1 / 60f, ar, ap, caps, 17, true); sw.Stop(); ukin = Math.Min(ukin, sw.Elapsed.TotalMilliseconds * 1000 / 5000); }
            check(ukin <= us * 1.2 && sk.StepsTaken == 0, $"physics: Low quality (kinematic) costs {ukin:0.0} us, no simulation steps");
        }

        // 5. Low quality: no history (the pose depends on this frame only) with the body set
        {
            var s1 = Sheet(4, ChainKinds.Defaults(ChainKind.Cape)); var s2 = Sheet(4, ChainKinds.Defaults(ChainKind.Cape));
            var caps = new[] { new SCapsule { A = new SVec(0, 0.9f, -0.05f), B = new SVec(0, 1.4f, -0.05f), R = 0.14f }, ChainKinds.Ground() };
            var ar = new[] { SQuat.Identity };
            for (int f = 0; f < 100; f++) s1.Update(1 / 60f, ar, new[] { new SVec(0.2f * (float)Math.Sin(f * 0.3), 1.4f, 0) }, caps, 2, true);
            var ap = new[] { new SVec(0.05f, 1.4f, 0) };
            s1.Update(1 / 60f, ar, ap, caps, 2, true); s2.Update(1 / 60f, ar, ap, caps, 2, true);
            bool same = true; for (int i = 0; i < s1.Joints; i++) { var a = s1.Local[i]; var b = s2.Local[i]; same &= a.X == b.X && a.Y == b.Y && a.Z == b.Z && a.W == b.W; }
            check(same, "physics: Low quality (kinematic) with body colliders keeps no history");
        }

        // 6. every shipped hero: complete body set (creatures excepted), sane sizes, data line; chains in budget and outside at rest
        string models = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        { var p = Path.Combine(dir.FullName, "unity", "Tools~", "models", "heroes_hd"); if (Directory.Exists(p)) { models = p; break; } }
        if (models == null) { bool ci = Environment.GetEnvironmentVariable("CI") == "true"; check(!ci, "physics: models not found here" + (ci ? "" : " (skipped)")); return; }
        int heroes = 0, complete = 0, sane = 0, data = 0, rigged = 0; var bad = new List<string>(); var noData = new List<string>();
        var ov = Overrides(models);
        foreach (var f in Directory.GetFiles(models, "SB_Hero_*_HD.glb").OrderBy(x => x))
        {
            var id = Path.GetFileName(f).Substring(8); id = id.Substring(0, id.Length - 7);
            if (Creatures.Is(id)) continue;   // creature rigs (CreatureRig): no humanoid body
            heroes++;
            var rest = LoadRest(f);
            var miss = new List<string>(); var set = ColliderSet.Build(id, rest.P, miss);
            if (miss.Count == 0 && set.Length >= 13) complete++; else bad.Add($"{id} missing {string.Join("/", miss)}");
            if (BodyColliderData.ById.ContainsKey(id)) data++; else noData.Add(id);
            var R = ColliderSet.Radii(id, rest.P); bool ok = true;
            for (int i = 0; i < R.Length; i++) { if (i == 1) continue; float L = ColliderSet.RefLength(i, rest.P); if (!(R[i] >= ColliderSet.BandLo[i] * L - 1e-3f && R[i] <= ColliderSet.BandHi[i] * L + 1e-3f)) { ok = false; bad.Add($"{id} {ColliderSet.Data[i]} {R[i]:0.000} vs bone {L:0.000}"); } }
            // limbs never fatter than they are long, the thigh at least as thick as the calf
            ok &= R[8] < ColliderSet.RefLength(8, rest.P) * 0.5f && R[6] < ColliderSet.RefLength(6, rest.P) * 0.5f;
            if (ok) sane++;

            // spring chains: <= budget, each chain >= 2 bones + tip, every joint's tail at rest outside its kind's colliders
            var sb = rest.Names.Where(n => n.StartsWith("sb_")).ToList();
            if (sb.Count == 0) continue;
            rigged++;
            var chains = sb.GroupBy(n => int.Parse(n.Split('_')[1])).ToList();
            int joints = chains.Sum(g => g.Count() - 1);
            check(joints <= ChainKinds.MaxJointsPerHero && chains.All(g => g.Count() >= 3), $"physics: {id} {chains.Count} chains, {joints} spring joints (budget {ChainKinds.MaxJointsPerHero})");
            float worst = 9; string where = "";
            foreach (var g in chains)
            {
                var kind = ChainKinds.Of(g.Key); var rig = SpringRigs.Of(id, kind);
                var mask = rig.Mask != 0 ? (BodyGroup)rig.Mask : ChainKinds.Mask(kind);
                var bones = g.OrderBy(n => int.Parse(n.Split('_')[2])).ToList();
                for (int k = 1; k < bones.Count; k++)
                {
                    var p = rest.Pos[bones[k]];
                    foreach (var sh in set)
                    {
                        if ((sh.Group & mask) == 0) continue;
                        float r = sh.Group == BodyGroup.Thighs && rig.ThighR > 0 ? rig.ThighR : sh.Group == BodyGroup.Calves && rig.CalfR > 0 ? rig.CalfR : sh.Radius;
                        var a = rest.Pos[sh.BoneA] + sh.Offset; var b = sh.Sphere ? a : rest.Pos[sh.BoneB];
                        float gap = SegDist(p, a, b) - r - rig.JointRadius;
                        if (gap < worst) { worst = gap; where = $"{bones[k]} in {sh.Name}"; }
                    }
                    if ((mask & BodyGroup.Ground) != 0 && p.Y - rig.JointRadius < worst) { worst = p.Y - rig.JointRadius; where = bones[k] + " in ground"; }
                }
            }
            // the pilot's chains (jasper) were placed before this rule: allowed 4 cm, everything new must be clear
            float tol = ov.TryGetValue(id, out var o) && o.TryGetProperty("tool", out var tl) && tl.GetString() == "spring_rig.py" ? -0.04f : -0.002f;
            check(worst >= tol, $"physics: {id} chain joints outside their colliders at rest (worst gap {worst * 100:0.0} cm{(worst < 0 ? ", " + where : "")})");
            check(ov.ContainsKey(id), $"physics: {id} chains listed in models/physics/overrides.json");
        }
        check(heroes > 100 && complete == heroes, $"physics: {complete}/{heroes} humanoid heroes have the full body collider set{(bad.Count > 0 ? " - " + string.Join("; ", bad.Take(5)) : "")}");
        check(sane == heroes, $"physics: {sane}/{heroes} heroes' collider radii are sane for their bone lengths");
        check(data == heroes, $"physics: {data}/{heroes} heroes have measured girth (BodyColliderData){(noData.Count > 0 ? " missing " + string.Join(",", noData.Take(8)) : "")}");
        check(ov.Keys.All(k => File.Exists(Path.Combine(models, $"SB_Hero_{k}_HD.glb"))), $"physics: every overrides entry names a shipped hero ({ov.Count} entries, {rigged} heroes rigged)");
    }
}
