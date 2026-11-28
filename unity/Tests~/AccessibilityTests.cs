// Settings > Accessibility (PLAN step 115; review items P2-17, P3-4; UX review #12, #16; art review L2): the text
// size rules, that every text path in Ui honours the scale, that key screens' text fits at Extra large, the
// colour-blind palettes (CIEDE2000 after a Machado 2009 simulation), and that reduce motion / flash effects reach
// the shake, cinematics, crit flash, lightning and bright bursts. Source checks where the client can't run here.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilbound.Core;

static class AccessibilityTests
{
    // ---------- colour science ----------
    static readonly Dictionary<string, double[,]> Cvd = new Dictionary<string, double[,]>
    {
        ["normal"] = new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } },
        ["deutan"] = new[,] { { 0.367322, 0.860646, -0.227968 }, { 0.280085, 0.672501, 0.047413 }, { -0.011820, 0.042940, 0.968881 } },
        ["protan"] = new[,] { { 0.152286, 1.052583, -0.204868 }, { 0.114503, 0.786281, 0.099216 }, { -0.003882, -0.048116, 1.051998 } },
        ["tritan"] = new[,] { { 1.255528, -0.076749, -0.178779 }, { -0.078411, 0.930809, 0.147602 }, { 0.004733, 0.691367, 0.303900 } },
    };
    static double[] Rgb(string hex) { hex = hex.TrimStart('#'); return Enumerable.Range(0, 3).Select(i => Convert.ToInt32(hex.Substring(i * 2, 2), 16) / 255.0).ToArray(); }
    static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
    static double Unlin(double c) { c = Math.Max(0, Math.Min(1, c)); return c <= 0.0031308 ? c * 12.92 : 1.055 * Math.Pow(c, 1 / 2.4) - 0.055; }
    static double[] Sim(string hex, string type)
    {
        var l = Rgb(hex).Select(Lin).ToArray(); var m = Cvd[type];
        return Enumerable.Range(0, 3).Select(i => Unlin(m[i, 0] * l[0] + m[i, 1] * l[1] + m[i, 2] * l[2])).ToArray();
    }
    static double[] Lab(double[] rgb)
    {
        var l = rgb.Select(Lin).ToArray();
        double X = (0.4124 * l[0] + 0.3576 * l[1] + 0.1805 * l[2]) / 0.95047, Y = 0.2126 * l[0] + 0.7152 * l[1] + 0.0722 * l[2], Z = (0.0193 * l[0] + 0.1192 * l[1] + 0.9505 * l[2]) / 1.08883;
        double F(double t) => t > 0.008856 ? Math.Cbrt(t) : 7.787 * t + 16.0 / 116;
        return new[] { 116 * F(Y) - 16, 500 * (F(X) - F(Y)), 200 * (F(Y) - F(Z)) };
    }
    static double Rad(double d) => d * Math.PI / 180;
    static double De2000(double[] a, double[] b)
    {
        double L1 = a[0], a1 = a[1], b1 = a[2], L2 = b[0], a2 = b[1], b2 = b[2];
        double C1 = Math.Sqrt(a1 * a1 + b1 * b1), C2 = Math.Sqrt(a2 * a2 + b2 * b2), Cb = (C1 + C2) / 2;
        double G = 0.5 * (1 - Math.Sqrt(Math.Pow(Cb, 7) / (Math.Pow(Cb, 7) + Math.Pow(25, 7))));
        double a1p = (1 + G) * a1, a2p = (1 + G) * a2, C1p = Math.Sqrt(a1p * a1p + b1 * b1), C2p = Math.Sqrt(a2p * a2p + b2 * b2);
        double h1 = (Math.Atan2(b1, a1p) * 180 / Math.PI + 360) % 360, h2 = (Math.Atan2(b2, a2p) * 180 / Math.PI + 360) % 360;
        double dL = L2 - L1, dC = C2p - C1p, dh = h2 - h1;
        if (C1p * C2p == 0) dh = 0; else if (dh > 180) dh -= 360; else if (dh < -180) dh += 360;
        double dH = 2 * Math.Sqrt(C1p * C2p) * Math.Sin(Rad(dh / 2));
        double Lb = (L1 + L2) / 2, Cbp = (C1p + C2p) / 2, hs = h1 + h2;
        double hb = C1p * C2p == 0 ? hs : Math.Abs(h1 - h2) <= 180 ? hs / 2 : hs < 360 ? (hs + 360) / 2 : (hs - 360) / 2;
        double T = 1 - 0.17 * Math.Cos(Rad(hb - 30)) + 0.24 * Math.Cos(Rad(2 * hb)) + 0.32 * Math.Cos(Rad(3 * hb + 6)) - 0.20 * Math.Cos(Rad(4 * hb - 63));
        double dth = 30 * Math.Exp(-Math.Pow((hb - 275) / 25, 2)), Rc = 2 * Math.Sqrt(Math.Pow(Cbp, 7) / (Math.Pow(Cbp, 7) + Math.Pow(25, 7)));
        double SL = 1 + 0.015 * (Lb - 50) * (Lb - 50) / Math.Sqrt(20 + (Lb - 50) * (Lb - 50)), SC = 1 + 0.045 * Cbp, SH = 1 + 0.015 * Cbp * T, RT = -Math.Sin(Rad(2 * dth)) * Rc;
        return Math.Sqrt(Math.Pow(dL / SL, 2) + Math.Pow(dC / SC, 2) + Math.Pow(dH / SH, 2) + RT * (dC / SC) * (dH / SH));
    }
    static double Worst(string[] cols, params string[] types) =>
        types.Min(t => Enumerable.Range(0, cols.Length).SelectMany(i => Enumerable.Range(i + 1, cols.Length - i - 1).Select(j => De2000(Lab(Sim(cols[i], t)), Lab(Sim(cols[j], t))))).Min());
    static double Lum(string hex) { var c = Rgb(hex).Select(Lin).ToArray(); return 0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]; }
    static double Contrast(string a, string b) { double x = Lum(a), y = Lum(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }

    public static void Run(Action<bool, string> check)
    {
        // ---------- text size rules ----------
        check(Accessibility.TextSizeNames.SequenceEqual(new[] { "Small", "Default", "Large", "Extra large" }) && Accessibility.TextFactors[Accessibility.DefaultTextSize] == 1f
              && Accessibility.TextFactors.Zip(Accessibility.TextFactors.Skip(1), (a, b) => b > a).All(x => x) && Accessibility.TextFactors[3] >= 1.25f,
            "a11y: four text sizes, Default is 1x, each larger than the last, Extra large at least 1.25x");
        bool floorOk = true, monoOk = true, fitOk = true, defaultSame = true;
        for (int size = 6; size <= 40; size++)
        {
            for (int lv = 0; lv < 4; lv++)
            {
                int px = Accessibility.TextSize(size, lv), fl = Accessibility.FitFloor(size, lv);
                floorOk &= px >= Accessibility.MinReadable && fl >= Accessibility.MinReadable;
                if (lv > 0) monoOk &= px >= Accessibility.TextSize(size, lv - 1);
                // shrink-to-fit: never below the Default size, never above the setting's size, the largest that fits
                int k = (size + px) / 2;
                int fit = Accessibility.Fit(size, lv, p => p <= k);
                fitOk &= fit >= fl && fit <= px && (fit == fl || fit == Math.Min(px, k)) && Accessibility.Fit(size, lv, p => false) == fl;
                if (lv >= 1) fitOk &= fl == Accessibility.TextSize(size, 1);
            }
            defaultSame &= Accessibility.TextSize(size, 1) == Math.Max(Accessibility.MinReadable, size);
        }
        check(floorOk && Accessibility.MinReadable >= 10, $"a11y: no text is drawn under {Accessibility.MinReadable} at any setting (minimum readable size)");
        check(monoOk && defaultSame, "a11y: text grows with each setting; Default draws every size as written (only the floor lifts tiny text)");
        check(fitOk, "a11y: shrink-to-fit picks the largest size that fits, never below the Default size or above the setting");

        // ---------- client sources ----------
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "a11y: client sources not found here (skipped)"); return; }
        string dir = Path.Combine(root, "Assets", "Sigilbound", "Client");
        string Src(string f) => File.ReadAllText(Path.Combine(dir, f));
        string Between(string src, string from, string to) { int a = src.IndexOf(from, StringComparison.Ordinal); if (a < 0) return ""; int b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal); return b < 0 ? src.Substring(a) : src.Substring(a, b - a); }
        string ui = Src("Ui.cs"), settings = Src("SettingsScreen.cs"), access = Src("Access.cs"), pal = Src("Palette.cs"), arena = Src("Arena.cs"), react = Src("ArenaReact.cs"), fx = Src("SkillFx.cs"), app = Src("App.cs"), roster = Src("RosterUi.cs"), friends = Src("FriendsScreens.cs");

        // Every Ui.Text call path honours the scale: each public text helper goes through Sized / TextPx / ButtonPx,
        // and the only places a font size is set are the scaled RawLabel, the button style (given the fitted px) and Field.
        var paths = new (string name, string body, string must)[]
        {
            ("Label", Between(ui, "public static GUIStyle Label(", ";"), "RawLabel(Sized(size)"),
            ("Text", Between(ui, "public static void Text(", "\n        }"), "RawLabel(TextPx(r, text, size"),
            ("TextOnArt", Between(ui, "public static void TextOnArt(", "\n        }"), "TextPx(r, text, size"),
            ("PlainText", Between(ui, "public static void PlainText(", "\n        }"), "TextPx(r, text, size"),
            ("Button", Between(ui, "public static bool Button(", "\n        }"), "int px = ButtonPx(r, text, size, serif && k != Kind.Ghost, k)"),
            ("Field", Between(ui, "public static string Field(", "\n        }"), "fontSize = Sized(13)"),
            ("FitSize", Between(ui, "public static int FitSize(", "\n        }"), "Label(sz"),
            ("Heading", Between(ui, "public static void Heading(", ";"), "Text(r, text, size"),
            ("Banner", Between(ui, "public static void Banner(", "\n        }"), "Text(face, text, size"),
        };
        foreach (var p in paths) check(p.body.Contains(p.must), $"a11y: Ui.{p.name} draws at the player's text size ({p.must})");
        var btn = Between(ui, "public static bool Button(", "\n        }");
        check(Regex.Matches(btn, @"ButtonStyle\(k, (\w+)").Cast<Match>().All(m => m.Groups[1].Value == "px") && Regex.Matches(btn, @"ButtonStyle\(").Count == 2,
            "a11y: both button styles (enabled and disabled) take the fitted size");
        var sizeSets = Regex.Matches(ui, @"fontSize = ([^,;]+)").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).ToList();
        check(sizeSets.All(v => v == "px" || v == "size" || v == "Sized(13)") && ui.Contains("static GUIStyle RawLabel(int px") && Regex.Matches(ui, @"RawLabel\(Sized\(|TextPx\(|ButtonPx\(").Count >= 6,
            $"a11y: Ui sets a font size only from a scaled value ({string.Join(", ", sizeSets.Distinct())})");
        check(ui.Contains("Accessibility.TextSize(size, Access.TextLevel)") && ui.Contains("Accessibility.FitFloor(size, level)") && ui.Contains("st.CalcHeight(_gc, r.width) <= r.height + 2") && ui.Contains("st.CalcSize(_gc).x <= r.width + 1"),
            "a11y: TextPx measures width (one line) or height (wrapped) and stops at the Default size");
        // No screen draws text around Ui: GUI.Label / fontSize / text GUI.Button only in Ui and the listed exceptions.
        var allow = new HashSet<string> { "Ui.cs", "Updater.cs", "PerfOverlay.cs", "Vfx.cs" };
        var stray = new List<string>();
        foreach (var f in Directory.GetFiles(dir, "*.cs"))
        {
            string n = Path.GetFileName(f); if (allow.Contains(n)) continue;
            string s = File.ReadAllText(f);
            foreach (Match m in Regex.Matches(s, @"GUI\.Label\([^\n]*")) if (!(n == "FriendsScreens.cs" && m.Value.Contains("Plain(14"))) stray.Add(n + ": " + m.Value.Substring(0, Math.Min(60, m.Value.Length)));
            foreach (Match m in Regex.Matches(s, @"fontSize = [^,;\n]*")) if (!m.Value.Contains("Ui.Sized(")) stray.Add(n + ": " + m.Value);
            foreach (Match m in Regex.Matches(s, @"GUI\.Button\([^\n]*")) stray.Add(n + ": " + m.Value.Substring(0, Math.Min(60, m.Value.Length)));
        }
        check(stray.Count == 0, "a11y: every screen draws text through Ui (scaled): " + (stray.Count == 0 ? "no strays" : string.Join(" | ", stray.Take(4))));
        check(friends.Contains("Ui.Label(size, c, fs, a, wrap)") && friends.Contains("fontSize = Ui.Sized(size)") && Regex.Matches(friends, @"Ui\.PlainText\(").Count >= 8,
            "a11y: friends and chat (player-typed text, no rich text) use the scaled styles");
        check(Regex.IsMatch(access, @"TextLevel\s*\{[^}]*\}[^}]*Ui\.ClearStyles\(\)") || access.Contains("Saved(); Ui.ClearStyles(); }"), "a11y: changing the text size rebuilds the cached styles");

        // Key screens at Extra large: each label (size and rect as in the source) either grows and fits, or shrinks
        // back no lower than Default, where it fits. ApproxWidth is a font-free estimate (Cinzel bold wider).
        string longestAlly = GameData.Heroes.Select(h => h.ShortName).OrderByDescending(s => s.Length).First();
        var screens = new (string where, string text, double w, double h, int size, bool serif)[]
        {
            ("top bar gold chip", "99.9K", 37, 28, 12, false),
            ("top bar shard chip", "9999", 37, 28, 12, false),
            ("nav label", "Friends", 390 / 6.0, 22, 12, true),
            ("nav label", "Summon", 390 / 6.0, 22, 12, true),
            ("battle HUD name tag", longestAlly, 126, 16, 11, false),
            ("battle HUD level badge", "60", 22, 16, 10, false),
            ("battle banner", "Wave 3!", 390, 30, 19, true),
            ("results hero name", longestAlly, 120, 22, 15, false),
            ("results level line", "Level up!  Lv 60", 120, 16, 12, false),
            ("stage row label", "Boss 10-6", 150, 22, 13, false),
            ("stage row stop name", "The Ember Throne", 170, 20, 12, false),
            ("hero list name", longestAlly + "  #4", 250, 22, 14, false),
            ("hero list line", "★★★★★★ Lv60 · Shadow", 250, 20, 12, false),
            ("release button", "Hold to release 12", 358 - 114 - 8, 44, 15, true), // ReleasePanel rr: RW - 0.32 RW - 8
            ("rank up button", "Hold: Rank up 4", 358, 48, 16, true),              // RankUpPanel br: RW wide
            ("climb summary count", "42 stages", 300, 34, 24, true),
            ("climb summary line", "12 fights in all", 300, 16, 11, false),
            ("climb summary button", "Keep climbing", 175, 44, 15, true),
            ("settings chip", "Extra large", Accessibility.ApproxWidth("Extra large", Accessibility.TextSize(13, 3), true) + 2 * (6 + Math.Min(18, 48 * 0.3)) + 4, 48, 13, true), // ChipW
        };
        int grew = 0; var over = new List<string>();
        foreach (var s in screens)
        {
            double pad = s.where.Contains("button") || s.where.StartsWith("settings") ? 2 * (6 + Math.Min(18, s.h * 0.3)) : 0; // Ui.ButtonInset
            bool Fits(int px) => Accessibility.ApproxWidth(s.text, px, s.serif) <= s.w - pad + 1 && px * 1.15 <= s.h + 3;
            int fit = Accessibility.Fit(s.size, 3, Fits), floor = Accessibility.FitFloor(s.size, 3);
            if (fit > floor) grew++;
            if (!Fits(fit)) over.Add($"{s.where} \"{s.text}\" at {fit}");
        }
        check(over.Count == 0, $"a11y: key screens fit at Extra large (top bar, nav, HUD tags, results, stage rows, hero list, Release/Rank Up, Climb summary; {grew}/{screens.Length} labels larger): " + (over.Count == 0 ? "none overflow" : string.Join(", ", over)));
        check(grew >= screens.Length / 2, "a11y: Extra large actually enlarges most key labels (not everything shrinks back)");
        // Layouts that can grow do: Settings rows scale with the text, chips are measured, notes wrap.
        check(settings.Contains("float g = Ui.TextGrow, th = 22 * g;") && settings.Contains("CalcHeight(_subGc, b.width)") && settings.Contains("float w = ChipW(labels[i], b);") && !settings.Contains("labels[i].Length * 8.2f"),
            "a11y: Settings rows grow with the text size and chips are measured at it");

        // ---------- Settings > Accessibility group ----------
        var group = Between(settings, "void DrawAccessibility(", "void PalettePreview(");
        int at = settings.IndexOf("DrawAccessibility(ref y, b);", StringComparison.Ordinal), vol = settings.IndexOf("Slider(ref y, b, \"Music\"", StringComparison.Ordinal);
        check(at > 0 && at < vol && new[] { "\"Text size\"", "\"Reduce motion\"", "\"Screen shake\"", "\"Flash effects\"", "\"Colour-blind palette\"", "\"Hold to confirm\"", "\"Larger tap targets\"", "PalettePreview(ref y, b)" }.All(group.Contains),
            "a11y: Settings opens with an Accessibility group: text size, reduce motion, screen shake, flashes, colour-blind palette (with preview chips), hold time, tap size");
        check(Regex.Matches(settings, "\"Screen shake\"").Count == 1, "a11y: Screen shake moved into the group (one row, not two)");
        check(access.Contains("PlayerPrefs") && !Regex.IsMatch(access, @"G\.S\.|SaveData") && new[] { "sb.textSize", "sb.reduceMotion", "sb.flash", "sb.palette", "sb.hold", "sb.bigTaps" }.All(access.Contains)
              && !File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Core", "SaveData.cs")).Contains("textSize"),
            "a11y: the choices are device preferences (PlayerPrefs), nothing added to the save (old saves load unchanged)");

        // ---------- reduce motion and flashes ----------
        check(Accessibility.EffectiveShake("full", true) == "off" && Accessibility.EffectiveShake("reduced", false) == "reduced" && Accessibility.EffectiveShake("junk", false) == "full"
              && app.Contains("Vfx.ShakeSetting = () => Access.Shake;") && access.Contains("Accessibility.EffectiveShake(Quality.SavedShake, ReduceMotion)"),
            "a11y: Reduce motion turns the screen shake off (the Screen shake setting folded in), and the Titan's breathing tilt with it");
        var comp = Between(arena, "void ComposeCamera()", "\n        }");
        var held = Between(arena, "void ReducedOverride(", "\n        }");
        check(comp.Contains("if (Access.ReduceMotion) ReducedOverride(ref pos, ref rot, ref fov);") && held.Contains("_ovW >= 0.999f") && held.Contains("Quaternion.LookRotation(f, Vector3.up)") && held.Contains("CutDip = 1f") && arena.Contains("GUI.depth = 10"),
            "a11y: with Reduce motion a cinematic holds one level still shot (no swing, zoom or roll) with a soft dip at each cut");
        check(Accessibility.FlashScale(0) == 1 && Accessibility.FlashScale(1) < 0.5f && Accessibility.FlashScale(2) == 0 && Accessibility.BurstCap(2) < Accessibility.BurstCap(1) && Accessibility.BurstCap(1) <= 1f
              && Accessibility.BoltFlicks(0) == 3 && Accessibility.BoltFlicks(1) == 1 && Accessibility.BoltFlicks(2) == 1,
            "a11y: flash levels: On as designed, Reduced dimmer with no flicker, Off no flashes; bright bursts capped");
        var crit = Between(fx, "public void CritFlash(", "\n        }");
        check(crit.Contains("if (fs <= 0) return;") && crit.Contains("0.25f * fs") && fx.Contains("Mathf.Min(k, Access.BurstCap)") && fx.Contains("Accessibility.BoltFlicks(Access.Flash)") && fx.Contains("0.8f * fl"),
            "a11y: crit/ult/break full-screen flash, additive bursts, crit flare and skill lightning follow Flash effects");
        check(react.Contains("_flash * Access.FlashScale") && react.Contains("Access.Flash < 2") && !react.Contains("_flash * Arena.MotionScale"),
            "a11y: sky lightning flashes follow Flash effects (no strobe when Reduced, none when Off)");

        // ---------- colour-blind palettes ----------
        string[] oldEl = { "#e8553a", "#3fb57a", "#3a8ee8", "#e8c23a", "#9a5ae8" }, oldRar = { "#9aa3ad", "#4aa3ff", "#b46cff", "#ffb02e" };
        check(Accessibility.ElementHex[0].SequenceEqual(oldEl) && Accessibility.RarityHex[0].SequenceEqual(oldRar) && Accessibility.GoodBadHex[0].SequenceEqual(new[] { "#5bd17a", "#ff5a5a" })
              && Accessibility.GearHex[0].SequenceEqual(new[] { "#9aa3ad", "#5bc46a", "#4aa3ff", "#b46cff", "#ffb02e" }),
            "a11y: the Default palette is the game's own colours, unchanged");
        check(pal.Contains("Accessibility.ElementColour(Access.ColourMode, e)") && pal.Contains("Accessibility.RarityColour(Access.ColourMode, r)") && pal.Contains("Accessibility.GearColour(Access.ColourMode, r)") && pal.Contains("GoodBadHex[Access.ColourMode]"),
            "a11y: Palette's element, rarity, gear and good/bad colours follow the chosen palette");
        double dDef = Worst(oldEl, "deutan", "protan"), dDp = Worst(Accessibility.ElementHex[1], "deutan", "protan"), nDp = Worst(Accessibility.ElementHex[1], "normal");
        double tDef = Worst(oldEl, "tritan"), tTr = Worst(Accessibility.ElementHex[2], "tritan"), nTr = Worst(Accessibility.ElementHex[2], "normal");
        check(dDp >= 20 && nDp >= 20 && dDp > dDef * 2, $"a11y: red-green palette: elements at least 20 apart (CIEDE2000) under deuteranopia and protanopia (worst {dDp:0.0}; default {dDef:0.0}) and to everyone ({nDp:0.0})");
        check(tTr >= 20 && nTr >= 20 && tTr > tDef, $"a11y: blue-yellow palette: elements at least 20 apart under tritanopia (worst {tTr:0.0}; default {tDef:0.0}) and to everyone ({nTr:0.0})");
        double rDp = Worst(Accessibility.RarityHex[1], "deutan", "protan"), rTr = Worst(Accessibility.RarityHex[2], "tritan"), rDef = Worst(oldRar, "deutan", "protan");
        check(rDp >= 10 && rTr >= 15 && rDp > rDef * 2, $"a11y: rarity colours tell apart under each type (red-green worst {rDp:0.0}, default {rDef:0.0}; blue-yellow {rTr:0.0}); rarity letters stay");
        double gDp = Worst(Accessibility.GearHex[1], "deutan", "protan"), gTr = Worst(Accessibility.GearHex[2], "tritan");
        double bDp = Worst(Accessibility.GoodBadHex[1], "deutan", "protan");
        check(gDp >= 10 && gTr >= 10 && bDp >= 25, $"a11y: gear tiers ({gDp:0.0} / {gTr:0.0}) and ally/enemy blue vs orange ({bDp:0.0}) tell apart");
        double low = new[] { 1, 2 }.SelectMany(m => Accessibility.ElementHex[m].Concat(Accessibility.RarityHex[m])).Min(c => Contrast(c, "#1b1624"));
        check(low >= 3.0, $"a11y: every palette colour reads on the dark panel (lowest {low:0.0}:1)");
        check(Regex.Matches(group, @"Ui\.ElementBadge|Ui\.RarityLetter").Count == 0 && Between(settings, "void PalettePreview(", "\n        }").Contains("Ui.ElementBadge(") && Between(settings, "void PalettePreview(", "\n        }").Contains("Ui.RarityLetter("),
            "a11y: palette preview chips keep the element icons and rarity letters (step 106 cues)");

        // ---------- hold time and tap targets ----------
        check(Accessibility.Hold(1) == 1f && Accessibility.Hold(0) < 1f && Accessibility.Hold(2) > 1f && roster.Contains("public static float HoldSeconds => Access.HoldSeconds;"),
            "a11y: hold-to-confirm takes 1 s by default, shorter or longer by choice");
        check(Accessibility.TapDp(false) == 48f && Accessibility.TapDp(true) == 56f && ui.Contains("Dp(Accessibility.TapDp(Access.LargeTaps))"), "a11y: larger tap targets make Ui.Tap 56 dp (48 by default)");
    }
}
