// The UI/UX consistency review's 14 design rules (reviews/ux_consistency.md), as checks. The client can't run
// here, so most are source checks like UxTests; the layout rule runs the real rect arithmetic (Core/UiLayout.cs)
// at the three reference widths (640 = 4:3 tablet, 1067 = 16:9, 1422 = 21:9) and asserts nothing overlaps or is
// cut. Each check names its rule.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Sigilbound.Core;

static class UxRulesTests
{
    static string Between(string src, string from, string to)
    {
        int a = src.IndexOf(from, StringComparison.Ordinal); if (a < 0) return "";
        int b = src.IndexOf(to, a + from.Length, StringComparison.Ordinal); return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
    }
    // A method's body: from its signature to the next method at the same indent (8 spaces).
    static string Method(string src, string sig)
    {
        int a = src.IndexOf(sig, StringComparison.Ordinal); if (a < 0) return "";
        int b = src.IndexOf("\n        }", a, StringComparison.Ordinal);
        return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
    }
    // Code without // comments (so a comment quoting an old label doesn't count).
    static string Code(string src) => string.Join("\n", src.Split('\n').Select(l => { int i = l.IndexOf("//", StringComparison.Ordinal); return i >= 0 && !InString(l, i) ? l.Substring(0, i) : l; }));
    static bool InString(string line, int idx) { bool q = false; for (int i = 0; i < idx; i++) { if (line[i] == '\\') { i++; continue; } if (line[i] == '"') q = !q; } return q; }
    // The string literals in code, with C# escapes decoded (▶ counts as the glyph it draws).
    static IEnumerable<string> Literals(string code)
    {
        foreach (Match m in Regex.Matches(code, "\"((?:[^\"\\\\\\n]|\\\\.)*)\""))
        {
            string s = Regex.Replace(m.Groups[1].Value, @"\\u([0-9A-Fa-f]{4})", x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
            yield return s.Replace("\\n", "\n").Replace("\\\"", "\"");
        }
    }

    public static void Run(Action<bool, string> check)
    {
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "ux rules: client sources not found here (skipped)"); return; }
        string dir = Path.Combine(root, "Assets", "Sigilbound", "Client");
        string Src(string f) => File.ReadAllText(Path.Combine(dir, f));
        var files = Directory.GetFiles(dir, "*.cs").ToDictionary(Path.GetFileName, File.ReadAllText);
        var code = files.ToDictionary(kv => kv.Key, kv => Code(kv.Value));
        string all = string.Join("\n", code.Values);
        string ui = Src("Ui.cs"), app = Src("App.cs"), bc = Src("BattleController.cs"), climb = Src("AutoClimbUi.cs"), screens = Src("Screens.cs"), prep = Src("PrepScreens.cs"),
            trials = Src("TrialsScreens.cs"), extras = Src("ExtrasScreens.cs"), modes = Src("ModesScreen.cs"), map = Src("WorldMap.cs"), set = Src("SettingsScreen.cs"), upd = Src("Updater.cs");

        // ---------- rule 1: tap size ----------
        var button = Method(ui, "public static bool Button(Rect r, string text");
        check(ui.Contains("public static Rect HitRect(Rect r)") && button.Contains("var hitR = HitRect(r);") && button.Contains("GUI.Button(hitR,") && button.Contains("Held(hitR)"),
            "ux rule 1: every Ui.Button taps over at least Ui.Tap on each side (a smaller plate gets a larger hit rect)");
        var small = new List<string>();
        foreach (var kv in code)
            foreach (Match m in Regex.Matches(kv.Value, @"Ui\.Button\(new Rect\([^;]*?, (\d+(?:\.\d+)?)\),"))
                if (double.Parse(m.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) < 44) small.Add($"{kv.Key}: {m.Value.Substring(0, Math.Min(70, m.Value.Length))}");
        check(small.Count == 0, $"ux rule 1: no button is drawn under 44 units tall (literal heights; {small.Count} found{(small.Count > 0 ? ": " + small[0] : "")})");
        check(!Regex.IsMatch(all, @"RoundButton\(new Rect\([^)]*, (3\d|4[0-7]), (3\d|4[0-7])\)") && bc.Contains("float bs = Mathf.Max(48, Ui.Tap)"), "ux rule 1: battle corner buttons are 48 dp");

        // ---------- rule 2: one header per sub-screen, Back returns where you came from ----------
        var subScreens = new[] { ("Screens.cs", "void DrawHero(Rect area)"), ("Screens.cs", "void DrawEquip(Rect area)"), ("Screens.cs", "void DrawTitan(Rect area)"), ("Screens.cs", "void DrawPrep(Rect area)"),
            ("TrialsScreens.cs", "void DrawDungeon(Rect area)"), ("TrialsScreens.cs", "void DrawArenaPvp(Rect area)"), ("ExtrasScreens.cs", "void DrawAchievements(Rect area)"),
            ("FriendsScreens.cs", "void DrawBlocked(Rect area)"), ("RankUpPanel.cs", "void DrawRankUpPanel(Rect area)") };
        var noHeader = subScreens.Where(s => !Method(files[s.Item1], s.Item2).Contains("Ui.ScreenHeader(")).Select(s => s.Item2).ToList();
        check(noHeader.Count == 0, $"ux rule 2: every sub-screen draws Ui.ScreenHeader (Back + title){(noHeader.Count > 0 ? "; missing: " + string.Join(", ", noHeader) : "")}");
        check(prep.Contains("Ui.BackButton(Ui.ToRect(P.Back))") && Method(ui, "public static bool ScreenHeader(").Contains("BackButton(back)"),
            "ux rule 2: landscape team setup and the header share the one Back button");
        var arrowLabels = code.SelectMany(kv => Literals(kv.Value).Where(l => l.Contains('‹')).Select(l => kv.Key + ": " + l)).ToList();
        check(arrowLabels.Count == 0, $"ux rule 2: no \"‹ Back\" style labels left (one Back button){(arrowLabels.Count > 0 ? "; " + arrowLabels[0] : "")}");
        var goBack = Method(app, "public void GoBack()");
        check(app.Contains("_cameFrom[s] = Screen") && goBack.Contains("CameFrom(Screen, ScreenId.Trials)") && app.Contains("it.id == CameFrom(Screen, ScreenId.Trials)"),
            "ux rule 2: Back from the Titan, dungeons and arena returns to the tab they were opened from, and the nav highlights it");
        foreach (var root2 in new[] { ("Screens.cs", "void DrawSummon(Rect area)"), ("TrialsScreens.cs", "void DrawTrials(Rect area)") })
            check(Method(files[root2.Item1], root2.Item2).Contains("Ui.Banner("), $"ux rule 2: tab root {root2.Item2.Split(' ')[1].Split('(')[0]} keeps its ribbon banner");

