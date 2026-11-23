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

        // Windows 10+ test build (64-bit). Built on the Linux CI runner, which can only make Mono
        // Windows players (IL2CPP for Windows needs a Windows machine); fine for testing.
        // Runs as a phone-shaped window you can resize; the UI scales to the window's width.
        public static void Windows()
        {
            ProjectSetup.Apply();
            PlayerSettings.bundleVersion = Version();
            var target = UnityEditor.Build.NamedBuildTarget.Standalone;
            PlayerSettings.SetScriptingBackend(target, ScriptingImplementation.Mono2x);
            WriteBuildInfo(BuildNumber());
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.defaultScreenWidth = 540;
            PlayerSettings.defaultScreenHeight = 960;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = false;
            var output = Environment.GetEnvironmentVariable("SB_OUTPUT");
            if (string.IsNullOrEmpty(output)) output = Arg("-customBuildPath");
            if (string.IsNullOrEmpty(output)) output = "Builds/Windows/Sigilbound.exe";
            if (!output.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) output = Path.Combine(output, "Sigilbound.exe");
            Build(BuildTarget.StandaloneWindows64, output);
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

        static void Build(BuildTarget target, string path)
        {
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ProjectSetup.ScenePath }, locationPathName = path, target = target, options = BuildOptions.None,
            });
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
