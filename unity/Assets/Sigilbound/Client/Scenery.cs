// Battle arenas after game3d/scenery.mjs: the ten chapter themes, plus five act 2 themes (harbor
// posts and lanterns, fen dead trees and reeds, barrow stones and mounds, glowing ember groves, storm-coast rain) (same colours, scaled like the web
// rig), a two-tone ground with a worn patch around the rune circle and a winding dirt path, water or
// lava where the theme has them, scatter (grass tufts, rocks, trees with themed leaves, crystals,
// mushrooms, badland strata), dark silhouettes on the horizon, and drifting motes (leaves, spray,
// dust, fireflies, sparks, petals, embers). Everything is built in code, deterministic per chapter.
// The scenery pass (SCENERY.md): props come from the baked Blender kit (SceneryKit) painted by one ramp
// per theme, the sky, ridges and ground are painted in code (SceneryPaint), and boss stages get an
// ArenaReactor (ArenaReact.cs) that makes the backdrop answer the fight.
using System.Collections.Generic;
using UnityEngine;
using Sigilbound.Core;

namespace Sigilbound.Client
{
    public static class Scenery
    {
        sealed class Theme
        {
            public string Name, Grass, Grass2, Worn, Hill, Rock, Bark, Tuft, Cloth, Sil, Water, Motes, MoteColor; public string[] Leaves, Strata; public bool Crystals, Mushrooms;
            // Act 2 scatter: pier posts and crates, glowing lantern posts, bare dead trees, reeds,
            // standing stones and barrow mounds; and glowing leaves (the ember groves).
            public bool Posts, Lanterns, DeadTrees, Reeds, Stones, GlowLeaves;
            // Painted backdrop and mood (SCENERY.md): ridge style (hills, forest, peaks, mesas, sea, ruins,
            // volcano), worked ruins (flagstones, columns, walls), storm (lightning on boss stages), the
            // boss stages' vent eruptions, and the fog range.
            public string Style = "hills"; public bool Ruins, Storm; public ArenaReactor.Hazard Vent = ArenaReactor.Hazard.Dust;
            public float FogNear = 26, FogFar = 64, Clouds = 0.5f;
            public bool Pines; // conifers only (the Stormcrown crag)
        }
        // scenery.mjs THEMES and TITAN_THEME.
        static readonly Theme[] Themes =
        {
            new Theme { Name = "dusk forest", Grass = "#4f6a2a", Grass2 = "#6f7432", Worn = "#8a6844", Hill = "#41582a", Rock = "#7d746a", Bark = "#4e3726", Leaves = new[] { "#3c6a26", "#557a2a", "#a8702a", "#b8522a" }, Tuft = "#5f7a2c", Cloth = "#7a2a22", Sil = "#2e2c46", Motes = "leaves", MoteColor = "#e08a3a", Style = "forest" },
            new Theme { Name = "teal shore", Grass = "#c2a87e", Grass2 = "#b39b72", Worn = "#8c7a5e", Hill = "#a8956a", Rock = "#6a7078", Bark = "#9a8a76", Leaves = new[] { "#4a6a3a", "#5a7a40" }, Tuft = "#8f9a52", Water = "#1f5f6c", Cloth = "#d8d0b8", Sil = "#24404e", Motes = "spray", MoteColor = "#e8f6ff", Style = "sea", Vent = ArenaReactor.Hazard.Water },
            new Theme { Name = "red badlands", Grass = "#94503a", Grass2 = "#ad6844", Worn = "#c09070", Hill = "#8a4632", Rock = "#a0553a", Bark = "#5e4636", Strata = new[] { "#8a4430", "#b06a46", "#6e3426", "#c4885a" }, Tuft = "#857040", Cloth = "#d0a040", Sil = "#44201f", Motes = "dust", MoteColor = "#ffc890", Style = "mesas", FogNear = 30, FogFar = 72, Clouds = 0.3f },
            new Theme { Name = "green marsh", Grass = "#3a5630", Grass2 = "#4f6236", Worn = "#6e5a40", Hill = "#34502e", Rock = "#58605a", Bark = "#433a30", Leaves = new[] { "#4a5a30" }, Tuft = "#7a8a44", Water = "#22402f", Cloth = "#5a4a30", Sil = "#223c36", Motes = "fireflies", MoteColor = "#d8ff7a", Style = "hills", Vent = ArenaReactor.Hazard.Water, FogNear = 20, FogFar = 56 },
            new Theme { Name = "gold ruins", Grass = "#58652f", Grass2 = "#76763c", Worn = "#8e8474", Hill = "#4c5a2c", Rock = "#a49a88", Bark = "#5a4636", Leaves = new[] { "#4c682c", "#667a32" }, Tuft = "#6a7a34", Cloth = "#7a2a4a", Sil = "#36304c", Motes = "sparks", MoteColor = "#ffd27a", Crystals = true, Style = "ruins", Ruins = true, Vent = ArenaReactor.Hazard.Crystal },
            new Theme { Name = "blossom forest", Grass = "#557636", Grass2 = "#748646", Worn = "#977660", Hill = "#4a6a34", Rock = "#8a8088", Bark = "#4a3036", Leaves = new[] { "#e89ac0", "#f0b0cc", "#d880aa", "#f8cade" }, Tuft = "#6a8a3c", Cloth = "#5a3a7a", Sil = "#463858", Motes = "petals", MoteColor = "#ffc0dc", Style = "forest", Clouds = 0.4f },
            new Theme { Name = "moonlit shore", Grass = "#a39c8c", Grass2 = "#958e80", Worn = "#77705f", Hill = "#8a8474", Rock = "#5c6272", Bark = "#8a8478", Leaves = new[] { "#3a5a4a" }, Tuft = "#6a7a5a", Water = "#1a3658", Cloth = "#c8d0e0", Sil = "#1a2642", Motes = "spray", MoteColor = "#cfe2ff", Style = "sea", Vent = ArenaReactor.Hazard.Water },
            new Theme { Name = "amethyst badlands", Grass = "#5e4e6c", Grass2 = "#6f5c80", Worn = "#9584a6", Hill = "#54466a", Rock = "#5e4c70", Bark = "#4a3c4e", Strata = new[] { "#56466c", "#6f5c86", "#463a5a", "#88729e" }, Tuft = "#6a5a7a", Cloth = "#3a2a5a", Sil = "#221a36", Motes = "sparks", MoteColor = "#c890ff", Crystals = true, Style = "mesas", Vent = ArenaReactor.Hazard.Crystal, FogNear = 28, FogFar = 70 },
            new Theme { Name = "glow marsh", Grass = "#2c4846", Grass2 = "#385850", Worn = "#5e586c", Hill = "#284242", Rock = "#4a5460", Bark = "#38384a", Leaves = new[] { "#2a4a4a" }, Tuft = "#4a7a6a", Water = "#123040", Cloth = "#2a3a5a", Sil = "#122838", Motes = "fireflies", MoteColor = "#80e8ff", Mushrooms = true, Style = "hills", Vent = ArenaReactor.Hazard.Spores, FogNear = 20, FogFar = 56 },
            new Theme { Name = "burning ruins", Grass = "#665834", Grass2 = "#7a6238", Worn = "#8a7a6a", Hill = "#5a4a30", Rock = "#7a6e64", Bark = "#4a3a2e", Leaves = new[] { "#5a5a2a", "#7a5a26" }, Tuft = "#7a6a3a", Cloth = "#6a1a1a", Sil = "#381410", Motes = "embers", MoteColor = "#ff8a3a", Crystals = true, Style = "ruins", Ruins = true, Vent = ArenaReactor.Hazard.Lava, Clouds = 0.6f },
            // Act 2 (GameData.ThemeOf: chapters 11-15).
            new Theme { Name = "saltreach harbor", Grass = "#8a8070", Grass2 = "#9a907c", Worn = "#6e6658", Hill = "#7a7262", Rock = "#5a5c62", Bark = "#5a4632", Tuft = "#7a7a5a", Water = "#163a4a", Cloth = "#c8b890", Sil = "#1e2c3a", Motes = "spray", MoteColor = "#e0f0ff", Posts = true, Lanterns = true, Style = "sea", Vent = ArenaReactor.Hazard.Water },
            new Theme { Name = "black fens", Grass = "#1e2a20", Grass2 = "#26322a", Worn = "#3a3a30", Hill = "#1a2620", Rock = "#3a4038", Bark = "#2a2620", Tuft = "#3a4a30", Water = "#0a1410", Cloth = "#2a3020", Sil = "#0e1812", Motes = "fireflies", MoteColor = "#b0ff6a", DeadTrees = true, Reeds = true, Mushrooms = true, Style = "forest", Vent = ArenaReactor.Hazard.Spores, FogNear = 16, FogFar = 50, Clouds = 0.6f },
            new Theme { Name = "wraithmoor barrows", Grass = "#56584a", Grass2 = "#626452", Worn = "#7a7868", Hill = "#4a4c40", Rock = "#6a6c70", Bark = "#3a3632", Tuft = "#6a6a52", Cloth = "#3a3a4a", Sil = "#202230", Motes = "dust", MoteColor = "#d8e0f0", Stones = true, DeadTrees = true, Style = "hills", FogNear = 18, FogFar = 54, Clouds = 0.65f },
            new Theme { Name = "ember groves", Grass = "#3a3230", Grass2 = "#4a3c34", Worn = "#5a4a40", Hill = "#34302e", Rock = "#4a4440", Bark = "#2a2220", Leaves = new[] { "#ff7a2a", "#ffaa3a", "#e04a1a" }, Tuft = "#5a4a3a", Cloth = "#6a1a10", Sil = "#2a0e0a", Motes = "embers", MoteColor = "#ff9a3a", GlowLeaves = true, Style = "forest", Vent = ArenaReactor.Hazard.Lava },
            new Theme { Name = "storm coast", Grass = "#4a5052", Grass2 = "#565c5c", Worn = "#6a6a66", Hill = "#3e4446", Rock = "#3a3e46", Bark = "#3a3432", Tuft = "#5a6456", Water = "#0e2234", Cloth = "#8a9aa8", Sil = "#0e1622", Motes = "rain", MoteColor = "#b8d0ff", Stones = true, Style = "peaks", Storm = true, Vent = ArenaReactor.Hazard.Water, FogNear = 20, FogFar = 58, Clouds = 0.8f },
        };
        // Themes 15 and 16 (GameData.GlassTheme, CragTheme): their own lands for The Glass Wastes and Stormcrown Heights.
        static readonly Theme GlassDesert = new Theme { Name = "glass desert", Grass = "#b8aa8a", Grass2 = "#c8bc9c", Worn = "#9a8e76", Hill = "#a89a7c", Rock = "#8a8478", Bark = "#5a4e44", Tuft = "#9a9070", Cloth = "#6a8aa0", Sil = "#3a4458", Motes = "sparks", MoteColor = "#c8f4ff", Crystals = true, DeadTrees = true, Style = "dunes", Vent = ArenaReactor.Hazard.Crystal, FogNear = 30, FogFar = 74, Clouds = 0.3f };
        static readonly Theme StormcrownCrag = new Theme { Name = "stormcrown crag", Grass = "#5a6260", Grass2 = "#6a706c", Worn = "#7a7a74", Hill = "#4a5052", Rock = "#5a5e66", Bark = "#3a3430", Leaves = new[] { "#2e4a3a", "#3a5240" }, Tuft = "#6a7062", Cloth = "#4a5a7a", Sil = "#141c2c", Motes = "dust", MoteColor = "#e8f0ff", Stones = true, Pines = true, Style = "peaks", Storm = true, Vent = ArenaReactor.Hazard.Dust, FogNear = 22, FogFar = 60, Clouds = 0.75f };
        static Theme[] _all;
        static Theme[] All => _all ??= new List<Theme>(Themes) { GlassDesert, StormcrownCrag }.ToArray();
        static readonly Theme TitanTheme = new Theme { Name = "titan caldera", Grass = "#3a3436", Grass2 = "#463e3e", Worn = "#6a625e", Hill = "#332e30", Rock = "#2e2a2e", Bark = "#2a2222", Tuft = "#3a3030", Cloth = "#5a1a10", Sil = "#1e0c0a", Motes = "embers", MoteColor = "#ff7a2a", Style = "volcano", Storm = true, Vent = ArenaReactor.Hazard.Lava, Clouds = 0.6f };
        public static string ThemeName(int chapter, bool titan) => ThemeOf(chapter, titan).Name;
        static Theme ThemeOf(int chapter, bool titan) => titan ? TitanTheme : All[Mathf.Clamp(GameData.ThemeOf(chapter), 0, All.Length - 1)];
        public static Color ShadowColor(int chapter, bool titan) => Palette.Hex(ThemeOf(chapter, titan).Sil);
        public static (float near, float far) FogRange(int chapter, bool titan) { var th = ThemeOf(chapter, titan); return (th.FogNear, th.FogFar); }
        public static bool Stormy(int chapter, bool titan) => ThemeOf(chapter, titan).Storm;

