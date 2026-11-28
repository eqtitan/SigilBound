// Physics standard (docs/sigilbound/art/PHYSICS.md; the library's Core/Rig/RigPhysics.cs + measured
// BodyColliderData, Core/Rig/RigChains.cs, Client/BodyPhysics.cs, Client/SpringBones.cs): every shipped hero gets a complete body collider set of sane size, every spring chain is
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

    public sealed class Shape { public string Name, A, B; public float R; public BodyGroup G; }

    // The body collider set of one hero as BodyPhysics builds it (legacy profile: biped, girth 1).
    public static List<Shape> BodySet(string id, Func<string, SVec?> pos, Func<string, string> parentOf, List<string> missing = null)
    {
        var o = new List<Shape>();
        foreach (var c in RigPhysics.BodySet(SkeletonKind.Biped))
        {
            var a = pos(c.Bone); var b = c.Tip != null ? pos(c.Tip) : null;
            if (!a.HasValue || (c.Tip != null && !b.HasValue)) { missing?.Add(a.HasValue ? c.Tip : c.Bone); continue; }
            float len = RigPhysics.RefLength(c, pos, parentOf(c.Bone));
            float r = Math.Max(0.02f, Math.Min(0.6f, RigPhysics.Radius(id, c, len, 1f)));
            o.Add(new Shape { Name = c.Name, A = c.Bone, B = c.Tip ?? c.Bone, R = r, G = RigPhysics.Group(c.Name) });
        }
        return o;
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

    // Cost check helpers (section 4). RefUs: the reference workload's cost per frame on the reference machine
    // (this repo's CI class: GitHub ubuntu-latest, 4 vCPU; measured 12-13 us, .NET 8 optimised, after warm-up).
    const double RefUs = 12.5;

    static void Frames(SpringSolver s, SQuat[] ar, SVec[] ap, SCapsule[] caps, int n, bool kin, ref int f)
    {
        for (int i = 0; i < n; i++, f++) { ap[0] = new SVec(0.1f * (float)Math.Sin(f * 0.1), 1.3f, 0); s.Update(1 / 60f, ar, ap, caps, caps.Length, kin); }
    }

    // The reference workload: the solver's inner loop shape (48 points x 17 segments x 2 passes of point-to-segment
    // distance with a square root) in plain scalar float code on arrays - no structs, no operators, nothing that
    // optimisation settings or solver changes affect. Fixed forever: changing it rescales every budget.
    static float[] RefInputs(SCapsule[] caps)
    {
        var w = new float[7 * 17 + 144];
        for (int k = 0; k < 17; k++) { var c = caps[k]; var o = 7 * k; w[o] = c.A.X; w[o + 1] = c.A.Y; w[o + 2] = c.A.Z; w[o + 3] = c.B.X; w[o + 4] = c.B.Y; w[o + 5] = c.B.Z; w[o + 6] = c.R; }
        for (int j = 0; j < 144; j++) w[7 * 17 + j] = 0.01f * j - 0.5f;
        return w;
    }

    static float RefFrames(float[] w, int n)
    {
        float acc = 0;
        for (int f = 0; f < n; f++)
            for (int j = 0; j < 48; j++)
            {
                int p = 7 * 17 + 3 * j; float px = w[p], py = w[p + 1], pz = w[p + 2];
                for (int pass = 0; pass < 2; pass++)
                    for (int k = 0; k < 17; k++)
                    {
                        int o = 7 * k; float ax = w[o], ay = w[o + 1], az = w[o + 2], bx = w[o + 3] - ax, by = w[o + 4] - ay, bz = w[o + 5] - az;
                        float l = bx * bx + by * by + bz * bz;
                        float t = l > 1e-12f ? Math.Max(0f, Math.Min(1f, ((px - ax) * bx + (py - ay) * by + (pz - az) * bz) / l)) : 0f;
                        float dx = px - (ax + bx * t), dy = py - (ay + by * t), dz = pz - (az + bz * t); float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                        if (d < w[o + 6]) { px += dx * 0.01f; py += dy * 0.01f; pz += dz * 0.01f; }
                        acc += d;
                    }
            }
        return acc;
    }

    static double Median(double[] v) { var c = (double[])v.Clone(); Array.Sort(c); return c[c.Length / 2]; }

    public static void Run(Action<bool, string> check)
    {
        // 1. kinds and budgets are consistent
        check(Enum.GetValues(typeof(ChainKind)).Cast<ChainKind>().All(k => ChainKinds.Of(ChainKinds.First(k)) == k) && ChainKinds.Of(0) == ChainKind.Skirt && ChainKinds.Of(29) == ChainKind.Cape && ChainKinds.Of(69) == ChainKind.Banner,
              "physics: chain numbers map to every chain kind (0 skirt, 20 cape, 30 tail, 35 soft wings, 40 hair, 50 ears, 55 sash, 60 pouch, 64 chain, 67 banner)");
        check(Enum.GetValues(typeof(ChainKind)).Cast<ChainKind>().All(k => (ChainKinds.Mask(k) & (BodyGroup.Arms | BodyGroup.Forearms)) == 0 || k == ChainKind.Hair || k == ChainKind.SoftWings),
              "physics: only hair and wings collide with the arms (cloth never catches on a swinging arm)");
        check(ChainKinds.BattleBudget >= 10 * ChainKinds.MaxJointsPerHero / 2 && ChainKinds.MaxJointsPerHero <= 64, $"physics: budgets {ChainKinds.MaxJointsPerHero} joints per hero, {ChainKinds.BattleBudget} per battle");
        check(RigPhysics.BodySet(SkeletonKind.Biped).All(c => RigPhysics.Group(c.Name) != BodyGroup.Extra) && BodyColliderData.Names.SequenceEqual(RigPhysics.BodySet(SkeletonKind.Biped).Select(c => c.Name)),
              "physics: every library body collider has a group and the measured data follows RigPhysics.BodySet's order");

        // 2. a hero without measured data keeps the library's fraction x bone length (compatible extension)
        {
            var c = RigPhysics.BodySet(SkeletonKind.Biped).First(x => x.Name == "thigh_l");
            check(Math.Abs(RigPhysics.Radius("no-such-hero", c, 0.43f, 1.2f) - c.Radius * 0.43f * 1.2f) < 1e-6f, "physics: no measured line = the library's fraction x length x girth");
            var some = BodyColliderData.ById.First();
            check(Math.Abs(RigPhysics.Radius(some.Key, c, 0.43f, 1.2f) - some.Value[BodyColliderData.Names.IndexOf("thigh_l")]) < 1e-6f, $"physics: a measured hero ({some.Key}) uses its own girth");
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

        // 3b. swing limit: a teleporting anchor (a dash ult) can't whip a joint past MaxAngle from its animated rest direction
        {
            var rig = ChainKinds.Defaults(ChainKind.Cape); var s = Sheet(1, rig); var ar = new[] { SQuat.Identity }; var none = new SCapsule[0];
            float worst = 0;
            for (int f = 0; f < 120; f++)
            {
                var ap = new[] { new SVec(f % 20 < 10 ? 0f : 1.5f, 1.3f, 0) };   // jumps 1.5 m back and forth
                s.Update(1 / 60f, ar, ap, none, 0, false);
                // joint 0's rest direction is the anchor's (identity) frame: (0,-0.3,-0.01)
                var head = ap[0] + new SVec(-0.15f, 0f, -0.18f); var d = (s.Tail(0) - head).Normalized; var r = new SVec(0, -0.3f, -0.01f).Normalized;
                worst = Math.Max(worst, (float)(Math.Acos(Math.Max(-1, Math.Min(1, SVec.Dot(d, r)))) * 180 / Math.PI));
            }
            check(rig.MaxAngle > 0 && worst <= rig.MaxAngle + 0.5f, $"physics: cape swing limit holds under a teleporting root (worst {worst:0.0} deg, limit {rig.MaxAngle} deg)");
            check(new SpringRig().MaxAngle == 0 && new SpringRig().Inflate == 0 && SpringRigs.Of("jasper", ChainKind.Skirt).MaxAngle == 0, "physics: the pilot's skirt keeps no swing limit and no inflate (unchanged)");
        }

        // 4. cost: a full-budget hero (48 joints) against a full body set (16 shapes + ground), and a whole battle.
        //    Machine-relative (L035): CI's shared runner differs from a dev box, so the solver is timed against a fixed
        //    scalar reference workload in the same process (interleaved batches, median, after a JIT warm-up), and the
        //    budget is in reference units: RefUs = what the reference costs on the reference machine (CI class, 4 cores),
        //    so "150 us" still means 150 us there. Allocation-free is checked exactly, not timed.
        {
            var dbg = (DebuggableAttribute)Attribute.GetCustomAttribute(typeof(SpringSolver).Assembly, typeof(DebuggableAttribute));
            check(dbg == null || !dbg.IsJITOptimizerDisabled, "physics: the game core is timed as optimised code (Tests~ builds with Optimize=true; unoptimised IL runs the solver ~9x slower than any player build - L035)");
            var rig = ChainKinds.Defaults(ChainKind.Cape); var s = Sheet(16, rig); var sk = Sheet(16, rig);
            var caps = new SCapsule[17]; var ar = new[] { SQuat.Identity }; var ap = new[] { new SVec(0, 1.3f, 0) };
            for (int i = 0; i < 16; i++) caps[i] = new SCapsule { A = new SVec(-0.2f + 0.025f * i, 0.2f + 0.08f * i, 0), B = new SVec(-0.2f + 0.025f * i, 0.3f + 0.08f * i, 0.05f), R = 0.08f };
            caps[16] = ChainKinds.Ground();
            check(s.Joints == ChainKinds.MaxJointsPerHero, $"physics: cost rig has {s.Joints} joints");
            var refW = RefInputs(caps); int fd = 0, fk = 0; float sink = 0;
            // warm-up: long enough for tiered JIT to promote Update/Solve to optimised code (the first ~0.1 s is tier 0, ~2.5x slower)
            var warm = Stopwatch.StartNew();
            while (warm.ElapsedMilliseconds < 400) { Frames(s, ar, ap, caps, 200, false, ref fd); Frames(sk, ar, ap, caps, 200, true, ref fk); sink += RefFrames(refW, 200); }
            long a0 = GC.GetAllocatedBytesForCurrentThread();
            Frames(s, ar, ap, caps, 1000, false, ref fd); Frames(sk, ar, ap, caps, 1000, true, ref fk);
            long alloc = GC.GetAllocatedBytesForCurrentThread() - a0;
            check(alloc == 0, $"physics: the spring solver allocates nothing per step ({alloc} bytes over 2000 updates)");
            const int Batches = 9, N = 1000;
            var rs = new double[Batches]; var ds = new double[Batches]; var ks = new double[Batches]; var ratio = new double[Batches]; var kr = new double[Batches];
            for (int b = 0; b < Batches; b++)
            {
                long t0 = Stopwatch.GetTimestamp(); sink += RefFrames(refW, N);
                long t1 = Stopwatch.GetTimestamp(); Frames(s, ar, ap, caps, N, false, ref fd);
                long t2 = Stopwatch.GetTimestamp(); Frames(sk, ar, ap, caps, N, true, ref fk);
                long t3 = Stopwatch.GetTimestamp();
                double us(long x, long y) => (y - x) * 1e6 / Stopwatch.Frequency / N;
                rs[b] = us(t0, t1); ds[b] = us(t1, t2); ks[b] = us(t2, t3); ratio[b] = ds[b] / rs[b]; kr[b] = ks[b] / ds[b];
            }
            double refUs = Median(rs), heroUs = Median(ds), kinUs = Median(ks), units = Median(ratio);
            double battleUnits = units * ChainKinds.BattleBudget / ChainKinds.MaxJointsPerHero;
            const double HeroBudget = 150 / RefUs, BattleBudget = 1500 / RefUs;   // 12 and 120 reference units
            check(units < HeroBudget, $"physics: one full-budget hero (48 joints x 17 colliders) costs {units:0.00} reference units per frame (budget {HeroBudget:0} = 150 us on the reference machine; here {heroUs:0.0} us, reference {refUs:0.0} us, median of {Batches})");
            check(battleUnits < BattleBudget, $"physics: a full battle at the budget ({ChainKinds.BattleBudget} joints) costs {battleUnits:0.0} reference units per frame (budget {BattleBudget:0} = 1.5 ms on the reference machine; here {heroUs * ChainKinds.BattleBudget / ChainKinds.MaxJointsPerHero / 1000:0.00} ms; a phone core is ~4x slower, Low quality runs kinematic)");
            check(Median(kr) <= 1.2 && sk.StepsTaken == 0 && !float.IsNaN(sink), $"physics: Low quality (kinematic) costs {kinUs:0.0} us ({Median(kr):0.00}x dynamic), no simulation steps");
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
        var ov = Overrides(models); int nBody = RigPhysics.BodySet(SkeletonKind.Biped).Count;
        foreach (var f in Directory.GetFiles(models, "SB_Hero_*_HD.glb").OrderBy(x => x))
        {
            var id = Path.GetFileName(f).Substring(8); id = id.Substring(0, id.Length - 7);
            if (Creatures.Is(id)) continue;   // creature rigs (CreatureRig): no humanoid body
            heroes++;
            var rest = LoadRest(f);
            var miss = new List<string>(); var set = BodySet(id, rest.P, n => rest.Parent.TryGetValue(n, out var pa) ? pa : null, miss);
            if (miss.Count == 0 && set.Count == nBody) complete++; else bad.Add($"{id} missing {string.Join("/", miss)}");
            if (BodyColliderData.ById.ContainsKey(id)) data++; else noData.Add(id);
            bool ok = true;
            foreach (var c in RigPhysics.BodySet(SkeletonKind.Biped))
            {
                var sh = set.FirstOrDefault(x => x.Name == c.Name); if (sh == null) continue;
                float L = RigPhysics.RefLength(c, rest.P, rest.Parent.TryGetValue(c.Bone, out var pa) ? pa : null), fb = c.Radius * L;
                if (!(sh.R >= 0.5f * fb - 1e-3f && sh.R <= 1.8f * fb + 1e-3f)) { ok = false; bad.Add($"{id} {c.Name} {sh.R:0.000} vs library {fb:0.000}"); }
            }
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
                        if ((sh.G & mask) == 0) continue;
                        float r = sh.G == BodyGroup.Thighs && rig.ThighR > 0 ? rig.ThighR : sh.G == BodyGroup.Calves && rig.CalfR > 0 ? rig.CalfR : sh.R + rig.Inflate;
                        float gap = SegDist(p, rest.Pos[sh.A], rest.Pos[sh.B]) - r - rig.JointRadius;
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
        check(sane == heroes, $"physics: {sane}/{heroes} heroes' measured collider radii are within 0.5-1.8x the library's size for their bones{(bad.Count > 0 ? " - " + string.Join("; ", bad.Take(5)) : "")}");
        check(data == heroes, $"physics: {data}/{heroes} heroes have measured girth (BodyColliderData){(noData.Count > 0 ? " missing " + string.Join(",", noData.Take(8)) : "")}");
        check(ov.Keys.All(k => File.Exists(Path.Combine(models, $"SB_Hero_{k}_HD.glb"))), $"physics: every overrides entry names a shipped hero ({ov.Count} entries, {rigged} heroes rigged)");
    }
}
