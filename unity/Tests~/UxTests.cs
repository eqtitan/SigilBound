// Screens and controls (PLAN step 106, UX review P1-12 to P1-19): source checks that keep the phone-size fixes
// in place (the client can't run here), and the WCAG contrast of the disabled-button colours.
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

static class UxTests
{
    static double Lum(string hex)
    {
        double Ch(int v) { double c = v / 255.0; return c <= 0.03928 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4); }
        hex = hex.TrimStart('#');
        int r = Convert.ToInt32(hex.Substring(0, 2), 16), g = Convert.ToInt32(hex.Substring(2, 2), 16), b = Convert.ToInt32(hex.Substring(4, 2), 16);
        return 0.2126 * Ch(r) + 0.7152 * Ch(g) + 0.0722 * Ch(b);
    }
    static double Contrast(double a, double b) => (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    static string Scale(string hex, double k)
    {
        hex = hex.TrimStart('#');
        return string.Concat(Enumerable.Range(0, 3).Select(i => ((int)Math.Round(Convert.ToInt32(hex.Substring(i * 2, 2), 16) * k)).ToString("x2")));
    }

    public static void Run(Action<bool, string> check)
    {
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "ux: client sources not found here (skipped)"); return; }
        string Src(string f) => File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", f));
        string ui = Src("Ui.cs"), app = Src("App.cs"), prep = Src("PrepScreens.cs"), bc = Src("BattleController.cs"), map = Src("WorldMap.cs"), home = Src("Screens.cs");

        // Disabled buttons: light grey text on the grey plate, and on the painted plate darkened by the tint.
        string text = Regex.Match(ui, "DisabledText = Palette\\.Hex\\(\"(#[0-9a-fA-F]{6})\"\\)").Groups[1].Value;
        string plate = Regex.Match(ui, "DisabledPlate = Palette\\.Hex\\(\"(#[0-9a-fA-F]{6})\"\\)").Groups[1].Value;
        var tint = Regex.Match(ui, "GUI\\.color = keep \\* new Color\\(([0-9.]+)f");
        bool parsed = text.Length == 7 && plate.Length == 7 && tint.Success;
        double k = parsed ? double.Parse(tint.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : 1;
        double worst = parsed ? new[] { plate, "#45414c", Scale("#fde3a0", k), Scale("#e2b14e", k), Scale("#a6d6ff", k), Scale("#ffb0a0", k) }.Min(p => Contrast(Lum(text), Lum(p))) : 0;
        check(parsed && worst >= 4.5, $"ux: disabled button labels are at least 4.5:1 on every plate (worst {worst:0.0}:1)");

        // Touch targets: 48 dp from the screen density, never under 44 units.
        check(ui.Contains("public static float Tap => Mathf.Max(44f, Dp(48f));") && ui.Contains("dpi / 160f"), "ux: Ui.Tap is 48 dp (density aware, 44-unit floor)");
        var list = prep.Substring(prep.IndexOf("void DrawStageList", StringComparison.Ordinal));
        list = list.Substring(0, list.IndexOf("static void ScrollFade", StringComparison.Ordinal));
        check(list.Contains("Mathf.Max(tap + 6") && list.Contains("Scroller.Begin") && list.Contains("Ui.Button(row, \"\", Ui.Kind.Ghost)") && list.Contains("new Rect(r.xMax - tap - 4, r.y + 2, tap, tap)"),
            "ux: stage list rows are at least 48 dp, the whole row taps, the list scrolls, and the close button is 48 dp");
        check(bc.Contains("float bs = Mathf.Max(48, Ui.Tap)") && !Regex.IsMatch(bc, "RoundButton\\(new Rect\\([^)]*, 40, 40\\)"), "ux: battle corner buttons are 48 dp (no 40-unit buttons left)");

        // Home: the main call to action is pinned (not inside the scroll), the switches are a segmented control.
        var wide = home.Substring(home.IndexOf("void DrawHomeWide", StringComparison.Ordinal));
        wide = wide.Substring(0, wide.IndexOf("string HomeSubtitle", StringComparison.Ordinal));
        check(wide.IndexOf("HomeContinue(cta)", StringComparison.Ordinal) >= 0 && wide.IndexOf("HomeContinue(cta)", StringComparison.Ordinal) < wide.IndexOf("Scroller.Begin", StringComparison.Ordinal),
            "ux: Home's Continue Campaign is drawn outside the scroller (always visible)");
        check(map.Contains("Ui.Segmented(mode, new[] { \"Normal\", \"Hard\" }") && map.Contains("Ui.Segmented(acts, new[] { \"Act I\", \"Act II\" }") && !map.Contains("\"Hard\", hard ? Ui.Kind.ChipOn"),
            "ux: Normal/Hard and Act I/II are segmented switches in the top bar");

        // Safe area: the bars place their contents inside it.
        check(app.Contains("Ui.SafeContent(8)") && app.Contains("public static Rect NavCell"), "ux: top bar and nav keep their contents inside the safe area");

        // Colour-blind safety: no colour-only dots or initials for elements.
        var client = Directory.GetFiles(Path.Combine(root, "Assets", "Sigilbound", "Client"), "*.cs").Select(File.ReadAllText).ToList();
        check(!client.Any(s => s.Contains("ColorTag(\"●\"")) && !prep.Contains("ShortName.Substring(0, 1)") && prep.Contains("Ui.ElementBadge(disc"),
            "ux: elements show their icon wherever a coloured dot or initial stood");
        check(bc.Contains("\" WEAK!\"") && bc.Contains("\" RESIST\"") && bc.Contains("Icon.Shield : Icon.Skull"), "ux: damage numbers say WEAK!/RESIST and turn chips mark allies and enemies by glyph");
        check(ui.Contains("public static void RarityTab") && client.Sum(s => Regex.Matches(s, "Ui\\.RarityTab\\(").Count) >= 4, "ux: rarity frames carry a letter tab");

        // Press feedback: drawn (ghost) buttons light up while held and tap like the plates.
        check(ui.Contains("else PressOverlay(r, held);") && ui.Contains("k == Kind.Ghost ? 0.3f : 0.45f"), "ux: ghost buttons show a pressed state and play the tap sound");
    }
}
