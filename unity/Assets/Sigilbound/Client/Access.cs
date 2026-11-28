// Settings > Accessibility (PLAN step 115): the player's choices, kept on this device (PlayerPrefs, like the
// frame rate and screen shake; nothing goes in the save, so old saves load unchanged). The tables and rules
// are in Core/Accessibility.cs. Values are read once and cached: the draw code asks every frame.
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Access
    {
        const string KText = "sb.textSize", KMotion = "sb.reduceMotion", KFlash = "sb.flash", KPalette = "sb.palette", KHold = "sb.hold", KTaps = "sb.bigTaps";
        static bool _loaded;
        static int _text = Accessibility.DefaultTextSize, _flash, _palette, _hold = 1;
        static bool _motion, _taps;

        static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            _text = Accessibility.ClampLevel(PlayerPrefs.GetInt(KText, Accessibility.DefaultTextSize));
            // Respect the phone's own setting where Unity exposes one: no API for it, so Reduce motion starts off.
            _motion = PlayerPrefs.GetInt(KMotion, 0) == 1;
            _flash = Accessibility.FlashLevel(PlayerPrefs.GetString(KFlash, "on"));
            _palette = Accessibility.PaletteMode(PlayerPrefs.GetString(KPalette, "default"));
            _hold = Mathf.Clamp(PlayerPrefs.GetInt(KHold, 1), 0, Accessibility.HoldSeconds.Length - 1);
            _taps = PlayerPrefs.GetInt(KTaps, 0) == 1;
        }
        // Read the preferences on the main thread at start-up (PlayerPrefs is main-thread only; the getters are
        // also called from draw code and colour lookups).
        public static void Warm() => Load();
        static void Saved() => PlayerPrefs.Save();

        // Text size level (0 Small, 1 Default, 2 Large, 3 Extra large). Changing it rebuilds the cached styles.
        public static int TextLevel
        {
            get { Load(); return _text; }
            set { Load(); value = Accessibility.ClampLevel(value); if (value == _text) return; _text = value; PlayerPrefs.SetInt(KText, value); Saved(); Ui.ClearStyles(); }
        }
        public static float TextFactor => Accessibility.Factor(TextLevel);

        public static bool ReduceMotion
        {
            get { Load(); return _motion; }
            set { Load(); _motion = value; PlayerPrefs.SetInt(KMotion, value ? 1 : 0); Saved(); }
        }
        // Settings > Screen shake, folded in: Reduce motion turns it off.
        public static string Shake => Accessibility.EffectiveShake(Quality.SavedShake, ReduceMotion);

        public static int Flash
        {
            get { Load(); return _flash; }
            set { Load(); _flash = Mathf.Clamp(value, 0, 2); PlayerPrefs.SetString(KFlash, Accessibility.FlashKeys[_flash]); Saved(); }
        }
        public static float FlashScale => Accessibility.FlashScale(Flash);
        public static float BurstCap => Accessibility.BurstCap(Flash);

        public static int ColourMode
        {
            get { Load(); return _palette; }
            set { Load(); value = Mathf.Clamp(value, 0, Accessibility.PaletteKeys.Length - 1); if (value == _palette) return; _palette = value; PlayerPrefs.SetString(KPalette, Accessibility.PaletteKeys[value]); Saved(); Ui.ClearStyles(); }
        }

        public static int HoldLevel
        {
            get { Load(); return _hold; }
            set { Load(); _hold = Mathf.Clamp(value, 0, Accessibility.HoldSeconds.Length - 1); PlayerPrefs.SetInt(KHold, _hold); Saved(); }
        }
        public static float HoldSeconds => Accessibility.Hold(HoldLevel);

        public static bool LargeTaps
        {
            get { Load(); return _taps; }
            set { Load(); _taps = value; PlayerPrefs.SetInt(KTaps, value ? 1 : 0); Saved(); }
        }
    }
}
