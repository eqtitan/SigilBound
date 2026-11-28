// Campaign world map (docs/sigilbound/PLAN.md steps 8 and 38), shown in landscape, with an Act I / Act II switch. Sigilbound's own land:
// a parchment map generated in code (noise coastlines raised along the road, hills and a
// vignette), a dashed road through ten chapter medallions (element icon, number, six pips for the
// stages cleared, lock when locked), the chapter's stage list (PrepScreens.cs DrawStageList), and a track
// of the real boss first-clear rewards (a Mystic Shard per chapter boss, plus a Radiant Shard
// every third chapter: Game.StageRewards). Normal/Hard switch top-left. Portrait keeps the list.
using Sigilbound.Core;
using UnityEngine;
using static Sigilbound.Core.GameData;

namespace Sigilbound.Client
{
    public sealed partial class App
    {
        // Chapter positions on the map, as fractions of its width and height (a winding road west to east).
        static readonly Vector2[] MapPoints =
        {
            new Vector2(0.07f, 0.68f), new Vector2(0.17f, 0.40f), new Vector2(0.27f, 0.62f), new Vector2(0.37f, 0.33f), new Vector2(0.47f, 0.58f),
            new Vector2(0.56f, 0.30f), new Vector2(0.65f, 0.60f), new Vector2(0.75f, 0.36f), new Vector2(0.85f, 0.64f), new Vector2(0.93f, 0.34f),
        };
        // Act 2 (chapters 11-15): along the storm coast from the harbour.
        static readonly Vector2[] MapPoints2 =
        {
            new Vector2(0.12f, 0.62f), new Vector2(0.31f, 0.36f), new Vector2(0.5f, 0.64f), new Vector2(0.69f, 0.36f), new Vector2(0.88f, 0.6f),
        };
        // Act I on the owner's painted world map (skin "bg_campaign"): each chapter on painted land, from the
        // western forests over the northern passes and the heartland to the corrupted east, the Last Pyre
        // deep in it. Fractions of the painting (its ornate border takes ~8% at the sides, ~7% top and bottom).
        static readonly Vector2[] PaintedPoints =
        {
            new Vector2(0.245f, 0.585f), new Vector2(0.36f, 0.47f), new Vector2(0.32f, 0.27f), new Vector2(0.44f, 0.32f), new Vector2(0.56f, 0.27f),
            new Vector2(0.53f, 0.60f), new Vector2(0.62f, 0.78f), new Vector2(0.68f, 0.45f), new Vector2(0.71f, 0.64f), new Vector2(0.80f, 0.48f),
        };
        static bool PaintedMap => Skin.Tex("bg_campaign") != null;
        static Vector2[] Act1Points => PaintedMap ? PaintedPoints : MapPoints;
        // Act II on the owner's painted storm-coast map (skin "bg_campaign2"), each chapter on its landmark:
        // the Saltreach harbour below the lighthouse, the drowned shrine in the fens, the barrow stones,
        // the burning great tree, and the Hollow King's spire on the far cliff.
        static readonly Vector2[] PaintedPoints2 =
        {
            new Vector2(0.11f, 0.47f), new Vector2(0.37f, 0.50f), new Vector2(0.53f, 0.52f), new Vector2(0.67f, 0.36f), new Vector2(0.87f, 0.26f),
        };
        static bool PaintedMap2 => Skin.Tex("bg_campaign2") != null;
        static Vector2[] Act2Points => PaintedMap2 ? PaintedPoints2 : MapPoints2;
        static readonly Texture2D[] _mapTex = new Texture2D[2];
        int _act; // 0 = follow progress, else 1 or 2 (the Act I / Act II switch)


