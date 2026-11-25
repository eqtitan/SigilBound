// Runtime-build art for the Unity-only roster heroes (docs/sigilbound/ROSTER.md), added to the
// tables generated from the web game (HeroArt.Generated.cs, which must not be edited by hand).
// These heroes normally show as baked free-path models (Tools~/models/heroes); these entries shape
// the runtime-built fallback. Anything missing falls back to the defaults in HeroArt.Kit/Body/Face.
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public static partial class HeroArt
    {
        static HeroArt()
        {
            // Step 21 (Roadwardens 1).
            KitTable["bram"] = new Dictionary<string, object> { ["pack"] = false, ["belt"] = "#2a1e16" };
            KitTable["nettle"] = new Dictionary<string, object> { ["band"] = "#e8c84a", ["belt"] = true };
            KitTable["hobb"] = new Dictionary<string, object> { ["shield"] = "round", ["studs"] = true, ["belt"] = "#ffd870" };
            KitTable["greta"] = new Dictionary<string, object> { ["hair"] = "braid", ["belt"] = "#e8d8b0" };
            BodyTable["bram"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.05d, Bulk = new[] { 1.2d, 1.05d, 1.2d }, Arms = 1.2d };
            BodyTable["nettle"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.92d, Head = 1.05d };
            BodyTable["hobb"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.0d, Bulk = new[] { 1.1d, 1d, 1.1d } };
            BodyTable["greta"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.96d };
            FaceTable["bram"] = new FaceSpec { Brow = -4d, Mouth = "smile", Iris = "#3a5a6a" };
            FaceTable["nettle"] = new FaceSpec { Brow = 10d, Mouth = "smirk", Iris = "#6a8a2a" };
            FaceTable["hobb"] = new FaceSpec { Brow = 6d, Mouth = "flat", Iris = "#5a3a1a" };
            FaceTable["greta"] = new FaceSpec { Brow = -6d, Mouth = "smile", Iris = "#3a6a7a" };

            // Step 22 (Roadwardens 2).
            KitTable["cole"] = new Dictionary<string, object> { ["belt"] = true, ["pack"] = false };
            KitTable["tilly"] = new Dictionary<string, object> { ["robe"] = "short", ["robeColor"] = "#2a2238", ["belt"] = "#c8a8ff" };
            KitTable["ronan"] = new Dictionary<string, object> { ["pack"] = false, ["belt"] = "#3a2a1a" };
            KitTable["isla"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#c8c0a8", ["belt"] = "#ffd870" };
            BodyTable["cole"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.0d, Bulk = new[] { 1.1d, 1d, 1.05d }, Arms = 1.1d };
            BodyTable["tilly"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.92d, Head = 1.04d };
            BodyTable["ronan"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.04d, Bulk = new[] { 1.25d, 1.1d, 1.2d } };
            BodyTable["isla"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.97d };
            FaceTable["cole"] = new FaceSpec { Brow = 12d, Mouth = "frown", Iris = "#5a3a2a" };
            FaceTable["tilly"] = new FaceSpec { Brow = -10d, Mouth = "flat", Iris = "#b48cff" };
            FaceTable["ronan"] = new FaceSpec { Brow = -6d, Mouth = "smile", Iris = "#4a6a2a" };
            FaceTable["isla"] = new FaceSpec { Brow = -8d, Mouth = "smile", Iris = "#d8b040" };

            // Step 23 (Roadwardens 3).
            KitTable["dunstan"] = new Dictionary<string, object> { ["shield"] = "tower", ["cape"] = "#2a4a2a", ["pack"] = false };
            KitTable["marisol"] = new Dictionary<string, object> { ["hair"] = "ponytail", ["belt"] = "#7ae0ff" };
            KitTable["vesper"] = new Dictionary<string, object> { ["belt"] = true, ["pack"] = false };
            KitTable["brega"] = new Dictionary<string, object> { ["shield"] = "round", ["studs"] = true, ["pack"] = false };
            BodyTable["dunstan"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.04d, Bulk = new[] { 1.1d, 1d, 1.1d } };
            BodyTable["marisol"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.98d };
            BodyTable["vesper"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 0.96d };
            BodyTable["brega"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.0d, Bulk = new[] { 1.15d, 1.05d, 1.15d } };
            FaceTable["dunstan"] = new FaceSpec { Brow = 10d, Mouth = "frown", Iris = "#5a6a4a" };
            FaceTable["marisol"] = new FaceSpec { Brow = 12d, Mouth = "smirk", Iris = "#2ad0ff" };
            FaceTable["vesper"] = new FaceSpec { Brow = 14d, Mouth = "flat", Iris = "#7ad07a" };
            FaceTable["brega"] = new FaceSpec { Brow = 8d, Mouth = "flat", Iris = "#4a8a8a" };
        }
    }
}
