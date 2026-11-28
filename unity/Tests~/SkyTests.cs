// Real-sun sky (owner decision 2026-11-28): Core/SolarClock (NOAA sun, low-precision moon, bands, face-the-equator
// mapping, the location estimate), SceneGen.LightOf under a real sky (night by design stays night, the Titan keeps its
// light, a 12 degree shadow floor, light and disc on one line), the deterministic tour clock, the one-time location
// question in the pop-up queue, and the client wiring (no network, coarse location only, both builds).
using System;
using System.IO;
using System.Linq;
using Sigilbound.Core;

static class SkyTests
{
    static DateTime U(int y, int mo, int d, int h, int mi = 0) => new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);

    // The highest the sun gets on a UTC day (minute steps) and when.
    static (double el, double az, DateTime at) Peak(DateTime day, double lat, double lon)
    {
        var best = (el: -999.0, az: 0.0, at: day);
        for (int m = 0; m < 1440; m++)
        {
            var t = day.AddMinutes(m); var p = SolarClock.Sun(t, lat, lon);
            if (p.Elevation > best.el) best = (p.Elevation, p.Azimuth, t);
        }
        return best;
    }

    static double Sep(SkyPos a, SkyPos b)
    {
        double r = Math.PI / 180;
        double c = Math.Sin(a.Elevation * r) * Math.Sin(b.Elevation * r) + Math.Cos(a.Elevation * r) * Math.Cos(b.Elevation * r) * Math.Cos((a.Azimuth - b.Azimuth) * r);
        return Math.Acos(Math.Max(-1, Math.Min(1, c))) / r;
    }

    // The tour's sky cases as numbers (what SBSKY logs in a real build), and with csv sun paths in camera angles:
    // yaw relative to the default camera's heading (negative = left) and elevation, every 10 minutes.
    public static void Probe(bool csv)
    {
        var day = U(2026, 11, 28, 0);
        var cases = new (string name, DateTime utc, double lat, double lon, int ch, int st)[]
        {
            ("ny-0700", day.AddHours(12), 40.7, -74.0, 0, 1), ("ny-1200", day.AddHours(17), 40.7, -74.0, 0, 1),
            ("ny-1730", day.AddHours(22.5), 40.7, -74.0, 0, 1), ("ny-2200", day.AddHours(27), 40.7, -74.0, 0, 1),
            ("sydney-1200", day.AddHours(1), -33.9, 151.2, 0, 1), ("lair-ny-1200", day.AddHours(17), 40.7, -74.0, 6, 5),
            ("ny-solstice-noon", U(2026, 6, 21, 16, 57), 40.7128, -74.006, 0, 1), ("tour-fixed", SkyClock.FixedUtc, SkyClock.FixedLat, SkyClock.FixedLon, 0, 1),
        };
        if (!csv)
        {
            Console.WriteLine("case\tsun el/az\tmoon el/az\tband\tlight pitch/yaw\tdisc el/yaw (rel. camera)\tbody");
            foreach (var k in cases)
            {
                var sky = SkyState.At(k.utc, k.lat, k.lon, k.name); var li = SceneGen.LightOf(k.ch, k.st, false, false, sky);
                Console.WriteLine($"{k.name}\t{sky.Sun.Elevation:0.0}/{sky.Sun.Azimuth:0}\t{sky.Moon.Elevation:0.0}/{sky.Moon.Azimuth:0}\t{li.Mood}\t{li.SunPitch:0.0}/{li.SunYaw:0}\t{li.DiscElevation:0.0}/{SolarClock.Wrap180(li.DiscYaw - SolarClock.CameraHeading):0}\t{(li.DiscMoon ? "moon" : "sun")}{(li.DesignNight ? " (night by design)" : "")}");
            }
            return;
        }
        Console.WriteLine("path,minute,rel_yaw,elevation");
        foreach (var (name, d0, lat, lon) in new[] { ("ny-nov28", day.AddHours(5), 40.7, -74.0), ("ny-jun21", U(2026, 6, 21, 4), 40.7, -74.0), ("sydney-nov28", U(2026, 11, 27, 13), -33.9, 151.2) })
            for (int m = 0; m < 1440; m += 10)
            {
                var p = SolarClock.Sun(d0.AddMinutes(m), lat, lon);
                if (p.Elevation < -3) continue;
                Console.WriteLine($"{name},{m},{SolarClock.Wrap180(SolarClock.SceneYaw(p.Azimuth, lat) - SolarClock.CameraHeading):0.00},{p.Elevation:0.00}");
            }
    }

    public static void Run(Action<bool, string> check)
    {
        var keep = SkyClock.Current; SkyClock.Current = null;
        try { Sun(check); Moon(check); Bands(check); Mapping(check); Guess(check); Light(check); Tour(check); Popups(check); Sources(check); }
        finally { SkyClock.Current = keep; }
    }

    // ---------- the sun against NOAA's solar calculator ----------
    static void Sun(Action<bool, string> check)
    {
        // New York (40.7128 N, 74.0060 W), June solstice 2026: solar noon about 12:57 EDT (16:57 UTC) at 72.7 degrees, due south.
        var ny = Peak(U(2026, 6, 21, 0), 40.7128, -74.006);
        check(Math.Abs(ny.el - 72.7) < 0.3 && Math.Abs(ny.az - 180) < 1.5 && Math.Abs((ny.at - U(2026, 6, 21, 16, 57)).TotalMinutes) <= 3,
            $"sky: New York's solstice noon sun reaches about 72.6-72.7 degrees due south near 16:57 UTC ({ny.el:0.00} az {ny.az:0.0} at {ny.at:HH:mm})");
        // Sunrise there that morning: azimuth about 58 degrees (north of east).
        DateTime rise = U(2026, 6, 21, 8);
        while (SolarClock.Sun(rise, 40.7128, -74.006).Elevation < -0.833) rise = rise.AddMinutes(1);
        var r = SolarClock.Sun(rise, 40.7128, -74.006);
        check(Math.Abs(r.Azimuth - 58) < 1.5 && Math.Abs((rise - U(2026, 6, 21, 9, 25)).TotalMinutes) <= 4,
            $"sky: New York's solstice sunrise is in the north-east, about 05:25 EDT ({rise:HH:mm}Z, az {r.Azimuth:0.0})");
        // London, December solstice: a 15 degree noon sun in the south.
        var lon = Peak(U(2026, 12, 21, 0), 51.5, -0.13);
        check(Math.Abs(lon.el - 15.1) < 0.35 && Math.Abs(lon.az - 180) < 1.5, $"sky: London's midwinter noon sun is about 15 degrees up, south ({lon.el:0.00}, az {lon.az:0.0})");
        // Sydney, December solstice: about 79.6 degrees, and to the NORTH (southern hemisphere).
        var syd = Peak(U(2026, 12, 21, 0), -33.87, 151.21);
        check(Math.Abs(syd.el - 79.6) < 0.35 && (syd.az < 2 || syd.az > 358), $"sky: Sydney's midsummer noon sun is about 79.6 degrees up, due north ({syd.el:0.00}, az {syd.az:0.0})");
        // The equator at the March equinox: the noon sun is overhead.
        var eq = Peak(U(2026, 3, 20, 0), 0, -78.5);
        check(eq.el > 89.3, $"sky: at the equator on the March equinox the noon sun is overhead ({eq.el:0.00})");
        check(SolarClock.Sun(U(2026, 6, 21, 4), 40.7, -74).Elevation < -15, "sky: New York at midnight in June: the sun is well below the horizon");
    }

    // ---------- the moon: low precision, checked against eclipses ----------
    static void Moon(Action<bool, string> check)
    {
        // Total lunar eclipse, 3 March 2026 (greatest about 11:33 UTC): the moon stands opposite the sun.
        var s = SolarClock.Sun(U(2026, 3, 3, 11, 33), 20, -150); var m = SolarClock.Moon(U(2026, 3, 3, 11, 33), 20, -150);
        check(Math.Abs(Sep(s, m) - 180) < 2.5, $"sky: at the March 2026 lunar eclipse the moon is opposite the sun ({Sep(s, m):0.0} degrees apart)");
        // Total solar eclipse, 12 August 2026 (greatest about 17:46 UTC near 65.2 N 25.2 W): the moon covers the sun
        // (our moon is geocentric, so up to a degree of parallax on top of its own error).
        s = SolarClock.Sun(U(2026, 8, 12, 17, 46), 65.2, -25.2); m = SolarClock.Moon(U(2026, 8, 12, 17, 46), 65.2, -25.2);
        check(Sep(s, m) < 2.5, $"sky: at the August 2026 solar eclipse the moon sits on the sun ({Sep(s, m):0.0} degrees apart)");
    }

    // ---------- the time-of-day band ----------
    static void Bands(Action<bool, string> check)
    {
        var want = new (double el, Mood m)[] { (-30, Mood.Night), (-6.01, Mood.Night), (-6, Mood.Dusk), (0, Mood.Dusk), (2.9, Mood.Dusk), (3, Mood.Golden), (11.9, Mood.Golden), (12, Mood.Late), (24.9, Mood.Late), (25, Mood.Day), (72.6, Mood.Day) };
        var bad = want.Where(w => SolarClock.BandOf(w.el) != w.m).Select(w => $"{w.el}->{SolarClock.BandOf(w.el)}").ToList();
        check(bad.Count == 0, "sky: bands from the sun's elevation: night below -6, dusk to 3, golden to 12, late to 25, then day" + (bad.Count > 0 ? " (" + string.Join(", ", bad) + ")" : ""));
        // A New York day in late November walks through every band.
        var day = Enumerable.Range(0, 24).Select(h => SkyState.At(U(2026, 11, 28, 5).AddHours(h), 40.7, -74).Band).Distinct().ToList();
        check(day.Count == 5, $"sky: a New York day passes through all five bands ({string.Join(",", day)})");
    }

    // ---------- face the equator ----------
    static void Mapping(Action<bool, string> check)
    {
        double head = SolarClock.CameraHeading;
        check(Math.Abs(head - (-BattleLayout.Camera(BattleLayout.DefaultCamera).Yaw)) < 1e-9 && Math.Abs(head + 38) < 1e-9, $"sky: the equator lies along the default camera's heading ({head:0.0} degrees yaw)");
        check(Math.Abs(SolarClock.SceneYaw(180, 40) - head) < 1e-9 && Math.Abs(SolarClock.SceneYaw(0, -34) - head) < 1e-9,
            "sky: south is straight ahead of the default camera in the north, north straight ahead in the south");
        // Where east lands on screen in the default battle view (landscape): left in the north, right in the south.
        var v = BattleLayout.View(BattleLayout.DefaultCamera, 16.0 / 9, false);
        double ScreenX(double az, double lat)
        {
            double y = SolarClock.SceneYaw(az, lat) * Math.PI / 180;
            var p = v.Eye + new V3(Math.Sin(y) * 100, 0, Math.Cos(y) * 100);
            var q = BattleLayout.ProjectTo(v.Eye, new V3(v.Target.X, v.Eye.Y, v.Target.Z), v.Fov, 16.0 / 9, p);
            return q?.x ?? double.NaN;
        }
        check(ScreenX(120, 40.7) < 0 && ScreenX(240, 40.7) > 0 && ScreenX(60, -33.9) > 0 && ScreenX(300, -33.9) < 0,
            "sky: the northern sun rises on the left and sets on the right; the southern sun rises on the right");
        check(Math.Abs(SolarClock.AzimuthOf(SolarClock.SceneYaw(123, 40), 40) - 123) < 1e-9 && Math.Abs(SolarClock.AzimuthOf(SolarClock.SceneYaw(17, -20), -20) - 17) < 1e-9,
            "sky: scene yaw and compass azimuth map back and forth");
        // Over a winter day in New York the sun is in front of the camera (within 90 degrees of its heading) from about
        // 9:00 to 15:00 EST, i.e. for most of the daylight.
        int up = 0, ahead = 0;
        for (int m = 0; m < 1440; m += 10)
        {
            var p = SolarClock.Sun(U(2026, 11, 28, 0).AddMinutes(m), 40.7, -74);
            if (p.Elevation <= 0) continue; up++;
            if (Math.Abs(SolarClock.Wrap180(SolarClock.SceneYaw(p.Azimuth, 40.7) - head)) < 90) ahead++;
        }
        check(up > 0 && ahead == up, $"sky: in a northern winter the sun stays in the camera's half of the sky all day ({ahead}/{up} daylight samples)");
    }

    // ---------- where the player probably is ----------
    static void Guess(Action<bool, string> check)
    {
        var us = LocationGuess.Estimate(-5, "US", "America/New_York", false, true);
        check(us.lat == 39 && us.lon == -75, $"sky guess: a US phone on Eastern time: 39 N, 75 W from the zone ({us.lat}, {us.lon})");
        var de = LocationGuess.Estimate(1, "de", "Europe/Berlin", false, true);
        check(de.lat == 51 && de.lon == 10, $"sky guess: a single-zone country uses its own longitude ({de.lat}, {de.lon})");
        var cn = LocationGuess.Estimate(8, "CN", "Asia/Shanghai");
        check(cn.lon == 114, "sky guess: China's one zone is far east of its people: the country's longitude wins");
        var au = LocationGuess.Estimate(10, null, "Australia/Sydney", true, false);
        check(au.lat == -40 && au.lon == 150, $"sky guess: no region, a southern zone name: 40 S ({au.lat}, {au.lon})");
        var dst = LocationGuess.Estimate(-3, null, null, true, false);
        check(dst.lat == -40 && dst.lon == -45, "sky guess: daylight saving in January marks a southern zone");
        var none = LocationGuess.Estimate(5.5, null, null);
        check(none.lat == 40 && none.lon == 82.5, "sky guess: nothing known but the offset: 40 N, longitude = offset x 15");
        check(LocationGuess.Estimate(14, "", "").lon == 180 && LocationGuess.Estimate(-12, "ZZ", null).lon == -180, "sky guess: offsets past the date line clamp to +-180");
        check(LocationGuess.Round1(40.7128) == 40.7 && LocationGuess.Round1(-74.006) == -74.0 && LocationGuess.Round1(-33.86) == -33.9, "sky guess: cached places keep one decimal");
    }

    // ---------- the stage light under a real sky ----------
    static void Light(Action<bool, string> check)
    {
        var noon = SkyState.At(U(2026, 11, 28, 17), 40.7, -74);
        var dawn = SkyState.At(U(2026, 11, 28, 12, 20), 40.7, -74);
        var night = SkyState.At(U(2026, 11, 29, 3), 40.7, -74);
        // Chapter 1 stop 2 is authored "late"; the real sky decides.
        var a = SceneGen.LightOf(0, 1, false, false, null);
        check(!a.Real && a.Mood == Mood.Late && a.SunPitch == 44 && Math.Abs(SolarClock.Wrap180(a.DiscYaw - a.SunYaw - 180)) < 1e-3, "sky light: without a real sky every stage keeps its authored light (disc opposite the light)");
        var d = SceneGen.LightOf(0, 1, false, false, noon);
        check(d.Real && d.Mood == noon.Band && Math.Abs(d.DiscElevation - noon.Sun.Elevation) < 1e-3 && Math.Abs(d.SunPitch - Math.Max(12, noon.Sun.Elevation)) < 1e-3,
            $"sky light: the real sun picks the band and the light's pitch ({d.Mood}, pitch {d.SunPitch:0.0})");
        // The light travels away from the disc: horizontal directions opposite, so shadows point away from it.
        double Ly = d.SunYaw * Math.PI / 180, Dy = d.DiscYaw * Math.PI / 180;
        check(Math.Abs(Math.Sin(Ly) * Math.Sin(Dy) + Math.Cos(Ly) * Math.Cos(Dy) + 1) < 1e-4, "sky light: the light comes from exactly where the disc is");
        var e = SceneGen.LightOf(0, 1, false, false, dawn);
        check(e.SunPitch >= 12 && e.DiscElevation < 12 && e.DiscElevation == (float)dawn.Sun.Elevation, $"sky light: a low sun keeps the shadows at 12 degrees or more while the disc sits low ({e.DiscElevation:0.0})");
        var n = SceneGen.LightOf(0, 1, false, false, night);
        check(n.Night && n.DiscMoon && n.Sun < d.Sun && n.SunPitch >= 12, "sky light: at real night the moon lights the stage, dimmer");
        // A night-by-design lair (chapter 7's crown crag) at real noon stays night under a moon; so does Hard mode.
        var lair = SceneGen.LightOf(6, 5, false, false, noon);
        var hard = SceneGen.LightOf(0, 1, true, false, noon);
        check(StoryScenes.Of(6, 5).Mood == Mood.Night && lair.Night && lair.DiscMoon && lair.DesignNight && hard.Night && hard.DesignNight,
            "sky light: story night stops and Hard mode stay night at real noon (moon, lair light)");
        // The Titan keeps its fixed dramatic light.
        var t = SceneGen.LightOf(0, -1, false, true, noon);
        check(!t.Real && t.Mood == Mood.Day && t.SunPitch == 50, "sky light: the Titan's caldera ignores the real sky");
        // Storm dimming still sits on top.
        int storm = Enumerable.Range(0, GameData.Chapters.Length).First(c => Biomes.Of(c).Storm && StoryScenes.Of(c, 0)?.Mood != Mood.Night);
        int calm = Enumerable.Range(0, GameData.Chapters.Length).First(c => !Biomes.Of(c).Storm && StoryScenes.Of(c, 0)?.Mood != Mood.Night);
        check(SceneGen.LightOf(storm, 0, false, false, noon).Sun < SceneGen.LightOf(calm, 0, false, false, noon).Sun, "sky light: a storm stage is still dimmer under the same real sun");
        // The generator reads the same sky (the layout's light is the arena's).
        SkyClock.Current = noon;
        try
        {
            var L = SceneGen.Generate(0, 1);
            check(L.Light.Real && L.Light.Mood == noon.Band, "sky light: the scene generator lights the layout with SkyClock's sky");
        }
        finally { SkyClock.Current = null; }
        check(!SceneGen.Generate(0, 1).Light.Real, "sky light: with SkyClock cleared the layout is authored again");
    }

    // ---------- the screenshot tour and the tests never depend on the hour ----------
    static void Tour(Action<bool, string> check)
    {
        var f1 = SkyClock.Fixed(); var f2 = SkyClock.Fixed();
        check(f1.Utc == U(2026, 6, 21, 16) && f1.Lat == 40.7 && f1.Lon == -74.0 && f1.Sun.Elevation == f2.Sun.Elevation && f1.Moon.Azimuth == f2.Moon.Azimuth && f1.Band == Mood.Day,
            $"sky tour: the fixed moment is midsummer noon in New York, the same every run ({f1})");
        var l1 = SceneGen.LightOf(0, 1, false, false, f1); var l2 = SceneGen.LightOf(0, 1, false, false, f2);
        check(l1.SunPitch == l2.SunPitch && l1.SunYaw == l2.SunYaw && l1.DiscYaw == l2.DiscYaw && l1.Sun == l2.Sun, "sky tour: the same sky gives the same light");
        check(SkyClock.Current == null, "sky tour: tests leave SkyClock empty (authored light) unless they set it");
    }

    // ---------- the one-time location question ----------
    static void Popups(Action<bool, string> check)
    {
        var c = new PopupContext { OnHome = true, SkyAskPending = true, UpdateAvailable = true, SinceClose = 10 };
        check(PopupQueue.Next(c) == Popup.SkyAsk, "sky popup: the location question comes before the update banner");
        c.TipPending = true;
        check(PopupQueue.Next(c) == Popup.Tip, "sky popup: tips go first");
        c = new PopupContext { OnHome = true, SkyAskPending = true, FirstLaunch = true, SinceClose = 10 };
        check(PopupQueue.Next(c) == Popup.None, "sky popup: never on the very first launch");
        c = new PopupContext { InBattle = true, SkyAskPending = true, SinceClose = 10 };
        check(PopupQueue.Next(c) == Popup.None, "sky popup: never in battle or away from Home");
        check(Array.IndexOf(PopupQueue.Order, Popup.SkyAsk) == Array.IndexOf(PopupQueue.Order, Popup.UpdateBanner) - 1, "sky popup: listed just before the update banner");
    }

    // ---------- the client side ----------
    static void Sources(Action<bool, string> check)
    {
        string root = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && root != null && !Directory.Exists(Path.Combine(root, "Assets", "Sigilbound")); i++) root = Path.GetDirectoryName(root);
        if (root == null) { check(true, "sky: sources not found here (skipped client checks)"); return; }
        string Src(string rel) { var p = Path.Combine(root, "Assets", "Sigilbound", rel); return File.Exists(p) ? File.ReadAllText(p) : ""; }
        var sun = Src("Client/SkySun.cs"); var core = Src("Core/SolarClock.cs"); var disc = Src("Client/SkyDisc.cs");
        var arena = Src("Client/Arena.cs"); var shots = Src("Client/ShotsMode.cs"); var set = Src("Client/SettingsScreen.cs"); var app = Src("Client/App.cs");
        var perm = Src("Editor/AndroidLocationPermission.cs"); var urp = Src("Editor/UrpSetup.cs"); var scen = Src("Client/Scenery.cs");
        string[] net = { "UnityWebRequest", "HttpClient", "WebRequest", "WWW(", "Online.", "Socket" };
        check(sun.Length > 0 && core.Length > 0 && !net.Any(w => sun.Contains(w) || core.Contains(w) || disc.Contains(w)), "sky: the location never leaves the device (no network code in SkySun, SolarClock or SkyDisc)");
        check(sun.Contains("ACCESS_COARSE_LOCATION") && sun.Contains("Permission.RequestUserPermission") && sun.Contains("Input.location.Start(5000f") && sun.Contains("Input.location.Stop()")
              && sun.Contains("LocationGuess.Round1(lat)") && !sun.Contains("ACCESS_FINE_LOCATION"),
            "sky: one coarse, low-accuracy fix, the service stopped after it, cached rounded to 0.1 degree");
        check(perm.Contains("Coarse = \"android.permission.ACCESS_COARSE_LOCATION\"") && perm.Contains("root.RemoveChild(n)") && !perm.Contains("IsStoreBuild") && perm.Contains("IPostGenerateGradleAndroidProject"),
            "sky: the manifest gets approximate location (and loses Unity's automatic fine location) in both the tester and the store build");
        check(app.Contains("SkyAskPending = SkySun.AskPending(G.S.cleared)") && app.Contains("SkySun.Why") && sun.Contains("cleared >= 3") && sun.Contains("Application.platform == RuntimePlatform.Android"),
            "sky: Android asks once, after a few cleared stages, with an in-game reason first");
        check(sun.Contains("Lets the sky show the real sun where you are"), "sky: the reason line says what the location is for");
        check(set.Contains("\"Real sun at my location\"") && set.Contains("\"Use my location\"") && sun.Contains("GetInt(KOn, 1) == 1"), "sky: Settings has Real sun at my location (on by default) and Use my location");
        check(sun.Contains("if (App.NoSave || App.InShots) { SkyClock.Current = SkyClock.Fixed(); return; }") && shots.Contains("IEnumerator SkyTour()") && shots.Contains("SkySun.FixedUtc = null"),
            "sky: the screenshot tour runs on a fixed clock and place; its sky step sets and clears its own moments");
        foreach (var name in new[] { "ny-0700", "ny-1200", "ny-1730", "ny-2200", "sydney-1200", "lair-ny-1200" })
            check(shots.Contains($"(\"{name}\""), $"sky: the tour photographs {name}");
        check(arena.Contains("SkySun.Refresh();") && arena.IndexOf("SkySun.Refresh();") < arena.IndexOf("SceneGen.Generate(") && arena.Contains("SkyDisc.Build(") && arena.Contains("Sky.Follow(Cam)"),
            "sky: the arena reads the sky before generating the stage and keeps the disc on the light's line every frame");
        check(disc.Contains("Shader.Find(\"Sigilbound/SkyGlow\")") && urp.Contains("\"Sigilbound/SkyGlow\"") && File.Exists(Path.Combine(root, "Assets", "Sigilbound", "Client", "Shaders", "SigilSkyGlow.shader")),
            "sky: the disc's additive shader ships (always-included list)");
        var sh = File.ReadAllText(Path.Combine(root, "Assets", "Sigilbound", "Client", "Shaders", "SigilSkyGlow.shader"));
        check(sh.Contains("Blend One One") && sh.Contains("ZWrite Off") && !sh.Contains("multi_compile_fog"), "sky: the disc is unlit additive light with no fog and no depth write");
        check(scen.Contains("SkyDisc.Queue - 1") && disc.Contains("Queue = 2996") && scen.Contains("2996 + i"), "sky: the disc draws after the painted sky and before the backdrop ridges");
    }
}
