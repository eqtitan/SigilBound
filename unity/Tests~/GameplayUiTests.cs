// Gameplay/UI gaps (in-house report S4, UX review #5/#9, art review M7/M9, PLAN P2-14/P2-15): portrait Home keeps
// the team visible, loading heroes get a placeholder, enemy intent is wired into both battle HUDs, and the UI
// chrome is neutral slate (violet means Shadow only). Source checks (the client can't run here), the Home layout
// maths on many phone sizes, and the shipped skin plates decoded and measured.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilbound.Core;

static class GameplayUiTests
{
    // Hue in degrees and HSL saturation of "#rrggbb".
    static (double hue, double sat, double light) Hsl(string hex)
    {
        hex = hex.TrimStart('#');
        double r = Convert.ToInt32(hex.Substring(0, 2), 16) / 255.0, g = Convert.ToInt32(hex.Substring(2, 2), 16) / 255.0, b = Convert.ToInt32(hex.Substring(4, 2), 16) / 255.0;
        return Hsl(r, g, b);
    }
    static (double hue, double sat, double light) Hsl(double r, double g, double b)
    {
        double mx = Math.Max(r, Math.Max(g, b)), mn = Math.Min(r, Math.Min(g, b)), l = (mx + mn) / 2, d = mx - mn;
        if (d < 1e-9) return (0, 0, l);
        double s = l > 0.5 ? d / (2 - mx - mn) : d / (mx + mn);
        double h = mx == r ? (g - b) / d + (g < b ? 6 : 0) : mx == g ? (b - r) / d + 2 : (r - g) / d + 4;
        return (h * 60, s, l);
    }
    static bool Violet(double hue, double sat) => hue >= 255 && hue <= 330 && sat > 0.15;

    // Mean RGB of the middle third of an 8-bit RGB/RGBA PNG (enough of a decoder for the skin plates).
    static (double r, double g, double b)? PngCentre(string path)
    {
        var data = File.ReadAllBytes(path);
        int pos = 8, w = 0, h = 0, ct = 0, depth = 0; var idat = new MemoryStream();
        while (pos + 8 <= data.Length)
        {
            int len = (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
            string type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
            if (type == "IHDR") { w = (data[pos + 8] << 24) | (data[pos + 9] << 16) | (data[pos + 10] << 8) | data[pos + 11]; h = (data[pos + 12] << 24) | (data[pos + 13] << 16) | (data[pos + 14] << 8) | data[pos + 15]; depth = data[pos + 16]; ct = data[pos + 17]; }
            else if (type == "IDAT") idat.Write(data, pos + 8, len);
            else if (type == "IEND") break;
            pos += 12 + len;
        }
        if (depth != 8 || (ct != 6 && ct != 2)) return null;
        int bpp = ct == 6 ? 4 : 3, stride = w * bpp;
        idat.Position = 0; var raw = new MemoryStream();
        using (var z = new ZLibStream(idat, CompressionMode.Decompress)) z.CopyTo(raw);
        var src = raw.ToArray(); var img = new byte[h * stride]; var prev = new byte[stride];
        for (int y = 0; y < h; y++)
        {
            int f = src[y * (stride + 1)]; var line = new byte[stride];
            for (int x = 0; x < stride; x++)
            {
                int a = x >= bpp ? line[x - bpp] : 0, b = prev[x], c = x >= bpp ? prev[x - bpp] : 0, v = src[y * (stride + 1) + 1 + x];
                int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                line[x] = (byte)(f switch { 1 => v + a, 2 => v + b, 3 => v + (a + b) / 2, 4 => v + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c), _ => v });
            }
            Array.Copy(line, 0, img, y * stride, stride); prev = line;
        }
        double sr = 0, sg = 0, sb = 0; int n = 0;
        for (int y = h / 3; y < 2 * h / 3; y++)
            for (int x = w / 3; x < 2 * w / 3; x++) { int i = y * stride + x * bpp; if (bpp == 4 && img[i + 3] < 128) continue; sr += img[i]; sg += img[i + 1]; sb += img[i + 2]; n++; }
        return n == 0 ? ((double, double, double)?)null : (sr / n / 255, sg / n / 255, sb / n / 255);
    }

