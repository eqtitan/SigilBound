// Download size guard (PLAN.md step 58): the high-detail heroes are most of the APK. Unity stores
// StreamingAssets uncompressed, so prepare-models.mjs gzips the GLBs and compress-textures.py shrinks
// the textures. These checks keep the source folder inside a budget, keep every texture encoded by
// the texture step, and keep POSITION and skin WEIGHTS as plain floats (never quantized: the game
// must not risk skinned heroes stuck in their bind pose).
using System.IO;
using System.Linq;

static class ApkSizeTests
{
    public static void Run(System.Action<bool, string> check)
    {
        // Android's packaging un-gzips assets named *.gz and ships them full size (build 102): the packed
        // models must use another extension (.glbz).
        {
            string d0 = System.AppContext.BaseDirectory, prep = null;
            for (int i = 0; i < 8 && d0 != null && prep == null; i++, d0 = Path.GetDirectoryName(d0))
            { var p0 = Path.Combine(d0, "Tools~", "models", "prepare-models.mjs"); if (File.Exists(p0)) prep = p0; }
            if (prep != null) { var t = File.ReadAllText(prep); check(!t.Contains("\".gz\"") && t.Contains("p + \"z\""), "apk size: packed models are .glbz, not .gz (Android un-gzips .gz assets)"); }
        }
        string dir = System.AppContext.BaseDirectory, hd = null;
        for (int i = 0; i < 8 && dir != null && hd == null; i++, dir = Path.GetDirectoryName(dir))
        {
            var p = Path.Combine(dir, "Tools~", "models", "heroes_hd");
            if (Directory.Exists(p)) hd = p;
        }
        // On CI a missing folder fails instead of passing as "skipped" (QA review 2026-11-27, #9).
        if (hd == null) { bool ci = System.Environment.GetEnvironmentVariable("CI") == "true"; check(!ci, "apk size: heroes_hd not found here" + (ci ? "" : " (skipped)")); return; }
        var files = Directory.GetFiles(hd);
        int heroes = files.Count(f => f.EndsWith("_HD.glb"));
        long tex = files.Where(f => f.EndsWith(".jpg") || f.EndsWith(".png")).Sum(f => new FileInfo(f).Length);
        // Textures per hero after compress-textures.py: about 0.28 MB (albedo 0.19, normal 0.085, glow 0.006).
        check(tex <= heroes * 340_000L, $"apk size: textures {tex / 1048576.0:0.0} MB for {heroes} heroes (budget 0.34 MB each; run models/compress-textures.py)");
        var unmarked = files.Where(f => f.EndsWith("_albedo.jpg") || f.EndsWith("_normal.jpg")).Where(f => !HasMark(f)).Select(Path.GetFileName).ToList();
        check(unmarked.Count == 0, "apk size: every albedo/normal went through compress-textures.py" + (unmarked.Count > 0 ? " — not: " + string.Join(", ", unmarked.Take(4)) : ""));
        var pngMaps = files.Where(f => f.EndsWith("_albedo.png") || f.EndsWith("_normal.png")).Select(Path.GetFileName).ToList();
        check(pngMaps.Count == 0, "apk size: no PNG albedo/normal maps ship" + (pngMaps.Count > 0 ? " — " + string.Join(", ", pngMaps.Take(4)) : ""));
        // Geometry: about 1.25 MB raw per hero (gzipped to ~0.53 MB at build time).
        long glb = files.Where(f => f.EndsWith(".glb")).Sum(f => new FileInfo(f).Length);
        check(glb <= heroes * 1_800_000L, $"apk size: models {glb / 1048576.0:0.0} MB raw for {heroes} heroes (budget 1.8 MB each)");
        var quantized = files.Where(f => f.EndsWith(".glb")).Where(f => !FloatPosWeights(f)).Select(Path.GetFileName).ToList();
        check(quantized.Count == 0, "apk size: POSITION and WEIGHTS_0 stay float in every hero" + (quantized.Count > 0 ? " — not: " + string.Join(", ", quantized.Take(4)) : ""));
    }

    static bool HasMark(string f)
    {
        var b = File.ReadAllBytes(f);
        var mark = System.Text.Encoding.ASCII.GetBytes("sb-tex-v1");
        for (int i = 2; i + 4 < b.Length && b[i] == 0xFF; )
        {
            int marker = b[i + 1], len = (b[i + 2] << 8) | b[i + 3];
            if (marker == 0xFE && len - 2 == mark.Length && b.Skip(i + 4).Take(mark.Length).SequenceEqual(mark)) return true;
            if (marker == 0xDA) break; // start of scan: no more headers
            i += 2 + len;
        }
        return false;
    }

    static bool FloatPosWeights(string f)
    {
        var b = File.ReadAllBytes(f);
        int n = System.BitConverter.ToInt32(b, 12);
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(b, 20, n));
        var acc = doc.RootElement.GetProperty("accessors");
        foreach (var m in doc.RootElement.GetProperty("meshes").EnumerateArray())
            foreach (var pr in m.GetProperty("primitives").EnumerateArray())
                foreach (var name in new[] { "POSITION", "WEIGHTS_0" })
                    if (pr.GetProperty("attributes").TryGetProperty(name, out var ai) && acc[ai.GetInt32()].GetProperty("componentType").GetInt32() != 5126) return false;
        return true;
    }
}
