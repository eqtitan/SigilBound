// The real sun and moon over the player (owner decision 2026-11-28, "real-sun sky"). Unity-free so Tests~ can check it.
//   SolarClock  - solar elevation and azimuth from latitude, longitude and UTC (NOAA's solar calculator equations,
//                 good to about 0.1 degree between 1901 and 2099), a low-precision moon (about 1 degree), the
//                 time-of-day band, and the mapping from compass azimuth to the arena's yaw ("face the equator").
//   SkyState    - one moment's sky: the sun, the moon, the band, and which body lights a night stage.
//   SkyClock    - the sky the next arena is built under; null keeps every stage's authored light (setting off, tests).
//   LocationGuess - where the player probably is without asking: the time zone's standard offset (15 degrees per
//                 hour), the device's region (a small country table) and the hemisphere a time zone name implies.
// Client/SkySun.cs feeds it (device clock, cached coarse location); SceneGen.LightOf turns it into the stage light.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    // A body's place in the sky: elevation above the horizon and compass azimuth (0 north, 90 east), in degrees.
    public struct SkyPos
    {
        public double Elevation, Azimuth;
        public SkyPos(double elevation, double azimuth) { Elevation = elevation; Azimuth = azimuth; }
        public override string ToString() => $"el {Elevation:0.0} az {Azimuth:0.0}";
    }

    public static class SolarClock
    {
        const double Rad = Math.PI / 180, Deg = 180 / Math.PI;

        // Band edges on the sun's elevation (degrees). Night below civil twilight (-6); dusk (or dawn) through
        // sunset and the first few degrees; golden hour; the low "late" light of morning, afternoon and winter noons.
        public const double NightBelow = -6, DuskBelow = 3, GoldenBelow = 12, LateBelow = 25;

        // Shadows stretch without limit as the sun nears the horizon: the light never drops below this, the disc may.
        public const double MinShadowPitch = 12;

        public static double JulianDay(DateTime utc)
        {
            if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
            return (utc - new DateTime(2000, 1, 1, 12, 0, 0, DateTimeKind.Utc)).TotalDays + 2451545.0;
        }

        // NOAA solar position (the equations of the NOAA Global Monitoring Lab's solar calculator spreadsheet),
        // with its atmospheric refraction correction. Longitude east positive.
        public static SkyPos Sun(DateTime utc, double lat, double lon)
        {
            if (utc.Kind == DateTimeKind.Local) utc = utc.ToUniversalTime();
            double jc = (JulianDay(utc) - 2451545.0) / 36525.0;
            double l0 = Mod(280.46646 + jc * (36000.76983 + jc * 0.0003032), 360);
            double m = 357.52911 + jc * (35999.05029 - 0.0001537 * jc);
            double e = 0.016708634 - jc * (0.000042037 + 0.0000001267 * jc);
            double c = Math.Sin(m * Rad) * (1.914602 - jc * (0.004817 + 0.000014 * jc)) + Math.Sin(2 * m * Rad) * (0.019993 - 0.000101 * jc) + Math.Sin(3 * m * Rad) * 0.000289;
            double trueLong = l0 + c;
            double omega = 125.04 - 1934.136 * jc;
            double appLong = trueLong - 0.00569 - 0.00478 * Math.Sin(omega * Rad);
            double meanObliq = 23 + (26 + (21.448 - jc * (46.815 + jc * (0.00059 - jc * 0.001813))) / 60) / 60;
            double obliq = meanObliq + 0.00256 * Math.Cos(omega * Rad);
            double dec = Math.Asin(Math.Sin(obliq * Rad) * Math.Sin(appLong * Rad));
            double y = Math.Tan(obliq / 2 * Rad); y *= y;
            double eot = 4 * Deg * (y * Math.Sin(2 * l0 * Rad) - 2 * e * Math.Sin(m * Rad) + 4 * e * y * Math.Sin(m * Rad) * Math.Cos(2 * l0 * Rad)
                                    - 0.5 * y * y * Math.Sin(4 * l0 * Rad) - 1.25 * e * e * Math.Sin(2 * m * Rad));   // minutes
            double minutes = utc.TimeOfDay.TotalMinutes;
            double tst = Mod(minutes + eot + 4 * lon, 1440);
            double ha = tst / 4 < 0 ? tst / 4 + 180 : tst / 4 - 180;
            double latR = lat * Rad;
            double cosZen = Math.Sin(latR) * Math.Sin(dec) + Math.Cos(latR) * Math.Cos(dec) * Math.Cos(ha * Rad);
            double zen = Math.Acos(Clamp(cosZen, -1, 1)) * Deg;
            double el = 90 - zen;
            el += Refraction(el);
            double az;
            double sinZen = Math.Sin(zen * Rad);
            if (Math.Abs(Math.Cos(latR) * sinZen) < 1e-9) az = lat > 0 ? 180 : 0;   // the sun overhead or at a pole
            else
            {
                double a = Math.Acos(Clamp((Math.Sin(latR) * Math.Cos(zen * Rad) - Math.Sin(dec)) / (Math.Cos(latR) * sinZen), -1, 1)) * Deg;
                az = ha > 0 ? Mod(a + 180, 360) : Mod(540 - a, 360);
            }
            return new SkyPos(el, az);
        }

        // NOAA's refraction approximation (degrees to add to the geometric elevation).
        static double Refraction(double el)
        {
            if (el > 85) return 0;
            double te = Math.Tan(el * Rad), k;
            if (el > 5) k = 58.1 / te - 0.07 / (te * te * te) + 0.000086 / Math.Pow(te, 5);
            else if (el > -0.575) k = 1735 + el * (-518.2 + el * (103.4 + el * (-12.79 + el * 0.711)));
            else k = -20.772 / te;
            return k / 3600;
        }

        // A low-precision moon (the Astronomical Almanac's short series, as in the suncalc library): about a degree
        // off, plenty for a disc in a painted sky. Longitude east positive.
        public static SkyPos Moon(DateTime utc, double lat, double lon)
        {
            double d = JulianDay(utc) - 2451545.0;
            double L = (218.316 + 13.176396 * d) * Rad, M = (134.963 + 13.064993 * d) * Rad, F = (93.272 + 13.229350 * d) * Rad;
            double l = L + 6.289 * Rad * Math.Sin(M), b = 5.128 * Rad * Math.Sin(F);
            double e = 23.4397 * Rad;
            double ra = Math.Atan2(Math.Sin(l) * Math.Cos(e) - Math.Tan(b) * Math.Sin(e), Math.Cos(l));
            double dec = Math.Asin(Math.Sin(b) * Math.Cos(e) + Math.Cos(b) * Math.Sin(e) * Math.Sin(l));
            double theta = (280.16 + 360.9856235 * d) * Rad + lon * Rad;   // local sidereal time
            double H = theta - ra, phi = lat * Rad;
            double alt = Math.Asin(Clamp(Math.Sin(phi) * Math.Sin(dec) + Math.Cos(phi) * Math.Cos(dec) * Math.Cos(H), -1, 1));
            double azSouth = Math.Atan2(Math.Sin(H), Math.Cos(H) * Math.Sin(phi) - Math.Tan(dec) * Math.Cos(phi));   // from south, west positive
            double el = alt * Deg;
            el += Refraction(el);
            return new SkyPos(el, Mod(azSouth * Deg + 180, 360));
        }

        // The time-of-day band for the sun's elevation (morning and evening share the warm bands).
        public static Mood BandOf(double sunElevation) =>
            sunElevation < NightBelow ? Mood.Night : sunElevation < DuskBelow ? Mood.Dusk : sunElevation < GoldenBelow ? Mood.Golden
            : sunElevation < LateBelow ? Mood.Late : Mood.Day;

        // Face the equator: the default battle camera's heading (Unity yaw, 0 = +Z, clockwise from above) points at the
        // sun's daily path: south for a northern player, north for a southern one. East then sits on the camera's left
        // in the north (the sun rises left, crosses, sets right) and on its right in the south.
        public static double CameraHeading => -BattleLayout.Camera(BattleLayout.DefaultCamera).Yaw;
        public static double EquatorAzimuth(double lat) => lat >= 0 ? 180 : 0;
        public static double SceneYaw(double azimuth, double lat) => Wrap180(CameraHeading + azimuth - EquatorAzimuth(lat));
        // And back: the compass azimuth a scene yaw stands for (tests, logs).
        public static double AzimuthOf(double sceneYaw, double lat) => Mod(sceneYaw - CameraHeading + EquatorAzimuth(lat), 360);

        public static double Mod(double a, double m) { double r = a % m; return r < 0 ? r + m : r; }
        public static double Wrap180(double a) { a = Mod(a, 360); return a > 180 ? a - 360 : a; }
        static double Clamp(double v, double lo, double hi) => v < lo ? lo : v > hi ? hi : v;
    }

    // One moment's sky over one place.
    public sealed class SkyState
    {
        public DateTime Utc; public double Lat, Lon; public string Source = "fixed";
        public SkyPos Sun, Moon; public Mood Band;

        public static SkyState At(DateTime utc, double lat, double lon, string source = "fixed")
        {
            if (utc.Kind == DateTimeKind.Unspecified) utc = DateTime.SpecifyKind(utc, DateTimeKind.Utc);
            var s = new SkyState { Utc = utc.ToUniversalTime(), Lat = lat, Lon = lon, Source = source };
            s.Sun = SolarClock.Sun(s.Utc, lat, lon);
            s.Moon = SolarClock.Moon(s.Utc, lat, lon);
            s.Band = SolarClock.BandOf(s.Sun.Elevation);
            return s;
        }

        // The moon a night stage shows: the real moon when it is well up; at real night with the moon down, a moon
        // opposite the sun's path (high enough to read); on a stage that is night by design during the real day,
        // a fixed moon a little right of the camera's heading, 40 degrees up.
        public SkyPos NightMoon()
        {
            if (Moon.Elevation > 8) return Moon;
            if (Sun.Elevation < SolarClock.NightBelow)
                return new SkyPos(Math.Max(15, Math.Min(60, -Sun.Elevation)), SolarClock.Mod(Sun.Azimuth + 180, 360));
            return new SkyPos(40, SolarClock.Mod(SolarClock.EquatorAzimuth(Lat) + 20, 360));
        }

        public override string ToString() => $"{Utc:yyyy-MM-dd HH:mm}Z lat {Lat:0.0} lon {Lon:0.0} ({Source}) sun {Sun} moon {Moon} band {Band}";
    }

    public static class SkyClock
    {
        // The sky the next arena is built under (Client/SkySun.Refresh). null: every stage keeps its authored light
        // (Settings "Real sun at my location" off, and Tests~, which must restore it to null).
        public static SkyState Current;

        // The fixed moment and place of the screenshot tour (-sbShots) and the tests: midsummer noon in New York
        // (12:00 EDT = 16:00 UTC), so shots never change with the hour they were taken.
        public static readonly DateTime FixedUtc = new DateTime(2026, 6, 21, 16, 0, 0, DateTimeKind.Utc);
        public const double FixedLat = 40.7, FixedLon = -74.0;
        public static SkyState Fixed() => SkyState.At(FixedUtc, FixedLat, FixedLon, "fixed");
    }

    // The edge glow (owner decision 2026-11-28): the disc stays exactly true, so when the sun (or moon) is above or beside
    // the frame - always, in the default battle and menu views, which end below the horizon (L032) - a warm glow and a few
    // soft beams come in from the top edge on its side. How far outside the frame and how strong (Client/SkyDisc).
    public static class SkyEdge
    {
        // Angles of a direction in a camera's frame: right, up and forward are the camera's unit axes; vfov in degrees.
        // out: degrees between the direction and the nearest frame edge (0 inside); x: the screen x (0..1) the glow comes
        // from, on the body's side (clamped inside the top edge); behind: the body is behind the camera.
        public static (double outDeg, double x, bool behind) Frame(V3 dir, V3 right, V3 up, V3 forward, double vfov, double aspect)
        {
            double lx = dir.Dot(right), ly = dir.Dot(up), lz = dir.Dot(forward);
            double ax = Math.Atan2(lx, lz) * 180 / Math.PI, ay = Math.Asin(Math.Max(-1, Math.Min(1, ly))) * 180 / Math.PI;
            double hv = vfov / 2, hx = Math.Atan(Math.Tan(hv * Math.PI / 180) * aspect) * 180 / Math.PI;
            double ox = Math.Max(0, Math.Abs(ax) - hx), oy = Math.Max(0, Math.Abs(ay) - hv);
            double x = 0.5 + 0.5 * Math.Tan(Math.Max(-80, Math.Min(80, ax)) * Math.PI / 180) / Math.Tan(hx * Math.PI / 180);
            return (Math.Sqrt(ox * ox + oy * oy), Math.Max(0.06, Math.Min(0.94, x)), lz < 0);
        }

        // Strength of the glow and of the beams (0..1 before colour). Zero while the disc is truly in frame (it fades in over
        // the first 4 degrees outside, so the two never double up) or below the horizon; weaker the farther outside (never
        // gone while the body is up: a sun behind the camera still warms the top edge a little); weaker for a high noon
        // sun; the moon a faint cool glow with no beams; storms dim both.
        public static (double glow, double beams) Strength(double outDeg, double elevation, bool moon, bool storm)
        {
            if (elevation < -1.5 || outDeg <= 0) return (0, 0);
            double enter = Smooth(0, 4, outDeg);
            double far = 1 - 0.65 * Smooth(5, 90, outDeg);
            double high = 1 - 0.5 * Smooth(30, 75, elevation);
            double low = Smooth(-1.5, 2, elevation);
            double k = enter * far * high * low * (storm ? 0.45 : 1);
            return moon ? (k * 0.45, 0) : (k, k);
        }

        static double Smooth(double a, double b, double v) { double t = Math.Max(0, Math.Min(1, (v - a) / (b - a))); return t * t * (3 - 2 * t); }
    }

    // Where the player probably is, without asking (and the fallback when they say no or there is no fix).
    public static class LocationGuess
    {
        // ISO 3166 region -> (latitude, longitude) of where most people live. Longitude is used only for countries
        // with one time zone; for the wide ones (Multi) the time zone's offset gives a better longitude.
        static readonly Dictionary<string, (double lat, double lon)> Countries = new Dictionary<string, (double, double)>
        {
            ["US"] = (39, -95), ["CA"] = (47, -80), ["MX"] = (21, -101), ["BR"] = (-16, -47), ["AR"] = (-34, -60), ["CL"] = (-34, -71),
            ["CO"] = (5, -74), ["PE"] = (-10, -77), ["VE"] = (9, -67), ["EC"] = (-1, -79), ["UY"] = (-34, -56), ["BO"] = (-17, -65),
            ["GB"] = (52.5, -1.5), ["IE"] = (53.3, -7), ["FR"] = (47, 2.5), ["DE"] = (51, 10), ["NL"] = (52.2, 5.3), ["BE"] = (50.8, 4.4),
            ["ES"] = (40.4, -3.7), ["PT"] = (39.5, -8.5), ["IT"] = (42.5, 12.5), ["CH"] = (46.8, 8.2), ["AT"] = (47.6, 14.5), ["PL"] = (52, 19.5),
            ["CZ"] = (49.8, 15.5), ["SE"] = (59.5, 16), ["NO"] = (60.5, 9), ["DK"] = (55.8, 10.5), ["FI"] = (61.5, 25), ["GR"] = (38.5, 23),
            ["TR"] = (39.5, 32), ["RO"] = (45.5, 25), ["HU"] = (47.3, 19.3), ["UA"] = (49, 31), ["RU"] = (55.5, 45), ["IL"] = (31.8, 35),
            ["EG"] = (29, 31), ["SA"] = (24, 45), ["AE"] = (24.5, 54.5), ["NG"] = (8, 7.5), ["KE"] = (-0.5, 37), ["ZA"] = (-28, 27),
            ["MA"] = (33, -6.5), ["IN"] = (22, 79), ["PK"] = (30, 71), ["BD"] = (23.8, 90.3), ["CN"] = (32, 114), ["JP"] = (35.5, 137),
            ["KR"] = (36.5, 127.5), ["TW"] = (24, 121), ["HK"] = (22.3, 114.2), ["SG"] = (1.35, 103.8), ["MY"] = (3.5, 102), ["TH"] = (14.5, 100.5),
            ["VN"] = (16, 106.5), ["PH"] = (13, 122), ["ID"] = (-6.5, 110), ["AU"] = (-31, 145), ["NZ"] = (-39.5, 175),
        };
        static readonly HashSet<string> Multi = new HashSet<string> { "US", "CA", "MX", "BR", "RU", "AU", "ID" };

        // Time zone names (IANA) of the southern hemisphere, by prefix.
        static readonly string[] South =
        {
            "Australia/", "Pacific/Auckland", "Pacific/Chatham", "Pacific/Fiji", "Pacific/Tongatapu", "Pacific/Apia", "Pacific/Noumea",
            "America/Sao_Paulo", "America/Argentina", "America/Buenos_Aires", "America/Santiago", "America/Montevideo", "America/Asuncion",
            "America/La_Paz", "America/Lima", "America/Bahia", "America/Recife", "America/Fortaleza", "America/Belem", "America/Cuiaba",
            "America/Campo_Grande", "America/Manaus", "America/Porto_Velho", "Africa/Johannesburg", "Africa/Maputo", "Africa/Harare",
            "Africa/Lusaka", "Africa/Windhoek", "Africa/Gaborone", "Africa/Luanda", "Africa/Kinshasa", "Africa/Dar_es_Salaam",
            "Indian/Mauritius", "Indian/Reunion", "Indian/Antananarivo", "Asia/Jakarta", "Asia/Makassar", "Asia/Jayapura", "Antarctica/",
        };

        // stdOffsetHours: the time zone's standard offset (no daylight saving). region: ISO 3166 two-letter code or
        // null. tzId: the zone's name (IANA on Android and Linux, Windows names elsewhere) or null. dstInJanuary /
        // dstInJuly: whether the zone observes daylight saving in that month (a southern zone does in January).
        public static (double lat, double lon, string how) Estimate(double stdOffsetHours, string region, string tzId, bool dstInJanuary = false, bool dstInJuly = false)
        {
            double lon = Math.Max(-180, Math.Min(180, stdOffsetHours * 15));
            string r = region?.Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(r) && Countries.TryGetValue(r, out var c))
                return (c.lat, Multi.Contains(r) ? lon : c.lon, "region " + r);
            bool south = false;
            if (!string.IsNullOrEmpty(tzId)) foreach (var p in South) if (tzId.StartsWith(p, StringComparison.OrdinalIgnoreCase)) { south = true; break; }
            if (!south && dstInJanuary && !dstInJuly) south = true;
            return (south ? -40 : 40, lon, south ? "time zone (south)" : "time zone");
        }

        // Cached and logged locations keep one decimal (about 11 km): enough for the sun, never a street address.
        public static double Round1(double v) => Math.Round(v, 1, MidpointRounding.AwayFromZero);
    }
}