    public static void Run(Action<bool, string> check)
    {
        // ---- portrait Home layout: the team's band stays clear on every phone ----
        var (bt, bb) = HomeLayout.TeamBand();
        check(bt > 0.3 && bt < 0.45 && bb > 0.55 && bb < 0.68, $"home: the menu camera puts the team at {bt:0.00}-{bb:0.00} of the screen height");
        bool clear = true, pinned = true, roomy = true; string worst = "";
        foreach (var (w, h) in new[] { (360.0, 640.0), (360.0, 780.0), (390.0, 844.0), (412.0, 915.0), (430.0, 932.0), (360.0, 800.0), (480.0, 1066.0), (600.0, 960.0) })
            foreach (double tap in new[] { 44.0, 48.0, 56.0 })
            {
                double top = 56, nav = 64; // the app's top bar and bottom nav
                var area = new URect(0, top, w, h - top - nav);
                var l = HomeLayout.Portrait(area, h, tap);
                var band = new URect(0, bt * h, w, (bb - bt) * h);
                if (l.strip.Overlaps(band) || (l.sheet.H > 0 && l.sheet.Overlaps(band)) || l.cta.Overlaps(band)) { clear = false; worst = $"{w}x{h}"; }
                if (Math.Abs(l.cta.Bottom - (area.Bottom - 8)) > 0.01 || l.cta.H < Math.Max(56, tap + 8) - 0.01 || l.sheet.Bottom > l.cta.Y) pinned = false;
                if (l.strip.H < Math.Max(48, tap) - 0.01 || l.strip.Y < area.Y) roomy = false;
            }
        check(clear, "home: portrait tab strip, card sheet and Continue button never cover the team (8 phone sizes x 3 tap sizes)" + (clear ? "" : $" (overlap at {worst})"));
        check(pinned && roomy, "home: Continue Campaign stays pinned above the nav at full height; tabs are at least one tap tall");

        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "gameplay ui: client sources not found here (skipped)"); return; }
        string Src(string f) => File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", f));
        string screens = Src("Screens.cs"), arena = Src("Arena.cs"), cv = Src("CharacterView.cs"), ph = Src("LoadingPlaceholder.cs"), bc = Src("BattleController.cs"), intent = Src("EnemyIntentUi.cs"), pal = Src("Palette.cs"), app = Src("App.cs");

        // The layout's camera is the real menu camera, and the lineup rows are where App puts them.
        string I(double v) => v.ToString(CultureInfo.InvariantCulture);
        bool cam = arena.Contains($"SetPose(new Vector3({I(HomeLayout.MenuEye[0])}, {I(HomeLayout.MenuEye[1])}f, {I(HomeLayout.MenuEye[2])}f), new Vector3({I(HomeLayout.MenuTarget[0])}, {I(HomeLayout.MenuTarget[1])}f, {I(HomeLayout.MenuTarget[2])}f), {I(HomeLayout.MenuFov)})")
            && app.Contains($"0, {I(HomeLayout.LineupNearZ)}f + (i % 2) * 0.5f");
        check(cam, "home: HomeLayout's camera and lineup rows match Arena.FrameMenu and App.UpdateLineup");
        var home = screens.Substring(screens.IndexOf("void DrawHome(Rect area)"), screens.IndexOf("void DrawHomeWide(Rect area)") - screens.IndexOf("void DrawHome(Rect area)"));
        check(home.Contains("HomeLayout.Portrait(") && home.Contains("HomeTabs(strip)") && home.Contains("HomeContinue(cta)") && !home.Contains("y += 170"),
            "home: portrait Home lays out with HomeLayout (tab strip, sheet under the team, pinned Continue), not the old card column");

        // Placeholders until the model is revealed, on both Home layouts.
        check(Regex.Matches(screens, @"LoadingPlaceholder\.DrawLineup\(").Count >= 2 && cv.Contains("AwaitingModel = true") && cv.Contains("finished = true; AwaitingModel = false")
            && cv.Contains("if (on) AwaitingModel = false") && ph.Contains("v.AwaitingModel") && ph.Contains("Portraits.Get(heroId)") && ph.Contains("Silhouette") && ph.Contains("Time.unscaledTime / SweepSeconds"),
            "home: a loading hero shows its portrait card (or a silhouette) with a light sweep until HeroModels reveals it or the stand-in shows");

        // Enemy intent in both HUDs, from Core's preview, cached, with details on tap.
        check(Regex.Matches(bc, @"IntentChip\(u, ").Count == 2 && bc.Contains("_intents.Of(_b, u)") && bc.Contains("u == _actor") && intent.Contains("b.PreviewAction(u)")
            && intent.Contains("if (s != _stamp)") && intent.Contains("SkillText.Describe(sk)") && intent.Contains("Ui.Tap"),
            "intent: both battle HUDs draw each foe's intent from Battle.PreviewAction (cached per battle change), tap for the skill text, 48 dp hit box");
        check(Enum.GetValues(typeof(IntentKind)).Cast<IntentKind>().Where(k => k != IntentKind.None).All(k => Regex.IsMatch(intent, $@"IntentKind\.{k} => Icon\.")),
            "intent: every intent kind has its own icon (shape, not only colour)");
        check(!Regex.IsMatch(intent, @"ColourOf[\s\S]*?#(9a5ae8|b080ff|a050ff)"), "intent: no Shadow violet among the intent colours");

        // ---- palette: neutral chrome tokens, violet only for Shadow ----
        var chrome = Regex.Match(pal, @"ChromeHex = \{([^}]*)\}").Groups[1].Value.Split(',').Select(x => x.Trim().Trim('"')).Where(x => x.StartsWith("#")).ToList();
        var bad = chrome.Where(x => { var c = Hsl(x); return Violet(c.hue, c.sat) || c.sat > 0.25 || c.light > 0.3; }).ToList();
        check(chrome.Count >= 12 && bad.Count == 0, $"palette: {chrome.Count} UI chrome tokens are dark neutral slate (low saturation, no violet){(bad.Count > 0 ? ": " + string.Join(",", bad) : "")}");
        foreach (var (name, token) in new[] { ("Bg", "Bg"), ("Panel", "Panel"), ("Panel2", "Panel2") })
        {
            var m = Regex.Match(pal, token + @" = Hex\(""(#[0-9a-fA-F]{6})""\)");
            check(m.Success && chrome.Contains(m.Groups[1].Value), $"palette: {name} is one of the chrome tokens ({m.Groups[1].Value})");
        }
        var shadow = Hsl(Accessibility.ElementHex[0][(int)Element.Shadow]);
        check(Violet(shadow.hue, shadow.sat) && Accessibility.ElementHex.All(m => { var c = Hsl(m[(int)Element.Shadow]); return c.hue > 255 && c.hue < 335; }), $"palette: Shadow keeps its violet in every colour mode (hue {shadow.hue:0})");
        // No violet hex literal left in the menu chrome (3D model colours in HeroModels/HeroRig/Palette.Looks are art, not chrome).
        var leftovers = new List<string>();
        foreach (var f in Directory.GetFiles(Path.Combine(root, "Assets", "Sigilbound", "Client"), "*.cs"))
        {
            string n = Path.GetFileName(f); if (n == "Palette.cs" || n == "HeroModels.cs" || n == "HeroRig.cs") continue;
            foreach (Match m in Regex.Matches(File.ReadAllText(f), @"Palette\.Hex\(""(#[0-9a-fA-F]{6})""\)"))
            {
                var c = Hsl(m.Groups[1].Value);
                if (Violet(c.hue, c.sat) && c.light < 0.35) leftovers.Add(n + " " + m.Groups[1].Value);
            }
        }
        check(leftovers.Count == 0, $"palette: no dark violet chrome literals left in the client (they use the Palette tokens){(leftovers.Count > 0 ? ": " + string.Join(", ", leftovers.Take(6)) : "")}");
        // The painted plates (panels, cards, plain buttons, chips, banner) were recoloured off violet (Tools~/skin/neutral.py).
        string skin = Path.Combine(root, "Assets", "Resources", "Skin");
        var plates = new[] { "panel", "card", "card_highlight", "card_small", "banner", "button_secondary", "button_secondary_pressed", "button_chip", "button_chipon" };
        var violetPlates = new List<string>(); int measured = 0;
        foreach (var p in plates)
        {
            var f = Path.Combine(skin, p + ".png"); if (!File.Exists(f)) continue;
            var c = PngCentre(f); if (c == null) continue; measured++;
            var hs = Hsl(c.Value.r, c.Value.g, c.Value.b);
            if (Violet(hs.hue, hs.sat)) violetPlates.Add($"{p} (hue {hs.hue:0})");
        }
        check(measured >= 6 && violetPlates.Count == 0, $"palette: the painted chrome plates are neutral, not violet ({measured} measured){(violetPlates.Count > 0 ? ": " + string.Join(", ", violetPlates) : "")}");
        var gold = PngCentre(Path.Combine(skin, "button_primary.png"));
        check(gold != null && Hsl(gold.Value.r, gold.Value.g, gold.Value.b).hue is > 30 and < 60, "palette: the gold primary button keeps its gold");
    }
}
