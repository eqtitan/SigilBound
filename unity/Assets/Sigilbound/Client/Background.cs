// Desktop focus behaviour (owner request 2026-11-28): on Windows the game used to freeze whenever its window lost
// focus (Unity's runInBackground off). Now a Settings choice, saved on this device:
//   sb.bgRun  1 = keep running (default on desktop) | 0 = pause when the window isn't focused
//   sb.bgMute 1 = mute while the window isn't focused (default) | 0 = keep the sound playing
// Phones are unaffected: Android/iOS always pause apps in the background.
using UnityEngine;

namespace Sigilbound.Client
{
    public static class Background
    {
        public static bool Supported => !Application.isMobilePlatform;

        public static bool KeepRunning
        {
            get => PlayerPrefs.GetInt("sb.bgRun", 1) == 1;
            set { PlayerPrefs.SetInt("sb.bgRun", value ? 1 : 0); PlayerPrefs.Save(); Apply(); }
        }

        public static bool MuteWhenUnfocused
        {
            get => PlayerPrefs.GetInt("sb.bgMute", 1) == 1;
            set { PlayerPrefs.SetInt("sb.bgMute", value ? 1 : 0); PlayerPrefs.Save(); OnFocus(Application.isFocused); }
        }

        public static void Apply()
        {
            if (!Supported) return;
            Application.runInBackground = KeepRunning;
        }

        // Called from App.OnApplicationFocus. Only matters when the game keeps running unfocused.
        public static void OnFocus(bool focused)
        {
            if (!Supported) return;
            AudioListener.pause = !focused && KeepRunning && MuteWhenUnfocused;
        }
    }
}
