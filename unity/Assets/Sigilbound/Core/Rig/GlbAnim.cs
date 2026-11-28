// Reads a .glb's node hierarchy (rest TRS) and its animations (keyframe tracks per node) without Unity.
// Used by the editor's humanoid clip baker (Editor/HumanoidClipBaker.cs: the shared clips become Humanoid muscle
// clips during the CI build) and by the core tests (every shared clip drives every human bone a migrated hero maps).
// Values are in glTF space (right-handed, +Z front); ToUnity* convert the way glTFast does (X mirrored).
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sigilbound.Core
{
    public sealed class GlbAnim
    {
        public sealed class Node
        {
            public string Name; public int Parent = -1;
            public float[] T = { 0, 0, 0 }, R = { 0, 0, 0, 1 }, S = { 1, 1, 1 };
        }
        public sealed class Track
        {
            public int Node; public string Path;   // translation | rotation | scale
            public bool Step;
            public float[] Times; public float[] Values; // Values: Times.Length x (3 or 4)
            public int Width => Path == "rotation" ? 4 : 3;
        }
        public sealed class Clip
        {
            public string Name; public float Length;
            public readonly List<Track> Tracks = new List<Track>();
            public IEnumerable<string> AnimatedNodes(GlbAnim g) => Tracks.Select(t => g.Nodes[t.Node].Name).Distinct();
        }

        public readonly List<Node> Nodes = new List<Node>();
        public readonly List<Clip> Clips = new List<Clip>();
        public readonly List<int> Joints = new List<int>();

        public int Find(string name) => Nodes.FindIndex(n => n.Name == name);
        public Clip ClipNamed(string name) => Clips.FirstOrDefault(c => c.Name == name);

        public static GlbAnim Read(byte[] glb)
        {
            if (glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != 0x46546C67) throw new FormatException("not a binary glTF (.glb)");
            int jsonLen = (int)BitConverter.ToUInt32(glb, 12);
            var root = (Dictionary<string, object>)Json.Parse(Encoding.UTF8.GetString(glb, 20, jsonLen));
            int binStart = 20 + jsonLen + 8;
            var g = new GlbAnim();
            var nodes = Json.Arr(root, "nodes") ?? new List<object>();
            for (int i = 0; i < nodes.Count; i++)
            {
                var n = new Node { Name = Json.Str(nodes[i], "name", "node" + i) };
                if (Json.Has(nodes[i], "translation")) n.T = Floats(Json.Arr(nodes[i], "translation"));
                if (Json.Has(nodes[i], "rotation")) n.R = Floats(Json.Arr(nodes[i], "rotation"));
                if (Json.Has(nodes[i], "scale")) n.S = Floats(Json.Arr(nodes[i], "scale"));
                g.Nodes.Add(n);
            }
            for (int i = 0; i < nodes.Count; i++)
                foreach (var c in Json.Arr(nodes[i], "children") ?? new List<object>()) g.Nodes[(int)(double)c].Parent = i;
            var skins = Json.Arr(root, "skins") ?? new List<object>();
            if (skins.Count > 0) foreach (var j in Json.Arr(skins[0], "joints")) g.Joints.Add((int)(double)j);

            var accessors = Json.Arr(root, "accessors") ?? new List<object>();
            var views = Json.Arr(root, "bufferViews") ?? new List<object>();
            float[] Read(int acc)
            {
                var a = accessors[acc];
                if (Json.Int(a, "componentType") != 5126) throw new FormatException("animation accessor is not float");
                int count = Json.Int(a, "count");
                int width = Json.Str(a, "type") switch { "SCALAR" => 1, "VEC3" => 3, "VEC4" => 4, _ => throw new FormatException("accessor type") };
                var v = views[Json.Int(a, "bufferView")];
                int stride = Json.Int(v, "byteStride", width * 4);
                int off = binStart + Json.Int(v, "byteOffset") + Json.Int(a, "byteOffset");
                var o = new float[count * width];
                for (int i = 0; i < count; i++)
                    for (int k = 0; k < width; k++) o[i * width + k] = BitConverter.ToSingle(glb, off + i * stride + k * 4);
                return o;
            }
            foreach (var an in Json.Arr(root, "animations") ?? new List<object>())
            {
                var clip = new Clip { Name = Json.Str(an, "name", "clip" + g.Clips.Count) };
                var samplers = Json.Arr(an, "samplers");
                foreach (var ch in Json.Arr(an, "channels"))
                {
                    var tgt = Json.Obj(ch, "target");
                    if (!Json.Has(tgt, "node")) continue;
                    var s = samplers[Json.Int(ch, "sampler")];
                    var tr = new Track { Node = Json.Int(tgt, "node"), Path = Json.Str(tgt, "path"), Step = Json.Str(s, "interpolation", "LINEAR") == "STEP" };
                    if (tr.Path != "translation" && tr.Path != "rotation" && tr.Path != "scale") continue;
                    if (Json.Str(s, "interpolation", "LINEAR") == "CUBICSPLINE") throw new FormatException("cubic spline tracks are not supported");
                    tr.Times = Read(Json.Int(s, "input")); tr.Values = Read(Json.Int(s, "output"));
                    if (tr.Times.Length > 0) clip.Length = Math.Max(clip.Length, tr.Times[tr.Times.Length - 1]);
                    clip.Tracks.Add(tr);
                }
                g.Clips.Add(clip);
            }
            return g;
        }

        static float[] Floats(List<object> l) => l.Select(x => (float)(double)x).ToArray();

        // Value of a track at time t (clamped; linear, normalized lerp for rotations, or step).
        public static float[] Sample(Track tr, float t)
        {
            int w = tr.Width, n = tr.Times.Length;
            var o = new float[w];
            if (n == 0) return o;
            if (t <= tr.Times[0] || n == 1) { Array.Copy(tr.Values, 0, o, 0, w); return o; }
            if (t >= tr.Times[n - 1]) { Array.Copy(tr.Values, (n - 1) * w, o, 0, w); return o; }
            int hi = Array.BinarySearch(tr.Times, t); if (hi < 0) hi = ~hi;
            int lo = hi - 1;
            float f = tr.Step ? 0f : (t - tr.Times[lo]) / Math.Max(1e-6f, tr.Times[hi] - tr.Times[lo]);
            float sign = 1f;
            if (w == 4) { float d = 0; for (int k = 0; k < 4; k++) d += tr.Values[lo * 4 + k] * tr.Values[hi * 4 + k]; if (d < 0) sign = -1f; }
            for (int k = 0; k < w; k++) o[k] = tr.Values[lo * w + k] * (1 - f) + sign * tr.Values[hi * w + k] * f;
            if (w == 4) { float m = (float)Math.Sqrt(o[0] * o[0] + o[1] * o[1] + o[2] * o[2] + o[3] * o[3]); if (m > 1e-6f) for (int k = 0; k < 4; k++) o[k] /= m; }
            return o;
        }

        // glTFast's conversion: right-handed glTF to left-handed Unity by mirroring X.
        public static float[] ToUnityPos(float[] p) => new[] { -p[0], p[1], p[2] };
        public static float[] ToUnityRot(float[] q) => new[] { q[0], -q[1], -q[2], q[3] };

        // (name, parent) list for Core.HumanoidMap.
        public List<(string Name, int Parent)> Hierarchy() => Nodes.Select(n => (n.Name, n.Parent)).ToList();
    }
}
