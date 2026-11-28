// The procedural campaign scene generator (docs/sigilbound/art/CAMPAIGN_SCENES_PLAN.md, SCENERY.md section 8).
// Stage id -> seed -> a layout: terrain relief, the road toward the lair, the story set-pieces (Core/StoryScenes), the
// landmark framed for the default camera, skyline, mid-ground, framing, approach props, scatter and edge tufts, plus
// the stage's light. Every placement goes through SceneRules.Check against every camera (SceneCameras): nothing in the
// hero lane, nothing blocking it from a guarded view or the lair reveal, foreground only at the screen's edges, and
// nothing no camera can see. Client/Scenery.cs builds the layout; Tests~/SceneGenTests.cs runs it for every stage.
using System;
using System.Collections.Generic;

namespace Sigilbound.Core
{
    public enum Layer { Terrain, Scatter, Tuft, Mid, Frame, Skyline, Approach, SetPiece, Landmark, LandmarkPart, Boss, Lantern }
    public enum Reject { None, Lane, Blocks, Foreground, Unseen, Overlap, Water, Ridge, Reveal, Budget, NoMesh }

    public sealed class Placement
    {
        public string Mesh; public float X, Y, Z, Scale = 1, Yaw; public Layer Layer;
        public string Glow; public bool Far, Sway; public LairFx Fx; public float FxY;
        public bool Moved;            // a story piece the rules moved off its authored spot
        public float R => SceneMeshes.Box(Mesh).R * Scale;
        public SceneCameras.Volume Volume()
        {
            var b = SceneMeshes.Box(Mesh);
            return new SceneCameras.Volume(X, Z, b.R * Scale, b.RTall * Scale, Y + Math.Max(0, b.Y0) * Scale, Y + b.Y1 * Scale);
        }
    }

    // The stage's light (Client/Arena.Build). Hero exposure stays constant (art review M2): the sun and ambient keep a
    // floor and the mood darkens the world's albedo (WorldTint) and sky instead; the lane keeps more of its value.
    public sealed class SceneLight
    {
        public Mood Mood; public bool Night;
        public float Sun = 1.15f, SunPitch = 48, SunYaw = -30, Ambient = 0.55f, WorldTint = 1, LaneTint = 1, Dusk, FogK = 1;
        public float LaneLift = 1;     // the worn ring's albedo is raised by this on very dark biomes (the black fens), so heroes stand on readable ground
        // The visible sun or moon (Client/SkyDisc), always where the light comes from: the direction toward the body
        // as Unity yaw (0 = +Z) and its elevation, which may sit below the light's MinShadowPitch floor.
        public float DiscYaw = 150, DiscElevation = 48; public bool DiscMoon;
        public bool Real;              // lit by the real sun or moon (SkyClock), not the stage's authored time of day
        public bool DesignNight;       // the stage is night by design (story night stops, Hard mode): it stays night
    }

    public sealed class SceneLayout
    {
        public int Chapter, Stage, Tier; public bool Hard, Titan; public uint Seed; public Biome Biome;
        public readonly List<Placement> Items = new List<Placement>();
        public Placement Landmark;            // framed in the default view (the lair's landmark or its far copy ahead)
        public float LandmarkTop;             // its height in metres as placed
        public readonly List<(float x, float z)> Path = new List<(float, float)>(); // the road from the camera side to the landmark
        public SceneLight Light;
        public float WaterZ = -1;
        public List<SceneCam> Cams;
        public readonly Dictionary<Reject, int> Rejects = new Dictionary<Reject, int>();
        public readonly List<string> Dropped = new List<string>();   // story pieces with no legal spot
        public int Tris;
        public float Relief;
        public float Height(float x, float z) => SceneGen.TerrainHeight(this, x, z);
    }

