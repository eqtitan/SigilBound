// Battle effect tunables that the art review set limits on (art review M2/M3, PMO P2-9/P2-10), kept as plain
// numbers so the limits are tested outside Unity (Tests~/BattleVisualsTests.cs). Client/SkillFx.cs, Vfx.cs and
// HollowLook.cs read them. Tiers: 0 Low, 1 Medium, 2 High, 3 Ultra.
using System;

namespace Sigilbound.Core
{
    public static class BattleFx
    {
        // ---------- pillars, walls, crowns and domes (SigilFxPillar.shader) ----------
        // The brightest a pillar's edge gets (0-1 of its colour), never opaque: 40-60% as the review asks, a little
        // less on Low (no bloom there to soften it). Flash effects (Access) can only lower it further.
        public static double PillarOpacity(int tier) => new[] { 0.42, 0.48, 0.55, 0.55 }[Clamp(tier)];
        // Where the surface faces the camera (in front of the unit) only this fraction of the edge shows.
        public const double PillarCore = 0.12;
        public const double PillarFresnel = 2.0;
        // A pillar on a unit lasts at most this long (seconds): it marks the hit, then gets out of the way.
        public const double MaxLifeOnTarget = 0.75;
        public static double LifeOnTarget(double asked) => Math.Min(asked, MaxLifeOnTarget);
        // Its top fades over this fraction of its height (uv), its foot feathers over FadeBottom.
        public const double FadeTop = 0.6, FadeBottom = 0.12;

        // ---------- cast circles ----------
        // A caster's circle matches its element floor disc (CharacterView.Aura: 1.1 m x its scale x its line-up
        // width) instead of a 2.6 m stamp over its neighbours; the disc hides while the circle shows.
        public const double AuraDiameter = 1.1;
        public static double Footprint(double viewScale, double width) => AuraDiameter * Math.Max(0.5, viewScale) * Math.Max(1, width);
        public const double CircleRune = 1.05, CircleGlow = 1.3;   // rune and glow quads, x the footprint
        public const double CircleTime = 0.95;                     // how long the circle (and the hidden disc) lasts

        // ---------- Hollow enemies (HollowLook, SigilHollowOutline.shader) ----------
        // A continuous red outline (inverted hull) instead of the old speckles, in pixels at 1080 p, by tier (Low
        // keeps it: one unlit pass per enemy is the cheapest way to read them). Scaled with the screen's height.
        public static double OutlinePx(int tier) => new[] { 2.0, 2.4, 2.8, 3.0 }[Clamp(tier)];
        public static double OutlineWidth(int tier, double screenH) => OutlinePx(tier) * Math.Max(0.5, screenH / 1080.0);
        // The outline's red (linear, HDR on High and Ultra so bloom catches it; under the bloom threshold on Low).
        public static readonly double[] HollowRed = { 1.0, 0.13, 0.2 };
        public static double OutlineGain(int tier) => tier >= 2 ? 1.6 : 1.0;
        // Glowing eyes: two soft dots on the face, sized by the head (the head bone to the head top).
        public const double EyeUp = 0.38, EyeForward = 0.5, EyeApart = 0.2, EyeSize = 0.22; // x the head's span
        public static double EyeGain(int tier) => tier >= 2 ? 2.2 : 1.2;

        // Relative luminance of a linear colour (Rec. 709): the outline must contrast with every biome's ground.
        public static double Luma(double r, double g, double b) => 0.2126 * r + 0.7152 * g + 0.0722 * b;
        // Readable when its hue stands out from the ground: the red channel leads the ground's by a margin, or it
        // is clearly brighter or darker (a dark violet body inside a bright red line reads on pale ground too).
        public static bool RedReads(double[] ground)
        {
            double gr = ground[0], gg = ground[1], gb = ground[2];
            double redLead = HollowRed[0] - Math.Max(gg, gb) * 0.5 - gr * 0.5; // how much redder the line is
            double contrast = Math.Abs(Luma(HollowRed[0], HollowRed[1], HollowRed[2]) - Luma(gr, gg, gb));
            return redLead > 0.35 || contrast > 0.3;
        }

        static int Clamp(int tier) => Math.Max(0, Math.Min(3, tier));
    }
}