        // ---------- rule 3: button roles ----------
        var kindEnum = Regex.Match(ui, @"public enum Kind \{([^}]*)\}").Groups[1].Value.Replace(" ", "");
        check(kindEnum == "Primary,Secondary,Danger,Chip,ChipOn,Ghost" && !Regex.IsMatch(all, @"Kind\.(Blue|Red)\b"), "ux rule 3: three button roles (Primary, Secondary, Danger); Kind.Blue and Kind.Red are gone");
        var dangerOk = new[] { "Release", "Reset", "Delete", "Sell", "Retreat", "Stop", "Remove", "Start a new game", "Hold to release" };
        var dangerBad = new List<string>();
        foreach (var kv in code)
            foreach (Match m in Regex.Matches(kv.Value, @"Ui\.Button\(([^;\n]*?)Ui\.Kind\.Danger"))
            {
                var lits = Regex.Matches(m.Groups[1].Value, @"""([^""\n]*)""").Cast<Match>().Select(x => x.Groups[1].Value).ToList();
                if (lits.Count > 0 && !lits.Any(l => dangerOk.Any(w => l.StartsWith(w, StringComparison.Ordinal)))) dangerBad.Add(lits[0]);
            }
        foreach (Match m in Regex.Matches(all, @"\(""([^""]*)"", UiRole\.Danger")) if (!dangerOk.Any(w => m.Groups[1].Value.StartsWith(w, StringComparison.Ordinal))) dangerBad.Add(m.Groups[1].Value);
        check(dangerBad.Count == 0, $"ux rule 3: red (Danger) only on destructive or stop actions{(dangerBad.Count > 0 ? "; " + string.Join(", ", dangerBad) : "")}");
        var roles = new[] { UiRole.Primary, UiRole.Secondary, UiRole.Danger, UiRole.Secondary };
        var order = UiLayout.RowOrder(roles);
        bool rowsOk = order.Last() == 0 && order.SequenceEqual(new[] { 1, 2, 3, 0 });
        foreach (float w in new[] { 300f, 576f, 868f })
        {
            var boxes = UiLayout.ButtonRow(new Box(0, 0, w, 300), roles, 48);
            var prim = boxes[0];
            rowsOk &= boxes.All(b => b.Inside(new Box(0, 0, w, 300)) && b.W >= Math.Min(120, w)) && boxes.All(b => b.YMax <= prim.YMax + 0.1f && (b.Y < prim.Y - 0.1f || b.XMax <= prim.XMax + 0.1f));
            for (int i = 0; i < boxes.Length; i++) for (int j = i + 1; j < boxes.Length; j++) rowsOk &= !boxes[i].Overlaps(boxes[j]);
        }
        check(rowsOk, "ux rule 3: Ui.ButtonRow puts the Primary last (bottom right), sizes buttons together and wraps without overlaps (300, 576, 868 wide)");
        check(Regex.Matches(all, @"Ui\.ButtonRow\(").Count >= 6 && Method(bc, "void DrawResults(Rect r, ResultsSpec spec)").Contains("Ui.ButtonRow(") && climb.Contains("Ui.ButtonRow("),
            "ux rule 3: results, summaries and confirms lay their buttons out with Ui.ButtonRow");
        check(!extras.Contains("can ? Ui.Kind.Danger") && !Regex.IsMatch(screens, @"Summon .{0,40}Ui\.Kind\.(Blue|Danger)"), "ux rule 3: Hard stage chips and Summon x10 are not red or blue");

        // ---------- rule 4: one selected style ----------
        var mark = Method(ui, "static void SelectedMark(Rect r, bool painted)");
        check(button.Contains("if (k == Kind.ChipOn) SelectedMark(plate, painted);") && mark.Contains("Palette.Gold") && mark.Contains("Icon.Check") && mark.Contains("r.yMax - 6"),
            "ux rule 4: ChipOn always draws the selected mark: gold rim and wash, underline and check (not colour alone)");
        check(File.Exists(Path.Combine(root, "Assets", "Resources", "Skin", "button_chipon.png")) && Regex.IsMatch(mark, @"painted \? 0\.1[5-9]"),
            "ux rule 4: the painted chip_on plate exists and gets a stronger gold wash than the drawn one");
        check(!bc.Contains("static bool HudChip(") && Method(extras, "float HeroTabs(").Contains("Ui.Segmented(") && Method(trials, "void DrawDungeon(").Contains("Ui.Segmented(")
              && Method(ui, "public static Rect CardFrame(").Contains("if (selected) SelectedMark(r, painted)") && screens.Contains("Ui.CardFrame(r, inTeam,") && !climb.Contains("Ui.Round(m, Palette.Gold, null, 9)"),
            "ux rule 4: tabs are segmented switches, in-team cards take the one selected mark (Ui.CardFrame) and switches the ChipOn look; the extra check badges and HudChip are gone");