    public static partial class SceneMeshes
    {
        static readonly MeshBox Unknown = new MeshBox(1, 1, 0, 2, 0, "?");
        public static MeshBox Box(string mesh) => mesh != null && All.TryGetValue(mesh, out var b) ? b : Unknown;
        public static bool Has(string mesh) => mesh != null && All.ContainsKey(mesh);
        // Where an image-to-3D landmark's moving parts hang (its own mesh units, before the piece's yaw and scale): the
        // procedural part keeps turning on boss stages, now on the generated building's axle or belfry.
        public static readonly Dictionary<string, (string part, float x, float y, float z, float yaw)> Sockets = new Dictionary<string, (string, float, float, float, float)>
        {
            ["drowned_mill"] = ("drowned_mill_wheel", 2.2f, 0.62f, -0.55f, 90f),
            ["bellspire"] = ("bellspire_bell", -0.3f, 9.4f, -0.1f, 0f),
            ["lighthouse"] = ("lighthouse_lamp", 0.04f, 7.1f, -0.2f, 25f),
        };
        public static bool Generated(string mesh) => mesh != null && All.TryGetValue(mesh, out var b) && b.Pack == "i3d";
        static readonly Dictionary<string, List<string>> _variants = new Dictionary<string, List<string>>();
        // The base kit's meshes that start with `prefix` (oak -> oak0, oak1, oak2).
        public static List<string> Variants(string prefix)
        {
            if (_variants.TryGetValue(prefix, out var l)) return l;
            l = new List<string>();
            foreach (var kv in All) if (kv.Value.Pack == "props" && kv.Key.StartsWith(prefix) && kv.Key.Length > prefix.Length && char.IsDigit(kv.Key[prefix.Length])) l.Add(kv.Key);
            if (l.Count == 0 && All.TryGetValue(prefix, out var one) && one.Pack == "props") l.Add(prefix);
            l.Sort(StringComparer.Ordinal);
            return _variants[prefix] = l;
        }
    }

    public static class SceneRules
    {
        // Checks a placement against the lane, every camera and what is already placed. `cams`: the stage's cameras.
        public static Reject Check(SceneLayout L, Placement p)
        {
            if (!SceneMeshes.Has(p.Mesh)) return Reject.NoMesh;
            var v = p.Volume();
            bool story = p.Layer == Layer.SetPiece || p.Layer == Layer.Landmark || p.Layer == Layer.LandmarkPart;
            // the hero lane (ring + margin) stays clear of every footprint
            if (SceneCameras.InLane(p.X, p.Z, v.R * 0.85)) return Reject.Lane;
            // the near ridge card at z 28 (x +-30) bounds the world
            if (p.Z + (p.Far ? 0 : v.RTall) > 27.5 || Math.Abs(p.X) > 27) return Reject.Ridge;
            // water: only the story's own boats, posts and gates stand in it
            if (L.WaterZ > 0 && !story && p.Layer != Layer.Boss && p.Z > L.WaterZ - 0.6) return Reject.Water;
            // overlap: big things keep apart; scatter keeps off the set-pieces
            foreach (var o in L.Items)
            {
                if (o.Layer == Layer.LandmarkPart || p.Layer == Layer.LandmarkPart) continue;
                double d = Math.Sqrt((o.X - p.X) * (o.X - p.X) + (o.Z - p.Z) * (o.Z - p.Z));
                bool big = o.Layer >= Layer.Mid && p.Layer >= Layer.Mid && o.Layer != Layer.Approach && p.Layer != Layer.Approach;
                bool keepOff = (o.Layer >= Layer.SetPiece || o.Layer == Layer.Boss) && p.Layer <= Layer.Approach;
                if (big && d < (o.R + p.R) * 0.62) return Reject.Overlap;
                if (keepOff && d < o.R * 0.8 + 0.6) return Reject.Overlap;
            }
            bool seen = false;
            foreach (var c in L.Cams)
            {
                if (!seen && SceneCameras.Sees(c, v)) seen = true;
                if (c.Reveal)
                {
                    // the reveal looks at the landmark: nothing but the landmark itself may stand in its sight lines
                    if (p.Layer == Layer.Landmark || p.Layer == Layer.LandmarkPart || L.Landmark == null) continue;
                    if (p.Mesh == L.Landmark.Mesh) continue;
                    foreach (var t in RevealTargets(L)) if (SceneCameras.Blocks(c.View.Eye, t, v)) return Reject.Reveal;
                    continue;
                }
                if (!c.Guard) continue;
                var eye = c.View.Eye;
                if (Dist2(eye, v) < 1.5 * 1.5) return Reject.Blocks;
                // quick out: wholly behind the lane's far side from this eye
                double dc = Math.Sqrt((v.X - eye.X) * (v.X - eye.X) + (v.Z - eye.Z) * (v.Z - eye.Z)) - v.R;
                if (dc < c.LaneFar())
                    foreach (var t in SceneCameras.LanePoints()) if (SceneCameras.Blocks(eye, t, v)) return Reject.Blocks;
                // foreground (nearer than the lane) only in the screen's outer bands
                double near = c.LaneNear();
                foreach (var s in SceneCameras.Samples(v))
                {
                    var q = SceneCameras.Project(c.View, c.Aspect, s);
                    if (q == null || q.Value.depth >= near) continue;
                    if (Math.Abs(q.Value.x) < SceneCameras.EdgeBand && Math.Abs(q.Value.y) <= 1.05) return Reject.Foreground;
                }
            }
            if (!seen && p.Layer != Layer.LandmarkPart) return Reject.Unseen;
            return Reject.None;
        }

