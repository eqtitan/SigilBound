// The arena biomes (docs/sigilbound/art/SCENERY.md section 3; was the theme table in Client/Scenery.cs): colours,
// what grows or stands there, the backdrop style, fog and mood, plus the procedural scene generator's data per
// biome (Core/SceneGen.cs, CAMPAIGN_SCENES_PLAN.md): terrain relief, the scatter / mid-ground / framing / skyline
// tables and what thickens toward the lair. Plain data, so the generator and its tests run outside Unity.
using System.Collections.Generic;

namespace Sigilbound.Core
{
    // One entry of a placement table: a kit mesh prefix (oak -> oak0, oak1 ...), its weight, scale range and whether it
    // sways on boss stages.
    public readonly struct Pick
    {
        public readonly string Prefix; public readonly float W, Lo, Hi; public readonly bool Sway;
        public Pick(string prefix, float w, float lo, float hi, bool sway = false) { Prefix = prefix; W = w; Lo = lo; Hi = hi; Sway = sway; }
    }

    public sealed class Biome
    {
        public string Name, Grass, Grass2, Worn, Hill, Rock, Bark, Tuft, Cloth, Sil, Water, Motes, MoteColor; public string[] Leaves, Strata; public bool Crystals, Mushrooms;
        // Act 2 scatter: pier posts and crates, glowing lantern posts, bare dead trees, reeds, standing stones and barrow
        // mounds; and glowing leaves (the ember groves).
        public bool Posts, Lanterns, DeadTrees, Reeds, Stones, GlowLeaves;
        // Painted backdrop and mood: ridge style (hills, forest, peaks, mesas, sea, ruins, dunes, volcano), worked ruins
        // (flagstones, columns, walls), storm (lightning on boss stages), the boss stages' vent eruptions, the fog range.
        public string Style = "hills", Vent = "dust"; public bool Ruins, Storm;
        public float FogNear = 26, FogFar = 64, Clouds = 0.5f;
        public bool Pines; // conifers only (the Stormcrown crag)
        // Sky top, horizon/fog, ground and prop tint (was Palette.Scene).
        public string SkyTop, SkyBottom, SceneGround, SceneProp;
        // Generator: the height of the rolling ground outside the hero lane (metres, 0 = flat).
        public float Relief = 1.2f;

        public bool HasWater => Water != null;
    }

