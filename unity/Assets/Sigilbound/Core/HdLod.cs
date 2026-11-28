// LOD1 of a high-detail hero (performance review #3, PLAN P2-2). prepare-models.mjs simplifies the shipped
// mesh with meshoptimizer and writes SB_Hero_<id>_HD_LOD1.sbl (gzipped): which of the full mesh's vertices
// the LOD keeps, and its triangles over those kept vertices. No vertex is moved or made up, so positions,
// normals, UVs and skin weights are the full mesh's own; bones and clips are the same file's. HeroModels
// builds the lighter Unity mesh from the full one (Low and Medium tiers, Hollow enemies on every tier).
//
// Layout, little-endian: "SBL1", int version (1), int full vertex count, int full index count, int kept vertex
// count, int LOD index count, int[3] the full mesh's first triangle, float[9] its glTF positions (a check that
// the Unity mesh has the vertices in file order), int[kept] ascending kept vertex ids as deltas, then the LOD
// indices into the kept list: ushort, or int when more than 65535 vertices are kept.
using System;

namespace Sigilbound.Core
{
    public sealed class HdLod
    {
        public const int Version = 1;
        public int FullVertices, FullIndices;
        public int[] FirstTriangle = new int[3];
        public float[] FirstPositions = new float[9];
        public int[] Keep;    // full-mesh vertex id of each LOD vertex, ascending
        public int[] Indices; // LOD triangles, glTF winding, into Keep

        public int Triangles => Indices.Length / 3;
        public float TriangleRatio => FullIndices > 0 ? (float)Indices.Length / FullIndices : 0;

        public static HdLod Parse(byte[] d)
        {
            if (d == null || d.Length < 72 || d[0] != 'S' || d[1] != 'B' || d[2] != 'L' || d[3] != '1') throw new FormatException("SBL1: not a LOD file");
            if (BitConverter.ToInt32(d, 4) != Version) throw new FormatException("SBL1: unknown version");
            var l = new HdLod { FullVertices = BitConverter.ToInt32(d, 8), FullIndices = BitConverter.ToInt32(d, 12) };
            int keep = BitConverter.ToInt32(d, 16), n = BitConverter.ToInt32(d, 20);
            bool wide = keep > 65535;
            if (keep <= 0 || keep > l.FullVertices || n <= 0 || n % 3 != 0 || l.FullIndices % 3 != 0 || 72L + keep * 4L + n * (wide ? 4L : 2L) != d.Length) throw new FormatException("SBL1: bad sizes");
            for (int k = 0; k < 3; k++) l.FirstTriangle[k] = BitConverter.ToInt32(d, 24 + 4 * k);
            for (int k = 0; k < 9; k++) l.FirstPositions[k] = BitConverter.ToSingle(d, 36 + 4 * k);
            l.Keep = new int[keep];
            int o = 72, v = 0;
            for (int i = 0; i < keep; i++, o += 4)
            {
                int delta = BitConverter.ToInt32(d, o);
                if (i > 0 && delta <= 0) throw new FormatException("SBL1: kept vertices not ascending");
                v = i == 0 ? delta : v + delta;
                if (v < 0 || v >= l.FullVertices) throw new FormatException("SBL1: kept vertex out of range");
                l.Keep[i] = v;
            }
            l.Indices = new int[n];
            for (int i = 0; i < n; i++)
            {
                int x = wide ? BitConverter.ToInt32(d, o) : BitConverter.ToUInt16(d, o);
                o += wide ? 4 : 2;
                if (x < 0 || x >= keep) throw new FormatException("SBL1: index out of range");
                l.Indices[i] = x;
            }
            return l;
        }

        // Picks out the kept elements of a per-vertex array of the full mesh (stride values per vertex).
        public T[] Gather<T>(T[] full, int stride = 1)
        {
            var r = new T[Keep.Length * stride];
            for (int i = 0; i < Keep.Length; i++) Array.Copy(full, Keep[i] * stride, r, i * stride, stride);
            return r;
        }

        // The LOD triangles for a Unity mesh whose first triangle is unityFirst: glTFast reverses the winding
        // (glTF is counter-clockwise, Unity clockwise), so when the Unity mesh is flipped, so are these.
        // Null when unityFirst isn't the file's first triangle in either winding (not the mesh this LOD was made for).
        public int[] UnityTriangles(int u0, int u1, int u2)
        {
            int a = FirstTriangle[0], b = FirstTriangle[1], c = FirstTriangle[2];
            bool same = (u0 == a && u1 == b && u2 == c) || (u0 == b && u1 == c && u2 == a) || (u0 == c && u1 == a && u2 == b);
            bool flip = (u0 == a && u1 == c && u2 == b) || (u0 == c && u1 == b && u2 == a) || (u0 == b && u1 == a && u2 == c);
            if (!same && !flip) return null;
            var t = (int[])Indices.Clone();
            if (flip) for (int i = 0; i < t.Length; i += 3) { int x = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = x; }
            return t;
        }
    }
}
