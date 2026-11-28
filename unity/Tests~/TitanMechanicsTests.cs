// The remodelled Titan's own mechanics (owner request 2026-11-28: "Since the Titan has been remodelled, his
// mechanics need to be updated"). Checks the shipped model and its data against each other:
// - every clip name the battle asks for resolves (titan.json aliases and moves) to a clip baked into the GLB;
// - each impact event (normalized clip time in titan.json) lies inside the fist contact window measured from the
//   baked fists (titan_clips.json, written by blenderforge/titan_moves.py), and the camera beats keep their lengths;
// - the chest-core socket (the breath's source), the fist sockets and the breakable crystal ridges exist;
// - the ridge stages fall at the health thresholds (66% and 33% of the top damage tier still to deal);
// - the reach fits the long arms (the fists land on the target);
// - the three moves have new names and descriptions but the same numbers, and the damage is unchanged.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;
using static Sigilbound.Core.GameData;

static class TitanMechanicsTests
{
    static string HdDir()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
        { var p = Path.Combine(dir, "Tools~", "models", "heroes_hd"); if (Directory.Exists(p)) return p; }
        return null;
    }
    static Dictionary<string, object> GlbJson(string path)
    {
        var b = File.ReadAllBytes(path);
        int len = BitConverter.ToInt32(b, 12);
        return Json.Parse(System.Text.Encoding.UTF8.GetString(b, 20, len)) as Dictionary<string, object>;
    }
    static double D(object o) => o is double d ? d : 0;

    public static void Run(Action<bool, string> check)
    {
        var hd = HdDir();
        check(hd != null, "titan mechanics: Tools~/models/heroes_hd found");
        if (hd == null) return;
        var spec = TitanSpec.Parse(File.ReadAllText(Path.Combine(hd, "titan.json")));
        var glbPath = Path.Combine(hd, $"SB_Hero_{spec.Model}_HD.glb");
        var g = GlbJson(glbPath);
        var anims = ((List<object>)g["animations"]).Select(a => (string)((Dictionary<string, object>)a)["name"]).ToHashSet();
        var nodes = ((List<object>)g["nodes"]).Select(n => (string)((Dictionary<string, object>)n)["name"]).ToList();
        var skin = (Dictionary<string, object>)((List<object>)g["skins"])[0];
        var joints = ((List<object>)skin["joints"]).Select(j => nodes[(int)D(j)]).ToHashSet();
        var info = (Dictionary<string, object>)Json.Parse(File.ReadAllText(Path.Combine(hd, "titan_clips.json")));
        var clips = (Dictionary<string, object>)info["clips"];

        // ---- clip aliases resolve to the Titan's own clips
        check(spec.Clips == "embedded" && anims.Count >= 10, $"titan clips: titan.json uses the model's own clips ({anims.Count} baked: {string.Join(", ", anims.OrderBy(x => x))})");
        string Resolve(string name) => spec.Aliases.TryGetValue(name, out var c) ? c : name;
        var style = AnimStyle.TitanHd;
        var asked = new[] { style.Idle, style.Cast, style.Support, style.Win }.Concat(style.Strike).Concat(style.Hit).Concat(style.BigHit).Concat(style.Death).Concat(style.Fidget)
            .Concat(new[] { "Idle", "Walk_Loop", "Jump", "Jump_Land", "Ult_Overhead", "Ult_Channel", "HitReact", "Yes", "Wave", "Interact", "No" }).Distinct().ToList();
        var missing = asked.Where(n => !anims.Contains(Resolve(n))).ToList();
        check(missing.Count == 0, $"titan clips: every name the battle asks for ({asked.Count}) resolves to a baked Titan clip" + (missing.Count > 0 ? " — missing: " + string.Join(", ", missing) : ""));
        var moveKeys = new[] { "idle", "walk", "roar", "fists", "leap", "land", "breath", "sweep", "collapse" };
        var badMoves = moveKeys.Where(k => !spec.Moves.ContainsKey(k) || !anims.Contains(spec.Moves[k].Clip)).ToList();
        check(badMoves.Count == 0, "titan clips: each move (idle, knuckle-walk, roar, two-fist slam, leap, landing, chest breath, long-arm sweep, collapse) names a baked clip" + (badMoves.Count > 0 ? " — " + string.Join(", ", badMoves) : ""));
        check(Resolve("Hit_Chest") != Resolve("Hit_Head") && Resolve(style.Death[0]) == spec.ClipOf("collapse", "") && Resolve("Ult_Channel") == spec.ClipOf("breath", ""),
            "titan clips: its own flinch and heavy stagger, the collapse as its death pose, the chest thrust for the breath");

        // ---- events inside the clips' contact windows (the fists really on the ground)
        List<double[]> Windows(string clip) => clips.TryGetValue(clip, out var c) ? ((List<object>)((Dictionary<string, object>)c)["contact"]).Select(w => ((List<object>)w).Select(D).ToArray()).ToList() : new List<double[]>();
        double Len(string clip) => D(((Dictionary<string, object>)clips[clip])["len"]);
        bool In(string move, string ev)
        {
            var clip = spec.ClipOf(move, ""); double t = spec.Ev(move, ev, -1);
            return t >= 0 && Windows(clip).Any(w => t >= w[0] - 1e-6 && t <= w[1] + 1e-6);
        }
        var bad = new List<string>();
        foreach (var (m, e) in new[] { ("fists", "impact"), ("land", "impact"), ("sweep", "impact"), ("collapse", "ground") })
            if (!In(m, e)) bad.Add($"{m}.{e} {spec.Ev(m, e, -1):0.000} outside {string.Join(" ", Windows(spec.ClipOf(m, "")).Select(w => $"[{w[0]:0.000},{w[1]:0.000}]"))}");
        check(bad.Count == 0, "titan timing: Bedrock Hammer, the landing, the Earthsplitter and the collapse hit when the fists are on the ground (event inside the measured contact window)" + (bad.Count > 0 ? " — " + string.Join("; ", bad) : ""));
        // the authored events in titan.json are the ones baked (titan_clips.json)
        var drift = new List<string>();
        foreach (var kv in spec.Moves)
        {
            if (!clips.TryGetValue(kv.Value.Clip, out var c)) continue;
            var evs = (Dictionary<string, object>)((Dictionary<string, object>)c)["events"];
            foreach (var e in kv.Value.Events) if (!evs.TryGetValue(e.Key, out var baked) || Math.Abs(D(baked) - e.Value) > 0.002) drift.Add($"{kv.Key}.{e.Key}");
        }
        check(drift.Count == 0, "titan timing: titan.json's event times match the baked clips' events" + (drift.Count > 0 ? " — " + string.Join(", ", drift) : ""));
        var leapW = Windows(spec.ClipOf("leap", "")); double takeoff = spec.Ev("leap", "takeoff", -1);
        check(leapW.Count > 0 && takeoff > leapW[0][1] && takeoff - leapW[0][1] < 0.1 && spec.Ev("leap", "crouch", 1) < takeoff,
            $"titan timing: Faultline Leap crouches on its knuckles, pushes off and takes off just after (fists leave {(leapW.Count > 0 ? leapW[0][1] : -1):0.00}, takeoff {takeoff:0.00})");
        check(spec.Ev("fists", "windup", 1) < spec.Ev("fists", "top", 1) && spec.Ev("fists", "top", 1) < spec.Ev("fists", "impact", 0)
              && spec.Ev("sweep", "top", 1) < spec.Ev("sweep", "impact", 0) && Windows(spec.ClipOf("fists", "")).All(w => w[1] < spec.Ev("fists", "top", 1) || w[0] >= spec.Ev("fists", "impact", 1) - 1e-6),
            "titan timing: wind-up, then the top of the swing (slow motion), then the impact; no fist touches the ground in the wind-up");
        double bl = Len(spec.ClipOf("breath", "")), rel = spec.Ev("breath", "release", 0), sw2 = spec.Ev("breath", "sweep2", 0), end = spec.Ev("breath", "end", 0);
        check(rel < sw2 && sw2 < end && Math.Abs(rel * bl - 2.0) < 0.25 && Math.Abs((sw2 - rel) * bl - 1.8) < 0.15 && Math.Abs((end - sw2) * bl - 1.6) < 0.15,
            $"titan timing: Heartfire Torrent gathers {rel * bl:0.0} s, sweeps {(sw2 - rel) * bl:0.0} s and back {(end - sw2) * bl:0.0} s (the camera beats: 1.8-2 s, 1.8 s, 1.6 s)");
        var walkW = Windows(spec.ClipOf("walk", ""));
        check(walkW.Count == 1 && walkW[0][0] <= 0.001 && walkW[0][1] >= 0.999 && Windows(spec.ClipOf("idle", "")).Count == 0,
            "titan clips: the knuckle-walk always has a fist on the ground; the idle's fists hang clear of it");

        // ---- sockets and ridges
        double headY = D(info["headBoneY"]); var chest = ((List<object>)info["chestCore"]).Select(D).ToArray();
        check(nodes.Contains(spec.ChestBone) && joints.Contains(spec.ChestBone) && Math.Abs(chest[1] / headY - spec.Chest) < 0.06 && chest[2] > 0.05,
            $"titan sockets: the chest-core socket '{spec.ChestBone}' is on the skeleton, at the open core ({chest[1] / headY:0.00} of the head height, titan.json chest {spec.Chest:0.00}), on its front");
        check(nodes.Contains(spec.FistL) && nodes.Contains(spec.FistR), "titan sockets: fist sockets for impacts where the fists land");
        var ridges = nodes.Where(n => n.StartsWith("ridge_")).ToList();
        int s1 = ridges.Count(n => n.EndsWith("_s1")), s2 = ridges.Count(n => n.EndsWith("_s2"));
        bool png = File.ReadAllBytes(Path.Combine(hd, $"SB_Hero_{spec.Model}_HD_glow.png")) is var pb && pb.Length > 26 && pb[25] == 6; // colour type 6: RGBA
        check(s1 >= 3 && s2 >= 3 && ridges.All(joints.Contains) && png && D(info["ridgeMaskCoverage"]) > 0.005,
            $"titan ridges: {ridges.Count} crystals on their own bones ({s1} break at stage 1, {s2} at stage 2), skinned, and the ridge glow mask ships in the glow PNG's alpha");

        // ---- ridge stages at the health thresholds
        long full = TitanTiers[TitanTiers.Length - 1].Dmg;
        check(TitanSpec.Health(0) == 1 && TitanSpec.Health(full) == 0 && TitanSpec.Health(full * 2) == 0 && Math.Abs(TitanSpec.Health(full / 2) - 0.5) < 1e-9,
            "titan health: 1 at the start, 0 at the top damage tier (it can't die; health is the top tier still to deal)");
        var st = new TitanSpec();
        long At(double h) => (long)Math.Round((1 - h) * full);
        check(st.RidgeStage(TitanSpec.Health(At(0.67))) == 0 && st.RidgeStage(TitanSpec.Health(At(0.66))) == 1 && st.RidgeStage(TitanSpec.Health(At(0.34))) == 1
              && st.RidgeStage(TitanSpec.Health(At(0.33))) == 2 && st.RidgeStage(0) == 2 && spec.RidgeStages[0] == 0.66 && spec.RidgeStages[1] == 0.33,
            $"titan ridges: the first crystals break at 66% health ({At(0.66):N0} damage), the next at 33% ({At(0.33):N0})");
        check(TitanSpec.ShardsFor(0) == 0 && TitanSpec.ShardsFor(3) > TitanSpec.ShardsFor(1) && !TitanSpec.HeavyBreath(0) && TitanSpec.HeavyBreath(1),
            "titan effects: no crystal shards and no heavy breath layers (flame tongues, haze, smoke) on Low");
        check(spec.FlareGain * spec.GlowGain <= 3.2 && spec.RidgeGlow > 0 && spec.FlareGain > 1.5,
            $"titan effects: the wind-up flare ({spec.FlareGain:0.0}x the seams) is visible and bounded");

        // ---- reach fits the long arms
        double k = spec.Head / headY, z = D(((Dictionary<string, object>)clips[spec.ClipOf("fists", "")])["contactZ"]) * k;
        check(Math.Abs(spec.Reach - z) <= 0.5, $"titan reach: Bedrock Hammer stops {spec.Reach:0.0} m from its target and its fists land {z:0.0} m in front of it");

        // ---- same roles and numbers, new names
        var t = Titan.Skills;
        bool same = t.Length == 3
            && t[0].Target == Target.Enemy && t[0].Mult == 1.3 && t[0].Cooldown == 0 && t[0].Fx.Length == 0
            && t[1].Target == Target.AllEnemies && t[1].Mult == 0.8 && t[1].Cooldown == 3 && t[1].Fx.Length == 1 && t[1].Fx[0].Type == FxType.Slow && t[1].Fx[0].Turns == 2 && t[1].Fx[0].Chance == 0.5
            && t[2].Target == Target.AllEnemies && t[2].Mult == 1.0 && t[2].Cooldown == 4 && t[2].Fx.Length == 2 && t[2].Fx[0].Type == FxType.Poison && t[2].Fx[0].Chance == 0.5
            && t[2].Fx[1].Type == FxType.DefDown && t[2].Fx[1].Chance == 0.4 && t.All(s => s.Hits == 1 && s.Scale == Scale.Atk);
        check(same, "titan moves: same roles and numbers (single-target 130%, team-wide 80% + Slow, team-wide 100% + Poison and DEF down; cooldowns 0/3/4)");
        var heroSkills = Heroes.Where(h => h.Id != "titan").SelectMany(h => h.Skills.Select(s => s.Name)).ToHashSet();
        var old = new[] { "Crushing Palm", "Quake", "Cinder Breath" };
        check(t.Select(s => s.Name).SequenceEqual(new[] { "Bedrock Hammer", "Faultline Leap", "Heartfire Torrent" }) && t.All(s => !heroSkills.Contains(s.Name) && !old.Contains(s.Name))
              && TitanMoveLore.Length == 3 && TitanMoveLore.All(l => l.Length > 40),
            "titan moves: new names (Bedrock Hammer, Faultline Leap, Heartfire Torrent) that no hero skill uses, each with a description of the body move");
        // Damage unchanged: a fixed-seed sample of Titan fights gives exactly the damage it gave before the remodel.
        long total = 0; var rng = new Random(11);
        for (int i = 0; i < 24; i++)
        {
            var gm = new Game(seed: 7100 + i);
            var pick = Heroes.Where(h => h.Id != "titan").OrderBy(_ => rng.Next()).Take(4).ToList();
            gm.S.team = pick.Select(h => { var o = gm.AddHero(h.Id); o.stars = 4; o.level = 40; return o.uid; }).ToList();
            var b = Battle.ForTitan(gm); b.RunAuto(); total += b.TitanDamage;
        }
        check(total == GoldenDamage, $"titan damage: unchanged by the remodel (24 fixed-seed fights: {total:N0}, before {GoldenDamage:N0})");
    }
    const long GoldenDamage = 1061452;   // measured on the code before the remodel (df9a8d9)
}