        static double Dist2(V3 e, SceneCameras.Volume v)
        {
            double dx = Math.Max(0, Math.Sqrt((e.X - v.X) * (e.X - v.X) + (e.Z - v.Z) * (e.Z - v.Z)) - v.R);
            double dy = e.Y < v.Y0 ? v.Y0 - e.Y : e.Y > v.Y1 ? e.Y - v.Y1 : 0;
            return dx * dx + dy * dy;
        }
        public static IEnumerable<V3> RevealTargets(SceneLayout L)
        {
            var m = L.Landmark; float top = L.LandmarkTop;
            foreach (double k in new[] { 0.15, 0.5, 0.85 })
                foreach (double dx in new[] { -1.2, 0.0, 1.2 }) yield return new V3(m.X + dx, m.Y + top * k, m.Z);
        }

        // How well the landmark sits in the default view's background band at every aspect: 0 = framed; else the summed
        // distance outside (NDC). Its body (35 % of its height, at most 2.5 m up: the default view looks down 30 degrees, so a
        // tall tower's tip rises past the HUD) must be on screen between the enemies' heads and the turn bar, and not
        // squeezed against the left or right edge (art review M1: landmarks cropped or off-frame).
        public static double FrameError(SceneLayout L, Placement m, float top, double margin = 0)
        {
            double err = 0, h = Math.Min(top * 0.35, 2.5);
            foreach (var c in L.Cams)
            {
                if (!c.Default) continue;
                var q = SceneCameras.Project(c.View, c.Aspect, new V3(m.X, m.Y + h, m.Z));
                if (q == null) { err += 10; continue; }
                var (x, y, _) = q.Value;
                double hi = m.Far ? SceneCameras.BandHiFar : SceneCameras.BandHi;   // a copy far ahead may sit right under the turn bar
                err += Math.Max(0, Math.Abs(x) - SceneCameras.BandX + margin) + Math.Max(0, SceneCameras.BandLo + margin - y) + Math.Max(0, y - hi + margin);
                if (!L.Titan && L.Stage == GameData.StagesPerChapter - 1)
                {
                    // the lair, seen through the tilted boss camera (BattleLayout.BossView): its base on screen and the
                    // landmark up to LairShown metres (all of it, for most lairs) under the turn bar
                    var qb = SceneCameras.Project(c.View, c.Aspect, new V3(m.X, m.Y + 0.05, m.Z));
                    var qt = SceneCameras.Project(c.View, c.Aspect, new V3(m.X, m.Y + Math.Min(top, SceneCameras.LairShown), m.Z));
                    if (qb == null || qt == null) { err += 10; continue; }
                    err += Math.Max(0, -1 + margin - qb.Value.y) + Math.Max(0, qt.Value.y - SceneCameras.BandHiFar + margin);
                }
            }
            return err;
        }
    }

    public static class SceneGen
    {
        // Scenery triangle budgets per quality tier (Low, Medium, High, Ultra), terrain included (CAMPAIGN_SCENES_PLAN.md).
        public static readonly int[] TriBudget = { 16000, 26000, 38000, 44000 };
        public static readonly float[] Density = { 0.4f, 0.7f, 1f, 1f };
        public static readonly int[] TerrainGrid = { 24, 32, 48, 48 };
        static readonly int[] MidCount = { 5, 8, 11, 13 }, SkylineCount = { 4, 6, 8, 9 }, FrameCount = { 2, 3, 4, 4 };

        public static uint Seed(int chapter, int stage)
        {
            unchecked
            {
                uint h = (uint)(101 + chapter * 7) * 2654435761u ^ (uint)(stage + 1) * 40503u;
                h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
                return h;
            }
        }

        // Progress along the chapter's route: 0 at the first stop, 1 at the lair.
        public static float Progress(int stage) => stage < 0 ? 0 : Math.Min(1f, stage / (float)(GameData.StagesPerChapter - 1));

        public static SceneLight LightOf(int chapter, int stage, bool hard, bool titan) => LightOf(chapter, stage, hard, titan, SkyClock.Current);

