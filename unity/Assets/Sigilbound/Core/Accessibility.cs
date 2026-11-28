// Accessibility options (PLAN step 115; review items P2-17 and P3-4, UX review #12 and #16, art review L2).
// The numbers and colours live here, Unity-free, so Tests~ can check them: the client keeps the player's
// choices on the device (PlayerPrefs, like the frame rate and screen shake) and reads these tables.
//
//  - Text size: Small / Default / Large / Extra large, one global scale applied by Ui's text helpers, with a
//    minimum readable size. Text that no longer fits its rect at a bigger setting shrinks back toward its
//    Default size (never below it), so a fixed layout never overflows more than it does at Default.
//  - Reduce motion: no camera shake, no cinematic camera swings or zoom punches (the shot holds still, with a
//    gentle cut in and out), no breathing tilt. Menu idle motion stays.
//  - Flash effects: On / Reduced / Off caps full-screen flashes, sky lightning and bright additive bursts.
//  - Colour-blind palette: Default / Deuteranopia-Protanopia / Tritanopia element and rarity colours, chosen
//    by CIEDE2000 distance after a Machado (2009) simulation of each type. Icons and rarity letters stay.
//  - Hold to confirm: how long the red hold buttons (Release, Rank Up with Epic+) take.
//  - Larger tap targets: Ui.Tap 56 dp instead of 48.
using System;

namespace Sigilbound.Core
{
    public static class Accessibility
    {
        // ---------- text size ----------
        public static readonly string[] TextSizeNames = { "Small", "Default", "Large", "Extra large" };
        public static readonly float[] TextFactors = { 0.9f, 1f, 1.15f, 1.3f };
        public const int DefaultTextSize = 1;
        // The smallest size any text is drawn at (UX review #12: secondary text floor). About 9.3 sp at 390 wide.
        public const int MinReadable = 10;

        public static int ClampLevel(int level) => Math.Max(0, Math.Min(TextFactors.Length - 1, level));
        public static float Factor(int level) => TextFactors[ClampLevel(level)];
        // The size text written at `size` (the Default-setting size in code) is drawn at for this setting.
        public static int TextSize(int size, int level) => Math.Max(MinReadable, (int)Math.Round(size * Factor(level), MidpointRounding.AwayFromZero));
        // Shrink-to-fit never goes below this: the Default-setting size (floored). A bigger setting therefore
        // never makes text smaller than Default, and anything that fits at Default fits at every setting.
        public static int FitFloor(int size, int level) => Math.Min(TextSize(size, level), TextSize(size, DefaultTextSize));
        // The largest size from TextSize down to FitFloor at which `fits` says the text fits (FitFloor if none).
        public static int Fit(int size, int level, Func<int, bool> fits)
        {
            int top = TextSize(size, level), floor = FitFloor(size, level);
            for (int px = top; px > floor; px--) if (fits(px)) return px;
            return floor;
        }
        // A rough single-line width for layout checks without a font (Tests~): average advance per character,
        // wider for the bold Cinzel display face (small caps; measured: about 0.56 em mixed case) than for the sans face.
        public static double ApproxWidth(string text, int px, bool serif)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            double w = 0; bool tag = false;
            foreach (char c in text)
            {
                if (c == '<') tag = true;
                if (!tag) w += c == ' ' ? 0.28 : char.IsDigit(c) ? 0.56 : char.IsUpper(c) ? (serif ? 0.76 : 0.66) : c == 'i' || c == 'l' || c == '.' || c == ',' || c == '\'' ? 0.26 : (serif ? 0.6 : 0.52);
                if (c == '>') tag = false;
            }
            return w * px;
        }