        // ---------- rule 5: disabled and locked ----------
        // LoadingPlaceholder's light sweep is a decorative band over a picture (no text), so it may be faint.
        check(!code.Where(kv => kv.Key != "LoadingPlaceholder.cs").Any(kv => Regex.IsMatch(kv.Value, @"GUI\.color = new Color\(1, 1, 1, 0\.[0-7]")), "ux rule 5: nothing fades its text with GUI.color alpha under 0.8");
        check(ui.Contains("public static bool LockedCard(Rect r, string title, string reason, float progress = -1)") && Regex.Matches(trials, @"Ui\.LockedCard\(").Count >= 2,
            "ux rule 5: Trials cards and dungeon floors use the shared opaque Ui.LockedCard");
        var unlock = code.SelectMany(kv => Literals(kv.Value)).Where(l => Regex.IsMatch(l, "unlock", RegexOptions.IgnoreCase) && Regex.IsMatch(l, "^(Unlocks|Clear)")).ToList();
        check(Words.Unlock("1-6") == "Clear stage 1-6 to unlock" && unlock.All(l => Regex.IsMatch(l, "^Clear (stage|floor|Hard) .* to unlock")) && Regex.Matches(all, @"Words\.Unlock\(").Count >= 5,
            $"ux rule 5: one unlock phrasing, \"Clear stage X to unlock\" ({unlock.Count} literal(s) checked)");
        var ach = Method(extras, "void DrawAchievements(Rect area)");
        check(ach.Contains("ready ? \"Claim\" : shortProg") && trials.Contains("y += 12;") && !trials.Contains("GUI.color = col"),
            "ux rule 5: a disabled Claim names its progress; the Trials footer sits below the cards' frames");

        // ---------- rule 6: modals ----------
        var draw = Method(app, "void DrawModalAndToasts()");
        check(draw.Contains("Ui.Begin(true);") && draw.Contains("UiLayout.ModalWidth(safe.width, _modalMaxW)") && draw.Contains("UiLayout.ToastWidth(safe.width)") && !draw.Contains("Ui.W - 16") && !draw.Contains("Ui.W - 60"),
            "ux rule 6: modal and toast widths come from the safe screen, not the screen behind");
        check(UiLayout.ModalWidth(1067, 560) == 560 && UiLayout.ModalWidth(390, 560) == 358 && UiLayout.ModalWidth(800, 900) == 768 && UiLayout.ToastWidth(1067) == 620,
            "ux rule 6: the same dialog is 560 wide on any screen at least 592 units wide (results 900)");
        check(draw.Contains("if (_modalClosable)") && draw.Contains("Icon.Close") && draw.Contains("!box.Contains(e.mousePosition)") && app.Contains("public void InfoModal(")
              && app.Contains("InfoModal(tip.Title") && screens.Contains("InfoModal(\"How to play\"") && Src("StatusIcons.cs").Contains("app.InfoModal(") && Src("WhatsNew.cs").Contains("InfoModal(title"),
            "ux rule 6: info modals (tips, help, status legend, What's New) have a 48 dp X and close on an outside tap");
        check(app.Contains("public bool ModalFooter(Rect b") && Regex.Matches(all, @"ModalFooter\(b").Count >= 5, "ux rule 6: info modals share one footer");
        check(Regex.Matches(set, "SettingGroup\\(ref y, b, \"").Count >= 7 && Between(set, "SettingGroup(ref y, b, \"Accessibility\")", "y += 4;").Contains("\"Screen shake\""),
            "ux rule 6: Settings has section headings, Screen shake under Accessibility");

        // ---------- rule 7: results ----------
        var finishes = new[] { "void Finish()", "void FinishHard()", "void FinishTitan()", "void FinishDungeon()", "void FinishArena()" };
        var notSpec = finishes.Where(f => !Between(bc, f, "\n        }\n").Contains("ShowResults(")).ToList();
        check(notSpec.Count == 0 && !all.Contains("ResultModal(") && !Regex.IsMatch(all, @"App\.Modal\(""(VICTORY|DEFEAT)"""),
            $"ux rule 7: every battle finish builds a ResultsSpec and shows it (both orientations){(notSpec.Count > 0 ? "; not: " + string.Join(", ", notSpec) : "")}");
        var titles = Regex.Matches(bc, @"ShowResults\(([^,]+),").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();
        check(titles.Count > 0 && titles.All(t => t == "Words.Victory" || t == "Words.Defeat" || t == "Words.Cleared" || t.StartsWith("res.Win ? Words.Victory : Words.Defeat") || t == "string title"),
            $"ux rule 7: results titles are VICTORY, DEFEAT or CLEARED ({string.Join(" | ", titles)})");
        var labels = Regex.Matches(bc, @"\.Add\(([^,\n]+), UiRole\.").Cast<Match>().Select(m => m.Groups[1].Value.Trim()).Distinct().ToList();
        check(labels.All(l => l.StartsWith("Words.") || l == "\"Stop Replay\""), $"ux rule 7: results buttons use the Words vocabulary ({string.Join(" | ", labels)})");
        var dr = Method(bc, "void DrawResults(Rect r, ResultsSpec spec)");
        check(dr.Contains("bool narrow = r.width < 600") && dr.Contains("u == mvp ? \"  \" + Ui.ColorTag(\"MVP\"") && Regex.Matches(bc, @"Words\.EditTeam").Count >= 6,
            "ux rule 7: portrait results have the MVP, the damage stats and Edit Team too");
        check(bc.Contains("App.Modal(\"Replay Summary\"") && bc.Contains("DrawClimbSummary(r, log, null, spec, _arenaSlot, true)") && climb.Contains("App.OpenArenaPrep("),
            "ux rule 7: the Replay summary uses the Climb Summary layout, and its Edit Team opens team setup for the arena too");

