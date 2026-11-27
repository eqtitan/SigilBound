// Play Store builds (define SB_STORE, BuildScript.AndroidStore): Play updates the app and doesn't allow
// apps to install their own updates, so Updater.cs is not compiled at all. This stand-in keeps the
// build number for Settings and feedback reports and has no download or install code.
#if SB_STORE
using UnityEngine;

namespace Sigilbound.Client
{
    public static class Updater
    {
        public const bool Included = false; // Settings hides the update options
        static int _build = -1;
        public static int MyBuild
        {
            get
            {
                if (_build < 0) { var info = Resources.Load<TextAsset>("BuildInfo"); _build = info != null && int.TryParse(info.text.Trim(), out var n) && n > 0 ? n : 0; }
                return _build;
            }
        }
        public static int LatestBuild => 0;
        public static string LatestVersion => "";
        public static bool AutoUpdate { get => false; set { } }
    }
}
#endif
