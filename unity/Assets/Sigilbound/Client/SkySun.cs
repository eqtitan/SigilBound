// Real sun at my location (owner decision 2026-11-28): the clock and the place the arena's sky is computed for
// (Core/SolarClock). Saved on this device, never sent anywhere (no network call reads or writes any of this):
//   sb.realSun   1 = the real sun and moon light the arena (default) | 0 = every stage keeps its authored time of day
//   sb.sunLat / sb.sunLon / sb.sunSrc  the last coarse fix, rounded to one decimal (about 11 km), and where it came from
//   sb.locAsked  1 = the one-time location question was answered (either way)
// Location: Android asks once for ACCESS_COARSE_LOCATION at a calm moment (App.CheckPopups, after a few cleared stages,
// on Home), after an in-game line says why; then one low-accuracy fix from Input.location and the service stops.
// Declined, no fix, or Windows / Linux / the editor: an estimate from the time zone and the region (LocationGuess).
// The screenshot tour (-sbShots) uses SkyClock's fixed moment and place so its shots never change with the hour.
using System;
using System.Collections;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class SkySun
    {
        const string KOn = "sb.realSun", KLat = "sb.sunLat", KLon = "sb.sunLon", KSrc = "sb.sunSrc", KAsked = "sb.locAsked";
        public const string Why = "Lets the sky show the real sun where you are.";
        public const string Stays = "Your location stays on this device: it's rounded to about 10 km and never sent anywhere.";

        // Screenshot mode and its sky steps: a fixed moment and place (ShotsMode sets these; null = the device's own).
        public static DateTime? FixedUtc; public static double? FixedLat, FixedLon; public static string FixedName;

        public static bool Enabled
        {
            get => PlayerPrefs.GetInt(KOn, 1) == 1;
            set { PlayerPrefs.SetInt(KOn, value ? 1 : 0); PlayerPrefs.Save(); Refresh(); }
        }

        // Computes the sky the next arena is built under (Arena.Build calls this first).
        public static void Refresh()
        {
            if (FixedUtc.HasValue)
            {
                SkyClock.Current = SkyState.At(FixedUtc.Value, FixedLat ?? SkyClock.FixedLat, FixedLon ?? SkyClock.FixedLon, FixedName ?? "fixed");
                return;
            }
            if (App.NoSave || App.InShots) { SkyClock.Current = SkyClock.Fixed(); return; }   // the tour: never the hour it runs at
            if (!Enabled) { SkyClock.Current = null; return; }
            var (lat, lon, src) = Location();
            SkyClock.Current = SkyState.At(DateTime.UtcNow, lat, lon, src);
        }

        // The cached fix if there is one, else the time zone and region estimate.
        public static (double lat, double lon, string src) Location()
        {
            if (PlayerPrefs.HasKey(KLat) && PlayerPrefs.HasKey(KLon))
                return (PlayerPrefs.GetFloat(KLat), PlayerPrefs.GetFloat(KLon), PlayerPrefs.GetString(KSrc, "location"));
            return Estimate();
        }

        public static string SourceLabel()
        {
            if (!Enabled) return "off";
            return Location().src.StartsWith("location") ? "your location" : "your time zone";
        }

        static (double lat, double lon, string src) Estimate()
        {
            double std = (DateTime.Now - DateTime.UtcNow).TotalHours; string tz = null; bool jan = false, jul = false;
            try
            {
                var z = TimeZoneInfo.Local;
                std = z.BaseUtcOffset.TotalHours; tz = z.Id;
                int y = DateTime.UtcNow.Year;
                jan = z.IsDaylightSavingTime(new DateTime(y, 1, 15, 12, 0, 0)); jul = z.IsDaylightSavingTime(new DateTime(y, 7, 15, 12, 0, 0));
            }
            catch (Exception) { }
            var g = LocationGuess.Estimate(std, Region(), tz, jan, jul);
            return (LocationGuess.Round1(g.lat), LocationGuess.Round1(g.lon), g.how);
        }

        // The device's region: Android's locale country (CultureInfo is often invariant in the Android player), else .NET's.
        static string Region()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { using (var loc = new AndroidJavaClass("java.util.Locale")) using (var d = loc.CallStatic<AndroidJavaObject>("getDefault")) { var c = d.Call<string>("getCountry"); if (!string.IsNullOrEmpty(c)) return c; } }
            catch (Exception) { }
#endif
            try { return System.Globalization.RegionInfo.CurrentRegion.TwoLetterISORegionName; } catch (Exception) { return null; }
        }

        // ---------- the one-time question (Android) ----------
        public static bool CanAsk => Application.platform == RuntimePlatform.Android && !App.NoSave && !App.InShots;
        // Owed once, on Home, after the player has cleared a few stages (never at first launch).
        public static bool AskPending(int cleared) => CanAsk && Enabled && cleared >= 3 && PlayerPrefs.GetInt(KAsked, 0) == 0;
        public static void MarkAsked() { PlayerPrefs.SetInt(KAsked, 1); PlayerPrefs.Save(); }

        // Asks the system for coarse location (if not granted yet), takes one low-accuracy fix, stops the service,
        // caches it rounded. done(message) reports what happened, for a toast.
        public static IEnumerator RequestLocation(Action<string> done)
        {
            MarkAsked();
#if UNITY_ANDROID && !UNITY_EDITOR
            const string Coarse = "android.permission.ACCESS_COARSE_LOCATION";
            if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(Coarse))
            {
                bool answered = false;
                var cb = new UnityEngine.Android.PermissionCallbacks();
                cb.PermissionGranted += _ => answered = true;
                cb.PermissionDenied += _ => answered = true;
#pragma warning disable CS0618 // PermissionDeniedAndDontAskAgain: obsolete in newer Unity, still raised by some
                cb.PermissionDeniedAndDontAskAgain += _ => answered = true;
#pragma warning restore CS0618
                UnityEngine.Android.Permission.RequestUserPermission(Coarse, cb);
                float until = Time.realtimeSinceStartup + 60f;
                while (!answered && Time.realtimeSinceStartup < until) yield return null;
                if (!UnityEngine.Android.Permission.HasUserAuthorizedPermission(Coarse)) { Refresh(); done?.Invoke("Using your time zone for the sun"); yield break; }
            }
            if (!Input.location.isEnabledByUser) { Refresh(); done?.Invoke("Location is off: using your time zone"); yield break; }
            Input.location.Start(5000f, 5000f);   // low accuracy: network or cell, no GPS
            float wait = Time.realtimeSinceStartup + 20f;
            while (Input.location.status == LocationServiceStatus.Initializing && Time.realtimeSinceStartup < wait) yield return null;
            bool ok = Input.location.status == LocationServiceStatus.Running;
            double lat = 0, lon = 0;
            if (ok) { var d = Input.location.lastData; lat = d.latitude; lon = d.longitude; ok = !(lat == 0 && lon == 0); }
            Input.location.Stop();   // one fix is all the sky needs
            if (ok) { Save(lat, lon); done?.Invoke("The sky now follows the sun where you are"); }
            else { Refresh(); done?.Invoke("No location fix: using your time zone"); }
#else
            Refresh();
            done?.Invoke("Using your time zone for the sun");
            yield break;
#endif
        }

        static void Save(double lat, double lon)
        {
            PlayerPrefs.SetFloat(KLat, (float)LocationGuess.Round1(lat));
            PlayerPrefs.SetFloat(KLon, (float)LocationGuess.Round1(lon));
            PlayerPrefs.SetString(KSrc, "location");
            PlayerPrefs.Save();
            Refresh();
        }

        // Settings: forget the cached fix (back to the time zone estimate).
        public static void Forget()
        {
            PlayerPrefs.DeleteKey(KLat); PlayerPrefs.DeleteKey(KLon); PlayerPrefs.DeleteKey(KSrc); PlayerPrefs.Save();
            Refresh();
        }
    }
}