    public static class Biomes
    {
        // scenery.mjs THEMES (0-14, GameData.ThemeOf), then the glass desert (15) and the Stormcrown crag (16).
        static readonly Biome[] Table =
        {
            new Biome { Name = "dusk forest", Grass = "#4f6a2a", Grass2 = "#6f7432", Worn = "#8a6844", Hill = "#41582a", Rock = "#7d746a", Bark = "#4e3726", Leaves = new[] { "#3c6a26", "#557a2a", "#a8702a", "#b8522a" }, Tuft = "#5f7a2c", Cloth = "#7a2a22", Sil = "#2e2c46", Motes = "leaves", MoteColor = "#e08a3a", Style = "forest",
                        SkyTop = "#1f2b46", SkyBottom = "#c9805a", SceneGround = "#2a2a22", SceneProp = "#16141f", Relief = 1.4f },
            new Biome { Name = "teal shore", Grass = "#c2a87e", Grass2 = "#b39b72", Worn = "#8c7a5e", Hill = "#a8956a", Rock = "#6a7078", Bark = "#9a8a76", Leaves = new[] { "#4a6a3a", "#5a7a40" }, Tuft = "#8f9a52", Water = "#1f5f6c", Cloth = "#d8d0b8", Sil = "#24404e", Motes = "spray", MoteColor = "#e8f6ff", Style = "sea", Vent = "water",
                        SkyTop = "#16283a", SkyBottom = "#4f7c8a", SceneGround = "#1c2a2a", SceneProp = "#0e1a22", Relief = 0.9f },
            new Biome { Name = "red badlands", Grass = "#94503a", Grass2 = "#ad6844", Worn = "#c09070", Hill = "#8a4632", Rock = "#a0553a", Bark = "#5e4636", Strata = new[] { "#8a4430", "#b06a46", "#6e3426", "#c4885a" }, Tuft = "#857040", Cloth = "#d0a040", Sil = "#44201f", Motes = "dust", MoteColor = "#ffc890", Style = "mesas", FogNear = 30, FogFar = 72, Clouds = 0.3f,
                        SkyTop = "#2a1a1e", SkyBottom = "#a8483a", SceneGround = "#2e1a1c", SceneProp = "#160c0e", Relief = 2.2f },
            new Biome { Name = "green marsh", Grass = "#3a5630", Grass2 = "#4f6236", Worn = "#6e5a40", Hill = "#34502e", Rock = "#58605a", Bark = "#433a30", Leaves = new[] { "#4a5a30" }, Tuft = "#7a8a44", Water = "#22402f", Cloth = "#5a4a30", Sil = "#223c36", Motes = "fireflies", MoteColor = "#d8ff7a", Style = "hills", Vent = "water", FogNear = 20, FogFar = 56,
                        SkyTop = "#1a2e2a", SkyBottom = "#7fa088", SceneGround = "#23362a", SceneProp = "#101c16", Relief = 0.7f },
            new Biome { Name = "gold ruins", Grass = "#58652f", Grass2 = "#76763c", Worn = "#8e8474", Hill = "#4c5a2c", Rock = "#a49a88", Bark = "#5a4636", Leaves = new[] { "#4c682c", "#667a32" }, Tuft = "#6a7a34", Cloth = "#7a2a4a", Sil = "#36304c", Motes = "sparks", MoteColor = "#ffd27a", Crystals = true, Style = "ruins", Ruins = true, Vent = "crystal",
                        SkyTop = "#28203a", SkyBottom = "#c8a870", SceneGround = "#2c2640", SceneProp = "#14101e", Relief = 1.3f },
            new Biome { Name = "blossom forest", Grass = "#557636", Grass2 = "#748646", Worn = "#977660", Hill = "#4a6a34", Rock = "#8a8088", Bark = "#4a3036", Leaves = new[] { "#e89ac0", "#f0b0cc", "#d880aa", "#f8cade" }, Tuft = "#6a8a3c", Cloth = "#5a3a7a", Sil = "#463858", Motes = "petals", MoteColor = "#ffc0dc", Style = "forest", Clouds = 0.4f,
                        SkyTop = "#2a2438", SkyBottom = "#e0b0d0", SceneGround = "#3a2e4a", SceneProp = "#ffd0f0", Relief = 1.4f },
            new Biome { Name = "moonlit shore", Grass = "#a39c8c", Grass2 = "#958e80", Worn = "#77705f", Hill = "#8a8474", Rock = "#5c6272", Bark = "#8a8478", Leaves = new[] { "#3a5a4a" }, Tuft = "#6a7a5a", Water = "#1a3658", Cloth = "#c8d0e0", Sil = "#1a2642", Motes = "spray", MoteColor = "#cfe2ff", Style = "sea", Vent = "water",
                        SkyTop = "#101a2e", SkyBottom = "#5a7ab0", SceneGround = "#1a2440", SceneProp = "#0c1220", Relief = 0.9f },
            new Biome { Name = "amethyst badlands", Grass = "#5e4e6c", Grass2 = "#6f5c80", Worn = "#9584a6", Hill = "#54466a", Rock = "#5e4c70", Bark = "#4a3c4e", Strata = new[] { "#56466c", "#6f5c86", "#463a5a", "#88729e" }, Tuft = "#6a5a7a", Cloth = "#3a2a5a", Sil = "#221a36", Motes = "sparks", MoteColor = "#c890ff", Crystals = true, Style = "mesas", Vent = "crystal", FogNear = 28, FogFar = 70,
                        SkyTop = "#0e0a16", SkyBottom = "#3a2a5a", SceneGround = "#180f26", SceneProp = "#b080ff", Relief = 2.0f },
            new Biome { Name = "glow marsh", Grass = "#2c4846", Grass2 = "#385850", Worn = "#5e586c", Hill = "#284242", Rock = "#4a5460", Bark = "#38384a", Leaves = new[] { "#2a4a4a" }, Tuft = "#4a7a6a", Water = "#123040", Cloth = "#2a3a5a", Sil = "#122838", Motes = "fireflies", MoteColor = "#80e8ff", Mushrooms = true, Style = "hills", Vent = "spores", FogNear = 20, FogFar = 56,
                        SkyTop = "#0e1a2a", SkyBottom = "#2a5a7a", SceneGround = "#10283c", SceneProp = "#80e0ff", Relief = 0.7f },
            new Biome { Name = "burning ruins", Grass = "#665834", Grass2 = "#7a6238", Worn = "#8a7a6a", Hill = "#5a4a30", Rock = "#7a6e64", Bark = "#4a3a2e", Leaves = new[] { "#5a5a2a", "#7a5a26" }, Tuft = "#7a6a3a", Cloth = "#6a1a1a", Sil = "#381410", Motes = "embers", MoteColor = "#ff8a3a", Crystals = true, Style = "ruins", Ruins = true, Vent = "lava", Clouds = 0.6f,
                        SkyTop = "#1a0a0a", SkyBottom = "#ff6a2a", SceneGround = "#24100c", SceneProp = "#100606", Relief = 1.3f },
            // Act 2 (GameData.ThemeOf: chapters 11-15).
            new Biome { Name = "saltreach harbor", Grass = "#8a8070", Grass2 = "#9a907c", Worn = "#6e6658", Hill = "#7a7262", Rock = "#5a5c62", Bark = "#5a4632", Tuft = "#7a7a5a", Water = "#163a4a", Cloth = "#c8b890", Sil = "#1e2c3a", Motes = "spray", MoteColor = "#e0f0ff", Posts = true, Lanterns = true, Style = "sea", Vent = "water",
                        SkyTop = "#1a2436", SkyBottom = "#c89060", SceneGround = "#22262a", SceneProp = "#ffcf70", Relief = 0.6f },
            new Biome { Name = "black fens", Grass = "#1e2a20", Grass2 = "#26322a", Worn = "#3a3a30", Hill = "#1a2620", Rock = "#3a4038", Bark = "#2a2620", Tuft = "#3a4a30", Water = "#0a1410", Cloth = "#2a3020", Sil = "#0e1812", Motes = "fireflies", MoteColor = "#b0ff6a", DeadTrees = true, Reeds = true, Mushrooms = true, Style = "forest", Vent = "spores", FogNear = 16, FogFar = 50, Clouds = 0.6f,
                        SkyTop = "#080e0a", SkyBottom = "#2a3a28", SceneGround = "#0e160e", SceneProp = "#b0ff6a", Relief = 0.6f },
            new Biome { Name = "wraithmoor barrows", Grass = "#56584a", Grass2 = "#626452", Worn = "#7a7868", Hill = "#4a4c40", Rock = "#6a6c70", Bark = "#3a3632", Tuft = "#6a6a52", Cloth = "#3a3a4a", Sil = "#202230", Motes = "dust", MoteColor = "#d8e0f0", Stones = true, DeadTrees = true, Style = "hills", FogNear = 18, FogFar = 54, Clouds = 0.65f,
                        SkyTop = "#1a1c26", SkyBottom = "#8a8e9a", SceneGround = "#24262c", SceneProp = "#d8e0f0", Relief = 1.6f },
            new Biome { Name = "ember groves", Grass = "#3a3230", Grass2 = "#4a3c34", Worn = "#5a4a40", Hill = "#34302e", Rock = "#4a4440", Bark = "#2a2220", Leaves = new[] { "#ff7a2a", "#ffaa3a", "#e04a1a" }, Tuft = "#5a4a3a", Cloth = "#6a1a10", Sil = "#2a0e0a", Motes = "embers", MoteColor = "#ff9a3a", GlowLeaves = true, Style = "forest", Vent = "lava",
                        SkyTop = "#140806", SkyBottom = "#c04a1a", SceneGround = "#1e100c", SceneProp = "#ff9a3a", Relief = 1.2f },
            new Biome { Name = "storm coast", Grass = "#4a5052", Grass2 = "#565c5c", Worn = "#6a6a66", Hill = "#3e4446", Rock = "#3a3e46", Bark = "#3a3432", Tuft = "#5a6456", Water = "#0e2234", Cloth = "#8a9aa8", Sil = "#0e1622", Motes = "rain", MoteColor = "#b8d0ff", Stones = true, Style = "peaks", Storm = true, Vent = "water", FogNear = 20, FogFar = 58, Clouds = 0.8f,
                        SkyTop = "#0a1018", SkyBottom = "#3a4a5e", SceneGround = "#141a22", SceneProp = "#b8d0ff", Relief = 1.1f },
            // 15, 16 (GameData.GlassTheme, CragTheme): their own lands for The Glass Wastes and Stormcrown Heights.
            new Biome { Name = "glass desert", Grass = "#b8aa8a", Grass2 = "#c8bc9c", Worn = "#9a8e76", Hill = "#a89a7c", Rock = "#8a8478", Bark = "#5a4e44", Tuft = "#9a9070", Cloth = "#6a8aa0", Sil = "#3a4458", Motes = "sparks", MoteColor = "#c8f4ff", Crystals = true, DeadTrees = true, Style = "dunes", Vent = "crystal", FogNear = 30, FogFar = 74, Clouds = 0.3f,
                        SkyTop = "#2a3a4e", SkyBottom = "#d8c8a8", SceneGround = "#3a3630", SceneProp = "#a8f0ff", Relief = 2.0f },
            new Biome { Name = "stormcrown crag", Grass = "#5a6260", Grass2 = "#6a706c", Worn = "#7a7a74", Hill = "#4a5052", Rock = "#5a5e66", Bark = "#3a3430", Leaves = new[] { "#2e4a3a", "#3a5240" }, Tuft = "#6a7062", Cloth = "#4a5a7a", Sil = "#141c2c", Motes = "dust", MoteColor = "#e8f0ff", Stones = true, Pines = true, Style = "peaks", Storm = true, Vent = "dust", FogNear = 22, FogFar = 60, Clouds = 0.75f,
                        SkyTop = "#0c1424", SkyBottom = "#6a7a92", SceneGround = "#1a2030", SceneProp = "#c8dcff", Relief = 2.4f },
        };
        public static readonly Biome Titan = new Biome { Name = "titan caldera", Grass = "#3a3436", Grass2 = "#463e3e", Worn = "#6a625e", Hill = "#332e30", Rock = "#2e2a2e", Bark = "#2a2222", Tuft = "#3a3030", Cloth = "#5a1a10", Sil = "#1e0c0a", Motes = "embers", MoteColor = "#ff7a2a", Style = "volcano", Storm = true, Vent = "lava", Clouds = 0.6f,
                                                         SkyTop = "#1a0a0a", SkyBottom = "#c0401a", SceneGround = "#24100c", SceneProp = "#100606", Relief = 0f };