        void DrawWorldMap(Rect area)
        {
            bool hard = _hardTab && G.S.cleared > 0;
            int progressAll = hard ? G.S.hardCleared : G.S.cleared;
            bool act2Open = G.S.cleared >= ClassicStages;
            if (act2Open && !ModalOpen && PlayerPrefs.GetInt("sb.act2Intro", 0) == 0)
            {
                PlayerPrefs.SetInt("sb.act2Intro", 1); PlayerPrefs.Save();
                _act = 2;
                Modal(Act2Title, 300, b =>
                {
                    Ui.Text(new Rect(b.x, b.y, b.width, b.height - 56), Act2Intro, 14, Palette.Text, FontStyle.Normal, TextAnchor.UpperLeft, true);
                    if (Ui.Button(new Rect(b.x, b.yMax - 46, b.width, 44), "To Saltreach")) CloseModal();
                });
            }
            int act = MapAct(hard);
            var pts = act == 1 ? Act1Points : Act2Points;
            int c0 = act == 1 ? 0 : ClassicChapters, cn = act == 1 ? ClassicChapters : Chapters.Length - ClassicChapters;
            float trackH = hard ? 0 : 58;
            bool listOpen = _chapterOpen >= 0 && _chapterOpen < Chapters.Length;
            var map = new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16 - trackH);
            // An open stage list also covers the reward track (58 more units for its rows: UX review #1).
            var listArea = new Rect(map.x, map.y, map.width, area.yMax - 8 - map.y);
            if (act == 1 && PaintedMap) GUI.DrawTexture(map, Skin.Tex("bg_campaign"), ScaleMode.StretchToFill); // the painting has its own ornate border
            else if (act == 2 && PaintedMap2) GUI.DrawTexture(map, Skin.Tex("bg_campaign2"), ScaleMode.StretchToFill);
            else { GUI.DrawTexture(map, MapTexture(act), ScaleMode.StretchToFill); Ui.Round(map, Color.clear, Palette.Trim, 12); } // gilded frame

            // Road: dashes between consecutive medallions.
            for (int k = 0; k + 1 < cn; k++)
            {
                int c = c0 + k;
                Vector2 a = MapPos(map, pts[k]), b = MapPos(map, pts[k + 1]);
                bool walked = (c + 1) * StagesPerChapter <= (hard ? G.S.hardCleared : G.S.cleared);
                int dashes = Mathf.Max(3, Mathf.RoundToInt(Vector2.Distance(a, b) / 14));
                for (int d = 0; d < dashes; d += 2)
                {
                    var p = Vector2.Lerp(a, b, (d + 0.5f) / dashes);
                    Ui.Round(new Rect(p.x - 2.5f, p.y - 2.5f, 5, 5), walked ? Palette.Gold : new Color(0.25f, 0.18f, 0.1f, 0.8f), null, 2);
                }
            }

            // An open stage list covers part of the map: the medallions and switches under it must not take its taps.
            Ui.Cover = listOpen ? StageListRect(listArea, _chapterOpen) : (Rect?)null;
            // Medallions.
            int progress = progressAll;
            int current = Mathf.Min(Chapters.Length - 1, progress / StagesPerChapter);
            for (int k = 0; k < cn; k++)
            {
                int c = c0 + k;
                int first = c * StagesPerChapter;
                bool locked = hard ? first > G.S.hardCleared || first >= G.S.cleared : first > G.S.cleared;
                int done = Mathf.Clamp(progress - first, 0, StagesPerChapter);
                var p = MapPos(map, pts[k]); float s = 50;
                var m = new Rect(p.x - s / 2, p.y - s / 2, s, s);
                if (c == current && !locked) Ui.Round(new Rect(m.x - 10, m.y - 10, s + 20, s + 20), new Color(1f, 0.85f, 0.4f, 0.28f), null, 35);
                var el = Game.ChapterElement(c);
                Ui.Round(m, locked ? Palette.Hex("#2a2430") : Palette.Hex("#1d1726"), _chapterOpen == c ? Palette.TrimHi : locked ? Palette.Bronze : Palette.Trim, 25);
                Icons.Draw(new Rect(m.x + 9, m.y + 7, s - 18, s - 18), locked ? Icon.Lock : Icons.ForElement(el), locked ? Palette.Dim : Palette.Element(el));
                Ui.Text(new Rect(m.x, m.yMax - 18, s, 14), (c + 1).ToString(), 11, locked ? Palette.Dim : Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
                // Six pips: stages cleared in this chapter.
                for (int q = 0; q < StagesPerChapter; q++)
                    Ui.Round(new Rect(p.x - 21 + q * 7.5f, m.yMax + 3, 6, 6), q < done ? Palette.Gold : new Color(0, 0, 0, 0.55f), Palette.TrimLo, 3);
                // The chapter name on a dark pill, readable when locked too (the lock icon tells the state: art review H5).
                var label = new Rect(p.x - 75, m.yMax + 10, 150, 17);
                Ui.TextOnArt(label, Chapters[c], 11, locked ? Palette.Hex("#cfc5b2") : Palette.Text, FontStyle.Bold);
                var hitM = new Rect(m.x - 6, m.y - 6, s + 12, s + 34);
                if (Ui.Button(hitM, "", Ui.Kind.Ghost))
                {
                    if (locked) Toast(hard && first < G.S.cleared ? $"Clear Hard {Game.StageLabel(first - 1)} to open {Chapters[c]}" : $"Clear stage {Game.StageLabel(first - 1)} to open {Chapters[c]}");
                    else _chapterOpen = _chapterOpen == c ? -1 : c;
                }
            }

            // Title; the Normal/Hard and Act switches are in the top bar (DrawCampaignSwitches), outside the painting.
            Ui.Banner(new Rect(map.center.x - 170, map.y + 8, 340, 30), (hard ? "Hard · " : "") + (act == 1 ? "Act I" : "Act II · The Sigil Bearers"), 18);
            Ui.TextOnArt(new Rect(map.xMax - 190, map.y + 12, 176, 26), hard ? $"Hard {G.S.hardCleared}/{G.S.cleared}" : $"{G.S.cleared}/{TotalStages} stages", 13, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);

            if (listOpen)
            {
                Ui.Cover = null; Ui.OnTop = true;
                try { DrawStageList(listArea, _chapterOpen, hard); } finally { Ui.OnTop = false; }
            }
            Ui.Cover = null;
            if (!hard && !listOpen) DrawRewardTrack(new Rect(area.x + 10, map.yMax + 6, area.width - 20, trackH - 8), c0, cn);
        }

