using System;
using System.Linq;
using Sigilbound.Core;

// Titan fights: in every camera view (the three Titan-only views and the usual five) and common
// phone/PC aspects, the whole team stays in frame and the Titan's head and tag stay under the HUD
// (turn bar, skill banner); over the Titan's shoulder only the team must fit. The Titan views must
// also SELL the size: on screen the Titan stands at least twice as tall as a hero (owner, 2026-11-26).
static class TitanCameraTests
{
    public static double Ratio(CamView v, double aspect)
    {
        double Span(V3 foot, double h)
        {
            var a = BattleLayout.ProjectTo(v.Eye, v.Target, v.Fov, aspect, foot); var b = BattleLayout.ProjectTo(v.Eye, v.Target, v.Fov, aspect, foot + new V3(0, h, 0));
            return a == null || b == null ? 0 : b.Value.y - a.Value.y;
        }
        double titan = Span(BattleLayout.TitanSlot(), BattleLayout.TitanHead);
        double hero = Enumerable.Range(0, 4).Select(i => Span(BattleLayout.Slot(true, i, 4), 1.85)).Average();
        return hero <= 0 ? 0 : titan / hero;
    }

    public static void Run(Action<bool, string> check)
    {
        var bad = new System.Collections.Generic.List<string>(); double far = 0;
        foreach (var cam in BattleLayout.Cameras)
            foreach (var aspect in new[] { 2.17, 1.78, 1.33 })
            {
                double dist = BattleLayout.FitDistance(cam, aspect, true); far = Math.Max(far, dist);
                var eye = BattleLayout.CameraPosition(cam, dist, true);
                double fov = BattleLayout.FovFor(aspect);
                bool head = BattleLayout.TitanPoints().All(p => { var q = BattleLayout.Project(eye, fov, aspect, p, true); return q != null && q.Value.y <= BattleLayout.TitanCeiling + 1e-6; });
                bool ring = BattleLayout.FramePoints(true).All(p => { var q = BattleLayout.Project(eye, fov, aspect, p, true); return q != null && Math.Abs(q.Value.x) <= 0.95 + 1e-6 && Math.Abs(q.Value.y) <= 0.84 + 1e-6; });
                if (!head || !ring || dist >= 39.9) bad.Add($"{cam.Key}@{aspect}");
            }
        check(bad.Count == 0, $"titan camera: head under the HUD and the circle in frame in every usual view (farthest {far:0.0} m)" + (bad.Count > 0 ? " — fails: " + string.Join(", ", bad) : ""));

        var tbad = new System.Collections.Generic.List<string>(); var notes = new System.Collections.Generic.List<string>();
        foreach (var cam in BattleLayout.TitanCameras)
            foreach (var aspect in new[] { 2.17, 1.78, 1.33 })
            {
                var v = BattleLayout.View(cam.Key, aspect, true);
                // Worm's-eye (option C): heads and tags of the team in frame (feet may crop), Titan under the HUD.
                bool fits = cam.Key == "worm"
                    ? BattleLayout.WormHeadsIn(v, aspect) && BattleLayout.ProjectTo(v.Eye, v.Target, v.Fov, aspect, BattleLayout.TitanSlot() + new V3(0, BattleLayout.WormHornTop, 0)) is var hq && hq != null && hq.Value.y <= BattleLayout.WormTagTop + 1e-6
                    : BattleLayout.TitanViewFits(v, aspect, cam.Key != "shoulder");
                double r = Ratio(v, aspect);
                if (!fits || (cam.Key == "side" && r < 2.0)) tbad.Add($"{cam.Key}@{aspect} (fits {fits}, titan/hero {r:0.0}x)");
                if (aspect == 2.17)
                {
                    var a = BattleLayout.ProjectTo(v.Eye, v.Target, v.Fov, aspect, BattleLayout.Slot(true, 1, 4)); var b = BattleLayout.ProjectTo(v.Eye, v.Target, v.Fov, aspect, BattleLayout.Slot(true, 1, 4) + new V3(0, 1.85, 0));
                    notes.Add($"{cam.Key} {r:0.0}x, hero {(a == null || b == null ? 0 : (b.Value.y - a.Value.y) / 2):P0} of screen");
                }
            }
        check(tbad.Count == 0, $"titan views: team in frame, head under the HUD, side view shows the Titan at least 2x a hero ({string.Join(", ", notes)})" + (tbad.Count > 0 ? " — fails: " + string.Join("; ", tbad) : ""));
        check(BattleLayout.TitanCycle.Length == BattleLayout.Cameras.Length + 3 && BattleLayout.TitanCycle[0].Key == BattleLayout.DefaultTitanCamera,
            "titan views: the View button offers the three Titan views first, Worm's-eye by default, then the usual five");
        SpecAndCinematics(check);
    }

