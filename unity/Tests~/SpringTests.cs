// Spring bones (character TD pilot 2026-11-28; Core/SpringSolver.cs, Client/SpringBones.cs, Core/Kit/SpringRigs.cs):
// the preview tool and the game run the same solver (parity fixture written by the preview solver), it is
// deterministic, cheap, keeps cloth out of the leg capsules, the Low-quality kinematic mode has no history, and every
// spring-rigged hero GLB has well-formed chains and a settings entry.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using Sigilbound.Core;

static class SpringTests
{
    static SVec V(JsonElement a, int o = 0) => new SVec(a[o].GetSingle(), a[o + 1].GetSingle(), a[o + 2].GetSingle());
    static SQuat Q(JsonElement a) => new SQuat(a[0].GetSingle(), a[1].GetSingle(), a[2].GetSingle(), a[3].GetSingle());
    static float QDist(SQuat a, SQuat b) => 1f - Math.Abs(a.X * b.X + a.Y * b.Y + a.Z * b.Z + a.W * b.W);

    // A skirt-like test rig: n chains of 3 joints round a pelvis anchor.
    static SpringSolver Rig(int chains, SpringRig r = null)
    {
        var par = new List<int>(); var anc = new List<int>(); var lp = new List<SVec>(); var lr = new List<SQuat>(); var ax = new List<SVec>();
        for (int c = 0; c < chains; c++)
        {
            double a = c * 2 * Math.PI / chains; float sx = (float)Math.Sin(a), cz = (float)Math.Cos(a);
            for (int s = 0; s < 3; s++)
            {
                par.Add(s == 0 ? -1 : par.Count - 1); anc.Add(s == 0 ? 0 : -1);
                lp.Add(s == 0 ? new SVec(0.12f * sx, -0.02f, 0.12f * cz) : new SVec(0.03f * sx, -0.2f, 0.03f * cz));
                lr.Add(SQuat.Identity); ax.Add(new SVec(0.03f * sx, -0.2f, 0.03f * cz));
            }
        }
        return new SpringSolver(par.ToArray(), anc.ToArray(), lp.ToArray(), lr.ToArray(), ax.ToArray(), r ?? new SpringRig());
    }

    static void Drive(SpringSolver s, int frames, Func<int, float> dt, List<SQuat> record, bool kin = false)
    {
        var ar = new SQuat[1]; var ap = new SVec[1]; var caps = new SCapsule[4];
        for (int f = 0; f < frames; f++)
        {
            float t = f / 60f, ang = 0.7f * (float)Math.Sin(t * 3);
            ar[0] = new SQuat(0, (float)Math.Sin(ang / 2), 0, (float)Math.Cos(ang / 2));
            ap[0] = new SVec(0.1f * (float)Math.Sin(t * 2), 0.9f, 0.2f * (float)Math.Sin(t * 4));
            var kL = new SVec(0.1f, 0.45f, 0.3f * (float)Math.Sin(t * 6)); var kR = new SVec(-0.1f, 0.45f, -0.3f * (float)Math.Sin(t * 6));
            caps[0] = new SCapsule { A = new SVec(0.1f, 0.85f, 0), B = kL, R = 0.09f }; caps[1] = new SCapsule { A = kL, B = new SVec(0.1f, 0.05f, 0), R = 0.07f };
            caps[2] = new SCapsule { A = new SVec(-0.1f, 0.85f, 0), B = kR, R = 0.09f }; caps[3] = new SCapsule { A = kR, B = new SVec(-0.1f, 0.05f, 0), R = 0.07f };
            s.Update(dt(f), ar, ap, caps, 4, kin);
            if (record != null) record.AddRange(s.Local);
        }
    }