        // ---------- rule 8: orientation parity ----------
        var hudTall = Method(bc, "public void DrawHud()"); var hudWide = Method(bc, "void DrawHudWide()");
        check(Regex.Matches(bc, @"static readonly HudBtn\[\] HudOrder").Count == 1 && hudTall.Contains("DrawHudButtons(") && hudWide.Contains("DrawHudButtons(") && !hudTall.Contains("Icon.Loop") && !hudWide.Contains("Icon.Loop"),
            "ux rule 8: the battle corner buttons come from one list (HudOrder), drawn the same way in both orientations");
        check(hudTall.Contains("CooldownText(cd)") && hudWide.Contains("\"turns\"") && !bc.Contains("ready in") && hudTall.Contains("SkillIcon(") && hudWide.Contains("SkillIcon(") && hudTall.Contains("ShortNames("),
            "ux rule 8: skill buttons show the same icon, short name and \"N turns\" in both orientations");
        var tallPrep = Method(screens, "void DrawPrep(Rect area)");
        check(tallPrep.Contains("EnemyChip(") && tallPrep.Contains("_prepFilter") && tallPrep.Contains("strong") && prep.Contains("OpenArenaPrep(") && trials.Contains("OpenArenaPrep(rv)") && !trials.Contains("Battle.Begin(BattleSpec.ForArena"),
            "ux rule 8: portrait team setup has the waves, matchup and element filter; the arena goes through team setup like every fight");

        // ---------- in-house report (build 190): the shared pieces at the extremes ----------
        // Portrait at Larger tap targets (56 dp): the corner buttons wrap instead of landing on Leave.
        foreach (float bsz in new[] { 48f, 56f })
        {
            float left = 6, right = 390 - 6, bg = 6;
            var leave = new Box(left, 6, bsz, bsz);
            var errs = new List<string>();
            for (int n = 5; n <= 7; n++)
            {
                var hb = UiLayout.HudButtons(right, 6, left + bsz + bg, bsz, bg, n);
                var allB = hb.Append(leave).ToArray();
                for (int i = 0; i < allB.Length; i++) for (int j = i + 1; j < allB.Length; j++) if (allB[i].Overlaps(allB[j])) errs.Add($"{n} buttons: {i}/{j}");
                foreach (var b in hb) if (b.X < left - 0.1f || b.XMax > right + 0.1f) errs.Add($"{n} buttons: {b} off screen");
            }
            check(errs.Count == 0, $"ux rule 1/8: portrait battle corner buttons at {bsz:0} units (390 wide) never overlap Leave or each other (they wrap){(errs.Count > 0 ? "; " + errs[0] : "")}");
        }
        check(Method(bc, "(int rows, float lastX) DrawHudButtons(").Contains("UiLayout.HudButtons("), "ux rule 8: the HUD draws its corner buttons from that UiLayout arithmetic");
        // Bottom nav labels fit their cell at any text size (Extra large ran "HEROESSUMMON" together).
        var fit = Method(ui, "public static void FitText(");
        check(Method(app, "void DrawNav(Rect r)").Contains("Ui.FitText(") && fit.Contains("px > MinText") && fit.Contains("sf = false"),
            "ux rule 11: nav labels shrink to their cell (to the 11-point floor, then the narrower face) at every text size");
        // Coloured words reach 4.5:1 on the panel in every palette (colour-blind colours were 3.2:1).
        var panelHex = Regex.Match(Src("Palette.cs"), @"Panel = Hex\(""(#[0-9a-fA-F]{6})""\)").Groups[1].Value;
        (float, float, float) Rgb(string h) { h = h.TrimStart('#'); return (Convert.ToInt32(h.Substring(0, 2), 16) / 255f, Convert.ToInt32(h.Substring(2, 2), 16) / 255f, Convert.ToInt32(h.Substring(4, 2), 16) / 255f); }
        double worst = 99, worstRaw = 99; string worstName = "";
        if (panelHex.Length == 7)
        {
            var (pr, pg, pb) = Rgb(panelHex); double lp = TextColour.Luminance(pr, pg, pb);
            for (int mode = 0; mode < Accessibility.ElementHex.Length; mode++)
                foreach (var hx in Accessibility.ElementHex[mode].Concat(Accessibility.RarityHex[mode]).Concat(Accessibility.GearHex[mode]).Concat(Accessibility.GoodBadHex[mode]))
                {
                    var (r, g, b) = Rgb(hx);
                    worstRaw = Math.Min(worstRaw, TextColour.Contrast(TextColour.Luminance(r, g, b), lp));
                    var (nr, ng, nb) = TextColour.Readable(r, g, b, pr, pg, pb);
                    double c = TextColour.Contrast(TextColour.Luminance(nr, ng, nb), lp);
                    if (c < worst) { worst = c; worstName = $"{hx} (mode {mode})"; }
                }
        }
        check(worst >= 4.5 && Method(ui, "public static void Text(Rect r, string text").Contains("Readable(color.Value)") && Method(ui, "public static void PlainText(").Contains("c = Readable(c);") && ui.Contains("ColorUtility.ToHtmlStringRGB(Readable(c))"),
            $"ux rule 9/11: element, rarity, gear and good/bad colours read at 4.5:1 or better as text in every palette (worst {worst:0.0}:1 at {worstName}; raw swatch {worstRaw:0.0}:1)");