        // Which act the map shows: the player's pick, else where their progress is.
        int MapAct(bool hard)
        {
            bool act2Open = G.S.cleared >= ClassicStages;
            int act = _act != 0 ? _act : (act2Open && (hard ? G.S.hardCleared : G.S.cleared) >= ClassicStages ? 2 : 1);
            return act == 2 && !act2Open ? 1 : act;
        }

        // The campaign's two switches as segmented controls in the top bar (UX review #7: the chips on the
        // painting were violet on violet and 28 dp tall). Locked options keep a readable label and a lock, and
        // say why when tapped.
        void DrawCampaignSwitches(Rect r)
        {
            bool hard = _hardTab && G.S.cleared > 0, act2Open = G.S.cleared >= ClassicStages;
            int act = MapAct(hard);
            float h = Mathf.Min(r.height, Mathf.Max(44, Ui.Tap)), segW = Mathf.Clamp(r.width / 4.4f, 62, 84), gap = 12;
            float total = segW * 4 + gap + 12, x = r.x + Mathf.Max(0, (r.width - total) / 2);
            var mode = new Rect(x, r.center.y - h / 2, segW * 2 + 6, h);
            var acts = new Rect(mode.xMax + gap, mode.y, segW * 2 + 6, h);
            int m = Ui.Segmented(mode, new[] { "Normal", "Hard" }, hard ? 1 : 0, new[] { true, G.S.cleared > 0 }, 14);
            if (m == 0 && hard) { _hardTab = false; _chapterOpen = -1; }
            else if (m == 1 && G.S.cleared == 0) Toast("Clear stage 1-1 to unlock Hard mode");
            else if (m == 1 && !hard) { _hardTab = true; _chapterOpen = -1; }
            int a = Ui.Segmented(acts, new[] { "Act I", "Act II" }, act - 1, new[] { true, act2Open }, 14);
            if (a == 1 && !act2Open) Toast($"Clear stage {Game.StageLabel(ClassicStages - 1)} to open Act II");
            else if (a >= 0 && a + 1 != act) { _act = a + 1; _chapterOpen = -1; }
        }

        static Vector2 MapPos(Rect map, Vector2 p) => new Vector2(map.x + p.x * map.width, map.y + p.y * map.height);

