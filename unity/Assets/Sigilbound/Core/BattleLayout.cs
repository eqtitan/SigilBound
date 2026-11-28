// Battle layout and camera framing, ported from game3d/three3d.mjs (v0.19.x), as plain math so it
// is testable outside Unity. Coordinates here are Unity's (left-handed, +Z away from the default
// camera): the web's three.js (x, y, z) maps to (x, y, -z), which keeps the same picture on screen.
using System;
using System.Collections.Generic;
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

        // ---------- line-up on the ring (art review M1, PMO P2-9) ----------
        // Units used to stand ON the rim (half of each body outside the ring, the ends of a row wrapping round to
        // the sides). Now every unit's footprint stays inside the ring: they stand on an inner "stand" ellipse,
        // inset from the rim by the widest unit's footprint plus a margin, allies on the near arc and enemies on the
        // far arc, each row within MaxHalf of its centre line (so the ring's middle stays clear and the two rows
        // never meet at the sides). When a line-up needs more arc than the ring has (wide heroes, creatures, a long
        // row), the ring itself grows (Ring, up to MaxRing) instead of the units spilling off it.
        public const double FootR = 0.42;                // a normal hero's footprint radius (metres)
        public const double FootMargin = 0.22;           // clear ground between a footprint and the rim line
        public const double MaxHalf = 58 * Math.PI / 180; // a row's half-spread around its centre line
        public const double MaxRing = 1.4;               // the ring grows at most this much (the scenery keeps clear to ~1.42x)
        public const double BossWidth = 1.35;            // a boss stands 1.35x (BattleController's view scale)

        // The current battle's ring scale (1 = the classic 4.2 x 3.95 m rim). BattleController sets it from the
        // line-up (RingScale) before spawning and puts it back to 1 when the battle ends; the arena's circle, the
        // slots and the camera's frame points all follow it, scaled about Centre.
        public static double Ring = 1;
        public static double Rx => RimRx * Ring;
        public static double Rz => RimRz * Ring;

        // How far the stand ellipse sits inside the rim for this widest unit (width 1 = a normal hero).
        public static double Inset(double maxWidth) => FootR * Math.Max(1, maxWidth) + FootMargin;
        static double MeanR(double ring) => (RimRx + RimRz) / 2 * ring;

        // Arc length (metres) a row of these widths needs: Spacing x the mean width of each neighbouring pair.
        public static double RowLength(IList<double> widths)
        {
            double total = 0;
            for (int k = 1; k < (widths?.Count ?? 0); k++) total += Spacing * (widths[k - 1] + widths[k]) / 2;
            return total;
        }
        static double MaxW(IList<double> a, IList<double> b) => Math.Max(a == null || a.Count == 0 ? 1 : a.Max(), b == null || b.Count == 0 ? 1 : b.Max());

        // The ring scale that fits both rows of this battle (widths per unit; null or empty = nobody on that side).
        public static double RingScale(IList<double> allies, IList<double> enemies)
        {
            double inset = Inset(MaxW(allies, enemies));
            double need = Math.Max(RowLength(allies), RowLength(enemies));   // the longer row decides
            double k = (need / (2 * MaxHalf) + inset) / MeanR(1);
            return Math.Max(1, Math.Min(MaxRing, k));
        }
        public static double RingScale(int allies, int enemies) => RingScale(Ones(allies), Ones(enemies));
        static double[] Ones(int n) { var a = new double[Math.Max(0, n)]; for (int i = 0; i < a.Length; i++) a[i] = 1; return a; }

        // Allies along the near arc, enemies along the far arc, ~2.2 m apart, all of normal width.
        public static V3 Slot(bool ally, int i, int n) => Slot(ally, i, Ones(Math.Max(1, n)));

        // As Slot, with per-unit widths (Creatures.SlotWidth / LineupWidth, bosses x BossWidth): each gap is
        // Spacing x the mean width of its two neighbours, the row stays centred, inside the ring at its current
        // scale (Ring) and within MaxHalf; a row too long even for that is squeezed (closer than Spacing).
        // maxWidth: the widest unit in the whole battle (both rows share one stand ellipse); 0 = this row's widest.
        public static V3 Slot(bool ally, int i, IList<double> widths, double maxWidth = 0)
        {
            int n = widths.Count;
            double inset = Inset(maxWidth > 0 ? maxWidth : widths.Max());
            double sx = Rx - inset, sz = Rz - inset, mean = (sx + sz) / 2;
            double t = 0;
            if (n > 1)
            {
                var at = new double[n]; double total = 0;
                for (int k = 1; k < n; k++) { total += Spacing * (widths[k - 1] + widths[k]) / 2; at[k] = total; }
                double half = total / 2 / mean, scale = Math.Min(1.0, MaxHalf / Math.Max(1e-6, half));
                t = (at[i] - total / 2) / mean * scale;
            }
            double a = ally ? t : Math.PI - t;               // 0 = near side (camera), PI = far side
            return new V3(RimCx + Math.Sin(a) * sx, 0, RimCz - Math.Cos(a) * sz);
        }

        // True when a footprint of radius r at p lies wholly inside the rim (sampled round its edge).
        public static bool InsideRim(V3 p, double r)
        {
            for (int k = 0; k < 32; k++)
            {
                double a = k * Math.PI / 16, x = p.X + Math.Cos(a) * r - RimCx, z = p.Z + Math.Sin(a) * r - RimCz;
                if (x * x / (Rx * Rx) + z * z / (Rz * Rz) > 1) return false;
            }
            return true;
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
        // Head height and stand-off come from the Titan's model data (TitanSpec, heroes_hd/titan.json):
        // 6.24 m (the old 4.8 m head times the battle view's 1.3 scale) for the current knight.
        public static double TitanHead => TitanSpec.Current.HeadWorld;
        public static double TitanBack => TitanSpec.Current.Back;
        public static double TitanFace => TitanSpec.Current.FaceWorld;   // where the cameras look it in the eye
        public static V3 TitanSlot() => new V3(RimCx, 0, RimCz + Rz) + new V3(0, 0, TitanBack); // the far rim (it towers behind it), then its stand-off
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

        static V3 NearArc => new V3(RimCx, 0, RimCz - Rz);
        public static V3 TeamCentre => new V3(0, 0, RimCz - Rz * 0.72);

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
                default:
                    return WormView(aspect);
            }
            double lo = 0, hi = 60;
            if (!TitanViewFits(rail(hi), aspect, head)) return rail(hi);
            for (int i = 0; i < 26; i++) { double m = (lo + hi) / 2; if (TitanViewFits(rail(m), aspect, head)) hi = m; else lo = m; }
            return rail(hi);
        }

        // Worm's-eye (owner pick, mock-up option C, 2026-11-26): 5 m behind the team at 0.9 m. The lens starts
        // at 62 degrees across (on a 2.17 screen) and widens only as far as needed so that, with the aim as low
        // as possible, the Titan's horns sit below WormTagTop (its name, HP and turn bars fit ABOVE the Titan,
        // under the HUD; owner request) while every hero stays on screen from the chest up with its tag.
        public const double WormBack = 5.0, WormHeight = 0.9, WormHfov = 62;
        public const double WormTagTop = 0.16;                 // NDC the horns may reach (the tag sits above them)
        // Where its name tag sits: above the horn tips (TitanSpec.HornTop), with room for the tag itself.
        public static double WormHornTop => TitanSpec.Current.HornWorld + 0.6;
        public static CamView WormView(double aspect)
        {
            var ts = TitanSlot();
            var eye = new V3(0, WormHeight, Slot(true, 1, 4).Z - WormBack);
            var horns = new V3(ts.X, WormHornTop, ts.Z);
            CamView best = new CamView { Eye = eye, Target = new V3(0, TitanHead * 0.6, ts.Z), Fov = 40 };
            for (double hfov = WormHfov; hfov <= 100; hfov += 1)
            {
                double vfov = 2 * Math.Atan(Math.Tan(hfov * Math.PI / 360) / Math.Max(0.3, aspect)) * 180 / Math.PI;
                CamView V(double ty) => new CamView { Eye = eye, Target = new V3(0, ty, ts.Z), Fov = vfov };
                double lo = 0, hi = 20;
                for (int i = 0; i < 30; i++)
                {
                    double m = (lo + hi) / 2; var q = ProjectTo(eye, V(m).Target, vfov, aspect, horns);
                    if (q != null && q.Value.y <= WormTagTop) hi = m; else lo = m;
                }
                best = V(hi);
                if (WormHeadsIn(best, aspect)) return best;
            }
            return best;
        }
        // Every ally's head (and the space for its tag above) is on screen in the worm's-eye view.
        public static bool WormHeadsIn(CamView v, double aspect)
        {
            for (int i = 0; i < 4; i++)
                foreach (var h in new[] { 1.1, 1.6, 2.4 })   // chest, head, tag
                {
                    var q = ProjectTo(v.Eye, v.Target, v.Fov, aspect, Slot(true, i, 4) + new V3(0, h, 0));
                    if (q == null || Math.Abs(q.Value.x) > 0.95 || q.Value.y > 0.84 || q.Value.y < -0.84) return false;
                }
            return true;
        }

        // ---------- Titan cinematics: framing rules (art review H3, 2026-11-28) ----------
        // The arrival is shot from the heroes' side: two keys, both low behind the team's row (their backs in
        // the foreground) looking up the Titan's front. Key A: 3.2 m behind the row at 1.1 m, aimed at its
        // waist (feet to chest in frame, the heroes' heads low in the foreground). Key B (the roar): 1.2 m behind
        // the row, between the two middle heroes, at 0.8 m, aimed up at its face (TitanFace). Then it blends into the view.
        // heroXs: the team's x positions. The camera stands in the gap nearest the middle, so with an odd team
        // (3 heroes: the middle one at x = 0) it no longer sits right behind a hero's back (in-house report,
        // 2026-11-28: Pell's back filled the frame and hid the Titan's face).
        public static (CamView a, CamView b) TitanIntroKeys(double aspect, IList<double> heroXs = null)
        {
            var ts = TitanSlot(); double row = Slot(true, 1, 4).Z; bool portrait = aspect < 0.75;
            double ex = IntroGapX(heroXs);
            var a = new CamView { Eye = new V3(ex, 1.1, row - 3.2), Target = new V3(ex * 0.5, TitanHead * 0.42, ts.Z), Fov = portrait ? 70 : 52 };
            var b = new CamView { Eye = new V3(ex, 0.8, row - 1.2), Target = new V3(0, TitanFace, ts.Z), Fov = portrait ? 74 : 56 };
            // Both keep the ground in frame: the aim drops just enough (the face then sits above the centre).
            a.Target = ClampTargetForGround(a.Eye, a.Target, a.Fov); b.Target = ClampTargetForGround(b.Eye, b.Target, b.Fov);
            return (a, b);
        }

        // The x of the gap between neighbouring heroes nearest the middle (0 with no team or an even one).
        public static double IntroGapX(IList<double> heroXs)
        {
            if (heroXs == null || heroXs.Count == 0) return 0;
            var xs = heroXs.OrderBy(x => x).ToList();
            if (xs.Count == 1) return xs[0] + (xs[0] > 0 ? -1 : 1) * Spacing / 2;
            double best = 0, bestD = double.MaxValue;
            for (int i = 1; i < xs.Count; i++) { double m = (xs[i - 1] + xs[i]) / 2; if (Math.Abs(m) < bestD) { bestD = Math.Abs(m); best = m; } }
            return best;
        }

        // A cinematic camera keeps the ground in frame (77-titan-4 showed the Titan alone in an empty sky):
        // the bottom edge of the frame must hit the ground within GroundSeen metres. Returns the highest
        // allowed pitch (degrees, + looks up) for a camera at eyeY with this vertical field of view.
        public const double GroundSeen = 30, GroundMargin = 2;
        public static double MaxPitchForGround(double eyeY, double vfovDeg)
        {
            double down = Math.Atan(Math.Max(0.05, eyeY) / GroundSeen) * 180 / Math.PI;
            return vfovDeg / 2 - down - GroundMargin;
        }
        public static double PitchOf(V3 eye, V3 target)
        {
            var d = target - eye; double h = Math.Sqrt(d.X * d.X + d.Z * d.Z);
            return Math.Atan2(d.Y, Math.Max(1e-6, h)) * 180 / Math.PI;
        }
        // Lowers a look target (keeping its heading and distance on the ground) until the ground rule holds.
        public static V3 ClampTargetForGround(V3 eye, V3 target, double vfovDeg)
        {
            double max = MaxPitchForGround(eye.Y, vfovDeg), p = PitchOf(eye, target);
            if (p <= max) return target;
            var d = target - eye; double h = Math.Sqrt(d.X * d.X + d.Z * d.Z);
            return new V3(target.X, eye.Y + Math.Tan(max * Math.PI / 180) * Math.Max(1e-6, h), target.Z);
        }

        // Any battle view (the usual presets, or a Titan view in a Titan fight). boss: a campaign boss stage (stage 6 of a
        // chapter, Normal and Hard), whose views that face the lair look up BossTilt degrees (BossView).
        public static CamView View(string key, double aspect, bool titan, bool boss = false)
        {
            var c = AnyCamera(key, titan);
            if (c.TitanOnly) return TitanView(c.Key, aspect);
            if (boss && !titan && Tilts(c)) return BossView(c, aspect);
            return new CamView { Eye = CameraPosition(c, FitDistance(c, aspect, titan), titan), Target = Target(titan), Fov = FovFor(aspect) };
        }

        // ---------- Boss-stage tilt (owner decision 2026-11-28, art review M1) ----------
        // On a campaign boss stage the camera tilts up a few degrees so the whole lair and more sky show behind the
        // enemies. The orbit (yaw, pitch around CamTarget) is unchanged; the look direction is raised BossTilt degrees
        // about the camera's own right axis, then the distance is refitted so the rim, the heroes and their tags
        // (FramePoints) stay inside the frame at this aspect. Hero view looks back at the team, away from the lair, and
        // keeps its usual framing.
        public const double BossTilt = 6;
        public static bool Tilts(CameraPreset c) => c != null && !c.TitanOnly && c.Key != "front";

        // The tilted look target for an eye that orbits CamTarget: same heading and distance, BossTilt degrees higher.
        public static V3 TiltedTarget(V3 eye, V3 target, double tiltDeg)
        {
            var d = target - eye; double h = Math.Sqrt(d.X * d.X + d.Z * d.Z), len = d.Length;
            double pitch = Math.Atan2(d.Y, Math.Max(1e-6, h)) + tiltDeg * Math.PI / 180;
            double kx = h > 1e-6 ? d.X / h : 0, kz = h > 1e-6 ? d.Z / h : 1;
            return eye + new V3(kx * Math.Cos(pitch), Math.Sin(pitch), kz * Math.Cos(pitch)) * len;
        }

        // True when every frame point (rim, heads, HUD tags) sits inside |x| <= 0.95, |y| <= 0.84 of this view.
        public static bool FrameFits(CamView v, double aspect, bool titan = false)
        {
            foreach (var p in FramePoints(titan))
            {
                var q = ProjectTo(v.Eye, v.Target, v.Fov, aspect, p);
                if (q == null || Math.Abs(q.Value.x) > 0.95 || q.Value.y > 0.84 || q.Value.y < -0.84) return false;
            }
            return true;
        }

        public static CamView BossView(CameraPreset c, double aspect)
        {
            double fov = FovFor(aspect);
            CamView At(double dist) { var eye = CameraPosition(c, dist); return new CamView { Eye = eye, Target = TiltedTarget(eye, CamTarget, BossTilt), Fov = fov }; }
            double lo = 5, hi = 60;
            for (int i = 0; i < 24; i++) { double mid = (lo + hi) / 2; if (FrameFits(At(mid), aspect)) hi = mid; else lo = mid; }
            return At(hi);
        }

        // Points that must stay on screen: the whole rim with head height and HUD-tag points above it.
        public static V3[] FramePoints(bool titan)
        {
            var pts = new System.Collections.Generic.List<V3>();
            foreach (int sx in new[] { -1, 1 })
                for (int k = 0; k <= 6; k++)
                {
                    double ang = k / 6.0 * Math.PI, x = sx * Math.Sin(ang) * (Rx + 0.4), zThree = -0.6 + Math.Cos(ang) * (Rz + 0.4);
                    pts.Add(new V3(x, 0, -zThree)); pts.Add(new V3(x, zThree > 0 ? 2.75 : 3.3, -zThree));
                }
            pts.Add(new V3(0, 3.75, RimCz + (Rz - 0.6)));      // HUD tags above the back row (3.95 at Ring 1)
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