        public static int Count => Table.Length;
        public static Biome ByIndex(int i) => Table[System.Math.Max(0, System.Math.Min(Table.Length - 1, i))];
        public static Biome Of(int chapter, bool titan = false) => titan ? Titan : ByIndex(GameData.ThemeOf(chapter));

        // What grows or stands in a biome: the scatter table (kit prefix, weight, scale range, sways on boss stages).
        public static List<Pick> Scatter(Biome b)
        {
            var l = new List<Pick> { new Pick("rock", 3f, 0.7f, 1.8f), new Pick("tuft", 1.2f, 0.8f, 1.3f) };
            if (b.Leaves != null && b.Pines) { l.Add(new Pick("pine", 3.5f, 0.8f, 1.3f, true)); l.Add(new Pick("bush", 0.6f, 0.7f, 1.0f, true)); }
            else if (b.Leaves != null) { l.Add(new Pick("oak", 3f, 0.8f, 1.3f, true)); l.Add(new Pick("bush", 1.5f, 0.8f, 1.3f, true)); if (!b.GlowLeaves && b.Style == "forest") l.Add(new Pick("pine", 1.5f, 0.8f, 1.2f, true)); }
            if (b.DeadTrees) l.Add(new Pick("dead", 2.5f, 0.8f, 1.3f, true));
            if (b.Strata != null) l.Add(new Pick("mesa", 2.5f, 1.0f, 1.8f));
            if (b.Crystals) l.Add(new Pick("crystal", 1.5f, 0.8f, 1.5f));
            if (b.Mushrooms) l.Add(new Pick("mushrooms", 2f, 0.9f, 1.6f));
            if (b.Reeds || b.Water != null && b.Style != "sea") l.Add(new Pick("reeds", 2f, 0.8f, 1.2f, true));
            if (b.Stones) { l.Add(new Pick("stone", 2f, 0.8f, 1.3f)); l.Add(new Pick("mound", 0.6f, 0.8f, 1.2f)); }
            if (b.Posts) { l.Add(new Pick("post", 2f, 0.9f, 1.2f)); l.Add(new Pick("crate", 1.2f, 0.8f, 1.2f)); }
            if (b.Lanterns) l.Add(new Pick("lantern", 1.5f, 0.9f, 1.1f));
            if (b.Ruins) { l.Add(new Pick("column", 2f, 0.9f, 1.2f)); l.Add(new Pick("wall", 1.5f, 0.9f, 1.2f)); l.Add(new Pick("arch", 0.5f, 1f, 1.2f)); l.Add(new Pick("banner_pole", 0.4f, 1f, 1.1f)); l.Add(new Pick("brazier", 0.4f, 1f, 1.1f)); }
            if (b.Style == "sea" && !b.Posts) l.Add(new Pick("post", 0.6f, 0.9f, 1.2f)); // driftwood stakes on the shore
            return l;
        }

