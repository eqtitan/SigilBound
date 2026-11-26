using System;
using System.Linq;
using Sigilbound.Core;

// Titan fights: in every camera view and common phone/PC aspect, the Titan's head and tag stay under
// the HUD (turn bar, skill banner) and the whole battle circle still fits (owner report 2026-11-26).
static class TitanCameraTests
{
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
        check(bad.Count == 0, $"titan camera: head under the HUD and the circle in frame in every view (farthest {far:0.0} m)" + (bad.Count > 0 ? " — fails: " + string.Join(", ", bad) : ""));
    }
}