        // ---------- reduce motion / flashes ----------
        public static readonly string[] FlashNames = { "On", "Reduced", "Off" };
        public static readonly string[] FlashKeys = { "on", "reduced", "off" };
        public static int FlashLevel(string key) { int i = Array.IndexOf(FlashKeys, key); return i < 0 ? 0 : i; }
        // How strong a full-screen flash or sky lightning is (1 = as designed).
        public static float FlashScale(int level) => level == 2 ? 0f : level == 1 ? 0.3f : 1f;
        // The brightest an additive spark or line may be (HDR multiplier; bloom picks up anything above ~1).
        public static float BurstCap(int level) => level == 2 ? 0.6f : level == 1 ? 0.9f : float.MaxValue;
        // Lightning strikes flicker (re-strike) this many times; one steady strike when flashes are limited.
        public static int BoltFlicks(int level) => level == 0 ? 3 : 1;
        // Settings > Screen shake, folded into Reduce motion: with Reduce motion on the camera never shakes.
        public static string EffectiveShake(string saved, bool reduceMotion) =>
            reduceMotion ? "off" : saved == "reduced" || saved == "off" ? saved : "full";

        // ---------- hold to confirm ----------
        public static readonly string[] HoldNames = { "Short", "Default", "Long" };
        public static readonly float[] HoldSeconds = { 0.6f, 1f, 1.8f };
        public static float Hold(int level) => HoldSeconds[Math.Max(0, Math.Min(HoldSeconds.Length - 1, level))];

        // ---------- larger tap targets ----------
        public static float TapDp(bool large) => large ? 56f : 48f;

        // ---------- colour-blind palettes ----------
        public static readonly string[] PaletteNames = { "Default", "Deuteranopia / Protanopia", "Tritanopia" };
        public static readonly string[] PaletteShort = { "Default", "Red-green", "Blue-yellow" };
        public static readonly string[] PaletteKeys = { "default", "deutan", "tritan" };
        public static int PaletteMode(string key) { int i = Array.IndexOf(PaletteKeys, key); return i < 0 ? 0 : i; }

        // Element colours by mode, in Element order (Flame, Gale, Tide, Light, Shadow). Mode 0 is the game's
        // own (Palette.cs). Red-green: vermilion, sage, sky, lemon, plum (lightness and blue-yellow carry
        // the differences). Blue-yellow: red, green, cyan, cream, orchid (the red-green axis carries them).
        public static readonly string[][] ElementHex =
        {
            new[] { "#e8553a", "#3fb57a", "#3a8ee8", "#e8c23a", "#9a5ae8" },
            new[] { "#d95316", "#6fab95", "#65bdfc", "#f0e660", "#994c93" },
            new[] { "#ed181b", "#469936", "#18e0e8", "#faeaaf", "#be76c4" },
        };
        // Rarity colours by mode (Common, Rare, Epic, Legendary).
        public static readonly string[][] RarityHex =
        {
            new[] { "#9aa3ad", "#4aa3ff", "#b46cff", "#ffb02e" },
            new[] { "#a8adb3", "#4a62d8", "#e08ad8", "#ffb02e" },
            new[] { "#c4c8cc", "#5a7ae8", "#d0407a", "#f0a224" },
        };
        // Gear rarity 1..5 by mode (gear has an extra green "uncommon" tier).
        public static readonly string[][] GearHex =
        {
            new[] { "#9aa3ad", "#5bc46a", "#4aa3ff", "#b46cff", "#ffb02e" },
            new[] { "#a8adb3", "#e8e4d0", "#4a62d8", "#e08ad8", "#ffb02e" },
            new[] { "#c4c8cc", "#5bc46a", "#5a7ae8", "#d0407a", "#f0a224" },
        };
        // Good / bad (allies, gains vs enemies, losses): green vs red, or blue vs orange for red-green.
        public static readonly string[][] GoodBadHex =
        {
            new[] { "#5bd17a", "#ff5a5a" },
            new[] { "#4ab0ff", "#ff8a1a" },
            new[] { "#5bd17a", "#ff5a5a" },
        };
        static int M(int mode) => Math.Max(0, Math.Min(ElementHex.Length - 1, mode));
        public static string ElementColour(int mode, Element e) => ElementHex[M(mode)][(int)e];
        public static string RarityColour(int mode, Rarity r) => RarityHex[M(mode)][(int)r];
        public static string GearColour(int mode, int rarity) => GearHex[M(mode)][Math.Max(1, Math.Min(5, rarity)) - 1];
    }
}