        // sky: the real sky (Core/SolarClock) or null for the stage's authored time of day. With a sky, the real sun's
        // elevation picks the band (day, late, golden, dusk, night) and the light's direction; the stage's palette,
        // weather and storm dimming stay on top, and a stop that is night by design (story night, Hard) stays night
        // under the real moon. The Titan's caldera keeps its fixed light.
        public static SceneLight LightOf(int chapter, int stage, bool hard, bool titan, SkyState sky)
        {
            var set = titan || stage < 0 ? null : StoryScenes.Of(chapter, stage);
            var authored = titan ? Mood.Day : hard ? Mood.Night : set?.Mood ?? Mood.Day;
            bool real = sky != null && !titan, designNight = authored == Mood.Night;
            var mood = real && !designNight ? sky.Band : authored;
            var (light, dusk, fogK) = StoryScenes.MoodOf(mood);
            var b = Biomes.Of(chapter, titan);
            var L = new SceneLight { Mood = mood, Night = mood == Mood.Night, Dusk = dusk, FogK = fogK, Real = real, DesignNight = designNight };
            // time of day: the sun drops and swings toward the side through the route
            (L.SunPitch, L.SunYaw) = mood switch { Mood.Late => (44f, -38f), Mood.Golden => (32f, -52f), Mood.Dusk => (24f, -62f), Mood.Night => (42f, -20f), _ => (50f, -30f) };
            L.DiscElevation = L.SunPitch; L.DiscYaw = (float)SolarClock.Wrap180(L.SunYaw + 180); L.DiscMoon = L.Night;
            if (real)
            {
                // The light comes from the body the player sees: the sun, or by night the moon (SkyState.NightMoon).
                var body = L.Night ? sky.NightMoon() : sky.Sun;
                L.DiscMoon = L.Night;
                L.DiscElevation = (float)body.Elevation;
                L.DiscYaw = (float)SolarClock.SceneYaw(body.Azimuth, sky.Lat);
                L.SunPitch = (float)Math.Max(SolarClock.MinShadowPitch, body.Elevation);
                L.SunYaw = (float)SolarClock.Wrap180(L.DiscYaw + 180);
            }
            // the world darkens with the mood; the heroes' light keeps a floor so they read on every stage
            L.WorldTint = mood switch { Mood.Late => 0.97f, Mood.Golden => 0.93f, Mood.Dusk => 0.86f, Mood.Night => 0.7f, _ => 1f };
            L.LaneTint = (float)Math.Pow(L.WorldTint, 0.4);      // the ring keeps more of its value (a lit arena)
            L.Sun = (b.Storm && !titan ? 0.97f : 1.15f) * Math.Max(0.88f, light);
            if (real && L.DiscMoon) L.Sun *= 0.9f;   // the real moon: a dimmer, cool light (Arena tints it)
            L.Ambient = 0.55f;
            // a floor for the lit lane: L* 16 (Y about 0.022) under this light
            double lit = LaneAlbedo(b, L) * Light(L);
            if (lit < 0.0225) L.LaneLift = (float)Math.Min(1.8, 0.0225 / Math.Max(1e-4, lit));
            return L;
        }

