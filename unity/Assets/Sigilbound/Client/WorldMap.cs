// Campaign world map (docs/sigilbound/PLAN.md step 8), shown in landscape. Sigilbound's own land:
// a parchment map generated in code (noise coastlines raised along the road, hills and a
// vignette), a dashed road through ten chapter medallions (element icon, number, six pips for the
// stages cleared, lock when locked), a side panel with the chosen chapter's stages, and a track
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
        static Texture2D _mapTex;


        void DrawWorldMap(Rect area)
        {
            bool hard = _hardTab && G.S.cleared > 0;
            float trackH = hard ? 0 : 58;
            var map = new Rect(area.x + 10, area.y + 8, area.width - 20, area.height - 16 - trackH);
            GUI.DrawTexture(map, MapTexture(), ScaleMode.StretchToFill);
            GUI.DrawTexture(map, Ui.RoundTex(Color.clear, Palette.Trim, 12)); // gilded frame

            // Road: dashes between consecutive medallions.
            for (int c = 0; c + 1 < Chapters.Length; c++)
            {
                Vector2 a = MapPos(map, c), b = MapPos(map, c + 1);
                bool walked = (c + 1) * StagesPerChapter <= (hard ? G.S.hardCleared : G.S.cleared);
                int dashes = Mathf.Max(3, Mathf.RoundToInt(Vector2.Distance(a, b) / 14));
                for (int d = 0; d < dashes; d += 2)
                {
                    var p = Vector2.Lerp(a, b, (d + 0.5f) / dashes);
                    GUI.DrawTexture(new Rect(p.x - 2.5f, p.y - 2.5f, 5, 5), Ui.RoundTex(walked ? Palette.Gold : new Color(0.25f, 0.18f, 0.1f, 0.8f), null, 2));
                }
            }

            // Medallions.
            int progress = hard ? G.S.hardCleared : G.S.cleared;
            int current = Mathf.Min(Chapters.Length - 1, progress / StagesPerChapter);
            for (int c = 0; c < Chapters.Length; c++)
            {
                int first = c * StagesPerChapter;
                bool locked = hard ? first > G.S.hardCleared || first >= G.S.cleared : first > G.S.cleared;
                int done = Mathf.Clamp(progress - first, 0, StagesPerChapter);
                var p = MapPos(map, c); float s = 50;
                var m = new Rect(p.x - s / 2, p.y - s / 2, s, s);
                if (c == current && !locked) GUI.DrawTexture(new Rect(m.x - 10, m.y - 10, s + 20, s + 20), Ui.RoundTex(new Color(1f, 0.85f, 0.4f, 0.28f), null, 35));
                var el = Game.ChapterElement(c);
                GUI.DrawTexture(m, Ui.RoundTex(locked ? Palette.Hex("#2a2430") : Palette.Hex("#1d1726"), _chapterOpen == c ? Palette.TrimHi : locked ? Palette.Bronze : Palette.Trim, 25));
                Icons.Draw(new Rect(m.x + 9, m.y + 7, s - 18, s - 18), locked ? Icon.Lock : Icons.ForElement(el), locked ? Palette.Dim : Palette.Element(el));
                Ui.Text(new Rect(m.x, m.yMax - 18, s, 14), (c + 1).ToString(), 11, locked ? Palette.Dim : Palette.Gold, FontStyle.Bold, TextAnchor.MiddleCenter);
                // Six pips: stages cleared in this chapter.
                for (int k = 0; k < StagesPerChapter; k++)
                    GUI.DrawTexture(new Rect(p.x - 21 + k * 7.5f, m.yMax + 3, 6, 6), Ui.RoundTex(k < done ? Palette.Gold : new Color(0, 0, 0, 0.55f), Palette.TrimLo, 3));
                var label = new Rect(p.x - 70, m.yMax + 10, 140, 16);
                GUI.DrawTexture(new Rect(label.center.x - 56, label.y, 112, 16), Ui.RoundTex(new Color(0.08f, 0.06f, 0.1f, 0.7f), null, 7));
                Ui.Text(label, Chapters[c], 10, locked ? Palette.Dim : Palette.Text, FontStyle.Bold, TextAnchor.MiddleCenter);
                if (!locked && Ui.Button(new Rect(m.x - 6, m.y - 6, s + 12, s + 34), "", Ui.Kind.Ghost)) _chapterOpen = _chapterOpen == c ? -1 : c;
            }

            // Title and Normal/Hard switch.
            Ui.Heading(new Rect(map.center.x - 150, map.y + 6, 300, 30), hard ? "Hard Campaign" : "Campaign", 22, TextAnchor.MiddleCenter);
            if (Ui.Button(new Rect(map.x + 10, map.y + 10, 80, 30), "Normal", hard ? Ui.Kind.Chip : Ui.Kind.ChipOn, 12)) { _hardTab = false; _chapterOpen = -1; }
            if (Ui.Button(new Rect(map.x + 96, map.y + 10, 80, 30), "Hard", hard ? Ui.Kind.ChipOn : Ui.Kind.Chip, 12, G.S.cleared > 0)) { _hardTab = true; _chapterOpen = -1; }
            Ui.Text(new Rect(map.xMax - 190, map.y + 10, 180, 30), hard ? $"Hard {G.S.hardCleared}/{G.S.cleared}" : $"{G.S.cleared}/{TotalStages} stages", 12, Palette.Text, FontStyle.Bold, TextAnchor.MiddleRight);

            if (_chapterOpen >= 0 && _chapterOpen < Chapters.Length) DrawChapterPanel(map, _chapterOpen, hard);
            if (!hard) DrawRewardTrack(new Rect(area.x + 10, map.yMax + 6, area.width - 20, trackH - 8));
        }

        Vector2 MapPos(Rect map, int c) => new Vector2(map.x + MapPoints[c].x * map.width, map.y + MapPoints[c].y * map.height);

        // The chosen chapter's stages, in a panel on the side of the map away from its medallion.
        void DrawChapterPanel(Rect map, int c, bool hard)
        {
            float w = Mathf.Min(330, map.width * 0.4f), h = Mathf.Min(map.height - 20, 200);
            bool right = MapPoints[c].x < 0.5f;
            var r = new Rect(right ? map.xMax - w - 10 : map.x + 10, map.yMax - h - 10, w, h);
            Ui.Card(r, true);
            int first = c * StagesPerChapter;
            Ui.Text(new Rect(r.x + 14, r.y + 10, r.width - 60, 22), $"{c + 1}. {Chapters[c]}", 15, Palette.Gold, FontStyle.Bold, TextAnchor.MiddleLeft, false, true);
            Ui.Text(new Rect(r.x + 14, r.y + 32, r.width - 28, 18), $"{Game.ChapterElement(c)} · Lv {Game.StageLevel(first) + (hard ? Game.HardLvl : 0)}–{Game.StageLevel(first + 5) + (hard ? Game.HardLvl : 0)}{(hard ? " · Hard" : "")}", 12, Palette.Dim);
            var close = new Rect(r.xMax - 38, r.y + 8, 30, 30);
            if (Ui.Button(close, "", Ui.Kind.Ghost)) { _chapterOpen = -1; return; }
            Icons.Draw(new Rect(close.x + 8, close.y + 8, 14, 14), Icon.Close, Palette.Dim);
            float bw = (r.width - 28 - 12) / 3f, bh = (r.height - 66 - 16) / 2f;
            for (int s = 0; s < StagesPerChapter; s++)
            {
                int idx = first + s;
                var b = new Rect(r.x + 14 + (s % 3) * (bw + 6), r.y + 58 + (s / 3) * (bh + 6), bw, bh);
                bool open = hard ? G.HardOpen(idx) : idx <= G.S.cleared;
                bool clear = hard ? idx < G.S.hardCleared : idx < G.S.cleared;
                bool next = hard ? idx == G.S.hardCleared : idx == G.S.cleared;
                var kind = clear ? Ui.Kind.Secondary : next ? Ui.Kind.Primary : Ui.Kind.Chip;
                string label = (Game.IsBossStage(idx) ? "Boss " : "") + Game.StageLabel(idx) + (clear ? "\nCleared" : next ? "\nNext" : "");
                if (Ui.Button(b, label, kind, 12, open)) OpenPrep(idx, false, ScreenId.Campaign, hard);
            }
        }

        // Boss first-clear rewards per chapter (Game.StageRewards): ticked once claimed.
        void DrawRewardTrack(Rect r)
        {
            GUI.DrawTexture(r, Ui.RoundTex(new Color(0.07f, 0.05f, 0.1f, 0.85f), Palette.Bronze, 10));
            float step = r.width / Chapters.Length;
            for (int c = 0; c < Chapters.Length; c++)
            {
                int boss = c * StagesPerChapter + StagesPerChapter - 1;
                var rw = Game.StageRewards(boss, true);
                bool got = boss < G.S.cleared;
                float cx = r.x + step * (c + 0.5f);
                if (c > 0) GUI.DrawTexture(new Rect(cx - step + 14, r.center.y - 1, step - 28, 2), Ui.Solid(got ? Palette.Gold : Palette.Bronze));
                var slot = new Rect(cx - 14, r.center.y - 14, 28, 28);
                GUI.DrawTexture(slot, Ui.RoundTex(got ? Palette.Hex("#3a2c14") : Palette.Hex("#1d1726"), got ? Palette.Gold : Palette.Bronze, 14));
                Icons.Draw(new Rect(slot.x + 6, slot.y + 6, 16, 16), Icon.Shard, rw.Radiant > 0 ? Palette.Hex("#ffb02e") : Palette.Blue);
                if (rw.Radiant > 0) Ui.Text(new Rect(slot.xMax - 4, slot.y - 6, 20, 14), "+R", 9, Palette.Hex("#ffb02e"), FontStyle.Bold);
                if (got) Icons.Draw(new Rect(slot.xMax - 10, slot.yMax - 10, 12, 12), Icon.Check, Palette.Good);
            }
        }

        // Parchment map of Sigilbound, generated once: value-noise land raised along the road,
        // sea, coast lines, hill shading and a darkened border.
        static Texture2D MapTexture()
        {
            if (_mapTex != null) return _mapTex;
            const int W = 512, H = 256;
            var px = new Color[W * H];
            Color sea = Palette.Hex("#2f5a5e"), seaDeep = Palette.Hex("#1e3d44"), land = Palette.Hex("#c9b083"), landHi = Palette.Hex("#ddc79a"), hill = Palette.Hex("#9c8458"), coast = Palette.Hex("#5a4426");
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)W, v = y / (float)H;
                    float n = Fbm(u * 6f, v * 3f);
                    // Raise land near the road so every chapter sits on solid ground.
                    float road = 1f;
                    for (int c = 0; c + 1 < MapPoints.Length; c++) road = Mathf.Min(road, SegDist(new Vector2(u, v), MapPoints[c], MapPoints[c + 1]));
                    float h = n * 0.75f + Mathf.Clamp01(1 - road / 0.16f) * 0.45f - 0.18f;
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
            _mapTex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "world_map" };
            _mapTex.SetPixels(flipped); _mapTex.Apply();
            return _mapTex;
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