        // ---------- rule 9: colour meaning ----------
        check(!Method(app, "void DrawNav(Rect r)").Contains("Palette.Blue") && prep.Contains("ring = your team") && !modes.Contains("Ui.Round(b, Palette.Bad, Palette.Gold, 13)"),
            "ux rule 9: badges are not blue for one tab; the element wheel says what its ring colours mean");
        check(!Regex.IsMatch(Src("RankUpPanel.cs"), @"first \(Train on the hero screen\)"", 11, Palette\.Bad"), "ux rule 9: hints are gold, not the red that means enemy or danger");

        // ---------- rule 10: icons ----------
        var icons = Src("Icons.cs");
        var curUsers = new[] { "App.cs", "BattleController.cs", "AutoClimbUi.cs", "PrepScreens.cs", "WorldMap.cs" }.Where(f => !files[f].Contains("Icons.Currency(")).ToList();
        check(icons.Contains("public static void Currency(Rect r, Cur c)") && curUsers.Count == 0, $"ux rule 10: items draw through Icons.Currency everywhere (top bar, results, summaries, stage list, reward track){(curUsers.Count > 0 ? "; not: " + string.Join(", ", curUsers) : "")}");
        var glyphs = new List<string>();
        foreach (var kv in code)
            foreach (Match m in Regex.Matches(kv.Value, @"Ui\.Button\([^;]*?""((?:[^""\\\n]|\\.)*)"""))
            {
                string l = Regex.Replace(m.Groups[1].Value, @"\\u([0-9A-Fa-f]{4})", x => ((char)Convert.ToInt32(x.Groups[1].Value, 16)).ToString());
                if (l.EndsWith(" ▶")) l = l.Substring(0, l.Length - 2); else if (l.EndsWith("★")) l = l.Substring(0, l.Length - 1); // drawn as icons by Ui.Button
                if (l.Any(ch => (ch >= 0x2190 && ch <= 0x27BF) || ch == '‹' || ch == '›')) glyphs.Add(kv.Key + ": " + l);
            }
        check(glyphs.Count == 0 && ui.Contains("static bool TailIcon(ref string text, out Icon icon)"), $"ux rule 10: no symbol glyphs in button labels (a trailing arrow or star is drawn as an icon){(glyphs.Count > 0 ? "; " + glyphs[0] : "")}");
        check(prep.Contains("Ui.ElementBadge(new Rect(slot.x + 4") && screens.Contains("Ui.ElementBadge(Ui.ToRect(c.Badge), t.Element)"), "ux rule 10: team slots and the picker show elements as Ui.ElementBadge");

        // ---------- rule 11: text ----------
        var tiny = new List<string>();
        foreach (var kv in code)
            foreach (Match m in Regex.Matches(kv.Value, @"Ui\.(Text|TextOnArt|PlainText|Button|Heading)\(.*?, (\d), (Palette|Color|new Color|Ui\.|FontStyle)"))
                tiny.Add(kv.Key + ": " + m.Value.Substring(0, Math.Min(60, m.Value.Length)));
        foreach (var kv in code)
            foreach (Match m in Regex.Matches(kv.Value, @"Ui\.(Text|TextOnArt|PlainText|Button)\(.*?, 10, (Palette|Color|new Color|Ui\.|FontStyle)"))
                tiny.Add(kv.Key + ": " + m.Value.Substring(0, Math.Min(60, m.Value.Length)));
        check(tiny.Count == 0, $"ux rule 11: no text drawn under 11 points ({tiny.Count} found{(tiny.Count > 0 ? ": " + tiny[0] : "")})");
        check(ui.Contains("public const int MinText = 11;") && ui.Contains("size = Mathf.Max(MinText, size); return Accessibility.TextSize(size") && Regex.Matches(ui, @"size = Mathf\.Max\(MinText, size\);").Count >= 3,
            "ux rule 11: Ui floors every text size at 11 (Sized, TextPx, ButtonPx)");
        check(Method(screens, "void DrawPrep(Rect area)").Contains("Ui.TextOnArt(") && Method(extras, "void DrawAchievements(Rect area)").Contains("Ui.TextOnArt(") && Method(trials, "void DrawArenaPvp(Rect area)").Contains("Ui.TextOnArt("),
            "ux rule 11: lines over the 3D scene (prep, achievements, arena) sit on a dark pill");

