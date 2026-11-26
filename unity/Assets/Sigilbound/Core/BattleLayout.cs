// Battle layout and camera framing, ported from game3d/three3d.mjs (v0.19.x), as plain math so it
// is testable outside Unity. Coordinates here are Unity's (left-handed, +Z away from the default
// camera): the web's three.js (x, y, z) maps to (x, y, -z), which keeps the same picture on screen.
using System;
using System.Linq;

namespace Sigilbound.Core
{
    public struct V3
    {
        public double X, Y, Z;
        public V3(double x, double y, double z) { X = x; Y = y; Z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        public static V3 operator *(V3 a, double k) => new V3(a.X * k, a.Y * k, a.Z * k);
        public double Dot(V3 b) => X * b.X + Y * b.Y + Z * b.Z;
        public V3 Cross(V3 b) => new V3(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
        public double Length => Math.Sqrt(Dot(this));
        public V3 Normalized => this * (1 / Length);
        public override string ToString() => $"({X:0.###}, {Y:0.###}, {Z:0.###})";
    }

    public sealed class CameraPreset { public string Key, Name; public double Yaw, Pitch; public bool TitanOnly; }

    // A solved camera: where it stands, what it looks at, its vertical field of view (degrees).
    public struct CamView { public V3 Eye, Target; public double Fov; }

    public static class BattleLayout
    {
        // The rune circle's rim (an ellipse): three.js RIM { cx 0, cz -0.6, rx 4.2, rz 3.95 }.
        public const double RimCx = 0, RimCz = 0.6, RimRx = 4.2, RimRz = 3.95;
        public const double Spacing = 2.2;               // metres of arc between neighbours
        public static readonly V3 Centre = new V3(RimCx, 0, RimCz);
        public static readonly V3 CamTarget = new V3(0, 1.0, 0.5); // three.js camTarget (0, 1, -0.5)
        // Titan fights aim higher and further back, so the 3.6x Titan sits lower on screen, under the HUD.
        public static readonly V3 TitanCamTarget = new V3(0, 2.1, 1.4);
        public static V3 Target(bool titan) => titan ? TitanCamTarget : CamTarget;
        // The Titan's head and name tag (it stands at Slot(false, 0, 1), head near 3.6 m, horns and tag above)
        // must stay below this screen height (NDC): the header, turn-order bar, skill banner and its own tag
        // cover the top of the screen (owner report, 2026-11-26).
        public const double TitanCeiling = 0.38;

        // Allies along the near arc, enemies along the far arc, ~2.2 m apart (three3d.mjs slot()).
        public static V3 Slot(bool ally, int i, int n)
        {
            double step = Spacing / ((RimRx + RimRz) / 2);
            double half = n <= 1 ? 0 : Math.Min(70 * Math.PI / 180, step * (n - 1) / 2);
            double t = n <= 1 ? 0 : -half + i * (2 * half / (n - 1));
            double a = ally ? t : Math.PI - t;               // 0 = near side (camera), PI = far side
            return new V3(RimCx + Math.Sin(a) * RimRx, 0, RimCz - Math.Cos(a) * RimRz);
        }

        // Yaw (degrees, Unity: 0 = facing +Z) that turns a unit at p toward the circle's centre.
        public static double FacingCentreYaw(V3 p) => Math.Atan2(Centre.X - p.X, Centre.Z - p.Z) * 180 / Math.PI;

        public static readonly CameraPreset[] Cameras =
        {
            new CameraPreset { Key = "classic", Name = "Classic", Yaw = 0, Pitch = 34 },
            new CameraPreset { Key = "high", Name = "Overhead", Yaw = 0, Pitch = 60 },
            new CameraPreset { Key = "low", Name = "Cinematic", Yaw = 0, Pitch = 16 },
            new CameraPreset { Key = "angle", Name = "Three-quarter", Yaw = 38, Pitch = 30 },
            new CameraPreset { Key = "front", Name = "Hero view", Yaw = 180, Pitch = 30 },
        };
        public const string DefaultCamera = "angle";
        public static CameraPreset Camera(string key) => Array.Find(Cameras, c => c.Key == key) ?? Array.Find(Cameras, c => c.Key == DefaultCamera);

        // Portrait screens get a wider lens so a full row fits without pulling far back.
        public static double FovFor(double aspect) => aspect < 0.75 ? 62 : 50;

        public static V3 CameraPosition(CameraPreset c, double dist, bool titan = false)
        {
            var t = Target(titan);
            double pitch = c.Pitch * Math.PI / 180, yaw = c.Yaw * Math.PI / 180, h = Math.Cos(pitch) * dist;
            return new V3(t.X + Math.Sin(yaw) * h, t.Y + Math.Sin(pitch) * dist, t.Z - Math.Cos(yaw) * h);
        }
        // The Titan's head-and-tag points (each must stay under TitanCeiling).
        // The high-detail Titan stands behind the far rim, head at TitanHead metres (heroes: 1.85), so it
        // towers over the team (owner: "players should say wow, that Titan is massive").
        public const double TitanHead = 4.8, TitanBack = 1.2;
        public static V3 TitanSlot() => Slot(false, 0, 1) + new V3(0, 0, TitanBack);
        public static V3[] TitanPoints()
        {
            var s = TitanSlot(); double top = TitanHead * 1.22; // horns and the name tag above the head
            return new[] { new V3(s.X, top, s.Z), new V3(s.X - 1.5, TitanHead * 0.85, s.Z), new V3(s.X + 1.5, TitanHead * 0.85, s.Z) };
        }

        // ---------- Titan-only views (owner picks, 2026-11-26) ----------
        public static readonly CameraPreset[] TitanCameras =
        {
            new CameraPreset { Key = "worm", Name = "Worm's-eye", TitanOnly = true },
            new CameraPreset { Key = "side", Name = "Side duel", TitanOnly = true },
            new CameraPreset { Key = "shoulder", Name = "Titan's shoulder", TitanOnly = true },
        };
        public const string DefaultTitanCamera = "worm";
        // The View button's cycle in a Titan fight: the Titan views first, then the usual five.
        public static CameraPreset[] TitanCycle => TitanCameras.Concat(Cameras).ToArray();
        public static CameraPreset AnyCamera(string key, bool titan) =>
            (titan ? Array.Find(TitanCameras, c => c.Key == key) : null) ?? Camera(key);

        static V3 NearArc => new V3(RimCx, 0, RimCz - RimRz);
        public static V3 TeamCentre => new V3(0, 0, RimCz - RimRz * 0.72);

        // Points a Titan view must keep in frame: each ally (4 slots, feet and head) and the Titan's feet;
        // the Titan's head and tag (TitanPoints) must also stay under TitanCeiling, except over its shoulder.
        public static V3[] AllyPoints()
        {
            var pts = new System.Collections.Generic.List<V3>();
            for (int i = 0; i < 4; i++) { var p = Slot(true, i, 4); pts.Add(p + new V3(0, 0.05, 0)); pts.Add(p + new V3(0, 2.4, 0)); }
            return pts.ToArray();
        }

        // Projects p for a camera at `eye` looking at `target`.
        public static (double x, double y)? ProjectTo(V3 eye, V3 target, double fovDeg, double aspect, V3 p)
        {
            var f = (target - eye).Normalized;
            var r = new V3(0, 1, 0).Cross(f).Normalized;
            var u = f.Cross(r);
            var d = p - eye;
            double z = d.Dot(f);
            if (z <= 0.3) return null;
            double t = Math.Tan(fovDeg * Math.PI / 360);
            return (d.Dot(r) / (z * t * aspect), d.Dot(u) / (z * t));
        }

        // True when the view keeps every ally and the Titan's feet in frame, and (unless shoulder) its head
        // and tag under the HUD ceiling.
        public static bool TitanViewFits(CamView v, double aspect, bool headRule)
        {
            bool In(V3 p, double top) { var q = ProjectTo(v.Eye, v.Target, v.Fov, aspect, p); return q != null && Math.Abs(q.Value.x) <= 0.95 && q.Value.y <= top && q.Value.y >= -0.84; }
            foreach (var p in AllyPoints()) if (!In(p, 0.84)) return false;
            if (!headRule) return true;
            if (!In(TitanSlot() + new V3(0, 0.05, 0), 0.84)) return false;
            foreach (var p in TitanPoints()) if (!In(p, TitanCeiling)) return false;
            return true;
        }

        // Solves a Titan view for this aspect: each kind has a rail (eye(d)) and the closest d that fits.
        public static CamView TitanView(string key, double aspect)
        {
            var ts = TitanSlot(); var near = NearArc; bool portrait = aspect < 0.75;
            Func<double, CamView> rail; bool head = true;
            switch (key)
            {
                case "side": // ground-level, side-on: the team on the left of the screen, the Titan on the right
                    double mid = (near.Z + ts.Z) / 2;
                    rail = d => new CamView { Eye = new V3(d, 1.5, mid - 0.4), Target = new V3(0, TitanHead * 0.8, mid), Fov = portrait ? 60 : 26 };
                    break;
                case "shoulder": // behind and above the Titan's right shoulder (its right is world -X), looking down at the team
                    head = false;
                    var sh = new V3(ts.X - 1.1, TitanHead * 1.12, ts.Z + 1.3);
                    var tgt = TeamCentre + new V3(0, 0.9, 0);
                    var back = (sh - tgt).Normalized;
                    rail = d => new CamView { Eye = sh + back * (1.5 + d), Target = tgt, Fov = portrait ? 62 : 48 };
                    break;
                default: // worm: knee height behind the team, a long lens tilted up at the Titan
                    rail = d => new CamView { Eye = new V3(0, 0.85, near.Z - 24.0 - d), Target = new V3(0, TitanHead * 0.78, ts.Z), Fov = portrait ? 28 : 15 };
                    break;
            }
            double lo = 0, hi = 60;
            if (!TitanViewFits(rail(hi), aspect, head)) return rail(hi);
            for (int i = 0; i < 26; i++) { double m = (lo + hi) / 2; if (TitanViewFits(rail(m), aspect, head)) hi = m; else lo = m; }
            return rail(hi);
        }

        // Any battle view (the usual presets, or a Titan view in a Titan fight).
        public static CamView View(string key, double aspect, bool titan)
        {
            var c = AnyCamera(key, titan);
            if (c.TitanOnly) return TitanView(c.Key, aspect);
            return new CamView { Eye = CameraPosition(c, FitDistance(c, aspect, titan), titan), Target = Target(titan), Fov = FovFor(aspect) };
        }

        // Points that must stay on screen: the whole rim with head height and HUD-tag points above it.
        public static V3[] FramePoints(bool titan)
        {
            var pts = new System.Collections.Generic.List<V3>();
            foreach (int sx in new[] { -1, 1 })
                for (int k = 0; k <= 6; k++)
                {
                    double ang = k / 6.0 * Math.PI, x = sx * Math.Sin(ang) * (RimRx + 0.4), zThree = -0.6 + Math.Cos(ang) * (RimRz + 0.4);
                    pts.Add(new V3(x, 0, -zThree)); pts.Add(new V3(x, zThree > 0 ? 2.75 : 3.3, -zThree));
                }
            pts.Add(new V3(0, 3.75, 3.95));                    // HUD tags above the back row
            if (titan) { pts.Add(new V3(-2.3, 3.4, 4.0)); pts.Add(new V3(2.3, 3.4, 4.0)); } // shoulders and maul; the head is in TitanPoints
            return pts.ToArray();
        }

        // Projects p for a camera at `eye` looking at CamTarget (vertical fov in degrees);
        // returns normalized device coordinates (-1..1), or null if behind the camera.
        public static (double x, double y)? Project(V3 eye, double fovDeg, double aspect, V3 p, bool titan = false)
        {
            var f = (Target(titan) - eye).Normalized;
            var r = new V3(0, 1, 0).Cross(f).Normalized;   // left-handed: right = up x forward
            var u = f.Cross(r);
            var d = p - eye;
            double z = d.Dot(f);
            if (z <= 1e-6) return null;
            double t = Math.Tan(fovDeg * Math.PI / 360);
            return (d.Dot(r) / (z * t * aspect), d.Dot(u) / (z * t));
        }

        // The closest distance (binary search, as in three3d.mjs resize()) at which every frame
        // point projects inside |x| <= 0.95 and |y| <= 0.84.
        public static double FitDistance(CameraPreset c, double aspect, bool titan)
        {
            var pts = FramePoints(titan);
            var head = titan ? TitanPoints() : new V3[0];
            double fov = FovFor(aspect);
            bool Fits(double dist)
            {
                var eye = CameraPosition(c, dist, titan);
                foreach (var p in pts)
                {
                    var q = Project(eye, fov, aspect, p, titan);
                    if (q == null || Math.Abs(q.Value.x) > 0.95 || q.Value.y > 0.84 || q.Value.y < -0.84) return false;
                }
                foreach (var p in head)
                {
                    var q = Project(eye, fov, aspect, p, titan);
                    if (q == null || Math.Abs(q.Value.x) > 0.95 || q.Value.y > TitanCeiling) return false;
                }
                return true;
            }
            double lo = 5, hi = 40;
            for (int i = 0; i < 22; i++) { double mid = (lo + hi) / 2; if (Fits(mid)) hi = mid; else lo = mid; }
            return hi;
        }
    }
}
