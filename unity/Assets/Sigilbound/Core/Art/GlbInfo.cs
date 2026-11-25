// Reads the facts the art validation needs from a .glb without Unity: node hierarchy, skin joints,
// triangle counts, materials, embedded image sizes and animation names. Used by the core tests to
// run the import checks on the CC0 models in StreamingAssets, and mirrored in JavaScript by
// unity/Tools~/heroforge/glb-stats.mjs for checks before anything reaches Unity.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Sigilbound.Core
{
    public sealed class GlbInfo
    {
        public readonly List<(string Name, int Parent)> Nodes = new List<(string, int)>();
        public readonly List<int> Joints = new List<int>();          // node indices of the first skin
        public readonly List<string> Materials = new List<string>();
        public readonly List<string> Animations = new List<string>();
        public readonly List<(string Name, int W, int H)> Images = new List<(string, int, int)>();
        public int Triangles, SkinnedMeshes, Skins;
        public double MinY = double.MaxValue, MaxY = double.MinValue; // from POSITION accessor bounds (mesh space)

        public static GlbInfo Read(byte[] glb)
        {
            if (glb.Length < 20 || BitConverter.ToUInt32(glb, 0) != 0x46546C67) throw new FormatException("not a binary glTF (.glb)");
            int jsonLen = (int)BitConverter.ToUInt32(glb, 12);
            var root = (Dictionary<string, object>)Json.Parse(Encoding.UTF8.GetString(glb, 20, jsonLen));
            int binStart = 20 + jsonLen + 8; // BIN chunk header follows the JSON chunk
            var info = new GlbInfo();

            var nodes = Json.Arr(root, "nodes") ?? new List<object>();
            var parent = Enumerable.Repeat(-1, nodes.Count).ToArray();
            for (int i = 0; i < nodes.Count; i++)
                foreach (var c in Json.Arr(nodes[i], "children") ?? new List<object>()) parent[(int)(double)c] = i;
            for (int i = 0; i < nodes.Count; i++) info.Nodes.Add((Json.Str(nodes[i], "name", "node" + i), parent[i]));

            var skins = Json.Arr(root, "skins") ?? new List<object>();
            info.Skins = skins.Count;
            if (skins.Count > 0) foreach (var j in Json.Arr(skins[0], "joints")) info.Joints.Add((int)(double)j);

            var accessors = Json.Arr(root, "accessors") ?? new List<object>();
            var meshes = Json.Arr(root, "meshes") ?? new List<object>();
            // Count each mesh once per node that instances it (a mesh used twice draws twice).
            foreach (var n in nodes)
            {
                if (!Json.Has(n, "mesh")) continue;
                var mesh = meshes[Json.Int(n, "mesh")];
                if (Json.Has(n, "skin")) info.SkinnedMeshes++;
                foreach (var p in Json.Arr(mesh, "primitives"))
                {
                    int mode = Json.Int(p, "mode", 4);
                    var attrs = Json.Obj(p, "attributes");
                    int verts = attrs != null && attrs.ContainsKey("POSITION") ? Json.Int(accessors[(int)(double)attrs["POSITION"]], "count") : 0;
                    int idx = Json.Has(p, "indices") ? Json.Int(accessors[Json.Int(p, "indices")], "count") : verts;
                    info.Triangles += mode == 4 ? idx / 3 : mode == 5 || mode == 6 ? Math.Max(0, idx - 2) : 0;
                    if (attrs != null && attrs.ContainsKey("POSITION"))
                    {
                        var acc = accessors[(int)(double)attrs["POSITION"]];
                        var mn = Json.Arr(acc, "min"); var mx = Json.Arr(acc, "max");
                        if (mn != null && mx != null) { info.MinY = Math.Min(info.MinY, (double)mn[1]); info.MaxY = Math.Max(info.MaxY, (double)mx[1]); }
                    }
                }
            }
            foreach (var m in Json.Arr(root, "materials") ?? new List<object>()) info.Materials.Add(Json.Str(m, "name"));
            foreach (var a in Json.Arr(root, "animations") ?? new List<object>()) info.Animations.Add(Json.Str(a, "name"));

            var views = Json.Arr(root, "bufferViews") ?? new List<object>();
            var images = Json.Arr(root, "images") ?? new List<object>();
            for (int i = 0; i < images.Count; i++)
            {
                var img = images[i];
                string name = Json.Str(img, "name", "image" + i);
                if (!Json.Has(img, "bufferView")) { info.Images.Add((name, 0, 0)); continue; }
                var v = views[Json.Int(img, "bufferView")];
                int off = binStart + Json.Int(v, "byteOffset"), len = Json.Int(v, "byteLength");
                var (w, h) = ImageSize(glb, off, len);
                info.Images.Add((name, w, h));
            }
            return info;
        }

        // Width and height from a PNG or JPEG header (0, 0 if unknown).
        public static (int W, int H) ImageSize(byte[] b, int off, int len)
        {
            int end = Math.Min(b.Length, off + len);
            if (len > 24 && b[off] == 0x89 && b[off + 1] == 0x50 && b[off + 2] == 0x4E && b[off + 3] == 0x47)
                return (BE32(b, off + 16), BE32(b, off + 20));
            if (len > 4 && b[off] == 0xFF && b[off + 1] == 0xD8)
            {
                int p = off + 2;
                while (p + 9 < end)
                {
                    if (b[p] != 0xFF) { p++; continue; }
                    byte marker = b[p + 1];
                    int segLen = (b[p + 2] << 8) | b[p + 3];
                    if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                        return ((b[p + 7] << 8) | b[p + 8], (b[p + 5] << 8) | b[p + 6]);
                    p += 2 + segLen;
                }
            }
            return (0, 0);
        }
        static int BE32(byte[] b, int o) => (b[o] << 24) | (b[o + 1] << 16) | (b[o + 2] << 8) | b[o + 3];

        // The joints as a (name, parent) list re-indexed to the joint set (parents outside the skin = -1).
        public List<(string Name, int Parent)> JointHierarchy()
        {
            var idx = new Dictionary<int, int>();
            for (int i = 0; i < Joints.Count; i++) idx[Joints[i]] = i;
            return Joints.Select(j =>
            {
                int p = Nodes[j].Parent;
                while (p >= 0 && !idx.ContainsKey(p)) p = Nodes[p].Parent;
                return (Nodes[j].Name, p >= 0 ? idx[p] : -1);
            }).ToList();
        }

        // The stats the validation reads, for one LOD file (the editor tool measures the same in Unity).
        public HeroAssetStats ToStats(string heroId, string source)
        {
            var s = new HeroAssetStats { HeroId = heroId, Source = source, Bones = Joints.Count, Materials = Materials.Count, SkinnedRenderers = SkinnedMeshes };
            s.LodTris[0] = Triangles;
            // Height stays 0 here: accessor bounds are in quantized mesh units (KHR_mesh_quantization)
            // before node scales, so only Unity's renderer bounds give metres.
            foreach (var im in Images) s.Textures.Add(im);
            var map = HumanoidMap.Map(JointHierarchy());
            s.MissingHumanBones.AddRange(HumanoidMap.Missing(map));
            s.HumanoidProblems.AddRange(HumanoidMap.HierarchyProblems(map, JointHierarchy()));
            var clips = HeroClips.Resolve(Animations);
            foreach (var c in clips.Keys) s.Clips.Add(c);
            s.AnimBackend = clips.Count > 0 ? "embedded" : "library";
            return s;
        }
    }
}
