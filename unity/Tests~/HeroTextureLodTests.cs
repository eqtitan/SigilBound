// GPU-compressed hero textures and LOD1 meshes (PLAN step 119, PMO P2-1 / P2-2; performance review #1.4, #3).
// prepare-models.mjs ships every high-detail hero's maps as KTX2 (Basis Universal, transcoded on the device) and,
// where the mesh simplifies, a LOD1 ("SBL1", Core/HdLod.cs) over the shipped mesh. These checks read what it wrote:
// the KTX2 files are valid, mip-mapped and the only maps that ship; every LOD1 parses, matches its full mesh (vertex
// and index counts, first triangle and positions), keeps the full mesh's skin (same file, so same bones and clips;
// kept vertices' weights still sum to 1 over joints the skin has) and cuts the triangles to about 40-50%.
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Sigilbound.Core;

static class HeroTextureLodTests
{
    static bool OnCi => Environment.GetEnvironmentVariable("CI") == "true";

    public static void Run(Action<bool, string> check)
    {
        Synthetic(check);

        string root = FindUp();
        string hdOut = root == null ? null : Path.Combine(root, "Assets", "StreamingAssets", "Models", "heroes_hd");
        if (hdOut == null || !File.Exists(Path.Combine(hdOut, "index_ktx.txt")))
        {
            check(!OnCi, "hero textures/LOD1: StreamingAssets/Models/heroes_hd not prepared" + (OnCi ? " (run prepare-models.mjs before the tests on CI)" : " here (skipped)"));
            return;
        }
        var ids = Lines(Path.Combine(hdOut, "index.txt"));
        var ktx = Lines(Path.Combine(hdOut, "index_ktx.txt")).Select(l => l.Split(' ')).Where(p => p.Length == 2).ToDictionary(p => p[0], p => p[1]);

        // ---- KTX2 maps ----
        var missing = ids.Where(id => !ktx.TryGetValue(id, out var m) || !m.Contains("a")).ToList();
        check(missing.Count == 0, $"hero textures: all {ids.Count} high-detail heroes ship KTX2 maps" + (missing.Count > 0 ? " — not: " + string.Join(", ", missing.Take(5)) : ""));
        int files = 0, bad = 0; var badNames = new List<string>(); long ktxBytes = 0;
        foreach (var kv in ktx)
            foreach (var c in kv.Value)
            {
                string map = c == 'a' ? "albedo" : c == 'n' ? "normal" : c == 'm' ? "mask" : null;
                string f = map == null ? null : Path.Combine(hdOut, $"SB_Hero_{kv.Key}_HD_{map}.ktx2");
                files++;
                if (f == null || !File.Exists(f)) { bad++; badNames.Add($"{kv.Key} {c}"); continue; }
                var b = File.ReadAllBytes(f); ktxBytes += b.Length;
                string why = CheckKtx2(b, map);
                if (why != null) { bad++; badNames.Add($"{kv.Key} {map}: {why}"); }
            }
        check(files > 0 && bad == 0, $"hero textures: {files} KTX2 maps are Basis Universal KTX2 files with a full mip chain (albedo/normal ETC1S, mask UASTC+zstd), {ktxBytes / 1048576.0:0.0} MB" + (bad > 0 ? " — not: " + string.Join("; ", badNames.Take(4)) : ""));
        var plainMaps = Directory.GetFiles(hdOut).Select(Path.GetFileName).Where(n => n.EndsWith(".jpg") || n.EndsWith(".png")).ToList();
        check(plainMaps.Count == 0, "hero textures: no JPEG/PNG maps ship next to the KTX2 ones (one form per map)" + (plainMaps.Count > 0 ? " — " + string.Join(", ", plainMaps.Take(4)) : ""));
        // Memory on the device, from the formats KTX for Unity transcodes to: ETC1S -> ETC2 RGB / BC1 (0.5 byte per
        // pixel), UASTC -> ASTC 4x4 / BC7 / ETC2 RGBA (1 byte), each with mips (x4/3). Before: RGBA32 (4 bytes) with mips.
        double after = 0, before = 0; int heroes = 0;
        foreach (var kv in ktx)
        {
            heroes++;
            foreach (var c in kv.Value)
            {
                var f = Path.Combine(hdOut, $"SB_Hero_{kv.Key}_HD_{(c == 'a' ? "albedo" : c == 'n' ? "normal" : "mask")}.ktx2");
                if (!File.Exists(f)) continue;
                var b = File.ReadAllBytes(f); long px = (long)BitConverter.ToUInt32(b, 20) * BitConverter.ToUInt32(b, 24);
                after += px * (c == 'm' ? 1.0 : 0.5) * 4 / 3; before += px * 4.0 * 4 / 3;
            }
        }
        check(heroes > 0 && after * 5 < before, $"hero textures: GPU memory per hero about {after / heroes / 1048576:0.00} MB (was {before / heroes / 1048576:0.00} MB as RGBA32)");

        // ---- LOD1 ----
        var lod1 = Lines(Path.Combine(hdOut, "index_lod1.txt"));
        int ok = 0; var wrong = new List<string>(); double ratioSum = 0, vertSum = 0; long lodBytes = 0;
        foreach (var id in lod1)
        {
            string lf = Path.Combine(hdOut, $"SB_Hero_{id}_HD_LOD1.sbl"), gf = Path.Combine(hdOut, $"SB_Hero_{id}_HD.glbz");
            try
            {
                if (!File.Exists(lf) || !File.Exists(gf)) { wrong.Add(id + " missing file"); continue; }
                var lb = File.ReadAllBytes(lf); lodBytes += lb.Length;
                var lod = HdLod.Parse(Gunzip(lb));
                var glb = new Glb(ModelPack.Unpack(Gunzip(File.ReadAllBytes(gf))));
                var pos = glb.Floats("POSITION", 3); var w = glb.Floats("WEIGHTS_0", 4); var j = glb.Joints(); var idx = glb.Indices();
                int nv = pos.Length / 3;
                string why = null;
                if (lod.FullVertices != nv || lod.FullIndices != idx.Length) why = $"counts {lod.FullVertices}/{lod.FullIndices} vs mesh {nv}/{idx.Length}";
                else if (lod.FirstTriangle[0] != idx[0] || lod.FirstTriangle[1] != idx[1] || lod.FirstTriangle[2] != idx[2]) why = "first triangle differs";
                else if (Enumerable.Range(0, 9).Any(k => Math.Abs(lod.FirstPositions[k] - pos[lod.FirstTriangle[k / 3] * 3 + k % 3]) > 1e-6)) why = "first positions differ";
                else if (lod.TriangleRatio < 0.3f || lod.TriangleRatio > 0.6f) why = $"keeps {lod.TriangleRatio:P0} of the triangles";
                else if (lod.Indices.Distinct().Count() != lod.Keep.Length) why = "unused kept vertices";
                else
                {
                    // The LOD's vertices are the full mesh's own: gather them as HeroModels.BuildLodMesh does and check the skin.
                    var kw = lod.Gather(w, 4); var kj = lod.Gather(j, 4); int joints = glb.SkinJoints();
                    for (int i = 0; i < lod.Keep.Length && why == null; i++)
                    {
                        float s = kw[i * 4] + kw[i * 4 + 1] + kw[i * 4 + 2] + kw[i * 4 + 3];
                        if (Math.Abs(s - 1) > 1e-5) why = $"vertex {lod.Keep[i]} weights sum {s}";
                        for (int k = 0; k < 4 && why == null; k++) if (kw[i * 4 + k] > 0 && kj[i * 4 + k] >= joints) why = $"vertex {lod.Keep[i]} uses joint {kj[i * 4 + k]} of {joints}";
                    }
                    // Degenerate triangles would mean the simplifier collapsed edges without removing the face.
                    for (int t = 0; t < lod.Indices.Length && why == null; t += 3)
                        if (lod.Indices[t] == lod.Indices[t + 1] || lod.Indices[t + 1] == lod.Indices[t + 2] || lod.Indices[t] == lod.Indices[t + 2]) why = "degenerate triangle";
                    // Flipping for a Unity mesh (glTFast reverses the winding) gives a valid triangle list too.
                    if (why == null && lod.UnityTriangles(idx[0], idx[2], idx[1]) is int[] fl && (fl[1] != lod.Indices[2] || fl[2] != lod.Indices[1])) why = "winding flip";
                }
                if (why == null) { ok++; ratioSum += lod.TriangleRatio; vertSum += (double)lod.Keep.Length / lod.FullVertices; } else wrong.Add(id + ": " + why);
            }
            catch (Exception e) { wrong.Add(id + " " + e.GetType().Name + " " + e.Message); }
        }
        check(lod1.Count >= ids.Count * 2 / 3, $"hero LOD1: {lod1.Count} of {ids.Count} high-detail heroes have one (flat-shaded triangle soups and the Titan keep the full mesh)");
        check(lod1.Count > 0 && ok == lod1.Count, $"hero LOD1: all {ok} parse, match their full mesh, keep its skin (weights sum to 1, joints within the skin) and have no degenerate triangles; {100 * ratioSum / Math.Max(1, ok):0}% of the triangles and {100 * vertSum / Math.Max(1, ok):0}% of the vertices on average, {lodBytes / 1048576.0:0.0} MB"
            + (wrong.Count > 0 ? " — not: " + string.Join("; ", wrong.Take(4)) : ""));
        var orphan = Directory.GetFiles(hdOut, "*_HD_LOD1.sbl").Select(Path.GetFileName).Where(n => !lod1.Contains(n.Substring(8, n.Length - 8 - "_HD_LOD1.sbl".Length))).ToList();
        check(orphan.Count == 0, "hero LOD1: every shipped LOD1 file is listed in index_lod1.txt" + (orphan.Count > 0 ? " — " + string.Join(", ", orphan.Take(4)) : ""));
    }