        public static SceneLayout Generate(int chapter, int stage, bool hard = false, int tier = 2, bool titan = false)
        {
            tier = Math.Max(0, Math.Min(3, tier));
            var b = Biomes.Of(chapter, titan);
            var L = new SceneLayout { Chapter = chapter, Stage = stage, Hard = hard, Titan = titan, Tier = tier, Biome = b, Seed = titan ? 1337u : Seed(chapter, stage) };
            L.Light = LightOf(chapter, stage, hard, titan);
            L.WaterZ = b.HasWater || titan ? 10f : -1f;
            L.Relief = titan ? 0 : b.Relief;
            double fogEnd = b.FogFar * L.Light.FogK;
            bool boss = !titan && stage == GameData.StagesPerChapter - 1;
            L.Cams = SceneCameras.Battle(fogEnd, boss);   // boss stages: the tilted views (BattleLayout.BossView)
            var rng = new Rng(L.Seed);
            var set = titan || stage < 0 ? null : StoryScenes.Of(chapter, stage);
            bool lair = set != null && stage == GameData.StagesPerChapter - 1;
            float prog = Progress(stage);

            // ---- the landmark: the lair's main piece, or its copy ahead on the road, framed for the default view ----
            var pieces = new List<Piece>(set?.Pieces ?? new Piece[0]);
            var rv = titan || stage < 0 ? null : StoryScenes.RevealOf(chapter);
            Piece main = null; float mainTop = 0;
            if (rv != null)
            {
                var (rp, top) = rv.Value;
                foreach (var pc in pieces) if (pc.Mesh == rp.Mesh && (main == null || pc.Scale > main.Scale)) main = pc;
                if (main == null && set != null)
                {
                    // no copy on this stop: one stands far ahead, nearer each stop (STORY_SCENES.md section 1)
                    main = new Piece { Mesh = rp.Mesh, X = rp.X, Z = 24.5f - 5f * prog, Scale = rp.Scale * 0.95f, Yaw = rp.Yaw, R = rp.R, Far = true };
                    pieces.Add(main);
                }
                if (main != null) mainTop = top * main.Scale / Math.Max(0.01f, rp.Scale);
            }
            float dx = 0, dz = 0;
            if (main != null)
            {
                var m = FromPiece(main, Layer.Landmark);
                (dx, dz) = FrameLandmark(L, m, mainTop, rng);
                m.X += dx; m.Z += dz; m.Moved = dx != 0 || dz != 0;   // the ground is levelled under it (TerrainHeight)
                L.Landmark = m; L.LandmarkTop = mainTop;
                Add(L, m);
                if (lair) L.Cams.AddRange(SceneCameras.Reveal(m.X, m.Z, mainTop, fogEnd));
            }
            BuildPath(L, rng);

            // ---- the stop's other story pieces: the landmark's parts and copies move with it; the rest keep their spot
            // if it is legal, else the nearest legal spot ----
            foreach (var pc in pieces)
            {
                if (pc == main) continue;
                bool part = main != null && (pc.Mesh == main.Mesh || pc.Mesh.StartsWith(main.Mesh + "_"));
                var p = FromPiece(pc, part ? Layer.LandmarkPart : Layer.SetPiece);
                if (part)
                {
                    p.X += dx; p.Z += dz;
                    // a generated landmark carries its part at its own socket
                    if (SceneMeshes.Generated(main.Mesh) && SceneMeshes.Sockets.TryGetValue(main.Mesh, out var so) && so.part == pc.Mesh)
                    {
                        var host = pieces.Find(q => q.Mesh == main.Mesh && Math.Abs(q.Scale - pc.Scale) < 0.02f) ?? main;
                        double a = host.Yaw * Math.PI / 180, k = host.Scale;
                        p.X = (float)(host.X + dx + (so.x * Math.Cos(a) + so.z * Math.Sin(a)) * k);
                        p.Z = (float)(host.Z + dz + (-so.x * Math.Sin(a) + so.z * Math.Cos(a)) * k);
                        p.Y = host.Y + so.y * (float)k; p.Yaw = host.Yaw + so.yaw; p.Scale = (float)k;
                    }
                    Add(L, p); continue;
                }
                if (!Fit(L, p, rng)) L.Dropped.Add($"{chapter + 1}-{stage + 1} {pc.Mesh}");
            }

            // ---- boss stage dressing: braziers flanking the foes, war banners behind them ----
            if (boss || titan)
            {
                float back = titan ? 9f : 7.8f, bx = titan ? 4.8f : 4.0f;
                foreach (float sx in new[] { -1f, 1f })
                {
                    Fit(L, new Placement { Mesh = "brazier", X = sx * 6.7f, Z = 4.4f, Scale = 1.1f, Yaw = (float)rng.Range(0, 360), Layer = Layer.Boss, Glow = "fire" }, rng, 3f);
                    Fit(L, new Placement { Mesh = "banner_pole", X = sx * bx, Z = back, Scale = 1.15f, Layer = Layer.Boss }, rng, 3f);
                }
            }
            // ---- Hard mode by night: lanterns lit along the flanks ----
            if (hard && !titan)
                foreach (var (lx, lz) in new[] { (-7.6f, 3f), (7.6f, 3f), (-8.4f, 9.5f), (8.4f, 9.5f) })
                    Fit(L, new Placement { Mesh = "lantern", X = lx, Z = lz, Scale = 1.05f, Layer = Layer.Lantern, Glow = "fire" }, rng, 2f);

            if (titan) { Finish(L); return L; }   // the Titan's caldera adds its own ridge (Client/Scenery.Caldera)

            int budget = TriBudget[tier] - TerrainTris(tier);
            // ---- skyline: big silhouettes just before the near ridge, painted with the far ramp ----
            Sprinkle(L, rng, Biomes.Skyline(b), SkylineCount[tier], Layer.Skyline, budget, (r) => (r.Range(-26, 26), r.Range(21.5, 26.2)), far: true);
            // ---- mid-ground: the biome's big shapes 10-17 m out ----
            Sprinkle(L, rng, Biomes.Mid(b), MidCount[tier], Layer.Mid, budget, (r) => Polar(r, 10, 17));
            // ---- framing: tall shapes on the near flanks, only ever at the screen's edges ----
            Sprinkle(L, rng, Biomes.Frame(b), FrameCount[tier], Layer.Frame, budget, (r) => (r.Pick(-1, 1) * r.Range(7.5, 13), r.Range(-7, 2.5)));
            // ---- toward the lair: fallen stone and dead wood thicken along the road ----
            int ap = (int)Math.Round((2 + 8 * prog) * Density[tier]);
            Sprinkle(L, rng, Biomes.Approach(b), ap, Layer.Approach, budget, (r) => NearPath(L, r));
            // ---- scatter: thinning out toward the lane ----
            int n = (int)Math.Round(70 * Density[tier]);
            var table = Biomes.Scatter(b);
            for (int i = 0; i < n; i++)
            {
                var (x, z) = Polar(rng, 6.5, 21);
                double gap = LaneGap(x, z);
                if (rng.Next() > Smooth(0.3, 4.5, gap)) continue;   // density falloff near the lane
                var e = Choose(rng, table);
                var p = Kit(L, rng, e, x, z, Layer.Scatter);
                if (p != null && L.Tris + Tris(p) <= budget) TryAdd(L, p); else if (p != null) Count(L, Reject.Budget);
            }
            // ---- grass tufts just outside the lane (they read as ground texture) ----
            for (int i = 0; i < (int)Math.Round(40 * Density[tier]); i++)
            {
                double a = rng.Range(0, Math.PI * 2), k = rng.Range(1.05, 1.5);
                double x = Math.Sin(a) * 6.2 * k, z = 0.6 + Math.Cos(a) * 5.6 * k;
                var p = new Placement { Mesh = "tuft" + (int)rng.Range(0, 2.99), X = (float)x, Z = (float)z, Scale = (float)rng.Range(0.5, 0.8), Yaw = (float)rng.Range(0, 360), Layer = Layer.Tuft };
                p.Y = L.Height(p.X, p.Z);
                if (L.Tris + Tris(p) <= budget) TryAdd(L, p);
            }
            Finish(L);
            return L;
        }

