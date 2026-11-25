// Code-built low-poly scenery meshes in the web game's faceted style (game3d/scenery.mjs):
// jittered icosahedrons for tree canopies and rocks, stacked cones for pines, tapered trunks and
// crossed grass blades. Every mesh is flat-shaded (one normal per face, so facets catch the light),
// built once per variant and shared, so a forest costs a handful of meshes. All shapes original.
using System.Collections.Generic;
using UnityEngine;

namespace Sigilbound.Client
{
    public static class LowPoly
    {
        static readonly Dictionary<string, Mesh> _cache = new Dictionary<string, Mesh>();
        public const int Variants = 6;

        // Faceted blob: an icosahedron subdivided once, each vertex pushed in or out by `jitter`.
        public static Mesh Blob(int variant, float jitter) => Cached($"blob{variant}{jitter}", () =>
        {
            var rnd = new System.Random(1000 + variant * 37 + (int)(jitter * 100));
            var (v, t) = Icosphere(1);
            for (int i = 0; i < v.Count; i++) v[i] *= 1f + ((float)rnd.NextDouble() * 2 - 1) * jitter;
            return Flat(v, t, "blob");
        });

        // A cone with `sides` facets, base at y=0, tip at y=1, radius 1.
        public static Mesh Cone(int sides) => Cached($"cone{sides}", () =>
        {
            var v = new List<Vector3> { Vector3.up, Vector3.zero };
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2 / sides; v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); }
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int a = 2 + i, b = 2 + (i + 1) % sides;
                t.AddRange(new[] { 0, b, a }); // side
                t.AddRange(new[] { 1, a, b }); // base
            }
            return Flat(v, t, "cone");
        });

        // A tapered trunk: radius 1 at y=0 down to `top` at y=1.
        public static Mesh Trunk(int sides, float top) => Cached($"trunk{sides}{top}", () =>
        {
            var v = new List<Vector3>();
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2 / sides; v.Add(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))); }
            for (int i = 0; i < sides; i++) { float a = i * Mathf.PI * 2 / sides; v.Add(new Vector3(Mathf.Cos(a) * top, 1, Mathf.Sin(a) * top)); }
            var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                int a = i, b = (i + 1) % sides, c = sides + i, d = sides + (i + 1) % sides;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
            return Flat(v, t, "trunk");
        });

        // Grass tuft: crossed blades fanning out from the base, double-sided. Height 1.
        public static Mesh Tuft(int variant) => Cached($"tuft{variant}", () =>
        {
            var rnd = new System.Random(2000 + variant);
            var v = new List<Vector3>(); var t = new List<int>();
            int blades = 5;
            for (int i = 0; i < blades; i++)
            {
                float a = i * Mathf.PI / blades + (float)rnd.NextDouble() * 0.4f, lean = 0.15f + (float)rnd.NextDouble() * 0.25f;
                float h = 0.6f + (float)rnd.NextDouble() * 0.4f, w = 0.07f;
                var dir = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); var side = new Vector3(-dir.z, 0, dir.x);
                int b = v.Count;
                v.Add(-side * w); v.Add(side * w); v.Add(dir * lean + Vector3.up * h);
                t.AddRange(new[] { b, b + 2, b + 1, b, b + 1, b + 2 }); // both windings: visible from either side
            }
            return Flat(v, t, "tuft", false); // blades are double-sided already
        });

        static Mesh Cached(string key, System.Func<Mesh> make)
        {
            if (_cache.TryGetValue(key, out var m) && m != null) return m;
            m = make(); _cache[key] = m; return m;
        }

        // Unshares vertices so every triangle gets its own face normal (flat shading). Closed shapes
        // get every face turned outward from their centre (Unity's front faces wind clockwise; the
        // face normal is Cross(b - a, c - a)), so the winding of the source lists doesn't matter.
        static Mesh Flat(List<Vector3> v, List<int> t, string name, bool orient = true)
        {
            var centre = Vector3.zero;
            foreach (var p in v) centre += p;
            centre /= Mathf.Max(1, v.Count);
            if (orient)
                for (int i = 0; i < t.Count; i += 3)
                {
                    Vector3 a = v[t[i]], b = v[t[i + 1]], c = v[t[i + 2]];
                    if (Vector3.Dot(Vector3.Cross(b - a, c - a), (a + b + c) / 3f - centre) < 0) { int k = t[i + 1]; t[i + 1] = t[i + 2]; t[i + 2] = k; }
                }
            var verts = new Vector3[t.Count]; var tris = new int[t.Count];
            for (int i = 0; i < t.Count; i++) { verts[i] = v[t[i]]; tris[i] = i; }
            var m = new Mesh { name = "lowpoly_" + name, vertices = verts, triangles = tris };
            m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        static (List<Vector3>, List<int>) Icosphere(int subdiv)
        {
            float p = (1 + Mathf.Sqrt(5)) / 2;
            var v = new List<Vector3>
            {
                new Vector3(-1, p, 0), new Vector3(1, p, 0), new Vector3(-1, -p, 0), new Vector3(1, -p, 0),
                new Vector3(0, -1, p), new Vector3(0, 1, p), new Vector3(0, -1, -p), new Vector3(0, 1, -p),
                new Vector3(p, 0, -1), new Vector3(p, 0, 1), new Vector3(-p, 0, -1), new Vector3(-p, 0, 1),
            };
            for (int i = 0; i < v.Count; i++) v[i] = v[i].normalized;
            var t = new List<int> { 0,11,5, 0,5,1, 0,1,7, 0,7,10, 0,10,11, 1,5,9, 5,11,4, 11,10,2, 10,7,6, 7,1,8,
                                    3,9,4, 3,4,2, 3,2,6, 3,6,8, 3,8,9, 4,9,5, 2,4,11, 6,2,10, 8,6,7, 9,8,1 };
            for (int s = 0; s < subdiv; s++)
            {
                var mid = new Dictionary<long, int>(); var nt = new List<int>();
                int Mid(int a, int b)
                {
                    long k = a < b ? ((long)a << 32) | (uint)b : ((long)b << 32) | (uint)a;
                    if (mid.TryGetValue(k, out var i)) return i;
                    v.Add(((v[a] + v[b]) * 0.5f).normalized); mid[k] = v.Count - 1; return v.Count - 1;
                }
                for (int i = 0; i < t.Count; i += 3)
                {
                    int a = t[i], b = t[i + 1], c = t[i + 2], ab = Mid(a, b), bc = Mid(b, c), ca = Mid(c, a);
                    nt.AddRange(new[] { a, ab, ca, b, bc, ab, c, ca, bc, ab, bc, ca });
                }
                t = nt;
            }
            return (v, t);
        }
    }
}
