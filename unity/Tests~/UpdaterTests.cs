// In-app updater (QA review 2026-11-27, P0-4): the version check and download check (Core/UpdateCheck.cs)
// against a real "unity-latest" release response and broken ones, and the Play Store build leaving the
// updater out (SB_STORE: Updater.cs not compiled, no install code in its stand-in, no install permission
// or updater plugins in the store AAB).
using System;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class UpdaterTests
{
    public static void Run(Action<bool, string> check)
    {
        var fixture = Path.Combine(AppContext.BaseDirectory, "fixtures", "release-unity-latest.json");
        check(File.Exists(fixture), "updater: release fixture present");
        if (File.Exists(fixture))
        {
            var json = File.ReadAllText(fixture);
            var a = UpdateCheck.Parse(json, UpdatePlatform.Android);
            check(a.Build == 169 && a.File == "Sigilbound-Unity-0.21.1-build169.apk" && a.Version == "0.21.1" && a.Size == 194697334,
                $"updater: the real release gives the versioned APK, build and size ({a.File}, {a.Size})");
            var w = UpdateCheck.Parse(json, UpdatePlatform.Windows);
            check(w.Build == 169 && w.File == "Sigilbound-Windows-0.21.1-build169.zip" && w.Size == 192445789, "updater: the real release gives the versioned Windows zip");
            check(UpdateCheck.IsNewer(a, 168) && !UpdateCheck.IsNewer(a, 169) && !UpdateCheck.IsNewer(a, 170), "updater: only a strictly newer build is offered (168 yes, 169 and 170 no)");
            check(!UpdateCheck.IsNewer(a, 0), "updater: a build without a number (local, editor) is never offered updates");
            var cut = UpdateCheck.Parse(json.Substring(0, json.IndexOf("Sigilbound-Windows-0.21.1", StringComparison.Ordinal)), UpdatePlatform.Android);
            check(cut.Build == 169 && cut.Size == 0, "updater: a truncated response still finds the asset names (size unknown)");
        }

        string Rel(params (string name, long size)[] assets) =>
            "{\"tag_name\":\"unity-latest\",\"body\":\"caf\\u00e9 \\\"q\\\"\",\"assets\":[" + string.Join(",", assets.Select(x => $"{{\"name\":\"{x.name}\",\"size\":{x.size},\"uploader\":{{\"login\":\"bot\"}}}}")) + "]}";
        var multi = UpdateCheck.Parse(Rel(("Sigilbound-Unity-0.20.0-build23.apk", 5_000_000), ("Sigilbound-Unity-0.21.1-build170.apk", 7_000_000), ("Sigilbound-Unity-0.21.0-build99.apk", 6_000_000)), UpdatePlatform.Android);
        check(multi.Build == 170 && multi.Size == 7_000_000, "updater: the highest build wins whatever the order");
        check(!UpdateCheck.Parse(Rel(("Sigilbound-Windows-0.21.1-build170.zip", 9_000_000)), UpdatePlatform.Android).Found, "updater: a release without this platform's asset offers nothing");
        check(!UpdateCheck.Parse(Rel(("Sigilbound-Unity.apk", 9_000_000), ("Sigilbound-Unity-0.21.1-build170.apk.part", 1)), UpdatePlatform.Android).Found, "updater: unversioned or odd names are ignored");
        bool none = true;
        foreach (var bad in new[] { null, "", "<!DOCTYPE html><html><body>404 Not Found</body></html>", "{\"message\":\"Not Found\",\"documentation_url\":\"x\"}", "{\"message\":\"API rate limit exceeded\"}", "\u0000\u0001garbage" })
        {
            try { if (UpdateCheck.Parse(bad, UpdatePlatform.Android).Found) none = false; } catch (Exception) { none = false; }
        }
        check(none, "updater: error pages, rate limits and garbage offer nothing and never throw");

        check(UpdateCheck.LooksLikePackage(7_000_000, 'P', 'K', 7_000_000), "updater: a zip of the listed size is installed");
        check(!UpdateCheck.LooksLikePackage(3_500_000, 'P', 'K', 7_000_000), "updater: a cut-off download (wrong size) is not installed");
        check(!UpdateCheck.LooksLikePackage(7_000_000, '<', '!', 7_000_000) && !UpdateCheck.LooksLikePackage(9_000, '<', '!'), "updater: an HTML error page is not installed");
        check(!UpdateCheck.LooksLikePackage(500_000, 'P', 'K') && UpdateCheck.LooksLikePackage(7_000_000, 'P', 'K'), "updater: with no listed size, a zip over 1 MB is accepted and a tiny one is not");

        // Play Store build: no updater.
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "updater: sources not found here (skipped store checks)"); return; }
        string Src(string rel) { var p = Path.Combine(root, "Assets", "Sigilbound", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        var upd = Src("Client/Updater.cs");
        var directives = upd.Split('\n').Select(l => l.Trim()).Where(l => l.StartsWith("#")).ToList();
        var codeBefore = upd.Substring(0, Math.Max(0, upd.IndexOf("#if !SB_STORE", StringComparison.Ordinal))).Split('\n').Where(l => !l.TrimStart().StartsWith("//") && l.Trim().Length > 0).ToList();
        check(directives.Count > 1 && directives[0] == "#if !SB_STORE" && directives[directives.Count - 1] == "#endif" && codeBefore.Count == 0 && upd.TrimEnd().EndsWith("#endif"),
            "updater: Updater.cs is entirely inside #if !SB_STORE (the store build doesn't compile it)");
        var store = Src("Client/UpdaterStore.cs");
        string[] installWords = { "AndroidJavaClass", "AndroidJavaObject", "UnityWebRequest", "PackageInstaller", "OpenURL", "powershell", "Process.Start", "install(" };
        check(store.Contains("#if SB_STORE") && !installWords.Any(x => store.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0), "updater: the store stand-in has no download or install code");
        var build = Src("Editor/BuildScript.cs");
        int storeAt = build.IndexOf("public static void AndroidStore()", StringComparison.Ordinal);
        var storeBody = storeAt < 0 ? "" : build.Substring(storeAt, Math.Max(0, build.IndexOf("static string[] SetUpdaterPlugins", storeAt, StringComparison.Ordinal) - storeAt));
        check(storeBody.Contains("SB_STORE") && storeBody.Contains("IsStoreBuild = true") && storeBody.Contains("SetUpdaterPlugins(false)") && storeBody.Contains("buildAppBundle = true"),
            "updater: the store build sets SB_STORE and leaves the updater's Android plugins out");
        var perm = Src("Editor/AndroidUpdatePermission.cs");
        int skip = perm.IndexOf("if (BuildScript.IsStoreBuild)", StringComparison.Ordinal), adds = perm.IndexOf("CreateElement(\"uses-permission\")", StringComparison.Ordinal);
        check(skip > 0 && adds > skip && perm.Substring(skip, 200).Contains("return;"), "updater: the store build's manifest gets no install permission or APK provider");
        var plugins = Path.Combine(root, "Assets", "Plugins");
        var staticPerm = Directory.Exists(plugins) ? Directory.GetFiles(plugins, "*.xml", SearchOption.AllDirectories).Where(f => File.ReadAllText(f).Contains("REQUEST_INSTALL_PACKAGES")).ToList() : new System.Collections.Generic.List<string>();
        check(staticPerm.Count == 0, "updater: no plugin manifest declares the install permission on its own (only the test-build post-processor adds it)");
    }
}