        const float AG = 0.46f, AP = 0.55f; // albedo scaling for the ground and the props (the web's bright rig)
        static Color D(string hex, float k) { var c = Palette.Hex(hex) * k; c.a = 1; return c; }

        // The painted textures for the last arena (they are rebuilt only when the arena changes).
        static string _paintKey; static readonly List<Object> _paint = new List<Object>();
        static Material _groundMat, _waterMat, _ramp, _farRamp, _glow, _fire; static Material[] _backMats; static Mesh[] _arcs;
        static readonly Dictionary<string, Material> _glows = new Dictionary<string, Material>();
        static T Keep<T>(T o) where T : Object { _paint.Add(o); return o; }

        // Builds the arena under `root`. density: scatter density (quality tier). top/fog: the sky's
        // zenith and horizon (Palette.Scene, after the stop's mood). interactive: a boss stage or the Titan
        // (ArenaReactor). set: the campaign stop (Core.StoryScenes) whose set-pieces stand here; night: Hard
        // mode, the same stops by night with lanterns lit.
        public static void Build(Transform root, int chapter, bool titan, float density, Color fog, Color top = default, bool interactive = false, Light sun = null,
                                 StageSet set = null, bool night = false)
        {
            var th = ThemeOf(chapter, titan);
            var rng = new System.Random(titan ? 1337 : 101 + chapter * 7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (top == default) top = Color.Lerp(fog, Color.black, 0.6f);
            Color sunTint = Color.Lerp(fog, Color.white, 0.45f);
            bool kit = SceneryKit.Ready;
            float waterZ = th.Water != null || titan ? 10f : -1f;

            string key = $"{th.Name}/{chapter}/{titan}/{kit}/{ColorUtility.ToHtmlStringRGB(fog)}{ColorUtility.ToHtmlStringRGB(top)}"; // the mood changes sky and ridges
            if (key != _paintKey)
            {
                foreach (var o in _paint) if (o != null) Object.Destroy(o);
                _paint.Clear(); _paintKey = key;
                uint seed = (uint)(titan ? 1337 : 101 + chapter * 7);
                var horizon = Color.Lerp(D(th.Hill, AG), fog * 0.6f, 0.5f);
                var groundTex = Keep(SceneryPaint.Ground(D(th.Grass, AG), D(th.Grass2, AG), D(th.Worn, AG * 0.9f), D(th.Rock, 0.5f), (uint)(chapter % 7), th.Ruins, D(th.Worn, 0.25f), waterZ, horizon));
                _groundMat = Keep(new Material(Materials.Lit(Color.white, 0, 0.08f)) { name = "GroundPaint", mainTexture = groundTex });
                if (_groundMat.HasProperty("_BaseMap")) _groundMat.SetTexture("_BaseMap", groundTex);
                // sky and three ridge layers: painted strips around the arena, pre-hazed and unlit. The nearest
                // ridge card stands at z 28 (x +-30), inside the ground the high menu/gallery and battle cameras see at the
                // top of the frame (they look down ~30 degrees and never see the true horizon), so the field
                // always ends in painted hills; low Titan cameras see the farther ranges and the sky above.
                _arcs = new[] { Keep(SceneryPaint.Backdrop(50, 44, -32, -2, 50)), Keep(SceneryPaint.Backdrop(42, 38, -28, -1.5f, 15)),
                                Keep(SceneryPaint.Backdrop(36, 33, -26, -1.5f, 10)), Keep(SceneryPaint.Backdrop(30, 28, -24, -1.5f, 7)) };
                // water: painted, low gloss (a glossy plane mirrored Unity's default blue sky and a hard sun streak);
                // lava: dark crust with glowing veins, the glow building only far out
                if (titan)
                {
                    var (crust, veins) = SceneryPaint.Lava(seed);
                    _waterMat = Keep(new Material(Materials.Glow(Color.white, Color.white, 1f)) { name = "LavaCrust", mainTexture = Keep(crust) });
                    if (_waterMat.HasProperty("_BaseMap")) _waterMat.SetTexture("_BaseMap", crust);
                    if (_waterMat.HasProperty("_BaseColor")) _waterMat.SetColor("_BaseColor", Color.white);
                    if (_waterMat.HasProperty("_EmissionMap")) { _waterMat.EnableKeyword("_EMISSION"); _waterMat.SetTexture("_EmissionMap", Keep(veins)); _waterMat.SetColor("_EmissionColor", Color.white * 1.1f); }
                    if (_waterMat.HasProperty("_Smoothness")) _waterMat.SetFloat("_Smoothness", 0.15f);
                }
                else if (th.Water != null)
                {
                    var wt = Keep(SceneryPaint.Water(D(th.Water, 0.75f), Color.Lerp(D(th.Water, 1.25f), D(th.Worn, 0.6f), 0.25f), D("#e8f0f0", 0.75f), fog, seed));
                    _waterMat = Keep(new Material(Materials.Lit(Color.white, 0, 0.25f)) { name = "WaterPaint", mainTexture = wt });
                    if (_waterMat.HasProperty("_BaseMap")) _waterMat.SetTexture("_BaseMap", wt);
                }
                _backMats = new Material[4];
                for (int i = 0; i < 4; i++)
                {
                    var tex = Keep(i == 0 ? SceneryPaint.Sky(top, fog, sunTint, seed, th.Clouds, th.Storm) : SceneryPaint.Ridge(i - 1, th.Style, Palette.Hex(th.Sil), fog, sunTint, seed));
                    var m = _backMats[i] = Keep(Materials.Unlit(Color.white)); m.mainTexture = tex;
                    m.renderQueue = 2996 + i; // far to near, before the effects
                    if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                }
                if (kit)
                {
                    var ramp = Keep(SceneryPaint.Ramp(Rows(th), Palette.Hex(th.Sil), sunTint));
                    _ramp = Keep(new Material(Materials.Lit(Color.white, 0, 0.12f)) { name = "SceneryRamp", mainTexture = ramp });
                    if (_ramp.HasProperty("_BaseMap")) _ramp.SetTexture("_BaseMap", ramp);
                    // Landmarks on the horizon: the same painting pushed toward the ridges' shadow colour and darkened,
                    // so after the fog they still read as a silhouette instead of washing out (owner check, build 133).
                    var sil = Palette.Hex(th.Sil); var rows = Rows(th);
                    for (int i = 0; i < rows.Length; i++) { var c = Color.Lerp(rows[i], sil, 0.55f) * 0.7f; c.a = 1; rows[i] = c; }
                    var farRamp = Keep(SceneryPaint.Ramp(rows, sil, Color.Lerp(sil, sunTint, 0.3f)));
                    _farRamp = Keep(new Material(Materials.Lit(Color.white, 0, 0.05f)) { name = "SceneryFarRamp", mainTexture = farRamp });
                    if (_farRamp.HasProperty("_BaseMap")) _farRamp.SetTexture("_BaseMap", farRamp);
                }
                var mote = Palette.Hex(th.MoteColor);
                _glow = Materials.Glow(mote * 0.4f, mote, th.Mushrooms ? 0.9f : 1.2f);
                _fire = Materials.Glow(Palette.Hex("#3a1a08"), Palette.Hex("#ffb050"), 1.8f);
            }

            // ---- sky and distant ridges ----
            for (int i = 0; i < 4; i++) Backdrop(root, i == 0 ? "Sky" : "SkyRidge" + (i - 1), _arcs[i], _backMats[i]);

            // ---- ground: the painted 90 m field and a plain far field under the ridges ----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground"; ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(9, 1, 9);
            ground.GetComponent<Renderer>().sharedMaterial = _groundMat;
            ground.GetComponent<Renderer>().receiveShadows = true;
            var far = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(far.GetComponent<Collider>());
            far.name = "GroundFar"; far.transform.SetParent(root, false);
            far.transform.localPosition = new Vector3(0, -0.03f, 0); far.transform.localScale = new Vector3(30, 1, 30);
            far.GetComponent<Renderer>().sharedMaterial = Materials.Lit(Color.Lerp(D(th.Grass, AG), fog, 0.3f), 0, 0.05f);
            far.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // ---- water / lava beyond the far side ----
            if (waterZ > 0)
            {
                var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.Destroy(w.GetComponent<Collider>());
                w.name = titan ? "Lava" : "Water"; w.transform.SetParent(root, false);
                // stays above the ground through its whole bob (SceneryWave); 90 x 16 m from the shore line at z 10
                w.transform.localPosition = new Vector3(0, titan ? 0.04f : 0.06f, waterZ + 8f);
                w.transform.localScale = new Vector3(9, 1, 1.6f);
                w.GetComponent<Renderer>().sharedMaterial = _waterMat;
                w.GetComponent<Renderer>().receiveShadows = false; // tree shadows popping with the quality tier's shadow distance
                w.AddComponent<SceneryWave>().Lava = titan;
            }

            ArenaReactor react = null;
            if (interactive)
            {
                react = root.gameObject.AddComponent<ArenaReactor>();
                react.Kind = th.Vent; react.Storm = th.Storm; react.NoCracks = titan; // the Titan's own moves crack the ground (SkillFx)
                react.Tint = Palette.Hex(th.MoteColor); react.Ramp = _ramp; react.Sun = sun;
                react.Tier = Quality.I != null ? Quality.I.Tier : 2;
            }

            if (!kit) ScatterSimple(root, th, titan, density, R);
            else
            {
                // ---- scatter (kept off the arena ellipse and the camera side) ----
                var pieces = set?.Pieces ?? new Piece[0];
                bool UnderPiece(float x, float z) { foreach (var pc in pieces) if ((x - pc.X) * (x - pc.X) + (z - pc.Z) * (z - pc.Z) < (pc.R + 0.6f) * (pc.R + 0.6f)) return true; return false; }
                bool Clear(float x, float z) => Mathf.Pow(x / 6.2f, 2) + Mathf.Pow((z - 0.6f) / 5.6f, 2) < 1 || z < -7 || (waterZ > 0 && z > waterZ - 0.6f) || UnderPiece(x, z);
                var table = Picks(th);
                float total = 0; foreach (var p in table) total += p.w;
                int n = Mathf.RoundToInt(70 * density);
                for (int i = 0; i < n; i++)
                {
                    float a = R(0, Mathf.PI * 2), r = R(6.5f, 20.5f), x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r * 0.9f + 4f; // inside the near ridge
                    if (Clear(x, z)) continue;
                    float pick = R(0, total); var e = table[0];
                    foreach (var p in table) { if (pick < p.w) { e = p; break; } pick -= p.w; }
                    var names = SceneryKit.Variants(e.prefix);
                    if (names.Count == 0) continue;
                    var name = names[(int)R(0, names.Count - 0.01f)];
                    var go = Place(root, name, new Vector3(x, 0, z), R(e.lo, e.hi), R(0, 360));
                    if (react != null && e.sway && go != null) { go.AddComponent<SceneryMoves>(); react.Sway.Add(go.transform); }
                    if (name == "banner_pole") Banner(root, go, react);
                    if (name == "brazier" && react != null && go != null) { go.AddComponent<SceneryMoves>(); react.Braziers.Add(go.transform); }
                }
                // grass tufts near the arena edge (they read as texture)
                for (int i = 0; i < Mathf.RoundToInt(60 * density); i++)
                {
                    float a = R(0, Mathf.PI * 2), k = R(1.05f, 1.5f); float x = Mathf.Sin(a) * 6.2f * k, z = 0.6f + Mathf.Cos(a) * 5.6f * k;
                    if (z < -6.5f) continue;
                    Place(root, "tuft" + (int)R(0, 2.99f), new Vector3(x, 0, z), R(0.5f, 0.8f), R(0, 360));
                }
                // ---- the campaign stop: its set-pieces, the landmark ahead or the lair ----
                if (set != null) Stop(root, set, chapter, react);
                // ---- Hard mode by night: lanterns lit along the flanks ----
                if (night && !titan)
                    foreach (var (lx, lz) in new[] { (-7.6f, 3f), (7.6f, 3f), (-8.4f, 9.5f), (8.4f, 9.5f) })
                        if (!UnderPiece(lx, lz)) Place(root, "lantern", new Vector3(lx, 0, lz), 1.05f, 0);
                // ---- boss stages: braziers flanking the foes, war banners behind them, vents beyond ----
                if (react != null)
                {
                    float back = titan ? 9f : 7.8f, bx = titan ? 4.8f : 4.0f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        var b = Place(root, "brazier", new Vector3(sx * 6.7f, 0, 4.4f), 1.1f, R(0, 360));
                        if (b != null) { b.AddComponent<SceneryMoves>(); react.Braziers.Add(b.transform); }
                        Banner(root, Place(root, "banner_pole", new Vector3(sx * bx, 0, back), 1.15f, 0), react);
                    }
                    react.Vents.Add(new Vector3(-6.8f, 0, 8f)); react.Vents.Add(new Vector3(6.8f, 0, 8f));
                    react.Vents.Add(new Vector3(R(-2f, 2f), 0, waterZ > 0 ? waterZ + 0.8f : 11.5f));
                }
            }

            // ---- drifting motes ----
            Motes(root, th, density);
        }