    public static void Run(Action<bool, string> check)
    {
        // 1. parity with the preview solver (scratchpad ctd/tools/springsim.py -> fixtures/spring_parity.json)
        var fx = Path.Combine(AppContext.BaseDirectory, "fixtures", "spring_parity.json");
        if (!File.Exists(fx)) check(false, "spring: parity fixture missing");
        else
        {
            using var d = JsonDocument.Parse(File.ReadAllText(fx));
            var r = d.RootElement; var rg = r.GetProperty("rig");
            var rig = new SpringRig { Stiffness = rg.GetProperty("Stiffness").GetSingle(), Drag = rg.GetProperty("Drag").GetSingle(), Gravity = rg.GetProperty("Gravity").GetSingle(), JointRadius = rg.GetProperty("JointRadius").GetSingle() };
            var js = r.GetProperty("joints").EnumerateArray().ToList();
            var s = new SpringSolver(js.Select(j => j.GetProperty("parent").GetInt32()).ToArray(), js.Select(j => j.GetProperty("anchor").GetInt32()).ToArray(),
                js.Select(j => V(j.GetProperty("lpos"))).ToArray(), js.Select(j => Q(j.GetProperty("lrot"))).ToArray(), js.Select(j => V(j.GetProperty("axis"))).ToArray(), rig);
            float worst = 0; int n = 0;
            foreach (var f in r.GetProperty("frames").EnumerateArray())
            {
                var caps = f.GetProperty("caps").EnumerateArray().Select(c => new SCapsule { A = V(c, 0), B = V(c, 3), R = c[6].GetSingle() }).ToArray();
                s.Update(f.GetProperty("dt").GetSingle(), new[] { Q(f.GetProperty("ar")) }, new[] { V(f.GetProperty("ap")) }, caps, caps.Length, f.GetProperty("kin").GetBoolean());
                int i = 0;
                foreach (var q in f.GetProperty("out").EnumerateArray()) { worst = Math.Max(worst, QDist(s.Local[i++], Q(q))); }
                n++;
            }
            check(n > 100 && worst < 1e-4f, $"spring: game solver matches the preview solver over {n} frames (worst quaternion gap {worst:E1})");
        }

        // 2. determinism: the same frame sequence twice gives bit-identical bones; uneven frame times too
        var a = new List<SQuat>(); var b = new List<SQuat>();
        Func<int, float> uneven = f => f % 7 == 0 ? 1 / 30f : f % 5 == 0 ? 0.011f : 1 / 60f;
        Drive(Rig(10), 900, uneven, a); Drive(Rig(10), 900, uneven, b);
        check(a.Count == b.Count && a.Zip(b, (x, y) => x.X == y.X && x.Y == y.Y && x.Z == y.Z && x.W == y.W).All(z => z), "spring: deterministic for a given frame sequence");
        check(a.All(q => !float.IsNaN(q.X) && !float.IsNaN(q.W)), "spring: no NaN under uneven frames");

        // 3. colliders: every tail stays out of the leg capsules (dynamic and kinematic)
        foreach (bool kin in new[] { false, true })
        {
            var s = Rig(12); var ar = new[] { SQuat.Identity }; var ap = new[] { new SVec(0, 0.9f, 0) };
            // a thigh swung forward through the skirt front
            var caps = new[] { new SCapsule { A = new SVec(0.1f, 0.85f, 0.0f), B = new SVec(0.1f, 0.55f, 0.32f), R = 0.09f } };
            for (int f = 0; f < 120; f++) s.Update(1 / 60f, ar, ap, caps, 1, kin);
            float minGap = 9;
            for (int i = 0; i < s.Joints; i++)
            {
                var p = s.Tail(i); var ab = caps[0].B - caps[0].A; float t = Math.Max(0, Math.Min(1, SVec.Dot(p - caps[0].A, ab) / SVec.Dot(ab, ab)));
                minGap = Math.Min(minGap, (p - (caps[0].A + ab * t)).Length - caps[0].R - s.Rig.JointRadius);
            }
            check(minGap > (kin ? -0.02f : -0.01f), $"spring: tails stay out of a thigh capsule ({(kin ? "kinematic" : "dynamic")}, worst {minGap * 100:0.0} cm)");
        }

        // 4. Low quality (kinematic): no history - the pose depends on this frame only
        {
            var s1 = Rig(6); var s2 = Rig(6);
            Drive(s1, 200, f => 1 / 60f, null, kin: true);
            var r1 = new List<SQuat>(); var r2 = new List<SQuat>();
            Drive(s1, 1, f => 1 / 60f, r1, kin: true); Drive(s2, 1, f => 1 / 60f, r2, kin: true);
            check(r1.Zip(r2, (x, y) => QDist(x, y) < 1e-6f).All(z => z), "spring: Low quality is kinematic (no swing history)");
            check(s1.StepsTaken == 0, "spring: Low quality runs no simulation steps");
        }

        // 5. a long gap (pause, off screen) restarts from rest instead of exploding
        {
            var s = Rig(8); var ar = new[] { SQuat.Identity }; var ap = new[] { new SVec(0, 0.9f, 0) }; var none = new SCapsule[0];
            for (int f = 0; f < 60; f++) s.Update(1 / 60f, ar, ap, none, 0, false);
            ap[0] = new SVec(5, 0.9f, 0); s.Update(2f, ar, ap, none, 0, false);
            float err = 0; for (int i = 0; i < s.Joints; i++) err = Math.Max(err, QDist(s.Local[i], SQuat.Identity));
            check(err < 1e-5f && s.StepsTaken == 1, "spring: a long frame gap resets the chains to rest");
            s.Update(0.5f / 60f, ar, ap, none, 0, false);
            check(s.StepsTaken == 0, "spring: a frame shorter than a step only re-aims the bones");
            for (int f = 0; f < 3; f++) s.Update(0.2f, ar, ap, none, 0, false);
            check(s.StepsTaken == SpringSolver.MaxSteps, "spring: a slow frame catches up at most MaxSteps steps");
        }

        // 6. cost: the largest pilot rig (12 chains x 3, 4 capsules) per frame
        {
            var s = Rig(12); Drive(s, 200, f => 1 / 60f, null);
            double us = double.MaxValue;
            for (int rep = 0; rep < 3; rep++) { var sw = Stopwatch.StartNew(); Drive(s, 10000, f => 1 / 60f, null); sw.Stop(); us = Math.Min(us, sw.Elapsed.TotalMilliseconds * 1000 / 10000); }
            check(us < 100, $"spring: 36 joints x 4 capsules cost {us:0.0} us per frame on this machine, best of 3 (budget 100 us)");
        }

        // 7. clip overrides name real clips and change only their own heroes
        foreach (var kv in AnimStyle.HdOverrides)
        {
            var o = kv.Value; var st = AnimStyle.Hd(kv.Key);
            var names = new[] { o.Idle, o.Run }.Concat(o.BigHit ?? new string[0]).Concat(o.Strike ?? new string[0]).Where(x => x != null).ToList();
            bool real = names.All(n => AnimStyle.HdClips.Contains(n));
            bool applied = (o.Idle == null || st.Idle == o.Idle) && (o.Run == null || AnimStyle.HdAliasesFor(kv.Key)["Run"] == o.Run) && (o.Strike == null || st.Strike.SequenceEqual(o.Strike)) && (o.BigHit == null || st.BigHit.SequenceEqual(o.BigHit));
            check(real && applied, $"clip override {kv.Key}: {string.Join(",", names)}");
        }
        check(AnimStyle.HdAliasesFor("aldric")["Run"] == "Jog_Fwd_Loop" && ReferenceEquals(AnimStyle.HdAliasesFor("aldric"), AnimStyle.HdAliases), "clip override: other heroes keep the shared aliases");

        // 8. data: every spring-rigged GLB has chains of >= 2 bones + tip, <= 255 joints, and a SpringRigs entry
        string models = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        { var p = Path.Combine(dir.FullName, "unity", "Tools~", "models", "heroes_hd"); if (Directory.Exists(p)) { models = p; break; } }
        if (models == null) { bool ci = Environment.GetEnvironmentVariable("CI") == "true"; check(!ci, "spring: models not found here" + (ci ? "" : " (skipped)")); return; }
        // accessory metal maps (blenderforge/hd_materials.py): every listed hero has its map, every map is listed
        var matIndex = Path.Combine(models, "index_mat.txt");
        var listed = File.Exists(matIndex) ? File.ReadAllLines(matIndex).Where(l => l.Trim().Length > 0).Select(l => l.Trim()).ToList() : new List<string>();
        var maps = Directory.GetFiles(models, "SB_Hero_*_HD_mat.png").Select(f => { var n = Path.GetFileName(f).Substring(8); return n.Substring(0, n.Length - 11); }).ToList();
        check(listed.All(maps.Contains) && maps.All(listed.Contains), $"materials: {maps.Count} accessory metal maps, all listed in index_mat.txt");
        var rigged = new List<string>();
        foreach (var f in Directory.GetFiles(models, "SB_Hero_*_HD.glb").OrderBy(x => x))
        {
            var id = Path.GetFileName(f).Substring(8); id = id.Substring(0, id.Length - 7);
            var bytes = File.ReadAllBytes(f); int n = BitConverter.ToInt32(bytes, 12);
            using var d = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes, 20, n));
            var names = d.RootElement.GetProperty("nodes").EnumerateArray().Select(x => x.TryGetProperty("name", out var nm) ? nm.GetString() : "").ToList();
            var sb = names.Where(x => x.StartsWith("sb_")).ToList();
            if (sb.Count == 0) { if (SpringRigs.ById.ContainsKey(id)) check(false, $"spring: {id} has settings but no chains"); continue; }
            rigged.Add(id);
            var chains = sb.GroupBy(x => x.Split('_')[1]).ToList();
            int joints = d.RootElement.GetProperty("skins")[0].GetProperty("joints").GetArrayLength();
            bool ok = chains.All(g => g.Count() >= 3 && g.Select(x => int.Parse(x.Split('_')[2])).OrderBy(x => x).SequenceEqual(Enumerable.Range(0, g.Count())));
            check(ok && joints <= 255 && SpringRigs.ById.ContainsKey(id), $"spring: {id} {chains.Count} chains, {joints} joints, settings {(SpringRigs.ById.ContainsKey(id) ? "found" : "MISSING")}");
        }
        check(SpringRigs.ById.Keys.All(rigged.Contains), $"spring: every SpringRigs entry is rigged ({string.Join(",", rigged)})");
    }
}
