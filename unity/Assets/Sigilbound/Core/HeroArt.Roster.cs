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

            // Step 24 (Roadwardens 4).
            KitTable["fennick"] = new Dictionary<string, object> { ["band"] = "#ffd040", ["belt"] = true };
            KitTable["orla"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#2a5050", ["belt"] = "#a8e0c8" };
            KitTable["silas"] = new Dictionary<string, object> { ["shield"] = "tower", ["pack"] = false };
            KitTable["wynn"] = new Dictionary<string, object> { ["belt"] = true, ["back"] = "quiver" };
            BodyTable["fennick"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.92d, Head = 1.05d };
            BodyTable["orla"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 1.0d };
            BodyTable["silas"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 1.02d, Bulk = new[] { 1.1d, 1d, 1.1d } };
            BodyTable["wynn"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.0d };
            FaceTable["fennick"] = new FaceSpec { Brow = 4d, Mouth = "smile", Iris = "#d86a2a" };
            FaceTable["orla"] = new FaceSpec { Brow = -10d, Mouth = "smile", Iris = "#5a8a6a" };
            FaceTable["silas"] = new FaceSpec { Brow = 12d, Mouth = "frown", Iris = "#8a7aff" };
            FaceTable["wynn"] = new FaceSpec { Brow = 6d, Mouth = "flat", Iris = "#c8a040" };

            // Step 26 (Glass and Storm 1).
            KitTable["kestrel"] = new Dictionary<string, object> { ["band"] = "#e0f0ff", ["pennant"] = "#c0e8ff", ["belt"] = true };
            KitTable["sorrel"] = new Dictionary<string, object> { ["cape"] = "#2a1a2a", ["pack"] = false };
            KitTable["amber"] = new Dictionary<string, object> { ["shield"] = "round", ["studs"] = true, ["hair"] = "braid" };
            KitTable["tobias"] = new Dictionary<string, object> { ["belt"] = "#ffd080", ["pack"] = true };
            BodyTable["kestrel"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.02d };
            BodyTable["sorrel"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.06d, Bulk = new[] { 1.25d, 1.1d, 1.2d }, Arms = 1.2d };
            BodyTable["amber"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.98d };
            BodyTable["tobias"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.0d };
            FaceTable["kestrel"] = new FaceSpec { Brow = 10d, Mouth = "flat", Iris = "#6a9aca" };
            FaceTable["sorrel"] = new FaceSpec { Brow = 6d, Mouth = "frown", Iris = "#6aaa4a" };
            FaceTable["amber"] = new FaceSpec { Brow = 4d, Mouth = "smile", Iris = "#d88030" };
            FaceTable["tobias"] = new FaceSpec { Brow = -6d, Mouth = "smile", Iris = "#8a6a3a" };

            // Step 27 (Glass and Storm 2).
            KitTable["nerys"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#2a4a6a", ["belt"] = "#e0f4ff" };
            KitTable["hale"] = new Dictionary<string, object> { ["cape"] = "#8a3a1a", ["pack"] = false };
            KitTable["juno"] = new Dictionary<string, object> { ["belt"] = "#ffb030", ["pack"] = false };
            KitTable["pike"] = new Dictionary<string, object> { ["band"] = "#c8d8e0", ["belt"] = true };
            BodyTable["nerys"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.98d };
            BodyTable["hale"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.06d, Bulk = new[] { 1.2d, 1.05d, 1.15d }, Arms = 1.15d };
            BodyTable["juno"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.97d };
            BodyTable["pike"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 1.0d };
            FaceTable["nerys"] = new FaceSpec { Brow = -10d, Mouth = "flat", Iris = "#7ac8f0" };
            FaceTable["hale"] = new FaceSpec { Brow = -4d, Mouth = "smile", Iris = "#c8a040" };
            FaceTable["juno"] = new FaceSpec { Brow = 14d, Mouth = "smirk", Iris = "#ff8a2a" };
            FaceTable["pike"] = new FaceSpec { Brow = 12d, Mouth = "smirk", Iris = "#6a8a9a" };

            // Step 28 (Glass and Storm 3).
            KitTable["rhea"] = new Dictionary<string, object> { ["shield"] = "round", ["cape"] = "#5a6a8a", ["pack"] = false };
            KitTable["corwin"] = new Dictionary<string, object> { ["coat"] = "#1a1428", ["belt"] = true };
            KitTable["astrid"] = new Dictionary<string, object> { ["cape"] = "#2a4a6a", ["hair"] = "ponytail", ["pack"] = false };
            KitTable["caspian"] = new Dictionary<string, object> { ["cape"] = "#0e3a48", ["pack"] = false };
            BodyTable["rhea"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.0d };
            BodyTable["corwin"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 1.03d };
            BodyTable["astrid"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.04d };
            BodyTable["caspian"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 1.08d, Bulk = new[] { 1.15d, 1.05d, 1.1d } };
            FaceTable["rhea"] = new FaceSpec { Brow = 6d, Mouth = "flat", Iris = "#e8c860" };
            FaceTable["corwin"] = new FaceSpec { Brow = 16d, Mouth = "smirk", Iris = "#c070ff" };
            FaceTable["astrid"] = new FaceSpec { Brow = 8d, Mouth = "smile", Iris = "#8ad0f0" };
            FaceTable["caspian"] = new FaceSpec { Brow = 6d, Mouth = "flat", Iris = "#2ad0d0" };

            // Step 29 (Glass and Storm 4).
            KitTable["ignis"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#3a0e08", ["belt"] = "#ffc040", ["pack"] = false };
            KitTable["lyra"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#8aa0c8", ["belt"] = "#e0f0ff" };
            KitTable["malachar"] = new Dictionary<string, object> { ["shield"] = "tower", ["cape"] = "#140e1a", ["pack"] = false };
            KitTable["rowan"] = new Dictionary<string, object> { ["shield"] = "round", ["cape"] = "#1e3418", ["pack"] = false };
            BodyTable["ignis"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.03d };
            BodyTable["lyra"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 1.0d };
            BodyTable["malachar"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.1d, Bulk = new[] { 1.2d, 1.05d, 1.2d }, Arms = 1.15d };
            BodyTable["rowan"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.05d, Bulk = new[] { 1.15d, 1.05d, 1.1d } };
            FaceTable["ignis"] = new FaceSpec { Brow = 8d, Mouth = "smile", Iris = "#ffa030" };
            FaceTable["lyra"] = new FaceSpec { Brow = -10d, Mouth = "flat", Iris = "#a8d0ff" };
            FaceTable["malachar"] = new FaceSpec { Brow = 16d, Mouth = "frown", Iris = "#b080ff" };
            FaceTable["rowan"] = new FaceSpec { Brow = -2d, Mouth = "flat", Iris = "#6a8a3a" };

            // Step 30 (Glass and Storm 5).
            KitTable["selene"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#1a2448", ["belt"] = "#a8c8ff" };
            KitTable["brann"] = new Dictionary<string, object> { ["pack"] = false, ["belt"] = "#ff8a30" };
            KitTable["ysra"] = new Dictionary<string, object> { ["belt"] = "#ff6ac8", ["pack"] = false };
            KitTable["oswin"] = new Dictionary<string, object> { ["pennant"] = "#ffd860", ["belt"] = true };
            BodyTable["selene"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 1.02d };
            BodyTable["brann"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, Bald = true, H = 1.08d, Bulk = new[] { 1.3d, 1.1d, 1.25d }, Arms = 1.3d };
            BodyTable["ysra"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.98d };
            BodyTable["oswin"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.05d };
            FaceTable["selene"] = new FaceSpec { Brow = -10d, Mouth = "flat", Iris = "#a8c8ff" };
            FaceTable["brann"] = new FaceSpec { Brow = 18d, Mouth = "smirk", Iris = "#ff8a30" };
            FaceTable["ysra"] = new FaceSpec { Brow = 14d, Mouth = "smirk", Iris = "#ff6ac8" };
            FaceTable["oswin"] = new FaceSpec { Brow = 8d, Mouth = "flat", Iris = "#e8c860" };

            // Step 32 (Hollow Unbound 1).
            KitTable["grimsby"] = new Dictionary<string, object> { ["coat"] = "#2a2630", ["belt"] = true };
            KitTable["marrow"] = new Dictionary<string, object> { ["cape"] = "#2a2622", ["pack"] = false };
            KitTable["ember"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#c8b090", ["belt"] = "#ffb050" };
            KitTable["quill"] = new Dictionary<string, object> { ["belt"] = "#e8d8a0", ["pack"] = true };
            BodyTable["grimsby"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.96d };
            BodyTable["marrow"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.15d, Bulk = new[] { 1.35d, 1.1d, 1.3d }, Arms = 1.35d, Legs = 1.2d };
            BodyTable["ember"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.97d };
            BodyTable["quill"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.98d };
            FaceTable["grimsby"] = new FaceSpec { Brow = 10d, Mouth = "flat", Iris = "#b8a8ff" };
            FaceTable["marrow"] = new FaceSpec { Brow = 18d, Mouth = "frown", Iris = "#ff7a2a" };
            FaceTable["ember"] = new FaceSpec { Brow = -10d, Mouth = "smile", Iris = "#d89040" };
            FaceTable["quill"] = new FaceSpec { Brow = 4d, Mouth = "flat", Iris = "#5a6a8a" };

            // Step 33 (Hollow Unbound 2).
            KitTable["drift"] = new Dictionary<string, object> { ["band"] = "#e8f8ff", ["belt"] = true };
            KitTable["sprig"] = new Dictionary<string, object> { ["hair"] = "braid", ["belt"] = "#e8d870" };
            KitTable["tallow"] = new Dictionary<string, object> { ["shield"] = "round", ["cape"] = "#a04a1a", ["pack"] = false };
            KitTable["hollis"] = new Dictionary<string, object> { ["belt"] = "#2a2a2a", ["pack"] = false };
            BodyTable["drift"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 0.92d, Head = 1.05d };
            BodyTable["sprig"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 0.86d, Head = 1.1d };
            BodyTable["tallow"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.0d, Bulk = new[] { 1.1d, 1d, 1.1d } };
            BodyTable["hollis"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 1.04d, Bulk = new[] { 1.15d, 1.05d, 1.1d } };
            FaceTable["drift"] = new FaceSpec { Brow = 6d, Mouth = "smile", Iris = "#7ac8f0" };
            FaceTable["sprig"] = new FaceSpec { Brow = -8d, Mouth = "smile", Iris = "#6aa03a" };
            FaceTable["tallow"] = new FaceSpec { Brow = 8d, Mouth = "flat", Iris = "#a06a2a" };
            FaceTable["hollis"] = new FaceSpec { Brow = 4d, Mouth = "flat", Iris = "#6a8a6a" };

            // Step 35 (Hollow Unbound 3).
            KitTable["auren"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#4a8070", ["belt"] = "#e0fff0" };
            KitTable["cinder"] = new Dictionary<string, object> { ["belt"] = true, ["pack"] = false };
            KitTable["thessaly"] = new Dictionary<string, object> { ["shield"] = "round", ["cape"] = "#123a48", ["pack"] = false };
            KitTable["umbra"] = new Dictionary<string, object> { ["robe"] = "long", ["robeColor"] = "#1a1a28", ["belt"] = "#9a8aff" };
            BodyTable["auren"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "hood" }, H = 1.0d };
            BodyTable["cinder"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], Shave = true, H = 1.04d };
            BodyTable["thessaly"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new[] { "crown" }, H = 1.06d, Bulk = new[] { 1.1d, 1d, 1.1d } };
            BodyTable["umbra"] = new BodySpec { Parts = new Dictionary<string, string>(), Hide = new string[0], H = 1.0d };
            FaceTable["auren"] = new FaceSpec { Brow = -8d, Mouth = "smile", Iris = "#7ad0b0" };
            FaceTable["cinder"] = new FaceSpec { Brow = 16d, Mouth = "frown", Iris = "#ff9030" };
            FaceTable["thessaly"] = new FaceSpec { Brow = 8d, Mouth = "flat", Iris = "#5ac0c8" };
            FaceTable["umbra"] = new FaceSpec { Brow = 10d, Mouth = "flat", Iris = "#b0a0ff" };
        }
    }
}