        static void Finish(SceneLayout L)
        {
            L.Tris = TerrainTris(L.Tier);
            foreach (var p in L.Items) L.Tris += Tris(p);
        }
        public static int TerrainTris(int tier) => TerrainGrid[Math.Max(0, Math.Min(3, tier))] * TerrainGrid[Math.Max(0, Math.Min(3, tier))] * 2;
        static int Tris(Placement p) => SceneMeshes.Box(p.Mesh).Tris;

        static Placement FromPiece(Piece pc, Layer layer) => new Placement
        {
            Mesh = pc.Mesh, X = pc.X, Y = pc.Y, Z = pc.Z, Scale = pc.Scale, Yaw = pc.Yaw, Layer = layer,
            Glow = pc.Glow, Far = pc.Far, Fx = pc.Fx, FxY = pc.FxY, Sway = pc.Fx == LairFx.Sway,
        };

        // Moves the landmark (and its parts) to the legal spot nearest its authored one whose framing in the default view
        // is best: x searched across the background band, z a little nearer or farther.
        static (float dx, float dz) FrameLandmark(SceneLayout L, Placement m, float top, Rng rng)
        {
            double bestScore = double.MaxValue; float bdx = 0, bdz = 0;
            foreach (double ddz in new[] { 0.0, -1.0, 1.0, -2.0, 2.0, 3.5, -3.0, -4.5, -6.0 })
                for (double x = -24; x <= 14; x += 0.5)
                {
                    var c = new Placement { Mesh = m.Mesh, X = (float)x, Z = (float)(m.Z + ddz), Y = m.Y, Scale = m.Scale, Yaw = m.Yaw, Layer = Layer.Landmark, Far = m.Far };
                    double fe = SceneRules.FrameError(L, c, top, 0.04);
                    double move = Math.Abs(x - m.X) * 0.02 + Math.Abs(ddz) * 0.05;
                    double score = fe * 100 + move;
                    if (score >= bestScore) continue;
                    if (SceneRules.Check(L, c) != Reject.None) continue;
                    bestScore = score; bdx = (float)(x - m.X); bdz = (float)ddz;
                }
            return (bdx, bdz);
        }

        // Places p at its spot if legal, else at the nearest legal spot within `reach` metres (a spiral search).
        static bool Fit(SceneLayout L, Placement p, Rng rng, float reach = 9f)
        {
            float x0 = p.X, z0 = p.Z;
            for (float r = 0; r <= reach; r += 0.75f)
            {
                int steps = r == 0 ? 1 : Math.Max(8, (int)(r * 4));
                double a0 = Math.Atan2(z0 - BattleLayout.RimCz, x0) ; // start pointing away from the ring
                for (int i = 0; i < steps; i++)
                {
                    double a = a0 + (i % 2 == 0 ? 1 : -1) * ((i + 1) / 2) * (Math.PI * 2 / steps);
                    p.X = (float)(x0 + Math.Cos(a) * r); p.Z = (float)(z0 + Math.Sin(a) * r);
                    float y0 = p.Y; p.Y = y0 + L.Height(p.X, p.Z) * (p.Far ? 1 : 0.6f);
                    var why = SceneRules.Check(L, p);
                    if (why == Reject.None) { p.Moved = r > 0; Add(L, p); return true; }
                    p.Y = y0;
                    if (r == 0) Count(L, why);
                }
            }
            p.X = x0; p.Z = z0;
            return false;
        }

