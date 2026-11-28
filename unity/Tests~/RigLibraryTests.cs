// Skeleton library (docs/sigilbound/art/SKELETON_LIBRARY.md; Core/Rig, Client/HumanoidRig.cs, Editor/HumanoidClipBaker.cs).
// Offline checks of what the game and the build rely on:
//   - avatar mapping completeness: every migrated hero's skeleton maps the full library biped (plus fingers) onto
//     Unity's human bones, with every bone under its human parent and unique node names (AvatarBuilder's rules),
//   - every shared clip retargets to every pilot: the clip's source skeleton maps the same human bones, the clip
//     drives them with finite unit rotations, and every clip the battle asks of the hero exists in its clip sets,
//   - joint-limit and gait sanity: known muscles, ranges inside -1..1 that keep the T-pose, small offsets,
//   - no legacy-path components on migrated heroes: RigExtras and DigitigradeLegs are only added when the humanoid
//     setup did not happen, and no migrated hero carries a per-hero clip override.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class RigLibraryTests
{
    static string Find(params string[] rel)
    {
        for (var d = new DirectoryInfo(AppContext.BaseDirectory); d != null; d = d.Parent)
        {
            var p = Path.Combine(new[] { d.FullName }.Concat(rel).ToArray());
            if (File.Exists(p) || Directory.Exists(p)) return p;
        }
        return null;
    }

    static bool UnitQuat(float[] v, int i) { double m = 0; for (int k = 0; k < 4; k++) { if (float.IsNaN(v[i + k]) || float.IsInfinity(v[i + k])) return false; m += v[i + k] * v[i + k]; } return Math.Abs(m - 1) < 0.02; }

    public static void Run(Action<bool, string> check)
    {
        // ---- tables (no files needed) ----
        var known = new HashSet<string>(RigProfiles.BodyMuscles);
        check(RigProfiles.BodyMuscles.Length == 55 && known.Count == 55, "rig library: 55 distinct body muscle names");
        check(RigProfiles.CurveName("Left Index 1 Stretched") == "LeftHand.Index.1 Stretched" && RigProfiles.CurveName("Right Thumb Spread") == "RightHand.Thumb.Spread"
              && RigProfiles.CurveName("Spine Front-Back") == "Spine Front-Back", "rig library: finger muscles map to the clip curve spelling");
        foreach (var p in RigProfiles.Table.Values)
        {
            var badLim = p.Limits.Where(l => !known.Contains(l.Muscle) || !(l.Min < l.Max) || l.Min < -1 || l.Max > 1 || l.Min > 0 || l.Max < 0).Select(l => l.Muscle).ToList();
            check(badLim.Count == 0, $"rig library: {p.Id} joint limits are known muscles with -1 <= min < 0 < max <= 1 (T-pose reachable) {string.Join(",", badLim)}");
            var g = RigProfiles.Gait(p.Gait);
            var badOff = g.Offsets.Where(kv => !known.Contains(kv.Key) || Math.Abs(kv.Value * p.GaitAmount) > 0.3f).Select(kv => kv.Key).ToList();
            check(badOff.Count == 0, $"rig library: {p.Id} gait {p.Gait} offsets are known muscles, each under 0.3 {string.Join(",", badOff)}");
            // an offset must not be clamped away by the hero's own limits
            var fought = g.Offsets.Where(kv => p.Limits.Any(l => l.Muscle == kv.Key && (kv.Value * p.GaitAmount < l.Min || kv.Value * p.GaitAmount > l.Max))).Select(kv => kv.Key).ToList();
            check(fought.Count == 0, $"rig library: {p.Id} gait offsets sit inside its joint limits {string.Join(",", fought)}");
            check(g.Tempo >= 0.6f && g.Tempo <= 1.4f && g.IdleTempo >= 0.6f && g.IdleTempo <= 1.4f && g.ActionWeight >= 0 && g.ActionWeight <= 1, $"rig library: {p.Id} gait tempo in 0.6-1.4");
            check(p.HockFold == 0 || p.Skeleton == SkeletonKind.BipedDigitigrade, $"rig library: {p.Id} hock fold only on a digitigrade biped");
            check(!AnimStyle.HdOverrides.ContainsKey(p.Id), $"rig library: migrated {p.Id} has no per-hero clip override (the library retires them)");
            check(p.ClipSets.Length > 0 && p.ClipSets.All(s => RigProfiles.ClipSources.Any(c => c.Set == s)), $"rig library: {p.Id} clip sets are baked sets");
        }
        RigProfiles.ForceLegacy = true;
        check(!RigProfiles.Migrated("ysolde"), "rig library: ForceLegacy turns the per-hero flag off (before/after tour)");
        RigProfiles.ForceLegacy = false;
        check(RigProfiles.Migrated("ysolde") && !RigProfiles.Migrated("hask"), "rig library: per-hero flag (pilots migrated, the rest legacy)");

        // ---- no legacy-path components on migrated heroes (the build code) ----
        var hm = Find("unity", "Assets", "Sigilbound", "Client", "HeroModels.cs");
        if (hm != null)
        {
            var src = File.ReadAllText(hm);
            int gate = src.IndexOf("if (hr == null)", StringComparison.Ordinal), end = src.IndexOf("var bodyPhys = root.GetComponent<BodyPhysics>()", StringComparison.Ordinal); // after the legacy block (feat/physics-standard)
            int rx = src.IndexOf("AddComponent<RigExtras>", StringComparison.Ordinal), dl = src.IndexOf("AddComponent<DigitigradeLegs>", StringComparison.Ordinal);
            check(gate > 0 && end > gate && rx > gate && rx < end && dl > gate && dl < end && src.IndexOf("AddComponent<RigExtras>", rx + 1, StringComparison.Ordinal) < 0,
                  "rig library: RigExtras and DigitigradeLegs are added only on the legacy path (never with a humanoid rig)");
            check(src.Contains("HumanoidRig.Setup(root, holder, RigProfiles.Of(profileId))") && src.Contains("RigProfiles.Migrated(profileId)"), "rig library: BuildHd takes the humanoid path for migrated heroes");
        }
        var rigSrc = Find("unity", "Assets", "Sigilbound", "Client", "HumanoidRig.cs");
        if (rigSrc != null)
        {
            var src = File.ReadAllText(rigSrc);
            check(src.Contains("Destroy(legacy)") && src.Contains("SetApplyFootIK(false)") && src.Contains("DirectorUpdateMode.Manual"),
                  "rig library: the humanoid rig removes glTFast's legacy Animation, plays without IK goals, evaluates by hand");
        }

        // ---- the models ----
        var models = Find("unity", "Tools~", "models");
        if (models == null || !File.Exists(Path.Combine(models, "ubc_anims.glb")))
        { bool ci = Environment.GetEnvironmentVariable("CI") == "true"; check(!ci, "rig library: models not found here" + (ci ? "" : " (skipped)")); return; }

        var sets = new Dictionary<string, (GlbAnim g, Dictionary<string, int> map, List<GlbAnim.Clip> clips)>();
        foreach (var (set, src, prefix) in RigProfiles.ClipSources)
        {
            var path = Path.Combine(models, src);
            check(File.Exists(path), $"rig library: clip set {set} source {src} exists");
            if (!File.Exists(path)) continue;
            var g = GlbAnim.Read(File.ReadAllBytes(path));
            var map = HumanoidMap.Map(g.Hierarchy());
            var clips = g.Clips.Where(c => (prefix == null || c.Name.StartsWith(prefix)) && !c.Name.Contains("TPose") && c.Length > 0).ToList();
            check(HumanoidMap.Missing(map).Count == 0 && HumanoidMap.HierarchyProblems(map, g.Hierarchy()).Count == 0, $"rig library: clip set {set}'s reference skeleton is a valid humanoid");
            check(clips.Count >= (set == "biped" ? 59 : 11), $"rig library: clip set {set} has {clips.Count} clips");
            sets[set] = (g, map, clips);
        }

        foreach (var p in RigProfiles.Table.Values)
        {
            string file = p.Id == "titan" ? "SB_Hero_titan_HD.glb" : $"SB_Hero_{p.Id}_HD.glb";
            var path = Path.Combine(models, "heroes_hd", file);
            check(File.Exists(path), $"rig library: {p.Id} model {file} exists");
            if (!File.Exists(path)) continue;
            var g = GlbAnim.Read(File.ReadAllBytes(path));
            var h = g.Hierarchy();
            var map = HumanoidMap.Map(h);
            foreach (var kv in p.Bones) { int i = g.Find(kv.Value); if (i >= 0) map[kv.Key] = i; }
            var want = RigProfiles.BipedBones.Concat(RigProfiles.FingerBones).ToList();
            var missing = want.Where(b => !map.ContainsKey(b)).ToList();
            check(missing.Count == 0, $"rig library: {p.Id} ({p.Skeleton}) maps all {want.Count} library biped bones {string.Join(",", missing)}");
            check(HumanoidMap.HierarchyProblems(map, h).Count == 0, $"rig library: {p.Id} human bones sit under their human parents");
            check(h.Select(x => x.Name).Distinct().Count() == h.Count, $"rig library: {p.Id} node names are unique (AvatarBuilder)");
            // T-pose rest: upper arms out along +-X, thighs down (the avatar takes the rest pose as its T-pose)
            check(TPose(g), $"rig library: {p.Id} rests in a T-pose");
            if (p.Skeleton == SkeletonKind.BipedDigitigrade)
                check(g.Find("meta_l") >= 0 && g.Find("meta_r") >= 0 && map["LeftFoot"] == g.Find("foot_l") && g.Nodes[g.Find("foot_l")].Parent == g.Find("meta_l"),
                      $"rig library: {p.Id} digitigrade: the metatarsal sits between calf and foot, unmapped (the hock folds after the clip)");
            foreach (var a in p.Addons) foreach (var b in a.Bones)
                check(g.Find(b) >= 0 && !map.Values.Contains(g.Find(b)), $"rig library: {p.Id} add-on {a.Kind} bone {b} exists and is not a human bone");

            // physics standard: the body collider set's bones exist with a real length, chains are extra (non-human)
            // bones, rigid items sit on bones of this skeleton
            var cols = p.Colliders;
            var need = new[] { "head", "neck", "chest", "spine", "hips", "upperarm_l", "upperarm_r", "lowerarm_l", "lowerarm_r", "thigh_l", "thigh_r", "calf_l", "calf_r" };
            check(need.All(n => cols.Any(c => c.Name == n)), $"rig library: {p.Id} has the standard body collider set (head, neck, chest, spine, hips, arms, thighs, calves)");
            var badCol = cols.Where(c => g.Find(c.Bone) < 0 || (c.Tip != null && g.Find(c.Tip) < 0) || c.Radius <= 0 || c.Radius > 1.5f).Select(c => c.Name).ToList();
            check(badCol.Count == 0, $"rig library: {p.Id} collider bones exist and radii are sane {string.Join(",", badCol)}");
            var badChain = p.Chains.SelectMany(c => c.Bones).Where(b => g.Find(b) < 0 || map.Values.Contains(g.Find(b))).ToList();
            check(badChain.Count == 0, $"rig library: {p.Id} physics chains are existing non-human bones {string.Join(",", badChain)}");
            check(p.Chains.All(c => c.Stiffness > 0 && c.Drag > 0 && c.Drag < 1 && c.TipLength > 0), $"rig library: {p.Id} chain settings sane");
            check(p.Rigid.All(r => g.Find(r.Bone) >= 0), $"rig library: {p.Id} rigid items sit on its bones");

            // every clip of its sets retargets: the source maps every human bone this hero needs, and drives them sanely
            foreach (var set in p.ClipSets)
            {
                if (!sets.TryGetValue(set, out var S)) continue;
                var srcMissing = map.Keys.Where(k => !S.map.ContainsKey(k)).ToList();
                check(srcMissing.Count == 0, $"rig library: {p.Id} <- {set}: the source skeleton maps every human bone the hero has {string.Join(",", srcMissing)}");
                int bad = 0; var worst = "";
                foreach (var c in S.clips)
                {
                    var driven = new HashSet<string>(c.Tracks.Where(t => t.Path == "rotation").Select(t => S.g.Nodes[t.Node].Name));
                    int humanDriven = S.map.Count(kv => driven.Contains(S.g.Nodes[kv.Value].Name));
                    bool finite = c.Tracks.Where(t => t.Path == "rotation").All(t => Enumerable.Range(0, t.Times.Length).All(i => UnitQuat(t.Values, i * 4)));
                    if (humanDriven < 15 || !finite) { bad++; worst = $"{c.Name} ({humanDriven} bones{(finite ? "" : ", bad quaternion")})"; }
                }
                check(bad == 0, $"rig library: {p.Id} <- {set}: all {S.clips.Count} clips drive the human bones with unit rotations {worst}");
            }
            // every clip the battle asks of this hero exists in its sets (by canonical alias)
            var names = new HashSet<string>(p.ClipSets.Where(sets.ContainsKey).SelectMany(s => sets[s].clips.Select(c => c.Name)));
            IEnumerable<string> asks;
            if (p.Id == "titan")
            {
                var ts = TitanSpec.Parse(File.ReadAllText(Path.Combine(models, "heroes_hd", "titan.json")));
                asks = new[] { "idle", "walk", "roar", "fists", "leap", "land", "breath", "sweep", "collapse" }.Select(m => ts.ClipOf(m, null)).Concat(ts.Aliases.Values);
            }
            else
            {
                var st = AnimStyle.Hd(p.Id);
                string A(string n) => n != null && AnimStyle.HdAliases.TryGetValue(n, out var t) ? t : n;
                asks = new[] { st.Idle, st.Cast, st.Support, st.Ult, st.Win, st.Defeat, st.Ready, st.Block, "Walk_Loop", A("Run"), HeroUlts.ClipFor(p.Id) }
                    .Concat(st.Strike).Concat(st.Hit).Concat(st.BigHit).Concat(st.Death).Where(n => n != null).Select(A);
            }
            var absent = asks.Where(n => n != null && !names.Contains(n)).Distinct().ToList();
            check(absent.Count == 0, $"rig library: {p.Id}: every clip the battle plays is in its clip sets {string.Join(",", absent)}");
        }
    }

    // World positions from the rest TRS (glTF space), to check the rest pose is a T-pose.
    static bool TPose(GlbAnim g)
    {
        var W = new Dictionary<int, (double[] p, double[] q)>();
        (double[] p, double[] q) Get(int i)
        {
            if (W.TryGetValue(i, out var r)) return r;
            var n = g.Nodes[i];
            double[] lp = { n.T[0] * n.S[0], n.T[1] * n.S[1], n.T[2] * n.S[2] }, lq = { n.R[0], n.R[1], n.R[2], n.R[3] };
            if (n.Parent < 0) return W[i] = (lp, lq);
            var (pp, pq) = Get(n.Parent);
            var rp = Rot(pq, n.T);
            return W[i] = (new[] { pp[0] + rp[0], pp[1] + rp[1], pp[2] + rp[2] }, Mul(pq, lq));
        }
        double[] Dir(string a, string b) { var pa = Get(g.Find(a)).p; var pb = Get(g.Find(b)).p; var d = new[] { pb[0] - pa[0], pb[1] - pa[1], pb[2] - pa[2] }; double m = Math.Sqrt(d[0] * d[0] + d[1] * d[1] + d[2] * d[2]); return d.Select(x => x / m).ToArray(); }
        if (new[] { "upperarm_l", "lowerarm_l", "upperarm_r", "lowerarm_r", "thigh_l", "calf_l" }.Any(n => g.Find(n) < 0)) return false;
        return Math.Abs(Dir("upperarm_l", "lowerarm_l")[0]) > 0.9 && Math.Abs(Dir("upperarm_r", "lowerarm_r")[0]) > 0.9 && Dir("thigh_l", "calf_l")[1] < -0.9;
    }
    static double[] Mul(double[] a, double[] b) => new[]
    {
        a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1], a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
        a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3], a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
    };
    static double[] Rot(double[] q, float[] v)
    {
        var p = new double[] { v[0], v[1], v[2], 0 };
        var c = new[] { -q[0], -q[1], -q[2], q[3] };
        var r = Mul(Mul(q, p), c);
        return new[] { r[0], r[1], r[2] };
    }
}