    // Art review H3 (2026-11-28): the model hookup is data (titan.json), the arrival is shot from the heroes'
    // side with the Titan facing the camera, cinematics keep the ground in frame, the tag clears the horns.
    static void SpecAndCinematics(Action<bool, string> check)
    {
        string dir = AppContext.BaseDirectory, hd = null;
        for (int i = 0; i < 8 && dir != null && hd == null; i++, dir = System.IO.Path.GetDirectoryName(dir))
        { var p = System.IO.Path.Combine(dir, "Tools~", "models", "heroes_hd"); if (System.IO.Directory.Exists(p)) hd = p; }
        var keep = TitanSpec.Current;
        try
        {
            var json = hd == null ? null : System.IO.Path.Combine(hd, "titan.json");
            check(json != null && System.IO.File.Exists(json), "titan model: Tools~/models/heroes_hd/titan.json ships with the HD models");
            if (json != null && System.IO.File.Exists(json))
            {
                var spec = TitanSpec.Parse(System.IO.File.ReadAllText(json));
                bool glb = System.IO.File.Exists(System.IO.Path.Combine(hd, $"SB_Hero_{spec.Model}_HD.glb")) && System.IO.File.Exists(System.IO.Path.Combine(hd, $"SB_Hero_{spec.Model}_HD_albedo.jpg"));
                check(glb, $"titan model: titan.json names a shipped model (SB_Hero_{spec.Model}_HD.glb and its albedo)");
                var els = (Element[])Enum.GetValues(typeof(Element));
                check(els.All(e => spec.SeamHex(e) != null), "titan model: a seam glow colour for every element");
                check(spec.GlowGain > 0 && spec.GlowGain <= 1.6, $"titan model: seam glow gain {spec.GlowGain:0.00} stays under the white clip (<= 1.6; it was 2.2)");
                check(spec.Face > 0.3 && spec.Face <= 1.1 && spec.HornTop >= 1 && spec.Head > 2, "titan model: face, horn and head heights are sane");
                check(Math.Abs(spec.ModelHead * spec.ViewScale - spec.Head) < 1e-9, "titan model: built to Head / ViewScale, so the view's scale is not applied twice (was 8.1 m instead of 6.24 m)");
            }
            var junk = TitanSpec.Parse("{\"model\":\"basalt\",\"head\":\"tall\",\"face\":9,\"clips\":\"embedded\",\"seams\":{\"Flame\":\"#ff8800\",\"Frost\":\"#ffffff\",\"Tide\":\"blue\"},\"glowCut\":[[0,0,0,1,1,1],[1,2]],\"aliases\":{\"Idle\":\"Idle_Hunch\"}}");
            check(junk.Model == "basalt" && junk.Head == new TitanSpec().Head && junk.Face == new TitanSpec().Face && junk.Clips == "embedded"
                  && junk.SeamHex(Element.Flame) == "#ff8800" && junk.SeamHex(Element.Tide) == null && junk.GlowCut.Count == 1 && junk.Aliases["Idle"] == "Idle_Hunch"
                  && junk.InGlowCut(0.5, 0.5, 0.5) && !junk.InGlowCut(2, 0.5, 0.5),
                  "titan model: titan.json bad values keep the defaults, good ones apply (model, clips, seams, glow cut, aliases)");
            check(TitanSpec.Parse("").Model == "titan", "titan model: no file, the current knight");

            // The old knight and a hunched colossus (face low, shorter, longer reach): both framed.
            var hunched = new TitanSpec { Model = "basalt", Head = 5.6, Face = 0.8, HornTop = 1.12, Reach = 4.6 };
            foreach (var (name, spec) in new[] { ("knight", keep), ("hunched", hunched) })
            {
                TitanSpec.Current = spec;
                var bad = new System.Collections.Generic.List<string>();
                var ts = BattleLayout.TitanSlot(); double row = BattleLayout.Slot(true, 1, 4).Z;
                foreach (var aspect in new[] { 2.17, 1.78, 1.33, 0.56 })
                {
                    var (a, b) = BattleLayout.TitanIntroKeys(aspect);
                    foreach (var (k, v) in new[] { ("A", a), ("B", b) })
                    {
                        // heroes' side: behind the team's row; the Titan (facing the circle's centre, -Z) faces the camera
                        var facing = new V3(Math.Sin(BattleLayout.FacingCentreYaw(ts) * Math.PI / 180), 0, Math.Cos(BattleLayout.FacingCentreYaw(ts) * Math.PI / 180));
                        if (!(v.Eye.Z < row) || (v.Eye - ts).Dot(facing) <= 0) bad.Add($"{k}@{aspect} not on the heroes' side");
                        if (BattleLayout.PitchOf(v.Eye, v.Target) <= 0) bad.Add($"{k}@{aspect} not looking up");
                        if (BattleLayout.PitchOf(v.Eye, v.Target) > BattleLayout.MaxPitchForGround(v.Eye.Y, v.Fov) + 1e-6) bad.Add($"{k}@{aspect} loses the ground");
                    }
                    var face = BattleLayout.ProjectTo(b.Eye, b.Target, b.Fov, aspect, ts + new V3(0, BattleLayout.TitanFace, 0));
                    if (face == null || Math.Abs(face.Value.x) > 0.9 || Math.Abs(face.Value.y) > 0.8) bad.Add($"B@{aspect} face out of frame");
                    var feet = BattleLayout.ProjectTo(a.Eye, a.Target, a.Fov, aspect, ts + new V3(0, 0.3, 0));
                    if (feet == null || Math.Abs(feet.Value.y) > 0.95) bad.Add($"A@{aspect} feet out of frame");
                    // the heroes' backs in the foreground: the middle two heroes' heads sit low in the first key
                    int heroes = Enumerable.Range(1, 2).Count(i => BattleLayout.ProjectTo(a.Eye, a.Target, a.Fov, aspect, BattleLayout.Slot(true, i, 4) + new V3(0, 1.6, 0)) is var q && q != null && q.Value.y < 0.2 && q.Value.y > -1);
                    if (heroes < 2) bad.Add($"A@{aspect} heroes not in the foreground");
                }
                check(bad.Count == 0, $"titan intro ({name}): from behind the heroes, looking up the Titan's front to its face, ground in frame" + (bad.Count > 0 ? " — " + string.Join("; ", bad.Take(4)) : ""));
                check(BattleLayout.WormHornTop >= spec.HornWorld + 0.5, $"titan tag ({name}): {BattleLayout.WormHornTop:0.0} m, clear of the horn tips at {spec.HornWorld:0.0} m");
            }
            TitanSpec.Current = keep;

            // No hero's back in the way: for teams of 1 to 4, the line from each key's eye to the Titan's face
            // passes at least 0.6 m to the side of every hero (3 heroes put Pell right in front of the lens).
            var blocked = new System.Collections.Generic.List<string>();
            for (int n = 1; n <= 4; n++)
            {
                var slots = Enumerable.Range(0, n).Select(i => BattleLayout.Slot(true, i, n)).ToList();
                foreach (var aspect in new[] { 2.17, 0.56 })
                {
                    var (a, b) = BattleLayout.TitanIntroKeys(aspect, slots.Select(p => p.X).ToList());
                    var face = BattleLayout.TitanSlot() + new V3(0, BattleLayout.TitanFace, 0);
                    foreach (var (k, v) in new[] { ("A", a), ("B", b) })
                        foreach (var hp in slots)
                        {
                            double t = (hp.Z - v.Eye.Z) / (face.Z - v.Eye.Z);
                            if (t <= 0 || t >= 1) continue;
                            double x = v.Eye.X + (face.X - v.Eye.X) * t;
                            if (Math.Abs(x - hp.X) < 0.6) blocked.Add($"{n} heroes {k}@{aspect}: {Math.Abs(x - hp.X):0.00} m");
                        }
                }
            }
            check(blocked.Count == 0, "titan intro: with 1 to 4 heroes the camera stands in a gap, no hero's back between it and the Titan's face" + (blocked.Count > 0 ? " — " + string.Join("; ", blocked.Take(4)) : ""));

            // Faultline Leap at its peak: the worm's-eye camera aimed at a Titan 12 m up on a 78-degree lens
            // must be tilted down so the ground stays in frame (77-titan-4: it floated in an empty sky).
            var wv = BattleLayout.WormView(2.17);
            var apex = BattleLayout.TeamCentre + new V3(0, 12 + BattleLayout.TitanHead * 0.5, 0) + (BattleLayout.TitanSlot() - BattleLayout.TeamCentre) * 0.5;
            var aim = BattleLayout.ClampTargetForGround(wv.Eye, apex, 78);
            double pitch = BattleLayout.PitchOf(wv.Eye, aim), bottom = pitch - 39;
            bool groundSeen = bottom < 0 && wv.Eye.Y / Math.Tan(-bottom * Math.PI / 180) <= BattleLayout.GroundSeen + 1e-6;
            var tq = BattleLayout.ProjectTo(wv.Eye, aim, 78, 2.17, apex);
            check(BattleLayout.PitchOf(wv.Eye, apex) > pitch && groundSeen && tq != null && tq.Value.y < 1,
                $"titan jump camera: clamped from {BattleLayout.PitchOf(wv.Eye, apex):0} to {pitch:0} degrees up, the ground in frame and the Titan still on screen");
        }
        finally { TitanSpec.Current = keep; }
    }
}
