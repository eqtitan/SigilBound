// Packed GLB container for the shipped hero models (APK size, performance review #6). prepare-models.mjs
// writes "SBZ1" + a region table + the GLB with each vertex/index buffer view byte-transposed (all first
// bytes of every element, then all second bytes, ...), then gzips the lot into NAME.glbz. Transposed float
// data compresses far better with gzip, and the transform is lossless: Unpack gives back the exact GLB
// bytes, so glTFast still reads plain float POSITION / WEIGHTS_0 (never quantized: glTFast left skinned
// heroes in their bind pose when those were quantized). HeroModels.ReadBytes unzips, then Unpack.
// Data that doesn't start with "SBZ1" (a plain GLB) is returned as it is.
using System;
using System.Collections.Generic;
using System.Text;

namespace Sigilbound.Core
{
    public static class ModelPack
    {
        static readonly byte[] Magic = { (byte)'S', (byte)'B', (byte)'Z', (byte)'1' };
        public const int Version = 1;

        public static bool IsPacked(byte[] d) => d != null && d.Length >= 12 && d[0] == Magic[0] && d[1] == Magic[1] && d[2] == Magic[2] && d[3] == Magic[3];

        public static byte[] Unpack(byte[] d)
        {
            if (!IsPacked(d)) return d;
            int version = BitConverter.ToInt32(d, 4), n = BitConverter.ToInt32(d, 8);
            if (version != Version || n < 0 || 12 + (long)n * 12 > d.Length) throw new FormatException("SBZ1: bad header");
            int head = 12 + n * 12;
            var glb = new byte[d.Length - head];
            Buffer.BlockCopy(d, head, glb, 0, glb.Length);
            for (int r = 0; r < n; r++)
            {
                int off = BitConverter.ToInt32(d, 12 + r * 12), len = BitConverter.ToInt32(d, 16 + r * 12), stride = BitConverter.ToInt32(d, 20 + r * 12);
                if (off < 0 || len < 0 || stride < 2 || (long)off + len > glb.Length || len % stride != 0) throw new FormatException("SBZ1: bad region");
                int count = len / stride;
                // packed[k * count + i] = plain[i * stride + k]
                for (int k = 0; k < stride; k++)
                {
                    int src = head + off + k * count, dst = off + k;
                    for (int i = 0; i < count; i++, dst += stride) glb[dst] = d[src + i];
                }
            }
            return glb;
        }

        // The C# side of the packer (prepare-models.mjs is the one the build uses); used by the tests.
        public static byte[] Pack(byte[] glb)
        {
            var regions = Regions(glb);
            var outp = new byte[12 + regions.Count * 12 + glb.Length];
            Buffer.BlockCopy(Magic, 0, outp, 0, 4);
            BitConverter.GetBytes(Version).CopyTo(outp, 4);
            BitConverter.GetBytes(regions.Count).CopyTo(outp, 8);
            int head = 12 + regions.Count * 12;
            Buffer.BlockCopy(glb, 0, outp, head, glb.Length);
            for (int r = 0; r < regions.Count; r++)
            {
                var (off, len, stride) = regions[r];
                BitConverter.GetBytes(off).CopyTo(outp, 12 + r * 12);
                BitConverter.GetBytes(len).CopyTo(outp, 16 + r * 12);
                BitConverter.GetBytes(stride).CopyTo(outp, 20 + r * 12);
                int count = len / stride;
                for (int k = 0; k < stride; k++)
                {
                    int dst = head + off + k * count, src = off + k;
                    for (int i = 0; i < count; i++, src += stride) outp[dst + i] = glb[src];
                }
            }
            return outp;
        }

        // Buffer views read by accessors, with their element stride (byteStride, or one element's size),
        // as offsets into the GLB file. Same rule as packRegions() in prepare-models.mjs.
        public static List<(int off, int len, int stride)> Regions(byte[] glb)
        {
            var list = new List<(int, int, int)>();
            if (glb.Length < 28 || Encoding.ASCII.GetString(glb, 0, 4) != "glTF") return list;
            int jsonLen = BitConverter.ToInt32(glb, 12);
            if (BitConverter.ToInt32(glb, 16) != 0x4E4F534A) return list; // "JSON"
            int binChunk = 20 + jsonLen;
            if (binChunk + 8 > glb.Length) return list;
            int binStart = binChunk + 8, binLen = BitConverter.ToInt32(glb, binChunk);
            var root = Json.Parse(Encoding.UTF8.GetString(glb, 20, jsonLen)) as Dictionary<string, object>;
            var views = Json.Arr(root, "bufferViews"); var accs = Json.Arr(root, "accessors");
            if (views == null || accs == null) return list;
            var strideOf = new Dictionary<int, int>();
            foreach (var a in accs)
            {
                if (!Json.Has(a, "bufferView")) continue;
                int v = Json.Int(a, "bufferView");
                if (strideOf.ContainsKey(v)) continue;
                int comp = Json.Int(a, "componentType") switch { 5120 => 1, 5121 => 1, 5122 => 2, 5123 => 2, _ => 4 };
                int n = Json.Str(a, "type") switch { "SCALAR" => 1, "VEC2" => 2, "VEC3" => 3, "VEC4" => 4, "MAT2" => 4, "MAT3" => 9, "MAT4" => 16, _ => 1 };
                strideOf[v] = comp * n;
            }
            for (int i = 0; i < views.Count; i++)
            {
                if (!strideOf.TryGetValue(i, out int stride)) continue;
                var bv = views[i];
                if (Json.Int(bv, "buffer") != 0) continue;
                if (Json.Has(bv, "byteStride")) stride = Json.Int(bv, "byteStride");
                int off = Json.Int(bv, "byteOffset"), len = Json.Int(bv, "byteLength");
                int count = stride > 0 ? len / stride : 0;
                if (stride < 2 || count < 16 || off < 0 || off + len > binLen) continue;
                list.Add((binStart + off, count * stride, stride));
            }
            // Overlapping views (allowed by glTF, never made by our exporter) are left plain.
            list.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            var clean = new List<(int, int, int)>(); int end = 0;
            foreach (var r in list) if (r.Item1 >= end) { clean.Add(r); end = r.Item1 + r.Item2; }
            return clean;
        }
    }
}
