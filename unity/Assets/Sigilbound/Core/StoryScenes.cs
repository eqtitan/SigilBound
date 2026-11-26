// Story scenes (docs/sigilbound/art/STORY_SCENES.md, PLAN step 85): every campaign chapter is a route.
// Its six stages are six stops along the way, the chapter's landmark seen ahead on the early ones, and
// stage 6 is fought in the boss's lair. Data only: Client/Scenery.cs places the pieces (meshes from
// Resources/Scenery/props.bytes and the per-act story packs built by Tools~/scenery/make_landmarks.py,
// which reads this file between the ACT markers to know which meshes each act needs).
// Coordinates are the arena's: x right, z away from the camera, the rune ring around (0, 0.6).
namespace Sigilbound.Core
{
    // Time of day along the route, inside the chapter's palette. Hard mode is always Night.
    public enum Mood { Day, Late, Golden, Dusk, Night }

    // What a lair piece does on the boss stage (ArenaReactor), and the sound cue it raises.
    public enum LairFx { None, Spin, Swing, Toll, Beam, Flare, Snuff, Strike, Chime, Drip, Pulse, Sway, Vent }

    public sealed class Piece
    {
        public string Mesh;
        public float X, Z, Yaw, Scale = 1, Y;
        public float R = 2;          // footprint radius: keeps the ring and the scatter clear (StoryScenesTests)
        public string Glow;          // glow colour key (StoryScenes.GlowHex) for its glowing parts; null = the theme's
        public bool Far;             // the landmark on the horizon: painted darker so it reads through the fog
        public LairFx Fx;
        public float FxY;            // height of the effect's anchor (flame, lightning target), in metres before scale
    }

    public sealed class StageSet
    {
        public string Name;          // the stop's name, shown in the stage list
        public Mood Mood;
        public Piece[] Pieces;
    }

    public static class StoryScenes
    {
        // Mood -> (sun strength, horizon drift toward the theme's shadow colour, fog distance scale).
        public static (float light, float dusk, float fog) MoodOf(Mood m) => m switch
        {
            Mood.Late => (0.95f, 0.1f, 1f),
            Mood.Golden => (0.9f, 0.2f, 1f),
            Mood.Dusk => (0.85f, 0.35f, 0.95f),
            Mood.Night => (0.65f, 0.5f, 0.8f),
            _ => (1f, 0f, 1f),
        };

        public static string GlowHex(string key) => key switch
        {
            "fire" => "#ffa040", "flame" => "#ff7a2a", "shadow" => "#b070ff", "tide" => "#4ab8ff", "light" => "#ffe07a",
            "gale" => "#7affb0", "hollow" => "#c060ff", "glass" => "#a8f0ff", "rose" => "#ffb860", _ => null,
        };

        // The sound cue each lair behaviour raises (audio adds the files; Sound.PlayAt ignores missing cues).
        public static string CueOf(LairFx fx) => fx switch
        {
            LairFx.Spin => "lairWheel", LairFx.Swing or LairFx.Toll => "lairBell", LairFx.Beam => "lairBeam", LairFx.Flare => "lairPyre",
            LairFx.Snuff => "lairSnuff", LairFx.Strike => "lairThunder", LairFx.Chime => "lairChime", LairFx.Drip => "lairDrip",
            LairFx.Pulse => "lairShard", _ => null,
        };

        static Piece P(string mesh, float x, float z, float yaw = 0, float scale = 1, float r = 2, string glow = null, float y = 0,
                       LairFx fx = LairFx.None, float fxY = 0, bool far = false)
            => new Piece { Mesh = mesh, X = x, Z = z, Yaw = yaw, Scale = scale, R = r, Glow = glow, Y = y, Fx = fx, FxY = fxY, Far = far };
        static StageSet S(string name, Mood mood, params Piece[] pieces) => new StageSet { Name = name, Mood = mood, Pieces = pieces };

