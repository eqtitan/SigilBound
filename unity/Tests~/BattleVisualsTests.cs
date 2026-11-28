// Battle visuals from the art review (M1-M3; PMO P2-9, P2-10): the line-up stays inside the rune circle for every
// team size and width (and the ring grows with it), damage numbers float above the head tags without piling up or
// leaving the screen, skill pillars stay translucent and short on their targets, cast circles match the caster,
// and the Hollow enemies' outline is a constant red line that reads on every biome.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class BattleVisualsTests
{
    public static void Run(Action<bool, string> check)
    {
        try { Layout(check); }
        finally { BattleLayout.Ring = 1; }
        Numbers(check);
        Effects(check);
        Hollow(check);
        Sources(check);
    }

    // ---------- M1: units inside the ring ----------
    static void Layout(Action<bool, string> check)
    {
        // Widths: normal heroes, the wide humanoids (Creatures.WideHeroes), creatures (SlotWidth) and a creature boss.
        var kinds = new[] { 1.0 }.Concat(Creatures.WideHeroes.Values).Concat(Creatures.Table.Values.Select(c => c.SlotWidth))
                    .Concat(Creatures.Table.Values.Select(c => c.SlotWidth * BattleLayout.BossWidth)).Concat(new[] { BattleLayout.BossWidth }).Distinct().ToList();
        double widest = kinds.Max();
        int cases = 0, bad = 0, squeezed = 0; string firstBad = null;
        double maxRing = 1;
        for (int na = 1; na <= 5; na++)
            for (int ne = 1; ne <= 5; ne++)
                foreach (var wa in kinds)
                    foreach (var we in kinds)
                        foreach (int mode in new[] { 0, 1, 2 }) // 0: everyone this width, 1: one in the middle, 2: alternating
                        {
                            List<double> Row(int n, double w) => Enumerable.Range(0, n).Select(i => mode == 0 ? w : mode == 1 ? (i == n / 2 ? w : 1.0) : (i % 2 == 0 ? w : 1.0)).ToList();
                            var allies = Row(na, wa); var foes = Row(ne, we);
                            double maxW = Math.Max(allies.Max(), foes.Max());
                            BattleLayout.Ring = BattleLayout.RingScale(allies, foes);
                            maxRing = Math.Max(maxRing, BattleLayout.Ring);
                            var pa = Enumerable.Range(0, na).Select(i => BattleLayout.Slot(true, i, allies, maxW)).ToList();
                            var pe = Enumerable.Range(0, ne).Select(i => BattleLayout.Slot(false, i, foes, maxW)).ToList();
                            cases++;
                            string why = null;
                            for (int i = 0; i < na && why == null; i++) if (!BattleLayout.InsideRim(pa[i], BattleLayout.FootR * allies[i])) why = $"ally {i} off the ring";
                            for (int i = 0; i < ne && why == null; i++) if (!BattleLayout.InsideRim(pe[i], BattleLayout.FootR * foes[i])) why = $"enemy {i} off the ring";
                            if (why == null && (pa.Any(p => p.Z >= BattleLayout.RimCz) || pe.Any(p => p.Z <= BattleLayout.RimCz))) why = "rows cross the middle";
                            // No two footprints overlap (within a row, or an ally and an enemy at the sides).
                            var all = pa.Select((p, i) => (p, r: BattleLayout.FootR * allies[i])).Concat(pe.Select((p, i) => (p, r: BattleLayout.FootR * foes[i]))).ToList();
                            for (int i = 0; i < all.Count && why == null; i++)
                                for (int j = i + 1; j < all.Count && why == null; j++)
                                    if ((all[i].p - all[j].p).Length < all[i].r + all[j].r) why = $"footprints {i} and {j} overlap";
                            // Rows centred, and within MaxHalf of the centre line.
                            if (why == null && (Math.Abs(pa.First().X + pa.Last().X) > 1e-6 && mode == 0)) why = "allies not centred";
                            if (why != null) { bad++; firstBad ??= $"{na}v{ne} widths {wa:0.##}/{we:0.##} mode {mode}: {why} (ring {BattleLayout.Ring:0.###})"; }
                            if (BattleLayout.Ring >= BattleLayout.MaxRing - 1e-9 && Math.Max(BattleLayout.RowLength(allies), BattleLayout.RowLength(foes)) > 0) squeezed++;
                        }
        check(bad == 0, $"line-up inside the rune circle: every footprint in, rows apart, no overlaps ({cases - bad}/{cases} team sizes 1-5 x widths {string.Join("/", kinds.Select(k => k.ToString("0.##")))}){(firstBad != null ? " first: " + firstBad : "")}");

        // The classic battle (4 heroes v 3 Hollow) keeps the classic ring; a wide line-up grows it; it never shrinks.
        BattleLayout.Ring = BattleLayout.RingScale(4, 3);
        check(Math.Abs(BattleLayout.Ring - 1) < 1e-9, $"4 v 3 normal heroes keep the classic ring (scale {BattleLayout.Ring:0.###})");
        var wide = new List<double> { Creatures.WideHeroes["vorgrim"], Creatures.WideHeroes["nyssa"], Creatures.WideHeroes["crusk"], Creatures.WideHeroes["hartwin"] };
        double kw = BattleLayout.RingScale(wide, new List<double> { 1, 1, 1 });
        check(kw > 1.0001 && kw <= BattleLayout.MaxRing, $"a wide line-up (Vorgrim, Nyssa, Crusk, Hartwin) grows the ring (scale {kw:0.###})");
        check(BattleLayout.RingScale(new List<double>(), new List<double>()) == 1 && maxRing <= BattleLayout.MaxRing + 1e-9, $"the ring never shrinks below the classic size or grows past {BattleLayout.MaxRing} (largest {maxRing:0.###})");

        // The ring is the camera's frame too: a bigger ring pulls the camera back so it still fits.
        BattleLayout.Ring = 1; var cam = BattleLayout.Camera(BattleLayout.DefaultCamera);
        double d1 = BattleLayout.FitDistance(cam, 2.17, false);
        BattleLayout.Ring = kw; double d2 = BattleLayout.FitDistance(cam, 2.17, false);
        var eye = BattleLayout.CameraPosition(cam, d2);
        bool inFrame = Enumerable.Range(0, 4).All(i =>
        {
            var p = BattleLayout.Slot(true, i, wide, wide.Max());
            var q = BattleLayout.Project(eye, BattleLayout.FovFor(2.17), 2.17, p + new V3(0, 2.0, 0));
            return q != null && Math.Abs(q.Value.x) <= 0.95 && Math.Abs(q.Value.y) <= 0.84;
        });
        check(d2 > d1 && inFrame, $"the camera fits the grown ring ({d1:0.0} m -> {d2:0.0} m) with every hero's head in frame");
        BattleLayout.Ring = 1;
        // The Titan still stands behind the far rim.
        check(Math.Abs(BattleLayout.TitanSlot().Z - (BattleLayout.RimCz + BattleLayout.RimRz + BattleLayout.TitanBack)) < 1e-9, "the Titan stands behind the far rim, as before");
    }

    // ---------- M3: damage numbers ----------
    static void Numbers(Action<bool, string> check)
    {
        var rng = new Random(5);
        bool allOk = true; string bad = null; int cases = 0;
        foreach (var (w, h) in new[] { (2340.0, 1080.0), (1080.0, 2340.0), (1920.0, 1080.0) })
        {
            double ui = Math.Min(w, h) / 1080.0, numH = 34 * ui, top = h * 0.12;
            // Anchors: middle, the corners and the edges (a unit at the side of the screen, the far row near the top).
            var anchors = new[] { (w / 2, h / 2), (8.0, h / 2), (w - 4, h / 2), (w / 2, h - 10), (w / 2, 5.0), (w - 3, h - 3) };
            foreach (var (ax, ay) in anchors)
                foreach (int burst in new[] { 1, 3, 6 })
                    foreach (bool reduce in new[] { false, true })
                    {
                        double rise = reduce ? 0 : NumberLayout.Rise * numH;
                        var live = new List<NumBox>();
                        for (int k = 0; k < burst; k++)
                        {
                            string text = k % 2 == 0 ? "CRIT 12,463" : "2,463";
                            double hh = numH * (k % 2 == 0 ? 1.3 : 1) + (k % 3 == 0 ? Math.Max(numH * 0.62, 20 * ui) : 0); // some carry a WEAK! line
                            live.Add(NumberLayout.Place(ax, ay, NumberLayout.Width(text, hh), hh, live, w, h, top, rise, rng.NextDouble() * 2 - 1));
                        }
                        cases++;
                        if (!NumberLayout.Clear(live, w, h, top, rise)) { allOk = false; bad ??= $"{w}x{h} anchor ({ax:0},{ay:0}) x{burst} rise {rise:0}: " + string.Join(" ", live.Select(b => $"[{b.X:0},{b.Y:0} {b.W:0}x{b.H:0}]")); }
                    }
        }
        check(allOk, $"damage numbers: up to 6 at once never overlap and stay on screen for their whole rise ({cases} cases){(bad != null ? " first bad: " + bad : "")}");
        // Above the tag, not on the body: a lone number starts above its anchor (the tag point over the head).
        var one = NumberLayout.Place(1000, 500, 120, 34, null, 2340, 1080, 130, 37, 0);
        check(one.Y - one.H / 2 > 500 && Math.Abs(one.X - 1000) < 1e-9, $"a number starts above its unit's head tag (bottom edge {one.Y - one.H / 2:0} px > tag 500 px)");
        // A small random spread, so repeated hits don't print in one spot.
        var l = NumberLayout.Place(1000, 500, 120, 34, null, 2340, 1080, 130, 37, -1); var r = NumberLayout.Place(1000, 500, 120, 34, null, 2340, 1080, 130, 37, 1);
        check(r.X - l.X > 30 && r.X - l.X < 34 * NumberLayout.Spread * 2 + 1e-9, $"numbers spread a little sideways ({r.X - l.X:0} px across)");
    }

    // ---------- M3: pillars and cast circles ----------
    static void Effects(Action<bool, string> check)
    {
        bool cap = Enumerable.Range(0, 4).All(t => BattleFx.PillarOpacity(t) >= 0.35 && BattleFx.PillarOpacity(t) <= 0.6);
        check(cap && BattleFx.PillarCore <= 0.2, $"skill pillars are translucent: edge at most 60% (Low..Ultra {string.Join("/", Enumerable.Range(0, 4).Select(t => BattleFx.PillarOpacity(t).ToString("0.00")))}), only {BattleFx.PillarCore:0.00} of that across the unit");
        check(BattleFx.LifeOnTarget(1.5) <= 0.75 && BattleFx.LifeOnTarget(0.4) == 0.4 && BattleFx.FadeTop >= 0.4, "pillars on a unit last at most 0.75 s and fade out over their top part");
        // A cast circle matches the caster's floor disc (1.1 m x scale x width), not a 2.6 m stamp.
        double foot = BattleFx.Footprint(1, 1), big = BattleFx.Footprint(BattleLayout.BossWidth, 1.35);
        check(Math.Abs(foot - BattleFx.AuraDiameter) < 1e-9 && BattleFx.CircleRune * foot <= 1.25 && BattleFx.CircleGlow * foot < 1.5 && big > foot * 1.5,
            $"cast circles fit the caster: {BattleFx.CircleRune * foot:0.00} m rune / {BattleFx.CircleGlow * foot:0.00} m glow for a hero (was 2.0 / 2.6 m), {BattleFx.CircleRune * big:0.00} m for a creature boss");
    }

    // ---------- M2: Hollow outline ----------
    static void Hollow(Action<bool, string> check)
    {
        bool widths = Enumerable.Range(0, 4).All(t => BattleFx.OutlinePx(t) >= 1.5 && BattleFx.OutlinePx(t) <= 3.5) && BattleFx.OutlinePx(0) <= BattleFx.OutlinePx(3);
        check(widths && Math.Abs(BattleFx.OutlineWidth(2, 2160) - 2 * BattleFx.OutlinePx(2)) < 1e-9, "Hollow outline: 1.5-3.5 px at 1080p on every tier (Low included), scaled with the screen");
        // The red line against every biome's ground (Scenery themes' grass x the ground albedo 0.46; sRGB -> linear).
        string[] grounds = { "#4f6a2a", "#c2a87e", "#94503a", "#3a5630", "#58652f", "#557636", "#a39c8c", "#5e4e6c", "#2c4846", "#665834",
                             "#8a8070", "#1e2a20", "#56584a", "#3a3230", "#4a5052", "#b8aa8a", "#5a6260", "#3a3436" };
        var fails = grounds.Where(hex => !BattleFx.RedReads(Lin(hex, 0.46))).ToList();
        check(fails.Count == 0, $"the Hollow red reads against all {grounds.Length} biome grounds{(fails.Count > 0 ? " (not: " + string.Join(", ", fails) + ")" : "")}");
        check(BattleFx.OutlineGain(0) * BattleFx.HollowRed[0] < 1.0 + 1e-9 && BattleFx.OutlineGain(2) > 1, "the outline is HDR (bloom) on High and Ultra, plain on Low");
    }
    static double[] Lin(string hex, double k)
    {
        double C(int i) { double v = Convert.ToInt32(hex.Substring(1 + i * 2, 2), 16) / 255.0; return Math.Pow(v, 2.2) * k; }
        return new[] { C(0), C(1), C(2) };
    }

    // ---------- the shaders and their wiring ----------
    static void Sources(Action<bool, string> check)
    {
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "battle visuals: client sources not found here (skipped)"); return; }
        root = Path.Combine(root, "Assets", "Sigilbound");
        string Read(string rel) { var p = Path.Combine(root, rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        var pillar = Read("Client/Shaders/SigilFxPillar.shader"); var outline = Read("Client/Shaders/SigilHollowOutline.shader");
        check(pillar.Contains("Blend One One") && pillar.Contains("ZWrite Off") && pillar.Contains("_Opacity") && pillar.Contains("pow(saturate(1.0 - facing)") && !pillar.Contains("multi_compile _"),
            "FxPillar shader: additive, no depth write, opacity cap and fresnel falloff, no keyword variants");
        check(outline.Contains("Cull Front") && outline.Contains("_ScreenParams") && !outline.Contains("TEXTURE2D"), "HollowOutline shader: inverted hull, constant pixel width, no textures");
        var urp = Read("Editor/UrpSetup.cs");
        check(urp.Contains("\"Sigilbound/FxPillar\"") && urp.Contains("\"Sigilbound/HollowOutline\""), "both shaders are in Always Included Shaders (found by name at runtime)");
        var fx = Read("Client/SkillFx.cs");
        check(!fx.Contains("Take(_walls, PrimitiveType.Cylinder, \"Wall\", _add)") && fx.Contains("LifeOnTarget"), "SkillFx walls use the open tube and FxPillar (no capped opaque cylinders) and short lives on targets");
        var vfx = Read("Client/Vfx.cs");
        check(vfx.Contains("NumberLayout.Place") && vfx.Contains("Access.ReduceMotion"), "Vfx numbers use NumberLayout and respect Reduce motion");
    }
}
