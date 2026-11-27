// The in-app updater's decisions (Client/Updater.cs), Unity-free so Tests~ can check them (QA review
// 2026-11-27, P0-4): which release asset is the newest build for this platform, whether it is newer than
// the running build, and whether a downloaded file is a real package. The updater is left out of Play
// Store builds (define SB_STORE): Client/Updater.cs compiles only without it.
using System.Text.RegularExpressions;

namespace Sigilbound.Core
{
    public enum UpdatePlatform { Android, Windows }

    public static class UpdateCheck
    {
        public struct Latest
        {
            public int Build; public string Version, File; public long Size; // Size 0 = unknown
            public bool Found => Build > 0;
        }

        // The versioned asset names CI publishes on the "unity-latest" release.
        public static string NamePattern(UpdatePlatform p) => p == UpdatePlatform.Android
            ? @"^Sigilbound-Unity-([0-9.]+)-build(\d+)\.apk$"
            : @"^Sigilbound-Windows-([0-9.]+)-build(\d+)\.zip$";

        // The highest build number among this platform's assets in the GitHub release JSON, with its size.
        // Not found (Build 0) for an error page, a release without this platform's asset, or garbage.
        public static Latest Parse(string releaseJson, UpdatePlatform p)
        {
            var best = new Latest { Version = "", File = null };
            if (string.IsNullOrEmpty(releaseJson)) return best;
            object root = null;
            try { root = Json.Parse(releaseJson); } catch (System.Exception) { }
            var assets = Json.Arr(root, "assets");
            var re = new Regex(NamePattern(p));
            if (assets == null)
            {
                // Not parseable as a release (or a JSON shape we don't expect): scan the asset names, as the
                // updater always did, so a parser gap can never strand testers on an old build.
                if (root != null) return best;
                foreach (Match m in Regex.Matches(releaseJson, @"""name""\s*:\s*""([^""]+)"""))
                {
                    var n = re.Match(m.Groups[1].Value);
                    if (n.Success && int.TryParse(n.Groups[2].Value, out var nb) && nb > best.Build) best = new Latest { Build = nb, Version = n.Groups[1].Value, File = n.Value };
                }
                return best;
            }
            foreach (var a in assets)
            {
                var m = re.Match(Json.Str(a, "name"));
                if (m.Success && int.TryParse(m.Groups[2].Value, out var b) && b > best.Build)
                    best = new Latest { Build = b, Version = m.Groups[1].Value, File = m.Value, Size = Json.Long(a, "size") };
            }
            return best;
        }

        // Offer an update only for a strictly newer build, and never when this build has no number
        // (local and editor builds).
        public static bool IsNewer(Latest latest, int myBuild) => myBuild > 0 && latest.Found && latest.Build > myBuild;

        // A downloaded package is installed only if it is a zip ("PK") of a plausible size and, when the
        // release said how big it is, exactly that size: an HTML error page or a cut-off download is not.
        public const long MinPackageBytes = 1_000_000;
        public static bool LooksLikePackage(long length, int byte0, int byte1, long expectedSize = 0) =>
            length > MinPackageBytes && byte0 == 'P' && byte1 == 'K' && (expectedSize <= 0 || length == expectedSize);
    }
}
