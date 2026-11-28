// APK size (performance review #6, QA #9): the packed model container (Core/ModelPack.cs) is lossless,
// what prepare-models.mjs ships decodes to valid GLBs within the snapping tolerance, and the prepared
// StreamingAssets fit the APK budget. On CI the budget check must run: a missing prepared folder fails.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Sigilbound.Core;

static class ModelPackTests
{
    // Play's limit for the base download is 200 MB; CI fails the APK above this.
    public const long ApkBudget = 190_000_000;
    // Everything in the APK besides StreamingAssets/Models (IL2CPP, engine, Resources, dex), measured from
    // build 169 (46.3 MB) plus headroom. The android job checks the real APK.
    const long ApkBaseline = 48_000_000;

    static bool OnCi => Environment.GetEnvironmentVariable("CI") == "true";

    public static void Run(Action<bool, string> check)
    {
        // Synthetic GLB: two interleaved vertex views and an index view.
        var glb = MakeGlb(out _);
        var packed = ModelPack.Pack(glb);
        check(ModelPack.IsPacked(packed) && ModelPack.Regions(glb).Count == 2 && ModelPack.Unpack(packed).SequenceEqual(glb), "model pack: a packed GLB unpacks to the exact same bytes");
        check(ReferenceEquals(ModelPack.Unpack(glb), glb), "model pack: a plain GLB passes through untouched");
        bool threw = false;
        var broken = (byte[])packed.Clone(); BitConverter.GetBytes(int.MaxValue).CopyTo(broken, 12);
        try { ModelPack.Unpack(broken); } catch (FormatException) { threw = true; }
        check(threw, "model pack: a damaged region table is an error, not a garbled model");

        string root = FindUp("Tools~");
        string hdSrc = root == null ? null : Path.Combine(root, "Tools~", "models", "heroes_hd");
        if (hdSrc != null && Directory.Exists(hdSrc))
        {
            var files = Directory.GetFiles(hdSrc, "*_HD.glb").OrderBy(f => f).ToList();
            files.Add(Path.Combine(root, "Tools~", "models", "ubc_anims.glb"));
            // prepare-models.mjs ships whichever is smaller per file, packed or plain (image-to-3D meshes gzip better
            // plain). The check compares what ships (each file's chosen form) with all-plain, over every fourth file
            // so it mixes kit-built and image-to-3D heroes: the chosen form is never larger than plain, packing is
            // chosen for some files, and it still pays overall. It used to require the sampled total under 97% of
            // plain, which a batch of image-to-3D heroes (96.x% at the time) could trip without anything being wrong.
            int exact = 0, idx = 0, packedWins = 0, worse = 0; long gzPlain = 0, gzChosen = 0;
            foreach (var f in files.Where(File.Exists))
            {
                var raw = File.ReadAllBytes(f);
                var p = ModelPack.Pack(raw);
                if (ModelPack.Unpack(p).SequenceEqual(raw)) exact++;
                if (idx++ % 4 == 0 && gzPlain < 6_000_000)
                {
                    long g = Gz(raw), gp = Gz(p), chosen = Math.Min(g, gp);
                    gzPlain += g; gzChosen += chosen;
                    if (gp < g) packedWins++;
                    if (chosen > g) worse++;
                }
            }
            check(exact == files.Count(File.Exists), $"model pack: all {exact} shipped hero/clip GLBs round-trip exactly");
            check(worse == 0 && packedWins > 0 && gzChosen < gzPlain, $"model pack: the shipped form (packed where it gzips smaller: {packedWins} of the sampled files) is {100.0 * gzChosen / Math.Max(1, gzPlain):0.0}% of all-plain gzip, never larger per file");
        }

        // What prepare-models.mjs wrote (CI prepares it before these checks).
        string sa = root == null ? null : Path.Combine(root, "Assets", "StreamingAssets", "Models");
        string hdOut = sa == null ? null : Path.Combine(sa, "heroes_hd");
        if (hdOut == null || !Directory.Exists(hdOut) || !Directory.GetFiles(hdOut, "*.glbz").Any())
        {
            check(!OnCi, "apk budget: StreamingAssets/Models not prepared" + (OnCi ? " (run prepare-models.mjs before the tests on CI)" : " here (skipped)"));
            return;
        }
        int ok = 0, n = 0; double worstPos = 0, worstW = 0; var bad = new List<string>();
        foreach (var z in Directory.GetFiles(hdOut, "*_HD.glbz").OrderBy(f => f))
        {
            n++;
            var name = Path.GetFileName(z);
            var src = Path.Combine(hdSrc ?? "", name.Substring(0, name.Length - 1));
            try
            {
                var plain = ModelPack.Unpack(Gunzip(File.ReadAllBytes(z)));
                bool header = Encoding.ASCII.GetString(plain, 0, 4) == "glTF" && BitConverter.ToInt32(plain, 8) == plain.Length;
                var (pos, w) = Attributes(plain);
                bool same = true;
                if (File.Exists(src))
                {
                    var (p0, w0) = Attributes(File.ReadAllBytes(src));
                    float ext = p0.Max(Math.Abs);
                    same = p0.Length == pos.Length && w0.Length == w.Length;
                    if (same)
                    {
                        for (int i = 0; i < pos.Length; i++) worstPos = Math.Max(worstPos, Math.Abs(pos[i] - p0[i]) / ext);
                        for (int i = 0; i < w.Length; i++) worstW = Math.Max(worstW, Math.Abs(w[i] - w0[i]));
                    }
                }
                bool sums = true;
                for (int i = 0; i + 3 < w.Length; i += 4) { float s = w[i] + w[i + 1] + w[i + 2] + w[i + 3]; if (s > 0.5f && Math.Abs(s - 1) > 1e-6) sums = false; }
                if (header && same && sums) ok++; else bad.Add(name);
            }
            catch (Exception e) { bad.Add(name + " " + e.GetType().Name); }
        }
        check(n > 0 && ok == n, $"model pack: all {n} prepared heroes unpack to valid GLBs with the same vertices and weights summing to 1 (the largest weight takes the others' rounding)" + (bad.Count > 0 ? " — not: " + string.Join(", ", bad.Take(4)) : ""));
        check(worstPos < 1.0 / 16384 && worstW <= 2.0 / 256 + 1e-6, $"model pack: snapping moves vertices at most {worstPos * 16384:0.00}/16384 of the model and weights at most {worstW * 256:0.00}/256");

        // What ships: each .glbz no larger than its content gzipped plain (prepare-models picked the smaller form).
        {
            int bigger = 0, seen = 0;
            foreach (var z in Directory.GetFiles(hdOut, "*_HD.glbz").OrderBy(f => f).Where((f, i) => i % 8 == 0))
            {
                var zb = File.ReadAllBytes(z); var plain = ModelPack.Unpack(Gunzip(zb)); seen++;
                if (zb.Length > Gz(plain) * 1.02) bigger++; // .NET's and zlib's level 9 differ by a little
            }
            check(seen > 0 && bigger == 0, $"model pack: each shipped .glbz is the smaller of packed and plain ({seen} sampled, {bigger} larger than plain gzip)");
        }

        long models = Directory.GetFiles(sa, "*", SearchOption.AllDirectories).Where(f => !f.EndsWith(".meta")).Sum(f => new FileInfo(f).Length);
        // the image-to-3D scenery landmarks came after build 169 (Resources/Scenery/Landmarks, counted raw: Unity compresses Resources)
        string lmDir = Path.Combine(root, "Assets", "Resources", "Scenery", "Landmarks");
        long landmarks = Directory.Exists(lmDir) ? Directory.GetFiles(lmDir, "*.bytes").Sum(f => new FileInfo(f).Length) : 0;
        long estimate = models + ApkBaseline + landmarks;
        check(estimate <= ApkBudget, $"apk budget: estimated APK {estimate / 1e6:0.0} MB (StreamingAssets/Models {models / 1e6:0.0} MB + {ApkBaseline / 1e6:0} MB + landmarks {landmarks / 1e6:0.0} MB) within {ApkBudget / 1e6:0} MB");
    }