        // An unlit painted arc (sky or ridge): no shadows, never hidden as an occluder ("Sky" names).
        static void Backdrop(Transform root, string name, Mesh mesh, Material m)
        {
            var go = new GameObject(name); go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        // Places a stop's pieces (meshes from the base kit or this act's story pack) and hands the lair's
        // moving parts to the reactor.
        static void Stop(Transform root, StageSet set, int chapter, ArenaReactor react)
        {
            SceneryKit.EnsurePack(GameData.ActOf(chapter) == 1 ? "story_act1" : "story_act2");
            foreach (var pc in set.Pieces)
            {
                var glow = GlowFor(pc);
                bool pulses = react != null && (pc.Fx == LairFx.Chime || pc.Fx == LairFx.Drip || pc.Fx == LairFx.Pulse || pc.Fx == LairFx.Beam || pc.Fx == LairFx.Snuff);
                if (pulses) glow = Keep(new Material(glow)); // its own glow, so the reactor can pulse or snuff just this piece
                var go = SceneryKit.Place(root, pc.Mesh, new Vector3(pc.X, pc.Y, pc.Z), pc.Scale, pc.Yaw, pc.Far ? _farRamp : _ramp, glow);
                if (go == null) { Debug.LogWarning("[scenery] story piece missing: " + pc.Mesh); continue; }
                go.name = "Story_" + pc.Mesh;
                if (pc.Mesh == "banner_pole") Banner(root, go, react);
                if (react == null) continue;
                if (pc.Fx == LairFx.Sway) { go.AddComponent<SceneryMoves>(); react.Sway.Add(go.transform); }
                else if (pc.Fx == LairFx.Vent) react.Vents.Add(go.transform.position);
                else if (pc.Fx != LairFx.None)
                {
                    go.AddComponent<SceneryMoves>();
                    react.Lair.Add(new ArenaReactor.LairPart { T = go.transform, Fx = pc.Fx, Anchor = go.transform.position + Vector3.up * pc.FxY * pc.Scale, Glow = pulses ? glow : null });
                }
            }
        }

        static Material GlowFor(Piece pc)
        {
            string hex = StoryScenes.GlowHex(pc.Glow);
            if (pc.Glow == "fire" || (hex == null && (pc.Mesh == "brazier" || pc.Mesh == "lantern" || pc.Mesh == "torch_post"))) return _fire;
            if (hex == null) return _glow;
            if (!_glows.TryGetValue(hex, out var m) || m == null) { var c = Palette.Hex(hex); _glows[hex] = m = Materials.Glow(c * 0.4f, c, 1.2f); }
            return m;
        }

        static GameObject Place(Transform root, string name, Vector3 p, float s, float yaw)
        {
            bool fire = name == "brazier" || name == "lantern" || name == "torch_post";
            return SceneryKit.Place(root, name, p, s, yaw, _ramp, fire ? _fire : _glow);
        }

        // A banner cloth hanging from the pole's crossbar; sways on boss stages.
        static void Banner(Transform root, GameObject pole, ArenaReactor react)
        {
            if (pole == null) return;
            var cloth = SceneryKit.Place(pole.transform, "banner_cloth", new Vector3(0, 2.85f, 0), 1, 0, _ramp, _glow);
            if (react != null && cloth != null) { cloth.AddComponent<SceneryMoves>(); react.Cloths.Add(cloth.transform); }
        }

        // Material rows of the prop ramp from the theme's colours (make_props.py rows).
        static Color[] Rows(Theme th)
        {
            var r = new Color[SceneryPaint.Rows];
            Color H(string h) => Palette.Hex(h);
            var leaves = th.Leaves ?? new[] { th.Tuft };
            var rock = H(th.Strata != null ? th.Strata[0] : th.Rock);
            r[SceneryKit.Bark] = H(th.Bark); r[SceneryKit.Rock] = rock; r[SceneryKit.Moss] = Color.Lerp(H(th.Tuft), rock, 0.35f);
            for (int i = 0; i < 3; i++) r[SceneryKit.LeafA + i] = H(leaves[i % leaves.Length]) * (th.GlowLeaves ? 1.45f : 1f); // ember groves: leaves that seem lit from within
            r[SceneryKit.Wood] = Color.Lerp(H(th.Bark) * 1.35f, H(th.Worn), 0.3f);
            r[SceneryKit.Stone] = th.Strata != null ? H(th.Strata[2]) : Color.Lerp(H(th.Rock), H(th.Worn), 0.35f) * 1.08f;
            r[SceneryKit.Cloth] = H(th.Cloth); r[SceneryKit.Metal] = H("#3c3a40"); r[SceneryKit.Cap] = H(th.MoteColor) * 0.5f; r[SceneryKit.Stem] = H("#d8cdb0");
            r[SceneryKit.Reed] = Color.Lerp(H(th.Tuft), H(th.Worn), 0.3f); r[SceneryKit.Earth] = th.Strata != null ? H(th.Strata[1]) : H(th.Hill);
            r[SceneryKit.Trim] = H("#c9a24a"); r[SceneryKit.Coal] = H("#2a1a14");
            for (int i = 0; i < r.Length; i++) { var c = r[i] * AP; c.a = 1; r[i] = c; }
            return r;
        }

        // What grows or stands in each theme: (kit prefix, weight, scale range, sways on boss stages).
        static List<(string prefix, float w, float lo, float hi, bool sway)> Picks(Theme th)
        {
            var l = new List<(string, float, float, float, bool)> { ("rock", 3f, 0.7f, 1.8f, false), ("tuft", 1.2f, 0.8f, 1.3f, false) };
            if (th.Leaves != null && th.Pines) { l.Add(("pine", 3.5f, 0.8f, 1.3f, true)); l.Add(("bush", 0.6f, 0.7f, 1.0f, true)); }
            else if (th.Leaves != null) { l.Add(("oak", 3f, 0.8f, 1.3f, true)); l.Add(("bush", 1.5f, 0.8f, 1.3f, true)); if (!th.GlowLeaves && th.Style == "forest") l.Add(("pine", 1.5f, 0.8f, 1.2f, true)); }
            if (th.DeadTrees) l.Add(("dead", 2.5f, 0.8f, 1.3f, true));
            if (th.Strata != null) l.Add(("mesa", 2.5f, 1.0f, 1.8f, false));
            if (th.Crystals) l.Add(("crystal", 1.5f, 0.8f, 1.5f, false));
            if (th.Mushrooms) l.Add(("mushrooms", 2f, 0.9f, 1.6f, false));
            if (th.Reeds || th.Water != null && th.Style != "sea") l.Add(("reeds", 2f, 0.8f, 1.2f, true));
            if (th.Stones) { l.Add(("stone", 2f, 0.8f, 1.3f, false)); l.Add(("mound", 0.6f, 0.8f, 1.2f, false)); }
            if (th.Posts) { l.Add(("post", 2f, 0.9f, 1.2f, false)); l.Add(("crate", 1.2f, 0.8f, 1.2f, false)); }
            if (th.Lanterns) l.Add(("lantern", 1.5f, 0.9f, 1.1f, false));
            if (th.Ruins) { l.Add(("column", 2f, 0.9f, 1.2f, false)); l.Add(("wall", 1.5f, 0.9f, 1.2f, false)); l.Add(("arch", 0.5f, 1f, 1.2f, false)); l.Add(("banner_pole", 0.4f, 1f, 1.1f, false)); l.Add(("brazier", 0.4f, 1f, 1.1f, false)); }
            if (th.Style == "sea" && !th.Posts) l.Add(("post", 0.6f, 0.9f, 1.2f, false)); // driftwood stakes on the shore
            return l;
        }

        // Without the prop kit (missing Resources/Scenery/props.bytes): the original primitive scatter.
        static void ScatterSimple(Transform root, Theme th, bool titan, float density, System.Func<float, float, float> R)
        {
            bool Clear(float x, float z) => Mathf.Pow(x / 6.2f, 2) + Mathf.Pow((z - 0.6f) / 5.6f, 2) < 1 || z < -7;
            int n = Mathf.RoundToInt(70 * density);
            var rock = Materials.Lit(D(th.Rock, AP), 0, 0.15f); var bark = Materials.Lit(D(th.Bark, AP), 0, 0.1f); var tuft = Materials.Lit(D(th.Tuft, AG * 1.1f), 0, 0.1f);
            for (int i = 0; i < n; i++)
            {
                float a = R(0, Mathf.PI * 2), r = R(6.5f, 22f), x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r * 0.9f + 4f;
                if (Clear(x, z)) continue;
                float s = R(0.6f, 1.6f), pick = R(0, 1);
                if (Act2Prop(root, th, new Vector3(x, 0, z), s, pick, bark, rock, R)) continue;
                if (th.Leaves != null && pick < 0.35f) Tree(root, new Vector3(x, 0, z), s, bark, th.Leaves, R, th.GlowLeaves ? th.MoteColor : null);
                else if (pick < 0.6f) Rock(root, new Vector3(x, 0, z), s, rock, R);
                else if (th.Strata != null && pick < 0.8f) Mesa(root, new Vector3(x, 0, z), s, th.Strata, R);
                else if (th.Crystals && pick < 0.75f) Prop(root, PrimitiveType.Cube, new Vector3(x, 0.6f * s, z), new Vector3(0.3f, 1.2f, 0.3f) * s, Materials.Glow(Palette.Hex(th.MoteColor) * 0.4f, Palette.Hex(th.MoteColor), 1.2f), new Vector3(R(-15, 15), 45, R(-15, 15)));
                else if (th.Mushrooms && pick < 0.8f) { Prop(root, PrimitiveType.Cylinder, new Vector3(x, 0.2f * s, z), new Vector3(0.12f, 0.2f, 0.12f) * s, bark); Prop(root, PrimitiveType.Sphere, new Vector3(x, 0.42f * s, z), new Vector3(0.6f, 0.25f, 0.6f) * s, Materials.Glow(Palette.Hex("#103038"), Palette.Hex(th.MoteColor), 0.9f)); }
                else for (int k = 0; k < 3; k++) MeshProp(root, LowPoly.Tuft((int)R(0, LowPoly.Variants - 0.01f)), new Vector3(x + R(-0.4f, 0.4f), 0, z + R(-0.4f, 0.4f)), Vector3.one * 0.45f * s, tuft, new Vector3(0, R(0, 360), 0));
            }
            for (int i = 0; i < Mathf.RoundToInt(60 * density); i++)
            {
                float a = R(0, Mathf.PI * 2), k = R(1.05f, 1.5f); float x = Mathf.Sin(a) * 6.2f * k, z = 0.6f + Mathf.Cos(a) * 5.6f * k;
                if (z < -6.5f) continue;
                MeshProp(root, LowPoly.Tuft((int)R(0, LowPoly.Variants - 0.01f)), new Vector3(x, 0, z), Vector3.one * R(0.22f, 0.32f), tuft, new Vector3(0, R(0, 360), 0));
            }
        }

        static void Prop(Transform root, PrimitiveType t, Vector3 pos, Vector3 scale, Material m, Vector3? euler = null)
        {
            var go = GameObject.CreatePrimitive(t);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos; go.transform.localScale = scale;
            if (euler.HasValue) go.transform.localEulerAngles = euler.Value;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        // Faceted low-poly trees as in scenery.mjs: a broadleaf (tapered trunk, clustered faceted
        // canopy) or, about a third of the time, a pine of stacked cones.
        // Act 2 scatter. Returns true when it placed something for this slot.
        static bool Act2Prop(Transform root, Theme th, Vector3 p, float s, float pick, Material bark, Material rock, System.Func<float, float, float> R)
        {
            if (th.Posts && pick < 0.3f)
            {
                // A pier post, sometimes with a crate stack beside it.
                Prop(root, PrimitiveType.Cylinder, p + Vector3.up * 0.7f * s, new Vector3(0.18f, 0.7f, 0.18f) * s, bark);
                if (R(0, 1) < 0.5f) Prop(root, PrimitiveType.Cube, p + new Vector3(0.5f, 0.25f, 0) * s, Vector3.one * 0.5f * s, Materials.Lit(D(th.Cloth, AP), 0, 0.1f), new Vector3(0, R(0, 90), 0));
                return true;
            }
            if (th.Lanterns && pick < 0.42f)
            {
                Prop(root, PrimitiveType.Cylinder, p + Vector3.up * 1.1f * s, new Vector3(0.07f, 1.1f, 0.07f) * s, bark);
                Prop(root, PrimitiveType.Sphere, p + Vector3.up * 2.25f * s, Vector3.one * 0.26f * s, Materials.Glow(Palette.Hex("#3a2a10"), Palette.Hex("#ffcf70"), 1.6f));
                return true;
            }
            if (th.DeadTrees && pick < 0.3f)
            {
                // A bare, leaning trunk with a few crooked branches.
                float h = R(1.8f, 3.0f) * s, lean = R(-12, 12);
                Prop(root, PrimitiveType.Cylinder, p + Vector3.up * h * 0.5f, new Vector3(0.14f * s, h * 0.5f, 0.14f * s), bark, new Vector3(lean, R(0, 360), 0));
                for (int i = 0; i < 3; i++)
                    Prop(root, PrimitiveType.Cylinder, p + Vector3.up * h * R(0.55f, 0.9f), new Vector3(0.05f, 0.45f, 0.05f) * s, bark, new Vector3(R(35, 70), R(0, 360), 0));
                return true;
            }
            if (th.Reeds && pick < 0.5f)
            {
                var reed = Materials.Lit(D(th.Tuft, AG), 0, 0.1f);
                for (int i = 0; i < 6; i++)
                    Prop(root, PrimitiveType.Cylinder, p + new Vector3(R(-0.4f, 0.4f), 0.5f * s, R(-0.4f, 0.4f)), new Vector3(0.03f, 0.5f * s, 0.03f), reed, new Vector3(R(-10, 10), 0, R(-10, 10)));
                return true;
            }
            if (th.Stones && pick < 0.35f)
            {
                if (R(0, 1) < 0.65f) // a standing stone
                    Prop(root, PrimitiveType.Cube, p + Vector3.up * 0.9f * s, new Vector3(0.5f, 1.8f, 0.35f) * s, rock, new Vector3(R(-6, 6), R(0, 360), R(-6, 6)));
                else // a barrow mound
                    Prop(root, PrimitiveType.Sphere, p, new Vector3(2.4f, 1.0f, 1.8f) * s, Materials.Lit(D(th.Hill, AP), 0, 0.08f));
                return true;
            }
            return false;
        }

        static void Tree(Transform root, Vector3 p, float s, Material bark, string[] leaves, System.Func<float, float, float> R, string glow = null)
        {
            int V() => (int)R(0, LowPoly.Variants - 0.01f);
            if (R(0, 1) < 0.33f)
            {
                var pine = Materials.Lit(D(leaves[0], AP) * 0.8f, 0, 0.08f);
                float h = R(2.6f, 3.6f) * s;
                MeshProp(root, LowPoly.Trunk(6, 0.6f), p, new Vector3(0.16f, 0.8f, 0.16f) * s, bark, new Vector3(0, R(0, 360), 0));
                for (int i = 0; i < 3; i++)
                {
                    float k = 1 - i * 0.28f;
                    MeshProp(root, LowPoly.Cone(7), p + Vector3.up * (0.55f * s + i * h * 0.24f), new Vector3(0.95f * k, h * 0.42f, 0.95f * k) * s, pine, new Vector3(0, R(0, 360), 0));
                }
                return;
            }
            float trunkH = R(1.3f, 1.8f) * s;
            MeshProp(root, LowPoly.Trunk(6, 0.55f), p, new Vector3(0.2f * s, trunkH, 0.2f * s), bark, new Vector3(0, R(0, 360), 0));
            int clumps = (int)R(3, 5.99f);
            for (int i = 0; i < clumps; i++)
            {
                var c = D(leaves[(int)R(0, leaves.Length - 0.01f)], AP);
                float r = R(0.75f, 1.15f) * s;
                var at = p + new Vector3(R(-0.55f, 0.55f) * s, trunkH + R(0.2f, 0.9f) * s, R(-0.55f, 0.55f) * s);
                var leafMat = glow != null ? Materials.Glow(c * 0.5f, Palette.Hex(glow), 0.8f) : Materials.Lit(c, 0, 0.08f); // ember groves: glowing leaves
                MeshProp(root, LowPoly.Blob(V(), 0.12f), at, new Vector3(r, r * R(0.8f, 1f), r), leafMat, new Vector3(R(0, 360), R(0, 360), R(0, 360)));
            }
        }

        // A faceted boulder, half sunk into the ground, sometimes with a smaller one beside it.
        static void Rock(Transform root, Vector3 p, float s, Material rock, System.Func<float, float, float> R)
        {
            int V() => (int)R(0, LowPoly.Variants - 0.01f);
            float w = R(0.45f, 0.7f) * s;
            MeshProp(root, LowPoly.Blob(V(), 0.22f), p + Vector3.up * w * 0.25f, new Vector3(w, w * R(0.55f, 0.8f), w * R(0.8f, 1.1f)), rock, new Vector3(R(-15, 15), R(0, 360), R(-15, 15)));
            if (R(0, 1) < 0.4f)
                MeshProp(root, LowPoly.Blob(V(), 0.25f), p + new Vector3(w * 1.1f, w * 0.1f, R(-0.3f, 0.3f)), Vector3.one * w * 0.45f, rock, new Vector3(R(0, 360), R(0, 360), R(0, 360)));
        }

        static void MeshProp(Transform root, Mesh mesh, Vector3 pos, Vector3 scale, Material m, Vector3 euler)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(root, false);
            go.transform.localPosition = pos; go.transform.localScale = scale; go.transform.localEulerAngles = euler;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
        }

        static void Mesa(Transform root, Vector3 p, float s, string[] strata, System.Func<float, float, float> R)
        {
            float y = 0;
            for (int i = 0; i < 4; i++)
            {
                float h = R(0.3f, 0.6f) * s, w = (1.6f - i * 0.25f) * s;
                Prop(root, PrimitiveType.Cylinder, p + Vector3.up * (y + h / 2), new Vector3(w, h / 2, w * 0.8f), Materials.Lit(D(strata[i % strata.Length], AP), 0, 0.1f));
                y += h;
            }
        }

        // Ground texture: noise between the two grass tones, the worn ellipse around the rune circle,
        // and the dirt path from the camera side into the background (scenery.mjs terrain).
        static Texture2D GroundTexture(Theme th, int chapter)
        {
            const int N = 256; const float extent = 45f; // the plane is 90 m wide
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            Color g1 = D(th.Grass, AG), g2 = D(th.Grass2, AG), worn = D(th.Worn, AG * 0.9f);
            var px = new Color[N * N];
            float[][] path = { new[] { 0.6f, -14f }, new[] { 0.4f, -5f }, new[] { 0f, 0.5f }, new[] { -0.8f, 6.5f }, new[] { -2.6f, 13f }, new[] { -1.2f, 21f }, new[] { 2.6f, 30f }, new[] { 4.5f, 42f } };
            float PathDist(float x, float z)
            {
                float best = 1e9f;
                for (int i = 0; i < path.Length - 1; i++)
                {
                    float ax = path[i][0], az = path[i][1], dx = path[i + 1][0] - ax, dz = path[i + 1][1] - az;
                    float u = Mathf.Clamp01(((x - ax) * dx + (z - az) * dz) / (dx * dx + dz * dz));
                    best = Mathf.Min(best, Mathf.Sqrt(Mathf.Pow(x - ax - dx * u, 2) + Mathf.Pow(z - az - dz * u, 2)));
                }
                return best;
            }
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    // Unity's Plane maps u to -X and v to -Z; invert so world (wx, wz) lines up.
                    float wx = -(x / (N - 1f) * 2 - 1) * extent, wz = -(y / (N - 1f) * 2 - 1) * extent;
                    float n = Mathf.PerlinNoise(wx * 0.18f + chapter * 3.1f, wz * 0.18f) * 0.65f + Mathf.PerlinNoise(wx * 0.9f, wz * 0.9f + chapter) * 0.35f;
                    var c = Color.Lerp(g1, g2, n);
                    float e = Mathf.Sqrt(Mathf.Pow(wx / 5.4f, 2) + Mathf.Pow((wz - 0.6f) / 5.0f, 2));
                    float wornK = Mathf.Clamp01((1.25f - e) / 0.5f) * (0.75f + 0.25f * n);
                    float pathK = Mathf.Clamp01((1.3f - PathDist(wx, wz)) / 0.8f) * 0.8f;
                    c = Color.Lerp(c, worn, Mathf.Max(wornK, pathK));
                    c *= 0.9f + 0.2f * Mathf.PerlinNoise(wx * 3f, wz * 3f);
                    c.a = 1;
                    px[y * N + x] = c;
                }
            t.SetPixels(px); t.Apply(true);
            return t;
        }

