// Frame-rate rules (owner decision 2026-11-27, PMO D2; Client/Quality.cs applies them). Unity-free so
// Tests~ can check them.
using System;

namespace Sigilbound.Core
{
    public static class FrameRate
    {
        // Saved choice: "60" (default), "high" (the screen's 90/120 Hz) or "30". Anything else, including
        // the old "auto" (match the screen, the default until 2026-11-27), reads as 60.
        public static string Mode(string saved) => saved == "30" || saved == "high" ? saved : "60";

        // The "high" choice: the screen's rate up to 120 (90 on a 90 Hz panel).
        public static int HighHz(int displayHz) => Math.Min(displayHz, 120);

        // Low tier, a warm phone and auto-replay force 30; otherwise the choice. loweredHz: "high" stepped
        // down to 60 because the phone couldn't hold the screen rate (0 = not lowered).
        public static int Target(string mode, int displayHz, int tier, bool thermal, bool replay, int loweredHz = 0) =>
            tier == 0 || thermal || replay || mode == "30" ? 30
            : mode == "high" ? (loweredHz > 0 ? loweredHz : HighHz(displayHz))
            : Math.Min(60, displayHz);
    }
}
