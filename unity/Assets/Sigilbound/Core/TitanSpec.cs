// The Titan's model hookup as data (heroes_hd/titan.json, shipped from Tools~/models/heroes_hd), so a
// replacement image-to-3D model can be dropped in without code changes: which GLB, how big, where its
// face and horn tips are, how its feet meet the ground, how far it reaches, its seam glow colour per
// element, its own clips and their event times, its sockets and its breakable crystal ridges. Everything has a default matching the current armoured knight, so a missing or broken file
// changes nothing. How to swap the model: docs/sigilbound/art/TITAN_MODEL.md.
using System;
using System.Collections.Generic;
using System.Globalization;

namespace Sigilbound.Core
{
    // One of the Titan's own moves: its clip and named events (normalized clip time).
    public sealed class TitanMove
    {
        public string Clip;
        public readonly Dictionary<string, double> Events = new Dictionary<string, double>();
    }

    public sealed class TitanSpec
    {
        // heroes_hd/SB_Hero_<Model>_HD.glb (+ _albedo.jpg, _normal.jpg, _glow.png), listed in heroes_hd/index.txt.
        public string Model = "titan";
        // Head height in the battle, metres from the soles (heroes: 1.85). The model is scaled so its head
        // bone (HeadBone; else the top of its mesh / HornTop) lands here.
        public double Head = 4.8 * 1.3;
        public string HeadBone = "Head";
        public double ViewScale = 1.3;   // CharacterView's scale for the Titan (the model is built inside it)
        public double Bulk = 1.18;       // extra width and depth (x/z) over the uniform scale
        // Heights as fractions of Head: the face (where cameras aim and the intro ends) and the horn tips
        // (the name tag sits above them). A hunched colossus has its face below its shoulders' top.
        public double Face = 0.97, HornTop = 1.27, Chest = 0.72;
        // Feet: AutoGround drops/lifts the model so the lowest point of its mesh is on the ground, then
        // FootOffset (metres, + up) is added; Back is how far behind the far rim it stands.
        public bool AutoGround = true;
        public double FootOffset = 0, Back = 1.2;
        public double Reach = 3.6;       // Bedrock Hammer: how far from its target it stops (longer arms: more)
        // Glow mask (the _glow PNG's brightness): colour per element (hex; missing: the element colour),
        // and the gain on it. Above ~1.5 a flame seam clips to white under bloom.
        public readonly Dictionary<Element, string> Seams = new Dictionary<Element, string>();
        public double GlowGain = 1.35;
        // Model-space boxes (Unity axes: the glTF's with X negated; metres before scaling) whose triangles
        // get GlowCutUv instead of their own UVs: kills stray glow islands (the old knight's chest letter).
        public readonly List<double[]> GlowCut = new List<double[]>();
        public double[] GlowCutUv = { 0.5963, 0.8755 };
        // Clips: "ubc" = the shared retargeted UAL set (the hero rig); "embedded" = the GLB's own clips
        // (a separately rigged model), plus any UAL clip whose bones match. Aliases map the names the
        // battle asks for (Idle, Jump, HitReact, ...) to the model's clip names.
        public string Clips = "ubc";
        public readonly Dictionary<string, string> Aliases = new Dictionary<string, string>();
        // The model's own moves (titan.json "moves"; made by blenderforge/titan_moves.py): per move key, its clip and
        // event times as normalized clip time (0-1). Keys: idle, walk, roar, fists (Bedrock Hammer's two-fist slam),
        // leap and land (Faultline Leap), breath (Heartfire Torrent), sweep (the Earthsplitter), collapse. A missing
        // move or event falls back to the shared clips and the old timings (Ev's default).
        public readonly Dictionary<string, TitanMove> Moves = new Dictionary<string, TitanMove>();
        // Sockets on the skeleton: the open chest core the breath pours from, and the rock fists' centres.
        public string ChestBone = "chest_core", FistL = "fist_l", FistR = "fist_r";
        // Crystal ridges (bones ridge_NN_s1 / _s2) break off as its health falls: stage 1 at RidgeStages[0], stage 2 at [1].
        public double[] RidgeStages = { 0.66, 0.33 };
        // Body glow in a wind-up: seams and core ramp to FlareGain x glowGain; the ridges glow at RidgeGlow x the seam colour.
        public double FlareGain = 2.2, RidgeGlow = 1.6;

        public double HeadWorld => Head;
        public double ModelHead => Head / Math.Max(0.01, ViewScale);   // head height inside the scaled view
        public double FaceWorld => Head * Face;
        public double HornWorld => Head * HornTop;

        public string ClipOf(string move, string fallback) => Moves.TryGetValue(move, out var m) && !string.IsNullOrEmpty(m.Clip) ? m.Clip : fallback;
        public double Ev(string move, string ev, double fallback) => Moves.TryGetValue(move, out var m) && m.Events.TryGetValue(ev, out var t) ? t : fallback;

        // The Titan can't be killed, so its "health" is the share of the top damage tier still to deal: it reaches 0 at
        // the last TitanTiers threshold. The HP bar over it and the ridge stages follow it.
        public static double Health(long damage)
        {
            long full = GameData.TitanTiers[GameData.TitanTiers.Length - 1].Dmg;
            return full <= 0 ? 1 : Math.Max(0, Math.Min(1, 1 - damage / (double)full));
        }
        // 0 = ridges whole, 1 = the first crystals broken (health at or under RidgeStages[0]), 2 = the second set too.
        public int RidgeStage(double health) => health <= RidgeStages[1] ? 2 : health <= RidgeStages[0] ? 1 : 0;

