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
        // The biome table moved to Core/Biomes.cs (the scene generator and its tests read it too).
        static ArenaReactor.Hazard HazardOf(string vent) => vent switch
        {
            "water" => ArenaReactor.Hazard.Water, "lava" => ArenaReactor.Hazard.Lava, "crystal" => ArenaReactor.Hazard.Crystal, "spores" => ArenaReactor.Hazard.Spores, _ => ArenaReactor.Hazard.Dust,
        };
        public static string ThemeName(int chapter, bool titan) => ThemeOf(chapter, titan).Name;
        static Biome ThemeOf(int chapter, bool titan) => Biomes.Of(chapter, titan);
        public static Color ShadowColor(int chapter, bool titan) => Palette.Hex(ThemeOf(chapter, titan).Sil);
        public static (float near, float far) FogRange(int chapter, bool titan) { var th = ThemeOf(chapter, titan); return (th.FogNear, th.FogFar); }
        public static bool Stormy(int chapter, bool titan) => ThemeOf(chapter, titan).Storm;

        const float AG = 0.46f, AP = 0.55f; // albedo scaling for the ground and the props (the web's bright rig)
        static Color D(string hex, float k) { var c = Palette.Hex(hex) * k; c.a = 1; return c; }

        // The painted textures for the last arena (they are rebuilt only when the arena changes).
        static string _paintKey; static readonly List<Object> _paint = new List<Object>();
        static Material _groundMat, _waterMat, _ramp, _farRamp, _glow, _fire; static Material[] _backMats; static Mesh[] _arcs;
        static readonly Dictionary<string, Material> _glows = new Dictionary<string, Material>();
        static Color32[] _rampPx, _farRampPx;                                       // the ramps on the CPU, to paint generated landmarks
        static readonly Dictionary<string, Material> _landmarkMats = new Dictionary<string, Material>();
        static T Keep<T>(T o) where T : Object { _paint.Add(o); return o; }

        // Builds the arena under `root`. density: scatter density (quality tier). top/fog: the sky's
        // zenith and horizon (Palette.Scene, after the stop's mood). interactive: a boss stage or the Titan
        // (ArenaReactor). set: the campaign stop (Core.StoryScenes) whose set-pieces stand here; night: Hard
        // mode, the same stops by night with lanterns lit.
        public static void Build(Transform root, int chapter, bool titan, float density, Color fog, Color top = default, bool interactive = false, Light sun = null,
                                 StageSet set = null, bool night = false, SceneLayout layout = null)
        {
            var th = ThemeOf(chapter, titan);
            if (titan) layout = null;   // the Titan keeps its caldera build (Caldera, the scatter loop below)
            float tint = layout?.Light.WorldTint ?? 1f;
            var rng = new System.Random(titan ? 1337 : 101 + chapter * 7);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            if (top == default) top = Color.Lerp(fog, Color.black, 0.6f);
            Color sunTint = Color.Lerp(fog, Color.white, 0.45f);
            bool kit = SceneryKit.Ready;
            float waterZ = th.Water != null || titan ? 10f : -1f;

            string key = $"{th.Name}/{chapter}/{layout?.Stage ?? -1}/{titan}/{kit}/{ColorUtility.ToHtmlStringRGB(fog)}{ColorUtility.ToHtmlStringRGB(top)}"; // per stage: the road and the mood // the mood changes sky and ridges
            if (key != _paintKey)
            {
                foreach (var o in _paint) if (o != null) Object.Destroy(o);
                _paint.Clear(); _paintKey = key; _landmarkMats.Clear();
                uint seed = (uint)(titan ? 1337 : 101 + chapter * 7);
                var horizon = Color.Lerp(D(th.Hill, AG), fog * 0.6f, 0.5f);
                float[] path = null;
                if (layout != null) { path = new float[layout.Path.Count * 2]; for (int i = 0; i < layout.Path.Count; i++) { path[i * 2] = layout.Path[i].x; path[i * 2 + 1] = layout.Path[i].z; } }
                // leaf litter, ash or shingle blotches in the biome's colour, seeded per stage
                Color spots = th.Leaves != null ? D(th.Leaves[th.Leaves.Length - 1], AG) : D(th.Rock, AG); spots.a = layout != null ? 0.35f : 0f;
                var groundTex = Keep(SceneryPaint.Ground(D(th.Grass, AG), D(th.Grass2, AG), D(th.Worn, AG * 0.9f), D(th.Rock, 0.5f), layout != null ? layout.Seed % 97u : (uint)(chapter % 7), th.Ruins, D(th.Worn, 0.25f), waterZ, horizon,
                                                         path: path, world: tint, lane: layout?.Light.LaneTint ?? 1f, lift: layout?.Light.LaneLift ?? 1f, spots: spots));
                _groundMat = Keep(new Material(Materials.Lit(Color.white, 0, 0.08f)) { name = "GroundPaint", mainTexture = groundTex });
                if (_groundMat.HasProperty("_BaseMap")) _groundMat.SetTexture("_BaseMap", groundTex);
                // sky and three ridge layers: painted strips around the arena, pre-hazed and unlit. The nearest
                // ridge card stands at z 28 (x +-30), inside the ground the high menu/gallery and battle cameras see at the
                // top of the frame (they look down ~30 degrees and never see the true horizon), so the field
                // always ends in painted hills; low Titan cameras see the farther ranges and the sky above.
                _arcs = new[] { Keep(SceneryPaint.Backdrop(50, 44, -32, -2, 90, 1.8f)), Keep(SceneryPaint.Backdrop(42, 38, -28, -1.5f, 15)),
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
                    _rampPx = SceneryPaint.RampPixels(Rows(th, tint), Palette.Hex(th.Sil), sunTint);
                    var ramp = Keep(SceneryPaint.Ramp(Rows(th, tint), Palette.Hex(th.Sil), sunTint));
                    _ramp = Keep(new Material(Materials.Lit(Color.white, 0, 0.12f)) { name = "SceneryRamp", mainTexture = ramp });
                    if (_ramp.HasProperty("_BaseMap")) _ramp.SetTexture("_BaseMap", ramp);
                    // Landmarks on the horizon: the same painting pushed toward the ridges' shadow colour and darkened,
                    // so after the fog they still read as a silhouette instead of washing out (owner check, build 133).
                    var sil = Palette.Hex(th.Sil); var rows = Rows(th, tint);
                    for (int i = 0; i < rows.Length; i++) { var c = Color.Lerp(rows[i], sil, 0.55f) * 0.7f; c.a = 1; rows[i] = c; }
                    _farRampPx = SceneryPaint.RampPixels(rows, sil, Color.Lerp(sil, sunTint, 0.3f));
                    var farRamp = Keep(SceneryPaint.Ramp(rows, sil, Color.Lerp(sil, sunTint, 0.3f)));
                    _farRamp = Keep(new Material(Materials.Lit(Color.white, 0, 0.05f)) { name = "SceneryFarRamp", mainTexture = farRamp });
                    if (_farRamp.HasProperty("_BaseMap")) _farRamp.SetTexture("_BaseMap", farRamp);
                }
                var mote = Palette.Hex(th.MoteColor);
                // The glass desert's crystals are glass, not lamps: a pale lilac body with a faint glow, so their facets read.
                // Other crystal lands (gold ruins, amethyst badlands, burning ruins) the same way: a lit body in the mote colour
                // with a soft glow. At 1.2 the bloom burned them to flat white cut-outs (real screenshots, build 182).
                _glow = th.Name == "glass desert" ? Materials.Glow(Palette.Hex("#b4a6d6"), Palette.Hex("#d6c8ff"), 0.22f)
                      : th.Crystals && !th.Mushrooms ? Materials.Glow(mote * 0.7f, mote, 0.3f)
                      : Materials.Glow(mote * 0.4f, mote, th.Mushrooms ? 0.9f : 1.2f);
                _fire = Materials.Glow(Palette.Hex("#3a1a08"), Palette.Hex("#ffb050"), 1.8f);
            }

            // ---- sky and distant ridges ----
            for (int i = 0; i < 4; i++) Backdrop(root, i == 0 ? "Sky" : "SkyRidge" + (i - 1), _arcs[i], _backMats[i]);

            // ---- ground: the painted 90 m field and a plain far field under the ridges ----
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Object.Destroy(ground.GetComponent<Collider>());
            ground.name = "Ground"; ground.transform.SetParent(root, false);
            ground.transform.localScale = new Vector3(9, 1, 9);
            if (layout != null && layout.Relief > 0) Terrain(ground, layout);
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
            if (titan) Caldera(root, th);

            ArenaReactor react = null;
            if (interactive)
            {
                react = root.gameObject.AddComponent<ArenaReactor>();
                react.Kind = HazardOf(th.Vent); react.Storm = th.Storm; react.NoCracks = titan; // the Titan's own moves crack the ground (SkillFx)
                react.Tint = Palette.Hex(th.MoteColor); react.Ramp = _ramp; react.Sun = sun;
                react.Tier = Quality.I != null ? Quality.I.Tier : 2;
            }

            if (!kit) ScatterSimple(root, th, titan, density, R);
            else if (layout != null)
            {
                // ---- the generated scene (Core/SceneGen): every placement already checked against every camera ----
                SceneryKit.EnsurePack(GameData.ActOf(chapter) == 1 ? "story_act1" : "story_act2");
                foreach (var it in layout.Items) PlaceItem(root, it, react);
                if (react != null)
                {
                    react.Vents.Add(new Vector3(-6.8f, 0, 8f)); react.Vents.Add(new Vector3(6.8f, 0, 8f));
                    react.Vents.Add(new Vector3(R(-2f, 2f), 0, waterZ > 0 ? waterZ + 0.8f : 11.5f));
                }
            }
            else
            {
                // ---- scatter (kept off the arena ellipse and the camera side) ----
                var pieces = set?.Pieces ?? new Piece[0];
                bool UnderPiece(float x, float z) { foreach (var pc in pieces) if ((x - pc.X) * (x - pc.X) + (z - pc.Z) * (z - pc.Z) < (pc.R + 0.6f) * (pc.R + 0.6f)) return true; return false; }
                bool Clear(float x, float z) => Mathf.Pow(x / 6.2f, 2) + Mathf.Pow((z - 0.6f) / 5.6f, 2) < 1 || z < -7 || (waterZ > 0 && z > waterZ - 0.6f) || UnderPiece(x, z)
                    || (titan && (InCalderaWall(x, z) || OnLavaVein(x, z)));
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
                for (int i = 0; i < Mathf.RoundToInt(40 * density); i++)
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

        // ---------- the Titan's caldera (SCENERY.md section 4, art review H3) ----------
        // A crater ridge: a ring of basalt wall behind and beside the arena, open toward the camera side, with a
        // thin line of glowing lava at its foot; and two lava-vein strips (the lava lake's crust-and-vein
        // painting) running in from the flanks to the lake, so the floor isn't a flat slab from the low views.
        // The wall is built in short chunks so the occluder pass can hide a chunk that blocks a camera.
        public static readonly Vector3 CalderaCentre = new Vector3(0, 0, 6f);
        public const float CalderaFoot = 17f, CalderaCrest = 19.5f, CalderaOuter = 22f, CalderaFrom = -15f, CalderaTo = 195f;
        static bool InCalderaWall(float x, float z)
        {
            float dx = x - CalderaCentre.x, dz = z - CalderaCentre.z, r = Mathf.Sqrt(dx * dx + dz * dz);
            float a = Mathf.Atan2(dz, dx) * Mathf.Rad2Deg; if (a < -90f) a += 360f;
            return r > CalderaFoot - 0.8f && a > CalderaFrom - 4f && a < CalderaTo + 4f;
        }
        static readonly (float x, float z, float yaw)[] LavaVeins = { (-9.6f, 3.2f, -14f), (9.6f, 3.2f, 14f) };
        const float VeinLen = 15f, VeinWide = 2.2f;
        static bool OnLavaVein(float x, float z)
        {
            foreach (var (vx, vz, yaw) in LavaVeins)
            {
                float c = Mathf.Cos(yaw * Mathf.Deg2Rad), sn = Mathf.Sin(yaw * Mathf.Deg2Rad);
                float lx = (x - vx) * c - (z - vz) * sn, lz = (x - vx) * sn + (z - vz) * c;   // across, along
                if (Mathf.Abs(lx) < VeinWide * 0.5f + 0.5f && Mathf.Abs(lz) < VeinLen * 0.5f) return true;
            }
            return false;
        }
        static void Caldera(Transform root, Biome th)
        {
            var parent = new GameObject("Caldera").transform; parent.SetParent(root, false);
            // Lava veins: the lake's material on long narrow planes (u along the vein), just above the ground.
            foreach (var (vx, vz, yaw) in LavaVeins)
            {
                var v = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Object.Destroy(v.GetComponent<Collider>());
                v.name = "LavaVein"; v.transform.SetParent(parent, false);
                v.transform.localPosition = new Vector3(vx, 0.035f, vz);
                v.transform.localRotation = Quaternion.Euler(0, yaw + 90f, 0);
                v.transform.localScale = new Vector3(VeinLen / 10f, 1, VeinWide / 10f);
                var r = v.GetComponent<Renderer>(); r.sharedMaterial = _waterMat; r.receiveShadows = false;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            // Crater ridge: 14 chunks, each a strip of quads across the wall's profile (foot, shoulder, crest, back).
            var rng = new System.Random(4242);
            var rock = Materials.Lit(Color.Lerp(D(th.Rock, 0.9f), Palette.Hex(th.Sil), 0.35f), 0, 0.1f);
            var glow = Materials.Glow(Palette.Hex("#3a1206"), Palette.Hex("#ff6a1a"), 1.2f);
            const int chunks = 14, per = 5;
            int n = chunks * per;
            var crest = new float[n + 1];
            for (int i = 0; i <= n; i++) crest[i] = 4.2f + 1.6f * (float)rng.NextDouble() + 1.2f * Mathf.Sin(i * 0.37f);
            for (int c = 0; c < chunks; c++)
            {
                var verts = new List<Vector3>(); var tris = new List<int>(); var foot = new List<Vector3>(); var footTris = new List<int>();
                for (int j = 0; j <= per; j++)
                {
                    int i = c * per + j;
                    float a = Mathf.Lerp(CalderaFrom, CalderaTo, i / (float)n) * Mathf.Deg2Rad, h = crest[i];
                    var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                    float jit = ((i * 7919) % 13) / 13f - 0.5f;   // the same for both chunks sharing this edge
                    verts.Add(CalderaCentre + d * CalderaFoot + Vector3.down * 0.15f);
                    verts.Add(CalderaCentre + d * (CalderaFoot + 1.1f + jit * 0.4f) + Vector3.up * h * 0.62f);
                    verts.Add(CalderaCentre + d * (CalderaCrest + jit * 0.5f) + Vector3.up * h);
                    verts.Add(CalderaCentre + d * CalderaOuter + Vector3.up * h * 0.45f);
                    foot.Add(CalderaCentre + d * (CalderaFoot - 0.7f) + Vector3.up * 0.04f);
                    foot.Add(CalderaCentre + d * (CalderaFoot + 0.15f) + Vector3.up * 0.04f);
                    if (j == 0) continue;
                    int b0 = (j - 1) * 4, b1 = j * 4;
                    for (int k = 0; k < 3; k++) { tris.AddRange(new[] { b0 + k, b0 + k + 1, b1 + k }); tris.AddRange(new[] { b1 + k, b0 + k + 1, b1 + k + 1 }); }
                    int f0 = (j - 1) * 2, f1 = j * 2;
                    footTris.AddRange(new[] { f0, f0 + 1, f1, f1, f0 + 1, f1 + 1 });
                }
                Strip(parent, "CalderaRidge" + c, verts, tris, rock, true);
                Strip(parent, "LavaFoot" + c, foot, footTris, glow, false);
            }
        }
        static readonly Dictionary<string, Mesh> _calderaMeshes = new Dictionary<string, Mesh>(); // built once (fixed seed)
        static void Strip(Transform parent, string name, List<Vector3> verts, List<int> tris, Material m, bool shadows)
        {
            if (!_calderaMeshes.TryGetValue(name, out var mesh) || mesh == null) _calderaMeshes[name] = mesh = BuildStrip(name, verts, tris);
            var go = new GameObject(name); go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>(); r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = shadows;
        }
        static Mesh BuildStrip(string name, List<Vector3> verts, List<int> tris)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(verts); mesh.SetTriangles(tris, 0);
            // Every face of the wall's profile (and the flat foot glow) looks upward: wind each triangle so its
            // normal has a positive y (Unity's front face is clockwise seen from the normal's side).
            var t = mesh.triangles;
            for (int i = 0; i < t.Length; i += 3)
            {
                var p0 = verts[t[i]]; var nrm = Vector3.Cross(verts[t[i + 1]] - p0, verts[t[i + 2]] - p0);
                if (nrm.y < 0) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
            }
            mesh.triangles = t; mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }

        // One generated placement: the near or far ramp, the stop's glow, an image-to-3D landmark's own painted texture,
        // and the boss-stage behaviours (sway, flare, toll, spin ...) handed to the reactor.
        static void PlaceItem(Transform root, Placement it, ArenaReactor react)
        {
            bool far = it.Far || it.Layer == Layer.Skyline;
            var pc = new Piece { Mesh = it.Mesh, Glow = it.Glow, Fx = it.Fx, FxY = it.FxY, Scale = it.Scale };
            var glow = GlowFor(pc);
            bool pulses = react != null && (it.Fx == LairFx.Chime || it.Fx == LairFx.Drip || it.Fx == LairFx.Pulse || it.Fx == LairFx.Beam || it.Fx == LairFx.Snuff);
            if (pulses) glow = Keep(new Material(glow));
            var lm = SceneryKit.Landmark(it.Mesh);
            GameObject go;
            if (lm != null)
            {
                string mk = it.Mesh + (far ? "/far" : "");
                if (!_landmarkMats.TryGetValue(mk, out var mat) || mat == null)
                {
                    var gc = Palette.Hex(StoryScenes.GlowHex(it.Glow) ?? "#ffd890");
                    var tex = Keep(SceneryPaint.LandmarkTexture(lm.Shade, lm.Row, lm.W, lm.H, far ? _farRampPx : _rampPx, gc, out var mask));
                    if (mask != null)
                    {
                        // glowing texels: a glow material with the mask as its emission map (the reactor's pulse drives its colour)
                        Keep(mask);
                        mat = Keep(new Material(Materials.Glow(Color.white, gc, far ? 0.6f : 1.1f)) { name = "Landmark_" + it.Mesh, mainTexture = tex });
                        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", Color.white);
                        if (mat.HasProperty("_EmissionMap")) { mat.EnableKeyword("_EMISSION"); mat.SetTexture("_EmissionMap", mask); }
                    }
                    else mat = Keep(new Material(Materials.Lit(Color.white, 0, far ? 0.05f : 0.12f)) { name = "Landmark_" + it.Mesh, mainTexture = tex });
                    if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                    _landmarkMats[mk] = mat;
                }
                go = SceneryKit.PlaceMesh(root, lm.Mesh, it.Mesh, new Vector3(it.X, it.Y, it.Z), it.Scale, it.Yaw, mat);
                if (pulses && mat.HasProperty("_EmissionMap") && mat.GetTexture("_EmissionMap") != null) { mat = Keep(new Material(mat)); go.GetComponent<MeshRenderer>().sharedMaterial = mat; glow = mat; }
            }
            else go = SceneryKit.Place(root, it.Mesh, new Vector3(it.X, it.Y, it.Z), it.Scale, it.Yaw, far ? _farRamp : _ramp, glow);
            if (go == null) { Debug.LogWarning("[scenery] mesh missing: " + it.Mesh); return; }
            bool story = it.Layer == Layer.SetPiece || it.Layer == Layer.Landmark || it.Layer == Layer.LandmarkPart;
            if (story) go.name = "Story_" + it.Mesh;
            if (it.Mesh == "banner_pole") Banner(root, go, react);
            if (react == null) return;
            if (it.Mesh == "brazier") { go.AddComponent<SceneryMoves>(); react.Braziers.Add(go.transform); return; }
            if (it.Fx == LairFx.Sway || (!story && it.Sway)) { go.AddComponent<SceneryMoves>(); react.Sway.Add(go.transform); }
            else if (it.Fx == LairFx.Vent) react.Vents.Add(go.transform.position);
            else if (it.Fx != LairFx.None)
            {
                go.AddComponent<SceneryMoves>();
                react.Lair.Add(new ArenaReactor.LairPart { T = go.transform, Fx = it.Fx, Anchor = go.transform.position + Vector3.up * it.FxY * it.Scale, Glow = pulses ? glow : null });
            }
        }

        // The ground's relief (Core/SceneGen.TerrainHeight): the 90 m field as a grid, flat in the lane, on the road and
        // at the shore. The texture mapping is copied from Unity's Plane (fitted to its own vertices), so the painted
        // ground lines up exactly as before.
        static readonly Dictionary<string, Mesh> _terrain = new Dictionary<string, Mesh>();
        static void Terrain(GameObject ground, SceneLayout layout)
        {
            var mf = ground.GetComponent<MeshFilter>();
            string key = $"{layout.Chapter}/{layout.Stage}/{layout.Tier}";
            if (!_terrain.TryGetValue(key, out var mesh) || mesh == null)
            {
                if (_terrain.Count > 6) { foreach (var m in _terrain.Values) if (m != null) Object.Destroy(m); _terrain.Clear(); }
                // uv = a + b*x + c*z on the plane (it is affine), solved from three of its vertices
                var pv = mf.sharedMesh.vertices; var pu = mf.sharedMesh.uv;
                int i0 = 0, i1 = 0, i2 = 0;
                for (int i = 0; i < pv.Length; i++)
                {
                    if (pv[i].x < pv[i0].x - 1e-3f || (Mathf.Abs(pv[i].x - pv[i0].x) < 1e-3f && pv[i].z < pv[i0].z)) i0 = i;
                }
                for (int i = 0; i < pv.Length; i++) { if (Mathf.Abs(pv[i].z - pv[i0].z) < 1e-3f && pv[i].x > pv[i1].x) i1 = i; }
                i1 = i1 == i0 ? 1 : i1;
                for (int i = 0; i < pv.Length; i++) { if (Mathf.Abs(pv[i].x - pv[i0].x) < 1e-3f && pv[i].z > pv[i2].z) i2 = i; }
                float dxp = pv[i1].x - pv[i0].x, dzp = pv[i2].z - pv[i0].z;
                Vector2 du = (pu[i1] - pu[i0]) / dxp, dv = (pu[i2] - pu[i0]) / dzp;
                int n = SceneGen.TerrainGrid[layout.Tier] + 1;
                var verts = new Vector3[n * n]; var uv = new Vector2[n * n]; var tris = new int[(n - 1) * (n - 1) * 6];
                for (int z = 0; z < n; z++)
                    for (int x = 0; x < n; x++)
                    {
                        float wx = -45f + 90f * x / (n - 1), wz = -45f + 90f * z / (n - 1);
                        float lx = wx / 9f, lz = wz / 9f;   // the plane is scaled 9x
                        verts[z * n + x] = new Vector3(lx, layout.Height(wx, wz), lz);
                        uv[z * n + x] = pu[i0] + du * (lx - pv[i0].x) + dv * (lz - pv[i0].z);
                    }
                int t = 0;
                for (int z = 0; z < n - 1; z++)
                    for (int x = 0; x < n - 1; x++)
                    {
                        int a = z * n + x;
                        tris[t++] = a; tris[t++] = a + n; tris[t++] = a + 1; tris[t++] = a + 1; tris[t++] = a + n; tris[t++] = a + n + 1;
                    }
                mesh = new Mesh { name = "Terrain" };
                mesh.vertices = verts; mesh.uv = uv; mesh.triangles = tris; mesh.RecalculateNormals(); mesh.RecalculateBounds();
                _terrain[key] = mesh;
            }
            // heights are in world metres; the plane's 9x scale is on x and z only
            mf.sharedMesh = mesh;
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
            if (!_glows.TryGetValue(hex, out var m) || m == null)
            {
                var c = Palette.Hex(hex);
                // Glass spires: a pale cyan glass body lit like the rest (darker facets away from the light) with a faint
                // inner glow; the singing flash on crits (ArenaReactor Chime) brightens it briefly. Others: lamp-like glows.
                _glows[hex] = m = pc.Glow == "glass" ? Materials.Glow(Palette.Hex("#8ec6d6"), c, 0.22f) : Materials.Glow(c * 0.4f, c, 1.2f);
            }
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
        static Color[] Rows(Biome th, float tint = 1f)
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
            for (int i = 0; i < r.Length; i++) { var c = r[i] * AP * tint; c.a = 1; r[i] = c; }
            return r;
        }

        // What grows or stands in each theme: (kit prefix, weight, scale range, sways on boss stages).
        static List<(string prefix, float w, float lo, float hi, bool sway)> Picks(Biome th)
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
        static void ScatterSimple(Transform root, Biome th, bool titan, float density, System.Func<float, float, float> R)
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
        static bool Act2Prop(Transform root, Biome th, Vector3 p, float s, float pick, Material bark, Material rock, System.Func<float, float, float> R)
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
        static Texture2D GroundTexture(Biome th, int chapter)
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

        static void Motes(Transform root, Biome th, float density)
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