        // ---------- rule 12: layout widths (the real rect arithmetic) ----------
        foreach (float fw in UiLayout.ReferenceWidths)
        {
            bool tablet = fw < 900;
            float h = tablet ? 480 : 600, tap = tablet ? 48 : 44;
            var area = new Box(0, 44, fw, h - 44 - 64);
            var errs = new List<string>();
            void NoOverlap(string what, params Box[] bs) { for (int i = 0; i < bs.Length; i++) for (int j = i + 1; j < bs.Length; j++) if (bs[i].Overlaps(bs[j])) errs.Add($"{what} {i}/{j} {bs[i]} {bs[j]}"); }
            void InArea(string what, Box b, Box a) { if (!b.Inside(a)) errs.Add($"{what} {b} outside {a}"); }
            void Min(string what, float v, float min) { if (v < min) errs.Add($"{what} {v:0} < {min}"); }
            // team setup
            var P = UiLayout.Prep(area, tap, fw);
            NoOverlap("prep header", P.Back, P.Title, P.Best, P.AutoEquip);
            NoOverlap("prep columns", P.Header, P.Team, P.Waves, P.Picker);
            foreach (var b in new[] { P.Back, P.Best, P.AutoEquip, P.Team, P.Picker }) InArea("prep", b, area);
            Min("prep title width", P.Title.W, 150); Min("picker width", P.Picker.W, 300); Min("back height", P.Back.H, tap);
            if (P.Compact != tablet) errs.Add("prep compact flag");
            var T = UiLayout.Team(P.Team, tap);
            NoOverlap("team", new[] { T.Sub, T.Go }.Concat(T.Slots).Concat(T.Presets).ToArray());
            foreach (var b in T.Slots.Concat(T.Presets).Append(T.Go)) InArea("team", b, P.Team);
            Min("slot width", T.Slots[0].W, 66); Min("preset width", T.Presets[0].W, 90); Min("preset height", T.Presets[0].H, tap);
            if (P.Compact) Min("waves room under the team", T.Rest.H, 60);
            // Home
            var H = UiLayout.Home(area, tap, fw);
            NoOverlap("home", H.Title, H.Right); NoOverlap("home", H.Title, H.Left); if (!H.Compact) NoOverlap("home", H.Left, H.Right);
            InArea("home cta", H.Cta, H.Compact ? area : H.Right); if (H.Compact) NoOverlap("home cta", H.Cta, H.Right); Min("home title width", H.Title.W, 200); Min("home column", H.Right.W, 230);
            // the stage list and its rows (1 to 4 enemies, with and without shards)
            var mapArea = new Box(10, area.Y + 8, fw - 20, area.H - 16);
            var list = UiLayout.StageList(mapArea, true, fw);
            InArea("stage list", list, mapArea);
            var row = new Box(0, 0, list.W - 16 - 4, 52);
            for (int n = 1; n <= 4; n++)
                foreach (bool sh in new[] { false, true })
                {
                    var s = UiLayout.StageRow(row, n, sh);
                    var parts = new List<Box> { s.Label, s.Enemies, s.Gold, s.Button }; if (sh) parts.Add(s.Shard); if (s.Gear.W > 0) parts.Add(s.Gear);
                    NoOverlap($"stage row ({n} enemies{(sh ? ", shards" : "")})", parts.ToArray());
                    foreach (var b in parts) InArea("stage row", b, row);
                    Min($"enemy chip ({n})", s.EnemyW, n <= 3 ? 60 : 50);
                }
            // results footer (the modal's width on this screen, Replay + Climb, up to four buttons)
            float mw = UiLayout.ModalWidth(fw, UiLayout.WideModalW) - 32;
            var rr = new Box(0, 0, mw, 400);
            for (int nb = 1; nb <= 4; nb++)
            {
                var f = UiLayout.ResultsFooter(rr, 2, nb, tap);
                NoOverlap($"results footer ({nb} buttons)", f.Toggles, f.Buttons);
                InArea("toggles", f.Toggles, rr); InArea("buttons", f.Buttons, rr);
                Min("switch width (\"Replay: Off\")", UiLayout.ToggleW(f.Toggles, 2), UiLayout.ToggleMinW);
                var bxs = UiLayout.ButtonRow(f.Buttons, Enumerable.Repeat(UiRole.Secondary, nb - 1).Append(UiRole.Primary).ToList(), tap);
                foreach (var b in bxs) { InArea("result button", b, f.Buttons); Min("result button width", b.W, 120); }
                NoOverlap("result buttons", bxs);
            }
            // the modes hub
            var cards = UiLayout.Modes(area, 5, fw);
            NoOverlap("modes", cards);
            foreach (var c in cards) { InArea("mode card", c, area); Min("mode card width", c.W, 150); Min("mode card height", c.H, 150); }
            check(errs.Count == 0, $"ux rule 12: team setup, Home, stage list rows, results footer and modes lay out at {fw:0} units with no overlaps or cut controls{(errs.Count > 0 ? "; " + errs[0] + (errs.Count > 1 ? $" (+{errs.Count - 1})" : "") : "")}");
        }
        // ---------- L028: card content inside the painted frame; one-line button labels; tablet Home and gear rows ----------
        // The small card frame's real 9-slice borders (Resources/Skin/skin.txt), as the client reads them.
        var skinTxt = Path.Combine(root, "Assets", "Resources", "Skin", "skin.txt");
        var cardB = UiLayout.CardSmallFallback;
        if (File.Exists(skinTxt))
            foreach (var line in File.ReadAllLines(skinTxt))
            {
                var p = line.Trim().Split(' ');
                if (p.Length == 5 && p[0] == "card_small") cardB = new UiLayout.Border9(float.Parse(p[1]), float.Parse(p[2]), float.Parse(p[3]), float.Parse(p[4]));
            }
        check(File.Exists(skinTxt) && cardB.L > 0 && cardB.T > 0, $"L028: skin.txt lists the card_small frame's borders ({cardB.L} {cardB.T} {cardB.R} {cardB.B})");
        foreach (float fw in UiLayout.ReferenceWidths)
        {
            bool tablet = fw < 900;
            float h = tablet ? 480 : 600, tap = tablet ? 48 : 44;
            var area = new Box(0, 44, fw, h - 44 - 64);
            var errs = new List<string>();
            void Card(string what, Box cell)
            {
                foreach (bool face in new[] { true, false })
                {
                    var c = UiLayout.HeroCard(cell, cardB, face);
                    var inner = UiLayout.FrameInner(cell, cardB, UiLayout.CardFrameK);
                    float band = Math.Min(cardB.T, cardB.L) * UiLayout.FrameScale(cell.W, cell.H, cardB, UiLayout.CardFrameK);
                    if (band < 4) errs.Add($"{what}: frame border {band:0.0} units (too thin to be the painted frame)");
                    foreach (var (n, b) in new[] { ("name", c.Name), ("line", c.Line), ("badge", c.Badge), ("face", c.Face) })
                        if (b.W > 0 && !b.Inside(inner)) errs.Add($"{what} {n} {b} outside the frame's inner rect {inner}");
                    if (c.Name.Overlaps(c.Line) || (face && (c.Face.Overlaps(c.Name) || c.Face.Overlaps(c.Line)))) errs.Add($"{what}: card parts overlap");
                    if (c.Name.H < 17 || c.Line.H < 17) errs.Add($"{what}: text lines {c.Name.H:0}/{c.Line.H:0} tall (< 17)");
                    if (c.Line.W < 90) errs.Add($"{what}: the stars, level and power line is {c.Line.W:0} wide (< 90)");
                    if (c.Name.XMax > cell.XMax - UiLayout.RarityTabW - 2) errs.Add($"{what}: the name runs under the rarity letter");
                }
            }
            var P = UiLayout.Prep(area, tap, fw);
            var g = UiLayout.Picker(P.Picker, tap);
            if (!g.View.Inside(P.Picker)) errs.Add($"picker view {g.View} outside {P.Picker}");
            for (int i = 0; i < 6; i++) { var cell = g.Cell(i); Card($"picker card {i}", new Box(g.View.X + cell.X, g.View.Y + cell.Y, cell.W, cell.H)); if (cell.XMax > g.View.W + 0.25f) errs.Add($"picker card {i} wider than the view"); }
            // the presets: one line inside the same frame
            foreach (var pr in UiLayout.Team(P.Team, tap).Presets) { var pin = UiLayout.FrameInner(pr, cardB, UiLayout.CardFrameK); if (pin.H < 18 || pin.W < 70) errs.Add($"preset content {pin} too small"); }
            check(errs.Count == 0, $"L028: team-setup hero cards keep the face, name and \"★ Lv · power\" line inside the painted frame's inner rect (9-slice borders) at {fw:0} units{(errs.Count > 0 ? "; " + errs[0] + (errs.Count > 1 ? $" (+{errs.Count - 1})" : "") : "")}");
            if (fw == UiLayout.ReferenceWidths[0])
            {
                // portrait team setup (390 wide): two columns of the same cards
                var perr = errs.Count; float cw = (390 - 32 - 8) / 2f;
                Card("portrait card", new Box(16, 0, cw, UiLayout.CardH(48)));
                check(errs.Count == perr, $"L028: portrait team-setup cards keep their text inside the frame{(errs.Count > perr ? "; " + errs[perr] : "")}");
            }
        }
        // Button labels: one word (or a label with a tail icon) never wraps; it shrinks to fit on one line.
        var oneLine = Method(ui, "static int OneLinePx(");
        check(button.Contains("bool oneLine = OneLine(text, tail);") && button.Contains("px = OneLinePx(lr, text, px, ref serif, k)") && Regex.Matches(button, @"ButtonStyle\(k, px, serif, (false, true|true, false), !oneLine\)").Count >= 3
              && ui.Contains("s.wordWrap = wrap;") && oneLine.Contains("fit > MinText") && oneLine.Contains("serif = false"),
            "L028: one-word and arrow button labels never wrap (\"TEA / M\"): no word wrap, shrink to the 11-point floor, then the sans face");
        // The stage row's Team button has room for "TEAM" and its arrow at 13 points on one line (Cinzel capitals are
        // under 0.8 em wide; 4 letters).
        {
            var errs = new List<string>();
            foreach (float fw in UiLayout.ReferenceWidths)
            {
                var list = UiLayout.StageList(new Box(10, 52, fw - 20, 400), true, fw);
                var row = new Box(0, 0, list.W - 16 - 4, 52);
                var s = UiLayout.StageRow(row, 4, true);
                float room = UiLayout.LabelRoom(s.Button, true), need = 4 * 0.8f * 13;
                if (room < need) errs.Add($"{fw:0}: label room {room:0.0} < {need:0.0}");
            }
            check(errs.Count == 0, $"L028: the stage row's \"TEAM ▶\" button fits its label on one line at default size ({UiLayout.StageButtonW:0} wide){(errs.Count > 0 ? "; " + errs[0] : "")}");
        }
        // Compact Home (a 1024x768 window is 800x600 units; a 4:3 tablet 640x480): the team stands clear of the card
        // column, Continue sits under the team, and the column shows login and achievements without scrolling (800: all of it).
        foreach (var (fw, h) in new[] { (640f, 480f), (800f, 600f) })
        {
            float tap = 48, chipsH = 48;
            var area = new Box(0, 44, fw, h - 44 - 64);
            var H = UiLayout.Home(area, tap, fw);
            var errs = new List<string>();
            double shift = HomeLayout.ShiftFor(H.Team.CenterX / fw, fw / h);
            var (l, r) = HomeLayout.TeamSpanX(fw / h, shift);
            if (l * fw < 0 || r * fw > H.Right.X - 4) errs.Add($"team spans {l * fw:0}..{r * fw:0}, column at {H.Right.X:0}");
            var (bt, bb) = HomeLayout.TeamBand();
            if (H.Cta.Y < (bb + HomeLayout.Margin) * h) errs.Add($"Continue at {H.Cta.Y:0} over the team's feet at {(bb + HomeLayout.Margin) * h:0}");
            if (H.Cta.Overlaps(H.Right) || H.Title.Overlaps(H.Right) || !H.Cta.Inside(area)) errs.Add("Continue or the title overlaps the column");
            float colH = UiLayout.HomeColumnH(5, chipsH);
            if (UiLayout.LoginH + UiLayout.HomeGap + UiLayout.AchievementsH > H.Left.H) errs.Add("achievements cut off");
            if (fw >= 800 && colH > H.Left.H + 0.25f) errs.Add($"column content {colH:0} > {H.Left.H:0}");
            if (H.Right.W < 260) errs.Add($"column {H.Right.W:0} wide");
            check(H.Compact && errs.Count == 0, $"ux rule 12: compact Home at {fw:0}x{h:0}: the team stands clear of the cards, Continue under it, achievements in view{(errs.Count > 0 ? "; " + errs[0] : "")}");
        }
        check(Method(screens, "void DrawHomeWide(Rect area)").Contains("HomeLayout.ShiftFor(") && app.Contains("Arena.MenuShift = Screen == ScreenId.Home") && Src("Arena.cs").Contains("pos += Vector3.right * _menuShift"),
            "ux rule 12: compact Home slides the menu camera by that shift (and only on Home)");
        // Equipment rows: the name has its own line, clear of Lock and Upgrade, inside the frame (column and portrait widths).
        {
            var errs = new List<string>();
            foreach (float tap in new[] { 44f, 48f, 56f })
                foreach (float w in new[] { 358f, 326f })
                {
                    var r = new Box(16, 0, w, UiLayout.GearRowH(tap));
                    var gr = UiLayout.GearRow(r, cardB, tap);
                    var parts = new[] { gr.Name, gr.Main, gr.Subs, gr.Lock, gr.Up, gr.Sell, gr.Equip };
                    for (int i = 0; i < parts.Length; i++) { if (!parts[i].Inside(gr.Inner)) errs.Add($"{w:0}/{tap:0}: part {i} {parts[i]} outside {gr.Inner}"); for (int j = i + 1; j < parts.Length; j++) if (parts[i].Overlaps(parts[j])) errs.Add($"{w:0}/{tap:0}: parts {i}/{j} overlap"); }
                    if (gr.Name.W < 280) errs.Add($"{w:0}: name {gr.Name.W:0} wide");
                    if (gr.Subs.H < 30) errs.Add($"{w:0}/{tap:0}: sub-stats {gr.Subs.H:0} tall");
                }
            check(errs.Count == 0 && Method(screens, "void DrawEquip(Rect area)").Contains("UiLayout.GearRow("), $"ux rule 12: equipment rows put the item name on its own line, clear of Lock (1024x768 ran it under){(errs.Count > 0 ? "; " + errs[0] : "")}");
        }
        check(!prep.Contains("Ui.Plate(r, Ui.Kind.ChipOn)") && !screens.Contains("Ui.Plate(r, Ui.Kind.ChipOn)") && prep.Contains("UiLayout.Picker(Ui.ToBox(R), tap)") && prep.Contains("Ui.CardFrame(pr, false"),
            "L028: hero and preset cards draw the card frame, not the one-line chip plate, from the same UiLayout arithmetic");

