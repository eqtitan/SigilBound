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
    }
}
