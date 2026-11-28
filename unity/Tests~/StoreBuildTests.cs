// Play Store build configuration (owner decisions 2026-11-28, READINESS items 5, 8 and 9): the store bundle
// (BuildScript.AndroidStore, define SB_STORE) installs as com.sigilbound.game while the tester APK keeps
// com.sigilbound.unity, and the store build has no Friends or chat for v1: Online.cs and FriendsScreens.cs are
// not compiled, the nav has five tabs that share the band, and no compiled code calls functions/api/sb.
// The client sources are read with SB_STORE defined (a small #if evaluator) so the check sees what the store
// build compiles; CI also compiles that variant (CompileCheck -p:ExtraDefines=SB_STORE).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Sigilbound.Core;

static class StoreBuildTests
{
    // Keeps the lines a build with (or without) SB_STORE compiles. Conditions that don't mention SB_STORE keep both
    // branches, so the check only ever sees more code than the build, never less.
    public static string Visible(string src, bool store)
    {
        var outLines = new List<string>();
        var stack = new Stack<(bool parentOn, bool known, bool taken, bool on)>();
        bool on = true;
        foreach (var raw in src.Split('\n'))
        {
            var l = raw.Trim();
            if (l.StartsWith("#if ") || l.StartsWith("#elif ") || l == "#else" || l.StartsWith("#endif"))
            {
                if (l.StartsWith("#if "))
                {
                    var (known, val) = Eval(l.Substring(4), store);
                    stack.Push((on, known, known && val, on && (!known || val)));
                }
                else if (stack.Count > 0)
                {
                    var f = stack.Pop();
                    if (l.StartsWith("#endif")) { on = f.parentOn; continue; }
                    if (l == "#else") { stack.Push((f.parentOn, f.known, true, f.parentOn && (!f.known || !f.taken))); }
                    else { var (known, val) = Eval(l.Substring(6), store); bool k = f.known && known; stack.Push((f.parentOn, k, f.taken || (k && val), f.parentOn && (!k || (!f.taken && val)))); }
                }
                on = stack.Count > 0 ? stack.Peek().on : true;
                continue;
            }
            if (on) outLines.Add(raw);
        }
        return string.Join("\n", outLines);
    }
    static (bool known, bool val) Eval(string cond, bool store)
    {
        cond = Regex.Replace(cond, @"//.*$", "").Trim();
        if (cond == "SB_STORE") return (true, store);
        if (cond == "!SB_STORE") return (true, !store);
        return (false, false);
    }

    static string Block(string src, string start, string end)
    {
        int a = src.IndexOf(start, StringComparison.Ordinal); if (a < 0) return "";
        int b = src.IndexOf(end, a, StringComparison.Ordinal); return b < 0 ? src.Substring(a) : src.Substring(a, b - a);
    }

