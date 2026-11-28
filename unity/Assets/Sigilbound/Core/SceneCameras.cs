// Every camera the player can see a campaign stage from, and the visibility rules the scene generator enforces
// (docs/sigilbound/art/SCENERY.md section 8, CAMPAIGN_SCENES_PLAN.md). Plain math, so Tests~ can check every stage.
//   - the five battle views (View button: Classic, Overhead, Cinematic, Three-quarter = the default, Hero view) on a
//     portrait phone (390 x 844), a 16:9 screen and a wide 19.5:9 phone in landscape;
//   - the lair reveal's camera path on boss stages (three keys: low behind the team, halfway, risen);
//   - battle start and victory use the framed battle view (no separate camera), so they are covered by the views.
// The hero lane is the rune ring plus its margin: where units stand, their HP tags float and skill effects play.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public sealed class SceneCam
    {
        public string Name;           // "angle@0.46", "reveal0" ...
        public CamView View;
        public double Aspect;
        public bool Default;          // the default battle view (Three-quarter): it frames the landmark
        public bool Guard;            // nothing may block the hero lane from it (placement is rejected)
        public bool Reveal;           // a lair reveal key: nothing may block the landmark from it
        public double FogEnd = 64;    // beyond this a placement is lost in the fog
        double _near = double.NaN, _far = double.NaN;
        public double LaneNear() => double.IsNaN(_near) ? _near = SceneCameras.LaneNear(this) : _near;
        // The farthest lane point from the eye on the ground plane (anything wholly beyond cannot block it).
        public double LaneFar()
        {
            if (!double.IsNaN(_far)) return _far;
            double best = 0; var e = View.Eye;
            foreach (var t in SceneCameras.LanePoints()) best = Math.Max(best, Math.Sqrt((t.X - e.X) * (t.X - e.X) + (t.Z - e.Z) * (t.Z - e.Z)));
            return _far = best;
        }
    }

    public static class SceneCameras
    {
        // Portrait phone (390 x 844), 16:9, wide phone (19.5:9). Battle views are fitted per aspect (BattleLayout.View).
        public static readonly double[] Aspects = { 390.0 / 844.0, 16.0 / 9.0, 19.5 / 9.0 };
        // The hero lane: the rim ellipse grown by this margin (bodies stand on the rim, effects reach past it), up to tag height.
        public const double LaneMargin = 1.3, LaneTop = 3.6;
        public static double LaneRx => BattleLayout.RimRx + LaneMargin;
        public static double LaneRz => BattleLayout.RimRz + LaneMargin;
        // A foreground piece nearer the camera than the lane may only sit in the outer bands of the screen.
        public const double EdgeBand = 0.6;
        // The background band the landmark is framed in (NDC of its body, SceneRules.FrameError; the default view at every aspect).
        public const double BandLo = 0.0, BandHi = 0.76, BandHiFar = 0.92, BandX = 0.86;   // BandHi: under the turn-order bar
        // On a boss stage (tilted camera) the lair must show up to this height under BandHiFar: whole for all but the
        // tallest towers (the bell tower, the Stormcrown crag, the burning tree), whose tips may still rise past the HUD.
        public const double LairShown = 6.0;

        // boss: a campaign boss stage, whose views facing the lair are tilted up (BattleLayout.BossView).
        public static List<SceneCam> Battle(double fogEnd, bool boss = false)
        {
            var l = new List<SceneCam>();
            foreach (var a in Aspects)
                foreach (var c in BattleLayout.Cameras)
                    l.Add(new SceneCam
                    {
                        Name = $"{c.Key}@{a:0.00}", View = BattleLayout.View(c.Key, a, false, boss), Aspect = a, FogEnd = fogEnd,
                        Default = c.Key == BattleLayout.DefaultCamera,
                        // Hero view looks back over the enemies' shoulders: whatever stands behind them in the other views is
                        // in front of it, so it is faded at run time (Arena.HideOccluders), not refused here.
                        Guard = c.Key != "front",
                    });
            return l;
        }

        // The lair reveal (Client/BattleController.LairReveal): low behind the team looking up at the landmark through a
        // gap in the allies' row, rising and pulling back, then blending into the battle view.
        public static (CamView a, CamView mid, CamView b) RevealKeys(double lx, double lz, double top, IList<double> allyXs = null)
        {
            if (allyXs == null) { allyXs = new List<double>(); for (int i = 0; i < 4; i++) allyXs.Add(BattleLayout.Slot(true, i, 4).X); }
            double k = 2.6 / Math.Max(4, lz + 5.6), straight = lx * k;
            var xs = new List<double>(allyXs); xs.Sort();
            var gaps = new List<double>();
            if (xs.Count > 0) { gaps.Add(xs[0] - 1.2); gaps.Add(xs[xs.Count - 1] + 1.2); for (int i = 1; i < xs.Count; i++) gaps.Add((xs[i - 1] + xs[i]) * 0.5); }
            double cross = straight, best = double.MaxValue;
            foreach (var g in gaps) if (Math.Abs(g - straight) < best) { best = Math.Abs(g - straight); cross = g; }
            double ex = (cross - lx * k) / (1 - k);
            var a = new CamView { Eye = new V3(ex, 1.05, -5.6), Target = new V3(lx, top * 0.72, lz), Fov = 54 };
            var b = new CamView { Eye = new V3(3.2, 2.7, -8), Target = new V3(lx * 0.6, top * 0.45, lz * 0.7), Fov = 52 };
            var m = new CamView { Eye = (a.Eye + b.Eye) * 0.5, Target = (a.Target + b.Target) * 0.5, Fov = 53 };
            return (a, m, b);
        }

        public static List<SceneCam> Reveal(double lx, double lz, double top, double fogEnd)
        {
            var (a, m, b) = RevealKeys(lx, lz, top);
            var l = new List<SceneCam>();
            int i = 0;
            foreach (var v in new[] { a, m, b })
                foreach (var asp in new[] { Aspects[0], Aspects[1] })
                    l.Add(new SceneCam { Name = $"reveal{i}@{asp:0.00}", View = v, Aspect = asp, Reveal = true, FogEnd = fogEnd });
            return l;
        }

        // Sample points of the hero lane: the centre, two rings (inside the rim and just past it) at foot, chest, head
        // and tag height. A sight line from a guarded camera to any of them must stay clear.
        static List<V3> _lane;
        public static List<V3> LanePoints()
        {
            if (_lane != null) return _lane;
            var l = new List<V3> { new V3(BattleLayout.RimCx, 1.0, BattleLayout.RimCz) };
            foreach (double k in new[] { 0.55, 1.0 })
                for (int a = 0; a < 16; a++)
                {
                    double ang = a * Math.PI / 8;
                    double x = BattleLayout.RimCx + Math.Cos(ang) * LaneRx * k, z = BattleLayout.RimCz + Math.Sin(ang) * LaneRz * k;
                    foreach (double h in new[] { 0.3, 1.4, 2.4, LaneTop }) l.Add(new V3(x, h, z));
                }
            return _lane = l;
        }

        // Inside the lane ellipse, grown by r.
        public static bool InLane(double x, double z, double r)
        {
            double ex = (x - BattleLayout.RimCx) / (LaneRx + r), ez = (z - BattleLayout.RimCz) / (LaneRz + r);
            return ex * ex + ez * ez < 1;
        }

        // Projection (NDC x, y) and depth along the view axis; null behind the camera.
        public static (double x, double y, double depth)? Project(CamView v, double aspect, V3 p)
        {
            var f = (v.Target - v.Eye).Normalized;
            var r = new V3(0, 1, 0).Cross(f).Normalized;
            var u = f.Cross(r);
            var d = p - v.Eye;
            double z = d.Dot(f);
            if (z <= 0.1) return null;
            double t = Math.Tan(v.Fov * Math.PI / 360);
            return (d.Dot(r) / (z * t * aspect), d.Dot(u) / (z * t), z);
        }

        // The camera's footprint on the ground: where the four corner rays meet y = 0 (or `far` metres out when a ray
        // runs above the horizon). Clockwise from bottom-left. Used by the previews and the docs' diagrams.
        public static V3[] Footprint(CamView v, double aspect, double far = 60)
        {
            var f = (v.Target - v.Eye).Normalized;
            var r = new V3(0, 1, 0).Cross(f).Normalized;
            var u = f.Cross(r);
            double t = Math.Tan(v.Fov * Math.PI / 360);
            var pts = new V3[4];
            int i = 0;
            foreach (var (sx, sy) in new[] { (-1.0, -1.0), (-1.0, 1.0), (1.0, 1.0), (1.0, -1.0) })
            {
                var dir = (f + r * (sx * t * aspect) + u * (sy * t)).Normalized;
                double s = dir.Y < -1e-4 ? Math.Min(far, -v.Eye.Y / dir.Y) : far;
                var hit = v.Eye + dir * s;
                pts[i++] = new V3(hit.X, 0, hit.Z);
            }
            return pts;
        }

        // A placement's volume: a low cylinder (the whole footprint up to 35 % of its height) and a tall one (the part above).
        public readonly struct Volume
        {
            public readonly double X, Z, R, RTall, Y0, Y1;
            public Volume(double x, double z, double r, double rTall, double y0, double y1) { X = x; Z = z; R = r; RTall = rTall; Y0 = y0; Y1 = y1; }
            public double YMid => Y0 + 0.35 * (Y1 - Y0);
        }

        // True when the segment eye -> p passes through the volume before reaching p (with a small margin at p).
        public static bool Blocks(V3 eye, V3 p, Volume v)
        {
            return SegHitsCyl(eye, p, v.X, v.Z, v.R, v.Y0, v.YMid) || SegHitsCyl(eye, p, v.X, v.Z, v.RTall, v.YMid, v.Y1);
        }
        static bool SegHitsCyl(V3 a, V3 b, double cx, double cz, double r, double y0, double y1)
        {
            if (y1 <= y0 || r <= 0) return false;
            double dx = b.X - a.X, dz = b.Z - a.Z, fx = a.X - cx, fz = a.Z - cz;
            double A = dx * dx + dz * dz, B = 2 * (fx * dx + fz * dz), C = fx * fx + fz * fz - r * r;
            double len = Math.Sqrt(A + (b.Y - a.Y) * (b.Y - a.Y));
            double tMax = 1 - 0.45 / Math.Max(0.5, len);
            double t0, t1;
            if (A < 1e-9) { if (C > 0) return false; t0 = 0; t1 = 1; }
            else
            {
                double disc = B * B - 4 * A * C;
                if (disc < 0) return false;
                double sq = Math.Sqrt(disc);
                t0 = (-B - sq) / (2 * A); t1 = (-B + sq) / (2 * A);
            }
            t0 = Math.Max(t0, 0); t1 = Math.Min(t1, tMax);
            if (t0 > t1) return false;
            double ya = a.Y + (b.Y - a.Y) * t0, yb = a.Y + (b.Y - a.Y) * t1;
            return Math.Max(ya, yb) >= y0 && Math.Min(ya, yb) <= y1;
        }

        // Points on a volume used for visibility and framing: base, middle and top on the axis, and four around the middle.
        public static IEnumerable<V3> Samples(Volume v)
        {
            double ym = (v.Y0 + v.Y1) * 0.5, rr = Math.Max(v.RTall, v.R * 0.6);
            yield return new V3(v.X, Math.Max(0.05, v.Y0 + 0.05), v.Z);
            yield return new V3(v.X, ym, v.Z);
            yield return new V3(v.X, v.Y1, v.Z);
            yield return new V3(v.X - rr, ym, v.Z); yield return new V3(v.X + rr, ym, v.Z);
            yield return new V3(v.X, ym, v.Z - rr); yield return new V3(v.X, ym, v.Z + rr);
        }

        public static bool Sees(SceneCam c, Volume v)
        {
            foreach (var p in Samples(v))
            {
                var q = Project(c.View, c.Aspect, p);
                if (q != null && Math.Abs(q.Value.x) <= 1 && Math.Abs(q.Value.y) <= 1 && (p - c.View.Eye).Length <= c.FogEnd) return true;
            }
            return false;
        }

        // The lane's nearest depth along this camera's axis (anything nearer is foreground).
        public static double LaneNear(SceneCam c)
        {
            double best = double.MaxValue;
            foreach (var p in LanePoints()) { var q = Project(c.View, c.Aspect, p); if (q != null) best = Math.Min(best, q.Value.depth); }
            return best;
        }
    }
}