    static string FindUp(string child)
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, child)) && Directory.Exists(Path.Combine(dir, "Assets"))) return dir;
        return null;
    }

    static long Gz(byte[] b)
    {
        using var ms = new MemoryStream();
        using (var gz = new GZipStream(ms, CompressionLevel.SmallestSize, true)) gz.Write(b, 0, b.Length);
        return ms.Length;
    }
    static byte[] Gunzip(byte[] b)
    {
        using var src = new MemoryStream(b); using var gz = new GZipStream(src, CompressionMode.Decompress); using var dst = new MemoryStream();
        gz.CopyTo(dst); return dst.ToArray();
    }

    // POSITION and WEIGHTS_0 of the first primitive as floats.
    static (float[] pos, float[] w) Attributes(byte[] glb)
    {
        int jsonLen = BitConverter.ToInt32(glb, 12), bin = 20 + jsonLen + 8;
        using var doc = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(glb, 20, jsonLen));
        var r = doc.RootElement; var accs = r.GetProperty("accessors"); var views = r.GetProperty("bufferViews");
        var attrs = r.GetProperty("meshes")[0].GetProperty("primitives")[0].GetProperty("attributes");
        float[] Read(string name, int comps)
        {
            if (!attrs.TryGetProperty(name, out var ai)) return new float[0];
            var a = accs[ai.GetInt32()]; var v = views[a.GetProperty("bufferView").GetInt32()];
            int count = a.GetProperty("count").GetInt32();
            int stride = v.TryGetProperty("byteStride", out var st) ? st.GetInt32() : comps * 4;
            int off = bin + (v.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
            var outp = new float[count * comps];
            for (int i = 0; i < count; i++) for (int k = 0; k < comps; k++) outp[i * comps + k] = BitConverter.ToSingle(glb, off + i * stride + k * 4);
            return outp;
        }
        return (Read("POSITION", 3), Read("WEIGHTS_0", 4));
    }

    // A small valid GLB: 40 vertices interleaved (position + weights, stride 28), 60 uint16 indices.
    static byte[] MakeGlb(out int vertices)
    {
        vertices = 40;
        var bin = new MemoryStream(); var bw = new BinaryWriter(bin);
        for (int i = 0; i < vertices; i++) { bw.Write(i * 0.01f); bw.Write(1f); bw.Write(-i * 0.02f); bw.Write(0.75f); bw.Write(0.25f); bw.Write(0f); bw.Write(0f); }
        for (int i = 0; i < 60; i++) bw.Write((ushort)(i % vertices));
        while (bin.Length % 4 != 0) bw.Write((byte)0);
        var binBytes = bin.ToArray();
        string json = "{\"asset\":{\"version\":\"2.0\"},\"buffers\":[{\"byteLength\":" + binBytes.Length + "}]," +
            "\"bufferViews\":[{\"buffer\":0,\"byteOffset\":0,\"byteLength\":" + vertices * 28 + ",\"byteStride\":28},{\"buffer\":0,\"byteOffset\":" + vertices * 28 + ",\"byteLength\":120}]," +
            "\"accessors\":[{\"bufferView\":0,\"componentType\":5126,\"count\":40,\"type\":\"VEC3\"},{\"bufferView\":0,\"byteOffset\":12,\"componentType\":5126,\"count\":40,\"type\":\"VEC4\"},{\"bufferView\":1,\"componentType\":5123,\"count\":60,\"type\":\"SCALAR\"}]," +
            "\"meshes\":[{\"primitives\":[{\"attributes\":{\"POSITION\":0,\"WEIGHTS_0\":1},\"indices\":2}]}]}";
        while (json.Length % 4 != 0) json += " ";
        var jb = Encoding.UTF8.GetBytes(json);
        var outp = new MemoryStream(); var w = new BinaryWriter(outp);
        w.Write(0x46546C67); w.Write(2); w.Write(12 + 8 + jb.Length + 8 + binBytes.Length);
        w.Write(jb.Length); w.Write(0x4E4F534A); w.Write(jb);
        w.Write(binBytes.Length); w.Write(0x004E4942); w.Write(binBytes);
        return outp.ToArray();
    }
}