        static void Add(SceneLayout L, Placement p) { L.Items.Add(p); L.Tris += Tris(p); }
        static bool TryAdd(SceneLayout L, Placement p)
        {
            var why = SceneRules.Check(L, p);
            if (why != Reject.None) { Count(L, why); return false; }
            Add(L, p); return true;
        }
        static void Count(SceneLayout L, Reject r) { L.Rejects.TryGetValue(r, out int n); L.Rejects[r] = n + 1; }

        static void Sprinkle(SceneLayout L, Rng rng, List<Pick> table, int count, Layer layer, int budget, Func<Rng, (double x, double z)> where, bool far = false)
        {
            if (table.Count == 0) return;
            int placed = 0;
            for (int tries = 0; tries < count * 6 && placed < count; tries++)
            {
                var (x, z) = where(rng);
                var p = Kit(L, rng, Choose(rng, table), x, z, layer);
                if (p == null) continue;
                p.Far = far;
                if (L.Tris + Tris(p) > budget) { Count(L, Reject.Budget); break; }
                if (TryAdd(L, p)) placed++;
            }
        }

        static Placement Kit(SceneLayout L, Rng rng, Pick e, double x, double z, Layer layer)
        {
            var names = SceneMeshes.Variants(e.Prefix);
            if (names.Count == 0) return null;
            var p = new Placement
            {
                Mesh = names[(int)rng.Range(0, names.Count - 0.001)], X = (float)x, Z = (float)z, Scale = (float)rng.Range(e.Lo, e.Hi),
                Yaw = (float)rng.Range(0, 360), Layer = layer, Sway = e.Sway,
            };
            p.Y = L.Height(p.X, p.Z);
            return p;
        }

        static Pick Choose(Rng rng, List<Pick> table)
        {
            double total = 0; foreach (var p in table) total += p.W;
            double k = rng.Range(0, total);
            foreach (var p in table) { if (k < p.W) return p; k -= p.W; }
            return table[table.Count - 1];
        }

        static (double, double) Polar(Rng r, double r0, double r1)
        {
            double a = r.Range(0, Math.PI * 2), d = r.Range(r0, r1);
            return (Math.Cos(a) * d, Math.Sin(a) * d * 0.9 + 4);
        }

        // Metres outside the lane ellipse (negative inside).
        public static double LaneGap(double x, double z)
        {
            double ex = x / SceneCameras.LaneRx, ez = (z - BattleLayout.RimCz) / SceneCameras.LaneRz;
            return (Math.Sqrt(ex * ex + ez * ez) - 1) * (SceneCameras.LaneRx + SceneCameras.LaneRz) * 0.5;
        }

        static double Smooth(double a, double b, double x) { double t = Math.Max(0, Math.Min(1, (x - a) / (b - a))); return t * t * (3 - 2 * t); }

        // ---- the road: from the camera side through the ring toward the landmark ----
        static void BuildPath(SceneLayout L, Rng rng)
        {
            L.Path.Clear();
            L.Path.Add(((float)rng.Range(-0.6, 0.6), -16f)); L.Path.Add(((float)rng.Range(-0.4, 0.4), -6f)); L.Path.Add((0f, 0.6f));
            float tx = L.Landmark?.X ?? (float)rng.Range(-6, 6), tz = L.Landmark != null ? L.Landmark.Z - L.Landmark.R * 0.6f : 26f;
            if (L.WaterZ > 0) tz = Math.Min(tz, L.WaterZ - 0.5f);
            float wig = (float)rng.Range(-2.2, 2.2);
            for (int i = 1; i <= 3; i++)
            {
                float t = i / 3f;
                float x = tx * t + wig * (float)Math.Sin(t * Math.PI), z = 0.6f + (tz - 0.6f) * t;
                L.Path.Add((x, z));
            }
        }
        public static double PathDistance(SceneLayout L, double x, double z)
        {
            double best = 1e9;
            for (int i = 0; i + 1 < L.Path.Count; i++)
            {
                var (ax, az) = L.Path[i]; var (bx, bz) = L.Path[i + 1];
                double dx = bx - ax, dz = bz - az, u = Math.Max(0, Math.Min(1, ((x - ax) * dx + (z - az) * dz) / Math.Max(1e-6, dx * dx + dz * dz)));
                double ex = x - ax - dx * u, ez = z - az - dz * u;
                best = Math.Min(best, Math.Sqrt(ex * ex + ez * ez));
            }
            return best;
        }
        static (double, double) NearPath(SceneLayout L, Rng r)
        {
            int seg = 2 + (int)r.Range(0, Math.Max(1, L.Path.Count - 3) - 0.001);
            var (ax, az) = L.Path[Math.Min(seg, L.Path.Count - 2)]; var (bx, bz) = L.Path[Math.Min(seg + 1, L.Path.Count - 1)];
            double u = r.Range(0, 1), side = r.Pick(-1, 1) * r.Range(2.2, 5.5);
            double dx = bx - ax, dz = bz - az, len = Math.Max(1e-3, Math.Sqrt(dx * dx + dz * dz));
            return (ax + dx * u - dz / len * side, az + dz * u + dx / len * side);
        }