        // Mid-ground (10-17 m out): the biome's big shapes, larger than the scatter, so the middle band has mass.
        public static List<Pick> Mid(Biome b)
        {
            var l = new List<Pick>();
            if (b.Leaves != null && b.Pines) l.Add(new Pick("pine", 3f, 1.2f, 1.6f, true));
            else if (b.Leaves != null) { l.Add(new Pick("oak", 3f, 1.15f, 1.5f, true)); if (b.Style == "forest" && !b.GlowLeaves) l.Add(new Pick("pine", 1.5f, 1.2f, 1.5f, true)); }
            if (b.DeadTrees) l.Add(new Pick("dead", 2f, 1.2f, 1.6f, true));
            if (b.Strata != null) l.Add(new Pick("mesa", 3f, 1.6f, 2.4f));
            if (b.Ruins) { l.Add(new Pick("wall", 2f, 1.1f, 1.4f)); l.Add(new Pick("column", 1.5f, 1.1f, 1.4f)); l.Add(new Pick("arch", 0.8f, 1.1f, 1.3f)); }
            if (b.Stones) { l.Add(new Pick("stone", 2f, 1.2f, 1.6f)); l.Add(new Pick("mound", 1f, 1.2f, 1.6f)); }
            if (b.Crystals) l.Add(new Pick("crystal", 1f, 1.4f, 2.0f));
            if (b.Posts) l.Add(new Pick("crate", 1f, 1.0f, 1.3f));
            l.Add(new Pick("rock", 1.5f, 1.6f, 2.4f));
            return l;
        }

