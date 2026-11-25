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
        }
    }
}
