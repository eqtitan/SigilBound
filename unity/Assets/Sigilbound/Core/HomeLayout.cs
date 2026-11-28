// Portrait Home layout (in-house report S4: the quest and login cards hid the 3D team). The team stands in a
// band of the screen fixed by the menu camera (Arena.FrameMenu); the cards fold into a compact strip of tabs
// above that band, an opened tab shows its card in a sheet below it, and "Continue Campaign" stays pinned at
// the bottom. Pure maths, so Tests~ can check it on every phone size.
using System;

namespace Sigilbound.Core
{
    public readonly struct URect
    {
        public readonly double X, Y, W, H;
        public URect(double x, double y, double w, double h) { X = x; Y = y; W = w; H = h; }
        public double Bottom => Y + H;
        public bool Overlaps(URect o) => X < o.X + o.W && o.X < X + W && Y < o.Bottom && o.Y < Bottom;
    }

    public static class HomeLayout
    {
        // The menu camera (Arena.FrameMenu: eye, look-at target, vertical field of view) and the lineup's spot
        // (App.UpdateLineup: rows at z 0.6 and 1.1; heroes about 2.3 m tall, a little more with weapons up).
        public static readonly double[] MenuEye = { 0, 7.2, -9.6 }, MenuTarget = { 0, 0.9, 1.1 };
        public const double MenuFov = 42, LineupNearZ = 0.6, LineupFarZ = 1.1, LineupTop = 2.5;

        // The team's screen band as fractions of the screen height (0 = top), from the camera above.
        // Unity keeps the vertical field of view on every aspect, so the band is the same on every phone.
        public static (double top, double bottom) TeamBand()
        {
            double Frac(double y, double z)
            {
                double fy = MenuTarget[1] - MenuEye[1], fz = MenuTarget[2] - MenuEye[2];
                double pitch = Math.Atan2(-fy, fz), a = Math.Atan2(MenuEye[1] - y, z - MenuEye[2]);
                return 0.5 + 0.5 * Math.Tan(a - pitch) / Math.Tan(MenuFov * Math.PI / 360);
            }
            return (Frac(LineupTop, LineupFarZ), Frac(0, LineupNearZ));
        }

        // ---------- compact landscape Home: the camera slides sideways ----------
        // The team's half-width in metres: the widest lineup (Creatures.MaxLineupSpan, 5.2 m) plus a body's half-width.
        public const double LineupHalfW = 3.1;
        // How far along the camera's view a point lies, and its screen x as a fraction of the width (0 = left)
        // with the camera moved sideways by shift metres (x right). aspect = width / height.
        static double Depth(double y, double z)
        {
            double fy = MenuTarget[1] - MenuEye[1], fz = MenuTarget[2] - MenuEye[2], n = Math.Sqrt(fy * fy + fz * fz);
            return ((y - MenuEye[1]) * fy + (z - MenuEye[2]) * fz) / n;
        }
        static double HalfW(double depth, double aspect) => depth * Math.Tan(MenuFov * Math.PI / 360) * aspect;
        public static double ScreenX(double x, double y, double z, double aspect, double shift) => 0.5 + 0.5 * (x - shift) / HalfW(Depth(y, z), aspect);
        // The sideways shift that centres the team's screen span (feet to heads, both rows) on fraction centre
        // (Arena.MenuShift). Perspective spreads the near feet wider than the heads, so it settles in a few steps.
        public static double ShiftFor(double centre, double aspect)
        {
            double hw = HalfW(Depth(LineupTop / 2, (LineupNearZ + LineupFarZ) / 2), aspect), s = (0.5 - centre) * 2 * hw;
            for (int i = 0; i < 6; i++) { var (l, r) = TeamSpanX(aspect, s); s += ((l + r) / 2 - centre) * 2 * hw; }
            return s;
        }
        // The team's left and right screen fractions with that shift: both rows, feet to heads.
        public static (double left, double right) TeamSpanX(double aspect, double shift)
        {
            double l = 1, r = 0;
            foreach (double z in new[] { LineupNearZ, LineupFarZ })
                foreach (double y in new[] { 0.0, LineupTop })
                    foreach (double x in new[] { -LineupHalfW, LineupHalfW })
                    { double f = ScreenX(x, y, z, aspect, shift); l = Math.Min(l, f); r = Math.Max(r, f); }
            return (l, r);
        }

        public const double TabH = 48, Gap = 8, CtaMin = 56, Margin = 0.02;

        // area: the content rect under the top bar and above the nav (UI units); screenH: Ui.H; tap: Ui.Tap.
        // Title and subtitle first, then the tab strip; the sheet fills from under the team's feet to the
        // pinned button (it scrolls if a card is taller).
        public static (URect title, URect strip, URect team, URect sheet, URect cta) Portrait(URect area, double screenH, double tap, double pad = 16)
        {
            var (bt, bb) = TeamBand();
            double teamTop = (bt - Margin) * screenH, teamBottom = (bb + Margin) * screenH;
            double ctaH = Math.Max(CtaMin, tap + 8), tabH = Math.Max(TabH, tap);
            var cta = new URect(pad, area.Bottom - ctaH - 8, area.W - pad * 2, ctaH);
            var title = new URect(area.X, area.Y + 4, area.W, 58);
            // The strip sits under the title, or squeezes up beside a shorter title on very short screens, but
            // never into the team.
            double sy = Math.Min(title.Bottom + 2, teamTop - tabH - Gap);
            sy = Math.Max(area.Y + 2, sy);
            var strip = new URect(pad, sy, area.W - pad * 2, tabH);
            var team = new URect(area.X, Math.Max(strip.Bottom, teamTop), area.W, Math.Max(0, teamBottom - Math.Max(strip.Bottom, teamTop)));
            double sheetTop = Math.Max(team.Bottom + Gap, strip.Bottom + Gap);
            var sheet = new URect(pad, sheetTop, area.W - pad * 2, Math.Max(0, cta.Y - Gap - sheetTop));
            return (title, strip, team, sheet, cta);
        }
    }
}