        // ---- terrain: flat in and around the lane, on the road and at the shore; rolling outside, rising toward the ridges ----
        public static float TerrainHeight(SceneLayout L, float x, float z)
        {
            if (L.Relief <= 0) return 0;
            double e = Math.Sqrt(Math.Pow(x / 6.2, 2) + Math.Pow((z - 0.6) / 5.6, 2));
            double mask = Smooth(1.15, 2.3, e);
            if (z < -4) mask *= 0.35;                                       // the camera side stays low
            if (L.WaterZ > 0) mask *= Smooth(L.WaterZ - 0.5, L.WaterZ - 4.5, z);
            if (L.Path.Count > 1) mask *= Smooth(0.9, 3.2, PathDistance(L, x, z));
            if (L.Landmark != null)                                           // levelled under the landmark and its parts
            {
                double r = L.Landmark.R * 0.7, d = Math.Sqrt((x - L.Landmark.X) * (x - L.Landmark.X) + (z - L.Landmark.Z) * (z - L.Landmark.Z));
                mask *= Smooth(r, r + 4, d);
            }
            if (mask <= 0) return 0;
            double n = Noise(x * 0.075, z * 0.075, L.Seed) * 0.7 + Noise(x * 0.19, z * 0.19, L.Seed + 7) * 0.3;
            double rise = Smooth(13, 25, Math.Max(Math.Abs(x), z));
            return (float)(L.Relief * mask * (0.3 + 0.7 * n + 0.45 * rise));
        }

        // Value noise (smoothstep), deterministic across platforms.
        public static double Noise(double x, double y, uint seed)
        {
            double fx0 = Math.Floor(x), fy0 = Math.Floor(y), fx = x - fx0, fy = y - fy0;
            int ix = (int)fx0, iy = (int)fy0;
            double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            double a = H(ix, iy, seed), b = H(ix + 1, iy, seed), c = H(ix, iy + 1, seed), d = H(ix + 1, iy + 1, seed);
            double ab = a + (b - a) * sx, cd = c + (d - c) * sx;
            return ab + (cd - ab) * sy;
        }
        static double H(int ix, int iy, uint seed)
        {
            unchecked
            {
                uint h = (uint)ix * 374761393u + (uint)iy * 668265263u + seed * 1442695041u;
                h = (h ^ (h >> 13)) * 1274126177u; h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216.0;
            }
        }

        // ---- readability (art review M2): the lane's ground against a hero's mid-tone, under the stage's light ----
        // CIE L* of the lit lane ground and of a reference hero mid-tone (painted albedo 0.3, no world tint).
        public static (double ground, double hero) LaneValues(int chapter, int stage, bool hard)
        {
            var b = Biomes.Of(chapter); var li = LightOf(chapter, stage, hard, false);
            double ground = LaneAlbedo(b, li) * li.LaneLift * Light(li), hero = 0.3 * (li.Sun * 0.62 + li.Ambient * 0.55);
            return (LStar(ground), LStar(hero));
        }
        static double LaneAlbedo(Biome b, SceneLight li) => Lum(b.Worn) * 0.46 * 0.9 * li.LaneTint;   // Scenery AG x the worn patch's 0.9
        static double Light(SceneLight li) => li.Sun * Math.Sin(li.SunPitch * Math.PI / 180) + li.Ambient * 0.55;
        static double Lin(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
        static double Lum(string hex)
        {
            int v = Convert.ToInt32(hex.TrimStart('#'), 16);
            return 0.2126 * Lin(((v >> 16) & 255) / 255.0) + 0.7152 * Lin(((v >> 8) & 255) / 255.0) + 0.0722 * Lin((v & 255) / 255.0);
        }
        static double LStar(double y) => y > 216.0 / 24389 ? 116 * Math.Pow(Math.Min(1, y), 1.0 / 3) - 16 : y * 24389.0 / 27;
    }

    // A small deterministic generator (xorshift), the same on every platform.
    public sealed class Rng
    {
        uint _s;
        public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
        public double Next() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return (_s & 0xFFFFFF) / 16777216.0; }
        public double Range(double a, double b) => a + Next() * (b - a);
        public double Pick(double a, double b) => Next() < 0.5 ? a : b;
    }
}