    // Format checks on a hand-made file, independent of the prepared models.
    static void Synthetic(Action<bool, string> check)
    {
        // Full mesh of 6 vertices, 4 triangles; LOD keeps vertices 0, 2, 3, 5 with 2 triangles.
        var ms = new MemoryStream(); var bw = new BinaryWriter(ms);
        bw.Write(Encoding.ASCII.GetBytes("SBL1")); bw.Write(1); bw.Write(6); bw.Write(12); bw.Write(4); bw.Write(6);
        bw.Write(0); bw.Write(1); bw.Write(2); for (int i = 0; i < 9; i++) bw.Write(i * 0.5f);
        foreach (var d in new[] { 0, 2, 1, 2 }) bw.Write(d);
        foreach (var x in new ushort[] { 0, 1, 2, 2, 1, 3 }) bw.Write(x);
        var bytes = ms.ToArray();
        var l = HdLod.Parse(bytes);
        check(l.Keep.SequenceEqual(new[] { 0, 2, 3, 5 }) && l.Triangles == 2 && Math.Abs(l.TriangleRatio - 0.5f) < 1e-6 && l.FirstPositions[8] == 4f,
            "hero LOD1: an SBL1 file parses (kept vertices from deltas, triangles, first-triangle check data)");
        check(l.Gather(new[] { 10, 11, 12, 13, 14, 15 }).SequenceEqual(new[] { 10, 12, 13, 15 }) && l.Gather(new[] { 0, 0, 1, 1, 2, 2, 3, 3, 4, 4, 5, 5 }, 2).SequenceEqual(new[] { 0, 0, 2, 2, 3, 3, 5, 5 }),
            "hero LOD1: Gather picks the kept vertices' attributes (any stride)");
        var same = l.UnityTriangles(1, 2, 0); var flip = l.UnityTriangles(2, 1, 0);
        check(same != null && same.SequenceEqual(l.Indices) && flip != null && flip.SequenceEqual(new[] { 0, 2, 1, 2, 3, 1 }) && l.UnityTriangles(3, 4, 5) == null,
            "hero LOD1: the triangles follow the Unity mesh's winding (glTFast flips it) and a different mesh is refused");
        var broken = (byte[])bytes.Clone(); BitConverter.GetBytes(0).CopyTo(broken, 72 + 4); // second delta 0: not ascending
        bool threw = false; try { HdLod.Parse(broken); } catch (FormatException) { threw = true; }
        var shortFile = bytes.Take(bytes.Length - 2).ToArray(); bool threw2 = false; try { HdLod.Parse(shortFile); } catch (FormatException) { threw2 = true; }
        check(threw && threw2, "hero LOD1: a damaged LOD file is an error, not a garbled mesh");
    }