        // Foreground framing (the flanks nearest the camera, only ever at the screen's edges): the tallest shapes.
        public static List<Pick> Frame(Biome b)
        {
            var l = new List<Pick>();
            foreach (var p in Mid(b)) if (p.Prefix == "oak" || p.Prefix == "pine" || p.Prefix == "dead" || p.Prefix == "mesa" || p.Prefix == "column" || p.Prefix == "stone" || p.Prefix == "crystal") l.Add(new Pick(p.Prefix, p.W, p.Lo * 1.05f, p.Hi * 1.1f, p.Sway));
            l.Add(new Pick("rock", 1f, 1.8f, 2.6f));
            return l;
        }

        // Skyline (22-27 m, just before the near ridge card, painted with the far ramp): big silhouettes against the
        // backdrop so the horizon is the biome's own, not only the painted ridge strip.
        public static List<Pick> Skyline(Biome b) => b.Style switch
        {
            "forest" => b.DeadTrees && b.Leaves == null ? new List<Pick> { new Pick("dead", 2f, 2.0f, 2.8f) } : new List<Pick> { new Pick("oak", 2f, 2.0f, 2.6f), new Pick(b.GlowLeaves ? "oak" : "pine", 1.5f, 2.2f, 3.0f) },
            "mesas" => new List<Pick> { new Pick("mesa", 3f, 3.0f, 4.5f) },
            "ruins" => new List<Pick> { new Pick("column", 2f, 2.0f, 2.6f), new Pick("wall", 1.5f, 1.8f, 2.4f), new Pick("arch", 1f, 2.0f, 2.4f) },
            "peaks" => b.Pines ? new List<Pick> { new Pick("pine", 3f, 2.2f, 3.0f), new Pick("stone", 1f, 2.2f, 2.8f) } : new List<Pick> { new Pick("stone", 2f, 2.2f, 3.0f), new Pick("rock", 2f, 3.0f, 4.0f) },
            "dunes" => new List<Pick> { new Pick("crystal", 1.5f, 2.6f, 3.4f), new Pick("dead", 1f, 2.0f, 2.6f), new Pick("rock", 1f, 3.0f, 4.0f) },
            "sea" => new List<Pick> { new Pick("rock", 2f, 3.0f, 4.2f) },
            _ => b.DeadTrees ? new List<Pick> { new Pick("dead", 2f, 2.0f, 2.6f), new Pick("stone", 1f, 2.0f, 2.6f) }
                             : b.Leaves != null ? new List<Pick> { new Pick("oak", 2f, 2.0f, 2.6f) } : new List<Pick> { new Pick("rock", 2f, 3.0f, 4.0f) },
        };

        // Toward the lair (stage 6): what thickens along the route, weighted by the stage's progress (0 at the first
        // stop, 1 at the lair): fallen stone, dead wood, the boss's banners.
        public static List<Pick> Approach(Biome b)
        {
            var l = new List<Pick> { new Pick("debris", 2f, 2.5f, 4f), new Pick("rock", 1f, 1.2f, 2f) };
            if (b.Leaves != null && !b.GlowLeaves || b.DeadTrees) l.Add(new Pick("dead", 1.5f, 0.9f, 1.3f, true));
            if (b.Stones || b.Ruins) l.Add(new Pick("stone", 1f, 0.9f, 1.2f));
            return l;
        }
    }
}
