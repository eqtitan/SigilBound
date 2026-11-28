// Command-line builds (used by CI through game-ci). Example:
//   Unity -batchmode -quit -projectPath unity -executeMethod Sigilbound.EditorTools.BuildScript.Android
// Environment (all optional): SB_VERSION (e.g. 0.19.2), SB_BUILD (versionCode, the CI run number),
// SB_OUTPUT (APK path; else game-ci's -customBuildPath; else Builds/Android/Sigilbound-Unity.apk).
using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Sigilbound.EditorTools
{
    public static class BuildScript
    {
        // The same permanent test key as the Capacitor app (mobile/android/app), so every Unity
        // test build installs over the previous one and keeps progress. Not a release key.
        const string TestKeystore = "../mobile/android/app/sigilbound-test.keystore";
        const string TestKeyPass = "sigilbound-test", TestKeyAlias = "sigilbound-test";

        public static void Android()
        {
            ProjectSetup.Apply();
            EditorUserBuildSettings.buildAppBundle = false; // an APK that installs straight onto a phone
            PlayerSettings.bundleVersion = Version();
            int code = BuildNumber();
            if (code > 0) PlayerSettings.Android.bundleVersionCode = code;
            WriteBuildInfo(code);
            var keystore = Path.GetFullPath(TestKeystore);
            if (File.Exists(keystore))
            {
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = keystore;
                PlayerSettings.Android.keystorePass = TestKeyPass;
                PlayerSettings.Android.keyaliasName = TestKeyAlias;
                PlayerSettings.Android.keyaliasPass = TestKeyPass;
            }
            else Debug.LogWarning($"Sigilbound: test keystore not found at {keystore}; using Unity's debug key (updates won't install over each other).");
            // SB_OUTPUT wins; otherwise game-ci's -customBuildPath (it checks that exact path for the
            // APK after the build and fails the job if it's empty); otherwise the local default.
            var output = Environment.GetEnvironmentVariable("SB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Arg("-customBuildPath");
            Build(BuildTarget.Android, string.IsNullOrEmpty(output) ? "Builds/Android/Sigilbound-Unity.apk" : output);
        }

        // Google Play build (docs/sigilbound/store/PLAY_CONSOLE.md): an AAB signed with the private upload key,
        // with the in-app updater left out (define SB_STORE: Updater.Boot returns at once and the manifest gets
        // no install permission or APK provider). It never uses the committed test key: the upload key comes
        // from ANDROID_UPLOAD_KEYSTORE (a file path), ANDROID_UPLOAD_KEYSTORE_PASS, ANDROID_UPLOAD_KEY_ALIAS and
        // ANDROID_UPLOAD_KEY_PASS, or the build stops. Friends and chat are left out too for v1 (SB_STORE:
        // Online.cs and FriendsScreens.cs aren't compiled, no Friends tab, no calls to functions/api/sb).
        public static bool IsStoreBuild;
        // The Play Store package name (owner decision 2026-11-28, permanent once uploaded). Only this build uses
        // it; the tester APK keeps ProjectSetup.AppId (com.sigilbound.unity) so testers' installs and saves stay.
        public const string StoreAppId = "com.sigilbound.game";
        public const string StoreAppName = "Sigilbound"; // the launcher name (LISTING.md); testers see "Sigilbound (Unity)"
        public static void AndroidStore()
        {
            // Our names first, then game-ci's (its androidKeystore* inputs decode the keystore into the project).
            string Env(params string[] ks) { foreach (var k in ks) { var v = Environment.GetEnvironmentVariable(k); if (!string.IsNullOrEmpty(v)) return v; } return null; }
            var ks = Env("ANDROID_UPLOAD_KEYSTORE", "ANDROID_KEYSTORE_NAME");
            if (!string.IsNullOrEmpty(ks) && !File.Exists(ks) && File.Exists(Path.GetFullPath(ks))) ks = Path.GetFullPath(ks);
            var ksPass = Env("ANDROID_UPLOAD_KEYSTORE_PASS", "ANDROID_KEYSTORE_PASS"); var alias = Env("ANDROID_UPLOAD_KEY_ALIAS", "ANDROID_KEYALIAS_NAME");
            var aliasPass = Env("ANDROID_UPLOAD_KEY_PASS", "ANDROID_KEYALIAS_PASS") ?? ksPass;
            if (string.IsNullOrEmpty(ks) || !File.Exists(ks) || string.IsNullOrEmpty(ksPass) || string.IsNullOrEmpty(alias))
            { Debug.LogError("Sigilbound: store build needs the upload key (ANDROID_UPLOAD_KEYSTORE/_PASS, ANDROID_UPLOAD_KEY_ALIAS/_PASS, or game-ci's ANDROID_KEYSTORE_*)"); EditorApplication.Exit(1); return; }
            ProjectSetup.Apply();
            IsStoreBuild = true;
            var target = UnityEditor.Build.NamedBuildTarget.Android;
            var defines = GetDefines(target);
            if (!(";" + defines + ";").Contains(";SB_STORE;")) SetDefines(target, string.IsNullOrEmpty(defines) ? "SB_STORE" : defines + ";SB_STORE");
            PlayerSettings.SetApplicationIdentifier(target, StoreAppId);
            PlayerSettings.productName = StoreAppName;
            EditorUserBuildSettings.buildAppBundle = true;
            PlayerSettings.bundleVersion = Version();
            int code = BuildNumber();
            if (code > 0) PlayerSettings.Android.bundleVersionCode = code;
            WriteBuildInfo(code);
            PlayerSettings.Android.useCustomKeystore = true;
            PlayerSettings.Android.keystoreName = Path.GetFullPath(ks);
            PlayerSettings.Android.keystorePass = ksPass;
            PlayerSettings.Android.keyaliasName = alias;
            PlayerSettings.Android.keyaliasPass = aliasPass;
            var output = Environment.GetEnvironmentVariable("SB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Arg("-customBuildPath");
            var updaterJava = SetUpdaterPlugins(false); // the updater's Java (PackageInstaller, APK provider) stays out too
            try { Build(BuildTarget.Android, string.IsNullOrEmpty(output) ? "Builds/Android/Sigilbound.aab" : output); }
            finally // local editors keep their own defines and the tester app id
            {
                SetDefines(target, defines); IsStoreBuild = false; SetUpdaterPlugins(true, updaterJava);
                PlayerSettings.SetApplicationIdentifier(target, ProjectSetup.AppId); PlayerSettings.productName = ProjectSetup.AppName;
            }
        }

        // The in-app updater's Android plugins (Plugins/Android). Store builds leave them out of the AAB; the
        // test APK keeps them. Returns the plugins that were changed, so only those are switched back.
        public static readonly string[] UpdaterPlugins = { "Assets/Plugins/Android/SigilboundUpdateInstaller.java", "Assets/Plugins/Android/SigilboundApkProvider.java" };
        static string[] SetUpdaterPlugins(bool on, string[] only = null)
        {
            var changed = new System.Collections.Generic.List<string>();
            foreach (var p in only ?? UpdaterPlugins)
            {
                if (!(AssetImporter.GetAtPath(p) is PluginImporter pi) || pi.GetCompatibleWithPlatform(BuildTarget.Android) == on) continue;
                pi.SetCompatibleWithPlatform(BuildTarget.Android, on);
                pi.SaveAndReimport();
                changed.Add(p);
            }
            if (!on) Debug.Log($"Sigilbound: store build, updater plugins left out ({changed.Count})");
            return changed.ToArray();
        }
#if UNITY_2022_2_OR_NEWER
        static string GetDefines(UnityEditor.Build.NamedBuildTarget t) => PlayerSettings.GetScriptingDefineSymbols(t);
        static void SetDefines(UnityEditor.Build.NamedBuildTarget t, string d) => PlayerSettings.SetScriptingDefineSymbols(t, d);
#else
        static string GetDefines(UnityEditor.Build.NamedBuildTarget t) => "";   // the stub compile check only; real builds use Unity 6
        static void SetDefines(UnityEditor.Build.NamedBuildTarget t, string d) { }
#endif

        // Windows 10+ test build (64-bit). Built on the Linux CI runner, which can only make Mono
        // Windows players (IL2CPP for Windows needs a Windows machine); fine for testing.
        // Opens as a 1600x900 landscape window you can resize; the UI scales to the window's height.
        public static void Windows()
        {
            ProjectSetup.Apply();
            PlayerSettings.bundleVersion = Version();
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
            WriteBuildInfo(BuildNumber());
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            var output = Environment.GetEnvironmentVariable("SB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Arg("-customBuildPath");
            if (string.IsNullOrEmpty(output)) output = "Builds/Windows/Sigilbound.exe";
            if (!output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "Sigilbound.exe");
            Build(BuildTarget.StandaloneWindows64, output);
        }

        // Linux preview build (64-bit, Mono) for the screenshot mode: agents run it under xvfb to see
        // the game (tools/unity-shots.sh, -sbShots=DIR in Client/ShotsMode.cs). Not for players.
        public static void Linux()
        {
            ProjectSetup.Apply();
            PlayerSettings.bundleVersion = Version();
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 1600;
            PlayerSettings.defaultScreenHeight = 900;
            PlayerSettings.resizableWindow = true;
            WriteBuildInfo(0); // no update check in the preview
            var output = Environment.GetEnvironmentVariable("SB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Arg("-customBuildPath");
            if (string.IsNullOrEmpty(output)) output = "Builds/Linux/Sigilbound.x86_64";
            if (!output.EndsWith(".x86_64", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "Sigilbound.x86_64");
            Build(BuildTarget.StandaloneLinux64, output);
        }

        public static void iOS() { ProjectSetup.Apply(); PlayerSettings.bundleVersion = Version(); Build(BuildTarget.iOS, "Builds/iOS"); }

        // The build number the in-game update check (Client/Updater.cs) compares against the
        // unity-latest release. 0 (local builds) turns the check off.
        static void WriteBuildInfo(int build)
        {
            Directory.CreateDirectory("Assets/Resources");
            File.WriteAllText("Assets/Resources/BuildInfo.txt", Math.Max(0, build).ToString());
            AssetDatabase.ImportAsset("Assets/Resources/BuildInfo.txt", ImportAssetOptions.ForceSynchronousImport);
        }

        static string Arg(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == name && !string.IsNullOrEmpty(args[i + 1]) && !args[i + 1].StartsWith("-")) return args[i + 1];
            return null;
        }

        // Android versionCode (must always go up so updates install): SB_BUILD, else game-ci's
        // -androidVersionCode argument, else unity/build-number.txt written by CI.
        static int BuildNumber()
        {
            if (int.TryParse(Environment.GetEnvironmentVariable("SB_BUILD"), out var n) && n > 0) return n;
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-androidVersionCode" && int.TryParse(args[i + 1], out n) && n > 0) return n;
            try { if (int.TryParse(File.ReadAllText("build-number.txt").Trim(), out n) && n > 0) return n; } catch (IOException) { }
            return 0;
        }

        // The web game's version (public/game/version.js) unless SB_VERSION overrides it.
        static string Version()
        {
            var v = Environment.GetEnvironmentVariable("SB_VERSION");
            if (!string.IsNullOrEmpty(v)) return v;
            try
            {
                var m = Regex.Match(File.ReadAllText("../public/game/version.js"), "version:\\s*\"([^\"]+)\"");
                if (m.Success) return m.Groups[1].Value;
            }
            catch (IOException) { }
            return "0.0.0";
        }

        // Android target API (ProjectSetup pins it; the workflow passes the same level to game-ci).
        public const int TargetSdk = 36;

        // Errors logged during the build are also written here (unity/Builds/build-errors.txt), so the workflow
        // can tell a Maven Central rate limit (HTTP 429) from a real failure and retry only the former.
        public const string ErrorLog = "Builds/build-errors.txt";

        static void Build(BuildTarget target, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var errors = new System.Text.StringBuilder();
            Application.LogCallback grab = (msg, stack, type) =>
            {
                if ((type == LogType.Error || type == LogType.Exception) && errors.Length < 400000) errors.AppendLine(msg);
            };
            try { Directory.CreateDirectory("Builds"); File.Delete(ErrorLog); } catch (IOException) { }
            Application.logMessageReceived += grab;
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ProjectSetup.ScenePath }, locationPathName = path, target = target, options = BuildOptions.None,
                });
            }
            finally { Application.logMessageReceived -= grab; }
            if (report.summary.result != BuildResult.Succeeded)
            {
                foreach (var st in report.steps)
                    foreach (var m in st.messages)
                        if (m.type == LogType.Error || m.type == LogType.Exception) errors.AppendLine(m.content);
                try { File.WriteAllText(ErrorLog, errors.ToString()); } catch (IOException) { }
            }
            var sum = report.summary;
            Debug.Log($"Build {sum.result}: {sum.totalSize / (1024 * 1024)} MB uncompressed at {path}");
            // game-ci validates the log after the build: it needs the literal "Build succeeded!" (what its
            // own default build method prints) or a "# Build results #" block with Errors: 0, else it
            // fails the job even when the player was built fine. Print both, in its format.
            Debug.Log("###########################\n#      Build results      #\n###########################\n" +
                      $"Duration: {sum.totalTime}\nWarnings: {sum.totalWarnings}\nErrors: {(sum.result == BuildResult.Succeeded ? 0 : Math.Max(1, sum.totalErrors))}\nSize: {sum.totalSize} bytes");
            if (sum.result == BuildResult.Succeeded) Debug.Log("Build succeeded!");
            else EditorApplication.Exit(1);
        }
    }
}
