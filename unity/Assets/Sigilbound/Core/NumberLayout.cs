// Where a floating damage number appears on screen (art review M3, PMO P2-10). Plain math (screen pixels,
// Unity convention: x right, y UP from the bottom edge) so it is testable outside Unity; Client/Vfx.cs draws them.
//   - above the unit's head tag, never on its body: the anchor is the tag point, the number starts AboveTag of
//     its own height higher, with a small random sideways spread;
//   - several at once (multi-hits, AoE, a crit and its WEAK! tag) stack upward instead of piling on one spot;
//   - the whole float (start to the top of its rise) stays inside the screen and below the HUD band at the top.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    // A number's box on screen: centre (X, Y) and size (W, H), in pixels.
    public struct NumBox
    {
        public double X, Y, W, H;
        public NumBox(double x, double y, double w, double h) { X = x; Y = y; W = w; H = h; }
        public bool Overlaps(NumBox o, double pad = 0) =>
            Math.Abs(X - o.X) * 2 < W + o.W + pad * 2 && Math.Abs(Y - o.Y) * 2 < H + o.H + pad * 2;
    }

    public static class NumberLayout
    {
        public const double AboveTag = 1.15;   // start this many of its own heights above the tag point (clears the name, bars and icons)
        public const double Spread = 0.9;      // random sideways spread: up to this many heights each way
        public const double Pad = 0.12;        // gap between stacked numbers (heights)
        public const double Rise = 1.1;        // how far it floats up over its life (heights); 0 with Reduce motion
        public const double Margin = 8;        // pixels kept clear of the screen's left, right and bottom edges

        // Width of a number in pixels for its text at line height h (bold digits are ~0.62 of the height; the
        // second line, a WEAK!/RESIST tag, is narrower than most numbers so the first line decides).
        public static double Width(string text, double h)
        {
            if (string.IsNullOrEmpty(text)) return h;
            string first = text.Split('\n')[0];
            return Math.Max(h, first.Length * h * 0.62);
        }

        // Places a number of size (w, h) for the tag point (ax, ay). live: the numbers on screen now (their current
        // boxes). rnd: -1..1, the random sideways pick. top: pixels reserved at the top (header, turn bar).
        // rise: how far it will float (pixels). Returns the start box.
        public static NumBox Place(double ax, double ay, double w, double h, IList<NumBox> live, double screenW, double screenH,
                                   double top, double rise, double rnd)
        {
            var b = new NumBox(ax + Math.Max(-1, Math.Min(1, rnd)) * Spread * h, ay + AboveTag * h, w, h);
            Clamp(ref b, screenW, screenH, top, rise);
            if (live == null || live.Count == 0 || Free(b, live, h)) return b;
            // Stack: rows of its own height upward from the start (the usual case), then downward when the top of the
            // screen is in the way, then side by side; the first free spot wins.
            double step = h + Pad * h;
            foreach (int dir in new[] { 1, -1 })
                for (int k = 1; k <= 48; k++)
                {
                    var c = b; c.Y = b.Y + dir * k * step;
                    if (c.Y + h / 2 + rise > screenH - top || c.Y - h / 2 < Margin) break;   // off screen: this way is full
                    if (Free(c, live, h)) return c;
                }
            for (int k = 1; k <= 48; k++)
                foreach (int dir in new[] { 1, -1 })
                {
                    var c = b; c.X = b.X + dir * k * (w * 0.5 + Pad * h);
                    Clamp(ref c, screenW, screenH, top, rise);
                    if (Free(c, live, h)) return c;
                }
            return b;
        }
        static bool Free(NumBox b, IList<NumBox> live, double h)
        {
            foreach (var o in live) if (b.Overlaps(o, Pad * h * 0.5)) return false;
            return true;
        }

        // Keeps the box, and its whole rise, on screen: inside the side and bottom margins, under the top band.
        public static void Clamp(ref NumBox b, double screenW, double screenH, double top, double rise)
        {
            double minX = b.W / 2 + Margin, maxX = screenW - b.W / 2 - Margin;
            b.X = minX > maxX ? screenW / 2 : Math.Max(minX, Math.Min(maxX, b.X));
            double minY = b.H / 2 + Margin, maxY = screenH - top - b.H / 2 - rise;
            b.Y = minY > maxY ? minY : Math.Max(minY, Math.Min(maxY, b.Y));
        }

        // True when no two boxes overlap and each, with its rise, is inside the screen (tests).
        public static bool Clear(IList<NumBox> boxes, double screenW, double screenH, double top, double rise)
        {
            for (int i = 0; i < boxes.Count; i++)
            {
                var b = boxes[i];
                if (b.X - b.W / 2 < 0 || b.X + b.W / 2 > screenW || b.Y - b.H / 2 < 0 || b.Y + b.H / 2 + rise > screenH - top + 1e-6) return false;
                for (int j = i + 1; j < boxes.Count; j++) if (b.Overlaps(boxes[j])) return false;
            }
            return true;
        }
    }
}
