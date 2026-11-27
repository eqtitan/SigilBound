// URP post-processing built in code: ACES tone mapping (as the web), bloom on bright glows
// (post.mjs threshold 0.88), and a per-arena colour grade translated from postfx2.mjs LUT_PRESETS
// (temperature/tint, saturation, contrast, split toning). Quality tiers: Low has no post-processing,
// Medium grades without bloom, High and Ultra grade with bloom (as post.mjs).
using UnityEngine;
#if SB_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace Sigilbound.Client
{
    public static class PostFx
    {
        // postfx2.mjs LUT_PRESETS (temperature, tint, saturation, contrast, shadowHue, shadowAmount,
        // highlightHue, highlightAmount, splitBalance), by scenery theme.
        sealed class Grade { public float Temp, Tint, Sat = 1, Contrast, ShHue = 220, ShAmt, HiHue = 40, HiAmt, Balance; }
        static readonly Grade[] Themes =
        {
            new Grade { Temp = 0.12f, ShHue = 235, ShAmt = 0.10f, HiHue = 32, HiAmt = 0.10f, Contrast = 0.10f, Sat = 1.03f },  // dusk forest
            new Grade { Temp = -0.06f, ShHue = 195, ShAmt = 0.12f, HiHue = 35, HiAmt = 0.07f, Contrast = 0.08f },              // teal shore
            new Grade { Temp = 0.10f, ShHue = 215, ShAmt = 0.09f, HiHue = 30, HiAmt = 0.10f, Contrast = 0.12f, Sat = 0.98f },  // red badlands
            new Grade { Tint = -0.06f, ShHue = 165, ShAmt = 0.12f, HiHue = 58, HiAmt = 0.06f, Contrast = 0.06f, Sat = 0.94f }, // green marsh
            new Grade { Temp = 0.10f, ShHue = 260, ShAmt = 0.09f, HiHue = 45, HiAmt = 0.14f, Contrast = 0.10f },               // gold ruins
            new Grade { ShHue = 275, ShAmt = 0.09f, HiHue = 335, HiAmt = 0.06f, Contrast = 0.05f, Sat = 1.03f },               // blossom forest
            new Grade { Temp = -0.20f, ShHue = 225, ShAmt = 0.16f, HiHue = 200, HiAmt = 0.05f, Contrast = 0.09f, Sat = 0.88f },// moonlit shore
            new Grade { ShHue = 268, ShAmt = 0.14f, HiHue = 340, HiAmt = 0.07f, Contrast = 0.11f },                            // amethyst badlands
            new Grade { ShHue = 182, ShAmt = 0.15f, HiHue = 175, HiAmt = 0.05f, Contrast = 0.09f, Sat = 0.96f },               // glow marsh
            new Grade { Temp = 0.18f, ShHue = 200, ShAmt = 0.07f, HiHue = 26, HiAmt = 0.16f, Contrast = 0.15f },               // burning ruins
            // act 2
            new Grade { Temp = 0.08f, ShHue = 210, ShAmt = 0.12f, HiHue = 36, HiAmt = 0.12f, Contrast = 0.1f },                // saltreach harbor
            new Grade { Tint = -0.08f, ShHue = 150, ShAmt = 0.16f, HiHue = 80, HiAmt = 0.05f, Contrast = 0.12f, Sat = 0.85f },  // black fens
            new Grade { Temp = -0.1f, ShHue = 230, ShAmt = 0.12f, HiHue = 210, HiAmt = 0.06f, Contrast = 0.08f, Sat = 0.8f },   // wraithmoor barrows
            new Grade { Temp = 0.22f, ShHue = 350, ShAmt = 0.1f, HiHue = 24, HiAmt = 0.18f, Contrast = 0.16f, Sat = 1.05f },   // ember groves
            new Grade { Temp = -0.18f, ShHue = 215, ShAmt = 0.16f, HiHue = 200, HiAmt = 0.08f, Contrast = 0.14f, Sat = 0.82f }, // storm coast
            new Grade { Temp = 0.04f, ShHue = 205, ShAmt = 0.08f, HiHue = 45, HiAmt = 0.08f, Contrast = 0.06f, Sat = 0.9f },   // glass desert (theme 15)
            new Grade { Temp = -0.16f, ShHue = 222, ShAmt = 0.15f, HiHue = 205, HiAmt = 0.06f, Contrast = 0.12f, Sat = 0.84f }, // stormcrown crag (theme 16)
        };
        static readonly Grade Titan = new Grade { Temp = 0.14f, ShHue = 350, ShAmt = 0.12f, HiHue = 28, HiAmt = 0.13f, Contrast = 0.17f, Sat = 1.02f };
        static readonly Grade Neutral = new Grade();

#if SB_URP
        static Volume _volume; static Bloom _bloom; static ColorAdjustments _color; static WhiteBalance _wb; static SplitToning _split;
        static Camera _cam; static int _tier = 2;

        static void Ensure()
        {
            if (_volume != null) return;
            var go = new GameObject("PostFx"); Object.DontDestroyOnLoad(go);
            _volume = go.AddComponent<Volume>();
            _volume.isGlobal = true; _volume.priority = 10;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            _volume.sharedProfile = profile;
            profile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);
            _bloom = profile.Add<Bloom>(true);
            _bloom.threshold.Override(0.88f); _bloom.scatter.Override(0.65f); _bloom.intensity.Override(0.8f); _bloom.highQualityFiltering.Override(false);
            _color = profile.Add<ColorAdjustments>(true);
            _wb = profile.Add<WhiteBalance>(true);
            _split = profile.Add<SplitToning>(true);
        }
#endif

        public static void Init(Camera cam)
        {
#if SB_URP
            _cam = cam; Ensure(); SetTier(_tier);
#endif
        }

        // Tiers 0 Low .. 3 Ultra: Low no post at all, Medium grade only, High/Ultra grade + bloom.
        public static void SetTier(int tier)
        {
#if SB_URP
            _tier = tier;
            if (_cam != null) { var data = _cam.GetUniversalAdditionalCameraData(); if (data != null) data.renderPostProcessing = tier >= 1 && !_covered; }
            if (_bloom != null) { _bloom.active = tier >= 2; _bloom.intensity.Override(tier >= 3 ? 0.9f : 0.8f); _bloom.highQualityFiltering.Override(tier >= 3); }
#endif
        }

        // A full-screen menu covers the arena (Arena.SetCovered): no post pass for a scene nobody sees.
        static bool _covered;
        public static void SetCovered(bool covered)
        {
            if (_covered == covered) return;
            _covered = covered;
#if SB_URP
            SetTier(_tier);
#endif
        }

        // Battle arena grade (postfx2 lutPresetForChapter: chapter % 10 themes, the Titan caldera).
        // night: Hard mode and the routes' night stops: a cooler, slightly less saturated grade.
        public static void SetArena(int chapter, bool titan, bool battle, bool night = false)
        {
#if SB_URP
            Ensure();
            var g = !battle ? Neutral : titan ? Titan : Themes[Mathf.Clamp(Sigilbound.Core.GameData.ThemeOf(chapter), 0, Themes.Length - 1)];
            float temp = g.Temp - (night ? 0.14f : 0), sat = g.Sat * (night ? 0.9f : 1), contrast = g.Contrast + (night ? 0.04f : 0);
            _wb.temperature.Override(temp * 60f); _wb.tint.Override(g.Tint * 60f);
            _color.saturation.Override((sat - 1) * 100f); _color.contrast.Override(contrast * 100f);
            _split.shadows.Override(night ? Toned(228, g.ShAmt + 0.05f) : Toned(g.ShHue, g.ShAmt)); _split.highlights.Override(Toned(g.HiHue, g.HiAmt)); _split.balance.Override(g.Balance * 100f);
#endif
        }
        // SplitToning colours: grey is neutral; push toward the hue by the preset's amount.
        static Color Toned(float hue, float amount) => Color.Lerp(Color.grey, Color.HSVToRGB(hue / 360f, 1f, 1f), Mathf.Clamp01(amount * 2.5f));
    }
}