        public static StageSet Of(int chapter, int stageInChapter)
        {
            if (chapter < 0 || chapter >= Sets.Length || stageInChapter < 0 || stageInChapter >= Sets[chapter].Length) return null;
            return Sets[chapter][stageInChapter];
        }
        public static string StopName(int stageIndex) => Of(stageIndex / GameData.StagesPerChapter, stageIndex % GameData.StagesPerChapter)?.Name;

        public static readonly StageSet[][] Sets =
        {
            // ACT1-BEGIN
            new[] { // 1. Thornwick Road (dusk forest)
                S("The crossroads", Mood.Day, P("waystone", -5.5f, 7.5f, 20, 1.1f, 1.5f), P("lantern", 5.8f, 6.5f, 0, 1, 1, "fire"), P("fence", 7.5f, 10, 10)),
                S("The hedge lane", Mood.Late, P("hedge", -7.8f, 6, 80, 1.1f, 2.5f), P("hedge", 7.8f, 7, -80, 1.1f, 2.5f), P("hedge", -6, 11.5f, 10, 1, 2.5f)),
                S("The tollhouse", Mood.Late, P("tollhut", -6.5f, 8.5f, 15, 1.1f, 2.5f), P("fence", 6.5f, 10, -10), P("lantern", 6.5f, 5.5f, 0, 1, 1, "fire")),
                S("The ambush", Mood.Golden, P("hedge", 7, 9.5f, -60, 1.1f, 2.5f), P("crate", -6.5f, 7.5f, 20), P("crate", -7.2f, 8.4f, 50, 0.8f), P("post0", -5.5f, 10.5f)),
                S("The Hollow camp", Mood.Dusk, P("camp", 6, 10.5f, -20, 1.1f, 3), P("banner_pole", -5.5f, 9), P("banner_pole", -7.2f, 11.5f), P("wagon", 1.5f, 21, 10, 1.2f, 3, far: true)),
                S("The broken wagon", Mood.Dusk, P("wagon", 0.5f, 11.8f, 6, 1.5f, 3.5f), P("hedge", -6.8f, 11.5f, 15, 1.25f, 3, fx: LairFx.Sway), P("hedge", 7, 12, -15, 1.25f, 3, fx: LairFx.Sway),
                    P("waystone", -4.6f, 9.4f, 20, 1.1f, 1.2f), P("lantern", 4.8f, 9.4f, 0, 1.1f, 1.2f, "fire", fx: LairFx.Flare, fxY: 1.9f)),
            },
            new[] { // 2. The Drowned Mill (teal shore; water beyond z 10)
                S("The flooded road", Mood.Day, P("post0", -6.6f, 6.2f), P("post1", -4.6f, 8.6f), P("post0", 5.4f, 7.2f), P("rowboat", -7.5f, 10.6f, 70, 1, 1.5f),
                    P("drowned_mill", 6, 23, -20, 0.95f, 3, far: true), P("drowned_mill_wheel", 3.8f, 23, 70, 0.95f, 2, y: 0.9f, far: true)),
                S("The millpond reeds", Mood.Late, P("reeds0", -6, 7.5f, 0, 1.3f), P("reeds1", 6.5f, 6.5f, 0, 1.3f), P("reeds0", -4, 10, 40, 1.2f), P("rowboat", -2, 11.6f, 15, 1, 1.5f),
                    P("drowned_mill", 5, 21, -20, 1, 3, far: true), P("drowned_mill_wheel", 2.8f, 21, 70, 1, 2, y: 0.9f, far: true)),
                S("The sluice gate", Mood.Late, P("sluice_gate", -1, 11.6f, 0, 1.2f, 3), P("reeds1", 5, 9, 0, 1.2f),
                    P("drowned_mill", 8.5f, 20, -25, 0.95f, 3, far: true), P("drowned_mill_wheel", 6.3f, 20, 65, 0.95f, 2, y: 0.9f, far: true)),
                S("The miller's cottage", Mood.Golden, P("cottage", -6.5f, 8.5f, 20, 1.1f, 3), P("fence", -7.8f, 5.2f, 70), P("crate", 6, 6.8f, 30),
                    P("drowned_mill", 7, 17.5f, -20, 1, 3), P("drowned_mill_wheel", 4.7f, 17.5f, 70, 1, 2, y: 0.9f)),
                S("The mill yard", Mood.Dusk, P("mill_yard", -4.5f, 10.5f, 10, 1.1f, 3.5f), P("cottage", -10.5f, 13.5f, 40, 0.9f, 3),
                    P("drowned_mill", 6.5f, 15, -15, 1.05f, 3), P("drowned_mill_wheel", 4.1f, 15, 75, 1.05f, 2, y: 0.9f)),
                S("The half-sunk mill", Mood.Dusk, P("drowned_mill", 1, 12.8f, -12, 1.45f, 4.5f), P("drowned_mill_wheel", -2.2f, 12.2f, 78, 1.45f, 2.5f, y: 1.2f, fx: LairFx.Spin),
                    P("rowboat", 6, 10.8f, 30, 1, 1.5f)),
            },
            new[] { // 3. Ashgrave Pass (red badlands)
                S("The foothills", Mood.Day, P("cairn", -6.5f, 9, 0, 0.8f, 1.5f), P("cairn", -3, 22, 0, 1.2f, 2, far: true), P("cairn", 3, 22.5f, 0, 1.2f, 2, far: true)),
                S("The ash slope", Mood.Late, P("dead0", -6, 7.5f), P("dead1", 6.5f, 8), P("scree", 7, 11.5f), P("cairn", -3, 22, 0, 1.2f, 2, far: true), P("cairn", 3, 22.5f, 0, 1.2f, 2, far: true)),
                S("The mourners' camp", Mood.Golden, P("camp", -6, 10, 20, 1.1f, 3), P("banner_pole", 6, 8.5f), P("cairn", -3, 21, 0, 1.2f, 2, far: true), P("cairn", 3, 21.5f, 0, 1.2f, 2, far: true)),
                S("The mourners' bell", Mood.Golden, P("bellpost", 5.8f, 8, -20, 1.1f, 1.5f), P("graves", -7, 9.5f, 10, 0.8f, 3), P("cairn", -3, 20, 0, 1.2f, 2, far: true), P("cairn", 3, 20.5f, 0, 1.2f, 2, far: true)),
                S("The gate cairns", Mood.Dusk, P("cairn", -7.5f, 9.5f, 0, 1.3f, 2), P("cairn", 7.5f, 10, 0, 1.3f, 2), P("graves", 0, 16, 0, 1, 3)),
                S("The graveyard in the pass", Mood.Dusk, P("cairn", -5, 11, 0, 1.6f, 2, fx: LairFx.Vent), P("cairn", 5, 11.5f, 0, 1.6f, 2, fx: LairFx.Vent), P("graves", 0, 12.5f, 0, 1.3f, 3.5f),
                    P("bellpost", 2.8f, 15.5f, -10, 1.2f, 1.5f, fx: LairFx.Toll)),
            },
            new[] { // 4. Saltmarsh Reaches (green marsh; water beyond z 10)
                S("The causeway", Mood.Day, P("plankwalk", -6.5f, 7.5f, 80, 1, 2), P("plankwalk", -7, 10.5f, 70, 1, 2), P("stilt_hut", 4, 22, -10, 1, 2.5f, far: true)),
                S("The eel weirs", Mood.Late, P("fishing_gear", 6.5f, 8, -20, 1, 2), P("reeds0", -6, 7), P("stilt_hut", -3, 21, 10, 1, 2.5f, far: true)),
                S("The drowned boat", Mood.Golden, P("rowboat", -4.5f, 11, 30, 1, 1.5f, y: -0.2f), P("reeds1", 6, 7.5f), P("stilt_hut", 4, 21, -10, 1, 2.5f, far: true)),
                S("The salt pans", Mood.Golden, P("salt_pans", -7, 8, 10, 1, 3), P("stilt_hut", 5, 20, -10, 1, 2.5f, far: true)),
                S("The outer hut", Mood.Dusk, P("stilt_hut", 6.5f, 11, -15, 1.1f, 2.5f), P("plankwalk", -6.5f, 8, 80, 1, 2)),
                S("The stilt village", Mood.Night, P("stilt_hut", -3, 12.5f, 10, 1.4f, 2.5f, fx: LairFx.Sway), P("stilt_hut", 3.8f, 13.5f, -20, 1.25f, 2.5f, fx: LairFx.Sway),
                    P("plankwalk", 0.4f, 13, 0, 1.2f, 2), P("fishing_gear", -7.5f, 9, 20, 1, 2), P("lantern", 7, 9.5f, 0, 1, 1, "fire", fx: LairFx.Flare, fxY: 1.9f)),
            },
            new[] { // 5. Bellspire Ruins (gold ruins)
                S("The old road", Mood.Day, P("column1", -6.5f, 8), P("column2", 7, 10), P("bellspire", 5, 23, 0, 0.9f, 3, far: true)),
                S("The broken wall", Mood.Day, P("wall0", -7, 9, 10, 1, 2.5f), P("wall1", 7, 10, -15, 1, 2.5f), P("bellspire", 5, 22.5f, 0, 0.9f, 3, far: true)),
                S("The market cross", Mood.Late, P("market_cross", -6, 9.5f, 0, 1, 3), P("bellspire", 6, 22, 0, 0.9f, 3, far: true)),
                S("The cloister", Mood.Golden, P("arcade", -7, 10, 80, 1, 3.5f), P("bellspire", 4, 21, 0, 0.95f, 3, far: true)),
                S("The fallen buttress", Mood.Golden, P("buttress", 6, 9, -20, 1.2f, 2.5f), P("bellspire", 4, 17, 0, 1, 3)),
                S("The leaning bell tower", Mood.Dusk, P("bellspire", 1.5f, 12.5f, 0, 1.2f, 3.5f), P("bellspire_bell", 0.2f, 12.5f, 0, 1.2f, 1, y: 9.8f, fx: LairFx.Swing),
                    P("column2", -6.5f, 10), P("wall1", 7, 10.5f, -20, 1, 2.5f)),
            },
            new[] { // 6. The Glass Wastes (glass desert)
                S("The edge of the waste", Mood.Day, P("dead0", -6.5f, 8), P("glass_spire", 6.5f, 9.5f, 0, 0.8f, 1.5f, "glass"), P("glass_wastes", 0, 23, 0, 1, 4, "glass", far: true)),
                S("The fused dunes", Mood.Day, P("glass_spire", 6.5f, 8.5f, 0, 0.9f, 1.5f, "glass"), P("glass_spire", -7, 10, 40, 0.7f, 1.5f, "glass"), P("glass_wastes", 2, 22.5f, 0, 1, 4, "glass", far: true)),
                S("The glassed caravan", Mood.Late, P("wagon", -6.5f, 9.5f, 30, 1, 3), P("glass_spire", -4, 12, 0, 0.6f, 1.2f, "glass"), P("glass_wastes", 3, 22, 0, 1, 4, "glass", far: true)),
                S("The fulgurite field", Mood.Golden, P("fulgurite", -6.5f, 9, 20, 1.2f, 2.5f), P("fulgurite", 7, 11, -30, 1, 2.5f), P("glass_wastes", 0, 21, 0, 1, 4, "glass", far: true)),
                S("The spire gate", Mood.Dusk, P("glass_spire", -5.5f, 11, 0, 1.6f, 2, "glass"), P("glass_spire", 5.5f, 11.5f, 30, 1.6f, 2, "glass"), P("glass_wastes", 0, 18, 0, 0.9f, 4, "glass")),
                S("The singing spires", Mood.Dusk, P("glass_wastes", 0, 12.5f, 0, 1.25f, 4.5f, "glass", fx: LairFx.Chime)),
            },
            new[] { // 7. Stormcrown Heights (stormcrown crag)
                S("The switchback", Mood.Day, P("scree", 6.5f, 9), P("pine0", -6.5f, 8), P("stormcrown", -4, 23, 0, 1.1f, 4, far: true)),
                S("The rope bridge", Mood.Late, P("rope_bridge", -7.5f, 9.5f, 70, 1, 3), P("stormcrown", -3, 22.5f, 0, 1.1f, 4, far: true)),
                S("The shepherds' bothy", Mood.Golden, P("bothy", 6.5f, 9, -20, 1.1f, 2), P("fence", -7, 8, 60), P("stormcrown", -4, 22, 0, 1.1f, 4, far: true)),
                S("The scree field", Mood.Dusk, P("scree", -6.5f, 9, 0, 1.3f, 2.5f), P("scree", 6.5f, 10.5f, 60, 1.1f, 2.5f), P("stormcrown", 2, 21, 0, 1.1f, 4, far: true)),
                S("The storm altar", Mood.Night, P("storm_altar", 6, 9, -15, 1, 2), P("stormcrown", 0, 18, 0, 1.1f, 4)),
                S("The crown crag", Mood.Night, P("stormcrown", 0, 14.5f, 0, 1.45f, 4.5f, fx: LairFx.Strike, fxY: 5.2f)),
            },
            new[] { // 8. Hollow Deep (amethyst badlands)
                S("The mine track", Mood.Day, P("mine_cart", -6.5f, 8.5f, 10, 1, 2.5f), P("hollow_deep", 4, 23, 0, 1, 4, "hollow", far: true)),
                S("The prospectors' camp", Mood.Late, P("camp", 6, 10, -15, 1.1f, 3), P("winch", -7, 8.5f), P("hollow_deep", 3, 22.5f, 0, 1, 4, "hollow", far: true)),
                S("The crystal seam", Mood.Golden, P("crystal0", -6.5f, 8, 0, 1.4f), P("crystal1", 6.5f, 9.5f, 0, 1.3f), P("hollow_deep", 4, 22, 0, 1, 4, "hollow", far: true)),
                S("The sinkhole", Mood.Dusk, P("sinkhole", 6.5f, 10.5f, 0, 1, 2.5f, "hollow"), P("hollow_deep", -3, 21, 0, 1, 4, "hollow", far: true)),
                S("The Hollow-touched", Mood.Night, P("crystal0", -6, 9), P("crystal1", 7, 8), P("sinkhole", -7, 12.5f, 0, 1, 2.5f, "hollow"), P("hollow_deep", 2, 18, 0, 1, 4, "hollow")),
                S("The cavern mouth", Mood.Night, P("hollow_deep", 0, 13.5f, 0, 1.15f, 4.5f, "hollow", fx: LairFx.Drip), P("winch", 7, 9.5f)),
            },
            new[] { // 9. Sunken Cathedral (glow marsh; water beyond z 10)
                S("The pilgrims' road", Mood.Late, P("shrine_post", -5.8f, 7), P("shrine_post", 6, 8.5f), P("sunken_cathedral", -2, 21, 0, 1, 4, "rose", far: true)),
                S("The drowned graves", Mood.Golden, P("graves", -6.5f, 9, 10, 0.9f, 3), P("sunken_cathedral", -2, 20.5f, 0, 1, 4, "rose", far: true)),
                S("The bell in the reeds", Mood.Golden, P("fallen_bell", 6, 9.5f, 0, 1, 1.5f), P("reeds0", 7.5f, 7.5f), P("sunken_cathedral", -3, 20, 0, 1, 4, "rose", far: true)),
                S("The chapter house", Mood.Dusk, P("chapel", -7, 9, 15, 1.1f, 3), P("sunken_cathedral", 2, 20, 0, 1, 4, "rose", far: true)),
                S("The nave steps", Mood.Night, P("nave_steps", 0, 12, 0, 1, 3), P("sunken_cathedral", 0, 17.5f, 0, 1, 4, "rose")),
                S("The flooded nave", Mood.Night, P("sunken_cathedral", 0, 9.6f, 0, 1.3f, 3, "rose", fx: LairFx.Drip)),
            },
            new[] { // 10. The Last Pyre (burning ruins)
                S("The burning road", Mood.Dusk, P("gatehouse", -6.5f, 10, 10, 1, 3), P("last_pyre", 0, 23, 0, 1, 4, "fire", far: true)),
                S("The ash fields", Mood.Dusk, P("brazier", -6.5f, 7, 0, 1, 1, "fire"), P("brazier", 6.5f, 8, 0, 1, 1, "fire"), P("dead0", 7.5f, 11.5f), P("last_pyre", 2, 22.5f, 0, 1, 4, "fire", far: true)),
                S("The torch avenue", Mood.Night, P("torch_post", -6.8f, 6, 0, 1, 1, "fire"), P("torch_post", 6.8f, 6, 0, 1, 1, "fire"), P("torch_post", -7, 9, 0, 1, 1, "fire"),
                    P("torch_post", 7, 9, 0, 1, 1, "fire"), P("torch_post", -7.2f, 12, 0, 1, 1, "fire"), P("torch_post", 7.2f, 12, 0, 1, 1, "fire"), P("last_pyre", 0, 22, 0, 1, 4, "fire", far: true)),
                S("The Hollow host", Mood.Night, P("banner_pole", -5.5f, 9), P("banner_pole", 5.5f, 9.5f), P("camp", -7.5f, 12.5f, 20, 1, 3), P("last_pyre", 2, 21, 0, 1, 4, "fire", far: true)),
                S("The dais steps", Mood.Night, P("torch_post", -6, 9, 0, 1, 1, "fire"), P("torch_post", 6, 9, 0, 1, 1, "fire"), P("last_pyre", 0, 17, 0, 1, 4, "fire")),
                S("The Last Pyre", Mood.Night, P("last_pyre", 0, 13.5f, 0, 1.15f, 4.5f, "fire", fx: LairFx.Flare, fxY: 3.6f),
                    P("torch_post", -6.8f, 8.5f, 0, 1, 1, "fire", fx: LairFx.Flare, fxY: 1.9f), P("torch_post", 6.8f, 8.5f, 0, 1, 1, "fire", fx: LairFx.Flare, fxY: 1.9f)),
            },
            // ACT1-END
            // ACT2-BEGIN
            new[] { // 11. Saltreach Harbor (water beyond z 10)
                S("The city gate", Mood.Day, P("gatehouse", -7, 9.5f, 10, 0.9f, 3), P("lighthouse", 5.5f, 23, 0, 1, 3, far: true), P("lighthouse_lamp", 5.5f, 23, 25, 1, 1, "flame", y: 8.1f, far: true)),
                S("The fish market", Mood.Late, P("market_stall", -6.5f, 7.5f, 20, 1, 1.5f), P("market_stall", 6.5f, 8, -20, 1, 1.5f), P("crate", 6, 9.8f),
                    P("lighthouse", 5.5f, 22.5f, 0, 1, 3, far: true), P("lighthouse_lamp", 5.5f, 22.5f, 25, 1, 1, "flame", y: 8.1f, far: true)),
                S("The warehouses", Mood.Golden, P("warehouse", -8, 8.5f, 15, 1, 3), P("crane", 6.5f, 9, -100, 1, 2.5f),
                    P("lighthouse", 6, 22, 0, 1, 3, far: true), P("lighthouse_lamp", 6, 22, 25, 1, 1, "flame", y: 8.1f, far: true)),
                S("The breakwater", Mood.Dusk, P("post0", -5, 9.6f), P("post1", -3, 9.8f), P("post0", 5, 9.6f), P("rowboat", 4.5f, 11.4f, 40, 1, 1.5f),
                    P("lighthouse", 5, 20, 0, 1, 3, far: true), P("lighthouse_lamp", 5, 20, 25, 1, 1, "flame", y: 8.1f, far: true)),
                S("The pier's end", Mood.Dusk, P("post0", -5.5f, 9.6f), P("crate", -7, 8), P("lighthouse", 5.5f, 17, 0, 1, 3), P("lighthouse_lamp", 5.5f, 17, 25, 1, 1, "flame", y: 8.1f)),
                S("The old lighthouse", Mood.Night, P("lighthouse", 5.5f, 16, 0, 0.75f, 3), P("lighthouse_lamp", 5.5f, 16, 25, 0.75f, 1, "flame", y: 6.1f, fx: LairFx.Beam),
                    P("rowboat", -4, 11, 20, 1, 1.5f), P("post0", -6, 9.6f), P("post1", 6.5f, 9.6f)),
            },
            new[] { // 12. The Black Fens (water beyond z 10)
                S("The last dyke", Mood.Late, P("dead0", -6.5f, 8), P("reeds1", 6.5f, 7.5f), P("drowned_shrine", -3, 21, 0, 1.2f, 3, "shadow", far: true)),
                S("The sunken track", Mood.Golden, P("plankwalk", -6.5f, 8.5f, 70, 1, 2), P("drowned_shrine", 3, 21, 0, 1.2f, 3, "shadow", far: true)),
                S("The hanging trees", Mood.Dusk, P("hanging_tree", -7, 9.5f, 0, 1.1f, 2), P("hanging_tree", 7, 10, 120, 1, 2), P("spirit_lantern", 6, 7.5f, 0, 1, 1),
                    P("drowned_shrine", -2, 20, 0, 1.2f, 3, "shadow", far: true)),
                S("The bog hovel", Mood.Dusk, P("cottage", -7.5f, 9, 25, 1, 3, y: -0.4f), P("drowned_shrine", 3, 20, 0, 1.2f, 3, "shadow", far: true)),
                S("The lantern path", Mood.Night, P("spirit_lantern", -6, 7, 0, 1, 1), P("spirit_lantern", 6, 7, 0, 1, 1), P("spirit_lantern", -5.5f, 10.5f, 0, 1, 1), P("spirit_lantern", 5.5f, 10.5f, 0, 1, 1),
                    P("drowned_shrine", 0, 16, 0, 1.2f, 3, "shadow")),
                S("The drowned shrine", Mood.Night, P("drowned_shrine", 0, 12, 0, 1.65f, 3.5f, "shadow", fx: LairFx.Pulse),
                    P("spirit_lantern", -6.5f, 6.5f, 0, 1, 1, fx: LairFx.Snuff), P("spirit_lantern", 6.5f, 6.5f, 0, 1, 1, fx: LairFx.Snuff), P("spirit_lantern", -6, 9.5f, 0, 1, 1, fx: LairFx.Snuff),
                    P("spirit_lantern", 6, 9.5f, 0, 1, 1, fx: LairFx.Snuff), P("spirit_lantern", -4.5f, 12, 0, 1, 1, fx: LairFx.Snuff), P("spirit_lantern", 4.5f, 12, 0, 1, 1, fx: LairFx.Snuff)),
            },
            new[] { // 13. Wraithmoor Barrows
                S("The moor road", Mood.Day, P("stone0", -6.5f, 7), P("stone1", -7, 10), P("stone0", 7, 9), P("kings_barrow", 0, 23, 0, 1, 5, "tide", far: true)),
                S("The lesser barrows", Mood.Late, P("mound", -8, 9, 20, 1, 3), P("mound", 8, 10.5f, -20, 1, 3), P("kings_barrow", 2, 23, 0, 1, 5, "tide", far: true)),
                S("The stone row", Mood.Golden, P("stone0", -6.5f, 6), P("stone1", -6.5f, 8.5f), P("stone0", -6.5f, 11), P("stone1", -6.5f, 13.5f), P("kings_barrow", 3, 22, 0, 1, 5, "tide", far: true)),
                S("The opened grave", Mood.Dusk, P("opened_grave", 7, 11, -20, 1, 3), P("kings_barrow", -2, 22, 0, 1, 5, "tide", far: true)),
                S("The king's avenue", Mood.Night, P("stone0", -5.5f, 8), P("stone0", 5.5f, 8), P("stone1", -6, 11), P("stone1", 6, 11), P("kings_barrow", 0, 19, 0, 1, 5, "tide")),
                S("The king's barrow", Mood.Night, P("kings_barrow", 0, 14.5f, 0, 1.35f, 5, "tide", fx: LairFx.Pulse)),
            },
            new[] { // 14. The Ember Groves
                S("The grove edge", Mood.Dusk, P("dead0", -6.5f, 8), P("roots", 7, 10, 0, 0.7f, 2), P("great_tree", 3, 23, 0, 1, 4, "light", far: true)),
                S("The charcoal burners", Mood.Dusk, P("kiln", -6.5f, 9, 20, 1, 1.5f), P("kiln", 7, 10, -30, 1, 1.5f), P("great_tree", -3, 23, 0, 1, 4, "light", far: true)),
                S("The burning arch", Mood.Night, P("burning_arch", 0, 13, 0, 1.2f, 3), P("great_tree", 6, 23, 0, 1, 4, "light", far: true)),
                S("The cinder stream", Mood.Night, P("cinder_stream", 6, 11.5f, 20, 1, 3), P("great_tree", -3, 22, 0, 1, 4, "light", far: true)),
                S("The heartwood ring", Mood.Night, P("roots", -6.5f, 10.5f, 0, 1, 2.5f), P("roots", 7, 11, 40, 1, 2.5f), P("great_tree", 0, 18.5f, 0, 1, 4, "light")),
                S("The burning great tree", Mood.Night, P("great_tree", 0, 14.5f, 0, 1.15f, 4.5f, "light", fx: LairFx.Flare, fxY: 7f)),
            },
            new[] { // 15. The Storm Coast (water beyond z 10)
                S("The cliff path", Mood.Day, P("stone0", -6, 6.5f), P("stone1", 6.5f, 8), P("sea_throne", -3, 23, 8, 1, 4, "gale", far: true)),
                S("The wreck", Mood.Late, P("shipwreck", 7, 9.5f, 70, 1, 3), P("crate", -5.5f, 8, 20), P("sea_throne", -6, 23, 15, 1, 4, "gale", far: true)),
                S("The dead beacon", Mood.Golden, P("beacon", -6.5f, 9.5f, 0, 1.1f, 2.5f), P("sea_throne", 5, 23, -10, 1, 4, "gale", far: true)),
                S("The sea stacks", Mood.Dusk, P("sea_stacks", 3, 14.5f, 0, 1, 4), P("post0", -6.5f, 8), P("sea_throne", -6, 22, 10, 1, 4, "gale", far: true)),
                S("The Hollow camp", Mood.Dusk, P("banner_pole", -5.5f, 9), P("banner_pole", 5.5f, 9.5f), P("brazier", -7, 6.5f, 0, 1, 1, "fire"), P("brazier", 7, 6.5f, 0, 1, 1, "fire"),
                    P("crate", -3.5f, 11.5f, 10), P("sea_throne", 1, 19, 0, 1, 4, "gale")),
                S("The sea-cliff throne", Mood.Night, P("sea_throne", 0, 13.5f, 0, 0.72f, 4.5f, "gale", fx: LairFx.Strike, fxY: 9.5f)),
            },
            // ACT2-END
        };
    }
}