    // KTX2 header (Khronos KTX 2.0 spec, section 3): identifier, vkFormat, typeSize, pixelWidth, pixelHeight, depth,
    // layers, faces, levelCount, supercompressionScheme. Basis Universal files have vkFormat 0.
    static string CheckKtx2(byte[] b, string map)
    {
        var id = new byte[] { 0xAB, 0x4B, 0x54, 0x58, 0x20, 0x32, 0x30, 0xBB, 0x0D, 0x0A, 0x1A, 0x0A };
        if (b.Length < 80 || !b.Take(12).SequenceEqual(id)) return "not KTX2";
        uint vk = BitConverter.ToUInt32(b, 12), w = BitConverter.ToUInt32(b, 20), h = BitConverter.ToUInt32(b, 24), levels = BitConverter.ToUInt32(b, 40), sc = BitConverter.ToUInt32(b, 44);
        if (vk != 0) return $"vkFormat {vk}, not Basis";
        if (w < 64 || h < 64 || w % 4 != 0 || h % 4 != 0) return $"{w}x{h}"; // block formats need multiples of 4 (768 px normal maps are fine)
        uint want = 1; for (uint s = Math.Max(w, h); s > 1; s >>= 1) want++;
        if (levels != want) return $"{levels} mip levels of {want}";
        uint scheme = map == "mask" ? 2u : 1u; // 1 BasisLZ (ETC1S), 2 Zstandard (UASTC)
        if (sc != scheme) return $"supercompression {sc}, expected {scheme}";
        return null;
    }

