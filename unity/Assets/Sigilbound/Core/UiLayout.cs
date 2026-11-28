// Screen layout arithmetic shared by the client and Tests~ (UI/UX consistency review, design rules 3, 6 and 12).
// The rects for the wide screens (team setup, Home, the stage list rows, the results footer, the modes hub),
// the button-row order and the modal and toast widths are plain arithmetic, so they live here without
// UnityEngine: Tests~/UxRulesTests.cs runs them at the three reference widths (640 = 4:3 tablet, 1067 = 16:9,
// 1422 = 21:9) and checks that nothing overlaps or is cut. The client converts Box to Rect and draws.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    // A rectangle in logical UI units (x right, y down), like UnityEngine.Rect.
    public readonly struct Box
    {
        public readonly float X, Y, W, H;
        public Box(float x, float y, float w, float h) { X = x; Y = y; W = w; H = h; }
        public float XMax => X + W; public float YMax => Y + H;
        public float CenterX => X + W / 2; public float CenterY => Y + H / 2;
        // Touching edges are not an overlap; a quarter unit of slack for float rounding.
        public bool Overlaps(Box o) => W > 0 && H > 0 && o.W > 0 && o.H > 0 && X < o.XMax - 0.25f && o.X < XMax - 0.25f && Y < o.YMax - 0.25f && o.Y < YMax - 0.25f;
        public bool Inside(Box o) => X >= o.X - 0.25f && Y >= o.Y - 0.25f && XMax <= o.XMax + 0.25f && YMax <= o.YMax + 0.25f;
        public override string ToString() => $"({X:0},{Y:0} {W:0}x{H:0})";
    }

    // The three button roles (design rule 3): one Primary per screen or modal, drawn last (right); Secondary for
    // the other actions; Danger only for destructive or stop actions.
    public enum UiRole { Primary, Secondary, Danger }

    public static class UiLayout
    {
        // ---------- widths ----------
        // Landscape screens narrower than this (4:3 tablets at 640, a 1024x768 window at 800) use the compact
        // layouts: tools on their own row, two columns instead of three, stacked footers.
        public const float NarrowW = 900;
        public static bool Compact(float fullW) => fullW < NarrowW;
        // The reference widths every wide screen must work at (design rule 12).
        public static readonly float[] ReferenceWidths = { 640, 1067, 1422 };

        // Modals and toasts take their width from the safe screen, never from the screen behind (rule 6).
        public const float ModalW = 560, WideModalW = 900, ToastMaxW = 620;
        public static float ModalWidth(float safeW, float max) => Math.Max(0, Math.Min(safeW - 32, max));
        public static float ToastWidth(float safeW) => Math.Max(0, Math.Min(safeW - 60, ToastMaxW));

        // ---------- button rows (rule 3) ----------
        // Display order: Secondary and Danger in the order given, the Primary last.
        public static int[] RowOrder(IList<UiRole> roles)
        {
            var order = new List<int>(roles.Count);
            for (int i = 0; i < roles.Count; i++) if (roles[i] != UiRole.Primary) order.Add(i);
            for (int i = 0; i < roles.Count; i++) if (roles[i] == UiRole.Primary) order.Add(i);
            return order.ToArray();
        }
        // How many rows n buttons need in width w (each at least minW wide).
        public static int RowCount(float w, int n, float gap, float minW)
        {
            if (n <= 0) return 0;
            int per = Math.Max(1, (int)Math.Floor((w + gap) / (minW + gap)));
            return (n + per - 1) / per;
        }
        public static float RowHeight(float w, int n, float gap, float minW, float h) { int rows = RowCount(w, n, gap, minW); return rows * h + Math.Max(0, rows - 1) * gap; }
        // The rects of a row of buttons inside r (bottom-aligned), indexed like roles. Buttons share the width (at
        // most maxW each, centred); when they don't fit at minW they wrap upward, the Primary staying in the
        // bottom row, rightmost.
        public static Box[] ButtonRow(Box r, IList<UiRole> roles, float h, float gap = 10, float minW = 120, float maxW = 220)
        {
            int n = roles.Count; var res = new Box[n];
            if (n == 0) return res;
            var order = RowOrder(roles);
            int per = Math.Max(1, (int)Math.Floor((r.W + gap) / (minW + gap)));
            int rows = (n + per - 1) / per;
            int idx = n;
            for (int row = rows - 1; row >= 0; row--) // fill rows from the bottom: the last items (the Primary) in the bottom row
            {
                int count = row == rows - 1 ? n - (rows - 1) * per : per;
                if (count <= 0) continue;
                idx -= count;
                float bw = Math.Min(maxW, (r.W - gap * (count - 1)) / count);
                float x = r.CenterX - (bw * count + gap * (count - 1)) / 2;
                float y = r.YMax - h - (rows - 1 - row) * (h + gap);
                for (int k = 0; k < count; k++) res[order[idx + k]] = new Box(x + k * (bw + gap), y, bw, h);
            }
            return res;
        }

        // ---------- team setup (landscape) ----------
        public struct PrepRects
        {
            public bool Compact, Wheel;
            public Box Header, Back, Title, Best, AutoEquip;
            public Box Team;    // sub line, the four slots, Battle, presets (and the waves, compact)
            public Box Waves;   // wheel, matchup and waves (wide); empty when compact
            public Box Picker;  // filter chips, sort chips and the hero picker
        }
        public const float BackW = 104, BestW = 112, AutoEquipW = 112;
        // One header row (Back, the title, Best team, Auto-equip), then three columns at 16:9 and wider, or two
        // (team, picker) below NarrowW, with the waves under the team.
        public static PrepRects Prep(Box area, float tap, float fullW)
        {
            float pad = 12, gap = 10;
            var p = new PrepRects { Compact = Compact(fullW) };
            p.Header = new Box(area.X + pad, area.Y + 8, area.W - 2 * pad, tap);
            p.Back = new Box(p.Header.X, p.Header.Y, Math.Max(BackW, tap), tap);
            p.AutoEquip = new Box(p.Header.XMax - AutoEquipW, p.Header.Y, AutoEquipW, tap);
            p.Best = new Box(p.AutoEquip.X - 6 - BestW, p.Header.Y, BestW, tap);
            p.Title = new Box(p.Back.XMax + 10, p.Header.Y, Math.Max(0, p.Best.X - 10 - (p.Back.XMax + 10)), tap);
            float y0 = p.Header.YMax + 8, bh = area.YMax - 8 - y0, x0 = area.X + pad, w = area.W - 2 * pad;
            if (p.Compact)
            {
                float lw = (w - gap) * 0.5f;
                p.Team = new Box(x0, y0, lw, bh);
                p.Picker = new Box(x0 + lw + gap, y0, w - lw - gap, bh);
                p.Waves = new Box(x0, y0, 0, 0);
            }
            else
            {
                float lw = area.W * 0.36f - pad, mw = area.W * 0.27f - gap;
                p.Team = new Box(x0, y0, lw, bh);
                p.Waves = new Box(p.Team.XMax + gap, y0, mw, bh);
                p.Picker = new Box(p.Waves.XMax + gap, y0, area.XMax - pad - (p.Waves.XMax + gap), bh);
                p.Wheel = true;
            }
            return p;
        }
        // Inside the team column: the sub line, the four slots, Battle and the presets row, top to bottom.
        public struct TeamRects { public Box Sub; public Box[] Slots; public Box Go; public Box[] Presets; public Box Rest; }
        public static TeamRects Team(Box col, float tap)
        {
            var t = new TeamRects { Slots = new Box[4], Presets = new Box[3] };
            t.Sub = new Box(col.X, col.Y, col.W, 20);
            float sw = (col.W - 3 * 6) / 4f, sy = t.Sub.YMax + 4;
            for (int i = 0; i < 4; i++) t.Slots[i] = new Box(col.X + i * (sw + 6), sy, sw, 66);
            float goH = Math.Max(48, tap);
            t.Go = new Box(col.X, sy + 66 + 8, col.W, goH);
            float pw = (col.W - 2 * 6) / 3f, py = t.Go.YMax + 8;
            for (int i = 0; i < 3; i++) t.Presets[i] = new Box(col.X + i * (pw + 6), py, pw, tap);
            float ry = py + tap + 8;
            t.Rest = new Box(col.X, ry, col.W, Math.Max(0, col.YMax - ry));
            return t;
        }

        // ---------- painted card frames (L028) ----------
        // A 9-slice frame's border widths in texture pixels (Resources/Skin/skin.txt: "name left top right bottom").
        public readonly struct Border9
        {
            public readonly float L, T, R, B;
            public Border9(float l, float t, float r, float b) { L = l; T = t; R = r; B = b; }
        }
        // The scale Skin.DrawFrame paints a frame's borders at in rect w x h: at most maxK, and shrunk on small rects
        // so the corners never fold over each other. The client draws with this, the checks below measure with it.
        public static float FrameScale(float w, float h, Border9 b, float maxK) =>
            Math.Max(0, Math.Min(maxK, Math.Min(w / Math.Max(1, b.L + b.R), h / Math.Max(1, b.T + b.B)) * 0.85f));
        // The content rect inside a frame drawn in r (around = false): r inset by the painted borders. Text and faces
        // go in here, never on the border (build 207: the chip plate's crown ornaments covered the name and power).
        public static Box FrameInner(Box r, Border9 b, float maxK)
        {
            float k = FrameScale(r.W, r.H, b, maxK);
            float l = b.L * k, t = b.T * k, rr = b.R * k, bt = b.B * k;
            return new Box(r.X + l, r.Y + t, Math.Max(0, r.W - l - rr), Math.Max(0, r.H - t - bt));
        }
        // Hero and preset cards use the small card frame (skin "card_small") at this scale: about a 9-unit border.
        // The chip plates (button_chip, button_chipon) are for one-line labels only: their crown ornaments sit in the
        // stretched middle column, so two lines of text always land on them.
        public const float CardFrameK = 0.14f;
        public static readonly Border9 CardSmallFallback = new Border9(64, 64, 64, 64);
        public static float CardH(float tap) => Math.Max(56, tap + 8);

        // A hero card in a team picker: the face at the left of the content rect, the element badge and the name on
        // the first line, stars, level and power (or element) on the second; all inside the frame's content rect.
        public struct CardRects { public Box Frame, Inner, Face, Badge, Name, Line; }
        public const float RarityTabW = 16;
        public static CardRects HeroCard(Box r, Border9 frame, bool face)
        {
            var c = new CardRects { Frame = r, Inner = FrameInner(r, frame, CardFrameK) };
            var i = c.Inner;
            // The face keeps 2 units clear inside the content rect (its rarity rim is drawn 2 units outside it).
            float fs = face ? Math.Max(0, Math.Min(i.H - 4, 44)) : 0;
            c.Face = new Box(i.X + 2, i.Y + (i.H - fs) / 2, fs, fs);
            float x = i.X + (face ? fs + 8 : 2), lh = i.H / 2;
            c.Badge = new Box(x, i.Y + (lh - 15) / 2, 15, 15);
            // The rarity letter sits on the frame's top-right corner; the name keeps clear of it.
            float nx = c.Badge.XMax + 4;
            c.Name = new Box(nx, i.Y, Math.Max(0, Math.Min(i.XMax, r.XMax - RarityTabW - 4) - nx), lh);
            c.Line = new Box(x, i.Y + lh, Math.Max(0, i.XMax - x), i.H - lh);
            return c;
        }
        // The hero picker under the filter and sort chips (landscape team setup): two columns of cards when each
        // keeps 160 units, else one.
        public struct PickerGrid { public Box View; public int Cols; public float CardW, CardH, Gap; public Box Cell(int i) => new Box((i % Cols) * (CardW + Gap), (i / Cols) * (CardH + Gap), CardW, CardH); }
        public const float PickCardMinW = 160;
        public static PickerGrid Picker(Box picker, float tap)
        {
            float fy = picker.Y + 8 + tap + 4; // the filter chips, then the sort chips
            var g = new PickerGrid { Gap = 6, View = new Box(picker.X + 6, fy + tap + 6, picker.W - 12, Math.Max(0, picker.YMax - fy - tap - 12)) };
            g.Cols = (g.View.W - g.Gap) / 2 >= PickCardMinW ? 2 : 1;
            g.CardW = (g.View.W - g.Gap * (g.Cols - 1)) / g.Cols;
            g.CardH = CardH(tap);
            return g;
        }

        // ---------- buttons: the label's room ----------
        // How far a label keeps from a button's sides: the style's 6-unit padding plus the painted plate's rounded
        // end (Ghost buttons have no plate). A trailing icon (" ▶") takes its size and a 4-unit gap.
        public static float ButtonInset(float h, bool ghost = false) => ghost ? 6 : 6 + Math.Min(18, h * 0.3f);
        public static float LabelRoom(Box button, bool tailIcon) => Math.Max(0, button.W - 2 * ButtonInset(button.H) - (tailIcon ? Math.Min(16, button.H * 0.36f) + 4 : 0));

        // ---------- equipment rows ----------
        // The item's name on its own line across the row (at 1024x768 the name ran under Lock), then the main stat
        // and the sub-stats on the left beside two button rows on the right: Lock and Upgrade, Sell and Equip.
        public struct GearRowRects { public Box Inner, Name, Main, Subs, Lock, Up, Sell, Equip; }
        public const float GearNameH = 22, GearLockW = 64, GearUpW = 104;
        public static float GearRowH(float tap) => GearNameH + 2 * tap + 4 + 2 + 2 * 10;
        public static GearRowRects GearRow(Box r, Border9 frame, float tap)
        {
            var g = new GearRowRects { Inner = FrameInner(r, frame, CardFrameK) };
            var i = g.Inner;
            g.Name = new Box(i.X + 4, i.Y, Math.Max(0, i.W - 8), GearNameH);
            float by = g.Name.YMax + 2, ux = i.XMax - GearUpW, lx = ux - GearLockW - 6;
            g.Up = new Box(ux, by, GearUpW, tap); g.Lock = new Box(lx, by, GearLockW, tap);
            g.Equip = new Box(ux, by + tap + 4, GearUpW, tap); g.Sell = new Box(lx, by + tap + 4, GearLockW, tap);
            float tw = Math.Max(0, lx - 8 - (i.X + 4));
            g.Main = new Box(i.X + 4, by + 2, tw, 18);
            g.Subs = new Box(i.X + 4, g.Main.YMax + 2, tw, Math.Max(0, i.YMax - (g.Main.YMax + 2)));
            return g;
        }

        // ---------- Home (landscape) ----------
        public struct HomeRects { public bool Compact; public Box Left, Right, Cta, Title, Team; }
        // Wide: quests on the left, login and the Continue button on the right, the team in 3D between. Compact
        // (4:3 tablets, a 1024x768 window; in-house report build 207: the 360-unit column covered the third hero
        // and Continue hid the Achievements card): one card column on the right edge (login, achievements, quests,
        // the help chips) using the full height, the title, the team and Continue in the space on its left. The
        // menu camera slides sideways so the team stands in that space (HomeLayout.ShiftFor).
        public static HomeRects Home(Box area, float tap, float fullW)
        {
            var h = new HomeRects { Compact = Compact(fullW) };
            float gap = 24, ctaH = Math.Max(60, tap + 10);
            if (h.Compact)
            {
                float cw = Math.Min(320, Math.Max(260, area.W * 0.4f));
                h.Right = new Box(area.XMax - 16 - cw, area.Y + 8, cw, area.H - 12);
                h.Left = h.Right; // the one scrolling column
                h.Team = new Box(area.X, area.Y, Math.Max(0, h.Right.X - 12 - area.X), area.H);
                float bw = Math.Min(360, h.Team.W - 32);
                h.Cta = new Box(h.Team.CenterX - bw / 2, area.YMax - 8 - ctaH, bw, ctaH);
                h.Title = new Box(area.X + 8, area.Y + 4, h.Team.W - 16, 40);
                return h;
            }
            float colW = Math.Max(230, Math.Min(320, (area.W - 350) / 2));
            h.Left = new Box(area.X + gap, area.Y + 20, colW, area.H - 28);
            h.Right = new Box(area.XMax - gap - colW, area.Y + 20, colW, area.H - 28);
            h.Cta = new Box(h.Right.X, h.Right.YMax - ctaH, colW, ctaH);
            h.Title = new Box(h.Left.XMax + 8, area.Y + 4, h.Right.X - h.Left.XMax - 16, 40);
            h.Team = new Box(h.Left.XMax, area.Y, h.Right.X - h.Left.XMax, area.H);
            return h;
        }
        // Home's cards: login 64 tall, achievements 58, quests 40 plus a 48-unit row each, 8 apart.
        public const float LoginH = 64, AchievementsH = 58, QuestRowH = 48, HomeGap = 8;
        public static float QuestsH(int quests) => 40 + quests * QuestRowH;
        // The compact column's content: login, achievements, quests, then the help chips.
        public static float HomeColumnH(int quests, float chipsH) => LoginH + HomeGap + AchievementsH + HomeGap + QuestsH(quests) + HomeGap + chipsH;

        // ---------- stage list row ----------
        // The Team button fits "TEAM" and its arrow on one line (build 207 wrapped it to "TEA / M" at 88).
        public const float StageButtonW = 108;
        public struct StageRowRects { public Box Label, Enemies, Gold, Shard, Gear, Button; public float EnemyW; }
        // Right to left: the Team button, the drops (gold, shards, then the gear chance if it fits), and the
        // enemies in what is left after the label, so the drops never run under the button.
        public static StageRowRects StageRow(Box row, int enemies, bool shards)
        {
            var s = new StageRowRects();
            float lw = Math.Max(112, Math.Min(170, row.W * 0.25f));
            s.Label = new Box(row.X + 8, row.Y, lw - 8, row.H);
            s.Button = new Box(row.XMax - StageButtonW - 4, row.Y + 3, StageButtonW, row.H - 6);
            float right = s.Button.X - 6;
            if (shards) { s.Shard = new Box(right - 40, row.Y, 40, row.H); right = s.Shard.X - 4; }
            s.Gold = new Box(right - 60, row.Y, 60, row.H); right = s.Gold.X - 4;
            float ex = row.X + lw + 4;
            float roomForEnemies = right - ex;
            // The gear chance only when the enemies keep at least 60 units each.
            if (roomForEnemies - 58 >= 60 * Math.Max(1, enemies)) { s.Gear = new Box(right - 54, row.Y, 54, row.H); right = s.Gear.X - 4; }
            float ew = Math.Min(120, Math.Max(0, right - ex) / Math.Max(1, enemies));
            s.EnemyW = ew;
            s.Enemies = new Box(ex, row.Y, ew * Math.Max(1, enemies), row.H);
            return s;
        }
        // The stage list panel: 62% of the map at 16:9 and wider (beside the chapter's medallion), almost all of it
        // when compact.
        public static Box StageList(Box area, bool rightSide, float fullW)
        {
            float w = Compact(fullW) ? area.W - 16 : Math.Min(area.W * 0.62f, 660), h = area.H - 16;
            return new Box(rightSide ? area.XMax - w - 8 : area.X + 8, area.Y + 8, w, h);
        }

        // ---------- results footer ----------
        public struct FooterRects { public bool Stacked; public Box Toggles, Buttons; public float Height; }
        public const float ToggleMinW = 140;
        // The Replay / Climb switches and the mode's buttons: side by side when every button keeps 120 units,
        // else the switches on their own row above the buttons.
        public static FooterRects ResultsFooter(Box r, int toggles, int buttons, float tap)
        {
            var f = new FooterRects();
            float tw = toggles <= 0 ? 0 : toggles * 150 + (toggles - 1) * 8;
            float side = toggles > 0 ? r.W - tw - 16 : r.W;
            if (RowCount(side, buttons, 10, 120) <= 1)
            {
                f.Toggles = new Box(r.X, r.YMax - tap, tw, tap);
                float ax = toggles > 0 ? r.X + tw + 16 : r.X;
                f.Buttons = new Box(ax, r.YMax - tap, r.XMax - ax, tap);
                f.Height = tap;
                return f;
            }
            float rh = RowHeight(r.W, buttons, 10, 120, tap);
            f.Buttons = new Box(r.X, r.YMax - rh, r.W, rh);
            f.Height = rh;
            if (toggles > 0)
            {
                f.Stacked = true;
                tw = Math.Min(r.W, tw);
                f.Toggles = new Box(r.CenterX - tw / 2, f.Buttons.Y - 8 - tap, tw, tap);
                f.Height = rh + 8 + tap;
            }
            return f;
        }
        // The width of each switch inside the toggles rect.
        public static float ToggleW(Box toggles, int n) => n <= 0 ? 0 : Math.Min(150, (toggles.W - 8 * (n - 1)) / n);

        // ---------- battle corner buttons ----------
        // n round buttons of size bs from `right` leftward, gap bg; a button that would start left of minX (the
        // Leave button's edge) begins a new row under the first, right-aligned. Portrait at 56 dp needs this: seven
        // buttons are about 426 units on a 390-wide screen (in-house report build 190: View landed on Leave).
        public static Box[] HudButtons(float right, float top, float minX, float bs, float bg, int n)
        {
            var res = new Box[n];
            float x = right - bs, y = top;
            for (int i = 0; i < n; i++)
            {
                if (x < minX - 0.01f && i > 0) { x = right - bs; y += bs + bg; }
                res[i] = new Box(x, y, bs, bs);
                x -= bs + bg;
            }
            return res;
        }

        // ---------- the modes hub (landscape) ----------
        // One row of cards at 16:9 and wider; two rows (three over two) when compact, so titles keep their size.
        public static Box[] Modes(Box area, int n, float fullW)
        {
            var res = new Box[n];
            float pad = 14, gap = 12, top = area.Y + 12, h = area.H - 24;
            if (!Compact(fullW))
            {
                float w = Math.Min(260, (area.W - pad * 2 - gap * (n - 1)) / n);
                float x = area.X + (area.W - (w * n + gap * (n - 1))) / 2;
                for (int i = 0; i < n; i++) res[i] = new Box(x + i * (w + gap), top, w, h);
                return res;
            }
            int top3 = (n + 1) / 2, bottom = n - top3;
            float rh = (h - gap) / 2;
            float cw = Math.Min(260, (area.W - pad * 2 - gap * (top3 - 1)) / top3);
            for (int i = 0; i < n; i++)
            {
                bool first = i < top3; int k = first ? i : i - top3, cnt = first ? top3 : bottom;
                float x = area.X + (area.W - (cw * cnt + gap * (cnt - 1))) / 2;
                res[i] = new Box(x + k * (cw + gap), top + (first ? 0 : rh + gap), cw, rh);
            }
            return res;
        }
    }

    // Text colours that read on the dark panels (WCAG 4.5:1; in-house report build 190: some colour-blind
    // palette colours were 3.2:1 as text). Coloured words (element, rarity, gear, good/bad) are lifted toward white
    // until they reach 4.5:1 on the panel; swatches, frames and bars keep the palette colour. Dark label colours
    // (meant for gold plates and light chips) are left alone.
    public static class TextColour
    {
        static double Ch(double c) => c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        public static double Luminance(float r, float g, float b) => 0.2126 * Ch(r) + 0.7152 * Ch(g) + 0.0722 * Ch(b);
        public static double Contrast(double la, double lb) => (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        public const double Min = 4.5, DarkLabel = 0.05;
        public static (float r, float g, float b) Readable(float r, float g, float b, float br, float bg, float bb, double min = Min)
        {
            double lbg = Luminance(br, bg, bb), l = Luminance(r, g, b);
            if (l < DarkLabel || Contrast(l, lbg) >= min) return (r, g, b);
            for (int i = 1; i <= 20; i++)
            {
                float t = i * 0.05f;
                float nr = r + (1 - r) * t, ng = g + (1 - g) * t, nb = b + (1 - b) * t;
                if (Contrast(Luminance(nr, ng, nb), lbg) >= min) return (nr, ng, nb);
            }
            return (1, 1, 1);
        }
    }

    // One name per feature and one way to write each item (design rule 14). Client strings use these.
    public static class Words
    {
        public const string Replay = "Replay", Climb = "Climb", View = "View", EditTeam = "Edit Team", Next = "Next", Again = "Again", Retry = "Retry", Back = "Back";
        public const string Victory = "VICTORY", Defeat = "DEFEAT", Cleared = "CLEARED";
        public static string BackTo(string place) => "Back to " + place;
        public static string Unlock(string stage) => $"Clear stage {stage} to unlock";
        public static string Level(int n) => "Lv " + n;
        // Words that must not appear in client string literals any more (whole words, case as written): the
        // feature names above replace them.
        public static readonly string[] Banned = { "auto-replay", "Auto-replay", "Edit team", "Loop", "Cam", "Unlocks after clearing", "Next Stage" };
    }
}
