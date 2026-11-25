// Impact timing and hit-stop per weapon family (docs/sigilbound/MECHANICS.md sections 3.2 and 4.1).
// Everything is time-based: `impact` is a normalized clip time, hit-stop is in scaled seconds, so an
// action lands and lasts the same at 30, 60 and 120 fps; only smoothness differs.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public enum WeaponFamily { Blade, Heavy, Polearm, Daggers, Bow, Caster, Support, Shield, Unarmed }

    public static class Impact
    {
        static readonly HashSet<string> Brawlers = new HashSet<string> { "odo", "kettle", "gorrum", "thrain" };

        public static WeaponFamily FamilyOf(string heroId)
        {
            if (Brawlers.Contains(heroId)) return WeaponFamily.Unarmed;
            switch (Looks.Of(heroId).Prop)
            {
                case "sword": case "katana": case "rapier": return WeaponFamily.Blade;
                case "hammer": case "club": case "mace": case "axe": case "flail": case "anchor": case "gauntlets": return WeaponFamily.Heavy;
                case "spear": case "trident": case "glaive": case "scythe": case "lance": case "whip": return WeaponFamily.Polearm;
                case "daggers": case "sickle": case "hook": return WeaponFamily.Daggers;
                case "bow": case "crossbow": case "chakram": case "net": return WeaponFamily.Bow;
                case "staff": case "orb": case "mirror": case "book": case "fan": return WeaponFamily.Caster;
                case "bell": case "lantern": case "candle": case "lute": case "harp": return WeaponFamily.Support;
                case "shield": return WeaponFamily.Shield;
                default: return WeaponFamily.Blade;
            }
        }

        // Normalized time of contact in the attack clip (ranged families: the release).
        public static double ImpactTime(WeaponFamily f) => f switch
        {
            WeaponFamily.Blade => 0.38, WeaponFamily.Heavy => 0.52, WeaponFamily.Polearm => 0.45, WeaponFamily.Daggers => 0.30,
            WeaponFamily.Bow => 0.55, WeaponFamily.Caster => 0.45, WeaponFamily.Support => 0.60, WeaponFamily.Shield => 0.40,
            _ => 0.33,
        };

        // Base hit-stop at 1x (seconds), before crit / kill / ultimate.
        public static double HitStopBase(WeaponFamily f) => f switch
        {
            WeaponFamily.Blade => 0.07, WeaponFamily.Heavy => 0.12, WeaponFamily.Polearm => 0.08, WeaponFamily.Daggers => 0.09,
            WeaponFamily.Bow => 0.05, WeaponFamily.Caster => 0.06, WeaponFamily.Support => 0.05, WeaponFamily.Shield => 0.09,
            _ => 0.06,
        };

        public const double MaxHitStop = 0.25;

        // Projectile flight (MECHANICS.md 4.4): by speed, clamped, so far targets take longer and near ones
        // don't crawl. Returns seconds for `dist` metres.
        public static double FlightTime(string kind, double dist)
        {
            var (speed, lo, hi) = kind switch
            {
                "arrow" => (22.0, 0.15, 0.35), "bolt" => (16.0, 0.18, 0.40), "orb" => (9.0, 0.25, 0.55), _ => (14.0, 0.18, 0.45),
            };
            return Math.Max(lo, Math.Min(hi, dist / speed));
        }

        // Multi-hit skills: time between hits on one target (the 0.22 / 0.48 / 0.74 spacing of a ~1 s
        // clip), and the short stop on every hit but the last.
        public const double MultiHitGap = 0.26, MultiHitStop = 0.04;

        // Hit-stop in SCALED seconds (battle speed is Time.timeScale): base x speed^0.45, so the real-time
        // stop is base x speed^-0.55 and impacts still read at 3x. Heals and buffs: 0.
        public static double HitStop(WeaponFamily f, bool crit, bool kill, bool ult, double speed)
        {
            double b = ult ? 0.14 : HitStopBase(f);
            if (crit) b += 0.04;
            if (kill) b += 0.03;
            b = Math.Min(b, MaxHitStop);
            return b * Math.Pow(Math.Max(1, speed), 0.45);
        }

        // Frame-stepped check (used by tests): the time at which a clip of `length` seconds, advanced in
        // steps of 1/fps, first crosses normalized `impact`. Within one frame of the exact time at any fps.
        public static double CrossingTime(double length, double impact, double fps)
        {
            double dt = 1.0 / fps, t = 0, prev = 0;
            while (true)
            {
                t += dt;
                if (prev < impact * length && t >= impact * length) return t;
                prev = t;
                if (t > length * 4) return t;
            }
        }
    }
}