    static List<string> Lines(string f) => File.Exists(f) ? File.ReadAllLines(f).Select(x => x.Trim()).Where(x => x.Length > 0).ToList() : new List<string>();

    static string FindUp()
    {
        string dir = AppContext.BaseDirectory;
        for (int i = 0; i < 8 && dir != null; i++, dir = Path.GetDirectoryName(dir))
            if (Directory.Exists(Path.Combine(dir, "Tools~")) && Directory.Exists(Path.Combine(dir, "Assets"))) return dir;
        return null;
    }

    static byte[] Gunzip(byte[] b)
    {
        using var src = new MemoryStream(b); using var gz = new GZipStream(src, CompressionMode.Decompress); using var dst = new MemoryStream();
        gz.CopyTo(dst); return dst.ToArray();
    }

    // Just enough of a GLB reader: the first primitive's attributes and indices, and the skin's joint count.
    sealed class Glb
    {
        readonly byte[] _b; readonly int _bin; readonly System.Text.Json.JsonElement _r, _prim;
        public Glb(byte[] b)
        {
            _b = b; int jsonLen = BitConverter.ToInt32(b, 12); _bin = 20 + jsonLen + 8;
            _r = System.Text.Json.JsonDocument.Parse(Encoding.UTF8.GetString(b, 20, jsonLen)).RootElement;
            _prim = _r.GetProperty("meshes")[0].GetProperty("primitives")[0];
        }
        (int off, int count, int stride, int type) Acc(int ai, int defStride)
        {
            var a = _r.GetProperty("accessors")[ai]; var v = _r.GetProperty("bufferViews")[a.GetProperty("bufferView").GetInt32()];
            int stride = v.TryGetProperty("byteStride", out var st) ? st.GetInt32() : defStride;
            int off = _bin + (v.TryGetProperty("byteOffset", out var vo) ? vo.GetInt32() : 0) + (a.TryGetProperty("byteOffset", out var ao) ? ao.GetInt32() : 0);
            return (off, a.GetProperty("count").GetInt32(), stride, a.GetProperty("componentType").GetInt32());
        }
        public float[] Floats(string name, int comps)
        {
            var (off, count, stride, _) = Acc(_prim.GetProperty("attributes").GetProperty(name).GetInt32(), comps * 4);
            var o = new float[count * comps];
            for (int i = 0; i < count; i++) for (int k = 0; k < comps; k++) o[i * comps + k] = BitConverter.ToSingle(_b, off + i * stride + k * 4);
            return o;
        }
        public int[] Joints()
        {
            int ai = _prim.GetProperty("attributes").GetProperty("JOINTS_0").GetInt32();
            var t = _r.GetProperty("accessors")[ai].GetProperty("componentType").GetInt32(); int size = t == 5121 ? 1 : 2;
            var (off, count, stride, _) = Acc(ai, 4 * size);
            var o = new int[count * 4];
            for (int i = 0; i < count; i++) for (int k = 0; k < 4; k++) o[i * 4 + k] = size == 1 ? _b[off + i * stride + k] : BitConverter.ToUInt16(_b, off + i * stride + k * 2);
            return o;
        }
        public int[] Indices()
        {
            int ai = _prim.GetProperty("indices").GetInt32();
            var t = _r.GetProperty("accessors")[ai].GetProperty("componentType").GetInt32(); int size = t == 5125 ? 4 : t == 5123 ? 2 : 1;
            var (off, count, _, _) = Acc(ai, size);
            var o = new int[count];
            for (int i = 0; i < count; i++) o[i] = size == 4 ? (int)BitConverter.ToUInt32(_b, off + i * 4) : size == 2 ? BitConverter.ToUInt16(_b, off + i * 2) : _b[off + i];
            return o;
        }
        public int SkinJoints() => _r.GetProperty("skins")[0].GetProperty("joints").GetArrayLength();
    }
}