    public static void Run(Action<bool, string> check)
    {
        // The evaluator itself.
        const string sample = "a\n#if !SB_STORE\nb\n#else\nc\n#endif\n#if SB_STORE\nd\n#endif\n#if UNITY_2022_2_OR_NEWER\ne\n#else\nf\n#endif\n#if SB_STORE\n#if X\ng\n#endif\n#endif\nh";
        check(Visible(sample, true).Replace("\n", "") == "acdefgh" && Visible(sample, false).Replace("\n", "") == "abefh", "store: the #if reader keeps what each variant compiles");

        // Nav arithmetic: the tabs share the band, contiguous and inside it, with five tabs (store) or six (testers).
        bool navOk = true; var bad = new List<string>();
        foreach (int n in new[] { 5, 6 })
            foreach (var (x0, w) in new[] { (0f, 390f), (0f, 360f), (40f, 560f), (12f, 616f - 24f), (253f, 560f) }) // portrait canvases, landscape bands (16:9, compact 4:3 at 640)
            {
                var cells = Enumerable.Range(0, n).Select(i => UiLayout.NavCell(i, n, x0, w, 700)).ToArray();
                bool ok = Math.Abs(cells[0].X - x0) < 0.01f && Math.Abs(cells[n - 1].XMax - (x0 + w)) < 0.01f
                    && cells.All(c => Math.Abs(c.W - w / n) < 0.01f && c.H == UiLayout.NavH && c.Y == 700)
                    && cells.Zip(cells.Skip(1), (a, b) => Math.Abs(a.XMax - b.X) < 0.01f).All(z => z);
                if (!ok) { navOk = false; bad.Add($"{n} tabs in {w}"); }
            }
        check(navOk, "store: the nav tabs fill the band edge to edge with five or six tabs, portrait and landscape " + string.Join(", ", bad));
        check(390f / 5 >= 64 && 560f / 5 >= 64, "store: five tabs are wider than six (each cell at least 64 units)");

        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "store: sources not found here (skipped)"); return; }
        string Src(string rel) { var p = Path.Combine(root, "Assets", "Sigilbound", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }

        // Package names.
        var build = Src("Editor/BuildScript.cs"); var setup = Src("Editor/ProjectSetup.cs");
        var storeBody = Block(build, "public static void AndroidStore()", "static string[] SetUpdaterPlugins");
        var testerBody = Block(build, "public static void Android()", "public static bool IsStoreBuild");
        check(build.Contains("public const string StoreAppId = \"com.sigilbound.game\";"), "store: the Play Store package name is com.sigilbound.game");
        check(setup.Contains("public const string AppId = \"com.sigilbound.unity\";") && setup.Contains("SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, AppId)"),
            "store: the tester APK keeps com.sigilbound.unity (ProjectSetup.AppId)");
        int setAt = storeBody.IndexOf("SetApplicationIdentifier(target, StoreAppId)", StringComparison.Ordinal), applyAt = storeBody.IndexOf("ProjectSetup.Apply()", StringComparison.Ordinal);
        check(setAt > applyAt && applyAt > 0 && !storeBody.Contains("SB_PACKAGE") && storeBody.Contains("SetApplicationIdentifier(target, ProjectSetup.AppId)"),
            "store: only the store build sets com.sigilbound.game (after ProjectSetup.Apply, no override) and puts the tester id back afterwards");
        check(testerBody.Contains("ProjectSetup.Apply()") && !testerBody.Contains("StoreAppId") && !testerBody.Contains("SetApplicationIdentifier"),
            "store: the tester build never uses the store package name");

        // No Friends or chat in the store build.
        foreach (var f in new[] { "Client/Online.cs", "Client/FriendsScreens.cs" })
        {
            var s = Src(f);
            var code = s.Split('\n').Where(l => l.Trim().Length > 0 && !l.TrimStart().StartsWith("//")).ToList();
            check(s.Length > 0 && code.Count > 2 && code[0].Trim() == "#if !SB_STORE" && code[code.Count - 1].Trim() == "#endif" && Visible(s, true).Split('\n').All(l => l.Trim().Length == 0 || l.TrimStart().StartsWith("//")),
                $"store: {f} is entirely inside #if !SB_STORE (the store build doesn't compile it)");
        }
        var clientDir = Path.Combine(root, "Assets", "Sigilbound", "Client"); var coreDir = Path.Combine(root, "Assets", "Sigilbound", "Core");
        var files = Directory.GetFiles(clientDir, "*.cs", SearchOption.AllDirectories).Concat(Directory.GetFiles(coreDir, "*.cs", SearchOption.AllDirectories)).ToList();
        string[] banned = { "api/sb", "sigilbound.pages.dev", "class Online", "Online.", "OpenFriends(", "DrawFriends(", "DrawFriendProfile(", "DrawChat(", "DrawBlocked(", "\"Friends\"", "friend code" };
        List<string> Hits(bool store)
        {
            var found = new List<string>();
            foreach (var f in files)
                foreach (var line in Visible(File.ReadAllText(f), store).Split('\n'))
                {
                    var t = line.Trim(); if (t.StartsWith("//")) continue;
                    int cmt = t.IndexOf("//", StringComparison.Ordinal); var codePart = cmt >= 0 && !t.Contains("://") ? t.Substring(0, cmt) : t;
                    foreach (var b in banned) if (codePart.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0) found.Add(Path.GetFileName(f) + ": " + b);
                }
            return found;
        }
        var hits = Hits(true);
        check(Hits(false).Any(h => h.Contains("api/sb")) && Hits(false).Any(h => h.Contains("DrawChat(")), "store: the scan finds the Friends code in the tester variant (so it is really looking)");
        check(hits.Count == 0, "store: no Friends UI, chat, friend codes or functions/api/sb calls in the code the store build compiles " + string.Join("; ", hits.Distinct().Take(8)));
        var app = Src("Client/App.cs");
        var navStore = Block(Visible(app, true), "NavItems =", "};"); var navTester = Block(Visible(app, false), "NavItems =", "};");
        int tabsStore = Regex.Matches(navStore, @"\(ScreenId\.").Count, tabsTester = Regex.Matches(navTester, @"\(ScreenId\.").Count;
        check(tabsStore == 5 && !navStore.Contains("ScreenId.Friends") && tabsTester == 6 && navTester.Contains("(ScreenId.Friends, \"Friends\", Icon.Friends)"),
            $"store: the store nav has five tabs and no Friends; tester builds keep the Friends tab ({tabsStore}/{tabsTester})");
        var order = new[] { "ScreenId.Home", "ScreenId.Modes", "ScreenId.Heroes", "ScreenId.Summon", "ScreenId.Trials" };
        check(order.Select(o => navStore.IndexOf(o, StringComparison.Ordinal)).Zip(order.Skip(1).Select(o => navStore.IndexOf(o, StringComparison.Ordinal)), (a, b) => a >= 0 && a < b).All(z => z),
            "store: the first five tabs keep their order (the tutorial points at Home, Battle, Heroes and Summon by index)");
        var navCell = Block(app, "public static Rect NavCell", "public void PushProfile");
        check(navCell.Contains("UiLayout.NavCell(i, NavItems.Length") && !Regex.IsMatch(navCell, @"/ ?6f") && Block(app, "void DrawNav(Rect r)", "int HomeBadge()").Contains("NavItems"),
            "store: the nav's cells come from the tab count, not a fixed six, so it re-flows without Friends");
        var go = Block(Visible(app, true), "public void Go(ScreenId s)", "void Update()");
        check(go.Contains("if (FriendsScreen(s)) s = ScreenId.Home;"), "store: any way into a Friends screen lands on Home in the store build");
        var battle = Src("Client/BattleController.cs");
        check(!battle.Contains("App.Online") && battle.Contains("App.PushProfile();"), "store: battles push the Friends profile through App (a no-op in the store build)");
    }
}