        static void Motes(Transform root, Theme th, float density)
        {
            var go = new GameObject("Motes"); go.transform.SetParent(root, false); go.transform.localPosition = new Vector3(0, 2, 2);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.loop = true; main.startLifetime = 8; main.maxParticles = Mathf.RoundToInt(160 * density); main.simulationSpace = ParticleSystemSimulationSpace.World;
            var c = Palette.Hex(th.MoteColor);
            bool glowy = th.Motes == "fireflies" || th.Motes == "sparks" || th.Motes == "embers";
            main.startColor = new ParticleSystem.MinMaxGradient(c, Color.Lerp(c, Color.white, 0.3f));
            main.startSize = th.Motes == "leaves" || th.Motes == "petals" ? new ParticleSystem.MinMaxCurve(0.08f, 0.16f) : new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
            main.startSpeed = 0.2f;
            main.gravityModifier = th.Motes == "embers" || th.Motes == "sparks" ? -0.02f : th.Motes == "leaves" || th.Motes == "petals" ? 0.01f : th.Motes == "rain" ? 0.6f : 0f;
            if (th.Motes == "rain") { main.startLifetime = 1.6f; main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.04f); }
            var em = ps.emission; em.rateOverTime = (th.Motes == "rain" ? 90 : 20) * density;
            var sh = ps.shape; sh.shapeType = ParticleSystemShapeType.Box; sh.scale = new Vector3(16, 4, 12);
            var noise = ps.noise; noise.enabled = true; noise.strength = th.Motes == "fireflies" ? 0.6f : 0.35f; noise.frequency = 0.3f;
            var col = ps.colorOverLifetime; col.enabled = true;
            var g = new Gradient(); g.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) }, new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(glowy ? 1 : 0.8f, 0.2f), new GradientAlphaKey(glowy ? 1 : 0.8f, 0.8f), new GradientAlphaKey(0, 1) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            // Textured, blended particles: without a texture each mote drew as a solid square
            // (the "orange squares" on devices). Leaves and petals get a leaf shape, the rest a soft dot.
            bool leafy = th.Motes == "leaves" || th.Motes == "petals";
            var tex = leafy ? MoteTex(true) : MoteTex(false);
            var add = Shader.Find("Legacy Shaders/Particles/Additive");
            var blend = Shader.Find("Sprites/Default");
            var mat = glowy && add != null ? new Material(add) : blend != null ? new Material(blend) : Materials.Unlit(Color.white);
            mat.mainTexture = tex;
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            r.sharedMaterial = mat;
            ps.Play();
        }

        static Texture2D _dotTex, _leafTex;
        internal static Texture2D MoteTex(bool leaf)
        {
            if (leaf ? _leafTex != null : _dotTex != null) return leaf ? _leafTex : _dotTex;
            const int n = 32;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = leaf ? "mote_leaf" : "mote_dot" };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2 - 1, v = (y + 0.5f) / n * 2 - 1, a;
                    if (leaf)
                    {
                        // A pointed leaf along the diagonal: an ellipse pinched at both ends, with a soft edge.
                        float pu = (u + v) * 0.7071f, pv = (v - u) * 0.7071f;
                        float w = 0.42f * (1 - pu * pu);
                        a = Mathf.Clamp01((w - Mathf.Abs(pv)) * 10f) * Mathf.Clamp01((1 - Mathf.Abs(pu)) * 6f);
                    }
                    else { float d = Mathf.Sqrt(u * u + v * v); a = Mathf.Clamp01(1 - d); a *= a; }
                    px[y * n + x] = new Color(1, 1, 1, a);
                }
            t.SetPixels(px); t.Apply();
            if (leaf) _leafTex = t; else _dotTex = t;
            return t;
        }
    }

    // Marks scenery that moves on boss stages (sway, flames): left out of the arena's static batch.
    public sealed class SceneryMoves : MonoBehaviour { }

    // Gentle swell on water, slow pulse on lava.
    public sealed class SceneryWave : MonoBehaviour
    {
        public bool Lava; Vector3 _p;
        void Start() { _p = transform.localPosition; }
        // Bob amplitude stays below the plane's height above the ground (0.06 water, 0.04 lava): a water plane
        // that bobbed 3 cm from 1.5 cm sank under the ground every few seconds and flickered (owner report).
        void Update() { transform.localPosition = _p + Vector3.up * Mathf.Sin(Time.time * (Lava ? 0.8f : 1.2f)) * (Lava ? 0.01f : 0.025f); }
    }
}