        // Boss first-clear rewards per chapter (Game.StageRewards): ticked once claimed.
        void DrawRewardTrack(Rect r, int c0, int cn)
        {
            Ui.Round(r, new Color(0.07f, 0.05f, 0.1f, 0.85f), Palette.Bronze, 10);
            float step = r.width / cn;
            for (int k = 0; k < cn; k++)
            {
                int c = c0 + k;
                int boss = c * StagesPerChapter + StagesPerChapter - 1;
                var rw = Game.StageRewards(boss, true);
                bool got = boss < G.S.cleared;
                float cx = r.x + step * (k + 0.5f);
                if (k > 0) GUI.DrawTexture(new Rect(cx - step + 14, r.center.y - 1, step - 28, 2), Ui.Solid(got ? Palette.Gold : Palette.Bronze));
                var slot = new Rect(cx - 14, r.center.y - 14, 28, 28);
                Ui.Round(slot, got ? Palette.Hex("#3a2c14") : Palette.Hex("#1d1726"), got ? Palette.Gold : Palette.Bronze, 14);
                Icons.Draw(new Rect(slot.x + 6, slot.y + 6, 16, 16), Icon.Shard, rw.Radiant > 0 ? Palette.Hex("#ffb02e") : Palette.Blue);
                if (rw.Radiant > 0) Ui.Text(new Rect(slot.xMax - 4, slot.y - 6, 20, 14), "+R", 9, Palette.Hex("#ffb02e"), FontStyle.Bold);
                if (got) Icons.Draw(new Rect(slot.xMax - 10, slot.yMax - 10, 12, 12), Icon.Check, Palette.Good);
            }
        }

        // Parchment map of Sigilbound, generated once: value-noise land raised along the road,
        // sea, coast lines, hill shading and a darkened border.
        // act 1: the heartland; act 2: a darker, stormier coast (more sea, its own noise).
        static Texture2D MapTexture(int act)
        {
            if (_mapTex[act - 1] != null) return _mapTex[act - 1];
            var pts = act == 1 ? MapPoints : MapPoints2;
            float seaBias = act == 1 ? 0f : 0.06f, noiseShift = act == 1 ? 0f : 37.5f;
            const int W = 512, H = 256;
            var px = new Color[W * H];
            Color sea = Palette.Hex(act == 1 ? "#2f5a5e" : "#24414e"), seaDeep = Palette.Hex(act == 1 ? "#1e3d44" : "#132a36"), land = Palette.Hex(act == 1 ? "#c9b083" : "#a99c7c"), landHi = Palette.Hex(act == 1 ? "#ddc79a" : "#c4b894"), hill = Palette.Hex(act == 1 ? "#9c8458" : "#6f6a5a"), coast = Palette.Hex("#5a4426");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float n = Fbm(u * 6f + noiseShift, v * 3f);
                    // Raise land near the road so every chapter sits on solid ground.
                    float road = 1f;
                    for (int c = 0; c + 1 < pts.Length; c++) road = Mathf.Min(road, SegDist(new Vector2(u, v), pts[c], pts[c + 1]));
                    float h = n * 0.75f + Mathf.Clamp01(1 - road / 0.16f) * 0.45f - 0.18f - seaBias;
                    Color col;
                    if (h < 0.28f) col = Color.Lerp(seaDeep, sea, Mathf.Clamp01(h / 0.28f));
                    else if (h < 0.31f) col = coast;
                    else col = Color.Lerp(land, Color.Lerp(landHi, hill, Mathf.Clamp01((h - 0.62f) * 3f)), Mathf.Clamp01((h - 0.31f) * 1.6f));
                    // Paper grain and a darkened border.
                    col *= 0.94f + 0.06f * Hash(x, y);
                    float edge = Mathf.Min(Mathf.Min(u, 1 - u) * 2f, Mathf.Min(v, 1 - v) * 4f);
                    col = Color.Lerp(col * 0.55f, col, Mathf.Clamp01(edge * 3f));
                    col.a = 1;
                    px[y * W + x] = col;
                }
            // Texture rows go bottom-up; our v was top-down, so flip.
            var flipped = new Color[W * H];
            for (int y = 0; y < H; y++) System.Array.Copy(px, y * W, flipped, (H - 1 - y) * W, W);
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "world_map_act" + act };
            tex.SetPixels(flipped); tex.Apply();
            return _mapTex[act - 1] = tex;
        }

        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            var ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }
        static float Hash(int x, int y) { uint h = (uint)(x * 374761393 + y * 668265263); h = (h ^ (h >> 13)) * 1274126177; return (h & 0xffff) / 65535f; }
        static float VNoise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y); float xf = x - xi, yf = y - yi;
            float sx = xf * xf * (3 - 2 * xf), sy = yf * yf * (3 - 2 * yf);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, sx), Mathf.Lerp(c, d, sx), sy);
        }
        static float Fbm(float x, float y)
        {
            float s = 0, amp = 0.5f, f = 1;
            for (int i = 0; i < 4; i++) { s += VNoise(x * f + 17.3f * i, y * f) * amp; f *= 2; amp *= 0.5f; }
            return s / 0.9375f;
        }
    }
}