        check(prep.Contains("UiLayout.Prep(Ui.ToBox(area), tap, Ui.FullW)") && screens.Contains("UiLayout.Home(Ui.ToBox(area), Ui.Tap, Ui.FullW)") && prep.Contains("UiLayout.StageRow(Ui.ToBox(row)") && modes.Contains("UiLayout.Modes(Ui.ToBox(area)")
              && Method(bc, "void DrawResults(Rect r, ResultsSpec spec)").Contains("UiLayout.ResultsFooter("),
            "ux rule 12: the client draws those screens from the same UiLayout arithmetic the checks above run");
        check(map.Contains("MapImageRect(map, paint.width / (float)paint.height, pts)") && map.Contains("GUI.DrawTextureWithTexCoords(vis, paint, uv)") && !map.Contains("Skin.Tex(\"bg_campaign\"), ScaleMode.StretchToFill") && map.Contains("MapPos(img, pts[k])"),
            "ux rule 12: the painted campaign maps keep their aspect (cropped or letterboxed) and the medallions follow the drawn image");

        // ---------- rule 13: safe area ----------
        var banner = Method(upd, "void DrawBanner()");
        check(banner.Contains("Ui.Begin(true);") && banner.Contains("Ui.SafeContent(16)") && banner.Contains("Ui.Button(btn,") && banner.Contains("Ui.Tap") && !banner.Contains("Screen.dpi"),
            "ux rule 13: the update banner is drawn in Ui units inside the safe area, with the shared buttons");