        // Quality tiers (Low 0 .. Ultra 3): shards per broken crystal (none on Low), and whether the breath carries its
        // heavy layers (flame tongues, heat haze, smoke; not on Low, which keeps the embers and the glow).
        public static int ShardsFor(int tier) => tier <= 0 ? 0 : tier == 1 ? 3 : tier == 2 ? 5 : 6;
        public static bool HeavyBreath(int tier) => tier > 0;

        static TitanSpec _current;
        public static TitanSpec Current { get => _current ?? (_current = new TitanSpec()); set => _current = value; }

        // Seam colour for an element as hex, or null (use the element colour).
        public string SeamHex(Element e) => Seams.TryGetValue(e, out var h) ? h : null;

        // Reads titan.json; unknown keys are ignored and bad values keep the default.
        public static TitanSpec Parse(string json)
        {
            var s = new TitanSpec();
            if (string.IsNullOrWhiteSpace(json)) return s;
            if (!(Json.Parse(json) is Dictionary<string, object> d)) return s;
            string Str(string k, string def) => d.TryGetValue(k, out var v) && v is string t && t.Length > 0 ? t : def;
            double Num(string k, double def, double lo, double hi) => d.TryGetValue(k, out var v) && v is double x && x >= lo && x <= hi ? x : def;
            bool Bool(string k, bool def) => d.TryGetValue(k, out var v) && v is bool b ? b : def;
            s.Model = Str("model", s.Model);
            s.Head = Num("head", s.Head, 1, 20);
            s.HeadBone = Str("headBone", s.HeadBone);
            s.ViewScale = Num("viewScale", s.ViewScale, 0.1, 5);
            s.Bulk = Num("bulk", s.Bulk, 0.5, 3);
            s.Face = Num("face", s.Face, 0.3, 1.5);
            s.HornTop = Num("hornTop", s.HornTop, 0.8, 2);
            s.Chest = Num("chest", s.Chest, 0.2, 1.2);
            s.AutoGround = Bool("autoGround", s.AutoGround);
            s.FootOffset = Num("footOffset", s.FootOffset, -3, 3);
            s.Back = Num("back", s.Back, -2, 6);
            s.Reach = Num("reach", s.Reach, 1, 10);
            s.GlowGain = Num("glowGain", s.GlowGain, 0, 4);
            s.Clips = Str("clips", s.Clips) == "embedded" ? "embedded" : "ubc";
            if (d.TryGetValue("seams", out var sv) && sv is Dictionary<string, object> seams)
                foreach (var kv in seams)
                    if (Enum.TryParse<Element>(kv.Key, true, out var el) && kv.Value is string hex && IsHex(hex)) s.Seams[el] = hex;
            if (d.TryGetValue("glowCut", out var gv) && gv is List<object> cuts)
                foreach (var c in cuts)
                    if (c is List<object> box && box.Count == 6 && box.TrueForAll(x => x is double)) s.GlowCut.Add(box.ConvertAll(x => (double)x).ToArray());
            if (d.TryGetValue("glowCutUv", out var uv) && uv is List<object> u && u.Count == 2 && u[0] is double u0 && u[1] is double u1) s.GlowCutUv = new[] { u0, u1 };
            if (d.TryGetValue("aliases", out var av) && av is Dictionary<string, object> al)
                foreach (var kv in al) if (kv.Value is string clip && clip.Length > 0) s.Aliases[kv.Key] = clip;
            if (d.TryGetValue("moves", out var mv) && mv is Dictionary<string, object> moves)
                foreach (var kv in moves)
                {
                    if (!(kv.Value is Dictionary<string, object> md)) continue;
                    var m = new TitanMove { Clip = md.TryGetValue("clip", out var c) && c is string cs ? cs : null };
                    foreach (var e in md) if (e.Value is double t && t >= 0 && t <= 1) m.Events[e.Key] = t;
                    s.Moves[kv.Key] = m;
                }
            s.ChestBone = Str("chestBone", s.ChestBone); s.FistL = Str("fistL", s.FistL); s.FistR = Str("fistR", s.FistR);
            if (d.TryGetValue("ridgeStages", out var rs) && rs is List<object> st && st.Count == 2 && st[0] is double a && st[1] is double b && a > b && a < 1 && b > 0)
                s.RidgeStages = new[] { a, b };
            s.FlareGain = Num("flareGain", s.FlareGain, 1, 4);
            s.RidgeGlow = Num("ridgeGlow", s.RidgeGlow, 0, 4);
            return s;
        }

        public static bool IsHex(string h)
        {
            if (h == null || h.Length != 7 || h[0] != '#') return false;
            return int.TryParse(h.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _);
        }

        // True when a triangle centroid lies in one of the GlowCut boxes.
        public bool InGlowCut(double x, double y, double z)
        {
            foreach (var b in GlowCut)
                if (x >= b[0] && y >= b[1] && z >= b[2] && x <= b[3] && y <= b[4] && z <= b[5]) return true;
            return false;
        }
    }
}