        // ---------- rule 14: numbers and words ----------
        var literals = code.SelectMany(kv => Literals(kv.Value).Select(l => (kv.Key, l))).ToList();
        var banned = new List<string>();
        foreach (var w in Words.Banned)
            foreach (var (f, l) in literals)
                if (Regex.IsMatch(l, $@"(?<![A-Za-z_]){Regex.Escape(w)}(?![A-Za-z_])")) banned.Add($"{f}: \"{w}\" in \"{l}\"");
        check(banned.Count == 0, $"ux rule 14: one name per feature (no {string.Join(", ", Words.Banned.Take(4))}… in client strings){(banned.Count > 0 ? "; " + banned[0] : "")}");
        var topBar = Method(app, "void DrawTopBar(Rect r)");
        check(topBar.Contains("string text = Ui.Fmt(amount)") && !topBar.Contains(".ToString()") && ui.Contains("n >= 100_000 ?"), "ux rule 14: counts go through Ui.Fmt (12,345 up to 99,999)");
        check(new Reward { Common = 2, Mystic = 1, Tome = 1 }.ToString() == "2 Worn Shards, 1 Mystic Shard, 1 Skill Tome", "ux rule 14: Reward text names items through ShardName and TomeName, as the results do");
        check(Src("ModesScreen.cs").Contains("Words.Unlock(") && map.Contains("Act1Title") && Sigilbound.Core.GameData.Act1Title.StartsWith("Act I · "), "ux rule 14: both acts' banners carry a subtitle");
    }
}
